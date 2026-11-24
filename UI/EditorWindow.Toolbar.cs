using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clip.Core;
using Syncfusion.Windows.Controls.RichTextBoxAdv;

namespace Clip.UI
{
    /// <summary>
    /// The formatting toolbar of the F2 editor: every button writes the control's selection
    /// formatting and reflects the caret's effective formatting (docs\DESIGN-v2-directives.md §5).
    /// </summary>
    public partial class EditorWindow
    {
        int _caseIndex = -1;

        static readonly double[] Sizes = { 12, 14, 16, 18, 20, 22, 24, 28, 32, 40 };

        /// <summary>RTF sizes are points; the control's FontSize is a DIP (96/72).</summary>
        const double DipPerPoint = 96.0 / 72.0;

        void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => Reflect();

        /// <summary>A native Ctrl+B/I/U or typing changes the document without moving the selection.</summary>
        void OnContentChanged(object sender, ContentChangedEventArgs e) => Reflect();

        static Color Ink(object brush) => brush is SolidColorBrush solid ? solid.Color : default;

        /// <summary>The control highlights with Word's fixed palette, so each theme mark maps to its nearest swatch.</summary>
        static HighlightColor Mark(string tag) => tag switch
        {
            "Amber" => HighlightColor.Yellow,
            "Sage" => HighlightColor.BrightGreen,
            "Sky" => HighlightColor.Turquoise,
            "Violet" => HighlightColor.Violet,
            _ => HighlightColor.NoColor,
        };

        Brush MarkBrush(HighlightColor? mark) => mark switch
        {
            HighlightColor.Yellow => (Brush)TryFindResource("Amber"),
            HighlightColor.BrightGreen => (Brush)TryFindResource("Sage"),
            HighlightColor.Turquoise => (Brush)TryFindResource("Sky"),
            HighlightColor.Violet => (Brush)TryFindResource("Violet"),
            null or HighlightColor.NoColor => Brushes.Transparent,
            _ => (Brush)TryFindResource("Fg48"),
        };

        /// <summary>Mirror the caret's effective formatting onto the buttons (WordPad behaviour).</summary>
        void Reflect()
        {
            if (_reflecting) return;
            var format = RichBox.Selection.CharacterFormat;

            BoldButton.IsChecked = format.Bold == true;
            ItalicButton.IsChecked = format.Italic == true;
            UnderlineButton.IsChecked = format.Underline is Underline underline && underline != Underline.None;
            StrikeButton.IsChecked = format.StrikeThrough is StrikeThrough strike && strike != StrikeThrough.None;

            if (format.FontSize > 0) SizeText.Text = Math.Round(format.FontSize / DipPerPoint).ToString();
            if (format.FontColor is Color ink) ColorBar.Background = new SolidColorBrush(ink);
            // The bar carries the caret's highlight; an empty bar reads as "no highlight".
            MarkChip.Background = MarkBrush(format.HighlightColor);
        }

        /// <summary>Apply on the selection, then take the caret back and refresh the buttons.</summary>
        void Apply(Action<SelectionCharacterFormat> change)
        {
            _reflecting = true;
            try { change(RichBox.Selection.CharacterFormat); }
            finally { _reflecting = false; }
            RichBox.Focus();
            Reflect();
        }

        void OnBoldClick(object sender, RoutedEventArgs e) => Apply(f => f.Bold = BoldButton.IsChecked == true);

        void OnItalicClick(object sender, RoutedEventArgs e) => SetItalic(ItalicButton.IsChecked == true);

        /// <summary>
        /// The control mishandles "italic off" on a mixed selection: one write makes every run italic
        /// instead of plain, and a second write lands on the value the first one put there.
        /// </summary>
        void SetItalic(bool on)
        {
            Apply(f => f.Italic = on);
            if (!on) Apply(f => f.Italic = false);
        }

        void OnUnderlineClick(object sender, RoutedEventArgs e) =>
            Apply(f => f.Underline = UnderlineButton.IsChecked == true ? Underline.Single : Underline.None);

        void OnStrikeClick(object sender, RoutedEventArgs e) =>
            Apply(f => f.StrikeThrough = StrikeButton.IsChecked == true ? StrikeThrough.SingleStrike : StrikeThrough.None);

        void OnSmallerClick(object sender, RoutedEventArgs e) => StepSize(-1);

        void OnLargerClick(object sender, RoutedEventArgs e) => StepSize(+1);

        void StepSize(int delta)
        {
            double current = RichBox.Selection.CharacterFormat.FontSize / DipPerPoint;
            int index = 0;
            for (int i = 0; i < Sizes.Length; i++)
                if (Math.Abs(Sizes[i] - current) < Math.Abs(Sizes[index] - current)) index = i;
            index = Math.Clamp(index + delta, 0, Sizes.Length - 1);
            double size = Sizes[index] * DipPerPoint;
            Apply(f => f.FontSize = size);
        }

        void OnClearClick(object sender, RoutedEventArgs e)
        {
            double size = RichBox.FontSize;
            var family = RichBox.FontFamily;
            var ink = Ink(TryFindResource("Fg75"));
            Apply(f =>
            {
                f.Bold = false;
                f.Underline = Underline.None;
                f.StrikeThrough = StrikeThrough.None;
                f.HighlightColor = HighlightColor.NoColor;
                f.FontColor = ink;
                f.FontSize = size;
                f.FontFamily = family;
            });
            SetItalic(false);
        }

        void OnColorButtonClick(object sender, RoutedEventArgs e) => OpenAt(ColorPopup);

        void OnMarkButtonClick(object sender, RoutedEventArgs e) => OpenAt(MarkPopup);

        void OnTextColorSwatch(object sender, RoutedEventArgs e)
        {
            ColorPopup.IsOpen = false;
            if (TryFindResource((string)((FrameworkElement)sender).Tag) is SolidColorBrush ink)
                Apply(f => f.FontColor = ink.Color);
        }

        /// <summary>
        /// A dark-theme highlight only reads with dark ink on it, so applying one also sets the ink to
        /// OnAccent and clearing it restores the editor's default ink.
        /// </summary>
        void OnMarkSwatch(object sender, RoutedEventArgs e)
        {
            MarkPopup.IsOpen = false;
            var mark = Mark((string)((FrameworkElement)sender).Tag);
            var ink = Ink(TryFindResource(mark == HighlightColor.NoColor ? "Fg75" : "OnAccent"));
            Apply(f =>
            {
                f.HighlightColor = mark;
                f.FontColor = ink;
            });
        }

        /// <summary>Join the clip into one line: each paragraph break becomes one space, formats stay on their runs.</summary>
        void OnSingleLineClick(object sender, RoutedEventArgs e)
        {
            foreach (SectionAdv section in RichBox.Document.Sections)
            {
                var paragraphs = section.Blocks.OfType<ParagraphAdv>().ToList();
                // A table (or any non-paragraph block) has no inline-move path: plain-text join instead.
                if (section.Blocks.Count != paragraphs.Count) { JoinAsPlainText(); return; }

                var target = paragraphs.FirstOrDefault(p => ParagraphText(p).Trim().Length != 0);
                if (target == null) { DocumentText(""); AfterSingleLine(); return; }

                foreach (var paragraph in paragraphs)
                {
                    if (paragraph == target) { TrimEdges(paragraph); continue; }
                    if (ParagraphText(paragraph).Trim().Length == 0) { section.Blocks.Remove(paragraph); continue; }
                    TrimEdges(paragraph);
                    target.Inlines.Add(new SpanAdv { Text = " " });
                    foreach (Inline inline in paragraph.Inlines.ToList())
                    {
                        paragraph.Inlines.Remove(inline);
                        target.Inlines.Add(inline is BreakAdv ? new SpanAdv { Text = " " } : inline);
                    }
                    section.Blocks.Remove(paragraph);
                }
            }
            AfterSingleLine();
        }

        /// <summary>Trim the edge runs so joined paragraphs get exactly one separator space.</summary>
        static void TrimEdges(ParagraphAdv paragraph)
        {
            var spans = paragraph.Inlines.OfType<SpanAdv>().ToList();
            if (spans.Count == 0) return;
            spans[0].Text = spans[0].Text.TrimStart();
            spans[^1].Text = spans[^1].Text.TrimEnd();
        }

        /// <summary>Each line trimmed, blank lines dropped, joined with one space.</summary>
        static string JoinLines(string text) =>
            string.Join(" ", (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0));

        void JoinAsPlainText() => DocumentText(JoinLines(CurrentText()));

        void AfterSingleLine()
        {
            var end = RichBox.Document.DocumentEnd;
            RichBox.Selection.Select(end, end);
            RichBox.Focus();
        }

        void OnCaseClick(object sender, RoutedEventArgs e)
        {
            var selection = RichBox.Selection;
            // Nothing selected: the whole clip is the target, the same scope as Clear and Single line.
            bool whole = selection.IsEmpty;

            _caseIndex = (_caseIndex + 1) % TextCase.Variants;
            int variant = _caseIndex;
            Func<string, string> recase = text => TextCase.Apply(text, variant);

            _reflecting = true;
            try
            {
                if (!whole)
                {
                    // The control has no per-run API for a partial range, so the selected text is
                    // replaced through its text API and takes the first run's formatting.
                    selection.InsertText(recase(selection.Text));
                }
                else if (!RecaseWholeClip(recase))
                {
                    selection.Select(RichBox.Document.DocumentStart, RichBox.Document.DocumentEnd);
                    selection.InsertText(recase(selection.Text));
                }
            }
            finally { _reflecting = false; }

            if (whole)
            {
                var end = RichBox.Document.DocumentEnd;
                selection.Select(end, end);
            }
            RichBox.Focus();
            Reflect();
        }

        /// <summary>
        /// Whole-clip recase that keeps bold/italic/links: a variant maps one char to one char, so the
        /// transform runs once over the whole clip and each run takes back its own slice. False means
        /// the variant changed the length or a block holds something other than plain runs.
        /// </summary>
        bool RecaseWholeClip(Func<string, string> transform)
        {
            var paragraphs = Paragraphs(RichBox.Document);
            if (paragraphs.Count == 0) return false;
            if (paragraphs.Any(p => p.Inlines.Any(i => i is not SpanAdv))) return false;

            string before = string.Join("\n", paragraphs.Select(
                p => string.Concat(p.Inlines.OfType<SpanAdv>().Select(s => s.Text))));
            string after = transform(before);
            if (after.Length != before.Length) return false;

            int at = 0;
            foreach (var paragraph in paragraphs)
            {
                foreach (var span in paragraph.Inlines.OfType<SpanAdv>())
                {
                    span.Text = after.Substring(at, span.Text.Length);
                    at += span.Text.Length;
                }
                at++; // the paragraph break the join above put between paragraphs
            }
            return true;
        }

        void OpenAt(Popup popup)
        {
            ColorPopup.IsOpen = popup == ColorPopup;
            MarkPopup.IsOpen = popup == MarkPopup;
        }
    }
}
