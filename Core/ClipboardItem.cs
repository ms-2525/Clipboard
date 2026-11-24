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
        /// <summary>Full length of the stored body. A list row carries only <see cref="Text"/>'s preview, so
        /// <c>TextLength &gt; Text.Length</c> means the rest must be loaded before paste/edit.</summary>
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

        /// <summary>Time segment of <see cref="Meta"/>; "HH:mm" or "hh:mm tt", set from config.</summary>
        public static string ClockFormat = "HH:mm";

        /// <summary>
        /// Ordering is lexical (ORDER BY captured_at DESC, id DESC), so every writer must emit exactly this shape.
        /// Milliseconds keep a re-copy inside the same second above the copy it replaced; older second-resolution
        /// rows still sort before them ("…:23" &lt; "…:23.500" &lt; "…:24").
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
