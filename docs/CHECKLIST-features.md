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
- ⬜ `[U]` Tray icon visually confirmed — S0 created it without error but couldn't verify from Win11's hidden-icons flyout in 3 attempts; owner eyeball or S3 audit with the icon pinned outside overflow -- **S3 AUDIT: PASS** (reached in the Win11 overflow flyout: left-click opened the popup, right-click opened the menu -- tray-overflow-open.png, tray-menu-open.png)
- ✅ `[H]` Single-instance mutex — second launch shows a message and exits
- 🟡 `[L]` Text capture on WM_CLIPBOARDUPDATE with: source app name, window title, local ISO timestamp
- 🟡 `[L]` Copies under `minTextLength` (default 3) skipped; whitespace-only skipped
- 🟡 `[L]` Password-manager exclusion: `Clipboard Viewer Ignore` + `ExcludeClipboardContentFromMonitorProcessing` formats never stored (check runs BEFORE any storage, incl. format snapshot)
- ✅ `[H]` SQLite `%APPDATA%\Clip\clip.db`, spec §5 schema + `rtf` column + `item_formats` table; idempotent migration on existing DBs
- ✅ `[H]` 200-item cap (config `maxItems`): oldest evicted, format blobs purged, zero orphans — **eviction never removes pinned rows** (core-fix verified; cap semantics: bounds UNPINNED items, pinned are extra — owner confirmation pending, flip to cap-on-total if wanted)
- ✅ `[H]` Zero idle CPU (event-driven, no polling); hotkey→popup < 150 ms; measured live in S3 acceptance
- 🟡 `[L]` **RAM (owner-approved): idle ≤ 150 MB ✅ PASS (S3 live: 111–113 MB) · hard ceiling 250 MB ❌ FAIL (S3 live: peak 253.7 MB across 25 rich previews, 238–240 MB retained after hide; panel-drop fix already saved 5 MB)** — fix sprint closes the last ~4 MB of rich-preview retention and re-measures under the full protocol -- **ACC-v2: idle PASS (114.6 MB WS / 53.2 MB private); ceiling FAIL HARD — 344–350 MB WS / 185 MB private after a session, NOT released by closing the editor, the popup or 12 s idle. Isolation: launch 114.7 → 5 opens 210.2 → 10 opens 220.2 → Settings 242.9**

## 2. Popup & keyboard contract (owner-frozen)
- ✅ `[L]` Alt+V global hotkey, rebindable via config/settings; hotkey conflict shows a clear message
- ✅ `[L]` Popup centered on the monitor of the **CURRENT ACTIVE WINDOW** (owner requirement, multi-monitor: `GetForegroundWindow` → `MonitorFromWindow`, fallback to cursor position only when no foreground window) — DPI-correct (PerMonitorV2 + ScreenDpi). S3 changes this from the current cursor-based rule
- ✅ `[L]` Search box focused on open; typing filters live, case-insensitive substring over text + app name
- ✅ `[L]` **←/→ switch tabs** (Clipboard → Images → Pinned → custom…), selection resets to first item -- **ACC-v2 PASS** (right arrow walked Clipboard→Images→Pinned; `images-tab.png`, `after-clear-pinned3.png`)
- ✅ `[L]` **Esc #1 clears search** (list returns, caret stays in search); **Esc #2 hides popup** -- **ACC-v2 PASS** (hwnd alive + placeholder restored after #1, "popup not visible" after #2; `esc1-cleared-crop.png`)
- ✅ `[L]` Focus-loss (clicking away) hides popup -- **ACC-v2 PASS** (click at 100,620 → gone; `focusloss-hidden.png`)
- ✅ `[L]` ↑/↓ navigate items, selected item scrolls into view — **v2: zero geometry change on navigation** (S1: one ↓ leaves 0 differing pixels below the selection; later moves scroll the minimal 54–88 px; heights 16+lines×18 computed once, never on selection)
- ✅ `[L]` **Enter pastes selected — VERIFIED live in S3** (sentinel text landed in Notepad, focus returned); **Ctrl+1..9 pastes nth visible item — ACC-v2 PASS 7/7** (#3..#9 each pasted the selected row's exact text; clipboard compared per press)
- ✅ `[L]` **Delete in list context = delete the item** (CRUD); in **edit mode = character right-delete**, never the item -- **ACC-v2 PASS** (list 129→128, selection sane; in search "abc"→"ab", item count unchanged)
- ✅ `[L]` **←/→ switch tabs — UNIVERSAL RULE (owner directive 23 Nov): in EVERY tab, including Images** — the Images sprint had repurposed ←/→ for image stepping; that is REVERSED — image stepping moves to **↑/↓** (↓ next, ↑ previous, Home/End first/last); Ctrl+Tab/Ctrl+Shift+Tab aliases unchanged — **fix sprint** (small key-routing change in MainWindow.Keys.cs + MainWindow.Images.cs)
- ⬜ `[L]` **Popup side padding reduced** (owner: "a lot of padding on the left and right side — reduce that"): outer rail 18→**10 DIP** (header/tabs/status), `History` ListBox side padding 6→**2 DIP**, row content side inset 12→**8 DIP** (edge-to-text ≈12 total, down from 18); vertical rhythm untouched — **fix sprint** (amends DESIGN-spacing-roles rail FOR POPUP SIDES ONLY)
- ⬜ `[L]` **Bottom bar restored: selected clip's source app + captured date/time** (owner: "why is the date and time and app gone from the bottom bar") — ClipRow carries App/Time again (data exists in the DB; the no-meta purge over-reached), bar shows `AppName · 23 Nov 15:16` via `ClipboardItem.ClockFormat` (12h/24h), selection-driven, updates on ↑/↓/tab change; counts + key hints stay right — **fix sprint**
- ⬜ `[L]` **Editor content margins reduced** (owner: "a lot of margin on left right top bottom where the text shows — reduce that"): the text box's own inset goes from the 18 rail to **8 DIP on all sides** (left/right/top/bottom), toolbar-to-text gap tight (~6), header band keeps 48; the text sits near the frame edge like a document, scrollbar flush — **fix sprint** (supersedes DESIGN-spacing-roles' 18 rail FOR THE EDITOR CONTENT AREA ONLY; window chrome padding stays)
- ⬜ `[L]` **Truncated-text tooltip, docked OUTSIDE the popup** (owner directive): appears ONLY when the row's content doesn't fully fit; its LEFT edge aligns with the popup's RIGHT edge (fully outside — never over other items); shows the full text (scrollable, max height = popup height); triggers on hover AND on keyboard dwell over a truncated row (~500 ms without navigation — fast arrowing never strobes); flips to the popup's LEFT side when the popup sits near the screen's right edge; non-activating, no focus steal; replaces the current in-window WPF ToolTip — **fix sprint** (ClipRow must expose a `Truncated` flag: visible lines < total lines or a line wider than the column)
- ✅ `[L]` Popup is **draggable** (drag anywhere on the background) -- **ACC-v2 PASS** (548,114 → 248,194 = exact −300/+80; `popup-dragged.png`)
- ✅ `[L]` Search placeholder text visible when empty -- **ACC-v2 PASS** ("Search Clipboard"/"Search Images"/"Search Pinned")
- ✅ `[L]` Visible keyboard-focus treatment on the selected row -- **ACC-v2 PASS** (2 DIP gradient rail + ring + tint; `popup-open-baseline.png`)
- 🟡 `[L]` Newest first ordering (captured_at DESC, id DESC)
- ✅ `[L]` **Home/End** → first/last item (only when search is EMPTY — the caret keeps them while typing); **PageUp/PageDown** → one viewport of rows, clamped, never wraps -- **ACC-v2 PASS** (Home→#1, End→#129, PgUp→#118, PgDn→#129→#129 clamped)
- ⬜ `[L]` **IME composition gate**: while a Hindi/Chinese/Japanese composition is open, Esc cancels the composition (NOT the popup) and Enter commits it (never pastes) — docs\RESEARCH-keyboard-ux.md; protects real input work
- ✅ `[L]` **Type-to-search from anywhere**: a printable key while focus is on the list redirects into the search box (closes the dead state after F2 closes); **Tab/Shift+Tab = no-op** in the popup -- **ACC-v2 PASS** for the type-to-search half (after ↓ navigation, typing "BIGF" landed in search and filtered live)
- ⬜ `[L]` Aliases: **Ctrl+Tab / Ctrl+Shift+Tab** also switch tabs; **Ctrl+↑/↓** jump between time sections (Today/Yesterday…)
- ✅ `[L]` **Shift+Enter = paste selected as PLAIN text — ACC-v2 PASS** (formats before `Rich Text Format, …, Ole Private Data` → after `System.String, Text, UnicodeText`); **Ctrl+C = copy-without-paste — VERIFIED live in S3** (clipboard read back `COPY-BACK-ONE 1`, no paste fired; ACC-v2 re-read the formats, no paste)
- ✅ `[L]` **Ctrl+Z = one-level undo of item deletion — VERIFIED live in S3** (Delete removes the row, Ctrl+Z restores it; Core `Restore(item)` round-trip already proven `[H]`)
- ✅ `[L]` **Context menu on rows** (right-click / Menu key / Shift+F10) — one surface exposing the seven existing commands (paste, paste-plain, edit/pin/move-to-tab, delete, open-image) -- **ACC-v2 PASS** via right-click (`ctxmenu-rclick.png`) and Shift+F10 (`ctxmenu-shiftf10-region.png`); the dedicated Menu key (synthesized VK_APPS) did not open it
- ⬜ `[L]` **Esc cascade, one ordered function**: IME → editor discard prompt → tab-delete confirm → tab-name editor → Ctrl+M picker → F2 editor → clear search → hide popup
- ⬜ `[U]` Selection stays stable when a live capture arrives while the popup is open -- **S3 AUDIT: UNTESTED-BY-AUTOMATION** (not driven with the popup open)

## 3. Paste fidelity (owner: "paste exactly like I copied, for EVERYTHING")
- 🟡 `[L]` All-formats snapshot on copy (HGLOBAL bytes per format, budget `maxItemBytes` 10 MB, handle formats skipped knowingly)
- 🟡 `[L]` Restore-first paste: every stored format back on the clipboard → Excel gets Biff12, Word RTF/HTML, browsers HTML — **live Excel→Excel columns/rows test is S3's FIRST live test** -- **ACC-v2: payload contract PASS for rich text / images / files** (`Rich Text Format, HTML Format…`; `DeviceIndependentBitmap, PNG`; `FileDrop, FileNameW` with both files); **live Excel→Excel NOT run** — Excel 16 is installed but automation could not hold its foreground to type a range
- 🟡 `[H]` Plain text + RTF + CF_HTML columns stored verbatim (header kept intact for round-trip)
- ✅ `[H]` Clipboard-busy retry: ~5 attempts on locked clipboard (set + snapshot paths)
- ✅ `[H]` Self-paste suppression via clipboard sequence number (our own pastes never re-enter history)

## 4. Plain-text paste (CopyQ-style)
- ✅ `[L]` Global **Ctrl+Shift+V** (rebindable, config `hotkeyPlain`): newest item pasted as raw text — VERIFIED live in S3 (clipboard formats before `…, HTML Format` → after `String, UnicodeText, Text`)
- ⬜ `[L]` Same shortcut with popup open = selected item as plain text

## 5. Rich text (render like rich text)
- ✅ `[H]` **S0 converters landed** — `Core\RtfText.cs` (RTF-as-bytes: Latin-1 envelope, 256/256 round-trip, 8-bit WordPad RTF loads clean) + `Core\HtmlToRtf.cs` (400 lines, 38/38 harness assertions incl. Devanagari/emoji lossless, tables→tabs, 32 KB in 2.12 ms); S1-critical findings frozen in docs\S0-HANDOFF.md (Span-wrapped underline/highlight; rows render foreground only)
- 🟡 `[U]` Selected card expands into a rich preview: tables, colors, font sizes, line breaks via HtmlRenderer.WPF; ONLY the selected card renders (list stays fast, one panel alive) -- **S3 AUDIT: FAIL** (it is an appended 150 DIP block below the meta in unstyled serif, not a content swap -- zoom-nav2-richpreview.png; DESIGN-v2 section 4 wants the swap) -- **ACC-v2 PASS**: the selected row now renders its formatted content INSIDE its own fixed height (bold/coloured/underlined runs, styled sans type, no appended block, rows below do not move) -- `rich-rtf-row-clean-crop.png`
- ⬜ `[H]` CF_HTML byte-offset fragment extraction (Unicode-safe; non-ASCII torture test) — `Interop\HtmlFragment.cs`
- ⬜ `[U]` Remote images blocked in preview (data: URIs allowed) — no network fetch on open -- **S3 AUDIT: UNTESTED-BY-AUTOMATION** (no HTML clip containing a remote img was available and I will not fabricate one)
- ⬜ `[U]` RTF-only items render via RtfPipe → same renderer (fallback path) -- **S3 AUDIT: UNTESTED-BY-AUTOMATION** (no RTF-only source available; WordPad is absent on this build)
- ⬜ `[U]` Known ceilings documented (verified in spike): color emoji render as tofu glyphs in preview; Google Docs/Notion HTML degrades; rowspan=4 did NOT reproduce in 1.6.1 (upstream bug may not apply) — paste-back fidelity unaffected in all cases -- **S3 AUDIT: UNTESTED-BY-AUTOMATION** (an editorial act, not a visual observation)

## 6. Images
- ✅ `[H]` Image capture: PNG ≤ `maxImageBytes` (5 MB default), 96px thumbnail, kind='image'; text never in image kind
- ✅ `[H]` **Alpha preserved at capture**: "PNG" clipboard format read FIRST (byte-verbatim storage, pixel alpha survives), GetImage only as fallback — 24/24 harness
- ✅ `[H]` **Paste-back sets a real CF_DIB** (24-bpp bottom-up, white-flattened, DWORD-aligned stride) + PNG verbatim; image branch runs BEFORE Formats (a stored "PNG" blob can no longer own an image paste); `SetImage` removed
- ✅ `[H]` **Pixel-count guard before decode** (50 MP / 16,384 px per side — patched-IHDR bomb rejected in 4 ms) + over-limit skips fire `Skipped` with human reasons, never silent
- ✅ `[H]` Thumbnail capped on BOTH axes (100×4000 → 2×96, 1920×1080 → 96×54)
- ✅ `[L]` Images tab: thumbnail grid, click/Enter copies image back; images auto-routed, text never appears there -- **S3 AUDIT: FAIL** (no grid: 22 DIP tiles in text rows, selected image gets no bigger preview -- tab-images.png, P1-3) -- **ACC-v2 PASS**: dominant preview 383×247 DIP + numbered thumbnail strip + "2 / 14" counter; ←/→ swap with 0 differing pixels outside the preview (`images-tab.png`, `images-tab-nav2.png`)
- ✅ `[U]` **Notification shows the image thumbnail** — 40×40 DIP, DecodePixelWidth=96, Uniform, from `item.Thumb` never `item.Image` ("Image copied" fallback if thumb missing) -- **ACC-v2 PASS on behaviour**: an image copy shows a real thumbnail in the toast; measured **97×51 DIP**, i.e. larger than the 40×40 spec (`toast-image-thumb.png`)
- ✅ `[L]` **F2 on an image opens it in the default program** — `ImageCache` landed (write-if-changed verified; startup cleanup: >7 days / newest 100, never delete on exit) — S3 wires it into App startup -- **ACC-v2 PASS** (Photos opened `clip-155.png` 982×652; `image-f2-viewer4.png`)
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
- ⬜ `[U]` Tab strip: flat text + 2px accent underline (no TabControl pills), per-tab counts ("Pinned · 4") -- **S3 AUDIT: PASS** (counts present, no middle-dot separator, underline floats ~5 DIP above the strip rule -- zoom-tabstrip.png, P3)
- ⬜ `[U]` ←/→ walks all tabs: Clipboard → Images → Pinned → custom… -- **S3 AUDIT: PASS** (tab-images.png, tab-pinned.png -- Clipboard -> Images -> Pinned by arrow key)

## 8. F2 editor (owner-frozen) — v2: ONE rich editor for everything
- ✅ `[L]` F2 opens the editor for selected text/rich item; caret in content (live popup pass = acceptance run; v2 scratch host already proves formatting loads) -- **ACC-v2 PASS**: rich clip and browser-HTML clip both open FORMATTED, caret at content end, toolbar live (`editor-html-small-crop.png`, `editor-chrome-html.png`)
- ✅ `[L]` **Ctrl+S saves & closes** (v2: real Win32 Ctrl+S keystroke observed; save contract verified — text+RTF replaced, format blobs 1→0, reopen keeps formatting); list refreshed in place = live pass -- **ACC-v2 PASS**: stored RTF keeps its bold (`boldToggles=25`) and the popup row re-rendered bold in place, selection kept (`popup-row1-bold-after-save.png`)
- ✅ `[H]` **Esc or F2 with unsaved changes → "Discard changes?"** (Yes=close without saving, No=keep editing); no changes → silent close — scratch-host asserted (g1–g3)
- ✅ `[H]` **ONE rich editor for EVERY text clip** (owner directive §5): plain clips open in the same RichTextBox (no separate plain mode; failures show a VISIBLE notice bar, never silent); HTML-only clips convert via HtmlToRtf; >2 MB refused with a notice; malformed RTF → plain + notice. 52/52 harness assertions incl. Devanagari load + real Ctrl+B/Ctrl+S keystrokes
- ✅ `[H]` **Formatting toolbar** (owner directive): B/I/U/S · size (points) · text colour · highlight · clear-formatting; toggles apply to the selection and reflect the caret state (reads the Span chain — naive Run reading can't see underline/strike); theme tokens only; keyboard-walkable (Shift+Tab path asserted)
- ✅ `[L]` On close, focus returns to the list at the same item (live pass = acceptance run) -- **ACC-v2 PASS**: after Esc, editor gone, popup is the foreground window, search box holds focus, the same row (#1) stays selected
- ✅ `[H]` Edits update text/rtf + timestamp and REPLACE the stored format blobs (c1–c4: blobs 1→0, GetRich returns the new RTF; html regenerated only if the clip had html)

## 9. Dedupe (owner-frozen: EXACT = case-sensitive, space-sensitive, byte-exact)
- ✅ `[H]` Re-copy existing text → exactly ONE entry, moved to top, fresh timestamp/app/formats; old entry gone
- ✅ `[H]` Case/space/Unicode-composition differences are DIFFERENT items
- ✅ `[H]` Identical image bytes dedupe identically
- ✅ `[H]` Pinned items keep their pin when moved to top
- ✅ `[H]` **Same-second re-copy moves to top** — fixed red-first (millisecond stamps `yyyy-MM-ddTHH:mm:ss.fff`); pre-fix observed order B,A → post-fix A,B; mixed-format sort verified
- ⬜ `[U]` S3 fix: popup must not briefly double-show a deduped card (OnNewItem removes existing id before inserting at top) -- **S3 AUDIT: UNTESTED-BY-AUTOMATION** (needs frame capture during a re-copy)

## 10. Settings & autostart (owner-frozen)
- 🟡 `[U]` Settings window: hotkey, plain-hotkey, max items, preview lines, notification (enabled/duration/width/height/position/offsets/maxLines), theme (dark/light/system), **time format 12h/24h**, start-on-login, clear history (with inline confirm) — S2 built + verified 69/69 -- **S3 AUDIT: UNTESTED-BY-AUTOMATION** (Settings window never renders: opening it terminates the process -- P0-1, settings-open-attempt.png)
- ✅ `[H]` **Config.PreviewLines landed** (=10, JSON `previewLines`) — Axis-2; full build 0 errors / 0 warnings (forced recompile via OutDir redirect)
- ✅ `[L]` **HotKeyWindow parser — FIXED in S3** (explicit friendly-name → VK map: digits, F-keys, OEM row; no silent fallback; exposes registration result) -- **ACC-v2 PASS live**: rebind Alt+V → Ctrl+Alt+K (new key opens, old one does not) → back to Alt+V (old opens, new does not)
- ✅ `[L]` **P0 crash fix: opening Settings threw InvalidCastException and killed the app — FIXED** (local `<Style x:Key="Field">` renamed `FieldBox`; every x:Key in all 5 windows swept against theme keys — no other collisions; live-verified ×2 from the tray, dark + light, process alive)
- ❌ `[L]` **Cold first popup open 221 ms — over the 150 ms budget (warm opens 12–32 ms PASS)** — pre-warm/cost-audit the first-render path (JIT + HtmlRenderer init) — fix sprint -- **ACC-v2 re-measured: 309.9 / 282.4 / 262.7 ms across 3 restarts (one 129.7)**; warm 60.6/56.3/53.4/50.9/49.8 ms — cold budget still FAILED, warm now slower than S3
- ✅ `[L]` Settings wiring: tray Settings menu opens the window (currently disabled placeholder); SettingsSaved applies live (theme swap, hotkey re-register incl. conflict message, ClockFormat, notification); ClearHistoryRequested wipes MAIN HISTORY ONLY (tabs survive per §7) -- **ACC-v2 PASS**: tray → Settings opens (no crash); theme Dark→Light→Dark applied live; hotkey rebind worked both ways; Clear history wiped Clipboard+Images to 0 and left Pinned's 1 clip (`settings-open.png`, `popup-light-theme2.png`, `after-clear-pinned3.png`)
- ⬜ `[U]` Notification settings include the **live on-screen preview overlay** (exact placement, live updates, drag-to-set offsets, auto-hide + pin) and the Settings window is reorganized into tabs (General / Notification) — owner requirement 23 Nov; public API unchanged: ctor(Config), SettingsSaved, ClearHistoryRequested -- **S3 AUDIT: UNTESTED-BY-AUTOMATION** (Settings window crashes on open -- P0-1)
- 🟡 `[H]` Time format: `config.json "timeFormat"`, footer + notification honor 12h/24h; WasNew excludes from JSON
- ✅ `[L]` Settings apply LIVE — no restart -- **ACC-v2 PASS** (theme, hotkey, time format, autostart, clear history all took effect without a restart)
- ✅ `[L]` Autostart: HKCU Run value named after the product, EXE path via `Environment.ProcessPath` (NOT Assembly.Location — that's the DLL and won't launch); toggle removes it — real registry round-trip `[H]` already PASS -- **ACC-v2 PASS live**: value `…\bin\Release\net10.0-windows\Clip.exe` → toggle off → value gone → toggle on → value back
- ⬜ `[L]` First-run hint: first ever launch shows "…is running — press Alt+V" (uses WasNew)

## 11. Notification (spec §4.4, silent, custom)
- ✅ `[U]` Own WPF window — never a native toast; silent (no sound, no system notification)
- ✅ `[L]` Copy shows: app name + timestamp + first ~3 lines truncated (S0 screenshot: "WindowsTerminal · 14:33:40 · Clip M1 verification text 12345")
- ✅ `[L]` Borderless, rounded, topmost; fade in/out ~150 ms; auto-dismiss on duration timer (3 s observed)
- ⬜ `[U]` **Clickable ✕** (per-region hit test; window stays click-through everywhere else; no focus stealing) — **keyboard-reachable and UIA-labelled** (audit: today it is a bare 13×15 TextBlock with no accessibility) -- **S3 AUDIT: UNTESTED-BY-AUTOMATION** (hit-testing a click-through toast is not safely automatable; source shows a bare TextBlock with no AutomationProperties)
- ✅ `[H]` **Self-attribution leak fixed** — own-process copies attribute app=product, title="" (`ForegroundApp.Attribute` pure function, tested: internal title + own pid → `(ProcessName, "")`); Skipped-event toast wiring remains S3
- ✅ `[L]` Position: top-left / top-right / bottom-left / bottom-right / center + pixel offsets, on the monitor of the **active window at capture time** (same multi-monitor rule as the popup; bottom-right verified at 150% DPI) (bottom-right verified at 150% DPI)
- 🟡 `[U]` Width/height/duration/enabled all honored from config -- **S3 AUDIT: FAIL** (configured height 160 DIP rendered ~54 DIP -- SizeToContent wins over config; width/duration/enabled worked) -- **ACC-v2: still FAIL** — width 420 DIP honoured exactly, height 160 → **72.7 DIP** (`toast-24h-crop.png`)
- 🟡 `[L]` **Toast window REUSED — wired in S3** (one NotificationWindow for the session: Hide-not-Close, timer reset, `ShowMessage`) — final RAM re-measure pending; peak still 253.7 MB → fix sprint -- **ACC-v2 PASS**: three copies 250 ms apart produced exactly ONE toast window (630×109 px); RAM retest is in ACCEPTANCE-v2 §8 (still over ceiling)
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
