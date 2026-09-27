// 本文件为自主实现，无外部参考代码。
//
// ============================================================================
// 星图 3.0 ·「翎羽星网」—— 界面层
// ============================================================================
// 设计依据：写法\阶段4_星图3.0_翎羽星网.md（D23~D28）
// 布局与动效参数来源：AGY 咨询稿（写法\_agy\reply_01~03）。
//   **配色优先级（2026-09-27 用户拍板）**：与既有调色板冲突时以 AGY 为准；
//   AGY 的色值已收编进 MalachitePalette（StarCardBg / StarCardBorder / StarInk …）。
//
// 与旧版（阶段 2）的关键差别：
//   1. **单页**：取消「力/技」翻页，五轨 + 纹章节点全部并入一张星网。
//   2. **命中判定取消兜底热区**：旧版有 96px 的"轨道大圆"兜底，会把中心区点击全吃掉；
//      本版只有"最近节点 + 18px 半径"一条路径。
//   3. **加点有反馈**：流光沿最短路径从翎心涌向新节点 → 落点爆闪 → 音效（旧版改完数字直接重建 UI 树）。
//   4. **指针坐标空间**：`Main.MouseScreen` 与 `UIElement.GetDimensions()` **本就是同一空间**，直接相减。
//      ⚠ 不要"自作聪明"再除 `Main.UIScale` —— 2026-09-27 我这么干过，实机表现是
//      "鼠标离节点很远才选中、偏移随屏幕坐标线性放大"。证据与推理链见 `MouseUi()` 的注释。

using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using ReLogic.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>细细的分隔线（详情卡用）。</summary>
    internal class HairLine : UIElement
    {
        public Color LineColor = Color.White;

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            CalculatedStyle d = GetDimensions();
            spriteBatch.Draw(AdditiveLayer.Pixel,
                new Rectangle((int)d.X, (int)d.Y, Math.Max(1, (int)d.Width), Math.Max(1, (int)d.Height)),
                LineColor);
        }
    }

    /// <summary>
    /// 星图入口纹章（常驻旋转）。手持孔雀柳刃时才显示，悬停平滑放大提亮。
    /// <para/>批次纪律：纹章是加色发光结构，tML UI 默认批次是 AlphaBlend，不换批次会整体发暗。
    /// </summary>
    public class SigilEntryElement : UIElement
    {
        private const float BaseScale = 0.30f;

        private bool _hover;
        private float _hoverK;

        public void SetHover(bool on) => _hover = on;

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            // UI 绘制异常若漏到 tML 的 UI 系统会直接崩游戏，按项目惯例（MeleeSlashProj.PreDraw）兜住。
            try
            {
                CalculatedStyle dim = GetDimensions();
                Vector2 center = dim.Center();
                _hoverK = MathHelper.Lerp(_hoverK, _hover ? 1f : 0f, 0.18f);

                Texture2D flare = TextureAssets.Extra[ExtrasID.SharpTears].Value;
                Texture2D bloom = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;
                Texture2D pixel = AdditiveLayer.Pixel;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

                VfxDraw.DrawIdleSigil(spriteBatch, flare, bloom, pixel, center,
                    Main.GameUpdateCount, BaseScale * (dim.Width / 72f), _hoverK,
                    MalachitePalette.GreenBright, MalachitePalette.AccentGold, MalachitePalette.AccentCyan);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
            }
            catch
            {
                try
                {
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                        DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
                }
                catch { /* 已尽力 */ }
            }
        }
    }

    /// <summary>
    /// 星网画布元素：把绘制完全委托给 <see cref="StarWebVfx"/>（几何渲染），
    /// 本类只负责"算坐标 / 传状态"。
    /// </summary>
    public class StarWebElement : UIElement
    {
        public const float CanvasSize = StarWebVfx.CanvasSize;

        /// <summary>
        /// 星网中心在本元素内的局部坐标。
        /// <para/>★ 2026-09-27 起**不再用画布几何中心**，而是由"墨迹包围盒"反算：
        /// 五臂角度已按用户要求**刻意不等距**（去对称化，见 `StarWebLayout.ArmDeg`），
        /// 加上每节点的确定性抖动，墨迹中心离几何中心有偏移。
        /// 实测包围盒 `x[-0.887..0.997] y[-0.978..0.841]` → 墨迹中心 `(0.055, -0.069)`，
        /// <para/>⚠ **节点视觉外延左右不对称**（实测：右 28.5px / 左 14.2px），所以不能按"居中"取 162，
/// 而要按外延反解：`cX ∈ [14.2 + 0.887R, 310.5 − 0.997R]` → R=152 时 [149, 159]，取 154（左右各留 5px）。
/// 纵向同理：`cY ∈ [166, 185]`，取 180。**改动节点表后必须重跑 STARWEB_BBOX/EXTREMES 并复核这里。**
        /// <para/>量测方式：跑一次离屏量测，读它打印的包围盒（`STARWEB_BBOX`）一行。
        /// **改动节点表的角度/半径后必须重跑一次并更新这里**，否则星网会偏出画布。
        /// </summary>
        public static readonly Vector2 LocalCenter = StarWebVfx.CanvasCenter;

        /// <summary>
        /// 渲染半径（px）：归一化 1.0 对应多少像素。
        /// <para/>取 152 由包围盒半跨度反算：半跨度 (0.942, 0.909)，加节点视觉外延 ≈20px，
        /// 需满足 `半跨度·R + 20 ≤ 170` → R ≤ 159；取 152 留出余量。
        /// 实测四边留白：上 11.3 / 下 12.2 / 左 7.2 / 右 6.5 px，全部落在 340×340 画布内。
        /// </summary>
        public const float RenderRadius = StarWebVfx.CanvasRenderRadius;

        /// <summary>已点亮集合（每帧由 UIState 注入）。</summary>
        public ICollection<string> Lit;

        /// <summary>可购买集合（每帧由 UIState 注入）。</summary>
        public ICollection<string> Purchasable;

        /// <summary>悬停节点 id。</summary>
        public string HoverId;

        /// <summary>加点流光状态（可 null）。</summary>
        public StarSweepState Sweep;

        public StarWebElement()
        {
            Width.Set(CanvasSize, 0f);
            Height.Set(CanvasSize, 0f);
        }

        /// <summary>星图中心在屏幕（UI 像素）坐标中的位置。</summary>
        public Vector2 ScreenCenter()
        {
            CalculatedStyle dim = GetDimensions();
            return new Vector2(dim.X, dim.Y) + LocalCenter;
        }

        /// <summary>鼠标（UI 像素）→ 本元素局部坐标。</summary>
        public Vector2 ToLocal(Vector2 mouseUi)
        {
            CalculatedStyle dim = GetDimensions();
            return mouseUi - new Vector2(dim.X, dim.Y);
        }

        /// <summary>命中判定（转发给 StarWebVfx，保证"看到的"与"点到的"用同一份坐标）。</summary>
        public string HitTest(Vector2 mouseUi)
            => StarWebVfx.HitTest(LocalCenter, RenderRadius, ToLocal(mouseUi));

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            try
            {
                StarWebVfx.Draw(spriteBatch, spriteBatch.GraphicsDevice, ScreenCenter(), RenderRadius,
                    Main.GameUpdateCount, Lit, Purchasable, HoverId, Main.UIScaleMatrix, Sweep);
            }
            catch
            {
                // 绘制异常漏出去会崩游戏；同时必须保证批次被还原成 Additive，
                // 否则同层的纹章入口会整体发暗（历史事故）。
                try
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                }
                catch { }
            }
        }
    }

    /// <summary>星图 3.0 面板（单页星网）。</summary>
    public class StarMapUIState : UIState
    {
        // ==================== 布局常量（AGY §4.4，面板绝对坐标）====================
        private const float PanelW = 700f, PanelH = 500f;
        private const float TitleX = 30f, TitleY = 20f;
        private const float StatusX = 150f, StatusY = 20f, StatusW = 480f;
        private const float CloseX = 650f, CloseY = 20f, CloseSize = 24f;
        private const float CanvasX = 40f, CanvasY = 90f;
        private const float RespecX = 110f, RespecY = 446f, RespecW = 200f, RespecH = 34f;
        private const float DetailX = 410f, DetailY = 70f, DetailW = 266f, DetailH = 370f;
        private const float NoticeX = 410f, NoticeY = 446f, NoticeW = 266f, NoticeH = 34f;
        private const float Pad = 16f;

        // ==================== 状态 ====================
        public bool IsVisible;

        private UIPanel _mainPanel;
        private UIPanel _detailCard;
        private StarWebElement _web;
        private SigilEntryElement _entryButton;
        private UIText _statusText;
        private UIText _noticeText;

        private string _hoverId;
        private string _detailFor;      // 详情卡当前渲染的节点（避免每帧重建 UI 树）
        private string _notice = "";
        private bool _noticeIsError;
        private int _noticeTimer;

        private bool _prevLeftDown, _prevRightDown, _prevInventoryKey;

        /// <summary>每帧算出的"可购买"集合（相邻 + 点数够 + 门槛达成）。</summary>
        private readonly HashSet<string> _purchasable = new HashSet<string>();

        // ---- 加点流光 ----
        private readonly StarSweepState _sweep = new StarSweepState();
        private float _sweepTotalPx;
        private bool _sweepBurstFired;

        public StarMapUIState() { }

        // ==================== 初始化：左下角入口纹章 ====================

        public override void OnInitialize()
        {
            _entryButton = new SigilEntryElement();
            _entryButton.Left.Set(20, 0f);
            _entryButton.Top.Set(-96, 1f);
            _entryButton.Width.Set(72, 0f);
            _entryButton.Height.Set(72, 0f);

            _entryButton.OnMouseOver += (evt, element) =>
            {
                _entryButton.SetHover(true);
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            _entryButton.OnMouseOut += (evt, element) => _entryButton.SetHover(false);
            _entryButton.OnLeftClick += (evt, element) =>
            {
                SoundEngine.PlaySound(IsVisible ? SoundID.MenuClose : SoundID.MenuOpen);
                if (IsVisible) HidePanel();
                else OpenPanel();
            };

            Append(_entryButton);
        }

        // ==================== 面板开关 ====================

        public void OpenPanel()
        {
            if (_mainPanel != null) RemoveChild(_mainPanel);
            IsVisible = true;
            _notice = "";
            _noticeTimer = 0;
            _detailFor = null;
            _hoverId = null;
            _sweep.Reset();

            _mainPanel = new UIPanel();
            _mainPanel.Width.Set(PanelW, 0f);
            _mainPanel.Height.Set(PanelH, 0f);
            _mainPanel.HAlign = 0.5f;
            _mainPanel.VAlign = 0.5f;
            _mainPanel.BackgroundColor = MalachitePalette.StarCardBg;
            _mainPanel.BorderColor = MalachitePalette.StarCardBorder;
            _mainPanel.SetPadding(0);

            // —— 标题 ——
            UIText title = MakeText(MalachiteData.Loc("翎 羽 星 网", "STAR WEB"), 1.1f, MalachitePalette.GreenBright);
            title.Left.Set(TitleX, 0f);
            title.Top.Set(TitleY, 0f);
            _mainPanel.Append(title);

            // —— 顶部状态条 ——
            _statusText = MakeText("", 0.86f, MalachitePalette.StarInk);
            _statusText.Left.Set(StatusX, 0f);
            _statusText.Top.Set(StatusY + 4f, 0f);
            _statusText.Width.Set(StatusW, 0f);
            _mainPanel.Append(_statusText);

            // —— 关闭按钮 ——
            UIPanel closeBtn = MakeButton("✕", 0.85f, MalachitePalette.StarInk, () =>
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                HidePanel();
            });
            closeBtn.Width.Set(CloseSize, 0f);
            closeBtn.Height.Set(CloseSize, 0f);
            closeBtn.Left.Set(CloseX, 0f);
            closeBtn.Top.Set(CloseY, 0f);
            _mainPanel.Append(closeBtn);

            // —— 星网画布 ——
            _web = new StarWebElement();
            _web.Left.Set(CanvasX, 0f);
            _web.Top.Set(CanvasY, 0f);
            _mainPanel.Append(_web);

            // —— 右侧详情卡 ——
            _detailCard = new UIPanel();
            _detailCard.Width.Set(DetailW, 0f);
            _detailCard.Height.Set(DetailH, 0f);
            _detailCard.Left.Set(DetailX, 0f);
            _detailCard.Top.Set(DetailY, 0f);
            _detailCard.BackgroundColor = MalachitePalette.StarCardBg;
            _detailCard.BorderColor = MalachitePalette.StarCardBorder;
            _detailCard.SetPadding(0);
            _mainPanel.Append(_detailCard);

            // —— 一键洗点 ——
            UIPanel respec = MakeButton(MalachiteData.Loc("一键洗点（全额退还）", "Respec All (full refund)"),
                0.80f, MalachitePalette.AccentGold, () =>
                {
                    var mp = LocalMp();
                    if (mp == null) return;
                    if (mp.StarNodes.Count <= 1)
                    {
                        SetNotice(MalachiteData.Loc("没有可退还的星。", "Nothing to refund."), true);
                        return;
                    }
                    int back = mp.RespecAllStars();
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    SetNotice(MalachiteData.Loc($"已洗点，退还 {back} 点技能点。", $"Respec complete. Refunded {back} points."), false);
                    RefreshDetail(force: true);
                });
            respec.Width.Set(RespecW, 0f);
            respec.Height.Set(RespecH, 0f);
            respec.Left.Set(RespecX, 0f);
            respec.Top.Set(RespecY, 0f);
            _mainPanel.Append(respec);

            // —— 底部反馈行 ——
            _noticeText = MakeText("", 0.74f, MalachitePalette.StarInkDim);
            _noticeText.Left.Set(NoticeX, 0f);
            _noticeText.Top.Set(NoticeY + 6f, 0f);
            _noticeText.Width.Set(NoticeW, 0f);
            _mainPanel.Append(_noticeText);

            Append(_mainPanel);
            RefreshDetail(force: true);
            Recalculate();
        }

        private void HidePanel()
        {
            if (_mainPanel != null)
            {
                RemoveChild(_mainPanel);
                _mainPanel = null;
            }
            _web = null;
            _detailCard = null;
            _statusText = null;
            _noticeText = null;
            _detailFor = null;
            _hoverId = null;
            _sweep.Reset();
            IsVisible = false;
            Recalculate();
        }

        // ==================== 每帧 ====================

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Main.gameMenu) return;

            Player player = Main.LocalPlayer;
            bool isHolding = player != null && player.active && !player.dead
                             && MalachiteCache.IsMalachiteItem(player.HeldItem);

            // 入口纹章：手持孔雀柳刃才显示
            if (isHolding && _entryButton != null && _entryButton.Parent == null) Append(_entryButton);
            else if (!isHolding && _entryButton != null && _entryButton.Parent != null) RemoveChild(_entryButton);

            // 悬停态校正：鼠标快速移出、或元素被移出树时 OnMouseOut 不一定触发，
            // 不校正会让纹章卡在"放大提亮"态。
            if (_entryButton != null && _entryButton.Parent != null)
                _entryButton.SetHover(_entryButton.IsMouseHovering);
            else
                _entryButton?.SetHover(false);

            if (!isHolding && IsVisible)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                HidePanel();
            }

            if (!IsVisible || player == null) return;

            var mp = player.GetModPlayer<MalachitePlayer>();

            if (_noticeTimer > 0 && --_noticeTimer == 0 && !string.IsNullOrEmpty(_notice))
            {
                _notice = "";
                if (_noticeText != null) _noticeText.SetText("");
            }

            RecomputeState(mp);
            AdvanceSweep();

            if (_web != null)
            {
                _web.Lit = mp.StarNodes;
                _web.Purchasable = _purchasable;
                _web.HoverId = _hoverId;
                _web.Sweep = _sweep.Active ? _sweep : null;

                string hover = _web.HitTest(MouseUi());
                if (hover != _hoverId)
                {
                    _hoverId = hover;
                    RefreshDetail(force: false);
                }
            }

            HandleClicks(mp);

            // 库存键边沿 = 关闭请求（沿用项目 UI 习惯：不吞原版输入，仅收起本面板）
            bool invNow = player.controlInv;
            if (invNow && !_prevInventoryKey)
            {
                _prevInventoryKey = invNow;
                HidePanel();
                return;
            }
            _prevInventoryKey = invNow;
        }

        /// <summary>算"可购买"集合 + 刷新状态条文案。</summary>
        private void RecomputeState(MalachitePlayer mp)
        {
            _purchasable.Clear();
            if (mp == null) return;

            foreach (StarNode n in StarNetwork.Nodes_)
            {
                if (StarNetwork.CanPurchase(mp.StarNodes, n.Id, mp.SkillPoints, out _) == StarBlock.None)
                    _purchasable.Add(n.Id);
            }

            if (_statusText != null)
            {
                int stage = Math.Clamp(ProgressSystem.GetStage(), 0, MalachiteData.StageInfos.Length - 1);
                string stageTitle = MalachiteData.StageInfos[stage].Title;
                int lit = Math.Max(0, mp.StarNodes.Count);
                _statusText.SetText(MalachiteData.Loc(
                    $"技能点 {mp.SkillPoints}     已点亮 {lit}/{StarNetwork.TotalNodes}     阶段 {stage} · {stageTitle}",
                    $"Points {mp.SkillPoints}     Lit {lit}/{StarNetwork.TotalNodes}     Stage {stage} · {stageTitle}"));
                _statusText.TextColor = mp.SkillPoints > 0 ? MalachitePalette.AccentGold : MalachitePalette.StarInkDim;
            }

            if (_noticeText != null)
                _noticeText.SetText(_notice);
        }

        // ==================== 加点 / 退还 ====================

        private void HandleClicks(MalachitePlayer mp)
        {
            bool leftNow = Main.mouseLeft;
            bool rightNow = Main.mouseRight;
            bool leftEdge = leftNow && !_prevLeftDown;
            bool rightEdge = rightNow && !_prevRightDown;

            if ((leftEdge || rightEdge) && _hoverId != null && mp != null)
            {
                if (leftEdge) TryPurchase(mp, _hoverId);
                else TryRefund(mp, _hoverId);
            }

            _prevLeftDown = leftNow;
            _prevRightDown = rightNow;
        }

        private void TryPurchase(MalachitePlayer mp, string nodeId)
        {
            StarNode n = StarNetwork.Get(nodeId);
            if (n == null) return;

            StarBlock block = StarNetwork.CanPurchase(mp.StarNodes, nodeId, mp.SkillPoints, out string reason);
            if (block != StarBlock.None)
            {
                SetNotice(reason, block != StarBlock.AlreadyLit);
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f, Pitch = -0.3f });
                return;
            }

            if (!mp.PurchaseStar(nodeId)) return;

            // —— 加点令牌 + 流光 ——
            _sweep.Reset();
            _sweep.Path = StarNetwork.PathFromRoot(mp.StarNodes, nodeId);
            _sweep.IsNucleus = n.Kind == StarKind.Nucleus;
            _sweep.BurstPos = n.Pos;
            _sweep.Active = _sweep.Path.Count >= 2;
            _sweepBurstFired = false;

            _sweepTotalPx = 0f;
            for (int i = 0; i + 1 < _sweep.Path.Count; i++)
                _sweepTotalPx += Vector2.Distance(_sweep.Path[i], _sweep.Path[i + 1]) * StarWebElement.RenderRadius;

            if (!_sweep.Active)
            {
                // 路径异常（理论上不会发生）也要给反馈，不能"点了没反应"
                _sweep.BurstTimer = StarWebVfx.BurstFrames;
            }

            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = 0.30f });
            SetNotice(MalachiteData.Loc($"点亮了「{StarNetwork.NameOf(n)}」。", $"Lit 「{StarNetwork.NameOf(n)}」."), false);
            RefreshDetail(force: true);
        }

        private void TryRefund(MalachitePlayer mp, string nodeId)
        {
            StarNode n = StarNetwork.Get(nodeId);
            if (n == null) return;

            if (nodeId == StarNetwork.RootId)
            {
                SetNotice(MalachiteData.Loc("翎心是星网的根，无法退还。", "The Heart is the root of the web; it cannot be refunded."), true);
                return;
            }
            if (!mp.StarNodes.Contains(nodeId))
            {
                SetNotice(MalachiteData.Loc("该星尚未点亮。", "That star is not lit."), true);
                return;
            }
            if (!StarNetwork.CanRefund(mp.StarNodes, nodeId))
            {
                SetNotice(MalachiteData.Loc("退还后会让其它星失去通路，先退外圈的星。",
                    "Refunding would orphan other stars. Refund the outer ones first."), true);
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f, Pitch = -0.3f });
                return;
            }

            if (!mp.RefundStar(nodeId)) return;
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f, Pitch = 0.1f });
            SetNotice(MalachiteData.Loc($"退还了「{StarNetwork.NameOf(n)}」（+{n.Cost} 点）。", $"Refunded 「{StarNetwork.NameOf(n)}」 (+{n.Cost})."), false);
            RefreshDetail(force: true);
        }

        /// <summary>推进加点流光：彗头前进 → 到头触发落点爆闪 → 爆闪结束收尾。</summary>
        private void AdvanceSweep()
        {
            if (!_sweep.Active) return;

            if (_sweep.TraveledPx < _sweepTotalPx)
            {
                _sweep.TraveledPx += StarWebVfx.SweepSpeedPx;
                if (_sweep.TraveledPx >= _sweepTotalPx && !_sweepBurstFired)
                {
                    _sweepBurstFired = true;
                    _sweep.BurstTimer = StarWebVfx.BurstFrames;
                    // 落点清脆铃音；星核额外叠一层低音轰鸣 + 微震屏（规格 §4.3）
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.60f, Pitch = 0.45f });
                    if (_sweep.IsNucleus)
                    {
                        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = -0.25f });
                        ScreenShakeSystem.Shake(2f);
                    }
                }
            }
            else if (_sweep.BurstTimer > 0)
            {
                _sweep.BurstTimer--;
                if (_sweep.BurstTimer <= 0) _sweep.Reset();
            }
            else
            {
                _sweep.Reset();
            }
        }

        // ==================== 详情卡 ====================

        private void RefreshDetail(bool force)
        {
            if (_detailCard == null) return;

            string id = _hoverId ?? StarNetwork.RootId;
            if (!force && id == _detailFor) return;
            _detailFor = id;

            _detailCard.RemoveAllChildren();

            var mp = LocalMp();
            StarNode n = StarNetwork.Get(id);
            if (n == null || mp == null) return;

            bool isLit = mp.StarNodes.Contains(id);
            bool canBuy = _purchasable.Contains(id);

            float y = Pad;

            // —— 节点名 ——
            Color nameColor = n.Kind == StarKind.Nucleus ? MalachitePalette.AccentGold : MalachitePalette.GreenBright;
            if (!isLit && !canBuy) nameColor = MalachitePalette.StarInkDim;
            AddCardText(StarNetwork.NameOf(n), 1.05f, nameColor, y); y += 26f;

            // —— 类型标签 + 消耗 ——
            string kindZh, kindEn;
            switch (n.Kind)
            {
                case StarKind.Root: kindZh = "翎心 · 星网之根"; kindEn = "Heart · Root of the Web"; break;
                case StarKind.Dust: kindZh = "星尘 · 属性"; kindEn = "Stardust · Attribute"; break;
                case StarKind.Asterism: kindZh = "星宿 · 质变"; kindEn = "Asterism · Quality"; break;
                default: kindZh = "星核 · 流派核心"; kindEn = "Nucleus · Archetype Core"; break;
            }
            AddCardText(MalachiteData.Loc(kindZh, kindEn), 0.72f, MalachitePalette.AccentCyan, y); y += 20f;

            string costText = n.Cost <= 0
                ? MalachiteData.Loc("消耗：无（初始点亮）", "Cost: none (lit from the start)")
                : MalachiteData.Loc($"消耗：{n.Cost} 点", $"Cost: {n.Cost} point(s)");
            Color costColor = n.Cost <= 0 || mp.SkillPoints >= n.Cost
                ? MalachitePalette.AccentGold : MalachitePalette.DangerRed;
            AddCardText(costText, 0.84f, costColor, y); y += 24f;

            // —— 分隔线 ——
            var line = new HairLine { LineColor = MalachitePalette.StarCardBorderDim };
            line.Left.Set(Pad, 0f); line.Top.Set(y, 0f);
            line.Width.Set(DetailW - Pad * 2f, 0f); line.Height.Set(1f, 0f);
            _detailCard.Append(line);
            y += 12f;

            // —— 效果正文 ——
            // 2026-09-27：改为按**实测换行高度**推进 y，而不是写死 96px ——
            // 否则长文案换行后会盖住下面的"状态/前置"两行（用户反馈的溢出问题）。
            const float bodyW = DetailW - Pad * 2f;
            string body = StarNetwork.DescOf(n);
            AddCardText(body, 0.78f, MalachitePalette.StarInk, y, bodyW);
            y += MeasureTextHeight(body, bodyW, 0.78f) + 10f;

            // —— 状态 ——
            string statusZh, statusEn; Color statusColor;
            if (isLit) { statusZh = "已点亮"; statusEn = "LIT"; statusColor = MalachitePalette.PrimaryGreen; }
            else if (n.Gate != StarGate.None && !StarNetwork.RequirementMet(n))
            { statusZh = "尚未开放"; statusEn = "LOCKED"; statusColor = MalachitePalette.DangerRed; }
            else if (canBuy) { statusZh = "可点亮（左键）"; statusEn = "AVAILABLE (LMB)"; statusColor = MalachitePalette.AccentGold; }
            else if (!StarNetwork.HasLitNeighbor(mp.StarNodes, id))
            { statusZh = "需先点亮相邻的星"; statusEn = "Needs an adjacent star"; statusColor = MalachitePalette.StarInkDim; }
            else { statusZh = "技能点不足"; statusEn = "Not enough points"; statusColor = MalachitePalette.DangerRed; }

            AddCardText(MalachiteData.Loc(statusZh, statusEn), 0.80f, statusColor, y); y += 24f;

            // —— 前置条件提示 ——
            string hint = "";
            if (n.Gate != StarGate.None && !StarNetwork.RequirementMet(n))
                hint = StarNetwork.GateHint(n);
            else if (isLit && id != StarNetwork.RootId)
                hint = MalachiteData.Loc("右键退还该星（免费，全额返还）", "RMB to refund this star (free, full refund)");
            else if (!isLit && !StarNetwork.HasLitNeighbor(mp.StarNodes, id))
                hint = MalachiteData.Loc("连通规则：必须与已点亮的星有连线", "Must share an edge with a lit star");

            if (!string.IsNullOrEmpty(hint))
                AddCardText(hint, 0.70f, MalachitePalette.StarInkDim, y, DetailW - Pad * 2f);
        }

        private void AddCardText(string text, float scale, Color color, float top, float width = -1f)
        {
            if (string.IsNullOrEmpty(text)) return;
            float w = width > 0f ? width : DetailW - Pad * 2f;
            UIText t = MakeText(WrapText(text, w, scale), scale, color);
            t.Left.Set(Pad, 0f);
            t.Top.Set(top, 0f);
            t.Width.Set(w, 0f);
            _detailCard.Append(t);
        }

        /// <summary>
        /// 手动换行。
        /// <para/>★ 2026-09-27 用户反馈："节点的效果不要单行写完，现在字数多的节点的字数都远超出 UI 范围了，
        /// 为什么不做一个换行呢"。
        /// 为什么不能靠 UIText 自己换行：tML 的 UIText 按**空格**断词，而中文整段没有空格 → 一行直接顶出卡片。
        /// 所以这里按**字符**量宽手动插换行；英文优先在最近的空格处断，避免把单词劈开。
        /// </summary>
        private static string WrapText(string text, float maxWidth, float scale)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 1f) return text;

            ReLogic.Graphics.DynamicSpriteFont font = FontAssets.MouseText.Value;
            var sb = new System.Text.StringBuilder(text.Length + 16);
            float lineW = 0f;
            int lastSpace = -1;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (ch == '\n') { sb.Append(ch); lineW = 0f; lastSpace = -1; continue; }

                float chW = font.MeasureString(ch.ToString()).X * scale;

                if (lineW + chW > maxWidth)
                {
                    // 英文尽量在最近的空格处断行；中文没有空格就按字断
                    if (lastSpace >= 0 && sb.Length - lastSpace <= 16)
                    {
                        sb[lastSpace] = '\n';
                    }
                    else
                    {
                        while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;   // 行首不留空格
                        sb.Append('\n');
                    }
                    lineW = 0f;
                    lastSpace = -1;
                }

                if (ch == ' ') lastSpace = sb.Length;
                sb.Append(ch);
                lineW += chW;
            }
            return sb.ToString();
        }

        /// <summary>量一段文本在给定宽度/缩放下的像素高度（供卡片纵向排布用）。</summary>
        private static float MeasureTextHeight(string text, float maxWidth, float scale)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            ReLogic.Graphics.DynamicSpriteFont font = FontAssets.MouseText.Value;
            string wrapped = WrapText(text, maxWidth, scale);
            int lines = 1;
            foreach (char ch in wrapped) if (ch == '\n') lines++;
            return lines * font.LineSpacing * scale;
        }

        // ==================== 小工具 ====================

        private void SetNotice(string text, bool isError)
        {
            _notice = text ?? "";
            _noticeIsError = isError;
            _noticeTimer = 240;   // 4 秒后自动清空，避免旧提示常驻误导
            if (_noticeText != null)
            {
                _noticeText.SetText(_notice);
                _noticeText.TextColor = isError ? MalachitePalette.DangerRed : MalachitePalette.PrimaryGreen;
            }
        }

        private static MalachitePlayer LocalMp()
            => Main.LocalPlayer == null ? null : Main.LocalPlayer.GetModPlayer<MalachitePlayer>();

        /// <summary>
        /// 鼠标位置（UI 元素坐标系）。
        /// <para/>★ 2026-09-27 实测更正（我在这里犯过错，记下来免得再犯）：
        /// 我曾以为"UI 元素坐标是 UI 像素、上屏经 Main.UIScaleMatrix 放大，而 Main.MouseScreen 是屏幕像素"，
        /// 于是写了 <c>Main.MouseScreen / Main.UIScale</c> —— **这是错的**，实机表现为"鼠标离节点很远才选中"，
        /// 且偏移量随离屏幕原点距离线性放大（用户反馈"非常非常非常大"）。
        /// 正确约定（tModLoader 自带代码即为证）：
        /// <c>Main.mouseX/mouseY</c>（=<see cref="Main.MouseScreen"/>) 与 <c>UIElement</c> 的
        /// <c>GetDimensions()</c> **本来就是同一个坐标空间**，直接相减即可。
        /// 证据：<c>ModLoader/UI/UIFocusInputTextField.cs</c> 里
        /// <c>Vector2 MousePosition = new Vector2(Main.mouseX, Main.mouseY); if (!ContainsPoint(MousePosition) ...)</c>
        /// —— <c>ContainsPoint</c> 判定的就是元素自身尺寸，两者直接比较、没有任何缩放。
        /// </summary>
        private static Vector2 MouseUi() => Main.MouseScreen;

        /// <summary>左上对齐文本（TextOriginX/Y = 0，避免居中默认值干扰坐标布局）。</summary>
        private static UIText MakeText(string text, float scale, Color color)
        {
            UIText t = new UIText(text, scale);
            t.TextOriginX = 0f;
            t.TextOriginY = 0f;
            t.TextColor = color;
            return t;
        }

        /// <summary>面板按钮基座：默认 30x28，调用方可覆写；文字居中。</summary>
        private UIPanel MakeButton(string label, float textScale, Color labelColor, Action onClick)
        {
            UIPanel btn = new UIPanel();
            btn.Width.Set(30, 0f);
            btn.Height.Set(28, 0f);
            btn.BackgroundColor = MalachitePalette.GreenDeep;
            btn.BorderColor = MalachitePalette.StarCardBorder;
            btn.SetPadding(0);

            btn.OnMouseOver += (evt, element) =>
            {
                btn.BackgroundColor = MalachitePalette.GreenDark;
                btn.BorderColor = MalachitePalette.GreenBright;
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            btn.OnMouseOut += (evt, element) =>
            {
                btn.BackgroundColor = MalachitePalette.GreenDeep;
                btn.BorderColor = MalachitePalette.StarCardBorder;
            };
            btn.OnLeftClick += (evt, element) => onClick?.Invoke();

            UIText text = new UIText(label, textScale);
            text.HAlign = 0.5f;
            text.VAlign = 0.5f;
            text.TextOriginX = 0.5f;
            text.TextOriginY = 0.5f;
            text.TextColor = labelColor;
            btn.Append(text);
            return btn;
        }
    }
}
