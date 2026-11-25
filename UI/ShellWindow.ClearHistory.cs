using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clip.Interop;

namespace Clip.UI
{
    public partial class ShellWindow
    {
        Window _clearDialog;

        /// <summary>
        /// Inline confirmation, same words and same scope — main history
        /// only, pinned clips and tab items stay (<see cref="Clip.Core.ClipboardStore.Clear"/>).
        /// </summary>
        internal void ConfirmClearHistory()
        {
            if (_clearDialog != null)
            {
                _clearDialog.Activate();
                return;
            }

            var title = new TextBlock
            {
                Text = "Clear history",
                FontSize = (double)FindResource("FontDisplaySize"),
                FontWeight = FontWeights.Medium,
                Foreground = (Brush)FindResource("Fg")
            };
            var note = new TextBlock
            {
                Text = "Delete all clips in Clipboard and Images? Pinned clips and tabs stay.",
                FontSize = (double)FindResource("FontBodySize"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("Fg75"),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var keep = new Button
            {
                Content = "Keep clips",
                Style = (Style)FindResource("SecondarySmall"),
                MinWidth = 96
            };
            var delete = new Button
            {
                Content = "Delete clips",
                Style = (Style)FindResource("DangerSmall"),
                MinWidth = 96,
                Margin = new Thickness(8, 0, 0, 0)
            };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 18, 0, 0)
            };
            buttons.Children.Add(keep);
            buttons.Children.Add(delete);

            var stack = new StackPanel();
            stack.Children.Add(title);
            stack.Children.Add(note);
            stack.Children.Add(buttons);

            var window = new Window
            {
                Title = "Clear history",
                Owner = IsVisible ? this : null,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.Height,
                Width = 420,
                WindowStartupLocation = IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Background = (Brush)FindResource("Surface"),
                FontFamily = (FontFamily)FindResource("UiFont"),
                Content = new Border
                {
                    BorderBrush = (Brush)FindResource("Hairline"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(22),
                    Child = stack
                }
            };

            window.SourceInitialized += (s, e) => WindowChrome.Apply(window);
            keep.Click += (s, e) => window.Close();
            window.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                window.Close();
            };
            delete.Click += (s, e) =>
            {
                _store.Clear();
                NotifyCleared();
                window.Close();
            };
            window.Closed += (s, e) => _clearDialog = null;

            _clearDialog = window;
            window.Show();
            window.Activate();
        }
    }
}
