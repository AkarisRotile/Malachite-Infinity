using System;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 孔雀柳刃 · 独立成长系统（阶段 2b，与星图/念伤害解耦）。
    /// 设计依据：成长曲线定稿（用户拍板：0~9 定稿 + 10~13 预置）
    /// 用法：武器/命中层只读本服务，禁止在别处另造曲线公式。
    /// 数值口径：Item.damage=32 / useTime=20 为档位基准；
    /// DamageMult(stage) 是最终伤害系数（32×系数≈目标绝对伤害）；
    /// SpeedFactor(stage) 是攻速系数（>1=更快，等效间隔≈20/系数）；
    /// 0~9 按原版流程梯队校准（见注释）；10~13 为 M4 锚点规则定稿前的预置斜坡。
    /// </summary>
    public static class WillowGrowth
    {
        public const int StageCount = 14; // stage 0..13

        public static int ClampStage(int stage) => Math.Clamp(stage, 0, StageCount - 1);

        /// <summary>
        /// 原版梯队参考（0~9，数值对齐波次将以 Terraria Wiki 复核）：
        /// 0 开局(铜短剑/木剑≈7) / 1 克眼后(金·铂金剑≈11~13) / 2 邪恶首领(恶魔刃≈17~21)
        /// / 3 骷髅王·地牢段(永夜刃素材≈24~27) / 4 肉山后·钴秘银(≈30~38)
        /// / 5 机械三王·精金钛金(≈40~52) / 6 世纪之花·叶绿(≈55~65)
        /// / 7 石巨人·甲虫泰拉刃(≈70~85) / 8 拜月教·日耀段(≈85~100)
        /// / 9 月总·星旋月耀级(≈100~150)。
        /// 本表以"同 DPS 体感"对齐而非照抄原版绝对数值。
        /// </summary>
        private static readonly float[] Damage = { 0.44f, 0.62f, 0.85f, 1.15f, 1.45f, 1.75f, 2.10f, 2.55f, 3.00f, 3.60f, 4.30f, 5.00f, 5.80f, 6.80f };

        /// <summary>攻速系数：逐渐加快（等效间隔 20/系数 ≈ 23.5 → 5.3 帧）。</summary>
        private static readonly float[] Speed = { 0.85f, 0.90f, 0.95f, 0.95f, 1.10f, 1.25f, 1.40f, 1.55f, 1.80f, 2.10f, 2.40f, 2.75f, 3.20f, 3.80f };

        private static readonly int[] ArmorPen = { 5, 7, 8, 9, 10, 10, 10, 12, 14, 16, 20, 24, 28, 35 };

        public static float DamageMult(int stage) => Damage[ClampStage(stage)];
        public static float SpeedFactor(int stage) => Speed[ClampStage(stage)];
        public static int FlatAP(int stage) => ArmorPen[ClampStage(stage)];
    }
}
