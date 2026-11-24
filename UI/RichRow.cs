using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Clip.Core;
using Clip.Interop;

namespace Clip.UI
{
    /// <summary>
    /// The one rtf/html → inline builder (docs\DESIGN-v2-synthesis.md §1). WPF's own RTF loader is the
    /// parser: text is loaded into ONE shared FlowDocument and its blocks are read out as plain run
    /// records, so a row never holds a document and the attributes are read through the wrapper Span
    /// chain (underline and strike live there — S0-HANDOFF §1).
    /// </summary>
    internal static class RichRow
    {
        const int MaxPayload = 32 * 1024; // rtf chars or extracted html fragment
        const int MaxTemplates = 32;

        static readonly Dictionary<long, RowContent.RichRun[]> Cache = new();
        static readonly Queue<long> Order = new();
        static FlowDocument _parser;

        /// <summary>Bounded template cache; dropped when the list content or the theme changes.</summary>
        public static void Clear()
        {
            Cache.Clear();
            Order.Clear();
        }

        /// <summary>
        /// Runs one tiny document through the real parser at startup, so the first Alt+V is not paying
        /// the JIT and the first TextRange.Load (measured: 264 ms cold vs 52 ms warm without it).
        /// </summary>
        public static void Warm()
        {
            var document = Parser();
            try
            {
                if (Load(document, "{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 Segoe UI;}}\\fs24 warm\\par\\b bold\\b0 \\ul under\\ul0}"))
                {
                    var runs = Collect(document);
                    if (runs != null) runs[0].ToInline();
                }
            }
            finally { document.Blocks.Clear(); }
        }

        public static List<Inline> Inlines(ClipRow row)
        {
            var list = new List<Inline>();
            if (row == null) return list;

            var template = row.CanBeRich ? Template(row) : null;
            if (template != null)
            {
                foreach (var run in template) list.Add(run.ToInline());
                return list;
            }
            Plain(list, row.Preview, row.Query);
            return list;
        }

        /// <summary>
        /// Full-text inlines for the outside tooltip: the same rich template as the row when the
        /// clip carries formatting, otherwise the full (2000-char) tooltip text — never the
        /// truncated row preview. Plain runs inherit the tip's own FontSize.
        /// </summary>
        public static List<Inline> TipInlines(ClipRow row)
        {
            var list = new List<Inline>();
            if (row == null) return list;

            var template = row.CanBeRich ? Template(row) : null;
            if (template != null)
            {
                foreach (var run in template) list.Add(run.ToInline());
                return list;
            }
            Plain(list, row.Tooltip ?? "", row.Query);
            return list;
        }

        // ---- plain fallback + search runs (synthesis Q2) ---------------------

        static void Plain(List<Inline> list, string text, string query)
        {
            var lines = ClipRow.SplitLines(text ?? "");
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) list.Add(new LineBreak());
                string line = lines[i];
                int at = 0;
                foreach (var range in Ranges(line, query))
                {
                    if (range.start > at) list.Add(new Run(line.Substring(at, range.start - at)));
                    list.Add(new Run(line.Substring(range.start, range.length)) { Foreground = MatchBrush() });
                    at = range.start + range.length;
                }
                if (at < line.Length) list.Add(new Run(line.Substring(at)));
            }
        }

        /// <summary>
        /// Up to 20 match ranges with the SAME comparison SearchService.Filter uses (ordinal, ignore
        /// case) — what filters is what highlights. A match changes Foreground only: no weight or size
        /// change means no inner re-wrap (no-relayout rule).
        /// </summary>
        static List<(int start, int length)> Ranges(string text, string query)
        {
            var ranges = new List<(int, int)>();
            string q = (query ?? "").Trim();
            if (q.Length == 0 || string.IsNullOrEmpty(text)) return ranges;
            int at = 0;
            while (ranges.Count < 20)
            {
                int hit = text.IndexOf(q, at, StringComparison.OrdinalIgnoreCase);
                if (hit < 0) break;
                ranges.Add((hit, q.Length));
                at = hit + q.Length;
            }
            return ranges;
        }

        static Brush MatchBrush() => RowContent.ThemeBrush("AccentText");

        // ---- build ----------------------------------------------------------

        static RowContent.RichRun[] Template(ClipRow row)
        {
            if (Cache.TryGetValue(row.Id, out var hit)) return hit;
            var built = Build(row);
            Cache[row.Id] = built;
            Order.Enqueue(row.Id);
            while (Order.Count > MaxTemplates) Cache.Remove(Order.Dequeue());
            return built;
        }

        static RowContent.RichRun[] Build(ClipRow row)
        {
            var payload = row.Payload;
            if (payload == null) return null;

            string source;
            if (!string.IsNullOrEmpty(payload.Rtf))
            {
                if (payload.Rtf.Length > MaxPayload) return null;
                source = payload.Rtf;
            }
            else if (!string.IsNullOrEmpty(payload.Html))
            {
                var fragment = HtmlFragment.Extract(payload.Html);
                if (fragment.Length == 0 || fragment.Length > MaxPayload) return null;
                try { source = HtmlToRtf.Convert(fragment); }
                catch (Exception) { return null; } // converter fault: the plain row is still truthful
            }
            else return null;

            var document = Parser();
            try
            {
                return Load(document, source) ? Collect(document) : null;
            }
            finally { document.Blocks.Clear(); }
        }

        static FlowDocument Parser()
        {
            // One document for the app, never shown and emptied after every build. Measured 3.5 ms per
            // RTF load against 4.6 ms through a RichTextBox wrapper — same loader, less machinery.
            return _parser ??= new FlowDocument();
        }

        static bool Load(FlowDocument document, string rtf)
        {
            foreach (var bytes in new[] { RtfText.ToBytes(rtf), RtfText.ToUtf8Bytes(rtf) })
            {
                document.Blocks.Clear();
                try
                {
                    using var stream = new MemoryStream(bytes);
                    new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.Rtf);
                    return true;
                }
                catch (Exception) { } // malformed body: the second attempt, then the plain row, handles it
            }
            document.Blocks.Clear();
            return false;
        }

        // ---- document → run records -----------------------------------------

        static RowContent.RichRun[] Collect(FlowDocument document)
        {
            var sink = new Sink(document.FontSize);
            foreach (var block in document.Blocks) AddBlock(block, default, sink);

            while (sink.Runs.Count > 0 && sink.Runs[^1].Text == null) sink.Runs.RemoveAt(sink.Runs.Count - 1);
            // "no run carries a non-default attribute" → the plain row, so identical clips look identical.
            if (sink.Bailed || sink.Runs.Count == 0 || sink.Runs.Count > RowContent.MaxRuns || !sink.Formatted)
                return null;
            return sink.Runs.ToArray();
        }

        static void AddBlock(Block block, Fmt fmt, Sink sink)
        {
            if (sink.Bailed) return;
            switch (block)
            {
                case Paragraph paragraph:
                    AddInlines(paragraph.Inlines, Merge(fmt, paragraph), sink);
                    sink.Break();
                    break;
                case Section section:
                    foreach (var child in section.Blocks) AddBlock(child, Merge(fmt, section), sink);
                    break;
                case List list:
                    foreach (var item in list.ListItems)
                        foreach (var child in item.Blocks) AddBlock(child, Merge(fmt, item), sink);
                    break;
                case Table table:
                    AddTable(table, sink);
                    break;
                case BlockUIContainer host:
                    if (host.Child is TextBlock text) { sink.Text(text.Text, fmt); sink.Break(); }
                    break;
            }
        }

        /// <summary>A table cannot be laid out in a 20 px line: one line per row, cells tab-joined.</summary>
        static void AddTable(Table table, Sink sink)
        {
            var fmt = default(Fmt);
            foreach (var group in table.RowGroups)
                foreach (var row in group.Rows)
                {
                    var cells = new List<string>();
                    foreach (var cell in row.Cells)
                        cells.Add(new TextRange(cell.ContentStart, cell.ContentEnd).Text
                            .Replace("\r", " ").Replace("\n", " ").Trim());
                    sink.Text(string.Join("\t", cells), fmt);
                    sink.Break();
                    if (sink.Bailed) return;
                }
        }

        static void AddInlines(InlineCollection inlines, Fmt fmt, Sink sink)
        {
            foreach (var inline in inlines)
            {
                if (sink.Bailed) return;
                switch (inline)
                {
                    case Run run:
                        sink.Text(run.Text, Merge(fmt, run));
                        break;
                    case Hyperlink link:
                        // Text only, original colour kept; uncoloured links use the content
                        // layer's link blue (never the app accent) — a row click pastes.
                        var linkFmt = Merge(fmt, link);
                        if (linkFmt.Fg == null) linkFmt.Fg = RowContent.ContentBrush("ContentLink");
                        linkFmt.Underline = true;
                        AddInlines(link.Inlines, linkFmt, sink);
                        break;
                    case Span span:
                        AddInlines(span.Inlines, Merge(fmt, span), sink);
                        break;
                    case LineBreak:
                        sink.Break();
                        break;
                    case InlineUIContainer host:
                        if (host.Child is TextBlock text) sink.Text(text.Text, fmt);
                        break;
                }
            }
        }

        /// <summary>Merges one element's LOCAL values into the chain's state — the Span wrapper included.</summary>
        static Fmt Merge(in Fmt outer, TextElement element)
        {
            var fmt = outer;
            if (element.ReadLocalValue(TextElement.ForegroundProperty) is Brush brush) fmt.Fg = brush;
            if (element.ReadLocalValue(TextElement.FontWeightProperty) is FontWeight weight)
                fmt.Bold = weight >= FontWeights.SemiBold;
            if (element.ReadLocalValue(TextElement.FontStyleProperty) is FontStyle style)
                fmt.Italic = style != FontStyles.Normal;
            if (element.ReadLocalValue(TextElement.FontSizeProperty) is double size) fmt.Size = size;
            if (element.ReadLocalValue(TextElement.BackgroundProperty) is Brush bg) fmt.Bg = bg;
            if (element.ReadLocalValue(TextElement.FontFamilyProperty) is FontFamily family)
            {
                fmt.Mono = IsMono(family);
                if (!family.Source.Contains("Segoe UI", StringComparison.OrdinalIgnoreCase)) fmt.Family = family;
            }
            if (element is Inline inline &&
                inline.ReadLocalValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations)
            {
                fmt.Underline = Has(decorations, TextDecorationLocation.Underline);
                fmt.Strike = Has(decorations, TextDecorationLocation.Strikethrough);
            }
            return fmt;
        }

        static bool Has(TextDecorationCollection decorations, TextDecorationLocation location)
        {
            foreach (var decoration in decorations)
                if (decoration.Location == location) return true;
            return false;
        }

        static bool IsMono(FontFamily family)
        {
            string name = family?.Source ?? "";
            return name.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Courier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Mono", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>True for near-white glyphs with no backing: they vanish on the light canvas.</summary>
        static bool IsLight(Brush brush)
        {
            if (brush is SolidColorBrush solid)
            {
                var c = solid.Color;
                double lum = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
                return lum > 0.75;
            }
            return false;
        }

        // ---- document state → run records -----------------------------------

        struct Fmt
        {
            public Brush Fg;
            public Brush Bg;
            public FontFamily Family;
            public bool Bold, Italic, Underline, Strike, Mono;
            public double Size; // 0 = the document default
        }

        sealed class Sink
        {
            int _chars;

            public Sink(double baseSize)
            {
                BaseSize = baseSize <= 0 ? 16 : baseSize;
            }

            public readonly List<RowContent.RichRun> Runs = new();
            public double BaseSize { get; }
            public bool Formatted;
            public bool Bailed;

            public void Text(string text, in Fmt fmt)
            {
                if (string.IsNullOrEmpty(text) || _chars >= RowContent.MaxChars) return;
                if (_chars + text.Length > RowContent.MaxChars) text = text.Substring(0, RowContent.MaxChars - _chars);
                _chars += text.Length;

                // Sizes are normalised to the row's own 14/20 type: a 12 pt body becomes 14 px, a
                // heading stays proportionally larger. Clamped 10–20 so no glyph outgrows its line.
                double size = fmt.Size <= 0 ? RowContent.RowSize : Math.Clamp(fmt.Size * (RowContent.RowSize / BaseSize), 10, 20);
                // Styled content keeps its own colours: an uncoloured run inside styled content
                // is ContentInk (the content layer's document default); fully unstyled runs stay null
                // and inherit the row's UI styling (Layer-1 ownership).
                Brush fg = fmt.Fg;
                if (fg == null && (fmt.Bold || fmt.Italic || fmt.Underline || fmt.Strike || fmt.Mono ||
                    fmt.Bg != null || fmt.Family != null ||
                    (fmt.Size > 0 && Math.Abs(fmt.Size - BaseSize) > 0.5))) fg = RowContent.ContentBrush("ContentInk");

                // White-on-dark source inside the light app canvas: keep the glyph colour,
                // park it on a dark pill so it stays legible without altering the format.
                Brush bg = fmt.Bg;
                if (bg == null && fg != null && IsLight(fg))
                    bg = RowContent.ContentBrush("ContentDarkBg");

                if (fmt.Bold || fmt.Italic || fmt.Underline || fmt.Strike || fmt.Mono || fg != null ||
                    fmt.Bg != null || fmt.Family != null ||
                    (fmt.Size > 0 && Math.Abs(fmt.Size - BaseSize) > 0.5))
                    Formatted = true;

                var lines = ClipRow.SplitLines(text);
                for (int i = 0; i < lines.Count; i++)
                {
                    if (i > 0) Break();
                    if (lines[i].Length == 0) continue;
                    Add(new RowContent.RichRun
                    {
                        Text = lines[i],
                        Fg = fg,
                        Bg = bg,
                        Family = fmt.Family,
                        Bold = fmt.Bold,
                        Italic = fmt.Italic,
                        Underline = fmt.Underline,
                        Strike = fmt.Strike,
                        Mono = fmt.Mono,
                        Size = size
                    });
                }
            }

            public void Break()
            {
                if (Runs.Count == 0 || Runs[^1].Text == null) return;
                Add(new RowContent.RichRun());
            }

            void Add(RowContent.RichRun run)
            {
                if (Runs.Count >= RowContent.MaxRuns) { Bailed = true; return; }
                Runs.Add(run);
            }
        }
    }
}
