# CURRENT_STATE.md — Clip

LANE: feature

## Position (post-S3 — full detail in docs\HANDOFF-orchestrator.md)
**S3 integration has LANDED and passed its live acceptance** (build 0/0; popup/keyboard/editor/tabs/plain-paste live-verified with measured evidence; 7 runtime defects found and fixed during its own run). Acceptance results are folded into docs\CHECKLIST-features.md's 🟡/✅ rows.

**Known opens for the FIX SPRINT (next):**
- RAM hard ceiling FAIL: peak 253.7 MB over 25 rich previews (limit 250; idle 111–113 passes) — close ~4 MB rich-preview retention, re-measure full protocol
- Settings window + tray menu + tray icon: NEVER opened live (UIA can't reach Win11 overflow) — fresh-audit attempt, else owner-verifies
- Not yet tested live: Excel→Excel fidelity, first-run hint, IME gate, Shift+Enter, Ctrl+1..9, Home/End/PageUp, context menu, focus-loss hide, notification ✕, autostart round-trip, hotkey→popup latency measure
- File budgets: MainWindow.xaml 482 lines, SettingsWindow.xaml 505 (split = row DataTemplate to its own dictionary / partial class)
- Tracked: image decode + SHA-256 off the UI thread; stale doc claims (Axis-2 list)

## Sequence from here
1. **IN FLIGHT: fresh-eyes visual audit** (#high, built nothing, own screenshots, walks BOTH checklists, marks [U] items).
2. Fix sprint (closes every audit FAIL + the opens above). Re-verify.
3. ca-milestone-audit release pass → release certificate + 3-minute owner sign-off.
4. Leave app running + the owner's return report (contents pinned in HANDOFF §"Owner's return report").

## Open owner decisions (defaults stand)
Cap counts unpinned only · binary size untargeted · RtfPipe kept · density 65px meta-always. Rejected deviations in HANDOFF — do not re-propose.
