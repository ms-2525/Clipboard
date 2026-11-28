using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using Clip.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Clip.Host
{
    /// <summary>
    /// The typed page&lt;-&gt;host bridge. The page sends {id, method, params}; every reply is
    /// {id, ok:true, result} or {id, ok:false, error}. Protocol, clipboard commands and the store writer stay
    /// on the UI thread; list reads run on <see cref="ReadQueue"/> and only their finished replies come back.
    /// </summary>
    internal sealed partial class ClipHost : IDisposable
    {
        static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        readonly ClipboardStore _store;
        readonly Config _cfg;
        readonly WebView2 _web;
        readonly Dispatcher _dispatcher;
        readonly string _dbPath;
        readonly long _pinnedTabId;
        readonly ReadQueue _reads;
        readonly Func<IntPtr> _returnTarget;
        readonly Action _hide;
        readonly Action _show;
        readonly Action _previewToast;
        readonly ModalGate _modals;

        /// <summary>
        /// <paramref name="returnTarget"/> is the last window that owned the foreground before the shell;
        /// <paramref name="hide"/> and <paramref name="show"/> keep the window itself out of this layer, and
        /// <paramref name="modals"/> is the gate the shell's auto-hide checks while a dialog is open.
        /// <paramref name="previewToast"/> shows the real notification at its real coordinates from Settings.
        /// </summary>
        public ClipHost(WebView2 web, ClipboardStore store, Config cfg, string dbPath,
            Func<IntPtr> returnTarget, Action hide, Action show, ModalGate modals, Action previewToast = null)
        {
            _web = web;
            _store = store;
            _cfg = cfg;
            _dbPath = dbPath;
            _returnTarget = returnTarget;
            _hide = hide;
            _show = show;
            _modals = modals;
            _previewToast = previewToast;
            _dispatcher = web.Dispatcher;
            _pinnedTabId = store.PinnedTabId;
            _reads = new ReadQueue();
            LoadSettings();
            _web.CoreWebView2.WebMessageReceived += OnMessage;
        }

        // ---- dispatch -------------------------------------------------------------------------

        void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string raw = e.WebMessageAsJson;
            // WebView2 raises this on the UI thread already; Invoke keeps the store's thread rule explicit
            // and turns a future off-thread callback into a queued one instead of a crash.
            _dispatcher.Invoke(() => Dispatch(raw));
        }

        void Dispatch(string raw)
        {
            string id = "null";
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;
                if (root.TryGetProperty("id", out var idElement)) id = idElement.GetRawText();
                if (!root.TryGetProperty("method", out var methodElement)) throw new ArgumentException("method required");
                string method = methodElement.GetString();
                var p = root.TryGetProperty("params", out var paramsElement) ? paramsElement : default;

                // These can load thousands of rows; queueing returns from the UI thread immediately. Params are
                // read inside Queue while the JsonDocument is still alive; the queued closure holds plain values.
                if (method == "init" || method == "getItems" || method == "search")
                {
                    Queue(method, id, p);
                    return;
                }

                // Paste hides the window and must answer after the target app has focus again, so it owns its
                // own timer and reply instead of the synchronous reply below.
                if (method == "paste")
                {
                    BeginPaste(id, p);
                    return;
                }

                object result = Invoke(method, p);
                Reply(Ok(id, result));
            }
            catch (Exception ex)
            {
                Reply(Error(id, ex));
            }
        }

        object Invoke(string method, JsonElement p)
        {
            switch (method)
            {
                case "getItem":
                {
                    long id = Long(p, "id");
                    var rich = _store.GetRich(id);
                    var meta = Meta(new List<long> { id });
                    meta.TryGetValue(id, out var info);
                    return new
                    {
                        text = _store.GetText(id),
                        html = rich?.Html,
                        rtf = rich?.Rtf,
                        formats = info?.Formats ?? new List<string>()
                    };
                }

                case "getThumb":
                {
                    byte[] thumb = _store.GetThumb(Long(p, "id"));
                    return thumb == null ? null : Convert.ToBase64String(thumb);
                }

                case "getImage":
                {
                    var img = _store.GetImage(Long(p, "id"));
                    byte[] bytes = img?.Image;
                    return bytes == null ? null : Convert.ToBase64String(bytes);
                }

                case "copy":
                    CopyToClipboard(_store, Long(p, "id"), Bool(p, "plain"));
                    TouchAndNotify(Long(p, "id"));
                    return new { copied = true };

                case "export":
                    return Export(p);

                case "saveConfig":
                    return SaveConfig(p);

                case "getConfig":
                    return GetConfig(p);

                case "saveEditor":
                    return SaveEditor(p);

                case "reOcr":
                    return ReOcr(p);

                case "clearHistory":
                    return ClearHistory(p);

                case "storageUsage":
                    return StorageUsage(p);

                case "delete":
                    return Delete(p);

                case "restore":
                    return Restore(p);

                case "togglePin":
                    return TogglePin(p);

                case "moveToTab":
                    return MoveToTab(p);

                case "listTabs":
                    return ListTabs(p);

                case "createTab":
                    return CreateTab(p);

                case "renameTab":
                    return RenameTab(p);

                case "deleteTab":
                    return DeleteTab(p);

                case "reorderTabs":
                    return ReorderTabs(p);

                case "hide":
                    _hide();
                    return new { hidden = true };

                case "show":
                    _show();
                    return new { shown = true };

                case "previewNotification":
                    // The owner sees the real toast, at the real coordinates, with his current settings.
                    _previewToast?.Invoke();
                    return new { shown = _previewToast != null };

                default:
                    throw new InvalidOperationException("unknown method: " + method);
            }
        }

        // ---- deferred reads -------------------------------------------------------------------

        /// <summary>Reads params on the UI thread, then lets <see cref="_reads"/> do every row read and tag pass.</summary>
        void Queue(string method, string id, JsonElement p)
        {
            int maxItems = _cfg.MaxItems;
            switch (method)
            {
                case "init":
                {
                    // Counting and the tab strip are index-only and sub-millisecond; only the page is deferred.
                    var counts = Counts();
                    var tabs = Tabs();
                    var config = ConfigDto();
                    _reads.Post(() => Finish(id, () => new
                    {
                        counts,
                        tabs,
                        config,
                        items = ReadPage(_dbPath, _pinnedTabId, maxItems, null, 0, 200, null, 0).Items
                    }));
                    return;
                }

                case "getItems":
                {
                    long? tabId = Tab(p);
                    int offset = Int(p, "offset", 0);
                    int limit = Int(p, "limit", 200);
                    string query = String(p, "query");
                    // A query's total comes from its hits on the worker; an unfiltered page needs one cheap count.
                    int total = string.IsNullOrWhiteSpace(query) ? Total(tabId) : 0;
                    _reads.Post(() => Finish(id, () =>
                        ReadPage(_dbPath, _pinnedTabId, maxItems, tabId, offset, limit, query, total)));
                    return;
                }

                case "search":
                {
                    long? tabId = Tab(p);
                    int offset = Int(p, "offset", 0);
                    int limit = Int(p, "limit", 200);
                    string query = String(p, "query");
                    _reads.Post(() => Finish(id, () =>
                        ReadSearch(_dbPath, _pinnedTabId, maxItems, tabId, offset, limit, query)));
                    return;
                }
            }
        }

        /// <summary>Runs on the read thread: read, classify and serialize there; only the finished string is posted back.</summary>
        void Finish(string id, Func<object> read)
        {
            string json;
            try
            {
                json = Ok(id, read());
            }
            catch (Exception ex)
            {
                json = Error(id, ex); // one reply per request, including a failed one
            }
            OnUi(() => Reply(json));
        }

        static string Ok(string id, object result) =>
            "{\"id\":" + id + ",\"ok\":true,\"result\":" + JsonSerializer.Serialize(result, Json) + "}";

        static string Error(string id, Exception ex) =>
            "{\"id\":" + id + ",\"ok\":false,\"error\":" + JsonSerializer.Serialize(ex.Message, Json) + "}";

        void OnUi(Action action)
        {
            try { _dispatcher.BeginInvoke(action); }
            catch (InvalidOperationException)
            {
                // The dispatcher is shutting down: the window that would read this reply is gone.
            }
        }

        // ---- UI-thread helpers ----------------------------------------------------------------

        int Total(long? tabId)
        {
            if (!tabId.HasValue)
            {
                var (clips, images) = _store.Counts();
                // The page and the search must agree on scope: main history is the newest Config.MaxItems rows.
                return Math.Min(clips + images, Math.Max(1, _cfg.MaxItems));
            }
            foreach (var tab in _store.ListTabs())
                if (tab.id == tabId.Value) return tab.count;
            return 0;
        }

        object Counts()
        {
            var (clips, images) = _store.Counts();
            // `all` is what the list can show (the page caps at MaxItems); clips/images stay the raw DB counts.
            return new { clips, images, all = Math.Min(clips + images, Math.Max(1, _cfg.MaxItems)) };
        }

        object Tabs() => _store.ListTabs()
            .Select(t => new { id = t.id, name = t.name, kind = t.kind, count = t.count })
            .ToList();

        /// <summary>Single-row metadata (getItem only; pages read it on the reader thread). No handle is kept.</summary>
        Dictionary<long, ItemMeta> Meta(List<long> ids) => ReadMetaAt(_dbPath, ids);

        void Reply(string json)
        {
            try { _web.CoreWebView2?.PostWebMessageAsJson(json); }
            catch (InvalidOperationException)
            {
                // The browser is tearing down; a reply that cannot be delivered has no reader anyway.
            }
        }

        // ---- params ---------------------------------------------------------------------------

        static bool TryParam(JsonElement p, string name, out JsonElement value)
        {
            value = default;
            return p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out value);
        }

        static string String(JsonElement p, string name)
        {
            if (!TryParam(p, name, out var v) || v.ValueKind != JsonValueKind.String) return null;
            return v.GetString();
        }

        static int Int(JsonElement p, string name, int fallback)
        {
            if (!TryParam(p, name, out var v)) return fallback;
            if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
            return int.TryParse(v.GetString(), out int n) ? n : fallback;
        }

        static bool Bool(JsonElement p, string name)
        {
            if (!TryParam(p, name, out var v)) return false;
            return v.ValueKind == JsonValueKind.True;
        }

        static long Long(JsonElement p, string name)
        {
            if (!TryParam(p, name, out var v)) throw new ArgumentException(name + " required");
            if (v.ValueKind == JsonValueKind.Number) return v.GetInt64();
            if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out long n)) return n;
            throw new ArgumentException(name + " must be a number");
        }

        static long? Tab(JsonElement p)
        {
            if (!TryParam(p, "tabId", out var v) || v.ValueKind == JsonValueKind.Null) return null;
            long id = v.ValueKind == JsonValueKind.Number ? v.GetInt64()
                : long.TryParse(v.GetString(), out long n) ? n : 0;
            return id > 0 ? id : (long?)null; // 0/null both mean the main history
        }

        public void Dispose()
        {
            _reads.Dispose();
            var core = _web.CoreWebView2;
            if (core != null) core.WebMessageReceived -= OnMessage;
        }
    }
}
