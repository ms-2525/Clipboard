# Clip — UI/UX Verification Checklist

**What changed on 24 Nov (owner instruction):** rows that had already PASSED were answering a
question nobody can fail any more, and they were 58 % of this file. They now live in
`docs\CHECKLIST-archive.md`. What remains is only what can still fail. Settled rows are not re-read,
not re-proven, and not extended with new prose.

**Visual authority:** `docs\DESIGN-language.md` (Tools3000 manner, orange accent). Reference
screenshots: `%TEMP%\opencode\t3\`. Ortu is retired as authority (`docs\RESEARCH-ortu-ui.md` stays
as history). Owner decision wins over any document, always.

**VERIFICATION PROTOCOL (owner-mandated):** every `[U]` item is executed by a FRESH sub-agent on
`opencode-go/deepseek-v4.1-flash#high` — never one that wrote or orchestrated the code it judges.
It launches the app, captures and reads its own screenshots, and reports PASS/FAIL with the
screenshot. No other party may mark `[U]`. Builders mark `[H]` and measured `[L]` only.

`[U]` = screenshot read · `[H]` = harness assertion · `[L]` = live/measured · `[M]` = motion
measurement (frame counter, not a still).

---

## 0. The comparative gate — one judgement per surface, the only rows that judge "does it look right"

Each of these is a **comparison against the reference**, not a list of sub-items. A surface passes
when a reader shown the two screenshots cannot name a *structural* difference: row anatomy, type
levels, badge grammar, spacing rhythm. Sub-item ticking does not pass this gate.

- [ ] [U] **Popup list** — 3 screenshots (dark, light, long-text) beside the reference.
      Row = identity tile | body ≤3 lines | right-aligned facts. Three type levels. One badge shape.
- [ ] [U] **Popup chrome** — header / tab strip / status bar: one divider per band, 58 / 54 / 38
      rhythm, two key hints maximum, right-aligned.
- [ ] [U] **Images tab** — preview + filmstrip: one selected state, numbered tiles, no blank tile.
- [ ] [U] **Editor** — toolbar, text area, footer: three type levels, buttons hug their labels.
- [ ] [U] **Settings** — cards with icon + title + description, controls hug content, 20/24
      padding, segmented control with a filled thumb.
- [ ] [U] **Notification + tray menu** — one radius, one shadow, one type scale, no stock chrome.

## 1. Open items — each can still fail today

### Row anatomy v3 (shipped 24 Nov, unverified)
- [ ] [U] Identity tile: 28×28, app initial, appears on every row; a row with no app shows `•`.
- [ ] [U] Facts column: `+N` chip above the clock, right-aligned, stacking in one column — never
      inline in the sentence (the old `+9` bare / `+20` pill split).
- [ ] [U] Truthful badge: a 30-line clip reports the real hidden count (`+27`, not the old `+57`).
- [ ] [U] List clamps to 3 lines; the full text is reachable by the outside tooltip.
- [ ] [U] No per-row metadata line; provenance is in the bottom bar for the selected row only.
- [ ] [U] Selection reads as a surface step, not a colour cast: `RowSelected` + ring + rail, no
      brown wash, no geometry change on ↑/↓.
- [ ] [U] **`Ctrl+1..9` affordance**: rows no longer print their position (the number was the code
      gutter). Decide: a corner number on the identity tile, or a bottom-bar hint — do not drop it
      silently, it is the only visible cue that the shortcut exists.

### Type & icons
- [ ] [U] Three type levels on every surface (body / secondary / meta), no fourth grey.
- [ ] [U] Body text is Inter, not a mono or fallback face (`dark-open.png` of 23 Nov looked
      monospaced — confirm on a real clip, not a fixture).
- [ ] [U] One drawn icon family; the notification close is a Path, not a U+2715 text glyph.
- [ ] [U] Tray icon crisp at 16/24/32 and themed; context menu has no Aero2 gutter.
- [ ] [U] `Sage` / `Sky` / `Violet` are used somewhere on screen or removed from the tokens
      (last look: present in both themes, used nowhere).

### Contrast & themes
- [ ] [U] Light theme end-to-end at AA: body, secondary, meta, placeholder, selected row.
      Evidence = the screenshot plus `node %TEMP%\opencode\verify-theme.mjs` (28 pairs, 0 fails).
- [ ] [U] Dark theme: no pure-white body text, no `Fg48` below 4.5:1 on any surface it sits on.
- [ ] [U] Theme change applies live from Settings and survives a restart.

### Motion (measured, not photographed)
- [ ] [M] Popup open ≤150 ms, close 83 ms, 0 ms on paste — three numbers on one line.
- [ ] [M] Copy pulse 380 ms fires while the popup is open.
- [ ] [M] Dedupe jump-to-top FLIP 200 ms, or the documented "ship nothing rather than jank" exit.
- [ ] [M] Reduced motion: the OS "Animation effects" toggle actually switches our animations off.
- [ ] [U] Drag-to-move the popup does not break keyboard focus.

### States, platform, settings
- [ ] [U] First-run empty state (needs a renamed config — owner's data is never touched).
- [ ] [U] Ghost notification preview appears, moves, writes offsets back, auto-hides (ACC-v2 FAIL).
- [ ] [U] Tray `Clear history…` has its confirm step.
- [ ] [U] Settings: invalid input gives an inline error, saves nothing, loses nothing.
- [ ] [U] Notification: timestamp not italic; ✕ has a hover state; click-through everywhere else.
- [ ] [U] Multi-monitor: popup + toast land on the active window's monitor, DPI-correct.
- [ ] [U] No stock WPF control visible anywhere (ScrollBar, ComboBox, ContextMenu, ToolTip,
      MessageBox, focus rectangle).

## 2. Frozen decisions — do not reintroduce (one line each, no evidence needed)

- Selection NEVER changes a row's height or the list's geometry.
- No grow-on-selection and no selected-row growth animation.
- No selected-card-only rich panel: rich content renders inside every realised row.
- No per-row metadata line (app · title · time) — the bottom bar owns provenance.
- Rich previews never fetch remote images; `HtmlRenderer` stays removed.
- No `✓ ✕ ▾` text glyphs as icons — drawn Paths only.
- Ortu's warm-paper light palette is retired; `docs\DESIGN-contrast.md` is history, not authority.
- `previewLines` keeps its 1..100 range; `durationMs` stays milliseconds.
- macOS keep-clips model is rejected: deleting a tab deletes its clips.
- No layout animation is sanctioned on any surface.

## 3. Audit scoring (unchanged dimensions, scored 0–4)

Accessibility (contrast, focus, keyboard, reduced motion) · Performance (latency, scroll, memory,
one renderer) · Theming (tokens only, dark+light first-class) · Platform conformance (tray, hotkey,
DPI, click-through) · Adaptivity (monitors, text scaling, notification positions).

Every open item above must be PASS or carry a written owner-accepted deviation.

## 4. Design inputs, in order

`docs\DESIGN-language.md` (authority) → `UI\Themes\*.xaml` (tokens) → `UI\Buttons.xaml` +
`Popups.xaml` + `RowTemplate.xaml` + `Icons.xaml` (components) → `%TEMP%\opencode\t3\*.png`
(reference) → `docs\AUDIT-current-visual.md` + `docs\CRITIQUE-design.md` (anti-reference).

The first integrated build of a surface must already satisfy §0 — not "built then polished".
