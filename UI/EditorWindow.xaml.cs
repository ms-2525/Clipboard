using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Clip.Core;
using Clip.Interop;
using Microsoft.Web.WebView2.Core;

namespace Clip.UI
{
    /// <summary>
    /// F2 editor: WPF owns the chrome and toolbar, the embedded TipTap page owns the text.
    /// The clip loads as its HTML, else its RTF converted once, else plain text; a body that cannot
    /// be read says so in the notice bar instead of silently losing its formatting
    /// (docs\HANDOFF-tiptap-port.md §3).
    /// </summary>
    public partial class EditorWindow : Window
    {
        const int MaxHtmlFragment = 2_000_000;

        const string PlainNotice = "This clip's formatting could not be loaded; editing as plain text.";
        const string TooLargeNotice = "This clip's HTML body is larger than 2 MB, so its formatting was not loaded; editing as plain text.";
        const string RuntimeNotice = "Editing needs the free WebView2 runtime (installed with Windows).";
        const string PreviewNotice = "This clip's full text could not be loaded, so saving would replace it with a partial copy. Nothing was saved.";

        readonly ClipboardStore _store;
        readonly ClipboardItem _item;
        string _loadHtml;
        string _loadText;
        bool _ready;
        bool _dirty;
        bool _saving;

        public EditorWindow(ClipboardStore store, ClipboardItem item, string html, string rtf)
        {
            InitializeComponent();
            _store = store;
            _item = item;

            // The shell fades in and the field has an 8 DIP frame: the page paints its own rounded canvas.
            Web.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            PrepareBody(html, rtf);

            SourceInitialized += (s, e) => WindowChrome.Apply(this);
            Loaded += OnLoaded;
            Closed += (s, e) =>
            {
                try { Web.Dispose(); }
                catch
                {
                    // A browser still starting up when the window closes can refuse disposal; the process
                    // owns nothing after this point, so a failed release must not take the tray app down.
                }
            };
        }

        // ---- load -------------------------------------------------------------------------

        /// <summary>HTML first, then RTF converted once, then plain text; a notice names every downgrade.</summary>
        void PrepareBody(string html, string rtf)
        {
            if (!string.IsNullOrEmpty(html))
            {
                string fragment = null;
                try { fragment = HtmlFragment.Extract(html); }
                catch
                {
                    // A CF_HTML body the tokeniser rejects is a failed load, reported by the notice below.
                }

                if (string.IsNullOrEmpty(fragment) || fragment.Length > MaxHtmlFragment)
                {
                    ShowNotice(string.IsNullOrEmpty(fragment) ? PlainNotice : TooLargeNotice);
                    _loadText = _item.Text;
                    return;
                }

                _loadHtml = fragment;
                return;
            }

            if (!string.IsNullOrEmpty(rtf))
            {
                _loadHtml = EditorRtf.ToHtml(rtf);
                if (_loadHtml != null) return;
                ShowNotice(PlainNotice);
            }

            _loadText = _item.Text;
        }

        void ShowNotice(string text)
        {
            FormatNoticeText.Text = text;
            FormatNotice.Visibility = Visibility.Visible;
        }

        // ---- start ------------------------------------------------------------------------

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // HoldEnd would keep an opacity clock alive and the whole window greyscale:
            // clear it the moment the fade lands so ClearType returns for the session.
            var fadeIn = Motion.Linear(1, 150);
            fadeIn.FillBehavior = FillBehavior.Stop;
            fadeIn.Completed += (a, b) =>
            {
                Shell.BeginAnimation(OpacityProperty, null);
                Shell.Opacity = 1;
            };
            Shell.BeginAnimation(OpacityProperty, fadeIn);
            await StartWebAsync();
        }

        async Task StartWebAsync()
        {
            try
            {
                // The browser profile lives in the app's data folder, never beside the exe
                // (the owner build is one self-contained file).
                var environment = await CoreWebView2Environment.CreateAsync(
                    null, Path.Combine(Config.DataDir, "webview2"));
                await Web.EnsureCoreWebView2Async(environment);

                var settings = Web.CoreWebView2.Settings;
                settings.AreDefaultContextMenusEnabled = false;
                settings.AreDevToolsEnabled = false;
                settings.IsStatusBarEnabled = false;
                settings.AreBrowserAcceleratorKeysEnabled = false;
                settings.IsZoomControlEnabled = false;
                settings.IsSwipeNavigationEnabled = false;

                // The page ships inside the exe, so https://clip/ is a name the host answers itself.
                Web.CoreWebView2.AddWebResourceRequestedFilter(
                    "https://clip/*", CoreWebView2WebResourceContext.All);
                Web.CoreWebView2.WebResourceRequested += OnResourceRequested;
                Web.CoreWebView2.WebMessageReceived += OnMessage;
                Web.CoreWebView2.NavigationStarting += OnNavigationStarting;
                Web.CoreWebView2.NewWindowRequested += (s, e) => e.Handled = true;
                Web.Source = new Uri(ThemeUrl());
            }
            catch
            {
                // A machine without the runtime has no browser at all: say so, never throw out of the window.
                ShowNotice(RuntimeNotice);
            }
        }

        /// <summary>The page is LAYER B and follows the theme, exactly like the row renderer.</summary>
        string ThemeUrl() =>
            "https://clip/editor.html?canvas=" + Uri.EscapeDataString(ThemeHex("ContentCanvas"))
            + "&ink=" + Uri.EscapeDataString(ThemeHex("ContentInk"))
            + "&link=" + Uri.EscapeDataString(ThemeHex("ContentLink"));

        /// <summary>A theme brush as #RRGGBB — the page's query parser accepts nothing carrying alpha.</summary>
        string ThemeHex(string key) =>
            Hex(TryFindResource(key) is SolidColorBrush brush ? brush.Color : Colors.Black);

        // ---- page resources ---------------------------------------------------------------

        /// <summary>Answers every https://clip/ request from the embedded page or the pack fonts; else 404.</summary>
        void OnResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var environment = Web.CoreWebView2.Environment;
            string path = new Uri(e.Request.Uri).AbsolutePath;
            (byte[] body, string mime) = path switch
            {
                "/editor.html" => (Embedded("clip.assets.editor.html"), "text/html"),
                "/editor.css" => (Embedded("clip.assets.editor.css"), "text/css"),
                "/editor.bundle.js" => (Embedded("clip.assets.editor.bundle.js"), "text/javascript"),
                "/Inter-Regular.ttf" => (Font("Inter-Regular.ttf"), "font/ttf"),
                "/Inter-SemiBold.ttf" => (Font("Inter-SemiBold.ttf"), "font/ttf"),
                "/Sora-Regular.ttf" => (Font("Sora-Regular.ttf"), "font/ttf"),
                "/Sora-SemiBold.ttf" => (Font("Sora-SemiBold.ttf"), "font/ttf"),
                _ => (null, (string)null),
            };

            e.Response = body == null
                ? environment.CreateWebResourceResponse(null, 404, "Not Found", "Content-Type: text/plain")
                : environment.CreateWebResourceResponse(new MemoryStream(body), 200, "OK", "Content-Type: " + mime);
        }

        static byte[] Embedded(string name)
        {
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
                if (stream == null) return null;
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            }
            catch
            {
                return null; // an asset the exe cannot read becomes the 404 above, never a broken page
            }
        }

        static byte[] Font(string file)
        {
            try
            {
                // The assembly name comes from the code that owns the resource, so the one rename point
                // (AssemblyName in Clip.csproj) stays the only place a product name lives.
                string assembly = typeof(EditorWindow).Assembly.GetName().Name;
                var info = Application.GetResourceStream(new Uri(
                    "pack://application:,,,/" + assembly + ";component/Assets/Fonts/" + file));
                using var stream = info?.Stream;
                if (stream == null) return null;
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            }
            catch
            {
                return null; // a missing face falls back to the page's own font stack
            }
        }

        /// <summary>https://clip/ is the only address this window may show; a redirect is not ours to follow.</summary>
        void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!e.Uri.StartsWith("https://clip/", StringComparison.Ordinal)) e.Cancel = true;
        }

        // ---- page messages ----------------------------------------------------------------

        void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var message = JsonDocument.Parse(e.WebMessageAsJson);
                var root = message.RootElement;
                switch (root.GetProperty("type").GetString())
                {
                    case "ready":
                        Seed();
                        break;
                    case "dirty":
                        _dirty = root.GetProperty("dirty").GetBoolean();
                        break;
                    case "save":
                        SaveAndClose();
                        break;
                    case "cancel":
                        RequestClose();
                        break;
                    case "sel":
                        ReflectSel(root);
                        break;
                    case "focus":
                        Field.SetResourceReference(Border.BorderBrushProperty, "AccentText");
                        break;
                    case "blur":
                        Field.SetResourceReference(Border.BorderBrushProperty, "ControlLine");
                        break;
                }
            }
            catch
            {
                // A malformed or half-arriving message must never take the tray app down with it.
            }
        }

        async void Seed()
        {
            try
            {
                if (_loadHtml != null)
                    await Web.ExecuteScriptAsync("window.clipBridge.load(" + JsonSerializer.Serialize(_loadHtml) + ")");
                else
                    await Web.ExecuteScriptAsync("window.clipBridge.loadPlain(" + JsonSerializer.Serialize(_loadText ?? "") + ")");
                // Only a page that has actually received the clip may speak for it on save.
                _ready = true;
                // The page's focus event is the only focus signal WPF gets from a WebView2 child.
                Web.Focus();
            }
            catch
            {
                // The page died between "ready" and the seed: it stays empty and closing writes nothing.
            }
        }

        // ---- save -------------------------------------------------------------------------

        static string Normalize(string text) => (text ?? "").TrimEnd('\r', '\n');

        async void SaveAndClose()
        {
            if (_saving) return;
            _saving = true;
            if (!_ready)
            {
                // A page that never loaded cannot speak for the clip: a rich body is never replaced by an empty one.
                Close();
                return;
            }

            try
            {
                string html = await ReadJson("window.clipBridge.getHtml()");
                string text = await ReadJson("window.clipBridge.getText()");
                if (html == null)
                {
                    _saving = false;
                    return;
                }

                _item.Text = Normalize(text);
                _item.Html = HtmlFragment.Wrap(html);
                _item.Rtf = null;
                if (!_store.Update(_item))
                {
                    // Only a caller that handed the editor a list preview gets here; the stored body is untouched.
                    ShowNotice(PreviewNotice);
                    _saving = false;
                    return;
                }
                Close();
            }
            catch
            {
                // The text is still on screen, so keep the window open rather than close over an unwritten edit.
                _saving = false;
            }
        }

        async Task<string> ReadJson(string script) =>
            JsonSerializer.Deserialize<string>(await Web.ExecuteScriptAsync(script));

        // ---- close / prompt ---------------------------------------------------------------

        void RequestClose()
        {
            if (!_ready || !_dirty)
            {
                Close();
                return;
            }
            DiscardPrompt.Visibility = Visibility.Visible;
            KeepEditing.Focus();
        }

        void KeepEditing_Click()
        {
            DiscardPrompt.Visibility = Visibility.Collapsed;
            Web.Focus();
            FocusPage();
        }

        async void FocusPage()
        {
            try { await Web.ExecuteScriptAsync("window.clipBridge.focus()"); }
            catch { } // the page is gone; the window still closes normally
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (DiscardPrompt.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.F2) KeepEditing_Click();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                SaveAndClose();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape || e.Key == Key.F2)
            {
                RequestClose();
                e.Handled = true;
            }
        }

        void OnCancelClick(object sender, RoutedEventArgs e) => RequestClose();
        void OnSaveClick(object sender, RoutedEventArgs e) => SaveAndClose();
        void OnKeepEditingClick(object sender, RoutedEventArgs e) => KeepEditing_Click();

        void OnDiscardClick(object sender, RoutedEventArgs e)
        {
            DiscardPrompt.Visibility = Visibility.Collapsed;
            Close();
        }
    }
}
