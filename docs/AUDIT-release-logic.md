# AUDIT - release logic & gate (final pre-release)

**Date:** 25 Nov 2026 - **Mode:** read-only (no app code changed) - **Branch:** `v2-phase1` @ **`aba6c71`**
**Worktree:** `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack`
**Read first:** `docs\BUILD-TRACKER.md`, `docs\AUDIT-db-logic.md`, `docs\AUDIT-search-tags.md`,
`docs\AUDIT-host-logic.md`, `docs\AUDIT-ui-code.md`, `docs\CHECKLIST-features.md`,
`docs\CHECKLIST-uiux.md`, `AGENTS.md`, `CURRENT_STATE.md`.

**Method.** Static reading of the shipped sources plus a scratch harness
(`%TEMP%\opencode\audit-release`) that `<Compile Include>`-s the **real** files
(`Core\ClipboardItem.cs`, `Core\ClipboardStore*.cs`, `Core\ClipTags.cs`, `Core\ClipSearch.cs`,
`Core\SearchService.cs`, `Core\ClipExport.cs`, `Interop\HtmlFragment.cs`) into one assembly and drives
them directly. The owner's database was **copied** (`%APPDATA%\Clip\clip.db` + `-wal` + `-shm` ->
`owner.db`); it was never opened in place. All other databases are fresh scratch files. Verified on the
copy: 38 rows, 230 format rows, 1 image, digest unchanged before/after the store opened and migrated.

**What I could not execute:**
- No app launch, no window driving, no screenshots: the desktop is blocked by the known UI-automation
  overlay. Every `[U]` row is therefore **NOT RUN** (and a builder may not sign it anyway).
- No live clipboard, paste into another app, IME composition, 150 % DPI/second monitor, or
  published-exe warm/cold show. No memory measurement of the release exe.
- OCR was not run through `Windows.Media.Ocr`; the OCR *store* path was exercised, not the engine.
- Those limits are stated again wherever a claim depends on them.

---

## Verdict on the six audit areas

| # | Area | Verdict | Why |
|---|------|---------|-----|
| 1 | One-home invariants | **PASS** (one mock-only duplicate) | Every invariant has one production home; `tags.ts` is gone; `lower()` SQL is gone. Only `UI\web\src\mock.ts` (dev preview, no WebView) still carries a second search/tag rule. |
| 2 | Data safety on the release build | **PASS** | 38-row migration lossless (digest `90D6FD6D...` unchanged), 1,000 inserts + cap evict exactly the oldest 200 and leave every survivor byte-identical, delete/restore keeps id/text/blobs, OCR text guarded, export bytes exact, integrity `ok`, ids never reused. No P0 data path found. |
| 3 | Bridge contract vs page | **PASS** | 15/15 page calls exist in `ClipHost.Invoke/Queue` with the same parameter names; `ItemDto` <-> `ClipItem` field-for-field; every event the page handles is emitted (by `ClipHost` or `ShellWindow`). |
| 4 | New features end to end (harness) | **PASS with one P1** | `image:` exact; PNG/TXT/CSV bytes proven; OCR text stored/searched/not plain-pasted; 28/28 tag fixtures + 6/6 real-row tags; CF_HTML envelopes valid and round-trip. **Export's on-screen feedback is broken (F1)** - the bytes are right, the confirmation is invisible. |
| 5 | Release hygiene | **PASS with debt** | 0 TODO/FIXME/stubs, 0 `Console.WriteLine`, 27/27 empty catches carry a reason, no colour literal outside the token blocks (web) except the known 34-row WPF fixture, `dist\` and `node_modules` untracked/ignored, dev probes `#if DEBUG`. 6 files over 400 lines; 3 new files are LF-only. |
| 6 | Checklists | **FAIL** | 3 PASS, 9 FAIL (features absent or stale), 24 NOT RUN. The uiux checklist still describes the retired WPF surface. The owner's stated release definition ("both checklists pass with evidence; a fresh agent signs the visuals; memory inside the ceiling") is **not met**. |

---

## 1. The one-home invariants - proved by call sites

| Invariant | One home | Proof |
|---|---|---|
| Truncation | `ClipboardItem.IsTruncated` / `RemainingChars` (`Core\ClipboardItem.cs:37,40`) | `TextLength > PreviewChars` is the only test. Grep for `TextLength >` / `Text.Length` finds only the search-box caret code and the property itself. Consumers: `Host\ClipHost.Queries.cs:180-181` (DTO), `UI\MainWindow.xaml.cs:482`, `UI\MainWindow.Rich.cs:125`, `UI\ClipRow.cs:88`, `Core\ClipboardStore.Items.cs:168` (`Update` refuses a preview), `Core\ClipTags.cs:192`, `Host\ClipHost.Settings.cs:262`, `App.xaml.cs:195`. Page reads the host's `truncated` boolean (`host.ts:172`). |
| Tag classifier | `Core\ClipTags.cs::Classify` | `UI\web\src\tags.ts` no longer exists. Production has exactly 4 call sites, all in `Host\ClipHost.Queries.cs` / `.Actions.cs`; `SearchService.Filter` consumes `ClipSearch` only, not a classifier. The only other tag logic is `mock.ts:130` (mock host) and `data.ts` fixtures. |
| Search rule | `SearchService.Contains` (`Core\SearchService.cs:13`) | `Filter` delegates to `ClipSearch.Parse/Matches` (`SearchService.cs:25-28`); `SearchFull` registers `clip_search_match` running the same predicate (`ClipboardStore.Items.cs:337-338`). No `lower(`/`LIKE`/`COLLATE NOCASE` search remains (only comments and the tab-name collation). Harness on 1k rows: `budget` Filter = SQL = 1000/1000; the old audit's `cafe` 168 = 168 at `5a7a285` used the same predicate. |
| Theme resolver | `App.ResolveTheme` (`App.xaml.cs:381-385`) | The only registry read is passed into it from `App.ApplyTheme` (`:368`) and `ClipHost.ResolveTheme` (`ClipHost.Settings.cs:160`). Both old inverted ternaries are gone. |
| Clipboard suppression | `CaptureService.Suppress(seq)` | v2 wires one hook: `App.V2.cs:103` `ClipHost.SuppressCapture = seq => _capture.Suppress(seq)`. Every v2 clipboard write calls it: copy (`ClipHost.Queries.cs:319`, only when `wrote`), paste (`ClipHost.Actions.cs:159`). The legacy app calls the same `CaptureService.Suppress` on its four writes. `CaptureService` compares the sequence number at change and at fire time (`CaptureService.cs:51,60`). |
| Sort key | `sort_seq DESC, id DESC` | Every item list query: `Query` (`ClipboardStore.Items.cs:249`), `SearchFull` (`:344`), `LatestText` (`:320`), `EnforceCap` (`:459`), dedupe candidate (`:122,128`). No list orders by `captured_at` any more. Tabs keep their own `sort_order` (a different table/order, correct). |
| CF_HTML writer | `Interop\HtmlFragment.Wrap` | Editor saves both use it: `Host\ClipHost.Settings.cs:266` and `UI\EditorWindow.xaml.cs:315`. No second `StartHTML` writer exists; capture stores the producer's envelope verbatim by design. |

Residual duplicate (not a release path): `UI\web\src\mock.ts` re-implements `toLowerCase` search
(`:117,133-136,154-161`) and `tag:` token parsing (`:119`). It runs only when `window.chrome.webview`
is absent (`host.ts:356-360`). `data.ts:115` computes `truncated` by string length on fixtures.
P3; keep the mock out of future "one rule" claims or make it call the same grammar.

---

## 2. Data safety on the release build (copies only)

Harness: `%TEMP%\opencode\audit-release\out\ReleaseAudit.exe`. Raw console output is in the transcript;
every check printed `PASS`.

| Test | Observed |
|---|---|
| Owner copy (38 rows / 230 formats / 1 image / 10 truncated) opened by the real store | digest of every row and format blob **before == after**; `integrity_check ok`; 0 orphans; Pinned tab intact. |
| 1,000 inserts then `EnforceCap(200)` | main 200, pinned row survives, tab row survives, 0 orphans, `integrity ok`; **every surviving row byte-identical**; the evicted set is exactly the oldest main rows. Insert 1,000 = 113 ms; cap = 2 ms (repeat 2 ms). |
| 600 emoji + 1,000 ASCII (the old P0 fixture) | `TextLength = 1600`, preview UTF-16 = **1800** (the old trap), `IsTruncated = true`; `GetText` returns all 1,600 code points; `Update(preview)` returns **false**; body survives. Export of that clip writes all 3,400 UTF-8 bytes. |
| Delete + restore (owner row and synthetic rich row) | same id, same text, **2/2 format blobs**, restored at the top; undo into a deleted tab lands in main history (visible). |
| OCR on the real image row | stores text, kind stays `image`, `LatestText()` does not return the OCR words, `SearchFull` finds the token, a second OCR pass is refused (`text IS NULL` guard). |
| Export bytes | image = stored PNG **227,259 B identical**; text = full body, no BOM; spreadsheet = CSV with `EF BB BF`, no `sep=`; a truncated clip exports the whole body. |
| Ids | `AUTOINCREMENT`: deleting max id 1370 and inserting gives 1371 (never reused). |
| Corrupt file | a scribbled copy was renamed to `corrupt.db.corrupt-20261125-055543` (630,784 B), a fresh DB created, `RecoveredFromCorruption=True`, and the next insert succeeded. Mid-session recovery is the same `WriteWithRecovery` wrapper used by every capture (`CaptureService.cs:132`). |

No path was found that silently truncates, overwrites or drops a stored body. The only silent
data-adjacent behaviour is **corruption recovery without a message in v2** (F5).

---

## 3. Bridge contract vs the page

Every method the page calls exists, with the same parameter names:

| Page (`host.ts`) | Host | Params match |
|---|---|---|
| `init` | `ClipHost.Queue` `:178` | none |
| `getItems({tabId,offset,limit,query})` | `:194-204` | **yes** |
| `search({tabId,offset,limit,query})` | `:207-215` | **yes** |
| `getItem({id})` | `ClipHost.Invoke:108` | **yes** |
| `getThumb({id})` | `:123` | **yes** |
| `copy({id,plain})` / `paste({id,plain})` | `:129` / `BeginPaste` | **yes** |
| `delete/restore/togglePin({id})` | `ClipHost.Actions` | **yes** |
| `moveToTab({id,tabId})` | `:81` | **yes** (no page caller - see F3) |
| `hide()`, `export({id,format})`, `saveConfig({patch})`, `saveEditor({id,html,text})`, `clearHistory()` | `ClipHost` / `.Export` / `.Settings` | **yes** |

DTO field-for-field: `id, kind, app, title, time, pinned, tabId, text, hasImage, hasHtml, hasRtf,
formats, tags, truncated, remainingChars` (`ItemDto` :337-357 <-> `ClipItem` host.ts:15-34). `init`
returns `counts {clips,images,all}`, `tabs [{id,name,kind,count}]`, `config` (10 fields), `items`;
`toSettings` defaults every field. `Counts.today/pinned` are declared but never sent -> the "N today"
chip is dead UI (`App.tsx:263,615`), a pre-existing P3.

Events: host emits `captured, edited, deleted, plainMode, themeChanged, settingsSaved, cleared`;
`ShellWindow` emits `skipped, hotkeyFailed, cleared, themeChanged`. The page handles all except
`edited`/`plainMode` (nothing currently emits `edited` except `saveEditor`, whose page path already
refreshes; `plainMode` is cosmetic). No page handler listens for an event nobody emits.

---

## 4. New features, harness level

| Feature | Verdict | Evidence |
|---|---|---|
| `image:` search | **PASS** | Parsed as an include tag; on the owner copy it returns exactly the 1 image row, 0 others. |
| Tags on real rows | **PASS** | 6/38 rows tagged, all correct: `spreadsheet` (real Excel), `link` x2, `path`, `[files,path]`, `image`; 0 false positives. |
| Tag fixture gate | **PASS** | My 28-case table (13 positives, 15 controls including `Note: any changes...`, `Cheque status: void`, `import data from the bank portal`, `This function (callback)...`, `Drop table reservations, please.`, `Select the ledger from the list`) = **28/28**. |
| Export | **PASS (bytes)** / **FAIL (feedback)** | PNG byte-identical to the stored blob; TXT no BOM; CSV BOM, no `sep=`. The Save dialog hides the shell and the success toast goes to the hidden page (F1). |
| OCR | **PASS (storage/search)** | text stored, full-body searchable, `LatestText` excludes images, late pass cannot overwrite a user edit; engine accuracy not re-run (93.7 % is the recorded number). |
| Editor save / CF_HTML | **PASS** | `HtmlFragment.Wrap` + `Extract` round-trip on `<p>plain</p>`, Devanagari+emoji, and a table: `EndHTML == UTF-8 byte length`, `StartFragment/EndFragment` bracket the fragment, extracted == sent, 3/3. `SaveEditor` loads the full row, refuses truncated/empty/2 MB/`<script`, writes text + envelope + drops stale RTF/formats. |
| Search performance @1k | **PASS** | `GetRecent(1000)` 4 ms, Filter 1 ms, Parse+Matches <1 ms, `SearchFull` 24-25 ms. First classification pass pays ~91 ms JIT; steady state 1 ms/1k. Core word search is ~5 ms, inside the 50 ms budget. |

---

## 5. Release hygiene (`AGENTS.md`)

| Check | Result |
|---|---|
| Build gate | `dotnet build -c Release` -> **0 errors, 0 warnings** (1.9 s). `tsc --noEmit` -> **0**. |
| TODO/FIXME/stubs/`[DEBUG]`/`NotImplementedException` | **0** in C#; no `console.*`/TODO in `UI\web\src`. |
| `Console.WriteLine` / `Debug.WriteLine` | **0**. |
| Empty catches | **27**, every one carries a one-line reason (checked each). |
| Hardcoded colours | `UI\Themes\*.xaml` are the token home; 34 literals remain in `UI\SettingsWindow.xaml:445-487` (pre-existing fixture sample); the documented `#FF6B6B` exception no longer exists in the tree. Web: all 78 colour literals are in `styles.css:2-84` (`:root` + dark), none in components. |
| File budget (400 lines) | **6 over:** `UI\web\src\styles.css` 1785, `UI\web\src\App.tsx` 815, `UI\SettingsWindow.xaml` 473, `UI\MainWindow.xaml.cs` 468, `Core\ClipboardStore.Items.cs` 433, `Core\HtmlToRtf.cs` 402. (Tracker already records the web two as post-release debt; the rest are pre-existing.) |
| Dev-only probes | `CLIP_V2_TIMING` and `CLIP_V2_DEBUG_PORT` are inside `#if DEBUG` (`ShellWindow.Events.cs:20,144,160`; `ShellWindow.xaml.cs:64`). `APPDATA` override is product behaviour, not a probe. |
| `dist\` committed | No. `dist/` is ignored; `dist\Clip.exe` (208,159,174 B) is untracked. |
| `UI\web\node_modules` ignored | Yes (`UI\web\.gitignore`), and `node_modules` is not tracked. |
| Working tree | Clean except the four untracked `docs\AUDIT-*.md` reports (including this one). |
| Release artifact matches HEAD | The exe (05:44:34) is newer than the last web source edit (05:43:34) and the rebuilt bundle (05:44:26); the embedded bundle contains the post-blocker raster-only `data:image/(png|jpe?g|gif|webp);base64` rule, so the six `aba6c71` fixes are inside the shipped exe. The commit itself is 40 s later than the publish (fix -> rebuild -> publish -> commit). |
| EOL | `Host\ClipHost.Queries.cs`, `Host\ClipHost.Settings.cs`, `UI\ShellWindow.xaml` are LF-only while the repo is CRLF. P3. |

---

## Findings

**P0 - none.** No stored-body truncation/overwrite/drop path was found on the release build.

### P1 - Export's Save dialog hides the window that reports the result (H4 is not fixed)

`Host\ClipHost.Export.cs:55` calls `dlg.ShowDialog(Window.GetWindow(_web))` and returns at `:56/:71`
without showing the shell. `UI\ShellWindow.xaml.cs:49` hides on **any** deactivation:
`Deactivated += (s, e) => { if (!_warming) Hide(); };`. The dialog takes activation, the shell hides,
and both the page's "Saved..." toast and an export error land in a hidden page. After Save or Cancel
the app stays hidden until the owner presses Alt+V.

Evidence: the host audit H4 observed exactly this at `a3dee2b`; grep for `_modal`/`ShowAfterExport`
finds nothing; no `_show()` exists in `ClipHost.Export.cs`. The bytes are written correctly (harness).

**Fix (3 lines):** re-show after the dialog (`_show()` before both returns), or add a `_modal` flag the
Deactivated handler checks. **Blocks signing a release: yes** (a primary feature reports nothing), not
a data defect.

### P1 - The release gate itself is not met (evidence, not a code bug)

The owner's definition (`BUILD-TRACKER.md:5-7`) requires both checklists to pass with evidence, a fresh
agent's visual sign-off, memory inside the ceiling, and a dual-axis audit. Current state:
- `CURRENT_STATE.md:29-30` records **~528 MB idle / ~643 MB with 5 clips** for the v2 app; the old
  ceiling was 250 MB. The tracker still lists "memory inside the ceiling" as a release condition. That
  is 2.1-2.6x over, and it is documented as the price of WebView2 rather than owner-accepted.
- `[U]` sign-off: none. All screenshots in `docs\design-evidence\s4a` are headless-mock captures and
  `s7` is builder-driven over CDP; the protocol (`CHECKLIST-uiux.md:12-15`) allows neither to sign.
- Warm-show on the published exe and cold open on v2 are unmeasured (`CURRENT_STATE.md:27` says
  "re-check pending"), and the checklists require them.

**Blocks signing: yes.** It does not block an owner preview.

### P2 - The v2 UI has no tab management; `moveToTab` has no caller

`host.ts:121,317` declares `moveToTab`, `ClipHost.cs:154` dispatches it, and no `.tsx` calls it (grep).
There is no create/rename/delete tab surface either. The v2 page can *view* existing tabs (init returns
them) and pin/unpin, but cannot move a clip into a tab or make one. `CURRENT_STATE.md:13-14` says
"pin/delete/undo/**move** ... are live" - overclaimed. The old WPF UI had all of it. Not data loss; a
workflow regression the owner will notice if they used tabs.

### P2 - v2 search is preview-only; text past 1,200 characters is unfindable

`ReadSearch` (`Host\ClipHost.Queries.cs:107-149`) matches the list rows' `Text`, which is
`substr(text,1,1200)` (`ClipboardStore.Items.cs:246`). No `SearchFull` call exists in `Host\`
(grep). The old UI falls back to `SearchFull` when a row is truncated (`MainWindow.xaml.cs:279`).
So a term that occurs only in the tail of an email/transcript is not found in the shipped UI, while
the stored body contains it. Tags are protected from the same trap by `IsTruncated` (`ClipTags.cs:192`).

### P2 - Corruption recovery is silent in v2

`ClipboardStore` raises `CorruptionRecovered` (`:35,83`) and the constructor reports
`RecoveredFromCorruption`. Only the **legacy** App subscribes (`App.xaml.cs:78,100`); `App.V2` /
`ShellWindow` have no handler (grep). On corruption the history is moved aside and a fresh DB starts
with no message, so the owner sees an empty list and no explanation. The file is preserved
(`clip.db.corrupt-<stamp>`), so this is recoverable by hand, not data loss.

### P2 - `_deleted` keeps every deleted clip's blobs for the whole session

`Host\ClipHost.Actions.cs:21` dictionary, filled at `:48` from `ReadFullItem` (text, HTML, RTF, image,
thumb, every format blob). The UI offers one-level undo, so older snapshots are dead weight - up to
hundreds of MB after deleting screenshots. H6 from the host audit is still open.

### P2 - Keyboard rows that the frozen checklist requires are absent

`useClipboardKeys.ts` has no `Ctrl+Up/Down` time-section jump and no `Ctrl+Shift+Left/Right` tab move;
there are no time sections in the v2 list, and no tab-name editor / `Ctrl+M` picker / tab-delete confirm,
so the Esc cascade order in the checklist cannot be satisfied. `Ctrl+P`, `Ctrl+1..9`, `Ctrl+F/K`,
`Ctrl+Shift+V`, `F2`, `Delete`, `Ctrl+Z`, `Ctrl+E` are implemented (`:59-91,130-164`). This is the
largest functional gap against `CHECKLIST-features.md` sections 2 and 5.

### P2 - File-size budget and pre-existing WPF fixture colours

Six files over 400 lines (list in section 5). `SettingsWindow.xaml:445-487` still carries 34 literal
colours as rich-text fixture content; `AGENTS.md`'s `#FF6B6B` exception is stale. Both are pre-existing,
but the budget is a standing rule and the release audit must record it.

### P3 - Smaller, confirmed items

- `Config.Load` does not clamp `MaxItems` (`Core\Config.cs:38,76-99`); a hand-edited `0` makes
  `EnforceCap(0)` delete all main history (`ClipboardStore.Items.cs:444-462`). The UI clamps 20-10,000.
- Two theme watchers subscribe the same `OnThemePreferenceChanged` (`App.V2.cs:110,229` and
  `App.xaml.cs:395`). Same resolver now, so no wrong polarity; it is duplicated work and can emit
  `themeChanged` twice per Windows change.
- `mock.ts` second search/tag grammar and `data.ts` length-based truncation (dev preview only).
- Tag chips remain mouse-only (`ClipList.tsx:291-306`: button, `tabIndex={-1}`, inside `role="option"`)
  - UI audit A1 open. Character count freezes after the first edit (`EditorOverlay.tsx:157`, Y1). Row
  tooltip can carry the full body (`rich.tsx:288`, S4). Settings opens with focus on the trigger
  (`SettingsDialog.tsx:153`, A5). Dead "N today" chip (`App.tsx:263,615`, H1).
- Over-budget clipboard formats are dropped by `ClipboardFormats.Snapshot` without a `Skipped` notice;
  only oversized images raise one (`CaptureService.cs:193-225`).
- Pinning from All reselects an id that is no longer in the main list (`App.tsx:334-337`); the next
  Enter/Delete does nothing until the selection moves. Cosmetic.
- `CURRENT_STATE.md:20` says the embedded page is 2,840,139 B; the built bundle is 2,840,788 B and the
  exe was published after it. Documentation drift, not a bad artifact (the new bundle's raster-only
  data-URI rule is present in `dist\Clip.exe` at offset 12,647,973).
- `Host\ClipHost.Queries.cs`, `Host\ClipHost.Settings.cs`, `UI\ShellWindow.xaml` are LF-only.
- The four `docs\AUDIT-*.md` reports are untracked; a fresh clone loses the release evidence trail.

---

## Checklist status (strict: unproven = NOT RUN)

`[H]` = harness - `[L]` = live/measured - `[U]` = fresh-agent visual - `[M]` = motion.

### `docs\CHECKLIST-features.md` sections 1-9

| Section | Row | Status | Evidence |
|---|---|---|---|
| 1 | Cold first popup 262-310 ms over budget | **NOT RUN** (v2) | Old-UI FAIL stands; v2 published-exe cold/warm not measured (`CURRENT_STATE.md:27`). |
| 2 | IME composition gate | **NOT RUN** | `isComposing/keyCode 229` guard exists (`useClipboardKeys.ts:38`); cannot synthesize an IME. |
| 2 | Ctrl+Up/Down time sections | **FAIL** | Absent from the key matrix; no time sections in the list. |
| 2 | Esc cascade (IME -> editor -> tab delete -> tab name -> Ctrl+M -> F2 -> search -> hide) | **FAIL** | Only IME guard, Radix modal Esc, search->hide exist; tab/name/picker surfaces absent. |
| 2 | [U] Selection stable on live capture | **NOT RUN** | Code keeps selection by id; no screenshot. |
| 3 | Live Excel/Word/Chrome/Paint matrix | **NOT RUN** | Never run; ACC-v2 records Office/Paint as untested. |
| 3 | Plain paste (Ctrl+Shift+V) selected item | **NOT RUN** | Path exists (`useClipboardKeys:65`, `PasteNow`); no live evidence at HEAD. |
| 4 | [H] CF_HTML byte-offset extraction, Unicode | **PASS** | Harness 3/3: wrap valid, `EndHTML` = byte length, fragment round-trips (emoji/Devanagari/table). |
| 4 | [U] Remote images never fetched | **NOT RUN** | Code allows only `data:image/(png|jpe?g|gif|webp);base64` (`rich.tsx:167`); no screenshot. |
| 4 | [U] F2 editor after TipTap port | **NOT RUN** | s7 screenshots are builder-run, not fresh-agent signed. |
| 4 | [L] F2 open ~2.9 s / +302 MB | **NOT RUN** | Recorded only as "unchanged" in `CURRENT_STATE.md:48`; not re-measured on v2. |
| 4 | [U] RTF-only clips render | **NOT RUN** | No fresh visual; code path present (`EditorRtf`). |
| 4 | [U] Known ceilings documented on screen | **FAIL** | No tofu/Docs/Notion copy in `UI\web\src`; the limits live only in `USER_GUIDE.md`. |
| 4 | [L] 5 MB PNG decode/encode/SHA-256 off the UI thread | **FAIL** | Capture runs on the DispatcherTimer thread and decodes/encodes/hashes synchronously (`CaptureService.cs:230-247,282-304`; hash in `InsertRow`). |
| 4 | [L] Memory discipline: thumbs only in the list | **NOT RUN** | List DTO has no image blob and thumbs load per row (`getThumb`); not measured. |
| 5 | Ctrl+P to/from Pinned | **NOT RUN** | Store semantics verified (`TogglePin`); UI live path unproven. |
| 5 | Ctrl+Shift+Left/Right adjacent tab | **FAIL** | Absent; no tab move UI at all. |
| 5 | [U] Re-copy never shows twice | **NOT RUN** | s4a mock shows reorder-to-top; no live/fresh evidence. |
| 6 | [U] Ghost notification preview | **FAIL** | v2 Settings has no preview; the ghost is legacy WPF only (`NotificationPreviewWindow`). |
| 6 | [L] First-run hint on first launch | **FAIL** | `WasNew` is only used by the legacy App (`App.xaml.cs:97`); v2 shows nothing. |
| 6 | [L] Skipped captures raise a notice | **FAIL** | Oversized image yes; over-budget clipboard formats are dropped silently. |
| 7 | 3-line row clamp, `previewLines` tooltip | **NOT RUN (stale)** | v2 contract is 10 lines (section 9), no `ClipRow.ListLinesMax`; the row describes the retired WPF list. |
| 7 | [U] Identity tile / facts / no-meta row | **NOT RUN (stale)** | v2 rows have index + per-row provenance, not the v3 anatomy. |
| 8 | Writes only in its folder and `%APPDATA%\Clip` | **PASS** | All writers use `Config.DataDir`; WebView profile now `DataDir\webview` (`ShellWindow.xaml.cs:60`); S10 clean-profile run created a fresh DB there. |
| 8 | One-pass acceptance (apps, password manager, reboot) | **NOT RUN** | Not run at HEAD; S10 captured 5 kinds only. |
| 8 | WPF `CanContentScroll`/recycling | **NOT RUN (stale)** | v2 uses react-virtual; no WPF list. |
| 8 | WPF search debounce/collection swap | **NOT RUN (stale)** | v2 has a 120 ms debounce (`App.tsx:166`) and a new results object; the WPF mechanism is gone. |
| 8 | Corrupt/locked DB: rename + notice | **FAIL** | Recovery proven (harness: renamed aside, fresh DB, write OK) but v2 shows **no notice** (no `CorruptionRecovered` handler). |
| 8 | Acceptance at 1,000 items | **NOT RUN** | Partial: `GetRecent(1000)` 4 ms PASS, core word search 5 ms PASS; scroll/cold start/idle RAM unmeasured. |
| 9 | [H] ClipTags fixture table >=20 + 8 controls | **PASS** | 28/28 harness cases; real 38-row copy 6/6 correct tags, 0 false positives. |
| 9 | [U] Row = index 01, 10 lines, app/title/time at 430 px | **NOT RUN** | s4a captures are mock/builder; 430 px row not signed. |
| 9 | [L] Digit 1-9 copies the visible index | **NOT RUN** | Implemented (`useClipboardKeys:157` -> `rows[key-1]`); mock observed, live unproven. |
| 9 | [U] 1-line vs 10-line vs 30-line + tooltip | **NOT RUN** | No fresh visual. |
| 9 | [L] Mixed-height virtualizer, selection across filter | **NOT RUN** | Code uses `@tanstack/react-virtual` with measured elements; no 1,000-item scroll measurement. |
| 9 | [L] Toast auto-fit at 150 % DPI | **NOT RUN** | WPF toast unchanged; one line 77 px observed at 100 %; 30 lines/mixed DPI unproven; configured height was ignored historically. |
| 9 | [L] Web keyboard matrix with Esc cascade | **NOT RUN** | s4a observed most keys in a mock; matrix incomplete (Ctrl+Up/Down, tab moves) and not live. |

Totals: **3 PASS - 9 FAIL - 24 NOT RUN.**

### `docs\CHECKLIST-uiux.md` - relevant rows

The whole file is still written for the retired WPF surface (identity tile, 3-line rows, facts column
above the clock, ghost preview, `CanContentScroll`, "no per-row metadata line"), while the shipped v2
contract is 10-line rows with a number gutter and per-row `app / title / time` (section 9). It cannot
be used as the acceptance gate until it is rewritten for the web surface.

| Row | Status | Why |
|---|---|---|
| Section 0 all six comparative gates (popup, chrome, images, editor, settings, notification/tray) | **NOT RUN** | `[U]` requires a fresh agent with its own screenshots; UI automation is blocked. |
| Identity tile / facts column / truthful `+N` / 3-line clamp / selection surface / Ctrl+1..9 affordance | **NOT RUN (stale)** | These describe the WPF row anatomy, not the v2 row (`ClipList.tsx:270-310`). |
| Type levels, Inter body, icon family, tray icon | **NOT RUN** | No signed screenshots; s2 contrast math (62+62 pairs, 0 failures) is token-level only. |
| Light/dark AA, theme live + restart | **NOT RUN** | Token evidence exists (S2); the `[U]` part is unsigned. |
| Motion: open/close/pulse/FLIP/reduced motion | **NOT RUN** | `[M]` never measured on v2. |
| Drag-to-move focus, first-run empty state, ghost preview, tray clear confirm, invalid settings, multi-monitor, no stock WPF control | **NOT RUN** | No live run; ghost row is stale (v2 has no ghost). |
| Section 2 frozen decisions | **N/A** | Decisions, not checks. |

No `[U]` row in this file can be marked PASS by this audit, by the file's own protocol.

---

## Release verdict

**DO NOT SHIP as release-ready.** The binary is **data-safe** and the build is clean; what fails is the
project's own release gate, not the data layer.

**Evidence for the data layer (strong):** real 38-row DB migrated losslessly, 1,000-row cap exact and
byte-preserving, truncation P0 closed with a 600-emoji proof, search one rule, ids stable, OCR guarded,
exports byte-exact, integrity `ok`, corrupt file recovered. P0 count: **0**.

**What blocks the signature (all named, none hidden):**

1. **Fix the export feedback** (P1, ~3 lines): re-show the shell after the Save dialog, or make the
   Deactivated handler ignore the modal. Until then a successful export looks like the app vanished.
2. **Decide the memory ceiling with the owner.** v2 is ~528 MB idle / ~643 MB with clips vs the 250 MB
   release condition. Either accept it in writing (it is the WebView2 trade) or the release condition
   is unmet.
3. **Close or owner-accept the tab gap** (no create/move/delete in v2) and the two missing keyboard
   rows (`Ctrl+Up/Down`, `Ctrl+Shift+Left/Right`).
4. **Run the live acceptance rows and the fresh `[U]` sign-off** (24 features rows NOT RUN, all uiux
   `[U]` rows unsigned, published-exe warm/cold unmeasured). This audit cannot do it - windows cannot
   be driven here.
5. **Record the checklist truth**: `features` currently has 9 FAIL / 24 NOT RUN; `uiux` is stale for
   v2. `CURRENT_STATE.md` and `BUILD-TRACKER.md` should say "preview build", not "shipped/released",
   until 1-4 are closed.

**If the owner wants the current exe now:** acceptable as an **owner preview** with these limits -
export confirmation invisible until the shell is re-opened; no tab management; text beyond 1,200
characters is not searched; corruption is recovered silently; ~0.5-0.65 GB RAM; no fresh visual
sign-off. No limit in that list risks losing stored data.

**Evidence index.** Harness: `%TEMP%\opencode\audit-release` (`ReleaseAudit.csproj`, `Program.cs`,
`out\`), databases `owner.db` (+ `-wal/-shm`), `owner-stress.db`, `fresh.db`, `corrupt.db`,
`perf.db`. Commands: `owner`, `stress`, `fresh`, `tags`, `cfhtml`, `perf`, `corruptwrite`, `dump`.
Build: `dotnet build -c Release` (0/0), `tsc --noEmit` (0). No app code changed; no live database
opened.
