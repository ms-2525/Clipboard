import type { RefObject } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import type { ClipItem } from './host'
import { formatStamp, type TimeFormat } from './format'
import { IconInfo } from './icons'

/** 1234 B → "1.2 KB"; the Info dialog and the storage notice share the one shape. */
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  const rounded = value >= 100 ? Math.round(value) : Math.round(value * 10) / 10
  return `${rounded} ${units[unit]}`
}

/** What the store holds for this clip: the recorded formats, or the flags when those are empty. */
function storedAs(clip: ClipItem): string {
  if (clip.formats.length > 0) return clip.formats.join(', ')
  const parts: string[] = []
  if (clip.hasHtml) parts.push('HTML')
  if (clip.hasRtf) parts.push('RTF')
  if (clip.hasImage) parts.push('Image')
  if (parts.length === 0) parts.push(clip.kind === 'files' ? 'Files' : 'Text')
  return parts.join(', ')
}

/** Provenance plus a readable slice of the clip: the row truncates, this dialog does not. */
export function ClipInfo({
  clip,
  timeFormat,
  onClose,
  returnFocus,
}: {
  clip: ClipItem | null
  timeFormat: TimeFormat
  onClose: () => void
  /** The row that opened the dialog; focus returns there on close. */
  returnFocus?: RefObject<HTMLElement | null>
}) {
  return (
    <Dialog.Root open={clip != null} onOpenChange={(open) => { if (!open) onClose() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content
          className="dialog confirm"
          aria-describedby="clipinfo-desc"
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
            <span className="info-icon" aria-hidden="true">
              <IconInfo />
            </span>
            <div className="grow">
              <Dialog.Title className="t-title" style={{ margin: 0 }}>
                Clip info
              </Dialog.Title>
              <Dialog.Description id="clipinfo-desc" className="t-sec" style={{ marginTop: 2 }}>
                {clip ? `${clip.app} · ${formatStamp(clip.time, timeFormat)}` : 'Clip details'}
              </Dialog.Description>
            </div>
          </div>
          <div className="dialog-body">
            {clip && (
              <dl className="info-grid">
                <div className="info-row">
                  <dt>From</dt>
                  <dd>{clip.app}</dd>
                </div>
                <div className="info-row">
                  <dt>Window</dt>
                  <dd>{clip.title || 'Untitled clip'}</dd>
                </div>
                <div className="info-row">
                  <dt>Captured</dt>
                  <dd>{formatStamp(clip.time, timeFormat)}</dd>
                </div>
                <div className="info-row">
                  <dt>Size</dt>
                  <dd>{formatBytes(clip.sizeBytes)}</dd>
                </div>
                <div className="info-row">
                  <dt>Stored as</dt>
                  <dd>{storedAs(clip)}</dd>
                </div>
                <div className="info-row">
                  <dt>Tags</dt>
                  <dd>
                    {clip.tags.length === 0 ? (
                      <span className="t-sec">No tags</span>
                    ) : (
                      <span className="info-tags">
                        {clip.tags.map((id) => (
                          <span key={id} className="chip tag">
                            {id}
                          </span>
                        ))}
                      </span>
                    )}
                  </dd>
                </div>
              </dl>
            )}
          </div>
          <div className="dialog-foot clipinfo-foot">
            <span className="t-meta">Esc close</span>
            <Dialog.Close asChild>
              <button className="btn ghost">Close</button>
            </Dialog.Close>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}

/**
 * The host's advisory "storage crossed the limit" notice (owner decision 25 Nov: copies are never
 * refused). The page shows it once per run; the host re-emits on every open until space is cleared.
 */
export function StorageNotice({
  notice,
  onClose,
}: {
  notice: { bytes: number; limit: number } | null
  onClose: () => void
}) {
  return (
    <Dialog.Root open={notice != null} onOpenChange={(open) => { if (!open) onClose() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content
          className="dialog confirm"
          aria-describedby="storage-desc"
          aria-modal="true"
          onEscapeKeyDown={(event) => {
            event.stopPropagation()
          }}
          onCloseAutoFocus={(event) => {
            event.preventDefault()
            document.querySelector<HTMLElement>('.row[tabindex="0"]')?.focus()
          }}
        >
          <div className="dialog-head">
            <span className="confirm-icon" style={{ color: 'var(--fg55)' }} aria-hidden="true">
              <IconInfo />
            </span>
            <div className="grow">
              <Dialog.Title className="t-title" style={{ margin: 0 }}>
                Clip storage is full
              </Dialog.Title>
              <Dialog.Description id="storage-desc" className="t-sec" style={{ marginTop: 4 }}>
                {notice
                  ? `Your clips use ${formatBytes(notice.bytes)} of the ${formatBytes(notice.limit)} storage limit.`
                  : ''}
              </Dialog.Description>
            </div>
          </div>
          <div className="dialog-body" style={{ paddingTop: 6 }}>
            <div className="set-row">
              <span className="grow" style={{ wordBreak: 'break-word' }}>
                New copies are still saved — nothing is lost. To free space, right-click clips you no
                longer need and choose Delete, or use Clear history in Settings → Storage.
              </span>
            </div>
          </div>
          <div className="dialog-foot">
            <span className="t-meta grow">Esc close</span>
            <Dialog.Close asChild>
              <button className="btn primary">OK</button>
            </Dialog.Close>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
