# Parcellite (Linux/GNOME) — UI/UX audit for Clip

**Identity.** Parcellite ("lightweight GTK+ clipboard manager"), started 2007 by Gilberto "Xyhthyx" Miralla, now Doug Springer. Repo **github.com/rickyrockrat/parcellite** (275★, **GPL-3.0**, C + **GTK2**, ~8 source files, 110k lines total). Site: https://parcellite.sourceforge.net (redirects to sourceforge.net/projects/parcellite). Last commit 2025-12-17 (gcc-15 build fix) — *maintained, but the UI is frozen at the 2008 GTK2 idiom*. Linux/X11 only. **Read, not run**: source at `master` + the three official screenshots. Honest limit: the project publishes **one** screenshot of the popup, so the visual claims below rest on that one capture plus code.

Sources: `src/main.c` (all UI), `src/history.c` (store), `src/preferences.c` (settings/defaults), `src/history.h`, `README`, shipped screenshots.

## 1. "List" anatomy — there is no list; the history *is* a GtkMenu

Every open builds a fresh `gtk_menu_new()` and appends one `GtkMenuItem` per clip (`main.c:1911`, `:2005`). It is a **popup menu, not a window**: no title, no scrollbar, no resize, no search field, no hover state beyond GTK's highlight.

| Part | Value | Source |
|---|---|---|
| Row | one `GtkMenuItem` + plain `GtkLabel`; row height = GTK2 menu row (~22 px), no inner padding | `main.c:2004-2014` |
| Text budget | hard-truncated to **`item_length` = 50 chars** (range 5–200), default ellipsize = **MIDDLE**, so "`Parcellite is a lightweig…ory of clipboard entries.`" | `preferences.c:41,43`; `main.c:1963-1987` |
| Multi-line | newlines **erased** in display, label `single_line` = TRUE; a 20-line log clip becomes one 50-char line | `main.c:1988-2020` |
| Per-row extras | **none** — no icon, no type badge, no app/source, no timestamp, no index number, no size | whole file |
| Groups | normal + persistent lists, split by one `gtk_separator_menu_item_new()` | `main.c:2080-2092` |
| Selection | GTK menu highlight; on open, **first row pre-selected** (or the current clip if reverse-history) | `main.c:2128-2130` |
| State encoding | current **CLIPBOARD** entry = **bold**; current **PRIMARY** entry = *italic*; both via label markup | `main.c:2023-2042` |
| Footer rows | separator → `_Edit Clipboard` → `_Clear` | `main.c:2107-2117` |
| Empty state | one greyed row, label "Empty" | `main.c:2071-2073` |
| Search | none. Type-to-search jumps selection to first label matching the keystrokes; **no match paints the entire menu background literally red** | `main.c:1556-1609`, `:1594` |

## 2. Rich text — flattened with no preview

Capture only asks for plain text (`gtk_clipboard_wait_is_text_available` → `wait_for_text`, `main.c:305`). HTML/RTF clips arrive as unformatted text or not at all; there is no `text/html` target read anywhere in the source. **No preview exists** — no rendered formatting, no multi-line body, no image thumb. The only "rendering" is bold/italic markup used as a state marker, and an optional `nonprint_disp` that swaps control characters for display. Failures are visible: glyphs the widget font lacks print as empty boxes (`□`) in the shipped screenshot.

## 3. Images — not supported

`CLIP_TYPE_IMG 0x2` is defined in `history.h` but **never referenced** in any `.c` file. No `gtk_clipboard_wait_for_image`, no pixbuf capture, no thumbnails, no placeholder row. The pixbuf block in `history.c:239-257` is a *comment* describing a storage format that was never built. `content_exists()` (`main.c:276`) only asks whether *any* target exists, so an image copy is silently unrecorded — the previous text entry just stays. A user who copies a screenshot gets nothing and no explanation.

## 4. UX model — one click in, one click out

- **Hotkeys** (X11/keybinder): `Ctrl+Alt+H` history, `Ctrl+Alt+X` persistent history, `Ctrl+Alt+A` actions, `Ctrl+Alt+P` main menu (`preferences.c:44-47`); Alt-E edit / Alt-C clear inside the popup.
- **Click a row** = put that text on CLIPBOARD (and PRIMARY if enabled), menu closes. No paste happens unless *Auto Paste* is on, and that is **off by default**; when on it shells out to `xdotool` for Ctrl-V or middle-click (`main.c:1653-1710`).
- **Right-click a row** = only a tiny submenu, and only when persistent history is enabled: `Copy All to Clip` / `Move To Persistent|Normal` / optional `Edit` / `Cancel` (`main.c:1030-1058`). No "paste as plain text", no "pin", no "open in…".
- **Delete** = **Ctrl + right-click** on a row (marks it; real delete on menu close). Shift+right-click also marks. Undiscoverable, and there is no undo.
- **Clear** = whole-history wipe from the bottom `_Clear` row / Alt-C, with a "Clear the history?" confirm (suppressible by pref). No per-item delete button, no undo.
- **Edit** = modal 450×300 dialog titled "Editing Clipboard", bare `GtkTextView` + Cancel/OK; emptying the text **deletes** the entry (`main.c:808-845`).
- **Actions** = user shell commands against the current clip, stored in a binary file and managed in a 2-column `Action | Command` table (New/Edit/Delete/Up/Down).
- **History cap** = a plain spinner, default **25** items (`preferences.c:34`); duplicate copies move the existing entry to the top instead of adding a twin.

## 5. Verdict — why it feels dated

- **Chrome is stock GTK2, 1:1.** `GtkStatusIcon` tray icon, `GtkImageMenuItem` with stock art (broom for Clear, gear for Execute, floppy for Save As), stock Cancel/OK, notebook tabs "General"/"Actions". Nothing is designed; everything is borrowed from a toolkit that died.
- **Menu, not picker.** Because it is a real `GtkMenu`, it cannot scroll, resize, or hold a widget — so help text ships as a *fake row*: "`Use Alt-E to edit, Alt-C to clear`" sits where a search field or a footer belongs.
- **One typographic level.** Same ~8–10 pt system sans for every row; the only distinction is an accidental bold and an accidental italic. No hierarchy, no secondary line, no alignment columns.
- **No breathing room.** Rows are flush-left at ~22 px with zero vertical padding; a 410×342 capture carries a dozen clips with no grouping, no dividers, no hover affordance visible.
- **Feedback via literal `"red"`** (`gdk_color_parse("red")`) on the whole surface — not a theme token, unreadable on dark themes, and it *erases* the selection state instead of marking the miss.
- **Death by a thousand unpolished details:** shipped translations render the buttons as "`Cancel,`" and "`OK,`" (visible in the official screenshots), and 50-char middle-ellipsis cuts words in half.

## 5 things Clip MUST steal

1. **Row-line discipline.** Fixed char budget + single line + newlines stripped at *display* time (stored text untouched). A 20 000-char clip must never change row height. Clip should copy the rule and drop the 50-char limit for a real ellipsis.
2. **Keyboard-first open.** The popup opens with the newest row **already selected**; arrows + Enter work before the mouse moves. Zero-step default selection, one keystroke to commit.
3. **Type-to-jump.** Typing filters/jumps with no search box to click, Backspace backs out, and the current-clip row is the anchor when the buffer empties. Clip should steal the *gesture* and render the result properly (a highlight, not a red menu).
4. **Click = copy, never inject.** Row click puts text on the clipboard and closes; auto-paste is a separate opt-in. Clip must keep paste-on-select off by default — an accidental keystroke into the wrong window is the worst failure mode this class of app has.
5. **"What's on my clipboard right now" is marked.** Parcellite does it with bold/italic; Clip should do it with a badge — but the underlying rule ("the row you already hold is always visibly identified on open") is right and cheap.

**Not stolen:** its menu-as-list, its state colours, its destructive-action model, its editor, its Action-command table, its binary config format. Nothing else here is worth copying — the rest is an anti-reference.

## 3 anti-patterns

1. **Building the list out of `GtkMenu`.** No scroll, no resize, no search field, no per-row widgets, no content growing — and you end up shipping instructions *as a list item* ("Use Alt-E to edit"). Any list of user content must be a real scrollable container from day one.
2. **Colour as error feedback with a hardcoded colour name.** `set_widget_bg("red", menu)` on "no match" ignores the theme, is illegible in dark mode, and destroys the selection instead of marking what failed. Feedback belongs in a token-styled, local element.
3. **Power-features behind invisible chords and irreversible deletes.** Delete is Ctrl+right-click, clear-all is a modal confirm, edit-to-empty deletes the row, and none of it is undoable. If a destructive action has no visible affordance *and* no undo, users don't discover it — or they discover it by accident.

## Screenshots

- History popup (only UI capture published), 410×342 — https://a.fsdn.com/con/app/proj/parcellite/screenshots/186208.jpg
- Preferences › General (50-char budget, "Middle" ellipsis, hotkeys, save/confirm/reverse/single-line), 412×479 — https://a.fsdn.com/con/app/proj/parcellite/screenshots/186210.jpg
- Preferences › Actions table, 412×479 — https://a.fsdn.com/con/app/proj/parcellite/screenshots/186212.jpg
- Index: https://sourceforge.net/projects/parcellite/ · Code: https://github.com/rickyrockrat/parcellite/blob/master/src/main.c
