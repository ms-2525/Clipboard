import type { RefObject } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import type { ClipItem } from './host'
import { PickerList, type PickerItem } from './MoveToTab'

/** The ids Core\ClipSearch.cs treats as `code:`; every other id filters as itself. */
const LANGUAGES = ['csharp', 'typescript', 'javascript', 'python', 'rust', 'php', 'go', 'java', 'shell']

/** The search token for a canonical tag id: `spreadsheet:`, `code:` for any language, else `id:`. */
export function tagToken(tag: string): string {
  return LANGUAGES.includes(tag) ? 'code:' : `${tag}:`
}

/** The clip's tags as query tokens, in first-seen order, without duplicates (`code:` once). */
export function tagTokens(tags: string[]): string[] {
  return [...new Set(tags.map(tagToken))]
}

/** Host grammar (ClipSearch.IsTagShaped): a letter, then 1+ letters/digits/-/_, then the colon. */
const isTagToken = (word: string) => /^[A-Za-z][A-Za-z0-9_-]+:$/.test(word)

/** Tag filters already in the query; a negated `-foo:` counts, a drive `C:` does not. */
export function activeTagTokens(query: string): string[] {
  return query.split(/\s+/).filter((word) => isTagToken(word.replace(/^-/, '')))
}

/** The query without its tag filters, keeping every plain term the owner typed. */
export function clearTagTokens(query: string): string {
  return query
    .split(/\s+/)
    .filter((word) => word && !isTagToken(word.replace(/^-/, '')))
    .join(' ')
}

/** Ctrl+T: the selected clip's tags as keyboard-reachable filter tokens. */
export function TagPicker({
  clip,
  query,
  onChoose,
  onClose,
  returnFocus,
}: {
  clip: ClipItem | null
  query: string
  onChoose: (token: string | null) => void
  onClose: () => void
  /** The row that opened the picker; focus returns there on close. */
  returnFocus?: RefObject<HTMLElement | null>
}) {
  const tokens = clip ? tagTokens(clip.tags) : []
  const active = activeTagTokens(query)
  const items: PickerItem[] = tokens.map((token) => ({
    key: token,
    label: token,
    note: active.includes(token) ? 'active' : undefined,
  }))
  if (active.length > 0) items.push({ key: '', label: 'Clear tag filter' })

  return (
    <Dialog.Root open={clip != null} onOpenChange={(open) => { if (!open) onClose() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content
          className="dialog move-picker"
          aria-describedby="tag-desc"
          aria-modal="true"
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
                Filter by tag
              </Dialog.Title>
              <Dialog.Description id="tag-desc" className="t-sec" style={{ marginTop: 4 }}>
                {clip ? `${clip.app} · ${clip.title || 'Untitled clip'}` : ''}
              </Dialog.Description>
            </div>
          </div>
          <div className="dialog-body">
            {clip && tokens.length === 0 && <div className="t-sec">This clip has no tags.</div>}
            {clip && items.length > 0 && (
              <PickerList
                label="Filter by tag"
                items={items}
                onChoose={(key) => {
                  onChoose(key === '' ? null : key)
                  onClose()
                }}
              />
            )}
          </div>
          <div className="dialog-foot">
            <span className="t-meta grow">↑↓ choose · Enter filter · Esc close</span>
            <Dialog.Close asChild>
              <button className="btn ghost">Cancel</button>
            </Dialog.Close>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
