using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Clip.Interop
{
    /// <summary>
    /// The notification-area mark: the UI's clipboard drawn on the icons' 24-unit grid at every size Windows
    /// asks for, monochrome so it reads against either taskbar theme.
    /// </summary>
    internal static class TrayIcon
    {
        static readonly int[] Sizes = { 16, 20, 24, 32, 48 };

        public static Icon Create()
        {
            var ink = IsLightTaskbar()
                ? Color.FromArgb(0x20, 0x20, 0x20)
                : Color.FromArgb(0xE8, 0xE8, 0xE8);

            var frames = new List<(int, byte[])>();
            foreach (int size in Sizes) frames.Add((size, Frame(size, ink)));

            // Clone so the returned icon no longer depends on the stream it was read from.
            using var stream = new MemoryStream(Pack(frames));
            using var temp = new Icon(stream);
            return (Icon)temp.Clone();
        }

        /// <summary>One frame. Below 24 px the second text line is dropped; it would merge into a blob.</summary>
        static byte[] Frame(int size, Color ink)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                float u = size / 24f;
                using var pen = new Pen(ink, 2f * u)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                using var path = new GraphicsPath();
                AddRounded(path, 4 * u, 6 * u, 16 * u, 15 * u, 2 * u);
                AddRounded(path, 9 * u, 3 * u, 6 * u, 4 * u, 1.2f * u);
                g.DrawPath(pen, path);

                g.DrawLine(pen, 8 * u, 11.5f * u, 16 * u, 11.5f * u);
                if (size >= 24) g.DrawLine(pen, 8 * u, 15.5f * u, 13 * u, 15.5f * u);
            }

            var area = new Rectangle(0, 0, size, size);
            var data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var bytes = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * size * 4, size * 4);
            bitmap.UnlockBits(data);
            return bytes;
        }

        static void AddRounded(GraphicsPath path, float x, float y, float w, float h, float r)
        {
            float d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
        }

        /// <summary>Writes the frames as one .ico, so Windows picks the right size instead of downscaling a 32.</summary>
        static byte[] Pack(List<(int Size, byte[] Bgra)> frames)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);

            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)frames.Count);

            int offset = 6 + 16 * frames.Count;
            foreach (var (size, _) in frames)
            {
                int bytes = 40 + size * size * 4 + size * ((size + 31) / 32) * 4;
                w.Write((byte)(size >= 256 ? 0 : size));
                w.Write((byte)(size >= 256 ? 0 : size));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write(bytes);
                w.Write(offset);
                offset += bytes;
            }

            foreach (var (size, bgra) in frames)
            {
                w.Write(40);
                w.Write(size);
                w.Write(size * 2);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write(0);
                w.Write(size * size * 4);
                w.Write(0);
                w.Write(0);
                w.Write(0);
                w.Write(0);
                for (int y = size - 1; y >= 0; y--) w.Write(bgra, y * size * 4, size * 4);
                w.Write(new byte[((size + 31) / 32) * 4 * size]);
            }

            return stream.ToArray();
        }

        static bool IsLightTaskbar()
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "SystemUsesLightTheme", 0);
            return value is int i && i == 1;
        }
    }
}
