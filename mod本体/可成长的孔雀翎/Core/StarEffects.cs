// 本文件为自主实现，无外部参考代码。
//
// ============================================================================
// 星图 3.0 ·「翎羽星网」—— 求值层
// ============================================================================
// 职责：把「已点亮的节点集合」汇总成 <see cref="TalentProfile"/>。
// 这是**唯一**把节点数据翻译成战斗数值的地方；武器/近战/弹幕层不得再自行遍历节点。
//
// 设计依据：写法\阶段4_星图3.0_翎羽星网.md §5
//   · 星尘 → 累加数值（Damage / ArmorPen / AttackSpeed / Crit / Volley）
//   · 星宿 + 星核 → 质变标志位（StarFlag），并对全局规则做改写
//     （例如「过载暴击」改写暴击溢出系数、「盈满之念」再 ×2）

using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    public static class StarEffects
    {
        /// <summary>空档案（未点亮任何节点时的基准）。</summary>
        public static TalentProfile Empty => new TalentProfile
        {
            DamageMult = 1f,
            AttackSpeedMult = 1f,
            CritOverflowRate = TalentCatalog.Effects.CritOverflowBase
        };

        /// <summary>
        /// 汇总已点亮节点。
        /// <para/>复杂度 O(已点亮数)，每次求值都重算 —— 节点最多 36 个，比起"缓存失效"
        /// 带来的时序 bug，直算更省心；调用点也不在逐帧热路径上（伤害结算 / 射击 / UI 悬停）。
        /// </summary>
        public static TalentProfile Build(HashSet<string> lit)
        {
            TalentProfile p = Empty;
            if (lit == null || lit.Count == 0) return p;

            foreach (string id in lit)
            {
                StarNode n = StarNetwork.Get(id);
                if (n == null) continue;

                switch (n.Stat)
                {
                    case StarStat.Damage: p.DamageMult += n.StatValue; break;
                    case StarStat.ArmorPen: p.ArmorPen += (int)n.StatValue; break;
                    case StarStat.AttackSpeed: p.AttackSpeedMult += n.StatValue; break;
                    case StarStat.Crit: p.CritChanceBonus += (int)n.StatValue; break;
                    case StarStat.Volley: p.VolleyBonus += (int)n.StatValue; break;
                }

                p.Flags |= n.Flag;
            }

            // ---- 全局规则改写（顺序有意义：先取基础档，再由星核翻倍）----

            // 「双生并发」：直接 +2 并发（不依赖旧版的"轨道等级"概念）
            if (p.HasFlag(StarFlag.TwinVolley)) p.VolleyBonus += 2;

            // 「过载暴击」把溢出系数从 0.5% 提到 0.9% / 1%
            float rate = p.HasFlag(StarFlag.OverloadCrit)
                ? TalentCatalog.Effects.CritOverflowOverload
                : TalentCatalog.Effects.CritOverflowBase;

            // 「盈满之念」再 ×2
            if (p.HasFlag(StarFlag.NucleusOverflow)) rate *= 2f;

            p.CritOverflowRate = rate;

            // 并发弹幕硬上限：武器层还有一次 clamp（maxShots=8），这里先做一次防御性收敛，
            // 避免将来加节点时把弹幕数顶到离谱的量级（历史教训：并发轨曾是弹幕爆炸源）。
            if (p.VolleyBonus > 7) p.VolleyBonus = 7;

            return p;
        }

        /// <summary>取某玩家的星网档案。</summary>
        public static TalentProfile Of(MalachitePlayer mp)
            => mp == null ? Empty : Build(mp.StarNodes);

        /// <summary>取某玩家的星网档案（Terraria 侧便捷重载）。</summary>
        public static TalentProfile Of(Player player)
        {
            if (player == null || !player.active) return Empty;
            return Of(player.GetModPlayer<MalachitePlayer>());
        }

        /// <summary>是否持有某质变（非 UI 代码门控统一入口，替代旧的 CrestNodes.IsUnlocked）。</summary>
        public static bool Has(MalachitePlayer mp, StarFlag f) => Of(mp).HasFlag(f);

        /// <summary>是否持有某质变（按玩家）。</summary>
        public static bool Has(Player player, StarFlag f) => Of(player).HasFlag(f);

        /// <summary>
        /// 加点摘要（物品 tooltip 用）：只列出"确实有值"的项，避免刷屏。
        /// </summary>
        public static string Summary(TalentProfile p)
        {
            var sb = new StringBuilder();
            sb.Append(MalachiteData.Loc("[c/00E676:· 星网]", "[c/00E676:· Star Web]"));

            int dmg = (int)System.Math.Round((p.DamageMult - 1f) * 100f);
            if (dmg > 0) sb.Append(MalachiteData.Loc($"  终伤 +{dmg}%", $"  Dmg +{dmg}%"));
            if (p.ArmorPen > 0) sb.Append(MalachiteData.Loc($"  穿甲 +{p.ArmorPen}", $"  AP +{p.ArmorPen}"));

            int spd = (int)System.Math.Round((p.AttackSpeedMult - 1f) * 100f);
            if (spd > 0) sb.Append(MalachiteData.Loc($"  攻速 +{spd}%", $"  Spd +{spd}%"));

            if (p.CritChanceBonus > 0) sb.Append(MalachiteData.Loc($"  暴击 +{p.CritChanceBonus}%", $"  Crit +{p.CritChanceBonus}%"));
            if (p.VolleyBonus > 0) sb.Append(MalachiteData.Loc($"  并发 +{p.VolleyBonus}", $"  Volley +{p.VolleyBonus}"));

            int cores = CountFlags(p.Flags & NucleusMask);
            int aster = CountFlags(p.Flags & ~NucleusMask);
            if (aster > 0) sb.Append(MalachiteData.Loc($"  星宿 ×{aster}", $"  Asterisms ×{aster}"));
            if (cores > 0) sb.Append(MalachiteData.Loc($"  星核 ×{cores}", $"  Cores ×{cores}"));

            return sb.ToString();
        }

        /// <summary>星核标志位掩码。</summary>
        public const StarFlag NucleusMask =
            StarFlag.NucleusBladelessDance | StarFlag.NucleusPiercing | StarFlag.NucleusMyriadBlades |
            StarFlag.NucleusOverflow | StarFlag.NucleusMindDomain;

        private static int CountFlags(StarFlag f)
        {
            int c = 0;
            uint v = (uint)f;
            while (v != 0) { c += (int)(v & 1u); v >>= 1; }
            return c;
        }
    }
}
