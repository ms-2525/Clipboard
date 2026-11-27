using System;
using System.IO;
using Clip.Core;
using Xunit;

namespace Clip.Tests
{
    /// <summary>
    /// SaveEditor refuses a truncated row ("would destroy the stored body") via
    /// ClipboardItem.IsTruncated, and ClipboardStore.Update refuses it again
    /// (false, nothing written). Update is exercised here against a real temp
    /// SQLite database; each test owns its file and deletes it afterwards.
    /// </summary>
    public sealed class ClipboardStoreUpdateTests : IDisposable
    {
        readonly string _dbPath;
        readonly ClipboardStore _store;

        public ClipboardStoreUpdateTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), "clip-tests-" + Guid.NewGuid().ToString("N") + ".db");
            _store = new ClipboardStore(_dbPath);
        }

        public void Dispose()
        {
            _store.Dispose();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(_dbPath + suffix); } catch (IOException) { }
            }
        }

        static ClipboardItem FullItem(string text) => new ClipboardItem
        {
            Kind = "text",
            Text = text,
            CapturedAt = "2026-11-27T10:00:00.000",
        };

        [Theory]
        [InlineData(0, false)]
        [InlineData(1200, false)] // exactly the preview: whole body present
        [InlineData(1201, true)] // one code point past: list row is a preview
        public void IsTruncated_Boundary(long textLength, bool expected)
        {
            var item = new ClipboardItem { Text = "preview", TextLength = textLength };
            Assert.Equal(expected, item.IsTruncated);
        }

        [Fact]
        public void IsTruncated_ComparesCodePointsNotUtf16Units()
        {
            // TextLength is SQLite code points; Text.Length is UTF-16 units. An emoji
            // preview can be LONGER in UTF-16 than the whole body yet still complete.
            var item = new ClipboardItem
            {
                Text = new string(char.ConvertFromUtf32(0x1F600)[0], 1300), // lone surrogates, Length 1300
                TextLength = 100, // stored body is 100 code points: this object has it all
            };
            Assert.False(item.IsTruncated);
        }

        [Fact]
        public void Update_RefusesTruncatedRow_WritesNothing()
        {
            _store.Insert(FullItem("stored body"));
            var preview = new ClipboardItem
            {
                Id = 1,
                Kind = "text",
                Text = "preview",
                TextLength = ClipboardStore.PreviewChars + 1, // list preview, not the body
            };
            Assert.True(preview.IsTruncated);
            Assert.False(_store.Update(preview));
            Assert.Equal("stored body", _store.GetText(1));
        }

        [Fact]
        public void Update_FullRow_RewritesBodyAndDropsFormats()
        {
            var item = FullItem("before");
            _store.Insert(item);
            var full = new ClipboardItem
            {
                Id = item.Id,
                Kind = "sheet", // SaveEditor mints this for TSV+table; Update just stores it
                Text = "a\tb",
                Html = "<table></table>",
                Rtf = null, // stale RTF is dropped on save
            };
            Assert.True(_store.Update(full));
            Assert.Equal("a\tb", _store.GetText(item.Id));
            var rich = _store.GetRich(item.Id);
            Assert.Equal("<table></table>", rich.Html);
            Assert.Null(rich.Rtf);
            var row = _store.GetRecent(10).Find(c => c.Id == item.Id);
            Assert.NotNull(row);
            Assert.Equal("sheet", row.Kind);
        }
    }
}
