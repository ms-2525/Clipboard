# S7 — real settings and the real editor overlay

Built in `UI\web` on `v2-phase1`; driven inside the DEBUG WebView2 shell over the dev CDP port
(`CLIP_V2_DEBUG_PORT=9333`) against **copies** of the test database under `%TEMP%\opencode\s7`
(`clip2.db`, `clip3.db` seeded through `ClipboardStore.Insert`). The live `%APPDATA%\Clip\clip.db`
was never opened, and the owner's `config.json` and `Run` value were backed up before the run and
restored afterwards.

## A. Settings, both ways

`saveConfig({patch})` validates the whole patch, applies what can be applied, persists once through
`Config.Save()` (camelCase JSON in `%APPDATA%\Clip`), replies `{config, hotkeyError}` and emits
`{event:"settingsSaved", data:config}`.

| Change | Evidence |
|---|---|
| theme Light → Dark | saved `"theme":"dark"` in `config.json`; page `data-theme` dark (`03`, `06`) |
| theme System | saved `"theme":"system"`; the raw choice stays in the file, the page gets the resolved mode (`22`: after a cold start the page is dark, the System card is on) |
| zoom 100 → 125 → 150 | `Ctrl +` twice: viewport 900×700 → 600×466 (`20`); dialog and page both report `scrollWidth == clientWidth` at 600 (`21`, `22`); `"uiZoom":150` survives the restart and is applied at init |
| time format 12 h | rows re-render `25 Nov 2026, 04:19 AM` (`14`, `16`); stored `"timeFormat":"12h"` |
| max items | host cap set to 20 through the same bridge: header chip `20`, `20 clips`, 20 rows (`settings4` run) |
| hotkey | capture field shows keycaps (`07`); saved `"hotkey":"Alt+J"`; global Alt+J shows the focused shell after a hide (window enumeration: visible → hidden → visible) |
| hotkey conflict | a second process owns Ctrl+Alt+K; Save returns `Could not register Ctrl+Alt+K. Another app may own it; kept Alt+J.` inline (`08`), the field returns to Alt+J, `config.json` keeps Alt+J, and Alt+J still shows the shell |
| autostart | toggle off → `HKCU\...\Run\Clip` removed and `"startOnLogin":false`; toggle on → the value is written once and `true` |
| clear history | the dialog asks with the tray's words; Delete → `All 0`, `Pinned 1` (pinned rows stay), `No clips yet`, toast `History cleared` (`23`) |

Zoom presets 80/100/125/150 live in the dialog and persist as soon as they change; `Ctrl+Plus`,
`Ctrl+Minus` and `Ctrl+0` step the same values from the page.

## B. Editor

Opening (F2 or Edit clip) loads `getItem`, strips the CF_HTML header with the same parser the rows
use, and feeds TipTap the fragment; a clip without HTML opens as paragraphs. A truncated row opens
its **whole** body (`16`: 5,040 characters, `Full body` chip, while the list row shows `+3.6k`).

The toolbar keeps the existing look and covers bold/italic/underline/strike/inline code, H1–H3,
bullet/ordered/task lists, quote, code block, link, colour/highlight, font family, table + add row,
text size, undo/redo, clear and single line; the footer keeps the character count.

Save → `saveEditor({id, html, text})`:

- refuses empty text, HTML over 2,000,000 chars and any `<script`;
- loads the full row (so `ClipboardStore.Update`'s preview guard still applies) and writes
  `HtmlFragment.Wrap(html)` — the one envelope writer, moved out of `EditorWindow` so both editors
  share it;
- replies `{saved:true, htmlLength}` or `{saved:false, error}`, then the page shows `Clip saved` and
  closes (`14`).

Proof on the stored row (`s7probe check`): `StartHTML` points at `<html>`, `EndHTML` equals the
UTF-8 byte length, `StartFragment`/`EndFragment` bracket exactly the fragment, the fragment parses
back, contains the typed `<strong> S7 edit</strong>`, a table with the added row and no script —
12/12. The same bytes on the Windows clipboard validate byte-exactly (`check-clipboard.mjs`), and a
real **Word 16.0** paste produced the text, **3 tables** and `boldNet=true, boldEdit=true`.

Discard: the first Esc closes the link popover (`popClosed.editor === true`), the second shows
`Discard changes?` (`17`), the third closes with the stored body untouched (row 8 stayed at
4,800 chars, no `DISCARD ME`).

## C. Must not regress

| Check | Result |
|---|---|
| console | 0 errors / warnings |
| network | 5 requests, all `https://clip.local/`; 0 external |
| overflow | 0 at 900, 600 (150 %), 430 (`18`, `19`); dialog `397/397` at 430, `518/518` at 600 |
| `?mock=1` (headless Edge, file://) | 1,000-row fixture, settings Dark/125 % saved, editor saved, 0 console errors, 0 external requests (`25`, `26`) |
| `tsc --noEmit` | clean |
| `npm run build` | clean (single-file dist) |
| `dotnet build -c Release` | 0 warnings, 0 errors |

## Screenshots

| File | What it shows |
|---|---|
| `02-settings-light.png` | settings, light, all groups above the fold |
| `03-settings-dark.png` | same dialog after the Dark card (live preview) |
| `06-settings-150-reopen.png` | dialog reopened at 150 % zoom; 0 overflow |
| `08-hotkey-conflict.png` | capture result + inline conflict message, Alt J kept |
| `09-clear-confirm.png` | the tray's clear-history confirmation inside the dialog |
| `11-editor-dark.png` | editor on a real Word clip: h2, bold run, table, link, emoji |
| `12-editor-light.png` | same clip in light |
| `13-editor-table.png` | a table inserted from the toolbar |
| `14-editor-saved.png` | `Clip saved` toast, the list row rendering the saved rich body |
| `15-editor-table-row.png` | the new table after **Add a table row** (3 rows) |
| `16-editor-truncated.png` | a truncated clip opened with its full body (5,040 chars) |
| `17-discard-prompt.png` | `Discard changes?` after the second Esc |
| `18-list-430.png` | list at 430 px, 0 overflow |
| `19-settings-430.png` | settings at 430 px, 0 overflow |
| `20-list-150.png` | list at 150 % (600×466) |
| `21-editor-150.png` | editor at 150 %, 0 overflow |
| `22-settings-after-restart-150-system.png` | cold start: zoom 150, System theme, System/150 %/12-hour on |
| `23-after-clear-fixed.png` | after Clear history: All 0, Pinned 1 |
| `25-mock-settings-saved.png` | `?mock=1` settings saved (Dark, 125 %) |
| `26-mock-editor-saved.png` | `?mock=1` editor save |

## Cuts and notes

- **Toast width/height/position** are accepted and clamped by `saveConfig` but have no row in the
  dialog (the task allowed cutting them). `Toast lines` (Notification.MaxLines) is wired.
- **Alignment** buttons were not added: `@tiptap/extension-text-align` is not installed and the
  npm cache here has no copy; every other toolbar group from the brief is present.
- The host keeps no theme polarity of its own: `Host\ClipHost.Settings.cs` calls `App.ResolveTheme`
  (the one resolver), and a saved choice applies `App.ApplyThemeFromSettings` so the WPF dictionaries
  and the Windows watcher follow the same value the page gets. The page hears the resolved mode in
  the `saveConfig` reply/`settingsSaved` and, when it differs, the `themeChanged` event.
- `RebindHotkey` reaches the app's private `RegisterV2Hotkeys` by reflection because `App.V2.cs` was
  off-limits for this slice; a typed method on `App` should replace it (marked `shortcut:`).
- `Core\Config.cs` gained one additive property, `UiZoom` (80–150), because `saveConfig` must persist
  the zoom and `Config.Save()` only writes its own properties.
