using System;
using System.IO;
using System.Linq;

namespace Clip.Core
{
    /// <summary>Lazily materialized PNGs for F2 "open in default program"; never the source of truth.</summary>
    public static class ImageCache
    {
        static string Dir => Path.Combine(Config.DataDir, "cache", "images");

        /// <summary>Path of the item's PNG, rewritten only when the bytes differ so a viewer holding it open is safe.</summary>
        public static string Write(long id, byte[] png)
        {
            try
            {
                if (png == null || png.Length == 0) return null;
                var dir = Dir;
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"clip-{id}.png");
                if (File.Exists(path) && SameBytes(path, png)) return path;

                var tmp = path + ".tmp";
                File.WriteAllBytes(tmp, png);
                File.Move(tmp, path, overwrite: true);
                return path;
            }
            catch { return null; } // never crash the app for a cache write
        }

        static bool SameBytes(string path, byte[] png)
        {
            if (new FileInfo(path).Length != png.Length) return false;
            return File.ReadAllBytes(path).AsSpan().SequenceEqual(png);
        }

        /// <summary>Startup only: delete files older than 7 days, then keep the newest 100. Missing dir is fine.</summary>
        public static void Cleanup()
        {
            try
            {
                var dir = new DirectoryInfo(Dir);
                if (!dir.Exists) return;

                var cutoff = DateTime.UtcNow.AddDays(-7);
                foreach (var f in dir.GetFiles("clip-*.png").Where(f => f.LastWriteTimeUtc < cutoff))
                    TryDelete(f);

                var left = dir.GetFiles("clip-*.png");
                if (left.Length > 100)
                    foreach (var f in left.OrderByDescending(f => f.LastWriteTimeUtc).Skip(100))
                        TryDelete(f);
            }
            catch { } // best-effort; locked files wait for the next start
        }

        static void TryDelete(FileInfo f)
        {
            try { f.Delete(); } catch { } // a viewer may hold it open until next start
        }
    }
}
