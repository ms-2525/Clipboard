import { useCallback, useEffect, useRef, useState, type RefObject } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import * as Switch from '@radix-ui/react-switch'
import * as Slider from '@radix-ui/react-slider'
import type { HostSettings, SaveConfigResult, SettingsPatch } from './host'
import { formatBytes } from './ClipInfo'
import { ShortcutsDialog } from './ShortcutsDialog'
import { IconBell, IconChevron, IconClipboard, IconClose, IconDatabase, IconInfo, IconMonitor, IconMoon, IconSettings, IconSun } from './icons'

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  settings: HostSettings
  /** Applies a theme immediately so the dialog previews its own choice. */
  onPreviewTheme: (theme: 'light' | 'dark') => void
  /** Zoom is live: presets apply and persist as soon as they are chosen. */
  onZoom: (zoom: number) => void
  onSave: (patch: SettingsPatch) => Promise<SaveConfigResult>
  onClearHistory: () => void
  /** Fresh clip.db usage for the Clear row; advisory, so a failure simply shows no readout. */
  onStorageUsage: () => Promise<{ bytes: number; limit: number }>
  /** Shows the real native toast at its real coordinates with the current settings. */
  onPreviewToast: () => void
  /** The Settings button; Radix has no Trigger here, so close restores focus to it explicitly. */
  returnFocus?: RefObject<HTMLElement | null>
}

const ZOOMS = [80, 100, 125, 150]
const MAX_LINES = 10
const messageOf = (error: unknown) => (error instanceof Error ? error.message : String(error))

/** Modifier order the host's parser accepts (and the one the old settings used). */
const MODIFIERS = ['Ctrl', 'Alt', 'Shift', 'Win'] as const

/** KeyboardEvent → "Ctrl+Shift+V", the combo string HotKeyWindow parses. */
function comboOf(event: KeyboardEvent): string | null {
  const key = event.key
  if (key === 'Control' || key === 'Alt' || key === 'Shift' || key === 'Meta') return null
  let name = key
  if (/^[a-z]$/i.test(key)) name = key.toUpperCase()
  else if (/^[0-9]$/.test(key) === true) name = key
  else if (key === ' ') name = 'Space'
  else if (key === '+') name = '='
  else if (key === 'Escape' || key === 'Tab' || key === 'Enter' || key === 'Backspace') return null
  else if (/^F\d{1,2}$/i.test(key)) name = key.toUpperCase()
  else if (key.length > 1) name = key[0].toUpperCase() + key.slice(1).toLowerCase()
  const mods = MODIFIERS.filter((mod) =>
    mod === 'Ctrl' ? event.ctrlKey || event.metaKey : mod === 'Alt' ? event.altKey : mod === 'Shift' ? event.shiftKey : false,
  )
  if (mods.length === 0) return null
  return [...mods, name].join('+')
}

function Keycaps({ combo }: { combo: string }) {
  return (
    <span className="keycaps">
      {combo.split('+').filter(Boolean).map((part, index) => (
        <span className="keycap" key={`${part}-${index}`}>
          {part}
        </span>
      ))}
    </span>
  )
}

type Section = 'general' | 'capture' | 'appearance' | 'notifications' | 'storage' | 'about'

const SECTIONS: { id: Section; label: string; icon: React.ComponentType }[] = [
  { id: 'general', label: 'General', icon: IconSettings },
  { id: 'capture', label: 'Capture', icon: IconClipboard },
  { id: 'appearance', label: 'Appearance', icon: IconSun },
  { id: 'notifications', label: 'Notifications', icon: IconBell },
  { id: 'storage', label: 'Storage', icon: IconDatabase },
  { id: 'about', label: 'About', icon: IconInfo },
]

export function SettingsDialog({ open, onOpenChange, settings, onPreviewTheme, onZoom, onSave, onClearHistory, onPreviewToast, onStorageUsage, returnFocus }: Props) {
  const [autostart, setAutostart] = useState(settings.startOnLogin)
  const [cap, setCap] = useState(settings.maxItems)
  const [showImagesInAll, setShowImagesInAll] = useState(settings.showImagesInAll)
  const [lines, setLines] = useState(settings.notification.maxLines)
  const [toastSize, setToastSize] = useState(settings.notification.size ?? 0)
  const [themeChoice, setThemeChoice] = useState(settings.themeChoice)
  const [timeFormat, setTimeFormat] = useState(settings.timeFormat)
  const [hotkey, setHotkey] = useState(settings.hotkey)
  const [capturing, setCapturing] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [confirmClear, setConfirmClear] = useState(false)
  const [storage, setStorage] = useState<{ bytes: number; limit: number } | null>(null)
  const [showShortcuts, setShowShortcuts] = useState(false)
  const [section, setSection] = useState<Section>('general')
  const shortcutsReturn = useRef<HTMLElement | null>(null)
  const captureRef = useRef<HTMLButtonElement>(null)
  const bodyRef = useRef<HTMLDivElement>(null)
  const navRef = useRef<HTMLDivElement>(null)
  const wasOpen = useRef(false)
  // The fade is the scroll affordance: it is present exactly while content remains below.
  const [more, setMore] = useState(false)
  // The scroll gutter exists only while the body really scrolls (a 10px reserve would skew the pane).
  const [scrolls, setScrolls] = useState(false)
  const measure = useCallback(() => {
    const body = bodyRef.current
    if (body) {
      setMore(body.scrollHeight - body.scrollTop - body.clientHeight > 4)
      setScrolls(body.scrollHeight > body.clientHeight + 1)
    }
  }, [])

  // Every open starts from the saved values; a live zoom change must not reset the draft underneath.
  useEffect(() => {
    if (open && !wasOpen.current) {
      setAutostart(settings.startOnLogin)
      setCap(settings.maxItems)
      setShowImagesInAll(settings.showImagesInAll)
      setLines(settings.notification.maxLines)
      setToastSize(settings.notification.size ?? 0)
      setThemeChoice(settings.themeChoice)
      setTimeFormat(settings.timeFormat)
      setHotkey(settings.hotkey)
      setCapturing(false)
      setSaving(false)
      setError('')
      setConfirmClear(false)
      setStorage(null)
      setSection('general')
      // Advisory readout: a failure leaves the Clear row without a usage figure, never an error.
      void onStorageUsage().then(setStorage).catch(() => {})
    }
    wasOpen.current = open
  }, [open, settings, onStorageUsage])

  // Re-measure when the dialog opens, a section changes, a group expands or the window resizes.
  useEffect(() => {
    if (!open) return
    const frame = requestAnimationFrame(measure)
    window.addEventListener('resize', measure)
    return () => {
      cancelAnimationFrame(frame)
      window.removeEventListener('resize', measure)
    }
  }, [open, section, confirmClear, error, capturing, measure])

  // Hotkey capture: the field takes the next chord, Esc cancels the capture only (not the dialog).
  useEffect(() => {
    if (!open || !capturing) return
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        event.stopPropagation()
        setCapturing(false)
        return
      }
      event.preventDefault()
      event.stopPropagation()
      const combo = comboOf(event)
      if (!combo) return
      setHotkey(combo)
      setError('')
      setCapturing(false)
    }
    window.addEventListener('keydown', onKey, true)
    return () => window.removeEventListener('keydown', onKey, true)
  }, [open, capturing])

  const pickTheme = (choice: 'light' | 'dark' | 'system') => {
    setThemeChoice(choice)
    if (choice !== 'system') onPreviewTheme(choice)
  }

  const pickZoom = (zoom: number) => {
    setCapturing(false)
    onZoom(zoom)
  }

  const pickSection = (next: Section) => {
    setSection(next)
    setCapturing(false)
    // Keep the pane's scroll position at the top when switching sections.
    bodyRef.current?.scrollTo({ top: 0 })
  }

  const save = async () => {
    if (saving) return
    setSaving(true)
    setError('')
    try {
      const result = await onSave({
        theme: themeChoice,
        timeFormat,
        maxItems: cap,
        showImagesInAll,
        uiZoom: settings.uiZoom,
        hotkey,
        startOnLogin: autostart,
        notification: { maxLines: lines, size: toastSize },
      })
      if (result.hotkeyError) {
        // The host kept the binding that still works; say which one is live and leave the dialog open.
        setHotkey(result.config.hotkey)
        setError(result.hotkeyError)
        setSection('general')
        return
      }
      onOpenChange(false)
    } catch (failure) {
      setError(messageOf(failure))
      setSection('general')
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
    <Dialog.Root open={open} onOpenChange={(next) => { setCapturing(false); if (!next) setShowShortcuts(false); onOpenChange(next) }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content
          className="dialog settings"
          aria-describedby="settings-desc"
          aria-modal="true"
          onEscapeKeyDown={(event) => {
            // Radix still closes the dialog; this only keeps the same keystroke from
            // reaching the window key matrix, which would hide the whole app.
            event.stopPropagation()
          }}
          onCloseAutoFocus={(event) => {
            // Radix restores focus to a Dialog.Trigger; this dialog has none, so name the button.
            event.preventDefault()
            returnFocus?.current?.focus()
          }}
        >
          <div className="dialog-head">
            <div className="grow">
              <Dialog.Title className="t-title" style={{ margin: 0 }}>
                Settings
              </Dialog.Title>
              <Dialog.Description id="settings-desc" className="t-sec" style={{ marginTop: 2 }}>
                Clip runs in the tray and keeps history local to this machine.
              </Dialog.Description>
            </div>
            <Dialog.Close asChild>
              <button className="icon-btn ghost" aria-label="Close settings">
                <IconClose />
              </button>
            </Dialog.Close>
          </div>

          <div className="dialog-body-wrap">
            <div className={'dialog-body' + (scrolls ? ' scrolling' : '')} ref={bodyRef} onScroll={measure}>
              <div className="settings-shell">
                <nav className="settings-nav" aria-label="Settings sections" ref={navRef}>
                  {SECTIONS.map(({ id, label, icon: Icon }) => (
                    <button
                      key={id}
                      type="button"
                      className="settings-nav-item"
                      data-on={section === id}
                      aria-current={section === id ? 'page' : undefined}
                      onClick={() => pickSection(id)}
                    >
                      <Icon />
                      {label}
                    </button>
                  ))}
                </nav>

                <div className="settings-pane" key={section} role="tabpanel" aria-label={SECTIONS.find((s) => s.id === section)?.label}>
                  {section === 'general' && (
                    <>
                      <div className="settings-pane-head">
                        <div className="t-title">General</div>
                        <div className="t-sec">Startup, keyboard, and how Clip fits around Windows.</div>
                      </div>
                      <div className="set-group">
                        <div className="set-subtitle">Startup</div>
                        <div className="set-row">
                          <div className="grow">
                            <div className="label">Launch at login</div>
                            <div className="t-sec">Capture clips as soon as Windows starts.</div>
                          </div>
                          <Switch.Root className="switch" checked={autostart} onCheckedChange={setAutostart} aria-label="Launch at login">
                            <span className="thumbdot" />
                          </Switch.Root>
                        </div>
                        <div className="set-row" data-disabled="true" title="The v2 shell always starts in the tray">
                          <div className="grow">
                            <div className="label">Start hidden in the tray</div>
                            <div className="t-sec">The v2 build always starts in the tray; open it with the hotkey.</div>
                          </div>
                          <Switch.Root className="switch" checked disabled aria-label="Start hidden">
                            <span className="thumbdot" />
                          </Switch.Root>
                        </div>
                      </div>

                      <div className="set-group">
                        <div className="set-subtitle">Keyboard</div>
                        <div className="set-row">
                          <div className="grow">
                            <div className="label">Open Clip hotkey</div>
                            <div className="t-sec">
                              {capturing ? 'Press the new combination. Esc keeps the current one.' : 'Click the keys, then type the new combination.'}
                            </div>
                            {error && (
                              <div className="t-sec set-error" role="alert">
                                {error}
                              </div>
                            )}
                          </div>
                          <button
                            ref={captureRef}
                            className={'btn ghost hotkey-field' + (capturing ? ' capturing' : '')}
                            onClick={() => {
                              setError('')
                              setCapturing((on) => !on)
                            }}
                            onBlur={() => setCapturing(false)}
                            aria-label="Change the open Clip hotkey"
                          >
                            <Keycaps combo={capturing ? 'Press keys' : hotkey} />
                          </button>
                        </div>
                        <div className="set-row">
                          <div className="grow">
                            <div className="label">Time format</div>
                            <div className="t-sec">List timestamps as 24-hour or 12-hour.</div>
                          </div>
                          <div className="radio-inline" role="radiogroup" aria-label="Time format">
                            <button className="radio-card" data-on={timeFormat === '24h'} role="radio" aria-checked={timeFormat === '24h'} onClick={() => setTimeFormat('24h')} type="button">
                              24-hour
                            </button>
                            <button className="radio-card" data-on={timeFormat === '12h'} role="radio" aria-checked={timeFormat === '12h'} onClick={() => setTimeFormat('12h')} type="button">
                              12-hour
                            </button>
                          </div>
                        </div>
                      </div>
                    </>
                  )}

                  {section === 'capture' && (
                    <>
                      <div className="settings-pane-head">
                        <div className="t-title">Capture</div>
                        <div className="t-sec">What Clip keeps, and what it skips.</div>
                      </div>
                      <div className="set-group">
                        <div className="set-subtitle">What to keep</div>
                        <div className="set-row">
                          <div className="grow">
                            <div className="label">History limit</div>
                            <div className="t-sec">Oldest unpinned clips are removed first.</div>
                          </div>
                          <Slider.Root className="slider" min={100} max={5000} step={100} value={[cap]} onValueChange={([value]) => setCap(value)} aria-label="History limit">
                            <Slider.Track className="slider-track">
                              <Slider.Range className="slider-range" />
                            </Slider.Track>
                            <Slider.Thumb className="slider-thumb" />
                          </Slider.Root>
                          <span className="set-value">{cap.toLocaleString()}</span>
                        </div>
                        <div className="set-row">
                          <div className="grow">
                            <div className="label">Show images in Inbox</div>
                            <div className="t-sec">When off, image clips appear only in the Images tab.</div>
                          </div>
                          <Switch.Root className="switch" checked={showImagesInAll} onCheckedChange={setShowImagesInAll} aria-label="Show images in Inbox">
                            <span className="thumbdot" />
                          </Switch.Root>
                        </div>
                      </div>
                      <div className="set-group">
                        <div className="set-subtitle">Format</div>
                        <div className="set-row" data-disabled="true" title="Rich capture is on in the v2 build">
                          <div className="grow">
                            <div className="label">Plain text only</div>
                            <div className="t-sec">Rich capture is always on; Ctrl+Shift+V pastes plain when you want it.</div>
                          </div>
                          <Switch.Root className="switch" checked={false} disabled aria-label="Plain text only">
                            <span className="thumbdot" />
                          </Switch.Root>
                        </div>
                      </div>
                    </>
                  )}

                  {section === 'appearance' && (
                    <>
                      <div className="settings-pane-head">
                        <div className="t-title">Appearance</div>
                        <div className="t-sec">Theme and zoom preview live; zoom saves the moment it changes.</div>
                      </div>
                      <div className="set-group">
                        <div className="set-subtitle">Theme</div>
                        <div className="radio-row" role="radiogroup" aria-label="Theme">
                          <button className="radio-card" data-on={themeChoice === 'light'} role="radio" aria-checked={themeChoice === 'light'} onClick={() => pickTheme('light')} type="button">
                            <IconSun />
                            Light
                          </button>
                          <button className="radio-card" data-on={themeChoice === 'dark'} role="radio" aria-checked={themeChoice === 'dark'} onClick={() => pickTheme('dark')} type="button">
                            <IconMoon />
                            Dark
                          </button>
                          <button className="radio-card" data-on={themeChoice === 'system'} role="radio" aria-checked={themeChoice === 'system'} onClick={() => pickTheme('system')} type="button">
                            <IconMonitor />
                            System
                          </button>
                        </div>
                      </div>
                      <div className="set-group">
                        <div className="set-subtitle">Zoom</div>
                        <div className="radio-row" role="radiogroup" aria-label="Zoom">
                          {ZOOMS.map((zoom) => (
                            <button key={zoom} className="radio-card" data-on={settings.uiZoom === zoom} role="radio" aria-checked={settings.uiZoom === zoom} onClick={() => pickZoom(zoom)} type="button">
                              {zoom}%
                            </button>
                          ))}
                        </div>
                        <div className="t-sec">Zoom is saved as soon as it changes; Ctrl + / Ctrl − step it.</div>
                      </div>
                    </>
                  )}

                  {section === 'notifications' && (
                    <>
                      <div className="settings-pane-head">
                        <div className="t-title">Notifications</div>
                        <div className="t-sec">The toast that confirms each capture. Preview shows the real one.</div>
                      </div>
                      <div className="set-group">
                        <div className="set-row">
                          <div className="grow">
                            <div className="label">Toast lines</div>
                            <div className="t-sec">Longest toast body; the toast itself still auto-fits.</div>
                          </div>
                          <Slider.Root className="slider" min={1} max={MAX_LINES} step={1} value={[lines]} onValueChange={([value]) => setLines(value)} aria-label="Toast lines">
                            <Slider.Track className="slider-track">
                              <Slider.Range className="slider-range" />
                            </Slider.Track>
                            <Slider.Thumb className="slider-thumb" />
                          </Slider.Root>
                          <span className="set-value">{lines}</span>
                          <button className="btn ghost sm" onClick={onPreviewToast} type="button">
                            Preview
                          </button>
                        </div>
                        <div className="set-row">
                          <div className="grow">
                            <div className="label">Toast size</div>
                            <div className="t-sec">0 is the default size, 10 is double. Preview shows the real one.</div>
                          </div>
                          <Slider.Root className="slider" min={0} max={10} step={1} value={[toastSize]} onValueChange={([value]) => setToastSize(value)} aria-label="Toast size">
                            <Slider.Track className="slider-track">
                              <Slider.Range className="slider-range" />
                            </Slider.Track>
                            <Slider.Thumb className="slider-thumb" />
                          </Slider.Root>
                          <span className="set-value">{toastSize === 0 ? 'Default size' : toastSize === 10 ? '2x' : `${toastSize} · ${(1 + toastSize / 10).toFixed(1)}×`}</span>
                        </div>
                      </div>
                    </>
                  )}

                  {section === 'storage' && (
                    <>
                      <div className="settings-pane-head">
                        <div className="t-title">Storage</div>
                        <div className="t-sec">Everything stays in a local database on this machine.</div>
                      </div>
                      <div className="set-group">
                        <div className="set-subtitle">Local database</div>
                        {storage && storage.limit > 0 && (
                          <div className="storage-meter">
                            <div
                              className="storage-meter-track"
                              role="progressbar"
                              aria-label="Clip database usage"
                              aria-valuemin={0}
                              aria-valuemax={storage.limit}
                              aria-valuenow={Math.min(storage.bytes, storage.limit)}
                            >
                              <div
                                className="storage-meter-fill"
                                style={{ width: `${Math.min(100, (storage.bytes / storage.limit) * 100).toFixed(1)}%` }}
                              />
                            </div>
                            <div className="t-sec">
                              {formatBytes(storage.bytes)} of {formatBytes(storage.limit)} used
                            </div>
                          </div>
                        )}
                        {confirmClear ? (
                          <div className="set-row">
                            <div className="grow">
                              <div className="label">Delete all clips in Clipboard and Images?</div>
                              <div className="t-sec">Pinned clips and tabs stay. This cannot be undone.</div>
                            </div>
                            <button className="btn ghost sm" onClick={() => setConfirmClear(false)}>
                              Keep clips
                            </button>
                            <button
                              className="btn danger sm"
                              onClick={() => {
                                setConfirmClear(false)
                                onOpenChange(false)
                                onClearHistory()
                              }}
                            >
                              Delete clips
                            </button>
                          </div>
                        ) : (
                          <div className="set-row">
                            <div className="grow">
                              <div className="label">Clear history</div>
                              <div className="t-sec">Delete all clips in Clipboard and Images; pinned clips and tabs stay.</div>
                            </div>
                            <button className="btn sm" onClick={() => setConfirmClear(true)}>
                              Clear…
                            </button>
                          </div>
                        )}
                      </div>
                    </>
                  )}

                  {section === 'about' && (
                    <>
                      <div className="settings-pane-head">
                        <div className="t-title">About</div>
                        <div className="t-sec">Known limits of this build, where they bite.</div>
                      </div>
                      <div className="set-group">
                        <div className="t-sec">Colour emoji can show as boxes in the preview; the copy itself is untouched.</div>
                        <div className="t-sec">HTML from Google Docs, Notion or a web page is simplified on the way back into those apps; the words stay.</div>
                        <div className="t-sec">OCR text is there to find a picture. Never read an amount from it.</div>
                        <div className="t-sec">Excel turns a leading zero, 007, into 7 when it opens a CSV; Clip writes the file correctly.</div>
                      </div>
                    </>
                  )}
                </div>
              </div>
            </div>
            {more && (
              <div className="scroll-fade" aria-hidden="true">
                <IconChevron />
              </div>
            )}
          </div>

          <div className="dialog-foot">
            <button
              className="btn ghost"
              style={{ marginRight: 'auto' }}
              type="button"
              ref={(el) => {
                shortcutsReturn.current = el
              }}
              onClick={() => setShowShortcuts(true)}
            >
              Shortcuts
            </button>
            <Dialog.Close asChild>
              <button className="btn ghost">Cancel</button>
            </Dialog.Close>
            <button className="btn primary" onClick={() => void save()} disabled={saving}>
              {saving ? 'Saving…' : 'Save changes'}
            </button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
    <ShortcutsDialog
      open={showShortcuts && open}
      onOpenChange={setShowShortcuts}
      openHotkey={hotkey}
      plainHotkey={settings.hotkeyPlain}
      returnFocus={shortcutsReturn}
    />
    </>
  )
}
