// 本文件为自主实现，无外部参考代码。
//
// ============================================================================
// 星图 3.0 ·「翎羽星网」—— **逻辑层**（击杀门槛 / 购买 / 退还 / 连通性）
// ============================================================================
// 设计依据：写法\阶段4_星图3.0_翎羽星网.md（D23~D28）
//
// 职责边界（2026-09-27 拆分后）：
//   · Core\StarWebLayout.cs —— 节点表 / 边表 / 几何 / 纯查询。**零 Terraria 依赖**，
//     供游戏端与任何其它宿主复用（UI / 弹幕 / 离屏渲染都读同一份，不会各自漂移）。
//   · 本文件 —— 需要 Terraria 的那一半：击杀门槛（NPC.downed* / Main.hardMode）、
//     购买/退还/洗点、连通性校验、旧档迁移、存活集合净化。
//
// 购买的唯一规则：新节点必须与某个**已点亮**节点之间存在边（邻接连通）。
// 星宿/星核另有击杀门槛（StarGate）—— 这是 D26：击杀只解锁「购买资格」，仍要花点数。
//
// 数据表与几何一律转发到 StarWebLayout，本文件**不再持有**任何节点/边数据。

using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    /// <summary>购买被拒绝的原因（UI 据此选状态色）。</summary>
    public enum StarBlock
    {
        None = 0,
        /// <summary>已经点亮了。</summary>
        AlreadyLit,
        /// <summary>点数不足。</summary>
        NoPoints,
        /// <summary>与任何已点亮节点都不相邻。</summary>
        NoNeighbor,
        /// <summary>击杀门槛未达成。</summary>
        GateLocked
    }

    public static class StarNetwork
    {
        // ==================== 数据/几何转发（唯一出处仍是 StarWebLayout）====================

        /// <summary>根节点 id（初始免费点亮）。</summary>
        public const string RootId = StarWebLayout.RootId;

        /// <summary>节点总数。</summary>
        public const int TotalNodes = StarWebLayout.TotalNodes;

        /// <summary>全解锁所需总点数。</summary>
        public const int TotalCost = StarWebLayout.TotalCost;

        /// <summary>全部节点（只读）。</summary>
        public static IReadOnlyList<StarNode> Nodes_ => StarWebLayout.Nodes_;

        /// <summary>边表（只读，供渲染层遍历）。</summary>
        public static IReadOnlyList<(string a, string b)> EdgePairs_ => StarWebLayout.EdgePairs_;

        /// <summary>UI 展示顺序。</summary>
        public static string[] All => StarWebLayout.All;

        public static StarNode Get(string id) => StarWebLayout.Get(id);

        public static IReadOnlyList<string> Neighbors(string id) => StarWebLayout.Neighbors(id);

        public static bool IsLit(HashSet<string> lit, string id) => StarWebLayout.IsLit(lit, id);

        public static bool HasLitNeighbor(HashSet<string> lit, string id)
            => StarWebLayout.HasLitNeighbor(lit, id);

        /// <summary>节点归一化坐标（-1~1）。</summary>
        public static Vector2 PosOf(string id)
        {
            StarNode n = StarWebLayout.Get(id);
            return n == null ? Vector2.Zero : n.Pos;
        }

        // ==================== 文案取用（Terraria 侧）====================
        //
        // 为什么文案不放在 StarNode 上：那需要 MalachiteData.Loc（Terraria 侧），
        // 会把 StarNode 拖出"零 Terraria 依赖"，让这一层再也无法被非 Terraria 宿主复用。
        // 因此"纯数据"留在 StarWebLayout/StarNode，"按语言取文案"留在本层。

        /// <summary>节点显示名（按当前语言）。</summary>
        public static string NameOf(StarNode n)
            => n == null ? "?" : MalachiteData.Loc(n.NameZh, n.NameEn);

        /// <summary>节点效果描述（按当前语言）。</summary>
        public static string DescOf(StarNode n)
            => n == null ? "" : MalachiteData.Loc(n.DescZh, n.DescEn);

        // ==================== 击杀门槛 ====================

        /// <summary>门槛是否已达成（downed* 为主，本模组自记 Boss 表为双保险，与 CrestNodes 同口径）。</summary>
        public static bool RequirementMet(StarNode n)
        {
            if (n == null) return false;
            if (n.Gate == StarGate.None) return true;

            var prog = MalachiteProgress.Instance;
            bool self(int npcId) => prog != null && prog.DefeatedBosses.Contains($"Vanilla:{npcId}");

            return n.Gate switch
            {
                StarGate.KingSlime => NPC.downedSlimeKing || self(NPCID.KingSlime),
                StarGate.Skeletron => NPC.downedBoss3 || self(NPCID.SkeletronHead),
                StarGate.WallOfFlesh => Main.hardMode || self(NPCID.WallofFlesh),
                StarGate.Plantera => NPC.downedPlantBoss || self(NPCID.Plantera),
                StarGate.Golem => NPC.downedGolemBoss || self(NPCID.Golem),
                StarGate.Cultist => NPC.downedAncientCultist || self(NPCID.CultistBoss),
                StarGate.MoonLord => NPC.downedMoonlord || self(NPCID.MoonLordCore),
                _ => true
            };
        }

        /// <summary>门槛文案（"需击败 XXX"）。</summary>
        public static string GateHint(StarNode n)
        {
            if (n == null || n.Gate == StarGate.None) return "";
            string zh, en;
            switch (n.Gate)
            {
                case StarGate.KingSlime: zh = "史莱姆王"; en = "King Slime"; break;
                case StarGate.Skeletron: zh = "骷髅王"; en = "Skeletron"; break;
                case StarGate.WallOfFlesh: zh = "血肉墙"; en = "Wall of Flesh"; break;
                case StarGate.Plantera: zh = "世纪之花"; en = "Plantera"; break;
                case StarGate.Golem: zh = "石巨人"; en = "Golem"; break;
                case StarGate.Cultist: zh = "拜月教邪教徒"; en = "Lunatic Cultist"; break;
                case StarGate.MoonLord: zh = "月亮领主"; en = "Moon Lord"; break;
                default: return "";
            }
            return MalachiteData.Loc($"需击败 {zh}", $"Requires {en}");
        }

        // ==================== 购买 / 退还 ====================

        /// <summary>
        /// 判定能否购买。返回 <see cref="StarBlock.None"/> 表示可买。
        /// <paramref name="reason"/> 已按当前语言本地化，可直接显示。
        /// </summary>
        public static StarBlock CanPurchase(HashSet<string> lit, string id, int points, out string reason)
        {
            reason = "";
            StarNode n = Get(id);
            if (n == null) { reason = MalachiteData.Loc("未知节点。", "Unknown node."); return StarBlock.NoNeighbor; }
            if (IsLit(lit, id)) { reason = MalachiteData.Loc("该节点已点亮。", "This star is already lit."); return StarBlock.AlreadyLit; }
            if (!RequirementMet(n))
            {
                reason = GateHint(n);
                return StarBlock.GateLocked;
            }
            if (!HasLitNeighbor(lit, id))
            {
                reason = MalachiteData.Loc("需先点亮一枚相邻的星。", "Light an adjacent star first.");
                return StarBlock.NoNeighbor;
            }
            if (points < n.Cost)
            {
                reason = MalachiteData.Loc($"技能点不足（需 {n.Cost}）。", $"Not enough skill points (need {n.Cost}).");
                return StarBlock.NoPoints;
            }
            return StarBlock.None;
        }

        /// <summary>购买成功返回 true，并扣点、写入集合。</summary>
        public static bool TryPurchase(HashSet<string> lit, string id, ref int points)
        {
            if (lit == null) return false;
            if (CanPurchase(lit, id, points, out _) != StarBlock.None) return false;
            points -= Get(id).Cost;
            lit.Add(id);
            return true;
        }

        /// <summary>
        /// 能否退还单个节点。
        /// <para/>约束：退还后**不能让任何已点亮节点与翎心失去全部通路** —— 否则会留下"孤儿星"，
        /// 玩家看着亮着却永远连不回去（旧版星图没有这个问题，因为它根本没有连通性）。
        /// </summary>
        public static bool CanRefund(HashSet<string> lit, string id)
        {
            if (lit == null || id == null) return false;
            if (id == RootId) return false;           // 翎心不可退
            if (!lit.Contains(id)) return false;

            lit.Remove(id);
            bool ok = AllReachableFromRoot(lit);
            lit.Add(id);                              // 还原（本函数是纯查询）
            return ok;
        }

        /// <summary>退还单点：返还其消耗。</summary>
        public static bool TryRefund(HashSet<string> lit, string id, ref int points)
        {
            if (!CanRefund(lit, id)) return false;
            points += Get(id).Cost;
            lit.Remove(id);
            return true;
        }

        /// <summary>
        /// 一键洗点：清空除翎心外的全部节点，返回应返还的点数。
        /// <para/>调用方负责把返回值加到技能点余额上。
        /// </summary>
        public static int RefundAll(HashSet<string> lit)
        {
            if (lit == null) return 0;
            int refund = 0;
            foreach (string id in new List<string>(lit))
            {
                if (id == RootId) continue;
                StarNode n = Get(id);
                if (n != null) refund += n.Cost;
                lit.Remove(id);
            }
            lit.Add(RootId);   // 翎心必须保留，否则整张网无从点亮
            return refund;
        }

        // ==================== 连通性 ====================

        /// <summary>已点亮集合中，是否全部节点都能从翎心走到（BFS）。</summary>
        public static bool AllReachableFromRoot(HashSet<string> lit)
        {
            if (lit == null || lit.Count == 0) return true;
            if (!lit.Contains(RootId)) return false;

            var seen = new HashSet<string> { RootId };
            var q = new Queue<string>();
            q.Enqueue(RootId);
            while (q.Count > 0)
            {
                string cur = q.Dequeue();
                foreach (string nb in Neighbors(cur))
                {
                    if (!lit.Contains(nb) || !seen.Add(nb)) continue;
                    q.Enqueue(nb);
                }
            }
            return seen.Count == lit.Count;
        }

        /// <summary>
        /// 从翎心到目标节点的最短路径（只走已点亮节点），返回**节点坐标序列**，供加点流光动效使用。
        /// <para/>找不到路径（理论上不该发生，因为购买前提就是有已点亮邻居）时返回单点路径。
        /// </summary>
        public static List<Vector2> PathFromRoot(HashSet<string> lit, string targetId)
        {
            var path = new List<Vector2>();
            StarNode target = Get(targetId);
            if (target == null) return path;

            // 目标节点可能尚未写入集合（购买后立刻取路径）——按"视作已点亮"处理
            bool inSet = lit != null && lit.Contains(targetId);
            if (!inSet) lit = new HashSet<string>(lit ?? new HashSet<string>()) { targetId };

            if (!lit.Contains(RootId)) { path.Add(target.Pos); return path; }

            var prev = new Dictionary<string, string> { [RootId] = null };
            var q = new Queue<string>();
            q.Enqueue(RootId);
            bool found = false;
            while (q.Count > 0 && !found)
            {
                string cur = q.Dequeue();
                foreach (string nb in Neighbors(cur))
                {
                    if (!lit.Contains(nb) || prev.ContainsKey(nb)) continue;
                    prev[nb] = cur;
                    if (nb == targetId) { found = true; break; }
                    q.Enqueue(nb);
                }
            }

            if (!found) { path.Add(target.Pos); return path; }

            var chain = new List<string>();
            for (string cur = targetId; cur != null; cur = prev[cur]) chain.Add(cur);
            chain.Reverse();
            foreach (string id in chain)
            {
                StarNode n = Get(id);
                if (n != null) path.Add(n.Pos);
            }
            return path;
        }

        // ==================== 旧档迁移 ====================

        /// <summary>
        /// 旧「纹章节点」（2026-09-22 体系）→ 新星网节点 id 的平移表。
        /// <para/>D26 口径：旧档玩家**已经击杀解锁**的纹章节点，迁移时直接算作"已点亮"，不扣点
        /// （否则改版会让老玩家白丢 5 个节点 = 10 点）。新增节点时本表不需要动。
        /// </summary>
        private static readonly (string legacy, string starId)[] LegacyCrestMap =
        {
            ("MeleeForm",    "Atk_S"),    // 刃之形
            ("RadiantCrest", "Mnd_S"),    // 灼翎纹章
            ("ReturnEdge",   "Pen_S"),    // 回锋
            ("SwordArray",   "Rng2_3"),   // 流风剑阵
            ("JadeWard",     "Rng2_4"),   // 翠幕
        };

        /// <summary>把旧纹章节点集合平移进新星网集合。</summary>
        /// <returns>实际平移的节点数。</returns>
        public static int MigrateLegacyCrests(IEnumerable<string> legacyIds, HashSet<string> lit)
        {
            if (legacyIds == null || lit == null) return 0;
            var old = new HashSet<string>(legacyIds);
            int moved = 0;
            foreach ((string legacy, string starId) in LegacyCrestMap)
            {
                if (!old.Contains(legacy)) continue;
                if (Get(starId) == null) continue;
                if (lit.Add(starId)) moved++;
            }
            return moved;
        }

        /// <summary>
        /// 门槛已达成、但玩家尚未点亮的星宿/星核名单（击杀 Boss 后的提示文案）。
        /// <para/>D26 的语义落点：击杀只是"开了购买资格"，所以这里提示的是"可以买了"，不是"已获得"。
        /// </summary>
        public static string NewlyAvailableSummary(MalachitePlayer mp, int maxNames)
        {
            if (mp == null || maxNames <= 0) return "";
            var names = new List<string>(maxNames);
            foreach (StarNode n in Nodes_)
            {
                if (n.Kind != StarKind.Asterism && n.Kind != StarKind.Nucleus) continue;
                if (mp.StarNodes.Contains(n.Id)) continue;
                if (!RequirementMet(n)) continue;
                names.Add(NameOf(n));
                if (names.Count >= maxNames) break;
            }
            return string.Join(MalachiteData.Loc("、", ", "), names);
        }

        // ==================== 存活集合维护 ====================

        /// <summary>
        /// 净化已点亮集合：剔除未知 id（旧档残留 / 节点被删），并保证翎心在集合内。
        /// 由存档读取与进世界时调用。
        /// </summary>
        /// <returns>是否发生了修改。</returns>
        public static bool Sanitize(HashSet<string> lit)
        {
            if (lit == null) return false;
            bool changed = false;
            foreach (string id in new List<string>(lit))
            {
                if (Get(id) != null) continue;
                lit.Remove(id);
                changed = true;
            }
            if (lit.Add(RootId)) changed = true;
            return changed;
        }
    }
}
