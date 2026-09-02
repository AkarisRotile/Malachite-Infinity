using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace 可成长的孔雀翎
{
    public enum DialogState
    {
        Hidden = 0, Typing = 1, Paused = 2, FadingOut = 3, Closing = 4
    }

    // =====================================================================
    // 对话 UI（冻结区）：MalachiteUIState / HistoryUIState 一律不重构。
    // 旧星图（星图节点/连线/主面板等 Sigil* 元素类）已于 M3（v2 切换）整体删除，
    // 由独立文件 UI\StarMapUI.cs 的可翻页加点界面替代（入口按钮思路保留）。
    // =====================================================================

    public class MalachiteUIState : UIState
    {
        public UIPanel DialogPanel;
        public UIText DialogText;
        public UIPanel NameplatePanel;
        public UIText NameplateText;

        public List<(int face, string text)> CurrentMessages = new List<(int, string)>();
        private List<string> _prewrappedMessages = new List<string>();

        public int MessageIndex = 0, CharIndex = 0, StateTimer = 0, PauseTimer = 0;
        public DialogState CurrentState = DialogState.Hidden;
        public float CurrentAlpha = 0f;
        public int ActiveFrames = 0;
        public string CurrentCategory = "";

        private int _lastRenderedCharIndex = -1;
        private int _lastRenderedMsgIndex = -1;

        private Dictionary<int, Asset<Texture2D>> portraitAssets = new Dictionary<int, Asset<Texture2D>>();
        private static readonly Dictionary<int, string> FaceToTexture = new Dictionary<int, string>()
        {
            {0, "normal"}, {1, "quest"}, {2, "thinking"}, {3, "angry"}, {4, "sad"}
        };

        private UIPanel skipButton;
        private UIText skipButtonText;
        private UIPanel historyButton;
        private UIText historyButtonText;

        public override void OnInitialize()
        {
            if (!Main.dedServ)
            {
                foreach (var kvp in FaceToTexture)
                {
                    portraitAssets[kvp.Key] = ModContent.Request<Texture2D>($"可成长的孔雀翎/Textures/Portraits/{kvp.Value}");
                }
            }

            DialogPanel = new UIPanel();
            DialogPanel.HAlign = 0.5f;
            DialogPanel.VAlign = 0.86f;
            DialogPanel.Width.Set(700, 0);
            DialogPanel.Height.Set(145, 0);
            DialogPanel.SetPadding(10);
            DialogPanel.IgnoresMouseInteraction = false;

            DialogPanel.OnLeftClick += (evt, element) => {
                if (CurrentState == DialogState.Typing)
                {
                    if (_prewrappedMessages != null && MessageIndex < _prewrappedMessages.Count)
                    {
                        CharIndex = _prewrappedMessages[MessageIndex].Length;
                        CurrentState = DialogState.Paused;
                        PauseTimer = 0;
                        UpdateRenderedText(true);
                    }
                }
                else if (CurrentState == DialogState.Paused)
                {
                    if (MessageIndex < _prewrappedMessages.Count - 1)
                    {
                        MessageIndex++;
                        CharIndex = 0;
                        StateTimer = 0;
                        PauseTimer = 0;
                        CurrentState = DialogState.Typing;
                        UpdateRenderedText(true);
                    }
                    else
                    {
                        CurrentState = DialogState.FadingOut;
                        StateTimer = 0;
                    }
                }
            };

            NameplatePanel = new UIPanel();
            NameplatePanel.Width.Set(140, 0);
            NameplatePanel.Height.Set(28, 0);
            NameplatePanel.Left.Set(20, 0);
            NameplatePanel.Top.Set(-14, 0);
            NameplatePanel.SetPadding(0);
            NameplatePanel.BackgroundColor = new Color(15, 32, 22, 240);
            NameplatePanel.BorderColor = new Color(70, 200, 130, 230);
            NameplatePanel.IgnoresMouseInteraction = true;

            NameplateText = new UIText("孔雀翎", 0.85f);
            NameplateText.HAlign = 0.5f;
            NameplateText.VAlign = 0.5f;
            NameplateText.TextColor = new Color(175, 255, 200);
            NameplatePanel.Append(NameplateText);
            DialogPanel.Append(NameplatePanel);

            DialogText = new UIText("", 0.95f, false);
            DialogText.HAlign = 0f;
            DialogText.VAlign = 0f;
            DialogText.Left.Set(24, 0);
            DialogText.Top.Set(26, 0);
            DialogText.Width.Set(640, 0);
            DialogText.Height.Set(85, 0);
            DialogText.TextOriginX = 0f;
            DialogText.TextOriginY = 0f;
            DialogText.IgnoresMouseInteraction = true;
            DialogPanel.Append(DialogText);

            skipButton = new UIPanel();
            skipButton.Width.Set(65, 0);
            skipButton.Height.Set(24, 0);
            skipButton.HAlign = 1f;
            skipButton.VAlign = 1f;
            skipButton.Left.Set(-85, 0);
            skipButton.Top.Set(-8, 0);
            skipButton.SetPadding(0);
            skipButton.OnLeftClick += (evt, element) => {
                CurrentState = DialogState.Closing;
                CurrentAlpha = 0f;
                DialogText.SetText("");
            };
            skipButton.OnMouseOver += (evt, element) => {
                skipButton.BackgroundColor = new Color(40, 85, 55, 240);
                skipButton.BorderColor = Color.SpringGreen;
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            skipButton.OnMouseOut += (evt, element) => {
                skipButton.BackgroundColor = new Color(20, 45, 30, 200);
                skipButton.BorderColor = new Color(75, 160, 100, 180);
            };
            skipButtonText = new UIText("跳过 ▶", 0.78f);
            skipButtonText.HAlign = 0.5f;
            skipButtonText.VAlign = 0.5f;
            skipButtonText.TextColor = new Color(160, 240, 180);
            skipButton.Append(skipButtonText);
            DialogPanel.Append(skipButton);

            historyButton = new UIPanel();
            historyButton.Width.Set(65, 0);
            historyButton.Height.Set(24, 0);
            historyButton.HAlign = 1f;
            historyButton.VAlign = 1f;
            historyButton.Left.Set(-12, 0);
            historyButton.Top.Set(-8, 0);
            historyButton.SetPadding(0);
            historyButton.OnLeftClick += (evt, element) => ModContent.GetInstance<MalachiteUISystem>().ShowHistoryUI();
            historyButton.OnMouseOver += (evt, element) => {
                historyButton.BackgroundColor = new Color(40, 85, 55, 240);
                historyButton.BorderColor = Color.SpringGreen;
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            historyButton.OnMouseOut += (evt, element) => {
                historyButton.BackgroundColor = new Color(20, 45, 30, 200);
                historyButton.BorderColor = new Color(75, 160, 100, 180);
            };
            historyButtonText = new UIText("回忆 📜", 0.78f);
            historyButtonText.HAlign = 0.5f;
            historyButtonText.VAlign = 0.5f;
            historyButtonText.TextColor = new Color(160, 240, 180);
            historyButton.Append(historyButtonText);
            DialogPanel.Append(historyButton);

            Append(DialogPanel);
        }

        private string PrewrapText(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || Main.dedServ) return text;
            var font = Terraria.GameContent.FontAssets.MouseText.Value;
            var paragraphs = text.Split('\n');
            List<string> resultLines = new List<string>();

            foreach (var para in paragraphs)
            {
                string line = "";
                for (int i = 0; i < para.Length; i++)
                {
                    char c = para[i];
                    string testLine = line + c;
                    if (font.MeasureString(testLine).X * 0.95f > maxWidth && line.Length > 0)
                    {
                        resultLines.Add(line);
                        line = c.ToString();
                    }
                    else
                    {
                        line = testLine;
                    }
                }
                if (!string.IsNullOrEmpty(line)) resultLines.Add(line);
            }
            return string.Join("\n", resultLines);
        }

        public void Show(List<(int face, string text)> messages, string category = "")
        {
            if (messages == null || messages.Count == 0) return;
            CurrentMessages = messages;
            CurrentCategory = category;

            _prewrappedMessages.Clear();
            for (int i = 0; i < messages.Count; i++)
            {
                _prewrappedMessages.Add(PrewrapText(messages[i].text, 630f));
            }

            MessageIndex = 0; CharIndex = 0; StateTimer = 0; PauseTimer = 0; ActiveFrames = 0;
            CurrentState = DialogState.Typing;
            CurrentAlpha = 0.1f;
            _lastRenderedCharIndex = -1;
            _lastRenderedMsgIndex = -1;

            if (NameplateText != null)
                NameplateText.SetText(MalachiteData.Loc("孔雀翎", "✦ Malachite Spirit ✦"));

            UpdateRenderedText(true);
        }

        private void UpdateRenderedText(bool force = false)
        {
            if (_prewrappedMessages == null || MessageIndex >= _prewrappedMessages.Count) return;
            string currentStr = _prewrappedMessages[MessageIndex];

            if (force || _lastRenderedCharIndex != CharIndex || _lastRenderedMsgIndex != MessageIndex)
            {
                _lastRenderedCharIndex = CharIndex;
                _lastRenderedMsgIndex = MessageIndex;
                DialogText.SetText(currentStr.Substring(0, Math.Min(CharIndex, currentStr.Length)));
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Main.gameMenu) return;

            float targetAlpha = 0f;
            bool inCombat = Main.LocalPlayer.GetModPlayer<MalachitePlayer>().InCombat;

            switch (CurrentState)
            {
                case DialogState.Typing:
                case DialogState.Paused:
                case DialogState.FadingOut:
                    targetAlpha = inCombat ? 0.35f : 1.0f;
                    ActiveFrames++;
                    break;
            }

            if (CurrentState == DialogState.Typing)
            {
                if (++StateTimer >= 3)
                {
                    StateTimer = 0;
                    CharIndex++;
                    if (MessageIndex < _prewrappedMessages.Count)
                    {
                        if (CharIndex >= _prewrappedMessages[MessageIndex].Length)
                        {
                            CharIndex = _prewrappedMessages[MessageIndex].Length;
                            CurrentState = DialogState.Paused;
                            PauseTimer = 0;
                        }
                        UpdateRenderedText();
                    }
                }
            }
            else if (CurrentState == DialogState.Paused)
            {
                var config = ModContent.GetInstance<MalachiteConfig>();
                bool autoPlay = config != null && config.AutoPlayDialog;

                if (autoPlay)
                {
                    PauseTimer++;
                    string currentStr = _prewrappedMessages[MessageIndex];
                    int waitTime = 120 + currentStr.Length * 3;
                    if (PauseTimer >= waitTime)
                    {
                        PauseTimer = 0;
                        if (MessageIndex < _prewrappedMessages.Count - 1)
                        {
                            MessageIndex++;
                            CharIndex = 0;
                            StateTimer = 0;
                            CurrentState = DialogState.Typing;
                            UpdateRenderedText(true);
                        }
                        else
                        {
                            CurrentState = DialogState.FadingOut;
                            StateTimer = 0;
                        }
                    }
                }
            }
            else if (CurrentState == DialogState.FadingOut)
            {
                if (++StateTimer >= 30) CurrentState = DialogState.Closing;
            }

            if (CurrentState == DialogState.Closing || CurrentState == DialogState.Hidden)
            {
                targetAlpha = 0f;
                if (CurrentAlpha < 0.01f) CurrentState = DialogState.Hidden;
            }

            CurrentAlpha = MathHelper.Lerp(CurrentAlpha, targetAlpha, 0.15f);

            DialogPanel.BackgroundColor = new Color(10, 18, 14, 235) * CurrentAlpha;
            DialogPanel.BorderColor = new Color(50, 185, 110, 240) * CurrentAlpha;
            DialogText.TextColor = new Color(220, 255, 235) * CurrentAlpha;

            NameplatePanel.BackgroundColor = new Color(15, 32, 22, 240) * CurrentAlpha;
            NameplatePanel.BorderColor = new Color(70, 200, 130, 230) * CurrentAlpha;
            NameplateText.TextColor = new Color(175, 255, 200) * CurrentAlpha;

            skipButton.BackgroundColor = new Color(20, 45, 30, 200) * CurrentAlpha;
            skipButton.BorderColor = new Color(75, 160, 100, 180) * CurrentAlpha;
            skipButtonText.TextColor = new Color(160, 240, 180) * CurrentAlpha;

            historyButton.BackgroundColor = new Color(20, 45, 30, 200) * CurrentAlpha;
            historyButton.BorderColor = new Color(75, 160, 100, 180) * CurrentAlpha;
            historyButtonText.TextColor = new Color(160, 240, 180) * CurrentAlpha;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (CurrentAlpha <= 0.01f || portraitAssets.Count == 0)
                return;

            int faceId = 0;
            if (CurrentMessages != null && MessageIndex < CurrentMessages.Count)
                faceId = CurrentMessages[MessageIndex].face;

            if (!portraitAssets.TryGetValue(faceId, out Asset<Texture2D> portraitAsset))
                portraitAssets.TryGetValue(0, out portraitAsset);

            if (portraitAsset == null) return;

            Texture2D portrait = portraitAsset.Value;
            CalculatedStyle dimensions = DialogPanel.GetDimensions();

            float targetHeight = Main.screenHeight * 0.55f;
            float scale = targetHeight / portrait.Height;
            float targetWidth = portrait.Width * scale;
            float maxWidth = Main.screenWidth * 0.8f;

            if (targetWidth > maxWidth)
            {
                scale = maxWidth / portrait.Width;
                targetWidth = maxWidth;
                targetHeight = portrait.Height * scale;
            }

            float drawX = dimensions.X + dimensions.Width / 2f - targetWidth / 2f;
            float drawY = dimensions.Y - targetHeight + 25f;
            if (drawY < 10f) drawY = 10f;

            spriteBatch.Draw(portrait, new Vector2(drawX, drawY), null, Color.White * CurrentAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    public class HistoryUIState : UIState
    {
        private UIPanel panel;
        private UIList historyList;
        private UIScrollbar scrollbar;
        private UIText title;

        public override void OnInitialize()
        {
            panel = new UIPanel();
            panel.Width.Set(520, 0);
            panel.Height.Set(420, 0);
            panel.HAlign = 0.5f;
            panel.VAlign = 0.5f;
            panel.BackgroundColor = new Color(12, 24, 18, 235);
            panel.BorderColor = new Color(60, 180, 110, 220);
            Append(panel);

            title = new UIText("✦ 剧情回忆 ✦", 1.15f);
            title.HAlign = 0.5f;
            title.Top.Set(12, 0);
            title.TextColor = new Color(160, 255, 190);
            panel.Append(title);

            UIPanel separator = new UIPanel();
            separator.Width.Set(480, 0);
            separator.Height.Set(2, 0);
            separator.HAlign = 0.5f;
            separator.Top.Set(45, 0);
            separator.SetPadding(0);
            separator.BackgroundColor = new Color(50, 150, 90, 180);
            separator.BorderColor = Color.Transparent;
            panel.Append(separator);

            historyList = new UIList();
            historyList.Width.Set(470, 0);
            historyList.Height.Set(335, 0);
            historyList.HAlign = 0.5f;
            historyList.Top.Set(55, 0);
            historyList.ListPadding = 6f;
            panel.Append(historyList);

            scrollbar = new UIScrollbar();
            scrollbar.Width.Set(18, 0);
            scrollbar.Height.Set(335, 0);
            scrollbar.Left.Set(-25, 1f);
            scrollbar.Top.Set(55, 0);
            panel.Append(scrollbar);
            historyList.SetScrollbar(scrollbar);

            UIPanel closeBtn = new UIPanel();
            closeBtn.Width.Set(28, 0);
            closeBtn.Height.Set(28, 0);
            closeBtn.Left.Set(-36, 1f);
            closeBtn.Top.Set(10, 0);
            closeBtn.BackgroundColor = new Color(40, 15, 15, 180);
            closeBtn.BorderColor = new Color(180, 60, 60, 200);
            closeBtn.SetPadding(0);

            UIText closeText = new UIText("✕", 0.9f);
            closeText.HAlign = 0.5f;
            closeText.VAlign = 0.5f;
            closeText.TextColor = Color.LightCoral;
            closeBtn.Append(closeText);

            closeBtn.OnLeftClick += (evt, element) => ModContent.GetInstance<MalachiteUISystem>().HideHistoryUI();
            closeBtn.OnMouseOver += (evt, element) => {
                closeBtn.BackgroundColor = new Color(90, 25, 25, 220);
                closeBtn.BorderColor = Color.Red;
                closeText.TextColor = Color.White;
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            closeBtn.OnMouseOut += (evt, element) => {
                closeBtn.BackgroundColor = new Color(40, 15, 15, 180);
                closeBtn.BorderColor = new Color(180, 60, 60, 200);
                closeText.TextColor = Color.LightCoral;
            };
            panel.Append(closeBtn);
        }

        public void UpdateList()
        {
            if (historyList == null) return;
            historyList.Clear();

            if (Main.LocalPlayer == null || !Main.LocalPlayer.active) return;
            var mp = Main.LocalPlayer.GetModPlayer<MalachitePlayer>();

            foreach (var group in DialogDatabase.OrderedGroups)
            {
                bool unlocked = mp.SavedDialogGroups.Contains(group.Id);
                UIPanel item = new UIPanel();
                item.Width.Set(450, 0);
                item.Height.Set(36, 0);
                item.SetPadding(6);

                if (unlocked)
                {
                    item.BackgroundColor = new Color(25, 60, 38, 220);
                    item.BorderColor = new Color(80, 200, 120, 200);
                    item.IgnoresMouseInteraction = false;

                    UIText name = new UIText(group.GetDisplayName(), 0.85f);
                    name.TextColor = new Color(180, 255, 200);
                    name.HAlign = 0f;
                    name.VAlign = 0.5f;
                    item.Append(name);

                    string gid = group.Id;
                    item.OnLeftClick += (evt, e) => {
                        ModContent.GetInstance<MalachiteUISystem>().HideHistoryUI();
                        MalachiteUISystem.ShowDialogGroup(gid, force: true);
                    };
                    item.OnMouseOver += (evt, e) => {
                        item.BackgroundColor = new Color(40, 95, 55, 240);
                        item.BorderColor = Color.SpringGreen;
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    };
                    item.OnMouseOut += (evt, e) => {
                        item.BackgroundColor = new Color(25, 60, 38, 220);
                        item.BorderColor = new Color(80, 200, 120, 200);
                    };
                }
                else
                {
                    item.BackgroundColor = new Color(16, 20, 18, 180);
                    item.BorderColor = new Color(55, 65, 60, 150);
                    item.IgnoresMouseInteraction = true;

                    string hint = group.GetUnlockHint();
                    UIText locked = new UIText(MalachiteData.IsEnglish ? $"???  {hint}" : $"未解锁：{hint}", 0.75f);
                    locked.TextColor = Color.Gray;
                    locked.HAlign = 0f;
                    locked.VAlign = 0.5f;
                    item.Append(locked);
                }

                historyList.Add(item);
            }
        }
    }

    // =====================================================================
    // UI 系统骨架：对话 + History + 星图（StarMapUI）三套接口的注册/更新/绘制。
    // =====================================================================

    public class MalachiteUISystem : ModSystem
    {
        internal MalachiteUIState MalachiteUI;
        internal StarMapUIState StarMapUI;
        internal HistoryUIState HistoryUI;
        private UserInterface _userInterface;
        private UserInterface _starMapInterface;
        private UserInterface _historyInterface;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                MalachiteUI = new MalachiteUIState();
                MalachiteUI.Activate();
                _userInterface = new UserInterface();
                _userInterface.SetState(MalachiteUI);

                // v2：旧 Sigil UI 由可翻页加点界面 StarMapUI 取代（注册/移除模式沿用旧实现）
                StarMapUI = new StarMapUIState();
                StarMapUI.Activate();
                _starMapInterface = new UserInterface();
                _starMapInterface.SetState(StarMapUI);

                HistoryUI = new HistoryUIState();
                HistoryUI.Activate();
                _historyInterface = new UserInterface();
                _historyInterface.SetState(null);
            }
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (Main.gameMenu) return;

            if (MalachiteUI != null && MalachiteUI.CurrentAlpha > 0.001f)
                _userInterface?.Update(gameTime);

            // 星图 UI：面板可见或手持孔雀柳刃时更新（入口按钮显隐由其自身 Update 管理）
            if (StarMapUI != null && (StarMapUI.IsVisible || MalachiteCache.IsMalachiteItem(Main.LocalPlayer?.HeldItem)))
                _starMapInterface?.Update(gameTime);

            if (HistoryUI != null && _historyInterface?.CurrentState == HistoryUI)
                _historyInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer("MalachiteMod: Dialog UI", delegate {
                    if (MalachiteUI.CurrentAlpha > 0.001f) _userInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                }, InterfaceScaleType.UI));

                layers.Insert(mouseTextIndex + 1, new LegacyGameInterfaceLayer("MalachiteMod: History UI", delegate {
                    if (HistoryUI != null && _historyInterface?.CurrentState == HistoryUI)
                        _historyInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                }, InterfaceScaleType.UI));

                layers.Insert(mouseTextIndex + 2, new LegacyGameInterfaceLayer("MalachiteMod: Star Map UI", delegate {
                    if (StarMapUI != null && (StarMapUI.IsVisible || MalachiteCache.IsMalachiteItem(Main.LocalPlayer?.HeldItem)))
                        _starMapInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                }, InterfaceScaleType.UI));
            }
        }

        public static bool IsAnyBossAlive()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.life > 0)
                {
                    if (npc.boss || npc.type == NPCID.EaterofWorldsHead
                                 || npc.type == NPCID.EaterofWorldsBody
                                 || npc.type == NPCID.EaterofWorldsTail)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static void ShowDialog(List<(int face, string text)> messages)
        {
            if (!Main.dedServ) ModContent.GetInstance<MalachiteUISystem>().MalachiteUI.Show(messages, "");
        }

        public static void ShowDialogGroup(string groupId, bool force = false)
        {
            var group = DialogDatabase.GetGroup(groupId);
            if (group == null) return;

            if (!force && group.Category != "milestone" && IsAnyBossAlive())
                return;

            var messages = group.GetMessages();
            if (messages == null || messages.Count == 0) return;

            if (!Main.dedServ)
                ModContent.GetInstance<MalachiteUISystem>().MalachiteUI.Show(messages, group.Category);

            if (Main.LocalPlayer != null && Main.LocalPlayer.active)
                Main.LocalPlayer.GetModPlayer<MalachitePlayer>().RecordDialogGroup(groupId);
        }

        public void ShowHistoryUI()
        {
            if (HistoryUI != null && _historyInterface != null)
            {
                HistoryUI.UpdateList();
                _historyInterface.SetState(HistoryUI);
            }
        }

        public void HideHistoryUI()
        {
            if (_historyInterface != null)
                _historyInterface.SetState(null);
        }
    }
}
