using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Clip.Interop;
using WF = System.Windows.Forms;

namespace Clip.UI
{
    /// <summary>
    /// Full text of a truncated row, docked OUTSIDE the popup (owner directive 23 Nov): it never covers
    /// another row, never takes focus, and flips to the popup's left when the right edge is too close.
    /// </summary>
    public partial class RowTooltipWindow : Window
    {
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x80;
        const int WS_EX_NOACTIVATE = 0x08000000;
        // 390 DIP is the comfortable reading width; narrower screens get whatever room is left.
        const double TipWidth = 390;
        // Air between the tip, the popup and the screen edges (DIP).
        const double Gap = 12;
        const double TipMinWidth = 200;

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public RowTooltipWindow()
        {
            InitializeComponent();
            Title = "Clip tooltip";
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var h = new WindowInteropHelper(this).Handle;
            int ex = GetWindowLong(h, GWL_EXSTYLE);
            SetWindowLong(h, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        /// <summary>Shows full text beside the popup, in the free side, never off the screen.</summary>
        internal void ShowFor(Window owner, ClipRow row, double rowTop)
        {
            Body.Text = row?.Tooltip ?? "";

            var handle = new WindowInteropHelper(owner).Handle;
            var screen = handle != IntPtr.Zero ? WF.Screen.FromHandle(handle) : WF.Screen.FromPoint(WF.Cursor.Position);
            double scale = ScreenDpi.Scale(screen);
            var work = screen.WorkingArea;
            double leftEdge = work.Left / scale + Gap;
            double rightEdge = work.Right / scale - Gap;
            double topEdge = work.Top / scale + Gap;
            double bottomEdge = work.Bottom / scale - Gap;

            // The popup owns the middle of the screen, so the tip takes the wider free side and
            // shrinks to it instead of clamping back over the list.
            double ownerRight = owner.Left + owner.ActualWidth;
            double rightRoom = rightEdge - ownerRight - Gap;
            double leftRoom = owner.Left - Gap - leftEdge;
            bool onRight = rightRoom >= leftRoom;
            double room = Math.Max(TipMinWidth, Math.Max(0, onRight ? rightRoom : leftRoom));
            Width = Math.Min(TipWidth, room);

            // Anchored on the row it explains: grow down from the row's top, or up when there is
            // no useful room below. The cap keeps it inside the working area.
            double rowTopDip = owner.Top + rowTop;
            double below = bottomEdge - rowTopDip;
            double above = rowTopDip - topEdge;
            bool growUp = below < 240 && above > below;
            MaxHeight = Math.Max(160, growUp ? above : below);

            if (!IsVisible) Show();
            UpdateLayout();
            Scroller.ScrollToTop();

            double left = onRight ? ownerRight + Gap : owner.Left - Gap - ActualWidth;
            left = Math.Min(Math.Max(left, leftEdge), rightEdge - ActualWidth);

            double top = Math.Min(Math.Max(growUp ? rowTopDip - ActualHeight : rowTopDip, topEdge),
                bottomEdge - ActualHeight);
            // Pixel snap like the popup/toast: a fractional DIP origin blurs the whole tip.
            double tipScale = ScreenDpi.Scale(screen);
            Left = Math.Round(Math.Min(Math.Max(left, leftEdge), rightEdge - ActualWidth) * tipScale) / tipScale;
            Top = Math.Round(Math.Max(topEdge, top) * tipScale) / tipScale;
        }

        internal void Dismiss()
        {
            if (!IsVisible) return;
            Hide();
            OnDismissed?.Invoke();
        }

        /// <summary>Set by the popup: drops its dwell state when the pointer leaves this window.</summary>
        internal Action OnDismissed { get; set; }

        /// <summary>True while the mouse is over this window — moving here is not leaving the row.</summary>
        internal bool ContainsCursor()
        {
            if (!IsVisible) return false;
            var cursor = WF.Cursor.Position;
            double scale = ScreenDpi.Scale(WF.Screen.FromPoint(cursor));
            double x = cursor.X / scale;
            double y = cursor.Y / scale;
            return x >= Left && x <= Left + ActualWidth && y >= Top && y <= Top + ActualHeight;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            Dismiss();
        }
    }
}
