import { useEffect, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import * as Tabs from '@radix-ui/react-tabs'
import { IconPlus, IconWarning } from './icons'
import { messageOf } from './host'

export interface StripTab {
  id: number | null
  label: string
  count: number
  kind: 'all' | 'pinned' | 'custom' | 'images'
}

interface Props {
  tabs: StripTab[]
  activeId: number | null
  onSelect: (id: number | null) => void
  onCreate: (name: string) => Promise<void>
  onRename: (id: number, name: string) => Promise<void>
  /** Resolves the host's deletedClips; the page reports that number, never the pre-delete count. */
  onDelete: (tab: StripTab) => Promise<number>
  /** Persists the custom-tab strip order after a drag; Pinned and the virtual tabs never move. */
  onReorder: (order: number[]) => Promise<void>
  /** True while the field, tab menu or delete confirm owns the keyboard; the window key matrix stands down. */
  onOverlayChange: (open: boolean) => void
}

type EditState = { mode: 'create' } | { mode: 'rename'; id: number }

/**
 * The tab strip: one horizontal scroller holding the tabs, the inline name field and `+`.
 * Radix Tabs is in manual activation mode, so arrows move the roving focus and Enter/Space activates.
 */
export function TabStrip({ tabs, activeId, onSelect, onCreate, onRename, onDelete, onReorder, onOverlayChange }: Props) {
  const [edit, setEdit] = useState<EditState | null>(null)
  const [value, setValue] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [dragId, setDragId] = useState<number | null>(null)
  const [dropId, setDropId] = useState<number | null>(null)
  const [menu, setMenu] = useState<{ tab: StripTab; x: number; y: number } | null>(null)
  const [confirm, setConfirm] = useState<StripTab | null>(null)
  const [confirmError, setConfirmError] = useState('')
  const [deleting, setDeleting] = useState(false)
  /** `undefined` = no tab has focus, so the active tab owns the single tab stop. */
  const [focusId, setFocusId] = useState<number | null | undefined>(undefined)
  const inputRef = useRef<HTMLInputElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  /** The tab the menu was opened on; focus returns there when the confirm closes. */
  const confirmReturn = useRef<HTMLElement | null>(null)

  useEffect(
    () => onOverlayChange(edit != null || menu != null || confirm != null),
    [edit, menu, confirm, onOverlayChange],
  )

  useEffect(() => {
    if (!edit) return
    const input = inputRef.current
    if (!input) return
    input.focus()
    if (edit.mode === 'rename') input.select()
    // The field sits at the end of the scroller; bring field + error into view without moving the page.
    input.parentElement?.scrollIntoView({ block: 'nearest', inline: 'nearest' })
  }, [edit])

  const beginCreate = () => {
    setError('')
    setValue('')
    setEdit({ mode: 'create' })
    setMenu(null)
  }

  const beginRename = (tab: StripTab) => {
    if (tab.kind !== 'custom' || tab.id == null) return
    setError('')
    setValue(tab.label)
    setEdit({ mode: 'rename', id: tab.id })
    setMenu(null)
  }

  const cancelEdit = () => {
    if (busy) return
    setEdit(null)
    setError('')
  }

  const submit = async () => {
    if (!edit || busy) return
    setBusy(true)
    setError('')
    try {
      if (edit.mode === 'create') await onCreate(value)
      else await onRename(edit.id, value)
      setEdit(null)
    } catch (failure) {
      // The host wrote these words for the owner; show them unchanged and keep the field open.
      setError(messageOf(failure))
      window.setTimeout(() => inputRef.current?.focus(), 0)
    } finally {
      setBusy(false)
    }
  }

  const onEditKey = (event: ReactKeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault()
      void submit()
    } else if (event.key === 'Escape') {
      // The window key matrix would otherwise clear the search or hide the app.
      event.preventDefault()
      event.stopPropagation()
      cancelEdit()
    }
  }

  const openMenu = (tab: StripTab, event: React.MouseEvent) => {
    if (tab.kind !== 'custom') return
    event.preventDefault()
    event.stopPropagation()
    confirmReturn.current = event.currentTarget as HTMLElement
    setMenu({ tab, x: event.clientX, y: event.clientY })
  }

  const deleteTab = async () => {
    if (!confirm || deleting) return
    setDeleting(true)
    setConfirmError('')
    try {
      await onDelete(confirm)
      setConfirm(null)
    } catch (failure) {
      setConfirmError(messageOf(failure))
    } finally {
      setDeleting(false)
    }
  }

  /**
   * Drops the dragged custom tab before the target one and persists that order. Pinned and the
   * virtual Inbox/Images tabs are never draggable, so they never reach this path.
   */
  const commitReorder = async (from: number, to: number) => {
    const customs = tabs.filter((tab) => tab.kind === 'custom' && tab.id != null).map((tab) => tab.id as number)
    if (!customs.includes(from) || !customs.includes(to) || from === to) return
    const without = customs.filter((id) => id !== from)
    without.splice(without.indexOf(to), 0, from)
    setDragId(null)
    setDropId(null)
    // The page toasts a failure; the strip only needs its indicator cleared, which is done above.
    await onReorder(without)
  }

  // The tab menu is a tiny fixed popover: Esc closes, arrows walk its two items, a click outside closes.
  useEffect(() => {
    if (!menu) return
    const onDown = (event: MouseEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) setMenu(null)
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        // The window key matrix re-subscribes on close; without this the same keystroke hides the app.
        event.stopPropagation()
        setMenu(null)
        return
      }
      if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return
      event.preventDefault()
      const items = Array.from(menuRef.current?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])
      if (items.length === 0) return
      const at = items.indexOf(document.activeElement as HTMLElement)
      items[(at + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length]?.focus()
    }
    window.addEventListener('mousedown', onDown)
    window.addEventListener('keydown', onKey)
    window.setTimeout(() => menuRef.current?.querySelector<HTMLElement>('[role="menuitem"]')?.focus(), 0)
    return () => {
      window.removeEventListener('mousedown', onDown)
      window.removeEventListener('keydown', onKey)
    }
  }, [menu])

  const confirmText = confirm
    ? `Its ${confirm.count.toLocaleString()} ${confirm.count === 1 ? 'clip' : 'clips'} will be deleted, and this cannot be undone. Pinned clips are not affected.`
    : ''

  return (
    <>
      <Tabs.Root
        className="tabs-root"
        value={activeId === null ? 'all' : String(activeId)}
        onValueChange={(next) => onSelect(next === 'all' ? null : Number(next))}
        activationMode="manual"
      >
        <div className="tabs">
          <div className="tabs-scroll">
            <Tabs.List className="tabs-list" aria-label="Clip tabs">
              {tabs.map((tab) => {
                const editing = edit?.mode === 'rename' && edit.id === tab.id
                if (editing) {
                  return (
                    <span className="tab-edit" key={tab.id ?? 'all'}>
                      <input
                        ref={inputRef}
                        className="tab-edit-input"
                        value={value}
                        onChange={(event) => setValue(event.target.value)}
                        onKeyDown={onEditKey}
                        aria-label="Tab name"
                        aria-invalid={error ? true : undefined}
                        spellCheck={false}
                        disabled={busy}
                      />
                      {error && <span className="tab-edit-error">{error}</span>}
                    </span>
                  )
                }
                return (
                  <Tabs.Trigger
                    key={tab.id ?? 'all'}
                    value={tab.id === null ? 'all' : String(tab.id)}
                    className={
                      'tab' +
                      // Inbox's id is null, same as the untouched drag state: compare only once a drag exists.
                      (dragId != null && dragId === tab.id ? ' dragging' : '') +
                      (dropId != null && dropId === tab.id && dragId !== tab.id ? ' drop-before' : '')
                    }
                    draggable={tab.kind === 'custom'}
                    onDragStart={(event) => {
                      if (tab.kind !== 'custom' || tab.id == null) return
                      event.dataTransfer.effectAllowed = 'move'
                      event.dataTransfer.setData('text/plain', String(tab.id))
                      setDragId(tab.id)
                    }}
                    onDragOver={(event) => {
                      if (dragId == null || tab.kind !== 'custom' || tab.id == null || tab.id === dragId) return
                      event.preventDefault()
                      event.dataTransfer.dropEffect = 'move'
                      if (dropId !== tab.id) setDropId(tab.id)
                    }}
                    onDragLeave={() => {
                      if (dropId === tab.id) setDropId(null)
                    }}
                    onDrop={(event) => {
                      event.preventDefault()
                      if (dragId != null && tab.id != null) void commitReorder(dragId, tab.id)
                    }}
                    onDragEnd={() => {
                      setDragId(null)
                      setDropId(null)
                    }}
                    // Radix leaves the tab stop on the list until something is focused; these keep one tab tabbable.
                    tabIndex={(focusId === undefined ? tab.id === activeId : focusId === tab.id) ? 0 : -1}
                    onFocus={() => setFocusId(tab.id)}
                    onBlur={(event) => {
                      if (!(event.relatedTarget as HTMLElement | null)?.closest?.('.tab')) setFocusId(undefined)
                    }}
                    onDoubleClick={() => beginRename(tab)}
                    onContextMenu={(event) => openMenu(tab, event)}
                  >
                    <span className="tab-label" title={tab.label}>
                      {tab.label}
                    </span>
                    <span className="tab-count">{tab.count.toLocaleString()}</span>
                  </Tabs.Trigger>
                )
              })}
              {edit?.mode === 'create' && (
                <span className="tab-edit">
                  <input
                    ref={inputRef}
                    className="tab-edit-input"
                    value={value}
                    onChange={(event) => setValue(event.target.value)}
                    onKeyDown={onEditKey}
                    placeholder="Tab name"
                    aria-label="New tab name"
                    aria-invalid={error ? true : undefined}
                    spellCheck={false}
                    disabled={busy}
                  />
                  {error && <span className="tab-edit-error">{error}</span>}
                </span>
              )}
            </Tabs.List>
          </div>
          <button
            type="button"
            className="tab-add"
            aria-label="New tab"
            title="New tab"
            onClick={beginCreate}
          >
            <IconPlus />
          </button>
        </div>
      </Tabs.Root>

      {menu && (
        <div
          ref={menuRef}
          className="popover tab-menu"
          role="menu"
          aria-label={`${menu.tab.label} tab`}
          style={{ left: Math.min(menu.x, window.innerWidth - 196), top: Math.min(menu.y, window.innerHeight - 92) }}
        >
          <button className="pop-item" role="menuitem" onClick={() => beginRename(menu.tab)}>
            Rename…
          </button>
          <button
            className="pop-item danger"
            role="menuitem"
            onClick={() => {
              setConfirmError('')
              setConfirm(menu.tab)
              setMenu(null)
            }}
          >
            Delete tab…
          </button>
        </div>
      )}

      {confirm && (
        <Dialog.Root open onOpenChange={(open) => { if (!open && !deleting) setConfirm(null) }}>
          <Dialog.Portal>
            <Dialog.Overlay className="overlay" />
            <Dialog.Content
              className="dialog confirm"
              aria-describedby="tab-delete-desc"
              aria-modal="true"
              onEscapeKeyDown={(event) => {
                // Radix still closes; this only keeps the same keystroke from the window key matrix.
                event.stopPropagation()
              }}
              onCloseAutoFocus={(event) => {
                // The context menu is gone; put focus back on the tab that opened it.
                event.preventDefault()
                const target = confirmReturn.current?.isConnected
                  ? confirmReturn.current
                  : document.querySelector<HTMLElement>('.tab[data-state="active"]')
                target?.focus()
              }}
            >
              <div className="dialog-head">
                <span className="confirm-icon" aria-hidden="true">
                  <IconWarning />
                </span>
                <div className="grow">
                  <Dialog.Title className="t-title" style={{ margin: 0 }}>
                    Delete “{confirm.label}”?
                  </Dialog.Title>
                  <Dialog.Description id="tab-delete-desc" className="t-sec" style={{ marginTop: 4 }}>
                    {confirmText}
                  </Dialog.Description>
                  {confirmError && <div className="set-error">{confirmError}</div>}
                </div>
              </div>
              <div className="dialog-foot">
                <Dialog.Close asChild>
                  <button className="btn ghost" autoFocus disabled={deleting}>
                    Cancel
                  </button>
                </Dialog.Close>
                <button className="btn danger" disabled={deleting} onClick={() => void deleteTab()}>
                  Delete tab
                </button>
              </div>
            </Dialog.Content>
          </Dialog.Portal>
        </Dialog.Root>
      )}
    </>
  )
}
