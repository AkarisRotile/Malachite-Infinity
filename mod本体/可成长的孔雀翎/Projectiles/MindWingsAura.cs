// 代码来源与合规署名：
// - 视觉概念「单侧神圣光翼」的意象参考（**非代码参考**）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES，
//   与本模组既有的 EsCrestWingsProj / VfxDraw.DrawStaccatoMonowing 同源，保持视觉语言一致。
// - 本文件为自主实现：跟随玩家的双翼 + 脚下软场 + 三段展开/收尾，未复制任何外部源码。
//
// ============================================================================
// 碧翎念涌（Verdant Plumage）—— 取代旧的「领域展开」原型
// ============================================================================
// 2026-09-27 用户拍板：「现在这个领域有点像历史遗留问题了，改掉它」+「改成展开随玩家移动的光翼」。
//
// 旧实现（Projectiles\MalachiteDomain.cs，已删除）的问题：
//   · 它是**钉在施法点**的旋转法阵贴图（灵感来自 CalamityOverhaul 的 Cyberspace），
//     放完就跟你没关系了 —— 与你"孔雀翎/念"的贴身陪伴主题完全脱节；
//   · 于是「域内念伤 +30%」变成"你得站回圈里"，实战里要么被逼着站桩、要么干脆吃不到；
//   · 视觉上是通用法阵贴图，和本模组的羽翎/星芒语言没有关系。
//
// 新形态：**展开后跟随玩家**的对称三层光翼，脚下带一层同心软场。
//   · 展开 24 帧（聚气收拢 → 爆发过冲 1.25× → 回弹锁定）
//   · 常驻 288 帧（1.8s 呼吸；移动时角度后掠滞后）
//   · 收尾 18 帧（先骤缩积蓄，再崩解外溢）
//   时间轴与几何的**唯一出处是 Core\Vfx\VfxDraw.cs**（`WingPose` / `DrawMindWings`），
//   本文件只负责"跟随、范围效果、音效、收尾粒子"这些需要 Terraria 的部分。
//
// 渲染纪律：与 EsCrestWingsProj 同一套批次往返（End → Begin(Additive) → 画 → End → Begin(AlphaBlend)），
//   这是本项目已验证可用的光翼画法；1×1 一律用 AdditiveLayer.Pixel（禁 MagicPixel）。

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
    /// 碧翎念涌：展开后跟随玩家的对称光翼 + 脚下软场（星核「念域展开」的产物）。
    /// <para/>ai[0] = 计时器（自展开起的帧数）。
    /// </summary>
    public class MindWingsAura : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_98";

        /// <summary>总持续帧数（5.5 秒 @60fps）。展开 24 + 常驻 288 + 收尾 18。唯一出处是 VfxDraw。</summary>
        public const int TotalFrames = VfxDraw.WingTotalFrames;

        /// <summary>脚下软场半径（敌人减速 / 视觉光晕的判定半径）。唯一出处仍是 VfxDraw。</summary>
        public static float Radius => VfxDraw.WingAuraRadius;

        /// <summary>光翼根部相对玩家中心的偏移（背部偏上）。</summary>
        private static readonly Vector2 RootOffset = new Vector2(-4f, -8f);

        /// <summary>
        /// 整体尺寸倍率。
        /// <para/>终结技单翼用 3（巨大演出）；常驻版取 1.2 —— 形状完全一致，只是别糊住视野。
        /// **这是唯一需要调的尺寸旋钮**。
        /// </summary>
        private const float SizeMult = VfxDraw.MindWingSizeMult;

        /// <summary>
        /// 是否左右各一。
        /// <para/>★ 2026-09-27 用户拍板：**保持单翼**（false）。
        /// 原话："这个翅膀保留单向的就可以了，双向的一整对翅膀太丑了。这毕竟是个2D游戏"。
        /// 理由成立：Terraria 是侧视 2D，人物永远侧身，一对对称翅膀会读成"背后贴了两片装饰"，
        /// 中间还会空出一块；单翼反而天然与大招完全同构。
        /// </summary>
        private const bool DualWings = VfxDraw.MindWingBothSides;

        private int _effectTimer;
        private bool _openSoundPlayed;

        public override void SetDefaults()
        {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
            Projectile.timeLeft = TotalFrames;
        }

        public override void AI()
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) { Projectile.Kill(); return; }

            Projectile.ai[0]++;
            float timer = Projectile.ai[0];
            int dir = owner.direction != 0 ? owner.direction : 1;

            // ---- 跟随：翼根钉在玩家背部偏上（本弹幕每帧重算，因此天然跟着跑）----
            Projectile.Center = owner.MountedCenter + new Vector2(dir * RootOffset.X, RootOffset.Y);

            // ---- 展开音效（第 1 帧一次）----
            if (!_openSoundPlayed && timer >= 1f)
            {
                _openSoundPlayed = true;
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = 0.25f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item105 with { Volume = 0.55f, Pitch = -0.10f }, Projectile.Center);
            }

            // ---- 范围效果：软场内的敌人被念蚀减速（每 20 帧结算一次）----
            if (++_effectTimer >= 20)
            {
                _effectTimer = 0;
                float rSq = Radius * Radius;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || !npc.CanBeChasedBy()) continue;
                    if (npc.DistanceSQ(Projectile.Center) > rSq) continue;
                    npc.AddBuff(BuffID.Slow, 120);
                }

                // 羽尖星尘（限流）
                if (EffectLimiterSystem.CanSpawnEffect(1, 90) && Main.rand.NextBool(2))
                {
                    Vector2 side = Main.rand.NextBool() ? Vector2.UnitX * dir : -Vector2.UnitX * dir;
                    Vector2 from = owner.MountedCenter + side * Main.rand.NextFloat(30f, 90f)
                                   + new Vector2(0f, -Main.rand.NextFloat(20f, 70f));
                    int di = Dust.NewDustPerfect(from, DustID.GemEmerald,
                        new Vector2(-side.X * 0.6f, -Main.rand.NextFloat(0.2f, 0.9f)), 0,
                        Main.rand.NextBool(3) ? MalachitePalette.AccentGold : MalachitePalette.GreenBright,
                        Main.rand.NextFloat(0.7f, 1.2f)).dustIndex;
                    Main.dust[di].noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.20f, 0.72f, 0.45f);
        }

        /// <summary>收尾：崩解为向外爆散的星尘（AGY：24 颗）。</summary>
        public override void OnKill(int timeLeft)
        {
            if (!EffectLimiterSystem.CanSpawnEffect(6, 160)) return;

            Player owner = Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers
                ? Main.player[Projectile.owner] : null;
            Vector2 center = owner != null && owner.active ? owner.MountedCenter : Projectile.Center;

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.65f, Pitch = 0.45f }, center);
            for (int i = 0; i < 24; i++)
            {
                float ang = MathHelper.TwoPi * i / 24f + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);
                int di = Dust.NewDustPerfect(center + ang.ToRotationVector2() * Main.rand.NextFloat(6f, 28f),
                    DustID.GemEmerald, vel, 0,
                    i % 3 == 0 ? MalachitePalette.AccentGold
                               : (i % 3 == 1 ? MalachitePalette.AccentCyan : MalachitePalette.GreenBright),
                    Main.rand.NextFloat(0.8f, 1.5f)).dustIndex;
                Main.dust[di].noGravity = true;
                Main.dust[di].fadeIn = 0.4f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return false;
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active) return false;

            float timer = Projectile.ai[0];
            int dir = owner.direction != 0 ? owner.direction : 1;

            Texture2D flareTex = Terraria.GameContent.TextureAssets.Extra[ExtrasID.SharpTears].Value;

            // 与 EsCrestWingsProj 同一套批次往返（本项目已验证可用的光翼画法）
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            VfxDraw.DrawMindWings(Main.spriteBatch, flareTex, AdditiveLayer.Pixel,
                Projectile.Center - Main.screenPosition, dir, timer, SizeMult, DualWings,
                MalachitePalette.GreenBright, MalachitePalette.AccentGold);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
