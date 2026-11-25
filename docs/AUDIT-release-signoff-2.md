# Release signoff 2 — G-02, G-04 and the Ctrl+T tag picker

**Reviewer:** fresh visual agent, not the builder. **Date:** 25 Nov 2026.
**Build:** `UI\web\dist\index.html`, 2,862,374 bytes, SHA-256 `D17D6535…1580F0`, mtime 08:05:44.
**Exe check:** `dist\Clip.exe` = **208,187,846 bytes**, mtime 08:08:16. A UTF-8 byte scan of the exe contains `Filter by tag` (2), `This clip has no tags` (1) and `Clear tag filter` (1). The page under test is the page in the exe.
**Method:** Playwright Chromium (headless, DPR 1), `file://`, hooks `?mock=1000`, `?mock=real`, `?surface=settings`, `?theme=dark`. Every image cited below was taken and read in this pass: `%TEMP%\opencode\release-signoff-2\`. No app code was edited; this file and the screenshots are the only outputs.

**Prior state:** G-02 and G-04 were returned to NOT RUN after the styles changed; the Ctrl+T picker is new in this build. Everything here was re-observed against the current file, not inherited from `AUDIT-release-signoff.md`.

---

## 1. Verdict

| Row | Verdict |
|---|---|
| **G-02 — list surface** | **PASS** |
| **G-04 — settings surface** | **PASS** |
| Ctrl+T tag picker (new) | **PASS** |

---

## 2. G-02 — list surface at 900, light and dark

| Check | Evidence |
|---|---|
| One-line row directly above a ten-line row | Row **01 = 65 px, 1 line**; row **02 = 245 px**, text box 200 px = **10 lines** at `line-height 20`, content 640 px, clamped with the trailing ellipsis (`needed for the file....`). Identical geometry in both themes. `list-top-900-light.png`, `list-top-900-dark.png` |
| Index column 01 / 99 / 100 | 98, 99, 100, 101 all share box **x 35 → 66, w 31**, `text-align:right`, `font-variant-numeric: tabular-nums`, 12 px, `scrollWidth == clientWidth`. Text column starts at **76**; 100 is the image row so its text starts at 182 after the thumbnail. No collision, no reflow. `index-99-100-900-light.png`, `index-99-100-900-dark.png` |
| Hover vs selected vs focused on one screen | Hover row 03 `#EAEDF1` / `#212837`; selected row 04 peach `#FFF6F1` / `#1C2330` plus the **3 px rail** `#A93A0C` / `#FF9C6B`; focus **2 px inset** `#C2410C` / `#FF9580`, `:focus-visible = true`. All three readable together in `state-threeway-900-light.png`; ring alone in `state-focus-900-light/dark.png`; mouse-selected without ring in `state-selected-900-light.png`. |
| Three `+N` chips | `+1.8k chars` (row 02, facts chip), `+1 file` (rows 04, 09, 11), `+1 tag` (row 10). Each stays on **one line**, tabular numerals, 6.15:1 light / 6.88:1 dark. `chips-plusN-900-light.png`, `list-top-900-light.png` |
| Tag chips | `Link`, `Spreadsheet`, `Files`, `Image`, `Files` — 11 px, 6 px radius, 6.15 / 6.88:1, max two plus a `+N` fold on row 10 (`Image · Files · +1 tag`). |
| Image row with OCR text | Row **100 = 263 px**: thumbnail, `Text read from image`, the OCR body, `Figma · Screenshot 2026-11-24 143012.png`, `+997 chars`. Image with no text (row 05) honestly reads `No text found in this image`. `index-99-100-900-light.png`, `image-files-rows-900-light.png` |
| Files row | Row 04: `board-minutes.docx`, `action-items.xlsx`, `+1 file`, meta `Chrome · board-minutes.docx and 2 more`, tag `Files` (105 px). Same shape at row 09 (`Slack · migration.sql and 2 more`). |
| Empty list | Pinned 0: `Nothing pinned` / `Press Ctrl+P on a clip to keep it here.` Centred, header still `Pinned clips 0 · 0 shown`. `empty-light.png`, `empty-dark.png` |
| No results | `Nothing matches “zzzzqqq”` / `Try a different search, or clear it with Esc.`; header `Filtered`, footer `Results for “zzzzqqq” · 0 found`. `no-results-900-light.png`, `no-results-900-dark.png` |
| Time never truncating | `.prov-time` **107 px**, `scrollWidth == clientWidth` at 900 **and** at 430 (rows 01–05); tabular numerals. `list-430-light.png` |

Row rhythm at 900: 65 / 245 / 85 / 105 / 105 with 1 px hairlines; the index is always on the first line of the row, including the 3-line files row and the image rows. No chip wraps, no row clips its own content.

**One measured note, not new:** the hover *fill* is stronger than the selected *fill* (hover 1.17:1 light / 1.20:1 dark vs selected 1.07 / 1.13 vs the page). Selection is carried by the 3 px rail (**6.37 / 7.67**) and the focus ring (**4.86 / 7.25**), so the states are unmistakable and the hierarchy holds; the fill values are unchanged from the previously signed build. This is listed as defect 3 only so the next person knows where the margin is.

---

## 3. G-04 — settings surface

### 900 × 700 (both themes)

- Dialog **860 × 563 at (20, 69)**; body 561/561, **not scrollable** (content fits), footer **576 → 631**, panel bottom 632 — **pinned**.
- **Two independent stacks**, 395 px + 28 px gutter + 395 px:
  - left — General 152–344, Capture 358–568;
  - right — Appearance 152–259, **Time 273–324**, **Data 338–418**.
- **TIME → DATA gap = 14 px**, the same as Appearance → Time (**14 px**). No dead column: all five groups are populated and on screen.
- **Nothing clipped:** the last Capture row ends at 568, eight px above the footer. `Capture` includes History limit, Toast lines and Plain text only in full.
- `settings-900x700-light.png`, `settings-900x700-dark.png`.
- Note: the right stack ends 150 px above the left (Data bottom 418 vs Capture 568). That is imbalance, not a dead column — both columns carry content and the footer is 158 px lower. No action.

### 430 × 400 — sheet (both themes)

- Sheet **430 × 400 at (0, 0)**; header 0–80; body **263 px** (696 px of content, max scroll **433**); footer **343–400**.
- **Fade:** `.scroll-fade`, 30 px at y = 313, `linear-gradient(rgba(0,0,0,0), panel-bg 78%)` with the chevron; visibly dims `History limit` at rest. `settings-430x400-light.png`, `settings-430x400-dark.png`.
- **Every group reachable by wheel:** 3 wheel steps → `scrollTop 433`; Appearance, Time and Data all on screen; Data ends at 337, six px above the footer. `settings-430x400-light-scrolled.png`, `settings-430x400-*-bottom.png`. At max scroll the fade sits over empty space, not over text.

---

## 4. Ctrl+T tag picker (new), both themes

| Step | Observed |
|---|---|
| Open on a tagged clip (row 01, `Link`) | Dialog **Filter by tag**, subtitle `Chrome · API reference`, one item `link:` with the 2 px focus ring (5.18:1 light / 8.18:1 dark). `picker-open-900-light.png`, `picker-open-900-dark.png` |
| Apply a tag | Enter → search field becomes `link:`, header `Filtered`, `1 shown`, footer `Results for “link:” · 1 found`; list holds only the tagged clip. `picker-applied-900-light.png`, `-dark.png` |
| Open on an untagged clip (row 06) | **`This clip has no tags.`**, zero items, initial focus falls to Cancel. `picker-untagged-900-light.png`, `-dark.png` |
| Esc returns focus to the row | Focus lands on the originating row (`class="row selected"`, index 01 or 06) with the visible 2 px ring; dialog gone. `picker-esc-focus-900-light.png`, `picker-esc-untagged-900-*.png` |

The item text `link:` is the tag **query token** (`${tag}:` in the source, `code:` for language tags); applying writes that token into search. It is deliberate and coherent with the search field, but see defect 1.

---

## 5. Contrast (computed from rendered colours)

| Pair | Light | Dark |
|---|---:|---:|
| Picker title / item | 17.96 | 14.74 |
| Picker subtitle | 6.83 | 7.96 |
| Picker hint (`↑↓ choose · Enter filter · Esc close`) | 5.14 | 5.67 |
| Picker Cancel | 6.83 | 7.96 |
| Picker focus ring | 5.18 | 8.18 |
| Settings group label (`GENERAL`, 11 px 600) | 5.14 | 5.67 |
| Settings description (12 px) | 6.83 | 7.96 |
| Settings selected radio | 16.47 | 7.31 (unselected) |
| Settings footer Cancel | 6.83 | 7.96 |
| List body / index / time / chips | 16.85 / 4.82 / 4.82 / 6.15 | 13.08 / 5.03 / 5.03 / 6.88 |

All new picker pairs and both settings columns clear 4.5:1; the tightest is the picker hint at 5.14 (11 px).

**Tabular numerals:** index, time and every chip are `tabular-nums`. Settings values (`200`, `3`) are 11 px `normal` — single values, no column to align, so nothing regressed. **Focus ring:** visible on rows, the picker item and Cancel. **Hover vs selected:** distinct in every state; see the note in §2.

---

## 6. Remaining defects (max 3, none blocking)

1. **Tag picker shows the raw query token `link:` instead of the tag name.** `hP` renders the same string that is written into search, so the menu reads like `link:` under a heading `Filter by tag` while the row chip reads `Link`. Low: applying works and the search box explains the token. Fix by labelling the item `Link` and keeping the `link:` token internal, or by showing `Tag: Link`.
2. **Settings 900 leaves ~150 px of empty lower-right panel** (right stack ends 418, left 568). Not a dead column and not clipped; pure balance. Optional: move Data under Time only when the panel is taller, or let the stacks stretch.
3. **Hover fill is stronger than selected fill** (1.17 / 1.20 vs 1.07 / 1.13). Selection is still unmistakable via the 3 px rail and the ring, and these values are unchanged from the signed build; flagged so the rail is never removed without rebalancing the fills.

---

**G-02 and G-04 are signed** — every row-contract and settings-surface item was observed on fresh screenshots in both themes at 900 and 430, the TIME→DATA gap is 14 px, the 430 sheet fades and scrolls to every group, and the new Ctrl+T picker opens, applies, explains an untagged clip and restores focus on Esc; the three items above are cosmetic and non-blocking.
