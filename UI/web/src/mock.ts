import { makeClips, MOCK_TABS, mockThumb } from './data'
import type { ClipItem, Host, HostEvent, ItemsQuery, SearchResult } from './host'

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

/** The vocabulary a `tag:` query may name; anything else is searched as text. */
const KNOWN_TAGS = new Set([
  'image', 'files', 'spreadsheet', 'link', 'email', 'path', 'colour', 'number',
  'json', 'html', 'sql', 'xml', 'javascript', 'typescript', 'python', 'csharp',
  'rust', 'go', 'java', 'powershell',
])

/** Deterministic 1,000-clip fixture for `npm run dev` and screenshots. Never used in WebView2. */
export function createMockHost(): Host {
  let clips: ClipItem[] = makeClips(1000).map((clip, index) => ({ ...clip, tags: MOCK_TAGS[index] ?? [] }))
  const deleted: { clip: ClipItem; index: number }[] = []
  const listeners = new Set<(event: HostEvent) => void>()
  const tabs = MOCK_TABS.map((tab) => ({
    ...tab,
    count: clips.filter((clip) => clip.tabId === tab.id).length,
  }))
  const mainHistory = () => clips.filter((clip) => clip.tabId === null)
  const emit = (event: HostEvent) => {
    for (const listener of listeners) listener(event)
  }

  // Preview-only: `window.dispatchEvent(new Event('clip:mock-captured'))` pushes a capture
  // through the same page path as the real host, so live-capture layout can be checked.
  window.addEventListener('clip:mock-captured', () => {
    const newest = clips.reduce((max, clip) => Math.max(max, clip.id), 0)
    const source = clips.find((clip) => clip.id === 1) ?? clips[0]
    if (!source) return
    const item: ClipItem = {
      ...source,
      id: newest + 1,
      title: 'Mock capture',
      text: 'A fresh capture for the preview.',
      time: new Date().toISOString(),
      tabId: null,
      pinned: false,
    }
    clips = [item, ...clips]
    emit({ event: 'captured', data: item })
  })

  // Preview-only recopy of an existing id (the host's dedupe bump, seen as `captured`).
  window.addEventListener('clip:mock-recapture', (event) => {
    const id = (event as CustomEvent<{ id?: number }>).detail?.id
    const source = clips.find((clip) => clip.id === id)
    if (!source) return
    const item: ClipItem = { ...source, title: `Recaptured · ${source.title}`, time: new Date().toISOString() }
    clips = [item, ...clips.filter((clip) => clip.id !== id)]
    emit({ event: 'captured', data: item })
  })

  // Preview-only event injector, for the skipped / hotkeyFailed toasts.
  window.addEventListener('clip:mock-event', (event) => {
    const detail = (event as CustomEvent<{ event?: string; data?: unknown }>).detail
    if (detail?.event) emit({ event: detail.event, data: detail.data })
  })

  const wait = <T,>(value: T) => new Promise<T>((resolve) => window.setTimeout(() => resolve(value), 25))
  const theme = new URLSearchParams(window.location.search).get('theme') === 'dark' ? 'dark' : 'light'

  const match = (query: string, tabId: number | null): SearchResult => {
    const tags: string[] = []
    const terms: string[] = []
    const unknownTags: string[] = []
    for (const token of query.trim().toLowerCase().split(/\s+/)) {
      if (!token) continue
      const tag = /^([a-z0-9-]+):$/.exec(token)
      if (!tag) {
        terms.push(token)
      } else if (KNOWN_TAGS.has(tag[1])) {
        tags.push(tag[1])
      } else {
        unknownTags.push(tag[1])
      }
    }
    const rows = clips.filter((clip) => {
      if (clip.tabId !== tabId) return false
      if (tags.some((tag) => !clip.tags.includes(tag))) return false
      return terms.every(
        (term) =>
          clip.title.toLowerCase().includes(term) ||
          clip.text.toLowerCase().includes(term) ||
          clip.app.toLowerCase().includes(term) ||
          clip.formats.some((format) => format.toLowerCase().includes(term)),
      )
    })
    return { total: rows.length, items: rows.slice(0, 500), unknownTags }
  }

  return {
    mode: 'mock',
    init: async () => ({
      counts: {
        clips: mainHistory().filter((clip) => clip.kind !== 'image').length,
        images: mainHistory().filter((clip) => clip.kind === 'image').length,
      },
      tabs,
      config: { theme },
      items: mainHistory().slice(0, 200),
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
    search: async ({ query, tabId = null, limit = 500 }: ItemsQuery & { query: string }) => {
      const result = match(query, tabId)
      return wait({ ...result, items: result.items.slice(0, limit) })
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
    paste: async () => wait({ pasted: false, error: 'Paste is not wired yet' }),
    delete: async ({ id }) => {
      const index = clips.findIndex((clip) => clip.id === id)
      if (index < 0) throw new Error('Clip not found')
      deleted.push({ clip: clips[index], index })
      clips = clips.filter((clip) => clip.id !== id)
      emit({ event: 'deleted', data: { id } })
      return wait(undefined)
    },
    restore: async ({ id }) => {
      const index = deleted.findIndex((entry) => entry.clip.id === id)
      if (index < 0) throw new Error('Nothing to restore')
      const [{ clip, index: at }] = deleted.splice(index, 1)
      clips = [...clips.slice(0, at), clip, ...clips.slice(at)]
      return wait(undefined)
    },
    togglePin: async ({ id }) => {
      const clip = clips.find((entry) => entry.id === id)
      if (!clip) throw new Error('Clip not found')
      clips = clips.map((entry) => (entry.id === id ? { ...entry, pinned: !entry.pinned } : entry))
      return wait(undefined)
    },
    moveToTab: async ({ id, tabId }) => {
      clips = clips.map((entry) => (entry.id === id ? { ...entry, tabId } : entry))
      return wait(undefined)
    },
    hide: async () => wait(undefined),
    exportClip: async () => {
      throw new Error('Export is not wired yet')
    },
    onEvent: (listener) => {
      listeners.add(listener)
      return () => {
        listeners.delete(listener)
      }
    },
  }
}
