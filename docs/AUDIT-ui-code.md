# AUDIT — v2 web UI, source only

**Date:** 25 Nov 2026 · **Mode:** read-only static audit · **Branch:** `v2-phase1`
**Worktree:** `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack`
**Scope:** `UI\web\src\` — `App.tsx` (876), `ClipList.tsx` (320), `rich.tsx` (310), `EditorOverlay.tsx` (261),
`SettingsDialog.tsx` (343), `host.ts` (361), `format.ts` (45), `mock.ts` (263), `styles.css` (1825), `fonts.css` (33).
`useClipboardKeys.ts` (171) is read only to answer the keyboard question; `DeleteConfirm.tsx`, `EditorToolbar.tsx`,
`icons.tsx` are read where a finding depends on them.
**Read first:** `docs\AUDIT-impeccable-web.md`, `docs\AUDIT-search-tags.md`. Nothing already fixed there is re-reported:
the row focus→selection P3, the `tags.ts` duplicate classifier, the accent/control tokens, the 10 px ramp, the
`::selection`/caret/scrollbar surfaces, and the Inter-not-served P2-5 are all **closed** and were not re-litigated.

**Method:** code reading plus `ripgrep` over the ten files, with `Host\ClipHost.cs`, `Host\ClipHost.Queries.cs`,
`Host\ClipHost.Actions.cs`, `Core\ClipboardStore.Tabs.cs` and `node_modules\dompurify` read as the contracts the page
is judged against. No browser, no app, no build, no edit to app code. Every quote below is verbatim from the file at
the stated line.

---

## Verdict by area

| # | Area | Verdict | One line |
|---|---|---|---|
| 1 | Accessibility in code | **FAIL** | Listbox/roving tabindex are correct, but tag chips are unreachable, the editor dialog has no trap or focus restore, and `Delete` deletes a clip while the caret is in the search box. |
| 2 | Sanitizer correctness | **PASS with P2** | Explicit tag/attr allow-list, style filtered per property, one path to `dangerouslySetInnerHTML`, no XSS found. The `data:` filter is broader than the product (`image/svg+xml` passes) and the row tooltip can hold the whole body. |
| 3 | State and memory | **RISK** | Save does not invalidate the body cache (`rich.tsx`), thumbnails are cached with no cap, and a delete decrements the result total twice. |
| 4 | Token hygiene | **PASS with P2** | Zero colour literals outside the two token blocks, `!important` only in reduced-motion. 12 static inline styles remain, one of them a full style object on a class (`tile`) that has no CSS rule. |
| 5 | Host contract drift | **RISK** | `ClipItem` ↔ `ItemDto` match field-for-field (one mock-only `kind`, H6). `Counts.today`/`pinned` are declared and never sent (dead “N today” chip); `moveToTab` has no caller. Timeouts are sane; no rejection is swallowed on a user path. |
| 6 | Hygiene | **FAIL (size)** | `App.tsx` 876 and `styles.css` 1825 against a 400-line budget; no `console.*`, no TODO/FIXME, one `eslint-disable`. `messageOf` has two homes. |

Count: **P0 0 · P1 3 · P2 11 · P3 12** (A6 is a composite of three small items counted once).

---

## 1. Accessibility in code — **FAIL**

What is right, so it is not re-opened: the list is a real `listbox` (`ClipList.tsx:222`) with `option` rows,
`aria-selected` (`253`), `aria-posinset`/`aria-setsize` (`254-255`), and a roving tabindex that yields exactly one
tab stop (`218`, `256`: `tabIndex={clip.id === tabbableId ? 0 : -1}`, `tabbableId = selectedId >= 0 ? selectedId : clips[0]?.id`).
Focus and selection are the same thing (`onFocus={() => onSelect(clip.id)}`, `260`), which closes the old P3-1.
The search input is labelled (`App.tsx:596` `aria-label="Search clips"`), every icon-only button is named
(`Clear search` `604`, `Toggle theme` `642`, `Settings` `657`, `Dismiss toast` `867`, `Close editor`
`EditorOverlay.tsx:166`), tabs are Radix `tablist`/`tab` (`App.tsx:676`), and the toast region is live
(`App.tsx:855` `<div className="toast-region" aria-live="polite">`).

### [A1] P1 — Tag chips are a mouse-only trap for keyboard and screen-reader users

`ClipList.tsx:284-299`

```tsx
{shownTags.map((id) => (
  <button key={id} type="button" className="chip tag" tabIndex={-1} ... onClick={(event) => {
    event.stopPropagation(); onTag(id)
  }}>
```

The chip is a `<button>` **inside** `role="option"`. `option` has `childrenPresentational: true` in ARIA 1.2, so the
button role is pruned: a screen reader hears the tag word as part of the row's name but cannot activate it. It is also
`tabIndex={-1}` and has no key handler, and the global matrix deliberately skips controls
(`useClipboardKeys.ts:120` `if (onControl) return`), so `Enter` pastes the clip instead. **Result: filtering by tag has
no keyboard path at all** (WCAG 2.1.1). The chip's `onMouseDown` preventDefault (`291`) makes the mouse path feel
right, which hides the gap in every screenshot.

**Smallest correct fix (one file):** emit the chips as non-interactive `<span className="chip tag">` so the option no
longer owns a button, and give the focused row an `onKeyDown` that calls `onTag(shownTags[0])` for `Alt+Enter` and
`event.stopPropagation()` (the row handler runs before the window listener, so the paste binding is not also fired).
Alternative: move the chip strip out of the option into a sibling `role="group"` with ordinary tab stops.

### [A2] P1 — The editor dialog declares `aria-modal` and does not trap or restore focus

`EditorOverlay.tsx:164`

```tsx
<div className="editor-overlay" role="dialog" aria-modal="true" aria-label="Clip editor">
```

This is a hand-rolled overlay, not a Radix dialog. There is no focus scope, no `inert`/`aria-hidden` on `.app`
behind it, and no restore on close (`onClose` at `App.tsx:851` only sets `editorOpen` false; the focused editor
node unmounts, so focus falls to `<body>`). A keyboard user can `Tab` straight out of the “modal” into the toolbar
and list behind it, and screen-reader navigation is not confined either. Compare the two dialogs that do it right:
`SettingsDialog.tsx:150` and `DeleteConfirm.tsx:17` are both `Dialog.Root` (focus trap, Escape, restore, `aria-modal`).
`Esc` is handled by a separate window listener (`EditorOverlay.tsx:103-117`), which is exactly the work Radix already does.

**Smallest correct fix:** wrap the existing markup in `Dialog.Root open` + `Dialog.Portal` + `Dialog.Content`
(as `DeleteConfirm` does) and keep the inline `discard` cascade; delete the `Escape` window listener. If the overlay
must stay a `div`, add `useEffect` to focus the first control, `keydown` Tab cycling inside `overlayRef`, and
`returnFocus.focus()` on unmount — three new pieces instead of one wrapper.

### [A3] P1 — `Delete` in the search box opens the delete confirm for the selected clip

`useClipboardKeys.ts:142-148`, reached from `App.tsx:490` (`enabled: !editorOpen && !settingsOpen && ...` is **true**
while focus is in the search box):

```ts
if (key === 'Delete') {
  if (selected) { take(); actions.onDelete() }
  return
}
```

`typing` is computed (`43`) but the navigation and action blocks run **before** the typing guard at `159`
(`if (inSearch || typing) return`). So `Delete` — the key a person presses to erase one character — does not erase it
and raises “Delete this clip?” for whatever row is selected. `Home`/`End` (`102-106`) are also intercepted, so the caret
cannot be sent to the start or end of the query. (`ArrowUp`/`ArrowDown`/`PageUp`/`PageDown` are deliberately allowed
from the box — the comment at `89` says so, and those keys do nothing in a single-line input, so they are not part of
this finding.) `DeleteConfirm` focuses Cancel, so the damage is one more `Enter` away, but the mapping is wrong.

**Smallest correct fix:** keep the deliberate arrow/page navigation, and guard only the keys that belong to a text
field: before the navigation block, `if (typing && (key === 'Delete' || key === 'Home' || key === 'End')) return`.
`Escape` still clears the search from the box, and `Delete` returns to erasing characters.

### [A4] P2 — Radio-style controls expose no state to assistive tech

`SettingsDialog.tsx:260-271` (theme) and `273-292` (zoom, time) are plain buttons carrying only a visual flag:

```tsx
<button className="radio-card" data-on={themeChoice === 'light'} onClick={() => pickTheme('light')} type="button">
```

No `role="radio"`, no `aria-checked`, no `aria-pressed`, no group role. A screen reader announces “Light, button” for
all three themes and cannot say which one is live. `aria-label="Zoom"` on the wrapping `<div>` (`273`) is inert — a
`div` with no role ignores it. `data-disabled="true"` rows (`182`, `246`) are likewise only a title.

**Smallest correct fix:** `aria-pressed={themeChoice === 'light'}` on each of the three groups (a toggle-button group
is a valid pattern and needs no new component), and drop the useless `aria-label` or give the row `role="group"`.

### [A5] P2 — Settings dialog opens with focus left on the trigger

`SettingsDialog.tsx:153`

```tsx
<Dialog.Content className="dialog" aria-describedby="settings-desc" onOpenAutoFocus={(event) => event.preventDefault()}>
```

`preventDefault` with no replacement focus means the dialog mounts with focus still on the topbar Settings button
behind the scrim. Radix traps once focus is inside; until the user tabs, the dialog is not announced and the first
`Tab` order is undefined. Either delete the `preventDefault` (Radix would focus the first control) or focus the
dialog's heading (`tabIndex={-1}`).

### [A6] P3 — Smaller a11y debt

- Empty state lives inside the listbox: `ClipList.tsx:222-231` — the `role="listbox"` contains a `div.empty`, so with
  zero clips the listbox has no options and the “No clips yet” sentence may not be announced. Give the empty `div`
  `role="status"` or render it as a sibling of `.list`.
- Row tooltips are native `title` only (`ClipList.tsx:273,277,284,299`; `rich.tsx:273,299`): no keyboard or touch path.
  Acceptable, but it is the tooltip mechanism the design language uses for full text.
- `dirty-dot` (`EditorOverlay.tsx:172`) is an empty span with a `title`; the adjacent “Unsaved” chip carries the meaning.

---

## 2. Sanitizer correctness — **PASS with P2**

Verified in `rich.tsx`:

- **Explicit allow-list, not defaults** — `171-178`: `ALLOWED_TAGS: TAGS` (a 25-tag list, `14-18`), `ALLOWED_ATTR: ATTRS`
  (`19`), `ALLOW_DATA_ATTR: false`, `ALLOW_ARIA_ATTR: false`, plus `FORBID_TAGS`. `script/style/link/meta/iframe/object/
  embed/form/input/svg/math/template/base/audio/video/source/select/textarea/button` are removed **before** parsing
  (`BLOCKED`, `141`, applied at `150`) *and* forbidden again at sanitize time. No `ADD_TAGS`, no `ALLOW_UNKNOWN_PROTOCOLS`.
- **`style` is filtered by property, not just attribute** — `88-119`: `cleanStyle` splits the declaration, keeps only
  `PROPS` (`22-25`), maps/normalises every property that reaches the output (`font-weight`, `font-size`, `font-style`,
  `text-align`, `text-decoration`, `white-space`), and rejects values with `SAFE_VALUE` (`28`) before rebuilding the
  attribute from a `Map`. Colours survive only through `rgbOf` → canvas → `#rrggbb` and a 4.5:1 proof on both row
  surfaces (`107-116`). There is no declaration path that keeps the source string verbatim except `font-family`, whose
  value cannot leave the property without `url(`, `expression`, `<`, `>`, `\` — all rejected.
- **One path to `dangerouslySetInnerHTML`** — `rich.tsx:307` `dangerouslySetInnerHTML={{ __html: safe }}`, and `safe`
  is `useMemo(() => (body?.html ? toRichHtml(body.html) : ''), [body])` (`266`). Nothing else in the ten files writes
  inner HTML except the constant `PLACEHOLDER` (`37`, assigned at `194`). `EditorOverlay` feeds `setContent(html)` into
  TipTap, which parses into its own schema — not an inner-HTML sink.
- **The tooltip is safe** — `rich.tsx:267,273` `title={plain}` is a React attribute, HTML-escaped, and comes from text.
  (Its *size* is the problem, see S4.)
- **The Word normaliser is regex-safe on 1 MB** — `cfHtmlFragment` (`122-139`) builds two `RegExp`s from constant names
  (`StartHTML`, `EndHTML`) with `m` + `^…$`, so each scan is anchored per-line, and the offsets are applied with
  `TextEncoder`/`TextDecoder` slices. `SAFE_VALUE`'s `(?!.*(?:…))` lookahead is linear per declaration and has no
  nested quantifier over user input. `cleanStyle` splits on `;` before testing, so a 1 MB style blob cannot trigger a
  backtracking blow-up.

### [S1] P2 — `data:image/svg+xml` passes the `src` filter

`rich.tsx:167`

```ts
if (data.attrName === 'src' && !/^data:image\//i.test(data.attrValue)) data.keepAttr = false
```

`data:image/svg+xml,<svg …>` satisfies that regex. DOMPurify then allows it too: its `_isValidAttribute` has a
data-URI bypass for `src` on any tag in `DEFAULT_DATA_URI_TAGS`, which includes `img`
(`node_modules\dompurify\dist\purify.cjs.js:1271-1275`, `2225`:

```js
if ((lcName === "src" || …) && lcTag !== "script" && stringIndexOf(value, "data:") === 0 && DATA_URI_TAGS[lcTag]) return true;
```

). So the SVG survives as `<img src="data:image/svg+xml,…">`. In an `<img>` Chromium renders SVG in secure static mode
(no script, no external fetch), so this is **not** the script vector the prompt asks about — but the allow-list is wider
than anything Clip produces (clipboard images and `getThumb` are raster/PNG), and it is a rasteriser/decoder surface
inside a row that the product never wanted.

**Smallest correct fix:** `/^data:image\/(?:png|jpe?g|gif|webp);base64,/i` at `rich.tsx:167` and `188`.

### [S2] P3 — the `href` half of the hook is dead code

`rich.tsx:166` restricts `href` to `^https?://`, then `182-185` removes `href` from **every** anchor and sets
`tabindex="-1"`. The hook branch cannot change an output. Keeping it implies links are safe to keep, which they are not
meant to be. Delete the line (or keep `href` out of `ATTRS` altogether) so the next reader does not reason from it.

### [S3] P3 — Sanitising cost per mounted rich row, unmeasured

`toRichHtml` does `DOMParser` → `cleanStyle` over every element → DOMPurify → a second `DOMParser` (`148-198`). The
list mounts ~10 rows (`overscan: 4`), so opening a page of rich clips runs this on up to 10 bodies of up to the host's
2 MB HTML limit (`EditorOverlay.tsx:37`) on the main thread. No measurement without the app; flagged, not scored.

### [S4] P2 — The row tooltip can carry the whole body

`rich.tsx:267` `const plain = body?.text || clip.text || clip.title`, used as `title={title}` (`273`, `299`, `307`).
`body.text` is `getItem`'s full text, not the 1,200-char preview (`host.ts:83-89`; host returns `_store.GetText(id)`
untruncated, `Host\ClipHost.cs:114-120`). A 1 MB clip therefore puts a 1 MB attribute on a mounted row — duplicated in
the a11y tree, re-rendered on every row commit, and shown as a native tooltip.

**Smallest correct fix:** `const plain = (body?.text || clip.text || clip.title).slice(0, 300)`.

---

## 3. State and memory — **RISK**

Right: every listener and timer has a cleanup (`App.tsx:166, 399, 440, 572`; `ClipList.tsx:172, 195`), the request
sequence guards are correct (`App.tsx:192-206` search, `119/177/215` tab), `inflight` is cleared in `finally`
(`rich.tsx:234`), and the **captured-with-active-query** path is safe: a row that the query filtered out is not
re-inserted into the results — `App.tsx:523-527` only calls `moveToTop` when `current.items.some(row => row.id === item.id)`.
A stale body cannot attach to a *reused* row either, because the virtualizer keys by clip id
(`ClipList.tsx:85` `getItemKey: (i) => clips[i].id`) and React remounts on a key change.

### [M1] P2 — Saving an edit does not invalidate the cached body

`rich.tsx:217` returns the cached body first:

```ts
if (bodyCache.has(id)) return Promise.resolve(bodyCache.get(id) ?? null)
```

`EditorOverlay` saves through `host.saveEditor` and `App.tsx:847-850` calls `onSaved` → `reload()` → new `items`
(so `clip.text` is the new preview) — but `RowBody` still prefers the cached body (`rich.tsx:267`), and `bodyCache` has
no delete anywhere outside the failure path (`231`). The list therefore keeps rendering the **pre-edit** rich body and
the pre-edit `title` until the page is reloaded, while the header/footer say “Clip saved”. The host does emit `edited`
(`mock.ts:248`), and `host.ts:207` even normalises that event — but `App.tsx`'s event switch has no `edited` branch
(`512-570`), so nothing reacts.

**Smallest correct fix:** `export const forgetBody = (id: number) => { bodyCache.delete(id); inflight.delete(id) }` and
call it in `onSaved` (and in an `edited` event branch) before `reload()`.

### [M2] P2 — `thumbCache` is uncapped

`ClipList.tsx:8`

```ts
const thumbCache = new Map<number, string | null>()
```

Every image row ever scrolled past keeps its base64 thumbnail for the whole session. `bodyCache` got a 200-entry cap
(`rich.tsx:213`); this one got none. A 1,000-row history where many rows are screenshots holds every encoded thumb
(10–40 KB each → 10–40 MB), plus the decoded images the browser keeps.

**Smallest correct fix:** the same FIFO cap as `bodyCache` in the `.then` (`41-44`) — 200 entries, delete
`thumbCache.keys().next().value` before `set`.

### [M3] P2 — `bodyCache` is capped by count, not bytes

`rich.tsx:213-226`: 200 bodies, each `{ text, html, rtf }` returned whole by `getItem`. With the 2 MB HTML the editor
accepts, and RTF/HTML being several times the text, 200 entries can hold hundreds of MB. The cap reads as a memory
guard but only bounds the number of keys.

**Smallest correct fix:** track cumulative characters and evict from the front while over ~8 M chars (about four
full-size bodies), or lower `CACHE_MAX` to 50 — the list only ever mounts ~10 rows.

### [M4] P2 — A delete subtracts from the result total twice

`App.tsx:477` (local, optimistic) and `App.tsx:568` (event) both do
`total: Math.max(0, current.total - 1)`. The host emits `deleted` for the same delete —
`Host\ClipHost.Actions.cs:50` `OnUi(() => Emit("deleted", new { id }));` — and `App.tsx:474` says so in a comment
(“The host also emits `deleted`”). With a search active, `found.total` ends one short per deleted clip, so the footer
“N found” disagrees with the host from then on.

**Smallest correct fix:** drop the `total` arithmetic from the `deleted` event branch (it exists to cover a delete the
page did not initiate) — or from `confirmDelete` — and let exactly one of them own the count. Removing the row twice is
idempotent; the counter is not.

### [M5] P3 — Four copies of the selection-fallback rule

`App.tsx:128, 139, 183, 221` repeat
`setSelectedId((current) => (current != null && rows.some((row) => row.id === current) ? current : rows[0]?.id ?? null))`.
One `const keepSelection = (rows) => setSelectedId(...)` next to `moveToTop` removes the risk of one copy drifting.

### [M6] P3 — The theme button does not persist

`App.tsx:641` `onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}` changes page state only; no
`saveConfig`, unlike zoom (`applyZoom`, `409-425`) and unlike the Settings dialog. Relaunch reverts the choice, and
opening Settings afterwards shows the saved `themeChoice`, so pressing Save there silently undoes the toggle. Either
persist it (`host.saveConfig({ theme: next })`) or remove the button and point at Settings.

### [M7] P3 — First paint is always light

`App.tsx:71-74` initialises `theme` to `'light'` (the URL parameter only in mock mode) and the real theme arrives with
`init()` (`129`). A dark-theme user gets a light frame until the bridge answers. Cannot be seen without running; noted
because the mechanism is unambiguous. A static `data-theme` from the host (or a background colour on the WebView2
host) would close it.

---

## 4. Token hygiene — **PASS with P2**

- **Colour literals:** every `#rrggbb`/`rgba()` in `styles.css` is inside `:root` (`1-43`) or `[data-theme='dark']`
  (`45-86`). A full-file scan for `#hex|rgb|hsl` returns only those lines. Components contain no colour literals;
  `EditorOverlay.tsx:233` uses `var(--badge)`/`var(--fg55)` and `161` uses `var(--highlight)`. The two `color-mix()`
  uses (`styles.css:376,597`) mix tokens, not literals.
- **`!important`:** four uses, all inside `@media (prefers-reduced-motion: reduce)` (`1176-1179`) — the documented
  exception for overriding every animation, not a specificity escape hatch. PASS.
- **Duplicated CSS:** the only duplicated selectors are `.clip-rich th`, `.clip-rich code`, `.clip-rich table`, each
  split into two deliberate rules (`723-731`, `694-712`, `714-722`). No component re-implements a `styles.css` block.

### [T1] P2 — `className="tile"` has no CSS rule; its whole style is inline

`EditorOverlay.tsx:233`

```tsx
<span className="tile" style={{ width: 22, height: 22, borderRadius: 6, display: 'grid', placeItems: 'center', background: 'var(--badge)', color: 'var(--fg55)', fontSize: 11, fontWeight: 600 }}>
```

`.tile` does not exist in `styles.css` (grep for `^\.tile` returns nothing). Eight design decisions — size, radius,
layout, background, colour, type — live in a JSX string, invisible to the theme block and to the 400-line budget
review. This is the exact pattern the token rule exists to prevent.

**Smallest correct fix:** add `.tile { width:22px; height:22px; border-radius:6px; display:grid; place-items:center;
background:var(--badge); color:var(--fg55); font-size:11px; font-weight:600 }` to `styles.css` and leave
`className="tile"`.

### [T2] P3 — Twelve more static inline styles that belong in classes

`App.tsx:602` (`width:22,height:22,border:0`), `616` (`marginRight:2`), `687` (`marginLeft:6`), `752`/`765`
(`marginTop:4`, `maxWidth:380`), `768` (`marginTop:12`), `865` (`width:20,height:20`);
`ClipList.tsx:227`; `EditorOverlay.tsx:198,212` (`fontWeight:600`); `SettingsDialog.tsx:156,159`.

The genuinely dynamic ones are fine and should stay: `ClipList.tsx:233` (`height: virt.getTotalSize()`) and `258`
(`transform: translateY(...)`), `EditorOverlay.tsx:244` (`fontSize: size`). The rest are constants and drift the
moment a token changes. Prior-audit P3-4 (20 px toast target) is the same `inline width/height` at `App.tsx:865` — it
survives because the fix belongs in `.icon-btn.sm`.

### [T3] P3 — Two separate `@media (max-width: 560px)` blocks

`styles.css:1649` and `1688`. Merge; one breakpoint per block is the surrounding convention.

---

## 5. Host contract drift — **RISK**

### What matches exactly

`host.ts:15-34` vs `Host\ClipHost.Queries.cs:337-357`, field by field:

| TS `ClipItem` | C# `ItemDto` | Wire (camelCase) | Match |
|---|---|---|---|
| `id: number` | `long Id` | `id` | ✔ |
| `kind: ClipKind` | `string Kind` | `kind` | ✔ for `text`/`image`/`files`; the TS union's `'rich'` is **never sent** (see H6) |
| `app`, `title`, `time` `string` | `string App/Title/Time` | same | ✔ |
| `pinned: boolean` | `bool Pinned` | same | ✔ |
| `tabId: number \| null` | `long? TabId` | same | ✔ (serializer writes `null`) |
| `text: string` | `string Text` | same | ✔ |
| `hasImage/hasHtml/hasRtf` | `bool` ×3 | same | ✔ |
| `formats: string[]` | `List<string>` | same | ✔ |
| `tags: string[]` | `string[] Tags` | same | ✔ |
| `truncated: boolean` | `bool Truncated` | same | ✔ |
| `remainingChars: number` | `int RemainingChars` | same | ✔ |

`normalizeItem` (`host.ts:161-179`) defends the nulls the host can actually send (`AppName`, `WindowTitle`, `Tags`), so
a single bad row cannot break a page. `PageResult`/`SearchResult`/`ClipBody`/`HostTab` also match
(`Queries.cs:324-335`, `ClipHost.cs:114-120`, `ClipHost.cs:272-274`; tab `kind` is `custom`/`pinned`, confirmed in
`Core\ClipboardStore.Tabs.cs:46,63`).

### [H1] P2 — `Counts.today` and `Counts.pinned` are declared and never sent

`host.ts:43-51`

```ts
export interface Counts {
  clips?: number
  images?: number
  all?: number
  pinned?: number
  today?: number
}
```

The host sends exactly three (`Host\ClipHost.cs:265-270`: `new { clips, images, all = Math.Min(clips + images, …) }`).
`App.tsx:262` reads `init?.counts?.today` and `615-622` render the “N today” chip only when it is non-null — **that chip
can never appear in the real app**, and the mock does not send it either (`mock.ts:138-141`). Dead UI plus a DTO that
promises a field the contract does not have.

**Smallest correct fix:** delete `today`/`pinned` from `Counts` and the `today` chip from `App.tsx`, or add `today` to
`Counts()` in the host. Two files, one decision; do not leave the middle state.

### [H2] P3 — `moveToTab` is a bridge method with no caller

Declared `host.ts:121`, implemented at `host.ts:317-319` and `mock.ts:198-201`, dispatched at `ClipHost.cs:154`. No
call site in any `.tsx`. It is either a planned slice or dead weight; the audit only records that the contract surface
is wider than the UI.

### [H3] P2 — One 15 s timeout for everything except export

`host.ts:157-158, 223` `REQUEST_TIMEOUT_MS = 15000`; only `exportClip` overrides it (`328`, `10 * 60 * 1000`).
Judgement per method:

| Method | Host work | 15 s sane? |
|---|---|---|
| `init`, `getItems`, `search` | FIFO read thread; 10 k rows measured 330–390 ms (`AUDIT-search-tags` §6) | yes |
| `getItem` | one row + one meta read | yes |
| `getThumb` | one thumb, base64 | yes |
| `copy` | clipboard write | yes |
| `paste` | hides window, waits for the target app to focus (`ClipHost.cs:89`) | yes, but 15 s of a dead-looking window is long; 5 s + the existing copy fallback would feel better |
| `export` | **the owner types a file name** | correctly 10 min |
| `saveConfig`, `saveEditor`, `delete`, `restore`, `togglePin`, `clearHistory`, `hide` | local | yes |

OCR is not a bridge call (it runs at capture), so there is no OCR timeout to judge. The one structural gap: `getItems`
pages **serially** (`245-262`) at 15 s per page, so a stuck reader thread blocks the UI for `pages × 15 s` with only
“Loading history…” on screen and no cancel. Acceptable at the default 200; worth one line in the loading state if
`MaxItems` is ever raised.

### [H4] PASS — no user-initiated rejection is swallowed

`copyItem`, `pasteItem` (with copy fallback), `undoDelete`, `pinItem`, `hideWindow`, `saveClip`, `saveSettings`,
`clearHistory`, `confirmDelete`, `applyZoom` all `catch` into a toast or an inline `role="alert"`. The two silent
catches are deliberate, commented, and non-user-facing: the deep `getItems` that merely extends history
(`App.tsx:141-143`) and a missing thumbnail (`ClipList.tsx:45-48`). `delete`/`restore` toast “not ready yet” for the
not-yet-wired case via `isNotReady` (`App.tsx:54`).

### [H5] P3 — `isNotReady` is a string match on prose

`App.tsx:54` `/unknown method|not wired|not ready|phase 2/i.test(messageOf(error))`. It works today because the host
throws `"unknown method: " + method` (`ClipHost.cs:166`) and the mock says “Paste is not wired yet” (`mock.ts:176`), but
re-wording either message silently changes the toast. A stable error code in the envelope would remove the guess.

### [H6] P3 — `kind: 'rich'` is a fixture-only value

`host.ts:12` `export type ClipKind = 'text' | 'rich' | 'image' | 'files'`; grep for `"rich"` in `Core\` and `Host\`
returns nothing — `CaptureService.cs` writes only `text` (`76`), `files` (`96`) and `image` (`240`). A rich clip is a
`text` clip with `hasHtml`/`hasRtf`. The value exists only in the mock (`data.ts:72-144`), so in the real app
`EditorOverlay.tsx:34` labels every rich clip “Text”, and the preview's “Rich text” tile cannot be reproduced. Nothing
breaks — `isRich` correctly uses `hasHtml || hasRtf` (`rich.tsx:239`) and export uses `hasImage`/tags — but the union
describes a contract the host does not have.

**Smallest correct fix:** drop `'rich'` from `ClipKind` and from `kindLabel`, and make `data.ts` set `hasHtml` without
inventing a kind (the fixture already does both).

---

## 6. Hygiene — **FAIL (size only)**

- **No `console.*`, no `TODO`/`FIXME`/`XXX`/`HACK`, no `@ts-ignore`** in any of the ten files. One
  `eslint-disable-next-line react-hooks/exhaustive-deps` (`ClipList.tsx:154`) with the reason in the two comments above it.
- **Files over the 400-line budget:** `App.tsx` **876** (2.2×) and `styles.css` **1825** (4.6×). `ClipList.tsx` 320,
  `rich.tsx` 310, `SettingsDialog.tsx` 343 are inside. `App.tsx` carries data loading, search, events, zoom, export,
  delete, keyboard wiring and the whole topbar/list/footer markup — it is the file every future slice will touch and
  the one where a mistake costs a release.
- **Duplicated logic across the two files:** none worth a shared module. `compact()` (`ClipList.tsx:10`) and the
  footer's `toLocaleString` are different formats for different roles, not a duplicate. The real duplication is
  *inside* `App.tsx` (M5, M4).
- **Unused exports:** `Host.moveToTab` (H2); `pathsOf` (`rich.tsx:201`) is exported but only used in the same file;
  `toRichHtml` is exported with no consumer outside `rich.tsx`. Harmless, but three exports claim an API nobody calls.
- **Over-long line:** `EditorOverlay.tsx:233` (one 250-char style object). Already counted in T1.

### [Y1] P3 — Character count freezes after the first keystroke

`EditorOverlay.tsx:159`

```tsx
const characterCount = useMemo(() => editor?.getText().length ?? 0, [editor, dirty, loading])
```

`dirty` flips `false → true` once and then stays true, so subsequent typing does not recompute the memo. The footer
stops counting after the first edit.

**Smallest correct fix:** keep a `chars` state updated in the existing `onUpdate` (`EditorOverlay.tsx:63`) and render
that. The `useMemo` cannot work as written because its dependencies stop changing after the first edit.

---

## The three fixes I would insist on before shipping

1. **Make the keyboard honest about text fields (`useClipboardKeys.ts:142`).** `Delete`, `Home` and `End` must not act
   on the history while focus is in the search input. One guard above the navigation block returns normal caret
   editing and stops `Delete` from opening a destructive confirm. This is the only finding where a keystroke meant for
   the text being typed starts a delete.
2. **Give the editor overlay a real dialog (`EditorOverlay.tsx:164`).** It is the only surface that claims
   `aria-modal` and has no trap, no inert background, and no focus restore. Wrapping it in the `Dialog.Root` already
   used by `DeleteConfirm` is smaller than the hand-written trap and removes the separate Escape listener.
3. **Invalidate `bodyCache` on save (`rich.tsx:217`, `App.tsx:847`).** Today an edit is written, the toast says
   “Clip saved”, and the row keeps showing the old body until reload. One exported `forgetBody(id)` called from
   `onSaved` and from an `edited` event branch fixes it, and it is the difference between “saved” being true on screen
   and true in the store.

Runner-up, if a fourth is allowed: restrict the `src` allow-list to raster base64 (`rich.tsx:167`) — a one-line
tightening of the only filter standing between copied HTML and the DOM.

---

## What I could not verify without running the app

- **Anything rendered.** No screenshots, no computed styles, no live DOM, no WebView2. Contrast, focus-ring visibility,
  virtualiser measurements and the 10-rich-row sanitise cost are all outside this pass; the prior visual audit stands
  for colour.
- **The light-to-dark first-paint flash (M7)** — the mechanism is in the code, the visible duration is not.
- **`DOMPurify` runtime behaviour** is asserted from its shipped source (`node_modules\dompurify\dist\purify.cjs.js`),
  not from a call. The `data:` conclusion rests on lines `1271-1275` and `2225` of that file.
- **Does the host's `deleted` event arrive before or after `confirmDelete` resolves (M4)?** Either order double-counts,
  so the finding does not depend on it, but I did not observe the ordering.
- **Screen-reader output** for the nested tag chip is reasoned from ARIA 1.2 `childrenPresentational` and Chromium's
  pruning, not heard through NVDA.
- **`tsc --noEmit`** was not run (read-only, and the prior audit recorded 0 errors at an earlier commit).

**Skipped:** the app, the build, the browser, and any measurement. **Risk:** every finding here is a code-reading
finding; the accessibility ones (A1, A2) and the cache one (M1) are invisible in a screenshot and need a keyboard-only
pass and an edit-then-look check to confirm the fixes.
