# HANDOFF — TipTap editor port (for a fresh orchestrating chat)

Owner decision, 24 Nov 2026: Clip's F2 editor moves from Syncfusion SfRichTextBoxAdv to the
**TipTap + WebView2** stack already proven in the owner's other app (QuickNotes). Reason:
TipTap is MIT — completely free, no account, no licence key, nothing revocable. The owner
does not want to claim the (also free) Syncfusion Community key.

Read `AGENTS.md` first. Everything below is verified fact from this machine unless marked
"to verify". Do not re-research licensing — the work is done; see §1.

## 0. Where the code is right now

- HEAD `662f2b2` ("Design language v3 (Tools3000) + checklist open-only restructure") contains
  BOTH the v3 visual rework AND the Syncfusion editor swap (Clip.csproj package +
  `UI\EditorWindow.xaml/.xaml.cs/.Toolbar.cs` rewritten to the control's selection API).
- Build: `dotnet build -c Release` → 0 errors, 0 warnings (verified at HEAD).
- Syncfusion runs **unlicensed** (`SyncfusionLicenseProvider.ValidateLicense → False`).
  Everything works, but shipping in this state is not allowed; the port removes the issue.
- Visual authority is now `docs\DESIGN-language.md` (v3, Tools3000-derived; orange accent).
- Baseline to return to if the port fails: `git revert` to `662f2b2`.

## 1. Licensing facts (verified — do not reopen)

| Option | Verdict |
|---|---|
| **TipTap v3 (`@tiptap/*`)** | MIT. Free, commercial OK, no key. In production in `C:\Users\mayan\CascadeProjects\QuickNotes` (see §2). |
| **WebView2** (`Microsoft.Web.WebView2` NuGet) | Free, MIT; runtime preinstalled on Win 11, silent evergreen bootstrapper for older machines. |
| Syncfusion Community | Free for this owner but needs an account + key. **Rejected by owner.** |
| Telerik UI for WPF | $749–$1,249 / developer / year; matched NuGet packages sit behind their authenticated feed (401 verified). Dead end. |
| Xceed Extended WPF Toolkit | Licence file says **non-commercial only**, branding required, revocable. NOT free for this app. |

## 2. Reference implementation to copy from (owner's own app)

`C:\Users\mayan\CascadeProjects\QuickNotes` — Electron, TipTap 3:

- `src\tiptap-editor.js` (1016 lines): extensions wiring (StarterKit, Underline, Color,
  TextStyle, Image, Link, TableKit, TaskList, Typography, Placeholder, HorizontalRule),
  markdown input rules, Excel/table paste sanitising, custom extensions pattern.
- `src\main\note-format.js`: HTML-in/out discipline (escape/unescape, wrapper div,
  balanced-div slicing) — reuse the *ideas*, Clip has its own store.
- `package.json`: exact `@tiptap/extension-*` versions (all MIT). Bundled with esbuild into
  one IIFE (`build:editor` script) so the runtime never needs node_modules.
- `src\editor\excel-table-html.js` / `spreadsheet-paste.js`: paste-from-Excel table handling.

## 3. Target architecture (two-layer rule stays)

```
Layer A (app chrome)      = WPF, as today: window, toolbar buttons, footer, theme tokens.
Layer B (clip content)    = the F2 editor window: WebView2 hosting one TipTap editor.
Main-window row previews = UNCHANGED. RichRow/RowContent inline renderer stays
                           (no library can render 200 virtualized rich rows cheaply;
                           owner rule: never a heavyweight editor per row).
```

- The list popup stays 100% WPF and instant (no browser process for Alt+V).
- F2 opens the editor window; WebView2 is created with the window and disposed with it.
- Data flow unchanged elsewhere: SQLite columns `text/html/rtf`, `item_formats` blobs,
  PasteInjector, hotkeys, capture, single-instance — all untouched.

### Save contract decisions (already made — implement, don't redesign)

1. Load: `item.Html` (strip CF_HTML header via the existing `Interop\HtmlFragment.Extract`)
   else plain `item.Text`. RTF-origin clips: convert once on open **via the existing
   `Core\HtmlToRtf`-reverse? — NO.** Decision: for RTF-only clips, load RTF by setting it as
   clipboard content into the WebView? Unacceptable. Implement instead: TipTap receives the
   clip's HTML when it exists; for RTF-only clips reuse the *existing* editor fallback: open
   the Syncfusion-era path is gone, so convert RTF→HTML once per open. **Simplest truthful
   option:** RTF-only clips open as plain text + a notice bar (same UX as today's failed
   load), UNLESS a free RTF→HTML converter is available at implementation time — check
   `Core\RichRow`: it already builds inline runs from RTF via WPF's parser; a tiny
   HTML writer over those runs (≤60 lines, content layer) is allowed. Do that only if the
   owner-visible regression is unacceptable; otherwise plain+notice. To verify during slice 4.
2. Save: `item.Text` = plain text; `item.Html` = TipTap's clean HTML (always set — TipTap is
   the editor of record); `item.Rtf = null` after any edit (stale RTF must never outlive the
   content it no longer matches). Paste-back to Word/Chrome rides the `HTML Format`
   clipboard channel, which both consume natively (to verify: §6 test T5).
3. `item_formats` are already deleted by `ClipboardStore.Update` — unchanged.

## 4. Slices (subagents, disjoint file sets, one slice ≤ 5 production files)

Run sequentially; gate every slice on `dotnet build -c Release` = 0 errors **0 warnings**.

**Slice 1 — WebView2 host (files: `Clip.csproj`, `UI\EditorWindow.xaml`,
`UI\EditorWindow.xaml.cs`)**
- csproj: remove `Syncfusion.SfRichTextBoxAdv.WPF`; add `Microsoft.Web.WebView2` (MIT).
- EditorWindow.xaml: keep every element of the v3 chrome (header, toolbar row, notice bar,
  discard prompt, footer); replace the `sf:SfRichTextBoxAdv` with `<wv2:WebView2 x:Name="Web"/>`
  in the same LAYER-B frame (Surface background, ControlLine border, radius 8).
- Code-behind: create CoreWebView2, `SetVirtualHostNameToContentFolder("clip", editor dir)`,
  navigate to `https://clip/editor.html`. Bridge:
  - Outbound (host→page): `ExecuteScriptAsync("window.clipBridge.load(...)")` — HTML load,
    command runs (see slice 3), plain-text load.
  - Inbound (page→host): `WebMessageReceived` JSON `{type:"ready"|"dirty"|"save", html, text}`.
- Esc / Ctrl+S / F2 stay WPF-side (PreviewKeyDown) exactly as today; "dirty" message drives
  the existing discard prompt.
- Runtime check: on first open, if WebView2 environment creation throws, show the existing
  FormatNotice with "Editing needs the free WebView2 runtime (installed with Windows)."
  Never crash the window.
- Warm-up (optional, only if measured slow): create the environment once at app idle
  (`App.xaml.cs` is NOT in this slice — if warm-up is needed, raise it to the orchestrator
  and do it in slice 4 as a measured decision).

**Slice 2 — editor asset (files: `Assets\editor\editor.html`,
`Assets\editor\editor.js` (bundled), `Assets\editor\editor.css`, `scripts\build-editor.cmd`)**
- One page: Inter font (already shipped in `Assets\Fonts`), content on a white Surface with
  14/20 body — match `docs\DESIGN-language.md` type scale. No app colours in JS/CSS beyond
  ink + link; this is Layer B (original content), not chrome.
- editor.js: TipTap setup copied from QuickNotes `src\tiptap-editor.js` (StarterKit,
  Underline, Color, TextStyle, Image, Link, TableKit, TaskList, Typography, Placeholder,
  HorizontalRule). Expose on `window.clipBridge`:
  `load(html)`, `loadPlain(text)`, `getHtml()`, `getText()`, `run(cmd, arg)` where cmd is one
  of: bold, italic, underline, strike, highlight, color, size, clearFormat, singleLine,
  case, plus `on('ready'|'dirty'|'save')` → `window.chrome.webview.postMessage(...)`.
- `singleLine` and `case` are small custom commands (QuickNotes' custom-extension pattern);
  they are the only new logic in the content layer.
- Build: esbuild IIFE bundle committed to the repo (`scripts\build-editor.cmd` runs
  `npm i && npm run build` with a local package.json kept in `Assets\editor\`). The C# build
  must never require node — the built `editor.bundle.js` is committed.
- csproj: `<Content Include="Assets\editor\**" />` (slice 1 already owns the csproj edit —
  coordinate so BOTH changes land in slice 1's commit or accept one shared csproj touch
  across two slices; the orchestrator merges the refs, never both agents in parallel).

**Slice 3 — toolbar bridge (file: `UI\EditorWindow.Toolbar.cs` only)**
- Keep the existing button set and v3 button grammar; each click becomes
  `Web.ExecuteScriptAsync($"window.clipBridge.run('{cmd}', {argJson})")`.
- Reflection of caret state: page posts `{type:"sel", bold, italic, underline, strike, size,
  color, highlight}` on selection change; host mirrors onto the buttons (same WordPad
  behaviour as today).
- Remove every `Syncfusion.*` using and API call. File stays ≤ 400 lines.

**Slice 4 — verification + evidence (no production files; writes
`docs\AUDIT-tiptap-port.md` + harness under `%TEMP%\opencode`)**
- Harness: the previous session proved SendKeys is unreliable; use the deterministic route —
  temporarily add an `App.OnStartup` branch `--test-editor <png> <mode>` that opens
  `EditorWindow` in-process for the newest clip of a kind, renders with
  `RenderTargetBitmap`, saves PNG, shuts down. Revert the branch after evidence. (The
  previous session's scripts are at `%TEMP%\opencode\verify-editor.ps1` and
  `%TEMP%\opencode\evidence\*.png` — temp, may be gone; re-create.)
- Tests T1–T8 in §6. Record every number/shot in `docs\AUDIT-tiptap-port.md`.
- Update `AI_LEDGER.md`, `CURRENT_STATE.md` (lane + position), and the editor row of
  `docs\CHECKLIST-features.md` with evidence paths. Owner-facing summary in chat, plain
  English.

Rules for every slice: no TODO/stubs/Console.WriteLine; no hex outside
`UI\Themes\{Dark,Light}.xaml`; `ShutdownMode="OnExplicitShutdown`" stays; never build
`_fonttest\`; one slice = one commit, message states what was verified.

## 5. Do-not-touch list (owner directive, repeated)

SQLite schema/store semantics, `CaptureService`, `HotKeyWindow`/hotkeys, `PasteInjector`,
`ClipboardFormats` snapshot/restore, single-instance mutex, tray, tabs/pinning/Images,
row rendering (`RichRow`/`RowContent`/`ClipRow`/`RowTemplate.xaml`), themes (v3 is fresh —
do not restyle), SettingsWindow, NotificationWindow. The editor swap touches
`EditorWindow*` and `Assets\editor\` ONLY.

## 6. Acceptance — the owner judges on screen; no self-reported green

| # | Test | Evidence |
|---|---|---|
| T1 | Build | `0 errors, 0 warnings` paste |
| T2 | Plain clip opens, caret at end, Esc dirty-prompt works | screenshot |
| T3 | 24 KB Edge/ChatGPT HTML clip (dark source): colours/bold/lists render, white-on-dark stays on dark | screenshot |
| T4 | Word-style RTF clip: see §3.1 decision — whatever ships, state it in the audit | screenshot |
| T5 | Edit → Ctrl+S → reopen: formatting persists in editor AND row preview; paste into Word/Chrome keeps formatting (manual, owner or agent browser) | screenshot + paste result note |
| T6 | Toolbar: bold/italic/underline/strike/size/colour/highlight/clear/single-line/case all act on selection; buttons reflect caret | screenshots |
| T7 | F2 cold open and warm open, ms; WebView2 process RAM while open | numbers |
| T8 | DPI 100% / 150% / 200%: screenshots of the editor window | 3 screenshots |

Back up `%APPDATA%\Clip\clip.db` before any store-writing test; restore after (previous
session pattern: copy → test → restore).

## 7. Known risks to carry honestly to the owner

- F2 cold open will be slower than the WPF/ Syncfusion editor (browser environment spin-up);
  measure T7 before promising a number; mitigation if bad: keep one hidden editor window
  alive after first use (RAM trade — decide with numbers, not taste).
- ~60–90 MB extra RAM **while the editor window is open** (Chromium). Zero when closed.
- RTF-only clips: fidelity decision in §3.1 — the audit must state which path shipped.
- WebView2 runtime missing on rare old machines → notice with download link (never crash).
- TipTap is HTML-only: after an edit, `item.Rtf` is null by design; Word paste-back rides
  the HTML channel (T5 proves it; if Word ever misbehaves, fall back to keeping the captured
  RTF untouched and marking the clip "edited as HTML" — owner decision at that point).

## 8. Kickoff prompt for the new chat (paste this)

> Read C:\Users\mayan\CascadeProjects\Clipboard\AGENTS.md and
> docs\HANDOFF-tiptap-port.md, then execute the four slices in §4 exactly, gating each on a
> 0-error 0-warning build, with the acceptance evidence of §6. Copy the TipTap wiring from
> C:\Users\mayan\CascadeProjects\QuickNotes\src\tiptap-editor.js. Do not touch the
> do-not-touch list in §5. Orchestrate with subagents, one slice at a time, no parallel
> builds. Report in plain English for a non-coder owner.
