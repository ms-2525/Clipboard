using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Clip.Interop
{
    internal static class ForegroundApp
    {
        static readonly Dictionary<uint, string> Cache = new Dictionary<uint, string>();

        /// <summary>Process name and window title of the app that currently owns the screen.</summary>
        public static (string app, string title) Get()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return ("", "");

            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, sb.Capacity);

            GetWindowThreadProcessId(hwnd, out uint pid);
            return Attribute(sb.ToString(), pid, Environment.ProcessId);
        }

        /// <summary>
        /// Pid → (app, title). Clip's own windows are never attributed as the source: a copy made from the popup or
        /// the hotkey host would otherwise be recorded as "Clip · ClipHotkey". Product name only, no internal names.
        /// </summary>
        internal static (string app, string title) Attribute(string windowTitle, uint pid, int selfPid)
        {
            if (pid == 0) return ("", windowTitle);
            if (pid == (uint)selfPid) return (Process.GetCurrentProcess().ProcessName, "");

            if (!Cache.TryGetValue(pid, out var name))
            {
                try { name = Process.GetProcessById((int)pid).ProcessName; }
                catch { name = ""; } // the process exited between the clipboard change and this read
                Cache[pid] = name;
            }
            return (name, windowTitle);
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool BringWindowToTop(IntPtr hwnd);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hwnd, int command);

        const int SW_RESTORE = 9;

        /// <summary>
        /// Forces <paramref name="hwnd"/> to the foreground. The attach is to the thread that owns the
        /// foreground, not to the target: that is what makes SetForegroundWindow succeed while another app
        /// is active. Without it a hotkey that arrives on a hidden message window shows the popup behind the
        /// current app, every keystroke keeps going to that app, and the popup looks dead.
        /// </summary>
        internal static bool Force(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            if (GetForegroundWindow() == hwnd) return true;

            IntPtr foreground = GetForegroundWindow();
            uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
            uint self = GetCurrentThreadId();
            bool attached = foregroundThread != 0 && foregroundThread != self
                && AttachThreadInput(self, foregroundThread, true);
            try
            {
                ShowWindow(hwnd, SW_RESTORE);
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached) AttachThreadInput(self, foregroundThread, false);
            }
            return GetForegroundWindow() == hwnd;
        }
    }
}
