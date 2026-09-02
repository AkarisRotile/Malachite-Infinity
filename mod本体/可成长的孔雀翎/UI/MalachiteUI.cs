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

    public class FloatingWeaponElement : UIElement
    {
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            CalculatedStyle dimensions = GetDimensions();
            int malachiteType = MalachiteCache.NativeMalachiteItem;
            {
                Texture2D tex = Terraria.GameContent.TextureAssets.Item[malachiteType].Value;
                float yOffset = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 12f;
                Vector2 pos = new Vector2(dimensions.X + dimensions.Width / 2, dimensions.Y + dimensions.Height / 2 + yOffset);
                spriteBatch.Draw(tex, pos, null, Color.White * 0.25f, 0f, tex.Size() / 2, 2f, SpriteEffects.None, 0f);
            }
        }
    }

    public class SigilNodeElement : UIElement
    {
        public int ID;
        public bool IsHovered;

        public SigilNodeElement(int id)
        {
            ID = id;
            Width.Set(18, 0);
            Height.Set(18, 0);
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            IsHovered = true;
            SoundEngine.PlaySound(SoundID.MenuTick);
            ModContent.GetInstance<MalachiteUISystem>().SigilUI.HoveredSigilID = ID;
        }

        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
            IsHovered = false;
            var ui = ModContent.GetInstance<MalachiteUISystem>().SigilUI;
            if (ui.HoveredSigilID == ID) ui.HoveredSigilID = -1;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            SoundEngine.PlaySound(SoundID.MenuTick);
            ModContent.GetInstance<MalachiteUISystem>().SigilUI.ToggleSigil(ID);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var mp = Main.LocalPlayer.GetModPlayer<MalachitePlayer>();
            var sigil = MalachiteTalents.Sigils[ID];
            bool isUnlocked = sigil.UnlockCondition();
            bool isActive = mp.ActiveSigils.Contains(ID);

            Color color = !isUnlocked ? new Color(100, 30, 30) : (isActive ? Color.LimeGreen : (IsHovered ? Color.White : Color.DarkSeaGreen));
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Vector2 center = GetDimensions().Center();
            float rotation = MathHelper.PiOver4;
            float scale = IsHovered ? 15f : 12f;

            if (isActive || IsHovered)
            {
                float pulse = isActive ? (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.2f + 0.8f : 1f;
                spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color * 0.3f, rotation, new Vector2(0.5f, 0.5f), scale * 1.5f * pulse, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);

            if (isActive)
            {
                float innerPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.2f + 0.8f;
                spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), Color.White * 0.9f, rotation, new Vector2(0.5f, 0.5f), 5f * innerPulse, SpriteEffects.None, 0f);
            }
        }
    }

    public class SigilMapElement : UIElement
    {
        private SigilNodeElement[] _nodeLookup;

        public SigilMapElement()
        {
            FloatingWeaponElement weapon = new FloatingWeaponElement();
            weapon.HAlign = 0.5f; weapon.VAlign = 0.5f; Append(weapon);

            _nodeLookup = new SigilNodeElement[MalachiteTalents.Sigils.Count];
            for (int i = 0; i < MalachiteTalents.Sigils.Count; i++)
            {
                SigilNodeElement node = new SigilNodeElement(i);
                node.HAlign = 0.5f; node.VAlign = 0.5f;
                node.Left.Set(MalachiteTalents.NodePositions[i].X, 0);
                node.Top.Set(MalachiteTalents.NodePositions[i].Y, 0);
                Append(node);
                _nodeLookup[i] = node;
            }
        }

        protected override void DrawChildren(SpriteBatch spriteBatch)
        {
            var mp = Main.LocalPlayer.GetModPlayer<MalachitePlayer>();
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;

            foreach (var conn in MalachiteTalents.Connections)
            {
                var nodeA = (conn.Item1 >= 0 && conn.Item1 < _nodeLookup.Length) ? _nodeLookup[conn.Item1] : null;
                var nodeB = (conn.Item2 >= 0 && conn.Item2 < _nodeLookup.Length) ? _nodeLookup[conn.Item2] : null;

                if (nodeA != null && nodeB != null)
                {
                    Vector2 start = nodeA.GetDimensions().Center();
                    Vector2 end = nodeB.GetDimensions().Center();

                    bool aActive = mp.ActiveSigils.Contains(conn.Item1);
                    bool bActive = mp.ActiveSigils.Contains(conn.Item2);
                    Color lineColor = (aActive && bActive) ? Color.LimeGreen * 0.8f : new Color(30, 60, 40, 150);

                    Vector2 dir = end - start;
                    float length = dir.Length();

                    if (length > 0)
                    {
                        float thicknessMulti = (aActive && bActive) ? 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f - length * 0.05f) * 0.25f : 1f;
                        spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), lineColor * 0.25f, dir.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, 3f * thicknessMulti), SpriteEffects.None, 0f);
                        spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), lineColor * 0.7f, dir.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, 1.5f * thicknessMulti), SpriteEffects.None, 0f);
                    }
                }
            }
            base.DrawChildren(spriteBatch);
        }
    }

    public class SigilUIState : UIState
    {
        public UIPanel EntryButton;
        public UIPanel MainPanel;
        public UIText CapacityText, HoverNameText, HoverCostText, HoverDescText;
        public UIText TrackDescText, PierceDescText, ConflictText, WarningText;

        public bool IsVisible = false;
        public int HoveredSigilID = -1;
        private int WarningTimer = 0;
        private int _lastHoveredID = -2;
        private int _lastActiveCount = -1;

        public override void OnInitialize()
        {
            EntryButton = new UIPanel();
            EntryButton.Left.Set(20, 0); EntryButton.Top.Set(-60, 1f);
            EntryButton.Width.Set(130, 0); EntryButton.Height.Set(40, 0);
            EntryButton.BackgroundColor = new Color(15, 40, 25, 200);
            EntryButton.BorderColor = Color.LimeGreen;

            EntryButton.OnMouseOver += (evt, element) => {
                EntryButton.BackgroundColor = new Color(25, 60, 40, 220);
                EntryButton.BorderColor = Color.SpringGreen;
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            EntryButton.OnMouseOut += (evt, element) => {
                EntryButton.BackgroundColor = new Color(15, 40, 25, 200);
                EntryButton.BorderColor = Color.LimeGreen;
            };
            EntryButton.OnLeftClick += (evt, element) => {
                SoundEngine.PlaySound(IsVisible ? SoundID.MenuClose : SoundID.MenuOpen);
                IsVisible = !IsVisible;
                if (IsVisible) RebuildPanel();
                else { if (MainPanel != null) RemoveChild(MainPanel); Recalculate(); }
            };

            UIText btnText = new UIText(MalachiteData.Loc("✦ 翎之星图", "✦ Sigil Map"));
            btnText.HAlign = 0.5f; btnText.VAlign = 0.5f;
            EntryButton.Append(btnText);
            Append(EntryButton);
        }

        public void RebuildPanel()
        {
            _lastHoveredID = -2; _lastActiveCount = -1;
            if (MainPanel != null) RemoveChild(MainPanel);

            MainPanel = new UIPanel();
            MainPanel.Width.Set(820, 0); MainPanel.Height.Set(520, 0);
            MainPanel.HAlign = 0.5f; MainPanel.VAlign = 0.5f;
            MainPanel.BackgroundColor = new Color(12, 22, 16, 230);
            MainPanel.BorderColor = new Color(45, 140, 85, 200);

            SigilMapElement mapPanel = new SigilMapElement();
            mapPanel.Width.Set(480, 0); mapPanel.Height.Set(500, 0);
            mapPanel.Left.Set(10, 0); mapPanel.Top.Set(10, 0);
            MainPanel.Append(mapPanel);

            UIPanel detailsPanel = new UIPanel();
            detailsPanel.Width.Set(300, 0); detailsPanel.Height.Set(480, 0);
            detailsPanel.Left.Set(500, 0); detailsPanel.Top.Set(15, 0);
            detailsPanel.BackgroundColor = new Color(5, 10, 8, 180);
            detailsPanel.BorderColor = new Color(30, 90, 50, 150);

            UIText titleText = new UIText(MalachiteData.Loc("—— 翎 之 秘 印 ——", "—— S I G I L S ——"), 1.15f);
            titleText.HAlign = 0.5f; titleText.Top.Set(10, 0);
            titleText.TextColor = new Color(175, 255, 175);
            detailsPanel.Append(titleText);

            CapacityText = new UIText(MalachiteData.Loc("灵力占用: 0 / 0", "Spirit Cost: 0 / 0"), 1.05f);
            CapacityText.HAlign = 0.5f; CapacityText.Top.Set(45, 0);
            detailsPanel.Append(CapacityText);

            UIPanel separator = new UIPanel();
            separator.Width.Set(260, 0); separator.Height.Set(2, 0);
            separator.HAlign = 0.5f; separator.Top.Set(80, 0);
            separator.SetPadding(0); separator.BackgroundColor = Color.MediumSpringGreen * 0.4f;
            separator.BorderColor = Color.Transparent;
            detailsPanel.Append(separator);

            HoverNameText = new UIText(MalachiteData.Loc("悬停星辰以解析", "Hover over a star to inspect"), 1.1f);
            HoverNameText.HAlign = 0.5f; HoverNameText.Top.Set(100, 0);
            detailsPanel.Append(HoverNameText);

            HoverCostText = new UIText("", 0.95f);
            HoverCostText.HAlign = 0.5f; HoverCostText.Top.Set(135, 0);
            detailsPanel.Append(HoverCostText);

            UIPanel descSeparator = new UIPanel();
            descSeparator.Width.Set(180, 0); descSeparator.Height.Set(2, 0);
            descSeparator.HAlign = 0.5f; descSeparator.Top.Set(165, 0);
            descSeparator.SetPadding(0); descSeparator.BackgroundColor = Color.DarkOliveGreen * 0.4f;
            descSeparator.BorderColor = Color.Transparent;
            detailsPanel.Append(descSeparator);

            HoverDescText = new UIText("", 0.9f) { IsWrapped = true };
            HoverDescText.Width.Set(280, 0); HoverDescText.HAlign = 0.5f; HoverDescText.Top.Set(175, 0);
            detailsPanel.Append(HoverDescText);

            TrackDescText = new UIText("", 0.85f) { IsWrapped = true };
            TrackDescText.Width.Set(280, 0); TrackDescText.HAlign = 0.5f;
            detailsPanel.Append(TrackDescText);

            PierceDescText = new UIText("", 0.85f) { IsWrapped = true };
            PierceDescText.Width.Set(280, 0); PierceDescText.HAlign = 0.5f;
            detailsPanel.Append(PierceDescText);

            ConflictText = new UIText("", 0.85f) { IsWrapped = true };
            ConflictText.Width.Set(280, 0); ConflictText.HAlign = 0.5f;
            detailsPanel.Append(ConflictText);

            WarningText = new UIText("", 0.95f) { IsWrapped = true };
            WarningText.Width.Set(280, 0); WarningText.HAlign = 0.5f;
            WarningText.Top.Set(-50, 1f);
            WarningText.TextColor = new Color(255, 80, 80);
            detailsPanel.Append(WarningText);

            MainPanel.Append(detailsPanel);

            UIPanel closeBtn = new UIPanel();
            closeBtn.Width.Set(30, 0); closeBtn.Height.Set(30, 0);
            closeBtn.Left.Set(-40, 1f); closeBtn.Top.Set(10, 0);
            closeBtn.BackgroundColor = new Color(0, 0, 0, 150);
            closeBtn.BorderColor = new Color(150, 50, 50, 200);
            closeBtn.SetPadding(0);

            UIText closeText = new UIText("X", 0.9f);
            closeText.HAlign = 0.5f; closeText.VAlign = 0.5f;
            closeText.TextColor = Color.LightCoral;
            closeBtn.Append(closeText);

            closeBtn.OnLeftClick += (evt, element) => {
                SoundEngine.PlaySound(SoundID.MenuClose);
                IsVisible = false;
                RemoveChild(MainPanel);
                Recalculate();
            };
            MainPanel.Append(closeBtn);

            Append(MainPanel);
            Recalculate();
        }

        public void ToggleSigil(int id)
        {
            MalachitePlayer mp = Main.LocalPlayer.GetModPlayer<MalachitePlayer>();
            if (mp.ActiveSigils.Contains(id))
            {
                mp.ActiveSigils.Remove(id);
                mp.SyncPlayerSigils();
                return;
            }

            var sigil = MalachiteTalents.Sigils[id];
            if (!sigil.UnlockCondition())
            {
                ShowWarning(MalachiteData.Loc("星辰封印尚未解除。", "Star seal not yet lifted."));
                return;
            }

            foreach (int conflict in sigil.Conflicts)
            {
                if (mp.ActiveSigils.Contains(conflict))
                {
                    string conflictNameZh = MalachiteTalents.Sigils[conflict].NameZh;
                    string conflictNameEn = MalachiteTalents.Sigils[conflict].NameEn;
                    ShowWarning(MalachiteData.Loc(
                        $"[流派冲突] 无法同时共存【{sigil.NameZh}】与【{conflictNameZh}】！普攻与射线流派互斥。",
                        $"[Path Conflict] Cannot equip [{sigil.NameEn}] and [{conflictNameEn}] together! Normal and Beam paths are mutually exclusive."
                    ));
                    return;
                }
            }

            int currentCost = 0;
            foreach (int s in mp.ActiveSigils)
            {
                if (s >= 0 && s < MalachiteTalents.Sigils.Count)
                    currentCost += MalachiteTalents.Sigils[s].Cost;
            }

            int maxCap = MalachiteTalents.GetMaxCapacity(ProgressSystem.GetStage());
            if (currentCost + sigil.Cost > maxCap)
            {
                ShowWarning(MalachiteData.Loc("[灵力枯竭] 刻印容量已达极限！", "[Spirit Depleted] Sigil capacity has reached its limit!"));
                return;
            }

            mp.ActiveSigils.Add(id);
            mp.SyncPlayerSigils();
        }

        private void ShowWarning(string msg)
        {
            if (WarningText != null) { WarningText.SetText(msg); WarningTimer = 180; }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Main.gameMenu) return;

            Player player = Main.LocalPlayer;
            MalachitePlayer mp = player.GetModPlayer<MalachitePlayer>();

            bool isHoldingMalachite = player.active && !player.dead && MalachiteCache.IsMalachiteItem(player.HeldItem);

            if (isHoldingMalachite && EntryButton.Parent == null) Append(EntryButton);
            else if (!isHoldingMalachite && EntryButton.Parent != null) RemoveChild(EntryButton);

            if (!isHoldingMalachite && IsVisible)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                IsVisible = false;
                if (MainPanel != null) RemoveChild(MainPanel);
                Recalculate();
            }

            if (!IsVisible) return;

            UpdateHoverInformation(mp);

            if (WarningTimer > 0)
            {
                WarningTimer--;
                if (WarningTimer == 0 && WarningText != null) WarningText.SetText("");
            }
        }

        private void UpdateHoverInformation(MalachitePlayer mp)
        {
            if (_lastHoveredID == HoveredSigilID && _lastActiveCount == mp.ActiveSigils.Count) return;

            _lastHoveredID = HoveredSigilID;
            _lastActiveCount = mp.ActiveSigils.Count;

            int currentCost = 0;
            foreach (int s in mp.ActiveSigils)
            {
                if (s >= 0 && s < MalachiteTalents.Sigils.Count)
                    currentCost += MalachiteTalents.Sigils[s].Cost;
            }

            int maxCap = MalachiteTalents.GetMaxCapacity(ProgressSystem.GetStage());
            if (CapacityText != null)
            {
                CapacityText.SetText(MalachiteData.Loc($"灵力占用: {currentCost} / {maxCap}", $"Spirit Cost: {currentCost} / {maxCap}"));
                CapacityText.TextColor = currentCost > maxCap ? new Color(255, 80, 80) : Color.LimeGreen;
            }

            if (HoverNameText != null)
            {
                if (HoveredSigilID != -1)
                {
                    var sigil = MalachiteTalents.Sigils[HoveredSigilID];
                    bool unlocked = sigil.UnlockCondition();

                    HoverNameText.SetText($"【{sigil.Name}】");
                    HoverNameText.TextColor = unlocked ? Color.LimeGreen : Color.Gray;
                    HoverCostText.SetText(unlocked ? MalachiteData.Loc($"灵力消耗: {sigil.Cost}", $"Spirit Cost: {sigil.Cost}") : MalachiteData.Loc("未知", "Unknown"));
                    HoverCostText.TextColor = Color.LightGoldenrodYellow;

                    if (unlocked)
                    {
                        HoverDescText.SetText(sigil.Desc);
                        HoverDescText.TextColor = new Color(180, 180, 180);

                        TrackDescText.SetText(string.IsNullOrEmpty(sigil.TrackDesc) ? "" : MalachiteData.Loc($"✦ 巡猎流：{sigil.TrackDescZh}", $"✦ Hunt Path: {sigil.TrackDescEn}"));
                        TrackDescText.TextColor = mp.ActiveSigils.Contains(1) ? new Color(50, 205, 50) : new Color(120, 140, 100);

                        PierceDescText.SetText(string.IsNullOrEmpty(sigil.PierceDesc) ? "" : MalachiteData.Loc($"✦ 贯穿流：{sigil.PierceDescZh}", $"✦ Pierce Path: {sigil.PierceDescEn}"));
                        PierceDescText.TextColor = mp.ActiveSigils.Contains(2) ? new Color(50, 205, 50) : new Color(120, 140, 100);

                        if (sigil.Conflicts.Length > 0)
                        {
                            string conflicts = string.Join(", ", sigil.Conflicts.Select(c => MalachiteTalents.Sigils[c].Name));
                            ConflictText.SetText(MalachiteData.Loc($"⚠️ 灵脉冲突: {conflicts}", $"⚠️ Synergy Conflict: {conflicts}"));
                            ConflictText.TextColor = new Color(205, 92, 92);
                        }
                        else ConflictText.SetText("");
                    }
                    else
                    {
                        HoverDescText.SetText(MalachiteData.Loc("此星辰黯淡无光...", "This star is dim..."));
                        HoverDescText.TextColor = Color.Gray;
                        TrackDescText.SetText(MalachiteData.Loc("✦ 破除封印需:", "✦ To unseal:"));
                        TrackDescText.TextColor = new Color(205, 92, 92);
                        PierceDescText.SetText(sigil.UnlockHint);
                        PierceDescText.TextColor = new Color(255, 182, 193);
                        ConflictText.SetText("");
                    }
                }
                else
                {
                    HoverNameText.SetText(MalachiteData.Loc("悬停星辰以解析", "Hover over a star to inspect"));
                    HoverNameText.TextColor = Color.Gray;
                    HoverCostText.SetText(""); HoverDescText?.SetText(""); TrackDescText?.SetText(""); PierceDescText?.SetText(""); ConflictText?.SetText("");
                }

                if (HoverDescText != null && TrackDescText != null && PierceDescText != null && ConflictText != null)
                {
                    float currentY = 175f;
                    if (!string.IsNullOrEmpty(HoverDescText.Text)) { HoverDescText.Top.Set(currentY, 0); HoverDescText.Recalculate(); currentY += HoverDescText.GetOuterDimensions().Height + 12f; }
                    else HoverDescText.Top.Set(9000, 0);

                    if (!string.IsNullOrEmpty(TrackDescText.Text)) { TrackDescText.Top.Set(currentY, 0); TrackDescText.Recalculate(); currentY += TrackDescText.GetOuterDimensions().Height + 8f; }
                    else TrackDescText.Top.Set(9000, 0);

                    if (!string.IsNullOrEmpty(PierceDescText.Text)) { PierceDescText.Top.Set(currentY, 0); PierceDescText.Recalculate(); currentY += PierceDescText.GetOuterDimensions().Height + 8f; }
                    else PierceDescText.Top.Set(9000, 0);

                    if (!string.IsNullOrEmpty(ConflictText.Text)) { ConflictText.Top.Set(currentY, 0); ConflictText.Recalculate(); }
                    else ConflictText.Top.Set(9000, 0);
                }
            }
        }
    }

    public class MalachiteUISystem : ModSystem
    {
        internal MalachiteUIState MalachiteUI;
        internal SigilUIState SigilUI;
        internal HistoryUIState HistoryUI;
        private UserInterface _userInterface;
        private UserInterface _sigilInterface;
        private UserInterface _historyInterface;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                MalachiteUI = new MalachiteUIState();
                MalachiteUI.Activate();
                _userInterface = new UserInterface();
                _userInterface.SetState(MalachiteUI);

                SigilUI = new SigilUIState();
                SigilUI.Activate();
                _sigilInterface = new UserInterface();
                _sigilInterface.SetState(SigilUI);

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

            if (SigilUI != null && (SigilUI.IsVisible || MalachiteCache.IsMalachiteItem(Main.LocalPlayer?.HeldItem)))
                _sigilInterface?.Update(gameTime);

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

                layers.Insert(mouseTextIndex + 2, new LegacyGameInterfaceLayer("MalachiteMod: Sigil UI", delegate {
                    if (SigilUI != null && (SigilUI.IsVisible || MalachiteCache.IsMalachiteItem(Main.LocalPlayer?.HeldItem)))
                        _sigilInterface.Draw(Main.spriteBatch, new GameTime());
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