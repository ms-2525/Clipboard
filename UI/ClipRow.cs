using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clip.Core;

namespace Clip.UI
{
    /// <summary>
    /// One row of the popup: a precomputed <see cref="Preview"/> (never the full body — a 1 MB clip
    /// re-measures at 64–111 ms otherwise), the two meta facts, and the time-section flags.
    /// </summary>
    public sealed class ClipRow : INotifyPropertyChanged
    {
        // ~400 chars covers six 13 px lines; the selected row never renders more than that.
        const int PreviewCap = 400;
        const int TooltipCap = 2000;
        const double CharsPerLine = 62;

        readonly string _previewSource;
        BitmapSource _thumb;

        public ClipRow(ClipboardItem item, int previewLines)
        {
            Item = item;
            _previewSource = item.Text ?? "";
            Preview = item.Kind == "image" ? "" : Truncate(_previewSource, PreviewCap);
            FullText = Truncate(_previewSource, TooltipCap);
            MetaApp = string.IsNullOrWhiteSpace(item.AppName) ? "" : item.AppName;
            MetaTime = DateTime.TryParse(item.CapturedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when)
                ? when.ToString("dd MMM " + (string.IsNullOrEmpty(ClipboardItem.ClockFormat) ? "HH:mm" : ClipboardItem.ClockFormat), CultureInfo.InvariantCulture)
                : "";
            IsImage = item.Kind == "image";
            KindIcon = Application.Current?.TryFindResource(IsImage ? "IconPicture" : "IconLines") as Geometry;
            MoreLines = HiddenLines(previewLines);
        }

        public ClipboardItem Item { get; }
        public long Id => Item.Id;
        public string Preview { get; }
        public string FullText { get; }
        public string MetaApp { get; }
        public string MetaTime { get; }
        public bool IsImage { get; }
        public Geometry KindIcon { get; }

        public string BodyText => IsImage ? "Image  ·  " + Size() : Preview;

        public string MoreLines { get; }

        /// <summary>Visible lines the body actually needs (1–3), so a short clip does not open to blank space.</summary>
        public int LineCount => Math.Max(1, Math.Min(3, Preview.Split('\n').Length));

        public Visibility BadgeVisibility => MoreLines == null ? Visibility.Collapsed : Visibility.Visible;

        public string Tooltip => FullText;

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

        string HiddenLines(int previewLines)
        {
            if (IsImage || string.IsNullOrEmpty(_previewSource)) return null;
            int total = 0;
            foreach (var line in _previewSource.Replace("\r", "").Split('\n'))
                total += (int)Math.Ceiling(Math.Max(1, line.Length) / CharsPerLine);
            int visible = Math.Max(1, Math.Min(3, previewLines));
            int hidden = total - visible;
            if (_previewSource.Length > PreviewCap && hidden < 1) hidden = 1;
            return hidden > 0 ? "+" + hidden + (hidden == 1 ? " more line" : " more lines") : null;
        }

        /// <summary>Truncation is always at the end (docs\DESIGN-typography.md §7).</summary>
        static string Truncate(string text, int cap) =>
            string.IsNullOrEmpty(text) || text.Length <= cap ? text : text.Substring(0, cap).TrimEnd() + "…";
    }
}
