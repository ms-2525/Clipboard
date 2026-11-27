import { Extension } from '@tiptap/core'
import { Plugin, Selection, TextSelection } from '@tiptap/pm/state'
import { Decoration, DecorationSet } from '@tiptap/pm/view'
import type { EditorView } from '@tiptap/pm/view'
import type { Node as PMNode } from '@tiptap/pm/model'

/**
 * Break-mark decorations for the rich editor: ¶ for Enter, ↵ for Shift+Enter.
 *
 * Rules (all verified in e2e/breakmarks.spec.ts):
 * - Every paragraph earns a ¶, painted with ::after on the <p> itself, so the mark takes
 *   no caret position, survives no copy, and can never swallow a Backspace.
 * - The document's last paragraph NEVER earns a ¶: doc-end is not a break anyone pressed.
 *   This also covers any leftover trailing empty paragraph after list-only content
 *   (older saves; TrailingNode is now exempt for lists, Backspace deletes it) — it stays an unmarked
 *   blank line instead of a stuck, icon-shouting one.
 * - Every Shift+Enter (hard_break) earns a ↵ widget. A <br> cannot carry ::after, so a
 *   zero-width widget is the only paint that cannot park the caret after it.
 * - Empty paragraphs keep their ¶ on their own line (their filler <br> is hidden by CSS).
 * - Marks are decorations only: the document (getHTML/getText/clipboard) never contains them.
 */
export interface BreakStats {
  paras: number
  softs: number
  empties: number
}

/** Paragraphs, Shift+Enter breaks and empty paragraphs in a document. */
export function countBreakStats(doc: PMNode): BreakStats {
  let paras = 0
  let softs = 0
  let empties = 0
  doc.descendants((node) => {
    if (node.type.name === 'paragraph') {
      paras++
      if (node.textContent === '') empties++
    } else if (node.type.name === 'hardBreak') {
      softs++
    }
    return true
  })
  return { paras, softs, empties }
}

/** True when the paragraph is the document's last child: doc-end is not a pressed break. */
export function isTrailingParagraph(doc: PMNode, node: PMNode, pos: number): boolean {
  return node.type.name === 'paragraph' && pos + node.nodeSize === doc.content.size
}

/** Builds the ¶/↵ DecorationSet for a document. Pure: no DOM, no editor, fully testable. */
export function buildBreakDecorations(doc: PMNode): DecorationSet {
  const found: Decoration[] = []
  doc.descendants((node, pos) => {
    if (node.type.name === 'paragraph') {
      if (isTrailingParagraph(doc, node, pos)) return true
      found.push(
        Decoration.node(pos, pos + node.nodeSize, {
          class: node.textContent === '' ? 'brk-para brk-empty' : 'brk-para',
        }),
      )
    } else if (node.type.name === 'hardBreak') {
      found.push(Decoration.widget(pos, () => createSoftMark(), { side: -1, key: `sb${pos}` }))
    }
    return true
  })
  return DecorationSet.create(doc, found)
}

/** Live extension: always-on decorations; visibility is the .show-breaks CSS class. */
export const BreakMarks = Extension.create({
  name: 'breakMarks',
  addProseMirrorPlugins() {
    return [
      new Plugin({
        props: {
          decorations: (state) => buildBreakDecorations(state.doc),
        },
      }),
    ]
  },
})

/** The ↵ glyph. Zero-width + inert (see CSS): the caret cannot park after it. */
export function createSoftMark(): HTMLElement {
  const span = document.createElement('span')
  span.className = 'pb-mark pb-soft'
  span.textContent = '↵'
  span.setAttribute('aria-hidden', 'true')
  span.setAttribute('contenteditable', 'false')
  return span
}

/**
 * Backspace in an empty trailing paragraph that directly follows a non-textblock
 * (list, table, quote): ProseMirror has no join target and does nothing, stranding the
 * blank line — including the parser-added one below list-only content. Delete the paragraph
 * and park the caret at the end of the list instead. Everywhere else (nested positions,
 * non-empty paragraphs, textblock neighbours) returns false so default behavior is untouched.
 */
export function handleBreakBackspace(view: EditorView, event: KeyboardEvent): boolean {
  if (event.key !== 'Backspace' || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return false
  const { state } = view
  const { selection } = state
  if (!(selection instanceof TextSelection) || !selection.$cursor) return false
  const { $cursor } = selection
  // Top level only: never interfere with list-item, table-cell or quote editing.
  if ($cursor.depth !== 1 || $cursor.parentOffset !== 0) return false
  const para = $cursor.parent
  if (!para.isTextblock || para.textContent !== '') return false
  const index = $cursor.index(0)
  // Only the document's last paragraph, and only after a non-textblock.
  if (index === 0 || index !== state.doc.childCount - 1) return false
  if (state.doc.child(index - 1).isTextblock) return false
  const pos = $cursor.before(1)
  const tr = state.tr.delete(pos, pos + para.nodeSize)
  tr.setSelection(Selection.atEnd(tr.doc))
  view.dispatch(tr)
  return true
}

/** Test helper: split a DecorationSet back into node decorations and widget decorations. */
export function splitDecorations(set: DecorationSet, doc: PMNode): { nodes: Decoration[]; widgets: Decoration[] } {
  const all = set.find(0, doc.content.size)
  const nodes: Decoration[] = []
  const widgets: Decoration[] = []
  for (const decoration of all) {
    // Widget decorations carry a toDOM renderer; node decorations carry attrs. No deep
    // prosemirror-view imports needed for the split (Decoration.type is internal API).
    const inner = decoration as unknown as { type: { toDOM?: unknown } }
    if (typeof inner.type.toDOM === 'function') widgets.push(decoration)
    else nodes.push(decoration)
  }
  return { nodes, widgets }
}
