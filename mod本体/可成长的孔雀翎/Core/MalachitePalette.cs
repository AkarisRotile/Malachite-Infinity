using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 全局调色板（game-art-guide 规范）：
    /// 风格锁定「Terraria 原版像素风」，模组严格复用目标游戏调色板与素材尺寸。
    /// 五色定位：背景 / 主色 / 强调 / 危险 / 文字。色值全部集中于此，禁止散落硬编码。
    /// 同一色系明暗变化按 HSL 调整（改 L 不改 H），保证和谐。
    /// </summary>
    public static class MalachitePalette
    {
        // ============ 背景色（画面 60%，最暗） ============
        /// <summary>深绿黑：特效底层 / HUD 背景 / 弹窗遮罩底色。</summary>
        public static readonly Color PanelBackground = new Color(14, 27, 18, 200);
        /// <summary>更深的纯黑绿：极端背景。</summary>
        public static readonly Color BackgroundDeep = new Color(8, 16, 11, 235);

        // ============ 主色（品牌/主角，30%）孔雀翎翠绿 ============
        /// <summary>主色 #3DDC84（H≈142）：羽刃、普攻光带。</summary>
        public static readonly Color PrimaryGreen = new Color(61, 220, 132);
        /// <summary>主色亮阶（L↑）：高光、白绿混合。</summary>
        public static readonly Color GreenBright = new Color(124, 255, 184);
        /// <summary>主色暗阶（L↓）：暗部、火花边缘。</summary>
        public static readonly Color GreenDark = new Color(30, 122, 70);
        /// <summary>主色深阶（L↓↓）：拖尾根部、光带暗端。</summary>
        public static readonly Color GreenDeep = new Color(18, 66, 40);

        // ============ 强调色（交互/潜伏/蓄力，10%） ============
        /// <summary>金 #FFD54F：潜伏可用、蓄力满、羽尖高光。</summary>
        public static readonly Color AccentGold = new Color(255, 213, 79);
        /// <summary>青 #4FD8E8：冰封、月光、射线。</summary>
        public static readonly Color AccentCyan = new Color(79, 216, 232);
        /// <summary>紫 #B388FF：星辉、渊、幻影。</summary>
        public static readonly Color AccentPurple = new Color(179, 136, 255);

        // ============ 危险/警告色（敌人/爆炸/扣血） ============
        /// <summary>红 #E24B4A：爆炸、渊火、敌人标记。</summary>
        public static readonly Color DangerRed = new Color(226, 75, 74);
        /// <summary>橙 #FF8A3D：爆裂、瘟疫预警。</summary>
        public static readonly Color DangerOrange = new Color(255, 138, 61);

        // ============ 文字色（前景 + 次级） ============
        /// <summary>浅绿白：主文字。</summary>
        public static readonly Color TextLight = new Color(232, 245, 233);
        /// <summary>灰绿：次级文字 / 未激活。</summary>
        public static readonly Color TextDim = new Color(160, 190, 170);

        // ============ 通用 ============
        public static readonly Color White = Color.White;
        public static readonly Color Transparent = Color.Transparent;
        public static readonly Color PureBlack = Color.Black;

        // ============================================================================
        // 星图 3.0「翎羽星网」专用色（来源：AGY 设计咨询稿 写法\_agy\reply_01~03）
        // ============================================================================
        // 优先级声明（2026-09-27 用户拍板）：**与 AGY 设计稿冲突时以 AGY 为准**。
        // 原 R5「颜色只许来自本文件」的精神不变（仍然集中，便于一处调参），
        // 但"必须用绿黑系"这层隐含约束被解除 —— AGY 给的深蓝黑面板就是比绿黑好看。
        //
        // 说明：星网节点/连线用的 #3DDC84 / #7CFFB8 / #1E7A46 / #124228 / #FFD54F /
        // #4FD8E8 / #E8F5E9 与主调色板**同值**，因此不另立名目，直接复用上面的字段。

        /// <summary>星网面板底色 #0D131F（AGY 稿给 85% 不透明度）——比绿黑更冷、更"深空"。</summary>
        public static readonly Color StarCardBg = new Color(13, 19, 31, 217);

        /// <summary>星网面板/详情卡边框 #3A4D6B（石板蓝）。</summary>
        public static readonly Color StarCardBorder = new Color(58, 77, 107);

        /// <summary>星网面板边框暗阶（未激活/次级分隔）。</summary>
        public static readonly Color StarCardBorderDim = new Color(29, 38, 53);

        /// <summary>星网详情卡上的冷白正文（用于 AGY 稿的字段色，比 TextLight 更冷）。</summary>
        public static readonly Color StarInk = new Color(220, 228, 242);

        /// <summary>星网详情卡次级文字（冷灰蓝）。</summary>
        public static readonly Color StarInkDim = new Color(138, 152, 178);
    }
}
