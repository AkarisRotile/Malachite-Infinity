// 代码来源与合规署名：
// - 机制灵感（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES 的"纹章术"——
//   挥击后在空中留下延迟引爆的晶体纹章。绘制为原版基础几何 + Additive 多层发光，配色仅用 MalachitePalette。
// - v6.6 起绘制为 2.0 护眼神圣几何（模块三）：原版 Extra[98]/[89] 光晕贴图 5 层复合，高光严控核心小区域。
// 本文件为自主实现（无外部参考源码）。
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// ES 空间纹章（Crest Arts 2.0 · 严格护眼版，v6.6）。
    /// - 配置入口：SetupCrest(scaleAspect, totalScale, explosionDelay, damageMult, onHitAction)（§四矩阵唯一出处 MalachiteMelee.CrestSpecOf）。
    /// - 生命线（以各纹章引爆帧为基准，Prompt「22=13+9」同构）：0~delay-3 展开自转聚能 → delay-2~delay-1 向心坍缩(-18%) →
    ///   delay 帧晶化引爆（单次范围判定，或 LingerFrames>0 进入持续切割期）→ delay+1~delay+9 膨胀消散。
    /// - 护眼铁律：严禁全屏闪白；纯白高光只存在于核心晶核（×0.22 微小半径）；外围保持翡翠绿/暗金高饱和。
    /// - 渲染铁律：宽长比 scaleAspect、几何 × totalScale；绘制坐标 = world − Main.screenPosition；
    ///   Additive 状态下绘制，结束时严格还原 BlendState.AlphaBlend。
    /// - 注：MP 权威同步列 M 待办（D20），当前原型期本地判定。
    /// </summary>
    public class EsCrestSigilProj : ModProjectile
    {
        public override string Texture => "可成长的孔雀翎/Textures/Pixel"; // 纯几何绘制，贴图占位

        /// <summary>引爆范围判定基准半宽（px，未乘 totalScale/aspect）。</summary>
        private const float CrestBaseR = 65f;

        private int Age => (int)Projectile.localAI[0];

        // —— SetupCrest 配置字段（原型期本地判定，D20：MP 权威同步列后续 M 项）——
        private float _aspectX = 1f;
        private float _aspectY = 1f;
        private float _totalScale = 1f;
        private int _delay = 12;
        private float _dmgMult = 0.75f;
        private int _linger = 0;
        private MalachiteMelee.MoveKind _kind = MalachiteMelee.MoveKind.Step1;
        private int _dir = 1;
        private bool _gold = false;
        private int _tier = 0;              // 纹章档位（0=小 1=大），由 CrestSpec.Tier 经 SetBehavior 下发
        private Action<NPC> _onHit = null;

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600; // 实际生命由 SetupCrest 与 AI 年龄线控制
            Projectile.ignoreWater = true;
            Projectile.DamageType = MindDamageClass.Instance;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6; // 持续切割（悬浮地雷）重复判定间隔
        }

        /// <summary>
        /// 纹章参数配置入口（§四）：外形长宽比 / 整体缩放 / 引爆延迟 / 伤害倍率 / 命中回调。
        /// 须在 Projectile.NewProjectile 之后、首帧 AI 之前调用。
        /// </summary>
        public void SetupCrest(Vector2 scaleAspect, float totalScale, int explosionDelay, float damageMult, Action<NPC> onHitAction = null)
        {
            _aspectX = Math.Max(0.1f, scaleAspect.X);
            _aspectY = Math.Max(0.1f, scaleAspect.Y);
            _totalScale = Math.Max(0.1f, totalScale);
            _delay = Math.Max(0, explosionDelay);
            _dmgMult = Math.Max(0.05f, damageMult);
            _onHit = onHitAction;
            Projectile.timeLeft = _delay + 12 + _linger;
        }

        /// <summary>绑定来源招式/朝向/持续切割帧/金纹章/纹章档位（在 SetupCrest 之前调用，供引爆回调与配色使用）。</summary>
        public void SetBehavior(MalachiteMelee.MoveKind kind, int dir, int lingerFrames, bool gold, int tier = 0)
        {
            _kind = kind;
            _dir = dir != 0 ? dir : 1;
            _linger = Math.Max(0, lingerFrames);
            _gold = gold;
            _tier = tier;
            Projectile.timeLeft = _delay + 12 + _linger;
        }

        /// <summary>按矩阵生成纹章（§四）：返回弹幕实例；delayOverride<0 时使用 spec.ExplosionDelay。</summary>
        public static EsCrestSigilProj SpawnCrest(IEntitySource source, int ownerIndex, Vector2 position,
            int baseDamage, float knockback, MalachiteMelee.MoveKind kind, int dir,
            MalachiteMelee.CrestSpec spec, int delayOverride = -1)
        {
            int delay = delayOverride >= 0 ? delayOverride : spec.ExplosionDelay;
            int idx = Projectile.NewProjectile(source, position, Vector2.Zero,
                ModContent.ProjectileType<EsCrestSigilProj>(), baseDamage, knockback, ownerIndex);
            if (idx < 0 || idx >= Main.maxProjectiles) return null;
            EsCrestSigilProj crest = Main.projectile[idx].ModProjectile as EsCrestSigilProj;
            if (crest == null) return null;
            crest.SetBehavior(kind, dir, spec.LingerFrames, spec.Gold, spec.Tier);
            Player owner = ownerIndex >= 0 && ownerIndex < Main.maxPlayers ? Main.player[ownerIndex] : null;
            crest.SetupCrest(new Vector2(spec.AspectX, spec.AspectY), spec.TotalScale, delay, spec.DamageMult,
                owner != null && owner.active ? (NPC n) => MalachiteMelee.ApplyCrestHit(n, owner, kind, dir) : null);
            return crest;
        }

        public override void AI()
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
            int age = Age;

            // 引爆线：delay 后保留 9 帧膨胀淡出（持续切割期相应延长；Prompt 22=13+9 同构）
            if (age >= _delay + 10 + _linger) { Projectile.Kill(); return; }

            if (age == _delay) Explode();

            if (age < _delay)
            {
                Projectile.friendly = false;
                // 向心聚能粒子（半径随 totalScale）
                if (age % 3 == 0 && EffectLimiterSystem.CanSpawnEffect(1, 60))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 off = Main.rand.NextVector2Circular(34f, 34f) * _totalScale;
                        Vector2 from = Projectile.Center + off;
                        int di = Dust.NewDustPerfect(from, DustID.GemEmerald, -off.SafeNormalize(Vector2.UnitY) * 2.6f,
                            0, Color.White, Main.rand.NextFloat(0.8f, 1.3f)).dustIndex;
                        Main.dust[di].noGravity = true;
                        Main.dust[di].fadeIn = 0.5f;
                    }
                }
            }
            else if (_linger > 0 && age <= _delay + _linger)
            {
                Projectile.friendly = true; // 持续切割期：引擎按命中盒重复判定（6 帧间隔）
            }
            else
            {
                Projectile.friendly = false;
            }

            Projectile.rotation += 0.05f; // 缓慢自转
            Projectile.localAI[0]++;
        }

        /// <summary>引爆帧：伤害倍率一次性落定；单次爆=主动范围判定，地雷=开启持续切割命中盒。</summary>
        private void Explode()
        {
            Projectile.damage = Math.Max(1, (int)(Projectile.damage * _dmgMult));
            Player plr = Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers ? Main.player[Projectile.owner] : null;

            float halfW = CrestBaseR * _totalScale * _aspectX;
            float halfH = CrestBaseR * _totalScale * _aspectY;

            if (_linger > 0)
            {
                // 悬浮地雷（j.B）：命中盒=缩放后的几何陷阱，后续由引擎逐帧判定（OnHitNPC 施加矩阵机制）
                Vector2 core = Projectile.Center; // 改尺寸后重设中心，防止判定盒偏移
                Projectile.width = Math.Max(8, (int)(halfW * 2f));
                Projectile.height = Math.Max(8, (int)(halfH * 2f));
                Projectile.Center = core;
                Projectile.friendly = true;
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.85f, Pitch = 0.1f }, Projectile.Center);
                ScreenShakeSystem.Shake(4f);
                SpawnCrystalShards(8);
                return;
            }

            // 单次爆：主动范围判定，结算后立刻取消 friendly 防引擎重复结算
            Rectangle zone = new Rectangle(
                (int)(Projectile.Center.X - halfW), (int)(Projectile.Center.Y - halfH),
                Math.Max(16, (int)(halfW * 2f)), Math.Max(16, (int)(halfH * 2f)));
            float critChance = 15f;
            if (plr != null && plr.active) critChance += plr.GetTotalCritChance(MindDamageClass.Instance);

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || !npc.Hitbox.Intersects(zone)) continue;
                NPC.HitInfo hi = new NPC.HitInfo
                {
                    Damage = Projectile.damage,
                    Knockback = Projectile.knockBack,
                    HitDirection = npc.Center.X < Projectile.Center.X ? 1 : -1,
                    Crit = Main.rand.NextFloat(100f) < critChance,
                    DamageType = MindDamageClass.Instance,
                };
                npc.StrikeNPC(hi);
                ApplyOnHit(npc);
                if (EffectLimiterSystem.CanSpawnEffect(2, 60))
                {
                    for (int k = 0; k < 5; k++)
                        EffectLimiterSystem.SpawnSpark(npc.Center + Main.rand.NextVector2Circular(14f, 14f),
                            Main.rand.NextVector2Circular(3f, 3f),
                            _gold ? MalachitePalette.AccentGold : MalachitePalette.GreenBright,
                            Main.rand.NextFloat(0.9f, 1.5f), 16);
                }
            }
            plr?.GetModPlayer<MalachitePlayer>()?.ReportSpecialHit(_kind); // 防复读锁：纹章爆炸伤害解锁上一个被锁招式
            Projectile.friendly = false;

            // 2.0 引爆音：清脆玻璃破碎（Item27）打底；重招再叠重击音（Item14）
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.85f, Pitch = 0.1f }, Projectile.Center);
            if (_kind == MalachiteMelee.MoveKind.Step3 || _kind == MalachiteMelee.MoveKind.AirDive || _kind == MalachiteMelee.MoveKind.Finisher2)
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.25f }, Projectile.Center);
            SpawnCrystalShards(10);
            ScreenShakeSystem.Shake(_kind switch
            {
                MalachiteMelee.MoveKind.Finisher2 => 10f,
                MalachiteMelee.MoveKind.Step3 => 8f,
                MalachiteMelee.MoveKind.AirDive => 3f, // 触地 Impact 已大震，纹章只补
                _ => 4f,
            });

            // AirDive 地面冲击波：向两侧生成高速地刺粒子（§四矩阵）
            if (_kind == MalachiteMelee.MoveKind.AirDive)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int k = 0; k < 7; k++)
                    {
                        if (!EffectLimiterSystem.CanSpawnEffect(1, 90)) break;
                        Vector2 pos = Projectile.Center + new Vector2(side * (18f + k * 22f), -6f);
                        Vector2 vel = new Vector2(side * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(3f, 7f));
                        EffectLimiterSystem.SpawnSpark(pos, vel, MalachitePalette.AccentGold, Main.rand.NextFloat(0.9f, 1.5f), 18);
                    }
                }
            }
        }

        /// <summary>应用矩阵命中机制：优先 SetupCrest 传入的回调，否则按来源招式回退（§四 OnHit 列）。</summary>
        private void ApplyOnHit(NPC npc)
        {
            if (npc == null || !npc.active) return;
            if (_onHit != null) { _onHit(npc); return; }
            Player plr = Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers ? Main.player[Projectile.owner] : null;
            MalachiteMelee.ApplyCrestHit(npc, plr, _kind, _dir);
        }

        /// <summary>持续切割期引擎命中：应用矩阵机制（伤害已在 Explode 中按 damageMult 落定）。</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            ApplyOnHit(target);
            // 防复读锁：持续切割期引擎命中（悬浮地雷）也算造成伤害
            if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
                Main.player[Projectile.owner]?.GetModPlayer<MalachitePlayer>()?.ReportSpecialHit(_kind);
        }

        /// <summary>引爆晶体碎片：向四周喷溅细碎宝石尘（限流，无白光污染）。</summary>
        private void SpawnCrystalShards(int count)
        {
            for (int i = 0; i < count && EffectLimiterSystem.CanSpawnEffect(1, 80); i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                int di = Dust.NewDustPerfect(Projectile.Center, DustID.GemEmerald, vel, 0,
                    _gold ? MalachitePalette.AccentGold : MalachitePalette.GreenBright, Main.rand.NextFloat(0.7f, 1.2f)).dustIndex;
                Main.dust[di].noGravity = true;
                Main.dust[di].fadeIn = 0.5f;
            }
        }

        /// <summary>
        /// 2.0 护眼神圣纹章绘制（模块三）：5 层复合几何（原版 Extra[98]/[89] 光晕贴图）。
        /// 护眼铁律：严禁全屏泛白——纯白只存在于核心晶核（×0.22 微小半径）与引爆极细十字芯线。
        /// </summary>
        private void DrawSigilCrest()
        {
            Texture2D flareTex = TextureAssets.Extra[ExtrasID.SharpTears].Value;     // 原 Extra[98] 菱芒 flare
            Texture2D bloomTex = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value; // 原 Extra[89] 柔光 bloom

            int age = Age;
            int delay = Math.Max(1, _delay);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 绘制数学已提取至 Core\Vfx\VfxDraw.cs（唯一出处：游戏内与 UI 共用）
            VfxDraw.DrawCrestSigil(Main.spriteBatch, flareTex, bloomTex,
                Projectile.Center - Main.screenPosition,
                age, delay, _linger, _totalScale, _aspectX, _aspectY, Main.GameUpdateCount,
                MalachitePalette.GreenBright, MalachitePalette.AccentGold, MalachitePalette.AccentCyan, _tier);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Age < 0) return false;
            DrawSigilCrest();
            return false;
        }
    }
}
