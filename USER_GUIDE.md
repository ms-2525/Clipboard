# Clip — User Guide (v2, November 2026)

Clip is a Windows clipboard manager. It remembers what you copy — text, links, formatted text,
images and files — and gives it back when you need it. It lives in the notification area
(the tray); press **Alt+V** anywhere to open the list.

## Everyday use

- **Copy anything.** Clip captures it automatically: plain text, a web link, rich text with its
  formatting, a screenshot, or a list of files copied in Explorer.
- **Alt+V** opens the list, newest first. It opens warm, so it is on screen in a blink.
- **Type** to search — the list filters as you type. **Esc** clears the search; **Esc** again hides
  the list.
- **Enter** pastes the selected clip into the app you were in. **1–9** pastes the 1st–9th row
  directly. **Ctrl+Shift+V** opens the list for a plain-text paste (formatting stripped).
- **↑ / ↓, Home / End, PageUp / PageDown** move through the list. The list never jumps.
- **F2** (or the **Edit clip** button) opens the editor. **Ctrl+E** (or **Save as**) saves the
  selected clip as a file.
- **Delete** removes the selected clip. **Ctrl+Z** brings it back. **Ctrl+P** pins or unpins it.
- **Double-click** a row to copy it back to the clipboard without pasting.

Clicking a tag chip on a row filters the list by that tag.

## Tabs

The list shows your own tabs from the database: **All** (everything in the main history),
**Pinned** (your keepers) and any tabs you created in earlier versions. A clip in a tab or Pinned
is never auto-deleted. The history limit (Settings) applies to the main history only.

## Tags and search

Clip derives tags from the clip itself: `image`, `files`, `spreadsheet`, `link`, `email`,
`path`, `color`, `number`, `json`, `html`, `sql`, `code`, plus a language tag such as
`python`, `typescript`, `csharp`, `javascript`, `sql` or `shell`.

- **Words** are ANDed: `bank statement` finds clips containing both.
- **`tag:`** filters by a known tag: `image:`, `files:`, `spreadsheet:` (aliases `excel:`, `csv:`,
  `xls:`, `sheet:`), `link:` (aliases `url:`, `website:`), `code:`, `python:`, `email:` and the
  rest above.
- **`-`** excludes: `-image:` hides images, `-link:` hides links.
- A word that looks like a tag but is not one (`cheque:`) is searched as text, and the list says
  "no tag named … — searched as text". A tag is only shown when Clip can prove it; a missing tag
  is deliberate, a wrong one is not.

## Saving clips

- **Images** save as **PNG**.
- **Spreadsheets** (a clip tagged `spreadsheet`) save as **CSV**.
- **Text, rich text, code** save as **.txt** or **.md**.
- **Save as…** (Ctrl+E) picks the right format automatically; the menu offers the other formats.

**Excel note.** The CSV is correct UTF-8 (with a byte-order mark). Excel still reinterprets values
*on open*: a code like `007` loses its leading zeros and a cell like `=1+1` becomes a formula. If
that matters, open the CSV through **Data → From Text/CSV** and set the columns to Text, or put an
apostrophe in front of the value before saving.

## Reading text from screenshots (OCR)

Images are read offline by Windows' own OCR engine and the words are stored with the clip, so
`invoice` searches find screenshots too. The image row shows "Text read from image".

- It works without internet. Nothing is uploaded.
- **Never trust OCR for amounts.** The rupee sign (`₹`) is not readable by the engine, and Indian
  digit grouping (`1,23,456`) is lost. Treat the numbers as a hint and verify every amount against
  the picture itself.
- Small or low-contrast text is also unreliable. The measured accuracy on a clean screenshot was
  93.7 % of characters; that is an average, not a promise.

## Settings

Tray icon → right-click → **Settings** (or the gear in the list).

- **Launch at login** — start Clip with Windows.
- **Open Clip hotkey** — click the keys, then type a new combination. A combination another app owns
  is refused and the old one is kept.
- **History limit** — how many clips the main history keeps (100–5000); the oldest unpinned clips go
  first.
- **Toast lines** — how many lines a capture toast shows; the toast still auto-fits.
- **Theme** — Light, Dark or System (follows Windows).
- **Zoom** — 80 %, 100 %, 125 % or 150 %; Ctrl + / Ctrl − also step it.
- **Clear history** — deletes the main history and images; Pinned clips and tabs stay.
- **Save changes** applies everything at once. Cancel leaves the old values.

## Where your data lives

Everything is under `%APPDATA%\Clip\` (normally `C:\Users\<you>\AppData\Roaming\Clip`):

- `clip.db` — the database (clips, tabs, formats, OCR text).
- `config.json` — your settings.
- `webview\` — the browser profile the window uses.
- `crash.log` — written only if something fails unexpectedly.

There is no cloud sync and no telemetry. `Clip.exe --db <path>` opens a different database, and an
explicit `APPDATA` environment variable redirects the whole folder (useful for a test profile).

## Keyboard map

| Key | Action |
|---|---|
| Alt+V | open/close the list |
| Ctrl+Shift+V | open for a plain-text paste |
| type | search |
| Ctrl+K / Ctrl+F | focus the search box |
| Esc | clear search, then hide |
| ↑ ↓ / Home / End / PageUp / PageDown | move the selection |
| 1–9 | paste the 1st–9th row |
| Enter | paste the selected clip |
| F2 | edit the selected clip |
| Delete | delete the selected clip |
| Ctrl+Z | undo the last delete |
| Ctrl+P | pin / unpin |
| Ctrl+E | save the selected clip |
| Ctrl+ + / Ctrl+ − / Ctrl+0 | zoom in / out / reset |

## Good to know

- The window hides when it loses focus; it does not exit. **Tray → Exit** is the only way to quit.
- A second launch brings the running window forward instead of starting a second copy.
- Password managers and apps that mark a copy as excluded are never recorded.
- If Alt+V is already owned by another app, Clip says so in a toast; choose another combination in
  Settings.
- F2 only reaches Clip while its window has focus; a screenshot tool that owns F2 globally will take
  it first. Use the **Edit clip** button in that case.
- The first Alt+V after signing in to Windows can take a moment while the page warms; every open
  after it is instant.
- `Clip.exe --legacy` opens the old WPF interface for this release only. It will be removed in a
  later version, so use the new UI for daily work.

## Three-minute check

1. Copy a sentence from any app → Alt+V → it is at the top of the list.
2. Type a word from it → the list filters; press Esc twice.
3. Copy a screenshot → find it in All (tag `image`) → Enter pastes it into Paint.
4. Select a clip → **Save as** → open the file.
5. Tray icon → Settings → switch to Dark → Save → the list turns dark immediately.
