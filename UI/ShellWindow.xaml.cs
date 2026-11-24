using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Clip.Core;
using Clip.Host;
using Clip.Interop;
using Microsoft.Web.WebView2.Core;

namespace Clip.UI
{
    /// <summary>
    /// v2 development shell: one WebView2, borderless 900x700, serving UI\web\dist over https://clip.local/.
    /// Phase 1 is read-only plus copy; the page never opens the DB itself.
    /// </summary>
    public partial class ShellWindow : Window
    {
        const string HostName = "clip.local";

        readonly ClipboardStore _store;
        readonly Config _cfg;
        readonly string _dbPath;
        ClipHost _host;

        public ShellWindow(ClipboardStore store, Config cfg, string dbPath)
        {
            InitializeComponent();
            _store = store;
            _cfg = cfg;
            _dbPath = dbPath;

            var app = TryFindResource("App") as SolidColorBrush;
            if (app != null)
                Web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(app.Color.A, app.Color.R, app.Color.G, app.Color.B);

            SourceInitialized += (s, e) => WindowChrome.Apply(this);
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Dev-only profile location: the owner's app profile is never reused by this shell.
                string profile = Path.Combine(Path.GetTempPath(), "opencode", "clip-v2-webview");
                Directory.CreateDirectory(profile);

                // CLIP_V2_DEBUG_PORT is a dev-only CDP hook for verification; unset on the normal path.
                string debugPort = Environment.GetEnvironmentVariable("CLIP_V2_DEBUG_PORT");
                var options = string.IsNullOrEmpty(debugPort)
                    ? null
                    : new CoreWebView2EnvironmentOptions("--remote-debugging-port=" + debugPort);
                var environment = await CoreWebView2Environment.CreateAsync(null, profile, options);
                await Web.EnsureCoreWebView2Async(environment);

                var settings = Web.CoreWebView2.Settings;
                settings.AreDefaultContextMenusEnabled = false;
                settings.AreDevToolsEnabled = false;
                settings.IsStatusBarEnabled = false;
                settings.AreBrowserAcceleratorKeysEnabled = false;
                settings.IsZoomControlEnabled = false;
                settings.IsSwipeNavigationEnabled = false;

                Web.CoreWebView2.NavigationStarting += OnNavigationStarting;
                Web.CoreWebView2.NewWindowRequested += (s, args) => args.Handled = true;

                _host = new ClipHost(Web, _store, _cfg, _dbPath);

                string webRoot = FindWebRoot();
                if (webRoot == null)
                {
                    Web.NavigateToString(FallbackHtml);
                    return;
                }

                Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    HostName, webRoot, CoreWebView2HostResourceAccessKind.DenyCors);
                Web.Source = new Uri($"https://{HostName}/index.html");
            }
            catch
            {
                // A runtime-less machine has no browser at all: say so instead of taking the process down.
                Notice.Text = "The WebView2 runtime is not available, so the v2 shell cannot be shown.";
                Notice.Visibility = Visibility.Visible;
            }
        }

        /// <summary>The dist lives in the worktree during Phase 1; walk up from bin\Release\net10.0-windows.</summary>
        static string FindWebRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; dir != null && depth < 8; depth++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "UI", "web", "dist");
                if (File.Exists(Path.Combine(candidate, "index.html"))) return candidate;
            }
            return null;
        }

        /// <summary>https://clip.local/ is the only address this window may show; a redirect is not ours to follow.</summary>
        void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Uri)) return; // NavigateToString has no real address
            if (e.Uri.StartsWith($"https://{HostName}/", StringComparison.OrdinalIgnoreCase)) return;
            if (e.Uri == "about:blank") return; // NavigateToString (the fallback page) lands here
            if (e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase)) return; // and here on some runtimes
            e.Cancel = true;
        }

        void OnClosed(object sender, EventArgs e)
        {
            try { _host?.Dispose(); }
            catch (Exception) { } // browser already torn down; nothing left to unsubscribe
            try { Web.Dispose(); }
            catch (Exception) { } // browser still starting up may refuse; the process owns nothing now
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Shown only until the web agent's dist exists. It is deliberately a bridge self-check: the
        /// counts, tabs and first rows prove the page can reach the real store without any UI code.
        /// </summary>
        const string FallbackHtml = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:">
<title>Clip v2 - web build missing</title>
<style>
  body { margin: 0; padding: 32px; background: Canvas; color: CanvasText;
         font: 14px/1.5 "Segoe UI", system-ui, sans-serif; }
  h1 { margin: 0 0 8px; font-size: 22px; letter-spacing: -0.01em; }
  p { margin: 0 0 20px; color: GrayText; }
  #status { margin: 0 0 10px; color: CanvasText; font-family: Consolas, monospace; font-size: 12px; }
  #tabs { margin: 0 0 18px; color: CanvasText; font-size: 12px; }
  .row { display: flex; gap: 12px; align-items: center; padding: 10px 0;
         border-top: 1px solid color-mix(in srgb, CanvasText 12%, Canvas); }
  .row img { width: 48px; height: 48px; object-fit: cover; border-radius: 8px; background: ButtonFace; }
  .body { min-width: 0; }
  .head { color: GrayText; font-size: 11px; text-transform: uppercase; letter-spacing: 0.04em; }
  .text { color: CanvasText; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
</style>
</head>
<body>
  <h1>web build missing</h1>
  <p>UI\web\dist\index.html was not found, so the React app was not served. The host bridge is running.</p>
  <div id="status">bridge: connecting...</div>
  <div id="tabs"></div>
  <div id="items"></div>
  <script>
    const pending = new Map();
    let seq = 0;
    chrome.webview.addEventListener('message', e => {
      const m = e.data;
      const done = m && pending.get(m.id);
      if (done) { pending.delete(m.id); done(m); }
    });
    function call(method, params) {
      return new Promise((resolve, reject) => {
        const id = ++seq;
        pending.set(id, resolve);
        chrome.webview.postMessage({ id, method, params: params || {} });
        setTimeout(() => { if (pending.delete(id)) reject(new Error('timeout: ' + method)); }, 5000);
      });
    }
    (async () => {
      const status = document.getElementById('status');
      try {
        const response = await call('init');
        if (!response.ok) throw new Error(response.error);
        const data = response.result;
        status.textContent = `bridge OK - ${data.counts.clips} clips, ${data.counts.images} images, ${data.tabs.length} tabs`;
        document.getElementById('tabs').textContent =
          data.tabs.map(t => `${t.name} (${t.count})`).join('  /  ');
        const list = document.getElementById('items');
        const preview = data.items.slice(0, 8);
        const image = data.items.find(i => i.hasImage);
        if (image && !preview.some(i => i.id === image.id)) preview.push(image);
        for (const item of preview) {
          const row = document.createElement('div');
          row.className = 'row';
          if (item.hasImage) {
            const thumb = await call('getThumb', { id: item.id });
            if (thumb.ok && thumb.result) {
              const img = document.createElement('img');
              img.src = 'data:image/png;base64,' + thumb.result;
              row.append(img);
            }
          }
          const body = document.createElement('div');
          body.className = 'body';
          const head = document.createElement('div');
          head.className = 'head';
          head.textContent = `${item.kind} - ${item.app || 'Unknown'} - ${item.time || ''}`;
          const text = document.createElement('div');
          text.className = 'text';
          text.textContent = (item.text || '').slice(0, 240);
          body.append(head, text);
          row.append(body);
          list.append(row);
        }
      } catch (err) {
        status.textContent = 'bridge error: ' + err.message;
      }
    })();
  </script>
</body>
</html>
""";
    }
}
