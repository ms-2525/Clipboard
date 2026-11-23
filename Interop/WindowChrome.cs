using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clip.Interop
{
    /// <summary>
    /// Windows 11 chrome for an OPAQUE borderless window. A layered window (AllowsTransparency)
    /// loses ClearType on every glyph, so the rounded corner and the drop shadow come from DWM here.
    /// </summary>
    internal static class WindowChrome
    {
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWCP_ROUND = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct MARGINS
        {
            public int Left, Right, Top, Bottom;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("dwmapi.dll")]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x80;

        /// <summary>The OS rounds the window at 8 DIP; the DWM frame supplies the shadow a no-chrome window lacks.</summary>
        public static void Apply(Window window)
        {
            var h = new WindowInteropHelper(window).Handle;
            if (h == IntPtr.Zero) return;

            // A borderless window with no owner lands in Alt-Tab unless it says otherwise.
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);

            int round = DWMWCP_ROUND;
            DwmSetWindowAttribute(h, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

            // A 1 px frame is the trigger DWM needs to draw a shadow around a WS_POPUP window.
            var margin = new MARGINS { Left = 1, Right = 1, Top = 1, Bottom = 1 };
            DwmExtendFrameIntoClientArea(h, ref margin);
        }
    }
}
