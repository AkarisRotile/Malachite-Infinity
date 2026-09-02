using System;
using Terraria;

namespace 可成长的孔雀翎
{
    // =====================================================================
    // 阶段 2 新天赋模型（v1）—— 纯加点化：五轨小节点 + 技能点经济。
    // v2 切换（M3）后：旧 20 节点星图整体退役，本文件为唯一效果出处。
    // 设计依据：写法\阶段2_天赋星图重设计.md（v1 决策 D6~D12，用户 2026-09-02 拍板）
    // =====================================================================

    /// <summary>
    /// 潜伏标记接口（拆除旧全局弹幕接管类后的落点）：
    /// 由本模组自己的弹幕类（MalachiteProj/Bolt/Domain）实现，
    /// StealthSystem 经此接口读写潜伏状态，不再需要全局接管类。
    /// </summary>
    public interface IStealthStrikeProjectile
    {
        /// <summary>该弹幕实例是否为潜伏攻击（金羽状态）。</summary>
        bool IsStealthStrike { get; set; }
    }

    /// <summary>五轨小节点。</summary>
    public enum TrackKind
    {
        Attack = 0,       // 攻击：最终伤害 +%
        ArmorPen = 1,     // 穿透：护甲穿透 +
        AttackSpeed = 2,  // 攻速：攻击速度 +%
        Crit = 3,         // 暴击：暴击率 +%（>100% 溢出按比例增幅终伤）
        Volley = 4        // 并发弹幕：每次使用 +1 道（单价更高）
    }

    /// <summary>求值结果：武器/弹幕只读此档案，禁止再出现散落效果判断。</summary>
    public struct TalentProfile
    {
        public float DamageMult;       // 总伤害倍率（≥1）
        public int ArmorPen;           // 护甲穿透（平值）
        public float AttackSpeedMult;  // 攻速倍率（≥1）
        public int CritChanceBonus;    // 暴击率加成（平值 %）
        public int VolleyBonus;        // 并发弹幕数（每次使用 +N 道）

        public bool IsEmpty => DamageMult <= 1f && ArmorPen == 0 && AttackSpeedMult <= 1f
            && CritChanceBonus == 0 && VolleyBonus == 0;

        /// <summary>总暴击溢出增幅：暴击 > 100% 时，每溢出 1% 提升 finalMult（常量见 TalentCatalog）。</summary>
        public static float OverflowDamageMultiplier(int totalCritPercent)
        {
            int overflow = totalCritPercent - 100;
            return overflow > 0 ? 1f + overflow * TalentCatalog.Effects.CritOverflowFinalMult : 1f;
        }
    }

    /// <summary>
    /// 五轨元数据与默认数值（v1 草案，数值对齐时可集中调整）。
    /// 注意：本文件是效果曲线的唯一出处（游戏规则书），武器层不得另造公式。
    /// </summary>
    public static class TalentCatalog
    {
        public const int TrackCount = 5;

        /// <summary>v2 切换开关（M3 已置 true）：旧 20 节点星图模型已整体退役，v2 迁移与加点 UI 生效。</summary>
        // 用 readonly 而非 const：若将来回退可避免 CS0162 死代码噪音。
        public static readonly bool V2Active = true;

        /// <summary>每级默认效果（由 Formula 计算）。</summary>
        public static class Effects
        {
            public const float DamagePerLevel = 0.04f;      // 攻击：+4%/级
            public const int ArmorPenPerLevel = 3;          // 穿透：+3/级
            public const float SpeedPerLevel = 0.025f;      // 攻速：+2.5%/级
            public const int CritPerLevel = 3;              // 暴击：+3%/级
            public const float CritOverflowFinalMult = 0.005f; // 暴击溢出：1% → 终伤 +0.5%
        }

        /// <summary>等级上限（随阶段成长的推荐表；cap = 3 + stage，最高 13 档时=16，用 Min 收敛防越界）。</summary>
        public static int LevelCap(TrackKind kind, int stage)
        {
            int cap = Math.Max(0, 3 + stage);
            if (kind == TrackKind.Volley)
                cap = Math.Min(cap, 5); // 并发轨上限更低（防弹幕数爆炸）
            return Math.Clamp(cap, 0, 30);
        }

        /// <summary>升到 nextLevel（即当前级为 nextLevel-1 → nextLevel）所需技能点。</summary>
        public static int CostToNext(TrackKind kind, int nextLevel)
        {
            // 常规轨：1 + 当前级；并发轨单价更高：2×(1 + 当前级)
            int prev = Math.Max(0, nextLevel - 1);
            int cost = 1 + prev;
            if (kind == TrackKind.Volley)
                cost *= 2;
            return cost;
        }

        /// <summary>单级效果是否允许购买（阶段门槛等后续规则在此收敛）。</summary>
        public static bool CanPurchase(TrackKind kind, int nextLevel, int stage)
            => nextLevel >= 1 && nextLevel <= LevelCap(kind, stage);

        /// <summary>轨道显示名（中/英）。</summary>
        public static string DisplayName(TrackKind kind, bool english = false)
            => kind switch
            {
                TrackKind.Attack => english ? "Attack" : "攻击",
                TrackKind.ArmorPen => english ? "Armor Penetration" : "穿透",
                TrackKind.AttackSpeed => english ? "Attack Speed" : "攻速",
                TrackKind.Crit => english ? "Critical Strike" : "暴击",
                TrackKind.Volley => english ? "Concurrent Projectiles" : "并发弹幕",
                _ => "?"
            };

        /// <summary>单级效果描述（供 UI 明细）。</summary>
        public static string EffectDescription(TrackKind kind, bool english = false)
            => kind switch
            {
                TrackKind.Attack => english ? $"+{Effects.DamagePerLevel * 100:0.#}% final damage / lv" : $"最终伤害 +{Effects.DamagePerLevel * 100:0.#}% / 级",
                TrackKind.ArmorPen => english ? $"+{Effects.ArmorPenPerLevel} armor penetration / lv" : $"护甲穿透 +{Effects.ArmorPenPerLevel} / 级",
                TrackKind.AttackSpeed => english ? $"+{Effects.SpeedPerLevel * 100:0.#}% attack speed / lv" : $"攻击速度 +{Effects.SpeedPerLevel * 100:0.#}% / 级",
                TrackKind.Crit => english ? $"+{Effects.CritPerLevel}% crit chance / lv; overflow >100% boosts final damage" : $"暴击率 +{Effects.CritPerLevel}% / 级；>100% 后溢出按比例增幅终伤",
                TrackKind.Volley => english ? $"+1 projectile per shot / lv (higher point cost)" : $"每次攻击 +1 道弹幕 / 级（消耗更高）",
                _ => ""
            };
    }

    /// <summary>把玩家当前加点汇总为 TalentProfile（武器 ModItem 与命中层调用）。</summary>
    public static class TalentEvaluator
    {
        public static TalentProfile Build(MalachitePlayer mp)
        {
            int stage = ProgressSystem.GetStage();
            TalentProfile p = new TalentProfile
            {
                DamageMult = 1f + TalentCatalog.Effects.DamagePerLevel * mp.TrackLevel[(int)TrackKind.Attack],
                ArmorPen = TalentCatalog.Effects.ArmorPenPerLevel * mp.TrackLevel[(int)TrackKind.ArmorPen],
                AttackSpeedMult = 1f + TalentCatalog.Effects.SpeedPerLevel * mp.TrackLevel[(int)TrackKind.AttackSpeed],
                CritChanceBonus = TalentCatalog.Effects.CritPerLevel * mp.TrackLevel[(int)TrackKind.Crit],
                VolleyBonus = mp.TrackLevel[(int)TrackKind.Volley],
            };
            _ = stage; // 阶段门槛规则在 CanPurchase / 数值表波次统一接入
            return p;
        }
    }
}
