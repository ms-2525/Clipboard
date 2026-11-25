import type { RefObject } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import type { ClipItem } from './host'
import { IconWarning } from './icons'

interface Props {
  clip: ClipItem | null
  onCancel: () => void
  onConfirm: () => void
  /** The row that opened the dialog; focus returns there on close. */
  returnFocus?: RefObject<HTMLElement | null>
}

/**
 * Delete is destructive, so it always asks first and focuses Cancel - Enter after `Delete`
 * cancels, it never removes a clip that was still being read.
 */
export function DeleteConfirm({ clip, onCancel, onConfirm, returnFocus }: Props) {
  return (
    <Dialog.Root open={clip != null} onOpenChange={(open) => { if (!open) onCancel() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content
          className="dialog confirm"
          aria-describedby="delete-desc"
          aria-modal="true"
          onCloseAutoFocus={(event) => {
            event.preventDefault()
            // A deleted row is gone; the selected row is the next home for focus.
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
                Delete this clip?
              </Dialog.Title>
              <Dialog.Description id="delete-desc" className="t-sec" style={{ marginTop: 4 }}>
                {clip ? `${clip.app} · ${clip.title || 'Untitled clip'}` : ''}
              </Dialog.Description>
            </div>
          </div>
          <div className="dialog-foot">
            <Dialog.Close asChild>
              <button className="btn ghost">Cancel</button>
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
