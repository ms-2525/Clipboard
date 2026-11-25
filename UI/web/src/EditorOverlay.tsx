import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import { EditorContent, useEditor } from '@tiptap/react'
import { Extension } from '@tiptap/core'
import StarterKit from '@tiptap/starter-kit'
import { BackgroundColor, Color, FontFamily, FontSize, TextStyle } from '@tiptap/extension-text-style'
import Highlight from '@tiptap/extension-highlight'
import { TableKit } from '@tiptap/extension-table'
import TaskList from '@tiptap/extension-task-list'
import TaskItem from '@tiptap/extension-task-item'
import { EditorToolbar, type EditorPop } from './EditorToolbar'
import { cfHtmlFragment } from './rich'
import type { ClipBody, ClipItem } from './host'
import { formatTime, type TimeFormat } from './format'
import { IconCheck, IconClose, IconWarning } from './icons'

interface Props {
  clip: ClipItem
  timeFormat: TimeFormat
  getItem: (id: number) => Promise<ClipBody | null>
  /** Full image for image clips; the OCR text below is what F2 is for. */
  getImage?: (id: number) => Promise<string | null>
  onSave: (html: string, text: string) => Promise<{ saved: boolean; error?: string }>
  /** The main window's Export: the host shows the dialog and toasts; never throws. */
  onSaveAs: (format: string) => Promise<void>
  /** The host opens the stored clip in its default app and toasts; never throws. */
  onOpenInApp: () => Promise<void>
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

/**
 * Pasted HTML carries the font on the block (<p style="font-family…">), not only on spans.
 * The list keeps block styles (rich.tsx preserves them on every element) but TipTap marks
 * only read spans — without this the same clip renders in two different fonts.
 */
const ClipBlockFont = Extension.create({
  name: 'clipBlockFont',
  addGlobalAttributes() {
    return [
      {
        types: ['paragraph', 'heading', 'blockquote', 'bulletList', 'orderedList', 'listItem', 'taskItem', 'codeBlock'],
        attributes: {
          fontFamily: {
            default: null,
            parseHTML: (element) => element.style.fontFamily || null,
            renderHTML: (attributes) => (attributes.fontFamily ? { style: `font-family: ${attributes.fontFamily}` } : {}),
          },
          fontSize: {
            default: null,
            parseHTML: (element) => element.style.fontSize || null,
            renderHTML: (attributes) => (attributes.fontSize ? { style: `font-size: ${attributes.fontSize}` } : {}),
          },
        },
      },
    ]
  },
})

export function EditorOverlay({ clip, timeFormat, getItem, getImage, onSave, onSaveAs, onOpenInApp, onClose, onSaved }: Props) {
  const [dirty, setDirty] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)
  const [size, setSize] = useState(14)
  const [pop, setPop] = useState<EditorPop>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [imgSrc, setImgSrc] = useState<string | null>(null)
  /** Bumped on every successful save: the green bar shows until the next edit or its timeout. */
  const [savedTick, setSavedTick] = useState(0)
  const showSaved = savedTick > 0 && !dirty && !confirmDiscard

  useEffect(() => {
    if (!showSaved) return
    const timer = window.setTimeout(() => setSavedTick(0), 2600)
    return () => window.clearTimeout(timer)
  }, [showSaved, savedTick])
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
      BackgroundColor,
      FontFamily,
      FontSize,
      ClipBlockFont,
      Highlight.configure({ multicolor: true }),
      TableKit.configure({ table: { resizable: false } }),
      TaskList,
      TaskItem.configure({ nested: true }),
    ],
    content: '',
    autofocus: false,
    editorProps: {
      // Down-arrow through a long clip keeps the caret this far inside the scrollport,
      // so the last line never hides behind the foot + bottom breathing room.
      scrollThreshold: 80,
      scrollMargin: 80,
    },
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

  // Image clips: the picture on top, the OCR text below it in the editor.
  useEffect(() => {
    if (clip.kind !== 'image' || !getImage) {
      setImgSrc(null)
      return
    }
    let cancelled = false
    setImgSrc(null)
    void getImage(clip.id)
      .then((url) => {
        if (!cancelled) setImgSrc(url)
      })
      .catch(() => {
        // The OCR text below is the point of F2; a missing picture must not hide it.
      })
    return () => {
      cancelled = true
    }
  }, [clip.id, clip.kind, getImage])

  const requestClose = useCallback(() => {
    if (saving) return
    if (!dirty) {
      onClose()
      return
    }
    setConfirmDiscard(true)
  }, [dirty, onClose, saving])

  // F2 toggles the editor: from the list it opens, and here it closes. Only a dirty body asks
  // about discarding; a clean one just closes.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'F2') return
      event.preventDefault()
      requestClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [requestClose])

  // Esc cascade: popover → discard prompt → close. Radix owns the keystroke and the focus trap.
  const onEscape = useCallback(
    (event: KeyboardEvent) => {
      // The window key matrix re-subscribes the moment this close commits, while the same
      // keystroke is still bubbling to the window: without this it would hide the whole app.
      event.preventDefault()
      event.stopPropagation()
      if (pop) setPop(null)
      else if (confirmDiscard) onClose()
      else if (dirty) setConfirmDiscard(true)
      else onClose()
    },
    [confirmDiscard, dirty, onClose, pop],
  )

  /** Writes the editor body to the store; true when the stored clip now matches the screen. Never closes. */
  const persist = useCallback(async (): Promise<boolean> => {
    if (!editor || saving || loading) return false
    if (!dirty) return true
    const html = editor.getHTML()
    const text = editor.getText({ blockSeparator: '\n' })
    if (!text.trim()) {
      setError('The editor has no text, so nothing was saved.')
      return false
    }
    if (html.length > MAX_HTML) {
      setError('The clip is over 2 MB of HTML, so it was not saved.')
      return false
    }
    if (/<script/i.test(html)) {
      setError('The clip contains a script, so it was not saved.')
      return false
    }
    setSaving(true)
    setError('')
    try {
      const result = await onSave(html, text)
      if (!result.saved) {
        setError(result.error || 'The clip could not be saved.')
        setSaving(false)
        return false
      }
      baseline.current = editor.getHTML()
      setDirty(false)
      onSaved()
      setSaving(false)
      setSavedTick((tick) => tick + 1)
      return true
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : String(failure))
      setSaving(false)
      return false
    }
  }, [dirty, editor, loading, onSave, onSaved, saving])

  const save = useCallback(async () => {
    // Ctrl+S / Save clip persist and stay open; Esc or Cancel closes. The green bar confirms the write.
    await persist()
  }, [persist])

  // Open in app / Export act on the stored clip, so a dirty editor is written first; a failed
  // write aborts before the host is asked. Both callbacks toast and never throw.
  const openHere = useCallback(async () => {
    if (await persist()) await onOpenInApp()
  }, [persist, onOpenInApp])

  const saveHereAs = useCallback(async () => {
    if (await persist()) await onSaveAs('auto')
  }, [persist, onSaveAs])

  // Ctrl+S / Cmd+S saves: the browser would otherwise open its Save-page dialog.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
        event.preventDefault()
        void save()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [save])

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
            {clip.kind !== 'files' && (
              <>
                <button
                  className="btn ghost"
                  onClick={() => void openHere()}
                  disabled={saving || loading}
                  title={clip.kind === 'image' ? 'Open the image in its default app' : 'Open this clip in its default app'}
                >
                  Open in app
                </button>
                <button
                  className="btn ghost"
                  onClick={() => void saveHereAs()}
                  disabled={saving || loading}
                  title="Export this clip to a file"
                >
                  Export
                </button>
              </>
            )}
            <button className="btn" onClick={() => void save()} disabled={saving || loading} title="Save clip (Ctrl+S)">
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
          ) : dirty ? (
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
          ) : (
            showSaved && (
              <div
                className="discard-banner"
                role="status"
                style={{
                  background: 'color-mix(in srgb, var(--sage) 14%, var(--surface))',
                  borderBottom: '1px solid color-mix(in srgb, var(--sage) 45%, transparent)',
                  color: 'var(--sage)',
                }}
              >
                <IconCheck />
                <span style={{ fontWeight: 600 }}>Saved</span>
                <span className="banner-sub">Your edits are in this clip — keep editing, or press Esc to close.</span>
              </div>
            )
          )}

          <EditorToolbar editor={editor} pop={pop} setPop={setPop} size={size} onSize={setSize} onSingleLine={singleLine} highlight={highlight} />

          <div className="editor-scroll">
            <div className="editor-page">
              {loading ? (
                <div className="t-sec">Loading the full clip…</div>
              ) : (
                <>
                  {clip.kind === 'image' && imgSrc && (
                    <div style={{ marginBottom: 12 }}>
                      <img
                        src={imgSrc}
                        alt=""
                        draggable={false}
                        style={{ width: '100%', height: 'auto', maxHeight: 360, objectFit: 'contain', borderRadius: 8, background: 'var(--field)' }}
                      />
                      <div className="t-sec" style={{ marginTop: 6 }}>
                        Text read from the image — edit it below. It stays searchable.
                      </div>
                    </div>
                  )}
                  <div style={{ fontSize: size, lineHeight: 1.6 }}>
                    <EditorContent editor={editor} />
                  </div>
                </>
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
                  <span className="keycap">Ctrl+S</span> Save
                  <span className="keycap" style={{ marginLeft: 8 }}>Esc</span> Close
                </span>
              </div>
            </div>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
