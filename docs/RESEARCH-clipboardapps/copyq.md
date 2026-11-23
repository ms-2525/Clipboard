# CopyQ — how it ships (research, techniques only, no code copied)

**Identity:** CopyQ by Lukas Holecek (hluk) · GPL-3.0-or-later · Qt 6 / C++ · Linux, Windows, macOS 13+ · v17.0.0
(`README.md:27`, `CHANGES.md:1` — read from `github.com/hluk/CopyQ`, tag master, Nov 2026). GPL: **behaviour and numbers only**, never code/asset reuse.
Not installed or run: all findings are from source read in a local clone plus the project's own screenshots.

---

## 1. Rich text in the MAIN list — yes, every row renders formatted

The list row **is** the clip, rendered as HTML — not a snippet, not a per-item preview:
- `plugins/itemtext/itemtext.cpp:48-56` picks `text/html` when present; `:103-110` sets it on a `QTextDocument` (`setHtml`) rendered inside a read-only `QTextEdit` **per row**; if the HTML renders empty it silently falls back to plain text (`:106`).
- Caps that make it survivable: 100 KB of text (`:25`), 1024 chars per line (`:28`), 4096 lines (`:32`); default display cap 1000 lines / 2000 px height (`plugins/itemtext/itemtextsettings.ui`), user-configurable with `0 = show all`.
- Truncation is visible: the elided tail is replaced by a rounded "… " pill drawn as inline HTML (`:64-70`, `:112-136`).
- Rich text can be switched off per install ("Save and display HTML and rich text", default on: `itemtext.cpp:278`).
- Rows are real widgets, not painted pixmaps: `src/item/itemdelegate.cpp:133-149` creates an item widget per row; `:562-599` destroys widgets more than one screen away, keeping only the current row. That is how 1000s of HTML rows stay cheap.

**What the user sees:** HTML copied from a browser (e.g. a page heading) appears *as that heading* in the row — bold/links/colour/headings survive; long content is cut with a "… …" chip. Screenshot: `docs/images/copyq-show.png` (row 0 = big bold heading, row 4 = large bold title, row 2 = wrapped paragraph).

## 2. Editor — F2 edits rich text **in place**, in the list

- `docs/keyboard.rst`: F2 = edit selected items, Ctrl+E = external editor, F5 = action dialog.
- F2 opens `ItemEditorWidget` (a `QTextEdit`) glued **over the row**, with a toolbar above it — no dialog: `itemdelegate.cpp:252-318` builds `editorParent` with `QVBoxLayout{toolBar, editor}` inside the view; `clipboardbrowser.cpp:335-384` hides scrollbars/alt rows while editing.
- Format chosen automatically: text/plain → text/uri-list → text/html (`itemdelegate.cpp:258`); non-text formats refused (`:266`); HTML is used when present so the user sees formatting while editing (`:279-283`).
- Toolbar (`itemeditorwidget.cpp:258-330`): Save · Cancel | Undo · Redo | Font, **Bold, Italic, Underline, Strikethrough** | Foreground, Background | **Erase style** | Search.
- Saving: plain text is always written; HTML is written **only if the document actually carries formatting** (`>3 distinct formats`, `itemeditorwidget.cpp:28-31`, `:84-95`). Enter / Ctrl+Enter saves (`:119-137`).
- Notes are edited through the same in-place editor on the `application/x-copyq-item-notes` format (`clipboardbrowser.cpp:1216-1226`), so a note is a separate field, not part of the clip.
- Ctrl+E shells out: temp file with the right suffix, editor command with `%1`, poll every 500 ms, write back on change (`src/item/itemeditor.cpp:82-122`, `:149-172`).

## 3. Images — thumb in the row, true size in a preview dock

- Row thumbnails are downscaled to **max 320×240** by default (configurable, `itemimage.cpp:236-242`), smooth-scaled (`:206-210`), 4 px margin (`:115`), DPI-aware (`:122-125`, `:201`). Animated GIFs play **only while that row is current** (`:128-148`) — no background animation cost.
- A right-side **preview dock** shows the current item rendered *unscaled* (`mainwindow.cpp:783-786`, `:1237-1243`; `src/ui/mainwindow.ui:51-83`): for images that is the full-size picture inside a scroll area, for HTML the full document with 16× the row caps (`itemtext.cpp:29-33, 243`). It is hidden by default and toggled from the menu; selection changes update it after a 100 ms debounce (`mainwindow.cpp:101`, `:1224-1269`).
- The config UI exposes plain "Maximum Image Width / Height" spin boxes (shown as 480×720 in `docs/images/image-editor.png`) — no zoom UI, no lightbox.

## 4. List anatomy

- **Row = content only.** No title line, no app name, no timestamp baked into the layout; optional extras are *widgets wrapped around* the row: tags as a chip strip above the text (`plugins/itemtags/itemtags.cpp:351-369`), notes as a strip above/beside with a 16 px coloured indicator bar (`plugins/itemnotes/itemnotes.cpp`, `theme.rst` "Plugin Indicators"), pinned = 6 pt border on the row's right edge.
- **Row number gutter**, 0-based by default, 2 px margin, drawn in the row-number font/colour (`itemdelegate.cpp:638-651`; `theme.cpp:526, 553-566`). Visible in every screenshot.
- **Heights:** variable, content-driven; 100 px placeholder until measured (`itemdelegate.h:23`); 2 px item margins and spacing = font line-spacing / 6 (~3-4 px) (`theme.cpp:532`, `:287-288`); max width 2048 px, wrapping optional (`appconfig.h:259-262`, default on).
- **Anti-jitter:** measure deltas smaller than 1/3 line (grow) or 1 line (shrink) are **ignored** so rows don't twitch while scrolling (`itemdelegate.cpp:242-247`).
- **Selection:** alternate row colours on (`clipboardbrowser.cpp:222`); selected row = full-bleed solid background + inverted text, with separate rules for hover / inactive window (`shared/themes/items.css:6-55`); the *current* (keyboard) row additionally gets a 0.1em outline (`theme.cpp:433-435`). Extended multi-select (`clipboardbrowser.cpp:218`).
- **Tab bar:** hidden while there is one tab, appears at two (`docs/tabs-and-items.rst`); selected tab `padding: 0.5em`, lighter bg, unselected darker (`theme.cpp:442-453`); optional per-tab item counter at 6 pt (`:459-465`); middle-click closes a tab (`tabbar.cpp:161-170`); drag an item onto a tab to move it (`:172-200`); optional **tab tree** groups `Job/Tasks/1` (`docs/tabs-and-items.rst`); Ctrl+1…9 / Ctrl+0 jump to tab / last tab (`docs/keyboard.rst`).
- **Search:** a full-width bar pinned above the list (`ui/mainwindow.ui:20-38`, zero margins/spacing in the central layout), live filtering, left magnifier, right clear button (`filterlineedit.cpp:370, 413-417`), matches highlighted inside the rendered text with yellow/black (`theme.cpp:395-396`, `itemdelegate.cpp:320-342`). Typing anywhere in the window goes into it; Esc clears then hides (`mainwindow.cpp:4169-4199`).
- Icon-only toolbar docked right (`mainwindow.cpp:781`, `:3204-3208`).

## 5. Visual craft

- The whole look is data: placeholder-driven CSS (`shared/themes/items.css`, `main_window.css`; `docs/theme.rst`), with colour expressions (`${sel_bg - #222}`, `${fg + #444}`) and per-state rules. Defaults live in `theme.cpp:411-527`.
- Separate fonts for items / editor / search hits / row numbers / notes / notifications (`theme.cpp:402-407`); search hits get their own font + colours.
- **Motion: none.** No `QPropertyAnimation`/`QVariantAnimation` anywhere in `src`; the only fade is window opacity on notifications (`notificationbasic.cpp:183, 314-321`).
- Craft level is utilitarian, not refined: 2 px margins, `border-radius: 2px`, 6 pt counters, 0.5em tab padding. It reads as a dense desktop tool.

## 6. Keyboard / UX model worth noting

- Type-to-search, Esc = cancel search or hide; F2 in-place edit; Ctrl+E external editor; Ctrl+Up/Down reorder; Delete; Ctrl+A.
- **Enter pastes into the app you came from and hides the window** — `activate_pastes`, `activate_closes`, `activate_focuses` all default `true` (`appconfig.h:277-290`); window also hides on losing focus after 500 ms (`:172-183`).
- **Ctrl+Left/Right switches which stored format is *displayed*** for the selected item, and format priority is user-orderable (config Items tab text, `docs/images/image-editor.png`). One clip, many representations, user picks the one shown.
- Tray menu shows the last 5 items, numbered, snippet-elided, images as thumbs (`appconfig.h:292-316`; `docs/images/copyq-menu.png`).
- Tabs carry accelerators via `&` in the name (Alt+C) (`docs/tabs-and-items.rst`).

---

## 5 things Clip MUST steal (all WPF-native)

1. **Row = the clip, variable height, formatted, capped.** Drop the fixed 18 px `Body` + `CharacterEllipsis` in `UI/MainWindow.xaml` (lines 305-309): host a `ContentControl` per row that shows a `FlowDocument` (HTML → `HtmlFragment` → `FlowDocumentScrollViewer`, read-only) sized to content with `MaxHeight≈240` and a "… " pill when cut; cap source at ~100 KB and per-paragraph at 1024 chars before parsing. `VirtualizingPanel.ScrollUnit="Pixel"`.
2. **A right-hand preview pane for the current row**: full-size image (no downscale) or the complete HTML, in a Grid column with a `GridSplitter`, toggled in Settings, refreshed with a ~100 ms debounce on selection change. Reuses the one `HtmlPanel` Clip already builds in `MainWindow.Rich.cs` — move it off the row and into the pane.
3. **Gutter numbers + current-row outline.** A ~24 px index column (1-based), full-bleed selection band, and a 1 px ring **only** for the keyboard-current row, so multi-select stays readable. Clip already has `Ring`/`Rail` layers — make the number the anchor instead of the icon tile.
4. **Measure hysteresis so rich rows never twitch:** ignore height deltas under ⅓ line growing / 1 line shrinking (`itemdelegate.cpp:242-247`), and dispose row visuals one screen away (today Clip keeps exactly one rich panel alive; recycling lets it keep ~10).
5. **In-place F2 editor over the row** (format toolbar: bold/italic/underline/strike/colour/erase, Save·Cancel) instead of the modal `EditorWindow`; save plain text always and HTML **only when formatting exists** (`itemeditorwidget.cpp:28-31`); Enter = paste into the previous app + hide, Esc = clear search, then hide.

## 3 anti-patterns to avoid

1. **Motion-free, hint-free UI.** CopyQ animates nothing and explains nothing; its power lives in docs and a command language. Clip's hover/pulse motion and visible labels are a feature, not decoration — don't strip them to look "native".
2. **Unbounded rich rendering.** Inline HTML for every row only works because of hard byte/line/char caps plus offscreen widget disposal. A `WebBrowser`/HtmlPanel per row without those caps will freeze at a few hundred clips.
3. **Configuration-as-UI, at 2 px and 6 pt.** Dozens of checkboxes across six config tabs, 6 pt tab counters, 0-based row numbers and 2 px margins are dense-desktop-tool choices a modern Windows app should not copy — and 0-based numbering costs the owner a mental translation.

## Verified screenshots (HTTP 200, fetched from the repo's own docs)

- https://raw.githubusercontent.com/hluk/CopyQ/master/docs/images/copyq-show.png — main list: row numbers, alt rows, blue selection, inline rich text, image row, right toolbar
- https://raw.githubusercontent.com/hluk/CopyQ/master/docs/images/tags.png — tab tree with icons + counts, in-row metadata/timestamp, tag text
- https://raw.githubusercontent.com/hluk/CopyQ/master/docs/images/image-editor.png — Items config: image size limits, editor commands, "Ctrl+Left/Right changes displayed format"
- https://raw.githubusercontent.com/hluk/CopyQ/master/docs/images/copyq-menu.png — numbered tray menu of recent items
- https://raw.githubusercontent.com/hluk/CopyQ/master/docs/images/pin-items-shortcuts.png — pinned item indicator + shortcut config
