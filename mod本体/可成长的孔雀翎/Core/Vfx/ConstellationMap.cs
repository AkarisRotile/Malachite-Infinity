// 本文件为自主实现（无外部参考源码）。
//
// ============================================================================
// 「力」页星图 —— 基于**真实星座**的几何数据
// ============================================================================
// 设计缘起（2026-09-25 用户指正）：初版把五轨画成从中心放射的五条直线，用户指出
// "哪有星座是五条直着的线" —— 那读起来是风车，不是星座。真实星座的特征是：
//   ① 恒星在天空疏密不均、亮度差异大；② 靠"想象连线"结成形；③ 每个星座形状各异且有辨识度。
// 因此本版改为：**内建真实恒星表（赤经/赤纬/星等），用天文投影算出真实星座形状**。
//
// 星表来源：各恒星的 J2000 赤经(RA)、赤纬(Dec)、视星等(V) 为公开天文数据
// （参见 Wikipedia 各星座条目与亮星星表）。坐标在此仅作为"形状底稿"，
// 不追求毫角秒精度；星等用于决定绘制亮度与"等级 → 点亮顺序"。
//
// 观感约定：**星等越亮 = 等级越高**。即加点时优先点亮该星座最亮的星，
// 高等级才轮到暗星 —— 这既符合天文直觉（亮星先被看见），也让进度读起来自然。
//
// 铁律：零 Terraria 依赖，只用 Microsoft.Xna.Framework。

using System;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    /// <summary>一颗恒星：星名、J2000 赤经（小时）、赤纬（度）、视星等。</summary>
    public struct Star
    {
        public string Name;
        public float RaHours;   // 赤经，单位：小时（0~24）
        public float DecDeg;    // 赤纬，单位：度（-90~+90）
        public float Mag;       // 视星等（越小越亮）

        public Star(string name, float raHours, float decDeg, float mag)
        {
            Name = name;
            RaHours = raHours;
            DecDeg = decDeg;
            Mag = mag;
        }
    }

    /// <summary>一个星座：名称 + 主要恒星 + 连线（以本星座星表下标成对给出）+ 画布锚点。</summary>
    public sealed class Constellation
    {
        public string NameZh;
        public string NameEn;
        public Star[] Stars;
        public int[] Links;          // 成对下标：a0,b0, a1,b1, ...
        public Vector2 Anchor;       // 在星图局部坐标中的摆放位置（-1~1 归一化域）
        public float Scale;          // 该星座的缩放（不同星座在天上张角不同，需要统一视觉体量）

        public Constellation(string zh, string en, Star[] stars, int[] links, Vector2 anchor, float scale)
        {
            NameZh = zh;
            NameEn = en;
            Stars = stars;
            Links = links;
            Anchor = anchor;
            Scale = scale;
        }
    }

    /// <summary>
    /// 「力」页星图几何：真实星座数据 + 天文投影 + 布局。
    /// 与旧版 <see cref="StarMapVfx"/>（放射五臂）的关键区别：**不造假形状**，
    /// 形状全部由真实赤经赤纬投影得到。
    /// </summary>
    public static class ConstellationMap
    {
        /// <summary>星座数量（= 五轨）。</summary>
        public const int Count = 5;

        /// <summary>星等映射范围：亮于此的算"最亮"，暗于此的算"最暗"（用于等级→点亮顺序）。</summary>
        public const float MagBrightest = 1.6f;
        public const float MagDimmest = 5.0f;

        // --------------------------------------------------------------------
        // 星表（RA 小时 / Dec 度 / 星等）
        // --------------------------------------------------------------------

        // 小熊座 Ursa Minor：北斗七星之外最著名的极区星座，本设计中作为中心主星座（北极星定盘）。
        private static readonly Star[] UrsaMinor =
        {
            new Star("Polaris",   2.530f, 89.264f, 1.98f),
            new Star("Yildun",   17.537f, 86.586f, 4.35f),
            new Star("Epsilon",  16.766f, 82.037f, 4.21f),
            new Star("Zeta",     15.734f, 77.794f, 4.29f),
            new Star("Eta",      16.291f, 75.755f, 4.95f),
            new Star("Pherkad",  15.345f, 71.834f, 3.00f),
            new Star("Kochab",   14.845f, 74.156f, 2.07f),
        };

        // 猎户座 Orion：全天最容易辨认的星座（沙漏/猎户腰带），承担"攻击"轨。
        private static readonly Star[] Orion =
        {
            new Star("Betelgeuse", 5.919f,   7.407f, 0.42f),
            new Star("Bellatrix",  5.418f,   6.350f, 1.64f),
            new Star("Alnitak",    5.679f,  -1.943f, 1.74f),
            new Star("Alnilam",    5.604f,  -1.202f, 1.69f),
            new Star("Mintaka",    5.533f,  -0.299f, 2.23f),
            new Star("Saiph",      5.796f,  -9.670f, 2.07f),
            new Star("Rigel",      5.242f,  -8.202f, 0.13f),
        };

        // 仙后座 Cassiopeia：醒目的 W 形，承担"攻速"轨（形状本身就快节奏）。
        private static readonly Star[] Cassiopeia =
        {
            new Star("Caph",     0.153f, 59.150f, 2.28f),
            new Star("Schedar",  0.675f, 56.537f, 2.24f),
            new Star("Gamma",    0.945f, 60.717f, 2.47f),
            new Star("Ruchbah",  1.430f, 60.235f, 2.68f),
            new Star("Segin",    1.907f, 63.670f, 3.35f),
        };

        // 天鹅座 Cygnus：大十字 + 长尾，承担"暴击"轨（十字意象契合）。
        private static readonly Star[] Cygnus =
        {
            new Star("Deneb",     20.690f, 45.280f, 1.25f),
            new Star("Sadr",      20.370f, 40.257f, 2.23f),
            new Star("Albireo",   19.512f, 27.960f, 3.05f),
            new Star("Delta",     19.750f, 45.131f, 2.87f),
            new Star("Gienah",    20.770f, 33.970f, 2.48f),
        };

        // 小熊之外的第二个极区参照：天琴座 Lyra：小而亮的织女星群，承担"护甲穿透"轨。
        private static readonly Star[] Lyra =
        {
            new Star("Vega",      18.615f, 38.784f, 0.03f),
            new Star("Sheliak",   18.834f, 33.363f, 3.52f),
            new Star("Sulafat",   18.982f, 32.690f, 3.24f),
            new Star("Delta2",    18.908f, 36.899f, 4.30f),
            new Star("Zeta",      18.746f, 37.605f, 4.36f),
        };

        /// <summary>
        /// 五个真实星座（顺序 = 五轨顺序：攻击 / 穿透 / 攻速 / 暴击 / 并发）。
        /// Anchor 为在归一化星图域（-1~1）中的摆放位置；Scale 用于统一视觉体量。
        /// </summary>
        public static readonly Constellation[] All =
        {
            // 攻击 —— 猎户座（左上）
            new Constellation("猎户座", "Orion", Orion,
                new[] { 0, 1, 1, 4, 4, 2, 2, 0, /*腰带*/ 2, 3, 3, 4, /*下身*/ 2, 5, 4, 6 },
                new Vector2(-0.62f, -0.58f), 0.36f),

            // 穿透 —— 天琴座（右上）
            new Constellation("天琴座", "Lyra", Lyra,
                new[] { 0, 3, 3, 1, 1, 2, 2, 3, 3, 4 },
                new Vector2(0.62f, -0.58f), 0.60f),

            // 攻速 —— 仙后座（左下，W 形）
            new Constellation("仙后座", "Cassiopeia", Cassiopeia,
                new[] { 0, 1, 1, 2, 2, 3, 3, 4 },
                new Vector2(-0.60f, 0.56f), 0.58f),

            // 暴击 —— 天鹅座（右下，大十字）
            new Constellation("天鹅座", "Cygnus", Cygnus,
                new[] { 0, 1, 1, 2, 1, 3, 2, 4 },
                new Vector2(0.60f, 0.56f), 0.54f),

            // 并发 —— 小熊座（正中，极星定盘，作为星图核心；本身张角也最大）
            new Constellation("小熊座", "Ursa Minor", UrsaMinor,
                new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 6, 6, 5, 1, 6 },
                new Vector2(0f, 0f), 1.15f),
        };

        // --------------------------------------------------------------------
        // 天文投影
        // --------------------------------------------------------------------

        /// <summary>
        /// 把赤经/赤纬投到平面（简易等距圆柱 + 纬度余弦修正）。
        /// <para/>RA 取负 —— 天空中 RA 增大方向与"东"一致，而屏幕 x 向右，故取负才是从地球仰望的样子。
        /// <para/>纬度余弦修正是必要的：高赤纬星座（如小熊座）若不修正会被横向拉长，
        /// 北斗七星尤其明显（真实形状接近勺形，不修正会摊成一条横线）。
        /// </summary>
        public static Vector2 Project(Star s)
        {
            float raRad = s.RaHours * MathHelper.TwoPi / 24f;
            float decRad = MathHelper.ToRadians(s.DecDeg);
            return new Vector2(-raRad * (float)Math.Cos(decRad), decRad);
        }

        /// <summary>
        /// 求某星座在"自身局部空间"的投影点，并归一化到以质心为原点、最大跨度为 2 的单位框内。
        /// 这样各星座可用同一套 Anchor/Scale 摆放，不会被原始张角大小带偏。
        /// </summary>
        public static Vector2[] LocalShape(Constellation c)
        {
            int n = c.Stars.Length;
            Vector2[] p = new Vector2[n];
            Vector2 sum = Vector2.Zero;
            for (int i = 0; i < n; i++)
            {
                p[i] = Project(c.Stars[i]);
                sum += p[i];
            }
            Vector2 centroid = sum / Math.Max(1, n);
            float maxR = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                p[i] -= centroid;
                maxR = Math.Max(maxR, p[i].Length());
            }
            // 归一化到半径 1
            for (int i = 0; i < n; i++) p[i] /= maxR;
            return p;
        }

        /// <summary>
        /// 星等 → 亮度 0~1（1 = 最亮）。用线性内插，超出范围的夹取。
        /// </summary>
        public static float Brightness(float mag)
        {
            float t = MathHelper.Clamp((MagDimmest - mag) / (MagDimmest - MagBrightest), 0f, 1f);
            return t;
        }

        /// <summary>
        /// 等级 → 该星座应当点亮几颗星。
        /// 逻辑：亮星优先点亮（星等升序），故先把星按星等排序，再按等级比例分配数量。
        /// </summary>
        /// <param name="c">星座。</param>
        /// <param name="level">该轨已投入等级。</param>
        /// <param name="cap">该轨当前等级上限。</param>
        /// <returns>应点亮的恒星数量（0 ~ Stars.Length）。</returns>
        public static int LitCount(Constellation c, int level, int cap)
        {
            if (level <= 0) return 0;
            if (cap <= 0) return 0;
            float t = MathHelper.Clamp(level / (float)cap, 0f, 1f);
            return (int)Math.Round(t * c.Stars.Length);
        }

        /// <summary>按星等升序返回该星座恒星的下标顺序（用于"亮星先亮"）。</summary>
        public static int[] BrightestFirst(Constellation c)
        {
            int n = c.Stars.Length;
            int[] idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            // 插入排序：n 很小（5~7），无需引入排序框架
            for (int i = 1; i < n; i++)
            {
                int key = idx[i];
                int j = i - 1;
                while (j >= 0 && c.Stars[idx[j]].Mag > c.Stars[key].Mag)
                {
                    idx[j + 1] = idx[j];
                    j--;
                }
                idx[j + 1] = key;
            }
            return idx;
        }
    }
}
