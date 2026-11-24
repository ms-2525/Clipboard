using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Clip.Core
{
    /// <summary>
    /// SQLite persistence for items / tabs / item_formats. This file owns the connection, schema, migrations and
    /// the PRAGMAs; the item and tab queries live in the two partial files next to it.
    /// </summary>
    public sealed partial class ClipboardStore : IDisposable
    {
        readonly SqliteConnection _conn;
        readonly string _path;

        /// <summary>Built-in Pinned container. Any item with a tab_id is exempt from cap eviction and Clear.</summary>
        public long PinnedTabId { get; private set; }

        /// <summary>
        /// True when the old database could not be read and was moved to clip.db.corrupt-&lt;timestamp&gt; instead of
        /// killing startup. History is a cache; the App may toast the loss.
        /// </summary>
        public bool RecoveredFromCorruption { get; private set; }

        public ClipboardStore(string dbPath)
        {
            _path = dbPath;
            SqliteConnection conn = null;
            try
            {
                conn = Open(dbPath);
                Migrate(conn);
            }
            catch (SqliteException ex) when (IsCorruption(ex))
            {
                // Never start with no app: move the unreadable file aside and carry on with a fresh one (A2-05).
                conn?.Dispose();
                SqliteConnection.ClearAllPools();
                MoveAside(dbPath);
                RecoveredFromCorruption = true;
                conn = Open(dbPath);
                Migrate(conn);
            }
            _conn = conn;
        }

        static SqliteConnection Open(string dbPath)
        {
            var conn = new SqliteConnection($"Data Source={dbPath}");
            try
            {
                conn.Open();
                // WAL + NORMAL: 100 autocommit inserts measured 444-465 ms before, 10-15 ms after.
                using (var pragma = conn.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                    pragma.ExecuteNonQuery();
                }
                CreateSchema(conn);
                return conn;
            }
            catch
            {
                conn.Dispose(); // close the half-open handle before the caller sees the failure
                throw;
            }
        }

        static void CreateSchema(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS items (
  id INTEGER PRIMARY KEY AUTOINCREMENT, kind TEXT NOT NULL, text TEXT, html TEXT,
  rtf TEXT, image BLOB, thumb BLOB, uris TEXT, app_name TEXT, window_title TEXT,
  captured_at TEXT NOT NULL, pinned INTEGER NOT NULL DEFAULT 0, size_bytes INTEGER,
  tab_id INTEGER, image_hash TEXT);
CREATE INDEX IF NOT EXISTS ix_captured ON items(captured_at DESC);
CREATE TABLE IF NOT EXISTS item_formats (
  id INTEGER PRIMARY KEY AUTOINCREMENT, item_id INTEGER NOT NULL, fmt TEXT NOT NULL, data BLOB NOT NULL);
CREATE INDEX IF NOT EXISTS ix_fmt_item ON item_formats(item_id);
CREATE TABLE IF NOT EXISTS tabs (
  id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL COLLATE NOCASE UNIQUE,
  kind TEXT NOT NULL, sort_order INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL);";
            cmd.ExecuteNonQuery();
        }

        /// <summary>Schema upgrades for databases created by older builds. Runs on every open; every step is idempotent.</summary>
        void Migrate(SqliteConnection conn)
        {
            AddColumnIfMissing(conn, "rtf", "TEXT");
            AddColumnIfMissing(conn, "tab_id", "INTEGER");
            AddColumnIfMissing(conn, "image_hash", "TEXT");

            using (var cmd = conn.CreateCommand())
            {
                // pinned is retired: tab_id is the only pin state, so an item the user unpins stays unpinned on the
                // next start instead of being re-adopted (A2-04). Adopt first, then clear the dead flag everywhere.
                cmd.CommandText = @"
CREATE INDEX IF NOT EXISTS ix_items_tab ON items(tab_id);
CREATE INDEX IF NOT EXISTS ix_image_hash ON items(image_hash);
-- One covering index answers the whole list (metadata + preview + body length), so opening the
-- popup or switching tabs never reads a payload blob. Building it is the one-time cost of the
-- first start after this upgrade: ~4 s on a 10 000 × 1000-line history, milliseconds on a normal one.
-- The 1200 literal must stay identical to ClipboardStore.PreviewChars and the substr() in Query.
CREATE INDEX IF NOT EXISTS ix_list ON items(tab_id, captured_at DESC, id DESC, kind, app_name,
  window_title, uris, substr(text,1,1200), length(text));
-- Counts the tab strip needs, narrow enough to stay an index-only scan.
CREATE INDEX IF NOT EXISTS ix_kind ON items(tab_id, kind);
INSERT INTO tabs (name, kind, sort_order, created_at)
SELECT 'Pinned', 'builtin', 0, strftime('%Y-%m-%dT%H:%M:%S','now','localtime')
WHERE NOT EXISTS (SELECT 1 FROM tabs WHERE name = 'Pinned');
UPDATE items SET tab_id = (SELECT id FROM tabs WHERE name = 'Pinned') WHERE pinned = 1 AND tab_id IS NULL;
UPDATE items SET pinned = 0 WHERE pinned = 1;
DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items)";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id FROM tabs WHERE name = 'Pinned' LIMIT 1";
                var id = cmd.ExecuteScalar();
                if (id == null) throw new InvalidOperationException("Clip database is missing the built-in Pinned tab.");
                PinnedTabId = (long)id;
            }
        }

        /// <summary>A column ADD guarded by the real schema, not by matching an English error message (A2-13).</summary>
        static void AddColumnIfMissing(SqliteConnection conn, string name, string type)
        {
            using (var info = conn.CreateCommand())
            {
                info.CommandText = "SELECT 1 FROM pragma_table_info('items') WHERE name = $n";
                info.Parameters.AddWithValue("$n", name);
                if (info.ExecuteScalar() != null) return;
            }
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE items ADD COLUMN {name} {type}"; // name/type are compile-time constants
            cmd.ExecuteNonQuery();
        }

        // Only a genuinely unreadable file is moved aside; a busy or locked one is somebody else's session and is
        // rethrown, never renamed (renaming it would take the history away from a healthy running instance).
        static bool IsCorruption(SqliteException ex) =>
            ex.SqliteErrorCode == 11 || ex.SqliteErrorCode == 26; // SQLITE_CORRUPT, SQLITE_NOTADB

        void MoveAside(string dbPath)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var from = dbPath + suffix;
                if (File.Exists(from)) File.Move(from, $"{dbPath}.corrupt-{stamp}{suffix}", overwrite: true);
            }
        }

        public void Dispose() => _conn.Dispose();
    }
}
