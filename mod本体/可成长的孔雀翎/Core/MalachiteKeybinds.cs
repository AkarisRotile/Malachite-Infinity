using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    public class MalachiteKeybinds : ModSystem
    {
        // 声明一个全局可访问的快捷键变量
        public static ModKeybind DialogueKey { get; private set; }
        public static ModKeybind DomainKey { get; private set; }

        public override void Load()
        {
            // 注册快捷键，默认绑定为鼠标右键 (Mouse2)
            // 玩家可以在泰拉瑞亚的“设置 -> 控件 -> 下滑到模组控制”中找到并修改它
            DialogueKey = KeybindLoader.RegisterKeybind(Mod, "与孔雀翎对话 (Talk to Malachite)", "Mouse2");
            // 领域展开（原型测试键，默认 V）
            DomainKey = KeybindLoader.RegisterKeybind(Mod, "领域展开 (Domain Expansion)", "V");
        }

        public override void Unload()
        {
            // 卸载模组时清空，防止内存泄漏
            DialogueKey = null;
            DomainKey = null;
        }
    }
}