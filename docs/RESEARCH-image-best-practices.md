# Clip image pipeline — best-practice research and decision sheet

Researched 2026-11-23. Scope: how mature open-source clipboard/capture tools store, thumbnail,
cap, re-paste and open images, plus the two mechanics needed by the new **F2 → open image in
default program** feature and by **notification thumbnails**.

Everything below marked "source" was read in the actual repository at the commit listed in §1,
not inferred from screenshots or blog posts. Where only community evidence exists, it says so.

---

## 0. Top gaps (read this if nothing else)

1. **Alpha is destroyed at capture, before storage.** `CaptureService.CaptureImage` uses
   `System.Windows.Clipboard.GetImage()` (`Core/CaptureService.cs:95`). WPF's default image path
   goes through CF_DIB/CF_BITMAP and drops the alpha channel. The raw `"PNG"` clipboard format —
   the only lossless carrier most apps publish — is never read. ShareX has an explicit setting
   whose description is literally "Default .NET method can't get image with alpha channel from
   clipboard" (`ShareX/ApplicationConfig.cs:226`).
2. **Paste-back never puts a CF_DIB on the clipboard.** `PasteInjector` calls
   `Clipboard.SetImage` (`Interop/PasteInjector.cs:37`), which relies on OLE synthesis and a
   top-down 32-bpp bitmap; ShareX replaced exactly this with its own bottom-up 24-bpp CF_DIB
   because "Windows clipboard history and Office render [the synthesized one] incorrectly"
   (`ShareX.HelpersLib/Helpers/WindowsClipboard.cs:34-37`). Worse, when an image item happens to
   have `Formats` blobs, the paste takes the `Formats` branch first (`PasteInjector.cs:27-31`) and
   may put only `"PNG"`/HTML on the clipboard — which Excel/Word/Paint do not consume.
3. **There is no image UI yet.** `UI/MainWindow.xaml:73-85` binds only `Text`/`Meta`, and
   `App.OnPasteRequested` passes `item.Text` (`App.xaml.cs:77`), so an image item pastes an empty
   string. F2 has nothing to attach to until an image card and an image paste branch exist.
4. **Thumbnail is width-only.** `CaptureService.Thumbnail(src, 96)` scales by width only
   (`Core/CaptureService.cs:129-137`), so a 100×5000 image yields a 96×4800 thumbnail (a large
   BLOB and a giant row). No height cap.
5. **Oversized images are dropped silently and only after a full decode + re-encode**
   (`CaptureService.cs:99-100`). A 200-megapixel PNG below 5 MB would still be decoded in full
   first. No pixel-dimension guard.

---

## 1. Sources (versions, licences, what was read)

| Project | Commit / ref | Licence | Files read |
|---|---|---|---|
| Ditto | `a80fd3536bc407cac8cce43b5682022282ace983` (2026-09-30) | GPL-3.0 | `src/Clip.cpp`, `src/ClipEditThread.cpp`, `src/OleClipSource.cpp`, `src/BitmapHelper.cpp`, `src/ClipFormatQListCtrl.cpp`, `src/QListCtrl.cpp`, `src/CP_Main.cpp`, `src/Options.cpp`, `src/AdvancedGeneral.cpp`, `src/ImageHelper.h`, `src/ClipboardSaveRestore.cpp` |
| CopyQ | `8661ce26313a051b1f7f96de96898c1695477f98` (2026-11-17) | GPL-3.0-or-later | `src/common/common.cpp`, `src/common/clipboarddataguard.cpp`, `src/gui/clipboardbrowser.cpp`, `src/item/itemeditor.cpp`, `src/item/itemfactory.cpp`, `src/platform/win/winplatformclipboard.cpp`, `docs/images.rst`, `docs/faq.rst` |
| ShareX | `099bcb60a1c4af514f8b8b936a870492744e98fd` (2026-11-22) | GPL-3.0 | `ShareX.HelpersLib/Helpers/ClipboardHelpers.cs`, `.../WindowsClipboard.cs`, `.../FileHelpers.cs`, `ShareX/ApplicationConfig.cs`, `ShareX/CleanupManager.cs`, `ShareX/TaskHelpers.cs` |
| PowerToys (Advanced Paste) | `1d47773c7d1a85b77fc8941c392867e8b6dfc10c` (2026-11-23) | MIT | `AdvancedPaste.Engine/AdvancedPasteTempFileManager.cs`, `AdvancedPaste/Helpers/TransformHelpers.cs`, `doc/devdocs/modules/advancedpaste.md` |
| Qt (basis of CopyQ's Windows paste) | qtbase branch `6.8` | LGPL/GPL/commercial | `src/plugins/platforms/windows/qwindowsmimeregistry.cpp` (`QWindowsMimeImage`) |
| Microsoft | live docs, fetched 2026-11-23 | — | *Clipboard Formats*, *Using the clipboard*, *BitmapImage.DecodePixelWidth*, *ProcessStartInfo.UseShellExecute*, *Optimizing Performance: 2D Graphics and Imaging* |

**Licence rule for this doc:** Ditto, CopyQ and ShareX are GPL. We use their source only to learn
behaviour and numbers; every line we ship is written fresh. PowerToys is MIT, but we still write
original code (no copy-paste) so the file-header/licence story stays simple.

**Could not verify:** Qt's Jira pages `QTBUG47656` and `QTBUG-64322` return only a Jira shell
(no content) without a login; their claims are cited from the in-code comments that reference
them. No official Adobe or Microsoft statement on Photoshop/Office clipboard format preference
was found; those rows are marked *community/source-comment evidence*.

---

## 2. What Clip does today (exact behaviour)

| Stage | File:line | Behaviour |
|---|---|---|
| Capture decision | `Core/CaptureService.cs:38-63` | Text wins; image only when clipboard has no text. This already avoids the Office-cell-copied-as-image trap (see §6). |
| Image read | `CaptureService.cs:91-97` | `Clipboard.ContainsImage()` → `Clipboard.GetImage()`; no `"PNG"`, no CF_DIBV5. |
| Encode / cap | `CaptureService.cs:99-102` | Full-image PNG encode, cap `MaxImageBytes` (5 MB, `Core/Config.cs:41`), **silently skip** if over. |
| Thumbnail | `CaptureService.cs:102,129-137` | PNG, max **96 px wide**, aspect by width only, encoded once at capture. |
| Storage | `Core/ClipboardStore.cs:18-25,46-67` | `image BLOB` (full PNG) + `thumb BLOB` in the `items` row. |
| List load | `ClipboardStore.cs:89-117` | `SELECT … thumb …` only; full `image` column never loaded for the 200-row list. **Good.** |
| Paste | `Interop/PasteInjector.cs:25-52` | If item has `Formats`, restore those only; else `SetImage(BitmapSource)`; no CF_DIB bytes. |
| Format snapshot | `Interop/ClipboardFormats.cs:18-19,27-49` | CF_DIB/CF_BITMAP/CF_DIBV5/CF_PALETTE/metafiles skipped; named formats (incl. `"PNG"`) are snapshotted. |
| UI | `UI/MainWindow.xaml:73-85` | No image template; text-only card. |
| Paste orchestration | `App.xaml.cs:69-80` | Always `CopyAndPaste(item.Text)`; image items would paste empty. |
| Notification | `UI/NotificationWindow.xaml:52-53`, `.xaml.cs:88-150` | Text body only; no thumbnail; card 420 × 160 (`Config.cs:53-54`). |
| F2 | — | Not implemented. `OnPreviewKeyDown` handles Esc/Enter/Ctrl+1-9/arrows/Delete only (`MainWindow.xaml.cs:94-136`). |

So today the app has a **capture+store** pipeline and a **text-only UI**. The image half of the
feature is still to be landed; this doc treats the UI/paste work as part of the image work.

---

## 3. Benchmark: practice → who does it → what we do → verdict

| # | Practice | Who does it (evidence) | What Clip does today | Verdict |
|---|---|---|---|---|
| 1 | Store images as **PNG**, not BMP/DIB | CopyQ PNG-first (`common.cpp:235-241`); ShareX PNG for copy (`ClipboardHelpers.cs:72-74`); Ditto stores **both** raw CF_DIB and `"PNG"` (`Clip.cpp:1862-1880`, `CP_Main.cpp:681-687`) | PNG only | **ALIGNED** (deliberate difference vs Ditto: we don't duplicate a 4–8× larger DIB; it is reconstructed at paste time) |
| 2 | Read the clipboard's **`"PNG"` format first** to keep alpha/bytes | ShareX `GetImage` tries `"PNG"` then bitmap (`ClipboardHelpers.cs:158-166`); Ditto treats `"PNG"` as a supported type (`CP_Main.cpp:687`) | `Clipboard.GetImage()` only | **GAP: adopt** |
| 3 | Keep original PNG bytes **verbatim** (no re-encode) | CopyQ clones the `image/png` bytes; Ditto stores them as received | re-encodes whatever GetImage returned | **GAP: adopt** |
| 4 | Thumbnail generated **once**, small, persisted | Clip (persisted thumb); Ditto generates on demand per visible row and caches in RAM, no disk thumb (`QListCtrl.cpp:1166-1195`, `ClipFormatQListCtrl.cpp:18-53`); CopyQ decodes on demand and scales to row height (`itemfactory.cpp:55-74`); ShareX writes a separate `_thumb.jpg` at 200×150 (configurable) (`TaskHelpers.cs:465-486`, `ApplicationConfig.cs:121`) | 96 px wide, persisted, once | **ALIGNED in spirit**; fix height cap (§5) |
| 5 | Never load full images into the 200-row list | Clip already selects only `thumb`; CopyQ spills large data to separate on-demand files (`docs/faq.rst:571-581`); Ditto loads only visible rows | Only `thumb` in `GetRecent` | **ALIGNED** — keep it, and decode lazily once per item (§5) |
| 6 | Paste a **real CF_DIB (24-bpp, bottom-up)** plus `"PNG"` | ShareX 2026-11: `SetData(CF_DIB, dib)` then `SetData("PNG", png)` (`WindowsClipboard.cs:45-56`); Qt advertises CF_DIB always and `"PNG"` only for alpha (`qwindowsmimeregistry.cpp:820-860`) | `SetImage` (OLE synthesis), or only `Formats` blobs | **GAP: adopt** (highest impact after capture) |
| 7 | Carry alpha in `"PNG"`; flatten to white for CF_DIB | ShareX fills white for the DIB unless alt mode (`ClipboardHelpers.cs:86-96,220-227`); Qt adds **real CF_DIBV5** when the image has alpha (`qwindowsmimeregistry.cpp:824-828`) | alpha lost at capture, nothing to carry | **GAP: adopt** (PNG capture; white flatten; DIBV5 optional) |
| 8 | Don't treat an Office/Calc cell copy as an image | CopyQ drops binary image data when text is present (`common.cpp:163-198`); Ditto users suffer thumbnails-of-spreadsheets (`Ditto #945`, `#964`); Win+V supports bitmap but the text copy wins in practice | Text checked first — image branch only when no text | **ALIGNED** |
| 9 | Per-item size cap | Win+V **4 MB**, 25 entries (Microsoft support); CopyQ 100 MiB and 4096×4096 (`clipboarddataguard.cpp:81-117`, `common.cpp:82-87`); Ditto default unlimited, user cap, rejects the **whole clip** (`Clip.cpp:529-542`, `Options.cpp:1705-1708`); ShareX no cap (writes files) | 5 MB → skip silently | **ALIGNED on number, GAP on handling** (notify; add pixel guard) |
| 10 | Very large image policy | Win+V: ignored; Ditto: whole clip rejected, user sees nothing; CopyQ: >4096 px skipped; ShareX: always writes a file (no clipboard-size limit) | skip silently | **DELIBERATE DIFFERENCE** (skip, like Win+V) — but add a visible reason. Do **not** keep a thumb-only entry: an entry that cannot paste is a trap |
| 11 | Delayed rendering on paste | Ditto delay-renders every format (`OleClipSource.cpp:39-59`); ShareX writes immediately; Win32 docs describe delayed rendering | immediate `SetDataObject(copy:true)` | **DELIBERATE DIFFERENCE** (acceptable ≤5 MB; see §6 memory note) |
| 12 | Open image in external app via shell `open` | ShareX: `ProcessStartInfo { FileName = path, UseShellExecute = true }` (`FileHelpers.cs:254-270`); Ditto: writes `EditClip_<id>.png` then `ShellExecuteEx("open")` (`CP_Main.cpp:1175-1234`) | none | **GAP: adopt** (feature) |
| 13 | Temp/cache lifecycle for opened images | Ditto keeps files in a temp `EditClips\` folder and deletes files older than **7 days** (`ClipEditThread.cpp:39-40`, `Options.cpp:1886-1891`); PowerToys uses a random temp dir + ownership marker, cleans **>1 day** (`AdvancedPasteTempFileManager.cs:16-70`); ShareX deletes its whole `%TEMP%\ShareX` at startup (`CleanupManager.cs:93-107`); CopyQ deletes the temp file when its editor closes (`itemeditor.cpp:64-75`) | none | **GAP: adopt** — cache under `%APPDATA%\Clip`, 7 days + count cap, never delete on exit |
| 14 | Notification thumbnail sized to card, decoded at display size | No OSS equivalent (custom cards); WPF guidance: "Always decode the image to desired size and not to the default size" (MS *Optimizing Performance*); `DecodePixelWidth` decodes PNG natively at that size (MS API doc) | text-only notification | **GAP: adopt** (§9) |

---

## 4. Storage format and capture

### 4.1 Storage format — PNG, yes

- CopyQ: prefers `image/png`, keeps webp/gif only when the source actually offers them, and drops
  the remaining image MIMEs once PNG is present (`src/common/common.cpp:235-255`); large item data
  is spilled to separate on-demand files (`docs/faq.rst:571-581`).
- ShareX: copy path encodes PNG bytes and ships them to the clipboard
  (`ClipboardHelpers.cs:72-74`).
- Ditto: stores the original `CF_DIB` **and** `"PNG"` as separate format blobs when the source
  offers them, and creates both for an edited image file (`Clip.cpp:1862-1880`,
  `ClipEditThread.cpp:250-286`). It pays 4–8× disk for the DIB but keeps maximum fidelity.
- Win+V only supports "Bitmap" per Microsoft's support page (no PNG in the list), but that is an
  OS-side limitation, not a model to copy.

**Verdict: keep PNG-only in `items.image`.** It is lossless, alpha-capable, and 4–8× smaller than
a DIB. A DIB is reconstructed at paste time (§6) instead of stored.

### 4.2 Capture order (the actual fix)

Recommended order, each step guarded:

1. `Clipboard.GetData("PNG")` → `MemoryStream` → `ToArray()`. If the stream is a valid PNG
   (`89 50 4E 47` signature), **use those bytes verbatim** as `item.Image`. Also derive the
   `BitmapSource` from them for the thumbnail (decode once).
2. CF_DIBV5 via Win32 (`GetClipboardData(17)`) → decode 32-bpp bottom-up, non-premultiplied.
   This is the only way to recover alpha from apps that publish DIBV5 but not `"PNG"`.
   *Should*, not must — add only if the manual test matrix shows a real source losing alpha.
3. `Clipboard.GetImage()` as today (DIB/CF_BITMAP fallback, opaque).

Evidence: ShareX does exactly 1→3 (`ClipboardHelpers.cs:158-166`); Ditto's supported-type list
contains `"PNG"` (`CP_Main.cpp:687`); CopyQ prefers `image/png` and falls back to `image/bmp`
(`common.cpp:278-288`).

> **Why not just re-encode `GetImage()`?** Once WPF has decoded a DIB, the alpha channel is gone
> (or black). Re-encoding to PNG preserves the loss, it does not recover it. The Ditto thread is
> explicit: with only CF_DIB, "alpha areas will just fill with black", reproduced in
> Clip Studio/Photoshop/Krita/Blender (`Ditto #584` comments, 2023-12-29).

### 4.3 Format snapshot interaction

`ClipboardFormats.Snapshot` already skips numeric CF_DIB/CF_DIBV5/CF_BITMAP, but it **does**
capture the registered `"PNG"` format (`Interop/ClipboardFormats.cs:18-19,34-45`). For an image
item that means the PNG is stored twice (up to 10 MB `MaxItemBytes` per item) and the paste path
takes the `Formats` branch before the image branch. Two options:

- **Preferred:** in `CaptureService.Store`, skip `InsertFormats` for `Kind == "image"` (or skip
  the `"PNG"`/`"HTML Format"` entries), so images have exactly one source of truth.
- **Minimum:** in `PasteInjector.CopyAndPaste`, handle `Kind == "image"` **before** the
  `Formats` check.

Either is a few lines; doing both is safest.

---

## 5. Thumbnails and memory

### 5.1 Size and generation

| Tool | Thumb size | When | Persistent? |
|---|---|---|---|
| Clip | ≤96 px wide | at capture | yes, `thumb BLOB` |
| Ditto | row height (≈50 px, DPI-scaled) | on first paint of a visible row | RAM only (`m_ThumbNails`), freed with the row cache; option warns it "could increase memory usage" (`AdvancedGeneral.cpp:319`) |
| CopyQ | scales to item row height, smooth | on item widget creation | RAM only (`itemfactory.cpp:55-74`) |
| ShareX | 200×150 default, JPEG q90, white background | at file save | separate `_thumb.jpg` file |

Clip's choice (persist a small thumb, never decode the full image for the list) is the right one
for a 60 MB idle target. Keep it.

**Fix:** cap **both** dimensions. A 96×72 box is the natural card size; scale uniformly by
`min(96/w, 72/h)` and never upscale. One `TransformedBitmap` with a single uniform scale, then
`Freeze()`.

**DPI:** at 150 % scaling a 96 DIP-wide image needs 144 physical px; at 200 %, 192 px. Since the
thumb is also used by the notification and is tiny, generate at **192×144** and let WPF downscale.
Cost: 200 thumbs × ~10-20 KB ≈ 2–4 MB of DB; decoded only for realized rows. If that is too much,
96×72 is acceptable at 100 % and only slightly soft on HiDPI.

### 5.2 Memory discipline rules (keep these invariant)

- `GetRecent` must never select `image`; it does not today (`ClipboardStore.cs:94-97`).
- Decode a thumb **once per item**, not per binding: add a lazy `ImageSource ThumbSource` on
  `ClipboardItem` (frozen `BitmapImage`, `DecodePixelWidth = 96..192`, `CacheOption.OnLoad`).
  A value converter that decodes on every call will re-decode on scroll and blow the RAM target.
- `ListBox` virtualization stays on (default). Do not wrap the list in a `ScrollViewer`, which
  disables it.
- F2, paste, and notification must load the full `image` **only for the one item** involved.
- `GetImage` returns one full PNG; release the reference after `SetClipboardData`/file write.

---

## 6. Paste-back format set — what to put on the clipboard

### 6.1 Evidence table (what each app family actually consumes)

| App family | Consumes | Evidence / confidence |
|---|---|---|
| Excel, Word, PowerPoint, Paint (classic Office/Win32) | **CF_DIB** (24-bpp, bottom-up, BI_RGB); CF_BITMAP is synthesized from DIB | MS *Clipboard Formats*: "When copying bitmaps, it is best to place the CF_DIB or CF_DIBV5 format"; ShareX comment: the opaque 24-bpp bottom-up DIB "is what Office and Windows clipboard history expect" (`ClipboardHelpers.cs:86-88`). **Strong.** |
| Windows clipboard history (Win+V) | Bitmap (CF_DIB); 4 MB/item, 25 items | Microsoft support, *Using the clipboard*. **Strong.** |
| Browsers (Chrome/Edge/Firefox), Electron apps (Slack/Teams/VS Code), Qt apps | `"PNG"` and/or CF_DIB | CopyQ ships `image/png` via Qt, which advertises CF_DIB + `"PNG"` (`qwindowsmimeregistry.cpp:820-860`); ShareX reads `"PNG"` first. **Good, not per-browser verified.** |
| Photoshop, Krita, Clip Studio, Blender | alpha requires `"PNG"` **or real CF_DIBV5**; plain CF_DIB loses alpha (black) | Ditto #584: alpha lost with CF_DIB alone; user says CopyQ handles it. Qt adds CF_DIBV5 only when the image has alpha, precisely for this. **Community + source-comment evidence.** |
| Windows Explorer / file targets | CF_HDROP (a real file). Optional bonus: our F2 cache file could be offered | Ditto contributor: with CF_HDROP the graphics app "works correctly" (`Ditto #584`, 2024-01-04). **Community.** |

### 6.2 Recommended format sequence (adopt)

Write with Win32 `OpenClipboard`/`EmptyClipboard`/`SetClipboardData` in this order (order is the
fallback priority — apps take the first format they recognise; MS *Clipboard Formats*):

1. **CF_DIB** — 24-bpp, `BI_RGB`, **bottom-up**, rows padded to 4 bytes, alpha composited **over
   white**. Always present. This is the universal Office/Paint/Win+V path and it is what ShareX
   ships first (`WindowsClipboard.cs:53`).
2. **`"PNG"`** — the exact stored PNG bytes (including alpha). Present always (cheap; it is the
   bytes we already have). Chromium/Qt/editors that explicitly ask for PNG get true alpha.
3. *(Should, gated by manual test)* **CF_DIBV5** — 32-bpp, bottom-up, non-premultiplied,
   `BITMAPV5HEADER` with sRGB color space, only when the image has translucent pixels. This is
   the only alpha-capable standard bitmap format; Qt sets it for alpha images
   (`qwindowsmimeregistry.cpp:824-828`). Office takes CF_DIB first anyway, so putting this last
   should not disturb it — but **verify** Excel/Word/PPT/Paint before shipping it.

Do **not** set CF_BITMAP explicitly: Windows synthesizes it from CF_DIB (MS *Clipboard Formats*),
and a device-dependent bitmap can pick up the wrong palette.

Do **not** put `"PNG"` *first*. Qt has two in-code warnings that advertising CF_PNG confuses MS
Office (`qwindowsmimeregistry.cpp:824`, `QTBUG47656`) and that PowerPoint cannot handle PNG-only
images (`QTBUG-64322`). With CF_DIB first, Office takes DIB and never reaches PNG; PNG stays
available to apps that ask for it by name.

### 6.3 Alpha policy

- Store alpha losslessly (PNG captured verbatim).
- On paste, alpha lives in `"PNG"` (and optionally DIBV5); the CF_DIB fallback is **flattened
  onto white**, not black. ShareX does exactly this (`ClipboardHelpers.cs:86-96`); Qt's
  ARGB32→RGB32 conversion is the source of the black-background complaints in the Ditto thread.
- Accept that Paint and some Office paths will show a white background — they have no alpha
  model. That is correct, not a bug.
- Optional later: add CF_HDROP pointing at the F2 cache file for graphics apps. Do not do this
  by default: it changes paste semantics in Explorer and creates a file dependency.

### 6.4 Memory note (why we can skip delayed rendering)

A 5 MB PNG can decode to a 3840×2160×4 ≈ 33 MB DIB that the OS owns until the clipboard changes.
That is transient and outside our idle RAM budget, so immediate rendering is acceptable at the
current cap. Ditto delay-renders every format (`OleClipSource.cpp:39-59`) to avoid this; that is
the upgrade path if the cap is ever raised or multi-hundred-MB screenshots appear. Mark as
`shortcut:` in code if we keep immediate rendering.

---

## 7. Size caps and oversized images

| Tool | Cap | Oversized behaviour |
|---|---|---|
| Clip | 5 MB encoded PNG | skip silently (`CaptureService.cs:100`) |
| Win+V | 4 MB per item; 25 items | ignored (Microsoft support) |
| CopyQ | 100 MiB per MIME default; 4096×4096 px | skip (`common.cpp:82-87`, `clipboarddataguard.cpp:112-116`) |
| Ditto | unlimited by default; optional `MaxClipSizeInBytes` | rejects the whole clip, no entry (`Clip.cpp:529-542`) |
| ShareX | none (writes a file) | n/a |

**Decision:** keep 5 MB (slightly looser than Win+V, reasonable for a clipboard manager) and keep
"skip" as the policy — matching Win+V is the least surprising behaviour, and a thumb-only entry
that cannot paste is worse than no entry.

Add two guards and one message:

- Reject **before decode** when the `"PNG"` byte length already exceeds `MaxImageBytes`.
- Reject when `PixelWidth * PixelHeight > 40_000_000` (or either side > 8192), regardless of
  encoded size — decompression-bomb guard. CopyQ's 4096×4096 is the precedent.
- On rejection, show the existing notification with "Image too large to save (12.4 MB, limit
  5 MB)" instead of silence. One line in `App.OnCaptured`/`CaptureService`.
- Do not attempt a degraded (recompressed/JPEG) copy: it silently changes the user's pixels.

Also note Win+V will not capture our pasted image if the DIB exceeds 4 MB; nothing we can do, but
worth a line in the user guide.

---

## 8. F2 → open image in the default program

### 8.1 Where the file lives

Constraint: Clip writes only inside its own folder and `%APPDATA%\Clip`. `Config.DataDir` is
already `%APPDATA%\<assembly name>` (`Core/Config.cs:12-15`), so:

```
%APPDATA%\Clip\cache\images\clip-<id>.png
```

Do **not** use `%TEMP%`: violates the owner's write constraint, and ShareX/Ditto both learned that
temp folders need their own cleanup policy anyway (ShareX deletes `%TEMP%\ShareX` wholesale at
startup, `CleanupManager.cs:93-107`).

### 8.2 Naming

`clip-<id>.png` where `<id>` is `items.id` (unique, already chronological). No app name or window
title in the name — that leaks user content into a path that other apps, backup tools and the
shell's recent-files list can see. The id also makes the write idempotent and collision-free;
Ditto uses the same idea (`EditClip_<id>.png`, `CP_Main.cpp:1175`), PowerToys timestamps because
it has no database id (`TransformHelpers.cs:132-138`).

If a content hint is wanted later, add dimensions (`clip-<id>-1920x1080.png`) once width/height
are stored — do not put app/title in it.

### 8.3 Lifecycle (recommended: lazy write, keep for 7 days, cap by count)

- **On F2:** write only if missing or length differs; otherwise reuse. Re-writing a file a viewer
  currently holds open throws; reuse avoids that.
- **Write atomically:** `clip-<id>.png.tmp` → `File.Move(tmp, path, overwrite: true)`.
- **Do not delete on exit.** The launched viewer may still read the file, and users expect
  "Save As" to work after they close and reopen the app. ShareX's own external-open path passes
  the real saved file, never a delete-on-exit temp (`FileHelpers.cs:254-270`).
- **Cleanup on startup:** delete `clip-*.png` with `LastWriteTimeUtc` older than **7 days**
  (Ditto's exact policy, `ClipEditThread.cpp:39-40`), then keep at most **100** newest by write
  time (ShareX's count-based cleanup pattern, `CleanupManager.cs:75-91`). Wrap in try/catch;
  locked files are skipped until next start.
- **On history delete/clear:** best-effort delete the matching `clip-<id>.png`. Nice, not must.
- PowerToys' ownership-marker trick (`AdvancedPasteTempFileManager.cs:16-49`) is only needed when
  you delete by directory prefix in a shared temp dir; we own the whole subfolder, so skip it.

### 8.4 Code shape (no P/Invoke, ~15 lines)

```csharp
// Core/ImageCache.cs
public static string Materialize(long id, byte[] png)
{
    Directory.CreateDirectory(Dir);                       // Config.DataDir\cache\images
    var path = Path.Combine(Dir, $"clip-{id}.png");
    if (!File.Exists(path) || new FileInfo(path).Length != png.Length)
    {
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, png);
        File.Move(tmp, path, overwrite: true);
    }
    return path;
}

// App.xaml.cs — handler for MainWindow.OpenImageRequested
var full = _store.GetImage(item.Id);
if (full?.Image is not { Length: > 0 }) return;
var path = ImageCache.Materialize(item.Id, full.Image);
Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
```

`UseShellExecute = true` is required to hand a document to its registered default app; without it
`Process.Start` only runs executables (MS `ProcessStartInfo.UseShellExecute`; ShareX uses exactly
this, `FileHelpers.cs:262-266`). No `ShellExecuteEx` needed — we do not care about the process
handle. Wrap in `try/catch (Win32Exception)` and show the existing notification ("No default app
for PNG") rather than crashing.

### 8.5 UI wiring and edge cases

- `MainWindow.OnPreviewKeyDown`: handle `Key.F2` **before** the existing chain, only when
  `Keyboard.Modifiers == ModifierKeys.None` and `History.SelectedItem is ClipboardItem { Kind:
  "image" }`. Raise `OpenImageRequested`, `e.Handled = true`. Do nothing (don't mark handled) for
  text items so F2 stays free later.
- Hide the popup before `Process.Start` (same reason as paste: the target app must get
  foreground). `OnDeactivated` would hide it anyway, but doing it first avoids a flicker.
- Cap the write: an F2 on an image that is somehow missing from the DB should no-op, not throw.
- Known limit: an external editor that saves over the cache file changes the cache copy, not the
  history row. Document it; do not make the file read-only (some editors then fail to save).

---

## 9. Notification thumbnail (custom 420 × 160 card)

Current card: `Config.Notification.Width = 420`, `Height = 160` (`Config.cs:53-54`), padding
14,10 (`NotificationWindow.xaml:9-10`), text body only.

Recommendation:

| Property | Value | Why |
|---|---|---|
| Box | **40 × 40 DIP**, left of the body, 10 px gap | 40 fits a 160-high card with a 3-line text body and matches the 12 px text scale; 48 also works if the body drops to 2 lines |
| Stretch | `Uniform` inside a `Border` with `Raised` background, `ClipToBounds` | show the whole image (users recognise it), no crop; letterbox blends with the card |
| Decode | `BitmapImage` with `DecodePixelWidth = 96`, `CacheOption = OnLoad`, `Freeze()` | 96 px covers 40 DIP at 240 % scaling; PNG decodes natively at the requested size (MS `DecodePixelWidth`) and "always decode the image to desired size" is the WPF perf guidance (MS *Optimizing Performance*) |
| Scaling mode | `RenderOptions.BitmapScalingMode="HighQuality"` on the `Image` | clean downscale of a 192 px thumb to 40 DIP |
| Source | `item.Thumb` only — never `item.Image` | 5 MB decode on a toast would spike working set for no visible gain |
| Text | `"Image · 1.2 MB"` using `SizeBytes`, or `"Image · 1920×1080"` if dims are added | gives the card useful content where text items show a snippet |
| Visibility | collapse the image column for text items | no empty box |

Layout change is small: add a 2-column `Grid` inside the existing row 1, `Image` in column 0,
`Body` in column 1. No animation changes.

---

## 10. Prioritized ADOPT list

### MUST (feature is broken/incomplete without these)

1. **`Core/CaptureService.cs`** — read `"PNG"` first (`Clipboard.GetData("PNG")` → bytes) and use
   those bytes verbatim; fall back to `Clipboard.GetImage()`. This is the only way alpha survives.
2. **`Core/CaptureService.cs`** — reject before decode when the PNG byte length > `MaxImageBytes`,
   and when `w*h > 40M` or either side > 8192. On reject, emit a notification with the reason.
3. **`Interop/PasteInjector.cs`** — for `Kind == "image"`, bypass the `Formats` branch and call a
   new `Interop/ClipboardImageWriter` that writes **CF_DIB (24-bpp bottom-up, white-composited)
   then `"PNG"`** via `OpenClipboard`/`SetClipboardData`. Do not use `Clipboard.SetImage`.
4. **`Core/CaptureService.cs`** — for image items, skip `InsertFormats` (the `"PNG"` blob is a
   duplicate and misroutes paste); or at minimum handle images before `Formats` in paste.
5. **`UI/MainWindow.xaml` + `.xaml.cs`** — image card (96×72 thumb, rounded, `Uniform`,
   "Image · size") and F2 handler raising `OpenImageRequested` for `Kind == "image"` only.
6. **New `Core/ImageCache.cs` + `App.xaml.cs`** — `%APPDATA%\Clip\cache\images\clip-<id>.png`,
   write-if-changed, `Process.Start(UseShellExecute = true)`, `Win32Exception` → notification.
7. **`App.xaml.cs`** — fix `OnPasteRequested` to pass the item, not `item.Text`.
8. **`Core/ImageCache.cs`** — startup cleanup: age > 7 days, then keep newest 100.

### SHOULD

9. **`Core/CaptureService.cs`** — thumbnail capped in **both** dimensions (96×72 DIP, generate at
   192×144 for HiDPI), uniform scale, no upscale.
10. **`Core/ClipboardItem.cs`** — lazy frozen `ThumbSource` (`DecodePixelWidth` = display px ×
    DPI, `OnLoad`, `Freeze`) so scrolling never re-decodes; bind the card to it.
11. **`Interop/ClipboardFormats.cs`** — when the clipboard offers `"PNG"` for an image item, do
    not store it a second time in `item_formats`.
12. **`UI/NotificationWindow.xaml(.cs)`** — 40×40 image thumb, `DecodePixelWidth = 96`,
    `"Image · 1.2 MB"` body, collapsed for text.
13. **`Core/CaptureService.cs`** — read CF_DIBV5 via Win32 when `"PNG"` is absent, to recover
    alpha from apps that publish only DIBV5. Only if the test matrix shows a real case.
14. **`Interop/ClipboardImageWriter.cs`** — add real CF_DIBV5 (32-bpp, bottom-up,
    non-premultiplied, sRGB header) **last** when the image has alpha; verify Office first.

### NICE

15. **`UI/SettingsWindow.xaml(.cs)`** — expose `MaxImageBytes` (and optionally the cache limits)
    instead of hand-editing `config.json`.
16. **`Core/ClipboardStore.cs`** — delete `clip-<id>.png` on item delete / clear (best effort).
17. **`Core/ClipboardStore.cs` + `CaptureService`** — store `width`/`height` (migration) for
    "1920×1080" labels and dimension-hinted cache filenames.
18. **`Interop/PasteInjector.cs`** — optional CF_HDROP (the F2 cache file) for graphics apps;
    off by default, or as a "Paste as file" action.
19. **`Interop/PasteInjector.cs`** — delayed rendering (`WM_RENDERFORMAT`) if the cap is raised;
    Ditto's model. Not worth it at 5 MB.
20. **`Core/ImageCache.cs`** — CF_HTML `<img src="file:///…">` alongside the image on paste
    (ShareX does this when a filename exists, `ClipboardHelpers.cs:79-84`); only after the cache
    lifecycle is stable, because it creates a file dependency.

**Size of the must-do set:** ~2 helpers (image writer, image cache), two small branches in
capture/paste, one XAML template, one key handler, one App handler. No schema change, no new
dependency.

---

## 11. Manual test matrix (run before calling the paste work done)

| Target | Action | Expect |
|---|---|---|
| Excel / Word / PowerPoint | Ctrl+V an image item | image inserted, correct colours, no "picture too large" popup |
| MS Paint | Ctrl+V | image, white background where transparent |
| Chrome / Edge (contenteditable, e.g. Gmail) | Ctrl+V | image inserted |
| Slack / Teams / VS Code | Ctrl+V | image inserted |
| Windows Explorer | Ctrl+V | nothing (we set no CF_HDROP) — acceptable |
| Win+V after pasting | open history | image entry present if ≤4 MB |
| Photoshop / Krita / GIMP (if installed) | Ctrl+V a transparent PNG | alpha preserved (or documented loss if DIBV5 not shipped) |
| Copy a range of Excel cells | capture | text item, **not** an image item |
| Copy a 6000×4000 photo > 5 MB | capture | notification "too large", no row |
| F2 on an image | press | default viewer opens `%APPDATA%\Clip\cache\images\clip-<id>.png` |
| F2 twice | press twice | same file reused, no error if the viewer holds it |
| Restart after 8 days | start | old cache files gone, recent ones kept |
| HiDPI 150 %/200 % | open popup/toast | thumbs sharp, rows not huge |

---

## 12. Open points / confidence

- **Office + CF_DIBV5** — not verified on current Office builds. Ship DIB + PNG first; add DIBV5
  only after the matrix passes. Qt's code deliberately uses DIBV5 for alpha and Office-compatible
  DIB for everything else, which is the same compromise.
- **Photoshop** — no official format documentation found. Community reports say CF_DIB alone
  loses alpha and even PNG-on-clipboard can still lose it in Photoshop. Do not promise Photoshop
  alpha until tested; the PNG is stored losslessly either way.
- **Win+V 4 MB** — Microsoft's support page is the source; it does not document whether the 4 MB
  is measured on the DIB or the encoded PNG. Treat it as an approximate ceiling.
- **CopyQ's Windows image plugin** — CopyQ's docs say an "Item Image" plugin must be installed on
  Windows (`docs/images.rst:11`); that is why its effective behaviour is Qt's
  `QWindowsMimeImage`, not CopyQ's own code.
- **PowerToys Advanced Paste** does not show clipboard image thumbnails; its image file actions
  are "Paste as .png file" / "Paste as .jpg file" (plus OCR), which write a timestamped image into
  a temp dir and put a file on the clipboard (`TransformHelpers.cs:58-70,132-138`). Useful only as
  a temp-lifecycle precedent.
