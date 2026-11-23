# Maccy (macOS) — UI/UX audit for Clip

**Identity.** Maccy by Alex Rodionov (p0deje). Repo **github.com/p0deje/Maccy**, MIT, Swift/SwiftUI-on-AppKit, macOS 14+ (`README.md`), free (MIT) / $9.99 on the Mac App Store (id 1527619437). Status-bar popover clipboard manager, ~200-item history, 40+ languages. Famous for "no fluff" minimalism. Read, not run: full `master` source (commit `e24f997`) + App Store screenshots. All paths below are under `Maccy/` in that repo.

## 1. List anatomy — one 22px line, zero decoration

The row is a single `HStack` with **no separators between rows** (`Views/ListItemView.swift:57-130`). Rows are stacked with `LazyVStack(spacing: 0)` (`Views/HistoryListView.swift:103`, `Views/PinsView.swift`).

| Part | Value | Source |
|---|---|---|
| Row height | **22 pt** (24 on macOS 26); `minHeight`, not fixed — an image row grows | `Observables/Popup.swift:34-38`, `ListItemView.swift:121` |
| Row content | clip preview text **only** (1 line), + optional 15×15 app icon, optional thumbnail / colour swatch, right-aligned shortcut glyphs | `ListItemView.swift:57-119` |
| Title font | SwiftUI default `.body` = **13 pt SF Pro**, regular; **no** secondary line, no timestamp, no app name, no badge | `ListItemTitleView.swift` (no `.font`), `ListItemView.swift:87-89` |
| Truncation | `lineLimit(1)` + **`.truncationMode(.middle)`** — head *and* tail of a long clip stay visible | `ListItemTitleView.swift:11-12` |
| Padding | list inset 5 pt (`Popup.horizontalPadding`); icon leading 4; text trailing 5; shortcut stack trailing **10** | `ContentView.swift:40`, `ListItemView.swift:64,75,88,119` |
| Whitespace | newlines/tabs/edge-spaces rendered as `⏎ ⇥ ·` when `showSpecialSymbols` (default **on**) — a 3-line clip still reads as one honest line | `Models/HistoryItem.swift:126-138`, `Defaults.Keys+Names.swift:69` |
| Selection | `Color.accentColor.opacity(0.8)` fill, radius **4** (7 on macOS 26), text forced **white**, no border/glow | `ListItemView.swift:124-130`, `Popup.swift:26-32` |
| Connected selection | adjacent selected rows use a rectangle with **two corners squared** so a run of rows reads as one pill | `ListItemView.swift:10-33` + `HistoryItemView.swift:11-24` |
| Shortcut chips | `⌘1…⌘9` on the first 9 visible rows, pinned rows get a letter; glyphs at `opacity 0.7`, no keycap boxes | `Observables/History.swift:587`, `Views/KeyboardShortcutView.swift:18-27` |
| Multi-select badge | only while multi-selecting: index+1 in a `.caption` white-text capsule over `secondary 0.5/0.8` | `ListItemView.swift:93-104` |
| Dividers | **only between groups** (header/list, pins/history, list/footer), 1 px, inset 6 pt horizontally and 6 pt vertically | `HistoryListView.swift:48-66`, `FooterView.swift:23-25` |

Row order for a text clip: `[text] … [⌘n]`. For an image clip: `[thumb] [⌘n]`. For a hex colour: a small colour swatch instead of a thumb (`HistoryItemView.swift:28-50`).

## 2. Rich text — flattened for display, preserved for paste

There is **no rich rendering in the list and none in the preview**. Both read a flattened string:

- `HistoryItem.previewableText` returns, in order: file URLs (percent-decoded, joined by `\n`), plain `text`, `rtf.string`, `html.string`, else title (`Models/HistoryItem.swift:143-157`). Title = that, `shortened(to: 1000)` + scalars unsafe for layout stripped (`HistoryItem.swift:122-124`).
- The row's `AttributedString` path is **only for search highlighting** — bold by default, or italic/underline/`findHighlightColor` (`HistoryItemDecorator.swift:202-227`, default `.bold` in `Defaults:33`). A clip's own bold/serif/colour never reaches the list.
- The preview pane prints `Text(item.previewText.string).font(.body)` (`PreviewItemView.swift:56-66`) — also plain.
- Formatting is *stored* and *restored*: RTF/HTML are kept as separate pasteboard payloads and written back on paste; "remove formatting" is an explicit modifier, not the default (`Clipboard.swift:283-290`, `History.swift:349-372`).
- The editor edits **plain text only**, and warns: "Rich text will lose formatting when edited" (`Views/ItemEditorView.swift:53-62`).

**So what does the user see for an HTML/RTF clip?** The text, in system sans, one line in the list, block of text in the preview. Formatting is invisible until pasted. That is the deliberate trade: every row is the same 13 pt system line, so the list never looks ragged.

## 3. Images

| Thing | Value | Source |
|---|---|---|
| Row thumbnail | NSImage resized to **340 × `imageMaxHeight`** (default 40) *preserving aspect* → a portrait image is 40 pt tall, a wide one ≤340 wide; rendered inline before the title with 5 pt vertical padding, no border, no radius, no frame | `HistoryItemDecorator.swift:16`, `Extensions/NSImage+Resized.swift`, `ListItemView.swift:79-85` |
| Preview image | resized to the screen's visible-frame size, drawn `.fit`, `clipShape(.rect(cornerRadius: 5))` — letterboxed, never cropped | `HistoryItemDecorator.swift:15`, `PreviewItemView.swift:11-15` |
| While loading | grey `0.3` placeholder at the preview's ideal size + spinner (`ProgressView`) | `PreviewItemView.swift:42-54` |
| Broken image | same grey block + `photo.badge.exclamationmark` (multicolour) | `PreviewItemView.swift:28-40` |
| Generation | lazily on row appear (`ensureThumbnailImage`), async task, cancelled and recached on cleanup | `HistoryItemView.swift:63`, `HistoryItemDecorator.swift:127-178` |
| Extras | image **OCR'd with Vision** on copy; the recognised text becomes the row title, making screenshots searchable | `HistoryItem.swift:105-119, 252-291` |

## 4. Search / UX model

- **Search field is the header, always focused** (`ContentView.swift:41-43`), 23 pt tall, radius = row radius, fill `secondary @ 0.10`, 11 pt magnifier, 11 pt clear button (`SearchFieldView.swift:11-40`). A dim "Maccy" wordmark sits left of it (`ListHeaderView.swift:16-20`); the whole header can collapse to 0 height (`HeaderView.swift:39-48`).
- Enter = copy & close; ⌥Enter = paste; ⌥⇧Enter = paste without formatting; ⌘n shortcut = pick nth. Search auto-clears when the popup loses focus (`ListHeaderView.swift:25-29`).
- **Keyboard is the product**: ↑↓/⌘↑⌘↓/Home/End, ⌘-click & shift-click multi-select, ⌥P pin, ⌥⌘⌫ delete, ⌥⌘⌫⌘ clear, ⌘, prefs, Esc close, ⌥(default) toggle preview — all in one dispatch switch (`Views/KeyHandlingView.swift:30-190`) plus a "cycle mode": hold the hotkey and press it repeatedly to walk rows, release modifiers to accept (`Popup.swift:149-196`).
- Search modes: exact (default) → regex → fuzzy (Fuse, threshold 0.7), with a `mixed` order exact→regex→fuzzy (`Search.swift:36-132`). Matches are bolded in-place, never highlighted by recolouring the row.
- **Pinning** = a keyboard letter, not a folder: pinning moves the item into a pins block (top or bottom, user choice), gives it a permanent random `⌘letter` that survives restarts, and it stays in the list — no separate tab (`Models/HistoryItem.swift:10-62`, `History.swift:229-243`). Pins are reorderable by drag.
- Popup opens **at the mouse cursor** by default (options: cursor / menu-bar icon / window centre / screen centre / last position), clamped to the screen (`PopupPosition.swift:52-56`).

## 5. Visual craft — what makes it look expensive

- **Window**: 450×800 default (content-driven height, clamped to 3 rows minimum and the remembered max), frameless `NSPanel`, clear background, corner radius = `cornerRadius + horizontalPadding` = **9 pt** (12 on macOS 26) with `.continuous` curve, `masksToBounds` (`FloatingPanel.swift:66-75`, `Popup.swift:47-50`, `Defaults:74`).
- **Material**: `NSVisualEffectView(material: .popover, blendingMode: .behindWindow)`; on macOS 26 an `NSGlassEffectView(style: .regular)` (`Views/VisualEffectView.swift:3-30`, `ContentView.swift:12-17`). No opaque panel colour anywhere → it reads as part of the desktop.
- **Spacing rhythm** is 5/6/: outer padding 5, section padding 6, divider inset 6, icon gap 5, title-trailing 5, shortcut-trailing 10. Nothing else is a magic number (`Popup.swift:20-24`).
- **Three fixed bands** — header (search + preview toggle), scrolling list, footer of shortcut rows (Clear / Preferences… / About / Quit) — with the list carrying all the animation (`ContentView.swift:20-52`, `FooterView.swift`).
- **Motion**: list changes `.default.speed(3)` ≈ 0.1 s; preview open/close 0.25 s ease-in-out animating the *window frame* so the popup stays anchored and only grows sideways/upward (`SlideoutController.swift:162-200`).
- **Preview pane** = attached slideout, 400 pt default / 200 min, drag divider with resize cursor, placement flips left when it would go off-screen, auto-opens after 1500 ms dwell and stays suppressed once dismissed by hand (`SlideoutController.swift:68-100, 260-300`, `Defaults:61,78`). Contents: content, divider, then 12 pt metadata rows — Application, Dimensions, Size, First copy, Last copy, Number of copies (`PreviewItemView.swift:69-114`).
- Contrast is quiet: no shadows inside, no card borders, one accent fill; everything else is `.primary`/`.secondary`.

## 5 things Clip MUST steal (WPF-implementable)

1. **Never render formatted content in a row — render it only in the preview.** Copy the flatten rule: row text = `text → rtf.string → html.string → file URLs`. Clip already ships RtfPipe and `HtmlFragment`; make Core expose `RowText`/`PreviewText` and have the list bind one `TextBlock`. WPF has no middle ellipsis, so pre-trim in the converter: keep head 60 % + `…` + tail 40 % of the allowed characters. Rich render (HtmlRenderer.WPF / FlowDocument) appears in **one** place only: the preview pane.
2. **Fix the row at 24 px, one line, no separators, 4 px radius fill at accent 80 % with white text, list inset 5 px.** ListBox item template: `Height=24`, `Margin=0`, no border, `CornerRadius=4`, `Padding="4,0,10,0"`, selected background = accent at 0.8, one vertical-centred `TextBlock`. Add Maccy's connected-corner trick with a converter that squares the top radius when the previous row is selected. Skip per-row GridLines, row numbers, timestamps, app names.
3. **Print the keyboard model inside the row**: `Ctrl+1…Ctrl+9` right-aligned at 0.7 opacity on the first 9 visible rows (and a letter chip on pinned rows), assigned in Core, handled in MainWindow. This is the cheapest possible "keyboard-first" signal — no legend, no help text.
4. **A preview pane with a dwell timer, not a tooltip**: right-hand `Grid` column (+1 px `GridSplitter`), 400 px default / 200 px min, width persisted in Config; `DispatcherTimer` 1500 ms armed on selection change → open, cancelled on keystroke close, and after a manual close do not reopen until the next explicit toggle. Inside: image letterboxed (`Stretch=Uniform`, radius 5) or scrollable plain text, a 1 px divider, then 12 px metadata rows (source app, dimensions, bytes, first/last copy, copy count), toolbar above (pin / edit / delete).
5. **Window chrome as one radius, two bands and a material**: `WindowStyle=None` + `AllowsTransparency` + acrylic/DWM blur (not a flat colour), single `CornerRadius=9` clip, `SizeToContent=Height` so height = content clamped to [3 rows, remembered max], no title bar, no per-row borders, dividers only between groups with 6 px inset, and open at the cursor clamped to the monitor work area.

## 3 anti-patterns

1. **No empty state at all.** Zero search results render nothing between header and footer — the whole project contains no "no results" string or view (`HistoryListView.swift` has no empty branch). The translucent blank panel reads as broken. Clip should show one quiet centred line: `No matches for “x”`.
2. **Row rhythm is user-tunable.** Text rows are 22 px, image rows up to 40 px by default and **210 px** if the user drags the `imageMaxHeight` stepper (`Defaults:50`, `AppearanceSettingsPane.swift:33-37,101-110`) — the list loses its beat and neighbouring rows stop aligning. Clip: keep a fixed 24 px row with a 20×14 letterboxed thumb; the large image belongs to the preview.
3. **Auto-opening a 400 px pane that resizes the window under the pointer** (`SlideoutController.swift:73-92,162-200`) is jumpy while merely arrowing through history, and the metadata exists *only* there, so a user who never opens it never learns copy counts or dates. Clip: open on explicit action (or into a fixed-width window that does not move), and surface "copied 3× · 2 h ago" somewhere always-visible. *Aside:* the accent fill hardcodes `Color.white` for selected text (`ListItemView.swift:124`) — Clip should pick black/white from the accent's luminance instead.

## Screenshots

- https://maccy.app — hero video `https://maccy.app/img/maccy/Demo.mp4`, `https://maccy.app/img/maccy/Instructions.png`
- App Store (800×500 frames, readable at 1600×1000): [main](https://is1-ssl.mzstatic.com/image/thumb/PurpleSource211/v4/59/12/d8/5912d8f7-936d-992a-fbd3-c2ac51ab4984/ed8f76c6-46a1-46b2-b7ac-ff420a8f8c26_01-Main.png/1600x1000bb.png) · [types/images/files](https://is1-ssl.mzstatic.com/image/thumb/PurpleSource211/v4/f5/b1/46/f5b14653-90fa-1467-8c9e-aace4f98df1d/cb67dc6d-b92a-43f9-93dc-f9739691d437_02-Types.png/1600x1000bb.png) · [search](https://is1-ssl.mzstatic.com/image/thumb/PurpleSource221/v4/b1/47/96/b147962c-a405-9415-7553-7dd9182a3a6f/03-Search.png/1600x1000bb.png) · [pin](https://is1-ssl.mzstatic.com/image/thumb/PurpleSource211/v4/9e/6c/b0/9e6cb021-2c96-8056-20fb-33a8627d2398/98b974f1-9bd2-458d-8884-e7ed2fe30164_04-Pin.png/1600x1000bb.png) · [shortcuts](https://is1-ssl.mzstatic.com/image/thumb/PurpleSource221/v4/04/16/51/041651bf-999a-1f42-31db-990a76e3314d/2d324059-3804-4628-88c0-6a3a1bc849c3_05-Shortcuts.png/1600x1000bb.png)
- What the shots confirm: selected row is a solid accent pill with white text and middle-ellipsised title ("…conse…m id est laborum."), `⌘1…⌘6` chips at right, footer rows with their own glyphs, image clip as a ~40 px inline thumb, file clip as `file:///Applications/Maccy.app/` — and the metadata block with "Press ⌥P to (un)pin." hints (older build drew the preview as a separate tooltip card; current builds attach it as a slideout).
