using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 灾厄兼容层（反射）：在灾厄已安装时安全读取其存档字段与玩家/弹幕数据，
    /// 灾厄缺失时所有访问返回默认值。本文件是唯一允许提及灾厄类型的文件，
    /// 其余业务代码一律通过 ProgressSystem / StealthSystem 访问。
    /// </summary>
    public static class CalamityCompat
    {
        public static bool Loaded => ModLoader.HasMod("CalamityMod");

        // ---- 玩家潜伏字段 (CalamityMod.Players.CalamityPlayer) ----
        private static Type _calPlayerType;
        private static FieldInfo _stealthField;
        private static FieldInfo _stealthMaxField;

        // ---- 弹幕潜伏标记 (CalamityMod.Projectiles.CalamityGlobalProjectile) ----
        private static Type _calGlobalProjType;
        private static FieldInfo _stealthStrikeField;

        // ---- 存档击杀标记 (CalamityMod.Systems.DownedBossSystem) ----
        private static Type _downedBossType;
        private static Dictionary<string, FieldInfo> _downedFlagCache = new Dictionary<string, FieldInfo>();

        static CalamityCompat()
        {
            if (!Loaded) return;

            var calMod = ModLoader.GetMod("CalamityMod");
            Assembly asm = calMod?.Code;
            if (asm == null) return;

            _calPlayerType = asm.GetType("CalamityMod.Players.CalamityPlayer");
            if (_calPlayerType != null)
            {
                _stealthField = _calPlayerType.GetField("stealth", BindingFlags.Public | BindingFlags.Instance);
                _stealthMaxField = _calPlayerType.GetField("stealthMax", BindingFlags.Public | BindingFlags.Instance);
            }

            _calGlobalProjType = asm.GetType("CalamityMod.Projectiles.CalamityGlobalProjectile");
            if (_calGlobalProjType != null)
                _stealthStrikeField = _calGlobalProjType.GetField("stealthStrike", BindingFlags.Public | BindingFlags.Instance);

            _downedBossType = asm.GetType("CalamityMod.Systems.DownedBossSystem");
        }

        // 反射调用 Player.GetModPlayer<T>()（tModLoader 仅提供泛型重载）
        private static object GetModPlayerInstance(Player player, Type modPlayerType)
        {
            var method = typeof(Player).GetMethods()
                .FirstOrDefault(m => m.Name == "GetModPlayer" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            if (method == null) return null;
            return method.MakeGenericMethod(modPlayerType).Invoke(player, null);
        }

        // 反射调用 Projectile.GetGlobalProjectile<T>()（tModLoader 仅提供泛型重载）
        private static object GetGlobalProjectileInstance(Projectile proj, Type globalType)
        {
            var method = typeof(Projectile).GetMethods()
                .FirstOrDefault(m => m.Name == "GetGlobalProjectile" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            if (method == null) return null;
            return method.MakeGenericMethod(globalType).Invoke(proj, null);
        }

        /// <summary>读取灾厄玩家潜伏值（灾厄缺失返回 0）。</summary>
        public static float GetPlayerStealth(Player player)
        {
            if (_calPlayerType == null) return 0f;
            var inst = GetModPlayerInstance(player, _calPlayerType);
            if (inst == null || _stealthField == null) return 0f;
            return (float)_stealthField.GetValue(inst);
        }

        /// <summary>读取灾厄玩家最大潜伏值（灾厄缺失返回 0）。</summary>
        public static float GetPlayerStealthMax(Player player)
        {
            if (_calPlayerType == null) return 0f;
            var inst = GetModPlayerInstance(player, _calPlayerType);
            if (inst == null || _stealthMaxField == null) return 0f;
            return (float)_stealthMaxField.GetValue(inst);
        }

        /// <summary>读取弹幕潜伏攻击标记（灾厄缺失返回 false）。</summary>
        public static bool IsStealthStrike(Projectile proj)
        {
            if (_calGlobalProjType == null) return false;
            var inst = GetGlobalProjectileInstance(proj, _calGlobalProjType);
            if (inst == null || _stealthStrikeField == null) return false;
            return (bool)_stealthStrikeField.GetValue(inst);
        }

        /// <summary>设置弹幕潜伏攻击标记（灾厄缺失时无操作）。</summary>
        public static void SetStealthStrike(Projectile proj, bool value)
        {
            if (_calGlobalProjType == null) return;
            var inst = GetGlobalProjectileInstance(proj, _calGlobalProjType);
            if (inst == null || _stealthStrikeField == null) return;
            _stealthStrikeField.SetValue(inst, value);
        }

        /// <summary>读取灾厄击杀存档布尔标记（灾厄缺失或字段不存在返回 false）。</summary>
        public static bool GetDownedFlag(string fieldName)
        {
            if (_downedBossType == null) return false;

            if (!_downedFlagCache.TryGetValue(fieldName, out var field))
            {
                field = _downedBossType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
                _downedFlagCache[fieldName] = field;
            }
            if (field == null) return false;
            return (bool)field.GetValue(null);
        }
    }
}
