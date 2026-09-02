using Terraria;
using Terraria.ID;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 进度判定系统：统一阶段(stage)判定与灾厄 Boss 击杀查询。
    /// 灾厄在场时读取其存档（与 v1.5.3 行为一致）；灾厄缺失时自动退回原版锚点。
    /// stage 8/10~13 的灾厄专属锚点在无灾厄环境下暂不可达，阶段 2 重构时正式重锚。
    /// </summary>
    public static class ProgressSystem
    {
        // ---- 灾厄 Boss 击杀（有灾厄读存档，无灾厄 false）----
        public static bool DownedExoMechs => CalamityCompat.GetDownedFlag("downedExoMechs");
        public static bool DownedCalamitas => CalamityCompat.GetDownedFlag("downedCalamitas");
        public static bool DownedYharon => CalamityCompat.GetDownedFlag("downedYharon");
        public static bool DownedDoG => CalamityCompat.GetDownedFlag("downedDoG");
        public static bool DownedProvidence => CalamityCompat.GetDownedFlag("downedProvidence");
        public static bool DownedPlaguebringer => CalamityCompat.GetDownedFlag("downedPlaguebringer");
        public static bool DownedCryogen => CalamityCompat.GetDownedFlag("downedCryogen");
        public static bool DownedLeviathan => CalamityCompat.GetDownedFlag("downedLeviathan");
        public static bool DownedAstrumDeus => CalamityCompat.GetDownedFlag("downedAstrumDeus");
        public static bool DownedPolterghast => CalamityCompat.GetDownedFlag("downedPolterghast");

        /// <summary>获取当前武器成长阶段（与原 v1.5.3 判定顺序一致）。</summary>
        public static int GetStage()
        {
            if (DownedExoMechs && DownedCalamitas) return 13;
            if (DownedYharon) return 12;
            if (DownedDoG) return 11;
            if (DownedProvidence) return 10;
            if (NPC.downedMoonlord) return 9;
            if (DownedPlaguebringer) return 8;
            if (NPC.downedGolemBoss) return 7;
            if (NPC.downedPlantBoss) return 6;
            if (NPC.downedMechBossAny) return 5;
            if (Main.hardMode) return 4;
            if (NPC.downedBoss3) return 3;
            if (NPC.downedBoss2) return 2;
            if (NPC.downedBoss1) return 1;
            return 0;
        }
    }
}
