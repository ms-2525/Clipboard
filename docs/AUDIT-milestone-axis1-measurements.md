# AUDIT-milestone-axis1-measurements — raw numbers behind the Axis-1 verdict

Run: 23 Nov 2026 22:14–22:55 local. Agent: fresh Axis-1 sub-agent (wrote no product code).
Display: one 1920x1200 @150% (1280x800 DIP). All captures set `SetProcessDpiAwarenessContext(-4)` first.
Binary under test: a build of the tree as of 22:23:07 (`%TEMP%\opencode\audit1\out\Clip.exe`) — the packaged
`bin\Release\net10.0-windows\Clip.exe` is 21:41:45, older than four source files (22:22:41) and
`Core\ClipboardStore.Items.cs` (22:27:14). A from-scratch build of the current tree on a copy: 0 errors / 0 warnings.

## Popup geometry (UIA + win32)
- popup window 548,114 **825x780 px = 550x520 DIP** (unchanged across 12 measurements)
- popup exstyle **0x88 = WS_EX_TOPMOST | WS_EX_TOOLWINDOW** (acceptance-v2 P1 "not topmost" is fixed)
- 1-line row h = **51 px** (34 DIP = 16 + 1x18)
- 10-line row h = **294 px** (196 DIP = 16 + 10x18)
- rich 3-line row h = **138 px** (92 DIP — NOT 16+3x18; rich rows size to the rendered block)

## Zero-jump on arrow navigation (pixel diff of my own captures)
- baseline: row1 selected (y 267..405), row2 selected after one Down (y 405..699)
- diff of the band BELOW the newly selected row (img y 593–730): **0 differing pixels**
- diff of the band ABOVE the previously selected row (img y 79–160): **0 differing pixels**
- row 1 height before/after deselection: **138 px both times** (no height change)
- second Down: list scrolls 138 px (one row), selected row lands at y=267, height unchanged 294 px

## Row surfaces (pixel scans, dark theme)
- App background `#08090C`; odd row fill = `#08090C`
- even row fill = `#16181D` (`RowAlt`, Dark.xaml:41)
- row divider = `#212329` (`Divider`, Dark.xaml:24), 1–2 px band between rows
- selected row fill `#1C1310` + inset ring `#3E2517`
- hover on an even row: `#16181D` → **`#1F2126`** (+9/+9/+9 ≈ the 3.9 % white of `RowHover` `#0AFFFFFF`);
  the divider under the hovered row `#212329` → `#2A2C31` — a tint over the zebra, not a flat hover fill
- selected row is unaffected by hover (selection wins)

## Bottom bar (screenshots, 825x80 crops of the popup footer)
- row 1 selected: `WindowsTerminal · OC | Win… · 23 Nov 22:29` + `6 clips` + `Enter paste` `F2 edit` `Esc hide`
- last row selected (End): `copyq · AUDIT-RICH bold… · 23 Nov 22:24` — app, title AND time all follow the selection
- UIA note: the `SelectionBar` element's automation NAME is the static label "Selected clip source, title and time";
  the live text is `SelectionBar.Text = row.Status` (MainWindow.xaml.cs:277). Automated checks must read the pixels.

## Badge math (DB line counts vs rendered)
- 30-line fixture (1 visual line per source line): 10 shown, **+20** = 30 − 10, `previewLines=10`
- 19-line paragraph of ~97-char lines wraps to 38 visual lines: 10 shown, **+28** = 38 − 10
  (the badge counts rendered/wrapped lines — truthful for what is hidden)

## Outside tooltip (live, current build)
- parked mouse: 1 window (the popup)
- 1.5 s hover on a truncated row: second window **1373,348 510x780** `ex=0x8000088`
  (left edge 1373 = popup right edge 548+825 exactly; height 780 = popup height; NOACTIVATE|TOPMOST|TOOLWINDOW)
- move away → tooltip gone
- 1.4 s hover on the NON-truncated rich row → no second window
- caveat: the tooltip's top (348) did not coincide with the hovered row's top (405); vertical anchoring is not
  what CHECKLIST-features §2 documents ("top on the row's top")
- flip: `popup-visual\dark-tip-flip.png` — after dragging the popup right, the same tooltip sits flush on its LEFT

## Images tab
- Right: `Search Images`, counter `1 / 1`, status `1 image`; preview pane ≈596x393 px inside the 550x520 popup
- Right again: `Search Pinned` (status count empty — known P3); Left: back to Images
- Down on Images steps the image (counter stays 1/1 with a single image)
- numbered tile strip below the preview, selected tile carries the orange ring

## Editor (F2 on the RTF clip; window 540,132 **840x744 px = 560x496 DIP**)
- toolbar: B I U S | A- 12 A+ | A (colour) | highlight | Clear — WordPad-class, no image/table/alignment
- content opens WYSIWYG (bold heading + blue underlined run + plain third line), ClearType fringing visible at 3x
- text box border **9.3 DIP** from the window frame; first neutral glyph ink **11.3 DIP** (checklist says 13.3 — glyph bearing)
- footer `Ctrl+S saves` · Cancel · **Save changes** (orange primary)
- Esc with no edits → silent close, popup back with focus
- editor survives an outside click (still visible 1.2 s later, same rect). One earlier run found the editor gone by
  the time a later process looked — cause not established (a focus change, not a keystroke); recorded as an open
  question, **no data loss was demonstrated**

## Toast (live)
- two copies 300 ms apart → exactly **one** toast window: **1274,872 630x240 px = 420x160 DIP**, `ex=0x8000088`
- auto-hidden ~3 s after the last copy
- settings ghost preview (`integration\settings-dark-with-ghost-preview.png`, measured): dashed rect
  x 1274..1903 (630 px = 420 DIP), top edge y=872, bottom edge y=1111 (240 px = 160 DIP) — exactly the live toast's box

## Text crispness
- popup row text band (100 px tall, 770 px wide): 2634 text pixels, **1696 colour-fringed** (64 %) → ClearType is live
- all reading windows set `UseLayoutRounding=True`, `SnapsToDevicePixels=True`, `TextFormattingMode=Display`;
  `AllowsTransparency` only on the decorative `NotificationPreviewWindow`

## Hotkey (blocker found this run)
- Alt+V did not open the popup on 3 consecutive fresh launches; the startup toast read
  **"Could not register Alt+V. Another app may own it."** (`startup-toast.png`)
- CopyQ is installed and running on this machine (3 processes; its window title mirrors the clipboard) — the prime
  suspect for holding Alt+V, **not proven**: pressing Alt+V produced no visible CopyQ window and no foreground change
- Clip drops BOTH bindings when either fails (`App.xaml.cs:235` `if (popup.Registered && plain.Registered)`) — so
  Ctrl+Shift+V is dead too while Alt+V is taken; the only signal is a 3 s toast at startup
- the tray path works: Win+B → Enter → "Clip: clipboard history" → Enter opens the popup

## Source-truth spot checks
- `Clip.csproj` PackageReference: Microsoft.Data.Sqlite 11.0.0, H.NotifyIcon.Wpf 2.4.1, RtfPipe 2.0.7677.4303 — **no
  HtmlRenderer.WPF, no System.Text.Encoding.CodePages** (CHECKLIST-features §13 row lists both as shipped)
- no `HtmlRenderer` reference anywhere in `*.cs` (rich rows render through `RichTextBox` + `Core\HtmlToRtf.cs`)
- `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` IS called (App.xaml.cs:46) and works from the
  WindowsDesktop shared framework without the package
- no hard-coded `#RRGGBB` outside `UI\Themes\*.xaml` (not even the accepted `#FF6B6B` exception remains)
