import { useEffect, useRef, useState } from 'react'
import type { ClipItem } from './host'
import type { MoveTarget } from './MoveToTab'
import {
  IconCheck,
  IconChevron,
  IconChevronRight,
  IconCopy,
  IconDownload,
  IconEdit,
  IconFolder,
  IconOpen,
  IconTrash,
} from './icons'

interface Props {
  clip: ClipItem
  targets: MoveTarget[]
  x: number
  y: number
  /** False for file lists: the host refuses to save or open them as a document. */
  canSave: boolean
  canOpen: boolean
  onEdit: () => void
  onCopy: () => void
  onSaveAs: () => void
  onDelete: () => void
  onOpenDefault: () => void
  onMove: (tabId: number | null) => void
  onClose: () => void
}

/**
 * One flat menu: Edit, Copy, Save as, Delete, Open in default app, then an
 * inline Move-to-tab section with a tick on the current tab. Everything lives
 * in the same box, so there is no flyout to clip, flip or portal.
 */
export function RowMenu({
  clip,
  targets,
  x,
  y,
  canSave,
  canOpen,
  onEdit,
  onCopy,
  onSaveAs,
  onDelete,
  onOpenDefault,
  onMove,
  onClose,
}: Props) {
  const ref = useRef<HTMLDivElement>(null)
  const moveRef = useRef<HTMLButtonElement>(null)
  const [openDests, setOpenDests] = useState(false)
  /** Keyboard opens carry focus into the destinations after commit; clicks keep it. */
  const focusDests = useRef(false)

  useEffect(() => {
    const onDown = (event: MouseEvent) => {
      if (!ref.current?.contains(event.target as Node)) onClose()
    }
    window.addEventListener('mousedown', onDown)
    return () => window.removeEventListener('mousedown', onDown)
  }, [onClose])

  useEffect(() => {
    ref.current?.querySelector<HTMLElement>('[role="menuitem"]')?.focus()
  }, [])

  useEffect(() => {
    if (openDests && focusDests.current) {
      focusDests.current = false
      ref.current?.querySelector<HTMLElement>('.ctx-dests [role="menuitemradio"]')?.focus()
    }
  }, [openDests ])

  const openSection = (moveFocus: boolean) => {
    if (openDests) {
      if (moveFocus) ref.current?.querySelector<HTMLElement>('.ctx-dests [role="menuitemradio"]')?.focus()
      return
    }
    if (moveFocus) focusDests.current = true
    setOpenDests(true)
  }

  const closeSection = () => {
    setOpenDests(false)
    moveRef.current?.focus()
  }

  const walk = (direction: 1 | -1) => {
    const rows = Array.from(
      ref.current?.querySelectorAll<HTMLElement>('[role="menuitem"], [role="menuitemradio"]') ?? [],
    ).filter((row) => row.offsetParent !== null)
    if (rows.length === 0) return
    const at = rows.indexOf(document.activeElement as HTMLElement)
    rows[(Math.max(at, 0) + direction + rows.length) % rows.length]?.focus()
  }

  const onKey = (event: React.KeyboardEvent<HTMLDivElement>) => {
    const target = event.target as HTMLElement
    const inDests = target.closest('.ctx-dests') != null
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      walk(event.key === 'ArrowDown' ? 1 : -1)
      return
    }
    if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault()
      const rows = Array.from(
        ref.current?.querySelectorAll<HTMLElement>('[role="menuitem"], [role="menuitemradio"]') ?? [],
      ).filter((row) => row.offsetParent !== null)
      ;(event.key === 'Home' ? rows[0] : rows[rows.length - 1])?.focus()
      return
    }
    if ((event.key === 'ArrowRight' || event.key === 'Enter') && target.dataset.move === 'toggle') {
      event.preventDefault()
      openSection(true)
      return
    }
    if (event.key === 'ArrowLeft' && (inDests || target.dataset.move === 'toggle')) {
      event.preventDefault()
      event.stopPropagation()
      if (openDests) closeSection()
      return
    }
    if (event.key === 'Escape') {
      event.preventDefault()
      onClose()
    }
  }

  const item = (key: string, label: string, icon: React.ReactNode, action: () => void, hint?: string, danger?: boolean) => (
    <button
      key={key}
      type="button"
      role="menuitem"
      className={'ctx-item' + (danger ? ' danger' : '')}
      onClick={action}
    >
      <span className="ctx-icon">{icon}</span>
      <span className="ctx-label">{label}</span>
      {hint && <span className="keycap">{hint}</span>}
    </button>
  )

  return (
    <div
      ref={ref}
      className="popover ctx-menu"
      role="menu"
      aria-label="Clip actions"
      style={{
        left: Math.max(8, Math.min(x, window.innerWidth - 256)),
        top: Math.max(8, Math.min(y, window.innerHeight - 320)),
      }}
      onKeyDown={onKey}
    >
      <div className="ctx-title" title={`${clip.app} · ${clip.title}`}>
        {clip.app} · {clip.title || 'Untitled clip'}
      </div>
      {item('edit', 'Edit', <IconEdit />, onEdit, 'F2')}
      {item('copy', 'Copy', <IconCopy />, onCopy)}
      {canSave && item('save', 'Save as', <IconDownload />, onSaveAs, 'Ctrl E')}
      {item('delete', 'Delete', <IconTrash />, onDelete, 'Del', true)}
      {canOpen && <div className="ctx-sep" role="separator" />}
      {canOpen && item('open', 'Open in default app', <IconOpen />, onOpenDefault)}
      <div className="ctx-sep" role="separator" />
      <button
        ref={moveRef}
        type="button"
        role="menuitem"
        aria-haspopup="true"
        aria-expanded={openDests}
        className="ctx-item"
        data-move="toggle"
        onClick={() => (openDests ? setOpenDests(false) : openSection(false))}
      >
        <span className="ctx-icon">
          <IconFolder />
        </span>
        <span className="ctx-label">Move to tab</span>
        <span className="ctx-chevron">{openDests ? <IconChevron /> : <IconChevronRight />}</span>
      </button>
      {openDests && (
        <div className="ctx-dests" role="group" aria-label="Move clip to tab">
          {targets.map((target) => {
            const current = target.id === clip.tabId
            return (
              <button
                key={target.id ?? 'all'}
                type="button"
                role="menuitemradio"
                aria-checked={current}
                className="ctx-item"
                onClick={() => onMove(target.id)}
              >
                <span className="ctx-icon">{current ? <IconCheck /> : null}</span>
                <span className="ctx-label">{target.label}</span>
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
