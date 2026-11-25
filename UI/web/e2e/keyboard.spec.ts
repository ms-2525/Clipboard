import { test, expect, type Page } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { pathToFileURL } from 'node:url'

/**
 * The owner's keyboard contract, tested on the built page with the mock host.
 * Zero network, zero WebView2: the page's own `?mock=1000` fixture is the data.
 */
const PAGE = pathToFileURL(fileURLToPath(new URL('../dist/index.html', import.meta.url))).href + '?mock=1000'

const activeTab = (page: Page) => page.locator('[role="tab"][aria-selected="true"] .tab-label')
const footerScope = (page: Page) => page.locator('.footer-left .t-meta')
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

test('arrows move the selection and focus follows, stopping at both ends', async ({ page }) => {
  await expect(page.locator('.row.selected .row-index')).toHaveText('01')
  await page.keyboard.press('ArrowDown')
  await expect(page.locator('.row.selected .row-index')).toHaveText('02')
  await expect(selectedRow(page)).toBeFocused()
  await page.keyboard.press('ArrowUp')
  await expect(page.locator('.row.selected .row-index')).toHaveText('01')
  // No wrap: up from the first row stays on the first, down from the last stays on the last.
  await page.keyboard.press('ArrowUp')
  await expect(page.locator('.row.selected .row-index')).toHaveText('01')
  await expect(selectedRow(page)).toBeFocused()
  await page.keyboard.press('End')
  await expect(page.locator('.row.selected .row-index')).toHaveText('1000')
  await page.keyboard.press('ArrowDown')
  await expect(page.locator('.row.selected .row-index')).toHaveText('1000')
  await expect(selectedRow(page)).toBeFocused()
})

test('Left/Right change tab in strip order and wrap', async ({ page }) => {
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('Pinned')
  await expect(footerScope(page)).toHaveText('Pinned history')
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('Images')
  await expect(footerScope(page)).toHaveText('Image results')
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('Work')
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('Images')
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

test('the Images tab is a saved image: view and matches the tag search', async ({ page }) => {
  await page.keyboard.type('image:', { delay: 20 })
  await expect(search(page)).toHaveValue('image:')
  await expect(shown(page)).not.toHaveText('1,000 shown')
  const expected = (await shown(page).textContent())?.trim() || ''
  await page.keyboard.press('Escape')
  await expect(shown(page)).toHaveText('1,000 shown')

  await page.getByRole('tab', { name: /Images/ }).click()
  await expect(activeTab(page)).toHaveText('Images')
  await expect(footerScope(page)).toHaveText('Image results')
  await expect(shown(page)).toHaveText(expected)

  // `tag:` is the explicit prefix for the same filter.
  await page.getByLabel('Search clips').fill('')
  await page.keyboard.type('tag:image', { delay: 20 })
  await expect(shown(page)).toHaveText(expected)

  // An unknown tag under the prefix warns and searches as text.
  await page.getByLabel('Search clips').fill('tag:zzz')
  await expect(page.locator('.tag-hint')).toContainText('no tag named')
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

test('Ctrl+1..Ctrl+9 paste the visible row; a bare digit is search text', async ({ page }) => {
  // A plain digit starts a search, so numbers like a year or an amount can be typed directly.
  await page.keyboard.type('2', { delay: 40 })
  await expect(search(page)).toHaveValue('2')
  await expect(search(page)).toBeFocused()
  await page.keyboard.press('Escape')
  await page.waitForTimeout(300)

  // Ctrl+2 pastes the second visible row and does not touch the query.
  await selectedRow(page).focus()
  await page.keyboard.press('Control+2')
  await expect(toast(page)).toContainText('Copied instead')
  await expect(search(page)).toHaveValue('')

  // Enter still pastes the selected row.
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

test('Discard and close never writes the edit', async ({ page }) => {
  const before = (await page.locator('.row.selected .row-body').textContent())?.trim() ?? ''
  await page.keyboard.press('F2')
  await expect(page.locator('.editor-overlay .ProseMirror')).toBeFocused()
  await page.keyboard.type('X')
  await page.keyboard.press('F2')
  await expect(page.getByText('Discard changes?')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)
  // The stored body is untouched: the discard path must not reach the store.
  await expect(page.locator('.row.selected .row-body')).toHaveText(before)
})

test('the All count matches the list instead of a seeded zero', async ({ page }) => {
  const all = page.locator('[role="tab"][aria-selected="true"]')
  await expect(all.locator('.tab-label')).toHaveText('All')
  await expect(all.locator('.tab-count')).toHaveText('1,000')
  await expect(shown(page)).toHaveText('1,000 shown')
})

test('the Settings preview and the known-limits note are on screen', async ({ page }) => {
  await page.locator('button[aria-label="Settings"]').click()
  await expect(page.locator('.dialog')).toHaveCount(1)
  await expect(page.getByText('Known limits')).toBeVisible()
  // G-03: at 900x700 the sheet must fit without a scroll; the limits note once pushed it over.
  const overflow = await page.evaluate(() => {
    const body = document.querySelector('.dialog-body') as HTMLElement
    return body.scrollHeight - body.clientHeight
  })
  expect(overflow).toBeLessThanOrEqual(0)
  await page.getByRole('button', { name: 'Preview' }).click()
  // The mock answers `shown`; a failure would surface as an error toast.
  await expect(page.locator('.toast.error')).toHaveCount(0)
  await page.keyboard.press('Escape')
  await expect(page.locator('.dialog')).toHaveCount(0)
})

test('the image editor offers Open in app and Export; the files editor offers neither', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  await page.keyboard.press('End')
  await expect(page.locator('.row.selected .row-index')).toHaveText('1000')
  await page.keyboard.press('F2')
  await expect(overlay).toBeVisible()
  await expect(overlay.getByRole('button', { name: 'Open in app' })).toBeVisible()
  await expect(overlay.getByRole('button', { name: 'Export' })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)

  await page.keyboard.press('Home')
  for (let i = 0; i < 3; i++) await page.keyboard.press('ArrowDown')
  await expect(page.locator('.row.selected .row-index')).toHaveText('04')
  await page.keyboard.press('F2')
  await expect(overlay).toBeVisible()
  await expect(overlay.getByRole('button', { name: 'Open in app' })).toHaveCount(0)
  await expect(overlay.getByRole('button', { name: 'Export' })).toHaveCount(0)
  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
})

test('Ctrl+S saves without closing and flashes a green Saved bar', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  await page.keyboard.press('F2')
  await expect(overlay.locator('.ProseMirror')).toBeFocused()
  await page.keyboard.type('X')
  await expect(overlay.getByText('Unsaved changes').first()).toBeVisible()
  await page.keyboard.press('Control+s')
  await expect(overlay.getByText('Saved', { exact: true })).toBeVisible()
  // Still open: Ctrl+S persists but never closes.
  await expect(overlay).toHaveCount(1)
  await page.keyboard.press('Escape')
  await expect(overlay).toHaveCount(0)
})

test('Esc in the editor returns to the list: same row, same scroll, window stays open', async ({ page }) => {
  await page.locator('.row.selected').focus()
  await page.keyboard.press('End')
  await expect(page.locator('.row.selected .row-index')).toHaveText('1000')
  for (let i = 0; i < 10; i++) await page.keyboard.press('ArrowUp')
  const beforeId = await page.locator('.row.selected').getAttribute('data-id')
  const beforeScroll = await page.locator('.list').evaluate((el) => el.scrollTop)

  await page.keyboard.press('F2')
  await expect(page.locator('.editor-overlay')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)

  // The same keystroke must not reach the window matrix: the app stays open.
  expect(await page.evaluate(() => (window as unknown as { __mockHideCalls?: number }).__mockHideCalls ?? -1)).toBe(0)
  await expect(page.locator('.row.selected')).toHaveAttribute('data-id', beforeId ?? '')
  expect(await page.locator('.list').evaluate((el) => el.scrollTop)).toBe(beforeScroll)
  await expect(selectedRow(page)).toBeFocused()
})

test('Esc on a dirty editor discards without hiding the window', async ({ page }) => {
  await page.keyboard.press('F2')
  await expect(page.locator('.editor-overlay .ProseMirror')).toBeFocused()
  await page.keyboard.type('X')
  await page.keyboard.press('Escape')
  await expect(page.getByText('Discard changes?')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(page.locator('.editor-overlay')).toHaveCount(0)
  expect(await page.evaluate(() => (window as unknown as { __mockHideCalls?: number }).__mockHideCalls ?? -1)).toBe(0)
  await expect(selectedRow(page)).toBeFocused()
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
  expect(await page.evaluate(() => (window as unknown as { __mockHideCalls?: number }).__mockHideCalls ?? -1)).toBe(0)
  await expect(button).toBeFocused()
})

test('both arrows move the list from the search box', async ({ page }) => {
  await search(page).focus()
  await page.keyboard.type('stripe', { delay: 30 })
  await expect(search(page)).toHaveValue('stripe')
  const first = (await page.locator('.row.selected .row-index').textContent()) ?? ''
  await page.keyboard.press('ArrowDown')
  const second = (await page.locator('.row.selected .row-index').textContent()) ?? ''
  expect(second).not.toBe(first)
  await page.keyboard.press('ArrowUp')
  await expect(page.locator('.row.selected .row-index')).toHaveText(first)
  // The caret stays in the box while the arrows move the list.
  await expect(search(page)).toBeFocused()
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
