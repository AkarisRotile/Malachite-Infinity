using System;
using Terraria;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 潜伏攻击系统：自研潜伏值 + 潜伏攻击判定 + 弹幕标记。
    /// 灾厄在场时读写灾厄玩家/弹幕数据（行为与 v1.5.3 一致）；
    /// 灾厄缺失时使用本模组 MalachitePlayer 维护的潜伏值。
    /// </summary>
    public static class StealthSystem
    {
        /// <summary>潜伏攻击可用阈值（与灾厄一致：潜伏值达到最大值 25%）。</summary>
        public const float StrikeThreshold = 0.25f;

        /// <summary>无灾厄时本模组自建潜伏值上限。</summary>
        public const float NativeMaxStealth = 100f;

        /// <summary>触发一次潜伏攻击消耗的潜伏值比例（灾厄语义：消耗 25% 基础值）。</summary>
        public const float StrikeCost = 25f;

        public static bool UsingCalamityStealth => CalamityCompat.Loaded;

        /// <summary>当前潜伏值（0 ~ 最大值）。</summary>
        public static float GetStealth(Player player)
        {
            if (CalamityCompat.Loaded)
            {
                float v = CalamityCompat.GetPlayerStealth(player);
                float max = CalamityCompat.GetPlayerStealthMax(player);
                if (max > 0f) return v;
            }
            return player.GetModPlayer<MalachitePlayer>().stealthValue;
        }

        /// <summary>最大潜伏值。</summary>
        public static float GetMaxStealth(Player player)
        {
            if (CalamityCompat.Loaded)
            {
                float max = CalamityCompat.GetPlayerStealthMax(player);
                if (max > 0f) return max;
            }
            return NativeMaxStealth;
        }

        /// <summary>是否可触发潜伏攻击。</summary>
        public static bool StealthStrikeAvailable(Player player)
            => GetStealth(player) >= GetMaxStealth(player) * StrikeThreshold;

        /// <summary>该弹幕是否为潜伏攻击。</summary>
        public static bool IsStealthStrike(Projectile proj)
        {
            if (CalamityCompat.Loaded) return CalamityCompat.IsStealthStrike(proj);
            return proj.GetGlobalProjectile<MalachiteGlobalProjectile>().isStealthStrike;
        }

        /// <summary>将弹幕标记为潜伏攻击。</summary>
        public static void MarkStealthStrike(Projectile proj)
        {
            if (CalamityCompat.Loaded)
            {
                CalamityCompat.SetStealthStrike(proj, true);
                return;
            }
            proj.GetGlobalProjectile<MalachiteGlobalProjectile>().isStealthStrike = true;
        }

        /// <summary>消耗一次潜伏攻击所需的潜伏值（灾厄语义下由灾厄自身扣除，本方法仅用于无灾厄模式）。</summary>
        public static void ConsumeStrike(Player player)
        {
            if (CalamityCompat.Loaded) return; // 灾厄自己管理消耗
            var mp = player.GetModPlayer<MalachitePlayer>();
            mp.stealthValue = Math.Max(0f, mp.stealthValue - StrikeCost);
        }
    }
}
