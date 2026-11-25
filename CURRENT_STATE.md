# CURRENT_STATE.md — Clip

LANE: bug

## Working tree (25 Nov 2026, uncommitted, needs owner rebuild)

Done since the last pin: Esc overlay guards on all four Radix dialogs + both menus
(`stopPropagation`-only, Radix still closes); empty-text export refused
(`This clip has no text to save.`); real `.md` export (rich HTML converts, `.txt`
byte-identical); `<pre>` hang fixed (fall through to `Tag`/`End`). Proven: `dotnet`
0/0, scratch fixtures 7/7 + 10/10 + 5/5 on the real files, page e2e 20/20 + 3/3 + 23/23
via agents. Owner decisions in `CHECKLIST-uiux.md` §14: tray mouse-only, keep 430
counts, remeasure memory on the new exe.

Not done: commit the tree — another session's edits are interleaved in it (`App.V2`,
`ClipSearch`/`ClipTags`, `EditorToolbar`, `mock.ts`, ClearHistory, Popups theme), so
stage named files and coordinate; rebuild `dist\Clip.exe`; live pass on the new exe
(cold `Alt+V`, Esc overlays, Office matrix, IME, reboot); fresh visual read (G-02,
Export label, W-05); memory remeasure.

## What shipped

`dist\Clip.exe` is the app: one self-contained win-x64 file, no .NET runtime needed, with the
React page embedded and served from memory. A plain double-click starts the tray app; the old WPF
window and its `--legacy` flag were deleted on 25 Nov 2026.

Live in the shipped UI: tray and hotkey, capture of text / rich text / links / files / images,
automatic tags, full-body search with tag filters, tabs (create, rename, delete, move), export to
.txt / .md / .csv / .png, the F2 editor, offline OCR, settings, pin / delete / undo, both paste
paths, autostart and single-instance behaviour.

**Artifact:** `dist\Clip.exe` — 208,183,750 bytes (~198.5 MiB). The page is embedded; no folder is
needed beside the exe.

## Measured numbers

| Number | Value | How |
|---|---|---|
| Warm show, window appears | 3.5 ms | measured from another process |
| Warm show, first paint | 30 ms | same run, time to painted frame |
| Search at 1,000 clips | 12.4 ms p50 | full stored body, not the preview |
| OCR accuracy | 93.7 % of characters | real screenshot, Windows.Media.Ocr |
| Idle memory | 528 MB working set | shell hidden, clean profile |
| Image + OCR memory | ~643 MB working set | private bytes are lower than working set |

Memory is the WebView2 price: six Chromium processes plus the page. It is stated here, not hidden.

## Known limits (honest list)

- **OCR is never trusted for amounts.** `₹` is unreadable, Indian grouping is lost, some amount
  lines are dropped; verify money against the picture.
- **Hindi OCR needs the Hindi language pack** installed in Windows. Without it those images get no
  text at all rather than garbage.
- **`CF_DIBV5` restores as a custom clipboard format**, not under its own name (a Win32 limit, same
  as the old app).
- **Excel reinterprets values on open**: `007` becomes `7` and `=1+1` becomes `2`. The exported CSV
  itself is correct; open it through Data → From Text/CSV with Text columns when it matters.
- **No time-section jump yet** (`Ctrl+Up/Down`); the list scrolls and searches, but has no date
  sections.
- **WebView2 runtime is required.** It ships with Windows 10/11; without it the window shows a
  "runtime not available" notice instead of the page.

## How to rebuild

Double-click `build-release.cmd`: it rebuilds the page when stale, publishes one self-contained
`dist\Clip.exe`, and starts it.

## Where the truth lives

- Acceptance: `docs\CHECKLIST-features.md`, `docs\CHECKLIST-uiux.md`.
- Audits and open debt: `docs\BUILD-TRACKER.md`, `docs\AUDIT-*.md`.
- The user-facing contract: `USER_GUIDE.md`.
