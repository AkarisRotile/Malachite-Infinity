using System;
using System.Collections.Generic;
using System.Linq;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 对话组静态数据库。
    /// 所有对话统一从 DialogGroup 播放，历史系统只记录组 ID。
    /// </summary>
    public static class DialogDatabase
    {
        public const string CatMilestone = "milestone";
        public const string CatRightClick = "right_click";
        public const string CatDaily = "daily";
        public const string CatDeath = "death";
        public const string CatTruth = "truth";
        public const string CatEnv = "env";

        public static Dictionary<string, DialogGroup> AllGroups = new Dictionary<string, DialogGroup>();
        public static List<DialogGroup> OrderedGroups = new List<DialogGroup>();

        // 右键对话 ID 池
        public static readonly string[] RightClickStage1 =
        {
            "right_click_stage1_1",
            "right_click_stage1_2",
            "right_click_stage1_3"
        };

        public static readonly string[] RightClickStage2 =
        {
            "right_click_stage2_1",
            "right_click_stage2_2",
            "right_click_stage2_3"
        };

        public static readonly string[] RightClickStage3 =
        {
            "right_click_stage3_1",
            "right_click_stage3_2",
            "right_click_stage3_3",
            "right_click_stage3_4"
        };

        public static readonly string[] RightClickNight =
        {
            "right_click_night_1",
            "right_click_night_2"
        };

        public static readonly string[] RightClickLowHealth =
        {
            "right_click_lowhealth_1",
            "right_click_lowhealth_2"
        };

        public static readonly string[] Idle =
        {
            "idle_1",
            "idle_2",
            "idle_3"
        };

        static DialogDatabase()
        {
            RegisterAll();
        }

        private static void Add(
            string id,
            string nameZh,
            string nameEn,
            string category,
            int sortOrder,
            string unlockZh,
            string unlockEn,
            IEnumerable<(int, string)> zh,
            IEnumerable<(int, string)> en)
        {
            var group = new DialogGroup(
                id,
                nameZh,
                nameEn,
                category,
                sortOrder,
                unlockZh,
                unlockEn,
                zh,
                en);

            AllGroups[id] = group;
            OrderedGroups.Add(group);
        }

        private static void RegisterAll()
        {
            RegisterRightClickDialogs();
            RegisterTruthDialogs();
            RegisterDailyDialogs();
            RegisterDeathDialogs();
            RegisterMilestoneDialogs();

            OrderedGroups = OrderedGroups.OrderBy(g => g.SortOrder).ToList();
        }

        private static void RegisterRightClickDialogs()
        {
            // ========== 右键对话：阶段 1 ==========
            Add(
                "right_click_stage1_1",
                "日常1", "Daily 1",
                CatRightClick, 10,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (1, "嗯？怎么突然看我？"), (1, "我脸上有树叶吗？") },
                new[] { (1, "Hmm? Why are you looking at me suddenly?"), (1, "Is there a leaf on my face?") });

            Add(
                "right_click_stage1_2",
                "日常2", "Daily 2",
                CatRightClick, 11,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (4, "虽然我现在能做的还很少..."), (0, "但我会努力去变强的。") },
                new[] { (4, "Though there's not much I can do right now..."), (0, "I will work hard to become stronger.") });

            Add(
                "right_click_stage1_3",
                "日常3", "Daily 3",
                CatRightClick, 12,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (0, "你在听吗？"), (0, "只要能待在你身边，我就很安心。") },
                new[] { (0, "Are you listening?"), (0, "As long as I can stay by your side, I feel at ease.") });

            // ========== 右键对话：阶段 2 ==========
            Add(
                "right_click_stage2_1",
                "日常4", "Daily 4",
                CatRightClick, 20,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (0, "我感觉力量正在不断涌现！"), (0, "现在的我，应该能帮上更多忙了吧？") },
                new[] { (0, "I feel power surging constantly!"), (0, "I should be able to help out more now, right?") });

            Add(
                "right_click_stage2_2",
                "日常5", "Daily 5",
                CatRightClick, 21,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (0, "只要是你的意愿，"), (3, "我会为你扫平一切。") },
                new[] { (0, "If it is your will,"), (3, "I will sweep away everything in your path.") });

            Add(
                "right_click_stage2_3",
                "日常6", "Daily 6",
                CatRightClick, 22,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (2, "累了吗？"), (0, "交给我，稍微休息一下吧。") },
                new[] { (2, "Tired?"), (0, "Leave it to me and take a short rest.") });

            Add(
                "right_click_stage2_truth",
                "机械造物", "Machinations",
                CatRightClick, 23,
                "发现嘉登瘟疫真相后右键对话触发", "Triggered by right-click dialogue after discovering the plague truth",
                new[] { (3, "冰冷的机械没有灵魂..."), (3, "我会替丛林讨回公道。") },
                new[] { (3, "Cold machines have no soul..."), (3, "I will claim justice for the jungle with my blade.") });

            Add(
                "right_click_stage2_goliath",
                "歌莉娅", "Goliath",
                CatRightClick, 24,
                "阶段 8 后未发现真相时右键对话触发", "Triggered by right-click dialogue before the truth is discovered at stage 8+",
                new[] { (2, "歌莉娅究竟是怎么变成这样的……？"), (0, "我们一定要查出真相。") },
                new[] { (2, "How exactly did Goliath become like this...?"), (0, "We must find out the truth.") });

            // ========== 右键对话：阶段 3 ==========
            Add(
                "right_click_stage3_1",
                "日常7", "Daily 7",
                CatRightClick, 30,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (0, "一切都结束了呢..."), (0, "接下来的漫长岁月，我会一直陪着你。") },
                new[] { (0, "It's all over, isn't it..."), (0, "For the long years ahead, I will always be with you.") });

            Add(
                "right_click_stage3_2",
                "日常8", "Daily 8",
                CatRightClick, 31,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (0, "无论你想去哪里，我都愿意跟随。"), (0, "你，就是我的整个世界。") },
                new[] { (0, "Wherever you want to go, I will follow."), (0, "You are my entire world.") });

            Add(
                "right_click_stage3_3",
                "日常9", "Daily 9",
                CatRightClick, 32,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (0, "不要总是这么盯着我看呀..."), (2, "就算是我，也会害羞的...") },
                new[] { (0, "Don't just keep staring at me like that..."), (2, "Even I would get embarrassed...") });

            Add(
                "right_click_stage3_4",
                "日常10", "Daily 10",
                CatRightClick, 33,
                "右键对话随机触发", "Trigger right-click dialogue randomly",
                new[] { (0, "能在这个破碎的世界遇见你..."), (0, "也是我此生最大的幸运。") },
                new[] { (0, "Meeting you in this broken world..."), (0, "Is the greatest fortune of my life.") });

            // ========== 右键环境 ==========
            Add(
                "right_click_jungle_low",
                "丛林·旧忆", "Jungle Memory",
                CatEnv, 40,
                "阶段 8 前在丛林右键对话触发", "Right-click dialogue in jungle before stage 8",
                new[] { (2, "这里的气息...虽然微弱，但依旧熟悉。"), (0, "小心那些毒雾，它们很危险。") },
                new[] { (2, "The aura here... faint, but still familiar."), (0, "Be careful of the toxic mist, it's very dangerous.") });

            Add(
                "right_click_jungle_high",
                "丛林·新风", "Jungle Breeze",
                CatEnv, 41,
                "阶段 8 后在丛林右键对话触发", "Right-click dialogue in jungle after stage 8",
                new[] { (0, "丛林的微风重新流动了。"), (0, "这片土地，总有一天会变回曾经那美丽的模样吧。") },
                new[] { (0, "The jungle breeze is flowing again."), (0, "This land will one day return to its beautiful appearance.") });

            Add(
                "right_click_beach",
                "海边", "Seaside",
                CatEnv, 42,
                "在海滩右键对话触发", "Right-click dialogue at beach",
                new[] { (0, "海风吹在身上真的很舒服。"), (0, "如果可以的话，真想一直这样和你坐在这里。") },
                new[] { (0, "The sea breeze feels really nice."), (0, "If possible, I'd like to just sit here with you forever.") });

            Add(
                "right_click_night_1",
                "夜晚·警戒", "Night Vigil",
                CatEnv, 43,
                "夜晚右键对话触发", "Right-click dialogue at night",
                new[] { (0, "晚上的怪物变多了。"), (0, "我会照亮你的前路，放手去战斗吧。") },
                new[] { (0, "There are more monsters at night."), (0, "I will light your way, go fight without restraint.") });

            Add(
                "right_click_night_2",
                "夜晚·陪伴", "Night Company",
                CatEnv, 44,
                "夜晚右键对话触发", "Right-click dialogue at night",
                new[] { (0, "夜有点深了，冷不冷？"), (0, "可以靠我近一点。") },
                new[] { (0, "The night is getting deep, are you cold?"), (0, "You can lean a little closer to me.") });

            Add(
                "right_click_rain",
                "雨", "Rain",
                CatEnv, 45,
                "下雨时右键对话触发", "Right-click dialogue while raining",
                new[] { (2, "下雨了呢..."), (0, "靠近一点，我给你挡着。") },
                new[] { (2, "It's raining..."), (0, "Come closer, I can shelter you from the rain.") });

            Add(
                "right_click_lowhealth_1",
                "重伤·急喊", "Low HP Warning",
                CatEnv, 46,
                "生命值低于 50% 时右键对话触发", "Right-click dialogue while below 50% HP",
                new[] { (4, "你受伤了！流了好多血！"), (4, "快退回来，不要再勉强了！") },
                new[] { (4, "You're hurt! Bleeding so much!"), (4, "Retreat quickly, don't force yourself anymore!") });

            Add(
                "right_click_lowhealth_2",
                "重伤·心疼", "Low HP Grief",
                CatEnv, 47,
                "生命值低于 50% 时右键对话触发", "Right-click dialogue while below 50% HP",
                new[] { (4, "看到你受这么多伤，我……") },
                new[] { (4, "Seeing you take so much damage, I...") });
        }

        private static void RegisterTruthDialogs()
        {
            Add(
                "truth_draedon",
                "瘟疫真相", "Plague Truth",
                CatTruth, 50,
                "阶段 8 后与嘉登同屏时自动触发", "Triggered when Draedon is present at stage 8+",
                new[]
                {
                    (1, "这股气息……这种令人作呕的感觉……"),
                    (1, "原来是你创造了瘟疫！"),
                    (1, "不可饶恕……"),
                    (1, "把我的力量发挥到极致吧，我要陪你把这些造物彻底碾碎！")
                },
                new[]
                {
                    (1, "This aura... this sickening feeling..."),
                    (1, "It was you who created the plague!"),
                    (1, "Unforgivable..."),
                    (1, "Unleash my power to the fullest, I will crush these creations with you!")
                });
        }

        private static void RegisterDailyDialogs()
        {
            Add(
                "greeting",
                "初次问候", "Greeting",
                CatDaily, 60,
                "获得孔雀翎后自动触发", "Triggered after obtaining Malachite",
                new[] { (0, "你来啦！我可是一直在等你哦。"), (0, "今天准备带我去哪儿转转？") },
                new[] { (0, "You're here! I've been waiting for you."), (0, "Where are we going for a stroll today?") });

            Add(
                "recovery_1",
                "恢复1", "Recovery 1",
                CatDaily, 61,
                "低血量后恢复满血时随机触发", "Triggered when recovering from low HP",
                new[] { (0, "呼……看到你的伤口愈合，我放心了。"), (4, "下次可不许再这么乱来了，我会心疼的。") },
                new[] { (0, "Phew... I'm relieved to see your wounds heal."), (4, "Don't be so reckless next time, it breaks my heart.") });

            Add(
                "recovery_2",
                "恢复2", "Recovery 2",
                CatDaily, 62,
                "低血量后恢复满血时随机触发", "Triggered when recovering from low HP",
                new[] { (0, "太好了，你终于恢复了。"), (0, "答应我，以后遇到危险多躲在我身后，好吗？") },
                new[] { (0, "Great, you've finally recovered."), (0, "Promise me, hide behind me more when facing danger, okay?") });

            Add(
                "idle_1",
                "发呆1", "Idle 1",
                CatDaily, 63,
                "长时间发呆随机触发", "Randomly triggered when idle",
                new[] { (2, "累了吗？那就停下来休息一下吧。"), (0, "我会在这里守着你的，安心闭上眼睛吧。") },
                new[] { (2, "Tired? Let's stop and rest for a bit."), (0, "I'll keep watch here, close your eyes peacefully.") });

            Add(
                "idle_2",
                "发呆2", "Idle 2",
                CatDaily, 64,
                "长时间发呆随机触发", "Randomly triggered when idle",
                new[] { (1, "一直盯着发呆……"), (1, "我的脸上有什么东西吗？") },
                new[] { (1, "Staring into space for so long..."), (1, "Is there something on my face?") });

            Add(
                "idle_3",
                "发呆3", "Idle 3",
                CatDaily, 65,
                "长时间发呆随机触发", "Randomly triggered when idle",
                new[] { (0, "发呆的时候也要注意周围哦。"), (0, "不过没关系，有我在呢。") },
                new[] { (0, "Pay attention to your surroundings even when spacing out."), (0, "But it's okay, I'm right here.") });

            Add(
                "dawn",
                "清晨", "Dawn",
                CatDaily, 66,
                "黎明时自动触发", "Triggered at dawn",
                new[] { (0, "朝阳出来了……"), (0, "早安，今天也要一起加油哦。") },
                new[] { (0, "The morning sun is out..."), (0, "Good morning, let's do our best together today.") });

            Add(
                "dusk",
                "黄昏", "Dusk",
                CatDaily, 67,
                "夜幕降临时自动触发", "Triggered at nightfall",
                new[] { (4, "夜幕降临了。"), (0, "黑暗中总是潜伏着危险，要小心。") },
                new[] { (4, "Night has fallen."), (0, "Danger always lurks in the dark, be careful.") });

            Add(
                "ambient_jungle",
                "丛林闲话", "Jungle Chat",
                CatDaily, 68,
                "在丛林日常随机触发", "Randomly triggered in jungle",
                new[]
                {
                    (2, "熟悉的丛林气息……"),
                    (4, "曾经我在这里失去了所有，但现在，我有了你。"),
                    (0, "别怕，我会保护你的。")
                },
                new[]
                {
                    (2, "The familiar scent of the jungle..."),
                    (4, "I once lost everything here, but now, I have you."),
                    (0, "Don't be afraid, I will protect you.")
                });

            Add(
                "ambient_beach",
                "海边闲话", "Beach Chat",
                CatDaily, 69,
                "在海滩日常随机触发", "Randomly triggered at beach",
                new[] { (0, "大海的潮水声真让人心平气和。"), (0, "能一起享受片刻的宁静，真好。") },
                new[] { (0, "The sound of the ocean tide is so calming."), (0, "It's nice to share this moment of peace with you.") });

            Add(
                "ambient_rain",
                "雨天闲话", "Rain Chat",
                CatDaily, 70,
                "下雨时日常随机触发", "Randomly triggered while raining",
                new[] { (2, "雨下个不停。"), (0, "可是在你身边，连绵的阴雨也不那么令人讨厌了。") },
                new[] { (2, "The rain just won't stop."), (0, "But beside you, the endless rain isn't so annoying.") });

            Add(
                "ambient_sky",
                "空岛闲话", "Sky Chat",
                CatDaily, 71,
                "在天空时日常随机触发", "Randomly triggered in sky",
                new[] { (0, "我们已经来到了世界的顶部。"), (0, "如果你想一直飞下去，我会为你托起轻风的。") },
                new[] { (0, "We have reached the top of the world."), (0, "If you want to keep flying, I will lift the breeze for you.") });
        }

        private static void RegisterDeathDialogs()
        {
            Add(
                "death_worm",
                "死亡·长虫", "Death Worm",
                CatDeath, 80,
                "被长直类 Boss 击败后自动触发", "Triggered after being killed by a worm-like boss",
                new[] { (3, "可恶的爬虫..."), (3, "一定是因为我们的平台修得还不够长！") },
                new[] { (3, "Damn reptiles..."), (3, "It must be because our platforms weren't built long enough!") });

            Add(
                "death_providence",
                "死亡·火球", "Death Providence",
                CatDeath, 81,
                "被普罗维登斯击败后自动触发", "Triggered after being killed by Providence",
                new[] { (4, "好刺眼的光..."), (1, "下次我们还是晚上再来找这颗大火球的麻烦吧。") },
                new[] { (4, "Such blinding light..."), (1, "Next time, let's mess with this giant fireball at night instead.") });

            Add(
                "death_exo",
                "死亡·机械", "Death Exo Mechs",
                CatDeath, 82,
                "被星流巨械击败后自动触发", "Triggered after being killed by Exo Mechs",
                new[] { (3, "这不公平！"), (1, "凭什么他只要在椅子上坐着，我们却要在枪林弹雨里到处乱窜？") },
                new[] { (3, "It's not fair!"), (1, "Why does he get to sit in a chair while we run for our lives in a hail of bullets?") });

            Add(
                "death_yharon",
                "死亡·大鸟", "Death Yharon",
                CatDeath, 83,
                "被犽戎击败后自动触发", "Triggered after being killed by Yharon",
                new[] { (4, "好烫好烫..."), (1, "这只大鸟的龙卷风也太密集了吧！") },
                new[] { (4, "So hot, so hot..."), (1, "This big bird's tornadoes are way too dense!") });

            Add(
                "death_default",
                "死亡·默认", "Death Default",
                CatDeath, 84,
                "死亡时随机触发", "Triggered randomly on death",
                new[] { (1, "啊...这下要掉不少钱了。"), (1, "你出门前把钱存进猪猪存钱罐了吗？") },
                new[] { (1, "Ah... we're going to drop a lot of coins."), (1, "Did you put your money in the Piggy Bank before we left?") });
        }

        private static void RegisterMilestoneDialogs()
        {
            // 直接使用 MalachiteData 中的里程碑数组
            // 数组索引 0 为空，阶段从 1 到数组长度-1
            for (int stage = 1; stage < MalachiteData.MilestoneDialoguesZh.Length; stage++)
            {
                var zh = MalachiteData.MilestoneDialoguesZh[stage];
                var en = stage < MalachiteData.MilestoneDialoguesEn.Length
                    ? MalachiteData.MilestoneDialoguesEn[stage]
                    : new List<(int, string)>(zh); // 保险：如果没有英文，用中文占位

                Add(
                    $"milestone_{stage}",
                    $"里程碑{stage}",
                    $"Milestone {stage}",
                    CatMilestone,
                    100 + stage,
                    $"将孔雀翎成长至阶段 {stage} 解锁",
                    $"Grow Malachite to stage {stage} to unlock",
                    zh,
                    en);
            }
        }

        public static DialogGroup GetGroup(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            AllGroups.TryGetValue(id, out var group);
            return group;
        }
    }
}