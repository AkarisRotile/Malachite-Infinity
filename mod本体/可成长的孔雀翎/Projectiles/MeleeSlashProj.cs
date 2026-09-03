// 代码来源与合规署名：
// - 手感设计参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES——高频连段/精准被动/冲刺无敌帧。
// - 挥动结构参考（学习后自主实现，未直接复制源码）：
//   · CalamityEntropy（社区开源）Content/Items/Donator/TlipocasScythe.cs 内嵌 TlipocasScytheHeld.PreDraw：
//     挥扫历史角采样 → 内缘(≈0.5R)~外缘(刃尖)的扇形光带 + 嵌套白热芯带 + 刃身本体后置；本实现改用
//     自制柔光贴图(SlashGlow.png)以 sprite 段等效还原（无其 shader/贴图资产）。
//     https://github.com/hocha113/CalamityEntropy
//   · CalamityOverhaul（MIT, (c) hocha113）OniSlashRenderer：爆发过冲→回坐曲线、带宽包络/外锐内柔。
//   · CalamityModPublic（Azafure LLC 专有·仅参考）DevilsDevastationHoldout：本体+多层光晕的分层思路。
// - 挥动贴图：用户自绘 MeleeSlash.png（圆心=最左像素）。柔光贴图 SlashGlow.png 为本仓自制。
// 本文件为自主实现。
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 近战形态·斩击（v5.7 重写）：按参考实现（特莉波卡镰刀 TlipocasScytheHeld）的结构做"刀光"：
    /// - 挥扫 = 爆发曲线推进 θ，逐帧记录 (θ, 刃长刻度, 变形)；
    /// - 刀光 = 扇形光带（内缘 0.5R→刃尖外缘，自制柔光贴图渐变）+ 嵌套白热芯 + 刀尖轨迹锐线；
    /// - 刃身贴图仅当前姿态一层（本体调暗 + 白热芯叠加），不再叠贴图残影（消除"贴纸暂留"）；
    /// - 命中：切线火花 + 扩散环 + 砍痕闪刃 + 震屏（ScreenShakeSystem 全屏 + 本弹局部抖动）。
    /// </summary>
    public class MeleeSlashProj : ModProjectile
    {
        /// <summary>用户自绘挥动贴图（已裁透明边）。</summary>
        public override string Texture => "可成长的孔雀翎/Textures/MeleeSlash";

        public int SlashStep = 0;
        public int SlashDir = 1;
        public MalachiteMelee.MoveKind Kind = MalachiteMelee.MoveKind.Step;
        public float CustomStart = 0f;
        public float CustomEnd = 0f;
        public float ReachMult = 1f;
        public float ScaleMult = 1f;
        public int ExtraHold = 0;

        private static Texture2D _glowTex;
        private static Effect _slashFx;
        private static bool _fxProbeDone = false;
        private static bool _fxNotified = false;
        private static bool _dbgErrShown = false;

        private static bool FxReady
        {
            get
            {
                if (!_fxProbeDone)
                {
                    _fxProbeDone = true;
                    // 同步加载并把所有异常吞掉：源码/JIT(开发)模式下 tML 不编译 .fx，资产必然不存在；
                    // 若用同步 Request 仍抛错会漏到主线程绘制栈（见 2026-09-03 client.log 事故），故必须就地吞掉并回退 sprite 光带。
                    try
                    {
                        _slashFx = ModContent.Request<Effect>("可成长的孔雀翎/Effects/SlashArc", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
                    }
                    catch
                    {
                        _slashFx = null;
                    }
                }
                return _slashFx != null;
            }
        }

        private int _age = 0;
        private int _flash = 0;
        private int _hitFx = 0;
        private Vector2 _hitPos = Vector2.Zero;
        private Vector2 _hitDir = Vector2.UnitX;
        private bool _upperHit = false;
        private float _shakeT = 0f;

        private readonly List<float> _th = new List<float>();
        private readonly List<float> _sc = new List<float>();
        private readonly List<float> _sx = new List<float>();
        private readonly List<float> _sy = new List<float>();
        private readonly List<Vector2> _tip = new List<Vector2>();

        private static Texture2D GlowTex => _glowTex ??= ModContent.Request<Texture2D>("可成长的孔雀翎/Textures/SlashGlow").Value;
        private static int Gather => MalachiteMelee.SwingGatherFrames;
        private static int SweepEnd => MalachiteMelee.SwingGatherFrames + MalachiteMelee.SwingSweepFrames;
        private int TotalFrames => SweepEnd + MalachiteMelee.SwingHoldFrames + ExtraHold;

        public override void SetDefaults()
        {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
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

        private static float BurstCurve(float p)
        {
            p = Math.Clamp(p, 0f, 1f);
            float e = MalachiteMelee.SwingBurstEnd;
            if (p < e) return MalachiteMelee.SwingOvershoot * SmoothStep01(p / e);
            return MathHelper.Lerp(MalachiteMelee.SwingOvershoot, 1f, SmoothStep01((p - e) / (1f - e)));
        }

        private void SwingArc(int step, out float start, out float end)
        {
            if (Kind == MalachiteMelee.MoveKind.Step)
            {
                start = MalachiteMelee.StepStartRad(step, SlashDir);
                end = MalachiteMelee.StepEndRad(step, SlashDir);
            }
            else
            {
                start = MalachiteMelee.FrontMirror(CustomStart, SlashDir);
                end = MalachiteMelee.FrontMirror(CustomEnd, SlashDir);
            }
        }

        public override void AI()
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) { Projectile.Kill(); return; }
            if (_age >= TotalFrames) { Projectile.Kill(); return; }

            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            SwingArc(step, out float start, out float end);
            float reachBase = MalachiteMelee.StepReach(step) * ReachMult;

            if (_age < Gather)
            {
                Projectile.friendly = false;
                Projectile.rotation = start;
                PlaceAt(owner, start, reachBase * 0.5f);
            }
            else if (_age < SweepEnd)
            {
                Projectile.friendly = true;
                int since = _age - Gather;
                float p = since / (float)Math.Max(1, MalachiteMelee.SwingSweepFrames);
                float c = BurstCurve(p);
                float theta = MathHelper.Lerp(start, end, c);
                float pulse = 1f + MalachiteMelee.SwingReachPulse * c;
                Projectile.rotation = theta;
                PlaceAt(owner, theta, reachBase * pulse);
                if (since == 0 && Kind != MalachiteMelee.MoveKind.Step && Projectile.owner == Main.myPlayer)
                    _shakeT = Kind == MalachiteMelee.MoveKind.Finisher ? 7f : 5f;
                if (p >= MalachiteMelee.SwingBurstEnd && _flash == 0) _flash = 3;

                float scaleFactor = Kind == MalachiteMelee.MoveKind.Step ? MalachiteMelee.StepScale(step) : ScaleMult;
                float artScale = reachBase * pulse * scaleFactor / MalachiteMelee.SlashArtWidth;
                float sx = Kind == MalachiteMelee.MoveKind.Step ? MalachiteMelee.StepStretchX(step) : 1.05f;
                float sy = Kind == MalachiteMelee.MoveKind.Step ? MalachiteMelee.StepSquashY(step) : 0.90f;
                sx *= 1f + 0.10f * c;
                sy *= 1f - 0.06f * c;
                _th.Add(theta);
                _sc.Add(artScale);
                _sx.Add(sx);
                _sy.Add(sy);
                _tip.Add(owner.Center + new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * (artScale * MalachiteMelee.SlashArtWidth * sx));
                TrimHistory();
            }
            else
            {
                Projectile.friendly = true;
                Projectile.rotation = end;
                PlaceAt(owner, end, reachBase);
            }

            if (_flash > 0) _flash--;
            if (_hitFx > 0) _hitFx--;
            if (_shakeT > 0f) _shakeT = Math.Max(0f, _shakeT - 1f);
            _age++;
        }

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
                _th.RemoveAt(0); _sc.RemoveAt(0); _sx.RemoveAt(0); _sy.RemoveAt(0); _tip.RemoveAt(0);
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

            if (Kind == MalachiteMelee.MoveKind.Upper && !_upperHit)
            {
                _upperHit = true;
                target.velocity.Y -= 9f;
                target.velocity.X *= 0.4f;
                target.netUpdate = true;
            }

            Vector2 radial = target.Center - owner.Center;
            if (radial.LengthSquared() < 1f) radial = Vector2.UnitX * (SlashDir != 0 ? SlashDir : 1);
            else radial.Normalize();
            Vector2 travel = Vector2.Zero;
            if (_tip.Count >= 2) travel = _tip[_tip.Count - 1] - _tip[_tip.Count - 2];
            if (travel.LengthSquared() < 1f)
                travel = radial.RotatedBy(MathHelper.PiOver2 * (SlashDir < 0 ? -0.5f : 0.5f));
            travel.Normalize();
            Vector2 perp = new Vector2(-travel.Y, travel.X);

            if (_hitFx <= 0)
            {
                _hitFx = MalachiteMelee.HitFlashFrames;
                _hitPos = target.Center;
                _hitDir = travel;
                _shakeT = Math.Max(_shakeT, 3f);
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.32f, Pitch = 0.25f }, target.Center);
                for (int i = 0; i < 4; i++)
                {
                    if (!EffectLimiterSystem.CanSpawnEffect(1, 60)) break;
                    EffectLimiterSystem.SpawnSpark(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        travel * Main.rand.NextFloat(0.5f, 2.2f) + Main.rand.NextVector2Circular(2f, 2f),
                        Color.White, Main.rand.NextFloat(1.2f, 1.9f), 12);
                }
            }

            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            Color spark = step >= 2 || Kind == MalachiteMelee.MoveKind.Finisher ? MalachitePalette.AccentGold : MalachitePalette.GreenBright;
            int count = Kind == MalachiteMelee.MoveKind.Finisher ? 12 : (step >= 2 ? 9 : 6);
            for (int i = 0; i < count; i++)
            {
                if (!EffectLimiterSystem.CanSpawnEffect(1, 60)) break;
                float back = Main.rand.NextFloat(2.5f, 6.5f);
                float side = Main.rand.NextFloat(0f, 3.5f) * (Main.rand.NextBool() ? 1f : -1f);
                Vector2 vel = -travel * back + perp * side;
                EffectLimiterSystem.SpawnSpark(target.Center + Main.rand.NextVector2Circular(12f, 12f), vel,
                    Main.rand.NextBool(5) ? Color.White : spark, Main.rand.NextFloat(0.9f, 1.6f), 20);
            }
        }

        /// <summary>1x1 Pixel 拉伸段（锐线/环用）。</summary>
        private static void DrawSeg(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 1f || width < 0.5f) return;
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, mid, null, color, d.ToRotation(),
                new Vector2(0.5f, 0.5f), new Vector2(len, Math.Max(1f, width)), SpriteEffects.None, 0f);
        }

        /// <summary>柔光贴图段（刀光主体）：白芯柔光拉伸成扇形光带单元（等效参考的渐变条带，无 shader）。</summary>
        private static void DrawGlowSeg(Vector2 a, Vector2 b, float width, Color color)
        {
            Texture2D gt = GlowTex;
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 2f) return;
            Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
            Main.spriteBatch.Draw(gt, mid, null, color, d.ToRotation(), gt.Size() * 0.5f,
                new Vector2(len / gt.Width, Math.Max(1f, width) / gt.Height), SpriteEffects.None, 0f);
        }

        private static readonly VertexPositionColorTexture[] _fanVerts = new VertexPositionColorTexture[64];

        /// <summary>刀光扇形带：shader 版（顶点带 + SlashArc.fx，等效参考的 Reveal/刃头线/径向三层）。</summary>
        private void DrawFanShader(Player owner, Vector2 oc, int step, Color tint, float holdFade)
        {
            SwingArc(step, out float theta0, out float _u1); _ = _u1;
            float theta1 = _th[_th.Count - 1];
            float R = _sc[_th.Count - 1] * MalachiteMelee.SlashArtWidth * _sx[_th.Count - 1];
            if (Math.Abs(theta1 - theta0) < 0.001f || R <= 2f) return;
            int slices = 28;
            float pNow = Math.Clamp((_age - Gather) / (float)Math.Max(1, MalachiteMelee.SwingSweepFrames), 0f, 1f);
            float innerK = MalachiteMelee.PathInnerK;
            for (int k = 0; k <= slices; k++)
            {
                float uc = pNow * k / slices;
                float th = MathHelper.Lerp(theta0, theta1, BurstCurve(uc));
                Vector2 dd = new Vector2((float)Math.Cos(th), (float)Math.Sin(th));
                float rad = R;
                // 内缘 v=0 / 外缘 v=1（外缘=刀尖轨迹锐利）
                Vector2 pi = oc + dd * (rad * innerK) - Main.screenPosition;
                Vector2 po = oc + dd * rad - Main.screenPosition;
                float a = MathHelper.Lerp(0.30f, 1f, uc) * holdFade;
                _fanVerts[k * 2] = new VertexPositionColorTexture(new Vector3(pi, 0f), Color.White * a, new Vector2(uc, 0f));
                _fanVerts[k * 2 + 1] = new VertexPositionColorTexture(new Vector3(po, 0f), Color.White * a, new Vector2(uc, 1f));
            }

            var gd = Main.graphics.GraphicsDevice;
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, _slashFx, Matrix.Identity);
            _slashFx.Parameters["transformMatrix"].SetValue(Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, 0f, 1f));
            Vector3 tc = tint.ToVector3();
            _slashFx.Parameters["uColTint"].SetValue(tc);
            _slashFx.Parameters["uColDeep"].SetValue(Vector3.Lerp(tc, Vector3.Zero, 0.72f));
            _slashFx.Parameters["uColHot"].SetValue(Vector3.One);
            _slashFx.Parameters["uLead"].SetValue(1f);
            _slashFx.Parameters["uFlash"].SetValue(Math.Clamp(_flash / 3f, 0f, 1f));
            _slashFx.Parameters["uOpacity"].SetValue(holdFade);
            _slashFx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, _fanVerts, 0, slices * 2);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>刀光扇形带：sprite 回退版（shader 不可用时的保底，观感同 v5.7）。</summary>
        private void DrawFanSprite(Player owner, Vector2 oc, int step, Color tint, float holdFade)
        {
            SwingArc(step, out float theta0, out float _u1); _ = _u1;
            float theta1 = _th[_th.Count - 1];
            float R = _sc[_th.Count - 1] * MalachiteMelee.SlashArtWidth * _sx[_th.Count - 1];
            if (Math.Abs(theta1 - theta0) < 0.001f || R <= 2f) return;
            int steps = 30;
            float pNow = Math.Clamp((_age - Gather) / (float)Math.Max(1, MalachiteMelee.SwingSweepFrames), 0f, 1f);
            float sliceW = Math.Abs(theta1 - theta0) / steps;
            Vector2 prevOuter = Vector2.Zero;
            bool hasPrev = false;
            for (int k = 0; k <= steps; k++)
            {
                float s = pNow * k / steps;
                float th = MathHelper.Lerp(theta0, theta1, BurstCurve(s));
                Vector2 dd = new Vector2((float)Math.Cos(th), (float)Math.Sin(th));
                float tk = k / (float)steps;
                float ga = MalachiteMelee.PathGlowAlpha * (0.15f + 0.85f * tk) * holdFade;
                Color gc = Color.Lerp(tint, Color.White, Math.Clamp(tk * 1.6f, 0f, 1f));
                float wid = Math.Max(4f, sliceW * R * 1.05f);
                DrawGlowSeg(oc + dd * (R * 0.50f), oc + dd * R, wid, gc * ga);
                float wa = MalachiteMelee.PathWhiteAlpha * (0.5f + 0.5f * tk) * holdFade;
                DrawGlowSeg(oc + dd * (R * 0.74f), oc + dd * (R * 0.98f), wid * 0.55f, Color.White * wa);
                if (hasPrev)
                    DrawSeg(prevOuter, oc + dd * R, Math.Max(2f, MalachiteMelee.StepReach(step) * ReachMult * MalachiteMelee.PathEdgeWidth), gc * Math.Min(1f, ga * 2.2f));
                prevOuter = oc + dd * R;
                hasPrev = true;
            }
        }

        private float KindRingK()
        {
            return Kind switch
            {
                MalachiteMelee.MoveKind.Finisher => 1.9f,
                MalachiteMelee.MoveKind.Charged => 1.4f,
                _ => 1f,
            };
        }

        public override bool PreDraw(ref Color lightColor)
        {
            try { DrawCore(ref lightColor); }
            catch (Exception ex) { if (!_dbgErrShown) { _dbgErrShown = true; Main.NewText("[近战特效] 绘制异常: " + ex.Message, MalachitePalette.DangerRed); } }
            return false;
        }

        private void DrawCore(ref Color lightColor)
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || _th.Count == 0) return;

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = new Vector2(0f, MalachiteMelee.SlashArtPivotY);
            int n = _th.Count;
            int step = Math.Clamp(SlashStep, 0, MalachiteMelee.ComboMaxSteps - 1);
            Color tint = Kind == MalachiteMelee.MoveKind.Finisher ? MalachitePalette.AccentGold
                : Kind == MalachiteMelee.MoveKind.Charged ? MalachitePalette.GreenBright
                : step switch { 0 => MalachitePalette.GreenBright, 1 => Color.White, _ => MalachitePalette.AccentGold };

            // 刀光震屏（重击/命中局部轻颤；全屏另由 ScreenShakeSystem）
            Vector2 jit = _shakeT > 0f ? Main.rand.NextVector2Circular(_shakeT * 0.30f, _shakeT * 0.30f) : Vector2.Zero;
            Vector2 oc = owner.Center + jit;
            Vector2 pivot = oc - Main.screenPosition;

            float reachBase = MalachiteMelee.StepReach(step) * ReachMult;
            float holdFade = _age > SweepEnd
                ? MathHelper.Lerp(1f, 0.5f, (_age - SweepEnd) / (float)Math.Max(1, MalachiteMelee.SwingHoldFrames + ExtraHold))
                : 1f;

            // ========== 刀光主体（GPU）：顶点带 + SlashArc.fx（Reveal 揭开/刃头白热线/径向三层截面，
            //     机制参考鬼切 OniGateRift.fx，自主精简实现；shader 不可用自动回退 sprite 光带保底可见） ==========
            if (FxReady)
            {
                if (!_fxNotified)
                {
                    _fxNotified = true;
                    Main.NewText("[刀光] SlashArc.fx GPU 刀光已启用", MalachitePalette.GreenBright);
                }
                DrawFanShader(owner, oc, step, tint, holdFade);
            }
            else
                DrawFanSprite(owner, oc, step, tint, holdFade);

            // ========== 刃身本体：只画当前姿态一层（本体调暗 + 白热芯，杜绝贴纸残影） ==========
            Vector2 curScale = new Vector2(_sx[n - 1], _sy[n - 1]) * _sc[n - 1];
            Main.spriteBatch.Draw(tex, pivot, null, Color.White * (0.20f * MalachiteMelee.SlashArtAlpha),
                _th[n - 1], origin, curScale * 1.6f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pivot, null, tint * (0.30f * MalachiteMelee.SlashArtAlpha),
                _th[n - 1], origin, curScale * 1.25f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pivot, null,
                new Color(MalachiteMelee.SlashArtBrightness, MalachiteMelee.SlashArtBrightness, MalachiteMelee.SlashArtBrightness) * (0.85f * MalachiteMelee.SlashArtAlpha),
                _th[n - 1], origin, curScale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pivot, null, Color.White * (0.75f * MalachiteMelee.SlashArtAlpha),
                _th[n - 1], origin, curScale * 0.94f, SpriteEffects.None, 0f);

            // ========== 满形闪（落位帧整条光带高亮一拍） ==========
            if (_flash > 0)
            {
                float fa = 0.55f * _flash / 3f;
                for (int i = 0; i < _tip.Count - 1; i++)
                    DrawGlowSeg(_tip[i] + jit, _tip[i + 1] + jit, 26f, Color.White * fa);
            }

            // ========== 击中反馈：扩散环 + 砍痕闪刃 ==========
            if (_hitFx > 0)
            {
                float t = _hitFx / (float)MalachiteMelee.HitFlashFrames;
                float ringR = MalachiteMelee.HitRingMaxR * KindRingK() * (1f - t) + 5f;
                Color rc = Color.Lerp(Color.White, tint, 0.4f) * (0.9f * t);
                int segs = 14;
                for (int s = 0; s < segs; s++)
                {
                    float a0 = MathHelper.TwoPi * s / segs;
                    float a1 = MathHelper.TwoPi * (s + 1) / segs;
                    DrawSeg(_hitPos + jit + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * ringR,
                            _hitPos + jit + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * ringR, 2.5f, rc);
                }
                Vector2 hd = _hitDir;
                DrawSeg(_hitPos + jit - hd * (MalachiteMelee.HitSlashLen * 0.55f),
                        _hitPos + jit + hd * (MalachiteMelee.HitSlashLen * 0.45f), 3.5f, Color.White * (0.85f * t));
            }
        }
    }
}