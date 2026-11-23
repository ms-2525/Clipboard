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
            var title = sb.ToString();

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return ("", title);

            if (!Cache.TryGetValue(pid, out var name))
            {
                try { name = Process.GetProcessById((int)pid).ProcessName; }
                catch { name = ""; }
                Cache[pid] = name;
            }
            return (name, title);
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    }
}
