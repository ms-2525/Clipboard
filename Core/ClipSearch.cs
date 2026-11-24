using System;
using System.Collections.Generic;

namespace Clip.Core
{
    /// <summary>
    /// Step 1 search grammar: whitespace-separated words are ANDed, a known <c>tag:</c> filters by the
    /// tags <see cref="ClipTags"/> derives, and a leading <c>-</c> excludes. Pure (no IO, no state); the
    /// per-row match is allocation-free and the query is parsed once per request.
    /// </summary>
    public static class ClipSearch
    {
        /// <summary>Every tag <see cref="ClipTags"/> can emit as a language; <c>code:</c> means any of them.</summary>
        static readonly string[] LanguageTags =
        {
            "csharp", "javascript", "typescript", "python", "rust", "php", "go", "java", "shell"
        };

        /// <summary>
        /// Splits a query into positive text terms, excluded text terms (a leading '-' is kept in
        /// <see cref="ClipSearchQuery.Terms"/>), included tags, excluded tags and tag-shaped names that
        /// are not known tags (those still search as text, so nothing the user typed is ignored).
        /// </summary>
        public static ClipSearchQuery Parse(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return ClipSearchQuery.Empty;

            var terms = new List<string>();
            var include = new List<string>();
            var exclude = new List<string>();
            var unknown = new List<string>();
            int i = 0;
            while (i < query.Length)
            {
                while (i < query.Length && char.IsWhiteSpace(query[i])) i++;
                if (i >= query.Length) break;

                bool negate = false;
                if (query[i] == '-' && i + 1 < query.Length && !char.IsWhiteSpace(query[i + 1]))
                {
                    negate = true;
                    i++;
                }

                if (query[i] == '"')
                {
                    i++;
                    int start = i;
                    while (i < query.Length && query[i] != '"') i++;
                    string phrase = query.Substring(start, i - start);
                    if (i < query.Length) i++; // closing quote
                    if (phrase.Length > 0) terms.Add(negate ? "-" + phrase : phrase);
                    continue;
                }

                int tokenStart = i;
                while (i < query.Length && !char.IsWhiteSpace(query[i])) i++;
                string token = query.Substring(tokenStart, i - tokenStart);
                if (token.Length == 0) continue;

                if (TryTagToken(token, out string tag))
                {
                    if (negate) exclude.Add(tag);
                    else include.Add(tag);
                }
                else
                {
                    // "foo:" is not a filter, but it is still what the user typed: search it as text
                    // and let the page warn that the tag is unknown.
                    if (IsTagShaped(token, out string name)) AddUnknown(unknown, name);
                    terms.Add(negate ? "-" + token : token);
                }
            }

            return new ClipSearchQuery(Freeze(terms), Freeze(include), Freeze(exclude), Freeze(unknown));
        }

        /// <summary>
        /// True when every text term is present (or every excluded one absent) and every included tag
        /// is present while every excluded tag is absent. Text comparison is the same ordinal,
        /// case-insensitive substring <see cref="SearchService.Filter"/> uses.
        /// </summary>
        public static bool Matches(ClipboardItem item, IReadOnlyList<string> tags, ClipSearchQuery query)
        {
            if (query == null) return true;

            for (int i = 0; i < query.Terms.Length; i++)
            {
                string term = query.Terms[i];
                bool negated = term.Length > 1 && term[0] == '-';
                bool found = Contains(item, term.AsSpan(negated ? 1 : 0));
                if (negated ? found : !found) return false;
            }

            for (int i = 0; i < query.Include.Length; i++)
                if (!HasTag(tags, query.Include[i])) return false;
            for (int i = 0; i < query.Exclude.Length; i++)
                if (HasTag(tags, query.Exclude[i])) return false;
            return true;
        }

        // ---- grammar ----------------------------------------------------------------------------

        /// <summary>
        /// A tag token is exactly a known tag name followed by ':' and nothing else ("image:", "excel:").
        /// "https://x" and "C:\path" never end in ':' and a one-letter drive ("C:") is refused by the
        /// shape test, so a URL or path cannot silently become a filter.
        /// </summary>
        static bool TryTagToken(string token, out string tag)
        {
            tag = null;
            return IsTagShaped(token, out string name) && TryCanonical(name, out tag);
        }

        static bool IsTagShaped(string token, out string name)
        {
            name = null;
            if (token.Length < 3 || token[token.Length - 1] != ':') return false; // "C:" is a path, not a tag
            if (!IsAsciiLetter(token[0])) return false;
            for (int i = 1; i < token.Length - 1; i++)
            {
                char c = token[i];
                if (!IsAsciiLetter(c) && !char.IsDigit(c) && c != '-' && c != '_') return false;
            }
            name = token.Substring(0, token.Length - 1);
            return true;
        }

        /// <summary>The known ids <see cref="ClipTags"/> emits, plus the aliases the step plan promises.</summary>
        static bool TryCanonical(string name, out string tag)
        {
            string lower = name.ToLowerInvariant();
            switch (lower)
            {
                case "image":
                case "img":
                case "pic":
                case "photo":
                case "screenshot":
                    tag = "image";
                    return true;

                case "files":
                case "file":
                case "attachment":
                    tag = "files";
                    return true;

                case "spreadsheet":
                case "excel":
                case "sheet":
                case "xls":
                case "csv":
                    tag = "spreadsheet";
                    return true;

                case "link":
                case "url":
                case "website":
                    tag = "link";
                    return true;

                case "code":
                    tag = "code";
                    return true;

                case "email":
                case "path":
                case "color":
                case "number":
                case "json":
                case "html":
                case "sql":
                case "xml":
                case "csharp":
                case "javascript":
                case "typescript":
                case "python":
                case "rust":
                case "php":
                case "go":
                case "java":
                case "shell":
                    tag = lower;
                    return true;

                default:
                    tag = null;
                    return false;
            }
        }

        static void AddUnknown(List<string> unknown, string name)
        {
            for (int i = 0; i < unknown.Count; i++)
                if (string.Equals(unknown[i], name, StringComparison.OrdinalIgnoreCase)) return;
            unknown.Add(name.ToLowerInvariant());
        }

        // ---- matching ---------------------------------------------------------------------------

        /// <summary>The value is a span so a "-" exclusion never allocates a stripped copy per row.</summary>
        static bool Contains(ClipboardItem item, ReadOnlySpan<char> value)
        {
            if (item == null) return false;
            if (item.Text != null && SearchService.Contains(item.Text.AsSpan(), value)) return true;
            // AppName is searchable in the existing SearchService.Filter too.
            if (item.AppName != null && SearchService.Contains(item.AppName.AsSpan(), value)) return true;
            // An image (or a legacy row) has no text; its file list and window title are what it has.
            if (item.Text == null)
            {
                if (item.Uris != null && SearchService.Contains(item.Uris.AsSpan(), value)) return true;
                if (item.WindowTitle != null && SearchService.Contains(item.WindowTitle.AsSpan(), value)) return true;
            }
            return false;
        }

        static bool HasTag(IReadOnlyList<string> tags, string tag)
        {
            if (tags == null) return false;
            if (string.Equals(tag, "code", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < tags.Count; i++)
                    if (IsLanguage(tags[i])) return true;
                return false;
            }
            for (int i = 0; i < tags.Count; i++)
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool IsLanguage(string tag)
        {
            for (int i = 0; i < LanguageTags.Length; i++)
                if (string.Equals(LanguageTags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        static string[] Freeze(List<string> list) => list.Count == 0 ? Array.Empty<string>() : list.ToArray();
    }

    /// <summary>
    /// A parsed query. <see cref="Terms"/> keeps its order and sign: "foo" must occur, "-foo" must not.
    /// <see cref="Include"/> / <see cref="Exclude"/> hold canonical tag ids ("excel:" appears as
    /// "spreadsheet"); <see cref="UnknownTags"/> holds lower-case names, without the colon, for a hint.
    /// </summary>
    public sealed class ClipSearchQuery
    {
        public string[] Terms { get; }
        public string[] Include { get; }
        public string[] Exclude { get; }
        public string[] UnknownTags { get; }

        internal ClipSearchQuery(string[] terms, string[] include, string[] exclude, string[] unknownTags)
        {
            Terms = terms;
            Include = include;
            Exclude = exclude;
            UnknownTags = unknownTags;
        }

        internal static readonly ClipSearchQuery Empty = new ClipSearchQuery(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
    }
}
