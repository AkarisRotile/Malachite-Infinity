// 本文件为自主实现（无外部参考源码）。
// 绘制复用 Core\Vfx\VfxDraw.cs 的 DrawCrestSigil（与 EsCrestSigilProj 同一视觉语言，小纹章档）。
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 灼翎纹章（纹章节点产物）：绕角色旋转的小纹章，**纯视觉载体**——不判定、不受伤、不挡弹。
    /// <para/>契约（2026-09-22 用户拍板）：解锁节点后恒常存在 2 枚（相位相差 180°）；
    /// 每 3 次普攻由 <see cref="CrestNodes.SignalBeams"/> 下发一次信号，各朝鼠标指针方向发射一道射线。
    /// <para/>ai[0] = 轨道相位偏移（rad，0 / π）；ai[1] = 本帧待发射信号（1 = 发射并自清）；
    /// localAI[0] = 存活帧数（驱动视觉旋转与轻微呼吸）。
    /// </summary>
    public class OrbitingCrestProj : ModProjectile
    {
        public override string Texture => "可成长的孔雀翎/Textures/Pixel"; // 纯几何绘制，贴图占位

        /// <summary>绕身轨道半径（px）——略小于浮游剑阵的散布，避免视觉抢占。</summary>
        private const float OrbitRadius = 52f;

        /// <summary>每帧公转角度（rad）——约 2.5 秒一整圈。</summary>
        private const float OrbitSpeed = 0.026f;

        /// <summary>纹章几何整体缩放（小纹章档）。</summary>
        private const float CrestScale = 0.55f;

        /// <summary>绘制用的固定"成形"年龄：delay 取 10 时 age=8 落在成形末期（scale≈0.97、alpha≈0.97）。</summary>
        private const int DrawDelay = 10;
        private const int DrawAge = 8;

        /// <summary>与 EsCrestSigilProj 一致的还原栅格状态（避免两处写法漂移）。</summary>
        private static readonly RasterizerState RasterizerCull = RasterizerState.CullCounterClockwise;

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;      // 铁律：纹章不造成伤害，伤害只在射线上
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;          // 由 AI 每帧续命，实际生命 = 玩家存活期
            Projectile.DamageType = MindDamageClass.Instance;
            Projectile.alpha = 255;           // 首帧揭开
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            // 续命：节点持续生效期间纹章不自然消失
            Projectile.timeLeft = 2;
            Projectile.alpha = 0;

            // 轨道位置：相位偏移固定，公转由全局帧号驱动（两枚纹章始终对径相对）
            float angle = Main.GameUpdateCount * OrbitSpeed + Projectile.ai[0];
            Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * OrbitRadius;
            Projectile.Center = player.Center + offset;
            Projectile.velocity = Vector2.Zero;

            Projectile.localAI[0]++;

            // 常驻微光（弱于剑阵，避免抢亮度）
            if (Main.rand.NextBool(6) && EffectLimiterSystem.CanSpawnEffect(1, 90))
            {
                int di = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(9f, 9f),
                    DustID.GemEmerald, Main.rand.NextVector2Circular(0.8f, 0.8f), 0,
                    MalachitePalette.GreenBright, Main.rand.NextFloat(0.5f, 0.8f)).dustIndex;
                Main.dust[di].noGravity = true;
                Main.dust[di].fadeIn = 0.4f;
            }

            // 消费发射信号（由 CrestNodes.SignalBeams 置 1）
            if (Projectile.ai[1] > 0f)
            {
                Projectile.ai[1] = 0f;
                int shotDamage = player.HeldItem != null && !player.HeldItem.IsAir
                    ? player.GetWeaponDamage(player.HeldItem)
                    : 32;
                int beamDamage = Math.Max(1, (int)(shotDamage * CrestNodes.BeamDamageMult));
                // 「双生并发」未点亮 → 0（单发，与旧版一致）；点亮 → 按并发档案展开扇形
                CrestNodes.FireBeamFrom(Projectile, player, beamDamage, player.HeldItem?.knockBack ?? 2f,
                    CrestNodes.BeamFanExtraFor(player));

                SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.32f, Pitch = 0.25f }, Projectile.Center);
                if (EffectLimiterSystem.CanSpawnEffect(2, 90))
                {
                    for (int i = 0; i < 5; i++)
                        EffectLimiterSystem.SpawnSpark(Projectile.Center, Main.rand.NextVector2Circular(3.5f, 3.5f),
                            MalachitePalette.AccentCyan, Main.rand.NextFloat(1.0f, 1.6f), 12);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            DrawCrest();
            return false;
        }

        /// <summary>
        /// 小纹章绘制（Additive 5 层几何，复用 VfxDraw.DrawCrestSigil）。
        /// 渲染铁律：绘制坐标 = world − Main.screenPosition；结束时严格还原 BlendState.AlphaBlend。
        /// </summary>
        private void DrawCrest()
        {
            Texture2D flareTex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Texture2D bloomTex = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            VfxDraw.DrawCrestSigil(Main.spriteBatch, flareTex, bloomTex,
                Projectile.Center - Main.screenPosition,
                DrawAge, DrawDelay, 0, CrestScale, 1f, 1f, Main.GameUpdateCount,
                MalachitePalette.GreenBright, MalachitePalette.AccentGold, MalachitePalette.AccentCyan, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerCull, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
