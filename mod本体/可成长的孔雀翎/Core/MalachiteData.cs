using System;
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
            new StageInfo { TitleZh = "阶段 6 (繁花)", TitleEn = "Stage 6 (Blossom)", Color = Color.LimeGreen, LoreZh = "“哼哼，这片地儿现在归我啦！手下败将！”", LoreEn = "\"Heh, this place is mine now! Loser!\"", HintZh = "✧ 翎之指引：丛林神庙中沉睡着古老的石头造物。挡住了我们的去路。", HintEn = "✧ Malachite's Guidance: An ancient stone creation sleeps in the jungle temple, blocking our path." },
            new StageInfo { TitleZh = "阶段 7 (遗辉) [c/00FF00:- 力量解封 -]", TitleEn = "Stage 7 (Relic) [c/00FF00:- Power Unsealed -]", Color = Color.Yellow, LoreZh = "“当年被丢出丛林之后，我赌气说过一句‘我总有一天会回来的’。然后……我在山里迷了路，睡着之前还在想这句话。”", LoreEn = "\"After being thrown out of the jungle back then, I sulked and swore, ‘One day I'll come back.’ Then... I got lost in the mountains and fell asleep still thinking about that sentence.\"", HintZh = "✧ 翎之指引：地牢门口的诵经声越来越响……他们想唤来月上的东西。我们要快些才行。", HintEn = "✧ Malachite's Guidance: The chanting by the dungeon entrance grows louder... They want to summon something from the moon. We need to hurry." },
            new StageInfo { TitleZh = "阶段 8 (寻迹) [c/00FF00:- 瘟疫之源 -]", TitleEn = "Stage 8 (Tracing) [c/00FF00:- Plague Source -]", Color = Color.LightSeaGreen, LoreZh = "“那些诵经的人，应该是想唤来月亮上的那位存在。他们以为自己是在请神，却不知道自己请来的是什么。”", LoreEn = "\"Those chanting people must be trying to summon the being on the moon. They think they're inviting a god, but they have no idea what they're calling down.\"", HintZh = "✧ 翎之指引：月亮真的在靠近。别怕……嗯，其实我也有点怕。那我们牵紧一点，一起去。", HintEn = "✧ Malachite's Guidance: The moon is really closing in. Don't be afraid... Well, I'm a little scared too. Then let's hold hands tight and go together." },
            new StageInfo { TitleZh = "阶段 9 (月明)", TitleEn = "Stage 9 (Moonlight)", Color = Color.Cyan, LoreZh = "“都结束了。谢谢你陪我走完这么远……接下来，想去哪里，我都陪你去。”", LoreEn = "\"It's all over. Thank you for walking this far with me... Wherever you want to go next, I'll go with you.\"", HintZh = "✧ 翎之指引：村子口那棵老树结果子了，我们去摘两颗，歇一歇吧。", HintEn = "✧ Malachite's Guidance: The old tree at the village gate has borne fruit. Let's pick two and rest a while." },
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
            new List<(int, string)> { (0, "终于……能好好使出力气了。"), (0, "现在，我不会再受到重力的束缚了。"), (0, "让我为你开路吧！") },
            new List<(int, string)> { (3, "这些冰冷的钢铁……让我又想起了那片丛林。"), (3, "我答应过自己，总有一天要回去……把那朵花，咳，没事。"), (2, "你先别问，等我赢了，再讲给你听。"), (0, "总之你先别问！") },
            new List<(int, string)> { (0, "哼哼，这片地儿现在归我啦！手下败将！"), (0, "……咳，我是不是太大声了。"), (2, "我很久、很久没有这么开心过了。谢谢你。") },
            new List<(int, string)> { (0, "它倒下的时候，把庙顶的灰都震下来了。"), (0, "你没事就好。我看你挡得……有点吃力。"), (2, "明明可以多信任我一点嘛……") },
            new List<(int, string)> { (2, "月亮……好像真的要掉下来了。"), (0, "你怕吗？"), (2, "其实我有一点怕。不过——"), (0, "你既然不怕，那我也就不怕了。") },
            new List<(int, string)> { (0, "月亮已经回到天上去了。"), (0, "你做到了。我们也做到了。"), (2, "这一路的风雨，总算都过去了。"), (0, "接下来的岁月，就让我永远陪着你，哪儿也不去，好吗？") },
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
            new List<(int, string)> { (0, "Finally... I can put my strength to good use."), (0, "Now, gravity won't hold me back anymore."), (0, "Let me clear the way for you!") },
            new List<(int, string)> { (3, "These cold iron machines... make me think of the jungle again."), (3, "I promised myself I'd go back someday... to that flower. Ahem, never mind."), (2, "Don't ask me yet. I'll tell you once I win."), (0, "Just don't ask!") },
            new List<(int, string)> { (0, "Heh, this place is mine now! Loser!"), (0, "...Ahem, was I too loud?"), (2, "It's been so, so long since I was this happy. Thank you.") },
            new List<(int, string)> { (0, "When it fell, the dust on the temple roof came tumbling down."), (0, "I'm glad you're alright. You seemed... a little strained blocking that."), (2, "You could trust me a little more, you know...") },
            new List<(int, string)> { (2, "The moon... really does look like it's about to fall."), (0, "Are you scared?"), (2, "I'm a little scared, honestly. But—"), (0, "If you're not scared, then neither am I.") },
            new List<(int, string)> { (0, "The moon has returned to the sky."), (0, "You did it. We did it."), (2, "All the storms along the way are finally over."), (0, "For the years to come, let me stay by your side forever. Nowhere else. Okay?") },
            new List<(int, string)> { (3, "Even divine flames cannot warm that greedy heart."), (0, "If you feel cold, I will hold you close,"), (0, "And illuminate all the paths ahead for you.") },
            new List<(int, string)> { (3, "What an ugly and greedy monster."), (0, "Don't be afraid, hold my hand tight,"), (0, "I will cut down everything for you.") },
            new List<(int, string)> { (4, "Such burning blood..."), (0, "But even if this body melts away,"), (0, "I will carve out the path forward for you.") },
            new List<(int, string)> { (0, "Exo shattered, the Witch submits..."), (0, "The creator paid the price for his arrogance. The jungle's blood feud is finally settled."), (0, "Thank you for breaking this cage of fate for me."), (0, "For all the years to come, let me stay by your side forever, nowhere else, okay?") }
        };

        public static List<(int face, string text)> GetMilestoneDialogues(int stage)
        {
            stage = Math.Clamp(stage, 0, MilestoneDialoguesZh.Length - 1); // 越界防御（与 WillowGrowth.ClampStage 同风格）
            return IsEnglish ? MilestoneDialoguesEn[stage] : MilestoneDialoguesZh[stage];
        }

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