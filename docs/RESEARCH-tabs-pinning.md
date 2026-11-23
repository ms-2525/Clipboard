# Research — Tabs, Pinned tab, and move-to-tab for Clip

**Status:** decision-ready design. Phase 1 of 2 — this file is the only artefact written; no project source was edited.
**Date:** 23 Nov 2026. Current-code references are a snapshot of `Core\` at this date (another agent may be editing it).
**Owner requirement (frozen):** built-in **Pinned** tab + user-created custom tabs; items can be moved/added into a tab; **items in tabs are never auto-deleted** (cap eviction skips them, Clear History keeps them); full CRUD on tabs; Delete key deletes the selected item **only in list context**, never in an edit field.

**Licensing:** CopyQ and Ditto are GPL-family open-source projects. Only documented behaviour, commit messages, issue reports and technique names are used here. **No source code is copied.** Re-implement from the behaviour described, in Clip's own C# style.

**What is not in the repo:** the project spec itself. "§10.3 pin" is referenced as given by the owner and as recorded in `docs\CHECKLIST-features.md` §7. Nothing about §10.3 beyond that is assumed.

---

## 0. TL;DR — the recommended model

1. **One mechanism:** a tab is a container. `items.tab_id` is `NULL` for Clipboard/Images (main history) or a `tabs.id` for Pinned/custom tabs. **Pin = move to the Pinned tab** (`Ctrl+P` toggles), not a second per-item flag.
2. **Images is a view, not a container:** Clipboard and Images read `tab_id IS NULL` (Images adds `kind='image'`). Pinned and custom tabs read `tab_id = <id>`. ←/→ walks all four kinds of tab.
3. **Exemption is structural:** `EnforceCap` and `Clear` only touch `tab_id IS NULL` rows. No extra predicates to forget.
4. **Single parent, move only:** moving a clip into a tab removes it from history; moving it back re-enters history at its original timestamp. No row duplication, no duplicated image blobs.
5. **Dedupe is scoped per tab.** A pinned copy no longer swallows a fresh copy in history — a fresh copy creates a new history row. This is deliberate and matches CopyQ's independent tabs.
6. **Deleting a custom tab deletes its items** after an explicit confirmation that names the count (CopyQ's model). Pinned cannot be deleted. Items are protected from *automatic* deletion, not from a confirmed manual delete.
7. **Keyboard:** `←/→` switch tabs, `Ctrl+Tab` always works, `Ctrl+T`/`Ctrl+W` create/delete a tab, `Ctrl+P` pin/unpin, `Ctrl+M` opens a tab picker (proven muscle memory), `Ctrl+Shift+←/→` moves to the adjacent container. `Ctrl+1..9` stays **paste-nth**, not tab focus (CopyQ differs here because it has no paste-nth feature).
8. **Tabs show counts** (`Pinned · 4`) — CopyQ ships this as an option; ClipboardFusion's pinned list has its own limit. Counts are cheap and make the Pinned tab self-explanatory.

---

## 1. Ground truth in Clip today

| Fact | Evidence |
|---|---|
| `items` has no tab/locator column; only a **legacy `pinned INTEGER DEFAULT 0`** that no code writes or reads | `Core\ClipboardStore.cs:18-22`, grep: only the schema and a dedupe comment mention `pinned` |
| Dedupe is byte-exact, BINARY-collated, and moves the existing row to the top without touching `pinned` | `Core\ClipboardStore.cs:69-107` (comment lines 69-72; UPDATE line 91-92) |
| `EnforceCap` deletes globally by age, ignoring any protection | `Core\ClipboardStore.cs:224-233` |
| `Clear` deletes **all** items and formats | `Core\ClipboardStore.cs:217-222` |
| `Delete` already removes formats + item | `Core\ClipboardStore.cs:209-215` |
| One-shot idempotent migration pattern already exists (`ALTER … rtf`, catch "duplicate column name") | `Core\ClipboardStore.cs:31-44` |
| Capture always inserts/dedupes with no tab concept and calls `EnforceCap` | `Core\CaptureService.cs:66-78` |
| Popup has a single list, `Ctrl+1..9` pastes the nth item, Delete deletes the selection unconditionally | `UI\MainWindow.xaml.cs:94-136` |
| Popup `OnNewItem` inserts directly into `_all` | `UI\MainWindow.xaml.cs:54-59` |
| Cap is 200 | `Core\Config.cs:29` |
| Tabs are already frozen in the feature checklist; the earlier Ortu doc said "do not build tabs" and is superseded | `docs\CHECKLIST-features.md:67-73`; `docs\RESEARCH-ortu-ui.md:455` |

**Consequence:** the `pinned` column is dead weight today, but it is useful as a migration landing pad if the in-flight Core change implements a pin flag first (see §3.3, step 4).

---

## 2. Findings

### 2.1 Storage model and capture routing

| # | Practice | Who | Citation | How it maps to Clip |
|---|---|---|---|---|
| S1 | Each tab is a **separate item list**, not a filter over one table; tabs survive restart independently | CopyQ | [C1] "Tabs are means to organize texts, images and other data"; each tab has its own `copyq_tab_*.dat` file [C6] | We keep one SQLite table and a `tab_id` discriminator (cheaper, atomic). Same semantics: independent lists, no cross-tab edits. |
| S2 | **Per-tab options:** item limit and whether the tab's items are persisted to disk (a volatile tab). Capture routing is a separate global setting, not a per-tab switch. | CopyQ | [C4] per-tab `max_item_count`, `store_items`; [C5] 3.9.0 "New per-tab configuration allows disabling storing items on disk and limiting number of items"; [C1] "Tab for storing clipboard" | Clip needs no per-tab knobs for MVP: all tabs persist, and captures route only to history. This answers the brief's "disable capture into a tab": CopyQ does it by designating one capture tab, not by per-tab toggles. |
| S3 | New captures go to **one designated clipboard tab**; all other tabs are manual | CopyQ | [C1] "Tab for storing clipboard" option | Clip: new captures always go to main history (`tab_id NULL`). Predictable; no content-magic routing. |
| S4 | A type tab can be produced by auto-organising commands (e.g. move PNGs to an Images tab, removing them from the clipboard tab) | CopyQ | [C1] "Move Images to Other Tab" command with `Remove=true` | Clip instead makes **Images a filter view** of main history — less machinery, same user-visible tab. |
| S5 | Tab data is independent: the same content can exist in two tabs, and editing/deleting in one does not affect the other | CopyQ | [C2] "Deleting (or editing) items in one tab doesn't reflect on its copies in other tabs" | We choose **single parent (move, not copy)**, so this is moot. It is also why per-tab dedupe is the correct scope. |
| S6 | Default tab item limit is **200**, configurable per tab, raised to a 100 000 ceiling in 8.0 | CopyQ | [C3] `maxitems` default 200, per-tab description [C4]; [C22] 8.0.0 "Tab item limit has been increased to 100,000" | Coincidence worth noting: Clip's 200 cap matches CopyQ's default. Ours applies only to main history. |
| S7 | Encryption is **all tabs or nothing** (built-in since 14.0); the legacy plugin encrypted per item, not per tab, and tab data is otherwise plaintext on disk | CopyQ | [C21] "Built-in encryption automatically covers all tabs … there is no per-tab or per-item setup" | Clip's SQLite file is unencrypted; tabs must not imply per-tab privacy. No per-tab encryption in v1 — state it in Settings/help if encryption is ever discussed. |
| S8 | CopyQ has **no global "clear all tabs"** in the core app; clearing is a per-current-tab command (community scripts add "clear all") | CopyQ | [C14], [C15], [C20] | Clip's tray "Clear History" is global for history only. This is a deliberate difference: tabs are reference material, so the global command must never reach them. |
| S9 | "Permanent" clips/macros live in **separate groups** from the rolling history, and a group can auto-switch per foreground program | ArsClip | [A1], [A3] | Confirms containers are separate from history. Clip skips per-program auto-switch: it would surprise inside a popup and was not requested. |

### 2.2 Moving items between tabs / collections

| # | Practice | Who | Citation | How it maps to Clip |
|---|---|---|---|---|
| M1 | Drag an item onto a tab; **default action is move**, Ctrl-drag was intended as copy | CopyQ | [C7] commit "Default action to drag items to tabs is 'move'"; drag-and-drop to tab fixed in 7.0 [C8] | Drag-drop is a **secondary** Clip gesture for a later sprint; it must call the same `MoveItemToTab` API. |
| M2 | Keyboard/programmatic "Move to tab" exists as a reusable action/command; a target tab can be created on demand | CopyQ | [C9] issue: right-click → "Move to 'Tasks' tab" | Validates `MoveItemToTab(itemId, tabId)` as the single mutator. |
| M3 | **Moving an item to the tab it is already in must be a no-op**; CopyQ had a bug that deleted the item | CopyQ | [C9] issue #2669; fix [C10] "shown only if the current tab is not the same" | Hard requirement for Clip's `MoveItemToTab`: same-tab returns success without writing. |
| M4 | A **popup tab picker bound to `Ctrl+M`** is the community-proven move gesture; the script's author says he used `Ctrl+M` for exactly this in Ditto | CopyQ community script | [C11] `Shortcut=ctrl+m`, builds `menuItems(tabs)` then moves + selects in target | Adopt as Clip's **primary** move command; the habit is cross-tool, so no learning curve. |
| M5 | Keyboard group move: `F7` creates a group **from the selected clips**; `Ctrl+X/C/V` cuts/copies/pastes clips **between groups** without touching the Windows clipboard; `Ctrl+G` group tree; `Backspace` parent | Ditto | [D2] key bindings table | Confirms two things: selection→collection creation, and a keyboard-only path. Clip: `Ctrl+M` picker (not `F7`, which is free but less mnemonic); no in-app cut/paste between tabs for MVP. |
| M6 | A group can have a **global hotkey**; pressing it twice within 250 ms saves the current selection directly into that group | Ditto | [D3] | Not MVP. If per-tab hotkeys are ever added, this is the proven pattern — but it conflicts with Clip's global hotkey budget; skip. |
| M7 | Items can be made permanent **from the history popup** ("Make Permanent As…"), rather than only created inside the permanent store | ArsClip | [A2] | Reinforces that Clip's flow must start from a selected history item, not from a separate "new item in tab" dialog. |
| M8 | Optionally shows grouped clips in the main list (off by default) — a clip can be visible in both places | Ditto | [D5] | Clip chooses single-parent (tab items are not in history) for simplicity. A "show tab items in history" toggle is a possible later preference, not MVP. |

### 2.3 Protection from eviction and clearing

| # | Practice | Who | Citation | How it maps to Clip |
|---|---|---|---|---|
| P1 | Clips are exempt from **both** auto-delete mechanisms (max count and age) when: never-auto-delete, have a hotkey, **are in a group**, or are sticky | Ditto | [D1] Auto-Delete wiki | Direct validation of the frozen rule: "in a tab" is itself a protection class. Clip needs no extra flag. |
| P2 | Pinned items are skipped by eviction; if a tab is full of pinned items, CopyQ **refuses the new item** rather than deleting a pinned one | CopyQ | [C12] `docs/pin-items.rst`; code `canDropItem()` [C13] | Our cap counts only history rows, so this dead-end cannot occur. Pinning never makes a new capture fail. |
| P3 | Pinned items **cannot be removed**, so "Clear current tab" leaves them behind; the 6.0 changelog confirms it no longer shows a warning when pinned items remain | CopyQ | [C12] "Pinned items cannot be removed"; [C14] 6.0.0 changelog; command [C15] | Clip's global Clear History behaves like CopyQ's clear on the history tab: it never touches Pinned or custom tabs. |
| P4 | Windows **Win+V**: pinned items survive "Clear all" and survive restart; history is capped (commonly 25) | Microsoft | [W1], [W2] | Users already expect "Clear does not clear pins". Clip's Pinned tab must honour that expectation. |
| P5 | Pinned items **do not count toward the 25-item limit** (one source) vs. **do consume slots** (another source) — sources conflict | third-party | [W4] vs [W5] | **Do not copy either claim.** Clip's Pinned tab is a separate container, so the question does not arise: pins are outside the 200-row history entirely. Marked: conflicting/unverifiable. |
| P6 | ClipboardFusion has a **separate History tab and Pinned tab**; the pinned list has its **own maximum** and cannot be bulk-cleared | ClipboardFusion | [F1], [F2] | The closest Windows precedent for "Pinned is a tab". Validates the unified model. Its separate pinned cap is a product choice we reject (never auto-delete). |

### 2.4 Pinning semantics

| # | Practice | Who | Citation | How it maps to Clip |
|---|---|---|---|---|
| N1 | CopyQ's pin is a **per-item marker** that anchors the row in place, blocks deletion, and is skipped by eviction | CopyQ | [C12] | A different, heavier model: it changes list ordering and requires "unpin before delete". Clip's Pinned tab avoids row anchoring and lets Delete work normally inside the tab. |
| N2 | Pinning adds the clip to a **separate Pinned list/tab**; the help does not say whether the History copy remains, and users report that *using* a pinned clip re-adds it to History | ClipboardFusion | [F2] "To add clipboard items to the 'Pinned' tab, right-click on the item listed in the 'History' tab and select 'Pin'"; [F4] "Pinned entries are copied to History when used" | Precedent for **Pinned as a tab**, not for move-vs-copy. Clip chooses move for single-parent simplicity; pasting a pinned clip is suppressed from re-entering history by the existing sequence-number guard. |
| N3 | Deleting a pinned item is blocked until unpinned | CopyQ | [C12] "Deleting pinned items won't work, unpin the items first" | We deliberately **do not** copy this. Clip's protection is from *automatic* deletion; `Delete` in a tab is an explicit user action, same as Win+V. Fewer states, and the owner's Delete contract has no exception. |
| N4 | Users of ClipboardFusion **ask for custom folders/categories**; the request is still open in 2026 and at least one user switched to CopyQ for it | ClipboardFusion forum | [F3] | Validates custom tabs as the differentiator, not a nice-to-have. Also validates keeping them simple: name + list, no folder trees in v1. |

### 2.5 Tab lifecycle, deletion, and counts

| # | Practice | Who | Citation | How it maps to Clip |
|---|---|---|---|---|
| L1 | Removing a tab asks for confirmation and **purges its items**; the last tab cannot be removed | CopyQ | [C16] `removeTab()` → `purgeItems()`; `w->count() > 1` guard | Clip: Pinned is permanent, so "last tab" is impossible. Custom-tab deletion purges after a count-bearing confirmation. |
| L2 | Counter-example: deleting a group **keeps its clips**, which then count toward the history limit again | Clipboard History (macOS app) | [H1] | Rejected for Clip: with a 200-item cap, returning a 30-item tab dumps old rows into history and the next copy silently evicts them (and live history). Purge-on-confirm confines the damage. |
| L3 | Tab deletion can destroy deliberately saved reference material — the confirmation is the safety net, and CopyQ's copy does **not** name the item count | CopyQ | [C16] "Do you want to remove tab X?" | Clip improves on this: confirmation names the tab **and** its item count, defaults to Cancel, and never binds Enter to "delete". |
| L4 | Renaming a tab that is the capture target updates the capture setting too | CopyQ | [C5] 5.0.0: renaming the clipboard tab updates `clipboard_tab`/`tray_tab` | Clip has no capture-target setting, so rename is metadata-only. Pinned is not renameable. |
| L5 | Tab bar can show the **item count next to each tab name** (optional, added 2.3.0) | CopyQ | [C17] changelog; `show_tab_item_count` [C18] | Adopt, always on: `Pinned · 4`. Also matches Ortu's `· {count}` tabular treatment [O1]. |
| L6 | Tabs support custom icons, drag-reorder, tree groups with `/` names | CopyQ | [C1], [C17] | Out of scope. Flat order: Clipboard, Images, Pinned, then custom tabs by creation order. |

### 2.6 Keyboard model

| # | Practice | Who | Citation | How it maps to Clip |
|---|---|---|---|---|
| K1 | `Left`/`Right`, `Ctrl+Tab`, `Ctrl+Shift+Tab` navigate tabs; `Ctrl+T`/`Ctrl+W` create/remove; `Ctrl+1..9` focus a tab; `Delete` deletes items; `F2` edits | CopyQ | [C19] | Adopt all except `Ctrl+1..9` — Clip already uses it to paste the nth visible item (`MainWindow.xaml.cs:108-113`). Reusing it for tab focus would break a frozen shortcut. |
| K2 | A text search/filter coexists with arrow-key tab navigation | CopyQ | [C19] | Clip must decide by **state**: arrows switch tabs unless the search box has focus *and* contains text. |
| K3 | Tab bar hides when there is only one tab | CopyQ | [C1] | Clip always shows the bar (four tabs exist from day one: Clipboard, Images, Pinned, +). |
| K4 | Move-to-tab has no default global shortcut in CopyQ; a script adds `Ctrl+M` | CopyQ community | [C11] | Confirms there is no "standard" chord to inherit; `Ctrl+M` is the de-facto one because of Ditto muscle memory. |
| K5 | Delete in a text editor edits text, not the underlying record | Windows convention / all three tools | [C19] F2 = edit; Delete = remove item only in the list | The implementation trap is WPF key tunneling: the window-level `PreviewKeyDown` sees Delete **before** the inline editor. Guard by focus, not by hope. |
| K6 | Win+V pinning has **no documented keyboard shortcut** — it is a mouse hover / three-dot action | Microsoft guide | [W3] | Clip's `Ctrl+P` is an improvement on the platform, with no conflicting standard to inherit. |

---

## 3. Recommended design — data model

### 3.1 Vocabulary (use it in code comments and UI copy)

- **View tab** — Clipboard, Images. No row in `tabs`; a query over main history.
- **Container tab** — Pinned, custom tabs. A row in `tabs`; items point at it via `items.tab_id`.
- **Main history** — `items.tab_id IS NULL`. The only thing capped or cleared.
- **Pin** — move the selected item into the built-in Pinned container. There is no `IsPinned` boolean anywhere.

### 3.2 Why pin = move to the Pinned tab (one mechanism), not a flag

1. Every protection rule becomes one predicate (`tab_id IS NULL`) instead of a flag **and** a tab, so cap/clear/dedupe cannot drift apart.
2. It is already shipped on Windows: ClipboardFusion has separate History and Pinned tabs [F1][F2], so the container relationship is proven; making it a true move keeps one row per clip.
3. It is visible and navigable with ←/→; a hidden boolean needs a badge and a pinned *section* to explain itself (the old Ortu plan [O2]).
   Cost: pinning removes the item from Clipboard view. Mitigated by an always-visible Pinned tab with a count and a `Ctrl+P` toggle back.

**Dedupe interplay (confirmed, with one rule):**

- `CaptureService` always leaves `ClipboardItem.TabId = null`, so a capture can only dedupe against **main history**.
- A pinned copy therefore does **not** swallow a fresh copy. Copying the same text again creates a new history row; the Pinned row is untouched. This is the same user-visible result as CopyQ's independent tab stores [C1][C2].
- Dedupe's `UPDATE` must not rewrite `tab_id` (today it doesn't touch `pinned`; keep that property for tabs). Metadata/timestamp/formats update; the container never changes.
- `MoveItemToTab` does **not** dedupe. Pinning two identical clips creates two Pinned rows. Accepted: tabs are curated; dedupe is a capture concern. Document with a `shortcut:` comment if desired.
- Editing with F2 (`Update`) keeps `tab_id`: an edited pinned item stays pinned and moves to the top of Pinned; an edited history item stays in history.

### 3.3 SQLite migration (exact, idempotent)

Run `CREATE TABLE` inside the existing constructor command block; run `ALTER`/seed/index in `Migrate()` **in this order** (the index references `tab_id`, which old databases do not have yet).

**Step 1 — new table (safe on every startup):**

```sql
CREATE TABLE IF NOT EXISTS tabs (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  name       TEXT NOT NULL COLLATE NOCASE UNIQUE,
  kind       TEXT NOT NULL DEFAULT 'custom',      -- 'pinned' | 'custom'
  sort_order INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL
);
```

**Step 2 — the item locator (nullable on purpose):**

```sql
ALTER TABLE items ADD COLUMN tab_id INTEGER;
```

Idempotency: catch `SqliteException` containing `duplicate column name`, exactly like the existing `rtf` migration (`ClipboardStore.cs:31-44`). SQLite has no `ADD COLUMN IF NOT EXISTS`.

**Step 3 — seed the built-in Pinned tab (idempotent):**

```sql
INSERT INTO tabs (name, kind, sort_order, created_at)
SELECT 'Pinned', 'pinned', 0, strftime('%Y-%m-%dT%H:%M:%S','now','localtime')
WHERE NOT EXISTS (SELECT 1 FROM tabs WHERE kind = 'pinned');
```

**Step 4 — adopt any legacy `pinned=1` row (idempotent, resolves the in-flight agent):**

```sql
UPDATE items
SET tab_id = (SELECT id FROM tabs WHERE kind = 'pinned')
WHERE pinned = 1 AND tab_id IS NULL;
```

Today no code writes `pinned=1`, so this is a no-op. If a parallel change implements the flag, its rows land in the Pinned tab on upgrade and the flag can then be left unread. Do **not** keep both protection mechanisms active: two sources of truth will diverge.

**Step 5 — index for per-tab lists and cap queries:**

```sql
CREATE INDEX IF NOT EXISTS ix_items_tab ON items(tab_id, captured_at DESC, id DESC);
```

**Step 6 — startup orphan sweep (every launch, idempotent and cheap):**

```sql
DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items);
```

Cheap at Clip's scale (≤ a few hundred rows + tabs). It also repairs any DB that crashed between an item delete and its format delete.

**Step 7 — cache the Pinned id** in the store constructor:

```sql
SELECT id FROM tabs WHERE kind = 'pinned' LIMIT 1;
```

Expose it as `PinnedTabId` so `Ctrl+P` never needs `ListTabs()`.

**Collation notes**

- `tabs.name` uses `NOCASE` so `Pinned`/`pinned` and `Work`/`work` are duplicates. SQLite `NOCASE` folds **ASCII only**; accented duplicates are possible. Acceptable for tab names; do not add a custom collation.
- **Do not change item text collation.** Dedupe stays byte-exact / case-sensitive (`BINARY`), as frozen in the checklist [CK]. `tab_id` is an integer and has no collation implications.
- `tab_id` is deliberately **not** a declared foreign key: `PRAGMA foreign_keys` is off by default, and a dormant `REFERENCES` clause buys nothing. Validate the target tab in `MoveItemToTab`.

### 3.4 Exact SQL for the two destructive operations

**`EnforceCap(max)` — history only, pinned/custom untouched:**

```sql
DELETE FROM items WHERE id IN (
  SELECT id FROM items
  WHERE tab_id IS NULL
  ORDER BY captured_at DESC, id DESC
  LIMIT -1 OFFSET $m);
DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items);
```

The second statement is the existing orphan cleanup, unchanged. It now deliberately leaves tab items' formats alone because those `item_id`s still exist.

**`Clear()` — "Clear History" means main history:**

```sql
DELETE FROM items WHERE tab_id IS NULL;
DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items);
```

- No `EnforceCap` call afterwards (tabs would not be touched anyway, but the call would be misleading).
- The tray handler already refreshes the popup (`App.xaml.cs:87`), so no new wiring.
- Settings copy must say: **"Clears Clipboard and Images. Pinned and tab items are kept."**

**Storage risk (accepted, state it once):** tabs are uncapped by design. A tab full of a few-MB images can add hundreds of MB to the SQLite file; because tabs are never evicted, the app cannot fix this silently. Do not add eviction. If it ever matters, show the tab's total size and offer "move items to Clipboard" as a manual action. (Not in this sprint.)

**`Delete(id)`** is unchanged and correct in any tab:

```sql
DELETE FROM item_formats WHERE item_id = $id;
DELETE FROM items WHERE id = $id;
```

**No orphans invariant:** every path that removes items (`Delete`, `Clear`, `EnforceCap`, `DeleteTab`) ends with `DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items)`, and the startup sweep repairs history. A harness assertion must verify `COUNT(item_formats) == COUNT(items rows that have formats)` is consistent after every operation.

---

## 4. Recommended design — `ClipboardStore` API

New file `Core\ClipboardTab.cs` (plain data, bindable):

```csharp
public sealed class ClipboardTab
{
    public long   Id        { get; set; }
    public string Name      { get; set; }
    public string Kind      { get; set; }   // "pinned" | "custom"
    public int    SortOrder { get; set; }
    public int    ItemCount { get; set; }
    public bool   IsPinned  => Kind == "pinned";
}
```

`Core\ClipboardItem.cs` gains one line: `public long? TabId { get; set; }`.

Exact members on `ClipboardStore`:

| Signature | Semantics | Failure / no-op |
|---|---|---|
| `long PinnedTabId { get; }` | Cached at construction from `kind='pinned'`. | Impossible to be 0 after migration; throw `InvalidOperationException` if the seed is missing (corrupt DB) rather than silently mis-pinning. |
| `List<ClipboardTab> ListTabs()` | `ORDER BY sort_order, id`; `ItemCount` via correlated `COUNT(*)`. Include Pinned. | Empty custom list is normal. |
| `ClipboardTab CreateTab(string name)` | Trim; validate 1–24 chars, no control chars; insert with `sort_order = COALESCE(MAX(sort_order),0)+1`; return the row with `ItemCount = 0`. | Throws `ArgumentException` with a UI-ready message on blank/too long/duplicate (`NOCASE`). UI catches and shows it inline. Do not return `null` silently. |
| `void RenameTab(long id, string name)` | Same validation; `UPDATE tabs SET name=$name WHERE id=$id AND kind='custom'`. | Throws `ArgumentException` on invalid/duplicate; throws `InvalidOperationException` if the id is Pinned or unknown, so a bug is visible. UI never offers rename on Pinned. |
| `bool DeleteTab(long id)` | In one transaction: confirm `kind='custom'`; `DELETE FROM items WHERE tab_id=$id`; orphan-format cleanup; `DELETE FROM tabs WHERE id=$id`. | `false` for builtin/unknown; no partial work (transaction). |
| `bool MoveItemToTab(long itemId, long? tabId)` | `null` = main history. Validate tab exists (unless null) and item exists. If `tab_id` already equals target, return `true` **without writing** (CopyQ #2669). Else `UPDATE items SET tab_id=$tab WHERE id=$id`. Never write `captured_at`. | `false` if item or target is missing. No dedupe, no reordering, no timestamp rewrite. |
| `int CountItems(long? tabId)` | `SELECT COUNT(*) … WHERE tab_id IS $tab` (optionally `AND kind=$kind`). | Used for tab labels and the Images count; `ListTabs` supplies container counts. |
| `List<ClipboardItem> GetRecent(int max, string kind, long? tabId)` | `WHERE tab_id IS $tab` + optional `kind`; same ordering. `max < 0` means **no limit** (SQLite `LIMIT -1`); container views pass `-1` so a tab is never truncated to `MaxItems`. Keep the existing overloads delegating with `tabId: null`. | — |
| `bool TryDedupeMoveToTop(ClipboardItem it)` | Same signature; match adds `AND tab_id IS $tab` bound to `it.TabId`; UPDATE must not touch `tab_id` or `pinned`. | Capture passes `TabId = null`; no other caller today. |

`GetRecent`'s `SELECT` must also return `tab_id` and populate `ClipboardItem.TabId`. The UI needs it to decide what `Ctrl+P` means and whether "Clipboard" is a valid `Ctrl+M` destination; without it, every such check needs a second query.

**What is deliberately absent**

- No `AddToTab`/`CopyToTab`. "Add" from the UI is `MoveItemToTab` from a history selection; a second verb would double the surface for no user-visible gain. Adding brand-new content to a tab would require an editor flow that was not requested — F2 + move covers it.
- No copy/duplicate semantics: a single-parent move keeps blobs single-instance (important for 5 MB images).
- No per-tab cap, no per-tab "store items" switch, no tab tree, no tab icons, no reorder. All are CopyQ features that the frozen requirement does not ask for.
- `Insert` continues to omit `tab_id` (captures land in history). If a future flow ever creates an item directly in a tab, insert first then `MoveItemToTab`; do not add a second insert path now.

**Sorting inside a container:** `captured_at DESC, id DESC`, identical to history. A newly pinned old clip therefore appears at its chronological position, not the top. This keeps the card meta line truthful (moving never rewrites `captured_at`). Pinned lists are short, so this is acceptable; if it ever bothers the owner, add a `tabbed_at` column rather than lying about `captured_at`.

---

## 5. Recommended design — UI and keyboard

### 5.1 Tab bar visual (Ortu language, not browser chrome)

Place a 28–30 px tab strip between the search field and the list, with an 8 px gap above/below and a 1 px `Divider` hairline below.

| Element | Spec |
|---|---|
| Structure | `ItemsControl` + horizontal `StackPanel` in a hidden-scrollbar `ScrollViewer`. **Do not use WPF `TabControl`** (default chrome fights the flat design and brings keyboard/focus behaviour we don't want). |
| Tab text | 12 px, `Fg` at 0.92 selected / 0.45 unselected; hover 0.62. No pill background, no border, no shadow. |
| Selected indicator | 2 px accent underline, 16–20 px wide, centred under the label (the Ortu rail language, not a tab). Amber instead of accent **only** for the Pinned tab. |
| Count | 10 px `Fg` 0.30, tabular figures, middle-dot separator: `Pinned · 4` [O1][C17]. Hide the count when it is 0. |
| Pinned signal | 10 px amber pin glyph before the label; cards inside Pinned keep the amber left rail from [O2]. Amber stays reserved for pinned. |
| Create affordance | 16 px `+` glyph after the last tab, `Fg` 0.30 → 0.60 hover; click = same as `Ctrl+T`. |
| Overflow | Name max 24 chars; if the strip exceeds width, truncate names to 14 chars with `…` and let the strip scroll horizontally (mouse wheel over the strip). Ensure the selected tab scrolls into view. |
| Order | Clipboard · Images · Pinned · custom (by `sort_order`). |
| Focus | Tabs are **not** in the Tab-key focus chain. Keyboard control is via the shortcuts below; clicking a tab also works. |

The old Ortu recommendation "do not build tabs" [O3] is **superseded by the owner-frozen requirement**; the mock's pill-style fake tabs remain wrong. Minimal text tabs are the compromise.

### 5.2 Keyboard map

| Keys | Action | Guard / notes |
|---|---|---|
| `←` / `→` | Previous / next tab in bar order; clamp at both ends (no wrap); selection resets to first item and scrolls to top [CK]. | If the **search box has focus and contains text**, let the caret move. If the query is empty, or focus is the list, switch tabs. |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Next / previous tab, always (clamps at both ends, same as arrows). | Works even while typing in search. |
| `Ctrl+T` | New custom tab: inline name `TextBox` appears at the end of the strip; Enter commits, Esc cancels; switch to the new tab on success. | Duplicate/blank keeps the editor open with an inline error. Focus returns to Search on cancel. |
| `Ctrl+W` | Delete current tab if it is **custom**; empty → delete immediately; non-empty → confirmation overlay naming tab and count; default button = Cancel; Esc cancels; Enter must **not** confirm. | No-op on Clipboard/Images/Pinned. Disabled while the F2 editor or a tab-name editor is open (the editor owns the keys). After delete, switch to Clipboard. |
| `Ctrl+P` | Toggle pin: if the selected item is in Pinned → `MoveItemToTab(id, null)`; otherwise → `MoveItemToTab(id, PinnedTabId)`. Unpin always returns to **main history**, never to the tab the clip came from (no back-pointer). | Single selection only (list is single-select today). Flash the Pinned count / announce via the existing notification if desired — optional. |
| `Ctrl+M` | Open the **tab picker** (primary move command). | Destinations: Pinned + custom tabs; plus "Clipboard" when the item is not already in main history. Never Images. |
| `Ctrl+Shift+←` / `Ctrl+Shift+→` | Move selected item to the previous / next **container** in bar order (Clipboard → Pinned → custom…). Adjacent fallback, no overlay. | Skips Images (a view). No-op at the ends. Uses the same `MoveItemToTab`. |
| `Ctrl+1..9` | **Unchanged:** paste the nth visible item. | Must not be repurposed for tabs, unlike CopyQ [C19]. |
| `Delete` | See the matrix below. | — |
| `F2` | Edit item (existing) / rename tab when the tab strip itself has focus (optional). | Rename is also available by double-clicking a tab label, which matches CopyQ's menu-only rename [C16]. Full CRUD does not require a keyboard rename. |

**Tab picker (`Ctrl+M`) overlay spec:** 240–280 px wide, `Raised` background, radius 10, hairline border, anchored under the selected row or centred over the list. Rows 28 px: digit 10 px `Fg` 0.30, name 12 px `Fg` 0.92, count 10 px right-aligned. `1..9` selects immediately, `↑/↓` moves, `Enter` commits, `Esc`/`Ctrl+M` closes. It is keyboard-only by design; clicking a row also commits. This is the CopyQ community script pattern [C11], minus the script engine.

**Esc unwind order** (compose with the planned "Esc #1 clears search, Esc #2 hides" [CK]): Ctrl+M picker → tab-name editor → tab-delete confirmation → F2 editor (with its discard prompt) → clear search → hide. An overlay must consume Esc before the popup's own search/hide logic sees it.

### 5.3 Delete-key context matrix (the contract)

| Context | Focused element | `Delete` does | Why / implementation |
|---|---|---|---|
| **List** | `History` ListBox or a row; no inline editor | **Deletes the selected item** (in any tab, including Pinned), refreshes the filter, selects the next row | Owner CRUD contract [CK]. Tab protection is against *automatic* deletion only (finding P2) [W1]. |
| **Search** | `Search` TextBox (empty or typing) | **Deletes the character to the right** (native). Never deletes an item. | It is a text field. Deleting records from a search box is a data-loss bug waiting to happen. |
| **F2 editor** | `TextBox` / `RichTextBox` | **Deletes the character to the right**; never the item | Window-level `PreviewKeyDown` runs *before* the editor. Guard it. |
| **Tab-name editor** | inline `TextBox` | Character delete; never the tab or an item | Same guard. |
| **Ctrl+M picker** | picker overlay | Unbound (no destructive default) | Nothing to delete here. |

Exact guard for `MainWindow.OnPreviewKeyDown` (current handler at `MainWindow.xaml.cs:94-136`):

```csharp
// The editor owns all keys while open (Esc/Enter/Delete must not reach the popup).
if (_editing) return;

// …existing Esc / Enter / Ctrl+digit / arrows…

else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBoxBase)
{
    // delete selected item
}
```

Do **not** early-return for the search box globally: Enter in search must still paste, and Esc must still clear/hide. Only the Delete branch is gated by `TextBoxBase`. The editor gets a full early return because its own Esc/Enter semantics differ.

Note for the owner's wording "search = delete item": with a real search box that is a text editor, this cannot be literal. The safe, standard rule is **list focus = delete item, any text field = delete character**. This is what CopyQ does (Delete is a list command; F2 editing is separate) [C19].

### 5.4 Tab CRUD UI

- **Create:** `Ctrl+T` or `+`. Inline 90 px name editor in the strip. Validation: trim, 1–24 chars, no control chars, unique `NOCASE`; errors shown by a 1 px red-ish border + tooltip (do not open a native MessageBox from the popup — it steals focus and looks alien).
- **Rename:** double-click the tab label (and optionally `F2` when the strip has focus). Same validation. Pinned has no rename UI.
- **Delete:** `Ctrl+W` on a custom tab, or right-click → Delete. Empty tabs delete immediately; non-empty tabs show an in-popup confirmation card (**not** a native `MessageBox`):
  > Delete "Invoices" and its 12 items? Items in tabs are not deleted automatically — this is permanent.
  Buttons: **Cancel** (default, focused) and a red **Delete**. Esc cancels. Enter cancels.
- **Pinned:** no close affordance, no rename, no delete. It is a system tab.
- **After any CRUD:** reload tabs + counts, keep the current selection stable if possible (delete current tab → select Clipboard, row 0).

### 5.5 Search, selection, and live capture

- Search is **scoped to the active tab** (CopyQ's default; its "search all tabs" is a community add-on [C20]). This is the predictable choice; the Pinned tab is one arrow away.
- **Open on Clipboard every time** the popup is shown; do not persist the last tab. Tabs are reference views, and the just-copied item must be visible on Alt+V. Reaching Pinned costs two `→` presses.
- Keep the query when switching tabs, reset selection to the first filtered row, scroll to top [CK]. If the new tab has no match, show its empty state rather than silently clearing the query.
- `OnNewItem` (current `MainWindow.xaml.cs:54-59`) must only insert when the active tab is **Clipboard or Images**; if the user is reading Pinned/custom, just refresh that tab's count. Also remove an existing deduped id before inserting at index 0 (already an S3 checklist item).
- Empty states (Ortu tile pattern [O4]):
  - Pinned empty: "Nothing pinned" + keycap `Ctrl+P` "pin the selected clip".
  - Custom tab empty: "Empty tab" + keycap `Ctrl+M` "move clips here".
  - Images empty: "No images yet".
- Counts update after capture, move, delete, clear, and tab CRUD — recompute on `Reload`, not per keystroke.
- After `MoveItemToTab`, remove the moved item from the current filtered list (it left this tab), select the next row, and refresh counts. If the target is the current tab, the store returns a no-op and the UI must not reload or clear the query.

### 5.6 Drag-and-drop (secondary, not MVP)

CopyQ's default drag-to-tab is move [C7]. For Clip: `PreviewMouseMove` on a row with left button held + 4 px threshold → `DragDrop.DoDragDrop(row, new DataObject("Clip.ItemId", id), Move)`; tab bar `AllowDrop=true`; on `Drop`, resolve the tab and call `MoveItemToTab`. Show a 1 px accent outline on the hovered tab. Moving **out** of a tab by dropping on the Clipboard tab should work (target = `null`). Keep it out of the first implementation sprint; `Ctrl+M`/`Ctrl+Shift+←/→` already satisfy "keyboard command + secondary drag" once drag follows.

---

## 6. Edge cases the implementation must handle

| Case | Required behaviour |
|---|---|
| `MoveItemToTab` to the same tab | No-op, return success, **no delete** (CopyQ #2669 [C9]) |
| Pin an item already pinned | No-op |
| Unpin when Pinned is empty / item deleted | `false`, no exception |
| Unpin an old clip into a full history | It re-enters at its original `captured_at`, so the next capture may evict it as the oldest. Accepted: unpin is the user saying "treat it as history again"; rewriting the timestamp would lie in the card meta. |
| Copy text identical to a pinned item | New main-history row; Pinned row untouched (§3.2) |
| F2 edit a pinned item | Stays pinned, timestamp bumps to now, top of Pinned |
| Delete an item in a tab | Row + formats deleted; tab itself stays |
| Paste a clip from a tab | Stays in its tab; Clip's own paste is suppressed by the sequence-number guard, so it does not reappear in history |
| Clear History while viewing Pinned | Pinned unchanged; stays on Pinned; count unchanged |
| EnforceCap after a capture | Only main history trimmed; tab rows and their formats intact |
| Delete the active custom tab | Switch to Clipboard, selection row 0, counts refreshed |
| Rename/create duplicate (`NOCASE`) | Rejected with inline message; no DB write |
| Create tab named `Pinned`/`pinned` | Rejected (unique NOCASE) |
| Empty tab deletion | No confirmation needed |
| Popup closed while picker/editor open | Dismiss overlays first, then hide; never leave a dangling editor |
| Popup re-opened after working in a custom tab | Starts on Clipboard, search cleared, selection row 0; tab choice is not persisted |
| Very long tab name | Trim to 24 chars on save; ellipsis in the strip |
| Tab holds more than `MaxItems` items | All are listed (`GetRecent(-1, …)`); `MaxItems` caps history only, never a tab view or a tab's storage |
| 12+ custom tabs | Horizontal scroll; selected tab scrolled into view |
| Delete key while search has focus | Character delete, never item (matrix §5.3) |
| `Ctrl+P` while search has text | Pins the selected (filtered) item; query stays |
| Images view: pin an image | Image moves to Pinned and disappears from Images (main only). Expected for a move model; document in the empty state/tooltip if it confuses. |
| DB from an older build | `tab_id` null for every row → behaves exactly as today; Pinned seeded empty |

---

## 7. ADOPT list (file-level)

### Must

1. **`Core\ClipboardStore.cs`** — add `tabs`, `tab_id`, `ix_items_tab`, Pinned seed, legacy `pinned=1` adoption, startup orphan sweep (§3.3). Keep the existing try/catch idempotency style.
2. **`Core\ClipboardStore.cs`** — change `EnforceCap` and `Clear` to the exact SQL in §3.4. This is the frozen "never auto-deleted" contract.
3. **`Core\ClipboardStore.cs`** — scope `TryDedupeMoveToTop` by `it.TabId` (`tab_id IS $tab`); never rewrite `tab_id`.
4. **`Core\ClipboardStore.cs`** — add `ListTabs`, `CreateTab`, `RenameTab`, `DeleteTab`, `MoveItemToTab`, `CountItems`, the `GetRecent` overload, and `PinnedTabId` (§4). Validate names and tab existence at the store boundary.
5. **`Core\ClipboardItem.cs`** — `long? TabId`.
6. **`Core\ClipboardTab.cs`** — new tiny data class (§4).
7. **`UI\MainWindow.xaml`** — tab strip row (ItemsControl, not TabControl), 2 px underline, count, `+`, hairline; keep card template.
8. **`UI\MainWindow.xaml.cs`** — remove the hardcoded single-list load: current tab state, per-tab `GetRecent`, `←/→` + `Ctrl+Tab` with the search-caret guard, selection reset, `Ctrl+T`/`Ctrl+W`, `Ctrl+P` toggle, `Ctrl+M` picker, `Ctrl+Shift+←/→`, Delete guard (`_editing` early return + `Keyboard.FocusedElement is not TextBoxBase`), skip `OnNewItem` inserts on container tabs, refresh counts.
9. **`UI\SettingsWindow.xaml(.cs)`** — Clear History copy: "Clears Clipboard and Images. Pinned and tab items are kept." Confirmation text matches.
10. **Harness assertions** (§8) — the exemption rules are data-integrity rules; they get `[H]` tests, not just a live click-through.

### Should

11. **`UI\MainWindow.xaml.cs`** — tab picker as a small `Border` overlay (keyboard-only is fine); destination list excludes Images.
12. **`UI\MainWindow.xaml`** — Pinned amber glyph + amber underline; cards inside Pinned keep the amber rail [O2].
13. **`Core\ClipboardStore.cs`** — show a `shortcut:` comment where duplicates are allowed in tabs ("no dedupe on move; tabs are curated — add if users complain").
14. **`docs\CHECKLIST-features.md`** — tick the research reference; add the delete-context matrix and tab counts to the verification items.
15. **`UI\MainWindow.xaml.cs`** — on tab switch, if the search query has no results, keep the query but show the empty state (don't silently clear).

### Nice (defer)

16. Drag-and-drop item → tab (§5.6), with drop-on-Clipboard to move back.
17. "N matches in Pinned" hint when the current tab search is empty.
18. Global search across all tabs (CopyQ's "Search All Tabs" is itself a community add-on [C20]).
19. Tab reorder (drag) and tab icons.
20. `tabbed_at` column so new pins sort to the top without touching `captured_at`.
21. "Move all items to Clipboard" as an alternative in the delete-tab confirmation (the non-destructive option [H1]) — only if the owner later prefers keeping clips over purging.

---

## 8. Test plan (headless store harness, `[H]`)

Use a temp DB; no UI required except where noted.

1. **Fresh DB:** `ListTabs()` returns exactly one tab, `kind='pinned'`, `PinnedTabId > 0`.
2. **Reopen:** still exactly one Pinned (seed idempotent); existing items all have `tab_id IS NULL`.
3. **Legacy adoption:** insert a row with `pinned=1` and `tab_id NULL`, reopen → row is in Pinned; `pinned` column is not consulted afterwards.
4. **Cap exemption:** insert 3 history items, move one to Pinned, `EnforceCap(2)` → 2 history + 1 Pinned; Pinned row and its formats intact.
5. **Clear exemption:** `Clear()` → 0 history, Pinned row + formats intact, zero orphan formats.
6. **Dedupe scope:** copy text equal to a Pinned item → `TryDedupeMoveToTop` returns false and inserts a history row; history duplicate of history → true, one row, `tab_id` unchanged.
7. **Same-tab move:** `MoveItemToTab(id, PinnedTabId)` twice → second is a no-op; row count, `tab_id`, and `captured_at` unchanged.
8. **Move round-trip:** history → Pinned → `null` → original `captured_at` preserved, no duplicate, formats still attached.
9. **Delete tab:** custom tab with 3 items → all 3 items + their formats gone, history and Pinned untouched, tab gone.
10. **Built-in protection:** `DeleteTab(PinnedTabId)` → false, nothing deleted.
11. **Name rules:** create `"Pinned"`/`"pinned"` → throws; blank/25+ chars → throws; rename to an existing name → throws; rename custom works; rename Pinned → throws.
12. **Counters:** `ListTabs()` counts and `CountItems(null)` match after insert/move/delete.
13. **Orphans:** after every operation above, `SELECT COUNT(*) FROM item_formats WHERE item_id NOT IN (SELECT id FROM items)` is 0.
14. **UI-only checks (`[L]`):** Delete in search vs list vs F2; `←/→` with empty vs non-empty query; `Ctrl+M` picker destinations; `Ctrl+P` round-trip; `Ctrl+W` confirmation with count; counts refresh on capture while on Pinned.

---

## 9. Unverified / conflicting claims (do not build on these)

- **Ditto group deletion:** the Groups menu has "Delete Group" [D4], but whether its clips are deleted or returned to the main list is **not documented in the sources found**. Not used in any recommendation.
- **ClipboardFusion pin move-vs-copy / unpin destination:** the help says pin adds the item to the Pinned tab but does not state whether the History copy is removed, and does not document where unpin returns the item. Clip's move + return-to-history rule is our choice, not copied.
- **Win+V pinned items vs the 25-item cap:** sources conflict [W4][W5]. Only the "pinned survive Clear all and restart" part is Microsoft-documented [W1][W2].
- **CopyQ arrow-key wrap at the ends** and **whether Ctrl+drag-to-tab copies correctly today** were not verified against a running build. Clip clamps at the ends and defers drag-drop.
- **CopyQ "clear current tab" exact pinned behaviour:** the 6.0 changelog implies pinned survive (it suppresses the message when pinned remain) [C14]; the primary pin doc does not state it. Treated as supporting evidence only.

---

## 10. Sources

All accessed 23 Nov 2026.

**CopyQ (hluk/CopyQ) — GPL-family, technique reference only**

- [C1] Tabs and Items — https://copyq.readthedocs.io/en/stable/tabs-and-items.html
- [C2] CopyQ usage guide (independent tab copies) — https://www.makeuseof.com/manage-linux-clipboard-using-copyq/
- [C3] `appconfig.h` (`maxitems` default 200, `show_tab_item_count`, `clipboard_tab`) — https://codedocs.xyz/hluk/CopyQ/appconfig_8h_source.html
- [C4] Real config with per-tab `store_items` / `max_item_count` — https://gist.github.com/jasonm23/4fd10f79521278af55f2979975700740
- [C5] CHANGES 3.9.0 (per-tab config) and 5.0.0 (tab rename updates capture target) — https://github.com/hluk/CopyQ/blob/master/CHANGES.md
- [C6] Per-tab data file `copyq_tab_XYZ.dat` — https://github.com/hluk/CopyQ/issues/2617
- [C7] Commit: default drag-to-tab action is move — https://github.com/hluk/CopyQ/commit/9ddfad63179ca1506da56dd6c79f1c66d6f5a125
- [C8] CHANGES 7.0.0 (drag-to-tab fix) — https://github.com/hluk/CopyQ/blob/master/CHANGES.md
- [C9] Issue #2669: move-to-same-tab deletes the item — https://github.com/hluk/CopyQ/issues/2669
- [C10] Fix commit b842ef2 ("show only if the tab is not the same") — https://github.com/hluk/CopyQ/commit/b842ef27c082caa12da614855406777646f4741f
- [C11] Community "Move To Tab" script with `Ctrl+M` — https://wrpsoft.blogspot.com/2022/07/some-useful-scripts-for-copyq-clipboard.html
- [C12] Pin Items — https://copyq.readthedocs.io/en/stable/pin-items.html
- [C13] `canDropItem()` / pinned-skip eviction, cited in Clip's logic research — `docs\RESEARCH-logic-best-practices.md:70` (source refs `src/gui/clipboardbrowser.cpp:1568-1593`)
- [C14] CHANGES 6.0.0 ("Clear Current Tab" keeps pinned) — https://github.com/hluk/CopyQ/blob/master/CHANGES.md
- [C15] `clear-clipboard-tab.ini` — https://github.com/hluk/copyq-commands/blob/master/commands/clear-clipboard-tab.ini
- [C16] `MainWindow::removeTab` (confirmation, `purgeItems`, last-tab guard) — https://github.com/hluk/CopyQ/blob/master/src/gui/mainwindow.cpp
- [C17] CHANGES v2.3.0 (optional tab item count) — https://github.com/hluk/CopyQ/blob/master/CHANGES.md
- [C18] `show_tab_item_count` in a real config — https://github.com/hluk/CopyQ/issues/2497
- [C19] Keyboard shortcuts — https://copyq.readthedocs.io/en/stable/keyboard.html
- [C20] "Search All Tabs" community command — https://github.com/hluk/copyq-commands/
- [C21] Password protection / encryption scope (all tabs, no per-tab setup) — https://copyq.readthedocs.io/en/stable/password-protection.html
- [C22] CHANGES 8.0.0 (tab item limit raised to 100 000) — https://github.com/hluk/CopyQ/blob/master/CHANGES.md

**Ditto (sabrogden/Ditto) — GPL-family, technique reference only**

- [D1] Auto Delete wiki (groups exempt) — https://github.com/sabrogden/Ditto/wiki/Auto-Delete
- [D2] Key bindings (F7, Ctrl+X/C/V between groups, Ctrl+G, Backspace) — https://github.com/sabrogden/Ditto/wiki
- [D3] Group HotKeys (double-press global hotkey saves selection) — https://github.com/sabrogden/Ditto/wiki/Group-HotKeys
- [D4] Menu resource (`Groups → Delete Group`, `Move to Group`) — https://github.com/sabrogden/Ditto/blob/master/CP_Main.rc
- [D5] Options ("Show Clips in Groups in Main List", delete prompt) — https://deepwiki.com/sabrogden/Ditto/2.2.2-options-and-configuration

**Windows built-in**

- [W1] Microsoft — Using the clipboard (Clear all keeps pinned; 25-item limit) — https://support.microsoft.com/en-us/windows/apps/using-the-clipboard
- [W2] Microsoft — How to use Windows 11 clipboard history — https://www.microsoft.com/en-us/windows/learning-center/how-to-use-clipboard-history-in-windows-11
- [W3] Pinning is mouse-driven (three-dot menu), no keyboard pin shortcut documented — https://www.digitalcitizen.life/clipboard-windows-11/
- [W4] Claims pinned do **not** count toward 25 — https://www.utexsave.com/post/history-of-the-windows-clipboard-win-v-how-it-works-its-limitations-and-how-to-go-furtherhist
- [W5] Claims pinned **do** consume slots — https://www.makeuseof.com/i-enabled-windows-11-clipboard-history-and-immediately-turned-off-the-cloud-sync-i-didnt-know-was-on/

**ClipboardFusion (Binary Fortress)**

- [F1] Help — five tabs incl. Local History / Local Pinned; pin context menu — https://www.clipboardfusion.com/help/
- [F2] Guide — Pinned tab, separate maximum, cannot be bulk-cleared — https://www.clipboardfusion.com/HelpGuide/ClipboardManager/
- [F3] Feature request: category subfolders for pinned items (open, users switching to CopyQ) — https://www.clipboardfusion.com/Discussions/View/feature-request-add-category-subfolders-for-pinned-items/?ID=018ee3c7-9f45-723b-9522-db71417eb4c5
- [F4] User report: pinned entries are copied to History when used — https://www.clipboardfusion.com/Discussions/View/pinned-entries-are-copied-to-history-when-used/?ID=f5afa93f-65c5-4279-a7a6-42e518b31766

**ArsClip (joejoesoft)**

- [A1] Product page — Permanent Clips/Macros, groups — https://joejoesoft.com/vcms/97/
- [A2] "Make Permanent As…" from the popup; permanent clips/groups — https://softpanorama.org/Windows/Keyboard_and_mouse/Clipboard_managers/arsclip.shtml
- [A3] Permanent clips automation, per-program groups/AutoSwitch — http://www.joejoesoft.com/vcms/151/

**Other maintained manager (counterpoint)**

- [H1] Clipboard History (macOS) FAQ — groups, multi-membership, "deleting a group keeps its clips but they count toward the limit again" — https://clipboard-history.cc/faq/

**Clip repo references**

- [CK] `docs\CHECKLIST-features.md:26-39, 67-73, 83-88` — frozen popup, tabs, dedupe contracts.
- [O1] `docs\RESEARCH-ortu-ui.md:143-151` — header/status `· {count}` tabular treatment.
- [O2] `docs\RESEARCH-ortu-ui.md:170-180` — section header / pinned amber card treatment.
- [O3] `docs\RESEARCH-ortu-ui.md:455` — "Do not build tabs" (superseded by owner freeze; keep the warning about pill-style fake tabs).
- [O4] `docs\RESEARCH-ortu-ui.md:177-180` — empty-state tile + keycap rows.
- [L1] `docs\RESEARCH-logic-best-practices.md:65-73` — eviction/pinned research (superseded here: tab membership replaces the flag).
