using System;
using System.Globalization;
using System.Text;

namespace Clip.Core
{
    /// <summary>Inline marks and text plumbing for <see cref="HtmlToMarkdown"/>.</summary>
    internal static partial class HtmlToMarkdown
    {
        sealed partial class Renderer
        {
            void EndLink()
            {
                var t = _targets[_targets.Count - 1];
                CloseMarksAbove(t.MarkDepth);
                _targets.RemoveAt(_targets.Count - 1);
                string text = t.Buf.ToString();
                if (string.IsNullOrEmpty(text)) return;
                string href = t.Href ?? "";
                Inline(string.IsNullOrEmpty(href) ? text : "[" + text + "](" + Angle(href) + ")");
            }

            void EndCode()
            {
                var t = _targets[_targets.Count - 1];
                CloseMarksAbove(t.MarkDepth);
                _targets.RemoveAt(_targets.Count - 1);
                string s = t.Buf.ToString().Replace('\n', ' ').Replace('\r', ' ').Trim();
                if (s.Length == 0) return;
                Inline(s.IndexOf('`') < 0 ? "`" + s + "`" : "`` " + s + " ``");
            }

            static string Angle(string url) =>
                url.IndexOfAny(new[] { ' ', '(', ')' }) < 0 ? url : "<" + url + ">";

            string Image(string tag, int after)
            {
                string alt = Attr(tag, after, "alt") ?? "";
                string src = (Attr(tag, after, "src") ?? "").Trim();
                return src.Length == 0 ? Escape(alt) : "![" + Escape(alt) + "](" + Angle(src) + ")";
            }

            void Mark(string name, string open, string closer)
            {
                Inline(open);
                _marks.Add(new Mark(name, closer));
            }

            void CloseMarks(string name)
            {
                int idx = _marks.FindLastIndex(m => m.Name == name);
                if (idx < 0) return; // stray end tag: ignore it, keep the text
                CloseMarksAbove(idx);
                _marks.RemoveRange(idx, _marks.Count - idx);
            }

            void CloseMarksAbove(int depth)
            {
                while (_marks.Count > depth)
                {
                    int k = _marks.Count - 1;
                    Cur.Append(_marks[k].Closer);
                    _marks.RemoveAt(k);
                }
            }

            void Span(string tag, int after, string name)
            {
                string css = Attr(tag, after, "style");
                if (name == "font")
                {
                    string color = Attr(tag, after, "color");
                    string parsed = TryParseColor(color ?? "");
                    if (parsed != null) css = (css == null ? "" : css + ";") + "color:" + parsed;
                    // The legacy 1-7 size ladder maps to points; unknown values do not fail the span.
                    string size = Attr(tag, after, "size");
                    if (int.TryParse(size, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= 7)
                    {
                        int[] pts = { 8, 10, 12, 14, 18, 24, 36 };
                        css = (css == null ? "" : css + ";") + "font-size:" + pts[n - 1] + "pt";
                    }
                }
                if (css != null) ApplyStyle(css, name);
            }

            void ApplyStyle(string css, string tag)
            {
                bool bold = false, italic = false, strike = false, under = false;
                string color = null, bg = null, size = null;
                foreach (string declaration in css.Split(';'))
                {
                    int colon = declaration.IndexOf(':');
                    if (colon <= 0) continue;
                    string key = declaration.Substring(0, colon).Trim().ToLowerInvariant();
                    string value = declaration.Substring(colon + 1).Trim().ToLowerInvariant();
                    if (key is "color" or "background" or "background-color")
                    {
                        string parsed = TryParseColor(value);
                        if (parsed != null)
                            if (key == "color") color = parsed; else bg = parsed;
                    }
                    else if (key == "font-weight")
                        bold = value == "bold" || value == "bolder" ||
                               (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) && weight >= 600);
                    else if (key == "font-style") italic = value == "italic" || value == "oblique";
                    else if (key is "text-decoration" or "text-decoration-line")
                    {
                        if (!value.Contains("none"))
                        {
                            under |= value.Contains("underline");
                            strike |= value.Contains("line-through");
                        }
                    }
                    else if (key == "font-size" && ValidSize(value)) size = value;
                }
                if (bold) Mark(tag, "**", "**");
                if (italic) Mark(tag, "*", "*");
                if (strike) Mark(tag, "~~", "~~");
                if (under) Mark(tag, "<u>", "</u>");
                if (color != null || bg != null || size != null)
                {
                    var open = new StringBuilder("<span style=\"");
                    if (color != null) open.Append("color:").Append(color).Append(';');
                    if (bg != null) open.Append("background-color:").Append(bg).Append(';');
                    if (size != null) open.Append("font-size:").Append(size).Append(';');
                    open.Append("\">");
                    Mark(tag, open.ToString(), "</span>");
                }
            }

            static bool ValidSize(string value)
            {
                if (string.IsNullOrEmpty(value)) return false;
                string[] units = { "px", "pt", "em", "rem", "%" };
                foreach (string unit in units)
                {
                    if (!value.EndsWith(unit, StringComparison.Ordinal) || value.Length == unit.Length) continue;
                    if (double.TryParse(value.Substring(0, value.Length - unit.Length),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && n > 0) return true;
                }
                return false;
            }

            // ---- text plumbing -----------------------------------------------

            void Write(string s, bool literal)
            {
                if (s.Length == 0) return;
                if (!AtRoot) { Cur.Append(literal ? s : Escape(s)); _wroteAny = true; return; }
                Flush();
                Marker();
                bool fresh = _lineStart;
                Prefix();
                if (_freshMarker)
                {
                    _freshMarker = false;
                    if (s.Length > 0 && (s[0] == ' ' || s[0] == '\t')) s = s.Substring(1);
                    if (s.Length == 0) return;
                }
                _root.Append(literal ? s : fresh ? Lead(Escape(s)) : Escape(s));
                _wroteAny = true;
            }

            // Appends markdown that is already escaped (links, code, images, mark openers).
            void Inline(string s)
            {
                if (s.Length == 0) return;
                if (!AtRoot) { Cur.Append(s); _wroteAny = true; return; }
                Flush();
                Marker();
                Prefix();
                _root.Append(s);
                _wroteAny = true;
            }

            // A block boundary is deferred to the next content, so blocks never emit blank paragraphs.
            void Gap(int n)
            {
                if (!AtRoot) return;
                if (n == 2 && _lists.Count > 0) n = 1; // a blank line would end the list
                if (n > _gap) _gap = n;
            }

            void Flush()
            {
                if (_gap == 0 || !AtRoot) { _gap = 0; return; }
                if (!_wroteAny) { _gap = 0; _lineStart = true; return; }
                RStrip();
                if (_gap == 1) _root.Append('\n');
                else
                {
                    _root.Append('\n');
                    if (_quotes > 0) _root.Append(new string('>', _quotes)).Append('\n');
                    _root.Append('\n');
                }
                _lineStart = true;
                _gap = 0;
            }

            void Prefix()
            {
                if (!_lineStart || !AtRoot) return;
                _lineStart = false;
                for (int i = 0; i < _quotes; i++) _root.Append("> ");
                if (_lists.Count > 0) _root.Append(' ', _lists.Count * 2);
            }

            void Newline()
            {
                RStrip();
                _root.Append('\n');
                _lineStart = true;
                _gap = 0;
            }

            void RawNewline()
            {
                _root.Append('\n');
                _lineStart = true;
                _gap = 0;
            }

            void RStrip()
            {
                while (_root.Length > 0 && (_root[_root.Length - 1] == ' ' || _root[_root.Length - 1] == '\t'))
                    _root.Length--;
            }

            static string Escape(string s)
            {
                if (s.IndexOfAny(new[] { '\\', '`', '*', '_', '[', ']' }) < 0) return s;
                var sb = new StringBuilder(s.Length + 8);
                foreach (char c in s)
                {
                    if (c == '\\' || c == '`' || c == '*' || c == '_' || c == '[' || c == ']') sb.Append('\\');
                    sb.Append(c);
                }
                return sb.ToString();
            }

            // A line starting with a marker char would become a heading, quote, list or rule: escape it.
            static string Lead(string s)
            {
                if (s.Length == 0 || s[0] == '\\') return s;
                char c = s[0];
                bool marker = c is '#' or '>' or '-' or '+' or '*' or '=' or '~' or '|' or '_' or '`';
                if (!marker && c >= '0' && c <= '9' && s.Length > 2 && (s[1] == '.' || s[1] == ')')) marker = true;
                return marker ? "\\" + s : s;
            }

            // ---- colours and entities (mirrors HtmlToRtf) --------------------

            static string TryParseColor(string value) // → #rrggbb, or null for inherit / transparent / unknown
            {
                if (string.IsNullOrEmpty(value)) return null;
                if (value[0] == '#')
                {
                    string hex = value.Substring(1);
                    if (hex.Length == 3) hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
                    if (hex.Length == 8) hex = hex.Substring(0, 6); // #rrggbbaa
                    return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
                        ? "#" + hex.ToLowerInvariant() : null;
                }
                if (value.StartsWith("rgb", StringComparison.Ordinal))
                {
                    int open = value.IndexOf('('), close = open < 0 ? -1 : value.IndexOf(')', open + 1);
                    if (close < 0) return null;
                    string[] parts = value.Substring(open + 1, close - open - 1).Split(',');
                    if (parts.Length < 3) return null;
                    int r = Channel(parts[0]), g = Channel(parts[1]), b = Channel(parts[2]);
                    return r < 0 || g < 0 || b < 0 ? null : string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b).ToLowerInvariant();
                }
                switch (value)
                {
                    case "black": return "#000000"; case "white": return "#ffffff"; case "red": return "#ff0000";
                    case "green": return "#008000"; case "lime": return "#00ff00"; case "blue": return "#0000ff";
                    case "yellow": return "#ffff00"; case "orange": return "#ffa500"; case "purple": return "#800080";
                    case "gray": case "grey": return "#808080"; case "teal": return "#008080";
                    case "navy": return "#000080"; case "maroon": return "#800000"; case "olive": return "#808000";
                    case "aqua": case "cyan": return "#00ffff"; case "brown": return "#a52a2a";
                }
                return null;
            }

            static int Channel(string value)
            {
                value = value.Trim();
                bool percent = value.EndsWith("%", StringComparison.Ordinal);
                if (!double.TryParse(percent ? value.TrimEnd('%') : value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
                    return -1;
                return Math.Max(0, Math.Min(255, (int)Math.Round(percent ? n * 255 / 100 : n)));
            }

            static string DecodeEntities(string s)
            {
                if (s.IndexOf('&') < 0) return s;
                var sb = new StringBuilder(s.Length);
                for (int i = 0; i < s.Length; )
                {
                    int semi = s[i] == '&' ? s.IndexOf(';', i + 1) : -1;
                    if (semi < 0 || semi - i > 12) { sb.Append(s[i++]); continue; }
                    string replacement = Entity(s.Substring(i + 1, semi - i - 1));
                    if (replacement != null) { sb.Append(replacement); i = semi + 1; }
                    else sb.Append(s[i++]); // unknown entity: leave the raw text alone
                }
                return sb.ToString();
            }

            static string Entity(string name)
            {
                switch (name.ToLowerInvariant())
                {
                    case "amp": return "&"; case "lt": return "<"; case "gt": return ">"; case "quot": return "\"";
                    case "apos": return "'"; case "nbsp": return "\u00A0"; case "ndash": return "\u2013";
                    case "mdash": return "\u2014"; case "hellip": return "\u2026"; case "bull": return "\u2022";
                    case "copy": return "\u00A9"; case "reg": return "\u00AE"; case "trade": return "\u2122";
                    case "deg": return "\u00B0"; case "middot": return "\u00B7"; case "times": return "\u00D7";
                    case "euro": return "\u20AC"; case "pound": return "\u00A3"; case "yen": return "\u00A5";
                }
                if (name.Length >= 2 && name[0] == '#')
                {
                    bool hex = name[1] == 'x' || name[1] == 'X';
                    bool ok = int.TryParse(hex ? name.Substring(2) : name.Substring(1),
                        hex ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture, out int code);
                    if (ok && code > 0 && code <= 0x10FFFF && (code < 0xD800 || code > 0xDFFF))
                        return char.ConvertFromUtf32(code);
                }
                return null;
            }
        }
    }
}
