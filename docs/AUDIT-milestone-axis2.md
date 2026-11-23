# AUDIT — Milestone, Axis 2: Engineering Standards & Data Integrity

**Run:** 23 Nov 2026 · pre-release milestone gate (`ca-milestone-audit`, Axis 2)
**Auditor:** fresh AXIS-2 sub-agent — wrote no Clip code before this run; independent eyes.
**Baseline:** git `1c59d27` ("Integration: gutter/Tab-guard/line-spacing gaps closed + full keyboard regression PASS live + fixture cleanup + evidence").
**Contract sources read first:** `AGENTS.md`, `AI_LEDGER.md`, `docs\CHECKLIST-features.md` §15, `docs\AUDIT-axis2-standards.md` (prior standards audit, re-verified below).

**Scope:** compile gate · hygiene · colour law · layering · file budgets · 12-line duplication · DB integrity (read + live probe) · stale docs · EOL/encoding · git cleanliness.
**Out of scope by mandate:** business semantics (dedupe, cap, tabs, pinning, skip rules, timestamps) — findings only, never edits; `docs\CHECKLIST-*.md` — never edited.

## Verdict

| Area | Result |
|---|---|
| Compile gate (`-t:Rebuild`, Release) | **GREEN** — 0 errors, 0 warnings |
| Hygiene (TODO/FIXME/stub/debug) | **PASS** — 0 hits across 36 `.cs` + all `.xaml` |
| Catch reasons | **PASS after self-fix** — 5 silent catches got their one-line why; the rest already carried one |
| `shortcut:` comments | **PASS** — 3 present, all carry limit + upgrade trigger |
| Colour law | **PASS** — 0 hex literals outside `UI\Themes\*.xaml` (the old `#FF6B6B` exception is now the themed `Error` token in both dictionaries) |
| Layering | **PASS** — no `using Clip.UI` in `Core\`/`Interop\`; only the documented `Core↔Interop` pair |
| 12-line duplication | **PASS** — 0 identical windows; semantic duplication: PNG decode ×5 (P2) |
| File budget (400) | **FAIL — 4 files over** (P2; real line counts) |
| DB integrity | **PASS** — WAL+NORMAL, atomic capture/dedupe/restore, `image_hash` index used, corrupt-DB recovery verified live |
| Stale docs | **4 stale checklist claims (off-limits) + 2 live-guidance lines fixed**; historical research left as record |
| EOL / BOM | **3 BOMs, 4 mixed-EOL files** (P3) |
| Git clean | **NO** — `USER_GUIDE.md` untracked (P3) |

**Axis-2 verdict: GREEN for release.** No P0 (build/data broken) and no P1 (shipped feature broken) finding exists. Every prior Axis-2 defect that was marked fixed stayed fixed and is re-proven below. The open items are P2/P3 standards debt with named owners; none block shipping.

---

## Gate evidence

```
$ dotnet build -c Release -t:Rebuild -p:OutDir=%TEMP%\opencode\msd2-gate\
  Clip -> C:\Users\mayan\AppData\Local\Temp\opencode\msd2-gate\Clip.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:06.97
```

- The stale instance (PID 100452) was stopped before the build and **relaunched afterwards** (new PID 33020), so the owner's tray app is running again.
- Re-run **after all self-fixes**: `Build succeeded. 0 Warning(s) 0 Error(s)` (6.37 s). No fix introduced a warning.

---

## P0 — none

## P1 — none

---

## P2

**A2-20 — Four files exceed the 400-line budget (`AGENTS.md` standing rule)** *(measured with `ReadAllLines`; prior audits used `Measure-Object -Line`, which skips blank lines and under-reported these)*
- `UI\MainWindow.xaml.cs` **448**
- `UI\SettingsWindow.xaml` **444**
- `UI\SettingsWindow.xaml.cs` **433**
- `App.xaml.cs` **424**
- At the line exactly: `Core\HtmlToRtf.cs` 400, `UI\MainWindow.xaml` 400 (pass).
- The documented `ClipboardStore` split **did** land and holds: `ClipboardStore.cs` 149 / `ClipboardStore.Items.cs` 321 / `ClipboardStore.Tabs.cs` 144 (was 429). `MainWindow` was split by feature (`Tabs`/`Keys`/`Images`/`Rich`/`Tooltip`), but the residual code-behind and the Settings window pair remain one file each.
- Not restructured mid-audit (a split is a behaviour-risk refactor, not hygiene). Recommendation: split `SettingsWindow.xaml` into General / Notification `ResourceDictionary`s and `MainWindow.xaml.cs` into `Reload`/`Selection` partials, one slice each, `[H]`-verified after.

**A2-21 — PNG→`BitmapSource` decode duplicated 5× (was 3× at the last audit)**
- `Core\CaptureService.cs:259`, `Interop\PasteInjector.cs:161`, `UI\ClipRow.cs:164`, `UI\MainWindow.Images.cs:148`, `UI\NotificationWindow.xaml.cs:116` — all five are the same `MemoryStream` → `BitmapImage` → `CacheOption.OnLoad` → decode → `Freeze` ritual, differing only in `DecodePixelWidth`.
- Risk: the `OnLoad`+`Freeze` discipline is exactly what prevents the "file locked / cross-thread" class of bug; one site drifting is silent. One helper (e.g. `Core\Png.Decode(byte[], int decodeWidth)`), five call sites, no new abstraction.

**A2-22 — F2-save (`Update`) is the one format-touching mutation still not transactional**
- `Core\ClipboardStore.Items.cs:151-166`: `UPDATE items …; DELETE FROM item_formats …` run as two autocommitted statements.
- Impact: a crash/power loss between them leaves the **old format blobs attached to the new body** — the row's text and its rich payload disagree, and paste restores the stale rich content. Narrow (single-process, milliseconds-wide window) but it is the only remaining way a user sees wrong content rather than merely lost fidelity.
- Fix: `using var tx = _conn.BeginTransaction();` + `cmd.Transaction = tx;` + `tx.Commit()` — identical shape to `InsertWithFormats` (`:19-25`). Not self-fixed (behaviour-surface change; belongs to the owner of the store file).

**A2-23 — Acceptance checklists still assert dead things** *(cannot edit `docs\CHECKLIST-*.md`)*
| Location | Stale claim | Reality |
|---|---|---|
| `CHECKLIST-features.md:71` | selected card "expands into a rich preview … via `HtmlRenderer.WPF`" | selection never changes geometry (v2); renderer is `HtmlToRtf` → RTF → `FlowDocument` (`UI\RichRow.cs:138,158`); the same line's later evidence contradicts it |
| `CHECKLIST-features.md:153` | dependency list includes `HtmlRenderer.WPF 1.6.1` | not a `PackageReference` in `Clip.csproj`, not in `bin\`, zero code references |
| `CHECKLIST-features.md:175` | "Row growth uses fixed `Height` per state" (§15 scale contract) | row growth is deleted by `DESIGN-v2-directives.md` §1 — selection must not change height at all |
| `CHECKLIST-features.md:185` | "`ClipboardStore` split … + transaction wrapper" still ⬜ | split landed (`CORE-POLISH-HANDOFF.md:13`); transactions landed for insert/dedupe/restore |
| `CHECKLIST-uiux.md:57,58` | selected-row growth 18→54 (120 ms); "65px compact rows, meta ALWAYS visible" | superseded by `DESIGN-v2-directives.md` §1/§2; `CHECKLIST-uiux.md:13` already says so, 57/58 do not |
Owner/next agent should strike or annotate these; the checklist is the acceptance source of truth, so a stale line is a false target for the next builder.

**A2-24 — Working tree is not clean at the baseline commit**
- `git status` → `?? USER_GUIDE.md` (59 lines, an owner-facing guide, untracked). Not deleted or altered (possible user work); needs a commit decision.

---

## P3

**A2-25 — EOL / encoding drift** (`core.autocrlf=true`, `core.eol` unset; repo files are written LF by agents, so a future checkout will flip the whole tree)
| File | Finding |
|---|---|
| `Core\HtmlToRtf.cs` | UTF-8 BOM (400 LF lines, uniform) |
| `UI\MainWindow.Tooltip.cs` | UTF-8 BOM (162 CRLF lines, uniform) |
| `UI\MainWindow.xaml` | UTF-8 BOM + 1 CRLF among 399 LF |
| `UI\NotificationWindow.xaml.cs` | 2 CRLF among 258 LF |
| `UI\SettingsWindow.xaml.cs` | 2 CRLF among 431 LF |
| `Clip.csproj` | 4 CRLF among 35 LF (known since A2-16) |
- Report only: a mass EOL/BOM flip would bury the real diff at release time. If it is ever normalised, do it as its own commit plus a `.gitattributes` (`* text=auto eol=lf`).

**A2-26 — No index on `kind`; the Images view walks then sorts**
- Probe plan for `WHERE tab_id IS NULL AND kind = 'image' ORDER BY captured_at DESC, id DESC` uses `ix_items_tab` + `USE TEMP B-TREE FOR ORDER BY`. Fine at 200 and negligible at 1000 rows; only worth `ix_kind_captured(kind, captured_at DESC)` if the owner raises the cap by an order of magnitude. Deliberately **no** index on text dedupe (multi-MB rows) — recorded as correct.

**A2-27 — Non-hex colour literals that remain (all defensible)**
- `App.xaml.cs:295-297` — tray icon ink `Color.FromArgb(0x20,0x20,0x20)` / `0xE8` is monochrome-by-design against the taskbar theme, not a palette duplication (the old theme-duplicating `8,9,12`/`255,138,61` are gone).
- `UI\RowContent.cs:100` — `Colors.Black` fallback when a theme brush is missing; a failure default, never a design colour.

**A2-28 — Dead-component claims in historical docs** *(reported, not edited — they are the research record; the two that read as live instructions were fixed, see self-fixes)*
- `docs\RESEARCH-image-best-practices.md:21,71,92,398` — `Clipboard.SetImage` as the paste path (removed; now CF_DIB + PNG).
- `docs\RESEARCH-image-best-practices.md:342` — `ImageCache.Materialize(...)` (real API `ImageCache.Write(id, png)`).
- `docs\HANDOFF-orchestrator.md:78` — "Packages on net10: HtmlRenderer.WPF 1.6.1" (no such dependency).
- `docs\DESIGN-typography.md:266,416` — `HtmlRenderer.WPF` as the rich renderer / its base stylesheet.
- `docs\DESIGN-layout.md` §5 (row anatomy, 65 px, grow-on-selection), `docs\RESEARCH-motion-ui.md` row 12 — superseded by `DESIGN-v2-directives.md`, no superseded banner on either.
- `docs\RESEARCH-features-and-scale.md:165` — "Selected row grows (planned)".

---

## Prior Axis-2 findings — re-verified stayed fixed

| Prior | Check performed | Evidence |
|---|---|---|
| A2-01 `PreviewLines` | read | `Core\Config.cs:31` `public int PreviewLines { get; set; } = 10;` |
| A2-02 retry in one home | grep | definition only at `Interop\ClipboardFormats.cs:97`; 5 `PasteInjector` sites + 1 internal call delegate, no local copy |
| A2-04 legacy `pinned` retired | read | `Core\ClipboardStore.cs:103-104` — adopt, then `UPDATE items SET pinned = 0 WHERE pinned = 1` |
| A2-05 corrupt-DB recovery | probe | garbage file → `RecoveredFromCorruption = true`, `bad.db.corrupt-*` created, fresh DB usable |
| A2-06 image-hash dedupe | probe + EXPLAIN | `ix_image_hash` created (`ClipboardStore.cs:99`); plan `SEARCH items USING INDEX ix_image_hash (image_hash=?)`; byte compare still in the WHERE |
| A2-07 atomic writes | read + probe | `InsertWithFormats` one tx (`Items.cs:19-25`); dedupe update+formats one tx (`:126-145`); `Restore` one tx (`:65-93`); probe: row+blobs appear together, dedupe leaves 1 format blob and 0 orphans |
| A2-09 colour law | grep | 0 `#RRGGBB` outside `UI\Themes`; `Error` token `#FF6B6B` in `Dark.xaml:31`, `#CC2E33` in `Light.xaml:30`; the three `#000000` shadow literals are gone |
| A2-13 message-string migration guard | read | `AddColumnIfMissing` uses `pragma_table_info` (`ClipboardStore.cs:119-130`) |
| A2-15 store split | measure | 149 / 321 / 144 lines, three partials, zero call-site churn |

---

## DB integrity evidence (throwaway DB in `%TEMP%\opencode\msd2-db`)

Probe links the **real** `Clip.dll` built by the gate (`%TEMP%\opencode\msd2-probe`, `axis2probe.dll` run against the gate output). Output:

```
PASS WAL journal_mode  [wal]
PASS synchronous=NORMAL  [1]
PASS index ix_image_hash exists
PASS InsertWithFormats row+blobs
PASS dedupe hit moves to top
PASS dedupe replaced formats (1, no orphans)
  image dedupe plan: SEARCH items USING INDEX ix_image_hash (image_hash=?)
PASS image dedupe uses ix_image_hash
PASS Delete purges row+formats
PASS Restore same id + formats in one tx
PASS captured_at stored as TEXT  [text]
PASS size_bytes stored as INTEGER  [integer]
  GetRecent plan: SEARCH items USING INDEX ix_items_tab (tab_id=?) | USE TEMP B-TREE FOR ORDER BY
  GetTabItems plan: SEARCH items USING INDEX ix_items_tab (tab_id=?) | USE TEMP B-TREE FOR ORDER BY
PASS EnforceCap keeps main history <= cap  [2]
PASS corrupt db flagged RecoveredFromCorruption
PASS corrupt file moved aside
PASS fresh db works after recovery
PROBE GREEN
```

- `PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL` runs on every open (`Core\ClipboardStore.cs:51-56`).
- Corruption is recognised by code **11/26 only** (`:134-135`); busy/locked DBs are rethrown by design, never renamed (`:132-133`).
- Types: `captured_at TEXT NOT NULL` (lexical ordering contract intact), `size_bytes INTEGER`, no float/decimal anywhere for money-like data (none exists).
- `EnforceCap` keeps its COUNT gate (`Items.cs:304-308`) and is `tab_id IS NULL`-scoped (`:314`).

---

## Self-fixed in this run (all zero-behaviour)

| # | File:line | Change |
|---|---|---|
| 1 | `Core\ClipboardStore.cs:62` | reason comment on the `catch` that disposes a half-open connection before rethrowing |
| 2 | `Interop\ClipboardFormats.cs:106` | reason comment on the clipboard-busy retry `catch` |
| 3 | `App.xaml.cs:121` | reason comment on the second `catch` of the crash-log write |
| 4 | `UI\NotificationWindow.xaml.cs:88` | reason comment on the hit-test `catch` (click-through fallback) |
| 5 | `UI\NotificationWindow.xaml.cs:132` | reason comment on the undecodable-thumbnail `catch` |
| 6 | `docs\DESIGN-typography.md:323` | "`PreviewLines` … missing from `Config.cs`, S3 must land it" → **landed** (`Config.PreviewLines`, default 10) |
| 7 | `docs\DESIGN-v2-directives.md:38` | "the HtmlRenderer panel already wired" → the live HTML→RTF→FlowDocument row renderer |

Gate re-run after these edits: **0 errors, 0 warnings**. No business rule, no `UI` behaviour, no checklist file and no `_fonttest\` file was touched.

Remaining silent `catch` bodies were inspected and already state their outcome in code rather than prose: `CaptureService.cs:240` (sets the human skip reason) and `MainWindow.Tabs.cs:94` (surfaces the validation message) — left alone deliberately.

## Not fixed — and why

- **A2-20** file budgets — splits are refactors with call-site risk; sized and proposed above.
- **A2-21** PNG decode — one helper, but the five call sites are UI/store code mid-feature; schedule as a small slice.
- **A2-22** `Update` transaction — behaviour-adjacent; must be done by the store-file owner with an `[H]` assertion.
- **A2-23 / A2-24 / A2-25 / A2-28** — checklist files are frozen to this axis; the repo has no clean commit to fold a doc sweep into; EOL normalisation would bury the release diff.
- **A2-26** index — not a defect at the current cap.

## Gate gaps observed (tooling)

The mechanical stages this audit performs by hand (hygiene grep, 12-line duplication hash, hex-literal scan, layering import check, file-budget count, EOL/BOM scan) still have no standing script in this dotnet-only repo — same gap `AGENTS.md` already records. The duplication/size/SQLite-probe scripts this run used are reproducible (probe project at `%TEMP%\opencode\msd2-probe`); promoting them to a checked-in gate would stop the next audit from re-deriving them.
