# Clip — Owner's Guide (plain English)

*Clip is a Windows clipboard manager. It remembers everything you copy (text, formatted text,
images, files) so you can paste it again later. It lives in the tray; press **Alt+V** anywhere
to open your clipboard list.*

## Everyday use
- **Copy anything** — it's captured automatically (a small silent notification confirms each capture).
- **Alt+V** opens the list, newest first, with a number next to each clip.
- **Type** to search; the list filters live. **Esc** once clears the search, twice closes the list.
- **Enter** pastes the selected clip into whatever you were working on. **Ctrl+1..9** pastes the
  1st–9th clip directly. **Shift+Enter** pastes as plain text (formatting stripped).
- **↑/↓** move through clips — the list never jumps or resizes. **←/→** always switch tabs.
- **F2** opens the selected clip in the editor (images open in your photo viewer instead).
  Edit freely — the toolbar has bold/italic/underline/strike, sizes, text colour, highlight,
  and clear-formatting. **Ctrl+S** saves back into the clip; **Esc** asks before discarding.
- **Ctrl+C** copies the selected clip back to the clipboard without pasting.
- **Ctrl+Z** brings back a clip you just deleted. **Delete** removes the selected clip.

## Tabs
- **Clipboard** (text + files), **Images** (big preview + filmstrip; ↑/↓ browses), **Pinned**
  (your keepers), plus your own tabs (**Ctrl+T** new, **Ctrl+W** delete empty, rename inline).
- **Ctrl+P** pins/unpins the selected clip. **Ctrl+M** moves it to any tab. **Ctrl+Shift+←/→**
  moves it between tabs.
- Clips in tabs and Pinned are never auto-deleted. The 200-clip cap applies to the main
  Clipboard tab only (default; can be changed).

## The list
- Formatted clips show their real formatting (colours, sizes, underline) right in the list.
- Long clips show their first lines (default 10 — change in Settings → "Lines shown") with a
  "+N more lines" badge. Hovering a cut-off clip shows its full text in a side panel outside the
  list window; stopping on it with the keyboard does the same.
- The bottom bar shows the selected clip's source app, window title, and time.
- Rows alternate two subtle shades with hairline dividers (the CopyQ Forest look).

## Ctrl+Shift+V (paste as plain text, anywhere)
Even with the list closed, **Ctrl+Shift+V** pastes your newest clip as plain text into whatever
has focus. Rebindable in Settings.

## Settings (tray icon → right-click → Settings)
- **General**: hotkeys, clips kept, lines shown, theme (dark/light), 12h/24h clock, start at sign-in.
- **Notification**: on/off, size, position, time on screen — with a live preview ghost you can
  drag to position exactly where you want it.
- All changes apply immediately — no restart. **Clear history** wipes the main list and images
  only; Pinned and your tabs survive.

## Good to know
- First launch: a small hint tells you it's running and the hotkey.
- Password managers and "clipboard-excluded" apps are never recorded.
- The first Alt+V after starting Windows takes ~half a second; every open after is instant.
- Clips live in `%APPDATA%\Clip\clip.db`; the whole app is that folder plus the exe.
- To quit: tray → right-click → Exit. Closing/hiding the list never exits the app.

## Your 3-minute check after reading this
1. Copy this sentence → Alt+V → it's at the top with your app's name in the bottom bar.
2. ↑/↓ through the list — nothing jumps. ←/→ moves between tabs.
3. F2 on it → bold a word → Ctrl+S → the list shows it bold now.
4. Copy a screenshot → Images tab → it's big; Enter pastes it into Paint.
5. Tray → Settings → Notification → "Show preview" → drag the ghost where you like it.
