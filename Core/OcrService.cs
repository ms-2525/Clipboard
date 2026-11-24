using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Clip.Core
{
    /// <summary>Outcome of one OCR attempt. A failure is data, never an exception.</summary>
    public enum OcrStatus
    {
        Ok,
        NoText,
        /// <summary>The requested language is not installed; nothing was recognised (never garbage).</summary>
        LanguageNotAvailable,
        Failed
    }

    public readonly struct OcrResult
    {
        public OcrResult(OcrStatus status, string text = null, int words = 0)
        {
            Status = status;
            Text = text;
            Words = words;
        }

        public OcrStatus Status { get; }

        /// <summary>Recognised text, lines joined by the engine's newline. Null unless <see cref="Status"/> is Ok.</summary>
        public string Text { get; }

        /// <summary>Word count as the engine reported it (bounding boxes exist per word).</summary>
        public int Words { get; }
    }

    /// <summary>
    /// Offline OCR through Windows' built-in engine (Windows.Media.Ocr): nothing to ship, ~40 ms for a
    /// full-HD screenshot. The measured facts this class is built around (docs\OCR research, 25/11/2026):
    /// RecognizeAsync refuses a concurrent call even from a different engine, the stored 96 px thumb returns
    /// zero characters, an image side over MaxImageDimension throws, and a missing language silently returns
    /// garbage rather than an error. Hence: one process-wide gate, full-image input, a downscale guard and a
    /// typed "language not available" result.
    /// </summary>
    public sealed class OcrService : IDisposable
    {
        // Process-wide, static: two OcrService instances must still never call RecognizeAsync at the same time.
        static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        static readonly Dictionary<string, OcrEngine> EngineCache = new Dictionary<string, OcrEngine>(StringComparer.OrdinalIgnoreCase);

        // A 50 MP capture (Clip's limit) decodes to a 200 MB bitmap; the engine only limits a side to 10,000.
        const long MaxOcrPixels = 24_000_000;

        readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
        readonly Thread _worker;

        public OcrService()
        {
            _worker = new Thread(Run)
            {
                IsBackground = true,
                Name = "Clip OCR",
                Priority = ThreadPriority.BelowNormal
            };
            _worker.SetApartmentState(ApartmentState.MTA); // measured: OCR needs no STA
            _worker.Start();
        }

        /// <summary>
        /// Installed recognizer languages, English first; empty when the Windows OCR component is missing.
        /// The UI may only advertise OCR for these tags.
        /// </summary>
        public IReadOnlyList<string> Languages
        {
            get
            {
                var tags = new List<string>();
                try
                {
                    foreach (var language in OcrEngine.AvailableRecognizerLanguages)
                    {
                        string tag = language.LanguageTag;
                        if (string.IsNullOrEmpty(tag)) continue;
                        if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase)) tags.Insert(0, tag);
                        else tags.Add(tag);
                    }
                }
                catch (Exception) { } // OCR component missing: "no languages", never an exception at a caller
                return tags;
            }
        }

        /// <summary>English when installed; null when no recognizer is installed at all.</summary>
        public string DefaultLanguage => Languages.Count > 0 ? Languages[0] : null;

        /// <summary>
        /// The capture hook: queues one already-committed image for OCR on the single below-normal worker.
        /// <paramref name="write"/> runs on the worker thread after a successful read and must hop to its own
        /// writer thread (the store has one writer connection). Nothing here throws into the capture path, and
        /// a failed or empty pass is simply dropped.
        /// </summary>
        public void Enqueue(long itemId, byte[] png, Action<long, string> write)
        {
            if (png == null || png.Length == 0 || write == null) return;
            try
            {
                _queue.Add(() =>
                {
                    var result = TryRead(png);
                    if (result.Status != OcrStatus.Ok) return;
                    try { write(itemId, result.Text); }
                    catch (Exception) { } // the image row is already stored; OCR text is optional
                });
            }
            catch (InvalidOperationException)
            {
                // Queue closed during shutdown: the capture is committed, only the enrichment is lost.
            }
        }

        /// <summary>
        /// Reads one PNG (the full stored image, never the thumbnail) in the requested language, or English.
        /// Serialised process-wide; never throws. Call from a background thread: it blocks while the engine runs.
        /// </summary>
        public OcrResult TryRead(byte[] png, string language = null)
        {
            if (png == null || png.Length == 0) return new OcrResult(OcrStatus.Failed);

            string tag = string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language;
            if (tag == null) return new OcrResult(OcrStatus.LanguageNotAvailable);

            OcrEngine engine = EngineFor(tag);
            if (engine == null) return new OcrResult(OcrStatus.LanguageNotAvailable);

            Gate.Wait();
            try
            {
                var (text, words) = Recognize(png, engine).GetAwaiter().GetResult();
                return string.IsNullOrWhiteSpace(text)
                    ? new OcrResult(OcrStatus.NoText) // unreadable image: the engine reports no text, no error
                    : new OcrResult(OcrStatus.Ok, text, words);
            }
            catch (Exception)
            {
                // Undecodable or oversized input must never take down capture; the picture is still the clip.
                return new OcrResult(OcrStatus.Failed);
            }
            finally
            {
                Gate.Release();
            }
        }

        static OcrEngine EngineFor(string tag)
        {
            lock (EngineCache)
                if (EngineCache.TryGetValue(tag, out var cached)) return cached;

            OcrEngine engine = null;
            try
            {
                // No TryCreateFromUserProfileLanguages fallback: a missing language must stay "not available".
                // Measured, the English engine reads a Hindi screenshot as text-shaped garbage.
                var language = new Language(tag);
                if (OcrEngine.IsLanguageSupported(language)) engine = OcrEngine.TryCreateFromLanguage(language);
            }
            catch (Exception) { } // malformed tag counts as "not installed"

            if (engine != null) lock (EngineCache) EngineCache[tag] = engine;
            return engine;
        }

        static async Task<(string text, int words)> Recognize(byte[] png, OcrEngine engine)
        {
            using IRandomAccessStream stream = new MemoryStream(png, writable: false).AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var transform = ScaleFor(decoder.PixelWidth, decoder.PixelHeight);

            using var bitmap = transform == null
                ? await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
                : await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

            var result = await engine.RecognizeAsync(bitmap);
            int words = 0;
            foreach (var line in result.Lines) words += line.Words.Count;
            return (result.Text, words);
        }

        /// <summary>
        /// Null when the image fits the engine as-is. Downscaling is a guard, not an optimisation: anything over
        /// MaxImageDimension (10,000) makes RecognizeAsync throw, and Clip accepts up to 16,384 per side.
        /// </summary>
        static BitmapTransform ScaleFor(uint width, uint height)
        {
            if (width == 0 || height == 0) return null;
            double scale = Math.Min(1.0, (double)OcrEngine.MaxImageDimension / Math.Max(width, height));
            double pixels = (double)width * height * scale * scale;
            if (pixels > MaxOcrPixels) scale *= Math.Sqrt(MaxOcrPixels / pixels);
            if (scale >= 1.0) return null;
            return new BitmapTransform
            {
                ScaledWidth = (uint)Math.Max(1, Math.Round(width * scale)),
                ScaledHeight = (uint)Math.Max(1, Math.Round(height * scale)),
                InterpolationMode = BitmapInterpolationMode.Fant
            };
        }

        void Run()
        {
            foreach (var job in _queue.GetConsumingEnumerable())
            {
                try { job(); }
                catch (Exception) { } // the only OCR worker must survive any single image
            }
        }

        public void Dispose()
        {
            try { _queue.CompleteAdding(); }
            catch (ObjectDisposedException) { } // already disposed
        }
    }
}
