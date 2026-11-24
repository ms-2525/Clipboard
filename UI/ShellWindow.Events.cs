using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Clip.Core;
using Microsoft.Web.WebView2.Core;

namespace Clip.UI
{
    /// <summary>
    /// The shell as a real tray app: warm creation, demand show/hide, the process-close guard and the
    /// host→page events. Window chrome, the bridge host and paste plumbing stay in ShellWindow.xaml.cs.
    /// </summary>
    public partial class ShellWindow
    {
        readonly string _timingPath = Environment.GetEnvironmentVariable("CLIP_V2_TIMING");
        bool _warming = true;
        bool _pageReady;
        bool _allowClose;
        string _hotkeyFailure;

        /// <summary>
        /// Created at startup as a transparent, taskbar-less window so the WebView2 and the page are built once;
        /// <see cref="EndWarm"/> hides it again after the first painted frame. A demand show never reloads.
        /// </summary>
        public void Warm()
        {
            ShowActivated = false;
            ShowInTaskbar = false;
            Opacity = 0;
            Show();
        }

        internal void Toggle()
        {
            if (IsVisible) Hide();
            else ShowShell();
        }

        internal void ShowShell()
        {
            if (_warming) EndWarm(); // demanded before the first frame: finish the warm surface instead of reloading
            if (!IsVisible) Show();
            Activate();
        }

        /// <summary>Tray Settings: the page owns that surface and has no route yet, so its own button is clicked.</summary>
        internal void ShowForSettings()
        {
            ShowShell();
            _ = Web.CoreWebView2?.ExecuteScriptAsync(
                "document.querySelector('button[aria-label=\"Settings\"]')?.click()");
        }

        /// <summary>Hides the transparent warm window and restores the real show settings. Runs once.</summary>
        void EndWarm()
        {
            if (!_warming) return;
            _warming = false;
            Hide();
            Opacity = 1;
            ShowInTaskbar = true;
            ShowActivated = true;
        }

        void OnClosing(object sender, CancelEventArgs e)
        {
            if (_allowClose) return;
            e.Cancel = true; // the shell hides; only the tray's Exit ends the process
            Hide();
        }

        internal void AllowClose() => _allowClose = true;

        // ---- bridge events ---------------------------------------------------

        static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>The page's live "captured" feed; the DTO is read back exactly like a list row.</summary>
        public void NotifyCaptured(ClipboardItem item) => _host?.EmitCaptured(item);

        public void NotifySkipped(string reason) => Emit("skipped", new { reason });

        public void NotifyHotkeyFailed(string combo)
        {
            _hotkeyFailure = combo;
            if (_pageReady) Emit("hotkeyFailed", new { combo });
        }

        public void NotifyThemeChanged(string theme) => Emit("themeChanged", new { theme });

        void Emit(string name, object data)
        {
            string json = "{\"event\":" + JsonSerializer.Serialize(name, Json) +
                          ",\"data\":" + JsonSerializer.Serialize(data, Json) + "}";
            try { Web.CoreWebView2?.PostWebMessageAsJson(json); }
            catch (InvalidOperationException)
            {
                // The browser is shutting down; an event with no reader is not worth a crash.
            }
        }

        void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e) => _ = OnPageLoadedAsync();

        async Task OnPageLoadedAsync()
        {
            _pageReady = true;
            // A failed hotkey is still true after a reload, so a fresh page is told again instead of losing it.
            if (_hotkeyFailure != null) Emit("hotkeyFailed", new { combo = _hotkeyFailure });
            if (!_warming) return;

            try { await FirstFrameAsync(); }
            catch (Exception) { } // the runtime can disappear mid-load; the surface still ends
            EndWarm();
            if (!string.IsNullOrEmpty(_timingPath)) await RunShowProbeAsync();
        }

        async Task<double> FirstFrameAsync()
        {
            var watch = Stopwatch.StartNew();
            await Web.CoreWebView2.ExecuteScriptAsync("new Promise(r => requestAnimationFrame(() => r(1)))");
            return watch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Dev-only acceptance probe: with CLIP_V2_TIMING=&lt;file&gt; set it opens and hides the warm shell 20
        /// times and writes each real Show()→first-frame time. Unset on the normal path.
        /// </summary>
        async Task RunShowProbeAsync()
        {
            var lines = new List<string>();
            for (int i = 0; i < 20; i++)
            {
                var watch = Stopwatch.StartNew();
                ShowShell();
                await FirstFrameAsync();
                Hide();
                lines.Add($"{watch.Elapsed.TotalMilliseconds:0.0}");
                await Task.Delay(150);
            }
            File.WriteAllLines(_timingPath, lines);
        }
    }
}
