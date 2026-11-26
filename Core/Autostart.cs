using System;
using System.Reflection;
using Microsoft.Win32;

namespace Clip.Core
{
    public static class Autostart
    {
        const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>True when the HKCU Run value points at this exe (quotes tolerated).</summary>
        public static bool IsEnabled()
        {
            try
            {
                var asm = Assembly.GetEntryAssembly();
                if (asm == null) return false;
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                string value = key?.GetValue(asm.GetName().Name) as string;
                if (string.IsNullOrEmpty(value)) return false;
                string exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return true; // a value exists; nothing to compare it with
                return string.Equals(value.Trim('"'), exe, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false; // a settings tick must never throw; treat unreadable as off
            }
        }

        public static void Set(bool enable)
        {
            try
            {
                var asm = Assembly.GetEntryAssembly();
                if (asm == null) return;
                string name = asm.GetName().Name;

                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                                ?? Registry.CurrentUser.CreateSubKey(RunKey);
                if (key == null) return;

                // ProcessPath is the launchable .exe; Assembly.Location is empty in a
                // single-file build, so there is no useful fallback.
                string exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return;

                if (enable) key.SetValue(name, exe);
                else key.DeleteValue(name, false); // missing value is fine
            }
            catch
            {
                // A settings toggle must never crash the app; leave the registry as it was.
            }
        }
    }
}
