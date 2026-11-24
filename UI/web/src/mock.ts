import { makeClips, MOCK_TABS, mockThumb } from './data'
import type { Host, ItemsQuery } from './host'

/**
 * Mock-only tag ids. The real host classifies in Core\ClipTags.cs; the page never
 * derives a tag from text, so the fixture states them explicitly.
 */
const MOCK_TAGS: Record<number, string[]> = {
  0: ['link'],
  2: ['spreadsheet'],
  3: ['files'],
  4: ['image', 'spreadsheet'],
  9: ['image', 'files', 'spreadsheet'], // exercises the 2 + "+N" fold
}

/** Deterministic 1,000-clip fixture for `npm run dev` and screenshots. Never used in WebView2. */
export function createMockHost(): Host {
  const clips = makeClips(1000).map((clip, index) => ({ ...clip, tags: MOCK_TAGS[index] ?? [] }))
  const tabs = MOCK_TABS.map((tab) => ({
    ...tab,
    count: clips.filter((clip) => clip.tabId === tab.id).length,
  }))
  const mainHistory = clips.filter((clip) => clip.tabId === null)

  const wait = <T,>(value: T) => new Promise<T>((resolve) => window.setTimeout(() => resolve(value), 25))
  const theme = new URLSearchParams(window.location.search).get('theme') === 'dark' ? 'dark' : 'light'

  return {
    mode: 'mock',
    init: async () => ({
      counts: {
        clips: mainHistory.filter((clip) => clip.kind !== 'image').length,
        images: mainHistory.filter((clip) => clip.kind === 'image').length,
      },
      tabs,
      config: { theme },
      items: mainHistory.slice(0, 200),
    }),
    getItems: async ({ tabId = null, offset = 0, limit = 1000, query = '' }: ItemsQuery = {}) => {
      const needle = query.trim().toLowerCase()
      const rows = clips.filter((clip) => {
        if (clip.tabId !== tabId) return false
        if (!needle) return true
        return (
          clip.title.toLowerCase().includes(needle) ||
          clip.text.toLowerCase().includes(needle) ||
          clip.app.toLowerCase().includes(needle)
        )
      })
      return wait(rows.slice(offset, offset + limit))
    },
    getItem: async (id: number) => {
      const clip = clips.find((entry) => entry.id === id)
      return wait(
        clip
          ? { text: clip.text, html: clip.hasHtml ? '<p>' + clip.text + '</p>' : null, rtf: null, formats: clip.formats }
          : null,
      )
    },
    getThumb: async (id: number) => wait<string | null>(mockThumb(id)),
    copy: async () => wait(undefined),
    paste: async () => {
      throw new Error('Paste is not wired yet')
    },
    onEvent: () => () => {},
  }
}
