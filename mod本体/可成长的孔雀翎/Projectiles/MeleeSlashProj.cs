using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 近战形态·斩击 hitbox（原型·ES 流 MVP）。
    /// 短暂存在、无飞行速度；命中目标时给持有者叠「精准」层数；
    /// 视觉为 additive 扇形刃光（无贴图内容，纯程序绘制）。
    /// </summary>
    public class MeleeSlashProj : ModProjectile
    {
        public override string Texture => "可成长的孔雀翎/Textures/Pixel";

        /// <summary>玩家朝向（1=右 / -1=左）。</summary>
        public int SlashDir = 1;

        public override void SetDefaults()
        {
            Projectile.width = 64;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 7;
            Projectile.ignoreWater = true;
            Projectile.DamageType = MindDamageClass.Instance;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active) return;
            var mp = owner.GetModPlayer<MalachitePlayer>();
            mp.PrecisionStacks = Math.Min(MalachiteMelee.PrecisionMaxStacks, mp.PrecisionStacks + 1);
            mp.PrecisionTimer = MalachiteMelee.PrecisionDuration;

            // 命中白闪（打击感，限流）
            if (EffectLimiterSystem.CanSpawnEffect(2, 60))
            {
                for (int i = 0; i < 3; i++)
                {
                    Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(14f, 14f),
                        DustID.TintableDust, Main.rand.NextVector2Circular(4f, 4f), 0,
                        Main.rand.NextBool(2) ? MalachitePalette.White : MalachitePalette.PrimaryGreen,
                        Main.rand.NextFloat(0.6f, 1.0f));
                    d.noGravity = true;
                    d.fadeIn = 0.4f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float life = MathHelper.Clamp(1f - (Projectile.timeLeft - 1) / 6f, 0f, 1f); // 0→1 展开
            float fade = 1f - life * 0.55f; // 尾部淡出
            Vector2 center = Projectile.Center - Main.screenPosition;

            AdditiveLayer.Begin();
            // 扇形刃光：9 条沿角度的光带（参考本项目射线光带绘制范式）
            const int rays = 9;
            const float spreadDeg = 100f;
            int facing = SlashDir;
            for (int i = 0; i < rays; i++)
            {
                float t = rays == 1 ? 0.5f : i / (float)(rays - 1);
                float deg = (-spreadDeg * 0.5f + spreadDeg * t);
                // 朝向：右=绕+X，左=绕-X（镜像角度）
                float ang;
                if (facing >= 0) ang = MathHelper.ToRadians(deg);
                else ang = MathHelper.Pi - MathHelper.ToRadians(deg);
                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                float len = (52f - 20f * life) * (0.9f + 0.2f * t);
                float w = 1.6f + 2.4f * (1f - Math.Abs(t - 0.5f) * 2f); // 中间粗

                Color col = MalachitePalette.White * (fade * 0.55f);
                Main.spriteBatch.Draw(AdditiveLayer.Pixel, center, null, col, dir.ToRotation(),
                    new Vector2(0f, 0.5f), new Vector2(len, w * 1.6f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(AdditiveLayer.Pixel, center, null, MalachitePalette.PrimaryGreen * (fade * 0.85f), dir.ToRotation(),
                    new Vector2(0f, 0.5f), new Vector2(len, w), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(AdditiveLayer.Pixel, center, null, MalachitePalette.GreenBright * (fade * 0.9f), dir.ToRotation(),
                    new Vector2(0f, 0.5f), new Vector2(len * 0.62f, w * 0.4f), SpriteEffects.None, 0f);
            }
            AdditiveLayer.End();
            return false;
        }
    }
}
