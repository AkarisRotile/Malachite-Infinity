using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 星图页签（v2 翻页骨架，设计依据：写法\阶段2_天赋星图重设计.md §7.1）。
    /// 页 0 = 属性加点（五轨小节点）；页 1 = 技（大节点/攻击模式占位）。
    /// 加页流程：enum 增加成员 → 在 StarMapUIState 构造器注册构建委托，无需改既有页。
    /// </summary>
    public enum StarMapPage
    {
        Attributes = 0,
        Skills = 1
    }

    /// <summary>逐级小圆点（当前等级 / 上限点阵）。</summary>
    public class PipBarElement : UIElement
    {
        public int Value;
        public int Cap;

        private const int PipW = 8;
        private const int PipH = 14;
        private const int Step = 13; // 每颗步距（含间距）

        public PipBarElement(int value, int cap)
        {
            Value = value;
            Cap = Math.Max(0, cap);
            Width.Set(Cap * Step + 4, 0f);
            Height.Set(PipH + 4, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            CalculatedStyle dim = GetDimensions();
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;

            for (int i = 0; i < Cap; i++)
            {
                Color c = i < Value
                    ? (i == Value - 1 ? MalachitePalette.GreenBright : MalachitePalette.PrimaryGreen)
                    : MalachitePalette.GreenDeep;
                float x = dim.X + i * Step + 2f;
                float y = dim.Y + 2f;
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, PipW, PipH), c * 0.9f);
            }
        }
    }

    /// <summary>
    /// v2 天赋星图 UI（可翻页）。
    /// 旧 Sigil 星图（原 MalachiteUI.cs 内）已于 M3 整体退役，本类为替代实现；
    /// 对话/History UI 冻结不动，仅本星图 + MalachiteUISystem 接入。
    /// </summary>
    public class StarMapUIState : UIState
    {
        // ==================== 状态 ====================
        /// <summary>主面板是否展开。</summary>
        public bool IsVisible = false;

        /// <summary>当前页索引（0 起）。</summary>
        public int CurrentPageIndex => _pageIndex;

        /// <summary>总页数（由注册表推导，便于加页）。</summary>
        public int PageCount => _pageBuilders.Count;

        private int _pageIndex = 0;
        private UIPanel _mainPanel;
        private UIElement _pageHost;
        private UIPanel _entryButton;

        private readonly List<UIPanel> _tabButtons = new List<UIPanel>();
        private UIText _pageLabel;

        /// <summary>反馈行文案（加点失败原因等；成功操作时清空）。</summary>
        private string _notice = "";

        /// <summary>上一帧库存键状态（用于关闭请求边沿检测）。</summary>
        private bool _prevInventoryKey = false;

        // ==================== 翻页注册表（页 → 内容构建委托）====================
        private readonly Dictionary<int, Action<StarMapUIState>> _pageBuilders =
            new Dictionary<int, Action<StarMapUIState>>();

        // 页签文案（中/英）——「力/技」呼应：力=属性加点，技=技能占位
        private static readonly (string zh, string en)[] PageTitles =
        {
            ("力", "Power"),
            ("技", "Skill")
        };

        public StarMapUIState()
        {
            // 加页：在 StarMapPage 增加成员后，在此注册一个构建方法即可。
            _pageBuilders[(int)StarMapPage.Attributes] = (ui) => ui.BuildAttributesPage();
            _pageBuilders[(int)StarMapPage.Skills] = (ui) => ui.BuildSkillsPage();
        }

        // ==================== 初始化：左下角入口按钮 ====================

        public override void OnInitialize()
        {
            _entryButton = new UIPanel();
            _entryButton.Left.Set(20, 0f);
            _entryButton.Top.Set(-60, 1f); // 左下角悬浮入口
            _entryButton.Width.Set(130, 0f);
            _entryButton.Height.Set(40, 0f);
            _entryButton.BackgroundColor = MalachitePalette.GreenDark * 0.85f;
            _entryButton.BorderColor = MalachitePalette.PrimaryGreen;

            _entryButton.OnMouseOver += (evt, element) =>
            {
                _entryButton.BackgroundColor = MalachitePalette.GreenDeep * 0.95f;
                _entryButton.BorderColor = MalachitePalette.GreenBright;
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            _entryButton.OnMouseOut += (evt, element) =>
            {
                _entryButton.BackgroundColor = MalachitePalette.GreenDark * 0.85f;
                _entryButton.BorderColor = MalachitePalette.PrimaryGreen;
            };
            _entryButton.OnLeftClick += (evt, element) =>
            {
                SoundEngine.PlaySound(IsVisible ? SoundID.MenuClose : SoundID.MenuOpen);
                if (IsVisible) HidePanel();
                else OpenPanel();
            };

            UIText btnText = MakeCenteredText(MalachiteData.Loc("✦ 天赋星图", "✦ Talent Star Map"), 0.85f, MalachitePalette.TextLight);
            _entryButton.Append(btnText);
            Append(_entryButton);
        }

        // ==================== 翻页公开接口 ====================

        /// <summary>跳转到指定页（越界自动钳制；同页重进则刷新内容）。</summary>
        public void GoToPage(int pageIndex)
        {
            if (_pageBuilders.Count == 0) return;
            _pageIndex = Math.Clamp(pageIndex, 0, _pageBuilders.Count - 1);
            RefreshNavVisuals();
            RebuildCurrentPage();
        }

        /// <summary>下一页（到末页后不再前进）。</summary>
        public void NextPage() => GoToPage(_pageIndex + 1);

        /// <summary>上一页（到首页后不再后退）。</summary>
        public void PrevPage() => GoToPage(_pageIndex - 1);

        // ==================== 面板开关 ====================

        private void OpenPanel()
        {
            if (_mainPanel != null) RemoveChild(_mainPanel);
            IsVisible = true;
            _notice = "";

            _mainPanel = new UIPanel();
            _mainPanel.Width.Set(700, 0f);
            _mainPanel.Height.Set(500, 0f);
            _mainPanel.HAlign = 0.5f;
            _mainPanel.VAlign = 0.5f;
            _mainPanel.BackgroundColor = MalachitePalette.BackgroundDeep;
            _mainPanel.BorderColor = MalachitePalette.PrimaryGreen * 0.8f;
            _mainPanel.SetPadding(0);

            // —— 标题 ——
            UIText title = MakeCenteredText(MalachiteData.Loc("—— 天 赋 星 图 ——", "—— TALENT STAR MAP ——"), 1.1f, MalachitePalette.GreenBright);
            title.Top.Set(10, 0f);
            _mainPanel.Append(title);

            // —— 关闭按钮（右上角）——
            UIPanel closeBtn = MakeButton("✕", 0.85f, MalachitePalette.TextLight, () =>
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                HidePanel();
            });
            closeBtn.Left.Set(-38, 1f);
            closeBtn.Top.Set(10, 0f);
            _mainPanel.Append(closeBtn);

            // —— 顶栏页签（居中并排）——
            float total = PageTitles.Length * 140 - 10;
            for (int i = 0; i < PageTitles.Length; i++)
            {
                int idx = i;
                UIPanel tab = MakeButton(PageTitles[i].zh, 0.9f, MalachitePalette.TextLight, () =>
                {
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    GoToPage(idx);
                });
                tab.Width.Set(130, 0f);
                tab.Height.Set(28, 0f);
                tab.Left.Set(-total / 2f + idx * 140, 0.5f);
                tab.Top.Set(44, 0f);
                _mainPanel.Append(tab);
                _tabButtons.Add(tab);
            }

            // 说明：用户反馈 ◀/▶ 翻页箭头观感不佳已移除；
            // 翻页仍由顶栏页签触发（GoToPage），编程翻页接口 Next/Prev/GoToPage 保留给后续"技"页使用。
            // —— 页面内容挂载区 ——
            _pageHost = new UIElement();
            _pageHost.Left.Set(10, 0f);
            _pageHost.Top.Set(98, 0f);
            _pageHost.Width.Set(680, 0f);
            _pageHost.Height.Set(394, 0f);
            _mainPanel.Append(_pageHost);

            Append(_mainPanel);
            GoToPage(_pageIndex); // 恢复上次所在页并首建内容
            Recalculate();
        }

        private void HidePanel()
        {
            if (_mainPanel != null)
            {
                RemoveChild(_mainPanel);
                _mainPanel = null;
            }
            IsVisible = false;
            Recalculate();
        }

        // ==================== 页内容刷新 ====================

        private void RebuildCurrentPage()
        {
            if (_pageHost == null || _pageBuilders.Count == 0) return;
            _pageHost.RemoveAllChildren();
            _pageBuilders[_pageIndex](this);
            _pageHost.Recalculate();
        }

        private void RefreshNavVisuals()
        {
            if (_pageLabel != null)
                _pageLabel.SetText(MalachiteData.Loc(
                    $"{_pageIndex + 1} / {_pageBuilders.Count}  ·  {PageTitles[_pageIndex].zh}",
                    $"{_pageIndex + 1} / {_pageBuilders.Count}  ·  {PageTitles[_pageIndex].en}"));

            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool active = i == _pageIndex;
                _tabButtons[i].BackgroundColor = active ? MalachitePalette.GreenDark * 0.95f : MalachitePalette.BackgroundDeep;
                _tabButtons[i].BorderColor = active ? MalachitePalette.GreenBright : MalachitePalette.GreenDeep;
            }
        }

        // ==================== 页 0：属性加点 ====================

        private void BuildAttributesPage()
        {
            var mp = LocalMp();
            if (mp == null) return;
            int stage = Math.Clamp(ProgressSystem.GetStage(), 0, MalachiteData.StageInfos.Length - 1);
            var stageData = MalachiteData.StageInfos[stage];

            // 顶部：技能点余额 / 阶段
            UIText balance = MakeText(MalachiteData.Loc(
                $"技能点余额：{mp.SkillPoints}       阶段：{stage} · {stageData.Title}",
                $"Skill Points: {mp.SkillPoints}       Stage: {stage} · {stageData.Title}"), 1.0f,
                mp.SkillPoints > 0 ? MalachitePalette.AccentGold : MalachitePalette.TextDim);
            balance.Left.Set(2, 0f);
            balance.Top.Set(0, 0f);
            _pageHost.Append(balance);

            // 五行轨道
            int rowY = 40;
            const int rowH = 66;
            for (int i = 0; i < TalentCatalog.TrackCount; i++)
            {
                TrackKind kind = (TrackKind)i;
                int cur = mp.TrackLevel[i];
                int cap = TalentCatalog.LevelCap(kind, stage);
                int next = cur + 1;
                bool atCap = cur >= cap;
                bool affordable = !atCap && mp.SkillPoints >= TalentCatalog.CostToNext(kind, next);

                // 轨名
                UIText name = MakeText(TalentCatalog.DisplayName(kind), 0.95f, MalachitePalette.TextLight);
                name.Left.Set(0, 0f);
                name.Top.Set(rowY + 2, 0f);
                _pageHost.Append(name);

                // 点阵（当前级 / 上限）
                PipBarElement pips = new PipBarElement(cur, cap);
                pips.Left.Set(70, 0f);
                pips.Top.Set(rowY + 14, 0f);
                _pageHost.Append(pips);

                // 等级文本
                UIText level = MakeText($"{cur} / {cap}", 0.8f, MalachitePalette.TextDim);
                level.Left.Set(262, 0f);
                level.Top.Set(rowY + 12, 0f);
                _pageHost.Append(level);

                // 下一级成本（资金不足 → 红字；满级 → 灰）
                string costText;
                Color costColor;
                if (atCap)
                {
                    costText = MalachiteData.Loc("已满级", "MAX");
                    costColor = MalachitePalette.TextDim;
                }
                else
                {
                    int nextCost = TalentCatalog.CostToNext(kind, next);
                    costText = MalachiteData.Loc($"下1级 需 {nextCost} 点", $"Next: {nextCost} pts");
                    costColor = affordable ? MalachitePalette.AccentGold : MalachitePalette.DangerRed;
                }
                UIText cost = MakeText(costText, 0.8f, costColor);
                cost.Left.Set(322, 0f);
                cost.Top.Set(rowY + 12, 0f);
                _pageHost.Append(cost);

                // 减 / 加 按钮（洗点=减级并退点；资金不足/满级 → 置灰）
                UIPanel minus = MakeButton("−", 0.9f, MalachitePalette.TextLight, () => ModifyTrack(kind, add: false));
                minus.Left.Set(436, 0f);
                minus.Top.Set(rowY + 2, 0f);
                minus.BackgroundColor = cur > 0 ? MalachitePalette.GreenDark * 0.9f : MalachitePalette.BackgroundDeep;
                _pageHost.Append(minus);

                UIPanel plus = MakeButton("＋", 0.9f, MalachitePalette.TextLight, () => ModifyTrack(kind, add: true));
                plus.Left.Set(470, 0f);
                plus.Top.Set(rowY + 2, 0f);
                plus.BackgroundColor = affordable ? MalachitePalette.GreenDark * 0.9f : MalachitePalette.BackgroundDeep;
                _pageHost.Append(plus);

                // 每级效果描述（小字第二行）
                UIText effect = MakeText(TalentCatalog.EffectDescription(kind), 0.6f, MalachitePalette.TextDim);
                effect.Left.Set(70, 0f);
                effect.Top.Set(rowY + 38, 0f);
                effect.Width.Set(560, 0f);
                _pageHost.Append(effect);

                rowY += rowH;
            }

            // 底部提示/反馈行
            UIText notice = MakeText(
                string.IsNullOrEmpty(_notice)
                    ? MalachiteData.Loc("− 洗点（减级）免费并退还该级消耗 ｜ 并发弹幕轨单价更高 ｜ 总暴击>100% 时溢出按比例增幅终伤",
                        "- Respec (-) is free & refunds the level cost | Volley costs more | Crit overflow >100% boosts final damage")
                    : _notice, 0.78f,
                string.IsNullOrEmpty(_notice) ? MalachitePalette.TextDim : MalachitePalette.DangerRed);
            notice.Left.Set(2, 0f);
            notice.Top.Set(372, 0f);
            notice.Width.Set(676, 0f);
            _pageHost.Append(notice);
        }

        /// <summary>加点/洗点（本地即刻生效；MP 权威同步见 TODO）。</summary>
        private void ModifyTrack(TrackKind kind, bool add)
        {
            var mp = LocalMp();
            if (mp == null) return;
            int idx = (int)kind;
            int cur = mp.TrackLevel[idx];
            int stage = ProgressSystem.GetStage();
            int cap = TalentCatalog.LevelCap(kind, stage);

            if (add)
            {
                int next = cur + 1;
                if (!TalentCatalog.CanPurchase(kind, next, stage) || next > cap)
                {
                    _notice = MalachiteData.Loc("已达该轨当前阶段的等级上限。", "This track has reached the stage cap.");
                    RebuildCurrentPage();
                    return;
                }
                int cost = TalentCatalog.CostToNext(kind, next);
                if (mp.SkillPoints < cost)
                {
                    _notice = MalachiteData.Loc("技能点不足！", "Not enough skill points!");
                    RebuildCurrentPage();
                    return;
                }
                mp.SkillPoints -= cost;
                mp.TrackLevel[idx] = next;
                // TODO(MP 同步)：后续波次补服务端权威校验与发包（msgType=2：技能点/轨道等级），当前仅本地生效。
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else
            {
                if (cur <= 0) return;
                int refund = TalentCatalog.CostToNext(kind, cur); // 退回升到当前级所消耗的点数
                mp.SkillPoints += refund;
                mp.TrackLevel[idx] = cur - 1;
                // TODO(MP 同步)：同上 —— 洗点同样需在权威波次做校验与发包。
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            _notice = "";
            RebuildCurrentPage();
        }

        // ==================== 页 1：技（占位） ====================

        private void BuildSkillsPage()
        {
            UIText title = MakeCenteredText(MalachiteData.Loc("【技】大节点 · 攻击模式", "[Skills] Major Nodes · Attack Modes"), 1.0f, MalachitePalette.TextDim);
            title.Top.Set(130, 0f);
            _pageHost.Append(title);

            UIText body = MakeCenteredText(MalachiteData.Loc("待开放：大节点技能 / 攻击模式将在后续版本解锁。",
                "Coming soon: major node skills & attack modes unlock in a later version."), 0.95f, MalachitePalette.TextDim);
            body.Top.Set(180, 0f);
            _pageHost.Append(body);

            UIText hint = MakeCenteredText(MalachiteData.Loc("（本页为预留区域，后续只做注册式加页，不改既有结构）",
                "(Reserved area; future pages will be added via registration only.)"), 0.7f, MalachitePalette.TextDim * 0.7f);
            hint.Top.Set(240, 0f);
            _pageHost.Append(hint);
        }

        // ==================== 常驻更新 ====================

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Main.gameMenu) return;

            Player player = Main.LocalPlayer;
            bool isHolding = player != null && player.active && !player.dead
                             && MalachiteCache.IsMalachiteItem(player.HeldItem);

            // 入口按钮：手持孔雀柳刃才显示
            if (isHolding && _entryButton != null && _entryButton.Parent == null) Append(_entryButton);
            else if (!isHolding && _entryButton != null && _entryButton.Parent != null) RemoveChild(_entryButton);

            // 不手持时自动收起
            if (!isHolding && IsVisible)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                HidePanel();
            }

            if (!IsVisible || player == null) return;

            // Esc/库存键边沿 = 关闭请求（沿用项目 UI 习惯：不吞原版输入，仅收起本面板）
            bool invNow = player.controlInv;
            if (invNow && !_prevInventoryKey)
            {
                _prevInventoryKey = invNow;
                HidePanel();
                return;
            }
            _prevInventoryKey = invNow;
        }

        // ==================== 小工具 ====================

        private static MalachitePlayer LocalMp()
        {
            if (Main.LocalPlayer == null) return null;
            return Main.LocalPlayer.GetModPlayer<MalachitePlayer>();
        }

        /// <summary>左上对齐文本（TextOriginX/Y = 0，避免居中默认值干扰坐标布局）。</summary>
        private static UIText MakeText(string text, float scale, Color color)
        {
            UIText t = new UIText(text, scale);
            t.TextOriginX = 0f;
            t.TextOriginY = 0f;
            t.TextColor = color;
            return t;
        }

        /// <summary>居中文本（HAlign=0.5 + 原点居中）。</summary>
        private static UIText MakeCenteredText(string text, float scale, Color color)
        {
            UIText t = new UIText(text, scale);
            t.HAlign = 0.5f;
            t.TextOriginX = 0.5f;
            t.TextOriginY = 0f;
            t.TextColor = color;
            return t;
        }

        /// <summary>面板按钮基座：默认 30x28，调用方可覆写；文字内容居中。宽度/高度由调用方设置。</summary>
        private UIPanel MakeButton(string label, float textScale, Color labelColor, Action onClick)
        {
            UIPanel btn = new UIPanel();
            btn.Width.Set(30, 0f);
            btn.Height.Set(28, 0f);
            btn.BackgroundColor = MalachitePalette.GreenDark * 0.9f;
            btn.BorderColor = MalachitePalette.PrimaryGreen * 0.7f;
            btn.SetPadding(0);

            btn.OnMouseOver += (evt, element) =>
            {
                btn.BackgroundColor = MalachitePalette.GreenDeep;
                btn.BorderColor = MalachitePalette.GreenBright;
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            btn.OnMouseOut += (evt, element) =>
            {
                btn.BackgroundColor = MalachitePalette.GreenDark * 0.9f;
                btn.BorderColor = MalachitePalette.PrimaryGreen * 0.7f;
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
