# RESEARCH — AI-agent testing setup for Clip v2 (late 2026)

Date: 2026-11-25. Read-only research; no installs, no product changes.
Every version, size and price below was fetched from the vendor's own page or the npm registry on
**2026-11-25**, not recalled. Local facts were measured on this machine the same day.
Where something was not verified, it says so in **Uncertainty** at the end.

Clip v2 shape (measured): WPF .NET 10 host, one warm WebView2, single-file React 19 page
`UI\web\dist\index.html` = **2.73 MB**, served by the host at `https://clip.local/index.html`
(virtual-host folder mapping in a dev build, in-memory resource in a release build;
`UI\ShellWindow.xaml.cs:96-113`). Page bridge is WebView2 `postMessage`
(`UI\web\src\host.ts`). When `window.chrome.webview` is absent the page boots a seeded mock host
(`host.ts:401-404`) — so a plain browser exercises the whole UI against fixtures, never the owner's
clipboard.

---

## 1. Answers to the five questions

### 1. Playwright + `@playwright/test`

- **Current:** `@playwright/test` **1.65.1**, published 2026-11-13 (npm registry). It ships Chromium
  **159.0.8154.6**.
- **Reuse of the cached browser:** the cache is `chromium-1243` (Chrome **153.0.8010.12**,
  431.9 MB on disk) plus `chromium_headless_shell-1243` (270.1 MB) and `ffmpeg-1011` (3.4 MB).
  `chromium-1243` is exactly the revision of **Playwright 1.63.0** (`browsers.json` at tag
  `v1.63.0` → chromium 1243; `v1.62.0` → 1234, `v1.64.0` → 1248, `v1.65.x` → 1248/159).
  So **pin `@playwright/test@1.63.0`** and nothing downloads. 1.65.1 would fetch a new
  ~430 MB Chromium (+270 MB headless shell if headless runs are wanted).
- **No implicit download:** `playwright`, `playwright-core` and `@playwright/test` at 1.63.0 declare
  **no `scripts`/postinstall** (package.json on unpkg), so `npm i` fetches JS only. Browsers are
  installed *only* by the explicit `npx playwright install`, and the registry skips a browser whose
  folder holds the `INSTALLATION_COMPLETE` marker (`registry/index.ts` at `v1.63.0`). Both cached
  folders carry that marker — verified on disk.
- **Minimal project:** one `playwright.config.ts` and a `tests/` folder inside `UI\web`.
  Serve the **built** file, not the Vite dev server, because the shipped artifact is
  `dist/index.html`. `vite preview` reproduces a real HTTP origin; the host serves over `https://`
  with `DenyCors`, so a served http origin is closer to production than `file://` (which gives an
  opaque `null` origin and will break the day the page gains a `fetch`, module import or worker).
  The page is a single inlined file, so `file://` does work today — it is just the less honest of the
  two, and it hides origin/CSP mistakes.
- **Keyboard contract is testable:** export menu handles `Escape` / `ArrowUp` / `ArrowDown`
  (`App.tsx:571-584`), zoom handles `Ctrl+0/+/-` (`App.tsx:620-634`), tab/list/search are ordinary
  DOM. There are **0 `data-testid` attributes** in `src\` — role/text locators work for most of it;
  add `data-testid` only where two controls read the same.

### 2. Playwright MCP and agent tooling (planner / generator / healer, CLI)

- **What it is:** `npx playwright init-agents --loop=vscode|claude|codex|opencode` writes agent
  definitions (instructions + Playwright MCP tools) into the repo. Planner explores and emits a
  Markdown plan in `specs/`; generator turns the plan into runnable specs (verifying locators live);
  healer replays a failing test, patches locators/waits, re-runs until green or declares it a real
  bug. All three are just your coding agent + the local MCP server (playwright.dev/docs/test-agents).
- **Cloud account:** none. `@playwright/mcp` **0.0.86** (2026-11-18) and `@playwright/cli`
  **0.1.25** (2026-11-18) are Apache-2.0 and run locally; no key, no sign-in.
- **`file://` support:** blocked by default. MCP restricts the filesystem to workspace roots and
  blocks navigation to `file://` unless started with `--allow-unrestricted-file-access`
  (README option table). A served `http://127.0.0.1:4173` URL needs no flags — another reason to
  serve.
- **Bonus for layer 2:** MCP and the CLI both take `--cdp-endpoint`, so an agent can drive the
  *real* Clip shell through the same CDP port as a Playwright test.
- **MCP vs CLI:** the README itself recommends the CLI for coding agents (smaller tool schemas, fewer
  tokens) and MCP for persistent exploratory loops. For token cost on a 2.73 MB single-page app,
  CLI wins.

### 3. Driving the real WebView2 shell

- **Yes, `connectOverCDP` works with WebView2.** Microsoft documents the exact recipe on
  playwright.dev/docs/webview2: start the WebView2 process with `--remote-debugging-port=9222`,
  then `chromium.connectOverCDP('http://localhost:9222')`, take
  `browser.contexts()[0].pages()[0]`.
- **Two ways to enable it:** (a) `CoreWebView2EnvironmentOptions.AdditionalBrowserArguments =
  "--remote-debugging-port=9222"` passed to `CoreWebView2Environment.CreateAsync`; (b) the
  environment variables `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` and `WEBVIEW2_USER_DATA_FOLDER`
  (documented in the same Playwright page, no code change).
- **Clip already has (a), correctly guarded.** `UI\ShellWindow.xaml.cs:66-74`: the port comes from
  `CLIP_V2_DEBUG_PORT` inside `#if DEBUG`, and a Release build compiles the whole branch out. So a
  Debug build is CDP-drivable today and `dist\Clip.exe` is not.
- **Why it must never ship on:** an open CDP port is full remote control of the page that holds the
  clipboard history — any local process can attach, read every row, invoke the page's
  `postMessage` bridge (`copy`, `delete`, `export`, settings writes) and navigate. It also keeps a
  listening socket for the life of the app. Test-only, dev-only, environment-gated is the only
  honest configuration; a user-facing "enable debugging" toggle in Settings would turn a private
  history into a local attack surface for zero user benefit.
- **Honest isolation:** `CLIP_V2_DEBUG_PORT` + `APPDATA=<temp dir>` gives a throwaway config, SQLite
  DB and WebView2 profile (`Config.cs:15-23`, `ShellWindow.xaml.cs:63`), so acceptance runs never
  touch the owner's real history.

### 4. Commercial AI QA tools

Plainly: **none of them can attach to a WebView2 window running on this machine.** They drive their
own browsers (cloud or a private-location runner they launch), or, in QA Wolf's desktop product, a
fresh Windows VM of theirs. A local desktop app is reachable only by installing it in their VM
(QA Wolf) or not at all. None of them expose "connect to CDP on 9222 and use my page".

| Vendor | Current public price (2026-11-25) | Reaches local WebView2? | Data implication |
|---|---|---|---|
| QA Wolf | Pup $50/mo (250 credits); Pack $150/mo (1,500 credits, desktop included here); Enterprise custom. Desktop run = 2 credits. | Only by installing the exe in **their** Windows VM; clicks OS-level, no DOM/CDP into WebView2 | Clipboard fixtures (and any history the app captures in that VM) live in their cloud |
| Momentic | Free $0 + $50 usage/mo; Scale $500/mo; $0.0125/step, hosted browser $0.005/min, triage $1.25/call | No — hosted browsers | Page + screenshots + step text leave the machine |
| Reflect (SmartBear) | No public prices: Premium/Advanced/Enterprise with 5,000/20,000/40,000 credits, 14-day trial, "Contact Sales" | No — hosted recorder | Same |
| Testim (Tricentis) | Community plan free; paid tiers quote-only on the page fetched | No — its own browser (tunnel reaches servers, not a local WebView2) | Same |
| Meticulous | No public price found (`/pricing` returned 404 on 2026-11-25); demo-based | No | Same |
| Applitools | Starter $667/mo billed annually (100k component or 1,000 page checkpoints) | No — it is a visual assertion layer that needs a driver (Playwright/Selenium/Appium) | Uploads screenshots of clipboard rows to their cloud |
| Checkly | Hobby $0; Starter $24/mo; Team $64/mo (annual); Playwright check runs on their cloud or a private location **you** run | No — it launches its own browser; cannot attach to an existing WebView2 | Check code and traces in their cloud |
| BrowserStack | Desktop $29/mo billed annually (single user); plans from $12.50/mo | No — remote real desktop browsers | Same |

### 5. LLM-driven browser libraries

- **Stagehand** — SDK for browser agents (Browserbase), local OSS use is free plus your LLM tokens;
  Browserbase cloud is $0 / $20 / $99 per month. npm: use **`@browserbasehq/stagehand`**; the bare
  `stagehand` name on npm is an unrelated 2023 package (latest 1.0.1, 2023-01-03) — don't install it.
- **Browser Use** — MIT harness; no subscription; their cloud is **$0.06/browser-hour**, home-IP
  proxy $5/GB, LLM billed per token (their example: 100 browser-hours + 0.9 GB + 20M/1M tokens on
  "BU Fast" = **$13.50**). Their own docs bill model calls separately, so cost scales with the model
  you pick.
- **Midscene.js** — MIT, free; natural-language `aiAct` / `aiAssert` on top of Playwright or its own
  agent. Their own AppControlBench figure: **$0.59 of model API for 60 tasks**. That is the cheapest
  credible LLM-driving number available, and still non-deterministic.
- **Do they beat deterministic Playwright as a release gate?** No. A gate must fail for the same
  reason twice. LLM actors re-read the page each run, so a passing run proves "a model found a path",
  not "the keyboard contract holds". They are good at *finding* flows and at asserting things that
  are hard to select (visual coherence, "does this look broken"), which is layer 3.

---

## 2. Fit table

| Tool | What it tests well here | Sees the real shell? | Price | Privacy | Verdict |
|---|---|---|---|---|---|
| `@playwright/test@1.63.0` | Keyboard contract, tab CRUD, search, export menu, mock-host flows, built single file | No (page only) | Free, Apache-2.0; **0 MB** download with the cached 1243 | Stays on disk; no network | **Adopt — layer 1** |
| Playwright MCP `0.0.86` / CLI `0.1.25` / `init-agents` | Exploration, plan → spec generation, healing, agent screenshots; can also attach via `--cdp-endpoint` | Via CDP only | Free | Local; page text goes to your model provider, not a QA vendor | **Adopt — layer 3 (and layer 2 driver)** |
| Playwright + `connectOverCDP` to WebView2 | Bridge protocol, real WebView2 rendering, real DB rows, real zoom | **Yes**, page inside the shell | Free | Local only; never ship the port | **Adopt — layer 2, Debug build only** |
| QA Wolf | Hosted Windows-VM desktop regression at scale, installer/menus/notifications | Their VM, OS-level clicks, no DOM | $150/mo min for desktop (2 credits/run) | Clipboard data in their cloud | **Skip** — cost and no DOM access |
| Momentic | AI-authored web flows, triage | No | $50 usage free, $500/mo | Payloads + screenshots leave | **Skip** |
| Reflect | Hosted recorder | No | Quote-only | Same | **Skip** |
| Testim | Hosted recorder, Community free | No | Free tier, paid quote | Same | **Skip** |
| Meticulous | Record/replay web regression | No | Not public | Same | **Skip** |
| Applitools | Visual diffing on top of a driver | No driver here | $667/mo annual | Screenshots of clipboard rows uploaded | **Skip** — one app, one screen |
| Checkly | Uptime/synthetic Playwright checks, 24/7 | No | $0 / $24 / $64 | Check code + traces leave | **Skip** — no service to monitor |
| BrowserStack | Cross-browser/device matrix | No | $29/mo desktop | Same | **Skip** — WebView2 is one engine |
| Stagehand | LLM actions inside Playwright | Only with `--cdp-endpoint` plumbing you write | Free + tokens; Browserbase $20/$99 | Page content to LLM | **Skip as gate**, optional exploration |
| Browser Use | Autonomous browsing | No (own browser) | $0.06/browser-hr + tokens | Same | **Skip** |
| Midscene.js | NL assertions on top of Playwright | Inherits Playwright | Free + ~$0.59/60 tasks | Same | **Optional layer 3** if NL assertions are wanted |

---

## 3. Recommendation — three layers

### Layer 1 — deterministic page e2e (the release gate)

Tool: `@playwright/test@1.63.0` inside `UI\web`. It tests the exact built file, needs no cloud, no
account, no new browser, and its failures are reproducible.

Install (Node 26.13.1 is fine; npm 12.2.3):

```
cd UI\web
cmd /c "npm i -D @playwright/test@1.63.0"
```

`cmd /c` because PowerShell blocks `npm.ps1` on this machine (`ExecutionPolicy`). Do **not** run
`npx playwright install` — 1.63.0 expects revision 1243, which is already cached with its
`INSTALLATION_COMPLETE` marker, so the install step would be a no-op anyway.

Minimal `UI\web\playwright.config.ts`:

```ts
import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  reporter: [['html', { open: 'never' }]],
  use: { baseURL: 'http://127.0.0.1:4173', trace: 'retain-on-failure' },
  webServer: {
    command: 'npm run preview -- --port 4173 --strictPort',
    url: 'http://127.0.0.1:4173/index.html',
    reuseExistingServer: true,
  },
})
```

`vite preview` serves `dist/`, so `npm run build` must run before the suite (a `pretest` script, or a
one-line `npm run build && npx playwright test`). Tests hit the page with the **mock host** — seeded
fixtures, no real clipboard, so the suite can run unattended and in parallel.

Specs worth having on night one (all observable today):
export-menu keyboard (`Escape`, `ArrowUp/Down` focus walk), `Ctrl+0/+/-` zoom, search typing and
empty state, tab create/rename/delete, move-to-tab, list row selection, pin toggle, export menu
opens and closes on outside click.

For the owner: `npx playwright test --reporter=html` produces a clickable report with screenshots —
judgeable without reading code, which matches how this project signs off.

### Layer 2 — real-shell live acceptance (narrow, honest, not a gate)

What automation is honest: launch the **Debug** build with
`CLIP_V2_DEBUG_PORT=9222` and `APPDATA=<temp>`, `connectOverCDP`, then assert page-level truth inside
the real shell: bridge round-trips (list loads from the real SQLite DB), copy → host writes the real
clipboard, tab CRUD persists, zoom applies, and the built bundle is the one being served.
That catches the class of bug a browser test cannot: `host.ts` ↔ `ClipHost` protocol drift, WebView2
rendering/zoom, real data shapes.

What stays manual or agent-screenshot:
native window chrome and first-paint, tray icon/menu, `Alt+V` hotkey registration and firing, paste
into Notepad/Excel and focus return to the previous app, autostart, DPI/multi-monitor, installer and
the self-contained exe, and the release exe's *embedded* bundle (no folder mapping). Also verify the
release exe is not CDP-drivable — a one-line check that port 9222 refuses a connection when launched
without the env var, and that `CLIP_V2_DEBUG_PORT` does nothing in Release (it is compiled out).

Do not turn this into the gate: it needs a desktop session, a debug build, and a mutable DB, and it
cannot see the parts users actually judge (tray, hotkey, paste target).

### Layer 3 — exploratory and visual agent passes

Use Playwright CLI/MCP + `npx playwright init-agents --loop=opencode` to (a) explore a flow and write
the Markdown plan, (b) generate a first draft of a spec, (c) heal a spec after an intentional UI
rename, and (d) take screenshots a fresh sub-agent reads for visual sign-off. Keep generated specs
reviewed and committed: healing silently rewrites assertions, so a healed spec must be re-read by a
human/agent before it counts. LLM browser libraries (Stagehand/Midscene/Browser Use) belong here at
most; none should decide release.

---

## 4. Exact commands

```
cd UI\web
cmd /c "npm i -D @playwright/test@1.63.0"      # no browser download; 1.63.0 == cached chromium-1243
npx playwright --version                        # expect 1.63.0
npm run build
npx playwright test
```

If a future Playwright upgrade is wanted: 1.64/1.65 expect revision 1248 → a new Chromium of about
**432 MB on disk** (plus 270 MB if the headless shell revision changes too), downloaded by
`npx playwright install`. Avoid until there is a reason.

Real-shell run (PowerShell):

```
$env:APPDATA = "$env:TEMP\clip-acceptance"
$env:CLIP_V2_DEBUG_PORT = "9222"
dotnet build -c Debug
.\bin\Debug\net10.0-windows\Clip.exe
# then, from a Playwright script/test:
# chromium.connectOverCDP('http://127.0.0.1:9222')
```

---

## 5. Do not install

| Thing | Why not |
|---|---|
| `@playwright/test@latest` (1.65.1) | Forces a ~432 MB new Chromium; no feature this app needs |
| `npx playwright install` (any browser) | Redundant: revision 1243 is already cached and matches the pinned runner |
| Cypress | No CDP attach to WebView2; a second, larger runtime for the same page |
| Selenium / WebDriverIO / Puppeteer | Extra driver/protocol layer; Puppeteer is a subset of what Playwright already gives |
| Appium / WinAppDriver | Windows desktop automation that cannot see WebView2 DOM reliably; WebView2 content is a separate process tree. CDP is the supported route |
| jsdom / Testing Library only | Cannot exercise real layout, keyboard focus order, virtualized rows, WebView2 rendering |
| QA Wolf, Momentic, Reflect, Testim, Meticulous, Applitools, Checkly, BrowserStack | None can attach to a local WebView2; each uploads clipboard-adjacent content; all cost more per month than this app's entire test need |
| Stagehand (`stagehand` npm name) | Wrong package — the maintained one is `@browserbasehq/stagehand`; still not a gate |
| Browser Use / Midscene as the gate | Non-deterministic pass/fail and per-run token cost; use only for exploration |
| A second Playwright version in the same `UI\web` | Two runners fight over the same `node_modules` and browser revision; pin one |

---

## 6. Uncertainty (stated plainly)

- **Not executed.** This was a read-only research task: the CDP attach against Clip's Debug build was
  not run tonight. The code path exists (`ShellWindow.xaml.cs:66-74`) and Microsoft documents the
  WebView2 recipe, but "it attaches on this machine, with `AreDevToolsEnabled=false`" is inferred,
  not observed. Verify in one 2-minute run before relying on layer 2.
- **`AreDevToolsEnabled=false`** disables F12/context-menu DevTools; whether it also restricts the
  CDP endpoint is not documented in what I read. If `connectOverCDP` is refused, that setting is the
  first suspect (it is a Debug-build-only question, not a shipping change).
- **Prices move.** QA Wolf, Momentic, Checkly, Browserbase, Browser Use and Applitools publish
  numbers (quoted above from their pages on 2026-11-25). Reflect, Testim and Meticulous publish no
  usable number — Reflect/Testim are quote-only, Meticulous' pricing URL 404'd. Treat those three as
  "unknown, likely four figures a year".
- **QA Wolf desktop** is documented as fresh Windows/macOS VMs with your installer; whether their
  desktop driver can reach inside a WebView2 control is not stated on their site. My reading is
  "OS-level clicks only, no DOM", so WebView2 assertions would be screenshot/OCR-grade at best.
- **Package names drift.** `@playwright/mcp` and `@playwright/cli` ship weekly (0.0.86 / 0.1.25 on
  2026-11-18); pin them if a workflow depends on them.
- 1.63.0 was chosen because its `browsers.json` revision is exactly 1243. A 1.63.x hotfix was not
  published (last 1.63 entry is a beta timestamp), so 1.63.0 is the correct pin today.

## Sources (fetched 2026-11-25)

- npm registry: `@playwright/test` 1.65.1 / 1.63.0 (published 2026-11-13 / 2026-09-04),
  `@playwright/mcp` 0.0.86, `@playwright/cli` 0.1.25, `stagehand` 1.0.1, `@midscene/web` 1.18.2,
  `browser-use` 0.9.0.
- github.com/microsoft/playwright tag `v1.63.0` → `packages/playwright-core/browsers.json`
  (chromium 1243), `packages/playwright-core/src/server/registry/index.ts` (INSTALLATION_COMPLETE),
  and tags `v1.61.0`/`v1.62.0`/`v1.64.0` for the revision sequence.
- playwright.dev/docs/webview2 (connectOverCDP, `AdditionalBrowserArguments`, env vars,
  per-worker user data dir), /docs/test-agents (planner/generator/healer, `init-agents`),
  /docs/test-webserver, /docs/browsers.
- github.com/microsoft/playwright-mcp README (0.0.86): `--cdp-endpoint`, `--allow-unrestricted-file-access`,
  `file://` blocked by default, no account.
- qawolf.com/pricing and qawolf.com/desktop-app-testing.
- momentic.ai/pricing (last-updated 2026-11-17 in page metadata).
- reflect.run/pricing; testim.io/pricing; meticulous.ai (pricing URL 404); applitools.com/pricing;
  checklyhq.com/pricing; browserstack.com/pricing.
- browserbase.com/pricing; browser-use.com/pricing; midscenejs.com (MIT, benchmark cost).
- Local: `%LOCALAPPDATA%\ms-playwright` folder sizes and `chrome.exe` version; `UI\web\dist\index.html`
  size; `UI\ShellWindow.xaml.cs`, `UI\web\src\host.ts`, `Core\Config.cs`.
