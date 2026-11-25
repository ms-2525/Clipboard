using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using Clip.Core;
using Clip.Interop;
using Clip.UI;
using H.NotifyIcon;
using Microsoft.Win32;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Clip
{
    public partial class App : Application
    {
        // Shared with App.V2.cs: one startup path, one store, one tray.
        Mutex _mutex;
        Config _cfg;
        ClipboardStore _store;
        CaptureService _capture;
        HotKeyWindow _hotkey;
        HotKeyWindow _hotkeyPlain;
        TaskbarIcon _tray;
        NotificationWindow _notif;

        // Rename hook: every user-visible name follows the assembly name.
        static string Product => Assembly.GetEntryAssembly().GetName().Name;

        protected override void OnStartup(StartupEventArgs e)
        {
            // RTF byte envelopes need the legacy code pages on .NET; register before any RTF render.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            LogFatalErrors();
            StartV2Shell(e.Args);
        }

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
            catch (UnauthorizedAccessException) { } // same reason: a failed log must never throw
        }

        /// <summary>Applies the configured theme and returns the resolved "light"/"dark".</summary>
        string ApplyTheme(string theme)
        {
            theme = ResolveTheme(theme, Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1));
            var name = theme == "light" ? "Light" : "Dark";
            Resources.MergedDictionaries[0] =
                new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Themes/{name}.xaml") };
            return theme;
        }

        /// <summary>
        /// The one polarity (HKCU AppsUseLightTheme: 0 = dark, 1 = light); the value is passed in so both
        /// branches are provable without changing the user's Windows theme. The web host calls this too.
        /// </summary>
        internal static string ResolveTheme(string choice, object appsUseLightTheme)
        {
            if (choice != "system") return choice == "light" ? "light" : "dark";
            return appsUseLightTheme is int i && i == 0 ? "dark" : "light";
        }

        /// <summary>
        /// A theme saved from the web settings: the raw choice drives the WPF dictionaries and the
        /// Windows watcher, while the page hears the resolved mode from the host's reply and event.
        /// </summary>
        internal string ApplyThemeFromSettings(string choice)
        {
            SystemEvents.UserPreferenceChanged -= OnThemePreferenceChanged;
            _themeSetting = choice; // the watcher and ExitV2 both read the raw choice
            if (choice == "system") SystemEvents.UserPreferenceChanged += OnThemePreferenceChanged;
            return ApplyTheme(choice);
        }

        /// <summary>
        /// An implicitly styled popup only finds UI\Themes\Popups.xaml once it is in a tree; the tray
        /// menu is created unparented, so the style is applied by hand. Keeps stock WPF chrome out.
        /// </summary>
        static T Themed<T>(T element) where T : FrameworkElement
        {
            if (Application.Current?.TryFindResource(typeof(T)) is Style style) element.Style = style;
            return element;
        }

        void AddTrayItem(ContextMenu menu, string header, Action action)
        {
            var item = Themed(new MenuItem { Header = header });
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }
    }
}
