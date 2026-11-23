using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Clip.UI
{
    /// <summary>
    /// The Images tab: one dominant preview over the existing virtualising filmstrip
    /// (docs\DESIGN-v2-ui.md §3, synthesis Q6 — fit never crop, decode once per selection).
    /// </summary>
    public partial class MainWindow
    {
        const int PreviewWidth = 1024;

        bool _stripLayout;
        int _previewSeq;

        /// <summary>Tab switch is the only thing that changes the list's shape; every reload re-asserts it.</summary>
        void ApplyImagesLayout()
        {
            if (_stripLayout == _images) return;
            _stripLayout = _images;

            if (_images)
            {
                ImagesPane.Visibility = Visibility.Visible;
                History.ItemsPanel = (ItemsPanelTemplate)FindResource("ImageStripPanel");
                History.ItemTemplate = (DataTemplate)FindResource("ImageTileTemplate");
                History.Padding = new Thickness(6, 4, 6, 4);
                History.Margin = new Thickness(0, 0, 0, 6);
                History.Height = 112;
                History.VerticalAlignment = VerticalAlignment.Bottom;
                ScrollViewer.SetHorizontalScrollBarVisibility(History, ScrollBarVisibility.Hidden);
                ScrollViewer.SetVerticalScrollBarVisibility(History, ScrollBarVisibility.Disabled);
                ScrollCue.Visibility = Visibility.Collapsed;
                return;
            }

            DropPreview();
            ImagesPane.Visibility = Visibility.Collapsed;
            History.ItemsPanel = (ItemsPanelTemplate)FindResource("ClipListPanel");
            History.ItemTemplate = (DataTemplate)FindResource("ClipRowTemplate");
            History.Padding = new Thickness(2, 6, 2, 6);
            History.Margin = new Thickness(0);
            History.Height = double.NaN;
            History.VerticalAlignment = VerticalAlignment.Stretch;
            ScrollViewer.SetHorizontalScrollBarVisibility(History, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(History, ScrollBarVisibility.Hidden);
            ScrollCue.Visibility = Visibility.Visible;
        }

        void OnHistorySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_images) ShowPreview(History.SelectedItem as ClipRow);
            UpdateSelectionBar();
            QueueRowTip(History.SelectedItem as ClipRow);
        }

        /// <summary>Stored 96 px thumb first (no blank frame), then the 1024 px decode when it lands.</summary>
        void ShowPreview(ClipRow row)
        {
            int seq = ++_previewSeq;

            if (row == null)
            {
                DropPreview();
                PreviewCounterText.Text = "";
                return;
            }

            int index = _visible.IndexOf(row);
            PreviewCounterText.Text = (index + 1) + " / " + _visible.Count;

            if (row.Thumb == null) PreviewUnavailable("This image's preview could not be read.");
            else
            {
                PreviewFallback.Visibility = Visibility.Collapsed;
                PreviewImage.Source = row.Thumb;
            }

            // The store lives on the UI thread; only the pixel decode is handed over.
            byte[] png;
            try { png = _store.GetImage(row.Id)?.Image; }
            catch (Exception) { png = null; } // deleted between the list and the read
            if (png == null || png.Length == 0)
            {
                PreviewUnavailable("This image is no longer stored.");
                return;
            }

            Task.Run(() =>
            {
                BitmapSource big = null;
                try { big = DecodeImage(png, PreviewWidth); }
                catch (Exception) { } // a corrupt blob gets the visible placeholder, never a crash
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (seq != _previewSeq) return; // a newer selection owns the pane
                    if (big == null)
                    {
                        PreviewUnavailable("This image's preview could not be read.");
                        return;
                    }
                    PreviewFallback.Visibility = Visibility.Collapsed;
                    PreviewImage.Source = big;
                }), DispatcherPriority.Background);
            });
        }

        void PreviewUnavailable(string note)
        {
            PreviewImage.Source = null;
            PreviewFallbackNote.Text = note;
            PreviewFallback.Visibility = Visibility.Visible;
        }

        /// <summary>One large bitmap at most: the previous preview is dropped on every swap, hide and tab change.</summary>
        void DropPreview()
        {
            _previewSeq++;
            PreviewImage.Source = null;
            PreviewFallback.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Frozen preview bitmap. The header is read first so the 1024 cap applies to the LARGER side
        /// (a tall screenshot must not decode at full height), and OnLoad + Freeze make the result
        /// safe to build on a background thread and hand to the UI thread.
        /// </summary>
        public static BitmapSource DecodeImage(byte[] png, int targetWidth)
        {
            using var ms = new MemoryStream(png, writable: false);
            var frame = BitmapFrame.Create(ms, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            int sourceWidth = frame.PixelWidth;
            int sourceHeight = frame.PixelHeight;
            if (sourceWidth <= 0 || sourceHeight <= 0) throw new InvalidDataException("no pixels");
            ms.Position = 0;

            int larger = Math.Max(sourceWidth, sourceHeight);
            double scale = larger > targetWidth ? (double)targetWidth / larger : 1;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        /// <summary>One image per ↑/↓ (↓ next, ↑ previous); ←/→ switch tabs here too (owner directive 23 Nov).</summary>
        void StepImage(int delta)
        {
            if (_images && _visible.Count > 0) SelectAt(History.SelectedIndex + delta);
        }

        /// <summary>A 20-image strip has no scrollbar, so the wheel is the scroll (v2 §3.5).</summary>
        void OnStripWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_images) return;
            var scroll = FindDescendant<ScrollViewer>(History);
            if (scroll == null) return;
            if (e.Delta > 0) scroll.LineLeft();
            else scroll.LineRight();
            e.Handled = true;
        }

        /// <summary>Hiding the popup releases the big bitmap; the tab keeps its selection for the next open.</summary>
        void OnHistoryVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue) return;
            DropPreview();
        }

        static T FindDescendant<T>(DependencyObject node) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is T match) return match;
                var nested = FindDescendant<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
