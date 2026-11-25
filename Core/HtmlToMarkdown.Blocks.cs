using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Clip.Core
{
    /// <summary>Block structure for <see cref="HtmlToMarkdown"/>: paragraphs, headings, lists, tables.</summary>
    internal static partial class HtmlToMarkdown
    {
        sealed partial class Renderer
        {
            void Start(string name, string tag, int after)
            {
                if (AtRoot && _pendingItem) Marker();
                switch (name)
                {
                    case "p": case "div": case "section": case "article": case "header": case "footer":
                    case "figure": case "figcaption": case "address": case "main": case "aside": case "nav":
                    case "form": case "dl": case "dt": case "dd": case "fieldset":
                    case "thead": case "tbody": case "tfoot":
                        if (AtRoot) Gap(2);
                        break;
                    case "br":
                        if (AtRoot)
                        {
                            Flush();
                            if (_lineStart) break; // a leading <br> adds no blank line
                            RStrip();
                            _root.Append("  \n");
                            _lineStart = true;
                            _gap = 0;
                            _wroteAny = true;
                        }
                        else Cur.Append(' ');
                        break;
                    case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                        if (!AtRoot) break;
                        Flush();
                        Marker();
                        if (!_lineStart) Newline();
                        Prefix();
                        _root.Append('#', name[1] - '0').Append(' ');
                        _lineStart = false;
                        _wroteAny = true;
                        break;
                    case "hr":
                        if (!AtRoot) break;
                        Flush();
                        Marker();
                        if (!_lineStart) Newline();
                        Prefix();
                        _root.Append("---");
                        _wroteAny = true;
                        Newline();
                        Gap(2);
                        break;
                    case "blockquote":
                        if (AtRoot) { Gap(2); _quotes++; }
                        break;
                    case "pre":
                        if (!AtRoot) break;
                        Flush();
                        Marker();
                        if (!_lineStart) Newline();
                        Prefix();
                        _root.Append("```\n");
                        _lineStart = true;
                        _wroteAny = true;
                        _pre = true;
                        break;
                    case "ul": case "ol":
                        if (!AtRoot) break;
                        Gap(2);
                        int start = 1;
                        if (name == "ol")
                        {
                            string s = Attr(tag, after, "start");
                            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out start) || start < 1) start = 1;
                        }
                        _lists.Add(new ListState { Ordered = name == "ol", Next = start - 1 });
                        break;
                    case "li":
                        Item();
                        break;
                    case "table":
                        if (!AtRoot || _rows != null) break; // a nested table flattens into its cell
                        Gap(2);
                        _rows = new List<List<string>>();
                        break;
                    case "tr":
                        if (!AtRoot || _rows == null) break;
                        _row = new List<string>();
                        break;
                    case "td": case "th":
                        if (!AtRoot || _rows == null || _row == null) break;
                        _targets.Add(new Target(2, new StringBuilder(), null, _marks.Count));
                        break;
                    case "img":
                        Inline(Image(tag, after));
                        break;
                    case "input":
                        if (_pendingItem && AtRoot &&
                            string.Equals(Attr(tag, after, "type"), "checkbox", StringComparison.OrdinalIgnoreCase))
                        { _hasChecked = true; _checked = Attr(tag, after, "checked") != null; }
                        break;
                    case "a":
                        _targets.Add(new Target(0, new StringBuilder(), (Attr(tag, after, "href") ?? "").Trim(), _marks.Count));
                        break;
                    case "code": case "kbd": case "samp": case "tt":
                        _targets.Add(new Target(1, new StringBuilder(), null, _marks.Count));
                        break;
                    case "b": case "strong": Mark(name, "**", "**"); break;
                    case "i": case "em": case "cite": case "var": Mark(name, "*", "*"); break;
                    case "s": case "strike": case "del": Mark(name, "~~", "~~"); break;
                    case "u": case "ins": Mark(name, "<u>", "</u>"); break;
                    case "mark": Mark(name, "<mark>", "</mark>"); break;
                    case "span": case "font": Span(tag, after, name); return;
                }
                string css = Attr(tag, after, "style");
                if (css != null) ApplyStyle(css, name);
            }

            void End(string name)
            {
                switch (name)
                {
                    case "p": case "div": case "section": case "article": case "header": case "footer":
                    case "figure": case "figcaption": case "address": case "main": case "aside": case "nav":
                    case "form": case "dl": case "dt": case "dd": case "fieldset":
                    case "thead": case "tbody": case "tfoot":
                    case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                        Gap(2);
                        break;
                    case "blockquote":
                        if (AtRoot && _quotes > 0) _quotes--;
                        Gap(2);
                        break;
                    case "pre":
                        if (_pre && AtRoot)
                        {
                            _root.Append("\n```");
                            _pre = false;
                            _lineStart = true;
                            Gap(2);
                        }
                        break;
                    case "ul": case "ol":
                        if (AtRoot && _lists.Count > 0) _lists.RemoveAt(_lists.Count - 1);
                        Gap(2);
                        break;
                    case "li": CloseItem(); break;
                    case "tr":
                        if (AtRoot && _rows != null && _row != null)
                        {
                            while (TopIs(2)) EndCell();
                            _rows.Add(_row);
                            _row = null;
                        }
                        break;
                    case "td": case "th":
                        if (TopIs(2)) EndCell();
                        break;
                    case "table":
                        if (AtRoot && _rows != null) { EmitTable(); Gap(2); }
                        break;
                    case "a":
                        if (TopIs(0)) EndLink();
                        break;
                    case "code": case "kbd": case "samp": case "tt":
                        if (TopIs(1)) EndCode();
                        break;
                }
                CloseMarks(name);
            }

            // Unclosed tags at the end of the fragment still flush their content.
            void EndOfInput()
            {
                while (_targets.Count > 0)
                {
                    var t = _targets[_targets.Count - 1];
                    if (t.Kind == 2 && _row != null) { EndCell(); continue; }
                    CloseMarksAbove(t.MarkDepth);
                    _targets.RemoveAt(_targets.Count - 1);
                    Inline(t.Buf.ToString());
                }
                while (_marks.Count > 0)
                {
                    int k = _marks.Count - 1;
                    Cur.Append(_marks[k].Closer);
                    _marks.RemoveAt(k);
                }
                if (_rows != null) EmitTable();
            }

            // ---- lists -------------------------------------------------------

            void Item()
            {
                if (!AtRoot) return;
                if (_wroteAny) Newline(); else _lineStart = true;
                _gap = 0;
                _pendingItem = true;
                _hasChecked = false;
                _checked = false;
            }

            void Marker()
            {
                if (!_pendingItem || !AtRoot) return;
                _pendingItem = false;
                Flush();
                for (int i = 0; i < _quotes; i++) _root.Append("> ");
                int depth = _lists.Count;
                _root.Append(' ', Math.Max(0, depth - 1) * 2);
                if (depth == 0) _root.Append("- ");
                else if (_hasChecked) _root.Append(_checked ? "- [x] " : "- [ ] ");
                else if (!_lists[depth - 1].Ordered) _root.Append("- ");
                else
                {
                    var top = _lists[depth - 1];
                    top.Next++;
                    _lists[depth - 1] = top;
                    _root.Append(top.Next).Append(". ");
                }
                _lineStart = false;
                _wroteAny = true;
            }

            void CloseItem()
            {
                if (!AtRoot) return;
                if (_pendingItem) { _pendingItem = false; return; } // empty <li>: no bullet, like HtmlToRtf
                Newline(); // items stay tight: no blank line between them
            }

            // ---- tables ------------------------------------------------------

            // Text content of a <pre> region: tags strip, entities decode. A literal '<' in real
            // code arrives escaped (&lt;), so a raw '<' here is always markup — except a broken
            // tail with no '>', which is kept rather than dropped.
            static string PreText(string s)
            {
                var sb = new StringBuilder(s.Length);
                int i = 0;
                while (i < s.Length)
                {
                    int lt = s.IndexOf('<', i);
                    if (lt < 0) { sb.Append(s.Substring(i)); break; }
                    sb.Append(s.Substring(i, lt - i));
                    int gt = FindTagEnd(s, lt + 1);
                    if (gt < 0) { sb.Append(s.Substring(lt)); break; }
                    i = gt + 1;
                }
                return DecodeEntities(sb.ToString());
            }

            void EndCell()
            {
                var t = _targets[_targets.Count - 1];
                CloseMarksAbove(t.MarkDepth);
                _targets.RemoveAt(_targets.Count - 1);
                if (_row != null) _row.Add(CellText(t.Buf.ToString()));
            }

            void EmitTable()
            {
                if (_row != null) { _rows.Add(_row); _row = null; }
                while (TopIs(2)) EndCell();
                var rows = _rows;
                _rows = null;
                if (rows == null || rows.Count == 0) return;
                int cols = 1;
                foreach (var r in rows) cols = Math.Max(cols, r.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    Flush();
                    Prefix();
                    _root.Append("| ");
                    for (int c = 0; c < cols; c++)
                    {
                        if (c > 0) _root.Append(" | ");
                        _root.Append(c < rows[i].Count ? rows[i][c] : "");
                    }
                    _root.Append(" |");
                    RawNewline();
                    if (i == 0)
                    {
                        Prefix();
                        _root.Append("| ");
                        for (int c = 0; c < cols; c++)
                        {
                            if (c > 0) _root.Append(" | ");
                            _root.Append("---");
                        }
                        _root.Append(" |");
                        RawNewline();
                    }
                }
                _wroteAny = true;
            }

            static string CellText(string s)
            {
                var sb = new StringBuilder(s.Length);
                bool space = true; // trims the edges the same way it collapses the middle
                foreach (char c in s)
                {
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f') { space = true; continue; }
                    if (space && sb.Length > 0) sb.Append(' ');
                    space = false;
                    sb.Append(c == '|' ? "\\|" : c.ToString());
                }
                return sb.ToString();
            }
        }
    }
}
