using System;
using System.Diagnostics;
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
    /// The tray app: the WebView2 shell is the product — one warm surface, the configured hotkey,
    /// live capture and the single-instance rules. --db &lt;path&gt; redirects the database.
    /// </summary>
    public partial class App
    {
        ShellWindow _shell;
        EventWaitHandle _showSignal;
        string _themeSetting;

        void StartV2Shell(string[] args)
        {
            string dbPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--db", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    dbPath = args[++i];
            }

            // One instance per machine: a second launch surfaces the running window instead of a second listener.
            _mutex = new Mutex(false, $"{Product}.V2");
            if (!Own(_mutex))
            {
                _mutex.Dispose();
                _mutex = null;
                SignalRunningShell();
                Shutdown();
                return;
            }

            _cfg = Config.Load();
            // A fresh install defaults to StartOnLogin but has no Run value yet: write it once so the
            // tray tick and the Settings switch agree from the first right-click.
            if (_cfg.WasNew && _cfg.StartOnLogin) Autostart.Set(true);
            ClipboardItem.ClockFormat = _cfg.TimeFormat == "12h" ? "h:mm tt" : "HH:mm";
            _themeSetting = _cfg.Theme;
            _cfg.Theme = ApplyTheme(_themeSetting); // memory only: the page needs light|dark, never "system"

            string db = Path.GetFullPath(dbPath ?? Path.Combine(Config.DataDir, "clip.db"));
            Directory.CreateDirectory(Path.GetDirectoryName(db));
            _store = new ClipboardStore(db);

            _notif = new NotificationWindow(_cfg);
            // Settings' Preview shows the real toast at the real coordinates, not a picture of one.
            _shell = new ShellWindow(_store, _cfg, db, () => _notif?.ShowFor(new ClipboardItem
            {
                Kind = "text",
                Text = "This is how a copied clip will look. A short copy stays one line; a long one wraps up to your Toast lines setting.",
                AppName = Product,
                WindowTitle = "Notification preview",
                CapturedAt = ClipboardItem.Stamp(),
                SizeBytes = 122
            }));
            _shell.Closed += (s, e) =>
            {
                _store?.Dispose();
                _store = null;
                Release(_mutex);
                Shutdown();
            };

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

            // A damaged data page is only found on the first write: say so once and let the page drop stale rows.
            _store.CorruptionRecovered += () => Dispatcher.BeginInvoke(new Action(() =>
            {
                _notif?.ShowMessage(ClipboardStore.CorruptionNotice);
                _shell?.NotifyCorruption();
            }));

            // The toast pays its first-show cost off the critical path.
            Dispatcher.BeginInvoke(new Action(() => _notif?.WarmHidden()), DispatcherPriority.ApplicationIdle);

            // The constructor already moved a corrupt file aside: the tray must say where the history went.
            if (_store.RecoveredFromCorruption)
                Dispatcher.BeginInvoke(new Action(() => _notif?.ShowMessage(ClipboardStore.CorruptionNotice)),
                    DispatcherPriority.ApplicationIdle);

            // A first launch that lands silently in the tray is a mystery: once the shell is fully
            // loaded, name the hotkey. It fires from Ready, never during startup.
            if (_cfg.WasNew)
                _shell.Ready += () => _notif?.ShowMessage(
                    $"{Product} is running. Press {_cfg.Hotkey} to open the list.");

            RegisterV2Hotkeys();
            ListenForShow();
            WatchTheme();
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
            var menu = Themed(new ContextMenu());
            // Muted header like the reference menu's title row: rebuilt on every open, so never stale.
            menu.Items.Add(Themed(new MenuItem { Header = $"Open: {_cfg.Hotkey}", IsEnabled = false }));
            AddTrayItem(menu, $"{Product}: show/hide", () => _shell?.Toggle());
            AddTrayItem(menu, "Settings\u2026", () => _shell?.ShowForSettings());
            // The tick reads live state: the Run value for login, the config for toasts.
            var notes = Themed(new MenuItem { Header = "Notifications", IsCheckable = true, IsChecked = _cfg.Notification.Enabled });
            notes.Click += (s, e) =>
            {
                _cfg.Notification.Enabled = notes.IsChecked;
                SaveCfg();
            };
            menu.Items.Add(notes);
            // The tick reads the real HKCU Run value, not the config: an external change shows honestly.
            var login = Themed(new MenuItem { Header = "Start on login", IsCheckable = true, IsChecked = Autostart.IsEnabled() });
            login.Click += (s, e) =>
            {
                _cfg.StartOnLogin = login.IsChecked;
                Autostart.Set(login.IsChecked);
                SaveCfg();
            };
            menu.Items.Add(login);
            menu.Items.Add(Themed(new System.Windows.Controls.Separator()));
            AddTrayItem(menu, "Open data folder", () =>
            {
                try { Process.Start(new ProcessStartInfo(Config.DataDir) { UseShellExecute = true }); }
                catch (Exception) { _notif?.ShowMessage("Could not open the data folder."); }
            });
            AddTrayItem(menu, "Exit", ExitV2);
            OpenTrayMenuAtCursor(menu);
        }

        /// <summary>Persists the config without losing a "system" theme choice (same swap as the settings save).</summary>
        void SaveCfg()
        {
            string live = _cfg.Theme;
            _cfg.Theme = _themeSetting;
            _cfg.Save();
            _cfg.Theme = live;
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
            // Light-dismiss: a programmatically opened menu keeps StaysOpen=true, so a click
            // elsewhere or Esc would leave it hanging. False hands dismissal back to WPF.
            menu.StaysOpen = false;
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
