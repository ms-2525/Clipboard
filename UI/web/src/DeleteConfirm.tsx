import * as Dialog from '@radix-ui/react-dialog'
import type { ClipItem } from './host'
import { IconWarning } from './icons'

interface Props {
  clip: ClipItem | null
  onCancel: () => void
  onConfirm: () => void
}

/**
 * Delete is destructive, so it always asks first and focuses Cancel - Enter after `Delete`
 * cancels, it never removes a clip that was still being read.
 */
export function DeleteConfirm({ clip, onCancel, onConfirm }: Props) {
  return (
    <Dialog.Root open={clip != null} onOpenChange={(open) => { if (!open) onCancel() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content className="dialog confirm" aria-describedby="delete-desc">
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
