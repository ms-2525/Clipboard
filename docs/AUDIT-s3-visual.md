# AUDIT — S3 fresh-eyes visual verdict (first-hand evidence)

**Auditor:** fresh sub-agent, built nothing in this repo, edited no source. Everything below comes from
screenshots I took myself of the running app, pixel measurements on those screenshots, key/mouse input I
synthesised, and the Windows event log. Evidence folder: `docs\design-evidence\s3-audit\` (75 files).

**Build audited:** `bin\Release\net10.0-windows\Clip.exe`, `LastWriteTime 2026-11-23 16:29:49`
(= commit `7757d64`). **Not** the later crisp-text rebuild — that sprint was in flight and its binary had
not landed yet.

**Environment:** 1920×1200 physical @ **150 % DPI** = 1280×800 DIP, **one** monitor, taskbar 32 DIP.
Capture tool set `SetProcessDpiAwarenessContext(-4)` before any crop (verified `awareness=2`, system DPI 144);
all rectangles below are given in DIP (physical ÷ 1.5) unless marked px.

**Interference (disclosed):** the app was killed and relaunched by a parallel sprint at **16:51:54** mid-walk
(new pid, same binary). Captures after that timestamp come from the restarted instance; nothing about the
build changed. Also disclosed in §8: my Delete-key probe removed one **image** clip, and ~10 test clips I
copied are now in history.

---

## 1. Measured numbers

| Measurement | Value | Verdict |
|---|---|---|
| Hotkey → popup visible (cold, first open after launch) | **221.3 ms** | **over** the <150 ms budget |
| Hotkey → popup visible (warm, sample 2) | **12.4 ms** | pass |
| Hotkey → popup visible (warm, sample 3) | **31.6 ms** | pass |
| Idle CPU, 30 s, popup closed | **0 ms CPU = 0.000 %** (16 cores) | pass |
| Idle working set | 111.2 MB at launch → **96.9 MB** settled; private 50.9 MB; peak 111.2 MB | pass (≤150 MB) |
| Working set after ~10 popup opens + 4 rich previews | **230.5 MB** | rich-preview retention is real |
| Popup geometry | 825×780 px = **550×520 DIP** exactly | pass |
| Popup position | x = 365 DIP (= (1280−550)/2, centred ✓), **y = 76 DIP** (not vertically centred; work area centre would be 124) | P3 |
| Header band / tab strip / status bar | 48 / 45 (rule at 93.3) / 32 DIP | pass |
| Compact row height | **70.7 DIP** (106 px), 5 rows consistent | spec says 65 (P3) |
| Selected row, 1-line clip | 70 DIP — body still reserved to 3 lines, leaving 2 blank lines | P1 (v2 §1) |
| Selected row, 3-line clip | 102.7 DIP | — |
| Selected row with rich preview | **217 DIP** (154 → +150 preview block) | P1 (v2 §4) |
| Selection rail | 2 DIP wide (3 px at x 13–15), gradient, spans 31 DIP (spec 24) | pass / P3 |
| Selection ring / tint | `#3E2517` 1 px inset ✓, `#1C1310` = accent 8 % over App ✓ | pass |
| Hover tint | `#121316` = the `RowHover` token (4 % overlay) ✓ | pass |
| Text anti-aliasing | **0 colour-fringed pixels** on neutral text (body/meta/status) vs 400/1503 on the tinted selected row | **greyscale AA — directive 7's root cause confirmed by pixel count** |

---

## 2. Scorecard per surface (`CHECKLIST-uiux.md` §8 dimensions, 0–4)

| Surface | Access | Perf | Theming | Platform | Adaptivity | **Score** |
|---|---|---|---|---|---|---|
| **Popup** | 3 | 2 | 3 | 3 | 2 | **2.6 / 4** |
| **F2 editor** | 2 | 3 | **1** | 2 | 2 | **2.0 / 4** |
| **Notification** | 2 | 3 | **1** | 2 | 2 | **2.0 / 4** |
| **Settings** | — | — | — | — | — | **0 / 4 — never renders (P0 crash)** |
| **Tray** | 3 | 4 | **1** | 3 | 2 | **2.6 / 4** |
| **Overall** | | | | | | **≈1.8 / 4** |

Rationale for the low marks: the popup's typography/colour system is genuinely good and token-driven, but
(a) selection still changes geometry so ↑/↓ moves the list under the cursor, (b) the bottom edge slices a
row mid-glyph, (c) the editor, notification, tray menu and Ctrl+M picker each leak an unthemed Windows
default (blue focus ring, white scrollbar, white menu gutter, italic ✕), and (d) the Settings surface is
unreachable because opening it terminates the process.

---

## 3. Findings

### P0

**P0-1 · Opening Settings kills the app (unhandled exception on first render).**
Reproduced twice. `Clip.App.OpenSettings` → `SettingsWindow` → `Window.Show()` → Arrange →
`System.InvalidCastException: Unable to cast object of type 'System.Windows.Style' to type
'System.Windows.Media.Brush'` at `Border.OnRender`, process terminates (tray icon and Alt+V die with it).
Cause (read, not guessed): `UI\SettingsWindow.xaml` declares `<Style x:Key="Field" TargetType="TextBox">`,
which **shadows the theme brush** `<SolidColorBrush x:Key="Field">` for every `{DynamicResource Field}` in
that window — including the style's own `<Setter Property="Background" .../>` and its caret/selection
brushes. The first Border in a settings card therefore receives a Style where a Brush is required.
Evidence: `settings-open-attempt.png` (nothing rendered; desktop unchanged), event log excerpt above,
`docs\design-evidence\s3-audit\settings-open-attempt.png`.
Impact: Settings, theme switch, hotkey rebind, autostart toggle, Clear-history confirm, notification
preview overlay and every §2/§7 Settings acceptance row are **unreachable** — and the first time the owner
clicks "Settings…" in the tray the whole clipboard manager disappears.
Suggested one-line fix (not applied — I don't edit source): rename the Style key (`Field` → `FieldBox`) or
the brush key. Owner-facing consequence must also be fixed: **never let a window die from a XAML error.**

### P1

**P1-1 · Bottom edge still slices a row mid-glyph** — `nav-2.png` → `zoom-nav2-bottom-clip.png`.
The last visible row ("COPY-BACK-ONE 1") is cut horizontally through the glyphs and through its 22 px tile.
This is the incumbent defect the checklist claims S3 fixed; it is not fixed. Cause visible in the layout:
rows are 70.7 DIP and the viewport's height is not a multiple of that, so the remainder always cuts the
last row (see also `zoom-30lines-bottom.png`, where the cut lands just after the meta hairline and the
metadata line vanishes entirely). No scroll cue either.

**P1-2 · Tray context menu shows a bright white gutter down its left side** — `zoom-tray-menu.png`.
The menu inherits WPF's default Aero2 template: the icon/checkmark gutter renders white on a dark menu,
square corners, no hover styling. Reads as a different, unfinished app next to the popup. (Menu items
themselves are correct and truthful: "Open Clip / Settings… / Clear history… / Exit".)

**P1-3 · Images tab is a narrow list of 22 DIP thumbnails** — `tab-images.png`, `tab-pinned-with-item.png`.
Images render as a 22×22 DIP tile in a text row ("Image · 1 KB", meta line), not a grid, not a preview; the
selected image gets no larger preview at all. One of the two captured images rendered as a **blank grey
tile** — at that size a user cannot tell two images apart, which is exactly the owner's v2 §6 complaint.
(For reference the owner already rejected 96 px; the shipped tile is 22 px.)

**P1-4 · Rich preview is an appended block, not a content swap, and renders in an unstyled serif** —
`nav-2.png` → `zoom-nav2-richpreview.png`. Selecting a rich clip appends up to 150 DIP of preview *below*
the meta line (row measured 217 DIP tall; every row below jumps), instead of swapping the row's own content
inside a fixed height. The panel renders HtmlRenderer's default type — large serif, its own colours — not
the app's 13 px system (DESIGN-typography §5.8's base stylesheet is missing); a one-line clip leaves ~120
DIP of empty box.

**P1-5 · Selection changes geometry: the list jumps on ↑/↓** — `nav-0.png` … `nav-4.png`, measured.
Rows were 70.7 DIP compact and 102.7 DIP selected; moving the selection one row down moved the previously
selected row's content and every row after it (measured: row-2 meta rule at y = 398 px in `nav-0`, at
456 px in `nav-1`; the newly selected row itself moved **up 47 px**). A 1-line clip also grows to a 3-line
box, leaving two blank lines. This is the owner's v2 §1 complaint reproduced with numbers.

### P2

| # | Finding | Evidence |
|---|---|---|
| P2-1 | **"+N more lines" badge counts CRLF line breaks twice.** 30 CRLF lines → badge says **"+57 more lines"** (should be +27). A real paste from any Windows app uses CRLF, so the badge lies on the most common input. | `popup-dark-30lines.png` |
| P2-2 | **F2 on a rich clip opens a plain TextBox with no formatting.** Verified on an HTML/rich item: grey unformatted text, no colours/sizes. | `editor-rich-full.png` |
| P2-3 | **Editor leaks unthemed Windows defaults**: near-white default scrollbar track on the right, blue system focus border around the text area; body type reads soft/larger than the popup's. | `editor-rich-full.png`, `editor-pre.png` |
| P2-4 | **Ctrl+M tab picker uses the default blue selection/focus ring** (system accent), clashing with the orange accent; panel has no title. | `key-ctrlm-picker.png` |
| P2-5 | **A clip with no text body renders as an empty row** — blank 18 px body line, tile + meta + "+395 more lines" badge, nothing readable in the row's main slot. | `popup-search-filter.png` |
| P2-6 | **Notification is the pre-S3 design**: italic muted timestamp (typography spec bans italic), no 3 px accent rail, ✕ is a literal glyph from a fallback font with no accessible name, and the configured height (160 DIP) is **not** honoured — the toast rendered ~54 DIP (SizeToContent wins over config). | `zoom-notification-1.png`, `zoom-notification-3.png` |
| P2-7 | **Text-formatting options missing on the notification window** (`TextFormattingMode`/`TextRenderingMode`/`UseLayoutRounding` are set on popup/settings/editor, absent from `NotificationWindow.xaml`), so §3's "small-size stack on ALL windows" is not met. | source + `zoom-notification-1.png` |

### P3

- **Compact row = 70.7 DIP, spec = 65.** Cause: the 22 px tile makes the row's first grid row 22 DIP tall while the spec's arithmetic assumed an 18 px text row.
- **Tab underline floats ~5 DIP above the strip divider** instead of sitting on it; tab counts have no "·" separator ("Clipboard 93" vs the pinned "Pinned · 4").
- **Status-bar copy deviates** from the pinned keycap copy: renders "Enter paste / F2 edit / Esc hide" rather than "↵ paste / esc hide"; no keycap for plain paste (Ctrl+Shift+V) though the feature exists.
- **Search placeholder is per-tab** ("Search Clipboard" / "Search Images" / "Search Pinned"), not the documented "Search history, groups…".
- **Pinned tab with 0 items leaves the status bar's left text blank** (no "0 clips").
- **Meta line uses `LineHeight 14`** where the typography table says 11/16.
- **Notification app name uses `Accent` (#FF8A3D) not `AccentText`**, and the ✔/✕ glyph rule (typography §8.8) is still violated there.
- **Popup is 76 DIP from the top** rather than vertically centred on the monitor.

### What is genuinely good (and should survive v2)

- Idle CPU is a true 0 %, idle RAM 97 MB, warm hotkey 12–32 ms.
- The selection grammar is exact: 2 px gradient rail + `#1C1310` (accent 8 %) tint + `#3E2517` inset ring,
  nothing moves horizontally; hover uses the 4 % `RowHover` token.
- Every colour in the popup is a token; the corrected contrast palette is present in **both** theme files
  (`Muted #9BA1AC/#586070`, `Placeholder #82868F/#69696E`, `Error`, `OnAccent`, `ControlLine`,
  `AccentText #B84900` light, `Sage/Amber/Sky/Violet`) — §3's token fix is real at the token level.
- Time sections, hairlines, tab strip with counts + accent underline (amber on Pinned), and all three empty
  states carry designed, truthful copy:
  - Pinned: "Nothing pinned / Press Ctrl+P on a clip to keep it here. / Pinned clips stay when you clear history."
  - Images: "No images yet / Copy a screenshot or picture, and it appears here. / `Win+Shift+S` Take a screenshot"
  - No matches: "No clips match "f" / Try fewer words, or search the app name."
- Esc cascade works exactly as specified (Esc #1 clears the search and keeps the list, Esc #2 hides —
  verified with real keypresses: popup hwnd alive after #1, `hwnd=0` after #2).
- Live filter ✓, status count ✓ ("4 clips", "1 image", pluralised), search caret visible and accent-coloured.
- Toast **reuse** ✓ — three copies 250 ms apart produced exactly **one** toast showing the last content.
- Tray icon is reachable in the Win11 overflow, left-click opens the popup, right-click opens the menu.
- Editor: discard prompt works ("Discard changes? · Keep editing / Discard"), and Esc with **no** changes
  closes silently without prompting (both verified); "Save changes" uses accent fill + `OnAccent` ink.

---

## 4. vs the owner's v2 directives (`DESIGN-v2-directives.md`)

| # | Directive | What the current app actually does (my evidence) |
|---|---|---|
| **1** | **No geometry change on selection; kill the scroll jumping** | **Violated.** Row height changes with selection (70.7 → 102.7 DIP, → 217 with preview). Measured on `nav-0…nav-4`: moving selection down one row shifted the row content above it and moved the newly selected row **up 47 px**; a 1-line clip still reserves a 3-line body, so two blank lines appear. My own arrow-walking produced the jump the owner described. |
| **2** | **No per-row title/meta section** | **Violated.** Every row carries a full hairline + "Notepad  23 Nov 16:22" metadata line (`popup-dark.png`, `zoom-selected-row.png`, `popup-text-before.png`). Rows are not content-only. |
| **3** | **Sequential numbers (1, 2, 3 …) replace the icon tile on the Clipboard tab** | **Violated.** Every row's top-left slot is a 22 DIP rounded tile with a drawn "three lines" glyph, identical for all text clips (`zoom-selected-row.png`). No number appears anywhere in the popup; nothing on screen hints at Ctrl+1..9. |
| **4** | **Rich text renders in the main popup inside a fixed height (content swap, no geometry change)** | **Violated.** Rich content is appended as a separate panel **below** the meta line and grows the row by up to 150 DIP (`zoom-nav2-richpreview.png`); it is rendered in unstyled serif with the clip's own colours, not in the row's fixed content slot. |
| **5** | **F2 editor = proper WYSIWYG rich editor** | **Violated, reproduced.** F2 on the rich item "PLAIN-TEST-CHARLIE rich body" opened a plain multiline TextBox with unformatted grey text — no colours, no sizes, no WYSIWYG (`editor-rich-full.png`). Combined with the directives' accepted root causes this matches "opens plain for rich clips". The editor also shows a white default scrollbar and a blue focus border. |
| **6** | **Images tab = big previews filling the popup** | **Violated.** Images are listed as 22×22 DIP tiles in text rows with no preview at any size; the selected image gets nothing bigger (`tab-images.png`), and one captured image displayed as a blank grey tile — you cannot recognise an image without opening it. |
| **7** | **Text crisp and sharp everywhere (no AllowsTransparency greyscale AA)** | **Confirmed as still broken, pixel-measured.** On neutral backgrounds the popup's body/meta/status text has **0 colour-fringed pixels out of 936 ink pixels** (greyscale AA only) — ClearType is not in play; `popup-text-before.png` is the 3× before-shot for the sprint's comparison. The notification window additionally lacks the formatting options entirely, so it is the softest surface. |

*Superseded design rows:* where an older checklist row encodes the design v2 has now deleted (per-row meta,
grow-on-selection, 65 px meta-always anatomy), I did **not** mark it PASS — it is marked
**SUPERSEDED BY DESIGN-v2-directives.md** in the checklists.

---

## 5. [U] row walk — verdict counts

| Checklist | PASS | FAIL | UNTESTED | SUPERSEDED |
|---|---|---|---|---|
| `CHECKLIST-uiux.md` | 26 | 9 | 20 | 6 |
| `CHECKLIST-features.md` | 4 | 3 | 9 | 0 |

Every row carries its verdict and evidence file inline in the checklist files. Recurring reasons for
UNTESTED:

1. **Settings window crashes on open** (P0-1) — the whole §2 Settings restyle, theme switching, hotkey
   rebind, autostart toggle, notification preview overlay, inline validation, clear-history confirm.
2. **Light theme** — only reachable through Settings; the owner's `config.json` may not be edited by an
   auditor, so no light screenshot exists. Token values were verified by reading both theme dictionaries.
3. **Animation timings** (140 ms open, 83 ms close, 380 ms pulse, FLIP) — static captures cannot time a
   keyframe; only the *state* consequences were judged.
4. **Multi-monitor / per-monitor DPI switch** — one 1920×1200 display only.
5. **First-run hint** — would require renaming the owner's `%APPDATA%\Clip` state (forbidden).
6. **Destructive paths** (Clear history, autostart registry, paste-into-app) — deliberately not driven; my
   synthesised keys land in whatever window has focus and a multi-line paste into the auditor's own shell
   could execute text.

---

## 6. What only the owner can eyeball

- **Light theme end-to-end** (needs the Settings crash fixed first, or a one-off `"theme": "light"` run by you).
- **Multi-monitor placement** (popup follows the active window's monitor; toast follows the copy's).
- **The 380 ms copy-pulse moment** and the 140 ms entrance — motion cannot be judged from stills.
- **Tray icon crispness when pinned outside the overflow** at 16/24/32 px (I reached it inside the flyout only).
- **First-run hint** ("… is running — press Alt+V").
- **Paste fidelity into Excel/Word/Paint** — real Office round-trip.
- **Whether the v2 look is what you want at 150 %** — the geometry is exact, but the soft text (directive 7)
  is the thing that makes it feel cheap, and that is fixed/verified in the crisp-text sprint.

---

## 7. Files

`popup-dark.png`, `popup-dark-full.png`, `popup-dark-30lines.png`, `popup-search-filter.png`,
`popup-search-empty.png`, `popup-after-esc1.png`, `popup-hover-row.png`, `popup-clipboard-restored.png`,
`nav-0.png`…`nav-4.png`, `popup-text-before.png`, `tab-images.png`, `tab-pinned.png`,
`tab-pinned-with-item.png`, `tab-back-clipboard.png`, `editor-pre.png`, `editor-rich-full.png`,
`editor-rich-open-full.png`, `editor-esc-nochanges.png`, `editor-discard-result.png`, `key-home.png`,
`key-end.png`, `key-pagedown.png`, `key-pageup.png`, `key-ctrlm-picker.png`, `key-ctrlp-pin.png`,
`key-ctrlp-unpin.png`, `key-delete.png`, `key-ctrlz-undo.png`, `key-ctrltab.png`, `key-ctrlc.png`,
`notification-1.png`, `notification-3.png`, `notification-toast.png`, `taskbar-full.png`,
`tray-overflow-open.png`, `tray-flyout-2.png`, `tray-menu-open.png`, `settings-open-attempt.png`,
`ctx-menu-full.png`, plus the `zoom-*.png` crops quoted above.

---

## 8. Side effects I caused (nothing hidden)

1. **One image clip was deleted** by the Delete-key probe: my arrow/Ctrl+Tab keys had left the Images tab
   active, so `Delete` removed the selected image (count went from 1 to none — `key-delete.png`). Ctrl+Z was
   sent immediately afterwards; a later capture shows "Images 1" again, but I cannot prove that row is the
   original one (a parallel agent was also copying screenshots into the clipboard during the walk).
2. **~10 test clips** I copied are now in history: `LINE 1…30` block, `one/two/three/four/five`,
   `NOTIFY-REUSE alpha/bravo/charlie`, and a `PLAIN-TEST` search string. They are ordinary UI copies, no SQL.
3. **No edit was saved**: the editor was closed through its own **Discard** button, and the "PLAIN-TEST"
   text I appended to the editor buffer was discarded, so no stored clip was modified by me.
4. `%APPDATA%\Clip\config.json` and `clip.db` were never edited, renamed or seeded; I never clicked
   "Clear history…" or "Exit".
