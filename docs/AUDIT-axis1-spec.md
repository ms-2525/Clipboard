# AUDIT — Axis 1: Spec & Business Truth

**Auditor:** fresh-eyes subagent (`opencode-go/deepseek-v4.1-flash`), no code written, no source file edited.
**Date:** 23 Nov 2026, ~15:05–15:35 local.
**Contract:** `docs\CHECKLIST-features.md` (frozen). Every `✅ [H]` claim in the audited set was treated as Bucket-1 Hard Enforce and re-measured from scratch.
**Deliverable:** this file. It is the only repo write from this audit.

## Verdict in one line

**145 of 146 assertions green, 1 real defect found (dedupe does not move a same-second re-copy to the top), 0 false greens in the audited set, and all 4 mutation checks turned the harness RED — so the green results have teeth.**

---

## 1. What I audited and how

- **Harness:** a throwaway console project at `C:\Users\mayan\AppData\Local\Temp\opencode\axis1` (net10.0-windows) that `<Compile Include>`-d the **real** files:
  `Core\ClipboardStore.cs`, `ClipboardItem.cs`, `Config.cs`, `CaptureService.cs`, `Interop\HtmlFragment.cs`, `PasteInjector.cs`, `ClipboardFormats.cs`, `ClipboardListener.cs`, `ForegroundApp.cs`.
  No file was copied "by hand"; no formula was duplicated in the test. Every expected value is a hard-coded literal.
- **Source state at audit time** (no git repo, so timestamps + sizes are the pin):
  `ClipboardStore.cs` 20,085 B 15:12:24 · `CaptureService.cs` 12,376 B 15:08:56 · `Config.cs` 3,972 B 15:21:17 · `ClipboardItem.cs` 1,851 B · `HtmlFragment.cs` 2,863 B 14:43:03 · `PasteInjector.cs` 9,039 B 15:25:19 · `ClipboardFormats.cs` 8,942 B 15:21:33. `Config.PreviewLines` (the parallel agent's change) is present and touched none of my tests.
- **Throwaway databases** in `%TEMP%` only; the live `%APPDATA%\Clip\clip.db` was never opened by the harness.
- **146 assertions, 13 groups.** Result: **145 PASS, 1 FAIL**, exit code 1 (the harness fails loudly rather than reporting an average).
- Mutation checks: the same harness rebuilt against **mutated copies** in `mut\` (`-p:SrcRoot=`), the repo files never modified. After each mutation the harness was rebuilt against the real sources and went green again (`evidence-after-restore.txt`).

---

## 2. Reproduction matrix

Observed values are verbatim from the run; `PASS` means the literal matched exactly.

### 2.1 Dedupe — byte-exact (checklist §9, 39 assertions, 1 FAIL)

| Assertion | Literal expected | Observed | Result |
|---|---|---|---|
| dedupe/recopy-returns-true | `true` | `true` | PASS |
| dedupe/one-row-per-text | `1` | `1` | PASS |
| dedupe/moved-to-top | `[Hello]` | `[Hello]` | PASS |
| dedupe/new-timestamp | `2026-11-23T10:00:00` | `2026-11-23T10:00:00` | PASS |
| dedupe/new-app | `SecondApp` | `SecondApp` | PASS |
| dedupe/id-reused (row is moved, not re-created) | `1` | `1` | PASS |
| dedupe/formats-exactly-incoming-set | `[Text]` | `[Text]` | PASS |
| dedupe/formats-count | `1` | `1` | PASS |
| dedupe/format-bytes | `99` | `99` | PASS |
| dedupe/main-history-rows-after-recopy | `1` | `1` | PASS |
| lowercase (`Hello` vs `hello`) | `is-different=false`, `two-rows=2`, both texts kept, distinct=2 | `false / 2 / true,true / 2` | PASS |
| trailing space (`Hello` vs `Hello `) | same shape | `false / 2 / true,true / 2` | PASS |
| leading space (`Hello` vs ` Hello`) | same shape | `false / 2 / true,true / 2` | PASS |
| NFC vs NFD (`café` U+00E9 vs `cafe`+U+0301) | same shape | `false / 2 / true,true / 2` | PASS |
| dedupe/pinned-does-not-swallow | `false` | `false` | PASS |
| dedupe/pinned-still-one-row / timestamp-untouched | `1` / `2026-01-01T00:00:00` | `1` / `2026-01-01T00:00:00` | PASS |
| dedupe/fresh-main-row-created | `1` | `1` | PASS |
| dedupe/identical-image-bytes-merge | `true` | `true` | PASS |
| dedupe/different-image-bytes-kept | `false` | `false` | PASS |
| dedupe/image-rows | `1` | `1` | PASS |
| dedupe/same-second-recopy-merges | `true` | `true` | PASS |
| **dedupe/same-second-recopy-at-top** | **`A`** | **`top=B order=B,A`** | **FAIL (see D1)** |

### 2.2 Cap — pinned-safe eviction (checklist §1, §7; 9 assertions)

| Assertion | Literal expected | Observed | Result |
|---|---|---|---|
| cap/main-count-bounded (EnforceCap(3), 5 main) | `3` | `3` | PASS |
| cap/newest-kept | `T03,T04,T05` | `T03,T04,T05` | PASS |
| cap/pinned-survive (2 pinned, **oldest** timestamps) | `2` | `2` | PASS |
| cap/pinned-ids-survive | `6,7` | `6,7` | PASS |
| cap/pinned-formats-survive | `2` | `2` | PASS |
| cap/no-orphans | `0` | `0` | PASS |
| cap/all-in-tabs-no-exception | `<none>` | `<none>` | PASS |
| cap/all-in-tabs-untouched (cap 2, 3 tab rows) | `3` | `3` | PASS |
| cap/all-in-tabs-empty-main | `0` | `0` | PASS |

> Note on test design: the pinned rows deliberately carry the **oldest** timestamps. My first version gave them the newest timestamps and the mutation in §3(b) still "passed" the pinned test by luck — that is exactly the false-green trap this audit exists to catch. Corrected before the results above.

### 2.3 Clear / tabs / Pinned protection (checklist §7; 24 assertions)

| Assertion | Literal expected | Observed | Result |
|---|---|---|---|
| clear/main-emptied | `0` | `0` | PASS |
| clear/pinned-survives | `1` | `1` | PASS |
| clear/custom-tab-survives | `2` | `2` | PASS |
| clear/tab-formats-survive | `3` | `3` | PASS |
| clear/no-orphans | `0` | `0` | PASS |
| tabs/list-count | `2` | `2` | PASS |
| tabs/pinned-first | `Pinned/builtin` | `Pinned/builtin` | PASS |
| tabs/pinned-count | `1` | `1` | PASS |
| tabs/work-count | `Work/2` | `Work/2` | PASS |
| tabs/delete-returns-purge-count | `2` | `2` | PASS |
| tabs/delete-removes-row | `1` | `1` | PASS |
| tabs/delete-no-orphans | `0` | `0` | PASS |
| tabs/delete-format-rows-gone | `1` | `1` | PASS |
| tabs/pinned-delete-rejected | `-1` | `-1` | PASS |
| tabs/pinned-rename-rejected | `false` | `false` | PASS |
| tabs/pinned-name-intact | `Pinned` | `Pinned` | PASS |
| tabs/pinned-still-exists-after-attempts | `1` | `1` | PASS |
| tabs/blank-throws, whitespace-throws, too-long-throws (25 chars), dup-throws, case-dup-throws (`beta` after `Beta`) | `ArgumentException` ×5 | `ArgumentException` ×5 | PASS |
| tabs/rename-works / rename-dup-false | `true` / `false` | `true` / `false` | PASS |

### 2.4 Dedupe tab scope (checklist §7 "a pinned copy never swallows a fresh history copy"; 11 assertions)

| Assertion | Literal expected | Observed | Result |
|---|---|---|---|
| tabscope/fresh-main-does-not-merge-into-pinned | `false` | `false` | PASS |
| tabscope/main-has-own-row / pinned-still-one-row | `1` / `1` | `1` / `1` | PASS |
| tabscope/main-row-is-not-the-pinned-row | `different-id` | `different-id` | PASS |
| tabscope/second-main-merges | `true` | `true` | PASS |
| tabscope/main-still-one-row | `1` | `1` | PASS |
| tabscope/main-timestamp-updated | `2026-11-23T13:00:00` | `2026-11-23T13:00:00` | PASS |
| tabscope/pinned-untouched-timestamp | `2026-01-01T00:00:00` | `2026-01-01T00:00:00` | PASS |
| tabscope/pinned-id-untouched | `1` | `1` | PASS |
| tabscope/main-id-stable | `2` | `2` | PASS |
| tabscope/pinned-count-still-one | `1` | `1` | PASS |

### 2.5 CF_HTML fragment (checklist §5 line 56 — currently marked ⬜; 7 assertions)

Content used: `表格 🎉 हिन्दी` (3-byte CJK, 4-byte emoji, Devanagari with combining marks = **30 UTF-8 bytes**, hard-coded).

| Assertion | Literal expected | Observed | Result |
|---|---|---|---|
| html/header-is-105-bytes | `105` | `105` | PASS |
| html/byte-offset-fragment | `表格 🎉 हिन्दी` | `表格 🎉 हिन्दी` | PASS |
| html/torture-content-utf8-bytes | `30` | `30` | PASS |
| html/minus1-offsets-whole-body | `<html><body><!--StartFragment-->表格 🎉 हिन्दी<!--EndFragment--></body></html>` | identical | PASS |
| html/mid-multibyte-no-throw | `<none>` | `<none>` | PASS |
| html/mid-multibyte-clean-prefix (fragment ends 1 byte into `格`) | `abc表` | `abc表` | PASS |
| html/mid-multibyte-no-replacement-char | `absent` | `absent` | PASS |

### 2.6 Pixel guard (checklist §6; 7 assertions)

Bomb: a **real decodable 8×8 PNG** whose IHDR is rewritten to 30000×30000 with the CRC recomputed — i.e. a genuine "patched-IHDR bomb", not a synthetic header.

| Assertion | Literal expected | Observed | Result |
|---|---|---|---|
| pixel/patched-ihdr-rejected | `true` | `true` | PASS |
| pixel/reason-is-the-dimension-guard | `Image skipped: too large` | `Image skipped: too large` | PASS |
| pixel/rejected-fast | `fast` (< 1500 ms) | `cold 59 ms, warm 0 ms` | PASS |
| pixel/8x8-passes | `false` | `false` | PASS |
| pixel/8x8-no-reason | `<null>` | `<null>` | PASS |
| pixel/no-row-stored (bomb on the **real clipboard** → `CaptureService.Capture()`) | `0` | `0` | PASS |
| pixel/skipped-fires-with-reason | `Image skipped: too large` | `Image skipped: too large` | PASS |

### 2.7 Modifier-release input sequence (checklist §3 trap; 3 assertions)

| Assertion | Literal expected | Observed | Result |
|---|---|---|---|
| input/length | `9` | `9` | PASS |
| input/five-modifier-keyups | `11/2,12/2,10/2,5B/2,5C/2` (Ctrl, Alt, Shift, LWin, RWin — all KEYUP) | identical | PASS |
| input/ctrl-v-chord | `11/0,56/0,56/2,11/2` | identical | PASS |

### 2.8 CF_DIB construction (checklist §6; 12 assertions)

4×3 test image: top row red, middle green, bottom row **fully transparent** (must flatten to white).

| Assertion | Literal expected | Observed | Result |
|---|---|---|---|
| dib/total-length | `len 76` (40 + 12×3) | `len 76` | PASS |
| dib/biSize | `40` | `40` | PASS |
| dib/biWidth | `4` | `4` | PASS |
| dib/biHeight-bottom-up (positive) | `3` | `3` | PASS |
| dib/biPlanes-WORD | `1` | `1` | PASS |
| dib/biBitCount-WORD | `24` | `24` | PASS |
| dib/biCompression-BI_RGB | `0` | `0` | PASS |
| dib/biSizeImage | `36` | `36` | PASS |
| dib/stride-dword-aligned-for-width-4 | `12` | `12` | PASS |
| dib/first-stored-row-is-last-image-row-white | `FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF` | identical | PASS |
| dib/second-stored-row-green | `00-FF-00-…` (×4) | identical | PASS |
| dib/last-stored-row-red | `00-00-FF-…` (×4) | identical | PASS |

### 2.9 Clipboard round-trips (checklist §3, §4, §6; 17 assertions)

| Assertion | Literal expected | Observed | Result |
|---|---|---|---|
| clip/plain-has-text / text-value | `true` / `axis1 plain text` | `true` / `axis1 plain text` | PASS |
| clip/plain-no-rtf / plain-no-html (rich item → plain paste) | `false` / `false` | `false` / `false` | PASS |
| clip/snapshot-captures-custom-format / unicode | `true` / `true` | `true` / `true` | PASS |
| clip/restore-custom-format-bytes | `01-02-03-04-FA` | `01-02-03-04-FA` | PASS |
| clip/restore-unicode-value | `राउंडट्रिप ✓` | `राउंडट्रिप ✓` | PASS |
| clip/restore-unicode-bytes-identical (raw HGLOBAL before vs after) | `30-09-3E-09-09-09-02-09-21-09-1F-09-4D-09-30-09-3F-09-2A-09-20-00-13-27-00-00` | identical | PASS |
| clip/unicode-payload-decodes | `राउंडट्रिप ✓` | `राउंडट्रिप ✓` | PASS |
| clip/image-has-dib (CF_DIB present) | `true` | `true` | PASS |
| clip/image-has-png | `true` | `true` | PASS |
| clip/image-png-verbatim | `identical` | `identical` | PASS |
| clip/image-dib-length-8x8 | `232` (40 + 24×8) | `232` | PASS |
| clip/image-paste-branch-dib / -png | `true` / `true` | `true` / `true` | PASS |
| clip/image-paste-branch-no-format-blob-wins | `absent` | `absent` | PASS |

### 2.10 Extra checks I added while in there (17 assertions, all PASS)

| Group | Assertion → observed |
|---|---|
| Thumbnail (§6 "capped on BOTH axes") | 100×4000 → `96`×`2`; 1920×1080 → `96`×`54`; 40×30 → `40x30` (never upscales) — all exact |
| Migration idempotence (§7) | legacy `pinned=1` row adopted into Pinned (`legacy-hidden`), main history untouched (`1`), one builtin tab, **Pinned id stable across 3 opens** (`1`), state string `2/0/1/0/19` byte-identical on re-run |
| Alpha at capture (§6 "PNG read FIRST") | PNG + a real CF_DIB both on the clipboard → stored image is **byte-identical to the PNG**, decoded alpha = `128`, `Text` is `<null>`, kind=`image`, thumb present |

---

## 3. Mutation table (false-green proof)

Each mutation was applied to a **copy** in `mut\`, rebuilt, and the relevant group re-run. Build exit 0 in every case, so the RED is a test result, not a compile error.

| # | Mutation (in the copy only) | Expected symptom | Observed |
|---|---|---|---|
| a | Dedupe match `text = $v` → `text LIKE $v` | case-sensitivity test goes red | **harness exit 1**: `dedupe/lowercase-is-different expected=false observed=true`, `lowercase-two-rows expected=2 observed=1`, `lowercase-probe-kept expected=true observed=false`, `lowercase-both-texts-distinct expected=2 observed=1` |
| b | `EnforceCap` eviction drops the `tab_id IS NULL` scope | pinned rows get evicted | **harness exit 1**: `cap/pinned-survive expected=2 observed=0`, `cap/pinned-ids-survive expected=6,7 observed=<none>`, `cap/pinned-formats-survive expected=2 observed=0`, `cap/all-in-tabs-untouched expected=3 observed=2` |
| c | `HtmlFragment` slices the **string** at byte offsets | non-ASCII torture fragment goes red | **harness exit 1**: `html/byte-offset-fragment expected=表格 🎉 हिन्दी observed=表格 🎉 हिन्दी<!--EndFragment-->`, `html/mid-multibyte-clean-prefix expected=abc表 observed=abc表格<!` |
| d | Dedupe scope drops `AND tab_id IS NULL` | fresh main copy is swallowed by the pinned row | **harness exit 1**: `tabscope/fresh-main-does-not-merge-into-pinned expected=false observed=true`, `tabscope/main-has-own-row expected=1 observed=0`, `tabscope/main-row-is-not-the-pinned-row expected=different-id observed=no-main-row` |

Restore: rebuilt against the real sources → **exit 0 (all groups green at that point)** (`evidence-after-restore.txt`). The repo files were never touched.

> Honest caveat on (c): slicing chars at byte offsets does not garble the CJK here — it **overshoots** and drags `<!--EndFragment-->` into the result. The assertion still catches it, but the symptom is "extra trailing markup", not "mojibake".

---

## 4. Real discrepancies → fix-sprint worklist

Ranked by user impact. Only D1 is a measured behavioural defect; the rest are contract-vs-code or doc-vs-code gaps.

**D1 — Dedupe does not move a re-copy to the top when it happens inside the same second. (REAL, low severity, measured)**
`docs\CHECKLIST-features.md` §9 says ✅ "moved to top, fresh timestamp". Observed: `order=B,A` — the re-copied `A` stayed **below** `B`.
Cause: `CapturedAt` is second-resolution (`yyyy-MM-ddTHH:mm:ss`) and ordering is `captured_at DESC, id DESC`; `TryDedupeMoveToTop` rewrites the timestamp but **keeps the old id**, so a same-second re-copy loses the tie-break. Two copies landing in the same second is ordinary (100 ms debounce, copy → copy → re-copy).
Impact: after reopening the popup the "newest" item can be the wrong one until the next capture. The in-session UI still shows it first because `OnNewItem` inserts at the head, so this only shows up on reload — which is why 41/41 harness runs (that never tested the same second) missed it.
Cheapest fixes for the fix sprint (not mine to choose): add a monotonically increasing `seq INTEGER` touched on every capture and order by it, or store millisecond timestamps, or re-insert the row (new id) instead of updating in place. Any of the three also makes §9's wording literally true.

**D2 — "retires the flag" is not what the migration does. (doc overstatement, measured)**
§7 ✅ says migration "adopts existing `pinned=1` rows into Pinned and **retires the flag**". Measured: `rows-with-pinned-flag-still-set=1` — the column still holds `1`; the migration only sets `tab_id`. Behaviourally correct (nothing reads `pinned` after `Migrate()`; the structural guarantee is `tab_id`), so this is wording or a one-line `UPDATE items SET pinned = 0 WHERE tab_id IS NOT NULL`. Either close it or say "the flag is left inert".

**D3 — "patched-IHDR bomb rejected in 4 ms" is not reproducible as a literal. (claim precision)**
Measured on a genuine patched-CRC bomb: **cold 59 ms** for the first call in a fresh process (WIC initialisation is included), **warm 0 ms**. The meaningful claim — the header is rejected before any pixel decode and no row is written — holds exactly (`pixel/no-row-stored=0`, `Skipped` fired with the human reason). Also worth knowing: a PNG whose structure is broken (not just oversized) is rejected through the catch-all as `Image skipped: unreadable image` (fail-closed) — a different user-facing message than "too large".

**D4 — Two `⬜` boxes understate what exists (false negatives, not false greens).**
- §5 line 56 `⬜ [H]` CF_HTML byte-offset extraction — implemented **and** now proven on 3-byte + 4-byte UTF-8 (§2.5); flip to ✅ with this evidence.
- §10 line 102 `⬜ [L] "S3 must land Config.PreviewLines"` — `Config.PreviewLines = 10` is in `Core\Config.cs` now. Whether the popup consumes it is S3's problem, but the config half is done.

**D5 — The composition root throws fidelity away on paste. (highest-impact S3 item on this axis)**
`App.xaml.cs:77` calls `PasteInjector.CopyAndPaste(item.Text)`. For an **image** item `Text` is `null`, so the live app puts an *empty string* on the clipboard; for a rich/Excel item it pastes plain text and never calls `ClipboardStore.GetFormats`/`GetImage`. The core is verified green (this audit, §2.9), so §3 "restore-first paste" and §6 "paste-back sets a real CF_DIB" will read as "core done, wiring pending" — but the user-visible result today is "image paste does nothing". Flagging it because a ✅ core + 🟡 wiring reads as nearly-done. Must-fix in S3: `OnPasteRequested` → `CopyAndPaste(item)` with formats/image loaded.

**D6 — A custom tab may be named `Images` or `Clipboard`. (observation, no checklist claim)**
`CreateTab` only rejects blank / >24 / control chars / duplicates (verified). Harmless in the store; the S3 tab strip and the ←/→ walk must not confuse a custom "Images" with the Images **view**. Decide before the tab UI lands.

---

## 5. Weak evidence / what this harness could **not** prove

Called out on purpose — these are the places a green tick here should not be read as proof.

1. **`HtmlFragment`'s last-resort line** (`return Encoding.UTF8.GetString(...)` after 4 trims) never executed. For a producer that merely truncates mid-character, 3 trims always suffice, so "never emits U+FFFD" is proven **only** for truncation, not for a fragment containing genuinely invalid bytes (a broken producer). One untested branch.
2. **Clipboard-busy retry** (5 × 30 ms, `Snapshot`/`Restore`/`PasteInjector`) not exercised — I could not hold the clipboard locked deterministically without a helper process. The code path is read-verified only.
3. **Self-paste suppression** (`GetClipboardSequenceNumber`) not exercised: my harness calls `CaptureService.Capture()` directly and never goes through the listener, so a real self-paste round-trip is unproven here.
4. **`maxItemBytes` (10 MB) snapshot budget** not exercised (needs a >10 MB clipboard payload).
5. **`CF_HDROP` encode/decode round-trip** (`DecodeFileDrop`) not exercised — outside this axis's assigned rules; §3 is 🟡 anyway.
6. **`Config.Load` corrupt/partial-JSON fallback** (§10) not tested — I never call `Load()`/`Save()` so the live `config.json` stays untouched.
7. **Everything marked `[L]`/`[U]`** (popup, hotkey, tray, notification, editor, settings, RAM, Excel→Excel) genuinely needs the running app: not attempted here, by design. This axis says nothing about them.
8. **`EnforceCap` with `maxItems = 200`** — I proved the mechanism at cap=3. The default in `Config` is 200 (read), but the 200-row behaviour was not measured.

---

## 6. Cleanup and environment

- **Stale `Clip.exe`:** two instances were running at audit start (pids 28128 and 97992, from `bin\Release\net10.0-windows\Clip.exe`) owning Alt+V and the live db. Both were killed before any clipboard-writing test; the first (15:20–15:27) generation of every clipboard/pixel/alpha result above was produced with the app **down**.
- **Restart confirmation:** a `Clip.exe` is running now — pid **44508**, started **15:27:42**, path `C:\Users\mayan\CascadeProjects\Clipboard\bin\Release\net10.0-windows\Clip.exe`. It was (re)built by a parallel agent at 15:21:48 and is **not mine**; I left it running. Live `%APPDATA%\Clip\clip.db` mtime 15:31:58, `config.json` 15:27:39.
- **Side effect to know about:** the final full run (≈15:31) happened while that fresh instance was live — an unplanned second opinion: every clipboard/pixel/alpha assertion returned the **same** result with the app running as it did with the app down. The cost is that the copy-history in `%APPDATA%\Clip\clip.db` may now contain my test strings (`axis1 plain text`, `axis1-clobber`) and one 4×4 test image; the app's own Clear History removes them.
- **Scratch deleted:** `C:\Users\mayan\AppData\Local\Temp\opencode\axis1` (harness project, `mut\` copies, logs, mutation report) and every `%TEMP%\axis1-*.db*` file (0 left). Note for future harnesses: the harness's own exit-time delete missed 128 db files because `Microsoft.Data.Sqlite` pools connections and keeps the file handle alive — they were removed in a separate sweep after the process exited.
- **Repo untouched:** no source file, checklist, ledger or config was modified by this audit; this document is the only addition.
