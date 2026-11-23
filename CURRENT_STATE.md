# CURRENT_STATE.md — Clip

LANE: feature

## Position (post-synthesis — full detail in docs\HANDOFF-orchestrator.md)
**Design frozen:** docs\DESIGN-v2-ui.md + docs\DESIGN-v2-synthesis.md (12 shipped-app audits in docs\RESEARCH-clipboardapps\). Decisions: rich formatting in EVERY realized row via TextBlock.Inlines (shared static RichTextBox; HtmlToRtf for HTML-only), NO selected-row HtmlPanel, HtmlRenderer.WPF REMOVED, no preview pane/F3 popup (F3 read-only = future option), numbers gutter (10+ display-only), Segoe UI/Consolas for row content (Manrope chrome only). Accepted deviation: directive §4's "unselected rows plain" overridden (owner's words + CopyQ reference) — gated on measured row-render cost (≤8 ms/row, ≤200 MB open).
**In flight:** S0 Core converters (RtfText bytes + HtmlToRtf) · crisp-text sprint (ClearType + Settings P0 crash fix) · synthesis done.

## Known opens for the FIX SPRINT (after v2 sprints)
- RAM hard ceiling FAIL: peak 253.7 MB (old HtmlPanel design — dies with the v2 row rebuild; re-measure after)
- Cold first open 221 ms (warm 12–32 ms) — pre-warm the first-render path
- Settings P0 crash (Field key collision) — crisp sprint in flight
- image decode + SHA-256 off the UI thread; MainWindow.xaml 482 + SettingsWindow.xaml 505 line budget splits; stale doc claims

## Sequence from here (owner directive 23 Nov: visual audits add no value — SKIPPED)
1. ✅ **Diagnostics lane DONE**: RAM leak fixed (per-open Reload gated + NotificationPreviewWindow static-_owner root removed; 0/5 windows survive GC, was 1/5; settles flat ~174 WS, peaks 224–256 with settle-back) · topmost fixed (WindowChrome.Apply(window, topmost:true) + Raise; this was also the "Alt+V did nothing" root cause) · toast 420×160 exact · ghost shows · crash.log watchers added · DB left pristine (0 items, Pinned tab, VACUUM). **OPEN OWNER DECISION: cold open 424–432 ms vs 150 target — the only lever left (+48 MB idle for ~263 ms) was declined by default; owner can override.**
2. **IN FLIGHT: popup-visual sprint** (zebra, bottom bar, side padding, glyph, lines clamp, outside tooltip, popup buttons) — NOTE: the bottom bar + per-row-status removal was PARTIALLY implemented pre-split in MainWindow.xaml (SelectionBar), ClipRow.cs (Title/Status), RowTemplate.xaml, MainWindow.Images.cs (UpdateSelectionBar) — the agent's brief covers partial state; verify at integration.
3. **IN FLIGHT: keys+editor+settings sprint** (←/→ universal, editor margins 18→8, button grammar: Popups.xaml shared styles + Settings footer + editor + tray).
4. Integration pass (queued, fires when the popup-visual sprint lands): unify/verify the pre-existing bottom-bar work; **SettingsWindow.xaml.cs Tab guard** (`OnHotkeyKeyDown` swallows Tab — one-line `if (key == Key.Tab) return;` so the footer is Tab-reachable); **editor plain-clip line spacing** (~15 DIP Paragraph auto-margins — zero them for plain documents; flagged, may be the owner's "margin" complaint); confirm Buttons.xaml label metric (12 SemiBold, Ortu) across all surfaces; full keyboard regression; screenshots; one commit.
5. ca-milestone-audit dual-axis code/spec pass → release certificate.
6. Owner's final report (destination email registered: sy3362260@gmail.com — deliver/draft there when the owner confirms).

## Open owner decisions (defaults stand)
Cap counts unpinned only · binary size untargeted · RtfPipe kept · density 65px meta-always. Rejected deviations in HANDOFF — do not re-propose.
