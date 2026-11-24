# AUDIT — TipTap editor port (F2)

Date: 24 Nov 2026. Owner decision: the F2 editor moves from Syncfusion `SfRichTextBoxAdv` to TipTap in
WebView2 (MIT, no key). Handoff: `docs\HANDOFF-tiptap-port.md`. Evidence: `docs\evidence-tiptap\`.

**Shipped in three port commits plus one review commit, not the handoff's four.** Slice 1 (host) and
slice 3 (toolbar) are one compile unit — `EditorWindow.Toolbar.cs` called the Syncfusion control
directly, so removing the package could not build until the toolbar was ported too. The commits, each
gated on `dotnet build -c Release` = 0 errors / 0 warnings:

| Commit | What |
|---|---|
| `9c3ada2` | `Assets\editor\` page asset (TipTap **3.35.0** bundle, HTML, CSS), `scripts\build-editor.cmd`, `scripts\editor\` node project, csproj resource wiring + WebView2 package. Nothing consumed yet. |
| `dcbc1cd` | The port: `EditorWindow.xaml`, `EditorWindow.xaml.cs`, `EditorWindow.Toolbar.cs`, new `EditorRtf.cs`, Syncfusion package removed. |
| `e8b0e2b` | Audit + the first evidence pass + ledger/state/checklist updates. |
| this commit | Visual-review fixes (band contrast, scrollbar, selection, focus state, footer keycaps, spacing) + the refreshed evidence they produced. |

Syncfusion is gone from the tree (`grep -i syncfusion UI Core Interop` → 0); the package's ~30 MB of
assemblies no longer ship.

## T1 — Build

`dotnet build -c Release` after each commit:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## T2 — Plain clip opens, caret at end, Esc prompt

- `t2-plain.png`: a 5-line plain clip opens as paragraphs, caret at the end (`focus('end')`), toolbar
  reports size 16, no formatting claims.
- `t2-esc-prompt.png`: one typed character makes the clip dirty; Esc shows **Discard changes?** and the
  clip stays readable behind it.

WPF cannot draw over a WebView2 child window. The first build hid the browser surface, which blanked
the clip the question is about (seen in the first evidence pass and fixed the same session). The
prompt now lives in **its own grid row above the editor**, so nothing is hidden and no overlay trick
is needed.

## T3 — 24 KB Edge/ChatGPT clip, dark source

Clip **362** in the real history (24 235 bytes CF_HTML, captured from Edge with DarkReader). It carries
its colours and bands on block elements (`<h2 style="color: rgb(253,251,247); background-color: rgb(3,6,7)">`),
not on spans — TipTap's `TextStyle` alone would have dropped them.

- `t3-dark-edge.png`: headings, bold, lists and the per-block dark bands render; the light ink stays on
  its own dark band ("white-on-dark stays on dark"). The source's own colours are kept where they are
  readable on their own band.
- **Band guard, corrected after the visual review.** The first pass compared every run against the page
  canvas and skipped anything sitting on a source band. The ChatGPT clip has a black `<li>` band whose
  two sentences carry the source's own `#14181F` ink — 1.1:1, invisible, and the skip hid it. The guard
  now compares each run against its own span background, else its nearest ancestor block band, else
  the canvas; below 2.5:1 it takes whichever end of the theme (ink or canvas) reads better on that
  band. White-on-dark is untouched; dark-on-dark becomes light-on-dark.
- Parse result: 2 634 characters of clean HTML, 110 style attributes in, structure out.

## T3b — Long clip: scrollbar styled to the field

`t3b-long.png`: a 20-paragraph clip with a list and a table scrolls inside the rounded field with a
10 px transparent track and a rounded ink-18 % thumb — no Chromium stepper boxes, no white track
cutting the field's corner. Scrolling lives on the sheet, not the page body, so the canvas reaches the
field edge.

## T4 — Word-style RTF-only clip

WPF has **no** HTML export (`TextRange.Save(DataFormats.Html)` throws — verified), so RTF-only clips go
through `UI\EditorRtf.cs`: WPF's own RTF parser is walked once and written as the small HTML vocabulary
the page speaks (p / ul / ol / table, spans for local weight, style, decorations, colour, background,
size, family). 239 lines with documentation; no new dependency.

- `t4-rtf.png`: a seeded Word-style RTF (no HTML on the clip) shows the heading, **bold**, *italic*,
  underline, accent colour, two bullet levels and two font sizes.
- The two real RTF-only clips in the history (343, 346) also open without the fallback notice.
- Fidelity ceiling, stated plainly: spacing, tables' column widths, images and Word styles do not
  survive. The old Syncfusion editor had the same ceiling for RTF bodies (it used the same WPF parser
  for row previews).

## T5 — Edit → save → reopen → row preview → paste into Word

Clip 362, edit "Select all → Italic", Save changes. All work happened on a **copy** of `clip.db`
(`%TEMP%\opencode\evidence-tiptap\clip-copy.db`); the owner's database was never written.

- `t5-edited.png`: the edit before saving.
- `t5-reopened.png`: reopening the clip shows every block italic, the bands intact and the I button
  pressed — the round-trip through the store and the page works.
- `t5-list.png`: the main list's row preview for the same clip now renders from the saved HTML, with
  the RTF-seeded clip beside it as a control.
- Store after save (`GetRich` / `GetText`): `htmlLength=4459`, `rtfNull=true`, `italic=true`,
  `textLength=600`. The 610 → 600 character difference is ten CRLF pairs becoming the editor's `\n` —
  the same normalisation the old editor performed.
- **HTML is stored as a Windows "HTML Format" payload** (Version/StartHTML/…/EndFragment with UTF-8
  byte offsets), not bare `<p>` markup, because that is what Word, Chrome and Excel read on paste-back
  and what captured clips already carry. `Interop\HtmlFragment.Extract` stays the only reader.
- `t5-word-paste.png`: the saved clip put on the clipboard the way the row's Copy does, then pasted
  into a real Microsoft Word document. Structure, bold/italic and bullets survive; **Word's default
  "Merge Formatting" drops the source's dark bands and colours** — that is Word's paste mode, not a
  Clip change. The clipboard read back intact: `readBackHtml=4459 hasColour=True hasBand=True
  hasHeader=True`. Choosing Paste → Keep Source Formatting in Word keeps the bands.

## T6 — Toolbar

Each control was clicked through the real WPF button/popup against its own paragraph
(`t6-tools-before.png` → `t6-tools-format.png`):

| Control | Result |
|---|---|
| Bold / Italic / Underline / Strike | applied to the selected paragraph only |
| A+ / A− | 16 → 18, the size label follows the caret |
| Colour swatch (Accent) | paragraph recoloured, the colour bar under "A" shows it |
| Highlight (Amber) | fill applied with canvas-coloured ink so it reads in both themes |
| Clear | removes the bold+underline the eighth paragraph started with |
| Single line | 10 blocks joined into one paragraph, one space between; every mark survives (`t6-tools-single.png` shows bold, italic, underline, strike, 18 px, accent and amber all kept) |
| Aa | lowercase → UPPERCASE → Title Case (`t6-tools-case-lower/upper/title.png`), minor words stay lowercase |

Buttons reflect the caret: the page posts `{type:"sel", …}` after every selection change and every
command, and the toolbar mirrors bold/italic/underline/strike/size/colour/highlight.

## T7 — Open time and memory

Measured by the harness on this machine (2-vCPU Windows 11 VM, display at 150 %, software rendering;
the WebView2 runtime is 158.0.4370.33):

| | Cold (first open in a process) | Warm (second open, same process) |
|---|---|---|
| Browser core ready | 478 ms | 293 ms |
| Page ready (clip on screen) | **2 923 ms** | **2 706 ms** |

Memory while one editor is open: **+302 MB across +6 `msedgewebview2` processes**; after closing, back
to the machine's baseline (362.1 MB / 6 processes belong to other Windows apps that use WebView2).
Nothing leaks: the browser processes exit within ~3.5 s of the window closing.

Both numbers are worse than the handoff's estimates (60–90 MB) and much worse than the Syncfusion
editor's ~50–250 ms open. **Owner decision needed**, not a silent trade:

1. accept it and show a "Loading editor…" line in the page while it loads;
2. keep one hidden editor window alive after first use — removes the repeat wait but costs ~300 MB
   permanently on this VM;
3. pre-create only the environment at app idle — saves ~130–460 ms of ~2 900 ms, not worth it alone.

No mitigation was shipped in this port.

## T8 — DPI

- The machine runs at **150 %** (a 640×600 DIP window is 960×900 physical pixels; `t8-dpi-150.png`).
  The window, toolbar, glyphs and page all scale correctly at 150 %.
- **100 % and 200 % could not be produced here.** This VM exposes one 96-DPI virtual display and the
  documented scale API (`DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE`) crashed the calling process without
  changing the scale. The 100 % and 200 % screenshots therefore need a machine with a changeable
  scale; the app is declared PerMonitorV2 (`app.manifest`) and WebView2 scales natively.

## Visual review (fresh agent, before these fixes)

A fresh `deepseek-v4.1-flash#high` agent took its own shots plus zoom crops and judged them against
`docs\DESIGN-language.md` and `docs\CHECKLIST-uiux.md` §0. It rejected the first port: "No — not while
a band of text on the real ChatGPT clip is 1.1:1 and long clips reveal a stock scrollbar." That
verdict produced this commit:

| Found | Fix |
|---|---|
| Bullet band of clip 362: `#14181F` on `#030607`, ≈1.1:1 | The guard compares against the band (T3); the same shot is now light-on-dark |
| Stock Chromium scrollbar (steppers, white track) inside the rounded field | Thin ink-18 % thumb, transparent track, no buttons; scrolling moved onto the sheet (T3b) |
| Browser-blue text selection, the only blue on the surface | Neutral ink-20 % selection |
| Field focus ring never appeared (`IsKeyboardFocusWithin` is dead for a WebView2 child) | Page posts `focus`/`blur`, the host sets the border; `Web.Focus()` after seeding makes it deterministic |
| Footer "Ctrl+S saves" a plain 10 px left-aligned meta line | `Esc` / `Ctrl+S` Keycaps, right-aligned (DESIGN-language grammar) |
| B/I/U/S bordered buttons 2 DIP apart, a scalloped chain | 4 DIP between them, 10 DIP to the divider |
| Close icon stroke 2 (rule is 1.5) | 1.5 |
| Discard banner radius 10 (card grammar is 12) | 12 |
| Audit claimed "Single line keeps marks" while the harness cleared every mark before joining | Harness no longer clears; the new shot proves bold/italic/underline/strike/size/colour/highlight survive. The command was already correct. |

Accepted nits, not changed: h2 and h3 differ by 1 px; the discard banner leaves its left half empty;
the footer Cancel/Save stay live behind the banner; the blockquote rule is faint at 20 % ink.

All 16 shots in `docs\evidence-tiptap\` are from the fixed build. The judgement gap that remains is
the dark theme: every capture is the light theme.

## Deviations from the handoff (and why)

1. **Four slices → three commits** (compile unit, above).
2. **`SetVirtualHostNameToContentFolder` → embedded resources + `WebResourceRequested`.** The owner
   build is one self-contained exe; a loose `Assets\editor` folder would either break that exe or
   vanish from it. The fonts are served from the existing pack resources. Resource names are
   `clip.assets.editor.html/.css/.bundle.js`.
3. **Esc / Ctrl+S / F2 are caught in the page and posted as `cancel` / `save`.** A WebView2 child owns
   the keyboard; `OnPreviewKeyDown` cannot see keys typed into it. The WPF path still handles the same
   keys when focus is on the chrome. Same behaviour as before.
4. **The page follows the theme** (`ContentCanvas` / `ContentInk` / `ContentLink` via the URL) instead
   of the handoff's "white Surface". A fixed white sheet makes a Word clip's black text unreadable in
   the dark theme and makes the toolbar's theme text colours meaningless. To keep both directions
   readable the page runs the same rule the row renderer already uses: a run whose own colour stays
   under 2.5:1 against the canvas (and that is not sitting on its own band) gets the theme ink.
5. **RTF-only clips convert instead of degrading to plain text** (the handoff allowed either; the
   writer is ~60 lines of logic over WPF's parser).
6. **HTML is preferred over RTF on load** (handoff §3.1), the reverse of the Syncfusion editor's order.
7. **Save writes the CF_HTML envelope** (above), not raw TipTap HTML — otherwise Word's paste-back
   would receive a headerless "HTML Format" payload.
8. **Discard prompt is a banner row**, not a floating card (airspace, above).
9. **`_ready` is set only after the clip has been seeded into the page.** A save that arrived between
   the page's `ready` and the seed would have written an empty document over a rich clip.
10. **Highlight ink is the canvas colour** (white on the light theme's dark fills, dark on the dark
    theme's pastels) instead of `OnAccent`, which was 1.2:1 on the light theme's amber.
11. **TipTap 3.35.0**, the current release, not QuickNotes' 3.27.1. The wiring is copied from
    `QuickNotes\src\tiptap-editor.js`; the changelog between the two versions is additive for every
    API used here, and the bundle is re-checked in a browser, not assumed.
12. **The evidence harness is a separate scratch WPF project** (`%TEMP%\opencode\evidence-harness`)
    that references `Clip.csproj` and opens the real `EditorWindow`, instead of the handoff's temporary
    `App.OnStartup --test-editor` branch. Nothing had to be added to or reverted from `App.xaml.cs`,
    and the harness can drive the real buttons/popups. Capture is a DPI-aware screen grab, because
    `RenderTargetBitmap` cannot see the WebView2 child window (a `RenderTargetBitmap` shot would show
    the chrome and an empty content area — the handoff's plan would have produced exactly that).
13. **The field's focus ring is driven by a page message**, not `IsKeyboardFocusWithin`: the WPF
    property never sees the WebView2 child's focus. The page posts `focus`/`blur`; the host sets the
    border. The same reason the keys had to move into the page (deviation 3).

## Not proven here (owner or a machine with different hardware)

- DPI 100 % / 200 % screenshots.
- The "WebView2 runtime missing" notice (the runtime is installed on this machine; the catch path is
  code-reviewed, never executed).
- The 2 MB HTML oversize notice (>2 MB clips are rare; the branch is unchanged from the old editor).
- Colour emoji and Devanagari inside the editor (the row preview tests still cover the store).
- Word paste with **Keep Source Formatting** (Merge Formatting drops colours; the clipboard payload was
  read back and does carry them).

## Reproduction

1. `scripts\build-editor.cmd` rebuilds the page asset (needs Node; the C# build never does).
2. Copy `%APPDATA%\Clip\clip.db` (plus `-wal` / `-shm` if present) to `%TEMP%\opencode\evidence-tiptap\`
   with Clip closed.
3. The harness (`%TEMP%\opencode\evidence-harness`, a scratch WPF project that references the real
   `Clip.csproj`) opens the real `EditorWindow` on that copy, drives the real WPF controls and the real
   page, and captures each window with a DPI-aware screen grab (`RenderTargetBitmap` cannot see a
   WebView2 child window). It seeds the plain / formatting / RTF / long clips and never writes the real
   database.
4. `run-evidence.ps1` runs T2–T8 (T3b is the long clip); `paste-word.ps1` does the Word paste.
