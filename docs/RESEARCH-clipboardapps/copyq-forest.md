# CopyQ's Forest theme — extracted values + Clip row-separation mapping

**Read, not run.** CopyQ `master` @ 23 Nov 2026, files fetched raw from `raw.githubusercontent.com/hluk/CopyQ/master/`
(no clone needed). Every value carries its `file:line`. GPL-3.0-or-later: **behaviour and numbers only**, no code
or asset reuse. Directive under test: `DESIGN-v2-directives.md` §8 (alternate rows + divider, like Forest).

---

## 1. Forest's values — `shared/themes/forest.ini`

| Role | Value | Source |
|---|---|---|
| even-row base (`bg`) | `#FFFAEB` warm cream | `:12` |
| odd-row base (`alt_bg`) | `#F1EDCB` — 12.6 % darker than `bg` | `:2` |
| odd-row gradient bottom | `#E0DCBA` (`${alt_bg - #112}`, top→bottom) | `:3-11` |
| odd-row corner radius | 3 px | `:10` |
| selected fill (`sel_bg`) | `#6EB662` green | `:29` |
| selected gradient bottom | `#4C9440` (`${sel_bg - #222}`) | `:31-41` |
| selected ink (`sel_fg`) | `#FFFFFF` | `:30` |
| body ink (`fg`) | `#2F2A15` — **identical on both parities** | `:17` |
| gutter ink (`num_fg`) | `#96865B` — the gutter has its own tint | `:27` |
| gutter font | Ubuntu Mono 7 pt | `:28` |

No alternate ink: `items.css:7` reads `${alt_fg}`, but `alt_fg` is defined in neither `forest.ini` nor
`theme.cpp`, so odd rows inherit `fg`. Computed from the hexes: zebra step **1.136:1**; odd top→bottom edge
1.172; `sel_fg` on `sel_bg` only **2.46:1**; `num_fg` **3.44** on even and **3.02** on odd — CopyQ ships a
non-compliant gutter, so its tint is the one Forest value Clip must *not* copy.

## 2. How CopyQ implements it (three mechanisms, no fourth)

- **Stylesheet + palette, not delegate paint.** `setAlternatingRowColors(true)` (`clipboardbrowser.cpp:222`)
  and `palette.setColor(QPalette::AlternateBase, alt_bg)` (`theme.cpp:320`); the rule that actually fills
  is `#ClipboardBrowser::item:alternate:!hover:!selected{background:${alt_bg}}` (`items.css:6-9`).
- **One draw call.** `ItemDelegate::paint` draws the view background clipped to the row, then
  `drawControl(CE_ItemViewItem)` (`itemdelegate.cpp:612-622`). There is no parity branch and no second
  brush in C++ — parity is Qt's `:alternate` pseudo-state.
- **Selection wins by specificity, hover cancels the zebra.** `:selected` (`items.css:10-13`) outranks
  `:alternate:!selected`, so a selected row is one solid band; `::item:hover:!selected{background:${bg}}`
  (`items.css:39-43`) makes hovering an odd row *revert it to the even colour* rather than tint it.
- **Divider: CopyQ has none.** `divider`/`separator` occur only in menus, actions and command stores —
  never in item theming (repo-wide grep). Separation comes from (a) `item_spacing`, default
  `lineSpacing()/6` ≈ 2–3 px of view `bg` showing between rows (`theme.cpp:287-288`), and (b) the odd
  row's gradient darkening its bottom edge. **Clip must add the hairline Forest doesn't have** (§8).

## 3. Token mapping proposal — two touches, one reuse

| Token | Dark | Light | Action |
|---|---|---|---|
| `RowAlt` | `#16181D` | `#FFFFFF` | **new** — the odd-row fill |
| `RowHover` | `#121316` → `#0AFFFFFF` | `#E9EAED` → `#0A111827` | **alpha form**, pixel-identical on `App` |
| `Divider` | `#212329` | `#E2E5EA` | **reuse as-is** (`Dark.xaml:24`, `Light.xaml:23`) |

`RowHover` must become 4 % alpha, not stay opaque: an opaque hover brush *occludes* the zebra, and in dark
it is brighter than the alt row (`#121316` vs `#16181D`), so hovering would dim a row — the copy of Forest's
bug in `items.css:41`. As alpha it composites to exactly the shipped hex on `App` (verified: white 4 % over
`#08090C` = `#121316`; ink `#111827` 4 % over `#F2F3F5` = `#E9EAED`), so every ratio in
`DESIGN-contrast.md` §5 still holds, and over a zebra row it now tints instead of replacing. `RowHover` is
used only at `RowTemplate.xaml:51`, so nothing else moves. `RowSelected` stays **opaque** (it is also the
Picker pill at `MainWindow.xaml:350`, and a selected row must read as one clean band under its ring/rail).

Contrast check, computed from the hexes, 4.5:1 bar:

| Pair | Dark | Light |
|---|---|---|
| zebra step (App → `RowAlt`) | **1.121** | **1.110** |
| `Fg55` — body **and** number — on `RowAlt` | 6.00 | 5.78 |
| `AccentText` (search hit) on `RowAlt` | 7.57 | 5.26 |
| hover over an even / over an odd row | 1.072 / 1.103 | 1.083 / 1.083 |
| `Divider` on App / on `RowAlt` | 1.27 / 1.13 | 1.14 / 1.26 |

Both steps land on Forest's 1.136. Both themes go **lighter**, and both are forced that way: dark App is
3.6 % luminance (nothing darker exists), and in light anything at or below `#EBEDF0` drops `AccentText` to
4.49 — below the bar, and `DESIGN-contrast.md` §5.2 already forbids accent text on a tinted row in light.
`#FFFFFF` is the light ceiling and gives 5.26. No separate gutter token: `NumberText` keeps its audited
`Fg55 → Fg75` (synthesis Q4), unlike CopyQ's `num_fg`.

## 4. Mechanism the sprint implements

- Zebra on `RowRoot.Background` only (`RowTemplate.xaml:42`) — **not** `ItemRoot`, so the section band
  (`:31-40`) stays on `App` and `Fg48` keeps its audited surface.
- Parity from `ListBox AlternationCount="2"` (`MainWindow.xaml:239`) + a `DataTrigger` on
  `(ItemsControl.AlternationIndex)` via `RelativeSource AncestorType=ListBoxItem`. **Not** a VM `IsEven`:
  the generator re-assigns `AlternationIndex` when it recycles a container, so alternation survives
  `VirtualizationMode="Recycling"` (`:163-168`) and renumbers correctly after `ApplyFilter`.
- Z-order inside `RowRoot`: fill → **divider overlay** (`Height="1"`, `VerticalAlignment="Bottom"`,
  `IsHitTestVisible="False"`, brush `Divider`) → `HoverLayer` → `SelLayer` → `Ring`/`Rail` →
  `PulseLayer` → content. The line is an overlay in the existing single-cell `Grid`, so it costs **zero
  height**, and every state paints over it.
- Images strip (`ImageTileTemplate`) gets neither: tiles are not rows.
- Brush-only per Q3 — no `Height`/`Width`/`Margin`/`Padding`/`BorderThickness`/`CornerRadius`/`FontSize`/
  `Visibility`/`Stretch` change. `Body` keeps `FontFamily="Segoe UI"` (Devanagari untouched) and every new
  hex lives in `UI\Themes\*.xaml`.
