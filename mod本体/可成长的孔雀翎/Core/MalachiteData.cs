using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    public static class MalachiteData
    {
        public static bool IsEnglish => ModContent.GetInstance<MalachiteConfig>()?.Language == MalachiteLanguage.English;
        public static string Loc(string zh, string en) => IsEnglish ? en : zh;

        // 注：原 6 张成长数值表（DamageMultiplier/FlatAP/SpeedMult/数组/潜伏倍率）已迁至 Core\WillowGrowth.cs
        // （阶段 2b 独立成长系统，0~9 定稿 + 10~13 预置）

        public struct StageInfo
        {
            public string TitleZh, TitleEn;    
            public Color Color;     
            public string LoreZh, LoreEn;     
            public string HintZh, HintEn;     

            public string Title => Loc(TitleZh, TitleEn);
            public string Lore => Loc(LoreZh, LoreEn);
            public string Hint => Loc(HintZh, HintEn);
        }

        public static readonly StageInfo[] StageInfos = new StageInfo[]
        {
            new StageInfo { TitleZh = "阶段 0 (枯萎)", TitleEn = "Stage 0 (Withered)", Color = Color.Gray, LoreZh = "“翠绿的羽刃中传来微弱的悸动...她似乎受了很重的伤。”", LoreEn = "\"A faint throb comes from the emerald blade... She seems badly injured.\"", HintZh = "✧ 翎之指引：去向那夜晚徘徊的巨大眼球证明你的潜力，我会一直看着你的。", HintEn = "✧ Malachite's Guidance: Prove your potential to the giant eye wandering in the night. I'll be watching." },
            new StageInfo { TitleZh = "阶段 1 (锋芒)", TitleEn = "Stage 1 (Edge)", Color = Color.LightGreen, LoreZh = "“谢谢你...我感觉稍微好一些了。”", LoreEn = "\"Thank you... I feel a little better now.\"", HintZh = "✧ 翎之指引：去清除那片被腐化或猩红侵蚀的土地吧，我们需要更多的生机。", HintEn = "✧ Malachite's Guidance: Cleanse the land corrupted by evil. We need more vitality." },
            new StageInfo { TitleZh = "阶段 2 (邪噬)", TitleEn = "Stage 2 (Corruption)", Color = Color.MediumPurple, LoreZh = "“没关系的，只要能陪着你，这点污秽算不了什么。”", LoreEn = "\"It's okay. As long as I'm with you, this bit of filth is nothing.\"", HintZh = "✧ 翎之指引：地牢门前徘徊着古老的诅咒，一定要小心应对那具骷髅。", HintEn = "✧ Malachite's Guidance: An ancient curse lingers at the dungeon's entrance. Be careful with that skeleton." },
            new StageInfo { TitleZh = "阶段 3 (怨骨)", TitleEn = "Stage 3 (Resentment)", Color = Color.GhostWhite, LoreZh = "“不要勉强自己，就算拼尽全力，我也会守护在你身侧。”", LoreEn = "\"Don't push yourself too hard. I will protect you with all my might.\"", HintZh = "✧ 翎之指引：世界的深处，有一道由血肉铸成的梦魇之墙...不要害怕，放手去做吧。", HintEn = "✧ Malachite's Guidance: Deep in the world, there is a nightmare wall made of flesh... Don't be afraid, just do it." },
            new StageInfo { TitleZh = "阶段 4 (崩解)", TitleEn = "Stage 4 (Disintegration)", Color = Color.LightCoral, LoreZh = "“终于可以再次自由地飞翔了。让我为你破开前方的荆棘吧。”", LoreEn = "\"Finally, I can fly freely again. Let me cut through the thorns ahead for you.\"", HintZh = "✧ 翎之指引：那些在夜晚降临的冰冷机械，将是我们最好的试剑石。", HintEn = "✧ Malachite's Guidance: Those cold machines that descend at night will be our perfect whetstones." },
            new StageInfo { TitleZh = "阶段 5 (械动)", TitleEn = "Stage 5 (Mechanical)", Color = Color.LightSteelBlue, LoreZh = "“用机械强行催动的残躯而已”", LoreEn = "\"Just broken bodies forcibly driven by machinery.\"", HintZh = "✧ 翎之指引：丛林深处的花苞正在不安地悸动...那是我的故乡，陪我回去看看吧。", HintEn = "✧ Malachite's Guidance: A bulb deep in the jungle throbs uneasily... That is my homeland. Let's go back and see." },
            new StageInfo { TitleZh = "阶段 6 (繁花)", TitleEn = "Stage 6 (Blossom)", Color = Color.LimeGreen, LoreZh = "“熟悉的故土...却已物是人非。别担心，我会把这股剧毒转化为保护你的力量。”", LoreEn = "\"Familiar homeland... but everything has changed. Don't worry, I will turn this deadly poison into power to protect you.\"", HintZh = "✧ 翎之指引：丛林神庙中沉睡着古老的石头造物。挡住了我们的去路。", HintEn = "✧ Malachite's Guidance: An ancient stone creation sleeps in the jungle temple, blocking our path." },
            new StageInfo { TitleZh = "阶段 7 (遗辉) [c/00FF00:- 力量解封 -]", TitleEn = "Stage 7 (Relic) [c/00FF00:- Power Unsealed -]", Color = Color.Yellow, LoreZh = "“我已经恢复了全盛的姿态。接下来...是复仇的时刻了。”", LoreEn = "\"I have recovered my peak form. Next... it's time for revenge.\"", HintZh = "✧ 翎之指引：那个散播瘟疫的罪魁祸首——歌莉娅，就在丛林的某个角落。帮帮我，好吗？", HintEn = "✧ Malachite's Guidance: The culprit who spread the plague—Goliath—is hiding somewhere in the jungle. Help me, please?" },
            new StageInfo { TitleZh = "阶段 8 (寻迹) [c/00FF00:- 瘟疫之源 -]", TitleEn = "Stage 8 (Tracing) [c/00FF00:- Plague Source -]", Color = Color.LightSeaGreen, LoreZh = "“她并不是真正的源头。究竟是谁，用这些冰冷的机械亵渎了丛林……”", LoreEn = "\"She wasn't the true source. Who on earth desecrated the jungle with these cold machines...\"", HintZh = "✧ 翎之指引：天空变得压抑，邪教徒们正在召唤月亮上的那位暴君。抓紧我。", HintEn = "✧ Malachite's Guidance: The sky turns oppressive. Cultists are summoning the tyrant from the moon. Hold on tight." },
            new StageInfo { TitleZh = "阶段 9 (月明)", TitleEn = "Stage 9 (Moonlight)", Color = Color.Cyan, LoreZh = "“沐浴在月光之下，翎羽因你而闪烁。”", LoreEn = "\"Bathed in moonlight, my feathers shimmer for you.\"", HintZh = "✧ 翎之指引：地狱与神圣的交汇之处，似乎有一位天神在徘徊。小心点哦。", HintEn = "✧ Malachite's Guidance: Where hell and hallow intersect, a goddess seems to wander. Be careful." },
            new StageInfo { TitleZh = "阶段 10 (亵渎)", TitleEn = "Stage 10 (Profaned)", Color = Color.Orange, LoreZh = "“即使是神明，若要阻挡你前行，我也会将其斩裂。”", LoreEn = "\"Even if a god tries to block your path, I will cleave them apart.\"", HintZh = "✧ 翎之指引：宇宙中游荡的贪婪蠕虫正在觊觎你的灵魂。有我在，它休想得逞。", HintEn = "✧ Malachite's Guidance: A greedy worm wandering in the cosmos covets your soul. With me here, it won't succeed." },
            new StageInfo { TitleZh = "阶段 11 (破渊)", TitleEn = "Stage 11 (Abyssal)", Color = Color.Purple, LoreZh = "“不要怕那些深渊的低语，只要我在，绝不让黑暗触碰你分毫。”", LoreEn = "\"Don't fear the whispers of the abyss. As long as I'm here, darkness won't touch you.\"", HintZh = "✧ 翎之指引：只剩下那只忠诚的丛林之龙了。用它的龙炎来为我们淬火吧。", HintEn = "✧ Malachite's Guidance: Only that loyal jungle dragon remains. Let's temper ourselves with its dragonfire." },
            new StageInfo { TitleZh = "阶段 12 (焚风)", TitleEn = "Stage 12 (Scorching Wind)", Color = Color.DarkRed, LoreZh = "“烈火与狂风，不过是加冕的洗礼。别怕，我会倾尽一切。”", LoreEn = "\"Fire and gale are merely the baptism of coronation. Don't be afraid, I will give my all.\"", HintZh = "✧ 翎之指引：万事俱备。去吧，一起战胜那至尊的魔女与那可憎的造物！", HintEn = "✧ Malachite's Guidance: Everything is ready. Go, let's defeat the supreme witch and that abominable creation together!" },
            new StageInfo { TitleZh = "阶段 13 (终焉) [c/FFD700:- 永恒誓约 -]", TitleEn = "Stage 13 (Finale) [c/FFD700:- Eternal Vow -]", Color = Color.Gold, LoreZh = "“星流已碎，魔女臣服。傲慢终究导致了他的毁灭，现在，我将永远守护你。”", LoreEn = "\"Exo broken, the witch submits. Arrogance led to his downfall. Now, I will guard you for eternity.\"", HintZh = "✧ 翎之指引：一切都结束了。接下来，想去哪里散步呢？", HintEn = "✧ Malachite's Guidance: It's all over. Where would you like to take a walk next?" }
        };

        public static readonly List<(int face, string text)>[] MilestoneDialoguesZh = new List<(int, string)>[]
        {
            new List<(int, string)>(), 
            new List<(int, string)> { (2, "虽然只是一只眼球……"), (0, "但这丝生机让我恢复了些许力气。"), (0, "谢谢你，愿意带着我。") },
            new List<(int, string)> { (4, "抱歉..."), (4, "吸收这些污秽的血肉让我感到有些难受。"), (0, "但只要能帮上你的忙，我什么都可以忍受。") },
            new List<(int, string)> { (0, "古老的怨念消散了。"), (1, "你没受伤吧？"), (4, "快让我看看，怎么这么不小心……"), (0, "接下来的路，我们也要一起走。") },
            new List<(int, string)> { (0, "封印解除。"), (0, "现在，我不会再受到重力的束缚了。"), (0, "让我为你开路吧！") },
            new List<(int, string)> { (3, "这些冰冷的钢铁..."), (3, "让我想起了当初那个毁掉丛林的家伙。"), (2, "我回忆起了一些以前的阵法。"), (0, "请挥舞我吧，我会成为你的利刃。") },
            new List<(int, string)> { (2, "久违的丛林气息..."), (4, "只是，这里已经被瘟疫污染得太深了。"), (0, "小心点，千万不要碰到那些毒素。"), (0, "我会把它们全都挡下来的。") },
            new List<(int, string)> { (0, "轰然倒塌的石块，见证了你的强大。"), (0, "我已经找回了当年所有的力量。"), (3, "但是...那个毁了我的仇人，还在丛林里。"), (4, "陪我一起去找她报仇，好不好？") },
            new List<(int, string)> { (0, "终于...终于结束了。"), (4, "那些被瘟疫折磨的生灵们，总算可以安息了。"), (2, "……"), (2, "不……不对，我能感觉到，她并不是瘟疫的源头……"), (2, "在歌莉娅被改造的残躯之下，我嗅到了某种……我也不好说那是什么。"), (3, "幕后黑手绝对另有其人。"), (4, "……麻烦你了，帮了我这么久。我想先静一静。") },
            new List<(int, string)> { (2, "连那种不可名状的存在，你都能将其击败..."), (0, "只要待在你身边，我就会感到无比安心。"), (0, "让我为你献上更美的剑舞吧。") },
            new List<(int, string)> { (3, "神圣的火焰，也无法温暖那颗贪婪的心。"), (0, "如果你感到寒冷，我会靠紧你，"), (0, "为你照亮所有的前路。") },
            new List<(int, string)> { (3, "真是丑陋又贪婪的怪物。"), (0, "不要怕，抓紧我的手，"), (0, "我会为你斩断一切。") },
            new List<(int, string)> { (4, "好烫的血..."), (0, "但是，哪怕这副身躯被融化，"), (0, "我也会为你斩开前行的路。") },
            new List<(int, string)> { (0, "星流崩碎，魔女臣服……"), (0, "造物主为他的傲慢付出了代价，丛林的血仇，终于彻底了结了。"), (0, "谢谢你，帮我斩断了这宿命的囚笼。"), (0, "接下来的岁月，就让我永远陪着你，哪儿也不去，好吗？") }
        };

        public static readonly List<(int, string)>[] MilestoneDialoguesEn = new List<(int, string)>[]
        {
            new List<(int, string)>(), 
            new List<(int, string)> { (2, "Though it's just a giant eye..."), (0, "This trace of vitality restored some of my strength."), (0, "Thank you for bringing me along.") },
            new List<(int, string)> { (4, "Sorry..."), (4, "Absorbing this tainted flesh makes me feel a bit sick."), (0, "But as long as it helps you, I can endure anything.") },
            new List<(int, string)> { (0, "The ancient grudge has dissipated."), (1, "Are you hurt?"), (4, "Let me see, you should be more careful..."), (0, "We'll walk the path ahead together.") },
            new List<(int, string)> { (0, "The seal is lifted."), (0, "Now, I'm no longer bound by gravity."), (0, "Let me clear the way for you!") },
            new List<(int, string)> { (3, "This cold steel..."), (3, "Reminds me of the one who ruined the jungle."), (2, "I've recalled some of my old sword formations."), (0, "Wield me, I shall be your blade.") },
            new List<(int, string)> { (2, "The long-lost scent of the jungle..."), (4, "It's just... heavily polluted by the plague now."), (0, "Be careful, don't touch those toxins."), (0, "I will block them all for you.") },
            new List<(int, string)> { (0, "The crumbling stones bear witness to your strength."), (0, "I have recovered all my former power."), (3, "But... the enemy who ruined me is still in the jungle."), (4, "Accompany me to get revenge, will you?") },
            new List<(int, string)> { (0, "Finally... it's finally over."), (4, "The souls tormented by the plague can rest in peace now."), (2, "..."), (2, "No... wait, I can feel it. She wasn't the true source..."), (2, "Beneath Goliath's modified remains, I sensed something... I can't even describe it."), (3, "The mastermind is definitely someone else."), (4, "...Thank you for helping me all this time. I want to be alone for a moment.") },
            new List<(int, string)> { (2, "You even defeated such an indescribable entity..."), (0, "As long as I'm by your side, I feel incredibly safe."), (0, "Let me offer you an even more beautiful sword dance.") },
            new List<(int, string)> { (3, "Even divine flames cannot warm that greedy heart."), (0, "If you feel cold, I will hold you close,"), (0, "And illuminate all the paths ahead for you.") },
            new List<(int, string)> { (3, "What an ugly and greedy monster."), (0, "Don't be afraid, hold my hand tight,"), (0, "I will cut down everything for you.") },
            new List<(int, string)> { (4, "Such burning blood..."), (0, "But even if this body melts away,"), (0, "I will carve out the path forward for you.") },
            new List<(int, string)> { (0, "Exo shattered, the Witch submits..."), (0, "The creator paid the price for his arrogance. The jungle's blood feud is finally settled."), (0, "Thank you for breaking this cage of fate for me."), (0, "For all the years to come, let me stay by your side forever, nowhere else, okay?") }
        };

        public static List<(int face, string text)> GetMilestoneDialogues(int stage) => IsEnglish ? MilestoneDialoguesEn[stage] : MilestoneDialoguesZh[stage];

        public static Color LightGreen = Color.LightGreen;
        public static Color MediumPurple = Color.MediumPurple;
        public static Color GhostWhite = Color.GhostWhite;
        public static Color LightCoral = Color.LightCoral;
        public static Color LightSteelBlue = Color.LightSteelBlue;
        public static Color LimeGreen = Color.LimeGreen;
        public static Color LightSeaGreen = Color.LightSeaGreen;
    }

    public static class MalachiteCache
    {
        // ---- 本模组自建类型缓存（v2 起唯一身份来源；灾厄字段已按阶段 2 摘除）----

        /// <summary>本模组独立武器类型（孔雀柳刃）。</summary>
        public static int NativeMalachiteItem => ModContent.ItemType<PeacockWillowBlade>();

        /// <summary>本模组普攻飞刀弹幕类型。</summary>
        public static int NativeProjType => ModContent.ProjectileType<MalachiteProj>();

        /// <summary>本模组潜伏射线弹幕类型。</summary>
        public static int NativeBoltType => ModContent.ProjectileType<MalachiteBolt>();

        /// <summary>统一判断：是否为孔雀柳刃（本模组武器；灾厄孔雀翎已互不干涉）。</summary>
        public static bool IsMalachiteItem(Item item)
            => item != null && item.type == NativeMalachiteItem;
    }
}