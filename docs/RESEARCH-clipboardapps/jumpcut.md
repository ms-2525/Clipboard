# Jumpcut (macOS) — UI/UX audit for Clip

**Identity.** Jumpcut, by Steve Cook. Canonical current repo: **github.com/snark/jumpcut** (676★, MIT, Swift, v0.84 Dec 2023; pre-0.80 was Objective-C). Site: https://snark.github.io/jumpcut. Lineage: original SourceForge app → drdrang / Flycut / Soarcut forks. Read, not run: source at `master`, plus archived screenshots of the 0.63 bezel.
Sources: `README.md`; `Jumpcut/Jumpcut/Bezel.swift`, `Interactions.swift`, `Clippings.swift`, `MenuManager.swift`, `Pasteboard.swift`, `Settings.swift` (all under https://github.com/snark/jumpcut/blob/master/Jumpcut/Jumpcut/).

## 1. "List" anatomy — there is no list

The primary surface is a **single-item pager, not a row list**. Clips are never listed together in the bezel.

| Part | Value | Source |
|---|---|---|
| Panel | 325×325, corner radius 25, alpha 0.80, borderless, no shadow, `level = .modalPanel` | `Bezel.defaultAppearance`, `buildWindow` |
| Panel fill | white 0.1 @ 0.60 (outlet 0.1 @ 0.90) | `AppearanceAttributes` |
| Text box | 300×200, radius 12, **1 px black border**, bottom-anchored 10 px | `Bezel.init` constraints |
| Number chip | 36×30, radius 12, centered **above** the text box, 10 px gap | `secondaryOutlet` |
| Row height | n/a — one clip at a time. The only list is the status-bar `NSMenu` (native rows ≈22 pt) | `MenuManager.rebuild` |
| Font | system font (SF Pro) **14 pt fixed**, line spacing 1.2, single text color (white) | `Outlet.init(fontSize:)`, `setText` |
| Per-clip chrome | **none** — no source app, no time, no size, no icons, no kind badge | whole file |

So each "row" = the clip's full text, wrapped, filling a 300×200 card, plus one number above it. Nothing else.
Empty space: the text box is bottom-anchored, so ~75 px at the top of the 325 square is dead (legacy scissors watermark zone).

**Selection treatment:** the selected clip is the *only* clip on screen. Context comes from the number chip ("3 of 99"), not from neighbours. In the 0.63 screenshot the same idea was done more richly: adjacent clips showed through as ghosted text above/below the highlighted clip inside a translucent panel (https://leancrew.com/all-this/images/jumpcut-orig.png). Modern 0.8x dropped the ghosting to a plain card + chip.

**Numbering → quick-paste:** yes, and it is the core of the flow. Chip shows 1-based position; digits `1..9` jump to that slot, `0` = slot 10 (`handleBezelNumber`). Menu items are *not* numbered and carry no key equivalents.

## 2. Rich text

Flattened at capture. `Pasteboard.pollPasteboard` reads only `item.string(forType: .string)`; `Clippings.Clipping` stores `let Contents: String`; the bezel builds one `NSAttributedString` whose only attributes are font/color/paragraph style (`Outlet.setText`). **No rich rendering exists anywhere** — bold, links, lists, and tables arrive as plain text. Writing back is also plain: `gpb.setString(text, forType: .string)`. The app's own comment: `JCListItem Type = "NSStringPboardType"`.

## 3. Images

**Not supported, not captured, not displayed.** No `tiff`/`png`/`NSImage` path in `Pasteboard.swift`; the poll loop `break`s on the first string it finds and never inspects image types; the store is a text array; the bezel text view has no image attachment code. Files (paths) are also not treated specially — they'd only appear if the source app also put a string on the pasteboard.

## 4. UX model — what makes it fast

- Hotkey (default **Ctrl+Opt+V**) shows the bezel at screen centre with the clip you used last (`stack.position` persists across invocations).
- **Non-sticky by default**: keep the modifiers down and the bezel stays; the *same non-modifier key* steps down the stack, +Shift steps up, arrows/PgUp/PgDn/Home/End also work; **releasing the modifiers commits** (`metaKeyReleaseHandler` → `bezelSelection`). Option "sticky bezel" requires Return/Escape instead.
- Commit = put text on pasteboard, then `fakeCommandV()` **200 ms** later (`Interactions.paste`). One gesture, nothing to confirm.
- Escape cancels, Delete kills the current clip, optional wrap-around, optional "move used clip to top".
- **No search, no filter, no typing to narrow.** `bezelKeyDownBehavior`'s `default:` branch does nothing — letters are inert by design. Fast for "the last 5 things", dead for "that thing from Tuesday" (history is 99 by default, `rememberNum`).

## 5. Visual craft

**Holds up**
- Radical restraint: 3 appearance preferences total (status icon, hide icon, text alignment). Nothing to configure, nothing to break.
- Number chip above the text, out of the reading column: you always know *where you are* without a scrollbar or a highlight.
- **Smart alignment** (`smartAlign`): clips ≤100 chars with no newline are centred; longer/multi-line go left. Short grabs (an address, a code, a URL) read like an object; paragraphs read like prose. Cheap, genuinely good.
- Content at 14 pt with 1.2 leading in a 300 pt column — a real reading size, not a 12 px thumbnail of text.
- One colour, no accent, no icon per item: nothing competes with the clip itself.

**Dated**
- 2009 HUD: 325 pt square, 25 pt radius, 0.80 alpha, no shadow, fixed dark only (no light appearance, no theme), 1 px **black** border on a near-black card (invisible), and translucency that lets the document behind bleed through the text (visible in https://leancrew.com/all-this/images/jumpcut-wide-window.png).
- Appears at **screen centre**, not at the caret/cursor.
- ~75 px of empty space at the top of every panel; no motion/transition on show or step.
- Font size and monospace are plumbed (`OutletFontConfig`) but unreachable from Preferences — a dead knob.
- Menu rows are `shortenedText`: **first line only, 40 chars + "…"** (`Clipping.init`) — every long clip looks like every other long clip.

## 5 things Clip MUST steal (WPF-implementable)

1. **Hold-mods-to-browse, release-to-commit.** Open on hotkey → `Keyboard.Modifiers` still held → arrow/digit steps the selection → the moment `Keyboard.Modifiers == ModifierKeys.None` (PreviewKeyUp / DispatcherTimer poll), paste the selection into the previous foreground window. Removes the Enter keystroke from the hot path. Clip already has `HotKeyWindow` + `PasteInjector`; this is one state machine in code-behind.
2. **Same-key cycling with wrap.** Pressing the hotkey again while open steps down one item (Shift+hotkey steps up), so "second-most-recent" is two taps of one chord. Pure `Stack<int>` index + modulo; no UI work.
3. **Always-visible position number that *is* the shortcut.** A 36×30 rounded chip showing the 1-based index, and `D1..D9`/`D0` jump to slots 1–10. In Clip's list, put the chip in a fixed left gutter per row (Border + TextBlock), and handle `Key.D0..D9` in the same PreviewKeyDown that already does arrows. Gives direct access without a search box.
4. **Smart alignment on the preview pane.** Short single-line clip → centred; anything >100 chars or with a newline → left, 1.2 line height, one real reading size (14 pt equivalent). Compute `TextAlignment`/`LineHeight` in code-behind from `text.Length`/`Contains('\n')` — three lines, and it makes both a phone number and a paragraph look intentional.
5. **Persist the last-used index + optional move-to-top.** Reopen where you left off; a "move used item to top" toggle reorders after paste. Turns the list into a recency stack you can drive blind, and it is the difference between "browse each time" and "muscle memory". `ClipboardStore` needs only a saved index in config plus one UPDATE of `sort_order`.

(Honest note: Jumpcut is thin — text only, no images, no rich text, no search, one window. Items 1–5 are its *entire* transferable design, and all five are behaviour, not layout. Everything else it does, Clip already does better or shouldn't copy.)

## 3 anti-patterns

1. **Dogmatic single-item display.** One clip, no search, no filter, 99-item history. Great for the last five grabs; useless at item 60. Dr. Drang's 2009 review is the counter-example that aged the wrong way: LaunchBar's dense menu was worse, but "you can't see all the items" is a real cost (https://leancrew.com/all-this/2009/01/jumpcut/).
2. **Translucency as a style decision.** 0.80 alpha over arbitrary content means contrast is whatever happens to be behind the panel — the wide-window screenshot shows body text bleeding through the clip you're trying to read. Plus a 1 px black border that renders as nothing, and no light appearance at all. Clip should pick opaque surfaces and let the theme tokens own contrast.
3. **Flattening content into a 40-char one-line title.** `shortenedText` = first line, 40 chars, "…". Applied to a list (as Clip does), it produces rows that are indistinguishable, which then forces you to open each one — the exact reason a truncated list feels worse than a single big preview. Show the first meaningful line *plus one differentiator*, don't just chop.

## Screenshots (external, read-only)

- Modern site icon only: https://snark.github.io/jumpcut/assets/images/jumpcut.png (no current bezel screenshot ships in the repo — 0.8x geometry above is derived from `Bezel.swift`).
- 0.63 bezel, real screenshot (ghosted neighbours + number badge + centred clip): http://www.leancrew.com/all-this/images/jumpcut-orig.png
- drdrang wide/left-aligned variant (shows alpha bleed-through, badge placement): http://www.leancrew.com/all-this/images/jumpcut-wide-window.png
- Contrast case, LaunchBar's list with 40-char titles + source app + char count: http://www.leancrew.com/all-this/images/launchbar-clipboard.png
- Article carrying all three: https://leancrew.com/all-this/2009/01/jumpcut/
