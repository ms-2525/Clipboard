using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clip.Core
{
    /// <summary>
    /// DESIGN-language.md §3.1: tags are derived at read time and only from a positive test. A
    /// heuristic that can be wrong returns nothing; a missing tag is acceptable, a wrong one is not.
    /// Allocation-light: the list calls this for up to 1,000 rows and the only allocation is the
    /// returned array for rows that actually carry a tag.
    /// </summary>
    public static class ClipTags
    {
        // ---- text shapes ----------------------------------------------------------------------

        static readonly Regex EmailRe = new Regex(
            @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // No whitespace is allowed: a path with a space is indistinguishable from prose that starts
        // with a path ("C:\x.txt. Then read it"), and a missing tag is the allowed failure.
        static readonly Regex PathRe = new Regex(
            @"^(?:[A-Za-z]:\\[^\s<>:""|?*]+|\\\\[^\\/\s]+\\[^\\/\s]+(?:\\[^\s<>:""|?*]*)?)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex ColorRe = new Regex(
            @"^(?:#[0-9A-Fa-f]{3}|#[0-9A-Fa-f]{6}|(?:rgb|hsl)\(\s*[0-9.%+\-\s,/]+\))$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        static readonly Regex HtmlRe = new Regex(
            @"^\s*<(?:!doctype\s+html|(?:html|head|body|div|span|p|a|table|thead|tbody|tr|td|th|ul|ol|li|h[1-6]|b|i|strong|em|br|img|script|style|meta|link|title|section|article|header|footer|nav|form|input|button|svg)(?:\s[^>]*)?)>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // A bare verb is too easy to meet in prose ("Delete from the list"), so the statement shape is
        // part of the proof. A query this strict can miss terse SQL, which is the allowed failure.
        static readonly Regex SqlRe = new Regex(
            @"^\s*(?:SELECT\s+(?:\*|[\w.$]+(?:\s*,\s*[\w.$]+)*)\s+FROM\s+[\w.$]+|INSERT\s+INTO\s+[\w.$]+\s*(?:\(|VALUES\b|SELECT\b)|UPDATE\s+[\w.$]+\s+SET\s+[\w.$]+\s*=|DELETE\s+FROM\s+[\w.$]+\s*(?:WHERE\b|;|$)|CREATE\s+(?:OR\s+REPLACE\s+)?(?:TABLE|VIEW|INDEX|DATABASE)\s+[\w.$]+|ALTER\s+TABLE\s+[\w.$]+|DROP\s+(?:TABLE|VIEW|INDEX|DATABASE)\s+[\w.$]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        static readonly Regex XmlRe = new Regex(
            @"^\s*<\?xml\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // ---- language signatures --------------------------------------------------------------

        static readonly Regex CSharpRe = new Regex(
            @"(?m)^\s*using\s+System(?:\.\w+)*\s*;|^\s*namespace\s+[A-Z]\w*(?:\.[A-Za-z_]\w*)*|^\s*(?:public|internal)\s+(?:(?:sealed|static|abstract|partial)\s+)*(?:class|record|struct|interface)\s+[A-Z]\w*|^\s*Console\.WriteLine\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        static readonly Regex TypeScriptRe = new Regex(
            @"\b(?:interface|type)\s+[A-Z]\w*\s*[={]|:\s*(?:string|number|boolean|unknown|any|void)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex JavaScriptRe = new Regex(
            @"\bfunction\s*(?:[A-Za-z_$][\w$]*\s*)?\(|\b(?:const|let|var)\s+[A-Za-z_$][\w$]*\s*=\s*[^;\r\n]*=>|\([^()\r\n]*\)\s*=>|\bconsole\.log\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex PythonRe = new Regex(
            @"(?m)^\s*(?:def\s+[A-Za-z_]\w*\s*\(|class\s+[A-Z]\w*\s*[:(]|import\s+[a-z_][\w.]*|from\s+[a-z_][\w.]*\s+import\s+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        static readonly Regex RustRe = new Regex(
            @"\bfn\s+main\s*\(|\bfn\s+[a-z_]\w*\s*\([^)\r\n]*\)\s*->|\buse\s+std::",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex PhpRe = new Regex(
            @"<\?php\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        static readonly Regex GoRe = new Regex(
            @"(?m)^\s*package\s+main\b|\bfunc\s+main\s*\(|\bfunc\s+[a-z_]\w*\s*\(|^\s*import\s+\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        static readonly Regex JavaRe = new Regex(
            @"\bimport\s+java\.|\bpublic\s+static\s+void\s+main\s*\(|\bSystem\.out\.println\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex ShellRe = new Regex(
            @"\A#!.*\b(?:bash|zsh|ksh|dash|sh)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // ---- classify -------------------------------------------------------------------------

        /// <summary>
        /// Returns proven camelCase tags in the §3.1 order, or an empty shared array when unsure.
        /// <paramref name="formats"/> is the format-name list a list row already carries; when null,
        /// the item's own format tuples are used.
        /// </summary>
        public static string[] Classify(ClipboardItem item, IReadOnlyList<string> formats)
        {
            if (item == null) return Array.Empty<string>();
            List<string> tags = null;

            if (string.Equals(item.Kind, "image", StringComparison.OrdinalIgnoreCase) ||
                (item.Image != null && item.Image.Length > 0))
                tags = Add(tags, "image");

            bool fileDrop = false, spreadsheet = false;
            if (formats != null)
            {
                for (int i = 0; i < formats.Count; i++)
                    ScanFormat(formats[i], ref fileDrop, ref spreadsheet);
            }
            else if (item.Formats != null)
            {
                foreach (var format in item.Formats)
                    ScanFormat(format.Item1, ref fileDrop, ref spreadsheet);
            }

            if (fileDrop) tags = Add(tags, "files");
            if (spreadsheet) tags = Add(tags, "spreadsheet");

            if (string.IsNullOrWhiteSpace(item.Text)) return Result(tags);
            string text = item.Text.Trim();
            bool shape = true;

            if (IsSingleUrl(text)) tags = Add(tags, "link");
            else if (EmailRe.IsMatch(text)) tags = Add(tags, "email");
            else if (PathRe.IsMatch(text)) tags = Add(tags, "path");
            else if (ColorRe.IsMatch(text)) tags = Add(tags, "color");
            else if (IsNumber(text)) tags = Add(tags, "number");
            else if (IsJson(text)) tags = Add(tags, "json");
            else if (HtmlRe.IsMatch(text)) tags = Add(tags, "html");
            else if (SqlRe.IsMatch(text)) tags = Add(tags, "sql");
            else if (XmlRe.IsMatch(text)) tags = Add(tags, "xml");
            else shape = false;

            // One language only: a snippet is one language, and a structural tag already explains the
            // text (an HTML page with a script is html, not javascript).
            if (!shape)
            {
                string language = Language(text);
                if (language != null) tags = Add(tags, language);
            }

            return Result(tags);
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

        static bool IsSingleUrl(string text)
        {
            // Cheap prefix gate first: Uri.TryCreate would build a Uri for every prose row.
            if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
            for (int i = 0; i < text.Length; i++)
                if (char.IsWhiteSpace(text[i])) return false;
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return false;
            return uri.Host.Length > 0 &&
                (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        static bool IsNumber(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
            double.IsFinite(value);

        /// <summary>
        /// Only an object or array counts. A raw number/string is valid JSON too, but tagging "42" as
        /// json would call a number a document; number/colour already own those shapes.
        /// </summary>
        static bool IsJson(string text)
        {
            if (text.Length < 2 || (text[0] != '{' && text[0] != '[')) return false;
            try
            {
                using var doc = JsonDocument.Parse(text);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        static string Language(string text)
        {
            if (CSharpRe.IsMatch(text)) return "csharp";
            if (TypeScriptRe.IsMatch(text)) return "typescript";
            if (JavaScriptRe.IsMatch(text)) return "javascript";
            if (PythonRe.IsMatch(text)) return "python";
            if (RustRe.IsMatch(text)) return "rust";
            if (PhpRe.IsMatch(text)) return "php";
            if (GoRe.IsMatch(text)) return "go";
            if (JavaRe.IsMatch(text)) return "java";
            if (ShellRe.IsMatch(text)) return "shell";
            return null;
        }

        static List<string> Add(List<string> tags, string tag)
        {
            (tags ??= new List<string>(4)).Add(tag);
            return tags;
        }

        static string[] Result(List<string> tags) => tags == null ? Array.Empty<string>() : tags.ToArray();
    }
}
