# DESIGN-v2 — OWNER DIRECTIVES (frozen 23 Nov ~18:00; these override every earlier design ruling where they conflict)

Source: the owner's direct message after seeing S3. Their word is law; where an earlier doc
(docs\DESIGN-layout.md row anatomy, the 65px meta-always density ruling, grow-on-selection)
contradicts this file, THIS file wins.

## 1. NO geometry change on selection — kill the scroll jumping
The owner: "things are jumping when I scroll through the clipboard items using up/down arrow keys."
- Rows get a **content-adaptive height with a MAX cap** ("max height and then auto adapt based on the height") —
  a 1-line clip is short, a 20-line clip is taller up to the cap; the height depends ONLY on content.
- **Selection NEVER changes a row's height or the list's geometry.** The grow-on-selection animation
  (65 → 101/119px) is DELETED. ↑/↓ must move the highlight with zero layout jump.
- Scroll stability contract: scroll-into-view uses the MINIMAL delta (never a page jump), no animated scrolls.

## 2. Provenance lives in the SEPARATE BOTTOM BAR (owner, 23 Nov rev 3 — supersedes the per-row status line)
The owner: "the initial design of having a separate bottom bar was good and show the app title as well as
the app name." The bottom bar carries, for the SELECTED clip: **app name · window title · date/time**
(`AppName · WindowTitle · 23 Nov 15:16`, ClockFormat-aware, updates on every selection/tab change) on its
left, clip count + key hints on the right. Rows stay content-only — NO per-row status/meta line (rev 2's
"status below each item" is superseded by this).

## 2b. Preview lines (unchanged from rev 2)
Multi-line items show their TOP `PreviewLines` lines (setting default **10**, honored DIRECTLY — the
hardcoded clamp of 6 is deleted); row height = `16 + lines×18`, fewer lines size to fit.

## 2c. ORTU IS THE HEAVY UI/UX REFERENCE (owner, 23 Nov)
"take very heavy reference from https://github.com/abhijith-p-subash/ortu" — the repo is cloned locally
at `Ortu Reference\` (excluded from the build and git). Every UI sprint must READ ITS DESIGN SYSTEM FROM
THE CODE (frontend under `src\`, styles/tokens in its CSS/theme files) and match its craft level:
modern UI, margins proper but NOT big, end-polish with zero AI slop — the difference between
"each metric green" and "looks like a real product".

## 3. Sequential numbers replace the icon tile (Clipboard tab)
Each row's top-left 22px icon tile is replaced by **1, 2, 3, …** — the number IS the Ctrl+1..9 paste-nth
affordance. Continue past 9 (10, 11, …). Other tabs pick a sensible slot treatment (design agent decides).

## 4. Rich text shows in the MAIN popup itself
The selected row renders its actual formatted content (colors, sizes, tables — the HTML→RTF→FlowDocument
row renderer) **inside its fixed content height** — a content swap, never a geometry change.
Unselected rows keep the plain clamped preview.

## 5. F2 editor = ONE proper rich text editor for EVERYTHING (WYSIWYG)
The owner: "every clip should open in rich text editor.. one editor for everything!!!"
- **A SINGLE editor surface, always rich-capable.** Plain text clips open in the SAME rich editor —
  no separate plain-TextBox mode, no silent plain fallback, ever. A plain clip simply carries no
  formatting yet. Image clips keep the F2-opens-default-viewer rule (that is not an editor).
Verified root causes (orchestrator, EditorWindow.xaml.cs) — fix BOTH:
- Line 27 `_rich = !string.IsNullOrEmpty(rtf)`: HTML-only rich clips (browser copies) carry NO RTF →
  silently open the plain TextBox.
- Line 33 `Encoding.UTF8.GetBytes(rtf)`: corrupts 8-bit RTF → load throws → the catch silently degrades
  to plain. Any fix must treat RTF as bytes, and any fallback must be visible, never silent.
The editor must SHOW the item's real formatting and edit it WYSIWYG. Library allowed (owner's explicit
word). Design agent researches + decides the approach (e.g. native RichTextBox fed RTF with an HTML→RTF
conversion for html-only clips, vs a WebView2 contenteditable HTML editor using the preinstalled Edge
runtime) and freezes the round-trip save contract. [Resolved: native RichTextBox + Core\HtmlToRtf — S0 landed.]
- **Rich text toolbar in the editor** (owner directive 23 Nov): the editor carries a formatting toolbar
  that operates on the selection — bold, italic, underline, strikethrough, font size, text colour,
  highlight, and clear-formatting. WordPad-class, minimal buttons, theme-token styling, toggling state
  reflects the caret's current formatting. No image-insert, no table editor, no alignment wall — smallest
  complete toolbar. Saves through the SAME contract (RTF from the document via TextRange.Save;
  html regenerated only if the clip had html).

## 6. Images tab = BIG previews filling the popup
The owner: "the image should show in a bigger preview… cover as much screen inside the max height as
possible… how will I see the images when I have say 20 images? I will have to open each one!!!!!"
- Large tiles / a dominant preview layout that FILLS the popup viewport — images browsable at a glance.
- 96px stored thumbs are too small for this; larger previews decode from the cached full image on demand,
  off the UI thread (pairs with the tracked off-UI-thread decode item).
- Never require opening items one at a time to see what an image is.

## 7. Text crisp and sharp EVERYWHERE
The owner: "text is also not crisp and sharp at many places." Root cause is already pixel-proven
(docs\AUDIT-current-visual.md): `AllowsTransparency="True"` forces greyscale AA (0 color-fringed pixels
in the popup vs 1,026 in a ClearType control). Fix = restructure windows WITHOUT AllowsTransparency so
ClearType returns (Win11 DWM corner-rounding for the rounded look, DWM shadow), uniform
`TextFormattingMode="Display"`, `UseLayoutRounding`. The notification-preview ghost may keep transparency
(it is a decorative overlay). Pixel-verify fringing returns after the change.

## 8. Row separation — CopyQ Forest alternate coloring + divider (owner 23 Nov)
- Owner: "Show some divider between the items… check the forest theme of copyq i liked the alternate row coloring."
- Rows alternate two theme-tinted surfaces (even/odd) + a 1 DIP `Divider`-token hairline between rows.
  Brush-only (no geometry change), selection/hover/keyboard-current states paint OVER the zebra without
  moving anything. Both dark + light themes. Exact Forest hexes/alpha to be extracted by the research
  agent into theme tokens (no hardcoded colors outside Themes\*.xaml).

## 9. Process
The owner is NOT satisfied with the UI/UX polish; "have proper thought through UI/UX", "deploy as many
sub-agents as required." So: a v2 design pass rethinks the affected surfaces end-to-end BEFORE code,
then implementation sprints with disjoint file ownership, then a fresh-eyes audit against THIS file.
