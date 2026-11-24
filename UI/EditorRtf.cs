using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Clip.Core;
using WpfList = System.Windows.Documents.List;

namespace Clip.UI
{
    /// <summary>
    /// RTF → HTML for RTF-only clips. WPF has no HTML export (<c>TextRange.Save(DataFormats.Html)</c> throws),
    /// so the FlowDocument its RTF parser builds is written out here: word-processor block structure, plus
    /// a span per run that carries its own character formatting. The page does the rest.
    /// </summary>
    internal static class EditorRtf
    {
        public static string ToHtml(string rtf)
        {
            if (string.IsNullOrEmpty(rtf)) return null;
            try
            {
                var document = Load(rtf);
                if (document == null) return null;
                // A body with no characters must never replace the clip's text with nothing.
                if (string.IsNullOrWhiteSpace(new TextRange(document.ContentStart, document.ContentEnd).Text))
                    return null;

                var html = new StringBuilder();
                foreach (var block in document.Blocks) AppendBlock(block, html);
                return html.ToString();
            }
            catch
            {
                // A stream the RTF parser rejects becomes the caller's plain-text path, never a crash on open.
                return null;
            }
        }

        /// <summary>Load attempt 1 = Latin-1 bytes, attempt 2 = UTF-8, the pair the old editor used.</summary>
        static FlowDocument Load(string rtf)
        {
            foreach (var bytes in new[] { RtfText.ToBytes(rtf), RtfText.ToUtf8Bytes(rtf) })
            {
                if (bytes.Length == 0) continue;
                var document = new FlowDocument();
                try
                {
                    using var stream = new MemoryStream(bytes);
                    new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.Rtf);
                    return document;
                }
                catch
                {
                    // A half-loaded document is worse than none: the next attempt starts from a fresh one.
                }
            }
            return null;
        }

        // ---- blocks -----------------------------------------------------------------------

        static void AppendBlock(Block block, StringBuilder html)
        {
            switch (block)
            {
                case Paragraph paragraph:
                {
                    html.Append("<p>");
                    string style = ParagraphStyle(paragraph);
                    if (style != null) html.Append("<span style=\"").Append(style).Append("\">");
                    AppendInlines(paragraph.Inlines, html);
                    if (style != null) html.Append("</span>");
                    html.Append("</p>");
                    break;
                }

                case Section section:
                    foreach (var child in section.Blocks) AppendBlock(child, html);
                    break;

                case WpfList list:
                {
                    string tag = IsOrdered(list) ? "ol" : "ul";
                    html.Append('<').Append(tag).Append('>');
                    foreach (var item in list.ListItems)
                    {
                        html.Append("<li>");
                        foreach (var child in item.Blocks)
                        {
                            // A list item renders inline: its own <p> would add a paragraph inside the bullet.
                            if (child is Paragraph inner) AppendInlines(inner.Inlines, html);
                            else AppendBlock(child, html);
                        }
                        html.Append("</li>");
                    }
                    html.Append("</").Append(tag).Append('>');
                    break;
                }

                case Table table:
                    html.Append("<table>");
                    foreach (var group in table.RowGroups)
                    {
                        foreach (var row in group.Rows)
                        {
                            html.Append("<tr>");
                            foreach (var cell in row.Cells)
                            {
                                html.Append("<td>");
                                foreach (var child in cell.Blocks) AppendBlock(child, html);
                                html.Append("</td>");
                            }
                            html.Append("</tr>");
                        }
                    }
                    html.Append("</table>");
                    break;

                default:
                    // A block the walker has no tag for keeps its characters rather than vanishing.
                    html.Append("<p>")
                        .Append(Encode(new TextRange(block.ContentStart, block.ContentEnd).Text))
                        .Append("</p>");
                    break;
            }
        }

        static bool IsOrdered(WpfList list) => list.MarkerStyle is TextMarkerStyle.Decimal
            or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin
            or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman;

        /// <summary>Word often sets \f and \fs on the paragraph, not on every run, so the paragraph carries them down.</summary>
        static string ParagraphStyle(Paragraph paragraph)
        {
            var css = new List<string>();
            if (HasLocal(paragraph, TextElement.FontFamilyProperty))
                css.Add("font-family:'" + paragraph.FontFamily.Source + "'");
            if (HasLocal(paragraph, TextElement.FontSizeProperty))
                css.Add("font-size:" + Math.Round(paragraph.FontSize * 0.75) + "pt");
            return css.Count == 0 ? null : string.Join(";", css);
        }

        // ---- inlines ----------------------------------------------------------------------

        static void AppendInlines(InlineCollection inlines, StringBuilder html)
        {
            var runs = new List<(string text, string style)>();
            Collect(inlines, new List<string>(), runs);

            // Adjacent runs with one style become ONE span, so a Word body is not 400 two-character spans.
            var buffer = new StringBuilder();
            string open = null;
            foreach (var (text, style) in runs)
            {
                if (style != open)
                {
                    Flush(html, buffer, open);
                    open = style;
                }
                buffer.Append(text);
            }
            Flush(html, buffer, open);
        }

        static void Flush(StringBuilder html, StringBuilder buffer, string style)
        {
            if (buffer.Length == 0) return;
            if (style == null) html.Append(buffer);
            else html.Append("<span style=\"").Append(style).Append("\">").Append(buffer).Append("</span>");
            buffer.Clear();
        }

        /// <summary>
        /// Flattens the inline tree to (encoded text, css) leaves. Nesting is kept by carrying the
        /// ancestors' declarations down, so a Span's colour still covers the Runs inside it.
        /// </summary>
        static void Collect(InlineCollection inlines, List<string> inherited, List<(string text, string style)> runs)
        {
            foreach (var inline in inlines)
            {
                var style = new List<string>(inherited);
                AppendLocal(inline, style);

                switch (inline)
                {
                    case Run run: Add(run.Text, style, runs); break;
                    // Hyperlink derives from Span, so it must be matched first.
                    case Hyperlink link: Collect(link.Inlines, style, runs); break;
                    case Span span: Collect(span.Inlines, style, runs); break;
                    case LineBreak: Add("\n", style, runs); break;
                    case InlineUIContainer container when container.Child is System.Windows.Controls.TextBlock text:
                        Add(text.Text, style, runs);
                        break;
                }
            }
        }

        static void Add(string text, List<string> style, List<(string text, string style)> runs)
        {
            if (string.IsNullOrEmpty(text)) return;
            runs.Add((Encode(text), style.Count == 0 ? null : string.Join(";", style)));
        }

        /// <summary>
        /// Only a LOCAL value is the clip's own formatting; everything else is the document default,
        /// which the page already draws (so a plain black, undecorated run gets no span at all).
        /// </summary>
        static void AppendLocal(Inline inline, List<string> css)
        {
            if (HasLocal(inline, TextElement.FontWeightProperty) && inline.FontWeight >= FontWeights.SemiBold)
                css.Add("font-weight:600");

            if (HasLocal(inline, TextElement.FontStyleProperty) && inline.FontStyle == FontStyles.Italic)
                css.Add("font-style:italic");

            if (HasLocal(inline, Inline.TextDecorationsProperty) && inline.TextDecorations != null)
            {
                bool underline = false, strike = false;
                foreach (var decoration in inline.TextDecorations)
                {
                    underline |= decoration.Location == TextDecorationLocation.Underline;
                    strike |= decoration.Location == TextDecorationLocation.Strikethrough;
                }
                if (underline) css.Add("text-decoration:underline");
                if (strike) css.Add("text-decoration:line-through");
            }

            if (HasLocal(inline, TextElement.ForegroundProperty) && inline.Foreground is SolidColorBrush ink
                && ink.Color != Colors.Black)
                css.Add("color:" + Hex(ink.Color));

            if (HasLocal(inline, TextElement.BackgroundProperty) && inline.Background is SolidColorBrush band)
                css.Add("background-color:" + Hex(band.Color));

            if (HasLocal(inline, TextElement.FontSizeProperty))
                css.Add("font-size:" + Math.Round(inline.FontSize * 0.75) + "pt");

            if (HasLocal(inline, TextElement.FontFamilyProperty))
                css.Add("font-family:'" + inline.FontFamily.Source + "'");
        }

        static bool HasLocal(DependencyObject element, DependencyProperty property) =>
            element.ReadLocalValue(property) != DependencyProperty.UnsetValue;

        /// <summary>HTML-encode, then a newline (soft break or LineBreak run) becomes the same tag.</summary>
        static string Encode(string text) =>
            WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "<br>");

        static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
