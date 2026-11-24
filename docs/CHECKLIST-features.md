# Clip — Feature Verification Checklist (open items only)

**What changed on 24 Nov (owner instruction):** the 100+ rows that were already PASSED or built-and-
verified are now one-line history in `docs\CHECKLIST-archive.md`. They were 70 % of this file and
none of them could fail any more. This file is the work that is actually open.

**Rule for adding a row:** it must be failable today. If it cannot fail, it does not get a row.
"Built the thing we just built" is not an acceptance item.

`[H]` = scratch-harness assertion · `[L]` = live/measured · `[U]` = visual (fresh agent only) ·
`[M]` = motion measurement. Protocol and visual authority: see `CHECKLIST-uiux.md`.

---

## 1. Blockers

- [ ] [L] **Cold first popup open 262–310 ms — over the 150 ms budget** (warm 50–61 ms also slower
      than S3's 12–32 ms). Pre-warm the first-render path (JIT + rich-row renderer + font load).
      Owner-visible: the first Alt+V after login feels broken.

## 2. Keyboard contract

- [ ] [L] **IME composition gate**: while a Hindi/Chinese/Japanese composition is open, Esc cancels
      the composition (not the popup) and Enter commits it (never pastes).
- [ ] [L] **Ctrl+↑/↓ jump between time sections** (Today / Yesterday / …), clamped.
- [ ] [L] **Esc cascade in one order**: IME → editor discard prompt → tab-delete confirm → tab-name
      editor → Ctrl+M picker → F2 editor → clear search → hide popup.
- [ ] [U] Selection stays stable when a live capture arrives while the popup is open.

## 3. Paste fidelity (owner: "paste exactly like I copied, for EVERYTHING")

- [ ] [L] **Live matrix**: Excel → Excel (Biff12 columns/rows), Word (RTF), Chrome (HTML), Paint
      and Photos (PNG/DIB). Payload contract is PASS for rich text / images / files; the live
      Office round-trip has never run (Excel could not hold foreground under automation).
- [ ] [L] Plain paste (Ctrl+Shift+V) with the popup open pastes the *selected* item as text.

## 4. Rich text & images

- [ ] [H] CF_HTML byte-offset fragment extraction, Unicode-safe (Devanagari/emoji torture test).
- [ ] [U] Remote images in preview: no network fetch, transparent 1×1, never a broken-image glyph.
- [ ] [U] **F2 editor after the TipTap port**: toolbar / text / footer read as three type levels,
      discard banner leaves the clip readable, a dark-source clip keeps its dark bands with readable
      ink, a long clip scrolls on a themed bar, the field shows its accent focus ring. Evidence:
      `docs\evidence-tiptap\`, audit: `docs\AUDIT-tiptap-port.md`. Owner eyeball at 100 %/200 % DPI.
- [ ] [L] **F2 open after the port**: ~2.9 s cold, ~2.7 s warm, +302 MB while open (this VM). Decide:
      accept, a "Loading…" line, or a hidden warm window (RAM trade). Numbers in the audit.
- [ ] [U] RTF-only clips render through `UI\EditorRtf` (WPF has no HTML export) — seeded Word-style
      RTF verified; the two real RTF-only clips open without the plain-text notice.
- [ ] [U] Known ceilings documented on screen where they occur: colour emoji as tofu, Google
      Docs/Notion HTML degradation (paste-back fidelity unaffected).
- [ ] [L] Image decode/encode + SHA-256 of a 5 MB PNG moved off the UI thread.
- [ ] [L] Memory discipline: a 200-row list carries thumbs only; full PNGs load on demand.

## 5. Tabs & dedupe

- [ ] [L] **Ctrl+P** move to/from Pinned (store semantics already verified both directions).
- [ ] [L] **Ctrl+Shift+←/→** adjacent-tab move.
- [ ] [U] A re-copy with the popup open never briefly shows the deduped clip twice.

## 6. Settings & notification

- [ ] [U] **Ghost notification preview**: appears at the real toast coordinates, follows every
      value change, drag writes back offsets, auto-hides, pin keeps it up. (ACC-v2 saw nothing.)
- [ ] [L] First-run hint on the very first launch: "… is running — press Alt+V".
- [ ] [L] Skipped captures (oversized image, over-budget clipboard) raise a notice — never vanish.

## 7. List behaviour shipped 24 Nov (owner confirmation)

- [ ] [L] List rows clamp to 3 lines (`ClipRow.ListLinesMax`); `previewLines` (1..100) now sizes the
      outside tooltip instead of the row. Verify the tooltip still shows the full clip.
- [ ] [U] Identity tile, facts column and no-meta row survive a filter, a tab switch and a resize.

## 8. Infrastructure & scale

- [ ] [L] The app writes nothing outside its own folder and `%APPDATA%\Clip` (cache included).
- [ ] [L] Acceptance run in one pass: copies from Notepad / Chrome / VS Code / Explorer appear with
      the right app + time; a password-manager copy never appears; start-on-login survives reboot.
- [ ] [L] `CanContentScroll=True` + `VirtualizationMode=Recycling` preserved through the row
      redesign (measured: `False` = 1000 containers, 3.6–4.4 s to first layout).
- [ ] [L] Search: 120 ms debounce + swap the collection instance (Clear+Add of 1000 = 38–59 ms/key).
- [ ] [L] Corrupt/locked `clip.db`: rename + recreate + a user notice instead of a dead start.
- [ ] [L] **Acceptance at 1000 items**: scroll jank-free, search <50 ms end-to-end, cold start →
      popup <300 ms, idle RAM unchanged vs 200, `GetRecent(1000)` ≤10 ms.

## 9. Deferred, tracked, not in scope (kept as decisions, not checks)

Delayed-render IStream fallback · paste focus poll · CF_DIBV5 (only after the live matrix) ·
per-app capture exclusion · OfficeIMO.Rtf as the RTF fallback · 1000-item product review.
Roadmap: M4 paste transforms / export / sensitive masking / retention / per-app rules; M5 paste
queue, `app:`/`date:` prefixes, duplicate-count UX, OCR spike. Never: cloud sync, AI transforms,
scripting, diff, QR, image editor.
