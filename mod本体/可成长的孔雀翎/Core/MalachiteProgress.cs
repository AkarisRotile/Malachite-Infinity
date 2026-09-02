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

        /// <summary>已击败 Boss 的键集合（键格式："Mod名:类型名" 或 "Vanilla:名称"）。</summary>
        public readonly HashSet<string> DefeatedBosses = new HashSet<string>();

        /// <summary>世界级一次性标记：花下败仗（flower_retry_loss）是否已触发过。</summary>
        public bool FlowerRetryLossShown = false;

        public override void SaveWorldData(TagCompound tag)
        {
            tag["defeatedBosses"] = DefeatedBosses.ToList();
            tag["flowerRetryLossShown"] = FlowerRetryLossShown;
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
        }

        /// <summary>生成 Boss 的唯一键。</summary>
        public static string GetBossKey(NPC npc)
        {
            if (npc.ModNPC != null && npc.ModNPC.Mod != null)
                return $"{npc.ModNPC.Mod.Name}:{npc.ModNPC.Name}";
            return $"Vanilla:{npc.type}";
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
    }
}
