# Clip — UI/UX Verification Checklist

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
`%TEMP%\opencode\release-visual\` (screenshots of the built page). **The release visual pass is
`docs\AUDIT-release-visual.md`** — a fresh agent (not the builder), built page in Chromium via `file://`,
mock data, 900×700 and 430×700. It does not run the WebView2 shell and does not cover the native toast,
tray or title bar.

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
| G-01 | **Popup list** — light, dark, long text: index first, body-first rows, in-row provenance, right-aligned facts | `[U]` | **FAIL** | `AUDIT-release-visual` §1.3/§1.4: anatomy and rhythm read as one system, but the index column jumps 7 px at 100 and hover is invisible (1.02:1 / 1.10:1). Both are structural to the row. |
| G-02 | **Popup chrome** — header, tab strip, footer one band each; one type scale | `[U]` | **PASS** | `AUDIT-release-visual` §1.1/§1.2: hierarchy immediate, 14/20 body, 11/16 provenance, 15/22 title, 12/16 tabs, no fourth scale; both themes hold. Known non-structural weakness: the 48 px header repeats `1,000` four times. |
| G-03 | **Editor** — toolbar, text area, footer; buttons hug labels; one icon family | `[U]` | **FAIL** | `AUDIT-release-visual` §1.5/§3.5: clean/dirty/discard strong in both themes, but the toolbar mixes drawn SVGs with `☑ ❝ ▦ +▤ •≡` glyphs and the footer says `1 formats`. |
| G-04 | **Settings** — cards with icon + title + description; controls hug content; every group reachable | `[U]` | **FAIL** | `AUDIT-release-visual` §1.5/§3.1: at 900×700 the dialog is 520×668 and 203 px of content (TIME, DATA) sits below an invisible fold; the zoom row is sliced. |
| G-05 | **In-page toast** — one radius, one shadow, one type scale | `[U]` | **PASS** | `AUDIT-release-visual` §1.4/§4: legible and coherent in both themes; title 17.96/14.74, sub 6.83/7.96. |
| G-06 | **Native capture toast + tray menu** — one radius, one shadow, no stock WPF chrome | `[U]` | **NOT RUN** | Not covered by the release visual pass (it loads the page, not the shell). `docs\evidence-s10\06-toast.png` is builder-captured and `AUDIT-release-logic` P1 records the visual sign-off as absent. |

---

## 1. Row contract v2 (`DESIGN-language.md` §3)

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| R-01 | Index is the first column, zero-padded tabular `01`…`99` (and `100` in a 1,000-row history) | `[U]` | **PASS** | `AUDIT-release-visual` §2: `01`, `02`, `99`, `100`, `101` all exist; `50-index-crop.png`. |
| R-02 | Index column is a fixed width at 9/99/100/1000; the body never shifts | `[U]` | **FAIL** | §2 measured: 99 = 16 px box / right edge 51; 100 = 23 px / right 58; every cell right of it shifts 7 px. Cause `.row-index { min-width: 2ch }` (`53-99-100.png`, `50-index-crop.png`). |
| R-03 | One-line row fits content, minimum one line (~61–65 px) | `[U]` | **PASS** | §2: row 01 = 65 px; a tagless one-liner = 61 px (`03-row1-line.png`). |
| R-04 | Ten-line ceiling = 245 px, never an eleventh line | `[U]` | **PASS** | §2: row 02 = 245 px, exactly 10 lines + provenance (`04-row10-line.png`). |
| R-05 | Provenance on every row: `app · title`, date/time right | `[U]` | **PASS** | §2: every row carries it (`Excel · Q3 forecast` / `25 Nov 2026, 05:23`); S3b shipped it. |
| R-06 | Date and time never truncate (900 and 430) | `[L]` | **PASS** | §2: `.prov-time` is `flex-shrink:0`; at 430 every time cell is 107 px client == scroll and shows in full; §1.7. |
| R-07 | Tags: max 2 visible (the RTF marker takes a slot), overflow folds to `+N` | `[U]` | **PASS** | §2: `Link`, `Spreadsheet`, `Files`, `Image`, `+1`, RTF marker; `ClipList.tsx:247` keeps the 2-chip ceiling. |
| R-08 | The three `+N` chips are distinguishable in a scan | `[U]` | **FAIL** | §3.6: `+1 file` (body), `+1.8k`/`+997` (characters), `+1` (hidden tags) wear the same chip; only the tooltip tells them apart. |
| R-09 | Image row shows its 96×60 thumb left of the body, capped to 3 lines | `[U]` | **PASS** | §1.6/§2: 96×60 thumb on row 99, 10-line OCR body beside it (`05b-index-99-100.png`). Noted weakness: 200 px of dead column under a 60 px thumb. |
| R-10 | Files row: mono paths, folder ellipsizes, filename stays | `[U]` | **PASS** | §1.4: files row legible; `14-files-row.png`. |
| R-11 | Image row with OCR text shows `Text read from image` | `[U]` | **PASS** | §1.4/§2; `12-image-ocr.png`; `99` row reads `Text read from image`. |
| R-12 | Image row without OCR text shows `No text found in this image`, never a blank body | `[U]` | **PASS** | §1.4: placeholder rendered; contrast table has the `--fg55` pair at 6.83/7.96. |

---

## 2. Contrast & themes

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| C-01 | Body, secondary, meta, placeholder, tag, OCR and toast text ≥ 4.5:1 in **both** themes | `[L]` | **PASS** | `AUDIT-release-visual` §4: 18 pair groups, 4.62–17.96; `AUDIT-impeccable-web`: 62 light + 62 dark pairs, 0 failures (lowest 4.62/4.82). |
| C-02 | Disabled state text ≥ 4.5:1 (labels, keycaps, explanations) | `[L]` | **FAIL** | Disabled buttons/keycaps pass (5.14/5.67, `AUDIT-impeccable-web` P2-1), but the disabled settings explanation is **2.47:1 light / 3.28:1 dark** and its label 4.00/5.26 (`AUDIT-release-visual` §4). WCAG exempts controls; this sentence is the only explanation of a forced setting. |
| C-03 | Focused row ring is visible on both themes and both row surfaces | `[L]` | **PASS** | `AUDIT-release-visual` §1.9 (2 px accent rings); ring 5.18 light / 8.18 dark (`AUDIT-impeccable-web` App. C); `10-focus.png`. |
| C-04 | Hover is perceptible as a row state | `[L]` | **FAIL** | `AUDIT-release-visual` §1.4/§3.2: `#fbfcfd` on `#ffffff` = 1.02:1, dark ≈ 1.10:1; invisible in `08-hover.png`. |
| C-05 | Theme change applies live from Settings and survives a restart | `[L]` | **PASS** | `s7\EVIDENCE.md` §A: settled `"theme":"dark"`, page `data-theme` dark, System resolves after cold start (`22`). Note: the topbar toggle is session-only (M6 in `AUDIT-ui-code`), not this row. |

---

## 3. Widths and zoom

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| W-01 | 900 / 720 / 600 / 480 / 430: zero horizontal overflow, both themes | `[L]` | **PASS** | `s4a\EVIDENCE.md` table: `scrollWidth − clientWidth` = 0 at all five sizes, both themes; re-confirmed at 430 (`AUDIT-release-visual` §1.7) and 600/430 (`s7` C). |
| W-02 | Zoom 100 / 125 / 150 %: zero horizontal overflow on page, dialog and editor | `[L]` | **NOT RUN** | 100 and 150 measured (150 = 600×466, 0 overflow: `s7` `20`/`21`/`22`); 125 % was saved in mock (`25`) but never measured. Half a row is not a PASS. |
| W-03 | Settings fits the real 900×700 window; nothing below an invisible fold | `[L]` | **FAIL** | `AUDIT-release-visual` §3.1: dialog 520×668, body 517 px for 720 px of content; zoom row sliced; `TIME`/`DATA` hidden (`51-settings-cut-crop.png`). |
| W-04 | At 430 the settings overflow is visible (thumb, fade or "more" cue), not hidden | `[L]` | **FAIL** | §1.8/§3.1: body 519 px for 800 px, initial view ends at `APPEARANCE`; no visible scrollbar or cue (`32-430-settings-light.png`). |
| W-05 | Toast and export menu at 430: no clipped text, no overflow | `[U]` | **NOT RUN** | `release-visual\37-430-toast.png` and `38-430-export.png` exist but were not read or signed by the fresh pass. |

---

## 4. Keyboard

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| K-01 | Focus is selection; one row is the single tab stop | `[L]` | **PASS** | `onFocus={() => onSelect}` (`ClipList.tsx:267`); roving tabindex; `AUDIT-ui-code` §1; `s4a` keyboard shot; `10-focus.png`. |
| K-02 | `↑ ↓ Home End PgUp PgDn` move the selected row by the measured viewport | `[L]` | **PASS** | `s4a\EVIDENCE.md` keyboard table: id 10→27, Home/End first/last, PgDn = the two tall rows actually visible. |
| K-03 | `Enter`, `1`–`9`, `Ctrl+Shift+V` act on the selected/visible row | `[L]` | **PASS** | `s4a`: Enter pasted (mock fallback `Copied instead`), `3` pasted the third visible row, Ctrl+Shift+V took the plain-paste path. Host paste delivery is a features-checklist row, not this one. |
| K-04 | `F2 Ctrl+F Ctrl+P Delete Ctrl+Z` act on the right target | `[L]` | **PASS** | `s4a`: F2 opened the editor and `↓` did nothing while open; Ctrl+F focused search keeping the query; Ctrl+P pinned; Delete asked first; Ctrl+Z restored and re-selected. |
| K-05 | `Ctrl+E` opens the editor for the selected clip | `[L]` | **NOT RUN** | Implemented in the key matrix (`AUDIT-release-logic` P2) but absent from every observed key→action table. |
| K-06 | No matrix key fires while typing in search (`Delete`, `Home`, `End`, `Enter`, digits keep their caret meaning) | `[L]` | **NOT RUN** | The guard landed in `aba6c71` **after** the S4a run; S4a observed the bug it fixes (`Delete` asked to delete while the query held focus). No post-fix keyboard pass exists. |
| K-07 | IME composition suppresses matrix keys | `[L]` | **NOT RUN** | `s4a` only sent a synthetic `isComposing`/`keyCode 229` event; `CHECKLIST-features` records the real IME as NOT RUN (no real IME synthesised). |
| K-08 | Tag chips are reachable and activatable by keyboard | `[L]` | **FAIL** | `ClipList.tsx:292-296`: a `<button tabIndex={-1}>` inside `role="option"` (ARIA prunes it), no key handler; global matrix skips controls. `AUDIT-ui-code` A1, still open. Mouse-only filtering by tag. |
| K-09 | Settings opens with focus inside the dialog and Tab never leaves it | `[L]` | **FAIL** | `SettingsDialog.tsx:153` `onOpenAutoFocus` preventDefault; `AUDIT-release-visual` §3.3: activeElement stays on the Settings button, first four Tab stops are behind the scrim. |
| K-10 | `Esc` cascade: clears search → closes editor/popover/discard → hides the window | `[L]` | **PASS** | `s4a`: first Esc cleared the query, second called `hide()`; `s7` §B: Esc closes the link popover, then `Discard changes?`, then the editor. |

---

## 5. Editor

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| E-01 | Editor is a real modal: focus trapped, background inert, focus restored to the row on close | `[L]` | **NOT RUN** | Radix `Dialog.Root` rewrite landed in `aba6c71` (`EditorOverlay.tsx:162`); no keyboard pass after the rewrite. The pre-fix audit (`AUDIT-ui-code` A2) is the reason the row exists. |
| E-02 | Dirty editor: `Discard changes?` appears; discard leaves the stored body untouched | `[U]` | **PASS** | `s7` §B: third Esc closed with the row still 4,800 chars, no `DISCARD ME`; `AUDIT-release-visual` §1.5: clean/dirty/discard strong in both themes (`24`, `26`). |
| E-03 | Saving an edit invalidates the row body; the list shows the new text without reload | `[L]` | **NOT RUN** | `forgetBody(id)` was added in `aba6c71` (`rich.tsx:220`); no observed edit-then-look run. The bug it fixes is `AUDIT-ui-code` M1. |
| E-04 | Footer character count updates after every keystroke | `[L]` | **FAIL** | `EditorOverlay.tsx:157` memo deps `[editor, dirty, loading]`; `dirty` stops changing after the first edit, so the count freezes (`AUDIT-ui-code` Y1, unchanged). |
| E-05 | Editor toolbar is one drawn icon family and the footer pluralises | `[U]` | **FAIL** | `AUDIT-release-visual` §3.5: drawn SVGs mixed with `☑ ❝ ▦ +▤ •≡`; `1 formats` / `1 characters` (`55-toolbar-crop.png`). |

---

## 6. States

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| S-01 | Empty history: first run shows the empty state, header counts `0`, nothing broken | `[L]` | **PASS** | `evidence-s10\01-list-light-empty.png` on a clean profile (real exe, empty APPDATA); `s7` after Clear history (`23`); `release-visual\15-empty.png`. |
| S-02 | No results: `Nothing matches “…”` with the query, list not blank | `[L]` | **PASS** | `s4a\900-light-empty-no-results.png`; `AUDIT-release-visual` §1.4. |
| S-03 | Unknown tag: `no tag named “foo” — searched as text`, list does not shift | `[L]` | **PASS** | `s4a` both themes; measured `list-wrap` top shift = 0. |
| S-04 | Toast auto-fits content up to `Notification.MaxLines` (one line stays short, long stops at the cap) | `[L]` | **PASS** | S8b measured 77 → 160 px by content; `evidence-s10\06-toast.png` shows a two-line auto-fit; `s4a` one-line `Skipped`/`Hotkey unavailable`. |
| S-05 | Export menu opens on the selected clip; the result toast names the file (saved) or says cancelled | `[L]` | **PASS** | S6b (`ff11f84`): export from the page, toast names the file; `AUDIT-release-visual` §1.4. Post-export window visibility is a host defect (`AUDIT-release-logic` P1, S12), not this row. |
| S-06 | Settings invalid input: inline error, prior value kept and nothing lost | `[L]` | **PASS** | `s7` §A hotkey conflict (`08`): inline `Could not register … kept Alt+J`, field returns to Alt+J, config unchanged. |
| S-07 | OCR text is searchable from the page (marker row is the same clip the search finds) | `[L]` | **PASS** | S9b; `evidence-s10\README.md`: OCR filled the image row live, search finds it. |
| S-08 | Empty tab: actions that cannot run are not shown as dead disabled buttons | `[U]` | **FAIL** | `AUDIT-release-visual` §3 note: disabled `Edit clip` remains in the empty-tab header. |

---

## 7. Browser surfaces (WebView2 = Chromium)

| ID | Row (failable) | M | Status | Evidence |
|---|---|---|---|---|
| B-01 | Scrollbars, `::selection` and caret are themed in both themes (no default blue/grey) | `[L]` | **PASS** | `AUDIT-impeccable-web` P2-3 + App. C: selection 13.26/10.64, caret 4.66/7.06, thumb 3.50/3.64; `AUDIT-release-visual` §1.9 confirms the tokens. Thumb is not visible at rest when the body overflows — that is W-04. |
| B-02 | Times, counts and the index use tabular numerals | `[L]` | **PASS** | `AUDIT-impeccable-web` positive findings; `AUDIT-release-visual` §1.9. |
| B-03 | Every focusable control shows a visible focus ring | `[L]` | **PASS** | `AUDIT-release-visual` §1.9: 2 px accent ring on every control it tabbed; `AUDIT-impeccable-web` focus set. Focus *order* is K-09, not visibility. |

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
- No font glyphs as icons; drawn 16/20 px strokes only (currently violated by E-05).
- Colour lives only in the two token blocks in `styles.css`; components name tokens.
- No layout animation; selection never animates geometry; `prefers-reduced-motion` is honoured.
- Ortu's warm paper and `docs\DESIGN-contrast.md` are history, not authority.
- `previewLines` keeps its 1..100 range; `durationMs` stays milliseconds.
- macOS keep-clips model is rejected: deleting a tab deletes its clips.

## 10. Audit scoring (unchanged dimensions, 0–4)

Accessibility (contrast, focus, keyboard, reduced motion) · Performance (latency, scroll, memory, one
renderer) · Theming (tokens only, dark + light first-class) · Platform conformance (tray, hotkey, DPI,
WebView2 surfaces) · Adaptivity (widths, zoom, DPI, toast positions). Every open row must be PASS or carry
a written owner-accepted deviation in the release gate below.

## 11. Design inputs, in order

`docs\DESIGN-language.md` §3 (contract) → `UI\web\src\styles.css` token blocks (only colour home) →
`ClipList.tsx` / `rich.tsx` / `EditorOverlay.tsx` / `SettingsDialog.tsx` / `icons.tsx` (components) →
`docs\design-evidence\` + `docs\AUDIT-release-visual.md` (evidence) → `docs\AUDIT-impeccable-web.md` +
`docs\AUDIT-ui-code.md` (anti-reference). The first integrated build of a surface must already satisfy
§0 — not "built then polished".

## 12. Release gate

Cutover requires **all 54 rows PASS**, or a written owner-accepted deviation for the named row. A
deviation is not a PASS and is recorded in this section before the release commit.

Currently not PASS — these 22 must flip:

`G-01` list gate · `G-03` editor gate · `G-04` settings gate · `G-06` native toast + tray ·
`R-02` index column at 100 · `R-08` three `+N` meanings · `C-02` disabled explanation ·
`C-04` hover · `W-02` zoom 125 · `W-03` settings fits 900×700 · `W-04` settings 430 overflow cue ·
`W-05` toast/export at 430 · `K-05` Ctrl+E · `K-06` typing guard in search · `K-07` real IME ·
`K-08` tag-chip keyboard · `K-09` settings focus/trap · `E-01` editor focus trap ·
`E-03` save invalidates body · `E-04` character count · `E-05` toolbar icons · `S-08` dead disabled Edit.

The remaining 32 PASS rows are regression-locked: any change to the code under them returns them to
NOT RUN until re-proven, and `G-01`–`G-06` are re-run by the fresh visual agent on the release artifact.
