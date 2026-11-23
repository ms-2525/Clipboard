using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Clip.Core;

namespace Clip.UI
{
    /// <summary>F2 editor: plain text in a TextBox, rich (RTF) in a RichTextBox; Ctrl+S saves in place.</summary>
    public partial class EditorWindow : Window
    {
        readonly ClipboardStore _store;
        readonly ClipboardItem _item;
        readonly bool _rich;
        string _originalText;
        string _originalRtf;

        public EditorWindow(ClipboardStore store, ClipboardItem item, string html, string rtf)
        {
            InitializeComponent();
            _store = store;
            _item = item;

            _rich = !string.IsNullOrEmpty(rtf);
            if (_rich)
            {
                try
                {
                    var range = new TextRange(RichBox.Document.ContentStart, RichBox.Document.ContentEnd);
                    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rtf));
                    range.Load(ms, DataFormats.Rtf);
                    _originalText = Normalize(range.Text);
                    _originalRtf = SaveRtf();
                    RichBox.Visibility = Visibility.Visible;
                }
                catch
                {
                    // A body the RTF reader rejects still has its plain text; edit that rather than refusing to edit.
                    _rich = false;
                }
            }

            if (!_rich)
            {
                PlainBox.Text = item.Text ?? "";
                _originalText = Normalize(PlainBox.Text);
                PlainBox.Visibility = Visibility.Visible;
            }

            Loaded += (s, e) =>
            {
                Shell.BeginAnimation(OpacityProperty, Motion.Linear(1, 150));
                if (_rich)
                {
                    RichBox.Focus();
                    RichBox.CaretPosition = RichBox.Document.ContentEnd;
                }
                else
                {
                    PlainBox.Focus();
                    PlainBox.CaretIndex = PlainBox.Text.Length;
                }
            };
        }

        string CurrentText()
        {
            if (!_rich) return Normalize(PlainBox.Text);
            return Normalize(new TextRange(RichBox.Document.ContentStart, RichBox.Document.ContentEnd).Text);
        }

        string SaveRtf()
        {
            var range = new TextRange(RichBox.Document.ContentStart, RichBox.Document.ContentEnd);
            using var ms = new MemoryStream();
            range.Save(ms, DataFormats.Rtf);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        static string Normalize(string text) => (text ?? "").TrimEnd('\r', '\n');

        bool Dirty => _rich
            ? CurrentText() != _originalText || SaveRtf() != _originalRtf
            : Normalize(PlainBox.Text) != _originalText;

        void RequestClose()
        {
            if (DiscardPrompt.Visibility == Visibility.Visible) { KeepEditing_Click(); return; }
            if (!Dirty) { Close(); return; }
            DiscardPrompt.Visibility = Visibility.Visible;
            KeepEditing.Focus();
        }

        void SaveAndClose()
        {
            string text = CurrentText();
            if (_rich)
            {
                _item.Text = text;
                _item.Rtf = SaveRtf();
                _item.Html = null; // the old CF_HTML no longer matches the edited body
            }
            else
            {
                _item.Text = PlainBox.Text;
                _item.Html = null;
                _item.Rtf = null;
            }
            _store.Update(_item);
            Close();
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
            if (_rich) RichBox.Focus(); else PlainBox.Focus();
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
