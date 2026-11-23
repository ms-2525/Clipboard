# ACCEPTANCE — v2 live run (evidence, measured values, fix-sprint worklist)

**Run:** 23 Nov 2026, 19:12–20:10 (local). **Agent:** fresh acceptance sub-agent; edited no source, seeded no data,
renamed nothing. **Build tested:** `bin\Release\net10.0-windows\Clip.exe`, `LastWriteTime 2026-11-23 19:07:03`
(pid 65564 from 19:13, later restarted to pid 105016 → pid 23644 — see §9). Every number below is measured on that
binary; anything landing after 20:10 is not covered.

**Environment:** one display **1920×1200 @ 150 %** (1280×800 DIP), taskbar 32 DIP, **single monitor**.
All captures set `SetProcessDpiAwarenessContext(-4)` first (verified: popup = 825×780 px = 550×520 DIP exactly).
Evidence folder: `docs\design-evidence\acceptance-v2\`.

**Installed / not installed:** **Excel 16 and Word 16 are installed** (Office16 under `C:\Program Files\Microsoft Office`).
**WordPad is NOT installed** (no `write.exe`, no `wordpad.exe`), **Paint is installed** (MSIX alias), **Photos is installed**
(it is the `.png` handler), Notepad + Chrome present. RTF therefore had to be produced synthetically (a real
`Rich Text Format` clipboard write, not a WordPad copy) — disclosed wherever it matters.

---

## 0. The verdict in one line

The v2 rebuild is **real and largely working**: rows are flat with numbers, rich content renders formatted inside rows,
F2 is a single WYSIWYG editor with a working toolbar, the Images tab is a proper big-preview browser, Settings opens and
applies live, the tray menu is themed. **Two things block a green acceptance: the popup's bottom edge still slices a
glyph, and RAM runs 344–350 MB (ceiling 250) with no release on close.** Cold open is still ~3× the budget on 3 of 4
samples. One new P1: the on-screen notification preview does nothing.

| Area | PASS | FAIL | UNTESTED-by-automation |
|---|---|---|---|
| Popup core (22 checks) | 18 | 2 (bottom slice, cold-open budget) | 2 (Menu key, IME) |
| Rich rows (4) | 4 | 0 | 0 |
| F2 editor (8) | 8 | 0 | 0 |
| Fidelity (7) | 4 (image, files, rich text, plain payloads) | 0 | 3 (Excel live, Word live, Paint landing) |
| Images tab (5) | 4 | 0 | 1 (Paint landing) |
| Settings (7) | 6 | 1 (ghost preview) | 0 |
| Notification & tray (7) | 5 | 1 (height not honoured) | 1 (✕ click); tray Open Clip / Exit clicks partial |
| Perf (5) | 3 (idle CPU, idle RAM, 28 KB RTF) | 2 (open-state RAM, release on close) | 0 |

---

## 1. Popup core — keyboard & geometry

| # | Check | Verdict | Measured value / evidence |
|---|---|---|---|
| 1 | **Cold open (first open after launch) ×3** | **FAIL** | **309.9 / 282.4 / 262.7 ms** (4th restart sample 129.7 ms). Budget <150 ms. |
| 2 | **Warm open ×5** | PASS | **60.6 / 56.3 / 53.4 / 50.9 / 49.8 ms** (budget 150; S3 reported 12–32 ms — regression worth a look) |
| 3 | Search filters live | PASS | typed `BIGF` → status `1 clip`; `ab` → `17 clips` (`search-typed-from-list-crop.png`, `key-delete-in-search2-crop.png`) |
| 4 | Esc #1 clears search / #2 hides | PASS | hwnd 9243464 alive with placeholder restored after #1; "popup not visible" after #2 (`esc1-cleared-crop.png`) |
| 5 | Focus-loss hides | PASS | click at (100,620) → popup gone (`focusloss-hidden.png`) |
| 6 | Popup draggable | PASS | dragged 548,114 → **248,194** = exact −300/+80 (`popup-dragged.png`) |
| 7 | Type-to-search from list focus | PASS | after ↓ navigation, typing "BIGF" landed in the search box (`search-typed-from-list.png`) |
| 8 | **↑/↓ zero geometry change** | **PASS** | one ↓: **0 differing pixels** in the region below the selection (`acc-diff` region y 600–890); only the two rows' selection state changed (`nav-a-crop.png`, `nav-b-crop.png`) |
| 9 | Home / End / PageUp / PageDown | PASS | Home→#1, End→**#129**, PgUp→#118, PgDn→#129, PgDn→#129 (clamped, no wrap) |
| 10 | **Ctrl+1..9 paste nth (3..9)** | **PASS 7/7** | clipboard after each matched the selected row's text exactly (#3 BIGFIXTURE … #9 RICH FIXTURE 9) |
| 11 | Shift+Enter = plain paste | PASS | formats before `Rich Text Format, System.String, Text, UnicodeText, Ole Private Data, Locale` → after **`System.String, Text, UnicodeText`** |
| 12 | Ctrl+C copy-back | PASS | formats restored, no paste fired |
| 13 | Delete in list vs in search | PASS | list: 129→128, selection sane; Ctrl+Z 128→**129** restored; in search "abc" → Delete removed one character (→ `ab`, 17 matches), item count unchanged |
| 14 | Ctrl+Z undo | PASS | row returned at #2 with its text (`key-delete-undo.png`) |
| 15 | Context menu — right-click | PASS | 7 commands: Paste / Paste as plain text / Copy / Edit / Pin / Move to tab… / Delete (`ctxmenu-rclick.png`) |
| 16 | Context menu — Shift+F10 | PASS | same menu, keyboard-opened (`ctxmenu-shiftf10-region.png`) |
| 17 | Context menu — **Menu key (VK_APPS)** | **UNTESTED** | synthesized `VK_APPS` (with and without extended flag) produced no menu; Shift+F10 path works, so the surface is reachable |
| 18 | Row numbers 1..N | PASS | 1,2,3,4 visible on open; **58,59,60** seen mid-list (`row-numbers-58-60.png`) |
| 19 | **Bottom edge never slices a glyph** | **FAIL** | the last visible row is cut horizontally through its glyphs ("coloured run 3 …" sliced, `zoom-bottom-clip-3x.png`); no scroll cue |
| 20 | Search placeholder when empty | PASS | "Search Clipboard" / "Search Images" / "Search Pinned" |
| 21 | Visible focus treatment on the selected row | PASS | 2 DIP gradient rail + ring + tint (`popup-open-baseline.png`) |
| 22 | IME composition gate | **UNTESTED** | cannot synthesize a Hindi/Chinese composition on this box |

---

## 2. Rich rows live

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | Formatted content visible in rows **immediately on open, unselected too** | **PASS** | my synthetic RTF row renders bold 18 pt heading, blue underlined run, green run, 20 pt bold — all inside the row, content swap, no block appended (`rich-rtf-row-clean-crop.png`); unselected rows keep colour+underline+strike (`nav-b-crop.png`) |
| 2 | Badge math on a 30-line clip | **PASS** | 30 CRLF lines, 6 shown → **"+24"** (30−6). The old "+57" lie is gone (`rich-30lines-badge-crop.png`) |
| 3 | Search-match colouring | **PASS** | matched "ab" accented inside the row text (`key-delete-in-search2-crop.png`) |
| 4 | Browser HTML row (real Chrome copy) | PASS with ceiling | a 1.1 KB section copy renders rich (heading, blue links, italic, "+10"); a whole-page copy (>32 KB) falls back to plain text by the documented `MaxPayload = 32 KB` rule (`rich-chrome-small-crop.png`) |

---

## 3. F2 editor (the owner's #1 complaint)

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | Opens formatted on a rich clip | **PASS** | document opens with the clip's colours/links/bullets, toolbar visible (`editor-html-small-crop.png`) |
| 2 | **HTML-only browser clip opens formatted** | **PASS** | real Chrome copy → blue underlined links, italic, bullets (`editor-chrome-html.png`) |
| 3 | Toolbar toggle bold on a selection | **PASS** | Ctrl+A → B applied; button shows pressed, text is visibly bold (`editor-bold-toggle-crop.png`); TogglePattern read `BoldButton=On/Off` correctly |
| 4 | Caret reflection | **PASS** | caret inside a link → the colour chip turned **blue**; caret in plain text → B/U/S off (`editor-caret-inside-link-crop.png`) |
| 5 | **Ctrl+S saves & closes; row updates in place** | **PASS** | stored RTF after save: `rtfLen=2087 boldToggles=25`; the popup row re-rendered **bold** in place with the selection kept (`popup-row1-bold-after-save.png`) |
| 6 | Esc with changes → discard prompt | **PASS** | "Discard changes? · Keep editing / Discard", Discard in the Error token (`editor-discard-prompt2-crop.png`) |
| 7 | Focus returns to the same row | **PASS** | after Esc: editor gone, popup visible, **fg = popup**, focused element = `Search clips`, row **#1 still selected** |
| 8 | F2 on an image opens the default viewer | **PASS** | `Photos` launched with `clip-155.png` (982×652, 171.8 KB) (`image-f2-viewer4.png`) |

*Note:* one earlier attempt appeared to lose the bold; root-caused to my Ctrl+S landing in Chrome's Save-As dialog
(the editor had already closed). The clean re-run above is the truth.

---

## 4. Fidelity matrix (what this machine can prove)

| Target | Verdict | Evidence |
|---|---|---|
| Image → clipboard payload | **PASS** | after Enter on an image the clipboard is **`DeviceIndependentBitmap, PNG`** (what Paint/Word consume) |
| Files (Explorer) → payload | **PASS** | after Enter on a 2-file clip: **`FileDrop, FileNameW, FileName`**, both files listed (`ALPHA-clip-test.txt`, `BRAVO-clip-test.txt`) |
| Rich text → clipboard | PASS | `Rich Text Format, HTML Format, UnicodeText, Ole Private Data, Locale` restored on Ctrl+C |
| Plain paste (Ctrl+Shift+V / Shift+Enter) | PASS | only text formats after (`System.String, Text, UnicodeText`) |
| **Excel → Excel live** | **UNTESTED** | Excel 16 opens to its Home screen; automation could not hold Excel's foreground long enough to type a range (every synthetic Ctrl+C landed elsewhere). Machine HAS Excel — this is a 2-minute owner test |
| **Word RTF/HTML live** | **UNTESTED** | same focus problem; not attempted after Excel |
| WordPad RTF round-trip | **N/A** | WordPad not installed on this build |
| Image landing inside Paint | **UNTESTED** | the paste payload is proven, but my Enter pasted into whatever window held focus (twice into the agent's own chat box) — no reliable paint-canvas landing |

---

## 5. Images tab

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | Dominant preview fills the popup | **PASS** | preview **575×370 px = 383×247 DIP**, thumbnail strip with numbers, "2 / 14" counter, `14 images` status (`images-tab.png`) |
| 2 | ←/→ zero jump | **PASS** | preview swapped CARD 10 → CARD 9, counter 2/14 → 3/14, **0 differing pixels** in the strip/status region (`images-tab.png` vs `images-tab-nav2.png`) |
| 3 | Enter pastes the image | PASS (payload) | clipboard becomes `DeviceIndependentBitmap, PNG` |
| 4 | F2 opens the viewer | PASS | Photos with `clip-155.png` |
| 5 | Delete + Ctrl+Z | **PASS** | 15 → 14 → **15**, thumbnails intact after undo (`images-after-delete.png`, `images-after-undo.png`) |

---

## 6. Settings live

Settings opens from the tray and no longer kills the app (P0-1 closed). Window **1071×1028–1104 px at 425,12**
(714 DIP wide), tabs General / Notification, uppercase section labels, 38×22 switches, segmented controls.

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | Opens from tray, renders | **PASS** | `settings-open.png` |
| 2 | Theme switch applies live, both ways | **PASS** | Light → popup re-rendered light instantly (`popup-light-theme2.png`); back to Dark; `config.json "theme":"dark"` |
| 3 | Hotkey rebind → and BACK | **PASS** | Ctrl+Alt+K saved → new key opens the popup, **Alt+V does not**; rebind back → Alt+V opens, Ctrl+Alt+K does not (`config.json hotkey` checked at each step) |
| 4 | 12h / 24h toggles the clock | **PASS** | toast `07:55 PM` with 12h, `19:56` with 24h (`toast-12h-crop.png`, `toast-24h-crop.png`) |
| 5 | **Live on-screen preview (ghost)** | **FAIL** | "Show preview" clicked twice with the window focused: **no overlay window, nothing rendered** (`ghost-preview-retry-crop.png`); the card and copy are there, the behaviour is not |
| 6 | Clear history — inline confirm, main only | **PASS** | copy: "Delete all clips in Clipboard and Images? Pinned clips and tabs stay." (`clear-history-confirm-step-crop.png`); after confirm Clipboard **0**, Images **0**, **Pinned 1 survives** (`after-clear-clipboard.png`, `after-clear-pinned3.png`); both empty states designed |
| 7 | Autostart round-trip | **PASS** | HKCU Run `Clip` = `…\bin\Release\net10.0-windows\Clip.exe` → toggle off → **value removed** → toggle on → **restored**; left ON (owner's original) |

---

## 7. Notification & tray

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | Toast on copy, **reused** | PASS | 3 copies 250 ms apart → **exactly one** toast window (630×109 px) (`toast-reuse-single.png`) |
| 2 | Image thumbnail in the toast | PASS | my 640×400 test image shows as a **97×51 DIP** thumbnail (`toast-image-thumb.png`) — spec says 40×40, this is bigger (deviation, not a defect) |
| 3 | Placement bottom-right with 16 DIP offsets | PASS | toast at 1274,1003 → 16 DIP from the work-area edges |
| 4 | ✕ dismisses | **UNTESTED** | the ✕ renders as a clean `×` (fallback-glyph defect fixed) but I did not click it |
| 5 | **Height honoured from config** | **FAIL** | config `height 160` → rendered **72.7 DIP** (width 420 honoured: 420.0 DIP). `SizeToContent` still wins |
| 6 | Tray menu opens manually, **themed, no white gutter** | **PASS** | `tray-menu-themed.png` — dark rounded menu, "Open Clip / Settings… / Clear history… / Exit" |
| 7 | Tray Show / Settings / Clear / Exit live | PARTIAL | Settings used 6×; Clear history used via Settings; "Open Clip" and "Exit" deliberately not clicked |

---

## 8. Performance protocol

| Measurement | Value | Verdict |
|---|---|---|
| Idle CPU, 30 s, popup closed | **0 ms CPU = 0.000 %** of 16 cores | PASS |
| Idle RAM after launch | **114.5–114.9 MB WS / 53.2 MB private** (21–22 threads) | PASS (≤150) |
| Open-state RAM: launch → 5 opens → 10 opens | **114.7 → 210.2 → 220.2 MB WS** (private 53.4 → 142.9 → 147.7) | ~19 MB per open early on |
| Settings session cost | **+22.7 MB** (220.2 → 242.9 open; 243.0 closed; 234.8 after 10 s) | retained |
| Full-session walk (formatted rows → Images → editor open → editor closed → 12 s idle → popup closed +5 s) | **344.2 / 347.6 / 350.1 / 350.2 / 350.1 / 347.1 MB WS** (private 176 → 185) | **FAIL — hard ceiling 250 MB** |
| Release on close | 350.1 → 347.1 MB after closing the popup, **no return to idle** | **FAIL — nothing is handed back** |
| 28 KB RTF first encounter | **43.1 ms** first open vs 59–62 ms warm | PASS — no first-encounter hitch above noise |

**Where the RAM goes (measured deltas, same instance):** first open +52 MB (rich row render), each further open
+~19 MB while the list is fresh, a Settings session +23 MB. The earlier S2 "272–285 MB after an editor session" is
confirmed and my instance went higher (350 MB). Nothing in the app releases it — not the editor closing, not the popup
closing, not 12 s of idling.

---

## 9. Multi-monitor

**Only one monitor exists** (1920×1200, DISPLAY23). Popup placement, DPI scaling at 150 % and toast placement were
verified on it; "on the monitor of the active window" cannot be exercised here.

---

## 10. New findings this run (beyond the known list)

1. **P1 · Popup is not topmost and can end up behind other windows.** `GWL_EXSTYLE = 0x80` (TOOLWINDOW only, no
   `WS_EX_TOPMOST`). Reproduced 3× after an editor session: `IsWindowVisible = true`, correct rect (548,114), but
   another app painted over it and Alt+V "opened" it invisibly. A user would see "the hotkey does nothing".
2. **P1 · Hotkey stopped opening the popup once** after a long editor/hotkey sequence; it recovered only after a
   restart. Suspect a `_visible` state desync (synthetic focus makes it likelier, but the state has no reconcile path).
3. **P2 · The app process changed identity mid-run twice** (65564 → 105016 → 23644) with no restart from me. No crash
   dialog was observed and the tray icon stayed alive; cause unknown — worth a pipe/crash-log check.
4. **P3 · Notification thumbnail is 97×51 DIP**, spec says 40×40.
5. **P3 · 12-hour clock prints a leading zero** ("07:55 PM"); Windows shows "7:55 PM".
6. **P3 · Pinned tab status bar shows no count** even with 1 item (tab strip says "Pinned 1").
7. **P3 · RTF whose plain-text blob is shorter than its RTF renders clipped to the plain-text height** (my 240-line
   RTF with a 1-line text blob showed 1 line, no badge). Real copies keep the two in step, so this is an edge case —
   but a "content taller than the row" guard would be honest.
8. **P3 · "Lines per clip 10" in Settings vs 6 lines shown in the selected 30-line row** — the height cap wins; the
   copy over-promises.

---

## 11. FIX-SPRINT WORKLIST (everything FAIL or over budget, in priority order)

1. **Bottom-edge glyph slicing (P1, repeat offender).** Reserve a full row's height or fade/scroll-cue the last row —
   the viewport must never cut a glyph. `zoom-bottom-clip-3x.png`.
2. **RAM: 344–350 MB vs the 250 MB ceiling, and nothing is released.** Find the holder: +52 MB on the first open,
   +19 MB/open, +23 MB/Settings session, retained after the editor and the popup close. Target: idle return ≤ 150 MB,
   worst case ≤ 250 MB.
3. **Cold open 262–310 ms vs <150 ms** (warm 50–61 ms, also worse than S3's 12–32). Pre-warm the first-render path
   (JIT + RichRow loader warm-up + HtmlRenderer init) and re-measure 5 restarts.
4. **On-screen notification preview (ghost) does nothing** — button click produces no window and no drawing.
5. **Notification height from config is ignored** (160 → 72.7 DIP).
6. **Popup is not topmost / can hide behind other windows after an editor session.**
7. **Hotkey state desync** — reconcile `_visible` with `IsWindowVisible` on hotkey press.
8. Investigate the **process identity change** (65564 → 105016 → 23644).

Owner-only leftovers: Excel→Excel and Word paste (machine has both), Paint landing from Enter, Menu-key context menu,
IME composition, multi-monitor placement, tray Exit / Open Clip clicks, notification ✕ click, the 380 ms copy pulse.

---

## 12. Honest gaps of this run

- **WordPad absent** → RTF fidelity was tested with a real `Rich Text Format` clipboard write (synthetic producer), not
  a WordPad copy. RTF *rendering* and *round-trip through the editor* are proven; "paste back into the app it came from"
  is not.
- **Excel/Word live paste untested** — automation could not keep Office in the foreground. The payload contract is
  proven by formats (`DeviceIndependentBitmap/PNG`, `Rich Text Format/HTML Format`, `FileDrop/FileNameW`).
- **Paint landing untested** for the same reason.
- **The Menu key** did not respond to synthesized `VK_APPS`; Shift+F10 does.
- **IME composition gate** cannot be exercised without a real IME session.
- **Motion** (140 ms open, 83 ms close, 380 ms pulse, FLIP) cannot be timed from stills; not re-judged here.
- **Light theme** was seen only via the Settings switch (popup light screenshots); Settings itself in light was not captured.
- **`.png` handler is Photos**, not Paint — "F2 opens the default viewer" is Photos on this machine by design.

## 13. Side effects I caused (nothing hidden)

1. **Clear history was run** (as instructed): the main history and Images are now **empty**; Pinned keeps its 1 item.
   ~145 test/agent clips and 15 images were removed through the app's own confirm path — no SQL.
2. **`config.json` now carries `"timeFormat": "24h"`** (the key did not exist before; 24h is the default I left it on).
   `hotkey` = `Alt+V`, `theme` = `dark`, `startOnLogin` = `true` — all restored to the owner's values after testing.
3. **HKCU Run value** was removed and restored to the same EXE path.
4. **One clip was deleted early** (a stray Delete while my paste target was not focused); it was a test fixture, and the
   Ctrl+Z undo path was verified later anyway.
5. **10 characters were typed into the orchestrator's chat compose box** and an image was pasted there by two mis-focused
   Enter presses ("ZZZ-ADDED", "[Image 1]"); I undid most of the text but the draft may still hold a stray fragment.
   Cause: the popup pastes into whatever window the OS considers foreground, and my synthetic focus was the console.
6. **The app is left RUNNING** (pid 23644 at the time of writing), dark theme, Alt+V, autostart on, history empty.
