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
  onSearch: (value: string) => void
  onSearchFocus: () => void
  /** Move a measured page (the list owns the row heights). */
  onPage: (direction: 1 | -1) => void
  onPaste: (clip: ClipItem, plain: boolean) => void
  onEdit: () => void
  onDelete: () => void
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
      const typing = target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable
      const onControl = !!target.closest('button, a, [role="tab"]')
      const mod = event.ctrlKey || event.metaKey
      const key = event.key
      const lower = key.toLowerCase()
      const selected = rows.find((clip) => clip.id === selectedId) ?? null
      const index = rows.findIndex((clip) => clip.id === selectedId)

      const take = () => event.preventDefault()

      if (event.altKey) return

      if (mod) {
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
        if (lower === 'z') {
          take()
          actions.onRestore()
          return
        }
        // Ctrl+A/C/X stay with the search field.
        return
      }

      // Navigation is measured by the list, so it works in the search box too.
      if (rows.length > 0 && !onControl) {
        const at = index < 0 ? 0 : index
        if (key === 'ArrowDown') {
          take()
          actions.onSelect(rows[Math.min(rows.length - 1, at + 1)].id)
          return
        }
        if (key === 'ArrowUp') {
          take()
          actions.onSelect(rows[Math.max(0, at - 1)].id)
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

      // A focused button or tab keeps its own Enter/Space.
      if (onControl) return

      if (key === 'Enter') {
        if (selected) {
          take()
          actions.onPaste(selected, false)
        }
        return
      }
      if (key === 'F2') {
        if (selected) {
          take()
          actions.onEdit()
        }
        return
      }
      if (key === 'Escape') {
        take()
        if (search) actions.onSearch('')
        else actions.onHide()
        return
      }
      if (key === 'Delete') {
        if (selected) {
          take()
          actions.onDelete()
        }
        return
      }
      if (/^[1-9]$/.test(key) && !inSearch) {
        const clip = rows[Number(key) - 1]
        if (clip) {
          take()
          actions.onPaste(clip, false)
        }
        return
      }

      // Everything below would have to type; the search box already does it natively.
      if (inSearch || typing) return

      if (key.length === 1) {
        take()
        actions.onSearchFocus()
        actions.onSearch(search + key)
      }
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [enabled, rows, selectedId, search, searchRef, actions])
}
