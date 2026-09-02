// 代码来源与合规署名：
// - 射线实现思路（extraUpdates 极速、TrailCacheLength 残影、命中爆炸）参考自：
//   CalamityModPublic（Azafure, LLC 专有许可，官方允许作为开发参考）—— Projectiles/Rogue/MalachiteBolt.cs
//   https://github.com/CalamityTeam/CalamityModPublic
// - 三层光带配色（Core/Glow/Aura）与沿束散粒子思路参考自：
//   CalamityOverhaul（MIT License, Copyright (c) hocha113）—— Content/LegendWeapon/SHPCLegend/Cyberspaces/CyberPrismLaserProj.cs
//   https://github.com/hocha113/CalamityOverhaul
// 本文件为自主实现，未直接复制上述仓库源码。
using System;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 孔雀柳刃·潜伏射线（真射线，参考灾厄 MalachiteBolt 范式）：
    /// - extraUpdates 极速（视觉飞成光线的关键）
    /// - TrailCacheLength 残影 + 自绘 afterimages 连成光带
    /// - CWR 三层配色思想：白芯 / 主色发光 / 外晕，additive
    /// - 命中：穿透耗尽时爆炸 + 火花
    /// </summary>
    public class MalachiteBolt : ModProjectile
    {
        public override string Texture => "可成长的孔雀翎/Textures/MalachiteBoltTex";

        private bool _initialized = false;
        private bool _exploded = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;   // 残影长度
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 6;
            Projectile.extraUpdates = 8;                     // 极速射线感
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            Projectile.DamageType = ModContent.GetInstance<MalachiteDamageClass>();
            Projectile.tileCollide = false;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!_initialized)
            {
                _initialized = true;
                // 随阶段提升穿透
                int stage = ProgressSystem.GetStage();
                int extraPierce = stage >= 13 ? 6 : (stage >= 8 ? 4 : (stage >= 4 ? 2 : 1));
                Projectile.penetrate += extraPierce;
            }

            // 渐显（灾厄范式：alpha 255 → 100）
            Projectile.alpha = Math.Max(100, Projectile.alpha - 4);

            // 朝向飞行方向
            if (Projectile.velocity.LengthSquared() > 0.1f)
                Projectile.rotation = Projectile.velocity.ToRotation();

            // 沿束散光尘（参考 CWR SpawnLaserParticles：沿路径散布）
            if (Main.rand.NextBool(2) && EffectLimiterSystem.CanSpawnEffect(1, 100))
            {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustPerfect(pos, DustID.TintableDust,
                    -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(1.5f, 1.5f),
                    0, Main.rand.NextBool(3) ? MalachitePalette.White : MalachitePalette.AccentCyan,
                    Main.rand.NextFloat(0.5f, 0.9f));
                d.noGravity = true;
                d.fadeIn = 0.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.31f, 0.85f, 0.91f);
        }

        /// <summary>残影 + 三层光带绘制（射线核心视觉）。</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 texCenter = tex.Size() / 2f;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            AdditiveLayer.Begin();

            // 1) 残影连成光带（沿旧位置，透明度递减）
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + new Vector2(Projectile.width / 2f, Projectile.height / 2f) - Main.screenPosition;
                Color trailCol = MalachitePalette.AccentCyan * (t * 0.45f);
                Main.spriteBatch.Draw(tex, pos, null, trailCol, Projectile.rotation, texCenter,
                    Projectile.scale * (0.5f + 0.5f * t), SpriteEffects.None, 0f);
            }

            // 2) 头部三层光晕（白芯 + 主色发光 + 外晕）
            Color auraCol = MalachitePalette.AccentCyan * 0.30f;
            Color glowCol = MalachitePalette.AccentCyan * 0.75f;
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, auraCol, 0f,
                new Vector2(0.5f), 16f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, glowCol, 0f,
                new Vector2(0.5f), 8f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, MalachitePalette.White * 0.9f, 0f,
                new Vector2(0.5f), 3f, SpriteEffects.None, 0f);

            // 3) 沿速度方向的光带（CWR 三层：Core/Glow/Aura）
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, auraCol, dir.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(140f, 14f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, glowCol, dir.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(120f, 7f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, MalachitePalette.White * 0.55f, dir.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(100f, 2.5f), SpriteEffects.None, 0f);

            AdditiveLayer.End();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 基础：中毒
            target.AddBuff(BuffID.Poisoned, 180);

            // 命中火花（参考 CWR：圆周迸发粒子）
            if (EffectLimiterSystem.CanSpawnEffect(3, 110))
            {
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.25f, Pitch = 0.4f }, target.Center);
                for (int i = 0; i < 5; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f) * Main.rand.NextFloat(1f, 2f);
                    Dust d = Dust.NewDustPerfect(target.Center + vel, DustID.TintableDust, vel, 0,
                        Main.rand.NextBool(2) ? MalachitePalette.White : MalachitePalette.AccentCyan,
                        Main.rand.NextFloat(0.5f, 1.1f));
                    d.noGravity = true;
                    d.fadeIn = 0.3f;
                }
            }

            // 穿透耗尽时爆炸（参考灾厄：AOE + 大量 Dust）
            if (Projectile.penetrate <= 1 && !_exploded)
            {
                _exploded = true;
                Explode(target.Center);
            }
        }

        private void Explode(Vector2 center)
        {
            if (!EffectLimiterSystem.CanSpawnEffect(5, 140)) return;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.2f }, center);
            for (int i = 0; i < 30; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(10f, 10f), DustID.TintableDust, vel, 0,
                    i % 3 == 0 ? MalachitePalette.White : MalachitePalette.AccentCyan,
                    Main.rand.NextFloat(0.7f, 1.5f));
                d.noGravity = true;
                d.fadeIn = 0.5f;
            }
            // 范围内溅射伤害
            if (Projectile.owner == Main.myPlayer)
            {
                foreach (NPC n in Main.ActiveNPCs)
                {
                    if (n.CanBeChasedBy() && n.active && !n.friendly && n.DistanceSQ(center) < 110f * 110f)
                        n.SimpleStrikeNPC((int)(Projectile.damage * 0.5f), 0, false, 0, Projectile.DamageType, true, Projectile.owner, true);
                }
            }
        }
    }
}
