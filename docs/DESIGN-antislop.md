# Clip — anti-slop design audit and decisions

Researched and written 23 Nov 2026. Scope: the shipped XAML in `UI\`, the theme dictionaries,
the microcopy in `.xaml` and `.cs`, and the planned surfaces already frozen in
`docs\CHECKLIST-features.md` / `docs\CHECKLIST-uiux.md`. No code was changed.

This doc is written to be used twice:

1. now, as the design decision record for the real build;
2. later, as the audit script for a fresh agent that has never seen the code. Section 3 is
   deliberately phrased as ten checks that can be settled from screenshots alone.

**Verdict in one paragraph.** Clip's palette is not slop. Manrope is a chosen face, the orange is
a chosen accent, there is no gradient, no glass, no hero, no pill, no status dot. What reads
"AI-made" is the *furniture*: a bordered card around every clip, a raised box around the search
field, three different icon systems on one screen, an italic grey meta line joined by middle
dots, a type scale compressed into 11/12/13, and copy that describes a different app than the one
that runs ("Delete every saved clip?" while pinned clips survive; "Close" while edits are
discarded). The fix is subtraction plus one honest state language, not a redesign. The light
theme also has measured contrast failures where accent and error are used as text or glyphs.

---

## 1. Authority, and how conflicts were ruled

Order of authority used in this document:

1. **Craft floor** (`~/.config/opencode/skills/impeccable/reference/craft-floor.md`) — law.
   Its *Verify* list (contrast, spacing, states, copy) is a set of checks on the result; its
   *Refuse* list is a set of defaults that a committed brief may earn back, except the one
   explicit ban (an eyebrow/kicker above a heading) and the icon line, which is written as a ban:
   "Unicode glyphs or emoji standing in for an icon system. Icons are drawn…" [S1]
2. **The pinned visual world**: Ortu, as measured in `docs\RESEARCH-ortu-ui.md` and re-read from
   source for this audit [S16].
3. **Owner decisions** in the two checklists (absolute time instead of relative, tabs, 520 height,
   inline confirm, etc.).
4. **Platform guidance** (Fluent / Windows 11) where it does not contradict 1–3.

Rulings that follow, stated once so they are not relitigated per screen:

- **Ortu's faint text is not copied.** Ortu's placeholder is `fg/0.18` and its status bar is
  9 px at `fg/0.20`. Those are decoration in a marketing screenshot and fail the floor's
  placeholder/contrast check. Clip uses `Muted` (measured in §5) at ≥10 px for real text.
- **Ortu's left rail is allowed, but only as the pinned world's signature.** The floor refuses
  "a bar down one side of a rounded box (a one-sided border, inset shadow or pseudo-element)" as a
  default. Ours is earned by the pinned brief and must be built as a rounded `Rectangle` element,
  never `BorderThickness="2,0,0,0"` and never an inset shadow. If it is built as a border trick it
  becomes the thing the floor refuses.
- **9 px uppercase section labels are allowed in Settings.** The floor bans an *eyebrow above a
  heading*; a section label that is itself the heading is not an eyebrow. They carry grouping, so
  they stay — but never as a second label repeating the row's own label.
- **Draw the icons.** Windows permits Segoe Fluent Icons, but a system-icon font makes Clip read
  like a stock Windows pane; Ortu draws, and the floor's icon line is a ban. See §8.

---

## 2. What "AI-looking" means, in six mechanisms

The 2025–26 practitioner literature has converged on a small set of mechanisms rather than a long
list of styles. Cite these, not vibes.

| Mechanism | What it looks like | Why it reads as machine-made | Counter-move (what top utilities do) |
|---|---|---|---|
| **Averaging** | Indigo gradient, Inter, three cards, dark by reflex | A model returns the statistical centre of its training data when the project supplies no decisions. Generic is worse than ugly because it is forgettable. [S3][S4][S6] | Decide the few axes that matter (face, palette, container, density) and hold them on every surface. |
| **Decoration without a job** | Gradient text, glass, blobs, pills, status dots, tracked capitals, sparkles | Each is a signal borrowed from somewhere; used everywhere it stops signalling. The tell is the *pile*, not any single item. [S3][S5] | One accent doing semantic work; flat surfaces; colour reserved for state. |
| **The card as default container** | Every row, setting and section is a rounded bordered box; boxes inside boxes | "When everything is in a box, nothing is grouped." Equal containers flatten hierarchy. A 1 px border plus soft shadow on every surface is one of the most cited tells. [S4][S5][S7] | Containers only where they separate a floating surface or a real group; within a surface, use fill and spacing. |
| **Compressed type + faint metadata** | Headings 1–2 px from body; body white, secondary text 11 px grey italic | The eye is given no hierarchy, so the eye is given italics and grey as a costume. "Excessive italics" and "colour shades and opacities: pick three" are named tells. [S4][S5] | One body size (13) and one label family (9–11); hierarchy by size, weight, then one colour step. |
| **Icon substitution** | Emoji, Unicode ✓ ✕ ▾, mixed font glyphs, thin interchangeable line icons | Characters are not an icon system: no shared stroke, optical size or baseline; they render differently per font/OS. [S1][S7] | One drawn family, one stroke, a small size set scaled by role; system icons only where matching Windows chrome is the point. |
| **Copy that performs** | "Seamlessly", "Supercharge", exclamation marks, generic "Are you sure?", labels that do not name the action | Filler hides what will happen; a confirm that does not state the consequence cannot prevent the error. [S5][S7][S14][S15] | Name the object, the action and the consequence; specific buttons; no exclamation marks. |

Two supporting findings used below:

- **Icons and copy travel together.** The same pages that name the three-card layout name
  "confident and weightless" copy and "interchangeable thin icons" as part of the same
  fingerprint. [S6]
- **Empty/error/loading states are part of the fingerprint.** A model builds the happy path and
  stops; the absence of designed states is itself a tell. [S7][S13]

### What Windows 11 utility design does instead

- Windows 11's stated principles are *effortless, calm, personal, familiar, coherent*; colour
  "emphasiz[es] significant items only when necessary". [S8]
- Hierarchy comes from **layering and elevation**, where "shadows and contour (outlines) are used
  on controls and surfaces to subtly communicate an object's elevation", and "overusing shadows
  … can diminish their impact and create visual noise". Contour is an elevation signal, not a
  decoration to put on every list row. [S9]
- Compact density exists precisely for "productivity or data-entry apps (tables, forms,
  settings)": reduce height and padding so more content fits without scrolling. A clipboard popup
  is the archetypal compact surface. [S12]
- System icons are a **monoline** family with fixed metrics; `Segoe MDL2 Assets` has been
  superseded by `Segoe Fluent Icons` as the recommended symbol font, glyphs are "not intended for
  use in-line with text", and the recommended sizes are 16/20/24/32/40/48/64. [S10][S11]

That gives the positive rule for Clip:
**borders separate floating surfaces; fills separate states inside a surface; accent marks the one
thing that is chosen or primary; everything else is space and type.**

---

## 3. The ten slop checks for Clip

Each is a PASS/FAIL a fresh agent can settle from screenshots (plus one grep), without reading
this document's opinions. If any FAILs, the build is not ready for the `[U]` audit.

| # | Check | How to test | PASS condition |
|---|---|---|---|
| 1 | **Rows are not cards** | Popup with ≥6 clips, 100% and 200%. | Zero unselected rows have a border or a fill different from the list background. Exactly one row carries a surface, and it also carries the 2 px rail. Rows have no gap between them. |
| 2 | **Accent budget** | Popup at rest; Settings at rest; notification. | Accent is used only for (a) the selected row's rail/ring, (b) the one primary button's fill, (c) the focus ring. No accent body/meta text, no accent hover border on every button, no accent icon at rest. Count ≤3 semantic jobs per screen. |
| 3 | **Two type registries** | List every visible font size in the popup and in Settings. | ≤4 sizes per surface, and no heading sits within 1 px of its body size. Popup: 14 search, 13 body, 10 meta, 9 status. Settings: 13 row/title, 11 hint, 9 section label. |
| 4 | **Meta is legible and undecorated** | Zoom 200% on a row's second line. | Upright (not italic), ≥10 px, contrast ≥4.5:1 measured, exactly two facts (app + time), separated by spacing, no "·". Window title is not in the row. |
| 5 | **One icon family** | Screenshot at 100%/200%; inspect search, close, check, chevron, tray. | Every icon is a stroked path of one weight and optical size. No `✓ ✕ ▾`, no MDL2 `E721` at 12 px inline, no letter-in-a-square app/tray logo. |
| 6 | **Search is a band, not a box** | Popup at rest and while typing. | Header is a flat band with a bottom hairline, no rounded filled container. Placeholder is visible at rest and passes contrast. Caret is visible on open. |
| 7 | **Every empty state is designed** | Four screenshots: cleared history, query "zzzz", Images tab empty, Pinned tab empty. | Each shows a headline in the product's voice plus one teaching line naming the action or key; nothing is blank or "No results" alone. |
| 8 | **Copy is honest** | Read every string on every destructive path, plus the footer of Settings. | Each destructive confirm says what is deleted **and what survives**; buttons name the outcome ("Delete clips" / "Keep clips"), never OK/Yes/No; no exclamation marks; no control called "Close" if it discards. |
| 9 | **One spacing ladder** | Measure 8 gaps at 100%: window edge, header, row padding, row gap, settings card padding, card gap, section gap, footer. | Every value is on 2/4/6/8/10/12/16/24; row gap is 0; section gap (24) > card gap (8); no negative margins. |
| 10 | **Theme purity and light-theme contrast** | Grep shipping XAML for `#` outside `UI\Themes\*.xaml`; screenshot both themes. | Zero hex outside the dictionaries except `#000000` in shadows. Error text, accent-bearing glyph and selected state are legible in both themes (≥4.5:1 text, ≥3:1 glyphs). |

Check 10 is the one most likely to FAIL today: see §5.

---

## 4. Current-XAML verdicts

Legend: **Y** = reads as slop / template; **N** = not slop (keep, or keep with a note).
Severity is about how much it drags the whole app toward "assembled": H, M, L.

### 4.1 Popup (`UI\MainWindow.xaml`)

| # | Item | Slop | Sev | Evidence (file:line) | Fix |
|---|---|---|---|---|---|
| P1 | Search is a raised rounded box inside the window | **Y** | H | `MainWindow.xaml:38-40` — `Raised` fill + `Hairline` 1 px + radius 8 + padding 10,9 + margin bottom 12 | Flat 48 px band: bottom hairline only, padding `14,0`, input 14 px, no fill, no radius. Ortu's exact pattern. |
| P2 | Search icon is a font glyph from the superseded MDL2 set, at 12 px, inline with text | **Y** | H | `MainWindow.xaml:46-47` — `&#xE721;` / `Segoe MDL2 Assets` / 12 px | Drawn 14×14 magnifier path, stroke 2 in a 24 viewBox, `Muted`. Microsoft: MDL2 is superseded, glyphs are not for inline text, optimal sizes are 16/20/24… [S11] |
| P3 | No placeholder in the live window | **Y** | M | `MainWindow.xaml:48-51` — empty `TextBox`, no overlay | Overlay `TextBlock` shown when empty: "Search Clipboard", `Muted`, IsHitTestVisible false. Do **not** copy Ortu's 0.18 opacity; floor requires 4.5:1. |
| P4 | Card per clip: border + radius + surface fill + 6 px gap | **Y** | H | `MainWindow.xaml:9-23` (`Card`: `Surface`, radius 10, `Hairline` 1, padding 12,10, margin 0,0,0,6); used at `75-84` | Flat rows: transparent, radius 12, padding `12,8`, zero gap; list inset 6. Cards belong in the main window, not a 520 px keyboard list — see §4.5. |
| P5 | Selection is a full accent border | **Y** | H | `MainWindow.xaml:17-21` — `Background=Raised`, `BorderBrush=Accent`, thickness 1 | Selected = `Raised` fill + 2×24 px accent rail + optional 1 px low-alpha ring; no full accent border. A full orange border on one of twenty rows is the loudest possible state. |
| P6 | Body is pure `Fg` at 13, clamped to four lines, always | **Y** | M | `MainWindow.xaml:77-79` — `Foreground=Fg`, `FontSize 13`, `LineHeight 19`, `MaxHeight 76` | Unselected: `FgSoft` 13, one line, ellipsis. Selected: `Fg` 13, up to 3 lines at line-height 18. The growing row is Ortu's signature and also the reason no detail pane is needed. |
| P7 | Meta is 11 px italic `Muted`, with middle dots and the window title in the row | **Y** | H | `MainWindow.xaml:80-81`; `Core\ClipboardItem.cs:28-44` (joins `"  ·  "`, `"Unknown"`, 60-char title) | Upright 10 px `Muted`; two facts only (app, absolute time), separated by an 8 px gap; no dots; window title in the row tooltip. Italic is the "thin grey italic" tell and is the only italic in the app, so it is noise rather than voice. [S5][S3] |
| P8 | No hover state on rows | **Y** | M | `ListBoxItem` template `60-71`; `Card` has only a selected trigger `16-22` | Hover = `#1C1E24` (dark) / slightly darker than Surface (light), 100 ms opacity-only; declare hover trigger before selected so selected wins. |
| P9 | No empty state of any kind | **Y** | H | Absent from `MainWindow.xaml` | §7. |
| P10 | Scrollbar hidden always | N | — | `MainWindow.xaml:55` | Keep hidden at rest; show Ortu's 3 px thumb while scrolling. |
| P11 | Popup 560×620, list with 4-line cards | N (not slop) | — | `MainWindow.xaml:4` | Set 550×520 (owner-approved). The number is a density decision, not a slop signal; 620 with cards is what makes the list feel like a web feed. |
| P12 | Soft window shadow 28/6/0.55 | N | — | `MainWindow.xaml:28-30` | Keep. It has offset and blur and marks a floating surface, which is what the floor asks for. |

**The card-vs-rows question, answered.** Ortu uses both: cards in its main window, flat rows in its
popup. A card is not inherently slop; a card *per row at the same elevation as every other row* is.
In a 550 px popup the border does no grouping work (every row gets one, so nothing is grouped), and
it is asked to carry selection, which makes the selected row shout. Flat rows read *designed*
because the list becomes one object with a cursor; cards read *template* because they are the
container the model reaches for by default. [S4][S5][S7]

### 4.2 Settings (`UI\SettingsWindow.xaml`)

| # | Item | Slop | Sev | Evidence | Fix |
|---|---|---|---|---|---|
| S1 | Root is `Surface`; every row is a `Raised` bordered mini-card | **Y** | H | `187-192` (root `Surface`, radius 14), `26-33` (`Row`: `Raised`, radius 8, `Hairline` 1, padding 9,5, margin 4) | Root `App`; group cards `Surface` with one border; rows inside a card have no border and no fill. This is also what fixes the light-theme hint contrast (§5). |
| S2 | Compressed type scale: heading 13 SemiBold, row 12 regular, hint 11 | **Y** | H | `12-17` (`H` 13/SemiBold), row titles e.g. `216`, hints `19-24` | One body size (13) and one label family: 11 px hint, 9 px SemiBold uppercase section label + `CharacterSpacing 100`. No 12 px body. |
| S3 | Duplicate labels: section header plus row label say the same thing | **Y** | M | `242-249` ("Max items" then "Items kept in history"), `265-270` ("Preview lines" then "Lines shown per clip card in the popup") | One label per control. A section label groups rows; it never repeats the row's own label. |
| S4 | Nine sections for nine controls, each a 13 px heading | **Y** | M | `209-396` (Hotkey, Max items, Preview lines, Notification, Theme, Time format, Start on login, Clear history…) | Five groups: Hotkeys, History (includes Clear history), Notifications, Appearance, Startup. Section gap 24, card gap 8. |
| S5 | Negative margins pull hints/errors upward | **Y** | M | `239` margin `2,-6,0,10`; `257` `2,-2,0,10`; `275` `2,-2,0,10`; preview hint `271` margin `2,0,0,4` | Reserve the error line's height or let it take normal flow. Negative margins are the mechanical signature of "make it fit" rather than "lay it out". |
| S6 | One hardcoded error red, shared by both themes | **Y** | H | `10` — `ErrorBrush #FF6B6B` | Theme tokens: dark `#FF6B6B` (5.93:1 on Raised), light `#B42318` (5.66:1 on Raised, 6.57 on white). AGENTS.md already records this as the accepted exception; this replaces it with a token. |
| S7 | Checkbox tick is the character `✓` in `Surface` colour | **Y** | H | `46-74`; tick at `57-60`, `Foreground=Surface` | Drawn 10 px check path; tick always `OnAccent` (near-black) → 7.65:1. Today in light theme: white on orange = **2.35:1** (fails 3:1 for a glyph). |
| S8 | Combo chevron is the character `▾` | **Y** | M | `95` | Drawn 9 px chevron path, same stroke as every other icon. |
| S9 | Accent is spent on every button's hover border, while the primary action is a neutral button | **Y** | H | `152-154` (all `Btn` hover → `Accent` border); `428-430` (`Close` ghost, `Save` neutral `Btn`) | `Save changes` = accent fill + `OnAccent` text (black, 8.49:1); `Cancel` = ghost. Hover on neutral buttons brightens the hairline/fill, not the accent. Accent must mean "the chosen action". |
| S10 | "Close" discards edits with no warning, and the footer teaches "Esc to close" | **Y** | H | `428`, `426-427`; `SettingsWindow.xaml.cs:181` (`Close()`), no dirty check | Rename to `Cancel` (honest); add a dirty guard only if the owner approves (§10). Keep `Save changes` as the only accent. |
| S11 | Clear-history confirm repeats "Clear" and states a falsehood | **Y** | H | `401` (`Clear`), `405` ("Delete every saved clip? This cannot be undone."), `408` (`Clear` again); actual behaviour `Core\ClipboardStore.cs:269` — pinned and tab items are kept | Row button `Clear history…`; confirm "Delete all clips in Clipboard and Images? Pinned clips stay."; buttons `Delete clips` / `Keep clips`. Specific labels are an NN/g confirmation guideline. [S14] |
| S12 | "Duration (ms)" is an engineer's unit; `1–100` preview lines contradicts a 3-line row | **Y** | M | `293` (`Duration (ms)`), `270-273` (1–100) vs `MainWindow.xaml:78` (`MaxHeight 76`) | Show seconds ("Time on screen", `3.0 sec`); `Lines per clip` 1–3. See §10 for owner sign-off. |
| S13 | Redundant section/value pairs: "Theme" + "Appearance", "Time format" + "Clock", "Start on login" + "Launch when Windows starts" | **Y** | L | `356-363`, `373-380`, `389-392` | One label each: Theme, Time format, "Open when I sign in". |
| S14 | Six radii across the app (4, 6, 8, 10, 12, 14); four in Settings alone | **Y** | L | `54` (4), `124` (6), `28/151` (8), `187` (14); popup card 10 (`MainWindow.xaml:11`), toast 12 (`NotificationWindow.xaml:9`) | Four: 14 window, 12 card/row, 8 field/button, 6 keycap. |
| S15 | Inputs are transparent with no affordance until focus | N (fix via state, not border) | — | `35-44` | Keep borderless at rest; add a 1 px `Hairline` bottom rule or a `Raised` fill on focus; caret is already accent. Do not put a border on every field. |
| S16 | Primary `Save` is not visually primary | **Y** | H | `429` — same `Btn` style as everything | Covered by S9. |

### 4.3 Notification (`UI\NotificationWindow.xaml`)

| # | Item | Slop | Sev | Evidence | Fix |
|---|---|---|---|---|---|
| N1 | App name in accent at 12 px | **Y** | H | `29-30` — `Foreground=Accent` | `Fg` 11 px SemiBold. Accent as small text measures **2.35:1 on white** and has no semantic job here. The accent is for state, not for branding the toast. |
| N2 | Time is 11 px italic `Muted` | **Y** | M | `31-32` | 10 px upright `Muted`, tabular figures. Same rule as the row meta. |
| N3 | Close is a Unicode `✕` at 11 px in a ~21×13 hit area | **Y** | H | `35-49` (`Padding 5,1`, glyph 11) | Drawn 10 px X inside a 24×24 target in the header corner; `AutomationProperties.Name="Dismiss"`; hover to `Fg`, not accent. A 13 px-high target is hard to hit for no reason: the header corner has room. |
| N4 | `Surface` card on a floating toast | N | — | `9-14` | Keep radius 12 + hairline + shadow. Ortu uses `Raised` for its toast; that is a surface preference, not a slop signal. |
| N5 | "Image copied" as the body | N | — | `NotificationWindow.xaml.cs:129,135` | Keep. It is concrete and in the product's voice. |

### 4.4 Shell, meta model, tray

| # | Item | Slop | Sev | Evidence | Fix |
|---|---|---|---|---|---|
| A1 | Tray icon is a letter "C" in an orange square | **Y** | H | `App.xaml.cs:101-112` (`DrawString("C", …)`) | Drawn clipboard glyph, monochrome per taskbar theme, crisp at 16/24/32. A letter in a rounded square is the default logo of the AI era; it also breaks the drawn-icon rule. [S1] |
| A2 | Tray menu: `Show`, disabled `Settings`, `Clear History` with no confirm, `Exit` | **Y** | H | `App.xaml.cs:85-89` | `Open {Product}`, `Settings…`, `Clear history…` (opens Settings with the inline confirm visible), separator, `Exit`. No destructive action without the same confirm used in Settings. |
| A3 | Meta model emits "Unknown" and joins facts with middle dots | **Y** | M | `Core\ClipboardItem.cs:35,41` | Omit a missing app; return the two facts separately (`MetaApp`, `MetaTime`) so XAML can use a gap, not a dot. A screen reader reads each dot aloud and the string wraps mid-fact. [S3] |
| A4 | Single-instance message is terse and does not say how to recover | **Y** | L | `App.xaml.cs:39` | "{Product} is already running. Open it from the notification area." (Product name is already dynamic.) |
| A5 | Skip messages use "skipped", MB, and a technical frame | **Y** | M | `Core\CaptureService.cs:171,194,201` | "Image not saved: 7.2 MB is over the 5 MB limit." / "Image not saved: the format could not be read." Error copy names the problem and the recovery. [S15] |

### 4.5 What the mock taught us (do not copy)

`_fonttest\MainWindow.xaml` is historical (and `_fonttest\` is never built or edited, per
AGENTS.md). It contains four patterns that must not survive into the real build: fake
`Clipboard / Images` pills (`63-70`), the MDL2 search glyph (`56-57`), ad-hoc borders
`#2A2E3A` / `#333849` (`31`, `85`), and a faint hardcoded placeholder colour `#5C6270` (`59`).
The only thing worth keeping from it is the proof that the embedded Manrope renders.

---

## 5. Hardcoded colour inventory and measured contrast

### 5.1 Every hex outside `UI\Themes\*.xaml`

| File:line | Value | Role | Verdict |
|---|---|---|---|
| `UI\SettingsWindow.xaml:10` | `#FF6B6B` | `ErrorBrush`, shared by both themes | **Fails light theme** (2.39:1 on `Raised`). Replace with themed `Error` tokens. |
| `UI\MainWindow.xaml:29` | `#000000` | Window drop shadow | Keep (shadow colour, has offset + blur). |
| `UI\SettingsWindow.xaml:191` | `#000000` | Window drop shadow | Keep. |
| `UI\NotificationWindow.xaml:13` | `#000000` | Toast drop shadow | Keep. |
| `App.xaml.cs:108,109,112` | `#08090C`, `#FF8A3D` | Tray bitmap duplicates theme values in GDI | Remove with the drawn tray icon; if any GDI drawing remains, read the brush from the theme, not a literal. |
| `_fonttest\MainWindow.xaml:12-18,31,33,59,85,108` | theme token duplicates (12-18), `#2A2E3A` (31), `#000000` (33), `#5C6270` (59), `#333849` (85), accent demo text (108) | Scratch mock | Out of scope; do not ship. |

No other shipping XAML contains a literal colour. That is a genuinely good result and should be
protected by check 10.

### 5.2 Measured failures (WCAG relative luminance)

| Foreground / background | Ratio | Requirement | Result |
|---|---|---|---|
| `Muted #8A909C` on dark `Raised #1C1F28` | 5.13 | 4.5 (11 px text) | PASS |
| `Muted #8A909C` on dark `Surface #13151B` | 5.69 | 4.5 | PASS |
| `Muted #8A909C` on dark `App #08090C` | 6.21 | 4.5 | PASS |
| `Accent #FF8A3D` on light `Surface #FFFFFF` (notification app name) | **2.35** | 4.5 | **FAIL** |
| `Surface #FFFFFF` on `Accent #FF8A3D` (light-theme check tick) | **2.35** | 3.0 (glyph) | **FAIL** |
| `Error #FF6B6B` on light `Raised #ECEEF1` | **2.39** | 4.5 | **FAIL** |
| `Muted #68707E` on light `Raised #ECEEF1` (settings hints today) | **4.29** | 4.5 | **FAIL** |
| `Muted #68707E` on light `Surface #FFFFFF` | 4.99 | 4.5 | PASS |
| `FgSoft #B9BBC2` on dark `Surface #13151B` (proposed) | 9.52 | 4.5 | PASS |
| `FgSoft #4A5058` on light `Surface #FFFFFF` (proposed) | 8.14 | 4.5 | PASS |
| `Error #B42318` on light `Raised #ECEEF1` (proposed) | 5.66 | 4.5 | PASS |
| `OnAccent #15171C` on `Accent #FF8A3D` (proposed) | 7.65 | 4.5 | PASS |

Two fixes follow from the table, and they are structural rather than cosmetic:

1. **Accent never sets small text.** Remove the accent app name from the toast; use `Fg`.
   Primary buttons use accent *fill* with `OnAccent` text.
2. **Settings cards become `Surface` on an `App` root.** That alone lifts the light-theme hint
   from 4.29 to 4.99. Then darken light `Muted` to `#5F6673` (5.78 on white, 4.97 on `Raised`,
   5.2 on `App`) so selected-row meta also passes.

Proposed token set (only the deltas shown):

| Token | Dark | Light | Job |
|---|---|---|---|
| `FgSoft` | `#B9BBC2` | `#4A5058` | Unselected row body |
| `Muted` | `#8A909C` (unchanged) | `#5F6673` (was `#68707E`) | Meta, hints, labels |
| `OnAccent` | `#08090C` | `#15171C` | Text/glyph on accent fill |
| `Error` | `#FF6B6B` | `#B42318` | Inline errors only |

Three text levels total (`Fg`, `FgSoft`, `Muted`). Do not add a fourth grey; that is the
"six opacities" tell. [S5]

---

## 6. Final microcopy

### 6.1 Rules applied

- Sentence case. No exclamation marks. No "successfully", "seamlessly", "simply".
- No em dashes in UI strings (a named AI-copy tell [S5]); use a full stop or a second sentence.
- A control names its action. A destructive confirm names **what is deleted and what survives**
  and offers outcome-labelled buttons, not Yes/No. [S14]
- An error names the problem and the recovery, in the user's terms. [S15]
- Numbers and units are concrete; examples are shown where a format is ambiguous.
- Key legends are words (`Enter`, `Esc`), not `↵`/`glyph` characters: Manrope may lack the glyph
  and fall back to another font, which breaks the one-family rule.
- No visible XAML string hardcodes the product name, so the one-place rename still holds.
  Dynamic names are written `{Product}`.

### 6.2 The table

| Surface | Current | Final copy | Note |
|---|---|---|---|
| Popup placeholder, Clipboard tab | *(none)* | `Search Clipboard` | Scoped by tab; no ellipsis. |
| Popup placeholder, Images / Pinned / custom | *(none)* | `Search Images` / `Search Pinned` / `Search {tab}` | Names the scope. |
| Search clear control | *(none)* | tooltip + automation name: `Clear search` | Bound to non-empty text. |
| Tabs | planned `Clipboard / Images / Pinned` | `Clipboard`, `Images`, `Pinned`, `{tab}` | Counts are separate 10 px tabular text, no "·": `Pinned` + `4`. |
| Row meta | `Edge  ·  23 Nov 09:47  ·  window title`; `Unknown`; italic | `Edge` + gap + `23 Nov 09:47`; hide app when missing; window title in tooltip | Two facts, no dots, no italics. |
| Rich-preview overflow badge (clickable) | *(planned "+90 more lines")* | `Show 90 more lines` / `Show 1 more line`; expanded: `Show less` | If non-interactive: `90 lines not shown`. Never a bare `+90`. |
| Status bar left | *(none)* | `1 clip` / `12 clips`; Images: `3 images`; Pinned/custom: `4 clips` | Singular handled. Hidden when the list is empty. |
| Status bar right (keycaps) | planned `↵ paste` / `esc hide` | `Enter` `paste`   `F2` `edit`   `Esc` `hide` | Keycap style: Manrope 9 px SemiBold, `CharacterSpacing 20`, no monospace. |
| Empty: first run | *(none)* | `No clips yet` / `Copy text or an image anywhere, and it appears here.` / keycap `{Hotkey}` + `Open this list from any app` | Full spec §7.1. |
| Empty: no search match | *(none)* | `No clips match “{query}”` (Images tab: `No images match “{query}”`) / `Try fewer words, or search the app name.` | Query truncated at 40 chars. §7.2. |
| Empty: Images | *(none)* | `No images yet` / `Copy a screenshot or picture, and it appears here.` / keycap `Win+Shift+S` + `Take a screenshot` | §7.3. |
| Empty: Pinned | *(none)* | `Nothing pinned` / `Press Ctrl+P on a clip to keep it here.` / `Pinned clips stay when you clear history.` | §7.4. The second line is a statement, not a keycap. |
| Empty: custom tab | *(none)* | `Nothing in “{tab}”` / `Press Ctrl+M to move a clip into this tab.` | §7.5. |
| Notification close | `✕` (glyph) | tooltip + automation name: `Dismiss` | Drawn X, 24×24 target. |
| Notification image body | `Image copied` | `Image copied` | Keep. |
| Notification skip | `Image skipped: 7.2 MB over the 5 MB limit` | `Image not saved: 7.2 MB is over the 5 MB limit.` | Same for unreadable: `Image not saved: the format could not be read.` |
| Tray tooltip | `Clip` | `{Product}: clipboard history` | Product name dynamic. |
| Tray menu | `Show` / `Settings` (disabled) / `Clear History` / `Exit` | `Open {Product}` / `Settings…` / `Clear history…` / `Exit` | `Clear history…` opens Settings with the inline confirm shown; no silent wipe. |
| Single instance | `Clip is already running.` | `{Product} is already running. Open it from the notification area.` | Names the recovery. |
| Settings title | `Settings` | `Settings` | Fine. |
| Settings sections | Hotkey / Max items / Preview lines / Notification / Theme / Time format / Start on login / Clear history | `HOTKEYS` / `HISTORY` / `NOTIFICATIONS` / `APPEARANCE` / `STARTUP` | 9 px SemiBold, `CharacterSpacing 100`, `Muted`. Clear history sits in History; no section repeats a row label. |
| Settings row | `Open popup` | `Open the list` | "Popup" is developer vocabulary; avoids hardcoding the product name. |
| Settings row | `Paste as plain text` | `Paste as plain text` | Keep. |
| Settings hint | `Click a box, then press a new combination.` | `Click a field, then press the keys you want.` | "Box" is vague; "combination" is jargon. |
| Hotkey error | `The popup hotkey needs a modifier and a key, e.g. Alt+V.` | `Open the list needs a modifier and a key, like Alt+V.` | Names the control and gives an example. |
| Hotkey error | `The plain-text hotkey needs a modifier and a key, e.g. Ctrl+Shift+V.` | `Paste as plain text needs a modifier and a key, like Ctrl+Shift+V.` | |
| Hotkey error | `Must differ from the popup hotkey.` | `That is already the shortcut for Open the list.` | |
| Settings row | `Max items` + `Items kept in history` | `Clips kept` | One label. |
| Settings hint | *(none)* | `10 to 10,000. Pinned clips are kept separately.` | States the real cap semantics. |
| Settings error | `Enter a whole number between 10 and 10,000.` | `Choose a number from 10 to 10,000.` | Constructive, no blame. |
| Settings row | `Preview lines` + `Lines shown per clip card in the popup` | `Lines per clip` | One label; "card" language dies with the cards. |
| Settings hint | *(none)* | `1 to 3 lines in the list.` | Requires the range fix (§10). |
| Settings error | `Enter a whole number between 1 and 100.` | `Choose 1 to 3 lines.` | Matches the 3-line selected row. |
| Settings toggle | `Show a notification when something is copied` | `Notify me when I copy` | Shorter, active, first person. |
| Settings row | `Duration (ms)` | `Time on screen` + suffix `sec` | Show `3.0`; keep ms in config only. |
| Settings row | `Max lines` | `Lines shown` | Notification body. |
| Settings row | `Width (px)` / `Height (px)` | `Width (px)` / `Height (px)` | Units are honest here. |
| Settings values | `Top left` … `Center` | `Top left`, `Top right`, `Bottom left`, `Bottom right`, `Center` | Sentence case. |
| Settings row | `Offset X` / `Offset Y` | `Horizontal offset` / `Vertical offset` | X/Y is engineer-speak. |
| Settings hint | *(none)* | `Negative values move it left or up.` | Only where the two offset fields are. |
| Settings row | `Theme` + `Appearance` | `Theme` | Values: `Dark`, `Light`, `Match Windows`. |
| Settings row | `Time format` + `Clock` | `Time format` | Values: `24-hour (14:30)`, `12-hour (2:30 PM)`. Examples remove ambiguity. |
| Settings toggle | `Start on login` + `Launch when Windows starts` | `STARTUP` section + `Open when I sign in` | Avoids the product name; one label. |
| Settings row | `Clear history` + `Clipboard history` + `Clear` | `Clear history` + hint `Removes clips from Clipboard and Images. Pinned clips stay.` + button `Clear history…` | Names scope before the click. |
| Settings confirm | `Delete every saved clip? This cannot be undone.` / `Clear` / `Keep` | `Delete all clips in Clipboard and Images? Pinned clips stay.` / `Delete clips` / `Keep clips` | Current text is false (pinned/tabs survive). |
| Settings footer | `Ctrl+S to save   ·   Esc to close` + `Close` / `Save` | No sentence. Buttons: `Cancel` / `Save changes`. Tooltips: `Cancel (Esc)`, `Save changes (Ctrl+S)`. | "Close" hides that edits are discarded. |
| Dirty close (if added) | *(none)* | `Discard changes?` / `Discard` / `Keep editing` | Only when fields differ from saved config. |
| Tab errors (planned) | — | `Enter a name.` / `That name is taken. Choose another.` / `Keep the name to 24 characters.` | Plain, actionable. |
| Tab delete (planned) | — | `Delete “{tab}”? Its {n} clips will be deleted. Pinned clips are not affected.` / `Delete tab` / `Cancel` | Names the count and the survivor. |

---

## 7. Empty-state specifications

Shared rules: the tab strip and search field stay visible (the user can leave the state); the
status bar is hidden; no buttons (there is nothing to click except keys); no bordered hint cards
(Ortu's three bordered rows are more furniture than a 520 px popup needs — one plain keycap row
is enough). Content block max width 300, centred horizontally. Vertical: 40 px below the header.

### 7.1 First run, Clipboard tab, no history

```
            ┌────┐
            │ ▤  │        40×40, radius 16, fill `Raised`,
            └────┘        border 1px `Hairline`, icon 18px stroke 1.5 `Muted`
          No clips yet                 14px Medium Fg
  Copy text or an image anywhere,      12px Muted, line-height 18, max 2 lines
  and it appears here.
        [{Hotkey}]  Open this list from any app    24px below; keycap + 11px Muted
```

The hint teaches the one thing the user cannot guess: how to summon the list from another app.
`{Hotkey}` is the configured binding, not a hardcoded `Alt+V`.

### 7.2 No search matches

No icon tile (less furniture; the query is the subject).

```
        No clips match “zzzz”         14px Medium Fg
     Try fewer words, or search       12px Muted
     the app name.
```

Do not say "No results": it names neither the object nor the search. `{query}` is truncated at
40 characters with `…`. Enter still does nothing; Esc clears the search (existing contract).

### 7.3 Images tab, empty

Same layout as 7.1. Tile uses a drawn picture icon.

```
             No images yet            14px Medium Fg
  Copy a screenshot or picture, and  12px Muted
  it appears here.
      [Win+Shift+S]  Take a screenshot
```

The hint is Windows' own screenshot shortcut and is genuinely useful here. On Windows 10 it still
opens the Snipping Tool; the string remains true.

### 7.4 Pinned tab, empty

```
            Nothing pinned           14px Medium Fg
  Press Ctrl+P on a clip to keep     12px Muted, line-height 18
  it here.
  Pinned clips stay when you clear   11px Muted, 6px below; a statement, not a keycap
  history.
```

### 7.5 Custom tab, empty

```
         Nothing in “Research”       14px Medium Fg
  Press Ctrl+M to move a clip into   12px Muted
  this tab.
```

`{tab}` is the user's own name, quoted so the sentence still parses. No hint row.

### 7.6 Behaviour notes

- Empty state occupies the list area only; it must not replace the search header or the tab strip.
- Switching tabs while a query is active keeps the query. Use the tab's noun in the empty copy:
  `No images match “{query}”` on Images, `No clips match “{query}”` on Clipboard, Pinned and custom.
- The first-run state must be testable with the app closed: move `%APPDATA%\{Product}\clip.db`
  aside, relaunch, and screenshot. Do not test it by editing XAML.

---

## 8. The icon decision

**Decision: draw every UI icon as a WPF `Path` in one shared geometry dictionary. Do not use
Unicode characters. Do not use `Segoe MDL2 Assets`. Use `Segoe Fluent Icons` only if a future
control must match Windows chrome exactly, at 16 px, never inline with text.**

Evidence, in order:

1. **The floor bans the current approach.** "Unicode glyphs or emoji standing in for an icon
   system. Icons are drawn, from a real library or authored SVG, in one consistent stroke and
   weight." [S1] Our `✕`, `✓`, `▾` are exactly that.
2. **Our search glyph breaks three parts of Microsoft's own guidance.** It is from
   `Segoe MDL2 Assets`, which Microsoft says has been replaced by `Segoe Fluent Icons` as the
   recommended symbol font and is "still available, but we recommend updating"; glyphs "are not
   intended for use in-line with text"; and the recommended sizes are 16/20/24/32/40/48/64, not
   12. [S11] A system-icon font used correctly would be defensible; this is the superseded font,
   inline, at a non-optimal size.
3. **Ortu draws.** Its search icon is inline SVG 14×14, `stroke-width 2.5`, colour `fg/0.20`; its
   clear X is 13×13; its type tiles are 11×11 at stroke 2; its empty-state icon is 18×18 at stroke
   1.5. One geometry family, scaled by role. [S16]
4. **Consistency is visible at a glance.** A `✓` from Manrope and a `▾` from whatever font
   supplies it have different stroke weights, optical sizes and baselines; at 10–11 px this is
   the first thing a designer sees. Mixed icon sources are a named tell. [S7]
5. **The app already has a bespoke typeface.** Manrope earns the right to a bespoke icon set;
   falling back to system characters throws that away.

Implementation rules:

- One `ResourceDictionary` of `Geometry` (24×24 viewBox), all stroke-only, one stroke weight in
  viewBox units (`StrokeThickness 2`), round caps/joins. The `Viewbox` scales the stroke with the
  icon, so optical weight stays consistent from 11 to 18 px.
- Sizes: 14 search, 10 close visual (inside a 24×24 target), 16 command icons, 11–12 row
  type/action icons, 18 empty-state, 16/24/32 tray.
- `Fill` only where the metaphor requires a solid (e.g. a filled pin for pinned state), never two
  weights in one row.
- The tray icon is drawn from the same clipboard path, monochrome per taskbar theme, and exported
  at 16/24/32. Delete the letter "C".
- Keyboard legends in keycaps are text, not icons. That is the one place characters are correct.
- Add a one-line audit grep: no `Segoe MDL2`, no `&#xE`, and no `✕ ✓ ▾` literals in `UI\*.xaml`.

---

## 9. What we keep (and why it is not slop)

- **Manrope, embedded.** A chosen face, not the Inter default. Keep.
- **The orange `#FF8A3D`.** A chosen accent with a real job (selection/primary). Keep, but make it
  do only that job.
- **Dark as the primary theme.** A chosen default, not "dark by reflex": `System` is offered, and
  the light theme is a full peer that must pass the same checks.
- **The 14 px window radius and soft shadow.** A floating surface, offset + blur, exactly what the
  floor asks. Keep.
- **Hidden scrollbar at rest.** Matches Ortu and the compact brief. Keep; show the thumb while
  scrolling.
- **The growing selected row.** Ortu's signature; it replaces a detail pane. Keep; animate only
  `MaxHeight` (120 ms) with the documented key-repeat guard.
- **No gradients, glass, pills, status dots, sparklines or "New" badges.** Clip is already cleaner
  than the median. The audit should protect that rather than add "polish".

---

## 10. Open decisions for the owner

1. **Preview lines range.** The field accepts 1–100 (`SettingsWindow.xaml:273`,
   `SettingsWindow.xaml.cs:219-220`) while the row clamps to 3 lines when selected
   (`MainWindow.xaml:78`). Recommendation: 1–3, copy "Choose 1 to 3 lines." This is a behaviour
   change, so it needs sign-off.
2. **Dirty close in Settings.** Minimum is renaming `Close` to `Cancel`. Recommendation: add one
   inline footer question when fields differ ("Discard changes?"). Cost: one string set; benefit:
   no silent loss of six edited fields.
3. **Seconds vs milliseconds for notification duration.** Recommendation: display seconds with
   one decimal, keep `DurationMs` in config. Cost: one conversion in Settings code-behind.

No other product decision is hidden in this document; everything else is a direct consequence of
the floor, the pinned world, or measured contrast.

---

## Sources

- [S1] Craft floor, `~/.config/opencode/skills/impeccable/reference/craft-floor.md` (local, read
  23 Nov 2026).
- [S2] Wikipedia, "AI slop" (term, definitions, Kommers et al. Jan 2026 properties: superficial
  competence, asymmetric effort, mass producibility). <https://en.wikipedia.org/wiki/AI_slop>
- [S3] Arlan Marat, "AI slop UI", design.how (2026): tracked capitals, eyebrows, side rail, status
  dot, pill, icon tile, middle-dot separator, filler words; 79 lint rules across four systems.
  <https://design.how/ai-slop-ui>
- [S4] uiscanner, "what is 'AI slop' UI, and how to avoid it" (4 Jul 2026): indigo gradient, Inter,
  compressed type scale, three cards, 1 px border + soft shadow on every surface, dark by reflex.
  <https://uiscanner.com/blog/what-is-ai-slop-ui>
- [S5] Carlo D'Agnolo, "30 AI Design Slop Indicators" (4 Nov 2026): excessive italics, containers
  with borders, overusing dividers, tags and pills, colour shades and opacities, verbose copy; "the
  problem is the pile". <https://carlodagnolo.com/resources/posts/ai-design-tells/>
- [S6] 925Studios, "AI Slop Fonts and Gradients: The Tells That Give Away AI Design" (2026):
  median aesthetic, generic vs ugly, three-card reflex, weightless copy and interchangeable icons.
  <https://www.925studios.co/blog/ai-slop-design-tells>
- [S7] nyrthoughts, "15 recurring AI tells in UI design" (GitHub, 2026): card soup, emoji/glyph
  icons, random spacing, hollow copy, missing empty/loading/error states, mid-dot separators.
  <https://github.com/nyrthoughts/anti-slop-design/blob/main/docs/ai-tells.md>
- [S8] Microsoft Learn, "Design principles - Windows apps" (updated 2026): effortless, calm,
  colour emphasises significant items only when necessary.
  <https://learn.microsoft.com/en-us/windows/apps/design/design-principles>
- [S9] Microsoft Learn, "Layering and elevation in Windows" (updated 2026): contour and shadow
  communicate elevation; overuse creates noise.
  <https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/layering>
- [S10] Microsoft Learn, "Iconography in Windows" (updated 2026): system icons, monoline 1 epx,
  minimal/harmonious/evolved; Segoe Fluent Icons.
  <https://learn.microsoft.com/en-us/windows/apps/design/iconography/>
- [S11] Microsoft Learn, "Segoe Fluent Icons font" (updated Oct 2026): MDL2 replaced; glyphs not
  intended for inline text; optimal sizes 16/20/24/32/40/48/64.
  <https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font>
- [S12] Microsoft Learn, "Compact sizing for controls" (updated Jul 2026): compact density for
  productivity/data-entry apps.
  <https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/compact-sizing>
- [S13] Kate Kaplan, "Designing Empty States in Complex Applications: 3 Guidelines", NN/g (2021):
  status, learning cues, direct pathways. <https://www.nngroup.com/articles/empty-state-interface-design/>
- [S14] Jakob Nielsen, "Confirmation Dialogs Can Prevent User Errors — If Not Overused", NN/g
  (reviewed Aug 2026): specificity, consequence-labelled responses, no default Yes.
  <https://www.nngroup.com/articles/confirmation-dialog/>
- [S15] Tim Neusesser and Evan Sunwall, "Error-Message Guidelines", NN/g (2023): close to source,
  human language, name the problem and the recovery, do not blame.
  <https://www.nngroup.com/articles/error-message-guidelines/>
- [S16] Ortu source, read 23 Nov 2026: `src/routes/popup/+page.svelte` (SVG icons, flat rows, empty
  state, status bar), `src/routes/+page.svelte` (empty-state copy, cards in the main window),
  `src/app.css` (tokens). Plus `docs\RESEARCH-ortu-ui.md`.
  <https://github.com/abhijith-p-subash/ortu>

*Limits:* the slop taxonomy is practitioner literature (2025–26), not peer-reviewed design
research; it is used here for its convergent, checkable claims, not as proof of causation. Ortu has
no dark-mode screenshots, so its dark values are read from CSS, not pixels. Contrast ratios above
are computed from the hex values in the theme dictionaries with the WCAG relative-luminance
formula; the rendered result should still be spot-checked by the audit agent because WPF text
rendering and DPI can shift perception.
