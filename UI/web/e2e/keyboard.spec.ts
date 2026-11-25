import { test, expect, type Page } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { pathToFileURL } from 'node:url'

/**
 * The owner's keyboard contract, tested on the built page with the mock host.
 * Zero network, zero WebView2: the page's own `?mock=1000` fixture is the data.
 */
const PAGE = pathToFileURL(fileURLToPath(new URL('../dist/index.html', import.meta.url))).href + '?mock=1000'

const activeTab = (page: Page) => page.locator('[role="tab"][aria-selected="true"] .tab-label')
const cardTitle = (page: Page) => page.locator('.card-title .t-title')
const search = (page: Page) => page.getByLabel('Search clips')
const shown = (page: Page) => page.locator('.shown-count')
const selectedRow = (page: Page) => page.locator('.row.selected')
const toast = (page: Page) => page.locator('.toast')

test.beforeEach(async ({ page }) => {
  await page.goto(PAGE)
  await page.waitForSelector('.row')
  // The window appears focused; the page lands focus on the selected row after mount.
  await expect(selectedRow(page)).toBeFocused()
})

test('arrows move the selection and focus follows', async ({ page }) => {
  await expect(page.locator('.row.selected .row-index')).toHaveText('01')
  await page.keyboard.press('ArrowDown')
  await expect(page.locator('.row.selected .row-index')).toHaveText('02')
  await expect(selectedRow(page)).toBeFocused()
  await page.keyboard.press('ArrowUp')
  await expect(page.locator('.row.selected .row-index')).toHaveText('01')
})

test('Left/Right change tab in strip order and wrap', async ({ page }) => {
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('Pinned')
  await expect(cardTitle(page)).toHaveText('Pinned clips')
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('Work')
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('Pinned')
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('All')
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('Snippets')
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('All')
  // A tab switch puts the new first row under the keyboard.
  await expect(selectedRow(page)).toBeFocused()
})

test('typing from the list searches; Escape clears the query', async ({ page }) => {
  await page.keyboard.type('stripe', { delay: 40 })
  await expect(search(page)).toHaveValue('stripe')
  await expect(search(page)).toBeFocused()
  await expect(shown(page)).not.toHaveText('1,000 shown')
  await page.keyboard.press('Escape')
  await expect(search(page)).toHaveValue('')
  await expect(shown(page)).toHaveText('1,000 shown')
})

test('digits and Enter stay paste keys, not search', async ({ page }) => {
  await selectedRow(page).focus()
  await page.keyboard.press('2')
  await expect(toast(page)).toContainText('Copied instead')
  await expect(search(page)).toHaveValue('')
  await selectedRow(page).focus()
  await page.keyboard.press('Enter')
  await expect(toast(page)).toContainText('Copied instead')
})

test('F2 opens and closes a clean editor without a prompt', async ({ page }) => {
  await page.keyboard.press('F2')
  await expect(page.locator('.editor-overlay')).toBeVisible()
  await page.keyboard.press('F2')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)
  await expect(page.getByText('Discard changes?')).toHaveCount(0)
})

test('F2 on a dirty editor asks before discarding; Escape follows the same cascade', async ({ page }) => {
  await page.keyboard.press('F2')
  await expect(page.locator('.editor-overlay .ProseMirror')).toBeFocused()
  await page.keyboard.type('X')
  await expect(page.getByText('Unsaved changes').first()).toBeVisible()
  await page.keyboard.press('F2')
  await expect(page.getByText('Discard changes?')).toBeVisible()
  await expect(page.locator('.editor-overlay')).toHaveCount(1)
  await page.keyboard.press('Escape')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)

  await page.keyboard.press('F2')
  await expect(page.locator('.editor-overlay .ProseMirror')).toBeFocused()
  await page.keyboard.type('Y')
  await page.keyboard.press('Escape')
  await expect(page.getByText('Discard changes?')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)
})

test('the Settings icon is in the top bar and works from the keyboard', async ({ page }) => {
  const button = page.locator('button[aria-label="Settings"]')
  await expect(button).toBeVisible()
  await expect(button.locator('svg')).toHaveCount(1)
  await button.focus()
  await page.keyboard.press('Enter')
  await expect(page.locator('.dialog')).toHaveCount(1)
  await page.keyboard.press('Escape')
  await expect(page.locator('.dialog')).toHaveCount(0)
  await expect(button).toBeFocused()
})

test('Enter and F2 work while the search box has focus', async ({ page }) => {
  await page.keyboard.type('stripe', { delay: 30 })
  await expect(search(page)).toHaveValue('stripe')
  await page.keyboard.press('Enter')
  await expect(toast(page)).toContainText('Copied instead')
  await page.keyboard.press('F2')
  await expect(page.locator('.editor-overlay')).toBeVisible()
  await page.keyboard.press('F2')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)
})

test('Left/Right from the tab strip itself change one tab, not two', async ({ page }) => {
  await page.locator('[role="tab"][aria-selected="true"]').focus()
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('Pinned')
  await expect(page.locator('[role="tab"][aria-selected="true"]')).toBeFocused()
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('All')
})

test('Left/Right in the search field move the caret, never the tab', async ({ page }) => {
  await page.keyboard.press('Control+k')
  await expect(search(page)).toBeFocused()
  await page.keyboard.type('ab')
  const tab = await activeTab(page).textContent()
  await page.keyboard.press('ArrowLeft')
  await expect(search(page)).toHaveValue('ab')
  await expect(activeTab(page)).toHaveText(tab || '')
  expect(await search(page).evaluate((el) => (el as HTMLInputElement).selectionStart)).toBe(1)
})

test('the page makes no network request and logs no console error', async ({ page }) => {
  const errors: string[] = []
  const http: string[] = []
  page.on('console', (message) => { if (message.type() === 'error') errors.push(message.text()) })
  page.on('pageerror', (error) => errors.push(String(error)))
  page.on('request', (request) => { if (/^https?:/.test(request.url())) http.push(request.url()) })
  await page.goto(PAGE)
  await page.waitForSelector('.row')
  await page.keyboard.type('image:', { delay: 20 })
  await page.waitForTimeout(600)
  expect(http).toEqual([])
  expect(errors).toEqual([])
})
