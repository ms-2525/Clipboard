# AUDIT — second live acceptance of Clip v2 (owner keyboard + native tray)

**Date:** 25 Nov 2026, 09:16–09:36 IST
**Build under test:** `dist\Clip.exe` — 208,187,846 B, published 09:13, SHA-256
`6E9B1B952F6F123C12492700C586910B6F7549D0DD2C52D9A1CAF8360F9B9FA0` (matches the brief). Its page was
content-verified against `UI\web\dist\index.html` by the publisher; this pass judges the running exe, not
the source.
**Worktree:** `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack` (product code not modified).
**Isolation:** the child ran with `APPDATA=%TEMP%\opencode\live2\profile\AppData\Roaming` and
`LOCALAPPDATA=%TEMP%\opencode\live2\profile\AppData\Local`. The owner's real `%APPDATA%\Clip` was measured
after the run at **475 files / 58,935,311 B** — the same count and size as the prior pass's baseline; no
owner file changed and no owner process was touched.
**Desk:** 1920×1200 physical at 150 % scaling (144 DPI), interactive session, no overlay. Windows 11.
**Evidence root:** `%TEMP%\opencode\live2\` (`shots\`, `pastetarget.txt`, harness scripts). Every screenshot
below was captured and read by this agent on this machine.

---

## 1. The owner's keyboard contract — PASS/FAIL

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | `Alt+V` → a row visibly focused (ring) and selected, no click | **PASS** | `03-first-show.png` (first show: row `04` carries the ring from the prior state) and `10-a1-app.png` (row `01` ring and fill immediately after the hotkey). No click was sent. First show re-ran in **12.2 ms** (visible + foreground), a later re-show in **48.5 ms**. |
| 2 | `↑`/`↓` move the selection and the focus ring follows | **PASS** | `11-a2-down-app.png`: `01 → 02`, ring and rail on `02`; `12-a2-up-app.png`: back to `01`. No click was sent. |
| 3 | `←`/`→` change tab, including wrap; title and counts change | **PASS** | `41-tab-created-app.png` (Work), `42-tab-right-all-app.png` (All), `43-tab-right-pinned-app.png` (Pinned), `44-tab-right-work-app.png` (Work again), `45-tab-left-all-app.png`. Order is **All → Pinned → Work → All**, i.e. the strip wraps. Card title went `All clips` → `Pinned clips` → `Work clips` and the footer `All history` → `Pinned history` → `Work history`. Counts are wrong — see F-2. |
| 4 | Type `stripe` with the list focused → search fills and filters | **PASS** | `50-a4-search-stripe-app.png`: search pill reads `stripe` with its focus ring; list is **2 shown / 2 found**, `Filtered` chip, rows `01 stripe payout summary for November`, `02 stripe invoice 4471 - owner keyboard proof`. |
| 5 | `Esc` clears the query; `Esc` again hides | **FAIL** | `50-a4-search-stripe-app.png` → `51-a5-first-esc-app.png`: the **first** Esc hid the whole popup. `55-reopen-after-esc-app.png`: after reopening, `stripe` is still in the search box and the list is still filtered. The query is never cleared. See §2. |
| 6 | Search, then `Enter` → the selected clip is pasted | **PASS** | `72-a6-typed-app.png` (query `stripe`, row 01 selected) → `Enter` → `73-a6-after-enter.png`; the shell hid, the controlled target became foreground, and the run's stdout recorded a read-back of exactly **`stripe payout summary for November` (34 characters)** from the target's text field. (The scratch file was later overwritten by a second harness target instance; the observed 34-character value is the run result.) The old dead end is closed. |
| 7 | Search, then `F2` → the editor opens on the selected clip | **PASS** | `80-a7-f2-editor-app.png`: `Editing clip`, source `WindowsTerminal · OC | TipTap port implementation slices · 09:17`, body `stripe payout summary for November`, `34 characters · 5 formats`, `Esc Close`. |
| 8 | Clean editor `F2` closes with no prompt; dirty `F2` shows "Discard changes?" and does not close; `Esc` then closes | **FAIL (Esc step)** | `81-a8-clean-f2-closed-app.png`: clean `F2` returned to the filtered list, no prompt. `82-a8-dirty-f2-prompt-app.png`: after one `x`, `F2` shows **`Discard changes? You have unsaved edits in this clip.`** with `Keep editing` / `Discard and close`, and the editor stays open — good. Then `Esc` (`83-a8-editor-esc.png`) hid the **whole shell**, not just the editor; on the next show (`90-a9-settings-open-app.png`) row 01 read `xstripe payout summary for November` with a `Clip saved` toast — the edit survived the discard path. That last observation is a data-integrity concern to reproduce. |
| 9 | Settings icon visible with an SVG, opens with `Enter`, closes with `Esc` returning focus to it | **FAIL** | The icon is present and accessible: UIA finds a **Button** named `Settings`, rect **1563,66,48×48**, and the screenshots show a drawn 16 px glyph (not text). But a UIA focus + `Enter` did not produce a visible Settings dialog (`99-a9-settings-final-app.png` is the desktop, the shell having gone), and `Esc` from Settings hid the whole shell (`91-a9-settings-esc-app.png`). Opening with `Enter` is unproven; the focus-return clause cannot pass. |

**Warm/cold:** the only show timing taken was warm re-show — **12.2 ms** and **48.5 ms** hotkey → visible +
foreground. Cold first open on this exe was **not** measured, so the cold-row stays as it was.

---

## 2. The blocker: `Esc` is owned by the native shell, not the page

The live result is consistent across every surface: **Escape hides the whole popup and nothing else
happens.** The search query survives the hide, the editor discard prompt is bypassed, and the Settings
overlay is closed by losing the window. The code path explains it:

`UI\ShellWindow.xaml.cs:274-281` —

```csharp
protected override void OnPreviewKeyDown(KeyEventArgs e)
{
    base.OnPreviewKeyDown(e);
    if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
}
```

`Hide()` runs for every Escape before the page's ordered cascade in `useClipboardKeys.ts` can clear the
query or dismiss an overlay. The comment says "the page's own Esc calls hide()", but the page's first two
steps — *clear search* and *dismiss editor/discard/settings* — are now unreachable. This is the one defect
that breaks the owner's flow on its own: after typing a query, Esc loses the popup and leaves the query
behind; the next `Alt+V` returns to the same filtered state.

Reproduction: `Alt+V` → type `stripe` → `Esc` once → window gone (`51`). `Alt+V` again → `stripe` still
there (`55`). Same from the editor discard prompt (`82` → `83`) and from Settings (`99` → hidden).

---

## 3. Native tray menu (the previous G-06 failure)

Opened by right-clicking the `Clip: clipboard history` tray icon in the overflow. UIA and pixels read:

| Property | Live value |
|---|---|
| Surface | `#FFFFFF` — the light-theme `Surface`, not an Aero2 gradient |
| Corner radius | rounded, consistent with the **8 DIP** `CornerRadius` (≈12 physical px at 150 %) |
| Font | **14 DIP** app font from `Popups.xaml`; labels render at the app's menu size, not the 12 DIP stock menu |
| Items | `Clip: show/hide`, `Settings…`, `Clear history…`, `Exit` — each **195×48 physical** (`130×32 DIP`), UIA MenuItem, at 1261,800 / 848 / 896 / 957 |
| Separator | **1 physical px**, flat, `#EAEEF4` at y = 950 — no 3-D groove |
| Shadow | none (`HasDropShadow=False`; no shadow in the shot) |
| Version footer | **gone** — no `Clip 1.0.0`; UIA finds only the four items plus the separator |
| Hover pill | **not observed** — every row stayed `#FFFFFF` after pointer move and after a `Down` key; the pill exists in `Popups.xaml`, but this pass did not see it light up. Not proven either way. |
| Theme follow | **not tested** (time); only the light theme was read. |

`Exit` → the Clip process was gone in **186.7 / 430 / 446 ms** across three runs, and every
`msedgewebview2` carrying this profile was gone in **1,765.5 / 1,246.2 / 1,134.4 ms** — all inside the 2 s
budget. `96-after-exit.png` shows the desktop with no Clip window.

Screenshots: `95-traymenu.png`, `98-traymenu-hover.png`, `9A-traymenu-keyboard-hover.png`.

---

## 4. What still looks wrong

**F-1 — Esc (blocker, §2).** Breaks items 5, 8's Esc step and 9's focus return; also fails the ordered Esc
cascade row.

**F-2 — the All count is 0 while six rows are listed.** In `10-a1-app.png`, `32-altv-shown-app.png` and
`72-a6-typed-app.png` the strip reads **`All 0`** and the footer **`0 clips`**, while the card reads
**`6 shown`**. The value survived hide/show and filtering. `App.tsx` seeds `allCount` from `init.counts`
and only adjusts it on pin/move; live captures do not bump it. The owner reads "0 clips" on a non-empty
history. This is not covered by G-01/G-02's signed clauses, so it is reported here rather than used to
re-open them.

**F-3 — the dirty-editor discard path persisted the edit.** After `Discard changes?` was on screen, Esc
hid the shell; the next show had `xstripe payout summary for November` and a `Clip saved` toast
(`70` → `90`). Either the overlay saved on unmount or Esc reached a save action. Needs a focused repro;
the discard path must not write.

**F-4 — the tray hover pill is unproven** (§3). The chrome is themed and no longer stock, but the exact
hover treatment was not captured.

**Not re-run:** toast (prior pass evidence stands), dark theme, 430 width, Settings body, IME, Office. The
Office COM and Hindi IME rows remain blocked on this machine (`TYPE_E_CANTLOADLIBRARY`; no Hindi input
method) and were not converted.

---

## 5. Checklist rows changed

- `docs\CHECKLIST-uiux.md`
  - **G-06** `FAIL → PASS` on this live tray read, with the hover caveat recorded in the row.
  - Header and §13 tally: **54 rows — 51 PASS · 0 FAIL · 3 NOT RUN**.
  - §14 item 1 now asks for the unobserved tray hover pill instead of the completed tray/menu read.
- `docs\CHECKLIST-features.md`
  - Added **PASS** rows for **search → Enter paste** and **F2 from search** (both live, with the artefacts
    above).
  - **Esc cascade** `NOT RUN → FAIL` with the live first-Esc evidence and the native-handler cause.
  - Cold-popup row: added the **12.2 / 48.5 ms warm re-show** numbers; cold is still unmeasured.
  - Web keyboard matrix row: recorded the live partial (arrows, tabs, search, Enter, F2 pass; Esc fails);
    the row stays **NOT RUN** because Home/End/PageUp/PageDown, Ctrl+F/M/T, Ctrl+Shift+V and `1`–`9` were
    not exercised.
  - Header and §12 tally: **34 rows — 10 PASS · 6 FAIL · 18 NOT RUN**.

**G-01 / G-02 / G-03** were re-read and the live shell is consistent with their signed clauses where this
pass covered them (index/ring/provenance; header/tab strip/footer; editor toolbar/body/footer), but this
pass did **not** cover dark theme, 10-line text, or 430 width, so those rows were left at PASS on their
existing evidence. **G-04** could not be opened (item 9), so it was not re-signed.

---

## 6. Verdict

**The owner's keyboard flow is not ready.** The two holes named in the brief — search-then-Enter paste and
F2-from-search — are genuinely fixed and proven live. But **Esc now throws the whole popup away instead of
clearing the query**, and the same native handler bypasses the editor discard prompt and Settings; a count
of `0 clips` on six visible rows and an edit that survives "Discard changes?" are two further things the
owner will notice.
