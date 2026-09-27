// 代码来源与合规署名：
// - 参数体系与横截面模型的设计依据：CalamityOverhaul（MIT, (c) hocha113）OniSlashRenderer / OniCrimsonSweep.fx
//   的「像素空间尺寸系统 / 三层横坐标 / 体色与 alpha 分离」思路，以及 OniAnnihilateArc.fx 的「墨分五色阶化」。
//   本文件为自主实现，未复制上述源码。
//
// 本文件为近战刀光的<strong>调参唯一出处</strong>：零 Terraria 依赖，只用 Microsoft.Xna.Framework。
// 铁律：不得 using Terraria.*（一旦引入，本层即无法被非 Terraria 宿主复用）。
//
// 为什么要单独一份：此前参数只存在于 Core\MalachiteMelee.cs，而离屏渲染侧是**手抄副本**
// （其头注自己写着"数值变化时此文件必须同步，否则审阅失真"）。手抄必漂移，且漂移时预览失真最危险
// ——看着很好，进游戏不对。现在离屏侧直接链接本文件，两边读到的是同一组数值。

using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    /// <summary>刀光横截面档位（供离屏对照调参，正式使用由 <see cref="SlashTuning.ActiveBand"/> 指定）。</summary>
    public enum BandProfile
    {
        /// <summary>现状（单梭形：内缘 60% 主体色 + 外缘线性提亮到白）。</summary>
        Legacy = 0,
        /// <summary>四段晶体：内缘软融 30% / 主体 / 白热核心贴外缘 / 外缘硬切。</summary>
        Crystal = 1,
        /// <summary>四段 + 外缘收窄降亮（防过曝，靠结构而非亮度取胜）。</summary>
        CrystalSoft = 2
    }

    /// <summary>
    /// 条带混合模式。用自建枚举而非直接暴露 XNA 的 BlendState —— 本文件保持语义中立，
    /// 两端（MonoGame / FNA）各自映射到自己的 BlendState。
    /// </summary>
    public enum BandBlend
    {
        /// <summary>加色（改造前行为；无法表达压暗，且吃掉贴图羽化区）。</summary>
        Additive = 0,
        /// <summary>非预乘（顶点色 RGB 与 A 独立；可压暗，是外缘暗边/内缘软融的前提）。</summary>
        NonPremultiplied = 1
    }

    /// <summary>一段横截面的配色（t=0 内缘 → 1 外缘）。纯数据，可被任意宿主复用。</summary>
    public struct BandStop
    {
        public float T;          // 归一化横向位置
        public Color Color;      // 该处颜色
        public float Alpha;      // 该处不透明度

        public BandStop(float t, Color color, float alpha)
        {
            T = t;
            Color = color;
            Alpha = alpha;
        }
    }

    /// <summary>
    /// 近战刀光全参数（调参唯一出处）。
    /// 分区：挥动节奏 / 段位形状 / 贴图基准 / 条带几何 / 曳光 / 反馈。
    /// </summary>
    public static class SlashTuning
    {
        // ==================== 一、挥动节奏（帧）====================

        /// <summary>拉背/蓄势帧数（刃未出、不判定伤害，制造"啪"前的蓄力感）。</summary>
        public const int GatherFrames = 3;

        /// <summary>挥扫帧数（角度在此段内推进，爆发曲线驱动）。</summary>
        public const int SweepFrames = 8;

        /// <summary>满形定格帧数（挥到终点后的短暂停顿）。</summary>
        public const int HoldFrames = 4;

        /// <summary>按住近战键时的出刀间隔（帧；= 蓄势+挥扫+定格，越大越"一顿一顿"）。</summary>
        public const int SwingInterval = GatherFrames + SweepFrames + HoldFrames;

        // ==================== 二、爆发曲线 ====================

        /// <summary>爆发过冲倍率（&gt;1：先冲过目标角再回坐，替代"减速拖尾"）。</summary>
        public const float Overshoot = 1.05f;

        /// <summary>爆发曲线冲顶位置（行程占比，约 0.62 处过冲峰值）。</summary>
        public const float BurstEnd = 0.62f;

        /// <summary>加速度曲线指数 K（&gt;1=先慢后快，越蓄越狠；调试区间 1.4~2.2，&gt;2.5 前段显卡顿）。</summary>
        public const float AccelPower = 1.6f;

        /// <summary>刃长随挥动的脉动幅度（半径 ×(1+该值)，过冲处最大）。</summary>
        public const float ReachPulse = 0.08f;

        // ==================== 三、段位形状（三段弧）====================

        /// <summary>连段段数。</summary>
        public const int ComboSteps = 3;

        // 弧度语义：0 = 朝前水平；负值 = 向上（屏幕 Y 向下，sin&lt;0 即上）。
        private static readonly float[] StepStartRadL = { -1.10f, 1.30f, 2.90f };
        private static readonly float[] StepEndRadL = { 1.50f, -1.14f, -0.60f };
        private static readonly float[] StepReachArr = { 200f, 225f, 256f };
        private static readonly float[] StepScaleArr = { 1.0f, 1.12f, 1.30f };
        private static readonly float[] StepStretchXArr = { 1.05f, 1.12f, 1.22f };
        private static readonly float[] StepSquashYArr = { 0.88f, 0.82f, 0.72f };

        private static int ClampStep(int step) => System.Math.Clamp(step, 0, ComboSteps - 1);

        /// <summary>起始角（右向）；dir&lt;0 时按 Pi−r 镜像（左向）。</summary>
        public static float StepStartRad(int step, int dir)
        {
            float r = StepStartRadL[ClampStep(step)];
            return dir >= 0 ? r : MathHelper.Pi - r;
        }

        /// <summary>结束角（右向）；dir&lt;0 时按 Pi−r 镜像（左向）。</summary>
        public static float StepEndRad(int step, int dir)
        {
            float r = StepEndRadL[ClampStep(step)];
            return dir >= 0 ? r : MathHelper.Pi - r;
        }

        /// <summary>每段挥动半径（px，即攻击范围，越后段越大）。</summary>
        public static float StepReach(int step) => StepReachArr[ClampStep(step)];

        /// <summary>每段刃身大小倍率。</summary>
        public static float StepScale(int step) => StepScaleArr[ClampStep(step)];

        /// <summary>每段刃身贴图 X 向拉伸倍率（沿刃长：段位越大越"甩长"）。</summary>
        public static float StepStretchX(int step) => StepStretchXArr[ClampStep(step)];

        /// <summary>每段刃身贴图 Y 向压扁倍率（厚度：段位越大越"薄利"）。</summary>
        public static float StepSquashY(int step) => StepSquashYArr[ClampStep(step)];

        // ==================== 四、贴图基准 ====================

        /// <summary>裁剪后贴图宽度（px）：画到挥动半径的比例基准（reach / SlashArtWidth）。</summary>
        public const float ArtWidth = 140f;

        /// <summary>裁剪后贴图锚点 Y（最左像素列的垂直质心；X=0 即最左像素 = 挥动圆心）。</summary>
        public const float ArtPivotY = 12.5f;

        /// <summary>刃体贴图本体绘制透明度。</summary>
        public const float ArtAlpha = 1.0f;

        /// <summary>刃体贴图整体亮度倍率。</summary>
        public const float ArtBrightness = 0.62f;

        /// <summary>刃体贴图加色辉光强度（叠加一层柔和发光）。</summary>
        public const float ArtGlowAlpha = 0.30f;

        // ==================== 五、条带几何（本次改造核心）====================

        /// <summary>扇形条带采样段数（越高曲线越丝滑；顶点数 = 段数 × 每段顶点数）。</summary>
        public const int FanSubdivisions = 40;

        /// <summary>
        /// 条带顶点缓冲容量：段数 × 每列顶点数 × 2。
        /// 与 <see cref="BandProfile.Crystal"/> 的 6 个色标对齐（40 × 6 × 2 = 480）；
        /// 换档位时若色标数变化，此值须同步（Legacy 只需 40 × 2 × 2 = 160）。
        /// </summary>
        public const int FanVertexCapacity = FanSubdivisions * 12;

        /// <summary>
        /// 当前启用的条带混合模式。
        /// <see cref="BandBlend.Additive"/> = 改造前行为（保留以便一键回退与 A/B 对照）。
        /// </summary>
        public const BandBlend ActiveBlend = BandBlend.NonPremultiplied;

        /// <summary>
        /// 条带整体亮度增益。顶点色本身较暗（大量深翠绿），在 NonPremultiplied 下与背景做正常 alpha 混合，
        /// 结果会比加色模式暗一大截；此增益用于找回观感，而不必逐个色标改值。
        /// </summary>
        public const float BandGain = 1.0f;

        /// <summary>
        /// 环带最大厚度比例（相对外半径）。**这是刀光可见度的第一旋钮**。
        /// 2026-09-22 诊断：原值 0.35 使最厚仅 20~36px，而挥扫弧长 800~1200px（厚/长 ≈ 1/40），
        /// 视觉上退化成一根线 —— 这是"刀光几乎看不见"的头号原因（第二原因是 Additive 混合吃掉暗部）。
        /// </summary>
        public const float BandMaxThicknessK = 0.62f;

        /// <summary>环带厚度下限（px）——防低速/短半径时条带细到看不见。</summary>
        public const float BandMinThicknessPx = 16f;

        /// <summary>厚度包络的幂次（沿 u 的梭形：&lt;1 更早饱满，&gt;1 更晚收尖）。</summary>
        public const float BandEnvelopePow = 0.70f;

        /// <summary>曳光整体透明度。</summary>
        public const float VisualAlpha = 0.55f;

        /// <summary>外缘曳光带宽度比例（相对挥动半径，细亮线）。</summary>
        public const float EdgeWidthK = 0.07f;

        /// <summary>内侧软融宽度比例（比外缘宽、更淡）。</summary>
        public const float BandWidthK = 0.20f;

        /// <summary>轨迹采样上限（帧，= 拖尾残影/曳光带长度）。</summary>
        public const int TrailMax = 12;

        // ==================== 六、横截面配色 ====================

        /// <summary>
        /// 当前启用的横截面档位。
        /// <see cref="BandProfile.Legacy"/> = 改造前行为（保留以便一键回退与 A/B 对照）。
        /// </summary>
        public const BandProfile ActiveBand = BandProfile.Crystal;

        /// <summary>白热核心所在的外缘侧归一化位置（0.90 → 核心贴外缘，据鬼切 OniCrimsonSlash 横截面审计）。</summary>
        public const float CoreAt = 0.90f;

        /// <summary>白热核心的高斯宽度（越小核心越锐利）。</summary>
        public const float CoreSigma = 0.062f;

        /// <summary>内缘软融区占比（t &lt; 该值 为内缘淡出段）。</summary>
        public const float InnerFadeK = 0.30f;

        /// <summary>外缘硬切起点（t &gt; 该值 迅速降到 0，制造"硬边"而非柔散）。</summary>
        public const float OuterCutK = 0.965f;

        /// <summary>
        /// 按档位取横截面色标表（t=0 内缘 → 1 外缘）。
        /// 体色（连续）与不透明度（可硬切）**分离**：结构靠 alpha，亮度靠 color。
        /// </summary>
        /// <param name="tint">主题色（段位色）。</param>
        /// <param name="deep">阴面深色（MalachitePalette.GreenDeep）。</param>
        /// <param name="u">沿弧长归一化位置（0=尾 → 1=锋），用于整体亮度渐变。</param>
        public static BandStop[] StopsFor(BandProfile profile, Color tint, Color deep, float u)
        {
            u = MathHelper.Clamp(u, 0f, 1f);

            // 尾端整体压暗（越靠锋越亮）—— 与 u 相关的整体亮度包络
            float tailK = (float)System.Math.Pow(u, 1.2f);

            switch (profile)
            {
                case BandProfile.Legacy:
                {
                    // 改造前：内缘 0.6×主体色，外缘沿 u 线性提到纯白（暗翠→纯白之间无缓冲带）
                    Color colIn = tint * (0.6f * tailK);
                    Color colOut = Color.Lerp(tint, Color.White, MathHelper.Clamp(u * 1.5f, 0f, 1f)) * tailK;
                    return new[]
                    {
                        new BandStop(0f, colIn, 0.6f * tailK),
                        new BandStop(1f, colOut, tailK),
                    };
                }

                case BandProfile.CrystalSoft:
                {
                    // 外缘收窄降亮：靠结构取胜，白热核心峰值压到 0.80 防过曝
                    return new[]
                    {
                        new BandStop(0.00f, Color.Lerp(tint, deep, 0.55f) * tailK, 0.20f * tailK),
                        new BandStop(0.30f, tint * (0.85f * tailK), 0.62f * tailK),
                        new BandStop(0.62f, Color.Lerp(tint, Color.White, 0.30f) * tailK, 0.80f * tailK),
                        new BandStop(CoreAt, Color.Lerp(tint, Color.White, 0.72f) * tailK, 0.88f * tailK),
                        new BandStop(0.985f, Color.White * tailK, 0.55f * tailK),
                        new BandStop(1.00f, Color.White * tailK, 0f),
                    };
                }

                default: // BandProfile.Crystal
                {
                    // 四段晶体：内缘软融 30% / 主体 / 白热核心贴外缘(0.90) / 外缘硬切
                    return new[]
                    {
                        new BandStop(0.00f, Color.Lerp(tint, deep, 0.60f) * tailK, 0.16f * tailK),
                        new BandStop(InnerFadeK, tint * (0.90f * tailK), 0.66f * tailK),
                        new BandStop(0.60f, Color.Lerp(tint, Color.White, 0.34f) * tailK, 0.84f * tailK),
                        new BandStop(CoreAt, Color.Lerp(tint, Color.White, 0.80f) * tailK, 0.95f * tailK),
                        new BandStop(OuterCutK, Color.White * tailK, 0.80f * tailK),
                        new BandStop(1.00f, Color.White * tailK, 0f),
                    };
                }
            }
        }

        // ==================== 七、命中判定盒（覆盖挥动半径的大盒，随挥动扫过）====================

        /// <summary>判定盒中心所在的半径比例（相对 StepReach；0.5 = 覆盖内~外缘）。</summary>
        public const float HitCenterK = 0.5f;

        /// <summary>判定盒宽度比例（相对 StepReach，约 1.02 = 几乎覆盖全刃长）。</summary>
        public const float HitSpanK = 1.02f;

        /// <summary>判定盒高度（px，垂直厚度）。</summary>
        public const int HitHeight = 150;

        // ==================== 八、命中反馈 ====================

        /// <summary>卡肉抑制窗口（帧）：同一窗口内的后续挥击不再触发卡肉。</summary>
        public const int HitstopGapFrames = 10;

        /// <summary>命中反馈持续帧数。</summary>
        public const int HitFlashFrames = 7;

        /// <summary>命中扩散环最终半径（px）。</summary>
        public const float HitRingMaxR = 46f;

        /// <summary>命中砍痕闪刃长度（px，沿挥动切线）。</summary>
        public const float HitSlashLen = 60f;
    }
}
