// 本文件为自主实现，无外部参考代码。
//
// ============================================================================
// 纹章产物的**运行时参数与生成器**（星图 3.0 起）
// ============================================================================
// 2026-09-27 职责收窄：本类此前同时承担"节点身份/解锁条件/UI 文案"三件事，
// 星图 3.0 把这些全部移交给了 Core\StarNetwork.cs（星网唯一出处）。
// 现在本类只剩**运行时**：
//   · 射线（灼翎纹章）、剑阵（流风剑阵 / 万剑归宗）、回锋、翠幕 的参数常量
//   · 生成/信号函数（SignalBeams / FireBeamFrom / SpawnSwordArray / SpawnSwordArrayRing）
//
// 「该不该触发」一律由调用方读 TalentProfile 的 StarFlag 决定 —— 本类不做解锁判定。

using System;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    /// <summary>星网产物的运行时参数与生成器。</summary>
    public static class CrestNodes
    {
        // ==================== 射线（灼翎纹章 / StarFlag.RadiantCrest）====================

        /// <summary>
        /// 射线节拍：每累计发射多少**发弹幕**触发一次（2026-09-25 由"攻击次数"改为"弹幕数"）。
        /// 语义：1 次普攻发射 (1 + 并发弹幕) 发，故并发越高触发越密。
        /// </summary>
        public const int BeamEveryNthShot = 3;

        /// <summary>射线伤害相对本次普攻伤害的倍率（两枚纹章各一道，合计约 0.9×）。</summary>
        public const float BeamDamageMult = 0.45f;

        /// <summary>
        /// 【星宿 · 双生并发】射线扇形：单枚纹章一次最多额外射出几道。
        /// <para/>2026-09-27 用户要求："让提升并发弹幕数量的效果还会同时对射线生效"。
        /// 该行为由 <see cref="StarFlag.TwinVolley"/> 门控（未点亮时射线恒为单发，与旧版一致）。
        /// 上限取 3（即单枚纹章最多 4 道）—— 两枚纹章就是 8 道，再多会把画面糊成光墙。
        /// </summary>
        public const int MaxBeamFanExtra = 3;

        /// <summary>射线扇形的相邻夹角（弧度，约 3.2°）。</summary>
        private const float BeamFanStep = 0.056f;

        /// <summary>射线初速（MalachiteBolt 自带 extraUpdates=8，故视觉上即为瞬时射线）。</summary>
        private const float BeamSpeed = 34f;

        /// <summary>
        /// 由消耗方调用：本帧需要发射线时，向所有在场纹章下发信号。
        /// **只下发给玩家自己拥有的纹章**（owner == whoAmI），避免误触他人纹章。
        /// </summary>
        /// <returns>成功下达信号的纹章数。</returns>
        public static int SignalBeams(Player player)
        {
            if (player == null || !player.active) return 0;
            int crestType = ModContent.ProjectileType<OrbitingCrestProj>();
            int signalled = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI || p.type != crestType) continue;
                p.ai[1] = 1f; // 纹章会在下一次 AI 中消费
                signalled++;
            }
            return signalled;
        }

        /// <summary>
        /// 纹章执行发射：朝鼠标方向生成 <paramref name="extraBeams"/>+1 道 MalachiteBolt（扇形）。
        /// 仅在 owner 端执行（Main.myPlayer 判定），防止多人重复生成。
        /// </summary>
        /// <param name="extraBeams">
        /// 额外射线数（0 = 单发，与旧版一致）。由调用方按星网「并发」档案给出，
        /// 且**必须已确认 <see cref="StarFlag.TwinVolley"/> 已点亮**——本函数不再自行判定门槛。
        /// </param>
        public static void FireBeamFrom(Projectile crest, Player owner, int damage, float knockback, int extraBeams = 0)
        {
            if (crest == null || owner == null || !owner.active) return;
            if (owner.whoAmI != Main.myPlayer) return;

            try
            {
                int beams = 1 + Math.Clamp(extraBeams, 0, MaxBeamFanExtra);
                Vector2 aimDir = (Main.MouseWorld - crest.Center).SafeNormalize(Vector2.UnitX);
                int boltType = ModContent.ProjectileType<MalachiteBolt>();

                for (int i = 0; i < beams; i++)
                {
                    // 以鼠标方向为中轴对称展开扇形
                    float off = (i - (beams - 1) * 0.5f) * BeamFanStep;
                    Projectile.NewProjectile(crest.GetSource_FromThis(), crest.Center,
                        aimDir.RotatedBy(off) * BeamSpeed,
                        boltType, Math.Max(1, damage), knockback, owner.whoAmI);
                }
            }
            catch (Exception) { /* 射线生成失败不应中断普攻：静默降级 */ }
        }

        /// <summary>
        /// 该玩家本次射线应额外发射几道（0 = 单发）。
        /// <para/>语义：「并发弹幕」加成是否作用于射线，由星宿「双生并发」(_TwinVolley) 决定。
        /// </summary>
        public static int BeamFanExtraFor(Player player)
        {
            if (player == null || !player.active) return 0;
            TalentProfile profile = StarEffects.Of(player);
            if (!profile.HasFlag(StarFlag.TwinVolley)) return 0;
            return Math.Clamp(profile.VolleyBonus, 0, MaxBeamFanExtra);
        }

        // ==================== 剑阵（流风剑阵 / StarFlag.SwordArray）====================

        /// <summary>
        /// 剑阵节拍：每累计发射多少**发弹幕**生成一道剑阵。
        /// <para/>为何是 10：无加点时 1 次普攻 = 1 发弹幕 → 10 发出 1 道；
        /// 而并发高时 1 次普攻多发 → 剑阵显著变密，**并发加点真正作用于剑阵**。
        /// </summary>
        public const int ArrayEveryNthShot = 10;

        /// <summary>剑阵生成方位的最小 / 最大距离（px）——"有最远距离限制"。</summary>
        public const float ArraySpawnMinDist = 110f;
        public const float ArraySpawnMaxDist = 300f;

        /// <summary>剑阵相对本次普攻伤害的倍率。</summary>
        public const float ArrayDamageMult = 0.30f;

        /// <summary>场上剑阵数量软上限——高攻速下防止弹幕数爆炸。</summary>
        public const int ArrayMaxAlive = 12;

        // ==================== 【回锋 · StarFlag.ReturnEdge】剑阵排队机制 ====================
        //
        // ★ 2026-09-27 用户要求改造（原效果"剑阵折返再射一次"已随"剑阵不再飞向玩家"一并作废）：
        //   "生成剑阵的数量受到并发弹幕数量增加的效果影响，再给剑阵做一个生成排队，
        //    每 5 帧最多生成三个剑阵，超出这个数量的需要排队生成。
        //    同时，待生成的剑阵数量会以每个剑阵 1% 的独立乘区对最终伤害进行增幅。"

        /// <summary>【回锋】批量落地节拍：每这么多帧处理一次排队。</summary>
        public const int ArrayQueueBatchFrames = 5;

        /// <summary>【回锋】每个节拍最多落地几道剑阵（超出的继续排队）。</summary>
        public const int ArrayQueueBatchMax = 3;

        /// <summary>【回锋】每有一道"待生成"剑阵，最终伤害的独立乘区增幅。</summary>
        public const float ArrayQueueDamageBonusPerArray = 0.01f;

        /// <summary>【回锋】待生成加成的层数上限（防止极端情况下乘区失控）。</summary>
        public const int ArrayQueueBonusCap = 30;

        /// <summary>【回锋 StarFlag.ReturnEdge】折返蓄力帧数。</summary>
        public const int ReturnChargeFrames = 14;

        /// <summary>【回锋】折返时的靠近速度（px/帧）。</summary>
        public const float ReturnHomingSpeed = 34f;

        /// <summary>【回锋】第二次射击的伤害倍率（相对剑阵本体伤害）。</summary>
        public const float ReturnSecondShotMult = 0.85f;

        // ==================== 万剑归宗（StarFlag.NucleusMyriadBlades）====================

        /// <summary>【星核】每累计多少发弹幕触发一轮全向剑阵齐射。</summary>
        public const int MyriadEveryNthShot = 24;

        /// <summary>【星核】一轮齐射同时生成的剑阵数。</summary>
        public const int MyriadArrayCount = 6;

        /// <summary>【星核】齐射剑阵相对普攻伤害的倍率（低于常规剑阵：一次出 6 道，总量更大）。</summary>
        public const float MyriadDamageMult = 0.22f;

        /// <summary>
        /// 在玩家四周随机方位生成一道剑阵。
        /// 方位与远近均随机，落点夹在 [ArraySpawnMinDist, ArraySpawnMaxDist] 内；
        /// 生成方只负责摆位置，起始速度由弹幕自己朝玩家解算。
        /// </summary>
        /// <returns>成功生成的剑阵数（0 = 被软上限拦下）。</returns>
        public static int SpawnSwordArray(Player player, int damage, float knockback)
        {
            if (player == null || !player.active || player.dead) return 0;
            if (player.whoAmI != Main.myPlayer) return 0;
            if (CountAliveArrays(player) >= ArrayMaxAlive) return 0;

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = Main.rand.NextFloat(ArraySpawnMinDist, ArraySpawnMaxDist);
            return CreateArray(player, angle, dist, damage, knockback);
        }

        /// <summary>
        /// 【万剑归宗】全向齐射：在玩家四周**均匀方位**一次性铺开 count 道剑阵。
        /// 与 <see cref="SpawnSwordArray"/> 的随机单发不同，这里方位均分，读起来才是"归宗"而非"散射"。
        /// </summary>
        /// <returns>实际生成的剑阵数。</returns>
        public static int SpawnSwordArrayRing(Player player, int damage, float knockback, int count)
        {
            if (player == null || !player.active || player.dead) return 0;
            if (player.whoAmI != Main.myPlayer) return 0;
            if (count <= 0) return 0;

            int spawned = 0;
            // 起始方位随机，避免每次齐射都从同一角度切进去（观感僵硬）
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < count; i++)
            {
                if (CountAliveArrays(player) >= ArrayMaxAlive) break;
                float angle = baseAngle + MathHelper.TwoPi * i / count;
                float dist = Main.rand.NextFloat(ArraySpawnMinDist, ArraySpawnMaxDist);
                spawned += CreateArray(player, angle, dist, damage, knockback);
            }
            return spawned;
        }

        /// <summary>场上存活的剑阵数（用于 <see cref="ArrayMaxAlive"/> 软上限）。
        /// <para/>注：剑阵已无渐隐阶段（射完即 Kill），因此这里的计数就是"真正还在场上待机/蓄力"的数量。</summary>
        private static int CountAliveArrays(Player player)
        {
            int projType = ModContent.ProjectileType<OrbitingWillowProj>();
            int alive = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI || p.type != projType) continue;
                alive++;
            }
            return alive;
        }

        private static int CreateArray(Player player, float angleRad, float dist, int damage, float knockback)
        {
            Vector2 spawnPos = player.Center + new Vector2((float)Math.Cos(angleRad), (float)Math.Sin(angleRad)) * dist;
            try
            {
                Projectile.NewProjectile(player.GetSource_Misc("Willow_SwordArray"), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<OrbitingWillowProj>(), Math.Max(1, damage), knockback, player.whoAmI, 0f, 0f);
                return 1;
            }
            catch (Exception) { return 0; }
        }

        // ==================== 翠幕（StarFlag.JadeWard）====================

        /// <summary>【翠幕】护幕重新蓄能所需帧数（8 秒 @60fps）。</summary>
        public const int WardRechargeFrames = 480;

        /// <summary>【翠幕】抵消伤害后的短暂无敌帧（避免同帧多段伤害连续触发）。</summary>
        public const int WardGraceFrames = 30;

        // ==================== 碧翎念涌（StarFlag.NucleusMindDomain）====================
        //
        // 2026-09-27 重做：旧「领域展开」是钉在施法点的法阵（放完就跟你没关系，"站回圈里"才有加成）。
        // 现改为**展开后跟随玩家的光翼**，因此：
        //   · 念伤加成不再需要"站在圈里"的判定 —— 光翼就是你的一部分，展开期间常驻生效；
        //   · 半径 320 → 160（AGY：太大反而遮挡视野），只用来判定**敌人**是否被念蚀减速。

        /// <summary>【碧翎念涌】开启期间的念伤害增幅（相对最终伤害）。</summary>
        public const float MindDomainDamageBonus = 0.30f;

        /// <summary>【碧翎念涌】开启期间的并发弹幕加成。</summary>
        public const int MindDomainVolleyBonus = 2;

        /// <summary>
        /// 玩家身上的碧翎念涌是否已开启（供伤害加成与并发加成判定）。
        /// <para/>⚠ 判定"存在即生效"，**不区分是否正在收尾**：关闭时那 18 帧崩解里光翼仍在画面上，
        /// 加成跟着一起淡出反而更连贯。且这样还有一个副作用是好的 —— 收尾期间再按一次 V 会开出一对
        /// 新的光翼（旧的那条已在收尾、不算"已开启"），两者会交叉淡入淡出，不会变成"按了没反应"。
        /// </summary>
        public static bool IsMindDomainActive(Player player)
        {
            if (player == null || !player.active) return false;
            int t = ModContent.ProjectileType<MindWingsAura>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.type == t) return true;
            }
            return false;
        }

        /// <summary>
        /// 某个世界坐标是否落在玩家的光翼软场内（敌人减速的判定；也是"念蚀"的语义边界）。
        /// </summary>
        public static bool IsInsideMindDomain(Player player, Vector2 worldPos)
        {
            if (player == null || !player.active) return false;
            int t = ModContent.ProjectileType<MindWingsAura>();
            float rSq = MindWingsAura.Radius * MindWingsAura.Radius;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI || p.type != t) continue;
                if (Vector2.DistanceSquared(p.Center, worldPos) <= rSq) return true;
            }
            return false;
        }

        // ==================== 【连锋 · StarFlag.ChainEdge】连续命中同一目标 ====================
        //
        // ★ 2026-09-27 用户改造要求：
        //   "连锋这个节点的连招保护时间加长有点不知所谓，45 帧已经够久了，延长到 90 帧很鸡肋。
        //    并且近战攻击也并不是孔雀柳刃的主要攻击模式，作为大节点的话，不太适当。"
        //   → 旧效果（连段断链窗口 45→90 帧）作废，改为围绕**主攻击（翎羽飞刀）**的连击累积机制。

        /// <summary>【连锋】连续命中同一目标时，每层提供的最终伤害增幅。</summary>
        public const float LinkDamagePerStack = 0.04f;

        /// <summary>【连锋】最大层数。</summary>
        public const int LinkMaxStacks = 5;

        /// <summary>【连锋】多久没有再次命中同一目标就清空层数（帧，2 秒）。</summary>
        public const int LinkDecayFrames = 120;

        // ==================== 刃回响（StarFlag.BladeEcho）====================

        /// <summary>【刃回响】飞刀命中后追加的一次回响伤害倍率。</summary>
        public const float BladeEchoDamageMult = 0.40f;

        // ==================== 精准涌动（StarFlag.PreciseSurge）====================

        /// <summary>【精准涌动】精准满层时的攻速加成。</summary>
        public const float PreciseSurgeSpeedBonus = 0.25f;

        /// <summary>【精准涌动】判定"满层"所需的精准层数（与 MalachiteMelee 的满层阈值同口径）。</summary>
        public const int PrecisionMaxStacks = 5;
    }
}
