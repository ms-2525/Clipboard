import { makeClips, MOCK_TABS, mockThumb } from './data'
import { REAL_CLIPS } from './realClips'
import { toSettings, type ClipBody, type ClipItem, type Host, type HostEvent, type HostTab, type ItemsQuery, type SearchResult, type SettingsPatch } from './host'

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
  // `?mock=real` serves the owner's six real captures (rich HTML, RTF-only, files, image);
  // `?mock=1` is the 1,000-row fixture the virtualiser and keyboard checks use;
  // `?mock=1000` keeps every row in the All tab, for the 1,000-row render measurement.
  const params = new URLSearchParams(window.location.search)
  const real = params.get('mock') === 'real'
  const flat = params.get('mock') === '1000'
  // `?rich=1` makes every row of the 1,000-row fixture rich: the 10-mounted-rich-row measurement.
  const richAll = params.get('rich') === '1'
  const realBodies = new Map<number, ClipBody>(
    REAL_CLIPS.map((clip) => [
      clip.item.id,
      { text: clip.text, html: clip.html, rtf: clip.rtf, formats: clip.item.formats },
    ]),
  )
  const realThumbs = new Map<number, string>(
    REAL_CLIPS.filter((clip) => clip.thumb).map((clip) => [clip.item.id, clip.thumb as string]),
  )
  let clips: ClipItem[] = real
    ? REAL_CLIPS.map((clip) => clip.item)
    : makeClips(1000).map((clip, index) => ({
        ...clip,
        tags: MOCK_TAGS[index] ?? [],
        tabId: flat ? null : clip.tabId,
        hasHtml: richAll ? true : clip.hasHtml,
      }))
  // The bridge has no cancel path; counting calls here proves only mounted rows ask for a body.
  const counter = window as unknown as {
    __mockGetItemCalls?: number
    __mockGetItemIds?: number[]
    __mockGetThumbCalls?: number
    __mockGetThumbIds?: number[]
  }
  counter.__mockGetItemCalls = 0
  counter.__mockGetItemIds = []
  counter.__mockGetThumbCalls = 0
  counter.__mockGetThumbIds = []
  const deleted: { clip: ClipItem; index: number }[] = []
  const listeners = new Set<(event: HostEvent) => void>()
  let tabs: HostTab[] = MOCK_TABS.map((tab) => ({ ...tab }))
  const withCounts = () =>
    tabs.map((tab) => ({ ...tab, count: clips.filter((clip) => clip.tabId === tab.id).length }))
  const mainHistory = () => clips.filter((clip) => clip.tabId === null)

  /** The store's own messages, so the page validates the same fixture names it will see in WebView2. */
  const cleanName = (name: string, exceptId = 0) => {
    const clean = (name ?? '').trim()
    if (clean.length === 0) throw new Error('Enter a tab name.')
    if (clean.length > 24) throw new Error('Tab names are 24 characters at most.')
    if ([...clean].some((char) => char.charCodeAt(0) < 32)) throw new Error('Tab names cannot contain control characters.')
    if (tabs.some((tab) => tab.id !== exceptId && tab.name.toLowerCase() === clean.toLowerCase())) {
      throw new Error('A tab with that name already exists.')
    }
    return clean
  }
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
  // The mock keeps one settings object so the dialog behaves in a browser exactly as it does in WebView2.
  let settings = toSettings({ theme, themeChoice: theme, timeFormat: '24h', maxItems: 1000, hotkey: 'Alt+V' })

  const match = (query: string, tabId: number | null): SearchResult => {
    const tags: string[] = []
    const terms: string[] = []
    const unknownTags: string[] = []
    const addTag = (name: string): boolean => {
      const alias =
        name === 'excel' || name === 'sheet' || name === 'xls' || name === 'csv'
          ? 'spreadsheet'
          : name === 'img' || name === 'pic' || name === 'photo'
            ? 'image'
            : name === 'file'
              ? 'files'
              : name === 'url'
                ? 'link'
                : name
      if (!KNOWN_TAGS.has(alias)) return false
      tags.push(alias)
      return true
    }
    for (const token of query.trim().toLowerCase().split(/\s+/)) {
      if (!token) continue
      // The bare prefix is text, not a tag named "tag".
      if (token === 'tag:') {
        terms.push(token)
        continue
      }
      // `tag:image` is the explicit prefix; the bare `image:` form is what the chips write.
      const explicit = /^tag:([a-z0-9-]+)$/.exec(token)
      if (explicit) {
        if (!addTag(explicit[1])) {
          unknownTags.push(explicit[1])
          terms.push(token)
        }
        continue
      }
      const tag = /^([a-z0-9-]+):$/.exec(token)
      if (!tag) {
        terms.push(token)
      } else if (!addTag(tag[1])) {
        // The host keeps an unknown `name:` as the text the owner typed; so do we.
        unknownTags.push(tag[1])
        terms.push(token)
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
      tabs: withCounts(),
      config: settings,
      items: mainHistory().slice(0, Math.min(200, settings.maxItems)),
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
      return wait(rows.slice(offset, offset + Math.min(limit, tabId === null ? settings.maxItems : limit)))
    },
    search: async ({ query, tabId = null, limit = 500 }: ItemsQuery & { query: string }) => {
      const result = match(query, tabId)
      return wait({ ...result, items: result.items.slice(0, limit) })
    },
    getItem: async (id: number) => {
      counter.__mockGetItemCalls = (counter.__mockGetItemCalls ?? 0) + 1
      counter.__mockGetItemIds?.push(id)
      if (real && realBodies.has(id)) return wait(realBodies.get(id) ?? null)
      const clip = clips.find((entry) => entry.id === id)
      return wait(
        clip
          ? { text: clip.text, html: clip.hasHtml ? '<p>' + clip.text + '</p>' : null, rtf: null, formats: clip.formats }
          : null,
      )
    },
    getThumb: async (id: number) => {
      counter.__mockGetThumbCalls = (counter.__mockGetThumbCalls ?? 0) + 1
      counter.__mockGetThumbIds?.push(id)
      return wait<string | null>(real ? realThumbs.get(id) ?? null : mockThumb(id))
    },
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
    listTabs: async () => wait({ tabs: withCounts(), pinnedTabId: tabs.find((tab) => tab.kind === 'pinned')?.id ?? 0 }),
    createTab: async ({ name }) => {
      const clean = cleanName(name)
      const tab: HostTab = { id: Math.max(0, ...tabs.map((entry) => entry.id)) + 1, name: clean, kind: 'custom', count: 0 }
      tabs = [...tabs, tab]
      return wait(tab)
    },
    renameTab: async ({ id, name }) => {
      const tab = tabs.find((entry) => entry.id === id)
      if (!tab) throw new Error('no tab with id ' + id)
      if (tab.kind === 'pinned') throw new Error('The Pinned tab cannot be renamed.')
      const clean = cleanName(name, id)
      tabs = tabs.map((entry) => (entry.id === id ? { ...entry, name: clean } : entry))
      return wait(undefined)
    },
    deleteTab: async ({ id }) => {
      const tab = tabs.find((entry) => entry.id === id)
      if (!tab) throw new Error('no tab with id ' + id)
      if (tab.kind === 'pinned') throw new Error('The Pinned tab cannot be deleted.')
      const purged = clips.filter((clip) => clip.tabId === id).length
      clips = clips.filter((clip) => clip.tabId !== id)
      tabs = tabs.filter((entry) => entry.id !== id)
      return wait({ deleted: true, deletedClips: purged, movedToHistory: 0 })
    },
    hide: async () => wait(undefined),

    // The mock has no native toast: it only has to answer the shape the page expects.
    previewNotification: async () => {
      await wait(undefined)
      return { shown: true }
    },
    // Preview-only: the fixture writes nothing. It answers the same shape as the host so the
    // toast path can be seen in a browser; the real file is only ever written by Clip.Host.
    exportClip: async ({ id, format }) => {
      const clip = clips.find((entry) => entry.id === id)
      if (!clip) throw new Error('Clip not found')
      if (clip.kind === 'files') throw new Error('A file list is not a document, so there is nothing to save.')
      const ext =
        format !== 'auto'
          ? format
          : clip.hasImage
            ? 'png'
            : clip.tags.includes('spreadsheet')
              ? 'csv'
              : 'txt'
      const name = clip.title.replace(/[\\/:*?"<>|]/g, ' ').trim() || 'Clip'
      return wait({ saved: true, path: `C:\\Users\\Owner\\Documents\\${name}.${ext}` })
    },
    saveConfig: async (patch: SettingsPatch) => {
      const next: typeof settings = {
        ...settings,
        ...patch,
        notification: { ...settings.notification, ...(patch.notification ?? {}) },
        theme: patch.theme === 'dark' ? 'dark' : patch.theme === 'light' ? 'light' : settings.theme,
        themeChoice: patch.theme ?? settings.themeChoice,
        timeFormat: patch.timeFormat ?? settings.timeFormat,
      }
      // The one conflict the real host can answer: the plain-paste chord, which the fixture reserves.
      if (patch.hotkey && patch.hotkey.toUpperCase() === 'CTRL+SHIFT+V') {
        settings = next
        emit({ event: 'settingsSaved', data: settings })
        return wait({ config: settings, hotkeyError: `Could not register ${patch.hotkey}. Another app may own it; kept ${settings.hotkey}.` })
      }
      settings = next
      emit({ event: 'settingsSaved', data: settings })
      return wait({ config: settings, hotkeyError: null })
    },
    saveEditor: async ({ id, html, text }) => {
      const clip = clips.find((entry) => entry.id === id)
      if (!clip) return wait({ saved: false, error: 'That clip no longer exists.' })
      if (!html.trim()) return wait({ saved: false, error: 'The editor is empty, so nothing was saved.' })
      if (/<script/i.test(html)) return wait({ saved: false, error: 'The clip contains a script, so it was not saved.' })
      const body = realBodies.get(id)
      if (body) realBodies.set(id, { ...body, html, text, formats: [] })
      clips = clips.map((entry) => (entry.id === id ? { ...entry, text: text.slice(0, 1200), hasHtml: true, hasRtf: false, formats: [] } : entry))
      const dto = clips.find((entry) => entry.id === id)!
      emit({ event: 'edited', data: dto })
      return wait({ saved: true, htmlLength: html.length + 200 })
    },
    clearHistory: async () => {
      clips = clips.filter((clip) => clip.tabId !== null)
      emit({ event: 'cleared', data: {} })
      return wait(undefined)
    },
    onEvent: (listener) => {
      listeners.add(listener)
      return () => {
        listeners.delete(listener)
      }
    },
  }
}
