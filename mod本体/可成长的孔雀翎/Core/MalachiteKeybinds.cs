using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    public class MalachiteKeybinds : ModSystem
    {
        // 声明一个全局可访问的快捷键变量
        public static ModKeybind DialogueKey { get; private set; }
        public static ModKeybind DomainKey { get; private set; }
        /// <summary>近战形态攻击键（ES 流原型；可在 设置→控件→模组 中自由改键）。</summary>
        public static ModKeybind MeleeKey { get; private set; }

        public override void Load()
        {
            // 注册快捷键，默认绑定为鼠标右键 (Mouse2)
            // 玩家可以在泰拉瑞亚的“设置 -> 控件 -> 下滑到模组控制”中找到并修改它
            DialogueKey = KeybindLoader.RegisterKeybind(Mod, "与孔雀翎对话 (Talk to Malachite)", "Mouse2");
            // 碧翎念涌（星核技能键，默认 V）
            // 注：字段名仍叫 DomainKey —— 改名要同步 ProcessTriggers 与所有引用，收益不大，留个名字上的历史痕迹无妨。
            DomainKey = KeybindLoader.RegisterKeybind(Mod, "碧翎念涌 (Verdant Plumage)", "V");
            // 近战攻击（原型测试键，默认 F；按住连段，设置里可自由改键）
            MeleeKey = KeybindLoader.RegisterKeybind(Mod, "近战攻击 (Melee Attack)", "F");
        }

        public override void Unload()
        {
            // 卸载模组时清空，防止内存泄漏
            DialogueKey = null;
            DomainKey = null;
            MeleeKey = null;
        }
    }
}