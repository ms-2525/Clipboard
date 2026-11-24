using System;
using System.Collections.Generic;
using System.Text;

namespace Clip.Core
{
    /// <summary>
    /// The editor's Aa cycle: lowercase → UPPERCASE → Title Case. Every variant maps one char to one
    /// char, so a formatted document can take the result one run at a time without shifting offsets.
    /// </summary>
    public static class TextCase
    {
        public const int Variants = 3;

        /// <summary>Words a title leaves lowercase unless they open it (the short-connector set).</summary>
        static readonly HashSet<string> Minor = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "as", "at", "but", "by", "for", "if", "in", "is", "of", "on", "or",
            "so", "the", "to", "via", "per", "with", "yet", "nor"
        };

        public static string Apply(string text, int variant)
        {
            text ??= "";
            return variant switch
            {
                0 => text.ToLowerInvariant(),
                1 => text.ToUpperInvariant(),
                _ => Title(text),
            };
        }

        /// <summary>Every word capitalised except the minor connectors; the first word always is.</summary>
        public static string Title(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool firstWord = true;
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (!char.IsLetter(c))
                {
                    // A new paragraph's first word counts as first, like the clip's own first word.
                    if (c == '\n' || c == '\r') firstWord = true;
                    sb.Append(c);
                    i++;
                    continue;
                }

                int start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '\'' || text[i] == '\u2019')) i++;
                string word = text.Substring(start, i - start);
                bool cap = firstWord || !Minor.Contains(word);
                sb.Append(cap ? char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant() : word.ToLowerInvariant());
                firstWord = false;
            }
            return sb.ToString();
        }
    }
}
