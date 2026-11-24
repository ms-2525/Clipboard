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
    /// </summary>
    public sealed class ClipRow : INotifyPropertyChanged
    {
        // A row's text slot is capped by Config.PreviewLines (1..100, honored directly) — no render clamp.
        const int TooltipCap = 2000;
        const int EstimateChars = 20000;

        /// <summary>
        /// The estimator's chars-per-line, never measured per row: a measured height would twitch on
        /// font fallback and undo the frozen-geometry contract (§1.2). Calibrated for the content size:
        /// Inter 14 in the 610 px text column (660 DIP popup) measures 95 chars for typical
        /// prose (FormattedText, 150 % DPI). 92 = measured minus three chars of safety.
        /// </summary>
        public const double CharsPerLine = 92;

        readonly ClipboardStore _store;
        readonly string _text;
        readonly int _totalLines;
        readonly int _textCap;
        ClipboardItem _payload;
        bool _payloadRead;
        BitmapSource _thumb;

        public ClipRow(ClipboardItem item, int previewLines, ClipboardStore store)
        {
            Item = item;
            _store = store;
            _text = item.Text ?? "";
            IsImage = item.Kind == "image";
            IsFiles = item.Kind == "files";
            _totalLines = IsImage || IsFiles ? 1 : WrappedLines(_text);

            int lines = Math.Max(1, Math.Min(100, previewLines));
            int visible = Math.Max(1, Math.Min(lines, _totalLines));
            _textCap = lines * (int)CharsPerLine + (int)CharsPerLine;

            string preview = IsFiles ? FilePreview(item) : IsImage ? "Image  \u00b7  " + Size() : Truncate(_text, _textCap);
            Preview = preview;
            Tooltip = IsImage ? null : Truncate(IsFiles ? Paths(item) : _text, TooltipCap);

            TextHeight = visible * 20;
            RowHeight = TextHeight + 20 + FooterHeight;

            int hidden = _totalLines - visible;
            if (!IsFiles && !IsImage && preview.Length < _text.Length && hidden < 1) hidden = 1;
            if (!IsFiles && !IsImage && hidden > 0)
            {
                More = "+" + hidden;
            }
            Truncated = hidden > 0;

            Source = item.AppName ?? "";
            Title = item.WindowTitle ?? "";
            When = Stamp(item.CapturedAt);
        }

        public ClipboardItem Item { get; }
        public long Id => Item.Id;

        /// <summary>What the row's own text slot shows when no formatted body is available.</summary>
        public string Preview { get; }

        /// <summary>Full text (or every file path) — the row tooltip is its only home (directive §2).</summary>
        public string Tooltip { get; }

        /// <summary>Bottom bar provenance for the selected clip: app · window title · captured time.</summary>
        public string Source { get; }
        public string Title { get; }
        public string When { get; }

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
        public bool Truncated { get; }

        public double TextHeight { get; }
        public double RowHeight { get; }

        /// <summary>Divider block under the body: 8 px gap + 1 px line + 6 px gap + 16 px status (RowTemplate.xaml).</summary>
        const double FooterHeight = 31;

        public bool IsImage { get; }
        public bool IsFiles { get; }

        /// <summary>Rows that may carry an rtf/html body; images and files never do.</summary>
        public bool CanBeRich => !IsImage && !IsFiles;

        public string More { get; }
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
                if (_thumb != null || Item.Thumb == null || Item.Thumb.Length == 0) return _thumb;
                try
                {
                    using var ms = new MemoryStream(Item.Thumb);
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

        bool _showSection;
        public bool ShowSection
        {
            get => _showSection;
            set { if (_showSection == value) return; _showSection = value; Raise(nameof(ShowSection)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>"23 Nov 15:16" — the date is fixed English (owner's UI language), the clock follows config.</summary>
        static string Stamp(string stamp)
        {
            if (!DateTime.TryParse(stamp, out var when)) return "";
            string clock = string.IsNullOrEmpty(ClipboardItem.ClockFormat) ? "HH:mm" : ClipboardItem.ClockFormat;
            return when.ToString("dd MMM", System.Globalization.CultureInfo.InvariantCulture) + " " + when.ToString(clock);
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
