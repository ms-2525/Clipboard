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

The Alt+V list, Settings and the component gallery are **routes in one page**; the F2 editor is an
**in-document overlay**, not a navigation. Navigating to a separate editor route costs ~2.2 s; the
overlay paints in ~41 ms. There is no second WebView2, no browser start on F2, and no webview on the
cold popup path beyond the one warm surface created at login.

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
      ├─ routes: #/list  #/settings  #/gallery
      ├─ editor: in-document overlay over #/list (kept warm after first open)
      ├─ components: ClipRow, TabStrip, Search, Keycap, Dialog, Editor, Toolbar
      └─ theme: CSS variables for Light/Dark + content layer
```

- The WebView2 is created **hidden at startup after the tray icon exists**, loads `#/list`, and is never
  destroyed. Alt+V shows the window; F2 opens the editor overlay in the same document.
- Cold start is ~4.2 s on this VM (window 1.7 s, WebView2 ready 4.2 s). The tray icon must appear first;
  the surface is pre-created at login so the first Alt+V is warm. If the hotkey is pressed while the
  surface is still loading, show the native window with a lightweight native "Loading editor…" line
  until the page's first paint.
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
- **Spike result (25 Nov, final):** the responsive rebuild passes with **zero horizontal overflow** at
  CSS widths 900 / 720 / 600 / 480 / 430 and at WebView2 `ZoomFactor` 1.25 / 1.5. At 1.5
  (600×466 CSS) Save/Cancel and the settings actions are fully on-screen; the toolbar wraps to two
  rows. The original 900 px `min-width` was the only cause of the first failure; the final UI inherits
  the no-min-width rule.
- Native toast/tooltip positioning keeps the existing `ScreenDpi` math; the web content never
  computes screen coordinates.

## Performance budgets

| Metric | Budget | Measured (25 Nov spike) |
|---|---|---|
| Warm Alt+V show → first paint | **< 150 ms** | **28.7 ms median / 35.8 ms p90** |
| First Alt+V after surface ready | < 150 ms | 31.2 ms first show |
| Hidden idle total (host + WebView2) | ≤ 550 MB private | **248 MB private** / 491 MB WS |
| Shown list total | ≤ 600 MB private | **363 MB private** / 611 MB WS |
| Editor open total | ≤ 600 MB private | **421 MB private** / 698 MB WS, 662 MB WS after trim |
| Editor overlay paint | < 150 ms | **72.6 ms** at 1.0; 35–50 ms at 1.25/1.5 |
| Warm show at zoom 1.25 / 1.5 | < 150 ms | 19.7 / 21.6 ms median |
| 1,000-row scroll | 60 fps, visible rows only | 8–20 row elements |
| Search keystroke → list swap | < 50 ms (120 ms debounce) | not yet wired |

- **Memory metric:** private bytes per state is the budget; summed working set counts shared Chromium
  pages in several processes and is not additive. If the owner reads 600 MB as summed working set, the
  list is 11 MB over and the editor 98 MB over at first sample (37 MB trims after two more seconds).
  Approved reductions, in order: mount the editor only when first opened (or keep it warm but unmount
  the list), load list bodies/images on demand, and a leaner host if needed — **never a feature cut**.
- **Cold start:** 4.4 s (window 1.7 s, WebView2 ready 4.4 s, navigate→ready 2.16 s). Pre-create at
  login; do not launch on demand.

If a budget is missed, optimize the DOM, image loading or cache layout — **do not cut features**.
Report the measured number and the trade-off to the owner.

## Phases (each gated, current app remains runnable)

0. **Warm-surface spike — DONE (25 Nov).** Shared React UI in a scratch WPF WebView2: warm show
   28.7 ms median, editor overlay 72.6 ms, zoom 1.25/1.5 zero overflow, private memory 248/363/421 MB.
   Cold start requires login prewarm.
1. **Bridge + read-only list**: `Clip.Host`, `#/list` renders real recent items, tabs, counts; native
   paste still works from the page.
2. **Interactions**: selection, keyboard matrix, search, delete/undo, pin/move, context menu,
   empty states, live capture updates.
3. **Rich rows**: text/HTML/RTF/files/images, sanitization, 3-line clamp, +N, time sections, gallery.
4. **Settings + editor**: settings routes, hotkey capture, theme/zoom, editor **overlay** + TipTap
   toolbar, CF_HTML save, discard prompt. Measure first-open latency; mount lazily and keep warm.
5. **Native edges**: toast/tooltip, tray routing, DPI 100/150/200, zoom matrix, accessibility, IME.
6. **Cutover**: feature checklist, audit, owner acceptance; archive the WPF UI.

## Non-goals

- Cross-platform (Tauri/Electron) until Windows is perfect.
- Rewriting `Core\`/`Interop\` or the SQLite schema.
- A second web surface, or a browser on the cold popup path.
- Chasing the old 150/250 MB gate at the cost of features.

## Tauri spike (25 Nov) — validated alternative, not chosen

A full Tauri 2.12.7 spike was built after the owner installed Rust + MSVC. It is a real option, not a
paper rejection:

| Test | Result |
|---|---|
| HTML write/read (CF_HTML, fragment, Devanagari/₹) | PASS |
| RTF write/read, real Word RTF | PASS |
| File list write/read (order) | PASS |
| Image PNG/CF_DIB/DIBV5 + hash | PASS |
| Clipboard monitor events | PASS |
| Paste into Notepad (SendInput, focus guard) | PASS |
| Tray / hotkey / single instance / autostart / SQLite | PASS |
| Cold start → visible / first frame | 456 / 526 ms |
| Warm show → first paint | 36.5 ms median / 44.3 max |
| RAM shown / editor (WS / private) | 429 / 566 MB WS; 219 / 340 MB private |
| Exe | 11.7 MB; first build 149 s, incremental 34 s |

Why not chosen for the release: the spike proves the common formats, not Clip's full matrix (Excel
Biff12, arbitrary named formats, CF_DIBV5, delayed-render streams, RTF→HTML, password-manager
exclusions, IME, multi-monitor DPI, tray menu, long-run memory). Porting `Core` + `Interop` replaces 2,818
tested lines with new Rust and re-opens every one of those edge cases. The CPU comparison is also not
equal (the Tauri host reported 16 CPUs/24 GB; the WPF spike ran on the 2-vCPU VM), so the cold-start gap
is directionally right but not a fair 8×.

If the owner later chooses Tauri, it must pass the same feature checklist and clipboard matrix before
cutover. The 11.7 MB, cross-platform build is the reason to revisit it — not RAM alone.

## Open risks

1. Warm show may exceed 150 ms on the 2-vCPU VM; measure before committing the UI rewrite.
2. WebView2 airspace: no WPF overlay over the page; use in-page dialogs or native windows.
3. IME composition and the Esc cascade move page-side; the existing matrix must be re-proven.
4. HTML sanitization is new attack surface; strict allow-list, no network, CSP.
5. Memory may land at 550–650 MB; if so, the owner chooses between the ceiling and a native list —
   not a silent feature cut.
6. WebView2 runtime missing on old Windows 10; keep the existing notice and bootstrapper path.
