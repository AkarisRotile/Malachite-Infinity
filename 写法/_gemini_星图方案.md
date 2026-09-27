### 1. 五条关键美术改进
1. **增设浑天仪同心刻度环与主轴虚线**：构建天文台星盘底图，解决无边界感、碎片化与重心散的问题。
2. **星轨虚底连线（Ghost Lines）常驻**：暗态线段赋予星座骨架认知，使未点亮时也能清晰识别星座轮廓。
3. **星座局部星云底晕（Nebula Bloom）**：各星座中心垫入超低透明度大半径渐变，收拢视觉聚焦并提供景深。
4. **收敛星芒尺度并改用非对称 4 芒**：将 `spikeLen` 压缩至 1.5~2.2 倍，防止过曝糊死与几何框“打架”。
5. **层级色彩压制**：连线采用暗墨绿，未激活星用极微弱暗青，禁止大面积 Pure White，保障眼球舒适度。

---

### 2. 改进后的 `DrawConstellationMap`

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
    PrimitiveVfx.Begin(gd, view, Matrix.CreateOrthographicOffCenter(
        0f, gd.Viewport.Width, gd.Viewport.Height, 0f, 0f, 1f));

    // 1. 天球仪底盘：同心极坐标暗环与暗十字轴，锚定画面重心
    Color axisCol = cyanBase * 0.08f;
    PrimitiveVfx.Ring(center, scale * 0.95f, 1.2f, axisCol, 64);
    PrimitiveVfx.Ring(center, scale * 0.65f, 0.8f, axisCol * 0.7f, 48);
    PrimitiveVfx.Ring(center, scale * 0.35f, 0.8f, axisCol * 0.5f, 36);
    PrimitiveVfx.Line(center - new Vector2(scale, 0), center + new Vector2(scale, 0), 1f, axisCol * 0.4f, axisCol * 0.4f);
    PrimitiveVfx.Line(center - new Vector2(0, scale), center + new Vector2(0, scale), 1f, axisCol * 0.4f, axisCol * 0.4f);

    // 2. 背景微星尘
    if (!skillTreeStyle)
    {
        for (int i = 0; i < 90; i++)
        {
            float h1 = Hash01(i * 12.9898f), h2 = Hash01(i * 78.233f), h3 = Hash01(i * 39.425f);
            Vector2 dp = center + new Vector2(h1 * 2f - 1f, h2 * 2f - 1f) * scale;
            if (Vector2.Distance(dp, center) > scale * 0.92f) continue;
            PrimitiveVfx.FillCircle(dp, (0.8f + 1.2f * h3) * (scale / 300f), cyanBase * (0.10f + 0.15f * h3), Color.Transparent, 8);
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
            PrimitiveVfx.Line(pa, pb, w * 0.8f, cyanBase * 0.12f, cyanBase * 0.12f);
            if (isLinkLit || hot)
            {
                Color lCol = hot ? Color.Lerp(greenBase, Color.White, 0.4f) : greenBase;
                PrimitiveVfx.Line(pa, pb, w, lCol * 0.55f, lCol * 0.55f);
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
                PrimitiveVfx.FillCircle(pos, r * 1.8f, nodeCol * 0.25f, Color.Transparent, 16);
                PrimitiveVfx.FillCircle(pos, r * 0.75f, nodeCol * 0.9f, nodeCol * 0.3f, 16);
                PrimitiveVfx.FillCircle(pos, r * 0.28f, Color.White * 0.6f, nodeCol * 0.8f, 10);

                if (skillTreeStyle)
                {
                    float ringR = r * 1.6f;
                    PrimitiveVfx.PolygonRing(pos, ringR, 1.2f, 4, 0.785f, (hot ? Color.White : nodeCol) * 0.6f);
                }
            }
            else
            {
                // 未点亮星：微暗青色环 + 中心暗点，静谧不刺眼
                PrimitiveVfx.Ring(pos, r * 0.6f, 0.9f, cyanBase * 0.22f, 14);
                PrimitiveVfx.FillCircle(pos, r * 0.2f, cyanBase * 0.3f, Color.Transparent, 8);
            }
        }
    }

    PrimitiveVfx.End();
    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
        DepthStencilState.None, RasterizerState.CullNone, null, view);
}
```

---

### 3. PrimitiveVfx 变动说明
- **无需新增图元**：现存的 `Ring`、`Line`、`FillCircle`、`PolygonRing`、`StarSpikes` 组合完全足以支持重构。
- **参数微调**：星芒旋转固定为 `0.785f`（45 度），与多边形外框同轴对齐，消除散乱视觉噪声。