using System;
using System.Text;

namespace Clip.Core
{
    /// <summary>
    /// RTF is a BYTE format riding in a TEXT column. Latin-1 is the only encoding that maps every
    /// byte 0x00–0xFF to exactly one char and back, so an 8-bit RTF (\'hh sequences, WordPad-era)
    /// round-trips untouched; UTF-8 would re-encode 0x80–0xFF into two bytes and the loader then
    /// reads mojibake or throws. Rows the old UTF-8 path already touched get a second load attempt
    /// (<see cref="ToUtf8Bytes"/>); if that fails too the caller says so visibly, never silently.
    /// </summary>
    internal static class RtfText
    {
        /// <summary>Load attempt 1: the Latin-1 envelope — byte-exact for ASCII and 8-bit RTF.</summary>
        public static byte[] ToBytes(string rtf)
        {
            if (string.IsNullOrEmpty(rtf)) return Array.Empty<byte>();
            // A char above 0xFF cannot live in Latin-1; that row was never an 8-bit body, so UTF-8 is lossless.
            foreach (char c in rtf)
                if (c > '\u00FF') return Encoding.UTF8.GetBytes(rtf);
            return Encoding.Latin1.GetBytes(rtf);
        }

        /// <summary>Load attempt 2: differs from <see cref="ToBytes"/> only for a row the old path re-encoded.</summary>
        public static byte[] ToUtf8Bytes(string rtf) =>
            string.IsNullOrEmpty(rtf) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(rtf);

        /// <summary>The one save-side encoding: TextRange.Save bytes → string, byte-exact.</summary>
        public static string FromBytes(byte[] bytes) =>
            bytes == null || bytes.Length == 0 ? string.Empty : Encoding.Latin1.GetString(bytes);
    }
}
