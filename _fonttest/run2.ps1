Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class G {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public static IntPtr Find(uint pid) {
    IntPtr f = IntPtr.Zero;
    EnumWindows((h,l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        var t = new StringBuilder(256); GetWindowTextW(h, t, 256);
        if (t.ToString().Contains("Font Test")) { f = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return f;
  }
}
"@
[void][G]::SetProcessDPIAware()

$exe = "C:\Users\mayan\CascadeProjects\Clipboard\_fonttest\bin\Release\net10.0-windows\fonttest.exe"
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4

$h = [G]::Find([uint32]$p.Id)
"hwnd=$h alive=$(-not $p.HasExited)"
[void][G]::ShowWindow($h, 5)
[void][G]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 2500

$r = New-Object G+RECT
[void][G]::GetWindowRect($h, [ref]$r)
"window rect $($r.Left),$($r.Top) $($r.Right - $r.Left)x$($r.Bottom - $r.Top)"

# Full screen capture then crop to window
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$full = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$gg = [System.Drawing.Graphics]::FromImage($full)
$gg.CopyFromScreen(0, 0, 0, 0, $full.Size)
$gg.Dispose()

$w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
if ($w -gt 50 -and $ht -gt 50) {
  $crop = New-Object System.Drawing.Bitmap $w, $ht
  $gc = [System.Drawing.Graphics]::FromImage($crop)
  $gc.DrawImage($full, (New-Object System.Drawing.Rectangle 0,0,$w,$ht), (New-Object System.Drawing.Rectangle $r.Left,$r.Top,$w,$ht), [System.Drawing.GraphicsUnit]::Pixel)
  $gc.Dispose()
  $crop.Save("$env:TEMP\font_proof2.png", [System.Drawing.Imaging.ImageFormat]::Png)
  $crop.Dispose()
  "cropped=$env:TEMP\font_proof2.png"
}
$full.Save("$env:TEMP\font_full.png", [System.Drawing.Imaging.ImageFormat]::Png)
$full.Dispose()
"full=$env:TEMP\font_full.png"

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
"closed"
