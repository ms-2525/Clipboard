# Clip — User Guide (November 2026)

Clip is a Windows clipboard manager. It remembers what you copy — text, links, formatted text,
pictures and files — and gives it back when you need it. It runs in the notification area (the tray)
and keeps everything on this computer.

## 1. Opening Clip

- Clip starts with Windows if you switch on **Launch at login** in Settings. Otherwise start it from
  the Start menu once; it stays in the tray.
- Press **Alt+V** anywhere to open the list. Press **Esc** to hide it. One Esc first clears a search,
  a second Esc hides the window.
- Clicking another window also hides Clip. It does not quit — it waits in the tray.
- To quit for good, right-click the tray icon and choose **Exit**.
- The first open after signing in to Windows can take a moment while the window warms up. Later
  opens are immediate.

## 2. The list

The newest clip is at the top. Each row shows:

- **A number** — 01, 02, 03 and so on. These are not decoration: with the list active,
  **Ctrl+1**–**Ctrl+9** pastes the 1st–9th visible row straight into the app you came from. A bare
  digit types into the search box, so you can search for `2024` or `1,250` without a modifier.
- **The clip itself**, up to 10 lines. Long text is cut for display only; the whole clip is kept.
- **Where it came from** — app, window title and the time it was copied.
- **Tags** — what kind of thing it is, worked out automatically. Up to two tags fit on a row; the
  rest fold into **+N**. A clip with no tag is normal: Clip only tags what it can prove.

Tags you may see: **Image**, **Files**, **Spreadsheet**, **Link**, **Email**, **Path**, **Colour**,
**Number**, **JSON**, **HTML**, **SQL**, **XML**, and a code language such as **Python**,
**TypeScript**, **C#**, **JavaScript** or **Shell**.

Use **↑ / ↓**, **Home / End** and **PageUp / PageDown** to move. **Enter** pastes the selected clip.
**Double-click** a row to copy it back to the clipboard without pasting. Right-click a row to move
it to a tab (see below).

## 3. Search

Start typing. Clip searches the whole clip, not only the lines you can see, and the app it came
from. Every word you type must be present, so `bank statement` finds clips containing both words.

- **A tag** needs the colon. `image:` finds pictures; `excel:`, `sheet:`, `xls:` and `csv:` all find
  spreadsheet clips; `code:` finds any code language. Tags combine: `excel: link:` finds clips that
  are both. Put a minus in front to exclude: `-image:` hides pictures and keeps everything else.
  **`tag:` works too:** `tag:image`, `tag:excel`, `-tag:files`. An unknown name, `tag:zzz` or `zzz:`,
  is searched as text with a hint.
- **Quotes** keep a phrase together: `"bank statement"` finds that phrase, not the two words in
  separate places.
- **A typo like `foo:`** is not a tag, so Clip searches it as ordinary text and says
  `no tag named "foo" - searched as text`. Nothing you type is silently ignored.
- **Clicking a tag** on a row adds that tag to the search for you.

There is no OR search and no saved searches in this release.

## 4. Tabs

The strip above the list holds **All**, **Pinned**, **Images**, and any tabs you make.

**Images** is a built-in view, not a folder: it shows every picture in All history, with a live count.
It is the same as typing `image:`. Moving a clip into a tab does not remove it from Images.

- **Create:** click **+**, type a name, press **Enter**. **Esc** cancels. A blank name or one already
  in use is refused with a message.
- **Rename:** double-click a tab, or right-click it and choose **Rename**. **Pinned** and **All**
  cannot be renamed.
- **Move a clip:** right-click the clip and choose a destination, or select it and press **Ctrl+M**.
  The choices are **All history**, your own tabs, and **Pinned**.
- **Delete a tab:** right-click it and choose **Delete tab**. Clip tells you exactly how many clips
  are inside and asks first. **Deleting a tab deletes the clips inside it.** Pinned clips are not
  affected, because Pinned is its own tab.

The history limit in Settings applies to **All** only. Clips in your own tabs and in Pinned are never
removed automatically.

## 5. Editing a clip

Select a clip and press **F2**, or click **Edit clip**. The editor opens on the same clip — saving
changes it in place, it does not make a second copy.

- Use the toolbar for bold, italic, headings, lists, links and clearing formatting.
- Click **Save clip** when you are done. A small dot beside the title means you have unsaved changes.
- If you try to close with unsaved changes, Clip asks **Discard changes?** Choose **Keep editing** or
  **Discard and close**.
- An empty clip, a script, or more than 2 MB of formatting is refused with a message, so a bad save
  cannot destroy the clip.

## 6. Saving a clip as a file

Select a clip and press **Ctrl+E**, or click **Save as**. Pick a format:

- **Text** saves as `.txt`, or **Markdown** as `.md` — same words, different file ending.
- A **spreadsheet** clip saves as `.csv`.
- An **image** saves as `.png`, exactly the picture that was copied.
- A **file list** has no save option; it is a list of file locations, not a document.

Clip suggests a name from the clip's first line. The normal Windows Save dialog lets you change the
name and choose the folder; the file goes wherever you save it. A toast then confirms the save and
names the file. Saving never changes the clip, and a long clip is saved in full even when the row
shows only 10 lines.

**Two things to know about Excel.** A CSV opens with its columns intact. But Excel reinterprets
values *when it opens the file*: `007` becomes `7`, and a cell containing `=1+1` becomes a formula
showing `2`. The file itself is correct — this is Excel's import behaviour. If leading zeros or
literal text matter, open the file through **Data → From Text/CSV** and set those columns to Text,
or type an apostrophe before the value in Excel.

## 7. Text in images

When you copy a picture, Clip reads the text inside it using Windows' built-in reader. This happens
on your computer, with no internet and nothing uploaded.

- The words are stored with the picture, so searching for a word that appears only in a screenshot
  finds that clip.
- The row is marked **Text read from image**. If nothing could be read it says **No text found in
  this image**.
- Pasting the clip still pastes the picture, never the words.
- Hindi needs the Hindi language pack installed in Windows. Without it, Clip reads no text from that
  image rather than guessing.

**Never trust this text for amounts.** The rupee sign (`₹`) is not readable, Indian digit grouping
(`1,23,456`) is lost, and some amount lines are dropped. Treat every figure as a hint and check it
against the picture. The measured accuracy on a clean screenshot was 93.7 % of characters — an
average, not a promise. Small, blurred or stylised text is missed more often.

## 8. Settings

Open Settings from the gear in the list, or right-click the tray icon and choose **Settings**.

- **Launch at login** — start Clip with Windows.
- **Open Clip hotkey** — click the keys, then press the new combination. If another app owns it,
  Clip keeps the old one and tells you.
- **History limit** — how many clips **All** keeps (100 to 5,000). The oldest unpinned clips go
  first. Your own tabs and Pinned are not counted.
- **Toast lines** - the longest a capture toast may grow. The toast fits its content both ways: a short
  copy gets a narrow, short toast; a long one widens to the maximum, wraps, and stops at the line cap.
- **Theme** — Light, Dark or System (follows Windows).
- **Zoom** — 80 %, 100 %, 125 % or 150 %. It changes on screen as you pick it; **Ctrl +** and
  **Ctrl −** step it, **Ctrl+0** returns to 100 %.
- **Time** — 12-hour or 24-hour clock on every row.
- **Clear history** — deletes the clips in **All** and the image history. Pinned clips and your tabs
  stay. It asks first and cannot be undone.
- **Save changes** keeps your choices. Two settings are fixed in this release: Clip always starts in
  the tray, and rich capture is always on (use **Ctrl+Shift+V** when you want a plain paste).

## 9. Keyboard map

| Key | Action |
|---|---|
| Alt+V | open or hide the list |
| Ctrl+Shift+V | open the list to paste as plain text |
| type | search |
| Ctrl+K or Ctrl+F | jump to the search box |
| Esc | clear the search, then hide the window |
| ↑ ↓ / Home / End / PageUp / PageDown | move through the list |
| Ctrl+1 … Ctrl+9 | paste the 1st-9th visible row (a bare digit searches) |
| Enter | paste the selected clip |
| F2 | edit the selected clip |
| Ctrl+M | move the selected clip to a tab |
| Ctrl+P | pin or unpin the selected clip |
| Ctrl+E | save the selected clip as a file |
| Delete | delete the selected clip |
| Ctrl+Z | undo the last delete |
| Ctrl+ + / Ctrl+ − / Ctrl+0 | zoom in / out / reset |

## 10. Where your data lives

Everything is in your own Windows profile, under `%APPDATA%\Clip\`:

- `clip.db` — the clips, tabs and text read from images.
- `config.json` — your settings.
- `webview\` — the profile the window uses.
- `crash.log` — written only if something fails unexpectedly.

There is no cloud sync and no telemetry. If the database is damaged, Clip does not delete it: the
file is set aside with a `.corrupt-<date>-<time>` ending, a fresh history is started, and a
notification says **"Clip history could not be read and was moved aside. A fresh history has
started."** The old file remains in that folder if it ever needs to be recovered.

This release also keeps the old Clip window available if the program is started with `--legacy`.
It will be removed in the next release; use the window described here for daily work.
