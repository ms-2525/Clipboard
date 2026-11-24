using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Clip.UI
{
    /// <summary>
    /// The F2 editor's formatting toolbar: every button runs one TipTap command in the page, and the
    /// page posts the caret's marks back so the buttons mirror it (docs\DESIGN-v2-directives.md §5).
    /// </summary>
    public partial class EditorWindow
    {
        static readonly double[] Sizes = { 12, 14, 16, 18, 20, 22, 24, 28, 32, 40 };

        /// <summary>The page's last reported selection size, in points.</summary>
        double _sizePt = 16;

        static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        static bool Flag(JsonElement selection, string name) =>
            selection.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

        static Color? ColorOf(JsonElement selection, string name)
        {
            if (!selection.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
            string hex = value.GetString();
            if (string.IsNullOrEmpty(hex)) return null;
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch
            {
                return null; // a colour string the page mangled must not take the toolbar down
            }
        }

        static Color Ink(object brush) => brush is SolidColorBrush solid ? solid.Color : Colors.Black;

        /// <summary>Mirror the caret's marks onto the buttons (WordPad behaviour).</summary>
        void ReflectSel(JsonElement selection)
        {
            BoldButton.IsChecked = Flag(selection, "bold");
            ItalicButton.IsChecked = Flag(selection, "italic");
            UnderlineButton.IsChecked = Flag(selection, "underline");
            StrikeButton.IsChecked = Flag(selection, "strike");

            if (selection.TryGetProperty("size", out var size) && size.TryGetDouble(out double points) && points > 0)
                _sizePt = points;
            SizeText.Text = Math.Round(_sizePt).ToString();

            // No colour mark means the page draws the theme ink, so the bar shows that same ink.
            ColorBar.Background = new SolidColorBrush(ColorOf(selection, "color") ?? Ink(TryFindResource("ContentInk")));
            MarkChip.Background = ColorOf(selection, "highlight") is Color mark
                ? new SolidColorBrush(mark)
                : Brushes.Transparent;
        }

        /// <summary>One bridge command; the page answers with a fresh "sel" when it is done.</summary>
        async void Send(string cmd, object arg = null)
        {
            string argJson = arg == null ? "null" : JsonSerializer.Serialize(arg);
            try
            {
                await Web.ExecuteScriptAsync(
                    "window.clipBridge.run(" + JsonSerializer.Serialize(cmd) + ", " + argJson + ")");
            }
            catch
            {
                // The page is closing or refused the command: the buttons keep the page's last reported state.
            }
        }

        void OnBoldClick(object sender, RoutedEventArgs e) => Send("bold");

        void OnItalicClick(object sender, RoutedEventArgs e) => Send("italic");

        void OnUnderlineClick(object sender, RoutedEventArgs e) => Send("underline");

        void OnStrikeClick(object sender, RoutedEventArgs e) => Send("strike");

        void OnSmallerClick(object sender, RoutedEventArgs e) => StepSize(-1);

        void OnLargerClick(object sender, RoutedEventArgs e) => StepSize(+1);

        /// <summary>The size steps are the same ladder the old editor offered; the caret picks the start rung.</summary>
        void StepSize(int delta)
        {
            int index = 0;
            for (int i = 0; i < Sizes.Length; i++)
                if (Math.Abs(Sizes[i] - _sizePt) < Math.Abs(Sizes[index] - _sizePt)) index = i;
            index = Math.Clamp(index + delta, 0, Sizes.Length - 1);
            Send("size", Sizes[index]);
        }

        void OnClearClick(object sender, RoutedEventArgs e) => Send("clearFormat");

        void OnSingleLineClick(object sender, RoutedEventArgs e) => Send("singleLine");

        void OnCaseClick(object sender, RoutedEventArgs e) => Send("case");

        void OnColorButtonClick(object sender, RoutedEventArgs e) => OpenAt(ColorPopup);

        void OnMarkButtonClick(object sender, RoutedEventArgs e) => OpenAt(MarkPopup);

        void OnTextColorSwatch(object sender, RoutedEventArgs e)
        {
            ColorPopup.IsOpen = false;
            if (TryFindResource((string)((FrameworkElement)sender).Tag) is SolidColorBrush ink)
                Send("color", Hex(ink.Color));
        }

        /// <summary>
        /// A highlight needs ink that reads on its fill: the canvas colour is the one value guaranteed to
        /// contrast with the theme's fills (white on the light theme's dark tints, dark on the dark theme's
        /// pastels). Clearing it restores the canvas ink.
        /// </summary>
        void OnMarkSwatch(object sender, RoutedEventArgs e)
        {
            MarkPopup.IsOpen = false;
            string tag = (string)((FrameworkElement)sender).Tag;
            if (tag == "none")
            {
                Send("highlight", new { color = (string)null, ink = ThemeHex("ContentInk") });
                return;
            }
            Send("highlight", new { color = ThemeHex(tag), ink = ThemeHex("ContentCanvas") });
        }

        void OpenAt(Popup popup)
        {
            ColorPopup.IsOpen = popup == ColorPopup;
            MarkPopup.IsOpen = popup == MarkPopup;
        }
    }
}
