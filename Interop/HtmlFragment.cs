using System;
using System.Text;

namespace Clip.Interop
{
    /// <summary>
    /// Pulls the fragment out of the raw "Html Format" (CF_HTML) clipboard string.
    /// StartHTML/StartFragment/EndFragment are UTF-8 BYTE offsets, not .NET string indices:
    /// slicing the string directly mangles non-ASCII text and can cut tags in half, so all
    /// slicing happens on the UTF-8 byte array.
    /// </summary>
    internal static class HtmlFragment
    {
        public static string Extract(string cfHtml)
        {
            if (string.IsNullOrEmpty(cfHtml)) return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(cfHtml);

            // The header is a short ASCII block of "Name:value" lines ending where the HTML starts.
            int headerChars = cfHtml.IndexOf('<');
            if (headerChars < 0) headerChars = cfHtml.Length;

            int startHtml = -1, startFrag = -1, endFrag = -1;
            foreach (string line in cfHtml.Substring(0, headerChars).Split('\n'))
            {
                int colon = line.IndexOf(':');
                if (colon < 0 || !int.TryParse(line.Substring(colon + 1).Trim(), out int value)) continue;
                switch (line.Substring(0, colon).Trim().Trim('\uFEFF').ToLowerInvariant())
                {
                    case "starthtml": startHtml = value; break;
                    case "startfragment": startFrag = value; break;
                    case "endfragment": endFrag = value; break;
                }
            }

            string fragment;
            if (startFrag >= 0 && endFrag > startFrag)
                fragment = Decode(bytes, startFrag, endFrag);
            else
            {
                // Offsets missing or -1: hand back the whole body after the header.
                int bodyStart = startHtml >= 0 ? startHtml : Encoding.UTF8.GetByteCount(cfHtml.Substring(0, headerChars));
                fragment = Decode(bytes, bodyStart, bytes.Length);
            }
            return fragment.TrimEnd('\0');
        }

        static string Decode(byte[] bytes, int start, int end)
        {
            start = Math.Max(0, Math.Min(start, bytes.Length));
            end = Math.Max(start, Math.Min(end, bytes.Length));

            // A sloppy producer can end the fragment mid-character; drop at most one partial
            // sequence (UTF-8 is at most 4 bytes) instead of emitting a replacement char.
            var strict = new UTF8Encoding(false, true);
            for (int trim = 0; trim <= 3; trim++)
            {
                if (end <= start) return string.Empty;
                try { return strict.GetString(bytes, start, end - start); }
                catch (DecoderFallbackException) { end--; }
            }
            return Encoding.UTF8.GetString(bytes, start, end - start); // last resort, never throws
        }
    }
}
