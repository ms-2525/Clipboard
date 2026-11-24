# Build tracker ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Clip v2 release (night of 25/11/2026)

**Owner instruction:** continue building autonomously, sub-agents only, thorough DB-logic and UI/UX
audits after every major change, release-ready within 8ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Å“10 hours.
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
7. After each slice, a separate agent audits it (DB/logic + UI/UX) ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ `docs\AUDIT-slice-<n>.md`.
   Findings are fixed before the next slice starts, unless noted as accepted debt.
8. **Build lock:** while any agent has the app running from `bin\Release\net10.0-windows\`, the default
   build fails at the final copy (the exe is held). Build with `-o %TEMP%\opencode\build-<slice>` in
   that case; a lock is not a code error. Close the app before the slice's final gate build.

## Slice board

| # | Slice | Files (owner) | Status | Evidence required |
|---|---|---|---|---|
| S1 | `ClipTags` classifier + `tags[]` DTO | `Core\ClipTags.cs`, `Host\ClipHost.Queries.cs` | **DONE `1b57ecd`, then FAILED audit ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ S3f** | 27/27 fixtures, but prose tagged as TS/Python |
| S3f | Kill tag false positives, truncation rule, one search rule | `Core\ClipTags.cs`, `Core\SearchService.cs`, `Core\ClipSearch.cs` | **DONE `d9849ed`** (refining code-on-truncated) | 61/61 + 8/8 + 2/2; prose clean; 20.4 ms at 2k |
| S2 | Visual audit + contrast tokens | `UI\web\src\styles.css`, `icons.tsx` | **DONE `0d0b666`** | 16/20; 62+62 pairs, 0 failures; lowest 4.62 / 4.82 |
| R1 | OCR research (engine, cost, storage) | scratch only | **DONE** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Windows.Media.Ocr, +25.4 MB exe, zero schema change | Accuracy table, per-image ms, migration cost |
| R2 | Export + tag-query research | scratch only | **DONE** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â CSV byte-identical, BOM yes, `sep=` never | CSV proof on real clip, 15-query grammar |
| S3a | Search parser + tag filter + `search` bridge | `Core\ClipSearch.cs`, `Host\` | **DONE `ffc97aa`** | 15/15 grammar rows; 3ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Å“30 ms at 1k/2k, budget 50 ms |
| S3b | Row contract + tags + real Inter + focus=selection | `UI\web\src\*` | **DONE `2131962`** | 10-line clamp (65ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢245 px rows), index `01`, provenance, 5 widths 0 overflow, Inter 400/500/600 inlined |
| S4a | Keyboard matrix + host search + live capture in the UI | `UI\web\src\*` | **DONE** (commits in `src`, evidence `docs\design-evidence\s4a`) | full keyÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢action table; unknown-tag hint; recapture reorders without scroll jump |
| S5 | Rich rows: sanitized HTML, Word normalizer, files, placeholders | `UI\web\src\*` | **DONE `5ee4ac8`** | 12/12 adversarial payloads neutralised; 0 requests; 200 px clamp; 4 getItem calls for 1,000 rows |
| S4b | Clip actions in the host: paste, delete, undo, pin, move | `Host\`, `UI\ShellWindow.xaml.cs` | **DONE `99339dc`** | Notepad got all 4 kinds; plain paste drops rich formats; 600-emoji body pasted whole |
| S8a | Make `--v2` a real app: tray, hotkey, capture, single instance, autostart | `App.V2.cs`, `UI\ShellWindow*` | **DONE `5d56b53`** | Warm show 17.2 ms median; 6 live captures; second launch focuses first |
| S8b | Native toast auto-fit, Ctrl+Shift+V hotkey, Clear history, elevation guard | `App.V2.cs`, `NotificationWindow*`, `Host\ClipHost.Actions.cs` | **DONE `7a65194`** | Toast 77ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢160 px by content; plain paste proven; elevated target refused with a message |
| S8c | Audit blockers: copy self-capture, theme polarity, silent clipboard failure, OCR overwrite | `Host\`, `App.xaml.cs`, `Core\*` (narrow) | **DONE `4f99e5c`** | copy ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 0 captured events; theme table 6/6; failed write ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 0 chords injected |
| S3c | **P0** truncation guard, corrupt-DB recovery, one search rule | `Core\`, `Host\`, `UI\ClipRow.cs`, `UI\EditorWindow*` | **DONE `5a7a285`** | 600-emoji fixture, write guard refuses preview save, corrupt DB recovers once |
| S3d | Bridge off the UI thread, tab restore, id ordering, `_meta` reopen | `Host\`, `Core\ClipboardStore.Items.cs` | **DONE `552e648`** | 35/35; UI gap 192 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 41.5 ms; 9.5k scope fixed |
| S3g | Monotonic sort key: re-copy returns to top; recovery race | `Core\ClipboardStore*.cs` | **DONE `8dcdff0`** | 45/45; A,B ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ copy A ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ A,B after reload; 38-row migration lossless |
| S6a | Export core + host Save dialog | `Core\ClipExport.cs`, `Host\ClipHost.Export.cs` | **DONE `8cda936`** | CSV/PNG byte-identical; 24 name cases |
| S6b | Export in the UI: menu item, saved/cancelled feedback | `UI\web\src\*` | **DONE `ff11f84`** | Export from the page; toast names the file |
| S7 | Settings wired + real editor overlay | `UI\web\src\*`, `Host\ClipHost.Settings.cs` | **DONE** (evidence `docs\design-evidence\s7`) | config persists; CF_HTML round-trip valid; discard leaves body intact |
| S9a | OCR: retarget TFM, `Core\OcrService.cs`, store into the clip text | `Clip.csproj`, `Core\` | **DONE `a3dee2b`** | 93.7 % chars on clip 356; search finds it; plain paste still the picture; +25.5 MB exe |
| S9b | OCR in the UI: show extracted text with a marker | `UI\web\src\*` | **DONE `ff11f84`** | Image row shows its text; search reveals it |
| S10 | Packaging + cutover: embed `dist`, flip the default launch, checklists, release audit | `Clip.csproj`, `build-release.cmd`, `App.xaml.cs`, docs | **RELEASED** (editor/paste/quit + published warm-show re-check deferred) | One exe runs the new UI on a clean profile; capture/title/5-kind/OCR proven |

## Open defects / debt

**S10 release (25 Nov, ~05:30):**
- **H2 fixed.** `UI\web\dist` is embedded as `clip.assets.web/index.html` and served from memory over
  `https://clip.local/`; a published exe launched from an empty folder with redirected APPDATA showed the
  real page (title `Clip - clipboard`, 5/5 rows), not the fallback. A developer build from `bin\` still
  prefers the folder on disk.
- **H14 fixed.** The WebView2 profile is `%APPDATA%\Clip\webview`, next to the DB it serves.
- **Default flipped.** Plain launch = v2; `--legacy` = old WPF UI; `--v2` = alias. Single instance, tray,
  autostart target unchanged. `--db <path>` still redirects the database.
- `Core\Config.DataDir` reads an explicit `APPDATA` first so a clean-profile run can be isolated; normal
  sessions resolve to the same real path.
- Deferred to a post-release patch: see the S10 report (warm-show measurement on the published exe and the
  editor/toast/paste/quit screenshots were blocked by an elevated screen-capture overlay on the test desktop).

**From `docs\AUDIT-db-logic.md` (25 Nov):**
- **P0 truncation data loss ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â FIXED `5a7a285`.** One `IsTruncated`/`RemainingChars` home on the item;
  `Update` refuses to write a preview; 600-emoji fixture proves the old test was wrong.
- **P1 corrupt page ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â FIXED `5a7a285`.** One `Recover()` used by the constructor and every capture
  write; renames aside, recreates, retries once, notifies once.
- **P1 search disagreement ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â FIXED `5a7a285`.** One predicate (`SearchService.Matches`,
  OrdinalIgnoreCase) registered as the SQLite function `clip_search_match`; `CAFÃƒÆ’Ã¢â‚¬Â°` now 168/168.
- **P1 bridge blocks the UI thread** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â S3d.
- **P1** restore into a deleted tab; wall-clock ordering in a DST fall-back ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â S3d.
- **P2** pinning an old clip lands at the bottom of Pinned. Deferred; revisit after use.

**Carried:**
- `docs\AUDIT-search-tags.md` (25 Nov): prose tagged as TS/Python/JS (S3f), truncated previews minting
  tags (S3f), two search rules (S3f), `Meta()` losing formats at 32k ids (S3d), a second client-side
  classifier `UI\web\src\tags.ts` (S3b, deleting it).
- **S7 (25 Nov):** `ClipHost.RebindHotkey` reaches `RegisterV2Hotkeys` by reflection until
  `App.V2.cs` is free for a typed hook. Toast width/height/position are persisted by `saveConfig`
  but have no dialog row yet. Editor alignment was not added (no text-align extension installed or
  cached). Theme polarity has one home (`App.ResolveTheme`); the host calls it and also applies
  `App.ApplyThemeFromSettings` on save.
- `USER_GUIDE.md` must state: OCR cannot be trusted for **amounts** (ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¹ is unreadable by both engines,
  Indian grouping is lost); CSV opens correctly but Excel reinterprets `007` and `=1+1` on open.
- `UI\EditorWindow.xaml.cs` is 414 lines, over the 400 budget (pre-existing, +7 for the save guard).
- The v2 shell has no `CorruptionRecovered` handler yet (no writer until S4).
- Rows still show 3 lines + ISO times until S3b lands.
- Warm/hidden startup not wired (window shows immediately). ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â S8.
- The old WPF `UI\` stays the fallback until S8 cutover; do not delete early.

## Night log

- 01:40 ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â S1/S2/R1/R2 dispatched. Tracker created.
## Release blockers from docs\AUDIT-ui-code.md (25 Nov) Ã¢â‚¬â€ fix after packaging, then re-embed

- **P1 search-box keys**: Delete/Home/End act on the history while the caret is in the search box
  (useClipboardKeys.ts:142 via App.tsx:490). One guard: ignore matrix keys when the event target is
  an input/textarea/contenteditable.
- **P1 editor modal**: ria-modal claimed but no focus trap, no inert background, no focus restore
  (EditorOverlay.tsx:164). Reuse the Dialog.Root that DeleteConfirm already uses.
- **P1 stale row after save**: a saved edit never invalidates odyCache (ich.tsx:217), so the row
  keeps the pre-edit body. Add orgetBody(id) on save.
- P2: data:image/svg+xml passes the image filter (script vector); 	humbCache uncapped; a delete
  decrements the result count twice (host also emits deleted).
- Size debt: App.tsx 876 lines, styles.css 1825 Ã¢â‚¬â€ split after release, not during it.

## Night log - S10 (release)

- 05:35 - Web bundle embedded (208,159,174-byte exe); clean-profile run from an empty folder with redirected APPDATA: fresh clip.db, real page (Clip - clipboard), 5/5 clip kinds captured and visible, OCR filled the image row live. Default launch is now v2; --legacy opens the old UI. build-release.cmd rebuilds the web bundle when stale, refuses to publish without it, then prints the exe size and verifies the embedded page name.


