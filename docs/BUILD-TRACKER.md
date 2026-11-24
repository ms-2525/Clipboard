# Build tracker — Clip v2 release (night of 25/11/2026)

**Owner instruction:** continue building autonomously, sub-agents only, thorough DB-logic and UI/UX
audits after every major change, release-ready within 8–10 hours.
**Definition of release-ready:** one self-contained `dist\Clip.exe` with the tray, hotkey, capture and
paste working on the new UI; both checklists pass with evidence; a fresh agent signs the visuals; a
dual-axis audit (DB logic + UI/UX) is clean; memory inside the ceiling; docs updated.

## Rules for every slice

1. One writer at a time per file set. Research/audit agents are read-only.
2. Build gate: `dotnet build -c Release` = 0 errors, 0 warnings; `tsc --noEmit` clean when the web
   changes; the old WPF UI keeps building until cutover.
3. Evidence, not claims: a harness for logic, a screenshot the agent read for visuals, measured
   numbers for perf. Builders never sign their own UI.
4. Never touch the live database. Test against `%TEMP%\opencode\phase1-db\clip.db` (a copy).
5. `Core\` + `Interop\` behaviour stays; new rules go in `Core\` as pure, testable code.
6. Commit at the end of each slice on `v2-phase1`; audit runs against that commit.
7. After each slice, a separate agent audits it (DB/logic + UI/UX) → `docs\AUDIT-slice-<n>.md`.
   Findings are fixed before the next slice starts, unless noted as accepted debt.
8. **Build lock:** while any agent has the app running from `bin\Release\net10.0-windows\`, the default
   build fails at the final copy (the exe is held). Build with `-o %TEMP%\opencode\build-<slice>` in
   that case; a lock is not a code error. Close the app before the slice's final gate build.

## Slice board

| # | Slice | Files (owner) | Status | Evidence required |
|---|---|---|---|---|
| S1 | `ClipTags` classifier + `tags[]` DTO | `Core\ClipTags.cs`, `Host\ClipHost.Queries.cs` | **DONE `1b57ecd`, then FAILED audit → S3f** | 27/27 fixtures, but prose tagged as TS/Python |
| S3f | Kill tag false positives, truncation rule, one search rule | `Core\ClipTags.cs`, `Core\SearchService.cs`, `Core\ClipSearch.cs` | **DONE `d9849ed`** (refining code-on-truncated) | 61/61 + 8/8 + 2/2; prose clean; 20.4 ms at 2k |
| S2 | Visual audit + contrast tokens | `UI\web\src\styles.css`, `icons.tsx` | **DONE `0d0b666`** | 16/20; 62+62 pairs, 0 failures; lowest 4.62 / 4.82 |
| R1 | OCR research (engine, cost, storage) | scratch only | **DONE** — Windows.Media.Ocr, +25.4 MB exe, zero schema change | Accuracy table, per-image ms, migration cost |
| R2 | Export + tag-query research | scratch only | **DONE** — CSV byte-identical, BOM yes, `sep=` never | CSV proof on real clip, 15-query grammar |
| S3a | Search parser + tag filter + `search` bridge | `Core\ClipSearch.cs`, `Host\` | **DONE `ffc97aa`** | 15/15 grammar rows; 3–30 ms at 1k/2k, budget 50 ms |
| S3b | Row contract + tags + real Inter + focus=selection | `UI\web\src\*` | **DONE `2131962`** | 10-line clamp (65→245 px rows), index `01`, provenance, 5 widths 0 overflow, Inter 400/500/600 inlined |
| S4a | Keyboard matrix + host search + live capture in the UI | `UI\web\src\*` | IN PROGRESS | key→action table, unknown-tag hint, selection stable |
| S4b | Clip actions in the host: paste, delete, undo, pin, move | `Host\`, `Core\` | QUEUED (after S6a) | Paste into Notepad lands; undo restores |
| S4 | Clip actions: paste, delete/undo, pin, move, live capture | `Host\`, `UI\web\src\*` | QUEUED | Paste into Notepad lands; undo restores; capture appears live |
| S3c | **P0** truncation guard, corrupt-DB recovery, one search rule | `Core\`, `Host\`, `UI\ClipRow.cs`, `UI\EditorWindow*` | **DONE `5a7a285`** | 600-emoji fixture, write guard refuses preview save, corrupt DB recovers once |
| S3d | Bridge off the UI thread, tab restore, id ordering, `_meta` reopen | `Host\`, `Core\ClipboardStore.Items.cs` | **DONE `552e648`** | 35/35; UI gap 192 → 41.5 ms; 9.5k scope fixed |
| S3g | Monotonic sort key: re-copy returns to top; recovery race | `Core\ClipboardStore*.cs` | **DONE `8dcdff0`** | 45/45; A,B → copy A → A,B after reload; 38-row migration lossless |
| S5 | Rich rows: HTML/RTF/images/files, sanitizer, Word normalizer | `UI\web\src\*`, `Core\HtmlSanitize.cs` | QUEUED | Word + Excel + Chrome clips render; no network fetch |
| S6 | Export clip to file | `Core\ClipExport.cs`, `Host\`, `UI\web\src\*` | IN PROGRESS (S6a core+host) | CSV opens in Excel; PNG opens; names safe |
| S7 | Settings + TipTap editor overlay | `UI\web\src\*`, `Host\` | QUEUED | Save round-trip keeps CF_HTML; zoom persists |
| S8 | Native edges + cutover: tray, hotkey, toast, paste, single instance, autostart, DPI | `UI\ShellWindow*`, `App.xaml.cs`, `Core\Config.cs` | QUEUED | Alt+V 150 ms; toast auto-fits; reboot autostart |
| S9a | OCR engine: retarget TFM, `Core\OcrService.cs`, store into the clip text, search | `Clip.csproj`, `Core\Ocr*.cs`, `Core\ClipboardStore*` | QUEUED | Real screenshot text found by search; 40 ms warm; single worker |
| S9b | OCR in the UI: show extracted text, language notice, money caveat | `UI\web\src\*`, `Host\` | QUEUED | Image row shows its text; plain paste still pastes the picture |
| S10 | Packaging + release audit | `Clip.csproj`, `build-release.cmd`, docs | QUEUED | One exe runs on a clean profile; checklists pass |

## Open defects / debt

**From `docs\AUDIT-db-logic.md` (25 Nov):**
- **P0 truncation data loss — FIXED `5a7a285`.** One `IsTruncated`/`RemainingChars` home on the item;
  `Update` refuses to write a preview; 600-emoji fixture proves the old test was wrong.
- **P1 corrupt page — FIXED `5a7a285`.** One `Recover()` used by the constructor and every capture
  write; renames aside, recreates, retries once, notifies once.
- **P1 search disagreement — FIXED `5a7a285`.** One predicate (`SearchService.Matches`,
  OrdinalIgnoreCase) registered as the SQLite function `clip_search_match`; `CAFÉ` now 168/168.
- **P1 bridge blocks the UI thread** — S3d.
- **P1** restore into a deleted tab; wall-clock ordering in a DST fall-back — S3d.
- **P2** pinning an old clip lands at the bottom of Pinned. Deferred; revisit after use.

**Carried:**
- `docs\AUDIT-search-tags.md` (25 Nov): prose tagged as TS/Python/JS (S3f), truncated previews minting
  tags (S3f), two search rules (S3f), `Meta()` losing formats at 32k ids (S3d), a second client-side
  classifier `UI\web\src\tags.ts` (S3b, deleting it).
- `USER_GUIDE.md` must state: OCR cannot be trusted for **amounts** (₹ is unreadable by both engines,
  Indian grouping is lost); CSV opens correctly but Excel reinterprets `007` and `=1+1` on open.
- `UI\EditorWindow.xaml.cs` is 414 lines, over the 400 budget (pre-existing, +7 for the save guard).
- The v2 shell has no `CorruptionRecovered` handler yet (no writer until S4).
- Rows still show 3 lines + ISO times until S3b lands.
- Warm/hidden startup not wired (window shows immediately). — S8.
- The old WPF `UI\` stays the fallback until S8 cutover; do not delete early.

## Night log

- 01:40 — S1/S2/R1/R2 dispatched. Tracker created.
