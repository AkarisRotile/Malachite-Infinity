// 代码来源与合规署名：
// - 手感设计参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES——
//   高频连段叠"精准"被动、冲刺斩带无敌帧；挥砍"加速-急停"的顿挫节奏。
// - 挥动结构参考（学习后自主实现，未直接复制源码）：
//   · CalamityEntropy（社区开源）Core\BaseSwing.cs —— 挥动曲线/弧光拖尾/切线火花思路；
//     Content\Items\Donator\TlipocasScythe.cs 交替挥向。 https://github.com/hocha113/CalamityEntropy
//   · CalamityOverhaul（MIT, (c) hocha113）OniSlashRenderer —— 爆发过冲→回坐曲线、带宽/外锐内柔思路。
//   · CalamityModPublic（Azafure, LLC 专有·仅参考）DevilsDevastationHoldout —— 状态机/镜像思路。
// - 挥动特效贴图：用户自绘（MeleeSlash.png，已按内容裁掉透明边）；挥动圆心 = 贴图最左像素。
// 本文件为自主实现。
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 近战形态·斩击（原型 v5）：用户自绘挥动特效贴图（圆心=最左像素）绕持有者作爆发挥扫。
    /// - 生命周期：蓄势(隐/不判定) → 挥扫(爆发曲线"过冲→回坐") → 满形定格；
    /// - 贴图锚点：裁剪后贴图的 (0, SlashArtPivotY) 即挥动圆心，置于玩家中心随 θ 旋转；
    ///   贴图向右展开 = 刃长 = StepReach（2026-09-03 范围≈2倍）；
    /// - 命中判定盒：覆盖整条刃长大盒随挥动扫过；
    /// - 曳光/拖尾：贴图残影（沿历史角度）+ 外缘曳光细带（沿刃尖轨迹）+ 命中切线火花。
    /// </summary>
    public class MeleeSlashProj : ModProjectile
    {
        /// <summary>用户自绘挥动特效贴图（已裁透明边）。</summary>
        public override string Texture => "可成长的孔雀翎/Textures/MeleeSlash";

        /// <summary>连段段数（0..ComboMaxSteps-1）。</summary>
        public int SlashStep = 0;
        /// <summary>玩家朝向（1=右 / -1=左），决定挥动弧线镜像。</summary>
        public int SlashDir = 1;

        private int _age = 0;
        private int _flash = 0; // 满形闪余量（过冲峰值那帧起亮，随后逐帧衰减）
        // 历史（拖尾用）：挥扫帧的角度与贴图比例、刃尖世界坐标（外缘曳光带）
        private readonly List<float> _th = new List<float>();
        private readonly List<float> _sc = new List<float>();
        private readonly List<Vector2> _tip = new List<Vector2>();

        private static int Gather => MalachiteMelee.SwingGatherFrames;
        private static int SweepEnd => MalachiteMelee.SwingGatherFrames + MalachiteMelee.SwingSweepFrames;
        private static int Total => SweepEnd + MalachiteMelee.SwingHoldFrames;

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

            if (_age >= Total)
            {
                Projectile.Kill();
                return;
            }

            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            float start = MalachiteMelee.StepStartRad(step, SlashDir);
            float end = MalachiteMelee.StepEndRad(step, SlashDir);
            float reachBase = MalachiteMelee.StepReach(step);

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

                float artScale = reachBase * pulse * MalachiteMelee.StepScale(step) / MalachiteMelee.SlashArtWidth;
                _th.Add(theta);
                _sc.Add(artScale);
                _tip.Add(owner.Center + new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * (artScale * MalachiteMelee.SlashArtWidth));
                TrimHistory();
            }
            else
            {
                // 满形定格：停在终点
                Projectile.friendly = true;
                Projectile.rotation = end;
                PlaceAt(owner, end, reachBase);
            }

            if (_flash > 0) _flash--;
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

            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            Color spark = step >= 2 ? MalachitePalette.AccentGold : MalachitePalette.GreenBright;
            int count = step >= 2 ? 9 : 6;
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

        /// <summary>画一段加色粗线（1x1 Pixel 拉伸旋转），构成外缘曳光带。</summary>
        private static void DrawSeg(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 1f || width < 0.5f) return;
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, mid, null, color, d.ToRotation(),
                new Vector2(0.5f, 0.5f), new Vector2(len, Math.Max(1f, width)), SpriteEffects.None, 0f);
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
            Color tint = step switch
            {
                0 => MalachitePalette.GreenBright,
                1 => Color.White,
                _ => MalachitePalette.AccentGold,
            };
            Vector2 pivot = owner.Center - Main.screenPosition;

            // 1) 正常层：当前刃体贴图本体（随世界光照亮度但不做调色，特效贴图自带颜色）
            Main.spriteBatch.Draw(tex, pivot, null, Color.White * MalachiteMelee.SlashArtAlpha,
                _th[n - 1], origin, _sc[n - 1], SpriteEffects.None, 0f);

            // 2) 加色层：残影（拖尾）+ 外缘曳光带 + 本体辉光 + 满形闪
            float reachBase = MalachiteMelee.StepReach(step);
            float edgeW = Math.Max(3f, reachBase * MalachiteMelee.SlashEdgeWidth);
            float bandW = Math.Max(5f, reachBase * MalachiteMelee.SlashBandWidth);
            AdditiveLayer.Begin();

            // 2a 贴图残影：沿历史角度逐帧重绘刃影，越旧越淡（加速段帧距拉开/刹车收拢 = 速度可视化）
            for (int i = 0; i < n - 1; i++)
            {
                float age = i / (float)Math.Max(1, n - 2);
                float a = MalachiteMelee.SlashVisualAlpha * MathHelper.Lerp(0.08f, 0.5f, age);
                Main.spriteBatch.Draw(tex, pivot, null, Color.White * a, _th[i], origin, _sc[i], SpriteEffects.None, 0f);
            }

            // 2b 外缘曳光：内软外锐细带（沿刃尖轨迹）
            for (int i = 0; i < _tip.Count - 1; i++)
            {
                float age = i / (float)Math.Max(1, _tip.Count - 2);
                float a = MalachiteMelee.SlashVisualAlpha * MathHelper.Lerp(0.15f, 0.85f, age);
                DrawSeg(_tip[i], _tip[i + 1], bandW, tint * (a * 0.35f));
                DrawSeg(_tip[i], _tip[i + 1], edgeW, tint * a);
            }

            // 2c 本体加色辉光
            Main.spriteBatch.Draw(tex, pivot, null, Color.White * MalachiteMelee.SlashArtGlowAlpha,
                _th[n - 1], origin, _sc[n - 1], SpriteEffects.None, 0f);

            // 2d 满形闪：落位帧整条曳光高亮一拍
            if (_flash > 0)
            {
                float fa = 0.6f * _flash / 3f;
                for (int i = 0; i < _tip.Count - 1; i++)
                    DrawSeg(_tip[i], _tip[i + 1], bandW * 0.8f, Color.White * fa);
            }
            AdditiveLayer.End();
            return false;
        }
    }
}