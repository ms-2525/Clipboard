using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Clip.Interop
{
    /// <summary>
    /// The two shell calls the Startup folder needs: write a .lnk that launches one exe, and read
    /// back which exe a .lnk launches. A login start passes no arguments and needs no icon.
    /// </summary>
    internal static class ShortcutLink
    {
        /// <summary>Creates or overwrites the shortcut at <paramref name="linkPath"/>.</summary>
        public static void Create(string linkPath, string targetPath)
        {
            var link = (IShellLinkW)new ShellLink();
            try
            {
                link.SetPath(targetPath);
                // Windows shows this in the shortcut's Properties; the app itself works from absolute paths.
                link.SetWorkingDirectory(Path.GetDirectoryName(targetPath));
                ((IPersistFile)link).Save(linkPath, true);
            }
            finally
            {
                Marshal.FinalReleaseComObject(link);
            }
        }

        /// <summary>The exe a shortcut launches, or null when the file is missing or not a shortcut.</summary>
        public static string TargetOf(string linkPath)
        {
            if (!File.Exists(linkPath)) return null;
            var link = (IShellLinkW)new ShellLink();
            try
            {
                ((IPersistFile)link).Load(linkPath, 0 /* STGM_READ */);
                var buffer = new StringBuilder(260); // MAX_PATH: a launchable exe path
                link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0 /* SLGP_UNCPRIORITY */);
                string target = buffer.ToString();
                return string.IsNullOrEmpty(target) ? null : target;
            }
            catch (COMException)
            {
                return null; // a damaged or foreign .lnk: the tick reads off and offers to write a fresh one
            }
            finally
            {
                Marshal.FinalReleaseComObject(link);
            }
        }

        public static void Delete(string linkPath)
        {
            if (File.Exists(linkPath)) File.Delete(linkPath);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        class ShellLink
        {
        }

        // IShellLinkW (widened): every member up to and including SetPath, in vtable order.
        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig]
            int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
    }
}
