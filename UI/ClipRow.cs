using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Clip.Core;

namespace Clip.UI
{
    /// <summary>
    /// One row of the popup. Every piece of geometry it owns is computed ONCE here, from the item's
    /// text alone — selection can only change brushes (docs\DESIGN-v2-directives.md §1).
    /// A row carries the stored <see cref="ClipboardStore.PreviewChars"/>-char preview, never the full
    /// body: the list can hold 10 000 of those, and a 1000-line clip is 60 KB each.
    /// </summary>
    public sealed class ClipRow : INotifyPropertyChanged
    {
        // A row's text slot is capped by Config.PreviewLines (1..100, honored directly) — no render clamp.
        const int TooltipCap = 2000;
        const int EstimateChars = 20000;

        /// <summary>
        /// The estimator's chars-per-line, never measured per row: a measured height would twitch on
        /// font fallback and undo the frozen-geometry contract (§1.2). Calibrated for the content size:
        /// Inter 14 in the 526 px text column (660 DIP popup − 28 tile − 10 gap − 66 facts − 16 inset).
        /// FormattedText at 150 % DPI measures 6.63 px per character → 79.
        /// </summary>
        public const double CharsPerLine = 79;

        /// <summary>Rows in the list read as a list, not a document. Full text is the outside tooltip.</summary>
        public const int ListLinesMax = 3;

        readonly ClipboardStore _store;
        readonly string _text;
        readonly int _textCap;
        readonly int _previewLines;
        readonly long _textLen;
        bool _metricsDone;
        bool _truncated;
        double _textHeight;
        double _rowHeight;
        string _more;
        ClipboardItem _payload;
        bool _payloadRead;
        BitmapSource _thumb;
        bool _thumbRead;
        string _tooltip;
        bool _tooltipRead;

        public ClipRow(ClipboardItem item, int previewLines, ClipboardStore store)
        {
            Item = item;
            _store = store;
            _text = item.Text ?? "";
            _textLen = Math.Max(item.TextLength, _text.Length);
            IsImage = item.Kind == "image";
            IsFiles = item.Kind == "files";
            _previewLines = Math.Max(1, Math.Min(100, previewLines));
            _textCap = Math.Min(_previewLines, ListLinesMax) * (int)CharsPerLine + (int)CharsPerLine;

            Preview = IsFiles ? FilePreview(item) : IsImage ? "Image  \u00b7  " + Size() : Truncate(_text, _textCap);
            Source = item.AppName ?? "";
            Title = item.WindowTitle ?? "";
            When = Stamp(item.CapturedAt);
            Clock = ClockOf(item.CapturedAt);
            Initial = InitialOf(Source);
        }

        public ClipboardItem Item { get; }
        public long Id => Item.Id;

        /// <summary>What the row's own text slot shows when no formatted body is available.</summary>
        public string Preview { get; }

        /// <summary>
        /// Full text (or every file path) — the row tooltip is its only home (directive §2). Loaded when
        /// the tip actually asks for it, because the row only carries the visible preview.
        /// </summary>
        public string Tooltip
        {
            get
            {
                if (_tooltipRead) return _tooltip;
                _tooltipRead = true;
                if (IsImage) return _tooltip = null;
                string body = IsFiles ? Paths(Item)
                    : Item.IsTruncated && _store != null ? _store.GetText(Item.Id) ?? _text
                    : _text;
                _tooltip = Truncate(body, TooltipCap);
                return _tooltip;
            }
        }

        /// <summary>Bottom bar provenance for the selected clip: app · window title · captured time.</summary>
        public string Source { get; }
        public string Title { get; }
        public string When { get; }

        /// <summary>Clock only ("21:19") for the row's facts strip — the section band already says the day.</summary>
        public string Clock { get; }

        /// <summary>The identity tile's letter: the source app, one glyph, read before the text is.</summary>
        public string Initial { get; }

        public string Status
        {
            get
            {
                var parts = new List<string>(3);
                if (!string.IsNullOrWhiteSpace(Source)) parts.Add(Source.Trim());
                int titleAt = -1;
                if (!string.IsNullOrWhiteSpace(Title)) { titleAt = parts.Count; parts.Add(Title.Trim()); }
                if (!string.IsNullOrWhiteSpace(When)) parts.Add(When);
                if (parts.Count == 0) return "";

                // The bar is one 11 px line beside the count and key hints: a long window title is what
                // gives way, never the app or the time (they are the part a tail trim would eat).
                int used = (parts.Count - 1) * StatusSep.Length;
                foreach (var part in parts) used += part.Length;
                if (titleAt >= 0 && used > StatusChars)
                {
                    int room = Math.Max(8, parts[titleAt].Length - (used - StatusChars));
                    parts[titleAt] = parts[titleAt].Substring(0, Math.Min(parts[titleAt].Length, room)).TrimEnd() + "\u2026";
                }
                return string.Join(StatusSep, parts);
            }
        }

        const int StatusChars = 42;
        const string StatusSep = "  \u00b7  ";

        /// <summary>True when the row shows less than the clip holds — the only rows that get the outside tooltip.</summary>
        public bool Truncated { get { EnsureMetrics(); return _truncated; } }

        public double TextHeight { get { EnsureMetrics(); return _textHeight; } }
        public double RowHeight { get { EnsureMetrics(); return _rowHeight; } }

        public bool IsImage { get; }
        public bool IsFiles { get; }

        /// <summary>Rows that may carry an rtf/html body; images and files never do.</summary>
        public bool CanBeRich => !IsImage && !IsFiles;

        public string More { get { EnsureMetrics(); return _more; } }
        public Visibility MoreVisibility => More == null ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>1-based position in the visible list — the Ctrl+1..9 affordance (directive §3).</summary>
        int _number;
        public int Number
        {
            get => _number;
            set { if (_number == value) return; _number = value; Raise(nameof(Number)); }
        }

        /// <summary>Current search box text; match runs are baked into the plain render (synthesis Q2).</summary>
        string _query = "";
        public string Query
        {
            get => _query;
            set { if (_query == value) return; _query = value ?? ""; Raise(nameof(Query)); }
        }

        /// <summary>rtf/html for this row, read once and only when a realized row asks for it.</summary>
        public ClipboardItem Payload
        {
            get
            {
                if (_payloadRead) return _payload;
                _payloadRead = true;
                _payload = _store?.GetRich(Item.Id);
                return _payload;
            }
        }

        /// <summary>Decoded once per realised row; 96 px is the largest preview thumb the grid shows.</summary>
        public BitmapSource Thumb
        {
            get
            {
                if (_thumbRead) return _thumb;
                _thumbRead = true;
                var bytes = Item.Thumb is { Length: > 0 } ? Item.Thumb : _store?.GetThumb(Item.Id);
                if (bytes == null || bytes.Length == 0) return _thumb;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 96;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    _thumb = bmp;
                }
                catch { _thumb = null; } // a corrupt thumb must never take the list down
                return _thumb;
            }
        }

        string _sectionLabel;
        public string SectionLabel
        {
            get => _sectionLabel;
            set { if (_sectionLabel == value) return; _sectionLabel = value; Raise(nameof(SectionLabel)); }
        }

        /// <summary>
        /// "Today / Yesterday / This Week / Earlier" for this row, parsed once. A keystroke re-filtered
        /// 9500 rows and re-parsed every timestamp; the section only changes with the day, not the query.
        /// </summary>
        public string Section
        {
            get
            {
                if (_sectionRead) return _section;
                _sectionRead = true;
                if (!DateTime.TryParse(Item.CapturedAt, out var when)) return _section = "Earlier";
                var today = DateTime.Today;
                if (when.Date == today) _section = "Today";
                else if (when.Date == today.AddDays(-1)) _section = "Yesterday";
                else if (when >= today.AddDays(-7)) _section = "This Week";
                else _section = "Earlier";
                return _section;
            }
        }

        string _section;
        bool _sectionRead;

        bool _showSection;
        public bool ShowSection
        {
            get => _showSection;
            set { if (_showSection == value) return; _showSection = value; Raise(nameof(ShowSection)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Height, badge and truncation are only needed by a realised row, so they are computed on first
        /// read: building 10 000 rows must not count wrapped lines in all of them.
        /// </summary>
        void EnsureMetrics()
        {
            if (_metricsDone) return;
            _metricsDone = true;

            int total = IsImage || IsFiles ? 1 : Math.Max(WrappedLines(_text), EstimateLines(_textLen));
            int visible = Math.Max(1, Math.Min(Math.Min(_previewLines, ListLinesMax), total));

            _textHeight = visible * 20;
            _rowHeight = _textHeight + 16;

            int hidden = total - visible;
            if (!IsFiles && !IsImage && _textLen > _textCap && hidden < 1) hidden = 1;
            if (!IsFiles && !IsImage && hidden > 0) _more = "+" + hidden;
            _truncated = hidden > 0 || _textLen > _textCap;
        }

        /// <summary>Lower bound on the body's line count from its full length — the preview holds only its start.</summary>
        static int EstimateLines(long chars) => chars <= 0 ? 1 : (int)Math.Ceiling((double)chars / CharsPerLine);

        /// <summary>"23 Nov 15:16" — the date is fixed English (owner's UI language), the clock follows config.</summary>
        static string Stamp(string stamp)
        {
            if (!DateTime.TryParse(stamp, out var when)) return "";
            string clock = string.IsNullOrEmpty(ClipboardItem.ClockFormat) ? "HH:mm" : ClipboardItem.ClockFormat;
            return when.ToString("dd MMM", System.Globalization.CultureInfo.InvariantCulture) + " " + when.ToString(clock);
        }

        /// <summary>"21:19" only: the section band (Today / Yesterday) already carries the day.</summary>
        static string ClockOf(string stamp)
        {
            if (!DateTime.TryParse(stamp, out var when)) return "";
            string clock = string.IsNullOrEmpty(ClipboardItem.ClockFormat) ? "HH:mm" : ClipboardItem.ClockFormat;
            return when.ToString(clock);
        }

        /// <summary>The identity tile's letter. No source app (own-process copy, file drop) gets a dot.</summary>
        static string InitialOf(string app)
        {
            if (string.IsNullOrWhiteSpace(app)) return "\u2022";
            return char.ToUpperInvariant(app.Trim()[0]).ToString();
        }

        string Size()
        {
            long bytes = Item.SizeBytes;
            return bytes >= 1048576 ? (bytes / 1048576.0).ToString("0.#") + " MB" : Math.Max(1, bytes / 1024) + " KB";
        }

        /// <summary>Files row: the first basename plus a count — a full path in a 34 px row is noise.</summary>
        static string FilePreview(ClipboardItem item)
        {
            var paths = SplitLines(Paths(item));
            if (paths.Count == 0) return "File";
            string name = Basename(paths[0]);
            return paths.Count > 1 ? name + "  +" + (paths.Count - 1) + " more" : name;
        }

        static string Paths(ClipboardItem item) =>
            string.IsNullOrWhiteSpace(item.Uris) ? item.Text ?? "" : item.Uris;

        static string Basename(string path)
        {
            string p = path.TrimEnd('/', '\\');
            int cut = Math.Max(p.LastIndexOf('\\'), p.LastIndexOf('/'));
            return cut >= 0 && cut < p.Length - 1 ? p.Substring(cut + 1) : p;
        }

        /// <summary>Estimated rendered lines, counting \r\n as ONE break (the badge's old +57 bug).</summary>
        static int WrappedLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            string sample = text.Length > EstimateChars ? text.Substring(0, EstimateChars) : text;
            int total = 0;
            foreach (string line in SplitLines(sample))
                total += (int)Math.Ceiling(Math.Max(1, line.Length) / CharsPerLine);
            return Math.Max(1, total);
        }

        /// <summary>One pass over \r\n | \r | \n — no Regex, this runs for every row on every reload.</summary>
        internal static List<string> SplitLines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\n' && c != '\r') continue;
                lines.Add(text.Substring(start, i - start));
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                start = i + 1;
            }
            lines.Add(text.Substring(start));
            return lines;
        }

        /// <summary>Truncation is always at the end (docs\DESIGN-typography.md §7).</summary>
        static string Truncate(string text, int cap) =>
            string.IsNullOrEmpty(text) || text.Length <= cap ? text : text.Substring(0, cap).TrimEnd() + "\u2026";
    }
}
