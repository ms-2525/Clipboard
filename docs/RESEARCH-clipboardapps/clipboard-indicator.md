# Clipboard Indicator (GNOME Shell) — source audit

**Identity** `clipboard-indicator@tudmotu.com`, by Tudmotu. **License** MIT (`LICENSE.rst:1-3`).
**Platform** GNOME Shell 47–51 (`metadata.json:2-7`), EGO v74, ~2.67M downloads.
**Why it matters here** Closest living peer to Clip in *scope*: no window, no sync, no cloud — a panel button
plus one dropdown, ~1,100 lines of UI code and a 100-line stylesheet. Source read at commit `4fc7bea`
(2026-11-14): `extension.js`, `registry.js`, `stylesheet.css`, `README.md`.

**Correction to the brief:** there are *no* per-type icon badges. Type is shown by *content substitution*
and by *which action buttons exist* — see §2.

---

## 1. List anatomy (the dropdown)

- **Host** `PanelMenu.Button` — the dropdown is the panel menu itself, not a custom window (`extension.js:86-88`).
- **Order, top to bottom:** search entry → pinned/favorites scroll region → separator → history scroll
  region → separator → Private mode switch → Settings → Clear history (`extension.js:241-393`, `#showElements` 434-506).
- **Search row** `St.Entry` with `search-entry` style, magnifier primary icon, hint "Type here to search...",
  inserted at index 0 only when history is non-empty and private mode is off (`extension.js:245-261`, `446-452`).
- **Item** a plain `PopupMenuItem`: label = first 50 chars with all whitespace collapsed to single spaces
  (`MAX_ENTRY_LENGTH=50` line 28, `_truncate` 554-562), optional tag chip, right-aligned action buttons (§2),
  images replace the text label with a 1.5em square thumbnail (`_setEntryLabel` 564-579).
- **Selection** GNOME's radio `Ornament.DOT`, hidden on unselected rows by forcing ornament opacity 0
  (`836-838`, `939-955`) — the dot in the left gutter is the *only* "you are here" marker (see screenshot).
- **Sections** favorites and history are two independent `St.ScrollView`s, each capped at
  `max-height:450px` (`stylesheet.css` `.ci-history-menu-section`), so the menu never outgrows the screen.
- **Empty state** centered 5em paste icon + "Clipboard is empty" label (`396-408`, `stylesheet.css`).
- **Search behaviour** filtering sets `actor.visible` on existing rows — nothing is rebuilt, no re-query
  (`_onSearchTextChanged` 521-551); case-insensitive, optional regex, matches text **or** tag.

## 2. Rich text — what actually happens

- Capture asks the clipboard for mimetypes in a fixed priority order, plain text **first**, HTML **last**
  (`#getClipboardContent` 2001-2044). `ClipboardEntry.isText()` = `mimetype.startsWith('text/')` (`registry.js:315-319`).
- So a formatted clip is stored as whatever plain text the app offered; `text/html` is only a fallback and is
  then stored/displayed as a raw markup string. There is no rich rendering pass anywhere.
- **Type indication mechanism:** text entries get an *edit* button with `document-edit-symbolic`
  (`extension.js:728-744`); image entries get *no* edit button but an *image preview* button with
  `image-x-generic-symbolic` plus a thumbnail in the label slot (`710-726`, `564-579`). The row's
  *shape* (text line vs thumbnail) is the type signal. Nothing else marks formatting, files, or links.

## 3. Images

- Bytes cached to disk as one file per clip, filename = entry hash, so identical images dedupe by path
  (`registry.js:248-272`); prune-on-delete `274-279`.
- Menu thumbnail: `Gio.icon_new_for_string(path)` → `St.Icon`, class `clipboard-menu-img-preview`
  (1.5em, 1px white border) (`registry.js:224-234`, `stylesheet.css`).
- Full preview: a monitor-sized overlay, 75% black, image scaled to fit ≤50% width / ≤40% height,
  click-anywhere or Esc closes, parent menu closed first (`#showImagePreview` 1734-1803).
- Top bar shows the live current image at 1em when display mode includes content (`#updateIndicatorContent` 183-208).
- Cost: decoding at capture time is the documented freeze cause ("Copying large images causes a short freeze", `README.md`).

## 4. UX model

- **Top bar** icon `edit-paste-symbolic` + optional truncated current clip (15 chars) or 1em image + down arrow;
  display mode 0=icon, 1=content, 2=both, 3=neither (`extension.js:40`, `137-162`).
- **Keyboard-first open**: on menu open, focus jumps to search when history exists, else to the selected row
  (`263-281`). Row keys: Delete remove, `p` pin, `v` paste, `t` tag, `h` image preview, `e` edit, Enter select
  (`639-690`; same list in `README.md`).
- **Favorites** = boolean per entry (`registry.js:323`), rendered in their own section above the separator,
  optionally pinned at the bottom (`PINNED_ON_BOTTOM` 313-320); pinning moves the row to front (`843-848`);
  deleting a pinned item can require confirmation (`642-648`).
- **Private mode** teaches the strongest lesson: it is a *state*, not a dialog. While on, capture is skipped
  (`1052`), both list sections are hidden (`1410-1412`), the panel hbox dims to 30% white
  (`stylesheet.css` `.private-mode`), the panel reads "..." (`188`), and on exit the previously selected
  clip is put back on the clipboard (`1414-1428`).
- **Excluded apps** by focus `wm_class` (`1054-1057`), plus auto-clear on interval/boot, history cap 15,
  oldest non-favorites dropped (`924-937`).

## 5. Visual craft — inherited vs replicable

Inherited from GNOME (free): Adwaita dark surface, 12px rounding, popup fade/slide, symbolic icon set,
row hover, focus rings, the 450px scroll cap, `Ornament.DOT` selection. Their CSS is ~100 lines of accents
only: tag chip, image borders, dimmed pin, monospace countdown, 5em empty-state glyph.

Natively replicable in WPF with no new dependency: monochrome icon row per item, dot selection marker,
tag chip (rounded Border), 5em empty-state glyph + one grey line, capped-height ScrollViewer with two zones,
search box pinned as the first focusable row, hint text inside the box.

## 5 things Clip MUST steal (all WPF-implementable)

1. **Type-as-shape, not type-as-badge.** Text rows show text, image rows show a small square thumbnail in the
   same slot, file rows should show an icon + filename. Only the action set changes per type (edit for text,
   preview for image). `_setEntryLabel` 564-579, buttons 710-744. In WPF: one `DataTemplateSelector`
   with thumbnail/text/file templates — cheaper and calmer than five colored pills.
2. **Search pinned at the top and focused on open.** One box, hint text, live filter that toggles visibility
   instead of rebuilding (`245-261`, `263-281`, `521-551`). WPF: focus the TextBox on `Opened`, filter a
   `CollectionView` — no virtualization churn, no re-query.
3. **Two-zone list with a hard height cap.** Pinned zone, separator, history zone, each its own scroller,
   combined max height ~450px (`285-320`, stylesheet). Clip's window should never grow past its cap;
   the list scrolls, the window does not.
4. **Private mode as a visible ambient state.** Grey/desaturate the tray icon, banner "Private mode — not
   saving", pause capture, and restore the previous clipboard on exit (`1410-1435`, `stylesheet.css`).
   This is the one feature that turns a history tool into something a professional dares to use all day.
5. **A single dot selection marker + per-row mnemonic keys.** One 4-6px dot in a fixed left gutter
   (`939-955`), and every row answers Delete / Enter / a letter without opening a menu (`639-690`).
   WPF: `IsSelected` trigger in the ItemContainerStyle + `KeyDown` on the list.

## 3 anti-patterns (do not copy)

1. **A wall of five always-visible glyph buttons per row** (edit, pin, paste, tag, delete — 705-827).
   In the screenshot they consume ~45% of every row's width and repeat 15 times; the label is what the
   user is scanning. Reveal actions on hover/selection, or collapse them into one overflow button.
2. **Rich text degraded to raw markup.** `text/html` is last in the mimetype list and then stored as a
   plain string (`2001-2014`, `registry.js:315-319`), so formatted clips appear as `<div style=...>` soup
   and dedupe compares markup, not content. Clip already parses RTF/HTML — never display raw tags, and
   store the plaintext form for search/dedupe.
3. **Toggling private mode silently rewrites the user's clipboard** (`1414-1428`: it re-copies the last
   selected entry, or clears the clipboard if none). A privacy switch must not mutate clipboard contents —
   it should only stop recording, and say so.

Bonus smell: mode state is a module-level `let PRIVATEMODE` (`33`), i.e. process-global UI state — Clip's
state belongs to one store/service, not a file-scope variable.

## Screenshots

- Menu with search, pinned text + two thumbnails, dot selection, tag chip, action icons, private/settings/clear rows:
  https://raw.githubusercontent.com/Tudmotu/gnome-shell-extension-clipboard-indicator/master/screenshot.png
- Live listing (EGO page, v74): https://extensions.gnome.org/extension/779/clipboard-indicator/
