using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Clip.Core;
using Clip.Interop;

namespace Clip.UI
{
    /// <summary>
    /// F2 editor: ONE rich surface for every text clip. The item's RTF loads first, HTML-only clips
    /// go through HtmlToRtf, and a failed load says so in the notice bar instead of silently
    /// dropping to a plain box (docs\DESIGN-v2-ui.md §4, docs\S0-HANDOFF.md).
    /// </summary>
    public partial class EditorWindow : Window
    {
        /// <summary>The converter has no notice surface, so the caller refuses oversized fragments (§4.5).</summary>
        const int MaxHtmlFragment = 2_000_000;

        const string PlainNotice = "This clip's formatting could not be loaded; editing as plain text.";
        const string TooLargeNotice = "This clip's HTML body is larger than 2 MB, so its formatting was not loaded; editing as plain text.";

        readonly ClipboardStore _store;
        readonly ClipboardItem _item;
        readonly bool _hadHtml;
        string _originalText;
        string _originalRtf;
        bool _reflecting;

        public EditorWindow(ClipboardStore store, ClipboardItem item, string html, string rtf)
        {
            InitializeComponent();
            _store = store;
            _item = item;
            _hadHtml = !string.IsNullOrEmpty(html);

            SourceInitialized += (s, e) => WindowChrome.Apply(this);

            if (!LoadBody(html, rtf))
            {
                FormatNoticeText.Text = _notice;
                FormatNotice.Visibility = Visibility.Visible;
            }

            _originalText = CurrentText();
            _originalRtf = SaveRtf();

            Loaded += (s, e) =>
            {
                Shell.BeginAnimation(OpacityProperty, Motion.Linear(1, 150));
                RichBox.Focus();
                RichBox.CaretPosition = RichBox.Document.ContentEnd;
                Reflect();
            };
        }

        string _notice = PlainNotice;

        // ---- load -------------------------------------------------------------------------

        /// <summary>RTF → HTML → plain, in that order; false means the visible notice must show.</summary>
        bool LoadBody(string html, string rtf)
        {
            if (!string.IsNullOrEmpty(rtf))
            {
                if (TryLoadRtf(rtf)) return true;
            }
            else if (!string.IsNullOrEmpty(html))
            {
                string converted = null;
                try
                {
                    string fragment = HtmlFragment.Extract(html);
                    if (fragment.Length > MaxHtmlFragment) _notice = TooLargeNotice;
                    else converted = HtmlToRtf.Convert(fragment);
                }
                catch
                {
                    // An HTML body the tokeniser cannot read is a failed load, reported by the notice below.
                }
                if (!string.IsNullOrEmpty(converted) && TryLoadRtf(converted)) return true;
            }
            else
            {
                DocumentText(_item.Text);
                return true;
            }

            DocumentText(_item.Text);
            return false;
        }

        /// <summary>Attempt 1 is the byte-exact Latin-1 envelope, attempt 2 the old UTF-8 re-encode.</summary>
        bool TryLoadRtf(string body)
        {
            foreach (var bytes in new[] { RtfText.ToBytes(body), RtfText.ToUtf8Bytes(body) })
            {
                if (bytes.Length == 0) continue;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    Body().Load(ms, DataFormats.Rtf);
                    return true;
                }
                catch
                {
                    // Try the next envelope; the caller shows the notice when both are exhausted.
                }
            }
            return false;
        }

        TextRange Body() => new TextRange(RichBox.Document.ContentStart, RichBox.Document.ContentEnd);

        void DocumentText(string text) => Body().Text = text ?? "";

        // ---- save -------------------------------------------------------------------------

        static string Normalize(string text) => (text ?? "").TrimEnd('\r', '\n');

        string CurrentText() => Normalize(Body().Text);

        string SaveRtf()
        {
            using var ms = new MemoryStream();
            Body().Save(ms, DataFormats.Rtf);
            return RtfText.FromBytes(ms.ToArray());
        }

        bool Dirty => CurrentText() != _originalText || SaveRtf() != _originalRtf;

        void SaveAndClose()
        {
            string rtf = SaveRtf();
            _item.Text = CurrentText();
            // Set the rich body BEFORE Update: the store writes exactly the fields it is handed.
            _item.Rtf = rtf;
            _item.Html = _hadHtml ? ToHtml(rtf) : null;
            _store.Update(_item);
            Close();
        }

        /// <summary>Only an html-origin clip keeps an html body; a converter failure drops it rather than storing a lie.</summary>
        static string ToHtml(string rtf)
        {
            try { return RtfPipe.Rtf.ToHtml(rtf); }
            catch { return null; } // a body the converter rejects must not be stored as html
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
