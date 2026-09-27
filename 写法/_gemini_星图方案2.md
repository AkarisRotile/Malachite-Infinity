### 1. 偏暗诊断与解法
**结论：必须做「两端拉开」——提升底盘/未激活结构的可读性，同时增强核心节点的辉光能量。**
- **底盘不可见原因**：黑底上 `alpha 0.08` 线性叠加后有效亮度不足 15/255，完全被面板吞没。浑天仪应提至 `0.22~0.32`，并加入**外圈刻度点与中心极核**，使其像发光的仪器而非背景污渍。
- **未激活暗星**：Ghost Lines 与暗节点加深为具辨识度的「冷深青」，确保 0 进度下列装輪廓清晰。
- **已激活亮星**：增加内外双层 Glow 半径，重拾星图该有的「星辉感」。

---

### 2. 完整实现

```csharp
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
    PrimitiveVfx.Begin(gd, view, Matrix.CreateOrthographicOffCenter(0f, gd.Viewport.Width, gd.Viewport.Height, 0f, 0f, 1f));

    // 1. 浑天仪底盘：提亮至清晰可见，增加罗盘刻度与天极发光核
    Color axisCol = cyanBase * 0.24f;
    Color subCol = cyanBase * 0.14f;
    PrimitiveVfx.Ring(center, scale * 0.95f, 1.8f, axisCol, 72);
    PrimitiveVfx.Ring(center, scale * 0.65f, 1.2f, subCol, 54);
    PrimitiveVfx.Ring(center, scale * 0.35f, 1.0f, subCol * 0.8f, 36);
    PrimitiveVfx.Line(center - new Vector2(scale * 0.98f, 0), center + new Vector2(scale * 0.98f, 0), 1.2f, axisCol * 0.7f, axisCol * 0.7f);
    PrimitiveVfx.Line(center - new Vector2(0, scale * 0.98f), center + new Vector2(0, scale * 0.98f), 1.2f, axisCol * 0.7f, axisCol * 0.7f);
    
    // 八方刻度标记与天极星核
    for (int k = 0; k < 8; k++)
    {
        float ang = k * MathHelper.PiOver4;
        Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
        PrimitiveVfx.Line(center + dir * (scale * 0.92f), center + dir * (scale * 0.98f), 1.5f, axisCol, axisCol);
    }
    PrimitiveVfx.GlowDot(center, scale * 0.04f, cyanBase, 0.4f, 2);

    // 2. 背景微星尘
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

    // 3. 星座与星轨
    for (int i = 0; i < n; i++)
    {
        var constellation = ConstellationMap.Get(i);
        int lvl = levels[i];
        int cap = caps[i];
        bool isHover = (i == hoverIndex);
        float progress = cap > 0 ? Math.Clamp((float)lvl / cap, 0f, 1f) : 0f;

        // 星云底晕（提亮对比度）
        Vector2 cCenter = center + constellation.CenterOffset * scale;
        Color nebulaCol = (progress > 0 ? greenBase : cyanBase) * (0.08f + progress * 0.12f + (isHover ? 0.08f : 0f));
        PrimitiveVfx.FillCircle(cCenter, scale * 0.25f, nebulaCol, Color.Transparent, 20);

        // A. 虚底骨架线（Ghost Lines）：大幅提高基础可见度，使暗星座依然有轮廓
        for (int e = 0; e < constellation.Edges.Length; e++)
        {
            var edge = constellation.Edges[e];
            Vector2 p1 = center + constellation.Stars[edge.A].Offset * scale;
            Vector2 p2 = center + constellation.Stars[edge.B].Offset * scale;
            Color ghostCol = isHover ? cyanBase * 0.45f : cyanBase * 0.22f;
            PrimitiveVfx.Line(p1, p2, 1.0f, ghostCol, ghostCol);
        }

        // B. 点亮实线
        int activeEdgeCount = (int)Math.Round(progress * constellation.Edges.Length);
        for (int e = 0; e < activeEdgeCount; e++)
        {
            var edge = constellation.Edges[e];
            Vector2 p1 = center + constellation.Stars[edge.A].Offset * scale;
            Vector2 p2 = center + constellation.Stars[edge.B].Offset * scale;
            Color activeCol = Color.Lerp(greenBase, goldBase, (float)e / Math.Max(1, constellation.Edges.Length - 1)) * 0.85f;
            PrimitiveVfx.Line(p1, p2, 2.2f, activeCol, activeCol);
            PrimitiveVfx.Line(p1, p2, 0.8f, Color.White * 0.6f, Color.White * 0.6f); // 亮实线加白芯
        }

        // C. 星核节点
        for (int s = 0; s < constellation.Stars.Length; s++)
        {
            var star = constellation.Stars[s];
            Vector2 p = center + star.Offset * scale;
            bool active = progress >= star.Threshold;

            if (active)
            {
                Color baseC = star.IsMajor ? goldBase : greenBase;
                float pulse = 1f + 0.12f * (float)Math.Sin(time * 2.5f + s);
                PrimitiveVfx.GlowDot(p, scale * (star.IsMajor ? 0.048f : 0.032f) * pulse, baseC, 0.95f, 3);
                PrimitiveVfx.FillCircle(p, scale * (star.IsMajor ? 0.015f : 0.010f), Color.White * 0.9f, baseC, 16);

                if (star.IsMajor || isHover)
                {
                    float spikeLen = scale * (star.IsMajor ? 0.08f : 0.045f) * pulse;
                    PrimitiveVfx.StarSpikes(p, spikeLen, 1.8f, baseC * 0.75f, 4, time * 0.15f);
                }
            }
            else
            {
                // 未激活节点：清晰暗星点，保留微光
                Color darkStar = cyanBase * (isHover ? 0.5f : 0.28f);
                PrimitiveVfx.FillCircle(p, scale * 0.007f, darkStar, Color.Transparent, 10);
                PrimitiveVfx.Ring(p, scale * 0.009f, 0.8f, darkStar * 0.6f, 12);
            }
        }
    }

    PrimitiveVfx.End();
    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, view);
}
```