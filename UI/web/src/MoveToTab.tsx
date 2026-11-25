import { useEffect, useRef, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import type { ClipItem, HostTab } from './host'

export interface MoveTarget {
  id: number | null
  label: string
}

/** The picker's order: main history first, custom tabs in strip order, Pinned last. */
export function moveTargets(tabs: HostTab[]): MoveTarget[] {
  const targets: MoveTarget[] = [{ id: null, label: 'All history' }]
  for (const tab of tabs) if (tab.kind === 'custom') targets.push({ id: tab.id, label: tab.name })
  const pinned = tabs.find((tab) => tab.kind === 'pinned')
  if (pinned) targets.push({ id: pinned.id, label: 'Pinned' })
  return targets
}

function MoveList({
  targets,
  currentTabId,
  onChoose,
}: {
  targets: MoveTarget[]
  currentTabId: number | null
  onChoose: (tabId: number | null) => void
}) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    ref.current?.querySelector<HTMLElement>('[role="menuitem"]')?.focus()
  }, [])

  const onKey = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return
    event.preventDefault()
    const items = Array.from(ref.current?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])
    if (items.length === 0) return
    const at = items.indexOf(document.activeElement as HTMLElement)
    items[(at + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length]?.focus()
  }

  return (
    <div ref={ref} className="move-list" role="menu" aria-label="Move clip to tab" onKeyDown={onKey}>
      {targets.map((target) => {
        const current = target.id === currentTabId
        return (
          <button
            key={target.id ?? 'all'}
            type="button"
            role="menuitem"
            className="pop-item"
            onClick={() => onChoose(target.id)}
          >
            {target.label}
            {current && <span className="move-current">current</span>}
          </button>
        )
      })}
    </div>
  )
}

/** Right-click menu: one job, the destination list, never a second dialog on top of the list. */
export function MoveMenu({
  clip,
  targets,
  x,
  y,
  onChoose,
  onClose,
}: {
  clip: ClipItem
  targets: MoveTarget[]
  x: number
  y: number
  onChoose: (tabId: number | null) => void
  onClose: () => void
}) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onDown = (event: MouseEvent) => {
      if (!ref.current?.contains(event.target as Node)) onClose()
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
      }
    }
    window.addEventListener('mousedown', onDown)
    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('mousedown', onDown)
      window.removeEventListener('keydown', onKey)
    }
  }, [onClose])

  const height = Math.min(window.innerHeight - 16, 66 + targets.length * 26)
  return (
    <div
      ref={ref}
      className="popover move-menu"
      style={{
        left: Math.max(8, Math.min(x, window.innerWidth - 228)),
        top: Math.max(8, Math.min(y, window.innerHeight - height - 8)),
      }}
    >
      <div className="move-title">Move to tab</div>
      <div className="move-sub" title={`${clip.app} · ${clip.title}`}>
        {clip.app} · {clip.title || 'Untitled clip'}
      </div>
      <MoveList targets={targets} currentTabId={clip.tabId} onChoose={onChoose} />
    </div>
  )
}

/** Ctrl+M: the same destinations as a small, focus-trapped picker. */
export function MovePicker({
  clip,
  targets,
  onChoose,
  onClose,
}: {
  clip: ClipItem | null
  targets: MoveTarget[]
  onChoose: (tabId: number | null) => void
  onClose: () => void
}) {
  return (
    <Dialog.Root open={clip != null} onOpenChange={(open) => { if (!open) onClose() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content className="dialog move-picker" aria-describedby="move-desc">
          <div className="dialog-head">
            <div className="grow">
              <Dialog.Title className="t-title" style={{ margin: 0 }}>
                Move clip to tab
              </Dialog.Title>
              <Dialog.Description id="move-desc" className="t-sec" style={{ marginTop: 4 }}>
                {clip ? `${clip.app} · ${clip.title || 'Untitled clip'}` : ''}
              </Dialog.Description>
            </div>
          </div>
          <div className="dialog-body">
            {clip && (
              <MoveList
                targets={targets}
                currentTabId={clip.tabId}
                onChoose={(tabId) => {
                  onChoose(tabId)
                  onClose()
                }}
              />
            )}
          </div>
          <div className="dialog-foot">
            <span className="t-meta grow">↑↓ choose · Enter move · Esc close</span>
            <Dialog.Close asChild>
              <button className="btn ghost">Cancel</button>
            </Dialog.Close>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
