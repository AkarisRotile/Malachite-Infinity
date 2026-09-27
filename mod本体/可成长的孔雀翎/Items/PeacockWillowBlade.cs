// 代码来源与合规署名：
// 初始原型的行为灵感来自 CalamityModPublic（Azafure, LLC 专有许可，官方允许作为开发参考）
//   Items/Weapons/Rogue/Malachite.cs —— 行为范式参考（潜伏玩法已移除，不再使用其机制）。
//   https://github.com/CalamityTeam/CalamityModPublic
// 阶段 2 独立化后本文件为自主重构：伤害/攻速/并发弹幕数量全部由「阶段表 + 五轨加点档案
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

        // 阶段成长数据统一来自 WillowGrowth（独立成长系统，见 Core\WillowGrowth.cs）
        private static int ClampStage(int stage) => WillowGrowth.ClampStage(stage);

        public override void SetDefaults()
        {
            Item.damage = 32;              // 基础伤害（阶段/加点倍率在 ModifyWeaponDamage 应用）
            Item.DamageType = MindDamageClass.Instance;
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
            // 对话入口统一走 MalachitePlayer.ProcessTriggers（DialogueKey，含隐藏/跳句状态门）。
            // 物品层只拦截右键防误射击，不再触发对话：双入口会在同一次右键各开一个随机对话
            // （后开覆盖先开），且改键后右键仍会绕过键位误触对话。
            if (player.altFunctionUse == 2)
                return false;
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

        // ==================== 伤害 / 攻速（阶段表 × 星网档案） ====================

        /// <summary>
        /// 当前攻速倍率（阶段表 × 星网攻速 × 精准涌动）。
        /// <para/>★ 唯一出处：<see cref="UseSpeedMultiplier"/>（挥动间隔）与 <see cref="ProjectileSpeedMult"/>
        /// （弹速）都从这里取，避免"两处各算一遍"后漂移。
        /// </summary>
        private static float ComputeAttackSpeedMult(Player player, MalachitePlayer mp, TalentProfile profile)
        {
            float speed = WillowGrowth.SpeedFactor(ProgressSystem.GetStage()); // 0.85→3.8
            speed *= profile.AttackSpeedMult;

            // 「精准涌动」：精准满层时攻速 +25%（近战形态打出的精准也计入，鼓励远近衔接）
            if (profile.HasFlag(StarFlag.PreciseSurge) && mp.PrecisionStacks >= CrestNodes.PrecisionMaxStacks)
                speed *= 1f + CrestNodes.PreciseSurgeSpeedBonus;

            return speed;
        }

        /// <summary>
        /// 弹速倍率：**与攻速同源**，但取平方根做阻尼（2026-09-27 用户要求"攻速提升同时提升弹速"）。
        /// <para/>为什么必须阻尼：攻速在月总阶段到 3.8×，若弹速等比例跟随，飞刀会以 53px/帧 出屏，
        /// 弹道手感与"看得见飞刀"这个前提一起消失。取 sqrt 后 0.85→0.92、3.8→1.95，
        /// 既让"攻速也提速"明确读得出来，又不破坏弹道。
        /// </summary>
        private static float ProjectileSpeedMult(float attackSpeedMult)
            => MathHelper.Clamp((float)Math.Sqrt(Math.Max(0.01f, attackSpeedMult)), 0.85f, 2.2f);

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            int stage = ProgressSystem.GetStage();
            damage *= WillowGrowth.DamageMult(stage);

            TalentProfile profile = player.GetModPlayer<MalachitePlayer>().Profile;
            damage *= profile.DamageMult; // 无加点时恒为 1

            // 【回锋】：每有 1 道"待生成"剑阵，最终伤害 ×1.01（独立乘区）
            // 语义 —— 把"一次憋出大量剑阵"变成一个需要权衡的爆发窗口：排队越久，伤害越高，但剑阵还没落地。
            var stats = player.GetModPlayer<MalachitePlayer>();
            if (profile.HasFlag(StarFlag.ReturnEdge) && stats.SwordArrayQueue > 0)
                damage *= stats.PendingArrayDamageMult;

            // 【连锋】：连续命中同一目标的层数 → 逐层增伤（换目标/2 秒未命中清零）
            if (profile.HasFlag(StarFlag.ChainEdge) && stats.LinkStacks > 0)
                damage *= 1f + stats.LinkStacks * CrestNodes.LinkDamagePerStack;

            // 星核「碧翎念涌」：光翼展开期间念伤害 +30%
            // （2026-09-27 重做后光翼**跟随玩家**，所以不再需要"站在圈里"的判定）
            if (profile.HasFlag(StarFlag.NucleusMindDomain) && CrestNodes.IsMindDomainActive(player))
                damage *= 1f + CrestNodes.MindDomainDamageBonus;

            // 暴击溢出增幅：溢出系数由星网决定
            // ——「过载暴击」把 0.5%/1% 提到 0.9%/1%，「盈满之念」再 ×2，规则唯一出处 StarEffects。
            // totalCrit = 星网暴击 + 该职业(念)装备暴击（GetCritChance 已含 4% 基础暴击）
            float totalCrit = profile.CritChanceBonus + player.GetCritChance(Item.DamageType);
            damage *= profile.OverflowMult((int)Math.Ceiling(totalCrit));
        }

        public override float UseSpeedMultiplier(Player player)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            float speed = ComputeAttackSpeedMult(player, mp, mp.Profile);
            return MathHelper.Clamp(player.altFunctionUse == 2 ? 3f : speed, 0.1f, 5f);
        }

        // ==================== 射击（普攻） ====================

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // 注：近战形态由独立按键（MalachiteKeybinds.MeleeKey）在玩家层触发，左键保持远程射击。
            try
            {
                var mp = player.GetModPlayer<MalachitePlayer>();
                TalentProfile profile = mp.Profile;

                // —— 弹速：与攻速同源（平方根阻尼）。2026-09-27 用户要求"攻速提升同时提升弹速"。——
                velocity *= ProjectileSpeedMult(ComputeAttackSpeedMult(player, mp, profile));

                // 普攻（翎羽）：1 道飞刀 + 星网「并发弹幕」加成（轻微扇形）
                int shots = 1 + profile.VolleyBonus;

                // 「念域展开」（星核）：域持续期间额外 +2 并发
                if (profile.HasFlag(StarFlag.NucleusMindDomain) && CrestNodes.IsMindDomainActive(player))
                    shots += CrestNodes.MindDomainVolleyBonus;

                int fired = 1;
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
                    fired = maxShots;
                }

                // —— 星网产物统一按「实际发射的弹幕数」推进 ——
                // 2026-09-25 用户拍板：剑阵原按"普攻次数"计数（每次普攻必出一道），使「并发弹幕」加点对剑阵完全无收益，
                // 该节点因此形同废点。改为按发射弹幕数累计后，并发越高剑阵越密，加点价值回归。
                // 三个累加器各自独立：阈值不同（10 / 3 / 24），不可共用。

                // —— 流风剑阵（星宿 · 需击败世纪之花）——
                // 2026-09-27 改造：① 剑阵不再飞向玩家（就地停留）；
                //                ② 改走**生成队列**（每 5 帧最多 3 道），避免高攻速下一次铺满屏；
                //                ③ 「回锋」点亮后，每次请求的剑阵数量额外 +并发数。
                if (profile.HasFlag(StarFlag.SwordArray))
                {
                    mp.shotsForSwordArray += fired;
                    if (mp.shotsForSwordArray >= CrestNodes.ArrayEveryNthShot)
                    {
                        int due = mp.shotsForSwordArray / CrestNodes.ArrayEveryNthShot;
                        mp.shotsForSwordArray %= CrestNodes.ArrayEveryNthShot; // 消费后归零，累加器不无限增长

                        int perRequest = 1;
                        if (profile.HasFlag(StarFlag.ReturnEdge))
                            perRequest += Math.Max(0, profile.VolleyBonus);   // 【回锋】数量受并发影响

                        mp.EnqueueSwordArrays(due * perRequest);
                    }
                }

                // —— 灼翎纹章射线（星宿 · 击杀骷髅王）——
                if (profile.HasFlag(StarFlag.RadiantCrest))
                {
                    mp.shotsForBeam += fired;
                    if (mp.shotsForBeam >= CrestNodes.BeamEveryNthShot)
                    {
                        int due = mp.shotsForBeam / CrestNodes.BeamEveryNthShot;
                        mp.shotsForBeam %= CrestNodes.BeamEveryNthShot;
                        for (int i = 0; i < due; i++)
                            CrestNodes.SignalBeams(player);
                    }
                }

                // —— 万剑归宗（星核 · 需击败月亮领主）：每 24 发弹幕一轮全向剑阵齐射 ——
                if (profile.HasFlag(StarFlag.NucleusMyriadBlades))
                {
                    mp.shotsForMyriad += fired;
                    if (mp.shotsForMyriad >= CrestNodes.MyriadEveryNthShot)
                    {
                        int due = mp.shotsForMyriad / CrestNodes.MyriadEveryNthShot;
                        mp.shotsForMyriad %= CrestNodes.MyriadEveryNthShot;
                        int myriadDamage = Math.Max(1, (int)(damage * CrestNodes.MyriadDamageMult));
                        for (int i = 0; i < due; i++)
                            CrestNodes.SpawnSwordArrayRing(player, myriadDamage, knockback, CrestNodes.MyriadArrayCount);
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

        // ==================== 动态 Tooltip（阶段 + 五轨档案） ====================

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.RemoveAll(line => line.Name.StartsWith("Tooltip"));

            int stage = ProgressSystem.GetStage();
            var stageData = MalachiteData.StageInfos[ClampStage(stage)];
            var mp = Main.LocalPlayer.GetModPlayer<MalachitePlayer>();
            TalentProfile profile = mp.Profile;

            tooltips.Add(new TooltipLine(Mod, "GrowthLevel", MalachiteData.Loc(
                $"[c/32CD32:✦] 孔雀柳刃 [c/32CD32:✦]   {stageData.Title}",
                $"[c/32CD32:✦] Willow Blade [c/32CD32:✦]   {stageData.Title}")) { OverrideColor = stageData.Color });

            int flatAP = WillowGrowth.FlatAP(stage) + profile.ArmorPen;
            if (flatAP > 0)
                tooltips.Add(new TooltipLine(Mod, "DynamicAP", MalachiteData.Loc(
                    $"[c/FFA500:♦ 护甲穿透 +{flatAP}]",
                    $"[c/FFA500:♦ Armor Penetration +{flatAP}]")) { OverrideColor = MalachitePalette.AccentGold });

            // 星网档案摘要（数值 + 星宿/星核计数）
            tooltips.Add(new TooltipLine(Mod, "TalentSummary", StarEffects.Summary(profile)) { OverrideColor = MalachitePalette.GreenBright });

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
                $"[c/808080:⟡ 点击左下角旋转纹章，打开「翎羽星网」（加点/洗点）⟡]",
                $"[c/808080:⟡ Click the spinning sigil at the bottom-left to open the Star Web ⟡]")) { OverrideColor = MalachitePalette.TextDim });
        }
    }
}
