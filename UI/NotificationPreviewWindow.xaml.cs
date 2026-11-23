using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Clip.Core;
using Clip.Interop;
using WF = System.Windows.Forms;

namespace Clip.UI
{
    /// <summary>
    /// Ghost of the notification toast shown while the Notification tab is edited.
    /// Placement is the same math as <see cref="NotificationWindow"/>, except the size comes
    /// from config (the real toast sizes itself to its content).
    /// </summary>
    public partial class NotificationPreviewWindow : Window
    {
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x80;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int IdleHideMs = 4000;

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        static NotificationPreviewWindow _ghost;
        static DispatcherTimer _idle;
        static Window _owner;
        static string _position = "bottom-right";

        /// <summary>True while the Settings "Show preview" toggle pins the ghost open.</summary>
        internal static bool Pinned { get; set; }

        /// <summary>OffsetX/OffsetY (config pixels) after the user drags the ghost.</summary>
        internal static event Action<int, int> Dragged;

        public NotificationPreviewWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var h = new WindowInteropHelper(this).Handle;
            int ex = GetWindowLong(h, GWL_EXSTYLE);
            SetWindowLong(h, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        internal static void ShowGhost(Config cfg, Window owner)
        {
            if (owner != null) _owner = owner;
            Ensure().Apply(cfg);
        }

        internal static void UpdateGhost(Config cfg)
        {
            if (_ghost == null)
            {
                // Nothing on screen yet: the first change opens it, as ShowGhost would.
                if (_owner != null) ShowGhost(cfg, _owner);
                return;
            }
            _ghost.Apply(cfg);
        }

        internal static void HideGhost()
        {
            _idle?.Stop();
            _ghost?.Hide();
        }

        static NotificationPreviewWindow Ensure()
        {
            if (_ghost != null) return _ghost;

            _idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(IdleHideMs) };
            _idle.Tick += (s, e) =>
            {
                _idle.Stop();
                if (!Pinned) _ghost?.Hide();
            };
            _ghost = new NotificationPreviewWindow();
            return _ghost;
        }

        void Apply(Config cfg)
        {
            var n = cfg.Notification ?? new Config.NotificationSettings();

            Width = n.Width;
            Height = n.Height;
            _position = (n.Position ?? "bottom-right").ToLowerInvariant();
            Shell.Cursor = _position == "center" ? Cursors.Arrow : Cursors.SizeAll;

            var surface = (SolidColorBrush)FindResource("Surface");
            Wash.Fill = new SolidColorBrush(surface.Color) { Opacity = 0.82 };
            Wash.Stroke = (Brush)FindResource("Accent");
            Label.Foreground = (Brush)FindResource("Fg");

            Place(n);
            if (!IsVisible) Show();

            // Every edit restarts the idle clock; a pin ignores it.
            _idle?.Stop();
            if (!Pinned) _idle?.Start();
        }

        void Place(Config.NotificationSettings n)
        {
            var s = ScreenFor();
            double sc = ScreenDpi.Scale(s);
            var wa = s.WorkingArea;
            double w = Width, h = Height;

            Left = _position switch
            {
                "top-right" or "bottom-right" => (wa.Right - n.OffsetX) / sc - w,
                "center" => wa.Left / sc + (wa.Width / sc - w) / 2,
                _ => wa.Left / sc + n.OffsetX,
            };

            Top = _position switch
            {
                "bottom-left" or "bottom-right" => (wa.Bottom - n.OffsetY) / sc - h,
                "center" => wa.Top / sc + (wa.Height / sc - h) / 2,
                _ => wa.Top / sc + n.OffsetY,
            };
        }

        /// <summary>The monitor the Settings window is on, not the mouse's.</summary>
        static WF.Screen ScreenFor()
        {
            if (_owner != null && _owner.IsVisible)
            {
                var p = _owner.PointToScreen(
                    new Point(_owner.ActualWidth / 2, _owner.ActualHeight / 2));
                return WF.Screen.FromPoint(new System.Drawing.Point((int)p.X, (int)p.Y));
            }
            return WF.Screen.FromPoint(WF.Cursor.Position);
        }

        void OnDragStart(object sender, MouseButtonEventArgs e)
        {
            // Center mode has no offsets to set, so it is not draggable.
            if (_position == "center" || e.LeftButton != MouseButtonState.Pressed) return;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                return; // button was released between the event and the call
            }
            CommitDrag();
        }

        void CommitDrag()
        {
            var s = ScreenFor();
            double sc = ScreenDpi.Scale(s);
            var wa = s.WorkingArea;
            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : Height;

            // Inverse of Place(): right/bottom offsets are physical pixels, left/top are DIPs.
            int ox = _position == "top-right" || _position == "bottom-right"
                ? (int)Math.Round(wa.Right - sc * (Left + w))
                : (int)Math.Round(Left - wa.Left / sc);
            int oy = _position == "bottom-left" || _position == "bottom-right"
                ? (int)Math.Round(wa.Bottom - sc * (Top + h))
                : (int)Math.Round(Top - wa.Top / sc);

            Dragged?.Invoke(ox, oy);
        }
    }
}
