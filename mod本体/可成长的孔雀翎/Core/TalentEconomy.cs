using System;
using Terraria;
using Terraria.ID;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 技能点经济（v2 拍板：仅"不同 Boss 首杀"发点、全图在线玩家都有份、保守数值 1~4 为主）。
    /// 权威数据：MalachiteProgress.DefeatedBosses（世界档）承担"是否首杀"；
    /// 本类只负责"该 Boss 值几点"与"发放给谁"。
    /// 注：多人权威同步（把服务端点数推给各客户端）属后续波次 TODO，当前单机/主机本地即时生效。
    /// </summary>
    public static class TalentEconomy
    {
        // ===== 数值（保守版，集中可调）=====
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
