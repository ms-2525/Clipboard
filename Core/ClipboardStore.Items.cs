using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Clip.Core
{
    public sealed partial class ClipboardStore
    {
        /// <summary>Row only. Use <see cref="InsertWithFormats"/> so the row and its format blobs commit together.</summary>
        public void Insert(ClipboardItem it)
        {
            using var tx = _conn.BeginTransaction();
            InsertRow(it, tx);
            tx.Commit();
        }

        /// <summary>The capture write path: item row + all format blobs in ONE transaction (A2-07).</summary>
        public void InsertWithFormats(ClipboardItem it)
        {
            using var tx = _conn.BeginTransaction();
            InsertRow(it, tx);
            InsertFormatsCore(it.Id, it.Formats, tx);
            tx.Commit();
        }

        void InsertRow(ClipboardItem it, SqliteTransaction tx)
        {
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO items (kind, text, html, rtf, image, thumb, uris, app_name, window_title, captured_at, size_bytes, image_hash, sort_seq)
VALUES ($kind, $text, $html, $rtf, $image, $thumb, $uris, $app, $title, $at, $size, $hash,
  (SELECT COALESCE(MAX(sort_seq),0)+1 FROM items))";
            cmd.Parameters.AddWithValue("$kind", it.Kind);
            cmd.Parameters.AddWithValue("$text", (object)it.Text ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$html", (object)it.Html ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$rtf", (object)it.Rtf ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$image", (object)it.Image ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$thumb", (object)it.Thumb ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$uris", (object)it.Uris ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$app", (object)it.AppName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$title", (object)it.WindowTitle ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$at", it.CapturedAt);
            cmd.Parameters.AddWithValue("$size", it.SizeBytes);
            cmd.Parameters.AddWithValue("$hash", (object)ImageHash(it) ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            using var id = _conn.CreateCommand();
            id.Transaction = tx;
            id.CommandText = "SELECT last_insert_rowid()";
            it.Id = (long)id.ExecuteScalar();
        }

        /// <summary>Full SHA-256 of the PNG, hex. Deterministic and collision-safe enough to be the dedupe key.</summary>
        static string ImageHash(ClipboardItem it) =>
            it.Kind == "image" && it.Image != null && it.Image.Length > 0
                ? it.ImageHash ?? Convert.ToHexString(SHA256.HashData(it.Image))
                : null;

        /// <summary>
        /// Undo primitive for a deleted row: same id, tab, formats and captured_at, re-inserted in one transaction.
        /// No dedupe (an explicit user action) and no eviction — the cap re-applies on the next capture. A
        /// restored clip takes the next sort_seq, so undo brings it back at the top where the user is looking.
        /// The retired `pinned` flag is not restored; tab_id carries the pin state.
        /// </summary>
        public long Restore(ClipboardItem it)
        {
            // The tab may have been deleted after the row was removed. A restored row pointing at a tab that
            // no longer exists is in no list at all (AUDIT P1), so it lands in main history where it is visible.
            if (it.TabId.HasValue && !TabExists(it.TabId.Value)) it.TabId = null;

            using var tx = _conn.BeginTransaction();
            using (var cmd = _conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO items (id, kind, text, html, rtf, image, thumb, uris, app_name, window_title, captured_at, size_bytes, tab_id, image_hash, sort_seq)
VALUES ($id, $kind, $text, $html, $rtf, $image, $thumb, $uris, $app, $title, $at, $size, $tab, $hash,
  (SELECT COALESCE(MAX(sort_seq),0)+1 FROM items))";
                cmd.Parameters.AddWithValue("$id", it.Id);
                cmd.Parameters.AddWithValue("$kind", it.Kind);
                cmd.Parameters.AddWithValue("$text", (object)it.Text ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$html", (object)it.Html ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$rtf", (object)it.Rtf ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$image", (object)it.Image ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$thumb", (object)it.Thumb ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$uris", (object)it.Uris ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$app", (object)it.AppName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$title", (object)it.WindowTitle ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$at", it.CapturedAt);
                cmd.Parameters.AddWithValue("$size", it.SizeBytes);
                cmd.Parameters.AddWithValue("$tab", (object)it.TabId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$hash", (object)ImageHash(it) ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            InsertFormatsCore(it.Id, it.Formats, tx);
            tx.Commit();
            return it.Id;
        }

        /// <summary>
        /// Exact-match dedupe against main history only: byte-for-byte (case- and space-sensitive; SQLite BINARY
        /// collation, no C# normalisation). A copy in a tab is never matched, so it cannot swallow a fresh history
        /// copy. On a hit the existing row takes the new metadata, formats and display timestamp AND the next
        /// sort_seq, so re-copying an old clip returns it to the top of every list and makes it <see cref="LatestText"/>.
        /// </summary>
        public bool TryDedupeMoveToTop(ClipboardItem it)
        {
            var hash = ImageHash(it);
            long id;
            using (var find = _conn.CreateCommand())
            {
                find.CommandText = hash != null
                    // image_hash is indexed, so only the one candidate row's blob is compared (A2-06).
                    ? "SELECT id FROM items WHERE kind = 'image' AND tab_id IS NULL AND image_hash = $h AND image = $v"
                    : "SELECT id FROM items WHERE kind = $kind AND tab_id IS NULL AND text = $v";
                if (hash == null) find.Parameters.AddWithValue("$kind", it.Kind);
                if (hash != null) find.Parameters.AddWithValue("$h", hash);
                find.Parameters.AddWithValue("$v", (object)(hash != null ? it.Image : (object)it.Text) ?? DBNull.Value);
                // The newest duplicate by the list's own order. sort_seq is what the list shows.
                find.CommandText += " ORDER BY sort_seq DESC, id DESC LIMIT 1";
                var hit = find.ExecuteScalar();
                if (hit == null && hash != null)
                {
                    // Image stored before image_hash existed: one BLOB scan for that row, which then gets its hash.
                    find.CommandText = "SELECT id FROM items WHERE kind = 'image' AND tab_id IS NULL AND image_hash IS NULL"
                        + " AND image = $v ORDER BY sort_seq DESC, id DESC LIMIT 1";
                    hit = find.ExecuteScalar();
                }
                if (hit == null) return false;
                id = (long)hit;
            }

            using var tx = _conn.BeginTransaction();
            using (var upd = _conn.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = @"
UPDATE items SET captured_at = $at, app_name = $app, window_title = $title, html = $html, rtf = $rtf,
  thumb = COALESCE($thumb, thumb), image_hash = COALESCE($hash, image_hash),
  sort_seq = (SELECT COALESCE(MAX(sort_seq),0)+1 FROM items) WHERE id = $id;
DELETE FROM item_formats WHERE item_id = $id";
                upd.Parameters.AddWithValue("$at", it.CapturedAt);
                upd.Parameters.AddWithValue("$app", (object)it.AppName ?? DBNull.Value);
                upd.Parameters.AddWithValue("$title", (object)it.WindowTitle ?? DBNull.Value);
                upd.Parameters.AddWithValue("$html", (object)it.Html ?? DBNull.Value);
                upd.Parameters.AddWithValue("$rtf", (object)it.Rtf ?? DBNull.Value);
                upd.Parameters.AddWithValue("$thumb", (object)it.Thumb ?? DBNull.Value);
                upd.Parameters.AddWithValue("$hash", (object)hash ?? DBNull.Value);
                upd.Parameters.AddWithValue("$id", id);
                upd.ExecuteNonQuery();
            }
            InsertFormatsCore(id, it.Formats, tx);
            tx.Commit();
            it.Id = id;
            return true;
        }

        /// <summary>
        /// F2 editor: rewrites an item's body in ONE transaction (A2-22). The stored time is bumped for the
        /// display/grouping stamp; list position follows sort_seq (<see cref="Query"/>), so an edited clip keeps
        /// its place. Refuses (false, nothing written) when the item only carries a list preview: its Text is then
        /// the first <see cref="PreviewChars"/> code points, and saving it would destroy the stored body (AUDIT P0).
        /// </summary>
        public bool Update(ClipboardItem it)
        {
            if (it.IsTruncated) return false;
            var now = ClipboardItem.Stamp();
            using var tx = _conn.BeginTransaction();
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            // The old format blobs no longer match the edited body, so drop them rather than paste stale content.
            cmd.CommandText = @"
UPDATE items SET text = $text, html = $html, rtf = $rtf, captured_at = $at WHERE id = $id;
DELETE FROM item_formats WHERE item_id = $id";
            cmd.Parameters.AddWithValue("$text", (object)it.Text ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$html", (object)it.Html ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$rtf", (object)it.Rtf ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$at", now);
            cmd.Parameters.AddWithValue("$id", it.Id);
            cmd.ExecuteNonQuery();
            tx.Commit();
            it.CapturedAt = now;
            it.TextLength = 0; // this object now carries the whole body it just wrote
            return true;
        }

        /// <summary>
        /// The OCR write path: the recognised text becomes an image row's body so search finds the picture.
        /// Deliberately not <see cref="Update"/> — that guard exists to stop a <see cref="PreviewChars"/>-char
        /// PREVIEW from replacing a body, while OCR text is the whole recognised body and must not bump
        /// captured_at or drop the image's format blobs. Only an image row is touched, so a late result can
        /// never overwrite a text clip. Reads derive <see cref="ClipboardItem.TextLength"/> from length(text),
        /// so the row reports the full recognised length, never the preview.
        /// Writes only while the image has no text: once the user edits the row, it is theirs, and a later
        /// OCR pass (a re-copy of the same image enqueues one) must not replace it.
        /// </summary>
        public bool SetOcrText(long id, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE items SET text = $text WHERE id = $id AND kind = 'image' AND text IS NULL";
            cmd.Parameters.AddWithValue("$text", text);
            cmd.Parameters.AddWithValue("$id", id);
            return cmd.ExecuteNonQuery() == 1;
        }

        public List<ClipboardItem> GetRecent(int max) => GetRecent(max, null);

        /// <summary>
        /// How many characters of a body a list row carries. The page's row preview draws far fewer,
        /// so anything past this is never shown. Keep the literal in sync with
        /// ix_list in <see cref="Migrate"/>: SQLite only uses an expression index for an identical expression.
        /// </summary>
        public const int PreviewChars = 1200;

        /// <summary>
        /// Clipboard/Images view = main history only (tab_id IS NULL). kind null = all rows, "text", "image" or "files".
        /// Returns list metadata plus a <see cref="PreviewChars"/>-char preview of the body — never the image,
        /// html, rtf or thumb blobs. Ask <see cref="GetRich"/>, <see cref="GetText"/> or <see cref="GetThumb"/>
        /// for one row (a Word table's CF_HTML is 50-200 KB, and a 10 k-row history would load gigabytes).
        /// Newest first means highest sort_seq, never the wall-clock string: a DST fall-back made a new clip sort
        /// below older ones, and id alone left a re-copied clip in place (AUDIT P1).
        /// </summary>
        public List<ClipboardItem> GetRecent(int max, string kind) => Query(_conn, max, kind, null);

        /// <summary>Container view (Pinned or a custom tab), newest first, same shape as GetRecent. max -1 = no limit.</summary>
        public List<ClipboardItem> GetTabItems(int max, long tabId) => Query(_conn, max, null, tabId);

        /// <summary>
        /// Same rows and order as <see cref="GetRecent(int)"/>, read through a caller-owned connection. The bridge's
        /// reader thread opens one of these per request so ClipboardStore's own connection stays on the UI thread.
        /// </summary>
        internal static List<ClipboardItem> ReadRecent(SqliteConnection conn, int max) => Query(conn, max, null, null);

        /// <summary>Same rows and order as <see cref="GetTabItems"/>, on a caller-owned connection.</summary>
        internal static List<ClipboardItem> ReadTabItems(SqliteConnection conn, int max, long tabId) =>
            Query(conn, max, null, tabId);

        static List<ClipboardItem> Query(SqliteConnection conn, int max, string kind, long? tabId)
        {
            var list = new List<ClipboardItem>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT id, kind, substr(text,1,1200), length(text), uris, app_name, window_title, captured_at, tab_id, size_bytes
FROM items WHERE " + (tabId.HasValue ? "tab_id = $tab" : "tab_id IS NULL")
                + (kind == null ? "" : " AND kind = $kind") + @"
ORDER BY sort_seq DESC, id DESC LIMIT $m";
            if (tabId.HasValue) cmd.Parameters.AddWithValue("$tab", tabId.Value);
            if (kind != null) cmd.Parameters.AddWithValue("$kind", kind);
            cmd.Parameters.AddWithValue("$m", max);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new ClipboardItem
                {
                    Id = r.GetInt64(0),
                    Kind = r.IsDBNull(1) ? null : r.GetString(1),
                    Text = r.IsDBNull(2) ? null : r.GetString(2),
                    TextLength = r.IsDBNull(3) ? 0 : r.GetInt64(3),
                    Uris = r.IsDBNull(4) ? null : r.GetString(4),
                    AppName = r.IsDBNull(5) ? null : r.GetString(5),
                    WindowTitle = r.IsDBNull(6) ? null : r.GetString(6),
                    CapturedAt = r.IsDBNull(7) ? null : r.GetString(7),
                    TabId = r.IsDBNull(8) ? (long?)null : r.GetInt64(8),
                    SizeBytes = r.IsDBNull(9) ? 0 : r.GetInt64(9)
                });
            }
            return list;
        }

        /// <summary>The full body for one row — paste, edit, undo and the row tooltip need all of it.</summary>
        public string GetText(long id)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT text FROM items WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() && !r.IsDBNull(0) ? r.GetString(0) : null;
        }

        /// <summary>One stored 96 px preview — the Images strip decodes only the tiles on screen.</summary>
        public byte[] GetThumb(long id)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT thumb FROM items WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() && !r.IsDBNull(0) ? (byte[])r.GetValue(0) : null;
        }

        /// <summary>Tab-strip counts without loading a single row (ix_kind is index-only).</summary>
        public (int clips, int images) Counts()
        {
            int clips = 0, images = 0;
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT kind, COUNT(*) FROM items WHERE tab_id IS NULL GROUP BY kind";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                string kind = r.IsDBNull(0) ? null : r.GetString(0);
                int n = r.GetInt32(1);
                if (kind == "image") images += n;
                else clips += n;
            }
            return (clips, images);
        }

        /// <summary>
        /// The newest main-history body for the plain-paste hotkey: highest sort_seq, so a DST fall-back or a manual
        /// clock change cannot make it paste an older clip, and re-copying an old clip makes it the latest again.
        /// An image is skipped even though OCR gives it text: plain paste must paste the picture, not its words.
        /// </summary>
        public string LatestText()
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
SELECT text FROM items WHERE tab_id IS NULL AND kind <> 'image'
  AND text IS NOT NULL AND length(trim(text)) > 0
ORDER BY sort_seq DESC, id DESC LIMIT 1";
            return cmd.ExecuteScalar() as string;
        }

        /// <summary>
        /// Same match as the in-memory <see cref="SearchService.Filter"/>, because it runs that same predicate:
        /// SQLite's <c>lower()</c> folds ASCII only, so the comparison is done in C# and must stay one rule.
        /// Full text can be hundreds of MB, so this belongs on a background thread with its OWN connection
        /// and is only needed when a row's body is longer than its preview.
        /// </summary>
        public List<long> SearchFull(string query, int max, string kind, long? tabId)
        {
            var ids = new List<long>();
            using var conn = new SqliteConnection($"Data Source={_path}");
            conn.Open();
            // The C# comparison wins for accented text: SQL lower() folds ASCII only, so CAFÉ was found by
            // one search path and not the other (AUDIT P1). One predicate, no SQL case folding.
            conn.CreateFunction("clip_search_match", (string value, string q) =>
                SearchService.Matches(value, q) ? 1L : 0L);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT id FROM items WHERE " + (tabId.HasValue ? "tab_id = $tab" : "tab_id IS NULL")
                + (kind == null ? "" : " AND kind = $kind") + @"
  AND (clip_search_match(text, $q) = 1 OR clip_search_match(app_name, $q) = 1)
ORDER BY sort_seq DESC, id DESC LIMIT $m";
            if (tabId.HasValue) cmd.Parameters.AddWithValue("$tab", tabId.Value);
            if (kind != null) cmd.Parameters.AddWithValue("$kind", kind);
            cmd.Parameters.AddWithValue("$q", query.Trim());
            cmd.Parameters.AddWithValue("$m", max);
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add(r.GetInt64(0));
            return ids;
        }

        /// <summary>
        /// Per term, the ids of rows whose FULL body carries it: the half a list row's
        /// <see cref="PreviewChars"/> preview cannot answer (a word at character 3,000 is invisible in the
        /// preview). Only a truncated body is scanned, with the same C# comparison as <see cref="SearchFull"/>,
        /// so the caller adds these ids to its own in-memory match without reading long bodies into memory.
        /// </summary>
        internal static List<HashSet<long>> ReadTailMatches(SqliteConnection conn, List<string> terms,
            int max, long? tabId)
        {
            var sets = new List<HashSet<long>>(terms.Count);
            // One registration per connection; every term below reuses it.
            conn.CreateFunction("clip_search_match", (string value, string q) =>
                SearchService.Matches(value, q) ? 1L : 0L);
            foreach (string term in terms)
            {
                var ids = new HashSet<long>();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT id FROM items WHERE " + (tabId.HasValue ? "tab_id = $tab" : "tab_id IS NULL") + @"
  AND length(text) > $preview AND clip_search_match(text, $q) = 1
ORDER BY sort_seq DESC, id DESC LIMIT $m";
                if (tabId.HasValue) cmd.Parameters.AddWithValue("$tab", tabId.Value);
                cmd.Parameters.AddWithValue("$preview", PreviewChars);
                cmd.Parameters.AddWithValue("$q", term);
                cmd.Parameters.AddWithValue("$m", max);
                using var r = cmd.ExecuteReader();
                while (r.Read()) ids.Add(r.GetInt64(0));
                sets.Add(ids);
            }
            return sets;
        }

        /// <summary>Rich bodies for one row, loaded when the card or the paste path actually needs them.</summary>
        public ClipboardItem GetRich(long id)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT html, rtf FROM items WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new ClipboardItem
            {
                Id = id,
                Html = r.IsDBNull(0) ? null : r.GetString(0),
                Rtf = r.IsDBNull(1) ? null : r.GetString(1)
            };
        }

        /// <summary>Full PNG for one item, for Images-tab paste-back.</summary>
        public ClipboardItem GetImage(long id)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT kind, image, tab_id FROM items WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new ClipboardItem
            {
                Id = id,
                Kind = r.IsDBNull(0) ? null : r.GetString(0),
                Image = r.IsDBNull(1) ? null : (byte[])r.GetValue(1),
                TabId = r.IsDBNull(2) ? (long?)null : r.GetInt64(2)
            };
        }

        public void InsertFormats(long itemId, List<(string fmt, byte[] bytes)> formats)
        {
            if (formats == null || formats.Count == 0) return;
            using var tx = _conn.BeginTransaction();
            InsertFormatsCore(itemId, formats, tx);
            tx.Commit();
        }

        void InsertFormatsCore(long itemId, List<(string fmt, byte[] bytes)> formats, SqliteTransaction tx)
        {
            if (formats == null || formats.Count == 0) return;
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO item_formats (item_id, fmt, data) VALUES ($item, $fmt, $data)";
            var pItem = cmd.Parameters.Add("$item", SqliteType.Integer);
            var pFmt = cmd.Parameters.Add("$fmt", SqliteType.Text);
            var pData = cmd.Parameters.Add("$data", SqliteType.Blob);
            pItem.Value = itemId;
            foreach (var (fmt, bytes) in formats)
            {
                if (string.IsNullOrEmpty(fmt) || bytes == null) continue;
                pFmt.Value = fmt;
                pData.Value = bytes;
                cmd.ExecuteNonQuery();
            }
        }

        public List<(string fmt, byte[] bytes)> GetFormats(long itemId)
        {
            var list = new List<(string fmt, byte[] bytes)>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT fmt, data FROM item_formats WHERE item_id = $item ORDER BY id";
            cmd.Parameters.AddWithValue("$item", itemId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), (byte[])r.GetValue(1)));
            return list;
        }

        public void Delete(long id)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM item_formats WHERE item_id = $id; DELETE FROM items WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Reusing a clip (copy/paste from history) makes it the newest: next sort_seq + fresh stamp,
        /// so it returns to the top of every list like a re-copied clip. Returns the new stamp, or null
        /// when the row is gone.
        /// </summary>
        public string Touch(long id)
        {
            var now = ClipboardItem.Stamp();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
UPDATE items SET sort_seq = (SELECT COALESCE(MAX(sort_seq),0)+1 FROM items), captured_at = $at WHERE id = $id";
            cmd.Parameters.AddWithValue("$at", now);
            cmd.Parameters.AddWithValue("$id", id);
            return cmd.ExecuteNonQuery() == 1 ? now : null;
        }

        /// <summary>"Clear History" means main history: Clipboard and Images. Pinned and tab items are kept.</summary>
        public void Clear()
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
DELETE FROM items WHERE tab_id IS NULL;
DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items)";
            cmd.ExecuteNonQuery();
        }

        public void EnforceCap(int max)
        {
            // Called on every capture: a cheap COUNT keeps the delete + orphan sweep off the hot path.
            using (var count = _conn.CreateCommand())
            {
                count.CommandText = "SELECT COUNT(*) FROM items WHERE tab_id IS NULL";
                if (Convert.ToInt32(count.ExecuteScalar()) <= max) return;
            }

            using var cmd = _conn.CreateCommand();
            // Tab items are never evicted. If every row is in a tab the cap is exceeded on purpose; it limits the
            // main-history budget only, so protection never makes a fresh capture fail. "Newest" is sort_seq: a
            // DST fall-back must not evict the clip just copied, and a re-copied clip now counts as newest.
            cmd.CommandText = @"
DELETE FROM items WHERE tab_id IS NULL AND id IN (
  SELECT id FROM items WHERE tab_id IS NULL ORDER BY sort_seq DESC, id DESC LIMIT -1 OFFSET $m);
DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items)";
            cmd.Parameters.AddWithValue("$m", max);
            cmd.ExecuteNonQuery();
        }
    }
}
