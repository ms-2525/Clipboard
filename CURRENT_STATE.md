# CURRENT_STATE.md — Clip

LANE: feature

## Done (all harness-verified; statuses + evidence in docs\CHECKLIST-*.md)
- M1 built, run and live-verified (S0: 3 runtime bugs found+fixed — ShutdownMode, modal-blocked startup, fields-not-properties; idle CPU +0.000 s; Alt+V popup verified at 150% DPI).
- Core complete & verified: capture (text/rich/files/image, alpha, pixel guard, debounce, Skipped) · all-formats fidelity (snapshot/restore, FileDrop, DIB) · dedupe (exact byte, 41/41) · pinned-safe cap & clear · tabs schema in flight · plain-paste injection · modifier-release.
- S2: SettingsWindow + Autostart + notification ✕/thumbnails, 69/69.
- Spike: HtmlRenderer.WPF + RtfPipe + H.NotifyIcon verified; `Interop\HtmlFragment.cs` landed; CodePages startup requirement found.
- Research docs: logic (Ditto/CopyQ/PowerToys), image, ortu-ui, motion, tabs, libraries, keyboard-ux (in flight).

## In flight (background agents)
visual audit of running app · typography / contrast / layout / anti-slop researches · scale research (1000 items + features) · keyboard-UX research.

## Next (autonomous pipeline, owner away)
1. `@impeccable` critique agent on the visual audit's evidence
2. **S3 integration** — the full build (brief: all frozen contracts + every research ADOPT list + design docs + S0 fixes + toast reuse + hotkey parser fix + PreviewLines + active-window monitor rule)
3. Live acceptance run (Excel→Excel first, then Word/browser/files/alpha/RAM protocol)
4. Fresh-eyes visual + checklist audit (per verification protocol)
5. Fix sprint, re-verify
6. **ca-milestone-audit** — dual-axis pre-release gate, self-fixing non-business blockers, release report

## Not done / deliberately deferred
docs\CHECKLIST-features.md §14 (IStream fallback, focus poll, DIBV5 behind matrix, per-app exclusion, CSS3 engine, OfficeIMO alternative, scale contract pending).

## Open owner decisions (defaults stand until they return)
- Cap counts unpinned only (pinned are extra) — flip to total-cap on their word.
- Binary size: untargeted, reported only.
- RtfPipe (2021-frozen) kept as fallback-only.
