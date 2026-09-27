// 代码来源与合规署名：
// - 手感与招式结构参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES
//   （三段段击 / 目押取消 Gatling / 跳跃取消 JC / 空中滞空 Air-Stall / 空间纹章 Crest Arts）。
// - 挥动曲线思路参考（学习后自主实现，未复制源码）：CalamityOverhaul（MIT，(c) hocha113）OniSlashRenderer.BurstCurve。
// 本文件为自主实现。
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 近战形态·ES 全招式状态机（v6.4）：八招输入判定树 / 爆发帧物理 / 取消管线 / 空间纹章参数契约矩阵。
    /// - 输入：MalachitePlayer（D17：状态 + 近战键 + 方向修饰，宽容判定；优先级=蓄力松手 &gt; 方向修饰 &gt; 段击）。
    /// - 物理：MeleeSlashProj 爆发帧(since==0)施加（§三.1 单一出处）。
    /// - 纹章：EsCrestSigilProj.SetupCrest 为配置入口；矩阵唯一出处 CrestSpecOf（§四）。
    /// - 调参入口全部集中于此（阶段2c §五）。
    /// </summary>
    public static class MalachiteMelee
    {
        // ==================== 总开关与输入节奏 ====================
        /// <summary>
        /// 近战形态**调试总开关**（不再是玩法门控）。
        /// 2026-09-22 起玩法门控由「刃之形」节点（击败史莱姆王）负责，见 <see cref="IsPrototypeActive"/>；
        /// 本开关保留用于一键整体关闭近战（排查"是不是近战引起的"这类问题）。
        /// </summary>
        public static readonly bool PrototypeOn = true;

        /// <summary>连段断链窗口（帧）：超过则重置回第 1 段（§一.1：45 帧内无下一次输入归零）。</summary>
        public const int ComboBreakWindow = 45;

        // 注：曾有一个"连锋 → 断链窗口 45→90 帧"的版本，2026-09-27 用户判定为鸡肋并作废
        //（"45 帧已经够久了，延长到 90 帧很鸡肋；近战也不是孔雀柳刃的主要攻击模式"）。
        // 连锋现已改为"连续命中同一目标逐层增伤"，见 CrestNodes.LinkDamagePerStack。
        /// <summary>连段段数（Step1/Step2/Step3）。</summary>
        public const int ComboMaxSteps = 3;
        /// <summary>按住近战键时的出刀间隔（帧；= 蓄势+挥扫+定格，越大越"一顿一顿"）。</summary>
        public const int SwingInterval = 15;

        // ---- 精准被动（连击能量：命中叠层，满层解锁满月终结）----
        public const int PrecisionMaxStacks = 5;
        public const float PrecisionDamagePerStack = 0.10f; // 每层 +10%
        public const int PrecisionDuration = 180;           // 层数持续时间（帧）

        // ---- 突进（双击方向）----
        /// <summary>双击判定的最大间隔（帧）。</summary>
        public const int DoubleTapWindow = 14;
        public const float DashSpeed = 17f;
        public const int DashImmuneTime = 26;
        public const int DashCooldown = 24;

        // ---- 挥动曲线 v4（纯特效弧光；顿挫 = "爆发过冲→回坐"曲线）----
        /// <summary>拉背/蓄势帧数（刃未出、不判定伤害，制造"啪"前的蓄力感）。</summary>
        public const int SwingGatherFrames = 3;
        /// <summary>挥扫帧数（角度在此段内推进，爆发曲线驱动）。</summary>
        public const int SwingSweepFrames = 8;
        /// <summary>满形定格帧数（挥到终点后的短暂停顿）。</summary>
        public const int SwingHoldFrames = 4;
        /// <summary>爆发过冲倍率（>1：先冲过目标角再回坐，替代"减速拖尾"）。</summary>
        public const float SwingOvershoot = 1.05f;
        /// <summary>爆发曲线冲顶位置（行程占比，约 0.62 处过冲峰值）。</summary>
        public const float SwingBurstEnd = 0.62f;
        /// <summary>加速度曲线指数 K（>1=先慢后快，越蓄越狠；=1 即原平滑步。调试区间 1.4~2.2，>2.5 前段显卡顿）。</summary>
        public const float SwingAccelPower = 1.6f;
        /// <summary>刃长随挥动的脉动幅度（半径 ×(1+该值)，过冲处最大）。</summary>
        public const float SwingReachPulse = 0.08f;
        // 弧度语义：0 = 朝前水平；负值 = 向上（屏幕 Y 向下，sin<0 即上）。
        // ES 三连段（v6.3）：段1 左上→右下重劈 ~150°(2.6rad)；段2 右下→左上挑击 ~140°(2.44rad)；段3 大跨步回旋 ~200°(3.5rad)。
        private static readonly float[] StepStartRadL = { -1.10f, 1.30f, 2.90f };
        private static readonly float[] StepEndRadL = { 1.50f, -1.14f, -0.60f };
        /// <summary>每段挥动半径（px，即攻击范围，越后段越大）。</summary>
        private static readonly float[] StepReachArr = { 200f, 225f, 256f };
        /// <summary>每段刃身大小倍率。</summary>
        private static readonly float[] StepScaleArr = { 1.0f, 1.12f, 1.30f };

        public static float StepStartRad(int step, int dir) { float r = StepStartRadL[Math.Clamp(step, 0, 2)]; return dir >= 0 ? r : MathHelper.Pi - r; }
        public static float StepEndRad(int step, int dir) { float r = StepEndRadL[Math.Clamp(step, 0, 2)]; return dir >= 0 ? r : MathHelper.Pi - r; }
        public static float StepReach(int step) => StepReachArr[Math.Clamp(step, 0, 2)];
        public static float StepScale(int step) => StepScaleArr[Math.Clamp(step, 0, 2)];
        /// <summary>每段刃身贴图 X 向拉伸倍率（沿刃长：段位越大越"甩长"）。</summary>
        private static readonly float[] StepStretchXArr = { 1.05f, 1.12f, 1.22f };
        /// <summary>每段刃身贴图 Y 向压扁倍率（厚度：段位越大越"薄利"）。</summary>
        private static readonly float[] StepSquashYArr = { 0.88f, 0.82f, 0.72f };
        public static float StepStretchX(int step) => StepStretchXArr[Math.Clamp(step, 0, 2)];
        public static float StepSquashY(int step) => StepSquashYArr[Math.Clamp(step, 0, 2)];

        // ---- 挥动特效贴图（用户自绘 MeleeSlash.png，已裁透明边；挥动圆心=贴图最左像素）----
        /// <summary>裁剪后贴图宽度（px）：画到挥动半径的比例基准（reach / SlashArtWidth）。</summary>
        public const float SlashArtWidth = 140f;
        /// <summary>裁剪后贴图锚点 Y（最左像素列的垂直质心；X=0 即最左像素 = 挥动圆心）。</summary>
        public const float SlashArtPivotY = 12.5f;
        /// <summary>刃体贴图本体绘制透明度（正常光照层）。</summary>
        public const float SlashArtAlpha = 1.0f;
        /// <summary>刃体贴图整体亮度倍率（调暗本体，靠周围光晕托出"刀光"而非实贴图）。</summary>
        public const float SlashArtBrightness = 0.62f;
        /// <summary>刃体贴图加色辉光强度（叠加一层柔和发光）。</summary>
        public const float SlashArtGlowAlpha = 0.30f;

        // ---- 曳光/拖尾（适配贴图：沿刃尖轨迹的加色细带 + 贴图残影）----
        /// <summary>曳光整体透明度（0~1）。</summary>
        public const float SlashVisualAlpha = 0.55f;
        /// <summary>外缘曳光带宽度比例（相对挥动半径，细亮线）。</summary>
        public const float SlashEdgeWidth = 0.07f;
        /// <summary>内侧软融宽度比例（比外缘宽、更淡）。</summary>
        public const float SlashBandWidth = 0.20f;
        /// <summary>轨迹采样上限（帧，= 拖尾残影/曳光带长度）。</summary>
        public const int SlashTrailMax = 12;

        // ---- 挥动路径特效（双层扇形弧光带——彩色主带 + 嵌套白热芯带）----
        /// <summary>路径扇内半径比例（相对刃尖外半径，弧光带内缘）。</summary>
        public const float PathInnerK = 0.45f;
        /// <summary>主带透明度。</summary>
        public const float PathGlowAlpha = 0.72f;
        /// <summary>白热芯带内半径比例（嵌套在彩色带内侧更窄更亮）。</summary>
        public const float PathWhiteInnerK = 0.70f;
        /// <summary>白热芯带透明度。</summary>
        public const float PathWhiteAlpha = 0.60f;
        /// <summary>路径外缘锐亮线宽度比例（相对刃尖外半径）。</summary>
        public const float PathEdgeWidth = 0.05f;

        // ---- 击中反馈 ----
        /// <summary>卡肉抑制窗口（帧）：同一窗口内的后续挥击不再触发卡肉。</summary>
        public const int HitstopGapFrames = 10;
        /// <summary>命中反馈持续帧数。</summary>
        public const int HitFlashFrames = 7;
        /// <summary>命中扩散环最终半径（px）。</summary>
        public const float HitRingMaxR = 46f;
        /// <summary>命中砍痕闪刃长度（px，沿挥动切线）。</summary>
        public const float HitSlashLen = 60f;

        // ---- 命中判定盒（覆盖挥动半径的大盒，随挥动扫过）----
        /// <summary>判定盒中心所在的半径比例（相对 StepReach；0.5 = 覆盖内~外缘）。</summary>
        public const float SwingHitCenterK = 0.5f;
        /// <summary>判定盒宽度比例（相对 StepReach，约 1.02 = 几乎覆盖全刃长）。</summary>
        public const float SwingHitSpanK = 1.02f;
        /// <summary>判定盒高度（px，垂直厚度）。</summary>
        public const int SwingHitHeight = 150;

        /// <summary>精准层数的总伤害倍率（1 + 层数×单层）。</summary>
        public static float PrecisionDamageMult(int stacks) => 1f + Math.Min(PrecisionMaxStacks, Math.Max(0, stacks)) * PrecisionDamagePerStack;

        /// <summary>
        /// 近战形态是否在玩家身上生效：持有孔雀柳刃 **且星网已点亮「刃之形」**（需击败史莱姆王 + 2 点）。
        /// <para/>2026-09-22：近战原由 <see cref="PrototypeOn"/> 硬编码常开（原型期），改由节点门控；
        /// <c>PrototypeOn</c> 退化为**调试总开关**（置 false 可整体强制关掉近战，便于排查）。
        /// <para/>2026-09-27（星图 3.0）：门控从"击杀即解锁"改为"击杀解锁购买资格 + 花点购买"，
        /// 判定统一走 <see cref="StarEffects.Has"/>。
        /// </summary>
        public static bool IsPrototypeActive(Player player)
            => PrototypeOn
               && player != null
               && MalachiteCache.IsMalachiteItem(player.HeldItem)
               && StarEffects.Has(player, StarFlag.MeleeForm);

        /// <summary>是否为地面普攻链段（Step1/2/3）。</summary>
        public static bool IsStepKind(MoveKind kind) => kind == MoveKind.Step1 || kind == MoveKind.Step2 || kind == MoveKind.Step3;

        /// <summary>是否为空战三连段（AirStep1/2/3，j.A->j.B->j.C）。</summary>
        public static bool IsAirStepKind(MoveKind kind) => kind == MoveKind.AirStep1 || kind == MoveKind.AirStep2 || kind == MoveKind.AirStep3;

        /// <summary>目押取消（Gatling）作用域：地面链 + 空战链。</summary>
        public static bool IsChainKind(MoveKind kind) => IsStepKind(kind) || IsAirStepKind(kind);

        /// <summary>地面链段索引（Step1→0 / Step2→1 / Step3→2）。</summary>
        public static int GroundIndexOf(MoveKind kind) => kind == MoveKind.Step1 ? 0 : kind == MoveKind.Step2 ? 1 : 2;

        // ==================== ES 全招式枚举契约（§一/§二）====================
        /// <summary>近战招式种类（弹幕 Kind）。</summary>
        public enum MoveKind
        {
            // 地面普攻链
            Step1 = 0,      // 5A/5B：地面轻劈（轻微踏步，近身微型纹章）
            Step2 = 1,      // 2C/5C：地面挑空斩（上步撩斩，高浮空，支持跳跃取消 JC）
            Step3 = 2,      // 6C：疾步大回旋重斩（大跨步突进，强吹飞，巨型横向纹章）

            // 特殊指令技
            UpperRise = 3,  // Griflet/格里弗莱特（上方向+攻击）：对空腾跃升龙（带人升空，双连珠纹章）

            // 空战三连击（j.A -> j.B -> j.C，空中按近战键按 airComboStep 流转）
            AirStep1 = 4,   // j.A：空中轻挑斩（斜上 45° 快速撩刀，出刀极快 10 帧，锁重力）
            AirStep2 = 5,   // j.B：空中回旋斩（水平 180° 广角大回旋，锁重力，半空留置纹章）
            AirStep3 = 6,   // j.C：空中下劈翻腾砸（自上而下猛砸，带下压惯性，命中强制击坠）

            AirDive = 7,    // Bors/鲍斯（空中下方向+攻击）：陨石垂直急坠下砸（触地全屏爆震纹章）
            DashCut = 8,    // Mordred/莫德雷德（冲刺中攻击）：穿透突进居合（穿透无敌，多段切线纹章）
            Finisher1 = 9,  // Bedivere 一段：斜上前撩飞升（顶点顿挫定格 + 初阶小单翼，自动派生二段）
            Finisher2 = 10, // Bedivere 二段：斜下俯冲贯穿（终点极强卡肉 + 终阶巨单翼 + 全屏空间引爆）
            Charged = 11,   // v5.2 批1 保留：蓄力重斩（按住≥ChargeFrames 松手；非满层精准）

            // 阶段3 搓招扩展（D31~D33，2026-09-05 拍板）
            BackflipRetreat = 12, // 雀返（后摇 S+F）：后空翻脱战残影技（位移/无敌在玩家层，弹幕纯视觉）
            AirNeedle = 13,       // 翠羽点穴（空战三连后摇 S+F）：45° 斜下突刺 + 命中借力反冲
            LandingSweep = 14,    // 拂柳穿心（下砸落地 6 帧内 F）：贴地滑铲横扫，接入段3 断链
            WarpStep = 15,        // 雀跃·凌虚（D34 方案 B，地面后摇双击 W）：抛物线瞬步 + 短无敌 + 羽毛光尘
            AerialVortex = 16,     // 翠华流风（D35，升龙命中后 W+F 按住）：滞空回旋绞杀，结束自动派生 j.A
        }

        // ==================== 动作物理干涉契约（§三.1；爆发帧 since==0 施加，唯一出处 MeleeSlashProj）====================
        /// <summary>地面三连段踏步推力（px/帧）：Step1 试探步 / Step2 上步蓄力 / Step3 全身跨步重斩。</summary>
        public static readonly float[] StepPushX = { 1.5f, 2.2f, 5.0f };
        /// <summary>升龙对空斩垂直升力（vel.Y 覆写）。</summary>
        public const float UpperRiseJump = -9.2f;
        /// <summary>升龙对空斩前冲分量。</summary>
        public const float UpperRisePush = 2.0f;
        /// <summary>空中悬停滞空帧数（Air-Stall：前 N 帧锁重力 + 抵消下坠动量）。</summary>
        public const int AirStallFrames = 8;
        /// <summary>雀返后空翻水平速度（向后）。</summary>
        public const float BackflipVX = 12f;
        /// <summary>雀返后空翻垂直速度（向上）。</summary>
        public const float BackflipVY = -9.5f;
        /// <summary>雀返无敌帧（前 6 帧）。</summary>
        public const int BackflipImmuneFrames = 6;
        /// <summary>翠羽点穴俯冲速度（斜下：水平 11 / 垂直 8）。</summary>
        public const float AirNeedleVX = 11f;
        public const float AirNeedleVY = 8f;
        /// <summary>翠羽点穴命中借力反冲（向上速度）。</summary>
        public const float AirNeedleBounceY = -7f;
        /// <summary>拂柳穿心贴地滑铲水平速度。</summary>
        public const float LandingSweepVX = 14f;
        /// <summary>雀跃·凌虚瞬步速度（水平/垂直）。</summary>
        public const float WarpStepVX = 0f;   // 正上方位移（2026-09-05 用户指令：不再横向）
        public const float WarpStepVY = -12f;
        /// <summary>雀跃·凌虚无敌帧。</summary>
        public const int WarpStepImmuneFrames = 4;
        /// <summary>翠华流风持续帧数。</summary>
        public const int AerialVortexFrames = 22;
        /// <summary>翠华流风吸附半径（px）。</summary>
        public const float AerialVortexRadius = 120f;
        /// <summary>翠华流风向心拉扯强度（px/帧，乘击退抗性）。</summary>
        public const float AerialVortexPull = 0.12f;
        /// <summary>空战 j.A/j.B 滞空：下坠动量保留系数（0.15 = 强滞空）。</summary>
        public const float AirStallDamp = 0.15f;
        /// <summary>j.C 空中下劈：爆发帧给玩家的下压速度。</summary>
        public const float AirStep3DownPush = 3.5f;
        /// <summary>j.C 命中强制击坠：覆写目标垂直速度（Hard Knockdown）。</summary>
        public const float AirStep3KnockdownY = 12f;
        /// <summary>空中陨石下砸：强制垂直速度（重力贯穿；2026-09-03 实机反馈提速 16→24）。</summary>
        public const float AirDiveSpeedY = 34f;
        /// <summary>空中陨石下砸：水平动量保留系数。</summary>
        public const float AirDiveDampX = 0.2f;
        /// <summary>突进居合切：极速突进速度。</summary>
        public const float DashCutSpeed = 15f;
        /// <summary>突进居合切：穿透无敌帧。</summary>
        public const int DashCutImmuneFrames = 12;
        /// <summary>段2 挑空：命中覆写目标垂直速度（§一.1）。</summary>
        public const float Step2LaunchY = -7.5f;
        /// <summary>段2 挑空：目标水平动量保留系数。</summary>
        public const float Step2LaunchDampX = 0.2f;
        /// <summary>段3 大吹飞：命中覆写目标水平速度（斩击；纹章为 12f 见矩阵）。</summary>
        public const float Step3BlowX = 14f;
        /// <summary>跳跃取消（JC）：打断后摇同步升空速度（§三.2）。</summary>
        public const float JCVelocityY = -9.2f;

        // ==================== 两段式大招（Two-Stage Finisher）物理契约 ====================
        /// <summary>Finisher1 一段飞升：斜前上撩剑的爆发帧速度（X 前冲 / Y 升空；2026-09-03 实机指令：位移距离 ×10）。</summary>
        public const float Finisher1SpeedX = 95f;
        public const float Finisher1SpeedY = -125f;
        /// <summary>一段突进无敌帧（覆盖飞升 + 顶点冻结）。</summary>
        public const int Finisher1ImmuneFrames = 22;
        /// <summary>一段顶点顿挫：清速冻结滞空帧数（锁重力由 AirStallFrames 机制承担）。</summary>
        public const int ApexFreezeFrames = 8;
        /// <summary>Finisher2 二段俯冲：斜下流星贯地速度（×10）与穿透无敌帧（覆盖俯冲 + 终点顿挫）。</summary>
        public const float Finisher2SpeedX = 160f;
        public const float Finisher2SpeedY = 135f;
        public const int Finisher2ImmuneFrames = 20;
        /// <summary>二段终点极强卡肉定格帧数（+8 级震屏，终极顿挫）。</summary>
        public const int Finisher2HitstopFrames = 10;

        /// <summary>进入蓄力态所需按住帧数（16 ≈ 0.27s @60fps）。</summary>
        public const int ChargeFrames = 16;
        /// <summary>满月终结所需精准层数（满层 5）。</summary>
        public const int FinisherMinStacks = 5;
        /// <summary>蓄力/终结招后的间隔帧（硬直节奏）。</summary>
        public const int HeavyGapFrames = 24;

        // 各招弧线（基准朝右 start→end；弹幕内按朝向 FrontMirror 镜像）
        private static readonly float[] UpperRiseArc = { 1.05f, -2.45f };   // 升龙：自下而上大范围仰弧 ~200°
        private static readonly float[] AirStep1Arc = { 0.55f, -0.85f };    // j.A：斜上 45° 快速撩刀（短弧快刀）
        private static readonly float[] AirStep2Arc = { 0.25f, 0.25f - MathHelper.Pi }; // j.B：水平 180° 广角大回旋
        private static readonly float[] AirStep3Arc = { -2.35f, 0.45f };    // j.C：自正上方经前方猛劈至斜下方（≈160°）
        private static readonly float[] Finisher1Arc = { 0.95f, -1.50f };   // 一段：自斜下方划向斜前上方（≈140° 斜撩弧线）
        private static readonly float[] ChargedArc = { 1.15f, -1.15f };     // 蓄力重斩：大横斩

        /// <summary>朝右基准角 → 实际朝向角（1 右原样 / -1 左水平镜像）。</summary>
        public static float FrontMirror(float rad, int dir) => dir >= 0 ? rad : MathHelper.Pi - rad;

        // ==================== 空间纹章参数契约矩阵（§四；配置入口 EsCrestSigilProj.SetupCrest）====================
        /// <summary>单枚纹章的参数契约（外形长宽比/总缩放/引爆延迟/伤害倍率/持续切割/配色）。</summary>
        public struct CrestSpec
        {
            public float TotalScale;   // 整体缩放（所有几何尺寸 × 该值）
            public float AspectX;      // 横向长宽比分量（菱形与十字切痕的 X 轴倍率）
            public float AspectY;      // 纵向长宽比分量（Y 轴倍率；>1 = 纵向撕裂）
            public int ExplosionDelay; // 引爆延迟帧（生成后第 N 帧引爆）
            public float DamageMult;   // 相对主斩的伤害倍率
            public float Knockback;    // 击退
            public int LingerFrames;   // 引爆后持续切割帧数（0 = 单次爆；>0 = 悬浮地雷多段）
            public bool Gold;          // 金纹章（重招配色）
            /// <summary>纹章档位：0 = 小纹章（原版五层几何）；1 = 大纹章（符文双环/同心几何环/聚能吸入光丝/晶化碎片芒）。</summary>
            public int Tier;
        }

        /// <summary>招式 → 纹章参数契约（矩阵唯一出处）。特殊生成布局（双连珠/三切线/触地中心）见 MeleeSlashProj。</summary>
        public static CrestSpec CrestSpecOf(MoveKind kind)
        {
            switch (kind)
            {
                // Tier 分档口径（2026-09-18 用户拍板：现状=小纹章 / 华丽版=大纹章）
                //   大纹章 = 金色重招（Gold=true）∪ 悬浮地雷（LingerFrames>0，长期在场最需要华丽）
                //   改档只动本表这一列，绘制侧无需任何改动。
                case MoveKind.Step1:          // 一段段击 → 小
                    return new CrestSpec { TotalScale = 0.65f, AspectX = 1.0f, AspectY = 1.0f, ExplosionDelay = 10, DamageMult = 0.50f, Knockback = 2f, LingerFrames = 0, Gold = false, Tier = 0 };
                case MoveKind.Step2:          // 二段挑空 → 小
                    return new CrestSpec { TotalScale = 1.00f, AspectX = 0.7f, AspectY = 1.4f, ExplosionDelay = 12, DamageMult = 0.75f, Knockback = 3f, LingerFrames = 0, Gold = false, Tier = 0 };
                case MoveKind.Step3:          // 三段大吹飞 → 大（金）
                    return new CrestSpec { TotalScale = 1.65f, AspectX = 1.6f, AspectY = 0.75f, ExplosionDelay = 14, DamageMult = 1.25f, Knockback = 4f, LingerFrames = 0, Gold = true, Tier = 1 };
                case MoveKind.UpperRise:      // 升龙 → 小
                    return new CrestSpec { TotalScale = 0.75f, AspectX = 0.8f, AspectY = 1.3f, ExplosionDelay = 8, DamageMult = 0.50f, Knockback = 2f, LingerFrames = 0, Gold = false, Tier = 0 };
                case MoveKind.AirStep1:       // 空战 j.A → 小
                    return new CrestSpec { TotalScale = 0.55f, AspectX = 0.9f, AspectY = 1.1f, ExplosionDelay = 8, DamageMult = 0.40f, Knockback = 2f, LingerFrames = 0, Gold = false, Tier = 0 };
                case MoveKind.AirStep2:       // 空战 j.B 悬浮地雷 → 大（绿但常驻 14 帧切割）
                    return new CrestSpec { TotalScale = 1.15f, AspectX = 1.2f, AspectY = 1.0f, ExplosionDelay = 12, DamageMult = 0.80f, Knockback = 2f, LingerFrames = 14, Gold = false, Tier = 1 };
                case MoveKind.AirDive:        // 陨石下砸 → 大（金）
                    return new CrestSpec { TotalScale = 1.90f, AspectX = 2.5f, AspectY = 0.35f, ExplosionDelay = 0, DamageMult = 1.50f, Knockback = 6f, LingerFrames = 0, Gold = true, Tier = 1 };
                case MoveKind.DashCut:        // 突进居合 → 小
                    return new CrestSpec { TotalScale = 0.60f, AspectX = 1.4f, AspectY = 0.6f, ExplosionDelay = 6, DamageMult = 0.40f, Knockback = 3f, LingerFrames = 0, Gold = false, Tier = 0 };
                case MoveKind.Finisher2:      // 满月终结二段 → 大（金，最重）
                    return new CrestSpec { TotalScale = 2.60f, AspectX = 1.5f, AspectY = 1.0f, ExplosionDelay = 10, DamageMult = 2.10f, Knockback = 13f, LingerFrames = 0, Gold = true, Tier = 1 };
                case MoveKind.Charged:        // 蓄力重斩 → 小
                    return new CrestSpec { TotalScale = 1.15f, AspectX = 1.05f, AspectY = 0.9f, ExplosionDelay = 12, DamageMult = 1.00f, Knockback = 4f, LingerFrames = 0, Gold = false, Tier = 0 };
                case MoveKind.AirNeedle:      // 翠羽点穴 → 大（金）
                    return new CrestSpec { TotalScale = 0.60f, AspectX = 1.0f, AspectY = 0.8f, ExplosionDelay = 6, DamageMult = 0.90f, Knockback = 3f, LingerFrames = 0, Gold = true, Tier = 1 };
                case MoveKind.LandingSweep:   // 拂柳穿心滑铲 → 大（金）
                    return new CrestSpec { TotalScale = 1.60f, AspectX = 2.2f, AspectY = 0.3f, ExplosionDelay = 0, DamageMult = 1.10f, Knockback = 5f, LingerFrames = 0, Gold = true, Tier = 1 };
                case MoveKind.AerialVortex:   // 翠华流风绞杀 → 大（金）
                    return new CrestSpec { TotalScale = 1.40f, AspectX = 0.6f, AspectY = 1.6f, ExplosionDelay = 10, DamageMult = 0.60f, Knockback = 3f, LingerFrames = 0, Gold = true, Tier = 1 };
                default:
                    return new CrestSpec { TotalScale = 0.80f, AspectX = 1f, AspectY = 1f, ExplosionDelay = 12, DamageMult = 0.75f, Knockback = 2f, LingerFrames = 0, Gold = false, Tier = 0 };
            }
        }

        // ==================== 招式触发（输入解析在 MalachitePlayer）====================

        /// <summary>
        /// 地面段击的**逐段伤害递增**倍率（星核「刃舞·无想」）。
        /// <para/>未点亮恒为 1；点亮后第 n 段 ×(1 + 0.12n)，即段1 ×1.0 / 段2 ×1.12 / 段3 ×1.24。
        /// </summary>
        public static float StepDamageMult(Player player, int step)
        {
            if (player == null) return 1f;
            if (!StarEffects.Has(player, StarFlag.NucleusBladelessDance)) return 1f;
            return 1f + StarFlag_StepBonus * Math.Max(0, step);
        }

        /// <summary>「刃舞·无想」每段递增幅度。</summary>
        public const float StarFlag_StepBonus = 0.12f;


        /// <summary>地面普攻链：按 comboStep 步进派生 Step1/Step2/Step3（§二 输入判定树地面分支）。</summary>
        public static void TryMeleeStrike(Player player, IEntitySource source, float damage, float knockback)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            int now = (int)Main.GameUpdateCount;
            if (now - mp.MeleeLastStrikeTick > ComboBreakWindow)
                mp.MeleeComboStep = 0;

            int step = mp.MeleeComboStep % ComboMaxSteps;
            mp.MeleeComboStep = (step + 1) % ComboMaxSteps;
            mp.MeleeLastStrikeTick = now;

            int dir = player.direction != 0 ? player.direction : 1;
            float mult = PrecisionDamageMult(mp.PrecisionStacks) * StepDamageMult(player, step);
            int dmg = Math.Max(1, (int)(damage * mult));
            float kb = Math.Max(1f, knockback + step);

            // 踏步推力/挑空/吹飞统一在 MeleeSlashProj 爆发帧(since==0)施加（§三.1 单一出处）
            SpawnSlash(player, source, dmg, kb, dir, step);
            // 出刀音阶随段位爬升：段1/2/3 音调递进，段3 换重音（手感递进）
            SoundEngine.PlaySound(step == 2 ? SoundID.Item71 with { Pitch = 0.05f } : SoundID.Item15 with { Pitch = step * 0.12f }, player.Center);
            if (step == 2 && player.whoAmI == Main.myPlayer)
                ScreenShakeSystem.Shake(8f); // 段3 终结重击震屏
        }

        /// <summary>目押取消 Gatling（§三.2）：取消窗内再次按下近战键 → 抹掉后摇无缝接下一段（地面链与空战链通用）。</summary>
        public static void GatlingContinue(Player owner, MoveKind kind)
        {
            if (owner == null || !owner.active || owner.dead || !IsPrototypeActive(owner)) return;
            var mp = owner.GetModPlayer<MalachitePlayer>();
            int now = (int)Main.GameUpdateCount;
            int dir = owner.direction != 0 ? owner.direction : 1;
            int wd = owner.HeldItem != null ? owner.GetWeaponDamage(owner.HeldItem) : 32;
            int dmg = Math.Max(1, (int)(wd * PrecisionDamageMult(mp.PrecisionStacks)));
            var src = owner.GetSource_ItemUse(owner.HeldItem);
            if (IsAirStepKind(kind))
            {
                // 空战链：j.A→j.B→j.C→j.A 循环
                int idx = kind == MoveKind.AirStep1 ? 0 : kind == MoveKind.AirStep2 ? 1 : 2;
                int next = (idx + 1) % 3;
                mp.AirComboStep = (next + 1) % 3;
                int airDmg = Math.Max(1, (int)(dmg * (next == 2 ? 1.15f : 1f)));
                SpawnAirChain(owner, src, airDmg, 6f, dir, next);
                SoundEngine.PlaySound(next == 2 ? SoundID.Item71 with { Volume = 0.8f, Pitch = -0.15f } : SoundID.Item15 with { Volume = 0.7f, Pitch = next == 0 ? 0.3f : 0.15f }, owner.Center);
            }
            else
            {
                // 地面链：Step1→Step2→Step3→Step1 循环
                int next = (GroundIndexOf(kind) + 1) % ComboMaxSteps;
                mp.MeleeComboStep = (next + 1) % ComboMaxSteps; // 与 TryMeleeStrike 步进语义一致
                int chainDmg = Math.Max(1, (int)(dmg * StepDamageMult(owner, next)));
                SpawnSlash(owner, src, chainDmg, Math.Max(1f, 4f + next), dir, next);
                SoundEngine.PlaySound(next == 2 ? SoundID.Item71 with { Pitch = 0.05f } : SoundID.Item15 with { Pitch = next * 0.12f }, owner.Center);
            }

            mp.MeleeLastStrikeTick = now;
            mp.ClearMeleeBuffer();            // 本次按键已消费，防止冷却结束再次出招
            mp.GatlingConsumedTick = now;     // 标记消费时刻：玩家层吞掉随之而来的松手（一次点按只出一段）
            mp.MeleeFireCd = SwingInterval;
        }

        /// <summary>双击方向触发突进斩：位移 + 短暂无敌 + 大号斩击。</summary>
        public static void DoDash(Player player, int dir)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            int now = (int)Main.GameUpdateCount;
            if (now - mp.LastDashTick < DashCooldown) return;

            mp.LastDashTick = now;
            player.velocity.X = dir * DashSpeed;
            player.immuneTime = Math.Max(player.immuneTime, DashImmuneTime);

            int weaponDmg = player.HeldItem != null ? player.GetWeaponDamage(player.HeldItem) : 32;
            int dmg = Math.Max(8, (int)(weaponDmg * (1f + 0.15f * mp.PrecisionStacks)));
            var src = player.GetSource_ItemUse(player.HeldItem);
            int step = Math.Clamp(mp.MeleeComboStep % ComboMaxSteps, 0, ComboMaxSteps - 1);
            SpawnSlash(player, src, dmg, 6f, dir, step);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = 0.1f }, player.Center);
            for (int i = 0; i < 6 && EffectLimiterSystem.CanSpawnEffect(1, 60); i++)
            {
                int di = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.TintableDust, new Vector2(dir * Main.rand.NextFloat(1f, 4f), Main.rand.NextFloat(-2f, 2f)),
                    0, MalachitePalette.AccentGold, Main.rand.NextFloat(0.8f, 1.4f)).dustIndex;
                Main.dust[di].noGravity = true;
                Main.dust[di].fadeIn = 0.4f;
            }
        }

        /// <summary>生成斩击弹幕（绕玩家弧线挥动，见 MeleeSlashProj）；step → Step1/2/3 映射。</summary>
        private static void SpawnSlash(Player player, IEntitySource source, int damage, float knockback, int dir, int step)
        {
            MoveKind kind = step switch { 0 => MoveKind.Step1, 1 => MoveKind.Step2, _ => MoveKind.Step3 };
            int idx = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                ModContent.ProjectileType<MeleeSlashProj>(), damage, knockback, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].ModProjectile is MeleeSlashProj sp)
            {
                sp.SlashDir = dir;
                sp.SlashStep = step;
                sp.Kind = kind;
            }
        }

        /// <summary>蓄力松手出招：精准满层=两段式终结一段（Finisher1/Bedivere 飞升；二段由斩击弹幕自动派生），否则=蓄力重斩（Charged，v5.2 保留）。</summary>
        public static void FireHeavy(Player player, bool finisher, float damage)
        {
            if (!finisher && !TryUseSpecial(player, MoveKind.Charged)) return; // 蓄力重斩受锁；满月终结豁免
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            float mult = PrecisionDamageMult(mp.PrecisionStacks);
            float dmgMult = finisher ? 1.4f : 1.5f; // 一段 1.4×（二段另有 2.2× + 210% 空间引爆）
            int dmg = Math.Max(1, (int)(damage * mult * dmgMult));
            MoveKind kind = finisher ? MoveKind.Finisher1 : MoveKind.Charged;
            float start = finisher ? Finisher1Arc[0] : ChargedArc[0];
            float end = finisher ? Finisher1Arc[1] : ChargedArc[1];
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), dmg, finisher ? 11f : 9f, dir,
                start, end, finisher ? 1.30f : 1.30f, finisher ? 1.30f : 1.15f, kind, finisher ? 4 : 0);
            SoundEngine.PlaySound(finisher
                ? SoundID.Item71 with { Volume = 0.85f, Pitch = -0.25f }
                : SoundID.Item15 with { Volume = 0.8f, Pitch = -0.2f }, player.Center);
            if (player.whoAmI == Main.myPlayer)
                ScreenShakeSystem.Shake(5f); // 顶点顿挫的强震在斩击弹幕内触发（原 finisher?5f:5f 死分支收敛）
        }

        /// <summary>二段俯冲贯穿（Finisher2）：自半空极速斜下扎落；终点卡肉/巨翼/空间引爆在斩击弹幕内实现。</summary>
        public static void SpawnFinisher2(Player player)
        {
            if (player == null || !player.active || player.dead) return;
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            int wd = player.HeldItem != null ? player.GetWeaponDamage(player.HeldItem) : 32;
            int dmg = Math.Max(1, (int)(wd * PrecisionDamageMult(mp.PrecisionStacks) * 2.2f));
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), dmg, 13f, dir, 0f, 0f, 1.0f, 1.30f, MoveKind.Finisher2, 0);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.85f, Pitch = -0.3f }, player.Center);
        }

        /// <summary>升龙对空斩（UpperRise/Griflet，上方向+攻击）：腾跃升空动量在弹幕爆发帧施加（§三.1）。</summary>
        public static void FireUpperRise(Player player, IEntitySource source, float damage)
        {
            if (!TryUseSpecial(player, MoveKind.UpperRise)) return;
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            float mult = PrecisionDamageMult(mp.PrecisionStacks);
            int dmg = Math.Max(1, (int)(damage * mult * 1.1f));
            SpawnMove(player, source, dmg, 7f, dir,
                UpperRiseArc[0], UpperRiseArc[1], 1.05f, 1.10f, MoveKind.UpperRise, 0);
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.7f, Pitch = 0.35f }, player.Center);
            if (player.whoAmI == Main.myPlayer)
                ScreenShakeSystem.Shake(4f); // 升龙轻震
        }

        /// <summary>空战三连段入口（§模块二）：按 airStep（0/1/2）派生 j.A 快撩 / j.B 大回旋 / j.C 下劈击坠。</summary>
        public static void FireAirChain(Player player, IEntitySource source, float damage, int airStep)
        {
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            int dmg = Math.Max(1, (int)(damage * PrecisionDamageMult(mp.PrecisionStacks) * (airStep == 2 ? 1.15f : 1f)));
            SpawnAirChain(player, source, dmg, 6f, dir, airStep);
            SoundEngine.PlaySound(airStep == 2
                ? SoundID.Item71 with { Volume = 0.8f, Pitch = -0.15f }
                : SoundID.Item15 with { Volume = 0.7f, Pitch = airStep == 0 ? 0.3f : 0.15f }, player.Center);
            if (airStep == 2 && player.whoAmI == Main.myPlayer)
                ScreenShakeSystem.Shake(4f); // j.C 击坠轻震
        }

        /// <summary>生成空战链斩击（j.A 出刀周期仅 10 帧：蓄势 2 + 挥扫 6 + 定格 2）。</summary>
        private static void SpawnAirChain(Player player, IEntitySource source, int damage, float knockback, int dir, int airStep)
        {
            MoveKind kind;
            float start, end, reach, scale;
            int extra = 0;
            switch (airStep)
            {
                case 0:
                    kind = MoveKind.AirStep1; start = AirStep1Arc[0]; end = AirStep1Arc[1];
                    reach = 0.95f; scale = 1.0f;
                    break;
                case 1:
                    kind = MoveKind.AirStep2; start = AirStep2Arc[0]; end = AirStep2Arc[1];
                    reach = 1.0f; scale = 1.05f; extra = 1;
                    break;
                default:
                    kind = MoveKind.AirStep3; start = AirStep3Arc[0]; end = AirStep3Arc[1];
                    reach = 1.05f; scale = 1.15f; extra = 1;
                    break;
            }
            int idx = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                ModContent.ProjectileType<MeleeSlashProj>(), damage, knockback, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].ModProjectile is MeleeSlashProj sp)
            {
                sp.SlashDir = dir;
                sp.Kind = kind;
                sp.CustomStart = start;
                sp.CustomEnd = end;
                sp.ReachMult = reach;
                sp.ScaleMult = scale;
                sp.ExtraHold = extra;
                var mp = player.GetModPlayer<MalachitePlayer>();
                mp.AirChainTailWindow = 14; // D32：空战三连后摇 14 帧窗口内 S+F = 翠羽点穴（原 6 帧过窄，实测反馈放宽）
                if (airStep == 0)
                {
                    sp.GatherOverride = 2; // j.A 极快刀：总周期 10 帧
                    sp.SweepOverride = 6;
                    sp.HoldOverride = 2;
                }
            }
        }

        /// <summary>空中陨石下砸（AirDive/Bors）：急坠动量与触地引爆在弹幕 AirDiveAI 内实现（§一.4）。</summary>
        public static void FireAirDive(Player player, float damage)
        {
            if (!TryUseSpecial(player, MoveKind.AirDive)) return;
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            int dmg = Math.Max(1, (int)(damage * PrecisionDamageMult(mp.PrecisionStacks)));
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), dmg, 8f, dir, 0f, 0f, 1f, 1f, MoveKind.AirDive, 0);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = -0.35f }, player.Center);
        }

        /// <summary>雀返（D31）：后空翻脱战——位移/无敌帧施加于玩家层，弹幕仅残影视觉。</summary>
        public static void FireBackflipRetreat(Player player)
        {
            if (player == null || !player.active || player.dead) return;
            if (!TryUseSpecial(player, MoveKind.BackflipRetreat)) return;
            int dir = player.direction != 0 ? player.direction : 1;
            player.velocity.X = -dir * BackflipVX;
            player.velocity.Y = BackflipVY;
            player.immune = true; // 无敌三件套（与 DashCut 一致；仅 immuneTime 不触发无敌）
            player.immuneTime = Math.Max(player.immuneTime, BackflipImmuneFrames);
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), 0, 0f, dir, 0f, 0f, 1f, 1f, MoveKind.BackflipRetreat, 0);
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.55f, Pitch = 0.5f }, player.Center);
        }

        /// <summary>翠羽点穴（D32）：45° 斜下突刺；命中借力反冲在弹幕 AirNeedleAI 内实现。</summary>
        public static void FireAirNeedle(Player player, float damage)
        {
            if (player == null || !player.active || player.dead) return;
            if (!TryUseSpecial(player, MoveKind.AirNeedle)) return;
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            int dmg = Math.Max(1, (int)(damage * PrecisionDamageMult(mp.PrecisionStacks) * 0.9f));
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), dmg, 3f, dir, 0f, 0f, 1f, 1f, MoveKind.AirNeedle, 0);
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = 0.15f }, player.Center);
        }

        /// <summary>拂柳穿心（D33）：贴地滑铲横扫；半月纹章在弹幕 LandingSweepAI 内生成。</summary>
        public static void FireLandingSweep(Player player, float damage)
        {
            if (player == null || !player.active || player.dead) return;
            if (!TryUseSpecial(player, MoveKind.LandingSweep)) return;
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            int dmg = Math.Max(1, (int)(damage * PrecisionDamageMult(mp.PrecisionStacks) * 1.1f));
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), dmg, 5f, dir, 0f, 0f, 1f, 1f, MoveKind.LandingSweep, 0);
        }

        /// <summary>雀跃·凌虚（D34 方案 B）：地面后摇中双击 W → 抛物线瞬步 + 短无敌；弹幕仅羽毛光尘视觉。</summary>
        public static void FireWarpStep(Player player)
        {
            if (player == null || !player.active || player.dead) return;
            if (!TryUseSpecial(player, MoveKind.WarpStep)) return;
            int dir = player.direction != 0 ? player.direction : 1;
            player.velocity.X = dir * WarpStepVX;
            player.velocity.Y = WarpStepVY;
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, WarpStepImmuneFrames);
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), 0, 0f, dir, 0f, 0f, 1f, 1f, MoveKind.WarpStep, 0);
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.5f, Pitch = 0.65f }, player.Center);
        }

        /// <summary>翠华流风（D35）：升龙命中后 W+F 按住 → 滞空回旋绞杀（三段切割，结束自动派生 j.A）。</summary>
        public static void FireAerialVortex(Player player, float damage)
        {
            if (player == null || !player.active || player.dead) return;
            if (!TryUseSpecial(player, MoveKind.AerialVortex)) return;
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            int dmg = Math.Max(1, (int)(damage * PrecisionDamageMult(mp.PrecisionStacks) * 0.35f));
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), dmg, 3f, dir, 0f, 0f, 1f, 1f, MoveKind.AerialVortex, 0);
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.7f, Pitch = 0.45f }, player.Center);
        }

        /// <summary>突进居合切（DashCut/Mordred）：穿透无敌 + 极速突进 + 沿途延时切线纹章在弹幕 DashCutAI 内实现。</summary>
        // ==================== 防战技复读（2026-09-05 用户拍板）====================
        /// <summary>复读锁兜底时长（帧）：120 = 2 秒未用自动解锁。</summary>
        public const int SpecialLockTimeoutFrames = 120;
        /// <summary>是否受复读锁约束的战技（普攻链 Step/AirStep 与满月终结 Finisher 豁免）。</summary>
        public static bool IsLockableKind(MoveKind kind) =>
            kind == MoveKind.UpperRise || kind == MoveKind.DashCut || kind == MoveKind.Charged
            || kind == MoveKind.AirDive || kind == MoveKind.BackflipRetreat || kind == MoveKind.AirNeedle
            || kind == MoveKind.LandingSweep || kind == MoveKind.WarpStep || kind == MoveKind.AerialVortex;

        /// <summary>战技复读锁：非锁类恒通过；同招式被锁时返回 false 并播拒止音，否则上锁放行。</summary>
        public static bool TryUseSpecial(Player player, MoveKind kind)
        {
            if (!IsLockableKind(kind)) return true;

            // 星核「刃舞·无想」：解除复读锁 —— 同一战技可以连续使用，连招不再被打断。
            if (StarEffects.Has(player, StarFlag.NucleusBladelessDance)) return true;

            var mp = player.GetModPlayer<MalachitePlayer>();
            if (mp.SpecialLockActive && mp.LockedMove == kind)
                return false; // 复读禁止（2026-09-05：静默拒绝，无拒止音）
            mp.SpecialLockActive = true;
            mp.LockedMove = kind;
            mp.SpecialLockTimer = SpecialLockTimeoutFrames; // 2 秒未用自动解锁
            return true;
        }

        /// <summary>突进居合切（DashCut/Mordred）：穿透无敌 + 极速突进 + 沿途延时切线纹章在弹幕 DashCutAI 内实现。</summary>
        public static void FireDashCut(Player player, float damage)
        {
            if (!TryUseSpecial(player, MoveKind.DashCut)) return;
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            int dmg = Math.Max(1, (int)(damage * PrecisionDamageMult(mp.PrecisionStacks)));
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), dmg, 7f, dir, 0f, 0f, 1f, 1f, MoveKind.DashCut, 0);
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = 0.2f }, player.Center);
        }

        /// <summary>生成自定义招式斩击弹幕（绕玩家弧线挥动，见 MeleeSlashProj）。</summary>
        private static void SpawnMove(Player player, IEntitySource source, int damage, float knockback, int dir,
            float startRaw, float endRaw, float reachMult, float scaleMult, MoveKind kind, int extraHold)
        {
            int idx = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                ModContent.ProjectileType<MeleeSlashProj>(), damage, knockback, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].ModProjectile is MeleeSlashProj sp)
            {
                sp.SlashDir = dir;
                sp.Kind = kind;
                sp.CustomStart = startRaw;
                sp.CustomEnd = endRaw;
                sp.ReachMult = reachMult;
                sp.ScaleMult = scaleMult;
                sp.ExtraHold = extraHold;
            }
        }

        // ==================== 纹章命中行为（§四 矩阵"特殊命中机制"列）====================

        /// <summary>
        /// 纹章命中敌方后的特殊机制（矩阵 OnHit 列唯一出处；EsCrestSigilProj 引爆时调用）。
        /// 【原生击退铁律】所有速度覆写必须先过 knockBackResist 门：免疫击退的敌怪（如 Boss 心脏，
        /// knockBackResist &lt;= 0）不受任何推飞/浮空/击坠/硬直减速；部分抗性按比例缩放。
        /// 破甲/震屏/连击能量等非位移效果不受此门约束。
        /// </summary>
        public static void ApplyCrestHit(NPC npc, Player owner, MoveKind kind, int dir)
        {
            if (npc == null || !npc.active) return;
            dir = dir != 0 ? dir : 1;
            float resist = npc.knockBackResist > 0f ? Math.Min(1f, npc.knockBackResist) : 0f;
            bool canMove = resist > 0f;
            switch (kind)
            {
                case MoveKind.Step1: // 轻微击退（原生门） + 为玩家恢复 1 点连击能量（能量恢复与击退免疫无关）
                    if (canMove)
                    {
                        npc.velocity.X += dir * 2.2f * resist;
                        npc.velocity.Y -= 1.2f * resist;
                    }
                    if (owner != null && owner.active)
                    {
                        var mp = owner.GetModPlayer<MalachitePlayer>();
                        mp.PrecisionStacks = Math.Min(PrecisionMaxStacks, mp.PrecisionStacks + 1);
                        mp.PrecisionTimer = PrecisionDuration;
                    }
                    break;
                case MoveKind.Step2: // 强力浮空
                    if (canMove) npc.velocity.Y -= 6.8f * resist;
                    break;
                case MoveKind.Step3: // 大吹飞 + 大震屏
                    if (canMove)
                    {
                        npc.velocity.X = dir * 12f * resist;
                        npc.velocity.Y = Math.Min(npc.velocity.Y, -0.5f * resist);
                    }
                    if (owner != null && owner.whoAmI == Main.myPlayer) ScreenShakeSystem.Shake(8f);
                    break;
                case MoveKind.UpperRise: // 双重阶梯浮空连打（每枚 +1 级颠勺）
                    if (canMove) npc.velocity.Y -= 4.5f * resist;
                    break;
                case MoveKind.AirStep1: // j.A 快撩：轻微托起
                    if (canMove) npc.velocity.Y -= 1.2f * resist;
                    break;
                case MoveKind.AirStep2: // j.B 半空留置雷：持续切割，轻微托起
                    if (canMove) npc.velocity.Y -= 1.6f * resist;
                    break;
                case MoveKind.AirStep3: // j.C 下劈：追加下坠（主击坠在斩击 OnHitNPC 覆写）
                    if (canMove) npc.velocity.Y += 3f * resist;
                    break;
                case MoveKind.AirDive: // 地面冲击波：向两侧炸开
                    if (canMove)
                    {
                        npc.velocity.X = dir * 6f * resist;
                        npc.velocity.Y -= 5f * resist;
                    }
                    break;
                case MoveKind.DashCut: // 多段切线破甲 + 长时间硬直（免疫者只吃破甲判定）
                    npc.AddBuff(BuffID.Ichor, 300);
                    if (canMove) npc.velocity *= 0.15f;
                    break;
                case MoveKind.Finisher2: // 二段终点全屏空间引爆：大吹飞 + 破甲 + 重震
                    if (canMove)
                    {
                        npc.velocity.X = dir * 16f * resist;
                        npc.velocity.Y -= 8f * resist;
                    }
                    npc.AddBuff(BuffID.Ichor, 300);
                    if (owner != null && owner.whoAmI == Main.myPlayer) ScreenShakeSystem.Shake(10f);
                    break;
                case MoveKind.Charged: // v5.2 蓄力重斩纹章：破甲 + 浮空（保持既有手感）
                    npc.AddBuff(BuffID.Ichor, 240);
                    if (canMove) npc.velocity.Y -= 4f * resist;
                    break;
            }
            npc.netUpdate = true;
        }
    }
}
