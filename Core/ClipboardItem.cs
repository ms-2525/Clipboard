using System;
using System.Collections.Generic;
using System.Globalization;

namespace Clip.Core
{
    public class ClipboardItem
    {
        public long Id { get; set; }
        public string Kind { get; set; }
        public string Text { get; set; }
        /// <summary>Length of the stored body in SQLite code points (never UTF-16 units); 0 when this object
        /// already carries the whole body. <see cref="IsTruncated"/> is the only test that reads it.</summary>
        public long TextLength { get; set; }
        public string Html { get; set; }
        public string Rtf { get; set; }
        public byte[] Image { get; set; }
        public byte[] Thumb { get; set; }
        /// <summary>Newline-joined URI/path list for kind "files"; also mirrored into Text so search works.</summary>
        public string Uris { get; set; }
        /// <summary>Caller loads via ClipboardStore.GetFormats(id); restored as every original format on paste.</summary>
        public List<(string fmt, byte[] bytes)> Formats { get; set; }
        public string AppName { get; set; }
        public string WindowTitle { get; set; }
        public string CapturedAt { get; set; }
        public long SizeBytes { get; set; }
        /// <summary>Container (Pinned or a custom tab) the item currently sits in; null = main history.</summary>
        public long? TabId { get; set; }

        /// <summary>
        /// True when a list row carries only the first <see cref="ClipboardStore.PreviewChars"/> code points
        /// of the stored body. The test must use the stored length: comparing <see cref="TextLength"/> to
        /// <c>Text.Length</c> mixes SQLite code points with UTF-16 units, so a preview dense in emoji can be
        /// LONGER in UTF-16 than the whole body and the clip looks complete (AUDIT P0). Paste, edit and
        /// search must load the full body whenever this is true.
        /// </summary>
        public bool IsTruncated => TextLength > ClipboardStore.PreviewChars;

        /// <summary>Stored code points past the preview; 0 unless <see cref="IsTruncated"/>.</summary>
        public long RemainingChars => IsTruncated ? TextLength - ClipboardStore.PreviewChars : 0;

        /// <summary>Time segment of <see cref="Meta"/>; "HH:mm" or "hh:mm tt", set from config.</summary>
        public static string ClockFormat = "HH:mm";

        /// <summary>
        /// Display/grouping timestamp shape. List order is <c>sort_seq DESC, id DESC</c> (a monotonic key), never
        /// this string, because a DST fall-back makes wall-clock time go backwards. Milliseconds keep the footer
        /// truthful when two clips land in the same second.
        /// </summary>
        internal const string StampFormat = "yyyy-MM-ddTHH:mm:ss.fff";

        internal static string Stamp() => DateTime.Now.ToString(StampFormat);

        /// <summary>Card footer: "App  ·  23 Nov 09:47  ·  window title".</summary>
        public string Meta
        {
            get
            {
                var parts = new List<string>
                {
                    string.IsNullOrWhiteSpace(AppName) ? "Unknown" : AppName
                };
                if (DateTime.TryParse(CapturedAt, out var when))
                    parts.Add(when.ToString("dd MMM " + ClockFormat, CultureInfo.InvariantCulture));
                if (!string.IsNullOrWhiteSpace(WindowTitle))
                    parts.Add(WindowTitle.Length > 60 ? WindowTitle.Substring(0, 60) + "…" : WindowTitle);
                return string.Join("  ·  ", parts);
            }
        }
    }
}
