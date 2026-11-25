# Clip — Feature Verification Checklist (open items only)

**Status 25 Nov (docs resync):** 32 rows live — **8 PASS · 5 FAIL · 19 NOT RUN**. Four rows that can no
longer fail moved to `docs\CHECKLIST-archive.md` (WPF-era clamp, identity tile, `CanContentScroll`, the
WPF search swap). PASS means the named evidence exists and was read; a builder never signs a `[U]` row.

**Re-cited 25 Nov:** two rows that were already exercised but never cited now read PASS — the plain-paste
payload (`s4b\e-plain-*`, `s8b\plainmode-shell.png`) and the v2 row contract (`AUDIT-release-signoff-2` §2).
The rest of the NOT RUN rows were re-checked against `S4b`, `S8a`, `S8b`, `S9a`, `S10`, `S12`, `S13`,
`s17`, `s18`, `s20` and the two signoffs; where the evidence is partial the row now says what was measured.

**What changed on 24 Nov (owner instruction):** the 100+ rows that were already PASSED or built-and-
verified are now one-line history in `docs\CHECKLIST-archive.md`. They were 70 % of this file and
none of them could fail any more. This file is the work that is actually open.

**Rule for adding a row:** it must be failable today. If it cannot fail, it does not get a row.
"Built the thing we just built" is not an acceptance item.

`[H]` = scratch-harness assertion · `[L]` = live/measured · `[U]` = visual (fresh agent only) ·
`[M]` = motion measurement. Protocol and visual authority: see `CHECKLIST-uiux.md`.
Harness sources are in `%TEMP%\opencode\` (scratch, not committed): `s17\verify-0742.log` is the
52-check page harness re-run on the 07:36 bundle; `audit-release-2\` is the release-logic harness.

---

## 1. Blockers

- [ ] **FAIL** - [L] **Cold first popup open 160.8 ms - still over the 150 ms budget** (live pass 25 Nov: cold 160.8 ms, warm 97.9 ms median; `docs\AUDIT-live-acceptance.md`). Pre-warm the
      first-render path (JIT + rich-row renderer + font load). Owner-visible: the first Alt+V after login
      feels broken. S8a measured warm show at **17.2 ms median** (20 samples/run, `s8a\timing.txt` +
      `timing2.txt`) on the v2 shell, so the old 50–61 ms warm figure is stale — but cold is still
      unmeasured on the release exe (`208,187,846` B); the only number on that exe is idle memory
      (527.5 MB WS / 270.5 MB private).

## 2. Keyboard contract

- [ ] **NOT RUN** — [L] **IME composition gate**: while a Hindi/Chinese/Japanese composition is open,
      Esc cancels the composition (not the popup) and Enter commits it (never pastes). The guard exists
      (`useClipboardKeys.ts:43`); no real IME has been driven.
- [x] **PASS** — [L] **Ctrl+↑/↓ jump between time sections** (Today / Yesterday / …), clamped.
      `s17\verify-0742.log` (52/52, re-run 07:42 on the 07:36 bundle): Ctrl+Down 0 → row 39 (25 → 24 Nov)
      at `offsetTop 0`, repeats, advances to 23 Nov; Ctrl+Up returns and is clamped at both ends.
- [ ] **NOT RUN** — [L] **Esc cascade in one order**: IME → editor discard prompt → tab-delete confirm
      → tab-name editor → Ctrl+M picker → F2 editor → clear search → hide popup. All the overlays exist
      (`1ced540`, `141ec9e`); the ordered live sequence has never been observed.
- [ ] **NOT RUN** — [U] Selection stays stable when a live capture arrives while the popup is open.
      No fresh screenshots.

## 3. Paste fidelity (owner: "paste exactly like I copied, for EVERYTHING")

- [ ] **NOT RUN** — [L] **Live matrix**: Excel → Excel (Biff12 columns/rows), Word (RTF), Chrome (HTML),
      Paint and Photos (PNG/DIB). Payload contract is PASS for rich text / images / files; the live
      Office round-trip has never run (Excel could not hold foreground under automation).
- [x] **PASS** — [L] Plain paste (Ctrl+Shift+V, the `plain` branch) pastes the *selected* item as text.
      **S4b harness (live payload):** `s4b\e-plain-excel.txt` / `e-plain-rich.txt` drove `paste` with
      `plain=true` on the selected clip — the clipboard held only `System.String, Text, UnicodeText`
      (no HTML/RTF/Biff12) and Notepad read back the exact sentinel text. **S8b** `plainmode-shell.png`
      is the real shell after the plain-paste proof (row 01 `plain-paste-proof`, toast `explorer 03:43`).
      The Ctrl+Shift+V → plain branch is proven in `s4a` (headless page); the payload is the live proof.

## 4. Rich text & images

- [x] **PASS** — [H] CF_HTML byte-offset fragment extraction, Unicode-safe (Devanagari/emoji torture
      test). `audit-release-2\core` on `Interop\HtmlFragment.cs`: 3/3 at this HEAD
      (`docs\AUDIT-release-logic-2.md` §4).
- [ ] **NOT RUN** — [U] Remote images in preview: no network fetch, transparent 1×1, never a
      broken-image glyph. Rule present (`rich.tsx`); no fresh shot.
- [ ] **NOT RUN** — [U] **F2 editor after the TipTap port**: toolbar / text / footer read as three type
      levels, discard banner leaves the clip readable, a dark-source clip keeps its dark bands with
      readable ink, a long clip scrolls on a themed bar, the field shows its accent focus ring. The
      07:17 fresh pass read the toolbar and footer (`AUDIT-release-visual-2` §2.4) but not the dark-band
      or long-clip cases; evidence `docs\evidence-tiptap\`, audit `docs\AUDIT-tiptap-port.md`.
- [ ] **NOT RUN** — [L] **F2 open after the port**: ~2.9 s cold, ~2.7 s warm, +302 MB while open (this
      VM). Decide: accept, a "Loading…" line, or a hidden warm window (RAM trade). Not re-measured.
- [ ] **NOT RUN** — [U] RTF-only clips open in the v2 editor without the plain-text notice. The old WPF
      path (`UI\EditorRtf`, `EditorWindow`) is legacy-only; the v2 `EditorOverlay` reads `body.html` else
      `textToHtml(body.text)` (`EditorOverlay.tsx:88`) and `getItem` returns `rtf` but the page never
      reads it — so this row now tests the v2 editor. No fresh visual of a real RTF-only clip.
- [ ] **FAIL** — [U] Known ceilings documented on screen where they occur: colour emoji as tofu, Google
      Docs/Notion HTML degradation (paste-back fidelity unaffected). Still only in `USER_GUIDE.md`.
- [ ] **FAIL** — [L] Image decode/encode + SHA-256 of a 5 MB PNG moved off the UI thread. Capture still
      decodes, encodes and hashes on the dispatcher thread (`AUDIT-release-logic-2` §4).
- [ ] **NOT RUN** — [L] Memory discipline: a 200-row list carries thumbs only; full PNGs load on demand.
      The list holds no image blob (code) but this was not measured.

## 5. Tabs & dedupe

- [ ] **NOT RUN** — [L] **Ctrl+P** move to/from Pinned (store semantics already verified both
      directions). Store proven; the live UI path is not.
- [x] **PASS** — [L] **Ctrl+Shift+←/→** adjacent-tab move. `s17\verify-0742.log` (52/52): All → Work →
      Design → Snippets → Pinned → All, counts refresh (All 1,000→999, Work 0→1), wraps at both ends,
      and the nearest row takes the selection after the clip leaves the current tab. Bridge/store side
      22/22 (`AUDIT-release-logic-2` §1.3).
- [ ] **NOT RUN** — [U] A re-copy with the popup open never briefly shows the deduped clip twice. No
      fresh visual.

## 6. Settings & notification

- [ ] **FAIL** — [U] **Ghost notification preview**: appears at the real toast coordinates, follows
      every value change, drag writes back offsets, auto-hides, pin keeps it up. v2 Settings has no
      preview at all.
- [ ] **FAIL** — [L] First-run hint on the very first launch: v2 has **no first-run affordance at all** — no
      host or page path reads `WasNew` (only the legacy `--legacy` App shows "… is running — press Alt+V"),
      so a first launch lands silently in the tray.
- [ ] **FAIL** — [L] Skipped captures (oversized image, over-budget clipboard) raise a notice — never
      vanish. Half shipped: an oversized image now reaches the quiet `Skipped` toast end to end (page
      `aba6c71`, host `5d56b53`, injected headless — `%TEMP%\opencode\chrome-signoff\
      skipped-toast-900x700-light.png`); a clipboard whose formats exceed `MaxItemBytes` still loses those
      blobs silently in `ClipboardFormats.Snapshot`, with no toast.

## 7. (Retired with the WPF list — moved to the archive)

The 3-line clamp and the identity-tile/facts rows describe `ClipRow`/WPF anatomy that is not reachable in
the v2 page. They are one-liners in `docs\CHECKLIST-archive.md` (§ "Retired 25 Nov").

## 8. Infrastructure & scale

- [x] **PASS** — [L] The app writes nothing outside its own folder and `%APPDATA%\Clip` (cache included).
      Unchanged; every writer resolves through `Config.DataDir` (`AUDIT-release-logic-2` §4).
- [ ] **NOT RUN** — [L] Acceptance run in one pass: copies from Notepad / Chrome / VS Code / Explorer
      appear with the right app + time; a password-manager copy never appears; start-on-login survives
      reboot. S10's clean-profile run captured five kinds (rich HTML, files, image, link, text) on the
      published exe and S8a captured six more, but the four named apps, the password-manager exclusion
      and the reboot are still unrun.
- [x] **PASS** — [L] Corrupt/locked `clip.db`: rename + recreate + a user notice instead of a dead start.
      Recovery proven at `5a7a285` (`AUDIT-release-logic`); the notice is now wired end to end:
      `App.V2.cs` tray toast + `ShellWindow` `corruptionRecovered` + page handler (`141ec9e`). **Headless
      only** for the toast: `s17\verify-0742.log` "injected corruptionRecovered shows the host's notice"
      and `s17\06-corruption-toast.png`; the live tray toast was not seen on screen.
- [ ] **NOT RUN** — [L] **Acceptance at 1000 items**: scroll jank-free, search <50 ms end-to-end, cold
      start → popup <300 ms, idle RAM unchanged vs 200, `GetRecent(1000)` ≤10 ms. Search is now proven
      (below, 9.3 ms p50) and `GetRecent` 4 ms; scroll, cold start and RAM are unmeasured.
      Full-body search reads the stored body past the 1,200-char preview: p50 **9.3 ms**, p95 11.4 ms at
      1,000 rows with 200 truncated, and a term at character 3,001 is found (`AUDIT-release-logic-2`
      §1.2, harness `audit-release-2`).

## 9. v2 row contract (owner, 25 Nov)

- [x] **PASS** — [H] `ClipTags` fixture table (≥20 clips): image, files, spreadsheet, link, email, path,
      colour, number, JSON, HTML, SQL, and 8 controls that must return **no** tag (prose, password, mixed
      formats, ambiguous code). 28/28 re-run at this HEAD, zero false positives
      (`AUDIT-release-logic-2` §4).
- [x] **PASS** — [U] Row = index `01`, body up to 10 lines auto-height, then `app · title · date time`
      on every row; time never truncates at 430 px width. Fresh pass `AUDIT-release-signoff-2` §2 on the
      current 08:05 bundle (headless Chromium, both themes): row **01 = 65 px / 1 line**, row **02 = 245 px
      / 10 lines** clamped with the trailing ellipsis; index 98–101 share `x 35 → 66, w 31`,
      `tabular-nums`, text starts at **76**; `.prov-time` **107 px**, `scrollWidth == clientWidth` at 900
      **and 430**; image row 100 = 263 px with `Text read from image`, files row carries
      `Chrome · board-minutes.docx and 2 more`. **Headless page**, fresh agent — not the shell.
- [ ] **NOT RUN** — [L] Digit `1`–`9` copies the item whose **visible** index is that number, renumbering
      correctly after search, tab switch and filter. Code path present; no post-fix live pass.
- [ ] **NOT RUN** — [U] A 1-line clip is shorter than a 10-line row, a 30-line clip clamps at 10, and the
      outside tooltip still carries the full text (auto-fit, not a fixed row size). No fresh visual.
- [ ] **NOT RUN** — [L] Mixed-height virtualizer: 1,000 items, scroll with no jump, selection stays on
      the same clip across a filter. Not measured.
- [ ] **NOT RUN** — [L] Toast auto-fits: 1 line is short, 30 lines stops at `MaxLines`/`Height`, and the
      toast stays inside the work area at 150 % DPI. S8b measured the real native toast at **77 px** (1 line)
      and **160 px** (30-line cap) at 100 % (`s8b\toast-1line-real.png`, `toast-30line-real.png`); the
      150 % DPI clause is still unmeasured.
- [ ] **NOT RUN** — [L] Web list keyboard matrix: ↑ ↓ Home End PageUp PageDown Enter Ctrl+Shift+V F2
      Ctrl+F Esc Ctrl+M Ctrl+T `1`–`9`, with the Esc cascade order preserved. Individual chords are
      proven headless (above; S20 adds Ctrl+T: picker open → Enter filters → Esc returns row focus,
      `s20\proof-report.json` A/B); the full matrix and the cascade order have no live pass.

## 10. Deferred, tracked, not in scope (kept as decisions, not checks)

Delayed-render IStream fallback · paste focus poll · CF_DIBV5 (only after the live matrix) ·
per-app capture exclusion · OfficeIMO.Rtf as the RTF fallback · 1000-item product review.
Roadmap: M4 paste transforms / export / sensitive masking / retention / per-app rules; M5 paste
queue, `app:`/`date:` prefixes, duplicate-count UX, OCR spike. Never: cloud sync, AI transforms,
scripting, diff, QR, image editor.

## 11. Release gate (this file)

Not PASS here: **5 FAIL** — on-screen ceilings, 5 MB PNG off-thread, ghost notification preview,
first-run hint, skipped-capture notice. **19 NOT RUN** — blocked on a real clipboard/IME/DPI/second
monitor, a live app run, or a fresh pair of eyes; the four that matter most for the release feel are the
cold first popup (§1), the live paste matrix (§3), the F2 open cost (§4) and the 1,000-item acceptance
(§8). Tab CRUD, full-body search and the export shell have **no row in this file**; they are proven in
`AUDIT-release-logic-2` §1.1–§1.3 (9/9 export, 11/11 search, 22/22 tabs) and tracked in
`BUILD-TRACKER.md` S12/S13.

## 12. Final tally (25 Nov docs resync)

**32 rows — 8 PASS · 5 FAIL · 19 NOT RUN.**

| Status | Rows |
|---|---|
| PASS | Ctrl+↑/↓ time sections · CF_HTML offsets (harness) · **plain paste (harness + real shell)** · Ctrl+Shift+←/→ tab move · writes only its folder + `%APPDATA%` · corrupt-DB recovery (headless notice) · **v2 row contract (fresh pass)** · `ClipTags` 28/28 (harness) |
| FAIL | on-screen ceilings · 5 MB PNG off-thread · ghost notification preview · first-run hint · skipped-capture notice |
| NOT RUN | cold first popup · IME · Esc cascade order · selection-vs-capture (`[U]`) · live paste matrix · remote images (`[U]`) · F2 editor after port (`[U]`) · F2 open cost · RTF-only clips (`[U]`) · thumbs-only memory · Ctrl+P live · re-copy twice (`[U]`) · one-pass acceptance · 1,000-item acceptance · digits after tab switch · 1/10/30-line + tooltip (`[U]`) · mixed-height virtualizer · toast at 150 % DPI · full keyboard matrix + cascade |

## 13. What still needs the owner

1. **Live Office round-trip** — Excel→Excel (columns/rows), Word (RTF), Chrome (HTML), Paint/Photos
   (PNG/DIB). Automation cannot hold an Office window in the foreground; this is a two-minute manual pass.
2. **Cold first Alt+V on the release exe** — five launches after login; warm is 17.2 ms, cold is the
   only launch-feel number still missing.
3. **Real IME** — Hindi/Chinese/Japanese composition, Esc cancels it, Enter commits (never pastes).
4. **DPI and a second monitor** — native toast placement at 150 %, the shell at 125 % zoom, and
   placement on a second monitor.
5. **Tray menu interaction** — open Settings, Clear history, Exit by hand; no fresh agent has clicked it.
6. **First-run + reboot** — v2 shows no first-run hint at all (§6), so only start-on-login surviving a
   reboot can be checked.
7. **Memory ceiling** — accept ~528 MB idle / ~643 MB with an image, or set a new ceiling.
8. **Three product decisions (FAILs):** on-screen known-limits note, ghost notification preview,
   notice for over-budget clipboard formats; plus the 5 MB PNG off-thread fix (engineering, not a decision).
