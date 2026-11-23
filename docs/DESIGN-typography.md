# DESIGN — Typography (Clip)

**Status:** implementable spec. Every value below is a decision; nothing is left to taste.
**Researched:** 23 Nov 2026. **Owner-facing summary:** §1. **Verdict table:** §11.
**Supersedes on conflict:** the 9/10 px values copied from Ortu (reason in §2.5); everything else agrees with `docs/RESEARCH-ortu-ui.md` §7 and `docs/CHECKLIST-uiux.md` §3.

---

## 1. The five changes that fix "the text looks bad"

1. **The app is rendering Manrope ExtraLight (200), not Regular.** The shipped `Assets/Fonts/Manrope.ttf` is a variable font whose *default instance* is weight 200, and WPF cannot drive variable-font axes. Every `FontWeight="SemiBold"` / `"Bold"` in the XAML is currently dead code. §2 has the proof and the fix (ship three static faces).
2. **Kill the italic meta line.** It is a *synthetic skew* of the above ExtraLight — the single worst-case glyph combination available on Windows: thinnest weight + fake oblique + 11 px + grey on dark. §6.
3. **Body text stops being pure white.** `Fg #FFFFFF` on every row is the loudest "AI dashboard" tell in the app. Compact rows go to `#959698`, the selected row to `#C4C5C6`, and hierarchy comes from colour + weight, never from another font.
4. **One type scale with three core sizes:** 13 px body, 11 px secondary, 10 px uppercase micro-label, plus exactly two exceptions (14 px search, 12 px field/caption). No 12.5 / 15 px sprinkling.
5. **Small text stops being sub-pixel mush.** `TextFormattingMode="Display"` + `UseLayoutRounding` at window level, explicit `LineHeight`, and no sizes below 10 px (which also removes the `✓ ✕ ▾` characters that Manrope does not contain and Windows was rendering in a fallback font at the wrong weight). §8.

---

## 2. What the shipped font actually is (measured, not assumed)

I parsed `Assets/Fonts/Manrope.ttf` directly (table dump, 164,700 bytes, 19 tables). These are facts about *this file*, not about Manrope in general:

| Table | Finding |
|---|---|
| `fvar` | 1 axis: `wght`, **min 200, default 200, max 800**; 7 named instances (200/300/400/500/600/700/800) |
| `name` ID 1 / 4 / 6 | `Manrope ExtraLight` / `Manrope ExtraLight` / `Manrope-ExtraLight` |
| `name` ID 16 / 17 / 25 | `Manrope` / `ExtraLight` / `Manrope` |
| `OS/2` | `usWeightClass = 200`; `fsSelection = 192` (REGULAR + USE_TYPO_METRICS); x-height **1080**/2000 em (0.540), cap 1440 (0.720), typo ascender 2132 (1.066), descender −600 (0.300), lineGap 0 |
| `gvar`, `HVAR`, `STAT` | present → genuinely a variable font, not a mislabelled static |
| `GSUB` features | includes `tnum` (tabular figures exist), `liga`, `kern` |
| cmap | `·` `…` `—` `→` `↩` `×` `•` `“ ” ‘ ’` present; **`✓` U+2713, `✕` U+2715, `▾` U+25BE absent** |

Independent confirmation that this is Google's file as-shipped, not a renamed one — Google Fonts metadata for Manrope lists the variable file's `post_script_name: Manrope-ExtraLight`, `full_name: Manrope ExtraLight`, axis 200–800 ([METADATA.pb](https://github.com/google/fonts/blob/main/ofl/manrope/METADATA.pb)).

### 2.1 What WPF does with it

- **WPF has no variable-font support.** The tracking issue is still open: "because of WPF's lack of support for variable fonts, we cannot take advantage of such technology" ([dotnet/wpf#7758](https://github.com/dotnet/wpf/issues/7758), opened 2023, still open in 2026). WPF also does not drive Segoe UI Variable's optical axis — it only knows the three *named cuts* ([MahApps typography docs](https://mahapps.github.io/mahapps.com/docs/styles/typography)). A pack-URI font therefore renders at its **default instance**: here, ExtraLight 200.
- **`FontWeight` cannot switch to a weight the family does not contain.** WPF selects the closest face by `OS/2 usWeightClass` ([WPF font selection model](https://assets.pdfsharp.net/docs/WPF%20Font%20Selection%20Model.pdf), §2–4). With one face at 200, every requested weight resolves to it.
- **WPF adds a *simulated bold* face only when the family's heaviest weight is between 350 and 550** ("If the heaviest weight is at least 350, and no more than 550, add a new face … with a weight of Bold (700)", [WPF font selection model](https://assets.pdfsharp.net/docs/WPF%20Font%20Selection%20Model.pdf); confirmed in the reference source `FontDifferentiator.cs`, "Step 2: Add simulated bold faces"). **200 is outside that window, so there is no faux bold either.**
- **WPF *does* add a simulated oblique face**, so `FontStyle="Italic"` renders as a geometric skew of the 200-weight glyphs (same document, "Adding simulated oblique faces"; `FontDifferentiator.cs` Step 3).

**Consequence:** the popup, notification and settings are, as written today, one weight of ExtraLight with fake italics. This matches the owner's complaint exactly and no colour tweak will fix it.

### 2.2 The fix — ship static faces (smallest complete change)

The existing URI is already the *folder* form — `pack://application:,,,/Assets/Fonts/#Manrope` — which WPF resolves by enumerating every font file in that folder and merging faces that share the family name ([Packaging Fonts with Applications](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/packaging-fonts-with-applications); worked example for multi-file families: [SO 34903445](https://stackoverflow.com/questions/34903445/wpf-multiple-font-files-of-the-same-family)). So **no XAML URI change is needed** — only the files:

1. Get static instances of Manrope at 400 / 500 / 600 (Google Fonts family download ships a `static/` folder; or instance the shipped file):
   `fonttools varLib.instancer Manrope.ttf wght=400 -o Manrope-Regular.ttf` (same for 500 → `Manrope-Medium.ttf`, 600 → `Manrope-SemiBold.ttf`; use `--update-name-table` so subfamily names become Regular/Medium/SemiBold).
2. Delete `Assets/Fonts/Manrope.ttf` (the 200 default) so no stray ExtraLight face competes for `FontWeight=Normal`.
3. `Clip.csproj`:
   ```xml
   <ItemGroup>
     <Resource Include="Assets\Fonts\Manrope-Regular.ttf" />
     <Resource Include="Assets\Fonts\Manrope-Medium.ttf" />
     <Resource Include="Assets\Fonts\Manrope-SemiBold.ttf" />
   </ItemGroup>
   ```
4. Keep the theme resource exactly as it is: `<FontFamily x:Key="UiFont">pack://application:,,,/Assets/Fonts/#Manrope</FontFamily>`.
5. Gate: each file must report `usWeightClass` 400 / 500 / 600 and typographic family `Manrope` (the parser in §2 can be re-run; 30 seconds).

Weights shipped: **400, 500, 600 only.** 700 is not part of the Windows design language ("Bold is not part of the Windows 11 design language", [Microsoft Fluent/Reactor guidance](https://github.com/microsoft/microsoft-ui-reactor/blob/main/skills/design-docs/typography-and-colors.md)) and nothing in this spec uses it.

Fallback if statics cannot be produced: switch `UiFont` to `Segoe UI` and keep §3 unchanged. Do **not** ship the variable file hoping WPF finds the other instances.

### 2.3 Why 9–10 px from Ortu cannot be copied literally

Ortu is a macOS Tauri app; its sizes are CSS px on a Retina display, i.e. **2× the device pixels** (its own screenshots are Retina). Ortu's 10 px meta = 20 device px. On a 100 %-DPI Windows machine the same declaration is 10 device px — *half the physical size*. That is why the meta line reads as a grey smudge here while it looks deliberate in Ortu. §3 therefore keeps Ortu's *ratios* but raises every sub-12 px value by one step for Windows 1× (derived, not a quote).

### 2.4 Small-size legibility, in numbers

- Microsoft's own floor: "**Minimum values: 14 px Semibold, 12 px Regular** — text smaller than these sizes and weights [is] illegible in some languages" ([Typography in Windows 11](https://learn.microsoft.com/en-us/windows/apps/design/style/typography)). Caption (the smallest ramp step) is **12/16 epx** ([Windows type ramp](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography)).
- APCA: contrast sensitivity falls sharply as text gets smaller and thinner; Lc 90 is "recommended minimum for extremely thin fonts with a minimum of 24 px at weight 200", and Lc 60 (the floor for content text) starts at **16 px/700 or 18 px/600** ([APCA in a Nutshell](https://git.apcacontrast.com/documentation/APCA_in_a_Nutshell.html), [contrast sensitivity curve](https://www.readtech.org/ARC/tests/visual-readability-contrast)). 13 px ExtraLight is off that table entirely.
- Light-on-dark blooms (halation), and the bloom scales with stroke weight/contrast, so a 12 px caption that was merely small in light mode becomes "small and blurry" on dark; the standard mitigations are raising the smallest step to 13–14 px and dropping a weight step below 14 px ([ACM CHI 2023, font grade in light/dark polarity](https://dl.acm.org/doi/fullHtml/10.1145/3544548.3581552); practical summary: [artofstyleframe](https://artofstyleframe.com/blog/typography-for-dark-mode-ui)).
- WPF's own anti-aliasing makes it worse: with `AllowsTransparency=True` and a `DropShadowEffect` on the root border, children are rendered into a semi-transparent intermediate bitmap, so WPF "disables ClearType when it detects that the buffer into which text is drawn could have a transparent background" ([RenderOptions.ClearTypeHint](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.cleartypehint)); ClearType is lost to per-pixel-opacity windows and to bitmap effects ([Eli Arbel's list](https://arbel.net/2007/02/03/give-me-back-my-cleartype)). Every glyph in this app is grayscale-antialiased. Thin + grey + grayscale AA at 11 px is the failure mode, not the fix.

### 2.5 The one rule that justifies the deviations

> **10 px is allowed only for uppercase, ≤3-word micro-labels at ≥4.5:1. Anything the user must read in a sentence is ≥11 px; body text is 13 px.**

This preserves Ortu's "two registries" look (13 px body, tiny metallic label) while putting a floor under prose. The two pinned checklist values it supersedes are listed in §10.

---

## 3. The type scale

Sizes and line heights are WPF DIPs (`FontSize`, `LineHeight`). At 100 % DPI 1 DIP = 1 device pixel; at 150 % everything scales together, so the *ratios* are what matter.

Manrope's own metrics from §2 give a natural line box of 1.366 em (13 px → 17.76 px), so 18 px is a hair-loosened but honest 13 px leading, and 16 px is the tight-but-safe 11 px leading. Its x-height is 0.540 em — high for a geometric sans — which is why 13 px Manrope *looks* as large as 14 px Segoe; do not upsize the scale to compensate.

| Token | Size / LineHeight | Weight | Tracking | Dark colour | Light colour | Used for |
|---|---|---|---|---|---|---|
| `Title` | 13 / 18 | 600 | 0 | `Fg` #FFFFFF | `#15171C` | settings window title, notification app name, active tab |
| `Body` | 13 / 18 | 400 | 0 | `Fg55` #959698 / `Fg75` #C4C5C6 selected | `#5F6673` / `#4A515B` selected | clip text, rich-preview base, tab labels |
| `BodyStrong` | 13 / 18 | 600 | 0 | `Fg` | `#15171C` | buttons, active tab, confirm labels |
| `Search` | 14 / 20 | 400 | 0 | `Fg75` #C4C5C6 | `#2E343C` | the search field (Ortu's 14 px) |
| `Meta` | 11 / 16 | 500 (time segment 400) | 0 | `Fg55` #959698 | `#5F6673` | card meta line, notification timestamp |
| `Hint` | 11 / 16 | 400 | 0 | `Fg55` #959698 | `#5F6673` | settings descriptions, status-bar hints, empty-state sub |
| `Label` | 10 / 14 | 600 | **+80** (0.08 em) | `Fg48` #848588 | `#666E7B` | section headers, settings section labels, chips — **uppercase in the string** |
| `Field` | 12 / 16 | 500 | 0 | `Fg` | `#15171C` | settings value boxes |
| `Caption` | 12 / 16 | 400 | 0 | `Fg48` #848588 | `#666E7B` | combo items, secondary line under an empty state |
| `EmptyTitle` | 14 / 20 | 500 | 0 | `Fg55` | `#5F6673` | "Nothing here yet" / "No results" |
| `Keycap` | 10 / 12 | 600 | 0 | `Fg70` #B8B9BB | `#3E444D` | keycap glyph — **Consolas**, not Manrope |
| `Placeholder` | 14 / 20 | 400 | 0 | `Fg40` #70737A | `#6B7280` | search placeholder only |

Measured WCAG contrast for the dark pairings (composited, against `Surface #13151B` / selected `#26282D`):

| Colour | on Surface | on selected row | verdict |
|---|---|---|---|
| `Fg55` #959698 | 6.16 | 4.98 | passes 4.5 everywhere, including inside the selected card |
| `Fg48` #848588 | 4.95 | 4.00 | labels only — never place `Label` on a selected row |
| `Fg40` #70737A | 3.84 | — | placeholder only, on the `App` band (4.19) |
| `Fg35` #66676B | 3.23 | 2.61 | **non-text only** (separators, icon strokes) |
| `Muted` #8A909C (today's meta) | 5.69 | 4.60 | contrast was never the problem — the *stroke* was |

Light equivalents: `#5F6673` = 5.78 / 4.97 / 5.20 on `#FFFFFF` / `#ECEEF1` / `#F2F3F5`.

New brushes to add to **both** `UI/Themes/Dark.xaml` and `Light.xaml` (values above), replacing per-element `Muted` guesses:

```xml
<SolidColorBrush x:Key="Fg75" Color="#C4C5C6"/>  <!-- light: #4A515B -->
<SolidColorBrush x:Key="Fg55" Color="#959698"/>  <!-- light: #5F6673 -->
<SolidColorBrush x:Key="Fg48" Color="#848588"/>  <!-- light: #666E7B -->
<SolidColorBrush x:Key="Fg40" Color="#70737A"/>  <!-- light: #6B7280 -->
<SolidColorBrush x:Key="Fg35" Color="#66676B"/>  <!-- light: #8A9099  (non-text only) -->
<SolidColorBrush x:Key="Divider" Color="#212329"/> <!-- light: #E2E5EA (Hairline is the border) -->
```

`Muted` stays as-is so existing non-typographic uses do not break, but **no text uses it after this spec.**

---

## 4. Window-level setup (do this once per window)

```xml
<Window ... FontFamily="{StaticResource UiFont}"
        FontSize="13"
        UseLayoutRounding="True"
        SnapsToDevicePixels="True"
        TextOptions.TextFormattingMode="Display"
        TextOptions.TextRenderingMode="Grayscale"
        TextOptions.TextHintingMode="Auto">
```

- `TextFormattingMode="Display"`: GDI-compatible metrics, every glyph on whole pixels — the documented answer for small WPF text ("switching to display mode should solve these problems", [WPF 4 text stack](https://learn.microsoft.com/en-us/archive/blogs/text/wpf-4-0-text-stack-improvements); practical comparison: [roland-weigelt](https://www.roland-weigelt.de/blog/rweigelt/wpf-text-rendering-and-the-blues.html)). Above ~15 pt Ideal is better; everything here is ≤14 px, so Display everywhere.
- `TextRenderingMode="Grayscale"`: state the truth. ClearType is already impossible in these windows (§2.4), and forcing it produces colour fringes at the rounded corners. Do not add `RenderOptions.ClearTypeHint="Enabled"` — it would fight the root `DropShadowEffect`.
- `FontFamily` on the **Window** (a `Control` property), not `TextElement.FontFamily` on a Grid. Both derive from the same attached property, but the Window form is the pattern that reliably reaches `TextBox`, `ComboBox` and popups created inside the window.
- `ToolTip` is not in the window's visual tree; add one implicit style to the theme so tooltips do not revert to Segoe UI 12 px:
  ```xml
  <Style TargetType="ToolTip">
    <Setter Property="FontFamily" Value="{StaticResource UiFont}"/>
    <Setter Property="FontSize" Value="11"/>
    <Setter Property="LineHeight" Value="16"/>
  </Style>
  ```
- Remove `TextElement.FontFamily` from the `Grid`s in `MainWindow.xaml:32`, `NotificationWindow.xaml:16`, `SettingsWindow.xaml:194`.

---

## 5. Surface-by-surface property spec

### 5.1 Popup — search band

```xml
<TextBox x:Name="Search" FontSize="14" LineHeight="20" FontWeight="Normal"
         Foreground="{StaticResource Fg75}" CaretBrush="{StaticResource Accent}"
         Background="Transparent" BorderThickness="0" Padding="0"
         VerticalContentAlignment="Center" TextOptions.TextFormattingMode="Display"/>
<!-- overlay when Search.Text is empty -->
<TextBlock Text="Search history, groups…" FontSize="14" LineHeight="20"
           Foreground="{StaticResource Fg40}" IsHitTestVisible="False"
           VerticalAlignment="Center"/>
```
Band: `MinHeight 48`, padding `0,14`, icon 14 px at `Fg40`. Typed text at `Fg75` (#C4C5C6) so the query is the brightest thing in the header without being pure white.

### 5.2 Popup — clip row body

```xml
<TextBlock Text="{Binding Text}" TextWrapping="Wrap"
           TextTrimming="CharacterEllipsis"
           FontSize="13" LineHeight="18" LineStackingStrategy="MaxHeight"
           FontWeight="Normal"
           Foreground="{StaticResource Fg55}"
           Height="18"/>   <!-- compact; selected: Height = PreviewLines * 18 -->
```
Selected-state overrides (in the existing `DataTrigger`): `Foreground` → `Fg75`, `Height` → `54` (3 × 18, or `PreviewLines × 18`).

Row container: `Border Background="Transparent" CornerRadius="12" Padding="12,8"`, no border, no margin. Compact row = **34 px**; selected row = **102 px** (18 → 54 body + 8 gap + 1 hairline + 7 pad + 16 meta + 16 padding).

### 5.3 Popup — meta line (the footer)

Shown **on the selected card only** (see §6.4 for why, and §6.5 for the exact alternate if the owner wants it on every row).

```xml
<StackPanel Margin="0,8,0,0">
  <Border BorderThickness="0,1,0,0" BorderBrush="{StaticResource Divider}"
          Margin="0,0,0,7"/>
  <TextBlock FontSize="11" LineHeight="16" TextTrimming="CharacterEllipsis"
             Typography.NumeralAlignment="Tabular">
    <Run Text="{Binding MetaApp}" FontWeight="Medium" Foreground="{StaticResource Fg55}"/>
    <Run Text="  ·  " FontWeight="Normal" Foreground="{StaticResource Fg35}"/>
    <Run Text="{Binding MetaTime}" FontWeight="Normal" Foreground="{StaticResource Fg55}"/>
  </TextBlock>
</StackPanel>
```
- No `FontStyle`. `Run` is acceptable here (two style changes in one line that must ellipsize as a unit); Microsoft's "avoid Runs" advice is a 1000-item micro-optimisation, not a 12-row list concern ([Optimizing performance: text](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-text)).
- `Typography.NumeralAlignment="Tabular"` works because Manrope carries a `tnum` feature (§2). Verify in the first build; if digits do not snap, delete the attribute rather than fake it.
- Format stays **absolute time in the configured 12h/24h** — the owner pinned that over Ortu's relative time (`CHECKLIST-uiux.md` line 19). Only the *typography* changes here.

### 5.4 Popup — tabs (S3, not yet built)

```
height 34, padding 0 12, gap 18, bottom border 1 px Divider
label: 13/18, 500, Fg55            active: 13/18, 600, Fg
count: 11/16, 400, Fg48, tabular, 6 px left of the label end ("Pinned · 4")
active underline: 2 px Accent, full label width, sits on the divider
```
No pills, no icons, no uppercase — the checklist pins "flat text + 2 px accent underline".

### 5.5 Popup — status bar

```
height 30, padding 0 16, top border 1 px Divider
left  "{n} clips"      11/16, 500, Fg48, tabular
right "↵ paste"/"esc hide"  11/16, 400, Fg55, gap 12 between pairs
keycap: Border radius 6, bg #26282D, border #3B3D40, padding 4,1;
        glyph 10/12, 600, Consolas, Fg70
```
Ortu uses 9 px here; at Windows 1× that is a 9-device-pixel cheat sheet the user is supposed to read at a glance, so it goes to 11 px (rule §2.5).

### 5.6 Notification window

```
root Raised #1C1F28, radius 12, padding 14,10
app name   11/16, 600, Accent        (owner pin: app name in accent)
"·"/gap    8 px spacing, no separator run needed
timestamp  11/16, 400, Fg55, tabular, no italic
close      12 px Segoe MDL2 Assets glyph \uE711, Fg55 → Accent on hover
body       13/18, 400, Fg75, TextWrapping=Wrap, TextTrimming=CharacterEllipsis,
           Height = clamp(MaxLines,1,10) * 18 (set in code-behind from config)
```
Baseline trick: the app name and timestamp share the same 16 px line box, so they sit on one baseline without margins. Delete `Margin="10,1,0,0"` from the timestamp — it exists only to fake alignment under the old mismatched sizes.

### 5.7 Settings window

| Element | Spec |
|---|---|
| window title "Settings" | `Title` 13/18 600, margin `2,0,2,8` (was 15 Bold) |
| section label `HOTKEY` | `Label` 10/14 600, `CharacterSpacing="80"`, uppercase in the XAML string, `Fg48`, margin `2,10,0,6` |
| row title | 13/18 500, `Fg75`, vertically centred |
| row description | `Hint` 11/16 400 `Fg55`, `TextWrapping=Wrap`, `MaxWidth` of the row |
| value field | `Field` 12/16 500 `Fg`, right-aligned, `Padding="0"`, tabular for numbers |
| hint under a section | `Hint` 11/16 400 `Fg48` |
| inline error | 11/16 400 `ErrorBrush #FF6B6B` |
| buttons | 12/16 600; ghost = 400 `Fg55` → `Fg` on hover |
| checkbox label | 12/16 400 `Fg75`; tick glyph `\uE73E` (MDL2) 12 px, **not** `✓` |
| combo item | `Caption` 12/16 400, padding 8,6; chevron `\uE70D` (MDL2) 10 px `Fg48`, **not** `▾` |
| segmented pill (theme/time) | 11/16 500; active black on `Accent`; inactive `Fg55` |
| footer hint | `Hint` 11/16 400 `Fg48`; buttons right |

`CharacterSpacing` is in 1/1000 em, so `0.08em → 80` (the research doc's mapping for Ortu's 0.1/0.12 em values is correct; 80 replaces them per rule §2.5 at 1×).

### 5.8 Empty states and rich preview

- Empty title `EmptyTitle` 14/20 500 `Fg55`; sub `Hint` 11/16 `Fg48`; hint rows `Hint` with `Keycap`.
- **Rich preview is the one place our type system leaks.** `HtmlRenderer.WPF` renders clip HTML with its own CSS and cannot resolve a pack-URI family, so an unstyled clip shows up as Times New Roman 16 px — the most conspicuous "sloppy" frame on screen. Spec: set the panel's base stylesheet to

  ```css
  body { font-family: "Segoe UI", sans-serif; font-size: 13px; line-height: 1.385;
         color: #C4C5C6; margin: 0; }
  a { color: #FF8A3D; }
  code, pre { font-family: Consolas, monospace; font-size: 12px; }
  ```

  Segoe UI (not Manrope) is deliberate: the renderer resolves fonts by name from the system collection, and Segoe UI at 13 px is the closest metric neighbour that will actually exist. Mark as derived; verify the first rich clip renders at 13 px and not 16 px serif.

---

## 6. The meta line: decision, evidence, replacement

### 6.1 Decision — **kill the italic**

Remove `FontStyle="Italic"` from `MainWindow.xaml:81` and `NotificationWindow.xaml:32`. Do not replace it with anything slanted.

### 6.2 Evidence

1. **It is not an italic.** The font has no italic face; WPF synthesises an oblique by skewing the upright glyphs (§2.1). A synthetic skew of a 200-weight stroke is a smear, not a style.
2. **Microsoft excludes italic from the Windows type system on purpose:** "Bold and Italic styles are not part of the Windows type ramp. Use Semibold instead of Bold for emphasis. Italic is excluded because it can reduce readability and legibility, particularly for people with dyslexia" ([Typography in Windows 11](https://learn.microsoft.com/en-us/windows/apps/design/style/typography)).
3. **The visual authority never italicises metadata:** "Meta labels are uppercase 9/10 px semibold with 0.1–0.12em tracking, **never italic**" (`RESEARCH-ortu-ui.md` §7) — confirmed against Ortu's own markup, where the meta row is `text-[10px] text-fg/32` with no `italic` class (`src/routes/+page.svelte`).
4. **The genre does not italicise metadata either.** Maccy renders secondary information with the system `Color.secondary`, no slant (`Maccy/Views/ListItemView.swift`); Raycast's own token set uses weight + colour only (derived from its published DESIGN tokens — no official guidance exists, so treat Raycast's numbers as indicative, not normative).

### 6.3 Replacement (exact)

`11 px / 16 px line, weight 500 for the app name, 400 for the timestamp, colour Fg55 (#959698), tabular figures, separators at Fg35, one line, ellipsis at the end, above a 1 px `Divider` hairline, 8 px above / 7 px below.` See §5.3 for the markup.

Why 11 and not the pinned 10: 10 px at 100 % DPI is 10 device pixels with Manrope's 0.540 em x-height → a 5.4 px x-height and a ~1.1 px 500-weight stroke, rendered with grayscale AA and no hinting. That is the smudge the owner is complaining about. 11 px is still visibly subordinate to 13 px body (and the hairline + colour + weight carry the hierarchy), and it satisfies the ≥4.5:1 rule on the selected background (4.98), which 10 px does not buy you any more of.

### 6.4 Why the meta is selected-only

Three-segment provenance on every row is the "AI dashboard" tell; every launcher in the genre puts one line of content per row and keeps metadata in the expanded/selected surface (Ortu popup: no timestamps at all, "No card borders, no shadows, no timestamps in the popup", `RESEARCH-ortu-ui.md` §3; Maccy: single-line rows, middle-truncated, no metadata; Raycast: one accessory, not a meta paragraph). It also buys density: 34 px compact rows put 12 clips in the 410 px list viewport instead of 7, and it makes the selected-card growth the moment the metadata appears — the Ortu behaviour the checklist pins.

Consequence for the designer: provenance is available for the row you are about to paste, which is the row where it matters.

### 6.5 If the owner insists on provenance at a glance

Do **not** restore a three-segment text line on every row. Add a 15 × 15 app icon (or the 22 × 22 kind tile) and keep the type identical; compact row becomes 34 px with the icon inside it, no extra text. Type line count stays 1. The 50 px variant (meta on every row) is the fallback only if icons are impossible: row = 12 padding + 18 body + 4 gap + 16 meta + 8 padding = 50 px, meta at the same spec as §5.3 but with no hairline in the compact state.

---

## 7. Truncation spec

| State | Body lines | Body height | Meta | Row height | Ellipsis |
|---|---|---|---|---|---|
| compact | 1 | 18 | hidden | 34 | `CharacterEllipsis` at the end of line 1 |
| hover | 1 | 18 | hidden | 34 | unchanged (hover changes background only) |
| selected | `clamp(PreviewLines, 1, 6)` (default 3) | lines × 18 (default 54) | visible | 34 + 68 for 3 lines = 102 | `CharacterEllipsis` on the last *visible* line |
| image clip | 0 (thumbnail instead) | thumb `max-h 40` compact / `80` selected | as above | — | n/a |
| notification | `clamp(MaxLines, 1, 10)` | lines × 18 | timestamp in header | SizeToContent | `CharacterEllipsis` on the last line |
| settings hint | as needed | auto | — | — | wrap, no clamp |

Rules:

1. **`PreviewLines` is the selected card's line count, not the compact row's.** Store it (the property is missing from `Config.cs` today — S3 must land it, `CHECKLIST-features.md` line 102), validate 1–100 in Settings as today, but **clamp at render to 6**: at 7 lines the card is 120 px tall and fewer than three cards fit the popup, which breaks the list's purpose. Six is the most that still reads as a "card".
2. **Ellipsis is always at the end, always `CharacterEllipsis`.** `WordEllipsis` leaves ragged half-empty lines on code/URLs (the majority of clipboard content); middle truncation (Maccy's `truncationMode(.middle)`) is right for filenames and wrong for clip text, where the beginning is the identifier.
3. **Give the text a fixed `Height`, not `MaxHeight`.** WPF applies `TextTrimming` to the constrained measure; with `MaxHeight` the block can measure taller and the clip becomes a hard cut mid-glyph (measured behaviour: trimming trims only when a width/height constraint is in force — [SO 21892728](https://stackoverflow.com/questions/21892728/need-a-wpf-textblock-or-textbox-with-texttrimming-and-maxlines); trimming silently does nothing inside a horizontal `StackPanel` because width is unconstrained — [.NET 10 measurements](https://s-iguchi09.github.io/apps/wpf-standard-control-demo/textblock.html)). `Height = lines × LineHeight` is deterministic; the animation from 18 → 54 still works (the motion spec animates the body `MaxHeight`; use `Height` with the same numbers and it animates identically).
4. **`LineStackingStrategy` stays `MaxHeight`** (the default) with an explicit `LineHeight` ≥ natural. `BlockLineHeight` with a small `LineHeight` overlaps lines ([measurements](https://s-iguchi09.github.io/apps/wpf-standard-control-demo/textblock.html)).
5. **Never clamp by character count.** `ClipboardItem.Meta`'s 60-char window-title cut (`Core/ClipboardItem.cs:40`) becomes dead code once the title moves to the tooltip (§10) — delete it rather than leaving two truncation mechanisms.
6. **Tooltip carries the full text**: `ToolTip="{Binding Text}"`, `TextWrapping=Wrap`, `MaxWidth=420`, and a `ToolTipService.InitialShowDelay="600"` so arrowing does not flash panels.

---

## 8. Manrope-in-WPF gotchas

1. **Default instance is ExtraLight (200).** Proven from `fvar`. Anyone who "fixes the weight" by editing XAML will see no change until statics ship.
2. **No faux bold for a 200-weight family** (350–550 window). Expect `FontWeight="SemiBold"` to look *identical* to `Normal` if the statics are missing — that identity is the smoking gun, and it is the first thing to check after the fix.
3. **Faux oblique is added**, so italic never disappears — it only ever looks wrong. Do not debug "why is my italic skewed".
4. **ClearType is unreachable** in these windows (per-pixel opacity + root `DropShadowEffect`). Judging text at 100 % DPI on a grayscale-AA WPF window is not the same as judging a browser screenshot; do not compensate with heavier colours.
5. **`TextFormattingMode="Display"` is what makes 13 px readable**; it also makes glyph advances whole-pixel, so mixed-size inline text can look very slightly loose. Accept it. If the popup's 140 ms scale-in (motion spec) is visible as softness, that is expected: Display-mode text is pre-snapped and blurs while transformed, then snaps at scale 1.0. Never animate scale on text *after* the entrance.
6. **`LineHeight` is a fixed line box, not leading.** Setting it below the natural 1.366 em clips/overlaps; setting it above is safe. 18 px at 13 px and 16 px at 11 px are both above natural (17.76 / 15.03).
7. **Width must be constrained for trimming to work.** Any row content must sit in a `Grid` `*` column (as today) — never a horizontal `StackPanel` (unbounded width = no ellipsis).
8. **Three characters are not in the font**: `✓` U+2713, `✕` U+2715, `▾` U+25BE. WPF silently falls back to Segoe UI Symbol, so they render at a different weight/optical size from everything around them. Replace with Segoe MDL2 Assets glyphs (`\uE73E`, `\uE711`, `\uE70D`) or with `×` U+00D7, which *is* present.
9. **Tabular figures exist** (`tnum` in GSUB) but must be requested: `Typography.NumeralAlignment="Tabular"` on timestamps and counts. Verify; if unchanged, drop the attribute.
10. **`CharacterSpacing` is 1/1000 em.** `80` = 0.08 em. It applies to the whole `TextBlock`, so put it on the label, never on a shared style used by body text.
11. **WPF has no `text-transform`.** Uppercase labels must be written uppercase in the string (`HOTKEY`, not "Hotkey" + a style).
12. **Do not add a fallback list** like `#Manrope, Segoe UI`: it invites silent fallback when the pack URI is wrong, which is exactly the bug you want to see. Missing glyphs already fall back per-glyph.

---

## 9. What "verified" looks like on the first build

Run these five checks before calling the typography done. Each is a screenshot the owner can look at.

| # | Check | Pass looks like | Fail means |
|---|---|---|---|
| 1 | Harness: for each shipped file, `new Typeface(family, Normal, weight, Normal).TryGetGlyphTypeface(out gt)` → `gt.Weight` | 400 / 500 / 600, three distinct files | statics not deployed / names collide |
| 2 | 400 % crop of one row: "Hello" set at 400 vs 600 | visibly different stroke width, no blur halo | all one weight → §2 fix not applied |
| 3 | Selected card at 400 % | no slant anywhere; ellipsis sits on the last line, not a mid-glyph cut; descenders `g y p` fully visible; hairline above the meta is 1 px and `#212329` | `MaxHeight` instead of `Height`, or italic left in |
| 4 | Search band, empty and typed | placeholder legible grey (`#70737A`), typed text brighter, caret and text share a baseline, band is 48 px | 13 px leftover or a Segoe fallback |
| 5 | 100 % and 150 % DPI, same window | no clipping at 150 %, keycaps and labels do not drift, timestamp digits do not shimmy | `Display` mode not applied, or `LineHeight` too small |

Also eyeball the letter `g` and the numeral `1`: Manrope's single-storey `g` and flat-topped `1` are the fastest way to spot a silent Segoe UI fallback.

---

## 10. Pinned-decision conflicts (owner: one-line checklist edits)

| Pinned value | Where | This spec | Why |
|---|---|---|---|
| "Metadata line: **10px** non-italic above a 1 px hairline" | `CHECKLIST-uiux.md:19` | **11 px** / 16, 500 | Ortu's 10 px is Retina CSS px = 20 device px; 10 device px is below Microsoft's 12 px floor and is the specific smudge being fixed (§2.3, §6.3). Everything else in that line (non-italic, hairline, absolute 12h/24h time) is kept. |
| "**9px** uppercase section labels" | `CHECKLIST-uiux.md:27`, `RESEARCH-ortu-ui.md` §7 | **10 px** / 14, 600, +0.08 em | Same Retina-vs-1× correction; 10 px keeps the "metallic label" register while restoring cap height to 7.2 px. |
| Status bar **9 px** text / 8 px keycaps | `RESEARCH-ortu-ui.md` §7 | **11 px** text / 10 px keycaps | It is the only always-visible cheat sheet; a 9-device-pixel `↵ paste` is unreadable on a 1× display. |
| Window title **15 px Bold** | `SettingsWindow.xaml:202` | **13 px** 600 | Bold is not in the Windows ramp; Ortu's settings header is 13 px semibold. |

Everything else in `RESEARCH-ortu-ui.md` §7 is adopted unchanged (13 px body, colour-based hierarchy, uppercase tracked labels, no italic meta).

---

## 11. Verdict table — current XAML → spec → change?

| # | File:line | Current | Spec | Change |
|---|---|---|---|---|
| 1 | `MainWindow.xaml:32` | `TextElement.FontFamily` on Grid | `FontFamily` on Window + `TextOptions`/rounding (§4) | **Y** |
| 2 | `MainWindow.xaml:46` | search glyph 12 px, `Muted` | 14 px MDL2, `Fg40`, centred | **Y** |
| 3 | `MainWindow.xaml:48-50` | TextBox 13 px, `Fg` (white), no placeholder | `Search` 14/20, `Fg75`, `Padding 0`, placeholder `Fg40` | **Y** |
| 4 | `MainWindow.xaml:77-79` | body 13 px white, `LineHeight 19`, `MaxHeight 76`, Wrap + CharacterEllipsis | 13/18, `Fg55` → `Fg75`, `Height 18` → `54` | **Y** |
| 5 | `MainWindow.xaml:80-81` | meta 11 px **Italic** `Muted`, no hairline, always visible | 11/16 500/400 `Fg55`, hairline, selected-only, tabular | **Y** |
| 6 | `MainWindow.xaml:9-23` | Card style: `Surface` bg, Hairline border, radius 10, padding 12,10, margin 6 | flat row, radius 12, padding 12,8, no border/margin | **Y** (layout doc owns the box; the type values above assume it) |
| 7 | `NotificationWindow.xaml:29-30` | app name 12 SemiBold Accent | `Title` 11/16 600 Accent | **Y** |
| 8 | `NotificationWindow.xaml:31-32` | time 11 **Italic** `Muted`, `Margin 10,1,0,0` | 11/16 400 `Fg55`, no italic, no margin, tabular | **Y** |
| 9 | `NotificationWindow.xaml:37` | `✕` 11 px (glyph missing in Manrope) | MDL2 `\uE711` 12 px `Fg55` → `Accent` | **Y** |
| 10 | `NotificationWindow.xaml:55-56` | body 13 px `Fg`, no leading | 13/18 `Fg75`, Height = MaxLines × 18 | **Y** |
| 11 | `SettingsWindow.xaml:12-17` | `H` 13 SemiBold `Fg`, sentence case | `Label` 10/14 600 uppercase, `CharacterSpacing 80`, `Fg48` | **Y** |
| 12 | `SettingsWindow.xaml:19-24` | `Hint` 11 `Muted`, margin 2,2,0,8 | `Hint` 11/16 `Fg55`, margin per row | **Y** |
| 13 | `SettingsWindow.xaml:35-44` | `Field` 12, default weight | `Field` 12/16 500, tabular numbers | **Y** |
| 14 | `SettingsWindow.xaml:46-74` | `Chk` 12, tick `✓` 10 px (missing glyph) | 12/16 400 `Fg75`; tick MDL2 `\uE73E` 12 px or a `Path` | **Y** |
| 15 | `SettingsWindow.xaml:76-115` | `Combo` 12, chevron `▾` 10 px (missing glyph) | 12/16 500; chevron MDL2 `\uE70D` 10 px `Fg48` | **Y** |
| 16 | `SettingsWindow.xaml:117-135` | `ComboBoxItem` 12, padding 8,6 | `Caption` 12/16 400, padding 8,6 | **N** (size correct; only line-height added) |
| 17 | `SettingsWindow.xaml:137-162` | `Btn` 12 SemiBold | 12/16 600 | **N** |
| 18 | `SettingsWindow.xaml:164-184` | `BtnGhost` 12 Normal, `Muted` | 12/16 400, `Fg55` → `Fg` on hover | **Y** (colour only) |
| 19 | `SettingsWindow.xaml:202-203` | "Settings" 15 Bold | 13/18 600 | **Y** |
| 20 | `SettingsWindow.xaml:216,229,249,265,293,296,311,314,326,346,349,363,380,400` | row labels 12 px, inherited `Fg` | 13/18 500 `Fg75` | **Y** |
| 21 | `SettingsWindow.xaml:236,270` + error blocks `237-239,254-257,272-275` | hint 11 `Muted`; errors 11 `#FF6B6B` | `Hint` 11/16; error 11/16 | **Y** (sizes fine, leading/colour) |
| 22 | `SettingsWindow.xaml:426-427` | footer "Ctrl+S…" 11 `Muted` | `Hint` 11/16 `Fg48` | **Y** (colour) |
| 23 | tabs (not built) | — | §5.4 | **N/A → build to spec** |
| 24 | tooltip (not styled) | inherits Segoe UI | §4 `ToolTip` style | **N/A → add style** |
| 25 | rich preview (not built) | — | §5.8 base stylesheet | **N/A → build to spec** |
| 26 | `Core/ClipboardItem.cs:38-41` | meta joins app + date + 60-char title | `MetaApp` + `MetaTime`, title → tooltip; delete the 60-char cut | **Y** |

---

## 12. Not verified / risks

- The claim "the app today renders ExtraLight" is derived from the file's `fvar`/`OS/2` tables plus documented WPF font behaviour, not from a screenshot (this session ran no UI). It is falsified or confirmed in one minute by check #2 of §9.
- `Typography.NumeralAlignment="Tabular"` depends on Manrope's `tnum` feature surviving instancing; verify (drop if not).
- Google Fonts' static ZIP naming (`Manrope Medium` vs `ManropeMedium`) varies between releases; the `usWeightClass` values in §2.2 are the real gate, not the filenames.
- `HtmlRenderer.WPF` base-stylesheet font resolution was not tested here; the fallback (Times New Roman at 16 px) is the expected default, and §5.8 is the fix to verify.
- Ortu is a Retina macOS app; the 1×→2× reasoning in §2.3 is sound but is an inference, not something Ortu's source states.
