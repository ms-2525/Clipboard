# Clip v2 - release record (25/11/2026)

**Artifact:** `dist\Clip.exe` - 208,196,038 bytes, self-contained single file (no .NET install needed).
SHA-256 `221D045B05D1199DBD4160A07E6ECD3D0C007C0598326FB9D7229B1B67470548`.
The web UI is embedded in the binary (five unique 48-character sequences from the built page were found
inside it, plus the newest strings `Known limits`, `previewNotification` and `No images yet`); nothing
needs to sit beside the exe.

**Branch:** `v2-phase1` in the `v2-stack` worktree, HEAD `fe40b9a` at publish (208,196,038 B above).
The owner's live app on `master` was not touched; `--legacy` still opens the old WPF UI.

## What is in it

- The whole tested clipboard engine (`Core\` + `Interop\`) unchanged: CF_HTML, RTF, CF_HDROP, CF_DIB/DIBV5,
  named formats, dedupe, cap, password-manager exclusions, SendInput paste.
- A React 19 + TipTap UI in one warm WebView2: rows with index, up to 10 lines, provenance and automatic
  tags; virtualised list (8-20 rows in the DOM for 1,000 clips); light/dark; 430-900 px; zoom 80-150 %.
- Keyboard first: the window lands focus on the selected row, Up/Down move and wrap, Left/Right change
  tab (wrapping), typing starts a search, Esc clears it and a second Esc hides, F2 opens and closes the
  editor (a discard prompt only when the body changed), Ctrl+1-Ctrl+9 paste the visible row and a bare
  digit is search text; the Settings icon is always in the top bar.
- Tag search (`image:`, `excel:`, `code:`, `-tag:`, phrases, unknown tags fall back to text), the
  explicit **`tag:`** prefix (`tag:image`, `tag:excel`, `-tag:files`), full-body search including text
  past the 1,200-character preview, and a built-in **Images** tab (a live `image:` view, never a folder).
- Tabs: create, rename, delete (warns "its N clips will be deleted, and this cannot be undone"), move a
  clip by context menu, Ctrl+M picker or Ctrl+Shift+Left/Right; Ctrl+T filters by a clip's tags.
- Editor: F2 overlay, full TipTap toolbar, CF_HTML round-trip, dirty discard, focus trap.
- Export: Ctrl+E to .txt / .md / .csv / .png. CSV is byte-identical to Excel's own payload (UTF-8 BOM,
  no `sep=`). Excel still reinterprets `007` and `=1+1` on open; the file is correct.
- Offline OCR of captured images via Windows.Media.Ocr; text stored in the clip, searchable, marked
  "Text read from image". English only on this machine; Hindi OCR was explicitly skipped for this
  release (owner decision) and needs a Windows language pack if it is ever wanted.
- Native tray, global hotkey, auto-fitting toast (**width and height both follow content**, capped at the
  configured maximum), single instance, autostart, corruption recovery with a notice, a first-run hint
  naming the hotkey, a real-toast **Preview** in Settings and a **Known limits** note on the same page.
- A dropped over-budget clipboard format is named in a toast instead of vanishing; image decode,
  thumbnail, DIB re-encode and SHA-256 run on a worker thread, so a 5 MB screenshot cannot freeze an open
  popup.

## Measured

| Metric | Value |
|---|---|
| Warm show, keypress → window visible (external, 150 % DPI) | **97.9 ms median** / 110.7 p90 / 81.4 min (19 samples) |
| Cold first `Alt+V` (0.4 s after launch) | **160.8 ms**; process start → visible 688 ms |
| Internal show path (in-app probe) | 3.5 ms median (the OS hotkey + window animation dominate the numbers above) |
| First-ever launch after an update | 10.8 s inside `Process.Start` (single-file extraction / AV), then 11-15 ms |
| Release exe at idle | 527.5 MB working set / 270.5 MB private, 7 processes |
| OCR accuracy, real screenshot | 93.7 % of characters |
| Full-body search | 9.3-12.4 ms p50 at 1,000 clips |
| Migration of the real 38-row DB | lossless, identical row and blob digests |
| Build | 0 warnings, 0 errors; `tsc --noEmit` clean; `npm run e2e` 15/15 in 11.4 s; `tag:` grammar 11/11 |

## Verification state

- **UI/UX checklist: 50 PASS, 0 FAIL, 4 NOT RUN.** Three fresh visual passes signed the release UI; the
  last one signed the chrome bands (G-02) on the exact embedded page. The four open rows are native
  toast/tray (G-06), 125 % zoom (W-02), the 430 toast/export menu (W-05) and a real IME (K-07).
- **Feature checklist: 8 PASS, 5 FAIL, 19 NOT RUN** - the open rows are live checks (Office round-trip,
  cold start, 1,000-item acceptance, IME) plus five old-app product items listed under limits.
- **No P0 in any audit.** The data layer was audited three times (DB logic, tags/search, host actions)
  and re-swept on the release build.
- **Keyboard contract has a standing gate**: `cd UI\web; npm.cmd run e2e` -> 15/15 Playwright tests on the
  built page in ~11 s, using the Chromium already on the machine (Playwright 1.63.0, no download). It
  covers arrows/wrap, tab cycle with Images, typing/Esc, `Ctrl+1..9` vs bare digits, F2 clean/dirty,
  discard-never-saves, Settings by keyboard, live All count, search-Enter, and zero network/console noise.

## Known limits (named, not hidden)

1. Live acceptance is done on this machine except two items: Office round-trip (both COM servers fail
   with `TYPE_E_CANTLOADLIBRARY` on this install) and a Hindi IME (none is enabled). Cold first `Alt+V`
   is 160.8 ms, 11 ms over the old 150 ms budget; warm is 97.9 ms median. The owner must accept the
   cold number or set a new budget.
2. Memory is 527.5 MB working set / 270.5 MB private at idle, measured on the 06:52 exe; the final
   221D build was not re-measured, and the ~643 MB figure with an image loaded is from an earlier build.
3. Paste lands nothing if the target app's editor does not hold internal focus (seen when Notepad's tab
   bar had focus): the guard checks the foreground window, not the focused child. Click into the
   document before pasting, or paste again.
4. OCR cannot be trusted for amounts: neither engine reads the rupee sign, and Indian digit grouping is
   lost. It is for finding a screenshot, never for a figure.
5. `CF_DIBV5` is restored as a custom clipboard format, not under its own name - a Win32 limit shared with
   the old app.
6. Picker items show the raw token (`link:`) rather than the chip label (`Link`); settings leaves empty
   space in the lower right at 900; the selection rail is what distinguishes selection from hover.
7. Deleting a tab permanently deletes its clips after a confirm; there is no tab-level undo.
8. Eleven files exceed the 400-line budget (`App.tsx` 876, `styles.css` 1825, `ClipboardStore.Items.cs`
   497 among them). Split after release.
9. The old WPF UI remains reachable with `--legacy` for one release.

## How to publish

From this worktree: `build-release.cmd` (rebuilds the web bundle when stale, verifies the embedded page,
publishes to `dist\`, then starts it). To adopt it in the main repo, merge `v2-phase1` into `master`
there and run the same script.
