using Clip.Core;
using Xunit;

namespace Clip.Tests
{
    /// <summary>
    /// SaveEditor mints kind "sheet" for a TSV+table body and drops the Biff format
    /// blobs as stale. ClipExport.KindOf keeps treating that row as a spreadsheet
    /// by the kind alone ("the kind carries it") — these two tests pin that the
    /// edited grid still exports as CSV after its native formats are gone.
    /// </summary>
    public class ClipExportSheetTests
    {
        [Fact]
        public void KindOf_SheetWithoutFormats_IsSpreadsheet()
        {
            var item = new ClipboardItem { Kind = "sheet", Text = "a\tb" };
            Assert.Equal(ClipExportKind.Spreadsheet, ClipExport.KindOf(item, null));
        }

        [Fact]
        public void Prepare_SheetWithoutFormats_OffersCsv()
        {
            var item = new ClipboardItem { Kind = "sheet", Text = "a\tb" };
            var result = ClipExport.Prepare(item, null, "auto");
            Assert.True(result.Ok);
            Assert.Equal(".csv", result.Extension);
        }
    }
}
