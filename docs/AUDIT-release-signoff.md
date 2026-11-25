# Release signoff — Clip v2 UI

**Reviewer:** fresh visual agent, not the builder. **Date:** 25 Nov 2026.
**Build:** `UI\web\dist\index.html`, SHA-256 `CC028DCB…55A730D9`, mtime 07:36:20.
**Exe check:** `dist\Clip.exe` = **208,187,846 bytes**, mtime 07:38:27. A UTF-8 byte scan of the exe contains `more characters`, `cannot be undone` and `No text found in this image`. The page under test is the page in the exe.
**Method:** Playwright Chromium (headless, 1× DPI), `file://`, hooks `?mock=1`, `?mock=1000`, `?surface=settings`, `?surface=editor`, `?theme=dark`. Every image cited was taken and read in this pass: `%TEMP%\opencode\release-signoff\`.
No app code was edited. This file and the screenshots are the only outputs.

**Prior passes:** `AUDIT-release-visual.md`, `AUDIT-release-visual-2.md`; fixes in `50207bd`, `141ec9e`, `8125132`. Verified live, not from the commits.

---

## 1. Ten-point result

| # | Check | Verdict | What I observed |
|---|-------|---------|-----------------|
| 1 | 430 px: three chip forms distinguishable | **PASS** | `+1.8k chars` 80.5 px, `+1 tag` 50.4 px, `+1 file` 58.8 px — identical at 900 and 430 because the units are no longer hidden. Read in `tagchip-430.png` (“Image · Files · +1 tag”) and `main-430-light/dark.png`. |
| 2 | 430 px: tab strip `+` fully inside at rest | **PASS** | Strip 10–420; `+` at **390–416** in a 430 viewport, 4 px inside the strip. `strip-430-light.png`, `strip-430-dark.png`. No overflow: `scrollWidth == clientWidth` 408. |
| 3 | 430 px: long tab name ellipsized, not sliced; duplicate error wrapped | **PASS** | 22-char name “Quarterly invoices Mar” creates a 66 px tab; `.tab-label` is 22 px visible / 132 px content with `text-overflow: ellipsis` — renders **“Q…”**, not a sliced word (`longtab-430.png`). Duplicate `Work`: error **150×24 px, 2 lines**, bottom 141 vs strip bottom 145; 24-char error also 2 lines, same fit (`dup-strip-430.png`, `longerr-430.png`). |
| 4 | Dialog initial focus ring visible on open | **PASS** | Settings opens on Close with `outline: 2px #c2410c` (dark `#ff9550`); Delete-tab confirm on Cancel with the ring; Move picker on “All history” with the ring. No Tab needed. `settings-900-light/dark.png`, `dialog-confirm-focus.png`, `dialog-picker-focus.png`. |
| 5 | Settings 900×700: zero scroll, every section, TIME balanced, no phantom gutter | **PASS** | Dialog 860×657 at (20, 21.5); body **520/520**, all five groups end by 614.5, footer at 622.5. TIME radios are **193.5 + 8 + 193.5 px** (full width, no stretched empty box). `offsetWidth 858 == clientWidth 858`; gutter is `auto`, no 10 px phantom. Lists every section in both themes. |
| 6 | Settings 430×400 sheet: visible fade, every section reachable | **PASS** | Sheet 430×400 at (0, 0); body 263/696. A **30 px fade** is present at rest and absent at max scroll 433, where TIME (188–241) and DATA (255–337) are both visible above the footer. `settings-430x400-light-rest.png`, `settings-430x400-dark-bottom.png`. |
| 7 | Dialogs: aria-modal, 20-Tab trap, Esc restores opener | **PASS** | `aria-modal="true"` on Settings, Delete-tab confirm and Move picker. 20 consecutive Tabs: **0 escapes** in all three. Esc returns focus to the Settings button, the “Work” tab, and the selected row respectively — each with its ring. |
| 8 | Row contract and rhythm at 900 | **PASS** | Index **99 / 100 / 101**: box `x=35, w=30.98, right=65.98`, `text-align:right`, `tabular-nums`; `.row-content` at **75.98** for all. Heights: row **01 = 65**, row **02 = 245**, image row **100 = 263**. `.prov-time` is **107/107 px** at 900 and 430 — never truncates. Tags max two + fold: row 05 = `Image`, `Files`, `+1 tag`. `row01-900.png`, `row02-900.png`, `row100-ocr.png`. |
| 9 | Craft floor | **PASS** | 20 toolbar SVGs + 5 intentional type labels (H1–H3, Clear, Single line); 0 emoji-as-icons; 0 gradient text; 0 backdrop-filter panels; tabular numerals on index/time/counts/chips; `::selection` and accent caret (`#c2410c` / `#ff9550`), 10 px list / 6 px strip scrollbars; hover `#eaedf1` / `#212837` is visible but weaker than selected (peach fill + ring + 3 px `#a93a0c` rail); the selected row shows a 2 px `#c2410c` ring, offset −2, when keyboard-focused (`focus-selected-900.png`, `hover-900-light.png`, `toolbar-900.png`). |
| 10 | No regression: empty, no-results, tag hint, OCR, files, editor, export, move | **PASS** | Empty tab: “Nothing in “Empty” / Right-click a clip and move it here.” (`state-empty-tab-900.png`). Search: “Nothing matches “zzzz” / Try a different search, or clear it with Esc.” (`search-noresults.png`). Unknown-tag hint: “no tag named “word” — searched as text” (`search-unknown-tag-hint.png`). OCR row 100: “Text read from image”, 263 px (`row100-ocr.png`). Files: `migration.sql`, `README.md`, `+1 file` (`row-files.png`). Editor clean/dirty/discard all present (`editor-clean/dirty/discard-900.png`). Export menu: “Save as… / Text (.txt) / Markdown (.md)” (`state-export-menu.png`). Move picker lists All history / Work / Design / Snippets / Pinned with “current” and `↑↓ choose · Enter move · Esc close` (`dialog-picker-focus.png`). |

**10 PASS / 0 FAIL** against the brief. Two caveats are ranked below: the tag-hint result set is dishonest (pre-existing, not a regression) and one long-tab state is technically ellipsized but shows only one character.

---

## 2. Contrast (computed from rendered colours, both themes)

Threshold 4.5 for normal text; all pairs below are 10–14 px. Selected-row body/provenance/time are measured on the selected fill, the harder case.

| Pair | Light | Dark | Result |
|---|---:|---:|---|
| Body text, selected row (`--fg`) | 16.85 | 13.08 | PASS |
| Provenance app/title (`--fg55`) | 6.41 | 7.07 | PASS |
| Time / index (`--fg48`) | 4.82 | 5.03 | PASS |
| `+1.8k chars` chip | 6.15 | 6.88 | PASS |
| `+1 file` chip | 6.15 | 6.88 | PASS |
| `+1 tag` chip | 6.15 | 6.88 | PASS |
| Inline duplicate error (`--danger`, 10 px) | 6.40 | 5.85 | PASS |
| Active tab label (`--on-accent` on `--accent`) | 7.30 | 7.30 | PASS |
| Active tab count | 5.26 | 4.82 | PASS |
| Inactive tab label | 6.65 | 7.31 | PASS |
| Dialog title | 17.96 | 14.74 | PASS |
| Dialog body text | 6.83 | 7.96 | PASS |
| Disabled setting explanation | 6.83 | 7.96 | PASS |
| Toast title / sub | 17.96 / 6.83 | 14.74 / 7.96 | PASS |

Tightest text pair in the release surface: dark active-tab count **4.82**. Focus ring 5.18 / 8.18. Hover is a surface cue (1.17 / 1.20), never the only selection cue.

---

## 3. Remaining defects, ranked (max 4)

1. **The unknown-tag fallback lies about what it searched.** `zzzz:` shows the hint “no tag named “zzzz” — searched as text” but displays **all** clips: 497 shown at `?mock=1`, **500 of 1,000** at `?mock=1000`, while `zzzz` without the colon correctly shows 0. The parser drops the unknown `tag:` token instead of adding its name to the text terms, so an empty term list matches everything (capped at 500). **Fix:** when the token is `name:` and the tag is unknown, push `name` into the text-term list as well (`i.push(name); r.push(name)` in the mock parser and the same rule in `SearchService`), or change the hint to “tag ignored” and keep the full list deliberately. This is not introduced by the three fixes — the previous build showed the same 500/1,000 — but it is wrong on screen and would embarrass a demo.
2. **At 430 a long tab label collapses to one character.** “Quarterly invoices Mar” renders **“Q…”**: 22 px of label beside the active count, because six 66 px tabs plus `+` fill the 408 px strip. It is ellipsized, not sliced, so the acceptance wording passes; it is still unusable. **Fix:** let the active tab keep up to the existing 132 px cap and let `.tabs-scroll` scroll (`+` already sits outside the scroller and stays visible), or hide the active tab count under 560 so the label gets ~46 px.
3. **Settings’ right column leaves a 173 px hole at 900.** TIME ends at 361.5, DATA starts at 534.5; the right column ends 614.5 while the left ends 520.5. The two columns share grid rows, so the short TIME group inherits the tall CAPTURE row. **Fix:** stack each column independently (`align-content: start` with an 18 px gap, or two flex columns) so DATA follows TIME.
4. **The 900 px tab strip keeps a ~240 px empty trough.** Last tab ends at 450.3; `+` sits at 690.7–716.7. It reads as an unfinished input. **Fix:** `width: fit-content` on the strip (keeping `+` after the last tab) or let the strip stop at the tabs. Cosmetic; the `+` is discoverable.

Not ranked: inactive tab counts disappear at 430 while the active count stays (deliberate space rule); the active-tab count makes the new “Empty 0” tab look like a badge with no tab (correct data); the mock search cap is 500 (fixture only).

---

## 4. Owner line

**Owner would accept this as the release UI** — the 900×700 release window and the 430 px sheet are complete in both themes, every defect from the two previous passes is verified fixed, and no text falls below 4.5:1; the remaining issues are a misleading unknown-tag result set, one over-compressed tab label and two cosmetic spacing nits, none of which blocks release.
