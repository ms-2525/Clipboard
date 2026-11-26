import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import * as Tooltip from '@radix-ui/react-tooltip'
import { ClipList, type ClipListHandle } from './ClipList'
import { DeleteConfirm } from './DeleteConfirm'
import { EditorOverlay } from './EditorOverlay'
import { MovePicker, moveTargets } from './MoveToTab'
import { RowMenu } from './RowMenu'
import { ClipInfo, StorageNotice } from './ClipInfo'
import { SettingsDialog } from './SettingsDialog'
import { TagPicker, canonicalTag, clearTagTokens, tagToken, TAG_ALIASES, TAG_VOCAB } from './TagPicker'
import { TabStrip, type StripTab } from './TabStrip'
import { createHost, messageOf, toSettings, type ClipItem, type HostConfig, type HostSettings, type HostTab, type InitResult, type SaveConfigResult, type SearchResult, type SettingsPatch } from './host'
import { useClipboardKeys } from './useClipboardKeys'
import { forgetBody } from './rich'
import type { TimeFormat } from './format'
import {
  IconCheck,
  IconClipboard,
  IconClose,
  IconMoon,
  IconSearch,
  IconSettings,
  IconSun,
  IconWarning,
} from './icons'

const params = new URLSearchParams(window.location.search)

interface Toast {
  id: number
  title: string
  sub: string
  error: boolean
  /** Full text for the native tooltip (an export's absolute path does not fit the toast). */
  hint?: string
}

/** Formats the host accepts for this clip; a file list has none, because the host refuses it. */
function exportOptions(clip: ClipItem | null): { format: string; label: string }[] {
  if (!clip || clip.kind === 'files') return []
  if (clip.hasImage) return [{ format: 'png', label: 'PNG (.png)' }]
  if (clip.tags.includes('spreadsheet')) return [{ format: 'csv', label: 'CSV (.csv)' }]
  return [
    { format: 'txt', label: 'Text (.txt)' },
    { format: 'md', label: 'Markdown (.md)' },
  ]
}

/** The last search answer, kept while the next one is pending so the rows never flash blank. */
interface SearchView extends SearchResult {
  query: string
}

/** How many clips the Inbox tab can show; the host's richer `all` wins over the raw clips/images pair. */
function allCountOf(init: InitResult): number {
  const counts = init.counts
  if (typeof counts.all === 'number') return counts.all
  if (typeof counts.clips === 'number' || typeof counts.images === 'number') return (counts.clips ?? 0) + (counts.images ?? 0)
  return init.items?.length ?? 0
}

/** The next-slice bridge methods answer "unknown method"; that is a gap, not a failure. */
const isNotReady = (error: unknown) => /unknown method|not wired|not ready|phase 2/i.test(messageOf(error))
/** The virtual Images tab's id; never a DB tab id (the store starts at 1). */
const IMAGES_TAB = -1

/** Cache slot for a tab's last-known unfiltered rows: Inbox is `all`, customs/pinned by id. */
const cacheKeyOf = (tabId: number | null) => (tabId == null ? 'all' : `tab:${tabId}`)

/** Local calendar day of a row's time: the boundary Ctrl+Up/Down walks. */
const dayKey = (time: string) => {
  const date = new Date(time)
  return Number.isNaN(date.getTime()) ? '' : `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`
}

/** Indices where a new day starts; the rows are newest first. */
function sectionStarts(rows: ClipItem[]): number[] {
  const starts: number[] = []
  let previous = ''
  rows.forEach((row, index) => {
    const key = dayKey(row.time)
    if (index === 0 || key !== previous) starts.push(index)
    previous = key
  })
  return starts
}

/** A re-copied clip keeps its id; the store moves it to the top, so the row must move too. */
function moveToTop(rows: ClipItem[], item: ClipItem): ClipItem[] {
  const at = rows.findIndex((row) => row.id === item.id)
  // A reorder notification may carry only the id; a full row carries a time and every field.
  const merged = at >= 0 ? (item.time ? { ...rows[at], ...item } : rows[at]) : item.time ? item : null
  if (!merged) return rows
  if (at < 0) return [merged, ...rows]
  if (at === 0) return [merged, ...rows.slice(1)]
  return [merged, ...rows.slice(0, at), ...rows.slice(at + 1)]
}

export default function App() {
  const [host] = useState(createHost)
  const previewUi = host.mode === 'mock'

  const [theme, setTheme] = useState<'light' | 'dark'>(() => (previewUi && params.get('theme') === 'dark' ? 'dark' : 'light'))
  const [settings, setSettings] = useState<HostSettings>(() =>
    toSettings({ theme: previewUi && params.get('theme') === 'dark' ? 'dark' : 'light' }),
  )
  const [tab, setTab] = useState<number | null>(null)
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [searchOpen, setSearchOpen] = useState(true)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  /** Shift+Up/Down range: the anchor row plus every id between anchor and cursor. Empty = single cursor. */
  const [anchorId, setAnchorId] = useState<number | null>(null)
  const [selectedIds, setSelectedIds] = useState<number[]>([])
  const [settingsOpen, setSettingsOpen] = useState(previewUi && params.get('surface') === 'settings')
  const [editorOpen, setEditorOpen] = useState(previewUi && params.get('surface') === 'editor')
  const [confirm, setConfirm] = useState<ClipItem[] | null>(null)
  const [toasts, setToasts] = useState<Toast[]>([])
  const [init, setInit] = useState<InitResult | null>(null)
  const [tabRows, setTabRows] = useState<HostTab[]>([])
  const [allCount, setAllCount] = useState(0)
  /** The virtual Images tab: it shows `tag:image` over Inbox and is never stored as a DB tab. */
  const [images, setImages] = useState(false)
  const [imageCount, setImageCount] = useState(0)
  const [tabOverlay, setTabOverlay] = useState(false)
  const [rowMenu, setRowMenu] = useState<{ clip: ClipItem; x: number; y: number } | null>(null)
  const [moveClip, setMoveClip] = useState<ClipItem | null>(null)
  const [tagClip, setTagClip] = useState<ClipItem | null>(null)
  const [infoClip, setInfoClip] = useState<ClipItem | null>(null)
  /** Host "storage" event payload; shown once per run, then muted until the page reloads. */
  const [storageNotice, setStorageNotice] = useState<{ bytes: number; limit: number } | null>(null)
  const [items, setItems] = useState<ClipItem[]>([])
  const [found, setFound] = useState<SearchView | null>(null)
  const [searching, setSearching] = useState(false)
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [loadError, setLoadError] = useState('')
  /** `tag:` dropdown: highlighted row plus whole-library counts, snapshotted when it opens. */
  const [suggestIndex, setSuggestIndex] = useState(0)
  const [tagCounts, setTagCounts] = useState<Record<string, number> | null>(null)
  /** The dropdown only lives while the caret is in the box: a tab click must never pop it. */
  const [searchFocused, setSearchFocused] = useState(false)

  const toastId = useRef(0)
  const tabSeq = useRef(0)
  const searchSeq = useRef(0)
  const lastQuery = useRef('')
  const lastDeleted = useRef<{ clip: ClipItem; index: number }[] | null>(null)
  const searchRef = useRef<HTMLInputElement>(null)
  const searchTyped = useRef(false)
  const listRef = useRef<ClipListHandle>(null)
  const settingsReturn = useRef<HTMLElement | null>(null)
  const confirmReturn = useRef<HTMLElement | null>(null)
  const moveReturn = useRef<HTMLElement | null>(null)
  const tagReturn = useRef<HTMLElement | null>(null)
  const infoReturn = useRef<HTMLElement | null>(null)
  const storageShown = useRef(false)
  /** Per-tab last-known unfiltered rows: a switch shows these at once, then refreshes underneath. */
  const itemsCache = useRef(new Map<string, ClipItem[]>())
  /** The Images tab's last `tag:image` answer, for the same instant switch. */
  const imagesFound = useRef<SearchView | null>(null)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
  }, [theme])

  const showToast = useCallback((title: string, sub: string, error = false, hint?: string) => {
    const id = ++toastId.current
    setToasts((current) => [...current.slice(-2), { id, title, sub, error, hint }])
    window.setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), 2600)
  }, [])

  /** One place where a host config becomes live page state: theme, time format, list cap, zoom. */
  const applySettings = useCallback((next: HostSettings) => {
    setSettings(next)
    setTheme(next.theme)
  }, [])

  /** listTabs is the only live tab source: the host emits no tabs event. */
  const refreshTabs = useCallback(async () => {
    try {
      const result = await host.listTabs()
      setTabRows(result.tabs)
    } catch (error) {
      showToast('Could not refresh tabs', messageOf(error), true)
    }
    // Counts are live, not seeded once: the page mounts before the first capture, so a seeded All
    // count would read 0 on a non-empty history forever. An empty query is the whole history.
    try {
      const [all, images] = await Promise.all([
        host.search({ query: '', tabId: null, offset: 0, limit: 1 }),
        host.search({ query: 'tag:image', tabId: null, offset: 0, limit: 1 }),
      ])
      setAllCount(all.total)
      setImageCount(images.total)
    } catch {
      // A count is a hint; the list itself still works if this call fails.
    }
  }, [host, showToast])

  const load = useCallback(() => {
    const seq = ++tabSeq.current
    // A cleared history wipes every row: cached pages from before it are lies.
    itemsCache.current.clear()
    imagesFound.current = null
    setStatus('loading')
    setLoadError('')
    host
      .init()
      .then((result) => {
        const rows = result.items ?? []
        setInit(result)
        setTabRows(result.tabs ?? [])
        setAllCount(allCountOf(result))
        setItems(rows)
        setSelectedId((current) => (current != null && rows.some((row) => row.id === current) ? current : rows[0]?.id ?? null))
        applySettings(result.config)
        setStatus('ready')
        void refreshTabs()

        // init carries the newest page (the host caps it); deepen the All list to 1,000.
        if (seq !== tabSeq.current) return
        host
          .getItems({ tabId: null, offset: 0, limit: 1000 })
          .then((all) => {
            if (seq !== tabSeq.current || all.length === 0) return
            setItems(all)
            setSelectedId((current) => (current != null && all.some((row) => row.id === current) ? current : all[0]?.id ?? null))
          })
          .catch(() => {
            // init's first page is already on screen; the deeper read only extends history.
          })

        // Warm the other tabs + Images so the first switch shows cached rows at once
        // instead of flashing. Same limits as a real switch, so the cache matches it.
        window.setTimeout(() => {
          void (async () => {
            try {
              for (const entry of result.tabs ?? []) {
                const key = cacheKeyOf(entry.id)
                if (itemsCache.current.has(key)) continue
                const rows = await host.getItems({ tabId: entry.id, offset: 0, limit: 1000 })
                if (rows.length > 0) itemsCache.current.set(key, rows)
              }
              if (!imagesFound.current) {
                const found = await host.search({ query: 'tag:image', tabId: null, limit: 500 })
                imagesFound.current = { query: 'tag:image', ...found }
              }
            } catch {
              // Warming fails silently: the switch just fetches as before.
            }
          })()
        }, 600)
      })
      .catch((error) => {
        setLoadError(messageOf(error))
        setStatus('error')
      })
  }, [applySettings, host, refreshTabs])

  useEffect(() => {
    load()
  }, [load])

  const focusSearch = useCallback(() => {
    setSearchOpen(true)
    window.setTimeout(() => {
      const input = searchRef.current
      input?.focus()
      input?.setSelectionRange(input.value.length, input.value.length)
    }, 0)
  }, [])

  /** Only text the owner typed rides along to the next tab; tag chips and clears do not. */
  const setSearchText = useCallback((value: string) => {
    searchTyped.current = value.trim().length > 0
    setSearch(value)
    // Typing or clearing in the search box leaves the virtual Images tab; it becomes a normal search.
    setImages(false)
  }, [])
  const setSearchProgrammatic = useCallback((value: string) => {
    searchTyped.current = false
    setSearch(value)
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search.trim()), 120)
    return () => window.clearTimeout(timer)
  }, [search])

  // Search is a query over the whole history, not a filter of the loaded page.
  useEffect(() => {
    if (!query) {
      searchSeq.current++
      setSearching(false)
      if (!lastQuery.current) return
      lastQuery.current = ''
      setFound(null)
      const seq = ++tabSeq.current
      host
        .getItems({ tabId: tab, offset: 0, limit: 1000 })
        .then((rows) => {
          if (seq !== tabSeq.current) return
          setItems(rows)
          setSelectedId((current) => (current != null && rows.some((row) => row.id === current) ? current : rows[0]?.id ?? null))
        })
        .catch((error) => {
          if (seq === tabSeq.current) showToast('Could not reload history', messageOf(error), true)
        })
      return
    }

    lastQuery.current = query
    const seq = ++searchSeq.current
    setSearching(true)
    host
      .search({ query, tabId: tab, limit: 500 })
      .then((result) => {
        if (seq !== searchSeq.current) return
        setFound({ query, ...result })
        // Focus is selection: a hit list with no selected row cannot be pasted or edited by keyboard.
        setSelectedId((current) =>
          current != null && result.items.some((row) => row.id === current) ? current : (result.items[0]?.id ?? null),
        )
        setSearching(false)
      })
      .catch((error) => {
        if (seq !== searchSeq.current) return
        setSearching(false)
        setFound({ query, items: [], total: 0, unknownTags: [] })
        showToast('Search failed', messageOf(error), true)
      })
  }, [query, tab, host, showToast])

  const selectTab = useCallback(
    (next: number | null) => {
      const previous = tab
      setImages(false)
      // A typed query survives a tab change; a programmatic one (a tag chip) does not.
      const keepQuery = searchTyped.current && (search.trim() !== '' || query !== '')
      if (!searchTyped.current && (search || query)) {
        lastQuery.current = ''
        setSearch('')
        setQuery('')
      }
      setTab(next)
      setFound(null)
      searchSeq.current++
      // A surviving typed query owns the list: the query effect searches the new tab,
      // so an unfiltered fetch here would flash the wrong rows first.
      if (keepQuery) {
        // No cache for an arbitrary query+tab: clear so the old tab never shows as the new one.
        setItems([])
        setSearching(true)
        void refreshTabs()
        return
      }
      // Seamless switch: the tab's last-known rows appear at once (no blank flash),
      // then the fetch below refreshes them underneath.
      const cached = itemsCache.current.get(cacheKeyOf(next))
      if (cached) {
        setItems(cached)
        setSelectedId((current) => (current != null && cached.some((row) => row.id === current) ? current : cached[0]?.id ?? null))
      } else {
        // First visit: nothing to show yet, so the old tab's rows must go now rather
        // than flash inside the new one.
        setItems([])
      }
      const seq = ++tabSeq.current
      host
        .getItems({ tabId: next, offset: 0, limit: 1000 })
        .then((rows) => {
          if (seq !== tabSeq.current) return
          setItems(rows)
          setSelectedId((current) => (current != null && rows.some((row) => row.id === current) ? current : rows[0]?.id ?? null))
        })
        .catch((error) => {
          if (seq !== tabSeq.current) return
          setTab(previous)
          showToast('Could not switch tab', messageOf(error), true)
        })
      void refreshTabs()
    },
    [host, query, refreshTabs, search, showToast, tab],
  )

  const showImagesInAll = settings.showImagesInAll !== false

  const tabs = useMemo(() => {
    // Hidden images still belong to the Images tab; the Inbox count drops them so the strip says what it shows.
    const all = showImagesInAll ? allCount : Math.max(0, allCount - imageCount)
    const imagesTab: StripTab = { id: IMAGES_TAB, label: 'Images', count: imageCount, kind: 'images' }
    // Fixed heads: Inbox, then the Images view, then Pinned, then the custom tabs in host order.
    const rows: StripTab[] = [
      { id: null, label: 'Inbox', count: all, kind: 'all' },
      imagesTab,
    ]
    for (const hostTab of tabRows) {
      rows.push({ id: hostTab.id, label: hostTab.kind === 'pinned' ? 'Pinned' : hostTab.name, count: hostTab.count, kind: hostTab.kind })
    }
    return rows
  }, [tabRows, allCount, imageCount, showImagesInAll])

  /** The Images tab is the saved `tag:image` view over All; it never becomes a DB tab. */
  const selectImages = useCallback(() => {
    setImages(true)
    setTab(null)
    // Same instant switch as a tab: the last answer shows at once, then the search refreshes it.
    const cached = imagesFound.current
    if (!cached) {
      // Same stale-row flash as a tab switch: the Inbox rows (with their images) must go
      // now, not when the `tag:image` search answers.
      setItems([])
      setFound(null)
      setSearching(true)
    } else {
      setFound(cached)
    }
    searchSeq.current++
    lastQuery.current = ''
    // Trailing space: the tag is done, so the `tag:` list stays closed.
    setSearchProgrammatic('tag:image ')
  }, [setSearchProgrammatic])

  const chooseTab = useCallback(
    (id: number | null) => {
      if (id === IMAGES_TAB) selectImages()
      else selectTab(id)
    },
    [selectImages, selectTab],
  )

  /** Left/Right: Inbox, Images, Pinned, then the custom tabs; wraps so the strip is one cycle. */
  const cycleTab = useCallback(
    (direction: 1 | -1) => {
      if (tabs.length < 2) return
      const activeId = images ? IMAGES_TAB : tab
      const at = tabs.findIndex((entry) => entry.id === activeId)
      const next = tabs[(Math.max(0, at) + direction + tabs.length) % tabs.length]
      if (next) chooseTab(next.id)
    },
    [chooseTab, images, tab, tabs],
  )
  const moveDestinations = useMemo(() => moveTargets(tabRows), [tabRows])

  // The Inbox list hides image clips when the setting is off (the Images tab keeps them);
  // shortcut: hidden images still occupy the loaded page, so a full page can show fewer rows.
  const searched = found?.items ?? items
  const visible = !showImagesInAll && tab === null && !images
    ? searched.filter((clip) => clip.kind !== 'image' && !clip.hasImage)
    : searched
  const visibleRef = useRef(visible)
  visibleRef.current = visible
  const itemsRef = useRef(items)
  itemsRef.current = items

  // Write-through caches: every rows update refreshes the current view's last-known page,
  // so capture / delete / move / reload all stay seamless without touching each path.
  // Empty pages never overwrite: a genuinely empty tab just fetches again next visit.
  useEffect(() => {
    if (images || items.length === 0) return
    itemsCache.current.set(cacheKeyOf(tab), items)
  }, [items, images, tab])
  useEffect(() => {
    if (found?.query === 'tag:image' && tab === null) imagesFound.current = found
  }, [found, tab])

  useEffect(() => {
    if (selectedId != null && visible.some((clip) => clip.id === selectedId)) return
    setAnchorId(null)
    setSelectedIds([])
    setSelectedId(visible[0]?.id ?? null)
  }, [visible, selectedId])

  // A filter or tab switch drops rows the range named; keep only what is still on screen.
  useEffect(() => {
    if (selectedIds.length === 0 && anchorId == null) return
    const keep = new Set(visible.map((clip) => clip.id))
    if (selectedIds.length > 0 && !selectedIds.every((id) => keep.has(id))) {
      setSelectedIds((current) => current.filter((id) => keep.has(id)))
    }
    if (anchorId != null && !keep.has(anchorId)) setAnchorId(null)
  }, [visible, selectedIds, anchorId])

  const selected = visible.find((clip) => clip.id === selectedId) ?? null
  const activeTab = tabs.find((entry) => entry.id === tab)
  const tabLabel = images ? 'Images' : (activeTab?.label ?? 'Inbox')
  const tabCount = images ? (found?.query === 'tag:image' ? found.total : imageCount) : (activeTab?.count ?? visible.length)
  // Every row in the Pinned tab is pinned; repeating the glyph there is noise.
  const pinnedTab = tab != null && tabs.some((entry) => entry.id === tab && entry.kind === 'pinned')
  const today = init?.counts?.today
  const timeFormat: TimeFormat = settings.timeFormat === '12h' ? '12h' : '24h'
  const searchActive = query !== ''
  const activeSearch = found?.query === query ? found : null
  const emptyTitle =
    searching && visible.length === 0
      ? 'Searching…'
      : images
        ? 'No images yet'
        : searchActive
          ? `Nothing matches “${query}”`
          : tab == null
            ? 'No clips yet'
            : activeTab?.kind === 'pinned'
              ? 'Nothing pinned'
              : `Nothing in “${tabLabel}”`
  const emptySub =
    searching && visible.length === 0
      ? 'The new tab is loading.'
      : images
        ? 'Copy a screenshot or a picture and it will show up here.'
        : searchActive
          ? 'Try a different search, or clear it with Esc.'
          : tab == null
        ? 'Copy something and it will appear here.'
        : activeTab?.kind === 'pinned'
          ? 'Press Ctrl+P on a clip to keep it here.'
          : 'Right-click a clip and move it here.'

  /** The `tag:` fragment being typed, or null when no dropdown may show (Q9 one-tag lock). */
  const tagFrag = useMemo(() => {
    const at = search.search(/tag:/i)
    if (at < 0) return null
    const after = search.slice(at)
    if (/tag:/i.test(after.slice(4))) return null // a second `tag:` never opens (Q9)
    const match = /^tag:([A-Za-z0-9_-]*)(.*)$/i.exec(after)
    if (!match) return null
    const frag = match[1]
    const rest = match[2]
    // A complete `tag:image` (or an alias like `tag:excel`) followed by space/text is done: closed.
    if (rest.length > 0 && canonicalTag(frag) != null) return null
    return frag
  }, [search])
  const suggestOpen = tagFrag != null && searchFocused
  /** Snapshot whole-library counts once per open; names filter live as the fragment grows. */
  useEffect(() => {
    if (!suggestOpen) {
      setTagCounts(null)
      setSuggestIndex(0)
      return
    }
    let alive = true
    setTagCounts(null)
    setSuggestIndex(0)
    void Promise.all(
      TAG_VOCAB.map((name) =>
        host
          .search({ query: `tag:${name}`, tabId: null, offset: 0, limit: 1 })
          .then((result) => ({ name, total: result.total }))
          .catch(() => ({ name, total: 0 })),
      ),
    ).then((rows) => {
      if (!alive) return
      const counts: Record<string, number> = {}
      for (const row of rows) counts[row.name] = row.total
      setTagCounts(counts)
    })
    return () => {
      alive = false
    }
  }, [suggestOpen, host])
  const suggestItems = useMemo(() => {
    if (!suggestOpen || tagFrag == null) return []
    const needle = tagFrag.toLowerCase()
    // An alias fragment surfaces its canonical tag: `tag:exc` shows `spreadsheet`.
    return TAG_VOCAB.filter(
      (name) =>
        name.startsWith(needle) ||
        Object.keys(TAG_ALIASES).some((alias) => TAG_ALIASES[alias] === name && alias.startsWith(needle)),
    ).filter((name) => (tagCounts ? (tagCounts[name] ?? 0) > 0 : true))
  }, [suggestOpen, tagFrag, tagCounts])
  useEffect(() => {
    setSuggestIndex((current) => Math.min(current, Math.max(0, suggestItems.length - 1)))
  }, [suggestItems.length])
  /** Plain words of the query (no `tag:` filter, negated or not): what the rows highlight. */
  const highlight = useMemo(
    () =>
      search
        .split(/\s+/)
        .map((word) => word.replace(/^-/, ''))
        .filter((word) => word && !/^tag:[A-Za-z0-9_-]*$/i.test(word) && word.toLowerCase() !== 'tag:')
        .filter((word) => word.length > 0),
    [search],
  )
  const chooseTag = useCallback(
    (name: string) => {
      const at = search.search(/tag:/i)
      if (at < 0) return
      const after = search.slice(at)
      const end = after.search(/\s/)
      const before = search.slice(0, at)
      const tail = end < 0 ? '' : after.slice(end)
      const spacer = before.length > 0 && !/\s$/.test(before) ? ' ' : ''
      // One tag only: replace the fragment, keep every other word, end with a space (Q3).
      setSearchText(`${before}${spacer}tag:${name} ${tail.replace(/^\s+/, '')}`.replace(/\s+/g, ' '))
      window.setTimeout(() => searchRef.current?.focus(), 0)
    },
    [search, setSearchText],
  )

  const copyItem = useCallback(
    async (clip: ClipItem) => {
      try {
        await host.copy({ id: clip.id, plain: false })
        showToast('Copied to clipboard', `${clip.app} · ${clip.title}`)
      } catch (error) {
        showToast('Copy failed', messageOf(error), true)
      }
    },
    [host, showToast],
  )

  const pasteItem = useCallback(
    async (clip: ClipItem, plain: boolean) => {
      try {
        const result = await host.paste({ id: clip.id, plain })
        if (result && result.pasted === false) throw new Error(result.error || 'Paste did not run')
        showToast(plain ? 'Pasted as plain text' : 'Pasted', `${clip.app} · ${clip.title}`)
      } catch (error) {
        // Paste is the newest bridge action; if it cannot run, the clip still goes to the
        // clipboard and the toast says which of the two happened.
        try {
          await host.copy({ id: clip.id, plain })
          showToast(
            'Copied instead',
            isNotReady(error) ? `Paste is not ready yet - ${clip.app} · ${clip.title}` : messageOf(error),
          )
        } catch (copyError) {
          showToast('Paste failed', messageOf(copyError), true)
        }
      }
    },
    [host, showToast],
  )

  const reload = useCallback(async (): Promise<ClipItem[]> => {
    if (query) {
      const result = await host.search({ query, tabId: tab, limit: 500 })
      setFound({ query, ...result })
      return result.items
    }
    const rows = await host.getItems({ tabId: tab, offset: 0, limit: 1000 })
    setItems(rows)
    return rows
  }, [host, query, tab])

  const createTab = useCallback(
    async (name: string) => {
      const created = await host.createTab({ name })
      await refreshTabs()
      selectTab(created.id)
    },
    [host, refreshTabs, selectTab],
  )

  const renameTab = useCallback(
    async (id: number, name: string) => {
      await host.renameTab({ id, name })
      await refreshTabs()
    },
    [host, refreshTabs],
  )

  const deleteTab = useCallback(
    async (entry: StripTab) => {
      if (entry.id == null) return 0
      const result = await host.deleteTab({ id: entry.id })
      await refreshTabs()
      if (tab === entry.id) selectTab(null)
      showToast(
        `Deleted “${entry.label}”`,
        `${result.deletedClips.toLocaleString()} ${result.deletedClips === 1 ? 'clip' : 'clips'} deleted. Pinned clips were not affected.`,
      )
      return result.deletedClips
    },
    [host, refreshTabs, selectTab, showToast, tab],
  )

  /** Persists the strip order after a tab drag, then re-reads the strip; the selection stays put. */
  const reorderTabs = useCallback(
    async (order: number[]) => {
      try {
        await host.reorderTabs({ order })
        await refreshTabs()
      } catch (error) {
        showToast('Could not reorder tabs', messageOf(error), true)
      }
    },
    [host, refreshTabs, showToast],
  )

  /** Move, then re-read both the strip and the current page; the row leaves the list it came from. */
  const moveClipToTab = useCallback(
    async (clip: ClipItem, tabId: number | null) => {
      const index = visibleRef.current.findIndex((row) => row.id === clip.id)
      try {
        await host.moveToTab({ id: clip.id, tabId })
      } catch (error) {
        showToast('Move failed', messageOf(error), true)
        return
      }
      if (clip.tabId == null && tabId != null) setAllCount((count) => Math.max(0, count - 1))
      else if (clip.tabId != null && tabId == null) setAllCount((count) => count + 1)
      await refreshTabs()
      let rows: ClipItem[]
      try {
        rows = await reload()
      } catch (error) {
        showToast('Moved, but the list did not refresh', messageOf(error), true)
        return
      }
      setSelectedId((current) =>
        current != null && rows.some((row) => row.id === current)
          ? current
          : rows[Math.min(Math.max(index, 0), rows.length - 1)]?.id ?? null,
      )
      const label = moveDestinations.find((target) => target.id === tabId)?.label ?? 'Inbox'
      showToast('Moved', `${clip.title || 'Clip'} → ${label}`)
    },
    [host, moveDestinations, refreshTabs, reload, showToast],
  )

  /** Ctrl+Up/Down: the first row of the previous/next day group, clamped at both ends. */
  const jumpSection = useCallback(
    (direction: 1 | -1) => {
      const rows = visibleRef.current
      if (rows.length === 0) return
      const starts = sectionStarts(rows)
      const index = rows.findIndex((row) => row.id === selectedId)
      const at = index < 0 ? 0 : index
      const current = starts.filter((start) => start <= at).pop() ?? 0
      if (direction === 1) {
        const next = starts.find((start) => start > current)
        if (next == null) return
        setSelectedId(rows[next].id)
        requestAnimationFrame(() => listRef.current?.revealTop(rows[next].id))
        return
      }
      const previous = [...starts].reverse().find((start) => start < current) ?? 0
      setSelectedId(rows[previous].id)
      requestAnimationFrame(() => listRef.current?.revealTop(rows[previous].id))
    },
    [selectedId],
  )

  /** Plain navigation collapses the range: one cursor row, no anchor. */
  const selectSingle = useCallback((id: number) => {
    setAnchorId(null)
    setSelectedIds([])
    setSelectedId(id)
  }, [])

  /** Shift+Up/Down: grow or shrink the contiguous range between anchor and cursor. */
  const extendSelection = useCallback(
    (direction: 1 | -1) => {
      const rows = visibleRef.current
      if (rows.length === 0) return
      const at = rows.findIndex((row) => row.id === selectedId)
      const from = at < 0 ? 0 : at
      const next = Math.max(0, Math.min(rows.length - 1, from + direction))
      if (next === from) return
      const anchor = anchorId ?? selectedId ?? rows[from].id
      const anchorAt = rows.findIndex((row) => row.id === anchor)
      const base = anchorAt < 0 ? from : anchorAt
      if (anchorAt < 0) setAnchorId(rows[from].id)
      else setAnchorId(anchor)
      const range = rows.slice(Math.min(base, next), Math.max(base, next) + 1).map((row) => row.id)
      setSelectedIds(range)
      setSelectedId(rows[next].id)
    },
    [anchorId, selectedId],
  )

  /** Ctrl+Shift+Left/Right: one step along the Inbox -> custom tabs -> Pinned, wrapping at the ends. */
  const moveToAdjacentTab = useCallback(
    (direction: 1 | -1) => {
      if (!selected || moveDestinations.length < 2) return
      const current = moveDestinations.findIndex((target) => target.id === (selected.tabId ?? null))
      const from = current < 0 ? 0 : current
      const next = moveDestinations[(from + direction + moveDestinations.length) % moveDestinations.length]
      void moveClipToTab(selected, next.id)
    },
    [moveClipToTab, moveDestinations, selected],
  )

  const undoDelete = useCallback(async () => {
    const entries = lastDeleted.current
    if (!entries || entries.length === 0) {
      showToast('Nothing to undo', 'Delete a clip first.')
      return
    }
    try {
      // Restore lowest index first so the rows land back in their old order.
      for (const entry of [...entries].sort((a, b) => a.index - b.index)) {
        await host.restore({ id: entry.clip.id })
      }
      lastDeleted.current = null
      await reload()
      setAnchorId(null)
      setSelectedIds([])
      setSelectedId(entries[0].clip.id)
      showToast(entries.length === 1 ? 'Restored' : `Restored ${entries.length} clips`, `${entries[0].clip.app} · ${entries[0].clip.title}`)
    } catch (error) {
      showToast(isNotReady(error) ? 'Undo not ready yet' : 'Restore failed', messageOf(error), true)
    }
  }, [host, reload, showToast])

  const pinItem = useCallback(async () => {
    if (!selected) return
    try {
      await host.togglePin({ id: selected.id })
      if (selected.tabId == null) setAllCount((count) => Math.max(0, count - 1))
      else if (selected.pinned) setAllCount((count) => count + 1)
      await refreshTabs()
      await reload()
      setSelectedId(selected.id)
      showToast(selected.pinned ? 'Unpinned' : 'Pinned', `${selected.app} · ${selected.title}`)
    } catch (error) {
      showToast(isNotReady(error) ? 'Pin not ready yet' : 'Pin failed', messageOf(error), true)
    }
  }, [host, refreshTabs, reload, selected, showToast])

  const hideWindow = useCallback(async () => {
    try {
      await host.hide()
    } catch (error) {
      showToast(isNotReady(error) ? 'Hide not ready yet' : 'Hide failed', messageOf(error), true)
    }
  }, [host, showToast])

  /** "Open in default app": the host writes a temp file and launches the default app. */
  const openInApp = useCallback(
    async (clip: ClipItem) => {
      try {
        const result = await host.openInApp({ id: clip.id })
        if (result.launched) showToast('Opened', result.path || `${clip.app} · ${clip.title}`, false, result.path)
      } catch (error) {
        showToast('Could not open', messageOf(error), true)
      }
    },
    [host, showToast],
  )

  /** The host owns the dialog, the name and the bytes; the page only reports what came back. */
  const saveClip = useCallback(
    async (clip: ClipItem, format: string) => {
      try {
        const result = await host.exportClip({ id: clip.id, format })
        if (!result.saved) return // cancelled: no toast, no error
        const path = result.path ?? ''
        const name = path.split(/[\\/]/).pop() || 'Clip'
        showToast(`Saved “${name}”`, path || 'The file was saved.', false, path)
      } catch (error) {
        showToast('Export failed', messageOf(error), true)
      }
    },
    [host, showToast],
  )

  /** Ctrl+E: the host shows the Save dialog straight away; file lists have nothing to save. */
  const quickSave = useCallback(() => {
    if (!selected || exportOptions(selected).length === 0) return
    void saveClip(selected, 'auto')
  }, [saveClip, selected])

  const zoomRef = useRef(settings.uiZoom)
  zoomRef.current = settings.uiZoom
  const zoomSeq = useRef(0)

  /** Zoom is a host setting (WebView2 ZoomFactor): apply, persist, and step through the presets. */
  const applyZoom = useCallback(
    (zoom: number) => {
      const next = Math.min(150, Math.max(80, Math.round(zoom)))
      if (next === zoomRef.current) return
      zoomRef.current = next
      setSettings((current) => ({ ...current, uiZoom: next }))
      const seq = ++zoomSeq.current
      host
        .saveConfig({ uiZoom: next })
        .then((result) => {
          // Only the zoom is live here: applying the whole config would undo a theme the dialog is previewing.
          if (seq === zoomSeq.current) setSettings((current) => ({ ...current, uiZoom: result.config.uiZoom }))
        })
        .catch((error) => showToast('Zoom not saved', messageOf(error), true))
    },
    [host, showToast],
  )

  useEffect(() => {
    const steps = [80, 100, 125, 150]
    const onKey = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey) || event.altKey) return
      let next: number | null = null
      if (event.key === '0') next = 100
      else if (event.key === '+' || event.key === '=') next = steps.find((value) => value > zoomRef.current) ?? 150
      else if (event.key === '-') next = [...steps].reverse().find((value) => value < zoomRef.current) ?? 80
      if (next == null) return
      event.preventDefault()
      applyZoom(next)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [applyZoom])

  const saveSettings = useCallback(
    async (patch: SettingsPatch): Promise<SaveConfigResult> => {
      const result = await host.saveConfig(patch)
      applySettings(result.config)
      if (!result.hotkeyError) {
        // The cap, the theme and the time format can all change what the list shows.
        await reload()
        showToast('Settings saved', 'Clip uses them right away.')
      }
      return result
    },
    [applySettings, host, reload, showToast],
  )

  const clearHistory = useCallback(async () => {
    try {
      // The host emits `cleared`; the event below reloads and reports, so the tray path stays identical.
      await host.clearHistory()
    } catch (error) {
      showToast('Clear failed', messageOf(error), true)
    }
  }, [host, showToast])

  const deleteClips = useCallback(async (clips: ClipItem[]) => {
    if (clips.length === 0) return
    setConfirm(null)
    const rows = visibleRef.current
    const ids = new Set(clips.map((clip) => clip.id))
    const at = rows.findIndex((row) => ids.has(row.id))
    lastDeleted.current = clips.map((clip) => ({ clip, index: Math.max(0, rows.findIndex((row) => row.id === clip.id)) }))
    try {
      for (const clip of clips) {
        await host.delete({ id: clip.id })
      }
      // The host also emits `deleted`; removing here keeps the rows gone even when it does not.
      // The count is owned by that event (it also covers a delete the page did not start).
      setItems((current) => current.filter((row) => !ids.has(row.id)))
      setFound((current) => (current ? { ...current, items: current.items.filter((row) => !ids.has(row.id)) } : current))
      setAnchorId(null)
      setSelectedIds([])
      if (at >= 0) {
        const kept = rows.filter((row) => !ids.has(row.id))
        const next = kept[Math.min(at, kept.length - 1)]?.id ?? null
        setSelectedId((current) => (current != null && ids.has(current) ? next : current))
      }
      showToast(clips.length === 1 ? 'Deleted' : `Deleted ${clips.length} clips`, 'Press Ctrl+Z to bring it back')
    } catch (error) {
      lastDeleted.current = null
      showToast(isNotReady(error) ? 'Delete not ready yet' : 'Delete failed', messageOf(error), true)
    }
  }, [host, showToast])

  /** The dialog only opens for many clips; confirming deletes whatever it holds. */
  const confirmDelete = useCallback(() => {
    if (confirm) void deleteClips(confirm)
  }, [confirm, deleteClips])

  /** Programmatic filters (a chip or Ctrl+T) replace the query; only typed text survives a tab change. */
  const filterByTag = useCallback(
    (token: string | null) => {
      setImages(false)
      // Trailing space: the tag is done, so the `tag:` list stays closed.
      setSearchProgrammatic(token ? `${token} ` : clearTagTokens(search))
    },
    [search, setSearchProgrammatic],
  )

  /**
   * The shell calls Web.Focus() when the window appears. Land that focus on the selected row so
   * arrows, typing and Esc work from the first keystroke; a search field that already holds text
   * keeps its caret.
   */
  const focusRowsOnShow = useCallback(() => {
    if (editorOpen || settingsOpen || confirm != null || tabOverlay || rowMenu != null || moveClip != null || tagClip != null || infoClip != null) return
    if (selectedId == null) return
    const active = document.activeElement
    const emptySearch = active === searchRef.current && search.trim() === ''
    if (active && active !== document.body && !emptySearch) return
    listRef.current?.focusSelected()
  }, [confirm, editorOpen, infoClip, moveClip, rowMenu, search, selectedId, settingsOpen, tabOverlay, tagClip])

  /**
   * A slow reopen lands back on the main list with keys ready: every parent-owned
   * popup is closed, the search is cleared (stale text with list focus made
   * Left/Right/Home/End look dead), then the 1st row of the current tab is selected.
   * The shell decides when (a "reset" event); the page deliberately keeps no clock.
   */
  const resetToMain = useCallback(() => {
    setEditorOpen(false)
    setSettingsOpen(false)
    setConfirm(null)
    setRowMenu(null)
    setMoveClip(null)
    setTagClip(null)
    setInfoClip(null)
    setStorageNotice(null)
    if (images) setImages(false)
    // The cleared-query reload stays parked: lastQuery is already empty, so the
    // query effect below early-returns and never fights this selection.
    searchSeq.current++
    lastQuery.current = ''
    setFound(null)
    setSearchProgrammatic('')
    setQuery('')
    setAnchorId(null)
    setSelectedIds([])
    const first = itemsRef.current[0]?.id
    if (first != null) {
      setSelectedId(first)
      // Radix dialogs restore focus to their trigger on close; run after that.
      window.setTimeout(() => {
        listRef.current?.revealTop(first)
        listRef.current?.focusSelected()
      }, 50)
    } else {
      window.setTimeout(() => listRef.current?.focusSelected(), 50)
    }
  }, [images])

  useEffect(() => {
    const onShow = () => {
      focusRowsOnShow()
      // Every show re-reads the counts: a copy made while the window was hidden is already in the DB.
      void refreshTabs()
    }
    window.addEventListener('focus', onShow)
    return () => window.removeEventListener('focus', onShow)
  }, [focusRowsOnShow, refreshTabs])

  // A warm window is already focused when the page mounts, so no focus event follows: do it once.
  useEffect(() => {
    if (status !== 'ready') return
    const timer = window.setTimeout(() => listRef.current?.focusSelected(), 60)
    return () => window.clearTimeout(timer)
  }, [status])

  useClipboardKeys({
    enabled: !editorOpen && !settingsOpen && confirm == null && !tabOverlay && rowMenu == null && moveClip == null && tagClip == null && infoClip == null,
    rows: visible,
    selectedId,
    search,
    searchRef,
    onSelect: selectSingle,
    onRangeExtend: extendSelection,
    onSearch: setSearchText,
    onSearchFocus: focusSearch,
    onPage: (direction) => listRef.current?.pageBy(direction),
    onSection: jumpSection,
    onMoveTab: moveToAdjacentTab,
    onTab: cycleTab,
    onPaste: (clip, plain) => void pasteItem(clip, plain),
    onEdit: () => selected && setEditorOpen(true),
    onExport: quickSave,
    onDelete: () => {
      if (!selected) return
      // Delete acts on the whole Shift+Up/Down range; every other action uses the cursor row.
      // One clip deletes at once; many ask first.
      const ids = selectedIds.length > 1 ? new Set(selectedIds) : null
      const clips = ids ? visible.filter((row) => ids.has(row.id)) : [selected]
      if (clips.length === 0) return
      if (clips.length === 1) {
        void deleteClips(clips)
        return
      }
      confirmReturn.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
      setConfirm(clips)
    },
    onMove: () => {
      if (!selected) return
      moveReturn.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
      setMoveClip(selected)
    },
    onTagPicker: () => {
      if (!selected) return
      tagReturn.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
      setTagClip(selected)
    },
    onRestore: () => void undoDelete(),
    onPin: () => void pinItem(),
    onHide: () => void hideWindow(),
  })

  // Live capture must not disturb someone reading further down the history.
  useEffect(
    () =>
      host.onEvent(({ event, data }) => {
        if (event === 'reset') {
          // The shell hid more than 5s ago: land back on the main list, 1st row, keys ready.
          resetToMain()
          return
        }
        if (event === 'captured') {
          const item = data as ClipItem | undefined
          if (!item || typeof item.id !== 'number') return
          // Counts move for every capture, even one that belongs to another tab.
          void refreshTabs()
          const known = itemsRef.current.some((row) => row.id === item.id)
          const belongs = known || (tab === null ? item.tabId == null : item.tabId === tab)
          if (!belongs) return
          // A copy that already exists is reordered by the store; move that row and refresh its
          // fields instead of ignoring it. Selection is a separate id, so it does not move.
          setItems((rows) => moveToTop(rows, item))
          // A copy made while the window was closed is what the next open should start on, not the row
          // that happened to be selected before it.
          if (!document.hasFocus()) setSelectedId(item.id)
          // A query is a view over the same rows: only reorder the row when the results hold it.
          setFound((current) =>
            current && current.items.some((row) => row.id === item.id)
              ? { ...current, items: moveToTop(current.items, item) }
              : current,
          )
          return
        }
        if (event === 'skipped') {
          const reason = (data as { reason?: string } | undefined)?.reason
          showToast('Skipped', reason || 'The image was too large to capture.')
          return
        }
        if (event === 'storage') {
          // The host re-checks on every popup open; the dialog asks once per run (owner decision:
          // copies keep saving, the notice advises, it never blocks).
          const s = data as { bytes?: number; limit?: number } | undefined
          if (typeof s?.bytes !== 'number' || typeof s?.limit !== 'number') return
          if (storageShown.current) return
          storageShown.current = true
          setStorageNotice({ bytes: s.bytes, limit: s.limit })
          return
        }
        if (event === 'hotkeyFailed') {
          const combo = (data as { combo?: string } | undefined)?.combo
          showToast(
            'Hotkey unavailable',
            combo ? `${combo} is already used by another app.` : 'That shortcut is already used by another app.',
          )
          return
        }
        if (event === 'settingsSaved') {
          applySettings(toSettings(data as HostConfig))
          return
        }
        if (event === 'themeChanged') {
          const next = (data as { theme?: string } | undefined)?.theme
          if (next === 'dark' || next === 'light') setTheme(next)
          return
        }
        if (event === 'cleared') {
          // Counts and tabs live in init, not in a list page: reload the surface, not just the rows.
          void load()
          showToast('History cleared', 'Pinned clips and tabs stay.')
          return
        }
        if (event === 'corruptionRecovered') {
          // The host reloads the page in the same breath; this quiet note is for the moment before it does.
          const notice = (data as { notice?: string } | undefined)?.notice
          showToast('History recovered', notice || 'Clip history could not be read and was moved aside. A fresh history has started.')
          return
        }
        if (event === 'deleted') {
          const id = (data as { id?: number } | undefined)?.id
          if (typeof id !== 'number') return
          const at = visibleRef.current.findIndex((row) => row.id === id)
          if (at >= 0) {
            const rows = visibleRef.current.filter((row) => row.id !== id)
            setSelectedId((current) => (current === id ? rows[Math.min(at, rows.length - 1)]?.id ?? null : current))
            setSelectedIds((current) => current.filter((entry) => entry !== id))
            setAnchorId((current) => (current === id ? null : current))
          }
          setItems((rows) => rows.filter((row) => row.id !== id))
          setFound((current) =>
            current ? { ...current, items: current.items.filter((row) => row.id !== id), total: Math.max(0, current.total - 1) } : current,
          )
          void refreshTabs()
        }
      }),
    [applySettings, host, load, refreshTabs, reload, resetToMain, tab, showToast],
  )

  return (
    <Tooltip.Provider delayDuration={350}>
      <div className="app">
        <header className={'topbar' + (searchOpen ? '' : ' search-collapsed')}>
          <div className="brand">
            <span className="brand-mark">
              <IconClipboard size={17} />
            </span>
            <div className="brand-text">
              <div className="brand-name">Clip</div>
            </div>
          </div>

          <div className="search">
            <IconSearch />
            <input
              ref={searchRef}
              value={search}
              onChange={(event) => setSearchText(event.target.value)}
              onFocus={() => setSearchFocused(true)}
              onBlur={() => setSearchFocused(false)}
              onKeyDown={(event) => {
                if (!suggestOpen) return
                // Ctrl+Tab / Ctrl+Shift+Tab cycle tabs from anywhere; never eat them as a tag pick.
                if (event.ctrlKey || event.metaKey) return
                if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                  event.preventDefault()
                  event.stopPropagation()
                  if (suggestItems.length === 0) return
                  const direction = event.key === 'ArrowDown' ? 1 : -1
                  setSuggestIndex(
                    (current) => (current + direction + suggestItems.length) % suggestItems.length,
                  )
                } else if (event.key === 'Enter' || event.key === 'Tab') {
                  const pick = suggestItems[suggestIndex]
                  if (!pick) return
                  event.preventDefault()
                  event.stopPropagation()
                  chooseTag(pick)
                } else if (event.key === 'Escape') {
                  // Close the list first; the window matrix clears the text on the next Esc.
                  event.preventDefault()
                  event.stopPropagation()
                  const at = search.search(/tag:/i)
                  if (at >= 0) {
                    const after = search.slice(at)
                    const end = after.search(/\s/)
                    setSearchText(
                      `${search.slice(0, at)}${end < 0 ? '' : after.slice(end)}`.replace(/\s+/g, ' '),
                    )
                  }
                }
              }}
              placeholder="Search clips…"
              aria-label="Search clips"
              aria-expanded={suggestOpen}
              aria-autocomplete="list"
              role="combobox"
              aria-controls="tag-suggest"
              aria-activedescendant={suggestOpen && suggestItems[suggestIndex] ? `tag-${suggestItems[suggestIndex]}` : undefined}
              spellCheck={false}
            />
            {suggestOpen && (
              <div className="suggest" role="listbox" id="tag-suggest" aria-label="Tags">
                {suggestItems.length === 0 ? (
                  <div className="suggest-empty">No matching tag</div>
                ) : (
                  suggestItems.map((name, index) => {
                    const count = tagCounts?.[name]
                    const frag = tagFrag ?? ''
                    const at = name.toLowerCase().indexOf(frag.toLowerCase())
                    return (
                      <div
                        key={name}
                        id={`tag-${name}`}
                        role="option"
                        aria-selected={index === suggestIndex}
                        className={'suggest-row' + (index === suggestIndex ? ' active' : '')}
                        onMouseDown={(event) => event.preventDefault()}
                        onClick={() => chooseTag(name)}
                        onMouseEnter={() => setSuggestIndex(index)}
                      >
                        <span className="suggest-name">
                          {at >= 0 && frag.length > 0 ? (
                            <>
                              {name.slice(0, at)}
                              <mark className="hit">{name.slice(at, at + frag.length)}</mark>
                              {name.slice(at + frag.length)}
                            </>
                          ) : (
                            name
                          )}
                        </span>
                        <span className="suggest-count">{typeof count === 'number' ? `(${count})` : '(…)'}</span>
                      </div>
                    )
                  })
                )}
              </div>
            )}
            {search ? (
              <button
                className="icon-btn ghost"
                // 22 px: WCAG 2.5.8 passes via the spacing exception (lone target inside
                // the 34 px search box). Enlarge if neighbors ever crowd it.
                style={{ width: 22, height: 22, border: 0 }}
                onClick={() => setSearchText('')}
                aria-label="Clear search"
              >
                <IconClose />
              </button>
            ) : (
              <span className="keycap search-key">Ctrl K</span>
            )}
          </div>

          <span className="spacer" />

          {today != null && today > 0 && (
            <span className="hint today-chip" style={{ marginRight: 2 }}>
              <span className="chip">
                <span className="dot" />
                {today} today
              </span>
            </span>
          )}

          <button
            className="icon-btn search-toggle"
            aria-label={searchOpen ? 'Collapse search' : 'Expand search'}
            aria-expanded={searchOpen}
            onClick={() => {
              const next = !searchOpen
              setSearchOpen(next)
              if (next) window.setTimeout(() => searchRef.current?.focus(), 0)
              // Collapsing back to the bare list: land on the selected row, not the toggle.
              else listRef.current?.focusSelected()
            }}
          >
            <IconSearch />
          </button>

          <Tooltip.Root>
            <Tooltip.Trigger asChild>
              <button
                className="icon-btn"
                onClick={() => {
                  setTheme(theme === 'light' ? 'dark' : 'light')
                  // A theme click strands focus on this button, where Up/Down is dead.
                  listRef.current?.focusSelected()
                }}
                aria-label="Toggle theme"
              >
                {theme === 'light' ? <IconMoon /> : <IconSun />}
              </button>
            </Tooltip.Trigger>
            <Tooltip.Portal>
              <Tooltip.Content className="tip" sideOffset={6}>
                {theme === 'light' ? 'Switch to dark' : 'Switch to light'}
                <Tooltip.Arrow className="tip-arrow" />
              </Tooltip.Content>
            </Tooltip.Portal>
          </Tooltip.Root>

          <Tooltip.Root>
            <Tooltip.Trigger asChild>
              <button
                className="icon-btn"
                onClick={() => {
                  settingsReturn.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
                  setSettingsOpen(true)
                }}
                aria-label="Settings"
              >
                <IconSettings />
              </button>
            </Tooltip.Trigger>
            <Tooltip.Portal>
              <Tooltip.Content className="tip" sideOffset={6}>
                Settings
                <Tooltip.Arrow className="tip-arrow" />
              </Tooltip.Content>
            </Tooltip.Portal>
          </Tooltip.Root>
        </header>

        <main className="card">
          <div className="card-head">
            <TabStrip
              tabs={tabs}
              activeId={images ? IMAGES_TAB : tab}
              onSelect={chooseTab}
              onCreate={createTab}
              onRename={renameTab}
              onDelete={deleteTab}
              onReorder={reorderTabs}
              onOverlayChange={setTabOverlay}
            />
            <span className="spacer" />
            {searchActive && <span className="chip accent">Filtered</span>}
            <span className="t-meta shown-count">{visible.length.toLocaleString()} shown</span>
          </div>

          {status === 'loading' && (
            <div className="list-wrap">
              <div className="empty">
                <div>
                  <div className="t-title">Loading history…</div>
                  <div className="t-sec" style={{ marginTop: 4 }}>
                    Reading clips from the local store.
                  </div>
                </div>
              </div>
            </div>
          )}

          {status === 'error' && (
            <div className="list-wrap">
              <div className="empty">
                <div>
                  <div className="t-title">Could not load clipboard history</div>
                  <div className="t-sec" style={{ marginTop: 4, maxWidth: 380 }}>
                    {loadError}
                  </div>
                  <button className="btn" style={{ marginTop: 12 }} onClick={load}>
                    Retry
                  </button>
                </div>
              </div>
            </div>
          )}

          {status === 'ready' && (
            <ClipList
              ref={listRef}
              clips={visible}
              selectedId={selectedId}
              selectedIds={selectedIds}
              timeFormat={timeFormat}
              hidePins={pinnedTab}
              showGroups={tab === null}
              onSelect={selectSingle}
              onCopy={copyItem}
              onContextMenu={(clip, x, y) => setRowMenu({ clip, x, y })}
              onTag={(id) => {
                // Trailing space: the tag is done, so the `tag:` list stays closed.
                setSearchProgrammatic(`${tagToken(id)} `)
                focusSearch()
              }}
              getThumb={host.getThumb}
              getImage={host.getImage ?? host.getThumb}
              getItem={host.getItem}
              emptyTitle={emptyTitle}
              emptySub={emptySub}
              highlight={highlight}
              maxLines={settings.previewLines}
            />
          )}

          <div className="footer">
            <div className="footer-left">
              <span className="t-meta">{images ? 'Image results' : searchActive ? `Results for "${query}"` : tab === null ? 'Clipboard history' : `${tabLabel} history`}</span>
              {activeSearch && activeSearch.unknownTags.length > 0 && (
                <span
                  className="tag-hint"
                  title={`No tag named ${activeSearch.unknownTags.map((tag) => `“${tag}”`).join(', ')} — searched as text`}
                >
                  no tag named {activeSearch.unknownTags.map((tag) => `“${tag}”`).join(', ')} — searched as text
                </span>
              )}
            </div>
            <span className="spacer" />
            <div className="footer-mid">
              <span className="t-meta">
                {searchActive ? `${(found?.total ?? 0).toLocaleString()} found` : `${tabCount.toLocaleString()} clips`}
              </span>
            </div>
            <div className="footer-right">
              <span className="hint">
                <span className="keycap">Ctrl T</span> tags
              </span>
              <span className="hint">
                <span className="keycap">Ctrl Shift V</span> plain
              </span>
              <span className="hint">
                <span className="keycap">Esc</span> {search ? 'clear' : 'hide'}
              </span>
            </div>
          </div>
        </main>
      </div>

      <SettingsDialog
        open={settingsOpen}
        onOpenChange={(open) => {
          setSettingsOpen(open)
          // The dialog hands focus back to its header button, where Up/Down is dead.
          // The main window's keyboard belongs to the list, so land on the selected row.
          if (!open) window.setTimeout(() => listRef.current?.focusSelected(), 50)
        }}
        settings={settings}
        onPreviewTheme={setTheme}
        onZoom={applyZoom}
        onSave={saveSettings}
        onClearHistory={() => void clearHistory()}
        onStorageUsage={host.storageUsage}
        onPreviewToast={() => void host.previewNotification()}
        returnFocus={settingsReturn}
      />

      <DeleteConfirm
        clips={confirm}
        onCancel={() => setConfirm(null)}
        onConfirm={() => void confirmDelete()}
        returnFocus={confirmReturn}
      />

      {rowMenu && (
        <RowMenu
          clip={rowMenu.clip}
          targets={moveDestinations}
          x={rowMenu.x}
          y={rowMenu.y}
          canSave={exportOptions(rowMenu.clip).length > 0}
          canOpen={rowMenu.clip.kind !== 'files'}
          onEdit={() => {
            setRowMenu(null)
            setEditorOpen(true)
          }}
          onCopy={() => {
            const clip = rowMenu.clip
            setRowMenu(null)
            void copyItem(clip)
          }}
          onSaveAs={() => {
            const clip = rowMenu.clip
            setRowMenu(null)
            void saveClip(clip, 'auto')
          }}
          onDelete={() => {
            const clip = rowMenu.clip
            setRowMenu(null)
            void deleteClips([clip])
          }}
          onOpenDefault={() => {
            const clip = rowMenu.clip
            setRowMenu(null)
            void openInApp(clip)
          }}
          onInfo={() => {
            const clip = rowMenu.clip
            setRowMenu(null)
            infoReturn.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
            setInfoClip(clip)
          }}
          onMove={(tabId) => {
            const clip = rowMenu.clip
            setRowMenu(null)
            void moveClipToTab(clip, tabId)
          }}
          onClose={() => setRowMenu(null)}
        />
      )}

      <MovePicker
        clip={moveClip}
        targets={moveDestinations}
        onChoose={(tabId) => {
          if (moveClip) void moveClipToTab(moveClip, tabId)
        }}
        onClose={() => setMoveClip(null)}
        returnFocus={moveReturn}
      />

      <TagPicker
        clip={tagClip}
        query={search}
        onChoose={filterByTag}
        onClose={() => setTagClip(null)}
        returnFocus={tagReturn}
      />

      <ClipInfo
        clip={infoClip}
        timeFormat={timeFormat}
        onClose={() => setInfoClip(null)}
        returnFocus={infoReturn}
      />

      <StorageNotice
        notice={storageNotice}
        onClose={() => setStorageNotice(null)}
      />

      {editorOpen && selected && (
        <EditorOverlay
          clip={selected}
          timeFormat={timeFormat}
          getItem={host.getItem}
          getImage={host.getImage ?? host.getThumb}
          onSave={(html, text) => host.saveEditor({ id: selected.id, html, text })}
          onSaveAs={(format) => saveClip(selected, format)}
          onOpenInApp={() => openInApp(selected)}
          onReOcr={selected.kind === 'image' ? () => host.reOcr({ id: selected.id }) : undefined}
          onSaved={() => {
            // The row is still mounted with the pre-edit body; drop it before the reload.
            forgetBody(selected.id)
            void reload()
            showToast('Clip saved', `${selected.app} · ${selected.title}`)
          }}
          onClose={() => setEditorOpen(false)}
        />
      )}

      <div className="toast-region" aria-live="polite">
        {toasts.map((toast) => (
          <div className={'toast' + (toast.error ? ' error' : '')} key={toast.id} title={toast.hint || undefined}>
            <span className="ok">{toast.error ? <IconWarning /> : <IconCheck />}</span>
            <div className="grow">
              <div className="toast-title">{toast.title}</div>
              <div className="toast-sub">{toast.sub}</div>
            </div>
            <button
              className="icon-btn ghost"
              // 20 px: WCAG 2.5.8 passes via the spacing exception (toast padding
              // clears the 24 px circle). Enlarge if the toast layout densifies.
              style={{ width: 20, height: 20 }}
              onClick={() => setToasts((current) => current.filter((entry) => entry.id !== toast.id))}
              aria-label="Dismiss toast"
            >
              <IconClose />
            </button>
          </div>
        ))}
      </div>
    </Tooltip.Provider>
  )
}
