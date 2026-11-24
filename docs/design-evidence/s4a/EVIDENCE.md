# S4a — search, keyboard, live capture (web list)

Built from `UI/web` on `v2-phase1`; production `vite build`, driven in headless Edge at 900×700
(plus the narrow sizes), `deviceScaleFactor: 1`, `?mock=1` fixture of 1,000 clips. The screenshots
in this folder are the unedited captures.

## Screenshots

| File | What it shows |
|---|---|
| `900-light-query-unknown-tag.png` | query `foo: reconciliation`, 244 hits, hint `no tag named “foo” — searched as text` |
| `900-dark-query-unknown-tag.png` | same query and hint in dark |
| `900-light-empty-no-results.png` | `Nothing matches “zzzznothingmatches”` |
| `900-light-keyboard-selection.png` | row 03 selected by ArrowDown with the 2 px focus ring and the selection rail |
| `430x400-search-open.png` | 430×400, search open with `reconciliation`; the tall row starts at its first line |
| `900-light-tag-chip-filter.png` | tag chip click filled `link:` and focused the box |
| `900-light-delete-confirm.png` | Delete asks first; Cancel is focused |
| `900-light-recapture-to-top.png` | a re-copied clip (same id) is row 01 with its new time, selection still on id 1 |
| `900-light-events-toasts.png` | `Skipped` and `Hotkey unavailable` as quiet one-line toasts |

## Keyboard → observed action

| Key | Observed |
|---|---|
| `↓` / `↑` | selection id 10 → 27 (next matching row) in the filtered list; focus follows the row when the list had focus |
| `Home` / `End` | first result (id 2) / last result (id 997); the list scrolls to it |
| `PageDown` | index 0 → 2: the two tall rows actually in the viewport (measured, not a fixed count) |
| `PageUp` | back to the first result |
| `Enter` | paste; host answered “not ready” in mock → copy fallback, toast `Copied instead` naming the row |
| `1`–`9` | `3` pasted the **third visible** row (label 03, id 10); after the filter the labels renumber and the toast names that row |
| `Ctrl+Shift+V` | plain paste path; same copy fallback in mock |
| `F2` | editor overlay opened; `↓` while open changed nothing (id 1 before and after); `Esc` closed |
| `Ctrl+F` | search box focused (value kept) |
| printable on the list | `a` focused the box and made the query `a` |
| `Esc` | first press cleared the query and restored the tab rows; second pressed `hide()` |
| `Ctrl+P` | pinned id 1; the refresh kept id 1 selected; toast `Pinned` |
| `Delete` | confirm dialog appeared; `Esc` cancelled; confirming removed the row (7 → 6) and moved selection to the nearest row (1 → 2) |
| `Ctrl+Z` | restored the clip, re-selected id 1; toast `Restored` |
| IME | `ArrowDown` with `isComposing` / `keyCode 229` left the selection unchanged |

With the **search box focused**: `↓` still moved selection (focus stayed in the box), `Enter` pasted,
`3` was not intercepted (`defaultPrevented: false`) and typed `3`, `Delete` asked before deleting,
`Esc` cleared the query.

Live capture: scrolled to 420 px with the anchor row at −110 px; a `captured` event for a **new** id moved
scrollTop to 485 px while the same row stayed at −110 px and the selection stayed on id 1. With the list
at the top, the new row became row 01 and scrollTop stayed 0.

Re-copy of an existing id (the store's dedupe bump, sent as `captured` with the same id): clip 10 moved
from index 4 to index 0, its time changed 01:38 → 03:30 and its provenance title became
`Recaptured · …`; `shown` stayed 497, selection stayed on id 1, scrollTop stayed 0. While scrolled to
420 px, re-copying clip 11 (below the fold) moved scrollTop to 521 px (the moved row's 101 px) and the
anchor row id 5 stayed at −110 px with selection on id 1: the data moved, the viewport did not.

`skipped` shows `Skipped` + the host's reason (`Image skipped: 18.4 MB over the 12 MB limit`).
`hotkeyFailed` shows `Hotkey unavailable` + `Alt+V is already used by another app.` Both are quiet
(non-error) toasts; each event produces one toast.

Pending search: 7 rows were on screen before typing and still 7 immediately after (no blank flash).
The unknown-tag hint did **not** move the list (`list-wrap` top shift = 0).

## Overflow — `scrollWidth − clientWidth`

Both themes, after a fresh load at each size. Body and list are both 0 and no element crosses the
viewport edge.

| Size | Light | Dark |
|---|---|---|
| 900×700 | 0 | 0 |
| 720×600 | 0 | 0 |
| 600×466 | 0 | 0 |
| 480×400 | 0 | 0 |
| 430×400 | 0 | 0 |

Index past 99: row 497 carried a 23 px index (3 digits) with a 10 px gap to the body, overflow 0 at
900 and 430. CSS is `min-width: 2ch` with `flex: 0 0 auto`, so the column grows by one `ch` instead of
pushing into the padding.

## Contrast (new pairs)

Computed WCAG ratios; all ≥ 4.5:1.

| Pair | Ratio |
|---|---|
| `Fg48` hint on `Surface` (light) | 5.14 |
| `Fg48` hint on `Surface` (dark) | 5.67 |
| `Danger` on `Surface` (light) | 6.57 |
| `Danger` on `Surface` (dark) | 6.37 |

The committed 28-pair table is untouched; no colour token changed.

## Console / network

Zero console errors, zero `http(s)` requests across the run.

## Notes

- The mock answers `paste` with `pasted:false` (“Paste is not wired yet”) so the real fallback is
  exercised. The parallel host slice now implements `paste`, `delete`, `restore`, `togglePin`,
  `moveToTab` and `hide`; `paste` can also answer `ok:true` with `pasted:false` and a reason, which the
  page treats as a copy fallback and reports honestly.
- Preview-only fixtures in `mock.ts` (`clip:mock-captured`, `clip:mock-recapture` with `{id}` and
  `clip:mock-event` with `{event, data}`) replay the three host event paths in a browser. `mock.ts` is
  never created inside WebView2, so they cannot reach a real history.
- Reproducible headless runs need `--disable-background-timer-throttling`,
  `--disable-renderer-backgrounding` and `--disable-backgrounding-occluded-windows`; without them the
  hidden page throttles the 120 ms search debounce and rAF, which looks like a product bug.
