# Clip — Settled checklist history

Everything here **passed** and its code has not changed since, so it is no longer a checklist row
(24 Nov: 200+ rows → open-only files). Kept one line each so nothing is lost and nothing gets
re-litigated. Evidence paths are relative to `docs\design-evidence\` unless absolute. Rows under
"Retired" were removed because their surface is gone, not because they passed.

A row belongs here only when it **cannot fail any more**. If the code under it changes, the row
moves back into a checklist.

## Retired 25 Nov (v2 page replaced the WPF surface)
- `ClipRow` 3-line clamp + `previewLines` outside-tooltip sizing · WPF row, unreachable; v2 clamps at 10
- Identity tile, facts column, no-meta row · WPF anatomy, unreachable
- `CanContentScroll=True` + `VirtualizationMode=Recycling` through the row redesign · WPF list, unreachable
- Search 120 ms debounce + swap the collection instance (`Clear`+`Add` of 1,000) · WPF ItemsSource; v2
  debounces and replaces the array — full-body search is proven at 9.3 ms p50 (`AUDIT-release-logic-2` §1.2)

## Core capture & storage
- Tray-resident, no window at startup; Exit only via tray · `ShutdownMode=OnExplicitShutdown` · ACC-v2
- Tray icon reachable, left-click opens popup, right-click opens menu · S3 audit
- Single-instance mutex, second launch exits
- Text capture on WM_CLIPBOARDUPDATE with app / title / ISO time · ACC-v2
- `minTextLength` and whitespace-only skips; password-manager ignore formats checked before storage
- SQLite `%APPDATA%\Clip\clip.db`, schema + `rtf` + `item_formats`, idempotent migration
- 200-item cap: unpinned eviction, formats purged, pinned never evicted
- Idle CPU zero (event driven); no polling
- RAM: idle ≤150 MB (settles 174 WS / 92 priv), ceiling 250 MB, no growth over time; the 344–350 MB
  leak was found and fixed (NotificationPreviewWindow root + per-open Reload)
- WAL + `synchronous=NORMAL` (8.0 ms / 100 inserts, mutation → 103.7 ms)
- `InsertWithFormats` + dedupe in one transaction (mid-failure → 0 partial rows)
- `GetRecent` drops html/rtf; `GetRich(id)` loads them
- `image_hash` + BLOB confirm; `EnforceCap` count-gated; corrupt-db rename + recreate
- `Restore(item)` byte-identical undo; `ClipboardStore` split into 3 partials (≤324 lines each)

## Popup & keyboard
- Alt+V hotkey, rebindable, conflict message · ACC-v2
- Centered on the active window's monitor, PerMonitorV2 at 150 % · ACC-v2
- Search focused on open, live case-insensitive filter over text + app name
- ←/→ switch tabs everywhere incl. Images; ↑/↓ step images; Home/End first/last · KEYS sprint
- Esc #1 clears search, Esc #2 hides · ACC-v2 · Esc-collapse is instant by design
- Focus loss hides popup · ACC-v2
- ↑/↓ navigation with zero geometry change (0 differing pixels)
- Enter pastes; Ctrl+1..9 pastes nth · ACC-v2 7/7
- Delete = item in list, character in edit mode · ACC-v2 · Ctrl+Z one-level undo
- Side rails: outer 10 DIP, list 2 DIP, row inset 8 DIP · POPUP-VISUAL + INTEGRATION
- Bottom bar: `App · Title · 23 Nov 21:19`, selection-driven, title trims first
- Draggable popup · ACC-v2 · type-to-search from the list · ACC-v2
- Shift+Enter plain paste; Ctrl+C copy-without-paste · ACC-v2
- Context menu on rows (right-click, Shift+F10) with the seven commands · ACC-v2
- Editor content inset 8 DIP on all sides, toolbar aligned to text · KEYS+EDITOR
- Outside tooltip: only truncated rows, docked to the popup's right (or left near the edge),
  non-activating, hover + keyboard dwell · POPUP-VISUAL
- Zebra + hairline + EdgeMask: 5 scroll positions, both themes, no sliced glyph
- Clipboard tab is kind-filtered; images only in Images
- Row-tooltip and outside-tooltip never show for non-truncated rows

## Paste & content
- All-formats snapshot with 10 MB budget; clipboard-busy retry; self-paste suppression
- Plain-text hotkey Ctrl+Shift+V works globally · S3
- `RtfText` Latin-1 envelope + `HtmlToRtf` (38/38, 2.12 ms @32 KB)
- Rich content renders inside every realised row (tables → tabs, colours, weights) · ACC-v2

## Images
- PNG read first (alpha survives 24/24), real CF_DIB written on paste
- Pixel-count guard (50 MP / 16384 px, 4 ms) and human skip reasons
- Thumbnails capped on both axes; Images tab preview 383×247 + numbered filmstrip · ACC-v2
- Notification image thumb renders · ACC-v2 (measures 97×51, spec was 40×40 — cosmetic)
- F2 opens the image in the default program via `ImageCache` · ACC-v2

## Tabs & pinning
- Schema + `ix_items_tab` + seeded Pinned; 75/75 harness
- Migration adopts `pinned=1` then retires the flag (idempotent)
- Pinned/custom are containers, Clipboard/Images are views
- Tab CRUD: create/rename/delete with purge count; Pinned refuses delete/rename
- Never deleted structurally (EnforceCap/Clear scoped to `tab_id IS NULL`)
- Dedupe scoped per tab; pinned keep their pin on move to top
- Same-second re-copy moves to top (ms stamps)
- Ctrl+M picker, Ctrl+T / Ctrl+W · S3
- Tab strip UI: flat labels, counts, 2 px underline · S3 audit

## Editor
- F2 opens formatted content with caret in content · ACC-v2
- Ctrl+S saves and closes; row re-renders in place; focus returns to the list · ACC-v2
- Esc/F2 with changes → discard prompt; no changes → silent close
- ONE rich editor for every text clip; >2 MB refused with a notice; malformed RTF → plain + notice
- Formatting toolbar (B/I/U/S, size, colour, highlight, clear) with Span-chain caret state
- Edits replace the stored format blobs; html regenerated only if the clip had html

## Dedupe
- Exact = case/space/Unicode-composition sensitive; identical image bytes dedupe
- One entry, moved to top, fresh timestamp/app/formats

## Settings
- Window renders (the P0 InvalidCastException is fixed), tabs General/Notification, 48 DIP header
- Hotkey parser fixed; rebind Alt+V → Ctrl+Alt+K and back · ACC-v2
- Live apply: theme, hotkey, time format, autostart, clear history · ACC-v2
- Clear history is an inline two-click swap and wipes main history only · ACC-v2
- Autostart HKCU Run round-trip via `Environment.ProcessPath` · ACC-v2
- `Config.PreviewLines` landed; full build 0/0

## Notification
- Own WPF window, silent, topmost, never steals focus · ACC-v2
- 420×160 DIP honoured; single window reused for rapid copies; ✕ dismisses · INTEGRATION
- Self-attribution fixed (own-process copies never name an internal window)

## Infrastructure
- Renamable in one place (`<AssemblyName>`); PerMonitorV2 manifest
- H.NotifyIcon tray with a themed menu (no Aero2 gutter) · ACC-v2
- CodePages provider registered at startup
- Dependencies resolve, 0 warnings (Syncfusion `SfRichTextBoxAdv` is the editor now;
  `HtmlRenderer` and `RtfPipe` are gone — AGENTS.md still lists the old set)
- Text crispness: ClearType on every reading window (fringed pixels 0 → 2039 popup, 1760 toast)
- Static Inter 400/500/600 embedded; variable-axis fallback root cause removed
- Contrast tokens existed per `DESIGN-contrast.md` — **superseded** by `DESIGN-language.md` v3,
  whose values are computed and pass 28/28 pairs
