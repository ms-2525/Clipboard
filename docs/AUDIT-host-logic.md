# AUDIT — host-side behaviour: paste, capture, tray, toast, OCR storage, bridge writes

Scope: the v2 host stack added/changed since `docs/AUDIT-db-logic.md` — `Host/ClipHost*.cs`,
`App.V2.cs`, `UI/ShellWindow*.cs`, `UI/NotificationWindow.xaml.cs`, `Core/OcrService.cs`,
`Core/CaptureService.cs`, `Core/ClipboardStore.Items.cs`, `Interop/PasteInjector.cs`,
`Interop/ClipboardFormats.cs`.
Code under test: worktree `v2-stack` @ **`a3dee2b`**. `Host/ClipHost.Settings.cs` and the `saveConfig`/
`saveEditor`/`clearHistory` routes in `Host/ClipHost.cs` are **uncommitted S7 work** in the tree at the time of
the audit; findings that touch them are marked **[S7, in progress]**.
Method: static read + a scratch harness (`%TEMP%\opencode\audit-host`) that compiles the **real** Core/Interop/Host
files into one assembly and drives `ClipHost.CopyToClipboard`, `CaptureService`, `ClipboardFormats`,
`SetOcrText` and the clipboard directly; plus a **live isolated app** (built with
`-p:AssemblyName=ClipAudit`, so its mutexes and `%APPDATA%` folder are separate from the owner's) run against
a throw-away database. The owner's database was never opened; every DB used was a copy.
Not tested (stated where it matters): a second monitor / 150 % DPI (this machine is one 1280×800 screen at
100 %), a real paste into another app (the page could not be driven while another agent's instance owned the
hotkeys), and the export dialog's deactivation (code-read only).

---

## Verdict

| # | Area | Verdict | Evidence |
|---|------|---------|----------|
| 1 | Clipboard round-trip (`copy`/`paste`/`saveEditor`/`export`/OCR) | **FAIL** | `paste` suppression works (0 captured events). `copy` has no suppression: it re-enters capture, fires a toast and rewrites the row's provenance/time; copying a **pinned** row mints a duplicate main-history row. Harness, below. |
| 2 | Formats (text/HTML/RTF/files/image/DIBV5) | **PASS**, one RISK | The format **set** is restored exactly (12/12 names before == after), RTF comes back byte-equal and FileDrop comes back as the same paths, the CF_HTML fragment is intact. **CF_DIBV5 is stored but cannot be written back as CF_DIBV5** — proven. Plain path is text-only (no HTML/RTF on the clipboard). |
| 3 | Focus & window state after a refused paste | **RISK** | No-target/elevated/focus-lost paste leaves the clipboard untouched and re-shows the shell (no half-paste, no dead state). But a **failed clipboard write is silent and still injects Ctrl+V**, so the target pastes the *previous* clipboard while the page is told it succeeded. |
| 4 | Toast | **PASS** (three P3s) | Never takes focus (`WS_EX_NOACTIVATE`, observed `ex=0x08000088`); a burst of 5 captures reuses one window (no stacking, no queue); 420×77 at (844,659) with the work area 1280×752 → exactly 16 px gaps. Click-through outside the ✕, fade restarts per capture, measurement is O(lines) not twice. |
| 5 | OCR write path | **PASS**, one RISK | Cannot write a non-image row, a missing id or whitespace (proven); keeps `captured_at`, formats and the image blob. Exit mid-OCR, delete-during-OCR and double-OCR are safe. **A later OCR pass overwrites a user-edited image body.** |
| 6 | Single instance & shutdown | **PASS** (one P2, one P3) | Normal second launch surfaced the hidden shell (observed). Force-kill left **no** `msedgewebview2` orphans (12 before, 12 after; none of mine). OCR mid-flight cannot lose data. A second launch inside the first ~1 s is swallowed (event handle created last); the browser profile is a shared temp folder. |
| 7 | Bridge robustness | **PASS** (two P2/P3) | Unknown method, wrong params, a throwing method and a non-numeric method all produce exactly one error reply; JSON ids are echoed as raw JSON (injection-safe); reads are FIFO on one thread so replies keep order. A page reload can re-use ids **1..n** and take a stale reply. `export` blocks the whole bridge while the Save dialog is open. |
| 8 | Hygiene (`AGENTS.md`) | **PASS** (one P2, one P3) | 0 TODO/FIXME markers, 0 `Console.WriteLine`, all 27 empty catches carry a reason ("placeholder"/"stub" hits are the search box and a comment, not unfinished code). 12 files over the 400-line budget (only `Host/ClipHost.Actions.cs` 404 is new work). 34 hardcoded colours in `SettingsWindow.xaml` (pre-existing fixture); the documented `#FF6B6B` exception no longer exists in the tree. |

Findings summary — **Ship?** = must block release.

| ID | Finding | Rank | Ship? |
|----|---------|------|-------|
| H1 | `copy()` re-enters capture: toast, provenance rewrite, duplicate for pinned rows | **P1** | **YES** |
| H2 | Shipping exe cannot find the page: `UI\web\dist` is a sibling folder, not embedded | **P1** | **YES** (S10 owns it) |
| H3 | Two theme resolvers, one inverted (`System` picks the wrong theme) | **P1** | no, but fix (1 char) |
| H4 | Export dialog deactivates → shell hides → success/failure is invisible; blocks the bridge | P2 | no |
| H5 | Clipboard write failure is silent; paste still runs and reports success | P2 | no (fix: 3 lines) |
| H6 | `_deleted` retains every deleted clip's full blobs for the session | P2 | no |
| H7 | Second launch during the first second is silently lost | P2 | no |
| H8 | `Host/ClipHost.Actions.cs` 404 lines (budget 400) | P2 | no |
| H9 | A later OCR pass overwrites a user-edited image body | P3 | no |
| H10 | Stale bridge reply can resolve a new page's request after reload | P3 | no |
| H11 | Toast: fade restarts per capture, click-through, mixed-DPI placement, 2×(N+1) measures | P3 | no |
| H12 | Hardcoded colours in `SettingsWindow.xaml`; stale `#FF6B6B` allowance | P3 | no |
| H13 | CF_HTML read-back is one byte short (fragment intact) | P3 | no |
| H14 | WebView2 profile is a shared `%TEMP%` dev path | P3 | no (S10) |

---

## Findings

### H1 — `copy()` is captured by Clip itself: a toast, a rewritten provenance, and a duplicate for pinned rows

**What happens.** `ClipHost.CopyToClipboard` puts a clip on the clipboard and never tells
`CaptureService` it was Clip's own write. The old app does exactly that at `App.xaml.cs:187`
(`_capture.Suppress(GetClipboardSequenceNumber())`). 100 ms later the debounce reads the clipboard back:

- a main-history clip is de-duplicated (no second row) but **moves to the top**, gets
  `captured_at = now` and `app_name` = the **foreground** process — which is Clip itself when the user
  double-clicked the row, so the footer changes from `EXCEL · 23 Nov` to `CLIP · now`;
- `Captured` fires, so a native toast appears for a clip the user just copied;
- a **pinned** (or tab) clip is exempt from dedupe, so `copy()` **mints a second main-history row** and the
  same clip now appears twice.
The page's own double-click (`ClipList.tsx:261 onDoubleClick={() => onCopy(clip)}`) makes this a daily path.

**Evidence (harness, real Host/Core code, DB copy):**
```
--- A. host copy() of an unpinned clip id=1
rows 3->3  capturedEvents=1  newestFirst='alpha: the quick brown fox jumps over the lazy dog'
--- B. paste path (suppressed)
clipboard seq=9376 suppressed=9376   rows 3   capturedEvents=0
VERDICT paste re-entered capture: no
--- C. host copy() of a PINNED clip
pinned id=2  rows(main) 3->4  capturedEvents=2
VERDICT pinned copy minted a second row: YES
--- D. plain copy() of a rich clip
capturedEvents+=1  (a plain copy is re-captured too)
--- A again on the same DB (the row A had re-stamped):
row after copy: app='explorer' at=04:15:30.380 (was 'explorer' 04:10:31.590)
```
The re-stamp is a capture write: `ForegroundApp.Attribute` returns the **foreground** process, and in the real
app that is Clip itself, so the footer becomes `Clip · now` (harness: it became the harness's process name and
the current time).

**Reproduce.** Copy a clip that lives in Pinned (double-click), wait a second: a second copy of it appears in
All and a toast fires. Copy any clip in Pinned: its footer says Clip and "just now".

**Smallest correct fix.** In `CopyToClipboard`, after the write, `SuppressCapture?.Invoke(GetClipboardSequenceNumber());`
on both exits (the plain branch and the end). Two lines; the old app is the precedent. Add one harness assert:
copy → `Captured` count unchanged, pinned copy → main row count unchanged.

**Blocks release: YES.** It is user-visible duplication and false provenance on the most natural gesture.

---

### H2 — A delivered single exe shows the fallback page, not the UI

**What happens.** `ShellWindow.FindWebRoot()` walks up to 8 parents of `AppContext.BaseDirectory` looking for
`UI\web\dist\index.html`. `build-release.cmd` publishes **one exe into `dist\`**; run from the repo, the walk
finds `<repo>\UI\web\dist` and works by accident. Copy that exe (or the `dist` folder) anywhere else and the
shell shows `FallbackHtml` — the "web build missing" bridge self-check page. The React app is not embedded
anywhere (`Clip.csproj` embeds only the editor assets).

**Evidence:** `build-release.cmd` (`dotnet publish … -o dist`), `FindWebRoot` (ShellWindow.xaml.cs:155-164),
`Clip.csproj` (no `UI\web\dist` resource).

**Reproduce.** `dotnet publish -c Release -o %TEMP%\x` then run `%TEMP%\x\Clip.exe --v2`: fallback page.

**Smallest correct fix.** S10 already owns packaging; until it lands, this blocks the definition of
release-ready. Embed `UI\web\dist` as resources and serve it through the virtual host (or copy the folder next
to the exe in `build-release.cmd` and keep `FindWebRoot` as the lookup). Do not ship before one of these.

**Blocks release: YES.**

---

### H3 — Two theme resolvers disagree and one is inverted: choosing "System" picks the wrong theme **[S7, in progress]**

**What happens.** `SettingsDialog` offers Light / Dark / System. Two independent resolvers read the same
registry value with opposite polarity:

```csharp
// App.xaml.cs:372  (used by App.V2 at startup and by App's own preference watcher)
theme = v is int i && i == 0 ? "light" : "dark";
// ClipHost.Settings.cs:163  (correct: AppsUseLightTheme=0 means dark)
return v is int i && i == 0 ? "dark" : "light";
```
This machine has `AppsUseLightTheme = 0` (Windows dark). With `themeChoice = "system"`, App.V2 applies **light**,
and the page's `config.theme` is `light`, so the owner who chose "System" gets the opposite of Windows. On top
of that, **both** App.V2 (`WatchTheme`) and `ClipHost` subscribe to `SystemEvents.UserPreferenceChanged` and both
emit `themeChanged` with different values; and App's `_themeSetting` is never updated when the page changes the
setting, so a later Windows preference change re-applies "system" over the user's explicit Light/Dark choice.

**Evidence:** the two lines above; `App.V2.cs:226-240`; `ClipHost.Settings.cs:32-45,166-179`; registry read on
this machine `AppsUseLightTheme=0`.

**Reproduce.** Set theme System in Settings; restart; the app comes up light on a dark Windows (and every Windows
theme change sends opposite `themeChanged` events).

**Smallest correct fix.** One resolver: change App.ApplyTheme's ternary to `i == 0 ? "dark" : "light"` (one
character, also fixes the old app) and delete App.V2's `WatchTheme`/`OnThemePreferenceChanged` (ClipHost already
owns following the system). If App keeps its watcher, `SaveConfig` must update `App._themeSetting`.

**Blocks release: no** (cosmetic), but it is a guaranteed wrong result for a natural choice and costs one line.

---

### H4 — The export Save dialog hides the shell, so the result is invisible (and the bridge stalls)

**What happens.** `Export` runs on the UI thread inside `Dispatch` and calls
`dlg.ShowDialog(Window.GetWindow(_web))`. The dialog takes activation, the shell's
`Deactivated += … Hide()` fires, and the shell stays hidden after the dialog closes. The file is written and the
reply is delivered to a hidden page, so the page's "Saved to …" / "Could not save …" toast is never seen. If the
user cancels, the shell also stays hidden. Secondary: because `Export` is inside `Dispatch`, the dispatcher's
nested dialog loop keeps delivering `WebMessageReceived`, so other commands (`delete`, `clearHistory`, `hide`,
`paste`) can execute *while the dialog is open*; and every request issued during the dialog waits until it closes
(the page gives up after 15 s).

**Evidence:** `ClipHost.Export.cs:22-72`, `ShellWindow.xaml.cs:46`, `ClipHost.cs:59-103` (dispatch is inline on
the UI thread).

**Reproduce.** Export a clip, choose a path, click Save: the window is gone and nothing confirms the write.

**Smallest correct fix.** Re-show the shell after the dialog: `_show()` after `ClipExport.Write`/cancel (one
call), or pass a `_modal` bool the Deactivated handler checks and clear it after the dialog. The `_modal` flag
also makes the re-entrancy explicit. Longer term, run the dialog off `Dispatch`.

**Blocks release: no**, but the owner cannot tell a successful export from a failed one.

---

### H5 — A failed clipboard write is silent; the paste still runs and claims success

**What happens.** `ClipboardFormats.WithClipboardRetry` gives up after 5 × 30 ms and returns `void`
(the busy clipboard throws a `COMException`, which is the one it catches and swallows).
`PasteInjector.CopyAndPaste*` unconditionally calls `SendPaste()` and returns the sequence number, so
`PasteNow` replies `{pasted:true}`. The target then receives Ctrl+V with **whatever the clipboard held before**
— a different clip, or the user's own text. This is the one way to get a "half paste" that the focus ladder does
not prevent: it is not a focus problem, it is an unverified write reported as success.

**Evidence:** `ClipboardFormats.cs:97-108`, `PasteInjector.cs:24-67,171-178`, `ClipHost.Actions.cs:154-167`.

**Reproduce.** Hold the clipboard open from another process for >150 ms (a clipboard tool doing a slow
`SetClipboardData`, or a debugger break inside `OpenClipboard`), then paste a clip: Ctrl+V lands, the wrong
content is inserted.

**Smallest correct fix.** `internal static bool WithClipboardRetry(Action set)` returning false on the last
attempt; `CopyAndPaste*` throw `InvalidOperationException("the clipboard is busy")`; `PasteNow`'s existing catch
already re-shows the shell and reports the error.

**Blocks release: no** (rare), but it is a false success on the one operation the product exists for.

---

### H6 — Every deleted clip keeps its full blobs in memory for the whole session

**What happens.** `ClipHost._deleted` is a `Dictionary<long, ClipboardItem>`; `Delete` stores the result of
`ReadFullItem` (text, html, rtf, **image**, thumb, **and every format blob**) and only `Restore` removes an
entry. The UI offers exactly one undo (`lastDeleted` is a single ref in `App.tsx`), so the rest is dead weight.
A screenshot row is up to 5 MB PNG + up to 10 MB of formats; deleting 30 screenshots in a session retains
up to ~450 MB until exit.

**Evidence:** `ClipHost.Actions.cs:21,41-52`, `App.tsx:288-302` (one-entry undo), `Config.MaxImageBytes=5 MB`,
`MaxItemBytes=10 MB`.

**Reproduce.** Delete 30 image clips, watch the private bytes; no undo past the last is even offered.

**Smallest correct fix.** `_deleted.Clear(); _deleted[id] = item;` — one line, matches the one-level undo.

**Blocks release: no.**

---

### H7 — A second launch during the first second is silently swallowed

**What happens.** The named `…V2.Show` event is created at the **end** of `StartV2Shell`
(`ListenForShow()` after store open, shell creation, warm-up and capture wiring). A second launch calls
`SignalRunningShell`, which retries `TryOpenExisting` 10 × 50 ms, then exits. If the first instance is still
starting, the second process finds no handle and dies — and the first instance's warm window is transparent (or
already hidden), so the user sees nothing at all. (Normal case observed working: the second launch made the
hidden shell visible.)

**Evidence:** `App.V2.cs:107-108,128-154`; live run — second launch `exited: True`, shell `vis=True` afterwards.

**Reproduce.** Start `Clip.exe --v2` on a cold machine and double-click the shortcut again within ~1 s; the
second click does nothing.

**Smallest correct fix.** Create the event handle immediately after the mutex is owned and, if the shell is not
created yet, remember the request and `ShowShell()` once `_shell.Warm()` has run.

**Blocks release: no.**

---

### H8 — File budget: `Host/ClipHost.Actions.cs` is over 400 lines

`AGENTS.md` sets a 400-line budget. The 12 longest files, with counts (`(Get-Content <file>).Count`, blank lines
included):

| lines | file | note |
|------:|------|------|
| 539 | `UI/MainWindow.xaml.cs` | old app, pre-existing |
| 506 | `UI/SettingsWindow.xaml` | old app, fixture preview |
| 463 | `Core/ClipboardStore.Items.cs` | +22 for S9 OCR; pre-existing overage |
| 440 | `UI/SettingsWindow.xaml.cs` | old app |
| 424 | `Core/HtmlToRtf.cs` | pre-existing |
| 421 | `UI/RichRow.cs` | pre-existing |
| **404** | **`Host/ClipHost.Actions.cs`** | **new slice work, over by 4** |
| 398 | `UI/MainWindow.xaml` | old app |
| 397 | `UI/EditorWindow.xaml.cs` | 414 at the last audit; back under |
| 397 | `App.xaml.cs` | old app |
| 375 | `Core/ClipExport.cs` | under |
| 354 | `UI/MainWindow.Keys.cs` | old app |

**Smallest fix.** Move the Win32/elevation block (`BlockedByElevation`…`GetTokenInformation`, ~85 lines) out of
`ClipHost.Actions.cs` into `Host/ClipHost.Elevation.cs` — same partial class, no behaviour change.

**Blocks release: no.**

---

### H9 — A later OCR pass overwrites a user-edited image body

**What happens.** `SetOcrText` is correctly restricted to `kind = 'image'` and does not touch `captured_at`,
formats or the image — all proven. But it has no "only if still empty" rule. Re-copying the same image hits
`TryDedupeMoveToTop`, which returns the existing id and deliberately leaves `text` alone, and then
`CaptureService.Store` enqueues OCR **again** for that id. If the owner had edited the image row's text in the
meantime (the editor writes through `Update`), the fresh OCR result silently replaces the edit.

**Evidence (harness, real store):**
```
SetOcrText(text row)   -> False  (text untouched)
SetOcrText(missing id) -> False
SetOcrText(whitespace) -> False
SetOcrText(image row)  -> True   captured_at unchanged: True  formats kept 1->1  image 130 bytes
user edit stored       -> True   text='USER EDITED THESE WORDS'
late SetOcrText        -> True   text='OCR WORDS AGAIN'
VERDICT user edit survives a later OCR pass: NO - overwritten
```

**Reproduce.** OCR an image, edit its text, copy the same image again from Explorer → the edit is gone.

**Smallest correct fix.** `… WHERE id = $id AND kind = 'image' AND (text IS NULL OR text = '')` — one clause.
(A re-copy of an unchanged image then does no work either.)

**Blocks release: no** (narrow, but silent).

---

### H10 — A stale bridge reply can answer the new page's request after a reload

**What happens.** The page's request ids restart at 1 on every load (`host.ts:235,276`). A read queued just
before a reload (e.g. the reload that `NotifyCleared` triggers, or a hot reload) finishes after the new page is
up and its reply — id 1..n — is matched against the **new** page's pending map. The new `init` can receive a
`PageResult` (empty counts/list) or `getItems` can receive an `init` result; the page renders an empty history
until the next refresh. Nothing hangs: the 15 s timeout covers a reply that never comes.

**Evidence:** `host.ts:246-285`, `ClipHost.cs:222-249` (`Finish` posts the id as received).

**Smallest correct fix (client, 1 line).** Seed `this.seq` from a per-load random base
(`this.seq = 1_000_000 + Math.floor(Math.random() * 1_000_000)`), so a stale id cannot collide. Alternative:
the host includes a page-generation value in every reply.

**Blocks release: no.**

---

### H11 — Toast details (cost, clicks, DPI)

- **Burst:** five captures in 2 s reused **one** toast window (observed: only one toast HWND throughout), the
  timer restarts and the content is replaced — coalesced, not queued, not five windows. But `Present()` restarts
  the fade from opacity 0 on every capture, so a burst flickers the toast out/in five times. P3; keep the current
  opacity if the window is already visible.
- **Measurement:** not "twice per show" — `SetBody` calls `Fits()` (→ `MeasureRoot`, two `Measure` calls) once
  per candidate line count, then `Present()` measures twice more: 2×(candidates+1) passes, i.e. 8 for the default
  3-line toast and 62 for a 30-line message. Cheap at these sizes (a small Border/Grid/TextBlock); only worth
  changing if toast text ever gets large.
- **Mouse:** `WS_EX_NOACTIVATE` and `ShowActivated=False` mean it never takes focus (observed
  `ex=0x08000088`), and the `WM_NCHITTEST` hook makes everything except the ✕ `HTTRANSPARENT`. So it does not
  *block* the mouse — it lets clicks **pass through onto UI the owner cannot see** (a bottom-right toast sits
  over scrollbars/buttons). Consider making the whole toast `HTCLIENT` while it is visible, or auto-dismiss on
  any click.
- **DPI:** at 100 % the placement is exact (observed 420×77 at (844,659) in a 1280×752 work area → 16 px gaps).
  The arithmetic is also right for a single 150 % monitor, but `ScreenDpi.Scale` uses the **cursor's** monitor
  while WPF's `Left/Top` use the **window's** DPI. On a mixed-DPI desk (primary 100 %, secondary 150 %) the first
  show on the secondary is positioned with the wrong scale and is not re-placed after `WM_DPICHANGED`. Untested
  here (one 100 % monitor) — verify with a second display before shipping.

---

### H12 — Hardcoded colours and a stale exception

`UI/SettingsWindow.xaml` lines 445–487 contain 34 literal colours (`#1C1917`, `#FEF08A`, `#23272E`, …) inside the
rich-text preview sample. They are content colours of a fixture, but `AGENTS.md` says "no hardcoded colours
outside `UI/Themes/*.xaml`" and the documented exception (SettingsWindow error red `#FF6B6B`) **no longer exists
in the tree**, so the exception list is stale. Either move the sample's palette into the theme dictionaries or
record the sample as an accepted exception in `AGENTS.md`. P3, pre-existing.

---

### H13 — CF_HTML read-back is one byte short (fragment intact)

The raw `HTML Format` HGLOBAL measures **196 B both before and after** the round-trip (`GlobalSize` via
Win32), and the fragment between `StartFragment`/`EndFragment` ends correctly, so paste-back consumers are not
truncated. But WPF's `Clipboard.GetData("HTML Format")` read-back returns 193 of the 194 characters — the same
one-byte shortfall `AUDIT-db-logic` found in Excel's stored fixture (`id=370`). Do not claim a byte-exact
CF_HTML round-trip in the S7 evidence until this is explained; verify with a raw `GlobalSize`/`Marshal.Copy` read,
not `GetData`. P3, no owner impact found.

**Reproduce.** `audit-host.exe formats` — "raw CF_HTML size before/after: 196B", "html lengths: sent=194
back=193".

---

### H14 — The WebView2 profile is a shared `%TEMP%` dev path

`ShellWindow.OnLoaded` creates the browser profile at `%TEMP%\opencode\clip-v2-webview` — a development path,
for every install and every Windows user on the machine. The page stores no owner data (the DB and config are
elsewhere), so this is not a data-loss bug, but: Disk Cleanup or any temp cleaner removes the profile (page
state, fonts cache, any future login); two users share one folder; and two app instances pointed at the same
user-data folder is the one WebView2 configuration that fails outright. One line moves it to
`Path.Combine(Config.DataDir, "webview")`, next to the DB it serves. S10 should land this with the packaging fix.

**Evidence:** `ShellWindow.xaml.cs:55-57` (the comment says "Dev-only profile location").

**Blocks release: no** (S10).

---

## Area notes that passed

**Paste suppression is correct.** Harness B: the exact `PasteInjector.CopyAndPaste(item)` sequence, then
`Suppress(seq)`, then `Capture()` → 0 `Captured` events, row count unchanged. The sequence number comparison is
the right test (a later user copy increments it, so it is never over-suppressed).

**`saveEditor` cannot corrupt a truncated row.** It loads via `ReadFullItem` (whole body), refuses when
`IsTruncated`, refuses empty/short/oversized/`<script>`, drops stale RTF/formats, and the store's own
`Update` guard still applies. No write path in `saveEditor` or `export` touches the clipboard.

**Refused pastes are clean.** No target / elevated target / focus lost during the wait: the clipboard is
**not** written (the write happens only in `PasteNow`, after focus is confirmed), the shell is shown again, and
the reply carries a readable reason. The elevation check treats a process it cannot open as elevated — the
conservative choice. `SelfElevated` short-circuits correctly.

**OCR plumbing is sound.** One static gate, one below-normal MTA worker, typed failures, no exception can reach
capture; `SetOcrText` runs on the dispatcher (the one writer) and preserves everything except `text`; a write
after the row is deleted returns false; a write after shutdown is swallowed twice over. Exit with OCR in flight
loses only the enrichment.

**Shutdown leaves no browser behind.** Force-killing the isolated instance left exactly the pre-existing
`msedgewebview2` processes (12 before, 12 after; none of the new PIDs survived). The clean path disposes the
WebView before the store and the mutexes.

**Bridge protocol.** `id` is echoed with `GetRawText()` — a string or an object id is valid JSON and cannot
inject into the envelope; unknown methods, missing/ill-typed params and a method that throws all produce one
`{id, ok:false, error}` reply; read replies are produced on one FIFO thread and posted in order; unknown ids are
dropped by the page. `null`/non-numeric ids produce a reply the page ignores (it only matches numeric ids) rather
than a hang beyond the client's 15 s timeout.

**Live capture.** Six clipboard writes in the isolated app produced six rows in the right order with the right
`latestText`; the toast auto-fit to 77 px for one line; both hotkeys were owned by another Clip instance, and the
app showed a startup message toast instead of coming up dead (the `hotkeyFailed` path in code).

---

## Positive findings — do not touch

- The paste ladder itself (image → stored formats → rich fallback → text) is the old app's, one home, and the
  stored format set round-trips exactly (`CF_HTML`, `Rich Text Format`, `CF_HDROP`, `CF_DIB`, `PNG`, plus the
  WPF/OLE companions) with the FileDrop decoded back to real paths.
- `CaptureService` reads text, files and image in the right order, snapshots formats **after** the item is built,
  and writes item+formats+dedupe+cap inside `WriteWithRecovery` — one transaction, one recovery.
- `ClipHost.ReadQueue` is one thread, with a caught job boundary, and `Finish` guarantees exactly one reply per
  request including failures.
- `_store` calls stay on the dispatcher; reads use a fresh read-only connection so a recovery rename can never
  leave a stale handle. `getItem`/one-row costs measured 0.5–5 ms — no UI-thread problem at this scale.
- `NotificationWindow` cannot take activation (XAML `ShowActivated=False` + `WS_EX_NOACTIVATE`), reuses one
  instance, and clamps to the work area with a DIP-consistent gap.
- The `--v2` mutex pair (v2 + legacy) is the right guard: one listener per machine, and an abandoned mutex is
  taken over.

---

## The three things I would insist on before shipping

1. **Fix H1 and lock it with an assertion.** Copy must never be captured. Two lines in `CopyToClipboard`, one
   harness row (`copy → 0 captured events; pinned copy → 0 new main rows`). This is the only *user-visible
   duplication* in the host.
2. **One theme resolver with the right polarity (H3).** Delete the inverted copy in `App.ApplyTheme`, drop
   App.V2's duplicate watcher. "System" is a button in Settings today and it is provably wrong on a dark Windows.
3. **Close the two false-feedback paths (H4 + H5):** the save dialog must not hide the shell that reports its
   result, and a failed clipboard write must refuse the paste instead of injecting the old clipboard and
   claiming success. Then demonstrate both on the real page, at 100 % and 150 % DPI (H11's mixed-DPI risk is the
   only thing in this report I could not test).

H2 is not a host-logic defect but it is the release: until the page is embedded or copied beside the exe, the
delivered `Clip.exe` boots the fallback page. S10 must close it before the owner is handed the file.

---

## Evidence index

Scratch: `%TEMP%\opencode\audit-host\` (`harness.csproj`, `Program.cs`, `out\`, `iso\`, `live.ps1`),
databases `%TEMP%\opencode\phase1-db\{clip,roundtrip}.db` and `%TEMP%\opencode\audit-host\live.db` — all copies or
fresh files; the owner's DB was never opened. The isolated app used `-p:AssemblyName=ClipAudit`, so its data
folder (`%APPDATA%\ClipAudit`) and mutexes never touched the real product; that folder was removed afterwards.

Commands (all from the harness, real source files linked in one assembly):

```
dotnet build harness.csproj -c Release -o out
audit-host.exe makedb   <db>       # fresh store, 2 text + 1 rich + 1 image, a pinned text row
audit-host.exe roundtrip <db>      # A copy() unpinned, B paste+suppress, C copy() pinned, D plain copy()
audit-host.exe formats             # CF_HTML/RTF/FileDrop round-trip + CF_DIBV5(id 17) round-trip
audit-host.exe ocr     <db>        # SetOcrText guards + user-edit protection
audit-host.exe perf    <db>        # ReadMetaAt / ReadDto / ReadFullItem / ReadPage / ReadSearch
audit-host.exe dump    <db>        # rows, tabs, counts, LatestText
powershell -File live.ps1          # isolated ClipAudit: warm, toast rect, second launch, 5-capture burst, kill
```

Measured numbers used above: toast 420×77 at (844,659) vs work area (0,0,1280,752); second launch exits and the
shell becomes visible; 6 live captures with correct order; `msedgewebview2` 12 → 12 with no surviving new PID;
`ReadMetaAt` 0.94 ms, `ReadDto` 4.96 ms, `ReadFullItem` 0.48 ms, `ReadPage(200)` 0.81 ms, `ReadSearch` 0.67 ms.
No process was left running; the other agent's running instances were not touched. One caveat on method: a
`Clip` instance launched by another agent was running from `bin\Debug` for part of the session, so any clipboard
write on this desktop could have been captured by it. The harness therefore wrote only synthetic fixtures
(`alpha…`, `burst clip N`, a 194-character CF_HTML sample) and never real content; its own capture runs used the
isolated `ClipAudit` instance.
