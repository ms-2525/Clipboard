using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Clip.Interop
{
    /// <summary>
    /// The one Action Center notice ("Clip is running"): every other notice goes through our own
    /// toast, but the first-run hello must survive the page failing to load, so it uses the Win32
    /// tray balloon instead. No new dependency and no shortcut registration: a transient icon
    /// carries the balloon, then deletes itself. Call and forget from the UI thread.
    /// </summary>
    internal static class NativeToast
    {
        const int NIM_ADD = 0;
        const int NIM_DELETE = 2;
        const int NIF_ICON = 0x2;
        const int NIF_TIP = 0x4;
        const int NIF_INFO = 0x10;
        const int NIIF_INFO = 0x1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        sealed class BalloonWindow : System.Windows.Forms.NativeWindow, IDisposable
        {
            public BalloonWindow() => CreateHandle(new System.Windows.Forms.CreateParams());
            public void Dispose() => DestroyHandle();
        }

        static BalloonWindow _window;
        static Icon _icon;
        static System.Windows.Threading.DispatcherTimer _timer;

        public static void Show(string title, string message)
        {
            try
            {
                Hide(); // a pending hello is replaced, never stacked
                _icon = TrayIcon.Create();
                _window = new BalloonWindow();
                var data = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _window.Handle,
                    uID = 1,
                    uFlags = NIF_ICON | NIF_TIP | NIF_INFO,
                    hIcon = _icon.Handle,
                    szTip = title,
                    szInfo = message,
                    szInfoTitle = title,
                    dwInfoFlags = NIIF_INFO
                };
                if (!Shell_NotifyIcon(NIM_ADD, ref data)) { Hide(); return; }
                // The balloon lives in Action Center after it fades; the carrier icon is
                // deleted after its time so no ghost icon lingers in the tray.
                _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                _timer.Tick += (s, e) => Hide();
                _timer.Start();
            }
            catch { Hide(); } // a hello must never crash the app or leak an icon
        }

        public static void Hide()
        {
            try
            {
                _timer?.Stop();
                _timer = null;
                if (_window != null)
                {
                    var data = new NOTIFYICONDATA
                    {
                        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                        hWnd = _window.Handle,
                        uID = 1
                    };
                    Shell_NotifyIcon(NIM_DELETE, ref data);
                    _window.Dispose();
                    _window = null;
                }
                _icon?.Dispose();
                _icon = null;
            }
            catch { } // teardown, same rule: never throw
        }
    }
}
