// 代码来源与合规署名：
// - 手感设计参考（非代码参考）：《苍翼：混沌效应》(© ARC SYSTEM WORKS / 91Act) 角色 ES。
// - 挥动/刀光管线（学习后自主实现，未复制源码）：
//   CalamityOverhaul（MIT, (c) hocha113）OniSlash/OniSlashRenderer 与 CalamityEntropy（社区开源）
//   TlipocasScytheHeld 的通用管线：手持弹幕 + 缓动状态机 + 顶点扇形条带(TriangleStrip) +
//   Additive 发光混合 + 纹理映射 + GameViewMatrix.TransformationMatrix 视口变换 + 严格状态还原。
//   本实现为自主编写；着色用 FNA 内置 BasicEffect（TextureEnabled + 顶点色）替代自定义 HLSL——
//   因 tModLoader 不会把 .fx 编译进包（需预编译 .xnb 工具链，另行自建）。
// - 挥动贴图：用户自绘 MeleeSlash.png（圆心=最左像素）。
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
    /// 近战形态·斩击（v6.0 重写）：按 CWR/熵 通用管线重做刀光。
    /// - 运动：爆发曲线缓动推进 θ（缓动状态机）；
    /// - 刀光：每帧把挥扫历史角度采样成 内缘/外缘 双轨顶点，构建 TriangleStrip 扇形网格；
    /// - 渲染：End → Immediate + Additive → DrawUserPrimitives → 严格还原(Deferred/AlphaBlend/CullCounterClockwise)；
    /// - 矩阵：顶点先乘 Main.GameViewMatrix.ZoomMatrix（任意缩放不错位），再正交投影；
    /// - 颜色：顶点色渐变（尾→头 tint→白热；内缘暗→外缘白热芯），零每帧分配（静态数组/BasicEffect）。
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

        // ---- 常驻资源（避免每帧分配，指南规则2）----
        private static BasicEffect _fanFx;
        private static readonly VertexPositionColorTexture[] _fanVerts = new VertexPositionColorTexture[66]; // 32 切片 × 2

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

        private static void DrawSeg(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 1f || width < 0.5f) return;
            Main.spriteBatch.Draw(AdditiveLayer.Pixel, mid, null, color, d.ToRotation(),
                new Vector2(0.5f, 0.5f), new Vector2(len, Math.Max(1f, width)), SpriteEffects.None, 0f);
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
            try { DrawCore(ref lightColor); } catch { /* 任何绘制异常都不允许漏到引擎 */ }
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

            Vector2 jit = _shakeT > 0f ? Main.rand.NextVector2Circular(_shakeT * 0.30f, _shakeT * 0.30f) : Vector2.Zero;
            Vector2 oc = owner.Center + jit;
            Vector2 pivot = oc - Main.screenPosition;
            float holdFade = _age > SweepEnd
                ? MathHelper.Lerp(1f, 0.5f, (_age - SweepEnd) / (float)Math.Max(1, MalachiteMelee.SwingHoldFrames + ExtraHold))
                : 1f;

            // ① 绘制底层扇形刀光网格（执行完毕后 SpriteBatch 会保持在 Additive 状态）
            DrawFanStrip(oc, tint, holdFade);

            Vector2 curScale = new Vector2(_sx[n - 1], _sy[n - 1]) * _sc[n - 1];

            // ② 绘制刃身贴图（均处于 Additive 发光混合下）
            // 刃身外晕
            Main.spriteBatch.Draw(tex, pivot, null, tint * (0.40f * MalachiteMelee.SlashArtAlpha * holdFade),
                _th[n - 1], origin, curScale * 1.25f, SpriteEffects.None, 0f);
            // 刃身本体
            Main.spriteBatch.Draw(tex, pivot, null,
                tint * (MalachiteMelee.SlashArtBrightness * MalachiteMelee.SlashArtAlpha * holdFade),
                _th[n - 1], origin, curScale, SpriteEffects.None, 0f);
            // 白热芯（高亮居合切痕）
            Main.spriteBatch.Draw(tex, pivot, null, Color.White * (0.80f * MalachiteMelee.SlashArtAlpha * holdFade),
                _th[n - 1], origin, curScale * 0.90f, SpriteEffects.None, 0f);

            // ③ 击中反馈环与划痕
            if (_hitFx > 0)
            {
                float t = _hitFx / (float)MalachiteMelee.HitFlashFrames;
                float ringR = MalachiteMelee.HitRingMaxR * KindRingK() * (1f - t) + 5f;
                Color rc = Color.Lerp(Color.White, tint, 0.4f) * (0.9f * t);
                for (int s = 0; s < 14; s++)
                {
                    float a0 = MathHelper.TwoPi * s / 14f;
                    float a1 = MathHelper.TwoPi * (s + 1) / 14f;
                    DrawSeg(_hitPos + jit + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * ringR,
                            _hitPos + jit + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * ringR, 2.5f, rc);
                }
                Vector2 hd = _hitDir;
                DrawSeg(_hitPos + jit - hd * (MalachiteMelee.HitSlashLen * 0.55f),
                        _hitPos + jit + hd * (MalachiteMelee.HitSlashLen * 0.45f), 3.5f, Color.White * (0.85f * t));
            }

            // 严格还原为默认的 AlphaBlend 批次状态，避免污染游戏后续绘制
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 刀光扇形网格：32段平滑历史插值采样 → 纹理映射 → 正确矩阵投影与 Additive 混合
        /// （顶点=world−screen 坐标；View 由 GameViewMatrix.TransformationMatrix 统一接管，不再手动乘 ZoomMatrix）
        /// </summary>
        private void DrawFanStrip(Vector2 oc, Color tint, float holdFade)
        {
            int n = _th.Count;
            if (n < 2) return;

            // 固定采样 32 个细分段（共 64 个顶点），将离散历史点平滑化
            const int subdivisions = 32;

            for (int i = 0; i < subdivisions; i++)
            {
                // u: 0.0 (尾部消散端) -> 1.0 (刀刃当前端)
                float u = i / (float)(subdivisions - 1);

                // 在历史记录中进行线性插值，彻底消除折线感
                float sampleIdx = u * (n - 1);
                int idx0 = (int)sampleIdx;
                int idx1 = Math.Min(idx0 + 1, n - 1);
                float frac = sampleIdx - idx0;

                float th = MathHelper.Lerp(_th[idx0], _th[idx1], frac);
                float sc = MathHelper.Lerp(_sc[idx0], _sc[idx1], frac);
                float sx = MathHelper.Lerp(_sx[idx0], _sx[idx1], frac);

                float r = sc * MalachiteMelee.SlashArtWidth * sx;
                Vector2 dd = new Vector2((float)Math.Cos(th), (float)Math.Sin(th));

                float headK = Math.Clamp(u * 1.35f, 0f, 1f);
                float tailFade = (0.05f + 0.95f * (float)Math.Pow(u, 1.5f)) * holdFade;

                Color colIn = Color.Lerp(tint, Color.White, headK) * (tailFade * 0.4f);
                Color colOut = Color.Lerp(tint, Color.White, Math.Clamp(u * 1.6f, 0f, 1f)) * tailFade;
                if (i == subdivisions - 1) colOut = Color.White * tailFade;
                if (_flash > 0) { colIn = Color.Lerp(colIn, Color.White, 0.4f); colOut = Color.Lerp(colOut, Color.White, 0.6f); }

                // 顶点采用相对屏幕坐标（严禁手动乘 ZoomMatrix）
                Vector2 pIn = oc + dd * (r * MalachiteMelee.PathInnerK) - Main.screenPosition;
                Vector2 pOut = oc + dd * r - Main.screenPosition;

                _fanVerts[i * 2] = new VertexPositionColorTexture(new Vector3(pIn, 0f), colIn, new Vector2(u, 0f));
                _fanVerts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(pOut, 0f), colOut, new Vector2(u, 1f));
            }

            // 挂起当前的 SpriteBatch 批次
            Main.spriteBatch.End();

            // 初始化或配置 BasicEffect
            if (_fanFx == null)
            {
                _fanFx = new BasicEffect(Main.graphics.GraphicsDevice)
                {
                    VertexColorEnabled = true,
                    TextureEnabled = true, // 启用贴图
                    LightingEnabled = false,
                    FogEnabled = false
                };
            }

            // 正确矩阵赋值：View 统一由 TransformationMatrix 接管
            _fanFx.World = Matrix.Identity;
            _fanFx.View = Main.GameViewMatrix.TransformationMatrix;
            _fanFx.Projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, 0f, 1f);

            // 贴图绑定：优先专用刀光流线贴图，否则回退本模组 1x1 白（弹幕禁用原版 MagicPixel，见 R6）
            Texture2D trailTex = ModContent.HasAsset("可成长的孔雀翎/Textures/SlashTrail")
                ? ModContent.Request<Texture2D>("可成长的孔雀翎/Textures/SlashTrail").Value
                : AdditiveLayer.Pixel;
            _fanFx.Texture = trailTex;

            // 硬件渲染状态：Additive 发光
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Main.graphics.GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            Main.graphics.GraphicsDevice.DepthStencilState = DepthStencilState.None;
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            foreach (EffectPass pass in _fanFx.CurrentTechnique.Passes) pass.Apply();

            int vertexCount = subdivisions * 2;
            int primitiveCount = vertexCount - 2;
            Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, _fanVerts, 0, primitiveCount);

            // 重新开启 SpriteBatch，直接进入 Additive 混合模式以配合后续发光层绘制
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}