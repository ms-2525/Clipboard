# Clip v2 - release record (25/11/2026)

**Artifact:** `dist\Clip.exe` - 208,187,846 bytes, self-contained single file (no .NET install needed).
SHA-256 `213AC8F4C67397068977DBDBE1A7932AAEC782C041B994BD9F08443B15BF1744`.
The web UI is embedded in the binary (page SHA-256 `D17D65350A9780A1946BF9B72662C5234940FA6A1ECCB983B4AF1241DD1580F0`);
nothing needs to sit beside the exe.

**Branch:** `v2-phase1` in the `v2-stack` worktree, HEAD `9b78976`, 119 commits.
The owner's live app on `master` was not touched; `--legacy` still opens the old WPF UI.
The exe above was published at 08:08 from the S20 page (`Filter by tag` x2, `This clip has no tags`
found inside the binary).

## What is in it

- The whole tested clipboard engine (`Core\` + `Interop\`) unchanged: CF_HTML, RTF, CF_HDROP, CF_DIB/DIBV5,
  named formats, dedupe, cap, password-manager exclusions, SendInput paste.
- A React 19 + TipTap UI in one warm WebView2: rows with index, up to 10 lines, provenance and automatic
  tags; virtualised list (8-20 rows in the DOM for 1,000 clips); light/dark; 430-900 px; zoom 80-150 %.
- Keyboard first: the window lands focus on the selected row, Up/Down move, Left/Right change tab
  (wrapping), typing starts a search, Esc clears it and a second Esc hides, F2 opens and closes the
  editor (a discard prompt only when the body changed); the Settings icon is always in the top bar.
- Tag search (`image:`, `excel:`, `code:`, `-tag:`, phrases, unknown tags fall back to text), full-body
  search including text past the 1,200-character preview.
- Tabs: create, rename, delete (warns "its N clips will be deleted, and this cannot be undone"), move a
  clip by context menu, Ctrl+M picker or Ctrl+Shift+Left/Right; Ctrl+T filters by a clip's tags.
- Editor: F2 overlay, full TipTap toolbar, CF_HTML round-trip, dirty discard, focus trap.
- Export: Ctrl+E to .txt / .md / .csv / .png. CSV is byte-identical to Excel's own payload (UTF-8 BOM,
  no `sep=`). Excel still reinterprets `007` and `=1+1` on open; the file is correct.
- Offline OCR of captured images via Windows.Media.Ocr; text stored in the clip, searchable, marked
  "Text read from image". English only on this machine; Hindi needs a Windows language pack.
- Native tray, global hotkey, auto-fitting toast, single instance, autostart, corruption recovery with a
  notice.

## Measured

| Metric | Value |
|---|---|
| Warm show to first paint | 3.5 ms median external / 30 ms to paint (debug probe) |
| Release exe at idle | 527.5 MB working set / 270.5 MB private, 7 processes |
| OCR accuracy, real screenshot | 93.7 % of characters |
| Full-body search | 9.3-12.4 ms p50 at 1,000 clips |
| Migration of the real 38-row DB | lossless, identical row and blob digests |
| Build | 0 warnings, 0 errors; `tsc --noEmit` clean |

## Verification state

- **UI/UX checklist: 50 PASS, 0 FAIL, 4 NOT RUN.** Three fresh visual passes signed the release UI; the
  last one signed the chrome bands (G-02) on the exact embedded page. The four open rows are native
  toast/tray (G-06), 125 % zoom (W-02), the 430 toast/export menu (W-05) and a real IME (K-07).
- **Feature checklist: 8 PASS, 5 FAIL, 19 NOT RUN** - the open rows are live checks (Office round-trip,
  cold start, 1,000-item acceptance, IME) plus five old-app product items listed under limits.
- **No P0 in any audit.** The data layer was audited three times (DB logic, tags/search, host actions)
  and re-swept on the release build.
- **Keyboard contract has a standing gate**: `cd UI\web; npm.cmd run e2e` -> 9/9 Playwright tests on the
  built page in ~6 s, using the Chromium already on the machine (Playwright 1.63.0, no download).

## Known limits (named, not hidden)

1. Live acceptance is not done: paste into Office, IME, 125 % zoom, tray menu and the native toast need a
   desk with input. Press Esc on the stuck overlay first.
2. Memory is 527.5 MB idle on working set (inside the 500-600 allowance), higher with images loaded; the
   ~643 MB figure with an image is from an earlier build and was not re-measured.
3. OCR cannot be trusted for amounts: neither engine reads the rupee sign, and Indian digit grouping is
   lost. It is for finding a screenshot, never for a figure.
4. `CF_DIBV5` is restored as a custom clipboard format, not under its own name - a Win32 limit shared with
   the old app.
5. Picker items show the raw token (`link:`) rather than the chip label (`Link`); settings leaves empty
   space in the lower right at 900; the selection rail is what distinguishes selection from hover.
6. Eleven files exceed the 400-line budget (`App.tsx` 876, `styles.css` 1825, `ClipboardStore.Items.cs`
   497 among them). Split after release.
7. The old WPF UI remains reachable with `--legacy` for one release.

## How to publish

From this worktree: `build-release.cmd` (rebuilds the web bundle when stale, verifies the embedded page,
publishes to `dist\`, then starts it). To adopt it in the main repo, merge `v2-phase1` into `master`
there and run the same script.
