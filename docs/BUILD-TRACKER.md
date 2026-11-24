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

## Slice board

| # | Slice | Files (owner) | Status | Evidence required |
|---|---|---|---|---|
| S1 | `ClipTags` classifier + `tags[]` DTO | `Core\ClipTags.cs`, `Host\ClipHost.Queries.cs` | **DONE `1b57ecd`** | 27/27 fixtures, 0 false positives; real rows tagged |
| S2 | Visual audit + contrast tokens | `UI\web\src\styles.css`, `icons.tsx` | IN PROGRESS | Computed ratio table light+dark, screenshots |
| R1 | OCR research (engine, cost, storage) | scratch only | IN PROGRESS | Accuracy table, per-image ms, migration cost |
| R2 | Export + tag-query research | scratch only | IN PROGRESS | CSV proof on real clip, 15-query grammar |
| S3a | Search parser + tag filter + `search` bridge | `Core\ClipSearch.cs`, `Host\` | IN PROGRESS | `image:`/`excel:`/`-x:` parsed; 15-query table; <50 ms at 1k |
| S3b | Row contract + keyboard matrix in the UI | `UI\web\src\*` | QUEUED (waits on S2) | 10-line clamp, index `01`, provenance, digits 1–9 |
| S4 | Clip actions: paste, delete/undo, pin, move, live capture | `Host\`, `UI\web\src\*` | QUEUED | Paste into Notepad lands; undo restores; capture appears live |
| S5 | Rich rows: HTML/RTF/images/files, sanitizer, Word normalizer | `UI\web\src\*`, `Core\HtmlSanitize.cs` | QUEUED | Word + Excel + Chrome clips render; no network fetch |
| S6 | Export clip to file | `Core\ClipExport.cs`, `Host\`, `UI\web\src\*` | QUEUED | CSV opens in Excel; PNG opens; names safe |
| S7 | Settings + TipTap editor overlay | `UI\web\src\*`, `Host\` | QUEUED | Save round-trip keeps CF_HTML; zoom persists |
| S8 | Native edges + cutover: tray, hotkey, toast, paste, single instance, autostart, DPI | `UI\ShellWindow*`, `App.xaml.cs`, `Core\Config.cs` | QUEUED | Alt+V 150 ms; toast auto-fits; reboot autostart |
| S9 | OCR: extract, store, search, display | `Core\Ocr*.cs`, `Host\`, `UI\web\src\*` | QUEUED | Real screenshot text found by search; <100 ms warm |
| S10 | Packaging + release audit | `Clip.csproj`, `build-release.cmd`, docs | QUEUED | One exe runs on a clean profile; checklists pass |

## Open defects / debt

- `moreCount` means remaining characters; rename to `remainingChars` (UI shows `+1.8k`). — S3.
- Rows still show 3 lines + ISO times in the real host build; the row contract lands in S3.
- `UI\web\` is untracked in git — commit it with S2 once the audit stops editing it.
- Warm/hidden startup not wired (window shows immediately). — S8.
- The old WPF `UI\` stays the fallback until S8 cutover; do not delete early.

## Night log

- 01:40 — S1/S2/R1/R2 dispatched. Tracker created.
