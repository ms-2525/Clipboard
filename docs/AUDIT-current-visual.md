# Audit — Clip, current visuals

**Type:** technical visual audit of the rendered app (not a design critique)
**Date:** 23 Nov 2026
**Auditor:** independent sub-agent; no part of this codebase was written by me
**Target:** `C:\Users\mayan\CascadeProjects\Clipboard` — WPF `net10.0-windows`, running build `bin\Release\net10.0-windows\Clip.exe` (Clip.dll timestamp 23-11-2026 14:56)
**Environment:** single display 1920×1200 physical, Windows scaling **150 %** (1 DIP = 1.5 px), app manifest `PerMonitorV2`. Every screenshot is a device-pixel capture taken after `SetProcessDpiAwarenessContext(-4)`.
**Method:** all five dimensions checked against rendered pixels, not source assumptions. Contrast computed from the theme dictionaries *and* confirmed by sampling the rendered glyph pixels; alignment measured by pixel scans; text rendered at 4×/8× nearest-neighbour for close reading. Evidence files are in `docs\design-evidence\` and every finding names the file that proves it.

---

## Audit Health Score

| # | Dimension | Score | Key Finding |
|---|-----------|-------|-------------|
| 1 | Accessibility (keyboard / contrast / focus) | **2** | Light theme fails AA on three separate token pairs; the notification's ✕ is not a control and cannot be reached from the keyboard |
| 2 | Performance (render cost visible in the UI) | **2** | Every window is `AllowsTransparency=True` + a 28 px `DropShadowEffect`, which forces the whole window through a bitmap layer; text consequently loses ClearType (measured below) |
| 3 | Appearance & Theming (token discipline) | **3** | One colour system, two dictionaries, both render — but the light dictionary's hairline/surface steps are 1.1–1.3:1, i.e. structurally invisible; radii and type sizes are ad hoc |
| 4 | Platform Conformance (Windows 11 expectations) | **2** | Custom chrome everywhere with no scroll affordance, a disabled "Settings" item as the only route to configuration, and a custom Topmost toast that bypasses Focus Assist |
| 5 | Adaptivity (multi-monitor / DPI / text scale) | **3** | PerMonitorV2 + cursor-screen placement is the right intent and Settings clamps to the work area; the popup's fixed 620 DIP height is never clamped (source-derived, not observed — see limitations) |
| **Total** | | **12/20** | **Acceptable — significant work needed** |

Rating bands: 18–20 Excellent · 14–17 Good · 10–13 Acceptable · 6–9 Poor · 0–5 Critical.

---

## Platform Conformance Verdict

**Pass, with reservations.** This reads as a small native Windows utility, not a ported web page: no web-shaped controls, no hover-dependent affordances, no CSS-like scrollbars, real OS clipboard behaviour, real global hotkey, PerMonitorV2 manifest, and a coherent dark shell that a Windows 11 user would recognise as its own thing. The keyboard model (Alt+V / ↑↓ / Enter / Ctrl+1..9 / Del / Esc) is the most Windows-native part of the product and it is right.

What stops it from being *fully* native is not imitation — it is omission. Windows 11 gives a developer three things for free and this app declines all three:

1. **Window chrome.** `WindowStyle="None"` + `AllowsTransparency="True"` on every window means no DWM shadow (the shadow is a `DropShadowEffect` that gets clipped by the window rectangle — visible seam in `notif-close-8x.png`), no snap, no native close affordance, and, because the window is layered, **no ClearType**. The type quality problem the owner reports is a direct consequence of this choice, not of the typeface.
2. **Notifications.** The toast is a `Topmost` WPF window, not a Windows notification. It will appear over full-screen apps and will not respect Focus Assist / Do Not Disturb / presentation mode. (Evidence: `notif-dark-3line.png`, `notif-light-3line.png`; mechanism `NotificationWindow.xaml.cs:151-179`.)
3. **Discoverability of the system's own affordances.** The tray menu — the only persistent surface — contains a *disabled* "Settings" entry (`App.xaml.cs:86`), and the popup list has no scrollbar or scroll cue even when a row is sliced through its glyphs (`popup-dark-bottom-3x.png`).

Nothing here is "web in a box". The gap is that the app opted out of the composition host's facilities and then did not re-provide them.

---

## Executive Summary

- **Audit Health Score: 12/20 (Acceptable).**
- **Issues: 1 P0, 8 P1, 11 P2, 4 P3.**
- **The single most consequential fact is not visual:** the repository does not compile. `SettingsWindow.xaml.cs` still reads `_cfg.PreviewLines`, a property that no longer exists on `Config` (`SettingsWindow.xaml.cs:34,235` vs `Core\Config.cs:27-43`). Both `dotnet build -c Debug` on the real csproj and a scratch host linking the real files fail with the same two CS1061 errors. The running exe therefore predates the current source, and *no visual fix can be shipped until this is repaired*.
- **"Text looks bad" is real, and it is measurable.** The app's text is rendered with **greyscale antialiasing only**: 0 coloured edge pixels across 19 135 sampled text-edge pixels in the real popup, versus **1 026** coloured pixels in an opaque control window rendering the identical string with ClearType (`host-aa-opaque.png`). Cause: `AllowsTransparency="True"`. At 13 DIP on a 150 % display this is what makes every glyph look soft and thin. The font itself is fine and is loading correctly (see positives).
- **Light theme has never been usable.** It renders, but three token pairs fail WCAG AA: Accent text **2.11–2.35:1**, the error red **2.39–2.78:1** (computed), and Muted-on-Raised **4.29:1**. Confirmed on screen in `notif-light-3line.png` and `popup-light-meta-4x.png`.
- **The popup clips a line of text through the middle of its glyphs** and offers no scroll cue (`popup-dark-bottom-3x.png`).

Recommended next steps, in order: repair the build, stop paying for `AllowsTransparency`, fix the light token set, then the toast's ✕ and the popup's bottom edge.

---

## Detailed Findings

### P0 — Blocking

#### F1. The repository does not build; the running exe cannot be reproduced from source
- **Location:** `UI\SettingsWindow.xaml.cs:34` and `:235` reference `_cfg.PreviewLines`; `Core\Config.cs` has no such property. `UI\SettingsWindow.xaml:265-275` still ships the "Preview lines" row and error text.
- **Evidence:** `dotnet build -c Debug` on the real project → `CS1061: 'Config' does not contain a definition for 'PreviewLines'` (×2, build FAILED). Reproduced independently in a scratch host linking the same files.
- **Category:** Build integrity (blocks all visual work)
- **Impact:** The 14:56 Release binary does not match the source. Any fix an agent makes cannot be built, tested, or delivered; the next release is impossible as-is. It also explains why `SettingsWindow` is "built but unwired" — it cannot be compiled at all.
- **Recommendation:** Decide the intended policy for "preview lines" (the popup currently hard-codes a 4-line clamp via `MaxHeight="76"`), then either restore `Config.PreviewLines` and wire it into the card template, or delete the row from `SettingsWindow.xaml`/`.cs` and delete the two error strings. Re-run `dotnet build -c Release` to 0/0 before anything else.

---

### P1 — Major

#### F2. Light theme: accent text fails AA (2.11–2.35:1)
- **Location:** `UI\Themes\Light.xaml:11` (`Accent #FF8A3D`); used as text at `UI\NotificationWindow.xaml:29-30` (app name, 12 DIP SemiBold).
- **Evidence:** `notif-light-3line.png` (the word "Clip" in pale orange on white), and `notif-light-header-4x.png` at 4×. Sampled glyph peak is exactly `#FF8A3D`; computed ratios Accent-on-Surface **2.35:1**, on-App **2.11:1**, on-Raised **2.02:1**.
- **Category:** Accessibility / Theming
- **WCAG:** 1.4.3 Contrast (Minimum), 4.5:1 for text below 18.66 px bold / 24 px regular. 12 DIP SemiBold is not "large".
- **Impact:** In light theme the toast's only brand/identity cue is close to unreadable; the same token is used for the selected-card border, which is then the *only* marker of the current row.
- **Recommendation:** Add a *text* accent variant to both dictionaries (e.g. `AccentText` = `#B4551A` in light, keep `#FF8A3D` for fills/borders). Use `AccentText` for the notification app name. Do not reuse the fill accent as a text colour.

#### F3. Light theme: Muted on Raised is 4.29:1 — the weakest text in the product sits on it
- **Location:** `UI\Themes\Light.xaml:8,10`; used at `UI\MainWindow.xaml:80-81` (card meta line, 11 DIP italic) and `UI\SettingsWindow.xaml:165` ("Clear" ghost button).
- **Evidence:** `popup-light-meta-4x.png` (the footer line on the *selected* card, which is the Raised fill), `host-light-settings.png`.
- **Category:** Accessibility / Theming
- **WCAG:** 1.4.3, 4.5:1.
- **Impact:** The meta line is already the smallest, lightest, faked-italic text (see F8); failing contrast pushes it from "quiet" to "illegible", and it carries the only context a user has about where a clip came from.
- **Recommendation:** Darken `Muted` for light to ≈`#5A6270` (Muted-on-Raised ≈ 5.6:1, Muted-on-White ≈ 6.5:1) or introduce `MutedRaised`. Verify with the same table in this report after the change.

#### F4. Light theme: the error colour is below 3:1 — validation messages are not readable
- **Location:** `UI\SettingsWindow.xaml:10` (`ErrorBrush #FF6B6B`), applied at `:237-239, 254-257, 272-275`.
- **Evidence:** computed from tokens — Error-on-Surface **2.78:1**, Error-on-Raised **2.39:1**. *Not observed rendered*: the three error blocks are `Visibility="Collapsed"` and I did not force them (honest limitation, see §What I could not verify).
- **Category:** Accessibility / Theming
- **WCAG:** 1.4.3 (4.5:1).
- **Impact:** In light theme, "Enter a whole number between 10 and 10,000." would be a pale pink whisper — precisely the message that must be unmistakable.
- **Recommendation:** Put `ErrorBrush` in both theme dictionaries with a light value ≈`#C42B2B` (≈5.3:1 on white, ≈4.8:1 on Raised). It is currently the one documented hardcoded colour outside the dictionaries; moving it into the dictionaries removes the exception and fixes it at the same time.

#### F5. All app text is greyscale-antialiased (no ClearType) — this is the "text looks bad" cause
- **Location:** `UI\MainWindow.xaml:5`, `UI\NotificationWindow.xaml:4-5`, `UI\SettingsWindow.xaml:5` — all three windows set `AllowsTransparency="True"`, which forces WPF onto a layered bitmap and disables subpixel antialiasing.
- **Evidence (A/B, same string, same font, same DPI):** `host-aa-opaque.png` renders "Trailing spaces below" twice in one opaque window. Line 1 (ClearType) → **1 026** pixels with colour fringing, worst `#13A3DC`. Line 2 (greyscale forced) → **0**. The real popup (`popup-dark.png`, 13 DIP body) → **0** coloured pixels across 19 135 text-edge pixels. The real toast (`notif-light-3line.png`) → **0**. Macros `popup-dark-card1-body-3x.png`, `notif-light-body-4x.png` show only uniform grey halos, no RGB fringes.
- **Category:** Performance / Appearance
- **Impact:** Greyscale AA on a 150 % display makes 13 DIP Manrope Light-ish strokes look 1–2 px thin and slightly grey. Users read this as "blurry"/"cheap". It is independent of the theme and affects 100 % of the copy in the product.
- **Recommendation (pick one, in order of quality):**
  1. Remove `AllowsTransparency` from all three windows; use `WindowChrome`/`DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE)` for the 11/14 DIP corner and `DwmExtendFrameIntoClientArea` for the DWM shadow. Text then gets ClearType and the shadow stops being clipped (see F14).
  2. If the popup must stay transparent, keep `AllowsTransparency` **only** there and give the text containers an opaque backing at render time — plus compensate typographically: weight 500, size 14 DIP, line height 20.
- **Do not** simply raise font size: F5 is a rendering-mode problem, not a size problem.

#### F6. The notification's ✕ is a `TextBlock`, not a control
- **Location:** `UI\NotificationWindow.xaml:35-48` (`Border` + `TextBlock` with a `MouseLeftButtonUp` handler at `NotificationWindow.xaml.cs:51`).
- **Evidence:** `notif-close-8x.png` (8×) and `notif-light-header-4x.png` — a 14 px (9.3 DIP) glyph in Muted, `Padding="5,1"`, `Margin="0,-6,-8,0"`. Effective hit box ≈ 13×15 DIP (20×22 px).
- **Category:** Accessibility / Conformance
- **WCAG:** 2.1.1 Keyboard (no keyboard path to dismiss), 2.5.8 Target Size (24×24 CSS px minimum), 4.1.2 Name/Role (a TextBlock exposes no invokable control to UIA).
- **Impact:** A keyboard-only user must wait out the 3 s timer — and there is a `Topmost` toast covering part of the screen until then. A touch user has a 22 px target. A screen-reader user is told nothing.
- **Recommendation:** Make it a real `Button` (transparent template) with `AutomationProperties.Name="Dismiss"`, minimum 24×24 DIP, `IsTabStop=True`, Esc as a keyboard dismiss, and align it on the header baseline.

#### F7. The search field has no placeholder and no accessible name
- **Location:** `UI\MainWindow.xaml:48-50` — a borderless `TextBox` with no `Text`, no `AutomationProperties.Name`, no watermark.
- **Evidence:** `popup-dark-search-3x.png` and `popup-light-search-4x.png` — an empty pill with a small magnifier and (dark only) a caret. `popup-dark-emptyresult.png` shows the consequence: an empty box above a huge empty panel.
- **Category:** Accessibility / Communication
- **WCAG:** 3.3.2 Labels or Instructions; 4.1.2 (the field announces as an unnamed edit control).
- **Impact:** First-run users see a blank pill and no instruction; the empty-result state is indistinguishable from "still loading". Screen readers announce no purpose for the field.
- **Recommendation:** Add a watermark `TextBlock` ("Search history…") bound to `Text.IsEmpty` visibility, plus `AutomationProperties.Name="Search clipboard history"`. One extra `TextBlock` and a trigger — no new control.

#### F8. The last visible card is sliced through its glyphs, with no scroll affordance
- **Location:** `UI\MainWindow.xaml:54-57` (`ScrollViewer.VerticalScrollBarVisibility="Hidden"`), window height fixed at `620` (`:4`), list row 1 of a 2-row grid with no fade/padding at the bottom.
- **Evidence:** `popup-dark-bottom-3x.png` (3×): the last card's Meta line is cut horizontally mid-glyph at y=905–912; the viewport ends at 913 with `#08090C`; the card's 10 DIP corner radius is gone. Reproduced in `popup-dark.png`, `popup-light.png`, `popup-dark-filtered.png`.
- **Category:** Conformance / Appearance
- **Impact:** The primary surface looks broken on every open where history exceeds the viewport (i.e. almost always). Users cannot tell whether there is more content, or whether the app truncated their text.
- **Recommendation:** Two lines of XAML: add a bottom fade/inset mask (or `Padding="0,0,0,12"` on the ListBox so rows never touch the edge) and switch the scrollbar to `Auto` — or, if the hidden scrollbar is deliberate, show a 4 DIP "more" affordance / count. Do not clip rows at all: a viewport with `Margin` bottom ≠ 0 and a scrollbar is the cheap fix.

#### F9. Light theme has no visible structure: every boundary is 1.1–1.3:1
- **Location:** `UI\Themes\Light.xaml:6-12` (App `#F2F3F5`, Surface `#FFFFFF`, Raised `#ECEEF1`, Hairline `#E2E5EA`).
- **Evidence:** `popup-light.png` (cards are white on a near-white shell; only the text separates them), `host-light-settings.png` (rows read as pale pills floating on white). Computed: Hairline-on-Surface **1.26:1**, Raised-on-Surface **1.16:1**, Surface-on-App **1.11:1**.
- **Category:** Theming / Accessibility
- **WCAG:** 1.4.11 Non-text Contrast (3:1) for boundaries that convey grouping or state.
- **Impact:** In light theme the card model — the product's central idea — is not perceivable. Rows, fields and panels merge into one sheet; only the selected row's orange border survives, and that fails too (F2). This is why the light theme "has never been verified": on first look it reads as unfinished.
- **Recommendation:** Give light a real step: keep Surface `#FFFFFF`, drop App to `#E9EBEF`, Raised to `#F4F5F7`, and make Hairline `#D3D7DE` (≈1.55:1 — still under 3:1, so also add a 1 px inner top highlight or a subtle 1 px border in a `BorderStrong` token at ≈`#C3C8D1`, 1.8:1). If 3:1 is out of reach tonally, differentiate with elevation (shadow) rather than colour. Verify the final set against the table in §Measurements.

---

### P2 — Minor

#### F10. The meta line is fake-italic, undersized and semantically wrong
- **Location:** `UI\MainWindow.xaml:80-81` (`FontSize="11" FontStyle="Italic"`), `UI\NotificationWindow.xaml:31-32`.
- **Evidence:** `popup-dark-card1-meta-4x.png` (4×), `popup-light-meta-4x.png`. Probe: Manrope has **no italic face** — `FormattedText` advance for the same string at 13 DIP is `127.39` for Normal and `127.39` for Italic, i.e. WPF synthesises the slant by shearing. The result is thin, cramped strokes that fight the body text above them.
- **Category:** Appearance
- **Impact:** The line that should quietly say "where and when" is the least legible thing on the card, and the fake slant reads as a rendering glitch rather than emphasis.
- **Recommendation:** Drop `FontStyle="Italic"`; express provenance with size + colour only (11 DIP, Muted) and, if needed, a 0.02 em letter-spacing. Fake italic should never be used with a family that has no italic.

#### F11. Meta content exposes internals and self-attribution
- **Location:** `Core\ClipboardItem.cs:29-42` (joins `AppName · date · WindowTitle`), `Core\CaptureService.cs:69-76` (`ForegroundApp.Get()` at capture time).
- **Evidence:** `popup-dark-restored.png` — the top card's footer reads literally **`Clip · 23 Nov 15:16 · ClipHotkey`**: the app attributed a clip to itself and printed the *name of its own message-only hotkey window*. Compare `popup-dark.png` (`WindowsTerminal · … · OC | Windows WPF clipboard manager M1`) and `notif-light-3line.png` (`Clip`).
- **Category:** Appearance / Trust
- **Impact:** The footer leaks implementation names and mislabels provenance whenever a copy happens while Clip's own window is still foreground (which is the normal state right after the popup closes). The owner reads this as a bug in the product's honesty.
- **Recommendation:** Skip Clip's own PID/process when resolving the foreground app; and hide the window-title segment when it looks like a tool title (contains `|`, `-- `, or equals the process name). Two guards in `ForegroundApp.Get()`.

#### F12. The toast truncates every logical line mid-word with an ellipsis
- **Location:** `UI\NotificationWindow.xaml.cs:140-149` — each of the first `MaxLines` lines is cut at 120 chars and `"…"` appended.
- **Evidence:** `notif-dark-overflow.png` — three lines, three ellipses, each cutting a word: "…mail the variance sheet to the a…", "…thirteen …", "…wraps multiple t…".
- **Category:** Communication
- **Impact:** A toast that is meant to preview the clip instead produces three broken sentences. The user cannot tell whether the clip itself was truncated.
- **Recommendation:** Truncate on a word boundary (`LastIndexOf(' ')` before 120) and only append the ellipsis to the **last** rendered line; or drop the character cap entirely and let `MaxLines` + `Root.MaxHeight` do the work with a single trailing ellipsis.

#### F13. Line-height is inconsistent between the two surfaces that show the same text
- **Location:** `UI\MainWindow.xaml:78` (`LineHeight="19"`) vs `UI\NotificationWindow.xaml:55-56` (no `LineHeight`).
- **Evidence:** `popup-dark-card2-body-3x.png` (airy, 19 DIP pitch) vs `notif-light-body-4x.png` (visibly tighter; measured font line height 17.76 DIP at 13 DIP size). Same type size, same font, 1.2 DIP difference — and the wrapped toast lines nearly touch (`ledger`'s descender vs `entries`).
- **Category:** Appearance
- **Impact:** Two surfaces showing the same clip look like two different products.
- **Recommendation:** One `LineHeight` in the theme dictionary (e.g. `BodyLineHeight = 19`) and apply it to both.

#### F14. The popup's empty state is a void
- **Location:** `UI\MainWindow.xaml:54-86` — no `ItemsControl` empty template.
- **Evidence:** `popup-dark-emptyresult.png` — query "zzzzzzz", 840×930 px of nothing below the field.
- **Category:** Communication
- **Impact:** "No matches" and "broken" look identical; there is no way to clear the query except selecting the text and pressing Backspace.
- **Recommendation:** A single centred `TextBlock` ("No clips match '…'") shown when filtered count is 0, plus keep the field focused. This is the cheapest UX win in the report.

#### F15. Settings throws away two of its three rails
- **Location:** `UI\SettingsWindow.xaml:16` (`H` margin `2,0,0,3`), `:23` (`Hint` margin `2,2,0,8`), `:31` (`Row` padding `9,5`).
- **Evidence:** `host-light-settings.png` and `settings-light-rows-3x.png`; measured in `host-light-settings-1.png`: section heading ink starts at **x=46**, the label inside the row below it at **x=58** — a 12 px (8 DIP) offset that repeats for every section ("Max items" vs "Items kept in history", "Theme" vs "Appearance", "Clear history" vs "Clipboard history").
- **Category:** Appearance
- **Impact:** The eye gets no clean vertical rail to follow in a 15-row dialog, so the sections read as one undifferentiated ribbon.
- **Recommendation:** Move the heading/hint margin to `9,0,9,3` so headings sit on the same rail as row labels; add 8 DIP of top margin before each `H` to separate sections.

#### F16. Editable fields look like static values
- **Location:** `UI\SettingsWindow.xaml:35-44` (`Field`: `Background="Transparent" BorderThickness="0"`).
- **Evidence:** `host-dark-settings-1.png`, `host-light-settings.png` — "200", "4", "120000", "16" read as right-aligned labels; the only signal that they are inputs is the caret appearing after a click.
- **Category:** Appearance / Affordance
- **Impact:** Users will not discover that max items, preview lines and the numeric notification settings are editable — the dialog looks read-only.
- **Recommendation:** Give `Field` a 1 px `Hairline` border, 4 DIP radius and 4 DIP horizontal padding, or a 1 px underline. Keep it quieter than the buttons.

#### F17. The toast bypasses Focus Assist / Do Not Disturb and floats over full-screen apps
- **Location:** `UI\NotificationWindow.xaml:5` (`Topmost="True"`), `:10-20` (no notification API).
- **Evidence:** `notif-dark-3line.png`, `notif-light-3line.png` — a custom `Topmost` window appearing bottom-right.
- **Category:** Conformance
- **Impact:** A clipboard manager fires on *every* copy. During a demo, a meeting, or while gaming, this toast appears regardless of the user's do-not-disturb state, and a `Topmost` layered window over an exclusive full-screen app can cause a visible flicker.
- **Recommendation:** Honour `SystemParameters` / `SHQueryUserNotificationState` (return `QUNS_RUNNING_D3D_FULL_SCREEN`/`BUSY` → suppress). One P/Invoke, no redesign.

#### F18. The notification's header is top-aligned, not baseline-aligned
- **Location:** `UI\NotificationWindow.xaml:28-33` — a `StackPanel Orientation="Horizontal"` with `Time` at `Margin="10,1,0,0"`.
- **Evidence:** `notif-light-header-4x.png`, `notif-dark-header-4x.png` — "15:16" (11 DIP) sits visibly above the baseline of "Clip" (12 DIP SemiBold).
- **Category:** Appearance
- **Impact:** The header looks hand-placed; the two words read as two objects rather than one line.
- **Recommendation:** Use a single `TextBlock` with two `Run`s (as `_fonttest` already does at its line 99-102) or set `VerticalAlignment="Bottom"` with a 2 DIP bottom margin on the time.

#### F19. The toast's shadow is clipped, leaving a hard seam
- **Location:** `UI\NotificationWindow.xaml:12-14` — `DropShadowEffect` on the root `Border`, which is the window's entire content, so the shadow is cropped to the window rectangle.
- **Evidence:** `notif-close-8x.png` (8×) — the top-right corner shows the rounded corner but the shadow ends in a straight vertical cut; `notif-light-3line.png` shows shadow below/left and none above.
- **Category:** Appearance
- **Impact:** A visible rectangular artifact at the one corner the eye lands on when a toast appears.
- **Recommendation:** Either leave 12+ DIP of transparent margin around the content (window bigger than the card) or move to a DWM shadow via `DwmExtendFrameIntoClientArea`, which is the native answer and pairs with F5.

#### F20. Settings is unreachable, and the only menu entry for it says otherwise
- **Location:** `App.xaml.cs:84-89` — `new ToolStripMenuItem("Settings") { Enabled = false }`; nothing else constructs `SettingsWindow`.
- **Evidence:** source only. The tray menu is a WinForms `ContextMenuStrip` whose icons live inside the Windows 11 XAML taskbar island; I could not enumerate it from a script without blind-clicking the taskbar, so I did not capture it (see §What I could not verify).
- **Category:** Conformance
- **Impact:** Fifteen settings rows, a theme switch and a clear-history confirmation exist in the product and no user can reach them. A greyed-out "Settings" in the primary menu actively advertises that the feature is missing.
- **Recommendation:** Wire `menu.Items.Add("Settings", … _settings.ShowDialog())` and remove `Enabled = false`. Given F1, the window must first compile.

---

### P3 — Polish

#### F21. Glyph-as-icon drift
`UI\MainWindow.xaml:46` (`&#xE721;` MDL2 magnifier), `UI\NotificationWindow.xaml:37` (`✕`), `UI\SettingsWindow.xaml:95` (`▾`), `:57` (`✓`). Evidence: `popup-dark-search-3x.png` (the magnifier's stroke is a hairline against 13 DIP text), `notif-close-8x.png`, `host-light-settings.png`. Four glyphs from two families at four optical weights. Use one icon source (Segoe Fluent Icons MDL2 at 14 DIP) or ship a tiny vector set.

#### F22. Radius and elevation are ad hoc
`14` (`MainWindow.xaml:26`, `SettingsWindow.xaml:187`), `12` (`NotificationWindow.xaml:9`), `10` (`MainWindow.xaml:11`), `8` (`MainWindow.xaml:38`, `SettingsWindow.xaml:28`), `7`/`4` in `_fonttest`. Shadow colour `#000000` is hardcoded three times outside the theme dictionaries (`MainWindow.xaml:29`, `NotificationWindow.xaml:13`, `SettingsWindow.xaml:191`). Evidence: all three window captures side by side. Fix: `RadiusShell/RadiusCard/RadiusControl` + a `Shadow` token in both dictionaries.

#### F23. Emoji and CJK fall back to unrelated faces
`popup-dark-unicode-3x.png`: 🎯 📋 render as monochrome outline symbols (WPF `TextBlock` cannot use colour emoji), and 日本語のテキスト / 中文 render in a different family with visibly different weight and x-height inside the same line. Acceptable for a utility, but it means international clips look inconsistent. Fix only if international content is expected: `Segoe UI Emoji` as an explicit fallback and a tabular alignment review.

#### F24. The toast gives no clue how long it will stay
`NotificationWindow.xaml.cs:183-191` — a 3 s timer with no progress indicator. Evidence: `notif-dark-3line.png`. A 4 px 3 s linear bar along the bottom edge would remove the "will it disappear before I read it" hesitation. Also: pressing Esc does *not* dismiss the toast (only the ✕ does), which contradicts the popup's own Esc model.

---

## Patterns & Systemic Issues

1. **Opting out of the platform, then re-implementing it thinly.** `AllowsTransparency` + `WindowStyle="None"` costs ClearType (F5), real shadows (F19), snap, and native close affordances — and buys only a rounded corner that DWM now does for free on Windows 11. This single decision explains three findings.
2. **Seven colour keys are not a token system.** There is no `AccentText`, no `BorderStrong`, no `Error`, no `Focus`, no `Shadow`, no `LineHeight`, no radius scale. Every component that needed one of those either hardcoded it (`ErrorBrush`, three `#000000`s) or borrowed a token that nearly fits (Accent as text → F2). The dark theme survives this; the light theme does not.
3. **Separation is expressed only as a 1.1:1 tonal step.** Both themes lean on `Surface` vs `App` (1.09 dark / 1.11 light) and a `Hairline` at 1.27 / 1.26. This works at #08090C where the eye exaggerates small differences, and fails at #F2F3F5. The card model needs one more step in both directions.
4. **Text is treated as a single size.** 11 / 12 / 13 / 15 with no line-height token, no weight token, and one fake italic. Four surfaces each invent their own leading (F13, F10, F18).
5. **State is never announced.** No empty states (F14), no scroll cue (F8), no placeholder (F7), no validation visibility check (F4), no "copied" confirmation on the card itself. The app is silent about what it is doing.

---

## Positive Findings

These are measured, not assumed — keep them.

1. **The bundled font really loads.** `FormattedText` for "Trailing spaces below" at 13 DIP measures **127.39 DIP** from `pack://application:,,,/Assets/Fonts/#Manrope`; the rendered string in `popup-dark-card1-body-3x.png` measures **127.3 DIP** (191 px ÷ 1.5). Exact match — there is no hidden fallback to Segoe UI. When someone says "the font is wrong", this settles it.
2. **One alignment rail genuinely holds in the popup.** Pixel scans of `popup-dark.png`: search-box outer left **x=26**, card outer left **x=27** (≤1 px), card body ink **x=57–58**, card meta ink **x=57–58**, i.e. one 16 DIP margin for the shell and one 12 DIP inset for card content, text and meta sharing the same left edge. The popup's grid is correct; the settings dialog's is not (F15).
3. **The notification's insets are clean.** `notif-light-3line.png`: the header ink and the body ink both start at **x=47** (window left edge at x=22, so 25 px = 16.7 DIP = 1 px border + 14 DIP padding + glyph side bearing), and the right edge is symmetric. One inset governs everything.
4. **Dark theme text contrast passes AA comfortably and exactly as designed.** `Fg` 18.25:1, `Muted` 5.69:1 on Surface — and the rendered meta glyph peak is *exactly* `#8A909C`, so the token reaches the screen unmodified. (The light-theme suspicion that started this audit is wrong for dark: Muted on Surface is not the failure — F3 is, and only in light.)
5. **The card clamp works.** `MaxHeight="76"` + `CharacterEllipsis` produces a clean 4-line clamp with the ellipsis on the last line (`popup-dark-filtered.png`, top card), and unbreakable tokens wrap instead of overflowing (`popup-dark-card2-body-3x.png`, no horizontal scroll anywhere).
6. **The keyboard model is complete and consistent** — Esc/Enter/↑↓/Ctrl+1..9/Del all implemented in one place (`MainWindow.xaml.cs:94-136`) and Esc was verified to close the popup (`after esc visible=0`).
7. **Theme switching is one dictionary swap** (`App.xaml.cs:129-141`) with a working `system` mode that reads the registry correctly. Both dictionaries render without a single missing `StaticResource` — no WPF resource exceptions on either theme.
8. **PerMonitorV2 + cursor-screen placement** (`app.manifest`, `MainWindow.xaml.cs:42-47`, `NotificationWindow.xaml.cs:154-172`) is the right architecture for a multi-monitor utility; the notification's `SizeToContent` + second `TopFor(ActualHeight)` pass is a correct, non-obvious fix for measuring a sized-to-content popup.

---

## What I could not verify (stated honestly)

- **Multi-monitor and mixed DPI.** Only one display exists here (1920×1200 @150 %). The placement math divides `Screen.WorkingArea` by a per-screen scale and applies `offsetX/offsetY` in **device pixels** (`NotificationWindow.xaml.cs:162-171`) while the settings label says "Offset X (px)" — that is at least self-consistent, but I could not observe a mixed-DPI setup, so I make no claim about where the toast lands there.
- **Windows text-scaling (Settings → Accessibility → Text size).** Not changed (system-wide setting, out of scope for a read-only audit). All sizes are fixed DIP, so text scaling is very likely ignored; I did not measure it.
- **The error-red contrast (F4)** is computed from tokens only — the three error blocks are collapsed and I did not force them visible. It is arithmetic, not an observation.
- **Tray context menu** — not captured. The icon sits in the Windows 11 XAML taskbar island and the accessibility tree did not expose it; I would not blind-click the notification area. F20 is therefore source-only.
- **Frame timing / jank.** I did not instrument frames. Dimension 2 is scored from render architecture (layered windows, `DropShadowEffect`), not from a frame trace.
- **Hover, pressed and focus states** were not captured except where the caret shows focus. No hover styles were exercised.
- **The `SettingsWindow` render is XAML-real but code-behind-stubbed.** The real `SettingsWindow.xaml.cs` does not compile (F1), so the host used the **real XAML** with a minimal stub that only populates the same fields. Every visual claim about Settings is about the real XAML; no behavioural claim is made.
- **NotificationWindow is not actually unwired** (the brief assumed it was): `App.OnCaptured` constructs it on every copy, which is how `notif-*` captures were obtained. Only `SettingsWindow` is unreachable.

---

## Corpus & method notes

- Screenshots were taken with `SetProcessDpiAwarenessContext(-4)` before any capture; a DPI-unaware shell sees 1280×800 and would have produced crops 1.5× too small.
- Test copies were injected with `Set-Clipboard` into the **live history database** (`%APPDATA%\Clip\clip.db`). Nine fixture rows now exist there, all labelled `WindowsTerminal` or `Clip`, all from 23 Nov 15:11–15:16. This is acceptable per the brief but the owner should know: they are real rows in the real list.
- The app was stopped twice (to change theme), and is **left running in dark theme**, tray-resident, hotkey verified working (`popup-dark-restored.png`).
- Scratch host used to render `SettingsWindow`/`NotificationWindow` from the real XAML + real theme dictionaries + real font: created under `%TEMP%\opencode\showcase`, deleted after the captures.
- Side effect to be aware of: proving F1 required running `dotnet build -c Debug` on the real csproj. That created `bin\Debug\` and regenerated `obj\Debug\` — build outputs only; **no source file was edited by this audit**.

---

## Measurements (for the next pass to verify against)

Contrast, computed from `UI\Themes\*.xaml`; `✓` = meets 4.5:1 body; `~` = 3:1 large-only; `✗` = fails.

| Pair | Dark | Light |
|---|---|---|
| Fg on App | 19.91 ✓ | 16.15 ✓ |
| Fg on Surface | 18.25 ✓ | 17.93 ✓ |
| Fg on Raised | 16.46 ✓ | 15.43 ✓ |
| Muted on App | 6.21 ✓ | 4.50 ✓ (exactly at the line) |
| Muted on Surface | 5.69 ✓ | 4.99 ✓ |
| **Muted on Raised** | 5.13 ✓ | **4.29 ✗** |
| **Accent on App** | 8.49 ✓ | **2.11 ✗** |
| **Accent on Surface** | 7.78 ✓ | **2.35 ✗** |
| **Accent on Raised** | 7.02 ✓ | **2.02 ✗** |
| Error on Surface | 6.58 ✓ | **2.78 ✗** |
| Error on Raised | 5.93 ✓ | **2.39 ✗** |
| **Tick (Surface) on Accent** | 7.78 ✓ | **2.35 ✗** (non-text, needs 3:1) |
| Hairline on Surface | 1.27 ✗ | 1.26 ✗ |
| Raised on Surface | 1.11 ✗ | 1.16 ✗ |
| Surface on App | 1.09 ✗ | 1.11 ✗ |

Geometry (device px at 150 %; 1 DIP = 1.5 px):

| Item | Measured |
|---|---|
| Popup window | 840 × 930 px = 560 × 620 DIP, centred, `Top` = centre − 40 DIP |
| Shell inset (search box & cards) | 26 / 27 px = **16.7 DIP** |
| Card content inset | 57–58 px ⇒ 12 DIP padding + 1 DIP border ✓ |
| Search box height | 55 px = 36.7 DIP (13 DIP text + 18 padding + 2 border) |
| Card gap | 10 px = 6.7 DIP (margin 6) ✓ |
| Selected-card border | 1.5 px Accent |
| Last row cut | Meta glyphs sliced at y=905–912, viewport ends 913 |
| Toast | 630 × 191 px = 420 × 127 DIP; header and body ink both start at 25 px from the window edge |
| Toast ✕ hit box | ≈ 13 × 15 DIP |
| Settings window | 660 × 1077 px = 440 × 718 DIP (MaxHeight = work area − 16) |
| Settings heading vs row label | 46 vs 58 px = **8 DIP off the rail** |
| 13 DIP body pitch | 19.0 DIP in popup vs 17.76 DIP in toast |
| Manrope advance "Trailing spaces below" @13 | 127.39 DIP; rendered 127.3 DIP ✓ |
| ClearType fringed pixels | opaque A/B: 1 026 · real popup: **0** · real toast: **0** |

---

## Recommended Actions (priority order)

1. **[P0] `/impeccable harden`** — repair the `Config.PreviewLines` mismatch so the project builds 0/0; nothing else can ship before this (F1).
2. **[P1] `/impeccable colorize`** — add `AccentText`, `Error`, `BorderStrong` and a real light step (App/Raised/Hairline); target the failing pairs in the table above (F2, F3, F4, F9).
3. **[P1] `/impeccable typeset`** — kill the fake italic, add `BodyLineHeight`/`MetaLineHeight` tokens, drop `AllowsTransparency` (or compensate weight/size), and give the toast the popup's leading (F5, F10, F13, F18).
4. **[P1] `/impeccable layout`** — put the popup list on a bottom inset with a scroll affordance, align the Settings headings to the row rail, and give `Field` an editable look (F8, F15, F16).
5. **[P1] `/impeccable harden`** — the ✕ as a real 24 DIP button with a UIA name and an Esc path; suppress the toast under Focus Assist; search field placeholder + accessible name; empty-result state (F6, F7, F14, F17).
6. **[P2] `/impeccable clarify`** — meta line content rules (no self-attribution, no tool titles), toast truncation on word boundaries (F11, F12).
7. **[P2] `/impeccable document`** — write down the token set (radii, elevation, type scale, spacing) so the next window does not invent a fourth radius (F21, F22, F24).
8. **[P1] `/impeccable polish`** — final pass over clip/seam/edge artifacts once the above land.

You can ask me to run these one at a time, all at once, or in any order you prefer.
Re-run `/impeccable audit` after fixes to see the score improve.
