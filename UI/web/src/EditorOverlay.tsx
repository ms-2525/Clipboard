import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import { EditorContent, useEditor } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import { Color, FontFamily, TextStyle } from '@tiptap/extension-text-style'
import Highlight from '@tiptap/extension-highlight'
import { TableKit } from '@tiptap/extension-table'
import TaskList from '@tiptap/extension-task-list'
import TaskItem from '@tiptap/extension-task-item'
import { EditorToolbar, type EditorPop } from './EditorToolbar'
import { cfHtmlFragment } from './rich'
import type { ClipBody, ClipItem } from './host'
import { formatTime, type TimeFormat } from './format'
import { IconClose, IconWarning } from './icons'

interface Props {
  clip: ClipItem
  timeFormat: TimeFormat
  getItem: (id: number) => Promise<ClipBody | null>
  onSave: (html: string, text: string) => Promise<{ saved: boolean; error?: string }>
  onClose: () => void
  onSaved: () => void
}

const escapeHtml = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

/** Plain text becomes paragraphs, exactly what a text clip's body is. */
const textToHtml = (text: string) =>
  (text || '')
    .split(/\r?\n/)
    .map((line) => (line.trim() ? `<p>${escapeHtml(line)}</p>` : '<p></p>'))
    .join('')

const kindLabel = (kind: ClipItem['kind']) =>
  kind === 'rich' ? 'Rich text' : kind === 'image' ? 'Image' : kind === 'files' ? 'Files' : 'Text'

/** The host refuses these too; the page refuses first so a bad body never leaves the browser. */
const MAX_HTML = 2_000_000

export function EditorOverlay({ clip, timeFormat, getItem, onSave, onClose, onSaved }: Props) {
  const [dirty, setDirty] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)
  const [size, setSize] = useState(15)
  const [pop, setPop] = useState<EditorPop>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [formats, setFormats] = useState<string[]>(clip.formats)
  /** Bumped on every editor update: the count must move on each keystroke, not only when `dirty` flips. */
  const [revision, setRevision] = useState(0)
  const baseline = useRef('')
  // Captured during the first render, before Radix moves focus into the dialog.
  const returnFocus = useRef<HTMLElement | null>(document.activeElement instanceof HTMLElement ? document.activeElement : null)

  const editor = useEditor({
    extensions: [
      StarterKit.configure({ link: { openOnClick: false } }),
      TextStyle,
      Color,
      FontFamily,
      Highlight.configure({ multicolor: true }),
      TableKit.configure({ table: { resizable: false } }),
      TaskList,
      TaskItem.configure({ nested: true }),
    ],
    content: '',
    autofocus: false,
    onUpdate: ({ editor: instance }) => {
      setDirty(instance.getHTML() !== baseline.current)
      setRevision((value) => value + 1)
    },
  })

  useEffect(() => {
    if (!editor) return
    let cancelled = false
    setLoading(true)
    setError('')
    void (async () => {
      let body: ClipBody | null = null
      try {
        body = await getItem(clip.id)
      } catch {
        // A body the host cannot read still opens: the list preview is the best text there is.
      }
      if (cancelled) return
      // getItem carries the whole stored body (never the list preview), so a truncated clip edits in full.
      const html = body?.html ? cfHtmlFragment(body.html) : textToHtml(body?.text ?? clip.text ?? '')
      editor.commands.setContent(html || '<p></p>', { emitUpdate: false })
      baseline.current = editor.getHTML()
      setFormats(body?.formats?.length ? body.formats : clip.formats)
      setDirty(false)
      setLoading(false)
      requestAnimationFrame(() => editor.commands.focus('start'))
    })()
    return () => {
      cancelled = true
    }
  }, [clip.formats, clip.id, clip.text, editor, getItem])

  const requestClose = useCallback(() => {
    if (saving) return
    if (!dirty) {
      onClose()
      return
    }
    setConfirmDiscard(true)
  }, [dirty, onClose, saving])

  // Esc cascade: popover → discard prompt → close. Radix owns the keystroke and the focus trap.
  const onEscape = useCallback(
    (event: KeyboardEvent) => {
      event.preventDefault()
      if (pop) setPop(null)
      else if (confirmDiscard) onClose()
      else if (dirty) setConfirmDiscard(true)
      else onClose()
    },
    [confirmDiscard, dirty, onClose, pop],
  )

  const save = useCallback(async () => {
    if (!editor || saving || loading) return
    const html = editor.getHTML()
    const text = editor.getText({ blockSeparator: '\n' })
    if (!text.trim()) {
      setError('The editor has no text, so nothing was saved.')
      return
    }
    if (html.length > MAX_HTML) {
      setError('The clip is over 2 MB of HTML, so it was not saved.')
      return
    }
    if (/<script/i.test(html)) {
      setError('The clip contains a script, so it was not saved.')
      return
    }
    setSaving(true)
    setError('')
    try {
      const result = await onSave(html, text)
      if (!result.saved) {
        setError(result.error || 'The clip could not be saved.')
        setSaving(false)
        return
      }
      onSaved()
      onClose()
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : String(failure))
      setSaving(false)
    }
  }, [editor, loading, onClose, onSave, onSaved, saving])

  const singleLine = () => {
    if (!editor) return
    const text = editor.getText().replace(/\s*\n+\s*/g, ' ').trim()
    editor.commands.setContent(`<p>${escapeHtml(text)}</p>`)
    setDirty(editor.getHTML() !== baseline.current)
  }

  const characterCount = useMemo(() => editor?.getText().length ?? 0, [editor, revision, dirty, loading])
  if (!editor) return null
  const highlight = 'var(--highlight)'

  return (
    <Dialog.Root open>
      <Dialog.Portal>
        <Dialog.Content
          className="editor-overlay"
          aria-label="Clip editor"
          aria-modal="true"
          onEscapeKeyDown={onEscape}
          onCloseAutoFocus={(event) => {
            // Radix restores focus to a Dialog.Trigger; this editor has none, so give it the row.
            event.preventDefault()
            returnFocus.current?.focus()
          }}
        >
          <div className="editor-head">
            <button className="icon-btn ghost" onClick={requestClose} aria-label="Close editor" title="Close (Esc)">
              <IconClose />
            </button>
            <div className="grow">
              <div className="editor-title">
                Editing clip
                {dirty && <span className="dirty-dot" title="Unsaved changes" />}
                <span className="chip editor-chip">{kindLabel(clip.kind)}</span>
                {formats.length > 1 && (
                  <span className="chip editor-chip">
                    +{formats.length - 1} {formats.length === 2 ? 'format' : 'formats'}
                  </span>
                )}
              </div>
              <div className="t-meta editor-sub">
                {clip.app || 'Unknown'} · {clip.title} · {formatTime(clip.time, timeFormat)}
              </div>
            </div>
            <button className="btn ghost" onClick={requestClose}>
              Cancel
            </button>
            <button className="btn" onClick={() => void save()} disabled={saving || loading} title="Save to this clip">
              {saving ? 'Saving…' : 'Save clip'}
            </button>
          </div>

          {error && (
            <div className="discard-banner error-banner" role="alert">
              <IconWarning />
              <span>{error}</span>
            </div>
          )}

          {confirmDiscard ? (
            <div className="discard-banner">
              <IconWarning />
              <span style={{ fontWeight: 600 }}>Discard changes?</span>
              <span className="banner-sub">You have unsaved edits in this clip.</span>
              <span className="grow" />
              <button className="btn ghost sm" onClick={() => setConfirmDiscard(false)}>
                Keep editing
              </button>
              <button className="btn danger sm" onClick={onClose}>
                Discard and close
              </button>
            </div>
          ) : (
            dirty && (
              <div className="discard-banner">
                <IconWarning />
                <span style={{ fontWeight: 600 }}>Unsaved changes</span>
                <span className="banner-sub">Save the clip, or discard and keep the stored version.</span>
                <span className="grow" />
                <button
                  className="btn ghost sm"
                  onClick={() => {
                    editor.commands.setContent(baseline.current, { emitUpdate: false })
                    setDirty(false)
                  }}
                >
                  Revert edits
                </button>
              </div>
            )
          )}

          <EditorToolbar editor={editor} pop={pop} setPop={setPop} size={size} onSize={setSize} onSingleLine={singleLine} highlight={highlight} />

          <div className="editor-scroll">
            <div className="editor-page">
              <div className="editor-meta">
                <span
                  className="tile"
                  style={{ width: 22, height: 22, borderRadius: 6, display: 'grid', placeItems: 'center', background: 'var(--badge)', color: 'var(--fg55)', fontSize: 11, fontWeight: 600 }}
                >
                  {(clip.app || '?').slice(0, 1)}
                </span>
                <span className="t-sec">{formats.length ? formats.join(' · ') : 'No extra formats'}</span>
                <span className="spacer" />
                {clip.truncated && <span className="chip">Full body</span>}
                {dirty && <span className="chip accent">Unsaved</span>}
              </div>
              {loading ? (
                <div className="t-sec">Loading the full clip…</div>
              ) : (
                <div style={{ fontSize: size, lineHeight: 1.6 }}>
                  <EditorContent editor={editor} />
                </div>
              )}
              <div className="editor-foot">
                <span className="t-meta">
                  {characterCount.toLocaleString()} {characterCount === 1 ? 'character' : 'characters'}
                </span>
                <span className="chip">
                  {formats.length} {formats.length === 1 ? 'format' : 'formats'}
                </span>
                <span className="spacer" />
                <span className="t-meta">{dirty ? 'Unsaved changes' : 'Saved to this clip'}</span>
                <span className="hint editor-foot-hint">
                  <span className="keycap">Esc</span> Close
                </span>
              </div>
            </div>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
