import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import * as Tabs from '@radix-ui/react-tabs'
import * as Tooltip from '@radix-ui/react-tooltip'
import { ClipList, type ClipListHandle } from './ClipList'
import { DeleteConfirm } from './DeleteConfirm'
import { EditorOverlay } from './EditorOverlay'
import { SettingsDialog } from './SettingsDialog'
import { createHost, toSettings, type ClipItem, type HostConfig, type HostSettings, type InitResult, type SaveConfigResult, type SearchResult, type SettingsPatch } from './host'
import { useClipboardKeys } from './useClipboardKeys'
import type { TimeFormat } from './format'
import {
  IconCheck,
  IconClipboard,
  IconClose,
  IconEdit,
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
}

/** The last search answer, kept while the next one is pending so the rows never flash blank. */
interface SearchView extends SearchResult {
  query: string
}

const messageOf = (error: unknown) => (error instanceof Error ? error.message : String(error))

/** The next-slice bridge methods answer "unknown method"; that is a gap, not a failure. */
const isNotReady = (error: unknown) => /unknown method|not wired|not ready|phase 2/i.test(messageOf(error))

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
  const [toasts, setToasts] = useState<Toast[]>([])
  const [init, setInit] = useState<InitResult | null>(null)
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
  const listRef = useRef<ClipListHandle>(null)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
  }, [theme])

  const showToast = useCallback((title: string, sub: string, error = false) => {
    const id = ++toastId.current
    setToasts((current) => [...current.slice(-2), { id, title, sub, error }])
    window.setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), 2600)
  }, [])

  /** One place where a host config becomes live page state: theme, time format, list cap, zoom. */
  const applySettings = useCallback((next: HostSettings) => {
    setSettings(next)
    setTheme(next.theme)
  }, [])

  const load = useCallback(() => {
    const seq = ++tabSeq.current
    setStatus('loading')
    setLoadError('')
    host
      .init()
      .then((result) => {
        const rows = result.items ?? []
        setInit(result)
        setItems(rows)
        setSelectedId((current) => (current != null && rows.some((row) => row.id === current) ? current : rows[0]?.id ?? null))
        applySettings(result.config)
        setStatus('ready')

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
  }, [applySettings, host])

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
    },
    [host, showToast, tab],
  )

  const allCount = useMemo(() => {
    const counts = init?.counts
    if (!counts) return 0
    if (typeof counts.all === 'number') return counts.all
    if (typeof counts.clips === 'number' || typeof counts.images === 'number') return (counts.clips ?? 0) + (counts.images ?? 0)
    return init?.items?.length ?? 0
  }, [init])

  const tabs = useMemo(() => {
    const rows: { id: number | null; label: string; count: number }[] = [{ id: null, label: 'All', count: allCount }]
    for (const hostTab of init?.tabs ?? []) {
      rows.push({ id: hostTab.id, label: hostTab.kind === 'pinned' ? 'Pinned' : hostTab.name, count: hostTab.count })
    }
    return rows
  }, [init, allCount])

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
  const tabLabel = tabs.find((entry) => entry.id === tab)?.label ?? 'All'
  const tabCount = tabs.find((entry) => entry.id === tab)?.count ?? visible.length
  const today = init?.counts?.today
  const timeFormat: TimeFormat = settings.timeFormat === '12h' ? '12h' : '24h'
  const searchActive = query !== ''
  const activeSearch = found?.query === query ? found : null

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
      await reload()
      setSelectedId(selected.id)
      showToast(selected.pinned ? 'Unpinned' : 'Pinned', `${selected.app} · ${selected.title}`)
    } catch (error) {
      showToast(isNotReady(error) ? 'Pin not ready yet' : 'Pin failed', messageOf(error), true)
    }
  }, [host, reload, selected, showToast])

  const hideWindow = useCallback(async () => {
    try {
      await host.hide()
    } catch (error) {
      showToast(isNotReady(error) ? 'Hide not ready yet' : 'Hide failed', messageOf(error), true)
    }
  }, [host, showToast])

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
      setItems((rows) => rows.filter((row) => row.id !== clip.id))
      setFound((current) =>
        current ? { ...current, items: current.items.filter((row) => row.id !== clip.id), total: Math.max(0, current.total - 1) } : current,
      )
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

  useClipboardKeys({
    enabled: !editorOpen && !settingsOpen && confirm == null,
    rows: visible,
    selectedId,
    search,
    searchRef,
    onSelect: setSelectedId,
    onSearch: setSearch,
    onSearchFocus: focusSearch,
    onPage: (direction) => listRef.current?.pageBy(direction),
    onPaste: (clip, plain) => void pasteItem(clip, plain),
    onEdit: () => selected && setEditorOpen(true),
    onDelete: () => selected && setConfirm(selected),
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
        }
      }),
    [applySettings, host, load, reload, tab, showToast],
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
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search clips, apps, file names…"
              aria-label="Search clips"
              spellCheck={false}
            />
            {search ? (
              <button
                className="icon-btn ghost"
                style={{ width: 22, height: 22, border: 0 }}
                onClick={() => setSearch('')}
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
              <button className="icon-btn" onClick={() => setSettingsOpen(true)} aria-label="Settings">
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
          <Tabs.Root
            value={tab === null ? 'all' : String(tab)}
            onValueChange={(value) => selectTab(value === 'all' ? null : Number(value))}
            className="tabs-root"
          >
            <Tabs.List className="tabs" aria-label="Clip tabs">
              {tabs.map((entry) => (
                <Tabs.Trigger key={entry.id ?? 'all'} value={entry.id === null ? 'all' : String(entry.id)} className="tab">
                  {entry.label}
                  <span className="tab-count">{entry.count.toLocaleString()}</span>
                </Tabs.Trigger>
              ))}
            </Tabs.List>
          </Tabs.Root>
          <span className="tabs-hint t-meta">
            <span className="keycap">↑↓</span> move
            <span className="keycap" style={{ marginLeft: 6 }}>Enter</span> paste
          </span>
        </div>

        <main className="card">
          <div className="card-head">
            <div className="card-title">
              <span className="t-title">{tabLabel} clips</span>
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
            <button className="btn sm" onClick={() => setEditorOpen(true)} disabled={!selected} title="Preview the selected clip in the editor">
              <IconEdit />
              Edit clip
              <span className="keycap edit-key">F2</span>
            </button>
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
              onSelect={setSelectedId}
              onCopy={copyItem}
              onTag={(id) => {
                setSearch(`${id}:`)
                focusSearch()
              }}
              getThumb={host.getThumb}
              getItem={host.getItem}
              emptyTitle={
                searching && visible.length === 0
                  ? 'Searching…'
                  : searchActive
                    ? `Nothing matches “${query}”`
                    : tab === null
                      ? 'No clips yet'
                      : 'This tab is empty'
              }
              emptySub={
                searchActive
                  ? 'Try a different search, or clear it with Esc.'
                  : 'Copy something and it will appear here.'
              }
            />
          )}

          <div className="footer">
            <div className="footer-left">
              <span className="t-meta">{searchActive ? `Results for “${query}”` : `${tabLabel} history`}</span>
            </div>
            <span className="spacer" />
            <div className="footer-mid">
              <span className="t-meta">
                {searchActive ? `${(found?.total ?? 0).toLocaleString()} found` : `${tabCount.toLocaleString()} clips`}
              </span>
            </div>
            <div className="footer-right">
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
      />

      <DeleteConfirm clip={confirm} onCancel={() => setConfirm(null)} onConfirm={() => void confirmDelete()} />

      {editorOpen && selected && (
        <EditorOverlay
          clip={selected}
          timeFormat={timeFormat}
          getItem={host.getItem}
          onSave={(html, text) => host.saveEditor({ id: selected.id, html, text })}
          onSaved={() => {
            void reload()
            showToast('Clip saved', `${selected.app} · ${selected.title}`)
          }}
          onClose={() => setEditorOpen(false)}
        />
      )}

      <div className="toast-region" aria-live="polite">
        {toasts.map((toast) => (
          <div className={'toast' + (toast.error ? ' error' : '')} key={toast.id}>
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
