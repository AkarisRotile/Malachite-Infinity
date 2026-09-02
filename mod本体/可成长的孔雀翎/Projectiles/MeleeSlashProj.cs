// 代码来源与合规署名：
// - 手感设计参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES——
//   高频连段叠"精准"被动、冲刺斩带无敌帧；挥砍"加速-急停"的顿挫节奏。
// - 挥动结构参考（学习后自主实现，未直接复制源码）：
//   · CalamityEntropy（社区开源）Core\BaseSwing.cs —— 历史角采样画加色"弧光环带"、
//     命中火花沿挥动切线飞散；Content\Items\Donator\TlipocasScythe.cs 的交替挥向。
//     https://github.com/hocha113/CalamityEntropy
//   · CalamityOverhaul（MIT）Content\LegendWeapon\OnikiriLegend\OniSlashs\OniSlashRenderer.cs ——
//     "爆发过冲→回坐"的 BurstCurve（smoothstep 冲过 1.05 再落定 1，替代"减速拖尾"=没力根源）、
//     弧光=外缘锐利(刀尖轨迹)/内缘软融、带宽≈半径 40% 的量级。
//     https://github.com/hocha113/CalamityOverhaul
//   · CalamityModPublic（Azafure, LLC 专有许可·仅参考）Projectiles\Melee\DevilsDevastationHoldout.cs ——
//     RotationOffset 缓动 lerp 摆动 + Owner.direction 乘算镜像 + 收势/满形闪的状态机思路。
//     https://github.com/CalamityTeam/CalamityModPublic
// - 本版本已弃用泰拉之刃贴图刃体：挥动完全用自绘加色弧光带（1x1 Pixel 拉伸段）表现。
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
    /// 近战形态·斩击（原型 v4·纯特效弧光）：无刃体贴图，挥动 = 加色弧光带。
    /// - 生命周期：蓄势(隐/不判定) → 挥扫(爆发曲线"过冲→回坐") → 满形定格；
    /// - 弧光带：沿刀尖轨迹的逐帧采样，画"外缘锐利亮线 + 内缘软融宽带"（加色）；
    /// - 命中：沿挥动切线飞散的火花（Dust，走 EffectLimiterSystem）；
    /// - 朝向：三段弧线定义为朝右（dir=1），dir=-1 用 π-角 做水平镜像（弧光带对称，无需翻贴图）。
    /// </summary>
    public class MeleeSlashProj : ModProjectile
    {
        /// <summary>占位贴图：仅满足 TML 加载，实际不绘制刃体（挥动全走加色弧光带）。</summary>
        public override string Texture => "可成长的孔雀翎/Textures/Pixel";

        /// <summary>连段段数（0..ComboMaxSteps-1）。</summary>
        public int SlashStep = 0;
        /// <summary>玩家朝向（1=右 / -1=左），决定挥动弧线镜像。</summary>
        public int SlashDir = 1;

        private int _age = 0;
        private int _flash = 0; // 满形闪余量（过冲峰值那帧起亮，随后逐帧衰减）
        private readonly List<Vector2> _tip = new List<Vector2>(); // 刀尖轨迹采样（画弧光带）

        private static int Gather => MalachiteMelee.SwingGatherFrames;
        private static int SweepEnd => MalachiteMelee.SwingGatherFrames + MalachiteMelee.SwingSweepFrames;
        private static int Total => SweepEnd + MalachiteMelee.SwingHoldFrames;

        public override void SetDefaults()
        {
            Projectile.width = 190;
            Projectile.height = 130;
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
                // 蓄势段：刃隐于起点、不判定（可视为"拉背"）
                Projectile.friendly = false;
                float theta = start;
                Projectile.Center = owner.Center + new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * reachBase * 0.9f;
                Projectile.rotation = theta;
            }
            else if (_age < SweepEnd)
            {
                // 挥扫段：爆发曲线推进角度；过冲处刃长同步脉冲
                Projectile.friendly = true;
                int since = _age - Gather;
                float p = since / (float)Math.Max(1, MalachiteMelee.SwingSweepFrames);
                float c = BurstCurve(p);
                float theta = MathHelper.Lerp(start, end, c);
                float reach = reachBase * (1f + MalachiteMelee.SwingReachPulse * c);
                Projectile.Center = owner.Center + new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * reach;
                Projectile.rotation = theta;

                // 过冲峰值帧 → 满形闪
                if (p >= MalachiteMelee.SwingBurstEnd && _flash == 0)
                    _flash = 3;

                _tip.Add(Projectile.Center);
                if (_tip.Count > MalachiteMelee.SlashTrailMax)
                    _tip.RemoveAt(0);
            }
            else
            {
                // 满形定格：停在终点（弧光带冻结显示一拍）
                Projectile.friendly = true;
                Projectile.Center = owner.Center + new Vector2((float)Math.Cos(end), (float)Math.Sin(end)) * reachBase;
                Projectile.rotation = end;
            }

            if (_flash > 0) _flash--;
            _age++;
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

        /// <summary>画一段加色粗线（1x1 Pixel 拉伸旋转），构成弧光带的单元段。</summary>
        private void DrawSeg(Vector2 a, Vector2 b, float width, Color color)
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
            if (_tip.Count < 2) return false; // 蓄势期无带
            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            Color tint = step switch
            {
                0 => MalachitePalette.GreenBright,
                1 => Color.White,
                _ => MalachitePalette.AccentGold,
            };
            float reachBase = MalachiteMelee.StepReach(step);
            float scale = MalachiteMelee.StepScale(step);
            float bandW = reachBase * MalachiteMelee.SlashBandWidth * scale;
            float edgeW = reachBase * MalachiteMelee.SlashEdgeWidth * scale;
            int n = _tip.Count;

            // 满形定格后半程轻微沉降（收势）
            float holdFade = _age > SweepEnd
                ? MathHelper.Lerp(1f, 0.55f, (_age - SweepEnd) / (float)Math.Max(1, MalachiteMelee.SwingHoldFrames))
                : 1f;

            AdditiveLayer.Begin();

            // 内缘软融：宽、淡
            for (int i = 0; i < n - 1; i++)
            {
                float ageF = i / (float)Math.Max(1, n - 2);
                float a = MalachiteMelee.SlashVisualAlpha * 0.22f * MathHelper.Lerp(0.2f, 1f, ageF) * holdFade;
                DrawSeg(_tip[i], _tip[i + 1], bandW, tint * a);
            }

            // 外缘锐利：细、亮（末端=当前刀尖最亮）
            for (int i = 0; i < n - 1; i++)
            {
                float ageF = i / (float)Math.Max(1, n - 2);
                float a = MalachiteMelee.SlashVisualAlpha * MathHelper.Lerp(0.25f, 1f, ageF) * holdFade;
                Color c = tint * a;
                if (_flash > 0 && i == n - 2)
                    c = Color.White * Math.Min(1f, a + 0.5f * _flash);
                DrawSeg(_tip[i], _tip[i + 1], edgeW, c);
            }

            // 满形闪：落位帧全带高亮一拍（打击感）
            if (_flash > 0)
            {
                float fa = 0.5f * _flash / 3f * holdFade;
                for (int i = 0; i < n - 1; i++)
                    DrawSeg(_tip[i], _tip[i + 1], bandW * 0.5f, Color.White * fa);
            }

            AdditiveLayer.End();
            return false;
        }
    }
}
