// 本文件为自主实现（无外部参考源码）。
// 潜伏判定与标记统一经 StealthSystem（阶段2 独立化：纯自建 + IStealthStrikeProjectile 接口），
// 无任何灾厄编译期依赖 / 运行时探测。
// v2（M3）起：旧 20 节点星图与 Sigil 触发逻辑已整体退役；
// 命中层只做「阶段表护甲穿透 + 五轨档案穿透」的单一钩子（精简钩子）。
// 对话系统、日常/里程碑、潜伏值回复、领域键、存档 Dialog 键等保持原样。
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.DataStructures;
using Terraria.GameInput;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    public class MalachitePlayer : ModPlayer
    {
        public bool hasObtainedMalachite = false;
        public int leftClickShootCount = 0;
        public int currentMalachiteStage = 0;

        // 自建潜伏值（独立化后恒用自建潜伏条，由 StealthSystem 统一读写）
        public float stealthValue = StealthSystem.NativeMaxStealth;

        public int dailyChatTimer = 0;
        public int playerIdleTime = 0;
        public bool wasLowHealth = false;
        public bool hasGreetedLogin = false;
        public bool hasDiscoveredTruth = false;

        public HashSet<string> SavedDialogGroups = new HashSet<string>();

        public int treeDropCooldown = 0;

        public bool InCombat { get; private set; }

        // ===== 阶段2 新天赋（v2 加点化）：技能点 + 五轨（见 Core\TalentModel.cs）=====
        public const int TalentSaveVersion = 2;
        public bool TalentMigrationApplied = false; // 旧档迁移补偿已发放
        public int SkillPoints = 0;
        public int[] TrackLevel = new int[TalentCatalog.TrackCount];

        private int _draedonCheckTimer = 0;

        public override void SaveData(TagCompound tag)
        {
            tag["hasObtainedMalachite"] = hasObtainedMalachite;
            tag["currentMalachiteStage"] = currentMalachiteStage;
            tag["hasDiscoveredTruth"] = hasDiscoveredTruth;
            tag["DialogGroups"] = SavedDialogGroups.ToList();

            // ---- 阶段2 新天赋字段（v2 起持久化；旧档缺省由 LoadData 迁移）----
            tag["talentVersion"] = TalentSaveVersion;
            tag["talentMigrationApplied"] = TalentMigrationApplied;
            tag["skillPoints"] = SkillPoints;
            tag["trackLevel"] = TrackLevel.ToList();
        }

        public override void LoadData(TagCompound tag)
        {
            hasObtainedMalachite = tag.GetBool("hasObtainedMalachite");
            currentMalachiteStage = tag.GetInt("currentMalachiteStage");
            hasDiscoveredTruth = tag.GetBool("hasDiscoveredTruth");

            if (tag.ContainsKey("DialogGroups"))
                SavedDialogGroups = new HashSet<string>(tag.GetList<string>("DialogGroups"));

            // ---- 阶段2 新天赋读档/迁移（v2 已开启：旧星图模型整体退役，其字段已删除）----
            // D10：旧档（v1 星图或更早，缺 talentVersion）天赋清空重来：五轨归零、技能点 0，
            // 补偿（5 基准 + 已击杀不同 Boss 数）在 OnEnterWorld 一次性发放。
            if (tag.ContainsKey("talentVersion") && tag.GetInt("talentVersion") >= TalentSaveVersion)
            {
                TalentMigrationApplied = tag.GetBool("talentMigrationApplied");
                SkillPoints = tag.GetInt("skillPoints");
                TrackLevel = tag.GetList<int>("trackLevel").ToArray();
                if (TrackLevel.Length != TalentCatalog.TrackCount)
                    TrackLevel = new int[TalentCatalog.TrackCount];
            }
            else
            {
                TalentMigrationApplied = false;
                SkillPoints = 0;
                TrackLevel = new int[TalentCatalog.TrackCount];
            }
        }

        /// <summary>D10 补偿：5 基准 + 当前世界已击杀不同 Boss 数（封顶 30），每人一次。</summary>
        public override void OnEnterWorld()
        {
            if (Main.myPlayer != Player.whoAmI || TalentMigrationApplied) return;
            int kills = ModContent.GetInstance<MalachiteProgress>()?.DefeatedBossCount ?? 0;
            SkillPoints = Math.Max(SkillPoints, 5 + Math.Min(kills, 30));
            TalentMigrationApplied = true;
        }

        public void RecordDialogGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return;
            SavedDialogGroups.Add(groupId);
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (MalachiteKeybinds.DialogueKey.JustPressed)
            {
                if (Player.HeldItem != null && MalachiteCache.IsMalachiteItem(Player.HeldItem))
                {
                    var uiSystem = ModContent.GetInstance<MalachiteUISystem>();
                    if (uiSystem?.MalachiteUI != null)
                    {
                        bool isHidden = uiSystem.MalachiteUI.CurrentState == DialogState.Hidden;
                        bool canSkip = uiSystem.MalachiteUI.CurrentState != DialogState.Hidden && uiSystem.MalachiteUI.ActiveFrames > 15;
                        if (isHidden || canSkip) TriggerRightClickDialogue();
                    }
                }
            }

            // 领域展开（原型触发）：手持孔雀柳刃按下领域键，消耗 30 魔力释放
            if (MalachiteKeybinds.DomainKey != null && MalachiteKeybinds.DomainKey.JustPressed)
            {
                if (Main.myPlayer == Player.whoAmI
                    && Player.HeldItem != null
                    && Player.HeldItem.type == MalachiteCache.NativeMalachiteItem
                    && Player.statMana >= 30)
                {
                    Player.statMana -= 30;
                    Player.manaRegenDelay = 90;
                    Projectile.NewProjectile(Player.GetSource_Misc("MalachiteDomain"),
                        Player.Center, Vector2.Zero, ModContent.ProjectileType<MalachiteDomain>(), 0, 0f, Player.whoAmI);
                    SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.7f, Pitch = -0.2f }, Player.Center);
                }
            }
        }

        public override void PreUpdate()
        {
            if (treeDropCooldown > 0) treeDropCooldown--;

            // 自建潜伏值回复（战斗中慢速、脱战快速）
            if (Main.myPlayer == Player.whoAmI)
            {
                float regen = InCombat ? 0.35f : 1.5f;
                stealthValue = Math.Min(StealthSystem.NativeMaxStealth, stealthValue + regen);
            }

            if (Main.GameUpdateCount % 30 == 0)
            {
                InCombat = false;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && npc.damage > 0 && npc.DistanceSQ(Player.Center) < 2560000f)
                    {
                        InCombat = true;
                        break;
                    }
                }
            }
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            if (Player.whoAmI == Main.myPlayer)
                TriggerDeathEasterEgg();
        }

        public override void PostUpdateEquips()
        {
            if (Main.myPlayer != Player.whoAmI) return;

            if (!hasObtainedMalachite)
            {
                // v2：只认本模组武器（孔雀柳刃），灾厄孔雀翎与本模组已互不干涉
                if (Player.HasItem(MalachiteCache.NativeMalachiteItem))
                {
                    hasObtainedMalachite = true;
                    currentMalachiteStage = ProgressSystem.GetStage();
                    hasGreetedLogin = true;
                    dailyChatTimer = 18000;
                }
            }

            if (hasObtainedMalachite)
            {
                int actualStage = ProgressSystem.GetStage();
                if (actualStage > currentMalachiteStage)
                {
                    for (int i = currentMalachiteStage + 1; i <= actualStage; i++)
                    {
                        string milestoneId = $"milestone_{i}";
                        if (DialogDatabase.GetGroup(milestoneId) != null)
                        {
                            MalachiteUISystem.ShowDialogGroup(milestoneId);
                        }
                    }
                    currentMalachiteStage = actualStage;
                }
                else if (actualStage < currentMalachiteStage)
                {
                    currentMalachiteStage = actualStage;
                }

                bool isHolding = Player.HeldItem != null && MalachiteCache.IsMalachiteItem(Player.HeldItem);
                if (isHolding)
                {
                    ManageDailyChats();

                    // 彩蛋（v2 去灾厄化，纯原版自足触发）：
                    // 击败月总后持续手持孔雀柳刃约 5 秒，揭示"隐藏的真相"（旧实现需邻近灾厄 NPC Draedon，已移除）
                    if (!hasDiscoveredTruth && NPC.downedMoonlord)
                    {
                        if (++_draedonCheckTimer >= 300)
                        {
                            _draedonCheckTimer = 0;
                            hasDiscoveredTruth = true;
                            MalachiteUISystem.ShowDialogGroup("truth_draedon", force: true);
                        }
                    }
                    else _draedonCheckTimer = 0;
                }
            }
        }

        /// <summary>
        /// v2 精简命中钩子：孔雀柳刃命中只提供
        /// 「阶段表基础护甲穿透 + 五轨档案穿透」，旧星图效果分支已全部删除。
        /// </summary>
        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!MalachiteCache.IsMalachiteItem(Player.HeldItem)) return;

            int stage = Math.Clamp(ProgressSystem.GetStage(), 0, MalachiteData.FlatAP.Length - 1);
            var profile = TalentEvaluator.Build(this);
            modifiers.ArmorPenetration += MalachiteData.FlatAP[stage] + profile.ArmorPen;
        }

        public void TriggerRightClickDialogue()
        {
            int stage = ProgressSystem.GetStage();
            List<string> pool = new List<string>();

            if (stage < 6)
            {
                pool.AddRange(DialogDatabase.RightClickStage1);
            }
            else if (stage >= 6 && stage < 13)
            {
                pool.AddRange(DialogDatabase.RightClickStage2);
                if (hasDiscoveredTruth) pool.Add("right_click_stage2_truth");
                else if (stage >= 8) pool.Add("right_click_stage2_goliath");
            }
            else
            {
                pool.AddRange(DialogDatabase.RightClickStage3);
            }

            if (Player.ZoneJungle) pool.Add(stage < 8 ? "right_click_jungle_low" : "right_click_jungle_high");
            if (Player.ZoneBeach) pool.Add("right_click_beach");
            if (!Main.dayTime) pool.AddRange(DialogDatabase.RightClickNight);
            if (Main.raining) pool.Add("right_click_rain");

            if (Player.statLife < Player.statLifeMax2 * 0.5f)
            {
                pool.Clear();
                pool.AddRange(DialogDatabase.RightClickLowHealth);
            }

            if (pool.Count == 0) return;
            string selectedId = pool[Main.rand.Next(pool.Count)];
            MalachiteUISystem.ShowDialogGroup(selectedId);
        }

        private void ManageDailyChats()
        {
            if (dailyChatTimer > 0) dailyChatTimer--;

            if (!hasGreetedLogin)
            {
                hasGreetedLogin = true;
                TriggerDailyChat("greeting");
                return;
            }

            playerIdleTime = Player.velocity == Vector2.Zero ? playerIdleTime + 1 : 0;
            if (Player.statLife < Player.statLifeMax2 * 0.25f) wasLowHealth = true;

            if (Player.statLife == Player.statLifeMax2 && wasLowHealth)
            {
                if (!InCombat && dailyChatTimer <= 0)
                {
                    TriggerDailyChat(Main.rand.NextBool() ? "recovery_1" : "recovery_2");
                }
                wasLowHealth = false;
                return;
            }

            if (InCombat || dailyChatTimer > 0) return;

            if (playerIdleTime > 3600)
            {
                TriggerDailyChat(DialogDatabase.Idle[Main.rand.Next(DialogDatabase.Idle.Length)]);
                playerIdleTime = 0;
                return;
            }

            if (Main.dayTime && Main.time <= 60)
            {
                TriggerDailyChat("dawn");
                return;
            }

            if (!Main.dayTime && Main.time <= 60)
            {
                TriggerDailyChat("dusk");
                return;
            }

            if (Main.rand.NextBool(18000))
            {
                if (Player.ZoneJungle) TriggerDailyChat("ambient_jungle");
                else if (Player.ZoneBeach) TriggerDailyChat("ambient_beach");
                else if (Main.raining) TriggerDailyChat("ambient_rain");
                else if (Player.ZoneSkyHeight) TriggerDailyChat("ambient_sky");
            }
        }

        private void TriggerDailyChat(string groupId)
        {
            MalachiteUISystem.ShowDialogGroup(groupId);
            dailyChatTimer = Main.rand.Next(18000, 36000);
        }

        private void TriggerDeathEasterEgg()
        {
            if (!hasObtainedMalachite || !MalachiteCache.IsMalachiteItem(Player.HeldItem)) return;

            bool diedToWorm = false;
            bool diedToProvidence = false;
            bool diedToExoMechs = false;
            bool diedToYharon = false;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && (npc.boss || npc.type == NPCID.EaterofWorldsHead))
                {
                    if (npc.aiStyle == 6 || npc.aiStyle == 37) diedToWorm = true;
                    if (npc.ModNPC != null)
                    {
                        string name = npc.ModNPC.Name;
                        if (name.Contains("Scourge") || name.Contains("Devourer") || name.Contains("Deus") || name.Contains("Thanatos") || name.Contains("Wyrm")) diedToWorm = true;
                        if (name.Contains("Providence")) diedToProvidence = true;
                        if (name.Contains("Draedon") || name.Contains("Ares") || name.Contains("Artemis") || name.Contains("Apollo") || name.Contains("Thanatos")) diedToExoMechs = true;
                        if (name.Contains("Yharon")) diedToYharon = true;
                    }
                }
            }

            List<string> pool = new List<string>();
            if (diedToWorm) pool.Add("death_worm");
            if (diedToProvidence) pool.Add("death_providence");
            if (diedToExoMechs) pool.Add("death_exo");
            if (diedToYharon) pool.Add("death_yharon");

            if (pool.Count == 0) pool.Add("death_default");

            string selectedId = pool[Main.rand.Next(pool.Count)];
            MalachiteUISystem.ShowDialogGroup(selectedId, force: true);
        }
    }
}
