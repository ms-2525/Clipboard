# Step Plan: getting clips out, finding them by kind, reading text in images

**Status:** DRAFT — owner instructed "add this properly one by one" on 25/11/2026; recommendations
below are applied as defaults and can be changed at any step.

**What it is for:** today a clip can be pasted but not saved as a file, and a list of 1,000 clips can
only be searched by the words inside them. These three steps make clips findable by **what they are**
and let you **take one out as a file**, then read text that only exists inside a copied picture.

**How we build it:** one step at a time, each usable and checkable on screen. Later steps are a rough
order, not a promise — using a step usually changes the next one.

## Already available to build on

- The clipboard engine: it already holds text, HTML, RTF, file lists, images and named formats, and
  can read any of them back (`ClipboardStore.GetText/GetRich/GetImage/GetThumb`).
- `ClipTags` — the automatic tag classifier built in Phase 1 (proven-only tags, zero-false-positive gate).
- `SearchService` — the existing search over clip text.
- `ImageCache` — 1024 px image decode already used by the preview.
- The row contract in `docs/DESIGN-language.md` §3: index number, 10-line body, provenance, tags.

## The steps

| Step | What you will be able to do | How you will check it | Status |
|---|---|---|---|
| **1** | **Search by tag.** Type `image:`, `file:`, `excel:`, `code:`, `link:` in the search box and see only those clips; combine them (`excel: link:` = both) and exclude one (`-image:`). | Search `image:` and see only pictures; `excel:` shows the spreadsheet clips; `-image:` hides pictures and keeps the rest. | Not started |
| **2** | **Export a clip to a file.** Pick a clip → Save as. Text saves as `.txt`, Markdown as `.md`, a spreadsheet clip as `.csv`, an image as `.png`. The file name is offered and you can edit it before saving. | Export one text clip, one Excel clip as CSV (opens in Excel with columns intact) and one image as PNG; all three open correctly. | Not started |
| **3** | **Read text inside images.** A copied picture gets its text extracted offline, so you can read and search it without opening the app it came from. | Search a word that only appears inside a screenshot; the image clip is found and shows the text. | Not started |

Status moves: Not started → Logic Sheet approved → Built, waiting for your check → Checked by you.

## Decisions taken as defaults (change any of these)

- The tag is shown as **Spreadsheet**; typing `excel:`, `sheet:`, `xls:` or `csv:` all mean the same.
- A tag word needs the colon. A bare word still searches text, so `image` finds clips containing the
  word "image", and `image:` finds pictures. Predictable beats clever. A colon is never a tag when the
  prefix is unknown or when it is part of `://`.
- Space between words = **and**. A leading `-` = **not**. No OR in step 1.
- Export never changes the clip. A spreadsheet clip exports as CSV because the clipboard already
  carries the cells as tab-separated text — no Excel needed.
- The file name is always offered for you to confirm, never silently chosen (standing preference).
- OCR is **offline**; nothing is uploaded. English first; a second language only if Windows already
  has it installed. Handwriting and very low-resolution images are not promised.

### Step 2 detail — frozen by measurement (25/11/2026)

- CSV is written **UTF-8 with a BOM and never `sep=`**. Without the BOM Excel shows `à¤°à¤¾à¤®` for
  `राम`; with `sep=` it does so even when the BOM is present, so the two cannot both be used.
- The converter is byte-identical to Excel's own CSV payload on a real clip, and survives commas,
  quotes, embedded newlines, tabs, `₹`, `007` and `=1+1` without corrupting the file.
- **Excel still reinterprets on open**: `007` becomes `7` and `=1+1` becomes `2`. That is Excel's
  behaviour, not the export; the file itself is correct. Users need to know this.
- `.md` and `.txt` are the **same bytes**; the Save dialog offers both and no content detection runs.
- Image export is the stored PNG, unchanged. A file-list clip exports nothing (it is a list of paths,
  not a document).
- File name: first line of the clip → invalid characters become spaces → trim spaces/dots → 80
  characters → reserved Windows names guarded with `_` → fallback `Clip 2026-11-25 21-25.ext`.

### Step 3 detail — frozen by measurement (25/11/2026)

- Engine: **Windows' built-in OCR**. +25.4 MB on the exe, ~40 ms per full-HD screenshot, and clearly
  better than Tesseract on dark UIs and tables (Tesseract is 10× slower and weaker there).
- The recognised text is stored as the clip's text, so existing search finds it with **no schema
  change**. The image stays the image: plain paste still pastes the picture, never the OCR text.
- OCR runs on one background worker after capture, one image at a time (the Windows API refuses
  concurrent calls). Images wider than 10,000 px are scaled down first.
- **Amounts are not trustworthy.** Neither engine reads `₹`; Indian digit grouping is lost and some
  amount lines are dropped. OCR text is for finding a screenshot, never for a figure that matters.
- If the OCR language is not installed, the app says so once instead of returning garbage.

### Step 1 detail — where the rule lives

The grammar is `Core\ClipSearch.cs` (already built and audited): one parser, one matcher, used by the
bridge and by the old UI. `SearchService` remains the text-matching rule; there is no second parser.

## Left for later (not planned yet)

- Export all filtered clips in one go; export with formatting (HTML/RTF) as a chosen format.
- Tag filters shown as clickable chips in the tab strip.
- OCR of files (PDF/Word) that are copied as files rather than pictures.
- Editing the OCR text, or turning it into its own text clip.

## Things users should know from the start

- Tags are conservative: a clip with no confident tag shows none, and searching that tag will not
  find it. A missing tag is a small annoyance; a wrong tag makes search untrustworthy.
- Step 1 has no OR search and no saved searches.
- OCR reflects what the picture shows; it can miss small, blurred or stylised text.
