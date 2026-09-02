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
    /// 孔雀柳刃 —— 本模组独立武器（与灾厄"孔雀翎"完全解绑）。
    /// 自建弹幕 + 自建潜伏系统 + 天赋体系，不依赖任何灾厄内容。
    /// 贴图：Textures/MalachiteItemGame.png（用户原画矫正缩放）。
    /// </summary>
    public class PeacockWillowBlade : ModItem
    {
        public override string Texture => "可成长的孔雀翎/Textures/MalachiteItemGame";

        public override void SetDefaults()
        {
            Item.damage = 32;              // 基础伤害（阶段倍率在 ModifyWeaponDamage 中应用）
            Item.DamageType = ModContent.GetInstance<MalachiteDamageClass>();
            Item.width = 40;
            Item.height = 21;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp; // 法师/射手式握持（不挥动）
            Item.scale = 1.35f;                    // 手持贴图放大
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(silver: 50);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;
            Item.noMelee = true;           // 以弹幕为主
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

        public override Vector2? HoldoutOrigin() => new Vector2(13f, 11f); // 握持原点对准棕色柄部（分析自原画）

        public override Vector2? HoldoutOffset() => new Vector2(2f, 0f);

        // ---- 物品栏图标：预生成斜置图标（66° 构图 40x40），不依赖运行时加载原图 ----

        private static Texture2D _angledIcon;

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            if (_angledIcon == null)
                _angledIcon = ModContent.Request<Texture2D>("可成长的孔雀翎/Textures/MalachiteItemIcon").Value;

            // 图标 40x40 与默认贴图宽度一致，直接沿用引擎 scale（悬停放大同步）
            Vector2 iconOrigin = _angledIcon.Size() / 2f;
            spriteBatch.Draw(_angledIcon, position, null, drawColor, 0f, iconOrigin, scale, SpriteEffects.None, 0f);
            return false; // 阻止默认的水平版绘制
        }

        // ==================== 阶段成长 ====================

        public static int GetStage() => ProgressSystem.GetStage();

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            int stage = GetStage();
            damage *= MalachiteData.DamageMultiplier[stage];

            if (mp.ActiveSigils.Contains(13) && mp.ActiveSigils.Contains(1)) damage *= 1.20f;
            if (mp.ActiveSigils.Contains(7) && mp.ActiveSigils.Contains(1)) damage *= 0.90f;

            if (mp.ActiveSigils.Contains(1) && stage >= 10)
            {
                float lateGameBuff = 1f + (stage - 9) * 0.08f;
                if (stage >= 13) lateGameBuff += 0.15f;
                damage *= lateGameBuff;
            }
        }

        public override float UseSpeedMultiplier(Player player)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            float speedMultiplier = 1f + MalachiteData.SpeedMult[GetStage()];

            if (mp.ActiveSigils.Contains(7))
            {
                if (mp.ActiveSigils.Contains(1)) speedMultiplier += 0.35f;
                else if (mp.ActiveSigils.Contains(2)) speedMultiplier -= 0.15f;
            }

            if (mp.ActiveSigils.Contains(2)) speedMultiplier *= 0.85f;

            if (StealthSystem.StealthStrikeAvailable(player))
                speedMultiplier *= 0.7f;

            return MathHelper.Clamp(player.altFunctionUse == 2 ? 3f : speedMultiplier, 0.1f, 5f);
        }

        // ==================== 射击（潜伏 / 普攻） ====================

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            try
            {
                int finalDamage = Math.Max(1, damage);
                int stage = GetStage();
                var mp = player.GetModPlayer<MalachitePlayer>();

                // 天赋 19（幽魂）：魔力强化普攻
                if (!StealthSystem.StealthStrikeAvailable(player) && mp.ActiveSigils.Contains(19) && mp.ActiveSigils.Contains(1))
                {
                    if (player.statMana >= 5)
                    {
                        player.statMana -= 5;
                        player.manaRegenDelay = 60;
                        finalDamage = (int)(finalDamage * 1.15f);
                    }
                }

                if (StealthSystem.StealthStrikeAvailable(player))
                {
                    FireStealthStrike(player, source, position, velocity, finalDamage, knockback, stage, mp);
                    StealthSystem.ConsumeStrike(player); // 无灾厄模式扣自建潜伏值
                }
                else
                {
                    FireNormalStrike(player, source, position, velocity, finalDamage, knockback, stage, mp);
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

        private void FireStealthStrike(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback, int stage, MalachitePlayer modPlayer)
        {
            bool hasPierce = modPlayer.ActiveSigils.Contains(2);
            bool hasTrack = modPlayer.ActiveSigils.Contains(1);
            // 自建弹幕：贯穿天赋→射线，否则→飞刀
            int stealthProjType = hasPierce ? MalachiteCache.NativeBoltType : MalachiteCache.NativeProjType;

            int stealthCount = 3;
            if (hasPierce)
                stealthCount = stage >= 13 ? 7 : (stage >= 10 ? 5 : 3);

            float stealthSpread = stage >= 10 ? 10f : 6.5f;
            if (hasPierce) stealthSpread *= 0.65f;

            float stealthMult = MalachiteData.StealthDamageMult[stage];
            if (hasPierce && stage >= 2 && stage < 12) stealthMult += 0.12f;
            if (modPlayer.ActiveSigils.Contains(7) && hasPierce) stealthMult += 0.5f;

            int stealthDmg = Math.Max(1, (int)(damage * stealthMult));

            for (int i = 0; i < stealthCount; i++)
            {
                float angleOffset = MathHelper.ToRadians(-stealthSpread * (stealthCount - 1) / 2f + stealthSpread * i);
                int pIndex = Projectile.NewProjectile(source, position, velocity.RotatedBy(angleOffset), stealthProjType, stealthDmg, knockback, player.whoAmI);
                if (pIndex >= 0 && pIndex < Main.maxProjectiles)
                {
                    StealthSystem.MarkStealthStrike(Main.projectile[pIndex]);
                    if (hasPierce) Main.projectile[pIndex].scale *= 1.2f;
                }
            }

            if (hasTrack)
            {
                int stealthArrayCount = MalachiteData.StealthArrayCount[stage];
                float stealthArrayMult = stage >= 13 ? 1.2f : (stage >= 11 ? 0.9f : (stage >= 8 ? 0.7f : 0.5f));
                if (modPlayer.ActiveSigils.Contains(8)) stealthArrayMult += 0.15f;
                if (modPlayer.ActiveSigils.Contains(12)) stealthArrayMult += 0.15f;
                if (modPlayer.ActiveSigils.Contains(16)) stealthArrayMult += 0.25f;
                if (modPlayer.ActiveSigils.Contains(18)) stealthArrayMult += 0.35f;

                int arrayDamage = Math.Max(1, (int)(damage * stealthArrayMult));

                for (int i = 0; i < stealthArrayCount; i++)
                {
                    int pIndex = Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<OrbitingMalachiteProj>(), arrayDamage, knockback, player.whoAmI, 0f, i * 3f);
                    if (pIndex >= 0 && pIndex < Main.maxProjectiles)
                        StealthSystem.MarkStealthStrike(Main.projectile[pIndex]);
                }
            }

            SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.8f, Pitch = -0.2f }, player.Center);

            // 潜伏爆发光尘（Dust）
            for (int i = 0; i < 10 && EffectLimiterSystem.CanSpawnEffect(1, 90); i++)
            {
                Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.TintableDust, Main.rand.NextVector2Circular(10f, 10f),
                    0, MalachitePalette.AccentGold, Main.rand.NextFloat(0.8f, 1.5f));
                d.noGravity = true;
                d.fadeIn = 0.4f;
            }
        }

        private void FireNormalStrike(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback, int stage, MalachitePlayer modPlayer)
        {
            bool hasTrack = modPlayer.ActiveSigils.Contains(1);
            bool hasPierce = modPlayer.ActiveSigils.Contains(2);
            int defaultProjType = MalachiteCache.NativeProjType;
            int boltType = MalachiteCache.NativeBoltType;
            int baseProjType = hasPierce ? boltType : defaultProjType;

            // 射出基础本体弹幕（自建飞刀/射线）
            Projectile.NewProjectile(source, position, velocity, baseProjType, damage, knockback, player.whoAmI);

            int omittedRays = 0;
            if (stage >= 5 && hasTrack)
            {
                int boltCount = (stage >= 13 ? 2 : 1) + (modPlayer.ActiveSigils.Contains(16) ? 1 : 0);
                omittedRays = boltCount * 2;
            }

            modPlayer.leftClickShootCount++;
            if (modPlayer.leftClickShootCount >= (stage >= 13 ? 2 : 3))
            {
                modPlayer.leftClickShootCount = 0;

                if (hasPierce)
                {
                    int extraRayCount = stage >= 13 ? 4 : (stage >= 8 ? 2 : 1);
                    int rayDamage = Math.Max(1, (int)(damage * (stage >= 13 ? 1.0f : (stage >= 11 ? 0.8f : (stage >= 8 ? 0.6f : 0.5f)))));
                    for (int i = 0; i < extraRayCount; i++)
                    {
                        float angle = MathHelper.ToRadians(-4.5f * (extraRayCount - 1) / 2f + 4.5f * i);
                        Projectile.NewProjectile(source, player.Center, velocity.RotatedBy(angle) * 1.2f, boltType, rayDamage, knockback, player.whoAmI);
                    }
                }
                else if (hasTrack)
                {
                    int projCount = MalachiteData.NormalArrayCount[stage];
                    projCount += omittedRays;

                    if (projCount > 0)
                    {
                        float arrayDamageMult = stage >= 13 ? 1.0f : (stage >= 11 ? 0.8f : (stage >= 8 ? 0.6f : 0.5f));
                        arrayDamageMult += 0.15f;
                        if (modPlayer.ActiveSigils.Contains(8)) arrayDamageMult += 0.10f;
                        if (modPlayer.ActiveSigils.Contains(12)) arrayDamageMult += 0.15f;
                        if (modPlayer.ActiveSigils.Contains(16)) arrayDamageMult += 0.25f;
                        if (modPlayer.ActiveSigils.Contains(18)) arrayDamageMult += 0.35f;
                        if (stage >= 11) arrayDamageMult *= (stage >= 13 ? 1.25f : 1.15f);

                        int arrayDamage = Math.Max(1, (int)(damage * arrayDamageMult));

                        modPlayer.pendingNormalSwords += projCount;
                        if (modPlayer.pendingNormalSwords > 60) modPlayer.pendingNormalSwords = 60;
                        modPlayer.pendingNormalSwordDamage = arrayDamage;
                        modPlayer.pendingNormalSwordKnockback = knockback;
                    }
                }
            }
        }

        // ==================== 动态 Tooltip ====================

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.RemoveAll(line => line.Name.StartsWith("Tooltip"));

            int stage = Math.Clamp(GetStage(), 0, MalachiteData.StageInfos.Length - 1);
            var stageData = MalachiteData.StageInfos[stage];
            int flatAP = MalachiteData.FlatAP[stage];

            var mp = Main.LocalPlayer.GetModPlayer<MalachitePlayer>();
            bool hasTrack = mp.ActiveSigils.Contains(1);
            bool hasPierce = mp.ActiveSigils.Contains(2);

            if (mp.ActiveSigils.Contains(4) && hasTrack) flatAP += 8;
            if (hasTrack && stage >= 11) flatAP += (stage >= 13 ? 30 : 15);

            tooltips.Add(new TooltipLine(Mod, "GrowthLevel", MalachiteData.Loc(
                $"[c/32CD32:✦] 孔雀柳刃 [c/32CD32:✦]   {stageData.Title}",
                $"[c/32CD32:✦] Willow Blade [c/32CD32:✦]   {stageData.Title}")) { OverrideColor = stageData.Color });

            if (flatAP > 0)
                tooltips.Add(new TooltipLine(Mod, "DynamicAP", MalachiteData.Loc(
                    $"[c/FFA500:♦ 潜能释放: 护甲穿透 +{flatAP}]",
                    $"[c/FFA500:♦ Potential Unleashed: Armor Penetration +{flatAP}]")) { OverrideColor = MalachitePalette.AccentGold });

            if (stage >= 1)
            {
                if (hasPierce)
                {
                    int stealthArray = stage >= 13 ? 7 : (stage >= 10 ? 5 : 3);
                    tooltips.Add(new TooltipLine(Mod, "PotentialArray", MalachiteData.Loc(
                        $"[c/FFA500:  - 潜伏攻击发射射线数量 (当前: {stealthArray} 道)]",
                        $"[c/FFA500:  - Stealth strikes fire multiple beams (Current: {stealthArray})]")) { OverrideColor = MalachitePalette.AccentGold });
                }
                else if (hasTrack)
                {
                    int normalArray = MalachiteData.NormalArrayCount[stage];
                    int stealthArray = MalachiteData.StealthArrayCount[stage];
                    if (stage >= 5)
                        normalArray += (stage >= 13 ? 4 : 2) + (mp.ActiveSigils.Contains(16) ? 2 : 0);

                    if (normalArray > 0)
                        tooltips.Add(new TooltipLine(Mod, "PotentialArray", MalachiteData.Loc(
                            $"[c/FFA500:  - 连击生成 {normalArray} 把浮游剑阵 / 潜伏爆发 {stealthArray} 把]",
                            $"[c/FFA500:  - Combo generates {normalArray} swords / Stealth bursts {stealthArray} floating swords]")) { OverrideColor = MalachitePalette.AccentGold });
                    else
                        tooltips.Add(new TooltipLine(Mod, "PotentialArray", MalachiteData.Loc(
                            $"[c/FFA500:  - 潜伏爆发 {stealthArray} 把浮游剑阵]",
                            $"[c/FFA500:  - Stealth bursts {stealthArray} floating swords]")) { OverrideColor = MalachitePalette.AccentGold });
                }
            }

            if (stage >= 4)
                tooltips.Add(new TooltipLine(Mod, "PotentialGravity", MalachiteData.Loc(
                    $"[c/FFA500:  - 解除弹道下坠]", $"[c/FFA500:  - Removes projectile gravity]")) { OverrideColor = MalachitePalette.AccentGold });

            if (stage >= 5 && hasTrack)
                tooltips.Add(new TooltipLine(Mod, "PotentialBolts", MalachiteData.Loc(
                    $"[c/FFA500:  - 巡猎流派：协同射线将转化为等量的浮游剑阵]",
                    $"[c/FFA500:  - Hunt Path: Synergy beams are converted to floating swords]")) { OverrideColor = MalachitePalette.AccentGold });

            if (stage >= 6)
            {
                string buffNameZh = ProgressSystem.DownedPlaguebringer ? "瘟疫" : "中毒";
                string buffNameEn = ProgressSystem.DownedPlaguebringer ? "Plague" : "Poison";
                tooltips.Add(new TooltipLine(Mod, "PotentialPlague", MalachiteData.Loc(
                    $"[c/FFA500:  - {(stage >= 13 ? $"对{buffNameZh}目标无视 50% 护甲" : $"对{buffNameZh}目标无视 20% 护甲")}]",
                    $"[c/FFA500:  - {(stage >= 13 ? $"Ignores 50% Armor against {buffNameEn} targets" : $"Ignores 20% Armor against {buffNameEn} targets")}]"
                )) { OverrideColor = MalachitePalette.AccentGold });
            }

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
            tooltips.Add(new TooltipLine(Mod, "SigilHint", MalachiteData.Loc(
                $"[c/808080:⟡ 点击左下角按钮，配置翎之星图 ⟡]",
                $"[c/808080:⟡ Click the bottom-left button to configure Sigils ⟡]")) { OverrideColor = MalachitePalette.TextDim });
        }
    }
}
