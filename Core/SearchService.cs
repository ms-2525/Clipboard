using System;
using System.Collections.Generic;

namespace Clip.Core
{
    internal static class SearchService
    {
        /// <summary>
        /// The one text-comparison rule: ordinal, case-insensitive substring. SQLite's <c>lower()</c> folds
        /// ASCII only, so <see cref="ClipboardStore.SearchFull"/> calls this same predicate as a SQL function
        /// rather than comparing in SQL, and both search paths agree on accented text (AUDIT P1).
        /// </summary>
        internal static bool Contains(ReadOnlySpan<char> value, ReadOnlySpan<char> query) =>
            value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        internal static bool Matches(string value, string query) =>
            value != null && Contains(value.AsSpan(), query.AsSpan());

        public static List<ClipboardItem> Filter(List<ClipboardItem> items, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return items;
            var q = query.Trim();
            var hits = new List<ClipboardItem>();
            foreach (var it in items)
                if (Matches(it.Text, q) || Matches(it.AppName, q)) hits.Add(it);
            return hits;
        }
    }
}
