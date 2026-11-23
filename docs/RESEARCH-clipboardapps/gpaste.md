# GPaste — research brief (for Clip)

**Identity:** GPaste 51.3 (`meson.build:2`), by Marc-Antoine Perennou. **License: BSD-2-Clause** (`COPYING:1`, every source header `SPDX-License-Identifier: BSD-2-Clause`) — not GPL, so patterns (not code) are freely copyable anyway.
**Platform:** Linux/GNOME only. C + GTK 4.24 + libadwaita 1.10 + D-Bus + systemd user unit + XDG desktop portal; needs `gtk4-x11`, `gcr-4`, libsodium, SQLite (`meson.build` deps). No Windows path. Architecture: daemon (`gpaste-daemon`) + D-Bus API + GTK app (`gpaste-ui`) + GNOME Shell extension + CLI.
**Read from:** clone at `C:\Users\mayan\AppData\Local\Temp\opencode\gpaste` (paths below are relative to repo root).

## 1. List anatomy

There is no popup-at-cursor in the Windows sense: the "popup" is GNOME Shell's panel menu, the full UI is a normal window. Both use the same row model.

- **Row = index · preview · one line of text · actions**, in a `GtkListView` with recycled row widgets (`src/ui/gpaste-ui-history.c:234-278`).
  - Index label: 3 chars wide, right-aligned, dimmed, 6 px margins (`src/ui/gpaste-ui-item.c:972-989`).
  - Preview slot (image thumbnail or colour swatch, never both) (`gpaste-ui-item.c:1014-1033`).
  - Text: `GtkInscription`, always **one line**, `ELLIPSIZE_END` (`gpaste-ui-item.c:990`).
  - Two hover-revealed flat buttons: delete, then star/pin; the star stays visible when pinned (`gpaste-ui-item.c:182-203`, `1039-1051`).
- **Row height:** not fixed. Text rows = font height + 12 px (index label 6 px top/bottom, `gpaste-ui-item.c:978-981`); image rows keep the thumbnail's 50 px (`gpaste-ui-item.c:125-131`). Mixed heights in one list.
- **Row 0 is bold** — it is what is on the clipboard right now (`gpaste-ui-item.c:679-682`).
- **Text shown:** `element-size` chars, default **80**, 0 = no limit, newlines/tabs folded to spaces, cut with `…` (`data/gsettings/org.gnome.GPaste.gschema.xml:12-19`; `src/libgpaste/gpaste-3/gpaste-util.c:380-404`).
- **Type indication is a translated text prefix, no icons:** `[Image, 600 × 450, 21/09/15 16:31:41]`, `[Color] #ff0000`, `[Password] Gmail`, `[Files] ~/Downloads`; plain text has no marker at all (`gpaste-util.c:527-556`).
- **Selection = activate = put back on clipboard**, then close the window (`close-on-select` default true) (`gpaste-ui-history.c:903-917`; `gschema.xml:63-69`). It never pastes into the app for you — you press Ctrl+V yourself. Shell menu does the same (`src/gnome-shell/item.js:533-544`).
- **Search:** `Ctrl+F` (or just typing) opens a `GtkSearchBar`+`GtkSearchEntry`; 150 ms debounce; Enter activates the first match (`src/ui/gpaste-ui-window.c:854-873`, `1799-1811`). Query is a **case-insensitive regex** over the item's value (for images, over the displayed line) (`src/libgpaste/gpaste-daemon/gpaste-history.c:3001-3065`).
- **Merge mode:** a separate selection mode; header becomes "N selected / Cancel", rows become checkable, an action bar offers a separator (newline/space/custom) and merges the picks into one item (`gpaste-ui-window.c:1444-1565`; `src/ui/gpaste-ui-header.c:235-294`).
- **Empty states — five distinct answers:** Loading…, "GPaste daemon not running" + Retry, "Could Not Load History" + Retry, "No Items / Copy something to start building your history.", "No Results", "No Pinned Items" (`gpaste-ui-history.c:152-175`, `700-715`; `src/gnome-shell/dummyHistoryItem.js:63-90`).

## 2. Rich text

**The list never renders formatted content.** Not anywhere in the UI: a formatted clip is a plain-text one-liner, identical to a plain clip.
- Row text comes from the item's display line, which is the plain value (`gpaste-ui-item.c:644-650`; `gpaste-util.c:576-577`).
- Rich text is *stored* (all clipboard flavours) and restored on paste — `rich-text-support`, default true (`gschema.xml:164-171`).
- The only UI affordance is a context-menu entry **"Remove Rich Text"**, offered on every text row because nothing on screen says which rows have rich formats (`gpaste-ui-item.c:397-412`, `1104`).
- No preview pane, no markup rendering: `grep -i "html|markup|rich" src/ui` finds only status-text escaping, not content rendering.

## 3. Images

- **No thumbnails are generated or stored.** On every row bind the row asks the daemon for the **full image over D-Bus**, receives a `GdkTexture`, and a `GtkPicture` (`CONTENT_FIT_CONTAIN`) shrinks it to `images-preview-size` — default **50 px**, range 16–200 (`gpaste-ui-item.c:113-132`, `637-668`; `gschema.xml:54-61`). Re-read each time the row scrolls back into view.
- Previews are skipped entirely when `images-preview` is off; turning it on reads only rows missing a texture (`gpaste-ui-item.c:136-150`).
- **Hover preview:** custom tooltip that re-renders the picture capped at **400 px** on the largest side, aspect preserved, cached per thumbnail (`gpaste-ui-item.c:268-325`).
- Colour clips get a swatch sized from the text line height, clamped 16–32 px (`gpaste-ui-item.c:96-111`).
- Shell menu: same idea, `St.Icon` from `Gio.BytesIcon` at 50 px, decoded uncached per fill (`src/gnome-shell/item.js:465-482`).
- Menu height capped at 60 % of the monitor work area (`src/gnome-shell/indicator.js:1342-1352`).

## 4. Editor

- One dialog for both new items and edits: wrapping, scrollable `GtkTextView`, heading + "Save", stays open with a banner on failure (`src/libgpaste/gpaste-gtk4/gpaste-gtk-util.c:919-940`).
- Guards: refusing to save identical text (a no-op `Replace` would still drop rich text and rewrite the head as plain text) (`src/ui/gpaste-ui-edit-item.c:44-64`); refusing to open/save any line past 50 000 chars, because a `GtkTextView` relayouts a whole line per keystroke (measured: 300k chars = 578 ms/keystroke) (`gpaste-gtk-util.c:614-636`).
- **What it preserves:** the item's uuid and its pin (daemon-side `Replace` carries the pin over); nothing else. **Rich text, HTML and every other format are lost on edit** — the replacement is plain text (`gpaste-ui-edit-item.c:50-53`).
- Editing is offered only for text and password items; a password opens the name/timeout composer, never the value (`gpaste-ui-item.c:165-180`, `340-344`). Images, files, colours and rich text: no editing at all.

## 5. UX model

**Good**
- `Ctrl+0…9` pastes the row at that index, and the index column shows the digit; works even for rows not yet loaded (`gpaste-ui-window.c:779-794`; `gpaste-ui-history.c:944-948`).
- The entire action set is reachable from the keyboard through the row's context menu — `Menu` or `Shift+F10` (`src/ui/gpaste-ui-history.h:15`; `gpaste-ui-item.c:446-463`).
- Actions stay hidden until hover but are held inside `GtkRevealer`s so rows never change height as the pointer moves (`gpaste-ui-item.c:182-203`) — a detail most clipboard UIs get wrong.
- Five differentiated empty/error states with a real Retry.
- Row 0 bold; close-on-select toggle; "pop first item off history" global shortcut.

**Over-engineered (the warning)**
- **~30 GSettings keys and four preference pages** for a clipboard list: element-size, growing-lines, three primary-selection keys, track-extension-state, max-memory-usage, min/max text size, password timeout, upload-command, 5-value storage backend enum, trim-items, an experimental in-shell daemon (`gschema.xml`; `src/libgpaste/gpaste-gtk4/gpaste-gtk-preferences-pages.c:19-25`). Plus 7 global shortcuts, a ~25-subcommand CLI, a D-Bus API, a search provider, three storage backends with a migration wizard. This is a platform.
- **Search is a regex.** Typing `(` or `[` returns nothing and never explains why; `a.b` matches `axb` (`gpaste-history.c:3001-3021`).
- **Modes multiply:** merge mode swaps the header, the list's selection model and the action bar, with its own focus-juggling rules (`gpaste-ui-header.c:235-274`; `gpaste-ui-window.c:1406-1565`), plus separate new-item, new-password, password-edit and text-edit dialogs.

## 5 things Clip MUST steal (all WPF-implementable)

1. **Bold first row + dim right-aligned index, with Ctrl+0…9 to activate row N.** Clipboard users paste the same few things; the digit column makes that a one-key action. WPF: bold `TextBlock` for row 0, 3-char dim column (fixed width so it never jitters), `KeyBinding`s on the window.
2. **Hover-revealed row actions that never change row height.** Put pin/delete in a fixed-width `Grid` column with `Visibility="Hidden"` (not `Collapsed`) toggled by a `ListBoxItem IsMouseOver` trigger.
3. **Five named empty/error states, not one blank list:** Loading…, "Couldn't load history" + Retry, "No items — copy something", "No results for <query>", "No pinned items". One overlay `ContentControl` with five templates.
4. **A type prefix on non-text clips instead of icons:** `[Image, 600×450]`, `[Files] C:\Users\…`, `[Colour] #FF8000`, `[Password] name`, and no prefix for text. Cheap, screen-reader-safe, scannable, and it survives a single-line layout.
5. **Image rows: small thumbnail leading the row, large hover preview.** 50 px inline (matches GPaste's default), ~400 px on hover, **decoded once and cached** — Clip's `Core\ImageCache` is the right home; GPaste's per-scroll re-read is the thing to *not* copy.

## 3 anti-patterns

1. **Platform creep.** 30 settings, 4 prefs pages, 3 storage backends, CLI + D-Bus + shell extension for one list. Clip has an owner who judges on screen; every extra preference page is a screen nobody asked for.
2. **Regex search as the default box.** No escaping, no hint, no feedback on an invalid pattern — a typed `(` silently means "no results". Use case-insensitive literal substring matching.
3. **Hidden modes and hidden affordances.** Merge is a separate mode with its own header; pin/delete exist only on hover or via a `Menu` key most users never press. Visible, always-present (but quiet) affordances beat discoverable-by-documentation ones.

## Screenshots

- GNOME Shell menu (popup anatomy: search row, one-line rows, hover ×, footer): <https://www.imagination-land.org/images/GPaste/3/Extension.png>
- Graphical tool, history list with index column + `[Password]` / `[Files]` / `[Image, …]` prefixes: <https://www.imagination-land.org/images/GPaste/3/Ui-1-full.png>
- Preferences pages (settings complexity, visibly): <https://www.imagination-land.org/images/GPaste/3/Ui-2-full.png>
- Release post with all four screenshots: <https://www.imagination-land.org/posts/2026-11-02-gpaste-51.3-released.html>

**Caveat:** those screenshots are from GPaste 3.18 (GTK 3). Current 51.x is GTK 4/libadwaita (header bar, 50 px inline thumbnails, status pages). The line-numbered source above is the authoritative description; the images are only for overall shape.
