import { useEffect, useRef, type KeyboardEvent as ReactKeyboardEvent, type RefObject } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import type { ClipItem, HostTab } from './host'

export interface MoveTarget {
  id: number | null
  label: string
}

/** The picker's order: main history first, custom tabs in strip order, Pinned last. */
export function moveTargets(tabs: HostTab[]): MoveTarget[] {
  const targets: MoveTarget[] = [{ id: null, label: 'Inbox' }]
  for (const tab of tabs) if (tab.kind === 'custom') targets.push({ id: tab.id, label: tab.name })
  const pinned = tabs.find((tab) => tab.kind === 'pinned')
  if (pinned) targets.push({ id: pinned.id, label: 'Pinned' })
  return targets
}

/** One menu row: Tab move and Tag filter share the same picker grammar. */
export interface PickerItem {
  key: string
  label: string
  /** Right-aligned muted note, e.g. the current destination. */
  note?: string
}

/** The arrow-key list behind Ctrl+M and Ctrl+T: first item focused, Enter picks, Esc is the dialog's. */
export function PickerList({
  label,
  items,
  onChoose,
}: {
  label: string
  items: PickerItem[]
  onChoose: (key: string) => void
}) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    ref.current?.querySelector<HTMLElement>('[role="menuitem"]')?.focus()
  }, [])

  const onKey = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return
    event.preventDefault()
    const rows = Array.from(ref.current?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])
    if (rows.length === 0) return
    const at = rows.indexOf(document.activeElement as HTMLElement)
    rows[(at + (event.key === 'ArrowDown' ? 1 : -1) + rows.length) % rows.length]?.focus()
  }

  return (
    <div ref={ref} className="move-list" role="menu" aria-label={label} onKeyDown={onKey}>
      {items.map((item) => (
        <button key={item.key} type="button" role="menuitem" className="pop-item" onClick={() => onChoose(item.key)}>
          {item.label}
          {item.note && <span className="move-current">{item.note}</span>}
        </button>
      ))}
    </div>
  )
}

/** Target ids travel as strings so the list stays generic; "all" is the only non-numeric key. */
const moveItems = (targets: MoveTarget[], currentTabId: number | null): PickerItem[] =>
  targets.map((target) => ({
    key: target.id == null ? 'all' : String(target.id),
    label: target.label,
    note: target.id === currentTabId ? 'current' : undefined,
  }))

const moveKeyToId = (key: string) => (key === 'all' ? null : Number(key))

/** Ctrl+M: the same destinations as a small, focus-trapped picker. */
export function MovePicker({
  clip,
  targets,
  onChoose,
  onClose,
  returnFocus,
}: {
  clip: ClipItem | null
  targets: MoveTarget[]
  onChoose: (tabId: number | null) => void
  onClose: () => void
  /** The row that opened the picker; focus returns there on close. */
  returnFocus?: RefObject<HTMLElement | null>
}) {
  return (
    <Dialog.Root open={clip != null} onOpenChange={(open) => { if (!open) onClose() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content
          className="dialog move-picker"
          aria-describedby="move-desc"
          aria-modal="true"
          onEscapeKeyDown={(event) => {
            // Radix still closes; this only keeps the same keystroke from the window key matrix.
            event.stopPropagation()
          }}
          onCloseAutoFocus={(event) => {
            event.preventDefault()
            const target = returnFocus?.current?.isConnected
              ? returnFocus.current
              : document.querySelector<HTMLElement>('.row[tabindex="0"]')
            target?.focus()
          }}
        >
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
              <PickerList
                label="Move clip to tab"
                items={moveItems(targets, clip.tabId)}
                onChoose={(key) => {
                  onChoose(moveKeyToId(key))
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
