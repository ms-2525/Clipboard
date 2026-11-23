using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Clip.Core
{
    public sealed class ClipboardStore : IDisposable
    {
        readonly SqliteConnection _conn;

        /// <summary>Built-in Pinned container. Any item with a tab_id is exempt from cap eviction and Clear.</summary>
        public long PinnedTabId { get; private set; }

        public ClipboardStore(string dbPath)
        {
            _conn = new SqliteConnection($"Data Source={dbPath}");
            _conn.Open();

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS items (
  id INTEGER PRIMARY KEY AUTOINCREMENT, kind TEXT NOT NULL, text TEXT, html TEXT,
  rtf TEXT, image BLOB, thumb BLOB, uris TEXT, app_name TEXT, window_title TEXT,
  captured_at TEXT NOT NULL, pinned INTEGER NOT NULL DEFAULT 0, size_bytes INTEGER);
CREATE INDEX IF NOT EXISTS ix_captured ON items(captured_at DESC);
CREATE TABLE IF NOT EXISTS item_formats (
  id INTEGER PRIMARY KEY AUTOINCREMENT, item_id INTEGER NOT NULL, fmt TEXT NOT NULL, data BLOB NOT NULL);
CREATE INDEX IF NOT EXISTS ix_fmt_item ON item_formats(item_id);
CREATE TABLE IF NOT EXISTS tabs (
  id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL COLLATE NOCASE UNIQUE,
  kind TEXT NOT NULL, sort_order INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL);";
            cmd.ExecuteNonQuery();

            Migrate();
        }

        /// <summary>Schema upgrades for databases created by older builds. Runs on every open; every step is idempotent.</summary>
        void Migrate()
        {
            try
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = "ALTER TABLE items ADD COLUMN rtf TEXT";
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                // Already migrated.
            }

            try
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = "ALTER TABLE items ADD COLUMN tab_id INTEGER";
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                // Already migrated.
            }

            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE INDEX IF NOT EXISTS ix_items_tab ON items(tab_id);
INSERT INTO tabs (name, kind, sort_order, created_at)
SELECT 'Pinned', 'builtin', 0, strftime('%Y-%m-%dT%H:%M:%S','now','localtime')
WHERE NOT EXISTS (SELECT 1 FROM tabs WHERE name = 'Pinned');
UPDATE items SET tab_id = (SELECT id FROM tabs WHERE name = 'Pinned') WHERE pinned = 1 AND tab_id IS NULL;
DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items)";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id FROM tabs WHERE name = 'Pinned' LIMIT 1";
                var id = cmd.ExecuteScalar();
                if (id == null) throw new InvalidOperationException("Clip database is missing the built-in Pinned tab.");
                PinnedTabId = (long)id;
            }
        }

        public void Insert(ClipboardItem it)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO items (kind, text, html, rtf, image, thumb, uris, app_name, window_title, captured_at, size_bytes)
VALUES ($kind, $text, $html, $rtf, $image, $thumb, $uris, $app, $title, $at, $size)";
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
            cmd.ExecuteNonQuery();

            using var id = _conn.CreateCommand();
            id.CommandText = "SELECT last_insert_rowid()";
            it.Id = (long)id.ExecuteScalar();
        }

        /// <summary>
        /// Exact-match dedupe against main history only: byte-for-byte (case- and space-sensitive; SQLite BINARY
        /// collation, no C# normalisation). A copy in a tab is never matched, so it cannot swallow a fresh history
        /// copy. On a hit the existing row takes the new metadata, formats and timestamp and jumps to the top.
        /// </summary>
        public bool TryDedupeMoveToTop(ClipboardItem it)
        {
            long id;
            using (var find = _conn.CreateCommand())
            {
                // BLOB comparison for images keeps the full PNG out of memory here (GetRecent never loads it).
                find.CommandText = (it.Kind == "image"
                    ? "SELECT id FROM items WHERE kind = 'image' AND tab_id IS NULL AND image = $v"
                    : "SELECT id FROM items WHERE kind = $kind AND tab_id IS NULL AND text = $v")
                    + " ORDER BY captured_at DESC, id DESC LIMIT 1";
                if (it.Kind != "image") find.Parameters.AddWithValue("$kind", it.Kind);
                find.Parameters.AddWithValue("$v", (object)(it.Kind == "image" ? it.Image : (object)it.Text) ?? DBNull.Value);
                var hit = find.ExecuteScalar();
                if (hit == null) return false;
                id = (long)hit;
            }

            using (var upd = _conn.CreateCommand())
            {
                upd.CommandText = @"
UPDATE items SET captured_at = $at, app_name = $app, window_title = $title, html = $html, rtf = $rtf,
  thumb = COALESCE($thumb, thumb) WHERE id = $id;
DELETE FROM item_formats WHERE item_id = $id";
                upd.Parameters.AddWithValue("$at", it.CapturedAt);
                upd.Parameters.AddWithValue("$app", (object)it.AppName ?? DBNull.Value);
                upd.Parameters.AddWithValue("$title", (object)it.WindowTitle ?? DBNull.Value);
                upd.Parameters.AddWithValue("$html", (object)it.Html ?? DBNull.Value);
                upd.Parameters.AddWithValue("$rtf", (object)it.Rtf ?? DBNull.Value);
                upd.Parameters.AddWithValue("$thumb", (object)it.Thumb ?? DBNull.Value);
                upd.Parameters.AddWithValue("$id", id);
                upd.ExecuteNonQuery();
            }

            InsertFormats(id, it.Formats);
            it.Id = id;
            return true;
        }

        /// <summary>F2 editor: rewrites an item's body and moves it to the top of the list.</summary>
        public void Update(ClipboardItem it)
        {
            var now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            using var cmd = _conn.CreateCommand();
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
            it.CapturedAt = now;
        }

        public List<ClipboardItem> GetRecent(int max) => GetRecent(max, null);

        /// <summary>
        /// Clipboard/Images view = main history only (tab_id IS NULL). kind null = all rows, "text", "image" or "files".
        /// The image blob itself is never loaded here.
        /// </summary>
        public List<ClipboardItem> GetRecent(int max, string kind) => Query(max, kind, null);

        /// <summary>Container view (Pinned or a custom tab), newest first, same shape as GetRecent. max -1 = no limit.</summary>
        public List<ClipboardItem> GetTabItems(int max, long tabId) => Query(max, null, tabId);

        List<ClipboardItem> Query(int max, string kind, long? tabId)
        {
            var list = new List<ClipboardItem>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
SELECT id, kind, text, html, rtf, thumb, app_name, window_title, captured_at, uris
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
                    Html = r.IsDBNull(3) ? null : r.GetString(3),
                    Rtf = r.IsDBNull(4) ? null : r.GetString(4),
                    Thumb = r.IsDBNull(5) ? null : (byte[])r.GetValue(5),
                    AppName = r.IsDBNull(6) ? null : r.GetString(6),
                    WindowTitle = r.IsDBNull(7) ? null : r.GetString(7),
                    CapturedAt = r.IsDBNull(8) ? null : r.GetString(8),
                    Uris = r.IsDBNull(9) ? null : r.GetString(9)
                });
            }
            return list;
        }

        /// <summary>Full PNG for one item, for Images-tab paste-back.</summary>
        public ClipboardItem GetImage(long id)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT kind, image FROM items WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new ClipboardItem
            {
                Id = id,
                Kind = r.IsDBNull(0) ? null : r.GetString(0),
                Image = r.IsDBNull(1) ? null : (byte[])r.GetValue(1)
            };
        }

        /// <summary>Every format blob captured with one item, for full-fidelity paste-back.</summary>
        public void InsertFormats(long itemId, List<(string fmt, byte[] bytes)> formats)
        {
            if (formats == null || formats.Count == 0) return;
            using var tx = _conn.BeginTransaction();
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
            tx.Commit();
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

        /// <summary>All tabs in bar order (Pinned first), with the number of items currently in each.</summary>
        public List<(long id, string name, string kind, int count)> ListTabs()
        {
            var list = new List<(long id, string name, string kind, int count)>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
SELECT t.id, t.name, t.kind, (SELECT COUNT(*) FROM items i WHERE i.tab_id = t.id)
FROM tabs t ORDER BY t.sort_order, t.id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3)));
            return list;
        }

        /// <summary>Creates a custom tab. Throws ArgumentException with a UI-ready message on a blank or duplicate name.</summary>
        public long CreateTab(string name)
        {
            name = CleanTabName(name);
            if (TabNameTaken(name, 0)) throw new ArgumentException("A tab with that name already exists.");

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO tabs (name, kind, sort_order, created_at)
VALUES ($name, 'custom', (SELECT COALESCE(MAX(sort_order), 0) + 1 FROM tabs), $at);
SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$at", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
            return (long)cmd.ExecuteScalar();
        }

        /// <summary>Renames a custom tab. false for the built-in Pinned tab, unknown ids and duplicate names.</summary>
        public bool RenameTab(long id, string name)
        {
            name = CleanTabName(name);
            if (id == PinnedTabId || TabNameTaken(name, id)) return false;

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE tabs SET name = $name WHERE id = $id AND kind = 'custom'";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// Deletes a custom tab and purges its items and their formats, returning the purge count for the
        /// confirmation. -1 for the built-in Pinned tab (never deletable) or an unknown id.
        /// </summary>
        public int DeleteTab(long id)
        {
            if (id == PinnedTabId) return -1;
            using (var kind = _conn.CreateCommand())
            {
                kind.CommandText = "SELECT kind FROM tabs WHERE id = $id";
                kind.Parameters.AddWithValue("$id", id);
                if (!(kind.ExecuteScalar() is string k) || k != "custom") return -1;
            }

            using var tx = _conn.BeginTransaction();
            int count;
            using (var countCmd = _conn.CreateCommand())
            {
                countCmd.Transaction = tx;
                countCmd.CommandText = "SELECT COUNT(*) FROM items WHERE tab_id = $id";
                countCmd.Parameters.AddWithValue("$id", id);
                count = Convert.ToInt32(countCmd.ExecuteScalar());
            }

            using (var cmd = _conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
DELETE FROM item_formats WHERE item_id IN (SELECT id FROM items WHERE tab_id = $id);
DELETE FROM items WHERE tab_id = $id;
DELETE FROM tabs WHERE id = $id;
DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items)";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return count;
        }

        /// <summary>
        /// Moves an item into a tab, or back to main history when tabId is null. No dedupe and no timestamp
        /// rewrite (shortcut: pinning two identical clips keeps two rows; tabs are curated, add dedupe on move if
        /// users complain). A move to the tab it already sits in is a no-op.
        /// </summary>
        public void MoveItemToTab(long itemId, long? tabId)
        {
            if (tabId.HasValue && !TabExists(tabId.Value)) return;

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE items SET tab_id = $tab WHERE id = $id AND tab_id IS NOT $tab";
            cmd.Parameters.AddWithValue("$tab", (object)tabId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$id", itemId);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Ctrl+P: an item in Pinned goes back to main history; anything else goes into Pinned.</summary>
        public void TogglePin(long itemId)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE items SET tab_id = CASE WHEN tab_id = $pinned THEN NULL ELSE $pinned END WHERE id = $id";
            cmd.Parameters.AddWithValue("$pinned", PinnedTabId);
            cmd.Parameters.AddWithValue("$id", itemId);
            cmd.ExecuteNonQuery();
        }

        static string CleanTabName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) throw new ArgumentException("Enter a tab name.");
            if (name.Length > 24) throw new ArgumentException("Tab names are 24 characters at most.");
            foreach (var c in name)
                if (char.IsControl(c)) throw new ArgumentException("Tab names cannot contain control characters.");
            return name;
        }

        bool TabNameTaken(string name, long exceptId)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM tabs WHERE name = $name AND id <> $id LIMIT 1";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$id", exceptId);
            return cmd.ExecuteScalar() != null;
        }

        bool TabExists(long id)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM tabs WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            return cmd.ExecuteScalar() != null;
        }

        public void Dispose() => _conn.Dispose();
    }
}
