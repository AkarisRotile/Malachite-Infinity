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

        // ====================================================================
        // ⚠ 关于下面三个名字（改动前必读）
        // ====================================================================
        // 1) 这个名字**不是**显示用的文案，而是**本地化键**与**玩家改键的持久化名**：
        //      本地化键   = Mods.可成长的孔雀翎.Keybinds.<名字>.DisplayName
        //      存档里的名 = 可成长的孔雀翎/<名字>      （存于玩家输入配置）
        //    所以：**改名字 = 玩家已设的键位被重置**，而且旧键的本地化条目会变成死键。
        //    键一旦发布就只能增、不能改名。
        //
        // 2) 2026-09-27 改过一次名（原本是带空格括号的 `碧翎念涌 (Verdant Plumage)` 等）：
        //    原因有两个，第二个才是真正的坑——
        //      · tML 的 KeybindLoader 文档明确建议名字不要含空格；
        //      · 更糟的是**原来的名字本身就是中文文案**，于是"显示名"和"键名"被焊死在一起，
        //        结果本地化条目一旦缺失，界面上就会直接印出那条又长又混的语言键原文
        //        （`Mods.可成长的孔雀翎.Keybinds.碧翎念涌 (Verdant Plumage) DisplayName`），
        //        它比控件列表的标签栏宽 → 换行 → 压住下一行，看起来就是"糊成一坨"。
        //    现在名字是纯 ASCII 标识符，**所有面向玩家的文字都只存在于 Localization/*.hjson**。
        //    代价：玩家原有的这三个键位会重置回默认值（默认值恰好就是 V / F / 鼠标右键）。
        //
        // 3) 本地化条目在 en-US_Mods.可成长的孔雀翎.hjson 与 zh-Hans_Mods.可成长的孔雀翎.hjson
        //    的 Keybinds 节里，**两个文件都要有**，否则中文/英文界面会缺字或回退成键原文。
        //    另外：快捷键**没有 Tooltip**，想说明的行为只能写进 DisplayName 且必须短。

        public override void Load()
        {
            // 注册快捷键，默认绑定为鼠标右键 (Mouse2)
            // 玩家可以在泰拉瑞亚的"设置 -> 控件 -> 下滑到模组控制"中找到并修改它
            DialogueKey = KeybindLoader.RegisterKeybind(Mod, "TalkToMalachite", "Mouse2");
            // 碧翎念涌（星核技能键，默认 V；2026-09-27 起为**开/关**，开启后无限持续）
            // 注：字段名仍叫 DomainKey —— 改名要同步 ProcessTriggers 与所有引用，收益不大，留个名字上的历史痕迹无妨。
            DomainKey = KeybindLoader.RegisterKeybind(Mod, "VerdantPlumage", "V");
            // 近战攻击（原型测试键，默认 F；按住连段，设置里可自由改键）
            MeleeKey = KeybindLoader.RegisterKeybind(Mod, "MeleeAttack", "F");
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
