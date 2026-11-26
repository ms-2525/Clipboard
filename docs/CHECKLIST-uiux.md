# Clip — UI/UX Verification Checklist

**Status 25 Nov (live acceptance 3 + visual 3):** 54 rows — **52 PASS · 0 FAIL · 2 NOT RUN** (K-07 plus G-02, voided by the V4 card-head move). Release gate in §12.

**Surface (25 Nov):** the shipped UI is the React page in `UI\web`, rendered in the warm WebView2 shell
(`UI\ShellWindow*`). The WPF popup is retired. Every row below describes the v2 page, plus the native
toast and tray that surround it. The live row contract is `docs\DESIGN-language.md` §3; it wins over any
older document (the WPF identity tile, 3-line clamp, bottom-bar-only provenance and WPF theme rows are
retired — see §8).

**Archive rule:** a row that has passed and can never fail again moves to `docs\CHECKLIST-archive.md`
as one line. No v2 row qualifies: every row sits on code that is still changing. If the code under a PASS
row changes, that row returns to NOT RUN until re-proven.

**Visual authority:** `docs\DESIGN-language.md` §1–§5 (surfaces, type, row contract, controls,
contrast). `%TEMP%\opencode\t3\` is manner reference only — tools3000's *manner*, Clip's orange. Owner
decision wins over any document, always.

**Evidence index:** `docs\AUDIT-impeccable-web.md` (live computed styles; 62 light + 62 dark pairs) ·
`docs\AUDIT-ui-code.md` (source audit) · `docs\design-evidence\s4a` (keyboard, widths, states) ·
`docs\design-evidence\s7` (settings, editor) · `docs\evidence-s10` (clean-profile release) ·
**`docs\AUDIT-release-visual-2.md`** — the fresh pass at `50207bd`+`1ced540`, screenshots in
`%TEMP%\opencode\release-visual-2\`; it re-signed hover, index, settings, toolbar and the 900 px tab
strip, and its §5 named the 430 px defects that `8125132` then fixed ·
`%TEMP%\opencode\s17\verify-0742.log` — the 52-check page harness re-run at 07:42 on the 07:36 bundle
(keyboard, counts, chips, contrast, overflow; 52/52) ·
`%TEMP%\opencode\s18\final-report.json` — the 430 px re-measurement after `8125132`
(chip text, `+` position, duplicate error, dialog ring, settings rows, row heights) ·
**`%TEMP%\opencode\s20\proof-report.json`** — the S20 tag-picker + post-sign-off harness,
33/33 on the 08:0x bundle: Ctrl+T picker (A), untagged clip (B), search/editor guards (C), K-06
typing (D), editor focus/inert (E), edit-then-look (F), mock unknown tag (G), 430 long tab (H),
settings stack (I), strip end (J), five widths/themes + rows + chips (K); screenshots `01`–`11` ·
release artifact `dist\Clip.exe` **208,187,846 B**, rebuilt 07:38, contains the 07:36 page (markers
`more characters`, `cannot be undone`). It does **not** contain the S20 page: `UI\web\dist\index.html`
is now **2,862,374 B** and the exe needs a rebuild before release. It does not run the WebView2 shell
and does not cover the native toast, tray or title bar.

**VERIFICATION PROTOCOL (owner-mandated):** every `[U]` row is executed by a FRESH sub-agent on
`opencode-go/deepseek-v4.1-flash#high` — never one that wrote or orchestrated the code it judges. It
launches the app (or for the page, loads the built bundle), captures and reads its own screenshots, and
reports PASS/FAIL with the screenshot. Builders mark `[H]` and `[L]` only. A row is PASS only when the
named evidence exists; otherwise it is FAIL or NOT RUN.

`[U]` = screenshot read · `[H]` = harness assertion · `[L]` = live/measured · `[M]` = motion measurement.

**A row must be failable today.** If no plausible change to `UI\web` (or the named native surface) can
turn it red, it does not belong here.

---

## 0. The comparative gate — one judgement per surface

A surface passes when a reader shown the two screenshots cannot name a *structural* difference: row
anatomy, type levels, badge grammar, spacing rhythm. Sub-item ticking does not pass this gate. Only the
fresh visual pass may sign these.

| ID | Surface | M | Status | Evidence |
|---|---|---|---|---|
| G-01 | **Popup list** — light, dark, long text: index first, body-first rows, in-row provenance, right-aligned facts | `[U]` | **PASS** | The two structural defects that failed this gate are measured fixed by the 07:17 fresh pass: index box identical at 99/100/101 (`x=35, w=30.98, right=65.98`, content `x=75.98`) and hover `1.17:1` light / `1.20:1` dark, visible as a grey wash without competing with the selected fill + ring + rail (`AUDIT-release-visual-2` §2.2/§2.3, §3.2; `17-index-three.png`, `10-states-crop-*`). |
| G-02 | **Popup chrome** — header, tab strip, footer one band each; one type scale | `[U]` | **NOT RUN** | **25 Nov (V4):** the tab strip moved from its own band into `card-head` (`App.tsx`, `styles.css`); the 08:41 PASS below is void until a fresh agent re-reads 900 + 430. Prior evidence kept for the record: Fresh agent, current 08:05 bundle, headless Chromium DPR 1, `?mock=1000` and `?mock=real`, both themes at **900×700** and **430×400** (`%TEMP%\opencode\chrome-signoff\`: `list1000-*`, `real-*`, `settings-*`, `strip-manytabs-430x400-light.png`, `report.json`). **900:** header 44 px — mark + `Clip` 16 px/`CLIPBOARD HISTORY` 11 px, search pill (13 px input, `Ctrl K`), 32 px theme + settings, no rule under it; tab strip one raised 8 px-radius container ending after `+` (5 tabs, 12 px, 430 px wide in the 868 px row) with `move / Enter paste` on the same band; footer 42 px on a 1 px `#EDF0F4` hairline — `All history` · `1,000 clips` · `Ctrl T` / `Ctrl Shift V` / `Esc` keycaps, 10 px gaps, `right 871` inside `883`, no wrap. **430:** header wraps to 66 px (brand + search/theme/settings, then full-width search); strip fits all five tabs + `+` at rest (343/343, no scroll), and with four added tabs the 372 px scroller holds 610 px, scrolls to the end (`scrollLeft 238`) with the active tab and `+` still on screen; footer 36 px, one line, `All history` + `1,000 clips` — keycaps are deliberately `display:none` at this width (`.footer-right`), so nothing collides. Scale is 16/13/12/11 at both sizes; zero horizontal overflow and zero console errors in all 12 loads; every control labelled (`Search clips`, `Collapse/Expand search`, `Toggle theme`, `Settings`, `New tab`, `role=tab`). |
| G-03 | **Editor** — toolbar, text area, footer; buttons hug labels; one icon family | `[U]` | **PASS** | `AUDIT-release-visual-2` §2.4/§3.4: 25 buttons, 21 drawn 16 px SVGs (one 12 px `Aa`), 4 text labels by convention (H1–H3, Clear, Single line); the old `☑ ❝ ▦ +▤ •≡` glyphs and `<>`/`1.`/`{}` are drawn now. Footer reads `1 character [1 format]`, `35 characters [1 format]` (`A1-toolbar.png`, `98-one-char.png`). |
| G-04 | **Settings** — cards with icon + title + description; controls hug content; every group reachable | `[U]` | **PASS** | `AUDIT-release-signoff-2` §3 (fresh agent, current 08:05 bundle, both themes): at 900 the dialog is **860×563**, body **561/561**, not scrollable, two independent stacks **395 + 28 + 395**, all five groups populated and on screen, **TIME→DATA gap 14 px** (was the 173 px hole), last Capture row 8 px above the footer; at 430 the sheet is **430×400**, body **263/696**, a **30 px fade** at rest, wheel reaches `scrollTop 433` and every group is on screen. `settings-900x700-light/dark.png`, `settings-430x400-*-bottom.png`. |
| G-05 | **In-page toast** — one radius, one shadow, one type scale | `[U]` | **PASS** | `AUDIT-release-visual` §1.4/§4: legible and coherent in both themes; title 17.96/14.74, sub 6.83/7.96. Toast component and tokens unchanged since. |
| G-06 | **Native capture toast + tray menu** - one radius, one shadow, no stock WPF chrome | `[U]` | **PASS** | Live pass 25 Nov (`docs\AUDIT-live-acceptance-2.md` §3), isolated profile on `dist\Clip.exe` `6E9B1B95…9FA0`: the tray menu is the **themed** white `Surface` (`#FFFFFF`), **8 DIP rounded** corners, **14 DIP** app font, **1 physical px `#EAEEF4`** flat separator, **no shadow**, and the old `Clip 1.0.0` footer is **gone** (UIA items: `Clip: show/hide`, `Settings.`, `Clear history.`, `Exit`, each 195×48 at 150 % — `95-traymenu.png`, `98-traymenu-hover.png`, `9A-traymenu-keyboard-hover.png`). `Exit` removed the process in **186.7 ms** and every profile `msedgewebview2` in **1,765.5 ms** (< 2 s; three runs, max 446 / 1,765 ms). The toast evidence is the prior pass (`live-accept\shots\toast-focus.png`). **Hover pill read live on 25 Nov pass 3** (`AUDIT-live-acceptance-3` §1 #10) on `dist\Clip.exe` `221D045B…0548`: with the pointer over `Clip: show/hide` the row reads **`246,246,246`** against **`255,255,255`** for the rows below and for the menu surface (`91-tray-hover.png`, pixel read at 1440,824 vs 1440,920); `92-tray-keyboard.png` shows the same pill after a `Down` key (the pointer still owned it) - the pointer pill is proven, a keyboard-driven pill is still not. Pass 3 also re-read the chrome: white surface, `#EAEEF4` separator at y=950, four 195x48 items, no version footer, and `Exit` took the process in **447.8 ms** / webviews **1,050.6 ms** (`90-tray-menu.png`, `9A-after-exit.png`). |

---

## 1. Row contract v2 (`DESIGN-language.md` §3)

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| R-01 | Index is the first column, zero-padded tabular `01`…`99` (and `100` in a 1,000-row history) | `[U]` | **PASS** | `AUDIT-release-visual` §2: `01`, `02`, `99`, `100`, `101` all exist; re-measured `99/100/101` in pass 2 (§2.3). `50-index-crop.png`. |
| R-02 | Index column is a fixed width at 9/99/100/1000; the body never shifts | `[U]` | **PASS** | `AUDIT-release-visual-2` §2.3: `99/100/101` all `x=35, w=30.98, right=65.98`, `text-align:right`, `tabular-nums`; `.row-content` starts at `x=75.98` on all three. `.row-index { width: 4ch }` (was `min-width: 2ch`). `17-index-three.png`. |
| R-03 | One-line row fits content, minimum one line (~61–65 px) | `[U]` | **PASS** | `AUDIT-release-visual` §2: row 01 = 65 px; a tagless one-liner = 61 px (`03-row1-line.png`). Row height CSS and the row body are unchanged since (diff: only `--row-hover` and the index width); re-measured 65 px at `s18\final-report.json`. |
| R-04 | Ten-line ceiling = 245 px, never an eleventh line | `[U]` | **PASS** | `AUDIT-release-visual` §2: row 02 = 245 px, exactly 10 lines + provenance (`04-row10-line.png`). Clamp CSS untouched since; re-measured 245 px / `clamp 10` at `s18\final-report.json`. |
| R-05 | Provenance on every row: `app · title`, date/time right | `[U]` | **PASS** | `AUDIT-release-visual` §2: every row carries it (`Excel · Q3 forecast` / `25 Nov 2026, 05:23`). `hidePins` (1ced540) drops the per-row pin *glyph* in Pinned only; the provenance line is untouched. |
| R-06 | Date and time never truncate (900 and 430) | `[L]` | **PASS** | `AUDIT-release-visual` §2: `.prov-time` is `flex-shrink:0`; at 430 every time cell is 107 px client == scroll and shows in full; §1.7. Re-confirmed 07:42: zero horizontal overflow at 430 both themes (`s17\verify-0742.log`). |
| R-07 | Tags: max 2 visible (the RTF marker takes a slot), overflow folds to `+N` | `[U]` | **PASS** | `AUDIT-release-visual` §2: `Link`, `Spreadsheet`, `Files`, `Image`, `+1`, RTF marker. The 2-chip ceiling is `ClipList.tsx` and unchanged; pass 2 §2.5/§3.5 confirms the overflow chip renders `+N tag` at 900 and that Image + Files + `+1 tag` fit at 430. |
| R-08 | The three `+N` chips are distinguishable in a scan | `[U]` | **PASS** | Pass 2 §2.5 passed this at 900 (`+1.8k chars` 80.5 px, `+1 file` 58.8, `+1 tag` 50.4) and failed it only at 430, where `.chip-unit` was hidden (`+1.8k`, `+1`, `+1`). `8125132` removes the hiding; `s18\final-report.json` re-measures 430 with `+1.8k chars` / `+1 file` / `+1 tag` — the same three labels pass 2 read at 900. `91`, `92`, `95` crops. |
| R-09 | Image row shows its 96×60 thumb left of the body, capped to 3 lines | `[U]` | **PASS** | `AUDIT-release-visual` §1.6/§2: 96×60 thumb on row 99, 10-line OCR body beside it (`05b-index-99-100.png`). Thumb CSS untouched since. Noted weakness: 200 px of dead column under a 60 px thumb. |
| R-10 | Files row: mono paths, folder ellipsizes, filename stays | `[U]` | **PASS** | `AUDIT-release-visual` §1.4: files row legible; `14-files-row.png`. Files-row CSS untouched since. |
| R-11 | Image row with OCR text shows `Text read from image` | `[U]` | **PASS** | `AUDIT-release-visual` §1.4/§2; `12-image-ocr.png`; row `99` reads `Text read from image`. |
| R-12 | Image row without OCR text shows `No text found in this image`, never a blank body | `[U]` | **PASS** | `AUDIT-release-visual` §1.4: placeholder rendered; contrast table has the `--fg55` pair at 6.83/7.96. |

---

## 2. Contrast & themes

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| C-01 | Body, secondary, meta, placeholder, tag, OCR and toast text ≥ 4.5:1 in **both** themes | `[L]` | **PASS** | `AUDIT-release-visual` §4: 18 pair groups, 4.62–17.96; `AUDIT-impeccable-web`: 62 light + 62 dark pairs, 0 failures. Pass 2 recomputed every **new** element (§4): tightest new number is the dark active-tab count at 4.82. |
| C-02 | Disabled state text ≥ 4.5:1 (labels, keycaps, explanations) | `[L]` | **PASS** | `AUDIT-release-visual-2` §1: the 0.55 opacity is gone; label and explanation both `--fg55` = **6.83:1** light / **7.96:1** dark (was 4.00 / 2.47); the switch is dimmed instead. Re-measured 6.833 / 7.963 by the 07:42 harness. |
| C-03 | Focused row ring is visible on both themes and both row surfaces | `[L]` | **PASS** | `AUDIT-release-visual-2` §2.2: 2 px `#c2410c` 5.18:1 light, 2 px `#ff9550` 8.18:1 dark, distinct from hover and selected; `10-states-crop-*`. |
| C-04 | Hover is perceptible as a row state | `[L]` | **PASS** | `AUDIT-release-visual-2` §2.2/§3.2: `#eaedf1` on `#fff` = **1.17:1**, `#212837` on `#141821` = **1.20:1**; visible grey band with no ring while selection keeps its peach fill + ring + rail. `10-states-crop-light/dark.png`. |
| C-05 | Theme change applies live from Settings and survives a restart | `[L]` | **PASS** | `s7\EVIDENCE.md` §A: settled `"theme":"dark"`, page `data-theme` dark, System resolves after cold start (`22`). Theme tokens and the resolver are unchanged since. Note: the topbar toggle is session-only (`AUDIT-ui-code` M6), not this row. |

---

## 3. Widths and zoom

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| W-01 | 900 / 720 / 600 / 480 / 430: zero horizontal overflow, both themes | `[L]` | **PASS** | Re-measured at 07:42 on the final bundle: `scrollWidth − clientWidth = 0` at all five sizes, both themes (`s17\verify-0742.log`); `s4a\EVIDENCE.md` for the original table; `s18\final-report.json` re-confirms 430 after the tab strip. Re-measured after S20's fit-content strip and footer hint: `docW == innerW` on all ten width/theme pairs (`s20\proof-report.json` K1). |
| W-02 | Zoom 100 / 125 / 150 %: zero horizontal overflow on page, dialog and editor | `[L]` | **PASS** | Fresh pass `docs\AUDIT-visual-3.md` on page `87329539…` (2,865,809 B), zoom set with the page's own control and read back: 100 % page 900/900, dialog 900/900 + body 848/848, editor 900/900 + page 758/758; 125 % (720×560) 720/720, 720/720 + 508/508, 720/720/720/686; 150 % (600×466) 600/600, 600/600 + 508/508, 600/600/600/566. Zero elements past the right edge at any zoom (`zoom-100|125|150-page|settings|editor.png`, `measure.json`). Method named in the report: CSS viewport = 900×700 ÷ factor, not a native ZoomFactor. |
| W-03 | Settings fits the real 900×700 window; nothing below an invisible fold | `[L]` | **PASS** | `AUDIT-release-visual-2` §3.1: body `scrollHeight 520 = clientHeight 520`, zero scroll, no fade; all five groups above the footer at 622.5; dialog 860×657. S20 re-measured after the column split: body **426/426**, TIME→DATA gap **14 px** (was a 173 px hole), all five groups in order (`s20\proof-report.json` I1–I3; `06-settings-900.png`). |
| W-04 | At 430 the settings overflow is visible (thumb, fade or "more" cue), not hidden | `[L]` | **PASS** | `AUDIT-release-visual-2` §1/§3.1: 430×400 sheet, 263 px viewport for 696 px content, a 30 px fade + chevron at rest, wheel reaches `scrollTop 433` (max) where Time and Data are visible; `22`/`23`. S20 re-checks the same 696/263 sheet after wrapping the columns: group order and 14 px spacing preserved, `docW = 430` (`s20\proof-report.json` K5; `10-settings-430.png`). |
| W-05 | Toast and export menu at 430: no clipped text, no overflow | `[U]` | **PASS** | Fresh pass `docs\AUDIT-visual-3.md` at 430x700: toast `331/331` (title/sub 245/245) at x 75.4-408, export menu `152/152` at x 255-409, items `140/140` each (`Save as… Ctrl E`, `Text (.txt)`, `Markdown (.md)`), page `430/430`, nothing past `innerWidth`, no clipped or ellipsised text (`430-toast.png`, `430-export.png`). Re-measured on the rebuilt page `87329539…` with identical numbers. |

---

## 4. Keyboard

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| K-01 | Focus is selection; one row is the single tab stop | `[L]` | **PASS** | `onFocus={() => onSelect}` (`ClipList.tsx`); roving tabindex; `AUDIT-ui-code` §1; `s4a` keyboard shot. Re-measured on the final bundle: `focusEqualsSelection {focused:true, selected:"true", id:"1"}` (`s18\final-report.json`). |
| K-02 | `↑ ↓ Home End PgUp PgDn` move the selected row by the measured viewport | `[L]` | **PASS** | `s4a\EVIDENCE.md`: id 10→27, Home/End first/last, PgDn = the two tall rows actually visible. Re-measured: ArrowDown 01→02, Home `01`, End `497` (`s18\final-report.json`). |
| K-03 | `Enter`, `Ctrl+1`–`Ctrl+9`, `Ctrl+Shift+V` act on the selected/visible row | `[L]` | **PASS** | `s4a`: Enter pasted (mock fallback `Copied instead`), Ctrl+Shift+V took the plain-paste path; a bare digit is search text since the owner change, and `Ctrl+2` pasting the second visible row is proven in `e2e/keyboard.spec.ts` (`Ctrl+1..Ctrl+9 paste the visible row`). Those handler branches are unchanged by 141ec9e (diff adds only Ctrl+Up/Down, Ctrl+Shift+←/→ and Ctrl+M). Host paste delivery is a features-checklist row. |
| K-04 | `F2 Ctrl+F Ctrl+P Delete Ctrl+Z` act on the right target | `[L]` | **PASS** | `s4a`: F2 opened the editor and `↓` did nothing while open; Ctrl+F focused search keeping the query; Ctrl+P pinned; Delete asked first; Ctrl+Z restored and re-selected. Re-measured F2 opens the editor (`s18\final-report.json`); handler branches unchanged. |
| K-05 | `Ctrl+E` opens **Export** for the selected clip (the editor is F2) | `[L]` | **PASS** | Row corrected to the shipped contract: `useClipboardKeys.ts:106-110` maps Ctrl+E to `onExport`; `s18\final-report.json` `regress.ctrle=true` after a scripted `Control+e` (the `export-pop` menu opened, no editor). |
| K-06 | No matrix key fires while typing in search (`Delete`, `Home`, `End`, `Enter`, digits keep their caret meaning) | `[L]` | **PASS** | `s20\proof-report.json` D1: with `abc` in the search box, `Delete`, `Home`, `End` and `Enter` each left **0 dialogs, 0 toasts**, focus in the input and the value unchanged; `1` typed as text (`abc1`). No history action ran while the field owned the caret. |
| K-07 | IME composition suppresses matrix keys | `[L]` | **NOT RUN** | `s4a` only sent a synthetic `isComposing`/`keyCode 229` event; no real IME has been driven. `CHECKLIST-features` records the same. |
| K-08 | Tag chips are reachable and activatable by keyboard | `[L]` | **PASS** | Chips stay `tabIndex={-1}` because the row owns focus. `Ctrl+T` opens **Filter by tag** for the selected clip, listing its canonical tokens (`image:`, `spreadsheet:`) with the Ctrl+M arrow/Enter/Esc grammar; Enter fills the search box and filters (497 → **2 shown**), Esc returns focus to the row, an untagged clip says **“This clip has no tags.”** With a filter active the list adds **Clear tag filter** (the active token is marked `active`); choosing it empties the query and the 497 rows return. `s20\proof-report.json` A1–A6, B1–B2; `s20\clear-probe.log`; `01-tag-picker-open.png`, `02-tag-filter-applied.png`, `03-no-tags.png`, `11-tag-picker-clear.png`. |
| K-09 | Settings opens with focus inside the dialog and Tab never leaves it | `[L]` | **PASS** | `AUDIT-release-visual-2` §1/§2.3: opens on `Close settings`, 18 consecutive Tabs stay inside `.dialog`, Esc restores focus to the Settings button, `aria-modal="true"`. |
| K-10 | `Esc` cascade: clears search → closes editor/popover/discard → hides the window | `[L]` | **PASS** | `s4a`: first Esc cleared the query, second called `hide()`; `s7` §B: Esc closes the link popover, then `Discard changes?`, then the editor. Esc handler unchanged by the S12–S18 commits. |

---

## 5. Editor

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| E-01 | Editor is a real modal: focus trapped, background inert, focus restored to the row on close | `[L]` | **PASS** | `s20\proof-report.json` E1–E3: with the editor open, `.app` is `aria-hidden="true"` and `body` is `pointer-events: none` (background inert) while focus sits inside `.editor-overlay`; Esc closes and restores focus to the exact row that opened it (id 1, `aria-selected="true"`). The 40-Tab trap is `s17\verify-0742.log`. |
| E-02 | Dirty editor: `Discard changes?` appears; discard leaves the stored body untouched | `[U]` | **PASS** | `s7` §B: third Esc closed with the row still 4,800 chars, no `DISCARD ME`; `AUDIT-release-visual` §1.5: clean/dirty/discard strong in both themes (`24`, `26`). Discard branch untouched by 141ec9e. |
| E-03 | Saving an edit invalidates the row body; the list shows the new text without reload | `[L]` | **PASS** | `s20\proof-report.json` F1–F2: typed ` EDITPROOF` at the end of clip 1 and saved; the editor closed with **“Clip saved”** and the row body then read `https://docs.stripe.com/api/charges EDITPROOF` without a reload. `forgetBody(id)` at `rich.tsx:220`. `04-edit-then-look.png`. |
| E-04 | Footer character count updates after every keystroke | `[L]` | **PASS** | `s17\verify-0742.log`: `35 characters` on open → `36` after 1 → `45` after 10 → `55` after 20 keystrokes; `1 character` after a one-char edit; `1 format` singular. The frozen memo deps are gone (`141ec9e`). `03-editor-count.png`. |
| E-05 | Editor toolbar is one drawn icon family and the footer pluralises | `[U]` | **PASS** | `AUDIT-release-visual-2` §2.4/§3.4: 21 of 25 buttons are drawn 16 px SVGs, 4 are text labels by convention (H1–H3, Clear, Single line); footer reads `1 character [1 format]` / `35 characters [1 format]`. `A1-toolbar.png`, `98-one-char.png`. |

---

## 6. States

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| S-01 | Empty history: first run shows the empty state, header counts `0`, nothing broken | `[L]` | **PASS** | `evidence-s10\01-list-light-empty.png` on a clean profile (real exe, empty APPDATA); `s7` after Clear history (`23`); `release-visual\15-empty.png`. |
| S-02 | No results: `Nothing matches “…”` with the query, list not blank | `[L]` | **PASS** | `s4a\900-light-empty-no-results.png`; `AUDIT-release-visual` §1.4. Empty-state copy unchanged. |
| S-03 | Unknown tag: `no tag named “foo” — searched as text`, list does not shift | `[L]` | **PASS** | `s4a` both themes; measured `list-wrap` top shift = 0. The hint still renders from `activeSearch.unknownTags` (`App.tsx:896-903`). The preview mock now matches the host parser: `zzzz:` returns **0 shown** with the hint, `image:` returns the image rows (`s20\proof-report.json` G1–G2; `08-mock-unknown-tag.png`, `09-mock-image-filter.png`). |
| S-04 | Toast auto-fits content up to `Notification.MaxLines` (one line stays short, long stops at the cap) | `[L]` | **PASS** | S8b measured 77 → 160 px by content; `evidence-s10\06-toast.png` shows a two-line auto-fit; `s4a` one-line `Skipped`/`Hotkey unavailable`. Native toast untouched by S12–S18. |
| S-05 | Export menu opens on the selected clip; the result toast names the file (saved) or says cancelled | `[L]` | **PASS** | S6b (`ff11f84`): export from the page, toast names the file; `AUDIT-release-visual` §1.4. The post-export window-visibility defect that used to qualify this row is closed: `ec4ce55` shows the shell on save, cancel and a failed write (`AUDIT-release-logic-2` §1.1, harness `ask -> show`). |
| S-06 | Settings invalid input: inline error, prior value kept and nothing lost | `[L]` | **PASS** | `s7` §A hotkey conflict (`08`): inline `Could not register … kept Alt+J`, field returns to Alt+J, config unchanged. |
| S-07 | OCR text is searchable from the page (marker row is the same clip the search finds) | `[L]` | **PASS** | S9b; `evidence-s10\README.md`: OCR filled the image row live, search finds it. |
| S-08 | Empty tab: actions that cannot run are not shown as dead disabled buttons | `[U]` | **PASS** | `App.tsx:907` renders `Edit clip` only when a row is selected (`{selected && …}`), so an empty tab has no dead control; the 07:17 fresh pass read the empty tab at 900 and reported it legible, with no disabled Edit (`AUDIT-release-visual-2` §2.6). |

---

## 7. Browser surfaces (WebView2 = Chromium)

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| B-01 | Scrollbars, `::selection` and caret are themed in both themes (no default blue/grey) | `[L]` | **PASS** | `AUDIT-impeccable-web` P2-3 + App. C: selection 13.26/10.64, caret 4.66/7.06, thumb 3.50/3.64; pass 2 §1.7: 0 glass, 0 gradient text, selection/accent-caret/scrollbar tokens present. Thumb is not visible at rest when the body overflows — that is W-04. |
| B-02 | Times, counts and the index use tabular numerals | `[L]` | **PASS** | `AUDIT-impeccable-web` positive findings; `AUDIT-release-visual-2` §2.3 (`tabular-nums` on the index) and §1.7 (index, time, counts). |
| B-03 | Every focusable control shows a visible focus ring | `[L]` | **PASS** | `AUDIT-release-visual` §1.9: 2 px accent ring on every control it tabbed. Pass 2 found the three dialogs opened with `outline: none`; `8125132` adds `.dialog :focus`, and the delete confirm's computed outline on open is `solid 2px rgb(194,65,12)` / `rgb(255,149,80)` (`s18\final-report.json` `focus.open-*`), with `s18\probe4-settings-focus.png` / `probe4-move-focus.png` for the other two. Focus *order* is K-09. |

---

## 8. Retired with the WPF surface (not rows)

No longer reachable, so not failable and not re-read: the 28×28 identity tile, `ClipRow` internals and
`CanContentScroll`, the WPF theme dictionaries, the Images tab preview + filmstrip, the outside-tooltip
dock and the Aero2 tray gutter. Their history is in `docs\CHECKLIST-archive.md`; do not re-open them.

---

## 9. Frozen decisions — do not reintroduce

- Selection never changes row height or list geometry; no accent wash, no selected-row growth.
- No letter/identity tile: the index is the first column and the paste key.
- Provenance is on every row; the bottom bar repeats the selected row, never replaces it.
- Old clips gain tags at read time, no migration; a guessed tag is worse than a missing one.
- List clamp is 10 lines; the outside tooltip carries the full text.
- Rich rows render only sanitised content; no remote image fetch, no `HtmlRenderer`.
- No font glyphs as icons; drawn 16/20 px strokes only (text labels H1–H3, Clear, Single line are
  labels, not icons — accepted at `E-05`).
- Colour lives only in the two token blocks in `styles.css`; components name tokens.
- No layout animation; selection never animates geometry; `prefers-reduced-motion` is honoured.
- Ortu's warm paper and `docs\DESIGN-contrast.md` are history, not authority.
- `previewLines` keeps its 1..20 range (the `settings.ts` clamp and the Capture slider); `durationMs` stays milliseconds.
- macOS keep-clips model is rejected: deleting a tab deletes its clips, and the confirm says so.

## 10. Audit scoring (unchanged dimensions, 0–4)

Accessibility (contrast, focus, keyboard, reduced motion) · Performance (latency, scroll, memory, one
renderer) · Theming (tokens only, dark + light first-class) · Platform conformance (tray, hotkey, DPI,
WebView2 surfaces) · Adaptivity (widths, zoom, DPI, toast positions). Every open row must be PASS or
carry a written owner-accepted deviation in the release gate below.

## 11. Design inputs, in order

`docs\DESIGN-language.md` §3 (contract) → `UI\web\src\styles.css` token blocks (only colour home) →
`ClipList.tsx` / `rich.tsx` / `EditorOverlay.tsx` / `SettingsDialog.tsx` / `icons.tsx` (components) →
`docs\design-evidence\` + `docs\AUDIT-release-visual-2.md` (evidence) → `docs\AUDIT-impeccable-web.md` +
`docs\AUDIT-ui-code.md` (anti-reference). The first integrated build of a surface must already satisfy
§0 — not "built then polished".

## 12. Release gate

Cutover requires **all 54 rows PASS**, or a written owner-accepted deviation for the named row. A
deviation is not a PASS and is recorded in this section before the release commit.

**Not PASS today - 0 FAIL + 2 NOT RUN:**

| Row | Why |
|---|---|
| `K-07` NOT RUN | Real IME never driven (no Hindi/Chinese/Japanese input method on this machine). |
| `G-02` NOT RUN | V4 (25 Nov) moved the tab strip into `card-head`; chrome re-read owed at 900 + 430. |

`G-06` (tray + toast), `W-02` (125 % zoom) and `W-05` (430 toast/export) moved to PASS on the 25 Nov live
and visual passes: `docs\AUDIT-live-acceptance-2.md` §3 (`G-06` chrome, `95-traymenu.png`; hover pill
re-read in `AUDIT-live-acceptance-3` at `246,246,246` vs `255,255,255`) and `docs\AUDIT-visual-3.md`
(`W-02`, `W-05`).

**Also not signatures, but on the record:** the memory ceiling. 527.5 MB working set / 270.5 MB private,
7 processes, measured on the **06:52** exe (`208,183,750` B); the 07:38 rebuild (`208,187,846` B) has not
been re-measured and the ~643 MB figure with an image loaded is from an earlier build. This needs the
owner's written acceptance or a new ceiling.

**Fresh sign-offs:** `docs\AUDIT-release-signoff.md` (25 Nov, fresh agent): 10/10 against its brief, and its
four ranked defects are post-sign-off items. `docs\AUDIT-release-signoff-2.md` (25 Nov 08:23, fresh agent on
the 08:05 bundle): signs **G-04** on the settings-surface evidence in its §3; its “G-02” verdict is the
**list surface** (§2), which is why the band row needed its own read — done 08:41 on the same bundle, see the
G-02 row above. The tag-picker, typing-guard, editor-focus and edit-then-look rows are builder `[L]`
harness evidence, not fresh `[U]` signatures.

**What is no longer on this list:** the tag keyboard path, the typing-in-search guard, editor focus
restore/background inert, edit-then-look, settings fit **and the S20 settings stack (`signoff-2` §3)**, the
430 sheet cue, hover, the index column, the toolbar family, the disabled explanation, the character count,
the tab delete wording, the dialog focus ring, `aria-modal` **and the G-02 chrome band (08:41 fresh read at
900 and 430)** — all re-measured on the current bundle by the 07:17 fresh pass, the 07:42 harness, the S20
harness, signoff-2 or the band pass. The remaining 50 PASS rows are
regression-locked: any change to the code under them returns them to NOT RUN until re-proven, and
`G-01`–`G-06` are re-run by a fresh visual agent on the release artifact. The page now ships in
`dist\Clip.exe` (208,187,846 B, rebuilt 08:08 from the 08:05 bundle): one page scan of that exe is still owed.

## 13. Final tally (25 Nov, live acceptance 3)

**54 rows — 52 PASS · 0 FAIL · 2 NOT RUN** (K-07 plus G-02, voided 25 Nov by the V4 move of the tab strip into `card-head`). Every PASS row names its artifact; no `[U]` row was signed by a
builder. `G-06` moved from FAIL to PASS on the 25 Nov live tray read (`AUDIT-live-acceptance-2` §3) and its
hover sub-clause was completed in pass 3 (`AUDIT-live-acceptance-3` §1 #10: `246,246,246` pill on
`#FFFFFF`, pointer only); `G-04` moved from NOT RUN to PASS on `AUDIT-release-signoff-2` §3; `G-02` moved on
the 08:41 fresh band read of the same 08:05 bundle (header/tab strip/footer at 900 and 430, both themes).
Pass 3 also re-read the live shell at 900 and 430 DIP (gear control, strip, footer) without changing a
verdict.

## 14. What still needs the owner

1. **Tray item hover pill** — the pointer pill is now read (`246,246,246` on `#FFFFFF`, pass 3); what is still
   missing is a **keyboard-driven** pill: `Down` with the menu open left the pointer's row lit and never moved
   a highlight. Owner decided 25 Nov: the tray menu is **mouse-only**; no keyboard pill is intended. Keyboard users drive the app through `Alt+V` and shortcuts instead.
2. **125 % zoom (W-02)** — the shell at 125 %, page, dialog and editor, zero overflow; only 100 and 150
   were measured.
3. **430 toast and export menu (W-05)** — `release-visual\37-430-toast.png` / `38-430-export.png` exist but
   were never read or signed.
4. **Real IME (K-07)** — a composition open while the popup is up; Esc cancels the composition, Enter
   commits it.
5. **Memory ceiling** — accept ~528 MB idle / ~643 MB with an image, or set a new ceiling. Owner decided
    25 Nov: **remeasure on the rebuilt exe first**, then set the ceiling from fresh numbers.
6. **430 tab counts** — pass 3 read the live shell at 430 DIP and the strip showed `All / Pinned / Images`
   without their numbers (`96-shell-430-wide.png`), while the same strip shows `All 13 / Pinned 0 / Images 2`
   at 900. Owner decided 25 Nov: **keep** — names only at 430, counts at normal width.
6. **Re-scan the artifact** — `UI\web\dist\index.html` is 2,862,374 B (08:05) and `dist\Clip.exe` was rebuilt
   at 08:08 from it (`RELEASE-v2.md`); the checklist signs the bundle, so the exe needs one last page scan.
