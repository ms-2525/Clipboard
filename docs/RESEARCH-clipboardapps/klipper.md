# Klipper (KDE Plasma) — clipboard popup research

Read from source 2026-11-23. Klipper no longer has its own repo: it is `plasma-workspace/klipper` (`plasma/klipper` 404s on
invent). File refs are relative to `klipper/` at `https://invent.kde.org/plasma/plasma-workspace/-/blob/master/klipper/<path>`.

| | |
|---|---|
| Product | Klipper — the clipboard manager built into KDE Plasma (applet + service, no standalone window) |
| Version / license | plasma-workspace `master` (Plasma 6.x, Qt 6 + QML) · GPL-2.0-or-later · X11 + Wayland |
| Size | 76 files, ~1.9 MB; UI = 10 QML files (`declarative/qml/`) + 3 C++ viewmodels; **20 rows kept** (`klipper.kcfg:56`) |
| History | 2000-era KDE2 code; popup rewritten in QtQuick in 2024 (Fushan Wen) — `klipperpopup.cpp` is only a QML window wrapper |

## 1. List anatomy — popup, row, selection, search

**Window:** `PlasmaExtras.Representation`, width `clamp(15·gridUnit, screenW/4, 40·gridUnit)`, height `min(screenH/2, 40·gridUnit)`
(`declarative/qml/KlipperPopup.qml:50-51`); opens **under the cursor** (`klipperpopup.cpp:107-108`), **hides on any focus change**
(`:118-120`), global shortcut Meta+V (`klipper.cpp:143`).

**Header**, 2–3 rows (`ClipboardMenu.qml:255-403`): (1) `SearchField` + icon-only *Clear History* + *Configure* (`:263-326`), search
`enabled: count>0 || text.length>0` (`:266`); (2) `TabBar` **All History | Starred Only**, visible only when `starredCount>0` (`:329-375`);
(3) conditional inline warning that the clipboard holds a password, with "Clear Secret Data" (`:376-402`).

**Row** (`ClipboardItemDelegate.qml` — 308 lines for one row): height `max(textHeight, 1.6·gridUnit) + 2·smallSpacing` (`:61`), with
`isTall` above `2.5·gridUnit` top-aligning the buttons (`:44`). Selection = a filled highlight behind the row (`:103-106`) and the list
is `highlightFollowsCurrentItem: false` (`:452`) so keyboard nav never scrolls content under a resting mouse; hovering a row **moves the
current index to it** (`:98`) — one selection model for mouse and keys. **Hover toolbar** (`DelegateToolButtons.qml:33-63`): Invoke
action (only when `hasAction`), Show QR and Edit (text only), Remove, Star — current row only, hovered or keyboard-focused
(`ClipboardItemDelegate.qml:267`), with the text behind them **faded by an OpacityMask gradient** instead of ellipsised (`:114-148`).
Enter selects, Delete removes (`:83-87`).

**What a row shows:** plain text only — **3 lines, 100 codepoints**, elided, word-wrapped (`TextItemDelegate.qml:20, 32-33, 53-54`).
**No timestamp, no source app, no type icon, no format badge.** `display` is literally `item->text()`; for images it is `"▨ <w>x<h>"`
(`historymodel.cpp:495-503`).

**Search = filter-as-you-type on `display` only** (`ClipboardMenu.qml:457-466`: `KSortFilterProxyModel{filterRoleName:"display";
RegExp(text,"i")}`), resetting `currentIndex` to 0 on every filter change (`:461-465`). Typing while the *list* has focus is forwarded
into the field char by char (`:100-110, 212-215`), so is Backspace (`:136-145`); `Ctrl+F` focuses + select-all (`:120-129`); `Esc`
clears the filter, only a second Esc closes (`:113-119`). **Match text is never highlighted in rows.** Empty states differ:
`No matches` vs `Clipboard is empty` (`:519-533`).

## 2. Rich text

- **No rich preview whatsoever.** Rows are plain text. `TextFormat.StyledText` (`TextItemDelegate.qml:55`) exists only to inject
  `<font>` whitespace glyphs and `<br>`, and the clip is **HTML-escaped first** (`:36`) so copied HTML can never execute. (Clip ships
  `HtmlRenderer.WPF`: sanitise it and block remote images, or a copied page becomes a tracking pixel.)
- Whitespace is made legible instead: leading/trailing space → `␣`, tab → `⇥`, newline → `↵`, in the highlight colour (`:38-46`).
- Rich formats **are stored and re-pasted**: `updateclipboardjob.cpp:55-72, 137-178` keeps every accepted mimetype (PNG-vs-lossy
  dedupe); selecting republishes the full mime data, so bold and links survive into LibreOffice.
- **Editing silently destroys them**: `setData(Qt::DisplayRole)` rewrites `mimetypes` to `text/plain`, deletes all `aux` rows and the
  stored files (`historymodel.cpp:559-601`).

## 3. Images

- Image rows = a real thumbnail, `sourceSize.height = min(h, 4·gridUnit + 2·smallSpacing)`, aspect-preserved, clamped to the row,
  left-aligned, no caption (`ImageItemDelegate.qml:26-40`).
- File clips get **N shell thumbnails in a horizontal strip**, 4·gridUnit squares, each with a 60 %-opacity caption bar carrying the
  filename, plus `+N` when more URLs were copied than fit (`UrlItemDelegate.qml:18-45, 62-111`); thumbnails come from
  `KIO::filePreview` off-thread with a mime-icon fallback (`declarative/previewimageprovider.cpp:31-56`).
- Kind is one function — Url > non-empty Text > Image (`historyitem.cpp:37-46`) — and picks one of three QML delegates
  (`ClipboardMenu.qml:414-444`).

## 4. UX model

- Index 0 = current clipboard; selecting = move to top + write the clipboard; duplicates deduped by SHA-1 of the payload
  (`historymodel.cpp:160-180, 761-775`).
- **Selecting does not paste.** A row click only sets the clipboard and closes (`ClipboardMenu.qml:56` → `slotRepeatAction`); the user
  presses Ctrl+V in the app that regains focus. Deliberate on Wayland (no synthetic keys). In the popup: arrows/Home/End/PgUp/PgDn
  (`:146-197`), Tab moves focus into the row toolbar (`ClipboardItemDelegate.qml:86-87`).
- **No numbering, no numeric quick-select.** Only `Alt+1`/`Alt+2` for the two tabs (`ClipboardMenu.qml:198-209`). Global *Rotate History
  Up/Down* lives outside the popup (`klipper.cpp:396-412`) with a passive toast.
- **HoverBlocker** (`HoverBlocker.qml`): swallows hover until the mouse physically moves, so keyboard/scroll input never triggers the row
  that happens to sit under the cursor.
- **Predefined actions:** user regex actions in KConfig (`Regexp`, `Description`, `Automatic`, `Command_N`) **plus automatic MIME
  actions** built at runtime from every installed app handling the clip's mimetype (`urlgrabber.cpp:125-159`). Matches get the per-row
  *Invoke action* button and can pop the app/command menu on copy (`:166-225`) — an unstyled `QMenu` in its own window that
  **self-destructs after 8 s** (`:37-43, 309-321`; `klipper.kcfg:49`).
- **QR hand-off**: any text clip can push a `BarcodePage` rendering the full text as a QR code (`BarcodePage.qml:103-109`) — phone, no account.

## 5. Visual craft & pitfalls

**Good, all WPF-portable:** per-type row templates; whitespace glyphs; a hard 3-line/100-char preview budget that keeps rows scannable;
hover toolbar with gradient fade; hover-moves-selection + HoverBlocker; keyboard-first focus rings and focus-triggered tooltips; honest
empty states; a Starred tab that appears only when useful; password warning with an escape hatch; opening under the cursor; the QR hand-off.

**Dated:** no timestamp or source app on any row (20 anonymous text blobs are unsearchable by *when*); no match highlighting; rich clips
look identical to plain and editing flattens them silently; 308 lines of QML to render one line of text; a 20-item cap far below desktop
expectations; the actions menu is a separate, unstyled, self-closing window. The Plasma 5 screenshot below still shows the old flat grey
list with a pin icon that no longer exists.

## 5 things Clip MUST steal (WPF-implementable)

1. **Per-type row templates behind one selector**: `DataTemplateSelector` on clip kind → TextBlock template, thumbnail template, and a
   horizontal shell-thumbnail strip with filename caption + `+N` (Klipper's `DelegateChooser`, `ClipboardMenu.qml:414-444`).
2. **Whitespace-visible preview with a hard budget**: ≤3 lines, ≤100 chars, `␣ ⇥ ↵` in the accent colour (`TextItemDelegate.qml:20-49`)
   — kills "why are these two rows identical?" for free.
3. **Type-to-filter with honest empty states**: keystrokes on the list land in the search box; `Esc` clears the filter before it closes
   anything; `No matches` ≠ `Clipboard is empty`; every filter change reselects row 0.
4. **Star-backed All/Starred switcher**: tab appears only when a star exists; starred items are exempt from the cap and survive Clear
   History (asked once, "don't ask again") (`historymodel.cpp:305-467`; `ClipboardMenu.qml:329-375`).
5. **Hover toolbar + gradient fade + hover-moves-selection + HoverBlocker semantics**, plus a QR hand-off for text clips
   (`BarcodePage.qml`; WPF: QRCoder → `RenderTargetBitmap`).
## 3 anti-patterns to avoid

1. **Anonymous rows.** No time, no source app, no format cue → "which of these five near-identical clips is the 10-minutes-ago one?" Clip
   already stores the data; relative time + app icon is one line per row.
2. **Select ≠ paste.** A click only re-copies and closes, leaving the user to press Ctrl+V (`ClipboardMenu.qml:56`). On Windows Clip can
   paste on Enter/click — but keep Klipper's discipline of never stealing focus just to preview a selection.
3. **Silent destructive edit.** Editing rewrites a clip as `text/plain` and throws every other format away (`historymodel.cpp:559-601`);
   over an HTML clip, warn once or preserve formats.

## Screenshots

- Popup with search + selection (Plasma 5 era, still representative of the list): <https://userbase.kde.org/images.userbase/1/1f/Klipper2.png>
- Actions configuration (the regex/MIME action concept): <https://userbase.kde.org/images.userbase/6/67/Klipper-actions.png>
- Settings pages: <https://userbase.kde.org/images.userbase/a/aa/Klipper-conf.png> · doc page: <https://userbase.kde.org/Klipper>

## Not verified

No Plasma 6 screenshot of the QML popup exists on userbase or docs.kde.org (Plasma 5-era only; `docs.kde.org/.../klipper` 404s).
Sections 1/3/4 describe `master` source; the screenshots only illustrate the older plainer list. Klipper was not installed or run, per brief.
