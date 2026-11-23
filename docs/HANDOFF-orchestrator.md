# HANDOFF — orchestrator state (written for context compaction, 23 Nov ~16:30)

Read this together with: AGENTS.md (rules), CURRENT_STATE.md (position), AI_LEDGER.md (traps),
docs\CHECKLIST-features.md + docs\CHECKLIST-uiux.md (the acceptance source of truth),
docs\CORE-POLISH-HANDOFF.md (Core semantics S3 must consume).

## The mission and the owner
- Windows 11 clipboard manager "Clip", C# WPF net10.0-windows, at C:\Users\mayan\CascadeProjects\Clipboard.
- Owner: non-coder, multi-monitor, types Hindi (IME gate matters), LEFT ~13:00 for ~5 hours with the instruction:
  "continue building end to end without waiting, ensure everything is completed, audited and all bugs fixed when I am back."
- Their standing rules: I (the orchestrator) PLAN ONLY; ALL implementation goes to sub-agents on model
  **`opencode-go/deepseek-v4.1-flash#high`** (DeepSeek V4.1 Flash 1017 — ALWAYS the #high variant, corrected mid-chat).
- Verification protocol (owner-mandated): no self-reported green. [H] = scratch-harness assertions with observed
  values; [L] = live measured; [U] = a FRESH #high sub-agent that built nothing takes and reads its own screenshots.
  Builders never mark [U]. Harnesses live in %TEMP%\opencode, link REAL files, get deleted after.
- Sprint pattern that worked: disjoint file ownership per agent, pinned cross-sprint API contracts in the briefs,
  red-loop-first on bugs, two-strikes-then-split, git checkpoints between stable states.

## Pipeline position RIGHT NOW
- 26 sub-agent runs complete: M1 build+live-verify · S1 capture/rich/images · S2 settings+notification · dedupe(41) ·
  core-fix(5 research fixes) · image-fidelity(24) · tabs-Core(75) · font statics (ink-proven) · settings-rework
  (tabs + live ghost preview, 69) · Core polish (127, mutation-verified) · Axis-1 spec-truth (146 asserts, 4 mutations
  RED) · Axis-2 standards (build green, retry consolidation) · visual audit · critique (20/40) · library spike ·
  research passes: logic(Ditto/CopyQ/PowerToys), image, ortu-ui, motion, tabs, keyboard-ux, features+scale, typography,
  contrast, layout, anti-slop.
- **IN FLIGHT: S3 integration** (launched ~16:00; the big assembly: themes/tokens → flat-row popup rebuild →
  F2 editor → App wiring → live acceptance run; full brief is in this chat's S3 launch message and mirrored in the
  checklists). Session ID per launch: `ses_df818663cffeysNZQleXeEP4N2` — WARNING: two follow-up attempts with that
  string failed "session not found" (transcription issue). The authoritative ID arrives in S3's completion
  <subagent> message — use THAT for any follow-up. A follow-up was attempted to hand over
  docs\CORE-POLISH-HANDOFF.md; content is NOT lost (S3's brief already pinned GetRich/TabId/Restore/
  RecoveredFromCorruption/Skipped; the doc covers the rest, chiefly: **Update() drops rich bodies by design — the F2
  editor must extract RTF from its RichTextBox BEFORE calling Update**). Deliver that point to S3 when its
  completion message provides the working ID.

## After S3 reports (in this order)
1. Commit a checkpoint sweeping everything (core polish files + S3) with attribution lines.
2. Walk S3's live acceptance results against docs\CHECKLIST-features.md [L] items; mark only what has observed evidence.
3. Launch the FRESH-EYES visual audit (#high, built nothing): screenshots of every surface dark+light, walks BOTH
   checklists, scores the 5 dimensions (docs\CHECKLIST-uiux.md §8), marks [U] items PASS/FAIL with evidence files.
4. Launch the fix sprint (#high) closing every audit FAIL + P0/P1 (+ remaining tracked items: off-UI-thread image
   decode, settings file-budget split, stale-refs doc claims, any acceptance gap). Re-verify.
5. Run the ca-milestone-audit release report (dual-axis; it self-fixes non-business blockers): Axis 1 = spec truth
   vs both checklists incl. mutation spot-checks; Axis 2 = engineering/DB/hygiene. Deliver the 1-page release
   certificate + 3-minute owner sign-off checklist.
6. Leave the final app RUNNING; write the owner's return report in plain English (per the notes below).

## Owner's return report must cover (their explicit asks)
- "Everything completed, audited, all bugs fixed" — with the honest exceptions list (never claim untested things).
- The real Excel→Excel paste test is THEIRS to do (S3 can only test with what's installed — Ditto-research also
  proved the mechanism; if no Excel on machine, say so).
- Items only the owner can verify: tray icon pinned outside overflow, real Esc keypress (VM couldn't synthesize),
  real mouse-drag on the preview ghost, second-monitor behavior if applicable, IME composition with their own IME.
- The measured numbers: RAM idle ≤150 MB + ceiling 250 MB protocol, hotkey→popup <150 ms, exe size (reported,
  untargeted).

## Open decisions — DEFAULTS STAND until owner speaks
- Cap counts UNPINNED items only (pinned/tab items extra, protected). Flip to cap-on-total on their word.
- Binary size: untargeted (old <1 MB target physically dead with packages) — reported only.
- RtfPipe (2021-frozen): kept as fallback-only.
- Row density: 65px meta-always (critique's 34px variant rejected; flip on sight at review if they dislike).

## REJECTED deviations — do not re-propose
- Preview-lines range 1..100 default 10 (anti-slop doc's 1–3 rejected); duration stays `durationMs` (not seconds);
- No cloud/LAN sync, AI transforms, scripting, diff, QR, image editor (research NEVER list);
- No MVVM framework/DI containers (original spec, reaffirmed).

## Environment facts that matter
- A stale dev Clip.exe keeps running (rebuilt/restarted by several harnesses) — it OWNS Alt+V and LOCKS
  bin\Release\Clip.exe (MSB3027 on in-place builds: use `-p:OutDir=%TEMP%\...` while it runs; kill instances before
  final in-place builds).
- Shell screenshots need SetProcessDpiAwarenessContext(-4) first (DPI-unaware otherwise).
- %APPDATA%\Clip holds the owner's real config.json (SACRED — first-run-hint tests rename it and MUST restore) and
  real clip.db (69+ rows incl. test fixtures from agents — acceptable; owner can Clear History).
- Git: repo initialized this session; commits: 2a45392 baseline → 3f5e4b7 axis-2 → eaef710 fonts → 45c8cb7 settings.
  UNCOMMITTED right now: Core polish files, docs\CORE-POLISH-HANDOFF.md, checklist/doc updates — sweep at next
  checkpoint. Note: core polish + S3 report messages contain their full details if re-needed.
- Packages on net10: HtmlRenderer.WPF 1.6.1, H.NotifyIcon.Wpf 2.4.1 (menu via manual positioned open —
  ShowContextMenu routing fails on Win11 overflow), RtfPipe + System.Text.Encoding.CodePages
  (Encoding.RegisterProvider REQUIRED at App startup or first RTF render crashes).
- Fonts: static Manrope-Regular/Medium/SemiBold.ttf landed (variable ExtraLight removed — ink-proven);
  OFL.txt included; FontFamily-from-code trap in AI_LEDGER.

## Where the truth lives (read in this order after compaction)
1. docs\CHECKLIST-features.md / docs\CHECKLIST-uiux.md — every requirement, status, and evidence type
2. AGENTS.md — layout map, gates, standing rules  3. CURRENT_STATE.md  4. AI_LEDGER.md — all 15 traps
5. docs\CORE-POLISH-HANDOFF.md  6. docs\RESEARCH-*.md + docs\DESIGN-*.md + docs\AUDIT-*.md + docs\CRITIQUE-design.md
7. docs\RESEARCH-features-and-scale.md §Part A = the post-MVP roadmap (M4/M5/never) — NOT current scope.
