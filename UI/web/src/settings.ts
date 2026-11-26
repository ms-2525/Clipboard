/** The settings shape the host persists in %APPDATA%\Clip\config.json, and the patch the page sends. */

export interface HostConfig {
  theme?: 'light' | 'dark'
  /** "24h" (default) or "12h"; the page formats list timestamps. */
  timeFormat?: string
  [key: string]: unknown
}

export interface HostSettings {
  /** Resolved light|dark: the page only ever renders these two. */
  theme: 'light' | 'dark'
  /** What the user chose, including "system". */
  themeChoice: 'light' | 'dark' | 'system'
  timeFormat: '12h' | '24h'
  maxItems: number
  previewLines: number
  /** False = image clips live only in the Images tab; the All/history list hides them. */
  showImagesInAll: boolean
  uiZoom: number
  hotkey: string
  hotkeyPlain: string
  startOnLogin: boolean
  notification: { width: number; height: number; maxLines: number; size: number; position: string }
}

/** Only the fields the dialog edits; the host clamps and validates them. */
export interface SettingsPatch {
  theme?: 'light' | 'dark' | 'system'
  timeFormat?: '12h' | '24h'
  maxItems?: number
  /** Row preview lines (1..20); every non-image row clamps to this many wrapped lines. */
  previewLines?: number
  showImagesInAll?: boolean
  uiZoom?: number
  hotkey?: string
  startOnLogin?: boolean
  notification?: Partial<HostSettings['notification']>
}

export interface SaveConfigResult {
  config: HostSettings
  /** A readable conflict message when the hotkey could not be rebound; the old binding is still live. */
  hotkeyError?: string | null
}

/** Host config → a complete settings object; fields a build does not send keep their defaults. */
export function toSettings(config: HostConfig | null | undefined): HostSettings {
  const n = (config?.notification ?? {}) as Partial<HostSettings['notification']>
  const theme = config?.theme === 'dark' ? 'dark' : 'light'
  const choice = config?.themeChoice
  return {
    theme,
    themeChoice: choice === 'dark' || choice === 'system' || choice === 'light' ? choice : theme,
    timeFormat: config?.timeFormat === '12h' ? '12h' : '24h',
    maxItems: typeof config?.maxItems === 'number' ? config.maxItems : 200,
    previewLines:
      typeof config?.previewLines === 'number' && Number.isFinite(config.previewLines)
        ? Math.min(20, Math.max(1, Math.round(config.previewLines)))
        : 10,
    showImagesInAll: config?.showImagesInAll !== false,
    uiZoom: typeof config?.uiZoom === 'number' ? config.uiZoom : 100,
    hotkey: typeof config?.hotkey === 'string' ? config.hotkey : 'Alt+V',
    hotkeyPlain: typeof config?.hotkeyPlain === 'string' ? config.hotkeyPlain : 'Ctrl+Shift+V',
    startOnLogin: config?.startOnLogin !== false,
    notification: {
      width: typeof n.width === 'number' ? n.width : 420,
      height: typeof n.height === 'number' ? n.height : 160,
      maxLines: typeof n.maxLines === 'number' ? n.maxLines : 3,
      size: typeof n.size === 'number' && n.size >= 0 && n.size <= 10 ? n.size : 0,
      position: typeof n.position === 'string' ? n.position : 'bottom-right',
    },
  }
}
