# Clip v2 — owner requirements, current state, and what remains

**Written:** 25 Nov 2026, from the full build conversation. **Branch:** `v2-phase1`, worktree
`v2-stack`, HEAD `7ae506a` at writing. **Artifact at writing:** `dist\Clip.exe` (208,187,846 B,
published 09:13) — it does **not** yet include the newest page (Images tab, `tag:`, wrap, Ctrl-digits,
gear); a final rebuild follows the live sign-off.

This document is the acceptance spec. It says what was asked, what is built and proven, what is built
but not yet proven live, and what is genuinely missing.

---

## 1. Problem statement

The owner is a non-coder Chartered Accountant who relies on a clipboard manager all day. The old app
worked but could not be maintained by AI agents and had data-loss and layout defects. He asked for a
rebuild on a modern, AI-editable stack that keeps every trusted clipboard behaviour, adds reliable
automatic tags, tag search, image OCR and export, and is fully usable from the keyboard the instant it
appears. He judges only on-screen results.

The rebuild is code-complete for that brief, but three things were still missing or unproven when this
spec was written: a built-in **Images** tab, the explicit **`tag:`** search prefix (both were absent,
not hidden), and live proof of the newest keyboard rules on the final binary. Two environment blocks
(Office COM, no Hindi IME) and one budget decision (cold first open) remain.

## 2. Solution

One self-contained Windows executable: a thin WPF host with one warm WebView2 showing a React page,
over the unchanged, tested C# clipboard engine. The page owns the list, editor, settings, tabs, search,
export menu and OCR marker; the host owns the clipboard, SQLite, tray, hotkey and paste. The page is
embedded in the exe, so nothing sits beside it.

Every requested interaction gets one home:

- automatic tags are derived at read time by one classifier and shown as chips;
- search has one grammar: plain terms, phrases, `-exclude`, `<tag>:` and `tag:<tag>` (aliases included);
- the Images tab is a saved `image:` view over Inbox, never a duplicated DB tab;
- the keyboard has one matrix: focus starts on the selected row, arrows move and wrap, Left/Right change
  tab, typing searches, Esc clears then hides, F2 toggles the editor with a discard prompt only when dirty,
  `Ctrl+1`–`Ctrl+9` paste a visible row, and a bare digit is search text;
- the Settings control is a gear in the top bar.

## 3. Requirement status matrix

Legend: **SHIPPED** = built and proven with named evidence · **HEADLESS** = built and proven on the
page, live proof pending · **AWAITING SIGN** = fixed after a failed live read, fresh eyes needed ·
**BLOCKED** = cannot run on this machine · **NOT BUILT** = a real gap · **LIMIT** = accepted and named.

| # | Requirement (owner's words) | State | Evidence / gap |
|---|---|---|---|
| 1 | Rebuild on an AI-editable stack, keep the tested engine | SHIPPED | WPF + one warm WebView2 + React 19; engine unchanged; stack decision recorded |
| 2 | Copy/paste exactly for text, HTML, RTF, files, images, named formats | SHIPPED | Harness 4 formats into Notepad; live capture of 5 kinds; 600-emoji body whole |
| 3 | Paste into Office (Excel grid, Word RTF) | BLOCKED | Office 16 installed, both COM servers fail `TYPE_E_CANTLOADLIBRARY`; no UI matrix run |
| 4 | Tray, global hotkey, autostart, single instance | SHIPPED | Live tray show/hide; Alt+V; Exit 996 ms; duplicate launch exits |
| 5 | Row: index first, body up to 10 lines, provenance app·title·time | SHIPPED | 65 px one-line / 245 px ten-line at the default 10 (Capture → Row preview lines, 1–20); index 4ch; time 107 px at 900 and 430 |
| 6 | Toast auto-fits content in **both** dimensions, capped; inside the work area | SHIPPED (live re-measure pending) | Height 77 → 160 px by content; live two-line toast at 150 % DPI, no focus steal. Width now follows content too: minimum 240 DIP, configured 420 is the cap; a long body wraps at the cap and drops lines |
| 7 | Reliable automatic tags (proven only; missing is normal) | SHIPPED | 28/28 fixture, zero prose false positives; chips on rows; `+N` overflow |
| 8 | Search by tag (`image:`, `excel:`, `path:`, `code:`) | SHIPPED | Grammar 15/15; p50 9.3 ms at 1,000 rows; full-body match past char 3,001 |
| 9 | Explicit `tag:` search prefix | **HEADLESS** | `tag:image` → image, `tag:excel` → spreadsheet, `-tag:files`, unknown warns; **C# 11/11** + page 12/12; live pending |
| 10 | Separate tab for images | **HEADLESS** | Virtual Images tab = saved `image:` view over All, with count; keyboard cycle includes it; page 12/12; live pending |
| 11 | Text/markdown/CSV export; images as PNG | SHIPPED | CSV byte-identical to Excel's own; PNG SHA-256 equals the DB blob; `.md` = `.txt` bytes; UTF-8 BOM, never `sep=` |
| 12 | Offline OCR of copied images, searchable | SHIPPED | Windows.Media.Ocr; 93.7 % characters on a real screenshot; marker row; never pastes text; cannot overwrite an edit |
| 13 | OCR for Hindi | BLOCKED | No Hindi input method or OCR pack on this machine; engine returns LanguageNotAvailable |
| 14 | Keyboard usable from the instant the window appears | SHIPPED (page) / live re-check pending | Focus lands on the selected row; live Alt+V shows keys working; newest wrap + Ctrl rules are page 12/12 |
| 15 | Up/Down cycle clips, wrapping | **HEADLESS** | e2e `01 → 02 → 01 → 1000 → 01`; live pending on the final exe |
| 16 | Left/Right change tabs | SHIPPED | Live tab changes by keyboard; e2e covers strip order, wrap, one change per press |
| 17 | Type to search; Esc clears, Esc again hides | SHIPPED (live re-check running) | e2e typing + clear; live Esc clear + hide; digits are text. **Fixed 25 Nov:** the WPF shell had hidden the window on every Escape before the page saw it; the shell now owns Escape only when no page bridge exists, so the ordered cascade (clear → editor/Settings → hide) is the page's |
| 18 | F2 edits; F2 closes; discard only when changed; Esc closes | SHIPPED | e2e clean/dirty F2, Esc prompt then close; live on the 09:13 exe |
| 19 | Settings icon in the top bar | SHIPPED | Visible at 900/430, labelled, keyboard opens/closes; **gear** drawn (was sliders) |
| 20 | Paste a visible row by number | SHIPPED | Changed by owner to **Ctrl+1…Ctrl+9**; e2e proves it; bare digits search |
| 21 | Tabs: create, rename, delete, move a clip | SHIPPED | Bridge 22/22; strip CRUD; Ctrl+M; Ctrl+Shift+Left/Right; delete warns + cannot be undone |
| 22 | Editor: TipTap, full toolbar, save/discard, focus trap | SHIPPED | CF_HTML round-trip; 21/25 drawn SVGs; 40-Tab trap; live edit + save |
| 23 | Light/dark, 430–900 px, zoom 100/125/150 | SHIPPED except two rows | Theme live + persisted; 125 % persisted and reflowed live; 430 settings sheet; 150 % live DPI |
| 24 | UI/UX acceptance by fresh eyes | SHIPPED | Three fresh passes; UI/UX 50 PASS / 0 FAIL / 4 NOT RUN before the newest page |
| 25 | No data loss (truncation, corrupt DB, cap) | SHIPPED | Emoji truncation fixed and guarded; corrupt DB set aside + notice; cap exact; tab purge counted |
| 26 | Memory honest and acceptable | LIMIT / decision | 527.5 MB WS / 270.5 MB private, 7 processes (06:52 exe); newest exe not re-measured; owner must accept or set a ceiling |
| 27 | Cold first open <= 150 ms | LIMIT / decision | Live **160.8 ms** (150 % DPI, 11 ms over); warm median **97.9 ms**; internal show 3.5 ms |
| 28 | Startup hint ("… is running, press Alt+V") | SHIPPED (live pending) | `App.V2.cs` shows "Clip is running. Press Alt+V to open the list." from `Ready` on every cold start (default binding — the toast renders the configured hotkey, see Settings → Shortcuts). Fresh-profile live check running |
| 29 | Ghost notification preview in Settings | PARTIAL | Settings → **Preview** now calls the host and shows the **real** native toast at its real coordinates with the current settings (`previewNotification` → `NotificationWindow.ShowFor`). Drag-to-move and live-follow on every slider tick are not built |
| 30 | On-screen known-limits note | SHIPPED (live pending) | Settings → **Known limits**: colour emoji in the preview, Google Docs/Notion HTML degradation, OCR never for amounts, Excel `007` → `7` in CSV |
| 31 | Notice for over-budget clipboard formats | SHIPPED (live pending) | `ClipboardFormats.Snapshot` reports every dropped format name; `CaptureService` toasts "Part of the copy was not saved: <formats> over the N MB clip limit". Oversized images already toasted |
| 32 | 5 MB image decode off the UI thread | SHIPPED (live pending) | Capture reads the clipboard on the UI thread only; decode, thumbnail, DIB re-encode and SHA-256 run on a worker, the hash precomputed into `ClipboardItem.ImageHash`, and the store falls back to hashing when absent |
| 33 | Paste when the target's editor lacks focus | LIMIT | The guard checks the foreground window, not the focused child; click into the document |
| 34 | One exe, no runtime, UI embedded | SHIPPED | Content-verified: five unique page sequences inside the exe |
| 35 | `--legacy` old UI for one release | DONE, THEN REMOVED | Flag shipped in the v2 release; the old WPF app and `--legacy` were deleted on 25 Nov 2026 |
| 36 | Release guard: tray, 125 % dialog, 430 toast/export signatures | PARTIAL | `G-06` tray gained a themed menu, fresh sign pending; `W-02` 125 % dialog and `W-05` 430 toast/export remain unsigned |
| 37 | Files under the 400-line budget | NOT BUILT | Eleven files over, incl. `App.tsx`, `styles.css`, `ClipboardStore.Items.cs` |

## 4. User stories

1. As the owner, I want the app to open from a hotkey with the list already under my fingers, so that
   I never need the mouse for the first action.
2. As the owner, I want Up/Down to move through clips and wrap at the ends, so that I can reach the
   oldest or newest clip without thinking about boundaries.
3. As the owner, I want Left/Right to change tabs, so that I can move between All, Pinned, Images and my
   own tabs without aiming at the strip.
4. As the owner, I want typing any letter to search, so that I do not have to click or press Ctrl+K.
5. As the owner, I want a bare digit to be search text, so that I can find a year or an amount.
6. As the owner, I want `Ctrl+1`–`Ctrl+9` to paste the 1st–9th visible row, so that a number remains a
   fast paste key without stealing the digit from search.
7. As the owner, I want Esc to clear the search first and hide the window second, so that one key always
   backs out one step.
8. As the owner, I want Enter to paste the selected clip even while the caret is in the search box, so
   that searching then pasting is two keys.
9. As the owner, I want F2 to edit and F2 again to close, so that the same key opens and closes the
   editor.
10. As the owner, I want the discard prompt only when I changed something, so that closing a clean
    editor never nags me.
11. As the owner, I want Esc inside the editor to close it with the same discard rule, so that both keys
    behave consistently.
12. As the owner, I want a gear icon for Settings in the top bar, so that the control is obvious.
13. As the owner, I want auto-tags on each row, so that I can see at a glance what kind of clip it is.
14. As the owner, I want a missing tag to be normal, so that I can trust the tags that are shown.
15. As the owner, I want to search `image:`, `excel:`, `code:`, `files:`, `link:` and now `tag:image`,
    so that the tag vocabulary is explicit and discoverable.
16. As the owner, I want an unknown tag to be searched as text with a hint, so that nothing I typed is
    silently ignored.
17. As the owner, I want a dedicated Images tab, so that screenshots and pictures are one click away.
18. As the owner, I want the Images tab to show a live count, so that I know whether a new picture
    arrived.
19. As the owner, I want the Images tab to be a view, not a folder, so that moving a clip between tabs
    never changes what is an image.
20. As the owner, I want `-` to exclude and quotes to search a phrase, so that a narrow query stays
    possible.
21. As the owner, I want full-body search even for long clips, so that a word past the preview still
    finds the clip.
22. As the owner, I want tabs I can create, rename and delete, so that a project can have its own space.
23. As the owner, I want deleting a tab to warn how many clips go with it and that it cannot be undone,
    so that I never lose work by accident.
24. As the owner, I want to move a clip by right-click, Ctrl+M or Ctrl+Shift+Left/Right, so that
    organising is fast by mouse or keyboard.
25. As the owner, I want a TipTap editor with one icon family and a live character count, so that the
    editor looks and behaves like the rest of the app.
26. As the owner, I want an edit to show in the list immediately after saving, so that I trust the
    stored copy.
27. As the owner, I want to export a clip as text, markdown, CSV or PNG, so that I can use it outside
    Clip.
28. As the owner, I want CSV to open in Excel with columns intact, so that a copied table survives.
29. As the owner, I want to be told that Excel itself turns `007` into `7`, so that I do not blame Clip
    for the file.
30. As the owner, I want OCR text from images without sending anything to the cloud, so that client
    screenshots stay private.
31. As the owner, I want OCR text to be searchable but never pasted as the image's text, so that
    pasting a screenshot still pastes the picture.
32. As the owner, I want to be told not to trust OCR for amounts, so that a rupee figure is never taken
    from a picture.
33. As the owner, I want light and dark themes, 430–900 px widths and 100–150 % zoom, so that the app
    fits my monitors and scaling.
34. As the owner, I want one self-contained executable, so that nothing has to be installed or kept
    beside it.
35. As the owner, I want the app to start at login and stay in the tray, so that it is always ready.
36. As the owner, I want a corrupt database to be set aside with a visible message instead of a dead
    start, so that I know what happened and a fresh history begins.
37. As the owner, I want a startup hint telling me the app is running and the hotkey, so that a silent
    tray start is not a mystery.
38. As the owner, I want a native toast preview in Settings, so that I can see where it will appear
    before I rely on it.
39. As the owner, I want the app to tell me when a copy was skipped rather than dropping it silently,
    so that nothing disappears without a reason.
40. As the owner, I want a large image copy not to freeze the window, so that the list stays responsive.
41. As the owner, I want the tray menu to look like the app, so that no surface looks unfinished.
42. As the owner, I want a named list of limits in the release notes, so that I know what to avoid.
43. As the owner, I want every visual claim signed by an agent that did not build it, so that "done"
    means seen, not asserted.
44. As the owner, I want the final build proven by a test I can rerun, so that a future change cannot
    silently take the keyboard away.

## 5. Implementation decisions

- **One home per rule.** Tags: one classifier. Search: one grammar in C# (the page mock mirrors it for
  tests only). Theme: one resolver. Sort: one monotonic key. Truncation: one `IsTruncated` helper.
  Suppression: one hook. CF_HTML: one writer.
- **Tags are derived at read time**, never migrated; a guessed tag is worse than a missing one.
  Truncated previews keep format/language tags but never `link/email/path/colour/number/json`.
- **The `tag:` prefix is a normalisation step** before the existing tag lookup, so `tag:excel` and
  `excel:` produce the same canonical tag. A bare `tag:` is text, not a tag named "tag". An unknown
  `tag:foo` warns and searches as text, exactly like `foo:` does today.
- **The Images tab is virtual.** Its id is a page sentinel, never a DB id. Selecting it clears the DB
  tab to All and sets the programmatic query `image:`; selecting any other tab clears that query when it
  was programmatic. Deleting/renaming is not offered. Moving a clip does not target it.
- **The strip order** is All, Pinned, Images, then custom tabs; Left/Right cycles that order and wraps.
- **Keyboard matrix** (one window listener, disabled while a dialog/editor/popover owns the keyboard):
  Up/Down wrap; Left/Right change tab; printable non-digit starts a search; `Ctrl+1`–`Ctrl+9` paste the
  visible row; Enter/F2 also act while the search field has focus; Esc clears then hides; F2 opens the
  editor and the editor owns F2/Esc to close. A focused button keeps its Enter/Space.
- **Focus is selection.** The page focuses the selected row when the window appears (the shell already
  focuses the WebView). A search result list selects its first hit so keyboard actions always have a
  target.
- **The Images count** comes from the same `image:` search; a failed count is a hint, never a blocker.
- **Tabs are real DB containers**; deleting one purges its clips after a confirm that names the number
  and says it cannot be undone. Pinned is protected by kind, and the store's built-in kind is `builtin`,
  normalised by the page.
- **Export** writes UTF-8 with a BOM and never `sep=`; `.md` and `.txt` are identical bytes; a PNG is the
  stored bytes verbatim.
- **OCR** runs on Windows.Media.Ocr, serialized process-wide, writes into the clip's text only when it
  cannot overwrite a user edit, and never becomes the paste payload.
- **Release artifact** is one self-contained single-file exe with the page embedded; the old WPF UI
  and its `--legacy` flag were removed on 25 Nov 2026.
- **Owner-changed rules (25 Nov):** bare digits search; `Ctrl+1`–`Ctrl+9` paste; both arrows and tabs
  wrap; F2 toggles; Settings is a gear.

## 6. Testing decisions

A good test asserts external behaviour: what appears on screen and what the host writes, never private
functions. Three layers, each with a different job:

1. **Page e2e (standing gate):** Playwright (`@playwright/test` 1.63.0, cached Chromium, no download)
   loads the built single-file page with `?mock=1000` and asserts the keyboard contract, tabs, Images
   view, `tag:` search, editor close rules, settings gear and zero network/console errors. Command:
   `cd UI\web; npm.cmd run e2e` — 12 tests, ~9 s. A red test blocks release.
2. **Engine harness:** C# scratch projects compile the real `Core\` files. The search grammar has 11/11
   assertions for `tag:` (include, alias, exclude, unknown, bare prefix, mixed). Data safety is proven by
   the DB audits (migration digest, cap, delete/restore, tab purge, export bytes).
3. **Live acceptance by a fresh agent:** the published exe runs with an isolated profile; the agent
   presses real keys, reads its own screenshots and signs only what it saw. This is the only authority
   for visual and hardware claims (tray, toast, DPI, IME, Office).

Prior art: the `AUDIT-*.md` files use exactly these three layers; the live pass and the visual
sign-offs are the models for new rows.

## 7. Out of scope

- The "Stripe payout summary for November" example: the owner said to ignore it.
- Cloud sync, AI transforms, scripting, QR, diff, an image editor, macOS.
- Moving paste-by-number back to bare digits (decided: `Ctrl+1`–`Ctrl+9`, digits search).
- Auto-filing images into a real DB tab: the Images tab is a view, not a container.
- Rewriting the C# engine: it is preserved and tested as-is.

## 8. Further notes — what remains before this is the daily app

1. **Final binary.** Published: `dist\Clip.exe`, **208,196,038 B**, SHA-256
   **221D045B05D1199DBD4160A07E6ECD3D0C007C0598326FB9D7229B1B67470548**; the page was
   content-verified inside it (five unique page substrings plus `Known limits`, `previewNotification`,
   `No images yet`). A fresh agent is reading this exact file for the keyboard, Images/`tag:`, toast,
   first-run and tray checks (`docs\AUDIT-live-acceptance-3.md`).
2. **Checklist truth.** The feature and UI/UX checklists are updated from the live3 report; the cold-first
   row is FAIL at 160.8 ms until the owner accepts 11 ms over the old budget.
3. **Merge and adopt.** Merge `v2-phase1` into `master` in the live repo, run `build-release.cmd`, and
   fix the autostart registry entry, which currently points at the worktree exe. The real database was
   backed up to `%TEMP%\opencode\db-backup-095024` (db + shm + wal + verify-backup) before the first run.
4. **Built this round, awaiting the live sign:** first-run hint, real-toast Preview in Settings, on-screen
   Known-limits note, over-budget format notice, 5 MB image decode/thumbnail/hash off the UI thread.
   **Still open:** the preview does not drag or follow each slider tick, the 125 % dialog and 430
   toast/export rows are unsigned, and eleven files are over the 400-line budget (debt).
5. **Environment blocks** (Office COM, Hindi IME) stay NOT RUN with the reason; they are not defects.
