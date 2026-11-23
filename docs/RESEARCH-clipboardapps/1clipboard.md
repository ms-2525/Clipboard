# 1Clipboard — research note (for Clip)

**Identity — the brief is wrong on four counts.** The URL in the brief (`github.com/1clipboard/1clipboard`) is **404**; the real repo is `github.com/wiziple/1Clipboard` (309★, 25 forks). Its public history is **5 commits, all README** — the source was never published ("I am currently working on this project from scratch… If you want to access the current private repo, please contact me", `README.md:14-16`). License is **MIT**, not Apache-2.0 (`README.md:27`; GitHub API `license: null`). Stack is **Electron + AngularJS 1.4**, not Angular 4 (`package.json`: `angular ^1.4.8`, sqlite3, lwip, msgpack, robotjs; UI kit = **Photon**, `src/assets/css/photon.min.css`).

So "read the source" came from the shipped build, not GitHub: `https://1clipboard.io/download/windows/1ClipboardSetup.exe` (52.2 MB, Squirrel, **v0.1.8**, 2016) → `7z` → nupkg → `resources/app.asar` (33.9 MB, 5,626 files) → unpacked to `%TEMP%\cb_src` (asar header = JSON at offset 16, `base = 8 + u32@4`). Paths below are relative to that `src\`. Windows + macOS; frameless **360×540** popup, `resizable:false` (`app.js createMainWindow`). A `<webview>` exists purely for Google OAuth (`main.sidebar.html`, `auth.google.html`) — never for clip content. **Project dead** (last release 0.1.8); **issue tracker and PRs wiped** — API returns `[]` for both, Wayback has no issues listing → no issue-derived bug list is recoverable (see Gaps).

---

## 1. Rich text in the list — it never rendered HTML; it printed a placeholder

- **No iframe, no sanitizer, no `innerHTML`.** Grep across all 56 source files for `ng-bind-html|ngSanitize|innerHTML|insertAdjacentHTML|sanitiz|DOMPurify` = **0 hits**. The row body is a plain interpolation (`main/main.home.html:1`): `<p class="text-item"><span title="{{item.bodyText}}">{{item.displayText}}</span>`.
- **An HTML-only clip renders as a literal string.** `include/xcp-core.js:271`: `e.type&TYPE_HTML&&(a="text", r="[Formatted Text]")`. Text icon, `bodyText` undefined, nothing else. That is the entire on-screen value of a formatted clip: 16 characters of English.
- **HTML is captured and restored losslessly, just never displayed.** `xcp-core.js:35` reads `text/html` into `htmlData`; `xcp-core.js:284` and `:293` write it back with `clipboard.write({html})`. "Store rich, render plain" — formatting survives the round-trip through the database but is invisible in the list. Most HTML clips also carry `text/plain`, so they look exactly like a text row.
- **What the user got, per type:** text → one clamped line; URL → same line, `#1d6dc2` underline, click opens external (`app.css:96-99`); colour → 14px swatch + hex (`app.css:100-105`); image → inline image; formatted text → `[Formatted Text]`.
- **Known rendering bugs:** the authoritative list is unrecoverable (tracker wiped). Code-visible defects instead: (a) `.listbox p{max-height:20px}` (`app.css:73-75`) + `.text-item span{display:list-item}` (`:93-95`) stack a colour clip's swatch and hex in a 20px box, clipping the hex; (b) 200-character truncation (`xcp-core.js:260`) is wasted work when CSS clamps the row to one line anyway.

## 2. Images

- Capture: PNG buffer plus a **230px-wide thumbnail** built with `lwip` (`PROGRAM_IMAGE_WIDTH=230`, `xcp-core.js:371`; `makeThumbnail` `:44-55`, landscape/portrait aware, skipped when both dimensions ≤230).
- Row `url` = data-URL of the **thumbnail** when present, else the full image (`xcp-core.js:271`); `<img ng-src="{{item.url}}">` inside `.image-item{max-height:none;padding:2px 0}` (`app.css:90-92`). No `max-width` anywhere (app.css has none, Photon has none) → a non-thumbnailed image renders at natural size and is **cropped** by `.listbox *{overflow:hidden}` (`app.css:65-69`). The marketing shot shows the good path: the 230px thumbnail.
- Gutter icon 32×32 sprite (`app.css:42-56`): `main_iconimage.png` for images, `main_icontext.png` for text, 13px globe overlay on synced rows. No zoom, no lightbox, no capture-side size cap — full PNG lives in SQLite (`imageData` BLOB) next to the thumbnail.

## 3. Editor

- **There is none.** `contenteditable|editor` = 0 hits; `edit` across all of `src` = 0 hits. No clip editing, no rich editor, no formatting toolbar. Windows: main 360×540, preferences 500×435, about 300×400, auth 700×525; preferences = hotkey keycaps, language, startup/history toggles. Rich content can only enter and leave through the system clipboard.

## 4. List anatomy & visual craft

Row = one `<li>` (`main/main.home.html:1`), classes `.listbox-item.list-2-line`:
- Shell `min-height:60px`, `padding:8px 8px 10px`, `border-bottom:1px solid #e9e9e9`, text `#414142`; hover `#f7f7f7`, focus `#f9f9f9` + `outline:none` (`app.css:76-86, 116-117`).
- Grid: `.listbox-item-parent{padding-left:40px}` + `.listbox-item-leftchild{position:absolute;left:0;display:flex;align-items:center}` → 32px icon column, one content line (`p{line-height:20px;max-height:20px;white-space:nowrap;text-overflow:ellipsis}`), then a 12px `#999` time-ago line. "2-line" = content + timestamp, **not** two lines of text.
- Trailing 16px star and delete, both `display:none` until `:hover` (`.show-hover`, `app.css:150-153`); star `#ffc90e`, delete hover `#fc605c`. Shortcut badge 16×16, `#666`, radius 2px, 11px bold.
- Type: body **12px**, `line-height:1.6`, `-apple-system/Segoe UI`; row lines 20px; topbar 11px/700 `#6c6c6c` on `#f0f0f0`, 26px tall. Chrome: 50px `#2c2c2c` icon rail (28px glyphs, `#aaa`→`#fff`), 7px bottombar, 256px watermark empty state (`app.css:196-272`).
- **Datedness:** Photon's 2015 Yosemite imitation — hairline 1px separators, zero radius, zero elevation, 12px type, sprite icons, hover-only affordances, fixed 360px width, no dark mode, no density option. The *density discipline* is the only part worth keeping.

## 5. Verdict — an anti-reference, but not for the assumed reason

It did not attempt rich-in-list and fail; it **declined to attempt it** and paid a different price: the list stayed fast and uniform, and every formatted clip became unidentifiable. The transferable lesson is not "an iframe per row is expensive" — it is **"a placeholder is also a rendering decision, and `[Formatted Text]` is the worst available one."** Degrade to *content*, never to a label.

---

## 5 things Clip MUST steal or avoid-learn

1. **STEAL — dual-track storage.** Keep the rich payload (HTML/RTF) as a BLOB and a plain-text twin for the row (`xcp-core.js:35`, `:271`). Clip already stores formats; the win is that the row can use the twin *and* a cheap style signal (bold/italic runs) instead of nothing.
2. **AVOID-LEARN — never swap content for a label.** If Clip cannot render a clip's appearance, show its words: first ~120 chars of the plain twin + a small `formatted` badge. 16 characters of English is strictly worse than 120 characters of the user's own text.
3. **STEAL — thumbnail at capture, not at paint.** 230px PNG made once with `lwip` and stored beside the original (`makeThumbnail`, `xcp-core.js:44-55`). WPF equivalent: `BitmapImage.DecodePixelWidth=230`, cache as PNG. This is the part 1Clipboard got right.
4. **STEAL — fixed row rhythm.** A 60px row = 20px content line + 20px meta line, so scanning never jumps (`.listbox-item` `app.css:76-81`). Clip's variable heights gain predictability; but **cap image rows at a fixed preview height** — 1Clipboard set `max-height:none` and got clipped images.
5. **AVOID-LEARN — 12px base + hover-only star/delete.** Hover-only hides affordances from keyboard users and from anyone who never parks the mouse; 12px at 96dpi is below 2026 comfort. Keep body ≥13px and give star/delete a quiet persistent presence.

## 3 anti-patterns

1. **Literal placeholder text for a content type** (`"[Formatted Text]"`, `xcp-core.js:271`) — zero information, and indistinguishable from a clip whose text really is that string.
2. **Two icon classes for five clip types** (`ng-class="item.icon==='text' ? 'text' : 'image'"`) — files, HTML and colours all collapse into "text"; the type is only recoverable from a tooltip (`title="{{item.bodyText}}"`). Type must be legible in the row.
3. **Hover-only actions in a fixed, non-resizable 360px panel** (`resizable:__debug` with `__debug=false`) — no user control of width or density, and destructive actions (delete) reachable only by mouse.

## Screenshots

- Main list, hero (shows text rows, one 230px image row, star, `[Formatted Text]`-class rows): https://1clipboard.io/images/app.png
- Search + "View Image Only" toggle + yellow match highlight: https://1clipboard.io/images/app2.png
- "View more" menu (Save Clipboard History toggle, Preferences, About, Exit): https://1clipboard.io/images/app3.png
- Repo copies (if the site ever dies): https://raw.githubusercontent.com/wiziple/1Clipboard/master/resources/github/app.png · `.../icon.png`
- Site: https://1clipboard.io/ · repo: https://github.com/wiziple/1Clipboard

## Gaps (honest)

- Issue tracker and PR list are **empty** (API `[]`), and Wayback holds no `/issues` listing snapshot, so no user-reported rendering-bug quotes exist to cite; §1's defects are read off the shipped CSS/JS and are labelled as such.
- Author's rewrite (Angular 4 + ngrx + PouchDB, per `README.md:10`) is private — never inspected; everything here describes the shipped 0.1.8 vanilla-JS build.
- `websearch` returned no results in this session, so no third-party reviews were cross-checked.
