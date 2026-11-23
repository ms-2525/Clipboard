using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Clip.Interop
{
    /// <summary>Message-only window that fires on every clipboard change (event driven, no polling).</summary>
    internal sealed class ClipboardListener : IDisposable
    {
        const int WM_CLIPBOARDUPDATE = 0x031D;

        readonly HwndSource _src;

        public event Action Changed;

        public ClipboardListener()
        {
            var p = new HwndSourceParameters("ClipListener") { ParentWindow = (IntPtr)(-3) }; // -3 == HWND_MESSAGE
            _src = new HwndSource(p);
            _src.AddHook(Hook);
            AddClipboardFormatListener(_src.Handle);
        }

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE) Changed?.Invoke();
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            RemoveClipboardFormatListener(_src.Handle);
            _src.Dispose();
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    }
}
