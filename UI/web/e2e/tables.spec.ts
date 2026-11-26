import { test, expect } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { pathToFileURL } from 'node:url'

/**
 * Spreadsheet round-trip on real Excel bytes: cells stay cells across an edit + save.
 * `?mock=real` serves the owner's captures, including the Excel grid (TSV + CF_HTML table).
 */
const PAGE = pathToFileURL(fileURLToPath(new URL('../dist/index.html', import.meta.url))).href + '?mock=real'

test.beforeEach(async ({ page }) => {
  await page.goto(PAGE)
  await page.waitForSelector('.row')
})

test('an Excel clip opens as a table and saves cells without collapsing the grid', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  // The Excel capture is the second clip (Book1 - Excel).
  await page.keyboard.press('ArrowDown')
  await expect(page.locator('.row.selected')).toContainText('Book1')
  await page.keyboard.press('F2')
  await expect(overlay).toBeVisible()
  const cells = overlay.locator('.ProseMirror table td')
  await expect(cells).toHaveCount(6)
  await expect(overlay.locator('.editor-foot')).toContainText('spreadsheet')
  // Edit the first cell in place (append so the caret never leaves the cell).
  await cells.first().click()
  await page.keyboard.type('Q1')
  await page.keyboard.press('ControlOrMeta+s')
  await expect(overlay.getByText('Saved', { exact: true })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
  // Reopen: still a 2x3 grid, carrying the edit.
  await page.keyboard.press('F2')
  await expect(overlay.locator('.ProseMirror table td')).toHaveCount(6)
  await expect(overlay.locator('.ProseMirror table')).toContainText('aQ1')
  // The mock keeps the spreadsheet tag only when the saved text is still TSV:
  // this proves the columns survived as tabs, not just as pixels.
  await expect(overlay.locator('.editor-foot')).toContainText('spreadsheet')
  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
})
