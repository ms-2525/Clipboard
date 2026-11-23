using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using Clip.Core;
using Clip.Interop;
using Clip.UI;
using Application = System.Windows.Application;
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
        NotifyIcon _tray;
        MainWindow _main;
        NotificationWindow _notif;
        bool _exiting;

        // Rename hook: every user-visible name follows the assembly name.
        static string Product => Assembly.GetEntryAssembly().GetName().Name;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, $"{Product}.SingleInstance", out bool first);
            if (!first)
            {
                MessageBox.Show($"{Product} is already running.", Product);
                Shutdown();
                return;
            }

            _cfg = Config.Load();
            ApplyTheme(_cfg.Theme);
            Directory.CreateDirectory(Config.DataDir);

            _store = new ClipboardStore(Path.Combine(Config.DataDir, "clip.db"));
            _capture = new CaptureService(_store, _cfg);
            _capture.Captured += OnCaptured;

            _main = new MainWindow(_store, _cfg);
            _main.PasteRequested += OnPasteRequested;
            _main.Closing += (s, ev) => { if (!_exiting) { ev.Cancel = true; _main.Hide(); } };

            _hotkey = new HotKeyWindow(_cfg.Hotkey, _main.Toggle);
            BuildTray();
        }

        void OnCaptured(ClipboardItem item)
        {
            _main.OnNewItem(item);
            if (!_cfg.Notification.Enabled) return;
            _notif?.DismissNow();
            _notif = new NotificationWindow(_cfg);
            _notif.ShowFor(item);
        }

        void OnPasteRequested(ClipboardItem item)
        {
            // MainWindow is already hidden; give Windows a moment to hand focus
            // back to the target app, then set clipboard and send Ctrl+V.
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                _capture.Suppress(PasteInjector.CopyAndPaste(item.Text));
            };
            t.Start();
        }

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, (s, e) => _main.ShowPopup());
            menu.Items.Add(new ToolStripMenuItem("Settings") { Enabled = false }); // M3
            menu.Items.Add("Clear History", null, (s, e) => { _store.Clear(); _main.Reload(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitApp());

            _tray = new NotifyIcon
            {
                Icon = MakeIcon(),
                Text = Product,
                Visible = true,
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += (s, e) => _main.ShowPopup();
        }

        static Icon MakeIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Color.FromArgb(8, 9, 12))) g.FillRectangle(b, 0, 0, 31, 31);
                using (var p = new Pen(Color.FromArgb(255, 138, 61), 2)) g.DrawRectangle(p, 1, 1, 29, 29);
                using var f = new Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
                using var tb = new SolidBrush(Color.FromArgb(255, 138, 61));
                g.DrawString("C", f, tb, 7, 3);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        void ExitApp()
        {
            _exiting = true;
            _hotkey?.Dispose();
            _capture?.Dispose();
            _store?.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            try { _mutex.ReleaseMutex(); } catch { }
            Shutdown();
        }

        void ApplyTheme(string theme)
        {
            if (theme == "system")
            {
                var v = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
                theme = v is int i && i == 0 ? "light" : "dark";
            }
            var name = theme == "light" ? "Light" : "Dark";
            Resources.MergedDictionaries[0] =
                new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Themes/{name}.xaml") };
        }
    }
}
