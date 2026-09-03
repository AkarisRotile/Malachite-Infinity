using System;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    // =====================================================================
    // 阶段 2 新天赋模型（v1）—— 纯加点化：五轨小节点 + 技能点经济。
    // v2 切换（M3）后：旧 20 节点星图整体退役，本文件为唯一效果出处。
    // 设计依据：写法\阶段2_天赋星图重设计.md（v1 决策 D6~D12，用户 2026-09-02 拍板）
    // =====================================================================

    /// <summary>
    /// 潜伏标记接口（拆除旧全局弹幕接管类后的落点）：
    /// 由本模组自己的弹幕类（MalachiteProj/Bolt/Domain）实现，
    /// StealthSystem 经此接口读写潜伏状态，不再需要全局接管类。
    /// </summary>
    public interface IStealthStrikeProjectile
    {
        /// <summary>该弹幕实例是否为潜伏攻击（金羽状态）。</summary>
        bool IsStealthStrike { get; set; }
    }

    /// <summary>五轨小节点。</summary>
    public enum TrackKind
    {
        Attack = 0,       // 攻击：最终伤害 +%
        ArmorPen = 1,     // 穿透：护甲穿透 +
        AttackSpeed = 2,  // 攻速：攻击速度 +%
        Crit = 3,         // 暴击：暴击率 +%（>100% 溢出按比例增幅终伤）
        Volley = 4        // 并发弹幕：每次使用 +1 道（单价更高）
    }

    /// <summary>求值结果：武器/弹幕只读此档案，禁止再出现散落效果判断。</summary>
    public struct TalentProfile
    {
        public float DamageMult;       // 总伤害倍率（≥1）
        public int ArmorPen;           // 护甲穿透（平值）
        public float AttackSpeedMult;  // 攻速倍率（≥1）
        public int CritChanceBonus;    // 暴击率加成（平值 %）
        public int VolleyBonus;        // 并发弹幕数（每次使用 +N 道）

        public bool IsEmpty => DamageMult <= 1f && ArmorPen == 0 && AttackSpeedMult <= 1f
            && CritChanceBonus == 0 && VolleyBonus == 0;

        /// <summary>总暴击溢出增幅：暴击 > 100% 时，每溢出 1% 提升 finalMult（常量见 TalentCatalog）。</summary>
        public static float OverflowDamageMultiplier(int totalCritPercent)
        {
            int overflow = totalCritPercent - 100;
            return overflow > 0 ? 1f + overflow * TalentCatalog.Effects.CritOverflowFinalMult : 1f;
        }
    }

    /// <summary>
    /// 五轨元数据与默认数值（v1 草案，数值对齐时可集中调整）。
    /// 注意：本文件是效果曲线的唯一出处（游戏规则书），武器层不得另造公式。
    /// </summary>
    public static class TalentCatalog
    {
        public const int TrackCount = 5;

        /// <summary>v2 切换开关（M3 已置 true）：旧 20 节点星图模型已整体退役，v2 迁移与加点 UI 生效。</summary>
        // 用 readonly 而非 const：若将来回退可避免 CS0162 死代码噪音。
        public static readonly bool V2Active = true;

        /// <summary>每级默认效果（由 Formula 计算）。</summary>
        public static class Effects
        {
            public const float DamagePerLevel = 0.04f;      // 攻击：+4%/级
            public const int ArmorPenPerLevel = 3;          // 穿透：+3/级
            public const float SpeedPerLevel = 0.025f;      // 攻速：+2.5%/级
            public const int CritPerLevel = 3;              // 暴击：+3%/级
            public const float CritOverflowFinalMult = 0.005f; // 暴击溢出：1% → 终伤 +0.5%
        }

        /// <summary>等级上限（随阶段成长的推荐表；cap = 3 + stage，最高 13 档时=16，用 Min 收敛防越界）。</summary>
        public static int LevelCap(TrackKind kind, int stage)
        {
            int cap = Math.Max(0, 3 + stage);
            if (kind == TrackKind.Volley)
                cap = Math.Min(cap, 5); // 并发轨上限更低（防弹幕数爆炸）
            return Math.Clamp(cap, 0, 30);
        }

        /// <summary>升到 nextLevel（即当前级为 nextLevel-1 → nextLevel）所需技能点。</summary>
        public static int CostToNext(TrackKind kind, int nextLevel)
        {
            // 常规轨：1 + 当前级；并发轨单价更高：2×(1 + 当前级)
            int prev = Math.Max(0, nextLevel - 1);
            int cost = 1 + prev;
            if (kind == TrackKind.Volley)
                cost *= 2;
            return cost;
        }

        /// <summary>单级效果是否允许购买（阶段门槛等后续规则在此收敛）。</summary>
        public static bool CanPurchase(TrackKind kind, int nextLevel, int stage)
            => nextLevel >= 1 && nextLevel <= LevelCap(kind, stage);

        /// <summary>轨道显示名（中/英）。</summary>
        public static string DisplayName(TrackKind kind, bool english = false)
            => kind switch
            {
                TrackKind.Attack => english ? "Attack" : "攻击",
                TrackKind.ArmorPen => english ? "Armor Penetration" : "穿透",
                TrackKind.AttackSpeed => english ? "Attack Speed" : "攻速",
                TrackKind.Crit => english ? "Critical Strike" : "暴击",
                TrackKind.Volley => english ? "Concurrent Projectiles" : "并发弹幕",
                _ => "?"
            };

        /// <summary>单级效果描述（供 UI 明细）。</summary>
        public static string EffectDescription(TrackKind kind, bool english = false)
            => kind switch
            {
                TrackKind.Attack => english ? $"+{Effects.DamagePerLevel * 100:0.#}% final damage / lv" : $"最终伤害 +{Effects.DamagePerLevel * 100:0.#}% / 级",
                TrackKind.ArmorPen => english ? $"+{Effects.ArmorPenPerLevel} armor penetration / lv" : $"护甲穿透 +{Effects.ArmorPenPerLevel} / 级",
                TrackKind.AttackSpeed => english ? $"+{Effects.SpeedPerLevel * 100:0.#}% attack speed / lv" : $"攻击速度 +{Effects.SpeedPerLevel * 100:0.#}% / 级",
                TrackKind.Crit => english ? $"+{Effects.CritPerLevel}% crit chance / lv; overflow >100% boosts final damage" : $"暴击率 +{Effects.CritPerLevel}% / 级；>100% 后溢出按比例增幅终伤",
                TrackKind.Volley => english ? $"+1 projectile per shot / lv (higher point cost)" : $"每次攻击 +1 道弹幕 / 级（消耗更高）",
                _ => ""
            };
    }

    /// <summary>把玩家当前加点汇总为 TalentProfile（武器 ModItem 与命中层调用）。</summary>
    public static class TalentEvaluator
    {
        public static TalentProfile Build(MalachitePlayer mp)
        {
            int stage = ProgressSystem.GetStage();
            TalentProfile p = new TalentProfile
            {
                DamageMult = 1f + TalentCatalog.Effects.DamagePerLevel * mp.TrackLevel[(int)TrackKind.Attack],
                ArmorPen = TalentCatalog.Effects.ArmorPenPerLevel * mp.TrackLevel[(int)TrackKind.ArmorPen],
                AttackSpeedMult = 1f + TalentCatalog.Effects.SpeedPerLevel * mp.TrackLevel[(int)TrackKind.AttackSpeed],
                CritChanceBonus = TalentCatalog.Effects.CritPerLevel * mp.TrackLevel[(int)TrackKind.Crit],
                VolleyBonus = mp.TrackLevel[(int)TrackKind.Volley],
            };
            _ = stage; // 阶段门槛规则在 CanPurchase / 数值表波次统一接入
            return p;
        }
    }

    /// <summary>
    /// 近战形态原型（参考《苍翼：混沌效应》ES 手感，MVP→v3 顿挫化）：
    /// F 键节奏连段（3 段循环，两段式变速挥动+停驻）→ 命中叠「精准」层数（每层 +10%，上限 +50%）→
    /// 双击方向键 = 突进斩（短暂无敌帧）。
    /// 手感调顺后再作为「技」页大节点接入；当前 MeleePrototypeOn=true 时手持即启用原型。
    /// 输入/手感参数集中于此，方便实机微调。
    /// </summary>
    public static class MalachiteMelee
    {
        /// <summary>原型总开关：true=手持柳刃时按 F 键（可改键）为近战连段（调试期），false=退回远程（接入技页后由节点状态接管）。</summary>
        public static readonly bool PrototypeOn = true;

        // ---- 连段 ----
        /// <summary>连段断链窗口（帧）：超过则重置回第 1 段。</summary>
        public const int ComboBreakWindow = 26;
        /// <summary>连段段数。</summary>
        public const int ComboMaxSteps = 3;

        // ---- 精准被动 ----
        public const int PrecisionMaxStacks = 5;
        public const float PrecisionDamagePerStack = 0.10f; // 每层 +10%
        public const int PrecisionDuration = 180;           // 层数持续时间（帧）

        // ---- 突进斩 ----
        /// <summary>双击判定的最大间隔（帧）。</summary>
        public const int DoubleTapWindow = 14;
        public const float DashSpeed = 17f;
        public const int DashImmuneTime = 26;
        public const int DashCooldown = 24;

        /// <summary>按住近战键时的出刀间隔（帧；= 蓄势+挥扫+定格，越大越"一顿一顿"）。</summary>
        public const int SwingInterval = 15;

        // ---- 挥动曲线 v4（纯特效弧光；顿挫 = "爆发过冲→回坐"曲线，参考 CWR 鬼切 OniSlashRenderer.BurstCurve 思路，自主实现）----
        /// <summary>拉背/蓄势帧数（刃未出、不判定伤害，制造"啪"前的蓄力感）。</summary>
        public const int SwingGatherFrames = 3;
        /// <summary>挥扫帧数（角度在此段内推进，爆发曲线驱动）。</summary>
        public const int SwingSweepFrames = 8;
        /// <summary>满形定格帧数（挥到终点后的短暂停顿）。</summary>
        public const int SwingHoldFrames = 4;
        /// <summary>爆发过冲倍率（>1：先冲过目标角再回坐，替代"减速拖尾"）。</summary>
        public const float SwingOvershoot = 1.05f;
        /// <summary>爆发曲线冲顶位置（行程占比，约 0.62 处过冲峰值）。</summary>
        public const float SwingBurstEnd = 0.62f;
        /// <summary>刃长随挥动的脉动幅度（半径 ×(1+该值)，过冲处最大）。</summary>
        public const float SwingReachPulse = 0.08f;
        // 弧度语义：0 = 朝前水平；负值 = 向上（屏幕 Y 向下，sin<0 即上）。
        private static readonly float[] StepStartRadL = { 0.45f, -0.55f, 0.25f };
        private static readonly float[] StepEndRadL = { -1.05f, 1.0f, -1.25f };
        /// <summary>每段挥动半径（px，即攻击范围，越后段越大；2026-09-03 实机要求≈2倍：96/108/128 → 200/225/256）。</summary>
        private static readonly float[] StepReachArr = { 200f, 225f, 256f };
        /// <summary>每段刃身大小倍率。</summary>
        private static readonly float[] StepScaleArr = { 1.0f, 1.12f, 1.30f };

        public static float StepStartRad(int step, int dir) { float r = StepStartRadL[Math.Clamp(step, 0, 2)]; return dir >= 0 ? r : MathHelper.Pi - r; }
        public static float StepEndRad(int step, int dir) { float r = StepEndRadL[Math.Clamp(step, 0, 2)]; return dir >= 0 ? r : MathHelper.Pi - r; }
        public static float StepReach(int step) => StepReachArr[Math.Clamp(step, 0, 2)];
        public static float StepScale(int step) => StepScaleArr[Math.Clamp(step, 0, 2)];
        /// <summary>每段刃身贴图 X 向拉伸倍率（沿刃长：段位越大越"甩长"）。</summary>
        private static readonly float[] StepStretchXArr = { 1.05f, 1.12f, 1.22f };
        /// <summary>每段刃身贴图 Y 向压扁倍率（厚度：段位越大越"薄利"）。</summary>
        private static readonly float[] StepSquashYArr = { 0.88f, 0.82f, 0.72f };
        public static float StepStretchX(int step) => StepStretchXArr[Math.Clamp(step, 0, 2)];
        public static float StepSquashY(int step) => StepSquashYArr[Math.Clamp(step, 0, 2)];

        // ---- 挥动特效贴图（用户自绘 MeleeSlash.png，已裁透明边；挥动圆心=贴图最左像素）----
        /// <summary>裁剪后贴图宽度（px）：画到挥动半径的比例基准（reach / SlashArtWidth）。</summary>
        public const float SlashArtWidth = 140f;
        /// <summary>裁剪后贴图锚点 Y（最左像素列的垂直质心；X=0 即最左像素 = 挥动圆心）。</summary>
        public const float SlashArtPivotY = 12.5f;
        /// <summary>刃体贴图本体绘制透明度（正常光照层）。</summary>
        public const float SlashArtAlpha = 1.0f;
        /// <summary>刃体贴图整体亮度倍率（调暗本体，靠周围光晕托出"刀光"而非实贴图；v5.6 = 0.62）。</summary>
        public const float SlashArtBrightness = 0.62f;
        /// <summary>刃体贴图加色辉光强度（叠加一层柔和发光）。</summary>
        public const float SlashArtGlowAlpha = 0.30f;

        // ---- 曳光/拖尾（适配贴图：沿刃尖轨迹的加色细带 + 贴图残影）----
        /// <summary>曳光整体透明度（0~1）。</summary>
        public const float SlashVisualAlpha = 0.55f;
        /// <summary>外缘曳光带宽度比例（相对挥动半径，细亮线）。</summary>
        public const float SlashEdgeWidth = 0.07f;
        /// <summary>内侧软融宽度比例（比外缘宽、更淡）。</summary>
        public const float SlashBandWidth = 0.20f;
        /// <summary>轨迹采样上限（帧，= 拖尾残影/曳光带长度）。</summary>
        public const int SlashTrailMax = 12;

        // ---- 挥动路径特效（v5.3：按爆发曲线真实路径重采样，双层扇形弧光带——彩色主带 + 嵌套白热芯带，参考特莉波卡镰刀历史采样条带与鬼切斩痕带）----
        /// <summary>路径扇内半径比例（相对刃尖外半径，弧光带内缘）。</summary>
        public const float PathInnerK = 0.45f;
        /// <summary>主带透明度（v5.6 提高，刀光感）。</summary>
        public const float PathGlowAlpha = 0.72f;
        /// <summary>白热芯带内半径比例（嵌套在彩色带内侧更窄更亮）。</summary>
        public const float PathWhiteInnerK = 0.70f;
        /// <summary>白热芯带透明度（v5.6 提高）。</summary>
        public const float PathWhiteAlpha = 0.60f;
        /// <summary>路径外缘锐亮线宽度比例（相对刃尖外半径）。</summary>
        public const float PathEdgeWidth = 0.05f;

        // ---- 击中反馈（v5.1：参考鬼切/镰刀命中：扩散环 + 砍痕闪刃 + 白热爆点）----
        /// <summary>命中反馈持续帧数。</summary>
        public const int HitFlashFrames = 7;
        /// <summary>命中扩散环最终半径（px）。</summary>
        public const float HitRingMaxR = 46f;
        /// <summary>命中砍痕闪刃长度（px，沿挥动切线）。</summary>
        public const float HitSlashLen = 60f;

        // ---- 命中判定盒（覆盖挥动半径的大盒，随挥动扫过）----
        /// <summary>判定盒中心所在的半径比例（相对 StepReach；0.5 = 覆盖内~外缘）。</summary>
        public const float SwingHitCenterK = 0.5f;
        /// <summary>判定盒宽度比例（相对 StepReach，约 1.02 = 几乎覆盖全刃长）。</summary>
        public const float SwingHitSpanK = 1.02f;
        /// <summary>判定盒高度（px，垂直厚度）。</summary>
        public const int SwingHitHeight = 150;

        /// <summary>精准层数的总伤害倍率（1 + 层数×单层）。</summary>
        public static float PrecisionDamageMult(int stacks) => 1f + Math.Min(PrecisionMaxStacks, Math.Max(0, stacks)) * PrecisionDamagePerStack;

        /// <summary>原型是否在玩家身上生效（手持孔雀柳刃）。</summary>
        public static bool IsPrototypeActive(Player player)
            => PrototypeOn && player != null && MalachiteCache.IsMalachiteItem(player.HeldItem);

        /// <summary>LMB 近战连段触发（每段发一次斩击；第三段带小突进）。</summary>
        public static void TryMeleeStrike(Player player, IEntitySource source, float damage, float knockback)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            int now = (int)Main.GameUpdateCount;
            if (now - mp.MeleeLastStrikeTick > ComboBreakWindow)
                mp.MeleeComboStep = 0;

            int step = mp.MeleeComboStep % ComboMaxSteps;
            mp.MeleeComboStep = (step + 1) % ComboMaxSteps;
            mp.MeleeLastStrikeTick = now;

            int dir = player.direction != 0 ? player.direction : 1;
            float mult = PrecisionDamageMult(mp.PrecisionStacks);
            int dmg = Math.Max(1, (int)(damage * mult));
            float kb = Math.Max(1f, knockback + step);

            if (step == 2)
                player.velocity.X = dir * 6f; // 第三段小突进（手感：连段有"推出去"感）

            SpawnSlash(player, source, dmg, kb, dir, step);
            // 出刀音阶随段位爬升：段1/2/3 音调递进，段3 换重音（手感递进）
            SoundEngine.PlaySound(step == 2 ? SoundID.Item71 with { Pitch = 0.05f } : SoundID.Item15 with { Pitch = step * 0.12f }, player.Center);
            if (step == 2 && player.whoAmI == Main.myPlayer)
                ScreenShakeSystem.Shake(3.5f); // 段3 收尾轻震
        }

        /// <summary>双击方向触发突进斩：位移 + 短暂无敌 + 大号斩击。</summary>
        public static void DoDash(Player player, int dir)
        {
            var mp = player.GetModPlayer<MalachitePlayer>();
            int now = (int)Main.GameUpdateCount;
            if (now - mp.LastDashTick < DashCooldown) return;

            mp.LastDashTick = now;
            player.velocity.X = dir * DashSpeed;
            player.immuneTime = Math.Max(player.immuneTime, DashImmuneTime);

            int weaponDmg = player.HeldItem != null ? player.GetWeaponDamage(player.HeldItem) : 32;
            int dmg = Math.Max(8, (int)(weaponDmg * (1f + 0.15f * mp.PrecisionStacks)));
            var src = player.GetSource_ItemUse(player.HeldItem);
            int step = Math.Clamp(mp.MeleeComboStep % ComboMaxSteps, 0, ComboMaxSteps - 1);
            SpawnSlash(player, src, dmg, 6f, dir, step);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = 0.1f }, player.Center);
            for (int i = 0; i < 6 && EffectLimiterSystem.CanSpawnEffect(1, 60); i++)
            {
                Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.TintableDust, new Vector2(dir * Main.rand.NextFloat(1f, 4f), Main.rand.NextFloat(-2f, 2f)),
                    0, MalachitePalette.AccentGold, Main.rand.NextFloat(0.8f, 1.4f));
                d.noGravity = true;
                d.fadeIn = 0.4f;
            }
        }

        /// <summary>生成斩击弹幕（绕玩家弧线挥动，见 MeleeSlashProj）。</summary>
        private static void SpawnSlash(Player player, IEntitySource source, int damage, float knockback, int dir, int step)
        {
            int idx = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                ModContent.ProjectileType<MeleeSlashProj>(), damage, knockback, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].ModProjectile is MeleeSlashProj sp)
            {
                sp.SlashDir = dir;
                sp.SlashStep = step;
            }
        }

        // ============ ES 招式表（v5.2 批1：蓄力重斩 / 上挑斩 / 满月终结；输入由 MalachitePlayer 解析）============

        /// <summary>近战招式种类（弹幕 Kind）。</summary>
        public enum MoveKind
        {
            Step = 0,      // 普通段击（F 连打）
            Charged = 1,   // 蓄力重斩（按住 ≥ ChargeFrames 松手）
            Upper = 2,     // 上挑斩（地面上 + F）
            Finisher = 3,  // 满月终结（精准满层 + 蓄力松手）
        }

        /// <summary>进入蓄力态所需按住帧数（16 ≈ 0.27s @60fps）。</summary>
        public const int ChargeFrames = 16;
        /// <summary>满月终结所需精准层数（满层 5）。</summary>
        public const int FinisherMinStacks = 5;
        /// <summary>蓄力/终结招后的间隔帧（硬直节奏）。</summary>
        public const int HeavyGapFrames = 24;

        // 各招弧线（基准朝右弧度 start→end；弹幕内按朝向镜像）
        private static readonly float[] ChargedArc = { 1.15f, -1.15f };   // 大横斩（过前胸的大弧）
        private static readonly float[] UpperArc = { 0.75f, -2.30f };     // 上挑（下前→上后的大仰弧）
        private static readonly float[] FinisherArc = { 1.70f, -1.70f };  // 满月（近 180°+ 巨弧）

        /// <summary>朝右基准角 → 实际朝向角（1 右原样 / -1 左水平镜像）。</summary>
        public static float FrontMirror(float rad, int dir) => dir >= 0 ? rad : MathHelper.Pi - rad;

        /// <summary>蓄力松手出招：精准满层=满月终结，否则=蓄力重斩。</summary>
        public static void FireHeavy(Player player, bool finisher, float damage)
        {
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            float mult = PrecisionDamageMult(mp.PrecisionStacks);
            float dmgMult = finisher ? 2.1f : 1.5f;
            int dmg = Math.Max(1, (int)(damage * mult * dmgMult));
            MoveKind kind = finisher ? MoveKind.Finisher : MoveKind.Charged;
            float start = finisher ? FinisherArc[0] : ChargedArc[0];
            float end = finisher ? FinisherArc[1] : ChargedArc[1];
            SpawnMove(player, player.GetSource_Misc("MalachiteMove"), dmg, finisher ? 13f : 9f, dir,
                start, end, finisher ? 1.45f : 1.30f, finisher ? 1.45f : 1.15f, kind, finisher ? 2 : 0);
            SoundEngine.PlaySound(finisher
                ? SoundID.Item71 with { Volume = 0.85f, Pitch = -0.25f }
                : SoundID.Item15 with { Volume = 0.8f, Pitch = -0.2f }, player.Center);
            if (player.whoAmI == Main.myPlayer)
                ScreenShakeSystem.Shake(finisher ? 9f : 5f); // 满月重震 / 重斩中震
        }

        /// <summary>上挑斩（地面上 + F）：仰弧上挥，命中把敌人挑飞；带小跳跃起步。</summary>
        public static void UppercutStrike(Player player, IEntitySource source, float damage)
        {
            int dir = player.direction != 0 ? player.direction : 1;
            var mp = player.GetModPlayer<MalachitePlayer>();
            float mult = PrecisionDamageMult(mp.PrecisionStacks);
            int dmg = Math.Max(1, (int)(damage * mult * 1.1f));
            // 小跳跃起步（纯手感：上挑带腾身）
            if (player.velocity.Y == 0f)
                player.velocity.Y = -6.5f;
            SpawnMove(player, source, dmg, 7f, dir,
                UpperArc[0], UpperArc[1], 1.05f, 1.10f, MoveKind.Upper, 0);
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.7f, Pitch = 0.35f }, player.Center);
            if (player.whoAmI == Main.myPlayer)
                ScreenShakeSystem.Shake(4f); // 上挑轻震
        }

        /// <summary>生成自定义招式斩击弹幕（绕玩家弧线挥动，见 MeleeSlashProj）。</summary>
        private static void SpawnMove(Player player, IEntitySource source, int damage, float knockback, int dir,
            float startRaw, float endRaw, float reachMult, float scaleMult, MoveKind kind, int extraHold)
        {
            int idx = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                ModContent.ProjectileType<MeleeSlashProj>(), damage, knockback, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].ModProjectile is MeleeSlashProj sp)
            {
                sp.SlashDir = dir;
                sp.Kind = kind;
                sp.CustomStart = startRaw;
                sp.CustomEnd = endRaw;
                sp.ReachMult = reachMult;
                sp.ScaleMult = scaleMult;
                sp.ExtraHold = extraHold;
            }
        }
    }
}
