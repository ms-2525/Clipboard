# AUDIT - release logic, second pass (final pre-release)

**Date:** 25 Nov 2026 - **Mode:** read-only (no product code changed) - **Branch:** `v2-phase1`
**HEAD:** `bae76ad` (docs only after the code head `1ced540`; exe published at 06:52, HEAD docs at 07:00)
**Worktree:** `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack`
**Previous pass:** `docs\AUDIT-release-logic.md` (at `aba6c71`) - six reasons for "not release-ready".
**Read first:** `docs\BUILD-TRACKER.md`, `docs\CHECKLIST-features.md`, `docs\CHECKLIST-uiux.md`,
`CURRENT_STATE.md`, `AGENTS.md`, the four fix commits (`ec4ce55`, `4954e87`, `1ced540`, `50207bd`).

**Method.** Read the shipped sources; then two scratch harnesses that compile/load the **real** code:

- `%TEMP%\opencode\audit-release-2\core` - the previous harness rebuilt against this HEAD. Links
  `Core\ClipboardItem.cs`, `Core\ClipboardStore.cs`, `Core\ClipboardStore.Items.cs`,
  `Core\ClipboardStore.Tabs.cs`, `Core\ClipTags.cs`, `Core\ClipSearch.cs`, `Core\SearchService.cs`,
  `Core\ClipExport.cs`, `Interop\HtmlFragment.cs`. Databases are copies or fresh scratch files.
- `%TEMP%\opencode\audit-release-2\build\BridgeAudit.exe` - loads the real `Clip.dll` from the Release
  build and calls the **real internal bridge statics** (`ClipHost.ReadSearch`, `ClipHost.Export`,
  `ClipHost.ListTabs/CreateTab/RenameTab/DeleteTab`) through reflection, with the real `ModalGate`.
  This is not a re-implementation: the stack traces in the transcript name the real source lines.

The owner database was copied (`%APPDATA%\Clip\clip.db` + `-wal` + `-shm` -> `core\owner.db`) and only
the copy was opened. The live database was never touched.

**What I still could not execute:** no app launch, no window driving, no screenshots (the desktop
UI-automation overlay is still blocked). No live clipboard/paste, real IME, 150 % DPI/second monitor,
published-exe warm/cold show, or memory measurement. Every `[U]` row is therefore NOT RUN, and a
builder/auditor may not sign it anyway. Those limits are repeated where a claim depends on them.

---

## Verdict per area

| # | Area | Verdict | Why |
|---|------|---------|-----|
| 1 | Export keeps the shell (`ec4ce55`) | **PASS (harness)** | The gate is held while the dialog is open and released before `show()`; save, cancel and a failed write all run `ask -> show` and the shell is restored before `Export` returns. The reply is posted after that return (`Dispatch` -> `Reply`). |
| 2 | Full-body search (`ec4ce55`) | **PASS (harness)** | A term only at character 3,001 is found; two terms AND; a negated term is checked against the tail; tab scope, accents, paging and `unknownTags` hold. p50 **9.3 ms** at 1k rows with 200 truncated (claim was 12.4 ms). |
| 3 | Tab management (`4954e87`, `1ced540`) | **PASS (harness + reading)** | 22/22 tab checks: create/rename/list/delete reply shapes, store rules, move, delete-with-clips purges exactly the tab's rows and formats, Pinned is protected, unrelated row digest unchanged. The page calls all four methods; the delete toast uses `deletedClips`. |
| 4 | Corruption notice (`ec4ce55`) | **PASS (code) / live unobserved** | `App.V2` subscribes, the tray toast shows `ClipboardStore.CorruptionNotice`, the host emits `corruptionRecovered` and reloads the page. Recovery itself was proven in the previous pass; the toast being visible on screen is a `[U]`/live item. |
| 5 | Bridge completeness (with tabs) | **PASS** | All 21 `Host` members exist in `WebViewHost`; every page call (`init, getItems, search, getItem, getThumb, copy, paste, delete, restore, togglePin, moveToTab, listTabs, createTab, renameTab, deleteTab, hide, export, saveConfig, saveEditor, clearHistory, onEvent`) has a host method with matching parameter names. DTO fields match; tab reply shapes match. |
| 6 | Data safety on copies | **PASS** | 38-row owner copy lossless (digest `90D6FD6D19A7DD6F` unchanged on open), 1,000 inserts + cap exact and byte-preserving, delete/restore byte-identical, OCR guarded, export bytes exact, tab purge counted and clean. **No P0.** |
| 7 | Checklists | **FAIL (unchanged gate)** | `features`: **3 PASS / 7 FAIL / 26 NOT RUN**. `uiux`: 11 FAIL rows were touched by `50207bd` (9 causes fixed and now need a fresh signature, 2 remain FAIL), every `[U]` row is unsigned after the visual pass, and the tab strip has no checklist row at all. |
| 8 | Release hygiene | **PASS with debt** | Build 0/0, `tsc --noEmit` 0, 0 TODO/FIXME/stubs, 0 `Console`/`Debug`, 28/28 catches have a reason, probes `#if DEBUG`, `dist` holds only the exe, ignored, exe newer than the last web source and the bundle. **11 files over 400 lines** (was 6). |

---

## 1. The four fixes, verified against the code and the real statics

### 1.1 Export gate - closed

`Host\ClipHost.Export.cs:31-78` takes `ModalGate` and `show` as parameters. It calls `modals.Enter()`
before the dialog, and `finally { modals.Leave(); show(); }` covers save, cancel, a failed write and a
format refusal inside the dialog. `UI\ShellWindow.xaml.cs:52` now hides only when
`!_warming && !_modals.Open`; `_show` is `ShowAfterPaste` -> `ShowShell()` (Show + Activate + Web.Focus).
The reply is posted after the method returns (`ClipHost.Dispatch`: `Invoke` then `Reply`), so the page's
toast lands on a visible window.

Harness (`BridgeAudit export`, real `ClipHost.Export` + real `ModalGate`):

```
PASS save: the reply says saved and the bytes are on disk  :: saved=True wrote=True
PASS save: the gate is held while the dialog is open
PASS save: the shell is shown before Export returns, with the gate released  :: ask,show
PASS cancel: answers cancelled=true
PASS cancel: the shell is still restored before the reply  :: ask,show
PASS failed write: reports a save error  :: Could not save the clip: Could not find a part of the path ...
PASS failed write: the shell is restored too  :: ask,show
PASS kind-mismatch refusal happens before the dialog and never shows the shell
PASS image save through the gate is the stored PNG  :: bytes=70
```

The order recorded is `ask -> show`; the harness adds `reply` after the call returns, which is the
`Dispatch` order in the code. One residual (not a defect): an `Activate()` on a hidden window is a
Win32 no-op until `Show()`, but `ShowShell` calls `Show()` first, so the sequence is correct.

### 1.2 Full-body search - closed

`Host\ClipHost.Queries.cs:107-181` parses the query once, reads the scoped preview rows, and for each
plain term asks `ClipboardStore.ReadTailMatches` (`Core\ClipboardStore.Items.cs:360-384`) for ids whose
**stored body** matches. `ReadTailMatches` scans only `length(text) > 1200`, registers the same C#
`SearchService.Matches` predicate as `SearchFull`, and respects the same tab scope. `TextPasses`
combines preview-or-tail per term and applies the sign. `byTag` still runs `ClipSearch.Matches` with
an empty term list, so tags are applied once and text is not double-counted. A DB with no truncated row
skips the query entirely.

Harness on a 7-row fixture with terms at 2,400-3,000 characters:

```
PASS tail-only term is found (character 3,001)  :: 2
PASS preview and tail hits union for one term; tab rows stay out  :: 1,2,3,4,5
PASS two terms still AND (both must be present)  :: 2
PASS an excluded term is checked against the full body  :: 1,2,3,5
PASS a tab search stays inside its tab  :: 6
PASS accented term in the tail matches (same predicate, not SQL lower())  :: 7
PASS paging keeps the total and returns the asked slice  :: total=5 ids=4,3
PASS unknown tag is still searched as text and reported  :: total=0 unknown=foo
PASS text and tag filters still AND  :: total=0
PASS excluding a tail-only term drops that row
PASS no row is silently missing from a plain listing  :: main=6
```

Performance at 1,000 rows / 200 truncated / 3,000 chars:

```
PASS preview+tail term at 1k is under the 50 ms search budget  :: p50=9.3 ms p95=11.4 ms
PASS a term only in tails at 1k is under budget  :: p50=6.7 ms p95=7.4 ms
PASS a miss does not cost more than a hit  :: p50=6.6 ms p95=7.5 ms
```

That is better than the claimed 12.4 ms on this VM. `ReadPage`'s `query` branch
(`ClipHost.Queries.cs:84-91`) is still preview-only, but the page never calls `getItems` with a query
(grep: all `getItems` calls pass `tabId/offset/limit` only), so it is a latent second path, not a
release defect. **P3**: make `ReadPage` call the same tail logic or remove its query parameter.

### 1.3 Tabs - closed

Bridge (`Host\ClipHost.Tabs.cs`, 68 lines): `listTabs` returns `{tabs, pinnedTabId}`; `createTab`
returns the created row; `renameTab` refuses Pinned and taken names; `deleteTab` refuses Pinned and
returns `{deleted, deletedClips, movedToHistory}`. The store purges the tab's items and their format
rows in one transaction (`ClipboardStore.Tabs.cs:56-89`), so `movedToHistory: 0` is truthful.

UI (`1ced540`): `TabStrip.tsx` (Radix Tabs in manual activation mode), inline create/rename with the
host's error sentence and the field kept open, custom-tab context menu for rename/delete, a Radix
confirm that names the real clip count, and `MoveToTab.tsx` for the row menu and the Ctrl+M picker.
Every mutation re-reads `listTabs` and the current page. Pinned has no rename/delete affordance.
Typed search survives a tab switch (`searchTyped` ref). Delete toast reads `result.deletedClips`,
not the pre-delete count.

Harness (real `ClipHost.*Tab` statics + store):

```
PASS createTab replies id/name/kind/count  :: id=2
PASS listTabs returns Pinned first plus the custom tab, with its pinned id
PASS duplicate tab name is refused with the owner's sentence
PASS blank tab name is refused / 25-character name is refused
PASS renameTab succeeds / Pinned cannot be renamed / rename to a taken name is refused
PASS moveToTab places both rows in the tab / moved rows leave main history
PASS tab count reflects moved rows
PASS deleteTab replies the real purge count and zero rescued rows  :: deletedClips=2 movedToHistory=0
PASS deleted tab disappears / clips gone / unrelated main row byte-identical
PASS no orphan formats / integrity ok
PASS Pinned cannot be deleted / deleting an already-deleted tab is refused
PASS deleteTab counts and purges a clip with formats
```

**New P2 (UX, not data loss):** deleting a tab is permanent and the confirm says "will be deleted"
but not "cannot be undone"; there is no undo snapshot for a whole tab. The user has to confirm a
dialog, so it is not silent. Consider adding "cannot be undone" to the sentence.

### 1.4 Corruption notice - closed in code, live unobserved

`App.V2.cs:106-116` subscribes `CorruptionRecovered` -> tray toast `ShowMessage(CorruptionNotice)` and
`ShellWindow.NotifyCorruption()`; the constructor case is handled at `:115`; `ShellWindow.xaml.cs:171`
emits `corruptionRecovered` and reloads the WebView. The event has no page handler, but the reload
already replaces the stale rows, so the missing handler is cosmetic (**P3**).

### 1.5 The six visual defects (`50207bd`) - tokens verified, signature still missing

| Defect | Code now | Status |
|---|---|---|
| Settings did not fit 900x700 | `styles.css:1989-2013`: at >=840 px the dialog is 860 px and the body is a 2-column grid (5 groups, 3 rows); body scrolls with `scrollbar-gutter: stable` and a `scroll-fade` chevron that is present only while content remains. No hard-coded 668 px height. | Values present; **NOT RUN** for the eye. |
| Hover invisible | `--row-hover: #eaedf1` on `--surface:#ffffff` = **1.17:1**; dark `#212837` on `#141821` = **1.20:1**. Both match the claim. | Values verified; **NOT RUN** for the eye. |
| Index width jumped | `.row-index { width: 4ch }` (was `min-width: 2ch`); tabular numerals, right-aligned. | **NOT RUN** for the eye. |
| Dialog focus trap | `SettingsDialog.tsx` no longer calls `onOpenAutoFocus preventDefault`; Radix Content has `aria-modal="true"` and restore focus to the Settings button on close. | **NOT RUN** for the keyboard pass. |
| SVG toolbar | 6 glyph tools replaced with drawn SVGs (`IconBulletList/TaskList/Quote/Table/AddRow`); **3 text glyphs remain**: `<>` (inline code), `1.` (ordered list), `{}` (code block), plus H1/H2/H3 text buttons. Footer pluralisation fixed (`1 format`, `1 character`). | **FAIL on "one family"**: mixed glyphs remain. |
| Labelled +N chips | Tag overflow is `+N tag(s)`; character overflow is `+N chars`; the tooltip says which. | **NOT RUN** for the eye. |

The release-visual pass predates these commits, so no `[U]` row may be signed from it. This is the
largest remaining evidence gap, not a data defect.

---

## 2. Bridge completeness sweep

`UI\web\src\host.ts` -> `Host` has 21 methods (plus `mode`) after the tab additions. Every one is implemented in
`WebViewHost`; every page call site resolves to one of them. The four new methods:

| Page (`host.ts`) | Host dispatch | Params | Reply the page consumes |
|---|---|---|---|
| `listTabs()` | `ClipHost.cs` -> `ClipHost.Tabs.cs` | none | `{tabs, pinnedTabId}`, `kind` normalized |
| `createTab({name})` | same | `name` | `{id,name,kind,count}` |
| `renameTab({id,name})` | same | `id`, `name` | `{renamed:true}` |
| `deleteTab({id})` | same | `id` | `{deleted,deletedClips,movedToHistory}` |

`moveToTab` now has real callers (`App.tsx:408` row menu and Ctrl+M). DTO fields remain
`id,kind,app,title,time,pinned,tabId,text,hasImage,hasHtml,hasRtf,formats,tags,truncated,remainingChars`.

Events: the host emits `captured, edited, deleted, plainMode, themeChanged, settingsSaved, cleared,
corruptionRecovered`; `ShellWindow` emits `skipped, hotkeyFailed, cleared, themeChanged`. The page
handles every event except `edited`, `plainMode` and `corruptionRecovered`; the first is followed by a
page-side refresh, the second is cosmetic, and the third is followed by a full `Reload()` in the same
method. **No page handler listens for an event nobody emits.** `Counts.today/pinned` are still declared
and never sent, so the "N today" chip remains dead UI (pre-existing P3).

---

## 3. Data safety on copies (re-run at this HEAD)

| Test | Observed |
|---|---|
| Owner copy (38 rows / 230 formats / 1 image / 10 truncated) | Digest before/after the real store opened and migrated: **`90D6FD6D19A7DD6F` unchanged**; `integrity_check ok`; 0 orphans; Pinned tab intact. |
| 1,000 inserts + `EnforceCap(200)` | 200 main rows; Pinned and tab rows survive; **every surviving row byte-identical**; evicted set exactly the oldest; insert 117 ms, cap 2 ms. |
| 600 emoji + 1,000 ASCII (old P0) | `TextLength=1600`, preview UTF-16 1800, `IsTruncated=true`, `GetText` full, `Update(preview)` false, export 3,400 bytes. |
| Delete + restore | same id, text, 2/2 format blobs, restored on top; undo into a deleted tab lands in main history. |
| OCR | text stored, kind stays `image`, `LatestText` excludes it, `SearchFull` finds it, second pass refused, user text not overwritten. |
| Export | PNG byte-identical (227,259 B on the real row); TXT no BOM; CSV with BOM and no `sep=`. |
| Tab create + move + delete (with clips, incl. formats) | 22/22 checks; `deletedClips` equals the rows actually purged; format rows purged; 0 orphans; unrelated rows' digest unchanged; Pinned/unknown/duplicate refusals correct. |
| Ids | deleting max id 1370 then inserting gives 1371 (`AUTOINCREMENT`, never reused). |

No path was found that silently truncates, overwrites or drops a stored body. Tab deletion is explicit
and confirmed; it purges, it does not move clips back. **P0 count: 0.**

---

## 4. Checklist status (`docs\CHECKLIST-features.md`, its own 36 rows)

Strict rule: unproven = NOT RUN. `[H]` harness, `[L]` live/measured, `[U]` fresh-agent visual.

| Section | Row | Status | Evidence |
|---|---|---|---|
| 1 | Cold first popup 262-310 ms | NOT RUN (v2) | Published-exe cold/warm still unmeasured. |
| 2 | IME composition gate | NOT RUN | Guard exists (`useClipboardKeys.ts:38`); no real IME. |
| 2 | Ctrl+Up/Down time sections | **FAIL** | No sections, no chord (`useClipboardKeys.ts:108-116` is plain selection). |
| 2 | Esc cascade | **NOT RUN** (was FAIL) | Tab name, tab delete, Ctrl+M and Radix modals now exist; the ordered live sequence has not been observed. |
| 2 | [U] selection stable on capture | NOT RUN | No screenshots. |
| 3 | Live Excel/Word/Chrome/Paint matrix | NOT RUN | Never run. |
| 3 | Plain paste selected item | NOT RUN | Path exists; no live evidence. |
| 4 | [H] CF_HTML offsets, Unicode | **PASS** | 3/3 re-run at this HEAD. |
| 4 | [U] remote images never fetched | NOT RUN | Rule present (`rich.tsx:167`); no fresh shot. |
| 4 | [U] F2 editor after TipTap | NOT RUN | No fresh visual. |
| 4 | [L] F2 open ~2.9 s / +302 MB | NOT RUN | Not re-measured. |
| 4 | [U] RTF-only clips render | NOT RUN | No fresh visual. |
| 4 | [U] ceilings on screen | **FAIL** | Still only in `USER_GUIDE.md`. |
| 4 | [L] 5 MB PNG off the UI thread | **FAIL** | Capture still decodes/encodes/hashes on the dispatcher thread. |
| 4 | [L] memory discipline (thumbs only) | NOT RUN | List has no image blob; not measured. |
| 5 | Ctrl+P | NOT RUN | Store path proven; live UI not. |
| 5 | Ctrl+Shift+Left/Right adjacent tab | **FAIL** | Still absent. |
| 5 | [U] re-copy never shows twice | NOT RUN | No fresh visual. |
| 6 | [U] ghost notification preview | **FAIL** | v2 Settings has no preview. |
| 6 | [L] first-run hint | **FAIL** | `WasNew` is only read by the legacy App. |
| 6 | [L] skipped captures notice | **FAIL** | Oversized images notify; over-budget formats are dropped silently. |
| 7 | 3-line clamp / `previewLines` | NOT RUN (stale) | v2 contract is 10 lines; no WPF row. |
| 7 | [U] identity tile / facts | NOT RUN (stale) | Retired WPF anatomy. |
| 8 | Writes only its folder + `%APPDATA%\Clip` | **PASS** | Unchanged; all writers use `Config.DataDir`. |
| 8 | One-pass acceptance | NOT RUN | Not run at HEAD. |
| 8 | WPF `CanContentScroll` | NOT RUN (stale) | No WPF list. |
| 8 | WPF search debounce/swap | NOT RUN (stale) | v2 uses a 120 ms debounce. |
| 8 | Corrupt/locked DB: rename + notice | **NOT RUN** (was FAIL) | Recovery + tray toast + reload are in code; the toast was not seen on screen. |
| 8 | Acceptance at 1,000 | NOT RUN | Search 9.3 ms PASS, `GetRecent` 4 ms; scroll/cold start/RAM unmeasured. |
| 9 | [H] ClipTags >=20 + 8 controls | **PASS** | 28/28 re-run at this HEAD. |
| 9 | [U] row = index 01 / 10 lines / 430 px | NOT RUN | No fresh visual. |
| 9 | [L] digit 1-9 copies visible index | NOT RUN | Code path present; live unproven. |
| 9 | [U] 1/10/30-line + tooltip | NOT RUN | No fresh visual. |
| 9 | [L] mixed-height virtualizer at 1,000 | NOT RUN | Not measured. |
| 9 | [L] toast auto-fit at 150 % DPI | NOT RUN | Native toast not re-measured. |
| 9 | [L] web keyboard matrix + Esc cascade | NOT RUN | Ctrl+M exists now; no post-fix live pass. |

Totals: **3 PASS - 7 FAIL - 26 NOT RUN** (was 3 - 9 - 24). Two rows changed status:
**Esc cascade FAIL -> NOT RUN**, **corrupt-DB notice FAIL -> NOT RUN**. No row moved to PASS because
`[L]`/`[U]` evidence is still absent. The checklist has **no row** for full-body search, export shell
restore, tab CRUD or corruption toast; those should be added before the next gate.

### `docs\CHECKLIST-uiux.md` - rows whose cause changed since the last pass

The file (rewritten `02e23cd`) was written against the visual pass at `aba6c71`. `50207bd` and
`1ced540` changed the code under these rows, so their signed status is stale:

| Row | Checklist says | Now | Why |
|---|---|---|---|
| G-01 popup list | FAIL | **NOT RUN** | Index 4ch and hover tokens fixed; needs a fresh screenshot. |
| G-03 editor chrome | FAIL | **FAIL** | Footer plural fixed; 3 text glyphs remain in the toolbar. |
| G-04 settings | FAIL | **NOT RUN** | 860 px 2-column layout; needs a fresh screenshot. |
| R-02 index width | FAIL | **NOT RUN** | `width: 4ch`. |
| R-08 `+N` chips | FAIL | **NOT RUN** | Chips now read `+N tag(s)` / `+N chars`. |
| C-04 hover | FAIL | **NOT RUN** | 1.17:1 / 1.20:1, verified by calculation. |
| W-03 settings fits | FAIL | **NOT RUN** | Layout changed; unrendered. |
| W-04 430 settings cue | FAIL | **NOT RUN** | `scroll-fade` exists; unrendered. |
| K-09 settings focus trap | FAIL | **NOT RUN** | `onOpenAutoFocus` prevention removed. |
| S-08 dead disabled Edit | FAIL | **NOT RUN** | Edit renders only when a row is selected. |
| E-04 character count | FAIL | **FAIL** | Memo deps still `[editor, dirty, loading]`; `dirty` stops changing. |
| E-05 toolbar/footer | FAIL | **FAIL** | Partial only (see G-03). |
| S-05 export toast | PASS | **PASS** | The note "post-export visibility is a host defect" is now stale; the defect is closed. |
| G-02 chrome | PASS | **PASS** | Header still repeats `1,000` four times (non-structural). |

**Strict protocol:** every `[U]` row signed at `aba6c71` must be re-signed after `50207bd`/`1ced540`
(ClipList, styles and App changed). The release-visual pass cannot sign the current page.

Rows that remain NOT RUN **because UI automation is blocked** (not because of a code defect):
G-06 native toast/tray, W-02 125 % zoom, W-05 toast/export at 430, K-05 Ctrl+E, K-06 typing guard,
K-07 IME, E-01 editor trap, E-03 edit invalidates body, and every `[U]` row in sections 0-9. The new
tab strip (create/rename/delete/move) has **no uiux checklist row**; it needs one.

---

## 5. Release hygiene

| Check | Result |
|---|---|
| Build | `dotnet build -c Release` -> **0 errors, 0 warnings** (1.8 s). `tsc --noEmit` -> **0**. |
| TODO/FIXME/stubs/`[DEBUG]`/`NotImplementedException` | **0** in C#; no `console.*`/TODO in `UI\web\src`. |
| `Console.WriteLine` / `Debug.WriteLine` | **0**. |
| Empty catches | **28**, every one carries a one-line reason (listed in the transcript). |
| Hardcoded UI colours | UI tokens all in `styles.css:2-84`; 6 document-palette colours in `EditorToolbar.tsx:165` and the `#010203` probe in `rich.tsx:45` are content/probe values, not UI. Pre-existing WPF fixture colours remain in `SettingsWindow.xaml`. |
| File budget (400 lines) | **11 over:** `App.tsx` 1023, `styles.css` 2028, `MainWindow.xaml.cs` 539, `SettingsWindow.xaml` 506, `ClipboardStore.Items.cs` 497, `SettingsWindow.xaml.cs` 440, `HtmlToRtf.cs` 424, `ClipHost.Queries.cs` 422, `RichRow.cs` 421, `ClipHost.Actions.cs` 405, `host.ts` 405. The three web files grew with S12/S13. Debt, not a defect. |
| Dev probes | `CLIP_V2_TIMING` / `CLIP_V2_DEBUG_PORT` are inside `#if DEBUG`; a Release build has no CDP path. |
| `dist` contents | Only `Clip.exe` (208,183,750 B), untracked and ignored. |
| Artifact matches sources | Bundle `UI\web\dist\index.html` 2,856,860 B at 06:51:55; last web source 06:49:29; exe 06:52:36. Exe contains `Right-click a clip and move it here`, `corruptionRecovered`, `listTabs/createTab/renameTab/Delete tab`, `Ctrl+M`, and the S11 raster rule (`data:image/`, `jpe?g`, `webp`). |
| Working tree | Clean except the six pre-existing untracked `docs\AUDIT-*.md` reports (this pass adds a seventh). |
| EOL | New `Host\ClipHost.Tabs.cs` is CRLF; new `TabStrip.tsx`/`MoveToTab.tsx` are LF like the other web sources; `ClipHost.Queries.cs` remains LF-only (pre-existing P3). |

---

## 6. Delta table (previous finding -> now)

| # | Previous finding | Status | Note |
|---|---|---|---|
| F1 | Export's Save dialog hid the window; toast invisible (P1) | **CLOSED** | Gate + `show()` on save/cancel/failure; harness `ask -> show`. |
| F2 | Search could not see past 1,200 characters (P2) | **CLOSED** | Tail ids per term; AND/exclusion/tab/accents/paging proven; 9.3 ms p50. |
| F3 | No tab management; `moveToTab` callerless (P2) | **CLOSED** | Bridge + strip + move + Ctrl+M; 22/22. |
| F4 | Corruption recovered silently (P2) | **CLOSED in code** | Tray toast + event + reload; live toast unobserved. |
| F5 | Six visual release defects | **5 CLOSED in code, 1 PARTIAL** | Toolbar still mixes 3 glyphs; all six need a fresh signature. |
| F6 | Release gate not met: checklists, fresh visuals, memory | **OPEN** | 3/7/26; no post-fix `[U]`; 528/643 MB vs the 250 MB ceiling recorded earlier. |
| F7 | `_deleted` keeps every deleted clip's blobs for the session (P2) | **OPEN** | Unchanged; one-level undo, no eviction. |
| F8 | Keyboard rows: no Ctrl+Up/Down, no Ctrl+Shift+Left/Right (P2) | **PARTIAL** | Ctrl+M and tab edit/delete now exist; the two chords are still absent (both FAIL rows). |
| F9 | File-size budget and WPF fixture colours (P2) | **WORSE / OPEN** | 11 files over 400 (was 6). |
| P3 | `Config.Load` does not clamp `MaxItems` | OPEN | A hand-edited 0 still empties history on cap. |
| P3 | Duplicate theme watchers | OPEN | Same resolver, duplicate work. |
| P3 | `mock.ts` second search/tag grammar | OPEN | Dev preview only; never in WebView2. |
| P3 | Tag chips mouse-only | OPEN | `tabIndex={-1}` inside `role="option"`. |
| P3 | Character count freezes after first edit | OPEN | `EditorOverlay.tsx:157`. |
| P3 | Settings opened with focus on the trigger | **CLOSED** | `onOpenAutoFocus` prevention removed; focus returns to the button. |
| P3 | Dead "N today" chip | OPEN | Host never sends `counts.today`. |
| P3 | Over-budget clipboard formats dropped silently | OPEN | Only images raise `Skipped`. |
| P3 | Pin from All reselects a missing id | OPEN | Cosmetic. |
| P3 | LF-only `ClipHost.Queries.cs`, untracked audits | OPEN | Docs/evidence trail. |
| N1 | `corruptionRecovered` has no page handler | P3 | `Reload()` covers the stale rows. |
| N2 | Tab delete is permanent; confirm omits "cannot be undone" | P2 UX | Confirmed action, counted purge, no silent loss. |
| N3 | `ReadPage(query)` is still preview-only | P3 latent | No page caller passes a query today. |
| N4 | `moveToTab` to a stale tab silently no-ops but toasts "Moved" | P3 | Single instance makes it unlikely. |

---

## 7. One-screen verdict

# SHIP WITH NAMED LIMITS - as an owner preview, not as the signed release.

The binary is **data-safe**: no P0, the real 38-row database migrates losslessly, the cap evicts exactly
and preserves every survivor byte-for-byte, delete/restore keeps ids and blobs, OCR cannot overwrite an
edit, exports are byte-exact, tab deletion purges exactly what it says, and integrity is `ok` throughout.
The four fixes are real, and the export/search/tab work is proven by the real code, not a mock.

**Named limits the owner can read:**

1. **The visual fixes have not been looked at by an independent pair of eyes.** The settings fit,
   hover shade, index width, focus trap and chip labels are in the code, but the screenshots that
   signed the last visual pass were taken before these fixes. Treat the look as "changed, not signed".
2. **The editor toolbar is still a mix.** Most icons are now drawn, but inline code, numbered list and
   code block still show `<>`, `1.` and `{}`, and the headings are text.
3. **Two keyboard shortcuts are still missing:** Ctrl+Up/Down for time sections and
   Ctrl+Shift+Left/Right to move a tab. Moving a clip to a tab works through the right-click menu and
   Ctrl+M.
4. **Nothing about live use was re-run:** no real clipboard copy/paste, no Excel/Word/Chrome/Paint
   round-trip, no real IME, no 150 % DPI or second monitor, no first-run and no reboot test. All the
   `[U]` and most `[L]` rows are still unproven.
5. **Memory is about 528 MB idle and 643 MB with an image**, against the 250 MB figure recorded before.
   That is the WebView2 price and it needs the owner's written acceptance, or the ceiling must change.
6. **The first-run hint, the ghost notification preview, the on-screen known-limits note, and a notice
   for over-budget clipboard formats are still absent.** The app will not tell a new user it is running,
   will not warn that a very large non-image copy was dropped, and will not show its own ceilings.
7. **Deleting a tab permanently deletes every clip in it** after a confirm that names the count. The
   confirm does not say "cannot be undone"; there is no tab-level undo.
8. **The editor character count freezes after the first keystroke**, and the settings' disabled
   explanation is still below 4.5:1 contrast. Small, visible, known.
9. **Eleven files exceed the 400-line budget** (App.tsx 1023, styles.css 2028, five host/store files
   around 405-497). It builds and runs, but maintenance is harder until they are split.

**What blocks the formal release signature (not the preview):** the two checklists still read
3 PASS / 7 FAIL / 26 NOT RUN; every `[U]` row is unsigned after the last code changes; the memory
ceiling is unaccepted; and no live acceptance pass exists. Until those are closed, this is a preview
the owner can use, not a release this project can sign.

**Evidence index.** Harness: `%TEMP%\opencode\audit-release-2\core\out\ReleaseAudit.exe` (commands
`owner`, `fresh`, `stress`, `tags`, `cfhtml`, `perf`) and
`%TEMP%\opencode\audit-release-2\build\BridgeAudit.exe` (commands `search`, `export`, `tabs`, `perf`,
`all`), both rebuilt/run at `bae76ad` (code head `1ced540`). Databases: `core\owner.db` (copy),
`owner-stress.db`, `fresh.db`, `bridge\search.db`, `export.db`, `tabs.db`, `perf.db`. Build:
`dotnet build -c Release` (0/0), `tsc --noEmit` (0). No product code was changed; the live database
was never opened.
