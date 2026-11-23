# S2 — Images tab evidence (measured, 23 Nov 2026 ~18:40–19:10)

Build: `bin\Release\net10.0-windows\Clip.exe`, `dotnet build -c Release` 0 errors / 0 warnings,
`MainWindow.Images.cs` 19:06. Machine: 1920×1200 physical @ **150 % DPI** (1280×800 DIP), one monitor.
Every rectangle below comes from a DPI-aware (`SetProcessDpiAwarenessContext(-4)`) screen capture in this
folder, measured by pixel scan (physical px ÷ 1.5 = DIP).

## Geometry (target from DESIGN-v2-ui §3.2 in brackets)

| Element | Measured |
|---|---|
| Popup | 550 × 520 DIP (825 × 780 px) — unchanged |
| Preview pane | **538 × 263 DIP** (807 × 394.5 px; 803 px wide at 10 px below the top edge, i.e. the 12 DIP corner radius) [538 × 263] |
| Preview image, tall 360 × 1280 card | **69.3 × 246.7 DIP** — fitted to the 247 DIP box height, letterboxed, **never cropped** [fit, not crop] |
| Preview image, 1280 × 800 card | **522 × 326 DIP** — width-capped, letterboxed (`s2-right1.png`) |
| Tile | **104 × 104 DIP** (156 px), 8 DIP gap, 4 + a peek of the 5th at 550 px (`s2-images-dark.png`) |
| Filmstrip | bottom-aligned, 112 DIP high, 8 DIP below the pane, 6 DIP bottom margin |
| Counter | bottom-right of the pane, `n / N`, follows the selection (`1 / 13` … `13 / 13`) |

## Selection moves the preview only (zero layout change)

Pixel diff of the whole popup area between two `←`/`→` steps (`s2-images-dark.png` → `s2-right1.png` →
`s2-right2.png`): every changed pixel lies inside **y 277–827 px** — the preview image box and the strip.
No pixel changed in the header, tab strip, pane frame, or status bar, i.e. no row, no pane and no strip
moved. Clipboard tab regression (`s2-clip-b.png`): `↓↓` moved the ring two rows and no row's y moved.

## Timings (live app, 6–8 ms screen probe around the keypress)

| Event after `→` | Observed |
|---|---|
| Stored 96 px thumb painted | **53 ms** |
| 1024 px decode painted | **93 ms** |

Decode itself (harness calling the shipped `MainWindow.DecodeImage`, background thread, frozen result):

| Input | Time | Result |
|---|---|---|
| 1920 × 1080 PNG (typical screenshot) | **39 ms** | 1024 × 576, 2.25 MB, `IsFrozen=True` |
| 360 × 1280 PNG (tall) | 4 ms | 288 × 1024, 1.12 MB — larger side capped, no full-height decode |
| 300 × 200 PNG (small) | 0 ms | 300 × 200 — never upscaled |
| corrupt 14-byte blob | throws `NotSupportedException` | the caller turns it into the visible placeholder |

## Decode-failure placeholders (`s2-tile-harness.png`, real template + real `ClipRow`)

A tile whose stored thumb is garbage or missing renders the drawn picture icon + `No preview` on the
`Field` token — never a blank grey box. Both tiles measured 104 × 104 DIP. The live app showed the same
tile state after an undo that (pre-fix) restored an image without its thumb.

## RAM (working set, `Get-Process`)

| Moment | WS |
|---|---|
| Fresh launch, idle | 114.8 MB |
| Popup open, Clipboard tab | 162.0 MB |
| Images tab first open (+ preview + 4 tiles) | **174.1 MB** (+12.1) |
| 8 × `→` browse, peak | 218.2 MB |
| … settled 4 s later | 217.0 MB |
| Long session after the F2 editor had been opened (by a parallel session) | 272–285 MB (Images delta still ≈12 MB) |

One dominant bitmap is alive (≤ 2.25 MB measured, ≤ 4 MB worst case at 1024²); it is dropped on swap,
tab change and popup hide.

## Keyboard (Images tab)

`←`/`→` one image · `↓↓` one strip page (1 → 13/13, clamped) · `↑` back · `Home` 1/13 · `End` 13/13 ·
`Enter` pasted the shown image into Paint (`1280 × 800px` status) · `Ctrl+3` pasted the 3rd visible tile
(`900 × 700px`) · `F2` opened Photos on `clip-137.png` (360 × 1280) · `Delete` 13 → 12 · `Ctrl+Z` 13 and the
thumb survived (DB check: `missing_thumb` stayed 1) · wheel over the strip scrolls it horizontally ·
`Ctrl+Tab` / `Ctrl+Shift+Tab` still switch tabs · Esc cascade and type-to-search unchanged on the
Clipboard tab (`s2-clip-search.png`, `s2-clip-esc1.png`, `s2-clip-esc2.png`).

## Side effects, disclosed

- 13 image clips were copied through the real clipboard path (3 screenshots/cards drawn for this walk plus
  10 `CARD n` fixtures). No SQL was used to seed or edit the database.
- `config.json` was set to `"theme": "light"` for `s2-images-light.png` and restored to `"dark"` from a
  backup; the app was relaunched after each change.
- Image `id=69` (a 127-byte grey PNG from an earlier audit) lost its `thumb` row when a **pre-fix** build
  performed delete + Ctrl+Z during this walk (`Restore` did not carry the thumb). The fix landed
  afterwards and a re-run on the first image kept its thumb; `id=69`'s tile reads `No preview` while its
  big preview still renders.
- A parallel session was typing into the shared desktop during the walk; twice its keys landed in the
  popup (one opened the F2 editor, which was closed with **Discard**, and one switched tabs). Nothing was
  saved from the editor.
