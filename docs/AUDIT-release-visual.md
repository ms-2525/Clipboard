# Release visual audit — Clip v2

**Reviewer:** fresh visual agent (not the builder). **Date:** 25 Nov 2026.
**Build:** `UI\web\dist\index.html` (single file, no network), loaded from `file://`.
**Method:** Playwright Chromium headless, 1× DPI, 900×700 (the real window) and 430×700.
`?mock=1000`, `?mock=real`, `?mock=1`, `?theme=dark`, `?surface=settings`, `?surface=editor`.
Screenshots: `%TEMP%\opencode\release-visual\` (all images cited below are mine, read after capture).
No app code was edited.

**Evidence correction:** the brief's `?toast=1` hook does not exist in this build. `App.tsx` reads only
`mock`, `theme`, `surface`; the only toast path is a real action. I captured toasts by double-clicking
row 01 (copy) and by saving/editing where applicable. The screenshots, not the brief's hook list, are what
this report is based on.

**Contract note:** `docs\CHECKLIST-uiux.md` still describes an older row (28×28 identity tile, 3-line
clamp, no index, provenance in the bottom bar). `docs\DESIGN-v2-directives.md` §2 also says bottom-bar
provenance, and §8 asks for CopyQ-style zebra rows. This build has none of those: it has the index,
10-line clamp, in-row `app · title · time` and tags described in the brief. I judged against the brief's
row contract. If §2/§8 are still live, this is a spec conflict to settle before release — not a taste call.

---

## 1. Ten-point verdict

| # | Item | Verdict | What I saw / why |
|---|------|---------|------------------|
| 1 | First impression, light, 900×700 | **PASS** | Product, not demo. Hierarchy is immediate: selected row → body → provenance → chrome. Type scale is deliberate: 14/20 body, 11/16 provenance, 12/16 controls and tabs, 15/22 card title, 16/22 brand; the 13 px search sits between by choice, not accident. Weakest element: the 48 px card-header band, which repeats `1,000` four times on one screen (`All 1,000`, `All clips 1,000`, `1,000 shown`, footer `1,000 clips`). |
| 2 | First impression, dark, 900×700 | **PASS** | Same structure survives. Body is `#e7eaf0` on `#141821` (14.74:1), card edge is visible, orange rail/tab still pop, no muddy “washed” surfaces. The 10-line row still dominates but remains readable. |
| 3 | Owner's row contract | **FAIL** | Index, 10 lines, provenance and tags are all present and the time never truncates, **but the index column grows from 2ch to 3ch at displayed 100**. Measured on one scroll: displayed 99 index box 16 px wide, right edge 51; displayed 100 is 23 px wide, right edge 58. Every cell to the right shifts 7 px. See `57-index-99-100.png`, `50-index-crop.png`. |
| 4 | List scan states | **FAIL** | Selected (peach fill + 1 px ring + 3 px rail), keyboard focus (2 px orange ring, `10-focus.png`), Pinned (`11-pinned.png`), image with OCR (`Text read from image`), image without OCR (`No text found in this image`), files (mono paths, folder ellipsizes, filename stays), empty tab, no-results, unknown-tag hint, toast and export menu are all legible and coherent. **Hover is effectively invisible**: `#fbfcfd` on `#ffffff` in light (1.02:1 between surfaces) and `#1b202b` on `#141821` in dark (~1.10:1). I cannot see the hover in `08-hover.png`; only the pointer reveals it. |
| 5 | Overlays: settings, editor | **FAIL** | Editor clean / dirty / discard are strong in both themes (`Editing clip` + dot, amber `Unsaved changes` banner, `Discard changes?`, `Revert edits`; `1 formats` is a copy bug). **Settings does not fit the real window**: at 900×700 the dialog is 520×668, the body is 517 px tall for 720 px of content. The zoom row is sliced through its middle by the footer (`51-settings-cut-crop.png`); `TIME` (time format) and `DATA` (Clear history) are entirely below the fold. At 430×700 the body is 519 px for 800 px; the initial view ends at the `APPEARANCE` heading, so theme/zoom/time/clear are invisible (`32-430-settings-light.png`). The body has no visible scrollbar/gutter in the capture (offsetWidth == clientWidth; overlay scrollbars) and no fade/cue. Also: opening Settings leaves keyboard focus on the Settings button behind the scrim; the first four Tab stops are the background tab strip, Edit, Save as and the selected row. |
| 6 | Density and rhythm, 900 | **PASS** | 65 px for the one-line row with a tag is right: 10 px padding + 20 px line + 4 px gap + 20 px tag line + 10 px padding + 1 px border. The 4 px above a tagless row is the tag chip, not a layout bug. The 10-line row is 245 px; the image OCR row (displayed 100) is 263 px. Mixed heights still read as a list because the index gutter, provenance line and divider repeat on every row. Weak spot: the 96×60 thumbnail is orphaned in a 263 px image row (200 px of empty column under it); consider a 160×100 thumb or a shorter OCR clamp for image rows. |
| 7 | 430 px: list | **PASS** | No horizontal overflow (`documentElement.scrollWidth` 430 = client 430). Content is kept; only chrome is dropped: `brand-sub`, `shown-count`, `tabs-hint`, `search-key`, `footer-right` are `display:none`. The time cells are all 107 px, `scrollWidth == clientWidth`, so the time never truncates; long provenance ellipsizes instead. `30-430-light.png` / `31-430-dark.png`. |
| 8 | 430 px: settings and editor | **FAIL** | Editor is acceptable: toolbar wraps to three rows (`+ ↶ ↷ Clear Single line` on row 3) and all content stays reachable; dirty banner collapses to one line but still says `Unsaved changes` / `Revert edits` (`36-430-editor-dirty.png`). Settings is not: only GENERAL, CAPTURE and the `APPEARANCE` heading are visible at 430; theme, zoom, time format and Clear history require an invisible scroll. This is not graceful degradation, it is hiding controls. |
| 9 | Craft floor | **FAIL** | Pass: no gradient text, no glass panels (only a 2 px blur on the modal scrim), no one-sided bar except the frozen selection rail, tabular numerals on times/counts/index, themed scrollbar/selection/caret tokens (`10px` scrollbar, `::selection`, accent caret), and every focusable control I tabbed showed the 2 px accent ring or the search container's focus border. Fail: the editor toolbar mixes drawn SVGs with Unicode glyphs — `☑` task list, `❝` quote, `▦` table, `+▤` add row, `•≡` bullet list (`55-toolbar-crop.png`). The task checkbox and quote do not match the stroke family; the quote looks like two characters. Tag chips are deliberately `tabIndex=-1`, so filters remain mouse-only. The modal focus trap failure is below. Scrollbar CSS exists but the thumb is not visible at rest in this engine, so I can only confirm the token, not the visual. |
| 10 | Contrast spot-check, both themes | **FAIL** | All requested text pairs pass except the disabled settings explanation: light 2.47:1 for `The v2 build always starts in the tray...` and 4.00:1 for its label (row at 0.55 opacity); dark 3.28:1 / 5.26:1. WCAG exempts disabled controls, but that sentence is the only explanation of a forced setting and is not a control. Everything else is 4.62–17.96. Full table below. |

**Score: 4 PASS / 6 FAIL.**

---

## 2. Row contract evidence

| Check | Measured / seen | Verdict |
|---|---|---|
| Index number | `01`, `02`, `99`, `100`, `101` exist | PASS |
| 1-line row | Row 01: 65 px, `Chrome · API reference`, `25 Nov 2026, 05:49`, `Link` (`03-row1-line.png`) | PASS |
| 10-line ceiling | Row 02: 245 px; exactly 10 text lines + provenance; text starts `Here is the revised statement of work...`, ends `needed for the file....` (`04-row10-line.png`) | PASS (the four-dot cut is data + clamp ellipsis, not a line-11 leak) |
| Image OCR row | Displayed 100: 263 px; 96×60 thumb, `Text read from image`, 10 lines of OCR text, `Figma · Screenshot ...` (`05b-index-99-100.png`) | PASS, with floating-thumbnail note |
| Provenance | Every row: `app · title` left, date/time right, e.g. `Excel · Q3 forecast` / `25 Nov 2026, 05:23` | PASS |
| Tags | `Link`, `Spreadsheet`, `Files`, `Image`, fold `+1` | PASS |
| Time never truncates | `.prov-time` is `flex-shrink:0`; at 430 every cell is 107 px client and scroll width; row 03 shows `25 Nov 2026, 05:24` beside `Spreadsheet` in full (`30-430-light.png`) | PASS |
| Index aligns at displayed 99/100 | 99: box 16 px, right 51; 100: box 23 px, right 58; row body shifts with it. Cause: `.row-index { min-width: 2ch }` (`57-index-99-100.png`) | **FAIL** |

Fix: fixed `width: 4ch` (the 1,000-row fixture reaches index 1000) with `min-width` removed and `text-align:right`.

---

## 3. Ranked visual defects (max 6)

1. **Settings does not fit the owner's window.** At 900×700, 203 px of settings content is below the body fold; the zoom segmented control is cut horizontally by the footer (`51-settings-cut-crop.png`), and `TIME`/`DATA` are absent from the first view. At 430×700, 281 px is hidden and only the `APPEARANCE` heading shows. There is no visible scrollbar or fade. **Fix:** compress the vertical rhythm (18 px group gaps / 7 px row padding / 20 px head), or split into two columns at ≥720 px; at ≤500 px use a full-height sheet with a sticky section index. Add `scrollbar-gutter: stable` and an always-visible 10 px thumb (or a bottom fade + “more” cue) to `.dialog-body`.
2. **Row hover is a no-op.** `--raised` (`#fbfcfd`) against `--surface` (`#ffffff`) is 1.02:1; dark `#1b202b` against `#141821` is ~1.10:1. The row under the pointer looks identical to the rest (`08-hover.png`). **Fix:** hover fill `--field` (`#f1f3f6` / `#1f2532`), or an inset 1 px `--hairline` plus a 4% `--fg` wash; keep selected fill + rail distinct.
3. **Settings opens without focus and leaks Tab to the page behind it.** `Dialog.Content` has `onOpenAutoFocus={(e) => e.preventDefault()}`. After clicking Settings, activeElement is still the Settings button; Tab goes `tab All → Edit clip → Save as → row selected` (all behind the scrim) before reaching `Close settings`. The DOM has `role="dialog"` but no `aria-modal` in the capture. For a keyboard-first clipboard this is a real defect. **Fix:** remove the preventDefault (focus the close button or Save changes), keep the Radix trap, set `aria-modal="true"`, and add a 20-Tab regression test that never leaves `.dialog`.
4. **The index column jumps at 100.** `.row-index { min-width: 2ch }` grows from 16 px to 23 px; every row's content shifts 7 px, and the numbers' right edges do not line up (99 right = 51, 100 right = 58). **Fix:** fixed `width: 4ch` (covers index 1000) with `text-align:right`; test 9 / 99 / 100 / 1000.
5. **The editor toolbar is two icon systems.** Drawn SVG for bold/italic/underline/strike/code/link/palette/highlight vs Unicode text for `☑`, `❝`, `▦`, `+▤`, `•≡` (`55-toolbar-crop.png`). The checkbox and quote read as characters, not icons, and the weights do not match. The editor footer also says `1 formats` (and `1 characters` would appear for a one-character clip). **Fix:** replace those five with 16 px stroke SVGs from the same family; pluralize `format/formats` and `character/characters`.
6. **Three different `+N` meanings wear the same chip.** `+1` in a files body = one more file (`rich.tsx:305`), `+1.8k`/`+997` in provenance = remaining characters, `+1` after tags = hidden tags. In a scan the owner cannot tell which is which without hovering. **Fix:** visible labels — `+1 file`, `+1.8k chars`, `+1 tag` — or distinct chip styles; keep the tooltips.

Not ranked, but worth a line: the 12 px pin marker reads as a small orange asterisk at 1× (`56-pin-crop.png`); in the Pinned tab every row is pinned, so it is noise. The disabled `Edit clip` button remains in the empty-tab header (dead control); hide it when there is no selection.

---

## 4. Contrast table (computed, not eyeballed)

WCAG relative luminance from computed colours; selected-row values use the selected fill (`#fff6f1` / `#1c2330`). Body threshold 4.5, large 3.0.

| Pair | Size | Light | Dark | Result |
|---|---|---:|---:|---|
| Body text (`--fg` on `--surface`) | 14 px | 17.96 | 14.74 | PASS |
| Body text on selected row | 14 px | 16.85 | 13.08 | PASS |
| Provenance app/title (`--fg55`) | 11 px | 6.83 | 7.96 | PASS |
| Provenance app/title on selected row | 11 px | 6.41 | 7.07 | PASS |
| Time / index (`--fg48`) | 11–12 px | 5.14 | 5.67 | PASS |
| Time / index on selected row | 11–12 px | 4.82 | 5.03 | PASS |
| Tag chips (`--fg55` on `--badge`) | 11 px | 6.15 | 6.88 | PASS |
| OCR marker (`Text read from image`) | 11 px | 5.14 | 5.67 | PASS |
| OCR placeholder (`No text found…`, `--fg55`) | 14 px | 6.83 | 7.96 | PASS |
| Toast title | 12 px | 17.96 | 14.74 | PASS |
| Toast sub | 11 px | 6.83 | 7.96 | PASS |
| Toast check icon (`--accent-text` on `--accent-soft`) | 13 px icon | 4.75 | 7.70 | PASS |
| Footer/help muted (`--fg48`) | 11 px | 5.14 | 5.67 | PASS |
| Search placeholder (`--fg48` on `--field`) | 13 px | 4.62 | 4.90 | PASS |
| Active tab (`--on-accent` on `--accent`) | 12 px | 7.30 | 7.30 | PASS |
| Amber banner ink | 12 px | 5.56 | 8.76 | PASS |
| Danger text (`--danger`) | 12 px | 6.57 | 6.37 | PASS |
| **Disabled row label** (`Start hidden in the tray`, 0.55 opacity) | 13 px | **4.00** | 5.26 | **FLAG** |
| **Disabled row explanation** (0.55 opacity) | 12 px | **2.47** | **3.28** | **FLAG** |

The disabled row is technically exempt under WCAG 1.4.3 because it is inactive; the explanation is not a control, though, and at 2.47 it is genuinely hard to read. Raise the row opacity to ~0.75 or use a dedicated `--muted` token. Non-text notes: control borders are 3.50:1 light / 3.34:1 dark (≥3.0 for UI components); focus ring is 5.18:1 light / 8.18:1 dark.

---

## 5. Would I ship tonight?

**Yes — the list is release-quality — but not the Settings dialog as it stands.** The core surface does the owner's job: 10 lines, in-row provenance, tags, a stable 65/245 px rhythm, honest empty/search/unknown-tag states, a clean editor with a real dirty/discard flow, and both themes holding at 430 px without horizontal overflow. The one thing I would change first is **Settings at 900×700**: it hides the time-format control and Clear history behind an invisible scroll and cuts the zoom row in half, which is a feature-discovery failure rather than polish. The hover fix is a five-minute follow-up (one token), and the focus trap and index column should go in the same patch. I would not ship the current Settings as-is; with that dialog made to fit and show its overflow, I would ship this UI tonight.
