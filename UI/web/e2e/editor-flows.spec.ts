import { test, expect } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { pathToFileURL } from 'node:url'

/**
 * Tier 2 editor flows (disjoint from Tier 1 editor-guards: empty/script/size/files-convert/bullet-task/undo).
 *
 * Fixture notes (verified against src/mock.ts + src/realClips.ts):
 * - `?mock=1000` is the flat 1,000-clip fixture; F2 opens `.editor-overlay`, Esc closes it.
 * - `?mock=real` serves the 6 owner captures in order: files(374), Excel Book1(370),
 *   OpenCode(363), IMAGE(356), ChatGPT rich(349), tooltip probe(343). The image clip's
 *   stored OCR text is EMPTY (""), so "editor shows OCR text" is seeded first via
 *   type+save (mock.saveEditor writes realBodies, which mock.reOcr then returns).
 * - Mock host ops actually supported: saveEditor (rejects empty/script html only),
 *   reOcr (throws 'Only an image clip can be re-read.' for non-images and
 *   'No text found in this image.' for empty OCR text), exportClip (throws for files),
 *   openInApp. UNCOVERED-WITH-REASON: a forced host save failure reaching the
 *   'The clip could not be saved.' error banner is unreachable through the mock —
 *   mock.saveEditor never throws and its only {saved:false} paths (missing clip /
 *   empty / script) are either unreachable from an open editor or owned by Tier 1.
 * - No flow below is skipped: all six brief items are covered (image flow adapted
 *   for the empty-OCR fixture as described above). The two save+persistence legs
 *   (table insert, heading) run on `?mock=real` because only the real-clip path
 *   round-trips saved html through getItem.
 */

const PAGE1000 = pathToFileURL(fileURLToPath(new URL('../dist/index.html', import.meta.url))).href + '?mock=1000'
const PAGEREAL = pathToFileURL(fileURLToPath(new URL('../dist/index.html', import.meta.url))).href + '?mock=real'

test('table insert: 2x2 with header row, typed cell, save + reopen persists', async ({ page }) => {
  // ?mock=real: the flat ?mock=1000 mock rebuilds getItem bodies as `<p>text</p>`
  // (dropping saved html), while the real-clip path round-trips the stored html.
  await page.goto(PAGEREAL)
  await page.waitForSelector('.row')
  // Last row is the plain-text tooltip probe (kind text, no html body).
  await page.keyboard.press('End')
  await expect(page.locator('.row.selected')).toContainText('tooltip probe')
  const ed = page.locator('.editor-overlay')
  await page.keyboard.press('F2')
  const body = ed.locator('.ProseMirror')
  await expect(body).toBeFocused()
  // Normalise to one plain paragraph so the insert starts from a known document.
  await page.keyboard.press('Control+a')
  await page.keyboard.type('plain probe')
  await ed.getByRole('button', { name: 'Insert table' }).click()
  // insertTable({rows:2, cols:2, withHeaderRow:true}): one header row + one body row.
  await expect(ed.locator('.ProseMirror table th')).toHaveCount(2)
  await expect(ed.locator('.ProseMirror table td')).toHaveCount(2)
  await ed.locator('.ProseMirror table td').first().click()
  await page.keyboard.type('CELLMARK42')
  await expect(ed.locator('.ProseMirror table')).toContainText('CELLMARK42')
  await page.keyboard.press('Control+s')
  await expect(ed.getByText('Saved', { exact: true })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(ed).toHaveCount(0)
  await page.keyboard.press('F2')
  await expect(ed.locator('.ProseMirror table')).toHaveCount(1)
  await expect(ed.locator('.ProseMirror table')).toContainText('CELLMARK42')
  // The TSV body still carries tabs, so the saved grid keeps spreadsheet-ness.
  await expect(ed.locator('.editor-foot')).toContainText('spreadsheet')
  await page.keyboard.press('Escape')
  await expect(ed).toHaveCount(0)
})

test('revert edits: dirty banner clears, Esc closes with no discard prompt', async ({ page }) => {
  await page.goto(PAGE1000)
  await page.waitForSelector('.row')
  const ed = page.locator('.editor-overlay')
  await page.keyboard.press('F2')
  await expect(ed.locator('.ProseMirror')).toBeFocused()
  await page.keyboard.type('ZZZREVERT')
  await expect(ed.locator('.editor-foot')).toContainText('Unsaved changes')
  await ed.getByRole('button', { name: 'Revert edits' }).click()
  // Revert restores the baseline: the foot flips back and the dirty banner is gone.
  await expect(ed.locator('.editor-foot')).toContainText('Saved to this clip')
  await expect(ed.getByText('Unsaved changes')).toHaveCount(0)
  await page.keyboard.press('Escape')
  await expect(ed).toHaveCount(0)
  await expect(page.getByText('Discard changes?')).toHaveCount(0)
})

test('copy with break-marks on carries no ¶/↵ glyphs, full text intact', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  await page.goto(PAGE1000)
  await page.waitForSelector('.row')
  const ed = page.locator('.editor-overlay')
  await page.keyboard.press('F2')
  const body = ed.locator('.ProseMirror')
  await expect(body).toBeFocused()
  await page.keyboard.press('Control+a')
  await page.keyboard.type('alpha')
  await page.keyboard.press('Enter')
  await page.keyboard.type('beta')
  await ed.getByRole('button', { name: 'Show line and paragraph breaks' }).click()
  await expect(ed.getByRole('button', { name: 'Show line and paragraph breaks' })).toHaveAttribute('aria-pressed', 'true')
  // The toolbar click steals focus; the copy must come from the editor selection.
  await body.click()
  await page.keyboard.press('Control+a')
  await page.keyboard.press('Control+c')
  const text = await page.evaluate(() => navigator.clipboard.readText())
  expect(text).toContain('alpha')
  expect(text).toContain('beta')
  expect(text).not.toContain('¶')
  expect(text).not.toContain('↵')
  await page.keyboard.press('Escape')
  await expect(page.getByText('Discard changes?')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(ed).toHaveCount(0)
})

test('image OCR: dirty blocks Re-read with the exact banner, revert-then-Re-read succeeds', async ({ page }) => {
  await page.goto(PAGEREAL)
  await page.waitForSelector('.row')
  // Order is files, Excel, OpenCode, IMAGE, ChatGPT-rich, tooltip: the image is 4th.
  for (let i = 0; i < 3; i++) await page.keyboard.press('ArrowDown')
  await expect(page.locator('.row.selected')).toContainText('ChatGPT')
  const ed = page.locator('.editor-overlay')
  await page.keyboard.press('F2')
  await expect(ed).toBeVisible()
  // Kind gate: this caption only renders for image clips.
  await expect(ed).toContainText('Text read from the image — edit it below. It stays searchable.')
  await expect(ed.getByRole('button', { name: 'Re-read text from image' })).toBeVisible()
  // The fixture's stored OCR text is empty, so seed it first (mock.saveEditor feeds mock.reOcr).
  const body = ed.locator('.ProseMirror')
  await expect(body).toBeFocused()
  await page.keyboard.type('ocr seed text')
  await page.keyboard.press('Control+s')
  await expect(ed.getByText('Saved', { exact: true })).toBeVisible()
  // Re-focus after the save-triggered reload settles, then gate on the dirty
  // banner: keystrokes typed into the reload window would otherwise vanish silently.
  await body.click()
  await page.keyboard.type(' unsaved tail')
  await expect(ed.locator('.editor-foot')).toContainText('Unsaved changes')
  await ed.getByRole('button', { name: 'Re-read text from image' }).click()
  await expect(ed.getByText('Re-read replaces the text below — Save or Revert your edits first.')).toBeVisible()
  // Revert clears the dirty state; the re-read then replaces the body with the stored text.
  await ed.getByRole('button', { name: 'Revert edits' }).click()
  await expect(ed.locator('.editor-foot')).toContainText('Saved to this clip')
  await ed.getByRole('button', { name: 'Re-read text from image' }).click()
  await expect(ed.getByText('Saved', { exact: true })).toBeVisible()
  await expect(body).toContainText('ocr seed text')
  await expect(body).not.toContainText('unsaved tail')
  await page.keyboard.press('Escape')
  await expect(ed).toHaveCount(0)
})

test('quote, code block and heading apply structure; heading persists after save + reopen', async ({ page }) => {
  // ?mock=real like the table test: only the real-clip path round-trips saved html.
  await page.goto(PAGEREAL)
  await page.waitForSelector('.row')
  await page.keyboard.press('End')
  await expect(page.locator('.row.selected')).toContainText('tooltip probe')
  const ed = page.locator('.editor-overlay')
  await page.keyboard.press('F2')
  const body = ed.locator('.ProseMirror')
  await expect(body).toBeFocused()
  await page.keyboard.press('Control+a')
  await page.keyboard.type('quote probe')
  // Select only the text (selectText), never Ctrl+A: the document's trailing
  // empty paragraph is outside any quote, so a full-doc toggle would wrap, not lift.
  await body.getByText('quote probe').selectText()
  await ed.getByRole('button', { name: 'Quote' }).click()
  await expect(body.locator('blockquote')).toHaveCount(1)
  await expect(body.locator('blockquote')).toContainText('quote probe')
  await body.getByText('quote probe').selectText()
  await ed.getByRole('button', { name: 'Quote' }).click()
  await expect(body.locator('blockquote')).toHaveCount(0)
  // Toolbar toggles move focus; land the caret back before the next select-all.
  await body.click()
  await page.keyboard.press('Control+a')
  await page.keyboard.type('code probe')
  await body.getByText('code probe').selectText()
  await ed.getByRole('button', { name: 'Code block' }).click()
  await expect(body.locator('pre')).toContainText('code probe')
  await body.getByText('code probe').selectText()
  await ed.getByRole('button', { name: 'Code block' }).click()
  await expect(body.locator('pre')).toHaveCount(0)
  await body.click()
  await page.keyboard.press('Control+a')
  await page.keyboard.type('heading probe')
  await body.getByText('heading probe').selectText()
  await ed.getByRole('button', { name: 'Heading 1' }).click()
  await expect(body.locator('h1')).toContainText('heading probe')
  await page.keyboard.press('Control+s')
  await expect(ed.getByText('Saved', { exact: true })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(ed).toHaveCount(0)
  // Persisted shape: the heading survives the store round-trip.
  await page.keyboard.press('F2')
  await expect(ed.locator('.ProseMirror h1')).toContainText('heading probe')
  await page.keyboard.press('Escape')
  await expect(ed).toHaveCount(0)
})

test('character count moves per keystroke; Esc closes popover first, overlay stays', async ({ page }) => {
  await page.goto(PAGE1000)
  await page.waitForSelector('.row')
  const ed = page.locator('.editor-overlay')
  await page.keyboard.press('F2')
  const body = ed.locator('.ProseMirror')
  await expect(body).toBeFocused()
  const count = ed.locator('.editor-foot .t-meta').first()
  const before = Number(((await count.textContent()) ?? '').replace(/[^0-9]/g, ''))
  await page.keyboard.type('Q')
  await expect
    .poll(async () => Number(((await count.textContent()) ?? '').replace(/[^0-9]/g, '')))
    .toBe(before + 1)
  await ed.getByRole('button', { name: 'Text colour' }).click()
  await expect(ed.locator('.popover')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(ed.locator('.popover')).toHaveCount(0)
  // The same Esc stopped at the popover: the overlay is still open.
  await expect(ed).toHaveCount(1)
  await page.keyboard.press('Escape')
  await expect(page.getByText('Discard changes?')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(ed).toHaveCount(0)
})

test('typing right after save survives the post-save refresh', async ({ page }) => {
  await page.goto(PAGE1000)
  await page.waitForSelector('.row')
  const ed = page.locator('.editor-overlay')
  await page.keyboard.press('F2')
  const body = ed.locator('.ProseMirror')
  await expect(body).toBeFocused()

  await page.keyboard.press('Control+a')
  await page.keyboard.type('savemark')
  await page.keyboard.press('Control+s')
  await expect(ed.getByText('Saved', { exact: true })).toBeVisible()
  // No settle click: the keystrokes land while the post-save reload is in flight.
  await page.keyboard.type(' tailmark')
  // Settle window for the reload effect; the tail must be neither wiped nor saved yet.
  await page.waitForTimeout(800)
  await expect(body).toContainText('savemark tailmark')
  await expect(ed.getByText('Unsaved changes').first()).toBeVisible()

  // The tail is real content: a second save persists it.
  await page.keyboard.press('Control+s')
  await expect(ed.getByText('Saved', { exact: true })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(ed).toHaveCount(0)
})
