import { test, expect } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { pathToFileURL } from 'node:url'

const dist = pathToFileURL(fileURLToPath(new URL('../dist/index.html', import.meta.url))).href

test('revisiting Images never flashes the empty state', async ({ page }) => {
  await page.goto(dist + '?mock=1000')
  await page.waitForSelector('.row')
  const images = page.getByRole('tab', { name: /Images/ })
  const inbox = page.getByRole('tab', { name: /^Inbox/ })

  // First visit populates the cache (may load once while uncached: allowed).
  await images.click()
  await expect(page.locator('.footer-left .t-meta')).toHaveText('Image results')
  await page.waitForSelector('.row')
  const firstCount = await page.locator('.shown-count').textContent()

  // Revisit: rows must stay on screen with no empty/loading flash.
  await inbox.click()
  await page.waitForSelector('.row')
  await images.click()
  await expect(page.locator('.shown-count')).toHaveText(firstCount ?? '')
  await expect(page.locator('.list .empty')).toHaveCount(0)
})

test('revisiting a custom tab never flashes the empty state', async ({ page }) => {
  await page.goto(dist + '?mock=1')
  await page.waitForSelector('.row')
  const work = page.getByRole('tab', { name: /^Work/ })
  const inbox = page.getByRole('tab', { name: /^Inbox/ })

  await work.click()
  await page.waitForSelector('.row')
  const firstCount = await page.locator('.shown-count').textContent()

  await inbox.click()
  await page.waitForSelector('.row')
  await work.click()
  await expect(page.locator('.shown-count')).toHaveText(firstCount ?? '')
  await expect(page.locator('.list .empty')).toHaveCount(0)
})
