# PasteBar — research note (for Clip)

**Identity.** PasteBar, by AnotherVision LLC. UI = React 18 + **Tailwind** + Radix primitives; shell = **Tauri (Rust)** — it migrated off Electron (the "Electron" in public descriptions is stale). macOS + Windows. Version 0.7.0 (`package.json:2-3`). Source read at commit `bcad6a9` (2026-08-18), clone `%TEMP%\opencode\pastebar`.

**License — correction to the brief:** it is **NOT MIT.** `packages/pastebar-app-ui/LICENSE:1` = "FREE TO USE LICENSE, NOT FOR RESALE OR REDISTRIBUTION" (CC BY-NC, non-commercial; repo-root `CC-LICENSE`). **Do not copy code or CSS verbatim into Clip.** Ideas and measurements only.

UI root: `packages/pastebar-app-ui/src`. Renderer is a webview, so all "visual craft" is Tailwind class strings — values below are resolved from Tailwind defaults.

---

## 1. Rich text in the main list — it does NOT render rich text

- **Formatted clips are shown as their plain text**, not their HTML. Main row body = `<code className="whitespace-pre">` with the raw string, 5-line truncation + "+N lines / +N chars" toggle (`ClipboardHistoryRow.tsx:849-881`, `Dashboard/components/utils.ts:212-258`). Font is **SourceCodePro monospace, 0.875rem** (`styles/globals.css:654-660`).
- Capture side never keeps HTML at all: Rust clipboard layer reads text/image/files only; HTML survives only as an on-demand *conversion* command (html2text / pulldown-cmark, `src-tauri/src/commands/format_converter_commands.rs:125-160`).
- Rich rendering exists **only read-only**, in the code/clip viewer: markdown → HTML via `marked`, sanitised by DOMPurify (`USE_PROFILES:{html:true}`), injected with `dangerouslySetInnerHTML`, with an **HTML ⇄ Markdown toggle** (`components/code-viewer/index.tsx:51-67, 223-263`).
- **No per-item preview pane.** "Large view" (`Shift+click`) promotes one row into a wide card in place (`ClipboardHistoryLargeView.tsx:112-300`); the only true split is history panel ⟷ Paste-Menu panel (`ClipboardHistoryPage.tsx:1975-2005`).
- Their own inline markup language: **BBCode** `[copy] [mask] [blank] [hl] [h] [b] [i]` parsed into React nodes (`libs/bbcode.tsx:15-183`; `[copy]` becomes a live inline copy button, `[mask]` hides the middle of secrets).

## 2. Editor — plain textarea + BBCode toolbar, no WYSIWYG

- Clip value field is the `TextArea` molecule (auto-grow, `rows={2}`, `maxRows` 12 large-view / 6 normal, `text-sm`) (`ClipEditContent.tsx:878-898`).
- "Formatting" = a row of 17-19px lucide icons that **insert BBCode tags around the selection**: Copy, Mask, Blank, Highlight, Heading, Bold, Italic, Remove-formatting (`ClipEditContent.tsx:718-874`).
- Code clips use CodeMirror 5 via `fromTextArea` (`components/code-editor/index.tsx:2-18`). Nothing saves or round-trips HTML/RTF. **No contenteditable anywhere** (grep: 0 hits).

## 3. Images

- History row: `<img>` `max-w-full max-h-56 (224px) min-h-10 rounded-md shadow-sm border border-slate-100 / dark:border-slate-700`, `object-scale-down`, `animate-in fade-in duration-300` (`ClipboardHistoryRow.tsx:707-722`; same in quick-paste row `:519`).
- Pixels badge `{w}x{h}` bottom-left, `text-xs text-muted-foreground` (`:937-945`). Large view: `min-h-20 (80px) object-scale-down`, no zoom, no gallery, no lightbox (grep zoom/lightbox: only Radix dialog animations).
- Board clips get a manual **image scale** stepper (1x / 2x …) with a mono `Badge` `{n}x` + ZoomIn/ZoomOut 16px (`ClipCard.tsx:1227-1262`).
- No thumbnails generated: one full-res file, read and **base64-encoded through IPC** every time it is shown (`clipboard_commands.rs:136`). Watch out — do not copy this.

## 4. List anatomy

Row (one component, five visual states), `ClipboardHistoryRow.tsx:504-554`:
- Shell: `rounded-md` + `border-2` (2px, always present so selection never shifts layout) + `px-3 py-1` + `my-0.5` + `duration-300` + `hover:shadow-sm`; rest = `bg-slate-50`.
- Body: monospace text-sm, `whitespace-pre`, wrap toggle, `+N lines/chars` expander bottom-left, wrap icon.
- Bottom-right **hover toolbar** (12-14px lucide icons, gap-1, `bg-*` matching row state): time-ago → copy/paste → drag grip (16px) → ⋮ menu. Nothing but content when not hovered.
- Corner markers: favourite = 12px Star `fill-orange-400` top-right; new (<1 min) = `bg-teal-50`; pinned block sits above, `border-orange-300/50`, drop-to-pin dashed zone.
- States: selected/multi-select `bg-amber-50 border-amber-300`; **keyboard** `ring-2 ring-blue-400 ring-offset-1 scale-[.98] bg-blue-50 border-0`; just-copied `bg-green-50 border-green-600`; saved clip `bg-sky-50`; deleting `bg-red-50 border-red-400`; context-menu open `bg-slate-100`.
- Left gutter: 16px checkbox on multi-select, else a mono index `Badge` (1-0) for Alt+digit copy.
- Timestamps: centred `text-xs text-gray-400` line above rows when relative time changes (`:443-451`).
- Tabs/organisation: 41px top **NavBar** (`layout/NavBar.tsx:539`) with menubar dropdowns + collection switcher; collections = tabs, tabs/boards inside the Paste-Menu panel. Search = inline `Input` at list top, `iconLeft={<Search className="h-4 w-4"/>}`, `bg-gray-100 dark:bg-gray-700 text-md`, placeholder "Find in history…", plus a filter dropdown with per-language and per-source-app checkboxes and count badges (`ClipboardHistoryPage.tsx:2075-2090`, `ClipboardHistoryListFilter.tsx:181-297`). Search hits pulse-highlight: `bg-yellow-300` + `scale(1)→1.2→1` 0.3s (`globals.css:428-468`).
- List is virtualised (`react-window VariableSizeList` + `InfiniteLoader`, `ClipboardHistoryPage.tsx:66`).

## 5. Visual craft (actual values)

- **Window**: transparent host `p-1`, inner card `rounded-md border` (macOS `rounded-lg shadow-window`), `shadow-window = 1px 2px 3px rgb(0 0 0/.1), 0 2px 4px -1px rgb(0 0 0/.1)` (`App.tsx:718-724`, `tailwind.config.js:145-147`).
- **Radius**: `--radius: 0.5rem`; `lg=8px, md=6px, sm=4px` (`globals.css:574,607`; `tailwind.config.js:196-200`).
- **Type**: Inter (`globals.css:619-635`), scale = 14px body (`text-sm`), 12px meta (`text-xs`), 11px image dims; mono SourceCodePro 14px for content. Only 3 sizes + 1 mono — nothing between 12 and 14.
- **Colour** (HSL vars, light/dark): bg `0 0% 100%` / `217 18.41% 8.5%`; fg `222.2 47.4% 11.2%` / `213 31% 91%`; muted `210 40% 96.1%` / `223 47% 11%`; muted-fg `215.4 16.3% 46.9%` / `56.9%` (`globals.css:545-608`). Rows use the slate ramp, accents amber/sky/teal/green/orange semantically.
- **Spacing**: 8px grid; rows `px-3 py-1` (12/4px) with 2px gaps; nav 41px; search input `h-10` (or `h-8` small); toolbars `px-1`.
- **Motion**: every state change `duration-300`; image/link entrance `animate-in fade-in`; search hit 0.3s pulse; nothing bounces or slides except Radix menus (`zoom-in-90`).
- **Scrollbars**: thumbs `rounded-full bg-slate-300`/`dark:bg-slate-500`, 8px, auto-hide disabled inside clip text (`globals.css:73-92`, `CardValueViewer` options).

---

## 5 things Clip MUST steal (WPF-implementable)

1. **Semantic row states on a fixed 2px border slot** — keep `BorderThickness=2` always, change only `BorderBrush`/`Background`: normal `#F8FAFC`, selected `#FFFBEB`/`#FCD34D`, keyboard `#EFF6FF`/`#60A5FA` + `DropShadowEffect` 2px @ 12% + `ScaleTransform 0.98` (250ms), copied-flash `#F0FDF4`/`#16A34A` 900ms then fade, new `#F0FDFA`. Kills the "which item is focused?" problem and the layout jitter.
2. **Hover-revealed action strip inside the row, bottom-right, 12-14px icons** (time-ago → copy → grip → ⋮), invisible otherwise, `Opacity` 0→1 in 150ms, gap 4px, `Padding="3,1"`; row padding `12,4`, radius 6px, 2px vertical gap. Clip's list then shows content, not chrome.
3. **5-line preview + "+N lines / +N chars" expander** (`Dashboard/components/utils.ts:212-258`): preview cap `LineHeight*5`, expanded cap `200px` with scroll, expander text `12px #6B7280` — one click, in place, no modal. Gives every clip the same height so scanning works.
4. **Search-hit treatment**: match `Background=#FDE047`, then a 300ms `ScaleTransform` 1→1.2→1 pulse on the first hit + `ScrollIntoView` on it (`globals.css:458-468`). Also the inline **filter chips with counts** (language / source app) next to the search box.
5. **Inline token chips for special formats**: render Clip's rich/sensitive spans as interactive inline widgets — a "copy this" chip, a masked secret `a••••z`, a highlight chip — the way PasteBar renders `[copy]/[mask]/[hl]` (`libs/bbcode.tsx`). Concrete chrome: chip radius 4px, 12px text, purple `#F3E8FF`/`#7E22CE`; masked text `#6B7280` monospace.

## 3 anti-patterns (do not copy)

1. **Monospace everything.** PasteBar sets the row body in SourceCodePro 14px. Fine for a code-first tool, wrong for Clip's prose/rich users — body must stay Segoe UI Variable, mono only for code clips.
2. **Formatted clips degraded to plain text, with rich rendering quarantined in a read-only viewer.** If Clip stores HTML/RTF, the list must show it (WPF: `FlowDocumentScrollViewer`/`RichTextBox` read-only, or a rendered `TextBlock` from the run list) — otherwise users cannot tell two formatted clips apart.
3. **Full image base64 over IPC on every paint, no thumbnail, no lightbox.** Clip should cache a ≤224px thumbnail at capture time, and a click on the image should open a zoomable viewer (fit / 100% / 200%).

## Screenshots

- Main split view (hero): https://github.com/user-attachments/assets/c3024edb-67ae-4e92-a02f-a0957fb153a2
- Full app (1144px): https://github.com/user-attachments/assets/ce861bdb-d7df-4944-885e-12d37177e56d
- UI videos (copy/paste, board image, panel resize): https://github.com/user-attachments/assets/33a1cd09-0865-42d0-acc8-14376d8e675b · `.../a95bebec-d7a8-499a-9a43-69e2fba7a04a` · `.../d336498d-f430-43b8-9e87-a4fedeee1fae`
- Product site: https://www.pastebar.app
