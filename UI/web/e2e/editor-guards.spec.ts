/**
 * Tier 1 regression guards for the rich-text editor (EditorOverlay + toolbar).
 *
 * Runs on the built page with the mock host (`?mock=1000`, flat 1000-row fixture;
 * row 04 is the files-kind row). Reuses keyboard.spec.ts patterns throughout:
 * F2 opens the `.editor-overlay` (`.ProseMirror` is the editable), Esc follows the
 * discard cascade, `toHaveCount` and exact-text asserts pin the wired behavior.
 *
 * Coverage of the six requested items (no skips):
 *  1. empty-save refusal -> "empty editor refuses to save ..."
 *  2. oversized frontend 2 MB guard (MAX_HTML) -> "oversized HTML trips ..."
 *  3. script-paste sanitization (schema strips, save succeeds) -> "pasted script ..."
 *  4. files-kind convert gate -> "files clips ask ..."
 *  5. bullet + task list structure (one concept each, so two tests) -> "bullet lists ..."
 *     and "task lists ..."; each toggles via its toolbar button, asserts structure,
 *     saves, and asserts the structure still round-trips through reopen (the mock
 *     persists saved HTML like the host store, so markup survives getItem).
 *  6. undo/redo -> "undo removes typed text ..." plus "clean undo keeps the
 *     loaded body ...": the programmatic load is rebuilt outside history, so a
 *     clean Ctrl+Z is a no-op (redo still restores typed-then-undone text).
 *
 * Every test has at least one assertion that fails if the wired behavior were
 * removed (exact error strings from EditorOverlay.tsx, structural selectors,
 * reopen-and-verify). Negative controls (key assertion inverted -> red run ->
 * revert) were performed per test; see the task report.
 */
import { test, expect } from '@playwright/test'
import { fileURLToPath, pathToFileURL } from 'node:url'

const PAGE = pathToFileURL(fileURLToPath(new URL('../dist/index.html', import.meta.url))).href + '?mock=1000'

test.beforeEach(async ({ page }) => {
  await page.goto(PAGE)
  await page.waitForSelector('.row')
  await expect(page.locator('.row.selected')).toBeFocused()
})

test('empty editor refuses to save with the exact banner and keeps the stored body', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  const body = overlay.locator('.ProseMirror')
  const before = ((await page.locator('.row.selected .row-body').textContent()) ?? '').trim()
  // The fixture row must carry text: otherwise "unchanged" below would prove nothing.
  expect(before.length).toBeGreaterThan(0)

  await page.keyboard.press('F2')
  await expect(overlay).toBeVisible()
  await expect(body).toBeFocused()
  await expect(body).not.toHaveText('')

  await page.keyboard.press('Control+a')
  await page.keyboard.press('Backspace')
  await expect(body).toHaveText('')

  await page.keyboard.press('Control+s')
  // Exact frontend refusal string (EditorOverlay.tsx); the overlay must stay open.
  await expect(overlay.getByRole('alert')).toHaveText('The editor has no text, so nothing was saved.')
  await expect(overlay).toHaveCount(1)
  await expect(overlay.getByText('Saved', { exact: true })).toHaveCount(0)

  // Esc -> discard prompt -> Esc closes without writing; the stored body is untouched.
  await page.keyboard.press('Escape')
  await expect(page.getByText('Discard changes?')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
  await expect(page.locator('.row.selected .row-body')).toHaveText(before)
})

test('oversized HTML trips the frontend 2 MB guard before the host', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  const body = overlay.locator('.ProseMirror')
  const before = ((await page.locator('.row.selected .row-body').textContent()) ?? '').trim()
  expect(before.length).toBeGreaterThan(0)

  await page.keyboard.press('F2')
  await expect(overlay).toBeVisible()
  await expect(body).toBeFocused()
  await expect(body).not.toHaveText('')

  // One fill (NOT keystroke typing): the frontend must refuse before onSave runs.
  // The mock host enforces no cap, so only the MAX_HTML guard can produce this banner.
  const big = 'a'.repeat(2_000_100)
  await body.fill(big)
  const held = await body.evaluate((el: HTMLElement) => (el.textContent ?? '').length)
  expect(held).toBeGreaterThan(2_000_000)

  await page.keyboard.press('Control+s')
  await expect(overlay.getByRole('alert')).toHaveText('The clip is over 2 MB of HTML, so it was not saved.')
  await expect(overlay).toHaveCount(1)
  await expect(overlay.getByText('Saved', { exact: true })).toHaveCount(0)

  await page.keyboard.press('Escape')
  await expect(page.getByText('Discard changes?')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
  await expect(page.locator('.row.selected .row-body')).toHaveText(before)
})

test('pasted script markup is stripped by the schema and the save succeeds', async ({ page }) => {
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write'])
  const overlay = page.locator('.editor-overlay')
  const body = overlay.locator('.ProseMirror')

  await page.keyboard.press('F2')
  await expect(overlay).toBeVisible()
  await expect(body).toBeFocused()
  await expect(body).not.toHaveText('')

  await page.keyboard.press('Control+a')
  await page.evaluate(async () => {
    const html = '<script>alert(1)</script><p>pasted-ok-marker</p>'
    await navigator.clipboard.write([
      new ClipboardItem({
        'text/html': new Blob([html], { type: 'text/html' }),
        'text/plain': new Blob(['pasted-ok-marker'], { type: 'text/plain' }),
      }),
    ])
  })
  await page.keyboard.press('Control+v')
  // The benign marker proves the paste landed; without it "no script" would pass vacuously.
  await expect(body).toContainText('pasted-ok-marker')

  await page.keyboard.press('Control+s')
  // The refusal path is unreachable through the schema, so the save must succeed.
  await expect(overlay.getByText('Saved', { exact: true })).toBeVisible()
  await expect(overlay.getByRole('alert')).toHaveCount(0)

  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
  await page.keyboard.press('F2')
  const reopened = page.locator('.editor-overlay .ProseMirror')
  await expect(reopened).toContainText('pasted-ok-marker')
  const html = (await reopened.innerHTML()).toLowerCase()
  expect(html).not.toContain('<script')
  await page.keyboard.press('Escape')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)
})

test('files clips ask before converting to text and flip kind on confirm', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  const body = overlay.locator('.ProseMirror')

  await page.keyboard.press('Home')
  for (let i = 0; i < 3; i++) await page.keyboard.press('ArrowDown')
  await expect(page.locator('.row.selected .row-index')).toHaveText('04')

  await page.keyboard.press('F2')
  await expect(overlay).toBeVisible()
  await expect(overlay.locator('.editor-chip')).toHaveText('Files')
  await expect(body).toBeFocused()

  await page.keyboard.type('X')
  await expect(overlay.getByText('Unsaved changes').first()).toBeVisible()

  await page.keyboard.press('Control+s')
  await expect(overlay.getByText('Save as text?')).toBeVisible()
  await expect(overlay.getByText('Saved', { exact: true })).toHaveCount(0)

  await overlay.getByRole('button', { name: 'Save as text' }).click()
  await expect(overlay.getByText('Saved', { exact: true })).toBeVisible()
  await expect(page.locator('.toast')).toContainText('Clip saved')
  await expect(overlay.locator('.editor-chip')).toHaveText('Text')

  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
  // Kind flipped in the list: no longer a file row.
  await expect(page.locator('.row.selected .row-files')).toHaveCount(0)
})

test('bullet lists keep their structure through save and reopen', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  const body = overlay.locator('.ProseMirror')

  await page.keyboard.press('F2')
  await expect(body).toBeFocused()
  await expect(body).not.toHaveText('')

  await page.keyboard.press('Control+a')
  await page.keyboard.type('bullet-one')
  await overlay.getByRole('button', { name: 'Bullet list' }).click()
  // Structural asserts: a real <ul><li> carrying the typed text.
  await expect(overlay.locator('.ProseMirror ul')).toHaveCount(1)
  await expect(overlay.locator('.ProseMirror ul li').first()).toContainText('bullet-one')

  await page.keyboard.press('Control+s')
  await expect(overlay.getByText('Saved', { exact: true })).toBeVisible()
  // The save must not mangle the list while the editor is still open.
  await expect(overlay.locator('.ProseMirror ul li').first()).toContainText('bullet-one')

  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
  await page.keyboard.press('F2')
  const reopened = page.locator('.editor-overlay .ProseMirror')
  // The mock persists saved HTML like the host store, so the structure round-trips.
  await expect(reopened.locator('ul li').first()).toContainText('bullet-one')
  await page.keyboard.press('Escape')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)
})

test('task lists keep their structure through save and reopen', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  const body = overlay.locator('.ProseMirror')

  await page.keyboard.press('F2')
  await expect(body).toBeFocused()
  await expect(body).not.toHaveText('')

  await page.keyboard.press('Control+a')
  await page.keyboard.type('task-one')
  await overlay.getByRole('button', { name: 'Task list' }).click()
  // Structural asserts: TipTap task markup (li carries data-checked, not data-type) with the typed text.
  await expect(overlay.locator('.ProseMirror ul[data-type="taskList"]')).toHaveCount(1)
  await expect(overlay.locator('.ProseMirror ul[data-type="taskList"] li').first()).toContainText('task-one')

  await page.keyboard.press('Control+s')
  await expect(overlay.getByText('Saved', { exact: true })).toBeVisible()
  await expect(overlay.locator('.ProseMirror ul[data-type="taskList"] li').first()).toContainText('task-one')

  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
  await page.keyboard.press('F2')
  const reopened = page.locator('.editor-overlay .ProseMirror')
  // Same as the bullet test: the structure itself must survive the reopen.
  await expect(reopened.locator('ul[data-type="taskList"] li').first()).toContainText('task-one')
  await page.keyboard.press('Escape')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)
})

test('undo removes typed text and redo restores it', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  const body = overlay.locator('.ProseMirror')

  await page.keyboard.press('F2')
  await expect(body).toBeFocused()
  await expect(body).not.toHaveText('')
  const original = (await body.textContent()) ?? ''
  expect(original.trim().length).toBeGreaterThan(0)

  await page.keyboard.press('Control+a')
  await page.keyboard.type('undoqx-1')
  // Exact replace: the doc is exactly the token, so the token contract below is exact too.
  await expect(body).toHaveText('undoqx-1')

  await page.keyboard.press('Control+z')
  await expect(body).not.toContainText('undoqx-1')

  await page.keyboard.press('Control+Shift+z')
  await expect(body).toHaveText('undoqx-1')

  // Leave nothing behind: discard the redo, the stored clip is untouched.
  await page.keyboard.press('Escape')
  await expect(page.getByText('Discard changes?')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
})

test('clean undo keeps the loaded body: the programmatic load is not in history', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  const body = overlay.locator('.ProseMirror')

  await page.keyboard.press('F2')
  await expect(body).toBeFocused()
  const loaded = (await body.textContent()) ?? ''
  expect(loaded.trim().length).toBeGreaterThan(0)

  // No edits yet: undo must be a no-op, not a wipe back to the pre-load empty doc.
  await page.keyboard.press('Control+z')
  await expect(body).toHaveText(loaded)

  // Still clean: Esc closes with no discard prompt.
  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
})
