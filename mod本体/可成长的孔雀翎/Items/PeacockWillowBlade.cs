// 代码来源与合规署名：
// 初始原型的行为灵感来自 CalamityModPublic（Azafure, LLC 专有许可，官方允许作为开发参考）
//   Items/Weapons/Rogue/Malachite.cs —— 潜伏齐射替换普攻 / 攻速随潜伏状态变化 / 右键替代动作。
//   https://github.com/CalamityTeam/CalamityModPublic
// 阶段 2 独立化后本文件为自主重构：伤害/攻速/齐射数量全部由「阶段表 + 五轨加点档案
// (TalentProfile)」驱动，不再读取任何旧星图节点；与灾厄及其武器互不干涉。
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 孔雀柳刃 —— 本模组独立武器（与灾厄完全解绑）。
    /// 阶段成长（ProgressSystem.GetStage）+ 自建潜伏 + 五轨加点（TalentProfile）。
    /// 贴图：Textures/MalachiteItemGame.png（用户原画矫正缩放）。
    /// </summary>
    public class PeacockWillowBlade : ModItem
    {
        public override string Texture => "可成长的孔雀翎/Textures/MalachiteItemGame";

        private const int TableLength = 14; // 与 MalachiteData 各表长度一致（M4 重锚时同步）
        private static int ClampStage(int stage) => Math.Clamp(stage, 0, TableLength - 1);

        public override void SetDefaults()
        {
            Item.damage = 32;              // 基础伤害（阶段/加点倍率在 ModifyWeaponDamage 应用）
            Item.DamageType = ModContent.GetInstance<MalachiteDamageClass>();
            Item.width = 40;
            Item.height = 21;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp; // 法师/射手式握持（不挥动）
            Item.scale = 1.35f;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(silver: 50);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<MalachiteProj>();
            Item.shootSpeed = 14f;
            Item.UseSound = SoundID.Item1;
        }

        // 允许右键（对话）
        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                player.GetModPlayer<MalachitePlayer>().TriggerRightClickDialogue();
                return false;
            }
            return true;
        }

        public override Vector2? HoldoutOrigin() => new Vector2(13f, 11f);
        public override Vector2? HoldoutOffset() => new Vector2(2f, 0f);

        // ---- 物品栏图标：预生成斜置图标 ----
        private static Texture2D _angledIcon;

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            if (_angledIcon == null)
                _angledIcon = ModContent.Request<Texture2D>("可成长的孔雀翎/Textures/MalachiteItemIcon").Value;
            Vector2 iconOrigin = _angledIcon.Size() / 2f;
            spriteBatch.Draw(_angledIcon, position, null, drawColor, 0f, iconOrigin, scale, SpriteEffects.None, 0f);
            return false;
        }

        // ==================== 伤害 / 攻速（阶段表 × 五轨档案） ====================

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            int stage = ProgressSystem.GetStage();
            damage *= MalachiteData.DamageMultiplier[ClampStage(stage)];

            var profile = TalentEvaluator.Build(player.GetModPlayer<MalachitePlayer>());
            if (!profile.IsEmpty)
            {
                damage *= profile.DamageMult;

                // 暴击溢出增幅：总暴击 > 100% 时按 0.5%/1% 提升终伤（规则见 TalentCatalog）
                float totalCrit = 4f + profile.CritChanceBonus + player.GetCritChance(Item.DamageType);
                if (totalCrit > 100f)
                    damage *= 1f + (totalCrit - 100f) * TalentCatalog.Effects.CritOverflowFinalMult;
            }
        }

        public override float UseSpeedMultiplier(Player player)
        {
            int stage = ProgressSystem.GetStage();
            float speed = 1f + MalachiteData.SpeedMult[ClampStage(stage)];

            var profile = TalentEvaluator.Build(player.GetModPlayer<MalachitePlayer>());
            speed *= profile.AttackSpeedMult;

            // 潜伏可用时以慢速换取齐射（自建潜伏条语义保留）
            if (StealthSystem.StealthStrikeAvailable(player))
                speed *= 0.7f;

            return MathHelper.Clamp(player.altFunctionUse == 2 ? 3f : speed, 0.1f, 5f);
        }

        // ==================== 射击（普攻 / 潜伏齐射） ====================

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            try
            {
                int stage = ProgressSystem.GetStage();
                var profile = TalentEvaluator.Build(player.GetModPlayer<MalachitePlayer>());

                // 潜伏齐射：扇形多发，基础 3 发 ±6.5°；并发轨同样增弹（封顶防溢出）
                if (StealthSystem.StealthStrikeAvailable(player))
                {
                    int baseCount = 3;
                    int shots = Math.Min(12, baseCount + profile.VolleyBonus);
                    float spread = stage >= 8 ? 8f : 6.5f;
                    float stealthMult = MalachiteData.StealthDamageMult[ClampStage(stage)];
                    int finalDmg = Math.Max(1, (int)(damage * stealthMult));

                    for (int i = 0; i < shots; i++)
                    {
                        float angle = MathHelper.ToRadians(-spread * (shots - 1) / 2f + spread * i);
                        int pIndex = Projectile.NewProjectile(source, position, velocity.RotatedBy(angle), type, finalDmg, knockback, player.whoAmI);
                        if (pIndex >= 0 && pIndex < Main.maxProjectiles)
                            StealthSystem.MarkStealthStrike(Main.projectile[pIndex]);
                    }
                    StealthSystem.ConsumeStrike(player);

                    SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.8f, Pitch = -0.2f }, player.Center);
                    SpawnBurstDust(player.Center, MalachitePalette.AccentGold);
                }
                else
                {
                    // 普攻：1 道飞刀 + 并发弹幕轨每级 +1 道（轻微扇形）
                    int shots = 1 + profile.VolleyBonus;
                    if (shots <= 1)
                    {
                        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                    }
                    else
                    {
                        int maxShots = Math.Min(8, shots);
                        float spread = 5f;
                        for (int i = 0; i < maxShots; i++)
                        {
                            float angle = MathHelper.ToRadians(-spread * (maxShots - 1) / 2f + spread * i);
                            Projectile.NewProjectile(source, position, velocity.RotatedBy(angle), type, damage, knockback, player.whoAmI);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (Main.myPlayer == player.whoAmI)
                    Main.NewText(MalachiteData.Loc($"[孔雀柳刃] 战斗底层逻辑发生异常: {e.Message}", $"[Willow Blade] Combat logic exception: {e.Message}"), MalachitePalette.DangerRed);
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            }
            return false;
        }

        /// <summary>潜伏爆发光尘（限流）。</summary>
        private static void SpawnBurstDust(Vector2 center, Color color)
        {
            for (int i = 0; i < 10 && EffectLimiterSystem.CanSpawnEffect(1, 90); i++)
            {
                Dust d = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.TintableDust, Main.rand.NextVector2Circular(10f, 10f), 0, color, Main.rand.NextFloat(0.8f, 1.5f));
                d.noGravity = true;
                d.fadeIn = 0.4f;
            }
        }

        // ==================== 动态 Tooltip（阶段 + 五轨档案） ====================

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.RemoveAll(line => line.Name.StartsWith("Tooltip"));

            int stage = ProgressSystem.GetStage();
            var stageData = MalachiteData.StageInfos[ClampStage(stage)];
            var mp = Main.LocalPlayer.GetModPlayer<MalachitePlayer>();
            var profile = TalentEvaluator.Build(mp);

            tooltips.Add(new TooltipLine(Mod, "GrowthLevel", MalachiteData.Loc(
                $"[c/32CD32:✦] 孔雀柳刃 [c/32CD32:✦]   {stageData.Title}",
                $"[c/32CD32:✦] Willow Blade [c/32CD32:✦]   {stageData.Title}")) { OverrideColor = stageData.Color });

            int flatAP = MalachiteData.FlatAP[ClampStage(stage)] + profile.ArmorPen;
            if (flatAP > 0)
                tooltips.Add(new TooltipLine(Mod, "DynamicAP", MalachiteData.Loc(
                    $"[c/FFA500:♦ 护甲穿透 +{flatAP}]",
                    $"[c/FFA500:♦ Armor Penetration +{flatAP}]")) { OverrideColor = MalachitePalette.AccentGold });

            // 五轨档案摘要
            var sb = new System.Text.StringBuilder();
            sb.Append(MalachiteData.Loc("[c/00E676:· 加点档案]", "[c/00E676:· Talent]"));
            if (profile.VolleyBonus > 0)
                sb.Append(MalachiteData.Loc($"  并发弹幕 +{profile.VolleyBonus}", $"  Volley +{profile.VolleyBonus}"));
            if (profile.CritChanceBonus > 0)
                sb.Append(MalachiteData.Loc($"  暴击 +{profile.CritChanceBonus}%", $"  Crit +{profile.CritChanceBonus}%"));
            tooltips.Add(new TooltipLine(Mod, "TalentSummary", sb.ToString()) { OverrideColor = MalachitePalette.GreenBright });

            if (stage >= 6)
                tooltips.Add(new TooltipLine(Mod, "PotentialPoison", MalachiteData.Loc(
                    $"[c/FFA500:  - 命中附加中毒 (180 帧)]",
                    $"[c/FFA500:  - Hits inflict Poison (180 ticks)]")) { OverrideColor = MalachitePalette.AccentGold });

            if (stage >= 4)
                tooltips.Add(new TooltipLine(Mod, "PotentialGravity", MalachiteData.Loc(
                    $"[c/FFA500:  - 解除弹道下坠]", $"[c/FFA500:  - Removes projectile gravity]")) { OverrideColor = MalachitePalette.AccentGold });

            tooltips.Add(new TooltipLine(Mod, "Separator", "—————————————————————") { OverrideColor = MalachitePalette.TextDim });
            tooltips.Add(new TooltipLine(Mod, "MalachiteLore", stageData.Lore) { OverrideColor = MalachitePalette.GreenBright });
            tooltips.Add(new TooltipLine(Mod, "MalachiteHint", stageData.Hint) { OverrideColor = MalachitePalette.TextDim });

            string keyName = "未绑定";
            if (MalachiteKeybinds.DialogueKey != null)
            {
                var keys = MalachiteKeybinds.DialogueKey.GetAssignedKeys();
                if (keys.Count > 0) keyName = keys[0];
            }
            tooltips.Add(new TooltipLine(Mod, "Interaction", MalachiteData.Loc(
                $"[c/808080:⟡ 按下 【{keyName}】 键，倾听她的心声 ⟡]",
                $"[c/808080:⟡ Press 【{keyName}】 to listen to her heart ⟡]")) { OverrideColor = MalachitePalette.TextDim });
            tooltips.Add(new TooltipLine(Mod, "StarMapHint", MalachiteData.Loc(
                $"[c/808080:⟡ 点击左下角按钮，打开天赋星图（加点/洗点）⟡]",
                $"[c/808080:⟡ Click the bottom-left button to open the Star Map ⟡]")) { OverrideColor = MalachitePalette.TextDim });
        }
    }
}
