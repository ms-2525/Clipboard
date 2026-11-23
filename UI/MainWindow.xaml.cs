using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Clip.Core;
using Clip.Interop;
using WF = System.Windows.Forms;

namespace Clip.UI
{
    public partial class MainWindow : Window
    {
        readonly ClipboardStore _store;
        readonly Config _cfg;

        List<ClipRow> _rows = new();
        List<ClipRow> _visible = new();
        readonly DispatcherTimer _searchTimer;

        // Current tab: Images is a view over main history; a tab id is a container (Pinned/custom).
        long? _tabId;
        bool _images;
        string _tabName = "Clipboard";

        ClipboardItem _undoItem;
        bool _closing;
        bool _suppressFilter;
        bool _composing;
        Window _editor;

        // The list is rebuilt only when something actually changed — a fresh full Reload on every
        // Alt+V re-read every row and its thumb and cost ~19 MB of native render memory per open.
        bool _dirty = true;
        long? _loadedTab;
        bool _loadedImages;

        public event Action<ClipboardItem> PasteRequested;
        public event Action<ClipboardItem> PlainPasteRequested;
        public event Action<ClipboardItem> CopyRequested;

        public MainWindow(ClipboardStore store, Config cfg)
        {
            InitializeComponent();
            _store = store;
            _cfg = cfg;

            SourceInitialized += (s, e) => WindowChrome.Apply(this, topmost: true);

            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); ApplyFilter(); };

            // IME gate: while a composition is open Esc/Enter belong to the IME (RESEARCH-keyboard-ux §8).
            TextCompositionManager.AddPreviewTextInputStartHandler(this, (s, e) => _composing = true);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(this,
                (s, e) => { if (e.TextComposition.CompositionText.Length == 0) _composing = false; });
            TextCompositionManager.AddPreviewTextInputHandler(this, (s, e) => _composing = false);

            History.ItemsSource = new ObservableCollection<ClipRow>();
        }

        public ClipboardItem SelectedItem => (History.SelectedItem as ClipRow)?.Item;

        public void Toggle()
        {
            if (IsVisible)
            {
                if (_closing) { _closing = false; Shell.BeginAnimation(OpacityProperty, null); Shell.Opacity = 1; WindowChrome.Raise(this); Activate(); Search.Focus(); return; }
                // A visible-but-inactive popup (another window came forward, or it lost a z-order race)
                // must come forward, not toggle itself closed: that is what "the hotkey did nothing" was.
                if (!IsActive) { WindowChrome.Raise(this); Activate(); Search.Focus(); return; }
                ClosePopup(false);
            }
            else ShowPopup();
        }

        public void ShowPopup()
        {
            if (IsVisible) { Activate(); Search.Focus(); return; }

            _closing = false;
            _undoItem = null;
            CloseOverlays();
            _tabId = null;
            _images = false;
            _tabName = "Clipboard";

            _suppressFilter = true;
            Search.Clear();
            _suppressFilter = false;

            var s = ScreenForActiveWindow();
            double sc = ScreenDpi.Scale(s);
            var wa = s.WorkingArea;
            double maxH = Math.Max(320, wa.Height / sc - 16);
            Height = Math.Min(520, maxH);

            Left = wa.Left / sc + (wa.Width / sc - Width) / 2;
            Top = Math.Max(wa.Top / sc, wa.Top / sc + (wa.Height / sc - Height) / 2 - 40);

            Show();
            WindowChrome.Raise(this);
            Activate();
            if (_dirty || _loadedTab != _tabId || _loadedImages != _images) Reload();
            else
            {
                History.SelectedItem = _visible.FirstOrDefault();
                History.ScrollIntoView(History.SelectedItem);
            }
            // One forced pass so the selected row is realised (scroll cue + first paint) before we animate.
            UpdateLayout();
            Search.Focus();
            AnimateIn();
        }

        /// <summary>
        /// Loads the list and applies the row template once, off screen: no window, no render target and
        /// no resident surface — only the BAML parse, the row construction and the first text layout,
        /// which the first Alt+V would otherwise pay while the window is already up.
        /// </summary>
        internal void Warm()
        {
            if (IsVisible) return;
            Reload();

            var host = new StackPanel { Width = 534 };
            var template = (DataTemplate)FindResource("ClipRowTemplate");
            for (int i = 0; i < Math.Min(4, _visible.Count); i++)
                host.Children.Add(new ContentPresenter { Content = _visible[i], ContentTemplate = template });
            host.Measure(new Size(534, double.PositiveInfinity));
            host.Arrange(new Rect(0, 0, 534, host.DesiredSize.Height));
            host.UpdateLayout();
        }

        /// <summary>Paste hands the window straight back: an exit ghost would swallow the click (motion R4).</summary>
        public void HideForPaste()        {
            _closing = false;
            HideCore();
        }

        public void OnNewItem(ClipboardItem item)
        {
            if (!IsVisible) { _dirty = true; return; }
            // A capture only belongs to main history; while reading a container just refresh its count.
            ReloadTabs();
            if (_tabId != null) return;
            if (_images && item.Kind != "image") return;
            if (!_images && item.Kind == "image") return;

            // Measure the row that is about to move to the top BEFORE the list is rebuilt (FLIP).
            var previous = _rows.FirstOrDefault(r => r.Id == item.Id);
            double? before = RowY(previous);

            if (previous != null) _rows.Remove(previous);
            var row = new ClipRow(item, _cfg.PreviewLines, _store);
            _rows.Insert(0, row);

            string keep = SelectedItem?.Id.ToString();
            ApplyFilter(keep);
            History.UpdateLayout();
            double? after = RowY(row);

            PulseRow(row);
            if (before.HasValue && after.HasValue) FlipToTop(row, before.Value - after.Value);
        }

        public void Reload()
        {
            RichRow.Clear();
            _rows = LoadRows();
            MarkLoaded();
            ReloadTabs();
            ApplyFilter();
        }

        /// <summary>The list on screen and the current view agree — no reload needed on the next open.</summary>
        void MarkLoaded()
        {
            _loadedTab = _tabId;
            _loadedImages = _images;
            _dirty = false;
        }

        List<ClipRow> LoadRows()
        {
            List<ClipboardItem> items;
            if (_tabId is long id) items = _store.GetTabItems(-1, id);
            else if (_images) items = _store.GetRecent(_cfg.MaxItems, "image");
            else items = _store.GetRecent(_cfg.MaxItems).Where(i => i.Kind != "image").ToList();
            return items.Select(i => new ClipRow(i, _cfg.PreviewLines, _store)).ToList();
        }

        void ApplyFilter(string selectId = null)
        {
            if (_suppressFilter) return;

            var hits = SearchService.Filter(_rows.Select(r => r.Item).ToList(), Search.Text);
            var ids = new HashSet<long>(hits.Select(i => i.Id));
            var display = new List<ClipRow>();
            string query = Search.Text ?? "";
            string section = null;
            foreach (var row in _rows)
            {
                if (!ids.Contains(row.Id)) continue;
                row.SectionLabel = SectionFor(row.Item.CapturedAt);
                row.ShowSection = row.SectionLabel != section;
                section = row.SectionLabel;
                row.Number = display.Count + 1;
                row.Query = query;
                display.Add(row);
            }

            // Swap the collection instance: Clear+Add of 1000 rows costs 38–59 ms per keystroke.
            History.ItemsSource = new ObservableCollection<ClipRow>(display);
            _visible = display;

            ClipRow target = selectId != null ? display.FirstOrDefault(r => r.Id.ToString() == selectId) : null;
            History.SelectedItem = target ?? display.FirstOrDefault();
            if (History.SelectedItem != null) History.ScrollIntoView(History.SelectedItem);

            UpdateStatus();
            UpdateEmptyState();
            UpdatePlaceholder();
            UpdateSelectionBar();
            HideRowTip();
        }

        static string SectionFor(string stamp)
        {
            if (!DateTime.TryParse(stamp, out var when)) return "Earlier";
            var today = DateTime.Today;
            if (when.Date == today) return "Today";
            if (when.Date == today.AddDays(-1)) return "Yesterday";
            if (when >= today.AddDays(-7)) return "This Week";
            return "Earlier";
        }

        void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            PlaceholderText.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ClearSearch.Visibility = Search.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (_suppressFilter) return;
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        void OnClearSearch(object sender, RoutedEventArgs e)
        {
            Search.Clear();
            Search.Focus();
            ApplyFilter();
        }

        void UpdatePlaceholder()
        {
            PlaceholderText.Text = "Search " + _tabName;
            PlaceholderText.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ClearSearch.Visibility = Search.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        void UpdateStatus()
        {
            int n = _visible.Count;
            StatusCount.Text = n == 0 ? "" : n + (_images ? (n == 1 ? " image" : " images") : (n == 1 ? " clip" : " clips"));
        }

        /// <summary>Bottom bar: the selected clip's app · window title · captured time (directive §2 rev 3).</summary>
        void UpdateSelectionBar()
        {
            var row = History.SelectedItem as ClipRow;
            SelectionBar.Text = row?.Status ?? "";
        }

        // ---- scroll cue ------------------------------------------------------

        double _extent, _viewport, _offset;
        bool _cueQueued;

        void OnListScrolled(object sender, ScrollChangedEventArgs e)
        {
            _extent = e.ExtentHeight;
            _viewport = e.ViewportHeight;
            _offset = e.VerticalOffset;
            UpdateScrollCue();
            // A shown tip belongs to the old position; the dwell timer may keep running (selection scroll).
            if (_tip is { IsVisible: true }) HideRowTip();
        }

        /// <summary>A position signal only: the list scrolls by item, so a part row needs an explanation.</summary>
        void UpdateScrollCue()
        {
            double track = ScrollCue.ActualHeight;
            bool scrollable = _extent > _viewport + 1;
            if (scrollable && track <= 24 && !_cueQueued)
            {
                // The first scroll event lands before the cue has been measured: one retry is enough.
                _cueQueued = true;
                Dispatcher.BeginInvoke(new Action(() => { _cueQueued = false; UpdateScrollCue(); }), DispatcherPriority.Loaded);
            }
            ScrollCue.Opacity = scrollable && track > 24 ? 1 : 0;
            if (!scrollable || track <= 24) return;

            double thumb = Math.Max(24, track * _viewport / _extent);
            ScrollCueThumb.Height = thumb;
            Canvas.SetLeft(ScrollCueThumb, 2);
            Canvas.SetTop(ScrollCueThumb, _extent <= _viewport ? 0 : (track - thumb) * (_offset / (_extent - _viewport)));
        }

        void OnScrollCueSizeChanged(object sender, SizeChangedEventArgs e) => UpdateScrollCue();

        void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is ClipRow row)
                RequestPaste(row, plain: false);
        }

        void OnRowRightClick(object sender, MouseButtonEventArgs e)
        {
            var container = ItemsControl.ContainerFromElement(History, (DependencyObject)e.OriginalSource) as ListBoxItem;
            if (container?.DataContext is not ClipRow row) return;
            History.SelectedItem = row;
            ShowRowMenu(row);
            e.Handled = true;
        }

        void RequestPaste(ClipRow row, bool plain)
        {
            if (row == null) return;
            HideForPaste();
            if (plain) PlainPasteRequested?.Invoke(row.Item);
            else PasteRequested?.Invoke(row.Item);
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            // The editor, the popup's overlays and the outside row tooltip are child surfaces, not focus loss.
            if (_tip is { IsVisible: true }) return;
            if (IsVisible && _editor == null && !AnyOverlayOpen) ClosePopup(false);
        }

        void OnShellMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (FindAncestor<ScrollBar>((DependencyObject)e.OriginalSource) != null ||
                FindAncestor<ButtonBase>((DependencyObject)e.OriginalSource) != null ||
                FindAncestor<TextBoxBase>((DependencyObject)e.OriginalSource) != null ||
                FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource) != null) return;
            DragMove();
        }

        static T FindAncestor<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T match) return match;
                node = VisualTreeHelper.GetParent(node);
            }
            return null;
        }

        internal ListBoxItem ContainerOf(ClipRow row) => History.ItemContainerGenerator.ContainerFromItem(row) as ListBoxItem;

        internal ContentPresenter PresenterOf(ClipRow row)
        {
            var container = ContainerOf(row);
            // The item template's ContentPresenter is not always child 0 of the container.
            return container == null ? null : FindPresenter(container, 3);
        }

        static ContentPresenter FindPresenter(DependencyObject node, int depth)
        {
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is ContentPresenter presenter) return presenter;
                if (depth > 1)
                {
                    var nested = FindPresenter(child, depth - 1);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        internal T Part<T>(ClipRow row, string name) where T : class
        {
            var cp = PresenterOf(row);
            return cp?.ContentTemplate?.FindName(name, cp) as T;
        }

        void AnimateIn()
        {
            // No opacity fade: any opacity below 1 rasterises the subtree and greys the type it is
            // meant to sharpen. An 8 DIP settle reads as the same arrival and keeps ClearType on.
            if (!Motion.On) { ShellShift.Y = 0; return; }
            ShellShift.Y = 8;
            ShellShift.BeginAnimation(TranslateTransform.YProperty, Motion.Spline(0, 140, 0, 0, 0, 1));
        }

        void ClosePopup(bool handoff)
        {
            if (!IsVisible) return;
            if (handoff || !Motion.On) { HideCore(); return; }
            _closing = true;
            var fade = Motion.Linear(0, 83);
            fade.Completed += (s, e) =>
            {
                if (!_closing) return;
                _closing = false;
                HideCore();
            };
            Shell.BeginAnimation(OpacityProperty, fade);
        }

        void HideCore()
        {
            _searchTimer.Stop();
            HideRowTip();
            CloseOverlays();
            _undoItem = null;
            _editor = null;
            Shell.BeginAnimation(OpacityProperty, null);
            Shell.Opacity = 1;
            ShellShift.BeginAnimation(TranslateTransform.YProperty, null);
            ShellShift.Y = 0;
            // The 32-entry inline cache survives a hide on purpose: reopening must not re-parse rows.
            if (IsVisible) Hide();
        }

        /// <summary>The popup centres on the monitor of the window the user was working in (owner rule).</summary>
        static WF.Screen ScreenForActiveWindow()
        {
            var h = GetForegroundWindow();
            return h != IntPtr.Zero ? WF.Screen.FromHandle(h) : WF.Screen.FromPoint(WF.Cursor.Position);
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
    }
}
