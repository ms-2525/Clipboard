using System;
using System.IO;
using System.Reflection;
using Clip.Interop;
using Microsoft.Win32;

namespace Clip.Core
{
    /// <summary>
    /// "Start on login" is one shortcut in the user's own Startup folder, named after the product.
    /// A visible file the owner can find, inspect and delete by hand — a registry Run value cannot be.
    /// </summary>
    public static class Autostart
    {
        // Where the previous release kept the entry; removed once so nobody starts twice.
        const string LegacyRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        static string Product => Assembly.GetEntryAssembly()?.GetName().Name ?? "Clip";

        /// <summary>The one shortcut this app owns: <c>%APPDATA%\...\Startup\&lt;product&gt;.lnk</c>.</summary>
        public static string ShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup), Product + ".lnk");

        /// <summary>
        /// True only when the shortcut exists AND launches this exact exe. Read fresh every time, so the
        /// tray tick and the Settings switch show what Windows will really do; a shortcut left pointing at
        /// an older copy of the exe reads as off, because at login it would start that other copy.
        /// </summary>
        public static bool IsEnabled()
        {
            try
            {
                string target = ShortcutLink.TargetOf(ShortcutPath);
                string exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(target)) return false;
                if (string.IsNullOrEmpty(exe)) return true; // a shortcut exists; nothing to compare it with
                return string.Equals(target, exe, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false; // a settings tick must never throw; treat unreadable as off
            }
        }

        /// <summary>Creates or deletes the shortcut. A toggle must never crash the app.</summary>
        public static void Set(bool enable)
        {
            try
            {
                // ProcessPath is the launchable .exe; Assembly.Location is empty in a
                // single-file build, so there is no useful fallback.
                string exe = Environment.ProcessPath;
                if (enable)
                {
                    if (string.IsNullOrEmpty(exe)) return;
                    Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath));
                    ShortcutLink.Create(ShortcutPath, exe);
                }
                else
                {
                    ShortcutLink.Delete(ShortcutPath);
                }
            }
            catch (Exception)
            {
                // A settings toggle must never crash the app; leave the folder as it was.
            }
        }

        /// <summary>
        /// One-way move off the old registry entry: if it exists it was the user's "on", so the shortcut is
        /// written first and the value deleted second — at no point is neither in place. Idempotent: with no
        /// value left it does nothing (a user who turned it off stays off).
        /// </summary>
        public static void MigrateLegacyRunEntry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(LegacyRunKey, true))
                {
                    if (key?.GetValue(Product) == null) return;

                    // Only claim "on" when a usable exe path exists to point the shortcut at.
                    if (!string.IsNullOrEmpty(Environment.ProcessPath)) Set(true);
                    key.DeleteValue(Product, false);
                }
            }
            catch (Exception)
            {
                // Locked registry or no write access: the old value stays and the app still starts.
                // The next launch retries; nothing here justifies blocking startup.
            }
        }
    }
}
