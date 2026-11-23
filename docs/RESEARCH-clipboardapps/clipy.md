# Clipy (macOS) — UI/UX read from source

**Identity** · Clipy Project, MIT (`LICENSE`), Swift/AppKit + SwiftUI. Repo `Clipy/Clipy`, HEAD `74ab9d7` (2026‑11‑23), latest release **1.4.2** (2026‑11‑15). Requires macOS 14+ (`README.md:16`); free, local‑only, OpenCollective‑funded, 8 settings panes (`Clipy/Sources/Settings/SettingsPane.swift:6-14`).

**The one structural fact:** Clipy has **no history window**. History is an `NSMenu` invoked by a global hotkey, rebuilt by `NSMenuBuilder` (`Menus/NSMenuBuilder.swift:46-95`) or by its own SwiftUI re‑implementation of a native menu, `Packages/AlternativeMenu` (877 lines of style maths). Everything below is "menu‑row UI", not "list UI". Paths below are relative to the repo root; `AM/` = `Packages/AlternativeMenu/Sources/AlternativeMenu/Internal/`.

**Screenshots** · https://clipy-app.com/static/clipboard-history.png (official, README `:20`) · https://github.com/Clipy/Clipy/releases/tag/1.4.0 (Settings redesign) · https://clipy-app.com

---

## 1. List anatomy

- **Row height:** `minimumRowHeight: 24`, `selectionCornerRadius: 7`, panel `cornerRadius: 12`, panel padding `5` (`AM/AlternativeMenuStyle.swift:17-19,75`; pre‑macOS‑26 = 22 pt rows, same file `:50-58`). Native macOS menu font, **13 pt** `NSFont.menuFont` (`:121`); section headers 11 pt semibold in tertiary label colour (`:125`, `AM/AlternativeMenuRow.swift:26-30`). All numbers are macOS text‑size relative, not fixed pixel rows.
- **Row formula:** `ceil(max(24, max(imageH, textH) + 6))` — content padding 6, image spacing 6 (`AM/AlternativeMenuRowData.swift:115-122`, `AlternativeMenuStyle.swift:87,93`). So a 32 px image row grows to 38; text rows stay 24.
- **Item anatomy (left→right):** type glyph (SF Symbol) in a *shared* icon column → `N. ` monospaced number → single‑line title, tail‑truncated → right column: submenu chevron **or** key equivalent in tertiary colour. No timestamp, no source‑app, no second line, no badge.
- **Numbers:** `MonospacedDigitFormatter.numberedTitle` builds `"12" + ". " + title` with `kMonospacedNumbersSelector` so digits line up (`Extensions/MonospacedDigitFormatter.swift:13-44`); opt‑out setting `marksMenuItemsWithNumbers` default **true** (`AppStorage/AppStorageValues.swift:70-72`).
- **Selection:** native‑menu two‑tone blend — `menuSelectionMaterial` visual‑effect view with the row's own radius 7, text switches to `selectedMenuItemTextColor` (`AM/AlternativeMenuRoot.swift:60-65`, `AM/AlternativeMenuRow.swift:20-26`). No accent‑coloured full‑bleed bar, no left rail, no checkmark.
- **Categories/tabs:** none for history. "History" is a section header; beyond N inline items it collapses into numeric range folders `1 - 10`, `11 - 20`… (`NSMenuBuilder.swift:152-179`, default inline limit **0**, folder size **10**: `AppStorageValues.swift:152-158`) — visible in the screenshot as a single `1 - 6` folder.
- **Search:** optional 24 pt capsule field in a 36 pt header (`Menus/AlternaiveSearchMenu.swift:126-149`, `Menus/Components/AlternativeMenuSearchBar.swift:20-90`), `showsSearchBar` default **false** (`AppStorageValues.swift:66-68`). Without it, plain typing type‑selects over the *visible* rows via CoreServices `UCTypeSelect` (`AM/AlternativeMenuTypingSelection.swift:30-66`). With it, SQLite trigram FTS over title **and OCR text**, debounced 200 ms, capped at **15** results (`AlternaiveSearchMenu.swift:86-112`, `Repositories/PasteboardHistoryRepository.swift:96-110`).

## 2. Rich text

- The list **never renders formatting**. Title = first line of the plain‑string representation, whitespace‑trimmed, hard‑capped at **20 chars** + `…` (`Extensions/StringExtensions.swift:19-50`, `AppStorageValues.swift:148-150`); newline cut at the first line (`StringExtensions.swift:43-44`).
- RTF/RTFD/HTML clips get only a glyph: `text.alignleft`, `</>` for HTML, `doc.text` for PDF, `doc` for files, `link` for URL (`Models/PasteboardAvailableType.swift:72-87`). Stored title is `content.stringValue` prefixed at 10 000 chars (`Repositories/PasteboardHistoryRepository.swift:167`) — i.e. the rich data is kept for pasting only (`Models/PasteboardContent.swift:98-113`). RTF‑only clips without a plain‑string flavour get an empty title (no fallback for rtf/html: `Extensions/PasteboardHistoryExtensions.swift:37-49`).
- **Per‑item detail view: none.** The whole "detail" is a hover tooltip, first **200** chars, toggleable (`PasteboardHistoryExtensions.swift:27-34`, `AppStorageValues.swift:160-162`). HTML titles used to carry an `[HTML]` prefix that was removed in 1.4.1 (https://github.com/Clipy/Clipy/releases/tag/1.4.1).

## 3. Images

- Inline mini‑thumbnail **100 × 32** default (user‑settable: `AppStorageValues.swift:164-170`), aspect‑fit, 2 pt corner clip, 1 pt `black@0.15` hairline (`Models/PasteboardContent.swift:70-91`), drawn in the same icon column (`NSMenuBuilder.swift:202-213`, toggle `showsImagesInMenu` default true).
- Thumbnails are persisted bytes in their own table, not re‑decoded from the blob (`Database/SQLiteDataSchema.swift:45`, `Database/DatabaseMigration.swift:48-86`).
- **OCR:** images get Vision text recognition (`.accurate`) capped 10 000 chars and are searchable by that text (`Sources/OCR/TextRecognizer.swift:30-41`, repository FTS above). It never shows in the row — searchable, invisible.
- Colour codes (`#3999D8`, CSS names) show a **20 pt** swatch rendered as a circle with a 1 px 15 % black stroke, drawn at **18 pt** in the menu (`PasteboardContent.swift:50-69`, `NSMenuBuilder.swift:207-209`); the screenshot shows it sitting exactly in the icon column.
- No full image preview, no quick‑look panel, no size/dimension label.

## 4. Editor

- **Clips cannot be edited.** Only snippets: a separate window `CPYSnippetsEditorWindowController` — `NSOutlineView` (folders → snippets, drag‑and‑drop reorder, enable toggle) + right detail pane with folder title, separate per‑folder shortcuts, and a plain `NSTextView` at **14 pt system**, `isRichText = false`, placeholder "Please fill in the contents of the snippet" (`Snippets/CPYSnippetsEditorWindowController.swift:32-50`; cell 14 pt, 16 × 13 folder glyph: `Views/TableViewCell/CPYSnippetsEditorCell.swift:33-96`).
- That window **hard‑codes light mode**: `window?.appearance = NSAppearance(named: .aqua)` and `backgroundColor = NSColor(white: 0.99)` with the comment "Temporarily disable Dark Mode until this window is migrated to SwiftUI" (`CPYSnippetsEditorWindowController.swift:67-70`).
- Snippets import/export as XML (`:20-26`), split‑view min/max coordinates constrained (`:258-265`).

## 5. Visual craft & UX

Well‑crafted:
- Pixel‑faithful native‑menu reimplementation: 130 lines of measured constants (padding, icon column, key‑equivalent column, RTL offsets) instead of eyeballing (`AM/AlternativeMenuStyle.swift`), plus a shared icon‑column width computed from the widest symbol (`Menus/AlternativeMenuBuilder.swift:28,77-89`).
- Key‑equivalent column: numbers 1…0 for the first 10 rows (`NSMenuBuilder.swift:15,197-200`), so "hotkey → digit" pastes without arrows; also ⌘-click plain text, ⌘⌥-click paste‑and‑delete, ⌥-click delete (`Services/PasteService.swift:39-104`, all remappable).
- Search latency detail: spinner only appears after 200 ms so fast queries don't flash (`AlternaiveSearchMenu.swift:88-93`); first row pre‑highlighted so `⏎` pastes immediately (`AppStorageValues.swift:62-64`).
- Reorder‑on‑paste by default, `dateCreated` alternative (`AppStorageValues.swift:30-31`, `Settings/GeneralSettingsView.swift:45-53`), history cap 1…1000 default **30** (`AppStorageValues.swift:136-137`, `GeneralSettingsView.swift:38-42`).

Dated / weak:
- The core surface is a **menu**: 24 pt single‑line rows, 20‑character titles, no metadata, no preview, no pin, no categories, no per‑item actions beyond modifier‑clicks. It reads as 2015 shareware that got a 2026 coat of paint.
- Overflow folders (`1 - 10`, `11 - 20`) are a menu‑length workaround, not a browsing model; anything past the first folder is two hops away.
- Global hotkeys are the only entry point; there is no "open the whole history" surface with a stable scroll position.

---

## 5 things Clip MUST steal (WPF‑implementable)

1. **Shared icon column + monospaced index.** Reserve one fixed column for the type glyph (image/colour swatch sized to the same box) and render the row index with tabular figures, so every title starts on the same x. In WPF: `Grid` with a fixed `ColumnDefinition` + `Typography.NumeralAlignment="Tabular"`; one `#D9D9D9`/theme‑token divider between icon and text.
2. **Type‑to‑filter that needs no visible search box, plus a search field that appears on demand.** Typing should immediately filter (list stays usable with zero chrome); show the field only after the first keystroke or on Ctrl+F. Clip's always‑on search bar costs a row of height that buys nothing until used. Add the 200 ms "no spinner for fast queries" rule.
3. **Modifier‑click action verbs on rows, shown in the row's trailing column on hover.** ⌘ = paste as plain text, ⌘⇧ = paste and delete, ⌫/⌥ = delete. Show the verb as a small trailing hint when the pointer is over the row: the row teaches its own shortcuts and the owner never needs a context menu.
4. **Number‑key fast path.** First 10 visible rows get 1…0 as key equivalents, displayed in a right‑aligned tertiary column; press the digit to paste that row. Cheapest possible keyboard model and it scales to every tab.
5. **OCR‑index images and keep the thumbnail dumb.** Store a 100×32‑ish thumbnail beside the blob, draw it with 2 pt radius + 1 px 15 % hairline in the icon column, and index its recognised text so image clips are findable by their content. Do NOT try to render a real preview inline — the tiny framed thumb is what makes Clipy's image rows look deliberate.

## 3 anti‑patterns (do not copy)

1. **Overflow‑folder pagination** (`1 - 10`, `11 - 20`) instead of a scroller — an artefact of menus that can't scroll well. Clip has a window; scroll it.
2. **Tooltip‑as‑detail‑view.** 200 chars on hover is the only way to see what a clip actually contains. Every Clipy bug report about "which of these two similar clips?" comes from this. Clip needs a real preview/detail pane or at least a 2–3 line clamped preview.
3. **Forcing light mode to dodge a redesign** (`appearance = .aqua` + hard‑coded near‑white background, `CPYSnippetsEditorWindowController.swift:67-70`). A themeable Windows app must never hard‑code a surface; it is exactly the "half‑migrated window looks broken" smell Clip's audit already flags.

---

*Not checked:* no build/run (macOS‑only), so no motion timings, no real hover feel, no accessibility‑tree behaviour observed; row metrics are read from source, not measured on screen.
