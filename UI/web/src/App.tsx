import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import * as Tabs from '@radix-ui/react-tabs'
import * as Tooltip from '@radix-ui/react-tooltip'
import { ClipList } from './ClipList'
import { EditorOverlay } from './EditorOverlay'
import { SettingsDialog } from './SettingsDialog'
import { createHost, type ClipItem, type InitResult } from './host'
import { formatTime, type TimeFormat } from './format'
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

const messageOf = (error: unknown) => (error instanceof Error ? error.message : String(error))

export default function App() {
  const [host] = useState(createHost)
  const previewUi = host.mode === 'mock'

  const [theme, setTheme] = useState<'light' | 'dark'>(() => (previewUi && params.get('theme') === 'dark' ? 'dark' : 'light'))
  const [tab, setTab] = useState<number | null>(null)
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [searchOpen, setSearchOpen] = useState(true)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [settingsOpen, setSettingsOpen] = useState(previewUi && params.get('surface') === 'settings')
  const [editorOpen, setEditorOpen] = useState(previewUi && params.get('surface') === 'editor')
  const [toasts, setToasts] = useState<Toast[]>([])
  const [init, setInit] = useState<InitResult | null>(null)
  const [items, setItems] = useState<ClipItem[]>([])
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [loadError, setLoadError] = useState('')

  const toastId = useRef(0)
  const tabSeq = useRef(0)
  const searchRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
  }, [theme])

  const showToast = useCallback((title: string, sub: string, error = false) => {
    const id = ++toastId.current
    setToasts((current) => [...current.slice(-2), { id, title, sub, error }])
    window.setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), 2600)
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
        if (result.config?.theme === 'dark' || result.config?.theme === 'light') setTheme(result.config.theme)
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
  }, [host])

  useEffect(() => {
    load()
  }, [load])

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search.trim().toLowerCase()), 120)
    return () => window.clearTimeout(timer)
  }, [search])

  const selectTab = useCallback(
    (next: number | null) => {
      const previous = tab
      setTab(next)
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

  const filtered = useMemo(() => {
    if (!query) return items
    // The real grammar lives in Core\ClipSearch.cs; here only the canonical `id:` form
    // the page can emit is matched, against the tags the host already resolved.
    const tagQuery = /^([a-z0-9-]+):$/.exec(query)
    if (tagQuery) return items.filter((clip) => clip.tags.includes(tagQuery[1]))
    return items.filter(
      (clip) =>
        clip.title.toLowerCase().includes(query) ||
        clip.text.toLowerCase().includes(query) ||
        clip.app.toLowerCase().includes(query) ||
        clip.formats.some((format) => format.toLowerCase().includes(query)),
    )
  }, [items, query])

  useEffect(() => {
    if (selectedId == null) return
    if (filtered.some((clip) => clip.id === selectedId)) return
    setSelectedId(filtered[0]?.id ?? null)
  }, [filtered, selectedId])

  const selected = items.find((clip) => clip.id === selectedId) ?? null
  const tabLabel = tabs.find((entry) => entry.id === tab)?.label ?? 'All'
  const tabCount = tabs.find((entry) => entry.id === tab)?.count ?? filtered.length
  const today = init?.counts?.today
  const timeFormat: TimeFormat = init?.config?.timeFormat === '12h' ? '12h' : '24h'

  const copyItem = useCallback(
    async (clip: ClipItem) => {
      try {
        await host.copy({ id: clip.id, plain: false })
        showToast('Copied to clipboard', `${clip.app || 'Unknown'} · ${clip.title}`)
      } catch (error) {
        showToast('Copy failed', messageOf(error), true)
      }
    },
    [host, showToast],
  )

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (editorOpen || settingsOpen) return
      const el = event.target as HTMLElement
      const typing = el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.isContentEditable
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        setSearchOpen(true)
        window.setTimeout(() => searchRef.current?.focus(), 0)
        return
      }
      if (typing) return
      if (el.closest('button, a, [role="tab"]')) return

      if (event.key === 'F2') {
        if (selected) {
          event.preventDefault()
          setEditorOpen(true)
        }
        return
      }
      if (event.key === 'Enter') {
        if (selected) {
          event.preventDefault()
          void copyItem(selected)
        }
        return
      }
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        if (!filtered.length) return
        event.preventDefault()
        const index = filtered.findIndex((clip) => clip.id === selectedId)
        const next =
          index < 0
            ? 0
            : Math.min(filtered.length - 1, Math.max(0, index + (event.key === 'ArrowDown' ? 1 : -1)))
        setSelectedId(filtered[next].id)
        return
      }
      if (event.key === 'Escape') {
        if (search) setSearch('')
        else setSelectedId(null)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [editorOpen, settingsOpen, selected, selectedId, filtered, search, copyItem])

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
            <span className="keycap" style={{ marginLeft: 6 }}>Enter</span> copy
          </span>
        </div>

        <main className="card">
          <div className="card-head">
            <div className="card-title">
              <span className="t-title">{tabLabel} clips</span>
              <span className="chip">{tabCount.toLocaleString()}</span>
              {query && <span className="chip accent">Filtered</span>}
            </div>
            <span className="spacer" />
            <span className="t-meta shown-count">{filtered.length.toLocaleString()} shown</span>
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
              clips={filtered}
              selectedId={selectedId}
              timeFormat={timeFormat}
              onSelect={setSelectedId}
              onCopy={copyItem}
              onTag={(id) => {
                setSearch(`${id}:`)
                setSearchOpen(true)
                window.setTimeout(() => searchRef.current?.focus(), 0)
              }}
              getThumb={host.getThumb}
              emptyTitle={query ? 'No clips match' : tab === null ? 'No clips yet' : 'This tab is empty'}
              emptySub={
                query
                  ? 'Try a different search or clear it.'
                  : 'Copy something and it will appear here.'
              }
            />
          )}

          <div className="footer">
            <div className="footer-left">
              {selected ? (
                <span className="t-meta sel">
                  <strong style={{ fontWeight: 600, color: 'var(--fg55)' }}>{selected.app || 'Unknown'}</strong> ·{' '}
                  {selected.title} · {formatTime(selected.time, timeFormat)}
                </span>
              ) : (
                <span className="t-meta">Nothing selected</span>
              )}
            </div>
            <span className="spacer" />
            <div className="footer-mid">
              <span className="t-meta">{filtered.length.toLocaleString()} clips</span>
            </div>
            <div className="footer-right">
              <span className="hint">
                <span className="keycap">Esc</span> {search ? 'clear' : 'deselect'}
              </span>
            </div>
          </div>
        </main>
      </div>

      <SettingsDialog open={settingsOpen} onOpenChange={setSettingsOpen} theme={theme} setTheme={setTheme} />

      {editorOpen && selected && (
        <EditorOverlay
          clip={selected}
          timeFormat={timeFormat}
          initialDirty={previewUi && params.get('dirty') === '1'}
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
