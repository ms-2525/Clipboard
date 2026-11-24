/**
 * Typed bridge to Clip.Host over WebView2 postMessage.
 *
 * Page -> host: {id, method, params}
 * Host -> page: {id, ok:true, result} | {id, ok:false, error} | {event, data}
 *
 * All clipboard writes go through the host; the page never touches payload data.
 */
import { createMockHost } from './mock'

export type ClipKind = 'text' | 'rich' | 'image' | 'files'

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
}

export interface HostTab {
  id: number
  name: string
  kind: 'pinned' | 'custom'
  count: number
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

export interface HostConfig {
  theme?: 'light' | 'dark'
  /** "24h" (default) or "12h"; the page formats list timestamps. */
  timeFormat?: string
  [key: string]: unknown
}

export interface InitResult {
  counts: Counts
  tabs: HostTab[]
  config: HostConfig
  items: ClipItem[]
}

export interface ItemsQuery {
  tabId?: number | null
  offset?: number
  limit?: number
  query?: string
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

export interface Host {
  readonly mode: 'host' | 'mock'
  init(): Promise<InitResult>
  getItems(query?: ItemsQuery): Promise<ClipItem[]>
  getItem(id: number): Promise<ClipBody | null>
  getThumb(id: number): Promise<string | null>
  copy(params: { id: number; plain?: boolean }): Promise<void>
  paste(params: { id: number; plain?: boolean }): Promise<void>
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
      for (const listener of this.listeners) listener({ event: message.event, data: message.data })
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

  private send<T>(method: string, params: unknown = {}): Promise<T> {
    const id = ++this.seq
    return new Promise<T>((resolve, reject) => {
      const timer = window.setTimeout(() => {
        this.pending.delete(id)
        reject(new Error(`Clip host did not answer ${method}`))
      }, REQUEST_TIMEOUT_MS)
      this.pending.set(id, { resolve: resolve as (value: unknown) => void, reject, timer })
      this.bridge.postMessage({ id, method, params })
    })
  }

  init = async (): Promise<InitResult> => {
    const result = await this.send<InitResult>('init')
    return {
      counts: result.counts ?? {},
      tabs: result.tabs ?? [],
      config: result.config ?? {},
      items: (result.items ?? []).map(normalizeItem),
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

  copy = async (params: { id: number; plain?: boolean }) => {
    await this.send('copy', { id: params.id, plain: params.plain ?? false })
  }

  paste = async (params: { id: number; plain?: boolean }) => {
    await this.send('paste', { id: params.id, plain: params.plain ?? false })
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
