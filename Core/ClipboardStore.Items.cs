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
INSERT INTO items (kind, text, html, rtf, image, thumb, uris, app_name, window_title, captured_at, size_bytes, image_hash)
VALUES ($kind, $text, $html, $rtf, $image, $thumb, $uris, $app, $title, $at, $size, $hash)";
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
                ? Convert.ToHexString(SHA256.HashData(it.Image))
                : null;

        /// <summary>
        /// Undo primitive for a deleted row: same id, tab, formats and captured_at, re-inserted in one transaction.
        /// No dedupe (an explicit user action) and no eviction — the cap re-applies on the next capture.
        /// The retired `pinned` flag is not restored; tab_id carries the pin state.
        /// </summary>
        public long Restore(ClipboardItem it)
        {
            using var tx = _conn.BeginTransaction();
            using (var cmd = _conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO items (id, kind, text, html, rtf, image, thumb, uris, app_name, window_title, captured_at, size_bytes, tab_id, image_hash)
VALUES ($id, $kind, $text, $html, $rtf, $image, $thumb, $uris, $app, $title, $at, $size, $tab, $hash)";
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
        /// copy. On a hit the existing row takes the new metadata, formats and timestamp and jumps to the top.
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
                find.CommandText += " ORDER BY captured_at DESC, id DESC LIMIT 1";
                var hit = find.ExecuteScalar();
                if (hit == null && hash != null)
                {
                    // Image stored before image_hash existed: one BLOB scan for that row, which then gets its hash.
                    find.CommandText = "SELECT id FROM items WHERE kind = 'image' AND tab_id IS NULL AND image_hash IS NULL"
                        + " AND image = $v ORDER BY captured_at DESC, id DESC LIMIT 1";
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
  thumb = COALESCE($thumb, thumb), image_hash = COALESCE($hash, image_hash) WHERE id = $id;
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

        /// <summary>F2 editor: rewrites an item's body and moves it to the top of the list, in ONE transaction (A2-22).</summary>
        public void Update(ClipboardItem it)
        {
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
        }

        public List<ClipboardItem> GetRecent(int max) => GetRecent(max, null);

        /// <summary>
        /// Clipboard/Images view = main history only (tab_id IS NULL). kind null = all rows, "text", "image" or "files".
        /// The image blob and the html/rtf bodies are never loaded here; ask <see cref="GetRich"/> or
        /// <see cref="GetImage"/> for one row (a Word table's CF_HTML is 50-200 KB per row).
        /// </summary>
        public List<ClipboardItem> GetRecent(int max, string kind) => Query(max, kind, null);

        /// <summary>Container view (Pinned or a custom tab), newest first, same shape as GetRecent. max -1 = no limit.</summary>
        public List<ClipboardItem> GetTabItems(int max, long tabId) => Query(max, null, tabId);

        List<ClipboardItem> Query(int max, string kind, long? tabId)
        {
            var list = new List<ClipboardItem>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
SELECT id, kind, text, thumb, app_name, window_title, captured_at, uris, tab_id
FROM items WHERE " + (tabId.HasValue ? "tab_id = $tab" : "tab_id IS NULL")
                + (kind == null ? "" : " AND kind = $kind") + @"
ORDER BY captured_at DESC, id DESC LIMIT $m";
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
                    Thumb = r.IsDBNull(3) ? null : (byte[])r.GetValue(3),
                    AppName = r.IsDBNull(4) ? null : r.GetString(4),
                    WindowTitle = r.IsDBNull(5) ? null : r.GetString(5),
                    CapturedAt = r.IsDBNull(6) ? null : r.GetString(6),
                    Uris = r.IsDBNull(7) ? null : r.GetString(7),
                    TabId = r.IsDBNull(8) ? (long?)null : r.GetInt64(8)
                });
            }
            return list;
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
            // main-history budget only, so protection never makes a fresh capture fail.
            cmd.CommandText = @"
DELETE FROM items WHERE tab_id IS NULL AND id IN (
  SELECT id FROM items WHERE tab_id IS NULL ORDER BY captured_at DESC, id DESC LIMIT -1 OFFSET $m);
DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items)";
            cmd.Parameters.AddWithValue("$m", max);
            cmd.ExecuteNonQuery();
        }
    }
}
