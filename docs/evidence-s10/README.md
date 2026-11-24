# S10 release evidence — clean profile, embedded page

All shots were taken from a published `dist\Clip.exe` copied to an empty folder and run with
`APPDATA` and `LOCALAPPDATA` pointing at empty temp folders (no `--db`). The screenshots were read
by the builder; `07-final-exe-clean-profile.png` is from the final artifact (208,159,174 bytes).

| Shot | What it proves |
|---|---|
| `01-list-light-empty.png` | The real React page (title `Clip - clipboard`), not the fallback page, on first launch |
| `02-list-light-5-clips.png` | Five clip kinds captured and rendered: rich HTML, files, image, link, text |
| `03-settings-light.png` | Settings opens from the page; values are the persisted defaults |
| `04-settings-dark.png` | Theme switch applies immediately (dialog preview) |
| `05-list-dark-5-clips.png` | Dark theme after Save; the image row gained "Text read from image" from the offline OCR pass that finished after capture |
| `06-toast.png` | Native capture toast, auto-fitted to two lines |
| `07-final-exe-clean-profile.png` | The final published exe on a fresh profile: page loaded from the embedded `clip.assets.web/index.html`, 0 clips |

Measured on these runs:

- Fresh `clip.db` created at `%APPDATA%\Clip\clip.db`; `config.json` and `webview\` beside it.
- Page title asserted through UI Automation: `Clip - clipboard`.
- The list (`role=listbox`, name `Clipboard history`) reported exactly 5 children after the five copies.
- OCR filled the image row ("Text read from image / S10 image clip") after the initial render.
- Memory: ~528 MB total fresh (160 MB Clip + 368 MB across 6 WebView2 processes); ~643 MB after
  five clips including one image.
- Single instance: a second launch left exactly one `Clip.exe` running.

Not captured at release time: the editor overlay and a Notepad paste. An elevated screen-capture
utility (Tools3000) was covering the test desktop from 05:00 onward; its frozen overlay prevented
further on-screen interaction. Those two visuals and the published-exe warm-show timing are the
post-release re-check.
