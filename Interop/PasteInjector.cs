using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clip.Core;

namespace Clip.Interop
{
    internal static class PasteInjector
    {
        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x2;
        const ushort VK_SHIFT = 0x10;
        const ushort VK_CONTROL = 0x11;
        const ushort VK_MENU = 0x12;
        const ushort VK_V = 0x56;
        const ushort VK_LWIN = 0x5B;
        const ushort VK_RWIN = 0x5C;

        /// <summary>Places text on the clipboard, sends Ctrl+V, returns the sequence number so the caller can suppress self-capture.</summary>
        public static uint CopyAndPaste(string text)
        {
            ClipboardFormats.WithClipboardRetry(() => System.Windows.Clipboard.SetDataObject(text, true));
            return SendPaste();
        }

        /// <summary>Restores a captured item: images as real CF_DIB + PNG, else all original formats, else text/rich text by kind.</summary>
        public static uint CopyAndPaste(ClipboardItem item)
        {
            // Images first: the stored "PNG" format blob would otherwise win and Office/Paint cannot consume it.
            if (item?.Kind == "image")
            {
                // Caller is expected to have loaded the full image blob (ClipboardStore.GetImage) onto the item.
                if (item.Image is { Length: > 0 }) SetImageClipboard(item.Image);
                return SendPaste();
            }

            if (item?.Formats != null && item.Formats.Count > 0)
            {
                ClipboardFormats.Restore(item.Formats); // round-trips every format; per-app cases never needed
                return SendPaste();
            }

            if (item != null && (!string.IsNullOrEmpty(item.Html) || !string.IsNullOrEmpty(item.Rtf)))
            {
                var data = new System.Windows.DataObject();
                data.SetData(System.Windows.DataFormats.UnicodeText, item.Text ?? string.Empty);
                if (!string.IsNullOrEmpty(item.Rtf)) data.SetData(System.Windows.DataFormats.Rtf, item.Rtf);
                if (!string.IsNullOrEmpty(item.Html)) data.SetData(System.Windows.DataFormats.Html, item.Html);
                ClipboardFormats.WithClipboardRetry(() => System.Windows.Clipboard.SetDataObject(data, true));
            }
            else
            {
                ClipboardFormats.WithClipboardRetry(() => System.Windows.Clipboard.SetDataObject(item?.Text ?? string.Empty, true));
            }
            return SendPaste();
        }

        /// <summary>Pastes only the raw text: strips colours, tables and fonts (Ctrl+Shift+V).</summary>
        public static uint CopyAndPastePlain(ClipboardItem item)
        {
            ClipboardFormats.WithClipboardRetry(() => System.Windows.Clipboard.SetDataObject(item?.Text ?? string.Empty, true));
            return SendPaste();
        }

        /// <summary>
        /// CF_DIB first (24-bpp, bottom-up, flattened on white — what Office/Paint/Win+V consume), then the stored
        /// PNG verbatim so editors that explicitly ask for PNG still see the alpha channel.
        /// </summary>
        internal static void SetImageClipboard(byte[] png)
        {
            var dib = BuildDib(png);
            var data = new System.Windows.DataObject();
            if (dib != null) data.SetData(System.Windows.DataFormats.Dib, new MemoryStream(dib));
            data.SetData("PNG", new MemoryStream(png));
            ClipboardFormats.WithClipboardRetry(() => System.Windows.Clipboard.SetDataObject(data, true));
        }

        /// <summary>BITMAPINFOHEADER(40) + 24-bpp BGR rows, 4-byte stride, bottom-up, alpha composited over white.</summary>
        internal static byte[] BuildDib(byte[] png)
        {
            if (png == null || png.Length == 0) return null;
            BitmapSource src;
            try
            {
                var decoded = ToBitmapSource(png);
                if (decoded == null) return null;
                src = decoded.Format == PixelFormats.Bgra32
                    ? decoded
                    : new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0);
                if (src.CanFreeze) src.Freeze();
            }
            catch { return null; } // unreadable PNG: nothing sensible to put on the clipboard

            int w = src.PixelWidth, h = src.PixelHeight;
            if (w <= 0 || h <= 0) return null;
            var pixels = new byte[w * h * 4];
            src.CopyPixels(pixels, w * 4, 0);

            int stride = (w * 3 + 3) / 4 * 4;
            var dib = new byte[40 + stride * h];
            WriteInt(dib, 0, 40);           // biSize
            WriteInt(dib, 4, w);            // biWidth
            WriteInt(dib, 8, h);            // biHeight: positive = bottom-up
            WriteInt(dib, 12, 1);           // biPlanes
            WriteInt(dib, 14, 24);          // biBitCount
            WriteInt(dib, 16, 0);           // biCompression = BI_RGB
            WriteInt(dib, 20, stride * h);  // biSizeImage

            for (int y = 0; y < h; y++)
            {
                int dst = 40 + (h - 1 - y) * stride; // first stored row is the image's last row
                int row = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x * 4;
                    int a = pixels[i + 3];
                    dib[dst + x * 3] = Flatten(pixels[i], a);
                    dib[dst + x * 3 + 1] = Flatten(pixels[i + 1], a);
                    dib[dst + x * 3 + 2] = Flatten(pixels[i + 2], a);
                }
            }
            return dib;
        }

        static void WriteInt(byte[] buffer, int offset, int value) => BitConverter.GetBytes(value).CopyTo(buffer, offset);

        /// <summary>One channel composited over white: opaque keeps its colour, fully transparent becomes white.</summary>
        static byte Flatten(byte channel, int alpha) => (byte)((channel * alpha + 255 * (255 - alpha) + 127) / 255);

        /// <summary>The exact key sequence for Ctrl+V: modifier key-ups first, then the chord.</summary>
        internal static List<(ushort vk, uint flags)> PasteInputSequence() => new List<(ushort vk, uint flags)>
        {
            // A hotkey modifier still physically down (Alt from Alt+V) turns the chord into Ctrl+Alt+V; release them first.
            (VK_CONTROL, KEYEVENTF_KEYUP),
            (VK_MENU, KEYEVENTF_KEYUP),
            (VK_SHIFT, KEYEVENTF_KEYUP),
            (VK_LWIN, KEYEVENTF_KEYUP),
            (VK_RWIN, KEYEVENTF_KEYUP),
            (VK_CONTROL, 0),
            (VK_V, 0),
            (VK_V, KEYEVENTF_KEYUP),
            (VK_CONTROL, KEYEVENTF_KEYUP)
        };

        static uint SendPaste()
        {
            var sequence = PasteInputSequence();
            var inputs = new INPUT[sequence.Count];
            for (int i = 0; i < inputs.Length; i++) inputs[i] = Key(sequence[i].vk, sequence[i].flags);
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            return GetClipboardSequenceNumber();
        }

        static BitmapImage ToBitmapSource(byte[] png)
        {
            if (png == null || png.Length == 0) return null;
            using var ms = new MemoryStream(png);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        static INPUT Key(ushort vk, uint flags) => new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = flags } }
        };

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint n, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        static extern uint GetClipboardSequenceNumber();

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        // MOUSEINPUT must exist: it is the larger union member and keeps sizeof(INPUT) correct for x64.
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}
