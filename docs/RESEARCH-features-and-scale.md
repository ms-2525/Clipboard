# Clip — feature landscape (what we don't have) and the 1000-item scale plan

**Status:** research / decision input. Nothing outside this file was changed.
**Date:** 23 Nov 2026
**Scope:** (A) features mature clipboard managers ship that Clip does not; (B) what it takes to stay snappy and small at **1000+ items**.
**Grounding read first:** `docs/CHECKLIST-features.md`, `docs/RESEARCH-logic-best-practices.md`, `docs/RESEARCH-image-best-practices.md`, `docs/RESEARCH-ortu-ui.md`, `docs/RESEARCH-tabs-pinning.md`, `Core/*.cs`, `UI/MainWindow.xaml(.cs)`, `App.xaml.cs`.
**Licence rule:** Ditto, CopyQ, ShareX and ArsClip are GPL-family. Their source is read for *behaviour and numbers only*; every line we ship is written fresh. PowerToys is MIT but is still treated as a reference, not a source to copy.

| Tag | Meaning |
|---|---|
| **[S]** | read in the project's source this session |
| **[M]** | **measured on this machine this session** (see §B1) |
| **[D]** | from the project's own docs/README |
| **[U]** | not verified — do not build a decision on it |

---

## 0. Top findings (read this if nothing else)

**Part A**
1. The biggest value-per-line gap is **paste-time text transforms** (trim/uppercase/strip formatting/regex replace). Ditto's Special Paste menu is a dozen-plus variants, PowerToys has a whole module for it, ClipboardFusion has Triggers. All are pure `string` work in our stack.
2. **Export / backup / restore** is missing entirely and is the only feature on this list that protects against *our own* data loss (encrypted/locked DB, bad migration, dead disk). Ditto has Backup/Restore + Import/Export; CopyQ exports too.
3. A CA's clipboard holds **client data**. Sensitive-clip handling (mask PAN/Aadhaar/card-like strings, "skip next copy", pause capture) is cheap, local, and no surveyed manager does it well — a differentiator, not a copy.
4. **Per-app capture rules** and **age/size retention** are the two "hygiene" features every serious manager has (Ditto wildcards, ArsClip per-program groups, Ditto auto-delete by last-paste date).
5. Things that look tempting and should be **refused**: cloud sync, AI transforms, a scripting engine, LAN sync. They contradict local-first and dwarf the app.

**Part B**
6. **Our list already virtualizes** — 1000 rows realise **7 containers** and first layout is ~40–70 ms **[M]**. The danger is not the current XAML, it is the *planned* additions: grouping (Today/Yesterday), an Images **WrapPanel grid**, and pixel scrolling via `CanContentScroll="False"`.
7. `ScrollViewer.CanContentScroll="False"` is catastrophic and silent: **1000 containers, 3.6–4.4 s first layout** **[M]**. Any future "smooth scrolling" change must use `VirtualizingPanel.ScrollUnit="Pixel"` instead.
8. **The single biggest freeze risk is a big clip in a visible row.** A `TextBlock` with `MaxHeight=76` still formats the whole string: inside the real card, a 1 MB body costs **64–111 ms per relayout** and an ~8 MB body **254 ms** **[M]** — 4–15× a frame budget, every time the row is realised or the selection moves. Measured in isolation with an **unbounded** height constraint (what you get if the list ever ends up inside a `ScrollViewer`/vertical `StackPanel`), the same 1 MB string costs **1.06–1.4 s** **[M]**. Fix: bind a clamped `Preview` (~400 chars), never `Text`.
9. **Search is not the cost; rebuilding the collection is.** Filter over 1000×200 chars = **0.12–0.38 ms**; `ObservableCollection.Clear()+Add(1000)` + layout = **38–59 ms per keystroke**, 87 ms at 5000 **[M]**. Debounce 120 ms + swap the list.
10. **SQLite is fine at 1000; our write path is not.** `GetRecent(1000)` = **8 ms**; but 100 autocommit inserts take **~450 ms** vs **10–15 ms** with `WAL`+`synchronous=NORMAL` (≈40×) **[M]**, and image dedupe does a **full BLOB scan** (10–13 ms @1000, 54–61 ms @5000). One transaction + WAL + a hash column fixes both.

---

# PART A — Feature landscape

## A1. Already shipped or frozen (not proposed again)

Popup + search over text/app, tabs (Clipboard/Images/Pinned/custom — store + CRUD present, UI wiring pending), pin, F2 editor, all-format capture, exact dedupe with move-to-top, images tab + thumbnails (built, awaiting S3 wiring), F2 open-in-default-app, notification toast, plain-text paste hotkey, rich preview (built, one HtmlPanel, awaiting S3 wiring), cap eviction, settings (theme/time format/hotkeys/autostart/clear history), tray, multi-monitor DPI.
Source: `docs/CHECKLIST-features.md:16-120`.

Explicitly **out of scope here** and left to their own docs: paste fidelity/format fixes (`RESEARCH-logic`), image capture/paste fixes (`RESEARCH-image`), tab internals (`RESEARCH-tabs-pinning`), UI restyle (`RESEARCH-ortu-ui`). Part A only proposes features that are on none of those lists.

## A2. Landscape: what we lack

"Who ships it" is evidence, not endorsement. Value = value to a daily user of *this* app (a CA: heavy Excel/Word/browser, client data, keyboard-first). Cost = cost in **our** stack (WPF, SQLite, no server, no new runtime).

| # | Feature | One-line behaviour | Who ships it (evidence) | Value | Cost | Verdict |
|---|---|---|---|---|---|---|
| 1 | **Paste transforms** | Act on the selected clip at paste time: trim lines, collapse whitespace, UPPER/lower/Title, strip formatting, slugify, regex replace | Ditto `Special Paste` menu (Plain Text, UPPER, lower, Capitalize, Sentence, Invert, Trim White Space, CamelCase, Slugify, Remove Line Feeds, Ascii only) **[S]** `CP_Main.rc`; PowerToys Advanced Paste plain/Markdown/JSON **[D]**; ClipboardFusion `Triggers` rewrite text on copy **[D]** | High — daily | S (pure C#, one `Transform` enum + a submenu) | **MUST** |
| 2 | **Export / backup / restore** | One-click DB snapshot + restore; export selection as `.txt`/`.json`/`.csv` | Ditto `Backup Database` / `Restore Database` / `Import`+`Export Clip(s)` **[S]** `CP_Main.rc`; CopyQ export/import **[D]** | High (data-loss insurance) | S (`VACUUM INTO`, or copy DB when no writer) | **MUST** |
| 3 | **Sensitive-clip handling** | Detect PAN/Aadhaar/card/IBAN/password-ish clips; mask in the card; optional "don't store"; "ignore next copy"; pause capture | *No surveyed manager masks content.* Adjacent: Win+V opt-out formats **[D]**; Maccy "ignore next copy" / "disable" **[D]**; Ditto/CopyQ privacy formats **[S]** | High for a CA (client confidentiality) | S (regex + a mask helper; reuse `IgnoreFormats` gate) | **MUST** |
| 4 | **Retention: age + total size** | Delete items older than N days **and/or** beyond a total-bytes budget; per-item "never auto-delete" | Ditto auto-delete keyed on **last paste** date, plus per-item *Never Auto Delete* **[S]** `DatabaseUtilities.cpp:862-922`, `CP_Main.rc`; ClipboardFusion prunes to a max **[D]**; Win+V ~25 items **[U]** | High (DB hygiene, privacy) | S (config + `DELETE … WHERE captured_at < ?`, run on idle) | **MUST** |
| 5 | **Per-app capture rules** | Include/exclude by process name (wildcards), e.g. never store from `keepass.exe`, `mstsc.exe` | Ditto wildcard include/exclude on owner name **[S]** `ClipboardViewer.cpp:345-399`; ArsClip per-program groups **[D]** | High (already a logic-research GAP) | S/M (needs the owner-PID fix from `RESEARCH-logic` first) | **MUST** |
| 6 | **Multi-select + paste queue** | Select N items, paste them in order into a form/app, one `Ctrl+V` each | Ortu **Paste Stack** (2026, screenshot 4 + banner copy) **[D]** `RESEARCH-ortu-ui.md:26,366-367`; Ditto "delete multiple clips at once or paste several items in sequence" **[D]** (makeuseof.com Ditto review, not the source); CopyQ multi-select command access **[D]** `CHANGES.md` | High (form filling, Excel rows) | M (list `SelectionMode=Extended`, queue model, "paste next" hotkey) | **SHOULD** |
| 7 | **Search by app / date / type + filter chips** | `app:excel`, `today`, "Images only" chips; keep substring search as-is | Ditto: *Contains text / Regular expression / Wildcard*, search description vs full text **[S]** `CP_Main.rc`; Ditto type filter **[D]** | High — cheap | S (parse a prefix in `SearchService`, filter on `AppName`/`CapturedAt`) | **SHOULD** |
| 8 | **Duplicate-count UX** | Show "copied 3× · last 2 min ago" instead of silently teleporting the row | Nobody shows it; Ditto/CopyQ dedupe silently **[S]** `Clip.cpp` CRC, `clipboardbrowser.cpp` moveToTop | Medium (answers "where did my clip go?") | S (`use_count`, `last_used_at` columns) | **SHOULD** |
| 9 | **OCR of copied images** | "Image → text" on a screenshot, fully offline | **PowerToys** `image-to-text`, Windows OCR, no AI/network **[D]** (learn.microsoft.com, 2026-11); no OSS manager does it | High for invoices/statements | M/L (needs WinRT `Windows.Media.Ocr`; likely a TFM bump to `net10.0-windows10.0.xxxxx`) | **SHOULD** (after TFM spike) |
| 10 | **Per-item hotkeys ("quick paste")** | Assign `Ctrl+Alt+3` to one clip; press anywhere to paste it | Ditto **Quick Paste** + group hotkeys **[S]** `CP_Main.rc` (`Show Quick Paste`, `Remove Quick Paste`); Maccy pin assigns a permanent random shortcut **[D]**; ArsClip pinned shortcuts **[D]** | High for boilerplate, but competes with the global-hotkey budget | M (hotkey registry + conflict handling; we already have `HotKeyWindow`) | **SHOULD** |
| 11 | **Snippets / templates** | Named text with placeholders (`{date}`, `{clipboard}`, `{counter}`) | ArsClip *Permanent Clips/Macros* + JS macros **[D]**; ClipboardFusion Macros **[D]**; Ditto group hotkeys **[D]** | Medium (pinning covers 80%) | M | **NICE** |
| 12 | **"Paste as …" submenu** | Plain / rich / Markdown / file / JSON from the selected clip | PowerToys (plain, Markdown, JSON, .txt/.png/.html file) **[D]**; Maccy "paste without formatting" **[D]**; Ditto Special Paste **[S]** | Medium (plain exists; Markdown is the useful add) | S for plain/submenu, M for Markdown | **NICE** |
| 13 | **"Type text" fallback** | Type characters instead of injecting Ctrl+V, for apps that block paste (RDP/admin consoles) | ClipboardFusion `Type Text` **[D]** | Medium | M (`SendInput` per char; slow but reliable) | **NICE** |
| 14 | **Notes / tags per item** | A one-line note or tags above the body | CopyQ notes/tags **[D]** `docs/faq.rst`; Ditto clip description **[S]** (`Search Description`); ClipboardFusion friendly name **[D]** | Low/Medium | S (one column + editor field) | **NICE** |
| 15 | **Bulk actions** | Multi-select → delete / move to tab / pin | CopyQ multi-select actions **[D]**; Ditto bulk delete **[D]** | Medium (needed by tabs anyway) | S (rides on #6) | **NICE** |
| 16 | **Item statistics (small)** | "1,000 clips · 84 MB · 12 images · oldest 14 Sep"; DB size in Settings | No surveyed manager reports it; Ditto users debug 4 GB DBs by hand **[D]** (sourceforge forum) | Low/Medium (turns DB bloat into a visible fact) | S (`COUNT`, `page_count*page_size`) | **NICE** |
| 17 | **Drag an item out** | Drag a clip into Explorer/apps as text/file | Ditto drag support + `Set Drag File Name` **[S]** `CP_Main.rc`; Win+V drag **[U]** | Low/Medium | M (OLE drag source, file materialisation) | **NICE** |
| 18 | **Search highlight** | Bold the matched substring in the card | CopyQ highlights matches **[D]**; had to disable it for huge items (see B8) | Low | S but *dangerous*: forces rich text per row | **NICE** with a hard size cap |
| 19 | **Compare two clips (diff)** | Select two, see a diff | Ditto `Compare` menu **[S]** `CP_Main.rc` | Low for a CA | M | **NEVER** |
| 20 | **QR code view** | Render a clip as a QR code | Ditto `View as QRCode` **[S]** `CP_Main.rc` | Very low | S + dependency | **NEVER** |
| 21 | **Image annotation/editor** | Crop, arrows, blur inside the app | Ditto clip image editor **[D]**; ShareX (not a manager) | Low for us | L | **NEVER** (F2 → external app covers it) |
| 22 | **Cloud sync** | History on a vendor server, multi-device | ClipboardFusion Online Recent/Pinned **[D]**; Win+V sync **[D]** | Medium for users, fatal for local-first | L + ongoing trust cost | **NEVER** |
| 23 | **LAN sync** | Push clips to another PC | Ditto `Send To` (15 friends) **[S]** `CP_Main.rc` | Low | L (crypto, discovery, conflicts) | **NEVER** |
| 24 | **AI transforms** | "Fix grammar", "summarise", custom prompt | PowerToys Paste-with-AI (cloud **or** local Ollama/Phi Silica) **[D]** | Medium, but network or a 2 GB model | L | **NEVER** |
| 25 | **Scripting engine** | User scripts/commands over items | CopyQ commands (JS), ArsClip JS **[D]** | Low for our owner, high attack-surface | L | **NEVER** |
| 26 | **Fuzzy search** | Typo-tolerant matching | No surveyed manager ships it | Medium | M, noisy results | **NICE** (last) |

## A3. Roadmap (future milestones — clearly **not** S3)

S3 is untouched by this list. These are candidate *next* milestones, ordered by value ÷ cost.

**M4 — "trust & hygiene" (all MUSTs, ≈1 sprint)**
1. Retention by age + total size, per-item exempt (`#4`).
2. Export/backup/restore (`#2`).
3. Sensitive-clip masking + "ignore next copy" + pause capture (`#3`).
4. Per-app capture rules (`#5`, needs the owner-PID work from logic research).
5. Paste transforms (`#1`) — ship 6, not 15.
6. Search prefix filters `app:` / `type:` / `date:` (`#7`).

**M5 — "power use"**
7. Multi-select + paste queue (`#6`) and bulk actions (`#15`) — one sprint, one mechanism.
8. Duplicate-count metadata (`#8`), item statistics (`#16`).
9. Per-item hotkeys (`#10`) if the hotkey budget survives contact with reality.
10. OCR spike (`#9`) — TFM change first, feature second.

**M6 — "polish"**
11. `Paste as …` Markdown (`#12`), type-text fallback (`#13`), notes (`#14`), drag-out (`#17`), search highlight with a 2 KB cap (`#18`), templates (`#11`).

**Never:** `#19`–`#26`. Reasons are in the table; the short version: they either need a server/model, add an attack surface, or duplicate something the OS/external app does better.

## A4. Judgement calls worth arguing about

- **OCR is the only feature here that could change what the app is for a CA** (invoice/statement screenshots). It is SHOULD, not MUST, purely because of the TFM/WinRT cost — do the TFM spike before promising it.
- **Per-item hotkeys** are loved by Ditto power users but are the fastest way to a hotkey-conflict support burden. If we ship them, cap at 9 and only on pinned items.
- **Transforms vs "paste as Markdown"**: transforms are cheap and broadly useful; Markdown needs an HTML→MD converter (a dependency). Do transforms first, then see if Markdown is still wanted.
- **We are not adding tags** on top of custom tabs. Two ways to categorise one item is how CopyQ got its settings surface.

---

# PART B — 1000-item scale and resource plan

## B1. What was actually measured (and how)

A standalone WPF app (`net10.0-windows`, Release, .NET SDK 10.0.404) reproduced **our exact list shape**: 560×620 window, `Grid` with `Auto`/`*` rows, `ListBox` + the real item template (Border card with a `DataTrigger` on `IsSelected`, 13 px wrapped `TextBlock` with `LineHeight=19 MaxHeight=76 TextTrimming=CharacterEllipsis`, 11 px meta line), `ObservableCollection`. It counted realised `ListBoxItem` containers in the visual tree and timed layout with `Stopwatch` after `Dispatcher` idle. SQLite was measured separately with `Microsoft.Data.Sqlite 11.0.0` and a schema copied from `ClipboardStore.cs`.

**What this does *not* include:** the theme dictionaries, Manrope, the window `DropShadowEffect`, `AllowsTransparency`, and the real app's startup. Those must be measured in the app (see the contract in B10). Numbers are ranges across runs.

| Measurement | Result **[M]** |
|---|---|
| 1000 rows, default ListBox (Standard, item scrolling) | **7 containers**, first layout **64–74 ms** |
| 1000 rows, `VirtualizationMode=Recycling` | **7 containers**, first layout **24–41 ms** |
| 1000 rows, `ScrollViewer.CanContentScroll=False` ("pixel scroll") | **1000 containers**, first layout **3.6–4.4 s** |
| 1000 rows, `VirtualizingPanel.ScrollUnit=Pixel` | **7 containers**, **32–50 ms** |
| `ScrollIntoView(last)` at 1000 | **33–60 ms** until settled (includes a render pass) |
| Arrow-key move to last row (select + scroll) | **39–65 ms** until settled |
| `ObservableCollection.Clear()` + `Add(1000)` + layout | **38–59 ms** |
| `ItemsSource = List(1000)` + layout | **35–47 ms** (≈20% cheaper: no 1000 change events) |
| 5000 rows, Recycling | **7 containers**, **36 ms** first layout |
| `Clear()` + `Add(5000)` + layout | **87 ms** |
| Filter 1000 × 200 chars (`OrdinalIgnoreCase` over text+app) | **0.12–0.38 ms** |
| Filter 5000 × 200 chars | **0.59 ms** |
| `TextBlock` measure, **unbounded** height, 200 chars | **1.5–1.7 ms** |
| `TextBlock` measure, **unbounded** height, 20 000 chars | **45–51 ms** |
| `TextBlock` measure, **unbounded** height, 200 000 chars | **239–330 ms** |
| `TextBlock` measure, **unbounded** height, 1 000 000 chars | **1.06–1.4 s** (desired height still clamped to 76 by `MaxHeight`) |
| 1 MB text in a visible row, 1000-row list relayout (real card, finite viewport) | **64–111 ms** |
| ~8 MB text in a visible row, 5000-row list relayout | **254 ms** |
| Decode 1000 × 96×72 thumbnails (frozen, `DecodePixelWidth=96`) | **265–482 ms**, **+29 MB** working set |
| …after releasing them + GC | working set returns to baseline (native bitmaps freed) |
| SQLite `GetRecent(1000)` all columns | **8 ms**, ~0.1 MB allocated |
| SQLite `GetRecent(50)` | **0.8–1.5 ms** |
| SQLite `GetRecent(5000)` | **50–67 ms** |
| Dedupe `WHERE kind='text' AND text=$v` (no index) | **1.0–1.6 ms** @1000, **6–7 ms** @5000 |
| Dedupe `WHERE kind='image' AND image=$v` (BLOB compare) | **10–13 ms** @1000, **54–61 ms** @5000 |
| `EnforceCap` (delete 20 rows + orphan sweep) | **18–88 ms** @1000, **70–200 ms** @5000 |
| 100 single-row autocommit inserts, current journal mode | **444–465 ms** (≈4.5 ms each) |
| 100 single-row inserts after `journal_mode=WAL` + `synchronous=NORMAL` | **10–15 ms** (≈0.11 ms each) |
| `LIKE '%zzz%'` over 1000 / 5000 rows | **2.1 ms** / **5.8–7.3 ms** |
| Bundled SQLite | **3.53.4**, `ENABLE_FTS5=1`, **trigram tokenizer works** |

Test DB size: 12.6 MB at 1000 rows (50 images × 200 KB + text/html/rtf + formats), 62.8 MB at 5000.

**Schema caveat:** the SQLite measurements used the pre-tabs column set (`items` + `item_formats`, order `captured_at DESC, id DESC`). Tabs landed in `Core/` during this session, so the list query now also carries `WHERE tab_id IS NULL` and `ix_items_tab`. At 1000 rows that predicate is a filter over an index-order scan and will not change the conclusion, but it has **not** been re-measured — treat the DB numbers as "same order of magnitude", and re-run the recipe after the Core work settles.

## B2. List rendering — verdict per area

Our current XAML is **better than expected**: the `Grid` row is `*` (height-constrained), the `ListBox` is not inside a `ScrollViewer`/`StackPanel`, the `ItemsPanel` is the default `VirtualizingStackPanel`, and `ScrollViewer.VerticalScrollBarVisibility="Hidden"` does **not** affect virtualization. Measured: 7 containers. Keep all of that.

| Concern | Our code today | Verdict | Fix (effort) |
|---|---|---|---|
| Is virtualization on? | default ListBox, no explicit settings (`UI/MainWindow.xaml:54-57`) | **ALIGNED** (7 containers @1000 **[M]**) | Make intent explicit: `VirtualizingPanel.IsVirtualizing="True"` `VirtualizationMode="Recycling"` `ScrollUnit="Item"` `CacheLength="1,1"` — Recycling measured ~2× faster first layout (S) |
| Height constraint | `Grid` row `*` inside a window with fixed `Height` (`:33-36`) | **ALIGNED** | review rule: never wrap the list in a `ScrollViewer`/vertical `StackPanel` (that turns 7 containers into 1000 **[M]**) |
| Pixel/smooth scrolling | not set (item scrolling) | **ALIGNED** — keep `Item`. Pixel mode also virtualizes (7 containers **[M]**) but with variable row heights the thumb/extent becomes an estimate that jumps; the veteran horror stories are at 100k+, not 1000 | none (S if later) |
| Container recycling + `IsSelected` trigger | `Card` style binds `IsSelected` via `RelativeSource AncestorType=ListBoxItem` (`:16-22`) | **ALIGNED** — state lives in the data/container property, not in code, so recycling is safe | none; rule: never set row colours from code-behind |
| Selected row grows (planned) | current template is fixed `MaxHeight=76`; Ortu growth is planned (`RESEARCH-ortu-ui.md:431`) | **RISK (bounded)** — a height change invalidates the extent and can nudge the scrollbar; at 1000 rows only the realised rows matter | keep growth inside `MaxHeight` (3 lines), do not animate height; switch with `DataTrigger` only (S) |
| **Big text in a row** | `Text="{Binding Text}"` + `TextWrapping` + `MaxHeight=76` (`MainWindow.xaml:77-79`); a new `PreviewLines = 10` config exists (`Core/Config.cs:31`) that raises the visible line count without changing the layout cost | **RISK — highest** — `MaxHeight` does not stop the formatter: in the real card 1 MB costs **64–111 ms per relayout**, ~8 MB **254 ms** **[M]**; unbounded measure (virtualization broken / list in a `ScrollViewer`) is **1.06–1.4 s** for 1 MB **[M]** | add `Preview` (first ~400 chars, computed once in `ClipboardItem`) and bind that with `MaxHeight = PreviewLines × LineHeight`; keep `Text` for search/paste (S) |
| Grouped section headers (planned Today/Yesterday) | not built | **RISK** — `CollectionViewSource` grouping disables virtualization unless `VirtualizingPanel.IsVirtualizingWhenGrouping="True"`; measured article shows 1000 containers for 10 groups without it | either set that property **and** re-measure, or implement headers as pseudo-items in a flat list (M) |
| `CacheLength` | default (≈visible only; measured 7) | **ALIGNED** | optional `CacheLength="1,1"` page for wheel smoothness; re-measure (S) |
| Deferred scrolling | not set | **NICE** | `ScrollViewer.IsDeferredScrollingEnabled="True"` only if thumb-dragging ever feels heavy (S) |
| Row template cost | 2 `TextBlock`s + `Border` + trigger | **ALIGNED** | keep ≤ ~15 elements per row; no `DropShadowEffect` per row (it would be re-rasterised per container) |

## B3. Search

| Concern | Ours | Verdict | Fix |
|---|---|---|---|
| Filter cost | `SearchService.Filter` linear scan, `OrdinalIgnoreCase` (`Core/SearchService.cs:8-20`) | **ALIGNED** — 0.12–0.38 ms @1000, 0.59 ms @5000 **[M]** | none |
| Cost of *applying* the result | `_items.Clear()` + `Add` loop (`UI/MainWindow.xaml.cs:67-72`) | **GAP** — 38–59 ms @1000, 87 ms @5000 **[M]**; runs on every keystroke with no debounce | debounce 120 ms (`DispatcherTimer`, restart on each `TextChanged`); replace the collection (`ItemsSource = new List`) instead of Clear+Add (S) |
| Double rebuild on open | `ShowPopup()` calls `Reload()` then `Search.Clear()`; clearing a non-empty box raises `TextChanged` → second `ApplyFilter` (`MainWindow.xaml.cs:37-52,74`) | **GAP** — one wasted rebuild per open | clear search *before* reload, or set a `_loading` flag the handler respects (S) |
| When to move to FTS5 | not needed now | **ALIGNED at 1000** — FTS5 becomes necessary when the list is **paged** (history beyond the in-RAM window), not merely at 1000 rows: then the filter cannot see the text it is not holding | trigger: uncapped/paged history **or** >2000 loaded rows **or** >5 MB of loaded text. FTS5 is available in our bundle; use `tokenize='trigram'` for substring semantics (L) |
| What veterans do at 10k+ | Ditto: SQL `LIKE` with `LIMIT`, on-demand rows, CRC index **[S]** `DatabaseUtilities.cpp:479,484,862`; CopyQ: **all** items of a tab in RAM (200 default, hard-capped far below unlimited) and it *hangs* at 999 items when every row renders rich text **[S]** issue #1238, #600 | — | our in-RAM model matches CopyQ's; its failure mode (render, not filter) is what B2 guards |

**Decision:** keep in-memory filtering while the cap is a count we load fully (200 → 1000). Do **not** add FTS5 for 1000 items; it adds an index to keep in sync on every capture for a query that already costs 0.2 ms. Revisit only with paged history.

## B4. Data layer

> **Snapshot note:** `Core/` is changing under another agent (tabs landed during this research). Line numbers below are against the files read at the end of this session; function names are the durable reference. Anything marked "now" reflects tabs being present (`items.tab_id`, `PinnedTabId`, `GetRecent` = `tab_id IS NULL`).

| Concern | Ours | Verdict | Fix (effort) |
|---|---|---|---|
| PRAGMAs | none — ctor only opens + creates tables (`ClipboardStore.cs:14-35`) | **GAP** — 100 autocommit inserts 444–465 ms vs 10–15 ms after `WAL`+`synchronous=NORMAL` **[M]** | one-time in ctor: `journal_mode=WAL`, `synchronous=NORMAL`, `busy_timeout=3000`, `temp_store=MEMORY`, `cache_size=-8000` (8 MB), `mmap_size=0` (do not map 256 MB into a 150 MB app), `auto_vacuum=INCREMENTAL` **before** tables exist (or one `VACUUM` after) (S) |
| Write shape per capture | `Store()` → `Insert` → `InsertFormats` → `EnforceCap`, three implicit transactions (`CaptureService.cs:123-135`) | **GAP** | one explicit transaction around insert+formats+cap (S) |
| Eviction | every capture: `DELETE … OFFSET $m` **plus** `DELETE FROM item_formats WHERE item_id NOT IN (SELECT id FROM items)` (`ClipboardStore.cs:279-290`) | **GAP** — 18–88 ms @1000, 70–200 ms @5000 **[M]**, on the UI thread, every copy | run only when `COUNT(*) > max`; delete formats for the evicted ids (`DELETE … RETURNING id`, SQLite ≥3.35) instead of scanning the whole formats table (S/M) |
| Startup sweep | `Migrate()` re-runs the orphan-format `NOT IN` delete on **every** launch, before the tray appears (`ClipboardStore.cs:62-72`) | **GAP (small)** — a full formats scan is added to cold start; measured scale is tens of ms at 1000 items | keep the sweep but only when a cheap invariant check fails, or move it after the tray is ready (S) |
| Blob storage | `image` + `thumb` + every format BLOB in one row/child table | **RISK at 1000 with images** — DB 12.6 MB per 1000 rows with 50×200 KB images **[M]**; page cache and `VACUUM` grow with blobs, and every `GetRecent` row read touches the same pages | size-tiered: keep `thumb` (≈2.4 KB) and small text in DB; spill `image` and any `item_formats` blob > 64 KB to `%APPDATA%\Clip\blobs\<hash>.png`, store `hash`, `path`, `size` (M). Precedent: CopyQ `item_data_threshold` default **1024 bytes** → separate files, loaded on demand **[D]** FAQ; Ditto keeps raw blobs in `Data` **[S]**; Klipto (2026) stores images as hash-named files with a DB reference **[D]** |
| Index for the list | `ix_captured ON items(captured_at DESC)` and `ix_items_tab ON items(tab_id)` only (`ClipboardStore.cs:25,65`) | **GAP (small)** — ties on the same second force a sort; the tab index cannot provide `ORDER BY captured_at DESC, id DESC`; there is no `(kind, …)` index for the Images view | `(tab_id, captured_at DESC, id DESC)`, `(captured_at DESC, id DESC)`, `(kind, captured_at DESC, id DESC)` (S) |
| Dedupe lookup | text compares scan; images compare **BLOBs**; scoped `tab_id IS NULL` (`ClipboardStore.cs:112-127`) | **GAP** — image dedupe 10–13 ms @1000, 54–61 ms @5000 **[M]** and reads image bytes for every image copy | add `content_hash INTEGER NOT NULL` (CRC32 of text UTF-8 / of image bytes — the same hash dedupe needs anyway), index it: `(content_hash)` or `(kind, content_hash)`. Precedent: Ditto `Main_CRC` + `CREATE INDEX Main_CRC` **[S]** `DatabaseUtilities.cpp:484`; CopyQ hashes item data **[S]** `clipboardbrowser.cpp:1671` (S) |
| Timestamps | `TEXT` `yyyy-MM-ddTHH:mm:ss`, local time (`CaptureService.cs:76,97,221`) | **GAP** — second resolution breaks ordering ties and makes range filters (age retention) string comparisons | add `captured_ms INTEGER` (UTC ms) + index; keep the display string derived (S) |
| Free space after eviction | no vacuum strategy | **GAP** — deletes leave free pages; file never shrinks | `PRAGMA incremental_vacuum(N)` on idle; `VACUUM` only from Settings ("Compact database") (M) |
| Backup/export | none | see A2 #2 | `VACUUM INTO 'backup.db'` (safe with WAL, single file) (S) |

Size management verdict: with the current design, 1000 items ≈ **10–40 MB** without images and **tens to hundreds of MB** with them. Set a **total-bytes budget** (A2 #4) rather than trusting the count cap; a 200-item cap of 5 MB images is already 1 GB.

## B5. Startup / first open

| Concern | Ours | Verdict | Fix |
|---|---|---|---|
| Window built eagerly | `_main = new MainWindow(...)` at startup (`App.xaml.cs:52`) | **ALIGNED (trade-off)** — pays XAML+theme at startup so the hotkey is fast; costs RAM while unused | keep (it is the right call for a hotkey-first app) |
| Load before show | `ShowPopup()` → `Reload()` (`GetRecent`) → `ApplyFilter` → `Show()` (`MainWindow.xaml.cs:37-52`) | **GAP** — the user stares at nothing while we read 8 ms of DB **[M]** and rebuild 40–60 ms **[M]**; with a non-empty search also twice (B3) | `Show()` first, then populate on `DispatcherPriority.Background`; keep the previous list and refresh in place (S) |
| Cold DB read | `GetRecent(maxItems)` all columns | **ALIGNED at 1000** — 8 ms **[M]**; 5000 = 50–67 ms | no paging needed at 1000. Page only when history is uncapped (L) |
| Thumbnails | `thumb` bytes loaded for every row, decoded nowhere yet | **GAP (future Images tab)** — decoding 1000 thumbs = 265–482 ms and **+29 MB** **[M]** | decode lazily per realised row; frozen `BitmapSource`; LRU bound 200 entries ⇒ ≈6 MB (S/M) |
| `html`/`rtf` in the list query | selected for every row (`ClipboardStore.cs:179-209`) | **GAP at 1000 with rich content** — a Word table's CF_HTML can be 50–200 KB; 1000 rows = tens of MB of strings nobody displays until selection | drop `html`/`rtf` from `Query`; add `GetDetail(id)` called by the selected card/F2/paste (M) |
| Image capture work | full PNG `DecodePng` for the thumbnail **and** `EncodePng` both on the UI thread (`CaptureService.cs:206-212`); DIB fallback also encodes inline (`:198`) | **RISK** — a 4K screenshot decode+encode is 100 ms+ and freezes the app at the moment the user is watching | PNG bytes are already stored verbatim (good); move decode/thumb-encode/hash/insert to a background thread — freeze the source on the STA thread first (M) |

## B6. Memory discipline vs the owner's 150 MB / 250 MB targets

| Concern | Verdict | Action |
|---|---|---|
| Budget | idle ≤150 MB, peak ≤250 MB, bare WPF floor ≈111 MB (`CHECKLIST:26`) | treat ~40 MB as the working budget; every item below is sized against it |
| GC mode | **ALIGNED** — .NET defaults to Workstation + concurrent (background) GC for a non-server exe; the only lock-in is preventing an accidental future switch | make it explicit in `Clip.csproj`: `<ServerGarbageCollection>false</ServerGarbageCollection>`, `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>` (S) |
| Decoded thumbs | currently 0; naive cache = +29 MB/1000 **[M]** | LRU of **200** frozen thumbs (≈6 MB), evict on popup hide (S/M) |
| Big strings | `Text`/`Html`/`Rtf` for every row | `Preview` for binding + on-demand detail (B5) |
| ObservableCollection churn | 1000-item rebuild = **38–59 ms** **[M]**, repeated on every keystroke | debounce + swap; **do not** keep two full copies while filtering (S) |
| HtmlPanel teardown | planned "one alive at a time" (`CHECKLIST:55`) | dispose on selection change/popup hide; cap HTML at e.g. 2 MB before render; enforce with a counter in debug (M) |
| Toast windows | new `NotificationWindow` per capture (`App.xaml.cs:60-67`) — measured +35–40 MB per toast, 116→252 MB (`CHECKLIST:117`) | known must-fix in S3; nothing to add here |
| Working set | no artificial trimming | keep; do not call `EmptyWorkingSet` (it makes the next popup slower) |
| `Meta` property | builds a string + `DateTime.TryParse` per binding read (`ClipboardItem.cs:29-43`) | negligible at 7 realised rows; precompute at load **only if** grouping materialises all rows (S) |

## B7. CPU

| Concern | Verdict | Action |
|---|---|---|
| Idle CPU | **ALIGNED** — `WM_CLIPBOARDUPDATE` only, no timers except the 100 ms capture debounce (`CaptureService.cs:37-38`) | none |
| Scroll path | **ALIGNED** — no I/O in `GetRecent`/bindings, no per-row effects | rule: no DB call, no file read, no `Effect`, no animation inside the item template |
| Search debounce | **GAP** — none | 120 ms (PowerToys/Qt-style "natural pause"; sources cite 300 ms debounce on a 5000-note app for the same reason **[D]**). 120 ms keeps the popup feeling instant while cutting rebuilds ~3–5× |
| `EnforceCap` on every copy | **GAP** — 18–200 ms **[M]** | gate on count; move the orphan sweep to idle (S) |
| Image capture | **RISK** — full PNG decode for the thumbnail + re-encode on the UI thread | move decode/encode off-thread (B5) |
| Background priority | nothing uses it | `DispatcherPriority.Background` for list refresh/detail load; never `Send` (S) |
| Selection scroll | `ScrollIntoView` on every arrow key (`MainWindow.xaml.cs:120`) | fine at 1000 (39–65 ms settle **[M]**); avoid calling it twice per keypress (apply filter also resets selection) |

## B8. Other things that will bite at 1000 (found in source/measurement, not guesses)

1. **Grouping kills virtualization.** The planned Ortu "Today / Yesterday / This Week / Earlier" headers (`RESEARCH-ortu-ui.md:438`) are `CollectionViewSource` grouping. Without `VirtualizingPanel.IsVirtualizingWhenGrouping="True"` you get 1000 containers — the measured article (2026-10, .NET 10) shows exactly that for 10 groups **[D]**. Either enable + measure, or make headers pseudo-items.
2. **The Images tab grid has no virtualising wrap panel in WPF.** `WrapPanel` is not a virtualising panel. A 1000-thumb grid = 1000 `Image` elements, each decoding ≈27 KB → +27 MB and a slow open **[M] for the decode cost**. Options: page the grid (e.g. 60 at a time, "load more"), use a single-column `ListView` with `VirtualizingStackPanel`, or write a small `VirtualizingWrapPanel` (M/L).
3. **Rich preview must be the only rich thing.** CopyQ's 999-item hang was *rendering* (`libitemtext.dll`) and its 26 000-line JSON hang was converting text to rich text for **search highlighting** (issue #3340, fixed 2025-12 by disabling highlight on large items) **[S]**. Rules: one `HtmlPanel` alive, no HTML/rich text in the list, no search highlight above ~2 KB.
4. **Clipboard listener re-arm** is a reliability gap already logged in logic research (`ClipboardViewer.cpp:105-110`); at 1000 items it matters more because a silent listener stop looks like "history is broken".
5. **WAL file growth**: add `wal_autocheckpoint` default 1000 pages and a checkpoint on popup hide/exit, or a long-running session leaves a large `-wal` **[U]** for our exact workload, standard SQLite behaviour.
6. **Sliding out of the count cap deletes the formats row too** — with the current `NOT IN` sweep that's a full formats scan; a 10 MB item makes it worse. Covered in B4.
7. **A single 10 MB clip is legal today** (`MaxItemBytes`, `Config.cs:45`) and costs **64–111 ms** per relayout for 1 MB, **254 ms** for ~8 MB (B2), and seconds if virtualization ever breaks. Add a **display ceiling** independent of the storage ceiling.

## B9. Master verdict table (file-level)

Effort: **S** ≤ half a day · **M** 1–3 days · **L** > 3 days.

| # | Area | Our code | Verdict | Concrete fix | Effort |
|---|---|---|---|---|---|
| 1 | List virtualisation | `MainWindow.xaml:54-72` | ALIGNED | make explicit: `IsVirtualizing`, `Recycling`, `ScrollUnit=Item`, `CacheLength=1,1` | S |
| 2 | Row text binding | `MainWindow.xaml:77-79` | **RISK** | bind `Preview` (≤400 chars), not `Text` | S |
| 3 | Filter apply | `MainWindow.xaml.cs:67-72` | **GAP** | 120 ms debounce + `ItemsSource = new List` | S |
| 4 | Double rebuild on open | `MainWindow.xaml.cs:37-52,74` | GAP | clear search before reload / `_loading` guard | S |
| 5 | Show-before-load | `MainWindow.xaml.cs:37-52` | GAP | `Show()` then load at `Background` priority | S |
| 6 | Search algorithm | `SearchService.cs` | ALIGNED | none at 1000; FTS5 only when paged | — |
| 7 | List query columns | `ClipboardStore.cs:179-209` | GAP | drop `html`/`rtf`; add `GetDetail(id)` | M |
| 8 | PRAGMAs | `ClipboardStore.cs:14-35` | GAP | WAL, NORMAL, busy_timeout, cache_size, temp_store, mmap 0, incremental auto_vacuum | S |
| 9 | Per-capture write | `CaptureService.cs:123-135` | GAP | single transaction | S |
| 10 | Eviction | `ClipboardStore.cs:279-290` | GAP | count gate + `RETURNING` ids + idle orphan sweep | S/M |
| 11 | Dedupe | `ClipboardStore.cs:112-148` | GAP | `content_hash` column + index; hash images too | M |
| 12 | Timestamps/indexes | `ClipboardStore.cs:21-25,65` | GAP | `captured_ms INTEGER`; `(tab_id,captured_at,id)`, `(kind,captured_at,id)` | S |
| 13 | Blob tiering | `items.image`, `item_formats` | RISK @1000 w/ images | spill >64 KB to `blobs/<hash>` (CopyQ 1 KB threshold precedent) | M |
| 14 | Thumb cache | none yet | GAP (future) | lazy frozen `ThumbSource` + 200-entry LRU | S/M |
| 15 | Image capture thread | `CaptureService.cs:198,206-212` | RISK | decode/thumb-encode/hash/insert off-thread (PNG is already stored verbatim) | M |
| 16 | GC config | `Clip.csproj` | ALIGNED | explicit workstation+concurrent; consider `PublishReadyToRun` | S |
| 17 | Layered window | `MainWindow.xaml:4-6,28-30` (`AllowsTransparency` + root `DropShadowEffect`) | RISK (unmeasured for our size) | spike: drop the root `DropShadowEffect` (keep border + let DWM shadow), measure fps; keep transparency per UI authority | S/M |
| 18 | Grouped headers | planned | RISK | `IsVirtualizingWhenGrouping` + measure, or flat pseudo-items | M |
| 19 | Images grid | planned | RISK | paged grid or single-column virtualised list; no `WrapPanel` at 1000 | M |
| 20 | Rich preview lifecycle | planned | RISK | one panel, dispose on change/hide, 2 MB HTML cap | M |
| 21 | Search highlight | planned | RISK | only if added: cap at 2 KB (CopyQ #3340) | S |
| 22 | Listener re-arm | `ClipboardListener.cs` | GAP (existing) | 5-min re-arm timer (already in logic research) | S |
| 23 | DB size visibility | none | NICE | "1,000 clips · 84 MB" in Settings | S |
| 24 | Backup | none | MUST (Part A) | `VACUUM INTO` | S |

## B10. The 1000-item performance contract

These are the numbers S3/the audit should **enforce with evidence**, at `maxItems = 1000` (and spot-checked at 5000). They are derived from the measurements in B1, with headroom for the real app's theme/layered window. "How" is the measurement recipe, not a promise.

| # | Metric | Target at 1000 | How to prove it | Fails if |
|---|---|---|---|---|
| 1 | Realised rows | **≤ 20** `ListBoxItem` at top, middle and bottom of the list, at 1000 **and** 5000 | walk the visual tree of the `ListBox`, count `ListBoxItem` (recipe B1) | = item count |
| 2 | Popup open, warm | hotkey → **first frame ≤ 150 ms**; → **list populated ≤ 300 ms** | `Stopwatch` from `HotKeyWindow` callback to `ContentRendered`, and to the first populated layout; 20 runs, report p50/p95 | p95 > 2× target |
| 3 | Cold start | process start → tray ready **≤ 800 ms** **[U]** (no baseline measured) | `Stopwatch` in `Main` to end of `OnStartup` | > 1.5 s |
| 4 | Search keystroke | → filtered list on screen **≤ 50 ms p95**; pure filter work **≤ 5 ms** | instrument around `ApplyFilter` (excluding debounce) + `CompositionTarget.Rendering` for the visual | p95 > 50 ms |
| 5 | Search debounce | one rebuild per **120 ms** quiet period, not per keystroke | count `ApplyFilter` calls while typing 10 chars | 10 rebuilds |
| 6 | Scroll | no UI-thread layout **> 16 ms**, p95 **≤ 8 ms** during wheel/thumb scroll; **≥ 55 fps** while flicking 1000 rows | ETW/VisualDiagnostics `UIThread` + `Present` events; or a frame-time counter on `CompositionTarget.Rendering` | any single layout > 16 ms |
| 7 | Row height change | selected-row growth re-layout **≤ 5 ms** for one row | `Stopwatch` around `UpdateLayout` after `SelectedIndex++` | > 16 ms |
| 8 | Max string in a `TextBlock` | **≤ 512 chars** by construction (no full body ever bound) | code review + a debug assert in the `Preview` getter | a binding to `Text`/`Html`/`Rtf` in the list template |
| 9 | Idle RAM | **≤ 150 MB** after 10 min idle; **≤ 200 MB** with the popup open at 1000 items; **hard ceiling 250 MB** after the stress script | Task Manager / `Process.WorkingSet64`, Release, cold boot of the app (owner's protocol, `CHECKLIST:26`) | > 250 MB at any sample |
| 10 | 1000 vs 200 items | idle RAM delta **≤ +15 MB**; popup-open delta **≤ +20 MB** | same script with `maxItems` 200 and 1000 | delta > 2× |
| 11 | Decoded thumbs | cache **≤ 200 entries / ≤ 10 MB**; **0** full-image decodes for the list | counter in the thumb cache + `GC.GetTotalMemory`/working-set delta | cache grows with item count |
| 12 | Capture, text | UI-thread handler **≤ 16 ms** (excluding the 100 ms debounce wait) | `Stopwatch` inside `OnCaptured`/`Store` | > 50 ms |
| 13 | Capture, image | decode/thumb-encode/hash/insert **off the UI thread**; UI-thread part **≤ 16 ms** | thread id assertion + timings | PNG decode/encode on the UI thread |
| 14 | Capture write | one transaction; **≤ 20 ms** per capture at 1000 | SQLite `Stopwatch` around the transaction (expect ≈0.1–1 ms after WAL **[M]**) | 3 separate commits |
| 15 | Eviction | **≤ 30 ms** or deferred to idle; never on every copy when under the cap | `Stopwatch` around `EnforceCap` | > 100 ms on the UI thread |
| 16 | Dedupe | text **≤ 5 ms**, image **≤ 5 ms** at 1000 (hash lookup) | `Stopwatch` around `TryDedupeMoveToTop` | image path > 20 ms (current 10–13 ms **[M]**, 54–61 ms @5000) |
| 17 | DB size | free pages **< 10%** of the file; file **≤ 2×** the stored payload; a "Compact" action returns it | `PRAGMA page_count`, `freelist_count`, `page_size` | file keeps growing after eviction |
| 18 | WAL | `-wal` **≤ 8 MB** after a normal session; checkpointed on hide/exit | file size check | `-wal` grows without bound |
| 19 | Retention | a 1000-item DB with default retention stays **≤ 250 MB** including images | run the capture stress script, read the file size | unbounded |

**Stress script (same for every run):** empty DB → programmatically insert 1000 mixed items (70% text 100–400 chars, 20% rich text with a 2–8 KB HTML body, 10% images ≤ 200 KB) → open popup → type 8 characters one at a time into search → clear → arrow down 50 rows → End → scroll wheel 200 ticks → open 5 rich previews → switch tabs twice → capture 20 new items → sample working set after each phase. Record p50/p95 and the max single layout.

Rule that makes the contract meaningful: **no item may be marked green from a code read.** Every row above needs a measured value in the audit, on a Release build, on the owner's machine.

## B11. Deliberately not doing (so the plan stays small)

- **No FTS5 at 1000** — 0.2 ms filter vs an index to keep consistent on every capture.
- **No paging / virtual data provider** while `maxItems` is a count we load fully. Paging is the price of "unlimited history", and unlimited history is not a current goal.
- **No custom virtualising wrap panel** until the Images tab is actually measured; page the grid first.
- **No `WindowChrome` rewrite** of the popup; the layered-window cost gets a spike and a decision, not a redesign (UI authority is `RESEARCH-ortu-ui.md`).
- **No `EmptyWorkingSet`/working-set trimming**, no `GC.TryStartNoGCRegion`, no custom allocator tricks.
- **No SQLCipher / encryption at rest** in the performance milestone; it is a separate trust decision with a dependency and a key-management story.
- **No caching of decoded thumbs for all items**, even though Ditto caches all thumbnails (`QListCtrl.cpp:1167-1168`). Ditto's rows are ~50 px C++ DIBs; ours would be 27 KB managed bitmaps × 1000 = +29 MB **[M]**.

---

## Sources

**Measured this session [M]** — standalone WPF/SQLite harness, .NET SDK 10.0.404, `net10.0-windows`, Release, Windows 11, 560×620 window; SQLite 3.53.4 via Microsoft.Data.Sqlite 11.0.0. Harness kept outside the repo; recipe in B1/B10.

**Clip repository [S]**
- `UI/MainWindow.xaml:4-6,9-23,28-30,54-86` — window flags, card style, root shadow, list + template.
- `UI/MainWindow.xaml.cs:37-52,54-59,61-65,67-72,74,94-136` — open path, reload, filter, key handling.
- `Core/ClipboardStore.cs:14-35,38-81,83-105,112-148,179-209,228-247,279-290` — no PRAGMAs, migration + startup sweep, insert, dedupe, list query, formats, eviction (tabs present: `tab_id`, `PinnedTabId`).
- `Core/CaptureService.cs:37-38,53-106,123-135,154-224,258-295` — debounce, capture, store, image path (PNG-first, header guards), decode/encode/thumb.
- `Core/ClipboardItem.cs:29-43` — `Meta` on every binding read.
- `Core/Config.cs:29,31,43,45` — 200 items, preview lines, 5 MB image, 10 MB item budget.
- `App.xaml.cs:34-58,60-67,69-80` — startup, toast per capture, paste timer.
- `Clip.csproj:1-32` — no GC/publish settings.
- `docs/CHECKLIST-features.md:26,55,61-70,117` — RAM targets, one-panel rule, image grid, tabs, toast leak.

**Ditto 3.25.x (GPL — reference only) [S]**
- `CP_Main.rc` — Quick Paste, Global Hot Keys, Backup/Restore Database, Import/Export Clip(s), Groups/Send To, Special Paste (15 transforms), Compare, Search modes (contains/regex/wildcard), Quick Properties (Never/Auto delete), sticky clips, QR code.
- `src/DatabaseUtilities.cpp:479,484,700,761,766,862,930` — `Main_TopLevel` order index, `Main_CRC` hash index, `PRAGMA auto_vacuum=1`, `LIMIT -1 OFFSET` eviction, two-phase `MainDeletes` batch.
- `src/QListCtrl.cpp:1167-1168,1782` — owner-data list (`LVS_OWNERDATA`), all thumbnails cached in RAM.
- https://sourceforge.net/p/ditto-cp/discussion/287511/thread/759abcaf — maintainer advice: "having thousands of clips can lead to a slow down… limit clips to 500"; user report of a 4 GB DB.

**CopyQ (GPL — reference only) [S]/[D]**
- `docs/faq.html` (copyq.readthedocs.io) — items are kept in memory per tab; `item_data_threshold` default **1024 bytes**, larger data in separate files loaded on demand; unload-tab interval.
- Issue #600 — CopyQ's author: a per-tab cap exists (200 default) and "all items from current tab are loaded to memory", so unlimited history was refused.
- Issue #1238 — 999 items of 10–100 KB: search hangs; cause: rendering every item's text.
- Issue #3340 — 26 000-line JSON clip hangs search; fix: disable highlighting for large items (2025-12).
- `docs/pin-items.rst`, `src/gui/clipboardbrowser.cpp:1568-1593,1671-1695` — pin/eviction/hash dedupe (as cited in `RESEARCH-tabs-pinning.md`).

**PowerToys (MIT) [D]**
- https://learn.microsoft.com/en-us/windows/powertoys/advanced-paste (2026-11-17) — plain text, Markdown, JSON, image-to-text (local Windows OCR, no AI), paste-as-file, AI providers incl. local.

**ClipboardFusion [D]**
- https://www.clipboardfusion.com/help/ — History/Pinned/Online tabs, Macros, Triggers, HotKeys, "Type Text", max-items pruning, database location.

**ArsClip [D]**
- https://joejoesoft.com/vcms/97/ — Permanent Clips/Macros, groups, JavaScript clips, per-program groups, pinned hotkeys.

**Maccy (macOS) [D]**
- https://github.com/p0deje/Maccy README — paste / paste-without-formatting, pin assigns a permanent random shortcut, ignore-next-copy, clear unpinned vs all.

**WPF / SQLite mechanism evidence**
- Microsoft Learn, *How to: Improve the Scrolling Performance of a ListBox* — `VirtualizationMode.Recycling`.
- Microsoft Learn, *Optimizing Performance: Controls* — what disables virtualisation (containers as items, different container types, `CanContentScroll=false`).
- https://s-iguchi09.github.io/articles/wpf-ui-virtualization-lost-conditions (2026-10-03, .NET 10 measured) — 1000 items: 11 containers in a `*` grid row, 1000 in a vertical `StackPanel`, 1000 with `CanContentScroll=false`; grouping without `IsVirtualizingWhenGrouping` also materialises everything.
- https://stackoverflow.com/questions/79846992 — `ScrollUnit=Pixel` + random row heights: thumb dragging becomes slow at 100k–5M rows (not at 1000).
- https://blog.walterlv.com/post/wpf-transparent-window-without-allows-transparency.html — measured `WindowChrome` vs `AllowsTransparency`: 59 vs 19 fps, 83 vs 193 MB VRAM (full-screen animation, 2560×1080).
- https://blog.lindexi.com/post/WPF-从最底层源代码了解-AllowsTransparency-性能差的原因.html — WPF's layered-window path copies the frame out of D3D and presents via `UpdateLayeredWindow` (GDI), i.e. a per-frame CPU copy sized to the window.
- https://cstsolution.com/blog/sqlite-fts5-offline-search/ (2026-08) — LIKE 31 ms vs FTS5 0.017 ms on 20 000 docs; index-size trade-offs.
- https://andrewmara.com/blog/faster-sqlite-like-queries-using-fts5-trigram-indexes — FTS5 `trigram` accelerates `LIKE '%x%'` 50–100×; the right tokenizer for substring search.
- https://www.sqlite.org/pragma.html — `journal_mode`, `synchronous`, `cache_size`, `mmap_size`, `auto_vacuum`, `wal_autocheckpoint`.

## Unverified / open

- **[U]** Real-app fps with `AllowsTransparency` + root `DropShadowEffect` at 560×620. The 19-vs-59 fps figure is a third-party full-screen measurement; our window is 0.4% of that pixel count, so the absolute cost is unknown. Spike before changing anything.
- **[U]** Cold-start-to-tray baseline for this app (no measurement exists; 800 ms is a target, not a fact).
- **[U]** Win+V's current caps (≈25 items, ~4 MB) — Microsoft's support page was not re-fetched here; both are already marked [U] in `RESEARCH-logic-best-practices.md:105`.
- **[U]** Whether a `net10.0-windows10.0.19041.0` TFM change (needed for WinRT OCR) breaks any current package/`UseWindowsForms` combination — needs a spike.
- **[U]** WAL `-wal` growth for our workload (standard SQLite behaviour, not measured here).
- **[U]** Exact `ScrollIntoView` CPU cost at 1000: the 33–65 ms figures are *settle* times including a render pass, not isolated CPU.
- **[U]** Dedupe formula choice (CRC32 vs xxHash64). CRC32 matches Ditto; note `System.IO.Hashing.Crc32` is a **NuGet package**, so honouring the "no other dependencies" rule means ~30 lines of table-driven CRC32 (or reusing the existing byte compare as a second check). Collision behaviour on deliberate collisions was not analysed.
