// 代码来源与合规署名：
// - 残影轨迹（afterimages）/ 三层光带配色（Core/Glow/Aura）思路参考：CalamityModPublic（Azafure, LLC 专有，官方允许作为开发参考）
//   与 CalamityOverhaul（MIT, (c) hocha113）CyberPrismLaserProj。
// - 纹章神圣几何 / 顿挫单翼超射曲线为本项目自主设计。
// 本文件为自主实现，未直接复制上述仓库源码。

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 全弹幕特效的<strong>纯绘制数学</strong>：零 Terraria 依赖，只用 Microsoft.Xna.Framework 的 SpriteBatch。
    /// - 出处：自各 Projectile 的 PreDraw 提取，提取后为<strong>唯一出处</strong>：UI 与弹幕共用同一份。
    /// - 铁律一：本文件<strong>不得</strong> using Terraria.*。
    /// - 铁律二：本文件<strong>不负责</strong> SpriteBatch.Begin/End 与批次混合态的切换（那需要 Main.GameViewMatrix，属宿主职责）；
    ///   调用方必须在<strong>已 Begin 的 Additive/AlphaBlend 批次内</strong>调用，坐标系为屏幕空间（世界坐标 − Main.screenPosition）。
    /// </summary>
    public static class VfxDraw
    {
        // ==================================================================
        // 1. 孔雀柳刃·普攻飞刀（MalachiteProj）：残影轨迹 + 头部三层光晕
        // ==================================================================
        public static void DrawKnifeTrail(
            SpriteBatch sb, Texture2D tex, Texture2D pixel,
            IReadOnlyList<Vector2> oldPositions, Vector2 center, Vector2 halfSize, float rotation, float scale,
            Vector2 velocity, Color main, Vector2 screenOffset)
        {
            if (sb == null) return;
            Vector2 texCenter = new Vector2(tex.Width, tex.Height) * 0.5f;

            if (oldPositions != null)
            {
                for (int i = 0; i < oldPositions.Count; i++)
                {
                    if (oldPositions[i] == Vector2.Zero) continue; // 跳过未初始化历史位，避免世界原点幽灵残影
                    float t = 1f - i / (float)oldPositions.Count;
                    Vector2 pos = oldPositions[i] + halfSize - screenOffset;
                    sb.Draw(tex, pos, null, main * (t * 0.4f), rotation, texCenter,
                        scale * (0.5f + 0.5f * t), SpriteEffects.None, 0f);
                }
            }

            Vector2 screenPos = center - screenOffset;
            sb.Draw(pixel, screenPos, null, main * 0.30f, 0f, new Vector2(0.5f), 12f, SpriteEffects.None, 0f);
            sb.Draw(pixel, screenPos, null, main * 0.75f, 0f, new Vector2(0.5f), 6f, SpriteEffects.None, 0f);
            sb.Draw(pixel, screenPos, null, Color.White * 0.9f, 0f, new Vector2(0.5f), 2.5f, SpriteEffects.None, 0f);

            // 2) 尾部短光带（CWR 三层，长度较短）
            Vector2 dir = velocity;
            if (dir.LengthSquared() < 1e-6f) dir = Vector2.UnitX; else dir.Normalize();
            float bandRot = (float)Math.Atan2(dir.Y, dir.X);
            sb.Draw(pixel, screenPos, null, main * 0.25f, bandRot, new Vector2(0f, 0.5f), new Vector2(60f, 10f), SpriteEffects.None, 0f);
            sb.Draw(pixel, screenPos, null, main * 0.6f, bandRot, new Vector2(0f, 0.5f), new Vector2(48f, 5f), SpriteEffects.None, 0f);
            sb.Draw(pixel, screenPos, null, Color.White * 0.5f, bandRot, new Vector2(0f, 0.5f), new Vector2(38f, 2f), SpriteEffects.None, 0f);
        }

        // ==================================================================
        // 2. 射线（MalachiteBolt）：残影光带 + 头部三层光晕 + 沿速度方向三层光带
        // ==================================================================
        public static void DrawBoltRay(
            SpriteBatch sb, Texture2D tex, Texture2D pixel,
            IReadOnlyList<Vector2> oldPositions, Vector2 center, Vector2 halfSize, float rotation, float scale,
            Vector2 velocity, Color cyan, Vector2 screenOffset)
        {
            if (sb == null) return;
            Vector2 texCenter = new Vector2(tex.Width, tex.Height) * 0.5f;
            Vector2 screenPos = center - screenOffset;

            if (oldPositions != null)
            {
                for (int i = 0; i < oldPositions.Count; i++)
                {
                    if (oldPositions[i] == Vector2.Zero) continue;
                    float t = 1f - i / (float)oldPositions.Count;
                    Vector2 pos = oldPositions[i] + halfSize - screenOffset;
                    sb.Draw(tex, pos, null, cyan * (t * 0.45f), rotation, texCenter,
                        scale * (0.5f + 0.5f * t), SpriteEffects.None, 0f);
                }
            }

            Color auraCol = cyan * 0.30f;
            Color glowCol = cyan * 0.75f;
            sb.Draw(pixel, screenPos, null, auraCol, 0f, new Vector2(0.5f), 16f, SpriteEffects.None, 0f);
            sb.Draw(pixel, screenPos, null, glowCol, 0f, new Vector2(0.5f), 8f, SpriteEffects.None, 0f);
            sb.Draw(pixel, screenPos, null, Color.White * 0.9f, 0f, new Vector2(0.5f), 3f, SpriteEffects.None, 0f);

            Vector2 dir = velocity;
            if (dir.LengthSquared() < 1e-6f) dir = Vector2.UnitX; else dir.Normalize();
            float bandRot = (float)Math.Atan2(dir.Y, dir.X);
            sb.Draw(pixel, screenPos, null, auraCol, bandRot, new Vector2(0f, 0.5f), new Vector2(140f, 14f), SpriteEffects.None, 0f);
            sb.Draw(pixel, screenPos, null, glowCol, bandRot, new Vector2(0f, 0.5f), new Vector2(120f, 7f), SpriteEffects.None, 0f);
            sb.Draw(pixel, screenPos, null, Color.White * 0.55f, bandRot, new Vector2(0f, 0.5f), new Vector2(100f, 2.5f), SpriteEffects.None, 0f);
        }

        // ==================================================================
        // 3. 领域展开（MalachiteDomain）：呼吸缩放法阵 + 中心光点
        // ==================================================================
        /// <summary>贴图基准半径（MalachiteDomainTex 为 256px = 半径 128）。</summary>
        public const float DomainArtRadius = 128f;

        public static void DrawDomain(
            SpriteBatch sb, Texture2D tex, Texture2D pixel, Vector2 center, Vector2 screenOffset,
            float radius, float wrappedTime, float expandK, float alpha, Color bright)
        {
            if (sb == null) return;

            float breathe = 1f + 0.04f * (float)Math.Sin(wrappedTime * 2f);
            float scale = (radius / DomainArtRadius) * breathe;
            float rotation = wrappedTime * 0.25f;
            if (expandK < 1f) scale *= Math.Max(0f, expandK);

            Vector2 screenPos = center - screenOffset;
            sb.Draw(tex, screenPos, null, Color.White * (0.55f * alpha), rotation,
                new Vector2(tex.Width, tex.Height) * 0.5f, scale, SpriteEffects.None, 0f);
            sb.Draw(pixel, screenPos, null, bright * (0.8f * alpha), 0f,
                new Vector2(0.5f), 6f, SpriteEffects.None, 0f);
        }

        // ==================================================================
        // 4. 空间纹章（EsCrestSigilProj）：5 层护眼神圣几何
        // ==================================================================
        public static void DrawCrestSigil(
            SpriteBatch sb, Texture2D flareTex, Texture2D bloomTex, Vector2 drawPos,
            int age, int delay, int linger, float totalScale, float aspectX, float aspectY, float gameUpdateCount,
            Color greenBase, Color goldBase, Color cyanBase, int tier)
        {
            if (sb == null) return;

            Vector2 origin = new Vector2(flareTex.Width, flareTex.Height) * 0.5f;
            Vector2 bloomOrigin = new Vector2(bloomTex.Width, bloomTex.Height) * 0.5f;

            delay = Math.Max(1, delay);
            float chargeProgress = Math.Clamp(age / (float)Math.Max(1, delay - 2), 0f, 1f);
            float postBurstProgress = Math.Clamp((age - delay) / 9f, 0f, 1f);

            // 缩放曲线：成形 0.6→1.0；引爆前 2 帧向心坍缩(-18%)；引爆后膨胀 1.0→1.7 消散；切割期维持满形
            float animScale, alpha;
            if (linger > 0 && age > delay && age <= delay + linger)
            {
                animScale = 1f + 0.08f * (float)Math.Sin(gameUpdateCount * 0.9f);
                alpha = 0.85f;
            }
            else if (age < delay - 2)
            {
                animScale = MathHelper.Lerp(0.6f, 1.0f, chargeProgress);
                alpha = MathHelper.Lerp(0.2f, 1f, chargeProgress);
            }
            else if (age < delay)
            {
                animScale = 0.82f;
                alpha = 1f;
            }
            else
            {
                animScale = MathHelper.Lerp(1.0f, 1.7f, postBurstProgress);
                alpha = 1f - postBurstProgress;
            }
            if (delay <= 2 && age == delay) { animScale = 1f; alpha = 1f; }

            float S = totalScale;
            float rr = animScale * S;
            Color green = greenBase * alpha;
            Color gold = goldBase * alpha;
            Color cyan = cyanBase * alpha;
            Color mid = tier >= 1 ? Color.Lerp(green, cyan, 0.55f) : green;
            Color coreWhite = Color.White * (alpha * 0.9f);

            // 旋转相位（C·节奏）：小纹章保持原版线性；大纹章改"成形期快转 → 落定后慢转"的 ease-out
            float spin = tier >= 1 ? SpinPhase(age, 8f, 0.10f, 0.035f) : age * 0.05f;

            // ① 底层柔和空间能量光晕 —— **已移除**
            // 2026-09-25 用户要求：纹章中心不要绘制等比放大的贴图。
            // 此处原为 `sb.Draw(bloomTex, drawPos, ..., animScale * 1.5f * S, ...)`：
            // 把 Extra_89 那枚竖直椭圆柔光在中心放大 1.5 倍铺开，是中心最扎眼的一坨。
            // 体量感改由 ②③ 的芒环 + ⑤⑦ 的几何环承担，中心保持留空。

            // ② 符文刻度环
            if (tier >= 1)
            {
                // 大纹章：外环 12 长芒（逆时针）+ 内环 24 短芒（顺时针）—— 长短交错、双向自转，从"光点"变"法阵"
                DrawRadialRing(sb, flareTex, origin, drawPos, -spin * 0.60f, 12, 54f * rr, 30f * rr, 4.5f * rr, gold * 0.50f);
                DrawRadialRing(sb, flareTex, origin, drawPos, spin * 0.45f, 24, 39f * rr, 15f * rr, 3.0f * rr, mid * 0.40f);
            }
            else
            {
                // 小纹章：保留原版 12 芒光点环（现状，一字未改）
                float ringRot = -age * 0.03f;
                for (int i = 0; i < 12; i++)
                {
                    float a = ringRot + MathHelper.TwoPi * i / 12f;
                    Vector2 markerPos = drawPos + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * (52f * animScale * S);
                    sb.Draw(bloomTex, markerPos, null, gold * 0.45f, a, bloomOrigin, animScale * 0.12f * S, SpriteEffects.None, 0f);
                }
            }

            // ③ 大纹章专属：同心几何环（六边环 + 反向三边环）
            //    用"环"而不是"辐条"——辐条会穿过圆心，三条叠加就在核心叠出白斑，直接踩护眼铁律
            if (tier >= 1)
            {
                DrawPolygonRing(sb, flareTex, origin, drawPos, spin * 0.25f, 6, 62f * rr, 3.2f * rr, mid * 0.42f);
                DrawPolygonRing(sb, flareTex, origin, drawPos, -spin * 0.40f + 0.5f, 3, 78f * rr, 2.6f * rr, gold * 0.34f);
            }

            // ④ 双重正交反向自转菱形
            float rot1 = spin;
            float rot2 = -spin + MathHelper.PiOver4;
            // 泪形长轴在 Y：X 分量给"粗细"、Y 分量给"长度"，整体再转 -90° 让第一个尖朝水平
            Vector2 diamondScaleA = new Vector2(0.42f * aspectY, 1.25f * aspectX) * animScale * S;
            Vector2 diamondScaleB = new Vector2(0.35f * aspectY, 1.05f * aspectX) * animScale * S;
            const float D = -MathHelper.PiOver2;
            sb.Draw(flareTex, drawPos, null, green * 0.85f, rot1 + D, origin, diamondScaleA, SpriteEffects.None, 0f);
            sb.Draw(flareTex, drawPos, null, green * 0.85f, rot1 + D + MathHelper.PiOver2, origin, diamondScaleA, SpriteEffects.None, 0f);
            sb.Draw(flareTex, drawPos, null, gold * 0.70f, rot2 + D, origin, diamondScaleB, SpriteEffects.None, 0f);
            sb.Draw(flareTex, drawPos, null, gold * 0.70f, rot2 + D + MathHelper.PiOver2, origin, diamondScaleB, SpriteEffects.None, 0f);

            // ④ 引爆后的扩散表现
            // 2026-09-25 用户要求：**纹章中心不要绘制等比放大的贴图**。
            // 此处原先在中心画了两样东西，均已移除：
            //   · flareTex 的四片十字切痕（菱形，中心等比放大）
            //   · bloomTex 的 animScale×1.9 大柔光（引爆时最大，最扎眼的那一大坨）
            // 扩散感改由 ⑤ 的环（DrawPolygonRing 类几何）承担，那走的是"细芒拼环"，不是放大贴图。
            if (age >= delay)
            {
                const float H = -MathHelper.PiOver2;
                float crossScale = MathHelper.Lerp(1.5f, 2.6f, postBurstProgress);
                float crossAlpha = (1f - postBurstProgress) * 0.55f;
                for (int k = 0; k < 2; k++)
                {
                    float rot = k == 0 ? H : 0f;
                    float rr2 = crossScale * 2.2f * (k == 0 ? aspectX : aspectY) * S;
                    DrawPolygonRing(sb, flareTex, origin, drawPos, rot, 6, rr2, 2.2f * S, mid * crossAlpha * 0.9f);
                }
            }

            // ⑦ 大纹章专属：聚能吸入光丝（成形期由外向内收拢，让"凝聚"这件事被看见）
            if (tier >= 1 && age < delay - 2 && chargeProgress > 0.04f)
            {
                const int n = 6;
                for (int i = 0; i < n; i++)
                {
                    float a = MathHelper.TwoPi * i / n + age * 0.06f;
                    float r = MathHelper.Lerp(82f, 20f, chargeProgress) * rr;
                    float len = MathHelper.Lerp(30f, 9f, chargeProgress) * rr;
                    Vector2 sp = drawPos + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * r;
                    DrawSpike(sb, flareTex, origin, sp, a, len, 4.0f * rr, mid * (0.16f + 0.34f * (1f - chargeProgress)));
                }
            }

            // ⑧ 大纹章专属：晶化碎片芒（引爆帧与次帧向外炸开，次帧腰斩 → "碎裂→即刻消散"的顿挫）
            //    护眼做法：碎片锚定在 48×rr 的外圈、只朝外伸展，天然不进入核心区
            if (tier >= 1 && (age == delay || age == delay + 1))
            {
                float fa = age == delay ? 1f : 0.42f;
                const int n = 12;
                for (int i = 0; i < n; i++)
                {
                    float a = MathHelper.TwoPi * i / n + 0.37f * (float)Math.Sin(i * 3.1f);
                    float len = (34f + 26f * (float)Math.Abs(Math.Sin(i * 1.7f))) * rr;
                    Vector2 sp = drawPos + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * (48f * rr);
                    DrawSpike(sb, flareTex, origin, sp, a, len, 5f * rr, (i % 2 == 0 ? gold : mid) * (0.55f * fa));
                }
            }

            // ⑨ 核心 —— **已移除**
            // 2026-09-25 用户要求：纹章中心不要绘制等比放大的贴图。
            // 此处原为 `sb.Draw(bloomTex, drawPos, ..., animScale * 0.22f * S, ...)`，
            // 即把 Extra_89(ThePerfectGlow) 那枚竖直椭圆柔光在中心等比放大 —— 就是用户反复指出的"中心丑东西"。
            // 中心现在完全留空：芒环与几何环自然围出一个圆心，不需要再垫一层贴图。
        }

        // ==================== 纹章绘制辅助（大纹章专属几何）====================

        /// <summary>
        /// 旋转相位（ease-out 落定）：起始角速度 fastRate，随时间指数衰减并平滑收敛到 slowRate。
        /// age=0 时相位恰为 0；长时间后趋近 "slowRate × age + 常数"，即转为缓慢匀速自转。
        /// 相比线性 age×rate，成形期"抢一步"、落定后"稳住"，正是 C 想要的顿挫节奏。
        /// </summary>
        /// <summary>旋转相位：成形期快转 → 落定后慢转的 ease-out（大纹章用）。</summary>
        private static float SpinPhase(int age, float settleFrames, float fastRate, float slowRate)
        {
            float a = Math.Max(0, age);
            float s = Math.Max(0.01f, settleFrames);
            return slowRate * a + (fastRate - slowRate) * s * (1f - (float)Math.Exp(-a / s));
        }

        // ==================================================================
        // 4b. 待机纹章（UI 入口用）：常驻旋转、无生命周期
        // ==================================================================

        /// <summary>
        /// 待机纹章：给 UI 入口（左下角星图按钮）用的**常驻旋转**纹章。
        /// <para/>与 <see cref="DrawCrestSigil"/> 的区别：那个是弹幕生命周期驱动（成形/引爆/消散），
        /// 这个没有生命周期、永远处于"成形完成"态，旋转由 time 持续驱动，并带呼吸与悬停增强。
        /// </para>
        /// <para/>铁律：**不切换 SpriteBatch 批次**（调用方须已处于 Additive 批次内）——
        /// UI 绘制流对批次极敏感，由调用方统一负责 Begin/End 比在这里隐式切换安全得多。
        /// </para>
        /// </summary>
        /// <param name="center">屏幕坐标下的纹章中心。</param>
        /// <param name="time">时间驱动（传 Main.GameUpdateCount 即可）。</param>
        /// <param name="radiusScale">整体大小（1.0 ≈ 小纹章档）。</param>
        /// <param name="hover">悬停增强（0=常态，1=悬停；放大并提亮）。</param>
        public static void DrawIdleSigil(
            SpriteBatch sb, Texture2D flareTex, Texture2D bloomTex, Texture2D pixelTex, Vector2 center,
            float time, float radiusScale, float hover,
            Color greenBase, Color goldBase, Color cyanBase)
        {
            if (sb == null || flareTex == null || bloomTex == null || pixelTex == null) return;

            hover = MathHelper.Clamp(hover, 0f, 1f);
            float breathe = 1f + 0.045f * (float)Math.Sin(time * 0.045f);   // 呼吸
            float S = Math.Max(0.05f, radiusScale) * breathe * (1f + 0.16f * hover);
            float boost = 1f + 0.30f * hover;                              // 悬停提亮（过强会把中心叠成白团）

            Vector2 origin = new Vector2(flareTex.Width, flareTex.Height) * 0.5f;
            Vector2 bloomOrigin = new Vector2(bloomTex.Width, bloomTex.Height) * 0.5f;

            Color green = greenBase * boost;
            Color gold = goldBase * boost;
            Color mid = Color.Lerp(greenBase, cyanBase, 0.55f) * boost;

            // ① 底层柔和空间光晕 —— 已移除。
            // ②③ 芒环：**中心位置一笔都不画**。
            //   用户提供的中心特写显示：中心有一块实心暗竖条 + 一个发亮小方块。
            //   竖条 = 内圈 18 根芒的基座在中心重叠堆出来的；方块 = 中心那个像素点。
            //   修法：中心（含原 ①⑥ 两笔）完全留空；芒的 radius 外推到 ≥ lenPx/2 + 空腔，
            //   两圈芒都不再侵入中心区域。
            //   注：径向芒是"以 radius 为中心、两侧各延伸 len/2"的泪形，故 ② 的内端点在
            //   r = 62 − 10 = 52 —— **r<52 曾经一个像素都没有**，视觉上是个空心甜甜圈。
            //   2026-09-27 用户反馈"内部太空、不好看"，由 ⑤⑥⑦ 三笔填充（见下）。
            DrawRadialRing(sb, flareTex, origin, center, -time * 0.020f, 12, 86f * S, 40f * S, 7.0f * S, gold * 1.00f);
            DrawRadialRing(sb, flareTex, origin, center, time * 0.034f, 18, 62f * S, 20f * S, 4.2f * S, mid * 0.72f);

            // ④ 同心几何环（六边 + 反向三边）
            DrawPolygonRing(sb, flareTex, origin, center, time * 0.013f, 6, 64f * S, 3.4f * S, mid * 0.65f);
            DrawPolygonRing(sb, flareTex, origin, center, -time * 0.021f + 0.5f, 3, 80f * S, 2.8f * S, gold * 0.56f);

            // ================= ⑤⑥⑦ 内部填充（2026-09-27 新增）====================
            // 设计依据：用户 2026-09-27 拍板"丰富内部但别太复杂"。
            //   · 对称数选 **4 与 8**，刻意避开现有的 3 / 6 / 12 / 18 —— 否则所有笔画会周期性对齐成一个大十字。
            //   · 新墨迹集中在 r = 6~42，并留出 r = 42~52 作透气缓冲，既填掉空腔又不与外圈挤在一起。

            // ⑤ 内环：正方形环（4 边）。旋转速度与前四笔都不同（-0.026），进一步打散对齐周期。
            DrawPolygonRing(sb, flareTex, origin, center, -time * 0.026f + 0.30f, 4, 36f * S, 2.2f * S, gold * 0.75f);

            // ⑥ 内芒：8 根短芒，覆盖 r = 15~33，与 ⑤ 的四边形边（r 25.5~36）交错咬合。
            DrawRadialRing(sb, flareTex, origin, center, time * 0.029f + 0.40f, 8, 24f * S, 18f * S, 3.0f * S, green * 0.60f);

            // ⑦ 中央四芒小星（**不用贴图**）。
            //   2026-09-27 实测更正：项目一直把 TextureAssets.Extra[ThePerfectGlow] 当"圆形柔光"用，
            //   但导出原版贴图逐像素比对后确认 —— **Extra_89 与 Extra_98 字节完全相同**，
            //   都是竖直梭形（全 alpha 平台 宽10 × 长42 纹理像素）。拿它画"圆心"只会得到一根竖条，
            //   正是 2026-09-25 用户要求删掉的那类"中心丑东西"。
            //   故改为 4 根短芒组成的**小四芒星**：radius = len/2 使芒自圆心向外长、不穿过中心；
            //   中心只有 4 个图元，不糊白；且与「翎羽星网」的星形主题一致。
            //   悬停时这一笔提亮 —— 让 hover 有个"核心亮起"的质变，而不只是整体亮一点点。
            DrawRadialRing(sb, flareTex, origin, center, time * 0.017f, 4, 10f * S, 20f * S, 5.0f * S,
                Color.Lerp(goldBase, Color.White, 0.25f) * (0.62f + 0.30f * hover));
        }

        // ==================================================================
        // 4c. 「力」页星图 —— 真实星座
        // ==================================================================
        //
        // 设计教训（2026-09-25，用户两次指正）：
        //   初版把五轨画成从中心放射的五条直线 —— 用户："哪有星座是直着的五条线"，那是风车不是星座。
        //   二版改成放射臂后又把纹章的芒环/几何环/菱形/晶核全都摆上去 —— 用户："没让你把纹章元素疯狂复用堆叠屎山"。
        // 本版原则：**星座本体是主角，纹章只提供视觉语言**（翡翠绿/金配色、bloom 辉光质感、护眼用色、
        // 亮星带一点菱芒呼应纹章的几何感），**不再把纹章的元素逐个搬过来堆**。
        //
        // 形状来源：Core\Vfx\ConstellationMap.cs 的真实赤经赤纬投影，不手摆位置。

        /// <summary>
        /// 绘制「力」页星图：五个真实星座，每个星座代表一轨。
        /// <para/>进度表达：**星等越亮 = 等级越高**——加点时该星座最亮的星先点亮，高等级才轮到暗星。
        /// 未点亮的星以极暗的残星显示（保留"这里还有位置"的信息）。
        /// </para>
        /// <para/>铁律：**不切换 SpriteBatch 批次**（调用方须已处于 Additive 批次内）。
        /// </summary>
        /// <param name="center">星图中心（屏幕坐标）。</param>
        /// <param name="time">时间驱动（Main.GameUpdateCount）。</param>
        /// <param name="scale">整体缩放（局部归一化域 -1~1 → 屏幕像素的半径）。</param>
        /// <param name="levels">五轨已投入等级。</param>
        /// <param name="caps">五轨当前等级上限。</param>
        /// <param name="hoverIndex">高亮的星座序号（-1 = 无，跟随鼠标悬停的轨道）。</param>
        /// <param name="skillTreeStyle">
        /// true = **技能树风**（节点带外环、连线明亮、状态强对比）；
        /// false = **天文星图风**（星等分级、亮星带十字芒、连线克制、背景星尘）。
        /// 两种风格都保留，供对照选用——成熟参考分别是 RPG 星座式技能树 UI 与天文星图制图规范。
        /// </param>
        public static void DrawConstellationMap(
    SpriteBatch sb, GraphicsDevice gd, Vector2 center,
    float time, float scale, int[] levels, int[] caps, int hoverIndex,
    Color greenBase, Color goldBase, Color cyanBase, Matrix view, bool skillTreeStyle = false)
{
    if (sb == null || gd == null || levels == null || caps == null) return;
    int n = ConstellationMap.Count;
    if (levels.Length < n || caps.Length < n) return;

    scale = Math.Max(1f, scale);

    sb.End();
    PrimitiveVfx.Begin(gd, view, Matrix.CreateOrthographicOffCenter(
        0f, gd.Viewport.Width, gd.Viewport.Height, 0f, 0f, 1f));

    // 1. 浑天仪底盘：同心刻度环 + 十字主轴 + 八方刻度 + 天极核
    // 【第二轮调亮】原 alpha 0.08 在黑底上线性叠加后有效亮度不足 15/255 —— 等于没画。
    // 现提到 0.24/0.14 并补上刻度与极核，让它读起来像"发光的仪器"而不是背景污渍。
    Color axisCol = cyanBase * 0.24f;
    Color subCol = cyanBase * 0.14f;
    PrimitiveVfx.Ring(center, scale * 0.95f, 1.8f, axisCol, 72);
    PrimitiveVfx.Ring(center, scale * 0.65f, 1.2f, subCol, 54);
    PrimitiveVfx.Ring(center, scale * 0.35f, 1.0f, subCol * 0.8f, 36);
    PrimitiveVfx.Line(center - new Vector2(scale * 0.98f, 0), center + new Vector2(scale * 0.98f, 0), 1.2f, axisCol * 0.7f, axisCol * 0.7f);
    PrimitiveVfx.Line(center - new Vector2(0, scale * 0.98f), center + new Vector2(0, scale * 0.98f), 1.2f, axisCol * 0.7f, axisCol * 0.7f);

    // 八方刻度（外环上向内的一小段短线，刻度感）
    for (int k = 0; k < 8; k++)
    {
        float ang = k * MathHelper.PiOver4;
        Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
        PrimitiveVfx.Line(center + dir * (scale * 0.92f), center + dir * (scale * 0.98f), 1.5f, axisCol, axisCol);
    }
    PrimitiveVfx.GlowDot(center, scale * 0.030f, cyanBase, 0.40f, 2);

    // 2. 背景微星尘（同步提亮）
    if (!skillTreeStyle)
    {
        for (int i = 0; i < 90; i++)
        {
            float h1 = Hash01(i * 12.9898f), h2 = Hash01(i * 78.233f), h3 = Hash01(i * 39.425f);
            Vector2 dp = center + new Vector2(h1 * 2f - 1f, h2 * 2f - 1f) * scale;
            if (Vector2.Distance(dp, center) > scale * 0.92f) continue;
            PrimitiveVfx.FillCircle(dp, (1.0f + 1.4f * h3) * (scale / 300f), cyanBase * (0.18f + 0.22f * h3), Color.Transparent, 8);
        }
    }

    // 3. 星座遍历绘制
    for (int ci = 0; ci < n; ci++)
    {
        Constellation c = ConstellationMap.All[ci];
        bool hot = ci == hoverIndex;
        int cap = Math.Max(0, caps[ci]);
        int lv = Math.Clamp(levels[ci], 0, Math.Max(0, cap));

        Vector2[] shape = ConstellationMap.LocalShape(c);
        Vector2 anchor = center + c.Anchor * scale;
        float cs = c.Scale * scale * 0.42f;
        int starCount = c.Stars.Length;

        int litCount = ConstellationMap.LitCount(c, lv, cap);
        int[] order = ConstellationMap.BrightestFirst(c);
        bool[] lit = new bool[starCount];
        int newestStar = -1;
        for (int k = 0; k < litCount && k < order.Length; k++)
        {
            lit[order[k]] = true;
            newestStar = order[k];
        }

        // 星座局部星云底色（大幅收敛过曝，烘托整体形体）
        float fieldAlpha = hot ? 0.08f : (litCount > 0 ? 0.04f : 0.015f);
        PrimitiveVfx.FillCircle(anchor, cs * 1.6f, (hot ? goldBase : greenBase) * fieldAlpha, Color.Transparent, 24);

        // 连线：底虚线（骨架）+ 点亮实线
        for (int L = 0; L + 1 < c.Links.Length; L += 2)
        {
            int a = c.Links[L], b = c.Links[L + 1];
            if (a < 0 || a >= starCount || b < 0 || b >= starCount) continue;
            Vector2 pa = anchor + shape[a] * cs, pb = anchor + shape[b] * cs;

            bool isLinkLit = lit[a] && lit[b];
            float w = (skillTreeStyle ? 1.8f : 1.2f) * (scale / 300f) * 1.5f;
            
            // 常驻暗线构建轮廓认知；点亮则叠加亮线
            // 【第二轮调亮】0.12 在"全零"格子里几乎看不出轮廓，提到 0.24；悬停再提。
            PrimitiveVfx.Line(pa, pb, w * 0.8f, cyanBase * (hot ? 0.34f : 0.24f), cyanBase * (hot ? 0.34f : 0.24f));
            if (isLinkLit || hot)
            {
                Color lCol = hot ? Color.Lerp(greenBase, Color.White, 0.4f) : greenBase;
                PrimitiveVfx.Line(pa, pb, w, lCol * 0.62f, lCol * 0.62f);
            }
        }

        // 恒星节点
        for (int si = 0; si < starCount; si++)
        {
            Vector2 pos = anchor + shape[si] * cs;
            float lum = ConstellationMap.Brightness(c.Stars[si].Mag);
            float baseR = ((skillTreeStyle ? 3.5f : 2.5f) + 4.5f * lum) * (scale / 300f) * 2.2f;
            float r = baseR * (1f + 0.04f * (float)Math.Sin(time * 0.03f + si * 1.5f + ci));

            bool on = lit[si] || hot;
            bool isNewest = si == newestStar;
            Color nodeCol = isNewest ? goldBase : greenBase;

            if (on)
            {
                // 控制星芒尺度：抑制长度，提升高级感，杜绝白芒糊死
                if (lum > 0.4f)
                {
                    float spikeLen = r * (1.4f + lum * 1.2f);
                    float spikeW = Math.Max(1.0f, r * 0.35f);
                    PrimitiveVfx.StarSpikes(pos, spikeLen, spikeW, nodeCol * 0.35f, 4, 0.785f, 2.2f);
                }

                // 核心星核：翡翠绿/金为绝对主导，微量白芯
                // 【第二轮】外圈辉光再加一层更大的柔晕，找回"星辉感"（原只有 1.8× 一层，显得干）
                PrimitiveVfx.FillCircle(pos, r * 3.0f, nodeCol * 0.10f, Color.Transparent, 18);
                PrimitiveVfx.FillCircle(pos, r * 1.8f, nodeCol * 0.28f, Color.Transparent, 16);
                PrimitiveVfx.FillCircle(pos, r * 0.75f, nodeCol * 0.95f, nodeCol * 0.3f, 16);
                PrimitiveVfx.FillCircle(pos, r * 0.28f, Color.White * 0.6f, nodeCol * 0.8f, 10);

                // 2026-09-25 用户要求：删除节点外的**方框**（原为 4 边形 PolygonRing，看起来像打了方格子）。
                // skillTreeStyle 的节点现在与星图风一致，只保留星核本身。
            }
            else
            {
                // 未点亮星：【第二轮】0.22/0.3 太弱，提到 0.34/0.42 让它读得出"这里有一颗星"
                PrimitiveVfx.Ring(pos, r * 0.6f, 0.9f, cyanBase * (hot ? 0.48f : 0.34f), 14);
                PrimitiveVfx.FillCircle(pos, r * 0.22f, cyanBase * 0.42f, Color.Transparent, 8);
            }
        }
    }

    PrimitiveVfx.End();
    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
        DepthStencilState.None, RasterizerState.CullNone, null, view);
}

        /// <summary>
        /// 渲染状态用的状态：星图绘制前会被 End 掉，画完必须**还原成 Additive** ——
        /// 调用方（UI / 弹幕）后续的发光层都依赖加色混合。此处曾一度写成 AlphaBlend，那会让整个星图失去发光。
        /// </summary>
        private static readonly BlendState SigilRestoreBlend = BlendState.Additive;

        /// <summary>取小数部分（确定性伪随机辅助）。</summary>
        private static float Frac01(float v) => v - (float)Math.Floor(v);

        /// <summary>确定性伪随机 [0,1)：用于星尘散布，保证不随帧抖动。</summary>
        private static float Hash01(float seed)
        {
            float v = (float)Math.Sin(seed) * 43758.5453f;
            return v - (float)Math.Floor(v);
        }



        // ==================================================================
        // 5. 正多边形环 / 径向芒环 / 细芒（各纹章共用的几何零件）
        // ==================================================================

        /// <summary>径向芒环：n 条细芒沿圆周均匀排布，自转由 rot 驱动。</summary>
        private static void DrawRadialRing(SpriteBatch sb, Texture2D flareTex, Vector2 origin, Vector2 center,
            float rot, int n, float radius, float lenPx, float thickPx, Color color)
        {
            for (int i = 0; i < n; i++)
            {
                float a = rot + MathHelper.TwoPi * i / n;
                Vector2 at = center + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * radius;
                DrawSpike(sb, flareTex, origin, at, a, lenPx, thickPx, color);
            }
        }

        /// <summary>正多边形环（边为细芒）：环不穿过圆心，因此不会在核心叠亮（护眼）。</summary>
        private static void DrawPolygonRing(SpriteBatch sb, Texture2D flareTex, Vector2 origin, Vector2 center,
            float rot, int sides, float radius, float thickPx, Color color)
        {
            for (int k = 0; k < sides; k++)
            {
                float a0 = rot + MathHelper.TwoPi * k / sides;
                float a1 = rot + MathHelper.TwoPi * (k + 1) / sides;
                Vector2 p0 = center + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * radius;
                Vector2 p1 = center + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * radius;
                Vector2 d = p1 - p0;
                if (d.Length() < 1f) continue;
                DrawSpike(sb, flareTex, origin, (p0 + p1) * 0.5f, (float)Math.Atan2(d.Y, d.X), d.Length(), thickPx, color);
            }
        }

        /// <summary>
        /// 单条细芒：把原版 SharpTears 泪形菱芒按"长 lenPx、粗 thickPx"沿 angle 方向铺开。
        /// <para>
        /// 关键（2026-09-18 修正）：原版 Extra[98] SharpTears 是一枚<strong>竖直</strong>的泪形——它的尖在 <strong>Y 轴</strong>，
        /// 宽度在 X 轴。此前一律按"长轴在 X"去拉伸（X 给长度、Y 给粗细），结果是把泪形的<strong>两个尖头直接压没了</strong>，
        /// 只剩中间那段方方正正的身子——这正是"纹章看起来太方"的根因。
        /// 现约定：长度给 Y、粗细给 X，并把贴图整体旋转 -90°，使本地 +Y 对齐 angle 方向。
        /// </para>
        /// </summary>
        private static void DrawSpike(SpriteBatch sb, Texture2D flareTex, Vector2 origin, Vector2 at,
            float angle, float lenPx, float thickPx, Color color)
        {
            if (lenPx < 1f) return;
            sb.Draw(flareTex, at, null, color, angle - MathHelper.PiOver2, origin,
                new Vector2(Math.Max(1f, thickPx) / flareTex.Width, lenPx / flareTex.Height), SpriteEffects.None, 0f);
        }

        // ==================================================================
        // 5. 顿挫单翼（EsCrestWingsProj）：超射弹簧弹开 + 呼吸浮动 + 星轨连线
        // ==================================================================
        public static void DrawStaccatoMonowing(
            SpriteBatch sb, Texture2D flareTex, Texture2D bloomTex, Texture2D pixel, Vector2 rootPos,
            int direction, int tier, float timer, float snapFrames, float maxLifetime, float sizeMult,
            Color emeraldBase, Color goldBase, Vector2[] plumeTipsOut = null)
        {
            if (sb == null) return;

            Vector2 origin = new Vector2(flareTex.Width, flareTex.Height) * 0.5f;
            Vector2 bloomOrigin = new Vector2(bloomTex.Width, bloomTex.Height) * 0.5f;
            int dir = direction >= 0 ? 1 : -1;

            float alpha = 1f;
            if (timer < 3f) alpha = timer / 3f;
            else if (timer > maxLifetime - 8) alpha = (maxLifetime - timer) / 8f;

            Color cEmerald = emeraldBase * (alpha * 0.85f);
            Color cGold = goldBase * (alpha * 0.75f);
            Color cLaserWhite = Color.White * (alpha * 0.90f);

            // 超射顿挫张开算法（强弹簧）：2~4 帧内冲过 125% 再回弹锁死
            float snapP = Math.Clamp(timer / Math.Max(1f, snapFrames), 0f, 1f);
            float snapCurve = (float)(1f + 2.9f * Math.Pow(snapP - 1f, 3f) + 1.9f * Math.Pow(snapP - 1f, 2f));
            snapCurve = Math.Clamp(snapCurve, 0f, 1.25f);

            // 1. 背部半月圣环
            float haloScale = MathHelper.Clamp(timer / 4f, 0f, 1f) * (tier == 0 ? 0.65f : 1.1f) * sizeMult;
            float haloBaseAngle = dir == 1 ? MathHelper.Pi * 0.75f : MathHelper.Pi * 0.25f;

            sb.Draw(bloomTex, rootPos, null, cEmerald * 0.5f, 0f, bloomOrigin, haloScale, SpriteEffects.None, 0f);
            int haloDots = tier == 0 ? 4 : 7;
            for (int h = 0; h < haloDots; h++)
            {
                float ang = haloBaseAngle + (h - haloDots / 2) * 0.24f;
                Vector2 nodePos = rootPos + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * (36f * haloScale);
                sb.Draw(bloomTex, nodePos, null, cGold * 0.6f, ang, bloomOrigin, 0.12f * haloScale, SpriteEffects.None, 0f);
            }

            // 2. 单侧阶梯晶刃群（与常驻光翼共用同一份实现 → 两者形状完全一致）
            int plumeCount = tier == 0 ? 4 : 7;
            float maxLen = (tier == 0 ? 95f : 195f) * snapCurve * sizeMult;
            float wingRootAngle = tier == 0
                ? (dir == 1 ? MathHelper.Pi * 0.72f : MathHelper.Pi * 0.28f)
                : (dir == 1 ? -MathHelper.Pi * 0.72f : -MathHelper.Pi * 0.28f);

            DrawWingPlumeFan(sb, flareTex, rootPos, wingRootAngle, dir, plumeCount, maxLen,
                snapCurve, sizeMult, timer, tier == 0 ? 0.16f : 0.135f,
                cEmerald, cGold, cLaserWhite, 1f, plumeTipsOut);

            // 3. 星轨几何连线（仅大单翼点亮）
            if (tier == 1 && timer >= snapFrames && plumeCount >= 2 && plumeTipsOut != null)
            {
                float lineAlpha = MathHelper.SmoothStep(0f, 0.65f, (timer - snapFrames) / 6f) * alpha;
                for (int t = 0; t < plumeCount - 1 && t + 1 < plumeTipsOut.Length; t++)
                    DrawLaserSegment(sb, pixel, plumeTipsOut[t], plumeTipsOut[t + 1], cGold * lineAlpha, 1.4f);
            }
        }

        /// <summary>
        /// 超射顿挫弹簧曲线：从 0 冲过 1.25 再回弹锁定到 1.0。
        /// <para/>终结技单翼与常驻光翼（碧翎念涌）**共用**，保证"张开的那一下"手感一致。
        /// </summary>
        public static float SnapSpringCurve(float timer, float snapFrames)
        {
            float snapP = MathHelper.Clamp(timer / Math.Max(1f, snapFrames), 0f, 1f);
            float c = (float)(1f + 2.9f * Math.Pow(snapP - 1f, 3f) + 1.9f * Math.Pow(snapP - 1f, 2f));
            return MathHelper.Clamp(c, 0f, 1.25f);
        }

        /// <summary>
        /// 单侧阶梯晶刃群 —— **光翼本体**。终结技单翼与常驻光翼（碧翎念涌）**共用同一份实现**。
        /// <para/>★ 2026-09-27 用户明确要求："我要的视觉效果就是和那个近战大招的光翼一模一样的视觉效果"，
        /// 因此这里必须是唯一出处 —— 羽片数量/长度/扇角/配色/颤动节奏的任何改动都会同时作用于两处，不会漂移。
        /// </summary>
        /// <param name="wingRootAngle">最外侧主羽的基准角（弧度，屏幕坐标 y 向下）。</param>
        /// <param name="dir">朝向（1 / -1），决定扇形展开方向与镜像。</param>
        /// <param name="angleStep">相邻羽片的角度步长（小单翼 0.16 / 大单翼 0.135）。</param>
        private static void DrawWingPlumeFan(
            SpriteBatch sb, Texture2D flareTex, Vector2 rootPos, float wingRootAngle, int dir,
            int plumeCount, float maxLen, float snapCurve, float sizeMult, float timer, float angleStep,
            Color cEmerald, Color cGold, Color cLaserWhite, float intensity, Vector2[] plumeTipsOut)
        {
            Vector2 origin = new Vector2(flareTex.Width, flareTex.Height) * 0.5f;
            // 强度系数：形状/层数/配色比例完全不变，只整体压暗。
            // 用途：常驻光翼比终结技小（1.2x vs 3x），5 层叠在同一像素上更密，不压会把刃芯钳成纯白。
            cEmerald *= intensity; cGold *= intensity; cLaserWhite *= intensity;

            for (int i = 0; i < plumeCount; i++)
            {
                float wave = (float)Math.Sin(timer * 0.15f + i * 0.45f) * (0.025f + i * 0.006f) * -dir;
                float angleOffset = -dir * (i * angleStep);
                float plumeAngle = wingRootAngle + angleOffset + wave;

                float lenK = i == 0 ? 1.0f : (0.85f - (i - 1) * 0.095f);
                float plumeLen = maxLen * lenK;
                float dx = (float)Math.Cos(plumeAngle), dy = (float)Math.Sin(plumeAngle);

                Vector2 midPos = rootPos + new Vector2(dx, dy) * (plumeLen * 0.52f);
                Vector2 tipPos = rootPos + new Vector2(dx, dy) * (plumeLen * 1.04f);

                float widthFactor = i == 0 ? 0.30f : (0.22f - i * 0.018f);
                Vector2 scaleOuter = new Vector2(plumeLen / flareTex.Width * 1.4f, widthFactor * snapCurve);
                Vector2 scaleInner = new Vector2(plumeLen / flareTex.Width * 1.25f, 0.07f * snapCurve);

                sb.Draw(flareTex, midPos, null, cEmerald, plumeAngle, origin, scaleOuter, SpriteEffects.None, 0f);
                sb.Draw(flareTex, midPos, null, cGold * 0.65f, plumeAngle, origin, scaleOuter * 0.85f, SpriteEffects.None, 0f);
                sb.Draw(flareTex, midPos, null, cLaserWhite, plumeAngle, origin, scaleInner, SpriteEffects.None, 0f);
                sb.Draw(flareTex, tipPos, null, cGold, plumeAngle, origin, new Vector2(0.8f, 0.18f) * snapCurve * sizeMult, SpriteEffects.None, 0f);
                sb.Draw(flareTex, tipPos, null, cLaserWhite, plumeAngle, origin, new Vector2(0.45f, 0.07f) * snapCurve * sizeMult, SpriteEffects.None, 0f);

                if (plumeTipsOut != null && i < plumeTipsOut.Length) plumeTipsOut[i] = tipPos;
            }
        }

        /// <summary>两点间细激光段（1×1 像素拉伸）。</summary>
        public static void DrawLaserSegment(SpriteBatch sb, Texture2D pixel, Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 diff = b - a;
            float len = diff.Length();
            if (len < 1f) return;
            sb.Draw(pixel, a, null, color, (float)Math.Atan2(diff.Y, diff.X),
                new Vector2(0f, 0.5f), new Vector2(len, width), SpriteEffects.None, 0f);
        }

        // ==================================================================
        // 6. 碧翎念涌（MindWingsAura）：常驻光翼
        // ==================================================================
        //
        // ★ 2026-09-27 用户明确要求（并纠正了我的一次跑偏）：
        //   「我要的视觉效果就是和那个近战大招的光翼一模一样的视觉效果（去掉那个粗糙的大棱形就可以）」
        //   —— 所以本函数**不重新设计**光翼，而是直接调用与终结技单翼**同一份实现**
        //   （DrawWingPlumeFan + SnapSpringCurve），只做三件事：
        //     ① 去掉"背部半月圣环"（那段用 bloomTex 本意是圆形柔光，但 Extra_89 与 Extra_98
        //        实测字节相同、都是竖直梭形 → 实际渲染成一枚 ~237px 的**巨大竖直菱形**，
        //        即用户指认的"那个粗糙的大棱形"）；
        //     ② 生命周期从一次性的 24/48 帧改为**无限常驻**（2026-09-27 起；先前是定时 330 帧）；
        //     ③ 镜像到两侧，成为一对随玩家移动的光翼。
        //
        // 时间轴：张开沿用终结技的超射弹簧（SnapSpringCurve）；第 3 帧内渐显；
        //         关闭时用 WingDissipateFrames 帧渐隐（常驻期**没有任何衰减**）。
        //         常驻期不做额外"呼吸缩放"——羽片自身的 wave 颤动（写入 DrawWingPlumeFan）
        //         就是终结技的呼吸感，保持一致。
        //
        // ★ 无限持续为什么是安全的（改之前专门验过，不是想当然）：
        //   · SnapSpringCurve 把 snapP 钳在 1，故 timer 很大时恒返回恰好 1.0 —— 不会溢出、不会回弹；
        //   · 羽片颤动是 sin(timer * 0.15f) 的纯正弦，每帧固定前进 0.15 rad，天然循环、无累积漂移。
        //   （唯一的理论边界：float 在该量级下 ulp 追上 0.15 需连续常驻约 39 小时，
        //     届时颤动会变卡顿 —— 不在现实射程内，故不做 timer 取模。）

        /// <summary>光翼张开的弹簧收敛帧数（终结技约 4~6 帧，常驻版略慢以便看清展开）。</summary>
        public const float WingSnapFrames = 10f;

        /// <summary>
        /// 收尾渐隐帧数。
        /// <para/>★ 这是光翼**唯一**的时间常量：碧翎念涌已改为无限持续（开关式），
        /// 消散不再由"总帧数"推导，而是关闭时写入的倒计时。旧的 <c>WingTotalFrames = 330</c> 已删除——
        /// 留着它只会诱导后来者又去写定时，把"没有 CD 的持续时间"这个伪 CD 重新加回来。
        /// </summary>
        public const int WingDissipateFrames = 18;

        /// <summary>羽片数量（= 终结技"巨单翼"档）。</summary>
        public const int WingPlumeCount = 7;

        /// <summary>主羽基准长度（px）= 终结技巨单翼档。</summary>
        public const float WingMaxPlumeLen = 195f;

        /// <summary>
        /// 碧翎念涌的整体尺寸倍率（**唯一出处**：游戏端 MindWingsAura 与任何离屏渲染都取这里）。
        /// <para/>终结技单翼用 3（巨大演出）；常驻版 1.2 —— 形状完全一致，只是别糊住视野。
        /// </summary>
        public const float MindWingSizeMult = 1.2f;

        /// <summary>
        /// 碧翎念涌是否左右各一。
        /// <para/>★ 2026-09-27 用户拍板 **false（单翼）**："保留单向的就可以了，双向的一整对翅膀太丑了。
        /// 这毕竟是个 2D 游戏" —— Terraria 侧视，人物永远侧身，对称一对会读成"背后贴了两片装饰"，
        /// 中间还会空出一块；单翼也天然与大招完全同构。
        /// <para/>⚠ 放在本文件是为了让**离屏渲染也读同一个值**：此前那里自己写死 true，
        /// 于是"我看到的图"和"游戏里跑的"不是一回事（2026-09-27 被视觉复查当场抓出）。
        /// </summary>
        public const bool MindWingBothSides = false;

        /// <summary>
        /// 晶刃扇的强度系数（终结技 = 1.0 不压；常驻光翼压暗）。
        /// <para/>为什么必须压：晶刃扇在同一位置叠了 5 层（翠绿 + 金 + **白 0.90**），
        /// 终结技是 3× 大尺寸、只播 48 帧，叠得开；常驻版缩到 1.2× 后叠得更密，
        /// 离线实测**刃芯被钳成纯白（占发光像素 18%，峰值 (234,255,255)）**，
        /// 而参考图峰值是柔和绿 (168,207,175) —— 白刃反而盖过了金羽尖，观感倒置。
        /// 0.62 是把峰值压回参考量级的实测值。
        /// </summary>
        private const float WingIntensity = 0.62f;

        /// <summary>
        /// 脚下软场半径（px）—— **纯数值**，不再画任何东西。
        /// 语义：翼下敌人被念蚀减速的判定半径。旧版那个"法阵圈/尖轮"已随重做删除。
        /// </summary>
        public const float WingAuraRadius = 160f;

        /// <summary>羽尖暂存（避免逐帧分配）。</summary>
        private static readonly Vector2[] MindWingTipsL = new Vector2[WingPlumeCount];
        private static readonly Vector2[] MindWingTipsR = new Vector2[WingPlumeCount];

        /// <summary>
        /// 绘制碧翎念涌：一对随玩家移动的光翼。
        /// <para/>铁律：**不切换 SpriteBatch 批次**（调用方须已处于 Additive 批次内）。
        /// </summary>
        /// <param name="rootScreenPos">翼根（玩家背部偏上）的屏幕坐标。</param>
        /// <param name="direction">玩家朝向（1 右 / -1 左）。</param>
        /// <param name="timer">自展开起的帧数（**无上限**，可无限增长）。</param>
        /// <param name="dissipateRemaining">
        /// 关闭时的消散倒计时（帧）：&gt;0 = 正在收尾并据此渐隐，0 = 常驻中（完全不衰减）。
        /// <para/>终结技直接传 0（它有自己的总时长，不走这条路径）。
        /// </param>
        /// <param name="sizeMult">整体尺寸倍率（终结技用 3；常驻版建议 1.0~1.6，避免糊住视野）。</param>
        /// <param name="bothSides">
        /// true = 左右各一（一对光翼，默认）；false = 与终结技完全一致的单侧单翼。
        /// </param>
        public static void DrawMindWings(
            SpriteBatch sb, Texture2D flareTex, Texture2D pixel,
            Vector2 rootScreenPos, int direction, float timer, float dissipateRemaining, float sizeMult, bool bothSides,
            Color emeraldBase, Color goldBase)
        {
            if (sb == null || flareTex == null) return;

            int dir = direction >= 0 ? 1 : -1;

            // 渐显 / 渐隐（渐显 3 帧；只在"关闭"时进入收尾渐隐，常驻期没有任何衰减）
            float alpha = 1f;
            if (timer < 3f) alpha = timer / 3f;
            else if (dissipateRemaining > 0f)
                alpha = MathHelper.Clamp(dissipateRemaining / WingDissipateFrames, 0f, 1f);
            if (alpha <= 0.01f) return;

            Color cEmerald = emeraldBase * (alpha * 0.85f);
            Color cGold = goldBase * (alpha * 0.75f);
            Color cLaserWhite = Color.White * (alpha * 0.90f);

            float snapCurve = SnapSpringCurve(timer, WingSnapFrames);
            float maxLen = WingMaxPlumeLen * snapCurve * sizeMult;

            // 主翼（与朝向同侧的"后翼"）
            DrawOneSide(sb, flareTex, pixel, rootScreenPos, dir, timer, maxLen, snapCurve, sizeMult,
                cEmerald, cGold, cLaserWhite, MindWingTipsR);

            // 镜像翼
            if (bothSides)
                DrawOneSide(sb, flareTex, pixel, rootScreenPos, -dir, timer, maxLen, snapCurve, sizeMult,
                    cEmerald, cGold, cLaserWhite, MindWingTipsL);
        }

        /// <summary>画一侧光翼（含羽尖星轨连线）。</summary>
        private static void DrawOneSide(SpriteBatch sb, Texture2D flareTex, Texture2D pixel,
            Vector2 rootPos, int dir, float timer, float maxLen, float snapCurve, float sizeMult,
            Color cEmerald, Color cGold, Color cLaserWhite, Vector2[] tips)
        {
            // 与终结技巨单翼完全相同的基准角与步长
            float wingRootAngle = dir == 1 ? -MathHelper.Pi * 0.72f : -MathHelper.Pi * 0.28f;
            const float angleStep = 0.135f;

            DrawWingPlumeFan(sb, flareTex, rootPos, wingRootAngle, dir, WingPlumeCount, maxLen,
                snapCurve, sizeMult, timer, angleStep, cEmerald, cGold, cLaserWhite, WingIntensity, tips);

            // 星轨几何连线（与终结技同款）
            if (pixel == null || tips == null) return;
            for (int t = 0; t < WingPlumeCount - 1 && t + 1 < tips.Length; t++)
                DrawLaserSegment(sb, pixel, tips[t], tips[t + 1], cGold * 0.55f, 1.4f);
        }

        // ==================================================================
        // 曲线小工具
        // ==================================================================

        /// <summary>三次缓出。</summary>
        private static float EaseOutCubic(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            float u = 1f - t;
            return 1f - u * u * u;
        }
    }
}
