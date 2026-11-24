# AGENTS.md — Clip

Owner: non-coder product owner. AI agents do all implementation; the owner judges only on-screen results.

## Project layout (map to the builder contract)

- `Clip.csproj` — WPF net10.0-windows app (UseWPF + UseWindowsForms). Packages: Microsoft.Data.Sqlite, HtmlRenderer.WPF, H.NotifyIcon.Wpf, RtfPipe, System.Text.Encoding.CodePages.
- `Core\` — business rules + persistence (domain + store): `Config` (camelCase JSON in `%APPDATA%\<AssemblyName>\config.json`), `ClipboardItem`, `ClipboardStore` (SQLite: items / tabs / item_formats), `CaptureService` (debounced capture: text / rich / files / image; dedupe; Skipped), `SearchService`, `ImageCache`, `Autostart`.
- `Interop\` — Win32 / shared infrastructure: `ClipboardListener`, `HotKeyWindow`, `ClipboardFormats` (Snapshot/Restore), `PasteInjector`, `ForegroundApp`, `ScreenDpi`, `HtmlFragment`.
- `UI\` — WPF windows + code-behind (features; NO MVVM framework): `MainWindow`, `NotificationWindow`, `SettingsWindow`, `EditorWindow` (S3), `Themes\Dark|Light.xaml` — the ONLY color homes.
- `App.xaml(.cs)` — composition root: single-instance mutex, tray, theme apply, wiring.
- `docs\` — frozen contracts + research. **`docs\CHECKLIST-features.md` and `docs\CHECKLIST-uiux.md` are the acceptance source of truth**; evidence required, no self-reported green.
- `_fonttest\` — excluded scratch experiment; never build it, never edit it.

Layering that HOLDS: `UI → Core|Interop` only; nothing imports `UI`; `App` imports everything. Known accepted deviation: `Core ↔ Interop` import each other (CaptureService uses Interop listeners; PasteInjector consumes Core models) — documented here rather than restructured (existing-project rule).

## Gates (this project's own commands)

- Compile: `dotnet build -c Release` — 0 errors, 0 warnings is the bar.
- Owner build: double-click `build-release.cmd` → one self-contained exe at `dist\Clip.exe` (no .NET runtime needed on the target machine; `dist/` is gitignored).
- Component verification: scratch harness in `%TEMP%\opencode` linking the REAL files; every `[H]` checklist item needs observed values.
- Visual verification: a FRESH `deepseek-v4.1-flash#high` sub-agent takes and reads its own screenshots; builders never mark `[U]` items.
- Pre-release: `ca-milestone-audit` dual-axis pass over both checklists.

## Known gate gaps (checked in review per ca-autonomous-builder; tooling tickets welcome)
- The mechanical stages the Node gate would enforce (hygiene greps, 12-line duplication scan, architecture import check, stale-refs, EOL) have no standing automation in this dotnet-only repo — they are performed by audit agents each milestone and their findings live in docs\AUDIT-*.md.

## Standing rules

- One slice ≤ 5 production files; sprint agents own disjoint file sets; two strikes → the slice is reset and split.
- No TODO / FIXME / stubs / `[DEBUG]` / `Console.WriteLine` in production code; no empty `catch` without a one-line comment saying why.
- No hardcoded colors outside `UI\Themes\*.xaml`. (Accepted exception, owner-pending token: SettingsWindow error red `#FF6B6B`.)
- 400-line file budget; split by feature, never by layer.
- Product rename happens in ONE place: `<AssemblyName>` in `Clip.csproj`.
- WPF binds properties, not fields. `ShutdownMode="OnExplicitShutdown"` stays (a tray app must never die with its last window).
- Business semantics live in `Core\` only — UI code-behind reads and calls, never re-implements (dedupe, cap, formats, skip rules).
