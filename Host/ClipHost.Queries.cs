using System;
using System.Collections.Generic;
using System.Linq;
using Clip.Core;
using Clip.Interop;
using Microsoft.Data.Sqlite;

namespace Clip.Host
{
    /// <summary>Read queries and the copy command; protocol/dispatch lives in ClipHost.cs.</summary>
    internal sealed partial class ClipHost
    {
        const int PreviewChars = ClipboardStore.PreviewChars;

        // ---- queries --------------------------------------------------------------------------

        PageResult Page(long? tabId, int offset, int limit, string query)
        {
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 500);

            int total;
            List<ClipboardItem> rows;
            if (!string.IsNullOrWhiteSpace(query))
            {
                // Phase 1 filters the loaded list; a SQL search can replace this without changing the DTO.
                var all = tabId.HasValue ? _store.GetTabItems(-1, tabId.Value) : _store.GetRecent(-1);
                var hits = SearchService.Filter(all, query);
                total = hits.Count;
                rows = hits.Skip(offset).Take(limit).ToList();
            }
            else
            {
                total = Total(tabId);
                var all = tabId.HasValue ? _store.GetTabItems(offset + limit, tabId.Value) : _store.GetRecent(offset + limit);
                rows = all.Skip(offset).Take(limit).ToList();
            }

            var meta = Meta(rows.Select(r => r.Id).ToList());
            var items = new List<ItemDto>(rows.Count);
            foreach (var row in rows)
            {
                meta.TryGetValue(row.Id, out var info);
                items.Add(ToDto(row, info, ClipTags.Classify(row, info?.Formats)));
            }
            return new PageResult { Total = total, Items = items };
        }

        // ---- tag search -----------------------------------------------------------------------

        /// <summary>
        /// Step 1 search over the whole recent set, not just the loaded page. A word query needs no tags
        /// until the reply page is built; a tag query classifies every candidate once and keeps the tags
        /// beside the hits so the page is not classified a second time.
        /// </summary>
        SearchResult Search(long? tabId, int offset, int limit, string query)
        {
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 500);

            var q = ClipSearch.Parse(query);
            // Tabs are exempt from the history cap, so a container searches all of its items.
            var all = tabId.HasValue
                ? _store.GetTabItems(-1, tabId.Value)
                : _store.GetRecent(Math.Max(1, _cfg.MaxItems));

            bool byTag = q.Include.Length > 0 || q.Exclude.Length > 0;
            var meta = byTag ? Meta(all.Select(r => r.Id).ToList()) : null;
            var hits = new List<ClipboardItem>(Math.Min(all.Count, 64));
            var hitTags = byTag ? new List<string[]>(hits.Capacity) : null;
            foreach (var row in all)
            {
                string[] tags = null;
                if (byTag)
                {
                    meta.TryGetValue(row.Id, out var info);
                    tags = ClipTags.Classify(row, info?.Formats);
                }
                if (!ClipSearch.Matches(row, tags, q)) continue;
                hits.Add(row);
                hitTags?.Add(tags);
            }

            // The reply carries only this page, so a word query pays for tags on those rows alone.
            if (!byTag && hits.Count > 0)
                meta = Meta(hits.Skip(offset).Take(limit).Select(r => r.Id).ToList());

            var items = new List<ItemDto>(Math.Min(limit, hits.Count));
            for (int i = offset; i < hits.Count && items.Count < limit; i++)
            {
                meta.TryGetValue(hits[i].Id, out var info);
                var tags = byTag ? hitTags[i] : ClipTags.Classify(hits[i], info?.Formats);
                items.Add(ToDto(hits[i], info, tags));
            }
            return new SearchResult { Total = hits.Count, Items = items, UnknownTags = q.UnknownTags };
        }

        ItemDto ToDto(ClipboardItem row, ItemMeta info, string[] tags)
        {
            // item rows carry only PreviewChars of text; the DTO promises no more.
            string text = row.Text;
            if (text != null && text.Length > PreviewChars) text = text.Substring(0, PreviewChars);
            return new ItemDto
            {
                Id = row.Id,
                Kind = row.Kind,
                App = row.AppName,
                Title = row.WindowTitle,
                Time = row.CapturedAt,
                Pinned = row.TabId == _store.PinnedTabId,
                TabId = row.TabId,
                Text = text,
                HasImage = row.Kind == "image",
                HasHtml = info?.HasHtml ?? false,
                HasRtf = info?.HasRtf ?? false,
                Formats = info?.Formats ?? new List<string>(),
                Tags = tags,
                MoreCount = (int)Math.Min(int.MaxValue, Math.Max(0, row.TextLength - (row.Text?.Length ?? 0)))
            };
        }

        int Total(long? tabId)
        {
            if (!tabId.HasValue)
            {
                var (clips, images) = _store.Counts();
                return clips + images;
            }
            foreach (var tab in _store.ListTabs())
                if (tab.id == tabId.Value) return tab.count;
            return 0;
        }

        object Counts()
        {
            var (clips, images) = _store.Counts();
            return new { clips, images };
        }

        object Tabs() => _store.ListTabs()
            .Select(t => new { id = t.id, name = t.name, kind = t.kind, count = t.count })
            .ToList();

        // ---- list metadata --------------------------------------------------------------------

        sealed class ItemMeta
        {
            public bool HasHtml;
            public bool HasRtf;
            public List<string> Formats;
        }

        /// <summary>
        /// Format names and presences without Core changes or payload blobs. A read-only connection is enough:
        /// WAL allows it beside the store's writer, and a DB that cannot answer only loses row badges.
        /// </summary>
        Dictionary<long, ItemMeta> Meta(List<long> ids)
        {
            var map = new Dictionary<long, ItemMeta>();
            if (ids.Count == 0) return map;
            try
            {
                var conn = MetaConnection();
                if (conn == null) return map;

                using var cmd = conn.CreateCommand();
                var names = new List<string>(ids.Count);
                for (int i = 0; i < ids.Count; i++)
                {
                    names.Add("$p" + i);
                    cmd.Parameters.AddWithValue(names[i], ids[i]);
                }
                cmd.CommandText = $@"
SELECT i.id,
       CASE WHEN i.html IS NOT NULL AND i.html <> '' THEN 1 ELSE 0 END,
       CASE WHEN i.rtf IS NOT NULL AND i.rtf <> '' THEN 1 ELSE 0 END,
       (SELECT group_concat(f.fmt, char(31)) FROM item_formats f WHERE f.item_id = i.id)
FROM items i WHERE i.id IN ({string.Join(",", names)})";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    map[r.GetInt64(0)] = new ItemMeta
                    {
                        HasHtml = r.GetInt32(1) != 0,
                        HasRtf = r.GetInt32(2) != 0,
                        Formats = r.IsDBNull(3)
                            ? new List<string>()
                            : r.GetString(3).Split('\u001f', StringSplitOptions.RemoveEmptyEntries).ToList()
                    };
                }
            }
            catch
            {
                // Metadata is cosmetic (badges); the list must still render if the side connection fails.
            }
            return map;
        }

        SqliteConnection MetaConnection()
        {
            if (_meta != null || _metaFailed) return _meta;
            try
            {
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = _dbPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString();
                var conn = new SqliteConnection(connectionString);
                conn.Open();
                _meta = conn;
            }
            catch
            {
                _metaFailed = true; // one failed open is enough; do not retry on every page
            }
            return _meta;
        }

        // ---- copy (the existing list's Copy path) ----------------------------------------------

        /// <summary>
        /// Same restore ladder as App.OnCopyRequested, without paste/hide. Internal static so a scratch
        /// harness can exercise the exact path against a DB copy without a live WebView2.
        /// </summary>
        internal static void CopyToClipboard(ClipboardStore store, long id, bool plain)
        {
            string text = store.GetText(id) ?? "";
            if (plain)
            {
                ClipboardFormats.WithClipboardRetry(() => System.Windows.Clipboard.SetText(text));
                return;
            }

            var image = store.GetImage(id);
            var formats = store.GetFormats(id);
            var rich = store.GetRich(id);

            if (image?.Kind == "image" && image.Image != null)
            {
                PasteInjector.SetImageClipboard(image.Image);
            }
            else if (formats.Count > 0)
            {
                ClipboardFormats.Restore(formats);
            }
            else if (!string.IsNullOrEmpty(rich?.Rtf) || !string.IsNullOrEmpty(rich?.Html))
            {
                var data = new System.Windows.DataObject();
                data.SetData(System.Windows.DataFormats.UnicodeText, text);
                if (!string.IsNullOrEmpty(rich.Rtf)) data.SetData(System.Windows.DataFormats.Rtf, rich.Rtf);
                if (!string.IsNullOrEmpty(rich.Html)) data.SetData(System.Windows.DataFormats.Html, rich.Html);
                ClipboardFormats.WithClipboardRetry(() => System.Windows.Clipboard.SetDataObject(data, true));
            }
            else
            {
                ClipboardFormats.WithClipboardRetry(() => System.Windows.Clipboard.SetText(text));
            }
        }

        // ---- DTO ------------------------------------------------------------------------------

        sealed class PageResult
        {
            public int Total { get; set; }
            public List<ItemDto> Items { get; set; }
        }

        sealed class SearchResult
        {
            public int Total { get; set; }
            public List<ItemDto> Items { get; set; }
            public string[] UnknownTags { get; set; }
        }

        sealed class ItemDto
        {
            public long Id { get; set; }
            public string Kind { get; set; }
            public string App { get; set; }
            public string Title { get; set; }
            /// <summary>The stored local timestamp (yyyy-MM-ddTHH:mm:ss.fff), so the page owns formatting.</summary>
            public string Time { get; set; }
            public bool Pinned { get; set; }
            public long? TabId { get; set; }
            public string Text { get; set; }
            public bool HasImage { get; set; }
            public bool HasHtml { get; set; }
            public bool HasRtf { get; set; }
            public List<string> Formats { get; set; }
            public string[] Tags { get; set; }
            public int MoreCount { get; set; }
        }
    }
}
