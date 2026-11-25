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
  await expect(activeTab(page)).toHaveText('Images')
  await expect(footerScope(page)).toHaveText('Image results')
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('Pinned')
  await expect(footerScope(page)).toHaveText('Pinned history')
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('Work')
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('Pinned')
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('Images')
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('Inbox')
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('Snippets')
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).toHaveText('Inbox')
  // A tab switch puts the new first row under the keyboard.
  await expect(selectedRow(page)).toBeFocused()
})

test('Ctrl+Tab cycles tabs forward and Ctrl+Shift+Tab cycles back', async ({ page }) => {
  await expect(activeTab(page)).toHaveText('Inbox')
  await page.keyboard.press('Control+Tab')
  await expect(activeTab(page)).toHaveText('Images')
  await page.keyboard.press('Control+Tab')
  await expect(activeTab(page)).toHaveText('Pinned')
  await page.keyboard.press('Control+Shift+Tab')
  await expect(activeTab(page)).toHaveText('Images')
  // From the search box with text, plain arrows own the caret but Ctrl+Tab still cycles,
  // and the typed query survives onto a normal tab.
  // (Entering Images always replaces the query with its saved `tag:image` view.)
  await page.getByRole('tab', { name: /Pinned/ }).click()
  await expect(activeTab(page)).toHaveText('Pinned')
  await search(page).fill('stripe')
  await page.keyboard.press('Control+Tab')
  await expect(activeTab(page)).toHaveText('Work')
  await expect(search(page)).toHaveValue('stripe')
})

test('the Images tab is a saved tag:image view and matches the tag search', async ({ page }) => {
  await page.keyboard.type('tag:image', { delay: 20 })
  await expect(search(page)).toHaveValue('tag:image')
  await expect(shown(page)).not.toHaveText('1,000 shown')
  const expected = (await shown(page).textContent())?.trim() || ''
  await page.keyboard.press('Escape')
  await expect(shown(page)).toHaveText('1,000 shown')

  await page.getByRole('tab', { name: /Images/ }).click()
  await expect(activeTab(page)).toHaveText('Images')
  await expect(footerScope(page)).toHaveText('Image results')
  await expect(shown(page)).toHaveText(expected)

  // A bare `image:` is plain text now, never a filter.
  await page.getByLabel('Search clips').fill('image:')
  await expect(page.locator('.tag-hint')).toHaveCount(0)

  // An unknown tag under the prefix warns and searches as text.
  await page.getByLabel('Search clips').fill('tag:zzz')
  await expect(page.locator('.tag-hint')).toContainText('no tag named')
})

test('a storage notice asks once per run and never blocks saving', async ({ page }) => {
  const dispatch = () =>
    page.evaluate(() =>
      window.dispatchEvent(
        new CustomEvent('clip:mock-event', {
          detail: { event: 'storage', data: { bytes: 1_200_000_000, limit: 1_073_741_824 } },
        }),
      ),
    )
  await dispatch()
  const dialog = page.getByRole('dialog')
  await expect(dialog).toBeVisible()
  await expect(dialog).toContainText('Clip storage is full')
  await expect(dialog).toContainText('use 1.1 GB of the 1 GB storage limit')
  await expect(dialog).toContainText('still saved')
  await page.getByRole('button', { name: 'OK' }).click()
  await expect(page.getByRole('dialog')).toHaveCount(0)
  // The host re-emits on every open while over the limit; the page mutes it for the rest of the run.
  await dispatch()
  await page.waitForTimeout(150)
  await expect(page.getByRole('dialog')).toHaveCount(0)
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

test('the Inbox count matches the list instead of a seeded zero', async ({ page }) => {
  const all = page.locator('[role="tab"][aria-selected="true"]')
  await expect(all.locator('.tab-label')).toHaveText('Inbox')
  await expect(all.locator('.tab-count')).toHaveText('1,000')
  await expect(shown(page)).toHaveText('1,000 shown')
})

test('the Settings preview and the known-limits note are on screen', async ({ page }) => {
  await page.locator('button[aria-label="Settings"]').click()
  await expect(page.locator('.dialog')).toHaveCount(1)
  // Sidebar lands on General; the limits note lives under About.
  await page.getByRole('button', { name: 'About' }).click()
  await expect(page.getByText('Known limits')).toBeVisible()
  // G-03: at 900x700 each section must fit without a scroll; the limits note once pushed the sheet over.
  for (const section of ['General', 'Capture', 'Appearance', 'Notifications', 'Storage', 'About']) {
    await page.getByRole('button', { name: section }).click()
    // The pane remounts per section (transition); measure only once the new section has committed.
    await expect(page.locator('.settings-pane .t-title')).toHaveText(section)
    const overflow = await page.evaluate(() => {
      const body = document.querySelector('.dialog-body') as HTMLElement
      return body.scrollHeight - body.clientHeight
    })
    expect(overflow).toBeLessThanOrEqual(0)
  }
  await page.getByRole('button', { name: 'Notifications' }).click()
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
  // The swapped webfonts re-wrap text rows and shift heights mid-test; measure only once they land.
  await page.evaluate(() => document.fonts.ready)
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
  // Close lands back on the list, not the button: Up/Down work with no extra click.
  await expect(selectedRow(page)).toBeFocused()
  const first = (await page.locator('.row.selected .row-index').textContent()) ?? ''
  await page.keyboard.press('ArrowDown')
  const second = (await page.locator('.row.selected .row-index').textContent()) ?? ''
  expect(second).not.toBe(first)
})

test('the theme toggle hands the keyboard back to the list', async ({ page }) => {
  await page.locator('button[aria-label="Toggle theme"]').click()
  await expect(selectedRow(page)).toBeFocused()
  const first = (await page.locator('.row.selected .row-index').textContent()) ?? ''
  await page.keyboard.press('ArrowDown')
  const second = (await page.locator('.row.selected .row-index').textContent()) ?? ''
  expect(second).not.toBe(first)
  await page.keyboard.press('ArrowUp')
  await expect(page.locator('.row.selected .row-index')).toHaveText(first)
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
  await expect(activeTab(page)).toHaveText('Images')
  await expect(page.locator('[role="tab"][aria-selected="true"]')).toBeFocused()
  await page.keyboard.press('ArrowLeft')
  await expect(activeTab(page)).toHaveText('Inbox')
})

test('Left/Right/Home/End in the search field move the caret, never the list or tab', async ({ page }) => {
  await page.keyboard.press('Control+k')
  await expect(search(page)).toBeFocused()
  await page.keyboard.type('ab')
  const tab = await activeTab(page).textContent()
  const sel = await selectedRow(page).textContent()
  await page.keyboard.press('ArrowLeft')
  await expect(search(page)).toHaveValue('ab')
  await expect(activeTab(page)).toHaveText(tab || '')
  await expect(selectedRow(page)).toHaveText(sel || '')
  expect(await search(page).evaluate((el) => (el as HTMLInputElement).selectionStart)).toBe(1)
  await page.keyboard.press('Home')
  expect(await search(page).evaluate((el) => (el as HTMLInputElement).selectionStart)).toBe(0)
  await expect(selectedRow(page)).toHaveText(sel || '')
  await page.keyboard.press('End')
  expect(await search(page).evaluate((el) => (el as HTMLInputElement).selectionStart)).toBe(2)
  await expect(search(page)).toHaveValue('ab')
  await expect(selectedRow(page)).toHaveText(sel || '')
  await expect(activeTab(page)).toHaveText(tab || '')
})

test('row tooltips only appear when the text is clipped', async ({ page }) => {
  // Row 01 is a short one-line link: fully visible, so hovering sets no tooltip.
  const short = page.locator('.row[data-id="1"] .row-text')
  await short.hover()
  await expect(short).not.toHaveAttribute('title')
  // Row 02 is a long clamped clip: the tooltip carries the full text.
  const long = page.locator('.row[data-id="2"] .row-text')
  await long.hover()
  const tip = await long.getAttribute('title')
  expect(tip != null && tip.length > 0).toBe(true)
})

test('empty search still drives the list with Left/Right/Home/End', async ({ page }) => {
  await search(page).focus()
  await expect(search(page)).toHaveValue('')
  await page.keyboard.press('End')
  const last = (await page.locator('.row.selected .row-index').textContent()) ?? ''
  expect(last).not.toBe('01')
  await page.keyboard.press('Home')
  await expect(page.locator('.row.selected .row-index')).toHaveText('01')
  const tab = await activeTab(page).textContent()
  await page.keyboard.press('ArrowRight')
  await expect(activeTab(page)).not.toHaveText(tab || '')
  // Right from Inbox lands on Images, which is a saved search: entering it fills the box.
  await expect(activeTab(page)).toHaveText('Images')
  await expect(search(page)).toHaveValue('tag:image ')
  await page.keyboard.press('Escape')
  await expect(activeTab(page)).toHaveText(tab || '')
  // The caret never owned these keys: the box is empty again and keeps focus.
  await expect(search(page)).toHaveValue('')
  await expect(search(page)).toBeFocused()
})

test('the page makes no network request and logs no console error', async ({ page }) => {
  const errors: string[] = []
  const http: string[] = []
  page.on('console', (message) => { if (message.type() === 'error') errors.push(message.text()) })
  page.on('pageerror', (error) => errors.push(String(error)))
  page.on('request', (request) => { if (/^https?:/.test(request.url())) http.push(request.url()) })
  await page.goto(PAGE)
  await page.waitForSelector('.row')
  await page.keyboard.type('tag:image', { delay: 20 })
  await page.waitForTimeout(600)
  expect(http).toEqual([])
  expect(errors).toEqual([])
})

test('Change case offers upper, lower, title and sentence case', async ({ page }) => {
  const overlay = page.locator('.editor-overlay')
  const body = overlay.locator('.ProseMirror')
  await page.keyboard.press('F2')
  await expect(body).toBeFocused()
  // The mock clip loads its stored body first; replace it so the assertions see only typed text.
  await expect(body).not.toHaveText('')
  await page.keyboard.press('Control+a')
  await page.keyboard.type('the cat and the dog. it ran!')
  // A collapsed caret acts on the whole clip, like Clear formatting.
  await overlay.getByRole('button', { name: 'Change case' }).click()
  await overlay.getByRole('button', { name: 'UPPERCASE', exact: true }).click()
  await expect(body).toHaveText('THE CAT AND THE DOG. IT RAN!')

  await page.keyboard.press('Control+a')
  await overlay.getByRole('button', { name: 'Change case' }).click()
  await overlay.getByRole('button', { name: 'Title Case', exact: true }).click()
  await expect(body).toHaveText('The Cat and the Dog. It Ran!')

  await page.keyboard.press('Control+a')
  await overlay.getByRole('button', { name: 'Change case' }).click()
  await overlay.getByRole('button', { name: 'Sentence case', exact: true }).click()
  await expect(body).toHaveText('The cat and the dog. It ran!')

  await page.keyboard.press('Control+a')
  await overlay.getByRole('button', { name: 'Change case' }).click()
  await overlay.getByRole('button', { name: 'lowercase', exact: true }).click()
  await expect(body).toHaveText('the cat and the dog. it ran!')
  await expect(overlay.getByText('Unsaved changes').first()).toBeVisible()
})
