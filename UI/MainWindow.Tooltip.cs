using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Clip.UI
{
    /// <summary>
    /// The popup's viewport edges: the outside tooltip for a truncated row (docs\CHECKLIST-features.md)
    /// and the bottom mask that keeps the list from ending in a sliced row. Both are overlays â€” neither
    /// touches row geometry, so the zero-jump contract still holds.
    /// </summary>
    public partial class MainWindow
    {
        const int TipDwellMs = 500;

        RowTooltipWindow _tip;
        DispatcherTimer _tipTimer;
        ClipRow _tipRow;

        void QueueRowTip(ClipRow row)
        {
            if (row == null || !row.Truncated) { HideRowTip(); return; }
            if (ReferenceEquals(row, _tipRow)) return; // same row: an already-running dwell keeps running

            _tipRow = row;
            if (_tipTimer == null)
            {
                _tipTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TipDwellMs) };
                _tipTimer.Tick += (s, e) => { _tipTimer.Stop(); ShowRowTip(); };
            }
            _tipTimer.Stop();
            _tipTimer.Start();
        }

        void ShowRowTip()
        {
            var row = _tipRow;
            if (row == null || !IsVisible || !row.Truncated) return;
            var container = ContainerOf(row);
            if (container == null) return;

            _tip ??= new RowTooltipWindow { OnDismissed = HideRowTip };
            _tip.ShowFor(this, row, container.TranslatePoint(new Point(0, 0), this).Y);
            // A no-activate window still costs the popup its foreground on some setups; keep the keys here.
            if (!IsActive) Activate();
        }

        internal void HideRowTip()
        {
            _tipTimer?.Stop();
            _tipRow = null;
            _tip?.Dismiss();
        }

        void OnListMouseMove(object sender, MouseEventArgs e)
        {
            if (_images) return;
            var container = ItemsControl.ContainerFromElement(History, (DependencyObject)e.OriginalSource) as ListBoxItem;
            QueueRowTip(container?.DataContext as ClipRow);
        }

        void OnListMouseLeave(object sender, MouseEventArgs e)
        {
            // Moving onto the tooltip itself (to scroll it) is not leaving the row.
            if (_tip != null && _tip.ContainsCursor()) return;
            HideRowTip();
        }

        // ---- bottom edge: whole rows only ------------------------------------

        Panel _itemsHost;
        bool _edgeHooked;

        void OnEdgeMaskLoaded(object sender, RoutedEventArgs e)
        {
            if (_edgeHooked) return;
            _edgeHooked = true;
            // LayoutUpdated is the reliable signal: keyboard scrolling moves the panel without raising a
            // ScrollChanged the popup can see, and a stale mask is worse than a sliced glyph.
            History.LayoutUpdated += (s, ev) => UpdateEdgeMask();
            History.SizeChanged += (s, ev) => UpdateEdgeMask();
            UpdateEdgeMask();
        }

        /// <summary>
        /// Covers the part of the row the viewport cuts from the middle of a text line, down to the
        /// list's bottom, so the edge never slices a glyph. The strip takes the cut row's own fill, so
        /// the row simply ends at the edge instead of leaving a hole the size of its hidden half.
        /// </summary>
        void UpdateEdgeMask()
        {
            if (_images) { EdgeMask.Height = 0; return; }
            if (_itemsHost == null || !_itemsHost.IsDescendantOf(History)) _itemsHost = ItemsHostOf(History);
            if (_itemsHost == null) { EdgeMask.Height = 0; return; }

            double bottom = History.ActualHeight - History.Padding.Bottom;
            double cut = 0;
            ListBoxItem cutRow = null;
            double shownRow = 0;
            if (bottom > 0)
            {
                foreach (UIElement child in _itemsHost.Children)
                {
                    if (child is not ListBoxItem container || !container.IsVisible) continue;
                    double height = container.ActualHeight;
                    if (height <= 0) continue;
                    double shown = bottom - container.TranslatePoint(new Point(0, 0), History).Y;
                    if (shown <= 0.5) break;                 // this row starts below the edge
                    if (shown < height - 0.5) { cutRow = container; shownRow = shown; break; }
                }
            }

            if (cutRow?.DataContext is ClipRow row)
            {
                // Text starts under the row's 8 padding plus the section band when it has one.
                double textTop = 8 + (row.ShowSection ? 22 : 0);
                double textShown = shownRow - textTop;
                if (textShown >= 18)
                    cut = textShown - Math.Floor(textShown / 18) * 18;   // last whole line stays
                else if (shownRow > textTop)
                    cut = shownRow;                                      // not even one line: no stub

                // A row ending between two lines keeps its own fill, so the strip continues it; a row
                // removed whole leaves the list background, the way a list ends.
                EdgeMask.Background = cut > 0 && cut < shownRow - 0.5
                    ? EdgeBrush(cutRow)
                    : (Brush)FindResource("App");
            }

            // The mask is anchored to the list's bottom, the cut row to the viewport's: the list's own
            // bottom padding sits between them and is covered too.
            double mask = cut > 0.5 ? cut + History.Padding.Bottom : 0;
            if (Math.Abs(EdgeMask.Height - mask) > 0.5) EdgeMask.Height = mask;
        }

        /// <summary>The fill the cut row is painted with, so the strip continues it instead of breaking it.</summary>
        Brush EdgeBrush(ListBoxItem container)
        {
            if (container.IsSelected) return (Brush)FindResource("RowSelected");
            bool alternate = History.ItemContainerGenerator.IndexFromContainer(container) % 2 == 1;
            return (Brush)FindResource(alternate ? "RowAlt" : "App");
        }

        /// <summary>The virtualising panel that owns the realised row containers.</summary>
        static Panel ItemsHostOf(DependencyObject node)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is Panel panel && panel.IsItemsHost) return panel;
                var hit = ItemsHostOf(child);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}


