# Release visual re-check — Clip v2 (fresh pass 2)

**Reviewer:** a fresh visual agent, not the builder. **Date:** 25 Nov 2026.
**Build:** `UI\web\dist\index.html` (single file, no network), `file://`, commits **50207bd** (six fixes) + **1ced540** (tab strip).
**Method:** Playwright Chromium headless, 1× DPI, with 2×/4× crops for the toolbar and chips; 900×700, 840×700, 430×400 and 430×700.
Hooks used: `?mock=1000`, `?mock=1`, `?surface=settings`, `?surface=editor`, `?theme=dark`.
Every image cited below was taken and read in this pass: `%TEMP%\opencode\release-visual-2\`.
No app code was edited.

**Build drift:** the dist was rebuilt at **07:12:48** while this pass ran (the worktree is being edited concurrently; the rebuild converted `<>`, `1.` and `{}` to SVGs and clarified the delete sentence). Every measurement below was re-run on that build (`A1`–`A5`); the 06:51 shots are cited only where they show unchanged things. A later rebuild needs a re-run of §5.

**Scope note:** the row-contract conflict recorded in pass 1 (`CHECKLIST-uiux.md` / `DESIGN-v2-directives.md` §2 vs the shipped row) is unchanged and out of scope here.

---

## 1. The six rejected defects, re-checked

| # | Pass-1 defect | Status | Evidence |
|---|---------------|--------|----------|
| 1 | Settings did not fit 900×700 | **FIXED** | Body `scrollHeight 520 = clientHeight 520`, zero scroll, no fade; all five groups (General, Capture, Appearance, Time, Data) end at y=614.5 above the pinned footer at 622.5; dialog 860×657 at (20, 21.5). `20-settings-900-light/dark.png`. At 430×400 it is a 430×400 sheet: 263 px viewport for 696 px content, a 30 px fade + chevron present at rest, and wheel reaches `scrollTop 433` (max) where Time and Data are visible. `22-…-rest.png`, `23-…-bottom.png`. |
| 2 | Hover was a no-op | **FIXED** | `--row-hover` `#eaedf1` on `#fff` = **1.17:1** (light), `#212837` on `#141821` = **1.20:1** (dark). Clearly visible as a grey band with no ring, while the selected row keeps the peach fill + 1 px ring + 3 px rail. `10-states-crop-light/dark.png`. |
| 3 | Settings opened with focus behind the scrim; Tab leaked | **PARTIAL** | Trap is fixed: opens on `Close settings`, 18 consecutive Tabs all stay inside `.dialog`, Esc restores focus to the Settings button, `aria-modal="true"`. **But the focused control draws no ring on open** (`outline: none`; the first Tab does show 2 px `#c2410c`). See defect 5. |
| 4 | Index jumped at 100 | **FIXED** | 99 / 100 / 101 all: index box `x=35, w=30.98, right=65.98`, `text-align:right`, `tabular-nums`; `.row-content` starts at `x=75.98` on all three. `17-index-three.png`. Row 100 is the image row, so its text starts at 181.98 after the 96 px thumb — by design, the column does not move. |
| 5 | Toolbar mixed two icon systems; `1 formats` | **FIXED** | The five Unicode glyphs (`☑ ❝ ▦ +▤ •≡`) are now 16 px SVGs; the 07:12 rebuild also converted `<>`, `1.` and `{}`. 21 of 25 buttons are SVG, the other four are type labels (H1–H3, Clear, Single line). Footer reads `1 format`, and `1 character` / `35 characters` are both correct (live edit verified). `A1-toolbar.png`, `98-one-char.png`. |
| 6 | Three `+N` chips meant different things | **REGRESSED at ≤560 px** | At 900: `+1.8k chars` (80.5 px), `+1 file` (58.8 px), `+1 tag` (50.4 px) are distinguishable. At 430 `.chip-unit{display:none}` strips the words again: `+1.8k` (45.6 px), `+1` (27.6 px), `+1` (28.7 px). `91-chips-2x.png`, `92-chips-430-2x.png`, `95-tagchip-visible-900/430.png`. |
| + | Disabled settings explanation at 2.47:1 | **FIXED** | The 0.55 opacity is gone; label uses `--fg55`, explanation keeps `--fg55`: **6.83:1** light / **7.96:1** dark, and the switch is dimmed to 0.5 instead. |

Score: **4 fixed, 1 partial, 1 regressed below 560 px** (plus the disabled-row contrast flag fixed).

---

## 2. Seven-point verdict

| # | Item | Verdict | What I saw |
|---|------|---------|------------|
| 1 | Settings: 900×700 zero scroll, 430 sheet, 840–900 two columns | **PASS** | Zero scroll and every section visible at 900 and 840 (520/520); footer pinned; nothing clipped. At 430×400 the fade/chevron is visible at rest and all five groups are reachable by wheel. Columns are equal width (390/390 at 900, 364/364 at 840) with no orphaned labels. Caveat: a 157 px dead band under Time and a 10 px right-gutter asymmetry — defect 4. |
| 2 | Hover vs selected vs focus, both themes | **PASS** | Hover is a visible grey wash (1.17 / 1.20) with no ring; selected adds the peach fill, 1 px `#f2d8c9` ring and 3 px `#a93a0c` rail; the focused row shows its 2 px ring (`#c2410c` 5.18:1 light, `#ff9550` 8.18:1 dark). Selected still reads stronger than hover. `10-states-crop-*`. |
| 3 | Index 99 / 100 / 101 | **PASS** | Same 30.98 px box, same right edge 65.98, same content x 75.98. `17-index-three.png`. |
| 4 | Toolbar one family; character/format line | **PASS** | 25 buttons: 21 are SVG (16×16, one 12×12 `Aa`), 4 are text by convention (H1–H3, Clear, Single line); `<>`, `1.` and `{}` are drawn now, not typed. Reads `1 character [1 format]`, `35 characters [1 format]`. `A1-toolbar.png`, `98-one-char.png`, `42-editor-430.png`. |
| 5 | Three `+N` chips, incl. 430 | **FAIL** | All three are labelled at 900; at 430 the units are hidden and the old ambiguity is back (`+1.8k`, `+1`, `+1`). `92-chips-430-2x.png`, `95-tagchip-visible-430.png`. |
| 6 | Tabs: strip, counts, create + duplicate, delete cost, Move to tab, empty tab, both themes/sizes | **FAIL** | At 900 everything works and reads as product chrome: All 497 / Pinned 129 / Work 166 / Design 125 / Snippets 83 (three custom tabs in this build, not two); create, duplicate error, menu, the delete sentence (“Its 83 clips will be deleted, and this cannot be undone. Pinned clips are not affected.” — two lines, fits at 430), Move to tab list, and “Nothing in “Invoices” / Right-click a clip and move it here.” all legible. At 430 the New-tab `+` is off-screen at rest, “Snippets” is sliced mid-word, and the duplicate error is clipped mid-sentence. `50`, `51`, `53`, `54`, `56`, `58`, `60/61`, `70`, `72`, `81`, `96`. |
| 7 | Craft floor + contrast | **PASS** | DOM scan: 0 emoji-as-icons, 0 gradient text, 0 glass panels (only the unchanged 2 px modal scrim blur), 0 one-sided bars other than the frozen selection rail (`::before`, not matched by the scan); tabular numerals on index, time, counts; `::selection`, accent caret and 10 px scrollbar tokens present. Every new element clears 4.5:1 (§4). |

**Score: 5 PASS / 2 FAIL.** Both failures are confined to ≤560 px.

---

## 3. Measurements behind the verdicts

### 3.1 Settings

| Viewport | Dialog | Body client / scroll | Fade | All groups visible |
|---|---|---|---|---|
| 900×700 | 860×657 @ (20, 21.5) | 520 / 520 | no (correct) | yes, down to y=614.5; footer 622.5 |
| 840×700 | 808×657 @ (16, 21.5) | 520 / 520 | no | yes; columns 364 / 364 |
| 430×400 | 430×400 sheet | 263 / 696 | **yes, 30 px + chevron** | no at rest (correct); max scroll 433 shows Time + Data, fade then disappears |
| 430×700 | 430×700 sheet | 563 / 696 | yes | Data only below the fold |

At 900 the body has `clientWidth 848` but `offsetWidth 858`: `scrollbar-gutter: stable` reserves 10 px even with nothing to scroll, so the right column's content ends 10 px inside the left margin.

### 3.2 Row states

| Token | Light | Dark |
|---|---|---|
| hover fill / surface | `#eaedf1` / `#fff` — 1.17:1 | `#212837` / `#141821` — 1.20:1 |
| selected fill / ring / rail | `#fff6f1` / `#f2d8c9` / `#a93a0c` | `#1c2330` / `#334054` / `#ff9c6b` |
| focus outline | 2 px `#c2410c` — 5.18:1 | 2 px `#ff9550` — 8.18:1 |

Note: the hover *fill* is darker than the selected fill in both themes; selection is carried by the ring, the rail and the warm hue. It reads correctly, but do not remove the ring.

### 3.3 Tabs

- 900: strip 704.7×36, tabs + `+` end at x=518, then ~200 px of empty trough to the strip's right edge. Counts are 11 px pills; active tab `#1a0d02` on `#ff7a1a`.
- 430: strip `clientWidth 408`, `scrollWidth 504`. At rest `+` sits at x=486–512 (off-screen) and “Snippets” ends at 484. Scrolled to the end (96 px) the `+` appears at 396–422 but “Pinned” is then cut. No scrollbar or fade at rest.
- Inline create at 900: strip grows 36→47 px; input 150 px, error 167.9 px, both inside. At 430: the field spans 267→438.9 (strip ends 420) and the error's right edge 436.9 is clipped by 17 px.
- Delete confirm at 430: 398×159, the cost sentence wraps to two lines and fits.
- `aria-modal`: settings `true`; tab-delete confirm `null`; Ctrl+M picker `null`.

### 3.4 Toolbar / footer

25 buttons, 21 SVG at 16×16 (one 12×12 `Aa`), 4 text; stroke widths 1.4–1.7 px (Bold 1.7, Italic 1.6, rest 1.4–1.5). Footer: `35 characters` + chip `1 format`; live edit gives `1 character` + `1 format`.

### 3.5 `+N` chips

| Chip | Text @900 | Width @900 | Text @430 | Width @430 |
|---|---|---:|---|---:|
| remaining chars | `+1.8k chars` | 80.5 | `+1.8k` | 45.6 |
| more files | `+1 file` | 58.8 | `+1` | 27.6 |
| hidden tags | `+1 tag` | 50.4 | `+1` | 28.7 |

---

## 4. Contrast, computed (WCAG relative luminance from the rendered colours)

Thresholds: 4.5 for normal text, 3.0 for large; all entries below are small text (10–12 px).

| New element | Light | Dark | Result |
|---|---:|---:|---|
| Active tab label (`--on-accent` on `--accent`) | 7.30 | 7.30 | PASS |
| Active tab count (on 16–20% black over accent) | 5.26 | 4.82 | PASS |
| Inactive tab label | 6.65 | 7.31 | PASS |
| Inactive tab count (8% fg pill) | 5.66 | 5.91 | PASS |
| Tab strip hint (`↑↓ move Enter paste`) | 4.79 | 6.21 | PASS |
| Inline create field text | 17.96 | 14.74 | PASS |
| Inline duplicate error (10 px) | 6.40 | 5.85 | PASS |
| `+N chars` / `+N file` / `+N tag` chips | 6.15 | 6.88 | PASS |
| Tag chips | 6.15 | 6.88 | PASS |
| Delete confirm title | 17.96 | 14.74 | PASS |
| Delete confirm cost line | 6.83 | 7.96 | PASS |
| Delete confirm `Delete tab` | 6.57 | 6.37 | PASS |
| Context-menu item | 17.96 | 14.74 | PASS |
| Context-menu `Delete tab…` | 6.57 | 6.37 | PASS |
| Move menu subtitle / `current` | 5.14 | 5.67 | PASS |
| Settings disabled label / explanation (was 4.00 / 2.47) | **6.83 / 6.83** | **7.96 / 7.96** | PASS |
| Scroll fade chevron (`--fg48`) | 5.14 | 5.67 | PASS |

Non-text: focus ring 5.18 / 8.18; selection ring 1.36 / 1.69 is decorative and always paired with the 3 px rail (6.37 / 8.64); hover fills are surface cues, not text. The tightest new number is the dark active-tab count at **4.82**.

---

## 5. Remaining defects, ranked (max 5)

1. **`+N` chips lose their unit at ≤560 px, so the old ambiguity is back.** At 430: `+1.8k` / `+1` / `+1` (widths 45.6 / 27.6 / 28.7) against 80.5 / 58.8 / 50.4 at 900. Cause: `.chip-unit{display:none}` in the 560 px media query. **Fix:** delete that rule — the provenance line already ellipsizes the app·title, and Image + Files + `+1 tag` do fit at 430 (`95-tagchip-visible-430.png`). If the unit must shrink, use `ch`/`file`/`tag` (one word), never bare `+1`.
2. **At 430 the tab strip hides New tab and slices a tab.** Strip 408 px client vs 504 px content; `+` at 486–512 off-screen at rest; “Snippets” cut at 420 with no scrollbar or fade. Fix: make `.tab-add` `position: sticky; right: 0` with `background: var(--raised)` (and a 6 px left inset shadow), or hide `.tab-count` under 480 so five tabs plus `+` fit in 408 px.
3. **The duplicate-tab error is clipped at 430.** Error right edge 436.9 vs strip right 420 (17 px hidden; reads “…already e”), field flush to the strip border. Cause: the edit effect scrolls the 150 px input into view, not the 172 px field + error. **Fix:** scroll the wrapper instead — `inputRef.current?.parentElement?.scrollIntoView({ block: 'nearest', inline: 'nearest' })`.
4. **Settings two-column is even in width but not in density or margins (840–900).** TIME holds 53 px of controls in a 210 px stretched row (157 px empty box) and DATA starts 171 px below those controls; the 10 px `scrollbar-gutter` with no scrollbar leaves the right column 10 px inside the left margin. **Fix:** `scrollbar-gutter: auto` at ≥840 (`stable` only for the sheet), and let the right column stack independently (Appearance → Time → Data) so no group is stretched and both columns end within ~60 px of each other.
5. **The dialogs open with an invisible focused control, and the two new ones lack `aria-modal`.** Settings: focus is on Close (trap correct) with `outline: none`; the tab-delete confirm: focus on Cancel, `outline: none`; the Ctrl+M picker: focus on “All history”, `outline: none` until the first arrow. `aria-modal` is `true` on Settings but `null` on the confirm and the picker. **Fix:** give dialog initial focus the same 2 px ring (a `.dialog :focus` rule scoped to dialog content), and add `aria-modal="true"` to both `Dialog.Content`s.

Not ranked, one line each: the 900 tab strip leaves a ~200 px empty trough after `+` (it looks like an unfinished field — `width: fit-content` would close it); a custom tab's card title reads “Invoices clips 0”; the delete confirm's destructive button is a borderless red text button, the least prominent control in its own dialog.

---

## 6. Would I ship this UI tonight?

**Yes at 900×700 — the release window — and no at 430 until three small things are fixed.** Every fix from 50207bd lands at the size the owner actually uses: Settings fits with zero scroll and shows every section, hover is finally visible without competing with selection, the index column is rock-stable, the toolbar is one icon family with correct singular/plural copy, and the three `+N` chips are labelled and contrast-checked. The tab strip in 1ced540 genuinely reads as part of the product at 900 — same 36 px height, raised surface, accent active state and count pills — and its flows (create, duplicate rejection, delete cost, Move to tab, empty tab) are honest and complete. What still fails is the 430 px pass, and all three causes are one-liners: keep the chip units, keep `+` visible in the strip, and scroll the whole inline field into view so the error is not cut. Fix those (plus the dialog initial-focus ring and `aria-modal`, which cost one CSS rule and two attributes), and I would ship tonight at both sizes.
