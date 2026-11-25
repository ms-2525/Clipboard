import { useCallback, useEffect, useRef, useState, type RefObject } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import * as Switch from '@radix-ui/react-switch'
import * as Slider from '@radix-ui/react-slider'
import type { HostSettings, SaveConfigResult, SettingsPatch } from './host'
import { IconChevron, IconClose, IconMonitor, IconMoon, IconSun } from './icons'

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
  else if (/^[0-9]$/.test(key)) name = key
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

export function SettingsDialog({ open, onOpenChange, settings, onPreviewTheme, onZoom, onSave, onClearHistory, returnFocus }: Props) {
  const [autostart, setAutostart] = useState(settings.startOnLogin)
  const [cap, setCap] = useState(settings.maxItems)
  const [lines, setLines] = useState(settings.notification.maxLines)
  const [themeChoice, setThemeChoice] = useState(settings.themeChoice)
  const [timeFormat, setTimeFormat] = useState(settings.timeFormat)
  const [hotkey, setHotkey] = useState(settings.hotkey)
  const [capturing, setCapturing] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [confirmClear, setConfirmClear] = useState(false)
  const captureRef = useRef<HTMLButtonElement>(null)
  const bodyRef = useRef<HTMLDivElement>(null)
  const wasOpen = useRef(false)
  // The fade is the scroll affordance: it is present exactly while content remains below.
  const [more, setMore] = useState(false)
  // The scroll gutter exists only while the body really scrolls (a 10px reserve skews the two columns).
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
      setLines(settings.notification.maxLines)
      setThemeChoice(settings.themeChoice)
      setTimeFormat(settings.timeFormat)
      setHotkey(settings.hotkey)
      setCapturing(false)
      setSaving(false)
      setError('')
      setConfirmClear(false)
    }
    wasOpen.current = open
  }, [open, settings])

  // Re-measure when the dialog opens, a group expands or the window resizes.
  useEffect(() => {
    if (!open) return
    const frame = requestAnimationFrame(measure)
    window.addEventListener('resize', measure)
    return () => {
      cancelAnimationFrame(frame)
      window.removeEventListener('resize', measure)
    }
  }, [open, confirmClear, error, capturing, measure])

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

  const save = async () => {
    if (saving) return
    setSaving(true)
    setError('')
    try {
      const result = await onSave({
        theme: themeChoice,
        timeFormat,
        maxItems: cap,
        uiZoom: settings.uiZoom,
        hotkey,
        startOnLogin: autostart,
        notification: { maxLines: lines },
      })
      if (result.hotkeyError) {
        // The host kept the binding that still works; say which one is live and leave the dialog open.
        setHotkey(result.config.hotkey)
        setError(result.hotkeyError)
        return
      }
      onOpenChange(false)
    } catch (failure) {
      setError(messageOf(failure))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog.Root open={open} onOpenChange={(next) => { setCapturing(false); onOpenChange(next) }}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content
          className="dialog settings"
          aria-describedby="settings-desc"
          aria-modal="true"
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
            <div className="set-cols">
            <div className="set-col">
            <div className="set-group">
              <div className="t-micro">General</div>
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
            </div>

            <div className="set-group">
              <div className="t-micro">Capture</div>
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
              </div>
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
            </div>

            <div className="set-col">
            <div className="set-group">
              <div className="t-micro">Appearance</div>
              <div className="radio-row">
                <button className="radio-card" data-on={themeChoice === 'light'} onClick={() => pickTheme('light')} type="button">
                  <IconSun />
                  Light
                </button>
                <button className="radio-card" data-on={themeChoice === 'dark'} onClick={() => pickTheme('dark')} type="button">
                  <IconMoon />
                  Dark
                </button>
                <button className="radio-card" data-on={themeChoice === 'system'} onClick={() => pickTheme('system')} type="button">
                  <IconMonitor />
                  System
                </button>
              </div>
              <div className="radio-row" aria-label="Zoom">
                {ZOOMS.map((zoom) => (
                  <button key={zoom} className="radio-card" data-on={settings.uiZoom === zoom} onClick={() => pickZoom(zoom)} type="button">
                    {zoom}%
                  </button>
                ))}
              </div>
              <div className="t-sec">Zoom is saved as soon as it changes; Ctrl + / Ctrl − step it.</div>
            </div>

            <div className="set-group">
              <div className="t-micro">Time</div>
              <div className="radio-row">
                <button className="radio-card" data-on={timeFormat === '24h'} onClick={() => setTimeFormat('24h')} type="button">
                  24-hour
                </button>
                <button className="radio-card" data-on={timeFormat === '12h'} onClick={() => setTimeFormat('12h')} type="button">
                  12-hour
                </button>
              </div>
            </div>

            <div className="set-group">
              <div className="t-micro">Data</div>
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
            <Dialog.Close asChild>
              <button className="btn ghost">Cancel</button>
            </Dialog.Close>
            <button className="btn" onClick={() => void save()} disabled={saving}>
              {saving ? 'Saving…' : 'Save changes'}
            </button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
