// 代码来源与合规署名：
// - 手感设计参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES。
// - 挥动/刀光管线（学习后自主实现，未复制源码）：
//   CalamityOverhaul（MIT, (c) hocha113）OniSlash/OniSlashRenderer 与 CalamityEntropy（社区开源）
//   TlipocasScytheHeld 的通用管线：手持弹幕 + 缓动状态机 + 顶点扇形条带(TriangleStrip) +
//   Additive 发光混合 + 纹理映射 + GameViewMatrix.TransformationMatrix 视口变换 + 严格状态还原。
//   本实现为自主编写；着色用 FNA 内置 BasicEffect（TextureEnabled + 顶点色）替代自定义 HLSL——
//   因 tModLoader 不会把 .fx 编译进包（需预编译 .xnb 工具链，另行自建）。
// - 挥动贴图：用户自绘 MeleeSlash.png（圆心=最左像素）。
// 本文件为自主实现。
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 近战形态·斩击（v6.4：ES 全招式状态机——输入判定树 / 爆发帧物理 / 取消管线 / 纹章矩阵契约）。
    /// - 运动：爆发曲线缓动推进 θ（缓动状态机）；
    /// - 刀光：每帧把挥扫历史角度采样成 内缘/外缘 双轨顶点，构建 TriangleStrip 扇形网格；
    /// - 渲染：End → Immediate + Additive → DrawUserPrimitives → 严格还原(Deferred/AlphaBlend/CullCounterClockwise)；
    /// - 矩阵：顶点先乘 Main.GameViewMatrix.ZoomMatrix（任意缩放不错位），再正交投影；
    /// - 颜色：顶点色渐变（尾→头 tint→白热；内缘暗→外缘白热芯），零每帧分配（静态数组/BasicEffect）。
    /// </summary>
    public class MeleeSlashProj : ModProjectile
    {
        /// <summary>用户自绘挥动贴图（已裁透明边）。</summary>
        public override string Texture => "可成长的孔雀翎/Textures/MeleeSlash";

        public int SlashStep = 0;
        public int SlashDir = 1;
        public MalachiteMelee.MoveKind Kind = MalachiteMelee.MoveKind.Step1;
        public float CustomStart = 0f;
        public float CustomEnd = 0f;
        public float ReachMult = 1f;
        public float ScaleMult = 1f;
        public int ExtraHold = 0;
        // 每招自定义节奏（0=用全局默认；j.A 快刀用 2/6/2 → 总周期 10 帧）
        public int GatherOverride = 0;
        public int SweepOverride = 0;
        public int HoldOverride = 0;

        // ---- 常驻资源（避免每帧分配，指南规则2）----
        private static BasicEffect _fanFx;
        private static readonly SlashVertex[] _fanSrc = new SlashVertex[SlashTuning.FanVertexCapacity];       // 中立顶点（SlashVfx 产出）
        private static readonly VertexPositionColorTexture[] _fanVerts = new VertexPositionColorTexture[SlashTuning.FanVertexCapacity]; // 上传用（同布局，逐点转换）
        private static readonly VertexPositionColor[] _bladeVerts = new VertexPositionColor[18]; // 修长晶刃 6 三角片

        private int _age = 0;
        private int _flash = 0;
        private int _hitFx = 0;
        private Vector2 _hitPos = Vector2.Zero;
        private Vector2 _hitDir = Vector2.UnitX;
        private bool _upperHit = false;
        private float _shakeT = 0f;
        private bool _crestSpawned = false; // 每记挥击只按矩阵生成一次纹章（固定位置）
        private bool _hitCrestSpawned = false; // 每记挥击命中时只在命中点补一枚纹章（整记攻击最多两枚）
        private int _hitstop = 0;  // 卡肉顿帧：命中后冻结 2 帧
        private int _impactT = 0;   // 下砸触地冲击波余帧
        private bool _impactDone = false;
        private bool _finisher2Chained = false; // Finisher1 已派生二段（防重复）
        private Vector2 _airAnchor = Vector2.Zero; // 空战 j.B 出刀瞬间绝对世界坐标（半空留置雷锚点，不随玩家下落）

        private readonly List<float> _th = new List<float>();
        private readonly List<float> _sc = new List<float>();
        private readonly List<float> _sx = new List<float>();
        private readonly List<float> _sy = new List<float>();
        private readonly List<Vector2> _tip = new List<Vector2>();

        private int GatherFrames => GatherOverride > 0 ? GatherOverride : MalachiteMelee.SwingGatherFrames;
        private int SweepFrames => SweepOverride > 0 ? SweepOverride : MalachiteMelee.SwingSweepFrames;
        private int HoldFrames => HoldOverride > 0 ? HoldOverride : MalachiteMelee.SwingHoldFrames;
        private int SweepEnd => GatherFrames + SweepFrames;
        private int TotalFrames => SweepEnd + HoldFrames + ExtraHold;

        public override void SetDefaults()
        {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.DamageType = MindDamageClass.Instance;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2;
        }

        private static float SmoothStep01(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static float BurstCurve(float p)
        {
            p = Math.Clamp(p, 0f, 1f);
            float e = MalachiteMelee.SwingBurstEnd;
            if (p < e) return MalachiteMelee.SwingOvershoot * (float)Math.Pow(p / e, MalachiteMelee.SwingAccelPower); // 加速度层（2026-09-05）：先慢后快，K 调参入口见 MalachiteMelee
            return MathHelper.Lerp(MalachiteMelee.SwingOvershoot, 1f, SmoothStep01((p - e) / (1f - e)));
        }

        private void SwingArc(int step, out float start, out float end)
        {
            if (MalachiteMelee.IsStepKind(Kind))
            {
                start = MalachiteMelee.StepStartRad(step, SlashDir);
                end = MalachiteMelee.StepEndRad(step, SlashDir);
            }
            else
            {
                start = MalachiteMelee.FrontMirror(CustomStart, SlashDir);
                end = MalachiteMelee.FrontMirror(CustomEnd, SlashDir);
            }
        }

        public override void AI()
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) { Projectile.Kill(); return; }
            // 下砸/突进/大招两段走独立生命线（AirDive 触地引爆 / DashCut 16 帧穿透 / Finisher 两段自管终局）
            if (_age >= TotalFrames && Kind != MalachiteMelee.MoveKind.AirDive && Kind != MalachiteMelee.MoveKind.DashCut
                && Kind != MalachiteMelee.MoveKind.Finisher1 && Kind != MalachiteMelee.MoveKind.Finisher2
                && Kind != MalachiteMelee.MoveKind.BackflipRetreat && Kind != MalachiteMelee.MoveKind.AirNeedle
                && Kind != MalachiteMelee.MoveKind.LandingSweep && Kind != MalachiteMelee.MoveKind.WarpStep
                && Kind != MalachiteMelee.MoveKind.AerialVortex) { Projectile.Kill(); return; }

            // —— 取消管线（§三.2）：跳跃取消 JC / 普攻目押取消 Gatling ——
            if (TryCancel(owner)) return;

            // 空战 j.A/j.B 空中悬停滞空（§模块二）：挥动前 8 帧抵消下坠动量 + 锁死重力 + 刷新掉落伤害基准
            if ((Kind == MalachiteMelee.MoveKind.AirStep1 || Kind == MalachiteMelee.MoveKind.AirStep2) && _age < MalachiteMelee.AirStallFrames)
            {
                if (owner.velocity.Y > 0f) owner.velocity.Y *= MalachiteMelee.AirStallDamp;
                owner.fallStart = (int)(owner.position.Y / 16f);
                owner.GetModPlayer<MalachitePlayer>().AirStallFrames = MalachiteMelee.AirStallFrames;
            }

            // 卡肉顿帧（§三.1）：命中后冻结自身计时 2 帧，刀刃在肉体中悬停
            if (_hitstop > 0) { _hitstop--; return; }

            // 特殊招式走独立状态机（下砸 / 突进 / 大招二段俯冲）
            if (Kind == MalachiteMelee.MoveKind.AirDive) { AirDiveAI(owner); return; }
            if (Kind == MalachiteMelee.MoveKind.DashCut) { DashCutAI(owner); return; }
            if (Kind == MalachiteMelee.MoveKind.Finisher2) { Finisher2AI(owner); return; }
            if (Kind == MalachiteMelee.MoveKind.BackflipRetreat) { BackflipRetreatAI(owner); return; }
            if (Kind == MalachiteMelee.MoveKind.AirNeedle) { AirNeedleAI(owner); return; }
            if (Kind == MalachiteMelee.MoveKind.LandingSweep) { LandingSweepAI(owner); return; }
            if (Kind == MalachiteMelee.MoveKind.WarpStep) { WarpStepAI(owner); return; }
            if (Kind == MalachiteMelee.MoveKind.AerialVortex) { AerialVortexAI(owner); return; }

            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            SwingArc(step, out float start, out float end);
            float reachBase = MalachiteMelee.StepReach(step) * ReachMult;

            if (_age < GatherFrames)
            {
                Projectile.friendly = false;
                Projectile.rotation = start;
                PlaceAt(owner, start, reachBase * 0.5f);
            }
            else if (_age < SweepEnd)
            {
                Projectile.friendly = true;
                int since = _age - GatherFrames;
                float p = since / (float)Math.Max(1, SweepFrames);
                float c = BurstCurve(p);
                float theta = MathHelper.Lerp(start, end, c);
                float pulse = 1f + MalachiteMelee.SwingReachPulse * c;
                Projectile.rotation = theta;
                PlaceAt(owner, theta, reachBase * pulse);

                // 爆发帧（since == 0）：动作物理动量干涉（§三.1 单一出处）
                if (since == 0)
                {
                    ApplyMovePhysics(owner);
                    if (!MalachiteMelee.IsChainKind(Kind) && Projectile.owner == Main.myPlayer)
                        _shakeT = Kind == MalachiteMelee.MoveKind.Finisher2 ? 7f : Kind == MalachiteMelee.MoveKind.Finisher1 ? 6f : 5f;
                }
                if (p >= MalachiteMelee.SwingBurstEnd && _flash == 0) _flash = 3;

                // —— 特效层（2026-09-05）：挥砍火花弧——爆发段沿刃身撒出段色火花，走全局粒子预算 ——
                bool fastPhase = p > 0.30f && p < 0.90f; // 加速度层：极速段火花加密（每 2 帧），起手/收尾减速段每 4 帧
                if (since % (fastPhase ? 2 : 4) == 0 && EffectLimiterSystem.CanSpawnEffect(1, 90))
                {
                    float sparkAng = MathHelper.Lerp(start, end, c * 0.85f);
                    Vector2 sparkPos = owner.Center + new Vector2((float)Math.Cos(sparkAng), (float)Math.Sin(sparkAng)) * (reachBase * pulse * 0.6f);
                    Color sparkTint = step >= 2 || Kind == MalachiteMelee.MoveKind.Finisher1 ? MalachitePalette.AccentGold
                        : step == 1 ? Color.Lerp(MalachitePalette.GreenBright, MalachitePalette.AccentCyan, 0.5f)
                        : MalachitePalette.GreenBright;
                    int dirSign = SlashDir != 0 ? SlashDir : 1;
                    EffectLimiterSystem.SpawnSpark(sparkPos + Main.rand.NextVector2Circular(6f, 6f),
                        new Vector2((float)Math.Cos(sparkAng + MathHelper.PiOver2), (float)Math.Sin(sparkAng + MathHelper.PiOver2)) * Main.rand.NextFloat(1f, 3f) * dirSign,
                        sparkTint, Main.rand.NextFloat(0.7f, 1.2f), 14);
                }

                // 急速挥击帧（since == 2）：按纹章参数契约矩阵生成空间纹章（Crest Arts，§四）
                if (since == 2 && !_crestSpawned)
                {
                    _crestSpawned = true;
                    SpawnMoveCrests(owner);
                }

                float scaleFactor = MalachiteMelee.IsStepKind(Kind) ? MalachiteMelee.StepScale(step) : ScaleMult;
                float artScale = reachBase * pulse * scaleFactor / MalachiteMelee.SlashArtWidth;
                float sx = MalachiteMelee.IsStepKind(Kind) ? MalachiteMelee.StepStretchX(step) : 1.05f;
                float sy = MalachiteMelee.IsStepKind(Kind) ? MalachiteMelee.StepSquashY(step) : 0.90f;
                sx *= 1f + 0.10f * c;
                sy *= 1f - 0.06f * c;
                _th.Add(theta);
                _sc.Add(artScale);
                _sx.Add(sx);
                _sy.Add(sy);
                _tip.Add(owner.Center + new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * (artScale * MalachiteMelee.SlashArtWidth * sx));
                TrimHistory();
            }
            else
            {
                Projectile.friendly = true;
                Projectile.rotation = end;
                PlaceAt(owner, end, reachBase);
            }

            // —— 大招一段（Finisher1）：顶点顿挫定格 + 初阶小单翼 + 派生二段 ——
            if (Kind == MalachiteMelee.MoveKind.Finisher1 && _age >= SweepEnd && !_finisher2Chained)
            {
                if (_age == SweepEnd)
                {
                    // 顶点定格：清速冻结滞空 + 高频微震 + 晶核咬合音（三重物理停滞法则）
                    owner.velocity *= 0.05f;
                    owner.GetModPlayer<MalachitePlayer>().AirStallFrames = MalachiteMelee.ApexFreezeFrames;
                    EsCrestWingsProj.Spawn(owner, 0); // 初阶小单翼
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.55f, Volume = 0.7f }, owner.Center);
                    if (Projectile.owner == Main.myPlayer)
                        ScreenShakeSystem.Shake(6f);
                }
                var mpF = owner.GetModPlayer<MalachitePlayer>();
                // 后摇期间按普攻 → 提前派生；后摇结束 → 自动无缝切入二段
                if (mpF.MeleeJustPressed || _age >= TotalFrames)
                {
                    _finisher2Chained = true;
                    mpF.ClearMeleeBuffer();
                    mpF.GatlingConsumedTick = (int)Main.GameUpdateCount;
                    MalachiteMelee.SpawnFinisher2(owner);
                    Projectile.Kill();
                    return;
                }
            }

            if (_flash > 0) _flash--;
            if (_hitFx > 0) _hitFx--;
            if (_shakeT > 0f) _shakeT = Math.Max(0f, _shakeT - 1f);
            _age++;
        }

        /// <summary>取消管线（§三.2）：返回 true 表示本弹幕已被取消（已 Kill）。</summary>
        private bool TryCancel(Player owner)
        {
            var mp = owner.GetModPlayer<MalachitePlayer>();

            // 跳跃取消 JC：Step2 挑空命中后的卡肉窗口内按下跳跃键 → 打断后摇、与被挑飞的敌怪同步升空
            if (Kind == MalachiteMelee.MoveKind.Step2 && _hitFx > 0 && mp.JumpJustPressed)
            {
                owner.velocity.Y = MalachiteMelee.JCVelocityY;
                owner.fallStart = (int)(owner.position.Y / 16f);
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = 0.4f }, owner.Center);
                if (EffectLimiterSystem.CanSpawnEffect(2, 60))
                {
                    for (int i = 0; i < 4; i++)
                        EffectLimiterSystem.SpawnSpark(owner.Center + Main.rand.NextVector2Circular(10f, 14f),
                            new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), 3f), MalachitePalette.GreenBright, 1.1f, 12);
                }
                Projectile.Kill();
                return true;
            }

            // 普攻目押取消 Gatling：刀光挥完后的取消窗（SweepEnd+1 ~ TotalFrames）内再次按下近战键 → 抹掉后摇无缝接下一段（地面链/空战链通用）
            if (MalachiteMelee.IsChainKind(Kind) && _age >= SweepEnd + 1 && _age < TotalFrames && mp.MeleeJustPressed)
            {
                MalachiteMelee.GatlingContinue(owner, Kind);
                Projectile.Kill();
                return true;
            }
            return false;
        }

        /// <summary>爆发帧(since==0)物理动量干涉（§三.1；参数契约唯一出处 MalachiteMelee）。</summary>
        private void ApplyMovePhysics(Player owner)
        {
            int dir = owner.direction != 0 ? owner.direction : 1;
            switch (Kind)
            {
                case MalachiteMelee.MoveKind.Step1:
                case MalachiteMelee.MoveKind.Step2:
                case MalachiteMelee.MoveKind.Step3:
                    int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
                    owner.velocity.X += dir * MalachiteMelee.StepPushX[step];
                    break;
                case MalachiteMelee.MoveKind.UpperRise: // 腾跃升龙：垂直升力 + 前冲分量
                    owner.velocity.Y = MalachiteMelee.UpperRiseJump;
                    owner.velocity.X += dir * MalachiteMelee.UpperRisePush;
                    break;
                case MalachiteMelee.MoveKind.AirStep1:   // j.A/j.B：记录出刀瞬间绝对坐标（半空留置雷锚点）+ 滞空锁重力
                case MalachiteMelee.MoveKind.AirStep2:
                    _airAnchor = owner.Center;
                    owner.GetModPlayer<MalachitePlayer>().AirStallFrames = MalachiteMelee.AirStallFrames;
                    break;
                case MalachiteMelee.MoveKind.AirStep3:   // j.C：下劈下压惯性
                    owner.velocity.Y += MalachiteMelee.AirStep3DownPush;
                    break;
                case MalachiteMelee.MoveKind.Finisher1:  // 一段飞升：斜前上方高速跃起 + 突进全程无敌
                    owner.velocity.X = dir * MalachiteMelee.Finisher1SpeedX;
                    owner.velocity.Y = MalachiteMelee.Finisher1SpeedY;
                    owner.immune = true;
                    owner.immuneTime = Math.Max(owner.immuneTime, MalachiteMelee.Finisher1ImmuneFrames);
                    owner.fallStart = (int)(owner.position.Y / 16f);
                    break;
            }
        }

        /// <summary>按招式×纹章参数契约矩阵生成空间纹章（§四；since==2 调用）。</summary>
        private void SpawnMoveCrests(Player owner)
        {
            int dir = owner.direction != 0 ? owner.direction : 1;
            switch (Kind)
            {
                case MalachiteMelee.MoveKind.Step1: // 剑尖前方 +45px，近身微型纹章
                {
                    var s = MalachiteMelee.CrestSpecOf(Kind);
                    EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner,
                        owner.Center + new Vector2(dir * 45f, -10f), Projectile.damage, s.Knockback, Kind, dir, s);
                    break;
                }
                case MalachiteMelee.MoveKind.Step2: // 斜上方 45° +75px，纵向拉伸
                {
                    var s = MalachiteMelee.CrestSpecOf(Kind);
                    const float c45 = 0.7071f;
                    EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner,
                        owner.Center + new Vector2(dir * 75f * c45, -75f * c45), Projectile.damage, s.Knockback, Kind, dir, s);
                    break;
                }
                case MalachiteMelee.MoveKind.Step3: // 正前方远端 +115px，巨型横向纹章
                {
                    var s = MalachiteMelee.CrestSpecOf(Kind);
                    EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner,
                        owner.Center + new Vector2(dir * 115f, -4f), Projectile.damage, s.Knockback, Kind, dir, s);
                    break;
                }
                case MalachiteMelee.MoveKind.UpperRise: // 沿挑起轨迹双连珠（高度相差 40px；8/14 帧依次引爆，二次颠勺）
                {
                    var s = MalachiteMelee.CrestSpecOf(Kind);
                    EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner,
                        owner.Center + new Vector2(dir * 36f, -70f), Projectile.damage, s.Knockback, Kind, dir, s, delayOverride: 8);
                    EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner,
                        owner.Center + new Vector2(dir * 36f, -110f), Projectile.damage, s.Knockback, Kind, dir, s, delayOverride: 14);
                    break;
                }
                case MalachiteMelee.MoveKind.AirStep1: // j.A：斜上 45° 微型纹章（快刀伴生）
                {
                    var s = MalachiteMelee.CrestSpecOf(Kind);
                    EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner,
                        owner.Center + new Vector2(dir * 40f, -40f), Projectile.damage, s.Knockback, Kind, dir, s);
                    break;
                }
                case MalachiteMelee.MoveKind.AirStep2: // j.B：半空留置雷，固定在出刀瞬间绝对坐标，持续切割，不被重力影响
                {
                    var s = MalachiteMelee.CrestSpecOf(Kind);
                    EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner,
                        _airAnchor + new Vector2(dir * 24f, -14f), Projectile.damage, s.Knockback, Kind, dir, s);
                    break;
                }
                case MalachiteMelee.MoveKind.AirStep3: // j.C：击坠由斩击 OnHitNPC 覆写目标速度，不生成纹章
                    break;
                case MalachiteMelee.MoveKind.Charged:    // v5.2 蓄力重斩纹章
                {
                    var s = MalachiteMelee.CrestSpecOf(Kind);
                    EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner,
                        owner.Center + new Vector2(dir * 60f, -10f), Projectile.damage, s.Knockback, Kind, dir, s);
                    break;
                }
                // Finisher1：顶点展开单翼（无纹章）；Finisher2：终点在 Finisher2Impact 内直接空间引爆
                case MalachiteMelee.MoveKind.Finisher1:
                case MalachiteMelee.MoveKind.Finisher2:
                    break;
            }
        }

        private void PlaceAt(Player owner, float theta, float visualReach)
        {
            int w = (int)(visualReach * MalachiteMelee.SwingHitSpanK);
            Projectile.width = Math.Max(8, w);
            Projectile.height = MalachiteMelee.SwingHitHeight;
            float centerR = visualReach * MalachiteMelee.SwingHitCenterK;
            Projectile.Center = owner.Center + new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * centerR;
        }

        private void TrimHistory()
        {
            if (_th.Count > MalachiteMelee.SlashTrailMax)
            {
                _th.RemoveAt(0); _sc.RemoveAt(0); _sx.RemoveAt(0); _sy.RemoveAt(0); _tip.RemoveAt(0);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active) return;
            var mp = owner.GetModPlayer<MalachitePlayer>();
            mp.PrecisionStacks = Math.Min(MalachiteMelee.PrecisionMaxStacks, mp.PrecisionStacks + 1);
            mp.PrecisionTimer = MalachiteMelee.PrecisionDuration;
            mp.ReportSpecialHit(Kind); // 防复读锁：本招式造成伤害 → 解锁上一个被锁招式

            // 卡肉顿帧（收敛版）：玩家级窗口抑制——同一窗口内后续挥击不再触发卡肉（段2在段1动画结束前挥出则跳过）；
            // 命中→冻结 2 帧（原 3）、玩家/目标速度 ×0.3（原 0.1），且每挥只触发一次
            int nowT = (int)Main.GameUpdateCount;
            if (nowT - mp.LastHitstopTick >= MalachiteMelee.HitstopGapFrames)
            {
                mp.LastHitstopTick = nowT;
                _hitstop = 2;
                owner.velocity *= 0.3f;
                // 【原生击退铁律】击退免疫敌怪（knockBackResist<=0）的速度不被卡肉修改，只保留刃身悬停演出
                if (target.knockBackResist > 0f)
                    target.velocity *= 0.3f;
                _shakeT = Math.Max(_shakeT, 2f);
            }

            // 击飞/浮空（放在卡肉减速之后施加，保证不被减速吞掉）：
            // 段2 挑空（-7.5 + 水平动量 ×0.2，把怪拉到空连高度）；段3 大吹飞（dir×14）；升龙击飞；j.C 击坠。
            // 【原生击退铁律】所有速度覆写先过 knockBackResist 门：免疫击退的敌怪（如 Boss 心脏）绝不被推飞/挑空/击坠；
            // 部分抗性按比例缩放。伤害与特效照常。
            float resist = target.knockBackResist > 0f ? Math.Min(1f, target.knockBackResist) : 0f;
            if (resist > 0f)
            {
                if (Kind == MalachiteMelee.MoveKind.Step2)
                {
                    target.velocity.Y = MalachiteMelee.Step2LaunchY * resist;
                    target.velocity.X *= MalachiteMelee.Step2LaunchDampX;
                    target.netUpdate = true;
                }
                if (Kind == MalachiteMelee.MoveKind.Step3)
                {
                    target.velocity.X = (SlashDir != 0 ? SlashDir : 1) * MalachiteMelee.Step3BlowX * resist;
                    target.netUpdate = true;
                }
                if (Kind == MalachiteMelee.MoveKind.UpperRise && !_upperHit)
                {
                    _upperHit = true;
                    target.velocity.Y = -9f * resist;
                    target.velocity.X *= 0.4f;
                    target.netUpdate = true;
                }
                if (Kind == MalachiteMelee.MoveKind.UpperRise && _upperHit && owner.whoAmI == Main.myPlayer)
                    mp.AerialVortexWindow = 8; // D35：升龙命中后 8 帧窗口（按住 W+F = 翠华流风）
                if (Kind == MalachiteMelee.MoveKind.AirStep3)
                {
                    target.velocity.Y = MalachiteMelee.AirStep3KnockdownY * resist; // j.C 强制击坠（Hard Knockdown）
                    target.netUpdate = true;
                }
                if (Kind == MalachiteMelee.MoveKind.AirNeedle)
                {
                    target.velocity *= 0.15f; // 翠羽点穴硬直（极简实现：速度钉死；完整计时待实测后按需加）
                    target.netUpdate = true;
                }
            }

            Vector2 radial = target.Center - owner.Center;
            if (radial.LengthSquared() < 1f) radial = Vector2.UnitX * (SlashDir != 0 ? SlashDir : 1);
            else radial.Normalize();
            Vector2 travel = Vector2.Zero;
            if (_tip.Count >= 2) travel = _tip[_tip.Count - 1] - _tip[_tip.Count - 2];
            if (travel.LengthSquared() < 1f)
                travel = radial.RotatedBy(MathHelper.PiOver2 * (SlashDir < 0 ? -0.5f : 0.5f));
            travel.Normalize();
            Vector2 perp = new Vector2(-travel.Y, travel.X);

            if (_hitFx <= 0)
            {
                _hitFx = MalachiteMelee.HitFlashFrames;
                _hitPos = target.Center;
                _hitDir = travel;
                _shakeT = Math.Max(_shakeT, 3f);
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.32f, Pitch = 0.25f }, target.Center);
                for (int i = 0; i < 4; i++)
                {
                    if (!EffectLimiterSystem.CanSpawnEffect(1, 60)) break;
                    EffectLimiterSystem.SpawnSpark(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        travel * Main.rand.NextFloat(0.5f, 2.2f) + Main.rand.NextVector2Circular(2f, 2f),
                        Color.White, Main.rand.NextFloat(1.2f, 1.9f), 12);
                }
            }

            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            Color spark = step >= 2 || Kind == MalachiteMelee.MoveKind.Finisher1 || Kind == MalachiteMelee.MoveKind.Finisher2 ? MalachitePalette.AccentGold
                : step == 1 ? Color.Lerp(MalachitePalette.GreenBright, MalachitePalette.AccentCyan, 0.5f) : MalachitePalette.GreenBright;
            int count = Kind == MalachiteMelee.MoveKind.Finisher2 ? 12 : (step >= 2 ? 9 : 6);
            for (int i = 0; i < count; i++)
            {
                if (!EffectLimiterSystem.CanSpawnEffect(1, 60)) break;
                float back = Main.rand.NextFloat(2.5f, 6.5f);
                float side = Main.rand.NextFloat(0f, 3.5f) * (Main.rand.NextBool() ? 1f : -1f);
                Vector2 vel = -travel * back + perp * side;
                EffectLimiterSystem.SpawnSpark(target.Center + Main.rand.NextVector2Circular(12f, 12f), vel,
                    Main.rand.NextBool(5) ? Color.White : spark, Main.rand.NextFloat(0.9f, 1.6f), 20);
            }

            // —— 命中纹章：每次攻击命中敌怪时，在命中位置【且仅】生成一枚纹章（矩阵规格随招式）——
            //    与固定位置纹章合计：每次攻击最少 1 枚、最多 2 枚；多段/多目标命中只补这一次，防弹幕爆炸卡死
            if (!_hitCrestSpawned)
            {
                _hitCrestSpawned = true;
                var spec = MalachiteMelee.CrestSpecOf(Kind);
                EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner, target.Center,
                    Projectile.damage, spec.Knockback, Kind, SlashDir, spec);
            }
        }

        /// <summary>
        /// 纯程序化顶点绘制：修长翡翠晶刃（Slender Crystal Rapier / Laser Katana）——
        /// 极窄半宽 3.2px、82% 平行刃身 + 18% 针尖收锋、微型悬浮剑锷、单分子激光剑脊。
        /// 渲染安全：顶点缓冲静态复用；实体通道(NonPremultiplied) → 高光通道(Additive) → 严格还原 AlphaBlend。
        /// </summary>
        private void DrawProceduralBlade(Vector2 rootWorldPos, float theta, float bladeLength, Color themeColor)
        {
            Vector2 root = rootWorldPos - Main.screenPosition;
            Vector2 dir = theta.ToRotationVector2();
            Vector2 normal = new Vector2(-dir.Y, dir.X); // 垂直刃身法线

            // ================= 细长几何参数调校 =================
            float guardLen = bladeLength * 0.08f;  // 剑锷极短，贴近手部
            float guardWidth = 6.0f;               // 紧凑小巧的剑格
            float midLen = bladeLength * 0.82f;    // 82% 长度均维持平行细长身段
            float midWidth = 3.2f;                 // 核心：半宽仅 3.2 像素！极度修长

            // 6 大关键几何点
            Vector2 pRoot = root;
            Vector2 lGuard = root + dir * guardLen - normal * guardWidth;
            Vector2 rGuard = root + dir * guardLen + normal * guardWidth;
            Vector2 lMid = root + dir * midLen - normal * midWidth;
            Vector2 rMid = root + dir * midLen + normal * midWidth;
            Vector2 pTip = root + dir * bladeLength; // 针尖

            // 阴阳双面 3D 棱晶分色（极细截面下的细微明暗差，打造精密手术刀质感；色值全部来自调色板）
            Color cSpine = Color.White * 0.95f;                                     // 剑脊纯白
            Color cLeftFace = Color.Lerp(themeColor, Color.White, 0.38f);            // 阳面（微亮透光翠绿）
            Color cRightFace = Color.Lerp(themeColor, MalachitePalette.GreenDeep, 0.40f); // 阴面（深邃翡翠）
            Color cEdgeOut = themeColor * 0.75f;

            // 左半刃面（3个三角形）
            _bladeVerts[0] = new VertexPositionColor(new Vector3(pRoot, 0), cSpine);
            _bladeVerts[1] = new VertexPositionColor(new Vector3(lGuard, 0), cEdgeOut);
            _bladeVerts[2] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);

            _bladeVerts[3] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            _bladeVerts[4] = new VertexPositionColor(new Vector3(lGuard, 0), cLeftFace);
            _bladeVerts[5] = new VertexPositionColor(new Vector3(lMid, 0), cLeftFace);

            _bladeVerts[6] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            _bladeVerts[7] = new VertexPositionColor(new Vector3(lMid, 0), cLeftFace);
            _bladeVerts[8] = new VertexPositionColor(new Vector3(pTip, 0), cSpine);

            // 右半刃面（3个三角形）
            _bladeVerts[9] = new VertexPositionColor(new Vector3(pRoot, 0), cSpine);
            _bladeVerts[10] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            _bladeVerts[11] = new VertexPositionColor(new Vector3(rGuard, 0), cEdgeOut);

            _bladeVerts[12] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            _bladeVerts[13] = new VertexPositionColor(new Vector3(rMid, 0), cRightFace);
            _bladeVerts[14] = new VertexPositionColor(new Vector3(rGuard, 0), cRightFace);

            _bladeVerts[15] = new VertexPositionColor(new Vector3(root + dir * guardLen, 0), cSpine);
            _bladeVerts[16] = new VertexPositionColor(new Vector3(pTip, 0), cSpine);
            _bladeVerts[17] = new VertexPositionColor(new Vector3(rMid, 0), cRightFace);

            // 1. 实体细长刃身绘制 (NonPremultiplied)
            Main.spriteBatch.End();

            if (_fanFx == null || _fanFx.IsDisposed) // 显卡设备重置后重建（与 _slashTex 缓存同构）
            {
                _fanFx = new BasicEffect(Main.graphics.GraphicsDevice)
                {
                    VertexColorEnabled = true,
                    LightingEnabled = false,
                    FogEnabled = false
                };
            }
            _fanFx.World = Matrix.Identity;
            _fanFx.View = Main.GameViewMatrix.TransformationMatrix;
            _fanFx.Projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, 0f, 1f);
            _fanFx.TextureEnabled = false;

            Main.graphics.GraphicsDevice.BlendState = BlendState.NonPremultiplied;
            Main.graphics.GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            Main.graphics.GraphicsDevice.DepthStencilState = DepthStencilState.None;

            foreach (EffectPass pass in _fanFx.CurrentTechnique.Passes) pass.Apply();
            Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _bladeVerts, 0, 6);

            // 2. 细长高亮构件绘制 (Additive)
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D flareTex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Vector2 origin = flareTex.Size() * 0.5f;

            // ① 单分子级极细贯穿白光剑脊（粗细 0.035f）
            Vector2 spineMid = root + dir * (bladeLength * 0.5f);
            Main.spriteBatch.Draw(flareTex, spineMid, null, Color.White * 0.95f, theta, origin,
                new Vector2(bladeLength / flareTex.Width * 1.85f, 0.035f), SpriteEffects.None, 0f);

            // ② 剑锷微型悬浮菱形圣印（精简收小，不抢剑身风头）
            Vector2 hiltPos = root + dir * guardLen;
            Main.spriteBatch.Draw(flareTex, hiltPos, null, MalachitePalette.AccentGold * 0.75f, theta + MathHelper.PiOver4, origin,
                new Vector2(0.30f, 0.09f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(flareTex, hiltPos, null, MalachitePalette.GreenBright * 0.75f, theta - MathHelper.PiOver4, origin,
                new Vector2(0.30f, 0.09f), SpriteEffects.None, 0f);

            // ③ 针尖高频穿透星芒（小巧锐利的极星刺针）
            Main.spriteBatch.Draw(flareTex, pTip, null, Color.White * 0.9f, theta, origin,
                new Vector2(0.26f, 0.05f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(flareTex, pTip, null, MalachitePalette.GreenBright * 0.65f, theta + MathHelper.PiOver2, origin,
                new Vector2(0.18f, 0.04f), SpriteEffects.None, 0f);

            // 严密还原默认批次
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void DrawSeg(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 1f || width < 0.5f) return;
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, mid, null, color, d.ToRotation(),
                new Vector2(0.5f, 0.5f), new Vector2(len, Math.Max(1f, width)), SpriteEffects.None, 0f);
        }

        private float KindRingK()
        {
            return Kind switch
            {
                MalachiteMelee.MoveKind.Finisher2 => 1.9f,
                MalachiteMelee.MoveKind.Finisher1 => 1.5f,
                MalachiteMelee.MoveKind.Charged => 1.4f,
                MalachiteMelee.MoveKind.Step3 => 1.3f,
                _ => 1f,
            };
        }

        public override bool PreDraw(ref Color lightColor)
        {
            try { DrawCore(ref lightColor); } catch { /* 任何绘制异常都不允许漏到引擎 */ }
            return false;
        }

        private void DrawCore(ref Color lightColor)
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || _th.Count == 0) return;

            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            // 色彩深度校正（模块一）：内缘/主体恒为高饱和翡翠绿，段2 不再用死白；金只留给段3/终结
            Color tint = (Kind == MalachiteMelee.MoveKind.Finisher1 || Kind == MalachiteMelee.MoveKind.Finisher2
                || Kind == MalachiteMelee.MoveKind.BackflipRetreat || Kind == MalachiteMelee.MoveKind.AirNeedle || Kind == MalachiteMelee.MoveKind.LandingSweep
                || Kind == MalachiteMelee.MoveKind.WarpStep || Kind == MalachiteMelee.MoveKind.AerialVortex) ? MalachitePalette.AccentGold
                : Kind == MalachiteMelee.MoveKind.Charged ? MalachitePalette.GreenBright
                : step switch { 0 => MalachitePalette.GreenBright, 1 => Color.Lerp(MalachitePalette.GreenBright, MalachitePalette.AccentCyan, 0.5f), _ => MalachitePalette.AccentGold };

            Vector2 jit = _shakeT > 0f ? Main.rand.NextVector2Circular(_shakeT * 0.30f, _shakeT * 0.30f) : Vector2.Zero;
            if (_hitstop > 0) jit += Main.rand.NextVector2Circular(3f, 3f); // 卡肉期高频微震（§三.2）
            Vector2 oc = owner.Center + jit;
            float holdFade = _age > SweepEnd
                ? MathHelper.Lerp(1f, 0.5f, (_age - SweepEnd) / (float)Math.Max(1, HoldFrames + ExtraHold))
                : 1f;

            // ① 特殊招式（下砸/突进/大招二段）走独立绘制；普通招式绘制扇形刀光网格
            // —— 雀跃·凌虚（2026-09-05 重做）：不画刀刃，垂直上升金线随 _age 淡出 ——
            if (Kind == MalachiteMelee.MoveKind.WarpStep)
            {
                float wf = 1f - _age / 12f;
                Vector2 wp = oc - Main.screenPosition;
                for (int k = 0; k < 3; k++)
                    Main.spriteBatch.Draw(AdditiveLayer.Pixel, wp + new Vector2((k - 1) * 8f, 4f + _age * 3.2f), null,
                        MalachitePalette.AccentGold * (0.45f * wf), 0f, new Vector2(0.5f, 0.5f), new Vector2(1.6f, 16f), SpriteEffects.None, 0f);
                return;
            }

            if (Kind == MalachiteMelee.MoveKind.AirDive || Kind == MalachiteMelee.MoveKind.DashCut || Kind == MalachiteMelee.MoveKind.Finisher2
                || Kind == MalachiteMelee.MoveKind.BackflipRetreat || Kind == MalachiteMelee.MoveKind.AirNeedle || Kind == MalachiteMelee.MoveKind.LandingSweep
                || Kind == MalachiteMelee.MoveKind.AerialVortex)
            {
                DrawModeCore(oc, tint, holdFade);
                return;
            }

            // —— 特效层批2（2026-09-05）：加速风线——挥速峰值段的刀尖切线短划线（窗口 p∈[0.35,0.8]，峰值 ≈0.62）——
            float sweepP = Math.Clamp((_age - GatherFrames) / (float)Math.Max(1, SweepFrames), 0f, 1f);
            float windK = MathHelper.SmoothStep(0f, 1f, (sweepP - 0.35f) / 0.20f) * (1f - MathHelper.SmoothStep(0f, 1f, (sweepP - 0.62f) / 0.18f));
            if (windK > 0.02f && _th.Count > 0)
            {
                float windA = _th[_th.Count - 1];
                Vector2 windDir = new Vector2((float)Math.Cos(windA), (float)Math.Sin(windA));
                Vector2 windTip = oc + windDir * (_sc[_sc.Count - 1] * MalachiteMelee.SlashArtWidth * _sx[_sx.Count - 1]);
                for (int k = 0; k < 3; k++)
                {
                    Vector2 windPos = windTip - windDir * (12f + k * 16f) - Main.screenPosition;
                    Main.spriteBatch.Draw(AdditiveLayer.Pixel, windPos, null, tint * (0.35f * windK * (1f - k * 0.25f)), windA,
                        new Vector2(0.5f, 0.5f), new Vector2(18f, 1.3f), SpriteEffects.None, 0f);
                }
            }

            // —— 特效层（2026-09-05）：残影剑身——历史姿态回放（同锚点/旧角度/旧缩放），引擎批次内半透明叠画，不换批次 ——
            Texture2D ghostTex = SlashTex();
            Vector2 ghostOrigin = new Vector2(0f, MalachiteMelee.SlashArtPivotY);
            for (int g = 2; g <= 6; g += 2)
            {
                int gi = _th.Count - 1 - g;
                if (gi < 0 || gi >= _sx.Count || gi >= _sy.Count || gi >= _sc.Count) break; // 历史列表锁步写入，防御性越界检查
                float ga = 0.30f * (1f - g / 8f) * holdFade;
                Main.spriteBatch.Draw(ghostTex, oc - Main.screenPosition, null, tint * ga, _th[gi], ghostOrigin,
                    new Vector2(_sx[gi], _sy[gi]) * _sc[gi], SpriteEffects.None, 0f);
            }

            // —— 特效层（2026-09-05）：精准叠层环绕光点——命中累计 1~5 层，金芯翠晕绕身（本地玩家可见）——
            var mpFx = owner.GetModPlayer<MalachitePlayer>();
            if (mpFx.PrecisionStacks > 0 && owner.whoAmI == Main.myPlayer)
            {
                int orbN = Math.Min(mpFx.PrecisionStacks, MalachiteMelee.PrecisionMaxStacks);
                float baseAng = Main.GameUpdateCount * 0.12f;
                for (int i = 0; i < orbN; i++)
                {
                    float orbAng = baseAng + MathHelper.TwoPi * i / Math.Max(1, orbN);
                    Vector2 orbPos = oc + new Vector2((float)Math.Cos(orbAng), (float)Math.Sin(orbAng)) * 30f - Main.screenPosition;
                    Main.spriteBatch.Draw(AdditiveLayer.Pixel, orbPos, null, MalachitePalette.GreenBright * 0.55f, 0f, new Vector2(0.5f), 5f, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(AdditiveLayer.Pixel, orbPos, null, MalachitePalette.AccentGold * 0.9f, 0f, new Vector2(0.5f), 2f, SpriteEffects.None, 0f);
                }
            }

            // ② 刀光轨迹：月牙梭形风痕拉丝网格（尾迹层）
            DrawFanStrip(oc, tint, holdFade);

            // ③ 击中反馈环与划痕
            if (_hitFx > 0)
            {
                float t = _hitFx / (float)MalachiteMelee.HitFlashFrames;
                float ringR = MalachiteMelee.HitRingMaxR * KindRingK() * (1f - t) + 5f;
                Color rc = Color.Lerp(Color.White, tint, 0.4f) * (0.9f * t);
                for (int s = 0; s < 14; s++)
                {
                    float a0 = MathHelper.TwoPi * s / 14f;
                    float a1 = MathHelper.TwoPi * (s + 1) / 14f;
                    DrawSeg(_hitPos + jit + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * ringR,
                            _hitPos + jit + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * ringR, 2.5f, rc);
                }
                Vector2 hd = _hitDir;
                DrawSeg(_hitPos + jit - hd * (MalachiteMelee.HitSlashLen * 0.55f),
                        _hitPos + jit + hd * (MalachiteMelee.HitSlashLen * 0.45f), 3.5f, Color.White * (0.85f * t));
            }

            // ③.5 实体修长晶刃（Slender Crystal Rapier）：程序化顶点细刃叠在最上层——
            //     极窄半宽 3.2px、82% 平行刃身 + 18% 针尖收锋、单分子激光剑脊、微型剑锷、针尖星芒
            int nBlade = _th.Count;
            DrawProceduralBlade(owner.Center, _th[nBlade - 1], _sc[nBlade - 1] * MalachiteMelee.SlashArtWidth * _sx[nBlade - 1] * 1.04f, tint);

            // 严格还原为默认的 AlphaBlend 批次状态，避免污染游戏后续绘制
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // ============ 特殊招式状态机（§一.4 下砸 / §一.6 突进）============

        private void RecordPose()
        {
            float artScale = (MalachiteMelee.StepReach(0) * 0.9f) / MalachiteMelee.SlashArtWidth;
            _th.Add(Projectile.rotation);
            _sc.Add(artScale);
            _sx.Add(1f);
            _sy.Add(1f);
            _tip.Add(Projectile.Center);
            TrimHistory();
        }

        /// <summary>空中陨石下砸（AirDive/Bors）：重力贯穿急坠 + 全屏下坠残影 + 触地引爆（§一.4）。</summary>
        private void AirDiveAI(Player owner)
        {
            if (_age == 0)
            {
                // 爆发帧物理：垂直速度拉满 + 水平动量收窄（§三.1）
                owner.velocity.Y = MalachiteMelee.AirDiveSpeedY;
                owner.velocity.X *= MalachiteMelee.AirDiveDampX;
            }
            // 设计动作自带触地：刷新掉落伤害基准，防止摔伤
            owner.fallStart = (int)(owner.position.Y / 16f);

            Projectile.friendly = true;
            Projectile.rotation = MathHelper.PiOver2;
            Projectile.Center = owner.Center + new Vector2(SlashDir * 10f, 30f);
            RecordPose();

            // 下坠拖尾火花（视觉重做 2026-09-05）：每 3 帧两枚向上拖尾（翠+金）
            if (_age % 3 == 0 && EffectLimiterSystem.CanSpawnEffect(1, 90))
            {
                for (int s = 0; s < 2; s++)
                    EffectLimiterSystem.SpawnSpark(owner.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), -20f - s * 12f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.5f, 3.5f)),
                        s == 0 ? MalachitePalette.GreenBright : MalachitePalette.AccentGold, Main.rand.NextFloat(0.9f, 1.3f), 12);
            }

            // 触地/碰块引爆（§一.4 Ground Collision）
            if (_age > 3 && !_impactDone)
            {
                bool landed = owner.velocity.Y == 0f;
                bool hitTile = Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)
                    || Collision.SolidCollision(owner.position + new Vector2(0f, owner.height + 4f), owner.width, 10);
                if (landed || hitTile) Impact(owner);
            }

            if (_impactT > 0) _impactT--;
            if (_age > 60) { Projectile.Kill(); return; }
            _age++;
        }

        /// <summary>突进居合切（DashCut/Mordred）：穿透无敌 + 极速突进 + 沿途三枚延时切线纹章（§一.6）。</summary>
        private void DashCutAI(Player owner)
        {
            if (_age == 0)
            {
                // 爆发帧物理：极速突进 + 穿透无敌帧（§三.1）
                owner.velocity.X = (SlashDir != 0 ? SlashDir : 1) * MalachiteMelee.DashCutSpeed;
                owner.immune = true;
                owner.immuneTime = Math.Max(owner.immuneTime, MalachiteMelee.DashCutImmuneFrames);

                // 出刀扇形火花（视觉增强 2026-09-05）：8 枚金/白火花沿突进方向喷发
                for (int s = 0; s < 8 && EffectLimiterSystem.CanSpawnEffect(1, 90); s++)
                {
                    float a0 = (s / 7f - 0.5f) * 0.9f + (SlashDir > 0 ? 0f : MathHelper.Pi);
                    EffectLimiterSystem.SpawnSpark(owner.Center + new Vector2(SlashDir * 40f, -6f),
                        new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * 4.5f,
                        s % 3 == 0 ? MalachitePalette.White : MalachitePalette.AccentGold, Main.rand.NextFloat(0.8f, 1.3f), 12);
                }
            }

            Projectile.friendly = true;
            Projectile.rotation = SlashDir > 0 ? 0f : MathHelper.Pi;
            Projectile.Center = owner.Center + new Vector2(SlashDir * 54f, 2f);
            RecordPose();

            if (_age == 2)
            {
                // 沿突进路径等距 3 枚微型纹章（6/9/12 帧递进引爆，§四矩阵）
                var spec = MalachiteMelee.CrestSpecOf(MalachiteMelee.MoveKind.DashCut);
                for (int k = 0; k < 3; k++)
                {
                    Vector2 pos = owner.Center + new Vector2(SlashDir * (55f + 55f * k), -4f);
                    EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner, pos,
                        Projectile.damage, spec.Knockback, MalachiteMelee.MoveKind.DashCut, SlashDir, spec,
                        delayOverride: 6 + 3 * k);
                }
            }

            if (_age >= 16) { Projectile.Kill(); return; }
            _age++;
        }

        /// <summary>雀返（BackflipRetreat/D31）：后空翻脱战残影技——位移与无敌帧由玩家层施加，本弹幕 12 帧金色残影 + 翠羽光尘（纯视觉）。</summary>
        private void BackflipRetreatAI(Player owner)
        {
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            // 修复（2026-09-05 实测反馈）：刀向按朝向——面向右则刃指左(Pi)，面向左则刃指右(0)，不再恒指屏幕左
            Projectile.rotation = SlashDir > 0 ? MathHelper.Pi : 0f;
            Projectile.Center = owner.Center + new Vector2(-SlashDir * 18f, -10f);
            RecordPose();

            // 金色弧光轨迹（每 2 帧两枚弧线光尘，随 _age 淡出）
            if (_age < 11 && _age % 2 == 0 && EffectLimiterSystem.CanSpawnEffect(1, 70))
            {
                float fade = 1f - _age / 14f;
                for (int k = 0; k < 2; k++)
                {
                    float aa = (SlashDir > 0 ? MathHelper.Pi : 0f) + (k == 0 ? 0.35f : -0.35f);
                    EffectLimiterSystem.SpawnSpark(owner.Center + new Vector2((float)Math.Cos(aa), (float)Math.Sin(aa)) * (20f + k * 14f) + new Vector2(0f, -6f),
                        Vector2.Zero, MalachitePalette.AccentGold, 0.9f * fade, 8);
                }
            }

            // 翠羽光尘（保留）
            if (_age % 2 == 0 && EffectLimiterSystem.CanSpawnEffect(1, 80))
            {
                Color feather = Main.rand.NextBool(3) ? MalachitePalette.AccentGold : MalachitePalette.GreenBright;
                EffectLimiterSystem.SpawnSpark(owner.Center + new Vector2(-SlashDir * 14f, -16f) + Main.rand.NextVector2Circular(8f, 10f),
                    new Vector2(-SlashDir * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(0.5f, 2f)),
                    feather, Main.rand.NextFloat(0.7f, 1.1f), 12);
            }

            if (_age >= 14) { Projectile.Kill(); return; }
            _age++;
        }

        /// <summary>翠羽点穴（AirNeedle/D32）：45° 斜下突刺 10 帧；命中借力反冲（_hitFx 置位当帧检测一次）。</summary>
        private void AirNeedleAI(Player owner)
        {
            if (_age == 0)
            {
                Projectile.velocity = new Vector2((SlashDir != 0 ? SlashDir : 1) * MalachiteMelee.AirNeedleVX, MalachiteMelee.AirNeedleVY);
                Projectile.width = 46;
                Projectile.height = 26;
                Projectile.friendly = true;
            }

            if (_age < 10)
            {
                // 位移交给引擎（AI 后自动 position += velocity），此处只更新朝向——Claude 复核：手动加法会双倍速度
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            else
            {
                // 10 帧后失去点穴力：自然下坠直至碰块/超时
                Projectile.friendly = false;
                Projectile.velocity.Y += 0.45f;
                Projectile.rotation = Projectile.velocity.ToRotation();
                if (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height) || _age > 30)
                { Projectile.Kill(); return; }
            }

            // 命中借力反冲（_hitFx 由 OnHitNPC 置位）——只触发一次，供空中接升龙
            if (_hitFx > 0 && !_upperHit)
            {
                _upperHit = true;
                owner.velocity.Y = MalachiteMelee.AirNeedleBounceY;
            }

            RecordPose();
            _age++;
        }

        /// <summary>拂柳穿心（LandingSweep/D33）：下砸落地目押滑铲横扫 14 帧，贴地低姿态 + 半月压扁纹章 + 滑尘。</summary>
        private void LandingSweepAI(Player owner)
        {
            if (_age == 0)
            {
                owner.velocity.X = (SlashDir != 0 ? SlashDir : 1) * MalachiteMelee.LandingSweepVX;
                var spec = MalachiteMelee.CrestSpecOf(MalachiteMelee.MoveKind.LandingSweep);
                Vector2 groundPos = new Vector2(owner.Center.X, owner.position.Y + owner.height + 2f);
                EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner, groundPos,
                    Projectile.damage, spec.Knockback, MalachiteMelee.MoveKind.LandingSweep, SlashDir, spec);
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.7f, Pitch = 0.1f }, owner.Center);
                Projectile.width = 120;
                Projectile.height = 30;
            }

            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.rotation = SlashDir > 0 ? 0f : MathHelper.Pi;
            Projectile.Center = owner.Center + new Vector2((SlashDir != 0 ? SlashDir : 1) * 34f, owner.height * 0.45f);
            RecordPose();

            if (_age % 3 == 0 && EffectLimiterSystem.CanSpawnEffect(1, 90))
                EffectLimiterSystem.SpawnSpark(Projectile.Center + new Vector2(0f, 8f) + Main.rand.NextVector2Circular(10f, 3f),
                    new Vector2((SlashDir != 0 ? SlashDir : 1) * Main.rand.NextFloat(1f, 3f), 0f),
                    MalachitePalette.AccentGold, Main.rand.NextFloat(0.8f, 1.3f), 12);

            if (_age >= 14) { Projectile.Kill(); return; }
            _age++;
        }

        /// <summary>雀跃·凌虚（WarpStep/D34）：抛物线瞬步视觉——位移/无敌由玩家层施加，本弹幕 10 帧翠羽光尘。</summary>
        private void WarpStepAI(Player owner)
        {
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.rotation = MathHelper.PiOver2;
            Projectile.Center = owner.Center + new Vector2(0f, -16f);
            RecordPose();

            if (_age % 2 == 0 && EffectLimiterSystem.CanSpawnEffect(1, 80))
                EffectLimiterSystem.SpawnSpark(owner.Center + Main.rand.NextVector2Circular(10f, 14f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.5f, 1.5f)),
                    MalachitePalette.GreenBright, Main.rand.NextFloat(0.6f, 1.0f), 10);

            if (_age >= 12) { Projectile.Kill(); return; }
            _age++;
        }

        /// <summary>翠华流风（AerialVortex/D35）：滞空回旋绞杀——吸附 + 三段切割，结束自动派生 j.A。</summary>
        private void AerialVortexAI(Player owner)
        {
            if (_age == 0)
            {
                var mp = owner.GetModPlayer<MalachitePlayer>();
                mp.AirStallFrames = Math.Max(mp.AirStallFrames, MalachiteMelee.AerialVortexFrames); // 绞杀全程锁重力
                owner.velocity.Y = Math.Min(owner.velocity.Y, -2f); // 轻微升力进入滞空
                Projectile.width = 90;
                Projectile.height = 90;
                Projectile.friendly = true;
                var spec = MalachiteMelee.CrestSpecOf(MalachiteMelee.MoveKind.AerialVortex);
                EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner, owner.Center,
                    Projectile.damage, spec.Knockback, MalachiteMelee.MoveKind.AerialVortex, SlashDir, spec);
            }

            // 回旋刃：绕玩家旋转（半径 46px）
            float spin = _age * 0.55f * (SlashDir != 0 ? SlashDir : 1);
            Vector2 orbit = new Vector2((float)Math.Cos(spin), (float)Math.Sin(spin)) * 46f;
            Projectile.Center = owner.Center + orbit;
            Projectile.rotation = spin + MathHelper.PiOver2;
            Projectile.friendly = true;
            RecordPose();

            // 吸附：半径内敌怪向心拉扯（尊重原生击退免疫）
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.knockBackResist <= 0f) continue;
                Vector2 to = owner.Center - n.Center;
                float dSq = to.LengthSquared();
                if (dSq > MalachiteMelee.AerialVortexRadius * MalachiteMelee.AerialVortexRadius || dSq < 1f) continue;
                n.velocity += Vector2.Normalize(to) * (MalachiteMelee.AerialVortexPull * Math.Min(1f, n.knockBackResist));
            }

            // 三段切割（age 6/12/18；服务器权威，避免客户端重复结算）
            if ((_age == 6 || _age == 12 || _age == 18) && Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC n = Main.npc[i];
                    if (!n.active || n.friendly || n.DistanceSQ(owner.Center) > MalachiteMelee.AerialVortexRadius * MalachiteMelee.AerialVortexRadius) continue;
                    NPC.HitInfo hi = new NPC.HitInfo { Damage = Projectile.damage, Knockback = 2f, HitDirection = n.Center.X < owner.Center.X ? 1 : -1, DamageType = MindDamageClass.Instance };
                    n.StrikeNPC(hi);
                    n.netUpdate = true;
                }
                owner.GetModPlayer<MalachitePlayer>().ReportSpecialHit(MalachiteMelee.MoveKind.AerialVortex); // 绞杀切割解锁复读锁
                // 切割帧白核闪（视觉重做 2026-09-05）：中心白点 + 金尘
                if (EffectLimiterSystem.CanSpawnEffect(2, 100))
                {
                    EffectLimiterSystem.SpawnSpark(owner.Center, Vector2.Zero, MalachitePalette.White, 1.6f, 6);
                    EffectLimiterSystem.SpawnSpark(owner.Center + Main.rand.NextVector2Circular(20f, 20f), Vector2.Zero, MalachitePalette.AccentGold, 1.2f, 8);
                }
                if (_age == 18 && EffectLimiterSystem.CanSpawnEffect(4, 100))
                {
                    for (int s = 0; s < 8; s++)
                        EffectLimiterSystem.SpawnSpark(owner.Center + Main.rand.NextVector2Circular(40f, 40f),
                            Main.rand.NextVector2Circular(3f, 3f), MalachitePalette.AccentGold, 1.1f, 14);
                }
            }

            // 结束自动派生 j.A（空战链起点）
            if (_age >= MalachiteMelee.AerialVortexFrames)
            {
                int wd = owner.HeldItem != null ? owner.GetWeaponDamage(owner.HeldItem) : 32;
                var mp2 = owner.GetModPlayer<MalachitePlayer>();
                MalachiteMelee.FireAirChain(owner, owner.GetSource_Misc("MalachiteMove"), wd, 0);
                mp2.AirComboStep = 1;
                Projectile.Kill();
                return;
            }
            _age++;
        }

        // ============ 两段式大招状态机（Finisher1 顶点顿挫 / Finisher2 俯冲贯穿）============

        /// <summary>二段俯冲贯穿（Finisher2）：极速斜下流星贯地 + 穿透无敌 + 终点终极顿挫（§大招模块一）。</summary>
        private void Finisher2AI(Player owner)
        {
            if (_age == 0)
            {
                // 爆发帧物理：斜下前俯冲疾驰 + 穿透无敌
                owner.velocity.X = (SlashDir != 0 ? SlashDir : 1) * MalachiteMelee.Finisher2SpeedX;
                owner.velocity.Y = MalachiteMelee.Finisher2SpeedY;
                owner.immune = true;
                owner.immuneTime = Math.Max(owner.immuneTime, MalachiteMelee.Finisher2ImmuneFrames);
                owner.fallStart = (int)(owner.position.Y / 16f);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.85f, Pitch = -0.3f }, owner.Center);
            }

            Projectile.friendly = true;
            Projectile.rotation = SlashDir > 0 ? 0.62f : MathHelper.Pi - 0.62f; // 大剑锁定斜前下方
            Projectile.Center = owner.Center + new Vector2(SlashDir * 40f, 26f);
            RecordPose();

            // 流星贯穿残影（限流）
            if (_age % 2 == 0 && EffectLimiterSystem.CanSpawnEffect(1, 90))
                EffectLimiterSystem.SpawnSpark(owner.Center + new Vector2(-SlashDir * 14f, 6f) + Main.rand.NextVector2Circular(10f, 8f),
                    new Vector2(-SlashDir * 2f, 1.5f), MalachitePalette.AccentGold, 1.3f, 14);

            // 贯穿终点：触地 / 撞块 / 突进满 12 帧 → 终极顿挫
            if (!_impactDone && (_age >= 12 || owner.velocity.Y == 0f
                || Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)
                || Collision.SolidCollision(owner.position + new Vector2(0f, owner.height + 4f), owner.width, 10)))
            {
                Finisher2Impact(owner);
            }

            if (_impactT > 0) _impactT--;
            if (_impactDone && _impactT <= 0) { Projectile.Kill(); return; }
            if (_age > 60) { Projectile.Kill(); return; }
            _age++;
        }

        /// <summary>二段终点终极顿挫：10 帧极强卡肉定格 + 8 级强震 + 终阶巨单翼 + 全屏空间引爆。</summary>
        private void Finisher2Impact(Player owner)
        {
            _impactDone = true;
            _impactT = 14;
            _hitstop = MalachiteMelee.Finisher2HitstopFrames; // 10 帧极强卡肉
            owner.velocity *= 0.05f;
            owner.GetModPlayer<MalachitePlayer>().AirStallFrames = MalachiteMelee.Finisher2HitstopFrames;
            ScreenShakeSystem.Shake(8f);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f, Pitch = -0.3f }, Projectile.Center);

            // 终阶超巨型单翼完全解放（Tier 1）
            EsCrestWingsProj.Spawn(owner, 1);

            // 全屏空间引爆：超巨型金纹章立即引爆（210% 全额破甲大伤害）
            var spec = MalachiteMelee.CrestSpecOf(MalachiteMelee.MoveKind.Finisher2);
            EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner, owner.Center,
                Projectile.damage, spec.Knockback, MalachiteMelee.MoveKind.Finisher2, SlashDir, spec, delayOverride: 0);
        }

        /// <summary>下砸触地：8 级全屏震屏 + 十字冲击波 + 范围伤害一次 + 脚底贴地巨型纹章（§一.4/§四）。</summary>
        private void Impact(Player owner)
        {
            _impactDone = true;
            _impactT = 12;
            owner.GetModPlayer<MalachitePlayer>().ReportSpecialHit(MalachiteMelee.MoveKind.AirDive); // 下砸触地伤害解锁复读锁
            // D33 目押窗口（拂柳穿心）：落地 6 帧内按 F 转滑铲横扫（仅本地玩家）
            if (owner.whoAmI == Main.myPlayer)
                owner.GetModPlayer<MalachitePlayer>().LandingSweepWindow = 6;
            ScreenShakeSystem.Shake(8f);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.25f }, Projectile.Center);

            // 触地引爆纹章：脚底地表中心、横向极度压扁（矩阵 AirDive：scale 1.9 / (2.5,0.35) / 立即引爆 / 150%）
            var spec = MalachiteMelee.CrestSpecOf(MalachiteMelee.MoveKind.AirDive);
            Vector2 groundCenter = new Vector2(owner.Center.X, owner.position.Y + owner.height + 4f);
            EsCrestSigilProj.SpawnCrest(Projectile.GetSource_FromAI(), Projectile.owner, groundCenter,
                Projectile.damage, spec.Knockback, MalachiteMelee.MoveKind.AirDive, SlashDir, spec);

            // 左右对称地刺粒子（地面冲击波向两侧喷发）
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 6; k++)
                {
                    if (!EffectLimiterSystem.CanSpawnEffect(1, 90)) break;
                    EffectLimiterSystem.SpawnSpark(groundCenter + new Vector2(side * (24f + k * 20f), -4f),
                        new Vector2(side * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(4f, 8f)),
                        MalachitePalette.AccentGold, Main.rand.NextFloat(0.9f, 1.5f), 16);
                }
            }

            int dmg = Math.Max(1, (int)(Projectile.damage * 1.2f));
            Rectangle zone = new Rectangle((int)Projectile.Center.X - 80, (int)Projectile.Center.Y - 80, 160, 160);
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || !npc.Hitbox.Intersects(zone)) continue;
                NPC.HitInfo hi = new NPC.HitInfo { Damage = dmg, Knockback = 6f, HitDirection = npc.Center.X < Projectile.Center.X ? 1 : -1, DamageType = MindDamageClass.Instance };
                npc.StrikeNPC(hi); // 原生击退由 StrikeNPC 按 knockBackResist 结算（免疫者不受影响）
                if (npc.knockBackResist > 0f) // 上抛浮空同样尊重原生击退免疫
                    npc.velocity.Y -= 8f * Math.Min(1f, npc.knockBackResist);
                npc.netUpdate = true;
            }
        }

        /// <summary>下砸/突进的 Additive 绘制：刃身 + 速度残线 + （下砸）十字冲击波/扩散环。绘制后严格还原批次。</summary>
        private void DrawModeCore(Vector2 oc, Color tint, float holdFade)
        {
            if (_th.Count == 0) return;
            Texture2D tex = SlashTex();
            Vector2 origin = new Vector2(0f, MalachiteMelee.SlashArtPivotY);
            Vector2 curScale = new Vector2(_sx[_sx.Count - 1], _sy[_sy.Count - 1]) * _sc[_sc.Count - 1];
            float rot = _th[_th.Count - 1];
            Vector2 pivot = oc - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.spriteBatch.Draw(tex, pivot, null, tint * (0.9f * MalachiteMelee.SlashArtAlpha * holdFade), rot, origin, curScale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pivot, null, Color.White * (0.7f * MalachiteMelee.SlashArtAlpha * holdFade), rot, origin, curScale * 0.9f, SpriteEffects.None, 0f);

            // —— 特效层（2026-09-05）：突进/下砸/俯冲残影剑身（历史姿态回放）——
            for (int g = 2; g <= 6; g += 2)
            {
                int gi = _th.Count - 1 - g;
                if (gi < 0 || gi >= _sx.Count || gi >= _sy.Count || gi >= _sc.Count) break; // 历史列表锁步写入，防御性越界检查
                float ga = 0.32f * (1f - g / 8f) * holdFade;
                Main.spriteBatch.Draw(tex, pivot, null, tint * ga, _th[gi], origin, new Vector2(_sx[gi], _sy[gi]) * _sc[gi], SpriteEffects.None, 0f);
            }

            // 速度残线（朝运动反方向拉出）
            Vector2 back = Kind == MalachiteMelee.MoveKind.AirDive ? -Vector2.UnitY : -new Vector2((float)Math.Cos(rot), (float)Math.Sin(rot));
            for (int k = 1; k <= 3; k++)
                DrawSeg(oc + back * (46f * k), oc + back * (46f * k + 18f), 3f, tint * (0.5f * holdFade / k));

            // —— 突进金色拖尾划线（视觉增强 2026-09-05）——
            if (Kind == MalachiteMelee.MoveKind.DashCut)
            {
                float dk = MathHelper.Lerp(1f, 0.5f, _age / 16f) * holdFade;
                for (int t = 0; t < 3; t++)
                {
                    float off = 30f + t * 25f;
                    DrawSeg(oc + back * off, oc + back * (off + 14f), 3f, MalachitePalette.AccentGold * (0.5f - t * 0.15f) * dk);
                }
            }

            // —— 雀返金色弧光轨迹（视觉重做 2026-09-05）——
            if (Kind == MalachiteMelee.MoveKind.BackflipRetreat)
            {
                float bf = 1f - _age / 14f;
                for (int k = 0; k < 2; k++)
                {
                    float aa = (SlashDir > 0 ? MathHelper.Pi : 0f) + (k == 0 ? 0.4f : -0.4f);
                    Vector2 dd = new Vector2((float)Math.Cos(aa), (float)Math.Sin(aa));
                    DrawSeg(oc + dd * (22f + k * 16f), oc + dd * (36f + k * 16f), 2.5f, MalachitePalette.AccentGold * (0.5f * bf));
                }
            }

            // —— 下砸 46px 双层呼吸光环（视觉重做 2026-09-05）——
            if (Kind == MalachiteMelee.MoveKind.AirDive)
            {
                float breath = 1f + 0.15f * (float)Math.Sin(Main.GameUpdateCount * 0.4f);
                for (int s = 0; s < 24; s++)
                {
                    float a0 = MathHelper.TwoPi * s / 24f;
                    float a1 = MathHelper.TwoPi * (s + 1) / 24f;
                    DrawSeg(oc + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * (46f * breath),
                            oc + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * (46f * breath), 2.5f, MalachitePalette.AccentGold * 0.35f * holdFade);
                }
                for (int s = 0; s < 16; s++)
                {
                    float a0 = MathHelper.TwoPi * s / 16f;
                    float a1 = MathHelper.TwoPi * (s + 1) / 16f;
                    DrawSeg(oc + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * 32f,
                            oc + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * 32f, 2f, MalachitePalette.PrimaryGreen * 0.5f * holdFade);
                }
            }

            // —— 绞杀开屏扇放射金线 + 120px 呼吸环（视觉重做 2026-09-05）——
            if (Kind == MalachiteMelee.MoveKind.AerialVortex)
            {
                float fanA = _age * 0.55f * (SlashDir != 0 ? SlashDir : 1);
                for (int i = 0; i < 5; i++)
                {
                    float aa = fanA + 0.35f * (i - 2);
                    Vector2 dd = new Vector2((float)Math.Cos(aa), (float)Math.Sin(aa));
                    DrawSeg(oc + dd * 20f, oc + dd * 58f, 2.5f, MalachitePalette.AccentGold * (0.45f - 0.07f * i) * holdFade);
                }
                float breathe = 0.25f + 0.1f * (float)Math.Sin(_age * 0.6f);
                for (int s = 0; s < 24; s++)
                {
                    float a0 = MathHelper.TwoPi * s / 24f;
                    float a1 = MathHelper.TwoPi * (s + 1) / 24f;
                    DrawSeg(oc + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * 120f,
                            oc + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * 120f, 2f, MalachitePalette.AccentGold * breathe * holdFade);
                }
            }

            // —— 拂柳穿心贴地半月剑气三层弧（视觉重做 2026-09-05）——
            if (Kind == MalachiteMelee.MoveKind.LandingSweep)
            {
                float lf = 1f - _age / 14f;
                float push = _age * 8f * (SlashDir != 0 ? SlashDir : 1);
                float baseX = oc.X + push;
                float baseY = oc.Y + 18f;
                DrawSeg(new Vector2(baseX, baseY), new Vector2(baseX + 70f, baseY), 3.5f, MalachitePalette.AccentGold * (0.55f * lf));
                DrawSeg(new Vector2(baseX, baseY - 4f), new Vector2(baseX + 56f, baseY - 4f), 2.5f, MalachitePalette.GreenBright * (0.6f * lf));
                DrawSeg(new Vector2(baseX, baseY - 8f), new Vector2(baseX + 40f, baseY - 8f), 1.8f, MalachitePalette.White * (0.5f * lf));
            }

            if (_impactT > 0 && (Kind == MalachiteMelee.MoveKind.AirDive || Kind == MalachiteMelee.MoveKind.Finisher2))
            {
                float t = _impactT / 12f;
                float L = 120f * (1f - t) + 40f;
                Color crossC = MalachitePalette.White * Math.Min(0.9f, t * 1.5f); // 光爆强度 1.5×（护眼上限 0.9）
                DrawSeg(oc - Vector2.UnitX * L, oc + Vector2.UnitX * L, 7f, crossC);
                DrawSeg(oc - Vector2.UnitY * L, oc + Vector2.UnitY * L, 7f, crossC);
                for (int s = 0; s < 12; s++)
                {
                    float rr = 40f + (1f - t) * 90f;
                    float a0 = MathHelper.TwoPi * s / 12f;
                    float a1 = MathHelper.TwoPi * (s + 1) / 12f;
                    DrawSeg(oc + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * rr,
                            oc + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * rr, 3f, tint * t);
                }
                // 外环延迟 3 帧扩散（双环 2026-09-05）
                if (_impactT < 9)
                {
                    float t2 = (_impactT + 3f) / 12f;
                    for (int s = 0; s < 12; s++)
                    {
                        float rr2 = 55f + (1f - t2) * 120f;
                        float a0 = MathHelper.TwoPi * (s + 0.5f) / 12f;
                        float a1 = MathHelper.TwoPi * (s + 1.5f) / 12f;
                        DrawSeg(oc + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * rr2,
                                oc + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * rr2, 2.5f, MalachitePalette.AccentGold * t2);
                    }
                }
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>自定义拉丝贴图开关：作者日后自绘替换 Textures/SlashTrail.png 后可置 true（当前该资产实测为纯白块，无纹理细节）。</summary>
        private static readonly bool UseCustomSlashTrail = false;
        private static Texture2D _proceduralTrailTex;

        /// <summary>刃身贴图静态缓存：每帧绘制免去 ModContent 字典查找；设备重置/销毁后自动重取。</summary>
        private static Texture2D _slashTex;
        private static Texture2D SlashTex()
        {
            if (_slashTex != null && !_slashTex.IsDisposed) return _slashTex;
            return _slashTex = ModContent.Request<Texture2D>("可成长的孔雀翎/Textures/MeleeSlash").Value;
        }

        /// <summary>获取刀光拉丝贴图：首次调用烘焙一次（默认程序化 256×64 高频风痕线；UseCustomSlashTrail 置 true 时走本地资产），此后直接返回缓存。</summary>
        private static Texture2D GetSlashTexture()
        {
            if (_proceduralTrailTex != null && !_proceduralTrailTex.IsDisposed)
                return _proceduralTrailTex;
            return BakeTrailTexture();
        }

        /// <summary>烘焙/加载拉丝贴图（仅首次或设备重置时执行一次）。</summary>
        private static Texture2D BakeTrailTexture()
        {
            if (UseCustomSlashTrail && ModContent.HasAsset("可成长的孔雀翎/Textures/SlashTrail"))
            {
                _proceduralTrailTex = ModContent.Request<Texture2D>("可成长的孔雀翎/Textures/SlashTrail").Value;
                if (_proceduralTrailTex != null && !_proceduralTrailTex.IsDisposed)
                    return _proceduralTrailTex;
            }

            // 动态在内存中生成高品质多重风痕拉丝贴图
            int w = 256, h = 64;
            Color[] data = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                // 上下边缘快速羽化
                float edgeFade = (float)Math.Sin(v * MathHelper.Pi);
                edgeFade = (float)Math.Pow(edgeFade, 1.8f);

                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    // 沿 X 轴从左往右渐亮（尾部消散，刃前极亮）
                    float uFade = (float)Math.Pow(u, 1.5f);

                    // 构造两组不同频率的高频横向拉丝条纹
                    float streak1 = (float)Math.Sin(v * 28f + u * 2f) * 0.5f + 0.5f;
                    float streak2 = (float)Math.Cos(v * 55f - u * 4f) * 0.5f + 0.5f;
                    float streak = MathHelper.Lerp(streak1, streak2, 0.4f);

                    // 综合亮度计算
                    float intensity = (0.25f + 0.75f * streak) * edgeFade * uFade;
                    data[y * w + x] = Color.White * Math.Clamp(intensity, 0f, 1f);
                }
            }

            _proceduralTrailTex = new Texture2D(Main.graphics.GraphicsDevice, w, h);
            _proceduralTrailTex.SetData(data);
            return _proceduralTrailTex;
        }

        /// <summary>
        /// 刀光扇形网格：40段平滑历史插值采样 + 月牙梭形厚度塑形 → 纹理映射 → 正确矩阵投影与 Additive 混合
        /// （顶点=world−screen 坐标；View 由 GameViewMatrix.TransformationMatrix 统一接管，不再手动乘 ZoomMatrix）
        /// </summary>
        private void DrawFanStrip(Vector2 oc, Color tint, float holdFade)
        {
            // 顶点构建已提取至 Core\Vfx\SlashVfx.cs（唯一出处：游戏内与 UI 共用）
            // 参数取自 Core\Vfx\SlashTuning.cs（同源）；deep 色用于内缘软融段，否则晶体截面少一层。
            int vertexCount = SlashVfx.BuildFanStrip(_th, _sc, _sx, oc, tint, MalachitePalette.GreenDeep,
                SlashTuning.ActiveBand, holdFade,
                SlashTuning.ArtWidth, Main.screenPosition, _fanSrc,
                SlashTuning.FanSubdivisions, -1f, SlashTuning.BandGain);
            if (vertexCount == 0) return;

            // 中立顶点 → 上传顶点（同布局，逐点转换；480 点，成本可忽略）
            for (int i = 0; i < vertexCount; i++)
            {
                _fanVerts[i] = new VertexPositionColorTexture(
                    new Vector3(_fanSrc[i].Pos, 0f), _fanSrc[i].Color, _fanSrc[i].UV);
            }

            // 挂起当前的 SpriteBatch 批次
            Main.spriteBatch.End();

            // 初始化或配置 BasicEffect
            if (_fanFx == null || _fanFx.IsDisposed) // 显卡设备重置后重建（与 _slashTex 缓存同构）
            {
                _fanFx = new BasicEffect(Main.graphics.GraphicsDevice)
                {
                    VertexColorEnabled = true,
                    TextureEnabled = true, // 启用贴图
                    LightingEnabled = false,
                    FogEnabled = false
                };
            }

            // 正确矩阵赋值：View 统一由 TransformationMatrix 接管
            _fanFx.World = Matrix.Identity;
            _fanFx.View = Main.GameViewMatrix.TransformationMatrix;
            _fanFx.Projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, 0f, 1f);

            // 绑定风痕拉丝贴图（程序化烘焙；作者日后可自绘 SlashTrail.png 替换并置 UseCustomSlashTrail = true）
            _fanFx.TextureEnabled = true; // 每帧复位：修长晶刃实体通道（DrawProceduralBlade）会临时关闭
            _fanFx.Texture = GetSlashTexture();

            // 硬件渲染状态：
            // 2026-09-22 诊断改 — 原先用 BlendState.Additive，而本模组的条带色阶大量落在深翠绿一侧，
            // 加色混合下"变暗"根本无法表达（外缘暗边/内缘软融/冷却下沉全部丢失），且程序化拉丝贴图
            // 的上下边缘羽化到 alpha=0，在加色下羽化区被整体吃掉 —— 这是刀光"几乎看不见"的头号原因。
            // 改 NonPremultiplied：顶点色按「RGB 与 A 独立」语义提交，结构（alpha）与体色（color）分离，
            // 与 SlashVfx 的顶点色写法一致；同时与 v5.5 确立的"特效直接画进引擎批次"精神一致。
            // 模式选择走 SlashTuning.ActiveBlend（单一出处，可一键回退到 Additive 做 A/B 对照）。
            Main.graphics.GraphicsDevice.BlendState = SlashTuning.ActiveBlend == BandBlend.Additive
                ? BlendState.Additive
                : BlendState.NonPremultiplied;
            Main.graphics.GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            Main.graphics.GraphicsDevice.DepthStencilState = DepthStencilState.None;
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            foreach (EffectPass pass in _fanFx.CurrentTechnique.Passes) pass.Apply();

            // 顶点布局：每列 = 色标数 × 2，相邻两列的对应点连成四边形 → 三角数 = (列数-1) × (色标数-1) × 2
            int columns = SlashTuning.FanSubdivisions;
            int stops = vertexCount / (columns * 2);
            Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, _fanVerts, 0,
                (columns - 1) * (stops - 1) * 2);

            // 重新开启 SpriteBatch，直接进入 Additive 混合模式以配合后续发光层绘制
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
