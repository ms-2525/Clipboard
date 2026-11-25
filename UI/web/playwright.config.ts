import { defineConfig } from '@playwright/test'

// The page is one self-contained file; file:// is the same way the release harness loads it.
// Chromium 1243 is already in the machine cache (Playwright 1.63.0), so tests download nothing.
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 5_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    viewport: { width: 900, height: 700 },
    headless: true,
    // The mock host is selected by the page itself when it is not inside WebView2; ?mock fixes the size.
    baseURL: undefined,
  },
})
