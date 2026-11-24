using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Clip.Core;

namespace Clip.UI
{
    /// <summary>Tab strip / container commands: pin, move, create, rename, delete, picker.</summary>
    public partial class MainWindow
    {
        List<TabVm> _tabVms = new();

        internal bool AnyOverlayOpen =>
            TabEditor.Visibility == Visibility.Visible ||
            TabConfirm.Visibility == Visibility.Visible ||
            Picker.Visibility == Visibility.Visible;

        void CloseOverlays()
        {
            TabEditor.Visibility = Visibility.Collapsed;
            TabConfirm.Visibility = Visibility.Collapsed;
            Picker.Visibility = Visibility.Collapsed;
        }

        void ReloadTabs()
        {
            ApplyImagesLayout(); // the list's shape follows the tab, never the selection

            var (clipCount, imageCount) = _store.Counts();

            var vms = new List<TabVm>
            {
                new TabVm("Clipboard", clipCount, _tabId == null && !_images, false, null, false),
                new TabVm("Images", imageCount, _tabId == null && _images, false, null, true)
            };
            foreach (var t in _store.ListTabs())
                vms.Add(new TabVm(t.name, t.count, _tabId == t.id, t.kind == "builtin", t.id, false));

            _tabVms = vms;
            TabItems.ItemsSource = vms;
            var active = vms.FirstOrDefault(v => v.IsActive);
            if (active != null) _tabName = active.Name;
        }

        void OnTabClick(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is TabVm vm) SwitchTab(vm.TabId, vm.Images, vm.Name);
        }

        void SwitchTab(long? id, bool images, string name)
        {
            if (id == _tabId && images == _images) return;
            _tabId = id;
            _images = images;
            _tabName = name;
            CloseOverlays();
            RichRow.Clear();
            _rows = LoadRows();
            MarkLoaded();
            ReloadTabs();
            ApplyFilter();
            Search.Focus();
        }

        void SwitchToClipboard() => SwitchTab(null, false, "Clipboard");

        // ---- Ctrl+T: inline name editor -------------------------------------

        void BeginCreateTab()
        {
            TabNameBox.Text = "";
            TabNameError.Visibility = Visibility.Collapsed;
            TabEditor.Visibility = Visibility.Visible;
            // Focus after this key event finishes; a synchronous Focus can be undone by the popup's own focus pass.
            Dispatcher.BeginInvoke(new Action(() => TabNameBox.Focus()), DispatcherPriority.Input);
        }

        void CommitCreateTab()
        {
            try
            {
                long id = _store.CreateTab(TabNameBox.Text);
                TabEditor.Visibility = Visibility.Collapsed;
                var tab = _store.ListTabs().FirstOrDefault(t => t.id == id);
                SwitchTab(id, false, tab.name ?? TabNameBox.Text.Trim());
            }
            catch (ArgumentException ex)
            {
                TabNameError.Text = ex.Message;
                TabNameError.Visibility = Visibility.Visible;
                TabNameBox.Focus();
            }
        }

        void CancelCreateTab()
        {
            CloseOverlays();
            Search.Focus();
        }

        // ---- Ctrl+W: delete the current custom tab --------------------------

        void BeginDeleteTab()
        {
            if (_tabId is not long id) return;
            var tab = _store.ListTabs().FirstOrDefault(t => t.id == id);
            if (tab.kind != "custom") return;

            if (tab.count == 0)
            {
                _store.DeleteTab(id);
                SwitchToClipboard();
                return;
            }

            TabConfirmText.Text = $"Delete \u201c{tab.name}\u201d? Its {tab.count} {(tab.count == 1 ? "clip" : "clips")} will be deleted. Pinned clips are not affected.";
            TabConfirm.Visibility = Visibility.Visible;
            TabConfirmCancel.Focus();
        }

        void ConfirmDeleteTab()
        {
            if (_tabId is not long id) return;
            _store.DeleteTab(id);
            CloseOverlays();
            SwitchToClipboard();
        }

        void OnTabConfirmCancel(object sender, RoutedEventArgs e) => CloseOverlays();
        void OnTabConfirmDelete(object sender, RoutedEventArgs e) => ConfirmDeleteTab();

        // ---- pin / move -----------------------------------------------------

        void TogglePin()
        {
            if (History.SelectedItem is not ClipRow row) return;
            int index = _visible.IndexOf(row);
            _store.TogglePin(row.Id);
            Reload();
            SelectNear(index);
        }

        void MoveSelectedToContainer(int direction)
        {
            if (History.SelectedItem is not ClipRow row) return;

            var containers = new List<long?> { null };
            containers.AddRange(_store.ListTabs().Select(t => (long?)t.id));
            int current = row.Item.TabId == null ? 0 : containers.IndexOf(row.Item.TabId);
            int next = current + direction;
            if (current < 0 || next < 0 || next >= containers.Count) return;

            int index = _visible.IndexOf(row);
            _store.MoveItemToTab(row.Id, containers[next]);
            Reload();
            SelectNear(index);
        }

        void SelectNear(int index)
        {
            if (_visible.Count == 0) return;
            History.SelectedItem = _visible[Math.Max(0, Math.Min(index, _visible.Count - 1))];
            History.ScrollIntoView(History.SelectedItem);
        }

        // ---- Ctrl+M: picker --------------------------------------------------

        void OpenPicker()
        {
            if (History.SelectedItem is not ClipRow row) return;

            var list = new List<PickerVm>();
            int n = 1;
            if (row.Item.TabId != null)
                list.Add(new PickerVm((n++).ToString(), "Clipboard", "", null));

            foreach (var t in _store.ListTabs())
                list.Add(new PickerVm(n <= 9 ? (n++).ToString() : "", t.name, t.count == 0 ? "" : t.count.ToString(), t.id));

            PickerList.ItemsSource = list;
            PickerList.SelectedIndex = 0;
            Picker.Visibility = Visibility.Visible;
            PickerList.Focus();
        }

        void CommitPicker()
        {
            if (History.SelectedItem is not ClipRow row) return;
            if (PickerList.SelectedItem is not PickerVm vm) { CloseOverlays(); return; }
            int index = _visible.IndexOf(row);
            _store.MoveItemToTab(row.Id, vm.TabId);
            CloseOverlays();
            Reload();
            SelectNear(index);
        }

        void OnPickerCommitClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is PickerVm)
                CommitPicker();
        }

        // ---- empty state (docs\DESIGN-antislop.md §7) -----------------------

        void UpdateEmptyState()
        {
            if (_visible.Count > 0)
            {
                EmptyPanel.Visibility = Visibility.Collapsed;
                if (_images) ImagesPane.Visibility = Visibility.Visible;
                return;
            }

            EmptyPanel.Visibility = Visibility.Visible;
            EmptyKeyRow.Visibility = Visibility.Visible;
            EmptyNote.Visibility = Visibility.Collapsed;
            if (_images)
            {
                // No image to show: the preview area has nothing truthful to draw.
                ImagesPane.Visibility = Visibility.Collapsed;
                PreviewCounterText.Text = "";
                DropPreview();
            }

            string query = (Search.Text ?? "").Trim();
            if (query.Length > 0)
            {
                string shown = query.Length > 40 ? query.Substring(0, 40) + "\u2026" : query;
                EmptyIcon.Data = (Geometry)FindResource(_images ? "IconPicture" : "IconClipboard");
                EmptyTitle.Text = (_images ? "No images match" : "No clips match") + " \u201c" + shown + "\u201d";
                EmptySub.Text = "Try fewer words, or search the app name.";
                EmptyKeyRow.Visibility = Visibility.Collapsed;
                return;
            }

            if (_images)
            {
                EmptyIcon.Data = (Geometry)FindResource("IconPicture");
                EmptyTitle.Text = "No images yet";
                EmptySub.Text = "Copy a screenshot or picture, and it appears here.";
                EmptyKey.Text = "Win+Shift+S";
                EmptyKeyHint.Text = "Take a screenshot";
                return;
            }

            if (_tabId is long id)
            {
                var tab = _store.ListTabs().FirstOrDefault(t => t.id == id);
                if (tab.kind == "builtin")
                {
                    EmptyIcon.Data = (Geometry)FindResource("IconPin");
                    EmptyTitle.Text = "Nothing pinned";
                    EmptySub.Text = "Press Ctrl+P on a clip to keep it here.";
                    EmptyKeyRow.Visibility = Visibility.Collapsed;
                    EmptyNote.Text = "Pinned clips stay when you clear history.";
                    EmptyNote.Visibility = Visibility.Visible;
                }
                else
                {
                    EmptyIcon.Data = (Geometry)FindResource("IconClipboard");
                    EmptyTitle.Text = "Nothing in \u201c" + tab.name + "\u201d";
                    EmptySub.Text = "Press Ctrl+M to move a clip into this tab.";
                    EmptyKeyRow.Visibility = Visibility.Collapsed;
                }
                return;
            }

            EmptyIcon.Data = (Geometry)FindResource("IconClipboard");
            EmptyTitle.Text = "No clips yet";
            EmptySub.Text = "Copy text or an image anywhere, and it appears here.";
            EmptyKey.Text = _cfg.Hotkey;
            EmptyKeyHint.Text = "Open this list from any app";
        }

        void OnNewTabClick(object sender, RoutedEventArgs e) => BeginCreateTab();
    }

    /// <summary>One tab in the flat tab strip.</summary>
    public sealed class TabVm
    {
        public TabVm(string name, int count, bool active, bool pinned, long? tabId, bool images)
        {
            Name = name;
            Count = count;
            CountText = count == 0 ? "" : count.ToString();
            IsActive = active;
            IsPinned = pinned;
            TabId = tabId;
            Images = images;
        }

        public string Name { get; }
        public int Count { get; }
        public string CountText { get; }
        public bool IsActive { get; }
        public bool IsPinned { get; }
        public long? TabId { get; }
        public bool Images { get; }
    }

    /// <summary>A destination row in the Ctrl+M picker.</summary>
    public sealed class PickerVm
    {
        public PickerVm(string index, string name, string countText, long? tabId)
        {
            Index = index;
            Name = name;
            CountText = countText;
            TabId = tabId;
        }

        public string Index { get; }
        public string Name { get; }
        public string CountText { get; }
        public long? TabId { get; }
    }
}
