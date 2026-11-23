using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clip.Core;

namespace Clip.UI
{
    public partial class SettingsWindow : Window
    {
        readonly Config _cfg;

        /// <summary>Raised after the live config was mutated and saved to disk.</summary>
        public event Action<Config> SettingsSaved;

        /// <summary>Raised when the user confirms the clear-history action.</summary>
        public event Action ClearHistoryRequested;

        public SettingsWindow(Config cfg)
        {
            InitializeComponent();
            _cfg = cfg ?? new Config();
            Title = Assembly.GetEntryAssembly().GetName().Name;

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
            SelectByTag(PositionCombo, n.Position ?? "bottom-right");
            SelectByTag(ThemeCombo, _cfg.Theme ?? "dark");
            SelectByTag(TimeCombo, string.IsNullOrEmpty(_cfg.TimeFormat) ? "24h" : _cfg.TimeFormat);
            AutostartCheck.IsChecked = _cfg.StartOnLogin;
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

        static int Clamp(TextBox box, int min, int max, int fallback)
        {
            int v = int.TryParse(box.Text.Trim(), out int parsed) ? parsed : fallback;
            v = Math.Max(min, Math.Min(max, v));
            box.Text = v.ToString();
            return v;
        }

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
            HiddenErrors();

            string popup = HotkeyBox.Text.Trim();
            string plain = PlainHotkeyBox.Text.Trim();

            if (!IsValidCombo(popup))
            {
                ShowHotkeyError("The popup hotkey needs a modifier and a key, e.g. Alt+V.");
                return;
            }
            if (!IsValidCombo(plain))
            {
                ShowHotkeyError("The plain-text hotkey needs a modifier and a key, e.g. Ctrl+Shift+V.");
                return;
            }
            if (string.Equals(popup, plain, StringComparison.OrdinalIgnoreCase))
            {
                ShowHotkeyError("Must differ from the popup hotkey.");
                return;
            }

            if (!int.TryParse(MaxItemsBox.Text.Trim(), out int maxItems) ||
                maxItems < 10 || maxItems > 10000)
            {
                MaxItemsError.Visibility = Visibility.Visible;
                MaxItemsBox.Focus();
                return;
            }

            if (!int.TryParse(PreviewLinesBox.Text.Trim(), out int previewLines) ||
                previewLines < 1 || previewLines > 100)
            {
                PreviewLinesError.Visibility = Visibility.Visible;
                PreviewLinesBox.Focus();
                return;
            }

            HiddenErrors();
            MaxItemsBox.Text = maxItems.ToString();
            PreviewLinesBox.Text = previewLines.ToString();

            var n = _cfg.Notification ?? (_cfg.Notification = new Config.NotificationSettings());
            _cfg.Hotkey = popup;
            _cfg.HotkeyPlain = plain;
            _cfg.MaxItems = maxItems;
            _cfg.PreviewLines = previewLines;
            n.Enabled = NotifEnabled.IsChecked == true;
            n.DurationMs = Clamp(DurationBox, 500, 60000, n.DurationMs);
            n.Width = Clamp(WidthBox, 240, 800, n.Width);
            n.Height = Clamp(HeightBox, 80, 500, n.Height);
            n.Position = TagOf(PositionCombo, n.Position);
            n.OffsetX = Clamp(OffsetXBox, -2000, 2000, n.OffsetX);
            n.OffsetY = Clamp(OffsetYBox, -2000, 2000, n.OffsetY);
            n.MaxLines = Clamp(MaxLinesBox, 1, 10, n.MaxLines);
            _cfg.Theme = TagOf(ThemeCombo, _cfg.Theme);
            _cfg.TimeFormat = TagOf(TimeCombo, "24h");
            _cfg.StartOnLogin = AutostartCheck.IsChecked == true;

            Autostart.Set(_cfg.StartOnLogin);
            _cfg.Save();
            SettingsSaved?.Invoke(_cfg);
            Close();
        }

        void ShowHotkeyError(string message)
        {
            HotkeyError.Text = message;
            HotkeyError.Visibility = Visibility.Visible;
        }

        void HiddenErrors()
        {
            MaxItemsError.Visibility = Visibility.Collapsed;
            PreviewLinesError.Visibility = Visibility.Collapsed;
            HotkeyError.Visibility = Visibility.Collapsed;
        }

        static void SelectByTag(ComboBox combo, string value)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (string.Equals(item.Tag as string, value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        static string TagOf(ComboBox combo, string fallback) =>
            (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;
    }
}
