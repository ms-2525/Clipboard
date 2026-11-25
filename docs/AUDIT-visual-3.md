# AUDIT — visual sign-off 3 (page only, mock fixture)

Fresh visual pass. I did not build this page. Everything below is what I measured by printing numbers from
the built page in headless Chromium and what I saw in screenshots I took and read myself. **This is the page
only** (`UI\web\dist\index.html` with the `?mock=1000` fixture), not the native WPF shell and not the real exe.
I did not touch the app, its process, the two checklists, or `AUDIT-live-acceptance-3.md`.

## Build tested

| | |
|---|---|
| file | `UI\web\dist\index.html` |
| size | **2,865,809 bytes** (unchanged) |
| SHA-256 | `87329539F768A914DA2CB5A17D8BA59FA9FB89AA26500451632A7FB8FD7CB810` |
| built | 25 Nov 2026 10:01:24 |
| loaded as | `file:///.../UI/web/dist/index.html?mock=1000` |
| tool | Playwright 1.63.0 / Chromium 1243, headless, `deviceScaleFactor: 1` |

This supersedes the 09:49 build (`BDC5F29B…`, same 2,865,809 bytes) that the first version of this file
signed: the content changed when Known limits moved from the left column of Settings to the right. Every
number below was re-measured on `87329539…`; the 09:49 hash is the one to disbelieve.

Note: the checklist and `RELEASE-v2.md` still name an older **2,862,374 B** bundle (08:05). Nothing in this
pass speaks for that file or for an exe built from it.

### How "zoom" was applied

In the page, zoom is not a layout property: `settings.uiZoom` is persisted for the WebView2 host, which sets
its `ZoomFactor`. The faithful emulation of that (and the method the earlier 100/150 measurements used,
150 → 600×466) is the layout viewport WebView2 produces: `900×700 ÷ factor` at DPR 1.

- For each zoom I set **the page's own control** — `button[aria-label="Settings"]` → the Zoom row
  (`.radio-row[aria-label="Zoom"]`, the `[80,100,125,150]` cards) — and read the active card back:
  `100%`, `125%`, `150%` respectively. No CSS was injected.
- Screenshots are therefore 900×700, 720×560 and 600×466 CSS px (not upscaled images). A layout cannot
  overflow differently because of this; only the pixel scaling differs from the real window.
- The mock keeps config in memory, so a *reload* resets it to 100 %. "The choice survives a reload" could
  not be shown here; the control's own `data-on` state was verified in the same load.

## Row W-02 — zoom 100 / 125 / 150 %: zero horizontal overflow

Measured shape: `scrollWidth / clientWidth`. "Past right edge" = number of elements whose
`getBoundingClientRect().right` exceeds `innerWidth` and that are **not** inside a clipping scroller
(the strict scan also flagged nothing inside scrollers: 0 clipped-but-past elements in every state).

| Zoom | Viewport | (a) page `doc.documentElement` | (b) Settings `.dialog` / `.dialog-body` | (c) editor `doc` / `.editor-scroll` / `.editor-page` | Past right edge | Verdict | Screenshots |
|---|---|---|---|---|---|---|---|
| 100 % | 900×700 | 900/900 | 900/900 · 858/858 | 900/900 · 900/900 · 758/758 | 0 | **PASS** | `zoom-100-page.png` `zoom-100-settings.png` `zoom-100-editor.png` |
| 125 % | 720×560 | 720/720 | 720/720 · 508/508 | 720/720 · 720/720 · 686/686 | 0 | **PASS** | `zoom-125-page.png` `zoom-125-settings.png` `zoom-125-editor.png` |
| 150 % | 600×466 | 600/600 | 600/600 · 508/508 | 600/600 · 600/600 · 566/566 | 0 | **PASS** | `zoom-150-page.png` `zoom-150-settings.png` `zoom-150-editor.png` |

`document.body.scrollWidth/clientWidth` equals the `documentElement` pair in all nine states. No state has
`scrollWidth > clientWidth`, so no horizontal scrollbar exists anywhere.

The Zoom row itself is the control under test, so its four cards were measured at each width:

| Zoom | cards (`80% … 150%`) | card width | left .. right of the row | inside the dialog |
|---|---|---|---|---|
| 100 % | 4 visible | 90 px | 459 … 849 | yes (dialog 20…880) |
| 125 % | 4 visible | 109 px | 121 … 589 | yes (dialog 100…620) |
| 150 % | 4 visible | 109 px | 61 … 529 | yes (dialog 40…560) |

No card is clipped at any of the three zooms: each card's `scrollWidth` equals its `clientWidth`.

### What I saw in each screenshot

- `zoom-100-page.png` (900×700) — top bar with brand, search + `Ctrl K`, theme/settings buttons, tab strip
  `All 1,000 · Pinned 0 · Images 2 · Work 0 · Design 0 · Snippets 0 · +`, "1,000 shown", `Edit clip F2`,
  `Save as`, the selected row 01 with its meta right-aligned, footer `Esc hide`. Nothing cut, nothing
  overlapping the window edge.
- `zoom-100-settings.png` — 860×581 dialog, two columns; GENERAL/CAPTURE left, APPEARANCE/TIME/DATA/
  KNOWN LIMITS right, all readable; the four zoom cards in one row with `100%` highlighted; `Cancel` /
  `Save changes` in the footer. No scrollbar and no fade at this size (the zero-scroll evidence is
  `zoom-100-settings-final.png`, below).
- `zoom-100-editor.png` — header `Editing clip · Text · Chrome · API reference · 09:57`, `Cancel` /
  `Save clip` on the right, one toolbar row, white page with the URL, footer `35 characters · 1 format ·
  Saved to this clip · Esc Close`. No overlap.
- `zoom-125-page.png` (720×560) — identical structure; top bar keeps one row, tab strip fits, list rows
  wrap normally. `1,000 shown` is gone (see below). No horizontal bar.
- `zoom-125-settings.png` — 520×528 dialog (518 px inside the border), body scrolled to where clicking
  `125%` left it; the row shows all four cards with `125%` highlighted, the helper line "Zoom is saved as
  soon as it changes; Ctrl + / Ctrl − step it." fits on one line, TIME and DATA below are intact, the
  dialog head and footer are both fixed in place.
- `zoom-125-editor.png` — header intact, toolbar wraps to two rows (`Aa`, size, undo/redo, Clear,
  Single line on the second), every control reachable, page 686 px wide centred in 720, footer present.
- `zoom-150-page.png` (600×466) — brand row, then search with `Ctrl K`, then the tab strip; nothing is
  clipped and the footer keeps `Ctrl T tags · Ctrl Shift V plain · Esc hide`.
- `zoom-150-settings.png` — 520×434 dialog in a 600 px viewport, body stopped where clicking `150%` left it:
  a sliver of "Toast lines" under the head, `Plain text only`, then APPEARANCE with the four zoom cards and
  `150%` highlighted, the helper line, TIME `24-hour`/`12-hour`, then the DATA heading at the fade. Nothing
  horizontal is cut and the footer is intact.
- `zoom-150-editor.png` — header + two-row toolbar, all toolbar buttons visible, page 566 px wide inside
  the 600 px scroller, body text readable; the page footer is below the fold (vertical scroll only,
  `.editor-scroll` scrollHeight > clientHeight is expected).

**Verdict W-02: PASS at 100 %, 125 % and 150 %** — zero horizontal overflow on the page, the Settings
dialog (and its scrolling body) and the editor (and its scroll + page) at all three zooms.

## Delta — Known limits moved into the right column (10:01 rebuild)

Same emulation, same controls, re-run on `87329539…`. This is the change that was supposed to end the 95 px
scroll at 900×700.

| State | `.dialog-body` `clientHeight / scrollHeight` | Vertical overflow | Fade + chevron | `.dialog` | Body `clientWidth / scrollWidth` | Past right edge |
|---|---|---|---|---|---|---|
| 100 % (900×700) | **444 / 444** | **0** | absent | 860×581 (was 860×668) | 858/858 | 0 |
| 125 % (720×560) | 391 / 762 | 371 | present | 520×528 | 508/508 | 0 |
| 150 % (600×466) | 297 / 762 | 465 | present | 520×434 | 508/508 | 0 |

**Zero-scroll at 100 % now passes**: `scrollHeight === clientHeight` (444), no `scroll-fade` element in the
DOM, and the dialog is 87 px shorter than before because the left column no longer carries Known limits.

Column geometry at 100 % (rects, `left..right / top..bottom`), read from the live DOM:

- left column: GENERAL `41..436 / 142.5..334.5`, CAPTURE `41..436 / 348.5..576.5`
- right column: APPEARANCE `464..859 / 142.5..249.5`, TIME `464..859 / 263.5..314.5`,
  DATA `464..859 / 328.5..408.5`, **KNOWN LIMITS `464..859 / 422.5..572.5`**
- footer `21..879 / 584.5..639.5`

So DATA ends at 408.5, Known limits starts 14 px below it, the note ends at 572.5 and the footer starts
584.5 — 12 px of clearance. The pair-wise group overlap scan returned `[]` (no two `.set-group` boxes
intersect), and no text node inside the dialog is wider than its own box.

What I saw, `zoom-100-settings-final.png`: the full dialog, no scrollbar and no fade; Known limits sits under
DATA in the right column with its four sentences intact ("Colour emoji…", "HTML from Google Docs…",
"OCR text is there…", "Excel turns a leading zero, 007…") and `Save changes` below it. Nothing overlaps the
Data row or the footer.

`zoom-125-settings-final.png` (body at rest): GENERAL, CAPTURE, Toast lines, Plain text only, then the
APPEARANCE heading cut by the fade — the zoom row is **56 px below the fold** here, i.e. one wheel notch.
`zoom-125-settings-final-bottom.png` (body at its end): theme row, the four zoom cards with `125%` active,
the "Zoom is saved…" line, TIME, DATA, then Known limits fully readable above the footer.
`zoom-150-settings-final.png` (body at rest): zoom row **150 px below the fold** in a 297 px viewport;
`zoom-150-settings-final-bottom.png`: TIME, DATA and Known limits complete, footer clear. No horizontal
scrollbar in either, and nothing past the right edge (600/600, 720/720).

The 125/150 % residual scroll is the dialog's own height, not this change: 762 px of content cannot fit in
391 or 297 px without scrolling, and the fade + chevron reports it.

## Row W-05 — toast and export menu at 430 width

Viewport 430×700, DPR 1, zoom 100 % (set with the same control). Toast triggered by the real keyboard path
(`Ctrl+1` → the mock answers `pasted: false`, so the page shows its fallback toast). Export menu opened with
the row's own `Save as` button (`aria-haspopup="menu"`).

| Element | `scrollWidth / clientWidth` | Text clipped? | Past right edge | Verdict | Screenshot |
|---|---|---|---|---|---|
| page (`documentElement`) | 430/430 | — | 0 | — | both |
| toast `.toast` | 331/331 (box 75.4 … 408, 22 px from the edge) | no | 0 | **PASS** | `430-toast.png` |
| toast title / sub | 245/245 · 245/245 | no; both wrap inside the 380 px cap | 0 | **PASS** | `430-toast.png` |
| export menu `.export-pop` | 152/152 (box 255 … 409) | no | 0 | **PASS** | `430-export.png` |
| menu items (3) | 140/140 each | no | 0 | **PASS** | `430-export.png` |

Menu items as measured: `Save as… Ctrl E`, `Text (.txt)`, `Markdown (.md)` — each 140 px client and
scroll width, all inside 430.

### What I saw

- `430-toast.png` — toast bottom-right: check icon, **"Copied instead"**, sub **"Paste is not ready yet -
  Chrome · API reference"** on one line, dismiss ✕; it sits above the footer and does not reach the edge.
  Full sentences, nothing ellipsised.
- `430-export.png` — 152 px menu under `Save as`, right-aligned: `Save as…` with its `Ctrl E` keycap on the
  right, `Text (.txt)`, `Markdown (.md)`. Every row complete, no wrapping surprise, no clipping. It overlaps
  row 01's preview text as a dropdown does; the menu itself stays inside the viewport.

**Verdict W-05: PASS** — toast 331/331 and export menu 152/152 at 430 width, no clipped or ellipsised text
in either, nothing past the right edge.

Re-run on the 10:01 rebuild rather than carried over from the 09:49 one: the numbers came back identical —
toast `scrollWidth 331 / clientWidth 331` at `75.4 … 408`, menu `152/152` at `255 … 409`, items `140/140`
each, zero elements past `innerWidth`, and the only text wider than its box in the frame is still the
`Snippets` tab label (51/46). The toast reads "Copied instead" / "Paste is not ready yet - Chrome · API
reference"; the menu still lists `Save as… Ctrl E`, `Text (.txt)`, `Markdown (.md)`. The change was Settings
only and the screenshots confirm it changed nothing here.

## What still looks wrong (none of these is a W-02 / W-05 failure)

1. **Tab strip at 430: `Snippets` becomes `Snip`, with no visible ellipsis.** Measured `.tab-label`
   `scrollWidth 51 / clientWidth 46` (5 px short); a 3× crop (`430-tabs-crop.png`) shows `Snip` then `+`,
   no `…`. The full name is only in the `title` attribute. This is the ≤560 px shrink rule doing what its
   comment says, but the result silently drops four characters. Not the toast and not the menu, so W-05 is
   unaffected.
2. **The Settings body still scrolls at 125/150 %, and the Zoom row sits just under the fold.**
   `scrollHeight − clientHeight` of `.dialog-body`: **0 px** at 900×700 (fixed by the Known-limits move),
   **371 px** at 720×560 and **465 px** at 600×466 — 762 px of content in a 391/297 px viewport. At rest the
   zoom cards sit 56 px (125 %) and 150 px (150 %) below the fold, so reaching the control that fixes zoom
   costs one scroll, not the deep one it used to. The fade + chevron reports that more exists.
3. **`1,000 shown` disappears at 125 % and 150 %** (`.shown-count` is `display:none` below 840 px). At
   150 % the row has room for it; at 125 % it goes from visible (900) to gone (720). Design choice, but it
   is a visible loss of information for a non-obvious reason.
4. **The editor loses its clip-kind chip at 150 %.** `Text` shows at 100 % and 125 %, is hidden at 600 px
   (`.editor-chip` is dropped under `@media (max-width: 640px)`). Intentional, worth knowing.
5. **A tooltip sits over the top bar's shortcut hint after closing Settings.** Esc returns focus to the gear,
   which opens its Radix tooltip over `Enter paste` / `Ctrl K`. Cosmetic, dismisses on the next key or pointer
   move; it had to be dismissed before the 430 toast shot, so no current screenshot shows it.
6. **Bundle identity**: this pass signs `87329539…` / 2,865,809 B (10:01). The 09:49 build had the same size
   and the hash `BDC5F29B…`; the checklist's 2,862,374 B bundle (08:05) is neither.

## Screenshots (all in `%TEMP%\opencode\visual3\`)

| file | state |
|---|---|
| `zoom-100-page.png` `zoom-100-settings.png` `zoom-100-editor.png` | 100 %, 900×700 |
| `zoom-125-page.png` `zoom-125-settings.png` `zoom-125-editor.png` | 125 %, 720×560 |
| `zoom-150-page.png` `zoom-150-settings.png` `zoom-150-editor.png` | 150 %, 600×466 |
| `zoom-100-settings-final.png` | 100 %, Settings at rest — the zero-scroll shot |
| `zoom-125-settings-final.png` `zoom-125-settings-final-bottom.png` | 125 %, Settings at rest / at the end of the body |
| `zoom-150-settings-final.png` `zoom-150-settings-final-bottom.png` | 150 %, Settings at rest / at the end of the body |
| `430-toast.png` `430-export.png` | W-05, 430×700 |
| `430-tabs-crop.png` | 3× crop of the 430 tab strip (finding 1) |
| `measure.json` `measure-delta.json` | every number printed above, raw (full sweep / Known-limits delta) |

## Scope

Page only — React bundle with the in-page mock host (`?mock=1000`), headless Chromium, no WebView2, no WPF
shell, no real clipboard, no real zoom factor. The native window's own chrome, the tray, the notification
popup and the real 900×700 window borders are outside this pass.
