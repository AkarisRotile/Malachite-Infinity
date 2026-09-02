using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// additive（加色混合）发光绘制辅助：让特效光带/光点呈现 Terraria 原版武器特效的
    /// 通透发光质感（颜色叠加变亮，暗处不遮场景）。用法：Begin() → 绘制 → End()。
    /// 必须在 ModProjectile.PreDraw / UI 绘制等 Main.spriteBatch 已 Begin 的上下文中使用。
    /// 坐标系约定：调用方传**屏幕空间坐标**（world − Main.screenPosition），批处理矩阵用
    /// Main.GameViewMatrix.TransformationMatrix（仅缩放/视差，不含镜头平移）。
    /// （v5.4 修复：此前误用 Main.Transform（已含 −screenPosition 平移），所有调用点又被
    /// 二次减屏幕坐标 → 加色层整体平移出屏，表现为“特效完全不显示”。）
    /// </summary>
    public static class AdditiveLayer
    {
        // 共享 1x1 白色像素纹理（自建，尺寸确定，避免 MagicPixel 尺寸不可控导致光带异常巨大）
        private static Texture2D _pixel;
        public static Texture2D Pixel => _pixel ??= ModContent.Request<Texture2D>("可成长的孔雀翎/Textures/Pixel").Value;

        public static void Begin()
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public static void End()
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
