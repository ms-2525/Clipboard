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
- ⬜ `[U]` Empty state (no clips yet / no search matches) designed, not blank
- ⬜ `[U]` Hover states on rows and interactive controls
- ⬜ `[U]` Copy pulse feedback (~380 ms flash) when an item is pasted — Ortu's signature moment

## 2. Settings restyle (Ortu)
- ⬜ `[U]` `App` background with `Surface` cards (not all-Raised rows)
- ⬜ `[U]` 9px uppercase section labels
- ⬜ `[U]` 38×22 switches for toggles; orange segmented control for theme/time-format where applicable
- ⬜ `[U]` Inline validation errors in accent-red, never modal popups
- ⬜ `[U]` Clear-history confirm is an inline two-click swap

## 3. Typography & color
- ⬜ `[U]` Manrope embedded (pack URI) renders everywhere — headings, rows, meta, settings, notification; no fallback font anywhere visible
- ⬜ `[U]` Scale: distinct, obvious steps (13px row body, 10px meta, 13px SemiBold section headings) per Ortu spec
- ⬜ `[U]` Dark theme: all text ≥ 4.5:1 contrast on rendered result (Muted #8A909C on Surface #13151B verified by measurement, not assumption)
- ⬜ `[U]` Light theme legible end-to-end (shadows, borders, selection rail re-tinted, not just inverted)
- ⬜ `[U]` Notification: app name in accent, timestamp muted, body readable at a glance (~3 lines)

## 4. Motion & animation (spec: docs\RESEARCH-motion-ui.md — adopt all)
- ⬜ `[U]` Popup open: opacity 0.70→1 + scale 0.985→1, **140 ms, KeySpline(0,0,0,1)** — first frame already 70% visible so the <150 ms hotkey budget is intact
- ⬜ `[U]` Popup close: **83 ms** on Esc/focus-loss; **0 ms on paste** (Ortu + Maccy close instantly — an exit ghost swallows the next click)
- ⬜ `[U]` Notification: 150 ms fade + clip-free 0.98→1 settle (no slide on SizeToContent); never steals focus, never sounds
- ⬜ `[U]` Hover 100 ms / row groups 75 ms / selection 100 ms — all overlay-Opacity, never brush animation
- ⬜ `[U]` Selected-row growth: MaxHeight 18→54, **120 ms** — the one sanctioned layout animation, with the key-repeat snap guard; **manual test: hold ↑/↓ — no jank** (audit gate)
- ⬜ `[U]` Copy pulse: 150 in + 80 hold + 150 out = Ortu's exact **380 ms** as one keyframe animation (sage/green per Ortu)
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
