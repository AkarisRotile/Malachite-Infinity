using Terraria;
using Terraria.ID;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 进度判定系统（阶段 2 独立化后）：纯原版阶梯。
    /// 本模组已与灾厄解绑：不再读取任何灾厄存档/标志；
    /// 旧代码中 DownedXxx 兼容属性恒返回 false（M2 波次统一清除）。
    /// </summary>
    public static class ProgressSystem
    {
        // ---- 灾厄摘除后的兼容假值（M2 清理；勿再新增引用）----
        public static bool DownedExoMechs => false;
        public static bool DownedCalamitas => false;
        public static bool DownedYharon => false;
        public static bool DownedDoG => false;
        public static bool DownedProvidence => false;
        public static bool DownedPlaguebringer => false;
        public static bool DownedCryogen => false;
        public static bool DownedLeviathan => false;
        public static bool DownedAstrumDeus => false;
        public static bool DownedPolterghast => false;

        /// <summary>
        /// 获取当前武器成长阶段 —— 纯原版锚点（无灾厄依赖）：
        /// 0 无 / 1 克眼 / 2 邪恶首领 / 3 骷髅王 / 4 肉山(困难模式) / 5 机械三王任一 /
        /// 6 世纪之花 / 7 石巨人 / 8 拜月教 / 9 月总 /
        /// 10~11 月总后事件链（火星/双月/旧日军团，自足锚点）。
        /// 注：12~13 档的"终局自足规则"在 M4 数值重锚波次定稿前暂不可达。
        /// </summary>
        public static int GetStage()
        {
            // 9 = 月总
            if (NPC.downedMoonlord) return 9;
            // 8 = 拜月教（月总前最后一道门槛）
            if (NPC.downedAncientCultist) return 8;
            if (NPC.downedGolemBoss) return 7;
            if (NPC.downedPlantBoss) return 6;
            if (NPC.downedMechBossAny) return 5;
            if (Main.hardMode) return 4;
            if (NPC.downedBoss3) return 3;
            if (NPC.downedBoss2) return 2;
            if (NPC.downedBoss1) return 1;
            return 0;
        }

        /// <summary>是否处于月总后的挑战档（实现口径 stage 9+，即月总已败；M4 若扩展 10+ 档再收紧）。</summary>
        public static bool InEndgame(int stage) => stage >= 9;
    }
}
