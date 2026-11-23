# Ortu UI research — visual & interaction spec for Clip

Researched 2026-11-23. Target: make Clip look and behave like Ortu
(<https://github.com/abhijith-p-subash/ortu>) as far as is sensible for a WPF app.

## 1. Sources, and what is actually verified

Ortu is **Tauri v2 + SvelteKit 5 + Tailwind 3** (`package.json`, `src-tauri/tauri.conf.json`).
Its entire UI is HTML/CSS, so every number below is read from source, not eyeballed.
Nothing in this document is inferred from a screenshot when the source had the answer.

| Claim source | URL |
|---|---|
| Theme tokens, `.kbd`, `.section-header`, buttons, scrollbar | `src/app.css` — <https://github.com/abhijith-p-subash/ortu/blob/main/src/app.css> |
| Tailwind semantic colors | `tailwind.config.ts` |
| Quick popup (markup + every class) | `src/routes/popup/+page.svelte` |
| Main window | `src/routes/+page.svelte` |
| Settings | `src/routes/settings/+page.svelte` |
| Toast | `src/lib/Toaster.svelte` |
| Popup window size / chrome / centering | `src-tauri/src/lib.rs` (`ensure_popup_window`, `center_popup`), `src-tauri/tauri.conf.json` |
| Screenshots (light theme only) | `docs/assets/1.png` … `4.png` |
| Shortcut list & microcopy tone | `docs/SHORTCUTS.md`, `src/routes/popup/+page.svelte` |
| Marketing site | <https://ortu.abhijithpsubash.com/> |

Screenshot inventory (`docs/assets/`, all **light theme**, macOS):
`1.png` main window, `2.png` quick popup, `3.png` settings, `4.png` main window with Paste Stack popover.
**There is no dark-mode screenshot in the repo or on the site.** The dark tokens are exact
(`app.css`), but proportions/contrast in screenshots are light-theme while our app ships dark —
where they disagree, trust the CSS numbers, not the picture.

One notable deviation to be aware of: Ortu's font stack is **system-first**
(`-apple-system, "SF Pro Text", "Manrope", "Segoe UI Variable Text", …` — `src/app.css`);
Manrope is only a fallback. Our app *is* Manrope. Keep Manrope — it is a deliberate
choice, and the size/weight scale below was measured for Manrope's metrics too.

---

## 2. Window chrome

| | Quick popup | Main window |
|---|---|---|
| Size | **550 × 450**, `resizable(false)` | 1000 × 700 initial, resizable |
| Chrome | borderless, always-on-top, hidden on focus loss | native titlebar, `theme: Dark` |
| Radius | **14 px** (CSS `border-radius: 14px` on html/body/shell) | OS |
| Border | 1 px `overlay / 0.08` | — |
| Position | centered in the **work area** of the monitor under the cursor (not the primary) | centered |
| Background | `app / 0.97` + `backdrop-filter: blur(24px)` | `app` (solid) |

Source: `lib.rs` → `ensure_popup_window` (`.inner_size(550.0, 450.0)`), `center_popup`
(uses `monitor.work_area()` so the taskbar never crowds it), `popup/+page.svelte` shell div.

Windows-specific trick worth copying: the popup window is **opaque** on Windows and the
corners are rounded by DWM (`apply_popup_rounded_corners` → `DWMWA_WINDOW_CORNER_PREFERENCE`),
because CSS rounding over an opaque square window leaves white corner triangles. On Win10
DWM ignores it and the popup stays square. Our WPF window already does
`AllowsTransparency=True` + CSS-style rounding, which is the other valid answer — see §13.

Motion on show: **none.** `show_popup()` positions the window before `show()` so it never
flashes on the monitor it is leaving; there is no fade or scale-in. First frame is final frame.

---

## 3. Layout anatomy — quick popup (this is the window we are cloning)

```
┌──────────────────────────────────────────────┐  14px radius, app/0.97
│ [⌕]  Search history, groups…            [×]  │  header, min-height 48, px 14
├──────────────────────────────────────────────┤  1px overlay/0.06 divider
│ ▍ ⬜  ust.com  · https://www.ust.com/        │  row: px 12 / py 8, radius 12
│   ⬜  open.money · https://open.money/       │  compact = one line, 13px fg/55
│   ⬜  Company logo for Experion Technolog…   │
├──────────────────────────────────────────────┤  1px overlay/0.05
│ 33 clips             [↵] paste  [esc] hide   │  status bar, px 16 / py 8, 9px
└──────────────────────────────────────────────┘
```

Measurements (all from `src/routes/popup/+page.svelte`):

- **Header** — `min-height: 48px`, `padding: 0 14px`, `gap: 10px`,
  `border-bottom: 1px solid overlay/0.06`, `background: app/0.6`.
  - Search icon 14 × 14, `stroke-width 2.5`, colour `fg/0.20` (very faint).
  - Input 14 px, `fg/0.75`, vertically padded `py-3` (12 px). **Placeholder is `fg/0.18`** —
    almost invisible on purpose; the input is empty-looking, not grey-labelled.
  - Clear button (×) 13 px appears only when there is text, `fg/0.20 → fg/0.50` on hover.
  - When a group filter is active the icon is replaced by a **breadcrumb pill**:
    `h 24, px 10, radius full, bg sage/0.12, border sage/0.20, text 11px sage`, chevron 9 px,
    then a 1 × 16 `overlay/0.08` divider.
- **List** — `padding: 6px 6px` (y/x), scrollbar hidden unless scrolling (3 px overlay/0.25 thumb).
- **Row** — `padding: 8px 12px`, `gap: 10px`, `radius: 12px`, `cursor: default`,
  no border, no separator between rows.
  - **Type tile**: 22 × 22, `radius 8 (lg)`, `bg overlay/0.05`, icon 11 px `fg/0.25`.
  - Content area `min-width: 0`, single-line `truncate`.
  - **Right actions** (stack / pin / group / delete): 11 px icons in `p-1` buttons,
    `opacity 0` until row hover, `opacity 100` on hover. Pinned rows keep the pin visible
    (`amber-400/0.8`). This is the only hover-only chrome in the popup.
- **No card borders, no shadows, no timestamps in the popup.** Rows are flat; only the
  selected row has a surface.
- **Status bar** — `padding: 8px 16px`, `border-top: 1px overlay/0.05`, `bg app/0.4`.
  Left: `{n} clips` / `{n} clips · {m} groups` in **9 px, `fg/0.20`, tracking-wide**.
  Right: two hint pairs `[↵] paste` `[esc] hide`, 9 px `fg/0.35`, keycaps 8 px.

### Signature interaction: the selected row *grows*

Selection is not just a colour change. In `{#if isSelected}` the row switches from the
one-line compact layout to:

1. description (if any) on its own line — 10 px, semibold, `sage/0.70`;
2. content clamped to **3 lines** (`line-clamp-3`, 13 px `fg/0.75`, `leading-snug`,
   `whitespace-pre-wrap`, `break-words`) instead of `truncate` at 13 px `fg/0.55`;
3. image thumbnails grow from `max-h-10` to `max-h-20`, file thumbs from 36 → 44 px.

So the list visibly "breathes" as you arrow through it. This is the single most
recognisable Ortu behaviour and it costs one `DataTrigger` in WPF (see §12).

### Selected state, exactly

- Background `overlay / 0.08` (hover is `0.04`).
- Inset ring: `box-shadow: inset 0 0 0 1px rgba(255,138,61,0.15)` — no outer border.
- Left rail: **2 × 24 px**, `radius 0 999 999 0`, gradient `#FF8A3D → #ff6b1a`,
  vertically centred (`top:50%; translateY(-50%)`); unselected rows show a **2 × 16** rail in
  a type colour: pinned `amber-400/0.40`, URL `sky-400/0.30`, otherwise none.
- Right action icons stay hidden until hover — selection alone does not reveal them.

### While typing (group suggestions)

Above the results, matching groups render as rows: 22 × 22 tile `bg accent/0.10` with a folder
glyph in `accent/0.60`, name 13 px medium `fg/0.60`, right-aligned label `GROUP`
9 px semibold uppercase `tracking-[0.1em]` `fg/0.20`. Enter opens the group and clears the query.
Breadcrumb appears in the header, `Backspace` on empty query clears it, `Esc` unwinds
(group → filter → close).

### Hover preview (long content)

If content > **140 chars**, hovering a row floats a **300 × 220** panel near the cursor
(offset 14 px, 10 px margin, flipped left/above when it would overflow the 550 × 450 shell).
It is a plain panel; it disappears on `mouseleave`. Not required for v1 of our clone —
noted because it is the reason the popup never needs a detail pane.

---

## 4. Layout anatomy — main window (for our Settings/history parity)

- **Header** 44 px, `padding 0 16`, `border-bottom overlay/0.09`, contains:
  a **capture status pill** (`h 26, pl 8, pr 10, radius full`) — 7 px dot with
  `animate-ping` halo when capturing, text 11 px semibold in **sage** (`#AEB291`), then
  `· {count}` in `fg/0.30` tabular-nums; right side: stack button 26 × 26 with an
  accent badge (14 px, `bg #FF8A3D`, black text, 9 px bold), primary **Add** button
  (`h 26, radius 6, bg #FF8A3D, black text, 11 px semibold`), settings 26 × 26, kebab.
- **Sidebar** 192 px, `border-right overlay/0.09`, nav rows `pl 12 pr 8 py 7 radius 8`
  13 px; selected = `text fg, font-medium, bg overlay/0.07` + **2 × 18 rail** in accent
  (nav items) or `sage/0.70` (groups). Counts 10 px `fg/0.32` tabular-nums, right aligned.
  Section label `GROUPS` = 9 px semibold uppercase `tracking-[0.12em]` `fg/0.38`, with a
  `+` button on the same line.
- **Search** in main window is a real field: `radius 12`, `bg overlay/0.05`,
  `border overlay/0.10`, `padding 10px 12px 10px 36px`, 13 px `fg/0.85`,
  placeholder `fg/0.30`, focus → `bg 0.07` + `border 0.18`. Active filter shows a pill
  (`radius full, px 10, py 4`) with the group name in sage + ×.
- **List**: `padding 0 12 12`, cards `space-y-1.5` (6 px).
- **Card**: `radius 12`, `border 1px overlay/0.10`, `bg overlay/0.04`, padding
  **16 left / 12 right / 12 top / 10 bottom**, hover → `bg 0.06`, `border 0.16`,
  `shadow-lg black/30`, **`translateY(-1px)`**, `duration-150`; press `scale 0.995`.
  Selected → `bg accent/0.08`, `border accent/0.28`, `shadow-md accent/0.10` +
  3 px accent gradient rail (full height).
- **Card meta row** (the part our cards are missing): separated by a **1 px
  `overlay/0.07` top border**, `margin-top 8, padding-top 8`; contains group chips
  (`radius full, px 8, py 2, 9 px semibold uppercase tracking-wide, bg overlay/0.07,
  border overlay/0.12, text fg/0.48`), a `manual` chip in accent/10 + accent/68,
  then **relative time** 10 px `fg/0.32`; on the right a copy button that is
  `opacity 0` until hover/selection.
- **Section header** (`.section-header` in `app.css`): 9 px semibold uppercase
  `tracking-[0.12em]`, `mt 4 mb 8 px 4`, colour per section
  (`Today fg/0.50`, `Yesterday fg/0.40`, `This Week fg/0.32`, `Earlier fg/0.26`,
  `Pinned amber-400/0.50`), **followed by a flex-1 1 px hairline `overlay/0.06`**
  so the label reads like a rule with a caption. Sections `mb-4` (16 px).
- **Pinned** items: same card, amber signal — rail `amber-400/0.45`, `bg amber-400/0.03`,
  `border amber-400/0.12`, hover lift; when selected the accent rail/border wins.
- **Empty state**: 64 × 64 tile (`radius 16, bg overlay/0.03, border overlay/0.06`)
  with a 28 px icon at `fg/0.15`, then 14 px medium `fg/0.35` headline, 12 px
  `fg/0.20` line, then 3 shortcut rows (`p-3, radius 12, bg overlay/0.03,
  border overlay/0.05`) each with a keycap and 11 px `fg/0.30` label.
- No footer in the main window — keyboard hints live in Help and the empty state.

---

## 5. Layout anatomy — settings

- Header 48 px: back button 28 × 28 `radius 8`, gear 15 px, title 13 px semibold
  `fg/0.88 tracking-tight`. `data-tauri-drag-region` (whole header drags the window).
- Body: **`max-width 672 px` (2xl) centred**, `padding 20`, `space-y-6` (24 px). — CORRECTED 23 Nov: Tailwind `2xl` is 672 px, not 512 (docs\DESIGN-layout.md re-extracted this from source; supersede any 512 references).
- Section = label + one or more cards. Label: **9 px semibold uppercase
  `tracking-[0.1em]`, `fg/0.30`**. (Modal labels use `.section-label` = 10 px,
  `tracking-[0.1em]`, colour `#AEB291/0.80` — sage, for in-dialog grouping.)
- Card: `bg surface`, `radius 12`, `border overlay/0.08`, `padding 14`,
  `space-y-3` to its label.
- Row: title 13 px medium `fg/0.80`; description 11 px `fg/0.45` `leading-relaxed`
  `mt 2`; control right-aligned.
- Segmented control: wrapper `radius 12, bg overlay/0.06, border overlay/0.10, p 2`;
  active pill `bg #FF8A3D, **black** text, 12 px medium, radius 8, px 12 py 6`.
- Switch: 38 × 22 track, 18 px white knob with `translateX(16px)`,
  off = `overlay/0.18`, on = `#FF8A3D`, `transition-colors`; knob `transition-transform`.

---

## 6. Spacing rhythm

Ortu works on a **2 px grid with a 4/6/8/10/12/14/16 ladder**. Reuse these, don't invent:

| Token | Value | Used for |
|---|---|---|
| hair | 1 px | borders, dividers |
| tick | 2 px | rails (width), segment padding |
| xs | 4 px | pill inner padding-y, chip gaps |
| sm | 6 px | popup list inset, card gap (`space-y-1.5`), section-label gap |
| md | 8 px | popup row padding-y, card meta top gap |
| lg | 10 px | popup row gap, popup header gap, card bottom padding |
| xl | 12 px | popup row padding-x, card padding-x, most radii |
| 2xl | 14 px | popup header padding-x, settings card padding, window radius |
| 3xl | 16 px | window content margin, main list padding-x, empty-state gap |
| 4xl | 20–24 px | settings page padding / section gap |

Radii: **14** window · **12** cards, rows, popup rows, modals · **10** large tiles ·
**8** buttons, nav rows, type tiles · **6** menu items, keycaps · **full** pills, rails, switches.
Our current `CornerRadius 10 / 8 / 14` is one step tight on cards and rows (10 → 12).

---

## 7. Typography

Manrope. Sizes are literal px from Tailwind arbitrary values; line heights follow
`leading-snug` (1.375) and `leading-relaxed` (1.625), otherwise `normal`.

| Role | Size | Weight | Colour | Notes |
|---|---|---|---|---|
| Popup search field | **14** | 400 | `fg/0.75` | placeholder `fg/0.18` |
| Main-window search | 13 | 400 | `fg/0.85` | placeholder `fg/0.30` |
| Item body (selected) | 13 | 400 | `fg/0.75` | `line-clamp-3`, `leading-snug` |
| Item body (rest) | 13 | 400 | `fg/0.55` | single line, `truncate` |
| Card text (main) | 13 | 400 | `fg/0.70–0.80` | `leading-relaxed`, `line-clamp-3` |
| URL path / code | 12–13 | 400 | `fg/0.60–0.75` | path `break-all` `line-clamp-2`; code `mono` |
| Item description | **10** | 600 | `sage/0.60–0.70` | `tracking-tight`, the only coloured body text |
| Domain / language label | 10 / 9 | 600 / 700 | `sage/0.78` / `sage/0.80` | **uppercase**, `tracking-wider`/`widest` |
| Meta line, relative time | **10** | 400 | `fg/0.32` | tabular-nums for counts |
| Group / category chip | 9 | 600 | `fg/0.48` | uppercase, `tracking-wide` |
| Section header | **9** | 600 | per-section `fg/0.26–0.50` | uppercase, `tracking-[0.12em]` |
| Settings/status label | 9 | 600 | `fg/0.30` | uppercase, `tracking-[0.1em]` |
| Status bar text | 9 | 500 | `fg/0.20` | `tracking-wide` |
| Status-bar hint text | 9 | 400 | `fg/0.35` | next to a keycap |
| Header pill / banner | 11 | 600 / 400 | sage, `fg/0.65` | |
| Buttons | 12 | 600 | black on `#FF8A3D` | ghost buttons `fg/0.50` → `fg/0.80` |
| Nav rows | 13 | 400 / 500 when active | `fg/0.52` → `fg` | |
| Empty-state headline | 14 | 500 | `fg/0.30–0.35` | descriptive, sentence case |
| Masked secret | 15 | 400 | `fg/0.40` | mono, `tracking-[0.3em]`, `••••••••` |
| Toast | 12.5 | 500 | `fg/0.80` | `leading-snug` |

Rules that fall out of this:
- **Two registries only**: 13 px body, 9–10 px metallic labels. Almost nothing at 11–12 px
  except buttons, banners and toasts.
- Meta labels are **uppercase 9/10 px semibold with 0.1–0.12em tracking**, never italic.
- Body text is **never pure white** — `fg/0.55–0.80` reads as the Ortu texture.
  Pure white is reserved for nav/titles.
- Truncation ladder: 1 line (`truncate`) → 2 lines (URL path) → 3 lines
  (`line-clamp-3`, the default for body) → full (`Show more` when >3 lines **or** >250 chars).

---

## 8. Colour semantics

Tokens are RGB triplets + `overlay` for neutral white/near-black alphas
(`src/app.css`). Dark: `app 8 9 12`, `surface 19 21 27`, `raised 28 31 40`,
`fg 255 255 255`, `overlay 255 255 255`; accent `#ff8a3d` and sage `#aeb291`
are **theme-independent** (same value in light and dark). Light overlay is `17 24 39`.

How each colour earns its place:

- **Accent `#FF8A3D`** — scarce and structural, never decorative text:
  selected card tint (8%) + border (28%) + rail; focused nav rail; **primary button
  fill with black text**; stack badge; `manual` chip; danger hover (`delete`);
  drop target ring. Not used for body copy, not for links, not for every icon.
- **Sage `#AEB291`** — the second accent, and the reason Ortu doesn't read
  orange-soup: capture pill, URL domain labels, code language badges, group-filter
  selection, success toast, "Reveal" affordances, secondary buttons in dialogs.
- **Amber (`amber-400 #FBBF24`)** — pinned only (rail, chip, section header, pin icon).
- **Sky (`sky-400/0.30`)** — URL rail. **Violet (`violet-400/0.30`)** — code rail.
  **Green (`green-500`)** — the 380 ms copy confirmation only.
- **`fg` alphas** — hierarchy is done with opacity of the foreground, not with new greys.
  In dark, borders are **white-alpha**, not a fixed slate: 0.05–0.07 for dividers,
  0.08–0.12 for borders, 0.14–0.16 for hover/focus borders.
- **Hover** — `overlay/0.04` (popup rows), `0.05–0.06` (nav, main cards),
  `0.07–0.09` (icon buttons). Hover **never** moves text colour more than to `fg/0.80`.
- **Focus** — there is no visible focus ring anywhere. Fields get
  `border overlay/0.18` + `bg 0.07`; the keyboard *selection* is the focus indicator.
  (Accessibility caveat: Ortu leans entirely on selection; if we add a ring, use
  1 px accent/0.35 — do not use the Windows default blue.)
- **Selection** — tint + inset ring + rail (§3). Full-on `Accent` fill is reserved for the
  primary button, the stack badge and the settings segmented control.

Composited values for WPF (so we can keep plain `SolidColorBrush`es instead of opacity
stacks) — white-alpha over `Surface #13151B`, computed, rounded:

| Token | Hex | Token | Hex |
|---|---|---|---|
| `fg/0.80` | `#D0D0D1` | `overlay/0.08` (hover) | `#26282D` |
| `fg/0.75` | `#C4C5C6` | `overlay/0.06` (hover, subtle / divider) | `#212329` |
| `fg/0.70` | `#B8B9BB` | `overlay/0.05` (field bg) | `#1F2126` |
| `fg/0.55` | `#959698` | `overlay/0.04` (card bg) | `#1C1E24` |
| `fg/0.48` | `#848588` | `overlay/0.12` (border) | `#2F3136` |
| `fg/0.35` | `#66676B` | `overlay/0.18` (focus/switch off) | `#3B3D40` |
| `fg/0.30` | `#5A5B5F` | `accent/0.08` (selected card) | `#1C1310` |
| `fg/0.25` | `#4E5054` | `accent/0.22` (selected border, popup) | `#3E2517` |
| `fg/0.20` | `#424449` | `accent/0.28` (selected border, card) | `#4D2D1A` |
| `fg/0.18` (placeholder) | `#3D3F44` | `sage/0.12` (chip/capture bg) | `#1C1D1C` |

(`overlay/0.12` ≈ our existing `Hairline #262A35`, so Hairline stays as the card border and
6–7% white becomes the divider colour.)

---

## 9. Motion

Framer-less: Tailwind transitions and a few Svelte transitions. Everything is **short** —
the app feels instant, not animated.

| Moment | Spec | Source |
|---|---|---|
| Row hover | `transition-all *duration-100*` (popup) / `150` (cards); colour + inset ring only | popup/`+page` |
| Group row hover | `duration-75` | popup |
| Card hover lift | `translateY(-1px)` + `shadow-lg black/0.25–0.30`, `duration-150` | `+page.svelte` |
| Card press | `scale 0.995`, `translateY(0)` | `+page.svelte` |
| Button press | `active:scale-95` | `app.css .btn-primary` |
| Copy confirmation | card goes `bg green-500/0.07`, `border green-500/0.30`, `shadow-lg green/0.08` for **380 ms**, then back | `flashingItemId` + `setTimeout(..., 380)` |
| Capture dot | Tailwind `animate-ping` halo (1 s, infinite) over a 7 px dot | `+page.svelte` header |
| Toast | in `fly: y 18, 220 ms`; out `fade 150 ms`; reflow `flip 200 ms` | `Toaster.svelte` |
| Notification window | 150 ms fade in, hold, 150 ms fade out | our `NotificationWindow.xaml.cs` (already matches the spirit) |
| Popup open/close | **none** (window is positioned before show; close is instant hide) | `lib.rs` |
| Selected-row growth | no tween — instant relayout, because the ring/bg transition is 100 ms | popup |

Easing: Tailwind default `cubic-bezier(0.4, 0, 0.2, 1)` for `transition-*`; Svelte
defaults for `fly`/`fade` (`cubicOut` / `linear`). In WPF translate these to
`CubicEase(EaseOut)` with `Duration 0:0:0.10–0.15`; do **not** exceed 200 ms.

Signature moments to preserve: the **380 ms green pulse on the copied card**, the
**growing selected row**, and the **orange rail sliding down the left edge** as you arrow.
All three are cheap and all three are what makes Ortu feel like Ortu.

---

## 10. Microcopy (verbatim)

Search:
- popup: `Search history, groups…` · scoped: `Search in {Group}…`
- main: `Search clips…` · scoped: `Search in {Group}…`
- clear button `aria-label="Clear search"`

Empty:
- popup (empty history): `Nothing here yet` — 12 px `fg/0.25`, with a 40 × 40 tile
  (`radius 16`, `bg overlay/0.03`, `border overlay/0.05`, 18 px icon `fg/0.15`),
  `py-10`, centred.
- popup (no match): `No results`.
- main: `Your clipboard is empty` / `Start copying text anywhere and it'll appear here automatically.`
  plus three hint rows: `{Alt+V} Open quick popup anywhere`, `{Ctrl+1-9} Instantly copy by position`,
  `Add items manually with Add`.
- main search: `No results for "{query}"` + `Try a different search term`.
- main group: `Nothing in "{group}"` + `Copy something, then assign it to this group`.

Status / banners:
- `{n} clips` · `{n} clips · {m} groups` (popup footer)
- `{n} in paste stack · [key] to paste next` + `Clear` (banner, accent/0.08 bg, accent/0.15 border)
- `Ortu v{version} is available` + `Download` (sage banner) / `Download update →` (main)
- `Pinned`, `Today`, `Yesterday`, `This Week`, `Earlier` (section headers)
- `GROUP` (right edge of group suggestion rows), `manual` chip
- `Added to paste stack`, `Couldn't open the download page` (toasts)
- `Save to Group`, `New group…`, `Create & Save` (group overlay)
- Confirmation is always terse, sentence case, no exclamation marks, no "successfully".

Timestamps (`relativeTime()` in `+page.svelte`):
`just now` (<1 m) · `{m}m ago` · `{h}h ago` · `Yesterday` · weekday short (`Mon`) ·
`Nov 23`. **No timestamps at all in the popup** — recency is the list order.
Note `graph: "2h ago"` in screenshots is `{h}h ago`, not "2 hours ago".

---

## 11. Keyboard affordances

- **Keycap** (`.kbd` in `app.css`): inline-flex, `radius 6`, `border overlay/0.16`,
  `bg overlay/0.08`, `fg/0.70`, **mono**, **semibold**, `px 4 py 2`, size 8–10 px
  depending on context. It is a physical-looking chip, never uppercase.
- Hint lines pair a keycap with a lowercase verb: `↵ paste`, `esc hide`.
  Glyphs are used where they read (§ `↵`, `esc`), not `Enter`/`Escape`.
- Popup footer is the only always-visible cheat sheet; main window teaches through the
  empty state and the Help modal.
- Selection indicator doubles as the keyboard cursor: rail + tint + ring, no dotted frame,
  no `:focus` outline. **Arrow keys bound with wrap-around** (`(i+1) % total`), and the
  selected row is kept in view with `scrollIntoView({block:"nearest"})` —
  i.e. never jump the list, only nudge it.
- `Esc` unwinds one layer at a time: group overlay → active filter → hide window.
  `Backspace` on an empty query clears the filter.
- Numbers `Ctrl/Cmd+1–9` copy by position; the position is 1-based on the *visible* list,
  group suggestions included when a query is typed.

---

## 12. Diff — Ortu pattern → our XAML → gap & exact change

Legend: **MATCH** (keep), **GAP** (change), **ABSENT** (build).

### Shell / window

| # | Ortu | Ours (`UI/MainWindow.xaml`) | Verdict | Exact change |
|---|---|---|---|---|
| 1 | Popup 550 × 450, centred in work area | 560 × 620, centred then `−40` px | **GAP** | `Width="550" Height="450"`. Drop the `- 40` in `MainWindow.xaml.cs` (`Top = wa.Top/sc + (wa.Height/sc - Height)/2`). 450 shows ~9 compact rows; 620 with one-line rows looks like a different app. |
| 2 | Radius 14, 1 px `overlay/0.08` border | 14 + Hairline | **MATCH** | — |
| 3 | Soft, wide drop shadow (window-level) | `BlurRadius 28, Depth 6, 0.55` | **MATCH** | — |
| 4 | Content padding 16 | `Grid Margin="16"` | **MATCH** | — |
| 5 | Popup opens instantly, no fade | `Show()` | **MATCH** | — |

### Header / search

| # | Ortu | Ours | Verdict | Exact change |
|---|---|---|---|---|
| 6 | Search is a **flat band** inside the window: `min-height 48`, `px 14`, bottom hairline, 14 px input, no inner box | Search is a **raised pill** (`Raised` + `Hairline` + radius 8 + `Padding 10,9` + `Margin 0,0,0,12`) | **GAP** | Replace the Border with a `Border BorderThickness="0,0,0,1" BorderBrush="{StaticResource Hairline}" Padding="14,0" MinHeight="48"`; `Grid` with 14 px icon (`fg/0.20`) `gap 10`; `TextBox FontSize="14" Foreground="#C4C5C6"` (fg/0.75), `Padding="0,12"`. Delete `Margin` — the list's own 6 px inset provides breathing room. |
| 7 | Placeholder `fg/0.18`, i.e. nearly absent | No placeholder in the live window | **ABSENT** | Add an overlay `TextBlock Text="Search history, groups…" FontSize="14" Foreground="#3D3F44" IsHitTestVisible="False"` shown when `Search.Text` is empty. |
| 8 | Clear (×) button appears with text | none | **ABSENT** | 13 px `×` `Foreground="#424449"` → hover `#959698`, `VerticalAlignment=Center`, `Visibility` bound to text length. |
| 9 | Filter breadcrumb pill (sage) replaces the search icon | none | **ABSENT** | Skip for sprint 1 unless groups ship; if shipped: `CornerRadius 12, Background #1C1D1C, BorderBrush #33402F, Padding 10,0, Height 24`, text 11 px `#AEB291`. |

### Rows / cards — the biggest delta

| # | Ortu | Ours | Verdict | Exact change |
|---|---|---|---|---|
| 10 | **Flat rows**: no border, no shadow, radius 12, `padding 8px 12px`, 6 px list inset | Bordered card on `Surface`, radius 10, `Padding 12,10`, `Margin 0,0,0,6` | **GAP** | Delete the `Card` border/background. New `Border`: `Background="Transparent" CornerRadius="12" Padding="12,8" Margin="0,0,0,0"`. The list container becomes `Padding="6"` (`StackPanel`/`ItemsPanel`), and items are separated only by the row's own padding. Rows go from ~64 px to ~38 px, which is exactly how 9 rows fit in 450 px. |
| 11 | Body 13 px at `fg/0.55` when compact, `fg/0.75` when selected, `leading-snug` | 13 px `Fg` (full white), `LineHeight 19`, `MaxHeight 76` (4 lines) | **GAP** | `Foreground="#959698"` default, `#C4C5C6` when selected; `LineHeight="18"`, `MaxHeight="54"` (3 × 18). |
| 12 | **Selected row grows**: 1 line → 3 lines + description line | fixed 4-line clamp always | **GAP** | `DataTrigger IsSelected`: `MaxHeight 54`, `TextWrapping Wrap`, `TextTrimming` → none. Optionally show a 10 px `Description` line above (we have `AppName`/`WindowTitle`, not a description — skip the line rather than fake it). |
| 13 | Selected: `bg overlay/0.08` + inset 1 px accent/0.15 + 2 × 24 accent gradient rail | `Raised` + 1 px `Accent` border | **GAP** | `Background="#26282D"`, `BorderBrush="Transparent"`, and a 2 px rail `Rectangle Width="2" Height="24" RadiusX/Y="1" HorizontalAlignment="Left" VerticalAlignment="Center"` with `LinearGradientBrush` `#FF8A3D → #FF6B1A`. For the inset ring, overlay a `Border BorderThickness="1" BorderBrush="#3E2517" CornerRadius="12"` inside the row. Do not paint a full accent border — it is much louder than Ortu. |
| 14 | Hover: `overlay/0.04`, no border change | none | **ABSENT** | `Trigger IsMouseOver`: `Background="#1C1E24"`. Must not fight selection → order the triggers so `IsSelected` wins (WPF trigger order: later wins, so declare `IsMouseOver` first). |
| 15 | 22 × 22 type tile, `radius 8`, `bg overlay/0.05`, 11 px icon `fg/0.25` | none (text only) | **ABSENT** | Add a `Border Width="22" Height="22" CornerRadius="8" Background="#1F2126"` + 11 px glyph (`Segoe MDL2`/`Segoe Fluent`), `Margin="0,0,10,0"`, `VerticalAlignment="Top"`. Glyph per `Kind` via `DataTrigger` (text = page, image = picture, URL = globe). Optional for sprint 1, but it is what makes the left column read as a list. |
| 16 | Right actions hidden until hover (stack/pin/group/delete) | none | **ABSENT** | Park for the sprint where delete/pin land; the hover-hide pattern is `Opacity 0` default, `1` on `IsMouseOver`, 11 px icons `fg/0.25`. |
| 17 | Meta = group chips + **relative time**, 10 px `fg/0.32`, under a hairline | Meta = `"App  ·  23 Nov 09:47  ·  window title"`, 11 px **italic** `Muted` | **GAP** | (a) drop `FontStyle="Italic"` — Ortu never italicises metadata; (b) `FontSize="11"` → `10`, colour `#5A5B5F`; (c) add `Border BorderThickness="0,1,0,0" BorderBrush="#212329" Margin="0,8,0,0" Padding="0,8,0,0"` around the meta; (d) switch the middle segment to relative time (`just now` / `5m ago` / `2h ago` / `Yesterday` / `Mon` / `Nov 23`). App name can stay; **window title belongs in a tooltip**, not the row — Ortu keeps rows to one metadata line. |
| 18 | Pinned is a labelled section with amber signalling | none | **ABSENT** | Needs a `IsPinned` flag on `ClipboardItem` first. Then: `section-header` text `Pinned` 9 px semibold uppercase `CharacterSpacing 120`, `Foreground="#826418"` (amber-400 at 50 % over App), plus an amber rail on the card (`amber/0.45` over App ≈ `#755B17`). Out of scope if pinning isn't in this sprint. |
| 19 | Time sections `Today / Yesterday / This Week / Earlier` | none | **ABSENT** | Use a `CollectionViewSource` with `GroupDescriptions` (PropertyGroupDescription on a computed `Bucket`), and a `GroupStyle.HeaderTemplate` rendering the `.section-header` look: 9 px semibold uppercase, `Foreground="{StaticResource Muted}"`, `Margin="4,4,4,8"` + a 1 px `#212329` rule filling the row (`Grid` with `Rectangle` in column 2). Cheapest high-impact item on this list. |
| 20 | Empty state: tile + headline + sub + keycap hint rows | none | **ABSENT** | Add a `Grid` in the same row as the `ListBox`, `Visibility` bound to `Items.Count == 0`: 64 × 64 tile (`#1C1E24`, `#26282D` border, radius 16, 28 px glyph `#4E5054`), headline `Nothing here yet` / `No results` 14 px medium `#5A5B5F`, sub 12 px `#424449`, then one keycap row (`Ctrl+V` `Open quick popup anywhere`). |

### Footer / hints

| # | Ortu | Ours | Verdict | Exact change |
|---|---|---|---|---|
| 21 | Status bar 450-window: `{n} clips` left, `↵ paste` `esc hide` right, 9 px, top hairline | none | **ABSENT** | Add row 2: `Border BorderThickness="0,1,0,0" BorderBrush="#212329" Padding="16,8"`; left `TextBlock` 9 px `#424449` ("{n} clips"), right `StackPanel` of keycap+verb pairs, 9 px `#66676B`, keycap 8 px. Fixed height ≈ 30 px — budget it inside the 450. |
| 22 | Keycap chip: radius 6, 1 px `overlay/0.16`, `bg overlay/0.08`, mono semibold, `px 4 py 2` | none | **ABSENT** | Shared `Style x:Key="Kbd"`: `Border` template, `CornerRadius 6`, `Background="#26282D"`, `BorderBrush="#3B3D40"`, `Padding 4,1`, `TextBlock FontFamily="Consolas" FontSize="9" FontWeight="SemiBold" Foreground="#B8B9BB"`. Put it in the theme dictionaries so Settings and the footer share it. |

### Colour & motion

| # | Ortu | Ours | Verdict | Exact change |
|---|---|---|---|---|
| 23 | Accent used structurally; **sage as second accent**; amber/sky/violet as semantic rails | Accent only; every label is white/muted | **GAP** | Add `Sage #AEB291`, `Amber #FBBF24`, `Sky #38BDF8`, `Violet #A78BFA` to **both** theme dictionaries, plus `Fg70/Fg55/Fg35/Fg25` composited brushes (they differ per theme, so they belong in the dictionaries, not as opacity on usage). Use sage for app/domain labels and success, amber for pinned, sky for URLs. |
| 24 | Selected tint 8%, hover 4%, borders 5–12% white | Raised vs Surface only (12% steps), accent border | **GAP** | Add `RowHover #1C1E24`, `RowSelected #26282D`, `Divider #212329`, `RingAccent #3E2517` to dark (light-theme equivalents over `#FFFFFF`). |
| 25 | Hover lift + 380 ms green copy pulse + pulsing capture dot | Notification fades 150 ms; no list feedback | **GAP** | Card hover: `TranslateTransform` 0 → −1 px + shadow, 150 ms `CubicEase`. Copy pulse: set a `IsFlashing` bool, `DataTrigger` → `Background="#0A1612"` (green/0.07 over app), `BorderBrush="#1E3A2A"`, clear via `DispatcherTimer 380 ms`. Skip the ping dot unless capture-pause ships. |
| 26 | Tabs | our `_fonttest` mock had `Clipboard / Images` pills | — | **Do not build tabs.** Ortu's popup has none; filtering is search + (later) a breadcrumb pill. The mock's tabs were an invention. |

### Notification window

| # | Ortu | Ours (`UI/NotificationWindow.xaml`) | Verdict | Exact change |
|---|---|---|---|---|
| 27 | Toast: `bg raised/0.95`, radius 12, `border overlay/0.12`, 3 px accent rail left, icon 15 px, 12.5 px medium text, `shadow-lg`; bottom-centre; fly 18 px/220 ms | `Surface` bg, radius 12, app name in accent, time italic, body 13 px, 150 ms fade | **GAP** | Swap `Surface` → `Raised` (#1C1F28); add the 3 px left rail (accent / sage for success / `#F87171` for error); app name 11 px semibold sage (not accent orange — Ortu reserves orange for selection/CTA), time 10 px `#5A5B5F` **not italic**, body 12.5 px `#D0D0D1`. Keep the 150 ms fade; if moving to 220 ms `y+18` slide, animate `RenderTransform` not `Top` (Top animation is janky per-frame). |
| 28 | Radius 12 on a small floating surface | 12 | **MATCH** | — |

### Settings window

| # | Ortu | Ours (`UI/SettingsWindow.xaml`) | Verdict | Exact change |
|---|---|---|---|---|
| 29 | Window bg `app`, cards `surface`, 672 px (2xl) centred column (corrected from 512 — see DESIGN-layout.md), 20–24 px section gaps | Window bg `Surface`, everything is a `Raised` row, 440 wide, no section rhythm | **GAP** | Root `Background="{StaticResource App}"`; content `MaxWidth="672"`; section cards `Background="{StaticResource Surface}" BorderBrush="{StaticResource Hairline}" CornerRadius="12" Padding="14"`; gaps 24 px between sections, 12 px between cards in a section. |
| 30 | Section labels: 9 px semibold uppercase, `tracking 0.1em`, `fg/0.30` | `H` style = 13 px semibold, sentence case, `Fg` | **GAP** | Restyle `H` → `FontSize 9, FontWeight SemiBold, Foreground #5A5B5F, CharacterSpacing 100` (WPF `CharacterSpacing` is 1/1000 em → 100 = 0.1em). WPF has no `text-transform`, so write the labels uppercase in XAML (`HOTKEY`, `NOTIFICATION`). |
| 31 | Row title 13 medium + 11 px `fg/0.45` description under it | Label 12 px, hint 11 px muted, separate line | **GAP** | Row becomes a two-line stack inside the card: title `Fg70` 13, description `#66676B` 11 with `TextWrapping`. Existing `Hint` style mostly matches; move it inside the card. |
| 32 | Switch 38 × 22 with 18 px knob | `CheckBox` with ✓ box | **GAP** | Custom `CheckBox` template: 38 × 22 track (`CornerRadius 11`), `#3B3D40` off / `#FF8A3D` on, 18 px white ellipse translated 16 px, 150 ms colour + transform. |
| 33 | Segmented control (accent pill, black text) for enum choices | `ComboBox` for Position, text boxes for numbers | **GAP** (where enums exist) | Position (5 values) → segmented control or `RadioButton` pills: wrapper `#212329` radius 12, pill `#FF8A3D` + black 12 px, unselected `#959698`. Numeric fields stay as text boxes; give them the `modal-input` look: `bg #1F2126, border #2F3136, radius 8, px 12 py 8, 13 px`. |
| 34 | Header 48 px with back chevron + gear + 13 px semibold title | Header block 15 px bold + 11 px sub, no chrome | **GAP** | 48 px header, `Border` bottom hairline, 28 × 28 back button (`#26282D` on hover), 13 px semibold title `#E4E4E4`, drop the "Changes apply when you save." line (Ortu states consequences inline, per row). |

---

## 13. Implementation notes specific to WPF

- **Do not attempt backdrop blur.** Ortu's `bg-app/0.97 + blur(24)` is a web nicety; on WPF the
  opaque `App` window we already have is indistinguishable at 97% opacity. Keep
  `AllowsTransparency=True` and the existing window shadow.
- **Alpha text** — prefer the composited hex table in §8 over `Opacity` on `TextBlock`s:
  opacity on a parent multiplies down and will wash out children (e.g. icon + text rows).
- **`CharacterSpacing`** is in 1/1000 em: `0.12em → 120`, `0.1em → 100`, `tracking-wide 0.025em → 25`.
- **Trigger order** matters: declare `IsMouseOver` before `IsSelected` so selection wins.
- **Row height** in the popup must be driven by content (compact vs selected), not fixed —
  use `MaxHeight` on the text, not `Height` on the row, or the growth won't animate/lay out.
- **Relative time** needs a converter + `PropertyChanged` (or recompute in `Meta` like today).
  Cheapest: keep formatting in `ClipboardItem.Meta`, but split into `RelativeTime` and
  `MetaApp` properties so XAML can style them separately.
- **Keycap** and **section header** styles belong in `UI/Themes/Dark.xaml` + `Light.xaml`
  so Settings, footer and empty state share them. Our theme files are currently 7 brushes;
  expect ~20 (brushes + 3 styles) and **both** dictionaries must stay in sync — the light
  theme has no equivalents for `overlay`-alpha colours yet, so compute the light pairs
  (white-alpha over `#F2F3F5`, near-black-alpha `overlay = #111827`).

## 14. Deliberate divergences to keep

1. **Manrope everywhere** (Ortu uses it as a fallback). Keep ours; do not add SF/Segoe first.
2. **450 px popup** is right for a dense windows clipboard picker, but if we keep identification
   from window titles we will want ~4 lines visible — 450 is still the number, the selected row
   grows instead.
3. We have `Application` + `WindowTitle` metadata per clip, which Ortu doesn't show in the popup.
   Put it in the tooltip, not in the row (see #17) — otherwise the metadata line becomes noise.
4. `_fonttest/MainWindow.xaml` used `#2A2E3A` / `#333849` ad-hoc borders and fake tabs. Treat it
   as historical; the tokens in `Themes/*.xaml` are the only source of colour.

---

## 15. Not verified / limits

- No dark-mode screenshot exists upstream; contrast impressions for dark are derived from the
  CSS alphas, not from a picture. If a dark screenshot surfaces later, re-check #13/#24 tints.
- Hover-preview panel behaviour (>140 chars) was read from source only; no recording available.
- Tailwind default durations/easing are standard values; Ortu does not define custom
  `transitionDuration`/`timingFunction` in `tailwind.config.ts`, so defaults are correct.
- The 380 ms flash, `animate-ping` 1 s and toast timings are the only animation timings in
  source; there is no entrance animation to copy.
- Product Hunt / X posts by the author were not surfaced as design sources; the site
  (<https://ortu.abhijithpsubash.com/>) is marketing copy plus the four screenshots above,
  with no dark UI.
