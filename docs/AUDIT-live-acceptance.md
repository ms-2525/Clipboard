# AUDIT — live acceptance of the released Clip v2 build

**Date:** 25 Nov 2026, 08:38–09:16 IST
**Build under test:** `dist\Clip.exe`, 208,187,846 B, SHA-256 `213AC8F4…1744` (the release record's binary).
**Worktree:** `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack` (code not modified).
**Isolation:** the child process ran with `APPDATA=%TEMP%\opencode\live-profile\AppData\Roaming` and
`LOCALAPPDATA=%TEMP%\opencode\live-profile\AppData\Local`. The owner's `%APPDATA%\Clip` was measured before
and after: **475 files / 58,935,311 B both times**.
**Desk:** 1920×1200 physical at 150 % scaling (144 DPI), interactive session, no overlay. Windows 11.
**Evidence root:** `%TEMP%\opencode\live-accept\` (`shots\`, `*.json`, harness scripts). Screenshots were read
by this agent, not inferred from code.

---

## 1. PASS/FAIL table

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Cold start → tray → first `Alt+V`; warm show ×20 | **PASS** | First shell window **526.6 ms**, warm-ready **527.5 ms**, first `Alt+V` → visible **160.8 ms** (process start → visible **688.3 ms**, warm profile). Tray item `Clip: clipboard history` read in the overflow: `traycheck.png`, rect 1433,982,60×60. Warm 20: min **81.39**, median **97.94**, p90 **110.66**, max **122.90 ms** (19/20; one 2 s timeout). `coldstart.json`, `warm20.json`, `warm-show.png`. |
| 2 | Capture text+emoji / URL / image / 2 files / browser HTML; paste text into Notepad | **PASS** | All five rows on screen (`captures-1.png`, `captures-rich.png`): emoji+Hindi row, Link row, Image row with thumb (OCR later read `CLIP IMAGE`), Files row with both mono paths, rich HTML row with heading, bold, italic, a real table and a link. Chrome put CF_HTML on the clipboard (`HTML Format`, 2,232 chars). Notepad read-back **exact**: 55 chars, `ExactMatch=true` (`notepad-pasted4.png`, `paste-roundtrip.json`). |
| 3 | Excel → Excel and Word round-trip | **NOT RUN (blocked)** | Office 16 **is installed** (Excel 16.0.20625.20108, Word). Both COM servers fail on `Visible` with `TYPE_E_CANTLOADLIBRARY (0x80029C4A)` — the same failure recorded earlier. No UI-driven Office matrix was attempted inside the timebox. The matrix **could not run on this machine**. |
| 4 | Hindi IME composition; Esc cancels composition, not shell | **NOT RUN** | `Get-WinUserLanguageList` = **en-IN (4009:00004009)** and **en-US (0409:00000409)** only. No Hindi input method is enabled (the `00000439 / 00010439` layout DLLs exist in the registry but are not in the user's list). Testing needs the owner's language settings changed — not done. |
| 5 | Zoom 125 % persists and reflows at 900 / 430 widths | **PASS** | Set through Settings (`125%` radio, UIA Invoke); `config.json` `uiZoom: 125`; survived a full restart. Page at 900 DIP (`zoom125-900b.png`, 720 CSS px) and 430 DIP (`zoom125-430b.png`, 344 CSS px at 125 %): no horizontal scrollbar; header stacks, paths ellipsize. The shell is `ResizeMode="NoResize"`; widths were set with `SetWindowPos` (645 px wide at 430 DIP). Settings dialog itself at 430 was not captured (window hid) — W-02 stays partial. |
| 6 | Native toast: source + readable preview, no focus steal | **PASS** | `toast-focus.png`: `WindowsTerminal 09:05`, two-line readable preview `TOAST-PROOF … grandfather clock, 42 files`, 630×141 physical bottom-right. Foreground hwnd **7212868 before and after** (`toast.json`). |
| 7 | Tray menu via UIA → Exit → process + WebView2 gone < 2 s | **PASS** | Menu read and shot (`traymenu-exit.png`): show/hide, Settings…, Clear history…, Exit, `Clip 1.0.0`. Exit: process gone **996.3 ms**, all live-profile `msedgewebview2` gone **1672.1 ms**, remaining 0 (`exit.json`). |

---

## 2. Timing detail

**Cold start** (measured from a script that prints its stopwatch; `Process.Start` + `EnumWindows` poll;
shell identified by its 1350×1050 physical client-less window, not by title):

| Milestone | ms from start |
|---|---|
| `Process.Start` returns (warm profile, after the first run) | 14.0 |
| First shell window exists (tray built immediately before `Warm()` in `App.V2.cs`) | 526.6 |
| Shell hidden again = page's first frame done | 527.5 |
| `Alt+V` sent | 527.5 |
| Window visible | 688.3 |
| **Hotkey → visible (cold first open)** | **160.8** |

The **first-ever launch on this machine** returned from `Process.Start` after **10,773.9 ms**; the same
binary then returned in 11–15 ms on every later launch. That 10.8 s is before any Clip code runs
(single-file extraction / Defender on a 208 MB unsigned exe is the likely cause) and is a separate
observation, not part of the 688 ms figure.

**Warm show ×20** (`warm20.json`): `Alt+V` → `IsWindowVisible` on the shell. Method: `keybd_event`, then
`EnumWindows` in a 2 ms poll loop — **granularity 2 ms plus the EnumWindows round trip**, not a paint
timer. The screenshot taken at detection shows a fully painted page (`warm-show.png`), so this is an
upper bound on first paint. n = 19 because sample 2 timed out at 2 s: the hotkey is a toggle and the
harness may have raced its own Esc; it is recorded rather than hidden.

| min | median | p90 | max | mean |
|---|---|---|---|---|
| 81.39 | 97.94 | 110.66 | 122.90 | 97.65 ms |

---

## 3. What the screenshots show

| File | Read |
|---|---|
| `shots/warm-show.png` | Warm shell, 7-row list, footer hints, counts. |
| `shots/captures-1.png` | Files, Image (thumb), Link, emoji+Hindi text rows; native toast with both file paths. |
| `shots/captures-rich.png` | Chrome CF_HTML row: heading, bold amounts, italic, table columns, link — rendered as rich text, not a text blob. |
| `shots/paste-search.png` | Search `Clip live acceptance` → 2 results, emoji row first. |
| `shots/notepad-pasted4.png` | Notepad `L1 C56`, 55 characters, emoji and Devanagari intact. |
| `shots/traycheck.png`, `shots/overflow.png` | Overflow flyout; UIA also read the exact `Clip: clipboard history` name/rect. The cursor covers the icon in the PNG. |
| `shots/settings-100.png` | Settings at 100 % before the change. |
| `shots/zoom125-900b.png`, `shots/zoom125-430b.png` | 125 % at 900 and 430 DIP, no horizontal scrollbar. |
| `shots/toast-focus.png` | Native toast, source app, two-line preview, terminal still foreground. |
| `shots/traymenu.png`, `shots/traymenu-exit.png` | Tray menu — **stock white WPF ContextMenu**, square corners, default font, version `Clip 1.0.0`. |
| `shots/after-exit.png` | Desktop after Exit; no Clip window. |

---

## 4. Three things a user would still notice

1. **Search then Enter is a dead end.** With the caret in the search box, `Enter` keeps its native
   meaning (by design, `useClipboardKeys.ts`), so it does **not** paste the highlighted result; the
   header still says `Enter paste`. In the live run the result was selected and Enter did nothing until
   the row was clicked with the mouse. `ArrowDown` moves the selection but left the caret in the search
   box. This is the most likely "the app ignored me" moment.
2. **The tray menu is stock WPF next to a custom toast.** `traymenu-exit.png`: white square menu,
   default font and separators, and the version reads **`Clip 1.0.0`**, not a v2 version. It is the one
   surface that visibly did not get the v2 design pass (G-06 cannot pass on this evidence).
3. **Paste can report success and land nothing when the target app's editor does not hold focus.**
   With Notepad foreground but its tab bar focused, Clip hid, foreground returned to Notepad, and the
   document stayed empty; only after clicking inside the editor did the exact 55 characters arrive.
   The focus guard checks the top-level window, not the focused child. A user who Alt-Tabs from a
   toolbar or tab will see a successful-looking paste with no text. (Also observed: 1 of 20 warm
   hotkey presses produced no window inside 2 s.)

### Other observations, not in the three

- Capture of the Chrome copy recorded source app **`Clip`**, because the Chrome tab was opened in the
  background and Clip was foreground at clipboard-write time; the payload is genuine Chromium CF_HTML.
- The generated image's OCR first said "No text found", then later produced `CLIP IMAGE` — OCR ran
  asynchronously and the row updated.
- The tray menu's Settings item is the only route that worked; opening Settings through UIA at 125 %
  at 430 width made the shell hide.
- 900 DIP is 720 CSS px at 125 %; 430 DIP is 344 CSS px. Both were rendered without horizontal
  overflow, but neither is the 900/430 *CSS* width the design documents name.

---

## 5. Cleanup

- Clip process: **not running** (`clip=0`).
- `msedgewebview2` with the live profile: **0**.
- Owner profile unchanged: 475 files / 58,935,311 B before and after.
- The only Notepad change was a new tab; it was closed with **Don't save** and the original modified
  tab (2,224 chars) was left intact and minimised again. No other app data was written.

## 6. Checklist deltas made from this pass

- `docs\CHECKLIST-features.md` §1 cold-first-popup row: **NOT RUN → FAIL**, evidence above (160.8 ms
  cold, still over the 150 ms budget; warm 97.9 ms median).
- `docs\CHECKLIST-uiux.md` **G-06**: **NOT RUN → FAIL** — the toast passes, the tray menu is stock WPF.
- `W-02` (125 %) stays **NOT RUN**: page at 900/430 is proven, the settings dialog at 430 is not.
- The live matrix row stays **NOT RUN**: Chrome rich-text, image, files and Notepad plain text are
  proven; Excel and Word are blocked by the machine's broken Office COM registration.
