using System;
using System.Reflection;
using Microsoft.Win32;

namespace Clip.Core
{
    public static class Autostart
    {
        const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

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

                // On .NET, Assembly.Location is the managed .dll; the Run key needs the
                // launchable apphost .exe (Environment.ProcessPath).
                string exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) exe = asm.Location;

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
