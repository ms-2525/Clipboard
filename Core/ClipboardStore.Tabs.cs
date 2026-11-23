using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Clip.Core
{
    public sealed partial class ClipboardStore
    {
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
    }
}
