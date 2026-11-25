import { test, expect } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { pathToFileURL } from 'node:url'

/**
 * The right-click row menu: Edit, Copy, Save as, Delete, Open in default app,
 * and an inline Move-to-tab section. The header keeps only tabs + counts.
 */
const PAGE = pathToFileURL(fileURLToPath(new URL('../dist/index.html', import.meta.url))).href + '?mock=1000'

test.beforeEach(async ({ page }) => {
  await page.goto(PAGE)
  await page.waitForSelector('.row')
})

test('the header has no Edit/Save-as buttons; the menu holds every action', async ({ page }) => {
  await expect(page.locator('.card-head')).not.toContainText('Edit clip')
  await expect(page.locator('.card-head')).not.toContainText('Save as')
  await page.locator('.row.selected').click({ button: 'right' })
  await expect(page.locator('.ctx-menu')).toBeVisible()
  for (const name of ['Edit', 'Copy', 'Save as', 'Delete', 'Open in default app', 'Move to tab']) {
    // Hint keycaps ("F2", "Ctrl E") join the accessible name, so this is a contains match.
    await expect(page.getByRole('menuitem', { name })).toHaveCount(1)
  }
  // Delete reads red; every other row reads the normal ink.
  await expect(page.locator('.ctx-item.danger')).toHaveCSS('color', 'rgb(180, 35, 24)')
})

test('Move to tab expands inline and ticks the current tab', async ({ page }) => {
  await page.locator('.row.selected').click({ button: 'right' })
  await expect(page.locator('.ctx-dests')).toHaveCount(0)
  await page.locator('.ctx-item[data-move="toggle"]').click()
  const dests = page.locator('.ctx-dests')
  await expect(dests).toBeVisible()
  // Inline: nothing overflows the menu box, so no scrollbar can appear.
  expect(await page.locator('.ctx-menu').evaluate((el) => el.scrollWidth <= el.clientWidth + 1)).toBe(true)
  await expect(dests.locator('.ctx-item[aria-checked="true"]')).toHaveCount(1)
  // Keyboard path: focus the toggle, Right opens and lands inside the section,
  // Left steps back out, Esc closes the menu.
  await page.locator('.ctx-item[data-move="toggle"]').focus()
  await page.keyboard.press('ArrowRight')
  expect(await page.evaluate(() => document.activeElement?.getAttribute('role'))).toBe('menuitemradio')
  await page.keyboard.press('ArrowLeft')
  await expect(dests).toHaveCount(0)
  await page.keyboard.press('Escape')
  await expect(page.locator('.ctx-menu')).toHaveCount(0)
})

test('Ctrl+E still saves with no header button', async ({ page }) => {
  await page.locator('.row.selected').focus()
  await page.keyboard.press('Control+e')
  await expect(page.locator('.toast').first()).toContainText('Saved')
})
