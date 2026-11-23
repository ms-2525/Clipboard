# Clip — motion & animation spec

Researched 2026-11-23. This is the **implementation contract** for Clip's motion layer.
The UI sprint executes the tables below; nothing here is left to the implementer's taste.

Visual authority stays Ortu (`docs\RESEARCH-ortu-ui.md`). Where Ortu has no motion for a moment,
the Windows 11 / Fluent values fill the gap. Where the two disagree, the owner's checklist
(`docs\CHECKLIST-uiux.md` §4) and Ortu's own timings win — noted per row.

Legend for every number below:
- **[S]** = read from a primary source (linked in §1), quoted value.
- **[D]** = derived on judgment from two sourced anchors (arithmetic or interpolation shown inline).
- **[U]** = unreachable / no published value; not used.

---

## 0. What the app does today (baseline being replaced)

| Where | Current | Verdict |
|---|---|---|
| `UI\MainWindow.xaml.cs` `ShowPopup()` | `Show(); Activate(); Search.Focus();` — no animation | **GAP** — entrance is the one moment §4.1 asks for |
| `UI\MainWindow.xaml.cs` `Toggle/Hide()` | instant `Hide()` | keep instant on paste; see §3 row 4 |
| `UI\NotificationWindow.xaml.cs:181` | `Root.BeginAnimation(Opacity, new DoubleAnimation(1, 150ms))` | **MATCH** — our only animation, and it is already GPU-cheap [S] |
| `UI\NotificationWindow.xaml.cs:187` | 150 ms fade-out then `Close()` | **MATCH** [S] |
| anywhere else | nothing | hover/selection/pulse/growth are all absent |

There is no `Storyboard`, no `EasingFunction` and no reduced-motion check in the tree today —
the 150 ms notification fades are the entire motion system.

---

## 1. Sources, and what is actually verified

| Source | URL | What it establishes | Status |
|---|---|---|---|
| Windows — Motion in Windows (signature experiences) | <https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/motion> | The **animation properties table**: entrance/exit curves and the 167 / 250 / 333 ms set; "Connected / Consistent / Responsive / Delightful / Resourceful" principles | [S] fetched |
| Windows — Timing and easing | <https://learn.microsoft.com/en-us/windows/apps/design/motion/timing-and-easing> | `ControlNormalAnimationDuration = 250ms`, `ControlFastAnimationDuration = 167ms`, `ControlFasterAnimationDuration = 83ms`; `cubic-bezier(0,0,0,1)` = entering, `cubic-bezier(1,0,1,1)` = exiting | [S] fetched |
| Fluent 2 — Motion | <https://fluent2.microsoft.design/motion> | Enter/exit & elevation patterns; "keep durations short"; "avoid flashes"; **"Design for and include a 'no motion' setting … as recommended by the WCAG"**; list-item staggering guidance | [S] fetched |
| Windows — Animations overview (`PopInThemeAnimation` vs `FadeInThemeAnimation`) | <https://learn.microsoft.com/en-us/windows/apps/develop/motion/xaml-animation> | Flyout / context menu → **PopIn/PopOut** (opacity + translation); **dialog → FadeIn/FadeOut**; tooltip → fade; "if no other animation applies → fade" | [S] fetched |
| Windows — Page transitions | <https://learn.microsoft.com/en-us/windows/apps/develop/motion/page-transitions> | Page refresh = slide-up **+ fade**; `SuppressNavigationTransitionInfo` for custom work | [S] fetched |
| Ortu source — popup | `src/routes/popup/+page.svelte` @ `main` | row `transition-all duration-100`; group rows `duration-75`; **no window entrance/exit at all**; selected row re-lays out with no tween; `scrollIntoView({block:"nearest"})`; file is literally headed "Raycast-inspired compact launcher" | [S] fetched |
| Ortu source — main window | `src/routes/+page.svelte` @ `main` | card `transition-all duration-150`, hover `-translate-y-px` + shadow, press `scale-[0.995]`; copy flash = `flashingItemId` + `setTimeout(380)`; smooth scroll only in the main window | [S] fetched |
| Ortu source — toast | `src/lib/Toaster.svelte` @ `main` | `in:fly={{y:18, duration:220}}` (Svelte default `cubicOut`), `out:fade={{duration:150}}`, `animate:flip={{duration:200}}` | [S] fetched |
| Ortu source — `app.css` | `src/app.css` @ `main` | no custom `@keyframes`, no custom duration/easing tokens → Tailwind defaults apply: `cubic-bezier(0.4,0,0.2,1)` | [S] fetched |
| Maccy source — `FloatingPanel.swift` | `p0deje/Maccy` @ `master` | `animationBehavior = .none` — **the panel has no show/hide animation**; list-length resize uses `NSAnimationContext` **0.2 s** | [S] fetched |
| Maccy source — `SlideoutController.swift` | `p0deje/Maccy` @ `master` | preview slide-out: `animationDuration = 0.25`, `CAMediaTimingFunction(name: .easeInEaseOut)` | [S] fetched |
| WPF — `SystemParameters.ClientAreaAnimation` | <https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.clientareaanimation> | the reduced-motion flag WPF exposes | [S] fetched |
| WPF source — `SystemParameters.cs` | `dotnet/wpf` @ `main`, `PresentationFramework/System/Windows/SystemParameters.cs` | `ClientAreaAnimation` — "Maps to `SPI_GETCLIENTAREAANIMATION`"; `UIEffects` — "Maps to `SPI_GETUIEFFECTS`"; `StaticPropertyChanged` fires when the OS value changes | [S] fetched |
| Win32 — `SPI_GETCLIENTAREAANIMATION` | <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa> | `0x1042`, "Determines whether animations are enabled or disabled … in the client area" | [S] fetched |
| Microsoft Support — "Make it easier to focus on tasks" | <https://support.microsoft.com/en-us/accessibility/windows/make-it-easier-to-focus-on-tasks> | the user-facing switch: Settings → Accessibility → Visual effects → **Animation effects** | [S] fetched |
| WPF — Easing functions | <https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/easing-functions> | the out-of-the-box set: `BackEase`, `BounceEase`, `CircleEase`, `CubicEase`, `ElasticEase`, `ExponentialEase`, `PowerEase`, `QuadraticEase`, `QuarticEase`, `QuinticEase`, `SineEase`; `EasingMode`; custom via `EasingFunctionBase` | [S] fetched |
| WPF — Property animation techniques | <https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/property-animation-techniques-overview> | `Storyboard` (XAML, styles/templates) vs local animation via `BeginAnimation` (code, per-instance) | [S] fetched |
| WPF — Graphics rendering overview | <https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/wpf-graphics-rendering-overview> | retained-mode scene graph; a `RenderTransform`/`ScaleTransform` is applied to the visual while "the drawing instructions … remain unchanged"; layout is a separate concern from the visual layer | [S] fetched |
| FLIP (Paul Lewis) | <https://aerotwist.com/blog/flip-your-animations/> | First/Last/Invert/Play; "**there is a window of 100ms** after someone interacts … where you're able to do work without them noticing"; "animations that can be remapped to transform and opacity changes are the perfect fit" | [S] fetched |
| WCAG 2.3.3 Animation from Interactions | <https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html> | level AAA: motion triggered by interaction must be disable-able | [S] referenced (Fluent 2 points at it) |
| **Raycast** | raycast.com, manual.raycast.com, developers.raycast.com | **unreachable as a motion spec.** Closed source; the manual and API docs cover window management and extensions, not launcher chrome. No published duration, curve, or transform value exists. | [U] |
| **Alfred** | alfredapp.com | **unreachable.** Closed source, no motion documentation. | [U] |

**Raycast is covered indirectly and honestly:** Ortu's own popup file is headed
`POPUP SHELL — Raycast-inspired compact launcher`, and Ortu is the pinned visual authority.
So the Raycast *genre* conventions we adopt (instant, un-animated list; 75–150 ms in-place
transitions; selection that grows in place rather than overlaying a preview pane) are sourced
through Ortu's source, not invented. No number below is attributed to Raycast itself.

---

## 2. The token set — durations and curves

### 2.1 Durations (all sourced or derived from the table above)

| Token | Value | Source |
|---|---|---|
| `Fast` | **83 ms** | Windows `ControlFasterAnimationDuration` [S] |
| `Quick` | **100 ms** | Ortu row transition `duration-100` [S] |
| `Standard` | **167 ms** | Windows `ControlFastAnimationDuration` [S] |
| `Normal` | **250 ms** | Windows `ControlNormalAnimationDuration` [S] |
| `Enter` | **140 ms** | [D] Windows Direct Entrance is 167 ms; the popup materialises *at the cursor*, so there is no travel to show. Take Ortu's 100–150 ms ceiling minus one 60 Hz frame: 150 − 8 ≈ 140. |
| `Leave` | **83 ms** | Windows "Bare Minimum: fade-in + out, Linear, 83 ms" [S] |
| `Pulse` | **380 ms** total | Ortu `setTimeout(..., 380)` [S]; split 150 + 80 + 150 to match Ortu's 150 ms `transition-all` each way [S] |
| `Flip` | **200 ms** | Ortu `animate:flip={{duration:200}}` [S] |

Hard ceiling: **no transition exceeds 200 ms**; no repeating animation except the (out-of-scope)
capture dot. This matches Ortu's typography of speed and Windows' 167/250 ladder.

### 2.2 Curves → the exact WPF primitive

Fluent's curves are CSS cubic-béziers. **WPF has a first-class, exact equivalent:
`KeySpline` inside a key frame.** Do not approximate these with an ease class unless §5.1's
helper is unavailable — `KeySpline` is the same parametrisation (P0 = (0,0), P3 = (1,1),
control points P1/P2 given as `x1,y1,x2,y2`), so the mapping is 1:1.

| Curve | Meaning in Fluent | Exact WPF | Notes |
|---|---|---|---|
| `cubic-bezier(0,0,0,1)` | Direct Entrance / Direct Exit ("fast in, long settle") [S] | `new KeySpline(0,0,0,1)` | Decelerating. **The label "Fast – In" means "fast at the start", not `EasingMode.EaseIn`.** |
| `cubic-bezier(0.55,0.55,0,1)` | Existing elements changing position [S] | `new KeySpline(0.55,0.55,0,1)` | Only needed if you animate a move |
| `cubic-bezier(1,0,1,1)` | Gentle Exit ("accelerate away") [S] | `new KeySpline(1,0,1,1)` | Optional on dismissals |
| `cubic-bezier(0.4,0,0.2,1)` | Ortu's in-place transitions (Tailwind default) [S] | `new KeySpline(0.4,0,0.2,1)` | Used for tint/overlay opacity so hover + selection still feel like Ortu |
| `Linear` | Bare-minimum fades [S] | `new LinearDoubleKeyFrame(...)` / `DoubleAnimation` with no easing | All pure opacity fades |

If a plain `EasingFunction` class is truly required (e.g. a style trigger that cannot use key
frames), the closest approximations to `0,0,0,1`, computed against y = 3x^⅔ − 2x:

| x | `0,0,0,1` exact | `CubicEase EaseOut` | `ExponentialEase EaseOut, Exponent=6` |
|---|---|---|---|
| 0.1 | 0.446 | 0.271 | 0.452 |
| 0.5 | 0.890 | 0.875 | 0.953 |

So `CubicEase(EaseOut)` matches the middle and lags the start; `ExponentialEase(6, EaseOut)`
matches the start and overshoots the middle. **[D]** Both are acceptable stand-ins, neither is
exact — prefer `KeySpline`.

Never use `BounceEase`, `ElasticEase` or `BackEase`. Fluent's "Strong Entrance" (elastic,
3 key frames, 167/167/333 ms) exists, but it is a celebration curve for OS surfaces; Ortu has
nothing like it and §4.5 forbids scattered effects.

---

## 3. Master table

`RenderTransform` values are **DIPs**; `ScaleX/Y` are dimensionless. "Snap" = set the final
value with no timeline. Every row is GPU-safe unless marked **LAYOUT**.

| # | Element / interaction | What animates | From → To | Duration | Easing (WPF) | Trigger | Src |
|---|---|---|---|---|---|---|---|
| 1 | **Popup open**, all paths | `Shell.Opacity` | `0.70 → 1` | 140 ms | Linear | end of `ShowPopup()`, after `Show()` | [D] |
| 2 | | `Scale.ScaleX/ScaleY` (origin `0.5,0.5`) | `0.985 → 1` | 140 ms | `KeySpline(0,0,0,1)` | same | [D] from Windows 167 ms [S] |
| 3 | **Popup open, reduced motion** | — (snap) | opacity 1, scale 1 | 0 | — | same | §6 |
| 4 | **Popup close — hand-off** (`Enter`, double-click, `Ctrl+1‑9`) | *nothing* | — | 0 | — | `Paste()` before `Hide()` | [S] both Ortu & Maccy close instantly |
| 5 | **Popup close — dismissal** (`Esc`, focus lost) | `Shell.Opacity` | `1 → 0` | 83 ms | Linear | before `Hide()`; `Hide()` on `Completed` | [S] MS bare-minimum fade |
| 6 | **Notification in** | `Root.Opacity` | `0 → 1` | 150 ms | Linear | `ShowFor()`, after the second `Top` fix-up | [S] Ortu toast fade |
| 7 | | `Settle.ScaleX/ScaleY` (origin `0.5,1`) | `0.98 → 1` | 150 ms | Linear | same | [D] clip-free stand-in for Ortu's `fly y:18` [S] |
| 8 | **Notification out** | `Root.Opacity` | `1 → 0` | 150 ms | Linear | timer / ✕ → `Close()` on `Completed` | [S] Ortu `out:fade 150` |
| 9 | **Row hover in/out** | `HoverLayer.Opacity` | `0 ↔ 1` | 100 ms | `KeySpline(0.4,0,0.2,1)` | `IsMouseOver` | [S] Ortu `duration-100` |
| 10 | **Group-suggestion row hover** | same | `0 ↔ 1` | 75 ms | `KeySpline(0.4,0,0.2,1)` | `IsMouseOver` | [S] Ortu `duration-75` |
| 11 | **Selection change (keyboard)** | row overlays: `SelLayer` + `Rail` | old row `1 → 0`, new row `0 → 1` | 100 ms | `KeySpline(0.4,0,0.2,1)` | `SelectionChanged` | [S] Ortu 100 ms |
| 12 | **Selected-row growth** 1→3 lines **LAYOUT** | `BodyText.MaxHeight` | `18 → 54` | 120 ms | `KeySpline(0,0,0,1)` | `SelectionChanged` (snap when `IsRepeat`) | [D] Ortu snaps [S]; §4.5 demands a tween |
| 13 | **Copy pulse** | `PulseLayer.Opacity` (sage fill + ring) | `0 → 1` @150 ms, hold, `→ 0` @380 ms | 380 ms | Linear | after a successful copy/paste | [S] Ortu `380` + `duration-150` |
| 14 | **Dedupe jump-to-top** | row `Translate.Y` (FLIP) | `(firstY − lastY) → 0` | 200 ms | `KeySpline(0,0,0,1)` | after `_items.Move()` + `UpdateLayout()` | [D] from Ortu's flip 200 ms [S] |
| 15 | **Image-grid hover** | `Thumb.ScaleX/ScaleY` (origin `0.5,0.5`) | `1 → 1.03` | 120 ms | `KeySpline(0,0,0,1)` | `IsMouseOver` | [D]; Ortu grows thumbs in place [S] |
| 16 | | `ThumbShade.Opacity` | `0 → 1` | 100 ms | `KeySpline(0.4,0,0.2,1)` | same | [S] Ortu hover |
| 17 | **Settings window open** | `Shell.Opacity` | `0 → 1` | 150 ms | Linear | `Loaded` | [S] MS: dialogs = FadeIn, no transform |
| 18 | **Settings window close** | `Shell.Opacity` | `1 → 0` | 83 ms | Linear | `Closing` (cancel, `Completed → Close()`) | [S] MS bare-minimum fade |
| 19 | **Esc-collapse of search / clearing a filter** | *nothing* | — | 0 | — | `Esc` | [S] Ortu unwinds instantly |
| 20 | **(optional) filter breadcrumb pill leaving** | `Pill.Opacity` | `1 → 0` | 80 ms | Linear | filter cleared | [D] |

Notes that matter for rows 1–3:

- **Why scale and not `TranslateY`.** A borderless `AllowsTransparency` window has no headroom:
  the root `Border` fills the window exactly (`UI\MainWindow.xaml`, 560×620 today), so a
  `TranslateTransform` of +8 px would clip 8 px off the bottom edge (and reveal a transparent
  strip at the top) for the first frames of every open. Small *up*-scales never exceed the window
  bounds. If the sprint prefers a rise, add 8 px of transparent height to the window and
  bottom-align the shell — one line, but it touches the positioning maths in
  `MainWindow.xaml.cs:46-47`, which is the higher-risk edit.
- **`0.70` opacity start, not `0`.** This is the hotkey→visible budget rule (§4 R4). Ortu and
  Maccy both consider "first frame is final frame" the correct answer for a launcher; a full
  fade-in from 0 is a Windows *flyout* behaviour, and a flyout is summoned by a click you already
  saw land. A 550 px panel that is invisible for the first 40 ms reads as a slow app.
- **Row 4 is a real decision, not laziness.** Both reference apps hide the popup instantly on
  accept. A 83 ms exit fade over the target application is a ghost that also swallows one click.
  Dismissal paths get the fade because nothing is waiting on them.

---

## 4. Rules (invariants — violate these and the sprint fails the audit)

**R1 — Composited properties only.** Animate `UIElement.Opacity` and
`RenderTransform` (`ScaleTransform`, `TranslateTransform`, `RotateTransform`). Never animate
`Width`, `Height`, `MaxHeight`, `Margin`, `Padding`, `Top`, `Left`, or `LayoutTransform`
(`LayoutTransform` invalidates layout on every frame; `RenderTransform` does not). **One
documented exception: row 12**, because a 1→3 line growth *is* a layout change and there is no
GPU remapping that preserves the text reflow. Keep it to one property on one element of one row.

**R2 — One authored moment per surface.** Each surface family has exactly one entrance:
popup = scale + fade; notification = fade + edge settle; settings = fade only. Everything else
(rows, tabs, sections, search results, empty states, tooltips) appears with **no** entrance
animation. Ortu's popup and list ship no entrance animation at all — only its toast flies in [S] —
and adding one per row is exactly the "scattered effects" §4.5 forbids.

**R3 — Reduced motion is honoured, not approximated.** If
`SystemParameters.ClientAreaAnimation` is `false`, every duration in §3 becomes 0: set the final
values directly, and make dismissal paths call `Hide()`/`Close()` synchronously. No transform, no
fade. This is what the Windows "Animation effects" toggle promises the user [S], and Fluent 2
explicitly asks for a no-motion setting [S]. Read the flag at the moment of use (§5.1) so a live
toggle takes effect without a restart.

**R4 — Animation never blocks input or delays visibility.** Never `BeginTime` a delay; never
`await`/block on `Completed` for anything except the final `Hide()`/`Close()` of an exit; never
gate `Show()`, `Activate()`, focus, or the paste itself on an animation. The entrance's first
frame must already be ≥70 % opaque (row 1). The whole open path stays inside FLIP's 100 ms
response window [S], and the popup's own latency budget (<150 ms hotkey→visible) is measured
from `Show()`, not from the end of the animation.

**R5 — Finishing is silent.** Animations end on their final value, in place, with no overshoot,
no bounce, no loop, no stagger between rows. Cycles are capped at one pulse (380 ms); WCAG's
three-flashes-per-second rule is never approached [S].

**R6 — Two curves only, by role.** Movement and layout (`Scale`, `Translate`, `MaxHeight`) use
`KeySpline(0,0,0,1)`. In-place colour/overlay fades use `KeySpline(0.4,0,0.2,1)` (Ortu's
Tailwind default). Whole-surface opacity fades use **Linear** (Microsoft's bare-minimum fade).
Nothing else.

**R7 — Animation state is pre-declared.** Every animated `RenderTransform` is declared once in
XAML and animated by property path (`ScaleTransform.ScaleXProperty`). Never construct a
`Transform` inside an animation, and never animate a shared or frozen `Brush` — use a stacked
`Border` and animate its `Opacity` (a shared brush would animate every row at once).

**R8 — The newest animation wins.** WPF's `BeginAnimation` replaces a running clock on the same
property, so re-entrancy is safe — but exit handlers must be guarded by an explicit flag
(`_closing`, §5.3) so a fast re-open does not hide the window it just showed.

---

## 5. WPF implementation notes

### 5.1 The shared primitive — a 10-line motion helper

One static class, used by every call site. It is the only place that knows about splines or
the reduced-motion flag.

```csharp
static class Motion
{
    public static bool On => SystemParameters.ClientAreaAnimation;   // SPI_GETCLIENTAREAANIMATION

    public static DoubleAnimationUsingKeyFrames Spline(double to, double ms,
                                                       double x1, double y1, double x2, double y2)
        => new() { KeyFrames = { new SplineDoubleKeyFrame(
               to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)),
               new KeySpline(x1, y1, x2, y2)) } };

    public static DoubleAnimation Linear(double to, double ms)
        => new(to, TimeSpan.FromMilliseconds(ms));                    // no easing = linear
}
```
Both helpers animate **from the property's current base value** (no `From`), so the call-site
contract is: set the start value first, then begin the animation. A one-key-frame animation with
`KeyTime > 0` interpolates from the base value to that frame — which is exactly why every sketch
below assigns the `From` state immediately before calling `BeginAnimation`.

Wire the live toggle once, in `App` (optional — `On` is re-read at every call site, so an
in-flight app picks up a changed setting on the next interaction):

```csharp
SystemParameters.StaticPropertyChanged += (_, e) =>
    Debug.WriteLine($"{e.PropertyName} changed; motion now {Motion.On}");
```
`ClientAreaAnimation` is cached by WPF and invalidated by the OS notification, so calling
`Motion.On` at the moment of use is always current [S]. If a surface needs to react *while open*,
cache `Motion.On` in a field and refresh it from that event.

### 5.2 Popup open (rows 1–3) — `MainWindow.xaml.cs`

XAML: the root `Border` needs a declared transform and a name.

```xml
<Border x:Name="Shell" Background="{StaticResource App}" CornerRadius="14"
        RenderTransformOrigin="0.5,0.5">
  <Border.RenderTransform>
    <ScaleTransform x:Name="ShellScale"/>
  </Border.RenderTransform>
```

```csharp
void AnimateIn()
{
    if (!Motion.On) { Shell.Opacity = 1; ShellScale.ScaleX = ShellScale.ScaleY = 1; return; }
    Shell.Opacity = 0.70;
    ShellScale.ScaleX = ShellScale.ScaleY = 0.985;
    Shell.BeginAnimation(UIElement.OpacityProperty, Motion.Linear(1, 140));
    ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, Motion.Spline(1, 140, 0,0,0,1));
    ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, Motion.Spline(1, 140, 0,0,0,1));
}
```
Call it at the **end** of `ShowPopup()`, after `Search.Focus()` — visibility and focus must never
wait on it.

### 5.3 Popup close (rows 4–5) — one method, two paths

```csharp
bool _closing;

void ClosePopup(bool handoff)
{
    if (!IsVisible) return;
    if (handoff || !Motion.On) { Hide(); return; }          // paste path + reduced motion
    _closing = true;
    var fade = Motion.Linear(0, 83);
    fade.Completed += (_, __) =>
    {
        if (!_closing) return;                              // a re-open superseded us
        _closing = false;
        Hide();
        Shell.Opacity = 1;                                  // reset for the next AnimateIn
    };
    Shell.BeginAnimation(UIElement.OpacityProperty, fade);
}
```
`Paste()` keeps its current `Hide()` untouched and calls `ClosePopup(handoff: true)`; `OnDeactivated`
and `Esc` call `ClosePopup(handoff: false)`. If the user re-opens during the 83 ms fade,
`AnimateIn()`'s `BeginAnimation` replaces the clock and setting `_closing = false` at the top of
`ShowPopup()` stops the stale handler from hiding the window (R8).

### 5.4 Row hover / selection / pulse — stacked overlay `Border`s

Never animate the row's `Background` brush. Stack four transparent layers inside the row; each one
is a `Border` with `CornerRadius="12"`, `IsHitTestVisible="False"`, `Opacity="0"`:

```
Content
 └─ Grid
     ├─ HoverLayer   Background = RowHover      (#1C1E24)
     ├─ SelLayer     Background = RowSelected   (#26282D) + BorderBrush RingAccent, BorderThickness 1
     ├─ Rail         2×24 accent gradient       (Opacity 0)
     ├─ PulseLayer   Background sage@7%, Border sage@30%, BorderThickness 1
     └─ ContentPresenter
```

Hover is then a pure XAML style trigger, the cheapest place to put it — **but the `Style` must be
declared inside the `DataTemplate`**, so that `Storyboard.TargetName="HoverLayer"` resolves in the
template's namescope:

```xml
<Style x:Key="Row" TargetType="Border">
  <Style.Triggers>
    <Trigger Property="IsMouseOver" Value="True">
      <Trigger.EnterActions>
        <BeginStoryboard>
          <Storyboard>
            <DoubleAnimation Storyboard.TargetName="HoverLayer"
                             Storyboard.TargetProperty="Opacity"
                             To="1" Duration="0:0:0.100">
              <DoubleAnimation.EasingFunction>
                <CubicEase EasingMode="EaseOut"/>  <!-- closest class to 0.4,0,0.2,1 -->
              </DoubleAnimation.EasingFunction>
            </DoubleAnimation>
          </Storyboard>
        </BeginStoryboard>
      </Trigger.EnterActions>
      <Trigger.ExitActions>
        <BeginStoryboard>
          <Storyboard>
            <DoubleAnimation Storyboard.TargetName="HoverLayer"
                             Storyboard.TargetProperty="Opacity"
                             To="0" Duration="0:0:0.100"/>
          </Storyboard>
        </BeginStoryboard>
      </Trigger.ExitActions>
    </Trigger>
  </Style.Triggers>
</Style>
```
**Reduced-motion caveat:** a XAML storyboard cannot read `Motion.On`. Two acceptable answers:
(a) run hover through the same code-behind helper as selection (tidy, one pattern); or
(b) keep the storyboard and accept a 100 ms hover fade under reduced motion — hover is in-place
opacity, not motion, and does not trigger vestibular symptoms. Pick (b) only if it is written
down in the audit as a deviation; otherwise (a).

Selection and pulse go in code-behind (`History.SelectionChanged`, and the paste handler) using
`Motion.Spline` / `Motion.Linear` on the same named layers.

### 5.5 Copy pulse (row 13) — one animation, no timer

```csharp
void Pulse(Border layer)            // resolved like BodyOf() in §5.6
{
    if (!Motion.On) return;         // §6: the pulse is skipped under reduced motion
    layer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimationUsingKeyFrames {
        KeyFrames = {
            new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))),
            new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230))),
            new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380))) } });
}
```
This reproduces Ortu exactly: 150 ms in (its `transition-all`), ~80 ms visible, 150 ms out,
`setTimeout(380)` end. One `BeginAnimation`, no `DispatcherTimer`, no per-row state to clean up.

### 5.6 Selected-row growth (row 12) — the one layout animation

```csharp
TextBlock? BodyOf(object? item)          // x:Name="BodyText" lives in the DataTemplate
{
    if (History.ItemContainerGenerator.ContainerFromItem(item) is not DependencyObject c) return null;
    var cp = FindDescendant<ContentPresenter>(c);      // 5-line VisualTreeHelper walk
    return cp?.ContentTemplate?.FindName("BodyText", cp) as TextBlock;
}

void Growth()
{
    var tb = BodyOf(History.SelectedItem);
    if (tb == null) return;
    tb.TextTrimming = TextTrimming.None;               // must happen before the tween
    if (!Motion.On || _navRepeated) { tb.MaxHeight = 54; return; }   // R4: snap on key repeat
    tb.BeginAnimation(FrameworkElement.MaxHeightProperty, Motion.Spline(54, 120, 0,0,0,1));
}
```
`FindName` must be called on the **`ContentPresenter`** (the owner of the item template's
namescope) — calling it on the `ListBoxItem` returns `null`. `_navRepeated` is set from
`KeyEventArgs.IsRepeat` in the existing `OnPreviewKeyDown` and cleared on `KeyUp`. Animating
`MaxHeight` (never `Height`) lets the row's own `Border` auto-size, which is what makes the list
"breathe" — the same effect as Ortu's `line-clamp-3` switch.

Risk to watch: because this is the one layout animation, hold `ArrowDown` and confirm the popup
latency check in `CHECKLIST-uiux.md` §8 still passes. If it stutters, the documented fallback is:
snap `MaxHeight`, and cross-fade the *newly revealed* two lines in over 100 ms (opacity only).

### 5.7 FLIP for the dedupe jump (row 14)

```csharp
void MoveToTop(object item)
{
    if (History.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement c) return;
    if (c.RenderTransform is not TranslateTransform tt) return;
    double first = c.TransformToAncestor(History).Transform(new Point(0, 0)).Y;
    _items.Move(_items.IndexOf(item), 0);
    History.UpdateLayout();                                   // the FLIP "read" — inside the 100 ms window
    tt.Y = first - c.TransformToAncestor(History).Transform(new Point(0, 0)).Y;
    tt.BeginAnimation(TranslateTransform.YProperty, Motion.Spline(0, 200, 0,0,0,1));
}
```
Requires each row container to carry a declared `TranslateTransform` (R7). **The `null` guard is
the virtualization guard, not politeness:** `ListBox` virtualizes, and a container that is not
materialised cannot be measured or inverted. If the moved item's container is `null`, skip the
animation — do not force layout to conjure one. If the sprint cannot afford the container
plumbing, the honest fallback is **no animation**: a dedupe is a data refresh, not an
interaction, and both reference apps simply re-render. Say so in the audit rather than shipping a
janky FLIP.

### 5.8 Notification (rows 6–8) — edit, do not rewrite

Add to `UI\NotificationWindow.xaml` on `Root`:

```xml
<Border ... RenderTransformOrigin="0.5,1">
  <Border.RenderTransform>
    <ScaleTransform x:Name="Settle" ScaleX="0.98" ScaleY="0.98"/>
  </Border.RenderTransform>
```

In `ShowFor()` replace line 181 and add the settle:

```csharp
Root.Opacity = 0;
Root.BeginAnimation(OpacityProperty, Motion.Linear(1, 150));
if (Motion.On)
{
    Settle.BeginAnimation(ScaleTransform.ScaleXProperty, Motion.Linear(1, 150));
    Settle.BeginAnimation(ScaleTransform.ScaleYProperty, Motion.Linear(1, 150));
}
else { Settle.ScaleX = Settle.ScaleY = 1; }
```
Keep the existing `Completed → Close()` fade-out untouched (150 ms already matches Ortu [S]); the
settle is entrance-only, so `Settle` stays at 1 until the window is rebuilt. **Do not** animate `Top` for the slide: WPF moves a layered window
per frame, which is the janky path Ortu's research already flagged. If a true 18 px slide is
wanted later, give the window 18 px of headroom and translate the inner `Grid` — not `Top`.

`ScaleX`/`ScaleY` from `0.98` anchored at the bottom edge is clip-free by construction (it only
grows toward its final 1.0).

### 5.9 Settings (rows 17–18)

`Root` opacity only, `Loaded` → `Motion.Linear(1, 150)`, `Closing` → animate to 0 (83 ms),
`e.Cancel = true`, `Completed → Close()`. A `FadeInThemeAnimation` equivalent with **no**
transform: Microsoft's own table sends dialogs to fade, flyouts to pop [S]. That difference is
also what keeps R2 satisfied — settings must not feel like the launcher.

### 5.10 Selection-follow-scroll (from the Raycast/Ortu genre)

Do **not** animate scrolling in the popup. Ortu uses `scrollIntoView({block:"nearest"})`
(instant) for arrow keys in the popup, and `behavior:"smooth"` only in the main window [S].
Keep `History.ScrollIntoView(item)` as-is in the popup — a smooth scroll plus a growing row is two
motions fighting during keyboard navigation. In the main window, `ScrollViewer`'s default smooth
scroll is acceptable because selection there is click-driven.

---

## 6. Reduced motion — exact behaviour

| Surface | Animations on | `ClientAreaAnimation == false` |
|---|---|---|
| Popup open | 140 ms scale+fade (rows 1–2) | snap: opacity 1, scale 1, same frame as `Show()` |
| Popup close (dismissal) | 83 ms fade (row 5) | `Hide()` immediately |
| Popup close (handoff) | instant | instant (unchanged) |
| Notification in/out | 150 ms fade + settle | opacity 1 on show; `Close()` immediately on dismiss |
| Hover / selection | 75–100 ms | durations 0 — snap to the final tint |
| Copy pulse | 380 ms | skipped (a static tint would read as a state, not a confirmation) |
| Row growth | 120 ms | snap to 54 |
| FLIP | 200 ms | no animation, straight reorder |
| Settings | 150/83 ms fade | snap |

Detection is `SystemParameters.ClientAreaAnimation` (→ `SPI_GETCLIENTAREAANIMATION`) [S]. The
settings path is Settings → Accessibility → Visual effects → Animation effects [S].
`SystemParameters.UIEffects` (`SPI_GETUIEFFECTS`) is a *different* switch ("animate controls and
elements inside windows" in Performance Options) — do not AND them together; `ClientAreaAnimation`
is the one the accessibility toggle writes and the one Fluent asks apps to respect.

There is **no** automatic suppression of app-authored `Storyboard`s in WPF; the check is ours.

---

## 7. ADOPT list → `CHECKLIST-uiux.md` §4

| §4 item | Verdict | What ships |
|---|---|---|
| 4.1 Popup entrance/exit: one authored moment | **MUST** | rows 1–5. One moment = the 140 ms scale+fade entrance. Exit is an 83 ms fade on dismissal and 0 ms on paste. |
| 4.2 Notification fade ~150 ms, never steals focus, never sounds | **MUST** | rows 6–8 (fade kept at 150 ms; added clip-free settle). Focus/sound behaviour untouched. |
| 4.3 Hover transitions fast, subtle, GPU-cheap | **MUST** | rows 9–10 — overlay `Opacity` only, 100/75 ms. |
| 4.4 Selected-row growth animates, not snaps | **MUST** | row 12, with the snap-on-key-repeat guard and the documented fallback. |
| 4.5 No scattered effects; no identical entrance on every surface; reduced motion respected | **MUST** | R2 (popup = scale, toast = settle, settings = fade, rows = none), R3, §6 table. |
| 4.6 Copy pulse ~380 ms, sage/green, visible not distracting | **MUST** | row 13, exactly Ortu's 150/80/150 split. |
| Dedupe jump-to-top feels natural | **SHOULD** | row 14 (FLIP 200 ms) with the "ship nothing rather than jank" escape hatch. |
| Image-grid hover | **SHOULD** | rows 15–16. |
| Settings window open | **SHOULD** | rows 17–18. |
| Esc-collapse of search | **MUST (do nothing)** | row 19 — and write "deliberately instant" in the audit so it isn't read as an omission. |
| Breadcrumb pill fade | **NICE** | row 20, only if the filter pill ships. |
| Capture dot ping | **OUT OF SCOPE** | Ortu's `animate-ping` loop; belongs to the capture-pause sprint, and it is the one looping animation in the app. |

---

## 8. Decisions that are derived, not sourced (and their risk)

1. **140 ms entrance** — between Windows' 167 ms and Ortu's 150 ms ceiling [D]. Risk: on a cold
   first open the window can paint late; gate it with the popup-latency item in
   `CHECKLIST-uiux.md` §8, not a stopwatch.
2. **`0.70` starting opacity** — judgement, chosen so the first frame is unmistakably "there" [D].
   Risk: too high a start looks like a colour pop; too low breaks the 150 ms budget. 0.70 sits
   inside the useful range (0.5–0.85).
3. **Scale instead of `TranslateY`** — forced by the window's lack of headroom (§3 note). If the
   popup is later given a transparent gutter, a 6 px rise is a one-line swap [D].
4. **Row growth tween (120 ms)** — an *owner decision over Ortu*, which snaps [S]. This is the
   only layout animation in the app and the only place R1 is bent [D].
5. **Sage pulse on the row rather than Ortu's green card** — Ortu puts the pulse on a bordered
   card in the main window; our popup rows are flat, so the pulse rides a 1 px inset ring instead
   [D]. Colour stays sage/green per §4.6.
6. **Notification settle-scale instead of Ortu's 18 px fly** — a layered `SizeToContent` window
   has no room to translate [D]. Close enough at 0.98.
7. **83 ms popup exit** — Microsoft's bare-minimum fade number used for a dismissal rather than a
   167 ms Direct Exit [D]; a dismissal with nothing waiting should not spend 167 ms.
8. **Raycast's own values are absent** — indicated as [U] everywhere; do not let a later reader
   cite this document as Raycast-sourced.

---

## 9. Not verified / limits

- No Windows 11 flyout was frame-stepped to confirm the 167/250/333 ms table against the shipping
  OS; the table is quoted from Microsoft's docs, not measured here.
- Ortu has no entrance animation, so the popup entrance is a Windows-heritage addition, not a
  clone. It cannot be "verified against Ortu" — only against the Fluent table.
- Maccy's `animationBehavior = .none` was confirmed in source; its list *resize* (0.2 s) is the
  only window-level animation it has.
- WPF layout-cost for row 12 (≤ 12 visible rows) was reasoned, not profiled. The audit's popup
  latency measurement is the gate.
- The claim that the Settings → Accessibility → Visual effects → *Animation effects* toggle writes
  `SPI_SETCLIENTAREAANIMATION` was **not** verified by flipping the switch and re-reading the flag
  on a live machine. It is inferred from the SPI's documented meaning ("animations … in the client
  area") and WPF's 1:1 exposure of it. Worth a 30-second check during the sprint: toggle it and
  assert `SystemParameters.ClientAreaAnimation` flips.
- `KeySpline`/`SplineDoubleKeyFrame` is standard WPF; the 1:1 equivalence to CSS `cubic-bezier`
  follows from the shared definition (P0=(0,0), P3=(1,1), x = time), not from a Microsoft
  statement that the two are interchangeable.
- The Ortu popup's hover-preview panel (300×220, offset 14 px, appears on `mouseenter`) has no
  transition in source — it appears/disappears instantly. If we build it, it must stay instant to
  match, or get an 83 ms fade that is *not* in this table.
