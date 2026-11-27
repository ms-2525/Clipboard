using System;
using Xunit;

namespace Clip.Tests
{
    /// <summary>
    /// Verbatim mirrors of the private guard expressions in Host/ClipHost.Settings.cs
    /// SaveEditor (lines ~269-294). SaveEditor itself is a private bridge method that
    /// needs WebView2 + SQLite + the host, so it is not unit-testable without
    /// refactoring prod; these mirrors pin its host-independent rules instead.
    /// If the host expressions change, update the mirror AND the tests together.
    /// </summary>
    static class SaveEditorRules
    {
        // Host/ClipHost.Settings.cs:22 — the F2 editor refuses to load above this too.
        public const int MaxEditorHtml = 2_000_000;

        public static bool HtmlEmpty(string html) => string.IsNullOrWhiteSpace(html); // :269
        public static bool TextEmpty(string text) => string.IsNullOrWhiteSpace(text); // :271
        public static bool TooBig(string html) => html.Length > MaxEditorHtml; // :273
        public static bool HasScript(string html) => // :275
            html.IndexOf("<script", StringComparison.OrdinalIgnoreCase) >= 0;
        public static string TrimText(string text) => (text ?? "").TrimEnd('\r', '\n'); // :283
        public static bool IsGrid(string text, string html) => // :289-290
            text.IndexOf('\t') >= 0
            && html.IndexOf("<table", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public class EditorSaveRulesTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\r\n \t ")]
        public void EmptyHtml_Refused(string html)
        {
            Assert.True(SaveEditorRules.HtmlEmpty(html));
        }

        [Theory]
        [InlineData("<p>x</p>")]
        [InlineData(" <b>a</b> ")]
        public void NonEmptyHtml_Accepted(string html)
        {
            Assert.False(SaveEditorRules.HtmlEmpty(html));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  \r\n ")]
        public void EmptyText_Refused(string text)
        {
            Assert.True(SaveEditorRules.TextEmpty(text));
        }

        [Fact]
        public void SizeBoundary_UnderAndAt_AcceptedOver_Refused()
        {
            Assert.False(SaveEditorRules.TooBig(new string('a', SaveEditorRules.MaxEditorHtml - 1)));
            Assert.False(SaveEditorRules.TooBig(new string('a', SaveEditorRules.MaxEditorHtml)));
            Assert.True(SaveEditorRules.TooBig(new string('a', SaveEditorRules.MaxEditorHtml + 1)));
        }

        [Fact]
        public void SizeBoundary_CountsUtf16Units()
        {
            // string.Length counts UTF-16 code units: one emoji is Length 2, so a
            // 2M-char°EUR... body of emoji reaches the ceiling with half the glyphs.
            string emoji = char.ConvertFromUtf32(0x1F600);
            Assert.Equal(2, emoji.Length);
            Assert.True(SaveEditorRules.TooBig(new string('a', SaveEditorRules.MaxEditorHtml - 1) + emoji));
            Assert.Equal(2_000_000, SaveEditorRules.MaxEditorHtml);
        }

        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("<SCRIPT SRC=x>")]
        [InlineData("<Script type=\"text/javascript\">")]
        [InlineData("<p>a</p><script")]
        public void ScriptCarrier_Refused(string html)
        {
            Assert.True(SaveEditorRules.HasScript(html));
        }

        [Theory]
        [InlineData("<p>no script here</p>")]
        [InlineData("<p>a description of scrips</p>")] // "script" without the bracket
        [InlineData("<scrip")] // truncated tag name is not a script
        [InlineData("script>")] // missing the opening bracket
        public void ScriptFree_Accepted(string html)
        {
            Assert.False(SaveEditorRules.HasScript(html));
        }

        [Theory]
        [InlineData("a\tb", "<table><tr><td>a</td></tr></table>", true)] // TSV + table = sheet
        [InlineData("a\tb", "<TABLE><tr><td>a</td></tr></TABLE>", true)] // uppercase tag counts
        [InlineData("a\tb", "<p>no table</p>", false)] // tabs alone never mint a spreadsheet
        [InlineData("plain", "<table><tr><td>a</td></tr></table>", false)] // a lone table neither
        [InlineData("plain", "<p>hi</p>", false)]
        public void GridDetectionMatrix(string text, string html, bool expected)
        {
            Assert.Equal(expected, SaveEditorRules.IsGrid(text, html));
        }

        [Theory]
        [InlineData("abc\r\n", "abc")]
        [InlineData("abc\n\n\n", "abc")]
        [InlineData("abc\r\n\r\n", "abc")]
        [InlineData("abc", "abc")]
        [InlineData("abc \t", "abc \t")] // only CR/LF are trimmed, spaces and tabs stay
        [InlineData("a\nb\n", "a\nb")] // interior newlines stay
        [InlineData(null, "")]
        public void TrimEnd_StripsTrailingNewlinesOnly(string text, string expected)
        {
            Assert.Equal(expected, SaveEditorRules.TrimText(text));
        }
    }
}
