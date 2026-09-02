// 代码来源与合规署名：
// - 手感设计参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES——
//   高频连段叠"精准"被动、冲刺斩带无敌帧；挥砍"加速-急停"的顿挫节奏；招式以"状态+按键"组合表达。
// - 挥动结构参考（学习后自主实现，未直接复制源码）：
//   · CalamityEntropy（社区开源）Core\BaseSwing.cs —— 挥动曲线/弧光拖尾/切线火花思路；
//     Content\Items\Donator\TlipocasScythe.cs 交替挥向。 https://github.com/hocha113/CalamityEntropy
//   · CalamityOverhaul（MIT, (c) hocha113）OniSlashRenderer —— 爆发过冲→回坐曲线、带宽/外锐内柔思路。
//   · CalamityModPublic（Azafure, LLC 专有·仅参考）DevilsDevastationHoldout —— 状态机/镜像思路。
// - 挥动特效贴图：用户自绘（MeleeSlash.png，已按内容裁掉透明边）；挥动圆心 = 贴图最左像素。
// - 招式（段击/蓄力重斩/上挑斩/满月终结）为 ES 手感的原创适配，非复刻其指令表。
// 本文件为自主实现。
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 近战形态·斩击（原型 v5.2）：用户自绘挥动特效贴图（圆心=最左像素）绕持有者作爆发挥扫。
    /// - 生命周期：蓄势(隐/不判定) → 挥扫(爆发曲线"过冲→回坐") → 满形定格(+招式的额外定格)；
    /// - 招式种类（Kind）：Step 段击 / Charged 蓄力重斩 / Upper 上挑斩 / Finisher 满月终结——
    ///   每种有自己的弧线/刃长/比例/额外定格（由 MalachiteMelee 在生成时传入）；
    /// - 贴图锚点：裁剪后贴图的 (0, SlashArtPivotY) 即挥动圆心，置于玩家中心随 θ 旋转；
    /// - 命中判定盒：覆盖整条刃长的大盒随挥动扫过；
    /// - 曳光/拖尾/路径弧光/击中反馈（扩散环+砍痕闪刃+白热爆点）见 PreDraw/OnHitNPC。
    /// </summary>
    public class MeleeSlashProj : ModProjectile
    {
        /// <summary>用户自绘挥动特效贴图（已裁透明边）。</summary>
        public override string Texture => "可成长的孔雀翎/Textures/MeleeSlash";

        /// <summary>连段段数（仅 Kind=Step 时使用；0..ComboMaxSteps-1）。</summary>
        public int SlashStep = 0;
        /// <summary>玩家朝向（1=右 / -1=左），决定挥动弧线镜像。</summary>
        public int SlashDir = 1;
        /// <summary>招式种类（生成时由 MalachiteMelee 设置）。</summary>
        public MalachiteMelee.MoveKind Kind = MalachiteMelee.MoveKind.Step;
        /// <summary>自定义弧线起止（基准朝右的弧度，弹幕内按 SlashDir 镜像；仅非 Step 用）。</summary>
        public float CustomStart = 0f;
        public float CustomEnd = 0f;
        /// <summary>刃长/比例倍率（相对 StepReach/StepScale）。</summary>
        public float ReachMult = 1f;
        public float ScaleMult = 1f;
        /// <summary>额外定格帧（如满月终结的收势）。</summary>
        public int ExtraHold = 0;

        private int _age = 0;
        private int _flash = 0; // 满形闪余量（过冲峰值那帧起亮，随后逐帧衰减）
        private int _hitFx = 0; // 击中反馈帧余量（扩散环 + 砍痕闪刃）
        private Vector2 _hitPos = Vector2.Zero;
        private Vector2 _hitDir = Vector2.UnitX;
        private bool _upperHit = false; // 上挑斩只挑飞一次
        // 历史（拖尾用）：挥扫帧的角度与贴图比例、刃尖世界坐标（外缘曳光带）
        private readonly List<float> _th = new List<float>();
        private readonly List<float> _sc = new List<float>();
        private readonly List<float> _sx = new List<float>(); // 段位贴图变形：沿刃长拉伸
        private readonly List<float> _sy = new List<float>(); // 厚度压扁
        private readonly List<Vector2> _tip = new List<Vector2>();

        private static int Gather => MalachiteMelee.SwingGatherFrames;
        private static int SweepEnd => MalachiteMelee.SwingGatherFrames + MalachiteMelee.SwingSweepFrames;
        private int TotalFrames => SweepEnd + MalachiteMelee.SwingHoldFrames + ExtraHold;
        private int HoldStart => SweepEnd;

        public override void SetDefaults()
        {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600; // 生命周期由 _age 控制
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

        /// <summary>爆发曲线：smoothstep 冲过 SwingOvershoot(过冲)，再回坐落定 1（顿挫核心）。</summary>
        private static float BurstCurve(float p)
        {
            p = Math.Clamp(p, 0f, 1f);
            float e = MalachiteMelee.SwingBurstEnd;
            if (p < e)
                return MalachiteMelee.SwingOvershoot * SmoothStep01(p / e);
            return MathHelper.Lerp(MalachiteMelee.SwingOvershoot, 1f, SmoothStep01((p - e) / (1f - e)));
        }

        /// <summary>本招的挥动弧线起止（已按朝向镜像）。</summary>
        private void SwingArc(int step, out float start, out float end)
        {
            if (Kind == MalachiteMelee.MoveKind.Step)
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
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
            {
                Projectile.Kill();
                return;
            }
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            if (_age >= TotalFrames)
            {
                Projectile.Kill();
                return;
            }

            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            SwingArc(step, out float start, out float end);
            float reachBase = MalachiteMelee.StepReach(step) * ReachMult;

            if (_age < Gather)
            {
                // 蓄势段：隐刃于起点、不判定（"拉背"）
                Projectile.friendly = false;
                Projectile.rotation = start;
                PlaceAt(owner, start, reachBase * 0.5f);
            }
            else if (_age < SweepEnd)
            {
                // 挥扫段：爆发曲线推进角度；刃长同步脉动
                Projectile.friendly = true;
                int since = _age - Gather;
                float p = since / (float)Math.Max(1, MalachiteMelee.SwingSweepFrames);
                float c = BurstCurve(p);
                float theta = MathHelper.Lerp(start, end, c);
                float pulse = 1f + MalachiteMelee.SwingReachPulse * c;
                Projectile.rotation = theta;
                PlaceAt(owner, theta, reachBase * pulse);

                // 过冲峰值帧 → 满形闪
                if (p >= MalachiteMelee.SwingBurstEnd && _flash == 0)
                    _flash = 3;

                float scaleFactor = Kind == MalachiteMelee.MoveKind.Step ? MalachiteMelee.StepScale(step) : ScaleMult;
                float artScale = reachBase * pulse * scaleFactor / MalachiteMelee.SlashArtWidth;
                // 段位贴图变形：X=沿刃长拉伸 / Y=厚度压扁；挥扫过冲处变形最大（甩感）
                float sx = Kind == MalachiteMelee.MoveKind.Step ? MalachiteMelee.StepStretchX(step) : 1.05f;
                float sy = Kind == MalachiteMelee.MoveKind.Step ? MalachiteMelee.StepSquashY(step) : 0.90f;
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
                // 满形定格：停在终点（含招式的额外定格）
                Projectile.friendly = true;
                Projectile.rotation = end;
                PlaceAt(owner, end, reachBase);
            }

            if (_flash > 0) _flash--;
            if (_hitFx > 0) _hitFx--;
            _age++;
        }

        /// <summary>命中判定盒：覆盖整条刃长的大盒，中心在刃长的 SwingHitCenterK 处。</summary>
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
                _th.RemoveAt(0);
                _sc.RemoveAt(0);
                _sx.RemoveAt(0);
                _sy.RemoveAt(0);
                _tip.RemoveAt(0);
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

            // 上挑斩：把命中目标向上挑飞（一次挥动一次）
            if (Kind == MalachiteMelee.MoveKind.Upper && !_upperHit)
            {
                _upperHit = true;
                target.velocity.Y -= 9f;
                target.velocity.X *= 0.4f;
                target.netUpdate = true;
            }

            // ---- 打击感火花：沿挥动方向飞散的切线火花 ----
            Vector2 radial = target.Center - owner.Center;
            if (radial.LengthSquared() < 1f) radial = Vector2.UnitX * (SlashDir != 0 ? SlashDir : 1);
            else radial.Normalize();

            Vector2 travel = Vector2.Zero;
            if (_tip.Count >= 2) travel = _tip[_tip.Count - 1] - _tip[_tip.Count - 2];
            if (travel.LengthSquared() < 1f)
                travel = radial.RotatedBy(MathHelper.PiOver2 * (SlashDir < 0 ? -0.5f : 0.5f));
            travel.Normalize();
            Vector2 perp = new Vector2(-travel.Y, travel.X);

            // ---- 击中反馈（一次挥动只取第一次命中做强反馈，防群怪刷屏）----
            if (_hitFx <= 0)
            {
                _hitFx = MalachiteMelee.HitFlashFrames;
                _hitPos = target.Center;
                _hitDir = travel;
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
            Color spark = step >= 2 || Kind == MalachiteMelee.MoveKind.Finisher ? MalachitePalette.AccentGold : MalachitePalette.GreenBright;
            int count = Kind == MalachiteMelee.MoveKind.Finisher ? 12 : (step >= 2 ? 9 : 6);
            for (int i = 0; i < count; i++)
            {
                if (!EffectLimiterSystem.CanSpawnEffect(1, 60)) break;
                float back = Main.rand.NextFloat(2.5f, 6.5f);
                float side = Main.rand.NextFloat(0f, 3.5f) * (Main.rand.NextBool() ? 1f : -1f);
                Vector2 vel = -travel * back + perp * side;
                EffectLimiterSystem.SpawnSpark(
                    target.Center + Main.rand.NextVector2Circular(12f, 12f), vel,
                    Main.rand.NextBool(5) ? Color.White : spark,
                    Main.rand.NextFloat(0.9f, 1.6f), 20);
            }
        }

        /// <summary>画一段加色粗线（1x1 Pixel 拉伸旋转），构成弧光/曳光/环。</summary>
        private static void DrawSeg(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 1f || width < 0.5f) return;
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, mid, null, color, d.ToRotation(),
                new Vector2(0.5f, 0.5f), new Vector2(len, Math.Max(1f, width)), SpriteEffects.None, 0f);
        }

        /// <summary>本招命中反馈环的尺寸倍率（蓄力/满月更大）。</summary>
        private float KindRingK()
        {
            return Kind switch
            {
                MalachiteMelee.MoveKind.Finisher => 1.9f,
                MalachiteMelee.MoveKind.Charged => 1.4f,
                _ => 1f,
            };
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return false;
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || _th.Count == 0) return false;

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = new Vector2(0f, MalachiteMelee.SlashArtPivotY); // 挥动圆心 = 最左像素
            int n = _th.Count;
            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            Color tint = Kind == MalachiteMelee.MoveKind.Finisher ? MalachitePalette.AccentGold
                : Kind == MalachiteMelee.MoveKind.Charged ? MalachitePalette.GreenBright
                : step switch { 0 => MalachitePalette.GreenBright, 1 => Color.White, _ => MalachitePalette.AccentGold };
            Vector2 pivot = owner.Center - Main.screenPosition;

            // 1) 正常层：当前刃体贴图本体（亮度倍率调暗，防过曝）
            Main.spriteBatch.Draw(tex, pivot, null,
                new Color(MalachiteMelee.SlashArtBrightness, MalachiteMelee.SlashArtBrightness, MalachiteMelee.SlashArtBrightness) * MalachiteMelee.SlashArtAlpha,
                _th[n - 1], origin, new Vector2(_sx[n - 1], _sy[n - 1]) * _sc[n - 1], SpriteEffects.None, 0f);

            // 2) 加色层：挥动路径弧光 + 残影 + 曳光带 + 本体辉光 + 满形闪 + 击中反馈
            float reachBase = MalachiteMelee.StepReach(step) * ReachMult;
            float holdFade = _age > HoldStart
                ? MathHelper.Lerp(1f, 0.45f, (_age - HoldStart) / (float)Math.Max(1, MalachiteMelee.SwingHoldFrames + ExtraHold))
                : 1f;
            float edgeW = Math.Max(3f, reachBase * MalachiteMelee.SlashEdgeWidth);
            float bandW = Math.Max(5f, reachBase * MalachiteMelee.SlashBandWidth);
            AdditiveLayer.Begin();

            // 2a 挥动路径弧光（v5.3）：双层扇形带 = 彩色主带 + 嵌套白热芯带
            //     采样走爆发曲线的真实刃迹（与刃身同一条曲线，杜绝拖尾与刃身脱节）
            //     参考：特莉波卡镰刀 oldRots 历史条带(彩色带叠白亮内芯) / 鬼切外缘=刀尖轨迹锐利
            {
                SwingArc(step, out float theta0, out float _unusedEnd);
                _ = _unusedEnd;
                float theta1 = _th[n - 1];
                float R = _sc[n - 1] * MalachiteMelee.SlashArtWidth * _sx[n - 1];
                if (Math.Abs(theta1 - theta0) > 0.001f && R > 2f)
                {
                    int steps = 24;
                    // 当前挥扫进度 → 用同一条 BurstCurve 重采样整条已走路径（拖尾贴合刃迹）
                    float pNow = Math.Clamp((_age - Gather) / (float)Math.Max(1, MalachiteMelee.SwingSweepFrames), 0f, 1f);
                    float sliceW = Math.Abs(theta1 - theta0) / steps;
                    float segW = Math.Max(3f, sliceW * R * 0.95f);
                    float coreW = Math.Max(2f, sliceW * R * 0.55f);
                    Vector2 prevOuter = Vector2.Zero;
                    bool hasPrev = false;
                    for (int k = 0; k <= steps; k++)
                    {
                        float s = pNow * k / (float)steps;
                        float th = MathHelper.Lerp(theta0, theta1, BurstCurve(s));
                        Vector2 dd = new Vector2((float)Math.Cos(th), (float)Math.Sin(th));
                        float tk = k / (float)steps;
                        // 主带：内缘 PathInnerK → 外缘 R；旧段偏 tint、前缘白热
                        float ga = MalachiteMelee.PathGlowAlpha * (0.30f + 0.70f * tk) * holdFade;
                        Color gc = Color.Lerp(tint, Color.White, Math.Clamp(tk * 1.8f, 0f, 1f));
                        DrawSeg(owner.Center + dd * (R * MalachiteMelee.PathInnerK), owner.Center + dd * R,
                            segW, gc * ga);
                        // 白热芯带：嵌套更窄更亮（参考特莉波卡白亮内芯）
                        float wa = MalachiteMelee.PathWhiteAlpha * (0.5f + 0.5f * tk) * holdFade;
                        DrawSeg(owner.Center + dd * (R * MalachiteMelee.PathWhiteInnerK), owner.Center + dd * (R * 0.98f),
                            coreW, Color.White * wa);
                        // 外缘锐线：刀尖轨迹（最亮边）
                        if (hasPrev)
                            DrawSeg(prevOuter, owner.Center + dd * R,
                                Math.Max(2f, reachBase * MalachiteMelee.PathEdgeWidth), gc * Math.Min(1f, ga * 2.4f));
                        prevOuter = owner.Center + dd * R;
                        hasPrev = true;
                    }
                }
            }

            // 2b 贴图残影：越旧越淡偏 tint，越新越白热略放大
            for (int i = 0; i < n - 1; i++)
            {
                float age = i / (float)Math.Max(1, n - 2);
                float a = MalachiteMelee.SlashVisualAlpha * (0.05f + 0.75f * age * age) * holdFade;
                Color gc = Color.Lerp(tint, Color.White, Math.Clamp(age * 1.5f, 0f, 1f));
                Main.spriteBatch.Draw(tex, pivot, null, gc * a, _th[i], origin,
                    new Vector2(_sx[i], _sy[i]) * _sc[i] * MathHelper.Lerp(0.9f, 1.03f, age), SpriteEffects.None, 0f);
            }

            // 2b2 刃尖曳光带：宽软层 + 细亮层（新段近白热）
            for (int i = 0; i < _tip.Count - 1; i++)
            {
                float age = i / (float)Math.Max(1, _tip.Count - 2);
                float a = MalachiteMelee.SlashVisualAlpha * (0.10f + 0.9f * age) * holdFade;
                Color bc = Color.Lerp(tint, Color.White, Math.Clamp(age * 1.3f, 0f, 1f));
                DrawSeg(_tip[i], _tip[i + 1], bandW * 1.6f, bc * (a * 0.22f));
                DrawSeg(_tip[i], _tip[i + 1], edgeW, bc * a);
            }

            // 2c 本体加色辉光 + 前缘白热（领先刃）
            Main.spriteBatch.Draw(tex, pivot, null, Color.White * MalachiteMelee.SlashArtGlowAlpha,
                _th[n - 1], origin, new Vector2(_sx[n - 1], _sy[n - 1]) * _sc[n - 1], SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pivot, null, Color.White * (MalachiteMelee.SlashArtGlowAlpha * 1.6f),
                _th[n - 1], origin, new Vector2(_sx[n - 1], _sy[n - 1]) * _sc[n - 1] * 1.06f, SpriteEffects.None, 0f);

            // 2d 满形闪：落位帧整条曳光高亮一拍
            if (_flash > 0)
            {
                float fa = 0.6f * _flash / 3f;
                for (int i = 0; i < _tip.Count - 1; i++)
                    DrawSeg(_tip[i], _tip[i + 1], bandW * 0.8f, Color.White * fa);
            }

            // 2e 击中反馈：扩散环 + 砍痕闪刃（参考鬼切/镰刀命中反馈；蓄力/满月环更大）
            if (_hitFx > 0)
            {
                float t = _hitFx / (float)MalachiteMelee.HitFlashFrames; // 1 → 0 衰减
                float ringR = MalachiteMelee.HitRingMaxR * KindRingK() * (1f - t) + 5f;
                Color rc = Color.Lerp(Color.White, tint, 0.4f) * (0.9f * t);
                int segs = 14;
                for (int s = 0; s < segs; s++)
                {
                    float a0 = MathHelper.TwoPi * s / segs;
                    float a1 = MathHelper.TwoPi * (s + 1) / segs;
                    DrawSeg(_hitPos + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * ringR,
                            _hitPos + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * ringR,
                            2.5f, rc);
                }
                Vector2 hd = _hitDir;
                DrawSeg(_hitPos - hd * (MalachiteMelee.HitSlashLen * 0.55f),
                        _hitPos + hd * (MalachiteMelee.HitSlashLen * 0.45f),
                        3.5f, Color.White * (0.85f * t));
            }
            AdditiveLayer.End();
            return false;
        }
    }
}