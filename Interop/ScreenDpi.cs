using System;
using System.Runtime.InteropServices;

namespace Clip.Interop
{
    internal static class ScreenDpi
    {
        // Physical pixels per WPF device-independent unit on that monitor.
        public static double Scale(System.Windows.Forms.Screen s)
        {
            var pt = new POINT
            {
                X = s.Bounds.Left + s.Bounds.Width / 2,
                Y = s.Bounds.Top + s.Bounds.Height / 2
            };
            var hmon = MonitorFromPoint(pt, 2 /* MONITOR_DEFAULTTONEAREST */);
            if (hmon == IntPtr.Zero) return 1.0;
            if (GetDpiForMonitor(hmon, 0 /* MDT_EFFECTIVE_DPI */, out uint dpiX, out _) != 0) return 1.0;
            return dpiX / 96.0;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

        [DllImport("shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr hmon, int dpiType, out uint dpiX, out uint dpiY);
    }
}
