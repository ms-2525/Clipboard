using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Clip.Core;

namespace Clip.UI
{
    /// <summary>The keyboard contract and the one ordered Esc cascade (docs\RESEARCH-keyboard-ux.md §8/§10).</summary>
    public partial class MainWindow
    {
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // The IME owns Esc/Enter/Delete/arrows while a composition is open.
            if (e.Key == Key.ImeProcessed || _composing) return;

            // Cascade levels 3–5: overlays own the keyboard while open.
            if (TabConfirm.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape || e.Key == Key.Enter) CloseOverlays();
                e.Handled = true;
                return;
            }
            if (TabEditor.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Enter) CommitCreateTab();
                else if (e.Key == Key.Escape) CancelCreateTab();
                else return;
                e.Handled = true;
                return;
            }
            if (Picker.Visibility == Visibility.Visible)
            {
                HandlePickerKey(e);
                return;
            }

            // Level 6: the editor is a separate window and owns its own keys.
            if (_editor != null) return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            if (e.Key == Key.Escape)
            {
                // Level 7 / 8: clear search first, then hide.
                if (Search.Text.Length > 0)
                {
                    Search.Clear();
                    ApplyFilter();
                    Search.Focus();
                }
                else ClosePopup();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (History.SelectedItem is ClipRow enterRow) RequestPaste(enterRow, plain: shift);
                e.Handled = true;
                return;
            }

            // Tab never moves focus off the single control in the popup.
            if (e.Key == Key.Tab)
            {
                if (ctrl) SwitchAdjacentTab(shift ? -1 : 1);
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                int index = e.Key - Key.D1;
                if (index < _visible.Count) RequestPaste(_visible[index], plain: false);
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.C)
            {
                if (Search.SelectionLength == 0 && History.SelectedItem is ClipRow copyRow)
                {
                    CopyRequested?.Invoke(copyRow.Item);
                    PulseRow(copyRow);
                    e.Handled = true;
                }
                return; // otherwise the search box's native copy
            }

            if (ctrl && e.Key == Key.Z)
            {
                if (_undoItem != null)
                {
                    UndoDelete();
                    e.Handled = true;
                }
                return; // no undo buffer: native text undo
            }

            if (ctrl && e.Key == Key.P)
            {
                TogglePin();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.M)
            {
                OpenPicker();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.T)
            {
                BeginCreateTab();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.W)
            {
                BeginDeleteTab();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.Up) { JumpSection(-1); e.Handled = true; return; }
            if (ctrl && e.Key == Key.Down) { JumpSection(1); e.Handled = true; return; }

            if (ctrl && shift && (e.Key == Key.Left || e.Key == Key.Right))
            {
                MoveSelectedToContainer(e.Key == Key.Left ? -1 : 1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                // Images: ↑/↓ step one image; text lists: ↑/↓ move one row.
                if (_images) { StepImage(e.Key == Key.Down ? 1 : -1); e.Handled = true; return; }
                Navigate(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                // ←/→ switch tabs in EVERY tab, Images included (owner directive 23 Nov).
                // Arrows stay with the caret only while a non-empty query is being edited.
                if (Search.Text.Length == 0 || !Search.IsKeyboardFocusWithin)
                {
                    SwitchAdjacentTab(e.Key == Key.Right ? 1 : -1);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Home && Search.Text.Length == 0) { SelectAt(0); e.Handled = true; return; }
            if (e.Key == Key.End && Search.Text.Length == 0) { SelectAt(_visible.Count - 1); e.Handled = true; return; }

            if (e.Key == Key.PageUp) { PageMove(-1); e.Handled = true; return; }
            if (e.Key == Key.PageDown) { PageMove(1); e.Handled = true; return; }

            if (e.Key == Key.Delete)
            {
                // Search focused with text: character delete. Search empty: the key belongs to the row.
                if (Keyboard.FocusedElement is TextBoxBase && Search.Text.Length > 0) return;
                DeleteSelected();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F2)
            {
                if (History.SelectedItem is ClipRow editRow) OpenEditor(editRow);
                e.Handled = true;
                return;
            }

            // F10 arrives as a system key (WM_SYSKEYDOWN), so the real key is SystemKey.
            if (e.Key == Key.Apps || ((e.Key == Key.F10 || e.SystemKey == Key.F10) && shift))
            {
                ShowRowMenu(History.SelectedItem as ClipRow, atMouse: false);
                e.Handled = true;
            }
        }

        /// <summary>Typing anywhere in the popup lands in the search box (closes the post-F2 dead state).</summary>
        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            base.OnPreviewTextInput(e);
            if (_composing || AnyOverlayOpen || _editor != null) return;
            if (e.Text.Length == 0 || char.IsControl(e.Text[0])) return;
            if (Keyboard.FocusedElement is TextBoxBase) return;

            Search.Focus();
            Search.Text += e.Text;
            Search.CaretIndex = Search.Text.Length;
            e.Handled = true;
        }

        void HandlePickerKey(KeyEventArgs e)
        {
            if (e.Key == Key.Escape || (e.Key == Key.M && (Keyboard.Modifiers & ModifierKeys.Control) != 0))
            {
                CloseOverlays();
                Search.Focus();
            }
            else if (e.Key == Key.Enter) CommitPicker();
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                int count = PickerList.Items.Count;
                if (count > 0)
                    PickerList.SelectedIndex = Math.Max(0, Math.Min(count - 1, PickerList.SelectedIndex + (e.Key == Key.Down ? 1 : -1)));
            }
            else if (e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                int index = e.Key - Key.D1;
                if (index < PickerList.Items.Count)
                {
                    PickerList.SelectedIndex = index;
                    CommitPicker();
                }
            }
            e.Handled = true;
        }

        void Navigate(int delta) => SelectAt(History.SelectedIndex + delta);

        void PageMove(int direction)
        {
            // One page = the rows that fit the viewport at the CURRENT row's height (heights vary now).
            double rowHeight = (History.SelectedItem as ClipRow)?.RowHeight ?? 34;
            int page = Math.Max(1, (int)Math.Floor(History.ActualHeight / Math.Max(1, rowHeight)));
            SelectAt(History.SelectedIndex + direction * page);
        }

        /// <summary>Minimal delta only: WPF's ScrollIntoView is a no-op on a fully visible row.</summary>
        void SelectAt(int index)
        {
            if (_visible.Count == 0) return;
            History.SelectedItem = _visible[Math.Max(0, Math.Min(_visible.Count - 1, index))];
            History.ScrollIntoView(History.SelectedItem);
        }

        void JumpSection(int direction)
        {
            if (History.SelectedItem is not ClipRow current || _visible.Count == 0) return;
            string section = current.SectionLabel;
            int start = _visible.IndexOf(current);

            if (direction > 0)
            {
                for (int i = start + 1; i < _visible.Count; i++)
                    if (_visible[i].SectionLabel != section) { SelectAt(i); return; }
                SelectAt(_visible.Count - 1);
            }
            else
            {
                // Walk back to the first row of the previous section.
                int i = start;
                while (i > 0 && _visible[i - 1].SectionLabel == section) i--;
                if (i == 0) { SelectAt(0); return; }
                string previous = _visible[i - 1].SectionLabel;
                while (i > 0 && _visible[i - 1].SectionLabel == previous) i--;
                SelectAt(i);
            }
        }

        void SwitchAdjacentTab(int direction)
        {
            int current = _tabVms.FindIndex(v => v.IsActive);
            int next = current + direction;
            if (current < 0 || next < 0 || next >= _tabVms.Count) return;
            var vm = _tabVms[next];
            SwitchTab(vm.TabId, vm.Images, vm.Name);
        }

        void DeleteSelected()
        {
            if (History.SelectedItem is not ClipRow row) return;

            var undo = new ClipboardItem
            {
                Id = row.Item.Id,
                Kind = row.Item.Kind,
                Text = FullText(row),
                AppName = row.Item.AppName,
                WindowTitle = row.Item.WindowTitle,
                CapturedAt = row.Item.CapturedAt,
                SizeBytes = row.Item.SizeBytes,
                TabId = row.Item.TabId,
                Formats = _store.GetFormats(row.Id)
            };
            if (row.IsImage) undo.Image = _store.GetImage(row.Id)?.Image;
            // The thumb rides along too: a restored image whose tile says "No preview" is a lie.
            undo.Thumb = row.Item.Thumb;
            var rich = _store.GetRich(row.Id);
            if (rich != null) { undo.Html = rich.Html; undo.Rtf = rich.Rtf; }

            // shortcut: one-level, session-only undo buffer; add levels only if the owner asks
            int index = _visible.IndexOf(row);
            _store.Delete(row.Id);
            _undoItem = undo;
            _rows.Remove(row);
            ApplyFilter();
            SelectNear(index);
            ReloadTabs();
        }

        void UndoDelete()
        {
            if (_undoItem == null) return;
            var item = _undoItem;
            _undoItem = null;
            _store.Restore(item);

            if (item.TabId != _tabId)
            {
                string tabName = "Clipboard";
                if (item.TabId != null)
                {
                    var tab = _store.ListTabs().FirstOrDefault(t => t.id == item.TabId);
                    if (tab.name != null) tabName = tab.name;
                }
                SwitchTab(item.TabId, false, tabName);
            }
            else Reload();

            // The restored row must be visibly back: drop a query that would hide it.
            if (!_rows.Any(r => r.Id == item.Id))
            {
                _suppressFilter = true;
                Search.Clear();
                _suppressFilter = false;
                Reload();
            }
            var restored = _visible.FirstOrDefault(r => r.Id == item.Id);
            if (restored != null)
            {
                History.SelectedItem = restored;
                History.ScrollIntoView(restored);
            }
            ReloadTabs();
        }
    }
}
