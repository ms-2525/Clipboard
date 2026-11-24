using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Clip.Core;
using Clip.Interop;
using Syncfusion.Windows.Controls.RichTextBoxAdv;

namespace Clip.UI
{
    /// <summary>
    /// F2 editor: ONE rich surface for every text clip, backed by Syncfusion's SfRichTextBoxAdv.
    /// The item's RTF loads first, HTML-only clips are parsed by the control itself, and a failed
    /// load says so in the notice bar instead of silently dropping to a plain box
    /// (docs\DESIGN-v2-ui.md §4, docs\S0-HANDOFF.md).
    /// </summary>
    public partial class EditorWindow : Window
    {
        /// <summary>The control parses HTML in-process, so the caller refuses oversized fragments (§4.5).</summary>
        const int MaxHtmlFragment = 2_000_000;

        const string PlainNotice = "This clip's formatting could not be loaded; editing as plain text.";
        const string TooLargeNotice = "This clip's HTML body is larger than 2 MB, so its formatting was not loaded; editing as plain text.";

        readonly ClipboardStore _store;
        readonly ClipboardItem _item;
        readonly bool _hadHtml;
        string _originalText;
        string _originalRtf;
        string _notice = PlainNotice;
        bool _reflecting;

        public EditorWindow(ClipboardStore store, ClipboardItem item, string html, string rtf)
        {
            InitializeComponent();
            _store = store;
            _item = item;
            _hadHtml = !string.IsNullOrEmpty(html);

            SourceInitialized += (s, e) => WindowChrome.Apply(this);

            // The toolbar must not read a half-loaded document, so reflection waits for Loaded.
            _reflecting = true;
            try
            {
                if (!LoadBody(html, rtf))
                {
                    FormatNoticeText.Text = _notice;
                    FormatNotice.Visibility = Visibility.Visible;
                }
                _originalText = CurrentText();
                _originalRtf = SaveRtf();
            }
            finally { _reflecting = false; }

            Loaded += (s, e) =>
            {
                // HoldEnd would keep an opacity clock alive and the whole document greyscale:
                // clear it the moment the fade lands so ClearType returns for the session.
                var fadeIn = Motion.Linear(1, 150);
                fadeIn.FillBehavior = FillBehavior.Stop;
                fadeIn.Completed += (a, b) =>
                {
                    Shell.BeginAnimation(OpacityProperty, null);
                    Shell.Opacity = 1;
                };
                Shell.BeginAnimation(OpacityProperty, fadeIn);
                RichBox.Focus();
                var end = RichBox.Document.DocumentEnd;
                RichBox.Selection.Select(end, end);
                Reflect();
            };
        }

        // ---- load -------------------------------------------------------------------------

        /// <summary>RTF → HTML → plain, in that order; false means the visible notice must show.</summary>
        bool LoadBody(string html, string rtf)
        {
            if (!string.IsNullOrEmpty(rtf)
                && (TryLoad(RtfText.ToBytes(rtf), FormatType.Rtf) || TryLoad(RtfText.ToUtf8Bytes(rtf), FormatType.Rtf)))
                return true;

            if (string.IsNullOrEmpty(rtf) && !string.IsNullOrEmpty(html))
            {
                string fragment = null;
                try { fragment = HtmlFragment.Extract(html); }
                catch
                {
                    // An html body the tokeniser cannot read is a failed load, reported by the notice below.
                }
                if (fragment != null)
                {
                    if (fragment.Length > MaxHtmlFragment) _notice = TooLargeNotice;
                    else if (TryLoad(Encoding.UTF8.GetBytes(fragment), FormatType.Html)) return true;
                }
            }

            DocumentText(_item.Text);
            return string.IsNullOrEmpty(rtf) && string.IsNullOrEmpty(html);
        }

        /// <summary>An RTF row is a byte format; the Latin-1 envelope is byte-exact, UTF-8 the old fallback.</summary>
        bool TryLoad(byte[] bytes, FormatType format)
        {
            if (bytes.Length == 0) return false;
            try
            {
                using var stream = new MemoryStream(bytes);
                RichBox.Load(stream, format);
                return true;
            }
            catch
            {
                // A stream the control rejects leaves no document behind, and the plain-text
                // fallback below needs one to type into.
                ResetDocument();
                return false;
            }
        }

        /// <summary>The control leaves no document after a rejected stream; an empty RTF restores a writable one.</summary>
        void ResetDocument()
        {
            using var skeleton = new MemoryStream(
                Encoding.UTF8.GetBytes(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Calibri;}}\pard\f0\fs24 \par}"));
            RichBox.Load(skeleton, FormatType.Rtf);
        }

        /// <summary>
        /// Plain text arrives as one paragraph per line; the control's 8 px default after-spacing
        /// would double the pitch of a 13 DIP face, so plain bodies are set flush.
        /// </summary>
        void DocumentText(string text)
        {
            var document = RichBox.Document;
            RichBox.Selection.Select(document.DocumentStart, document.DocumentEnd);
            RichBox.Selection.InsertText((text ?? "").Replace("\r\n", "\n").Replace('\r', '\n'));
            foreach (var paragraph in Paragraphs(document))
            {
                paragraph.ParagraphFormat.AfterSpacing = 0;
                paragraph.ParagraphFormat.BeforeSpacing = 0;
            }
        }

        // ---- document text ----------------------------------------------------------------

        /// <summary>Every paragraph in reading order, table cells included.</summary>
        static List<ParagraphAdv> Paragraphs(DocumentAdv document)
        {
            var paragraphs = new List<ParagraphAdv>();
            foreach (SectionAdv section in document.Sections) Collect(section.Blocks, paragraphs);
            return paragraphs;
        }

        static void Collect(BlockAdvCollection blocks, List<ParagraphAdv> into)
        {
            foreach (BlockAdv block in blocks)
            {
                if (block is ParagraphAdv paragraph) into.Add(paragraph);
                else if (block is TableAdv table)
                    foreach (TableRowAdv row in table.Rows)
                        foreach (TableCellAdv cell in row.Cells)
                            Collect(cell.Blocks, into);
            }
        }

        /// <summary>One paragraph's runs; a BreakAdv is a soft line break and reads as one.</summary>
        static string ParagraphText(ParagraphAdv paragraph)
        {
            var text = new StringBuilder();
            foreach (Inline inline in paragraph.Inlines)
            {
                if (inline is SpanAdv span) text.Append(span.Text);
                else if (inline is BreakAdv) text.Append('\n');
            }
            return text.ToString();
        }

        // ---- save -------------------------------------------------------------------------

        static string Normalize(string text) => (text ?? "").TrimEnd('\r', '\n');

        string CurrentText() => Normalize(string.Join("\n", Paragraphs(RichBox.Document).Select(ParagraphText)));

        string SaveRtf()
        {
            using var stream = new MemoryStream();
            RichBox.Save(stream, FormatType.Rtf);
            return RtfText.FromBytes(stream.ToArray());
        }

        /// <summary>Only an html-origin clip keeps an html body; an export failure drops it rather than storing a lie.</summary>
        string SaveHtml()
        {
            try
            {
                using var stream = new MemoryStream();
                RichBox.Save(stream, FormatType.Html);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
            catch { return null; } // a body the control cannot export must not be stored as html
        }

        bool Dirty => CurrentText() != _originalText || SaveRtf() != _originalRtf;

        void SaveAndClose()
        {
            string rtf = SaveRtf();
            _item.Text = CurrentText();
            // Set the rich body BEFORE Update: the store writes exactly the fields it is handed.
            _item.Rtf = rtf;
            _item.Html = _hadHtml ? SaveHtml() : null;
            _store.Update(_item);
            Close();
        }

        // ---- close / keys -----------------------------------------------------------------

        void RequestClose()
        {
            if (DiscardPrompt.Visibility == Visibility.Visible) { KeepEditing_Click(); return; }
            if (!Dirty) { Close(); return; }
            DiscardPrompt.Visibility = Visibility.Visible;
            KeepEditing.Focus();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (DiscardPrompt.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.F2) KeepEditing_Click();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                SaveAndClose();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape || e.Key == Key.F2)
            {
                RequestClose();
                e.Handled = true;
            }
        }

        void KeepEditing_Click()
        {
            DiscardPrompt.Visibility = Visibility.Collapsed;
            RichBox.Focus();
        }

        void OnCancelClick(object sender, RoutedEventArgs e) => RequestClose();
        void OnSaveClick(object sender, RoutedEventArgs e) => SaveAndClose();
        void OnKeepEditingClick(object sender, RoutedEventArgs e) => KeepEditing_Click();

        void OnDiscardClick(object sender, RoutedEventArgs e)
        {
            DiscardPrompt.Visibility = Visibility.Collapsed;
            Close();
        }
    }
}
