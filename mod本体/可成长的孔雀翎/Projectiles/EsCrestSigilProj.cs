// 代码来源与合规署名：
// - 机制灵感（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES 的"纹章术"——
//   挥击后在空中留下延迟引爆的晶体纹章。绘制为原版基础几何 + Additive 多层发光，配色仅用 MalachitePalette。
// 本文件为自主实现（无外部参考源码）。
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// ES 空间纹章（Crest Arts）：20 帧三阶段状态机。
    /// 0~11 帧：无判定，悬停+向心聚能粒子（GemEmerald）；
    /// 12 帧：引爆——音效(Item27)+破甲(Ichor)+微浮空+震屏+二次伤害（75% 主斩，暴击+15%）；
    /// 13~20 帧：取消判定，膨胀至 1.6 倍并淡出。
    /// </summary>
    public class EsCrestSigilProj : ModProjectile
    {
        public override string Texture => "可成长的孔雀翎/Textures/Pixel"; // 纯几何绘制，贴图占位

        private int Age => (int)Projectile.localAI[0];
        private bool Gold => Projectile.localAI[1] >= 3f; // 满月终结等重招用金纹章

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            Projectile.ignoreWater = true;
            Projectile.DamageType = MindDamageClass.Instance;
        }

        public override void AI()
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
            int age = Age;
            if (age == 0) Projectile.localAI[1] = Projectile.ai[0]; // 0=常规 / 3=终结(金色)
            if (age >= 20) { Projectile.Kill(); return; }

            if (age == 12) Explode();

            // 0~11：向心聚能粒子
            if (age <= 11 && age % 3 == 0 && EffectLimiterSystem.CanSpawnEffect(1, 60))
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 off = Main.rand.NextVector2Circular(34f, 34f);
                    Vector2 from = Projectile.Center + off;
                    Dust d = Dust.NewDustPerfect(from, DustID.GemEmerald, -off.SafeNormalize(Vector2.UnitY) * 2.6f,
                        0, Color.White, Main.rand.NextFloat(0.8f, 1.3f));
                    d.noGravity = true;
                    d.fadeIn = 0.5f;
                }
            }

            // 13~20：取消判定（Engine 一帧判定仅限引爆帧的主动范围打击，见 Explode）
            if (age >= 13) Projectile.friendly = false;

            Projectile.rotation += 0.05f; // 缓慢自转
            Projectile.localAI[0]++;
        }

        /// <summary>引爆帧：主动范围判定 + 反馈，结束后立刻取消 friendly 防引擎重复结算。</summary>
        private void Explode()
        {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.85f, Pitch = 0.1f }, Projectile.Center);
            ScreenShakeSystem.Shake(8f);

            int dmg = Math.Max(1, (int)(Projectile.damage * 0.75f)); // 主斩 75% 的二次打击
            float critChance = 15f;
            Player plr = Main.player[Projectile.owner];
            if (plr != null && plr.active) critChance += plr.GetTotalCritChance(MindDamageClass.Instance);

            Rectangle zone = new Rectangle((int)Projectile.Center.X - 65, (int)Projectile.Center.Y - 65, 130, 130);
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || !npc.Hitbox.Intersects(zone)) continue;
                NPC.HitInfo hi = new NPC.HitInfo
                {
                    Damage = dmg,
                    Knockback = 3f,
                    HitDirection = npc.Center.X < Projectile.Center.X ? 1 : -1,
                    Crit = Main.rand.NextFloat(100f) < critChance,
                    DamageType = MindDamageClass.Instance,
                };
                npc.StrikeNPC(hi);
                npc.AddBuff(BuffID.Ichor, 240); // 破甲（降防御）
                npc.velocity.Y -= 4f;            // 微浮空
                npc.netUpdate = true;
                if (EffectLimiterSystem.CanSpawnEffect(2, 60))
                {
                    for (int k = 0; k < 5; k++)
                        EffectLimiterSystem.SpawnSpark(npc.Center + Main.rand.NextVector2Circular(14f, 14f),
                            Main.rand.NextVector2Circular(3f, 3f),
                            Gold ? MalachitePalette.AccentGold : MalachitePalette.GreenBright,
                            Main.rand.NextFloat(0.9f, 1.5f), 16);
                }
            }
            Projectile.friendly = false; // 手动结算后关闭，避免同帧引擎再判
        }

        private static void Seg(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 1f || width < 0.5f) return;
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, mid, null, color, d.ToRotation(),
                new Vector2(0.5f, 0.5f), new Vector2(len, Math.Max(1f, width)), SpriteEffects.None, 0f);
        }

        private static void Quad(Vector2 center, float size, float rot, Color color)
        {
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, center - Main.screenPosition, null, color, rot,
                new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int age = Age;
            if (age < 0) return false;
            Color tint = Gold ? MalachitePalette.AccentGold : MalachitePalette.GreenBright;
            Vector2 c = Projectile.Center;

            // 阶段参数：0~11 成形(0.6→1.0)，12 引爆满形，13~20 膨胀 1.0→1.6 淡出
            float grow = age <= 11 ? MathHelper.Lerp(0.6f, 1.0f, age / 11f)
                : age <= 12 ? 1.0f : MathHelper.Lerp(1.0f, 1.6f, (age - 13) / 7f);
            float alpha = age <= 12 ? 1f : MathHelper.Lerp(1f, 0f, (age - 13) / 7f);
            float rot = Projectile.rotation;

            // Additive 四层复合几何
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // ① 底层能量底盘（三层套叠方片，缓转）
            Quad(c, 88f * grow, rot * 0.4f, tint * (0.10f * alpha));
            Quad(c, 66f * grow, rot * 0.7f + 0.785f, tint * (0.14f * alpha));
            Quad(c, 46f * grow, rot * 1.0f, tint * (0.20f * alpha));

            // ② 中层正交反向旋转双菱形（纹章本体）
            float L = 34f * grow;
            Vector2 d1 = new Vector2((float)Math.Cos(rot), (float)Math.Sin(rot)) * L;
            Vector2 d2 = new Vector2(-d1.Y, d1.X);
            Seg(c + d1, c - d1, 4f, tint * (0.75f * alpha));
            Seg(c + d2, c - d2, 4f, tint * (0.55f * alpha));
            float r2 = -rot * 1.4f;
            Vector2 e1 = new Vector2((float)Math.Cos(r2), (float)Math.Sin(r2)) * L * 0.72f;
            Vector2 e2 = new Vector2(-e1.Y, e1.X);
            Seg(c + e1, c - e1, 3f, MalachitePalette.AccentGold * (0.5f * alpha));
            Seg(c + e2, c - e2, 3f, Color.White * (0.35f * alpha));

            // ③ 核心白热核（脉动）
            float pulse = 1f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.9f);
            Quad(c, 16f * grow * pulse, rot, Color.White * (0.9f * alpha));
            Quad(c, 9f * grow * pulse, rot + 0.785f, tint * (0.8f * alpha));

            // ④ 引爆帧：十字激光切痕 + 扩散环
            if (age == 12 || age == 13)
            {
                float xa = age == 12 ? 1f : 0.5f;
                float xl = 96f * grow;
                Seg(c - Vector2.UnitX * xl, c + Vector2.UnitX * xl, 3f, Color.White * xa);
                Seg(c - Vector2.UnitY * xl, c + Vector2.UnitY * xl, 3f, Color.White * xa);
                Vector2 diag = Vector2.Normalize(new Vector2(1f, 1f)) * xl;
                Seg(c - diag, c + diag, 2f, tint * xa);
                Seg(c - new Vector2(-diag.Y, diag.X), c + new Vector2(-diag.Y, diag.X), 2f, tint * xa);
            }
            if (age >= 12 && age <= 15)
            {
                float ra = 1f - (age - 12) / 4f;
                float rr = MathHelper.Lerp(26f, 96f, (age - 12) / 4f);
                for (int s = 0; s < 12; s++)
                {
                    float a0 = MathHelper.TwoPi * s / 12f;
                    float a1 = MathHelper.TwoPi * (s + 1) / 12f;
                    Seg(c + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * rr,
                        c + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * rr, 2.5f, tint * ra);
                }
            }

            // 严格还原默认批次状态
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}