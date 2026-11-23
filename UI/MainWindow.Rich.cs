using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clip.Core;
using Clip.Interop;
using TheArtOfDev.HtmlRenderer.WPF;

namespace Clip.UI
{
    /// <summary>Selected-row growth, the one rich preview, the copy pulse and the dedupe jump.</summary>
    public partial class MainWindow
    {
        ClipRow _grownRow;
        HtmlPanel _richPanel;

        void ShowSelection(bool allowGrowth)
        {
            if (History.SelectedItem is not ClipRow row)
            {
                ClearRich();
                return;
            }

            if (_grownRow != null && !ReferenceEquals(_grownRow, row)) ReleaseRow(_grownRow);
            _grownRow = row;

            Grow(row, allowGrowth);
            EnsureRich(row);
        }

        void Grow(ClipRow row, bool allowGrowth, int retries = 1)
        {
            var body = Part<TextBlock>(row, "Body");
            if (body == null)
            {
                // Only a realized row can be grown; an off-screen row already renders at the compact height.
                if (retries > 0 && ContainerOf(row) != null)
                    Dispatcher.BeginInvoke(new Action(() => Grow(row, allowGrowth, retries - 1)), DispatcherPriority.Loaded);
                return;
            }

            // Growth follows the text it actually shows (1–3 lines × 18), never a fixed block of blank space.
            double target = row.LineCount * 18;
            if (!allowGrowth || !Motion.On || _navRepeat)
            {
                body.BeginAnimation(HeightProperty, null);
                body.Height = target;
                return;
            }

            var anim = Motion.SplineFrom(18, target, 120);
            anim.FillBehavior = FillBehavior.Stop;
            anim.Completed += (s, e) =>
            {
                // Commit the height instead of holding an animation value: a held-from animation
                // leaves the released row's text unrendered until it is selected again.
                body.BeginAnimation(HeightProperty, null);
                body.Height = target;
            };
            body.BeginAnimation(HeightProperty, anim);
        }

        void ReleaseRow(ClipRow row)
        {
            var body = Part<TextBlock>(row, "Body");
            if (body != null)
            {
                body.BeginAnimation(HeightProperty, null);
                body.Height = 18;
            }
            var host = Part<Border>(row, "PreviewHost");
            if (host != null) host.Visibility = Visibility.Collapsed;
            var rich = Part<ContentControl>(row, "RichHost");
            if (rich != null) rich.Content = null;
        }

        /// <summary>One HtmlPanel alive at a time (RAM protocol); loaded only for the selected row.</summary>
        void EnsureRich(ClipRow row, int retries = 1)
        {
            if (row == null) return;

            var host = Part<Border>(row, "PreviewHost");
            var content = Part<ContentControl>(row, "RichHost");
            if (host == null || content == null)
            {
                if (retries > 0 && ContainerOf(row) != null)
                    Dispatcher.BeginInvoke(new Action(() => EnsureRich(row, retries - 1)), DispatcherPriority.Loaded);
                return;
            }

            if (row.IsImage)
            {
                if (row.Thumb == null) return;
                content.Content = new Image
                {
                    Source = row.Thumb,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MaxHeight = 120
                };
                host.Visibility = Visibility.Visible;
                return;
            }

            var rich = _store.GetRich(row.Id);
            string html = null;
            if (!string.IsNullOrEmpty(rich?.Html)) html = HtmlFragment.Extract(rich.Html);
            if (string.IsNullOrWhiteSpace(html) && !string.IsNullOrEmpty(rich?.Rtf))
            {
                try { html = RtfPipe.Rtf.ToHtml(rich.Rtf); }
                catch { html = null; } // RTF-only item with a broken body: the plain row is still correct
            }
            // shortcut: 2 MB html ceiling for the preview; render a detail pane if bigger clips become common
            if (string.IsNullOrWhiteSpace(html) || html.Length > 2_000_000)
            {
                host.Visibility = Visibility.Collapsed;
                return;
            }

            var panel = new HtmlPanel
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                MaxHeight = 140,
                AvoidImagesLateLoading = true,
                IsSelectionEnabled = false,
                IsContextMenuEnabled = false,
                BaseStylesheet = RichCss(),
                Text = html
            };
            // One panel alive at a time: drop the previous row's panel before this one holds its document.
            DropPanel();
            panel.ImageLoad += OnRichImageLoad;
            content.Content = panel;
            host.Visibility = Visibility.Visible;
            _richPanel = panel;
        }

        void DropPanel()
        {
            if (_richPanel == null) return;
            _richPanel.ImageLoad -= OnRichImageLoad;
            _richPanel.Text = null;
            _richPanel = null;
        }

        void ClearRich()
        {
            DropPanel();
            if (_grownRow != null)
            {
                ReleaseRow(_grownRow);
                _grownRow = null;
            }
        }

        string RichCss()
        {
            string fg = Hex((SolidColorBrush)FindResource("Fg75"));
            string accent = Hex((SolidColorBrush)FindResource("AccentText"));
            return "body { font-family: \"Segoe UI\", sans-serif; font-size: 13px; line-height: 1.385; color: " + fg +
                   "; margin: 0; } a { color: " + accent + "; } code, pre { font-family: Consolas, monospace; font-size: 12px; }";
        }

        static string Hex(SolidColorBrush brush) =>
            "#" + brush.Color.R.ToString("X2") + brush.Color.G.ToString("X2") + brush.Color.B.ToString("X2");

        // Remote images never fetch on open; data: URIs are left to the renderer.
        static readonly BitmapImage BlankImage = CreateBlank();

        static BitmapImage CreateBlank()
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="));
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        static void OnRichImageLoad(object sender,
            TheArtOfDev.HtmlRenderer.WPF.RoutedEventArgs<TheArtOfDev.HtmlRenderer.Core.Entities.HtmlImageLoadEventArgs> args)
        {
            var e = args.Data;
            if (!string.IsNullOrEmpty(e.Src) && e.Src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
            e.Handled = true;
            e.Callback(BlankImage);
        }

        // ---- feedback -------------------------------------------------------

        void PulseRow(ClipRow row, int retries = 1)
        {
            if (!Motion.On) return;
            var layer = Part<Border>(row, "PulseLayer");
            if (layer == null)
            {
                if (retries > 0 && ContainerOf(row) != null)
                    Dispatcher.BeginInvoke(new Action(() => PulseRow(row, retries - 1)), DispatcherPriority.Loaded);
                return;
            }
            layer.BeginAnimation(OpacityProperty, Motion.Pulse());
        }

        /// <summary>Approximate FLIP for a deduped row: it starts where it used to sit and settles at the top.</summary>
        void FlipToTop(ClipRow row, int oldIndex)
        {
            if (!Motion.On || oldIndex <= 0) return;
            if (Part<StackPanel>(row, "ItemRoot")?.RenderTransform is not TranslateTransform tt) return;
            tt.Y = oldIndex * 65;
            tt.BeginAnimation(TranslateTransform.YProperty, Motion.Spline(0, 200, 0, 0, 0, 1));
        }

        // ---- row context menu ----------------------------------------------

        void ShowRowMenu(ClipRow row, bool atMouse = true)
        {
            if (row == null) return;

            var menu = new ContextMenu { Style = (Style)FindResource("MenuStyle") };
            if (atMouse) menu.Placement = PlacementMode.MousePoint;
            else
            {
                menu.PlacementTarget = (FrameworkElement)ContainerOf(row) ?? History;
                menu.Placement = PlacementMode.Bottom;
            }

            AddMenu(menu, "Paste", () => RequestPaste(row, false));
            AddMenu(menu, "Paste as plain text", () => RequestPaste(row, true));
            AddMenu(menu, "Copy", () => { CopyRequested?.Invoke(row.Item); PulseRow(row); });
            AddMenu(menu, "Edit", () => OpenEditor(row));
            bool pinned = row.Item.TabId == _store.PinnedTabId;
            AddMenu(menu, pinned ? "Unpin" : "Pin", () => { History.SelectedItem = row; TogglePin(); });
            AddMenu(menu, "Move to tab\u2026", () => { History.SelectedItem = row; OpenPicker(); });
            menu.Items.Add(new Separator());
            AddMenu(menu, "Delete", () => { History.SelectedItem = row; DeleteSelected(); });

            menu.IsOpen = true;
        }

        void AddMenu(ContextMenu menu, string header, Action action)
        {
            var item = new MenuItem { Header = header, Style = (Style)FindResource("MenuItemStyle") };
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }

        // ---- F2 editor / image viewer ---------------------------------------

        void OpenEditor(ClipRow row)
        {
            if (row == null) return;
            if (row.IsImage)
            {
                var image = _store.GetImage(row.Id);
                var path = ImageCache.Write(row.Id, image?.Image);
                if (path != null)
                {
                    try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                    catch { } // no default viewer registered: nothing sensible to do from here
                }
                return;
            }

            var rich = _store.GetRich(row.Id);
            var editor = new EditorWindow(_store, row.Item, rich?.Html, rich?.Rtf) { Owner = this };
            _editor = editor;
            editor.Closed += (s, e) =>
            {
                _editor = null;
                long id = row.Id;
                Reload();
                var again = _visible.FirstOrDefault(r => r.Id == id);
                if (again != null) History.SelectedItem = again;
                Search.Focus();
            };
            editor.Show();
        }
    }
}
