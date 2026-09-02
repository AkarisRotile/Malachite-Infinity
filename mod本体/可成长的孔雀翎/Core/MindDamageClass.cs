using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 「念」伤害类（前身 MalachiteDamageClass，用户 2026-09-02 重命名拍板 D13）。
    /// 继承语义（拍板 D14/D15）：
    /// - 伤害与攻速权重一致：通用(Generic)=100%、召唤=100%、近战/远程=25%、
    ///   其它模组任意伤害类型=25%、魔法/投掷=0%（攻速随伤害权重，不再单列）。
    /// - 暴击率/护甲穿透/击退：只吃通用(Generic)=100%，职业专属一律不继承。
    /// - 词缀：允许各职业词缀生效（沿用旧行为）；效果继承限 近战/远程/召唤。
    /// </summary>
    public class MindDamageClass : DamageClass
    {
        public static MindDamageClass Instance => ModContent.GetInstance<MindDamageClass>();

        public override LocalizedText DisplayName => Language.GetText("Mods.可成长的孔雀翎.DamageClasses.MindDamageClass.DisplayName");

        /// <summary>伤害与攻速同权重 w（其余统计 0）。</summary>
        private static StatInheritanceData DmgAndSpeed(float w) => new StatInheritanceData(
            damageInheritance: w,
            critChanceInheritance: 0f,
            attackSpeedInheritance: w,
            armorPenInheritance: 0f,
            knockbackInheritance: 0f);

        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
        {
            // 通用（"全职业/全部伤害+%"）：全统计 100%
            if (damageClass == DamageClass.Generic)
                return StatInheritanceData.Full;

            // Default 类不享受任何加成（同引擎默认）
            if (damageClass == DamageClass.Default)
                return StatInheritanceData.None;

            // 召唤：伤害与攻速 100%
            if (damageClass == DamageClass.Summon)
                return DmgAndSpeed(1f);

            // 近战 / 远程：伤害与攻速 25%
            if (damageClass == DamageClass.Melee || damageClass == DamageClass.Ranged)
                return DmgAndSpeed(0.25f);

            // 魔法 / 投掷：不继承
            if (damageClass == DamageClass.Magic || damageClass == DamageClass.Throwing)
                return StatInheritanceData.None;

            // 其余（含其它模组新增的任何伤害类型、以及原版混合变体）：统一 25%（伤害+攻速）
            return DmgAndSpeed(0.25f);
        }

        // 效果继承：近战/远程/召唤系装备效果（如岩浆石、神圣弹之类）可被本类触发
        public override bool GetEffectInheritance(DamageClass damageClass)
            => damageClass == DamageClass.Melee
            || damageClass == DamageClass.Ranged
            || damageClass == DamageClass.Summon;

        // 词缀继承：允许各职业词缀（传奇/凶残等）对本武器生效
        public override bool GetPrefixInheritance(DamageClass damageClass) => true;

        public override bool UseStandardCritCalcs => true;
    }
}
