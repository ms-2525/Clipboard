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
            // One rule for every search path: the same parser and matcher the bridge uses. This list
            // carries no tags, so an include tag cannot match here (the old UI has no tag data to
            // satisfy it); text terms behave exactly like `search`.
            var parsed = ClipSearch.Parse(query);
            var hits = new List<ClipboardItem>();
            foreach (var it in items)
                if (ClipSearch.Matches(it, null, parsed)) hits.Add(it);
            return hits;
        }
    }
}
