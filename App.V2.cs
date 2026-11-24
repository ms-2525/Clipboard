using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Clip.Core;
using Clip.Host;
using Clip.Interop;
using Clip.UI;
using H.NotifyIcon;
using Microsoft.Win32;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Clip
{
    /// <summary>
    /// The v2 tray app. A plain launch (or --v2, kept as an alias) runs the WebView2 shell as the product:
    /// one warm surface, the configured hotkey, live capture and the single-instance rules. --legacy opens
    /// the old WPF UI for this release only; --db &lt;path&gt; redirects the database.
    /// </summary>
    public partial class App
    {
        ShellWindow _shell;
        EventWaitHandle _showSignal;
        Mutex _legacyMutex;
        string _themeSetting;

        bool StartV2Shell(string[] args)
        {
            // --v2 is still accepted and ignored: it is the default now.
            bool legacy = false;
            string dbPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--legacy", StringComparison.OrdinalIgnoreCase)) legacy = true;
                else if (string.Equals(args[i], "--db", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    dbPath = args[++i];
            }
            if (legacy) return false;

            // One v2 per machine: a second launch surfaces the running window instead of a second listener.
            _mutex = new Mutex(false, $"{Product}.V2");
            if (!Own(_mutex))
            {
                _mutex.Dispose();
                _mutex = null;
                SignalRunningShell();
                Shutdown();
                return true;
            }

            // The old app owns the same clipboard: two listeners on one machine would capture everything twice.
            _legacyMutex = new Mutex(false, $"{Product}.SingleInstance");
            if (!Own(_legacyMutex))
            {
                _legacyMutex.Dispose();
                _legacyMutex = null;
                MessageBox.Show(
                    $"{Product} is already running from its tray icon. Exit it there, then start {Product} again.",
                    Product, MessageBoxButton.OK, MessageBoxImage.Information);
                Release(_mutex);
                Shutdown();
                return true;
            }

            _cfg = Config.Load();
            _themeSetting = _cfg.Theme;
            _cfg.Theme = ApplyTheme(_themeSetting); // memory only: the page needs light|dark, never "system"

            string db = Path.GetFullPath(dbPath ?? Path.Combine(Config.DataDir, "clip.db"));
            Directory.CreateDirectory(Path.GetDirectoryName(db));
            _store = new ClipboardStore(db);

            _shell = new ShellWindow(_store, _cfg, db);
            _shell.Closed += (s, e) =>
            {
                _store?.Dispose();
                _store = null;
                Release(_mutex);
                Release(_legacyMutex);
                Shutdown();
            };

            _notif = new NotificationWindow(_cfg);
            BuildV2Tray();
            _shell.Warm();

            _capture = new CaptureService(_store, _cfg);
            _capture.Captured += item =>
            {
                _shell?.NotifyCaptured(item);
                if (_cfg.Notification.Enabled) _notif?.ShowFor(item);
            };
            _capture.Skipped += reason =>
            {
                _shell?.NotifySkipped(reason);
                if (_cfg.Notification.Enabled)
                    _notif?.ShowMessage((reason ?? "").Replace("Image skipped:", "Image not saved:"));
            };
            // A paste Clip injects is Clip's own clipboard write, not a new copy.
            ClipHost.SuppressCapture = seq => _capture?.Suppress(seq);

            // The toast pays its first-show cost off the critical path, exactly like the old app.
            Dispatcher.BeginInvoke(new Action(() => _notif?.WarmHidden()), DispatcherPriority.ApplicationIdle);

            RegisterV2Hotkeys();
            ListenForShow();
            WatchTheme();
            return true;
        }

        /// <summary>Owns a named mutex; an abandoned one belonged to a crash, so this process takes it over.</summary>
        static bool Own(Mutex mutex)
        {
            try { return mutex.WaitOne(0); }
            catch (AbandonedMutexException) { return true; }
        }

        static void Release(Mutex mutex)
        {
            if (mutex == null) return;
            try { mutex.ReleaseMutex(); } catch { } // a mutex this process does not own: nothing to release
            mutex.Dispose();
        }

        /// <summary>A second launch pokes the running instance's listener; the window comes forward there.</summary>
        static void SignalRunningShell()
        {
            for (int i = 0; i < 10; i++)
            {
                if (EventWaitHandle.TryOpenExisting($"{Product}.V2.Show", out var signal))
                {
                    using (signal) signal.Set();
                    return;
                }
                Thread.Sleep(50); // the running instance may still be creating its listener
            }
        }

        void ListenForShow()
        {
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $"{Product}.V2.Show");
            var thread = new Thread(() =>
            {
                while (_showSignal.WaitOne())
                {
                    try { Dispatcher.BeginInvoke(new Action(() => _shell?.ShowShell())); }
                    catch (InvalidOperationException) { return; } // shutting down
                }
            })
            { IsBackground = true, Name = "Clip v2 show" };
            thread.Start();
        }

        void RegisterV2Hotkeys()
        {
            _hotkey?.Dispose();
            _hotkeyPlain?.Dispose();
            _hotkey = null;
            _hotkeyPlain = null;

            var popup = new HotKeyWindow(_cfg.Hotkey, () => _shell?.Toggle());
            var plain = new HotKeyWindow(_cfg.HotkeyPlain, () => _shell?.ShowPlain());
            // Each binding reports its own failure: a taken Ctrl+Shift+V must never disable Alt+V.
            if (popup.Registered) _hotkey = popup;
            else
            {
                _shell?.NotifyHotkeyFailed(_cfg.Hotkey);
                _notif?.ShowMessage($"Could not register {_cfg.Hotkey}. Another app may own it.");
                popup.Dispose();
            }
            if (plain.Registered) _hotkeyPlain = plain;
            else
            {
                _shell?.NotifyHotkeyFailed(_cfg.HotkeyPlain);
                _notif?.ShowMessage($"Could not register {_cfg.HotkeyPlain}. Another app may own it.");
                plain.Dispose();
            }
        }

        void BuildV2Tray()
        {
            _tray = new TaskbarIcon
            {
                Icon = TrayIcon.Create(),
                ToolTipText = $"{Product}: clipboard history"
            };
            _tray.TrayLeftMouseUp += (s, e) => _shell?.Toggle();
            _tray.TrayMouseDoubleClick += (s, e) => _shell?.Toggle();
            _tray.TrayRightMouseUp += (s, e) => OpenV2TrayMenu();
            _tray.TrayKeyboardContextMenu += (s, e) => OpenV2TrayMenu();
            _tray.ForceCreate();
        }

        void OpenV2TrayMenu()
        {
            var menu = new ContextMenu();
            AddTrayItem(menu, $"{Product}: show/hide", () => _shell?.Toggle());
            AddTrayItem(menu, "Settings\u2026", () => _shell?.ShowForSettings());
            AddTrayItem(menu, "Clear history\u2026", () => _shell?.ConfirmClearHistory());
            menu.Items.Add(new System.Windows.Controls.Separator());
            AddTrayItem(menu, "Exit", ExitV2);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(new MenuItem
            {
                Header = $"{Product} {Assembly.GetEntryAssembly().GetName().Version?.ToString(3)}",
                IsEnabled = false
            });
            OpenTrayMenuAtCursor(menu);
        }

        /// <summary>Both trays open on the cursor pixel scaled to DIP; Win11 refused H.NotifyIcon's own routing.</summary>
        void OpenTrayMenuAtCursor(ContextMenu menu)
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(cursor);
            double scale = ScreenDpi.Scale(screen);
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
            menu.HorizontalOffset = cursor.X / scale;
            menu.VerticalOffset = cursor.Y / scale;
            menu.IsOpen = true;
        }

        /// <summary>Follows Windows light/dark while the setting is "system"; the page hears only real changes.</summary>
        void WatchTheme()
        {
            if (_themeSetting == "system") SystemEvents.UserPreferenceChanged += OnThemePreferenceChanged;
        }

        void OnThemePreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string resolved = ApplyTheme(_themeSetting);
                if (resolved == _cfg.Theme) return;
                _cfg.Theme = resolved;
                _shell?.NotifyThemeChanged(resolved);
            }));
        }

        void ExitV2()
        {
            _exiting = true;
            if (_themeSetting == "system") SystemEvents.UserPreferenceChanged -= OnThemePreferenceChanged;
            _hotkey?.Dispose();
            _hotkey = null;
            _hotkeyPlain?.Dispose();
            _hotkeyPlain = null;
            _capture?.Dispose();
            _capture = null;
            _tray?.Dispose();
            _tray = null;
            _notif?.Close();
            _notif = null;
            _shell?.AllowClose();
            _shell?.Close(); // Closed: store, mutexes and Shutdown; one path for every exit
        }
    }
}
