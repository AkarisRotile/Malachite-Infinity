using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 本模组世界存档：记录玩家击败过的 Boss（任意模组/原版），
    /// 用于后续天赋技能点体系与进度重锚。灾厄缺失时也可正常工作。
    /// </summary>
    public class MalachiteProgress : ModSystem
    {
        public static MalachiteProgress Instance => ModContent.GetInstance<MalachiteProgress>();

        /// <summary>已击败 Boss 的键集合（键格式："Mod名:类型名" 或 "Vanilla:数字ID"；多部件 Boss 归组，见 GetBossKey）。</summary>
        public readonly HashSet<string> DefeatedBosses = new HashSet<string>();

        /// <summary>世界级一次性标记：花下败仗（flower_retry_loss）是否已触发过。</summary>
        public bool FlowerRetryLossShown = false;

        /// <summary>
        /// 已发放过点数的**里程碑/事件** id 集合（星图 3.0 新增，D28）。
        /// <para/>与 <see cref="DefeatedBosses"/> 同一权威口径：只有世界档说了算，
        /// 幂等记账，保证"首次通关事件额外发点"只发生一次。
        /// </summary>
        public readonly HashSet<string> GrantedMilestones = new HashSet<string>();

        public override void SaveWorldData(TagCompound tag)
        {
            tag["defeatedBosses"] = DefeatedBosses.ToList();
            tag["flowerRetryLossShown"] = FlowerRetryLossShown;
            tag["grantedMilestones"] = GrantedMilestones.ToList();
        }

        public override void LoadWorldData(TagCompound tag)
        {
            DefeatedBosses.Clear();
            if (tag.ContainsKey("defeatedBosses"))
            {
                foreach (var key in tag.GetList<string>("defeatedBosses"))
                    DefeatedBosses.Add(key);
            }
            FlowerRetryLossShown = tag.ContainsKey("flowerRetryLossShown") && tag.GetBool("flowerRetryLossShown");

            GrantedMilestones.Clear();
            if (tag.ContainsKey("grantedMilestones"))
            {
                foreach (var key in tag.GetList<string>("grantedMilestones"))
                    if (!string.IsNullOrEmpty(key)) GrantedMilestones.Add(key);
            }
        }

        /// <summary>生成 Boss 的唯一键。多部件原版 Boss 归组到代表部件（双子→Retinazer；月总头/手→核心），
        /// 保证一场战斗 = 一次首杀 = 一份点数；旧档已存的部件键仍能命中归组后的代表键，兼容不重复发点。</summary>
        public static string GetBossKey(NPC npc)
        {
            if (npc.ModNPC != null && npc.ModNPC.Mod != null)
                return $"{npc.ModNPC.Mod.Name}:{npc.ModNPC.Name}";
            int canonical = npc.type;
            switch (npc.type)
            {
                case NPCID.Spazmatism: canonical = NPCID.Retinazer; break;
                case NPCID.MoonLordHead:
                case NPCID.MoonLordHand: canonical = NPCID.MoonLordCore; break;
            }
            return $"Vanilla:{canonical}";
        }

        /// <summary>记录一次 Boss 击杀（幂等）。</summary>
        public void RegisterDefeat(NPC npc)
        {
            if (!npc.boss) return;
            string key = GetBossKey(npc);
            if (!string.IsNullOrEmpty(key)) DefeatedBosses.Add(key);
        }

        /// <summary>是否已击败指定模组 Boss。</summary>
        public bool Defeated(string modName, string npcName)
            => DefeatedBosses.Contains($"{modName}:{npcName}");

        /// <summary>已击败的不同 Boss 总数。</summary>
        public int DefeatedBossCount => DefeatedBosses.Count;

        // ===== 事件/里程碑补点巡检（星图 3.0，D28）=====
        private int _milestoneTimer = 60;

        /// <summary>
        /// 每秒巡检一次里程碑达成情况（幂等）。
        /// <para/>为什么放世界层而不是玩家层：里程碑是**世界**进度（NPC.downed* / Main.hardMode），
        /// 放玩家层会因多人各自执行而重复发点。
        /// </summary>
        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;   // 只有权威侧记账
            if (--_milestoneTimer > 0) return;
            _milestoneTimer = 60;
            TalentEconomy.CheckMilestones();
        }
    }
}
