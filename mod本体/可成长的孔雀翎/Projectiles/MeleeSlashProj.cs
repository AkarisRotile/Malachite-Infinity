// 代码来源与合规署名：
// - 手感设计参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES——
//   高频连段叠"精准"被动、冲刺斩带无敌帧；挥砍"加速-急停"的顿挫节奏。
// - 挥动结构参考（学习后自主实现，未直接复制源码）：
//   CalamityEntropy（社区开源）Core\BaseSwing.cs —— 两段式挥舞（加速段伸刃+提速、
//   减速段收刃+急刹、至终点后停驻）、历史轨迹弧光、命中火花沿挥动切线飞散的做法；
//   以及 Content\Items\Donator\TlipocasScythe.cs 的交替挥向（swing 0/1 交替）设计。
//   https://github.com/hocha113/CalamityEntropy
// - 斩击占位贴图：泰拉之刃(Terra Blade) 本体（物品贴图，运行时引用原版资源；
//   用户后续自绘挥砍贴图后替换）。
// 本文件为自主实现。
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 近战形态·斩击（原型 v3）：绕持有者作"两段式变速挥动"的 hitbox（顿挫手感）。
    /// - 角度推进不再匀速：前 ~45%（SwingPhase）加速伸刃，后段急刹收刃，
    ///   终点停驻 SwingHoldTicks 帧再消失（节奏参数全部集中在 MalachiteMelee）；
    /// - 刃长/刃身大小随挥动脉动（参考 BaseSwing 的 Length/scale 脉动，实现为自主公式）；
    /// - 绘制：历史轨迹"弧光"残影扇（加色）+ 当前泰拉刃本体（正常光照 + 加色高亮辉光）；
    /// - 命中：挥动方向的切线火花（参考 BaseSwing 命中火花沿切线飞散，自绘 Dust 实现）。
    /// </summary>
    public class MeleeSlashProj : ModProjectile
    {
        /// <summary>泰拉之刃本体（物品）贴图占位：用户自绘挥砍贴图后改回本模组路径。</summary>
        public override string Texture => "Terraria/Images/Item_" + ItemID.TerraBlade;

        /// <summary>连段段数（0..ComboMaxSteps-1）。</summary>
        public int SlashStep = 0;
        /// <summary>玩家朝向（1=右 / -1=左），决定挥动弧线镜像。</summary>
        public int SlashDir = 1;

        private int _age = 0;
        // 历史轨迹（画"弧光"用）：逐帧记录世界坐标/朝向/尺寸，越旧越淡。
        // 只记活跃帧（SwingTicks 条），停驻帧不动，末尾一条即"当前刃"。
        private readonly List<Vector2> _pos = new List<Vector2>();
        private readonly List<float> _rot = new List<float>();
        private readonly List<float> _scl = new List<float>();

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

        /// <summary>两段式角度曲线：0..p 加速（幂加速），p..1 急刹收刃（同幂镜像，终点导数→0 = 停驻感）。</summary>
        private static float AngleFrac(float u)
        {
            u = Math.Clamp(u, 0f, 1f);
            float p = MalachiteMelee.SwingPhase;
            float k = MalachiteMelee.SwingEasePower;
            if (u <= p)
                return p * (float)Math.Pow(Math.Max(1e-4f, u / p), k);
            return 1f - (1f - p) * (float)Math.Pow((1f - u) / (1f - p), k);
        }

        /// <summary>阶段脉动：0..p 由 lo 到 mid，p..1 由 mid 到 hi（刃长/刃身共用）。</summary>
        private static float PhaseLerp(float u, float lo, float mid, float hi)
        {
            u = Math.Clamp(u, 0f, 1f);
            float p = MalachiteMelee.SwingPhase;
            if (u <= p)
                return MathHelper.Lerp(lo, mid, p <= 0f ? 1f : u / p);
            return MathHelper.Lerp(mid, hi, Math.Clamp((u - p) / Math.Max(1e-4f, 1f - p), 0f, 1f));
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

            int ticks = MalachiteMelee.SwingTicks;
            int total = ticks + MalachiteMelee.SwingHoldTicks;
            if (_age >= total)
            {
                Projectile.Kill();
                return;
            }

            if (_age < ticks)
            {
                // ---- 活跃段：两段式变速推进角度 + 刃长/尺寸脉动 ----
                float u = _age / (float)Math.Max(1, ticks);
                float frac = AngleFrac(u);
                int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
                float theta = MathHelper.Lerp(MalachiteMelee.StepStartRad(step, SlashDir),
                    MalachiteMelee.StepEndRad(step, SlashDir), frac);

                float reachK = PhaseLerp(u, MalachiteMelee.ReachStartK, MalachiteMelee.ReachPeakK, MalachiteMelee.ReachEndK);
                float reach = MalachiteMelee.StepReach(step) * reachK;
                Vector2 dirVec = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
                Projectile.Center = owner.Center + dirVec * reach;
                Projectile.rotation = theta + MalachiteMelee.SlashBladeArtOffset;

                float scaleK = PhaseLerp(u, MalachiteMelee.ScaleStartK, MalachiteMelee.ScalePeakK, MalachiteMelee.ScaleEndK);
                float scl = MalachiteMelee.SlashVisualScale * MalachiteMelee.StepScale(step)
                    * (MalachiteMelee.StepReach(step) / 100f) * scaleK;

                _pos.Add(Projectile.Center);
                _rot.Add(Projectile.rotation);
                _scl.Add(scl);
                if (_pos.Count > MalachiteMelee.SwingTicks + 1)
                {
                    _pos.RemoveAt(0);
                    _rot.RemoveAt(0);
                    _scl.RemoveAt(0);
                }
            }
            // 停驻段（_age >= ticks）：刃保持终点姿势不动，顿挫/收势。
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

            // ---- 打击感火花：沿挥动方向飞散的切线火花（参考 BaseSwing 命中火花做法）----
            Vector2 radial = target.Center - owner.Center;
            if (radial.LengthSquared() < 1f) radial = Vector2.UnitX * (SlashDir != 0 ? SlashDir : 1);
            else radial.Normalize();

            // 瞬时挥向：用最近两帧轨迹差（玩家移动时也自然）
            Vector2 travel = Vector2.Zero;
            if (_pos.Count >= 2) travel = _pos[_pos.Count - 1] - _pos[_pos.Count - 2];
            if (travel.LengthSquared() < 1f)
            {
                // 无轨迹差（停驻段命中）：按挥动方向补一版
                travel = radial.RotatedBy(MathHelper.PiOver2 * (SlashDir < 0 ? -0.5f : 0.5f));
            }
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

        public override bool PreDraw(ref Color lightColor)
        {
            if (_pos.Count == 0) return false;
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = tex.Size() * 0.5f;
            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            Color tint = step switch
            {
                0 => MalachitePalette.GreenBright,
                1 => Color.White,
                _ => MalachitePalette.AccentGold,
            };
            int n = _pos.Count;
            Vector2 curPos = _pos[n - 1] - Main.screenPosition;
            float curRot = _rot[n - 1];
            float curScl = _scl[n - 1];

            // 1) 正常光照下的刃体（泰拉刃本体占位，随世界光照）
            Main.spriteBatch.Draw(tex, curPos, null, Projectile.GetAlpha(lightColor), curRot, origin, curScl, SpriteEffects.None, 0f);

            // 2) 加色"弧光"残影扇：沿历史轨迹逐帧重绘刃影，越旧越淡；
            //    加速段帧距拉开（看到"甩"）、刹车段帧距收拢（看到"停"）= 速度可视化。
            AdditiveLayer.Begin();
            for (int i = 0; i < n - 1; i++)
            {
                float age = i / (float)Math.Max(1, n - 2);
                float a = MalachiteMelee.SlashVisualAlpha * MathHelper.Lerp(0.07f, 0.36f, age);
                Main.spriteBatch.Draw(tex, _pos[i] - Main.screenPosition, null, tint * a, _rot[i], origin, _scl[i], SpriteEffects.None, 0f);
            }

            // 3) 当前刃加色高亮 + 柔和辉光
            Main.spriteBatch.Draw(tex, curPos, null, Color.White * MalachiteMelee.SlashVisualAlpha, curRot, origin, curScl, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, curPos, null, tint * (MalachiteMelee.SlashVisualAlpha * 0.30f), curRot, origin, curScl * 1.22f, SpriteEffects.None, 0f);
            AdditiveLayer.End();
            return false;
        }
    }
}
