// 本文件为自主实现（无外部参考源码）。
//
// ============================================================================
// 程序化图元库 —— **真正的几何**，不是贴图变换
// ============================================================================
// 为什么需要它（2026-09-25 用户批评后建立）：
//   此前所有"圆形/星芒/光点"都是拿 Extra[98]/Extra[89] 两张贴图做缩放，或者用 1×1 像素堆方块。
//   那既画不出真正的平滑圆形（贴图被压成竖条、方块暴露像素栅格），也不是像素风格该有的样子。
//   参考项目（CalamityOverhaul 的 OniAnnihilateArc.fx / OniCrimsonSweep.fx）的做法是**在几何/片元层
//   解析地画形状**——极角解析、像素空间尺寸系统。本文件是那条路线在本项目（无 shader）下的等价实现：
//   用 GraphicsDevice 直绘三角形，形状由数学给出，圆是真的圆。
//
// 铁律：零 Terraria 依赖（只用 XNA/FNA 的 GraphicsDevice / BasicEffect / VertexPositionColor），
//       因此本层可被任意宿主复用，且实现只有一份。
// 批次纪律：调用方必须在 End() 掉 SpriteBatch 之后调用本库（与 MeleeSlashProj.DrawFanStrip 同模式），
//       本库只负责设状态 + 提交三角形，不负责 Begin/End SpriteBatch。

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>程序化图元绘制：圆、环、弧、星芒、线段 —— 全部由数学生成，不使用贴图。</summary>
    public static class PrimitiveVfx
    {
        /// <summary>
        /// 单个图元允许的最大三角形数。
        /// <para/>2026-09-27 由 512 提到 8192：星网 UI 有 36 节点 + 50 连线 × 多层，
        /// 批处理模式下需要一次性容纳约 4600 个三角形，512 会被静默截断（表现为"后半张图没画出来"）。
        /// </summary>
        private const int MaxTris = 8192;

        private static VertexPositionColor[] _buffer = new VertexPositionColor[MaxTris * 3];
        private static BasicEffect _fx;
        private static GraphicsDevice _gd;
        private static Matrix _view = Matrix.Identity;
        private static Matrix _proj = Matrix.Identity;

        /// <summary>
        /// 由调用方（游戏端 ModSystem / UI）在每帧绘制前注入设备与矩阵。
        /// 之所以用静态注入而不是逐函数传参：图元调用点很多，传参会让签名爆炸；
        /// 而这些状态每帧只变一次，静态注入更贴合实际用法。
        /// </summary>
        public static void Begin(GraphicsDevice gd, Matrix view, Matrix proj)
        {
            _gd = gd;
            _view = view;
            _proj = proj;
            if (_fx == null || _fx.IsDisposed)
            {
                _fx = new BasicEffect(gd)
                {
                    VertexColorEnabled = true,
                    TextureEnabled = false,
                    LightingEnabled = false,
                    FogEnabled = false,
                };
            }
        }

        /// <summary>结束一帧的图元绘制（释放设备引用，避免跨帧持有失效设备）。</summary>
        public static void End()
        {
            _gd = null;
        }

        /// <summary>是否可用（未注入设备时为 false，所有图元调用将静默跳过）。</summary>
        public static bool Ready => _gd != null && !_gd.IsDisposed && _fx != null && !_fx.IsDisposed;

        /// <summary>
        /// 提交累积的三角形。内部在 Additive 混合下绘制；**调用方需自行 End/Begin SpriteBatch**。
        /// <para/>批处理模式下不绘制（由 <see cref="BatchEnd"/> 统一提交）。
        /// </summary>
        private static void Flush(int vertexCount, BlendState blend)
        {
            if (_batching) return;
            if (!Ready || vertexCount <= 0) return;

            _fx.World = Matrix.Identity;
            _fx.View = _view;
            _fx.Projection = _proj;
            _fx.TextureEnabled = false;

            _gd.BlendState = blend;
            _gd.RasterizerState = RasterizerState.CullNone;
            _gd.DepthStencilState = DepthStencilState.None;

            foreach (EffectPass pass in _fx.CurrentTechnique.Passes) pass.Apply();
            _gd.DrawUserPrimitives(PrimitiveType.TriangleList, _buffer, 0, vertexCount / 3);
        }

        private static void Push(Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            int i = _count * 3;
            if (i + 2 >= _buffer.Length) return;
            _buffer[i] = new VertexPositionColor(new Vector3(a, 0f), ca);
            _buffer[i + 1] = new VertexPositionColor(new Vector3(b, 0f), cb);
            _buffer[i + 2] = new VertexPositionColor(new Vector3(c, 0f), cc);
            _count++;
        }

        private static int _count;

        /// <summary>
        /// 批处理开关。关闭时（默认）每个图元各自 ResetCount + Flush，**与历史行为完全一致**；
        /// 打开时所有图元累积进同一顶点缓冲，由 <see cref="BatchEnd"/> 一次提交 —— 300+ draw call 收敛为 1。
        /// </summary>
        private static bool _batching;

        private static void ResetCount() { if (!_batching) _count = 0; }

        /// <summary>
        /// 开始批处理：此后所有图元调用只往缓冲里堆，不各自提交。
        /// <para/>用途：星网这类"几百个图元同帧同混合状态"的 UI 绘制。
        /// 注意：批处理只合并**同一 BlendState** 的图元；跨混合状态的绘制不要放进同一批。
        /// </summary>
        public static void BatchBegin()
        {
            _batching = true;
            _count = 0;
        }

        /// <summary>结束批处理并一次性提交（blend 一般传 <see cref="BlendState.Additive"/>）。</summary>
        public static void BatchEnd(BlendState blend)
        {
            _batching = false;
            Flush(_count * 3, blend);
            _count = 0;
        }

        /// <summary>本批已累积的三角形数（调试/预算核对用）。</summary>
        public static int BatchedTriangles => _count;

        // ====================================================================
        // 圆 / 环 / 弧
        // ====================================================================

        /// <summary>
        /// 实心圆（真正的圆：由 N 段三角扇构成，圆心到边缘颜色连续过渡）。
        /// </summary>
        /// <param name="center">圆心。</param>
        /// <param name="radius">半径（px）。</param>
        /// <param name="core">圆心颜色。</param>
        /// <param name="edge">边缘颜色（通常 alpha 0，得到柔边）。</param>
        /// <param name="segments">分段数（越大越圆；24 已足够平滑）。</param>
        public static void FillCircle(Vector2 center, float radius, Color core, Color edge, int segments = 24)
        {
            if (!Ready || radius <= 0.05f) return;
            segments = Math.Clamp(segments, 6, 96);
            ResetCount();

            for (int i = 0; i < segments; i++)
            {
                float a0 = MathHelper.TwoPi * i / segments;
                float a1 = MathHelper.TwoPi * (i + 1) / segments;
                Vector2 p0 = center + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * radius;
                Vector2 p1 = center + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * radius;
                Push(center, p0, p1, core, edge, edge);
            }
            Flush(_count * 3, BlendState.Additive);
        }

        /// <summary>
        /// 由内向外的多层柔光圆（比单层更接近真实发光点：中心饱和、外圈快速衰减）。
        /// </summary>
        public static void GlowDot(Vector2 center, float radius, Color color, float intensity, int layers = 3)
        {
            if (!Ready || radius <= 0.05f) return;
            layers = Math.Clamp(layers, 1, 6);
            for (int L = layers; L >= 1; L--)
            {
                float t = L / (float)layers;              // 1=最大最淡
                float r = radius * (0.45f + 0.85f * t);
                float a = intensity * (1f - t) * 0.9f + intensity * 0.18f;
                FillCircle(center, r, color * a, color * 0f, 24);
            }
        }

        /// <summary>圆环（有内外半径的环带，真正的几何环，不是贴图拼的）。</summary>
        public static void Ring(Vector2 center, float radius, float thickness, Color color, int segments = 48)
        {
            if (!Ready || radius <= 0.05f || thickness <= 0.05f) return;
            segments = Math.Clamp(segments, 8, 128);
            ResetCount();

            float half = thickness * 0.5f;
            float rIn = Math.Max(0f, radius - half);
            float rOut = radius + half;
            Color c = color;

            for (int i = 0; i < segments; i++)
            {
                float a0 = MathHelper.TwoPi * i / segments;
                float a1 = MathHelper.TwoPi * (i + 1) / segments;
                Vector2 d0 = new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0));
                Vector2 d1 = new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1));
                Vector2 i0 = center + d0 * rIn, o0 = center + d0 * rOut;
                Vector2 i1 = center + d1 * rIn, o1 = center + d1 * rOut;
                Push(i0, o0, o1, c, c, c);
                Push(i0, o1, i1, c, c, c);
            }
            Flush(_count * 3, BlendState.Additive);
        }

        /// <summary>
        /// 圆弧带（从 a0 到 a1 的环形扇区）——星图的弧线、扫掠痕迹用它，角度是真的圆弧。
        /// </summary>
        public static void Arc(Vector2 center, float radius, float thickness, float a0, float a1,
            Color inner, Color outer, int segments = 32, bool fadeEnds = true)
        {
            if (!Ready || radius <= 0.05f || thickness <= 0.05f) return;
            segments = Math.Clamp(segments, 4, 128);
            ResetCount();

            float half = thickness * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                float b0 = MathHelper.Lerp(a0, a1, t0);
                float b1 = MathHelper.Lerp(a0, a1, t1);

                // 两端渐隐（让弧线不是硬切头）
                float k0 = fadeEnds ? (float)Math.Sin(MathHelper.Pi * t0) : 1f;
                float k1 = fadeEnds ? (float)Math.Sin(MathHelper.Pi * t1) : 1f;

                Vector2 d0 = new Vector2((float)Math.Cos(b0), (float)Math.Sin(b0));
                Vector2 d1 = new Vector2((float)Math.Cos(b1), (float)Math.Sin(b1));
                Vector2 i0 = center + d0 * (radius - half), o0 = center + d0 * (radius + half);
                Vector2 i1 = center + d1 * (radius - half), o1 = center + d1 * (radius + half);

                Push(i0, o0, o1, inner * k0, outer * k0, outer * k1);
                Push(i0, o1, i1, inner * k0, outer * k1, inner * k1);
            }
            Flush(_count * 3, BlendState.Additive);
        }

        /// <summary>线段（带柔边的两三角带）——星座连线用它，粗细均匀且不依赖贴图。</summary>
        public static void Line(Vector2 a, Vector2 b, float thickness, Color ca, Color cb)
        {
            if (!Ready || thickness <= 0.05f) return;
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.05f) return;
            Vector2 n = new Vector2(-d.Y, d.X) / len * (thickness * 0.5f);
            ResetCount();
            Push(a - n, a + n, b + n, ca, ca, cb);
            Push(a - n, b + n, b - n, ca, cb, cb);
            Flush(_count * 3, BlendState.Additive);
        }

        /// <summary>
        /// 星芒（十字/多向）。真实星图里亮星才有芒，形状是**收尖的细长菱形**，由几何生成。
        /// </summary>
        /// <param name="arms">芒的条数（4 = 十字，6/8 更华丽）。</param>
        /// <param name="holeRadius">
        /// 内孔半径（px）：每条芒的**根部**从 <c>center + dir × holeRadius</c> 起画，而不是从圆心起画。
        /// <para/>★ 为什么需要它（2026-09-27 实测）：每根芒是"从中心发散的三角形扇"，所有芒共用同一个
        /// 中心顶点。画两层四芒 = **16 个三角形叠加在同一个像素上**，加色混合下
        /// GreenDark×16 就已钳制成纯白 —— 表现为"未点亮的星核中心也是白点"、金色星核被冲成白色。
        /// 给一个内孔即可让根部散开成小方阵，中心不再堆积。默认 0 = 与历史行为完全一致。
        /// </param>
        public static void StarSpikes(Vector2 center, float length, float width, Color color,
            int arms = 4, float rotation = 0f, float falloff = 1.6f, float holeRadius = 0f)
        {
            if (!Ready || length <= 0.05f) return;
            arms = Math.Clamp(arms, 2, 12);
            holeRadius = MathHelper.Clamp(holeRadius, 0f, Math.Max(0f, length - 0.1f));
            float span = length - holeRadius;
            if (span <= 0.05f) return;
            ResetCount();

            for (int k = 0; k < arms; k++)
            {
                float ang = rotation + MathHelper.Pi * k / arms;
                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                Vector2 nrm = new Vector2(-dir.Y, dir.X);
                Vector2 root = center + dir * holeRadius;
                Vector2 tip = center + dir * length;
                // 根宽、腰宽（用 falloff 控制收尖速度：越大越尖）
                Vector2 b0 = root + nrm * width * 0.5f;
                Vector2 b1 = root - nrm * width * 0.5f;
                Vector2 mid = root + dir * (span * 0.45f);
                Vector2 m0 = mid + nrm * width * 0.5f / (1f + falloff * 0.5f);
                Vector2 m1 = mid - nrm * width * 0.5f / (1f + falloff * 0.5f);

                Color cTip = color * 0.15f;
                Push(b0, b1, m1, color, color, color * 0.7f);
                Push(b0, m1, m0, color, color * 0.7f, color * 0.7f);
                Push(m0, m1, tip, color * 0.7f, color * 0.7f, cTip);
            }
            Flush(_count * 3, BlendState.Additive);
        }

        /// <summary>正多边形环（n 边形，几何生成，用于技能树节点外框）。</summary>
        public static void PolygonRing(Vector2 center, float radius, float thickness, int sides, float rotation, Color color)
        {
            if (!Ready || sides < 3) return;
            ResetCount();
            float half = thickness * 0.5f;
            Vector2 Prev(float a, float r) => center + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * r;

            for (int k = 0; k < sides; k++)
            {
                float a0 = rotation + MathHelper.TwoPi * k / sides;
                float a1 = rotation + MathHelper.TwoPi * (k + 1) / sides;
                Vector2 i0 = Prev(a0, radius - half), o0 = Prev(a0, radius + half);
                Vector2 i1 = Prev(a1, radius - half), o1 = Prev(a1, radius + half);
                Push(i0, o0, o1, color, color, color);
                Push(i0, o1, i1, color, color, color);
            }
            Flush(_count * 3, BlendState.Additive);
        }

        /// <summary>
        /// 正多边形**实心**（几何生成）。
        /// <para/>2026-09-27 新增：星网的三类星需要"实心菱形 / 实心八边形 / 实心星核"，
        /// 而此前只有环（<see cref="PolygonRing"/>）与圆（<see cref="FillCircle"/>），
        /// 用圆代替会丢掉"星尘是菱形、星宿是八边形"的形状区分。
        /// </summary>
        /// <param name="sides">边数（4 = 菱形/方形，8 = 八边形）。</param>
        /// <param name="rotation">整体旋转（弧度）。</param>
        public static void FillPolygon(Vector2 center, float radius, int sides, float rotation, Color color)
        {
            if (!Ready || sides < 3 || radius <= 0.05f) return;
            sides = Math.Clamp(sides, 3, 24);
            ResetCount();

            for (int k = 0; k < sides; k++)
            {
                float a0 = rotation + MathHelper.TwoPi * k / sides;
                float a1 = rotation + MathHelper.TwoPi * (k + 1) / sides;
                Vector2 p0 = center + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * radius;
                Vector2 p1 = center + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * radius;
                Push(center, p0, p1, color, color, color);
            }
            Flush(_count * 3, BlendState.Additive);
        }

        /// <summary>
        /// 柔边实心多边形（芯亮边淡）—— 比 <see cref="FillPolygon"/> 更适合发光节点。
        /// </summary>
        public static void FillPolygonSoft(Vector2 center, float radius, int sides, float rotation, Color core, Color edge)
        {
            if (!Ready || sides < 3 || radius <= 0.05f) return;
            sides = Math.Clamp(sides, 3, 24);
            ResetCount();

            for (int k = 0; k < sides; k++)
            {
                float a0 = rotation + MathHelper.TwoPi * k / sides;
                float a1 = rotation + MathHelper.TwoPi * (k + 1) / sides;
                Vector2 p0 = center + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * radius;
                Vector2 p1 = center + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * radius;
                Push(center, p0, p1, core, edge, edge);
            }
            Flush(_count * 3, BlendState.Additive);
        }

        /// <summary>
        /// 沿路径推进的流光（彗头 + 渐隐尾）。
        /// <para/>用途：星网加点时"能量从翎心沿最短路径涌向新节点"。
        /// </summary>
        /// <param name="from">段起点。</param>
        /// <param name="to">段终点。</param>
        /// <param name="headT">彗头在本段上的归一化位置（0~1）。</param>
        /// <param name="headLen">彗体总长（px）。</param>
        /// <param name="width">彗体宽度（px）。</param>
        /// <param name="headColor">彗头颜色。</param>
        /// <param name="tailColor">彗尾颜色（alpha 应给 0，得到线性衰减）。</param>
        public static void CometOnSegment(Vector2 from, Vector2 to, float headT, float headLen, float width,
            Color headColor, Color tailColor)
        {
            if (!Ready) return;
            Vector2 d = to - from;
            float len = d.Length();
            if (len < 0.05f) return;
            Vector2 dir = d / len;

            float headPos = MathHelper.Clamp(headT, 0f, 1f) * len;
            float tailPos = Math.Max(0f, headPos - headLen);

            Vector2 h = from + dir * headPos;
            Vector2 t = from + dir * tailPos;
            Vector2 n = new Vector2(-dir.Y, dir.X) * (width * 0.5f);

            ResetCount();
            // 头段（短、窄、亮）→ 尾段（长、宽、透明），用两段三角形带近似"两端渐细"
            Vector2 mid = Vector2.Lerp(t, h, 0.18f);
            Vector2 nHead = new Vector2(-dir.Y, dir.X) * (width * 0.25f);

            Push(h - nHead, h + nHead, mid + n, headColor, headColor, Color.Lerp(headColor, tailColor, 0.6f));
            Push(h - nHead, mid + n, mid - n, headColor, Color.Lerp(headColor, tailColor, 0.6f), Color.Lerp(headColor, tailColor, 0.6f));
            Push(mid - n, mid + n, t, Color.Lerp(headColor, tailColor, 0.6f), Color.Lerp(headColor, tailColor, 0.6f), tailColor);
            Flush(_count * 3, BlendState.Additive);
        }
    }
}
