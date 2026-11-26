interface Entry {
  combo: string
  label: string
}

export interface ShortcutSection {
  title: string
  entries: Entry[]
}

/** The whole key matrix in one place: useClipboardKeys + App zoom + editor + pickers. */
export function shortcutSections(openHotkey: string, plainHotkey: string): ShortcutSection[] {
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

export function ShortcutCombo({ combo }: { combo: string }) {
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
