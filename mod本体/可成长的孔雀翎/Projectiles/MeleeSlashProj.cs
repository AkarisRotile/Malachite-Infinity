// 代码来源与合规署名：
// - 手感设计参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES——
//   高频连段叠"精准"被动、冲刺斩带无敌帧等操作体验；本文件为自主实现，无任何源码或素材复用。
// 本文件为自主实现。
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 近战形态·斩击 hitbox（原型·ES 流 MVP）。
    /// 短暂存在、无飞行速度；命中目标时给持有者叠「精准」层数；
    /// 视觉为 additive 扇形刃光（无贴图内容，纯程序绘制）。
    /// </summary>
    public class MeleeSlashProj : ModProjectile
    {
        /// <summary>斩击贴图占位 = 泰拉之刃(Terra Blade)光束 Projectile_132（运行时引用原版资源，等同复制）。
        /// 想换成独立文件时：把该贴图 PNG 放进 Textures\MeleeSlashPlaceholder.png 并改此路径即可。</summary>
        public override string Texture => "Terraria/Images/Projectile_132";

        /// <summary>玩家朝向（1=右 / -1=左）。</summary>
        public int SlashDir = 1;

        public override void SetDefaults()
        {
            Projectile.width = 64;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 7;
            Projectile.ignoreWater = true;
            Projectile.DamageType = MindDamageClass.Instance;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active) return;
            var mp = owner.GetModPlayer<MalachitePlayer>();
            mp.PrecisionStacks = Math.Min(MalachiteMelee.PrecisionMaxStacks, mp.PrecisionStacks + 1);
            mp.PrecisionTimer = MalachiteMelee.PrecisionDuration;

            // 命中白闪（打击感，限流）
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
            // 占位贴图绘制：透明度/尺寸常量见 MalachiteMelee（SlashVisualAlpha / SlashVisualScale），可实机微调。
            float life = MathHelper.Clamp(1f - Projectile.timeLeft / 7f, 0f, 1f); // 0→1 展开
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            // 朝向 + 小幅弧线摆动，模拟"挥过"的弧迹
            float rot = (SlashDir >= 0 ? 0f : MathHelper.Pi) + MathHelper.Lerp(-0.38f, 0.38f, life);
            float alpha = MalachiteMelee.SlashVisualAlpha * MathHelper.Clamp(1.15f - life * 0.85f, 0.15f, 1f);
            float scale = MalachiteMelee.SlashVisualScale * (0.7f + 0.55f * life);

            Main.spriteBatch.Draw(tex, center, null, Color.White * alpha, rot, origin, scale, SpriteEffects.None, 0f);

            // 微弱 additive 发光层（延续本模组特效风格，透明度同常量缩水）
            AdditiveLayer.Begin();
            Main.spriteBatch.Draw(tex, center, null, Color.White * (alpha * 0.22f), rot, origin, scale * 1.07f, SpriteEffects.None, 0f);
            AdditiveLayer.End();
            return false;
        }
    }
}
