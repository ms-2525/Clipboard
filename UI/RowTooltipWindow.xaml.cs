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
        // 340 DIP: fits beside the centred 550 DIP popup on a 1280 DIP-wide screen (365 DIP free).
        const double TipWidth = 340;

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

        /// <summary>Shows full text beside the popup, its left edge on the popup's right edge.</summary>
        internal void ShowFor(Window owner, ClipRow row, double rowTop)
        {
            Body.Text = row?.Tooltip ?? "";
            Width = TipWidth;
            MaxHeight = Math.Max(160, owner.ActualHeight);
            Scroller.ScrollToTop();

            if (!IsVisible) Show();
            UpdateLayout();

            var handle = new WindowInteropHelper(owner).Handle;
            var screen = handle != IntPtr.Zero ? WF.Screen.FromHandle(handle) : WF.Screen.FromPoint(WF.Cursor.Position);
            double scale = ScreenDpi.Scale(screen);
            var work = screen.WorkingArea;
            double left = owner.Left + owner.ActualWidth;
            double rightEdge = work.Right / scale;
            if (left + TipWidth > rightEdge) left = owner.Left - TipWidth;

            double top = owner.Top + rowTop;
            double bottomEdge = work.Bottom / scale;
            if (top + ActualHeight > bottomEdge) top = bottomEdge - ActualHeight;
            double topEdge = work.Top / scale;
            if (top < topEdge) top = topEdge;
            if (left < work.Left / scale) left = work.Left / scale;

            Left = left;
            Top = top;
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
