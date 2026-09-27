// 本文件为自主实现，无外部参考代码。
//
// ============================================================================
// 天赋模型 —— 星图 3.0「翎羽星网」求值结果
// ============================================================================
// 设计依据：写法\阶段4_星图3.0_翎羽星网.md（D23~D28）
//
// 与旧版（阶段 2 五轨）的关系：
//   · 旧版：TalentProfile 由「五轨等级 × 每级固定数值」算出，只有 5 个数值字段，
//     构筑维度极窄，且不存在任何"质变"。
//   · 本版：TalentProfile 由「**已点亮星网节点集**」汇总，除数值外新增
//     <see cref="StarFlag"/> 质变标志位；消费方只做 HasFlag 判定。
//
// 兼容遗留：TrackKind / TalentCatalog 的等级与成本接口**只服务旧档迁移**
//   （把旧五轨等级折算成退还点数），新系统不再使用它们。
//   迁移完成后这些字段即退役，保留仅为"读得懂旧档"。

using System;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    // =====================================================================
    // 遗留：旧五轨（阶段 2）—— 仅供存档迁移读取，禁止在新代码里使用
    // =====================================================================

    /// <summary>【遗留】旧版五轨小节点。仅用于读取 v2 存档并折算退还点数。</summary>
    public enum TrackKind
    {
        Attack = 0,
        ArmorPen = 1,
        AttackSpeed = 2,
        Crit = 3,
        Volley = 4
    }

    /// <summary>
    /// 【遗留】旧五轨元数据。新系统请用 <see cref="StarNetwork"/> + <see cref="StarEffects"/>。
    /// </summary>
    public static class TalentCatalog
    {
        /// <summary>【遗留】旧版轨道数（存档数组长度校验用）。</summary>
        public const int TrackCount = 5;

        /// <summary>数值常量（新系统的唯一出处，星尘节点值写死在 StarNetwork 表里，此处只放全局规则常量）。</summary>
        public static class Effects
        {
            /// <summary>暴击溢出基础增幅：溢出 1% → 终伤 +0.5%。</summary>
            public const float CritOverflowBase = 0.005f;

            /// <summary>「过载暴击」后的溢出增幅：溢出 1% → 终伤 +0.9%。</summary>
            public const float CritOverflowOverload = 0.009f;
        }

        /// <summary>
        /// 【遗留】旧档迁移用：把五轨等级折算为应退还的技能点数。
        /// 旧版每级固定 1 点（2026-09-22 拍板取消递增定价），故退还额 = 等级总和。
        /// </summary>
        public static int LegacyRefund(int[] trackLevel)
        {
            if (trackLevel == null) return 0;
            int sum = 0;
            for (int i = 0; i < trackLevel.Length; i++) sum += Math.Max(0, trackLevel[i]);
            return sum;
        }

        /// <summary>
        /// 【遗留】旧档迁移用：把五轨等级近似映射到新星网的节点（尽量保留玩家原来的构筑意图）。
        /// <para/>映射规则（车轱辘话说明白：旧档玩家不该因为改版而"白点"）：
        /// 攻击→攻臂星尘、穿透→穿臂星尘、攻速→速臂星尘、暴击→暴臂星尘、并发→速臂/环上的并发节点。
        /// 只点亮"沿路径可达"的部分，其余折算成点数退还，由玩家自己重加。
        /// </para>
        /// </summary>
        public static void MapLegacyTracks(int[] trackLevel, System.Collections.Generic.HashSet<string> lit)
        {
            if (trackLevel == null || lit == null) return;

            // 每轨按等级从内向外点亮该臂的星尘链；并发轨点亮并发节点。
            // 注意：只做"预设"，不校验连通性 —— 这些节点都在对应臂的内段，天然连通。
            void LightArm(int trackIdx, string d1, string d2, string d3)
            {
                int lv = trackIdx < trackLevel.Length ? Math.Max(0, trackLevel[trackIdx]) : 0;
                if (lv >= 1) lit.Add(d1);
                if (lv >= 2) lit.Add(d2);
                if (lv >= 3) lit.Add(d3);
            }

            LightArm((int)TrackKind.Attack, "Atk_D1", "Atk_D2", "Atk_D3");
            LightArm((int)TrackKind.ArmorPen, "Pen_D1", "Pen_D2", "Pen_D3");
            LightArm((int)TrackKind.AttackSpeed, "Spd_D1", "Spd_D2", "Spd_D3");
            LightArm((int)TrackKind.Crit, "Crt_D1", "Crt_D2", "Crt_D3");

            int volley = (int)TrackKind.Volley < trackLevel.Length ? Math.Max(0, trackLevel[(int)TrackKind.Volley]) : 0;
            if (volley >= 1) lit.Add("Rng1_2");
            if (volley >= 2) lit.Add("Rng1_4");
        }
    }

    // =====================================================================
    // 星图 3.0 求值结果
    // =====================================================================

    /// <summary>
    /// 求值结果：武器 / 近战 / 弹幕 / 玩家层**只读此档案**，
    /// 禁止再出现"查节点 id 集合"的散落条件（旧版最脏的地方）。
    /// </summary>
    public struct TalentProfile
    {
        /// <summary>总伤害倍率（≥1）。</summary>
        public float DamageMult;

        /// <summary>护甲穿透（平值，叠在阶段表 FlatAP 之上）。</summary>
        public int ArmorPen;

        /// <summary>攻击速度倍率（≥1）。</summary>
        public float AttackSpeedMult;

        /// <summary>暴击率加成（平值 %）。</summary>
        public int CritChanceBonus;

        /// <summary>并发弹幕数（每次普攻额外 +N 道）。</summary>
        public int VolleyBonus;

        /// <summary>暴击溢出增幅：总暴击每超出 100% 的 1%，最终伤害 +此比例。</summary>
        public float CritOverflowRate;

        /// <summary>质变标志位（星宿 / 星核）。</summary>
        public StarFlag Flags;

        /// <summary>是否持有某质变。</summary>
        public bool HasFlag(StarFlag f) => (Flags & f) != 0;

        /// <summary>空档案（无任何加点）。</summary>
        public bool IsEmpty => DamageMult <= 1f && ArmorPen == 0 && AttackSpeedMult <= 1f
            && CritChanceBonus == 0 && VolleyBonus == 0 && Flags == StarFlag.None;

        /// <summary>
        /// 暴击溢出增幅倍率。溢出阈值与「过载暴击 / 盈满之念」的改写统一由
        /// <see cref="CritOverflowRate"/> 承载 —— 本方法不再自己决定系数。
        /// </summary>
        public float OverflowMult(int totalCritPercent)
        {
            int overflow = totalCritPercent - 100;
            if (overflow <= 0) return 1f;
            float rate = CritOverflowRate > 0f ? CritOverflowRate : TalentCatalog.Effects.CritOverflowBase;
            return 1f + overflow * rate;
        }

        /// <summary>「万流归墟」：穿甲超出目标防御的部分转终伤乘区（上限 +30%）。</summary>
        public float PiercingOverflowMult(int totalArmorPen, int targetDefense)
        {
            if (!HasFlag(StarFlag.NucleusPiercing)) return 1f;
            int surplus = totalArmorPen - Math.Max(0, targetDefense);
            if (surplus <= 0) return 1f;
            return 1f + Math.Min(0.30f, surplus * 0.01f);
        }
    }
}
