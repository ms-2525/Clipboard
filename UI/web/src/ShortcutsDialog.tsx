import * as Dialog from '@radix-ui/react-dialog'
import type { RefObject } from 'react'
import { IconClose } from './icons'

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** The live global bindings, not hardcoded guesses. */
  openHotkey: string
  plainHotkey: string
  returnFocus?: RefObject<HTMLElement | null>
}

interface Entry {
  combo: string
  label: string
}

interface Section {
  title: string
  entries: Entry[]
}

/** The whole key matrix in one place: useClipboardKeys + App zoom + editor + pickers. */
function sections(openHotkey: string, plainHotkey: string): Section[] {
  return [
    {
      title: 'Global',
      entries: [
        { combo: openHotkey, label: 'Show Clip' },
        { combo: plainHotkey, label: 'Paste the selected clip as plain text' },
      ],
    },
    {
      title: 'List',
      entries: [
        { combo: 'Enter', label: 'Paste the selected clip' },
        { combo: 'Ctrl+Shift+V', label: 'Paste the selected clip as plain text' },
        { combo: 'Ctrl+1…Ctrl+9', label: 'Paste row 1 to 9' },
        { combo: '↑ / ↓', label: 'Move the selection' },
        { combo: 'Shift+↑ / Shift+↓', label: 'Select a range of rows' },
        { combo: 'Home / End', label: 'First / last row' },
        { combo: 'PageUp / PageDown', label: 'Move one page' },
        { combo: '← / → / Ctrl+Tab', label: 'Change tab' },
        { combo: 'Ctrl+↑ / Ctrl+↓', label: 'Previous / next day' },
        { combo: 'Ctrl+Shift+← / Ctrl+Shift+→', label: 'Move the clip to the next tab' },
        { combo: 'Esc', label: 'Clear the search, then hide Clip' },
      ],
    },
    {
      title: 'Search',
      entries: [
        { combo: 'Ctrl+K', label: 'Focus the search box' },
        { combo: 'Ctrl+F', label: 'Focus the search box' },
        { combo: 'Type', label: 'Search from anywhere in the list' },
      ],
    },
    {
      title: 'Clip',
      entries: [
        { combo: 'F2', label: 'Open the editor' },
        { combo: 'Del', label: 'Delete the selected clips' },
        { combo: 'Ctrl+Z', label: 'Undo the delete' },
        { combo: 'Ctrl+P', label: 'Pin or unpin' },
        { combo: 'Ctrl+M', label: 'Move to a tab' },
        { combo: 'Ctrl+T', label: 'Filter by tag' },
        { combo: 'Ctrl+E', label: 'Export the selected clip' },
      ],
    },
    {
      title: 'Editor',
      entries: [
        { combo: 'Ctrl+S', label: 'Save and keep editing' },
        { combo: 'F2', label: 'Close the editor' },
        { combo: 'Esc', label: 'Close, asking first when dirty' },
      ],
    },
    {
      title: 'View',
      entries: [
        { combo: 'Ctrl+0', label: 'Zoom to 100%' },
        { combo: 'Ctrl++ / Ctrl+−', label: 'Step the zoom' },
      ],
    },
    {
      title: 'Dialogs',
      entries: [{ combo: '↑↓ Enter Esc', label: 'Choose, confirm, close' }],
    },
  ]
}

function Combo({ combo }: { combo: string }) {
  return (
    <span className="keycaps" style={{ justifyContent: 'flex-end', flexShrink: 0 }}>
      {combo.split(' ').map((word, index) => (
        <span key={`${word}-${index}`} style={{ display: 'inline-flex', gap: 3, alignItems: 'center' }}>
          {word.split('+').map((part, partIndex) => (
            <span className="keycap" key={`${part}-${partIndex}`}>
              {part}
            </span>
          ))}
        </span>
      ))}
    </span>
  )
}

export function ShortcutsDialog({ open, onOpenChange, openHotkey, plainHotkey, returnFocus }: Props) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content
          className="dialog"
          style={{ width: 440 }}
          aria-describedby="shortcuts-desc"
          aria-modal="true"
          onEscapeKeyDown={(event) => {
            event.stopPropagation()
          }}
          onCloseAutoFocus={(event) => {
            event.preventDefault()
            returnFocus?.current?.focus()
          }}
        >
          <div className="dialog-head">
            <div className="grow">
              <Dialog.Title className="t-title" style={{ margin: 0 }}>
                Keyboard shortcuts
              </Dialog.Title>
              <Dialog.Description id="shortcuts-desc" className="t-sec" style={{ marginTop: 2 }}>
                Everything Clip answers to, in one place.
              </Dialog.Description>
            </div>
            <Dialog.Close asChild>
              <button className="icon-btn ghost" aria-label="Close shortcuts">
                <IconClose />
              </button>
            </Dialog.Close>
          </div>

          <div className="dialog-body-wrap">
            <div className="dialog-body">
              {sections(openHotkey, plainHotkey).map((section) => (
                <div className="set-group" key={section.title}>
                  <div className="t-micro">{section.title}</div>
                  {section.entries.map((entry) => (
                    <div className="set-row" key={entry.combo + entry.label}>
                      <div className="grow">
                        <div className="label" style={{ fontWeight: 400 }}>
                          {entry.label}
                        </div>
                      </div>
                      <Combo combo={entry.combo} />
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </div>

          <div className="dialog-foot">
            <span className="t-meta grow">Esc close</span>
            <Dialog.Close asChild>
              <button className="btn">Done</button>
            </Dialog.Close>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
