using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace 可成长的孔雀翎
{
    // ========================================================================
    // ⚠ 本文件里**不要写面向玩家的文案**，一句都不要。
    // ========================================================================
    // 2026-09-27 清理：这里原本每个成员都挂着 `[Label("极简 (流畅优先) / Low")]` 和
    // `[Tooltip("...\n切换对话与星图的语言。")]` 这种**中英混排**的字符串。它们有两个问题：
    //
    //   1) 显示上没有任何作用（**已核对 tML 源码确认**，不是猜的）：
    //      `ConfigElement.OnBind()` 只读 `LabelKeyAttribute`（那个必须 `$` 开头的现代属性），
    //      标签文字一律来自 `ConfigManager.GetLocalizedLabel()`，也就是本地化文件。
    //      而 `LabelAttribute` / `TooltipAttribute` 已被 tML 标记 `[Obsolete]`，
    //      其值**只在 `Language.GetOrRegister(key, 默认值)` 里当"占位默认值"用** ——
    //      一旦 hjson 里有了这个键（我们有），属性就被完全忽略。
    //      所以这些字符串是彻头彻尾的死代码：删掉不会改变界面上的任何一个像素。
    //      （它们唯一的"功劳"是把中英混排的占位文本塞进了本地化导出文件，
    //       于是中文界面里出现过 `U I_界面设置 Header` 这种自动生成的垃圾。）
    //
    //   2) 就算它们会显示，中英混排本身也是错的：对两种语言的玩家都显示一半废话。
    //
    // 现在所有面向玩家的文字只存在于 `Localization/en-US_...hjson` 与 `zh-Hans_...hjson`，
    // 键按 tML 的自动规则生成，不需要也不应该写任何属性：
    //     成员标签/提示:  Configs.<配置类名>.<成员名>.Label / .Tooltip
    //     枚举选项:      Configs.<枚举类名>.<选项名>.Label / .Tooltip
    //     分节标题:      Configs.<配置类名>.Headers.<[Header] 的标识符>
    //
    // ⚠ 成员名与枚举项名是**存档兼容的一部分**（ModConfig 的 json 按成员名持久化），
    //   因为它们同时充当本地化键，改名会连带把玩家的配置和翻译一起打断 —— 只增不改。

    /// <summary>
    /// 粒子特效密度。三项的显示文字在本地化文件的 <c>Configs.ParticleEffectLevel.*.Label</c>。
    /// </summary>
    public enum ParticleEffectLevel
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// 模组自定义文本（剧情 / 星图）的语言。
    /// <para/>⚠ 这一项**只管本模组自己用 <c>MalachiteData.Loc</c> 输出的文本**，
    /// 与游戏语言是两套：物品名、配置标签、快捷键名始终跟随游戏语言（由 tML 决定）。
    /// </summary>
    public enum MalachiteLanguage
    {
        Chinese,
        English
    }

    public class MalachiteConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        // [Header] 不带 "$" 的值是**标识符**（不是文案），tML 据此拼出本地化键
        // `Configs.MalachiteConfig.Headers.<标识符>`。
        // 要求：不含空格（tML 会直接抛 ValueNotTranslationKeyException 把模组加载打断）。
        // 这里用纯 ASCII 标识符，而不是 `Performance_性能与画质` 那种"标识符里塞中文"——
        // 后者虽然能跑，但键里混中文可读性差、也不好和 en-US 文件对齐。
        [Header("Language")]
        [DefaultValue(MalachiteLanguage.Chinese)]
        [DrawTicks]
        public MalachiteLanguage Language;

        [Header("Performance")]
        [DefaultValue(ParticleEffectLevel.Medium)]
        [DrawTicks]
        public ParticleEffectLevel ParticleLevel;

        [Header("UI")]
        [DefaultValue(true)]
        public bool AutoPlayDialog;
    }
}
