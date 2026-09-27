using System;
using Terraria;
using Terraria.ID;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 技能点经济（星图 3.0 版，决策 D28）：
    /// ① Boss 首杀（原版 1/2/3/4，模组 Boss 兜底 2）；
    /// ② **事件首通 + 里程碑补点**（2026-09-27 新增）——解决旧版"中期点数荒、点数花不出去"。
    /// <para/>权威数据：<see cref="MalachiteProgress.DefeatedBosses"/>（Boss 首杀）
    /// 与 <see cref="MalachiteProgress.GrantedMilestones"/>（事件/里程碑幂等记账），两者都在世界档。
    /// 注：多人权威同步（把服务端点数推给各客户端）属后续波次 TODO，当前单机/主机本地即时生效。
    /// </summary>
    public static class TalentEconomy
    {
        // ===== Boss 首杀数值（保守版，集中可调）=====
        public const int TierEarlyBoss = 1;   // 史莱姆王/克眼/邪首/骷髅王/蜂后/鹿/事件王
        public const int TierHardWall = 2;    // 肉山
        public const int TierHardBoss = 3;    // 机械三王/世纪之花/石巨人/拜月教
        public const int TierMoonLord = 4;    // 月总
        public const int TierMiniOrEvent = 1; // 其它原版事件/小 Boss
        public const int TierModBoss = 2;     // 其它模组 Boss（无法归类时给中值）

        /// <summary>该 Boss 首杀值多少技能点（纯原版 + 模组兜底）。</summary>
        public static int PointsForBoss(NPC npc)
        {
            if (npc == null || !npc.boss) return 0;

            if (npc.ModNPC != null)
                return TierModBoss;

            int t = npc.type;
            // 月总
            if (t == NPCID.MoonLordCore || t == NPCID.MoonLordHead || t == NPCID.MoonLordHand)
                return TierMoonLord;

            // 中后期主线（肉山后三王线 → 拜月）
            if (t == NPCID.TheDestroyer || t == NPCID.SkeletronPrime || t == NPCID.Retinazer || t == NPCID.Spazmatism
                || t == NPCID.Plantera || t == NPCID.Golem || t == NPCID.CultistBoss)
                return TierHardBoss;

            // 肉山
            if (t == NPCID.WallofFlesh)
                return TierHardWall;

            // 前期主线
            if (t == NPCID.KingSlime || t == NPCID.EyeofCthulhu
                || t == NPCID.EaterofWorldsHead || t == NPCID.BrainofCthulhu
                || t == NPCID.SkeletronHead || t == NPCID.QueenBee || t == NPCID.Deerclops)
                return TierEarlyBoss;

            // 其余（霜月/南瓜月/火星/旧日等事件 Boss、地牢守卫等）按小档
            return TierMiniOrEvent;
        }

        // ====================================================================
        // 事件 / 里程碑补点（D28）
        // ====================================================================
        //
        // 为什么需要：旧版只有 Boss 首杀给点，总数约 34 点，而后期的「技」节点动辄要攒点，
        // 中期会出现"点数早就花完、之后一路没有成长感"的空窗。事件与里程碑补 13 点，
        // 总供给约 47 点 —— 仍**远低于**星网全解锁的 65 点，因此"必须取舍"的设计目标不变。

        /// <summary>单个里程碑：幂等 id、点数、达成判定、显示名。</summary>
        public readonly struct Milestone
        {
            public readonly string Id;
            public readonly int Points;
            public readonly Func<bool> Met;
            public readonly string Zh;
            public readonly string En;

            public Milestone(string id, int points, Func<bool> met, string zh, string en)
            {
                Id = id; Points = points; Met = met; Zh = zh; En = en;
            }
        }

        /// <summary>里程碑表（唯一出处）。判定回调必须**无副作用且廉价**——它每 60 帧被调一次。</summary>
        public static readonly Milestone[] Milestones =
        {
            new Milestone("hardmode",   2, () => Main.hardMode,                      "踏入困难模式", "Entered Hardmode"),
            new Milestone("goblins",    2, () => NPC.downedGoblins,                  "击退哥布林入侵", "Repelled the Goblin Army"),
            new Milestone("pirates",    2, () => NPC.downedPirates,                  "击退海盗入侵", "Repelled the Pirate Invasion"),
            new Milestone("frostlegion",1, () => NPC.downedFrost,                    "击退霜之军团", "Repelled the Frost Legion"),
            new Milestone("pumpkin",    2, () => NPC.downedHalloweenKing,            "通关南瓜月", "Cleared the Pumpkin Moon"),
            new Milestone("frostmoon",  2, () => NPC.downedChristmasIceQueen,        "通关霜月", "Cleared the Frost Moon"),
            new Milestone("martian",    2, () => NPC.downedMartians,                "击退火星暴乱", "Repelled the Martian Madness"),
        };

        /// <summary>
        /// 扫描并发放所有**已达成但尚未记账**的里程碑（幂等）。
        /// <para/>只在权威侧执行（单机 / 服务器 / 主机），客户端不写世界档。
        /// </summary>
        /// <returns>本次发放的总点数。</returns>
        public static int CheckMilestones()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return 0;

            MalachiteProgress prog = MalachiteProgress.Instance;
            if (prog == null) return 0;

            int total = 0;
            string unlockedZh = null, unlockedEn = null;

            foreach (Milestone m in Milestones)
            {
                if (prog.GrantedMilestones.Contains(m.Id)) continue;
                if (!m.Met()) continue;

                prog.GrantedMilestones.Add(m.Id);
                total += m.Points;
                unlockedZh = unlockedZh == null ? m.Zh : unlockedZh + "、" + m.Zh;
                unlockedEn = unlockedEn == null ? m.En : unlockedEn + ", " + m.En;
            }

            if (total <= 0) return 0;

            GrantToAllPlayers(total);
            if (!Main.dedServ)
            {
                Main.NewText(MalachiteData.Loc(
                    $"【星网里程碑】{unlockedZh} —— 技能点 +{total}。",
                    $"[Star Web Milestone] {unlockedEn} — skill points +{total}."),
                    MalachitePalette.AccentGold);
            }
            return total;
        }

        /// <summary>世界内所有在线玩家各 +points（含观战；击杀者同样有份）。</summary>
        public static void GrantToAllPlayers(int points)
        {
            if (points <= 0) return;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p != null && p.active)
                    p.GetModPlayer<MalachitePlayer>().SkillPoints += points;
            }
        }
    }
}
