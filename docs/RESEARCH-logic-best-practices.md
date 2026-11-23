# Clipboard-manager logic: what mature projects do, and what Clip does today

**Status:** research / decision input. Read-only study; no production code was changed.
**Date:** 2026-11-23
**Scope:** Ditto 3.25.x (source), CopyQ master (source), PowerToys Advanced Paste (source), Win+V (docs only).

## License rule — read before anyone copies code

Ditto and CopyQ are **GPL-3.0**. Their source is **read-only reference for techniques and algorithms**; every behaviour below must be re-implemented as original C#. Do **not** paste, translate line-by-line, or lift functions from those repos into this project — a GPL derivation would force GPL on Clip.
PowerToys is **MIT**, so it is a legitimate closer reference for our stack, but we still write our own code (no wholesale file copies, no "ported from PowerToys" blobs).

## Method / evidence legend

Ditto and CopyQ were cloned (shallow, sparse) and read directly; every claim below carries a `repo/path:line` citation.
PowerToys was read from `raw.githubusercontent.com` at `main`.
Win+V is **docs-only** evidence — it is closed source; numbers for it are marked as unverified.

| Tag | Meaning |
|---|---|
| **[S]** | read in source code this session |
| **[D]** | from the project's own docs/README |
| **[U]** | unverified / not fetchable here — do not build on it |

---

## 1. Practice → project → what Clip does → verdict

Baseline for "what Clip does": `Core/CaptureService.cs`, `Core/ClipboardStore.cs`, `Core/Config.cs`, `Interop/ClipboardFormats.cs`, `Interop/PasteInjector.cs`, `Interop/HotKeyWindow.cs`, `Interop/ClipboardListener.cs`, `Interop/ForegroundApp.cs`, `App.xaml.cs`.

### 1.1 Capture trigger & self-suppression

| Established practice | Who | What Clip does today | Verdict |
|---|---|---|---|
| `AddClipboardFormatListener` / `WM_CLIPBOARDUPDATE` event-driven, never polling **[S]** `Ditto/src/ClipboardViewer.cpp:73-96`, `CopyQ/src/platform/win/winplatformclipboard.cpp:17-25` | Ditto, CopyQ | `Interop/ClipboardListener.cs:21` `AddClipboardFormatListener` on a message-only window | **ALIGNED** |
| Re-arm the listener periodically + "ping" the clipboard to prove the chain is alive (5-min timer in Ditto) **[S]** `Ditto/src/ClipboardViewer.cpp:105-110,158-172` | Ditto | Nothing. If the listener is ever dropped, we silently stop capturing until restart | **GAP: adopt** (cheap: re-`AddClipboardFormatListener` on a 5-min timer) |
| Suppress own paste via a **marker format** on the clipboard (`"Clipboard Viewer Ignore"`) — checked before any work **[S]** `Ditto/src/ClipboardSaveRestore.cpp:69,104`, `Ditto/src/ProcessPaste.cpp:49`, `Ditto/src/ClipboardViewer.cpp:249-255`; CopyQ uses its own `application/x-copyq-owner` mime and, on match, only refreshes its model instead of saving a new item **[S]** `CopyQ/src/common/common.cpp:301-313`, `CopyQ/src/app/clipboardmonitor.cpp:186-190`, `CopyQ/src/scriptable/scriptable.cpp:2236-2239` | Ditto, CopyQ | Sequence number equality only: `CaptureService.cs:29,33` (`GetClipboardSequenceNumber() == _suppressed`). Fragile: any third-party clipboard write between our `SetDataObject` and the compare makes us capture our own paste as a new item | **GAP: adopt marker**; keep the seq check as a second belt |
| Ignore-list of privacy formats: `Clipboard Viewer Ignore`, `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory == 0`, `CanUploadToCloudClipboard == 0` **[S]** `CopyQ/src/platform/win/winplatformclipboard.cpp:27-48`; Ditto checks the first two plus `CanIncludeInClipboardHistory` **[S]** `Ditto/src/Clip.cpp:364-427` | Ditto, CopyQ | Only the first two: `Config.cs:35-39` → `CaptureService.cs:35`, plus `ClipboardFormats.cs:21-25`. `CanIncludeInClipboardHistory=0` (what Win+V/KeePass-aware apps set) is **not** checked | **GAP: adopt** (2 more `IsClipboardFormatAvailable` + one DWORD read) |
| Delay processing instead of handling inline: Ditto starts a 100 ms timer on change (`ProcessDrawClipboardDelay`), and waits `SaveClipDelay` = 500 ms before saving, because apps set formats progressively **[S]** `Ditto/src/ClipboardViewer.cpp:290-294`, `Ditto/src/Options.cpp:1728,1739` | Ditto | Captures immediately inside the `WM_CLIPBOARDUPDATE` handler (`CaptureService.cs:31`) | **GAP: should** (see §3.1 — this is the single biggest rich-text fidelity risk) |
| Determine the *source app* from `GetClipboardOwner()` (PID → process name + top-level window title), falling back to `GetForegroundWindow()` **[S]** `Ditto/src/ClipboardViewer.cpp:317-343` | Ditto | `CaptureService.cs:46` → `ForegroundApp.cs:16` uses **foreground** window | **GAP: adopt** — with a tray app the foreground is often still the source app, but for background/automated copies (and slow apps) it is wrong. Also CopyQ keeps "owner" as a lazily-updated field via a 20 ms timer **[S]** `CopyQ/src/app/clipboardownermonitor.cpp:10-63` |
| Per-app include/exclude lists with wildcard match, applied on the resolved owner name **[S]** `Ditto/src/ClipboardViewer.cpp:345-399` | Ditto | Only `IgnoreFormats` (by clipboard format) and `MinTextLength` (`Config.cs:35,40`) | **GAP: should** (exclude-by-app is the #1 user request for password managers / RDP) |
| Capture path wrapped in try/catch per operation, returns "nothing to save" instead of throwing **[S]** `Ditto/src/CopyThread.cpp:55-114`; CopyQ aborts cloning on `std::bad_alloc`, on clipboard-sequence change mid-read, on app quit, and after a 5 s budget **[S]** `CopyQ/src/common/clipboarddataguard.cpp:166-365` | Ditto, CopyQ | `CaptureService.cs:38-40,92-97` catch around WPF reads only; `ClipboardFormats.Snapshot` has no timeout and no "clipboard changed under me" check | **GAP: should** (see §3.4) |

### 1.2 Formats, storage, size budgets

| Established practice | Who | What Clip does today | Verdict |
|---|---|---|---|
| Enumerate with `EnumClipboardFormats`, not OLE `GetNextFormat` (documented bug: misses CF_DIB) **[S]** `Ditto/src/Clip.cpp:104-126` | Ditto | `ClipboardFormats.cs:34` `EnumClipboardFormats` | **ALIGNED** |
| Store **raw HGLOBAL bytes verbatim**, keyed by the format *name* string, in a child table: `Data(lID, lParentID, strClipBoardFormat TEXT, ooData BLOB)` **[S]** `Ditto/src/DatabaseUtilities.cpp:700-724`, `Ditto/src/Clip.cpp:1056-1093` | Ditto | `ClipboardStore.cs:23-25` `item_formats(item_id, fmt TEXT, data BLOB)` — same shape | **ALIGNED** |
| Default = a **curated whitelist** of formats; "support all types" is an opt-in advanced flag **[S]** default list `CF_TEXT, Rich Text Format, CF_UNICODETEXT, CF_HDROP, CF_DIB, "HTML Format"` at `Ditto/src/OptionsTypes.cpp:92-97`, all-types path `Ditto/src/CopyThread.cpp:80-84` | Ditto | All HGLOBAL formats, always (`ClipboardFormats.cs:27-49`) | **DELIBERATE DIFFERENCE: reason** — superset is better for Excel/Word fidelity; but see the CF_HDROP/CF_DIB hole below, the whitelist shows what veterans consider *essential* |
| Standard (non-registered) formats are first-class: `CF_HDROP` (file copies), `CF_DIB`, `CF_TEXT` are all stored **[S]** `Ditto/src/OptionsTypes.cpp:92-97` | Ditto | `ClipboardFormats.cs:77-83` returns `null` for any format `< 0xC000`, so **CF_TEXT (1), CF_HDROP (15), CF_LOCALE (16) are never stored**; `ClipboardFormats.cs:19` additionally skips `CF_DIB (8)` and `CF_DIBV5 (17)` | **GAP: must** — copying files today produces *no usable item* (the text fallback is empty, the image path doesn't apply), and Excel charts lose their DIB |
| Size cap semantics: Ditto drops the **whole clip** if one format exceeds `MaxClipSizeInBytes` (default 0 = unlimited) **[S]** `Ditto/src/Clip.cpp:534-542`, `Ditto/src/Options.cpp` (`GetMaxClipSizeInBytes` default 0); CopyQ drops **only the offending mime** and logs, default `.*:100M`, configurable per-pattern **[S]** `CopyQ/src/common/clipboarddataguard.cpp:36-136,199-223` | Ditto, CopyQ | Per-item total budget 10 MB (`Config.cs:43`); blobs are dropped one by one when the running total exceeds it (`ClipboardFormats.cs:33,42`) | **DELIBERATE DIFFERENCE: reason** — CopyQ-style per-format drop is right; but ours silently produces a *partial* format set with no marker, so paste can be inconsistent (HTML kept, its images dropped). Add a `partial` flag or a per-format cap |
| Images stored as the original DIB (CF_DIB) so bitmap-only consumers work **[S]** `Ditto/src/OptionsTypes.cpp:96`, `Ditto/src/Clip.cpp:161-173` | Ditto | Re-encoded PNG only (`CaptureService.cs:99`); WPF `SetImage` on paste produces a `BitmapSource`, not the original DIB | **DELIBERATE DIFFERENCE, but RISK** — see §3.2; any app that only reads CF_DIB gets nothing |
| `CF_HTML` stored byte-exact *with* its `Version:/StartHTML` header **[S]** Ditto stores the raw `"HTML Format"` HGLOBAL unmodified (`Clip.cpp:314-346` stores bytes; no rewriting anywhere) | Ditto | Same: `CaptureService.cs:83` stores the string verbatim, `ClipboardFormats.cs:40-44` snapshots `"HTML Format"` raw | **ALIGNED** (and do not "fix" it — see §3.3) |
| A text representation always exists (CopyQ prepends `text/plain`, appends `text/plain;charset=utf-8`) **[S]** `CopyQ/src/item/itemfactory.cpp:405-430` | CopyQ | Text is a first-class column, image items keep `Text = null` | **ALIGNED** for text/rTF/HTML; **GAP** for image-only items (pasting one injects an empty string today) |

### 1.3 Dedupe on re-copy

| Established practice | Who | What Clip does today | Verdict |
|---|---|---|---|
| CRC32 over **all** stored formats; RTF is normalised first (strip `{\*\datastore…}`, `\rsid`, `\insrsid`) because Word/Outlook regenerate them on every copy **[S]** `Ditto/src/Clip.cpp:885-950` | Ditto | No dedupe at all — every copy is a new row (`ClipboardStore.cs:46-67`) | **GAP: must** (history fills with the same Excel cell 5×) |
| On duplicate: **do not insert**; bump `clipOrder` so the existing row moves to the top, keep its `lDate` (original capture time) and its pinned flag **[S]** `Ditto/src/Clip.cpp:776-800` | Ditto | — | **GAP: adopt exact semantics** (move to top, keep position-independent metadata) |
| CopyQ: hash of the item data; if present → `moveToTop(hash)` and return (no duplicate, content preserved) **[S]** `CopyQ/src/gui/clipboardbrowser.cpp:1671-1676` | CopyQ | — | same as above |
| CopyQ also *updates in place* when the incoming text equals the newest item's text (selection-drag case) **[S]** `CopyQ/src/gui/clipboardbrowser.cpp:1681-1695` | CopyQ | — | **nice** (only matters for X11-style selections) |

### 1.4 Eviction & pinned survival

| Established practice | Who | What Clip does today | Verdict |
|---|---|---|---|
| Cap by item count, delete from the **oldest clipOrder**, but never delete rows that are pinned (`lDontAutoDelete`), have a shortcut, live in a group, or are sticky **[S]** `Ditto/src/DatabaseUtilities.cpp:844-891` | Ditto | `ClipboardStore.cs:184-193`: `DELETE … ORDER BY captured_at DESC, id DESC LIMIT -1 OFFSET $m` — **ignores the `pinned` column entirely** | **GAP: must (data loss)** — pinning silently deletes the user's saved item, and there is no UI warning |
| CopyQ: eviction walks from the bottom and removes only items where `canDropItem()` is true (pinned are skipped); if the tab is full of pinned items it **refuses to add** and tells the user **[S]** `CopyQ/src/gui/clipboardbrowser.cpp:1568-1593`, **[D]** `CopyQ/docs/pin-items.rst` | CopyQ | same as above | **GAP: adopt** the "skip pinned, warn if nothing droppable" rule |
| Optional expiry by age, keyed on **last paste** date, not capture date **[S]** `Ditto/src/DatabaseUtilities.cpp:893-922` | Ditto | none | **nice** |
| Two-phase delete to keep the UI responsive: trigger writes `MainDeletes`; blobs are deleted later in small batches, only when the machine has been idle ≥ N seconds **[S]** `Ditto/src/DatabaseUtilities.cpp:924-950` + trigger at `:736` | Ditto | single synchronous delete (`ClipboardStore.cs:169-193`) | **nice** at 200 items; becomes **should** above ~5k |
| Default caps: Ditto 500 items (100 portable) **[S]** `Ditto/src/Options.cpp:1076-1080` | Ditto | `Config.cs:29` 200 | **DELIBERATE DIFFERENCE: reason** — fine for a popup-first app |

### 1.5 Paste-back: clipboard write, focus, keys

| Established practice | Who | What Clip does today | Verdict |
|---|---|---|---|
| Write **all** stored formats back, raw HGLOBAL, most-descriptive-first; plus the ignore marker **[S]** `Ditto/src/ProcessPaste.cpp:32-56`, `Ditto/src/OleClipSource.cpp:1260-1320`, `Ditto/src/ClipboardSaveRestore.cpp:61-94` | Ditto | `ClipboardFormats.Restore` via WPF `DataObject` + `MemoryStream` (`ClipboardFormats.cs:51-74`) — **and it is never called**: `App.xaml.cs:77` pastes `item.Text` only, `GetFormats` has zero callers | **GAP: must (feature dead)** — every rich-text format we store is currently unreachable at paste time |
| Order: activate target → wait for it to actually be foreground → delay → send keys **[S]** Ditto: `ActivateTarget()` then `WaitForActiveWnd(target, max(25,500))` then `Sleep(SendKeysDelay=100)` then send `^v` with 10 ms key-down delay (`Ditto/src/ExternalWindowTracker.cpp:239-301`, defaults `Ditto/src/Options.cpp:2092-2121`); CopyQ: raise, wait 150 ms after raise, wait for modifiers up, then Shift+Insert (`CopyQ/src/platform/win/winplatformwindow.cpp:197-235`, defaults `CopyQ/src/common/appconfig.h:474-530`) | Ditto, CopyQ | `MainWindow.xaml.cs:84` `Hide()` → `App.xaml.cs:73` `DispatcherTimer(100 ms)` → `SendPaste()` immediately (`PasteInjector.cs:75-86`). No check that the target regained focus | **PARTLY ALIGNED** — our 100 ms matches Ditto's default `SendKeysDelay`, but Ditto *also* polls `GetForegroundWindow` first. Add the poll; keep 100 ms as the floor |
| Release modifier keys before injecting Ctrl+V (user may still hold Alt from the hotkey); PowerToys sends explicit key-UPs for LCtrl/RCtrl/LWin/RWin/LShift/RShift/LAlt/RAlt first **[S]** `PowerToys/src/modules/AdvancedPaste/AdvancedPaste/Helpers/ClipboardHelper.cs:77-95`; CopyQ instead *waits* for release, up to 2000 ms **[S]** `CopyQ/src/platform/win/winplatformwindow.cpp:121-133` | PowerToys, CopyQ | Nothing. With `Alt+V` as the hotkey, an injected Ctrl+V while Alt is still down arrives as **Ctrl+Alt+V** | **GAP: must** (this is a reproducible "paste does nothing" class of bug) |
| Fallback paste chord: try Ctrl+V, else Shift+Insert (CopyQ defaults to Shift+Insert on Windows and only uses Ctrl+V for whitelisted window titles) **[S]** `CopyQ/src/platform/win/winplatformwindow.cpp:170-178`, `CopyQ/src/common/appconfig.h:466-472`, **[D]** `CopyQ/docs/faq.rst:252-262` | CopyQ | Ctrl+V only | **GAP: nice** (matters for legacy Win32 apps, some terminals) |
| Tag injected keys with `dwExtraInfo` so the app's own hook can ignore them **[S]** PowerToys `ClipboardHelper.cs:97-127` (`0x5555`) | PowerToys | no `dwExtraInfo` set (`PasteInjector.cs:101-105`) | **nice** (we have no keyboard hook; irrelevant until we add one) |
| Plain-text paste = replace the clipboard with text only, then paste. Ditto: save clipboard → `EmptyClipboard` → set only `CF_TEXT`/`CF_UNICODETEXT` (+ HDROP→paths as text) → paste → restore original ~2 s later **[S]** `Ditto/src/MainFrm.cpp:525-545`, `Ditto/src/ClipboardSaveRestore.cpp:96-176`, `TextOnlyRestoreDelay=2000` `Ditto/src/Options.cpp:2135-2145`; CopyQ: `copy(text); paste()` **[S]** `CopyQ/src/common/globalshortcutcommands.cpp` (`pasteAsPlainTextScript`); PowerToys: `GetTextOrEmptyAsync` → new text-only `DataPackage` → paste **[S]** `TransformHelpers.ToPlainTextAsync` | all three | `PasteInjector.CopyAndPastePlain` exists (`PasteInjector.cs:55-59`) but **is never called**, and `Config.HotkeyPlain` (`Config.cs:28`) is saved by Settings yet no hotkey is ever registered for it | **GAP: must (feature dead)** — the Ctrl+Shift+V promise in `Config` is not implemented |

### 1.6 Housekeeping, startup, latency

| Established practice | Who | What Clip does today | Verdict |
|---|---|---|---|
| `PRAGMA auto_vacuum = 1` before creating tables **[S]** `Ditto/src/DatabaseUtilities.cpp:698` | Ditto | no PRAGMA at all (`ClipboardStore.cs:16-26`) | **GAP: should** |
| WAL mode | neither Ditto nor CopyQ uses it (Ditto: single connection + auto_vacuum; CopyQ: not SQLite at all) **[S]** | no PRAGMA; single connection, so WAL mainly buys crash resilience | **DELIBERATE DIFFERENCE: reason** — optional; if added, `journal_mode=WAL` + `synchronous=NORMAL` are one-time `PRAGMA`s in the ctor |
| Do **not** ingest whatever is already on the clipboard at startup (only record changes after connecting) **[S]** `Ditto/src/ClipboardViewer.cpp:59-107`, `CopyQ/src/app/clipboardmonitor.cpp:83-92` | Ditto, CopyQ | same — no initial capture | **ALIGNED** (and note: an initial capture would also leak an old password on launch) |
| Startup exclusivity via named mutex **[S]** Ditto sets `m_PingFormat`/single-instance plumbing in `CP_Main.cpp`; CopyQ uses a named local socket/session | Ditto, CopyQ | `App.xaml.cs:36` named mutex | **ALIGNED** |
| Hotkey registration failure is surfaced, not fatal **[S]** Ditto falls back to tray/menu use when the hotkey is taken | Ditto | `HotKeyWindow.cs:43-51` shows a deferred MessageBox and keeps running | **ALIGNED** |
| First-show latency: open the window first, populate from an indexed `ORDER BY` (Ditto's main list is indexed on `(stickyClipOrder DESC, bIsGroup ASC, clipOrder DESC)`) then refresh asynchronously (`PostMessage(WM_REFRESH_VIEW)`) **[S]** `Ditto/src/DatabaseUtilities.cpp:733-740`, `Ditto/src/CP_Main.cpp` (`RefreshView` Post vs Send) | Ditto | `GetRecent` with `ORDER BY captured_at DESC, id DESC` (`ClipboardStore.cs:94-97`); `ix_captured` exists on `captured_at DESC` so the sort is index-assisted | **ALIGNED in shape**; **GAP: nice** — the index does not include `id`, so ties (same second, `yyyy-MM-ddTHH:mm:ss`) fall back to a sort. Store an integer millisecond timestamp or order by `id` alone |
| Second-resolution text timestamps | — | `CaptureService.cs:53` `"yyyy-MM-ddTHH:mm:ss"` | **GAP: should** — two copies in the same second get identical keys; dedupe and ordering both suffer. Store epoch ms (INTEGER) |

### 1.7 Win+V / Advanced Paste behaviours worth knowing

| Behaviour | Evidence | Relevance to Clip |
|---|---|---|
| **Opt-out formats** for history/cloud sync are `ExcludeClipboardContentFromMonitorProcessing` (any data), `CanIncludeInClipboardHistory`=0 (history only), `CanUploadToCloudClipboard`=0 (sync only) | **[D]** learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats ("Cloud Clipboard and Clipboard History Formats") | We must both *honour* them on capture and *set* them on paste-back (see ADOPT #3/#8) |
| Advanced Paste validates before writing: `HasUsableDataAsync()` (text present-and-non-empty, else any of Text/Html/Bitmap/File) then `SetContent` + `Clipboard.Flush()` | **[S]** `PowerToys/.../Helpers/ClipboardHelper.cs:15-46`, `DataPackageHelpers.cs` (`HasUsableDataAsync`) | Our paste of an image-only item pastes `""` — a `HasUsableData`-style guard would fail loudly instead |
| `Clipboard.Flush()` must run on the UI thread and is retried up to 5× ("calling application is not the owner of the data") | **[S]** `ClipboardHelper.cs:48-75` | We already call WPF `SetDataObject(…, true)` (which ends in `OleFlushClipboard`) from the UI thread with a 5× retry (`PasteInjector.cs:62-73`) — **ALIGNED** |
| Win+V keeps a short history (commonly documented as 25 items) and skips very large items (~4 MB) | **[U]** — Microsoft support article not fetchable from this environment | Directionally: small, bounded, lossy. Our 200 × 10 MB is a much bigger net; keep it, but that makes DB housekeeping ours to solve |

---

## 2. Rich-text & format-fidelity code patterns

### 2.1 Ditto — actual pipeline

```
OnDrawClipboard (WM_CLIPBOARDUPDATE)
  if IsClipboardFormatAvailable("Clipboard Viewer Ignore")        -> ignore
  if enforce && IsClipboardFormatAvailable("Exclude...Monitor")    -> ignore
  if IsClipboardFormatAvailable("Ditto Delay Saving Data")         -> Sleep(1500)   // let a pending Ctrl+V land first
  SetTimer(ProcessDrawClipboardDelay = 100 ms)                     // process later, not inline
      |
      v
CopyThread: pTypes = Types-table whitelist  (or every EnumClipboardFormats type if "support all types")
  CClip::LoadFromClipboard:
      owner = process of GetClipboardOwner(), title = its top-level window   // fallback GetForegroundWindow
      apply wildcard include/exclude on owner name
      for each type in pTypes:
          for tries in 0..1:  HGLOBAL h = oleData.GetGlobalData(type); if h break; Sleep(5)
          if h == null -> skip this format (log)
          if GlobalSize(h) > MaxClipSizeInBytes (default 0 = off) -> abort WHOLE clip
          add (type, bytes) to m_Formats
      if zero formats -> retry the whole read after NoFormatsRetryDelay
  COleDataObjectEx::GetGlobalData:
      HGLOBAL h = COleDataObject::GetGlobalData(fmt)          // triggers delayed render
      if h == null:  GetData(fmt, &stgm);  if stgm.tymed == TYMED_ISTREAM:
                        read stream -> GlobalAlloc -> HGLOBAL  // <-- Excel "Biff12"/IStream-only formats
      if h is not a valid HGLOBAL -> GlobalFree, treat as null
  AddToDB:
      CRC32 over every format (RTF: strip {\*\datastore…}, \rsid, \insrsid first)
      if a row with that CRC exists -> UPDATE clipOrder (move to top); no insert
      else INSERT Main + INSERT Data(lParentID, strClipBoardFormat = format NAME, ooData = raw bytes)
```

```
paste (single item):
  ActivateTarget: SPI_SETFOREGROUNDLOCKTIMEOUT=0, AttachThreadInput, SetForegroundWindow, restore timeout
  WaitForActiveWnd(target, max(25, 500 ms))
  CacheGlobalData("Clipboard Viewer Ignore", "Ignore")   // marks it as ours
  CacheGlobalData(fmt, hgData) for EVERY stored format, in stored order
  SetClipboard()            // OLE delayed render: Ditto owns the clipboard and serves formats on demand
  Sleep(100)                // SendKeysDelay
  SendKeys("^{VKEY86}")     // per-key down/up delay 10 ms (RealSendKeysDelay)

paste as plain text:
  Save(TRUE)                // snapshot current clipboard
  EmptyClipboard(); marker; SetClipboardData(CF_TEXT / CF_UNICODETEXT) only
                              (+ CF_HDROP -> file paths as text if no text format)
  Sleep(TextOnlyPasteDelay = 0); SendPaste()
  ... later (TextOnlyRestoreDelay = 2000) restore the snapshot
```

### 2.2 CopyQ — actual pipeline (Windows backend is thin; Qt does the Win32 work)

```
WM_CLIPBOARDUPDATE (via Qt QClipboard::changed)
  if GetClipboardSequenceNumber() == last -> return            // duplicate notification
  data = clipboard->data(formats)   // every read goes through ClipboardDataGuard
  ClipboardDataGuard:
      per-mime cap (default ".*:100M", configurable pattern:bytes)
      total clone budget 5000 ms; abort if clipboard seq changed mid-read; abort on quitting; catch bad_alloc
      log a warning if any single format read takes > 500 ms
  if data contains "Clipboard Viewer Ignore" | "Exclude...Monitor"
     | CanIncludeInClipboardHistory == 0 | CanUploadToCloudClipboard == 0 | "Exclude...Monitor" == "1"
        -> hidden: run only hidden handlers, do NOT save
  if data contains application/x-copyq-owner -> ownClipboardChanged -> update model only, do NOT save
  else saveData -> addUnique:
        if hash(data) already in tab  -> moveToTop(existing), return      // no duplicate row
        else insert at top; if tab full, drop from bottom while canDropItem() (pinned survive)
```

```
paste (Return in the list):
  copy item to clipboard WITH ALL formats   // Qt mime model; target app chooses
  raise target: SetForegroundWindow (+AttachThreadInput when not foreground)
  wait window_wait_after_raised_ms = 150   (before_raise = 0 on Windows)
  wait for modifiers released (up to 2000 ms)
  send Shift+Insert by default (Ctrl+V only if the window title matches a regex)
  plain text: copy(text) [text-only clipboard] then paste()
```

### 2.3 Concrete answers

| Question | Ditto | CopyQ | PowerToys | Clip today |
|---|---|---|---|---|
| Force delayed renders at capture? | No explicit forcing. It just reads (which triggers render) and **retries**: 2×/5 ms per format, 10×/10 ms for the description, whole-clip retry after `NoFormatsRetryDelay` **[S]** | No forcing; Qt read triggers render, guarded by 5 s budget + seq-change abort **[S]** | No forcing; WinRT `DataPackageView` reads **[S]** | No retry per format (`ClipboardFormats.cs:40` one shot per format); only `OpenClipboard` is retried **[S]** |
| Path for formats that come back NULL but exist? | `IDataObject::GetData` + `TYMED_ISTREAM` → HGLOBAL fallback **[S]** `Clip.cpp:52-102` | Qt handles `application/x-qt-windows-mime` and IStream mimes itself **[S]** (uses Qt, not raw Win32) | n/a (WinRT) | None — such a format is silently lost |
| How is format size capped? | Per clip; one oversized format aborts the whole clip; default off **[S]** | Per mime, pattern-configurable, default 100 MiB **[S]** | WinRT/window-managed | Per item 10 MB, drop offending blobs, no flag **[S]** |
| CF_HTML stored with or without header? | **With** — raw `"HTML Format"` bytes, never rewritten **[S]** | Carried as the Qt `text/html` mime; the Win32 `"HTML Format"` envelope is produced by Qt on write (conversion, not byte-exact) **[S]** model, **[U]** exact envelope | Converted to plain text only (never re-writes HTML) | **With** — stored verbatim (`CaptureService.cs:82-83`, `ClipboardFormats.cs:40-44`) |
| What restores an Excel range with columns/rows? | Whatever was stored. Default whitelist has **no** `Biff12`/`Biff8`, so a default Ditto restores Excel as `HTML Format` + `Rich Text Format` + text; users who want native range paste add `Biff12` or enable "support all types" **[S]** `OptionsTypes.cpp:92-97`, `CopyThread.cpp:80-84` | All mimes the app offered, incl. Excel's `application/x-qt-windows-mime;value="Biff12"` **[S]** model | n/a | We *do* snapshot `Biff12`/`Biff8`/`XML Spreadsheet` (registered names) — but never restore them today |
| UnicodeText as string or raw bytes? | **Raw bytes** (`SetClipboardData(CF_UNICODETEXT, hGlobal)`) **[S]** `ClipboardSaveRestore.cpp:77` | bytes through QMimeData | n/a | Converted to `string` then handed to WPF (`ClipboardFormats.cs:59-60`) |
| Self-capture on paste | marker format on the clipboard | own mime marker + seq check | `dwExtraInfo` tag + flush | sequence number only |

---

## 3. RECOMMENDED PIPELINE for Clip (and every difference, flagged)

### 3.1 Capture

1. **Debounce.** On `WM_CLIPBOARDUPDATE` start a one-shot 100 ms timer; reset it on each new notification; do the read in the tick. Reason: apps (Office, VS Code, browsers) publish their formats in several `SetClipboardData` calls; reading inline captures a half-populated clipboard. Ditto: `ProcessDrawClipboardDelay=100` + `SaveClipDelay=500` **[S]**. → `Interop/ClipboardListener.cs` + `Core/CaptureService.cs`.
2. **Owner-based source app.** `GetClipboardOwner()` → PID → process name + top-level window title; fall back to `GetForegroundWindow()` only when the owner is `RuntimeBroker.exe`/null (Ditto's exact comment). → `Interop/ForegroundApp.cs`.
3. **Privacy gates** before any read: `Clipboard Viewer Ignore`, `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory == 0`, `CanUploadToCloudClipboard == 0`. → `Core/CaptureService.cs:35`.
4. **One pass, raw bytes.** Single `OpenClipboard`; `EnumClipboardFormats`; for each format: `GetClipboardData` → if null, retry ×2 with 5 ms (Ditto) → if still null, try the OLE `IDataObject::GetData`+`TYMED_ISTREAM` path. Store bytes exactly as received.
5. **Format policy:** skip only the GDI-handle types our code already lists — `CF_BITMAP 2`, `CF_METAFILEPICT 3`, `CF_PALETTE 9`, `CF_ENHMETAFILE 14` (plus today's `CF_DIB 8` / `CF_DIBV5 17`, which should be *stored*, not skipped) — and the display-only range `0x81–0x8E`. **Store** `CF_TEXT (1)`, `CF_HDROP (15)`, `CF_LOCALE (16)`, `CF_DIB (8)`, `CF_DIBV5 (17)`, and all registered names.
6. **Caps:** per-format cap (e.g. 10 MB) *and* per-item total, each drop logged and recorded as `partial=1` on the row. Never abort the whole clip (Ditto's "abort all" is user-hostile for a 30 MB log dump alongside 2 KB of text).
7. **Timestamp:** `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` as INTEGER, plus keep the ISO string for display if the UI wants it.
8. **Dedupe:** hash of `(format name, bytes)` over all captured formats, RTF normalised (strip `{\*\datastore`, `\rsid`, `\insrsid`). Hit → `UPDATE items SET captured_at = now` (move to top), keep `pinned` and `id`; no new row, no new blobs.
9. **Evict:** skip `pinned = 1`; if everything is pinned, keep the new item and exceed the cap by one rather than deleting a pin (CopyQ refuses and warns; we prefer keep + warn).

### 3.2 Paste

1. Load `GetFormats(id)`; if non-empty → write **raw HGLOBALs** with `OpenClipboard` + `EmptyClipboard` + `SetClipboardData` per format, exactly in stored order, and add `"Clipboard Viewer Ignore"` (+ `CanUploadToCloudClipboard=0`) first. Do **not** route through WPF `DataObject`.
2. Fall back, when no blobs exist, to: kind image → `CF_DIB` + PNG; else text/RTF/HTML planes.
3. `Hide()` the popup → poll `GetForegroundWindow()` until it is the target HWND or 500 ms elapses (Ditto `WaitForActiveWnd`, min 25 ms).
4. Wait until no modifier is physically down (≤ 2000 ms) **and** send explicit key-UPs for Ctrl/Alt/Shift/Win before the chord.
5. Send `Ctrl+V`; if the target never received it (optional: no reliable signal — skip), or make the chord configurable per app (default Shift+Insert for known-legacy windows).

### 3.3 Where our current plan differs — SAFE or RISK

| Current plan | Veterans' equivalent | Flag | One-line reason |
|---|---|---|---|
| Snapshot every HGLOBAL format | Ditto default = 6-format whitelist, all-types opt-in | **SAFE** | Superset only helps fidelity; the whitelist is a hint about what is *essential*, not a reason to store less |
| Skip `CF_DIB`/`CF_DIBV5`, images as re-encoded PNG | Ditto stores `CF_DIB` raw | **RISK** | Bitmap-only consumers (old Win32, some RDP clients) get no image; also loses the original pixel format/alpha |
| `FormatName()` returns null below `0xC000` | Ditto stores `CF_TEXT`, `CF_HDROP` | **RISK** | File copies are captured as nothing at all; ANSI-only apps lose `CF_TEXT` |
| Per-item 10 MB budget, drop offending blobs | CopyQ per-mime cap; Ditto whole-clip abort | **SAFE with caveat** | CopyQ-style is right; but a silent partial set makes a "rich" paste subtly wrong → set a `partial` flag |
| Restore via WPF `DataObject` + `MemoryStream` | Ditto: raw `SetClipboardData(HGLOBAL)`, all formats, marker first | **RISK** | WPF re-packages per-format data (stream vs HGLOBAL) and owns the names; a byte-exact `Biff12`/`HTML Format` round-trip is not guaranteed, and WPF also adds its own conversions |
| `UnicodeText` restored as a C# string | Ditto: raw bytes | **SAFE** | Both are valid; raw bytes is fewer steps and avoids trailing-NUL/BOM surprises, but the string path is not a correctness bug |
| `CF_HTML` stored verbatim with header | Ditto identical | **SAFE** | Keep it; any "clean-up" of the header would break Word/Outlook paste |
| Suppress self-capture by sequence number | marker format on the clipboard | **RISK** | Between `SetDataObject` and the compare, another process can bump the sequence; we then capture our own paste as a new item (and Win+V keeps it too) |
| 100 ms between `Hide()` and `SendPaste()` | Ditto 100 ms `SendKeysDelay`, *after* a focus poll; CopyQ 150 ms after raise | **SAFE on timing, RISK on mechanism** | The number is in line; the missing focus check is not |
| Paste `item.Text` for every item | all three paste the item's full format set | **RISK** | Today this is the only path taken, so rich text and images never reach the target |
| No plain-text hotkey registered | Ditto/CopyQ/PowerToys all implement text-only paste | **RISK** | `Config.HotkeyPlain` is a promise the UI makes and the app does not keep |

### 3.4 Crash/exception resilience in the capture path

- Wrap **each** format read in its own try/catch (a single bad format must not lose the clip) — Ditto logs and skips per format **[S]** `Clip.cpp:502-559`.
- Abort the read if `GetClipboardSequenceNumber()` changed since the notification (clipboard re-owned mid-read) — CopyQ **[S]** `clipboarddataguard.cpp:352-356`.
- Hard time budget for the whole read (CopyQ 5000 ms, env-configurable) and a warning above 500 ms per format **[S]** `clipboarddataguard.cpp:23-28,138-162`.
- Catch `OutOfMemoryException`/`bad_alloc` equivalents around blob copies and skip that format **[S]** `clipboarddataguard.cpp:209-214`.
- Never let an exception escape the `WM_CLIPBOARDUPDATE` handler (a WPF dispatcher exception here kills the app silently in a tray context).

---

## 4. Prioritized ADOPT list (file-level)

### MUST (correctness / data loss / dead features)

1. **Paste the item, not its text.** `App.xaml.cs:77` → load `_store.GetFormats(item.Id)` into `item.Formats` and call `PasteInjector.CopyAndPaste(item)`. Without this, every rich-text and image feature is dead code. *(Ditto `ProcessPaste.cpp:32-56`)*
2. **Protect pinned rows in eviction.** `ClipboardStore.cs:184-193` add `WHERE pinned = 0` to the OFFSET subquery. *(Ditto `DatabaseUtilities.cpp:872-877`, CopyQ `clipboardbrowser.cpp:1568-1593`)*
3. **Store standard formats.** `ClipboardFormats.cs:77-83` — resolve `1,8,13,15,16,17` to canonical names instead of `null`; keep `UnicodeText`. `CF_HDROP` makes file copies work at all. *(Ditto `OptionsTypes.cpp:92-97`)*
4. **Restore through raw HGLOBALs + ignore marker.** `ClipboardFormats.cs:51-74` replace the WPF `DataObject` path with `OpenClipboard`/`EmptyClipboard`/`SetClipboardData` loop, marker format first. *(Ditto `ClipboardSaveRestore.cpp:61-94`, `ProcessPaste.cpp:49`)*
5. **Release/await modifier keys before the paste chord.** `PasteInjector.cs:75-86` send key-UPs for Ctrl/Alt/Shift/Win (PowerToys) or poll `GetKeyState` up to 2 s (CopyQ). *(PowerToys `ClipboardHelper.cs:77-95`)*
6. **Implement the plain-text hotkey.** Register a second `HotKeyWindow` for `Config.HotkeyPlain` and wire `PasteInjector.CopyAndPastePlain` (currently unreachable). *(Ditto `MainFrm.cpp:525-545`)*
7. **Check `CanIncludeInClipboardHistory`/`CanUploadToCloudClipboard`** in `CaptureService.cs:35`. *(CopyQ `winplatformclipboard.cpp:27-48`)*
8. **Set those same formats on paste-back** so our own paste is not ingested by Win+V or other managers. *(learn.microsoft.com clipboard-formats)*

### SHOULD (fidelity / performance / robustness)

9. **Debounce capture by ~100 ms.** `ClipboardListener.cs` → one-shot timer, coalesce bursts. Fixes half-populated Office/browser copies. *(Ditto `ClipboardViewer.cpp:290-294`)*
10. **Owner-based source app.** `ForegroundApp.cs:16` → `GetClipboardOwner()` first, foreground as fallback. Makes app excludes trustworthy. *(Ditto `ClipboardViewer.cpp:317-343`)*
11. **Poll for target focus instead of sleeping blind.** `App.xaml.cs:73` → after `Hide()`, poll `GetForegroundWindow() == target` (25–500 ms) then send; keep 100 ms as the floor. *(Ditto `ExternalWindowTracker.cpp:239-256`)*
12. **Dedupe on re-copy with RTF normalisation.** `CaptureService.cs:66-73` + `ClipboardStore` new `FindByHash`/`Touch`; move to top, keep `id`/`pinned`. *(Ditto `Clip.cpp:767-800,885-950`)*
13. **Millisecond integer timestamps.** `ClipboardStore.cs:21`, `CaptureService.cs:53,111` — `TEXT` at second resolution breaks ordering and dedupe. *(contrast: Ditto `lDate` integer seconds + `clipOrder` REAL as the real sort key)*
14. **Per-format retry + IStream fallback** for NULL reads. `ClipboardFormats.cs:40` → ×2/5 ms, then OLE `GetData`. *(Ditto `Clip.cpp:52-102,518-527`)*
15. **Size caps that do not produce silent partials.** `ClipboardFormats.cs:42` → per-format cap + `partial` flag on the item; never drop `HTML Format` while keeping its text without marking it.
16. **`PRAGMA auto_vacuum=1`** (and optionally `journal_mode=WAL`, `synchronous=NORMAL`) in `ClipboardStore` ctor; add `VACUUM`/size reporting when the file exceeds a threshold. *(Ditto `DatabaseUtilities.cpp:698`)*
17. **Exclude-by-app list** in `Config` (`ExcludeApps`, wildcard) applied after the owner is resolved. *(Ditto `ClipboardViewer.cpp:345-399`)*

### NICE (polish, do later)

18. **Listener re-arm + ping** every 5 min so a dropped `WM_CLIPBOARDUPDATE` chain self-heals. *(Ditto `ClipboardViewer.cpp:105-110,158-172`)*
19. **Reconnect/restore the user's clipboard** ~2 s after a plain-text paste. *(Ditto `MainFrm.cpp:525-545`, `TextOnlyRestoreDelay=2000`)*
20. **Shift+Insert fallback / per-app paste chord.** *(CopyQ `winplatformwindow.cpp:170-178`, `appconfig.h:466-472`)*
21. **Eviction warning when the cap is full of pinned items** instead of dropping the new clip. *(CopyQ `clipboardbrowser.cpp:1582-1589`)*
22. **Deferred blob deletion in idle batches** once history grows past a few thousand rows. *(Ditto `DatabaseUtilities.cpp:924-950`)*
23. **Age-based expiry keyed on last-paste date.** *(Ditto `DatabaseUtilities.cpp:893-922`)*
24. **`dwExtraInfo` tag on injected keys** if we ever add a keyboard hook. *(PowerToys `ClipboardHelper.cs:97-127`)*

### Contradictions with the current design — integration sprint must decide

- **"Snapshot all HGLOBAL formats" vs Ditto's shipped whitelist.** Our choice is the superset and is *better* for Excel/Word, but it is not what the veteran default does. Keep ours; just know the 6 default Ditto types are the must-have floor (`CF_TEXT`, `CF_UNICODETEXT`, `CF_HDROP`, `CF_DIB`, `Rich Text Format`, `HTML Format`) — and today we miss three of them.
- **"Restore via WPF `DataObject` + `MemoryStream`"** is the one place where our plan is materially weaker than Ditto's. If the integration sprint keeps WPF for simplicity, it must accept a possible format-mangling risk on Office formats and should test Excel range paste + Word paste + browser paste explicitly before shipping.
- **100 ms hide→paste** stands; the correction is *how* we wait, not how long.
- **Not capturing the clipboard at startup** matches both veterans; do not "improve" this — it would resurrect secrets on launch.

---

## Sources

- Ditto — https://github.com/sabrogden/Ditto (GPL-3.0). Files cited: `src/Clip.cpp`, `src/ClipboardSaveRestore.cpp`, `src/ClipboardViewer.cpp`, `src/CopyThread.cpp`, `src/ProcessPaste.cpp`, `src/OleClipSource.cpp`, `src/ExternalWindowTracker.cpp`, `src/SendKeys.cpp`, `src/Options.cpp`, `src/OptionsTypes.cpp`, `src/DatabaseUtilities.cpp`, `src/CP_Main.cpp`, `src/MainFrm.cpp`.
- CopyQ — https://github.com/hluk/CopyQ (GPL-3.0). Files cited: `src/platform/win/winplatformclipboard.cpp`, `src/platform/win/winplatformwindow.cpp`, `src/common/clipboarddataguard.cpp`, `src/common/common.cpp`, `src/common/appconfig.h`, `src/common/globalshortcutcommands.cpp`, `src/app/clipboardmonitor.cpp`, `src/app/clipboardownermonitor.cpp`, `src/item/itemfactory.cpp`, `src/gui/clipboardbrowser.cpp`, `src/scriptable/scriptable.cpp`, `docs/faq.rst`, `docs/pin-items.rst`.
- PowerToys Advanced Paste — https://github.com/microsoft/PowerToys (MIT). Files cited: `src/modules/AdvancedPaste/AdvancedPaste/Helpers/ClipboardHelper.cs`, `.../Helpers/TransformHelpers.cs`, `.../Helpers/DataPackageHelpers.cs`.
- Clipboard formats / cloud history opt-outs — https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats (section "Cloud Clipboard and Clipboard History Formats").
- Win+V item cap / size limit — **[U]** not verified here; do not rely on the numbers without checking Microsoft Support directly.
