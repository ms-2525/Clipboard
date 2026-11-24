import { useEffect, useRef, useState } from 'react'
import { EditorContent, useEditor } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import { Color, FontFamily, TextStyle } from '@tiptap/extension-text-style'
import Highlight from '@tiptap/extension-highlight'
import { RICH_EDITOR_CONTENT } from './data'
import type { ClipItem } from './host'
import { formatTime, type TimeFormat } from './format'
import {
  IconBold,
  IconChevron,
  IconClose,
  IconHighlight,
  IconItalic,
  IconMinus,
  IconPalette,
  IconPlus,
  IconStrike,
  IconUnderline,
  IconWarning,
} from './icons'

interface Props {
  clip: ClipItem
  timeFormat: TimeFormat
  initialDirty?: boolean
  onClose: () => void
}

const escapeHtml = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

const kindLabel = (kind: ClipItem['kind']) =>
  kind === 'rich' ? 'Rich text' : kind === 'image' ? 'Image' : kind === 'files' ? 'Files' : 'Text'

export function EditorOverlay({ clip, timeFormat, initialDirty = false, onClose }: Props) {
  const [dirty, setDirty] = useState(initialDirty)
  const [size, setSize] = useState(15)
  const [pop, setPop] = useState<'colour' | 'font' | null>(null)
  const popRef = useRef<HTMLDivElement>(null)

  const editor = useEditor({
    extensions: [
      StarterKit.configure({ link: { openOnClick: false } }),
      TextStyle,
      Color,
      FontFamily,
      Highlight.configure({ multicolor: true }),
    ],
    content: RICH_EDITOR_CONTENT,
    autofocus: 'start',
    onUpdate: () => setDirty(true),
  })

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        if (pop) setPop(null)
        else onClose()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose, pop])

  useEffect(() => {
    if (!pop) return
    const onDown = (event: MouseEvent) => {
      if (!popRef.current?.contains(event.target as Node)) setPop(null)
    }
    window.addEventListener('mousedown', onDown)
    return () => window.removeEventListener('mousedown', onDown)
  }, [pop])

  if (!editor) return null

  const ink = 'var(--fg)'
  const highlight = 'var(--highlight)'

  const singleLine = () => {
    const text = editor.getText().replace(/\s*\n+\s*/g, ' ').trim()
    editor.commands.setContent(`<p>${escapeHtml(text)}</p>`)
    setDirty(true)
  }

  return (
    <div className="editor-overlay" role="dialog" aria-modal="true" aria-label="Clip editor preview">
      <div className="editor-head">
        <button className="icon-btn ghost" onClick={onClose} aria-label="Close editor" title="Close (Esc)">
          <IconClose />
        </button>
        <div className="grow">
          <div className="editor-title">
            Editing clip
            {dirty && <span className="dirty-dot" title="Unsaved changes" />}
            <span className="chip editor-chip">Preview</span>
            <span className="chip editor-chip">{kindLabel(clip.kind)}</span>
            {clip.formats.length > 1 && <span className="chip editor-chip">+{clip.formats.length - 1} formats</span>}
          </div>
          <div className="t-meta editor-sub">
            {clip.app || 'Unknown'} · {clip.title} · {formatTime(clip.time, timeFormat)}
          </div>
        </div>
        <button className="btn ghost" onClick={onClose}>
          Cancel
        </button>
        <button className="btn" disabled title="Editing is not wired yet — this is a preview">
          Save clip
        </button>
      </div>

      {dirty && (
        <div className="discard-banner">
          <IconWarning />
          <span style={{ fontWeight: 600 }}>Discard changes?</span>
          <span className="banner-sub">You have unsaved edits in this clip.</span>
          <span className="grow" />
          <button
            className="btn ghost sm"
            onClick={() => {
              editor.commands.setContent(RICH_EDITOR_CONTENT)
              setDirty(false)
            }}
          >
            Discard edits
          </button>
        </div>
      )}

      <div className="toolbar">
        <button className="tool" data-on={editor.isActive('bold')} onClick={() => editor.chain().focus().toggleBold().run()} title="Bold (Ctrl+B)" aria-label="Bold">
          <IconBold />
        </button>
        <button className="tool" data-on={editor.isActive('italic')} onClick={() => editor.chain().focus().toggleItalic().run()} title="Italic (Ctrl+I)" aria-label="Italic">
          <IconItalic />
        </button>
        <button className="tool" data-on={editor.isActive('underline')} onClick={() => editor.chain().focus().toggleUnderline().run()} title="Underline (Ctrl+U)" aria-label="Underline">
          <IconUnderline />
        </button>
        <button className="tool" data-on={editor.isActive('strike')} onClick={() => editor.chain().focus().toggleStrike().run()} title="Strikethrough" aria-label="Strikethrough">
          <IconStrike />
        </button>

        <span className="tool-sep" />

        <button className="tool" onClick={() => setSize((s) => Math.max(11, s - 1))} title="Smaller text" aria-label="Decrease text size" disabled={size <= 11}>
          <IconMinus />
        </button>
        <span className="size-readout">{size}</span>
        <button className="tool" onClick={() => setSize((s) => Math.min(24, s + 1))} title="Larger text" aria-label="Increase text size" disabled={size >= 24}>
          <IconPlus />
        </button>

        <span className="tool-sep" />

        <div className="tool-wrap">
          <button className="tool" data-on={pop === 'colour'} onClick={() => setPop((p) => (p === 'colour' ? null : 'colour'))} title="Text colour" aria-label="Text colour">
            <IconPalette />
            <IconChevron />
          </button>
          {pop === 'colour' && (
            <div className="popover" style={{ top: 32, left: 0 }} ref={popRef}>
              <div className="t-micro" style={{ padding: '2px 4px 6px' }}>
                Text colour
              </div>
              <div className="colours">
                {[ink, '#C2410C', '#2F8A5B', '#3B82C4', '#C23B3B', '#8A92A1'].map((colour) => (
                  <button
                    key={colour}
                    className="colour"
                    style={{ background: colour }}
                    aria-label={`Colour ${colour}`}
                    onClick={() => {
                      editor.chain().focus().setColor(colour).run()
                      setPop(null)
                    }}
                  />
                ))}
              </div>
              <button
                className="pop-item"
                onClick={() => {
                  editor.chain().focus().unsetColor().run()
                  setPop(null)
                }}
              >
                Clear colour
              </button>
            </div>
          )}
        </div>

        <button className="tool" data-on={editor.isActive('highlight')} onClick={() => editor.chain().focus().toggleHighlight({ color: highlight }).run()} title="Highlight" aria-label="Highlight">
          <IconHighlight />
        </button>

        <div className="tool-wrap">
          <button className="tool text" onClick={() => setPop((p) => (p === 'font' ? null : 'font'))} title="Font family" aria-label="Font family">
            Aa
            <IconChevron />
          </button>
          {pop === 'font' && (
            <div className="popover" style={{ top: 32, left: 0 }} ref={popRef}>
              <div className="t-micro" style={{ padding: '2px 4px 6px' }}>
                Font
              </div>
              {[
                { label: 'Sans', value: null },
                { label: 'Serif', value: 'Georgia, serif' },
                { label: 'Mono', value: "'Cascadia Code', Consolas, monospace" },
              ].map((font) => (
                <button
                  key={font.label}
                  className="pop-item"
                  onClick={() => {
                    if (font.value) editor.chain().focus().setFontFamily(font.value).run()
                    else editor.chain().focus().unsetFontFamily().run()
                    setPop(null)
                  }}
                >
                  {font.label}
                </button>
              ))}
            </div>
          )}
        </div>

        <span className="tool-sep" />

        <button className="tool text" onClick={() => editor.chain().focus().unsetAllMarks().clearNodes().run()} title="Clear formatting">
          Clear
        </button>
        <button className="tool text" onClick={singleLine} title="Collapse to one paragraph">
          Single line
        </button>
      </div>

      <div className="editor-scroll">
        <div className="editor-page">
          <div className="editor-meta">
            <span className="tile" style={{ width: 22, height: 22, borderRadius: 6, display: 'grid', placeItems: 'center', background: 'var(--badge)', color: 'var(--fg55)', fontSize: 11, fontWeight: 600 }}>
              {(clip.app || '?').slice(0, 1)}
            </span>
            <span className="t-sec">{clip.formats.length ? clip.formats.join(' · ') : 'No extra formats'}</span>
            <span className="spacer" />
            <span className="chip accent">Preview</span>
          </div>
          <div style={{ fontSize: size, lineHeight: 1.6 }}>
            <EditorContent editor={editor} />
          </div>
          <div className="editor-foot">
            <span className="t-meta">{editor.getText().length.toLocaleString()} characters</span>
            <span className="chip">{clip.formats.length} formats</span>
            <span className="spacer" />
            <span className="t-meta">Editing is not saved yet</span>
            <span className="hint editor-foot-hint">
              <span className="keycap">Esc</span> Close
            </span>
          </div>
        </div>
      </div>
    </div>
  )
}
