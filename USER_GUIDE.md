# Clip — User Guide (November 2026)

Clip is a Windows clipboard manager. It remembers what you copy — text, links, formatted text,
pictures and files — and gives it back when you need it. It runs in the notification area (the tray)
and keeps everything on this computer. Nothing is uploaded anywhere.

## 1. Installing and starting

There are two builds; both keep their data in the same place, so you can switch freely:

- **Portable** (`dist\Clip.exe`, ~197 MB) — one file, runs as-is. No .NET needed.
- **Runtime folder** (`dist-fd\`, ~81 MB) — `dist-fd\Clip.exe` plus the files beside it. Needs the
  **.NET 10 desktop runtime** installed on the machine. Keep the whole folder together.

Both also need the **WebView2 runtime** (it ships with Windows 11 and with Edge). On a machine
without it, Clip opens with the message *"The WebView2 runtime is not available"* instead of the
list — installing the free WebView2 Evergreen runtime fixes it.

Start Clip from the Start menu or by double-clicking the exe. It lives in the tray, never in the
taskbar. Only one copy runs at a time: starting it again just opens the existing window. The very
first run shows a toast naming the hotkey, since Clip starts silently in the tray.

## 2. The tray icon

- **Left-click** or **double-click** the icon to show or hide the list.
- **Right-click** (or the keyboard menu key on a focused icon) opens the menu:
  - A grey header shows the current hotkey (`Open: Alt+V`) — it always matches Settings.
  - **Clip: show/hide** — same as left-click.
  - **Settings…** — opens Settings.
  - **Notifications** (ticked on/off) — turns the capture toasts on or off.
  - **Start on login** (ticked on/off) — starts Clip with Windows. The tick reads the real
    Windows startup entry, so it stays honest even if something else changed it.
  - **Open data folder** — opens `%APPDATA%\Clip` in Explorer.
  - **Exit** — quits Clip completely.

## 3. Opening and hiding the list

- Press **Alt+V** anywhere to open the list. Press **Esc** to hide it. One Esc first clears a
  search, a second Esc hides the window.
- **Ctrl+Shift+V** opens the list ready to paste as plain text (see §12).
- Clicking another window also hides Clip. It does not quit — it waits in the tray.
- To quit for good, right-click the tray icon and choose **Exit**.
- The first open after signing in to Windows can take a moment while the window warms up. Later
  opens are immediate.

## 4. The list

The newest clip is at the top, grouped by day. Each row shows:

- **A number** — 01, 02, 03 and so on. These are not decoration: with the list active,
  **Ctrl+1**–**Ctrl+9** pastes the 1st–9th visible row straight into the app you came from. A bare
  digit types into the search box, so you can search for `2024` or `1,250` without a modifier.
- **The clip itself**, up to 10 lines. Long text is cut for display only; the whole clip is kept.
- **Where it came from** — app, window title and the time it was copied.
- **Tags** — what kind of thing it is, worked out automatically. Up to two tags fit on a row; the
  rest fold into **+N**. A clip with no tag is normal: Clip only tags what it can prove.

Tags you may see: **image**, **files**, **spreadsheet**, **link**, **email**, **path**, **color**,
**number**, **json**, **html**, **sql**, **xml**, and a code language (**csharp**, **typescript**,
**javascript**, **python**, **rust**, **php**, **go**, **java** or **shell**).

Moving around:

- **↑ / ↓**, **Home / End**, **PageUp / PageDown** move; **Ctrl+↑ / Ctrl+↓** jump to the previous or
  next day; **← / →** or **Ctrl+Tab** switch tabs.
- **Shift+↑ / Shift+↓** selects a range of rows. **Delete** deletes the whole selected range;
  everything else acts on the row the cursor is on.
- **Enter** pastes the selected clip. **Double-click** a row to copy it back to the clipboard
  without pasting.
- **Ctrl+Shift+← / Ctrl+Shift+→** moves the selected clip to the next tab along.

## 5. Search

Start typing from anywhere in the list (or press **Ctrl+K** / **Ctrl+F** to jump to the search box).
Clip searches the whole clip — not only the lines you can see — plus the app it came from. For
pictures and other clips with no text, it searches the file list and the window title instead.
Every word you type must be present, so `bank statement` finds clips containing both words.

- **A tag needs the `tag:` prefix.** `tag:image` finds pictures; `tag:excel`, `tag:sheet`,
  `tag:xls` and `tag:csv` all find spreadsheet clips; `tag:code` finds any code language.
  A bare `image:` is *not* a filter — it is searched as ordinary text, so web addresses, drive
  letters (`C:`) and similar text can never silently turn into a filter.
- Other spellings work too: `tag:img`, `tag:pic`, `tag:photo`, `tag:screenshot` (pictures);
  `tag:file`, `tag:attachment` (file lists); `tag:url`, `tag:website` (links);
  `tag:colour` (same as `tag:color`); `tag:powershell`, `tag:bash`, `tag:sh` (all mean shell code);
  or name a language directly — `tag:python`, `tag:csharp`, `tag:rust` and the rest.
- Tags combine: `tag:excel tag:link` finds clips that are both. Put a minus in front to exclude:
  `-tag:image` hides pictures and keeps everything else.
- **Quotes** keep a phrase together: `"bank statement"` finds that phrase, not the two words in
  separate places. `-` also excludes phrases and words: `-invoice` hides clips mentioning it.
- **An unknown `tag:zzz`** is searched as ordinary text, and the list says
  *no tag named "zzz" — searched as text*. Nothing you type is silently ignored.
- **Clicking a tag** on a row adds that tag to the search for you. **Ctrl+T** opens the tag picker
  to filter by tag without typing.

## 6. Tabs

The strip above the list holds **Inbox**, **Images**, **Pinned**, and any tabs you make.

**Inbox** is the arrival list: every clip you have not filed into a tab or pinned. Move a clip to a
tab, or pin it, and it leaves the Inbox. **Images** is a built-in view, not a folder: it shows every
picture in Inbox, with a live count — it is the same as typing `tag:image`. Moving a clip into a
tab does not remove it from Images.

- **Create:** click **+**, type a name, press **Enter**. **Esc** cancels. A blank name or one already
  in use is refused with a message.
- **Rename:** double-click a tab, or right-click it and choose **Rename**. **Pinned** and **Inbox**
  cannot be renamed.
- **Reorder:** drag a tab left or right; the order is kept. **Inbox** stays first, then **Images**,
  then **Pinned**; your own tabs follow and only those move.
- **Move a clip:** right-click the clip and choose a destination, select it and press **Ctrl+M**,
  or press **Ctrl+Shift+← / Ctrl+Shift+→** to send it to the next tab along. The choices are
  **Inbox**, your own tabs, and **Pinned**.
- **Pin:** select a clip and press **Ctrl+P**, or use the row menu. Pinning moves the clip to the
  Pinned tab; pressing it again unpins it back.
- **Clip info:** right-click the clip and choose **Clip info** to see every detail in full — the app
  and window title it came from (nothing cut short, unlike the row), the date and time, the space it
  takes, how it is stored, and its tags. **Esc** or **Close** returns you to the row.
- **Delete a tab:** right-click it and choose **Delete tab**. Clip tells you exactly how many clips
  are inside and asks first. **Deleting a tab deletes the clips inside it.** Pinned clips are not
  affected, because Pinned is its own tab.

The history limit in Settings applies to **Inbox** only. Clips in your own tabs and in Pinned are
never removed automatically.

## 7. The right-click menu on a row

Right-clicking a clip opens one menu:

- **Edit** (F2) — open the editor (§8).
- **Copy** — put the clip back on the clipboard without pasting. Double-clicking the row does the
  same.
- **Export** (Ctrl+E) — save the clip as a file (§9). Not shown for file lists.
- **Delete** (Del) — asks first; **Ctrl+Z** brings back the last delete.
- **Open in default app** — saves the clip to a temporary file and opens it in its usual app.
  Not shown for file lists.
- **Clip info** — the full detail view (§6).
- **Move to** — the destination tabs with a tick on the one the clip is in.

## 8. Editing a clip

Select a clip and press **F2**, or choose **Edit**. The editor opens on the same clip — saving
changes it in place, it does not make a second copy.

- Use the toolbar for bold, italic, headings, lists, links and clearing formatting.
- Press **Ctrl+S** or click **Save clip** any time — the editor stays open and a green **Saved**
  bar confirms the write. A note saying **Unsaved changes** means you still have edits to save.
  Press **Esc** (or **F2**) when you are done.
- If you try to close with unsaved changes, Clip asks **Discard changes?** Choose **Keep editing**
  or **Discard and close**.
- A save is refused with a message — and the clip left untouched — when the editor is empty, when
  it has no text, when it is over 2 MB of HTML, or when it contains a script.

## 9. Exporting a clip to a file

Select a clip and press **Ctrl+E**, or choose **Export**. Clip offers the formats that make sense
for that clip:

- **Text** saves as `.txt`: the plain words, exactly as copied. **Markdown** saves as `.md`: a rich
  clip keeps its headings, lists and emphasis as Markdown; a plain clip saves the same words. Both
  are plain UTF-8 without a byte-order mark.
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

## 10. Text in images

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
against the picture. Measured accuracy on a clean screenshot was 93.7% of characters — an average,
not a promise. Small, blurred or stylised text is missed more often.

## 11. What Clip does not keep

- Copies shorter than 3 characters are ignored silently.
- Copies a password manager (or any app) marks "do not process" are never read.
- Pictures over 5 MB are skipped with a toast (*Image not saved: … over the 5 MB limit*).
- A single clip's saved formats are capped at 10 MB: if a copy carries more than that, Clip keeps
  what fits and toasts *Part of the copy was not saved*.
- Copying the same thing twice does not make two rows — the existing clip moves back to the top.

## 12. Pasting notes

- **Enter** pastes the selected clip into the app you came from.
- **Ctrl+Shift+V** (in the list) pastes the selected clip as plain text, dropping bold, links and
  other formatting. Settings keeps rich capture always on, so this is the plain-text escape hatch.
- **An app running as administrator blocks pasting.** Windows does not let a normal app drive keys
  into an elevated one, so Clip says so and tells you to **Copy the clip instead** (double-click the
  row, or choose Copy) and paste it yourself with Ctrl+V.
- If focus did not return to the previous app, Clip says so rather than pretending a paste
  happened.

## 13. Settings

Open Settings from the gear in the list, from the tray menu, or by pressing the gear. Settings has
sections down the left: **General**, **Capture**, **Appearance**, **Notifications**, **Storage** and
**About**. **Save changes** keeps everything on the open page; zoom applies the moment you pick it.

- **General**
  - *Launch at login* — start Clip with Windows. On for fresh installs; mirrors the tray
    menu switch, and the tick always shows the real startup entry.
  - *Start hidden in the tray* — fixed on in this release; Clip always starts in the tray.
  - *Open Clip hotkey* — click the field, then press the new combination. If another app owns it,
    Clip keeps the old one and tells you.
  - *Time format* — 12-hour or 24-hour clock on every row.
- **Capture**
  - *History limit* — how many clips **Inbox** keeps (100 to 5,000, default 200). When it is full,
    the oldest unfiled clips go first; a clip you just re-copied counts as newest. Your own tabs
    and Pinned are not counted.
  - *Show images in Inbox* — when off, picture clips appear only in the **Images** tab.
  - *Plain text only* — fixed off in this release; rich capture is always on (use Ctrl+Shift+V
    when you want a plain paste).
- **Appearance**
  - *Theme* — Light, Dark or System (follows Windows while System is chosen).
  - *Zoom* — 80%, 100%, 125% or 150%. It changes on screen as you pick it and is saved at once;
    **Ctrl +** and **Ctrl −** step it, **Ctrl+0** returns to 100%.
- **Notifications** — the toast that confirms each capture.
  - *Toast lines* (1–10) — the longest a capture toast may grow. The toast fits its content both
    ways: a short copy gets a narrow, short toast; a long one widens to the maximum, wraps, and
    stops at the line cap. **Preview** shows the real toast, where it will appear, with your
    current settings.
- **Storage**
  - Everything lives in one storage file with a 1 GB guide, far above normal use (a full day of
    copying is a few MB). If it is ever crossed, Clip says so the next time the list opens and
    suggests deleting clips you no longer need or clearing history. Copies keep being saved either
    way — nothing is ever dropped because of the guide.
  - *Clear history* — deletes the clips in **Inbox** and the image history, and compacts the file.
    Pinned clips and your tabs stay. The row shows how much storage your clips use out of the
    1 GB guide. It asks first and cannot be undone.
- **About** — *Known limits*: colour emoji in the preview, HTML from Google Docs or Notion, OCR and
  amounts, and Excel's leading-zero rule for CSV.

## 14. Keyboard map

The same list lives inside the app under Settings → **Shortcuts**; the global hotkeys there show
your current bindings.

| Key | Action |
|---|---|
| Alt+V | show Clip |
| Ctrl+Shift+V | open the list to paste as plain text |
| Enter | paste the selected clip |
| Ctrl+Shift+V (in the list) | paste the selected clip as plain text |
| Ctrl+1 … Ctrl+9 | paste row 1 to 9 (a bare digit searches) |
| ↑ / ↓ | move the selection |
| Shift+↑ / Shift+↓ | select a range of rows |
| Home / End | first / last row |
| PageUp / PageDown | move one page |
| Ctrl+↑ / Ctrl+↓ | previous / next day |
| ← / → / Ctrl+Tab | change tab |
| Ctrl+Shift+← / Ctrl+Shift+→ | move the clip to the next tab |
| type | search from anywhere in the list |
| Ctrl+K or Ctrl+F | focus the search box |
| Ctrl+T | filter by tag |
| Esc | clear the search, then hide Clip |
| F2 | open the editor / close the editor |
| Ctrl+S (in the editor) | save and keep editing |
| Del | delete the selected clips (asks first) |
| Ctrl+Z | undo the delete |
| Ctrl+P | pin or unpin the selected clip |
| Ctrl+M | move the selected clip to a tab |
| Ctrl+E | save the selected clip as a file |
| Ctrl+0 | zoom to 100% |
| Ctrl++ / Ctrl+− | step the zoom |
| ↑↓ Enter Esc (in dialogs) | choose, confirm, close |

## 15. Where your data lives

Everything is in your own Windows profile, under `%APPDATA%\Clip\`:

- `clip.db` — the clips, tabs and text read from images.
- `config.json` — your settings.
- `webview\` — the profile the window uses.
- `crash.log` — written only if something fails unexpectedly.

There is no cloud sync and no telemetry. If the database is damaged, Clip does not delete it: the
file is set aside with a `.corrupt-<date>-<time>` ending, a fresh history is started, and a
notification says **"Clip history could not be read and was moved aside. A fresh history has
started."** The old file remains in that folder if it ever needs to be recovered.

This is the only Clip window: the older WPF window is gone from this release.
