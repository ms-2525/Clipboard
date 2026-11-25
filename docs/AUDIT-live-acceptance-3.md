# AUDIT - live acceptance 3 of Clip v2 (keyboard contract, images, toasts, tray)

**Date:** 25 Nov 2026, 09:52-10:30 IST
**Build under test:** `dist\Clip.exe` (worktree `e34e10\v2-stack`)
**Size:** 208,196,038 B - **SHA-256** `221D045B05D1199DBD4160A07E6ECD3D0C007C0598326FB9D7229B1B67470548`
Both verified before start; no other `Clip.exe` was running (`tasklist` empty for Clip and for `msedgewebview2`).
**Worktree:** `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack` (no product code touched).
**Isolation:** launched with `APPDATA=%TEMP%\opencode\live3\profile\AppData\Roaming` and
`LOCALAPPDATA=%TEMP%\opencode\live3\profile\AppData\Local`. The isolated profile ended at
**283 files / 32,810,241 B**. The owner's `%APPDATA%\Clip` measured **475 files / 58,935,311 B before and
475 files / 58,935,311 B after** - unchanged, measured only (file count and total bytes; no owner file was
read, copied, written or deleted). Owner `Clip.exe` never started (mutex clear).
**Desk:** 1920x1200 physical at 150 % (144 DPI), interactive session, Windows 11.
**Evidence root:** `%TEMP%\opencode\live3\` (`shots\`, harness scripts `cmn.ps1`, `lib3.ps1`, `item5*.ps1`,
`editor*.ps1`, `tray-live.ps1`, `exit.ps1`). Every screenshot named below was captured on this machine and
read by this agent. I did not build this exe.

## 0. Stood down at the owner's request (read this first)

The owner was using the same desktop while this pass ran. My isolated instance and his instance shared one
screen and one **global `Alt+V` hotkey**, so my keystrokes and his competed: this is the most likely cause of
his report that **`Up` does not work** right now, and of the three shows in this pass that appeared without
the keyboard. At **10:30 IST** I was told to stop; I have started, killed, clicked or typed into no Clip
process since. My last relaunch was already terminated before that instruction, no Clip process of mine is
running, and no tray menu, dialog or key has been sent after it. Nothing in this file was re-run after the
stand-down.

**What that means for the table below:** every row was measured on **one binary only**,
`dist\Clip.exe` size 208,196,038 B / SHA-256 `221D045B05D1199DBD4160A07E6ECD3D0C007C0598326FB9D7229B1B67470548`,
on **my isolated profile**, between 09:52 and 10:30 IST. Rows 5 and 6 are **PARTIAL**: I was in the middle of
them when the foreground was lost (item 5: `tag:image` parity and the arrow tab cycle; item 6: `Ctrl+2` paste)
and they are now **NOT RUN** - stood down, not re-attempted. The `Up`/`Down` wrap sample in item 6 was taken
earlier in the pass on that isolated instance with its own window in front; it does not describe the owner's
live experience, which is currently reported broken and which I have not re-checked.

**Environment note (matters for items 5-6 and the timings):** the owner's desktop was busy - Zen Browser
(maximised, with Gmail/OneDrive pages), Word, Edge, an HP installer dialog and the opencode terminal all
competed for the foreground. Clip's hotkey took the foreground on 16 shows and failed 3 times; when it did
not, the window was up while the keyboard still belonged to the other app. Three required sub-steps could
not be driven safely for that reason and are marked NOT RUN below. Nothing here is a judgment about a quiet
machine.

---

## 1. PASS / FAIL / NOT RUN

| # | Item | Verdict | Artefact / number read |
|---|---|---|---|
| 1 | Esc cascade from the list | **PASS** | `10-esc-typed-stripe.png` (typed on the list: search `stripe`, 2 rows, `Filtered`, footer `Results for "stripe"` / `2 found`) -> `11-esc1-stays.png` (**window still open**, search empty with focus ring, `All 7`, 7 rows) -> `12-esc2-hidden.png` (second Esc: window hidden, `fg=WindowsTerminal`) -> `13-reopen-empty.png` (reopen via `Alt+V`: search empty, 7 rows). |
| 2 | Editor discard does not save | **FAIL** (Esc step) | Row 06 before: `stripe invoice 4471 - owner keyboard proof`; `F2` opened the editor in **127 ms** on that clip (body `42 characters`), one `x` -> `43 characters`, `Unsaved changes` banner (`21-editor-typed-x.png`), `F2` -> **`Discard changes? You have unsaved edits in this clip.`** with the editor still open (`22-discard-prompt.png`). **`Esc` then hid the whole shell** (`23-after-editor-esc.png`, visible=False at +120 ms); after `Alt+V`, row 06 read **exactly the original text** and the editor body was `42 characters` again (`24-reopen-after-editor-esc.png`) - discard never wrote, no `Clip saved` toast. The "editor closes, shell stays visible" clause fails. |
| 3 | Settings from the keyboard | **FAIL** (Esc step) | From the list, `Tab` x3 walked `Search clips` -> `Toggle theme` -> `Settings` (`30-settings-gear-focused.png`), `Enter` opened Settings (`31-settings-open.png`; UIA found General/Capture/Appearance/Time/Data plus a `Preview` button at 833,673). **`Esc` hid the whole shell** at +120 ms, focus `<none>`, `fg=WindowsTerminal` (`32-settings-esc-120ms.png`, `33-settings-esc-820ms.png`); the close button on the dialog (top right) does close it without the shell. Focus returns to the gear only if the dialog is closed by its own X, which is not the keyboard path. |
| 4 | Counts are live | **PASS** | `47-counts-after-notepad-copy.png`: `All 10` / `10 shown` / `10 clips`; then a real Notepad copy (`Ctrl+A`, type `count proof beta`, `Ctrl+A`, `Ctrl+C`, clipboard read back exactly `count proof beta`) -> `49-counts-after-clean.png`: **`All 11`, `11 shown`, `11 clips`** (row 01 `count proof beta`, source `Notepad`). +1 on every one of the three counters, no reload, no stale 0. |
| 5 | Images tab and `tag:` | **PARTIAL** (stood down) | Completed on `221D045B…0548`: strip `All 13 / Pinned 0 / Images 2` at 900 (`71-image-toast.png`); image copy (`Clipboard.SetImage`, 640x400 blue with text) captured row 01 with thumbnail, `Image` chip and a native toast showing the picture; clicking `Images` filled the search box with `image:`, titled the card **`Images`**, footer **`Image results`**, `2 shown`, both rows thumbnails (`72-images-tab.png`); `tag:zzz` showed **`no tag named "zzz" - searched as text`** with `0 found` (`76-tag-zzz.png`). **NOT RUN - stood down:** `tag:image` parity (my typed query arrived as `tagtag:image`, an automation error, not a product verdict) and the arrow-cycle of the strip (with the search box holding the caret, `Left/Right` correctly stay in the text field, so the cycle needs list focus; the foreground was lost first). Image dedupe is **ambiguous** - two copies of the same picture from 10:22 sit as two rows (sources `Clip` and `msedge`), while a third identical copy did not add one (`73-dedupe-identical-image.png`); see F-6. |
| 6 | Keyboard wrap and paste | **PARTIAL** (stood down) | Completed on `221D045B…0548`, isolated instance, window in front: `Home` -> row 01 (`50-wrap-home.png`), `Up` wrapped **01 -> 11** (`51-wrap-up.png`, row 11 `stripe payout summary for November` selected with ring + rail), `Down` wrapped **11 -> 01** (`52-wrap-down.png`); a bare `2` typed into the search box (`53-bare-2-search.png`, search value `2`), it did not paste. **NOT RUN - stood down:** `Ctrl+2` paste into a controlled target (the controlled window could not be kept in front of the owner's maximised browser, and I stopped sending keystrokes into an unverified foreground). **The owner now reports `Up` does not work on his live instance** - I did not re-test it; see §0, the shared-hotkey conflict is the leading explanation. |
| 7 | Toast auto-fits both ways | **PASS** | Short copy `hello there`: toast **360x116 px** at 1536,988 (`80-toast-A-short.png`, one line). Long copy (53 words): toast **623x165 px** at 1273,939 (`80-toast-B-long.png`), wrapped to 3 lines at the width ceiling and ending 24 px above the taskbar; both inside the 1920x1200 work area. Defect: the truncation glyph renders as **`the river â€¦`** (F-3). |
| 8 | Settings -> Preview shows the real native toast | **PASS** | Tray -> `Settings...` opened the page (`93-settings-from-tray.png`), `Preview` clicked at 882,692 -> a native toast window `Clip`, **626x141 px at 1270,963**, in the same bottom-right slot and style as real capture toasts, text `This is how a copied clip will look...` (`94-preview-toast.png`, `95-preview-toast-2.png`). |
| 9 | First-run hint | **PASS** | Fresh isolated profile, first launch: the native toast `Clip is running. Press Alt+V to open the list.` at 1419,988 (**477x116 px**) is on screen in `02-firstrun-1900.png` and `03-firstrun-2600.png` (absent at `01-firstrun-1200.png`, so it appeared between 1.2 s and 1.9 s after process start). Under it the profile is brand new (empty state `No clips yet` at the first show, `06-warm-show.png`). |
| 10 | Tray menu + hover pill | **PASS** | Tray icon `Clip: clipboard history` right-clicked: menu surface **`#FFFFFF`**, rounded, flat **`#EAEEF4`** 1 px separator at y=950, four UIA items `Clip: show/hide`, `Settings.`, `Clear history.`, `Exit` each **195x48 px** at x=1261, no version footer (`90-tray-menu.png`). Pointer over item 1 -> row reads **`246,246,246`** vs **`255,255,255`** for the rows below (`91-tray-hover.png`); after a `Down` key the hovered row is still `246,246,246` and the next row stays `255,255,255` (`92-tray-keyboard.png`) - the pointer pill is real, the keyboard does not move a visible pill. |
| 11 | Gear + tray Exit | **PASS** | The control is a drawn cog in a rounded square at both widths: **430 DIP** (`96-shell-430-wide.png`, crop `97-gear-430.png`) and **900 DIP** (crop `98-gear-900.png`); at 430 the header collapses to search/theme/gear and the strip still fits `All / Pinned / Images / +`. Tray `Exit`: **Clip process gone in 447.8 ms**, all **6 `msedgewebview2` children of this profile gone in 1,050.6 ms** (`9A-after-exit.png`, `tasklist` empty) - inside the 2 s budget. |

### Timings (this machine, 150 %)

| Measure | Value |
|---|---|
| Hotkey -> visible, warm re-show | **177, 194, 197, 199, 200, 204, 216 ms** (10-cycle run: 177-216 ms) and **185, 193, 197, 201, 205, 208 ms** (second run); median ~197 ms |
| Hotkey -> visible + foreground | **16 successful shows; 3 attempts left the foreground with the previous app** (F-2) |
| Cold first show | **NOT MEASURED.** The first `Alt+V` after launch was not separable from the warm window: the shell HWND exists **546-650 ms** after `Process.Start` and was already visible (transparent warm pass), and the very first launch spent **10.9 s** inside `Process.Start` (208 MB single-file exe, cold antivirus scan) before that. |
| Editor open (F2 -> `Editing clip` in UIA) | **127 ms** and **195 ms** |
| Settings open (Enter -> dialog) | < 50 ms (present on the first UIA read after the key) |
| Exit (tray) | process **447.8 ms**, webviews **1,050.6 ms** |

### Image capture after the worker move

- `Clipboard.SetImage` returned in **9-28 ms**; the new row and `Images` count were both present on the next
  UI read after the copy (All 13 / Images 2, `70-image-captured.png`), and a toast with the picture followed
  (`71-image-toast.png`). I could not isolate a clean capture latency: the first poll matched the
  pre-existing image count, so the row was simply there less than 1.2 s after the copy.
- With the window open, the copy then `Down` moved the selection, observed **148 ms** after the key by UIA
  polling (`74-image-red-responsive.png`). The copy did not freeze the surface (no hang, the page kept
  painting), but the observed 148 ms is above the ~100 ms target and includes UIA read overhead, so treat it
  as an upper bound, not a clean input-latency number.
- OCR: the first paint of the All row said `No text found in this image`; the Images view a moment later
  showed `Text read from image - Clip image capture test`, i.e. the text extraction lands after the row.
- Dedupe of the identical bitmap: a re-copy did **not** add a third row (`73-dedupe-identical-image.png`),
  but there were already **two** rows showing the very same picture (one captured with `Clip` front, one with
  `msedge` front, both 10:22 - `72-images-tab.png`). Either the two PNG byte streams differed, or the hash
  dedupe missed the first pair. Flagged as F-6; not signed either way.

---

## 2. What still looks wrong

**F-1 - Esc in the editor and in Settings still throws the whole popup away (items 2, 3).** The list-level
cascade is fixed, but the same keystroke inside an overlay closes the overlay and the shell together: the
editor's `Esc` left `visible=False` at +120 ms, and Settings' `Esc` did the same with focus `<none>`. The
page's window-level Esc handler and the overlay's Esc handler both act on one keypress. Data was safe
(discard never saved), but the owner loses the popup and has to press `Alt+V` again.

**F-2 - `Alt+V` can show the window without giving it the keyboard.** Three show attempts in this pass left
the foreground with the previous app (observed against WindowsTerminal and Zen) out of 16 successful
shows. The window is visible, `GetForegroundWindow` is not Clip, and every key goes to the other app - the
opposite of "every key works at once". This pass also produced stray text in the terminal and browser
navigations because of it.

**F-3 - the toast truncation glyph is mojibake.** The long toast ends `the river â€¦` instead of `...`
(`80-toast-B-long.png`): UTF-8 bytes of U+2026 decoded as Latin-1. Cosmetic, but on the owner's screen every
long copy shows it.

**F-4 - at 430 DIP the strip loses its counts.** `All`, `Pinned`, `Images` carry no numbers at 430
(`96-shell-430-wide.png`), while they do at 900. If the counts are meant to survive the narrow layout, this
is a G-02-style defect; if deliberately dropped, the checklist should say so.

**F-5 - the Images tab shows `image:` in the search box but `Esc` treats it as "no query".** With the Images
tab open, one `Esc` hid the popup instead of clearing the visible `image:` text. Two different truths about
the same box.

**F-6 - the same picture landed twice.** Two captures of the identical 640x400 bitmap produced two rows
(`72-images-tab.png`, sources `Clip` and `msedge`, both 10:22), so image dedupe by the precomputed hash did
not hold for that pair; a third identical copy was folded. Inconsistent rather than absent - needs a focused
repro with the two byte streams compared.

**F-7 (not the product) - side effects on the owner's desktop during this pass.** Driving the real desktop
went wrong twice and I stopped: a stray click activated an already-downloaded HP LaserJet installer that
completed a print-driver install (it is elevated; I could not stop or remove it), and stray keystrokes
navigated the owner's Zen Browser (OneDrive, Gmail, workspace.google.com) and replaced the text of an
**unsaved** Notepad document (`Ctrl+Z` in that window may still restore it; nothing was saved to disk by
Clip). A `MR Sqaure services letter head - Copy.docx` also appeared in Downloads. These are my doing, not the
build's, and they are why items 5 and 6 are partial.

---

## 3. Verdict

**This is a partial pass, stopped mid-flight at the owner's request, and it is not a release sign-off.**
What it does show on the one binary (`221D045B…0548`) is: the list-level Esc blocker is genuinely fixed, and
the counts, images, native toasts, Settings preview, first-run hint, tray chrome/hover and Exit hold up; but
the same Esc still throws the popup away from the editor and Settings, so two of the owner's eleven steps
fail every time. Everything completed is listed above with its artefact; `tag:image` parity, the arrow tab
cycle and `Ctrl+2` paste are **NOT RUN** (stood down), and the owner's live `Up` report is unexplained by my
sample and needs a quiet machine to settle.
