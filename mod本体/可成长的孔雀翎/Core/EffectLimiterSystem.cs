using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    public class EffectLimiterSystem : ModSystem
    {
        public static int EffectScore = 0;

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu) return; 
            
            int recoveryRate = 6; 
            var config = ModContent.GetInstance<MalachiteConfig>();
            if (config != null)
            {
                if (config.ParticleLevel == ParticleEffectLevel.Low) recoveryRate = 3; 
                else if (config.ParticleLevel == ParticleEffectLevel.High) recoveryRate = 25; 
            }
            
            if (EffectScore > 0)
                EffectScore = Math.Max(0, EffectScore - recoveryRate);
        }

        public static bool CanSpawnEffect(int cost = 1, int maxLimit = 200)
        {
            if (Main.dedServ) return false;

            var config = ModContent.GetInstance<MalachiteConfig>();
            if (config != null)
            {
                if (config.ParticleLevel == ParticleEffectLevel.High) return true;
                if (config.ParticleLevel == ParticleEffectLevel.Low) maxLimit /= 3; 
            }
            
            if (EffectScore >= maxLimit) return false; 
            
            EffectScore += cost;
            return true;
        }

        public static void SpawnSpark(Vector2 position, Vector2 velocity, Color color, float scale = 1f, int lifetime = 20)
        {
            if (!CanSpawnEffect(1)) return;

            // 自研 Dust 发光点（替代灾厄 SparkParticle）：无重力、短寿命、可缩放
            int dust = Dust.NewDust(position - new Vector2(2f, 2f), 4, 4, DustID.TintableDust, velocity.X, velocity.Y, 0, color, scale);
            if (dust >= 0 && dust < Main.maxDust)
            {
                Dust d = Main.dust[dust];
                d.noGravity = true;
                d.noLightEmittence = false;
                d.scale = Math.Clamp(scale, 0.4f, 4f);
                d.fadeIn = 0.8f;
                if (lifetime > 0)
                    d.active = true;
            }
        }
    }
}