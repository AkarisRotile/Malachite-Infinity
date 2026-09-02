// 代码来源与合规署名：
// 本文件为过渡兼容层（GlobalProjectile）：运行时（软引用）识别并附加规则到
// 灾厄 Malachite 弹幕（Proj/Bolt/Stealth）与本模组浮游剑阵等，用于统一潜伏/免疫/词缀逻辑。
// 参考：CalamityModPublic（Azafure, LLC 专有许可，官方允许作为开发参考）
//   Projectiles/Rogue/MalachiteProj.cs、MalachiteBolt.cs、MalachiteStealth.cs
//   https://github.com/CalamityTeam/CalamityModPublic
// 注：IsNativeBossProj 联动（灾厄 Boss 拳/斩弹幕）已按决策 D5 废弃，待阶段 3 清理。
// 其余（浮游剑阵、月总激光接管等）为本模组/原版逻辑的自主实现。
using System;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    public class MalachiteGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        private float _previousVelY; 
        public int lifeTime = 0;
        
        public int hitCount = 0; 
        
        private bool _pierceApplied = false;
        private int _targetNPCIndex = -1; 
        public bool isRefracted = false; 
        public bool isFromSigil = false; 
        public bool isStealthStrike = false; // 无灾厄模式下的潜伏攻击标记
        private bool _initialized = false; 

        public bool IsNativeBossProj(Projectile projectile)
        {
            int t = projectile.type;
            if (t == 0) return false;
            return (MalachiteCache.Fist1 != 0 && t == MalachiteCache.Fist1) ||
                   (MalachiteCache.Fist2 != 0 && t == MalachiteCache.Fist2) ||
                   (MalachiteCache.Slash1 != 0 && t == MalachiteCache.Slash1) ||
                   (MalachiteCache.Slash2 != 0 && t == MalachiteCache.Slash2);
        }

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            int t = entity.type;
            return IsNativeBossProj(entity) ||
                   t == ModContent.ProjectileType<OrbitingMalachiteProj>() ||
                   t == ProjectileID.MoonlordTurretLaser ||  // <--- 仅接管激光
                   (MalachiteCache.ProjType != 0 && t == MalachiteCache.ProjType) ||
                   (MalachiteCache.BoltType != 0 && t == MalachiteCache.BoltType) ||
                   (MalachiteCache.StealthType != 0 && t == MalachiteCache.StealthType);
        }

        public override bool PreAI(Projectile projectile)
        {
            if (IsNativeBossProj(projectile)) return base.PreAI(projectile);

            _previousVelY = projectile.velocity.Y;

            if (projectile.type == ProjectileID.MoonlordTurretLaser)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    projectile.usesIDStaticNPCImmunity = false;
                    projectile.usesLocalNPCImmunity = true;
                    projectile.localNPCHitCooldown = 6; 
                }
                return base.PreAI(projectile);
            }
            
            if (!_initialized)
            {
                _initialized = true;

                projectile.usesIDStaticNPCImmunity = false;
                projectile.usesLocalNPCImmunity = true;
                projectile.localNPCHitCooldown = 6; 

                if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
                {
                    Player player = Main.player[projectile.owner];
                    if (player.active && MalachiteCache.IsMalachiteItem(player.HeldItem))
                    {
                        var mp = player.GetModPlayer<MalachitePlayer>();
                        bool hasTrack = mp.ActiveSigils.Contains(1);
                        bool hasPierce = mp.ActiveSigils.Contains(2);
                        bool isStealthHit = StealthSystem.IsStealthStrike(projectile);
                        int t = projectile.type;

                        if (mp.ActiveSigils.Contains(0)) {
                            if (hasTrack && t != ModContent.ProjectileType<OrbitingMalachiteProj>() && !isStealthHit) projectile.velocity *= 1.8f;
                            if (hasPierce && t == MalachiteCache.BoltType) {
                                projectile.ignoreWater = true;
                                projectile.damage = (int)(projectile.damage * 1.15f);
                            }
                        }

                        if (mp.ActiveSigils.Contains(13) && hasPierce && t == MalachiteCache.BoltType) {
                            projectile.scale *= 1.5f;
                        }

                        if (hasPierce && (t == MalachiteCache.BoltType || isStealthHit)) {
                            projectile.tileCollide = false;
                        }
                    }
                }
            }

            return base.PreAI(projectile);
        }

        public override void PostAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.MoonlordTurretLaser) return;

            if (IsNativeBossProj(projectile))
            {
                if (isFromSigil)
                {
                    projectile.hostile = false;
                    projectile.friendly = true;
                }
                return; 
            }

            int stage = PeacockModifier.GetMalachiteStage();
            int t = projectile.type;
            bool isMalachiteProj = MalachiteCache.ProjType != 0 && t == MalachiteCache.ProjType;
            bool isMalachiteBolt = MalachiteCache.BoltType != 0 && t == MalachiteCache.BoltType;

            if (stage >= 4 && (isMalachiteProj || isMalachiteBolt)) 
            {
                if (projectile.velocity.Y > _previousVelY && (projectile.velocity.Y - _previousVelY) < 1.0f)
                    projectile.velocity.Y = _previousVelY;
            }

            if (isMalachiteProj || isMalachiteBolt)
            {
                if (projectile.velocity.LengthSquared() > 0.1f)
                    projectile.rotation = projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (++lifeTime >= 240) projectile.Kill();

            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player player = Main.player[projectile.owner];
                if (player.active && !player.dead && MalachiteCache.IsMalachiteItem(player.HeldItem))
                {
                    MalachitePlayer mp = player.GetModPlayer<MalachitePlayer>();
                    bool hasPierce = mp.ActiveSigils.Contains(2);
                    bool hasTrack = mp.ActiveSigils.Contains(1);

                    if (mp.ActiveSigils.Contains(8) && hasPierce && (t == MalachiteCache.StealthType || t == MalachiteCache.BoltType))
                    {
                        if (Main.rand.NextBool(3))
                        {
                            EffectLimiterSystem.SpawnSpark(projectile.Center + Main.rand.NextVector2Circular(10f, 10f), projectile.velocity * 0.1f, MalachitePalette.AccentPurple, Main.rand.NextFloat(1.0f, 1.4f), 15);
                        }
                        if (Main.rand.NextBool(4))
                        {
                            EffectLimiterSystem.SpawnSpark(projectile.Center + Main.rand.NextVector2Circular(10f, 10f), projectile.velocity * 0.1f, MalachitePalette.PrimaryGreen, 1f, 15);
                        }
                    }

                    if (hasPierce && (t == MalachiteCache.StealthType || t == MalachiteCache.BoltType) && !_pierceApplied)
                    {
                        int extraPierce = stage >= 13 ? 5 : (stage >= 8 ? 3 : (stage >= 4 ? 2 : 1));
                        projectile.penetrate += extraPierce;
                        projectile.usesIDStaticNPCImmunity = false;
                        projectile.usesLocalNPCImmunity = true;
                        projectile.localNPCHitCooldown = 15;
                        _pierceApplied = true;
                    }

                    if (hasTrack && t != ModContent.ProjectileType<OrbitingMalachiteProj>() && !StealthSystem.IsStealthStrike(projectile))
                    {
                        UpdateTrackingTarget(projectile);
                    }
                }
            }
        }

        private void UpdateTrackingTarget(Projectile projectile)
        {
            NPC target = null;
            
            if (_targetNPCIndex >= 0 && _targetNPCIndex < Main.maxNPCs)
            {
                NPC cachedNPC = Main.npc[_targetNPCIndex];
                if (cachedNPC.active && cachedNPC.CanBeChasedBy() && projectile.DistanceSQ(cachedNPC.Center) < 1400f * 1400f)
                {
                    target = cachedNPC;
                }
                else
                {
                    _targetNPCIndex = -1; 
                }
            }

            if (target == null && lifeTime % 3 == 0)
            {
                float closestDist = 1200f * 1200f; 
                foreach (NPC npc in Main.ActiveNPCs)
                {
                    if (npc.CanBeChasedBy())
                    {
                        float dist = projectile.DistanceSQ(npc.Center);
                        if (dist < closestDist && Collision.CanHit(projectile.Center, 1, 1, npc.Center, 1, 1))
                        {
                            closestDist = dist;
                            target = npc;
                            _targetNPCIndex = npc.whoAmI; 
                        }
                    }
                }
            }

            if (target != null)
            {
                Vector2 desiredVelocity = (target.Center - projectile.Center).SafeNormalize(Vector2.Zero) * Math.Max(16f, projectile.velocity.Length());
                projectile.velocity = Vector2.Lerp(projectile.velocity, desiredVelocity, 0.035f);
            }
        }

        // 【新增】：在造成伤害前，根据命中次数放大伤害
        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (projectile.type == ProjectileID.MoonlordTurretLaser) return;
            if (IsNativeBossProj(projectile)) return;

            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player player = Main.player[projectile.owner];
                if (player.active && !player.dead && MalachiteCache.IsMalachiteItem(player.HeldItem))
                {
                    MalachitePlayer mp = player.GetModPlayer<MalachitePlayer>();
                    
                    // 当且仅当启用了贯穿节点 (ID=2)
                    if (mp.ActiveSigils.Contains(2))
                    {
                        int t = projectile.type;
                        // 确保只有射线和潜伏攻击享受这个加成
                        if (t == MalachiteCache.StealthType || t == MalachiteCache.BoltType || StealthSystem.IsStealthStrike(projectile))
                        {
                            // 计算增伤：取 hitCount 和 5 的最小值，乘以 0.04 (即每次 4%，最高 20%)
                            float damageBonus = Math.Min(hitCount, 5) * 0.04f;
                            
                            // 如果有增伤，直接乘到最终伤害乘区上
                            if (damageBonus > 0)
                            {
                                modifiers.FinalDamage *= (1f + damageBonus);
                            }
                        }
                    }
                }
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.MoonlordTurretLaser) return;
            
            if (IsNativeBossProj(projectile)) return;

            // 命中次数 +1
            hitCount++;

            if (projectile.owner == Main.myPlayer)
            {
                Player player = Main.player[projectile.owner];
                if (player.active && !player.dead && MalachiteCache.IsMalachiteItem(player.HeldItem))
                {
                    MalachitePlayer mp = player.GetModPlayer<MalachitePlayer>();
                    target.AddBuff(BuffID.Poisoned, 180);

                    if (ProgressSystem.DownedPlaguebringer && MalachiteCache.PlagueBuff != 0)
                        target.AddBuff(MalachiteCache.PlagueBuff, 180);

                    if (mp.ActiveSigils.Contains(6))
                    {
                        if (mp.ActiveSigils.Contains(1) && !StealthSystem.IsStealthStrike(projectile)) {
                            target.AddBuff(BuffID.Frostburn, 180);
                            target.AddBuff(BuffID.Chilled, 180);
                        } else if (mp.ActiveSigils.Contains(2) && projectile.type == MalachiteCache.BoltType) {
                            target.AddBuff(MalachiteCache.GlacialState != 0 ? MalachiteCache.GlacialState : BuffID.Frostburn, 120);
                            target.SimpleStrikeNPC((int)(damageDone * 0.3f), 0, false, 0, DamageClass.Generic, true, player.whoAmI, true);
                            
                            for (int i = 0; i < 8; i++) {
                                EffectLimiterSystem.SpawnSpark(target.Center + Main.rand.NextVector2Circular(target.width/2f, target.height/2f), Main.rand.NextVector2Circular(4f, 4f), MalachitePalette.AccentCyan, 1.2f, 20);
                            }
                        }
                    }
                }
            }
        }

        public override bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
        {
            if (IsNativeBossProj(projectile)) return base.OnTileCollide(projectile, oldVelocity);

            Player player = Main.player[projectile.owner];
            MalachitePlayer mp = player.GetModPlayer<MalachitePlayer>();
            
            int stage = PeacockModifier.GetMalachiteStage();
            int t = projectile.type;

            if (stage >= 4 && (t == MalachiteCache.ProjType || t == MalachiteCache.BoltType || t == ModContent.ProjectileType<OrbitingMalachiteProj>()))
            {
                projectile.Kill();
                return false; 
            }
            return base.OnTileCollide(projectile, oldVelocity);
        }
    }

    public class OrbitingMalachiteProj : ModProjectile
    {
        // 高分辨率像素画羽刃贴图（默认绘制，AI 旋转动画）
        public override string Texture => "可成长的孔雀翎/Textures/OrbitingMalachiteProjTex";

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true; 
            Projectile.penetrate = -1;  
            Projectile.DamageType = ModContent.GetInstance<MalachiteDamageClass>(); 
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;     
            Projectile.alpha = 255; 
            Projectile.usesIDStaticNPCImmunity = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15; 
        }

        // 使用像素画羽刃贴图默认绘制（AI 旋转动画呈现环绕效果）

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            if (Projectile.ai[1] > 0)
            {
                Projectile.ai[1]--;
                Projectile.Center = player.Center; 
                return; 
            }

            if (Projectile.alpha == 255)
            {
                Projectile.alpha = 0; 
                Projectile.velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * 32f; 
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f }, Projectile.Center);

                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                        DustID.TintableDust, Projectile.velocity * Main.rand.NextFloat(0.1f, 0.3f) + Main.rand.NextVector2Circular(2.5f, 2.5f),
                        0, Main.rand.NextBool(2) ? MalachitePalette.PrimaryGreen : MalachitePalette.GreenDark, Main.rand.NextFloat(0.8f, 1.4f));
                    d.noGravity = true;
                    d.fadeIn = 0.4f;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.24f, 0.86f, 0.52f); // 主色 #3DDC84 归一化

            int stage = PeacockModifier.GetMalachiteStage();
            int maxCharge = stage >= 13 ? 15 : 25; 

            if (Projectile.ai[0] < maxCharge) 
            {
                Projectile.ai[0]++;
                Projectile.velocity *= 0.86f; 
                Projectile.rotation += 0.75f;  
                
                float progress = Projectile.ai[0] / (float)maxCharge;
                
                if (Main.rand.NextFloat() < progress && EffectLimiterSystem.CanSpawnEffect(2, 90)) 
                {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                        DustID.TintableDust, Main.rand.NextVector2Circular(0.5f, 0.5f),
                        0, Main.rand.NextBool(3) ? MalachitePalette.PrimaryGreen : MalachitePalette.GreenDark, Main.rand.NextFloat(0.6f, 1.2f));
                    d.noGravity = true;
                    d.fadeIn = 0.3f;
                }
            }
            else
            {
                if (Projectile.owner == Main.myPlayer)
                {
                    try 
                    {
                        Vector2 aimDir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitX);
                        int projType = MalachiteCache.NativeProjType; // 浮游剑阵发射本模组普攻弹幕
                        
                        int pIndex = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, aimDir * 45f, projType, Projectile.damage, Projectile.knockBack, Projectile.owner);
                        
                        if (pIndex >= 0 && pIndex < Main.maxProjectiles)
                        {
                            if (StealthSystem.IsStealthStrike(Projectile))
                                StealthSystem.MarkStealthStrike(Main.projectile[pIndex]);
                        }
                    }
                    catch (Exception) { } 
                }
                
                SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
                
                if (EffectLimiterSystem.CanSpawnEffect(3, 90))
                {
                    for (int i = 0; i < 8; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.TintableDust,
                            Main.rand.NextVector2Circular(5f, 5f), 0,
                            MalachitePalette.GreenBright, Main.rand.NextFloat(0.8f, 1.5f));
                        d.noGravity = true;
                        d.fadeIn = 0.4f;
                    }
                }
                    
                Projectile.Kill(); 
            }
        }
    }
}