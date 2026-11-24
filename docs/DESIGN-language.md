# Clip — Design language v3 (Tools3000-derived)

**Owner decision, 24 Nov:** the visual authority is the application UI of Tools3000
(github.com/yuan278501381/Tools3000), replacing Ortu. Orange stays Clip's brand accent — the
*manner* is Tools3000's, the *colour* is ours. Reference screenshots: `%TEMP%\opencode\t3\`.

Tokens live in `UI\Themes\Dark.xaml` and `Light.xaml`. **Nothing outside those two files may name a
hex.** Components live in `Buttons.xaml` / `Popups.xaml` / `RowTemplate.xaml` / `Icons.xaml`.

Why this replaced the old language: the warm-paper light theme measured **2.25:1** for meta text on
a selected row, and dark selection was a brown accent-wash over a neutral surface. Both read as
"unfinished", not "wrong colour". The rules below are the fix.

## 1. Surfaces

| Level | Dark | Light | Use |
|---|---|---|---|
| App | `#0B0D12` | `#F6F7F9` | window/page background |
| Surface | `#141821` | `#FFFFFF` | cards, list, fields |
| Raised | `#1B202B` | `#FBFCFD` | secondary buttons, empty tile |
| Field | `#1F2532` | `#F1F3F6` | inputs, image canvas |

- Cards: **1 px `Hairline` border, no shadow**, radius 12, padding 20, gap 20.
- Window radius 16; controls 8; chips 6; identity tiles 8. Nothing else gets a radius.
- Dividers: 1 px `Divider`. Zebra is allowed only as *texture* (≤1.1:1 vs `App`) — a stripe you can
  name is a spreadsheet.

## 2. Type (Inter; 400 / 500 / 600 only, never 700)

| Role | Size / line | Weight | Ink |
|---|---|---|---|
| Row body | 14 / 20 | 400 | `Fg` |
| Card title, page title | 15–16 / 22 | 600 | `Fg` |
| Secondary (descriptions, paths) | 12 / 18 | 400 | `Fg55` |
| Meta (time, counts, section labels) | 11 / 16 | 400 | `Fg48` |
| Micro-label (uppercase) | 10 / 14 | 600, +0.5 tracking | `Fg48` |
| Button | 12 / 16 | 600 | per variant |
| Keycap | 11 / 14 | 600, Mono | `Fg75` on `KeycapBackground` |

Three levels per surface, maximum. A fourth grey is a bug.

## 3. Row anatomy — the screen that matters

```
[ 28 initial tile ] [ 10 ] [ body, 3 lines max ] [ 10 ] [ chip +N ]
                                                         [ clock   ]
```

- **Identity left:** 28×28 rounded tile, the source app's initial, `BadgeBackground` + `Fg55`.
  Read before the text is: Excel/Word/Chrome are recognised, a row number never is.
- **Facts right:** `+N` chip above the clock, both right-aligned in one column so numbers stack.
  Never inline in the sentence.
- **No per-row metadata line.** Provenance (app · title · time) lives in the bottom bar for the
  selected row only.
- **Selection** = `RowSelected` surface + 1 px `SelectionRing` + 3 px `SelectionRail`. No accent
  wash (it turns brown over a neutral dark), no geometry change, no ring on unselected rows.
- **List clamp:** `ClipRow.ListLinesMax = 3`. The full text is the outside tooltip.

## 4. One grammar per control

- **Chip / badge:** height 18–22, radius 6, padding 6–8, 10–11 SemiBold, `BadgeBackground` +
  `Fg55`; semantic meaning switches ink to `Sage`/`Amber`/`Sky`/`Error`, never a second shape.
- **Keycap:** `KeycapBackground`, 1 px `KeycapLine`, 11 Mono SemiBold. Key hints right-aligned,
  two per surface.
- **Button:** 28–32 high, radius 8. Primary `Accent` + `OnAccent` (dark ink — never white on
  orange, it is 2.6:1). Secondary `Raised` + 1 px `ControlLine`. Ghost transparent. Danger `Error`.
  Buttons hug their label; a button stretched to fill a row is a text field.
- **Segmented:** one `ControlLine` container, selected segment = `Accent` fill + `OnAccent`.
- **Toggle:** 38×22, `Accent` track when on, `ControlLine` when off.
- **Icons:** drawn `Path` only, stroke 1.5, 16 for commands / 20 for empty states. No font glyph
  may render an icon (a fallback face is visible at 100 %).
- **No stock WPF control may appear unstyled**: ScrollBar, ComboBox, ContextMenu, ToolTip,
  MessageBox, focus rectangle.

## 5. Contrast — computed, not guessed

`node %TEMP%\opencode\verify-theme.mjs` prints 28 pairs, both themes, **0 fails**. Rules:
text ≥ 4.5:1, UI boundaries ≥ 3:1. Selected row is tested as its own background, not the surface.

| Pair | Dark | Light |
|---|---|---|
| body `Fg` on `Surface` | 16.55 | 17.79 |
| secondary `Fg55` on `Surface` | 7.85 | 5.98 |
| meta `Fg48` on `Surface` | 5.66 | 5.16 |
| meta `Fg48` on `RowSelected` | 5.44 | 4.98 |
| `Fg48` on `App` (section label) | 6.19 | 4.82 |
| `AccentText` on `Surface` | 8.18 | 5.18 |
| `OnAccent` on `Accent` | 7.30 | 7.30 |
| `ControlLine` on `Surface` (≥3) | 3.64 | 3.50 |
| `SelectionRail` on `RowSelected` (≥3) | 5.89 | 4.68 |

The old light theme failed four of these (`Fg48` 2.52 on white, 2.25 on selection). That is what
"the contrast is very very bad" meant.

## 6. Motion (unchanged — still the frozen numbers)

Popup open 140 ms entrance, close 83 ms (0 ms on paste), hover/selection 100 ms, copy pulse 380 ms,
notification 150 ms fade. Never a layout animation; selection never moves geometry.

## 7. Behaviour changes that came with v3

| Change | Why | Status |
|---|---|---|
| List clamps to 3 lines (`ListLinesMax`) | 10-line rows turned the list into a document | owner to confirm |
| `previewLines` (1..100) now sizes the outside tooltip | keeps the frozen config range meaningful | owner to confirm |
| Per-row meta line removed | duplicated the bottom bar, added a 31 px block per row | owner to confirm |
| Dark zebra `#16181D` → `#101319` | 2.1:1 stripes read as a spreadsheet | shipped |
| Light palette warm-paper → neutral grey | warm Fg48 could not clear 4.5:1 | shipped |
| Light rail `Accent` → `AccentText` | pure orange is 2.3:1 on the pale selection | shipped |

## 8. How a change to this file is verified

1. `dotnet build -c Release` — 0 warnings.
2. `node %TEMP%\opencode\verify-theme.mjs` — 0 fails.
3. A fresh `[U]` agent reads its own screenshots against `CHECKLIST-uiux.md` §0 (one comparative
   row per surface). Builders never mark `[U]`.
