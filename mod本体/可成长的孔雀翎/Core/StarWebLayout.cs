// 本文件为自主实现，无外部参考代码。
//
// ============================================================================
// 星图 3.0 ·「翎羽星网」—— 网络**几何与数据表**（唯一出处）
// ============================================================================
// 设计依据：写法\阶段4_星图3.0_翎羽星网.md（D23~D28）
//
// 结构（数据即文档）：
//   · 5 条臂，角度 A(k) = -90° + 72°k（屏幕坐标 y 向下）
//     k0 攻(正上) / k1 穿(-18°) / k2 速(54°) / k3 暴(126°) / k4 念(198°)
//   · 每臂由内向外链： D1(r.30) → D2(r.50) → S(r.68) → D3(r.84) → N(r1.00)
//     星尘 星尘 星宿 星尘 星核
//   · 两圈环向连线（臂间中分角 A(k)+36°）：
//     内环 R1(r.40, 星尘) 串起相邻臂的 D1；外环 R2(r.76, 星宿) 串起相邻臂的 S
//     —— 这是"跨臂混搭"的通路；另有 R1↔R2 的径向后门，给出一条绕开臂链的捷径
//   · 共 36 节点： 1 翎心 + 20 星尘(1点) + 10 星宿(2点) + 5 星核(5点)
//     全解锁 = 20 + 20 + 25 = 65 点；而全 Boss 首杀 + 事件里程碑约 47 点
//     —— **必然取不全，这才是构筑**
//
// ★ 为什么数据表与本文件要单独存在（2026-09-27 拆分的理由）：
//   星网的**几何**（节点位置、边）必须与**Terraria 逻辑**（击杀门槛/购买/退还）解耦，
//   否则 `工具\VfxPreview` 离线预览台无法链接本文件（它不引用 Terraria 程序集），
//   星网就只能"进游戏才知道长什么样"。这与 SlashTuning / SlashVfx 的既有拆分是同一套原则：
//   **几何与调色板零漂移，游戏端与预览端共用同一份源**。
//   → Terraria 侧的门槛/购买/退还逻辑在 Core\StarNetwork.cs，本文件保持零 Terraria 依赖。
//
// 铁律：零 Terraria 依赖（只用 System + XNA 的 Vector2）。

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    /// <summary>星网几何与节点/边数据表（零 Terraria 依赖，游戏端与离线预览共用）。</summary>
    public static class StarWebLayout
    {
        // ==================== 几何常量 ====================

        /// <summary>
        /// 臂基准角度（度，屏幕坐标 y 向下）。
        /// <para/>★ 2026-09-27：**刻意不等距**（原为 -90+72k 的完美等距）。
        /// 用户指出"太过于对称反而不像真的星图"。现在五条臂的张角为 73/69/72/75/71，
        /// 并配合每节点的确定性抖动，读起来才是"星星恰好长成这样"而非"罗盘刻度"。
        /// </summary>
        private static readonly float[] ArmDeg = { -84f, -11f, 58f, 130f, 205f };

        /// <summary>臂短名（拼 id 用）。</summary>
        private static readonly string[] ArmKey = { "Atk", "Pen", "Spd", "Crt", "Mnd" };

        // 臂内径向位置：**刻意把间距拉匀到 ≈0.20**。
        // 为什么：命中半径 18px，而 R≈152 时 0.20 归一化 ≈ 30px —— 间距必须显著大于命中直径，
        // 否则相邻节点会互相吃点击（初版 D3=0.84 / N=1.00 只差 0.16，实测最近间距 20.4px，会点错）。
        private const float R_D1 = 0.24f;
        private const float R_D2 = 0.42f;
        private const float R_S = 0.61f;
        private const float R_D3 = 0.80f;
        private const float R_N = 1.00f;
        private const float R_RING1 = 0.40f;
        private const float R_RING2 = 0.76f;

        /// <summary>
        /// 每臂的半径偏移 —— 打破"五条臂一模一样"的机械感。
        /// <para/>这是去对称化里**代价最低**的一招：整臂同进同退，不压缩臂内间距。
        /// （对比：给单个节点乱抖半径会直接把两个节点推到命中半径以内。）
        /// </summary>
        private static readonly float[] ArmRadiusBias = { 0.000f, 0.022f, -0.016f, 0.028f, -0.020f };

        /// <summary>根节点 id（初始免费点亮）。</summary>
        public const string RootId = "Root";

        /// <summary>节点总数（UI 计数 / 预算核对用）。</summary>
        public const int TotalNodes = 36;

        /// <summary>全解锁所需总点数。</summary>
        public const int TotalCost = 65;

        // ==================== 静态字段（★ 声明顺序 = 初始化顺序，不可随意调换）====================
        //
        // ⚠ 血泪（2026-09-27，由 工具\VfxPreview 离线预览台当场抓到）：
        //   C# 的静态字段初始化器**按文本顺序**执行，且**不保证**先跑完全部字段再跑方法。
        //   本文件最初把 `Adj = BuildAdjacency()` 写在 `EdgePairs` 声明之前 → BuildAdjacency()
        //   迭代到的 EdgePairs 仍是 null → TypeInitializationException → 进游戏第一次打开星图必崩。
        //   而 mod 编译期完全无感（编译器不管这个），只有**真正跑一次**才暴露。
        //   → 因此这里把 5 个表**严格按依赖顺序**排在一起：Nodes → Index → EdgePairs → Adj → All。

        /// <summary>节点表。</summary>
        private static readonly StarNode[] Nodes = BuildTable();

        /// <summary>id → 节点（依赖 Nodes）。</summary>
        private static readonly Dictionary<string, StarNode> Index = BuildIndex();

        /// <summary>边表（依赖 Nodes 的 id 集合）。</summary>
        private static readonly (string a, string b)[] EdgePairs = BuildEdges();

        /// <summary>id → 邻接 id 列表（依赖 Nodes + EdgePairs）。</summary>
        private static readonly Dictionary<string, List<string>> Adj = BuildAdjacency();

        /// <summary>UI 展示顺序（按臂优先，环节点在后；也是遍历/补发的稳定顺序）。</summary>
        public static readonly string[] All = BuildOrder();

        /// <summary>全部节点（只读）。</summary>
        public static IReadOnlyList<StarNode> Nodes_ => Nodes;

        /// <summary>边表（只读，供渲染层遍历）。</summary>
        public static IReadOnlyList<(string a, string b)> EdgePairs_ => EdgePairs;

        // ---- 去对称化：确定性抖动 ----
        //
        // ★ 2026-09-27 用户："星图方面，不一定要保持规整，可以不对称。太过于对称反而不像真的星图"。
        //   原布局是 A(k) = -90° + 72°k 的完美等距风扇 + 每臂同一组半径 + 环节点正好落在中分角，
        //   读起来是"机械罗盘"而不是星图 —— 真实星座的星是疏密不均、也不在一条直线上的。
        //
        //   做法：结构意图（5 条臂、臂内 D1→D2→S→D3→N、两圈环）保持不变，只把每个节点的
        //   角度与半径按 **id 派生的确定性伪随机** 抖一下。必须确定性：否则每次启动星图都会换个样子。
        //
        //   抖动幅度按节点类型分档：臂上节点收敛（保持"臂"的读感），环节点放开（环本来就不该是正圆）。

        private const float ArmNodeAngleJitterDeg = 4.2f;
        private const float ArmNodeRadiusJitter = 0.050f;
        private const float RingNodeAngleJitterDeg = 6.5f;
        private const float RingNodeRadiusJitter = 0.085f;
        private const float NucleusAngleJitterDeg = 2.6f;
        private const float NucleusRadiusJitter = 0.026f;

        /// <summary>确定性伪随机 [0,1)：以 id 与 salt 为种子（xorshift 收尾）。</summary>
        private static float Jitter01(string id, float salt)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < id.Length; i++) h = h * 31 + id[i];
                h = h * 31 + (int)(salt * 1000f);
                h ^= h << 13; h ^= h >> 17; h ^= h << 5;
                return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
            }
        }

        /// <summary>确定性伪随机 [-1,1)。</summary>
        private static float JitterCentered(string id, float salt) => Jitter01(id, salt) * 2f - 1f;

        /// <summary>把节点角度/半径按类型抖动一次（臂上收敛、环上放开）。</summary>
        private static void Jitter(ref float deg, ref float r, string id, bool isRing, bool isNucleus)
        {
            float jd = isNucleus ? NucleusAngleJitterDeg : (isRing ? RingNodeAngleJitterDeg : ArmNodeAngleJitterDeg);
            float jr = isNucleus ? NucleusRadiusJitter : (isRing ? RingNodeRadiusJitter : ArmNodeRadiusJitter);
            deg += JitterCentered(id, 1.37f) * jd;
            r *= 1f + JitterCentered(id, 2.71f) * jr;
        }

        // ---- 工厂（把 13 个构造参数收敛成三种星各自的语义化签名）----

        private static StarNode Dust(string id, int arm, float deg, float r,
            string zh, string en, string dzh, string den, StarStat stat, float val)
        {
            Jitter(ref deg, ref r, id, arm < 0, false);
            return new StarNode(id, StarKind.Dust, deg, r, 1, zh, en, dzh, den, stat, val, StarFlag.None, StarGate.None, arm);
        }

        private static StarNode Aster(string id, int arm, float deg, float r,
            string zh, string en, string dzh, string den, StarFlag flag, StarGate gate = StarGate.None)
        {
            Jitter(ref deg, ref r, id, arm < 0, false);
            return new StarNode(id, StarKind.Asterism, deg, r, 2, zh, en, dzh, den, StarStat.None, 0f, flag, gate, arm);
        }

        private static StarNode Nucleus(string id, int arm,
            string zh, string en, string dzh, string den, StarFlag flag, StarGate gate)
        {
            // 星核也吃臂偏移：整条臂同进同退，才不会把 D3 往 N 挤（实测挤到 20.4px，会点错节点）。
            // 副作用正好：五个星核到中心的距离各不相同，比"钉在同一个圆上"更像真星图。
            float deg = ArmDeg[arm], r = R_N + ArmRadiusBias[arm];
            Jitter(ref deg, ref r, id, false, true);
            return new StarNode(id, StarKind.Nucleus, deg, r, 5, zh, en, dzh, den, StarStat.None, 0f, flag, gate, arm);
        }

        /// <summary>
        /// 臂 k 与臂 k+1 之间的**真实中分角**（臂角不等距后，"A(k)+36°"已不再等于中分角）。
        /// 环节点以它为基准，再叠加抖动。
        /// </summary>
        private static float RingDeg(int k)
        {
            float a = ArmDeg[k];
            float b = ArmDeg[(k + 1) % 5];
            float d = b - a;
            while (d <= 0f) d += 360f;      // 跨 0° 时取正向弧
            return a + d * 0.5f;
        }

        private static StarNode[] BuildTable()
        {
            var L = new List<StarNode>(TotalNodes);

            // ---- 翎心：网的根，位于正中，免费 ----
            L.Add(new StarNode(RootId, StarKind.Root, 0f, 0f, 0,
                "翎心", "Heart of Plumes",
                "星网的起点。所有翎羽都由此生长 —— 点亮相邻节点即可让它蔓延。",
                "The origin of the web. Every plume grows from here. Light an adjacent star to spread it.",
                StarStat.None, 0f, StarFlag.None, StarGate.None, -1));

            // ================= 臂 0：攻（正上）=================
            int a = 0; float d = ArmDeg[a];
            L.Add(Dust("Atk_D1", a, d, R_D1, "锋锐·壹", "Edge I", "最终伤害 +4%", "+4% final damage", StarStat.Damage, 0.04f));
            L.Add(Dust("Atk_D2", a, d, R_D2, "锋锐·贰", "Edge II", "最终伤害 +4%", "+4% final damage", StarStat.Damage, 0.04f));
            L.Add(Aster("Atk_S", a, d, R_S, "刃之形", "Bladed Form",
                "手持孔雀柳刃时按近战键进入近战形态：地面三连段 / 升龙 / 空战三连 / 陨石下砸 / 突进居合 / 蓄力重斩与满月终结，含目押取消与跳跃取消。",
                "Hold the Willow Blade and press the melee key: three-hit ground chain, rising slash, air chain, dive, dash-iai, charge finishers, with dodge-cancel and jump-cancel.",
                StarFlag.MeleeForm, StarGate.KingSlime));
            L.Add(Dust("Atk_D3", a, d, R_D3, "锋锐·叁", "Edge III", "最终伤害 +6%", "+6% final damage", StarStat.Damage, 0.06f));
            L.Add(Nucleus("Atk_N", a, "刃舞·无想", "Bladeless Dance",
                "近战战技解除「同招不连用」限制（连招不再被打断）；地面段击伤害逐段递增 +12%（段1 ×1.0 / 段2 ×1.12 / 段3 ×1.24）。",
                "Melee specials lose the repeat-lock (combos never break); ground chain steps deal +12% per step (x1.0 / x1.12 / x1.24).",
                StarFlag.NucleusBladelessDance, StarGate.Golem));

            // ================= 臂 1：穿 =================
            a = 1; d = ArmDeg[a];
            L.Add(Dust("Pen_D1", a, d, R_D1, "蚀骨·壹", "Bonebite I", "护甲穿透 +5", "+5 armor penetration", StarStat.ArmorPen, 5f));
            L.Add(Dust("Pen_D2", a, d, R_D2, "蚀骨·贰", "Bonebite II", "护甲穿透 +5", "+5 armor penetration", StarStat.ArmorPen, 5f));
            L.Add(Aster("Pen_S", a, d, R_S, "回锋", "Returning Edge",
                "剑阵生成数量随「并发弹幕」提升；生成改为排队，每 5 帧最多落地 3 道。每有 1 道待生成剑阵，最终伤害 ×1.01（独立乘区）。",
                "Blade arrays scale with your Volley bonus and spawn through a queue of at most 3 per 5 ticks. Each pending array grants an independent x1.01 final damage multiplier.",
                StarFlag.ReturnEdge, StarGate.WallOfFlesh));
            L.Add(Dust("Pen_D3", a, d, R_D3, "蚀骨·叁", "Bonebite III", "护甲穿透 +7", "+7 armor penetration", StarStat.ArmorPen, 7f));
            L.Add(Nucleus("Pen_N", a, "万流归墟", "Returning Abyss",
                "护甲穿透超出敌人防御的部分，按 1:1 转为最终伤害乘区（上限 +30%）。",
                "Armor penetration beyond the target's defense converts 1:1 into a final damage multiplier (cap +30%).",
                StarFlag.NucleusPiercing, StarGate.Cultist));

            // ================= 臂 2：速 =================
            a = 2; d = ArmDeg[a];
            L.Add(Dust("Spd_D1", a, d, R_D1, "疾羽·壹", "Swift Plume I", "攻击速度 +3%", "+3% attack speed", StarStat.AttackSpeed, 0.03f));
            L.Add(Dust("Spd_D2", a, d, R_D2, "疾羽·贰", "Swift Plume II", "攻击速度 +3%", "+3% attack speed", StarStat.AttackSpeed, 0.03f));
            L.Add(Aster("Spd_S", a, d, R_S, "双生并发", "Twin Volley",
                "并发弹幕 +2；且「并发」加成**同时作用于灼翎射线** —— 每枚纹章按并发数射出扇形射线（单枚最多 4 道）。",
                "Concurrent projectiles +2, and the Volley bonus ALSO applies to Radiant Crest beams: each crest fires a fan of beams (up to 4 per crest).",
                StarFlag.TwinVolley));
            L.Add(Dust("Spd_D3", a, d, R_D3, "疾羽·叁", "Swift Plume III", "并发弹幕 +1", "+1 concurrent projectile", StarStat.Volley, 1f));
            L.Add(Nucleus("Spd_N", a, "万剑归宗", "Myriad Blades",
                "每累计 24 发弹幕，触发一轮全向剑阵齐射（同时生成 6 道剑阵）。",
                "Every 24 projectiles fired, unleash a full-circle blade array volley (6 arrays at once).",
                StarFlag.NucleusMyriadBlades, StarGate.MoonLord));

            // ================= 臂 3：暴 =================
            a = 3; d = ArmDeg[a];
            L.Add(Dust("Crt_D1", a, d, R_D1, "明察·壹", "Insight I", "暴击率 +4%", "+4% critical chance", StarStat.Crit, 4f));
            L.Add(Dust("Crt_D2", a, d, R_D2, "明察·贰", "Insight II", "暴击率 +4%", "+4% critical chance", StarStat.Crit, 4f));
            L.Add(Aster("Crt_S", a, d, R_S, "过载暴击", "Overloaded Crit",
                "暴击超过 100% 后，每溢出 1% 提供的终伤增幅由 0.5% 提升至 0.9%。",
                "Crit overflow beyond 100% grants 0.9% final damage per 1% instead of 0.5%.",
                StarFlag.OverloadCrit));
            L.Add(Dust("Crt_D3", a, d, R_D3, "明察·叁", "Insight III", "暴击率 +6%", "+6% critical chance", StarStat.Crit, 6f));
            L.Add(Nucleus("Crt_N", a, "盈满之念", "Overflowing Mind",
                "暴击溢出增幅再 ×2；且溢出值会转化为小范围念爆，对周围敌人造成溅射。",
                "Crit overflow bonus is doubled, and the overflow erupts as a small Mind blast around the target.",
                StarFlag.NucleusOverflow, StarGate.MoonLord));

            // ================= 臂 4：念 =================
            a = 4; d = ArmDeg[a];
            L.Add(Dust("Mnd_D1", a, d, R_D1, "灵犀·壹", "Rapport I", "最终伤害 +3%", "+3% final damage", StarStat.Damage, 0.03f));
            L.Add(Dust("Mnd_D2", a, d, R_D2, "灵犀·贰", "Rapport II", "攻击速度 +3%", "+3% attack speed", StarStat.AttackSpeed, 0.03f));
            L.Add(Aster("Mnd_S", a, d, R_S, "灼翎纹章", "Radiant Crest",
                "身侧悬浮两枚小纹章；每 3 发弹幕，两枚纹章各朝鼠标指针方向发射一道射线。",
                "Two small crests orbit you. Every 3rd projectile fired, each crest fires a beam toward the cursor.",
                StarFlag.RadiantCrest, StarGate.Skeletron));
            L.Add(Dust("Mnd_D3", a, d, R_D3, "灵犀·叁", "Rapport III", "护甲穿透 +4", "+4 armor penetration", StarStat.ArmorPen, 4f));
            L.Add(Nucleus("Mnd_N", a, "碧翎念涌", "Verdant Plumage",
                "按念涌键展开一对跟随你的念羽光翼，持续 5.5 秒：期间念伤害 +30%、并发弹幕 +2；翼下软场内的敌人持续被念蚀减速。",
                "Press the key to unfold a pair of mind-feather wings that follow you for 5.5s: +30% Mind damage, +2 concurrent projectiles, and nearby enemies are slowed by the mind field.",
                StarFlag.NucleusMindDomain, StarGate.MoonLord));

            // ================= 内环 R1（星尘 ×5）—— 跨臂混搭通路 =================
            L.Add(Dust("Rng1_0", -1, RingDeg(0), R_RING1, "星屑·征", "Stardust: March", "暴击率 +3%", "+3% critical chance", StarStat.Crit, 3f));
            L.Add(Dust("Rng1_1", -1, RingDeg(1), R_RING1, "星屑·流", "Stardust: Flow", "最终伤害 +3%", "+3% final damage", StarStat.Damage, 0.03f));
            L.Add(Dust("Rng1_2", -1, RingDeg(2), R_RING1, "星屑·锐", "Stardust: Keen", "并发弹幕 +1", "+1 concurrent projectile", StarStat.Volley, 1f));
            L.Add(Dust("Rng1_3", -1, RingDeg(3), R_RING1, "星屑·舞", "Stardust: Dance", "攻击速度 +3%", "+3% attack speed", StarStat.AttackSpeed, 0.03f));
            L.Add(Dust("Rng1_4", -1, RingDeg(4), R_RING1, "星屑·明", "Stardust: Clarity", "并发弹幕 +1", "+1 concurrent projectile", StarStat.Volley, 1f));

            // ================= 外环 R2（星宿 ×5）—— 跨臂质变 =================
            L.Add(Aster("Rng2_0", -1, RingDeg(0), R_RING2, "连锋", "Chained Edge",
                "近战连段的断链窗口由 45 帧延长至 90 帧 —— 连招更耐久，不容易掉回第一段。",
                "The melee chain break window is extended from 45 to 90 ticks — combos last far longer.",
                StarFlag.ChainEdge));
            L.Add(Aster("Rng2_1", -1, RingDeg(1), R_RING2, "追翎", "Homing Plume",
                "翎羽飞刀获得追踪：出膛后自动转向前方最近的敌人（限转向速率、不会掉头）；命中后追加一次 40% 伤害的回响。",
                "Your plume blades home toward the nearest enemy ahead (rate-limited, never U-turn) and echo once for 40% extra damage on hit.",
                StarFlag.BladeEcho));
            L.Add(Aster("Rng2_2", -1, RingDeg(2), R_RING2, "精准涌动", "Surging Precision",
                "「精准」满层时攻击速度 +25%。",
                "While Precision is at maximum stacks, attack speed +25%.",
                StarFlag.PreciseSurge));
            L.Add(Aster("Rng2_3", -1, RingDeg(3), R_RING2, "流风剑阵", "Wandering Blades",
                "每累计 10 发弹幕，在身周随机方位生成一道剑阵；剑阵就地停留蓄力，蓄满后朝鼠标射出飞刀并消散（不再飞向玩家）。",
                "Every 10 projectiles fired summons a blade array at a random spot around you. Arrays stay put and charge in place, then fire at the cursor and fade.",
                StarFlag.SwordArray, StarGate.Plantera));
            L.Add(Aster("Rng2_4", -1, RingDeg(4), R_RING2, "翠幕", "Jade Ward",
                "每 8 秒凝起一层翠幕；受到伤害时抵消该次伤害，随后重新蓄能。",
                "Gain a jade ward every 8 seconds. It negates one instance of damage, then recharges.",
                StarFlag.JadeWard, StarGate.Golem));

            return L.ToArray();
        }

        // ==================== 边表（唯一出处）====================
        //
        // 星网之所以是"网"而不是"五根链条"，全靠这些边。改动这里等于改构筑自由度，务必同步文档。
        // 注意：字段声明已上移到文件顶部（静态初始化顺序依赖），这里只放构建函数。

        private static (string, string)[] BuildEdges()
        {
            var E = new List<(string, string)>();

            // ① 翎心 → 各臂 D1
            for (int k = 0; k < 5; k++) E.Add((RootId, $"{ArmKey[k]}_D1"));

            // ② 臂内链：D1 → D2 → S → D3 → N
            for (int k = 0; k < 5; k++)
            {
                string p = ArmKey[k];
                E.Add(($"{p}_D1", $"{p}_D2"));
                E.Add(($"{p}_D2", $"{p}_S"));
                E.Add(($"{p}_S", $"{p}_D3"));
                E.Add(($"{p}_D3", $"{p}_N"));
            }

            // ③ 内环 R1(k)：串起臂 k 与臂 k+1 的 D1（k+1 取模 5，成环）
            for (int k = 0; k < 5; k++)
            {
                int n = (k + 1) % 5;
                E.Add(($"{ArmKey[k]}_D1", $"Rng1_{k}"));
                E.Add(($"Rng1_{k}", $"{ArmKey[n]}_D1"));
            }

            // ④ 外环 R2(k)：串起臂 k 与臂 k+1 的 S
            for (int k = 0; k < 5; k++)
            {
                int n = (k + 1) % 5;
                E.Add(($"{ArmKey[k]}_S", $"Rng2_{k}"));
                E.Add(($"Rng2_{k}", $"{ArmKey[n]}_S"));
            }

            // ⑤ 径向后门 R1(k) ↔ R2(k)：给出一条绕开臂链的捷径（更多构筑自由度）
            for (int k = 0; k < 5; k++) E.Add(($"Rng1_{k}", $"Rng2_{k}"));

            return E.ToArray();
        }

        // ==================== 构建辅助 ====================

        private static Dictionary<string, StarNode> BuildIndex()
        {
            var d = new Dictionary<string, StarNode>(Nodes.Length);
            foreach (StarNode n in Nodes) d[n.Id] = n;
            return d;
        }

        private static Dictionary<string, List<string>> BuildAdjacency()
        {
            var d = new Dictionary<string, List<string>>(Nodes.Length);
            foreach (StarNode n in Nodes) d[n.Id] = new List<string>(4);
            foreach ((string a, string b) in EdgePairs)
            {
                if (!d.ContainsKey(a) || !d.ContainsKey(b)) continue;   // 防御：边表写错 id 时静默跳过而不是崩
                d[a].Add(b);
                d[b].Add(a);
            }
            return d;
        }

        private static string[] BuildOrder()
        {
            var L = new List<string>(Nodes.Length) { RootId };
            for (int k = 0; k < 5; k++)
            {
                string p = ArmKey[k];
                L.Add($"{p}_D1"); L.Add($"{p}_D2"); L.Add($"{p}_S"); L.Add($"{p}_D3"); L.Add($"{p}_N");
            }
            for (int k = 0; k < 5; k++) L.Add($"Rng1_{k}");
            for (int k = 0; k < 5; k++) L.Add($"Rng2_{k}");
            return L.ToArray();
        }

        // ==================== 查询 ====================

        public static StarNode Get(string id)
            => id != null && Index.TryGetValue(id, out StarNode n) ? n : null;

        /// <summary>邻接节点 id 列表（只读）。</summary>
        public static IReadOnlyList<string> Neighbors(string id)
            => id != null && Adj.TryGetValue(id, out List<string> l) ? l : Array.Empty<string>();

        public static bool IsLit(HashSet<string> lit, string id)
            => lit != null && lit.Contains(id);

        /// <summary>是否与某个已点亮节点相邻（购买的连通性条件）。</summary>
        public static bool HasLitNeighbor(HashSet<string> lit, string id)
        {
            if (lit == null || id == null) return false;
            foreach (string nb in Neighbors(id)) if (lit.Contains(nb)) return true;
            return false;
        }
    }
}
