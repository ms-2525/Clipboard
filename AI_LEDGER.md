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
| `Core\ClipboardStore` | items/tabs/item_formats; `EnforceCap`/`Clear` scoped `tab_id IS NULL`; `TryDedupeMoveToTop` main-history scope only |
| `CaptureService.Skipped` | human-readable skip reasons → toast (S3 wires) |

## Traps (do not repeat)

- WPF binds properties only — public fields on models render blank (S0, screenshotted).
- Default `ShutdownMode` kills a tray app when the toast closes → `OnExplicitShutdown` (S0).
- A modal MessageBox inside `OnStartup` blocks tray creation → defer via Dispatcher (S0).
- CF_HTML offsets are UTF-8 BYTE offsets; a sloppy producer may split a multibyte char — degrade to clean prefix, never throw (spike).
- 24-bpp DIB rows are DWORD-stride aligned; use `DataFormats.Dib` (id 8), not the "DeviceIndependentBitmap" string, which registers a custom format (image sprint).
- Still holding the hotkey's Alt turns injected Ctrl+V into Ctrl+Alt+V → always send modifier key-UPs first (core-fix).
- Office/browser copies arrive half-published → debounce capture ~100 ms; re-check suppression inside the debounced path (core-fix).
- Shell screenshots are DPI-unaware → `SetProcessDpiAwarenessContext(-4)` before CopyFromScreen (S0).
- On .NET, `Assembly.Location` is the DLL → autostart and any "exe path" need `Environment.ProcessPath` (S2).
- Creating a new NotificationWindow per capture leaks ~35–40 MB → one reused window (S0 measurement; S3 must-fix).
- `HotKeyWindow` parses with `Enum.TryParse<Keys>` — friendly names like "1" resolve to mouse buttons; explicit VK mapping required (S2 finding; S3 must-fix).
- A running stale Clip instance owns Alt+V → stop it before testing any new build.
- SQLite string compare is BINARY collation — that IS the exact-match guarantee; never add NOCASE/trim to dedupe paths (dedupe sprint).
