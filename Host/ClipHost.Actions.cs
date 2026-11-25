using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using Clip.Core;
using Clip.Interop;

namespace Clip.Host
{
    /// <summary>
    /// The page's clipboard actions: paste-back, delete/restore, pin and tab moves. Paste reuses the old app's
    /// exact ladder (<see cref="ClipboardFormats.Restore"/>, image restore, rich fallback, plain text only), and
    /// every command that touches the store runs on the UI thread, where ClipboardStore's connection lives.
    /// </summary>
    internal sealed partial class ClipHost
    {
        /// <summary>One-level session undo buffer per id: restore needs the full row the delete removed.</summary>
        readonly Dictionary<long, ClipboardItem> _deleted = new Dictionary<long, ClipboardItem>();

        /// <summary>
        /// Set by the v2 app: any clipboard write Clip performs (copy, paste) is its own, not a new capture.
        /// Without it a write re-enters history and fires a toast for a clip the user already had.
        /// </summary>
        internal static Action<uint> SuppressCapture;

        /// <summary>Ctrl+Shift+V armed the next paste as plain text; the flag is consumed by that paste only.</summary>
        bool _plainNext;

        /// <summary>The page also hears this as "plainMode" so it can show the mode in its own UI.</summary>
        public void SetPlainMode(bool on)
        {
            _plainNext = on;
            Emit("plainMode", new { on });
        }

        // ---- delete / restore -------------------------------------------------

        object Delete(JsonElement p)
        {
            long id = Long(p, "id");
            var item = ReadFullItem(_dbPath, id);
            if (item == null) throw new ArgumentException("no clip with id " + id);

            _store.Delete(id);
            _deleted[id] = item;
            // Queued so the reply lands first and the page has already dropped the row when the event arrives.
            OnUi(() => Emit("deleted", new { id }));
            return new { deleted = true };
        }

        object Restore(JsonElement p)
        {
            long id = Long(p, "id");
            // A deleted row is gone from the DB: only the session snapshot can bring it back.
            if (!_deleted.TryGetValue(id, out var item))
                throw new InvalidOperationException("no deleted clip with id " + id + " in this session");
            _deleted.Remove(id);

            _store.Restore(item);
            var dto = ReadDto(_dbPath, id, _pinnedTabId);
            if (dto == null) throw new InvalidOperationException("the restored clip could not be read back");
            return dto;
        }

        // ---- pin / move -------------------------------------------------------

        object TogglePin(JsonElement p)
        {
            long id = Long(p, "id");
            var (exists, _) = RowState(_dbPath, id);
            if (!exists) throw new ArgumentException("no clip with id " + id);

            _store.TogglePin(id);
            var (_, tabId) = RowState(_dbPath, id);
            return new { pinned = tabId == _pinnedTabId };
        }

        object MoveToTab(JsonElement p)
        {
            long id = Long(p, "id");
            long? tabId = Tab(p);
            var (exists, _) = RowState(_dbPath, id);
            if (!exists) throw new ArgumentException("no clip with id " + id);
            if (tabId.HasValue && !_store.ListTabs().Any(t => t.id == tabId.Value))
                throw new ArgumentException("no tab with id " + tabId);

            _store.MoveItemToTab(id, tabId); // null goes back to main history
            return new { moved = true };
        }

        // ---- paste ------------------------------------------------------------

        /// <summary>
        /// Hides the shell, waits for the window that had focus before Clip to own it again, then restores the
        /// clip and injects Ctrl+V. The wait is a DispatcherTimer, so the page keeps painting while it runs.
        /// </summary>
        void BeginPaste(string id, JsonElement p)
        {
            ClipboardItem item;
            bool plain;
            try
            {
                long clipId = Long(p, "id");
                plain = Bool(p, "plain") || _plainNext;
                _plainNext = false; // one paste only: the next open is a normal one
                item = ReadFullItem(_dbPath, clipId);
                if (item == null) throw new ArgumentException("no clip with id " + clipId);
            }
            catch (Exception ex)
            {
                Reply(Ok(id, new { pasted = false, error = ex.Message }));
                return;
            }

            IntPtr target = _returnTarget();
            if (target == IntPtr.Zero || !IsWindow(target))
            {
                Reply(Ok(id, new { pasted = false, error = "No target app to paste into. Open Clip over the app first." }));
                return;
            }

            if (BlockedByElevation(target))
            {
                _show(); // the shell is still open; bring it back rather than leaving the user in a dead app
                Reply(Ok(id, new { pasted = false, error = ElevatedPasteError }));
                return;
            }

            _hide();
            int tries = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (s, e) =>
            {
                if (Foreground(target))
                {
                    timer.Stop();
                    PasteNow(id, item, plain);
                    return;
                }
                if (++tries >= 3)
                {
                    timer.Stop();
                    _show();
                    Reply(Ok(id, new { pasted = false, error = FocusError(target) }));
                }
            };
            timer.Start();
        }

        /// <summary>The clipboard write that injects the paste; the old app's one call, no reimplementation.</summary>
        void PasteNow(string id, ClipboardItem item, bool plain)
        {
            try
            {
                uint seq = plain ? PasteInjector.CopyAndPastePlain(item) : PasteInjector.CopyAndPaste(item);
                SuppressCapture?.Invoke(seq);
                Reply(Ok(id, new { pasted = true }));
            }
            catch (Exception)
            {
                _show(); // the page's toast is invisible while the shell is hidden
                // The write runs before the chord, so a throw means nothing was injected: never claim a paste.
                Reply(Ok(id, new { pasted = false, error = PasteInjector.ClipboardWriteError }));
            }
        }

        // ---- elevation guard --------------------------------------------------

        const string ElevatedPasteError =
            "That app is running as administrator \u2014 Windows blocks paste into it. Copy the clip instead.";

        const uint ProcessQueryLimitedInformation = 0x1000;
        const uint TokenQuery = 0x0008;
        const int TokenElevation = 20;

        static readonly bool SelfElevated = IsElevated((uint)Environment.ProcessId);

        /// <summary>
        /// UIPI silently drops the injected Ctrl+V when the target runs at a higher integrity level than Clip:
        /// SendInput succeeds, so paste used to claim success and nothing landed. Compare token elevation first.
        /// </summary>
        static bool BlockedByElevation(IntPtr target)
        {
            if (SelfElevated) return false;
            GetWindowThreadProcessId(target, out uint pid);
            return pid != 0 && IsElevated(pid);
        }

        /// <summary>
        /// Token elevation for a process. A process we cannot even open or query is treated as elevated:
        /// the same access rules that hide it from us also block an injected chord into its windows.
        /// </summary>
        static bool IsElevated(uint pid)
        {
            IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (process == IntPtr.Zero) return true;
            try
            {
                if (!OpenProcessToken(process, TokenQuery, out IntPtr token)) return true;
                try
                {
                    if (!GetTokenInformation(token, TokenElevation, out int elevated, sizeof(int), out _)) return true;
                    return elevated != 0;
                }
                finally { CloseHandle(token); }
            }
            finally { CloseHandle(process); }
        }

        // ---- captured event (plumbing for a future --v2 capture service) ------

        /// <summary>
        /// The page's live "captured" feed. --v2 starts no CaptureService today, so nothing raises this; a host
        /// that does run one calls it from its Captured handler and the DTO is read back exactly like a list row.
        /// </summary>
        public void EmitCaptured(ClipboardItem item)
        {
            if (item == null) return;
            OnUi(() =>
            {
                var dto = ReadDto(_dbPath, item.Id, _pinnedTabId);
                if (dto != null) Emit("captured", dto);
            });
        }

        void Emit(string name, object data) =>
            Reply("{\"event\":" + JsonSerializer.Serialize(name, Json) + ",\"data\":" + JsonSerializer.Serialize(data, Json) + "}");

        // ---- focus guard ------------------------------------------------------

        /// <summary>True when the target owns the foreground (one attach attempt; see ForegroundApp.Force).</summary>
        static bool Foreground(IntPtr target) => ForegroundApp.Force(target);

        static string FocusError(IntPtr target)
        {
            var sb = new StringBuilder(256);
            GetWindowText(target, sb, sb.Capacity);
            return sb.Length == 0
                ? "The previous app did not take focus back, so nothing was pasted."
                : "Focus did not return to \"" + sb + "\", so nothing was pasted.";
        }

        // ---- one-row reads (UI thread; same shape as the list query) ----------

        /// <summary>The whole row plus format blobs, for the delete undo snapshot and the paste payload.</summary>
        internal static ClipboardItem ReadFullItem(string dbPath, long id)
        {
            using var conn = OpenRead(dbPath);
            ClipboardItem item = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT id, kind, text, html, rtf, image, thumb, uris, app_name, window_title, captured_at, size_bytes, tab_id
FROM items WHERE id = $id";
                cmd.Parameters.AddWithValue("$id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    item = new ClipboardItem
                    {
                        Id = r.GetInt64(0),
                        Kind = r.IsDBNull(1) ? null : r.GetString(1),
                        Text = r.IsDBNull(2) ? null : r.GetString(2), // full body, never the preview
                        Html = r.IsDBNull(3) ? null : r.GetString(3),
                        Rtf = r.IsDBNull(4) ? null : r.GetString(4),
                        Image = r.IsDBNull(5) ? null : (byte[])r.GetValue(5),
                        Thumb = r.IsDBNull(6) ? null : (byte[])r.GetValue(6),
                        Uris = r.IsDBNull(7) ? null : r.GetString(7),
                        AppName = r.IsDBNull(8) ? null : r.GetString(8),
                        WindowTitle = r.IsDBNull(9) ? null : r.GetString(9),
                        CapturedAt = r.IsDBNull(10) ? null : r.GetString(10),
                        SizeBytes = r.IsDBNull(11) ? 0 : r.GetInt64(11),
                        TabId = r.IsDBNull(12) ? (long?)null : r.GetInt64(12)
                    };
                }
            }
            if (item == null) return null;

            var formats = new List<(string fmt, byte[] bytes)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT fmt, data FROM item_formats WHERE item_id = $id ORDER BY id";
                cmd.Parameters.AddWithValue("$id", id);
                using var r = cmd.ExecuteReader();
                while (r.Read()) formats.Add((r.GetString(0), (byte[])r.GetValue(1)));
            }
            item.Formats = formats;
            return item;
        }

        /// <summary>One row as the list DTO, read after a mutation so the page can splice it in.</summary>
        internal static ItemDto ReadDto(string dbPath, long id, long pinnedTabId)
        {
            using var conn = OpenRead(dbPath);
            ClipboardItem row = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT id, kind, substr(text,1,1200), length(text), uris, app_name, window_title, captured_at, tab_id
FROM items WHERE id = $id";
                cmd.Parameters.AddWithValue("$id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    row = new ClipboardItem
                    {
                        Id = r.GetInt64(0),
                        Kind = r.IsDBNull(1) ? null : r.GetString(1),
                        Text = r.IsDBNull(2) ? null : r.GetString(2),
                        TextLength = r.IsDBNull(3) ? 0 : r.GetInt64(3),
                        Uris = r.IsDBNull(4) ? null : r.GetString(4),
                        AppName = r.IsDBNull(5) ? null : r.GetString(5),
                        WindowTitle = r.IsDBNull(6) ? null : r.GetString(6),
                        CapturedAt = r.IsDBNull(7) ? null : r.GetString(7),
                        TabId = r.IsDBNull(8) ? (long?)null : r.GetInt64(8)
                    };
                }
            }
            if (row == null) return null;

            var meta = ReadMeta(conn, new List<long> { id });
            meta.TryGetValue(id, out var info);
            return ToDto(row, info, ClipTags.Classify(row, info?.Formats), pinnedTabId);
        }

        /// <summary>Existence + current tab, without loading blobs (pin/move validation).</summary>
        static (bool exists, long? tabId) RowState(string dbPath, long id)
        {
            using var conn = OpenRead(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT tab_id FROM items WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return (false, null);
            return (true, r.IsDBNull(0) ? (long?)null : r.GetInt64(0));
        }

        // ---- Win32 ------------------------------------------------------------

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetTokenInformation(IntPtr token, int informationClass, out int info, int length, out int returned);
    }
}
