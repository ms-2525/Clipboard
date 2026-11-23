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
**RELEASE ISSUED 23 Nov ~22:45 (owner said "cut" — Axis 1 addendum pending; it completes on its own and its findings land as a short addendum + any needed fixes).**
1. ✅ All parallel lanes + integration committed (full live keyboard regression PASS; DB integrity probe-green; RAM flat under ceiling; F2-save atomicity red-loop-proven).
2. Axis-2 milestone audit: GREEN (0 P0/P1; 7 hygiene self-fixes; P2s fixed or backlog-recorded).
3. Axis-1 spec-truth audit: running → addendum when it reports.
4. ✅ USER_GUIDE.md written (plain-English + 3-minute sign-off).
5. Owner's final report: delivered in chat; registered email sy3362260@gmail.com awaits the owner's go to draft.

## Open owner decisions (defaults stand)
Cap counts unpinned only · binary size untargeted · RtfPipe kept · density 65px meta-always. Rejected deviations in HANDOFF — do not re-propose.
