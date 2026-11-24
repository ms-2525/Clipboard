using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Clip.Interop
{
    /// <summary>Global hotkey on a message-only window. Parsing is explicit: Keys' friendly names would map "1" to a mouse button.</summary>
    internal sealed class HotKeyWindow : IDisposable
    {
        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8;

        readonly HwndSource _src;
        readonly Action _pressed;
        readonly bool _maskModifier;

        public bool Registered { get; }
        public string Combo { get; }

        public HotKeyWindow(string combo, Action pressed)
        {
            _pressed = pressed;
            Combo = combo ?? "";
            var p = new HwndSourceParameters("ClipHotkey") { ParentWindow = (IntPtr)(-3) }; // -3 == HWND_MESSAGE
            _src = new HwndSource(p);

            Parse(Combo, out uint mods, out uint vk);
            _maskModifier = (mods & (MOD_ALT | MOD_WIN)) != 0;
            // A modifier-less or key-less binding is a settings error, not something to silently register.
            if (mods != 0 && vk != 0)
                Registered = RegisterHotKey(_src.Handle, 1, mods, vk);

            _src.AddHook(Hook);
        }

        /// <summary>Friendly names Settings emits: A–Z, 0–9, F1–F24 and the OEM key rows (` - = [ ] ; ' , . / \).</summary>
        static void Parse(string combo, out uint mods, out uint vk)
        {
            mods = 0;
            vk = 0;
            if (string.IsNullOrWhiteSpace(combo)) return;

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
                        if (part.Length > 0)
                        {
                            uint mapped = VirtualKey(part);
                            if (mapped != 0) vk = mapped;
                        }
                        break;
                }
            }
        }

        static uint VirtualKey(string part)
        {
            if (part.Length == 1)
            {
                char c = part[0];
                if (c >= 'a' && c <= 'z') return (uint)(0x41 + (c - 'a'));
                if (c >= '0' && c <= '9') return (uint)(0x30 + (c - '0'));
                switch (c)
                {
                    case '`': return 0xC0;  // VK_OEM_3
                    case '-': return 0xBD;  // VK_OEM_MINUS
                    case '=': return 0xBB;  // VK_OEM_PLUS
                    case '[': return 0xDB;  // VK_OEM_4
                    case ']': return 0xDD;  // VK_OEM_6
                    case ';': return 0xBA;  // VK_OEM_1
                    case '\'': return 0xDE; // VK_OEM_7
                    case ',': return 0xBC;  // VK_OEM_COMMA
                    case '.': return 0xBE;  // VK_OEM_PERIOD
                    case '/': return 0xBF;  // VK_OEM_2
                    case '\\': return 0xDC; // VK_OEM_5
                }
                return 0;
            }

            if (part[0] == 'f' && int.TryParse(part.Substring(1), out int fn) && fn >= 1 && fn <= 24)
                return (uint)(0x70 + fn - 1);

            switch (part)
            {
                case "space": return 0x20;
                case "enter":
                case "return": return 0x0D;
                case "tab": return 0x09;
                case "escape":
                case "esc": return 0x1B;
                case "backspace": return 0x08;
                case "insert": return 0x2D;
                case "delete": return 0x2E;
                case "home": return 0x24;
                case "end": return 0x23;
                case "pageup": return 0x21;
                case "pagedown": return 0x22;
                case "left": return 0x25;
                case "up": return 0x26;
                case "right": return 0x27;
                case "down": return 0x28;
                case "printscreen": return 0x2C;
                case "pause": return 0x13;
                case "numlock": return 0x90;
                case "capslock": return 0x14;
            }
            return 0;
        }

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt64() == 1)
            {
                // Before the popup takes focus: the menu Alt is about to open belongs to the window that
                // has focus right now (Alt+V then Down opened that window's system menu).
                if (_maskModifier) PasteInjector.ReleaseHotkeyModifiers();
                _pressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (Registered) UnregisterHotKey(_src.Handle, 1);
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
