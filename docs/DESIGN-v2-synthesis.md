# DESIGN-v2-synthesis — frozen deltas from 12 clipboard-manager audits

**Status:** frozen 23 Nov 2026. **Amends** [`DESIGN-v2-ui.md`](DESIGN-v2-ui.md); owner law is
[`DESIGN-v2-directives.md`](DESIGN-v2-directives.md) — their one disagreement is stated, not hidden (§1.5). **Evidence:**
the 12 files in `docs\RESEARCH-clipboardapps\`, `AUDIT-s3-visual.md`, `RESEARCH-features-and-scale.md` §B10, and the theme
tokens in `UI\Themes\Dark|Light.xaml` (the only colour homes). **Rule:** per question, the split, then one mechanism —
smallest thing that works, no new package (none was needed), no config knobs.

---

## 1. Q1 — rich text in the rows (the central question)

### 1.1 Evidence across the 12
**2 of 12 render rich in the list; 10 do not.** CopyQ renders *every visible row* as its own HTML document, survivable only with hard caps (100 KB / 1024 chars/line / 4096 lines), a "…" pill, disposal one screen away and measure hysteresis (sub-⅓-line grow, sub-1-line shrink ignored). ClipAngel builds formatted rows as runs **once per row**, using **one shared static RichTextBox** as parser/measurer (behind a per-paint bitmap — its worst bug). The rest flatten: Maccy ("the list never looks ragged"), Ditto (RTF drawing **off by default**, "could increase memory usage"), Klipper, GPaste, PasteBar (monospace), Clipy (20-char titles), Jumpcut, Parcellite, 1Clipboard (`[Formatted Text]`).

### 1.2 FROZEN — one RTF pipeline → `TextBlock.Inlines`; no renderer control per row
- **Which clips:** `kind="text"` **and** a stored `rtf`/`html` payload **≤ 32 KB**. Images stay thumbnails, files stay basenames. **Every realized row** renders, not only the selected one (§1.5).
- **RTF path:** `RtfText.ToBytes(rtf)` → `TextRange.Load(…, DataFormats.Rtf)` into **one shared static read-only `RichTextBox`** → clone `Paragraph.Inlines` into the row `TextBlock`. `Paragraph` → `LineBreak`; a WPF `Table` → cells tab-joined, one line per row. No RTF parser is written: WPF's loader *is* the parser (ClipAngel's device, minus its bitmap).
- **HTML path:** `HtmlToRtf.Convert(HtmlFragment.Extract(html))` — **the S3 converter, moved to slice S0** — then the same load. Both formats converge on one parser and one renderer. **No second converter, ever.**
- **Fallback (never a label, never a half-render):** no payload, payload > 32 KB, parse throws, > 300 runs, or **no run carries a non-default attribute** → today's plain `Preview` binding. That last test stops Word's "plain-looking RTF" from rendering in a different family than an identical plain clip.
- **Cache and recycling:** `id → Inline[]` template, **32 entries**, clone-on-realize (`new Run(run)`), cleared with the list on hide / reload / tab switch. Re-parsing per scroll frame would cost 1–3 ms × 2–3 rows/frame; cloning is ~50× cheaper. Recycling safety: a container's `TextBlock.Inlines` are rebuilt in `DataContextChanged` and emptied on release — a recycled row can never show the previous clip's runs. The shared box is `Document.Blocks.Clear()`ed after each load and holds no document between rows.
- **Type = content type:** Segoe UI 13/18 for every content run (Manrope has **no Devanagari**); monospace → Consolas; document font families are **not** honoured (a missing family renders per-glyph garbage); size clamped **11–20 px**; links → `AccentText` + underline, **not clickable** (a row click pastes).
- **Legibility clamp:** a run colour with `|luma − luma(App)| < 60` (0–255) becomes `Fg75` — a browser's black text is otherwise invisible on the dark theme.
- **Height is never measured from rich content:** it stays v2 §1.2's estimator (`16 + lines×18`, 6-line/124 px cap) and the render is **clipped** into it. CopyQ's measure-hysteresis is therefore unnecessary — a deterministic estimator cannot twitch.
- **Overflow affordance:** the existing **`+N` badge** (first line, right edge 532, sentence in the tooltip). **No "…" pill** — it needs an inline widget inside the render and duplicates the badge. **IME:** rows are read-only `TextBlock`s; the `Key.ImeProcessed || _composing` gate stays first in `OnPreviewKeyDown`.

### 1.3 Caps

| Cap | Value | On breach |
|---|---|---|
| payload (rtf bytes or html fragment) | **32 KB** | plain row (tooltip + F2 still show everything) |
| inline runs per row | **300** | plain row |
| rendered chars | **2 000** | stop after the 6th rendered line (clipped) |
| document font size | clamp **11–20 px** | clamp |
| inline template cache | **32 items** | drop oldest |

### 1.4 Budget — and how it is proven

| Metric | Budget | Measurement |
|---|---|---|
| realised *formatted* rows | **≤ 12 at once** | counter in the builder, at 3 positions of a 1000-row list |
| build, miss / hit | **≤ 8 ms / ≤ 1 ms p95** | `Stopwatch` around the build, 100 misses, Release |
| single layout while arrowing | **≤ 16 ms**, p95 ≤ 8 | B10 #6 recipe |
| working set, popup open, 1000 items, 20 rich | **≤ 200 MB**; hard ceiling **250 MB** (253.7 MB audit peak) | `Process.WorkingSet64`, B10 stress script |
| live `HtmlPanel` instances / new packages | **0 / 0** — `HtmlRenderer.WPF` ends unused, dropped in S1b after a zero-use grep | grep + counter; `Clip.csproj` diff |

### 1.5 The selected-row `HtmlPanel` is **DELETED**, not kept alongside
1. Redundant: it existed only to reveal formatting that every row now shows. 2. It is the measured hog: 25 live previews peaked **253.7 MB**, and the audit's 4 previews already reached **230.5 MB**. 3. It is a second renderer: two fidelity curves, two bug surfaces, one job. 4. Deleting it makes selection a pure tint change — the smallest selection path, zero layout risk.
**Cost:** HTML fidelity drops to the RTF subset (tables flatten; CSS backgrounds/floats/web fonts/inline images lost); `HtmlRenderer.WPF`, `HtmlPanel` and `RichCss()` leave with it. **Conflict with directive §4, stated:** §4's last sentence ("unselected rows keep the plain clamped preview") and "the HtmlRenderer panel already wired" describe the RAM-era mechanism; the owner's words in the same message ("show the rich text at the main window itself") and the app he named (**CopyQ renders every visible row**) require the opposite. This doc follows his words, records the deviation, and keeps reversal a **one-predicate** change (`RichRow.AppliesTo(row)` → `row.IsSelected`), no other edit.

---

## 2. Q2 — search highlighting

**Evidence:** ClipAngel bakes **red-bold** matches into the row's runs (its best affordance). PasteBar highlights yellow **and** pulses 1→1.2→1 over 300 ms, scrolling the hit into view. Ditto injects colour markup into the string it rasterises. Maccy bolds in place, "never recolouring the row". Klipper and GNOME highlight nothing (listed among their weaknesses).

**FROZEN:** runs from the same §1 builder (`UI\RichRow.cs`); no overlay, badge or pulse. Ranges from **`Core\SearchService.cs`** — `MatchRanges(preview, terms)` → ≤ **20** `(start,length)` pairs, `OrdinalIgnoreCase`, the *same* splitting the filter uses (what filters is what highlights). A match run changes **`Foreground` to `AccentText`, nothing else**: no `FontWeight`/`Size`/`Family` (SemiBold widens advances and re-wraps inside the row — the no-relayout rule), no background fill (four layers already own the row fill). Highlighting is the only reason a plain row leaves `Text="{Binding Preview}"`; with an empty box, plain rows pay nothing. **No scroll-to-match** — selection already does the minimal `ScrollIntoView`, and a second scroll per keystroke is the double-scroll B7 warns about. Debounce stays 120 ms (B10 #5). `AccentText` is the one token audited for contrast on both `App` and the `RowSelected` tint, in both themes.

---

## 3. Q3 — one row-state grammar, zero geometry

**Evidence:** PasteBar keeps a **fixed 2 px border** and changes brush only — six states, including `ring-2` + `scale 0.98` for the keyboard row. Ditto zebras odd/even, splits focused/unfocused selection colours, adds a 2 px "already pasted" bar. Maccy's selected row is one 24 px line at **accent 0.8 with forced white ink**. CopyQ uses alt rows + a 1 px outline for the keyboard-current row + hysteresis.

**FROZEN — Clip already ships the right grammar; kept and completed.** The popup hides on focus loss, so keyboard-current **is** selected (no second channel):

| State | Element | Token | Motion |
|---|---|---|---|
| selected / keyboard-current | `SelLayer` fill, radius 12 | `RowSelected` (8 % accent) | 100 ms opacity |
| selected edges | `Ring` 1 px inset + `Rail` 2×24 left | `SelectionRing`, `SelectionRail` | 100 ms |
| hover (unselected; `SelLayer` wins otherwise) | `HoverLayer` | `RowHover` (4 %) | 100 ms |
| just-copied / deduped while open | `PulseLayer` | `PulseFill`, `PulseRing` | 380 ms (150/80/150) |
| searching | text only (§2) | `AccentText` | none |

Banned here: PasteBar's 2 px slot (redundant second channel), Ditto's zebra (noise under a rail with 34–124 px rows) and its micro-icons (§7), Maccy's forced-white-on-accent (breaks theme ink), CopyQ's current-row outline (the ring draws it). **No state changes** `Height`, `Width`, `Margin`, `Padding`, `BorderThickness`, `CornerRadius`, `FontSize`, `Visibility` of a layout element, or `Stretch`. Reduced motion: pulse skipped, tints snap.

---

## 4. Q4 — the numbers gutter, exact

**Evidence:** CopyQ's gutter is **0-based** (its own documented cost: a mental translation). Ditto numbers the **first ten** only, with a divider and a configurable start. Maccy right-aligns `⌘1…⌘9` at **0.7 opacity** on the first nine. Jumpcut shows an index chip above its single card. GPaste uses a 3-char dim right-aligned column with `Ctrl+0…9`. Klipper has none.

**FROZEN** (directive §3, made exact): **slot** = content-grid column 0, `Width=22`, `VerticalAlignment=Top`, no background, no glyph, **no tile**; text `11 / 18` SemiBold, `NumeralAlignment=Tabular`, `TextAlignment=Right`, right edge **x = 40** (inset 6 + margin 12 + 22). **Value** = `index + 1` of the **visible list**, set in `ApplyFilter` where `SectionLabel` is set today — never the DB id, never 0-based; same chip on every tab (the digit pastes the Nth item of the *open* tab), Images tiles use the v2 §1.4 pill. **Colour** `Fg55` → `Fg75` when selected; never `Fg35` (fails 4.5:1 on the tint). **1–9** use the existing `Ctrl+1..9`; **10+ are display-only labels** — no `Ctrl+0`, no `Ctrl+10` (the row shows "10", so a `0` shortcut would be an invisible chord, §8); tooltip and status bar both say `Ctrl+1–9`. **≥ 1000 items:** the number may grow **leftwards into the 12 px content margin** (8 px still clear of the window edge) — never wraps, trims or clips, never enters the 10 px gap; four digits ≈ 26 px, using 4 px of margin. Nothing else in the row moves.

---

## 5. Q5 — preview pane / F3 popup: **NO**

**Evidence:** three of twelve own a detail surface. Ditto's **F3** is borderless, resizable, size remembered **per monitor**, with an `Added · Last used · N chars · N words` / `W×H, N%` footer. Maccy's 400 px slideout auto-opens after **1500 ms** dwell and stays shut after a manual dismiss — Maccy's own audit calls it an anti-pattern ("resizes the window under the pointer"). CopyQ has a right dock, **hidden by default**, opened from a menu (config-as-UI). The other nine have none.

**FROZEN: none.** Its four jobs are already covered — formatting by the row (§1), the big image by the Images tab (§6), the full text and paths by the tooltip (2 000 chars, 600 ms), exact content and editing by F2 (directive §5). A pane would revive the second renderer the RAM ceiling forbids (§1.5), take ≥ 300 px of a 550 px window (or resize it under the pointer), and add a surface nobody asked for. **Named upgrade path, not built:** F3 = `EditorWindow` opened read-only (one renderer, one save path), size per monitor in `Config` — the only version worth building on request.

---

## 6. Q6 — Images tab: confirmed, three amendments

**Evidence:** inline row thumbs are the norm (Ditto height-fits, Klipper `4·gridUnit`, GPaste 50 px, Maccy 340 × 40, CopyQ 320 × 240). GPaste **re-requests the full image over D-Bus on every row bind** — the anti-pattern; PasteBar base64s full-res over IPC per paint — the other one. ClipAngel stores a 100 × 20 "best fragment" because a squashed screenshot is unreadable. Shipped state per the audit: **22 × 22 DIP tiles in text rows, one rendering as a blank grey tile.**

**FROZEN: v2's dominant preview + filmstrip is right** — 538 × 263 over a virtualising 104 px strip, ←/→ walks it, `263 + 8 + 112 + 6 = 389 ≤ 395`. It is the only layout that fills the popup *and* keeps a virtualising panel (B8: no virtualising `WrapPanel` in WPF; the tile wall loses 40 % of the height the owner asked for). Honest cost: 4 full tiles, not 9. **Amendments:** (1) **Fit, never crop** — preview and tile both `Stretch=Uniform` (v2 said `UniformToFill`); a cropped screenshot is indistinguishable from any other, which defeats the owner's complaint. (2) **Decode once, never per scroll** — tiles bind the stored 96 px thumb (`DecodePixelWidth=96`), the preview decodes ≤ 1024 px once per selection off the UI thread, one large bitmap alive, dropped on swap/tab-switch/hide. (3) **No best-fragment crop yet** — capture-path code for a 104 px tile whose job is "which one is it"; the 538 px preview is the recognition surface. Everything else in v2 §3 stands.

---

## 7. Q7 — small steals: eight decisions

| Candidate (source) | Verdict | Reason |
|---|---|---|
| Whitespace glyphs for tab and edge spaces (Klipper) | **ADOPT** | Kills "why are these rows identical?" and the audit's blank-row finding (P2-5): `\t`→`⇥`, edge runs→`␣` in `Fg48`. One branch, no geometry |
| Type-to-filter without a search box (Maccy, GPaste) | **ADOPT (exists)** | `Keys.cs` already routes list keystrokes into the box, Esc clears first. Keep the box — the header's only affordance |
| "Degrade to content, never a label" (1Clipboard) | **ADOPT as a rule** | Encoded in §1.2's fallback; `[Formatted Text]`-class placeholders are banned (§8) |
| Per-type template selector, magnitude digit, micro-status icons | **REJECT** | Rows are content, not a table: type is legible from shape, and two more ciphers/icons beside the gutter is unexplained chrome |
| Hover action strip / toolbar in the row (PasteBar, Klipper) | **REJECT** | Content-only rows (directive §2); a strip needs reserved width or a Hidden→Visible jump. Actions live on right-click, F2, Delete, Ctrl+P |
| In-place "+N" expander and counted filter chips (PasteBar) | **REJECT** | Expanding on click *is* the geometry change the owner banned; Clip has tabs, not facets, and no request for one |
| Smart alignment + remember-last-index (Jumpcut) | **REJECT** | Mixed alignment reads as a bug in a 20-row list; remembering the index breaks the verified "opens on newest, Enter pastes it" flow |
| Private mode (GNOME) | **REJECT for v2, note it** | A real need but a new feature outside this wave; if it ships it must **never rewrite the clipboard** (GNOME's own bug) |

---

## 8. Q8 — anti-patterns now BANNED

1. **Menu-as-list** — history built from `GtkMenu`/`NSMenu` (Parcellite, Clipy): no scroll, no resize, no room to grow.
2. **Select ≠ paste** — a click that only re-copies (Klipper, GPaste); on Windows the popup pastes the choice.
3. **Silent destructive edit / format loss** — editing rewrites a clip to `text/plain` and drops its formats (Klipper, ClipAngel, GPaste); Clip's save contract is visible and format-preserving (v2 §4.3).
4. **Label instead of content** — `[Formatted Text]` (1Clipboard); an unrenderable clip shows its own words.
5. **Unbounded rich rendering** — a renderer per row with no caps (CopyQ's 999-item hang); §1.3 is the licence.
6. **Config-as-UI** — preference toggles standing in for a decision (GPaste's ~30 keys, CopyQ's six tabs).
7. **Per-scroll re-decode** — fetching/decoding on every row bind (GPaste, PasteBar); decode once, bounded cache.
8. **Hardcoded colours** — `gdk_color_parse("red")`, forced `Color.white`, `NSAppearance(.aqua)` (Parcellite, Maccy, Clipy); every colour is a theme token.
9. **Invisible chords, irreversible deletes** — delete via Ctrl+right-click with no undo (Parcellite): visible affordance or it does not exist; Ctrl+Z stays.
10. **Tooltip-as-detail-view** — 200 chars on hover as the only way to see a clip (Clipy).
11. **Anonymous rows** (Klipper) — **resolved, not ignored:** directive §2 forbids a meta line, so identity comes from content + number and app/time live in the tooltip and editor. The one audit finding the owner's law overrides, recorded as a deliberate trade.

---

## 9. Implementation slices (amends v2 §7; ≤ 5 production files each)

| Slice | Files | Owns |
|---|---|---|
| **S0 — text pipeline** (new, 2) | `Core\HtmlToRtf.cs` *(new)* · `Core\RtfText.cs` *(new)* | Moved out of S3 so S1 can use the converter; no second converter is ever written. No UI dependency |
| **S1a — template + row VM** (4) | `UI\RowTemplate.xaml` *(new)* · `UI\MainWindow.xaml` · `UI\ClipRow.cs` · `App.xaml` | Row anatomy, number slot, panel hosts removed, `Number`/`RowHeight`/`TextHeight`, Images-pane markup for S2 |
| **S1c — renderer + ranges** (2) | `UI\RichRow.cs` *(new)* · `Core\SearchService.cs` | §1.2 build/caps/cache/clamp, §2 `MatchRanges` |
| **S1b — wire-up + deletions** (4) | `UI\MainWindow.xaml.cs` · `UI\MainWindow.Rich.cs` · `UI\MainWindow.Keys.cs` · `Clip.csproj` | `DataContextChanged` build/release on recycle, delete `Grow/EnsureRich/DropPanel/RichCss/OnRichImageLoad`, drop `HtmlRenderer.WPF`, `PageMove` step, badge/number/tooltip plumbing |
| **S2 — Images** (5) | `UI\MainWindow.Images.cs` *(new)* · `UI\MainWindow.Tabs.cs` · `UI\MainWindow.Keys.cs` · `UI\RowTemplate.xaml` · `Core\ImageCache.cs` | §6 amendments (Uniform fit, 1024 px decode, no per-scroll decode) |
| **S3 — editor** (3, likely 2) | `UI\EditorWindow.xaml` · `UI\EditorWindow.xaml.cs` · `Core\CaptureService.cs` *(drop if untouched)* | v2 §4 unchanged (directive §5) |

**Order:** `S0` → (`S1a` ∥ `S1c`) → `S1b` → (`S2` ∥ `S3`). Never S1b before S0/S1c — it would be tempted to inline a second parser. `Clip.csproj` rides in S1b because that commit removes the last `HtmlRenderer.WPF` use (grep zero first). `MainWindow.Rich.cs` keeps the pulse and the FLIP-delta fix, loses the panel and the image branch (that owner becomes S2).

---

## 10. What the v2 spec got wrong

1. **It kept the `HtmlPanel` swap** and asserted the panel was cheap (§2.3); measured, 25 previews = 253.7 MB and 4 previews already = 230.5 MB. "One panel alive" was right; the panel itself was the problem.
2. **It rendered rich only for the selected row**, then admitted (§9.1) that a 1-line clip shows an 18 px rich strip and deferred the fix to "a separate detail pane if the owner dislikes it". The owner had already said what he wanted.
3. **It put rows in Manrope and rich content in Segoe UI** and called the Devanagari fallback "a typography decision" (§9.8); for *content* that is a correctness bug.
4. **It cropped the Images tiles** (`UniformToFill`) for looks, destroying the recognition the owner asked for.
5. **Its slice order was wrong:** S1 needs `HtmlToRtf`, which lived in S3 — following the plan as written would have produced the second HTML parser this doc forbids. Hence S0.
6. **It said "numbers 10+ are shown for completeness"** without saying what they do; dead chrome needs a sentence, and it now has one.
7. **Its `+N` badge counts CRLF twice** (audit P2-1: 30 lines → "+57") and the estimator ran on raw `\r\n`; the badge is now the *only* overflow signal, so a lying badge is worse than none — strip `\r` before counting.

---

## 11. Verification hooks (observed values, never a code read)

| # | Check | Pass looks like |
|---|---|---|
| R1 | Rich rows | 4 fixtures (Word RTF + Devanagari, browser table, HTML-only, 3-colour styled text) show bold/colour/heading size **inside unselected rows**, with nothing selected |
| R2 | Zero selection geometry | 20 × `↓` across interleaved rich/plain rows: `ExtentHeight` identical, no `ActualHeight` change, `UpdateLayout` ≤ 5 ms |
| R3 | Caps bite cleanly | a 40 KB payload and a 400-run payload each render as **plain text** — never blank, never partial |
| R4 | Memory | popup open at 1000 items with 20 rich rows ≤ **200 MB**; stress peak ≤ **250 MB**; `HtmlPanel` count **0** |
| R5 | Build cost | miss ≤ 8 ms p95, hit ≤ 1 ms p95; realised formatted rows ≤ 12 |
| R6 | Highlight | 3 typed chars → `AccentText` matches; heights and `ExtentHeight` unchanged; identical line breaks before/after (no inner re-wrap) |
| R7 | Numbers | rows 1/10/1000 read `1`/`10`/`1000` with no clipping and no content shift; `Ctrl+1..9` pastes that row of the open tab; `Ctrl+0` does nothing |
| R8 | Whitespace | a tab-only and a space-only clip are legible (`⇥` / `␣`), never an empty row |
| R9 | Images | portrait and 16:9 both fully visible in 538 × 263 (letterboxed, not cropped); first frame = stored thumb; swap ≤ 300 ms; no DB read or decode while scrolling the strip |

`[U]` for a fresh audit agent: row rhythm across the 34–124 px ladder with rich content, number alignment at 10/100/1000, `+N` vs a clipped last line, Images proportions at 150 % DPI, `AccentText` match legibility in both themes.
