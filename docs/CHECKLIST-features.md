# Clip — Feature Verification Checklist (frozen contracts)

**How to use:** every item below must be verified with OBSERVED EVIDENCE before it is checked off:
- `[H]` = scratch-harness assertion (component-level, per the verify-parts-first rule)
- `[L]` = live in-app verification (running app, screenshot or measured value)
- `[U]` = UI audit visual verification (screenshot read)

**Status legend:** ✅ done + verified · 🟡 built + component-verified, awaiting S3 wiring · ⬜ S3 to build · 🔬 research pending (ADOPT list)

No item may be marked from a sub-agent's self-report alone.

**VERIFICATION PROTOCOL (owner-mandated):** any check that requires looking at the rendered app (`[U]`, and visual parts of `[L]` such as screenshots) is executed by a FRESH sub-agent on `opencode-go/deepseek-v4.1-flash#high` — fresh session, never one that wrote or orchestrated the code (no self-review). It launches the app, captures and reads its own screenshots, and marks items with evidence. Builder agents may only mark `[H]` harness assertions and `[L]` measured values from their own scratch verification.

---

## 1. Core capture & storage (M1 spec)
- ✅ `[H]` Tray-resident app, no visible window at startup; closing/hiding never exits (Exit only via tray) — **plus S0's runtime fix: `ShutdownMode="OnExplicitShutdown"` (the app previously exited silently after the first toast closed)**
- ⬜ `[U]` Tray icon visually confirmed — S0 created it without error but couldn't verify from Win11's hidden-icons flyout in 3 attempts; owner eyeball or S3 audit with the icon pinned outside overflow
- ✅ `[H]` Single-instance mutex — second launch shows a message and exits
- 🟡 `[L]` Text capture on WM_CLIPBOARDUPDATE with: source app name, window title, local ISO timestamp
- 🟡 `[L]` Copies under `minTextLength` (default 3) skipped; whitespace-only skipped
- 🟡 `[L]` Password-manager exclusion: `Clipboard Viewer Ignore` + `ExcludeClipboardContentFromMonitorProcessing` formats never stored (check runs BEFORE any storage, incl. format snapshot)
- ✅ `[H]` SQLite `%APPDATA%\Clip\clip.db`, spec §5 schema + `rtf` column + `item_formats` table; idempotent migration on existing DBs
- ✅ `[H]` 200-item cap (config `maxItems`): oldest evicted, format blobs purged, zero orphans — **eviction never removes pinned rows** (core-fix verified; cap semantics: bounds UNPINNED items, pinned are extra — owner confirmation pending, flip to cap-on-total if wanted)
- ✅ `[H]` Zero idle CPU (event-driven, no polling); hotkey→popup < 150 ms; measured live in S3 acceptance
- 🟡 `[L]` **RAM (owner-approved): idle ≤ 150 MB ✅ PASS (S3 live: 111–113 MB) · hard ceiling 250 MB ❌ FAIL (S3 live: peak 253.7 MB across 25 rich previews, 238–240 MB retained after hide; panel-drop fix already saved 5 MB)** — fix sprint closes the last ~4 MB of rich-preview retention and re-measures under the full protocol

## 2. Popup & keyboard contract (owner-frozen)
- ✅ `[L]` Alt+V global hotkey, rebindable via config/settings; hotkey conflict shows a clear message
- ✅ `[L]` Popup centered on the monitor of the **CURRENT ACTIVE WINDOW** (owner requirement, multi-monitor: `GetForegroundWindow` → `MonitorFromWindow`, fallback to cursor position only when no foreground window) — DPI-correct (PerMonitorV2 + ScreenDpi). S3 changes this from the current cursor-based rule
- ✅ `[L]` Search box focused on open; typing filters live, case-insensitive substring over text + app name
- ⬜ `[L]` **←/→ switch tabs** (Clipboard → Images → Pinned → custom…), selection resets to first item
- ⬜ `[L]` **Esc #1 clears search** (list returns, caret stays in search); **Esc #2 hides popup**
- ⬜ `[L]` Focus-loss (clicking away) hides popup
- ⬜ `[L]` ↑/↓ navigate items, selected item scrolls into view
- 🟡 `[L]` **Enter pastes selected — VERIFIED live in S3** (sentinel text landed in Notepad, focus returned); Ctrl+1..9 pastes nth visible item ⬜ (not yet tested live)
- ⬜ `[L]` **Delete in list context = delete the item** (CRUD); in **edit mode = character right-delete**, never the item
- ⬜ `[L]` Popup is **draggable** (drag anywhere on the background)
- ⬜ `[L]` Search placeholder text visible when empty
- ⬜ `[L]` Visible keyboard-focus treatment on the selected row
- 🟡 `[L]` Newest first ordering (captured_at DESC, id DESC)
- ⬜ `[L]` **Home/End** → first/last item (only when search is EMPTY — the caret keeps them while typing); **PageUp/PageDown** → one viewport of rows, clamped, never wraps
- ⬜ `[L]` **IME composition gate**: while a Hindi/Chinese/Japanese composition is open, Esc cancels the composition (NOT the popup) and Enter commits it (never pastes) — docs\RESEARCH-keyboard-ux.md; protects real input work
- ⬜ `[L]` **Type-to-search from anywhere**: a printable key while focus is on the list redirects into the search box (closes the dead state after F2 closes); **Tab/Shift+Tab = no-op** in the popup
- ⬜ `[L]` Aliases: **Ctrl+Tab / Ctrl+Shift+Tab** also switch tabs; **Ctrl+↑/↓** jump between time sections (Today/Yesterday…)
- 🟡 `[L]` **Shift+Enter = paste selected as PLAIN text** ⬜ (not yet tested live); **Ctrl+C = copy-without-paste — VERIFIED live in S3** (clipboard read back `COPY-BACK-ONE 1`, no paste fired)
- ✅ `[L]` **Ctrl+Z = one-level undo of item deletion — VERIFIED live in S3** (Delete removes the row, Ctrl+Z restores it; Core `Restore(item)` round-trip already proven `[H]`)
- ⬜ `[L]` **Context menu on rows** (right-click / Menu key / Shift+F10) — one surface exposing the seven existing commands (paste, paste-plain, edit/pin/move-to-tab, delete, open-image)
- ⬜ `[L]` **Esc cascade, one ordered function**: IME → editor discard prompt → tab-delete confirm → tab-name editor → Ctrl+M picker → F2 editor → clear search → hide popup
- ⬜ `[U]` Selection stays stable when a live capture arrives while the popup is open

## 3. Paste fidelity (owner: "paste exactly like I copied, for EVERYTHING")
- 🟡 `[L]` All-formats snapshot on copy (HGLOBAL bytes per format, budget `maxItemBytes` 10 MB, handle formats skipped knowingly)
- 🟡 `[L]` Restore-first paste: every stored format back on the clipboard → Excel gets Biff12, Word RTF/HTML, browsers HTML — **live Excel→Excel columns/rows test is S3's FIRST live test**
- 🟡 `[H]` Plain text + RTF + CF_HTML columns stored verbatim (header kept intact for round-trip)
- ✅ `[H]` Clipboard-busy retry: ~5 attempts on locked clipboard (set + snapshot paths)
- ✅ `[H]` Self-paste suppression via clipboard sequence number (our own pastes never re-enter history)

## 4. Plain-text paste (CopyQ-style)
- ✅ `[L]` Global **Ctrl+Shift+V** (rebindable, config `hotkeyPlain`): newest item pasted as raw text — VERIFIED live in S3 (clipboard formats before `…, HTML Format` → after `String, UnicodeText, Text`)
- ⬜ `[L]` Same shortcut with popup open = selected item as plain text

## 5. Rich text (render like rich text)
- 🟡 `[U]` Selected card expands into a rich preview: tables, colors, font sizes, line breaks via HtmlRenderer.WPF; ONLY the selected card renders (list stays fast, one panel alive)
- ⬜ `[H]` CF_HTML byte-offset fragment extraction (Unicode-safe; non-ASCII torture test) — `Interop\HtmlFragment.cs`
- ⬜ `[U]` Remote images blocked in preview (data: URIs allowed) — no network fetch on open
- ⬜ `[U]` RTF-only items render via RtfPipe → same renderer (fallback path)
- ⬜ `[U]` Known ceilings documented (verified in spike): color emoji render as tofu glyphs in preview; Google Docs/Notion HTML degrades; rowspan=4 did NOT reproduce in 1.6.1 (upstream bug may not apply) — paste-back fidelity unaffected in all cases

## 6. Images
- ✅ `[H]` Image capture: PNG ≤ `maxImageBytes` (5 MB default), 96px thumbnail, kind='image'; text never in image kind
- ✅ `[H]` **Alpha preserved at capture**: "PNG" clipboard format read FIRST (byte-verbatim storage, pixel alpha survives), GetImage only as fallback — 24/24 harness
- ✅ `[H]` **Paste-back sets a real CF_DIB** (24-bpp bottom-up, white-flattened, DWORD-aligned stride) + PNG verbatim; image branch runs BEFORE Formats (a stored "PNG" blob can no longer own an image paste); `SetImage` removed
- ✅ `[H]` **Pixel-count guard before decode** (50 MP / 16,384 px per side — patched-IHDR bomb rejected in 4 ms) + over-limit skips fire `Skipped` with human reasons, never silent
- ✅ `[H]` Thumbnail capped on BOTH axes (100×4000 → 2×96, 1920×1080 → 96×54)
- 🟡 `[L]` Images tab: thumbnail grid, click/Enter copies image back; images auto-routed, text never appears there
- ⬜ `[U]` **Notification shows the image thumbnail** — 40×40 DIP, DecodePixelWidth=96, Uniform, from `item.Thumb` never `item.Image` ("Image copied" fallback if thumb missing)
- ⬜ `[L]` **F2 on an image opens it in the default program** — `ImageCache` landed (write-if-changed verified; startup cleanup: >7 days / newest 100, never delete on exit) — S3 wires it into App startup
- 🟡 `[L]` Memory discipline: 200-row list carries thumbs only; full PNG loaded on demand
- ⬜ `[L]` Office/Paint acceptance matrix run LIVE in S3 (hand-built DIB → Excel, Word, Paint, browser); CF_DIBV5 added only after the matrix passes

## 7. Tabs & pinning (design frozen in docs\RESEARCH-tabs-pinning.md)
- ✅ `[H]` Schema: `tabs(id, name NOCASE UNIQUE, kind, sort_order, created_at)` + `items.tab_id` (idempotent ALTER) + `ix_items_tab`; one built-in **Pinned** row seeded (`kind='builtin'`, 24-char name validation) — 75/75 harness
- ✅ `[H]` Migration adopts existing `pinned=1` rows into Pinned and retires the flag (re-run harmless, byte-identical state) — ONE protection mechanism, structural
- ✅ `[H]` Two tab kinds: Clipboard/Images are **views** over main history (Images adds `kind='image'`; `GetRecent` = main-only); Pinned/custom are **containers** (`GetTabItems`)
- ⬜ `[L]` **Ctrl+P = move to/from Pinned** — store semantics ✅ (`TogglePin` both directions, formats survive); the key binding is S3 UI
- ✅ `[H]` **Never deleted, structurally**: EnforceCap AND Clear are `tab_id IS NULL`-scoped; pinned/tab rows AND their formats survive; startup orphan sweep kept
- ✅ `[H]` **Dedupe scoped per tab**: a pinned copy never swallows a fresh history copy (verified head-to-head)
- ✅ `[H]` Tab CRUD: create/rename (blank/duplicate → `ArgumentException`/`false`); **delete purges items+formats and returns the count**; Pinned rejects delete/rename (`-1`/`false`) — the macOS keep-clips model is explicitly rejected
- 🟡 `[L]` Keyboard: **Ctrl+M tab-picker — VERIFIED live in S3** (opens, Esc closes); **Ctrl+T/Ctrl+W tab CRUD — VERIFIED live in S3** (created `work`, duplicate shows the red error, empty tab deletes, built-ins refuse); **Ctrl+Shift+←/→ adjacent-move ⬜** (not yet tested live); Ctrl+1..9 stays paste-nth ⬜
- ✅ `[H]` **Same-second re-copy moves to top** — FIXED red-first (millisecond stamps `yyyy-MM-ddTHH:mm:ss.fff`, still lexically sortable; pre-fix observed `B,A` → post-fix `A,B`; mixed old/new format sort verified; Update/F2 also emits ms)
- ✅ `[H]` **Pinned flag RETIRED** — single source of pin state is `tab_id`; migration adopts legacy pins then clears the column; nothing reads it again (mutation-verified)
- ⬜ `[U]` Tab strip: flat text + 2px accent underline (no TabControl pills), per-tab counts ("Pinned · 4")
- ⬜ `[U]` ←/→ walks all tabs: Clipboard → Images → Pinned → custom…

## 8. F2 editor (owner-frozen)
- ⬜ `[U]` F2 opens editor for selected text/rich item; caret in content
- ✅ `[L]` **Ctrl+S saves & closes, list refreshed in place — VERIFIED live in S3** (typed edit, row updated in place with fresh timestamp)
- ⬜ `[L]` **Esc or F2 with unsaved changes asks "Discard changes?"** (Yes=close without saving, No=keep editing); no changes → closes silently
- ⬜ `[L]` Rich items edit with formatting preserved (RichTextBox); plain items in multiline TextBox
- ⬜ `[L]` On close, focus returns to the list at the same item
- ⬜ `[H]` Edits update text/html/rtf + timestamp and REPLACE the stored format blobs (never restores pre-edit Excel data) — already proven `[H]` in S1

## 9. Dedupe (owner-frozen: EXACT = case-sensitive, space-sensitive, byte-exact)
- ✅ `[H]` Re-copy existing text → exactly ONE entry, moved to top, fresh timestamp/app/formats; old entry gone
- ✅ `[H]` Case/space/Unicode-composition differences are DIFFERENT items
- ✅ `[H]` Identical image bytes dedupe identically
- ✅ `[H]` Pinned items keep their pin when moved to top
- ✅ `[H]` **Same-second re-copy moves to top** — fixed red-first (millisecond stamps `yyyy-MM-ddTHH:mm:ss.fff`); pre-fix observed order B,A → post-fix A,B; mixed-format sort verified
- ⬜ `[U]` S3 fix: popup must not briefly double-show a deduped card (OnNewItem removes existing id before inserting at top)

## 10. Settings & autostart (owner-frozen)
- 🟡 `[U]` Settings window: hotkey, plain-hotkey, max items, preview lines, notification (enabled/duration/width/height/position/offsets/maxLines), theme (dark/light/system), **time format 12h/24h**, start-on-login, clear history (with inline confirm) — S2 built + verified 69/69
- ✅ `[H]` **Config.PreviewLines landed** (=10, JSON `previewLines`) — Axis-2; full build 0 errors / 0 warnings (forced recompile via OutDir redirect)
- 🟡 `[L]` **HotKeyWindow parser — FIXED in S3** (explicit friendly-name → VK map: digits, F-keys, OEM row; no silent fallback; exposes registration result) — live rebind test pending (Settings window not yet opened at runtime)
- ⬜ `[L]` Settings wiring: tray Settings menu opens the window (currently disabled placeholder); SettingsSaved applies live (theme swap, hotkey re-register incl. conflict message, ClockFormat, notification); ClearHistoryRequested wipes MAIN HISTORY ONLY (tabs survive per §7)
- ⬜ `[U]` Notification settings include the **live on-screen preview overlay** (exact placement, live updates, drag-to-set offsets, auto-hide + pin) and the Settings window is reorganized into tabs (General / Notification) — owner requirement 23 Nov; public API unchanged: ctor(Config), SettingsSaved, ClearHistoryRequested
- 🟡 `[H]` Time format: `config.json "timeFormat"`, footer + notification honor 12h/24h; WasNew excludes from JSON
- ⬜ `[L]` Settings apply LIVE — no restart
- ⬜ `[L]` Autostart: HKCU Run value named after the product, EXE path via `Environment.ProcessPath` (NOT Assembly.Location — that's the DLL and won't launch); toggle removes it — real registry round-trip `[H]` already PASS
- ⬜ `[L]` First-run hint: first ever launch shows "…is running — press Alt+V" (uses WasNew)

## 11. Notification (spec §4.4, silent, custom)
- ✅ `[U]` Own WPF window — never a native toast; silent (no sound, no system notification)
- ✅ `[L]` Copy shows: app name + timestamp + first ~3 lines truncated (S0 screenshot: "WindowsTerminal · 14:33:40 · Clip M1 verification text 12345")
- ✅ `[L]` Borderless, rounded, topmost; fade in/out ~150 ms; auto-dismiss on duration timer (3 s observed)
- ⬜ `[U]` **Clickable ✕** (per-region hit test; window stays click-through everywhere else; no focus stealing) — **keyboard-reachable and UIA-labelled** (audit: today it is a bare 13×15 TextBlock with no accessibility)
- ✅ `[H]` **Self-attribution leak fixed** — own-process copies attribute app=product, title="" (`ForegroundApp.Attribute` pure function, tested: internal title + own pid → `(ProcessName, "")`); Skipped-event toast wiring remains S3
- ✅ `[L]` Position: top-left / top-right / bottom-left / bottom-right / center + pixel offsets, on the monitor of the **active window at capture time** (same multi-monitor rule as the popup; bottom-right verified at 150% DPI) (bottom-right verified at 150% DPI)
- 🟡 `[U]` Width/height/duration/enabled all honored from config
- 🟡 `[L]` **Toast window REUSED — wired in S3** (one NotificationWindow for the session: Hide-not-Close, timer reset, `ShowMessage`) — final RAM re-measure pending; peak still 253.7 MB → fix sprint
- ✅ `[L]` Capture→toast latency ~400–600 ms after the 100 ms debounce — accepted trade-off (veteran debounce pattern; correctness over instant)
- ⬜ `[L]` Skipped-capture notifications wired via CaptureService.Skipped (oversized images etc. — no more silent vanishing)

## 12. Infrastructure, performance & renamability
- ✅ `[H]` Renamable in ONE place: csproj `<AssemblyName>` — exe name, %APPDATA% folder, mutex, tray label, autostart value all derive at runtime
- ✅ `[H]` PerMonitorV2 manifest
- ⬜ `[L]` App writes NOTHING outside the project folder and `%APPDATA%\Clip` (incl. cache)
- ⬜ `[L]` Acceptance run: copies from Notepad/Chrome/VS Code/Explorer appear with correct app+time; password-manager copy never appears; survives reboot with start-on-login
- 🔬 docs\RESEARCH-logic-best-practices.md ADOPT list (Ditto/CopyQ/PowerToys techniques; GPL = technique reference only)

## 13. Dependencies (library research, adopted)
- ✅ Packages resolve + app builds 0 errors / 0 warnings (S3 full Release build): HtmlRenderer.WPF 1.6.1, H.NotifyIcon.Wpf 2.4.1, RtfPipe (fallback-only), System.Text.Encoding.CodePages, Microsoft.Data.Sqlite
- ✅ `[L]` `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` runs at startup (S3 App wiring); rich previews rendered live with no TypeInitializationException (the crash-without-it was proven in the spike)
- ⬜ Remote images in preview answered with a transparent 1×1 image (not a broken-image glyph) while http(s) loads stay cancelled
- ⬜ Tray swapped to H.NotifyIcon (vector/themed) — menu must NOT rely on `ShowContextMenu` (routing failed in Win11 overflow; opening the same ContextMenu directly works — use positioned manual open or a click handler, and verify live with the icon pinned outside the overflow)
- ⬜ No other dependencies added (keep-as-is list honored)

## 14. Deliberately deferred (researched, tracked, NOT current scope)
- ⬜ Delayed-render **IStream fallback** (veterans use retries+IStream; we force-render at snapshot — works, deeper handling deferred)
- ⬜ Paste **focus poll** (our 100 ms hide→paste delay matches Ditto's shipped default; a focus-confirmation poll would be stronger)
- ⬜ **CF_DIBV5** paste format — ships only after the live Office/Photoshop matrix passes (least-proven step)
- ⬜ Owner-based / per-app capture exclusion list (beyond the two ignore formats)
- ⬜ HtmlRenderer **CSS3 engine** — upgrades automatically when upstream ships the release containing it
- ⬜ OfficeIMO.Rtf as RtfPipe replacement — only if RTF-only sources (WordPad-era) prove to matter
- ⬜ Feature landscape + **1000-item scale contract** — docs\RESEARCH-features-and-scale.md (agent in flight; must/should/nice split lands here when it reports)
- ✅ RtfPipe frozen-2021 status — accepted as fallback-only (owner informed)
- ⬜ **Feature roadmap (post-MVP, docs\RESEARCH-features-and-scale.md Part A):** M4 musts — paste transforms (Ditto Special Paste), export/backup/restore, sensitive-clip masking, age+size retention, per-app rules. M5 shoulds — paste queue/multi-select, search prefixes (`app:`/`date:`), duplicate-count UX, OCR (behind a TFM spike), per-item hotkeys. NEVER — cloud/LAN sync, AI transforms, scripting, diff, QR, image editor. NOT current scope; nothing here stalls S3.

## 15. Scale — owner: "snappy and lag-free at 1000+ items" (contract: docs\RESEARCH-features-and-scale.md §Part B, 19-row measurable)
S3/UI items:
- ⬜ `[L]` `CanContentScroll=True` + `VirtualizationMode=Recycling` preserved through the Ortu flat-row redesign (measured: Recycling ~2× faster to first layout; `False` = 1000 containers, 3.6–4.4 s — a silent kill)
- ⬜ `[L]` Rows bind a **truncated Preview string** (~400-char cap, line-clamped by `PreviewLines`) — never full `Text` (1 MB body = 64–111 ms per relayout otherwise); preview built once at load, not per keystroke
- ⬜ `[L]` Search: 120 ms debounce + **swap the collection instance** (Clear+Add of 1000 = 38–59 ms per keystroke; filter itself is 0.2 ms)
- ⬜ `[L]` Row growth uses fixed `Height` per state (never `MaxHeight`), scroll-follows-selection
Core items — ✅ all landed and verified (127 assertions, mutation-checked; store split zero call-site changes, all files ≤321 lines):
- ✅ `[H]` WAL + `synchronous=NORMAL` (100 inserts measured 8.0 ms; mutation removing the PRAGMA → 103.7 ms, assertion went RED — load-bearing)
- ✅ `[H]` `InsertWithFormats` + dedupe path in ONE transaction (forced mid-transaction failure → 0 partial rows; old two-step mutation → RED)
- ✅ `[H]` `GetRecent` drops `html`/`rtf`; one-row `GetRich(id)` loads them
- ✅ `[H]` `image_hash` (full SHA-256) + index; dedupe = hash index + BLOB confirm (mutation replacing hash with a constant still refuses a 1-byte-different image — exactness never depends on the hash)
- ✅ `[H]` `EnforceCap` gated by a COUNT check (no-op under cap, read-verified)
- ✅ `[H]` Corrupt-db recovery (codes 11/26 only — locked DBs rethrow by design): rename → `.corrupt-<ts>`, fresh DB, `RecoveredFromCorruption` flag; verified byte-identical preservation of the corrupt file
- ✅ `[H]` `Restore(ClipboardItem)` undo primitive — byte-identical round-trip incl. formats + tab_id + original id, position preserved, no orphans; one transaction
- ⬜ `[H]` Image decode/encode (and SHA-256 of a 5 MB PNG, ~5–15 ms) moved off the UI thread — remaining item, fix sprint
- ⬜ `[H]` **ClipboardStore split** (429 lines → items/tabs/core partial classes, zero call-site changes) + **transaction wrapper** for Insert+InsertFormats and the dedupe path — done together per docs\AUDIT-axis2-standards.md A2-07
- ⬜ `[L]` **Corrupt/locked clip.db recovery path** (rename + recreate + user notice) instead of killing startup — Axis-2 P2
- ⬜ `[L]` Acceptance-run note: **stop the stale running Clip.exe before in-place builds** — it locks `bin\Release\...\Clip.exe` and fails the copy step (MSB3027); build via OutDir redirect if the app must stay up
Acceptance at 1000 items: scroll jank-free, search <50 ms end-to-end, cold start → popup <300 ms, idle RAM unchanged vs 200, `GetRecent(1000)` ≤10 ms
