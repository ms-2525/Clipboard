using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Clip.Interop
{
    /// <summary>Global hotkey on a message-only window.</summary>
    internal sealed class HotKeyWindow : IDisposable
    {
        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8;

        readonly HwndSource _src;
        readonly Action _pressed;

        public HotKeyWindow(string combo, Action pressed)
        {
            _pressed = pressed;
            var p = new HwndSourceParameters("ClipHotkey") { ParentWindow = (IntPtr)(-3) }; // -3 == HWND_MESSAGE
            _src = new HwndSource(p);

            uint mods = 0;
            uint vk = 0;
            foreach (var raw in combo.Split('+'))
            {
                var part = raw.Trim().ToLowerInvariant();
                switch (part)
                {
                    case "alt": mods |= MOD_ALT; break;
                    case "ctrl":
                    case "control": mods |= MOD_CONTROL; break;
                    case "shift": mods |= MOD_SHIFT; break;
                    case "win": mods |= MOD_WIN; break;
                    default:
                        if (Enum.TryParse<System.Windows.Forms.Keys>(part, true, out var k) && k != System.Windows.Forms.Keys.None)
                            vk = (uint)k;
                        break;
                }
            }
            if (vk == 0) { mods = MOD_ALT; vk = 0x56; } // Alt+V

            if (!RegisterHotKey(_src.Handle, 1, mods, vk))
            {
                var product = Assembly.GetEntryAssembly().GetName().Name;
                // Deferred: a modal box here would block OnStartup, so the tray would never be built.
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    System.Windows.MessageBox.Show(
                        $"Could not register the hotkey '{combo}' — another app may own it. {product} will run without it.",
                        product));
            }

            _src.AddHook(Hook);
        }

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt64() == 1)
            {
                _pressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterHotKey(_src.Handle, 1);
            _src.Dispose();
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    }
}
