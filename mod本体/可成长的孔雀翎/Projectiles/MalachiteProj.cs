// 代码来源与合规署名：
// - 残影轨迹（afterimages）与拖尾 Dust 思路参考自：
//   CalamityModPublic（Azafure, LLC 专有许可，官方允许作为开发参考）—— Projectiles/Rogue/MalachiteBolt.cs、MalachiteProj.cs
//   https://github.com/CalamityTeam/CalamityModPublic
// - 三层光带配色（Core/Glow/Aura）思路参考自：
//   CalamityOverhaul（MIT License, Copyright (c) hocha113）—— Content/LegendWeapon/SHPCLegend/Cyberspaces/CyberPrismLaserProj.cs
//   https://github.com/hocha113/CalamityOverhaul
// 本文件为自主实现，未直接复制上述仓库源码。
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 孔雀柳刃·普攻飞刀弹幕（高分辨率像素画贴图 + Dust 光尘）。
    /// 外观：翠绿羽刃像素画（刀尖朝右），飞行时拖出细碎光尘；潜伏态金色光尘。
    /// 逻辑：直线飞行（阶段4起无重力）、巡猎天赋追踪、命中附加中毒/瘟疫。
    /// </summary>
    public class MalachiteProj : ModProjectile
    {
        public override string Texture => "可成长的孔雀翎/Textures/MalachiteProjTex";

        private int _targetNPCIndex = -1;
        private bool _initialized = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;    // 残影（灾厄范式）
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.extraUpdates = 3;                     // 更快的飞行手感
            Projectile.friendly = true;
            Projectile.DamageType = ModContent.GetInstance<MalachiteDamageClass>();
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.usesIDStaticNPCImmunity = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
            Projectile.ignoreWater = false;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!_initialized)
            {
                _initialized = true;
                var mp = player.GetModPlayer<MalachitePlayer>();
                // 天赋 0（轻灵）：初始速度 +80%
                if (mp.ActiveSigils.Contains(0) && mp.ActiveSigils.Contains(1) && !StealthSystem.IsStealthStrike(Projectile))
                    Projectile.velocity *= 1.8f;
            }

            // 阶段 >= 4：解除弹道下坠（阶段4前手动模拟重力）
            if (ProgressSystem.GetStage() < 4)
                Projectile.velocity.Y += 0.15f;

            // 旋转跟随飞行方向（贴图竖直刀尖朝上，+90° 使刃身顺飞行方向，灾厄范式）
            if (Projectile.velocity.LengthSquared() > 0.1f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 巡猎天赋（ID=1）：非潜伏攻击自动追踪
            var mp2 = player.GetModPlayer<MalachitePlayer>();
            if (mp2.ActiveSigils.Contains(1) && !StealthSystem.IsStealthStrike(Projectile))
                UpdateTrackingTarget();

            // Dust 光尘拖尾（引擎原生粒子，限流）
            bool stealth = StealthSystem.IsStealthStrike(Projectile);
            Color dustColor = stealth ? MalachitePalette.AccentGold : MalachitePalette.PrimaryGreen;
            if (Main.rand.NextBool(2) && EffectLimiterSystem.CanSpawnEffect(1, 90))
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    DustID.TintableDust,
                    -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    0, dustColor, Main.rand.NextFloat(0.6f, 1.1f));
                d.noGravity = true;
                d.fadeIn = 0.4f;
            }

            Lighting.AddLight(Projectile.Center, 0.24f, 0.86f, 0.52f); // 主色 #3DDC84 归一化
        }

        private void UpdateTrackingTarget()
        {
            NPC target = null;

            if (_targetNPCIndex >= 0 && _targetNPCIndex < Main.maxNPCs)
            {
                NPC cached = Main.npc[_targetNPCIndex];
                if (cached.active && cached.CanBeChasedBy() && Projectile.DistanceSQ(cached.Center) < 1400f * 1400f)
                    target = cached;
                else
                    _targetNPCIndex = -1;
            }

            if (target == null && Projectile.timeLeft % 3 == 0)
            {
                float closest = 1200f * 1200f;
                foreach (NPC npc in Main.ActiveNPCs)
                {
                    if (npc.CanBeChasedBy())
                    {
                        float dist = Projectile.DistanceSQ(npc.Center);
                        if (dist < closest && Collision.CanHit(Projectile.Center, 1, 1, npc.Center, 1, 1))
                        {
                            closest = dist;
                            target = npc;
                            _targetNPCIndex = npc.whoAmI;
                        }
                    }
                }
            }

            if (target != null)
            {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Math.Max(16f, Projectile.velocity.Length());
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.035f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 基础：中毒 + 瘟疫（击杀瘟疫使者后）
            target.AddBuff(BuffID.Poisoned, 180);
            if (ProgressSystem.DownedPlaguebringer && MalachiteCache.PlagueBuff != 0)
                target.AddBuff(MalachiteCache.PlagueBuff, 180);

            // 命中光尘迸发（限流）
            if (EffectLimiterSystem.CanSpawnEffect(3, 100))
            {
                for (int i = 0; i < 4; i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        target.Center + Main.rand.NextVector2Circular(target.width / 2f, target.height / 2f),
                        DustID.TintableDust,
                        Main.rand.NextVector2Circular(3f, 3f),
                        0, MalachitePalette.GreenBright, Main.rand.NextFloat(0.5f, 0.9f));
                    d.noGravity = true;
                    d.fadeIn = 0.3f;
                }
            }

            // 天赋 6（冰封）：附加霜火 + 冷冻
            Player player = Main.player[Projectile.owner];
            if (player.active && player.GetModPlayer<MalachitePlayer>().ActiveSigils.Contains(6))
            {
                if (player.GetModPlayer<MalachitePlayer>().ActiveSigils.Contains(1) && !StealthSystem.IsStealthStrike(Projectile))
                {
                    target.AddBuff(BuffID.Frostburn, 180);
                    target.AddBuff(BuffID.Chilled, 180);
                }
            }
        }

        /// <summary>残影轨迹 + 三层光晕（参考灾厄 afterimages 与 CWR 配色分层）。</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 texCenter = tex.Size() / 2f;
            bool stealth = StealthSystem.IsStealthStrike(Projectile);
            Color main = stealth ? MalachitePalette.AccentGold : MalachitePalette.PrimaryGreen;

            AdditiveLayer.Begin();

            // 1) 残影连成轨迹（透明度沿旧位置递减）
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + new Vector2(Projectile.width / 2f, Projectile.height / 2f) - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, main * (t * 0.4f), Projectile.rotation, texCenter,
                    Projectile.scale * (0.5f + 0.5f * t), SpriteEffects.None, 0f);
            }

            // 2) 头部三层光晕（外晕 / 发光 / 白芯）
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, main * 0.30f, 0f,
                new Vector2(0.5f), 12f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, main * 0.75f, 0f,
                new Vector2(0.5f), 6f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, MalachitePalette.White * 0.9f, 0f,
                new Vector2(0.5f), 2.5f, SpriteEffects.None, 0f);

            // 3) 尾部短光带（CWR 三层，长度较短）
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, main * 0.25f, dir.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(60f, 10f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, main * 0.6f, dir.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(48f, 5f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, screenPos, null, MalachitePalette.White * 0.5f, dir.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(38f, 2f), SpriteEffects.None, 0f);

            AdditiveLayer.End();
            return false;
        }
    }
}
