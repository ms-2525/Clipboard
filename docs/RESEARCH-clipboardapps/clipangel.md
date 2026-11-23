# ClipAngel — how it ships

**Identity.** ClipAngel by *tormozit*. Repo: https://github.com/tormozit/ClipAngel (the `AlexanderPro/ClipAngel` URL in
the brief 404s). **License: GPL-3.0** (`LICENSE:1-3`), *not* Apache-2.0 — read for ideas, never copy; GPL-3 is viral.
**Platform:** C# **WinForms**, `.NET Framework v4.8` (`ClipAngel.csproj`), x86, one portable `ClipAngel.exe`, SQLite.
58 stars, last push 2026-11-14, latest binary **2.23** (Jun 2026, SourceForge). Source read 2026-11-23 at `master`.
`Main.cs` = **9,200 lines / 397 KB**; shape = tray + one `Main` form: left `DataGridView` history, right preview pane,
bottom status strip, filters bottom-left, two vertical toolbars.

## 1. Rich text in the MAIN list — a *painted RTF bitmap*, not a control per row

No WebBrowser and no RichTextBox per row. One custom cell type renders the row's RTF offscreen to a `Bitmap` **at paint time** and blits it in:

- `Components/DataGridViewRichTextBoxColumn.cs:37` — `DataGridViewRichTextBoxCell : DataGridViewImageCell`.
- `:39` **one `static readonly RichTextBox`** shared by all rows; `:88-90` sized to the cell and fed `ctl.Rtf`
  (`:69-81`, falls back to `.Text` when the RTF is invalid).
- `:118` → `Components/RichTextBoxPrinter.cs:109-136` allocates a `Bitmap` and pushes the control through Win32
  **`EM_FORMATRANGE`** (`:49-50, 94`) — a printer API used as a text→bitmap renderer.
- `:144-171` `Paint()` draws it at `cellBounds.Left + 3`; selected rows re-render with `SelectionBackColor/ForeColor`
  (`:98-113`) so formatting survives the highlight. Rows are **flattened to one line**: `WordWrap = false` (`:96`),
  height `= font.Size + 11` (`Main.cs:5505`) → **~20 px** at Segoe UI 9pt (`Main.Designer.cs:756`=19). Formatting
  survives as **colour / bold / italic / inline links**, not layout — lists, indents, headings are lost in the row.

**How a row gets its RTF** (`Main.cs:5008-5078` `PrepareRow`): the clip's `Title` goes into a hidden RichTextBox, search
matches are painted **red + bold** (`:5055`), and the resulting `.Rtf` becomes the cell value (`:5057`). Plain and
formatted clips share *one* pipe: `text → RTF (with highlights) → bitmap`. `SyntaxHighlighter.cs` adds C#/SQL/JSON
colouring first — blue keywords, green comments in the screenshots. A formatted clip looks the same when selected; only
background/foreground swap. The faithful rendering is the *preview pane* (§2).

## 2. Editor — an **in-pane modal toggle**, and it degrades formats

- `Main.cs:123` `bool EditMode`; `:6959-6989 editClipTextToolStripMenuItem_Click` flips it. No window opens: the same
  right-hand `richTextBox` becomes editable (`:1199` `ReadOnly = !EditMode`), full text is force-loaded (`:1198`), and
  leaving edit mode calls `ReloadList()` (`:6972`).
- **Plain text** → edited in place by `SaveClipText()` (`:4449-4469`), which writes only `Title, Text, Size, Chars`.
  `RichText`/`HtmlText` are never rewritten.
- **HTML/RTF clips are not editable**: `:6976-6981` — if `clipType != "text"` it **creates a brand-new plain-text clip**
  (`AddClip(... "text", ...)`) and jumps there. The formatted original stays as a duplicate row and the two diverge. Rich
  editing does not exist: the grid's editing control with bold/italic keys is **commented out**
  (`DataGridViewRichTextBoxColumn.cs:226-394`), so "rich text" is a *view* only, never an editor.

## 3. Images — thumbnail scrap in the row, zoomable viewer in the pane

- Each image clip stores a **100×20 PNG "best fragment"** in `ImageSample` (`Main.cs:2726-2733`).
  `FindBestImageFragment` (`:3001-3060`) scans the first 500×500 px step-2 and returns the first 3×3 block whose
  neighbours differ by >20 — **the first bit of real detail**, not a squashed whole image. Drawn at **80 % opacity**
  (`:5064`) in its own column after a **green 1 px rule** (`DataGridViewRichTextBoxColumn.cs:167-169`); app icons
  likewise at 60 % (`:5133`).
- Preview uses `Components/ZoomablePictureBox.cs`: `PictureBox` in a scrollable `UserControl`, `SizeMode.Zoom`,
  **wheel zoom ×1.1 / ÷1.1** (`:24, 205-252, 305-313`), `ZoomFitInside()`/`ZoomOriginalSize()` (`:261-287`) on image
  selection (`Main.cs:1461-1470`). Image rows read `"842 x 448, Pixel: 32, <window title>"`.

## 4. List anatomy

14 columns, **all headers hidden** (`Main.Designer.cs:735`), **no cell borders** (`:732`), `FullRowSelect` (`:757`),
fixed rows (`:727-728`), `StandardTab = true` (`:758`). Visible, in order: `AppImage` (source **app icon**, default on) →
`TypeImage` (text/html/rtf/file/img/1c glyph, `Main.cs:5019-5041`) → `ColumnTitle` (the rich row, Fill) → `ImageSample`
(100×20 scrap) → `VisualWeight` (**9 px**, `Main.resx` Width=9; one coloured digit = **digit-count of `Chars`**,
`Main.cs:5014`) → `imageSample` (bound) → created/chars/size/used/type/title/id/favorite as hidden plumbing.

- **"Detailed mode" = `ShowSecondaryColumns`** (`Main.cs:5516`): one toolbar toggle reveals the **Created** timestamp
  (+ chars/size). Default **off** — no per-row time; time/type/app/window live in the bottom status strip
  (`:1266-1272`, e.g. `48(2:2)  306 C  1 974 B  rtf  19.03.2017 10:24:12`).
- **State by row tint, not badges:** favourites `255,230,220` peach, already-pasted `210,255,255` pale cyan
  (`App.config`, applied `Main.cs:5077`).
- **Filters** bottom-left: `MarkFilter` (all/used/favourite), `TypeFilter` (all/text/file/img/html/rtf), live text
  filter combo (`Main.cs:384-398`). Type/mark run in **SQL** off-thread via `Task.Run` (`:2029-2060`); the text filter
  becomes a **regex**: words split and OR-ed, `%` → `.*?` (`:5094-5114`), applied as you type (`:7033`). Last filters
  persist.
- Selection: click previews, **double-click pastes and hides** (`:4295-4298`). Caps: 30 000 clips, 2 000 KB/clip,
  `ReplaceDuplicates = True`.

## 5. Visual craft & UX model

- **Segoe UI 9pt** (`App.config Font`), optional `MonospacedFont`; tab stops 4/8/12/16 × font size (`:1213-1220`). One
  type scale, no hierarchy, no headers, 20 px rows — density over polish.
- **Hotkeys:** `Alt+V` open last, `Alt+B` open favourites, plus optional `Alt+…` for incremental/decremental paste,
  simulate-input, forced capture, compare-last. Double-click = paste, `Alt+click` link = open, `End` = clip end.
- **Search-as-you-type is the whole navigation model**: `AutoSelectMatch` + next/prev match; the pane scrolls to and
  pre-selects the match (`:1473-1493`). No folders, no tags, no tabs — Clip's tab model is already ahead. Nice touches:
  `WordWrap` toggle, text-position indicator `1(1:6)` (line:col), source-URL row, `PasteSpecial.cs`, external compare,
  ~40 commands across menu + two toolbars, discoverable only by tooltip.

## 5 things Clip MUST steal (WPF-native, no GPL code)

1. **Formatted rows as `TextBlock.Inlines`, built once per row — no WebBrowser, no per-row control.** Clip already
   ships `RtfPipe` + `HtmlRenderer.WPF`: on load, RTF → `FlowDocument` → copy paragraph `Inlines` into the template's
   `TextBlock` (wrap off, single line, ellipsis). Virtualizes, keeps bold/italic/colour, selection, DPI. Not the bitmap.
2. **Red-bold match highlighting baked into the row's runs** (ClipAngel `Main.cs:5055`). Rebuild `Inlines` with matched
   substrings as `#D32F2F`-bold. Highest-value search affordance in ClipAngel; costs one `Run` splitter.
3. **The 9 px "magnitude digit" column** (`Main.cs:5014` + `Main.resx` Width=9): one coloured digit = order of magnitude
   of clip size. ~5 lines, fixed 10 px column, tooltip with exact chars/bytes. Instant gravity at 30 k clips.
4. **Best-fragment thumbnails for images** (`FindBestImageFragment`, `:3001-3060`) instead of a squashed screenshot —
   100×20 crop of the first textured region at 80 % opacity. ~25 lines of `CopyPixels`; stored in `item_formats`, so
   **no schema change**.
5. **Tint rows by life-state** — favourite peach `#FFE6DC`, pasted pale cyan `#D2FFFF` — as tokens in `UI/Themes/*.xaml`.
   One `IValueConverter`, zero layout; Clip has tabs but no per-row state signal. Bonus: **quick edit-in-place** in the
   preview pane (one toggle, `Esc` = save and back) instead of a second window.

## 3 anti-patterns

1. **Editing destroys formats.** `Main.cs:6976-6981` forks a *new plain-text clip* whenever you edit an HTML/RTF clip,
   and `SaveClipText` (`:4455`) never rewrites `RichText`/`HtmlText`, so rich columns go stale: duplicate rows and silent
   capability loss. Clip must edit the selected format in place and persist it back — or refuse with a clear
   "convert to plain text?" confirm.
2. **Synchronous render on every paint.** `Paint` (`:144-147`) calls `GetRtfImage` on **every repaint**: new `Bitmap`
   plus an `EM_FORMATRANGE` round trip per visible cell, on the UI thread, with an `extraWidth = 200` fudge (`:117`) to
   stop clipping, and no cache. `DoubleBuffered` is set by **reflection on a private member** (`Main.cs:363-366`). Clip
   must render once per item and reuse it.
3. **God-form + dead surface.** A 9,200-line `Main.cs` holding UI, SQL, image algorithms, search and paste; a 14-column
   grid where 9 are hidden plumbing; a `ClipListSimpleDraw` setting declared in `App.config` and `Settings.Designer.cs`
   but **read nowhere** (verified repo-wide). Clip's 400-line budget and `Core`-owns-semantics rule are the right
   answer — resist "just add a column/setting".

## Screenshots (repo `Screenshot/`, raw URLs verified 200)

- Text clip + list + status strip: https://raw.githubusercontent.com/tormozit/ClipAngel/master/Screenshot/Text%20clip.png
- RTF **native** (row syntax colouring, favourite/used tints, size digits): https://raw.githubusercontent.com/tormozit/ClipAngel/master/Screenshot/RTF%20clip%20in%20native%20mode.png
- HTML **native** / **text** (colour vs flattened + image scraps) and image clip: `Screenshot/HTML%20clip%20in%20native%20mode.png`, `HTML%20clip%20in%20text%20mode.png`, `Image%20clip%20.png`, `Text%20filter.png` (same raw base)
- Project page (binaries, changelog): https://sourceforge.net/projects/clip-angel
