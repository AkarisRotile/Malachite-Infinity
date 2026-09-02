using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 自建潜伏条 HUD（ModSystem 通过 UI 层绘制）。
    /// 手持孔雀柳刃且未死亡时，在玩家头顶上方显示潜伏值；满 25% 后亮绿提示可触发潜伏攻击。
    /// </summary>
    public class StealthHUDBar : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (index == -1) return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "可成长的孔雀翎: Stealth HUD",
                delegate
                {
                    DrawBar();
                    return true;
                },
                InterfaceScaleType.UI));
        }

        private static void DrawBar()
        {
            Player player = Main.LocalPlayer;
            if (player == null || player.dead || Main.gameMenu) return;
            if (!MalachiteCache.IsMalachiteItem(player.HeldItem)) return;

            var mp = player.GetModPlayer<MalachitePlayer>();
            float stealth = Math.Min(mp.stealthValue, StealthSystem.NativeMaxStealth);
            float ratio = stealth / StealthSystem.NativeMaxStealth;
            bool ready = ratio >= StealthSystem.StrikeThreshold;

            var tex = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Vector2 pos = player.Center - Main.screenPosition - new Vector2(40f, 56f);
            const float width = 80f, height = 8f;

            // 背景条（调色板背景色）
            Main.spriteBatch.Draw(tex, new Rectangle((int)pos.X, (int)pos.Y, (int)width, (int)height),
                null, MalachitePalette.PanelBackground);

            // 前景条：满25%后变金表示可触发潜伏攻击（主色→强调色语义切换）
            Color fill = ready
                ? Color.Lerp(MalachitePalette.AccentGold, MalachitePalette.White, 0.4f)
                : Color.Lerp(MalachitePalette.GreenDark, MalachitePalette.PrimaryGreen, ratio);
            Main.spriteBatch.Draw(tex, new Rectangle((int)pos.X, (int)pos.Y, (int)(width * ratio), (int)height),
                null, fill * 0.9f);

            // 25% 阈值刻度线
            Main.spriteBatch.Draw(tex, new Rectangle((int)(pos.X + width * StealthSystem.StrikeThreshold), (int)pos.Y - 1, 2, (int)height + 2),
                null, MalachitePalette.White * 0.6f);

            // 名称
            Utils.DrawBorderString(Main.spriteBatch,
                MalachiteData.Loc("潜伏", "Stealth"), pos + new Vector2(0f, -16f),
                ready ? MalachitePalette.AccentGold : MalachitePalette.TextDim, 0.8f);
        }
    }
}
