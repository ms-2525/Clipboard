# Core polish — handoff for the integration sprint (READ before wiring paste/editor/undo)

Verified 127/127 + mutation checks. The assumed surface in S3's brief exists with these exact semantics:

1. **`GetRich(long id)`** returns the one row WITH `Html` and `Rtf`. `GetRecent`/`GetTabItems` NO LONGER carry them — the rich preview and the F2 editor must load via `GetRich(id)` first.
2. **`ClipboardItem.TabId`** (long?) populated on every query (main rows null, tab rows = tab id).
3. **`Update(ClipboardItem)` DROPS rich bodies + formats by design** — it writes the text it is given. F2 editor on a RICH item must build the new `Rtf` from its RichTextBox (`TextRange.Save` → `DataFormats.Rtf`) plus a plain-text extraction for `Text`, set both on the item, THEN call `Update` — or the save silently strips formatting. Plain items: TextBox text alone is correct.
4. **`Restore(ClipboardItem)`** (undo primitive): explicit id + tab_id + timestamp + formats in one transaction, position preserved, no cap re-apply. Ctrl+Z keeps a one-level in-memory copy of the deleted item and calls this.
5. **`store.RecoveredFromCorruption`** (bool) — check after startup, toast if true.
6. **`CaptureService.Skipped`** (Action<string>) → reused toast with the reason string.
7. `InsertWithFormats`/dedupe are transactional; `image_hash` (SHA-256) is internal to dedupe — nothing to consume.
8. Timestamps are millisecond-resolution `yyyy-MM-ddTHH:mm:ss.fff` now; display code parses with `DateTime.TryParse` (handles old + new formats); the footer still formats via `ClipboardItem.ClockFormat`.
9. `ClipboardStore` is now three partial-class files (`ClipboardStore.cs` / `ClipboardStore.Items.cs` / `ClipboardStore.Tabs.cs`) — consume only, do not edit.
