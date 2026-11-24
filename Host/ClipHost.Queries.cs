using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Clip.Core;
using Clip.Interop;
using Microsoft.Data.Sqlite;

namespace Clip.Host
{
    /// <summary>
    /// Read queries and the copy command; protocol/dispatch lives in ClipHost.cs. Everything that can load a
    /// list runs on <see cref="ReadQueue"/> with its own read-only connection, so ClipboardStore's connection
    /// never leaves the UI thread.
    /// </summary>
    internal sealed partial class ClipHost
    {
        // ---- background reads -----------------------------------------------------------------

        /// <summary>
        /// One FIFO thread for list reads. At most one bridge read connection exists at a time, so a burst of
        /// searches cannot pile connections up and replies leave in request order. A job must never touch
        /// ClipboardStore, the UI-thread <see cref="MetaReader"/> or any WPF object.
        /// </summary>
        internal sealed class ReadQueue : IDisposable
        {
            readonly BlockingCollection<Action> _jobs = new BlockingCollection<Action>();
            readonly Thread _thread;

            public ReadQueue()
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "Clip bridge reads" };
                _thread.Start();
            }

            public void Post(Action job)
            {
                try { _jobs.Add(job); }
                catch (InvalidOperationException) { } // queue closed during shutdown: the browser has no reader left
            }

            public void Dispose() => _jobs.CompleteAdding();

            void Run()
            {
                foreach (var job in _jobs.GetConsumingEnumerable())
                {
                    try { job(); }
                    catch (Exception) { } // each job reports its own failure; the only reader thread must survive
                }
            }
        }

        /// <summary>
        /// Read-only and unpooled: WAL lets it run beside the store's writer, and a fresh handle can never point
        /// at a file that a corruption recovery has renamed aside.
        /// </summary>
        internal static SqliteConnection OpenRead(string dbPath)
        {
            var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Rows + badges + tags for one page. Main history is the newest Config.MaxItems rows, the same scope
        /// <see cref="ReadSearch"/> searches; a tab is exempt from the cap and pages all of its items.
        /// </summary>
        internal static PageResult ReadPage(string dbPath, long pinnedTabId, int maxItems, long? tabId,
            int offset, int limit, string query, int total)
        {
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 500);
            int cap = Math.Max(1, maxItems);

            using var conn = OpenRead(dbPath);
            List<ClipboardItem> rows;
            if (!string.IsNullOrWhiteSpace(query))
            {
                var all = tabId.HasValue
                    ? ClipboardStore.ReadTabItems(conn, -1, tabId.Value)
                    : ClipboardStore.ReadRecent(conn, cap);
                var hits = SearchService.Filter(all, query);
                total = hits.Count;
                rows = hits.Skip(offset).Take(limit).ToList();
            }
            else
            {
                int take = tabId.HasValue ? offset + limit : Math.Min(offset + limit, cap);
                var all = tabId.HasValue
                    ? ClipboardStore.ReadTabItems(conn, take, tabId.Value)
                    : ClipboardStore.ReadRecent(conn, take);
                rows = all.Skip(offset).Take(limit).ToList();
            }

            return new PageResult { Total = total, Items = Dtos(conn, pinnedTabId, rows) };
        }

        /// <summary>The tag search on the reader thread; the reply is the same rows, order and unknownTags as before.</summary>
        internal static SearchResult ReadSearch(string dbPath, long pinnedTabId, int maxItems, long? tabId,
            int offset, int limit, string query)
        {
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 500);

            var q = ClipSearch.Parse(query);
            using var conn = OpenRead(dbPath);
            // Tabs are exempt from the history cap, so a container searches all of its items.
            var all = tabId.HasValue
                ? ClipboardStore.ReadTabItems(conn, -1, tabId.Value)
                : ClipboardStore.ReadRecent(conn, Math.Max(1, maxItems));

            bool byTag = q.Include.Length > 0 || q.Exclude.Length > 0;
            var meta = byTag ? ReadMeta(conn, all.Select(r => r.Id).ToList()) : null;
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
                meta = ReadMeta(conn, hits.Skip(offset).Take(limit).Select(r => r.Id).ToList());

            var items = new List<ItemDto>(Math.Min(limit, hits.Count));
            for (int i = offset; i < hits.Count && items.Count < limit; i++)
            {
                meta.TryGetValue(hits[i].Id, out var info);
                var tags = byTag ? hitTags[i] : ClipTags.Classify(hits[i], info?.Formats);
                items.Add(ToDto(hits[i], info, tags, pinnedTabId));
            }
            return new SearchResult { Total = hits.Count, Items = items, UnknownTags = q.UnknownTags };
        }

        static List<ItemDto> Dtos(SqliteConnection conn, long pinnedTabId, List<ClipboardItem> rows)
        {
            var meta = ReadMeta(conn, rows.Select(r => r.Id).ToList());
            var items = new List<ItemDto>(rows.Count);
            foreach (var row in rows)
            {
                meta.TryGetValue(row.Id, out var info);
                items.Add(ToDto(row, info, ClipTags.Classify(row, info?.Formats), pinnedTabId));
            }
            return items;
        }

        static ItemDto ToDto(ClipboardItem row, ItemMeta info, string[] tags, long pinnedTabId)
        {
            return new ItemDto
            {
                Id = row.Id,
                Kind = row.Kind,
                App = row.AppName,
                Title = row.WindowTitle,
                Time = row.CapturedAt,
                Pinned = row.TabId == pinnedTabId,
                TabId = row.TabId,
                Text = row.Text, // the stored PreviewChars-code-point preview; never re-cut it by Text.Length
                HasImage = row.Kind == "image",
                HasHtml = info?.HasHtml ?? false,
                HasRtf = info?.HasRtf ?? false,
                Formats = info?.Formats ?? new List<string>(),
                Tags = tags,
                Truncated = row.IsTruncated,
                RemainingChars = (int)Math.Min(int.MaxValue, row.RemainingChars)
            };
        }

        // ---- list metadata --------------------------------------------------------------------

        internal sealed class ItemMeta
        {
            public bool HasHtml;
            public bool HasRtf;
            public List<string> Formats;
        }

        /// <summary>
        /// SQLite allows 32 766 bound variables per statement, so ids are asked for in chunks; 500 measured
        /// 47.5 ms for 10 000 ids against 206 ms in one statement, and never trips the limit.
        /// </summary>
        const int MetaChunk = 500;

        static bool _metaFailureLogged;

        /// <summary>
        /// Format names and presences without Core changes or payload blobs. A failed chunk logs once and returns
        /// the rows already loaded: one bad chunk must not erase every badge on the page (AUDIT).
        /// </summary>
        internal static Dictionary<long, ItemMeta> ReadMeta(SqliteConnection conn, List<long> ids)
        {
            var map = new Dictionary<long, ItemMeta>();
            for (int start = 0; start < ids.Count; start += MetaChunk)
            {
                try
                {
                    using var cmd = conn.CreateCommand();
                    var names = new List<string>(Math.Min(MetaChunk, ids.Count - start));
                    for (int i = start; i < ids.Count && i < start + MetaChunk; i++)
                    {
                        names.Add("$p" + i);
                        cmd.Parameters.AddWithValue(names[names.Count - 1], ids[i]);
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
                catch (Exception ex)
                {
                    if (!_metaFailureLogged)
                    {
                        _metaFailureLogged = true;
                        Trace.WriteLine("Clip: item metadata failed; badges are partial for this reply. " + ex.Message);
                    }
                    return map;
                }
            }
            return map;
        }

        /// <summary>
        /// Metadata for the single-row getItem call, on a connection that is opened and closed inside the call.
        /// A persistent read-only handle would block ClipboardStore's recovery rename on Windows (File.Move fails
        /// while SQLite holds the file), and would then point at the renamed file. With no handle held between
        /// calls, the next call always opens whatever file is current (AUDIT P1).
        /// </summary>
        internal static Dictionary<long, ItemMeta> ReadMetaAt(string dbPath, List<long> ids)
        {
            try
            {
                using var conn = OpenRead(dbPath);
                return ReadMeta(conn, ids);
            }
            catch (Exception)
            {
                return new Dictionary<long, ItemMeta>(); // badges are cosmetic; getItem still returns the body
            }
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

        internal sealed class PageResult
        {
            public int Total { get; set; }
            public List<ItemDto> Items { get; set; }
        }

        internal sealed class SearchResult
        {
            public int Total { get; set; }
            public List<ItemDto> Items { get; set; }
            public string[] UnknownTags { get; set; }
        }

        internal sealed class ItemDto
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
            /// <summary>True when the body is longer than the preview: the page must load getItem before paste/edit.</summary>
            public bool Truncated { get; set; }
            /// <summary>Stored characters (SQLite code points) past the preview; 0 unless <see cref="Truncated"/>.</summary>
            public int RemainingChars { get; set; }
        }
    }
}
