using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Clip.Core;

namespace Clip.UI
{
    /// <summary>
    /// The formatting toolbar of the F2 editor: every button acts on the selection and reflects the
    /// caret's effective formatting (docs\DESIGN-v2-directives.md §5).
    /// </summary>
    public partial class EditorWindow
    {
        int _caseIndex = -1;

        static readonly double[] Sizes = { 12, 14, 16, 18, 20, 22, 24, 28, 32, 40 };

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

            // The bar carries the caret's highlight; an empty bar reads as "no highlight".
            var mark = Prop(selection, TextElement.BackgroundProperty) as Brush;
            bool noMark = !(mark is SolidColorBrush solid) || solid.Color.A == 0;
            MarkChip.Background = noMark ? Brushes.Transparent : mark;
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
            double current = Prop(RichBox.Selection, TextElement.FontSizeProperty) is double size ? size / DipPerPoint : 16;
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

        /// <summary>Join the whole clip into one line: every paragraph/line break becomes one space.</summary>
        void OnSingleLineClick(object sender, RoutedEventArgs e)
        {
            var doc = RichBox.Document;
            // Exotic blocks (lists/tables) have no inline-move path: plain-text join instead.
            if (doc.Blocks.Any(b => b is not Paragraph))
            {
                DocumentText(JoinLines(Body().Text));
                AfterSingleLine();
                return;
            }
            var paras = doc.Blocks.OfType<Paragraph>().ToList();
            if (paras.Count == 0) return;
            var nonEmpty = paras.Where(p => !string.IsNullOrWhiteSpace(new TextRange(p.ContentStart, p.ContentEnd).Text)).ToList();
            if (nonEmpty.Count == 0) { DocumentText(""); AfterSingleLine(); return; }

            RichBox.BeginChange();
            try
            {
                if (nonEmpty.Count == 1)
                {
                    var only = nonEmpty[0];
                    foreach (var lb in only.Inlines.OfType<LineBreak>().ToList())
                    {
                        only.Inlines.InsertBefore(lb, new Run(" "));
                        only.Inlines.Remove(lb);
                    }
                    TrimParaEdges(only);
                }
                else
                {
                    var single = new Paragraph { Margin = new Thickness(0) };
                    bool first = true;
                    foreach (var p in nonEmpty)
                    {
                        TrimParaEdges(p);
                        if (!first) single.Inlines.Add(new Run(" "));
                        first = false;
                        foreach (var inline in p.Inlines.ToList())
                        {
                            if (inline is LineBreak) { single.Inlines.Add(new Run(" ")); continue; }
                            p.Inlines.Remove(inline);
                            single.Inlines.Add(inline);
                        }
                    }
                    TrimParaEdges(single);
                    doc.Blocks.Clear();
                    doc.Blocks.Add(single);
                }
            }
            finally { RichBox.EndChange(); }
            AfterSingleLine();
        }

        /// <summary>Each line trimmed, blank lines dropped, joined with one space.</summary>
        static string JoinLines(string text) =>
            string.Join(" ", (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0));

        /// <summary>Trim the edge runs so joined paragraphs get exactly one separator space.</summary>
        static void TrimParaEdges(Paragraph p)
        {
            if (p.Inlines.FirstInline is Run first)
            {
                first.Text = first.Text.TrimStart();
                if (first.Text.Length == 0 && first.NextInline != null) p.Inlines.Remove(first);
            }
            if (p.Inlines.LastInline is Run last)
            {
                last.Text = last.Text.TrimEnd();
                if (last.Text.Length == 0 && last.PreviousInline != null) p.Inlines.Remove(last);
            }
        }

        void AfterSingleLine()
        {
            RichBox.CaretPosition = RichBox.Document.ContentEnd;
            RichBox.Focus();
        }

        void OnCaseClick(object sender, RoutedEventArgs e)
        {
            // Nothing selected: the whole clip is the target, the same scope as Clear and Single line.
            bool whole = RichBox.Selection.IsEmpty;
            var range = whole
                ? new TextRange(RichBox.Document.ContentStart, RichBox.Document.ContentEnd)
                : RichBox.Selection;
            var start = range.Start;
            var end = range.End;

            _caseIndex = (_caseIndex + 1) % TextCase.Variants;
            int variant = _caseIndex;

            _reflecting = true;
            RichBox.BeginChange();
            try { RewriteRuns(RichBox.Document, range, text => TextCase.Apply(text, variant)); }
            finally { RichBox.EndChange(); _reflecting = false; }

            // Keep the same words selected, so the second and third press cycle them rather than
            // suddenly reaching the whole clip.
            if (!whole)
            {
                try { RichBox.Selection.Select(start, end); }
                catch (ArgumentException)
                {
                    // The plain-text fallback replaced the range; its old end points are gone.
                }
            }
            RichBox.Focus();
            Reflect();
        }

        /// <summary>
        /// Rewrite the text the range covers one run at a time, so bold/italic/links survive. The
        /// transform sees the whole selection at once: Title Case must not restart at every formatting
        /// boundary. Paragraphs are separated by a newline in that text so two paragraphs never read as
        /// one word. Every run is collected before the first write - editing inside a live WPF text
        /// enumerator is what made this throw "Collection was modified".
        /// </summary>
        static void RewriteRuns(FlowDocument doc, TextRange range, Func<string, string> transform)
        {
            if (doc == null) return;

            var parts = new List<(Run Run, int Skip, int Take, int At)>();
            var text = new StringBuilder();
            Paragraph lastParagraph = null;

            foreach (Run run in RunsIn(doc.Blocks))
            {
                var os = run.ContentStart.CompareTo(range.Start) < 0 ? range.Start : run.ContentStart;
                var oe = run.ContentEnd.CompareTo(range.End) > 0 ? range.End : run.ContentEnd;
                if (os.CompareTo(oe) >= 0) continue;

                string whole = run.Text;
                int skip = Math.Clamp(run.ContentStart.GetOffsetToPosition(os), 0, whole.Length);
                int take = Math.Clamp(run.ContentStart.GetOffsetToPosition(oe), skip, whole.Length) - skip;
                if (take == 0) continue;

                Paragraph paragraph = ParagraphOf(run);
                if (lastParagraph != null && paragraph != lastParagraph) text.Append('\n');
                lastParagraph = paragraph;

                parts.Add((run, skip, take, text.Length));
                text.Append(whole, skip, take);
            }
            if (parts.Count == 0) return;

            string before = text.ToString();
            string after = transform(before);
            // A variant that changed the length would smear one run's share into the next: an exotic
            // clip takes the plain-text path instead of losing formatting silently.
            if (after.Length != before.Length)
            {
                range.Text = after;
                return;
            }

            foreach (var (run, skip, take, at) in parts)
            {
                string whole = run.Text;
                run.Text = whole.Substring(0, skip) + after.Substring(at, take) + whole.Substring(skip + take);
            }
        }

        static Paragraph ParagraphOf(TextElement element)
        {
            while (element != null && element is not Paragraph) element = element.Parent as TextElement;
            return element as Paragraph;
        }

        /// <summary>Every run in reading order, nesting through spans, lists and table cells.</summary>
        static IEnumerable<Run> RunsIn(BlockCollection blocks)
        {
            foreach (Block block in blocks.ToList())
                foreach (Run run in RunsIn(block))
                    yield return run;
        }

        static IEnumerable<Run> RunsIn(Block block)
        {
            switch (block)
            {
                case Paragraph p:
                    foreach (Run run in RunsIn(p.Inlines)) yield return run;
                    break;
                case Section s:
                    foreach (Run run in RunsIn(s.Blocks)) yield return run;
                    break;
                case List list:
                    foreach (ListItem item in list.ListItems.ToList())
                        foreach (Run run in RunsIn(item.Blocks)) yield return run;
                    break;
                case Table table:
                    foreach (TableRowGroup group in table.RowGroups.ToList())
                        foreach (TableRow row in group.Rows.ToList())
                            foreach (TableCell cell in row.Cells.ToList())
                                foreach (Run run in RunsIn(cell.Blocks)) yield return run;
                    break;
            }
        }

        static IEnumerable<Run> RunsIn(InlineCollection inlines)
        {
            foreach (Inline inline in inlines.ToList())
            {
                if (inline is Run run) yield return run;
                else if (inline is Span span)
                    foreach (Run nested in RunsIn(span.Inlines)) yield return nested;
            }
        }

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
