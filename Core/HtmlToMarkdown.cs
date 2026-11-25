using System;
using System.Collections.Generic;
using System.Text;

namespace Clip.Core
{
    /// <summary>
    /// CF_HTML fragment → Markdown for "Open in app" (Host\ClipHost.Export.cs). Structure
    /// (headings, lists, tables, quotes, code, links) becomes real Markdown; colours, sizes,
    /// highlights and underline have no Markdown syntax, so they ride along as inline HTML
    /// spans that most Markdown apps render. Unknown tags unwrap; script/style/head/svg/math
    /// and comment text never reaches the file. Scanner mirrors HtmlToRtf on purpose — the
    /// row path there stays untouched.
    /// </summary>
    internal static partial class HtmlToMarkdown
    {
        public static string Convert(string html)
        {
            var root = new StringBuilder(html == null ? 64 : Math.Min(html.Length * 2, 1 << 16));
            new Renderer(root).Run(html ?? "");
            string s = root.ToString().TrimEnd();
            return s.Length == 0 ? "" : s + "\n";
        }

        // 0 = link, 1 = code, 2 = table cell: buffers whose content wraps on close.
        readonly struct Target
        {
            public readonly int Kind;
            public readonly StringBuilder Buf;
            public readonly string Href;
            public readonly int MarkDepth;
            public Target(int kind, StringBuilder buf, string href, int markDepth)
            { Kind = kind; Buf = buf; Href = href; MarkDepth = markDepth; }
        }

        readonly struct Mark
        {
            public readonly string Name;
            public readonly string Closer;
            public Mark(string name, string closer) { Name = name; Closer = closer; }
        }

        struct ListState
        {
            public bool Ordered;
            public int Next;
        }

        sealed partial class Renderer
        {
            readonly StringBuilder _root;
            readonly List<Target> _targets = new List<Target>();
            readonly List<Mark> _marks = new List<Mark>();
            readonly List<ListState> _lists = new List<ListState>();
            List<List<string>> _rows;
            List<string> _row;

            int _quotes;
            bool _pre;
            int _gap; // 1 = newline, 2 = blank line (degrades to 1 inside lists)
            bool _lineStart = true;
            bool _wroteAny;
            bool _pendingItem; // <li> waiting for its first content: the marker is deferred
            bool _checked;
            bool _hasChecked;

            public Renderer(StringBuilder root) { _root = root; }

            bool AtRoot => _targets.Count == 0;
            StringBuilder Cur => AtRoot ? _root : _targets[_targets.Count - 1].Buf;
            bool TopIs(int kind) => !AtRoot && _targets[_targets.Count - 1].Kind == kind;

            public void Run(string html)
            {
                int i = 0;
                while (i < html.Length)
                {
                    if (_pre)
                    {
                        // Code samples may contain markup (<code>, <span>): only the text survives.
                        int close = IndexOfIgnoreCase(html, "</pre", i);
                        if (close < 0) close = html.Length;
                        if (close > i)
                        {
                            _root.Append(PreText(html.Substring(i, close - i)));
                            i = close;
                            continue;
                        }
                        // At the closing tag: fall through so Tag() consumes it and
                        // End("pre") closes the fence (_pre stays set for that check).
                    }
                    if (html[i] != '<')
                    {
                        int start = i;
                        while (i < html.Length && html[i] != '<') i++;
                        Write(DecodeEntities(html.Substring(start, i - start)), false);
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
                    { Write("<", false); i++; continue; }
                    i = Tag(html, i);
                }
                EndOfInput();
            }

            int Tag(string html, int start)
            {
                int gt = FindTagEnd(html, start + 1);
                if (gt < 0) { Write("<", false); return start + 1; } // not a tag after all: keep the char
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

                // Elements whose *content* must never reach the file.
                if (name == "script" || name == "style" || name == "head" || name == "svg" || name == "math")
                {
                    if (closing) return next;
                    int end = IndexOfIgnoreCase(html, "</" + name, next);
                    if (end < 0) return name == "head" ? next : html.Length;
                    int gt2 = html.IndexOf('>', end);
                    return gt2 < 0 ? html.Length : gt2 + 1;
                }

                if (closing) End(name); else Start(name, inner, after);
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

            /// <summary>The value of one attribute, or null when absent ("" when valueless, e.g. checked).</summary>
            static string Attr(string tag, int from, string name)
            {
                int i = from;
                while (i < tag.Length)
                {
                    while (i < tag.Length && (char.IsWhiteSpace(tag[i]) || tag[i] == '/')) i++;
                    int start = i;
                    while (i < tag.Length && (char.IsLetterOrDigit(tag[i]) || tag[i] == '-' || tag[i] == ':' || tag[i] == '_')) i++;
                    if (i == start) { i++; continue; }
                    string key = tag.Substring(start, i - start);
                    int j = i;
                    while (j < tag.Length && char.IsWhiteSpace(tag[j])) j++;
                    bool hasVal = false;
                    string val = "";
                    if (j < tag.Length && tag[j] == '=')
                    {
                        hasVal = true;
                        j++;
                        while (j < tag.Length && char.IsWhiteSpace(tag[j])) j++;
                        if (j < tag.Length && (tag[j] == '"' || tag[j] == '\''))
                        {
                            int close = tag.IndexOf(tag[j], j + 1);
                            val = close < 0 ? tag.Substring(j + 1) : tag.Substring(j + 1, close - j - 1);
                        }
                        else
                        {
                            int s2 = j;
                            while (j < tag.Length && !char.IsWhiteSpace(tag[j])) j++;
                            val = tag.Substring(s2, j - s2);
                        }
                    }
                    if (key.Equals(name, StringComparison.OrdinalIgnoreCase)) return hasVal ? val : "";
                }
                return null;
            }
        }
    }
}
