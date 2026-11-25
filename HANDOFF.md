# HANDOFF - Clip v2 release (written 25/11/2026, before compacting the build chat)

Read this top to bottom before touching anything. It says where the work lives, what is proven, what
is not, and exactly what is left.

> **Update, 25/11 10:10 - read these first, this file is a snapshot from earlier.**
> The live acceptance round 2 found that the WPF shell owned Escape (it hid the window before the page
> could clear the search, dismiss the editor or close Settings). That is fixed, along with live counts,
> the discard path and a set of product gaps. The current truth lives in
> `docs\SPEC-owner-requirements.md` (requirement matrix), `docs\AUDIT-live-acceptance-3.md` (fresh live
> pass on the final exe), `docs\AUDIT-visual-3.md` (zoom 100/125/150 and 430 width) and
> `docs\RELEASE-v2.md` (artifact and limits). The artifact is now **208,196,038 B, SHA-256
> `221D045B05D1199DBD4160A07E6ECD3D0C007C0598326FB9D7229B1B67470548`** (plus the Settings-layout
> rebuild), not the 208,187,846 B / `213AC8F4...` named below. New this round: Images tab, `tag:`
> prefix, first-run hint, real-toast Preview in Settings, Known-limits note, over-budget format toast,
> image decode/hash off the UI thread, toast auto-fits width too, `Ctrl+1..9` paste, wrapping arrows.

---

## 1. Where everything lives

| Thing | Path |
|---|---|
| Release worktree | `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack` |
| Branch / HEAD | `v2-phase1` / `9b78976` (+ commits made after this file was written) |
| Live repo (untouched) | `C:\Users\mayan\CascadeProjects\Clipboard` - `master`, still the old WPF UI |
| Release artifact | `v2-stack\dist\Clip.exe` - 208,187,846 bytes, self-contained, UI embedded |
| Artifact SHA-256 | `213AC8F4C67397068977BDDBE1A7932AAEC782C041B994BD9F08443B15BF1744` |
| Embedded page SHA-256 | `D17D65350A9780A1946BF9B72662C5234940FA6A1ECCB983B4AF1241DD1580F0` |

Docs to read first, in this order:
1. `docs\RELEASE-v2.md` - what shipped, measured numbers, named limits, how to publish.
2. `docs\BUILD-TRACKER.md` - every slice, its commit, and the evidence it produced.
3. `docs\CHECKLIST-features.md` and `docs\CHECKLIST-uiux.md` - the acceptance source of truth.
4. `docs\AUDIT-*.md` - the independent audits (DB, tags/search, host, release x2, visual sign-offs).

## 2. The stack (locked by the owner)

WPF .NET 10 thin host + **one warm WebView2** + React 19 / TypeScript / Vite / Tailwind / Radix /
TanStack Virtual / TipTap 3. The tested C# clipboard engine (`Core\` + `Interop\`) is unchanged behind a
typed JSON bridge (`Host\ClipHost*.cs`).

Tauri 2 was fully spiked (HTML/RTF/files/image round-trips, monitor, paste, tray, hotkey, SQLite all
passed) and is documented as a **fallback only**. The owner chose WPF to keep the engine and ship
faster. `v2-phase1` is the release branch; `master` still runs the old UI.

## 3. What works, proven

- Capture: text, HTML, RTF, file lists, images, named formats; dedupe moves a re-copy to the top;
  cap eviction exact; password-manager exclusions intact.
- Paste-back: all formats into Notepad (4 kinds tested), plain paste drops rich formats, truncated
  bodies paste in full (600-emoji fixture).
- List: index, up to 10 lines auto-height, in-row provenance, proven-only tags, rich HTML preview,
  file rows, image thumbs + OCR text, virtualised (8-20 DOM rows for 1,000 clips).
- Search: plain words, `image:` `excel:` `sheet:` `code:` `files:` `link:`, `-exclude`, phrases,
  unknown tag falls back to text with a hint, **full-body** search past the 1,200-char preview.
- Tabs: create inline, rename, delete (warns "its N clips will be deleted, and this cannot be undone"),
  move via context menu, `Ctrl+M` picker or `Ctrl+Shift+Left/Right`.
- Editor: `F2` overlay, full TipTap toolbar (all SVG icons), CF_HTML round-trip, dirty discard,
  focus trap, save invalidates the row body.
- Export: `Ctrl+E` to .txt / .md / .csv / .png. CSV is byte-identical to Excel's own payload
  (UTF-8 BOM, never `sep=`). Excel still reinterprets `007` and `=1+1` on open; the file is correct.
- OCR: Windows.Media.Ocr offline, text stored in the clip and searchable, marked "Text read from
  image", never pasted as text, never overwrites a user edit.
- Native: tray, global hotkey, `Ctrl+Shift+V` plain paste, auto-fitting toast, single instance,
  autostart, corruption recovery with a user-visible notice, elevation guard on paste.

## 4. Measured numbers

| Metric | Value |
|---|---|
| Warm show (release exe, external measurement) | 3.5 ms median / 4.0 p90 |
| Show -> page paint (debug probe) | 30 ms median / 36.5 p90 |
| Idle memory (release exe, fresh profile) | 527.5 MB WS / 270.5 MB private, 7 processes |
| OCR accuracy, real screenshot | 93.7 % of characters |
| Full-body search | 9.3-12.4 ms p50 at 1,000 clips |
| Migration of the real 38-row DB | lossless, identical row and blob digests |
| Build | 0 warnings, 0 errors; `tsc --noEmit` clean |

## 5. Acceptance state at handoff

- `CHECKLIST-uiux.md`: **49 PASS / 0 FAIL / 5 NOT RUN** (54 rows).
- `CHECKLIST-features.md`: **8 PASS / 5 FAIL / 19 NOT RUN** (32 rows).
- Data layer: **no P0** across three independent audits.
- Visual: two fresh agents signed the release UI (10/10, then G-02/G-04 on the exact embedded page).
- The 5 feature FAILs are old-app items, not v2 regressions: on-screen ceilings text, 5 MB image
  decode off the UI thread, ghost notification preview, first-run hint, skipped-capture notice.
- The NOT RUN rows are live/hardware checks: Office round-trip, IME, 125 % zoom in the shell, native
  toast on screen, tray interaction, 100/200 % DPI, cold start on the release exe.

## 6. What was running when this file was written

1. **Live acceptance agent** - cold start, 20 warm shows, 5 capture kinds -> Notepad paste, Office if
   installed, Hindi IME, 125 % zoom, native toast, tray Exit. Evidence `%TEMP%\opencode\live-accept`,
   report `docs\AUDIT-live-acceptance.md`.
2. **Chrome sign-off agent** - signing G-02 (header / tab strip / footer bands) and settling the
   skipped-capture and first-run rows.

If those reports exist, apply their findings and update the checklists honestly. A row becomes PASS
only with cited evidence; a `[U]` row only from a fresh agent's screenshot.

## 7. What is left, in order

1. Read the two reports above; fix anything they prove broken.
2. If code changes: rebuild the page (`npm.cmd run build` in `UI\web`) and the exe
   (`build-release.cmd`), then re-sign the affected rows with a fresh agent.
3. If all live items pass: merge into the live repo and rebuild the owner's app:
   ```powershell
   cd C:\Users\mayan\CascadeProjects\Clipboard
   git merge v2-phase1
   .\build-release.cmd
   ```
   Then confirm: the exe starts, `Alt+V` shows the new UI (not the fallback page), a paste into
   Notepad works, tray Exit leaves zero `msedgewebview2` children.
4. Delete the watchdog automation `aut_20f28ef3d001ZRc1IxBGtA5FS4` (it fires every 40 minutes and
   points at the old chat session).
5. Give the owner a one-screen close-out: what shipped, the numbers, what remains unproven, the named
   limits.

## 8. Traps that cost time tonight (keep them)

- **Never test against the live database.** Copy `%APPDATA%\Clip\clip.db` (plus `-wal`/`-shm`) to
  `%TEMP%` first.
- **Build lock:** if an app instance holds `bin\Release\net10.0-windows\Clip.exe`, build with
  `-o %TEMP%\opencode\build-<slice>`. That is a file lock, not a code error.
- **One writer per file set.** Research and audit agents are read-only.
- **Commit the tracker path-scoped** (`git commit docs/BUILD-TRACKER.md -m ...`) or another agent's
  staged files are swept into your commit.
- **Keep `docs\BUILD-TRACKER.md` ASCII.** PowerShell rewrote it three times tonight: a UTF-8 BOM, mixed
  CRLF, then a CP1252 round-trip that mangled the arrows.
- **UTF-16 vs SQLite code points:** comparing `Text.Length` to `TextLength` caused silent data loss on
  emoji-heavy clips. The rule is one `IsTruncated` helper on the item.
- **One home per rule:** two classifiers, two theme resolvers and two search rules all drifted within
  hours. `ClipTags`, `App.ResolveTheme`, `SearchService.Matches` and `sort_seq` are the only ones.
- **Excel CSV:** write a UTF-8 BOM and never `sep=`; they are mutually exclusive and `sep=` breaks
  Devanagari.
- **`RecognizeAsync` must be serialized process-wide**; the OCR worker is a single thread.
- **The published exe must embed the page.** A `dist` folder beside the exe does not exist after
  publish; the fallback page trap is recorded in the ledger.
- **Never claim a visual PASS without a fresh agent's screenshot.** Builders sign nothing visual.
- **A mock can lie while the host is right.** `zzzz:` returned everything in the mock but nothing in
  the real host; fix the mock so evidence does not mislead.

## 9. Named limits shipped with the build

1. Live acceptance depends on a real foreground window (Office, IME, zoom, toast, tray).
2. Memory is 527.5 MB idle on working set (inside the 500-600 allowance), higher with images loaded;
   the ~643 MB figure with an image is from an earlier build and was not re-measured.
3. OCR cannot be trusted for amounts: the rupee sign and Indian grouping are not readable.
4. `CF_DIBV5` restores as a custom clipboard format (Win32 limit, same as the old app).
5. Picker items show the raw token (`link:`) rather than the chip label (`Link`); settings leaves some
   blank space in the lower right at 900; the 3 px selection rail is what separates selected from hover.
6. Eleven files exceed the 400-line budget (`App.tsx` 876, `styles.css` 1825, `ClipboardStore.Items.cs`
   497 among them). Split after release.
7. The old WPF UI has been removed (25 Nov 2026); the WebView2 shell is the only UI.
8. Deleting a tab permanently deletes its clips after a confirm; there is no tab-level undo.

---

## Resume prompt (paste into the new chat after compacting)

> Continue the Clip v2 release. Working directory: `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack`, branch `v2-phase1`.
>
> Start by reading `HANDOFF.md` in `C:\Users\mayan\CascadeProjects\Clipboard`, then `docs\RELEASE-v2.md`, `docs\BUILD-TRACKER.md`, both checklists, and the newest `docs\AUDIT-*.md`. Two agents were running at handoff: a live acceptance pass (report `docs\AUDIT-live-acceptance.md`) and a G-02 chrome sign-off. Check whether they finished; apply their findings and update the checklists only with cited evidence.
>
> Then: (1) fix anything the live pass proves broken, rebuild with `build-release.cmd` if code changes, and re-sign affected rows with a fresh visual agent; (2) if all live items pass, merge `v2-phase1` into `master` in `C:\Users\mayan\CascadeProjects\Clipboard`, run `build-release.cmd` there, and confirm the exe starts, Alt+V shows the new UI, paste works, and tray Exit leaves no WebView2 children; (3) delete the watchdog automation `aut_20f28ef3d001ZRc1IxBGtA5FS4`; (4) give the owner a one-screen close-out with what shipped, the numbers, what is still unproven and the named limits.
>
> Rules: never open the owner's live database (copy it); one writer per file set; commit the tracker path-scoped; keep `docs\BUILD-TRACKER.md` ASCII; a builder never signs a visual row; report failures with evidence instead of working around them.
