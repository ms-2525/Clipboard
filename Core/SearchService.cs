using System;
using System.Collections.Generic;

namespace Clip.Core
{
    internal static class SearchService
    {
        public static List<ClipboardItem> Filter(List<ClipboardItem> items, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return items;
            var q = query.Trim();
            var hits = new List<ClipboardItem>();
            foreach (var it in items)
            {
                if ((it.Text != null && it.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (it.AppName != null && it.AppName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                    hits.Add(it);
            }
            return hits;
        }
    }
}
