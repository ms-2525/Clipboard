using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Clip.Interop
{
    /// <summary>
    /// Snapshots every HGLOBAL clipboard format so a paste can hand the target app exactly what it
    /// originally got (Excel Biff12, Word RTF/HTML, browser HTML, ...). No per-app special cases.
    /// </summary>
    internal static class ClipboardFormats
    {
        // Standard, pre-registered ids that carry real data. Names follow Ditto's format list.
        static readonly Dictionary<uint, string> StandardNames = new Dictionary<uint, string>
        {
            [1] = "Text",                       // CF_TEXT
            [8] = "DeviceIndependentBitmap",    // CF_DIB
            [13] = "UnicodeText",               // CF_UNICODETEXT
            [15] = "FileDrop",                  // CF_HDROP
            [16] = "Locale",                    // CF_LOCALE
            [17] = "DeviceIndependentBitmapV5"  // CF_DIBV5
        };

        // Only true GDI handle types are unreadable as HGLOBAL; CF_DIB/CF_DIBV5 are HGLOBAL-backed and must be stored.
        static readonly uint[] HandleTypes = { 2, 3, 9, 14 }; // CF_BITMAP, CF_METAFILEPICT, CF_PALETTE, CF_ENHMETAFILE

        static readonly HashSet<string> Ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Clipboard Viewer Ignore",
            "ExcludeClipboardContentFromMonitorProcessing"
        };

        public static List<(string fmt, byte[] bytes)> Snapshot(long maxBytes)
        {
            var result = new List<(string fmt, byte[] bytes)>();
            if (!OpenWithRetry()) return result; // clipboard stayed locked for the whole window
            try
            {
                long total = 0;
                for (uint f = EnumClipboardFormats(0); f != 0; f = EnumClipboardFormats(f))
                {
                    if (IsSkippedType(f)) continue;
                    string name = FormatName(f);
                    if (name == null || Ignored.Contains(name)) continue;

                    var bytes = ReadGlobal(GetClipboardData(f));
                    if (bytes == null || bytes.Length == 0) continue;
                    if (total + bytes.Length > maxBytes) continue; // budget guard: drop this blob, keep the rest
                    total += bytes.Length;
                    result.Add((name, bytes));
                }
            }
            finally { CloseClipboard(); }
            return result;
        }

        /// <summary>
        /// Puts the stored format set back on the clipboard. False when nothing was written or the write did
        /// not land, so a caller can refuse an action instead of using whatever the clipboard held before.
        /// </summary>
        public static bool Restore(List<(string fmt, byte[] bytes)> formats)
        {
            if (formats == null || formats.Count == 0) return false;

            var data = new System.Windows.DataObject();
            string primary = null;
            foreach (var (fmt, bytes) in formats)
            {
                if (string.IsNullOrEmpty(fmt) || bytes == null) continue;
                primary ??= fmt; // the first stored format is the one a reader asks for first
                switch (fmt)
                {
                    case "UnicodeText":
                        data.SetData(System.Windows.DataFormats.UnicodeText, Encoding.Unicode.GetString(bytes).TrimEnd('\0'));
                        break;
                    case "FileDrop":
                        // Readers want a path list, not our raw DROPFILES blob.
                        var paths = DecodeFileDrop(bytes);
                        if (paths != null && paths.Length > 0) data.SetData(System.Windows.DataFormats.FileDrop, paths);
                        break;
                    case "DeviceIndependentBitmap":
                        // DataFormats.Dib registers back to CF_DIB (8), the form Office/Paint consume; OLE also serves DIBV5 from it.
                        data.SetData(System.Windows.DataFormats.Dib, new MemoryStream(bytes));
                        break;
                    default:
                        // Includes "DeviceIndependentBitmapV5": Windows maps no friendly name back to id 17, so the
                        // V5-only case cannot be written as CF_DIBV5 through a WPF DataObject (the DIB above covers paste).
                        data.SetData(fmt, new MemoryStream(bytes));
                        break;
                }
            }

            if (primary == null) return false;
            return Write(() => System.Windows.Clipboard.SetDataObject(data, true), primary);
        }

        /// <summary>
        /// The one home for the clipboard-busy policy: another process owning the clipboard rejects a write for a few
        /// ms. False after the window, leaving the previous clipboard content in place.
        /// </summary>
        internal static bool WithClipboardRetry(Action set)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    set();
                    return true;
                }
                catch (COMException) { Thread.Sleep(30); } // still owned by another process: the next attempt, then give up
            }
            return false;
        }

        /// <summary>
        /// Writes, then reads the expected format back: a returned write can still have dropped the data, and
        /// a paste must never inject a chord over the previous clipboard content.
        /// </summary>
        internal static bool Write(Action set, string expectedFormat)
        {
            if (!WithClipboardRetry(set)) return false;
            try
            {
                return System.Windows.Clipboard.ContainsData(expectedFormat);
            }
            catch (COMException)
            {
                return true; // another app took the clipboard right after our successful write: the write itself landed
            }
        }

        /// <summary>The clipboard's change counter: lets the capture hook prove a write was Clip's own.</summary>
        internal static uint SequenceNumber() => GetClipboardSequenceNumber();

        /// <summary>Registered formats start at 0xC000; below that only the known standard ids have names.</summary>
        static string FormatName(uint format)
        {
            if (format < 0xC000) return StandardNames.TryGetValue(format, out var known) ? known : null;
            var sb = new StringBuilder(256);
            int n = GetClipboardFormatName(format, sb, sb.Capacity);
            return n > 0 ? sb.ToString(0, n) : null;
        }

        /// <summary>GDI handle types and the CF_DSP* (display-only rendering) range cannot be read as HGLOBAL.</summary>
        static bool IsSkippedType(uint f) => Array.IndexOf(HandleTypes, f) >= 0 || (f >= 0x81 && f <= 0x8E);

        /// <summary>CF_HDROP bytes are a DROPFILES struct plus a double-null-terminated path list.</summary>
        static string[] DecodeFileDrop(byte[] bytes)
        {
            IntPtr h = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)bytes.Length);
            if (h == IntPtr.Zero) return null;
            try
            {
                var ptr = GlobalLock(h);
                if (ptr == IntPtr.Zero) return null;
                try { Marshal.Copy(bytes, 0, ptr, bytes.Length); }
                finally { GlobalUnlock(h); }

                uint count = DragQueryFileW(h, 0xFFFFFFFF, null, 0);
                var paths = new string[count];
                for (uint i = 0; i < count; i++)
                {
                    uint len = DragQueryFileW(h, i, null, 0);
                    var sb = new StringBuilder((int)len + 1);
                    DragQueryFileW(h, i, sb, (uint)sb.Capacity);
                    paths[i] = sb.ToString();
                }
                return paths;
            }
            finally { GlobalFree(h); }
        }

        static byte[] ReadGlobal(IntPtr h)
        {
            if (h == IntPtr.Zero) return null;
            ulong size = GlobalSize(h).ToUInt64();
            if (size == 0 || size > int.MaxValue) return null; // not HGLOBAL-backed
            var ptr = GlobalLock(h);
            if (ptr == IntPtr.Zero) return null;
            try
            {
                var bytes = new byte[(int)size];
                Marshal.Copy(ptr, bytes, 0, (int)size);
                return bytes;
            }
            finally { GlobalUnlock(h); }
        }

        static bool OpenWithRetry()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (OpenClipboard(IntPtr.Zero)) return true;
                Thread.Sleep(30);
            }
            return false;
        }

        const uint GMEM_MOVEABLE = 0x0002;

        [DllImport("user32.dll")]
        static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        static extern uint EnumClipboardFormats(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClipboardFormatName(uint format, StringBuilder lpszFormatName, int cchMaxCount);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, StringBuilder lpszFile, uint cch);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll")]
        static extern UIntPtr GlobalSize(IntPtr hMem);

        [DllImport("kernel32.dll")]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalUnlock(IntPtr hMem);
    }
}
