// 本文件为自主实现，无外部参考代码。
//
// ============================================================================
// 星图 3.0 ·「翎羽星网」—— 节点数据模型
// ============================================================================
// 设计依据：星图 3.0「翎羽星网」定稿（2026-09-27 用户拍板）
//   D23 全面彻底重做（UI + 机制 + 数值）
//   D24 开放式星网混搭：一张连通星网、三类节点、允许跨臂自由构筑
//   D25 动态星空流光连线
//   D26 旧 5 个纹章节点并入星网：击杀只解锁「购买资格」，仍须花点数购买
//   D27 单页星网（取消「力/技」翻页）
//   D28 点数经济 = Boss 首杀 + 事件/里程碑补点
//
// 与旧版（阶段 2 五轨）的根本区别：旧版是"五个孤立星座 × 线性数值"，
// 本版是"**一张由边连接的网** × 数值 + 质变"。节点效果分两类：
//   · 星尘 → StarStat / StatValue（累加型数值）
//   · 星宿 / 星核 → StarFlag（质变标志位，消费方只做 HasFlag 判定）
//
// 存档口径：节点 id 是**稳定字符串键，只增不改**（改名即破坏旧档）。
//
// 铁律：零 Terraria 依赖（只用 XNA 的 Vector2），便于离线工具复用几何数据。

using System;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    /// <summary>节点类型（决定形状/半径/消耗/辉光层级）。</summary>
    public enum StarKind
    {
        /// <summary>翎心：网的根节点，初始免费点亮，十二角芒 + 双环。</summary>
        Root = 0,

        /// <summary>星尘：小属性节点，正菱形，1 点。</summary>
        Dust = 1,

        /// <summary>星宿：质变节点，正八边形，2 点。旧纹章节点全部归入此级。</summary>
        Asterism = 2,

        /// <summary>星核：流派核心，四角星芒（底层旋转 45°），5 点。</summary>
        Nucleus = 3
    }

    /// <summary>星尘的数值效果类型。一个星尘节点只给一种，累加进 <see cref="TalentProfile"/>。</summary>
    public enum StarStat
    {
        None = 0,
        /// <summary>最终伤害倍率（累加到 DamageMult）。</summary>
        Damage,
        /// <summary>护甲穿透平值。</summary>
        ArmorPen,
        /// <summary>攻击速度倍率（累加到 AttackSpeedMult）。</summary>
        AttackSpeed,
        /// <summary>暴击率平值（%）。</summary>
        Crit,
        /// <summary>并发弹幕：每次普攻 +1 道（旧版"并发弹幕轨"的替代，封顶见武器层 clamp）。</summary>
        Volley
    }

    /// <summary>
    /// 质变标志位（星宿 / 星核专用）。
    /// <para/>消费方（武器 / 近战 / 弹幕 / 玩家）只允许 <c>profile.HasFlag(StarFlag.X)</c> 判定，
    /// 不得再出现散落的"查节点 id 集合"逻辑 —— 这是旧版最脏的地方。
    /// </summary>
    [Flags]
    public enum StarFlag
    {
        None = 0,

        // ---- 星宿：沿用旧纹章节点的 5 个效果（D26：击杀只解锁购买资格）----
        /// <summary>刃之形：解锁近战形态（F 键八招式状态机）。</summary>
        MeleeForm = 1 << 0,
        /// <summary>灼翎纹章：2 枚绕身纹章，每 3 发弹幕各射一道射线。</summary>
        RadiantCrest = 1 << 1,
        /// <summary>回锋：剑阵射完不消散，折返再射一次。</summary>
        ReturnEdge = 1 << 2,
        /// <summary>流风剑阵：每 10 发弹幕在四周生成一道穿墙剑阵。</summary>
        SwordArray = 1 << 3,
        /// <summary>翠幕：每 8 秒凝一层护幕，抵消一次伤害。</summary>
        JadeWard = 1 << 4,

        // ---- 星宿：本版新增的 5 个质变 ----
        /// <summary>双生并发：并发弹幕轨每级额外 +1（每 2 级多 1 道）。</summary>
        TwinVolley = 1 << 5,
        /// <summary>过载暴击：暴击溢出增幅 0.5% → 0.9% / 1%。</summary>
        OverloadCrit = 1 << 6,
        /// <summary>连锋：近战地面段击 +1 段（3 → 4 段）。</summary>
        ChainEdge = 1 << 7,
        /// <summary>刃回响：飞刀命中后追加一次 40% 伤害的回响。</summary>
        BladeEcho = 1 << 8,
        /// <summary>精准涌动：精准满层时攻速 +25%。</summary>
        PreciseSurge = 1 << 9,

        // ---- 星核：五个流派核心 ----
        /// <summary>刃舞·无想：解除"同招不连用"锁；段击伤害逐段递增；精准上限 +2。</summary>
        NucleusBladelessDance = 1 << 10,
        /// <summary>万流归墟：穿甲溢出转为终伤乘区（上限 +30%）。</summary>
        NucleusPiercing = 1 << 11,
        /// <summary>万剑归宗：每 24 发弹幕触发一轮全向剑阵齐射。</summary>
        NucleusMyriadBlades = 1 << 12,
        /// <summary>盈满之念：暴击溢出增幅 ×2，且溢出转为念爆范围伤害。</summary>
        NucleusOverflow = 1 << 13,
        /// <summary>碧翎念涌：按念涌键**开/关**一对跟随玩家的念羽光翼（开启后无限持续），开启期间念伤 +30%、并发 +2、翼下敌人减速。</summary>
        NucleusMindDomain = 1 << 14,
    }

    /// <summary>
    /// 购买门槛（击杀类）。达成前即使相邻且点数充足也不可购买 —— 这是 D26 的落点：
    /// 旧版"击杀即免费解锁"改为"击杀解锁购买资格"。
    /// </summary>
    public enum StarGate
    {
        None = 0,
        KingSlime,
        Skeletron,
        WallOfFlesh,
        Plantera,
        Golem,
        Cultist,
        MoonLord
    }

    /// <summary>
    /// 单个星网节点。位置用归一化极坐标描述（<see cref="PolarDeg"/> / <see cref="PolarR"/>），
    /// 由 <see cref="StarNetwork"/> 在加载时统一换算成 <see cref="Pos"/> —— 这样"五臂放射"的
    /// 结构在数据表里一眼可读，不需要手写 36 组坐标。
    /// </summary>
    public sealed class StarNode
    {
        /// <summary>稳定字符串键（存档主键，只增不改）。</summary>
        public string Id;

        public StarKind Kind;

        /// <summary>极坐标：角度（度，屏幕坐标 y 向下，-90 = 正上）。</summary>
        public float PolarDeg;

        /// <summary>极坐标：半径（归一化，1.0 = 渲染半径极值）。</summary>
        public float PolarR;

        /// <summary>归一化画布坐标（-1~1），由极坐标在构造时算出。</summary>
        public Vector2 Pos;

        /// <summary>购买消耗（技能点）。</summary>
        public int Cost;

        // ---- 效果（星尘走 Stat，星宿/星核走 Flag）----
        public StarStat Stat;
        public float StatValue;
        public StarFlag Flag;

        /// <summary>击杀门槛。</summary>
        public StarGate Gate;

        // ---- 文案（中/英双语内联，走 MalachiteData.Loc —— 与既有 StarMap 文案口径一致）----
        public string NameZh, NameEn;
        public string DescZh, DescEn;

        /// <summary>所属臂序号（0~4）；-1 = 非臂节点（翎心 / 环节点）。</summary>
        public int Arm = -1;

        // 注意（2026-09-27）：本类**不提供** Name/Desc 这类"按当前语言取文案"的属性 ——
        // 那需要 MalachiteData.Loc（Terraria 侧），会把本文件拖出"零 Terraria 依赖"的边界，
        // 于是整个星网层就无法脱离 Terraria 被复用。
        // 取文案请用 Terraria 侧的 StarNetwork.NameOf(node) / DescOf(node)，或直接读 NameZh/NameEn。

        public StarNode(string id, StarKind kind, float polarDeg, float polarR, int cost,
            string nameZh, string nameEn, string descZh, string descEn,
            StarStat stat = StarStat.None, float statValue = 0f,
            StarFlag flag = StarFlag.None, StarGate gate = StarGate.None, int arm = -1)
        {
            Id = id;
            Kind = kind;
            PolarDeg = polarDeg;
            PolarR = polarR;
            Cost = cost;
            NameZh = nameZh; NameEn = nameEn;
            DescZh = descZh; DescEn = descEn;
            Stat = stat; StatValue = statValue;
            Flag = flag; Gate = gate; Arm = arm;

            float a = MathHelper.ToRadians(polarDeg);
            Pos = new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * polarR;
        }
    }
}
