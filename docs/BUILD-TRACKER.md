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
| S1 | `ClipTags` classifier + `tags[]` DTO | `Core\ClipTags.cs`, `Host\ClipHost.Queries.cs` | **DONE `1b57ecd`** | 27/27 fixtures, 0 false positives; real rows tagged |
| S2 | Visual audit + contrast tokens | `UI\web\src\styles.css`, `icons.tsx` | IN PROGRESS | Computed ratio table light+dark, screenshots |
| R1 | OCR research (engine, cost, storage) | scratch only | **DONE** — Windows.Media.Ocr, +25.4 MB exe, zero schema change | Accuracy table, per-image ms, migration cost |
| R2 | Export + tag-query research | scratch only | IN PROGRESS | CSV proof on real clip, 15-query grammar |
| S3a | Search parser + tag filter + `search` bridge | `Core\ClipSearch.cs`, `Host\` | **DONE `ffc97aa`** | 15/15 grammar rows; 3–30 ms at 1k/2k, budget 50 ms |
| S3b | Row contract + keyboard matrix in the UI | `UI\web\src\*` | QUEUED (waits on S2) | 10-line clamp, index `01`, provenance, digits 1–9 |
| S4 | Clip actions: paste, delete/undo, pin, move, live capture | `Host\`, `UI\web\src\*` | QUEUED | Paste into Notepad lands; undo restores; capture appears live |
| S5 | Rich rows: HTML/RTF/images/files, sanitizer, Word normalizer | `UI\web\src\*`, `Core\HtmlSanitize.cs` | QUEUED | Word + Excel + Chrome clips render; no network fetch |
| S6 | Export clip to file | `Core\ClipExport.cs`, `Host\`, `UI\web\src\*` | QUEUED | CSV opens in Excel; PNG opens; names safe |
| S7 | Settings + TipTap editor overlay | `UI\web\src\*`, `Host\` | QUEUED | Save round-trip keeps CF_HTML; zoom persists |
| S8 | Native edges + cutover: tray, hotkey, toast, paste, single instance, autostart, DPI | `UI\ShellWindow*`, `App.xaml.cs`, `Core\Config.cs` | QUEUED | Alt+V 150 ms; toast auto-fits; reboot autostart |
| S9a | OCR engine: retarget TFM, `Core\OcrService.cs`, store into the clip text, search | `Clip.csproj`, `Core\Ocr*.cs`, `Core\ClipboardStore*` | QUEUED | Real screenshot text found by search; 40 ms warm; single worker |
| S9b | OCR in the UI: show extracted text, language notice, money caveat | `UI\web\src\*`, `Host\` | QUEUED | Image row shows its text; plain paste still pastes the picture |
| S10 | Packaging + release audit | `Clip.csproj`, `build-release.cmd`, docs | QUEUED | One exe runs on a clean profile; checklists pass |

## Open defects / debt

**From `docs\AUDIT-db-logic.md` (25 Nov) — blocking:**
- **P0 truncation data loss.** `TextLength` counts SQLite code points; callers compare it to `Text.Length`
  (UTF-16 units), so an emoji-heavy clip looks complete and F2 save overwrites the stored body with the
  1,200-char preview. Five call sites. Fix: one `IsTruncated` helper in `Core\`. — **S3c**
- **P1 corrupt page not detected**: opens, then every capture throws SQLITE_CORRUPT and the app
  crash-loops; the `MoveAside` path is never reached. — **S3c**
- **P1 two search rules disagree** (`Filter` OrdinalIgnoreCase finds `CAFÉ`, SQL `lower()` does not). — **S3c**
- **P1 bridge search blocks the UI thread**: 48 ms at 1k, 531 ms at 9.5k. → run off the UI thread. — **S3e**
- **P1** restore into a deleted tab leaves an invisible row; wall-clock stamps invert order in a DST
  fall-back (order by id, display the time). — **S3d**
- **P2** pinning an old clip lands at the bottom of Pinned; tag filters only see loaded rows in the old UI. — **S3d**

**Carried:**
- `USER_GUIDE.md` must state: OCR cannot be trusted for **amounts** (₹ is unreadable by both engines,
  Indian grouping is lost); CSV opens correctly but Excel reinterprets `007` and `=1+1` on open.
- `moreCount` means remaining characters; rename to `remainingChars` + a real `truncated` flag. — S3c
- Rows still show 3 lines + ISO times in the real host build; the row contract lands in S3b.
- `UI\web\` is untracked in git — commit it with S2 once the audit stops editing it.
- Warm/hidden startup not wired (window shows immediately). — S8.
- The old WPF `UI\` stays the fallback until S8 cutover; do not delete early.

## Night log

- 01:40 — S1/S2/R1/R2 dispatched. Tracker created.
