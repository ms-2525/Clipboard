using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Clip.Core
{
    /// <summary>
    /// CF_HTML fragment (the output of <c>Interop\HtmlFragment.Extract</c>) â†’ RTF for the one WPF
    /// loader (TextRange.Load). Lossy by design (docs\DESIGN-v2-ui.md Â§4.5): CSS classes, floats, web
    /// fonts and images do not survive; a table becomes one line per row with tab-joined cells.
    /// Unknown tags are skipped and their text kept; head/style/script/comment text never reaches the
    /// document. Output is 7-bit ASCII with \uN escapes, so Devanagari, â‚¹ and emoji load anywhere.
    /// </summary>
    internal static class HtmlToRtf
    {
        const int BaseSize = 24;    // half-points â€” RTF's own 12 pt default
        const int IndentStep = 360; // twips â€” one list / quote level

        public static string Convert(string html)
        {
            var body = new StringBuilder(html == null ? 64 : Math.Min(html.Length * 2, 1 << 16));
            var renderer = new Renderer(body);
            renderer.Run(html);
            var sb = new StringBuilder(body.Length + 128);
            sb.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}{\f1 Consolas;");
            for (int f = 0; f < renderer.Fonts.Count; f++) // f0/f1 reserved → collected fonts start at f2
                sb.Append(@"{\f").Append(f + 2).Append(' ').Append(renderer.Fonts[f]).Append(@";}");
            sb.Append('}');
            if (renderer.Colours.Count > 0)
            {
                sb.Append(@"{\colortbl ;"); // slot 0 is \auto
                foreach (int rgb in renderer.Colours)
                    sb.Append(@"\red").Append((rgb >> 16) & 0xFF).Append(@"\green").Append((rgb >> 8) & 0xFF)
                      .Append(@"\blue").Append(rgb & 0xFF).Append(';');
                sb.Append('}');
            }
            return sb.Append('\n').Append(body).Append('}').ToString();
        }

        sealed class Renderer
        {
            // Value equality is what tells "this tag changed the formatting" from a no-op tag.
            record struct Style
            {
                public bool Bold, Italic, Underline, Strike, Mono, Pre;
                public int Size, Fg, Bg, Indent; // half-points; colour index (0 = auto); highlight index (0 = none); twips
                public string Family; // collected font-table name (null = default f0)
            }

            readonly record struct Frame(string Name, Style Saved);
            record struct ListState(bool Ordered, int Count);

            readonly StringBuilder _out;
            readonly Dictionary<int, int> _colourIndex = new Dictionary<int, int>();
            readonly Dictionary<string, int> _fontIndex = new Dictionary<string, int>();
            readonly List<Frame> _stack = new List<Frame>();
            readonly List<ListState> _lists = new List<ListState>();
            public List<int> Colours { get; } = new List<int>();
            public List<string> Fonts { get; } = new List<string>();

            Style _style;
            bool _wroteAny, _paraOpen, _spacePending;
            int _cells;
            string _prefix; // bullet / "2. " waiting for the next text of an <li>

            public Renderer(StringBuilder output) { _out = output; }

            public void Run(string html)
            {
                if (string.IsNullOrEmpty(html)) { _out.Append("\\pard\n"); return; }
                int i = 0;
                while (i < html.Length)
                {
                    if (html[i] != '<')
                    {
                        int start = i;
                        while (i < html.Length && html[i] != '<') i++;
                        AddText(DecodeEntities(html.Substring(start, i - start)), _style.Pre);
                        continue;
                    }
                    if (html.Length - i >= 4 && string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
                    {
                        int end = IndexOfIgnoreCase(html, "-->", i + 4);
                        i = end < 0 ? html.Length : end + 3;
                        continue;
                    }
                    // A '<' that cannot start a tag is text.
                    if (i + 1 >= html.Length || !(char.IsLetter(html[i + 1]) || html[i + 1] == '/' || html[i + 1] == '!'))
                    { AddText("<", false); i++; continue; }
                    i = Tag(html, i);
                }
                if (!_wroteAny) _out.Append("\\pard\n"); // an empty document still needs one paragraph
            }
            int Tag(string html, int start)
            {
                int gt = FindTagEnd(html, start + 1);
                if (gt < 0) { AddText("<", false); return start + 1; } // not a tag after all: keep the char
                string inner = html.Substring(start + 1, gt - start - 1).Trim();
                int next = gt + 1;
                if (inner.Length == 0 || inner[0] == '!') return next;
                if (inner.EndsWith("/", StringComparison.Ordinal)) inner = inner.Substring(0, inner.Length - 1).TrimEnd();
                bool closing = inner.StartsWith("/", StringComparison.Ordinal);
                if (closing) inner = inner.Substring(1).TrimStart();

                int after = 0;
                while (after < inner.Length && (char.IsLetterOrDigit(inner[after]) || inner[after] == '-' || inner[after] == ':')) after++;
                string name = inner.Substring(0, after).ToLowerInvariant();
                if (name.Length == 0) return next;

                // Elements whose *content* must never reach the document.
                if (name == "script" || name == "style" || name == "head")
                {
                    if (closing) return next;
                    int end = IndexOfIgnoreCase(html, "</" + name, next);
                    if (end < 0) return name == "head" ? next : html.Length; // no close: head carries on, CSS text is junk
                    int gt2 = html.IndexOf('>', end);
                    return gt2 < 0 ? html.Length : gt2 + 1;
                }

                if (closing) End(name); else Start(name, StyleAttribute(inner, after));
                return next;
            }
            static int FindTagEnd(string html, int from)
            {
                char quote = '\0';
                for (int i = from; i < html.Length; i++)
                {
                    char c = html[i];
                    if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                    if (c == '"' || c == '\'') { quote = c; continue; }
                    if (c == '>') return i;
                    if (c == '<') return -1; // a '<' before '>' means this was not a tag at all
                }
                return -1;
            }

            static int IndexOfIgnoreCase(string s, string what, int from) =>
                from >= s.Length ? -1 : s.IndexOf(what, from, StringComparison.OrdinalIgnoreCase);
            /// <summary>The value of one attribute â€” only <c>style</c> is ever asked for.</summary>
            static string StyleAttribute(string tag, int from)
            {
                int at = tag.IndexOf("style", from, StringComparison.OrdinalIgnoreCase);
                if (at < 0 || (at > 0 && !char.IsWhiteSpace(tag[at - 1]))) return null;
                int eq = tag.IndexOf('=', at + 5);
                if (eq < 0) return null;
                int i = eq + 1;
                while (i < tag.Length && char.IsWhiteSpace(tag[i])) i++;
                if (i < tag.Length && (tag[i] == '"' || tag[i] == '\''))
                {
                    int close = tag.IndexOf(tag[i], i + 1);
                    return close < 0 ? null : tag.Substring(i + 1, close - i - 1);
                }
                int start = i;
                while (i < tag.Length && !char.IsWhiteSpace(tag[i])) i++;
                return tag.Substring(start, i - start);
            }

            void Start(string name, string style)
            {
                Style before = _style;
                switch (name)
                {
                    case "p": case "div": case "section": case "article": case "header": case "footer":
                    case "figure": case "hr": Break(); break;
                    case "br": Line(); break;
                    case "h1": Heading(2.0); break;
                    case "h2": Heading(1.5); break;
                    case "h3": Heading(1.25); break;
                    case "h4": case "h5": case "h6": Heading(1.0); break;
                    case "blockquote": Break(); _style.Indent += IndentStep; break;
                    case "pre": Break(); _style.Mono = true; _style.Pre = true; break;
                    case "code": case "kbd": case "samp": case "tt": _style.Mono = true; break;
                    case "b": case "strong": _style.Bold = true; break;
                    case "i": case "em": case "cite": case "var": _style.Italic = true; break;
                    case "u": case "ins": _style.Underline = true; break;
                    case "s": case "strike": case "del": _style.Strike = true; break;
                    case "ul": case "ol": Break(); _style.Indent += IndentStep; _lists.Add(new ListState(name == "ol", 0)); break;
                    case "li": Break(); _prefix = Bullet(); break;
                    case "table": Break(); break;
                    case "tr": Break(); _cells = 0; break;
                    case "td": case "th": if (_cells++ > 0) Tab(); if (name == "th") _style.Bold = true; break;
                }
                if (style != null) ApplyStyle(style);
                if (_style != before) _stack.Add(new Frame(name, before));
            }
            void End(string name)
            {
                if (name is "ul" or "ol") { if (_lists.Count > 0) _lists.RemoveAt(_lists.Count - 1); Break(); }
                else if (IsBlock(name)) Break();
                if (name == "li") _prefix = null; // an empty <li> leaves no bullet for the next block
                int idx = _stack.FindLastIndex(f => f.Name == name);
                if (idx < 0) return; // stray end tag: ignore it, keep the text
                _style = _stack[idx].Saved;
                _stack.RemoveRange(idx, _stack.Count - idx);
            }
            static bool IsBlock(string name) =>
                name is "p" or "div" or "section" or "article" or "header" or "footer" or "figure"
                    or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "blockquote" or "pre" or "li" or "tr" or "table";
            void Heading(double scale) { Break(); _style.Bold = true; _style.Size = (int)Math.Round(BaseSize * scale); }
            string Bullet()
            {
                if (_lists.Count == 0) return "\u2022 ";
                ListState top = _lists[_lists.Count - 1];
                if (!top.Ordered) return "\u2022 ";
                top.Count++;
                _lists[_lists.Count - 1] = top;
                return top.Count.ToString(CultureInfo.InvariantCulture) + ". ";
            }

            void AddText(string s, bool pre)
            {
                if (string.IsNullOrEmpty(s)) return;
                var chunk = new StringBuilder(s.Length);
                foreach (char c in s)
                {
                    if (!pre && (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f')) { _spacePending = true; continue; }
                    if (c == '\r') continue;
                    // A space between two words (or two tags) is content; one before the first word is not.
                    if (_spacePending) { if (_paraOpen || chunk.Length > 0) chunk.Append(' '); _spacePending = false; }
                    chunk.Append(c);
                }
                if (chunk.Length > 0) WriteChunk(chunk.ToString());
            }
            void WriteChunk(string text)
            {
                EnsurePara();
                if (_prefix != null) { text = _prefix + text; _prefix = null; }
                _out.Append('{');
                if (_style.Bold) _out.Append("\\b ");
                if (_style.Italic) _out.Append("\\i ");
                if (_style.Underline) _out.Append("\\ul ");
                if (_style.Strike) _out.Append("\\strike ");
                if (_style.Mono) _out.Append("\\f1 ");
                if (_style.Family != null) _out.Append("\\f").Append(_fontIndex[_style.Family] + 1).Append(' ');
                if (_style.Size > 0) _out.Append("\\fs").Append(_style.Size).Append(' ');
                if (_style.Fg > 0) _out.Append("\\cf").Append(_style.Fg).Append(' ');
                if (_style.Bg > 0) _out.Append("\\highlight").Append(_style.Bg).Append(' ');
                Escape(text);
                _out.Append('}');
            }
            void EnsurePara()
            {
                if (_paraOpen) return;
                if (_wroteAny) _out.Append("\\par\n");
                _out.Append("\\pard");
                if (_style.Indent > 0) _out.Append("\\li").Append(_style.Indent);
                _out.Append('\n');
                _paraOpen = _wroteAny = true;
            }

            // A block boundary is deferred to the next text, so blocks never emit blank paragraphs.
            void Break() { _paraOpen = false; _spacePending = false; }

            // A leading <br> adds no blank line, so nothing is emitted before the first text.
            void Line() { if (!_paraOpen) return; _spacePending = false; _out.Append("\\line "); }
            void Tab() { EnsurePara(); _spacePending = false; _out.Append("\\tab "); }
            void Escape(string s)
            {
                foreach (char c in s)
                {
                    if (c == '\\' || c == '{' || c == '}') _out.Append('\\').Append(c);
                    else if (c == '\t') _out.Append("\\tab ");
                    else if (c == '\n') _out.Append("\\line ");
                    else if (c == '\r' || c < ' ') continue;
                    else if (c < '\u0080') _out.Append(c);
                    else _out.Append("\\u").Append((short)c).Append('?');
                }
            }

            void ApplyStyle(string css)
            {
                foreach (string declaration in css.Split(';'))
                {
                    int colon = declaration.IndexOf(':');
                    if (colon <= 0) continue;
                    string key = declaration.Substring(0, colon).Trim().ToLowerInvariant();
                    string value = declaration.Substring(colon + 1).Trim().ToLowerInvariant();
                    if (key is "color" or "background" or "background-color")
                    {
                        int rgb = Colour(value), index = rgb < 0 ? 0 : ColourIndex(rgb);
                        if (key == "color") _style.Fg = index; else _style.Bg = index;
                    }
                    else if (key == "font-weight")
                        _style.Bold = value == "bold" || value == "bolder" ||
                                      (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) && weight >= 600);
                    else if (key == "font-style") _style.Italic = value == "italic" || value == "oblique";
                    else if (key is "text-decoration" or "text-decoration-line")
                    {
                        if (value.Contains("none")) { _style.Underline = false; _style.Strike = false; }
                        else
                        {
                            _style.Underline |= value.Contains("underline");
                            _style.Strike |= value.Contains("line-through");
                        }
                    }
                    else if (key == "font-size") { int size = Size(value); if (size > 0) _style.Size = size; }
                    // First family only: monospace maps to \f1, generics are ignored, the rest join the font table.
                    else if (key == "font-family")
                    {
                        string first = value.Split(',')[0].Trim().Trim('"', '\'').Trim();
                        var name = new StringBuilder(first.Length);
                        foreach (char ch in first)
                            if (ch == ' ' || ch == '-' || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) name.Append(ch);
                        string family = name.ToString().Trim();
                        if (family.Contains("mono") || family.Contains("courier") || family.Contains("consolas") ||
                            family.Contains("menlo") || family.Contains("monaco")) { _style.Mono = true; _style.Family = null; }
                        else if (family.Length > 0 && family is not "serif" and not "sans-serif" and not "cursive" and not "fantasy"
                            and not "system-ui" && !family.StartsWith("ui-", StringComparison.Ordinal))
                        { FontIndex(family); _style.Family = family; }
                    }
                    else if (key == "white-space" && value.Contains("pre")) _style.Pre = true;
                }
            }
            int ColourIndex(int rgb)
            {
                if (_colourIndex.TryGetValue(rgb, out int index)) return index;
                Colours.Add(rgb);
                _colourIndex[rgb] = Colours.Count;
                return Colours.Count;
            }
            int FontIndex(string name)
            {
                if (_fontIndex.TryGetValue(name, out int index)) return index;
                Fonts.Add(name);
                _fontIndex[name] = Fonts.Count;
                return Fonts.Count;
            }

            static int Colour(string value) // â†’ 0xRRGGBB, or -1 for inherit / transparent / unknown
            {
                if (string.IsNullOrEmpty(value)) return -1;
                if (value[0] == '#')
                {
                    string hex = value.Substring(1);
                    if (hex.Length == 3) hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
                    if (hex.Length == 8) hex = hex.Substring(0, 6); // #rrggbbaa
                    return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)
                        ? rgb & 0xFFFFFF : -1;
                }
                if (value.StartsWith("rgb", StringComparison.Ordinal))
                {
                    int open = value.IndexOf('('), close = open < 0 ? -1 : value.IndexOf(')', open + 1);
                    if (close < 0) return -1;
                    string[] parts = value.Substring(open + 1, close - open - 1).Split(',');
                    int r = 0, g = 0, b = 0;
                    if (parts.Length < 3 || (r = Channel(parts[0])) < 0 || (g = Channel(parts[1])) < 0 || (b = Channel(parts[2])) < 0) return -1;
                    return (r << 16) | (g << 8) | b;
                }
                switch (value)
                {
                    case "black": return 0x000000; case "white": return 0xFFFFFF; case "red": return 0xFF0000;
                    case "green": return 0x008000; case "lime": return 0x00FF00; case "blue": return 0x0000FF;
                    case "yellow": return 0xFFFF00; case "orange": return 0xFFA500; case "purple": return 0x800080;
                    case "gray": case "grey": return 0x808080; case "teal": return 0x008080;
                    case "navy": return 0x000080; case "maroon": return 0x800000; case "olive": return 0x808000;
                    case "aqua": case "cyan": return 0x00FFFF; case "brown": return 0xA52A2A;
                }
                return -1;
            }

            static int Channel(string value)
            {
                value = value.Trim();
                bool percent = value.EndsWith("%", StringComparison.Ordinal);
                if (!double.TryParse(percent ? value.TrimEnd('%') : value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
                    return -1;
                return Math.Max(0, Math.Min(255, (int)Math.Round(percent ? n * 255 / 100 : n)));
            }

            static int Size(string value) // â†’ half-points
            {
                if (string.IsNullOrEmpty(value)) return 0;
                int cut = value.Length;
                double mult = 1.5; // a bare number is px, as in a browser
                if (value.EndsWith("pt", StringComparison.Ordinal)) { mult = 2; cut -= 2; }
                else if (value.EndsWith("px", StringComparison.Ordinal)) cut -= 2;
                else if (value.EndsWith("rem", StringComparison.Ordinal)) { mult = BaseSize; cut -= 3; }
                else if (value.EndsWith("em", StringComparison.Ordinal)) { mult = BaseSize; cut -= 2; }
                else if (value.EndsWith("%", StringComparison.Ordinal)) { mult = BaseSize / 100.0; cut -= 1; }
                else if (value.EndsWith("vw", StringComparison.Ordinal) || value.EndsWith("vh", StringComparison.Ordinal)) return 0;
                bool ok = double.TryParse(value.Substring(0, cut), NumberStyles.Float, CultureInfo.InvariantCulture, out double number);
                return !ok || number <= 0 ? 0 : Math.Max(8, Math.Min(160, (int)Math.Round(number * mult))); // 4â€“80 pt
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
