// 代码来源与合规署名：
// - 手感设计参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES——
//   高频连段叠"精准"被动、冲刺斩带无敌帧等操作体验。
// - 挥动动画思路参考（学习后自主实现，未直接复制源码）：
//   CalamityEntropy（社区开源）Core\BaseSwing.cs —— 手持弹幕绕玩家作"加速-减速"弧线挥舞、
//   长度/缩放脉动与历史位置拖影的做法；以及 Content\Items\Donator\TlipocasScythe.cs 的
//   交替挥向（swing 0/1 交替）设计。
//   https://github.com/hocha113/CalamityEntropy
// - 斩击贴图占位：泰拉之刃(Terra Blade)光束 Projectile_132（运行时引用原版资源）。
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
    /// 近战形态·斩击（原型 v2）：绕持有者作"弧线变速挥动"的 hitbox。
    /// - 每段（SlashStep 0..2）有独立的挥动弧线/半径/大小（数据见 MalachiteMelee.Step*）；
    /// - 位置每帧沿弧线推进 → 引擎逐帧碰撞即构成"扫过"判定；
    /// - 绘制：历史位置残影拖尾 + 当前泰拉刃光束贴图（透明度/尺寸常量可调）。
    /// </summary>
    public class MeleeSlashProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_132";

        /// <summary>连段段数（0..ComboMaxSteps-1）。</summary>
        public int SlashStep = 0;
        /// <summary>玩家朝向（1=右 / -1=左），决定挥动弧线镜像。</summary>
        public int SlashDir = 1;

        private int _age = 0;
        private readonly List<Vector2> _ghostPos = new List<Vector2>();
        private readonly List<float> _ghostRot = new List<float>();

        public override void SetDefaults()
        {
            Projectile.width = 190;
            Projectile.height = 130;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600; // 由 _age 控制生命周期
            Projectile.ignoreWater = true;
            Projectile.DamageType = MindDamageClass.Instance;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2;
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
            if (_age >= ticks)
            {
                Projectile.Kill();
                return;
            }

            // 记录上一帧位置做残影
            if (_ghostPos.Count == 0 || _ghostPos[_ghostPos.Count - 1] != Projectile.Center)
            {
                _ghostPos.Add(Projectile.Center);
                _ghostRot.Add(Projectile.rotation);
                if (_ghostPos.Count > 12) { _ghostPos.RemoveAt(0); _ghostRot.RemoveAt(0); }
            }

            // 弧线进度 + 加速-减速缓动（参考 BaseSwing 的变速手感）
            float t = _age / (float)ticks;
            float eased = t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;

            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            float start = MalachiteMelee.StepStartRad(step, SlashDir);
            float end = MalachiteMelee.StepEndRad(step, SlashDir);
            float theta = MathHelper.Lerp(start, end, eased);

            float reach = MathHelper.Lerp(MalachiteMelee.StepReach(step) * 0.82f, MalachiteMelee.StepReach(step), eased);

            Vector2 dir = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
            Vector2 pos = owner.Center + dir * reach - new Vector2(Projectile.width / 2f, Projectile.height / 2f);
            Projectile.position = pos;
            Projectile.rotation = theta + MathHelper.PiOver2; // 刃身沿切向
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

            if (EffectLimiterSystem.CanSpawnEffect(2, 60))
            {
                for (int i = 0; i < 3; i++)
                {
                    Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(14f, 14f),
                        DustID.TintableDust, Main.rand.NextVector2Circular(4f, 4f), 0,
                        Main.rand.NextBool(2) ? MalachitePalette.White : MalachitePalette.PrimaryGreen,
                        Main.rand.NextFloat(0.6f, 1.0f));
                    d.noGravity = true;
                    d.fadeIn = 0.4f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            float baseScale = MalachiteMelee.SlashVisualScale * MalachiteMelee.StepScale(step) * (MalachiteMelee.StepReach(step) / 100f);

            // 残影拖尾（沿旧位置的刃光，逐层变淡）
            AdditiveLayer.Begin();
            for (int i = 0; i < _ghostPos.Count; i++)
            {
                float k = i / (float)Math.Max(1, _ghostPos.Count - 1);
                float a = 0.10f + 0.22f * k;
                Main.spriteBatch.Draw(tex, _ghostPos[i] - Main.screenPosition, null, Color.White * (a * MalachiteMelee.SlashVisualAlpha),
                    _ghostRot[i], origin, baseScale * (0.55f + 0.3f * k), SpriteEffects.None, 0f);
            }
            AdditiveLayer.End();

            // 当前刃身
            float alpha = MalachiteMelee.SlashVisualAlpha;
            Main.spriteBatch.Draw(tex, screenPos, null, Color.White * alpha, Projectile.rotation, origin, baseScale, SpriteEffects.None, 0f);

            // 微弱核心发光
            AdditiveLayer.Begin();
            Main.spriteBatch.Draw(tex, screenPos, null, Color.White * (alpha * 0.25f), Projectile.rotation, origin, baseScale * 1.12f, SpriteEffects.None, 0f);
            AdditiveLayer.End();
            return false;
        }
    }
}
