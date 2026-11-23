# CRITIQUE — Clip, current rendered design (anti-reference for S3)

**Method:** design review only (detector not applicable, native Windows/WPF platform). This is the *delegated* Assessment A: read-only, no app launch, no source edits. Assessment B and the parent synthesis are out of scope here.
**Mode:** **OPERATE** — a keyboard-driven utility popup. Scanability, consistency, native expectations and the real usage scene outrank expression.
**Critic:** independent; wrote none of this codebase.
**Date:** 23 Nov 2026
**Target:** `C:\Users\mayan\CascadeProjects\Clipboard` — `UI\MainWindow.xaml`, `UI\NotificationWindow.xaml`, `UI\SettingsWindow.xaml`, `UI\Themes\*.xaml`, `App.xaml.cs`.
**Evidence:** the 24 images in `docs\design-evidence\`, cross-read against `AUDIT-current-visual.md`, `DESIGN-typography.md`, `DESIGN-contrast.md`, `DESIGN-layout.md`, `DESIGN-antislop.md`, `RESEARCH-ortu-ui.md`, `RESEARCH-motion-ui.md`, `CHECKLIST-uiux.md`. Every judgement below names its file.

### Evidence inventory — and one staleness warning

| Surface | Rendered evidence | Verdict |
|---|---|---|
| Popup (dark) | `popup-dark.png`, `-search-3x`, `-card1-body-3x`, `-card1-meta-4x`, `-card2-body-3x`, `-bottom-3x`, `-filtered`, `-restored`, `-emptyresult`, `-unicode-3x` | Current. XAML read confirms 560×620, card template, italic meta — screenshots match source. |
| Popup (light) | `popup-light.png`, `-light-search-4x`, `-light-meta-4x` | Current. |
| Notification | `notif-dark-3line`, `-light-3line`, `-dark-overflow`, `-dark-header-4x`, `-light-header-4x`, `-light-body-4x`, `-close-8x` | Current. Matches `NotificationWindow.xaml`. |
| Text rendering A/B | `host-aa-opaque.png` | Current. |
| **Settings** | `host-light-settings.png`, `host-dark-settings-1.png`, `settings-light-rows-3x.png` | **STALE.** Those captures show 9 sentence-case sections ("Max items", "Open popup", "Preview lines") and no tabs. `UI\SettingsWindow.xaml` (mtime 15:32, newer than the 14:56 audit binary) now has two tabs, uppercase section labels (`HOTKEYS` / `HISTORY` / `APPEARANCE` / `STARTUP` / `NOTIFICATIONS` / `PREVIEW`), token copy ("Clips kept"), segmented controls and an inline clear-history confirm. **There is no rendered evidence for the current Settings design**, so that surface is critiqued from source only, and its ~15-item checklist remains unverified. |
| Tray icon / tray menu | none | Source only (`App.xaml.cs:86,112`). |

Consequence: the two surfaces the owner complains about most (the popup and the toast) have honest pixels; Settings has none. Do not mark `[U]` Settings items from these files.

---

## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|-----------|-------|-----------|
| 1 | Visibility of System Status | 2 | Selection is a 1 px border with no position cue; "no match" and "still loading" are the same void (`popup-dark-emptyresult.png`); no scroll affordance (`popup-dark-bottom-3x.png`) |
| 2 | Match System / Real World | 2 | Plain "Hotkey / Notification / Theme" sits beside `Duration (ms)`, `Preview lines`, `Offset X`, and provenance that prints the app's own internal window name: **`Clip · 23 Nov 15:16 · ClipHotkey`** (`popup-dark-restored.png`) |
| 3 | User Control and Freedom | 3 | Popup exits are complete (Esc/Enter/del verified by the audit); drags: the toast has no keyboard dismiss, and closing Settings discards edits |
| 4 | Consistency and Standards | 2 | Same content, different leading (19 vs 17.76 DIP, audit F13); meta uses `·` but the toast uses spacing for the same pair; Settings sets `TextFormattingMode="Display"`, the popup and toast do not (`SettingsWindow.xaml:9` vs `MainWindow.xaml`/`NotificationWindow.xaml`) |
| 5 | Error Prevention | 2 | `Delete` on the selected row destroys a clip with no undo and no confirm (`MainWindow.xaml.cs:124-135`); the toast's ✕ is ≈13×15 DIP (`notif-close-8x.png`) |
| 6 | Recognition Rather Than Recall | 2 | No footer/keycap legend anywhere; no placeholder saying what is searched (`popup-dark-search-3x.png`); Settings values read as labels, so "editable" must be remembered (`settings-light-rows-3x.png`) |
| 7 | Flexibility and Efficiency | 3 | The keyboard model is genuinely good (Alt+V / ↑↓ / Enter / Ctrl+1‑9 / Del / Esc, one place); clamped not wrapping, no pin/edit accelerators |
| 8 | Aesthetic and Minimalist Design | 1 | Card per row + a three-segment meta line on all five visible rows + a boxed search inside a box; the footer is 10–20 % of each row's height and says the same words every time. This is the owner's complaint, in pixels |
| 9 | Error Recovery | 2 | Inline validation is well written and recoverable; but delete is unrecoverable and skipped-image copy speaks in MB/limit jargon |
| 10 | Help and Documentation | 1 | No tooltip, no help, no keycap legend, no teaching empty state. The only cheat sheet in the product is a settings row you cannot reach |
| **Total** | | **20/40 (50 %)** | **Acceptable — bottom of the band. Significant work needed** |

Honest reading: the score is dragged down by four heuristics (8, 10, 1, 6) that all fail for one structural reason — **furniture outranks content** — while heuristics 3 and 7 show the interaction model is already better than the visuals suggest. Fix the row model and this table moves 8→3, 6→3, 1→3 in one slice.

### Cognitive load: 5 of 8 checks fail (high)

| Check | Verdict |
|---|---|
| Single focus | PASS — the list is the only task. |
| Chunking (≤4 per group) | **FAIL** — every row ships body + app + date + 60-char window title, ×5 rows. |
| Grouping | **FAIL** — five identical containers; nothing groups anything (`popup-dark.png`). |
| Visual hierarchy | **FAIL** — in light, body, meta and search are within ~1 tone step of each other (`popup-light.png`). |
| One thing at a time | PASS. |
| Minimal choices | PASS — a list plus typing. |
| Working memory | **FAIL** — key bindings are never shown; the search scope is never stated. |
| Progressive disclosure | **FAIL** — 4 lines + provenance on every row; there is no compact state to expand from. |

All five failures are *extraneous* load from the visual system, not intrinsic to a clipboard. Four of the five disappear the moment rows go flat and meta becomes selected-only.

---

## Design Specificity Verdict

**Category-interchangeable, with a product-specific idea buried in the loudest place.**

What is specific: the keyboard contract, instant open, no detail pane, and a metadata model that knows *which app and window* a clip came from. What is generic: the container language. Bordered rounded cards on a surface, a raised rounded search box, a grey italic provenance line, middle-dot joins, `✓/✕/▾` characters — that is the default list-app texture of the 2024–26 model output. Nothing in `popup-dark.png` would look out of place in a notes app, a bookmarks app or a log viewer; the thing that makes it *Clip* (provenance) is exactly the thing printed five times down the right-hand third of every row.

The most damning specificity datum is that the design's only unique asset mislabels itself: `popup-dark-restored.png` shows `Clip · 23 Nov 15:16 · ClipHotkey` — the product attributing a clip to itself and printing the name of its own message-only hotkey window. A generic design can survive being generic; it cannot survive advertising that it does not know what it is.

Verdict: **genre-competent, product-unspecific.** Ortu-flat rows plus a selected-only provenance line is what turns this from "a clipboard manager" into *this* clipboard manager, because the row's expanded state is then the only place provenance appears — and provenance is the product's actual idea.

---

## Overall Impression

A fast, correctly-wired keyboard launcher wearing a dashboard's clothes. The engineering underneath (single keyboard handler, correct clipboard formats, PerMonitorV2, correct font loading) is better than the surface it renders. The single biggest opportunity is not a colour or a size: **delete the card and the meta line from the unselected row.** That one change fixes density (5 → 12 clips per viewport), gives selection something to *do*, removes the italic smudge, removes the repeated tool title, and makes the popup read as Ortu rather than as a list widget.

---

## What's Working (keep — measured, not assumed)

1. **One alignment rail already holds in the popup.** `popup-dark.png`: search outer left x=26 px, card outer left x=27 px (≤1 px apart); body ink and meta ink both at x=57–58. That is a 16.7 DIP shell inset and a 12 DIP card inset, and it is *already* better than the Settings dialog. The rebuild must not lose this: the layout spec's 18/50/532 rails are the same discipline, one step tighter.
2. **The card clamp behaves.** `popup-dark-filtered.png` top card: exactly four lines, ellipsis on the last line, no mid-glyph cut *inside* the card, no horizontal overflow anywhere (`popup-dark-card2-body-3x.png` — an unbreakable 120-char token wraps instead of scrolling). The clamp logic is right; only its *value* (4 lines, always) is wrong.
3. **The keyboard model is complete and in one place.** `MainWindow.xaml.cs:94-136`: Esc, Enter, Ctrl+1‑9, ↑↓, Del, all handled before the TextBox sees them; the audit verified Esc actually closes. `Ctrl+1‑9` is the single most launcher-native thing in the product and it already works.
4. (Also worth protecting: the toast's insets are symmetric — header and body ink both at x=47 px in `notif-light-3line.png` — and dark-theme body contrast passes AA exactly as designed.)

---

## Priority Issues

### C1 · [P0] The Settings surface cannot be opened by any user
- **Why it matters:** `App.xaml.cs:86` still adds `new ToolStripMenuItem("Settings") { Enabled = false }`. Fifteen settings, a theme switch, the notification preview and the clear-history confirm exist and are unreachable — and the only menu entry that mentions them says they are disabled. It is the single surface with no screenshot precisely because nobody can open it, which also means the ~15 Settings checklist items cannot be verified.
- **Fix:** one line — add an enabled item wired to `ShowDialog()`. Do it before the S3 visual pass so Settings can be audited at all.
- **Evidence:** source `App.xaml.cs:84-89`; the surface itself `host-light-settings.png` (stale but it is the only rendering); no tray screenshot exists.
- **Command:** `/impeccable harden`

### C2 · [P1] Card-soup is the whole texture, and it costs 40 % of the viewport
- **Why it matters:** every clip is a bordered, 10-radius, 6-px-gapped card whose interior is `12,10` (`MainWindow.xaml:9-23,75`). Because all five containers are equal, nothing is grouped; because each carries 4 lines + meta, only **5 clips fit in 620 px**. Ortu's flat 34 px row fits 12 in 520. The owner's "AI-slop texture" is this, and the practical cost is that finding a clip three copies back means scrolling a window that shows five.
- **Fix:** flat rows (transparent, radius 12, `12,8` padding, zero gap, 6 px list inset), one line compact, selection grows the row. Delete `Card` as a style entirely.
- **Evidence:** `popup-dark.png` and `popup-light.png` (five bordered cards, 6 px gaps); `popup-dark-filtered.png` (three cards, ~350 px of dead space below them).
- **Command:** `/impeccable shape`

### C3 · [P1] Selection is one 1 px border on a card that is the same size as its neighbours
- **Why it matters:** in `popup-dark-filtered.png` the selected card and the two below it are all four lines tall and identically filled; the *only* difference is an orange outline. In dark that outline measures 7.78:1 and works — but it is also the loudest possible mark, spent on 1 of 20 rows. In light it measures **2.35:1**, below the 3:1 non-text floor, on a surface step of 1.16:1 (`popup-light.png`) — so a light-theme user arrowing at speed loses the cursor. Meanwhile selection communicates *nothing*: the selected row is not taller, richer or more legible than the others, so the growth moment the whole Ortu pattern exists for is currently absent.
- **Fix:** tint + 2 px rail + 1 px decoration ring (contrast doc §6: the rail is the indicator, the ring is never it), and make selection change the row's *content* — 18 → 54 body, meta appears, provenance becomes readable. Selection then reads at a glance in both themes without a loud border.
- **Evidence:** `popup-dark-filtered.png`, `popup-light.png`, `popup-dark-card1-meta-4x.png` (meta identical whether selected or not).
- **Command:** `/impeccable colorize` → `/impeccable layout`

### C4 · [P1] The meta line is the least legible text in the product, and it repeats itself on every row
- **Why it matters:** `11 px Italic Muted` (`MainWindow.xaml:80-81`) is a synthetic oblique of a thin face — Manrope has no italic, so WPF shears it (`popup-dark-card1-meta-4x.png` shows the smear at 4×; `popup-light-meta-4x.png` shows it at 4.29:1 on the selected surface). It joins three facts with middle dots and prints a 60-char window title, which in the fixtures is *the same title on all five rows* (`popup-dark.png`: "OC | Windows WPF clipboard manager M1" ×5). The footer is ~20 % of a one-line row's height and ~10 % of a four-line card's — and it is the only line whose content never changes. So the row's second line is simultaneously the least readable and the most repetitive thing on screen.
- **Fix:** upright 11/16, app name 500 + time 400, two facts, no `·`, hairline above, **window title to the tooltip**, meta on the selected row only.
- **Evidence:** `popup-dark.png`, `popup-dark-card1-meta-4x.png`, `popup-light-meta-4x.png`, `popup-dark-restored.png`.
- **Command:** `/impeccable typeset`

### C5 · [P1] Truncation is silent and permanent — you paste blind
- **Why it matters:** the card clamps at `MaxHeight="76"` and nothing else exists: no "more lines" badge, no tooltip, no detail pane (`MainWindow.xaml:77-79`). `popup-dark-filtered.png` cuts a paragraph at "…then re-run the ageing re…" with no indication of how much is missing. For an owner whose clips are emails and ledgers, the product shows 4 of 30 lines and offers no way to see the rest before committing the paste. This is the one place the design actively withholds the content it exists to manage.
- **Fix:** the layout spec's badge (`+90 more lines`, `DESIGN-layout.md` §6) on the selected row, and `ToolTip={Binding Text}` with a 600 ms delay. Two bindings.
- **Evidence:** `popup-dark-filtered.png`; `popup-dark-card2-body-3x.png` (a clip that would be far longer than the clamp).
- **Command:** `/impeccable clarify`

### C6 · [P1] The bottom row is sliced through its glyphs, with no scroll cue
- **Why it matters:** the viewport ends at 913 px mid-meta-line (`popup-dark-bottom-3x.png`: glyph tops at 905, window edge at 913) and `VerticalScrollBarVisibility="Hidden"` with no thumb, fade or inset (`MainWindow.xaml:4,55`). Every open with more than five clips looks broken — the default state — and the user cannot tell "more below" from "the app cut my text".
- **Fix:** bottom inset + 6 px list padding so a row can never touch the edge, plus the 3 px overlay thumb while scrolling. Optionally the status bar gives position.
- **Evidence:** `popup-dark-bottom-3x.png` (3×), reproduced in `popup-dark.png`, `popup-light.png`, `popup-dark-filtered.png`.
- **Command:** `/impeccable layout`

### C7 · [P1] Light theme has no structure and fails contrast in three places
- **Why it matters:** App `#F2F3F5` / Surface `#FFFFFF` / Raised `#ECEEF1` / Hairline `#E2E5EA` measure 1.09–1.26:1 — the card model is *invisible* (`popup-light.png`: rows are white on near-white, only text separates them; `popup-light-search-4x.png`: the search pill's 1 px border does not exist). Accent as text is 2.35 (`notif-light-3line.png` — "Clip" in pale orange), the selected border 2.35, Muted-on-Raised 4.29. This is not a taste problem: the current theme cannot express the product's central idea, and it fails AA in the state the user looks at most.
- **Fix:** real light step, `AccentText` for type/rails, `Error`, `ControlLine`, `OnAccent`, Muted → `#586070`. All values are already computed in `DESIGN-contrast.md` §4.3/§7.2. The only risk is that nothing has been applied yet: `UI\Themes\Light.xaml` still holds exactly 7 brushes and no `AccentText`.
- **Evidence:** `popup-light.png`, `popup-light-search-4x.png`, `popup-light-meta-4x.png`, `notif-light-3line.png`.
- **Command:** `/impeccable colorize`

### C8 · [P1] Image clips render as empty cards
- **Why it matters:** `CaptureService.cs:210-219` stores `Kind="image"` with `Text` null; `MainWindow.xaml:74-84` binds only `Text` and `Meta` and contains no `Image`. A copied screenshot therefore produces a blank card with a provenance footer — indistinguishable from an empty clip, and the user pastes something they cannot see. Not visible in the evidence set only because every fixture is text.
- **Fix:** a `DataTrigger` on `Kind` swapping the body for a 40 px (compact) / 80 px (selected) thumbnail from `Thumb`. The data already exists.
- **Evidence:** source `MainWindow.xaml:74-84` + `CaptureService.cs:212`; no screenshot exists (say so in S3's audit rather than assuming it works).
- **Command:** `/impeccable layout`

### C9 · [P1] "No results" is a black void
- **Why it matters:** query "zzzzzzz" produces 840×930 px of nothing (`popup-dark-emptyresult.png`). No match and a hung app look identical; there is no clear button, no query echo, no way back except select-all-Backspace. First-run empty history will look the same. Cheapest high-value fix in the report.
- **Fix:** the designed states in `DESIGN-antislop.md` §7: `No clips match “zzzz”` + one teaching line; a first-run state with the configured keycap; a clear-× in the hash.
- **Evidence:** `popup-dark-emptyresult.png`.
- **Command:** `/impeccable onboard`

### C10 · [P2] The toast spends its brightest colour on the least important fact, then truncates three times
- **Why it matters:** `notif-dark-header-4x.png` — the app name is 12 px SemiBold in full `Accent`; the body is the clip, and it is the second thing you read after an orange word you already knew. In light that word is 2.35:1 (`notif-light-3line.png`). `notif-dark-overflow.png` cuts three logical lines mid-word with three ellipses ("then re-run the a…", "at thirteen …", "wraps multiple t…"), so a preview reads as broken sentences. `notif-close-8x.png`: the ✕ is a `TextBlock` glyph in a ≈13×15 DIP hit box, 4 px from a corner whose shadow is cut in a straight vertical seam.
- **Fix (all specified, none applied):** 3 px semantic rail, app name 11/600 in `Fg`, body first and brightest at 12.5/17, one ellipsis on the last line only, a real 24×24 Button named "Dismiss" with Esc, and a shadow that isn't clipped (or a DWM shadow).
- **Evidence:** `notif-dark-3line.png`, `notif-light-3line.png`, `notif-dark-header-4x.png`, `notif-light-header-4x.png`, `notif-dark-overflow.png`, `notif-close-8x.png`.
- **Command:** `/impeccable typeset` → `/impeccable harden`

### C11 · [P2] The popup will appear on the wrong monitor
- **Why it matters:** `ShowPopup()` places the window using `WF.Screen.FromPoint(WF.Cursor.Position)` (`MainWindow.xaml.cs:42`) while the owner contract is *the monitor of the active window* (`CHECKLIST-uiux.md` §6). For a keyboard-first launcher the cursor is frequently on another screen — you arrow there, press Alt+V, and the list materialises somewhere you are not looking. Also: the height is fixed at 620 with no work-area clamp, so on a short screen the status/bottom edge will fall off.
- **Fix:** `Screen.FromHandle(GetForegroundWindow())`, and clamp height to the work area.
- **Evidence:** source only (the audit could not test multi-monitor); cite it as source-derived.
- **Command:** `/impeccable adapt`

### C12 · [P2] Two token tables now exist, and the Settings one is private
- **Why it matters:** the Settings restyle could not edit the dictionaries, so it carries nine corrected colours as `TokMuted/TokTitle/TokLabel/TokError/TokControlLine/TokOnAccent/TokAccentText/TokDivider/TokField` inside `SettingsWindow.xaml:15-23`, swapped by `ApplyTokens()`. AGENTS.md forbids colour outside `UI\Themes\*.xaml`; more practically, the popup and toast will not get these values, so the same paragraph of text will be one grey in Settings and another in the popup — a consistency bug with a known cause. The comment says "this sprint may not edit them", so it is honest, but it must not survive S3.
- **Fix:** move the table into both dictionaries as the canonical tokens, delete the local table, delete `ApplyTokens()`.
- **Evidence:** source `SettingsWindow.xaml:12-23`.
- **Command:** `/impeccable colorize`

### C13 · [P3] Small polish that will otherwise be re-invented in S3
- Radii and hairlines are ad hoc (10 cards / 8 search / 14 shell / 12 toast; three `#000000` shadows outside the dictionaries) — `popup-dark.png` vs `notif-close-8x.png` side by side shows two different corner languages.
- The search glyph is a 12 px MDL2 `E721` at hairline weight next to 13 px text; the ✕ is a Unicode fallback; the settings chevron is `▾`, the checkbox tick is `✓` — four glyphs, two families, four weights (`popup-dark-search-3x.png`, `notif-close-8x.png`, `host-light-settings.png`).
- The toast header is top-aligned rather than on one baseline (`notif-light-header-4x.png`: "15:16" rides high; the ✕ floats above the cap height).
- CJK and emoji fall back to unrelated faces with visibly different weight inside one line (`popup-dark-unicode-3x.png`). Acceptable for a utility, but don't claim "one font everywhere".
- Settings still shows `Changes apply when you save.` (source), which `DESIGN-typography.md` §5 table row 34 says to drop — the footer's Save/Cancel already says it.

---

## Persona Red Flags

**Alex (Power User)** — the target user; the one who will keep the app.
- The keyboard model is excellent, but **nothing tells him it exists**: no footer, no keycaps, no tooltip (`popup-dark.png` — there is no bottom band at all). He will discover Ctrl+1‑9 by accident or never.
- Arrow-hold moves a 1 px border between five identically-sized slabs (`popup-dark-filtered.png`); with five rows in view, every fourth press jumps the whole viewport by a card. No scroll thumb tells him where in the history he is.
- `Settings` in the tray is **disabled**; a power user reads that as "unfinished".
- No F2 edit, no pin, no plain-text-in-place, no wrap-around at the list ends. He will notice all four within a day.

**Sam (Accessibility-dependent / keyboard-only, low vision)** — uses the list dozens of times an hour; must never lose the cursor.
- Light theme: the selected row is identified by a 2.35:1 border on a 1.16:1 surface step. That fails SC 1.4.11 and, at 150 % DPI, is genuinely hard to see (`popup-light.png`).
- The meta line is 11 px, synthetic-oblique, at 4.29:1 on the selected surface (`popup-light-meta-4x.png`) — and it is the only provenance available, read aloud with two middle dots and a 60-char tool title.
- The toast cannot be dismissed from the keyboard at all (`NotificationWindow.xaml:35-49`): a `Topmost` window blocks part of the screen for 3 s and the ✕ is a `TextBlock` with no UIA name.
- The search field has no accessible name and no visible label (`popup-dark-search-3x.png`).
- Whether the popup even reaches NVDA usefully is untested; the ListBoxItem template strips the container (`MainWindow.xaml:58-72`), which is what exposes selection state to UIA.

**Riley (Deliberate Stress Tester)** — clipboard content is adversarial by nature.
- A 30-line email is presented as four lines with no indication of what is missing (`popup-dark-filtered.png`) and no tooltip.
- An image clip is a blank card (`MainWindow.xaml:74-84` + `CaptureService.cs:212`) — a feature that appears to work and produces nothing.
- Emoji, CJK, RTL, tab characters and 120-char unbroken tokens: the last three are handled (wrap works, `popup-dark-card2-body-3x.png`); emoji and CJK visibly break the type system (`popup-dark-unicode-3x.png`).
- Copying while the popup is open labels the clip `Clip · ClipHotkey` (`popup-dark-restored.png`) — the provenance model silently lies in the most common state right after the popup closes.
- Query with zero matches leaves a void with the caret still in an unlabelled box (`popup-dark-emptyresult.png`).

**The owner (project persona: non-coder, judges only what is on screen, works all day between Excel/Tally and a browser)** — the person who wrote the brief.
- Summons the list expecting "the thing I copied"; is shown five cards whose dominant repeated text is a window title he did not copy.
- Chose dark, so contrast mostly holds; if he ever picks Light (a peer theme in his own product), the popup's structure disappears and the toast's brand word vanishes.
- Cares about "does it look like something a person designed": the card grid, the italic footer, the three glyph families and the disabled Settings item are the four things he can point at.

---

## The Ortu delta — where the texture is wrong, precisely

| Dimension | Current (pixels) | Ortu (pinned) | The change S3 must make |
|---|---|---|---|
| **Row container** | Bordered card, `Surface`, radius 10, `Margin 0,0,0,6`, always (`popup-dark.png`) | Flat row, transparent, radius 12, `12,8`, zero gap, 6 px list inset | Delete border, fill, margin. This is the single highest-leverage edit in the rebuild. |
| **Identity of a row** | Border + surface swap = 1 px of orange | Selection changes *content*: 1 line → 3, thumb 40→80, meta appears | Make selection structural, not decorative. |
| **Selection mark** | Full accent border at 1.5 px (`popup-dark.png`) — the loudest mark in the app, on a randomly-chosen row | 8 % tint + 1 px low-alpha ring + 2×24 gradient rail | Tint + rail; ring is decoration only (contrast doc: the ring never reaches 3:1). |
| **Border language** | One fixed hairline `#262A35` everywhere; in light, 1.26:1 | White-alpha 5–12 % in dark, ink-alpha in light; borders separate *floating* surfaces only | One `Divider` for bands/rules, one `Hairline` for floating surfaces, nothing on rows. |
| **Meta** | 11 px italic, 3 facts, dots, tool title, on every row | 10 px upright, `fg/0.32`, relative time, *no timestamps in the popup at all* | Upright, two facts, selected-only, hairline, absolute clock (owner pin). |
| **Search** | Raised pill inside the window, radius 8, 1 px, margin 12 (`popup-dark-search-3x.png`) | Flat 48 px band, bottom hairline, 14 px input, faint icon, clear-× appears with text | Flat band; placeholder at `Fg40`; a real 24 px clear target. |
| **Density** | 5 clips / 620 px | 9 clips / 450 px | At 550×520 with 34 px rows: **12 clips** plus status bar. |
| **Footer** | none | `{n} clips` + `[↵] paste` `[esc] hide`, 30 px band | Build it — it is the whole keyboard contract made visible. |

**What S3 must preserve from the audit's positives** (they are the parts already equal to Ortu):
1. The **≤1 px popup rail** (26/27 px shell inset; text at 57–58 px). Re-derive it as 18/50/532 DIP rather than re-inventing it.
2. The **card clamp's behaviour** — four lines, ellipsis on the last line, no overflow, wrap not scroll. Keep the *logic*, change the *numbers* to 1 line compact / `PreviewLines` grown with fixed `Height`.
3. The **complete keyboard model in one handler**, including `Ctrl+1‑9` on the *visible* filtered list. Add wrap-around and `IsRepeat` plumbing; do not restructure.
4. The **notification's single symmetric inset** (both header and body ink at x=47) and the 150 ms fade that already matches Ortu.

---

## The OPERATE lens (what the owner actually does with this)

**First open: can he find the newest clip in under a second?**
Dark: yes. Row 1 is the newest, pre-selected (`ApplyFilter()` sets index 0), and it is the first card under the search band at x=57 with an orange border — the eye lands there in one saccade (`popup-dark.png`). Light: marginal. The same card is white on near-white with a 2.35:1 border, and the search pill above it is an unlabelled lozenge (`popup-light.png`), so the "where do I look" answer costs an extra beat — and on a first run, "is this working?" costs a second one. Verdict: **yes in dark, borderline in light**; the fix is the light palette plus the placeholder, not the layout.

**Selection legibility at speed.** This is the weakest link in the whole surface. Hold ↓ and the only change is a 1 px border moving between five same-shaped, same-height blocks (`popup-dark-filtered.png`); when it reaches the viewport edge the list jumps a whole card because `ScrollIntoView` is nearest-mode with no scroll thumb. In light the border is below the 3:1 floor. Compare against the pinned behaviour: a rail sliding down the left edge, the row breathing open, the meta appearing. **Today, arrowing is colour-only and unanchored; after S3 it is structural.** If only one thing is fixed for OPERATE, fix this.

**Meta-line signal-to-noise.** Worst measured ratio on the surface: the footer is ~20 % of a one-line row's height, and its third segment is the *same 37-character tool title repeated on all five rows* (`popup-dark.png`). The information the owner needs at a glance is "what did I copy (body)" + "from where (app)" + "when (time)". He is being given those three plus a window title, in italic, at the smallest size in the product, on rows he is not looking at. Cutting to two facts and moving them to the selected row removes both the noise and the legibility problem.

**Notification glanceability.** A clipboard toast has one job: confirm *what* was captured, in 1.2 seconds, from the corner of the eye. Today the header's brightest element is the app name in accent (`notif-dark-header-4x.png`), so the eye reads an orange word it already knew, then has to drop to line 2. In light the app name is 2.35:1 — a smudge above legible text (`notif-light-3line.png`). Add to that three mid-word ellipses on overflow (`notif-dark-overflow.png`) and the toast reads as a fragment, not a confirmation. After the pinned fix (semantic rail + `Fg` header + body first + one ellipsis) the glance lands on the content.

**Settings findability per section.** As rendered: 9 sentence-case sections, one control per row, ~15 rows in one 1077 px scroll with no grouping and no rails; headings sit 8 DIP off the row-label rail (audit F15); numeric fields read as right-aligned static values (`settings-light-rows-3x.png`), and the destructive action sits in the same visual register as "Preview lines". As source (unrendered): two tabs, six uppercase section labels, cards per section, accent `Save changes`, outcome-labelled clear-history confirm — genuinely better, but **unverified**, and it puts the most dangerous control inside the same card as two harmless numbers. Recommendation: render it, then judge; and give Clear history its own last card with the ghost button right-aligned, well away from the numeric rows.

---

## Motion: statics vs the pinned spec

**What the statics show.** There is exactly one animation in the product — a 150 ms toast fade (`NotificationWindow.xaml.cs`); no Storyboard, no easing, no reduced-motion check. The popup pops at 560×620 in one frame, and a selection change is a 1 px border swap at 0 ms. So the current design is not *slow*, it is *flat*: nothing tells the eye that a thing happened.

**What will feel most transformed, in order.**
1. **Selected-row growth (120 ms, 18→54).** Today selected and unselected rows are the same size. When the selected row breathes open and closes, arrowing acquires a shape; this is also the change that fixes the OPERATE weakness in selection legibility. It is the single most felt animation in the spec — and it is only possible once rows are flat and compact.
2. **The flat-row rebuild itself, which is not an animation at all.** 5 → 12 clips per open changes the feel of the product more than any 100 ms curve. Do not let motion work displace it.
3. **Popup entrance (140 ms, 0.70 → 1, scale 0.985).** Correct and low-risk: at 620 px a 0.985 scale is 9 px of growth, invisible as movement and merely removes the "slap". Keep the 0.70 floor — a fade from 0 would read as slow on a hotkey.
4. **Selection overlay fade (100 ms) + hover (100 ms).** Small, but it is what makes the rail feel like it *arrives* rather than teleports.

**Two ordering risks the spec does not flag.**

- **The copy pulse (spec row 13, checklist §4.6 "MUST") has no surface on the primary path.** `Paste()` hides the popup *before* `PasteRequested` fires (`MainWindow.xaml.cs:81-86`), and the motion contract's own row 4 says paste closes with **0 ms** — so the 380 ms sage pulse can never be seen: the row is gone before the animation starts. It would only exist if a main window existed or if the popup stayed open on accept (neither is in contract). **Ship it when there is a surface for it** (e.g. a "captured" pulse in the notification, or the dedupe-while-open case); otherwise it is 30 lines of animation nobody can witness. This is a genuine disagreement with the checklist's MUST, and it should be recorded as a deviation rather than silently built.
- **Growth must ship *with* the flat-row slice, not before.** On today's cards, growing a row inside a bordered 76 px card animates a box; the "breathe" only reads once the compact row is a single line. Likewise, the spec's `MaxHeight` animation collides with `DESIGN-typography.md` §7 rule 3 (use fixed `Height` so trimming works) — resolve that as **fixed `Height`, animated**, and fix the motion doc's word "MaxHeight" rather than the typography.

**Reduced motion must land in the same slice as the first animation.** Nothing reads `SystemParameters.ClientAreaAnimation` today. One helper (§5.1 of the motion doc) before any Storyboard, or the `[U]` audit fails §4 immediately. It is the cheapest item in the whole UI sprint and the easiest to forget.

**Also:** the popup and toast must adopt `TextFormattingMode="Display"` before the entrance lands. Settings already sets it (`SettingsWindow.xaml:9`) and the popup does not (`MainWindow.xaml:1-6`), so today the *same* 13 px Manrope is measured with two different formatting modes in the same app — and display-mode text blurs while a scale animation runs. Expect a soft first frame; that is acceptable, but it is another reason not to animate scale anywhere else.

---

## Anti-slop verdict — the 10 checks, settled from these screenshots

| # | Check | Verdict | The screenshot that settles it | Agreement |
|---|---|---|---|---|
| 1 | Rows are not cards | **FAIL** | `popup-dark.png` — five bordered cards with 6 px gaps; the selected one is a *sixth* container variant, not a state of the same row. | Agree with `DESIGN-antislop.md` §3. |
| 2 | Accent budget ≤3 jobs | **FAIL** | `popup-dark.png` (full border on selection) + `notif-dark-header-4x.png` (accent as 12 px text) + `host-light-settings.png` (accent fill on checkbox) + caret + focus borders. Five jobs. | Agree. |
| 3 | Two type registries, ≤4 sizes | **PASS as written, thin in intent** | Popup: 13 body / 13 search / 11 meta = three sizes, no heading near body. Settings (rendered): 15/13/12/11 = four. Numerically compliant; hierarchically not, because there is no label register — hierarchy is 2 px of size and one grey. | Partly disagree with the doc, which implies the popup already fails. It does not; it fails *specificity*, not size count. |
| 4 | Meta legible and undecorated | **FAIL** | `popup-dark-card1-meta-4x.png` — italic, three facts, two middle dots, 60-char title inside the row. | Agree, strongest single failure. |
| 5 | One icon family | **FAIL** | `notif-close-8x.png` (Unicode ✕), `popup-dark-search-3x.png` (MDL2 E721 at hairline), `host-light-settings.png` (▾ chevron and ✓ tick). Four sources. | Agree. |
| 6 | Search is a band, not a box | **FAIL** | `popup-dark-search-3x.png`, `popup-light-search-4x.png` — raised pill with its own fill, border, radius and 12 px margin. | Agree. |
| 7 | Every empty state designed | **FAIL** | `popup-dark-emptyresult.png` — 840×930 px of void for a zero-match query; no first-run state exists at all. | Agree. |
| 8 | Copy is honest | **FAIL in the evidence, PASS in current source** | `host-light-settings.png` shows the old `Clipboard history / Clear` + "Delete every saved clip?" (false: pinned survive). `SettingsWindow.xaml:414-421` now reads "Delete all clips in Clipboard and Images? Pinned clips and tabs stay." / `Delete clips` / `Keep clips`. | Agree with the finding; disagree that it is still open — it is *unverified*, not unfixed. |
| 9 | One spacing ladder | **FAIL** | Off-ladder values in live XAML: `Grid Margin=16`, search `Padding 10,9` + `Margin 0,0,0,12`, card `Padding 12,10` + `Margin 0,0,0,6`, meta `Margin 0,7,0,0`, toast `Margin 0,-6,-8,0`. Visible as the 18/26/10 gap sequence in `popup-dark.png`. | Agree. |
| 10 | Theme purity + light contrast | **FAIL** | `popup-light.png` (structure 1.1–1.26), `notif-light-3line.png` (accent text 2.35), plus a *new* violation: nine hardcoded brushes in `SettingsWindow.xaml:15-23`. | Agree, and add the new one. |

**Where I disagree with the research docs (pixels, not taste):**
- `DESIGN-antislop.md` §4.1 P5 says the selected row may carry "an optional 1 px low-alpha ring". `DESIGN-contrast.md` §6 computes every candidate ring at 1.27–1.98:1 — it can never be the indicator. Build the rail and the tint; treat any ring as decoration, and do not let a "ring" creep back in as the focus cue.
- `DESIGN-layout.md` §11 R1 — its "enforceable" table still pins `MetaText 10/14`, `SectionHeader 9`, `StatusBar` text 9, which `DESIGN-typography.md` overrides (meta 11, section 10, status 11). An implementer following the layout doc's R1 to the letter will build the superseded values. The typography doc must win, and the layout table needs a one-line note saying so.
- The same doc's row table assumes **meta on every row** (65 px compact, 6 visible) while typography §6.4 argues **meta on the selected row only** (34 px compact, 12 visible). Both are "spec"; they cannot both ship. My recommendation, from the OPERATE lens: selected-only. It is the only version where selection carries information, and it is the only one that pays for itself in clips-per-open.
- `RESEARCH-ortu-ui.md` §13 says keep `AllowsTransparency=True`; the typography doc then forbids ClearType forever. I side with the checklist's compromise (Display + Grayscale everywhere, DWM/ClearType as a later experiment) — but note that the compromise is only defensible if the *same* rendering options are applied to all three windows, which is not true today.

---

## Keep-list for S3 (do not lose these while rebuilding)

1. Popup single content rail — ≤1 px alignment, text/meta sharing one left edge (`popup-dark.png` measurements: 26/27 px, ink 57–58).
2. Clamp behaviour — ellipsis on the last visible line, wrap never scroll, no horizontal overflow (`popup-dark-card2-body-3x.png`).
3. The keyboard model in one handler, including Ctrl+1‑9 against the *visible* list (`MainWindow.xaml.cs:94-136`).
4. Notification's single symmetric inset and the existing 150 ms fade.
5. Dark theme's base contrast (Fg 18.25, Muted 5.69 on Surface) and the app/surface step at `#08090C` — the dark palette does not need re-solving, only new tokens.
6. The embedded font's exact rendering (advance match 127.39 vs 127.3 DIP) — the *files* are now correct 400/500/600 statics in `Assets\Fonts\`; do not re-open the "which font" question.
7. PerMonitorV2 + work-area-based placement (fix the *screen source*, not the architecture).
8. The palette's existing discipline: no gradients, no glass, no pills, no status dots, no hero.

---

## Direction confirmation — does the pinned Ortu + design-docs direction resolve every complaint?

**1. Bad-looking text — YES, with one residual.**
The docs found the real root causes rather than guessing: the shipped variable font rendered at its 200 default with no reachable bold and a synthetic oblique, greyscale AA from `AllowsTransparency`, an 11 px fake-italic line, and pure-white body on a dark shell. The statics are now actually in `Assets\Fonts\` and in the csproj, and every visual decision (13/18 body, `Fg55`, 11/16 upright meta, Display mode, no size below 10 except tracked labels) is written down with a source. That resolves what `popup-dark-card1-body-3x.png`, `popup-dark-card1-meta-4x.png` and `notif-light-body-4x.png` show. **Residual:** only Settings carries `TextFormattingMode="Display"` + Grayscale; the popup and toast still render with different metrics, so "the text looks uneven" can survive the token fix. Apply the window block from `DESIGN-typography.md` §4 to all three windows in the same slice.

**2. Contrast — YES for dark, YES for light *once applied*, NO today.**
Dark already passes and the docs only add headroom (`Muted` 5.69 → 7.03, tints capped at 8 %). Light is measured, unfixed and unownable: `Light.xaml` still has 7 brushes, so `popup-light.png` and `notif-light-3line.png` are exactly what the owner will see if he switches theme tonight. The direction resolves it — the corrected hexes exist, are computed against the worst surface, and are internally consistent — but the *work is a token move, not a design decision*, and it is not done. One structural caveat the docs already state correctly: accent can never be small text in light; the toast's app name must change colour, not just value.

**3. Alignment — YES.**
The rail maths (popup 18/50/532, settings 20/35/405, toast 15) are derived rather than eyeballed, they match Ortu's own derivation, and the one rail that already works in the popup is the proof the method is right. The settings dialog's 8 DIP heading/label offset (`host-light-settings.png`, F15) is exactly what the rails fix. One caution: the layout doc's R1 table still quotes superseded type values (above), and the *current* Settings source is 714 px wide with a 672 px column — a different geometry from the layout spec's 440. Pick one before building; I would keep the wide column only if the two tabs stay, because a 440 px dialog with two tabs and 15 rows is a worse fit than a 672 px one at this row count, but it no longer reads as a Windows utility dialog.

**4. AI-slop texture — YES, decisively.**
This is where the pinned direction earns its keep. Every tell I can point at in the evidence is named and answered by a specific subtraction: card-per-row → flat rows; italic grey meta with middle dots → upright two-fact meta under a hairline on the selected row only; raised search pill → flat band; `✓ ✕ ▾` + MDL2 → one drawn stroke family; letter-"C" tray logo → drawn mark; nine sections of equal cards → five grouped sections; "Delete every saved clip?" → consequence-named confirm. Nothing in the direction adds decoration to compensate — which is the thing that usually turns an anti-slop pass into a second slop pass. Two things to watch, both visible in the plan rather than the pixels: the ghost notification preview (dashed rectangle + centred uppercase label, `NotificationPreviewWindow.xaml:14-20`) is itself an annotation pattern, but it is owner-mandated and confined to Settings, so keep it and accept it; and the "copy pulse" must not become a permanent tint that reads as a state instead of a confirmation (the motion doc already says this — hold it to it).

**One complaint the direction does *not* address:** density. None of the four docs decides whether the popup is 34 px compact rows with provenance on selection (12 clips visible) or 65 px rows with provenance always (6 clips). That choice is worth more to the owner's daily experience than any remaining colour decision, and it must be made *before* S3 writes a row template — everything else in the row spec depends on it.

---

## Questions to Consider

- If the list showed twelve clips instead of five, would the owner ever notice a colour? (If yes, the colour work is second.)
- What is the popup actually for: *finding* a clip, or *confirming* the one you just copied? The selected-only meta answers "confirming"; the always-on meta answers neither.
- If provenance is trustworthy enough to show at all, why is it the only text that is italic, greyed and 11 px — and why does it name the app's own hotkey window?
- What would the toast look like if it were allowed to be one line of the clip and nothing else?

---

*Assessment A only. Assessment B (detector) does not apply on WPF; the parent run owns the A/B synthesis and the follow-up questions.*
**Questions skipped: delegated read-only run.**
