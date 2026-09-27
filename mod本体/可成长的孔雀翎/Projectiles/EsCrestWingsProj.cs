// 代码来源与合规署名：
// - 招式与视觉概念参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES
//   两段式终结（斜上飞升 → 顶点顿挫 → 斜下贯穿 → 终点终极顿挫）与单侧神圣光翼的意象。
// - 绘制实现为自主编写：原版 Extra 光晕贴图 + Additive 多层 + 超射弹簧曲线（无外部源码复制）。
// 本文件为自主实现。
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 《苍翼：混沌效应》ES 专属——两段顿挫单侧神圣光翼（Two-Stage Finisher 配套，纯视觉）。
    /// ai[0] = 计时器；ai[1] = 阶级（0: 初阶小单翼 / 1: 终阶巨单翼）。
    /// 铁律：绝对单侧展开、锚定后肩、禁止向前翻转；护眼——高光锁在晶刃白热剑脊与尖端。
    /// 渲染遵守项目管线：Additive 绘制后严格还原 AlphaBlend；1×1 用 AdditiveLayer.Pixel（禁 MagicPixel）。
    /// </summary>
    public class EsCrestWingsProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_98";

        public int Tier => (int)Projectile.ai[1]; // 0: 小单翼, 1: 大单翼
        private float Timer => Projectile.ai[0];

        /// <summary>羽翼整体尺寸倍率（2026-09-03 实机指令：双阶羽翼 ×3）。</summary>
        private const float SizeMult = 3f;

        private int MaxLifetime => Tier == 0 ? 24 : 48;
        private int SnapFrames => Tier == 0 ? 4 : 6;

        /// <summary>羽翼尖端坐标暂存（避免 PreDraw 每帧分配）。</summary>
        private static readonly Vector2[] _plumeTips = new Vector2[8];

        public override void SetDefaults()
        {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
            Projectile.timeLeft = 60;
        }

        /// <summary>生成单翼（tier: 0=初阶小单翼 / 1=终阶巨单翼），锚定在玩家后肩。</summary>
        public static void Spawn(Player owner, int tier)
        {
            if (owner == null || !owner.active || owner.dead) return;
            Projectile.NewProjectile(owner.GetSource_Misc("EsCrestWing"), owner.Center, Vector2.Zero,
                ModContent.ProjectileType<EsCrestWingsProj>(), 0, 0f, owner.whoAmI, 0f, tier);
        }

        public override void AI()
        {
            Projectile.ai[0]++;
            if (Timer >= MaxLifetime) { Projectile.Kill(); return; }

            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) { Projectile.Kill(); return; }

            // 锚定：Tier1 巨翼跟随玩家后肩；Tier0 小翼只在生成首帧定位到升空顶点，之后【留在原地】
            // 斜指向突进前位置（斜向下），不随二段俯冲离开
            Vector2 backAnchor = new Vector2(-owner.direction * (Tier == 0 ? 12f : 16f) * SizeMult, -16f * SizeMult);
            if (Tier == 1 || Timer <= 1f)
                Projectile.Center = owner.MountedCenter + backAnchor;

            // 展开刹那的强顿挫音效与晶核咬合
            if (Timer == 1f)
            {
                if (Tier == 0)
                {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.55f, Volume = 0.7f }, Projectile.Center);
                }
                else
                {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f, Volume = 0.95f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item105 with { Pitch = -0.2f, Volume = 0.85f }, Projectile.Center);
                }
            }

            // 大单翼呼吸期间向外抛洒星尘（限流）
            if (Tier == 1 && Timer >= SnapFrames && Timer <= MaxLifetime - 10
                && EffectLimiterSystem.CanSpawnEffect(1, 90) && Main.rand.NextBool(2))
            {
                Vector2 dustOffset = new Vector2(-owner.direction * Main.rand.NextFloat(40f, 150f) * SizeMult, -Main.rand.NextFloat(30f, 130f) * SizeMult);
                int di = Dust.NewDustPerfect(Projectile.Center + dustOffset, DustID.GemEmerald,
                    new Vector2(-owner.direction * 0.8f, -Main.rand.NextFloat(0.4f, 1.2f)), 0,
                    Main.rand.NextBool(3) ? MalachitePalette.AccentGold : MalachitePalette.GreenBright, Main.rand.NextFloat(0.8f, 1.3f)).dustIndex;
                Main.dust[di].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            RenderStaccatoMonowing();
            return false;
        }

        /// <summary>顿挫单翼渲染：超射弹簧弹开（1.25 过冲 → 回弹锁定）+ 呼吸浮动。</summary>
        private void RenderStaccatoMonowing()
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active) return;

            Texture2D flareTex = TextureAssets.Extra[ExtrasID.SharpTears].Value;      // 原 Extra[98] 菱芒
            Texture2D bloomTex = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;  // 原 Extra[89] 柔光
            Texture2D pixelTex = AdditiveLayer.Pixel;                                 // 项目自建 1×1（禁 MagicPixel）

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 绘制数学已提取至 Core\Vfx\VfxDraw.cs（唯一出处：游戏内与离线预览共用）
            VfxDraw.DrawStaccatoMonowing(Main.spriteBatch, flareTex, bloomTex, pixelTex,
                Projectile.Center - Main.screenPosition,
                owner.direction, Tier, Timer, SnapFrames, MaxLifetime, SizeMult,
                MalachitePalette.GreenBright, MalachitePalette.AccentGold, _plumeTips);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
