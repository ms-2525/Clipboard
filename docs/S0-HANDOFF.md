# S0 handoff — converters (READ before S1 rows / S3 editor)

Verified 38/38 (real RichTextBox + TextRange.Load, STA harness). Build 0/0.

## API (Core\RtfText.cs, Core\HtmlToRtf.cs)
- `RtfText.ToBytes(rtf)` → Latin-1 envelope (chars >U+00FF ⇒ UTF-8); `ToUtf8Bytes(rtf)` = attempt 2; `FromBytes(bytes)` = always Latin-1.
- Editor chain: `ToBytes → Load`, on throw `ToUtf8Bytes → Load`, on throw `Text` + the VISIBLE notice bar.
- `HtmlToRtf.Convert(fragment)` — input is `HtmlFragment.Extract` output. 400 lines, single pass; b/i/u/s, colours, backgrounds, sizes, lists, blockquote/h2, tables→tab-separated (one tab per cell, NO colspan expansion), entities, `\uN` for all non-ASCII, style/script/head content never emitted, unknown tags keep text.
- Measured: 32 KB fragment → 21,291-char RTF in **2.12 ms/call**; Devanagari and emoji lossless.

## S1-CRITICAL (measured, not guessed)
1. **WPF's RTF loader puts underline, strike and BACKGROUND on a wrapper `Span` (every run is wrapped)** — `new Run(run)` cloning silently drops them. The row renderer must read the EFFECTIVE attribute through the Span chain (or clone Span+Run pairs), and the "no run carries a non-default attribute" test must read Spans too.
2. **The loader does NOT throw on plain junk text** — only on real malformation (bad `\'hh`). Never key a fallback on "junk throws"; the chain's two attempts + notice handle it.
3. **FROZEN DECISION (orchestrator): rows render FOREGROUND only** — `Background`/`\highlight` from web content is DROPPED in row rendering (a yellow highlight behind clamped-to-Fg75 text is illegible in the dark theme); foreground clamps to `Fg75` when illegible per the synthesis. The EDITOR keeps full fidelity (backgrounds included) — editing shows what you copied.
4. >2 MB fragments: **the CALLER refuses** (converter has no notice surface) — S3 editor checks `fragment.Length` before calling.
5. `\li` indent works only inside a group (ours always is); 360 twips = 24 DIP.
