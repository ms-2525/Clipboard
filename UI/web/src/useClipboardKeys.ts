import { useEffect } from 'react'
import type { ClipItem } from './host'

export interface KeyActions {
  /** False while an overlay owns the keyboard (editor, settings, delete confirm). */
  enabled: boolean
  rows: ClipItem[]
  selectedId: number | null
  search: string
  searchRef: { current: HTMLInputElement | null }
  onSelect: (id: number) => void
  /** Shift+Up/Down: extend the contiguous range instead of moving the cursor. */
  onRangeExtend: (direction: 1 | -1) => void
  onSearch: (value: string) => void
  onSearchFocus: () => void
  /** Move a measured page (the list owns the row heights). */
  onPage: (direction: 1 | -1) => void
  /** Ctrl+Up/Down: the nearest time section (calendar day) in the direction. */
  onSection: (direction: 1 | -1) => void
  /** Ctrl+Shift+Left/Right: move the selected clip one tab along the strip order. */
  onMoveTab: (direction: 1 | -1) => void
  /** Left/Right and Ctrl+Tab / Ctrl+Shift+Tab: change tab. */
  onTab: (direction: 1 | -1) => void
  onPaste: (clip: ClipItem, plain: boolean) => void
  onEdit: () => void
  onExport: () => void
  onDelete: () => void
  onMove: () => void
  /** Ctrl+T: the selected clip's tags, as a keyboard path to the chip filter. */
  onTagPicker: () => void
  onRestore: () => void
  onPin: () => void
  onHide: () => void
}

/**
 * One key matrix for the whole surface (DESIGN-language §3: the index is the paste key).
 * It listens on the window, so every key works whether focus is in the search box or the list;
 * only typing is left to the search box, and only when what is typed is text.
 */
export function useClipboardKeys(actions: KeyActions) {
  const { enabled, rows, selectedId, search, searchRef } = actions

  useEffect(() => {
    if (!enabled) return

    const onKey = (event: KeyboardEvent) => {
      // An IME owns the keystroke that is composing a character; never act on half of it.
      if (event.isComposing || event.keyCode === 229) return

      const target = event.target as HTMLElement
      if (!target || typeof target.closest !== 'function') return
      const inSearch = target === searchRef.current
      const isTextField = (el: HTMLElement | null) =>
        !!el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.tagName === 'SELECT' || el.isContentEditable)
      // A field owns its caret: the event target or whatever currently holds focus.
      const focused = document.activeElement instanceof HTMLElement ? document.activeElement : null
      const typing = isTextField(target) || isTextField(focused)
      const onControl = !!target.closest('button, a, [role="tab"]')
      const mod = event.ctrlKey || event.metaKey
      const key = event.key
      const lower = key.toLowerCase()
      const selected = rows.find((clip) => clip.id === selectedId) ?? null
      const index = rows.findIndex((clip) => clip.id === selectedId)

      const take = () => event.preventDefault()

      if (event.altKey) return

      if (mod) {
        // Ctrl+1..Ctrl+9 paste the visible row; a bare digit is search text.
        if (!event.shiftKey && /^[1-9]$/.test(key)) {
          const clip = rows[Number(key) - 1]
          if (clip) {
            take()
            actions.onPaste(clip, false)
          }
          return
        }
        if (!event.shiftKey && (key === 'ArrowUp' || key === 'ArrowDown')) {
          take()
          actions.onSection(key === 'ArrowDown' ? 1 : -1)
          return
        }
        if (event.shiftKey && (key === 'ArrowLeft' || key === 'ArrowRight')) {
          take()
          actions.onMoveTab(key === 'ArrowRight' ? 1 : -1)
          return
        }
        // Ctrl+Tab cycles tabs like Left/Right, from anywhere — including the search
        // box while it holds text, where plain arrows belong to the caret.
        // Plain Tab is untouched: it still moves focus and picks a tag suggestion.
        if (key === 'Tab') {
          take()
          actions.onTab(event.shiftKey ? -1 : 1)
          return
        }
        if (lower === 'f' || lower === 'k') {
          take()
          actions.onSearchFocus()
          return
        }
        if (event.shiftKey && lower === 'v') {
          if (selected) {
            take()
            actions.onPaste(selected, true)
          }
          return
        }
        if (lower === 'p') {
          if (selected) {
            take()
            actions.onPin()
          }
          return
        }
        if (lower === 'm') {
          if (selected) {
            take()
            actions.onMove()
          }
          return
        }
        // Ctrl+T must not fire while the search box owns the caret: T is a letter first.
        if (lower === 't' && !typing) {
          if (selected) {
            take()
            actions.onTagPicker()
          }
          return
        }
        if (lower === 'z') {
          take()
          actions.onRestore()
          return
        }
        if (lower === 'e') {
          take()
          actions.onExport()
          return
        }
        // Ctrl+A/C/X stay with the search field.
        return
      }

      // A text field owns its caret: only Escape, both arrows (into the list), Enter (paste the
      // highlighted result) and F2 (edit it) may act — except an EMPTY search box, which is
      // still the list: Left/Right/Home/End drive the tabs and rows while there is no text
      // to move through. The moment it holds text, the caret owns those keys again.
      const emptySearchBox =
        (target === searchRef.current || focused === searchRef.current) && search.trim() === ''
      const listKeyFromEmptySearch =
        emptySearchBox && (key === 'ArrowLeft' || key === 'ArrowRight' || key === 'Home' || key === 'End')
      if (typing && !listKeyFromEmptySearch && key !== 'Escape' && key !== 'ArrowDown' && key !== 'ArrowUp' && key !== 'Enter' && key !== 'F2') return

      // ArrowDown is measured by the list, so it works in the search box too.
      if (rows.length > 0 && !onControl) {
        const at = index < 0 ? 0 : index
        // Shift+Up/Down grows the range for a multi-delete; the caret keeps its search-box meaning.
        if (!typing && event.shiftKey && (key === 'ArrowDown' || key === 'ArrowUp')) {
          take()
          actions.onRangeExtend(key === 'ArrowDown' ? 1 : -1)
          return
        }
        if (key === 'ArrowDown') {
          take()
          actions.onSelect(rows[Math.min(at + 1, rows.length - 1)].id)
          return
        }
        if (key === 'ArrowUp') {
          take()
          actions.onSelect(rows[Math.max(at - 1, 0)].id)
          return
        }
        if (key === 'Home') {
          take()
          actions.onSelect(rows[0].id)
          return
        }
        if (key === 'End') {
          take()
          actions.onSelect(rows[rows.length - 1].id)
          return
        }
        if (key === 'PageDown' || key === 'PageUp') {
          take()
          actions.onPage(key === 'PageDown' ? 1 : -1)
          return
        }
      }

      // Left/Right change tab. A text field returned above, so the caret is never stolen.
      if (key === 'ArrowLeft' || key === 'ArrowRight') {
        take()
        actions.onTab(key === 'ArrowRight' ? 1 : -1)
        return
      }

      // Escape is global: clear the search first, then hide the window. A focused button or tab
      // must not swallow it, or the popup could not be dismissed from the keyboard.
      if (key === 'Escape') {
        take()
        if (search) actions.onSearch('')
        else actions.onHide()
        return
      }

      // F2 is global too: open the editor. While the editor is open it owns F2 and closes itself.
      if (key === 'F2') {
        if (selected) {
          take()
          actions.onEdit()
        }
        return
      }

      // Digits 1-9 are ordinary search text; `/5` (handled by the search field) is the paste key.
      if (key === 'Delete') {
        if (selected) {
          take()
          actions.onDelete()
        }
        return
      }

      // Printable text starts a search from anywhere that is not a text field, so the window is
      // usable the instant it appears. Space and the digit paste keys are the only exceptions.
      if (key.length === 1 && key !== ' ') {
        take()
        actions.onSearchFocus()
        actions.onSearch(search + key)
        return
      }

      // A focused button or tab keeps its own Enter/Space.
      if (onControl) return

      if (key === 'Enter' && selected) {
        take()
        actions.onPaste(selected, false)
      }
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [enabled, rows, selectedId, search, searchRef, actions])
}
