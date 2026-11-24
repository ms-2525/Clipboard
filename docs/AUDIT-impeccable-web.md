# Audit — Impeccable, Clip v2 web UI

Date: 25 Nov 2026. Worktree `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack`, branch `v2-phase1`.
Scope: `UI\web\` — list, editor overlay, settings dialog, toast, both themes, browser surfaces.
Method: the built app (`dotnet build -c Release` → `bin\Release\net10.0-windows\Clip.exe --v2 --db %TEMP%\opencode\phase1-db\clip.db`, real 38-clip owner DB), WebView2 CDP on 127.0.0.1:9222, computed styles read from the live DOM at 900×700 CSS / 1.5 DPR, WCAG ratios computed from the rendered `rgb()` values. Every state was screenshotted through CDP and the screenshots read back. No source-only guesses.

---

## Audit Health Score

| # | Dimension | Score | Key finding |
|---|---|---|---|
| 1 | Accessibility | 3/4 | AA text met after fixes; keyboard focus and selection can diverge on rows; 20 px toast target |
| 2 | Performance | 3/4 | Virtualised list, lazy thumbnails, single 826 KB file; TipTap ships in the initial bundle |
| 3 | Theming | 4/4 | One token layer, both themes end-to-end, zero hard-coded UI colour outside the token blocks |
| 4 | Responsive Design | 3/4 | Breakpoints exist; this audit exercised the shipping 900×700 window only; 22 px switch |
| 5 | Implementation Integrity | 3/4 | Detector clean in both modes, coherent Clip-specific system; committed Inter is not shipped |
| **Total** | | **16/20** | **Good** |

## Implementation Integrity verdict

**PASS after fixes.** The implementation is a coherent, product-specific system: the row anatomy (identity tile, 3-line clamp, right-aligned facts), the tab strip, the provenance footer and the in-document editor overlay are Clip's, not a template's. The detector is clean on the live page (`[]`) and on the changed files (`[]`).

The integrity problem found was real: the web token block had drifted from `docs\DESIGN-language.md`. The doc claims `OnAccent on Accent = 7.30:1`; the web rendered **3.66:1** because the web had quietly replaced Accent `#FF7A1A` with `#C2410C` while keeping the dark ink. The doc claims `ControlLine on Surface = 3.50/3.64`; the web rendered **1.40/1.46**. Restoring the committed Accent/OnAccent/ControlLine values fixed both and made the doc's table true for the web again. The remaining integrity gap is typography: the design world says Inter 400/500/600, the web render is Segoe UI (the Inter TTFs exist in `Assets\Fonts` but the v2 shell only maps `UI\web\dist`; they are not served). See P2-5.

## Executive summary

- **Score:** 16/20 (Good).
- **Issues:** P0 0 · P1 4 (all fixed) · P2 5 (4 fixed, 1 not) · P3 5 (none fixed; 3 accepted, 2 deferred).
- **Top issues before the fixes:** primary fill text 3.66:1; active-tab count 2.53:1; every control boundary 1.40–1.60:1; dark focus ring 2.96:1 on the field; disabled labels 2.93/1.99:1; 10 px functional text; unthemed selection, caret and editor scrollbar.
- **After:** 62 unique rendered light pairs and 62 dark pairs, **0 failures**, lowest light 4.62, lowest dark 4.82.
- **Next:** serve Inter (or formally re-bless Segoe UI) in the type slice; fix row focus→selection for keyboard users.

## Detailed findings by severity

### P1 — Major (all fixed)

**[P1-1] Primary fill text is 3.66:1**
- Location: `styles.css` tokens `--accent` / `--on-accent`; active tab (`button.tab`), active tool, brand mark.
- Category: Accessibility / Theming.
- Impact: the selected tab label is the only locator of the current tab; 12 px text at 3.66 fails AA.
- WCAG: 1.4.3 Contrast (Minimum).
- Fix: restore the committed `Accent #FF7A1A`, `OnAccent #1A0D02` (7.30:1). Files: `styles.css`.

**[P1-2] Active-tab count is 2.53:1**
- Location: `.tab-count` (`opacity: 0.85` stacked on an 8%-foreground chip over the accent fill).
- Category: Accessibility.
- Impact: "All 38" count is unreadable on the selected tab; it was the lowest text pair in the app.
- WCAG: 1.4.3.
- Fix: remove the opacity, move the active count background to `--count-bg` (black 16% / 20%). After: 5.26:1.

**[P1-3] Control boundaries are 1.40:1 (light) / 1.46:1 (dark)**
- Location: `--control` used by `.icon-btn`, `.tabs`, `.btn`, `.switch` (off), `.slider-track`, `.slider-thumb`, `.radio-card` (off), scrollbar thumb.
- Category: Accessibility / Theming.
- Impact: secondary buttons, the off switch and slider tracks have no perceivable edge on `Surface`; the off switch reads as empty space.
- WCAG: 1.4.11 Non-text Contrast.
- Fix: committed `ControlLine #7E8A9B` / `#667284` → 3.50:1 / 3.64:1. One token fixed 20+ controls.

**[P1-4] Dark focus ring is 2.96:1 on the field**
- Location: focus rings used `--accent` (`#C2410C`), which is 2.96:1 on `--field #1F2532` and 3.43:1 on `Surface`.
- Category: Accessibility.
- Impact: keyboard focus on the search field and inputs is below the 3:1 indicator floor.
- WCAG: 1.4.11 / 2.4.11.
- Fix: focus rings now use `--accent-text` — 7.06:1 on the dark field, 8.18:1 on the dark surface, 4.66/5.18 in light. `.row` uses `outline-offset: -2px` because the list clips outside the row box.

### P2 — Minor

**[P2-1] Disabled labels and keycaps are 2.93:1 / 1.99:1** (fixed)
- Location: `.btn:disabled` (opacity 0.45) — "Edit clip" in the empty search state, "Save clip", "Save changes".
- Category: Accessibility / States.
- Impact: inactive controls are exempt from 1.4.3, but the F2 hint at 1.99 is not readable even as a hint.
- Fix: explicit disabled tokens (`--fg48` on the surface, hairline border, transparent fill, muted keycap). After: 5.14:1 light / 5.67:1 dark for both label and keycap.

**[P2-2] Functional text at 10 px** (fixed)
- Location: detector found `.brand-sub`, `.keycap`, `.tab-count`, `.chip`, `.tabs-hint` keycaps, `.t-micro`; the editor meta tile also used inline 10 px.
- Category: Legibility.
- Impact: below the 11 px functional floor; worst at 150 % DPI.
- Fix: all to 11 px with matching line heights. Values after: keycap 6.15/6.88, chip 6.15/6.88, inactive tab count 6.65, micro label 5.14/5.67.

**[P2-3] Browser surfaces were unthemed** (fixed)
- Location: no global `::selection`; no `caret-color`; `.editor-scroll` had no scrollbar rule (Chromium default, visibly different from the list).
- Category: Theming / Craft floor.
- Impact: text selection in the editor was the browser's blue; the editor scrollbar broke the theme in dark.
- Fix: global `::selection` (`--selection-bg`, 13.26:1 light / 10.64:1 dark text), `caret-color: var(--accent-text)`, and one shared scrollbar rule for `.list`, `.dialog-body`, `.editor-scroll` with `width/height 10px`, control-colour thumb, transparent track and `background-clip: padding-box`.

**[P2-4] Empty-state hierarchy is inverted** (fixed)
- Location: `.empty` set `color: --fg48`; `.t-sec` then overrode the subtitle to the brighter `--fg55` in dark.
- Category: Visual hierarchy / Theming.
- Impact: "No clips match" looked dimmer than its own instruction line.
- Fix: removed the colour from `.empty`; the title inherits `--fg` and the subtitle stays `--fg55`.

**[P2-5] Committed face is not shipped** (not fixed)
- Location: `styles.css` body stack; `Assets\Fonts\Inter-*.ttf`; v2 shell maps only `UI\web\dist`.
- Category: Theming / Implementation Integrity.
- Impact: the design world says Inter 400/500/600; the render is Segoe UI Variable Text. Metrics and voice therefore differ from the WPF reference.
- Reason not fixed: serving the TTFs adds ~1.25 MB, requires copying `Assets\Fonts` into the web root and changes every line metric — a typography slice, not this colour pass. Fix: copy the three Inter weights into `UI\web\public\fonts`, add `@font-face`, and re-run this contrast/overflow pass.

### P3 — Polish (deferred)

**[P3-1] Keyboard focus can diverge from selection** (not fixed)
- Location: `ClipList.tsx` rows have `tabIndex={0}` but no `onFocus`; the global Enter handler copies `selected` state.
- Impact: Tab to the fourth row, press Enter — the first/selected row is copied, not the focused one.
- Fix: select on focus, or make Enter read the focused row. A logic change, outside the class-names-only rule.

**[P3-2] Checked switch track is 2.57:1 against the light dialog** (accepted)
- The on-state is also carried by the 7.30:1 knob and its position; the track fill alone is not the only state signal, and the WPF world specifies Accent for the on track. Keep.

**[P3-3] Selection ring is 1.28:1 (light) / 1.50:1 (dark)** (accepted)
- The ring is decorative; selection is carried by the rail (5.98 / 7.67), the `RowSelected` surface and the 3 px rail. The frozen selection treatment is preserved as instructed.

**[P3-4] Toast dismiss is 20×20** (not fixed)
- Desktop pointer target; WCAG 2.5.8 applies with a spacing exception. Control sizing is layout work for the next slice.

**[P3-5] Editor content has no focus ring** (accepted)
- `.tiptap` is `outline: none`; the caret (now accent-coloured, 7.06:1 on the dark field) and autofocus indicate editing. A ring on every editor open would add noise.

## Patterns & systemic issues

1. **The web token block was hand-derived from the WPF doc and drifted at exactly the places the doc had measured.** Accent, OnAccent and ControlLine were all replaced by quieter values; the doc's "computed, not guessed" table no longer described the web build. This is the systemic cause of 3 of the 4 P1s.
2. **The 11 px floor was not applied to the micro ramp.** Five roles sat at 10 px because the doc allows a 10 px micro-label and a 10–11 px chip; the detector's floor is 11. One size pass fixed all nine detector findings.
3. **Browser surfaces were skipped as a group** — selection, caret, editor scrollbar. The list had a custom scrollbar, which made the editor's default one look accidental.
4. **State styling used opacity instead of tokens** (`:disabled` 0.45, tab count 0.85), producing ratios that were never in any table.

## Positive findings

- One four-level surface ladder and one accent pair; adjusting three tokens fixed the tab, primary button, tool, switch, radio-card state, brand mark and focus system.
- The frozen row selection is intact: `RowSelected` surface + 1 px ring + 3 px rail, no accent wash, no geometry shift.
- Tabular numerals are present on every time/count role (`.t-meta`, `.tab-count`, `.chip`, mono readouts).
- Virtualised list (10 mounted rows), lazy thumbnails, real owner data — no placeholder content.
- Links use `text-underline-offset: 2px` in both preview and editor.
- Focus is visible on search (2 px inset ring), tabs, rows, toolbar buttons, dialog buttons, switches, sliders, radio cards, popover items and colour swatches.
- `prefers-reduced-motion` now reduces the entrance fades/transitions without removing state changes.

## Verification

- `dotnet build -c Release`: **0 warnings / 0 errors**.
- `tsc --noEmit`: **0 errors**.
- Detector live: `detect --cdp 9222 --target clip.local --json` → **`[]`**.
- Detector changed: `detect --json --changed` → **`[]`** (first pass flagged the blockquote side-tab and the dead `Inter` name; both resolved — see false positives).
- Console: after a full page reload, `Runtime.consoleAPICalled`, `Runtime.exceptionThrown` and `Log.entryAdded` (error/warning) → **0** (the favicon 404 was real and is fixed with a `data:` icon).
- States screenshotted and read: light/dark list, hover, selected, image row, empty search, toast, editor open, dirty, colour popover, settings, settings scrolled, focus rings, editor text selection.
- 900×700 CSS at 1.5 DPR, the shipping window.

### False positives and limitations

- **10 px text findings were not false positives** — they were real and are fixed.
- **Changed-scan "overused font" (Inter)** was a committed-world conflict, not a defect: the design authority pins Inter. The honest resolution was to remove the name from the stack because Inter is not installed or served (the stack was silently falling back to Segoe), and report the real gap as P2-5. Removing the dead name changes no rendered pixel.
- **Changed-scan "side-tab" on the editor blockquote** was real per the craft floor; changed from a 3 px left rule to a full 1 px `--band-rule` border. This is the one visual change beyond colour/contrast in the editor.
- **Active tab/count failures while the editor or dialog was open** were a scanner artifact: the covered list is still in the DOM. Panel scans were re-run scoped to `.editor-overlay` / `.dialog`.
- **Ghost button border 1.18:1** in my boundary probe is `transparent` by design; ghost buttons are identified by their label.
- **Selection ring 1.28/1.50** is decorative, not the state marker (rail 5.98/7.67).
- **Dialog scrollbar** was not visually exercised because the settings content fits at 900×700; it shares the verified list/editor rule. No horizontal scrollbar is reachable (`overflow-x: hidden`), but both axes are styled.

## What I changed

| File | Token / rule | Before → after |
|---|---|---|
| `styles.css` | `--accent` | `#C2410C` → `#FF7A1A` (committed Accent) |
| `styles.css` | `--on-accent` | `#1C0D05` → `#1A0D02`; ink on accent **3.66 → 7.30** |
| `styles.css` | `--accent-text` | light `#A93A0C` → `#C2410C` (6.37 → 5.18); dark `#FF9C6B` → `#FF9550` (8.64 → 8.18) |
| `styles.css` | `--control` | light `#D5DAE2` → `#7E8A9B` (**1.40 → 3.50**); dark `#2C3648` → `#667284` (**1.46 → 3.64**) |
| `styles.css` | focus rings | `--accent` → `--accent-text`; dark field **2.96 → 7.06**, dark surface 3.43 → 8.18 |
| `styles.css` | `--selection-bg` (new) | light `#F8D7C3`, dark `#4A2A1A`; text on selection **13.26 / 10.64** |
| `styles.css` | `--band`, `--band-fg`, `--band-strong`, `--band-rule` (new) | replaces 4 literals; blockquote 18.13–19.43 unchanged |
| `styles.css` | `--scrim`, `--tip-bg`, `--tip-fg`, `--tip-line`, `--count-bg`, `--highlight` (new) | removes the remaining UI literals; dark tooltip now raised |
| `styles.css` | `.tab-count` | opacity 0.85 removed; 10 → 11 px; **2.53 → 5.26** |
| `styles.css` | disabled `.btn`, `.keycap` | **2.93/1.99 → 5.14** light; **3.84/2.53 → 5.67** dark |
| `styles.css` | `.brand-sub`, `.keycap`, `.chip`, `.tab-count`, `.t-micro` | 10 → 11 px; editor meta tile 10 → 11 px |
| `styles.css` | `.empty` | removed `color: --fg48`; title now `--fg` |
| `styles.css` | `::selection`, caret, scrollbars | added global selection tokens, `caret-color`, shared themed scrollbar for editor |
| `styles.css` | focus-visible set | added switch, radio card, popover item, colour; switch/radio offset 2 px; row offset −2 px |
| `styles.css` | blockquote | `border-left: 3px` → full `1px` `--band-rule` border (detector side-tab) |
| `icons.tsx` | `IconHighlight` underline | `#C2410C` → `currentColor` |
| `EditorOverlay.tsx` | ink/highlight | theme literals → `var(--fg)` / `var(--highlight)`; unused `theme` prop removed; tile 11 px |
| `App.tsx` | editor call | removed the now-unused `theme` prop only |
| `index.html` | favicon | `<link rel="icon" href="data:,">` — kills the only console error |
| `styles.css` | reduced motion | added `prefers-reduced-motion` duration reduction |

## Before → after: every failing pair

| Context | Pair | Before | Req. | After |
|---|---|---|---|---|
| Light + dark, active tab | `#1C0D05` on `#C2410C` | **3.66 FAIL** | 4.5 | 7.30 PASS (`#1A0D02` on `#FF7A1A`) |
| Light + dark, active tab count | `#301306` on `#A3370A` (0.85 opacity) | **2.53 FAIL** | 4.5 | 5.26 PASS (`#1A0D02` on `#D66616`) |
| Light, disabled button (empty search / editor / settings) | `#939598` on `#FBFCFD` | **2.93 FAIL** | 4.5 (exempt) | 5.14 PASS |
| Light, disabled F2 keycap | `#939598`-blended on `#FBFCFD` | **1.99 FAIL** | 4.5 (exempt) | 5.14 PASS |
| Dark, disabled button | `#777B84` on `#1B202B` | **3.84 FAIL** | 4.5 (exempt) | 5.67 PASS |
| Dark, disabled F2 keycap | blended on raised | **2.53 FAIL** | 4.5 (exempt) | 5.67 PASS |
| Light, all control borders | `#D5DAE2` on `#FFFFFF` | **1.40 FAIL** | 3.0 | 3.50 PASS |
| Dark, all control borders | `#2C3648` on `#141821` | **1.46 FAIL** | 3.0 | 3.64 PASS |
| Dark, focus ring on field | `#C2410C` on `#1F2532` | **2.96 FAIL** | 3.0 | 7.06 PASS |
| Both, scrollbar thumb | control on surface | **1.40 / 1.46 FAIL** | 3.0 | 3.50 / 3.64 PASS |
| Both, text selection | browser default (not themed) | n/a | — | fg on selection 13.26 / 10.64 |

## Appendix A — computed text pairs, light theme

All 62 unique foreground/background pairs rendered across the list, editor, dirty banner, popover and settings; all sizes are 11–15 px, so the requirement is 4.5:1.

| Selector | fg | bg | px | ratio | result |
|---|---|---|---|---|---|
| input::placeholder | #666e7c | #f1f3f6 | 13 | 4.62 | PASS |
| span.chip.accent | #c2410c | #fff3ea | 11 | 4.75 | PASS |
| div.brand-sub | #666e7c | #f6f7f9 | 11 | 4.79 | PASS |
| span.tabs-hint.t-meta | #666e7c | #f6f7f9 | 11 | 4.79 | PASS |
| span.t-meta | #666e7c | #fff6f1 | 11 | 4.82 | PASS |
| span.t-meta | #666e7c | #fbfcfd | 11 | 5 | PASS |
| span.t-meta.shown-count | #666e7c | #ffffff | 11 | 5.14 | PASS |
| span.t-meta | #666e7c | #ffffff | 11 | 5.14 | PASS |
| span.t-meta.sel | #666e7c | #ffffff | 11 | 5.14 | PASS |
| span.hint | #666e7c | #ffffff | 11 | 5.14 | PASS |
| button.btn.sm | #666e7c | #ffffff | 12 | 5.14 | PASS |
| span.keycap.edit-key | #666e7c | #ffffff | 11 | 5.14 | PASS |
| div.t-meta.editor-sub | #666e7c | #ffffff | 11 | 5.14 | PASS |
| button.btn | #666e7c | #ffffff | 12 | 5.14 | PASS |
| span.hint.editor-foot-hint | #666e7c | #ffffff | 11 | 5.14 | PASS |
| div.t-micro | #666e7c | #ffffff | 11 | 5.14 | PASS |
| a | #c2410c | #ffffff | 15 | 5.18 | PASS |
| span.tab-count | #1a0d02 | #d66616 | 11 | 5.26 | PASS |
| span | #8a5a00 | #fff7e6 | 12 | 5.56 | PASS |
| span.banner-sub | #8a5a00 | #fff7e6 | 12 | 5.56 | PASS |
| button.btn.ghost.sm | #8a5a00 | #fff7e6 | 12 | 5.56 | PASS |
| span.keycap.search-key | #545b68 | #f1f3f6 | 11 | 6.15 | PASS |
| span.keycap | #545b68 | #f1f3f6 | 11 | 6.15 | PASS |
| span.chip | #545b68 | #f1f3f6 | 11 | 6.15 | PASS |
| span.keycap.edit-key | #545b68 | #f1f3f6 | 11 | 6.15 | PASS |
| div.tile | #545b68 | #f1f3f6 | 11 | 6.15 | PASS |
| span.chip.editor-chip | #545b68 | #f1f3f6 | 11 | 6.15 | PASS |
| span.tile | #545b68 | #f1f3f6 | 11 | 6.15 | PASS |
| div.preview | #545b68 | #fff6f1 | 12 | 6.41 | PASS |
| button.tab | #545b68 | #fbfcfd | 12 | 6.65 | PASS |
| span.tab-count | #545b68 | #fbfcfd | 11 | 6.65 | PASS |
| div.preview | #545b68 | #fbfcfd | 12 | 6.65 | PASS |
| button.radio-card | #545b68 | #fbfcfd | 12 | 6.65 | PASS |
| div.preview | #545b68 | #ffffff | 12 | 6.83 | PASS |
| strong | #545b68 | #ffffff | 11 | 6.83 | PASS |
| div.t-sec | #545b68 | #ffffff | 12 | 6.83 | PASS |
| div.toast-sub | #545b68 | #ffffff | 11 | 6.83 | PASS |
| button.btn.ghost | #545b68 | #ffffff | 12 | 6.83 | PASS |
| span.size-readout | #545b68 | #ffffff | 11 | 6.83 | PASS |
| button.tool.text | #545b68 | #ffffff | 12 | 6.83 | PASS |
| span.t-sec | #545b68 | #ffffff | 12 | 6.83 | PASS |
| p.t-sec | #545b68 | #ffffff | 12 | 6.83 | PASS |
| span.set-value | #545b68 | #ffffff | 11 | 6.83 | PASS |
| button.tab | #1a0d02 | #ff7a1a | 12 | 7.3 | PASS |
| button.radio-card | #14171c | #fff3ea | 12 | 16.47 | PASS |
| div.brand-name | #14171c | #f6f7f9 | 16 | 16.76 | PASS |
| span | #14171c | #fff6f1 | 14 | 16.85 | PASS |
| button.btn.sm | #14171c | #fbfcfd | 12 | 17.49 | PASS |
| span | #14171c | #fbfcfd | 14 | 17.49 | PASS |
| span.t-title | #14171c | #ffffff | 15 | 17.96 | PASS |
| span | #14171c | #ffffff | 14 | 17.96 | PASS |
| div.t-title | #14171c | #ffffff | 15 | 17.96 | PASS |
| div.toast-title | #14171c | #ffffff | 12 | 17.96 | PASS |
| div.editor-title | #14171c | #ffffff | 14 | 17.96 | PASS |
| strong | #14171c | #ffffff | 15 | 17.96 | PASS |
| em | #14171c | #ffffff | 15 | 17.96 | PASS |
| p | #14171c | #ffffff | 15 | 17.96 | PASS |
| button.pop-item | #14171c | #ffffff | 12 | 17.96 | PASS |
| h2.t-title | #14171c | #ffffff | 15 | 17.96 | PASS |
| div.label | #14171c | #ffffff | 13 | 17.96 | PASS |
| p | #f6f7f9 | #0b0d12 | 15 | 18.13 | PASS |
| strong | #ffffff | #0b0d12 | 15 | 19.43 | PASS |

## Appendix B — computed text pairs, dark theme

| Selector | fg | bg | px | ratio | result |
|---|---|---|---|---|---|
| span.tab-count | #1a0d02 | #cc6215 | 11 | 4.82 | PASS |
| input::placeholder | #8a92a1 | #1f2532 | 13 | 4.9 | PASS |
| span.t-meta | #8a92a1 | #1c2330 | 11 | 5.03 | PASS |
| span.t-meta | #8a92a1 | #1b202b | 11 | 5.21 | PASS |
| span.t-meta.shown-count | #8a92a1 | #141821 | 11 | 5.67 | PASS |
| span.t-meta | #8a92a1 | #141821 | 11 | 5.67 | PASS |
| span.t-meta.sel | #8a92a1 | #141821 | 11 | 5.67 | PASS |
| span.hint | #8a92a1 | #141821 | 11 | 5.67 | PASS |
| button.btn.sm | #8a92a1 | #141821 | 12 | 5.67 | PASS |
| span.keycap.edit-key | #8a92a1 | #141821 | 11 | 5.67 | PASS |
| div.t-micro | #8a92a1 | #141821 | 11 | 5.67 | PASS |
| button.btn | #8a92a1 | #141821 | 12 | 5.67 | PASS |
| div.t-meta.editor-sub | #8a92a1 | #141821 | 11 | 5.67 | PASS |
| span.hint.editor-foot-hint | #8a92a1 | #141821 | 11 | 5.67 | PASS |
| div.brand-sub | #8a92a1 | #0b0d12 | 11 | 6.21 | PASS |
| span.tabs-hint.t-meta | #8a92a1 | #0b0d12 | 11 | 6.21 | PASS |
| span.keycap.search-key | #a7aebb | #1f2532 | 11 | 6.88 | PASS |
| span.keycap | #a7aebb | #1f2532 | 11 | 6.88 | PASS |
| span.chip | #a7aebb | #1f2532 | 11 | 6.88 | PASS |
| span.keycap.edit-key | #a7aebb | #1f2532 | 11 | 6.88 | PASS |
| div.tile | #a7aebb | #1f2532 | 11 | 6.88 | PASS |
| span.chip.editor-chip | #a7aebb | #1f2532 | 11 | 6.88 | PASS |
| span.tile | #a7aebb | #1f2532 | 11 | 6.88 | PASS |
| div.preview | #a7aebb | #1c2330 | 12 | 7.07 | PASS |
| button.tab | #1a0d02 | #ff7a1a | 12 | 7.3 | PASS |
| button.tab | #a7aebb | #1b202b | 12 | 7.31 | PASS |
| span.tab-count | #a7aebb | #1b202b | 11 | 7.31 | PASS |
| div.preview | #a7aebb | #1b202b | 12 | 7.31 | PASS |
| button.radio-card | #a7aebb | #1b202b | 12 | 7.31 | PASS |
| span.chip.accent | #ff9550 | #2a1a12 | 11 | 7.7 | PASS |
| div.preview | #a7aebb | #141821 | 12 | 7.96 | PASS |
| strong | #a7aebb | #141821 | 11 | 7.96 | PASS |
| div.t-sec | #a7aebb | #141821 | 12 | 7.96 | PASS |
| div.toast-sub | #a7aebb | #141821 | 11 | 7.96 | PASS |
| p.t-sec | #a7aebb | #141821 | 12 | 7.96 | PASS |
| span.set-value | #a7aebb | #141821 | 11 | 7.96 | PASS |
| button.btn.ghost | #a7aebb | #141821 | 12 | 7.96 | PASS |
| span.size-readout | #a7aebb | #141821 | 11 | 7.96 | PASS |
| button.tool.text | #a7aebb | #141821 | 12 | 7.96 | PASS |
| span.t-sec | #a7aebb | #141821 | 12 | 7.96 | PASS |
| a | #ff9550 | #141821 | 15 | 8.18 | PASS |
| span | #e8b45a | #241e10 | 12 | 8.76 | PASS |
| span.banner-sub | #e8b45a | #241e10 | 12 | 8.76 | PASS |
| button.btn.ghost.sm | #e8b45a | #241e10 | 12 | 8.76 | PASS |
| span | #e7eaf0 | #1c2330 | 14 | 13.08 | PASS |
| button.btn.sm | #e7eaf0 | #1b202b | 12 | 13.53 | PASS |
| span | #e7eaf0 | #1b202b | 14 | 13.53 | PASS |
| button.radio-card | #e7eaf0 | #2a1a12 | 12 | 13.89 | PASS |
| span.t-title | #e7eaf0 | #141821 | 15 | 14.74 | PASS |
| span | #e7eaf0 | #141821 | 14 | 14.74 | PASS |
| div.t-title | #e7eaf0 | #141821 | 15 | 14.74 | PASS |
| div.toast-title | #e7eaf0 | #141821 | 12 | 14.74 | PASS |
| h2.t-title | #e7eaf0 | #141821 | 15 | 14.74 | PASS |
| div.label | #e7eaf0 | #141821 | 13 | 14.74 | PASS |
| div.editor-title | #e7eaf0 | #141821 | 14 | 14.74 | PASS |
| strong | #e7eaf0 | #141821 | 15 | 14.74 | PASS |
| em | #e7eaf0 | #141821 | 15 | 14.74 | PASS |
| p | #e7eaf0 | #141821 | 15 | 14.74 | PASS |
| button.pop-item | #e7eaf0 | #141821 | 12 | 14.74 | PASS |
| div.brand-name | #e7eaf0 | #0b0d12 | 16 | 16.13 | PASS |
| p | #f6f7f9 | #0b0d12 | 15 | 18.13 | PASS |
| strong | #ffffff | #0b0d12 | 15 | 19.43 | PASS |

## Appendix C — non-text and browser-surface measurements (light / dark)

| Element / surface | Pair | Ratio | Required | Result |
|---|---|---|---|---|
| Icon button border | `--control` on app | 3.27 / 3.98 | 3.0 | PASS |
| Tabs container border | `--control` on app | 3.27 / 3.98 | 3.0 | PASS |
| Secondary button border | `--control` on surface | 3.50 / 3.64 | 3.0 | PASS |
| Switch (off) border | `--control` on surface | 3.50 / 3.64 | 3.0 | PASS |
| Slider track border | `--control` on surface | 3.50 / 3.64 | 3.0 | PASS |
| Radio card (off) border | `--control` on surface | 3.50 / 3.64 | 3.0 | PASS |
| Radio card (on) border | `--accent-text` on surface | 5.18 / 8.18 | 3.0 | PASS |
| Switch knob (on) | `--on-accent` on accent | 7.30 / 7.30 | 3.0 | PASS |
| Selection rail | `--rail` on row-selected | 5.98 / 7.67 | 3.0 | PASS |
| Selection ring (decorative) | `--selection-ring` on row-selected | 1.28 / 1.50 | — | intentional |
| Focus ring | `--accent-text` on surface / field | 5.18–4.66 / 8.18–7.06 | 3.0 | PASS |
| Caret | `--accent-text` on field | 4.66 / 7.06 | 3.0 | PASS |
| Selection text | `--fg` on `--selection-bg` | 13.26 / 10.64 | 4.5 | PASS |
| Scrollbar thumb | `--control` on surface | 3.50 / 3.64 | 3.0 | PASS |
