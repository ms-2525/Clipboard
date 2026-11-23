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
        // A row renders at most six 18 px lines; 400 chars covers them (docs\DESIGN-v2-ui.md §1.2).
        const int PreviewCap = 400;
        const int TooltipCap = 2000;
        const int MaxLines = 6;
        const int EstimateChars = 20000;

        /// <summary>
        /// The estimator's chars-per-line, never measured per row: a measured height would twitch on
        /// font fallback and undo the frozen-geometry contract (§1.2). Calibrated for the NEW content
        /// font: Segoe UI 13 in the 482 px text column measures 79 chars for typical prose, 72 for
        /// URLs/digits (FormattedText, 150 % DPI); 62 was the Manrope-era figure and left a blank line
        /// on most rows. 77 = measured prose minus two chars of safety.
        /// </summary>
        public const double CharsPerLine = 77;

        readonly ClipboardStore _store;
        readonly string _text;
        readonly int _totalLines;
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

            Preview = IsFiles ? FilePreview(item) : IsImage ? "Image  \u00b7  " + Size() : Truncate(_text, PreviewCap);
            Tooltip = IsImage ? null : Truncate(IsFiles ? Paths(item) : _text, TooltipCap);

            int visible = Math.Max(1, Math.Min(Math.Min(previewLines, MaxLines), _totalLines));
            TextHeight = visible * 18;
            RowHeight = TextHeight + 16;

            int hidden = _totalLines - visible;
            if (!IsFiles && !IsImage && _text.Length > PreviewCap && hidden < 1) hidden = 1;
            if (!IsFiles && !IsImage && hidden > 0)
            {
                More = "+" + hidden;
                MoreTip = hidden + (hidden == 1 ? " more line not shown" : " more lines not shown");
            }
        }

        public ClipboardItem Item { get; }
        public long Id => Item.Id;

        /// <summary>What the row's own text slot shows when no formatted body is available.</summary>
        public string Preview { get; }

        /// <summary>Full text (or every file path) — the row tooltip is its only home (directive §2).</summary>
        public string Tooltip { get; }

        public double TextHeight { get; }
        public double RowHeight { get; }

        public bool IsImage { get; }
        public bool IsFiles { get; }

        /// <summary>Rows that may carry an rtf/html body; images and files never do.</summary>
        public bool CanBeRich => !IsImage && !IsFiles;

        public string More { get; }
        public string MoreTip { get; }
        public Visibility MoreVisibility => More == null ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>An empty tooltip must not open as an empty box (image rows have none).</summary>
        public bool HasTooltip => !string.IsNullOrEmpty(Tooltip);

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
