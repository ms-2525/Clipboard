using System;
using System.Reflection;
using System.Text.Json;
using Clip.Core;
using Clip.Interop;
using Microsoft.Win32;

namespace Clip.Host
{
    /// <summary>
    /// Settings and the F2 editor: the page's saveConfig / saveEditor / clearHistory commands.
    /// Every write goes through <see cref="Config"/>'s own camelCase file, the editor reuses the
    /// CF_HTML writer (<see cref="HtmlFragment.Wrap"/>) and the store's truncation guard, and the
    /// hotkey is rebound by the app that owns the bindings rather than by a second registration.
    /// </summary>
    internal sealed partial class ClipHost
    {
        const int ZoomMin = 80;
        const int ZoomMax = 150;
        // Same ceiling EditorWindow refuses to load; a page that sends more is not describing a clip.
        const int MaxEditorHtml = 2_000_000;

        /// <summary>The user's choice ("light"/"dark"/"system"); <see cref="_cfg"/>.Theme holds the resolved one.</summary>
        string _themeChoice = "light";
        int _zoom = 100;
        bool _settingsLoaded;

        /// <summary>
        /// Called once from the constructor. App.V2 resolves "system" for the shell before this host exists,
        /// so the raw choice is read back from the file it was saved in; the zoom is applied to the browser here.
        /// </summary>
        void LoadSettings()
        {
            if (_settingsLoaded) return;
            _settingsLoaded = true;
            _themeChoice = Config.Load().Theme;
            _zoom = Math.Clamp(_cfg.UiZoom, ZoomMin, ZoomMax);
            _cfg.UiZoom = _zoom;
            try { _web.ZoomFactor = _zoom / 100.0; }
            catch (InvalidOperationException)
            {
                // The browser is still initialising on a cold start; the first settings save applies it again.
            }
            if (_themeChoice == "system") SystemEvents.UserPreferenceChanged += OnThemePreferenceChanged;
        }

        /// <summary>The page's settings shape: the whole live config plus the raw theme choice.</summary>
        object ConfigDto()
        {
            var n = _cfg.Notification;
            return new
            {
                theme = _cfg.Theme,
                themeChoice = _themeChoice,
                timeFormat = _cfg.TimeFormat,
                maxItems = _cfg.MaxItems,
                previewLines = _cfg.PreviewLines,
                uiZoom = _zoom,
                hotkey = _cfg.Hotkey,
                hotkeyPlain = _cfg.HotkeyPlain,
                startOnLogin = _cfg.StartOnLogin,
                notification = new
                {
                    width = n.Width,
                    height = n.Height,
                    maxLines = n.MaxLines,
                    position = n.Position
                }
            };
        }

        // ---- saveConfig -----------------------------------------------------------------------

        /// <summary>
        /// Validates the whole patch first, then applies what is applied and persists once. A rejected value
        /// changes nothing. The theme is stored as the choice ("system") and only the live copy is resolved.
        /// </summary>
        object SaveConfig(JsonElement p)
        {
            if (!TryParam(p, "patch", out var patch) || patch.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("patch required");

            string theme = Choice(patch, "theme", null, "light", "dark", "system");
            string timeFormat = Choice(patch, "timeFormat", null, "12h", "24h");
            int? maxItems = IntOrNull(patch, "maxItems");
            int? zoom = IntOrNull(patch, "uiZoom");
            string hotkey = String(patch, "hotkey");
            bool? startOnLogin = BoolOrNull(patch, "startOnLogin");
            if (maxItems.HasValue) maxItems = Math.Clamp(maxItems.Value, 20, 10_000);
            if (zoom.HasValue) zoom = Math.Clamp(zoom.Value, ZoomMin, ZoomMax);
            if (hotkey != null && !IsHotkey(hotkey))
                throw new ArgumentException("\"" + hotkey + "\" is not a key combination. Use at least one modifier and a key.");

            if (TryParam(patch, "notification", out var n))
            {
                if (n.ValueKind != JsonValueKind.Object) throw new ArgumentException("notification must be an object");
                ApplyNotification(n);
            }

            bool themeChanged = false;
            if (theme != null)
            {
                _themeChoice = theme;
                string resolved = ResolveTheme(theme);
                themeChanged = resolved != _cfg.Theme;
                _cfg.Theme = resolved;
                WatchSystemTheme(theme == "system");
            }
            if (maxItems.HasValue) _cfg.MaxItems = maxItems.Value;
            if (timeFormat != null)
            {
                _cfg.TimeFormat = timeFormat;
                // The WPF toast and the old list stamp their own time; both read this static.
                ClipboardItem.ClockFormat = timeFormat == "12h" ? "h:mm tt" : "HH:mm";
            }
            if (zoom.HasValue)
            {
                _zoom = zoom.Value;
                _cfg.UiZoom = _zoom;
                _web.ZoomFactor = _zoom / 100.0;
            }
            if (startOnLogin.HasValue && startOnLogin.Value != _cfg.StartOnLogin)
            {
                _cfg.StartOnLogin = startOnLogin.Value;
                Autostart.Set(startOnLogin.Value);
            }

            // The hotkey last: a conflict rolls back to the binding that still works, and nothing else is lost.
            string hotkeyError = null;
            if (hotkey != null && hotkey != _cfg.Hotkey) hotkeyError = RebindHotkey(hotkey);

            // Persist the choice, not the resolved theme: App.V2 resolves "system" again on the next start.
            _cfg.Theme = _themeChoice;
            _cfg.Save();
            _cfg.Theme = ResolveTheme(_themeChoice);

            if (themeChanged) OnUi(() => Emit("themeChanged", new { theme = _cfg.Theme }));
            OnUi(() => Emit("settingsSaved", ConfigDto()));
            return new { config = ConfigDto(), hotkeyError };
        }

        void ApplyNotification(JsonElement n)
        {
            int? width = IntOrNull(n, "width");
            int? height = IntOrNull(n, "height");
            int? lines = IntOrNull(n, "maxLines");
            string position = Choice(n, "position", null, "bottom-right", "bottom-left", "top-right", "top-left");
            if (width.HasValue) _cfg.Notification.Width = Math.Clamp(width.Value, 240, 900);
            if (height.HasValue) _cfg.Notification.Height = Math.Clamp(height.Value, 60, 700);
            if (lines.HasValue) _cfg.Notification.MaxLines = Math.Clamp(lines.Value, 1, 30);
            if (position != null) _cfg.Notification.Position = position;
        }

        // ---- theme ----------------------------------------------------------------------------

        /// <summary>Windows' own light/dark, the same registry value App.ApplyTheme reads.</summary>
        static string ResolveTheme(string theme)
        {
            if (theme != "system") return theme == "light" ? "light" : "dark";
            var v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return v is int i && i == 0 ? "dark" : "light";
        }

        void WatchSystemTheme(bool on)
        {
            SystemEvents.UserPreferenceChanged -= OnThemePreferenceChanged; // idempotent: one subscription at most
            if (on) SystemEvents.UserPreferenceChanged += OnThemePreferenceChanged;
        }

        void OnThemePreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (_themeChoice != "system") return;
            string resolved = ResolveTheme("system");
            if (resolved == _cfg.Theme) return;
            _cfg.Theme = resolved;
            OnUi(() => Emit("themeChanged", new { theme = resolved }));
        }

        // ---- hotkey ---------------------------------------------------------------------------

        /// <summary>
        /// Rebinds through the app's own registration so there is still exactly one popup hotkey: the old
        /// window is disposed first, and a combo another app owns brings the old one back instead of leaving
        /// the user without a hotkey. Returns the readable error, or null on success.
        /// </summary>
        /// <remarks>
        /// shortcut: the hotkey windows are private to App.V2.cs, which this slice must not edit, so the
        /// rebind is one reflected call. Swap it for an internal method on App when that file is free.
        /// </remarks>
        string RebindHotkey(string combo)
        {
            var app = System.Windows.Application.Current;
            string[] members = { "_hotkey", "_hotkeyPlain" };
            var method = app?.GetType().GetMethod("RegisterV2Hotkeys", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) return "This build cannot change the hotkey at runtime.";

            string old = _cfg.Hotkey;
            _cfg.Hotkey = combo;
            try
            {
                method.Invoke(app, null);
                if (app.GetType().GetField(members[0], BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(app) != null)
                    return null;

                // The new binding was refused: put the working one back before reporting the conflict.
                _cfg.Hotkey = old;
                method.Invoke(app, null);
                return "Could not register " + combo + ". Another app may own it; kept " + old + ".";
            }
            catch (Exception ex)
            {
                _cfg.Hotkey = old;
                try { method.Invoke(app, null); } catch (Exception) { } // best effort: restore what worked
                return "Could not change the hotkey: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }

        /// <summary>Modifier + key, the shape HotKeyWindow parses; the real test is whether Windows grants it.</summary>
        static bool IsHotkey(string combo)
        {
            bool modifier = false, key = false;
            foreach (var raw in combo.Split('+'))
            {
                string part = raw.Trim();
                if (part.Length == 0) return false;
                switch (part.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                    case "alt":
                    case "shift":
                    case "win":
                        modifier = true;
                        break;
                    default:
                        key = true;
                        break;
                }
            }
            return modifier && key;
        }

        // ---- clear history --------------------------------------------------------------------

        /// <summary>Same scope as the tray dialog: main history only, pinned clips and tab items stay.</summary>
        object ClearHistory(JsonElement p)
        {
            _store.Clear();
            OnUi(() => Emit("cleared", new { }));
            return new { cleared = true };
        }

        // ---- editor save ----------------------------------------------------------------------

        /// <summary>
        /// Stores the edited clip: CF_HTML envelope for paste-back consumers, plain text for search and the
        /// list preview. Refused (nothing written) for an empty, oversized or script-carrying body, and the
        /// store's own preview guard still applies because only a full row is ever loaded here.
        /// </summary>
        object SaveEditor(JsonElement p)
        {
            long id = Long(p, "id");
            string html = String(p, "html");
            string text = String(p, "text");

            if (string.IsNullOrWhiteSpace(html))
                return new { saved = false, error = "The editor is empty, so nothing was saved." };
            if (string.IsNullOrWhiteSpace(text))
                return new { saved = false, error = "The editor has no text, so nothing was saved." };
            if (html.Length > MaxEditorHtml)
                return new { saved = false, error = "The clip is over 2 MB of HTML, so it was not saved." };
            if (html.IndexOf("<script", StringComparison.OrdinalIgnoreCase) >= 0)
                return new { saved = false, error = "The clip contains a script, so it was not saved." };

            var item = ReadFullItem(_dbPath, id);
            if (item == null) return new { saved = false, error = "That clip no longer exists." };
            if (item.IsTruncated)
                return new { saved = false, error = "The full clip could not be loaded, so saving would lose text. Nothing was saved." };

            item.Text = (text ?? "").TrimEnd('\r', '\n');
            item.Html = HtmlFragment.Wrap(html);
            item.Rtf = null; // the old RTF no longer describes the edited body
            if (!_store.Update(item))
                return new { saved = false, error = "The clip could not be saved. Nothing was written." };

            int length = item.Html.Length;
            OnUi(() =>
            {
                var dto = ReadDto(_dbPath, id, _pinnedTabId);
                if (dto != null) Emit("edited", dto);
            });
            return new { saved = true, htmlLength = length };
        }

        // ---- patch readers --------------------------------------------------------------------

        static int? IntOrNull(JsonElement p, string name)
        {
            if (!TryParam(p, name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
            return int.TryParse(v.GetString(), out int n) ? n : throw new ArgumentException(name + " must be a number");
        }

        static bool? BoolOrNull(JsonElement p, string name)
        {
            if (!TryParam(p, name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            throw new ArgumentException(name + " must be true or false");
        }

        /// <summary>A string restricted to a known set; null when the patch omits it, throws when unknown.</summary>
        static string Choice(JsonElement p, string name, string fallback, params string[] allowed)
        {
            string value = String(p, name) ?? fallback;
            if (value == null) return null;
            foreach (var candidate in allowed)
                if (candidate == value) return value;
            throw new ArgumentException(name + " must be one of: " + string.Join(", ", allowed));
        }
    }
}
