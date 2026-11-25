# Build tracker - Clip v2 release (night of 25/11/2026)

**Owner instruction:** continue building autonomously, sub-agents only, thorough DB-logic and UI/UX
audits after every major change, release-ready within 8-10 hours.
**Definition of release-ready:** one self-contained `dist\Clip.exe` with tray, hotkey, capture and paste
on the new UI; both checklists pass with evidence; a fresh agent signs the visuals; a dual-axis audit is
clean; memory stated honestly; docs updated.

> This file is ASCII only, on purpose. Three encoding accidents tonight (a BOM, mixed CRLF, and a
> CP1252 round-trip) all came from PowerShell rewriting it. Keep it plain.

## Rules for every slice

1. One writer per file set. Research and audit agents are read-only.
2. Build gate: `dotnet build -c Release` 0 errors / 0 warnings; `tsc --noEmit` clean for web changes.
3. Evidence, not claims: a harness for logic, a screenshot the agent read for visuals, measured numbers
   for performance. Builders never sign their own UI.
4. Never touch the live database. Test on `%TEMP%\opencode\phase1-db\clip.db` (a copy).
5. `Core\` + `Interop\` behaviour stays; new rules live in `Core\` as pure, testable code.
6. Commit per slice on `v2-phase1`; the audit runs against that commit.
7. After each slice, a separate agent audits it (DB/logic + UI/UX) into `docs\AUDIT-*.md`.
8. Build lock: while an app runs from `bin\Release\net10.0-windows\`, build with
   `-o %TEMP%\opencode\build-<slice>`; a lock is not a code error.
9. Commit the tracker with `git commit docs/BUILD-TRACKER.md -m ...` so another agent's staged files
   are never swept in.

## Slice board

| # | Slice | Files | Status | Evidence |
|---|---|---|---|---|
| S1 | `ClipTags` classifier + `tags[]` DTO | `Core\ClipTags.cs`, `Host\` | DONE `1b57ecd`, failed audit, fixed by S3f | 27/27, then prose false positives |
| S2 | Visual audit + contrast tokens | `UI\web\src\styles.css` | DONE `0d0b666` | 16/20; 62+62 pairs, 0 failures |
| R1 | OCR research | scratch | DONE | Windows.Media.Ocr, +25.4 MB, no schema change |
| R2 | Export and tag-query research | scratch | DONE | CSV byte-identical, BOM yes, `sep=` never |
| S3a | Search parser + tag filter + `search` | `Core\ClipSearch.cs`, `Host\` | DONE `ffc97aa` | 15/15 grammar; 3-30 ms at 1k/2k |
| S3b | Row contract, tags, real Inter, focus=selection | `UI\web\src\*` | DONE `2131962` | 65->245 px rows, index 01, 5 widths 0 overflow |
| S3c | P0 truncation guard, corrupt-DB recovery, one search rule | `Core\`, `Host\`, `UI\ClipRow.cs` | DONE `5a7a285` | 600-emoji fixture; preview save refused |
| S3d | Bridge off the UI thread, tab restore, `_meta` | `Host\`, `Core\ClipboardStore.Items.cs` | DONE `552e648` | UI gap 192 -> 41.5 ms |
| S3f | Kill tag false positives, truncation rule, one search rule | `Core\ClipTags.cs`, `SearchService`, `ClipSearch` | DONE `d9849ed`, `09e1161` | 61/61 + 10/10; prose clean |
| S3g | Monotonic sort key; recovery race | `Core\ClipboardStore*.cs` | DONE `8dcdff0` | 45/45; A,B -> copy A -> A,B after reload |
| S4a | Keyboard matrix, host search, live capture | `UI\web\src\*` | DONE | full key table; recapture reorders, no jump |
| S4b | Paste, delete/restore, pin, move, hide | `Host\`, `UI\ShellWindow.xaml.cs` | DONE `99339dc` | Notepad got all 4 clip kinds |
| S5 | Rich rows, sanitizer, Word normalizer, files | `UI\web\src\*` | DONE `5ee4ac8` | 12/12 attacks neutralised, 0 requests |
| S6a | Export core + Save dialog | `Core\ClipExport.cs`, `Host\ClipHost.Export.cs` | DONE `8cda936` | CSV/PNG byte-identical; 24 names |
| S6b | Export menu + OCR row in the UI | `UI\web\src\*` | DONE `ff11f84` | real file 50 bytes exact; OCR marker |
| S7 | Settings wired + editor overlay | `UI\web\src\*`, `Host\ClipHost.Settings.cs` | DONE `9ad5134` | theme save reaches page+config; CF_HTML valid |
| S8a | `--v2` as a real tray app | `App.V2.cs`, `UI\ShellWindow*` | DONE `5d56b53` | warm show 17.2 ms median; 6 live captures |
| S8b | Toast auto-fit, Ctrl+Shift+V, Clear history, elevation guard | `App.V2.cs`, `NotificationWindow*`, `Host\` | DONE `7a65194` | toast 77->160 px; elevated paste refused |
| S8c | Copy self-capture, theme polarity, silent paste failure, OCR overwrite | `Host\`, `App.xaml.cs`, `Core\` | DONE `4f99e5c` | copy -> 0 captured; failed write -> 0 chords |
| S9a | OCR: TFM retarget, service, storage | `Clip.csproj`, `Core\` | DONE `a3dee2b` | 93.7 % chars on clip 356; search finds it |
| S10 | Packaging: embed the web build, flip the default launch | `Clip.csproj`, `build-release.cmd`, `App.xaml.cs` | DONE `e813d53` | 198.6 MiB exe; clean-profile proof |
| S11 | UI audit fixes: search keys, modal, stale body, SVG, caches | `UI\web\src\*` | DONE `aba6c71` | 30/30 tabs trapped; edit refreshes the row |
| S12 | Export hide, full-body search, corruption notice | `Host\`, `UI\ShellWindow.xaml.cs` | DONE `ec4ce55` | save and cancel keep the shell visible; word at char 3,001 found in 12.4 ms |
| S13 | Tab management: create, rename, delete, move | `Host\`, `UI\web\src\*` | DONE `4954e87`, `1ced540` | 50/50 bridge; strip CRUD, Ctrl+M picker, delete warns with N clips |
| S16 | Final release audit + visual re-check + docs refresh | read-only / docs | Audit DONE (ship with named limits, no P0); docs DONE `bae76ad`; visual re-check running | 4 fixes closed; 3 PASS / 7 FAIL / 26 NOT RUN on features |
| S17 | Close named limits: toolbar glyphs, char count, section jump, tab-move keys, delete wording | `UI\web\src\*` | IN PROGRESS | 4 new key combos; every toolbar button an SVG |
| S18 | 430 px and dialog polish from the visual re-check | `UI\web\src\*` | QUEUED (after S17) | chip units readable, tab + reachable, no sliced labels, visible initial focus in all dialogs |
| S14 | Rewrite `CHECKLIST-uiux.md` for the v2 surface | `docs\` | DONE `02e23cd` | 54 rows: 32 PASS, 14 FAIL, 8 NOT RUN |
| S15 | Six visual release defects | `UI\web\src\*` | DONE `50207bd` | Settings 860x657 fits 900x700 with zero scroll; hover 1.17/1.20; index 4ch; 20/20 tab trap |

## Release audit verdict (25 Nov, `docs\AUDIT-release-logic.md`)

**Measured on the rebuilt release exe (`208,183,750` bytes, fresh profile, idle 10 s): 7 processes,
527.5 MB working set / 270.5 MB private; host 153.3 MB, WebView2 374.1 MB.** Inside the 600 MB ceiling
at idle on working set, and well inside it on private bytes. The ~643 MB figure with an image and OCR
loaded is from the earlier build and has not been re-measured.

**Not release-ready yet; data layer is clean.** No P0: 1,000-row cap eviction exact, migration of the
real 38-row DB lossless (identical digests), truncation guard holds, OCR cannot overwrite an edit,
export bytes exact, and all seven one-home invariants hold. Open: export-hide (S12), full-body search
(S12), tab management missing (S13), stale uiux checklist (S14), settings layout/hover/focus (S15),
memory 528 MB idle / 643 MB with clips on working set, and every `[U]` row NOT RUN because an elevated
overlay blocked UI automation.

## Open defects and debt

- Memory: 528 MB WS idle / ~643 MB with an image and OCR (private bytes lower). Above the 600 ceiling
  when a picture is loaded. Cause: six WebView2 processes plus a 2.8 MB page. Options if the owner
  objects: lazy-mount the editor, load row bodies on demand (already), or accept it.
- `CF_DIBV5` is restored as a custom format, not under its own clipboard name (Win32 limit, same as the
  old app).
- `Core\ClipboardStore.Items.cs` and `Host\ClipHost.Actions.cs` are past 400 lines; `App.tsx` 876 and
  `styles.css` 1825. Split after release.
- Old WPF UI stays reachable via `--legacy` for this release; delete it in the next one.
- `mock.ts` still carries a dev-only second search/tag rule; it never ships to WebView2.
- UX gaps vs the old app: no time-section jump (Ctrl+Up/Down), no tab UI until S13, no first-run tip.

## Night log

- 01:40 S1/S2/R1/R2 dispatched. 02:30 S3c found the emoji data-loss bug. 03:05 S3g fixed re-copy
  ordering. 03:35 S4a keyboard matrix. 04:00 S8a v2 is a tray app (17 ms show). 04:20 S9a OCR live.
  04:40 S8c closed four host blockers. 05:20 S10 packaged the release exe. 05:45 S11 fixed the UI
  audit P1s. 06:10 release audit: data clean, six product gaps named.
