# RESEARCH — keyboard & basic-UX conventions (genre + Windows platform)

**Status:** research complete. Every row below is decided (ADOPT-MUST / ADOPT-SHOULD / SKIP). Nothing here re-proposes a pinned item.
**Deliverable:** §10 is the binding table to append to `docs\CHECKLIST-features.md` §2; §8 is the Esc cascade; §9 is the undo-delete spec the integration sprint needs.
**Research date:** 23 Nov 2026. Sources are numbered in §14; house references are `[H1]`…`[H8]`.
**Method:** primary docs of the tools in the genre (their own manuals/wiki/changelogs), Microsoft Learn + Microsoft Support, and the project's own frozen contracts and code. Anything I could not source is marked **derived** with the reason. No app was launched; no source file was edited.

**Two-line summary:** 5 MUST additions (Home/End, PageUp/PageDown, IME-safe Esc/Enter, type-to-search from anywhere, Tab must not strand focus), 7 SHOULD additions (Ctrl+Tab alias, Ctrl+↑/↓ section jump, Ctrl+C copy-without-paste, Shift+Enter plain paste, Ctrl+Z one-level undo, item context menu, selection stability on live capture). Everything else is skipped with a reason.

---

## 0. Already pinned — checked, not re-proposed

Read before researching; every candidate was rejected if it was already decided here.

| Pinned | Source |
|---|---|
| ↑/↓ navigate; ←/→ tabs; selection resets to first item on tab switch | [H1] §2, §7 |
| Esc #1 clears search, Esc #2 hides; caret stays in search | [H1] §2 |
| Enter pastes; Ctrl+1..9 pastes nth visible item | [H1] §2 |
| Delete deletes the item in list context, character-delete inside any text field | [H1] §2, [H3] §5.3 |
| F2 edit, Ctrl+S save, Esc/F2-with-changes → "Discard changes?" (Yes=discard, No=keep editing) | [H1] §8 |
| Ctrl+P pin (move to/from Pinned); Ctrl+M tab picker; Ctrl+Shift+←/→ adjacent move; Ctrl+T / Ctrl+W tab CRUD | [H1] §7, [H3] §5 |
| Ctrl+Shift+V global plain paste; same chord with the popup open = selected item as plain text | [H1] §4 |
| Live case-insensitive search over text + app name; search box focused on open; drag-anywhere popup; click-away hides | [H1] §2 |
| Selected item scrolls into view; double-click pastes; visible focus treatment on the selected row | [H1] §2, [H6] §5 |
| Esc unwind order: tab picker → tab-name editor → tab-delete confirmation → F2 editor (discard prompt) → clear search → hide | [H3] §5.2 |
| Popup centered on the monitor of the current active window; fixed 550×520; no taskbar/Alt-Tab entry | [H1] §2, [H6] §6 |
| Paste focus poll (deferred), 100 ms hide→paste delay | [H1] §14 |

Current code, for reference: the window-level `PreviewKeyDown` handles exactly Esc (hide), Enter, Ctrl+digit, ↑/↓ (clamped) and Delete [H4 lines 94–136]; the ListBox hides its scrollbar and its items are `Focusable="False"` [H5 lines 55–61].

---

## 1. Verdict key

- **ADOPT-MUST** — genre or platform expectation; its absence is felt as a broken or unfinished list.
- **ADOPT-SHOULD** — clear value, no conflict, cheap; built in the same integration sprint.
- **SKIP** — with the one-line reason (conflict with the pinned contract, bloat, or habit risk).

**derived** = my judgement from behaviour/mechanics rather than a source sentence. It is still a closed decision.

---

## 2. Candidate table — list navigation

| # | Affordance | Who ships it (evidence) | Verdict |
|---|---|---|---|
| N1 | `Home` / `End` jump to first / last item | CopyQ lists exactly "PgDown/PgUp, Home/End, Up/Down — item list navigation" [R4]. Windows uses Home/End for start/end of a list or document [R11]. | **ADOPT-MUST**, with a guard: only when the search box is empty (see N10). |
| N2 | `PageUp` / `PageDown` move one viewport of rows | CopyQ [R4]. Raycast spends `Alt+↑/↓` on page jumps and `⌘↑/↓` on sections, i.e. paging is a first-class key in the genre, not a mouse-only action [R1]. Windows PageUp/PageDown = one page [R11]. | **ADOPT-MUST**. |
| N3 | `Ctrl+Home` / `Ctrl+End` as a second top/bottom chord | No clipboard manager documents it. CopyQ ships plain Home/End only [R4]. | **SKIP** — redundant with N1/N2 and it steals the caret's document start/end while a query is being typed [R11]. |
| N4 | `Alt+↑/↓` page jumps | Raycast [R1]. | **SKIP** — a second page binding; PageUp/PageDown is the Windows spelling and Alt+arrows conflict with nothing but add nothing. |
| N5 | `Ctrl+↑` / `Ctrl+↓` jump to the previous / next time section | Raycast: `⌘↑ / ⌘↓` "Jump to the previous or next section" [R1]. Clip's pinned design renders Today / Yesterday / date sections [H6] §1, so the concept exists. | **ADOPT-SHOULD** — the only affordance that makes our date sections navigable; computed from `CapturedAt` so it works before section headers are built. |
| N6 | `Ctrl+Tab` / `Ctrl+Shift+Tab` next / previous tab | Windows dialog convention, documented verbatim [R11]; CopyQ ships both alongside ←/→ [R4]. | **ADOPT-SHOULD** as an alias of the pinned ←/→ (zero new logic). |
| N7 | `Ctrl+PageUp/Down` next/previous tab | Browser idiom; absent from the Windows dialog list [R11]. | **SKIP** — PageUp/PageDown is list paging here (N2); shadowing it would be a coin-flip users lose. |
| N8 | `Ctrl+0` focus last tab | CopyQ [R4]. | **SKIP** — ←/→, Ctrl+Tab and Ctrl+M already cover tab movement; Ctrl+digit is pinned to paste-nth [H1] §2. |
| N9 | Selection wraps from last→first when pressing ↓ at the end | No result list in the surveyed set wraps. Raycast *repurposes* the at-the-end press for search history instead of wrapping [R1]; Qt/Windows list controls stop at the boundary (**derived** from the platform list-view model). | **SKIP** (keep the current clamp [H4 line 119]) — wrapping turns a held ↓ into a silent jump to the top and fights Home/End expectations. |
| N10 | `Home`/`End` while typing in the query | Windows text editing: Home/End move the caret to line start/end [R11]. | **KEEP NATIVE** — this is why N1 is guarded by "query empty". |

---

## 3. Candidate table — selection & search semantics

| # | Affordance | Who ships it (evidence) | Verdict |
|---|---|---|---|
| S1 | Each keystroke re-runs the filter and re-selects the **first** result | CopyQ: typing filters, and "press Enter key to copy the first found item" [R5]. PowerToys Run auto-selects the top result, with a setting to wait for slower plugins before doing so [R10]. Clip already does this (`ApplyFilter` sets index 0) [H4 line 71]. | **KEEP** (no change) — record it as an invariant so nobody "improves" it into sticky selection. |
| S2 | A live capture arriving while the popup is visible must not move the selection | No documentation in any tool; Clip's own `OnNewItem` inserts at index 0 then re-filters, which resets the selection to the new row [H4 lines 54–59]. | **ADOPT-SHOULD** — remember the selected id, restore it after a live insert. Selection must never move under the user's fingers. |
| S3 | `Ctrl+A` selects all text in the search box | Windows text editing [R11]. CopyQ's `Ctrl+A` selects all *items* only because its list owns focus [R4] — our search box owns focus by design [H1] §2. | **KEEP NATIVE** — never repurpose for item multi-select. |
| S4 | Typing anywhere (list focused, after the editor closes) lands in the search box | CopyQ: "Start typing a text to search items" [R4]. Raycast is type-to-search by construction [R1]. | **ADOPT-MUST** — one `PreviewTextInput` redirect when focus is not a text field. Without it, the pinned "focus returns to the list" after F2 [H1] §8 creates a dead state where typing does nothing. |
| S5 | `/` focuses the search box | No clipboard manager in the set. `/` is a PowerToys *plugin prefix* only in the `//` form [R10]. | **SKIP** — the search box is always focused, and with the query focused `/` must insert a slash; binding it would break searching for paths. |
| S6 | `Ctrl+L` focuses the search box | Explorer's `Ctrl+L` = address bar [R11]. | **SKIP** — there is no address bar; the same dead-state problem S4 solves would exist only if we made focus land elsewhere, which we are not doing. |
| S7 | `Tab` / `Shift+Tab` cycle focus inside the popup | Windows dialogs move focus with Tab [R11]; Ditto documents "Tab — move focus to the next control" [R7]. | **ADOPT-MUST (as a no-op)** — the popup has exactly one control. Tab must be consumed so focus cannot land on the ListBox, where typing stops filtering and keys silently change meaning. |
| S8 | `Ctrl+Backspace` / `Ctrl+Delete` delete a word; `Ctrl+←/→` move by word | Windows text-editing contract [R11]. | **KEEP NATIVE** — must pass through untouched in the search box and the editor. A verification item, not a binding. |

---

## 4. Candidate table — item actions

| # | Affordance | Who ships it (evidence) | Verdict |
|---|---|---|---|
| A1 | `Ctrl+C` copies the selected card back to the clipboard **without** pasting | Raycast: `⌘C / Ctrl+C` "Copy" as a common item action [R1]. CopyQ: "Copy the selected items back to clipboard with Enter key or `Ctrl+C`" [R5]. Alfred: "Press Return to paste … or use `⌘C` to copy the content to your current clipboard" [R3]. | **ADOPT-SHOULD** — distinct from Enter (no injection) and from the pinned Ctrl+Shift+V (that pastes plain text into the app). Guard: only when the query has no text selection, so copying selected query text still works natively. |
| A2 | `Shift+Enter` pastes the selected item as **plain text** | Ditto: "Shift-Enter — paste CF_TEXT (plain text with no formatting) only" [R7]. Maccy: `⌥⇧↵` paste without formatting [R9]. Raycast exposes plain paste through the Action Panel [R2]. Clip's equivalent is pinned to Ctrl+Shift+V [H1] §4. | **ADOPT-SHOULD** as an alias of the pinned chord (one branch). Today *any* Enter pastes formatted because the branch ignores modifiers [H4 line 103], so this is also a bug fix for the accidental Shift+Enter. |
| A3 | `Alt+Enter` opens clip properties | Ditto [R7]. | **SKIP** — F2 is our editor, and Alt+Enter has no other meaning in a borderless window; a second edit path is surface with no user. |
| A4 | `Space` toggles a full preview | No clipboard manager documents Space. Raycast's Quick Look is `⌘Y / Ctrl+Y` [R1]. Clip's selected card already expands into the rich preview [H1] §5, [H6] §1. | **SKIP** — Space is a character; with the search box focused it must type a space. This one would be a user-visible bug. |
| A5 | `Ctrl+Z` undoes an accidental delete | No built-in support in the genre: CopyQ requires the community "Undoable Move to Trash" script for Ctrl+Z [R6]; Ditto's answer is a delete *confirmation* instead (reported in the same thread) [R6]; Raycast documents delete with no undo [R2]; Windows clipboard documents only menu Delete [R12]. | **ADOPT-SHOULD** — one level, in-memory, session-only. Spec in §9. |
| A6 | Multi-select (`Ctrl+click`, `Shift+↑/↓`) with batch paste/delete | Ditto: multi-select by Shift+arrows or Ctrl+click, then paste them all [R8]. CopyQ: `Ctrl+A` select all, plural "delete selected items" [R4]. Raycast and Windows clipboard are single-select [R2][R12]. | **SKIP** — it requires a list-focus mode that kills type-to-search (S3/S4), and adds undefined batch-paste ordering. Bulk removal already exists as Clear History and tab delete. |
| A7 | Context menu on the selected item via **right-click**, the **Menu key**, and **Shift+F10** | Ditto's and CopyQ's primary command surface is a right-click menu [R7][R4]; Windows clipboard exposes per-item "See more (…) → Delete" [R12]; Windows: Shift+F10 displays the context menu for the selected item [R11]; the Application (Menu) key "displays an application-specific context menu" [R13]; Raycast uses `Ctrl+K` for the same role [R1]. | **ADOPT-SHOULD** — the only pointer route to CRUD. Seven items, all existing commands, no new logic. Guard: the chord always opens the *item* menu; right-click is context-sensitive (row → item menu, search box → native text menu). Nothing is lost by diverting the chord — the text menu's Cut/Copy/Paste/Undo all have working chords in the search box. |
| A8 | `Ctrl+K` action panel | Raycast [R1]. | **SKIP** — a second overlay that duplicates A7 and the existing keys; two entry points to keep in sync. |
| A9 | `Shift+Insert` paste / `Ctrl+Insert` copy aliases | Windows text-editing contract [R11], but no launcher in the set ships them. | **SKIP** — the genre binds Ctrl+C/Enter; two spellings per action is exactly the double-binding we are checking for. |
| A10 | Paste-as-format picker (plain / RTF / HTML / PNG) | Raycast "Paste as…" in the Action Panel [R2]. | **SKIP** — the pinned plain-paste chord covers the 95% case; a per-format picker needs a popup surface and an owner decision we do not have. |
| A11 | `Ctrl+E` external editor, `F5` action dialog, `F3` full text | CopyQ [R4], Ditto [R7]. | **SKIP** — F2 already edits; F5/F3 have no job once the selected card expands. |
| A12 | `Ctrl+Q` quit application | CopyQ [R4]. | **SKIP** — quitting a tray app from a transient popup is a hazard; Exit lives in the tray [H1] §1. |
| A13 | `Ctrl+,` open Settings | Raycast [R1], Maccy [R9] — both launch-bar apps. Ditto/CopyQ use a Preferences menu, not a chord. | **SKIP** — launcher-only convention; the tray owns Settings [H1] §10. Genre is split, and the chord would need a cross-window action from a popup that must hide first. |

---

## 5. Candidate table — overlays & Esc

| # | Affordance | Who ships it (evidence) | Verdict |
|---|---|---|---|
| E1 | Esc unwinds one layer per press, innermost first | Raycast: "Esc: Go back to the previous view. From Root Search, closes the window"; the Action Panel's own Esc closes the panel or a sub-menu first [R1]. CopyQ: "Esc — cancel search, hide window" [R4]. Windows: Esc is "Stop or leave the current task, or dismiss a dialog box" [R11]. | **ADOPT** (spec in §8) — one layer per press, no double-closes. |
| E2 | Esc while an IME composition is active cancels the composition, not the window | WPF: `Key.ImeProcessed` is "a special key masking the real key being processed by an IME" [R13]; `KeyEventArgs.ImeProcessedKey` exposes the real key when the IME handles it [R14]; composition start/update/complete are separate routed events [R15]. Microsoft ships a Servicing note about WPF apps closing/stopping when an IME is in use [R16], and a current WPF issue records Esc-canceled compositions leaking and keeping closed windows alive [R17]. The bug class is real in shipping apps: "Escape during IME composition closes dialogs or discards drafts" [R18]. | **ADOPT-MUST** — this is the difference between "supports Hindi/Chinese search" and "deletes the user's IME work by closing the popup". |
| E3 | Esc while the F2 editor has unsaved changes asks to discard | Pinned [H1] §8. | **KEEP** — becomes level 2 of the cascade. |
| E4 | Esc closes the popup even when a child overlay is open | Current code does exactly this [H4 line 98–102]. | **REPLACE** by §8 — this is why the cascade must be one function, not a branch per overlay. |

---

## 6. Candidate table — pointer and window UX

| # | Affordance | Who ships it (evidence) | Verdict |
|---|---|---|---|
| P1 | Hover must not move the selection | Not documented in any tool. Clip's rows are `Focusable="False"` and hover does not select [H5 line 61]. | **KEEP** (invariant) — mouse-wheel reading must never change what Enter will paste. |
| P2 | Mouse wheel scrolls without moving the keyboard selection | Platform list behaviour (**derived**). | **KEEP** (invariant) — same reason as P1. |
| P3 | Single click selects a row but does not steal the caret from the search box | Current structure: non-focusable rows keep the TextBox focused [H5 line 61] (**derived**; verify in audit). | **KEEP** — it is what makes "click a row, keep typing to refine" work. |
| P4 | Middle-click in the list does nothing | No clipboard manager in the set documents a middle-click action. | **SKIP** — no precedent; an accidental middle-click while reaching for the scrollbar must not paste or open anything. |
| P5 | Resizable popup / remembered size | Launcher overlays are fixed; Ditto/CopyQ are ordinary windows. Size is owner-pinned at 550×520 [H6] §1. | **SKIP** — a resizable borderless flyout breaks the centered-overlay model for no gain. |
| P6 | Remember the last dragged position | PowerToys Run offers "Preferred monitor position" (primary / cursor / focused window) instead of drag memory [R10]; Clip's owner rule is center-on-active-monitor [H1] §2. | **SKIP** — drag memory would fight the pinned rule and can strand the popup off-screen after a monitor change. Drag still works within a session (pinned). |
| P7 | Visible scrollbar / hover scrollbar gutter | Raycast/Alfred show no scrollbar; Clip hides it [H5 line 55]. | **SKIP** — launcher standard; the selected row is always scrolled into view (pinned), which is the real affordance. |
| P8 | Focus returns to the previously active window after paste **and** after Esc | Ditto/CopyQ paste into "the previous window" [R7]; Clip hides before injecting for exactly that reason [H4 line 84], and the focus poll is already tracked as deferred [H7]. | **KEEP — no new work.** Verify both paths; do not re-open the deferred focus poll here. |
| P9 | Right-click on a row selects it, then opens the menu | Windows list convention (**derived**). | **ADOPT with A7** — the menu must act on the row under the pointer, never on a stale selection. |

---

## 7. Windows platform expectations (Win11 utility window)

| Expectation | Evidence | What Clip must do |
|---|---|---|
| Esc dismisses the innermost thing; it never quits the app | [R11] | §8 cascade; Esc on the empty popup hides (pinned). |
| F2 = "rename/edit", F5 = refresh, F10 = menu bar, F1 = help | [R11] | F2 edit is already the convention; we ship no menu bar, so F10 stays native (**derived**). |
| IME composition owns Esc, Enter and the arrow keys while composing | [R13][R14][R15][R16][R18] | §8 + the Enter guard in §10. Filtering may still run on the composed text as WPF updates it. |
| `Win+B` focuses the notification area; the tray icon must then be reachable and its menu openable | [R11] | Verification item: tray menu reachable by keyboard (Win+B, arrows, Enter / Shift+F10). H.NotifyIcon caveat already recorded [H1] §13. |
| `Shift+F10` and the Menu (Application) key open the selected item's context menu | [R11][R13] | A7 — always in the popup, including while the search box has focus; right-click stays context-sensitive. |
| Numpad Enter is Enter | WPF maps both to the same value: `Enter = 6`, `Return = 6` [R13]; the key is `VK_RETURN` [R19]. | No branch needed; **verification item** with NumLock on and off. |
| Ctrl+Backspace / Ctrl+Delete / Ctrl+arrows are word and caret editing | [R11] | Never intercept; verification item in search and editor. |
| Sticky/Filter keys change repeat behaviour at OS level | Windows accessibility features; FilterKeys ignores repeated keystrokes (**derived**). | Nothing to build; note that held-↑ navigation is slower with FilterKeys on — accepted, it is the feature's purpose. |
| Hotkeys and injection use virtual-key codes, so they are layout-independent (Ctrl+V pastes on Cyrillic/Dvorak) | Injection is `VK_V`/`VK_RETURN` [R19] (**derived**: VK codes are key identities, not characters). | No code change; **verification item**: live paste with a Russian layout and with Dvorak. The known friendly-name → VK parser bug [H1] §10 is a separate defect. |
| A tool window must not appear in Alt-Tab/taskbar and must hand focus back on hide | [H6] §6, [H4 line 84] | Already pinned; verify after the cascade lands. |

---

## 8. The Esc cascade (exact order, one press = one layer)

**Principle:** one function owns Escape (`HandleEscape()`), called from the popup's `PreviewKeyDown`; the F2 editor is a separate window and owns its own Escape. No overlay handles Escape on its own except the native context menu (a Win32 popup that swallows the key before we see it).

### 8.1 The ordered stack

| Press | State at the moment of the press | Action | Resulting state |
|---|---|---|---|
| 0 | A native context menu is open (right-click / Menu / Shift+F10) | Consumed by the menu (we never see it) | Menu closed, popup unchanged, focus back to search |
| 1 | IME composition active in any text field | Consumed by the IME; **the cascade does not run** | Composition canceled, text field keeps its pre-composition text |
| 2 | "Discard changes?" prompt open (inside the editor window) | Activate **No** (keep editing) | Prompt closed, editor still open, caret where it was |
| 3 | Tab-delete confirmation open | Cancel | Confirmation closed, tab intact, focus back to search |
| 4 | Tab-name editor open (Ctrl+T in progress) | Cancel creation | Inline editor gone, focus back to search [H3] §5 |
| 5 | Ctrl+M tab picker open | Close picker | Focus back to search |
| 6 | F2 editor open, **no** unsaved changes | Close editor | Editor gone, focus back to the list at the same item [H1] §8 |
| 6b | F2 editor open, unsaved changes | Ask "Discard changes?" (default **No**) [H1] §8 | Prompt open; next Esc = level 2 |
| 7 | Search text non-empty (composition finished) | Clear search | Full list returns, caret stays in the search box, list selection = first item [H1] §2 |
| 8 | Search empty, no overlay | Hide the popup | Focus returns to the window that was active at hotkey time |

**Multi-overlay rule:** levels 3–6 are mutually exclusive by construction (the F2 editor disables tab CRUD, and the picker/tab-name editor/confirmation each own the keyboard while open [H3] §5). They are listed in stack order anyway so the code has one ordered if-chain and can never double-close.

**IME gate (E2), precise:**
- `if (e.Key == Key.ImeProcessed) return;` at the top of `PreviewKeyDown` — the real key is under the IME's control [R13][R14].
- Additionally track composition with `TextCompositionManager` start/complete events on the search box and editor, and skip the cascade while a composition is open [R15]. This covers the case where the IME has started a composition but the current key event is not yet masked.
- The **same gate** applies to Enter (A-paste), Delete (item/char) and ↑/↓: committing a candidate with Enter must never paste a clip.
- Do **not** gate on `InputMethod.Current.ImeState == On` alone: a user who leaves the Chinese IME enabled but idle must still get normal Esc/Enter.

**Enter guard (derived from the same rule):** while composing, Enter commits; while not composing, Enter pastes the selected item.

---

## 9. Undo delete (adopted, one level, session-only)

### 9.1 What veterans do

| Tool | Behaviour | Source |
|---|---|---|
| CopyQ | Delete is permanent and immediate; Ctrl+Z is **not** built in. The maintainer points users to a community "Undoable Move to Trash" command; the 2025/26 status is still "permanent and immediate" | [R6] |
| Ditto | Asks for confirmation before deleting (reported in the same thread) | [R6] |
| Raycast | Delete entries (single or by time window); no undo documented | [R2] |
| Windows clipboard (Win+V) | Per-item "See more → Delete"; no undo documented | [R12] |
| Maccy | `⌥⌫` deletes; no undo documented | [R9] |

**Conclusion:** undo is *not* a genre expectation, so it is SHOULD, not MUST. But our Delete is a single bare keystroke with no confirmation, and it destroys the original format blobs (`DELETE FROM item_formats` + `items` [H8 lines 261–267]) — unrecoverable. A one-level, in-memory undo is smaller than a confirmation dialog and does not interrupt the keyboard flow. **Adopted.**

**Privacy note (the CopyQ objection):** the buffer is memory-only, holds at most one item, and is cleared when the popup hides. Content comes back only on an explicit Ctrl+Z in the same popup session.

### 9.2 Behaviour spec

| Trigger | Result |
|---|---|
| `Delete` on the selected item | Item deleted exactly as today; the deleted item (fields + extra formats + full image when `kind='image'`) is copied into `_undoItem` first. A second delete replaces the buffer (one level). |
| `Ctrl+Z` with `_undoItem != null`, no overlay/editor open | Restore the row and its formats, undo level cleared, restored item selected and visible (see 9.3). |
| `Ctrl+Z` with `_undoItem == null` | **Not handled** — let the search box's native text undo run (Ctrl+Z = "undo the last typing" [R11]). This is the whole conflict resolution. |
| Any overlay/editor focused | Ctrl+Z is theirs (text undo). The list-level undo never fires. |
| Popup hides (Esc, paste, click-away) | `_undoItem = null`; the buffer never survives a popup session. |
| `Ctrl+W` tab delete, Clear History, cap eviction | **Not undoable** — tab delete already names the count and defaults to Cancel [H3] §5, Clear History has an inline confirm [H1] §10, eviction has no UI action. |

**Priority rationale to keep in the code comment:** the query is transient (Esc restores the whole list), a deleted clip is not; therefore Ctrl+Z prefers the destructive action and gives up one level of text-undo in a one-line search field.

### 9.3 Core layer — what the integration sprint must add now

`ClipboardItem` has no `TabId` today [H8 `Core\ClipboardItem.cs`]; the tabs sprint adds it [H3] §3. Undo therefore lands **with or after** the tabs schema.

```csharp
// Core\ClipboardStore.cs — new, one transaction
// RESTORE one previously deleted row. No dedupe (explicit user action).
public long Restore(ClipboardItem it);
```

1. `INSERT INTO items (kind, text, html, rtf, image, thumb, uris, app_name, window_title,
   captured_at, size_bytes, tab_id)` — **explicit `captured_at`** so the item returns to its original position, not the top.
2. `item_formats` rows inserted in the same transaction (`InsertFormats` logic, reused).
3. If `tab_id IS NULL` and main-history count > `maxItems`: delete the single **oldest other** main-history row (`id <> $new`). The restored row therefore always lands. Container tabs are never capped [H1] §1, so no cap work for `tab_id` rows.
4. Orphan-format cleanup at the end, as every other delete path does [H3] §4.
5. Return the new id.

UI side (`UI\MainWindow.xaml.cs`, ~25 lines):
- Before `_store.Delete(id)`: build the undo copy — the loaded item plus `GetImage(id)` when `kind='image'` and `GetFormats(id)` [H8] — then delete.
- `Ctrl+Z` branch: restore, then jump to the item's tab if it is not the current one, re-apply the filter, select the restored id; **if the current query would hide the restored row, clear the query first** so the undo is always visibly successful.
- `_undoItem = null` in the existing hide path.
- `shortcut:` comment on the one-level buffer (`// shortcut: one-level, session-only undo; add a level only if the owner asks`).

**Memory:** worst case one item = `maxItemBytes` formats (10 MB) + one PNG (`maxImageBytes` 5 MB) held until the popup hides. The RAM protocol [H1] §1 samples "after heavy use": add **delete + undo of a 5 MB image** to that worst-case sample, and confirm the buffer is released on hide.

**Verification (add to CHECKLIST §2):**
- `[H]` Delete then Restore round-trips every column and every `item_formats` row byte-for-byte; cap count is unchanged; no orphan formats.
- `[L]` Delete an image → Ctrl+Z → the image pastes correctly (full PNG, not the thumbnail); Delete in Pinned → Ctrl+Z → item returns to Pinned.
- `[L]` Ctrl+Z in an empty-history popup still undoes typing in the search box (native), never deletes.

---

## 10. Adopt list — exact bindings to append to the keyboard contract

**Surface column** = where the binding is live. **Guard** = the exact condition, so no decision is left to the implementer. All rows are new bindings or new guards on pinned bindings; none duplicates a pinned chord.

### 10.1 MUST

| Binding | Surface | Action | Guard / conflict check |
|---|---|---|---|
| `Home` | Popup list (no overlay) | Select the first visible item | Only when `Search.Text.Length == 0`; otherwise native caret-to-start [R11]. No pinned chord uses Home. |
| `End` | Popup list (no overlay) | Select the last visible item | Same guard. No pinned chord uses End. |
| `PageUp` | Popup list (no overlay) | Move selection up one viewport of rows, clamped | Always when no overlay owns the keys (the single-line query has no page). Never crosses into another tab. |
| `PageDown` | Popup list (no overlay) | Move selection down one viewport of rows, clamped | Same. Page size = `max(1, floor(viewportHeight / rowHeight))` from the realized container, fallback 8. |
| *IME gate* | Search box, editor, popup | Consume Esc/Enter/Delete/arrows while a composition is active; run no cascade and paste nothing | `e.Key == Key.ImeProcessed` or composition tracked open [R13][R15]. Applies before every branch in `PreviewKeyDown`. |
| *Type-to-search redirect* | Popup | Any printable character while focus is not a `TextBoxBase` focuses the search box and inserts the character | `OnPreviewTextInput` on the window; do not consume the character. Closes the pinned "focus returns to the list" dead state [H1] §8. |
| `Tab` / `Shift+Tab` | Popup (no overlay) | No-op; focus stays in the search box | Window-level, `Handled = true`. A single-control popup has nothing to cycle; this prevents focus landing on the ListBox and silently changing key meaning [R11]. |

### 10.2 SHOULD

| Binding | Surface | Action | Guard / conflict check |
|---|---|---|---|
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Popup (no overlay) | Next / previous tab (alias of pinned ←/→) | Same reset-to-first-item rule as ←/→ [H1] §2. Windows dialog convention [R11]; no pinned chord uses Ctrl+Tab. |
| `Ctrl+↑` / `Ctrl+↓` | Popup list (no overlay) | Jump to the first item of the previous / next time section (`CapturedAt` bucket) | No-op at the first/last section. Free chord (Ctrl+Shift+←/→ is pinned for adjacent tab move; Ctrl+↑/↓ is unused). CopyQ uses Ctrl+Up/Down to reorder [R4]; we do not reorder (newest-first is pinned), so no conflict — documented habit risk. |
| `Ctrl+C` | Popup, selected card | Restore the selected item to the clipboard with all its formats, no paste; popup stays open and the existing copy pulse plays | Only when `Search.SelectionLength == 0` (otherwise native text copy). Reuse the paste path's format-restore helper with injection off, and call the same self-paste suppression (`CaptureService.Suppress(seq)`) or the copy re-enters history and jumps to the top [H1] §3. |
| `Shift+Enter` | Popup, selected card | Paste as plain text (alias of pinned Ctrl+Shift+V) | Branch Enter on `Shift`; today any Enter pastes formatted [H4 line 103]. Never in the editor (there it is a newline, native). |
| `Ctrl+Z` | Popup (no overlay/editor) | Undo the last single-item delete (§9) | Only when the undo buffer is non-empty; otherwise native text undo [R11]. One level; cleared on hide. |
| `Shift+F10` / `Menu` (`Key.Apps`) | Popup (no overlay) | Open the selected item's context menu | Always — including while the search box has focus (otherwise the chord would be nearly dead). Right-click stays context-sensitive: row → item menu (and that row is selected first), search box → native text menu. Menu items: Paste · Paste as plain text · Copy · Edit · Pin/Unpin · Move to tab… · Delete (destructive last, separated). No new logic — each item invokes an existing command. |
| *Selection stability* | Popup | A live capture while the popup is visible keeps the selection on the same item id | Exception: the pinned "selection resets to first item" action (tab switch, filter change) still wins [H1] §2. |

### 10.3 Checklist block to paste into `docs\CHECKLIST-features.md` §2

```markdown
- ⬜ `[L]` **Home/End** jump to first/last item when the search box is empty; with a query they stay native caret keys
- ⬜ `[L]` **PageUp/PageDown** move one viewport of rows, clamped at the ends (selection stays visible)
- ⬜ `[L]` **IME safety**: while a Hindi/Chinese/Japanese composition is active, Esc/Enter/Delete/arrows go to the IME — Esc cancels the composition without clearing the search or hiding; Enter commits without pasting a clip
- ⬜ `[L]` Typing while focus is on the list (e.g. after F2 closes) redirects the character into the search box
- ⬜ `[L]` Tab/Shift+Tab never move focus off the search box in the popup
- ⬜ `[L]` **Ctrl+Tab / Ctrl+Shift+Tab** switch tabs (alias of ←/→, selection resets to first)
- ⬜ `[L]` **Ctrl+↑/Ctrl+↓** jump to the previous/next time section
- ⬜ `[L]` **Ctrl+C** copies the selected card with all formats, no paste, popup stays open, copy pulse plays; never re-enters history
- ⬜ `[L]` **Shift+Enter** pastes the selected item as plain text (same result as Ctrl+Shift+V with the popup open)
- ⬜ `[L]` **Ctrl+Z** restores the last deleted item (one level, same popup session), visible and selected; with nothing to restore it is the search box's native text undo
- ⬜ `[U]` **Item context menu** on right-click / Menu key / Shift+F10: Paste, Paste as plain text, Copy, Edit, Pin, Move to tab…, Delete
- ⬜ `[L]` A capture arriving while the popup is open does not move the selection off the item the user is on
```

---

## 11. Skip table

| Skipped | One-line reason |
|---|---|
| `Space` preview toggle | Space must type a space into the focused search box; the selected card already expands into the preview [H1] §5. |
| `/` to focus search | Search is always focused; with the query focused, `/` must type a slash (paths, URLs). |
| `Ctrl+L` to focus search | There is no address bar; it would be a no-op alias of an always-true state. |
| `Ctrl+A` / multi-select / batch paste | Needs a list-focus mode that kills type-to-search; batch-paste order is undefined; Clear History and per-item Delete cover bulk removal. |
| `Ctrl+Shift+↑/↓` extend selection | Same as multi-select. |
| `Ctrl+Home` / `Ctrl+End` | Redundant with Home/End + PageUp/PageDown, and steals document start/end in the query. |
| `Alt+↑/↓` page jump | Second spelling for N2; PageUp/PageDown is the Windows one. |
| `Alt+Enter` properties | F2 is our editor; a second edit path with no other meaning. |
| `Ctrl+K` action panel | Duplicates the context menu plus existing keys; two surfaces to keep in sync. |
| Paste-as-format picker | Pinned plain-paste covers the common case; needs a new popup surface and an owner decision. |
| `Shift+Insert` / `Ctrl+Insert` | No clipboard manager in the set ships them; the genre binds Ctrl+C/Enter. |
| `Ctrl+PageUp/Down` tab switch | PageUp/PageDown is list paging here; the Ctrl variant is a browser idiom. |
| `Ctrl+0` last tab | ←/→, Ctrl+Tab and Ctrl+M already cover it; Ctrl+digits are pinned to paste-nth. |
| `Ctrl+E` external editor, `F5` action dialog, `F3` full text | Either duplicates F2 or adds a surface with no job once the card expands. |
| `Ctrl+Q` quit | Quitting from a transient popup is a hazard; Exit lives in the tray. |
| `Ctrl+,` Settings | Launcher-only convention (genre split); the tray owns Settings and the popup would have to hide first. |
| Middle-click action in the list | No precedent; an accidental middle-click near the scroll region must do nothing. |
| Resizable popup | Fixed overlay by owner decision (550×520); resizing breaks the centered model. |
| Remember dragged window position | Owner rule is center-on-active-monitor; drag memory fights it and can strand the popup. PowerToys exposes a monitor setting instead [R10]. |
| Visible scrollbar | Launcher standard; selection always scrolls into view, which is the actual affordance. |
| Hover moves the selection | Would let mouse-wheel reading change what Enter pastes. |
| Undo for tab delete / Clear History / cap eviction | Both bulk paths already confirm (count-named, Cancel default); a second undo level multiplies states. |

---

## 12. Verification items added by this doc (no new binding)

1. Numpad Enter pastes, with NumLock on and off (`Enter = Return = 6` [R13]).
2. `Ctrl+Backspace`, `Ctrl+Delete`, `Ctrl+←/→` word editing works in the search box and the F2 editor (never intercepted).
3. Live paste with a Russian layout and with Dvorak (VK injection, layout-independent).
4. Tray menu reachable by keyboard: `Win+B`, arrows, Enter / Shift+F10.
5. Esc-hide returns focus to the previous window, not to nothing (same evidence path as the deferred paste focus poll [H7]).
6. Single click on a row leaves the caret in the search box (P3).
7. Holding ↑/↓ with FilterKeys on still navigates (slowly, by design).
8. The IME test is done with a real Hindi (Devanagari) IME and Microsoft Pinyin, typing 3–4 characters and pressing Esc then Enter mid-composition.

---

## 13. Two calls the owner may veto (defaults are locked)

1. **Item context menu** — the only new UI surface in this doc. Default: adopt with exactly the seven existing commands. Veto means mouse users keep the keyboard for CRUD.
2. **Undo retention** — one deleted item is held in RAM until the popup hides. Default: adopt, memory-only, session-only. Veto (privacy or memory) means delete stays immediate and permanent, and we add a Ditto-style confirmation instead.

---

## 14. Sources

**Genre**
- [R1] Raycast — Keyboard Shortcuts (manual): https://manual.raycast.com/keyboard-shortcuts
- [R2] Raycast — Clipboard History (manual): https://manual.raycast.com/clipboard-history
- [R3] Alfred — Clipboard History: https://www.alfredapp.com/help/features/clipboard/
- [R4] CopyQ — Keyboard (default shortcuts): https://copyq.readthedocs.io/en/latest/keyboard.html
- [R5] CopyQ — Basic Usage: https://copyq.readthedocs.io/en/latest/basic-usage.html
- [R6] CopyQ issue #2253 "Clipboard item deletion is permanent and immediate (without confirmation)" (opened Feb 2023, still open; maintainer + contributor comments incl. the community "Undoable Move to Trash" / Ctrl+Z script): https://github.com/hluk/CopyQ/issues/2253
- [R7] Ditto wiki — home + key bindings (Tab, F3, Ctrl+Space, Enter, Alt+Enter, Shift+Enter plain text): https://github.com/sabrogden/Ditto/wiki
- [R8] Ditto discussion — multi-select with Shift+arrows or Ctrl+click, then paste all: https://sourceforge.net/p/ditto-cp/discussion/287511/thread/b5919e05/
- [R9] Maccy README (⌘C copy, ⌥↵ paste, ⌥⇧↵ paste without formatting, ⌥⌫ delete, ⌥P pin, ⌘, settings): https://github.com/p0deje/Maccy
- [R10] Microsoft Learn — PowerToys Run (Esc hides, Tab navigates results, Preferred monitor position, wait-for-slower-results selection): https://learn.microsoft.com/en-us/windows/powertoys/run

**Windows platform**
- [R11] Microsoft Support — Keyboard shortcuts in Windows (Esc dismisses; Home/End/PageUp/PageDown; Ctrl+Backspace; Win+B; Shift+F10; Ctrl+Tab/Ctrl+Shift+Tab tab switching; Ctrl+Z undo; Ctrl+L address bar; Ctrl+Shift+V caveat): https://support.microsoft.com/en-us/windows/keyboard-shortcuts-in-windows-dcc61a57-8ff0-cffe-9796-cb9706c75eec
- [R12] Microsoft Support — Using the clipboard (Win+V, per-item See more → Delete, Clear all, no undo documented): https://support.microsoft.com/en-us/windows/apps/using-the-clipboard
- [R13] Microsoft Learn — `System.Windows.Input.Key` enum (`Enter = Return = 6`; `Apps` = Menu key; `ImeProcessed` masks IME-processed keys): https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.key
- [R14] Microsoft Learn — `KeyEventArgs.ImeProcessedKey`: https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.keyeventargs.imeprocessedkey
- [R15] Microsoft Learn — `TextCompositionManager` (start / update / complete composition events): https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.textcompositionmanager
- [R16] Microsoft Support — "WPF applications might not work correctly if an IME is used" (2024 servicing note): https://support.microsoft.com/en-us/servicing/os/windows/docs/2024/10/wpf-applications-might-not-work-correctly-if-an-ime-is-used
- [R17] dotnet/wpf issue #12013 — canceled IME composition retained, keeping a closed window alive: https://github.com/dotnet/wpf/issues/12013
- [R18] wordcraft issue #5242 — "Escape during IME composition closes dialogs or discards drafts": https://github.com/storytold/wordcraft/issues/5242
- [R19] Microsoft Learn — Virtual-Key Codes (`VK_RETURN`, `VK_V`): https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes

**House**
- [H1] `docs\CHECKLIST-features.md` §1, §2, §3, §4, §5, §7, §8, §10, §13, §14
- [H2] `docs\CHECKLIST-features.md` §8 (F2 / Ctrl+S / discard matrix)
- [H3] `docs\RESEARCH-tabs-pinning.md` §2.6, §3, §4, §5.1–§5.3 (keyboard model, Esc unwind, Delete matrix)
- [H4] `UI\MainWindow.xaml.cs` lines 42, 54–71, 84, 94–136
- [H5] `UI\MainWindow.xaml` lines 4–6, 55–61
- [H6] `docs\CHECKLIST-uiux.md` §1, §5, §6
- [H7] `docs\CHECKLIST-features.md` §14 (deferred paste focus poll)
- [H8] `Core\ClipboardStore.cs` lines 83–105 (`Insert`), 168–209 (`Query`), 212–227 (`GetImage`), 228–259 (formats), 261–267 (`Delete`), 279–290 (`EnforceCap`); `Core\ClipboardItem.cs` (no `TabId` yet)

---

## 15. Noticed while reading (not keyboard decisions, for the audit trail)

- `UI\MainWindow.xaml` opens at **560×620** while the pinned visual contract says **550×520** [H6] §1 — a UI-audit item, not fixed here.
- `OnNewItem` resets the selection to the newly captured row [H4 lines 54–59]; §10.2's selection-stability row is the fix.
- Esc currently hides unconditionally [H4 line 98], i.e. behind the pinned two-step contract; §8 replaces it.
- The `Enter` branch [H4 line 103] ignores modifiers, so `Shift+Enter` already pastes **formatted** — §10.2 turns it into the plain-text alias.
- `UI\MainWindow.xaml.cs` uses `WF.Screen.FromPoint(Cursor.Position)` [H4 line 42]; the pinned active-window rule is already scheduled as an S3 change [H1] §2, not touched here.
