# AI_LEDGER.md — Clip

## Owners (search here before creating anything new)

| Owner | What it is for |
|---|---|
| `Interop\HtmlFragment.Extract(string cfHtml)` | The ONLY correct CF_HTML splitter — offsets are UTF-8 byte offsets, never slice the string |
| `Core\ImageCache.Write/Cleanup` | F2 cache: `%APPDATA%\<Product>\cache\images\clip-<id>.png`, write-if-changed, startup cleanup >7d / newest 100 |
| `Interop\ClipboardFormats.Snapshot/Restore` | All-format fidelity; standard ids → canonical names; FileDrop decoded to paths; DIB raw bytes; `WithClipboardRetry` = the ONE 5-attempt/30ms clipboard-busy policy (read + write) |
| `Interop\PasteInjector` | 5-attempt/30ms clipboard-busy retry lives in `ClipboardFormats.WithClipboardRetry`; modifier key-UPs before Ctrl+V; image branch BEFORE formats branch; CopyAndPastePlain strips formats |
| `Interop\ScreenDpi.Scale(Screen)` | px→DIP for any monitor placement math |
| `Core\Config` | camelCase JSON, `%APPDATA%\<AssemblyName>\config.json`; `WasNew` is [JsonIgnore] |
| `UI\Themes\Dark|Light.xaml` | UiFont, App, Surface, Raised, Fg, Muted, Accent, Hairline (+ sage/amber/sky/violet to land with S3 per Ortu). No other color homes. |
| `Core\ClipboardStore` | items/tabs/item_formats; `EnforceCap`/`Clear` scoped `tab_id IS NULL`; `TryDedupeMoveToTop` main-history scope only, images via indexed `image_hash` (byte compare still decides); `InsertWithFormats` = the ONE atomic capture write; `GetRich` = html/rtf on demand (the list query never loads them); `Restore` = undo primitive (same id/tab/formats/timestamp, one transaction) |
| `UI\EditorWindow` + `Assets\editor\` | F2 editor: WPF chrome/toolbar + ONE TipTap page in WebView2. Page API is `window.clipBridge` (`load`, `loadPlain`, `getHtml`, `getText`, `run`, `focus`) and it posts `ready`/`dirty`/`sel`/`save`/`cancel`. The host serves the three embedded assets + the two Inter faces over `https://clip/` (`WebResourceRequested`), never from disk |
| `UI\EditorRtf.ToHtml` | RTF-only → HTML, because WPF has no HTML export; walks the FlowDocument its own RTF parser builds |
| `scripts\editor` + `scripts\build-editor.cmd` | The page's npm project (TipTap 3.35.0, esbuild IIFE). `Assets\editor\editor.bundle.js` is the committed build output — never hand-edited, never required by the C# build |
| `CaptureService.Skipped` | human-readable skip reasons → toast (S3 wires) |

## Traps (do not repeat)

- WPF binds properties only — public fields on models render blank (S0, screenshotted).
- Default `ShutdownMode` kills a tray app when the toast closes → `OnExplicitShutdown` (S0).
- A modal MessageBox inside `OnStartup` blocks tray creation → defer via Dispatcher (S0).
- CF_HTML offsets are UTF-8 BYTE offsets; a sloppy producer may split a multibyte char — degrade to clean prefix, never throw (spike).
- 24-bpp DIB rows are DWORD-stride aligned; use `DataFormats.Dib` (id 8), not the "DeviceIndependentBitmap" string, which registers a custom format (image sprint).
- Still holding the hotkey's Alt turns injected Ctrl+V into Ctrl+Alt+V → always send modifier key-UPs first (core-fix). Same Alt also opens the focused window's menu on the next key (Alt+V then Down showed *that* window's Close/Minimise menu): `HotKeyWindow` now calls `PasteInjector.ReleaseHotkeyModifiers` inside WM_HOTKEY, which taps unassigned VK 0xE8 (AutoHotkey's menu-mask key) before the modifier releases — without the tap the bare Alt release IS the menu command.
- Office/browser copies arrive half-published → debounce capture ~100 ms; re-check suppression inside the debounced path (core-fix).
- Shell screenshots are DPI-unaware → `SetProcessDpiAwarenessContext(-4)` before CopyFromScreen (S0).
- On .NET, `Assembly.Location` is the DLL → autostart and any "exe path" need `Environment.ProcessPath` (S2).
- Creating a new NotificationWindow per capture leaks ~35–40 MB → one reused window (S0 measurement; S3 must-fix).
- `HotKeyWindow` parses with `Enum.TryParse<Keys>` — friendly names like "1" resolve to mouse buttons; explicit VK mapping required (S2 finding; S3 must-fix).
- A running stale Clip instance owns Alt+V → stop it before testing any new build.
- SQLite string compare is BINARY collation — that IS the exact-match guarantee; never add NOCASE/trim to dedupe paths (dedupe sprint).
- `captured_at` ordering is LEXICAL: every writer must use `ClipboardItem.Stamp()` (`yyyy-MM-ddTHH:mm:ss.fff`). A second-resolution stamp made a same-second re-copy land BELOW the copy it replaced (Axis-1 D1).
- `items.pinned` is retired — the migration clears it and nothing reads it again; `tab_id` is the only pin state (Axis-2 A2-04).
- Manrope statics: GWfH-instanced files ship with the SOURCE family names ("Manrope ExtraLight") — always rewrite name IDs 1/3/4/6/16/17 + checksums or WPF falls back to Arial silently (font sprint).
- `new FontFamily("pack://application:,,,/Assets/Fonts/#Manrope")` IN CODE silently resolves to Arial with zero glyph typefaces — only the XAML FontFamilyConverter path (StaticResource UiFont) or `new FontFamily(new Uri("pack://..."), "./#Manrope")` work (font sprint).
- `AllowsTransparency="True"` makes a window LAYERED, and Windows never renders ClearType on a layered window (0 vs 1,026 colour-fringed pixels, same string). The reading windows stay opaque — rounded corners + shadow come from `Interop\WindowChrome.Apply` (DWM). The notification-preview ghost stays layered on purpose (no reading text).
- A cast effect (`DropShadowEffect`) anywhere above text re-rasterises the subtree and silently takes ClearType away again; a themed sibling or the DWM shadow is the substitute (crisp-text sprint).
- A local `x:Key` in a Window's own resources SHADOWS a theme brush of the same name for `{DynamicResource}`: SettingsWindow's `x:Key="Field"` style fed a `Style` into `Border.Background` and killed the app the moment Settings opened. Keep local style keys distinct (`FieldBox`).
- `WM_NCHITTEST`/`HTTRANSPARENT` click-through needs no layered window: verified on the opaque toast with a raw `SendMessage` (body → -1, ✕ → 1). `WindowFromPoint` ignores HTTRANSPARENT, so it always names the toast — use WM_NCHITTEST as the proof.
- A WebView2 child HWND owns the keyboard, so `Window.OnPreviewKeyDown` never sees Esc / Ctrl+S / F2 typed into the page — the page posts `cancel` / `save` instead (TipTap port). WPF also cannot draw over that HWND: the discard prompt is a banner row, never an overlay. Focus is the same story: `IsKeyboardFocusWithin` stays false, so the page posts `focus`/`blur` and the host paints the field ring.
- The editor's contrast guard must compare a run with its own band, never the page canvas: a DarkReader clip can put `#14181F` ink on its own `#030607` `<li>` (1.1:1). Skipping every backgrounded block hides exactly that. Compare against span background → nearest ancestor band → canvas, then take the theme end that reads.
- A WebView2 page shows a stock Chromium scrollbar (steppers, white track) unless `::-webkit-scrollbar` is styled; scroll on the rounded sheet, not the body, so the canvas reaches the field edge.
- A single-file exe cannot ship a loose asset folder: the editor page is three `EmbeddedResource`s served by `WebResourceRequested` over `https://clip/`, and the fonts come from `pack://application:,,,/<ownAssembly>;component/Assets/Fonts/...` (the `;component` name must come from the code's own assembly, or a different entry assembly silently falls back to Segoe).
- `DataFormats.Html` consumers (Word, Chrome, Excel) expect the **CF_HTML envelope with UTF-8 byte offsets**. Storing bare `<p>` markup breaks Word paste-back; the editor writes the envelope on save and `HtmlFragment.Extract` stays the only reader.
- `TextRange.Save(stream, DataFormats.Html)` throws `ArgumentException` — WPF has no RTF→HTML export. `UI\EditorRtf` walks the parsed document instead.
- TipTap is the editor of record, so a save normalises: `<b>`→`<strong>`, classes/comments dropped, text CRLF→LF — the same normalisation the previous editor performed, but the stored html gets much smaller than the captured CF_HTML.
- WebView2 budget measured on this VM: ~2.9 s to first content (cold 2914 ms, warm 2734 ms) and +302 MB / +6 browser processes while one editor is open; it all exits ~3.5 s after close. Do not promise a Syncfusion-speed F2 open.
