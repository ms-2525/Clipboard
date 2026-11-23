using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace Clip.UI
{
    /// <summary>
    /// The formatting toolbar of the F2 editor: every button acts on the selection and reflects the
    /// caret's effective formatting (docs\DESIGN-v2-directives.md §5).
    /// </summary>
    public partial class EditorWindow
    {
        static readonly double[] Sizes = { 10, 11, 12, 13, 14, 16, 18, 20, 24, 28, 32, 40 };

        /// <summary>RTF sizes are points; WPF text properties are DIPs (96/72).</summary>
        const double DipPerPoint = 96.0 / 72.0;

        static object Prop(TextRange selection, DependencyProperty property) => selection.GetPropertyValue(property);

        void OnSelectionChanged(object sender, RoutedEventArgs e) => Reflect();

        /// <summary>A native Ctrl+B/I/U changes the document without moving the selection.</summary>
        void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Reflect();

        /// <summary>
        /// A collapsed caret has no properties of its own, so reflection reads the character next to
        /// it — forward first, because that is the ink the next keystroke will take.
        /// </summary>
        TextRange Probe()
        {
            var selection = RichBox.Selection;
            if (!selection.IsEmpty) return selection;
            var caret = RichBox.CaretPosition;
            for (int i = 1; i <= 2; i++)
            {
                var next = caret.GetPositionAtOffset(i, LogicalDirection.Forward);
                if (next == null) break;
                var probe = new TextRange(caret, next);
                if (probe.Text.Trim().Length != 0) return probe;
            }
            for (int i = 1; i <= 2; i++)
            {
                var previous = caret.GetPositionAtOffset(-i, LogicalDirection.Backward);
                if (previous == null) break;
                var probe = new TextRange(previous, caret);
                if (probe.Text.Trim().Length != 0) return probe;
            }
            return selection;
        }

        /// <summary>Mirror the caret's effective formatting onto the buttons (WordPad behaviour).</summary>
        void Reflect()
        {
            if (_reflecting) return;
            TextRange selection = Probe();

            object weight = Prop(selection, TextElement.FontWeightProperty);
            BoldButton.IsChecked = weight is FontWeight w && w.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();

            object style = Prop(selection, TextElement.FontStyleProperty);
            ItalicButton.IsChecked = style is FontStyle s && s != FontStyles.Normal;

            UnderlineButton.IsChecked = Decorated(selection, TextDecorationLocation.Underline);
            StrikeButton.IsChecked = Decorated(selection, TextDecorationLocation.Strikethrough);

            if (Prop(selection, TextElement.FontSizeProperty) is double size)
                SizeText.Text = Math.Round(size / DipPerPoint).ToString();

            if (Prop(selection, TextElement.ForegroundProperty) is Brush ink) ColorBar.Background = ink;

            // A hollow chip means "no highlight", so the slash is what tells the two apart.
            var mark = Prop(selection, TextElement.BackgroundProperty) as Brush;
            bool noMark = !(mark is SolidColorBrush solid) || solid.Color.A == 0;
            MarkChip.Background = noMark ? Brushes.Transparent : mark;
            MarkSlash.Visibility = noMark ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// The RTF loader parks underline/strike on a wrapper Span and TextRange.GetPropertyValue does
        /// not see it, so decorations are read off the element chain at both ends of the range.
        /// </summary>
        static TextDecorationCollection DecorationsAt(TextPointer position)
        {
            for (var element = position?.Parent as TextElement; element != null; element = element.Parent as TextElement)
                if (element.ReadLocalValue(Inline.TextDecorationsProperty) is TextDecorationCollection found) return found;
            return null;
        }

        bool Decorated(TextRange selection, TextDecorationLocation location)
        {
            var last = selection.End.GetPositionAtOffset(-1, LogicalDirection.Backward) ?? selection.Start;
            return HasDecoration(DecorationsAt(selection.Start), location)
                && HasDecoration(DecorationsAt(last), location);
        }

        static bool HasDecoration(TextDecorationCollection decorations, TextDecorationLocation location) =>
            decorations != null && decorations.Any(d => d.Location == location);

        /// <summary>Apply on the selection, then take the caret back and refresh the buttons.</summary>
        void Apply(DependencyProperty property, object value)
        {
            _reflecting = true;
            try { RichBox.Selection.ApplyPropertyValue(property, value); }
            finally { _reflecting = false; }
            RichBox.Focus();
            Reflect();
        }

        // shortcut: a collapsed caret cannot receive a range property, so a toolbar press there is a
        // no-op until text is selected; upgrade to typed-formatting state if the owner asks for it.
        void OnBoldClick(object sender, RoutedEventArgs e) =>
            Apply(TextElement.FontWeightProperty, BoldButton.IsChecked == true ? FontWeights.Bold : FontWeights.Normal);

        void OnItalicClick(object sender, RoutedEventArgs e) =>
            Apply(TextElement.FontStyleProperty, ItalicButton.IsChecked == true ? FontStyles.Italic : FontStyles.Normal);

        void OnUnderlineClick(object sender, RoutedEventArgs e) =>
            ToggleDecoration(TextDecorationLocation.Underline, UnderlineButton.IsChecked == true);

        void OnStrikeClick(object sender, RoutedEventArgs e) =>
            ToggleDecoration(TextDecorationLocation.Strikethrough, StrikeButton.IsChecked == true);

        /// <summary>Underline and strikethrough share one property, so the other one is preserved.</summary>
        void ToggleDecoration(TextDecorationLocation location, bool on)
        {
            var selection = RichBox.Selection;
            if (selection.IsEmpty) return;

            var kept = new List<TextDecoration>();
            if (DecorationsAt(selection.Start) is TextDecorationCollection current)
                kept.AddRange(current.Where(d => d.Location != location));
            if (on) kept.Add(location == TextDecorationLocation.Underline ? TextDecorations.Underline[0] : TextDecorations.Strikethrough[0]);

            _reflecting = true;
            try
            {
                try
                {
                    // Re-wrap the range in a Span we own: that is the one form of "no decoration" WPF
                    // accepts here (an empty collection through ApplyPropertyValue is ignored).
                    new Span(selection.Start, selection.End).TextDecorations = new TextDecorationCollection(kept);
                }
                catch (ArgumentException)
                {
                    // A selection crossing paragraphs cannot be wrapped; set-only fallback.
                    if (kept.Count != 0) selection.ApplyPropertyValue(Inline.TextDecorationsProperty, new TextDecorationCollection(kept));
                }
            }
            finally { _reflecting = false; }
            RichBox.Focus();
            Reflect();
        }

        void OnSmallerClick(object sender, RoutedEventArgs e) => StepSize(-1);

        void OnLargerClick(object sender, RoutedEventArgs e) => StepSize(+1);

        void StepSize(int delta)
        {
            double current = Prop(RichBox.Selection, TextElement.FontSizeProperty) is double size ? size / DipPerPoint : 13;
            int index = 0;
            for (int i = 0; i < Sizes.Length; i++)
                if (Math.Abs(Sizes[i] - current) < Math.Abs(Sizes[index] - current)) index = i;
            index = Math.Clamp(index + delta, 0, Sizes.Length - 1);
            Apply(TextElement.FontSizeProperty, Sizes[index] * DipPerPoint);
        }

        void OnClearClick(object sender, RoutedEventArgs e)
        {
            _reflecting = true;
            try
            {
                RichBox.BeginChange();
                var selection = RichBox.Selection;
                selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                if (!selection.IsEmpty) new Span(selection.Start, selection.End).TextDecorations = new TextDecorationCollection();
                selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
                selection.ApplyPropertyValue(TextElement.ForegroundProperty, TryFindResource("Fg75") as Brush ?? Brushes.White);
                selection.ApplyPropertyValue(TextElement.FontSizeProperty, RichBox.FontSize);
                selection.ApplyPropertyValue(TextElement.FontFamilyProperty, RichBox.FontFamily);
                RichBox.EndChange();
            }
            finally { _reflecting = false; }
            RichBox.Focus();
            Reflect();
        }

        void OnColorButtonClick(object sender, RoutedEventArgs e) => OpenAt(ColorPopup);

        void OnMarkButtonClick(object sender, RoutedEventArgs e) => OpenAt(MarkPopup);

        void OpenAt(Popup popup)
        {
            ColorPopup.IsOpen = popup == ColorPopup;
            MarkPopup.IsOpen = popup == MarkPopup;
        }

        void OnTextColorSwatch(object sender, RoutedEventArgs e)
        {
            ColorPopup.IsOpen = false;
            if (TryFindResource((string)((FrameworkElement)sender).Tag) is Brush ink)
                Apply(TextElement.ForegroundProperty, ink);
        }

        /// <summary>
        /// A dark-theme highlight only reads with dark ink on it, so applying one also sets the ink to
        /// OnAccent and clearing it restores the editor's default ink.
        /// </summary>
        void OnMarkSwatch(object sender, RoutedEventArgs e)
        {
            MarkPopup.IsOpen = false;
            string key = (string)((FrameworkElement)sender).Tag;
            if (key == "none")
            {
                Apply(TextElement.BackgroundProperty, Brushes.Transparent);
                Apply(TextElement.ForegroundProperty, TryFindResource("Fg75") as Brush ?? Brushes.White);
                return;
            }
            if (TryFindResource(key) is not Brush mark) return;
            Apply(TextElement.BackgroundProperty, mark);
            Apply(TextElement.ForegroundProperty, TryFindResource("OnAccent") as Brush ?? Brushes.Black);
        }
    }
}
