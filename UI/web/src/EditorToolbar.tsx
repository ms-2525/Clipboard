import { useEffect, useRef, useState } from 'react'
import type { Editor } from '@tiptap/core'
import type { Node as ProseMirrorNode } from '@tiptap/pm/model'
import {
  IconAddRow,
  IconBold,
  IconBulletList,
  IconChevron,
  IconCodeBlock,
  IconHighlight,
  IconInlineCode,
  IconItalic,
  IconLink,
  IconMinus,
  IconOrderedList,
  IconPalette,
  IconPlus,
  IconQuote,
  IconRedo,
  IconStrike,
  IconTable,
  IconTaskList,
  IconUnderline,
  IconUndo,
} from './icons'

export type EditorPop = 'colour' | 'font' | 'link' | 'case' | null

interface Props {
  editor: Editor
  pop: EditorPop
  setPop: (next: EditorPop | ((current: EditorPop) => EditorPop)) => void
  size: number
  onSize: (next: number) => void
  onSingleLine: () => void
  highlight: string
}

/**
 * The Change-case menu. Word characters match the legacy editor's rule
 * (Core\TextCase): letters, digits, apostrophes.
 */
const MINOR_WORDS = new Set([
  'a', 'an', 'and', 'as', 'at', 'but', 'by', 'for', 'if', 'in', 'is', 'of', 'on', 'or',
  'so', 'the', 'to', 'via', 'per', 'with', 'yet', 'nor',
])

type CaseVariant = 'upper' | 'lower' | 'title' | 'sentence'

const isLetter = (value: string) => /\p{L}/u.test(value)

const isWordChar = (value: string) => /[\p{L}\p{Nd}'\u2019]/u.test(value)

// Title Case: every word except the short connectors; the block's first word always counts.
function titleRun(text: string, flag: boolean): { text: string; flag: boolean } {
  const chars = Array.from(text)
  let out = ''
  let firstWord = flag
  let index = 0
  while (index < chars.length) {
    if (!isLetter(chars[index])) {
      out += chars[index]
      index += 1
      continue
    }
    let end = index
    while (end < chars.length && isWordChar(chars[end])) end += 1
    const word = chars.slice(index, end).join('')
    const cap = firstWord || !MINOR_WORDS.has(word.toLowerCase())
    out += cap ? word[0].toUpperCase() + word.slice(1).toLowerCase() : word.toLowerCase()
    firstWord = false
    index = end
  }
  return { text: out, flag: firstWord }
}

// Sentence case: the first letter after ".", "!" or "?" (block starts count too).
// A digit also ends the wait, so "2.0 release" stays lower, but only a letter is uppercased.
function sentenceRun(text: string, flag: boolean): { text: string; flag: boolean } {
  let out = ''
  let sentenceStart = flag
  for (const char of text) {
    if (char === '.' || char === '!' || char === '?') {
      sentenceStart = true
      out += char
    } else if (isLetter(char)) {
      out += sentenceStart ? char.toUpperCase() : char.toLowerCase()
      sentenceStart = false
    } else {
      if (/\p{Nd}/u.test(char)) sentenceStart = false
      out += char
    }
  }
  return { text: out, flag: sentenceStart }
}

/**
 * The editor's formatting surface. Formatting state is read from TipTap on every render, so an
 * active mark lights up right after the command that changed it.
 */
export function EditorToolbar({ editor, pop, setPop, size, onSize, onSingleLine, highlight }: Props) {
  const [linkDraft, setLinkDraft] = useState('')
  const popRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!pop) return
    const onDown = (event: MouseEvent) => {
      if (!popRef.current?.contains(event.target as Node)) setPop(null)
    }
    window.addEventListener('mousedown', onDown)
    return () => window.removeEventListener('mousedown', onDown)
  }, [pop, setPop])

  const openLink = () => {
    setLinkDraft(editor.getAttributes('link').href ?? '')
    setPop((current) => (current === 'link' ? null : 'link'))
  }

  const applyLink = () => {
    const href = linkDraft.trim()
    if (href) editor.chain().focus().extendMarkRange('link').setLink({ href }).run()
    else editor.chain().focus().extendMarkRange('link').unsetLink().run()
    setPop(null)
  }

  const applyCase = (variant: CaseVariant) => {
    const { state } = editor
    // No selection means the whole clip, like Clear formatting.
    const from = state.selection.empty ? 0 : state.selection.from
    const to = state.selection.empty ? state.doc.content.size : state.selection.to
    const changes: { from: number; to: number; text: string; node: ProseMirrorNode }[] = []
    if (variant === 'upper' || variant === 'lower') {
      state.doc.nodesBetween(from, to, (node, pos) => {
        if (!node.isText || !node.text) return true
        const start = Math.max(pos, from)
        const end = Math.min(pos + node.nodeSize, to)
        if (start >= end) return true
        const slice = node.text.slice(start - pos, end - pos)
        const next = variant === 'upper' ? slice.toUpperCase() : slice.toLowerCase()
        if (next !== slice) changes.push({ from: start, to: end, text: next, node })
        return true
      })
    } else {
      // Title and sentence threading restarts at every block, like the legacy Aa cycle.
      let flag = true
      state.doc.nodesBetween(from, to, (node, pos) => {
        if (node.isTextblock) {
          flag = true
          return true
        }
        if (!node.isText || !node.text) return true
        const start = Math.max(pos, from)
        const end = Math.min(pos + node.nodeSize, to)
        if (start >= end) return true
        const slice = node.text.slice(start - pos, end - pos)
        const result = variant === 'title' ? titleRun(slice, flag) : sentenceRun(slice, flag)
        flag = result.flag
        if (result.text !== slice) changes.push({ from: start, to: end, text: result.text, node })
        return true
      })
    }
    if (changes.length) {
      // Back to front so an earlier replacement never shifts a later offset.
      const tr = state.tr
      for (let index = changes.length - 1; index >= 0; index -= 1) {
        const change = changes[index]
        tr.replaceWith(change.from, change.to, state.schema.text(change.text, change.node.marks))
      }
      editor.view.dispatch(tr)
    }
    editor.commands.focus()
    setPop(null)
  }

  return (
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
      <button className="tool" data-on={editor.isActive('code')} onClick={() => editor.chain().focus().toggleCode().run()} title="Inline code" aria-label="Inline code">
        <IconInlineCode />
      </button>

      <span className="tool-sep" />

      {[1, 2, 3].map((level) => (
        <button
          key={level}
          className="tool text"
          data-on={editor.isActive('heading', { level })}
          onClick={() => editor.chain().focus().toggleHeading({ level: level as 1 | 2 | 3 }).run()}
          title={`Heading ${level}`}
          aria-label={`Heading ${level}`}
        >
          H{level}
        </button>
      ))}
      <button className="tool" data-on={editor.isActive('bulletList')} onClick={() => editor.chain().focus().toggleBulletList().run()} title="Bullet list" aria-label="Bullet list">
        <IconBulletList />
      </button>
      <button className="tool" data-on={editor.isActive('orderedList')} onClick={() => editor.chain().focus().toggleOrderedList().run()} title="Numbered list" aria-label="Numbered list">
        <IconOrderedList />
      </button>
      <button className="tool" data-on={editor.isActive('taskList')} onClick={() => editor.chain().focus().toggleTaskList().run()} title="Task list" aria-label="Task list">
        <IconTaskList />
      </button>
      <button className="tool" data-on={editor.isActive('blockquote')} onClick={() => editor.chain().focus().toggleBlockquote().run()} title="Quote" aria-label="Quote">
        <IconQuote />
      </button>
      <button className="tool" data-on={editor.isActive('codeBlock')} onClick={() => editor.chain().focus().toggleCodeBlock().run()} title="Code block" aria-label="Code block">
        <IconCodeBlock />
      </button>

      <span className="tool-sep" />

      <div className="tool-wrap">
        <button className="tool" data-on={editor.isActive('link') || pop === 'link'} onClick={openLink} title="Link" aria-label="Link">
          <IconLink />
        </button>
        {pop === 'link' && (
          <div className="popover link-pop" ref={popRef}>
            <div className="t-micro" style={{ padding: '2px 4px 6px' }}>
              Link address
            </div>
            <input
              className="pop-input"
              value={linkDraft}
              onChange={(event) => setLinkDraft(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') applyLink()
              }}
              placeholder="https://…"
              aria-label="Link address"
              autoFocus
            />
            <div className="pop-actions">
              <button className="btn ghost sm" onClick={() => setPop(null)}>
                Cancel
              </button>
              <button className="btn sm" onClick={applyLink}>
                Apply
              </button>
            </div>
          </div>
        )}
      </div>
      <button className="tool" onClick={() => editor.chain().focus().insertTable({ rows: 2, cols: 2, withHeaderRow: true }).run()} title="Insert table" aria-label="Insert table">
        <IconTable />
      </button>
      <button className="tool" onClick={() => editor.chain().focus().addRowAfter().run()} title="Add a table row" aria-label="Add table row">
        <IconAddRow />
      </button>

      <span className="tool-sep" />

      <div className="tool-wrap">
        <button className="tool" data-on={pop === 'colour'} onClick={() => setPop((current) => (current === 'colour' ? null : 'colour'))} title="Text colour" aria-label="Text colour">
          <IconPalette />
          <IconChevron />
        </button>
        {pop === 'colour' && (
          <div className="popover" style={{ top: 32, left: 0 }} ref={popRef}>
            <div className="t-micro" style={{ padding: '2px 4px 6px' }}>
              Text colour
            </div>
            <div className="colours">
              {['var(--fg)', '#C2410C', '#2F8A5B', '#3B82C4', '#C23B3B', '#8A92A1'].map((colour) => (
                // Fixed document inks (WordPad-style), not theme tokens: the dot previews the ink
                // that lands in the clip body, which keeps its color in both themes by design.
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
        <button className="tool text" onClick={() => setPop((current) => (current === 'font' ? null : 'font'))} title="Font family" aria-label="Font family">
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

      <button className="tool" onClick={() => onSize(Math.max(11, size - 1))} title="Smaller text" aria-label="Decrease text size" disabled={size <= 11}>
        <IconMinus />
      </button>
      <span className="size-readout">{size}</span>
      <button className="tool" onClick={() => onSize(Math.min(24, size + 1))} title="Larger text" aria-label="Increase text size" disabled={size >= 24}>
        <IconPlus />
      </button>

      <span className="tool-sep" />

      <button className="tool" onClick={() => editor.chain().focus().undo().run()} disabled={!editor.can().undo()} title="Undo (Ctrl+Z)" aria-label="Undo">
        <IconUndo />
      </button>
      <button className="tool" onClick={() => editor.chain().focus().redo().run()} disabled={!editor.can().redo()} title="Redo (Ctrl+Y)" aria-label="Redo">
        <IconRedo />
      </button>
      <button
        className="tool text"
        onClick={() => {
          // Excel-style: marks only (colour, size, bold...), never the paragraphs or line breaks.
          // No selection means the whole clip, so one click cleans a pasted mess.
          const chain = editor.chain().focus()
          if (editor.state.selection.empty) chain.selectAll()
          chain.unsetAllMarks().run()
        }}
        title="Clear formatting"
        aria-label="Clear formatting"
      >
        Clear
      </button>
      <button className="tool text" onClick={onSingleLine} title="Collapse to one paragraph">
        Single line
      </button>
      <div className="tool-wrap">
        <button
          className="tool text"
          data-on={pop === 'case'}
          onClick={() => setPop((current) => (current === 'case' ? null : 'case'))}
          title="Change case: UPPERCASE / lowercase / Title Case / Sentence case"
          aria-label="Change case"
        >
          Case
          <IconChevron />
        </button>
        {pop === 'case' && (
          <div className="popover" style={{ top: 32, right: 0 }} ref={popRef}>
            <div className="t-micro" style={{ padding: '2px 4px 6px' }}>
              Change case
            </div>
            {(
              [
                { label: 'UPPERCASE', variant: 'upper' },
                { label: 'lowercase', variant: 'lower' },
                { label: 'Title Case', variant: 'title' },
                { label: 'Sentence case', variant: 'sentence' },
              ] as { label: string; variant: CaseVariant }[]
            ).map((item) => (
              <button key={item.variant} className="pop-item" onClick={() => applyCase(item.variant)}>
                {item.label}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
