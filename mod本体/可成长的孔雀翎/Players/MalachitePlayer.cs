// 本文件为自主实现（无外部参考源码）。
// 潜伏判定与标记统一经 StealthSystem 封装（灾厄在场读写灾厄数据，缺失走自建值）；
// 灾厄内容仅做运行时探测（如 Draedon 彩蛋 TryFind），无编译期依赖。
// 天赋（星图 Sigil）触发逻辑、对话系统与存档均为本模组自设计。
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
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    public class MalachitePlayer : ModPlayer
    {
        public bool hasObtainedMalachite = false;
        public int leftClickShootCount = 0;
        public int currentMalachiteStage = 0;

        // 无灾厄模式下的自建潜伏值（灾厄在场时使用灾厄自身的潜伏条）
        public float stealthValue = StealthSystem.NativeMaxStealth;

        public int dailyChatTimer = 0;
        public int playerIdleTime = 0;
        public bool wasLowHealth = false;
        public bool hasGreetedLogin = false;
        public bool hasDiscoveredTruth = false;

        public HashSet<string> SavedDialogGroups = new HashSet<string>();

        public int treeDropCooldown = 0;
        public int lifestealCooldownTrack = 0;
        public int lifestealCooldownPierce = 0;
        public int sylvanCooldown = 0;
        public int astralCooldown = 0;
        public int plagueExplosionCooldown = 0;
        public int lightningCooldown = 0;
        public int witchExplosionCooldown = 0;
        public int moonSphereCooldown = 0;
        public int moonPortalCooldown = 0;

        public int pendingNormalSwords = 0;
        public int pendingNormalSwordDamage = 0;
        public float pendingNormalSwordKnockback = 0f;
        public int swordSpawnTimer = 0;

        public HashSet<int> ActiveSigils = new HashSet<int>();
        public bool InCombat { get; private set; }

        // ===== 阶段2 新天赋（v1 纯加点化）：技能点 + 五轨（见 Core\TalentModel.cs）=====
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
            tag["ActiveSigils"] = ActiveSigils.ToList();
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

            if (tag.ContainsKey("ActiveSigils"))
                ActiveSigils = new HashSet<int>(tag.GetList<int>("ActiveSigils"));

            if (tag.ContainsKey("DialogGroups"))
                SavedDialogGroups = new HashSet<string>(tag.GetList<string>("DialogGroups"));

            // ---- 阶段2 新天赋读档/迁移（V2Active 由切换波次置 true，届时旧模型退役）----
            if (TalentCatalog.V2Active)
            {
                // D10：v1(ActiveSigils) → v2 清空重来；补偿按世界击杀数在 OnEnterWorld 发放一次
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
                    ActiveSigils = new HashSet<int>(); // 旧星图清空重来
                }
            }
            else
            {
                // 新键预读（幂等），旧模型运行期间不影响现有行为
                if (tag.ContainsKey("talentMigrationApplied")) TalentMigrationApplied = tag.GetBool("talentMigrationApplied");
                if (tag.ContainsKey("skillPoints")) SkillPoints = tag.GetInt("skillPoints");
                if (tag.ContainsKey("trackLevel"))
                {
                    TrackLevel = tag.GetList<int>("trackLevel").ToArray();
                    if (TrackLevel.Length != TalentCatalog.TrackCount)
                        TrackLevel = new int[TalentCatalog.TrackCount];
                }
            }
        }

        /// <summary>D10 补偿（切换波次启用后有效）：5 基准 + 当前世界已击杀不同 Boss 数（封顶 30）。</summary>
        public override void OnEnterWorld()
        {
            if (!TalentCatalog.V2Active) return;
            if (Main.myPlayer != Player.whoAmI || TalentMigrationApplied) return;
            int kills = ModContent.GetInstance<MalachiteProgress>()?.DefeatedBossCount ?? 0;
            SkillPoints = Math.Max(SkillPoints, 5 + Math.Min(kills, 30));
            TalentMigrationApplied = true;
        }

        public void SyncPlayerSigils()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                var packet = Mod.GetPacket();
                packet.Write((byte)1);
                packet.Write((byte)Player.whoAmI);
                packet.Write(currentMalachiteStage);
                packet.Write(ActiveSigils.Count);
                foreach (int sigil in ActiveSigils) packet.Write(sigil);
                packet.Send();
            }
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            var packet = Mod.GetPacket();
            packet.Write((byte)1);
            packet.Write((byte)Player.whoAmI);
            packet.Write(currentMalachiteStage);
            packet.Write(ActiveSigils.Count);
            foreach (int sigil in ActiveSigils) packet.Write(sigil);
            packet.Send(toWho, fromWho);
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
            if (lifestealCooldownTrack > 0) lifestealCooldownTrack--;
            if (lifestealCooldownPierce > 0) lifestealCooldownPierce--;
            if (sylvanCooldown > 0) sylvanCooldown--;
            if (astralCooldown > 0) astralCooldown--;
            if (plagueExplosionCooldown > 0) plagueExplosionCooldown--;
            if (lightningCooldown > 0) lightningCooldown--;
            if (witchExplosionCooldown > 0) witchExplosionCooldown--;
            if (moonSphereCooldown > 0) moonSphereCooldown--;
            if (moonPortalCooldown > 0) moonPortalCooldown--;

            // 无灾厄模式：自建潜伏值回复（战斗中慢速、脱战快速）
            if (!CalamityCompat.Loaded && Main.myPlayer == Player.whoAmI)
            {
                float regen = InCombat ? 0.35f : 1.5f;
                stealthValue = Math.Min(StealthSystem.NativeMaxStealth, stealthValue + regen);
            }

            if (pendingNormalSwords > 0 && Main.myPlayer == Player.whoAmI)
            {
                int activeSwords = 0;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.owner == Player.whoAmI && p.type == ModContent.ProjectileType<OrbitingMalachiteProj>())
                    {
                        activeSwords++;
                    }
                }

                int stage = PeacockModifier.GetMalachiteStage();
                int maxSwords = stage >= 13 ? 24 : 16;
                int spawnCountThisFrame = 0;
                while (activeSwords < maxSwords && pendingNormalSwords > 0 && spawnCountThisFrame < 2)
                {
                    Projectile.NewProjectile(Player.GetSource_Misc("Malachite_SwordQueue"), Player.Center, Vector2.Zero, ModContent.ProjectileType<OrbitingMalachiteProj>(), pendingNormalSwordDamage, pendingNormalSwordKnockback, Player.whoAmI, 0f, 0f);
                    pendingNormalSwords--;
                    activeSwords++;
                    spawnCountThisFrame++;
                }
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

            int maxCap = MalachiteTalents.GetMaxCapacity(PeacockModifier.GetMalachiteStage());
            int currentCost = 0;
            foreach (int s in ActiveSigils)
            {
                if (s >= 0 && s < MalachiteTalents.Sigils.Count)
                    currentCost += MalachiteTalents.Sigils[s].Cost;
            }
            if (currentCost > maxCap) ActiveSigils.Clear();
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
                bool hasNative = Player.HasItem(MalachiteCache.NativeMalachiteItem);
                bool hasCalamity = MalachiteCache.MalachiteItem != 0 && Player.HasItem(MalachiteCache.MalachiteItem);
                if (hasNative || hasCalamity)
                {
                    hasObtainedMalachite = true;
                    currentMalachiteStage = PeacockModifier.GetMalachiteStage();
                    hasGreetedLogin = true;
                    dailyChatTimer = 18000;
                }
            }

            if (hasObtainedMalachite)
            {
                int actualStage = PeacockModifier.GetMalachiteStage();
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
                    SyncPlayerSigils();
                }
                else if (actualStage < currentMalachiteStage)
                {
                    currentMalachiteStage = actualStage;
                    SyncPlayerSigils();
                }

                bool isHolding = Player.HeldItem != null && MalachiteCache.IsMalachiteItem(Player.HeldItem);
                if (isHolding)
                {
                    ManageDailyChats();

                    if (!hasDiscoveredTruth && currentMalachiteStage >= 8)
                    {
                        if (++_draedonCheckTimer >= 60)
                        {
                            _draedonCheckTimer = 0;
                            if (ModContent.TryFind<ModNPC>("CalamityMod", "Draedon", out var draedon) && NPC.AnyNPCs(draedon.Type))
                            {
                                hasDiscoveredTruth = true;
                                MalachiteUISystem.ShowDialogGroup("truth_draedon", force: true);
                            }
                        }
                    }
                }
            }
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!MalachiteCache.IsMalachiteItem(Player.HeldItem)) return;

            int stage = PeacockModifier.GetMalachiteStage();
            bool isStealthHit = StealthSystem.IsStealthStrike(proj);
            bool hasTrack = ActiveSigils.Contains(1);
            bool hasPierce = ActiveSigils.Contains(2);

            int ap = MalachiteData.FlatAP[stage];
            if (ActiveSigils.Contains(4) && hasTrack) ap += 8;
            if (hasTrack && stage >= 11) ap += (stage >= 13 ? 30 : 15);

            modifiers.ArmorPenetration += Math.Max(0, ap);

            if (stage >= 6)
            {
                int pb = MalachiteCache.PlagueBuff;
                bool hasPlague = pb != 0 && target.HasBuff(pb);
                bool hasPoison = target.HasBuff(BuffID.Poisoned);
                if (hasPlague || (!ProgressSystem.DownedPlaguebringer && hasPoison))
                    modifiers.ScalingArmorPenetration += (stage >= 13) ? 0.5f : 0.2f;
            }

            if (ActiveSigils.Contains(4) && hasPierce) modifiers.ScalingArmorPenetration += 0.20f;
            if (ActiveSigils.Contains(17) && hasPierce) modifiers.ArmorPenetration += 25;

            if (ActiveSigils.Contains(9) && hasTrack && !isStealthHit)
            {
                modifiers.ArmorPenetration += 10;
                float dist = Player.DistanceSQ(target.Center);
                if (dist < 400f * 400f)
                {
                    float bonus = MathHelper.Clamp(1f - ((float)Math.Sqrt(dist) / 400f), 0f, 0.3f);
                    modifiers.SourceDamage += bonus;
                }
            }

            if (ActiveSigils.Contains(11) && hasTrack && !isStealthHit)
            {
                float spd = Player.velocity.Length();
                float bonus = MathHelper.Clamp(spd / 15f, 0f, 0.25f);
                modifiers.SourceDamage += bonus;
            }

            if (ActiveSigils.Contains(15))
            {
                float hpPercent = target.life / (float)target.lifeMax;
                if (hasTrack && !isStealthHit && hpPercent > 0.8f) modifiers.FinalDamage *= 1.25f;
                if (hasPierce && isStealthHit && hpPercent < 0.25f) modifiers.FinalDamage *= 1.50f;
            }

            if (ActiveSigils.Contains(18) && hasTrack) modifiers.CritDamage += 0.5f;
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.myPlayer != Player.whoAmI || !MalachiteCache.IsMalachiteItem(Player.HeldItem)) return;

            bool isStealthHit = StealthSystem.IsStealthStrike(proj);
            bool isNormalHit = MalachiteCache.IsNormalProj(proj) && !isStealthHit;
            bool hasTrack = ActiveSigils.Contains(1);
            bool hasPierce = ActiveSigils.Contains(2);

            ProcessSigilOnHitEffects(proj, target, hit, damageDone, isStealthHit, isNormalHit, hasTrack, hasPierce);
        }

        private void ProcessSigilOnHitEffects(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, bool isStealthHit, bool isNormalHit, bool hasTrack, bool hasPierce)
        {
            if (ActiveSigils.Contains(5)) HandleLifesteal(isNormalHit, isStealthHit, hasTrack, hasPierce);
            if (ActiveSigils.Contains(8)) TriggerSigil_Sylvan(proj, target, hit, damageDone, isStealthHit, isNormalHit, hasTrack, hasPierce);
            if (ActiveSigils.Contains(3)) TriggerSigil_Poison(target, hit, damageDone, isStealthHit, isNormalHit, hasTrack, hasPierce);
            if (ActiveSigils.Contains(12)) TriggerSigil_Astral(target, hit, damageDone, isStealthHit, isNormalHit, hasTrack, hasPierce);
            if (ActiveSigils.Contains(14)) HandleHolyFlames(proj, target, isNormalHit, hasTrack, hasPierce);

            if (ActiveSigils.Contains(10) && hit.Crit) TriggerSigil_Explosion(target, damageDone, isStealthHit, isNormalHit, hasTrack, hasPierce);
            if (ActiveSigils.Contains(9) && hasPierce && isStealthHit) TriggerSigil_Gravity(target);
            if (ActiveSigils.Contains(11) && hasPierce && MalachiteCache.IsBolt(proj)) Player.AddBuff(BuffID.RapidHealing, 120);
            if (ActiveSigils.Contains(13)) TriggerSigil_Moon(target, hit, damageDone, isStealthHit, isNormalHit, hasTrack, hasPierce);
            if (ActiveSigils.Contains(16) && hasPierce && isStealthHit && MalachiteCache.IsBolt(proj)) TriggerSigil_Refract(proj, target, damageDone);
            if (ActiveSigils.Contains(17)) TriggerSigil_Lightning(target, damageDone, isNormalHit, hasTrack, hasPierce);
            if (ActiveSigils.Contains(18) && hasPierce && isStealthHit && hit.Crit) TriggerSigil_Witch(target, damageDone);
            if (ActiveSigils.Contains(19)) TriggerSigil_Polterghast(target, hit, damageDone, isStealthHit, isNormalHit, hasTrack, hasPierce);
        }

        private void HandleLifesteal(bool isNormalHit, bool isStealthHit, bool hasTrack, bool hasPierce)
        {
            if (hasTrack && isNormalHit && lifestealCooldownTrack <= 0)
            {
                Player.statLife = Math.Min(Player.statLife + 2, Player.statLifeMax2);
                Player.HealEffect(2, true);
                lifestealCooldownTrack = 30;
            }
            else if (hasPierce && isStealthHit && lifestealCooldownPierce <= 0)
            {
                Player.statLife = Math.Min(Player.statLife + 25, Player.statLifeMax2);
                Player.HealEffect(25, true);
                Player.AddBuff(BuffID.Endurance, 240);
                lifestealCooldownPierce = 300;
            }
        }

        private void HandleHolyFlames(Projectile proj, NPC target, bool isNormalHit, bool hasTrack, bool hasPierce)
        {
            if (hasTrack && isNormalHit) target.AddBuff(MalachiteCache.HolyFlames, 180);
            if (hasPierce && MalachiteCache.IsBolt(proj))
            {
                target.AddBuff(MalachiteCache.HolyFlames, 300);
                target.AddBuff(BuffID.Ichor, 300);
            }
        }

        private void TriggerSigil_Sylvan(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, bool isStealthHit, bool isNormalHit, bool hasTrack, bool hasPierce)
        {
            if (hasTrack && isNormalHit && hit.Crit && sylvanCooldown <= 0)
            {
                sylvanCooldown = 10;
                SoundEngine.PlaySound(SoundID.Grass, target.Center);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 leafVel = Main.rand.NextVector2CircularEdge(12f, 12f);
                    int p = Projectile.NewProjectile(Player.GetSource_OnHit(target), target.Center, leafVel, ProjectileID.Leaf, damageDone / 4, 1f, Player.whoAmI);
                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Main.projectile[p].usesIDStaticNPCImmunity = false;
                        Main.projectile[p].usesLocalNPCImmunity = true;
                        Main.projectile[p].localNPCHitCooldown = 15;
                        Main.projectile[p].timeLeft = 240;
                    }
                }
            }
            else if (hasPierce && (isStealthHit || MalachiteCache.IsBolt(proj)))
            {
                target.AddBuff(BuffID.Venom, 240);
                target.AddBuff(BuffID.Slow, 120);

                for (int i = 0; i < 6; i++)
                {
                    EffectLimiterSystem.SpawnSpark(target.Center + Main.rand.NextVector2Circular(target.width / 2f, target.height / 2f), Main.rand.NextVector2Circular(4f, 4f), MalachitePalette.GreenDark, 1.2f, 20);
                }

                if (sylvanCooldown <= 0)
                {
                    sylvanCooldown = 10;
                    for (int i = 0; i < 5; i++)
                    {
                        int p = Projectile.NewProjectile(Player.GetSource_OnHit(target), target.Center, Main.rand.NextVector2Circular(16f, 16f), ProjectileID.FlowerPetal, damageDone / 5, 0f, Player.whoAmI);
                        if (p >= 0 && p < Main.maxProjectiles)
                        {
                            Main.projectile[p].penetrate = 3;
                            Main.projectile[p].timeLeft = 240;
                            Main.projectile[p].scale = Main.rand.NextFloat(1.2f, 1.6f);
                            Main.projectile[p].usesIDStaticNPCImmunity = false;
                            Main.projectile[p].usesLocalNPCImmunity = true;
                            Main.projectile[p].localNPCHitCooldown = 15;
                        }
                    }
                }
            }
        }

        private void TriggerSigil_Poison(NPC target, NPC.HitInfo hit, int damageDone, bool isStealthHit, bool isNormalHit, bool hasTrack, bool hasPierce)
        {
            if (hasTrack && isNormalHit && Main.rand.NextBool(4))
            {
                int p = Projectile.NewProjectile(Player.GetSource_OnHit(target), target.Center, Main.rand.NextVector2Circular(8f, 8f), ProjectileID.Wasp, damageDone / 2, 0f, Player.whoAmI);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].timeLeft = 180;
                    Main.projectile[p].usesIDStaticNPCImmunity = false;
                    Main.projectile[p].usesLocalNPCImmunity = true;
                    Main.projectile[p].localNPCHitCooldown = 12;
                }
            }
            else if (hasPierce && isStealthHit && hit.Crit)
            {
                int p = Projectile.NewProjectile(Player.GetSource_OnHit(target), target.Center, Vector2.Zero, ProjectileID.ToxicCloud, damageDone / 2, 0f, Player.whoAmI);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].usesIDStaticNPCImmunity = false;
                    Main.projectile[p].usesLocalNPCImmunity = true;
                    Main.projectile[p].localNPCHitCooldown = 12;
                }
            }
        }

        private void TriggerSigil_Astral(NPC target, NPC.HitInfo hit, int damageDone, bool isStealthHit, bool isNormalHit, bool hasTrack, bool hasPierce)
        {
            if (hasTrack && isNormalHit && astralCooldown <= 0)
            {
                astralCooldown = 20;
                for (int i = 0; i < 2; i++)
                {
                    Vector2 velocity = (target.Center - Player.Center).SafeNormalize(Vector2.UnitY).RotatedByRandom(0.2f) * 15f;
                    int pIdx = Projectile.NewProjectile(Player.GetSource_OnHit(target), Player.Center, velocity, ProjectileID.AmethystBolt, damageDone / 2, 2f, Player.whoAmI);
                    if (pIdx >= 0 && pIdx < Main.maxProjectiles)
                    {
                        Main.projectile[pIdx].penetrate = 1;
                        Main.projectile[pIdx].timeLeft = 180;
                        Main.projectile[pIdx].usesIDStaticNPCImmunity = false;
                        Main.projectile[pIdx].usesLocalNPCImmunity = true;
                        Main.projectile[pIdx].localNPCHitCooldown = 10;
                    }
                }
            }
            else if (hasPierce && isStealthHit && hit.Crit)
            {
                target.SimpleStrikeNPC(damageDone * 2, 0, false, 0, DamageClass.Generic, true, Player.whoAmI, true);
                SoundEngine.PlaySound(SoundID.Item29, target.Center);

                for (int i = 0; i < 8; i++)
                {
                    EffectLimiterSystem.SpawnSpark(target.Center + Main.rand.NextVector2Circular(target.width / 2f, target.height / 2f), Main.rand.NextVector2Circular(7f, 7f), MalachitePalette.AccentPurple, 1.8f, 25);
                }
            }
        }

        private void TriggerSigil_Explosion(NPC target, int damageDone, bool isStealthHit, bool isNormalHit, bool hasTrack, bool hasPierce)
        {
            if (hasTrack && isNormalHit)
            {
                if (plagueExplosionCooldown <= 0)
                {
                    plagueExplosionCooldown = 6;
                    int extraDmg = (int)(target.lifeMax * 0.005f);
                    if (target.boss) extraDmg = Math.Min(60, extraDmg);

                    target.SimpleStrikeNPC(extraDmg, 0, false, 0, DamageClass.Generic, true, Player.whoAmI, true);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = 0.2f }, target.Center);
                    for (int i = 0; i < 8; i++)
                    {
                        EffectLimiterSystem.SpawnSpark(target.Center, Main.rand.NextVector2Circular(6f, 6f), MalachitePalette.PrimaryGreen, 1.8f, 20);
                    }
                }
            }
            else if (hasPierce && isStealthHit)
            {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f }, target.Center);

                for (int i = 0; i < 15; i++)
                {
                    Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                    EffectLimiterSystem.SpawnSpark(target.Center + Main.rand.NextVector2Circular(target.width / 2f, target.height / 2f), vel, MalachitePalette.PrimaryGreen, Main.rand.NextFloat(2f, 3.5f), 30);
                }

                foreach (NPC n in Main.ActiveNPCs)
                {
                    if (n.CanBeChasedBy() && n.DistanceSQ(target.Center) < 200f * 200f && n.whoAmI != target.whoAmI)
                    {
                        n.SimpleStrikeNPC((int)(damageDone * 1.5f), 0, false, 0, DamageClass.Generic, true, Player.whoAmI, true);
                    }
                }
            }
        }

        private void TriggerSigil_Gravity(NPC target)
        {
            target.AddBuff(BuffID.Slow, 120);
            for (int i = 0; i < 5; i++)
            {
                Vector2 pos = target.Center + Main.rand.NextVector2Circular(target.width / 2f, target.height / 2f);
                EffectLimiterSystem.SpawnSpark(pos, (target.Center - pos).SafeNormalize(Vector2.Zero) * 3f, MalachitePalette.AccentCyan, 1.2f, 15);
            }

            foreach (NPC n in Main.ActiveNPCs)
            {
                if (n.CanBeChasedBy() && !n.boss && n.whoAmI != target.whoAmI && n.knockBackResist > 0f && n.realLife == -1)
                {
                    if (n.DistanceSQ(target.Center) < 300f * 300f)
                    {
                        n.velocity += (target.Center - n.Center).SafeNormalize(Vector2.Zero) * 4f;
                    }
                }
            }
        }

        private void TriggerSigil_Moon(NPC target, NPC.HitInfo hit, int damageDone, bool isStealthHit, bool isNormalHit, bool hasTrack, bool hasPierce)
        {
            if (hasTrack && isNormalHit && hit.Crit && moonPortalCooldown <= 0)
            {
                moonPortalCooldown = 180;
                int portalCount = 0;
                Projectile oldestPortal = null;
                int minTimeLeft = int.MaxValue;

                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.owner == Player.whoAmI && p.type == ProjectileID.MoonlordTurret)
                    {
                        portalCount++;
                        if (p.timeLeft < minTimeLeft)
                        {
                            minTimeLeft = p.timeLeft;
                            oldestPortal = p;
                        }
                    }
                }

                if (portalCount >= 2 && oldestPortal != null) oldestPortal.Kill();

                Vector2 portalPos = target.Center - new Vector2(0, 150);
                int pIdx = Projectile.NewProjectile(Player.GetSource_OnHit(target), portalPos, Vector2.Zero, ProjectileID.MoonlordTurret, damageDone, 2f, Player.whoAmI);
                if (pIdx >= 0 && pIdx < Main.maxProjectiles) Main.projectile[pIdx].originalDamage = damageDone;
            }
            else if (hasPierce && isStealthHit && moonSphereCooldown <= 0)
            {
                moonSphereCooldown = 20;
                int pIdx = Projectile.NewProjectile(Player.GetSource_OnHit(target), target.Center, Vector2.Zero, ProjectileID.Electrosphere, damageDone / 2, 0f, Player.whoAmI);
                if (pIdx >= 0 && pIdx < Main.maxProjectiles)
                {
                    Main.projectile[pIdx].hostile = false;
                    Main.projectile[pIdx].friendly = true;
                    Main.projectile[pIdx].DamageType = DamageClass.Generic;
                    Main.projectile[pIdx].usesIDStaticNPCImmunity = false;
                    Main.projectile[pIdx].usesLocalNPCImmunity = true;
                    Main.projectile[pIdx].localNPCHitCooldown = 15;
                }
            }
        }

        private void TriggerSigil_Refract(Projectile proj, NPC target, int damageDone)
        {
            bool alreadyRefracted = false;
            if (proj.TryGetGlobalProjectile<MalachiteGlobalProjectile>(out var gProj))
            {
                alreadyRefracted = gProj.isRefracted;
            }

            if (!alreadyRefracted)
            {
                Vector2 baseDir = proj.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 3; i++)
                {
                    float angle = MathHelper.ToRadians(-15f + 15f * i);
                    Vector2 dir = baseDir.RotatedBy(angle) * 12f;
                    int refrType = proj.type == MalachiteCache.NativeBoltType ? MalachiteCache.NativeBoltType : MalachiteCache.BoltType;
                    int pIdx = Projectile.NewProjectile(Player.GetSource_OnHit(target), target.Center, dir, refrType, damageDone / 2, 0f, Player.whoAmI);
                    if (pIdx >= 0 && pIdx < Main.maxProjectiles)
                    {
                        if (Main.projectile[pIdx].TryGetGlobalProjectile<MalachiteGlobalProjectile>(out var newGProj))
                        {
                            newGProj.isRefracted = true;
                        }
                        StealthSystem.MarkStealthStrike(Main.projectile[pIdx]);
                        Main.projectile[pIdx].usesIDStaticNPCImmunity = false;
                        Main.projectile[pIdx].usesLocalNPCImmunity = true;
                        Main.projectile[pIdx].localNPCHitCooldown = 10;
                    }
                }
            }
        }

        private void TriggerSigil_Lightning(NPC target, int damageDone, bool isNormalHit, bool hasTrack, bool hasPierce)
        {
            if (hasPierce) target.AddBuff(BuffID.Electrified, 180);
            if (hasTrack && isNormalHit && lightningCooldown <= 0)
            {
                lightningCooldown = 6;
                int electroCount = 0;
                foreach (NPC n in Main.ActiveNPCs)
                {
                    if (n.CanBeChasedBy() && n.DistanceSQ(target.Center) < 300f * 300f && n.whoAmI != target.whoAmI)
                    {
                        for (int d = 0; d < 5; d++)
                        {
                            EffectLimiterSystem.SpawnSpark(Vector2.Lerp(target.Center, n.Center, d / 5f), Main.rand.NextVector2Circular(2f, 2f), MalachitePalette.AccentCyan, 1.2f, 15);
                        }
                        n.SimpleStrikeNPC((int)(damageDone * 0.4f), 0, false, 0, DamageClass.Generic, true, Player.whoAmI, true);
                        if (++electroCount >= 3) break;
                    }
                }
            }
        }

        private void TriggerSigil_Witch(NPC target, int damageDone)
        {
            if (witchExplosionCooldown <= 0)
            {
                witchExplosionCooldown = 15;
                SoundEngine.PlaySound(SoundID.Item74, target.Center);
                for (int i = 0; i < 15; i++)
                {
                    EffectLimiterSystem.SpawnSpark(target.Center + Main.rand.NextVector2Circular(target.width / 2f, target.height / 2f), Main.rand.NextVector2Circular(12f, 12f), MalachitePalette.DangerRed, 2.5f, 35);
                }

                foreach (NPC n in Main.ActiveNPCs)
                {
                    if (n.CanBeChasedBy() && n.DistanceSQ(target.Center) < 250f * 250f)
                    {
                        n.SimpleStrikeNPC((int)(damageDone * 1.5f), 0, false, 0, DamageClass.Generic, true, Player.whoAmI, true);
                    }
                }
            }
        }

        private void TriggerSigil_Polterghast(NPC target, NPC.HitInfo hit, int damageDone, bool isStealthHit, bool isNormalHit, bool hasTrack, bool hasPierce)
        {
            if (hasTrack && isNormalHit && hit.Crit)
            {
                Player.statMana = Math.Min(Player.statManaMax2, Player.statMana + 1);
                Player.ManaEffect(1);
            }
            else if (hasPierce && isStealthHit)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                    int pIdx = Projectile.NewProjectile(Player.GetSource_OnHit(target), target.Center, velocity, ProjectileID.LostSoulFriendly, damageDone / 3, 1f, Player.whoAmI);
                    if (pIdx >= 0 && pIdx < Main.maxProjectiles)
                    {
                        Main.projectile[pIdx].DamageType = DamageClass.Generic;
                        Main.projectile[pIdx].timeLeft = 180;
                        Main.projectile[pIdx].penetrate = 1;
                        Main.projectile[pIdx].usesIDStaticNPCImmunity = false;
                        Main.projectile[pIdx].usesLocalNPCImmunity = true;
                        Main.projectile[pIdx].localNPCHitCooldown = 15;
                    }
                }
            }
        }

        public void TriggerRightClickDialogue()
        {
            int stage = PeacockModifier.GetMalachiteStage();
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