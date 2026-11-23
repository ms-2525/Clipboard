# AUDIT — milestone, Axis 1 (SPEC & OWNER TRUTH)

**Run:** 23 Nov 2026, 22:14–22:55 local. **Agent:** a fresh Axis-1 sub-agent — wrote no product code, edited no
checklist. Independent eyes only.
**Method:** walk the v2-wave `✅`/`🟡` rows against the evidence files they name (read the PNGs, not the captions),
then re-verify the ten highest-owner-value claims **live** on the running app, then audit the checklists for
statements their own evidence does not support.
**Environment:** one display 1920x1200 @150 % (1280x800 DIP). Every capture set `SetProcessDpiAwarenessContext(-4)`.
**Binary under test:** the popup/editor/toast checks ran on a build of the tree as of **22:23:07**
(`%TEMP%\…\out\Clip.exe`), because the packaged artifact `bin\Release\net10.0-windows\Clip.exe` is **21:41:45** —
40 minutes older than the source tree (§6.1). The packaged binary itself was still exercised for capture and for
the hotkey failure. **Clean-tree build re-verified at the end on a copy of the tree: 0 errors / 0 warnings.**
**Evidence produced by this audit:** `docs\design-evidence\axis1\*.png`; raw numbers:
`docs\AUDIT-milestone-axis1-measurements.md`.

---

## 0. The verdict in one line

**The v2 rebuild is real** — rich rows, numbers, zebra + divider, the `app · title · time` bar, outside tooltips,
big image previews, the one rich editor and the exact 420x160 toast all behave as the owner asked **on the running
app**. The release is blocked by **paperwork and process**, not by the pixels: the two frozen checklists still
contradict each other and the app (§2/§3), the shipped `bin\Release` binary is **older than the source tree**
(§6.1), and on this machine **Alt+V cannot be registered at all** (§6.2).

| Area | PASS | FAIL | PARTIAL | Stale (record says less than the app does) |
|---|---|---|---|---|
| Live owner-value claims (10) | 9 | 0 | 1 | — |
| Owner directives §1–§9 | 7 | 0 | 2 | — |
| Checklist honesty | — | 3 false greens | — | 5 stale reds / 49 status-marker mismatches |

Row census of the two frozen contracts (204 rows): `CHECKLIST-features` **83 ✅ / 40 ⬜ / 15 🟡 / 3 ❌**;
`CHECKLIST-uiux` **3 ✅ / 57 ⬜ / 3 ❌** — the UI contract has never been swept, even though most of its `⬜` rows
now carry PASS prose.

---

## 1. Verdict table — the ten highest-owner-value claims, re-verified live

| # | Claim (frozen contract) | Verdict | Evidence / measured |
|---|---|---|---|
| 1 | **No jump on ↑/↓** (`CHECKLIST-features` §2 ↑/↓ row, directive §1) | **PASS** | 0 differing pixels above the old row and below the new one; row heights 138/294 px unchanged after deselection (`axis1\v-open.png` vs `axis1\v-down1.png`) |
| 2 | **Zebra + divider + hover tints (never erases)** (`§2` row separation, directive §8) | **PASS** | App `#08090C` / `RowAlt #16181D` alternate, `Divider #212329` hairline between rows; hover lifts an even row to `#1F2126` (a 3.9 % tint, not a flat fill), divider lifts to `#2A2C31` (`axis1\h-base.png` vs `axis1\h-row2.png`) |
| 3 | **Bottom bar `app · title · time`, selection-driven** (§2 bottom bar, directive §2) | **PASS** | `WindowsTerminal · OC | Win… · 23 Nov 22:29` → `copyq · AUDIT-RICH bold… · 23 Nov 22:24` when the selection moved (`axis1\bar-row1.png`, `axis1\bar-last.png`); counts + key hints stay right |
| 4 | **Sequential numbers in the gutter** (§3, directive §3) | **PASS** | 1, 2, 3 live (`axis1\v-open.png`); 58, 59, 60 in `acceptance-v2\row-numbers-58-60.png`; past 9 continues (10, 11) |
| 5 | **Rich formatted rows on open** (§5 rich row, directive §4) | **PASS** | the selected RTF clip renders bold heading + blue underlined run + plain line **inside its own height** on first paint (`axis1\v-open.png`); no appended block, rows below do not move |
| 6 | **Top-`PreviewLines` previews + truthful badge** (§2b) | **PASS** | `previewLines=10` honoured: a 30-line clip shows 10 and **+20**; a 19-line clip that wraps to 38 visual lines shows 10 and **+28** — the badge counts rendered lines, which is truthful about what is hidden |
| 7 | **Outside tooltip: truncated-only, right-edge flush, flips** (§2 tooltip) | **PARTIAL** | 1.5 s hover on a truncated row → window `1373,348 510x780`, `ex=0x8000088`; its left edge **1373 = popup right edge 548+825 exactly**, height = popup height; parked mouse → no window; non-truncated row → no window (`axis1\tip-row2.png`). Flip proven in `popup-visual\dark-tip-flip.png`. **Why PARTIAL:** the tooltip's top (348) did not sit on the hovered row's top (405) — the vertical anchoring the checklist documents ("top on the row's top") is not what the build does |
| 8 | **Images preview + ←/→ switches tabs** (§6 images, directive §6) | **PASS** | dominant preview fills the pane (≈596x393 px inside a 550x520 popup), numbered tile strip, `1 / 1` counter, `1 image`; ←/→ walked Clipboard → Images → Pinned → Images (`axis1\images-live.png`) |
| 9 | **Editor opens rich with toolbar + tight margins ≈13 DIP** (§8 F2, directive §5) | **PASS** | F2 on the RTF clip: 840x744 window, toolbar `B I U S │ A- 12 A+ │ A │ highlight │ Clear`, WYSIWYG content, box border **9.3 DIP**, first glyph ink **11.3 DIP** (checklist says 13.3 — glyph bearing), footer `Ctrl+S saves / Cancel / Save changes`, ClearType visible at 3x (`axis1\editor-rich.png`) |
| 10 | **Toast exactly 420×160 + reuse** (§11 toast rows) | **PASS** | two copies 300 ms apart → **one** toast at `1274,872 630x240 px = 420x160 DIP`, `ex=0x8000088`, auto-hidden ≈3 s (`axis1\toast-live.png`); the Settings ghost measures the same box to the pixel |

Also re-verified while walking: ClearType is live on the popup (**1696 of 2634** text pixels in a 100 px band carry
colour fringing); no hard-coded colour exists outside `UI\Themes\*.xaml`; every reading window sets
`TextFormattingMode=Display` + `UseLayoutRounding`, and `AllowsTransparency` survives only on the decorative
`NotificationPreviewWindow` — directive §7 holds.

---

## 2. False greens (a `✅` its own evidence does not support)

**G1 — `CHECKLIST-features` §1 row 26 (RAM) keeps a `✅` while its own newest measurement says FAIL.**
The row opens `✅ [L] RAM (owner-approved): idle ≤ 150 MB ✅ … ceiling 250 MB ✅` and then records
`ACC-v2: idle PASS … ceiling FAIL HARD — 344–350 MB WS / 185 MB private after a session, NOT released`. A reader
who scans statuses sees green memory; the app runs 40 % over its ceiling and hands nothing back on close. The
latest truth is buried in prose.

**G2 — `CHECKLIST-features` §13 row 153: the dependency list is not the shipped dependency list.**
It reads `✅ … HtmlRenderer.WPF 1.6.1, H.NotifyIcon.Wpf 2.4.1, RtfPipe, System.Text.Encoding.CodePages,
Microsoft.Data.Sqlite`. `Clip.csproj` references **three** packages (Microsoft.Data.Sqlite 11.0.0, H.NotifyIcon.Wpf
2.4.1, RtfPipe) — **HtmlRenderer.WPF is not referenced and `HtmlRenderer` appears nowhere in `*.cs`** (rich rows go
through `RichTextBox` + `Core\HtmlToRtf.cs`). `System.Text.Encoding.CodePages` is not referenced either; the
`RegisterProvider` call works from the WindowsDesktop shared framework, so the behaviour is fine — the *claim* is
stale, and it names a renderer that no longer exists.

**G3 — `CHECKLIST-uiux` §1 row 26 ("Button grammar unified across ALL surfaces") carries a bare `✅` for a
partly retro-fitted claim**: the row's own text still says "first-run untested — needs the owner's config renamed".
Minor, but it is the same pattern: `✅` with an untested clause inside.

*Systemic (counts as one finding, list item #4 below):* **49 `⬜`/`🟡` rows whose prose records an
observed PASS** — e.g. features §2 bottom bar `⬜ … PASS live`, §2 tooltip `⬜ … PASS, measured`, §5 rich row
`🟡 … ACC-v2 PASS`, §10 Settings `🟡 … ACC-v2 PASS`, §11 toast `🟡 … FIXED + measured live`, uiux §1 rows 11–22
`⬜ … S3 AUDIT: PASS`, uiux §2 rows 33–38 `⬜ … ACC-v2 PASS`. A gate script that counts non-`✅` rows declares the
whole wave unfinished; a human skimming the prose declares it green. **The checklists cannot serve as a release
gate in this state.**

## 3. Stale reds (the record is harsher than the app — the mirror image of a false green)

**R1 — the two frozen contracts contradict each other on a P1.** `CHECKLIST-features` §2 row 43 says the bottom
edge is fixed ("the viewport never cuts a glyph … five scroll positions end on whole glyph lines");
`CHECKLIST-uiux` §1 row 27 still says `ACC-v2: STILL FAIL — the last visible row is cut through its glyphs, no
scroll cue`, citing a screenshot that predates the EdgeMask. **Live, the fix is real:** the last visible row's ink
band ends cleanly (y 692–712) and the row's own fill runs to the rule at y=738 (`axis1\v-open.png`); `EdgeMask`
exists in `UI\MainWindow.xaml:245` + `MainWindow.Tooltip.cs`. Row 27 must be re-run and closed.

**R2 — the on-screen notification preview works but is still recorded as broken.** uiux §2 row 36 says
`ACC-v2: FAIL — no overlay window, nothing drawn`; features §10 row 127 says `UNTESTED`/`⬜`.
`integration\settings-dark-with-ghost-preview.png` proves otherwise, and I measured its dashed rectangle:
**x 1274..1903 (630 px = 420 DIP), y 872..1111 (240 px = 160 DIP)** — the exact box, position and size the live
toast uses. The row is stale-FAIL; only "moves/resizes live" and "drag writes back the offsets" remain untested.

**R3 — `CHECKLIST-features` §5 row 71 still describes the rich preview as `via HtmlRenderer.WPF`** and stays `🟡`
although the row's own ACCEPTANCE-v2 note says PASS and the implementation is a different renderer (see G2).

**R4 — the same file both passes and fails text crispness.** `CHECKLIST-uiux` §3 row 46 is `✅` ("ClearType
restored on every reading window … fringed pixels 0→2039"), while §6 row 79 still asserts "text is greyscale-AA per
directive 7". Live, ClearType fringing is present (1696 of 2634 text pixels in one band), so §6 row 79 is stale.

**R5 — `CHECKLIST-features` §1 row 18 (`⬜ Tray icon visually confirmed`) has a PASS verdict and durable
evidence** but was never flipped; the tray icon and its themed menu work in this run too.

---

## 4. Owner directive scorecard (`docs\DESIGN-v2-directives.md` §1–§9)

| § | Directive | Verdict | Proof |
|---|---|---|---|
| 1 | No geometry change on selection; content-adaptive height with a cap; minimal scroll | **PASS** | 0 differing pixels above/below the selection; row heights constant (138/294 px); one further ↓ scrolls exactly one row (`v-open`/`v-down1`) |
| 2 | Provenance in a separate bottom bar: `app · title · time` for the selected clip, count + hints right | **PASS** | two selections, two different app/title/time strings in the bar (`bar-row1.png`, `bar-last.png`) |
| 2b | Top `PreviewLines` lines (10, honoured directly), row = `16 + lines×18` | **PASS, with one exception** | 1-line row 34 DIP, 10-line row 196 DIP (formula exact), badge +20/+28 truthful. **Exception:** a rich 3-line row measures **92 DIP**, not 70 — rich rows size to the rendered block, not to the formula |
| 2c | Ortu as the heavy UI/UX reference, "not big margins", zero slop | **PASS (judgement)** | flat borderless rows, 12 radius, quiet dividers, numbers instead of icon tiles, tight editor margins (9.3 DIP) — reads as a product, not a template |
| 3 | Sequential numbers replace the icon tile; other tabs get a sensible slot treatment | **PASS** | 1, 2, 3 … 58, 59, 60 in the gutter; the Images tab uses a numbered tile strip |
| 4 | Rich text renders in the MAIN popup, **inside its fixed height**; "unselected rows keep the plain clamped preview" | **PARTIAL** | Render-in-row: PASS (no geometry change, no appended block). **But the build renders rich formatting on unselected rows too** (`v-down1.png`, row 1 after deselection still bold/blue/underlined) — the literal "unselected = plain" clause is not implemented. The owner's *intent* (no jump) is served better by this choice, but the frozen wording is not met |
| 5 | ONE rich editor for everything, WYSIWYG, toolbar, real formatting, tight margins | **PASS** | plain clip F2 → same rich surface; RTF clip opens formatted; toolbar B/I/U/S, size, colour, highlight, clear; save contract unchanged; `editor-rich.png` |
| 6 | Images tab: big previews filling the popup, never one-at-a-time | **PASS** | dominant preview + strip + counter (`images-live.png`); with 14 images the acceptance evidence shows the same layout (`acceptance-v2\images-tab.png`) |
| 7 | Text crisp everywhere (no `AllowsTransparency`, DWM chrome, Display mode, layout rounding) | **PASS** | fringing restored (1696/2634 px in one band); `AllowsTransparency` only on the decorative preview ghost; all reading windows `TextFormattingMode=Display` |
| 8 | CopyQ-Forest zebra + 1 DIP divider; selection/hover paint OVER the zebra; both themes | **PASS** | measured App/RowAlt/Divider hexes; hover = 3.9 % tint over the zebra; light theme `RowAlt #FFFFFF` vs `#F4F5F7` visible in `popup-visual\light-open.png` |
| 9 | Process: rethink → disjoint sprints → fresh-eyes audit against this file | **PASS (this document is its product)** | the sprint evidence folders exist and are internally consistent; what failed is not the process but the *bookkeeping* (§2/§3) |

---

## 5. The owner-eyeball list — accurately recorded, not silently marked done

| Owner-only check | Where it is recorded | Honest? |
|---|---|---|
| Excel → Excel live paste | features §3 row 60: `🟡 … live Excel→Excel NOT run` | **yes** |
| IME composition gate (Hindi/CJK) | features §2 row 49: `⬜` | **yes** |
| Tray icon pinned outside the Windows overflow | features §1 row 18 (PASS via the overflow flyout), §13 "Tray swapped to H.NotifyIcon" still `⬜` | **yes**, and I confirmed the tray works again this run (Win+B → Enter → "Clip: clipboard history") |
| Multi-monitor placement | uiux §6 row 77 + acceptance §9: single display, cannot be exercised | **yes** |
| First-run hint ("…is running — press Alt+V") | features §10 row 131: `⬜` | **yes** — and note this is exactly the toast that exposed §6.2 |
| Motion feel (140/83/380 ms, FLIP) | uiux §4 rows 53–65: all `UNTESTED-BY-AUTOMATION` | **yes** |

No owner-eyeball item was silently flipped to done.

---

## 6. Blockers found live (not on anyone's fix-sprint list)

**6.1 · The shipped binary is older than the source tree.** `bin\Release\net10.0-windows\Clip.exe` is
**21:41:45**; `Core\ClipboardStore.cs`, `Interop\ClipboardFormats.cs`, `App.xaml.cs` and
`UI\NotificationWindow.xaml.cs` were modified at **22:22:41** and `Core\ClipboardStore.Items.cs` at **22:27:14** —
all **after** the last build of the packaged artifact. (An *incremental* build into a redirected OutDir said
"0 errors" because it copied a 22:23:07 object; a **from-scratch build of the current tree on a copy compiles
clean at 22:58: 0 errors / 0 warnings** — the source is healthy, the artifact is not.) Whatever the owner runs at
next login (autostart points at `bin\Release\…\Clip.exe`) is **not** the code the checklists describe.
**Release rule: rebuild the packaged artifact, then assert its timestamp is newer than every source file.**

**6.2 · Alt+V cannot be registered on this machine, and the failure silently kills the second hotkey.**
Three fresh launches each showed **"Could not register Alt+V. Another app may own it."** (`axis1\startup-toast.png`).
Something on this box already holds Alt+V. The prime suspect is **CopyQ** (installed for the owner's Forest-theme
reference) — it is running (3 processes) and its window title mirrors the clipboard — but I could not prove the
association: pressing Alt+V myself produced no visible CopyQ window and no foreground change either. What is
certain: **Clip cannot register its own hotkey here.** Worse, by design `App.xaml.cs:235` only keeps the bindings
**if both registered**, so a taken Alt+V also disables **Ctrl+Shift+V**; the only signal is a 3-second toast at
startup. A user would conclude "the hotkey does nothing". Two tickets: (a) keep the binding that *did* register and
name the one that failed; (b) have the owner or an agent free Alt+V (CopyQ → Preferences → Shortcuts is the first
place to look) and re-test.

**6.3 · Popup topmost — fixed since the acceptance run.** The acceptance P1 "popup can hide behind other windows"
is stale: the live popup carries `WS_EX_TOPMOST` (`ex=0x88`). Record it as closed.

**6.4 · Process identity changed under the audit** (100452 → 33020 → … → 61692) with no crash in the event log —
the acceptance run saw the same thing. It did not break behaviour, but an unattended restart path still has no
explanation on record.

---

## 7. Axis-1 verdict

> **Axis 1 (SPEC & OWNER TRUTH): CONDITIONAL FAIL on release, PASS on the product.**
> Every owner-visible v2 claim I could reach — 9 of 10 spot checks, 7 of 9 directives — is true on the running app,
> and the one PARTIAL (rich formatting on unselected rows) is a wording conflict the owner should bless, not a
> defect. What blocks release is **bookkeeping and packaging**: the frozen checklists contain **3 false greens** and
> **5 stale reds** (two contracts contradicting each other on the bottom-edge P1), **49 rows whose status marker
> disagrees with their own prose** (the UI contract records a bare **3 ✅ out of 63 rows**), a **`bin\Release` binary
> older than its sources**, and a hotkey that is **dead on this machine** because another tray app owns Alt+V and
> Clip drops both bindings when one fails.
> **Single worst discrepancy:** the shipped binary predates the source tree (21:41 vs 22:22) — the app the owner
> will click is not the code the checklists describe, and the memory ceiling recorded in `✅` row 26 is 344–350 MB
> against a 250 MB contract.

**Re-audit trigger:** after (1) rebuild + timestamp check, (2) checklist status sweep (every row whose prose says
PASS becomes `✅`, every stale red is re-run), (3) the Alt+V conflict ticket, (4) RAM ceiling and cold-open budget.

---

## 8. Side effects this audit caused (nothing hidden)

1. Seeded 5 fixture clips through the app's own capture path (1 image, a 30-line clip, a 19-line clip, a 1-line
   clip, one RTF clip) + 2 toast-test strings (a Notepad copy did not land — CopyQ held the foreground); **all
   deleted through the app's own Delete path** at the end — the store is back to
   `items=0, formats=0, tabs=1 (Pinned)`, which is the same state the popup-visual cleanup left it in. (That cleanup
   had already removed the last Pinned clip the acceptance pass had preserved — noted, not repaired: the owner's
   history was empty before this audit.)
2. `config.json` untouched (dark, Alt+V, 24h, previewLines 10, notification 420x160 verified at the end).
3. Killed and relaunched the app twice to get a working instance; **left running** (PID 61692, dark theme) — note it
   runs the 22:23:07 source build from `%TEMP%`, not the stale packaged `bin\Release` binary.
4. Copy-pasted nothing into any other application; no editor save was performed (Esc only).
5. Added `docs\design-evidence\axis1\` (11 screenshots), `docs\AUDIT-milestone-axis1.md` (this file) and
   `docs\AUDIT-milestone-axis1-measurements.md`. No checklist row was edited.
