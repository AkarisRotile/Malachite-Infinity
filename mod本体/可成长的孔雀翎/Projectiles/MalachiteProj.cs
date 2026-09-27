// 代码来源与合规署名：
// - 残影轨迹（afterimages）与拖尾 Dust 思路参考自：
//   CalamityModPublic（Azafure, LLC 专有许可，官方允许作为开发参考）—— Projectiles/Rogue/MalachiteBolt.cs、MalachiteProj.cs
//   https://github.com/CalamityTeam/CalamityModPublic
// - 三层光带配色（Core/Glow/Aura）思路参考自：
//   CalamityOverhaul（MIT License, Copyright (c) hocha113）—— Content/LegendWeapon/SHPCLegend/Cyberspaces/CyberPrismLaserProj.cs
//   https://github.com/hocha113/CalamityOverhaul
// 本文件为自主实现，未直接复制上述仓库源码。
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 孔雀柳刃·普攻飞刀弹幕（高分辨率像素画贴图 + Dust 光尘）。
    /// 外观：翠绿羽刃像素画（刀尖朝右），飞行时拖出细碎光尘；潜伏态金色光尘。
    /// 逻辑：直线飞行（阶段4起无重力）、命中附加中毒。
    /// </summary>
    public class MalachiteProj : ModProjectile
    {
        public override string Texture => "可成长的孔雀翎/Textures/MalachiteProjTex";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;    // 残影（灾厄范式）
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.extraUpdates = 3;                     // 更快的飞行手感
            Projectile.friendly = true;
            Projectile.DamageType = MindDamageClass.Instance;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.usesIDStaticNPCImmunity = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
            Projectile.ignoreWater = false;
        }

        public override void AI()
        {
            Projectile.localAI[0]++;   // 存活帧数（AI 每帧调用；配合 extraUpdates 会更快累积）

            // 阶段 >= 4：解除弹道下坠（阶段4前手动模拟重力）
            if (ProgressSystem.GetStage() < 4)
                Projectile.velocity.Y += 0.15f;

            ApplyHoming();

            // 旋转跟随飞行方向（贴图竖直刀尖朝上，+90° 使刃身顺飞行方向，灾厄范式）
            if (Projectile.velocity.LengthSquared() > 0.1f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Dust 光尘拖尾（引擎原生粒子，限流）
            Color dustColor = MalachitePalette.PrimaryGreen;
            if (Main.rand.NextBool(2) && EffectLimiterSystem.CanSpawnEffect(1, 90))
            {
                int di = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    DustID.TintableDust,
                    -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    0, dustColor, Main.rand.NextFloat(0.6f, 1.1f)).dustIndex;
                Main.dust[di].noGravity = true;
                Main.dust[di].fadeIn = 0.4f;
            }

            Lighting.AddLight(Projectile.Center, 0.24f, 0.86f, 0.52f); // 主色 #3DDC84 归一化
        }

        // ====================================================================
        // 「追翎」：普攻翎羽飞刀的跟踪（由星宿「刃回响 BladeEcho」门控）
        // ====================================================================
        //
        // 2026-09-27 用户要求："让翎羽攻击（就是普攻）获得跟踪效果"，并特意划清界限：
        //   **翎羽 = 普攻飞刀（本类）**，与 **射线（MalachiteBolt，由灼翎纹章发射）** 是两回事。
        //   因此本文件加跟踪，MalachiteBolt 刻意**不加** —— 射线保持"瞬时直线"的性格。
        //
        // 手感约束：只在飞行一小段后开始转向（迟滞），且每帧转向有上限（限速率），
        // 否则飞刀会变成"贴脸导弹"，连"打偏"这个基本的弹道反馈都消失。

        /// <summary>开始转向前的迟滞帧数（太早转会导致刚出膛就拐弯，很难看）。</summary>
        private const int HomingDelayTicks = 2;

        /// <summary>
        /// 索敌半径（px）。
        /// <para/>2026-09-27 用户反馈"索敌效果不够强" → 由 520 提到 <b>1100</b>（约一个半屏幕），
        /// 视野内的敌人基本都能锁上。
        /// <para/>★ 再对齐旧版《巡猎》节点（`MalachiteProjectiles.cs:197` 的 <c>closestDist = 1200²</c>）→ 提到
        /// <b>1200</b>，与旧版一致。
        /// </summary>
        private const float HomingRange = 1200f;

        /// <summary>
        /// 已锁定目标的**保持**半径（px），必须 &gt; <see cref="HomingRange"/>。
        /// <para/>★ 这是旧版有、我先前漏掉的关键设计（旧版：索敌 1200 / 保持 1400）。
        /// 迟滞（hysteresis）的作用：目标在边界附近晃动时不会被反复"锁上→丢掉→再锁上"，
        /// 否则飞刀会在两个候选目标之间来回抖，手感是"电风扇"。没有迟滞时，
        /// 敌人只要跨出索敌圈 1px 就立刻脱锁，追踪等于白做。
        /// </summary>
        private const float HomingRetainRange = 1400f;

        /// <summary>
        /// 重新全量扫描的间隔（AI 步数）。0 = 每步都扫。
        /// <para/>★ 对齐旧版（`lifeTime % 3 == 0`）。本项目 <c>extraUpdates = 3</c>，
        /// AI 每 tick 跑 4 次，即每 tick 要全扫 4 遍 <c>Main.maxNPCs</c>(200) ——
        /// 在有浮游剑阵同时在场时是实打实的无谓开销。改为**只在自己没有锁定目标时**才扫，
        /// 且扫的间隔不小于 <see cref="HomingRescanInterval"/>。
        /// </summary>
        private const int HomingRescanInterval = 2;

        /// <summary>
        /// 每次 AI 更新的最大转向角（弧度）。注意 extraUpdates=3，即**每 tick 转 4 次**。
        /// <para/>由 0.030 提到 <b>0.052</b> → 每 tick 最大约 0.21 rad ≈ 12°，转向明显跟手。
        /// </summary>
        private const float HomingTurnRate = 0.052f;

        /// <summary>
        /// 超过这个夹角的目标才放弃（弧度）。
        /// <para/>由 1.15（66°）放宽到 <b>2.2（≈126°）</b> —— 原先背面的敌人完全不追，
        /// 实战手感就是"经常不拐弯"。
        /// </summary>
        private const float HomingMaxAngle = 2.2f;

        private void ApplyHoming()
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active) return;

            // 门控：星宿「追翎」未点亮 → 完全保持旧弹道
            if (!StarEffects.Of(owner).HasFlag(StarFlag.BladeEcho)) return;
            if (Projectile.localAI[0] < HomingDelayTicks) return;
            if (Projectile.velocity.LengthSquared() < 0.01f) return;

            Vector2 dir = Vector2.Normalize(Projectile.velocity);

            // ---- ① 先续记忆锁：只要还在「保持半径」内就继续追它（迟滞）----
            // 这一步放在索敌之前是关键：旧版就是先查缓存 `_targetNPCIndex` 再考虑重扫，
            // 只有这样边界处的目标才不会被反复锁上/丢掉。
            NPC best = null;
            if (Projectile.localAI[1] > 0f)
            {
                int idx = (int)Projectile.localAI[1] - 1;
                if (idx >= 0 && idx < Main.maxNPCs)
                {
                    NPC prev = Main.npc[idx];
                    // 续锁**不要求视线**（对齐旧版）：飞行途中地形短暂遮挡就脱锁的话，
                    // 贴地战会频繁丢目标；只有"重新选目标"才要求看得见。
                    if (prev.active && !prev.friendly && prev.CanBeChasedBy(Projectile)
                        && Vector2.DistanceSquared(prev.Center, Projectile.Center)
                           <= HomingRetainRange * HomingRetainRange)
                        best = prev;
                }
            }

            // ---- ② 没有锁定目标时才重新索敌（并节流）----
            if (best == null)
            {
                Projectile.localAI[1] = 0f;

                // 节流：不必每个 AI 步都全扫一遍 Main.maxNPCs
                if ((int)Projectile.localAI[0] % HomingRescanInterval != 0) return;

                float bestSq = HomingRange * HomingRange;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal || npc.lifeMax <= 5) continue;
                    if (!npc.CanBeChasedBy(Projectile)) continue;

                    Vector2 to = npc.Center - Projectile.Center;
                    float dSq = to.LengthSquared();
                    if (dSq > bestSq || dSq < 1f) continue;

                    // ★ 视线判定（旧版同款 `Collision.CanHit`）：只锁看得见的敌人。
                    // 否则飞刀会锁定墙后的目标并一路拐进地形里 —— 玩家读到的不是"追踪强"，
                    // 而是"这刀老往墙上撞"。
                    if (!Collision.CanHit(Projectile.Center, 1, 1, npc.Center, 1, 1)) continue;

                    // 夹角限制：放宽到 126°，但仍排除正后方刚出膛就掉头
                    float ang = Math.Abs(MathHelper.WrapAngle((float)Math.Atan2(to.Y, to.X)
                                                              - (float)Math.Atan2(dir.Y, dir.X)));
                    if (ang > HomingMaxAngle) continue;

                    bestSq = dSq;
                    best = npc;
                }
                if (best == null) return;
            }
            Projectile.localAI[1] = best.whoAmI + 1;   // 记住锁定目标（+1 让 0 表示"无"）

            Vector2 desired = Vector2.Normalize(best.Center - Projectile.Center);
            float curAng = (float)Math.Atan2(dir.Y, dir.X);
            float desAng = (float)Math.Atan2(desired.Y, desired.X);
            float turn = MathHelper.Clamp(MathHelper.WrapAngle(desAng - curAng), -HomingTurnRate, HomingTurnRate);
            float newAng = curAng + turn;

            Projectile.velocity = new Vector2((float)Math.Cos(newAng), (float)Math.Sin(newAng))
                                  * Projectile.velocity.Length();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 基础：中毒（阶段 6+ 由更高阶词缀强化；无瘟疫路径）
            target.AddBuff(BuffID.Poisoned, 180);

            // 命中光尘迸发（限流）
            if (EffectLimiterSystem.CanSpawnEffect(3, 100))
            {
                for (int i = 0; i < 4; i++)
                {
                    int di = Dust.NewDustPerfect(
                        target.Center + Main.rand.NextVector2Circular(target.width / 2f, target.height / 2f),
                        DustID.TintableDust,
                        Main.rand.NextVector2Circular(3f, 3f),
                        0, MalachitePalette.GreenBright, Main.rand.NextFloat(0.5f, 0.9f)).dustIndex;
                    Main.dust[di].noGravity = true;
                    Main.dust[di].fadeIn = 0.3f;
                }
            }

            ApplyStarWebOnHit(target, hit, damageDone);
        }

        /// <summary>
        /// 星网质变在命中点的结算（2026-09-27 星图 3.0）。
        /// <para/>只在**弹幕归属端**执行：多人下所有客户端都会跑到 OnHitNPC，
        /// 不设这道门会让回响/念爆在联机里成倍结算。
        /// </summary>
        private void ApplyStarWebOnHit(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner != Main.myPlayer) return;
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;

            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || target == null || !target.active) return;

            var mp = owner.GetModPlayer<MalachitePlayer>();
            TalentProfile profile = mp.Profile;
            if (profile.Flags == StarFlag.None) return;

            // —— 星宿「连锋」：连续命中同一目标时叠层 ——
            if (profile.HasFlag(StarFlag.ChainEdge)) mp.ApplyLinkStack(target);

            // —— 星宿「刃回响 BladeEcho」：命中后追加一次 40% 伤害的回响 ——
            if (profile.HasFlag(StarFlag.BladeEcho) && damageDone > 0)
            {
                NPC.HitInfo echo = hit;
                echo.Damage = Math.Max(1, (int)(damageDone * CrestNodes.BladeEchoDamageMult));
                echo.Crit = false;
                echo.DamageType = MindDamageClass.Instance;
                target.StrikeNPC(echo);

                if (EffectLimiterSystem.CanSpawnEffect(2, 90))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        int di = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(14f, 14f),
                            DustID.TintableDust, Main.rand.NextVector2Circular(2.5f, 2.5f), 0,
                            MalachitePalette.AccentCyan, Main.rand.NextFloat(0.5f, 1.0f)).dustIndex;
                        Main.dust[di].noGravity = true;
                        Main.dust[di].fadeIn = 0.4f;
                    }
                }
            }

            // —— 星核「盈满之念 NucleusOverflow」：暴击的溢出转为小范围念爆 ——
            if (hit.Crit && profile.HasFlag(StarFlag.NucleusOverflow) && damageDone > 0)
            {
                const float radius = 120f;
                int splash = Math.Max(1, (int)(damageDone * NucleusOverflowSplashMult));
                int hits = 0;

                for (int i = 0; i < Main.maxNPCs && hits < NucleusOverflowMaxTargets; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                    if (!npc.CanBeChasedBy()) continue;
                    if (Vector2.DistanceSquared(npc.Center, target.Center) > radius * radius) continue;

                    npc.StrikeNPC(new NPC.HitInfo
                    {
                        Damage = splash,
                        Knockback = 1.5f,
                        HitDirection = npc.Center.X >= target.Center.X ? 1 : -1,
                        Crit = false,
                        DamageType = MindDamageClass.Instance,
                    });
                    hits++;
                }

                if (EffectLimiterSystem.CanSpawnEffect(4, 120))
                {
                    for (int i = 0; i < 12; i++)
                    {
                        float ang = MathHelper.TwoPi * i / 12f;
                        Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);
                        int di = Dust.NewDustPerfect(target.Center + vel * 5f, DustID.TintableDust, vel, 0,
                            i % 2 == 0 ? MalachitePalette.AccentGold : MalachitePalette.GreenBright,
                            Main.rand.NextFloat(0.7f, 1.3f)).dustIndex;
                        Main.dust[di].noGravity = true;
                        Main.dust[di].fadeIn = 0.4f;
                    }
                }
            }
        }

        /// <summary>「盈满之念」念爆的溅射伤害倍率（相对本次命中伤害）。</summary>
        private const float NucleusOverflowSplashMult = 0.35f;

        /// <summary>「盈满之念」念爆最多波及的目标数（防成群敌人时结算爆炸）。</summary>
        private const int NucleusOverflowMaxTargets = 5;

        /// <summary>残影轨迹 + 三层光晕（参考灾厄 afterimages 与 CWR 配色分层）。</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Color main = MalachitePalette.PrimaryGreen;

            // 绘制数学已提取至 Core\Vfx\VfxDraw.cs（唯一出处：游戏内与 UI 共用）
            VfxDraw.DrawKnifeTrail(Main.spriteBatch, tex, AdditiveLayer.Pixel,
                Projectile.oldPos, Projectile.Center,
                new Vector2(Projectile.width / 2f, Projectile.height / 2f),
                Projectile.rotation, Projectile.scale, Projectile.velocity, main, Main.screenPosition);

            return false;
        }
    }
}
