# DESIGN-v2-ui — the implementable spec for the v2 fix sprints

**Status:** frozen decisions. Owner law: [`DESIGN-v2-directives.md`](DESIGN-v2-directives.md).
Written 23 Nov 2026 by the v2 UI/UX design agent. Builders implement this file; they do not re-decide it.

**Inherits unchanged:** `DESIGN-typography.md` (Manrope 400/500/600, type tokens),
`DESIGN-contrast.md` (hexes), `DESIGN-antislop.md` (copy, drawn icons), `RESEARCH-ortu-ui.md`
(flat rows, rails), `RESEARCH-features-and-scale.md` §B (the 1000-item contract).
**Superseded by the owner:** `DESIGN-layout.md` §5–§6 (grow-on-selection, meta-always) and
`RESEARCH-motion-ui.md` rows 1–2, 12 (growth) — deleted, see §6.

**What this doc OWNS:** row anatomy, the inline rich swap, the Images tab, the F2 editor and its
save contract, the motion that belongs to these surfaces, the file/slice plan.
**NOT this doc:** window chrome, ClearType restoration, entrance/close animation ownership —
that is the crisp-text sprint. This doc only states what those designs *assume* (§5).

---

## 0. The five decisions, in one paragraph

Rows are **content-sized blocks with a 6-line cap**: height is computed once at load and never
changes — selection is a rail + tint + ring on a frozen box, so ↑/↓ cannot move the list.
The 22 px icon tile becomes the **number** (`1..N` = `Ctrl+1..9`); the meta line and its hairline
are **deleted**; the `+N` badge moves to the first line's right. The selected row **swaps its text
for the rich render inside the same fixed box**, one `HtmlPanel` alive, torn down on deselect.
Images get a **dominant preview (538 × 263) over a virtualising filmstrip of 104 px tiles** — the
preview is the "big image", the strip is the browse surface, ←/→ walks it. The editor is the
**existing native `RichTextBox`, fixed in place**: one surface for every text clip, RTF treated as
bytes, browser HTML converted by a small in-repo `HtmlToRtf`. No WebView2, no new dependency.

---

## 1. Row anatomy v2

### 1.1 The one rule

> **A row's height is a function of its content at load time, and of nothing else — ever.**

Selection may change exactly four things on a row: `SelLayer.Opacity`, `Ring.Opacity`,
`Rail.Opacity`, `Body.Foreground`. Nothing else. No `Height`, `MaxHeight`, `Margin`, `Padding`,
`BorderThickness`, `CornerRadius`, `FontSize`, or `Visibility` of a *layout* element may change on
selection. That single rule is what removes the jumping (directive §1).

### 1.2 Height

```
lines   = clamp(1, min(cfg.PreviewLines, 6), wrappedLines(preview))
RowHeight = 16 + lines × 18          (8 pad + lines×18 + 8 pad)
TextHeight = lines × 18
```

| lines | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| `TextHeight` | 18 | 36 | 54 | 72 | 90 | 108 |
| `RowHeight` | **34** | **52** | **70** | **88** | **106** | **124** |

- **Cap chosen: 6 lines / 124 px.** At 124 px the 383 px list viewport still shows 3 whole rows;
  at 7–8 lines it shows 2, and the popup stops being a list. The old selected-card cap was 3.
- `cfg.PreviewLines` still validates **1..100** (owner-frozen range, not re-opened). Values above 6
  are stored and **render at 6**; the Settings hint line must say so (§7, the one-line S1a ticket).
- `wrappedLines(preview)` = for each explicit line, `ceil(len / 62)`, summed, using at most the
  first **20 000 characters** of the item's text (badge counts stop at "300+"). `62` is the shipped
  `ClipRow.CharsPerLine` constant — no text measurement, no `FormattedText`.
- Files (`kind = "files"`) and images-in-a-list-tab are always `lines = 1`.
- The row is a `Border` with a literal `Height="{Binding RowHeight}"`. **Never `MaxHeight`**
  (typography §7.3: trimming needs a constrained measure; an animated `MaxHeight` is what produced
  the jumping).

### 1.3 Element table (popup list)

Rails are unchanged from `DESIGN-layout.md`: list inset 6, content rail 18, text rail 50,
content right 532. Only the *inside* of the row changes.

| Element | Value |
|---|---|
| List | `ListBox Padding="6"`, `VirtualizingPanel.ScrollUnit="Item"`, `VirtualizationMode="Recycling"`, `CacheLength="1,1"` (kept) |
| Row `Border` (`RowRoot`) | `Background=Transparent`, `CornerRadius=12`, `Height={Binding RowHeight}`, **no** border, **no padding** (padding lives on the content grid, so the tint/ring/rail stay full-bleed) |
| Content grid | `Margin="12,8"`, columns `22 / 10 / * / Auto` |
| Number slot | `Grid.Column 0`, `Width=22`, `Height=18`, `VerticalAlignment=Top` |
| Number text | `11 / 18`, SemiBold, `Typography.NumeralAlignment=Tabular`, `TextAlignment=Right`, `Fg55` → `Fg75` when selected |
| Gap | `Column 1`, `Width=10` |
| Body | `Grid.Column 2`, `Height={Binding TextHeight}`, `13 / 18`, `TextWrapping=Wrap`, `TextTrimming=CharacterEllipsis`, `LineStackingStrategy=MaxHeight`, `Fg55` → `Fg75` selected |
| Badge (trailing) | `Grid.Column 3` (`Auto`), `Height=18`, `Padding="6,0"`, `CornerRadius=9`, `Background=BadgeBackground`, right edge **532**, `Margin="8,0,0,0"` |
| Badge text | `11 / 14`, SemiBold, `Fg55`, tabular |
| SelLayer | `RowSelected` (`#1C1310`, 8 % accent), radius 12, `IsHitTestVisible=False` |
| Ring | `SelectionRing` `#3E2517`, 1 px, radius 12 (decoration only — the rail carries the state) |
| Rail | `2 × 24`, radius 1, `SelectionRail` gradient, `HorizontalAlignment=Left`, `VerticalAlignment=Top`, `Margin="0,8,0,0"` |
| HoverLayer | `RowHover`, radius 12, 100 ms opacity (existing) |
| Row tooltip | `ToolTip="{Binding Tooltip}"`, `TextWrapping=Wrap`, `MaxWidth=420`, `ToolTipService.InitialShowDelay=600` — the only home for the full text and full file paths; app/time stay unsurfaced (directive §2) |
| Section band (first item of a section) | `Margin="12,0,12,8"`, `10 / 14` SemiBold `Fg48` + hairline, unchanged (22 px block above the row) |

Text column widths for the pair estimator: 482 px without a badge, ~436 px with one. The `62`
chars/line constant is deliberately conservative for both (it wraps slightly early with a badge —
stable, not wrong).

### 1.4 The number chip (directive §3)

- Replaces the 22 × 22 icon tile **and its background**: no `TileBackground`, no glyph. The number
  sits on the row's own background, right-aligned in the old tile slot so 1- and 2-digit numbers
  share one right edge at x = 40.
- Numbers are the **row's position in the visible list** (`index + 1`), recomputed in `ApplyFilter`
  exactly where `SectionLabel` is set today. 1..9 are live `Ctrl+1..9`; 10+ are shown for
  completeness and are not shortcuts.
- **Pinned / custom tabs:** same chip, same rules (the Ctrl+digit binding pastes the Nth item of
  whatever tab is open, so the chip must be identical).
- **Images tab:** the chip becomes an overlay on each strip tile — `Height=16`, `Padding="4,0"`,
  `CornerRadius=4`, `Background=App` (opaque, so it reads over any image), `11 / 14` SemiBold,
  `Fg75`, at the tile's top-left with a 4 px inset.
- Colour: `Fg55` (6.16 on App, 4.98 on the selected tint — passes 4.5 for text). Never `Fg35`.

### 1.5 The `+N` badge

- Shows only when the clamp actually hides lines: `More = "+" + hiddenLines`, tooltip
  `"{n} more lines not shown"` (hidden = total − visible, floored at 1 for a 400-char cap).
- Right edge **532**, vertically centred on the **first** line box (`VerticalAlignment=Top`,
  `Height=18`), so it never collides with the last line's ellipsis.
- Not interactive (no click target, no ToggleButton). Visible text stays `+N`; the sentence lives
  in the tooltip. This is a **documented deviation** from `DESIGN-antislop.md` §6.2's
  `"90 lines not shown"` — a full sentence does not fit a trailing chip and would push the badge
  to a second line, which is the geometry change the owner forbade.
- Images and files never show a badge.

### 1.6 File rows and image items in list tabs

- **Files:** body = the first file's **basename**, plus `"  +N more"` when the clip holds several
  (N = count − 1). One line, `RowHeight = 34`. Full paths live in the tooltip; a path in a 34 px row
  is noise, the name is the identity.
- **Image item inside Pinned / a custom tab:** body = `"Image  ·  120 KB"` (the shipped
  `BodyText` shape), one line, no inline image, no badge. Looking at images is the Images tab's job
  (§3); decoding a 5 MB PNG into a 34 px list row buys nothing.
- **Clipboard tab:** unchanged rule — `kind = "image"` never appears there.
- **No icons in list rows at all** (the drawn-icon rule is satisfied by having no icon).

### 1.7 What this kills (delete, do not leave dormant)

| Deleted | Where |
|---|---|
| `Grow()`, `ReleaseRow()`'s height reset, the 120 ms growth tween, `_navRepeat` growth branch | `MainWindow.Rich.cs`, `MainWindow.Keys.cs` |
| `MetaBlock` + hairline + `MetaApp`/`MetaTime` bindings and properties | `MainWindow.xaml`, `ClipRow.cs` |
| `TypeTile`, `ThumbTile`, `KindIcon`, `TileBackground` usage | `MainWindow.xaml`, `ClipRow.cs` |
| `BodyText`, `MoreLines`, `BadgeVisibility`, `LineCount` (replaced by §1.2/§1.5) | `ClipRow.cs` |

`items.app_name`, `window_title`, `captured_at` stay stored (directive §2: "surface it nowhere by
default"). `ClipboardItem.Meta` becomes dead: **delete it** rather than leaving two meta models.

### 1.8 WPF mechanics (the part that makes it not jump)

1. `Height` is a **bound literal per row**, computed in `ClipRow`'s constructor. Nothing in code
   ever writes a row's height after that.
2. `UseLayoutRounding="True"` + `SnapsToDevicePixels="True"` already on the window; all heights are
   even numbers, so 100 % and 150 % DPI both land on whole pixels.
3. The rich panel lives inside a `Border` whose `Height` is bound to `TextHeight`, with
   `ClipToBounds="True"` and **no** `MaxHeight` animation (§2).
4. The item template's root stays a `StackPanel` (`ItemRoot`) so the optional 22 px section band is
   simply `Visibility=Collapsed`/`Visible` — a constant per *item*, resolved at load, not on
   selection.
5. Recycling rule: anything set from code-behind (rich content, body/rich visibility) must be reset
   in `ReleaseRow` when a row is deselected, because a recycled container keeps code-set values.
   XAML-trigger state (`IsSelected`) resets automatically.

### 1.9 Scroll stability contract (directive §1)

1. **Extent is constant** while arrowing: heights are fixed, so `ScrollViewer.ExtentHeight` must be
   byte-identical before and after 20 `↓` presses. That is a testable assertion (§8).
2. **Minimal delta only:** `History.SelectedItem = row; History.ScrollIntoView(row);` — WPF's
   `ScrollIntoView` is a no-op when the item is fully visible and scrolls the smallest amount
   otherwise. Never call `ScrollToVerticalOffset`, never `ScrollToEnd`, never an animated scroll.
3. **Keep `ScrollUnit="Item"`.** Do *not* switch to `Pixel` "for smoothness": with heterogeneous
   row heights the pixel extent is estimated and the thumb/offset jumps (scale doc B2/B8). Item
   scrolling's worst case is one row (≤ 124 px).
4. `PageMove`: `step = max(1, floor(viewport / currentRow.RowHeight))` — the shipped
   `ActualHeight / 65` is a dead constant once heights vary.
5. Selection is never re-seated by a scroll: `ScrollIntoView` happens *after* `SelectedItem` is set,
   in one place (`SelectAt`), once per keypress.
6. On filter / reload / tab switch the collection instance is replaced (120 ms debounce, scale
   contract B10); the selection legitimately resets to row 0 and one `ScrollIntoView` runs. That is
   a content change, not a selection jump.

---

## 2. Inline rich in the popup (directive §4)

### 2.1 The swap

Keep the shipped mechanism — **code-behind content assignment**, not a `ContentTemplate` trigger.
The one-panel rule has to live in one place anyway, and a `ContentTemplate` selector would either
allocate a template per state or hide the teardown from the reader.

- Part names (frozen; `RowTemplate.xaml` and `MainWindow.Rich.cs` both depend on them):
  `RowRoot`, `HoverLayer`, `SelLayer`, `Ring`, `Rail`, `PulseLayer`, `NumberText`, `Body`,
  `Badge`, `PreviewHost`, `RichHost`.
- `PreviewHost` is a `Border` in the same grid cell as `Body`, `Height={Binding TextHeight}`,
  `ClipToBounds="True"`, `Visibility=Collapsed`. On selection with a panel: `Body.Visibility=Collapsed`,
  `PreviewHost.Visibility=Visible`, `RichHost.Content = panel`. On deselect: content null, host
  collapsed, body visible. Both directions are in `EnsureRich` / `ReleaseRow` — one function each.
- **Geometry never changes:** the host is the body's box. A 1-line clip therefore shows an 18 px
  slice of its rich render; that is the directive's contract, not a bug (a clip cannot be one line
  tall and six lines of rich at the same time).

### 2.2 What renders

| Item has | Selected row shows |
|---|---|
| `html` | `HtmlFragment.Extract(html)` → `HtmlPanel` |
| `rtf` only | `RtfPipe.Rtf.ToHtml(rtf)` → `HtmlPanel` (try/catch: on failure keep the plain body, no panel) |
| neither (plain) | nothing swaps — the plain body *is* the content |
| `kind="image"` in a list tab | nothing swaps (never decoded here) |
| html > 2 000 000 chars | nothing swaps (existing ceiling, kept) |

`HtmlPanel` config stays as shipped: `Background=Transparent`, `Padding=0`, `AvoidImagesLateLoading`,
`IsSelectionEnabled=false`, `IsContextMenuEnabled=false`, `BaseStylesheet` = the shipped `RichCss()`
(Segoe UI 13/1.385, `Fg75`, accent links). Its `MaxHeight` becomes **`TextHeight`** (`140` dies).

### 2.3 Memory rule (non-negotiable — 253.7 MB was measured against a 250 MB ceiling)

1. **At most one `HtmlPanel` alive.** `EnsureRich` calls `DropPanel()` *before* assigning the new
   document; `DropPanel` unsubscribes `ImageLoad`, sets `Text = null`, drops the reference.
2. Teardown on **every** deselect (`ReleaseRow`), on popup hide (`ClearRich`), and on tab switch.
3. No pre-warming, no cache, no second panel for the "previous" row, no panel while the row is not
   realised (`ContainerFromItem` returns null → skip; do not force layout to conjure one).
4. The v2 no-growth swap does not regress this: the panel's document is identical to what the old
   `PreviewHost` held; the only change is that its box is now the body box instead of
   `MaxHeight=140`.
5. Verification hook: arrow through 20 rich rows, assert the live `HtmlPanel` count is 1 and
   working set stays under 200 MB (§8).

### 2.4 `ImageLoad` rules (keep exactly as shipped)

- `data:` URIs → left to the renderer (allowed).
- Everything else (http/https/file/path) → `e.Handled = true; e.Callback(BlankImage)` with the
  frozen transparent 1×1 PNG. No network, no disk, ever, while arrowing.

---

## 3. Images tab v2 (directive §6)

### 3.1 Layout decision: dominant preview + filmstrip

**Chosen:** a 538 × 263 preview pane on top of a horizontal, virtualising filmstrip of 104 px tiles.

Why not the alternative I rejected (a 3-column tile wall, 174 × 120, 9 visible):
the owner's words are *"the image should show in a bigger preview… cover as much screen inside the
max height as possible"* and *"how will I see the images when I have say 20 images?"*. A 174 px
thumbnail of a screenshot is a coloured rectangle — recognition happens at the big preview, and
`←`/`→` steps it with no opening, no second window, no Enter. The wall trades the one thing the
owner asked for (size) for a little more strip capacity.
Cost of the choice, stated honestly: **4 full thumbnails visible at a time** (+ a partial 5th that
signals scrollability), not 9. The counter (`7 / 20`) tells the user where they are.

Mechanically this is also the smallest change: it re-uses the existing `ListBox` (selection,
`ScrollIntoView`, hover, pulse, `Ctrl+1..9`) with a horizontal `VirtualizingStackPanel` — no
`WrapPanel` (non-virtualising, forbidden by scale doc B8), no wrapper row-VM, no second selection
model.

### 3.2 Geometry (popup 550 × 520; list viewport = 520 − 48 − 45 − 32 = **395**)

| Element | Value |
|---|---|
| Preview `Border` | `Margin="6,6,6,0"`, `Height=263`, `CornerRadius=12`, `Background=Field`, `ClipToBounds=True` |
| Preview `Image` | `Stretch=Uniform`, `Margin=8` (image box 510 × 247), never cropped |
| Counter | bottom-right, `Margin="0,0,8,8"`, `Height=20`, `Padding="6,0"`, `CornerRadius=6`, `Background=App`, `11 / 14` Medium `Fg55`, `"{index+1} / {count}"`, tabular |
| Gap | 8 |
| Strip | the `History` `ListBox` with `Padding="6,4"`, `Height=112`, `VerticalScrollBarVisibility=Disabled`, `HorizontalScrollBarVisibility=Hidden`, `ItemsPanel = <VirtualizingStackPanel Orientation="Horizontal"/>` |
| Tile | `104 × 104`, `Margin="0,0,8,0"`, `CornerRadius=8`, `Background=Field`, image `96 × 96` centred (`Stretch=UniformToFill` + `ClipToBounds` so tiles fill without distortion of the tile, at the cost of cropping) |
| Tile selected | 1 px `AccentText` ring drawn as an overlay `Border` (no thickness change → no reflow) + 100 ms fade |
| Tile hover | 1 px `ControlLine` overlay, 100 ms, **no scale** (the old 1.03 grow is dead: geometry stays put) |
| Number chip | §1.4 (16 px-high opaque `App` pill, top-left, 4 px inset) |

263 + 8 + 112 + 6 = 389 ≤ 395 (6 px slack). Numbers are exact; do not "round" them.

### 3.3 Keyboard (Images tab only)

| Key | Action |
|---|---|
| `→` / `←` | next / previous image (selection + horizontal `ScrollIntoView`) |
| `↓` / `↑` | ±8 images (one strip page) |
| `Home` / `End` | first / last |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | switch tab (the shipped binding — plain `←`/`→` are reassigned to browsing, so do not invent `Ctrl+←/→`) |
| `Enter` | paste the selected image (existing `PasteRequested`) |
| `Ctrl+1..9` | paste the Nth image |
| `F2` | open in the default viewer (unchanged `ImageCache.Write`) |
| `Ctrl+C`, `Delete`, `Esc`, typing | unchanged |

The IME gate (`Key.ImeProcessed || _composing`) stays *above* this table: it is why the gate is
first in `OnPreviewKeyDown`.

### 3.4 Decode path (off the UI thread) and memory

```
select(item) →
  preview.Image = item.Thumb (96 px, instant, already decoded)   // no blank frame
  seq = ++_previewSeq
  Task.Run(() => ImageCache.Decode(pngBytes, 1024))              // frozen BitmapSource
    → Dispatcher: if (seq == _previewSeq) preview.Image = decoded
```

- `ImageCache.Decode(byte[] png, int targetWidth)`: header-read `BitmapFrame.Create(stream,
  DelayCreation, None).PixelWidth` (cheap, no full decode), then `width = min(targetWidth,
  sourceWidth)`, `DecodePixelWidth=width`, `CacheOption=OnLoad`, `Freeze()`. Works off the UI thread;
  the returned source is frozen so the UI thread never touches the decoder.
- `targetWidth = 1024` (the 510 DIP box at 200 % DPI). PNG bytes come from
  `_store.GetImage(id)` — one row at a time, released after the decode starts.
- **One** large bitmap alive: the previous `BitmapSource` reference is dropped on swap (no cache).
- Ceiling with the popup open: preview ≤ 4.2 MB (1024² worst case) + realised tiles (~6 × 37 KB)
  + one stored PNG (≤ 5 MB) ≈ **≤ 11 MB** on top of the list. The 250 MB hard ceiling and the
  150 MB idle rule are untouched by this surface.
- No decode for the strip: tiles keep the stored 96 px thumb (`ClipRow.Thumb`, `DecodePixelWidth=96`).
- Tab switch / popup hide: `_previewSeq++`, preview `Image.Source = null`, drop the decode.

### 3.5 Mouse and states

- Click a tile selects it (ListBox default); double-click pastes (existing handler).
- Wheel over the strip scrolls it horizontally: `PreviewMouseWheel` → `ScrollViewer.LineRight/LineLeft`
  by 1, `e.Handled = true` (so a 20-image strip is reachable without a scrollbar).
- Empty Images tab: the shipped empty state ("No images yet" / "Copy a screenshot…"), unchanged.
- Status bar stays `"{n} images"`; the search placeholder stays `Search Images`.

---

## 4. F2 editor — ONE editor for everything (directive §5)

### 4.1 Decision: the shipped native `RichTextBox`, fixed in place

**One surface, always rich-capable.** `PlainBox` is deleted. Plain clips, RTF clips and HTML-only
clips all open the same `RichTextBox`; the item's format is converted *into* RTF on load.

Why not WebView2, concretely (this is the test the brief sets: name something the native path
cannot do):

| | Native `RichTextBox` | WebView2 |
|---|---|---|
| Dependency | none — already in WPF | `Microsoft.Web.WebView2` package **+ the Evergreen runtime**; Win11 ships it, locked-down enterprise images and N editions may not (the app's only editor would fail to open) |
| Startup | 0 ms, control already loaded | WebView2 environment creation on first open (hundreds of ms, own user-data folder, browser process) |
| Paste contract | RTF is what Office/Word/WordPad consume — the format the clipboard path already restores | `innerHTML` is not; we'd need the same HTML→RTF converter anyway plus a browser |
| IME (Devanagari) | native `TextBoxBase` IME, same path as the search box | child HWND with its own focus/IME composition queue; Esc/Ctrl+S routing and owner-focus need extra plumbing |
| What it can do that we need | everything the directive asks | renders CSS layout natively — **the one capability traded away** (see §4.5) |

The native path's real cost is the HTML→RTF subset converter (§4.5), ~200 lines in `Core`, which
is smaller than the WebView2 plumbing it replaces and adds no runtime risk.

### 4.2 Load contract

`EditorWindow(store, item)` — the `html`/`rtf` arguments stay, `_rich` and `PlainBox` die.

| Order | Condition | Path |
|---|---|---|
| 1 | `rtf` non-empty | `RtfText.ToBytes(rtf)` → `TextRange.Load(stream, DataFormats.Rtf)` |
| 2 | else `html` non-empty | `HtmlFragment.Extract(html)` → `HtmlToRtf.Convert(fragment)` → same `Load` |
| 3 | else | `range.Text = item.Text` (blank lines become paragraphs natively) |
| 4 | load threw (any) | `range.Text = item.Text` **and** show the notice bar |

- `_hadHtml = !string.IsNullOrEmpty(html)` — the only input to the save rule (§4.3).
- The fallback is **visible, never silent** (directive §5): a `FormatNotice` `Border` under the
  header, `11 / 16` `Fg55`, text *"Formatting could not be loaded. Editing as plain text."*, with
  `AutomationProperties.Name` set. Hidden unless step 4 ran.
- Content font is **Segoe UI**, not Manrope: the editor renders *clip content*, and Manrope has no
  Devanagari glyphs (a Hindi clip would fall back per glyph). UI chrome stays Manrope.
- Focus goes to the caret at `Document.ContentEnd`; `Ctrl+B / I / U` work natively (WPF editing
  commands). **No formatting toolbar in this slice** — a 4-button toolbar is the first thing to add
  *if the owner asks*; the directive's complaint was the wrong surface, not missing buttons.

### 4.3 Save contract (exact)

`Ctrl+S`, or `Save changes`. One `_store.Update(item)` call, unchanged in the store.

| Field | Rule |
|---|---|
| `items.text` | `TextRange.Text` of the whole document, `TrimEnd('\r','\n')` (existing `Normalize`) |
| `items.rtf` | **always** written: `range.Save(Rtf)` bytes → `RtfText.FromBytes` (Latin-1 string) |
| `items.html` | if `_hadHtml` → `RtfPipe.Rtf.ToHtml(rtf)` in try/catch (failure ⇒ `null`); otherwise `null` |
| `item_formats` blobs | **deleted** — the store already does this, because the original format blobs no longer describe the edited body |
| `items.captured_at` | bumped to now (store behaviour: an edited clip moves to the top) |
| `items.image` / `thumb`, `kind`, `tab_id` | untouched (the editor never opens images) |

Paste-after-edit consequence (state it, don't hide it): no format blobs remain, so
`PasteInjector` falls through to the `Text` + `Rtf` (+ `Html` if regenerated) branch. Word/WordPad
→ rich. Browser → rich only when `_hadHtml` was true. Notepad → plain. Images never reach here.

### 4.4 The two known bugs — both are load-path bugs, both die here

1. **`_rich = !string.IsNullOrEmpty(rtf)`** → an HTML-only browser clip opened in the plain box.
   Fixed by rows 1–2 of §4.2: HTML is a first-class input, always rendered.
2. **`Encoding.UTF8.GetBytes(rtf)`** → 8-bit RTF throws and silently degrades to plain. Fixed by
   `Core\RtfText.cs`:

```csharp
static class RtfText
{
    // The DB column is TEXT, so RTF travels as a string. Latin-1 is the only encoding that maps
    // every byte 0x00–0xFF to one char and back; UTF-8 re-encodes high bytes and corrupts 8-bit RTF.
    public static bool IsEightBit(string s) => s.All(c => c <= 0xFF);
    public static byte[] ToBytes(string rtf) =>
        IsEightBit(rtf) ? Encoding.Latin1.GetBytes(rtf) : Encoding.UTF8.GetBytes(rtf);
    public static string FromBytes(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
```

   Load is a tolerant chain, and the last step is the **visible** notice: `ToBytes` → `Load`; on
   throw, `UTF8.GetBytes` → `Load`; on throw, plain text + notice. Save always writes
   `FromBytes` (Latin-1), so a clip saved by v2 round-trips byte-exactly from then on.
   *Residual limit, stated:* rows captured before this change hold WPF's own decoding of CF_RTF;
   the chain recovers them, but a legacy row that WPF already mangled cannot be un-mangled.

### 4.5 `HtmlToRtf` — the subset, and its limits

New `Core\HtmlToRtf.cs`, input = the `HtmlFragment.Extract` fragment (an HTML *body* fragment, not a
CF_HTML header). Tokeniser + tag stack (~200 lines), emits RTF text.

| Supported | Emitted as |
|---|---|
| `p`, `div`, `br`, `h1`–`h3`, `blockquote` | paragraphs; heading sizes 2/1.5/1.25×; blockquote indented 360 twips |
| `b/strong`, `i/em`, `u`, `s/strike`, `code` | `\b`, `\i`, `\ul`, `\strike`; `code` → Consolas |
| `a` | link text only (no URL field) |
| `span`/`font` inline styles | `color`, `font-weight`, `font-style`, `text-decoration`, `font-size` (px→half-points), `font-family` |
| `ul`/`ol`/`li` | indented paragraphs with `•` / `1.` prefix |
| `pre` | Consolas paragraph, `\brdrb` hairline retained as plain text |
| entities | `&amp; &lt; &gt; &quot; &nbsp; &#NNN; &#xNN;` |
| **tables** | **flattened**: each cell → a paragraph, cells in a row joined by a tab, rows separated by a blank line |
| `img` | dropped (no remote fetch) |
| `head`, `style`, `script`, comments | **skipped entirely** — their text must never reach the document (a saved clip's `html` is a full `RtfPipe` document, `<style>` included) |
| everything else | tags skipped, text kept |

Limits (documented, not hidden): CSS classes, backgrounds, floats/flex/grid, web fonts and table
structure do not survive. Devanagari passes through as RTF `\uN` escapes and renders correctly.
Input larger than 2 MB is refused → plain text + the visible notice.

### 4.6 The round trip the owner sees

Open a browser-copied table → it appears as the same words with bold/colour/size, one cell per
line (not a grid). Type, restyle, save → it pastes into Word as formatted RTF, and back into the
popup the selected row shows the *regenerated* render. `html`-origin clips keep a rich browser
paste; everything else pastes rich in Office and plain elsewhere.

---

## 5. Compatibility requirements (owned by the crisp-text sprint — this doc only assumes them)

These designs **assume**, and must not be broken by the window work:

1. **Opaque windows** (`AllowsTransparency="False"`) for popup, editor, notification, settings →
   ClearType is reachable. The row tints (`RowSelected #1C1310`, `RowHover #121316`) are
   pre-composited opaque colours and need an opaque `App` behind them; nothing in §1 depends on
   per-pixel alpha.
2. **`TextOptions.TextFormattingMode="Display"`** window-wide (and `TextRenderingMode` left at a
   value that permits ClearType — a forced `Grayscale` defeats the point).
3. **`UseLayoutRounding` + `SnapsToDevicePixels`** on every window: the §1.2 heights and the §3.2
   geometry are DIPs that must land on device pixels.
4. **Shell border thickness stays 0.** Rows are measured against a 550 px content box (rails 18/50/532).
   If the chrome adds an inner 1 px frame, the list viewport and every rail shift by that border.
5. **Popup entrance/close animation is not this doc's** (§6). Any entrance must not change window
   opacity in a way that re-introduces a layered window, or §5.1's ClearType target is lost.
6. **Toast hit-testing untouched** (click-through except the ✕). The opaque window must not swallow
   clicks that the transparent toast used to pass through.
7. **One `HtmlPanel` maximum** stays true after the restructure (a per-element opacity animation on
   a *parent* of the panel would put it on an intermediate surface; animate only the overlay layers).

---

## 6. Motion v2 (what this doc owns)

| Item | v2 | Why |
|---|---|---|
| **Row growth (1→3 lines)** | **DEAD** | directive §1; heights are frozen at load |
| Window entrance / close fade, settings fade, notification fade+settle | **owned by the crisp sprint** | may be replaced by a translate or become instant |
| Popup close on paste / Enter / Ctrl+n | **0 ms `Hide()`** (unchanged) | an exit ghost swallows the next click |
| Row hover (`HoverLayer.Opacity`) | 100 ms, `KeySpline(0.4,0,0.2,1)` | shipped |
| Selection change (`SelLayer` + `Rail` + `Ring`) | 100 ms, same curve | shipped; the only selection motion |
| Copy pulse (`PulseLayer.Opacity`) on the just-captured / deduped row **while the popup is open** | 380 ms = 150 in / 80 hold / 150 out, Linear | shipped, Ortu's split |
| Dedupe FLIP (`ItemRoot.TranslateTransform.Y`) | 200 ms, `KeySpline(0,0,0,1)` | shipped, **fixed**: the delta must be *measured* (`TransformToAncestor` before/after the move), not `oldIndex × 65` — that constant is dead with variable heights. Container null → no animation (the documented escape hatch) |
| Images strip hover | 1 px `ControlLine` overlay, 100 ms, no scale | replaces the 1.03 thumb grow |
| Image preview swap | **instant** (thumb → decoded) | a cross-fade on a moving selection would smear the browse |
| Reduced motion (`SystemParameters.ClientAreaAnimation == false`) | pulse skipped; tints snap; FLIP skipped; close instant | unchanged policy |

---

## 7. File plan (disjoint ownership, ≤ 5 production files per slice)

Order matters; run the listed sprints in this sequence, and never in parallel with a slice that
shares a file.

### Sprint 1 — rows (parallel, two disjoint slices)

**S1a — row template + row data (4 files)**
`UI\RowTemplate.xaml` *(new)* · `UI\MainWindow.xaml` · `UI\ClipRow.cs` · `App.xaml` (one merged
dictionary line)
- Move the whole row `DataTemplate` + `ListBoxItem` style into `RowTemplate.xaml`, merged in
  `App.xaml`; MainWindow.xaml drops from 517 to ~360 lines and references
  `{StaticResource ClipRowTemplate}`.
- Declare (markup only, collapsed) the Images pane from §3.2: `ImagesPane` `Border` + `PreviewImage`
  + `PreviewCounter`, so Sprint 2 needs no markup edit.
- `ClipRow`: `Number`, `RowHeight`, `TextHeight`, `Body`, `More`, `Tooltip`; delete
  `MetaApp/MetaTime/BodyText/MoreLines/BadgeVisibility/KindIcon/LineCount`.
- Post-merge one-liner: `SettingsWindow.xaml:347` hint → `1 to 100. Rows grow with content, up to 6 lines.`
  (sequenced **after** the crisp sprint's Settings edit, never concurrently).

**S1b — row behaviour (3 files)**
`UI\MainWindow.xaml.cs` · `UI\MainWindow.Rich.cs` · `UI\MainWindow.Keys.cs`
- `ApplyFilter`: set `Number` alongside `SectionLabel`; keep the 120 ms debounce + instance swap.
- `Rich.cs`: delete `Grow()`; `EnsureRich` targets the fixed host; `ReleaseRow` tears down.
- `Keys.cs`: `PageMove` step from the current row height; keep the IME gate first, keep
  `Ctrl+1..9`, `Home/End`, `Enter`, `F2`, `Delete`, `Ctrl+P/M/T/W/Z/C`.

### Sprint 2 — Images tab + editor (parallel, zero shared files)

**S2 — Images (5 files)**
`UI\MainWindow.Images.cs` *(new)* · `UI\MainWindow.Tabs.cs` · `UI\MainWindow.Keys.cs` (the Images
key branch from §3.3 — S1b is finished by now, so the file is free) · `UI\RowTemplate.xaml` (add
`ImageTile` + `StripPanel` resources) · `Core\ImageCache.cs` (add `Decode`)
- `SwitchTab`: swap `History.ItemTemplate`, `History.ItemsPanel`, `History.Padding`, and
  `ImagesPane`/`History` visibility; `_previewSeq++` on leaving.
- `MainWindow.Images.cs`: preview decode (§3.4), counter text, wheel-to-horizontal, key branch.

**S3 — editor (5 files)**
`UI\EditorWindow.xaml` · `UI\EditorWindow.xaml.cs` · `Core\HtmlToRtf.cs` *(new)* ·
`Core\RtfText.cs` *(new)* · `Core\CaptureService.cs`
- Delete `PlainBox` and `_rich`; single `RichTextBox`; add `FormatNotice`; Segoe UI content font;
  the load/save contract of §4.2/§4.3.
- `CaptureService`: no behaviour change required for the fix (the tolerant chain covers capture);
  this file is listed only for the RTF read comment plus the `ImageCache.Decode`-adjacent image
  thumbnail note — **if it ends up untouched, drop it from the slice** (5 → 4 files).

### Resulting line budgets

| File | Now | After |
|---|---|---|
| `MainWindow.xaml` | 517 | ~360 (+~25 for the Images pane) |
| `RowTemplate.xaml` | — | ~180 |
| `MainWindow.xaml.cs` | 350 | ~365 |
| `MainWindow.Rich.cs` | 292 | ~215 |
| `MainWindow.Keys.cs` | 355 | ~370 |
| `ClipRow.cs` | 124 | ~150 |
| `EditorWindow.xaml` | 114 | ~110 |
| `EditorWindow.xaml.cs` | 157 | ~200 |
| `HtmlToRtf.cs` | — | ~200 |
| `RtfText.cs` | — | ~30 |

`SettingsWindow.xaml` (≈29 KB) stays over the 400-line budget — pre-existing, not this wave; log it.

---

## 8. Verification hooks (each needs an observed value, not a code read)

| # | Check | Pass looks like |
|---|---|---|
| H1 | Row heights | six fixture clips (1/3/6/20 lines + file + image-in-Pinned) produce exactly 34/70/124/124/34/34 px, and `RowHeight` is identical before and after selection |
| H2 | Zero selection geometry | 20 × `↓`: `ScrollViewer.ExtentHeight` unchanged, no row's rendered `ActualHeight` changes, `UpdateLayout` per move ≤ 5 ms |
| H3 | Throughput | 1000 items, arrow keys: realised `ListBoxItem` ≤ 20; no single layout > 16 ms |
| H4 | One panel | 20 rich rows arrowing: live `HtmlPanel` count = 1; working set with popup open ≤ 200 MB, never > 250 MB |
| H5 | Images decode | first frame in Images tab = stored 96 px thumb; big preview swaps in ≤ 300 ms; UI thread never blocks > 16 ms; preview bitmap ≤ 4.2 MB |
| H6 | Images keys | `←/→` step one image, `↑/↓` step 8, `Ctrl+→` switches tab, `Enter` pastes the shown image |
| H7 | Editor, four fixtures | plain / Word RTF with Devanagari / HTML-only browser table / legacy 8-bit RTF — **all four open in the rich surface**; the third shows formatted text (cells on separate lines); the fourth shows formatted text (no silent plain fallback) |
| H8 | Editor round trip | edit → save → reopen: text and formatting identical; `items.rtf` non-null; blobs deleted; an html-origin clip keeps `items.html` |
| H9 | Copy pulse + FLIP | pulse peaks on the new row only; dedupe FLIP starts at the old measured offset, 200 ms, no jump when the container was virtualised away |
| H10 | ClearType (crisp sprint) | colour-fringed pixels > 0 in a popup capture, same method as `AUDIT-current-visual.md` (today: 0) |

`[U]` visual items stay with the fresh audit agent: row rhythm and the 34/52/… ladder, number column
alignment at 10+, badge collision, Images pane proportions, editor notice bar legibility.

---

## 9. Deviations and risks (honest list)

1. **Rich preview of a short clip is short.** A 1-line clip's rich render is an 18 px strip. That is
   the direct consequence of "height depends only on content" + "no geometry change on selection".
   The fix, if the owner dislikes it, is a separate detail pane — not a growing row.
2. **Badge copy** is `+N` + tooltip, not `"N lines not shown"` (antislop §6.2). A full sentence in
   the trailing slot pushes the badge onto the last line and re-introduces a moving element.
3. **HTML→RTF is a subset.** Tables flatten to tab-separated lines; CSS layout, backgrounds, images
   and web fonts are lost on edit. WebView2 is the only path that keeps them, and it costs a runtime
   dependency plus a second save contract (§4.1). The popup preview still shows the original HTML
   until the clip is edited; after the first save, `items.html` is the regenerated (flattened)
   document, so what the preview shows equals what the editor holds — degradation happens once, at
   the first edit, and never silently.
4. **Images tab shows 4 full tiles at a time**, not 9 — the price of a 538 × 263 preview. The
   counter and the peek of the 5th tile carry orientation.
5. **`PreviewLines` > 6 renders at 6** (range stays 1–100, frozen). The Settings hint must say so.
6. **Line count is estimated** at 62 chars/line, not measured. Estimate error moves a row by one
   18 px line, always at load, never on selection.
7. **Legacy RTF rows** already mangled by the old UTF-8 path cannot be repaired; the chain only
   stops new corruption and recovers what is recoverable.
8. **Devanagari in the popup** still renders through Manrope's per-glyph fallback (the list font is
   UI type, not content type). The editor and the rich preview use Segoe UI and are correct. Changing
   the list font for script coverage is a typography decision, not a v2 fix.
9. **Window chrome, ClearType, entrance/close** are assumed, not specified here (§5). If the crisp
   sprint changes the shell's border thickness or keeps a layered popup, §1's rails and §5's
   ClearType target break and must be re-derived, not nudged.
