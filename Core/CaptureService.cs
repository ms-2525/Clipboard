using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clip.Interop;

namespace Clip.Core
{
    internal sealed class CaptureService : IDisposable
    {
        readonly ClipboardStore _store;
        readonly Config _cfg;
        readonly ClipboardListener _listener;
        readonly DispatcherTimer _debounce;
        uint _suppressed;

        public event Action<ClipboardItem> Captured;

        /// <summary>An image was deliberately not stored (too large); App turns the reason into a toast.</summary>
        public event Action<string> Skipped;

        // Research: reject before decoding anything a bomb could allocate for.
        const string PngFormat = "PNG";
        const long MaxPixels = 50_000_000;
        const int MaxDimension = 16384;

        public CaptureService(ClipboardStore store, Config cfg)
        {
            _store = store;
            _cfg = cfg;
            _listener = new ClipboardListener();
            _listener.Changed += OnChanged;
            // Apps publish their formats in several SetClipboardData calls; reading inline can catch a half-populated clipboard.
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _debounce.Tick += (s, e) => { _debounce.Stop(); Capture(); };
        }

        /// <summary>Our own paste produced this sequence number; never capture it back.</summary>
        public void Suppress(uint seq) => _suppressed = seq;

        /// <summary>Clipboard change: (re)start the debounce so a burst collapses into one read of the final state.</summary>
        internal void OnChanged()
        {
            if (_suppressed != 0 && GetClipboardSequenceNumber() == _suppressed) return;
            _debounce.Stop();
            _debounce.Start();
        }

        /// <summary>Reads the clipboard once and stores what it holds.</summary>
        internal void Capture()
        {
            // Re-checked at fire time: a change the debounce already queued may have been our own paste.
            if (_suppressed != 0 && GetClipboardSequenceNumber() == _suppressed) return;

            foreach (var format in _cfg.IgnoreFormats)
                if (IsClipboardFormatAvailable(RegisterClipboardFormat(format))) return;

            string text;
            try { text = System.Windows.Clipboard.GetText(); }
            catch { return; } // clipboard momentarily owned by another process

            if (!string.IsNullOrEmpty(text))
            {
                if (text.Trim().Length < _cfg.MinTextLength) return;

                var (app, title) = ForegroundApp.Get();
                var item = new ClipboardItem
                {
                    Kind = "text",
                    Text = text,
                    AppName = app,
                    WindowTitle = title,
                    CapturedAt = ClipboardItem.Stamp(),
                    SizeBytes = Encoding.UTF8.GetByteCount(text)
                };
                ReadRichText(item);
                Store(item);
            }
            else
            {
                // A file copy has no text, so it is checked before the image path. The path list doubles as searchable text.
                var files = TryGetFileList();
                if (files != null)
                {
                    var list = string.Join(Environment.NewLine, files);
                    var (app, title) = ForegroundApp.Get();
                    Store(new ClipboardItem
                    {
                        Kind = "files",
                        Text = list,
                        Uris = list,
                        AppName = app,
                        WindowTitle = title,
                        CapturedAt = ClipboardItem.Stamp(),
                        SizeBytes = Encoding.UTF8.GetByteCount(list)
                    });
                    return;
                }

                var item = CaptureImage();
                if (item != null) Store(item);
            }
        }

        /// <summary>Clipboard file list (CF_HDROP), or null when it holds none.</summary>
        static string[] TryGetFileList()
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsFileDropList()) return null;
                var paths = System.Windows.Clipboard.GetFileDropList();
                if (paths == null || paths.Count == 0) return null;
                var list = new string[paths.Count];
                paths.CopyTo(list, 0);
                return list;
            }
            catch { return null; } // clipboard locked mid-read
        }

        void Store(ClipboardItem item)
        {
            // Every original format rides along when it fits the budget: Excel gets Biff12 back, Word RTF/HTML, browsers HTML.
            item.Formats = ClipboardFormats.Snapshot(_cfg.MaxItemBytes);
            // A data page that opens but fails on write recovers once inside the store instead of crash-looping (AUDIT P1).
            _store.WriteWithRecovery(() =>
            {
                // Exact match already in history: that row takes the fresh snapshot and moves to the top instead of a second copy.
                if (!_store.TryDedupeMoveToTop(item)) _store.InsertWithFormats(item);
                _store.EnforceCap(_cfg.MaxItems);
            });
            Captured?.Invoke(item);
        }

        /// <summary>CF_HTML carries its Version:/StartHTML header; stored verbatim so paste-back round-trips.</summary>
        static void ReadRichText(ClipboardItem item)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Rtf))
                    item.Rtf = System.Windows.Clipboard.GetData(System.Windows.DataFormats.Rtf) as string;
                if (System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Html))
                    item.Html = System.Windows.Clipboard.GetData(System.Windows.DataFormats.Html) as string;
            }
            catch { } // locked mid-read; the plain text is still worth keeping
        }

        /// <summary>
        /// Image-only capture. The registered "PNG" format is the only lossless carrier most apps publish;
        /// GetImage goes through CF_DIB and has already lost alpha, so it is only the fallback.
        /// </summary>
        ClipboardItem CaptureImage()
        {
            byte[] png = null;
            BitmapSource src = null;

            try
            {
                if (System.Windows.Clipboard.ContainsData(PngFormat))
                    png = (System.Windows.Clipboard.GetData(PngFormat) as MemoryStream)?.ToArray();
            }
            catch { } // locked mid-read; the DIB fallback below may still work
            if (png != null && !HasPngSignature(png)) png = null;

            if (png != null)
            {
                if (png.Length > _cfg.MaxImageBytes)
                {
                    Skipped?.Invoke($"Image skipped: {Megabytes(png.Length)} MB over the {Megabytes(_cfg.MaxImageBytes)} MB limit");
                    return null;
                }
                // Header-only dimension read: a 30000x30000 PNG is rejected before a single pixel is decoded.
                if (HeaderTooLarge(png, out var reason))
                {
                    Skipped?.Invoke(reason);
                    return null;
                }
            }
            else
            {
                try
                {
                    if (!System.Windows.Clipboard.ContainsImage()) return null;
                    src = System.Windows.Clipboard.GetImage();
                }
                catch { return null; } // clipboard locked or no bitmap on the clipboard
                if (src == null) return null;

                if (src.PixelWidth > MaxDimension || src.PixelHeight > MaxDimension ||
                    (long)src.PixelWidth * src.PixelHeight > MaxPixels)
                {
                    Skipped?.Invoke("Image skipped: too large");
                    return null;
                }

                png = EncodePng(src);
                if (!WithinLimit(png, _cfg.MaxImageBytes))
                {
                    Skipped?.Invoke($"Image skipped: {Megabytes(png?.Length ?? 0)} MB over the {Megabytes(_cfg.MaxImageBytes)} MB limit");
                    return null;
                }
            }

            if (src == null)
            {
                src = DecodePng(png);
                if (src == null) return null;
            }

            var thumb = EncodePng(Thumbnail(src, 96)) ?? png;
            var (app, title) = ForegroundApp.Get();
            return new ClipboardItem
            {
                Kind = "image",
                Image = png,
                Thumb = thumb,
                AppName = app,
                WindowTitle = title,
                CapturedAt = ClipboardItem.Stamp(),
                SizeBytes = png.Length
            };
        }

        internal static bool WithinLimit(byte[] png, long maxBytes) => png != null && png.Length <= maxBytes;

        /// <summary>Header check only: BitmapDecoder with no caching reads the frame size without decoding pixels.</summary>
        internal static bool HeaderTooLarge(byte[] png, out string reason)
        {
            reason = null;
            try
            {
                using var ms = new MemoryStream(png);
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                if (frame.PixelWidth > MaxDimension || frame.PixelHeight > MaxDimension ||
                    (long)frame.PixelWidth * frame.PixelHeight > MaxPixels)
                {
                    reason = "Image skipped: too large";
                    return true;
                }
            }
            catch
            {
                reason = "Image skipped: unreadable image";
                return true;
            }
            return false;
        }

        static bool HasPngSignature(byte[] bytes) =>
            bytes != null && bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

        static string Megabytes(long bytes) => (bytes / 1048576.0).ToString("0.#");

        static BitmapSource DecodePng(byte[] png)
        {
            try
            {
                using var ms = new MemoryStream(png);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; } // not a decodable PNG; the caller treats it as "no image"
        }

        internal static byte[] EncodePng(BitmapSource src)
        {
            if (src == null) return null;
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(src));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }

        /// <summary>Aspect-preserving downscale so the LARGER dimension fits max; never upscales.</summary>
        internal static BitmapSource Thumbnail(BitmapSource src, int max)
        {
            if (src == null || src.PixelWidth <= 0 || src.PixelHeight <= 0) return null;
            if (src.CanFreeze) src.Freeze();
            int larger = Math.Max(src.PixelWidth, src.PixelHeight);
            if (larger <= max) return src;
            double scale = (double)max / larger;
            var scaled = new TransformedBitmap(src, new ScaleTransform(scale, scale));
            scaled.Freeze();
            return scaled;
        }

        public void Dispose()
        {
            _debounce.Stop();
            _listener.Changed -= OnChanged;
            _listener.Dispose();
        }

        [DllImport("user32.dll")]
        static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern uint RegisterClipboardFormat(string format);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsClipboardFormatAvailable(uint format);
    }
}
