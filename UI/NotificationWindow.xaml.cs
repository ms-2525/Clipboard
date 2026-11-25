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
using Clip.Interop;
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
        // Breathing room between a toast and the working area's edge, in DIP.
        const double EdgeGap = 12;
        // The narrowest a short toast can get; content decides between this and the configured cap.
        const double ToastMinWidth = 240;

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

            CloseHit.Click += (s, e) => { e.Handled = true; DismissNow(); };

            SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                int ex = GetWindowLong(h, GWL_EXSTYLE);
                // No WS_EX_TRANSPARENT: clicks reach the window, and the hit-test hook
                // lets everything outside the âœ• fall through to whatever is underneath.
                SetWindowLong(h, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
                WindowChrome.Apply(this);
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
                return (IntPtr)HTTRANSPARENT; // no client geometry yet: stay click-through rather than swallow the click
            }
        }

        public void ShowFor(ClipboardItem item)
        {
            var n = _cfg.Notification;
            _timer?.Stop();

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
                    // A thumb we cannot decode still has a truthful fallback: say an image was copied.
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
                SetBody(item.Text, n.MaxLines);
            }

            Present();
        }

        /// <summary>
        /// Auto-fit (DESIGN-language Â§3.2): the configured height is the maximum. Wraps at most
        /// <paramref name="maxLines"/> source lines, then drops lines until the measured toast fits, so the
        /// body is never cut mid-line. A one-line copy gets a short toast; a long one stops at the height.
        /// </summary>
        void SetBody(string text, int maxLines, bool trimLines = true)
        {
            var lines = (text ?? "").Replace("\r", "").Split('\n');
            int shown = Math.Min(Math.Max(1, maxLines), lines.Length);
            while (true)
            {
                Body.Text = BuildBody(lines, shown, trimLines);
                if (shown == 1 || Fits()) return;
                shown--;
            }
        }

        /// <summary>True when the laid-out content fits the configured maximum height (DIP).</summary>
        bool Fits()
        {
            MeasureRoot(_cfg.Notification.Width);
            return Root.DesiredSize.Height <= _cfg.Notification.Height;
        }

        /// <summary>
        /// Measures the toast content at <paramref name="width"/>. The half-DIP pass is deliberate: WPF reuses
        /// cached child sizes when the constraint is unchanged, so a text change would otherwise measure stale.
        /// </summary>
        void MeasureRoot(double width)
        {
            Root.InvalidateMeasure();
            Root.Measure(new Size(Math.Max(1, width - 0.5), double.PositiveInfinity));
            Root.Measure(new Size(width, double.PositiveInfinity));
        }

        static string BuildBody(string[] lines, int count, bool trimLines)
        {
            var body = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) body.Append('\n');
                body.Append(trimLines && lines[i].Length > 120 ? lines[i].Substring(0, 120) + "â€¦" : lines[i]);
            }
            return body.ToString().TrimEnd();
        }

        /// <summary>A plain message (first run, skipped capture, hotkey conflict) in the same reused toast.</summary>
        public void ShowMessage(string message)
        {
            var n = _cfg.Notification;
            _timer?.Stop();
            AppName.Text = Assembly.GetEntryAssembly().GetName().Name;
            AppName.Visibility = n.ShowApp ? Visibility.Visible : Visibility.Collapsed;
            Time.Visibility = Visibility.Collapsed;
            ThumbImg.Source = null;
            ThumbImg.Visibility = Visibility.Collapsed;
            Body.Foreground = (Brush)FindResource("Fg75");
            Body.Visibility = Visibility.Visible;
            SetBody(message ?? "", int.MaxValue, false);
            Present();
        }

        /// <summary>Shared placement + fade + auto-dismiss. The window instance lives for the session (S0 leak fix).</summary>
        void Present()
        {
            var n = _cfg.Notification;
            // Â§3.2: content decides both dimensions, the configured values cap them. A one-word copy
            // gets a narrow toast; a long paragraph stops at the maximum width and wraps until the
            // maximum height, never a fixed box with dead space.
            MeasureRoot(n.Width);
            double width = Math.Min(n.Width, Math.Max(ToastMinWidth, Math.Ceiling(Root.DesiredSize.Width)));
            Width = width;
            MeasureRoot(width);
            Height = Math.Min(n.Height, Math.Ceiling(Root.DesiredSize.Height));

            var s = WF.Screen.FromPoint(WF.Cursor.Position);
            // "Clip" alone binds to Window.Clip (Geometry), so qualify globally.
            double sc = global::Clip.Interop.ScreenDpi.Scale(s);
            var wa = s.WorkingArea;
            string pos = (n.Position ?? "").ToLowerInvariant();

            // Offsets are DIP so the 16 DIP gap stays a gap at every display scale.
            Left = pos switch
            {
                "top-right" or "bottom-right" => wa.Right / sc - n.OffsetX - Width,
                "center" => wa.Left / sc + (wa.Width / sc - Width) / 2,
                _ => wa.Left / sc + n.OffsetX,
            };

            Top = pos switch
            {
                "bottom-left" or "bottom-right" => wa.Bottom / sc - n.OffsetY - Height,
                "center" => wa.Top / sc + (wa.Height / sc - Height) / 2,
                _ => wa.Top / sc + n.OffsetY,
            };

            // Breathing room: an extreme offset can never pin the toast to (or past) a screen edge.
            Left = Math.Min(Math.Max(Left, wa.Left / sc + EdgeGap), wa.Right / sc - Width - EdgeGap);
            Top = Math.Min(Math.Max(Top, wa.Top / sc + EdgeGap), wa.Bottom / sc - Height - EdgeGap);

            // Same pixel snap as the popup: a fractional DIP origin blurs the whole toast.
            Left = Math.Round(Left * sc) / sc;
            Top = Math.Round(Top * sc) / sc;

            Root.Opacity = 0;
            Show();

            // A held opacity clock keeps the subtree greyscale even at 1.0: clear it the
            // moment the fade lands so ClearType returns for the rest of the toast's life.
            var fadeIn = Motion.Linear(1, 150);
            fadeIn.FillBehavior = FillBehavior.Stop;
            fadeIn.Completed += (s, e) =>
            {
                Root.BeginAnimation(OpacityProperty, null);
                Root.Opacity = 1;
            };
            Root.BeginAnimation(OpacityProperty, fadeIn);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(n.DurationMs) };
            _timer.Tick += (o, e) =>
            {
                _timer.Stop();
                var fade = Motion.Linear(0, 150);
                fade.Completed += (a, b) => HideAndReset();
                Root.BeginAnimation(OpacityProperty, fade);
            };
            _timer.Start();
        }

        void HideAndReset()
        {
            Root.BeginAnimation(OpacityProperty, null);
            Root.Opacity = 0;
            if (IsVisible) Hide();
        }

        public void DismissNow()
        {
            _timer?.Stop();
            HideAndReset();
        }

        /// <summary>
        /// Pays the process-wide first-show cost once, on this small window and off screen: the WPF
        /// render thread + D3D device are created when the first window is composited, which cost the
        /// popup ~430 ms on its first Alt+V. Invisible (0.4 % alpha) and never activated.
        /// </summary>
        public void WarmHidden()
        {
            if (IsVisible) return;
            double opacity = Opacity;
            Opacity = 0.004;
            Left = -32000;
            Top = -32000;
            Width = _cfg.Notification.Width;
            Height = _cfg.Notification.Height;
            Root.Opacity = 0;
            try
            {
                Show();
                UpdateLayout();
            }
            finally
            {
                Hide();
                Root.Opacity = 0;
                Opacity = opacity;
            }
        }
    }
}
