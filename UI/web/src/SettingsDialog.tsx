import { useState } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import * as Switch from '@radix-ui/react-switch'
import * as Slider from '@radix-ui/react-slider'
import { IconClose, IconMoon, IconSun } from './icons'

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  theme: 'light' | 'dark'
  setTheme: (theme: 'light' | 'dark') => void
}

export function SettingsDialog({ open, onOpenChange, theme, setTheme }: Props) {
  const [autostart, setAutostart] = useState(true)
  const [hidden, setHidden] = useState(false)
  const [cap, setCap] = useState(1000)
  const [lines, setLines] = useState(3)
  const [plain, setPlain] = useState(false)

  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content className="dialog" aria-describedby="settings-desc" onOpenAutoFocus={(event) => event.preventDefault()}>
          <div className="dialog-head">
            <div className="grow">
              <Dialog.Title className="t-title" style={{ margin: 0 }}>
                Settings
              </Dialog.Title>
              <Dialog.Description id="settings-desc" className="t-sec" style={{ marginTop: 2 }}>
                Clip runs in the tray and keeps history local to this machine. Preview — settings are not saved yet.
              </Dialog.Description>
            </div>
            <Dialog.Close asChild>
              <button className="icon-btn ghost" aria-label="Close settings">
                <IconClose />
              </button>
            </Dialog.Close>
          </div>

          <div className="dialog-body">
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
              <div className="set-row">
                <div className="grow">
                  <div className="label">Start hidden in the tray</div>
                  <div className="t-sec">No window on launch; open with the global hotkey.</div>
                </div>
                <Switch.Root className="switch" checked={hidden} onCheckedChange={setHidden} aria-label="Start hidden">
                  <span className="thumbdot" />
                </Switch.Root>
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
                  <div className="label">Preview lines</div>
                  <div className="t-sec">Clamp rows in the list; the full text stays in the tooltip.</div>
                </div>
                <Slider.Root className="slider" min={1} max={10} step={1} value={[lines]} onValueChange={([value]) => setLines(value)} aria-label="Preview lines">
                  <Slider.Track className="slider-track">
                    <Slider.Range className="slider-range" />
                  </Slider.Track>
                  <Slider.Thumb className="slider-thumb" />
                </Slider.Root>
                <span className="set-value">{lines}</span>
              </div>
              <div className="set-row">
                <div className="grow">
                  <div className="label">Plain text only</div>
                  <div className="t-sec">Drop rich formatting and images when capturing.</div>
                </div>
                <Switch.Root className="switch" checked={plain} onCheckedChange={setPlain} aria-label="Plain text only">
                  <span className="thumbdot" />
                </Switch.Root>
              </div>
            </div>

            <div className="set-group">
              <div className="t-micro">Appearance</div>
              <div className="radio-row">
                <button className="radio-card" data-on={theme === 'light'} onClick={() => setTheme('light')} type="button">
                  <IconSun />
                  Light
                </button>
                <button className="radio-card" data-on={theme === 'dark'} onClick={() => setTheme('dark')} type="button">
                  <IconMoon />
                  Dark
                </button>
              </div>
            </div>
          </div>

          <div className="dialog-foot">
            <Dialog.Close asChild>
              <button className="btn ghost">Cancel</button>
            </Dialog.Close>
            <button className="btn" disabled title="Settings editing is not wired yet — this is a preview">
              Save changes
            </button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
