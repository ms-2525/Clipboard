Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class F {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public static IntPtr Find(uint pid) {
    IntPtr f = IntPtr.Zero;
    EnumWindows((h,l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        var t = new StringBuilder(256); GetWindowTextW(h, t, 256);
        if (t.ToString() == "Font Test") { f = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return f;
  }
}
"@
[void][F]::SetProcessDPIAware()

$exe = "C:\Users\mayan\CascadeProjects\Clipboard\_fonttest\bin\Release\net10.0-windows\fonttest.exe"
if (-not (Test-Path $exe)) { "EXE MISSING"; exit 1 }

$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4

$m = Get-Process -Id $p.Id
"RAM={0} MB  Threads={1}" -f [math]::Round($m.WorkingSet64/1MB,1), $m.Threads.Count

$h = [F]::Find([uint32]$p.Id)
if ($h -eq [IntPtr]::Zero) { "NO WINDOW"; Stop-Process -Id $p.Id -Force; exit 1 }

[void][F]::SetWindowPos($h, (New-Object IntPtr(-1)), 120, 90, 940, 700, 0x0040)
Start-Sleep -Milliseconds 2000

$r = New-Object F+RECT
[void][F]::GetWindowRect($h, [ref]$r)
$w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
"rect=${w}x${ht}"

$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $w, $ht))
$g.Dispose()
$bmp.Save("$env:TEMP\font_proof.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"saved=$env:TEMP\font_proof.png"

Start-Sleep -Milliseconds 500
Stop-Process -Id $p.Id -Force
"closed"
