using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clip.Core;
using WF = System.Windows.Forms;

namespace Clip.UI
{
    public partial class NotificationWindow : Window
    {
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x80;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int WM_NCHITTEST = 0x0084;
        const int HTTRANSPARENT = -1;
        const int HTCLIENT = 1;

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }

        readonly Config _cfg;
        DispatcherTimer _timer;

        public NotificationWindow(Config cfg)
        {
            InitializeComponent();
            _cfg = cfg;
            Title = Assembly.GetEntryAssembly().GetName().Name;

            CloseHit.MouseLeftButtonUp += (s, e) => { e.Handled = true; DismissNow(); };

            SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                int ex = GetWindowLong(h, GWL_EXSTYLE);
                // No WS_EX_TRANSPARENT: clicks reach the window, and the hit-test hook
                // lets everything outside the ✕ fall through to whatever is underneath.
                SetWindowLong(h, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
                HwndSource.FromHwnd(h)?.AddHook(HitTest);
            };
        }

        IntPtr HitTest(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_NCHITTEST) return IntPtr.Zero;
            handled = true;

            // Packed screen coordinates of the cursor.
            long lp = lParam.ToInt64();
            var pt = new POINT { X = (short)(lp & 0xFFFF), Y = (short)((lp >> 16) & 0xFFFF) };
            ScreenToClient(hwnd, ref pt);

            try
            {
                // ScreenToClient is physical pixels; the element lives in DIPs.
                var fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
                var p = fromDevice?.Transform(new Point(pt.X, pt.Y)) ?? new Point(pt.X, pt.Y);

                var origin = CloseHit.TransformToAncestor(this).Transform(new Point(0, 0));
                var closeBox = new Rect(origin.X, origin.Y, CloseHit.ActualWidth, CloseHit.ActualHeight);
                return (IntPtr)(closeBox.Contains(p) ? HTCLIENT : HTTRANSPARENT);
            }
            catch
            {
                return (IntPtr)HTTRANSPARENT;
            }
        }

        public void ShowFor(ClipboardItem item)
        {
            var n = _cfg.Notification;

            AppName.Text = item.AppName;
            AppName.Visibility = n.ShowApp ? Visibility.Visible : Visibility.Collapsed;
            Time.Text = DateTime.TryParse(item.CapturedAt, out var t)
                ? t.ToString(string.IsNullOrEmpty(ClipboardItem.ClockFormat) ? "HH:mm" : ClipboardItem.ClockFormat)
                : "";
            Time.Visibility = n.ShowTime ? Visibility.Visible : Visibility.Collapsed;

            ThumbImg.Source = null;
            ThumbImg.Visibility = Visibility.Collapsed;
            Body.Visibility = Visibility.Visible;
            Body.Foreground = (Brush)FindResource("Fg");

            bool image = item.Kind == "image";
            bool hasThumb = image && item.Thumb != null && item.Thumb.Length > 0;

            if (hasThumb)
            {
                try
                {
                    var thumb = new BitmapImage();
                    using (var ms = new MemoryStream(item.Thumb))
                    {
                        thumb.BeginInit();
                        thumb.CacheOption = BitmapCacheOption.OnLoad;
                        thumb.StreamSource = ms;
                        thumb.EndInit();
                    }
                    thumb.Freeze();
                    ThumbImg.Source = thumb;
                    ThumbImg.MaxHeight = Math.Max(60, n.Height - 90);
                    ThumbImg.Visibility = Visibility.Visible;
                    Body.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    Body.Text = "Image copied";
                    Body.Foreground = (Brush)FindResource("Muted");
                }
            }
            else if (image)
            {
                Body.Text = "Image copied";
                Body.Foreground = (Brush)FindResource("Muted");
            }
            else
            {
                var lines = (item.Text ?? "").Replace("\r", "").Split('\n');
                int count = Math.Min(Math.Max(1, n.MaxLines), lines.Length);
                var body = new StringBuilder();
                for (int i = 0; i < count; i++)
                {
                    if (i > 0) body.Append('\n');
                    body.Append(lines[i].Length > 120 ? lines[i].Substring(0, 120) + "…" : lines[i]);
                }
                Body.Text = body.ToString().TrimEnd();
            }

            Width = n.Width;
            Root.MaxHeight = n.Height;

            var s = WF.Screen.FromPoint(WF.Cursor.Position);
            // "Clip" alone binds to Window.Clip (Geometry), so qualify globally.
            double sc = global::Clip.Interop.ScreenDpi.Scale(s);
            var wa = s.WorkingArea;
            string pos = (n.Position ?? "").ToLowerInvariant();

            Left = pos switch
            {
                "top-right" or "bottom-right" => (wa.Right - n.OffsetX) / sc - Width,
                "center" => wa.Left / sc + (wa.Width / sc - Width) / 2,
                _ => wa.Left / sc + n.OffsetX,
            };

            double TopFor(double h) => pos switch
            {
                "bottom-left" or "bottom-right" => (wa.Bottom - n.OffsetY) / sc - h,
                "center" => wa.Top / sc + (wa.Height / sc - h) / 2,
                _ => wa.Top / sc + n.OffsetY,
            };

            Top = TopFor(ActualHeight);

            Show();
            UpdateLayout();
            // SizeToContent: the real height is only known after the first layout pass.
            Top = TopFor(ActualHeight);

            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(n.DurationMs) };
            _timer.Tick += (o, e) =>
            {
                _timer.Stop();
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
                fade.Completed += (a, b) => { if (IsVisible) Close(); };
                Root.BeginAnimation(OpacityProperty, fade);
            };
            _timer.Start();
        }

        public void DismissNow()
        {
            _timer?.Stop();
            if (IsVisible) Close();
        }
    }
}
