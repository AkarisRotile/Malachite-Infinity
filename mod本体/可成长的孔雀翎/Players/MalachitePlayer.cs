// 本文件为自主实现（无外部参考源码）。
// 无任何灾厄编译期依赖 / 运行时探测。
// v2（M3）起：旧 20 节点星图与 Sigil 触发逻辑已整体退役；
// 命中层只做「阶段表护甲穿透 + 五轨档案穿透」的单一钩子（精简钩子）。
// 对话系统、日常/里程碑、领域键、存档 Dialog 键等保持原样。
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
        public int currentMalachiteStage = 0;

        // ===== 星图 3.0「翎羽星网」（2026-09-27）=====
        // 唯一进度载体：**已点亮的节点 id 集合**（稳定字符串键，只增不改）。
        // 旧版的 UnlockedCrestNodes（击杀即解锁的纹章节点）已并入本集合 —— 见 LoadData 的迁移。
        // 剑阵/射线由 CrestNodes 在普攻当帧直接生成，**不经本类中转**。
        public readonly HashSet<string> StarNodes = new HashSet<string>();

        // 求值缓存：StarNodes 变动时置脏，Profile 属性按需重算（避免逐帧遍历 36 节点）
        private TalentProfile _starProfile;
        private bool _starProfileDirty = true;

        /// <summary>当前星网档案（数值 + 质变标志位）。所有战斗层只读它。</summary>
        public TalentProfile Profile
        {
            get
            {
                if (_starProfileDirty)
                {
                    _starProfile = StarEffects.Build(StarNodes);
                    _starProfileDirty = false;
                }
                return _starProfile;
            }
        }

        /// <summary>星网集合被外部改动后调用，使档案缓存失效。</summary>
        public void InvalidateStarProfile() => _starProfileDirty = true;

        // 节点节拍累加器（按「实际发射的弹幕数」累计，消费后归零；运行期态、不进存档）
        public int shotsForBeam = 0;            // 射线节点：距下次发射线还差几发弹幕
        public int shotsForSwordArray = 0;      // 剑阵节点：距下次出剑阵还差几发弹幕
        public int shotsForMyriad = 0;          // 万剑归宗：距下次全向齐射还差几发弹幕
        private int _crestRefreshTimer = 0;     // 纹章存活巡检节流

        // ===== 【回锋】剑阵生成排队（2026-09-27 改造）=====
        // 语义：请求生成剑阵时**先进队列**，由 ProcessSwordArrayQueue 每 5 帧最多放行 3 道。
        // 队列长度同时是增益来源：每有 1 道待生成，最终伤害 ×1.01（独立乘区）。
        // 运行期态，不进存档。
        public int SwordArrayQueue = 0;
        private int _swordArrayQueueTimer = 0;

        /// <summary>待生成剑阵数带来的最终伤害独立乘区（1.0 = 无增幅）。</summary>
        public float PendingArrayDamageMult
        {
            get
            {
                int n = Math.Min(SwordArrayQueue, CrestNodes.ArrayQueueBonusCap);
                return n > 0 ? 1f + n * CrestNodes.ArrayQueueDamageBonusPerArray : 1f;
            }
        }

        /// <summary>请求生成若干道剑阵（进队列，不立即落地）。</summary>
        public void EnqueueSwordArrays(int count)
        {
            if (count <= 0) return;
            SwordArrayQueue = Math.Min(SwordArrayQueue + count, CrestNodes.ArrayQueueBonusCap * 4);
        }

        // ===== 【连锋 · StarFlag.ChainEdge】连续命中同一目标的层数（2026-09-27 改造）=====
        // 语义：翎羽飞刀连续打同一目标时逐层增伤；换目标或 2 秒未命中即清零。
        // 运行期态，不进存档。
        public int LinkStacks = 0;
        public int LinkTargetWhoAmI = -1;
        private int _linkDecayTimer = 0;

        /// <summary>【连锋】每次命中同一目标时叠加一层（换目标则从 1 层重新开始）。</summary>
        public void ApplyLinkStack(NPC target)
        {
            if (target == null) return;
            if (LinkTargetWhoAmI != target.whoAmI)
            {
                LinkTargetWhoAmI = target.whoAmI;
                LinkStacks = 1;
            }
            else
            {
                LinkStacks = Math.Min(CrestNodes.LinkMaxStacks, LinkStacks + 1);
            }
            _linkDecayTimer = CrestNodes.LinkDecayFrames;
        }

        /// <summary>【连锋】层数衰减：2 秒没再命中同一目标就清空。</summary>
        private void UpdateLinkStacks()
        {
            if (LinkStacks <= 0) return;
            if (_linkDecayTimer > 0) { _linkDecayTimer--; return; }
            LinkStacks = 0;
            LinkTargetWhoAmI = -1;
        }

        /// <summary>
        /// 队列处理器：每 <see cref="CrestNodes.ArrayQueueBatchFrames"/> 帧放行最多
        /// <see cref="CrestNodes.ArrayQueueBatchMax"/> 道剑阵，超出继续排队。
        /// </summary>
        private void ProcessSwordArrayQueue()
        {
            if (SwordArrayQueue <= 0) { _swordArrayQueueTimer = 0; return; }
            if (Main.myPlayer != Player.whoAmI) return;      // 生成只在归属端

            if (_swordArrayQueueTimer > 0) { _swordArrayQueueTimer--; return; }
            _swordArrayQueueTimer = CrestNodes.ArrayQueueBatchFrames;

            int budget = Math.Min(CrestNodes.ArrayQueueBatchMax, SwordArrayQueue);
            int wd = Player.HeldItem != null && !Player.HeldItem.IsAir ? Player.GetWeaponDamage(Player.HeldItem) : 32;
            int dmg = Math.Max(1, (int)(wd * CrestNodes.ArrayDamageMult));
            float kb = Player.HeldItem?.knockBack ?? 2f;

            for (int i = 0; i < budget; i++)
            {
                if (CrestNodes.SpawnSwordArray(Player, dmg, kb) <= 0) break;   // 撞软上限 → 本轮停手，继续排队
                SwordArrayQueue--;
            }
        }

        // ===== 翠幕（防御节点）：每 CrestNodes.WardRechargeFrames 充一层，受伤时抵消一次 =====
        // 运行期态，不进存档（重进世界重新蓄能，避免登录即白嫖一层）
        public int wardCooldown = 0;            // 剩余充能帧数（0 = 可充）
        public bool wardReady = false;          // 是否持有可用护幕
        private int _wardLogTimer = 0;          // 提示节流

        // 远程普攻为直线飞刀，不需要潜伏值
        public int dailyChatTimer = 0;
        public int playerIdleTime = 0;
        public bool wasLowHealth = false;
        public bool hasGreetedLogin = false;
        public bool hasDiscoveredTruth = false;

        public HashSet<string> SavedDialogGroups = new HashSet<string>();

        public int treeDropCooldown = 0;

        public bool InCombat { get; private set; }

        // ===== 星图 3.0（v3）：技能点 + 已点亮节点集（见 Core\StarNetwork.cs / StarEffects.cs）=====
        /// <summary>
        /// 星网存档版本。v3 = 翎羽星网（节点集合）；
        /// v2 = 旧五轨加点（读得到、可迁移）；缺省 = 更早的旧星图（清空 + 补偿）。
        /// </summary>
        public const int StarSaveVersion = 3;
        public const int LegacyTalentSaveVersion = 2;

        public bool TalentMigrationApplied = false; // 旧档迁移补偿已发放（沿用旧键，语义不变）
        public int SkillPoints = 0;

        // ===== 近战形态（ES 流 v6.4 全招式状态机，非存档字段）=====
        public int MeleeComboStep = 0;
        public int AirComboStep = 0;   // 空战三连段步进（j.A→j.B→j.C；落地复位）
        public int MeleeLastStrikeTick = -100;
        public int MeleeFireCd = 0;
        public int LandingSweepWindow = 0;   // D33：下砸落地目押窗口（Impact 置 6，PreUpdate 递减）
        public int AirChainTailWindow = 0;   // D32：空战三连后摇窗口（SpawnAirChain 置 6，PreUpdate 递减）
        public int AerialVortexWindow = 0;    // D35：升龙命中后 8 帧窗口（OnHitNPC 置 8，PreUpdate 递减）
        private int _vortexHoldFrames = 0;
        private bool _prevCtrlUp = false;
        private int _lastUpTapTick = -100;
        private bool _warpGroundArmed = false;
        private int _chargeStage = 0;           // 蓄力阶段：0=下一次为蓄力重斩；1=下一次为满月终结（蓄力二连）
        /// <summary>
        /// 上次蓄力重斩/满月终结的出手时刻。
        /// <para/>★ 2026-09-27 新增：此前"蓄力出手间隔"是**靠复读锁兼职**实现的
        /// （被锁 → 落空），星核「刃舞·无想」解除复读锁后就直接退化成"每帧挥一次"。
        /// 现在出手间隔独立记账，与复读锁完全解耦。
        /// </summary>
        private int _lastHeavyFireTick = -1000;
        private bool _chargeAutoFired = false;  // 本段蓄力已在阈值自动释放（吞掉随后松手）
        public int PrecisionStacks = 0;
        public int PrecisionTimer = 0;
        public int LastDashTick = -100;
        public int LastHitstopTick = -100; // 上次卡肉触发时刻（玩家级抑制窗口）
        private bool _prevCtrlLeft = false;
        private bool _prevCtrlRight = false;
        private int _lastLeftTapTick = -100;
        private int _lastRightTapTick = -100;

        // ===== v5.2 ES 招式输入态（非存档字段）=====
        private bool _meleePrevKey = false;
        private int _meleeHoldStartTick = -100;
        private bool _meleeCharged = false;
        private bool _meleeTapBuffered = false;

        private int _draedonCheckTimer = 0;

        // ===== v6.4 ES 取消管线输入态（每帧刷新，供 MeleeSlashProj 取消窗读取）=====
        public int AirStallFrames = 0;                   // 空战 j.A/j.B 滞空锁重力剩余帧（PreUpdateMovement 消费）
        public bool MeleeJustPressed { get; private set; } // 近战键本帧按下（目押取消 Gatling）
        public bool JumpJustPressed { get; private set; }  // 跳跃键本帧按下（跳跃取消 JC）
        public int GatlingConsumedTick = -100;               // 目押取消消费按键的时刻（用于吞掉随后的松手）
        private bool _prevJumpCtrl = false;

        public override void SaveData(TagCompound tag)
        {
            tag["hasObtainedMalachite"] = hasObtainedMalachite;
            tag["currentMalachiteStage"] = currentMalachiteStage;
            tag["hasDiscoveredTruth"] = hasDiscoveredTruth;
            tag["DialogGroups"] = SavedDialogGroups.ToList();

            // ---- 星图 3.0：技能点 + 已点亮节点集 ----
            tag["starVersion"] = StarSaveVersion;
            tag["talentMigrationApplied"] = TalentMigrationApplied;
            tag["skillPoints"] = SkillPoints;
            tag["starNodes"] = StarNodes.ToList();
        }

        public override void LoadData(TagCompound tag)
        {
            hasObtainedMalachite = tag.GetBool("hasObtainedMalachite");
            currentMalachiteStage = tag.GetInt("currentMalachiteStage");
            hasDiscoveredTruth = tag.GetBool("hasDiscoveredTruth");

            if (tag.ContainsKey("DialogGroups"))
                SavedDialogGroups = new HashSet<string>(tag.GetList<string>("DialogGroups"));

            // ---- 星图读取 / 迁移（v3 = 翎羽星网）----
            // 逐键 ContainsKey 防御（与 LoadWorldData 风格一致）：损坏/手改存档不致抛 KeyNotFoundException。
            StarNodes.Clear();

            int starVersion = tag.ContainsKey("starVersion") ? tag.GetInt("starVersion") : 0;
            if (starVersion >= StarSaveVersion)
            {
                // 当前版本：直接读节点集合
                TalentMigrationApplied = tag.ContainsKey("talentMigrationApplied") && tag.GetBool("talentMigrationApplied");
                SkillPoints = tag.ContainsKey("skillPoints") ? tag.GetInt("skillPoints") : 0;
                if (tag.ContainsKey("starNodes")) ReadStarNodes(tag.GetList<string>("starNodes"));
                StarNetwork.Sanitize(StarNodes);
            }
            else if (starVersion == LegacyTalentSaveVersion || tag.ContainsKey("talentVersion"))
            {
                // 旧档（v2 五轨体系）：平移纹章节点 + 五轨全额退还点数（免费洗点）
                TalentMigrationApplied = tag.ContainsKey("talentMigrationApplied") && tag.GetBool("talentMigrationApplied");
                SkillPoints = tag.ContainsKey("skillPoints") ? tag.GetInt("skillPoints") : 0;

                // ① 旧纹章节点（击杀即解锁）→ 平移为已点亮星网节点，不扣点
                if (tag.ContainsKey("crestNodes"))
                    StarNetwork.MigrateLegacyCrests(tag.GetList<string>("crestNodes"), StarNodes);

                // ② 旧五轨等级 → 折算退还点数，并尽量把原构筑意图映射成星网节点
                int[] legacyTracks = tag.ContainsKey("trackLevel")
                    ? tag.GetList<int>("trackLevel").ToArray()
                    : new int[TalentCatalog.TrackCount];
                if (legacyTracks.Length != TalentCatalog.TrackCount)
                    legacyTracks = new int[TalentCatalog.TrackCount];

                int spent = TalentCatalog.LegacyRefund(legacyTracks);
                // 已映射成节点的部分不再重复退点：映射消耗 = 每轨 min(等级,3) 个星尘（各 1 点）+ 并发轨最多 2 点
                int mappedCost = 0;
                for (int i = 0; i < legacyTracks.Length; i++)
                {
                    if (i == (int)TrackKind.Volley) mappedCost += Math.Min(Math.Max(0, legacyTracks[i]), 2);
                    else mappedCost += Math.Min(Math.Max(0, legacyTracks[i]), 3);
                }
                TalentCatalog.MapLegacyTracks(legacyTracks, StarNodes);
                SkillPoints += Math.Max(0, spent - mappedCost);

                StarNetwork.Sanitize(StarNodes);
                _starProfileDirty = true;   // 迁移后档案必须重算
            }
            else
            {
                // 更早的档（v1 星图或更早）：清空重来，补偿在 OnEnterWorld 一次性发放
                TalentMigrationApplied = false;
                SkillPoints = 0;
                StarNetwork.Sanitize(StarNodes);   // 至少保证翎心点亮
            }

            _starProfileDirty = true;
        }

        /// <summary>逐键防御读入节点 id（跳过空串与重复）。</summary>
        private void ReadStarNodes(IList<string> ids)
        {
            if (ids == null) return;
            foreach (string nodeId in ids)
            {
                if (!string.IsNullOrEmpty(nodeId))
                    StarNodes.Add(nodeId);
            }
        }

        /// <summary>
        /// D10 补偿：5 基准 + 当前世界已击杀不同 Boss 数（封顶 30），每人一次。
        /// 同时补发已达成条件但缺失的纹章节点（幂等）——覆盖"先打完骷髅王才装本模组"的档。
        /// </summary>
        public override void OnEnterWorld()
        {
            if (Main.myPlayer != Player.whoAmI) return;

            // 星网集合净化（剔除未知 id / 保证翎心在集合内），并对所有本地玩家执行
            // —— 星网进度属玩家存档数据，与世界档解耦。
            _crestRefreshTimer = 0;
            if (StarNetwork.Sanitize(StarNodes)) _starProfileDirty = true;
            _starProfileDirty = true;

            if (TalentMigrationApplied) return;
            int kills = ModContent.GetInstance<MalachiteProgress>()?.DefeatedBossCount ?? 0;
            SkillPoints = Math.Max(SkillPoints, 5 + Math.Min(kills, 30));
            TalentMigrationApplied = true;
        }

        // ==================== 星网操作（UI 唯一入口）====================

        /// <summary>购买一个星网节点（成功则扣点并刷新档案缓存）。</summary>
        public bool PurchaseStar(string nodeId)
        {
            if (!StarNetwork.TryPurchase(StarNodes, nodeId, ref SkillPoints)) return false;
            _starProfileDirty = true;
            return true;
        }

        /// <summary>退还一个星网节点（返还其消耗；会造成孤儿星时拒绝）。</summary>
        public bool RefundStar(string nodeId)
        {
            if (!StarNetwork.TryRefund(StarNodes, nodeId, ref SkillPoints)) return false;
            _starProfileDirty = true;
            return true;
        }

        /// <summary>一键洗点：清空除翎心外的全部节点并全额退点。</summary>
        public int RespecAllStars()
        {
            int refund = StarNetwork.RefundAll(StarNodes);
            SkillPoints += refund;
            _starProfileDirty = true;
            return refund;
        }

        public void RecordDialogGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return;
            SavedDialogGroups.Add(groupId);
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (MalachiteKeybinds.DialogueKey?.JustPressed == true)
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

            // 碧翎念涌（星图 3.0 起为**星核节点**「碧翎念涌」的产物，不再是无条件原型技能）
            // 2026-09-27 重做：从"钉在施法点的法阵"改为"展开后跟随玩家的光翼"。
            // 已有同类光翼在场时不重复生成（否则会叠成一团），改为刷新其剩余时间。
            if (MalachiteKeybinds.DomainKey != null && MalachiteKeybinds.DomainKey.JustPressed)
            {
                if (Main.myPlayer == Player.whoAmI
                    && Player.HeldItem != null
                    && Player.HeldItem.type == MalachiteCache.NativeMalachiteItem
                    && Profile.HasFlag(StarFlag.NucleusMindDomain)
                    && Player.statMana >= 30)
                {
                    Player.statMana -= 30;
                    Player.manaRegenDelay = 90;

                    int wingType = ModContent.ProjectileType<MindWingsAura>();
                    bool refreshed = false;
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile p = Main.projectile[i];
                        if (!p.active || p.owner != Player.whoAmI || p.type != wingType) continue;
                        p.timeLeft = MindWingsAura.TotalFrames;   // 续时而不叠加第二条
                        refreshed = true;
                        break;
                    }
                    if (!refreshed)
                    {
                        Projectile.NewProjectile(Player.GetSource_Misc("MindWingsAura"),
                            Player.Center, Vector2.Zero, wingType, 0, 0f, Player.whoAmI);
                    }
                    SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.7f, Pitch = -0.2f }, Player.Center);
                }
            }
        }

        public override void PreUpdate()
        {
            // 【回锋】剑阵生成排队：每 5 帧最多放行 3 道
            ProcessSwordArrayQueue();

            if (treeDropCooldown > 0) treeDropCooldown--;

            // ===== 纹章节点维护：解锁后恒常维持 2 枚绕身纹章（巡检节流，避免每帧全表扫描）=====
            // 剑阵无需在此维护：由 CrestNodes.SpawnSwordArray 在普攻当帧按随机方位生成。
            UpdateCrests();
            UpdateWard();

            if (Main.myPlayer == Player.whoAmI)
            {
                // ===== 近战形态（ES 流 v6.4）：点按=八向判定树 / 按住≥ChargeFrames=蓄力(松手=重斩或满月终结) =====
                if (MalachiteMelee.IsPrototypeActive(Player))
                {
                    if (PrecisionTimer > 0 && --PrecisionTimer == 0)
                        PrecisionStacks = 0;
                    if (MeleeFireCd > 0) MeleeFireCd--;
                    // 复读锁兜底：2 秒未用自动解锁（2026-09-05）
                    if (SpecialLockTimer > 0 && --SpecialLockTimer == 0)
                        SpecialLockActive = false;
                    if (LandingSweepWindow > 0) LandingSweepWindow--;
                    if (AirChainTailWindow > 0) AirChainTailWindow--;

                    // D35 翠华流风：升龙命中后 8 帧窗口内按住 W+F ≥6 帧 → 滞空绞杀
                    if (AerialVortexWindow > 0)
                    {
                        AerialVortexWindow--;
                        var mk = MalachiteKeybinds.MeleeKey;
                        if (Player.controlUp && mk != null && mk.Current)
                        {
                            _vortexHoldFrames++;
                            if (_vortexHoldFrames >= 6)
                            {
                                _vortexHoldFrames = 0;
                                AerialVortexWindow = 0;
                                int wd = Player.HeldItem != null ? Player.GetWeaponDamage(Player.HeldItem) : 32;
                                MalachiteMelee.FireAerialVortex(Player, wd);
                                MeleeFireCd = MalachiteMelee.SwingInterval;
                                ClearMeleeBuffer();
                            }
                        }
                        else _vortexHoldFrames = 0;
                    }
                    else _vortexHoldFrames = 0;
                    // 空战链复位：落地即从 j.A 重新开始（Air-Stall 锁重力期间不算落地）
                    if (Player.velocity.Y == 0f && AirStallFrames <= 0)
                        AirComboStep = 0;

                    var meleeKey = MalachiteKeybinds.MeleeKey;
                    bool cur = meleeKey != null && meleeKey.Current;
                    bool just = meleeKey != null && meleeKey.JustPressed;
                    bool rel = meleeKey != null && meleeKey.JustReleased;
                    int now = (int)Main.GameUpdateCount;

                    // 取消管线输入态：近战键/跳跃键的上升沿（本地玩家）
                    MeleeJustPressed = just;
                    bool jumpCur = Player.controlJump;
                    JumpJustPressed = jumpCur && !_prevJumpCtrl;
                    _prevJumpCtrl = jumpCur;

                    if (just)
                    {
                        _meleeHoldStartTick = now;
                        _meleeCharged = false;
                        // 蓄力改按压式（2026-09-05）：按下不缓冲，避免蓄力途中 cd 到期抢先普攻；缓冲改走松手链
                    }

                    if (cur)
                    {
                        // 按压式蓄力（2026-09-05）：长按达标直接释放，无需松手；连续第二次蓄力 = 满月终结
                        // ★ 2026-09-27 修 bug：星核「刃舞·无想」解除了 TryUseSpecial 的复读锁之后，
                        //   下面那个"被锁则落空"的分支永远不会进入，而本段又把 _meleeHoldStartTick 重置成 -100、
                        //   _meleeCharged 置回 false —— 于是条件每帧成立，**蓄力重斩每帧挥一次**（用户实机反馈）。
                        //   根因：复读锁顺带承担了"蓄力出手间隔"的职责，不该由它兼职。
                        //   修法：蓄力出手**自己**尊重 HeavyGapFrames，与复读锁解耦。
                        bool heavyReady = now - _lastHeavyFireTick >= MalachiteMelee.HeavyGapFrames;
                        if (!_meleeCharged && heavyReady && now - _meleeHoldStartTick >= MalachiteMelee.ChargeFrames)
                        {
                            _meleeCharged = true;
                            _chargeAutoFired = true;
                            _lastHeavyFireTick = now;
                            int wd = Player.HeldItem != null ? Player.GetWeaponDamage(Player.HeldItem) : 32;
                            if (_chargeStage == 1 && PrecisionStacks >= MalachiteMelee.FinisherMinStacks)
                            {
                                // 第二次蓄力 + 精准满层：满月终结——消耗全部精准层数（超必杀资源制，无法无限连放）
                                MalachiteMelee.FireHeavy(Player, true, wd);
                                PrecisionStacks = 0;
                                PrecisionTimer = 0;
                                _chargeStage = 0;
                            }
                            else if (SpecialLockActive && LockedMove == MalachiteMelee.MoveKind.Charged)
                            {
                                // 蓄力重斩被复读锁：本段蓄力落空，阶段不推进（2 秒自动解锁后可再来）
                                _chargeStage = 0;
                            }
                            else
                            {
                                // 第一次蓄力：蓄力重斩
                                MalachiteMelee.FireHeavy(Player, false, wd);
                                _chargeStage = 1;
                            }
                            MeleeFireCd = MalachiteMelee.HeavyGapFrames;
                            ClearMeleeBuffer();
                            GatlingConsumedTick = now;   // 吞掉紧随的松手
                            _meleeHoldStartTick = -100;  // 本段蓄力已消费；松手后重新长按进入下一段
                            _meleeCharged = false;
                        }

                        // —— 特效层（2026-09-05）：蓄力阶段环绕光点（阈值前每 6 帧一颗，预算门先判）——
                        if (!_meleeCharged && now - _meleeHoldStartTick >= 4 && now - _meleeHoldStartTick < MalachiteMelee.ChargeFrames
                            && now % 6 == 0 && EffectLimiterSystem.CanSpawnEffect(1, 60))
                        {
                            float chargeAng = now * 0.25f;
                            Vector2 orb = Player.Center + new Vector2((float)Math.Cos(chargeAng), (float)Math.Sin(chargeAng)) * 34f;
                            EffectLimiterSystem.SpawnSpark(orb, Vector2.Zero, MalachitePalette.GreenBright, 0.7f, 10);
                        }
                    }
                    else if (rel)
                    {
                        // 目押取消（Gatling）/蓄力自动释放：吞掉随之而来的松手，防止一次点按打出两段
                        if (now - GatlingConsumedTick <= 6 || _chargeAutoFired)
                        {
                            GatlingConsumedTick = -100;
                            _chargeAutoFired = false;
                        }
                        else if (LandingSweepWindow > 0)
                        {
                            // D33 拂柳穿心：下砸落地 6 帧目押 → 贴地滑铲横扫，接入段3 断链计数
                            int wd = Player.HeldItem != null ? Player.GetWeaponDamage(Player.HeldItem) : 32;
                            MalachiteMelee.FireLandingSweep(Player, wd);
                            LandingSweepWindow = 0;
                            MeleeComboStep = 2; // 下一击从段3 派生判定
                            MeleeFireCd = MalachiteMelee.SwingInterval;
                        }
                        else if (AirChainTailWindow > 0 && Player.controlDown)
                        {
                            // D32 翠羽点穴：空战三连后摇 6 帧内 S+F → 45° 斜下突刺
                            int wd = Player.HeldItem != null ? Player.GetWeaponDamage(Player.HeldItem) : 32;
                            MalachiteMelee.FireAirNeedle(Player, wd);
                            AirChainTailWindow = 0;
                            MeleeFireCd = MalachiteMelee.SwingInterval;
                        }
                        else if (MeleeFireCd > 0 && Player.controlDown && Player.velocity.Y == 0f && !Player.mount.Active)
                        {
                            // D31 雀返（仅地面）：近战后摇中 S+F → 后空翻脱战；空中 S+F 冷却期走缓冲不误触（D32 修复）
                            MalachiteMelee.FireBackflipRetreat(Player);
                            ClearMeleeBuffer();
                            MeleeFireCd = MalachiteMelee.SwingInterval;
                        }
                        else if (MeleeFireCd <= 0)
                        {
                            // 快速点按 → 段击 / 上挑
                            DoQuickMeleeStrike();
                            MeleeFireCd = MalachiteMelee.SwingInterval;
                        }
                        else
                        {
                            _meleeTapBuffered = true;
                        }
                    }

                    // 缓冲消费（冷却结束当帧出招）
                    if (_meleeTapBuffered && MeleeFireCd <= 0)
                    {
                        _meleeTapBuffered = false;
                        DoQuickMeleeStrike();
                        MeleeFireCd = MalachiteMelee.SwingInterval;
                    }

                    // 双击方向 = 突进斩（不变）
                    bool l = Player.controlLeft;
                    bool r = Player.controlRight;
                    if (l && !_prevCtrlLeft)
                    {
                        if (now - _lastLeftTapTick <= MalachiteMelee.DoubleTapWindow)
                            MalachiteMelee.DoDash(Player, -1);
                        _lastLeftTapTick = now;
                    }
                    if (r && !_prevCtrlRight)
                    {
                        if (now - _lastRightTapTick <= MalachiteMelee.DoubleTapWindow)
                            MalachiteMelee.DoDash(Player, 1);
                        _lastRightTapTick = now;
                    }
                    _prevCtrlLeft = l;
                    _prevCtrlRight = r;

                    // D34 雀跃·凌虚（方案 B）：地面后摇中双击 W → 抛物线瞬步 + 短无敌
                    bool up = Player.controlUp;
                    if (up && !_prevCtrlUp)
                    {
                        if (_warpGroundArmed && now - _lastUpTapTick <= MalachiteMelee.DoubleTapWindow)
                        {
                            MalachiteMelee.FireWarpStep(Player);
                            ClearMeleeBuffer();
                            MeleeFireCd = MalachiteMelee.SwingInterval;
                            _warpGroundArmed = false;
                        }
                        _lastUpTapTick = now;
                        // 第一击须在地面（避免空战 JC 冲突——方案 B）；绞杀窗口内不武装（硬隔离）；无需后摇即可触发
                        _warpGroundArmed = Player.velocity.Y == 0f && !Player.mount.Active && AerialVortexWindow <= 0;
                    }
                    _prevCtrlUp = up;

                    _meleePrevKey = cur;
                }
                else
                {
                    MeleeFireCd = 0;
                    _prevCtrlLeft = false;
                    _prevCtrlRight = false;
                    _meleePrevKey = false;
                    _meleeCharged = false;
                    _meleeTapBuffered = false;
                    MeleeJustPressed = false;
                    JumpJustPressed = false;
                    _prevJumpCtrl = false;
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
        }

        /// <summary>空战 j.A/j.B 空中悬停（§模块二）：滞空期内每帧锁死重力（剩余帧由斩击弹幕写入）。</summary>
        public override void PreUpdateMovement()
        {
            if (AirStallFrames > 0)
            {
                AirStallFrames--;
                Player.gravity = 0f;
            }
        }

        /// <summary>
        /// 翠幕充能：未解锁 → 不做任何事；已解锁 → 冷却归零时充起一层护幕。
        /// 护幕为"待消耗"状态，真正抵消伤害发生在 <see cref="FreeDodge"/>。
        /// </summary>
        private void UpdateWard()
        {
            if (Main.myPlayer != Player.whoAmI) return;

            if (!Profile.HasFlag(StarFlag.JadeWard))
            {
                wardReady = false;
                wardCooldown = 0;
                return;
            }

            if (wardReady) return;                 // 已满，不重复充能
            if (wardCooldown > 0)
            {
                wardCooldown--;
                return;
            }

            wardReady = true;
            // 充能完成的视觉提示（限流，避免与其它粒子抢预算）
            if (EffectLimiterSystem.CanSpawnEffect(2, 90))
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector2 from = Player.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * 42f;
                    int di = Dust.NewDustPerfect(from, DustID.GemEmerald, (Player.Center - from) * 0.10f, 0,
                        MalachitePalette.GreenBright, Main.rand.NextFloat(0.7f, 1.2f)).dustIndex;
                    Main.dust[di].noGravity = true;
                    Main.dust[di].fadeIn = 0.5f;
                }
            }
        }

        /// <summary>
        /// 翠幕抵消：消耗一层护幕使该次伤害完全无效。
        /// 只在本地玩家端结算（FreeDodge 本身是本地判定），并给一段短暂无敌避免同帧多段伤害连触。
        /// </summary>
        public override bool FreeDodge(Player.HurtInfo info)
        {
            if (!wardReady) return false;
            if (Main.myPlayer != Player.whoAmI) return false;
            if (!Profile.HasFlag(StarFlag.JadeWard)) return false;

            wardReady = false;
            wardCooldown = CrestNodes.WardRechargeFrames;
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, CrestNodes.WardGraceFrames);

            // 碎裂反馈：护幕破开的环形碎晶
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = 0.2f }, Player.Center);
            for (int i = 0; i < 14 && EffectLimiterSystem.CanSpawnEffect(1, 70); i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2.5f, 5f);
                int di = Dust.NewDustPerfect(Player.Center + vel * 4f, DustID.GemEmerald, vel, 0,
                    i % 3 == 0 ? MalachitePalette.White : MalachitePalette.GreenBright,
                    Main.rand.NextFloat(0.8f, 1.4f)).dustIndex;
                Main.dust[di].noGravity = true;
                Main.dust[di].fadeIn = 0.5f;
            }

            if (_wardLogTimer <= 0)
            {
                _wardLogTimer = 60;
                CombatText.NewText(Player.Hitbox, MalachitePalette.GreenBright,
                    MalachiteData.Loc("翠幕", "Jade Ward"), dramatic: false);
            }
            return true;
        }

        /// <summary>消费本帧目押按键（Gatling 已接下一段时调用，防止冷却结束重复出招）。</summary>
        public void ClearMeleeBuffer() => _meleeTapBuffered = false;

        /// <summary>
        /// 纹章节点维护：未解锁 → 无事；已解锁 → 维持 2 枚绕身纹章（相位对径）。
        /// 每 15 帧巡检一次，弹幕意外消失（死亡重生/切世界）能在 0.25 秒内自愈。
        /// </summary>
        private void UpdateCrests()
        {
            if (Main.myPlayer != Player.whoAmI) return;
            if (!Profile.HasFlag(StarFlag.RadiantCrest))
            {
                _crestRefreshTimer = 0;
                return;
            }

            if (_crestRefreshTimer > 0) { _crestRefreshTimer--; return; }
            _crestRefreshTimer = 15;

            int crestType = ModContent.ProjectileType<OrbitingCrestProj>();
            // 按相位槽位判定（而非仅计数量）：避免缺失一槽时补出两枚同相位纹章叠在一起
            bool hasSlot0 = false; // 相位 0
            bool hasSlot1 = false; // 相位 π
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Player.whoAmI || p.type != crestType) continue;
                // ai[0] 由生成方写入且此后不变；用容差判定避免浮点比较陷阱
                if (Math.Abs(p.ai[0]) < 0.01f) hasSlot0 = true;
                else hasSlot1 = true;
            }

            if (!hasSlot0) SpawnCrest(crestType, 0f);
            if (!hasSlot1) SpawnCrest(crestType, MathHelper.Pi);
        }

        private void SpawnCrest(int crestType, float phase)
        {
            Projectile.NewProjectile(Player.GetSource_Misc("Crest_Orbit"), Player.Center, Vector2.Zero,
                crestType, 0, 0f, Player.whoAmI, phase, 0f);
        }

        // ===== 防战技复读锁（2026-09-05 用户拍板）：除普攻链与满月终结外，同一战技不得连用；
        // 须以「其它招式造成伤害」解锁（ReportSpecialHit 由命中路径回调）。
        public bool SpecialLockActive = false;
        public MalachiteMelee.MoveKind LockedMove = MalachiteMelee.MoveKind.Step1;
        public int SpecialLockTimer = 0; // 复读锁兜底计时（120 帧 = 2 秒未用自动解锁）
        public void ReportSpecialHit(MalachiteMelee.MoveKind kind)
        {
            if (SpecialLockActive && kind != LockedMove)
            {
                SpecialLockActive = false;
                SpecialLockTimer = 0;
            }
        }

        /// <summary>
        /// 点按近战键的招式分支（§二 输入判定树，优先级自上而下）：
        /// 1) 冲刺态 = **严格双击 A/D 突进窗口**（LastDashTick ≤ DashImmuneTime；不用移动速度/护盾冲刺判定）→ 突进居合切 DashCut；
        /// 2) 空中（下方向→陨石下砸 AirDive；上方向→升龙 UpperRise；无方向→空战三连段 j.A→j.B→j.C）；
        /// 3) 地面（上方向→升龙 UpperRise；否则地面三连段 Step1/2/3）。
        /// </summary>
        private void DoQuickMeleeStrike()
        {
            int wd = Player.HeldItem != null ? Player.GetWeaponDamage(Player.HeldItem) : 32;
            int now = (int)Main.GameUpdateCount;
            bool dashing = now - LastDashTick <= MalachiteMelee.DashImmuneTime; // 严格双击 A/D 判定
            bool airborne = (Player.velocity.Y != 0f || AirStallFrames > 0) && !Player.mount.Active;

            if (dashing)
                MalachiteMelee.FireDashCut(Player, wd);
            else if (airborne)
            {
                if (Player.controlDown)
                    MalachiteMelee.FireAirDive(Player, wd);
                else if (Player.controlUp)
                    MalachiteMelee.FireUpperRise(Player, Player.GetSource_Misc("MalachiteMelee"), wd);
                else
                {
                    // 空战三连段（j.A→j.B→j.C 循环；步进与复位见 PreUpdate）
                    int airStep = AirComboStep % 3;
                    AirComboStep = (airStep + 1) % 3;
                    MalachiteMelee.FireAirChain(Player, Player.GetSource_Misc("MalachiteMelee"), wd, airStep);
                }
            }
            else if (Player.controlUp)
                MalachiteMelee.FireUpperRise(Player, Player.GetSource_Misc("MalachiteMelee"), wd);
            else
                MalachiteMelee.TryMeleeStrike(Player, Player.GetSource_Misc("MalachiteMelee"), wd, 4f);
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
                    // 花下败仗（世界级一次性）：达成阶段5后的首个清晨、世纪之花未败且手持柳刃时触发一次。先 show 再置位防重复。
                    var malachiteProgress = ModContent.GetInstance<MalachiteProgress>();
                    if (!malachiteProgress.FlowerRetryLossShown
                        && ProgressSystem.GetStage() >= 5
                        && !NPC.downedPlantBoss
                        && Main.dayTime && Main.time <= 60)
                    {
                        MalachiteUISystem.ShowDialogGroup("flower_retry_loss", force: true);
                        malachiteProgress.FlowerRetryLossShown = true;
                    }
                }
            }
        }

        /// <summary>
        /// 命中钩子：孔雀柳刃命中提供
        /// 「阶段表基础护甲穿透 + 星网档案穿透」，并在持有「万流归墟」时把**穿甲溢出**转为终伤乘区。
        /// </summary>
        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!MalachiteCache.IsMalachiteItem(Player.HeldItem)) return;

            TalentProfile profile = Profile;
            int totalAP = WillowGrowth.FlatAP(ProgressSystem.GetStage()) + profile.ArmorPen;
            modifiers.ArmorPenetration += totalAP;

            // 万流归墟：穿甲超出目标防御的部分按 1:1 转终伤（上限 +30%）。
            // target.defense 是原版防御值；这里用 TotalArmorPenetration 与实际结算同口径地近似。
            if (profile.HasFlag(StarFlag.NucleusPiercing))
            {
                int def = target != null ? target.defense : 0;
                float mult = profile.PiercingOverflowMult(totalAP, def);
                if (mult > 1f) modifiers.FinalDamage *= mult;
            }
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

            // v2 去灾厄化：不再按灾厄 Boss 名扫描，只保留可可靠判定的原版分支——
            // 死亡时在场 NPC 类型：世界吞噬者头 / 毁灭者 → 蠕虫组；世纪之花 → 花刺组；
            // 无匹配且死于岩浆（Player.lavaWet）→ 岩浆组；其余走默认组。
            bool diedToWorm = false;
            bool diedToFlower = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active) continue;
                if (npc.type == NPCID.EaterofWorldsHead || npc.type == NPCID.TheDestroyer) diedToWorm = true;
                if (npc.type == NPCID.Plantera) diedToFlower = true;
            }

            string selectedId = "death_default";
            if (diedToWorm) selectedId = "death_worm";
            else if (diedToFlower) selectedId = "death_yharon";
            else if (Player.lavaWet) selectedId = "death_providence";

            MalachiteUISystem.ShowDialogGroup(selectedId, force: true);
        }
    }
}
