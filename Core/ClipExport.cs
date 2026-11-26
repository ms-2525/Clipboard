using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Clip.Interop;

namespace Clip.Core
{
    /// <summary>What a stored clip can be saved as. Decided once from kind + the format names it carries.</summary>
    public enum ClipExportKind
    {
        Text,
        Spreadsheet,
        Image,
        Files
    }

    /// <summary>
    /// One export decision: the extension to offer, the exact bytes to write, or the sentence explaining why
    /// this clip cannot be saved. The host owns the dialog and the path; this object is pure data.
    /// </summary>
    public sealed class ClipExportResult
    {
        public ClipExportKind Kind { get; init; }
        /// <summary>Lower-case extension including the dot: ".txt", ".md", ".csv" or ".png". Null on error.</summary>
        public string Extension { get; init; }
        /// <summary>Null only when <see cref="Error"/> is set.</summary>
        public byte[] Bytes { get; init; }
        /// <summary>Null on success. A file list is the expected non-error case that still has nothing to save.</summary>
        public string Error { get; init; }
        public bool Ok => Error == null;
    }

    /// <summary>
    /// Converts one clip to the bytes "Export" writes. Frozen rules (STEP-PLAN export, 25/11/2026):
    /// text and markdown are the same UTF-8 bytes without a BOM; a spreadsheet exports as CSV from the
    /// clipboard's tab-separated text, UTF-8 WITH a BOM and never a "sep=" line; an image is the stored PNG
    /// untouched; a file list has nothing to save. No IO here except <see cref="Write"/>.
    /// </summary>
    public static class ClipExport
    {
        /// <summary>
        /// Cap on the offered name's stem. MAX_PATH is 260 including the folder the user picked, so 80 leaves
        /// well over 100 characters of headroom and stays under the 255-character NTFS name limit.
        /// </summary>
        public const int MaxStem = 80;

        static readonly char[] InvalidNameChars = "<>:\"/\\|?*".ToCharArray();

        static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        // Text is UTF-8 WITHOUT a BOM (frozen for this step); the CSV encoder below prepends EF BB BF.
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };
        static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // ---- kind ----------------------------------------------------------------------------

        /// <summary>
        /// Files first (a dropped list also carries text), then a real image, then a spreadsheet proven by its
        /// native formats, else text. <paramref name="formats"/> is the row's format-name list; when null the
        /// item's own tuples are used, so a harness can pass a bare ClipboardItem.
        /// </summary>
        public static ClipExportKind KindOf(ClipboardItem item, IReadOnlyList<string> formats)
        {
            bool fileDrop = false, spreadsheet = false;
            if (formats != null)
            {
                for (int i = 0; i < formats.Count; i++) ScanFormat(formats[i], ref fileDrop, ref spreadsheet);
            }
            else if (item?.Formats != null)
            {
                foreach (var format in item.Formats) ScanFormat(format.Item1, ref fileDrop, ref spreadsheet);
            }

            if (fileDrop || string.Equals(item?.Kind, "files", StringComparison.OrdinalIgnoreCase)) return ClipExportKind.Files;
            if (string.Equals(item?.Kind, "image", StringComparison.OrdinalIgnoreCase) ||
                (item?.Image != null && item.Image.Length > 0)) return ClipExportKind.Image;
            // An edited grid outlives its Biff blobs (dropped on save as stale): the kind carries it.
            if (string.Equals(item?.Kind, "sheet", StringComparison.OrdinalIgnoreCase)) return ClipExportKind.Spreadsheet;
            return spreadsheet ? ClipExportKind.Spreadsheet : ClipExportKind.Text;
        }

        static void ScanFormat(string name, ref bool fileDrop, ref bool spreadsheet)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (name.Equals("FileDrop", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("CF_HDROP", StringComparison.OrdinalIgnoreCase))
                fileDrop = true;
            else if (name.Equals("Biff12", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("Biff8", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("Biff5", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("XML Spreadsheet", StringComparison.OrdinalIgnoreCase))
                spreadsheet = true;
        }

        // ---- bytes ---------------------------------------------------------------------------

        /// <summary>
        /// The bytes for <paramref name="format"/>: "auto" picks the kind's one natural extension, or a concrete
        /// "txt"/"md"/"csv"/"png" (a leading dot and any case are accepted). A request that contradicts the
        /// kind is refused here, before the dialog or the file system is touched.
        /// </summary>
        public static ClipExportResult Prepare(ClipboardItem item, IReadOnlyList<string> formats, string format)
        {
            var kind = KindOf(item, formats);
            string requested = (format ?? "auto").Trim().TrimStart('.').ToLowerInvariant();
            if (requested != "auto" && requested != "txt" && requested != "md" &&
                requested != "csv" && requested != "png")
                return Fail(kind, "Unknown format \"" + format + "\". Choose auto, txt, md, csv or png.");

            switch (kind)
            {
                case ClipExportKind.Files:
                    return Fail(kind, "This clip is a list of files, not a document: there is nothing to save.");

                case ClipExportKind.Image:
                    if (requested != "auto" && requested != "png")
                        return Fail(kind, "This clip is an image; save it as .png.");
                    if (item?.Image == null || item.Image.Length == 0)
                        return Fail(kind, "This image is no longer stored.");
                    if (!IsPng(item.Image))
                        return Fail(kind, "The stored image is not a PNG file.");
                    return Ok(kind, ".png", item.Image);

                case ClipExportKind.Spreadsheet:
                    if (requested != "auto" && requested != "csv")
                        return Fail(kind, "This spreadsheet clip saves as .csv.");
                    if (string.IsNullOrEmpty(item?.Text))
                        return Fail(kind, "This spreadsheet clip has no copied cell text to save.");
                    return Ok(kind, ".csv", CsvBytes(item.Text));

                default:
                    if (requested == "csv" || requested == "png")
                        return Fail(kind, "This clip is text; save it as .txt or .md.");
                    string ext = requested == "md" ? ".md" : ".txt";
                    if (requested == "md")
                    {
                        // A rich clip keeps its structure in .md; a plain clip falls through unchanged.
                        string markdown = RichMarkdown(item);
                        if (markdown != null) return Ok(kind, ext, Utf8.GetBytes(markdown));
                    }
                    if (string.IsNullOrEmpty(item?.Text))
                        return Fail(kind, "This clip has no text to save.");
                    // .txt is always the stored text, exactly as copied.
                    return Ok(kind, ext, Utf8.GetBytes(item.Text));
            }
        }

        static ClipExportResult Ok(ClipExportKind kind, string extension, byte[] bytes) =>
            new ClipExportResult { Kind = kind, Extension = extension, Bytes = bytes };

        static ClipExportResult Fail(ClipExportKind kind, string error) =>
            new ClipExportResult { Kind = kind, Error = error };

        /// <summary>Rich HTML as Markdown for ".md" exports; null when there is no HTML or it converts to nothing.</summary>
        static string RichMarkdown(ClipboardItem item)
        {
            if (string.IsNullOrEmpty(item?.Html)) return null;
            string fragment;
            try { fragment = HtmlFragment.Extract(item.Html); }
            catch { return null; } // sloppy producer offsets: the plain text below still saves
            if (string.IsNullOrEmpty(fragment)) return null;
            string markdown;
            try { markdown = HtmlToMarkdown.Convert(fragment); }
            catch { return null; } // a hostile fragment must not break the save
            return string.IsNullOrWhiteSpace(markdown) ? null : markdown;
        }

        static bool IsPng(byte[] bytes)
        {
            if (bytes == null || bytes.Length < PngSignature.Length) return false;
            for (int i = 0; i < PngSignature.Length; i++)
                if (bytes[i] != PngSignature[i]) return false;
            return true;
        }

        // ---- file name -----------------------------------------------------------------------

        /// <summary>
        /// The name the Save dialog offers: first line of the text, every character Windows rejects (and every
        /// control) turned into a space, surrounding spaces/dots trimmed, capped at <see cref="MaxStem"/>
        /// characters on a code-point boundary, reserved device names prefixed "_", and the fixed extension for
        /// the kind appended. Empty, whitespace-only and image clips fall back to "<product> yyyy-MM-dd HH-mm".
        /// </summary>
        public static string SuggestedName(string text, string extension, DateTime capturedAt)
        {
            if (!string.IsNullOrEmpty(extension) && extension[0] != '.') extension = "." + extension;
            string stem = Clean(FirstLine(text));
            if (stem.Length == 0)
                stem = ProductStem + " " + capturedAt.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture);
            return stem + extension;
        }

        /// <summary>Assembly name, like the data folder: one rename hook, never a product literal.</summary>
        internal static string ProductStem => Assembly.GetEntryAssembly()?.GetName().Name ?? "Clip";

        static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            int start = 0;
            while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
            int end = start;
            while (end < text.Length && text[end] != '\r' && text[end] != '\n') end++;
            return text.Substring(start, end - start);
        }

        static string Clean(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;

            var sb = new StringBuilder(line.Length);
            foreach (char c in line)
            {
                // One space per bad character keeps the words apart: "a/b" -> "a b", not "ab".
                if (char.IsControl(c) || Array.IndexOf(InvalidNameChars, c) >= 0)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    continue;
                }
                sb.Append(c);
            }

            string s = sb.ToString().Trim().Trim('.', ' ');

            // The cap is on code points, so a surrogate pair (an emoji) is dropped whole, never halved.
            if (s.Length > MaxStem)
            {
                int cut = MaxStem;
                if (char.IsHighSurrogate(s[cut - 1])) cut--;
                s = s.Substring(0, cut).TrimEnd('.', ' ');
            }

            if (s.Length == 0) return string.Empty;

            // Windows reserves a device name with ANY extension: "CON.txt" is still CON.
            int dot = s.IndexOf('.');
            string stem = dot < 0 ? s : s.Substring(0, dot);
            if (ReservedNames.Contains(stem.TrimEnd(' '))) s = "_" + s;
            return s;
        }

        // ---- write ---------------------------------------------------------------------------

        /// <summary>
        /// Writes the chosen path. The caller must already hold the user's confirmation (Save dialog returned
        /// true, overwrite prompt answered) — this method never asks and never picks a path itself.
        /// </summary>
        public static void Write(string path, byte[] bytes)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A save path is required.");
            if (bytes == null) throw new ArgumentException("There is nothing to write.");
            File.WriteAllBytes(path, bytes);
        }

        // ---- spreadsheet TSV -> CSV ----------------------------------------------------------

        /// <summary>Clipboard TSV -> RFC 4180 CSV with a UTF-8 BOM. Never writes a "sep=" line (it defeats the BOM in Excel).</summary>
        static byte[] CsvBytes(string tsv)
        {
            string csv = TsvToCsv(tsv);
            byte[] body = Utf8.GetBytes(csv);
            var bytes = new byte[Bom.Length + body.Length];
            Buffer.BlockCopy(Bom, 0, bytes, 0, Bom.Length);
            Buffer.BlockCopy(body, 0, bytes, Bom.Length, body.Length);
            return bytes;
        }

        static string TsvToCsv(string tsv)
        {
            if (string.IsNullOrEmpty(tsv)) return string.Empty;
            var rows = ParseRows(tsv);
            var sb = new StringBuilder(tsv.Length + 32);
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    AppendField(sb, row[i]);
                }
                sb.Append("\r\n"); // every record, including the last; trailing empty cells stay
            }
            return sb.ToString();
        }

        /// <summary>
        /// RFC 4180 quoting: comma, quote, CR, LF and TAB are quoted (a tab would otherwise be mistaken for the
        /// TSV separator), and the quotes are doubled. Edge spaces are quoted too so a reader that trims
        /// unquoted fields cannot silently drop the padding.
        /// </summary>
        static void AppendField(StringBuilder sb, string field)
        {
            bool quote = field.Length > 0 && (field[0] == ' ' || field[field.Length - 1] == ' ');
            if (!quote)
            {
                for (int i = 0; i < field.Length; i++)
                {
                    char c = field[i];
                    if (c == ',' || c == '"' || c == '\r' || c == '\n' || c == '\t') { quote = true; break; }
                }
            }
            if (!quote) { sb.Append(field); return; }

            sb.Append('"');
            for (int i = 0; i < field.Length; i++)
            {
                if (field[i] == '"') sb.Append('"');
                sb.Append(field[i]);
            }
            sb.Append('"');
        }

        /// <summary>
        /// True when the quote at <paramref name="open"/> opens a quoted field. Excel only quotes a field that
        /// contains a tab or a line break, so a leading quote without one is literal data (a cell that merely
        /// starts with a quotation mark) and must not swallow the next cell.
        /// </summary>
        static bool HasClosingQuote(string s, int open)
        {
            bool quotedContent = false;
            for (int i = open + 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\t' || c == '\r' || c == '\n') quotedContent = true;
                if (c != '"') continue;
                if (i + 1 < s.Length && s[i + 1] == '"') { i++; continue; }
                if (!quotedContent) return false;
                if (i + 1 == s.Length || s[i + 1] == '\t' || s[i + 1] == '\r' || s[i + 1] == '\n') return true;
            }
            return false;
        }

        static List<List<string>> ParseRows(string s)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            int i = 0;
            bool rowHasContent = false;

            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"' && field.Length == 0 && HasClosingQuote(s, i))
                {
                    // Quoted field: consumes to the closing quote; "" is a literal quote.
                    i++;
                    while (i < s.Length)
                    {
                        if (s[i] == '"')
                        {
                            if (i + 1 < s.Length && s[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                            i++; // closing quote
                            break;
                        }
                        field.Append(s[i]);
                        i++;
                    }
                    rowHasContent = true;
                    continue;
                }

                if (c == '\t')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rowHasContent = true;
                    i++;
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    rowHasContent = false;
                    i += (c == '\r' && i + 1 < s.Length && s[i + 1] == '\n') ? 2 : 1;
                    continue;
                }

                field.Append(c);
                rowHasContent = true;
                i++;
            }

            // A single trailing line break ends the last row; it does not add an empty one.
            if (rowHasContent || field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
