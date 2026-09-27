// 本文件为自主实现（无外部参考源码）。
// 框架迁移自本模组「未解绑灾厄的最后一版稳定版本」的 OrbitingMalachiteProj（浮游剑阵），
// 逻辑等价重写，去掉灾厄依赖，改接本模组数据与弹幕。
// 2026-09-22 改为节点产物（CrestNodes.SwordArray，击败世纪之花解锁）：
//   生成点由随机方位指定 + 追身 + 穿墙 + 消亡代码（本文件原先缺失的三项）。
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 流风剑阵（剑阵节点产物）：每次普攻在玩家四周随机方位生成一道。
    /// <para/>生命周期 = 随机方位现身 → 穿墙朝玩家飞来 → 悬停自转蓄力 → 朝鼠标射出飞刀 → 消散消亡。
    /// <para/>ai[0] = 蓄力计帧；ai[1] = 出场延迟帧（保留以兼容错峰生成的调用惯例）；
    /// localAI[0] = 存活帧数；localAI[1] = 消亡倒计时（>0 时进入渐隐消亡，不再攻击/移动）。
    /// <para/>穿墙实现：tileCollide = false（全程），故剑阵可穿过物块追身，射出的飞刀同样设为不碰撞。
    /// </summary>
    public class OrbitingWillowProj : ModProjectile
    {
        public override string Texture => "可成长的孔雀翎/Textures/OrbitingMalachiteProjTex";

        /// <summary>绕身自转角速度（rad/帧）。</summary>
        private const float SpinPerFrame = 0.75f;

        /// <summary>追踪速度上限（px/帧）——远距离生成时防瞬时贴近。</summary>
        private const float MoveSpeedMax = 26f;

        /// <summary>追踪接近停止距离（px）：进入该半径后不再追身，留在该处悬停自转。</summary>
        private const float HoverDistance = 190f;

        /// <summary>射出飞刀的初速。</summary>
        private const float FireSpeed = 45f;

        /// <summary>蓄力帧数：stage≥13 更快（15 帧 vs 25 帧）。</summary>
        private const int ChargeFramesLow = 25;
        private const int ChargeFramesHigh = 15;

        /// <summary>消亡渐隐帧数（消亡代码的消散期）。</summary>
        private const int WitherFrames = 36;

        /// <summary>是否已进入消亡渐隐期（供生成方统计在场名额时排除）。</summary>
        public bool IsWithering => Projectile.localAI[1] > 0f;

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = MindDamageClass.Instance;
            Projectile.tileCollide = false;      // 穿墙：全程不碰撞物块
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255;              // 出场前不可见
            Projectile.usesIDStaticNPCImmunity = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.alpha >= 255)
                return false;

            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height * 0.75f);

            // 消亡期：整体渐隐（与 alpha 同步），让"消散"被看见
            Color drawColor = lightColor * (1f - Projectile.alpha / 255f);

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                drawColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.localAI[0]++;

            // ===== 消亡期：只做渐隐与粒子，不再移动、不再攻击 =====
            if (Projectile.localAI[1] > 0f)
            {
                UpdateWither();
                return;
            }

            // 错峰等待期：贴住生成点不动（可见性仍为 alpha=255 隐藏）
            if (Projectile.ai[1] > 0f)
            {
                Projectile.ai[1]--;
                return;
            }

            // ===== 出场帧：揭开 + 音效与火花 =====
            // ★ 2026-09-27 用户："剑阵不要向玩家飞过来，它在哪里生成就在哪里呆着就行了"。
            //   因此出场速度恒为 0；生成点由 CrestNodes.SpawnSwordArray 在玩家四周随机解算。
            if (Projectile.alpha == 255)
            {
                Projectile.alpha = 0;
                Projectile.velocity = Vector2.Zero;

                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f }, Projectile.Center);

                for (int i = 0; i < 6; i++)
                {
                    EffectLimiterSystem.SpawnSpark(
                        Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2Circular(2.5f, 2.5f),
                        Main.rand.NextBool(2) ? MalachitePalette.GreenBright : MalachitePalette.PrimaryGreen,
                        Main.rand.NextFloat(1.2f, 2.0f), 20);
                }
            }

            Lighting.AddLight(Projectile.Center, 0.1f, 0.45f, 0.2f);

            // ===== 消亡判定：超时即渐隐 =====
            // 不再按"离玩家多远"判死 —— 剑阵既然就地停留，玩家跑开不该把它抹掉。
            if (Projectile.localAI[0] > Projectile.timeLeft - WitherFrames)
            {
                BeginWither();
                UpdateWither();
                return;
            }

            int stage = ProgressSystem.GetStage();
            int maxCharge = stage >= 13 ? ChargeFramesHigh : ChargeFramesLow;

            if (Projectile.ai[0] < maxCharge)
            {
                Projectile.ai[0]++;

                // 就地悬停：只做自转与蓄力，不做任何位移
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation += SpinPerFrame;

                // 蓄力进度越高，环绕火花越密（限流）
                float progress = Projectile.ai[0] / (float)maxCharge;
                if (Main.rand.NextFloat() < progress && EffectLimiterSystem.CanSpawnEffect(2, 80))
                {
                    EffectLimiterSystem.SpawnSpark(
                        Projectile.Center + Main.rand.NextVector2Circular(15f, 15f), Vector2.Zero,
                        Main.rand.NextBool(3) ? MalachitePalette.GreenBright : MalachitePalette.PrimaryGreen,
                        Main.rand.NextFloat(1.2f, 1.8f), 10);
                }
            }
            else
            {
                // 蓄满：朝鼠标方向射出飞刀（高攻速下会对同一目标多段命中，走本地无敌帧节流）
                FireVolley(mult: 1f);
                // 射完即进入消亡（原「回锋折返」分支已随该节点改用途一并移除）
                BeginWither();
            }
        }

        /// <summary>
        /// 朝鼠标方向射出飞刀。mult 为伤害倍率（回锋第二射用 <see cref="CrestNodes.ReturnSecondShotMult"/>）。
        /// 仅在 owner 端生成，避免多人重复。
        /// </summary>
        private void FireVolley(float mult)
        {
            if (Projectile.owner != Main.myPlayer) return;
            try
            {
                Vector2 aimDir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitX);
                int dmg = Math.Max(1, (int)(Projectile.damage * mult));
                int knife = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                    aimDir * FireSpeed, ModContent.ProjectileType<MalachiteProj>(),
                    dmg, Projectile.knockBack, Projectile.owner);
                // 穿墙一致性：剑阵射出的飞刀同样穿墙，否则剑阵穿墙能力形同虚设
                if (knife >= 0 && knife < Main.maxProjectiles)
                    Main.projectile[knife].tileCollide = false;
            }
            catch (Exception) { }

            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);

            if (EffectLimiterSystem.CanSpawnEffect(3, 80))
            {
                for (int i = 0; i < 8; i++)
                {
                    EffectLimiterSystem.SpawnSpark(Projectile.Center, Main.rand.NextVector2Circular(5f, 5f),
                        MalachitePalette.PrimaryGreen, 2f, 15);
                }
            }
        }

        /// <summary>
        /// 【回锋节点】折返阶段：朝玩家方向飞回、途中不快进，归位后重新蓄力准备第二射。
        /// 返回 true 表示本帧仍在折返中（调用方应跳过常规蓄力/发射流程）。
        /// </summary>
        private bool UpdateReturn(Player player)
        {
            float dist = Vector2.Distance(Projectile.Center, player.Center);
            Projectile.localAI[2]++;

            // 朝玩家折返（复用穿墙追身逻辑，但速度更高、更"回旋"）
            if (dist > HoverDistance * 0.6f)
            {
                Vector2 seek = (player.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, seek * CrestNodes.ReturnHomingSpeed, 0.16f);
            }
            else
            {
                Projectile.velocity *= 0.80f;
            }
            Projectile.rotation += SpinPerFrame * 1.35f; // 折返期自转更快（"回旋"的观感）

            if (EffectLimiterSystem.CanSpawnEffect(1, 70))
            {
                EffectLimiterSystem.SpawnSpark(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f), Vector2.Zero,
                    MalachitePalette.AccentCyan, Main.rand.NextFloat(0.9f, 1.5f), 10);
            }

            if (Projectile.localAI[2] < CrestNodes.ReturnChargeFrames) return true;

            // 折返完毕：重新蓄力（回到蓄力起点），下一轮蓄满时会因 ai[2] 已置位而正常消亡
            Projectile.ai[0] = 0f;
            Projectile.localAI[2] = 0f;
            Projectile.velocity *= 0.25f;
            return false;
        }

        /// <summary>
        /// 消亡代码 · 起始：锁定渐隐倒计时并停止参与战斗。
        /// 幂等——重复调用不会重置倒计时。
        /// </summary>
        private void BeginWither()
        {
            if (Projectile.localAI[1] > 0f) return;
            Projectile.localAI[1] = WitherFrames;
            Projectile.friendly = false;   // 消亡期不再判定，避免"消散中的剑阵还在打人"
            Projectile.velocity = Vector2.Zero;
        }

        /// <summary>
        /// 消亡代码 · 每帧推进：渐隐（alpha 爬升）+ 由外向内收拢的消散粒子，倒计时归零时 Kill。
        /// </summary>
        private void UpdateWither()
        {
            Projectile.localAI[1]--;
            Projectile.velocity = Vector2.Zero;

            float t = 1f - Projectile.localAI[1] / (float)WitherFrames; // 0 → 1
            // alpha 100 → 255（与出场 255 → 100 的渐显对称）
            Projectile.alpha = Math.Min(255, (int)MathHelper.Lerp(100f, 255f, t));
            Projectile.rotation += SpinPerFrame * 0.35f;    // 消亡期旋转减速
            Lighting.AddLight(Projectile.Center, 0.1f * (1f - t), 0.45f * (1f - t), 0.2f * (1f - t));

            // 由外向内收拢的晶尘（越是末期越靠中心）
            if (EffectLimiterSystem.CanSpawnEffect(1, 70))
            {
                Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * (34f * (1f - t) + 6f);
                Vector2 vel = (Projectile.Center - from) * 0.12f;
                int di = Dust.NewDustPerfect(from, DustID.GemEmerald, vel, 0,
                    Main.rand.NextBool(3) ? MalachitePalette.GreenBright : MalachitePalette.PrimaryGreen,
                    Main.rand.NextFloat(0.6f, 1.1f)).dustIndex;
                Main.dust[di].noGravity = true;
                Main.dust[di].fadeIn = 0.4f;
            }

            if (Projectile.localAI[1] <= 0f)
            {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.3f }, Projectile.Center);
                Projectile.Kill();
            }
        }
    }
}
