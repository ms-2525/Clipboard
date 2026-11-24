# STACK decision — Clip v2 (UI replatform)

Date: 25 Nov 2026. Status: **accepted** by the owner's priorities: feature completeness and Windows
clipboard fidelity first; memory is a ceiling, not the goal; the UI must be excellent and adapt to DPI
and user zoom. This supersedes the stack bake-off in chat.

## Decision

| Layer | Choice |
|---|---|
| Native host | **.NET 10 WPF thin shell**, existing `Core\` + `Interop\` kept byte-for-byte |
| Presentation | **ONE warm WebView2** running **React 19 + TypeScript + Vite** |
| Styling / components | **Tailwind CSS + Radix primitives + shadcn-style owned components**, tokens from `docs\DESIGN-language.md` |
| List | **TanStack Virtual** (windowed rows, recycling) |
| Editor | **TipTap 3** page, reused and routed inside the same WebView2 |
| State | Zustand for UI state; typed bridge for data |
| Data | SQLite stays in C#; the page never opens the DB |
| Bridge | `Clip.Host`: typed JSON requests/commands/events; generated TS types |
| Native-only | tray, toast, tooltips outside the window, hotkeys, clipboard formats, paste injection, window positioning/DPI |

The Alt+V list, Settings, the editor and the component gallery are **routes in one page**. There is no
second WebView2, no browser start on F2, and no webview on the cold popup path beyond the one warm
surface created at login.

## Why this stack

1. **Feature completeness is the product.** `Core\` + `Interop\` (2,818 lines) already handle CF_HTML,
   RTF, CF_HDROP, CF_DIB, arbitrary named formats, SendInput paste-back, focus return, dedupe, cap,
   image hashing, SQLite, autostart and single instance. None of it is rewritten.
2. **Development speed.** ~67% of the code is WPF UI; only that layer is replaced. A Rust/Electron
   rewrite drops reusable code to ~16% and re-implements the hardest layer first.
3. **UI quality and customization.** React + Tailwind + Radix gives a real design system: tokens,
   components, dark/light, responsive layout, accessible focus, zoom, motion. WPF XAML cannot match
   the iteration speed or the available component ecosystem.
4. **Memory is met by architecture, not by cutting features.** One WebView2 instead of today's
   WPF list + second editor WebView2; virtualization keeps the DOM small; images load on demand.
   The old 150 MB / 250 MB gate is superseded by the owner's 500–600 MB ceiling.

Rejected, with the deciding reason:

| Option | Why not |
|---|---|
| Tauri 2 + React | Official clipboard plugin cannot read HTML/RTF/files or monitor; needs a Rust rewrite of the tested clipboard layer. On Windows it uses the same WebView2 and is not lighter than Electron. Toolchain adds ~7 GB. |
| Electron + React | Same Chromium cost, and every native clipboard/paste line is lost. CF_HDROP/named formats and SendInput are the product. |
| WinUI 3 + RichEditBox | Low RAM, but RichEditBox cannot faithfully load captured HTML (no HTML read path); the editor requirement fails. |
| Avalonia | No free HTML-capable rich editor; Pro editor is import-only and €899/seat. |
| Photino / Blazor Hybrid | Fewer batteries than WPF, more host work; no advantage. |
| Keep WPF UI, warm editor only | Lowest risk, but leaves the UI in XAML; owner wants a proper, AI-editable UI. |

## Architecture

```
App.exe (WPF, .NET 10)
├─ Core\        store, capture rules, search, config, image cache, RTF/HTML helpers   (unchanged)
├─ Interop\     clipboard formats, listener, paste, hotkeys, tray, DPI, window chrome (unchanged)
├─ Host\        Clip.Host bridge: queries, commands, events, typed DTOs             (new, thin)
└─ UI\
   ├─ ShellWindow.xaml        one window host, WebView2, drag/resize, hotkey show
   ├─ ToastWindow.xaml        native click-through toast                     (unchanged)
   └─ web\                    React app, embedded resources, served over https://clip/
      ├─ routes: #/list  #/settings  #/editor/:id  #/gallery
      ├─ components: ClipRow, TabStrip, Search, Keycap, Dialog, Editor, Toolbar
      └─ theme: CSS variables for Light/Dark + content layer
```

- The WebView2 is created **hidden at startup after the tray icon exists**, loads `#/list`, and is never
  destroyed. Alt+V shows the window; F2 swaps the route to `#/editor/:id` in the same page.
- The page has no Node, no DB, no network. Captured HTML is sanitized before it touches the DOM
  (DOMPurify + a strict allow-list); remote images never load.
- All clipboard writes/pastes go through `Clip.Host` by item id; JS `navigator.clipboard` is never
  used for payloads. This preserves suppression and format fidelity.
- Native tooltip/toast stay native because WPF/WebView2 airspace makes overlay windows unreliable.
- The current WPF UI remains on a branch/worktree until the web list passes the feature gates.

### Bridge surface (initial)

Queries: `getRecent`, `getTabItems`, `listTabs`, `counts`, `getText`, `getRich`, `getImage`, `getThumb`,
`getFormats`, `searchFull` (request id), `getConfig`.
Commands: `paste(id, plain)`, `copy(id)`, `delete(id)`, `restore(id)`, `togglePin(id)`,
`moveToTab(id, tabId)`, `createTab/renameTab/deleteTab`, `clear`, `saveSettings`, `saveEditor`,
`openExternal(kind)`, `setZoom`.
Events: `captured`, `skipped`, `settingsSaved`, `themeChanged`, `hotkeyFailed`, `corruptionRecovered`,
`selectionRestore`.

## DPI and zoom (must-pass)

- Host manifest stays **PerMonitorV2**; the WebView2 scales with the monitor. CSS px = DIP.
- The UI uses **no fixed pixel canvas**: flex/grid, `clamp()`, min/max widths, `ResizeObserver` for the
  virtualizer, line-clamp for 3-line previews, and row-height measurement at the current zoom.
- **User zoom**: 80 %–150 % via WebView2 `ZoomFactor`, persisted in `Config.UiZoom`; Ctrl+Plus /
  Ctrl+Minus / Ctrl+0 in the page; layout reflows (not a bitmap scale). Settings exposes it.
- Verification matrix: system DPI **100 / 150 / 200 %** × zoom **100 / 125 / 150 %**, plus a narrow
  (480 DIP) and wide (900 DIP) window. This VM can only prove 150 % system DPI; the owner or a real
  desktop verifies the rest. Fresh-agent visual checks at each stop.
- Native toast/tooltip positioning keeps the existing `ScreenDpi` math; the web content never
  computes screen coordinates.

## Performance budgets

| Metric | Budget |
|---|---|
| Warm Alt+V show → first paint | **< 150 ms** |
| First Alt+V after login (surface pre-created) | **< 150 ms** |
| Hidden idle total (host + WebView2) | **≤ 550 MB**, target 450 |
| Shown list total | **≤ 600 MB** |
| Editor route open total | **≤ 600 MB** |
| 1,000-row scroll | 60 fps, visible rows only |
| Search keystroke → list swap | < 50 ms (120 ms debounce) |
| Editor open | instant (route swap, no page load) |

If a budget is missed, optimize the DOM, image loading or cache layout — **do not cut features**.
Report the measured number and the trade-off to the owner.

## Phases (each gated, current app remains runnable)

0. **Warm-surface spike** (scratch, no repo rewrite): shared React UI in a WPF WebView2, hidden at
   startup; measure show latency, hidden/shown RAM, zoom. Gate: budgets above.
1. **Bridge + read-only list**: `Clip.Host`, `#/list` renders real recent items, tabs, counts; native
   paste still works from the page.
2. **Interactions**: selection, keyboard matrix, search, delete/undo, pin/move, context menu,
   empty states, live capture updates.
3. **Rich rows**: text/HTML/RTF/files/images, sanitization, 3-line clamp, +N, time sections, gallery.
4. **Settings + editor**: settings routes, hotkey capture, theme/zoom, editor route + TipTap toolbar,
   CF_HTML save, discard prompt.
5. **Native edges**: toast/tooltip, tray routing, DPI 100/150/200, zoom matrix, accessibility, IME.
6. **Cutover**: feature checklist, audit, owner acceptance; archive the WPF UI.

## Non-goals

- Cross-platform (Tauri/Electron) until Windows is perfect.
- Rewriting `Core\`/`Interop\` or the SQLite schema.
- A second web surface, or a browser on the cold popup path.
- Chasing the old 150/250 MB gate at the cost of features.

## Open risks

1. Warm show may exceed 150 ms on the 2-vCPU VM; measure before committing the UI rewrite.
2. WebView2 airspace: no WPF overlay over the page; use in-page dialogs or native windows.
3. IME composition and the Esc cascade move page-side; the existing matrix must be re-proven.
4. HTML sanitization is new attack surface; strict allow-list, no network, CSP.
5. Memory may land at 550–650 MB; if so, the owner chooses between the ceiling and a native list —
   not a silent feature cut.
6. WebView2 runtime missing on old Windows 10; keep the existing notice and bootstrapper path.
