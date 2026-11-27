using Clip.Interop;
using Xunit;

namespace Clip.Tests
{
    /// <summary>
    /// SaveEditor persists HtmlFragment.Wrap(html); paste-back consumers read it via
    /// Extract. These tests pin the envelope round-trip, including the double-envelope
    /// case (an already-wrapped fragment saved again): Extract unwraps exactly one
    /// layer, so a double-wrapped body comes back single-wrapped. That is the
    /// documented actual behavior, not a bug fix.
    /// </summary>
    public class HtmlFragmentTests
    {
        [Fact]
        public void WrapExtract_RoundTripsPlainFragment()
        {
            const string fragment = "<p>Hello <b>world</b></p>";
            Assert.Equal(fragment, HtmlFragment.Extract(HtmlFragment.Wrap(fragment)));
        }

        [Fact]
        public void WrapExtract_RoundTripsNonAsciiByteOffsets()
        {
            // Offsets are UTF-8 byte counts, so multi-byte text must survive exactly.
            const string fragment = "<p>caf\u00e9 \U0001F600 \u4e2d\u6587</p>";
            Assert.Equal(fragment, HtmlFragment.Extract(HtmlFragment.Wrap(fragment)));
        }

        [Fact]
        public void WrapExtract_EmptyFragment_ReturnsEnvelopeSkeleton()
        {
            // Documented actual: with an empty fragment StartFragment == EndFragment,
            // so Extract falls back to the whole body after the header instead of "".
            // Unreachable from SaveEditor (empty html is refused before Wrap), so no
            // prod change — pinned here so a future caller sees the quirk.
            const string skeleton = "<html><body><!--StartFragment--><!--EndFragment--></body></html>";
            Assert.Equal(skeleton, HtmlFragment.Extract(HtmlFragment.Wrap("")));
        }

        [Fact]
        public void Extract_DoubleWrappedBody_UnwrapsExactlyOneLayer()
        {
            const string fragment = "<p>cell</p>";
            string once = HtmlFragment.Wrap(fragment);
            string twice = HtmlFragment.Wrap(once);
            Assert.Equal(once, HtmlFragment.Extract(twice));
        }

        [Fact]
        public void Wrap_EmitsParseableHeader()
        {
            string wrapped = HtmlFragment.Wrap("<p>x</p>");
            Assert.StartsWith("Version:0.9\r\nStartHTML:", wrapped);
            Assert.Contains("<!--StartFragment-->", wrapped);
            Assert.Contains("<!--EndFragment--></body></html>", wrapped);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Extract_NullOrEmpty_ReturnsEmpty(string cfHtml)
        {
            Assert.Equal(string.Empty, HtmlFragment.Extract(cfHtml));
        }
    }
}
