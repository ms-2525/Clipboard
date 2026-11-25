# AUDIT — Clip data layer (logic defects)

Scope: `Core\ClipboardStore.cs`, `Core\ClipboardStore.Items.cs`, `Core\ClipboardStore.Tabs.cs`,
`Core\ClipboardItem.cs`, `Core\CaptureService.cs`, `Core\Config.cs`, `Core\ImageCache.cs`,
`Core\SearchService.cs`, `Core\ClipTags.cs`, and the new `Host\ClipHost*.cs` reader.
Code under test: worktree `v2-stack` @ `00d5afc` (Core identical to HEAD; `Host\` and `Core\ClipTags.cs` are the new stack).
Method: a scratch harness (`%TEMP%\opencode\db-audit\harness`) that links the **real** `Clip.dll`
and calls `ClipboardStore` / `SearchService` directly. Databases were always copies;
the owner's live `%APPDATA%\Clip\clip.db` was never opened.

---

## Verdict

| # | Area | Verdict | Evidence (what proved it) |
|---|------|---------|---------------------------|
| 1 | Schema & migration | **PASS** (one RISK) | Old first-build DB (no `rtf`/`tab_id`/`image_hash`) and a no-`tabs` DB both opened, read **and wrote** (`append`: new id=4, format stored). Pinned row adopted into Pinned, orphan format swept, ids kept. `PRAGMA user_version` = **0** (no version marker; migration is idempotent DDL probing). |
| 2 | Write path integrity | **PASS** (one RISK) | `Environment.Exit` inside an open transaction → rollback, 1 committed row + 1 format, 0 orphans, `integrity_check ok`. `Stop-Process -Force` mid-writer → 200 items all complete, `integrity ok`; 10 orphan formats left by non-transactional cleanup (P3). |
| 3 | Dedupe | **PASS** to the documented rule (P3 notes) | Identical text 1 h later from another app → same row, new app/time, moved to top. Trailing space / different case → new row. Same PNG twice → one row (SHA-256 + blob compare). Row in Pinned or a tab is **exempt**, so a re-copy makes a new main-history row. |
| 4 | Cap | **PASS** | 10 000 inserts → `EnforceCap(200)` = 365 ms (then 0 ms), main trimmed to exactly the newest 200, pinned 200/200 and tab 265/265 survive, 0 orphans, `integrity ok`. |
| 5 | Tabs & ordering | **FAIL** | Ties break on `id DESC` correctly; deleting a tab with 3 items purges all 3 + formats and returns 3 (integrity ok, Pinned kept). But: DST fall-back sorts a new clip *below* older ones and `LatestText()` returns the older clip; pinning an old clip buries it at the bottom of Pinned; pin→unpin from a custom tab loses the tab; undo into a deleted tab creates an invisible row. |
| 6 | Payload fidelity | **PASS** on blobs/preview shape, **FAIL** on the truncation signal | phase1 copy: 38 rows (36 text / 1 files / 1 image), 230 format rows, 12 HTML, 4 RTF, 1 PNG 227 259 B + 8 678 B thumb. s2copy: 141 main rows, 469 main-history format rows (471 total), 11/11 images are PNG blobs with non-empty PNG thumbs, 4/4 CF_HTML offset sets valid. But the `TextLength` (code points) vs `Text.Length` (UTF-16) test silently truncates emoji text → **P0** below. |
| 7 | Search | **FAIL** | Case/ASCII/Devanagari substring work; ordering newest-first. `café`: in-memory Filter 168 hits vs SQL `SearchFull` 84 → **MISMATCH** (SQLite `lower()` is ASCII-only). `GetRecent(-1)` = 48 ms @ 1 k rows, 531 ms @ 9.5 k (the bridge's query path). |
| 8 | Concurrency with the new host | **PASS** with rules | Persistent read-only side connection during 300 writes: 4 641 reads, 0 errors. Per-call read-only connection: 0 errors but only 6 reads in the same window (~16 ms/open). Second writer connection: 0/100 errors. Store connection used from 2 threads: 0 errors (not a contract). Opening while another process holds EXCLUSIVE: blocked 6 621 ms. |
| 9 | Corruption & recovery | **FAIL** | Random bytes / truncation to 8 KB / half / page-1 damage → moved to `clip.db.corrupt-<stamp>`, fresh DB, `RecoveredFromCorruption=true` (toast exists). **But** a 0-byte file is treated as a fresh DB (no recovery, no toast) and a corrupted *data page* passes open, then the capture write throws error 11 → unhandled → crash loop. |
| 10 | Privacy | **RISK** | All writes stay under `%APPDATA%\<AssemblyName>` (db, config, `cache\images`, `crash.log`). No app deny-list exists: only the two clipboard opt-out formats are honoured. Deleting a clip never deletes its cached PNG (7-day / 100-file sweep) and corrupt copies are kept forever. |

---

## Findings

### P0 — Emoji/astral text is silently truncated on paste, and the F2 editor can destroy the full body

**What happens.** `GetRecent`/`GetTabItems` return `length(text)` (SQLite code points) as
`TextLength` and `substr(text,1,1200)` (code points) as `Text`. Every caller decides "the preview is
the whole body" with `TextLength > Text.Length`, comparing code points to **UTF-16 units**. When the
preview is emoji-dense, `Text.Length` exceeds `TextLength`, the test says "not truncated", and the UI
pastes and edits only the preview — while `Update()` writes that preview back over the full body.

Measured (`audit preview`, real store):

```
600 emoji + 1000 ascii: TextLength=1600 previewUtf16=1800 uiLoadsFull=False pastedEqualsFull=False lostCodepoints=400
1199 emoji + 2 ascii:   TextLength=1201 previewUtf16=2399 uiLoadsFull=False lostCodepoints=1
plain 2000 ascii:       TextLength=2000 previewUtf16=1200 uiLoadsFull=True  (correct)
devanagari 1300:        TextLength=1400 previewUtf16=1200 uiLoadsFull=True  (correct)
```

Rows already in the owner's data show the same code-point/UTF-16 disagreement (s2copy: id=85
`cpLen=10717 utf16Len=10718`, id=64 `84/86`), so it is not theoretical.

**Who consumes the wrong test** (all five must change together):
- `UI\MainWindow.xaml.cs:482` `FullText()` — paste gives the truncated body.
- `UI\MainWindow.Rich.cs:123` — F2 editor starts from the preview; saving calls `ClipboardStore.Update`,
  which **permanently replaces the stored full body with the preview**. This is the data-loss path.
- `UI\MainWindow.xaml.cs:279` — deep search is skipped, so text past 1 200 chars in such a row is unfindable.
- `UI\ClipRow.cs:56,88` — the tooltip shows the truncated body.
- `Host\ClipHost.Queries.cs:59` — `MoreCount = TextLength - Text.Length` goes negative → clamped to 0,
  so the new web UI shows no "more" affordance and never loads the full body.

**Reproduce.** `audit.exe preview <db>`; or insert `new string('😀'...)`-heavy text > 1 200 code points
with a tail and open the popup → paste into Notepad.

**Smallest correct fix.** The preview is exactly `min(1200 code points, body)`, so the exact test is
`TextLength > ClipboardStore.PreviewChars`. Replace the five comparisons with that (or add
`bool TextTruncated` from SQL `length(text) > 1200` to `ClipboardItem` and use it for the decision and
for `MoreCount`). Leave `TextLength` as-is for display. Add one regression row with 600 emoji + 1 000 ASCII.

**Blocks release: YES.** It is silent clipboard corruption and a one-save destruction of stored data.

---

### P1 — A corrupt data page is not detected: capture throws and the app dies in a loop

**What happens.** `IsCorruption` is only consulted inside the constructor. A DB whose header and
`sqlite_master` are intact but whose item B-tree page is damaged opens normally
(`RecoveredFromCorruption=false`, list renders), then `InsertWithFormats` throws
`SQLite Error 11: database disk image is malformed`. `CaptureService.Store` calls that on the UI thread;
`App`'s `DispatcherUnhandledException` only writes `crash.log` (it does not set `Handled`), so the
process terminates. Every following start takes the same path: start fine → copy something → crash.
There is no recovery because the file is never moved aside.

```
audit.exe corrupt cp-40000.db page 40000
OPEN OK recovered=False ...
WRITE FAILED SqliteException code=11 'database disk image is malformed'
audit.exe open cp-40000.db              # next start
opened: True  recovered: False  recent: 5   # still not detected
```

The checklist row `Corrupt/locked clip.db: rename + recreate + a user notice instead of a dead start`
is only true for corruption that happens to touch page 1.

**Smallest correct fix.** Catch `SqliteException` with code 11/26 where captures are written
(`CaptureService.Store`, or a `ClipboardStore.TryWrite` wrapper), and run the existing
`MoveAside` + reopen + `RecoveredFromCorruption` path once. One retry, one toast, then never again.

**Blocks release: YES** (the new host inherits the crash).

---

### P1 — The search fallback and the list search disagree on accented text

`SearchService.Filter` uses `OrdinalIgnoreCase`; `SearchFull` uses SQLite `lower()`, which folds ASCII
only. On 1 000 rows (`audit search`):

```
q='café'    Filter=168  SearchFull=84   MISMATCH
q='CAFÉ'    Filter=168  SearchFull=84   MISMATCH
q='नमस्ते'  Filter=83   SearchFull=83   ok
q='budget'  Filter=168  SearchFull=168  ok
```

So a clip containing `CAFÉ RÉSUMÉ` is found while it is on screen, and disappears when the same query
falls through to the full-body scan; and an uppercase-accented clip can never be found by a lowercase
query in the SQL path. (Turkish `İ` happens to agree today only because both sides leave it alone;
`istanbul` finds 0 in both.)

**Smallest fix.** Make `SearchFull` fold the same way as .NET. Either register a `lower_unicode`
SQLite function (Microsoft.Data.Sqlite `CreateFunction`) and use it in `instr`, or drop the SQL path and
filter in C# with `OrdinalIgnoreCase`. Do not paper over it with `COLLATE NOCASE` (also ASCII-only).

**Blocks release:** the new UI keeps the same two-path search, so yes for any non-English owner data.

---

### P1 — "Undo delete" can restore an item into a deleted tab, where it is invisible forever

`Restore` writes `it.TabId` straight back with no existence check (`ClipboardStore.Items.cs:86`), and
there is no foreign key (the schema has none; `PRAGMA foreign_keys=1` is set by the provider but has
nothing to enforce).

```
audit.exe tabs tabs.db
deleted tab 2 with 0 items
undo restore into the deleted tab: id=2 ok; visible in main=False (tab_id=2)
rows in db=2; recent=1; tabs=1        # the row exists, is in neither list, and is exempt from the cap
```

**Smallest fix (one line).** In `Restore`: `if (it.TabId.HasValue && !TabExists(it.TabId.Value)) it.TabId = null;`
(or set it in the undo caller). The row then lands in main history where the user can see it.

**Blocks release:** cheap enough to fix now; the window is "delete item → delete its tab → hit Undo".

---

### P1 — Wall-clock timestamps: a DST fall-back (or a clock change) puts new clips at the bottom

`captured_at` is `DateTime.Now` with no offset (`ClipboardItem.Stamp`), and every list is ordered by
that string. When the clock goes back, a clip captured *after* the change sorts below clips from before it.

```
audit.exe order order.db
  2026-10-25T02:05:00.000 id=9  before-fallback
  2026-10-25T01:10:00.000 id=10 after-fallback   <- newest row, last in the list
LatestText=old-second-later
```

Consequences that session: new clips appear at the bottom of the popup, `LatestText()` (the plain-paste
hotkey) pastes an older clip, and `EnsureVisible`/selection jump. Same class of bug for a manual clock
fix or a timezone change. The `id DESC` tie-break cannot help because the timestamps differ.

**Smallest fix.** Store UTC in `captured_at` (`DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff")`) and
render local time at display; add a one-time backfill that shifts the existing local stamps by the
machine's current offset (best effort, documented). Do it with the web UI's date grouping, before the
grouping ships, because the same inversion will render two sections out of order and it is much harder
to explain to the owner after people have dated screenshots.

**Blocks release:** no for 51 weeks of the year; yes if the owner is in a DST country and releases near
the change. Rank P1, fix with the date-grouping work.

---

### P1 — The new bridge's search loads the whole history on the UI thread

`Host\ClipHost.Queries.cs:27` does `_store.GetRecent(-1)` and then `SearchService.Filter` for every
query with text. Measured on the real store:

| rows | `GetRecent(-1)` |
|------|-----------------|
| 953 (1 k test) | **48 ms** |
| 9 535 (10 k test) | **531 ms** |

The checklist budget is "search < 50 ms end-to-end" and "`GetRecent(1000)` ≤ 10 ms". At 1 k rows the
load alone is 48 ms; at 10 k every keystroke freezes the UI for half a second (before the 120 ms
debounce even helps, because the load happens per call). `GetRecent(200)` is fine (2 ms).

**Smallest fix.** For a query, use the existing `SearchFull(query, limit, kind, tabId)` (17 ms on 1 k;
a scan, but it returns at most `max` ids and never loads previews) instead of `GetRecent(-1) + Filter`,
and keep `Filter` only for the already-loaded page. If tag filtering is needed, filter the ids in SQL
first, then load those rows.

---

### P2 — Pin/unpin from a custom tab silently moves the item to main history

`TogglePin` treats anything not in Pinned as "move to Pinned"; unpinning sets `tab_id = NULL`, not the
previous tab. The item's Work membership is lost with no message.

```
in work: main=0 work=1 pinned=0
after Ctrl+P: pinned=1 work=0
after Ctrl+P again (unpin): main=1 work=0      <- not back in Work
```

**Smallest fix.** Accept it and document (a clip belongs to one container), or remember the previous
`tab_id` in memory for the undo toast. Not a data-loss bug; a "where did my clip go" bug.

### P2 — Pinning an old clip puts it at the bottom of Pinned

Pinned is ordered by `captured_at`, not by pin time. Pinning the oldest row of a 10 k history makes it
the last row of Pinned, below the fold.

```
pinned old row id=10 at=2026-10-25T01:10:00.000; pinned tab order: after-fallback
after pinning the newest row, pinned tab order: old-second-later,after-fallback
```

**Smallest fix.** When moving into a tab, stamp `captured_at` (or add `pinned_at`) — same write as
`TryDedupeMoveToTop` already does for a re-copy. Decide once; the web UI's Pinned list will otherwise
look unsorted.

### P2 — A locked database freezes the UI thread for up to the command timeout

`ClipboardStore` opens with `PRAGMA journal_mode=WAL`, a write that needs a lock. With another process
holding `BEGIN EXCLUSIVE`, `new ClipboardStore` blocked for **6 621 ms** (default command timeout 30 s;
`PRAGMA busy_timeout` is 0, so the wait is Microsoft.Data.Sqlite's retry loop). The constructor runs on
the UI thread before the window exists. Same class of stall for a capture that lands while a backup
tool holds the file.

**Smallest fix.** Nothing to change in SQL; open the store inside the existing startup try/catch and
show the "history unavailable" toast on `SqliteException` instead of hanging, or shorten
`DefaultTimeout` to ~3 s for the store connection. Do not add a second writer.

### P2 — Privacy: no app-level exclusion, and deleted images stay in the cache

- Exclusion is **format-only** (`Config.IgnoreFormats` = `Clipboard Viewer Ignore`,
  `ExcludeClipboardContentFromMonitorProcessing`). There is no app deny-list; a password manager that
  copies without those two formats is persisted in plaintext SQLite. `CHECKLIST-features.md` §10 lists
  "per-app capture exclusion" as deferred, so this is a known gap — but the row
  "a password-manager copy never appears" cannot be claimed as-is.
- `ImageCache.Write` stores `%APPDATA%\Clip\cache\images\clip-<id>.png`; `Delete`/`Clear` never remove
  it, and `Cleanup` only runs at startup (older than 7 days, keep newest 100). A clip the owner deleted
  remains recoverable on disk.
- `clip.db.corrupt-<stamp>` copies are never deleted.

**Smallest fix.** In `Delete`/`Clear`/`DeleteTab`, delete the corresponding cache file (best effort);
add the cache path to the Clear confirmation text. Keep the corrupt copies (they are the only recovery
material) but say so in the UI copy.

### P2 — Tag filtering in the new UI can only see the loaded page, and is computed from the preview

`ClipTags.Classify` receives `row.Text` (the 1 200-char preview) and the format names. So a SQL block or
HTML page whose shape starts after 1 200 chars is untagged, and a `files`/`spreadsheet` tag depends on
`item_formats` still existing (`Update` deletes them). `ClipHost.Page` has no tag parameter, so a tag
filter is a client-side filter of the 200 loaded rows: "filter by tag" will silently ignore older
matches. Decide before the UI ships: either filter the loaded page and label it as such, or add a
server-side tag pass over `GetRecent(-1)` (531 ms at 10 k — do not).

### P3 — Cleanup statements are not wrapped in transactions

`EnforceCap`, `Clear` and `Delete` run two statements in one `ExecuteNonQuery` in autocommit. A kill
between them leaves orphan `item_formats` rows:

```
(killed writer)  items=200  item_formats=410  orphan formats=10  integrity ok
(next open)      items=200  item_formats=400        # Migrate sweeps them
```

No data loss, and `Migrate` heals it on the next start; it costs disk until then. **Fix:** wrap both
statements in one `BeginTransaction()` (three call sites, no behaviour change).

### P3 — `MaxItems` from a hand-edited config is not validated

`SettingsWindow` clamps 10…10 000, but `Config.Load` does not. `EnforceCap(0)` deletes the whole main
history (pinned/tabs survive):

```
EnforceCap(0) … after trim: main=0 pinned=6/6 work=8/8
```

**Fix:** `if (max < 10) max = 10;` in `EnforceCap` (one line). Also `EnforceCap` is only called from
`CaptureService`, so lowering the limit in Settings does not trim until the next copy.

### P3 — No schema version marker

`PRAGMA user_version` is 0 and never written. Every upgrade is re-derived by probing
`pragma_table_info` and `CREATE INDEX IF NOT EXISTS`. That works for these three generations (proven),
but an index whose *definition* changes can never be rebuilt (`IF NOT EXISTS` keeps the old one), and
there is no way to tell which build wrote a file. **Fix:** stamp `user_version` after `Migrate` and
gate the one-time steps on it. Cheap now, expensive later.

### P3 — One fixture row's `html` column is 1 byte short of its `item_formats` blob

phase1 copy, id=370 (Excel): the `HTML Format` blob is 2 048 bytes and declares `EndHTML:0000002048`
(self-consistent); the `html` column is 2 047 chars. The paste path uses the blob (`GetFormats` first),
so real paste is intact; only the `html` fallback / `getItem` preview sees an `EndHTML` one past the
end. The other 11 HTML rows in that file have no offset problem, and the 39 MB copy's 4 CF_HTML rows are
all valid, so this is most likely fixture provenance, not the
capture path. Worth a one-line check in the CF_HTML unit test.

---

## Safe for the new UI — exactly how the bridge should open and use the DB

**Writes.** Never open a second writer. Every write stays on the existing `ClipboardStore` instance
(`InsertWithFormats`, `TryDedupeMoveToTop`, `EnforceCap`, `TogglePin`, …) on the UI thread, exactly as
`CaptureService` and `ClipHost` do today. The provider's serialized mode tolerated cross-thread use in
the test (0 errors / 300 writes) — that is luck, not a contract.

**Reads.** Two proven-safe options:
1. **Same store, UI thread** — `GetRecent(200)` is 2 ms, `Counts()`/`ListTabs()` < 1 ms. Best for the
   page and the tab strip.
2. **One persistent `Mode=ReadOnly; Pooling=false` connection** — the `ClipHost.MetaConnection` pattern;
   4 641 reads during 300 concurrent writes, 0 errors. Use it for per-row metadata and thumbs.
   Do **not** open a connection per row (~16 ms each); one per page is acceptable if you must.

Keep `ClipHost`'s `_dispatcher.Invoke` around every store call. Do not move `_store` calls to a
`Task.Run`; the only call that belongs off-thread is `SearchFull`, which already opens its own connection.

**Queries.**
- Page: `GetRecent(offset+limit)` + `Skip/Take` (fine at 200; it re-reads the page each time).
- Search: `SearchFull(q, max, kind, tabId)` (ids, newest first, bounded) — **not** `GetRecent(-1)`.
- Truncation: `TextLength > ClipboardStore.PreviewChars`, never `TextLength > Text.Length`.
  `MoreCount` must come from the same flag; the character delta is wrong for astral characters.
- Tab pages: `GetTabItems(-1, tab)` is unbounded; page it (extrapolating the 531 ms / 9.5 k load, a
  5 000-item tab is ~300 ms).
- Tags: not persisted; a tag filter is a filter of the rows you already loaded. Say so in the UI.

**Lifecycle.** Open the store once at startup (it blocks while another process holds a write lock — show
the toast instead of hanging). Dispose closes the handle, but the provider's pool keeps the file open
until `SqliteConnection.ClearAllPools()`; the corruption recovery routine already does this. If the store
recovers from corruption mid-session, discard and reopen `_meta` — it still points at the renamed file.

**Reading while capture writes is safe** under WAL (proven above); readers never see a half-written
capture because the item row and every format commit in one transaction.

---

## Positive findings — do not touch

- **Atomic capture.** `InsertWithFormats` writes `items` + `item_formats` in one transaction. Proven two
  ways: `Environment.Exit` inside the transaction (rollback, no orphan) and `Stop-Process -Force` during a
  writer loop (no partial rows, `integrity_check ok`).
- **The cap is correct and cheap.** `EnforceCap` is:
  ```sql
  DELETE FROM items WHERE tab_id IS NULL AND id IN (
    SELECT id FROM items WHERE tab_id IS NULL ORDER BY captured_at DESC, id DESC LIMIT -1 OFFSET $m);
  DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items);
  ```
  It keeps exactly the newest `MaxItems` main-history rows, never touches `tab_id IS NOT NULL`, and sweeps
  orphan formats. 10 000 rows: 365 ms once, 0 ms on every following capture. Pinned 200/200 and tab
  265/265 survived. `DeleteTab` with 3 items inside purged 3 items + 3 formats in one transaction and
  left `integrity ok`.
- **Dedupe semantics.** Byte-exact text (BINARY, no normalisation), SHA-256 + full-blob compare for
  images, tab/pinned rows exempt so a curated copy cannot swallow a fresh history capture, re-copy takes
  the new metadata/formats/timestamp and jumps to the top without changing its id.
- **The covering index does its job.** `ix_list` (with `substr(text,1,1200)` and `length(text)`) means the
  list never reads `html`/`rtf`/`image`/`thumb`. `GetRecent(200)` = 2 ms on 9 535 rows; the 531 ms number
  is only the explicit "load everything" call.
- **Migration is real, not aspirational.** Tested on the first-build schema (no `rtf`, `tab_id`,
  `image_hash`; second-resolution timestamps), a schema with no `tabs` table, and a DB with a custom tab
  already present. Ids, tabs and bodies survive; the old `pinned=1` row was adopted into Pinned; the
  orphan format row was swept; the new `ix_list` built in 625 ms on 9 535 rows.
- **Payloads are byte-exact.** `item_formats` BLOBs are stored and returned verbatim (Excel: 24 formats
  incl. Biff12/Biff8/Biff5/XML Spreadsheet/HTML/RTF/DIB); 469 main-history format rows in the 39 MB copy,
  all readable; 11/11 images are PNG blobs with non-empty 96 px thumbs; 14/14 RTF bodies start with
  `{\rtf`; 4/4 CF_HTML blobs have valid UTF-8 byte offsets and a non-empty decodable fragment.
- **Corruption at open is handled properly** (rename aside including `-wal`/`-shm`, recreate, toast) — only
  the two gaps in P1 above are missing.
- **Writes stay in one folder.** Only `%APPDATA%\<AssemblyName>`: `clip.db`, `config.json`,
  `cache\images\clip-*.png`, `crash.log`. No temp files, no registry writes, no network.
- **Ids are stable** (`AUTOINCREMENT`, never reused; dedupe/update/migration keep the id; `Restore` reuses
  the deleted id). The web UI can key rows by id.
- **Single instance** via a named mutex, so the "two app copies" race does not happen in production
  (`--v2 --db` is the deliberate dev escape).

---

## Evidence index

Raw outputs are in `%TEMP%\opencode\db-audit\` (`*.db`, harness at `harness\`). Commands used:
`dump`, `open`, `append`, `mkold`, `dedupe`, `cap`, `capread`, `order`, `fidelity`, `search`, `preview`,
`mismatch`, `tabs`, `tabpurge`, `corrupt <random|truncate|zero|half|header|page>`, `concurrency`,
`holdlock`, `trywrite`, `writer`, `crashwrite`, `orphans`, `raw`. All databases used were copies; no
process was left running.
