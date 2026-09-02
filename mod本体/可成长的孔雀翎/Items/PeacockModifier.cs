// 代码来源与合规署名：
// 本文件为过渡兼容层（GlobalItem），仅服务于灾厄原版武器 "Malachite"
// （运行时按 模组名:类名 识别，无编译期依赖，灾厄缺失时整层不生效）。
// 攻击逻辑本身仍由灾厄官方代码执行（Items/Weapons/Rogue/Malachite.cs：
// 普攻 MalachiteProj / 右键 MalachiteBolt / 潜伏 MalachiteStealth），
// 本模组仅叠加：右键对话入口、成长/天赋 tooltip、进度数值加成。
// 参考：CalamityModPublic（Azafure, LLC 专有许可，官方允许作为开发参考）
//   Items/Weapons/Rogue/Malachite.cs — https://github.com/CalamityTeam/CalamityModPublic
// 其余内容为本模组机制的自主实现。
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    public class PeacockModifier : GlobalItem
    {
        // 仅服务灾厄原版"孔雀翎"武器（本模组独立武器"孔雀柳刃"由 PeacockWillowBlade 自带逻辑）
        public override bool AppliesToEntity(Item item, bool lateInstantiation) =>
            item.ModItem != null && item.ModItem.Mod.Name == "CalamityMod" && item.ModItem.Name == "Malachite";

        public static int GetMalachiteStage() => ProgressSystem.GetStage();

public override bool AltFunctionUse(Item item, Player player) => true;

        // 在物品开始挥动之前进行拦截
        public override bool CanUseItem(Item item, Player player)
        {
            // 如果玩家按下的是右键
            if (player.altFunctionUse == 2)
            {
                // 触发自定义的右键功能接口
                HandleRightClickAction(player);
                
                // 返回 false 中止原版的物品使用流程
                return false; 
            }
            return true;
        }

        // 预留的右键统一接口
        private void HandleRightClickAction(Player player)
        {

            // --- 近战攻击接口预留 ---
            // 以后做近战时，不需要改回 true，只需要在这里生成一个挥剑弹幕即可。
            
            /*
            {
                // 1. 生成自定义的近战挥砍弹幕
                // Projectile.NewProjectile(..., ModContent.ProjectileType<挥砍弹幕>(), ...);
                
                // 2. 播放挥剑音效
                // SoundEngine.PlaySound(SoundID.Item1, player.Center);
                
                // 3. (可选) 手动扣除潜伏值
                // player.Calamity().stealth = 0f;
            }
            */
        }

        // 恢复对话处理逻辑

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.RemoveAll(line => line.Name.StartsWith("Tooltip"));

            int stage = Math.Clamp(GetMalachiteStage(), 0, MalachiteData.StageInfos.Length - 1);
            var stageData = MalachiteData.StageInfos[stage];
            int flatAP = MalachiteData.FlatAP[stage];
            
            var mp = Main.LocalPlayer.GetModPlayer<MalachitePlayer>();
            bool hasTrack = mp.ActiveSigils.Contains(1);
            bool hasPierce = mp.ActiveSigils.Contains(2);

            if (mp.ActiveSigils.Contains(4) && hasTrack) flatAP += 8; 
            
            if (hasTrack && stage >= 11) flatAP += (stage >= 13 ? 30 : 15);

            tooltips.Add(new TooltipLine(Mod, "GrowthLevel", MalachiteData.Loc($"[c/32CD32:✦] 成长的孔雀翎 [c/32CD32:✦]   {stageData.Title}", $"[c/32CD32:✦] Growing Malachite [c/32CD32:✦]   {stageData.Title}")) { OverrideColor = stageData.Color });
            
            if (flatAP > 0)
                tooltips.Add(new TooltipLine(Mod, "DynamicAP", MalachiteData.Loc($"[c/FFA500:♦ 潜能释放: 护甲穿透 +{flatAP}]", $"[c/FFA500:♦ Potential Unleashed: Armor Penetration +{flatAP}]")) { OverrideColor = MalachitePalette.AccentGold });
                
            if (stage >= 1) 
            {
                if (hasPierce) 
                {
                    int stealthArray = stage >= 13 ? 7 : (stage >= 10 ? 5 : 3);
                    tooltips.Add(new TooltipLine(Mod, "PotentialArray", MalachiteData.Loc($"[c/FFA500:  - 潜伏攻击发射射线数量 (当前: {stealthArray} 道)]", $"[c/FFA500:  - Stealth strikes fire multiple beams (Current: {stealthArray})]")) { OverrideColor = MalachitePalette.AccentGold });
                }
                else if (hasTrack) 
                {
                    int normalArray = MalachiteData.NormalArrayCount[stage];
                    int stealthArray = MalachiteData.StealthArrayCount[stage];
                    
                    if (stage >= 5) 
                        normalArray += (stage >= 13 ? 4 : 2) + (mp.ActiveSigils.Contains(16) ? 2 : 0);
                    
                    if (normalArray > 0)
                        tooltips.Add(new TooltipLine(Mod, "PotentialArray", MalachiteData.Loc($"[c/FFA500:  - 连击生成 {normalArray} 把浮游剑阵 / 潜伏爆发 {stealthArray} 把]", $"[c/FFA500:  - Combo generates {normalArray} swords / Stealth bursts {stealthArray} floating swords]")) { OverrideColor = MalachitePalette.AccentGold });
                    else
                        tooltips.Add(new TooltipLine(Mod, "PotentialArray", MalachiteData.Loc($"[c/FFA500:  - 潜伏爆发 {stealthArray} 把浮游剑阵]", $"[c/FFA500:  - Stealth bursts {stealthArray} floating swords]")) { OverrideColor = MalachitePalette.AccentGold });
                }
            }
            if (stage >= 4) tooltips.Add(new TooltipLine(Mod, "PotentialGravity", MalachiteData.Loc($"[c/FFA500:  - 解除弹道下坠]", $"[c/FFA500:  - Removes projectile gravity]")) { OverrideColor = MalachitePalette.AccentGold });
            if (stage >= 5 && hasTrack) 
            {
                tooltips.Add(new TooltipLine(Mod, "PotentialBolts", MalachiteData.Loc($"[c/FFA500:  - 巡猎流派：协同射线将转化为等量的浮游剑阵]", $"[c/FFA500:  - Hunt Path: Synergy beams are converted to floating swords]")) { OverrideColor = MalachitePalette.AccentGold });
            }
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
            tooltips.Add(new TooltipLine(Mod, "Interaction", MalachiteData.Loc($"[c/808080:⟡ 按下 【{keyName}】 键，倾听她的心声 ⟡]", $"[c/808080:⟡ Press 【{keyName}】 to listen to her heart ⟡]")) { OverrideColor = MalachitePalette.TextDim });
            tooltips.Add(new TooltipLine(Mod, "SigilHint", MalachiteData.Loc($"[c/808080:⟡ 点击左下角按钮，配置翎之星图 ⟡]", $"[c/808080:⟡ Click the bottom-left button to configure Sigils ⟡]")) { OverrideColor = MalachitePalette.TextDim });
        }

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            int stage = GetMalachiteStage(); 
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

        public override float UseSpeedMultiplier(Item item, Player player)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            float speedMultiplier = 1f + MalachiteData.SpeedMult[GetMalachiteStage()];
            
            if (mp.ActiveSigils.Contains(7)) 
            {
                if (mp.ActiveSigils.Contains(1)) speedMultiplier += 0.35f; 
                else if (mp.ActiveSigils.Contains(2)) speedMultiplier -= 0.15f; 
            }
            
            if (mp.ActiveSigils.Contains(2)) speedMultiplier *= 0.85f;

            if (StealthSystem.StealthStrikeAvailable(player))
            {
                speedMultiplier *= 0.7f;
            }

            return MathHelper.Clamp(player.altFunctionUse == 2 ? 3f : speedMultiplier, 0.1f, 5f);
        }

        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {

            try 
            {
                int finalDamage = Math.Max(1, damage);
                int stage = GetMalachiteStage();
                MalachitePlayer modPlayer = player.GetModPlayer<MalachitePlayer>();
                
                if (!StealthSystem.StealthStrikeAvailable(player) && modPlayer.ActiveSigils.Contains(19) && modPlayer.ActiveSigils.Contains(1))
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
                    FireStealthStrike(player, source, position, velocity, type, finalDamage, knockback, stage, modPlayer);
                    StealthSystem.ConsumeStrike(player); // 无灾厄模式扣自建潜伏值
                }
                else
                {
                    FireNormalStrike(player, source, position, velocity, type, finalDamage, knockback, stage, modPlayer);
                }
            }
            catch (Exception e)
            {
                if (Main.myPlayer == player.whoAmI)
                    Main.NewText(MalachiteData.Loc($"[孔雀翎安全系统] 战斗底层逻辑发生异常: {e.Message}", $"[Malachite Safe System] Combat logic exception: {e.Message}"), MalachitePalette.DangerRed);
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            }

            return false; 
        }

        private void FireStealthStrike(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback, int stage, MalachitePlayer modPlayer)
        {
            bool hasPierce = modPlayer.ActiveSigils.Contains(2);
            bool hasTrack = modPlayer.ActiveSigils.Contains(1); // 获取巡猎天赋状态
            int stealthProjType = hasPierce
                ? (MalachiteCache.BoltType != 0 ? MalachiteCache.BoltType : type)
                : (MalachiteCache.StealthType != 0 ? MalachiteCache.StealthType : type);
            
            int stealthCount = 3;
            if (hasPierce) {
                stealthCount = stage >= 13 ? 7 : (stage >= 10 ? 5 : 3);
            }
            
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
            
            for (int i = 0; i < 20; i++) 
            {
                EffectLimiterSystem.SpawnSpark(player.Center + Main.rand.NextVector2Circular(25f, 25f), Main.rand.NextVector2Circular(12f, 12f), MalachitePalette.PrimaryGreen, Main.rand.NextFloat(1.5f, 2.8f), 25);
            }
        }

        private void FireNormalStrike(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback, int stage, MalachitePlayer modPlayer)
        {
            bool hasTrack = modPlayer.ActiveSigils.Contains(1);
            bool hasPierce = modPlayer.ActiveSigils.Contains(2);
            int defaultProjType = MalachiteCache.ProjType != 0 ? MalachiteCache.ProjType : type;
            int boltType = MalachiteCache.BoltType != 0 ? MalachiteCache.BoltType : type;
            int baseProjType = hasPierce ? boltType : defaultProjType;

            // 射出基础本体弹幕
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
                        
                        arrayDamageMult += 0.15f; // hasTrack 触发
                        if (modPlayer.ActiveSigils.Contains(8)) arrayDamageMult += 0.10f;  
                        if (modPlayer.ActiveSigils.Contains(12)) arrayDamageMult += 0.15f; 
                        if (modPlayer.ActiveSigils.Contains(16)) arrayDamageMult += 0.25f; 
                        if (modPlayer.ActiveSigils.Contains(18)) arrayDamageMult += 0.35f; 
                        
                        if (stage >= 11)
                        {
                            arrayDamageMult *= (stage >= 13 ? 1.25f : 1.15f);
                        }
                        
                        int arrayDamage = Math.Max(1, (int)(damage * arrayDamageMult));
                        
                        modPlayer.pendingNormalSwords += projCount;
                        if (modPlayer.pendingNormalSwords > 60) modPlayer.pendingNormalSwords = 60; 
                        modPlayer.pendingNormalSwordDamage = arrayDamage;
                        modPlayer.pendingNormalSwordKnockback = knockback;
                    }
                }
            }
        }
    }
}