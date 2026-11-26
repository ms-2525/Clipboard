import { useRef } from 'react'
import type { RefObject } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import type { ClipItem } from './host'
import { IconWarning } from './icons'

interface Props {
  clips: ClipItem[] | null
  onCancel: () => void
  onConfirm: () => void
  /** The row that opened the dialog; focus returns there on close. */
  returnFocus?: RefObject<HTMLElement | null>
}

/**
 * Multi-delete asks once; a single delete never opens this dialog.
 * Left/Right/Up/Down cycle between Cancel and Delete so the popup stays keyboard-only.
 */
export function DeleteConfirm({ clips, onCancel, onConfirm, returnFocus }: Props) {
  const count = clips?.length ?? 0
  const footRef = useRef<HTMLDivElement>(null)

  /** Two buttons, one cycle: arrows wrap so the choice never needs Tab. */
  const cycleButtons = (event: React.KeyboardEvent) => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight' && event.key !== 'ArrowUp' && event.key !== 'ArrowDown') return
    event.preventDefault()
    const buttons = Array.from(footRef.current?.querySelectorAll<HTMLButtonElement>('button') ?? [])
    if (buttons.length === 0) return
    const at = buttons.indexOf(document.activeElement as HTMLButtonElement)
    const direction = event.key === 'ArrowRight' || event.key === 'ArrowDown' ? 1 : -1
    const next = at < 0 ? (direction > 0 ? 0 : buttons.length - 1) : (at + direction + buttons.length) % buttons.length
    buttons[next]?.focus()
  }

  return (
    <Dialog.Root open={count > 1} onOpenChange={(open) => { if (!open) onCancel() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content
          className="dialog confirm"
          aria-describedby="delete-desc"
          aria-modal="true"
          onKeyDown={cycleButtons}
          onEscapeKeyDown={(event) => {
            // Radix still closes; this only keeps the same keystroke from the window key matrix.
            event.stopPropagation()
          }}
          onCloseAutoFocus={(event) => {
            event.preventDefault()
            // Deleted rows are gone; the selected row is the next home for focus.
            const target = returnFocus?.current?.isConnected
              ? returnFocus.current
              : document.querySelector<HTMLElement>('.row[tabindex="0"]')
            target?.focus()
          }}
        >
          <div className="dialog-head">
            <span className="confirm-icon" aria-hidden="true">
              <IconWarning />
            </span>
            <div className="grow">
              <Dialog.Title className="t-title" style={{ margin: 0 }}>
                {`Delete ${count} clips?`}
              </Dialog.Title>
              <Dialog.Description id="delete-desc" className="t-sec" style={{ marginTop: 4 }}>
                {`${count} clips will be deleted. Press Ctrl+Z to bring them back.`}
              </Dialog.Description>
            </div>
          </div>
          <div className="dialog-foot" ref={footRef}>
            <Dialog.Close asChild>
              <button className="btn ghost" autoFocus>Cancel</button>
            </Dialog.Close>
            <button className="btn danger" onClick={onConfirm}>
              Delete
            </button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
