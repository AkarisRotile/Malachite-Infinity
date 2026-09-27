// 本文件为自主实现，无外部参考代码。
//
// ============================================================================
// 星图 3.0 ·「翎羽星网」—— 星网渲染器
// ============================================================================
// 设计依据：写法\阶段4_星图3.0_翎羽星网.md §4
// 视觉规格来源：AGY 咨询稿（写法\_agy\reply_01~03），其中卡片底色/边框色违反铁律 R5
//   （颜色只许 MalachitePalette）已被替换，几何与动效参数原样采纳。
//
// 铁律：
//   · **零 Terraria 依赖**（只用 XNA + MalachitePalette）—— 与 PrimitiveVfx 同源，便于被任意宿主复用。
//   · 不切换 SpriteBatch 批次；调用方须已处于 UI 绘制流程，本函数内部负责
//     End → 图元批处理 → Begin(Additive) 的批次往返（与 VfxDraw.DrawConstellationMap 同模式）。
//   · 全部走 PrimitiveVfx 的**批处理模式**：300+ 图元收敛为 1 次 draw call。
//     （旧版每个图元各自 DrawUserPrimitives，星图一开就掉帧。）

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 加点流光状态（由 UI 层推进，渲染层只读）。
    /// <para/>语义：能量从翎心沿最短路径涌向新点亮的节点 —— 见规格 §4.3。
    /// </summary>
    public sealed class StarSweepState
    {
        /// <summary>是否正在播放。</summary>
        public bool Active;

        /// <summary>归一化坐标路径（翎心 → 目标），由 StarNetwork.PathFromRoot 给出。</summary>
        public List<Vector2> Path = new List<Vector2>(8);

        /// <summary>彗头沿路径已推进的像素距离。</summary>
        public float TraveledPx;

        /// <summary>目标是否是星核（决定落点爆闪是否叠加金色环与震屏）。</summary>
        public bool IsNucleus;

        /// <summary>落点爆闪剩余帧（&gt;0 时绘制冲击波）。</summary>
        public int BurstTimer;

        /// <summary>落点位置（归一化坐标；爆闪阶段用）。</summary>
        public Vector2 BurstPos;

        public void Reset()
        {
            Active = false;
            Path.Clear();
            TraveledPx = 0f;
            IsNucleus = false;
            BurstTimer = 0;
            BurstPos = Vector2.Zero;
        }
    }

    /// <summary>星网几何绘制。</summary>
    public static class StarWebVfx
    {
        // ==================== 规格常量（唯一出处，调参只改这里）====================

        /// <summary>星尘基准半径（px）—— 正菱形 4×4。</summary>
        public const float DustR = 3f;

        /// <summary>星宿基准半径（px）—— 正八边形 8×8。</summary>
        public const float AsterismR = 6f;

        /// <summary>星核基准半径（px）—— 四角星芒 16×16。</summary>
        public const float NucleusR = 10f;

        /// <summary>翎心基准半径（px）。</summary>
        public const float RootR = 14f;

        /// <summary>悬停放大倍率。</summary>
        public const float HoverScale = 1.25f;

        /// <summary>节点点击命中半径（px）。视觉半径 3~14px，命中要显著大于视觉。
        /// <para/>但不能太大——臂内最近节点间距实测约 25px，命中半径取 16 时判定边界落在 12.5px，
        /// 仍能点到视觉边缘（星核 10px），同时不会吃邻居的点击。</summary>
        public const float NodeHitRadius = 16f;

        // ====================================================================
        // UI 画布布局常量（**唯一出处**：游戏端 StarMapUI 与任何离屏量测都取这里）
        // ====================================================================
        //
        // 2026-09-27 血泪：这些值原先在 StarMapUI 与离屏量测侧各写一份，
        // 我改了游戏端、另一处替换又静默失败 —— 结果"量测"的一直是旧布局，白追两轮溢出。
        // **几何常量只允许存在一份**（与 SlashTuning / WingTotalFrames 同一原则）。

        /// <summary>星网画布边长（px）。在 700×500 面板中占 (40,90,340,340)。</summary>
        public const float CanvasSize = 340f;

        /// <summary>
        /// 星网中心在画布内的局部坐标（**不是几何中心**）。
        /// <para/>原因：五臂角度刻意不等距 + 每节点确定性抖动 → 墨迹中心有偏移；
        /// 且**节点视觉外延左右不对称**（实测右 28.5px / 左 14.2px）。
        /// 按外延反解：`cX ∈ [18.2 + 0.887R, 320.5 − 0.997R]`，R=152 时 = [153, 169]，取 161（左右各留 8px）；
        /// 纵向 `cY ∈ [166, 185]`，取 180。
        /// <para/>⚠ 改动 `StarWebLayout` 的角度/半径后，**必须**重新离屏量测并读包围盒（`STARWEB_BBOX`）与
        /// `STARWEB_EXTREMES` 重新反解这里，否则星网会偏出画布。
        /// </summary>
        public static readonly Vector2 CanvasCenter = new Vector2(161f, 180f);

        /// <summary>渲染半径（px）：归一化 1.0 对应多少像素。由包围盒半跨度与外延反算，见 <see cref="CanvasCenter"/>。</summary>
        public const float CanvasRenderRadius = 152f;

        /// <summary>流光推进速度（px/帧）—— 规格 §4.3。</summary>
        public const float SweepSpeedPx = 12f;

        /// <summary>彗体总长（px）。</summary>
        public const float SweepCometLen = 24f;

        /// <summary>落点爆闪持续帧数。</summary>
        public const int BurstFrames = 16;

        /// <summary>可购买状态的呼吸周期（帧，60fps → 1Hz）。</summary>
        private const float BreathePeriodFrames = 60f;

        // ==================== 坐标换算（UI 命中判定与绘制共用同一份，避免"看到点不到"）====================

        /// <summary>归一化坐标 → 屏幕坐标。</summary>
        public static Vector2 ToScreen(Vector2 center, float radius, Vector2 normalized)
            => center + normalized * radius;

        /// <summary>节点屏幕坐标。</summary>
        public static Vector2 NodeScreenPos(Vector2 center, float radius, StarNode n)
            => n == null ? center : center + n.Pos * radius;

        /// <summary>
        /// 命中判定：返回离鼠标最近的节点 id（超出命中半径则返回 null）。
        /// <para/>旧版把"轨道兜底大圆"当第二通道，半径 96px 会吞掉中心区所有点击 —— 本版**取消兜底热区**，
        /// 命中半径给到 18px（视觉半径最大 14px）已足够好点；相邻节点间距最小约 30px，不会互相吃键。
        /// </summary>
        public static string HitTest(Vector2 center, float radius, Vector2 mouseLocal)
        {
            string best = null;
            float bestSq = NodeHitRadius * NodeHitRadius;
            foreach (StarNode n in StarWebLayout.Nodes_)
            {
                float dSq = Vector2.DistanceSquared(mouseLocal, NodeScreenPos(center, radius, n));
                if (dSq > bestSq) continue;
                bestSq = dSq;
                best = n.Id;
            }
            return best;
        }

        // ==================== 主绘制 ====================

        /// <summary>
        /// 绘制整张星网。
        /// </summary>
        /// <param name="sb">SpriteBatch（函数内部会 End / 重新 Begin）。</param>
        /// <param name="center">星网中心的屏幕坐标。</param>
        /// <param name="radius">渲染半径（归一化 1.0 对应的像素）。</param>
        /// <param name="time">时间驱动（Main.GameUpdateCount）。</param>
        /// <param name="lit">已点亮节点 id 集合。</param>
        /// <param name="purchasable">当前可购买节点 id 集合（相邻 + 点数够 + 门槛达成）。</param>
        /// <param name="hoverId">悬停节点 id（可 null）。</param>
        /// <param name="view">批次矩阵（UI 传 Main.UIScaleMatrix）。</param>
        /// <param name="sweep">加点流光状态（可 null）。</param>
        public static void Draw(SpriteBatch sb, GraphicsDevice gd, Vector2 center, float radius, float time,
            ICollection<string> lit, ICollection<string> purchasable, string hoverId,
            Matrix view, StarSweepState sweep)
        {
            if (sb == null || gd == null) return;
            radius = Math.Max(1f, radius);

            sb.End();
            PrimitiveVfx.Begin(gd, view, Matrix.CreateOrthographicOffCenter(
                0f, gd.Viewport.Width, gd.Viewport.Height, 0f, 0f, 1f));

            // 一次性批处理：本函数内所有图元合并为 1 次 draw call
            PrimitiveVfx.BatchBegin();

            DrawBackdrop(center, radius, time);
            DrawEdges(center, radius, lit, hoverId);
            DrawNodes(center, radius, time, lit, purchasable, hoverId);

            if (sweep != null && sweep.Active) DrawSweep(center, radius, sweep);

            PrimitiveVfx.BatchEnd(BlendState.Additive);
            PrimitiveVfx.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, view);
        }

        // ==================== 1. 底盘 + 星尘 ====================

        private static void DrawBackdrop(Vector2 center, float radius, float time)
        {
            // 浑天仪环：只留两圈 + 八方刻度，克制（旧版三层环 + 十字主轴 + 天极核属于"堆元素"）
            Color axis = MalachitePalette.AccentCyan * 0.10f;
            PrimitiveVfx.Ring(center, radius * 1.05f, 1.2f, axis, 96);
            PrimitiveVfx.Ring(center, radius * 0.60f, 1.0f, axis * 0.65f, 72);

            for (int k = 0; k < 8; k++)
            {
                float a = MathHelper.PiOver4 * k;
                Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                PrimitiveVfx.Line(center + dir * (radius * 1.00f), center + dir * (radius * 1.05f),
                    1.4f, axis * 1.6f, axis * 1.6f);
            }

            // 背景星尘：确定性哈希，不随帧抖动
            for (int i = 0; i < 110; i++)
            {
                float h1 = Hash01(i * 12.9898f);
                float h2 = Hash01(i * 78.233f);
                float h3 = Hash01(i * 39.425f);
                Vector2 p = center + new Vector2(h1 * 2f - 1f, h2 * 2f - 1f) * radius;
                if (Vector2.Distance(p, center) > radius * 1.02f) continue;
                if (Vector2.Distance(p, center) < radius * 0.10f) continue;   // 中心留白给翎心
                PrimitiveVfx.FillCircle(p, 0.8f + 1.2f * h3,
                    MalachitePalette.AccentCyan * (0.10f + 0.14f * h3), Color.Transparent, 6);
            }
        }

        // ==================== 2. 连线（三层）====================

        private static void DrawEdges(Vector2 center, float radius, ICollection<string> lit, string hoverId)
        {
            Color skeleton = MalachitePalette.GreenDeep * 0.55f;
            Color energyBase = MalachitePalette.GreenDark * 0.70f;
            Color energyTop = MalachitePalette.GreenBright * 0.90f;
            Color hot = MalachitePalette.AccentCyan;

            foreach ((string a, string b) in StarWebLayout.EdgePairs_)
            {
                StarNode na = StarWebLayout.Get(a), nb = StarWebLayout.Get(b);
                if (na == null || nb == null) continue;

                Vector2 pa = NodeScreenPos(center, radius, na);
                Vector2 pb = NodeScreenPos(center, radius, nb);

                bool aLit = lit != null && lit.Contains(a);
                bool bLit = lit != null && lit.Contains(b);

                // 骨架：永远画，让玩家看得见"这里还有路"
                PrimitiveVfx.Line(pa, pb, 1f, skeleton, skeleton);

                if (aLit && bLit)
                {
                    // 能量线：底宽带 + 顶亮线（规格 §4.2）
                    PrimitiveVfx.Line(pa, pb, 3f, energyBase, energyBase);
                    PrimitiveVfx.Line(pa, pb, 1f, energyTop, energyTop);
                }

                // 悬停路径高亮：鼠标所在节点的**已连通连线**提亮，指示"从这里会长出去"
                if (hoverId != null && ((a == hoverId && bLit) || (b == hoverId && aLit)))
                    PrimitiveVfx.Line(pa, pb, 2f, hot, hot);
            }
        }

        // ==================== 3. 节点 ====================

        private static void DrawNodes(Vector2 center, float radius, float time,
            ICollection<string> lit, ICollection<string> purchasable, string hoverId)
        {
            float breathe = 0.47f + 0.53f * (0.5f + 0.5f * (float)Math.Sin(time * MathHelper.TwoPi / BreathePeriodFrames));

            foreach (StarNode n in StarWebLayout.Nodes_)
            {
                Vector2 p = NodeScreenPos(center, radius, n);
                bool isLit = lit != null && lit.Contains(n.Id);
                bool isHover = n.Id == hoverId;
                bool canBuy = !isLit && purchasable != null && purchasable.Contains(n.Id);
                float k = isHover ? HoverScale : 1f;

                switch (n.Kind)
                {
                    case StarKind.Root: DrawRoot(p, time, isHover); break;
                    case StarKind.Dust: DrawDust(p, k, isLit, isHover, canBuy, breathe); break;
                    case StarKind.Asterism: DrawAsterism(p, k, isLit, isHover, canBuy, breathe); break;
                    case StarKind.Nucleus: DrawNucleus(p, time, k, isLit, isHover, canBuy, breathe); break;
                }
            }
        }

        /// <summary>翎心：永远点亮，十二角芒 + 双环 + 呼吸辉光。</summary>
        private static void DrawRoot(Vector2 p, float time, bool isHover)
        {
            float pulse = 1f + 0.06f * (float)Math.Sin(time * 0.045f);
            float r = RootR * pulse * (isHover ? HoverScale : 1f);

            PrimitiveVfx.FillCircle(p, r * 2.6f, MalachitePalette.GreenBright * 0.10f, Color.Transparent, 24);
            PrimitiveVfx.StarSpikes(p, r * 2.4f, r * 0.55f, MalachitePalette.GreenBright * 0.42f, 12, time * 0.006f, 2.4f);
            PrimitiveVfx.Ring(p, r * 1.5f, 1.4f, MalachitePalette.AccentGold * 0.55f, 40);
            PrimitiveVfx.FillPolygonSoft(p, r * 0.85f, 12, time * 0.01f, MalachitePalette.PrimaryGreen, MalachitePalette.PrimaryGreen * 0.35f);
            PrimitiveVfx.FillCircle(p, r * 0.34f, MalachitePalette.TextLight * 0.95f, Color.Transparent, 12);
            if (isHover) PrimitiveVfx.Ring(p, r * 1.9f, 1.2f, MalachitePalette.AccentCyan, 40);
        }

        /// <summary>星尘：正菱形，无辉光层。</summary>
        private static void DrawDust(Vector2 p, float k, bool isLit, bool isHover, bool canBuy, float breathe)
        {
            float r = DustR * k;

            if (isLit)
            {
                PrimitiveVfx.FillCircle(p, r * 2.2f, MalachitePalette.GreenBright * 0.16f, Color.Transparent, 10);
                PrimitiveVfx.FillPolygonSoft(p, r, 4, MathHelper.PiOver4, MalachitePalette.PrimaryGreen, MalachitePalette.PrimaryGreen * 0.45f);
                PrimitiveVfx.FillCircle(p, 1.0f, MalachitePalette.TextLight * 0.85f, Color.Transparent, 6);
            }
            else
            {
                // 未点亮也保证可见（历史事故③：画太暗等于没画）
                Color body = canBuy
                    ? Color.Lerp(MalachitePalette.GreenDark, MalachitePalette.PrimaryGreen, breathe * 0.5f)
                    : MalachitePalette.GreenDark * 0.95f;
                PrimitiveVfx.FillPolygonSoft(p, r, 4, MathHelper.PiOver4, body, body * 0.5f);
                if (canBuy)
                    PrimitiveVfx.Ring(p, r * 1.9f, 1.1f, MalachitePalette.GreenBright * breathe, 12);
            }

            if (isHover) PrimitiveVfx.Ring(p, r * 2.4f, 1.1f, MalachitePalette.AccentCyan, 12);
        }

        /// <summary>星宿：正八边形，1 层辉光。</summary>
        private static void DrawAsterism(Vector2 p, float k, bool isLit, bool isHover, bool canBuy, float breathe)
        {
            float r = AsterismR * k;

            if (isLit)
            {
                PrimitiveVfx.FillCircle(p, r * 2.4f, MalachitePalette.GreenBright * 0.14f, Color.Transparent, 16);
                PrimitiveVfx.Ring(p, r * 1.85f, 1.2f, MalachitePalette.GreenBright * 0.34f, 20);
                PrimitiveVfx.FillPolygonSoft(p, r, 8, 0f, MalachitePalette.PrimaryGreen, MalachitePalette.PrimaryGreen * 0.45f);
                PrimitiveVfx.FillCircle(p, 1.2f, MalachitePalette.TextLight * 0.95f, Color.Transparent, 6);
            }
            else
            {
                Color body = canBuy
                    ? Color.Lerp(MalachitePalette.GreenDark, MalachitePalette.PrimaryGreen, breathe * 0.55f)
                    : MalachitePalette.GreenDark * 0.72f;   // 未点亮星宿压暗：实测 0.95 时亮度已接近已点亮星尘，观感倒置
                PrimitiveVfx.FillPolygonSoft(p, r, 8, 0f, body, body * 0.5f);
                if (canBuy)
                    PrimitiveVfx.Ring(p, r * 1.85f, 1.2f, MalachitePalette.GreenBright * breathe, 20);
            }

            if (isHover) PrimitiveVfx.Ring(p, r * 2.3f, 1.2f, MalachitePalette.AccentCyan, 20);
        }

        /// <summary>星核：四角星芒（底层旋转 45°）+ 双辉光；点亮后转金色。</summary>
        private static void DrawNucleus(Vector2 p, float time, float k, bool isLit, bool isHover, bool canBuy, float breathe)
        {
            float r = NucleusR * k;
            Color main = isLit ? MalachitePalette.AccentGold : MalachitePalette.PrimaryGreen;

            if (isLit)
            {
                // 光晕半径从 r*3.0 收到 r*2.3：实测臂内 D3↔星核 间距只有 23~30px（R=152 时），
                // 而 r*3.0 = 30px 的光晕会把两个节点糊成一团（离线按实际尺寸量测得出）。
                PrimitiveVfx.FillCircle(p, r * 2.3f, MalachitePalette.AccentGold * 0.14f, Color.Transparent, 22);
                PrimitiveVfx.Ring(p, r * 2.0f, 1.4f, MalachitePalette.AccentGold * 0.50f, 28);
                // holeRadius 让两层芒的根部散开：否则 16 个三角形共用中心顶点，加色叠加会把
                // 金色冲成纯白（实测峰值 (255,255,255)，读起来是死白而不是 #FFD54F）。
                PrimitiveVfx.StarSpikes(p, r * 1.9f, r * 0.34f, main, 4, 0f, 2.2f, r * 0.34f);
                PrimitiveVfx.StarSpikes(p, r * 1.35f, r * 0.26f, main * 0.75f, 4, MathHelper.PiOver4, 2.2f, r * 0.46f);
                PrimitiveVfx.FillPolygonSoft(p, r * 0.45f, 4, 0f, MalachitePalette.TextLight, main * 0.5f);
            }
            else
            {
                // ★ 2026-09-27 规格一致性修正：未点亮 = **没有辉光**（规格 §4.1 四态表）。
                //   ① 此前未点亮的星核套了一圈 AccentCyan·0.32 的环 —— 后果是"只点亮翎心的新档"
                //      一开星图就有 5 枚青色光斑，读起来像已经激活；
                //   ② 更隐蔽的一个：两层四芒共 16 个三角形共用中心顶点，加色叠加把中心钳成
                //      **纯白点**（实测 (145,255,255)）—— 未点亮的星核也"发着白光"。
                //   现在环改 GreenDeep 暗环、芒给 holeRadius 散开根部，未点亮就真的是暗的。
                Color body = canBuy
                    ? Color.Lerp(MalachitePalette.GreenDark, MalachitePalette.PrimaryGreen, breathe * 0.6f)
                    : MalachitePalette.GreenDark * 0.80f;
                PrimitiveVfx.StarSpikes(p, r * 1.7f, r * 0.30f, body, 4, 0f, 2.2f, r * 0.62f);
                PrimitiveVfx.StarSpikes(p, r * 1.2f, r * 0.22f, body * 0.7f, 4, MathHelper.PiOver4, 2.2f, r * 0.42f);
                PrimitiveVfx.Ring(p, r * 1.9f, 1.2f,
                    canBuy ? MalachitePalette.GreenBright * breathe : MalachitePalette.GreenDeep * 0.90f, 26);
            }

            if (isHover) PrimitiveVfx.Ring(p, r * 2.6f, 1.4f, MalachitePalette.AccentCyan, 30);
        }

        // ==================== 4. 加点流光 ====================

        private static void DrawSweep(Vector2 center, float radius, StarSweepState s)
        {
            List<Vector2> path = s.Path;
            if (path == null || path.Count < 2)
            {
                DrawBurst(center, radius, s);
                return;
            }

            float traveled = s.TraveledPx;

            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector2 a = ToScreen(center, radius, path[i]);
                Vector2 b = ToScreen(center, radius, path[i + 1]);
                float segLen = Vector2.Distance(a, b);
                if (segLen < 0.01f) continue;

                if (traveled >= segLen)
                {
                    // 已经完全通过的段：留下一条"已充能"的亮线
                    PrimitiveVfx.Line(a, b, 2f, MalachitePalette.GreenBright * 0.55f, MalachitePalette.GreenBright * 0.55f);
                    traveled -= segLen;
                    continue;
                }

                // 彗头落在本段
                float t = MathHelper.Clamp(traveled / segLen, 0f, 1f);
                PrimitiveVfx.CometOnSegment(a, b, t, SweepCometLen, 3f,
                    MalachitePalette.White, MalachitePalette.GreenBright * 0.0f);
                break;
            }

            DrawBurst(center, radius, s);
        }

        /// <summary>落点爆闪：半径 8→28px Ease-Out，星核额外叠金环（规格 §4.3）。</summary>
        private static void DrawBurst(Vector2 center, float radius, StarSweepState s)
        {
            if (s.BurstTimer <= 0) return;

            float t = 1f - MathHelper.Clamp(s.BurstTimer / (float)BurstFrames, 0f, 1f);
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            float r = MathHelper.Lerp(8f, 28f, ease);
            float fade = 1f - t;

            Vector2 p = ToScreen(center, radius, s.BurstPos);
            Color c = Color.Lerp(MalachitePalette.GreenBright, MalachitePalette.PrimaryGreen, t) * fade;

            PrimitiveVfx.FillCircle(p, r * 0.9f, c * 0.35f, Color.Transparent, 20);
            PrimitiveVfx.Ring(p, r, 2.4f, c, 32);
            PrimitiveVfx.Ring(p, r * 0.55f, 1.4f, c * 0.7f, 24);

            if (s.IsNucleus)
                PrimitiveVfx.Ring(p, 36f * (0.6f + 0.4f * ease), 2f, MalachitePalette.AccentGold * fade * 0.8f, 30);
        }

        // ==================== 工具 ====================

        /// <summary>确定性伪随机 [0,1)：用于星尘散布，保证不随帧抖动。</summary>
        private static float Hash01(float seed)
        {
            float v = (float)Math.Sin(seed) * 43758.5453f;
            return v - (float)Math.Floor(v);
        }
    }
}
