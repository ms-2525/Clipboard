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
    /// Allocation-light: the list calls this for up to 1,000 rows and a tagged row allocates one
    /// short list plus the returned array.
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
        // DDL needs its terminator: "Drop table reservations, please." is prose, not SQL.
        static readonly Regex SqlRe = new Regex(
            @"^\s*(?:SELECT\s+(?:\*|[\w.$]+(?:\s*,\s*[\w.$]+)*)\s+FROM\s+[\w.$]+|INSERT\s+INTO\s+[\w.$]+\s*(?:\(|VALUES\b|SELECT\b)|UPDATE\s+[\w.$]+\s+SET\s+[\w.$]+\s*=|DELETE\s+FROM\s+[\w.$]+\s*(?:WHERE\b|;|$)|(?:CREATE\s+(?:OR\s+REPLACE\s+)?(?:TABLE|VIEW|INDEX|DATABASE)|ALTER\s+TABLE|DROP\s+(?:TABLE|VIEW|INDEX|DATABASE))\s+[\w.$]+[^;\r\n]*;)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        static readonly Regex XmlRe = new Regex(
            @"^\s*<\?xml\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>
        /// Tags that are only proven by the whole body. A list row carries PreviewChars, and a cut
        /// tail can turn a path followed by prose into "a path" or a partial JSON object into JSON.
        /// The prefix shapes (html, xml, sql) and the line-anchored language rules survive a preview.
        /// </summary>
        static readonly string[] WholeTextTags =
        {
            "link", "email", "path", "color", "number", "json"
        };

        /// <summary>True when this tag may only be emitted for a row that carries the whole body.</summary>
        static bool NeedsWholeText(string tag)
        {
            for (int i = 0; i < WholeTextTags.Length; i++)
                if (string.Equals(WholeTextTags[i], tag, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>The shape rules in §3.1 order; the first proven tag wins.</summary>
        static readonly (string Tag, Func<string, bool> Test)[] ShapeRules =
        {
            ("link", IsSingleUrl),
            ("email", IsEmail),
            ("path", PathRe.IsMatch),
            ("color", ColorRe.IsMatch),
            ("number", IsNumber),
            ("json", IsJson),
            ("html", HtmlRe.IsMatch),
            ("sql", SqlRe.IsMatch),
            ("xml", XmlRe.IsMatch),
        };

        // ---- language signatures --------------------------------------------------------------
        // A tag needs code shape, not code words. "Cheque status: void", "from bank import
        // statement" and "This function (callback) runs" are ordinary prose, so a keyword or a
        // vocabulary word is never proof on its own; a snippet these rules miss is the allowed
        // failure (§3.1), a prose sentence they tag is not.

        static readonly Regex CSharpRe = new Regex(
            @"(?m)^[ \t]*using\s+System(?:\.\w+)*[ \t]*;|^[ \t]*namespace\s+[A-Z]\w*(?:\.[A-Za-z_]\w*)*|^[ \t]*(?:public|internal)[ \t]+(?:(?:sealed|static|abstract|partial)[ \t]+)*(?:class|record|struct|interface)\s+[A-Z]\w*|\bConsole\.WriteLine\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        static readonly Regex TypeScriptRe = new Regex(
            @"\binterface\s+[A-Z]\w*(?:[ \t]*<[^<>\r\n]+>)?[ \t]*\{|\benum\s+[A-Z]\w*[ \t]*\{|^[ \t]*(?:export[ \t]+)?type\s+[A-Z]\w*(?:[ \t]*<[^<>\r\n]+>)?[ \t]*=|\bimplements\s+[A-Z]\w*|:[ \t]*Promise[ \t]*<|:[ \t]*(?:string|number|boolean)[ \t]*\[\]|\bas[ \t]+const\b|^[ \t]*import\s+[^;\r\n]*[ \t]+from[ \t]+['""][^'""\r\n]+['""]|\b(?:class|function|const|let|var)\s+[A-Za-z_$][\w$]*[ \t]*<[A-Z]\w*(?:\s+extends\s+[^<>\r\n]+)?>[ \t]*[({=]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        static readonly Regex JavaScriptRe = new Regex(
            @"\bfunction\s*(?:[A-Za-z_$][\w$]*[ \t]*)?\([^)\r\n]*\)[ \t]*\{|=>[ \t]*[{(]|\b(?:const|let|var)\s+[A-Za-z_$][\w$]*[ \t]*=[ \t]*require\s*\(|\bconsole\.log\s*\(|\bdocument\.(?:getElementById|querySelector|querySelectorAll|addEventListener|createElement|body|head|write)\b|\baddEventListener\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex PythonRe = new Regex(
            @"(?m)^[ \t]*def\s+[A-Za-z_]\w*[ \t]*\([^)\r\n]*\)[ \t]*(?:->[^:\r\n]+)?:[ \t]*\r?\n[ \t]+\S|^[ \t]*if\s+__name__\s*==\s*['""]__main__['""]|\bself\.|^[ \t]*import\s+[a-z_][\w.]*(?:\s*,\s*[a-z_][\w.]*)*\s*(?:#.*)?$|^[ \t]*from\s+[a-z_][\w.]*\s+import\s+[A-Za-z_*][\w.*]*(?:\s*,\s*[A-Za-z_*][\w.*]*)*\s*(?:#.*)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        static readonly Regex RustRe = new Regex(
            @"\bfn\s+main\s*\(|\bfn\s+[a-z_]\w*\s*\([^)\r\n]*\)\s*->|\buse\s+std::",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex PhpRe = new Regex(
            @"<\?php\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        static readonly Regex GoRe = new Regex(
            @"(?m)^[ \t]*package\s+main\b|\bfunc\s+main\s*\(|\bfunc\s+[a-z_]\w*\s*\(|^[ \t]*import[ \t]+\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        static readonly Regex JavaRe = new Regex(
            @"\bimport\s+java\.|\bpublic\s+static\s+void\s+main\s*\(|\bSystem\.out\.println\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex ShellRe = new Regex(
            @"\A#!.*\b(?:bash|zsh|ksh|dash|sh)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>The language rules in the §3.1 priority order; the first match is the tag.</summary>
        static readonly (string Tag, Regex Rule)[] LanguageRules =
        {
            ("csharp", CSharpRe),
            ("typescript", TypeScriptRe),
            ("javascript", JavaScriptRe),
            ("python", PythonRe),
            ("rust", RustRe),
            ("php", PhpRe),
            ("go", GoRe),
            ("java", JavaRe),
            ("shell", ShellRe),
        };

        /// <summary>
        /// One pass over prose instead of nine: the same alternatives, built from the rules above, so
        /// it can only answer "no language matches". The ordered rules still decide which tag. The
        /// list calls this for up to 1,000 rows; a regex per language would spend the 50 ms search
        /// budget on rows that carry no code at all. NonBacktracking forbids lookarounds, so a rule
        /// added to <see cref="LanguageRules"/> must stay free of them or the type initializer throws.
        /// </summary>
        static readonly Regex AnyLanguageRe = new Regex(
            string.Join("|", Array.ConvertAll(LanguageRules, r => "(?:" + r.Rule + ")")),
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        // ---- classify -------------------------------------------------------------------------

        /// <summary>
        /// Returns proven camelCase tags in the §3.1 order, or an empty shared array when unsure.
        /// <paramref name="formats"/> is the format-name list a list row already carries; when null,
        /// the item's own format tuples are used.
        /// </summary>
        public static string[] Classify(ClipboardItem item, IReadOnlyList<string> formats)
        {
            if (item == null) return Array.Empty<string>();

            // An image is a picture, whatever its OCR text says. The rules below describe text the user copied;
            // an incidental word in a screenshot must never mint link/path/number/language, and formats on an
            // image row (DIB, PNG) prove nothing about its content.
            if (string.Equals(item.Kind, "image", StringComparison.OrdinalIgnoreCase) ||
                (item.Image != null && item.Image.Length > 0))
                return new[] { "image" };

            List<string> tags = null;

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
            // An edited grid outlives its Biff blobs (dropped on save as stale): the kind carries it.
            if (string.Equals(item.Kind, "sheet", StringComparison.OrdinalIgnoreCase))
                tags = Add(tags, "spreadsheet");

            // A list row carries only PreviewChars of the body. Whole-text shapes are skipped on a
            // preview because its cut tail can mint a path or a JSON document that the full body does
            // not prove; formats, the image payload, the prefix shapes and the line-anchored
            // language rules are still safe. Missing is allowed, a wrong tag is not.
            if (string.IsNullOrWhiteSpace(item.Text)) return Result(tags);
            string text = item.Text.Trim();
            bool shape = false;

            for (int i = 0; i < ShapeRules.Length; i++)
            {
                if (item.IsTruncated && NeedsWholeText(ShapeRules[i].Tag)) continue;
                if (!ShapeRules[i].Test(text)) continue;
                tags = Add(tags, ShapeRules[i].Tag);
                shape = true;
                break;
            }

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

        static bool IsEmail(string text)
        {
            if (!EmailRe.IsMatch(text)) return false;
            // Reasonable-people guards: no consecutive dots, no leading/trailing dot in local part.
            if (text.Contains("..")) return false;
            int at = text.IndexOf('@');
            if (at <= 0 || text[0] == '.' || text[at - 1] == '.') return false;
            if (text.IndexOf("@-", StringComparison.Ordinal) >= 0) return false;
            return true;
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
            if (!AnyLanguageRe.IsMatch(text)) return null;
            for (int i = 0; i < LanguageRules.Length; i++)
                if (LanguageRules[i].Rule.IsMatch(text)) return LanguageRules[i].Tag;
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
