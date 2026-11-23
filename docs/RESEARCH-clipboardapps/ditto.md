# Ditto — how the reference Windows clipboard manager ships

Identity: **Ditto 3.25.113.0** (repo HEAD `a80fd35`, 2026-09-30), Win32 **C++/MFC**, GPLv3, Windows only.
Read from source clone of https://github.com/sabrogden/Ditto (`src/`, paths below are `src/<file>`).
Licence rule applied: techniques only, no code copied.

## 1. Rich text in the MAIN list
- Rows are **plain text by default**. Custom-drawn list: try `DrawRtfText`, else `DrawText` (`QListCtrl.cpp:591-604`).
- RTF drawing exists but is **opt-in and off by default**: `DrawRtfText` gated by `m_bDrawRTF` (`QListCtrl.cpp:1120-1164`), `GetDrawRTF()` default `FALSE` (`Options.cpp:1632`). The option label admits the trade: *"Draw RTF Text in List ... (Could Increase Memory Usage an Display Speed)"* (`CP_Main.rc:796`).
- The row string is the **flattened clipboard text, truncated to 500 chars** (`Clip.cpp:678-712`, default `DescTextSize=500` in `Options.cpp:1323-1326`). If a clip has no text format, the row is the **format name**, e.g. "Rich Text Format" (`Clip.cpp:761`).
- Per-item rich preview is a **separate borderless popup window**, not a pane. `ShowFullDescription` (`QListCtrl.cpp:1522-1751`) builds a `CToolTipEx` filled with plain text, RTF, HTML, DIB/PNG and metadata, positioned under the selected row.
- `CToolTipEx::Show` picks **one** renderer: image > HTML > RTF > text (`ToolTipEx.cpp:257-313`). So a formatted clip: plain snippet in the row, full colour/RTF/HTML in the F3 window.
- Search matches are highlighted by **injecting colour markup into the plain string** and rasterising it with `DrawHTML` (`QListCtrl.cpp:593-603`).

## 2. Editor
- Opens a **tab per clip, max 10 tabs** (`EditWnd.cpp:236-250, 252-291`); each tab is a ruler rich-edit control (`DittoRulerRichEditCtrl`).
- Toolbar: New / Save / Save All / Close / Save+Close+clipboard, labelled with shortcuts (`EditWnd.cpp:64-82`): `Ctrl+N`, `Ctrl+S`, `Ctrl+Shift+S`, `Esc`, `Shift+Esc` (`EditWnd.cpp:433-473`).
- Loads **RTF if the clip has it, else Unicode text** (`DittoRulerRichEditCtrl.cpp:31-40`) — so it is genuinely rich, not plain-only.
- Save writes **both** back: RTF (trimmed of the trailing `\par`) and Unicode text, but only for formats enabled in the saved-types list (`DittoRulerRichEditCtrl.cpp:149-157, 202-271`).
- A checkbox **"Update clip description on save?"** refreshes the tab title/list text (`EditWnd.cpp:50-56, 199-210`); saving plays a small "fly to toolbar" animation (`EditWnd.cpp:212-220`). Unsaved-change prompt on close (`DittoRulerRichEditCtrl.cpp:273-303`).
- Saving does **not** re-apply the original HTML format — HTML clips come back as RTF + text.

## 3. Images
- Inline **thumbnail in the row**, height-fitted to the row and the description shifted right (`QListCtrl.cpp:1166-1195`); on by default (`Options.cpp:1546-1556`). Unloaded DIB reserves blank width so rows don't jump (`QListCtrl.cpp:1189-1192`).
- Larger view = the same F3 popup swapping in `CImageViewer`: scroll, zoom/gestures, "scale image to fit window" default on (`Options.cpp:2345-2351`, `ImageViewer.cpp:15-80`).
- Window auto-sizes to the bitmap and the footer shows **`W x H, N%`** (`ToolTipEx.cpp:182-189, 265`).
- Drag a row out = real OLE drag of the clip content; `Ctrl` while dragging = files only; Ditto hides itself 500 ms into the drag (`QPasteWnd.cpp:5608-5646`).

## 4. List anatomy
- Row height = **font line height × lines-per-row (default 2) + 4 px** (`QListCtrl.cpp:414-428`, `ROW_BOTTOM_BORDER 4` at :27, default 2 at `Options.cpp:893-895`). Text inset 3 px left, 1 px top/bottom (`QListCtrl.cpp:514-517`).
- **Quick-paste numbers**: first ten rows get an 11 px gutter with the digit at bottom-left, a vertical divider at 8 px, small grey font (`QListCtrl.cpp:535-542, 610-647`; colour `180,180,180` at `Theme.cpp:48`). Mapping = row index + configurable start (`QListCtrl.cpp:242-255`, default start 1 `Options.cpp:84`), optional `Ctrl+num` (`Options.cpp:1222-1229`), optional "number key also pastes" (`Options.cpp:1368`).
- **Meta shown**: none beyond the text. The `Main` table has no source-application column (`DatabaseUtilities.cpp`, `CREATE TABLE Main`); "Added / Last Used / Quick Paste / hotkey / sticky" appear **only in the preview footer** (`QListCtrl.cpp:1615-1677`), plus `Length: N | Words: N` when text (`ToolTipEx.cpp:238-242`).
- **Icons**: symbol flags packed as a prefix before `|` in the description (`QListCtrl.cpp:472-479`) then drawn in order: group folder, never-auto-delete, has-shortcut, in-group, sticky (`QListCtrl.cpp:544-589`), PNGs at 16/20/24/32 px in `res/`.
- **Selection**: whole-row custom fill, separate focused vs unfocused colours, zebra odd/even rows (`QListCtrl.cpp:483-511`); a 2 px green bar marks clips already pasted (`QListCtrl.cpp:519-528`).
- **No tab bar in the main window** — navigation is groups (tree, `Ctrl+G`), breadcrumb + back arrow when inside a group (`QPasteWnd.cpp:677-691`), and 3 copy buffers (`ctrl-+`, `ctrl-*`) instead of tabs (`wiki/Copy-Buffers.md`).

## 5. Visual craft
- Type: **Segoe UI** everywhere, 13 px body (`EditWnd.cpp:49`), 15 px search box (`QPasteWnd.cpp:2214`), 11 px footer meta (`ToolTipEx.cpp:136`), 5 px quick-paste digits (`Options.cpp:85`); user-selectable list font, stored unscaled and re-scaled per monitor (`OptionsGeneral.cpp:587-608`).
- Colour: **theme = one XML file, ~30 named tokens** — caption gradient L/R (plus top-most / not-connected variants), zebra, selection, pasted, search highlight, group tree, description window, 3 scrollbar tokens (`Theme.h:14-107`; defaults `Theme.cpp:25-66`).
- Window is frameless with a **custom 25 px gradient caption strip** that can sit left/right/top (`DittoWindow.cpp:9, 72-83`), plus resize borders — this is what makes it feel like a utility popup, not a document window.
- **Overlay scrollbar**: 12 px, hover-grows, thumb `140,140,140`, auto-hides after 800 ms (`ModernScrollBar.cpp:20-22, 384`).
- Everything DPI-scaled at draw time; window size/position remembered **per screen resolution/DPI** (`Options.cpp:2366-2381`) — fights the classic "tiny on 4K" problem.

## 6. UX model
- `Ctrl+\`` opens; typing filters immediately; `Enter`/double-click pastes; `Shift+Enter` pastes plain text; `Ctrl+Space` toggles always-on-top; `F3` opens the preview; `Ctrl+G` group tree; `Backspace` up a group; `F7` new group; `n`/`p` move the preview; `Alt+Enter` properties (wiki `Home.md` key table).
- **Search is at the BOTTOM** of the window, 25 px tall, left of a system menu button (`QPasteWnd.cpp:769-773`), with three cycling modes: contains → wildcard → regex (`QPasteWnd.cpp:3644-3660`); "no results" replaces the list with a message (`QPasteWnd.cpp:727-736`). No field operators (`app:`, `type:`) exist.
- **Special Paste** submenu transforms at paste time: UPPER/lower/Capitalize/Sentence/Invert, Trim, Remove Line Feeds, +1/+2 line feeds, +current time, CamelCase, Slugify, Typoglycemia, POSIX paths, new GUID, multi-image tiling (`CP_Main.rc:483-509`).
- Preview window keys/actions: persistent pin, word-wrap toggle, "size to content", "remember position", "scale images to fit", and its own search (`ToolTipEx.cpp:765-805, 1287-1360`).
- Dropping onto Ditto's list does nothing (`QPasteWnd.cpp:5660-5663`) — it is an export-only drag source.

## 5 things Clip MUST steal
1. **F3 preview popup** — borderless, resizable, remembered per monitor, follows the selection (`n`/`p`), footer line "Added · Last used · N chars · N words" (or `W×H, 100%` for images), pin toggle. In WPF: a `Window` with `WindowStyle=None`, `RichTextBox` for RTF, `Viewbox`/zoomed `Image`, no chrome. This single feature closes most of Clip's "can't see what I copied" gap.
2. **Numbered first-ten gutter + number-to-paste** — fixed-width gutter so digits never shift text; digits `1..0`, configurable start, optional `Ctrl+num`, optional "act on number key". Pure ViewModel + `KeyBinding`s in WPF.
3. **Two-line fixed-height rows with inline image thumbnail** — image scaled to row height on the left, snippet beside, never variable row heights (virtualisation stays cheap), zebra rows, distinct focused/unfocused selection brushes.
4. **Row micro-status strip** — pin/sticky, group, has-hotkey, never-auto-delete, "already pasted" marker, drawn at 16/20/24/32 px per DPI. Turns a text list into a scannable table.
5. **Bottom search bar with three modes + in-row match highlight** — contains / wildcard / regex cycle, highlight matches by composing `Run`s (Clip has `FlowDocument`-free TextBlocks, so build Inlines instead of Ditto's markup-injection hack), and let the same search highlight inside the preview.

## 3 anti-patterns
1. **Description is a flattened, 500-char string in the DB.** A screenshot-heavy HTML clip or a table becomes one long line, and an RTF-only clip becomes the literal row text "Rich Text Format" (`Clip.cpp:761`). Never let storage lose the type's identity, and never let two clips render identically.
2. **Truth is off by default for performance.** Rich rendering in the list is disabled by default, and when enabled it pipes every visible RTF row through a hidden `CRichEditCtrl` to "clean" it, per paint (`QListCtrl.cpp:1141-1160`). The fix is caching, not lying about content — render once into a cached visual.
3. **The preview is a tooltip.** `CToolTipEx` is a `WS_POPUP` with `CS_SAVEBITS`, hover timers, "mouse click hides description" and auto-hide delays (`ToolTipEx.cpp:86-96, 472-495`, `QListCtrl.cpp:2185-2263`). Result: fights focus, surprises on click, hard to pin — a real window with a real keyboard contract is simpler and nicer.

## Screenshots / evidence URLs
- Animated official demo (list, thumbnails, quick paste): https://raw.githubusercontent.com/sabrogden/Ditto/master/ditto.gif
- Releases (screenshots in changelogs): https://github.com/sabrogden/Ditto/releases
- Key bindings table: https://github.com/sabrogden/Ditto/wiki/Home
- Copy buffers (tabs substitute): https://github.com/sabrogden/Ditto/wiki/Copy-Buffers
- Single-click paste screenshot: https://ditto-cp.sourceforge.io/github-wiki/SingleClickPaste.png
