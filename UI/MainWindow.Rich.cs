using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Clip.Core;
using Clip.Interop;

namespace Clip.UI
{
    /// <summary>Row feedback (pulse, dedupe FLIP), the row menu, F2 — and the inline row renderer.</summary>
    public partial class MainWindow
    {
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

        /// <summary>The row's top edge inside the list, or null when it is not realised.</summary>
        double? RowY(ClipRow row)
        {
            var container = row == null ? null : ContainerOf(row);
            return container == null ? null : container.TranslatePoint(new Point(0, 0), History).Y;
        }

        /// <summary>Dedupe FLIP: the moved row starts at its MEASURED old offset, never an index × height.</summary>
        void FlipToTop(ClipRow row, double delta)
        {
            if (!Motion.On || Math.Abs(delta) < 1) return;
            if (Part<StackPanel>(row, "ItemRoot")?.RenderTransform is not TranslateTransform tt) return;
            tt.Y = delta;
            var anim = Motion.Spline(0, 200, 0, 0, 0, 1);
            anim.FillBehavior = FillBehavior.Stop;
            anim.Completed += (s, e) =>
            {
                tt.BeginAnimation(TranslateTransform.YProperty, null);
                tt.Y = 0;
            };
            tt.BeginAnimation(TranslateTransform.YProperty, anim);
        }

        // ---- row context menu ----------------------------------------------

        void ShowRowMenu(ClipRow row, bool atMouse = true)
        {
            if (row == null) return;

            var menu = new ContextMenu();
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
            var item = new MenuItem { Header = header };
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
