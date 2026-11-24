using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Threading;
using Clip.Core;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Clip.Host
{
    /// <summary>
    /// The typed page&lt;-&gt;host bridge. The page sends {id, method, params}; every reply is
    /// {id, ok:true, result} or {id, ok:false, error}. Runs entirely on the UI thread because
    /// ClipboardStore's connection is UI-thread-affine.
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
        SqliteConnection _meta;
        bool _metaFailed;

        public ClipHost(WebView2 web, ClipboardStore store, Config cfg, string dbPath)
        {
            _web = web;
            _store = store;
            _cfg = cfg;
            _dbPath = dbPath;
            _dispatcher = web.Dispatcher;
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

                object result = Invoke(method, p);
                Reply("{\"id\":" + id + ",\"ok\":true,\"result\":" + JsonSerializer.Serialize(result, Json) + "}");
            }
            catch (Exception ex)
            {
                Reply("{\"id\":" + id + ",\"ok\":false,\"error\":" + JsonSerializer.Serialize(ex.Message, Json) + "}");
            }
        }

        object Invoke(string method, JsonElement p)
        {
            switch (method)
            {
                case "init":
                    return new
                    {
                        counts = Counts(),
                        tabs = Tabs(),
                        config = _cfg,
                        items = Page(null, 0, 200, null).Items
                    };

                case "getItems":
                    return Page(Tab(p), Int(p, "offset", 0), Int(p, "limit", 200), String(p, "query"));

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

                case "copy":
                    CopyToClipboard(_store, Long(p, "id"), Bool(p, "plain"));
                    return new { copied = true };

                case "paste":
                    throw new InvalidOperationException("phase 2");

                default:
                    throw new InvalidOperationException("unknown method: " + method);
            }
        }

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
            var core = _web.CoreWebView2;
            if (core != null) core.WebMessageReceived -= OnMessage;
            _meta?.Dispose();
            _meta = null;
        }
    }
}
