using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clip.Core;
using WF = System.Windows.Forms;

namespace Clip.UI
{
    public partial class MainWindow : Window
    {
        readonly ClipboardStore _store;
        readonly Config _cfg;
        readonly ObservableCollection<ClipboardItem> _items = new();
        List<ClipboardItem> _all = new();

        public event Action<ClipboardItem> PasteRequested;

        public MainWindow(ClipboardStore store, Config cfg)
        {
            InitializeComponent();
            _store = store;
            _cfg = cfg;
            Title = Assembly.GetEntryAssembly().GetName().Name;
            History.ItemsSource = _items;
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else ShowPopup();
        }

        public void ShowPopup()
        {
            Reload();
            Search.Clear();

            var s = WF.Screen.FromPoint(WF.Cursor.Position);
            // "Clip" alone binds to Window.Clip (Geometry), so qualify globally.
            double sc = global::Clip.Interop.ScreenDpi.Scale(s);
            var wa = s.WorkingArea;
            Left = wa.Left / sc + (wa.Width / sc - Width) / 2;
            Top = wa.Top / sc + (wa.Height / sc - Height) / 2 - 40;

            Show();
            Activate();
            Search.Focus();
        }

        public void OnNewItem(ClipboardItem item)
        {
            if (!IsVisible) return;
            _all.Insert(0, item);
            ApplyFilter();
        }

        public void Reload()
        {
            _all = _store.GetRecent(_cfg.MaxItems);
            ApplyFilter();
        }

        void ApplyFilter()
        {
            _items.Clear();
            foreach (var i in SearchService.Filter(_all, Search.Text)) _items.Add(i);
            History.SelectedIndex = _items.Count > 0 ? 0 : -1;
        }

        void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        void OnCardDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Paste((e.OriginalSource as FrameworkElement)?.DataContext as ClipboardItem);
        }

        void Paste(ClipboardItem item)
        {
            if (item == null) return;
            Hide(); // focus must return to the target app first
            PasteRequested?.Invoke(item);
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            if (IsVisible) Hide();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                Paste(History.SelectedItem as ClipboardItem);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                int i = e.Key - Key.D1;
                if (i < _items.Count) Paste(_items[i]);
                e.Handled = true;
            }
            else if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (_items.Count > 0)
                {
                    int i = History.SelectedIndex + (e.Key == Key.Down ? 1 : -1);
                    History.SelectedIndex = Math.Max(0, Math.Min(_items.Count - 1, i));
                    History.ScrollIntoView(History.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                if (History.SelectedItem is ClipboardItem it)
                {
                    int i = History.SelectedIndex;
                    _store.Delete(it.Id);
                    _all.RemoveAll(x => x.Id == it.Id);
                    ApplyFilter();
                    if (_items.Count > 0) History.SelectedIndex = Math.Min(i, _items.Count - 1);
                }
                e.Handled = true;
            }
        }
    }
}
