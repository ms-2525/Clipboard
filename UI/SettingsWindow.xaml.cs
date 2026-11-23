using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clip.Core;

namespace Clip.UI
{
    public partial class SettingsWindow : Window
    {
        /// <summary>Light column of docs\DESIGN-contrast.md §4/§7; the dark column is the XAML default.</summary>
        static readonly (string Key, byte R, byte G, byte B)[] LightTokens =
        {
            ("TokMuted", 0x58, 0x60, 0x70), ("TokTitle", 0x4A, 0x51, 0x5B),
            ("TokLabel", 0x66, 0x6E, 0x7B), ("TokError", 0xCC, 0x2E, 0x33),
            ("TokControlLine", 0x7F, 0x86, 0x91), ("TokOnAccent", 0xFF, 0xFF, 0xFF),
            ("TokAccentText", 0xB8, 0x49, 0x00), ("TokDivider", 0xE2, 0xE5, 0xEA),
            ("TokField", 0xF3, 0xF4, 0xF6),
        };

        readonly Config _cfg;
        bool _loading = true;
        bool _notifTouched;

        /// <summary>Raised after the live config was mutated and saved to disk.</summary>
        public event Action<Config> SettingsSaved;

        /// <summary>Raised when the user confirms the clear-history action.</summary>
        public event Action ClearHistoryRequested;

        public SettingsWindow(Config cfg)
        {
            InitializeComponent();
            ApplyTokens();

            _cfg = cfg ?? new Config();
            Title = "Settings";

            // Use the room the display has; the ScrollViewer only kicks in on short screens.
            MaxHeight = Math.Max(480, SystemParameters.WorkArea.Height - 16);

            var n = _cfg.Notification ?? (_cfg.Notification = new Config.NotificationSettings());
            HotkeyBox.Text = _cfg.Hotkey ?? "";
            PlainHotkeyBox.Text = _cfg.HotkeyPlain ?? "";
            MaxItemsBox.Text = _cfg.MaxItems.ToString();
            PreviewLinesBox.Text = _cfg.PreviewLines.ToString();
            NotifEnabled.IsChecked = n.Enabled;
            DurationBox.Text = n.DurationMs.ToString();
            WidthBox.Text = n.Width.ToString();
            HeightBox.Text = n.Height.ToString();
            OffsetXBox.Text = n.OffsetX.ToString();
            OffsetYBox.Text = n.OffsetY.ToString();
            MaxLinesBox.Text = n.MaxLines.ToString();
            SelectPosition(n.Position);
            SelectTheme(_cfg.Theme);
            if (_cfg.TimeFormat == "12h") Time12.IsChecked = true; else Time24.IsChecked = true;
            AutostartCheck.IsChecked = _cfg.StartOnLogin;

            NotificationPreviewWindow.Dragged += OnGhostDragged;
            Closed += (s, e) =>
            {
                NotificationPreviewWindow.Dragged -= OnGhostDragged;
                NotificationPreviewWindow.Pinned = false;
                NotificationPreviewWindow.HideGhost();
            };

            _loading = false;
            ShowTab(false);
        }

        // ---- theme tokens --------------------------------------------------

        void ApplyTokens()
        {
            var app = FindResource("App") as SolidColorBrush;
            if (app == null || app.Color != Color.FromRgb(0xF2, 0xF3, 0xF5)) return; // dark is the default

            foreach (var t in LightTokens)
                Resources[t.Key] = new SolidColorBrush(Color.FromRgb(t.R, t.G, t.B));
        }

        // ---- tabs ----------------------------------------------------------

        void OnGeneralTab(object sender, MouseButtonEventArgs e) => ShowTab(false);

        void OnNotificationTab(object sender, MouseButtonEventArgs e) => ShowTab(true);

        void ShowTab(bool notification)
        {
            PanelGeneral.Visibility = notification ? Visibility.Collapsed : Visibility.Visible;
            PanelNotification.Visibility = notification ? Visibility.Visible : Visibility.Collapsed;

            TabGeneralText.FontWeight = notification ? FontWeights.Normal : FontWeights.SemiBold;
            TabGeneralText.Foreground = (Brush)FindResource(notification ? "TokMuted" : "Fg");
            TabGeneralBar.Visibility = notification ? Visibility.Collapsed : Visibility.Visible;
            TabNotificationText.FontWeight = notification ? FontWeights.SemiBold : FontWeights.Normal;
            TabNotificationText.Foreground = (Brush)FindResource(notification ? "Fg" : "TokMuted");
            TabNotificationBar.Visibility = notification ? Visibility.Visible : Visibility.Collapsed;

            if (notification)
            {
                if (_notifTouched) NotificationPreviewWindow.ShowGhost(PreviewConfig(), this);
            }
            else
            {
                PreviewToggle.IsChecked = false;
                NotificationPreviewWindow.Pinned = false;
                NotificationPreviewWindow.HideGhost();
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                Save();
                e.Handled = true;
            }
        }

        // ---- hotkey capture ------------------------------------------------

        void OnHotkeyKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.None) return;

            e.Handled = true;
            if (IsModifierKey(key)) return; // wait for the real key

            var box = (TextBox)sender;
            var mods = new System.Collections.Generic.List<string>();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods.Add("Ctrl");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods.Add("Alt");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods.Add("Shift");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods.Add("Win");

            string name = KeyName(key);
            if (string.IsNullOrEmpty(name)) return;

            box.Text = mods.Count > 0 ? string.Join("+", mods) + "+" + name : name;
            HotkeyError.Visibility = Visibility.Collapsed;
        }

        static bool IsModifierKey(Key key) =>
            key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin;

        static string KeyName(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9) return ((int)(key - Key.D0)).ToString();
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "NumPad" + (key - Key.NumPad0);
            switch (key)
            {
                case Key.OemTilde: return "`";
                case Key.OemMinus: return "-";
                case Key.OemPlus: return "=";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemSemicolon: return ";";
                case Key.OemQuotes: return "'";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "/";
                case Key.OemPipe: return "\\";
                case Key.Space: return "Space";
                default: return key.ToString();
            }
        }

        static bool IsModifierName(string part) =>
            part == "Ctrl" || part == "Control" || part == "Alt" ||
            part == "Shift" || part == "Win";

        /// <summary>One or more modifiers plus one non-modifier key.</summary>
        static bool IsValidCombo(string combo)
        {
            if (string.IsNullOrWhiteSpace(combo)) return false;
            var parts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Trim()).ToArray();
            if (parts.Length < 2) return false;
            return parts.Take(parts.Length - 1).All(IsModifierName) && !IsModifierName(parts[^1]);
        }

        // ---- numeric input -------------------------------------------------

        void OnNumberInput(object sender, TextCompositionEventArgs e)
        {
            var box = (TextBox)sender;
            bool signed = (box.Tag as string) == "signed";
            foreach (char c in e.Text)
            {
                bool ok = char.IsDigit(c) ||
                          (signed && c == '-' && box.CaretIndex == 0 && !box.Text.Contains('-'));
                if (!ok)
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        /// <summary>Marks the field's inline error and reports whether the value is out of range.</summary>
        bool Bad(TextBox box, TextBlock error, int min, int max, out int value)
        {
            if (int.TryParse(box.Text.Trim(), out value) && value >= min && value <= max) return false;
            error.Visibility = Visibility.Visible;
            return true;
        }

        // ---- live preview --------------------------------------------------

        void OnNotifChanged(object sender, RoutedEventArgs e) => PreviewTouched();

        void OnNotifTextChanged(object sender, TextChangedEventArgs e) => PreviewTouched();

        void PreviewTouched()
        {
            if (_loading) return;

            bool first = !_notifTouched;
            _notifTouched = true;
            var cfg = PreviewConfig();
            if (first) NotificationPreviewWindow.ShowGhost(cfg, this);
            else NotificationPreviewWindow.UpdateGhost(cfg);
        }

        void OnPreviewToggle(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            bool pinned = PreviewToggle.IsChecked == true;
            NotificationPreviewWindow.Pinned = pinned;
            if (pinned)
            {
                _notifTouched = true;
                NotificationPreviewWindow.ShowGhost(PreviewConfig(), this);
            }
            else
            {
                NotificationPreviewWindow.HideGhost();
            }
        }

        /// <summary>Unsaved notification values, clamped the way the ghost can draw them.</summary>
        Config PreviewConfig()
        {
            var d = _cfg.Notification ?? new Config.NotificationSettings();
            return new Config
            {
                Notification = new Config.NotificationSettings
                {
                    Enabled = NotifEnabled.IsChecked == true,
                    DurationMs = d.DurationMs,
                    Width = Live(WidthBox, 240, 800, d.Width),
                    Height = Live(HeightBox, 80, 500, d.Height),
                    Position = PositionOf(),
                    OffsetX = Live(OffsetXBox, -2000, 2000, d.OffsetX),
                    OffsetY = Live(OffsetYBox, -2000, 2000, d.OffsetY),
                    MaxLines = Live(MaxLinesBox, 1, 10, d.MaxLines),
                    ShowApp = d.ShowApp,
                    ShowTime = d.ShowTime
                }
            };
        }

        static int Live(TextBox box, int min, int max, int fallback) =>
            int.TryParse(box.Text.Trim(), out int v) ? Math.Max(min, Math.Min(max, v)) : fallback;

        void OnGhostDragged(int x, int y)
        {
            // The TextChanged handlers pick these up and redraw the ghost at the same place.
            OffsetXBox.Text = x.ToString();
            OffsetYBox.Text = y.ToString();
        }

        string PositionOf() =>
            PosTopLeft.IsChecked == true ? "top-left" :
            PosTopRight.IsChecked == true ? "top-right" :
            PosBottomLeft.IsChecked == true ? "bottom-left" :
            PosCenter.IsChecked == true ? "center" : "bottom-right";

        void SelectPosition(string position)
        {
            switch ((position ?? "bottom-right").ToLowerInvariant())
            {
                case "top-left": PosTopLeft.IsChecked = true; break;
                case "top-right": PosTopRight.IsChecked = true; break;
                case "bottom-left": PosBottomLeft.IsChecked = true; break;
                case "center": PosCenter.IsChecked = true; break;
                default: PosBottomRight.IsChecked = true; break;
            }
        }

        void SelectTheme(string theme)
        {
            if (theme == "light") ThemeLight.IsChecked = true;
            else if (theme == "system") ThemeSystem.IsChecked = true;
            else ThemeDark.IsChecked = true;
        }

        string ThemeOf() =>
            ThemeLight.IsChecked == true ? "light" :
            ThemeSystem.IsChecked == true ? "system" : "dark";

        // ---- history -------------------------------------------------------

        void OnClearClick(object sender, RoutedEventArgs e)
        {
            ClearIdle.Visibility = Visibility.Collapsed;
            ClearConfirm.Visibility = Visibility.Visible;
        }

        void OnKeepClick(object sender, RoutedEventArgs e)
        {
            ClearConfirm.Visibility = Visibility.Collapsed;
            ClearIdle.Visibility = Visibility.Visible;
        }

        void OnConfirmClearClick(object sender, RoutedEventArgs e)
        {
            ClearConfirm.Visibility = Visibility.Collapsed;
            ClearIdle.Visibility = Visibility.Visible;
            ClearHistoryRequested?.Invoke();
        }

        // ---- footer --------------------------------------------------------

        void OnSaveClick(object sender, RoutedEventArgs e) => Save();

        void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        void Save()
        {
            HideErrors();

            string hotkey = HotkeyBox.Text.Trim();
            string plain = PlainHotkeyBox.Text.Trim();

            string hotkeyError =
                !IsValidCombo(hotkey) ? "Open the list needs a modifier and a key, like Alt+V." :
                !IsValidCombo(plain) ? "Paste as plain text needs a modifier and a key, like Ctrl+Shift+V." :
                string.Equals(hotkey, plain, StringComparison.OrdinalIgnoreCase)
                    ? "That is already the shortcut for Open the list." : null;
            if (hotkeyError != null)
            {
                ShowTab(false);
                ShowHotkeyError(hotkeyError);
                return;
            }

            if (Bad(MaxItemsBox, MaxItemsError, 10, 10000, out int maxItems))
            {
                ShowTab(false);
                MaxItemsBox.Focus();
                MaxItemsBox.SelectAll();
                return;
            }
            if (Bad(PreviewLinesBox, PreviewLinesError, 1, 100, out int previewLines))
            {
                ShowTab(false);
                PreviewLinesBox.Focus();
                PreviewLinesBox.SelectAll();
                return;
            }

            if (Bad(DurationBox, DurationError, 500, 60000, out int duration) ||
                Bad(MaxLinesBox, MaxLinesError, 1, 10, out int maxLines) ||
                Bad(WidthBox, WidthError, 240, 800, out int width) ||
                Bad(HeightBox, HeightError, 80, 500, out int height) ||
                Bad(OffsetXBox, OffsetXError, -2000, 2000, out int offsetX) ||
                Bad(OffsetYBox, OffsetYError, -2000, 2000, out int offsetY))
            {
                ShowTab(true);
                FirstBadNotificationBox().Focus();
                return;
            }

            var n = _cfg.Notification ?? (_cfg.Notification = new Config.NotificationSettings());
            _cfg.Hotkey = hotkey;
            _cfg.HotkeyPlain = plain;
            _cfg.MaxItems = maxItems;
            _cfg.PreviewLines = previewLines;
            n.Enabled = NotifEnabled.IsChecked == true;
            n.DurationMs = duration;
            n.MaxLines = maxLines;
            n.Width = width;
            n.Height = height;
            n.Position = PositionOf();
            n.OffsetX = offsetX;
            n.OffsetY = offsetY;
            _cfg.Theme = ThemeOf();
            _cfg.TimeFormat = Time12.IsChecked == true ? "12h" : "24h";
            _cfg.StartOnLogin = AutostartCheck.IsChecked == true;

            Autostart.Set(_cfg.StartOnLogin);
            _cfg.Save();

            NotificationPreviewWindow.Pinned = false;
            NotificationPreviewWindow.HideGhost();
            SettingsSaved?.Invoke(_cfg);
            Close();
        }

        /// <summary>The first visible inline error, so a blocked save focuses something fixable.</summary>
        TextBox FirstBadNotificationBox()
        {
            if (DurationError.Visibility == Visibility.Visible) return DurationBox;
            if (MaxLinesError.Visibility == Visibility.Visible) return MaxLinesBox;
            if (WidthError.Visibility == Visibility.Visible) return WidthBox;
            if (HeightError.Visibility == Visibility.Visible) return HeightBox;
            if (OffsetXError.Visibility == Visibility.Visible) return OffsetXBox;
            return OffsetYBox;
        }

        void ShowHotkeyError(string message)
        {
            HotkeyError.Text = message;
            HotkeyError.Visibility = Visibility.Visible;
        }

        void HideErrors()
        {
            foreach (var error in new[]
                     {
                         HotkeyError, MaxItemsError, PreviewLinesError, DurationError, MaxLinesError,
                         WidthError, HeightError, OffsetXError, OffsetYError
                     })
                error.Visibility = Visibility.Collapsed;
        }
    }
}
