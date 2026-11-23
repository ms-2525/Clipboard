using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clip.Core;
using Clip.Interop;
using Clip.UI;
using H.NotifyIcon;
using Microsoft.Win32;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;

namespace Clip
{
    public partial class App : Application
    {
        Mutex _mutex;
        Config _cfg;
        ClipboardStore _store;
        CaptureService _capture;
        HotKeyWindow _hotkey;
        HotKeyWindow _hotkeyPlain;
        TaskbarIcon _tray;
        MainWindow _main;
        NotificationWindow _notif;
        SettingsWindow _settings;
        bool _exiting;

        // Rename hook: every user-visible name follows the assembly name.
        static string Product => Assembly.GetEntryAssembly().GetName().Name;

        protected override void OnStartup(StartupEventArgs e)
        {
            // RtfPipe needs the legacy code pages on .NET; before any RTF render.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            LogFatalErrors();

            _mutex = new Mutex(true, $"{Product}.SingleInstance", out bool first);
            if (!first)
            {
                MessageBox.Show($"{Product} is already running. Open it from the notification area.", Product);
                Shutdown();
                return;
            }

            _cfg = Config.Load();
            ApplyTheme(_cfg.Theme);
            SetClockFormat();
            Directory.CreateDirectory(Config.DataDir);

            _store = new ClipboardStore(System.IO.Path.Combine(Config.DataDir, "clip.db"));
            ImageCache.Cleanup();

            _notif = new NotificationWindow(_cfg);

            _capture = new CaptureService(_store, _cfg);
            _capture.Captured += OnCaptured;
            _capture.Skipped += OnSkipped;

            _main = new MainWindow(_store, _cfg);
            _main.PasteRequested += OnPasteRequested;
            _main.PlainPasteRequested += OnPlainPasteRequested;
            _main.CopyRequested += OnCopyRequested;
            _main.Closing += (s, ev) => { if (!_exiting) { ev.Cancel = true; _main.Hide(); } };

            string failure = RegisterHotkeys();
            BuildTray();

            // Warm the row renderer off the critical path: the first Alt+V must not pay its JIT.
            // (An offscreen pre-show of the whole popup was measured too: it moved no cold-open time
            // and kept a whole render target resident, so idle RAM paid for nothing.)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RichRow.Warm();
                _main.Warm();
                _notif.WarmHidden();
            }), DispatcherPriority.ApplicationIdle);

            if (_cfg.WasNew)
                Dispatcher.BeginInvoke(new Action(() =>
                    _notif.ShowMessage($"{Product} is running. Press {_cfg.Hotkey} to open the list.")));
            if (_store.RecoveredFromCorruption)
                _notif.ShowMessage("Clip history could not be read and was moved aside. A fresh history has started.");
            if (failure != null)
                _notif.ShowMessage($"Could not register {failure}. Another app may own it.");
        }

        // ---- capture ------------------------------------------------------

        /// <summary>
        /// The process vanishing without a dialog was reported twice. There is no crash handler in a WPF
        /// tray app by default, so the next unexplained exit leaves a one-line reason in crash.log.
        /// </summary>
        void LogFatalErrors()
        {
            DispatcherUnhandledException += (s, e) => LogCrash("dispatcher", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash("appdomain", e.ExceptionObject as Exception);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) => LogCrash("task", e.Exception);
        }

        static void LogCrash(string source, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Config.DataDir);
                File.AppendAllText(Path.Combine(Config.DataDir, "crash.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (IOException) { } // a crash log that itself crashes is worse than no crash log
            catch (UnauthorizedAccessException) { }
        }

        void OnCaptured(ClipboardItem item)
        {
            _main.OnNewItem(item);
            if (!_cfg.Notification.Enabled) return;
            _notif.ShowFor(item);
        }

        void OnSkipped(string reason)
        {
            if (!_cfg.Notification.Enabled) return;
            _notif.ShowMessage((reason ?? "").Replace("Image skipped:", "Image not saved:"));
        }

        // ---- paste --------------------------------------------------------

        void OnPasteRequested(ClipboardItem item)
        {
            AfterHide(100, () =>
            {
                LoadForPaste(item);
                _capture.Suppress(PasteInjector.CopyAndPaste(item));
            });
        }

        void OnPlainPasteRequested(ClipboardItem item)
        {
            AfterHide(100, () => _capture.Suppress(PasteInjector.CopyAndPastePlain(item)));
        }

        /// <summary>Ctrl+C: put the selected item back on the clipboard without injecting a paste.</summary>
        void OnCopyRequested(ClipboardItem item)
        {
            LoadForPaste(item);
            if (item.Kind == "image" && item.Image != null)
            {
                PasteInjector.SetImageClipboard(item.Image);
            }
            else if (item.Formats != null && item.Formats.Count > 0)
            {
                ClipboardFormats.Restore(item.Formats);
            }
            else if (!string.IsNullOrEmpty(item.Rtf) || !string.IsNullOrEmpty(item.Html))
            {
                var data = new DataObject();
                data.SetData(DataFormats.UnicodeText, item.Text ?? "");
                if (!string.IsNullOrEmpty(item.Rtf)) data.SetData(DataFormats.Rtf, item.Rtf);
                if (!string.IsNullOrEmpty(item.Html)) data.SetData(DataFormats.Html, item.Html);
                ClipboardFormats.WithClipboardRetry(() => Clipboard.SetDataObject(data, true));
            }
            else
            {
                ClipboardFormats.WithClipboardRetry(() => Clipboard.SetText(item.Text ?? ""));
            }
            _capture.Suppress(GetClipboardSequenceNumber());
        }

        /// <summary>The list row carries no blobs; load exactly what the paste path needs.</summary>
        void LoadForPaste(ClipboardItem item)
        {
            if (item == null) return;
            if (item.Kind == "image")
            {
                var image = _store.GetImage(item.Id);
                if (image?.Image != null) item.Image = image.Image;
            }
            item.Formats = _store.GetFormats(item.Id);
            var rich = _store.GetRich(item.Id);
            if (rich != null)
            {
                item.Html = rich.Html;
                item.Rtf = rich.Rtf;
            }
        }

        void AfterHide(int delayMs, Action action)
        {
            // Give Windows a moment to give focus back to the target app before injecting.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                action();
            };
            timer.Start();
        }

        // ---- global plain-paste hotkey -------------------------------------

        void OnPlainHotkey()
        {
            ClipboardItem item = _main.IsVisible ? _main.SelectedItem : null;
            if (item == null)
                item = _store.GetRecent(_cfg.MaxItems).FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Text));
            if (item == null) return;

            if (_main.IsVisible) _main.HideForPaste();
            AfterHide(100, () => _capture.Suppress(PasteInjector.CopyAndPastePlain(item)));
        }

        // ---- hotkeys -------------------------------------------------------

        /// <summary>Registers both bindings; returns the failed combo, or null. Never leaves one window dangling.</summary>
        string RegisterHotkeys()
        {
            _hotkey?.Dispose();
            _hotkeyPlain?.Dispose();
            _hotkey = null;
            _hotkeyPlain = null;

            var popup = new HotKeyWindow(_cfg.Hotkey, _main.Toggle);
            var plain = new HotKeyWindow(_cfg.HotkeyPlain, OnPlainHotkey);
            if (popup.Registered && plain.Registered)
            {
                _hotkey = popup;
                _hotkeyPlain = plain;
                return null;
            }

            string failed = !popup.Registered ? _cfg.Hotkey : _cfg.HotkeyPlain;
            popup.Dispose();
            plain.Dispose();
            return failed;
        }

        void OnSettingsSaved(Config cfg)
        {
            ApplyTheme(cfg.Theme);
            SetClockFormat();

            string oldPopup = _hotkey?.Combo;
            string oldPlain = _hotkeyPlain?.Combo;
            string failed = RegisterHotkeys();

            if (failed != null && oldPopup != null)
            {
                // Revert to the bindings that were working and say so; never leave the app hotkey-less.
                _cfg.Hotkey = oldPopup;
                _cfg.HotkeyPlain = oldPlain;
                _cfg.Save();
                RegisterHotkeys();
                _notif.ShowMessage($"Could not register {failed}. Another app may own it. Kept {oldPopup}.");
            }
            else if (failed != null)
            {
                _notif.ShowMessage($"Could not register {failed}. Another app may own it.");
            }

            _main.Reload();
        }

        void SetClockFormat() => ClipboardItem.ClockFormat = _cfg.TimeFormat == "12h" ? "h:mm tt" : "HH:mm";

        // ---- tray ----------------------------------------------------------

        void BuildTray()
        {
            _tray = new TaskbarIcon
            {
                Icon = DrawTrayIcon(),
                ToolTipText = $"{Product}: clipboard history"
            };
            _tray.TrayLeftMouseUp += (s, e) => _main.ShowPopup();
            _tray.TrayMouseDoubleClick += (s, e) => _main.ShowPopup();
            _tray.TrayRightMouseUp += (s, e) => OpenTrayMenu();
            _tray.TrayKeyboardContextMenu += (s, e) => OpenTrayMenu();
            _tray.ForceCreate();
        }

        /// <summary>Drawn clipboard mark (same shape as the UI icon), monochrome against the taskbar theme.</summary>
        static System.Drawing.Icon DrawTrayIcon()
        {
            var ink = IsLightTaskbar()
                ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20)
                : System.Drawing.Color.FromArgb(0xE8, 0xE8, 0xE8);
            using var bitmap = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);
                using var pen = new System.Drawing.Pen(ink, 2.4f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round,
                    LineJoin = System.Drawing.Drawing2D.LineJoin.Round
                };
                g.DrawRectangle(pen, 6, 8, 20, 19);
                g.DrawRectangle(pen, 12, 4, 8, 5);
                g.DrawLine(pen, 11, 15, 21, 15);
                g.DrawLine(pen, 11, 20, 17, 20);
            }

            // Clone so the managed icon owns its copy, then release the GDI handle.
            IntPtr handle = bitmap.GetHicon();
            using var temp = System.Drawing.Icon.FromHandle(handle);
            var icon = (System.Drawing.Icon)temp.Clone();
            DestroyIcon(handle);
            return icon;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr handle);

        static bool IsLightTaskbar()
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "SystemUsesLightTheme", 0);
            return value is int i && i == 1;
        }

        /// <summary>Opened by hand: H.NotifyIcon's own ShowContextMenu routing failed on Win11 (spike).</summary>
        void OpenTrayMenu()
        {
            // No properties set here: UI\Themes\Popups.xaml styles every ContextMenu and MenuItem.
            var menu = new ContextMenu();

            AddTrayItem(menu, $"Open {Product}", () => _main.ShowPopup());
            AddTrayItem(menu, "Settings\u2026", () => OpenSettings(false));
            AddTrayItem(menu, "Clear history\u2026", () => OpenSettings(true));
            menu.Items.Add(new System.Windows.Controls.Separator());
            AddTrayItem(menu, "Exit", ExitApp);

            var cursor = System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(cursor);
            double scale = ScreenDpi.Scale(screen);
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
            menu.HorizontalOffset = cursor.X / scale;
            menu.VerticalOffset = cursor.Y / scale;
            menu.IsOpen = true;
        }

        void AddTrayItem(ContextMenu menu, string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }

        // ---- settings ------------------------------------------------------

        void OpenSettings(bool showClearConfirm)
        {
            if (_settings != null && _settings.IsVisible)
            {
                _settings.Activate();
                return;
            }

            var window = new SettingsWindow(_cfg);
            _settings = window;
            window.SettingsSaved += OnSettingsSaved;
            window.ClearHistoryRequested += () =>
            {
                _store.Clear();
                _main.Reload();
            };
            window.Closed += (s, e) => _settings = null;
            window.Show();

            if (showClearConfirm)
            {
                // Same inline two-click swap the user would reach by hand; no silent wipe from the tray.
                window.ClearIdle.Visibility = Visibility.Collapsed;
                window.ClearConfirm.Visibility = Visibility.Visible;
            }
        }

        // ---- theme / exit --------------------------------------------------

        void ApplyTheme(string theme)
        {
            if (theme == "system")
            {
                var v = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
                theme = v is int i && i == 0 ? "light" : "dark";
            }
            var name = theme == "light" ? "Light" : "Dark";
            Resources.MergedDictionaries[0] =
                new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Themes/{name}.xaml") };
        }

        void ExitApp()
        {
            _exiting = true;
            _hotkey?.Dispose();
            _hotkeyPlain?.Dispose();
            _capture?.Dispose();
            _tray?.Dispose();
            _settings?.Close();
            _notif?.Close();
            _store?.Dispose();
            try { _mutex.ReleaseMutex(); } catch { } // first instance only
            Shutdown();
        }

        [DllImport("user32.dll")]
        static extern uint GetClipboardSequenceNumber();
    }
}
