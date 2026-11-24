# CURRENT_STATE.md — Clip

LANE: release (25 Nov 2026, branch `v2-phase1`)

## What shipped

The published `dist\Clip.exe` **is** the v2 app. A plain double-click starts the tray app and the
warm WebView2 shell; `--legacy` still opens the old WPF UI for this release only; `--v2` is kept as
an alias of the default. The React page is embedded in the exe (`clip.assets.web/index.html`) and
served from memory over `https://clip.local/`, so the exe needs no folder beside it. A developer
build running from `bin\` still prefers `UI\web\dist` on disk for fast iteration.

- One tray icon; Alt+V opens the list; capture, rich rows, tags, search, export, the real editor,
  settings, pin/delete/undo/move and both paste paths are live.
- OCR runs offline on captured images and stores the words with the clip.
- Single instance, autostart target and the tray menu behave as before the flip. The WebView2
  profile now lives next to the database (`%APPDATA%\Clip\webview`), not in `%TEMP%`.

**Artifact:** `dist\Clip.exe` — 208,159,174 bytes (~198.6 MiB), self-contained win-x64 single file,
no .NET runtime needed. Embedded page: `clip.assets.web/index.html` (2,840,139 bytes inlined HTML).

## Measured numbers

| Number | Value | How |
|---|---|---|
| Warm show (S8a, debug probe) | 17.2 ms median | `CLIP_V2_TIMING`, Show→first rAF frame |
| Warm show (published exe) | re-check pending | the clean-profile desktop was covered by an elevated screenshot overlay at release time |
| OCR accuracy | 93.7 % of characters | clean screenshot, Windows.Media.Ocr |
| Idle memory, clean profile | ~528 MB (160 MB `Clip.exe` + 368 MB across 6 WebView2 processes) | fresh DB, 0 clips, shell hidden |
| With 5 clips incl. one image | ~643 MB (224 MB + 419 MB across 6) | after capture + OCR, shell hidden |
| Cold first window after login | a few seconds | WebView2 process + page start |

## Known limits (honest list)

- **OCR is never trusted for amounts.** `₹` is unreadable, Indian grouping is lost; verify money
  against the image.
- **CSV opens correctly but Excel reinterprets values**: `007` loses zeros, `=1+1` becomes a
  formula. Use Data → From Text/CSV with Text columns.
- **WebView2 memory is Chromium-heavy**: ~640 MB resident for a warm shell on this VM. Idle is not
  the old app's ~250 MB; it is the price of the web surface.
- **WebView2 runtime is required.** It ships with Windows 10/11; a machine without it gets the
  shell's "runtime not available" notice instead of the page.
- `--legacy` keeps the old WPF UI for this release only; it will be removed.
- Pinning an old clip still puts it at the bottom of Pinned; undo after a move returns to the old
  tab only while the tab exists.
- The plain-text hotkey is fixed at Ctrl+Shift+V (the popup hotkey is rebindable); the tray menu
  shows the running version.
- Editor cold open remains ~2.9 s / +302 MB while open (TipTap + WebView2); this is unchanged.

## Where the truth lives

- Acceptance: `docs\CHECKLIST-features.md`, `docs\CHECKLIST-uiux.md`.
- Defects and open debt: `docs\BUILD-TRACKER.md`, `docs\AUDIT-*.md`.
- The user-facing contract: `USER_GUIDE.md`.
