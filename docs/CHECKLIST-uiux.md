# Clip — UI/UX Verification Checklist

Verified the same way as features: observed evidence only. `[U]` = screenshot read against the rendered app.

**VERIFICATION PROTOCOL (owner-mandated):** every `[U]` visual check is executed by a FRESH sub-agent on `opencode-go/deepseek-v4.1-flash#high` (DeepSeek V4.1 Flash 1017, high variant) — fresh session, never one that wrote or orchestrated the code it judges (no self-review). The audit agent launches the app itself, captures its own screenshots, reads them, and reports PASS/FAIL per item with the screenshot as evidence. No other party may mark `[U]` items.
Visual authority: Ortu (docs\RESEARCH-ortu-ui.md) — the owner pinned it. Where Ortu conflicts with an owner decision, the owner decision wins (noted inline).

---

## 1. Ortu visual language (adopted)
- ⬜ `[U]` **Flat rows, not cards**: no per-item border, radius 12, compact 8×12 padding — the app's whole texture matches Ortu
- ⬜ `[U]` **Selection = rail + tint + inset ring**: 2px accent gradient rail (#FF8A3D→#FF6B1A), overlay tint, 1px inset ring — not a full accent border
- ⬜ `[U]` **Selected row grows** 1 → 3 lines (dovetails with the rich-preview expansion)
- ⬜ `[U]` **Time sections** (Today / Yesterday / …) with hairline rules between groups
- ⬜ `[U]` **Status bar** with live hints: `{n} clips` + keycaps `↵ paste` / `esc hide` — reinforces the all-keyboard contract
- ⬜ `[U]` **Second accent sage #AEB291** in use (domains/success/pill); amber for pinned items; sky/violet for semantic rails
- ⬜ `[U]` Borders are white-alpha 5–12% (light) / black-alpha (dark), not one fixed hairline
- ⬜ `[U]` Popup **550 wide × 520 tall** (Ortu's 550; height adapted for the expanding preview — owner-approved deviation)
- ⬜ `[U]` Metadata line: 10px non-italic above a 1px hairline, **absolute time in the configured 12h/24h format** (owner decision overrides Ortu's relative time)
- ⬜ `[U]` Tab bar matches Ortu's tab treatment across Clipboard / Images / Pinned / custom
- ⬜ `[U]` **One drawn Path icon family** — the three current systems (Segoe MDL2 `E721` glyph, Unicode `✓ ✕ ▾`, letter-"C" tray logo) are all replaced by stroke-consistent drawn Paths (Ortu draws 14px SVG at stroke 2.5); tray logo becomes a drawn mark, not a letter
- ⬜ `[U]` **Meta line content**: two facts (app + time) with spacing, not middle-dot chains; the window title moves to the row's tooltip — the line itself carries app + timestamp only
- ⬜ `[U]` **Truthful copy everywhere** (docs\DESIGN-antislop.md table): clear-history copy states pinned/tab items survive; "Close" discards without destruction language; badge "+90 more lines"; status keycaps; tray menu — every visible string from the doc's replacement table
- ⬜ `[U]` **Tray Clear History gets a confirm step** (it currently wipes with none — contradicts the designed confirm path); S3 wires it
- ⬜ `[U]` Empty states designed with exact copy per the doc: first-run (no history), no search matches, empty Images, empty Pinned
- ⬜ `[U]` **Bottom-edge mid-glyph clipping fixed** (audit: the popup viewport slices a text line at y≈905 with no scroll cue) — fixed row heights + bottom viewport padding or a scroll cue in the S3 rebuild
- REJECTED deviations (owner contracts stand): preview-lines stays 1..100 (not 1–3); duration stays `durationMs` (not seconds)
- ⬜ `[U]` Hover states on rows and interactive controls
- ⬜ `[U]` Copy pulse feedback (~380 ms flash) when an item is pasted — Ortu's signature moment

## 2. Settings restyle (Ortu + DESIGN docs)
- ⬜ `[U]` **Reorganized into tabs** (General / Notification): 672px centered `App` bg, `Surface` cards p14 r12, 48px header, 13/600 title
- ⬜ `[U]` 10px uppercase section labels (supersedes 9px — DESIGN-typography.md)
- ⬜ `[U]` 38×22 switches for toggles; orange segmented control for theme/time-format where applicable
- ⬜ `[U]` **Live notification preview overlay**: a ghost overlay (dashed accent border, 10px uppercase "Notification preview" label, semi-transparent Surface fill) appears at the EXACT on-screen coordinates the real toast will use — position, offsets, width, height, on the right monitor — and moves/resizes LIVE as any value changes; dragging the ghost writes back to offsetX/offsetY; auto-hides after ~4 s idle; a pin/`Show preview` button keeps it up. Owner requirement 23 Nov.
- ⬜ `[U]` Inline validation errors use the Error token (never modal popups), checkbox borders pass SC 1.4.11 via `ControlLine`
- ⬜ `[U]` Clear-history confirm is an inline two-click swap

## 3. Typography & color (evidence-corrected by docs\DESIGN-typography.md)
- ✅ `[H]` **Static Manrope 400/500/600 shipped; the variable default-200 TTF is REMOVED** — root cause of "text looks bad": WPF cannot drive variable axes (dotnet/wpf#7758). VERIFIED: usWeightClass 400/500/600 per file, no fvar/gvar; ink measurement old variable 1393px Normal AND SemiBold (0.0% diff = the bug) vs new statics +36.5%; family names rewritten (GWfH ships source names → silent Arial fallback, caught + fixed); OFL.txt included; resources confirmed embedding exactly the three statics
- ⬜ `[U]` Manrope embedded (pack URI) renders everywhere — headings, rows, meta, settings, notification; no fallback font anywhere visible
- ⬜ `[U]` **Meta line: 11 px, weight 500, NON-italic** (italic killed: Microsoft excludes it for readability; Ortu never italicises metadata), tabular figures, 1 px hairline, selected-card treatment; **10 px is allowed ONLY for uppercase ≤3-word labels**
- ⬜ `[U]` **Contrast token fixes applied (docs\DESIGN-contrast.md — computed values, not guesses)**: Dark — Muted `#8A909C`→`#9BA1AC` (7.03 on Surface, 5.01 under tint), placeholder→`#82868F` (4.51), hover/selection tints capped at **8 %** (15 % kills corrected Muted to 3.94). Light — full semantic re-solve: AccentText `#B84900`, Error `#CC2E33`, Sage `#686C47`, Amber `#826000`, Sky `#046C99`, Violet `#7347E8`, Muted `#586070`, placeholder `#69696E` (Accent stays `#FF8A3D` for FILLS only). Both — new tokens `OnAccent` (#08090C / #FFFFFF) and `ControlLine` (#666C7C / #7F8691) so the white-on-orange tick hits 7.78 and checkbox borders 3.14/3.16
- ⬜ `[U]` **The 2px accent rail carries selection** — the accent/0.15 ring measures 1.27:1 and is decoration only, never the indicator
- ⬜ `[U]` Small-size stack: window-level `TextFormattingMode="Display"` + `TextRenderingMode="Grayscale"` — **applied to ALL windows: popup, notification, settings, editor** (critique: only Settings has it today) — pixel-verified necessity (docs\AUDIT-current-visual.md: AllowsTransparency forces greyscale AA, 0 vs 1,026 fringed pixels in a ClearType control); a DWM-composited ClearType window restructure is a fix-sprint EXPERIMENT if text still reads soft after the statics land — not the S3 path
- ⬜ `[U]` Scale corrections applied (supersede the earlier Ortu numbers): meta 10→11, section labels 9→10 uppercase, status bar 9→11, title 15 Bold→13/600
- ⬜ `[U]` `✓ ✕ ▾` removed (verified absent from Manrope's cmap — Windows was silently falling back) → MDL2 glyphs or `×`
- ⬜ `[U]` Notification: app name in accent, timestamp muted, body readable at a glance (~3 lines)
- ⬜ `[U]` Dark + light themes legible end-to-end. Where docs\DESIGN-typography.md and docs\DESIGN-contrast.md both name a token value, **contrast wins on hexes, typography wins on weights/sizes**

## 4. Motion & animation (spec: docs\RESEARCH-motion-ui.md — adopt all)
- ⬜ `[U]` Popup open: opacity 0.70→1 + scale 0.985→1, **140 ms, KeySpline(0,0,0,1)** — first frame already 70% visible so the <150 ms hotkey budget is intact
- ⬜ `[U]` Popup close: **83 ms** on Esc/focus-loss; **0 ms on paste** (Ortu + Maccy close instantly — an exit ghost swallows the next click)
- ⬜ `[U]` Notification: 150 ms fade + clip-free 0.98→1 settle (no slide on SizeToContent); never steals focus, never sounds
- ⬜ `[U]` Hover 100 ms / row groups 75 ms / selection 100 ms — all overlay-Opacity, never brush animation
- ⬜ `[U]` Selected-row growth: MaxHeight 18→54, **120 ms** — the one sanctioned layout animation, with the key-repeat snap guard; **manual test: hold ↑/↓ — no jank** (audit gate)
- ⬜ `[U]` **Row density DECIDED: 65px compact rows, meta ALWAYS visible** (grows 101/119 on selection) per docs\DESIGN-layout.md §row anatomies — the critique's 34px selected-only-meta variant rejected for glanceability; flip on sight at owner review
- ⬜ `[U]` Copy pulse: 150 in + 80 hold + 150 out = Ortu's exact **380 ms** as one keyframe animation (sage/green per Ortu) — **fires on the row just captured/deduped WHILE THE POPUP IS OPEN** (critique catch: paste hides at 0 ms, so a paste-pulse is invisible; the capture surface is where it lives)
- ⬜ `[U]` **Clipboard tab is kind-filtered (text + files only; image rows never appear as blank cards)** — images live in the Images tab with thumbnails; view queries filter by kind per tab
- ⬜ `[U]` Dedupe jump-to-top: FLIP **200 ms** — with the documented "ship nothing rather than jank" escape hatch if it fights virtualization
- ⬜ `[U]` Settings: 150 ms fade, no transform (dialog vs flyout distinction keeps surfaces non-identical)
- ⬜ `[U]` Esc-collapse of search: deliberately instant
- ⬜ `[U]` Reduced motion: `SystemParameters.ClientAreaAnimation` honored — **30-second machine check that the Windows "Animation effects" toggle actually flips it** (unverified link, flagged in research)
- ⬜ `[U]` Invariants held: GPU-only properties, one authored moment per surface, animation never blocks input, two curves by role (entrance/exit)

## 5. Keyboard-first affordances (owner contract)
- ⬜ `[U]` Focus is ALWAYS visible on the selected row (rail + ring readable on both themes)
- ⬜ `[U]` Search caret visible on open (CaretBrush themed)
- ⬜ `[U]` Keycap hints in the status bar match the actual bindings (incl. F2 edit, Ctrl+Shift+V plain paste once shipped)
- ⬜ `[U]` Delete-key context matrix honored visually: deleting an item gives feedback (row removed + list stays at sane selection); editor Del behaves as text editing
- ⬜ `[U]` Drag-to-move popup does not break keyboard focus

## 6. Windows & platform conventions
- ⬜ `[U]` No taskbar entry; no Alt-Tab entry for popup/notification (WS_EX_TOOLWINDOW)
- ⬜ `[U]` Tray icon crisp at 16/24/32px, themed, context menu matches the app's visual world (H.NotifyIcon swap)
- ⬜ `[U]` Multi-monitor: popup + notification land on the monitor of the CURRENT ACTIVE WINDOW (not the mouse — owner requirement), correctly scaled (PerMonitorV2 verified at 150%); popup follows the hotkey press's active window, notification follows the copy's
- ⬜ `[U]` Notification click-through everywhere except the ✕; ✕ hover state visible
- ⬜ `[U]` Per-monitor DPI: text crisp, no blurry rescale on monitor switch

## 7. States & edge cases
- ⬜ `[U]` Empty history / empty search / empty Images / empty Pinned states all designed
- ⬜ `[U]` Very long text item: clamp + ellipsis, no layout blowout
- ⬜ `[U]` Wide/tall image thumbs in grid: uniform, no stretch distortion
- ⬜ `[U]` Rich preview worst cases render acceptably (huge table clipped with scroll or max-height; rowspan>3 defect documented, not hidden)
- ⬜ `[U]` Settings with invalid input: save blocked, error inline, no data loss on Close

## 8. Audit scoring (final gate)
The UI audit agent scores the rendered app 0–4 on five dimensions and every ⬜ above must be PASS or have a written owner-accepted deviation:
1. Accessibility (contrast, focus, keyboard completeness, reduced motion)
2. Performance (popup latency, scroll smoothness, memory discipline, only-one-renderer-alive)
3. Theming (token usage, dark+light first-class, no hardcoded colors)
4. Platform conformance (tray/hotkey/DPI/click-through/Windows expectations)
5. Adaptivity (multi-monitor, text scaling, notification positions)

## 9. Design inputs (the no-slop gate — S3 builds right the FIRST time)
S3's UI implementation must consume, in this order, before writing any XAML: docs\RESEARCH-ortu-ui.md (visual authority) → docs\RESEARCH-motion-ui.md (motion spec) → docs\DESIGN-typography.md + docs\DESIGN-contrast.md + docs\DESIGN-layout.md + docs\DESIGN-antislop.md (the design wave, in flight) → docs\AUDIT-current-visual.md + docs\CRITIQUE-design.md (incumbent sins as anti-reference). The design is not "built then polished" — the first integrated build must already satisfy every checklist item above, verified by a FRESH #high audit agent per the verification protocol.
