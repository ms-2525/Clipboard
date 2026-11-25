import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import * as Tooltip from '@radix-ui/react-tooltip'
import { ClipList, type ClipListHandle } from './ClipList'
import { DeleteConfirm } from './DeleteConfirm'
import { EditorOverlay } from './EditorOverlay'
import { MoveMenu, MovePicker, moveTargets } from './MoveToTab'
import { SettingsDialog } from './SettingsDialog'
import { TagPicker, clearTagTokens } from './TagPicker'
import { TabStrip, type StripTab } from './TabStrip'
import { createHost, messageOf, toSettings, type ClipItem, type HostConfig, type HostSettings, type HostTab, type InitResult, type SaveConfigResult, type SearchResult, type SettingsPatch } from './host'
import { useClipboardKeys } from './useClipboardKeys'
import { forgetBody } from './rich'
import type { TimeFormat } from './format'
import {
  IconCheck,
  IconClipboard,
  IconClose,
  IconEdit,
  IconFile,
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

/** How many clips the All tab can show; the host's richer `all` wins over the raw clips/images pair. */
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
  const [settingsOpen, setSettingsOpen] = useState(previewUi && params.get('surface') === 'settings')
  const [editorOpen, setEditorOpen] = useState(previewUi && params.get('surface') === 'editor')
  const [confirm, setConfirm] = useState<ClipItem | null>(null)
  const [exportOpen, setExportOpen] = useState(false)
  const [toasts, setToasts] = useState<Toast[]>([])
  const [init, setInit] = useState<InitResult | null>(null)
  const [tabRows, setTabRows] = useState<HostTab[]>([])
  const [allCount, setAllCount] = useState(0)
  /** The virtual Images tab: it shows `image:` over All history and is never stored as a DB tab. */
  const [images, setImages] = useState(false)
  const [imageCount, setImageCount] = useState(0)
  const [tabOverlay, setTabOverlay] = useState(false)
  const [rowMenu, setRowMenu] = useState<{ clip: ClipItem; x: number; y: number } | null>(null)
  const [moveClip, setMoveClip] = useState<ClipItem | null>(null)
  const [tagClip, setTagClip] = useState<ClipItem | null>(null)
  const [items, setItems] = useState<ClipItem[]>([])
  const [found, setFound] = useState<SearchView | null>(null)
  const [searching, setSearching] = useState(false)
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [loadError, setLoadError] = useState('')

  const toastId = useRef(0)
  const tabSeq = useRef(0)
  const searchSeq = useRef(0)
  const lastQuery = useRef('')
  const lastDeleted = useRef<{ clip: ClipItem; index: number } | null>(null)
  const searchRef = useRef<HTMLInputElement>(null)
  const searchTyped = useRef(false)
  const listRef = useRef<ClipListHandle>(null)
  const exportRef = useRef<HTMLDivElement>(null)
  const exportReturn = useRef<HTMLElement | null>(null)
  const settingsReturn = useRef<HTMLElement | null>(null)
  const confirmReturn = useRef<HTMLElement | null>(null)
  const moveReturn = useRef<HTMLElement | null>(null)
  const tagReturn = useRef<HTMLElement | null>(null)

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
        host.search({ query: 'image:', tabId: null, offset: 0, limit: 1 }),
      ])
      setAllCount(all.total)
      setImageCount(images.total)
    } catch {
      // A count is a hint; the list itself still works if this call fails.
    }
  }, [host, showToast])

  const load = useCallback(() => {
    const seq = ++tabSeq.current
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
      if (!searchTyped.current && (search || query)) {
        lastQuery.current = ''
        setSearch('')
        setQuery('')
      }
      setTab(next)
      setFound(null)
      searchSeq.current++
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

  const tabs = useMemo(() => {
    const rows: StripTab[] = [{ id: null, label: 'All', count: allCount, kind: 'all' }]
    const imagesTab: StripTab = { id: IMAGES_TAB, label: 'Images', count: imageCount, kind: 'images' }
    let placed = false
    for (const hostTab of tabRows) {
      rows.push({ id: hostTab.id, label: hostTab.kind === 'pinned' ? 'Pinned' : hostTab.name, count: hostTab.count, kind: hostTab.kind })
      if (hostTab.kind === 'pinned') {
        rows.push(imagesTab)
        placed = true
      }
    }
    if (!placed) rows.push(imagesTab)
    return rows
  }, [tabRows, allCount, imageCount])

  /** The Images tab is the saved `image:` view over All; it never becomes a DB tab. */
  const selectImages = useCallback(() => {
    setImages(true)
    setTab(null)
    setFound(null)
    searchSeq.current++
    lastQuery.current = ''
    setSearchProgrammatic('image:')
  }, [setSearchProgrammatic])

  const chooseTab = useCallback(
    (id: number | null) => {
      if (id === IMAGES_TAB) selectImages()
      else selectTab(id)
    },
    [selectImages, selectTab],
  )

  /** Left/Right: All, Pinned, Images, then the custom tabs; wraps so the strip is one cycle. */
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

  const visible = found?.items ?? items
  const visibleRef = useRef(visible)
  visibleRef.current = visible
  const itemsRef = useRef(items)
  itemsRef.current = items

  useEffect(() => {
    if (selectedId != null && visible.some((clip) => clip.id === selectedId)) return
    setSelectedId(visible[0]?.id ?? null)
  }, [visible, selectedId])

  const selected = visible.find((clip) => clip.id === selectedId) ?? null
  const activeTab = tabs.find((entry) => entry.id === tab)
  const tabLabel = images ? 'Images' : (activeTab?.label ?? 'All')
  const tabCount = images ? (found?.query === 'image:' ? found.total : imageCount) : (activeTab?.count ?? visible.length)
  // Every row in the Pinned tab is pinned; repeating the glyph there is noise.
  const pinnedTab = tab != null && tabs.some((entry) => entry.id === tab && entry.kind === 'pinned')
  const today = init?.counts?.today
  const timeFormat: TimeFormat = settings.timeFormat === '12h' ? '12h' : '24h'
  const searchActive = query !== ''
  const activeSearch = found?.query === query ? found : null
  const emptyTitle =
    images
      ? 'No images yet'
      : searching && visible.length === 0
        ? 'Searching…'
        : searchActive
          ? `Nothing matches “${query}”`
          : tab == null
            ? 'No clips yet'
            : activeTab?.kind === 'pinned'
              ? 'Nothing pinned'
              : `Nothing in “${tabLabel}”`
  const emptySub = images
    ? 'Copy a screenshot or a picture and it will show up here.'
    : searchActive
      ? 'Try a different search, or clear it with Esc.'
      : tab == null
        ? 'Copy something and it will appear here.'
        : activeTab?.kind === 'pinned'
          ? 'Press Ctrl+P on a clip to keep it here.'
          : 'Right-click a clip and move it here.'

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
      const label = moveDestinations.find((target) => target.id === tabId)?.label ?? 'All history'
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

  /** Ctrl+Shift+Left/Right: one step along All history -> custom tabs -> Pinned, wrapping at the ends. */
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
    const entry = lastDeleted.current
    if (!entry) {
      showToast('Nothing to undo', 'Delete a clip first.')
      return
    }
    try {
      await host.restore({ id: entry.clip.id })
      lastDeleted.current = null
      await reload()
      setSelectedId(entry.clip.id)
      showToast('Restored', `${entry.clip.app} · ${entry.clip.title}`)
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

  const closeExport = useCallback((restoreFocus: boolean) => {
    setExportOpen(false)
    if (restoreFocus) exportReturn.current?.focus?.()
  }, [])

  const openExport = useCallback(() => {
    if (!selected || exportOptions(selected).length === 0) return
    exportReturn.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
    setExportOpen(true)
  }, [selected])

  /** The host owns the dialog, the name and the bytes; the page only reports what came back. */
  const saveClip = useCallback(
    async (clip: ClipItem, format: string) => {
      setExportOpen(false)
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

  // The menu is a small popover: Escape closes it, arrows walk its items, and a click outside closes it.
  const onExportKey = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault()
      closeExport(true)
      return
    }
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return
    event.preventDefault()
    const items = Array.from(exportRef.current?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])
    if (items.length === 0) return
    const at = items.indexOf(document.activeElement as HTMLElement)
    items[(at + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length]?.focus()
  }

  useEffect(() => {
    if (!exportOpen) return
    const onDown = (event: MouseEvent) => {
      if (!exportRef.current?.contains(event.target as Node)) setExportOpen(false)
    }
    window.addEventListener('mousedown', onDown)
    return () => window.removeEventListener('mousedown', onDown)
  }, [exportOpen])

  useEffect(() => setExportOpen(false), [selectedId])

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

  const confirmDelete = useCallback(async () => {
    const clip = confirm
    if (!clip) return
    setConfirm(null)
    const index = visibleRef.current.findIndex((row) => row.id === clip.id)
    lastDeleted.current = { clip, index }
    try {
      await host.delete({ id: clip.id })
      // The host also emits `deleted`; removing here keeps the row gone even when it does not.
      // The count is owned by that event (it also covers a delete the page did not start).
      setItems((rows) => rows.filter((row) => row.id !== clip.id))
      setFound((current) => (current ? { ...current, items: current.items.filter((row) => row.id !== clip.id) } : current))
      if (index >= 0) {
        const rows = visibleRef.current.filter((row) => row.id !== clip.id)
        setSelectedId((current) => (current === clip.id ? rows[Math.min(index, rows.length - 1)]?.id ?? null : current))
      }
      showToast('Deleted', 'Press Ctrl+Z to bring it back')
    } catch (error) {
      lastDeleted.current = null
      showToast(isNotReady(error) ? 'Delete not ready yet' : 'Delete failed', messageOf(error), true)
    }
  }, [confirm, host, showToast])

  /** Programmatic filters (a chip or Ctrl+T) replace the query; only typed text survives a tab change. */
  const filterByTag = useCallback(
    (token: string | null) => {
      setImages(false)
      setSearchProgrammatic(token ?? clearTagTokens(search))
    },
    [search, setSearchProgrammatic],
  )

  /**
   * The shell calls Web.Focus() when the window appears. Land that focus on the selected row so
   * arrows, typing and Esc work from the first keystroke; a search field that already holds text
   * keeps its caret.
   */
  const focusRowsOnShow = useCallback(() => {
    if (editorOpen || settingsOpen || exportOpen || confirm != null || tabOverlay || rowMenu != null || moveClip != null || tagClip != null) return
    if (selectedId == null) return
    const active = document.activeElement
    const emptySearch = active === searchRef.current && search.trim() === ''
    if (active && active !== document.body && !emptySearch) return
    listRef.current?.focusSelected()
  }, [confirm, editorOpen, exportOpen, moveClip, rowMenu, search, selectedId, settingsOpen, tabOverlay, tagClip])

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
    enabled: !editorOpen && !settingsOpen && !exportOpen && confirm == null && !tabOverlay && rowMenu == null && moveClip == null && tagClip == null,
    rows: visible,
    selectedId,
    search,
    searchRef,
    onSelect: setSelectedId,
    onSearch: setSearchText,
    onSearchFocus: focusSearch,
    onPage: (direction) => listRef.current?.pageBy(direction),
    onSection: jumpSection,
    onMoveTab: moveToAdjacentTab,
    onTab: cycleTab,
    onPaste: (clip, plain) => void pasteItem(clip, plain),
    onEdit: () => selected && setEditorOpen(true),
    onExport: openExport,
    onDelete: () => {
      if (!selected) return
      confirmReturn.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
      setConfirm(selected)
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
          }
          setItems((rows) => rows.filter((row) => row.id !== id))
          setFound((current) =>
            current ? { ...current, items: current.items.filter((row) => row.id !== id), total: Math.max(0, current.total - 1) } : current,
          )
          void refreshTabs()
        }
      }),
    [applySettings, host, load, refreshTabs, reload, tab, showToast],
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
              <div className="brand-sub">Clipboard history</div>
            </div>
          </div>

          <div className="search">
            <IconSearch />
            <input
              ref={searchRef}
              value={search}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Search clips, apps, file names…"
              aria-label="Search clips"
              spellCheck={false}
            />
            {search ? (
              <button
                className="icon-btn ghost"
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
            }}
          >
            <IconSearch />
          </button>

          <Tooltip.Root>
            <Tooltip.Trigger asChild>
              <button
                className="icon-btn"
                onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}
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

        <div className="tabs-row">
          <TabStrip
            tabs={tabs}
            activeId={images ? IMAGES_TAB : tab}
            onSelect={chooseTab}
            onCreate={createTab}
            onRename={renameTab}
            onDelete={deleteTab}
            onOverlayChange={setTabOverlay}
          />
          <span className="tabs-hint t-meta">
            <span className="keycap">↑↓</span> move
            <span className="keycap" style={{ marginLeft: 6 }}>Enter</span> paste
          </span>
        </div>

        <main className="card">
          <div className="card-head">
            <div className="card-title">
              <span className="t-title">{images ? 'Images' : `${tabLabel} clips`}</span>
              <span className="chip">{tabCount.toLocaleString()}</span>
              {searchActive && <span className="chip accent">Filtered</span>}
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
            <span className="t-meta shown-count">{visible.length.toLocaleString()} shown</span>
            {selected && (
              <button className="btn sm" onClick={() => setEditorOpen(true)} title="Preview the selected clip in the editor">
                <IconEdit />
                Edit clip
                <span className="keycap edit-key">F2</span>
              </button>
            )}
            {exportOptions(selected).length > 0 && (
              <div className="tool-wrap" ref={exportRef}>
                <button
                  className="btn sm"
                  onClick={() => (exportOpen ? closeExport(false) : openExport())}
                  aria-haspopup="menu"
                  aria-expanded={exportOpen}
                  title="Save the selected clip as a file (Ctrl+E)"
                >
                  <IconFile />
                  Save as
                </button>
                {exportOpen && selected && (
                  <div className="popover export-pop" role="menu" aria-label="Save the selected clip" onKeyDown={onExportKey}>
                    <button className="pop-item" role="menuitem" autoFocus onClick={() => void saveClip(selected, 'auto')}>
                      Save as…
                      <span className="keycap">Ctrl E</span>
                    </button>
                    {exportOptions(selected).map((option) => (
                      <button
                        key={option.format}
                        className="pop-item"
                        role="menuitem"
                        onClick={() => void saveClip(selected, option.format)}
                      >
                        {option.label}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
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
              timeFormat={timeFormat}
              hidePins={pinnedTab}
              onSelect={setSelectedId}
              onCopy={copyItem}
              onContextMenu={(clip, x, y) => setRowMenu({ clip, x, y })}
              onTag={(id) => {
                setSearchProgrammatic(`${id}:`)
                focusSearch()
              }}
              getThumb={host.getThumb}
              getItem={host.getItem}
              emptyTitle={emptyTitle}
              emptySub={emptySub}
            />
          )}

          <div className="footer">
            <div className="footer-left">
              <span className="t-meta">{images ? 'Image results' : searchActive ? `Results for "${query}"` : `${tabLabel} history`}</span>
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
        onOpenChange={setSettingsOpen}
        settings={settings}
        onPreviewTheme={setTheme}
        onZoom={applyZoom}
        onSave={saveSettings}
        onClearHistory={() => void clearHistory()}
        onPreviewToast={() => void host.previewNotification()}
        returnFocus={settingsReturn}
      />

      <DeleteConfirm
        clip={confirm}
        onCancel={() => setConfirm(null)}
        onConfirm={() => void confirmDelete()}
        returnFocus={confirmReturn}
      />

      {rowMenu && (
        <MoveMenu
          clip={rowMenu.clip}
          targets={moveDestinations}
          x={rowMenu.x}
          y={rowMenu.y}
          onChoose={(tabId) => {
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

      {editorOpen && selected && (
        <EditorOverlay
          clip={selected}
          timeFormat={timeFormat}
          getItem={host.getItem}
          onSave={(html, text) => host.saveEditor({ id: selected.id, html, text })}
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
