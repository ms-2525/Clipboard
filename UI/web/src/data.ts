import type { ClipItem, ClipKind, HostTab } from './host'

/** Mock-only fixtures: used by `npm run dev` and headless screenshots, never inside WebView2. */

export const MOCK_TABS: HostTab[] = [
  { id: 1, name: 'Pinned', kind: 'pinned', count: 0 },
  { id: 2, name: 'Work', kind: 'custom', count: 0 },
  { id: 3, name: 'Design', kind: 'custom', count: 0 },
  { id: 4, name: 'Snippets', kind: 'custom', count: 0 },
]

const APPS = ['Chrome', 'Excel', 'Word', 'VS Code', 'Slack', 'Figma', 'Teams', 'Outlook', 'Notepad', 'Terminal']
const APPS_DESIGN = ['Figma', 'Chrome', 'Slack']
const APPS_WORK = ['Excel', 'Word', 'Outlook', 'Teams', 'Chrome', 'Slack']
const APPS_CODE = ['VS Code', 'Terminal', 'Chrome']

const NOW = Date.now()

const LOREM = [
  'Here is the revised statement of work for the Q3 rollout, including the revised milestone dates and the sign-off owners for each workstream.',
  'Thanks for the quick turnaround. I pushed the pricing table to the shared drive and flagged the two rows that still need finance approval.',
  'The reconciliation came out clean after the duplicate entry was removed; the closing balance now ties to the bank statement as at 31 October.',
  'Screenshot of the dashboard after the filter change — the monthly series now starts at the selected quarter instead of the full history.',
  'Please find attached the signed engagement letter and the updated KYC checklist. Let me know if anything else is needed for the file.',
  'Release 2.4 notes: virtualised list, faster cold start, a new single-line editor mode, and fixes for the paste-into-Excel path.',
  'Reminder: the office will be closed on Friday. Any approvals that need to go out this week should be with me by Thursday 4pm.',
  'I refactored the store to debounce writes; the old path was doing one fsync per keystroke which is why the UI felt sticky on large captures.',
]

const TITLES = ['Release notes', 'Client email', 'Reconciliation', 'SOW draft', 'Dashboard', 'Code snippet']

const FILE_SETS = [
  ['Q3-forecast-v4.xlsx', 'reconciliation-notes.md', 'signed-engagement.pdf'],
  ['brand-tokens.json', 'clip-icon-512.svg', 'hero-dark.png'],
  ['board-minutes.docx', 'action-items.xlsx', 'appendix-b.pdf'],
  ['migration.sql', 'README.md', 'schema-diagram.svg'],
]

function mulberry32(a: number) {
  return () => {
    a |= 0
    a = (a + 0x6d2b79f5) | 0
    let t = Math.imul(a ^ (a >>> 15), 1 | a)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

const svg = (a: string, b: string, shape: string) =>
  'data:image/svg+xml;charset=utf-8,' +
  encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="120" height="90" viewBox="0 0 120 90"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${a}"/><stop offset="1" stop-color="${b}"/></linearGradient></defs><rect width="120" height="90" rx="6" fill="url(#g)"/>${shape}</svg>`,
  )

const IMAGES = [
  svg('#1F2532', '#C2410C', '<circle cx="38" cy="36" r="18" fill="none" stroke="#F6F7F9" stroke-width="2.5"/><path d="M60 66 L88 26 L106 66 Z" fill="#F6F7F9" fill-opacity=".85"/>'),
  svg('#E8EAEE', '#B7BFCB', '<rect x="20" y="20" width="46" height="50" rx="4" fill="#FFFFFF" stroke="#8A92A1"/><rect x="28" y="30" width="30" height="3" rx="1.5" fill="#C2410C"/><rect x="28" y="39" width="24" height="3" rx="1.5" fill="#9AA2B1"/><rect x="28" y="48" width="27" height="3" rx="1.5" fill="#9AA2B1"/><circle cx="88" cy="58" r="18" fill="#C2410C" fill-opacity=".16"/><circle cx="88" cy="58" r="8" fill="#C2410C"/>'),
  svg('#141821', '#2C3648', '<path d="M14 68 L38 44 L56 60 L76 30 L106 68 Z" fill="#F6F7F9" fill-opacity=".85"/><circle cx="92" cy="24" r="7" fill="#C2410C"/>'),
]

export function mockThumb(id: number): string {
  return IMAGES[id % IMAGES.length]
}

export function makeClips(count = 1000): ClipItem[] {
  const rand = mulberry32(20261125)
  const pick = <T,>(arr: T[]) => arr[Math.floor(rand() * arr.length)]
  const clips: ClipItem[] = []

  for (let i = 0; i < count; i++) {
    const roll = rand()
    let kind: ClipKind = roll < 0.58 ? 'text' : roll < 0.82 ? 'rich' : roll < 0.92 ? 'image' : 'files'
    if (i === 0 || i === 2) kind = 'rich'
    if (i % 7 === 3) kind = 'files'
    if (i % 5 === 4) kind = 'image'

    const app =
      kind === 'image' ? pick(APPS_DESIGN) : kind === 'files' ? pick(APPS_WORK) : kind === 'rich' ? pick(APPS_CODE) : pick(APPS)
    const files = kind === 'files' ? pick(FILE_SETS) : []
    const body =
      kind === 'files'
        ? files.join('\n')
        : Array.from({ length: 3 + (i % 5) + (i % 11 === 0 ? 9 : 0) }, () => pick(LOREM)).join(' ')
    const full = body.length > 12 ? body : pick(LOREM)
    const text = full.slice(0, 1200)

    let formats: string[]
    if (kind === 'rich') formats = ['HTML', 'RTF', 'Text']
    else if (kind === 'image') formats = ['PNG', 'DIB']
    else if (kind === 'files') formats = ['FileDrop']
    else formats = ['Text']

    const customRoll = rand()
    const tabId = customRoll < 0.15 ? 1 : customRoll < 0.3 ? 2 : customRoll < 0.42 ? 3 : customRoll < 0.5 ? 4 : null

    clips.push({
      id: i + 1,
      kind,
      app,
      title:
        kind === 'files'
          ? `${files[0]} and ${files.length - 1} more`
          : kind === 'image'
            ? 'Screenshot 2026-11-24 143012.png'
            : pick(TITLES),
      time: new Date(NOW - (i * 12 + (i % 5)) * 60000).toISOString(),
      pinned: tabId === 1,
      tabId,
      text: text.length > 12 ? text : pick(LOREM),
      hasImage: kind === 'image',
      hasHtml: kind === 'rich',
      hasRtf: kind === 'rich',
      formats,
      tags: [],
      truncated: full.length > text.length,
      remainingChars: full.length - text.length,
    })
  }

  // Deterministic showcase at the top of "All": a one-line link, a clamped long clip,
  // a spreadsheet, a file drop and an image pasted from Excel (two proven tags).
  const patch = (index: number, values: Partial<ClipItem>) => {
    clips[index] = { ...clips[index], ...values }
  }
  patch(0, {
    kind: 'text',
    app: 'Chrome',
    title: 'API reference',
    text: 'https://docs.stripe.com/api/charges',
    formats: ['Text'],
    truncated: false,
    remainingChars: 0,
  })
  patch(1, {
    kind: 'text',
    app: 'Word',
    title: 'Policy draft',
    text: LOREM.concat(LOREM).join('\n'),
    formats: ['Text'],
    truncated: true,
    remainingChars: 1840,
  })
  patch(2, {
    kind: 'rich',
    app: 'Excel',
    title: 'Q3 forecast',
    text: LOREM[2],
    formats: ['Biff12', 'HTML', 'Text'],
    hasHtml: true,
    hasRtf: false,
    truncated: false,
    remainingChars: 0,
  })
  patch(4, {
    kind: 'image',
    app: 'Excel',
    title: 'Screenshot 2026-11-24 143012.png',
    formats: ['PNG', 'Biff12'],
    hasImage: true,
    // No OCR text: the older-clip / no-language case the row must still answer with a placeholder.
    text: '',
    truncated: false,
    remainingChars: 0,
  })
  return clips
}

export const RICH_EDITOR_CONTENT = `
<p><strong>Release notes — Clip 2.4</strong><br><em>Prepared for the Thursday review</em></p>
<p>This build focuses on the clipboard list: rows carry up to <strong>ten lines</strong> of source text, the list is virtualised, and selected rows keep the provenance line visible. Read the <a href="#">migration notes</a> before rolling out to the ops machine.</p>
<blockquote><p><strong>Dark band</strong> — pasted from the client portal. Light text on a dark surface survives the paste path without a theme wash.</p></blockquote>
<p>Numbers confirmed with finance:</p>
<ul>
  <li>Retention cap raised to <strong>5,000</strong> clips.</li>
  <li>Total captured text this week: <strong>₹4,82,000</strong> in invoice values.</li>
  <li>Two flagged entries were cleared manually.</li>
</ul>
<p>Pending: single-line mode for the paste preview, and a <em>discard changes</em> guard when the editor has unsaved work.</p>
`
