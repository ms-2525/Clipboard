# Clip — layout & alignment spec

**Deliverable of the LAYOUT & ALIGNMENT research pass, 2026-11-23.**
Read-only research: no app was launched and no project file was edited. Every number below is
either read from Ortu's source (`[S]`, cited by file and class token), read from a Microsoft
source (`[S]`), or derived here with the arithmetic shown (`[D]`). Nothing is eyeballed.

Visual authority: [`RESEARCH-ortu-ui.md`](RESEARCH-ortu-ui.md). Where the owner froze a
different value (popup size, absolute clock format) the owner decision wins and is marked
`[OWNER]`. Typography sizes are quoted from `RESEARCH-ortu-ui.md` §7 because
`DESIGN-typography.md` did not exist when this file was written; if it lands later, its type
values win and only the spacing here is binding.

Scope: **geometry only** — margins, paddings, radii, fixed heights, gaps, and the x/y rails they
produce, for the three surfaces (`MainWindow`, `NotificationWindow`, `SettingsWindow`).
Colour is named only where a hairline/ring needs a value (`#212329`, `#3E2517`); per `AGENTS.md`
those hexes and the tint/rail brushes must live in `UI\Themes\Dark.xaml` + `Light.xaml` as named
brushes, never inline in a window's XAML.

---

## 0. The one-paragraph summary

Our popup currently has **no shared left edge**: window inset 16, search text at 46, row text
at 29, and a metadata line that is 11 px italic 7 px below the body. Ortu has the same *kind* of
system (three chrome insets, a 22 px type tile, a 12 px text gap) but every one of its numbers is
derived from its list inset, so the eye reads a column. This spec keeps Ortu's numbers, collapses
our geometry to **one content rail (18) and one text rail (50)** on the popup, one **chrome rail
(20)** in Settings, and one **rail+content pair (15)** in the toast. Everything else is a 4-pt
(or sanctioned 2-pt nudge) step off those rails.

---

## 1. What is verified, and one correction to the authority

| Claim | Source | Status |
|---|---|---|
| Popup 550 × 450, radius 14, 1 px `overlay/0.08` border | `src-tauri/src/lib.rs` `ensure_popup_window`; `src/routes/popup/+page.svelte` shell class | `[S]` |
| Popup header `min-height: 48`, `px-3.5` (=14), `gap-2.5` (=10), `border-b overlay/[0.06]` | `src/routes/popup/+page.svelte`, search-header div | `[S]` |
| Search input 14 px, `py-3` (=12), placeholder `fg/18` | same file, `<input>` class | `[S]` |
| Popup list `py-1.5 px-1.5` (=6 × 6) | same file, item-list div | `[S]` |
| Popup row `px-3 py-[8px]` (=12 × 8), `gap-2.5` (=10), `rounded-xl` (=12) | same file, clip-item div | `[S]` |
| Popup row type tile **22 × 22**, `rounded-lg` (=8) | same file, type-icon div | `[S]` |
| Selected row: `line-clamp-3`, `leading-snug`, description 10 px semibold + `mb-0.5` | same file, `{#if isSelected}` branch | `[S]` |
| Status bar `px-4 py-2` (=16 × 8), `border-t overlay/[0.05]`, text 9 px | same file, status-bar div | `[S]` |
| Scrollbar **3 px**, thumb `overlay/0.25`, transparent track | `src/app.css` `.custom-scrollbar` | `[S]` |
| Main-window card inner padding **16 / 12 / 12 / 10** (`pl-4 pr-3 pt-3 pb-2.5`) | `src/routes/+page.svelte`, card content div | `[S]` |
| Card meta: `mt-2 pt-2` (=8 / 8) above a 1 px `overlay/[0.07]` top rule | same file, `cardMeta` snippet | `[S]` |
| Card list `px-3 pb-3` (=12), cards `space-y-1.5` (=6), section gap `mb-4` (=16) | same file, item-list div + section blocks | `[S]` |
| Section header 9 px, `mt-1 mb-2 px-1` (=4 / 8 / 4), hairline after | `src/app.css` `.section-header` | `[S]` |
| Settings header `h-12` (=48) `px-3`, `border-b overlay/[0.09]` | `src/routes/settings/+page.svelte` `<header>` | `[S]` |
| Settings body `p-5` (=20), sections `space-y-6` (=24), label→card `space-y-3` (=12) | same file, body column + sections | `[S]` |
| Settings card `p-3.5` (=14), `rounded-xl` (=12), `border overlay/[0.08]` | same file, section cards | `[S]` |
| Settings row title 13 / description 11 `mt-0.5`, control right-aligned | same file, row markup | `[S]` |
| Switch 38 × 22, segmented wrapper `p-0.5` (=2) radius 12, pill `px-3 py-1.5` (=12/6) | same file | `[S]` |
| Toast `pl-3.5 pr-3 py-2.5` (=14 / 12 / 10), `gap-2.5`, 3 px rail, 12.5 px text, max-w 380 | `src/lib/Toaster.svelte` | `[S]` |
| Fluent spacing ramp: 4 px base, with **2 / 6 / 10** allowed "to align icons to the four pixel grid" | [Fluent 2 — Layout](https://fluent2.microsoft.design/layout) | `[S]` |
| Windows spacing rationale: 8 between buttons, 12 between control and label, **16 between surface and edge text** | [Content layout and spacing](https://learn.microsoft.com/en-us/windows/apps/design/basics/content-basics) | `[S]` |
| Windows 11 corner radii: 8 px top-level/flyout, 4 px in-page control | [Geometry in Windows 11](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/geometry) | `[S]` |
| WinUI `ListViewItem`: `MinHeight` **40**, `Padding` **12,0,12,0** | [`ListViewItem_themeresources.xaml`](https://raw.githubusercontent.com/microsoft/microsoft-ui-xaml/7a33ad772d77d908aa6b316ec24e6d2eb3ebf571/dev/CommonStyles/ListViewItem_themeresources.xaml) | `[S]` |
| WinUI single-line item template: **Height 44, Padding 12**, icon→text **12** | [Item templates for list view](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/item-templates-listview) | `[S]` |
| Overlay scrollbars "are placed over the content, not in a gutter"; classic scrollbars define one | [MDN `scrollbar-gutter`](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/scrollbar-gutter) | `[S]` |
| Raycast has **no** published spacing/duration values | `RESEARCH-motion-ui.md` §1 (closed source) | `[U]` |

**Correction to the authority.** `RESEARCH-ortu-ui.md` §5 says the settings column is
"`max-width 512 px` (2xl)". In the source it is `max-w-2xl`, and Tailwind's `2xl` is **672 px**,
not 512. This does not change our 440 px settings window, but do not cite 512 as an Ortu number.
Everything else in §3–§5 of the authority matches the source exactly (verified class by class).

---

## 2. Baseline: what our XAML measures today, and why it reads misaligned

Measured from `UI/MainWindow.xaml`, `UI/SettingsWindow.xaml`, `UI/NotificationWindow.xaml`.

| Surface | Current geometry |
|---|---|
| Popup | 560 × 620; root `Grid Margin="16"`; search `Border` `Raised`/radius 8/`Padding="10,9"`/`Margin="0,0,0,12"`; icon 12 + `Margin 0,0,8,0`; input 13; list no padding; card radius 10, 1 px border, `Padding="12,10"`, `Margin="0,0,0,6"`; body 13/`LineHeight 19`/`MaxHeight 76`; meta 11 **italic** `Margin="0,7,0,0"`; no footer, no tabs, no sections |
| Settings | 440 wide; root `Grid Margin="16"`; title 15 Bold `Margin="2,0,2,8"`; `H` 13 SemiBold `Margin="2,0,0,3"`; `Row` `Raised` radius 8 `Padding="9,5"` `Margin="0,0,0,4"`; `Hint` 11 `Margin="2,2,0,8"`; footer `Margin="0,6,0,0"`; fields 150 / 90 / 140 wide |
| Notification | radius 12; `Padding="14,10"`; app 12 Accent Semibold; time 11 italic `Margin="10,1,0,0"`; ✕ `Padding="5,1"` `Margin="0,-6,-8,0"`; body 13; thumb `Margin="0,2,0,0"` |

### The six concrete failures

1. **Three unrelated left edges on the popup.** Window inset 16, search text at
   16 + 10 + 12 + 8 = **46**, row text at 16 + 1 + 12 = **29**. Nothing shares a rail, so the
   search box and the item text read as two columns that are 17 px apart for no reason.
2. **Mixed padding steps with no parent.** 16 (window) / 12,10 (card) / 10,9 (search) / 6 (card
   gap) are four different rhythms. The gap between two body texts is 10 + 6 + 10 = 26; the gap
   from the search box to the first card is 12 + 6 = 18; the top of the first card is 10. The eye
   sees the irregular 18/26/10 sequence before it sees any content.
3. **Metadata is styled as a second body line.** 11 px italic on 13 px body, 7 px down, no rule.
   Because `LineHeight 19` on a 13 px font leaves ~2 px of trailing leading, the *visible* gap is
   ~9 px for a 1-line item and ~9 px for a 4-line item — but the meta's own optical weight changes
   with the amount of text above it, which is why it "floats".
4. **A card per row.** Border + radius 10 + a 6 px bottom margin is the main-window card language,
   not the popup's. It makes every row a separate object, so no vertical column exists to align to.
5. **No bottom anchor.** No status bar and no hairline above the last row means the list simply
   stops 16 px above the window edge; the search pill (radius 8, raised, 12 px margin) is the only
   enclosed object, so it reads as the top of a different list.
6. **Settings has no rails at all.** Heading at 18 (`16 + 2`), row text at 26 (`16 + 1 + 9`), hint
   at 18, control right edges at 414 / 404 / … depending on row width; vertical gaps of 3, 4, 5, 6,
   8 alternate. The notification adds a negative-margin ✕ (`0,-6,-8,0`) that sits outside the
   content box, so its right edge is not on any rail and it can clip.

---

## 3. The grid

Fluent's ramp is a **4 px base with sanctioned 2 / 6 / 10 nudges for icon-bearing insets**
`[S]`. Ortu is effectively a 2 px grid on a 4/6/8/10/12/14/16 ladder (`RESEARCH-ortu-ui.md` §6).
This spec uses one rule for both:

| Layer | Allowed values | Where |
|---|---|---|
| Structure | 0, 4, 8, 12, 16, 20, 24, 32, 48 | margins between groups, band heights, card gaps |
| Icon nudges | 2, 6, 10, 14, 18 | insets that contain or precede an icon/tile (Fluent's documented exception) `[S]` |
| Fixed heights | multiples of 4: **28, 32, 48, 56** | bands only. A *row* height is never fixed — it is content-driven, because selection grows it (§5) |
| Radii | window **14**, card/row/toast **12**, tile/button/nav **8**, keycap/menu **6**, pill **full** | `RESEARCH-ortu-ui.md` §6 `[S]` |

Anything not in those sets (9, 11, 13, 15, 17, 19, 22 as a *gap*, 35 as a rail) is a bug in the
XAML, with two documented exceptions: the **22 px tile** (Ortu source) and the **35 px settings
text rail** (20 body inset + 1 px stroke + 14 card padding, all sourced).

---

## 4. Popup surface (`MainWindow.xaml`)

Window: **550 × 520**, radius 14, 1 px `Hairline`, `AllowsTransparency`, shadow as today.
Width 550 is Ortu `[S]`; height 520 is `[OWNER]` (`CHECKLIST-uiux.md` §1). The root content
`Grid` has **no margin** — the header, list and status bar are bands, exactly like Ortu's popup.

### 4.1 Element table (exact WPF values)

| Element | Margin | Padding | Height / size | Radius | Other |
|---|---|---|---|---|---|
| Window root `Border` | — | **0** | 550 × 520 | **14** | 1 px `Hairline` |
| Content `Grid` | **0** | — | — | — | no `Margin="16"` |
| Header band | 0 | **18,0** | `MinHeight 48` | 0 | bottom 1 px `#212329` |
| Header icon slot | 0,0,10,0 | — | **22 × 22** | 8 | glyph 14, `#424449`, centred |
| Search `TextBox` | 0 | **0,12** | `MinHeight 44` | 0 | 14 px / **LineHeight 20**, no border/background |
| Clear ✕ hit target | 0 | 0 | **24 × 24** | 8 | right edge at **532**, glyph 13 |
| Tab strip | 0 | **18,8,18,8** | inner **28** | 0 | bottom 1 px `#212329` (total 45) |
| Tab label | — | — | 28 line box | — | 12 px; x = **18**; underline **2 × label width**, bottom of strip |
| List | 0 | **6** | fills | 0 | `VerticalScrollBarVisibility="Hidden"` |
| Row (`Border`) | **0** (no gap) | **12,8** | content-driven: **65** compact / **101** grown / **119** with description | **12** | `BorderThickness 0`; ring drawn as overlay |
| Row tile | 0,0,10,0 | — | **22 × 22** | 8 | `VAlign Top`; bg `#1F2126`, glyph 11 `#4E5054` |
| Body text | 0 | 0 | `MaxHeight 18` → **54** selected | — | 13 px / **LineHeight 18**, `TextTrimming=CharacterEllipsis` |
| Meta block | **0,8,0,0** | **0,8,0,0** | 1 px top rule `#212329` | — | rule spans x 18 → 532 |
| Meta text | 0 | 0 | 14 line box | — | 10 px / 14, `#5A5B5F`, **not italic** |
| Badge ("+90 more lines") | 0 | **6,0** | **16** | **8** | right edge **532**, centred on meta line |
| Selection ring | — | — | fills row | 12 | overlay `Border`, 1 px `#3E2517`, `IsHitTestVisible=False` |
| Selection rail | 0,8,0,0 | — | **2 × 24** | 1 | x = 6 (row's left edge), orange gradient |
| Section header | **16,0,0,8** (top gap for non-first) | 0 | 14 | — | 9 px semibold, uppercase, `CharacterSpacing 100`, x = **18**, hairline to 532 |
| Status bar | 0 | **18,0** | **32** | 0 | top 1 px `#212329`; left text 9 px `#424449`; hints 9 px `#66676B` |
| Keycap (status) | 0,0,6,0 | **4,0** | **16** | 6 | mono 9 semibold, bg `#26282D`, border `#3B3D40` |

Notes that follow from the table:

- The **row has no `BorderThickness`**. `Padding="12,8"` with a 0 thickness puts the tile at
  x = 6 + 12 = **18**. Adding a 1 px border for selection would move every glyph by 1 px on
  selection — the classic "text jumps when selected" bug. Draw the ring as a separate overlay.
- The **tile slot is a layout fixture**, not decoration: 6 (list) + 12 (row) + 22 (slot) + 10 (gap)
  = **50**. It exists so the text column has a single derivation. The glyph inside may be
  `Collapsed` for text items; the 22 px slot must not be.
- **No `Margin` between rows.** Ortu's popup rows have none; the 8 px vertical padding is the
  gap. (Ortu's *main window* cards use 6 px; do not import that into the popup.)

### 4.2 Vertical map (y, DIP, from the window's inner top edge)

| y | Element | Height |
|---|---|---|
| 0 | window top edge | — |
| 0 – 48 | header band (incl. 1 px bottom rule) | 48 |
| 48 – 56 | tab strip top gap | 8 |
| 56 – 84 | tab strip content (label + underline) | 28 |
| 84 – 92 | tab strip bottom gap | 8 |
| 92 – 93 | tab strip bottom rule | 1 |
| 93 – 99 | list top inset | 6 |
| 99 – 164 | row 1 (compact) | 65 |
| 164 – 229 | row 2 | 65 |
| 229 – 294 | row 3 | 65 |
| 294 – 359 | row 4 | 65 |
| 359 – 424 | row 5 | 65 |
| 424 – 482 | row 6 (visible 58 of 65, clipped by the viewport) | 58 |
| 482 – 488 | list bottom inset | 6 |
| 488 – 520 | status bar (its 1 px top rule lands at 488) | 32 |

Six compact rows fit a 520 px popup (the 6th partially). A selected row costs +36; with the
optional description line, +54. This is the density cost of always showing the metadata line,
and it is the number the owner should see before approving 520 rather than 450.

### 4.3 Left-edge column map

Schematic, **each character ≈ 5 px**; the guide ticks are exact columns. The box is not to scale
vertically.

```
0   18    50                                                                                             532
 6      40                                                                                                  550
||  |   | |                                                                                               |   |
++--+---+-+-----------------------------------------------------------------------------------------------+---+
|    o    Search history, groups...                                                                     [x]   |
++--+---+-+-----------------------------------------------------------------------------------------------+---+
|   Clipboard    Images    Pinned . 4                                                                         |
++--+---+-+-----------------------------------------------------------------------------------------------+---+
|#  [22]  body text, one line (grows to three)                                                                |
|   -------------------------------------------------------------------------------------------------------   |
|         App . 09:47                                                                      [+90 more lines]   |
|                                                                                                             |
++--+---+-+-----------------------------------------------------------------------------------------------+---+
|   33 clips                                                                    [Enter] paste    [esc] hide   |
+-------------------------------------------------------------------------------------------------------------+
```

| x | Rail | Who starts here | Derivation |
|---|---|---|---|
| **0** | window edge | nothing (bands are full-bleed) | 0 |
| **6** | surface bleed | row tint, selection ring, section rule start | Ortu list `px-1.5` `[S]` |
| **18** | **content rail** | header icon slot, tab labels, row tile, section labels, meta rule, status text | 6 + 12 (Ortu row `px-3`) `[S]` |
| **40** | fixture right | tile right edge | 18 + 22 `[S]` |
| **50** | **text rail** | search text, body text, meta text, badge's left limit | 40 + 10 (Ortu `gap-2.5`) `[S]`/`[D]` |
| **532** | content right | clear ✕ right edge, badge right edge, trailing action, hints right edge, meta rule end | 550 − 18 `[D]` |
| **550** | window right | — | — |

One deliberate deviation from Ortu: Ortu's header is `px-3.5` (14) while its text lands at 50, so
its search icon (14) and its row tile (18) are 4 px apart. We set the header padding to **18** so
the search slot and the tile slot are the *same column*. This is the single change that makes the
popup read as one grid `[D]`.

---

## 5. Row anatomy and the selected-row-grows pattern

### 5.1 Two states, one skeleton

| | Compact (unselected) | Grown (selected) |
|---|---|---|
| Body lines | 1 (`MaxHeight 18`) | 3 (`MaxHeight 54`) |
| `TextTrimming` | `CharacterEllipsis` | `None` |
| Optional description line | hidden | 10 px / 16 line, **above** the body, `Margin 0,0,0,2` |
| Row height | **65** | **101** (119 with description) |
| Padding | 12,8 | 12,8 **(unchanged)** |
| Tile | 22 × 22 | 22 × 22 **(unchanged)** |
| Gap tile→text | 10 | 10 **(unchanged)** |
| Meta block | 8 / 1 px / 8 | 8 / 1 px / 8 **(unchanged)** |
| Radius / ring | 12 / none | 12 / 1 px `#3E2517` |
| Rail | 2 × 24 (orange when selected) | 2 × 24 |

Arithmetic, compact: `8 (pad) + 18 (line) + 8 (gap) + 1 (rule) + 8 (pad) + 14 (meta) + 8 (pad) = 65`.
Grown: `8 + 54 + 8 + 1 + 8 + 14 + 8 = 101`. Description (16 line + 2 margin) sits above the body:
`8 + 16 + 2 + 54 + 8 + 1 + 8 + 14 + 8 = 119`. All three numbers are content-driven; the point is
that the *same* padding applies in both states.

### 5.2 How growth avoids a visual shift

Ortu's answer, read from source: **nothing about the row's horizontal skeleton changes** — same
`px-3 py-[8px]`, same `gap-2.5`, same 22 px tile, same radius. Only the text clamp changes
(`truncate` → `line-clamp-3`) and the description line is inserted. There is **no spacer** and no
margin animation. Neighbouring rows below move down because the list is normal flow; the rows
above never move, and no text ever moves sideways `[S]`.

Raycast is closed source and publishes no values `[U]`; the "growth in place" convention is
attributed to Ortu, not Raycast (`RESEARCH-motion-ui.md` §1).

Binding rules for Clip:

1. Selection may change **exactly**: `MaxHeight` 18 ↔ 54, `TextTrimming`, the ring/rail/tint
   opacity, and the description's `Visibility`. Nothing else.
2. Never animate or change `Padding`, `Margin`, `BorderThickness`, `CornerRadius`, tile size or
   the tile's `Margin` on selection.
3. Growth is **downward only**. The row's top edge is immutable; a grown row pushes rows below it
   by 36 px. That movement is expected — do not compensate with margins.
4. After the change, `ScrollIntoView(item)` with `ScrollMode` nearest, **not animated**
   (`RESEARCH-motion-ui.md` §5.10).
5. Growth tween: 120 ms `KeySpline(0,0,0,1)`, snap on `IsRepeat` (`RESEARCH-motion-ui.md` row 12).
   This is the only layout animation allowed in the app (R1 exception).
6. The tile is `VerticalAlignment="Top"`, so it marks the first body line. Ortu centres it because
   its grown rows are still shallow; with a metadata block, centring would park the tile between
   body and meta `[D]`.

---

## 6. Metadata line and the badge

**Meta block.** Rule above, text below — the main-window card pattern, reused:

```
body text (1–3 lines, 18 each)
   ↓ 8
────────────  1 px #212329, x 18 → 532
   ↓ 8
App · 09:47                        [+90 more lines]   10 px / 14, #5A5B5F
   ↓ 8   (row padding)
```

- 10 px, regular, **never italic**, `#5A5B5F`. Absolute time in the configured format `[OWNER]`.
- Single line, `TextTrimming="CharacterEllipsis"`; the window title does **not** go here
  (`RESEARCH-ortu-ui.md` #17).
- The rule's left end is the content rail (18), not the window edge.

**Badge (`+90 more lines`).** There is no source for this element; placement is derived from
Ortu's meta-row trailing copy button, which is the last child of a `justify-between` row and
therefore ends at the card's inner right edge `[S]` + `[D]`:

| Property | Value |
|---|---|
| Anchor | bottom-right of the row: right edge = **532**, vertically **centred on the meta line** |
| Size | Height **16**, `Padding 6,0`, `CornerRadius 8` (pill if it carries no icon) |
| Text | 9 px SemiBold, `#848588`, `CharacterSpacing 25` |
| Background | `#1F2126` (overlay/0.05); no border |
| Collision | occupies the trailing slot; the meta text keeps `MaxWidth = 532 − 50 − badgeWidth − 12` |
| Clearance | the scrollbar thumb lives at 541–544, so the badge never touches it |

`+90` means "90 more lines below the clamp". It is a count of the *hidden* lines, not the total,
and it is only shown when the clamp actually hides something.

---

## 7. Scrollbar behaviour

| Rule | Value |
|---|---|
| Gutter | **never reserved.** `ScrollViewer.VerticalScrollBarVisibility="Hidden"` (current value stays) |
| Why | Ortu uses a 3 px overlay thumb on a transparent track `[S]`; overlay scrollbars do not consume a gutter by definition `[S]`. A reserved 12 px classic gutter would move the content-right rail from 532 to 520 and break the column map |
| Optional thumb | 3 × (viewport-proportional) `#404249`, `CornerRadius 2`, at x **541–544** (inside the 6 px list inset), visible only while scrolling |
| Content shift | **zero** in both states; `Hidden` does not reserve space and the thumb is an overlay |
| Horizontal | `Disabled` (current) — no horizontal scrolling anywhere |

If a future sprint enables a classic Windows scrollbar, the whole popup must be re-derived (content
right = 550 − 18 − 12), not nudged. Write that in the ticket, not in the code.

---

## 8. Tabs and time sections

Tabs are **not in the current XAML**; the geometry is fixed here so the implementation cannot
invent it. Source for the strip: `RESEARCH-tabs-pinning.md` §5.1 `[S]` (owner-frozen feature).

| Element | Value |
|---|---|
| Strip | inner height **28**, `Padding 18,8,18,8`, bottom 1 px `#212329`; total block 45 |
| Label | 12 px; selected `Fg` 0.92, unselected 0.45, hover 0.62; **no pill, no border, no background** |
| Gap between tabs | **16** |
| Count | 10 px, `#5A5B5F`, tabular figures, **6** after the label: `Pinned · 4` |
| Indicator | 2 px accent underline, width = label width, bottom of the strip; amber for Pinned |
| `+` affordance | 16 px glyph, 16 after the last tab, vertically centred |
| Order / overflow | per the tabs doc; labels truncate at 14 chars, strip scrolls horizontally, hidden scrollbar |

Sections (time groups):

| Element | Value |
|---|---|
| Header | 9 px SemiBold uppercase, `CharacterSpacing 100`; `Margin 16,0,0,8` (first section: `0,0,0,8`); x = 18 |
| Rule | flex-fill 1 px `#212329` after the label, 8 px gap, ends at 532 |
| Between sections | last row of section N → next header = **16**; header → first row = **8** |
| Colour per section | Today `#959698`, Yesterday `#848588`, This Week `#66676B`, Earlier `#4E5054`, Pinned amber (per authority §4) |

A section header is **not** a row: it has no tint, no radius, and never participates in selection
or growth.

---

## 9. Notification surface (`NotificationWindow.xaml`)

Window width stays config-driven (`Config.Notification.Width`); this spec fixes the internals.
Ortu's toast is the reference `[S]`.

| Element | Margin | Padding | Height / size | Radius | Notes |
|---|---|---|---|---|---|
| Window root `Border` | — | **0** | auto | **12** | 1 px `#2F3136`, bg `Raised` |
| Left rail column | — | — | **3 × full** | 12,0,0,12 | accent info / `#AEB291` success / `#F87171` error |
| Content column | — | **12,10,10,10** | — | — | text x = 3 + 12 = **15** |
| Header row | 0 | — | 16 | — | app 11 SemiBold sage; time 10 `#5A5B5F` non-italic; **gap 8** |
| Body | **0,4,0,0** | — | lines × **17** | — | 12.5 px / LineHeight 17 |
| Thumbnail | **0,8,0,0** | — | `MaxHeight` from config | 8 | `Stretch=Uniform` |
| Close ✕ | **0,2,4,0** | 0 | **24 × 24** hit target | 8 | no negative margins; glyph 13 |
| Body right limit | — | — | ends at width − 10 | — | keeps ≥ 8 px clear of ✕ |

Arithmetic for 3 body lines: `10 + 16 + 4 + 51 + 10 = 91`. The ✕'s 24 px target is larger than
its 13 px glyph, so it is reachable without moving the text rail.

---

## 10. Settings surface (`SettingsWindow.xaml`)

Keep the 440 px window (`SizeToContent=Height`, current `MaxHeight` logic unchanged). Ortu's
672 px column is for a full-window settings page; a 440 px dialog simply has a 400 px content
column `[D]`.

### 10.1 Element table

| Element | Margin | Padding | Height | Radius | Notes |
|---|---|---|---|---|---|
| Window root `Border` | — | **0** | 440 × auto | 14 | **`App`** background (not `Surface`), 1 px `Hairline` |
| Content `Grid` | **0** | — | — | — | replaces `Margin="16"` |
| Header band | 0 | **20,0** | **48** | 0 | title 13 SemiBold; bottom 1 px `#212329` |
| Body `ScrollViewer` | 0 | **20,0,20,0** | fills | — | hidden scrollbar (overlay rule §7) |
| Section label | **0,0,0,12** | 0 | 14 | — | 9 px SemiBold uppercase, `CharacterSpacing 100`, `#5A5B5F`; x = **20** |
| Section gap | previous card → next label **24** | — | — | — | `space-y-6` `[S]` |
| Card | **0,0,0,12** (last card 0) | **14** | auto | **12** | `Surface`, 1 px `Hairline` |
| Row grid | 0 | 0 | `MinHeight 44` | — | title 13 `#B8B9BB`; description 11 `#66676B` `Margin 0,2,0,0` |
| Single-line row height | — | — | **46** (14 + 18 + 14) | — | two-line row **66** |
| Field | 0 | **0,0,0,0** (text inside 6) | **28** | 8 | 13 px, right-aligned text; width 90 / 140 / 150 |
| Combo / segmented | — | — | **28** | 12 wrapper / 8 pill | wrapper `p 2`, pill 24 high `px 12`, 12 px text |
| Switch | 0 | — | **38 × 22** | full | knob 18, travel 16 |
| Footer | 0 | **20,12** | **56** | 0 | top 1 px `#212329`; hints 11 at x 20; buttons height **32**, `Padding 12,0`, radius 8, gap 8 |

### 10.2 Rails (x, from the window's inner left edge, width 440)

| x | Who | Derivation |
|---|---|---|
| **0** | window edge | — |
| **20** | **chrome + content rail**: header title, section label, card left edge, footer hints, button group's left edge | Ortu `p-5` `[S]` = Microsoft's "16 between surface and edge text" rounded up to the Ortu value `[D]` |
| **21** | card border | 20 + 1 |
| **35** | row title / description | 20 + 1 + 14 (Ortu card padding) `[S]` |
| **405** | **control right rail** | 440 − 20 − 1 − 14 `[D]` |
| **420** | content right edge: card right edge, footer button group's right edge | 440 − 20 |

Rules for controls:

1. Every control's right edge is **405**. Never centre a control, never left-align it beside its
   label.
2. Field widths come from {90, 140, 150} only. Right-aligned text inside fields is mandatory for
   numbers — it puts the digits on the rail, not the box edge.
3. **Paired rows** (Duration/Max lines, Width/Height, Offset X/Y) are the one place with two
   controls. Use a fixed grid, not two `Auto` columns:
   `*` labelA · `90` fieldA · `20` gap · `110` labelB · `90` fieldB.
   That puts fieldB on 405 and fieldA's right edge on **185** deterministically. Preferred fix
   when the form is next touched: **split every pair into two single-setting rows** (Ortu's
   one-setting-per-row model), which removes the second rail entirely.
4. Section labels and card edges share x 20; the title inside a card is 15 px further right. Do
   not add a third indent for hints (today `Hint` is at 18 and rows at 26).

---

## 11. Rules (the enforceable part)

**R1 — Single source of truth.** Each geometry literal appears in exactly **one** `Style` (or one
named element in the template). No instance may override `Padding`, `Margin`, `Height` or
`CornerRadius` of a styled part. Exact values:

| Style / element | Exact WPF value |
|---|---|
| `HeaderBand` (popup) | `Padding="18,0" MinHeight="48" BorderThickness="0,0,0,1"` |
| `SearchBox` | `Padding="0,12" FontSize="14" LineHeight="20" BorderThickness="0"` |
| `TabStrip` | `Padding="18,8" Height="45"` (inner 28) `BorderThickness="0,0,0,1"` |
| `RowCard` | `Padding="12,8" CornerRadius="12" BorderThickness="0" Margin="0"` |
| `RowTile` | `Width="22" Height="22" CornerRadius="8" Margin="0,0,10,0" VerticalAlignment="Top"` |
| `RowBody` | `FontSize="13" LineHeight="18" MaxHeight="18" TextTrimming="CharacterEllipsis"` |
| `MetaBlock` | `Margin="0,8,0,0" Padding="0,8,0,0" BorderThickness="0,1,0,0"` |
| `MetaText` | `FontSize="10" LineHeight="14" FontStyle="Normal"` |
| `Badge` | `Height="16" Padding="6,0" CornerRadius="8" FontSize="9" FontWeight="SemiBold"` |
| `StatusBar` | `Height="32" Padding="18,0" BorderThickness="0,1,0,0"` |
| `SectionHeader` | `Margin="16,0,0,8" FontSize="9" FontWeight="SemiBold" CharacterSpacing="100"` |
| `SettingsHeader` | `Height="48" Padding="20,0" BorderThickness="0,0,0,1"` |
| `SettingsBody` | `Padding="20,0"` |
| `SettingsCard` | `Padding="14" CornerRadius="12" Margin="0,0,0,12" BorderThickness="1"` |
| `SettingsRow` | `MinHeight="44"` (grid only) |
| `SettingsFooter` | `Height="56" Padding="20,12" BorderThickness="0,1,0,0"` |
| `ToastRoot` | `CornerRadius="12" Padding="0" BorderThickness="1"` |
| `ToastContent` | `Padding="12,10,10,10"` |

**R2 — One text column per surface.** Popup: 50. Settings: 35 (inside cards) and 20 (chrome and
section labels). Toast: 15. Metadata, tabs, section labels and status text all use their surface's
rail; nothing gets a bespoke indent.

**R3 — Bands vs rows.** Bands (header, tab strip, status bar, settings header/footer) are
full-bleed with a fixed height and a 1 px divider. Rows are never full-bleed (6 px bleed on the
popup), never fixed-height, and never carry a border except the selection ring.

**R4 — Growth** (§5.2): padding/tile/gap/radius frozen; only clamp + ring change; downward only;
nearest-scroll after the change; one 120 ms layout animation, snap on key repeat.

**R5 — Scrollbar** (§7): no gutter, no content shift, overlay thumb only.

**R6 — Hairlines.** 1 px `#212329`. Band dividers run 0 → width. Content rules (meta, section)
run content-rail → content-right. Never outline a row, never draw a rule between two rows.

**R7 — Hover/selection never move geometry.** No `translateY`, no shadow on popup rows (Ortu
reserves the lift for main-window cards). Selection is tint + ring + rail only.

**R8 — Right edges are rails too.** Popup 532, settings 405/420, toast width − 10. A control that
does not end on one of these is a bug.

---

## 12. Verdict table — current → spec

Legend: **Y** = change required, **N** = keep what we have.

### Popup

| # | Element | Current XAML | Spec | Change |
|---|---|---|---|---|
| 1 | Window size | `560 × 620` | `550 × 520` | **Y** |
| 2 | Content inset | `Grid Margin="16"` | `Margin="0"` (bands full-bleed) | **Y** |
| 3 | Search container | `Raised`, radius 8, 1 px, `Padding="10,9"`, `Margin="0,0,0,12"` | flat band, `MinHeight 48`, `Padding="18,0"`, bottom rule, no radius/background | **Y** |
| 4 | Search icon | 12 px glyph, `Margin="0,0,8,0"` | 14 px glyph in a 22 × 22 slot, `Margin="0,0,10,0"` | **Y** |
| 5 | Search text | 13 px | 14 px / LineHeight 20, `Padding="0,12"` | **Y** |
| 6 | Row radius | 10 | 12 | **Y** |
| 7 | Row padding | `12,10` | `12,8` | **Y** |
| 8 | Row margin | `0,0,0,6` | `0` | **Y** |
| 9 | Row border | 1 px `Hairline` always | none; 1 px `#3E2517` ring overlay when selected | **Y** |
| 10 | List inset | none | `Padding="6"` | **Y** |
| 11 | Row template | `StackPanel` (text only) | `Grid` 22 / * with tile slot | **Y** |
| 12 | Body text | 13 / `LineHeight 19` / `MaxHeight 76` | 13 / 18 / `MaxHeight 18` → 54 selected | **Y** |
| 13 | Body colour | `Fg` (white) | `#959698` compact, `#C4C5C6` selected | **Y** |
| 14 | Meta line | 11 px **italic**, `Margin="0,7,0,0"` | 10 px regular `#5A5B5F`, block `Margin="0,8,0,0"` `Padding="0,8,0,0"` + top rule | **Y** |
| 15 | Status bar | absent | 32 px band, `Padding="18,0"`, keycaps | **Y** |
| 16 | Tabs | absent | 45 px block (§8) | N (feature-gated) |
| 17 | Time sections | absent | §8, first section header `Margin="0,0,0,8"` | N (feature-gated) |
| 18 | Section rules | absent | 1 px from 18 → 532 | N (feature-gated) |
| 19 | Scrollbars | `Hidden` | `Hidden` + optional 3 px overlay | **N** |
| 20 | Window radius / shadow | 14 / 28-6-0.55 | same | **N** |
| 21 | Selection tint / rail | `Raised` + accent border | overlay tint + 2 × 24 rail + inset ring | **Y** (visual, not spacing) |

### Notification

| # | Element | Current | Spec | Change |
|---|---|---|---|---|
| 22 | Root padding | `14,10` | `0`; rail column 3 + content `12,10,10,10` | **Y** |
| 23 | Background | `Surface` | `Raised` + 1 px `#2F3136` | **Y** |
| 24 | App name | 12 Accent SemiBold | 11 Sage SemiBold | **Y** |
| 25 | Time | 11 **italic**, `Margin="10,1,0,0"` | 10 regular `#5A5B5F`, gap 8, same baseline | **Y** |
| 26 | Close ✕ | `Padding="5,1"`, `Margin="0,-6,-8,0"` | 24 × 24 target, `Margin="0,2,4,0"`, no negatives | **Y** |
| 27 | Body | 13 / default line height | 12.5 / LineHeight 17, `Margin="0,4,0,0"` | **Y** |
| 28 | Thumb | `Margin="0,2,0,0"` | `Margin="0,8,0,0"` | **Y** |
| 29 | Radius | 12 | 12 | **N** |

### Settings

| # | Element | Current | Spec | Change |
|---|---|---|---|---|
| 30 | Window width | 440 | 440 (content 400) | **N** |
| 31 | Root background | `Surface` | `App` | **Y** |
| 32 | Content inset | `Grid Margin="16"` | `0`; header band 48 `Padding="20,0"` | **Y** |
| 33 | Title | 15 Bold, `Margin="2,0,2,8"` | 13 SemiBold inside the 48 band, x = 20 | **Y** |
| 34 | Section heading | `H` 13 SemiBold, `Margin="2,0,0,3"` | 9 SemiBold uppercase, `CharacterSpacing 100`, `Margin="0,0,0,12"` | **Y** |
| 35 | Row card | `Raised`, radius 8, `Padding="9,5"`, `Margin="0,0,0,4"` | `Surface`, radius 12, `Padding="14"`, `Margin="0,0,0,12"` | **Y** |
| 36 | Row label | 12 | 13 `#B8B9BB` | **Y** |
| 37 | Hint / description | separate `Hint` row below the card, 11, `Margin="2,2,0,8"` | 11 `#66676B` inside the card, `Margin="0,2,0,0"` | **Y** |
| 38 | Field | 12, no fixed height | 13, Height 28, radius 8, right-aligned | **Y** |
| 39 | Combo | 12, width 140 | 12, width 140, **Height 28**, radius 8 | **Y** (height only) |
| 40 | Footer | `Margin="0,6,0,0"`, no divider, buttons `Padding="16,6"` | 56 band, top rule, `Padding="20,12"`, buttons Height 32 `Padding="12,0"` | **Y** |
| 41 | Control right edge | 414 / varies | **405** everywhere (420 for the button group) | **Y** |
| 42 | Paired numeric rows | 3 rows × 2 fields, `Auto` columns | fixed grid (§10.2) or split into 6 rows | **Y** (choose one) |
| 43 | Settings scrollbar | `Hidden` | `Hidden` | **N** |

---

## 13. Derived values, deviations, and what is not verified

1. **550 × 520** is an owner decision, not Ortu (Ortu is 550 × 450) `[OWNER]`.
2. **Content rail 18 / text rail 50** are derived: Ortu's 6 + 12 and 6 + 12 + 22 + 10 `[D]`. The
   header deviation (18 instead of Ortu's 14) is ours and should be visible in review.
3. **Status text at 18** deviates from Ortu's 16 so the footer shares the content rail. Ortu's own
   footer is 2 px off its list content; we chose alignment over fidelity `[D]`.
4. **Badge** has no source at all: size, copy and behaviour are derived from Ortu's trailing meta
   button plus the 16 px keycap height. If the owner wants a different affordance ("Show more"
   link), only this row changes.
5. **Settings 440 / content 400** is ours; Ortu's column is 672 `[S]` in a 1000 px window.
6. **Tab strip** geometry comes from `RESEARCH-tabs-pinning.md` §5.1, not from Ortu (Ortu's popup
   has no tabs). It is included so the feature cannot invent a different grid later.
7. **Raycast** is closed source; no Raycast spacing value is used anywhere in this document `[U]`.
8. Not verified by rendering. This pass read source and documents only, per its brief; the [U]
   audit agent must confirm the rails on screen at 100 % and 150 % DPI.
9. **22 px tile** and **35 px settings text rail** are the two non-ramp numbers. They are
   deliberate (sourced tile; sourced 14 px card padding + 1 px stroke) and must not be "rounded"
   to 20/36 without re-deriving the text rail.

---

## 14. Sources

Primary (read in this pass):

- Ortu — `src/routes/popup/+page.svelte`
  <https://github.com/abhijith-p-subash/ortu/blob/main/src/routes/popup/+page.svelte>
- Ortu — `src/routes/+page.svelte` (main window, cards, sections)
  <https://github.com/abhijith-p-subash/ortu/blob/main/src/routes/+page.svelte>
- Ortu — `src/routes/settings/+page.svelte`
  <https://github.com/abhijith-p-subash/ortu/blob/main/src/routes/settings/+page.svelte>
- Ortu — `src/app.css` (scrollbar, section header, keycap, card radius)
  <https://github.com/abhijith-p-subash/ortu/blob/main/src/app.css>
- Ortu — `src/lib/Toaster.svelte`
  <https://github.com/abhijith-p-subash/ortu/blob/main/src/lib/Toaster.svelte>
- Fluent 2 — Layout / global spacing ramp
  <https://fluent2.microsoft.design/layout>
- Microsoft Learn — Content layout and spacing (8 / 12 / 16 Windows values)
  <https://learn.microsoft.com/en-us/windows/apps/design/basics/content-basics>
- Microsoft Learn — Geometry in Windows 11 (8 px overlay, 4 px control radii)
  <https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/geometry>
- Microsoft Learn — Item templates for list view (44 height / 12 padding / 12 icon gap)
  <https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/item-templates-listview>
- WinUI source — `ListViewItem_themeresources.xaml` (MinHeight 40, Padding 12,0)
  <https://github.com/microsoft/microsoft-ui-xaml/blob/main/dev/CommonStyles/ListViewItem_themeresources.xaml>
- MDN — `scrollbar-gutter` (overlay scrollbars consume no gutter)
  <https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/scrollbar-gutter>

Internal:

- `docs/RESEARCH-ortu-ui.md` (visual authority; §6 spacing ladder, §7 typography)
- `docs/RESEARCH-motion-ui.md` (growth tween 120 ms, nearest-scroll rule, Raycast `[U]`)
- `docs/RESEARCH-tabs-pinning.md` §5.1 (tab strip geometry)
- `docs/CHECKLIST-uiux.md` §1 (550 × 520 owner decision; metadata line owner decision)
