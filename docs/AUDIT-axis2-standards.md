# AUDIT — Axis 2: Engineering Standards & Data Integrity

**Run:** 23 Nov 2026 · pre-release gate (`ca-milestone-audit` Step 3 + `ca-autonomous-builder` mechanical stages)
**Auditor:** fresh AXIS-2 sub-agent (never wrote any Clip code before this run)
**Baseline:** git `2a45392` ("Baseline checkpoint"). Contract sources read first: `AGENTS.md`, `CURRENT_STATE.md`, `AI_LEDGER.md`, `docs\CHECKLIST-features.md`.

**Scope:** compile gate, hygiene, file sizes, duplication, layering, colours, stale refs, EOL, SQLite schema/transaction/index review.
**Out of scope by mandate:** every file in `UI\` and `_fonttest\` (findings only, S3 owns the UI rebuild), and all business semantics — dedupe, cap, tabs, pinning, skip rules — which get findings, never edits.

## Verdict

| Axis | Result |
|---|---|
| Compile gate | **GREEN** — 0 errors, 0 warnings (forced full rebuild) |
| Hygiene (stubs/debug) | **PASS** — zero TODO/FIXME/stub/[DEBUG]/Console/Debug.WriteLine |
| Empty `catch` without a reason | 1 member-spec deviation found in `Core\Config.cs` → **fixed**; 1 left in `App.xaml.cs` (reported) |
| Duplication (12-line hash) | **PASS** — no pasted windows |
| Duplication (semantic) | 1 real breach: the clipboard-busy retry lived in **3** places → **collapsed to 1 home** |
| Layering | **PASS** — `using Clip.UI` in Core/Interop: zero |
| Colours outside `UI\Themes` | 5 hardcoded values (1 known-accepted, 3 shadows, 1 tray icon) — reported |
| Stale refs in `docs\` | 6 stale API/line claims — reported |
| EOL / encoding | 1 mixed-EOL file (`Clip.csproj`) — reported, no mass flip |
| DB integrity | Schema + migrations + orphan sweeps sound; 2 real gaps (legacy `pinned` re-adoption, no open-failure path) + 1 perf item |

---

## Findings

Severity: **P0** build/data broken now · **P1** will break a shipped feature · **P2** real defect at low frequency or scale · **P3** standards debt.

### P0

**A2-01 — `Config.PreviewLines` was pinned but never landed (build RED)** — **FIXED**
- *Location:* `Core\Config.cs`; consumers `UI\SettingsWindow.xaml.cs:34,229,235`.
- *Impact:* `dotnet build -c Release` failed with 2 × CS1061. The app could not be built at all.
- *Fix:* `public int PreviewLines { get; set; } = 10;` after `MaxItems`; the existing camelCase policy serialises it as `"previewLines"`. No other compile error existed.

### P1

**A2-02 — Clipboard-busy retry existed in 3 copies** — **FIXED**
- *Location:* `Interop\PasteInjector.SetClipboardRetry` (1 definition + 5 call sites), `Interop\ClipboardFormats.Restore` (inline loop), `Interop\ClipboardFormats.OpenWithRetry` (same policy, different shape).
- *Impact:* the documented policy ("5 attempts / 30 ms") could drift in one copy and nobody would notice; a fix applied to one path would silently miss paste, restore or snapshot.
- *Fix:* one home `ClipboardFormats.WithClipboardRetry(Action)` (already owns raw clipboard access and the read-side retry); `Restore` and all 5 `PasteInjector` sites call it; `PasteInjector.SetClipboardRetry` deleted. Behaviour is byte-identical (same 5 × 30 ms, same silent give-up). `AI_LEDGER.md` owners table updated — the retry was listed under `PasteInjector`, it now lives with `ClipboardFormats`.
- *Deliberate exception:* `OpenWithRetry` stays separate — it retries a `bool`-returning Win32 `OpenClipboard`, not a throwing write, so forcing it through the same `Action` helper would need a fake exception. One-line reason recorded here instead.

**A2-03 — `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` is never called**
- *Location:* `App.xaml.cs::OnStartup` — no occurrence anywhere in the repo.
- *Impact:* `CHECKLIST-features.md §13` (verified in the spike) says the **first RTF fallback render throws `TypeInitializationException`** without it. The RTF preview path is `⬜`/not wired yet, so nothing breaks today — it breaks the moment S3 wires RtfPipe.
- *Recommendation:* one line in `OnStartup` before any UI work. `System.Text.Encoding.CodePages` needs **no** `PackageReference` on net10 (it is in the shared framework; `project.assets.json` already blocks a package pin `(,10.0.32767]`). Do **not** add the package.
- *Status:* planned — S3 (composition root is S3's file; the checklist already owns this item).

**A2-04 — Legacy `pinned = 1` is never retired, so a re-unpinned item re-pins itself**
- *Location:* `Core\ClipboardStore.Migrate()` line 69: `UPDATE items SET tab_id = … WHERE pinned = 1 AND tab_id IS NULL`.
- *Impact:* the migration adopts old pinned rows into the Pinned tab but leaves `pinned = 1`. `TogglePin` / `MoveItemToTab(id, null)` clear `tab_id`, not `pinned` — so the next app start **re-adopts** the row into Pinned. `CHECKLIST-features.md §7` says the migration "adopts existing `pinned=1` rows into Pinned **and retires the flag**"; the flag survives. Only reachable for rows that came from a pre-tabs DB, and only after S3 ships Ctrl+P — user-visible pin resurrection.
- *Recommendation:* `UPDATE items SET tab_id = (…), pinned = 0 WHERE pinned = 1 AND tab_id IS NULL`. Re-run stays a no-op.
- *Status:* **not fixed** — this writes to pinning state, which is business semantics (off-limits to this axis). Pinning sprint / S3. One-line change.

### P2

**A2-05 — A corrupt or locked `clip.db` kills the app at startup with no recovery**
- *Location:* `Core\ClipboardStore..ctor` (`_conn.Open()` + `Migrate()`); called unguarded from `App.OnStartup`.
- *Impact:* SQLite `SQLITE_CORRUPT`/`SQLITE_NOTADB`/lock-throw propagates out of `OnStartup` → the process dies before the tray exists. A tray app that dies silently on a bad DB looks like "it just doesn't start", and the only fix is a manual `%APPDATA%` delete. Migrate's `ALTER` guards only swallow `"duplicate column name"`.
- *Recommendation:* in `App.OnStartup`, catch `SqliteException` once: rename `clip.db` → `clip.db.corrupt-<timestamp>`, recreate, carry on (history is a cache; losing it is acceptable, never starting is not).
- *Status:* planned — S3 composition root.

**A2-06 — Image dedupe compares whole BLOBs with no possible index**
- *Location:* `Core\ClipboardStore.TryDedupeMoveToTop` — `WHERE kind = 'image' AND tab_id IS NULL AND image = $v`.
- *Impact:* BLOB equality cannot use an index, so every image copy reads and compares every stored image blob: worst case ≈ 200 × 5 MB ≈ **1 GB of page reads on one copy**. Text dedupe is fine (see P3 index note). At the 200-item default cap this is measurable but masked by the 100 ms debounce; at the planned 1000-item scale it will be felt.
- *Recommendation:* add `image_hash TEXT` (SHA-256 of the PNG, hex) at capture + `CREATE INDEX IF NOT EXISTS ix_image_hash ON items(image_hash)`; dedupe on `image_hash = $h AND image = $v` — the hash is a pre-filter only, the byte compare still decides, so exact-byte semantics are untouched.
- *Status:* planned — fix sprint (schema change + capture wiring).

**A2-07 — Write paths that must be atomic are not wrapped as one unit**
- *Location:* `CaptureService.Store` → `Insert` then `InsertFormats`; `ClipboardStore.TryDedupeMoveToTop` → `UPDATE`+`DELETE item_formats` then `InsertFormats`.
- *Impact:* a crash/disk-full between the halves leaves an item with zero format blobs. Text still pastes (falls back to text/rich), so nothing is corrupted and no wrong data can appear — but rich/exact fidelity for that row is gone permanently, and in the dedupe path it is gone on the **surviving** row. `Delete`, `Clear`, `EnforceCap`, `Update` can leave orphan format rows mid-crash; those are swept (startup + every mutation), so they self-heal.
- *Status:* planned — recommendation in the section below.

### P3

**A2-08 — Empty `catch` without a reason (`AGENTS.md` standing rule)**
- `Core\Config.cs:82` `catch { }` → **fixed** (comment added: unreadable/corrupt config → fresh defaults, overwritten by `Save()`).
- `App.xaml.cs:125` `try { _mutex.ReleaseMutex(); } catch { }` → **reported only** (composition root, not Core/Interop). One-line comment needed: "process may not own the mutex."
- All other 4 empty catches in Core already carry a reason (`CaptureService.cs:147,164`, `ImageCache.cs:54,59`).

**A2-09 — Hardcoded colours outside `UI\Themes\*.xaml`** *(findings only — S3 re-skins and lands sage/amber/sky/violet)*

| Location | Value | Note |
|---|---|---|
| `UI\SettingsWindow.xaml:10` | `#FF6B6B` | known accepted exception (error red), owner-pending token — **still pending** |
| `UI\MainWindow.xaml:29`, `UI\NotificationWindow.xaml:13`, `UI\SettingsWindow.xaml:191` | `#000000` | drop-shadow colours; shadows have no theme token yet |
| `App.xaml.cs:108,109,111` | `Color.FromArgb(8,9,12)` / `(255,138,61)` | **tray icon glyph colours duplicate `Dark.xaml` App + Accent by hand** — if the theme moves, the icon silently keeps the old palette. A runtime `Bitmap` cannot bind a `SolidColorBrush`; derive from `Resources["App"]`/`["Accent"]` at icon-build time instead. |

**A2-10 — PNG→`BitmapSource` decode duplicated 3×**
- `Core\CaptureService.DecodePng`, `Interop\PasteInjector.ToBitmapSource` (identical 9 lines: `MemoryStream` → `BitmapImage`, `OnLoad`, `Freeze`), `UI\NotificationWindow.xaml.cs:113-121` (inline).
- *Recommendation:* one helper (e.g. `Core\Png.Decode`) at S3, when the third caller is being touched anyway. Not done now: fixing 2 of 3 leaves a half-refactor and the remaining site is S3's file.

**A2-11 — Monitor-placement math duplicated across two UI windows**
- `UI\MainWindow.ShowPopup` and `UI\NotificationWindow.ShowFor` both do `Screen.FromPoint(Cursor) → ScreenDpi.Scale → Left/Top` switch. S3 replaces the popup rule with the active-window monitor anyway; one shared `ScreenDpi.Place(…)` helper should land then.

**A2-12 — `captured_at` format string written in 5 places + 1 SQL literal**
- `"yyyy-MM-ddTHH:mm:ss"` in `CaptureService` ×3 (`:76,:97,:221`), `ClipboardStore.Update:153`, `ClipboardStore.CreateTab:318`; `Migrate` uses the equivalent `strftime('%Y-%m-%dT%H:%M:%S','now','localtime')`. Ordering is **lexical**, so if one site ever emits a different shape (`f` fractions, `Z`, invariant vs not) newest-first silently breaks for those rows.
- *Recommendation:* one `internal static string Stamp.Now` const + formatter used by all 5 sites (SQL literal stays, with a comment). Two-file change, zero behaviour change — worth a small slice, not worth widening this audit's diff.

**A2-13 — `Migrate` detects "already migrated" by matching an English message string**
- `catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))`. Works today (SQLite core messages are stable English) but is locale/version-fragile and would silently rethrow on a reworded message.
- *Recommendation:* guard with `PRAGMA table_info(items)` and only ALTER the missing columns.

**A2-14 — No index on `kind`; Images view can scan the whole table**
- `GetRecent(max, "image")` = `WHERE tab_id IS NULL AND kind = 'image' ORDER BY captured_at DESC, id DESC`. With `ix_captured` SQLite walks newest-first and filters `kind` per row — cheap at 200 rows even with few images, degrades at 1000.
- *Recommendation:* `CREATE INDEX IF NOT EXISTS ix_kind_captured ON items(kind, captured_at DESC)`; and if the 1000-item contract lands, `ix_main_captured ON items(tab_id, captured_at DESC, id DESC)` for the main-history query. Text dedupe deliberately has **no** index (text can be multi-MB; the 200-row scan is sub-millisecond) — recorded as correct, not a gap.

**A2-15 — File budget (400 lines)**
- `Core\ClipboardStore.cs` **429** lines, `UI\SettingsWindow.xaml` **434** lines. Split proposal below. Not restructured mid-audit (explicitly forbidden by the run brief).

**A2-16 — EOL: 1 file mixes CRLF and LF**
- `Clip.csproj` — 5 CRLF line ends (lines 25-29, the append-at-the-end `PackageReference` block) among 27 LF. Every other file in the repo is uniformly LF, no BOM anywhere. Report only; no mass flip.

**A2-17 — Stale refs in `docs\`** (documents are frozen contracts/research — reported, not edited)

| Doc | Stale claim |
|---|---|
| `docs\RESEARCH-image-best-practices.md:21,71,92,398` | describes `Clipboard.SetImage` / `SetImage(BitmapSource)` as the current paste path — **removed**; paste is `BuildDib` + PNG (`CHECKLIST §6`). Reader risk: S3 "restores" a deleted API. |
| `docs\RESEARCH-image-best-practices.md:342` | `ImageCache.Materialize(item.Id, …)` — real API is `ImageCache.Write(id, png)`. |
| `docs\RESEARCH-logic-best-practices.md:104` | cites `PasteInjector.cs:62-73` for the 5× retry — that block now lives at `ClipboardFormats.WithClipboardRetry`. |
| `docs\RESEARCH-logic-best-practices.md:79,255` | "`ClipboardFormats.Restore` … is never called", "`GetFormats` has zero callers" — still true, expected pre-S3. |
| `docs\DESIGN-typography.md:323` | "the property is missing from `Config.cs` today — S3 must land it" — **landed** (A2-01). |
| `docs\DESIGN-typography.md:316,323` | "`clamp(PreviewLines, 1, 6)` (default 3)" vs the pinned contract's `= 10` — see A2-18. |

**A2-18 — `PreviewLines` default conflict between contract and design doc**
- Contract (`CHECKLIST §10`, pinned by S2's built UI): property default **10**. Design (`DESIGN-typography.md:316,323`): default **3**, clamp at render to **6**.
- *Impact:* if S3 reads the design doc it will change the default the owner's contract pins; the card would shrink on upgrade.
- *Decision for S3:* keep **10** as the stored default (landed, contract wins), apply the **6** as a *render-time* clamp only. High value / zero code: one line in the design doc pointing at `Config.PreviewLines = 10`.

---

## Part B stage log (what each mechanical stage actually observed)

1. **Hygiene** — scanned 24 source files (`*.cs`, `*.xaml`, `.csproj`; `bin/obj/_fonttest/docs` excluded): `TODO` 0, `FIXME` 0, `not implemented` 0, `[DEBUG]` 0, `Console.WriteLine` 0, `Debug.WriteLine` 0, `HACK` 0, `XXX` 0. Empty `catch` blocks found: 6 — 4 already carried a reason (`CaptureService.cs:147,164`, `ImageCache.cs:54,59`), 1 in `Config.cs` **fixed**, 1 reported (`App.xaml.cs:125`). No stub returns, no `#if DEBUG` blocks.
2. **File sizes** — 24 files. Over 400: `UI\SettingsWindow.xaml` 434, `Core\ClipboardStore.cs` 429. Next largest: `Core\CaptureService.cs` 314, `UI\SettingsWindow.xaml.cs` 283, `Interop\PasteInjector.cs` 218 (after the retry removal), `Interop\ClipboardFormats.cs` 212. Full numbers reproducible with `Get-Content | Measure-Object`.
3. **Duplication — mechanical** — SHA-256 over every whitespace-normalised 12-line window that contains ≥8 non-blank, non-brace lines, across all `*.cs`: **no window appears at 2+ sites**. Script: `%TEMP%\opencode\dupe-scan.ps1`.
   **Duplication — semantic** — the known suspects, each resolved:
   - clipboard-busy retry: 3 copies → **1 home** (A2-02). 
   - PNG decode: 3 copies → recorded (A2-10).
   - PNG encode / thumbnail: single home (`CaptureService.EncodePng` / `Thumbnail`), no copies.
   - retry/delay patterns in `App.xaml.cs`: only one `DispatcherTimer` (the 100 ms focus hand-over); no other retry loop in App.
   - monitor placement: 2 copies in UI (A2-11).
   - `Meta`/clock formatting: single home (`ClipboardItem.Meta`).
4. **Layering** — `using Clip.UI` (or `Clip.UI.*`) in `Core\` or `Interop\`: **0 matches**. Cross-namespace imports are exactly the documented deviation and nothing more: `Core\CaptureService.cs → using Clip.Interop` and `Interop\PasteInjector.cs → using Clip.Core`. `Interop` contains no business semantics (no cap/dedupe/tab rules; `PasteInjector` consumes `ClipboardItem` and `ClipboardFormats` only). `App` imports everything as designed. Verified UI does not re-implement business rules: `MainWindow.Reload` calls `_store.GetRecent(_cfg.MaxItems)`, `Delete` calls `_store.Delete`, filtering calls `SearchService.Filter` — all delegated.
5. **Colours** — 18 hex literals total: 14 inside `UI\Themes\Dark|Light.xaml` (correct), 4 outside (A2-09), plus 3 hardcoded `Color.FromArgb` values in `App.xaml.cs` (tray icon). Note `MainWindow.xaml`'s previously known `#2A2E3A` / `#333849` are **gone** (already tokenised as `Hairline`/`Raised`); the known SettingsWindow `#FF6B6B` is still there.
6. **Stale refs** — A2-17. Call-plan gaps (dead code S3 is about to wire — **not** to be "cleaned up"): `ClipboardStore.GetFormats` (0 callers), `ClipboardItem.Formats` (written by capture, never read at paste), `ImageCache.Write/Cleanup` (0 callers), `Config.PreviewLines` (Settings only), `CaptureService.Skipped` (no subscriber), `PasteInjector.CopyAndPaste(ClipboardItem)` (0 callers — App pastes `item.Text` only), `ClipboardItem.ClockFormat` (never set, defaults "HH:mm"), `Config.TimeFormat` (consumed by nothing yet).
7. **EOL/encoding** — A2-16. No UTF-8 BOMs, no UTF-16 files, no trailing-CRLF anomalies.
8. **DB review** — next section.

---

## DB integrity review (`Core\ClipboardStore.cs`, read end to end)

**Migrations — sound and idempotent.** `rtf` and `tab_id` are added in guarded `ALTER`s that swallow the only expected failure; the Pinned seed / adoption / orphan sweep block is re-runnable by construction (`CREATE INDEX IF NOT EXISTS`, `INSERT … WHERE NOT EXISTS`, `UPDATE … WHERE tab_id IS NULL`, `DELETE … NOT IN`). Re-running on a migrated DB is a no-op; the `PinnedTabId` lookup that follows throws a clear message rather than limping on if the seed is somehow missing. Only nit: message-string detection (A2-13), and the whole `Migrate` is not one transaction — harmless because every step is idempotent, so a crash mid-migration self-heals on the next start.

**Orphan format rows — actually impossible to leak.** Verified per path: `Delete` purges formats in the same command as the row; `Clear` and `EnforceCap` are `tab_id IS NULL`-scoped and follow with a global `NOT IN (SELECT id FROM items)` sweep; `DeleteTab` purges formats → items → tab → sweep, all inside **one transaction** (the only fully-wrapped path); `Update` and the dedupe path drop formats explicitly so stale blobs can never be pasted back; startup runs the same sweep. No `FOREIGN KEY` is declared and `PRAGMA foreign_keys` is off — the sweeps are the mechanism, and they are sufficient because every row-creating path writes formats in the same call.

**Index coverage against the hot queries.**

| Query | Plan today | Verdict |
|---|---|---|
| `GetRecent(max)` — `tab_id IS NULL ORDER BY captured_at DESC, id DESC` | `ix_captured` walked newest-first, `tab_id` filtered per row | fine at 200; add `(tab_id, captured_at DESC, id DESC)` if the 1000-item contract lands |
| `GetRecent(max, "image")` | same walk + `kind` filter → full walk when images are rare | **A2-14** |
| `GetTabItems` — `tab_id = $t ORDER BY captured_at DESC` | `ix_items_tab` then sort | fine (tabs are small) |
| dedupe text — `kind = $k AND tab_id IS NULL AND text = $v` | full scan of items | fine and deliberate — an index on multi-MB text would be worse (A2-14 note) |
| dedupe image — `image = $v` | full scan, **reads every image blob** | **A2-06**, the one worth fixing |
| `ListTabs` counts, format purge sweeps | `ix_items_tab`, `ix_fmt_item` | fine |

**Transaction recommendation (one paragraph).** A transaction wrapper is **warranted, but at S3/fix-sprint, not as a stop-the-line change** — and it should be done at the *store's own boundary*, not by making callers manage transactions. The failure mode today is narrowly bounded: Microsoft.Data.Sqlite autocommits each statement, so no blob can be torn and no wrong data can appear; a crash between `Insert` and `InsertFormats`, or between the dedupe `DELETE` and its `InsertFormats`, leaves a row whose exact formats are gone and whose paste degrades to text/rich. That is *data loss of fidelity on one clip*, accepted today because the app is single-process and single-threaded (no reader can observe the gap) and because `InsertFormats` already opens its own transaction, which is precisely why it cannot currently join a wider one. The smallest correct fix is a new store method `InsertWithFormats(ClipboardItem)` (one `BEGIN` … `COMMIT` covering the row plus its blobs, formats written by the same command loop) plus making the dedupe path open a transaction from the `UPDATE` through the format re-insert, i.e. an internal `InsertFormats(id, formats, SqliteTransaction tx = null)` overload. That is ~30 lines in one file with no call-site churn beyond `CaptureService.Store`, and it removes the last way a user can permanently lose a clip's fidelity. Do it in the same slice as the `ClipboardStore` split (A2-15) to avoid touching the file twice.

## `ClipboardStore` split proposal (429 → 3 files, zero call-site change)

`partial class ClipboardStore` in `Clip.Core`, split by **feature**, not by layer:

1. `Core\ClipboardStore.cs` — connection, schema DDL, `Migrate`, `PinnedTabId`, `Dispose`, and the three tab-name helpers ≈ **135 lines**.
2. `Core\ClipboardStore.Items.cs` — `Insert`, `InsertFormats`, `GetFormats`, `GetRecent`/`Query`, `GetImage`, `Update`, `TryDedupeMoveToTop`, `Delete`, `Clear`, `EnforceCap` ≈ **205 lines**.
3. `Core\ClipboardStore.Tabs.cs` — `ListTabs`, `CreateTab`, `RenameTab`, `DeleteTab`, `MoveItemToTab`, `TogglePin` ≈ **90 lines**.

No public signature changes, no interface, no new abstraction — just three files a reader can hold in their head. Do it in the fix sprint, together with the transaction work (A2-07), not mid-audit.

---

## Self-fixed in this run

| # | File | Change | Risk |
|---|---|---|---|
| 1 | `Core\Config.cs` | `PreviewLines` property (`= 10`, JSON `previewLines`) — unblocks the build (A2-01) | none; contract-specified |
| 2 | `Core\Config.cs` | reason comment on the corrupt-config `catch` (A2-08) | none, comment only |
| 3 | `Interop\ClipboardFormats.cs` | added `WithClipboardRetry(Action)`, `Restore` now uses it (A2-02) | none, identical policy |
| 4 | `Interop\PasteInjector.cs` | deleted the duplicate helper, 5 call sites now delegate (A2-02); dropped the `using System.Threading;` that only it needed | none, identical policy |
| 5 | `AI_LEDGER.md` | owners table: retry home moved to `ClipboardFormats.WithClipboardRetry` | doc only |

Nothing in `UI\`, `_fonttest\`, and no dedupe/cap/tab/pin rule was touched.

## Not fixed — and why

- **A2-03** startup `Encoding.RegisterProvider` — `App.xaml.cs` is S3's composition root; adding it now would be an unverified S3 item anyway (needs `[L]` evidence).
- **A2-04** legacy `pinned` retirement — writes pin state = business semantics, off-limits to this axis.
- **A2-05** DB open-failure recovery — new behaviour in `App`, not a compile blocker.
- **A2-06** image-hash dedupe — schema change; needs capture wiring and a re-run of the dedupe harness (`41/41` currently green).
- **A2-07** transaction wrapper — recommended above, sized for the split slice.
- **A2-08** `App.xaml.cs:125` comment — composition root, outside this axis's edit area.
- **A2-09..A2-12, A2-17** UI/docs hygiene — S3's rebuild and the doc owners; editing frozen research docs mid-audit would erase the record they exist to keep.
- **A2-13, A2-14, A2-16** — low risk today; a PRAGMA guard, two indexes and a one-off EOL normalisation are fix-sprint work, not blockers, and a mass EOL flip would bury the real diff.
- **A2-15** `ClipboardStore` split — explicitly forbidden mid-audit by the run brief; proposal above.

## Carry-over to S3 (call-plan gaps, one line each)

- `PreviewLines` is stored but unused — bind the selected card's line count and clamp at render to 6 (A2-18).
- `GetFormats`/`item.Formats` have no reader at paste time — App pastes `item.Text`; wire `PasteInjector.CopyAndPaste(item)`.
- `ImageCache.Write`/`Cleanup` have no callers — wire F2-open and startup cleanup.
- `CaptureService.Skipped` has no subscriber — wire the toast.
- `ClipboardItem.ClockFormat` and `Config.TimeFormat` are never set/read — set `ClockFormat` from the theme/config at startup.
- Known S3 must-fixes still present in code (already on the checklists, repeated here so they are not lost): new `NotificationWindow` per capture (`App.OnCaptured`), `HotKeyWindow` `Enum.TryParse<Keys>` friendly-name parsing, tray "Settings" disabled placeholder, popup placement still cursor-based instead of active-window.

## Gate evidence

```
$ dotnet build -c Release -t:Rebuild -p:OutDir=%TEMP%\opencode\clip-gate\
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.46
```

`-t:Rebuild` forces the full compile; `OutDir` only redirects the copy step, because a **stale running `Clip.exe` (PID 28128, started 15:15) holds `bin\Release\net10.0-windows\Clip.exe` open** — the run brief says to leave it alone. Without the redirect the identical build fails only at the apphost copy (`MSB3027` file lock), never at a compiler error; an incremental `dotnet build -c Release` with nothing to copy reports 0/0. Stop the stale instance and re-run the plain gate to see it copy cleanly.
