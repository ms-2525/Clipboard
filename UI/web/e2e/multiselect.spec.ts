import { test, expect, type Page } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { pathToFileURL } from 'node:url'

/**
 * Shift+Up/Down multi-select: extend a contiguous range, Delete removes it, Ctrl+Z restores it.
 * Tested on the built page with the mock host.
 */
const PAGE = pathToFileURL(fileURLToPath(new URL('../dist/index.html', import.meta.url))).href + '?mock=1000'

const selectedRows = (page: Page) => page.locator('.row.selected')
const toast = (page: Page) => page.locator('.toast')

test.beforeEach(async ({ page }) => {
  await page.goto(PAGE)
  await page.waitForSelector('.row')
  await expect(selectedRows(page).first()).toBeFocused()
})

test('Shift+Down/Up grows and shrinks the range; Delete removes it; Ctrl+Z restores it', async ({ page }) => {
  await expect(page.locator('.row.selected .row-index')).toHaveText(['01'])
  await expect(selectedRows(page)).toHaveCount(1)

  // Extend down twice: 01-02, then 01-03. The cursor (focus) follows the edge.
  await page.keyboard.press('Shift+ArrowDown')
  await expect(selectedRows(page)).toHaveCount(2)
  await expect(page.locator('.row.selected .row-index')).toHaveText(['01', '02'])
  await expect(selectedRows(page).nth(1)).toBeFocused()

  await page.keyboard.press('Shift+ArrowDown')
  await expect(selectedRows(page)).toHaveCount(3)
  await expect(selectedRows(page).nth(2)).toBeFocused()

  // Shrink back to 01-02.
  await page.keyboard.press('Shift+ArrowUp')
  await expect(selectedRows(page)).toHaveCount(2)
  await expect(page.locator('.row.selected .row-index')).toHaveText(['01', '02'])

  // Delete asks once for the whole range.
  await page.keyboard.press('Delete')
  const dialog = page.getByRole('dialog')
  await expect(dialog).toBeVisible()
  await expect(dialog).toContainText('Delete 2 clips?')
  await dialog.getByRole('button', { name: 'Delete', exact: true }).click()

  await expect(toast(page)).toContainText('Deleted 2 clips')
  await expect(selectedRows(page)).toHaveCount(1)

  // Undo brings both rows back.
  await selectedRows(page).first().focus()
  await page.keyboard.press('Control+z')
  await expect(toast(page).filter({ hasText: 'Restored 2 clips' })).toHaveCount(1)
  await expect(page.locator('.row .row-index', { hasText: '01' })).toHaveCount(1)
})

test('a plain arrow collapses the range back to one row', async ({ page }) => {
  await page.keyboard.press('Shift+ArrowDown')
  await expect(selectedRows(page)).toHaveCount(2)
  await page.keyboard.press('ArrowDown')
  await expect(selectedRows(page)).toHaveCount(1)
})
