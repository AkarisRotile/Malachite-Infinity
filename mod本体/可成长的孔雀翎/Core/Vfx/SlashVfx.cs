// 代码来源与合规署名：
// - 挥动/刀光管线（学习后自主实现，未复制源码）：CalamityOverhaul（MIT, (c) hocha113）OniSlash/OniSlashRenderer
//   的爆发曲线思路，CalamityEntropy（社区开源）的顶点扇形条带(TriangleStrip) + Additive 发光混合范式。
// 本文件为自主实现。

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 刀光条带的<strong>中立顶点</strong>：不含任何图形 API 类型，供游戏端（MonoGame 的 VertexPositionColorTexture）
    /// 与离线预览端（FNA 的同名类型）各自转换。这是"游戏与预览共用同一份构建逻辑"的前提
    /// —— 两端的顶点结构体虽然同名同布局，但不是同一个类型，直接互相传不过去。
    /// </summary>
    public struct SlashVertex
    {
        public Vector2 Pos;
        public Color Color;
        public Vector2 UV;

        public SlashVertex(Vector2 pos, Color color, Vector2 uv)
        {
            Pos = pos;
            Color = color;
            UV = uv;
        }
    }

    /// <summary>
    /// 近战刀光的<strong>纯几何数学</strong>：零 Terraria 依赖，只用 Microsoft.Xna.Framework。
    /// - 出处：自 Projectiles\MeleeSlashProj.cs 的 DrawFanStrip / DrawProceduralBlade 提取，提取后为<strong>唯一出处</strong>；
    ///   游戏内绘制与 工具\VfxPreview 离线预览共用同一份文件，杜绝"两份实现各自漂移"。
    /// - 铁律：本文件<strong>不得</strong> using Terraria.*（一旦引入，离线预览工程即无法编译）。
    /// - 坐标：传入的 oc / rootWorld 为世界坐标时，由 screenOffset（= Main.screenPosition）统一平移到屏幕空间。
    /// </summary>
    public static class SlashVfx
    {
        /// <summary>扇形条带采样段数（越高曲线越丝滑；顶点数 = 段数 × 每段顶点数）。</summary>
        public const int FanSubdivisions = SlashTuning.FanSubdivisions;

        /// <summary>刃身实体顶点数（6 个三角片）。</summary>
        public const int BladeVertexCount = 18;

        /// <summary>平滑步（提取自 MeleeSlashProj.SmoothStep01）。</summary>
        public static float SmoothStep01(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        /// <summary>
        /// 爆发曲线（提取自 MeleeSlashProj.BurstCurve）：前段加速冲顶过冲，后段回坐。
        /// burstEnd=冲顶位置占比；overshoot=过冲倍率；accelPower=加速度指数 K。
        /// </summary>
        public static float BurstCurve(float p, float burstEnd, float overshoot, float accelPower)
        {
            p = Math.Clamp(p, 0f, 1f);
            if (p < burstEnd) return overshoot * (float)Math.Pow(p / burstEnd, accelPower);
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((p - burstEnd) / (1f - burstEnd)));
        }

        /// <summary>
        /// 构建刀光扇形条带（多段横截面 TriangleStrip）。
        /// <para/>顶点布局：每个 u 采样点产出 <c>stops.Length * 2</c> 个顶点，按 t 由**内缘向外缘**排列；
        /// 相邻两列的对应位置连成四边形（条带宽度 = stops.Length−1 段）。因此顶点总数 = 段数 × stops.Length × 2。
        /// <para/>u 轴**按累积弧长归一化**（对齐鬼切的 <c>uLenScale = 弧长/瓦片长</c>）——
        /// 原先用顶点序号当作 u，会让"扫得快"的帧纹理被拉长、"扫得慢"的帧被压挤，逐帧密度看着跳。
        /// </summary>
        /// <param name="th">挥扫角度历史（尾→头）。</param>
        /// <param name="sc">艺术缩放历史（半径 = sc × artWidth × sx）。</param>
        /// <param name="sx">刃长 X 向拉伸历史。</param>
        /// <param name="oc">挥动圆心（世界坐标）。</param>
        /// <param name="screenOffset">屏幕平移量（= Main.screenPosition）。</param>
        /// <param name="tint">主题色（段位色）。</param>
        /// <param name="deep">阴面深色（MalachitePalette.GreenDeep）。</param>
        /// <param name="profile">横截面档位（见 SlashTuning）。</param>
        /// <param name="holdFade">定格淡出（≤1）。</param>
        /// <returns>写入顶点数；0 表示数据不足，调用方应直接跳过绘制。</returns>
        public static int BuildFanStrip(
            IReadOnlyList<float> th, IReadOnlyList<float> sc, IReadOnlyList<float> sx,
            Vector2 oc, Color tint, Color deep, BandProfile profile, float holdFade, float artWidth,
            Vector2 screenOffset, SlashVertex[] dest, int subdivisions = SlashTuning.FanSubdivisions,
            float bandKOverride = -1f, float gain = 1f)
        {
            int n = th == null ? 0 : th.Count;
            if (n < 2 || dest == null) return 0;

            // 色标数只取决于档位（Legacy=2 / Crystal·CrystalSoft=6），用于算顶点布局与校验缓冲。
            // 注意：真正取值必须**逐列**按各自的 u 调 StopsFor（它的 tailK 依赖 u，越靠刀锋越亮），
            // 若在循环外取一次会让整条带亮度均匀、丢掉尾端压暗。
            int stopCount = SlashTuning.StopsFor(profile, tint, deep, 1f).Length;
            int vertsPerColumn = stopCount * 2;
            if (dest.Length < subdivisions * vertsPerColumn) return 0;

            float bandK = bandKOverride > 0f ? bandKOverride : SlashTuning.BandMaxThicknessK;

            // ── 预计算累积弧长（角位移 × 半径），用于 u 轴归一化 ──
            // 用"角度差 × 该段平均半径"近似弧长，足够消除密度不一致，且零堆分配。
            float[] arc = new float[subdivisions];
            {
                float acc = 0f;
                arc[0] = 0f;
                for (int i = 1; i < subdivisions; i++)
                {
                    float pu = (i - 1) / (float)(subdivisions - 1);
                    float cu = i / (float)(subdivisions - 1);
                    float pt = SampleTheta(th, n, pu);
                    float ct = SampleTheta(th, n, cu);
                    float pr = SampleScale(sc, n, pu) * artWidth * SampleScale(sx, n, pu);
                    float cr = SampleScale(sc, n, cu) * artWidth * SampleScale(sx, n, cu);
                    acc += Math.Abs(AngleDelta(ct, pt)) * (pr + cr) * 0.5f;
                    arc[i] = acc;
                }
                if (acc > 0.0001f)
                    for (int i = 0; i < subdivisions; i++) arc[i] /= acc;
                else
                    for (int i = 0; i < subdivisions; i++) arc[i] = i / (float)(subdivisions - 1);
            }

            for (int i = 0; i < subdivisions; i++)
            {
                float u = arc[i];

                float theta = SampleTheta(th, n, u);
                float scl = SampleScale(sc, n, u);
                float sxf = SampleScale(sx, n, u);

                float rOuter = scl * artWidth * sxf;
                Vector2 dd = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));

                // 梭形厚度包络：刀尾极细 → 中段饱满 → 刀锋前段紧收
                float env = (float)Math.Sin(Math.Pow(u, SlashTuning.BandEnvelopePow) * MathHelper.Pi);
                float bandThickness = Math.Max(SlashTuning.BandMinThicknessPx, rOuter * bandK * env);
                bandThickness = Math.Min(bandThickness, rOuter * 0.95f); // 不许厚到把圆心吃进去
                float rInner = rOuter - bandThickness;

                Vector2 pInner = oc + dd * rInner - screenOffset;
                Vector2 pOuter = oc + dd * rOuter - screenOffset;

                // 逐列按本列 u 取色标（tailK 依赖 u）
                BandStop[] col = SlashTuning.StopsFor(profile, tint, deep, u);

                for (int s = 0; s < stopCount; s++)
                {
                    float t = col[s].T;
                    Vector2 pos = Vector2.Lerp(pInner, pOuter, t);
                    // 顶点色用 NonPremultiplied 语义（RGB 与 A 独立），由调用方设好 BlendState。
                    // gain 用于整体找回观感：NonPremultiplied 比加色暗一大截，而不必逐个色标改值。
                    Color c = col[s].Color * MathHelper.Clamp(col[s].Alpha * holdFade * gain, 0f, 1f);

                    int baseIdx = i * vertsPerColumn + s * 2;
                    dest[baseIdx] = new SlashVertex(pos, c, new Vector2(u, t));
                    dest[baseIdx + 1] = new SlashVertex(pos, c, new Vector2(u, t));
                }
            }

            return subdivisions * vertsPerColumn;
        }

        /// <summary>在历史列表里按归一化位置线性插值取角度（消除折线感）。</summary>
        private static float SampleTheta(IReadOnlyList<float> th, int n, float u)
        {
            float idx = u * (n - 1);
            int i0 = (int)idx;
            if (i0 < 0) i0 = 0;
            if (i0 > n - 1) i0 = n - 1;
            int i1 = Math.Min(i0 + 1, n - 1);
            return MathHelper.Lerp(th[i0], th[i1], idx - i0);
        }

        /// <summary>在历史列表里按归一化位置线性插值取标量。</summary>
        private static float SampleScale(IReadOnlyList<float> list, int n, float u)
        {
            if (list == null || list.Count < n) return 1f;
            float idx = u * (n - 1);
            int i0 = (int)idx;
            if (i0 < 0) i0 = 0;
            if (i0 > n - 1) i0 = n - 1;
            int i1 = Math.Min(i0 + 1, n - 1);
            return MathHelper.Lerp(list[i0], list[i1], idx - i0);
        }

        /// <summary>角度短弧差（处理 ±π 环绕，避免跨环时算出 2π 的假弧长）。</summary>
        private static float AngleDelta(float a, float b)
        {
            float d = a - b;
            while (d > MathHelper.Pi) d -= MathHelper.TwoPi;
            while (d < -MathHelper.Pi) d += MathHelper.TwoPi;
            return d;
        }

        /// <summary>
        /// 构建修长晶刃实体（6 个三角片：阴阳双面棱晶分色）。
        /// 提取自 DrawProceduralBlade；渲染状态由调用方负责。
        /// </summary>
        /// <param name="rootWorld">刃根（世界坐标，= 玩家中心）。</param>
        /// <param name="theta">刃朝向弧度。</param>
        /// <param name="bladeLength">刃长 px。</param>
        /// <param name="themeColor">主题色（外缘/阳面基色）。</param>
        /// <param name="deepColor">阴面深色（MalachitePalette.GreenDeep）。</param>
        public static void BuildBlade(
            Vector2 rootWorld, float theta, float bladeLength, Color themeColor, Color deepColor,
            Vector2 screenOffset, VertexPositionColor[] dest)
        {
            if (dest == null || dest.Length < BladeVertexCount) return;

            Vector2 root = rootWorld - screenOffset;
            Vector2 dir = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
            Vector2 normal = new Vector2(-dir.Y, dir.X);

            float guardLen = bladeLength * 0.08f;
            float guardWidth = 6.0f;
            float midLen = bladeLength * 0.82f;
            float midWidth = 3.2f;

            Vector2 pRoot = root;
            Vector2 lGuard = root + dir * guardLen - normal * guardWidth;
            Vector2 rGuard = root + dir * guardLen + normal * guardWidth;
            Vector2 lMid = root + dir * midLen - normal * midWidth;
            Vector2 rMid = root + dir * midLen + normal * midWidth;
            Vector2 pTip = root + dir * bladeLength;

            Color cSpine = Color.White * 0.95f;
            Color cLeftFace = Color.Lerp(themeColor, Color.White, 0.38f);
            Color cRightFace = Color.Lerp(themeColor, deepColor, 0.40f);
            Color cEdgeOut = themeColor * 0.75f;

            dest[0] = new VertexPositionColor(new Vector3(pRoot, 0), cSpine);
            dest[1] = new VertexPositionColor(new Vector3(lGuard, 0), cEdgeOut);
            dest[2] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            dest[3] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            dest[4] = new VertexPositionColor(new Vector3(lGuard, 0), cLeftFace);
            dest[5] = new VertexPositionColor(new Vector3(lMid, 0), cLeftFace);
            dest[6] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            dest[7] = new VertexPositionColor(new Vector3(lMid, 0), cLeftFace);
            dest[8] = new VertexPositionColor(new Vector3(pTip, 0), cSpine);
            dest[9] = new VertexPositionColor(new Vector3(pRoot, 0), cSpine);
            dest[10] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            dest[11] = new VertexPositionColor(new Vector3(rGuard, 0), cEdgeOut);
            dest[12] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            dest[13] = new VertexPositionColor(new Vector3(rMid, 0), cRightFace);
            dest[14] = new VertexPositionColor(new Vector3(rGuard, 0), cRightFace);
            dest[15] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            dest[16] = new VertexPositionColor(new Vector3(pTip, 0), cSpine);
            dest[17] = new VertexPositionColor(new Vector3(rMid, 0), cRightFace);
        }
    }
}
