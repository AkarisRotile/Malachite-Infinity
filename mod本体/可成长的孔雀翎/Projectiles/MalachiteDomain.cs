// 代码来源与合规署名：
// - "领域展开"灵感与法阵/区域效果设计参考自：
//   CalamityOverhaul（MIT License, Copyright (c) hocha113）—— Content/LegendWeapon/SHPCLegend/Cyberspaces/ 目录
//   https://github.com/hocha113/CalamityOverhaul
// 本文件为自主实现（状态机 + 范围检测 + additive 法阵绘制），未直接复制上述仓库源码。
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
    /// 领域展开（Domain Expansion）原型：以释放点为中心的法阵领域。
    /// 状态机（ai[0]）：展开（0~30）→ 维持（30~300）→ 收尾（300~330）。
    /// 效果：范围内敌人减速；范围内玩家获得迅捷；边缘金色光尘流；收尾光尘迸发。
    /// 视觉：additive 法阵贴图（旋转 + 呼吸缩放）+ 中心光点。
    /// </summary>
    public class MalachiteDomain : ModProjectile
    {
        public override string Texture => "可成长的孔雀翎/Textures/MalachiteDomainTex";

        /// <summary>领域半径（像素）。</summary>
        public const float Radius = 320f;
        /// <summary>展开帧数。</summary>
        private const int ExpandFrames = 30;
        /// <summary>总持续时间（帧）。</summary>
        private const int TotalFrames = 330;

        private int _effectTimer = 0;

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalFrames;
            Projectile.penetrate = -1;
            Projectile.alpha = 255; // 初始透明，展开时显现
        }

        public override void AI()
        {
            // 位置固定于释放点
            Projectile.velocity = Vector2.Zero;

            int t = Projectile.timeLeft;
            int elapsed = TotalFrames - t;

            // 展开阶段 alpha 渐显
            if (elapsed < ExpandFrames)
                Projectile.alpha = (int)(255f * (1f - elapsed / (float)ExpandFrames));
            else
                Projectile.alpha = 0;

            // 范围效果（每 20 帧）
            if (++_effectTimer >= 20)
            {
                _effectTimer = 0;
                float radiusSq = Radius * Radius;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && npc.DistanceSQ(Projectile.Center) < radiusSq)
                        npc.AddBuff(BuffID.Slow, 120);      // 领域内敌人减速
                }
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (p.active && !p.dead && p.DistanceSQ(Projectile.Center) < radiusSq)
                        p.AddBuff(BuffID.Swiftness, 120);   // 领域内玩家迅捷
                }
            }

            // 边缘金色光尘流（限流）
            if (Main.rand.NextBool(3) && EffectLimiterSystem.CanSpawnEffect(1, 120))
            {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 edgePos = Projectile.Center + ang.ToRotationVector2() * Radius;
                Dust d = Dust.NewDustPerfect(edgePos, DustID.TintableDust,
                    -ang.ToRotationVector2() * Main.rand.NextFloat(1f, 2.5f),
                    0, MalachitePalette.AccentGold, Main.rand.NextFloat(0.7f, 1.2f));
                d.noGravity = true;
                d.fadeIn = 0.4f;
            }

            Lighting.AddLight(Projectile.Center, 0.24f, 0.86f, 0.52f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.alpha >= 255) return false;

            Texture2D tex = ModContent.Request<Texture2D>("可成长的孔雀翎/Textures/MalachiteDomainTex").Value;

            // 呼吸缩放 + 缓慢旋转（贴图 256px = 半径 128，放大到领域半径）
            float breathe = 1f + 0.04f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f);
            float scale = (Radius / 128f) * breathe;
            float rotation = Main.GlobalTimeWrappedHourly * 0.25f;

            // 展开阶段：法阵从小放大出现
            int elapsed = TotalFrames - Projectile.timeLeft;
            if (elapsed < ExpandFrames)
                scale *= elapsed / (float)ExpandFrames;

            // additive 发光绘制
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float alpha = 1f - Projectile.alpha / 255f;

            AdditiveLayer.Begin();
            Main.spriteBatch.Draw(tex, screenPos, null, Color.White * (0.55f * alpha), rotation,
                tex.Size() / 2f, scale, SpriteEffects.None, 0f);
            // 中心光点
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null,
                MalachitePalette.GreenBright * (0.8f * alpha), 0f,
                new Vector2(0.5f), 6f, SpriteEffects.None, 0f);
            AdditiveLayer.End();

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // 收尾：光尘迸发
            if (EffectLimiterSystem.CanSpawnEffect(5, 150))
            {
                for (int i = 0; i < 24; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f) * Main.rand.NextFloat(3f, 7f);
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.6f, Radius * 0.6f),
                        DustID.TintableDust, vel, 0,
                        i % 3 == 0 ? MalachitePalette.AccentGold : MalachitePalette.PrimaryGreen,
                        Main.rand.NextFloat(0.8f, 1.6f));
                    d.noGravity = true;
                    d.fadeIn = 0.5f;
                }
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
        }
    }
}
