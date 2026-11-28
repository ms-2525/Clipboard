/**
 * Typed bridge to Clip.Host over WebView2 postMessage.
 *
 * Page -> host: {id, method, params}
 * Host -> page: {id, ok:true, result} | {id, ok:false, error} | {event, data}
 *
 * All clipboard writes go through the host; the page never touches payload data.
 */
import { createMockHost } from './mock'
import { toSettings, type HostConfig, type HostSettings, type SaveConfigResult, type SettingsPatch } from './settings'

export type ClipKind = 'text' | 'rich' | 'image' | 'files' | 'sheet'

/** List-row DTO. `text` is a preview (host truncates to 1200 chars). */
export interface ClipItem {
  id: number
  kind: ClipKind
  app: string
  title: string
  time: string
  pinned: boolean
  tabId: number | null
  text: string
  hasImage: boolean
  hasHtml: boolean
  hasRtf: boolean
  formats: string[]
  /** Canonical tag ids from Core\ClipTags.cs. The page renders these, never derives its own. */
  tags: string[]
  /** True when the row's preview is shorter than the stored clip. */
  truncated: boolean
  /** Characters the preview omits; 0 when the clip is complete. Drives the +N chip. */
  remainingChars: number
  /** Bytes the whole clip occupies (body + formats + image); 0 when the host could not read it. */
  sizeBytes: number
}

export interface HostTab {
  id: number
  name: string
  kind: 'pinned' | 'custom'
  count: number
}

export interface ListTabsResult {
  tabs: HostTab[]
  pinnedTabId: number
}

export interface DeleteTabResult {
  deleted: boolean
  deletedClips: number
  movedToHistory: number
}

export interface Counts {
  /** Main-history rows (tabId null): the host's own shape. */
  clips?: number
  images?: number
  /** Optional richer counts a later host build may add. */
  all?: number
  pinned?: number
  today?: number
}

export type { HostConfig, HostSettings, SettingsPatch, SaveConfigResult } from './settings'
export { toSettings } from './settings'

export interface EditorSaveResult {
  saved: boolean
  htmlLength?: number
  error?: string
}

export interface ReOcrResult {
  text: string
  words: number
}

export interface InitResult {
  counts: Counts
  tabs: HostTab[]
  config: HostSettings
  items: ClipItem[]
}
export interface ItemsQuery {
  tabId?: number | null
  offset?: number
  limit?: number
  query?: string
}

/** `search` answers over the whole history; `total` is every hit, not the page. */
export interface SearchResult {
  total: number
  items: ClipItem[]
  /** Tag names the query used that Core does not know; they were searched as text. */
  unknownTags: string[]
}

/** getItem returns the full body, not a list row. */
export interface ClipBody {
  text: string | null
  html: string | null
  rtf: string | null
  formats: string[]
}

export interface HostEvent {
  event: string
  data?: unknown
}

/** Paste can answer `ok` and still report that nothing was injected (no target app). */
export interface PasteResult {
  pasted: boolean
  error?: string
}

/** `export` answers a cancel without an error; the cancellation reason lives only in `cancelled`. */
export interface ExportResult {
  saved: boolean
  cancelled?: boolean
  path?: string
}

/** `export` with launch:true: the editor's "Open in app" — a temp file the OS opens. */
export interface OpenInAppResult {
  launched: boolean
  path?: string
}

export interface Host {
  readonly mode: 'host' | 'mock'
  init(): Promise<InitResult>
  getItems(query?: ItemsQuery): Promise<ClipItem[]>
  search(query: ItemsQuery & { query: string }): Promise<SearchResult>
  getItem(id: number): Promise<ClipBody | null>
  getThumb(id: number): Promise<string | null>
  /** Full-resolution PNG for an image row and the F2 editor; absent on older hosts. */
  getImage?(id: number): Promise<string | null>
  copy(params: { id: number; plain?: boolean }): Promise<void>
  paste(params: { id: number; plain?: boolean }): Promise<PasteResult>
  delete(params: { id: number }): Promise<void>
  restore(params: { id: number }): Promise<void>
  togglePin(params: { id: number }): Promise<void>
  moveToTab(params: { id: number; tabId: number | null }): Promise<void>
  listTabs(): Promise<ListTabsResult>
  createTab(params: { name: string }): Promise<HostTab>
  renameTab(params: { id: number; name: string }): Promise<void>
  deleteTab(params: { id: number }): Promise<DeleteTabResult>
  /** Persists the custom-tab strip order after a drag; Pinned stays first on the host. */
  reorderTabs(params: { order: number[] }): Promise<void>
  hide(): Promise<void>
  /** Shows the real native toast at its real coordinates: the Settings preview. */
  previewNotification(): Promise<{ shown: boolean }>
  exportClip(params: { id: number; format: string }): Promise<ExportResult>
  openInApp(params: { id: number }): Promise<OpenInAppResult>
  saveConfig(patch: SettingsPatch): Promise<SaveConfigResult>
  /** The same settings shape read fresh; the dialog asks on open so live state (login start) is current. */
  getConfig(): Promise<HostSettings>
  saveEditor(params: { id: number; html: string; text: string }): Promise<EditorSaveResult>
  reOcr(params: { id: number }): Promise<ReOcrResult>
  clearHistory(): Promise<void>
  /** Current clip.db (+ WAL) bytes and the advisory limit, for the Settings readout. */
  storageUsage(): Promise<{ bytes: number; limit: number }>
  onEvent(listener: (event: HostEvent) => void): () => void
}

interface WebView2Bridge {
  postMessage(message: unknown): void
  addEventListener(type: 'message', handler: (event: { data: unknown }) => void): void
  removeEventListener(type: 'message', handler: (event: { data: unknown }) => void): void
}

declare global {
  interface Window {
    chrome?: { webview?: WebView2Bridge }
  }
}

interface Envelope {
  id?: number
  ok?: boolean
  result?: unknown
  error?: string
  event?: string
  data?: unknown
}

/** Clip.Host caps a page at 500 rows; the client pages up to the requested limit. */
interface PageResult {
  total?: number
  items?: ClipItem[]
}

const MAX_PAGE = 500
const REQUEST_TIMEOUT_MS = 15000

/** The host's own message, or the transport's; shown verbatim where the host wrote it for a person. */
export const messageOf = (error: unknown) => (error instanceof Error ? error.message : String(error))

/** The store calls the built-in tab "builtin"; the page's contract has one pinned kind. */
function normalizeTab(tab: HostTab): HostTab {
  return { ...tab, kind: tab.kind === 'custom' ? 'custom' : 'pinned' }
}

/** Host rows can carry nulls for app/title/text; keep every consumer string-safe. */
function normalizeItem(item: ClipItem): ClipItem {
  return {
    ...item,
    kind: item.kind ?? 'text',
    app: item.app || 'Unknown',
    title: item.title || '',
    time: item.time || '',
    tabId: item.tabId ?? null,
    text: item.text || '',
    formats: item.formats ?? [],
    tags: Array.isArray(item.tags) ? item.tags : [],
    truncated: Boolean(item.truncated),
    remainingChars: Math.max(0, item.remainingChars ?? 0),
    sizeBytes: Math.max(0, item.sizeBytes ?? 0),
    pinned: Boolean(item.pinned),
    hasImage: Boolean(item.hasImage),
    hasHtml: Boolean(item.hasHtml),
    hasRtf: Boolean(item.hasRtf),
  }
}

class WebViewHost implements Host {
  readonly mode = 'host' as const
  private seq = 0
  private pending = new Map<
    number,
    { resolve: (value: unknown) => void; reject: (error: Error) => void; timer: number }
  >()
  private listeners = new Set<(event: HostEvent) => void>()

  constructor(private bridge: WebView2Bridge) {
    bridge.addEventListener('message', this.onMessage)
  }

  private onMessage = (event: { data: unknown }) => {
    let message: Envelope
    try {
      message = (typeof event.data === 'string' ? JSON.parse(event.data) : event.data) as Envelope
    } catch {
      return
    }
    if (!message || typeof message !== 'object') return

    if (typeof message.event === 'string') {
      // A captured row is the same DTO as a list row; normalize it once here so the page
      // can prepend it without a second shape to defend against.
      const data =
        message.event === 'captured' || message.event === 'edited'
          ? normalizeItem(message.data as ClipItem)
          : message.data
      for (const listener of this.listeners) listener({ event: message.event, data })
      return
    }

    if (typeof message.id !== 'number') return
    const waiting = this.pending.get(message.id)
    if (!waiting) return
    window.clearTimeout(waiting.timer)
    this.pending.delete(message.id)
    if (message.ok === false) waiting.reject(new Error(message.error || 'Clip host request failed'))
    else waiting.resolve(message.result)
  }

  private send<T>(method: string, params: unknown = {}, timeoutMs = REQUEST_TIMEOUT_MS): Promise<T> {
    const id = ++this.seq
    return new Promise<T>((resolve, reject) => {
      const timer = window.setTimeout(() => {
        this.pending.delete(id)
        reject(new Error(`Clip host did not answer ${method}`))
      }, timeoutMs)
      this.pending.set(id, { resolve: resolve as (value: unknown) => void, reject, timer })
      this.bridge.postMessage({ id, method, params })
    })
  }

  init = async (): Promise<InitResult> => {
    const result = await this.send<{ counts?: Counts; tabs?: HostTab[]; config?: HostConfig; items?: ClipItem[] }>('init')
    return {
      counts: result?.counts ?? {},
      tabs: (result?.tabs ?? []).map(normalizeTab),
      config: toSettings(result?.config),
      items: (result?.items ?? []).map(normalizeItem),
    }
  }

  getItems = async (query: ItemsQuery = {}): Promise<ClipItem[]> => {
    const start = query.offset ?? 0
    const limit = query.limit ?? 1000
    const rows: ClipItem[] = []
    while (rows.length < limit) {
      const take = Math.min(MAX_PAGE, limit - rows.length)
      const page = await this.send<PageResult | ClipItem[]>('getItems', {
        tabId: query.tabId ?? null,
        offset: start + rows.length,
        limit: take,
        query: query.query ?? '',
      })
      const items = Array.isArray(page) ? page : page?.items ?? []
      rows.push(...items)
      if (items.length < take) break
    }
    return rows.map(normalizeItem)
  }

  getItem = (id: number) => this.send<ClipBody | null>('getItem', { id })

  search = async (query: ItemsQuery & { query: string }): Promise<SearchResult> => {
    const result = await this.send<{ total?: number; items?: ClipItem[]; unknownTags?: string[] }>('search', {
      tabId: query.tabId ?? null,
      offset: query.offset ?? 0,
      limit: query.limit ?? 500,
      query: query.query,
    })
    return {
      total: result?.total ?? 0,
      items: (result?.items ?? []).map(normalizeItem),
      unknownTags: result?.unknownTags ?? [],
    }
  }

  getThumb = async (id: number): Promise<string | null> => {
    const result = await this.send<unknown>('getThumb', { id })
    const dataUrl =
      typeof result === 'string'
        ? result
        : result && typeof result === 'object'
          ? (result as { dataUrl?: unknown }).dataUrl
          : null
    if (typeof dataUrl !== 'string' || !dataUrl) return null
    return dataUrl.startsWith('data:') ? dataUrl : `data:image/png;base64,${dataUrl}`
  }

  getImage = async (id: number): Promise<string | null> => {
    const result = await this.send<unknown>('getImage', { id })
    const dataUrl =
      typeof result === 'string'
        ? result
        : result && typeof result === 'object'
          ? (result as { dataUrl?: unknown }).dataUrl
          : null
    if (typeof dataUrl !== 'string' || !dataUrl) return null
    return dataUrl.startsWith('data:') ? dataUrl : `data:image/png;base64,${dataUrl}`
  }

  copy = async (params: { id: number; plain?: boolean }) => {
    await this.send('copy', { id: params.id, plain: params.plain ?? false })
  }

  paste = async (params: { id: number; plain?: boolean }): Promise<PasteResult> => {
    const result = await this.send<{ pasted?: boolean; error?: string } | null>('paste', {
      id: params.id,
      plain: params.plain ?? false,
    })
    // A host that answers without the flag still ran the command; only an explicit false fails.
    return { pasted: result?.pasted !== false, error: result?.error }
  }

  delete = async (params: { id: number }) => {
    await this.send('delete', { id: params.id })
  }

  restore = async (params: { id: number }) => {
    await this.send('restore', { id: params.id })
  }

  togglePin = async (params: { id: number }) => {
    await this.send('togglePin', { id: params.id })
  }

  moveToTab = async (params: { id: number; tabId: number | null }) => {
    await this.send('moveToTab', { id: params.id, tabId: params.tabId })
  }

  listTabs = async (): Promise<ListTabsResult> => {
    const result = await this.send<{ tabs?: HostTab[]; pinnedTabId?: number }>('listTabs')
    return { tabs: (result?.tabs ?? []).map(normalizeTab), pinnedTabId: result?.pinnedTabId ?? 0 }
  }

  createTab = async (params: { name: string }): Promise<HostTab> =>
    normalizeTab(await this.send<HostTab>('createTab', { name: params.name }))

  renameTab = async (params: { id: number; name: string }) => {
    await this.send('renameTab', { id: params.id, name: params.name })
  }

  deleteTab = async (params: { id: number }): Promise<DeleteTabResult> => {
    const result = await this.send<DeleteTabResult | null>('deleteTab', { id: params.id })
    return {
      deleted: result?.deleted === true,
      deletedClips: Math.max(0, result?.deletedClips ?? 0),
      movedToHistory: Math.max(0, result?.movedToHistory ?? 0),
    }
  }

  reorderTabs = async (params: { order: number[] }) => {
    await this.send('reorderTabs', { order: params.order })
  }

  hide = async () => {
    await this.send('hide')
  }

  /** Shows the real native toast at its real coordinates: the Settings preview. */
  previewNotification = async (): Promise<{ shown: boolean }> => {
    const result = await this.send<{ shown?: boolean }>('previewNotification')
    return { shown: result?.shown === true }
  }

  exportClip = async (params: { id: number; format: string }) => {
    // The owner types in the Save dialog before this answers; the ordinary request timeout is
    // far shorter than a person choosing a name.
    const result = await this.send<ExportResult | null>('export', { id: params.id, format: params.format }, 10 * 60 * 1000)
    // A host that answers without the flag saved nothing; never claim a file we did not confirm.
    return { saved: result?.saved === true, cancelled: result?.cancelled === true, path: result?.path }
  }

  openInApp = async (params: { id: number }): Promise<OpenInAppResult> => {
    // Same bytes Export would write, but to a temp file the OS opens: no dialog, no long timeout.
    const result = await this.send<OpenInAppResult | null>('export', { id: params.id, format: 'auto', launch: true })
    return { launched: result?.launched === true, path: result?.path }
  }

  saveConfig = async (patch: SettingsPatch): Promise<SaveConfigResult> => {
    const result = await this.send<{ config?: HostConfig; hotkeyError?: string | null }>('saveConfig', { patch })
    return { config: toSettings(result?.config), hotkeyError: result?.hotkeyError ?? null }
  }

  getConfig = async (): Promise<HostSettings> => {
    const result = await this.send<{ config?: HostConfig }>('getConfig')
    return toSettings(result?.config)
  }

  saveEditor = async (params: { id: number; html: string; text: string }): Promise<EditorSaveResult> => {
    const result = await this.send<EditorSaveResult | null>('saveEditor', params)
    // A host that answers without the flag did not save; never claim a write the host did not confirm.
    return { saved: result?.saved === true, htmlLength: result?.htmlLength, error: result?.error }
  }

  reOcr = async (params: { id: number }): Promise<ReOcrResult> => {
    const result = await this.send<{ text?: unknown; words?: unknown } | null>('reOcr', params)
    if (typeof result?.text !== 'string') throw new Error('Text recognition did not return any text.')
    return { text: result.text, words: typeof result.words === 'number' ? result.words : 0 }
  }

  clearHistory = async () => {
    await this.send('clearHistory')
  }

  storageUsage = async (): Promise<{ bytes: number; limit: number }> => {
    const result = await this.send<{ bytes?: number; limit?: number }>('storageUsage')
    return { bytes: Math.max(0, result?.bytes ?? 0), limit: Math.max(1, result?.limit ?? 0) }
  }

  onEvent = (listener: (event: HostEvent) => void) => {
    this.listeners.add(listener)
    return () => {
      this.listeners.delete(listener)
    }
  }
}

export function createHost(): Host {
  // Inside WebView2 the host is always authoritative: never mock a real user's history.
  const bridge = typeof window !== 'undefined' ? window.chrome?.webview : undefined
  if (bridge) return new WebViewHost(bridge)
  return createMockHost()
}
