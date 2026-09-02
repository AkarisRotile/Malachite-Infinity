using System;
using Terraria;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 潜伏攻击系统（阶段 2 独立化后）：纯自建，不再读写灾厄玩家/弹幕数据，
    /// 也不再依赖旧的全局弹幕接管类 —— 弹幕潜伏标记改由
    /// <see cref="IStealthStrikeProjectile"/> 接口（本模组弹幕类各自实现）承担。
    /// 潜伏值由 MalachitePlayer 维护。
    /// </summary>
    public static class StealthSystem
    {
        /// <summary>潜伏攻击可用阈值（潜伏值达到最大值的 25%）。</summary>
        public const float StrikeThreshold = 0.25f;

        /// <summary>自建潜伏值上限。</summary>
        public const float NativeMaxStealth = 100f;

        /// <summary>触发一次潜伏攻击消耗的潜伏值。</summary>
        public const float StrikeCost = 25f;

        /// <summary>当前潜伏值（0 ~ NativeMaxStealth）。</summary>
        public static float GetStealth(Player player)
            => Math.Clamp(player.GetModPlayer<MalachitePlayer>().stealthValue, 0f, NativeMaxStealth);

        /// <summary>最大潜伏值（固定 100）。</summary>
        public static float GetMaxStealth(Player player) => NativeMaxStealth;

        /// <summary>是否可触发潜伏攻击。</summary>
        public static bool StealthStrikeAvailable(Player player)
            => GetStealth(player) >= NativeMaxStealth * StrikeThreshold;

        /// <summary>
        /// 该弹幕是否为潜伏攻击。只认本模组弹幕（实现 IStealthStrikeProjectile 者），
        /// 其余类型一律 false —— 标记/读取失败均静默，无全局接管兜底。
        /// </summary>
        public static bool IsStealthStrike(Projectile proj)
            => proj?.ModProjectile is IStealthStrikeProjectile strike && strike.IsStealthStrike;

        /// <summary>
        /// 将弹幕标记为潜伏攻击。仅对实现 <see cref="IStealthStrikeProjectile"/> 的弹幕生效，
        /// 其它类型静默忽略（返回 false 表示未标记成功）。
        /// </summary>
        public static bool MarkStealthStrike(Projectile proj)
        {
            if (proj?.ModProjectile is IStealthStrikeProjectile strike)
            {
                strike.IsStealthStrike = true;
                return true;
            }
            return false;
        }

        /// <summary>消耗一次潜伏攻击所需的潜伏值。</summary>
        public static void ConsumeStrike(Player player)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            mp.stealthValue = Math.Max(0f, mp.stealthValue - StrikeCost);
        }
    }
}
