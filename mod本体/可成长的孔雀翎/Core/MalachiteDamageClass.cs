using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 本模组自建伤害类：孔雀翎武器与浮游剑阵共用的伤害类型。
    /// 继承规则（用户设计）：通用加成（Generic/Default）全量继承，
    /// 各职业专属加成（近战/远程/魔法/召唤/投掷及他模组职业）按 25% 比例继承——
    /// 例如同时装备近战攻速拳套与召唤伤害饰品时，两者加成都能生效（各取 25%）。
    /// </summary>
    public class MalachiteDamageClass : DamageClass
    {
        public override LocalizedText DisplayName => Language.GetText("Mods.可成长的孔雀翎.DamageClasses.MalachiteDamageClass.DisplayName");

        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
        {
            // 通用加成（全职业共享的饰品/增益）全量生效
            if (damageClass == DamageClass.Generic || damageClass == DamageClass.Default)
                return StatInheritanceData.Full;

            // 职业专属加成：各属性继承 25%
            return new StatInheritanceData(
                damageInheritance: 0.25f,
                critChanceInheritance: 0.25f,
                attackSpeedInheritance: 0.25f,
                armorPenInheritance: 0.25f,
                knockbackInheritance: 0.25f
            );
        }

        // 效果继承：允许职业装备效果（如饰品触发）作用于本类
        public override bool GetEffectInheritance(DamageClass damageClass) => true;

        // 词缀继承：允许各职业词缀生效
        public override bool GetPrefixInheritance(DamageClass damageClass) => true;

        public override bool UseStandardCritCalcs => true;
    }
}
