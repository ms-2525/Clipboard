# Clip — contrast & colour audit (DESIGN-contrast)

Researched and computed 2026-11-23. Source of truth for the numbers: `UI\Themes\Dark.xaml`,
`UI\Themes\Light.xaml`, `UI\MainWindow.xaml`, `UI\NotificationWindow.xaml`,
`UI\SettingsWindow.xaml` (read-only), plus the token spec in `docs\RESEARCH-ortu-ui.md` §8
for colours the theme files do not yet define (Sage / Amber / Sky / Violet / Placeholder).

Every ratio below was **computed from the declared hex values**, not estimated from a
screenshot. No app was launched. Where a token exists only in the research spec and not in a
theme dictionary, the table marks it **(spec)**.

---

## 0. Method

### 0.1 Formula (WCAG 2.2, normative definition)

Relative luminance, per channel on the 0–1 sRGB scale:

```
c_lin = c/12.92                          if c ≤ 0.04045
c_lin = ((c + 0.055)/1.055) ^ 2.4        otherwise
L     = 0.2126·R_lin + 0.7152·G_lin + 0.0722·B_lin
```

Contrast ratio:

```
CR = (L_lighter + 0.05) / (L_darker + 0.05)
```

Thresholds used (WCAG 2.2 AA):

| Content | Threshold | Why |
|---|---|---|
| Text, normal size | **4.5:1** | SC 1.4.3. Clip's largest text is 15 px Bold (Settings title) ≈ 11.25 pt — below the 14 pt-bold / 18 pt line, so **every** text pair in the app is "normal text"; there is no large-text 3:1 discount anywhere. |
| Focus indicators, selection indicators, rails, borders that are the only cue, checkbox tick, caret | **3:1** | SC 1.4.11. |
| Disabled / inactive controls | exempt | SC 1.4.3 "incidental", SC 1.4.11 "inactive components" (see §9.4). |
| Placeholder text | **4.5:1** | SC 1.4.3 explicitly includes placeholder text (§9.1). |

Ratios are **not rounded before the verdict**: 4.4953 is a FAIL at 4.5:1, exactly as the W3C
notes require.

### 0.2 Overlay compositing

WPF renders a `#AARRGGBB` brush by alpha-blending the overlay with what is under it. The
percentages below use the standard source-over formula in gamma-encoded sRGB — the same model
CSS uses, and therefore the model Ortu's alphas were authored against:

```
C_out = α·C_overlay + (1 − α)·C_base      (per R,G,B channel, 8-bit)
```

A white 8 % overlay over `Surface #13151B` is therefore `#26282D`, and contrast is computed
with `#26282D` as the background. This matters because the *effective* background of a hovered
or selected row is not a theme token — it is the token plus the overlay. (WPF's compositor is
colour-managed in scRGB; see §10 for the sub-1-point caveat.)

### 0.3 Surfaces measured

| Surface | Dark | Light | Used by |
|---|---|---|---|
| App | `#08090C` | `#F2F3F5` | popup shell |
| Surface | `#13151B` | `#FFFFFF` | cards, Settings/Notification root |
| Raised | `#1C1F28` | `#ECEEF1` | search bar, selected card, Settings rows, buttons |

Reference luminances: dark `L(App)=0.002735`, `L(Surface)=0.007539`, `L(Raised)=0.013801`;
light `L(App)=0.895710`, `L(Surface)=1.000000`, `L(Raised)=0.853328`.

---

## 1. Token inventory — what is live vs what is still spec

| Token | Dark.xaml | Light.xaml | Status |
|---|---|---|---|
| App / Surface / Raised | `#08090C` / `#13151B` / `#1C1F28` | `#F2F3F5` / `#FFFFFF` / `#ECEEF1` | live |
| Fg | `#FFFFFF` | `#15171C` | live |
| Muted | `#8A909C` | `#68707E` | live |
| Accent | `#FF8A3D` | `#FF8A3D` | live (same in both, as Ortu) |
| Hairline | `#262A35` | `#E2E5EA` | live |
| Error | — | — | **hardcoded** `#FF6B6B` in `SettingsWindow.xaml:10`, one red for both themes |
| Sage / Amber / Sky / Violet | — | — | **spec only** (Ortu values, `RESEARCH-ortu-ui.md` §8) |
| Placeholder | — | — | **absent** in the live window; spec `fg/0.18` dark / `fg/0.30` light |

Two facts drive most of this document:

1. Ortu declares accent and sage **theme-independent on purpose** (`src/app.css`:
   *"Accent colors (orange, sage) are intentionally theme-independent."*). That is fine for
   fills on a dark surface; it is not survivable as text on a light surface.
2. Clip's light theme is not Ortu's light theme. Ours is `App #F2F3F5 / Surface #FFFFFF /
   Raised #ECEEF1 / Fg #15171C`; Ortu's is `#DEE2E9 / #FCFDFF / #E9ECF1 / #11161F`. The
   difference is small but it moves the worst-case surface: our Raised is the one that fails.

---

## 2. Dark theme — text on surface

Measured against each surface. All text is normal-size → 4.5:1.

| Token | Hex | App | Surface | Raised | Verdict |
|---|---|---:|---:|---:|---|
| Fg | `#FFFFFF` | 19.91 | 18.25 | 16.46 | PASS |
| Muted | `#8A909C` | 6.21 | 5.69 | 5.13 | PASS (base) |
| Accent | `#FF8A3D` | 8.49 | 7.78 | 7.02 | PASS |
| Error | `#FF6B6B` | 7.17 | 6.58 | 5.93 | PASS |
| Sage (spec) | `#AEB291` | 9.08 | 8.32 | 7.51 | PASS |
| Amber (spec) | `#FBBF24` | 11.93 | 10.93 | 9.86 | PASS |
| Sky (spec) | `#38BDF8` | 9.29 | 8.52 | 7.68 | PASS |
| Violet (spec) | `#A78BFA` | 7.32 | 6.71 | 6.05 | PASS |
| Placeholder, spec `fg/0.18` over Surface | `#3D3F44` | — | **1.73** | **1.56** | **FAIL** |
| Placeholder over our Raised search bar (`fg/0.18`) | `#45474F` | — | 1.97 | **1.78** | **FAIL** |

Dark base text is **compliant**. The owner's "background/font contrast" complaint in dark is
not a failing ratio on the three base surfaces — it is three other things:

1. **Overlay states**: Muted drops to 4.06 on `Raised + 8 %` and below 4.5 on every 15 % row
   (§5). The selected card *is* Raised, so the state the user looks at most is the one that fails.
2. **Placeholder** is effectively invisible (1.73–1.78), by Ortu's design (`fg/0.18` is
   decorative, not text).
3. **11 px italic Manrope** in the meta line: Manrope ships no true italic, so WPF synthesises
   an oblique by skewing, which thins strokes. The declared pair passes at 5.13, but the
   rendered glyphs are the faintest thing on screen. `RESEARCH-ortu-ui.md` #17 already says to
   drop `FontStyle="Italic"`; that is a contrast fix in perception, not in maths.

---

## 3. Light theme — text on surface

| Token | Hex | App | Surface | Raised | Verdict |
|---|---|---:|---:|---:|---|
| Fg | `#15171C` | 16.15 | 17.93 | 15.43 | PASS |
| Muted | `#68707E` | **4.4953** | 4.99 | **4.29** | **FAIL** (App by 0.005, Raised outright) |
| Accent | `#FF8A3D` | **2.11** | **2.35** | **2.02** | **FAIL** |
| Error | `#FF6B6B` | **2.50** | **2.78** | **2.39** | **FAIL** |
| Sage (spec) | `#AEB291` | **1.98** | **2.19** | **1.89** | **FAIL** |
| Amber (spec) | `#FBBF24` | **1.50** | **1.67** | **1.44** | **FAIL** |
| Sky (spec) | `#38BDF8` | **1.93** | **2.14** | **1.84** | **FAIL** |
| Violet (spec) | `#A78BFA` | **2.45** | **2.72** | **2.34** | **FAIL** |
| Placeholder, spec `fg/0.30` | `#B9B9BB` | **1.76** | **1.96** | **1.69** | **FAIL** |

Only Fg survives. Every accent/semantic colour in light is a dark-theme value dropped onto a
pale surface; the whole light palette needs correcting, not a single token. Muted is the
subtle one: 4.4953 on App is below the threshold and 4.29 on Raised is clearly below it. Any
claim that "light is legible end-to-end" fails on measurement.

---

## 4. Corrected tokens (computed)

Method: keep hue, move lightness (and saturation only if lightness alone cannot reach the
target) the minimum amount, then round to a hex and re-check. Where a colour is used on all
three surfaces, it was solved for the **worst** surface (light `Raised`, dark `Raised`), not
for white — solving for white is how `#CA5000` looks like a fix but still gives 3.88 on Raised.

### 4.1 The "what Muted clears 4.5 on `#13151B`?" question

- Current `#8A909C` **already** clears it: **5.69** on `#13151B` (`0.277501+0.05) /
  (0.007539+0.05)`). No base-surface fix is mathematically required.
- The suggested `#9BA1AC` computes to **7.03** on `Surface`, **6.34** on `Raised`, and
  **5.01** on `Raised + 8 % white`. It is the recommended value because it has margin; the
  true minimum along the same hue that clears Raised + 8 % is `#9399A5` (4.55 on that state,
  6.38 on Surface), which is too tight to survive any further tint.
- The reason to change it anyway: at 5.13 on Raised the current value sits at the same rank as
  Windows' own *tertiary* text tier (5.78 composite), and any overlay pushes it under 4.5.
  Raising it buys the state coverage, not the base ratio.

### 4.2 Dark replacements

| Token | Current | Replace with | App | Surface | Raised | Worst state |
|---|---|---:|---:|---:|---|
| Muted | `#8A909C` | **`#9BA1AC`** | 7.67 | 7.03 | 6.34 | 5.01 at Raised+8 % |
| Placeholder | `#3D3F44` | **`#82868F`** | 5.46 | 5.00 | 4.51 | 4.55 at Surface+4 %; 4.04 at Raised+4 % → placeholder stays in the un-tinted search bar |
| Fg / Accent / Error / Sage / Amber / Sky / Violet | — | unchanged | | | | all pass |

### 4.3 Light replacements

| Token | Current | Replace with | App | Surface | Raised | Note |
|---|---|---:|---:|---:|---|
| Muted | `#68707E` | **`#586070`** | 5.69 | 6.32 | 5.44 | survives Raised+8 % at 4.64 |
| AccentText | `#FF8A3D` | **`#B84900`** | 4.74 | 5.26 | 4.53 | same hue, 4.5 on the worst surface |
| Error | `#FF6B6B` | **`#CC2E33`** | 4.72 | 5.24 | 4.51 | |
| Sage | `#AEB291` | **`#686C47`** | 4.95 | 5.49 | 4.72 | success / domain / capture |
| Amber | `#FBBF24` | **`#826000`** | 5.22 | 5.80 | 4.99 | pinned |
| Sky | `#38BDF8` | **`#046C99`** | 5.24 | 5.82 | 5.00 | URL rail |
| Violet | `#A78BFA` | **`#7347E8`** | 4.98 | 5.53 | 4.76 | code rail |
| Placeholder | `#B9B9BB` | **`#69696E`** | 4.92 | 5.46 | 4.70 | |

For reference, the computed minimum light Muted that clears `Raised + 8 %` is `#5B6171`
(4.54 on that state); `#586070` is used above because it keeps ~0.1 of margin. The same
one-step search for the light accent gives `#B84900` (4.53 on Raised) — that one is the
minimum, not a padded value.

`Accent` itself stays `#FF8A3D` in light **as a fill**, because content on an accent fill must
be near-black: black on `#FF8A3D` = **8.95:1**, `#15171C` on `#FF8A3D` = **7.65:1**, while
white on it is only **2.35:1**. Add a separate `AccentText` for accent-coloured type/rails.
The one-token alternative — set light `Accent` to `#B84900` — is smaller in XAML but then every
accent fill must use white ink (5.26) and can never use black (3.99 on `#B84900`).

### 4.4 Corrected contrast of the replacements under overlays (proof)

| Replacement | App | Surface | Raised | +4 % | +8 % | +15 % |
|---|---:|---:|---:|---:|---:|---:|
| Dark Muted `#9BA1AC` (white alpha) | 7.67 | 7.03 | 6.34 | 7.16 / 6.39 / 5.67 | 6.51 / 5.70 / 5.01 | 6.17 / 5.31 / 3.94 |
| Light Muted `#586070` (ink alpha) | 5.69 | 6.32 | 5.44 | 5.26 / 5.84 / 5.03 | 4.85 / 5.37 / 4.64 | 4.18 / 4.62 / 4.00 |

(Each cell is App / Surface / Raised on that overlay.) Both replacements hold through 8 %;
both fail at 15 % on at least one surface. The 15 % overlay must therefore not be used for
selection — see §5.3.

### 4.5 Deliverable table — token, current, verdict, correction

One row per token. "Corrected" is the value to ship (blank = unchanged). All columns are
contrast on App / Surface / Raised at the token's normal state.

**Dark theme**

| Token | Current | App | Surface | Raised | Verdict | Corrected | App | Surface | Raised |
|---|---|---:|---:|---:|---|---|---:|---:|---:|
| Fg | `#FFFFFF` | 19.91 | 18.25 | 16.46 | PASS | — | | | |
| Muted | `#8A909C` | 6.21 | 5.69 | 5.13 | PASS base / FAIL at Raised+8 % (4.06) | **`#9BA1AC`** | 7.67 | 7.03 | 6.34 |
| Accent | `#FF8A3D` | 8.49 | 7.78 | 7.02 | PASS | — | | | |
| Error | `#FF6B6B` | 7.17 | 6.58 | 5.93 | PASS (needs a token) | — | | | |
| Sage | `#AEB291` | 9.08 | 8.32 | 7.51 | PASS | — | | | |
| Amber | `#FBBF24` | 11.93 | 10.93 | 9.86 | PASS | — | | | |
| Sky | `#38BDF8` | 9.29 | 8.52 | 7.68 | PASS | — | | | |
| Violet | `#A78BFA` | 7.32 | 6.71 | 6.05 | PASS | — | | | |
| Placeholder | `#3D3F44` | — | **1.73** | **1.56** | **FAIL** | **`#82868F`** | 5.46 | 5.00 | 4.51 |

**Light theme**

| Token | Current | App | Surface | Raised | Verdict | Corrected | App | Surface | Raised |
|---|---|---:|---:|---:|---|---|---:|---:|---:|
| Fg | `#15171C` | 16.15 | 17.93 | 15.43 | PASS | — | | | |
| Muted | `#68707E` | **4.4953** | 4.99 | **4.29** | **FAIL** | **`#586070`** | 5.69 | 6.32 | 5.44 |
| Accent (as text/rail) | `#FF8A3D` | **2.11** | **2.35** | **2.02** | **FAIL** | **`AccentText #B84900`** | 4.74 | 5.26 | 4.53 |
| Error | `#FF6B6B` | **2.50** | **2.78** | **2.39** | **FAIL** | **`#CC2E33`** | 4.72 | 5.24 | 4.51 |
| Sage | `#AEB291` | **1.98** | **2.19** | **1.89** | **FAIL** | **`#686C47`** | 4.95 | 5.49 | 4.72 |
| Amber | `#FBBF24` | **1.50** | **1.67** | **1.44** | **FAIL** | **`#826000`** | 5.22 | 5.80 | 4.99 |
| Sky | `#38BDF8` | **1.93** | **2.14** | **1.84** | **FAIL** | **`#046C99`** | 5.24 | 5.82 | 5.00 |
| Violet | `#A78BFA` | **2.45** | **2.72** | **2.34** | **FAIL** | **`#7347E8`** | 4.98 | 5.53 | 4.76 |
| Placeholder | `#B9B9BB` | **1.76** | **1.96** | **1.69** | **FAIL** | **`#69696E`** | 4.92 | 5.46 | 4.70 |

Accent in light remains `#FF8A3D` for **fills**; only its text/rail usage moves to `AccentText`.
That split is the one place where a token is replaced by a pair rather than a single hex.

---

## 5. Overlay-state matrix

Ortu's states: hover `overlay / 0.04`, selected `overlay / 0.08`; the 15 % figure is the
aggressive tint the task asked to test. In dark, `overlay` is white; in light, Ortu's
`overlay` is `#111827` (ink, not white) — `src/app.css` light block. Clip currently has no
overlay brushes at all (cards are flat `Surface` → `Raised`), so this matrix is the acceptance
test for the selection/hover layer when the UI sprint adds it.

### 5.1 Dark — white alpha over each surface

| State | Effective bg | Fg | Muted now | Muted fixed | Accent | Error |
|---|---|---:|---:|---:|---:|---:|
| App, normal | `#08090C` | 19.91 | 6.21 | 7.67 | 8.49 | 7.17 |
| App + 4 % (hover) | `#121316` | 18.60 | 5.80 | 7.16 | 7.93 | 6.70 |
| App + 8 % (selected) | `#1C1D1F` | 16.91 | 5.28 | 6.51 | 7.21 | 6.09 |
| App + 15 % | `#2D2E30` | 13.60 | **4.24 FAIL** | 6.17 | 5.80 | 4.90 |
| Surface, normal | `#13151B` | 18.25 | 5.69 | 7.03 | 7.78 | 6.58 |
| Surface + 4 % | `#1C1E24` | 16.59 | 5.18 | 6.39 | 7.08 | 5.98 |
| Surface + 8 % | `#26282D` | 14.79 | 4.61 | 5.70 | 6.31 | 5.33 |
| Surface + 15 % | `#36383D` | 11.70 | **3.65 FAIL** | 5.31 | 4.99 | **4.22 FAIL** |
| Raised, normal | `#1C1F28` | 16.46 | 5.13 | 6.34 | 7.02 | 5.93 |
| Raised + 4 % | `#252831` | 14.73 | 4.59 | 5.67 | 6.28 | 5.31 |
| Raised + 8 % | `#2E3139` | 13.01 | **4.06 FAIL** | 5.01 | 5.55 | 4.69 |
| Raised + 15 % | `#3E4148` | 10.26 | **3.20 FAIL** | **3.94 FAIL** | 4.38 | **3.70 FAIL** |

That Raised + 8 % row is the one that silently breaks the current app: the *selected* card is
Raised, selection adds 8 % white, and the 11 px meta line drops to 4.06. Even the corrected
Muted cannot survive 15 % (Raised 3.94), and error red drops below 4.5 at 15 % on both Surface
(4.22) and Raised (3.70).

### 5.2 Light — ink alpha `#111827` over each surface

| State | Effective bg | Fg | Muted now | Muted fixed | Accent now | AccentText fixed | Error fixed |
|---|---|---:|---:|---:|---:|---:|---:|
| App, normal | `#F2F3F5` | 16.15 | **4.4953 FAIL** | 5.69 | 2.11 | 4.74 | 4.72 |
| App + 4 % | `#E9EAED` | 14.93 | **4.15 FAIL** | 5.26 | 1.95 | **4.38 FAIL** | **4.36 FAIL** |
| App + 8 % | `#E0E1E5` | 13.76 | **3.83 FAIL** | 4.85 | 1.80 | **4.04 FAIL** | **4.02 FAIL** |
| App + 15 % | `#D0D2D6` | 11.86 | **3.30 FAIL** | **4.18 FAIL** | 1.55 | **3.48 FAIL** | **3.47 FAIL** |
| Surface, normal | `#FFFFFF` | 17.93 | 4.99 | 6.32 | 2.35 | 5.26 | 5.24 |
| Surface + 8 % | `#ECEDEE` | 15.24 | **4.24 FAIL** | 5.37 | 1.99 | **4.47 FAIL** | **4.46 FAIL** |
| Surface + 15 % | `#DBDCDF` | 13.11 | **3.65 FAIL** | 4.62 | 1.71 | **3.85 FAIL** | **3.83 FAIL** |
| Raised, normal | `#ECEEF1` | 15.43 | **4.29 FAIL** | 5.44 | 2.02 | 4.53 | 4.51 |
| Raised + 4 % | `#E3E5E9` | 14.26 | **3.97 FAIL** | 5.03 | 1.87 | **4.19 FAIL** | **4.17 FAIL** |
| Raised + 8 % | `#DADDE1` | 13.16 | **3.66 FAIL** | 4.64 | 1.72 | **3.86 FAIL** | **3.85 FAIL** |
| Raised + 15 % | `#CBCED3` | 11.36 | **3.16 FAIL** | **4.00 FAIL** | 1.49 | **3.33 FAIL** | **3.32 FAIL** |

Consequences for light:

- Fixed Muted is the only secondary text colour that survives hover/selection up to 8 %.
- **`AccentText` and `Error` must never be placed on a tinted/selected row in light.** At 8 %
  they are 3.86–4.47. Keep accent type on the notification (Surface, un-tinted) and error
  lines outside the row they describe — which is how the XAML already places them.
- 15 % overlays are unusable in light at any token value above; do not adopt them.

### 5.3 Accent tint (Ortu's selected card)

Ortu's selected card is `accent / 0.08`, not white. With `Accent #FF8A3D`:

| Surface | +8 % bg | Fg | Muted now (dark) | Muted fixed (dark) | Accent |
|---|---|---:|---:|---:|---:|
| Dark App | `#1C1310` | 18.24 | 5.69 | 7.02 | 7.79 |
| Dark Surface | `#261E1E` | 16.28 | 5.08 | 6.27 | 6.96 |
| Dark Raised | `#2E282A` | 14.51 | 4.53 | 5.59 | 6.16 |

15 % accent tint on dark Raised is `#3E2F2B`: Muted now **3.98 FAIL**, fixed `#9BA1AC` 4.91.
Light 8 % accent tint: Muted fixed 5.36 / 5.91 / 5.13 (App/Surface/Raised) — PASS; AccentText
fixed 4.46 / 4.92 / **4.27 FAIL**. So: **cap every tint at 8 %**, and no accent text on the
tint in light.

---

## 6. UI components, rails and focus (3:1, SC 1.4.11)

All text in Clip is normal-size, so 3:1 is *only* for non-text indicators. These are the ones
that encode something a user must see.

| Element | Colour | On dark Surface | On dark Raised | On light Surface | On light Raised | Verdict |
|---|---|---:|---:|---:|---:|---|
| Selection rail (gradient start) | `#FF8A3D` | 7.78 | 7.02 | **2.35** | **2.02** | PASS dark / **FAIL light** |
| Selection rail (gradient end) | `#FF6B1A` | 6.40 | 5.78 | **2.85** | **2.45** | PASS dark / **FAIL light** |
| Selection inset ring, accent/0.15 | `#362720` on Surface | 1.27 | 1.29 | 1.13 | 1.12 | **FAIL** |
| Selection inset ring, accent/0.28 | `#553625` on Surface | 1.68 | 1.69 | 1.27 | 1.23 | **FAIL** |
| Focus ring, accent/0.35 (as suggested in `RESEARCH-ortu-ui.md` §8) | `#663E27` | 1.98 | 1.97 | 1.35 | 1.29 | **FAIL** |
| Pinned rail, amber/0.45 | `#7B621F` / `#806726` | 3.13 | **2.82** | — | — | PASS Surface / **FAIL Raised** |
| URL rail, sky/0.30 | `#1E475D` | **1.84** | **1.66** | 1.26 | — | **FAIL** |
| Code rail, violet/0.30 | `#3F385E` | **1.68** | **1.52** | 1.31 | — | **FAIL** |
| Sage rail, sage/0.55 | `#6C7062` | 3.59 | 3.23 | — | — | PASS |
| Checkbox border (unchecked), Hairline | `#262A35` | 1.27 | **1.15** | 1.26 | **1.09** | **FAIL** |
| Checkbox tick on checked fill | theme `Surface` on `#FF8A3D` | 7.78 (`#13151B`) | 7.78 | **2.35** (`#FFFFFF`) | **2.35** | PASS dark / **FAIL light** |
| Hairline divider / card border | `#262A35` | 1.27 | 1.15 | 1.26 | 1.09 | Exempt — decorative, text identifies the control |
| Surface boundary vs App (search field) | `#1C1F28` on `#08090C` | 1.21 | — | 1.05 | — | Exempt — icon + placeholder identify the field |

Notes:

- **The 2 px rail, not the 0.15 ring, is the selection/focus indicator.** `accent/0.15`
  computes 1.27 and can never meet 3:1; the full-strength rail passes in dark. In light the
  rail fails until `AccentText #B84900` is used (5.26 on Surface / 4.53 on Raised). Do not add
  the 1 px `accent/0.35` focus ring proposed in the visual research — it computes 1.98.
- **Rails must be solid, pre-composited colours.** At Ortu's alphas they fail; the minimum
  alphas that clear 3:1 on Raised are: amber 45 % → `#806726` (3.05), sky 55 % → `#2B769A`
  (3.26), violet 60 % → `#6F60A6` (3.05), sage 55 % → `#6C7062` (3.23). Full-strength tokens
  are simpler and stronger: amber 9.86, sky 7.68, violet 6.05, sage 7.51 on Raised. In light,
  use the corrected semantic tokens directly (`#826000`, `#046C99`, `#7347E8`, `#686C47`) —
  the bright dark-theme values fail at *any* alpha on white (sky at 100 % is still 2.14).
- **Unchecked checkbox** is currently invisible as a control: `Hairline` is 1.09–1.15 against
  its row. This is a real SC 1.4.11 failure, not a nicety. Use `ControlLine`: dark `#666C7C`
  (3.14 on Raised), light `#7F8691` (3.16 on Raised).
- **Checked checkbox**: dark passes with dark ink on `#FF8A3D` (current `Surface` tick = 7.78;
  `OnAccent #08090C` = 8.49). Light is broken twice — `#FF8A3D` fill vs `#ECEEF1` row is 2.02
  and the white tick is 2.35. With `AccentText` fill `#B84900` + white tick: 4.53 (fill vs
  row) and 5.26 (tick). Add an `OnAccent` token: dark `#08090C`, light `#FFFFFF` — exactly the
  WinUI pattern (§9.2).
- **Caret**: MainWindow uses `Fg` (passes). Settings uses `Accent` — 7.02 dark passes, 2.02
  light fails; point it at `AccentText`.

---

## 7. Minimal change set

Only failing or missing tokens. Nothing that already passes is touched.

### 7.1 `UI\Themes\Dark.xaml`

```xml
<SolidColorBrush x:Key="Muted"       Color="#9BA1AC"/>  <!-- was #8A909C; 7.03 on Surface, 5.01 on Raised+8% -->
<SolidColorBrush x:Key="Placeholder" Color="#82868F"/>  <!-- new; 5.00 Surface / 4.51 Raised -->
<SolidColorBrush x:Key="Error"       Color="#FF6B6B"/>  <!-- new; move out of SettingsWindow -->
<SolidColorBrush x:Key="OnAccent"    Color="#08090C"/>  <!-- new; ink on #FF8A3D = 8.49 -->
<SolidColorBrush x:Key="ControlLine" Color="#666C7C"/>  <!-- new; 3.14 on Raised -->
<SolidColorBrush x:Key="Sage"        Color="#AEB291"/>  <!-- new (spec) -->
<SolidColorBrush x:Key="Amber"       Color="#FBBF24"/>  <!-- new (spec) -->
<SolidColorBrush x:Key="Sky"         Color="#38BDF8"/>  <!-- new (spec) -->
<SolidColorBrush x:Key="Violet"      Color="#A78BFA"/>  <!-- new (spec) -->
<SolidColorBrush x:Key="AccentText"  Color="#FF8A3D"/>  <!-- new; same as Accent in dark -->
```

### 7.2 `UI\Themes\Light.xaml`

```xml
<SolidColorBrush x:Key="Muted"       Color="#586070"/>  <!-- was #68707E; 5.44 on Raised -->
<SolidColorBrush x:Key="Placeholder" Color="#69696E"/>  <!-- new; 4.70 on Raised -->
<SolidColorBrush x:Key="Error"       Color="#CC2E33"/>  <!-- new; 4.51 on Raised -->
<SolidColorBrush x:Key="OnAccent"    Color="#FFFFFF"/>  <!-- new; on #B84900 = 5.26 -->
<SolidColorBrush x:Key="ControlLine" Color="#7F8691"/>  <!-- new; 3.16 on Raised -->
<SolidColorBrush x:Key="AccentText"  Color="#B84900"/>  <!-- new; 4.53 on Raised, 5.26 on Surface -->
<SolidColorBrush x:Key="Sage"        Color="#686C47"/>  <!-- new; 4.72 on Raised -->
<SolidColorBrush x:Key="Amber"       Color="#826000"/>  <!-- new; 4.99 on Raised -->
<SolidColorBrush x:Key="Sky"         Color="#046C99"/>  <!-- new; 5.00 on Raised -->
<SolidColorBrush x:Key="Violet"      Color="#7347E8"/>  <!-- new; 4.76 on Raised -->
```

`Accent`, `Fg`, `App`, `Surface`, `Raised`, `Hairline` are **not changed**. Accent stays a
fill colour; `AccentText` is the type/rail variant.

### 7.3 Usage edits (three files, small)

| File | Line / style | Change |
|---|---|---|
| `SettingsWindow.xaml` | `:10` | delete the hardcoded `ErrorBrush`; every `Foreground="{StaticResource ErrorBrush}"` → `{StaticResource Error}` |
| `SettingsWindow.xaml` | `Chk` template `:55` | `BorderBrush="{StaticResource Hairline}"` → `{StaticResource ControlLine}` |
| `SettingsWindow.xaml` | `Chk` template `:58` | `Foreground="{StaticResource Surface}"` → `{StaticResource OnAccent}` (the `Surface` trick puts white on orange in light) |
| `SettingsWindow.xaml` | `Chk` trigger `:66–67` | checked `Background`/`BorderBrush` `{StaticResource Accent}` → `{StaticResource AccentText}` |
| `SettingsWindow.xaml` | `Field` `:41–42` | caret/selection `Accent` → `AccentText` |
| `NotificationWindow.xaml` | `AppName` `:29` | `Accent` → `AccentText` |
| `NotificationWindow.xaml` | ✕ hover `:43` | `Accent` → `AccentText` |
| `MainWindow.xaml` | `Card` selected `:20` | `BorderBrush` `Accent` → `AccentText` (1 px ring at 2.02 in light fails) |
| `MainWindow.xaml` | search band `:38–52` | add a `TextBlock Text="Search history, groups…"` over the `TextBox`, `Foreground="{StaticResource Placeholder}"`, `IsHitTestVisible="False"`, visible when `Search.Text` is empty; size 13 px |
| `MainWindow.xaml` | meta `:80` | drop `FontStyle="Italic"` (Ortu research #17; Manrope's synthetic oblique is what reads as "faint") |

No other colour values need to change. If the selection/hover overlay layer is built, cap
white alpha at **8 %** (dark) and ink alpha at **8 %** (light), and keep accent text off tinted
rows in light.

---

## 8. Semantic colours — error, success, pin

- **Error.** One token, two values. Dark `#FF6B6B` is good (5.93–7.17) and matches the current
  hardcode; light must be `#CC2E33` (4.51 Raised / 5.24 Surface). Error text currently sits
  outside the rows it describes (Settings, under the field), which is the only place it passes
  in light. Do not move it into a hovered/selected row: on an 8 % tint it computes 4.25.
- **Success.** Use `Sage` — dark `#AEB291` 7.51 on Raised, light `#686C47` 4.72. That is also
  Ortu's copy-pulse family (`green-500` in Ortu is only for the 380 ms pulse; sage is the
  app's semantic success). No separate green token is needed; adding one would be a fourth
  hue for the same meaning.
- **Pinned.** Use `Amber` — dark `#FBBF24` 9.86, light `#826000` 4.99. The rail must be solid:
  Ortu's `amber-400/0.45` is 3.13 on Surface but **2.82 on Raised**, so a pinned mark on the
  selected card fails. Solid composite `#806726` is the minimum that passes on Raised (3.05);
  full `#FBBF24` is the safer 2 px mark.
- **Accent is structural, not text.** It is a fill/border/rail colour. Where the app wants
  orange *type* (notification app name, ✕ hover, caret), use `AccentText`. In dark they are
  the same hex; in light they must differ.
- **Never hue alone.** Pinned = rail + section label; selection = rail + tint + border. That
  keeps SC 1.4.1 satisfied after the tint is capped at 8 %.

---

## 9. Research (sourced)

### 9.1 WCAG 2.2 — text and non-text thresholds

- **SC 1.4.3 Contrast (Minimum), AA:** text needs **4.5:1**; large text (≥18 pt, or ≥14 pt
  bold — ≈24 px / 18.66 px) needs 3:1. Thresholds are not rounded: *"the computed values
  should not be rounded (e.g., 4.499:1 would not meet the 4.5:1 threshold)."*
  <https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html>
- **Placeholder text is text:** *"This success criterion applies to text in the page,
  including placeholder text and text that is shown when a pointer is hovering over an object
  or when an object has keyboard focus."* Same page. This is why Ortu's `fg/0.18` placeholder
  (1.73) cannot ship.
- **SC 1.4.11 Non-text Contrast, AA:** UI components and meaningful graphics need **3:1**
  against adjacent colours; *"any visual information necessary to indicate state, such as
  whether a component is selected or focused must also ensure that the information used to
  identify the control in that state has a minimum 3:1 contrast ratio."* Boundaries are only
  in scope where they are the only cue: *"This success criterion does not require that
  controls have a visual boundary … If a control has visible content … a border … is not
  required."* Focus indicators are always in scope. Worked failures on the same page: a
  `#9D9D9D` checkbox border on white = 2.7:1 **fails**; a `#767676` input border on white
  passes. <https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html>
- **Large text in Clip:** nothing qualifies. 15 px Bold at 96 dpi ≈ 11.25 pt; the 14 pt-bold
  line is ≈18.66 px. 3:1 therefore applies only to rails/rings/ticks/carets.

### 9.2 Microsoft / Windows 11 dark-theme text tiers (WinUI)

Read from the WinUI theme resource source
(`controls/dev/CommonStyles/Common_themeresources_any.xaml`,
<https://github.com/microsoft/microsoft-ui-xaml>), which is what `TextFillColor*Brush`
resolves to:

| Token | Dark | Light |
|---|---|---|
| `TextFillColorPrimary` | `#FFFFFF` | `#E4000000` (89 % black) |
| `TextFillColorSecondary` | `#C5FFFFFF` (77 % white) | `#9E000000` (62 %) |
| `TextFillColorTertiary` | `#87FFFFFF` (53 % white) | `#72000000` (45 %) |
| `TextFillColorDisabled` | `#5DFFFFFF` (36 % white) | `#5C000000` (36 %) |
| `TextOnAccentFillColorPrimary` | `#000000` | `#FFFFFF` |

Composited on Clip's surfaces:

| Tier | On dark Surface | On dark Raised | On light Surface | On light Raised |
|---|---:|---:|---:|---:|
| Primary | 18.25 | 16.46 | 17.22 | 15.10 |
| Secondary | 11.11 | 10.25 | 6.19 | 5.89 |
| Tertiary | 5.78 | 5.54 | 3.32 | 3.24 |
| Disabled | 3.39 | 3.36 | 2.52 | 2.48 |

Two conclusions. (a) Clip's `Muted` is a **tertiary**-tier colour, not a secondary one: 5.69
dark / 4.99 light against Windows' 10–11 dark / 5.9 light. That is why it feels faint next to
primary text. (b) Windows resolves *on-accent* ink per theme — black on the light accent in
dark, white on the darker accent in light. Clip's `Tick Foreground="{StaticResource Surface}"`
is the bug that ignores this; an `OnAccent` token reproduces the platform behaviour.

Microsoft's own accessibility guidance repeats the WCAG thresholds verbatim: *"visible text
must have a minimum luminance contrast ratio of 4.5:1 against its background. Exceptions
include logos and incidental text, such as text in inactive UI."*
<https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements>
Windows' colour guidance also says accent values are *"generated automatically and optimized
for contrast in both light and dark modes"* — i.e. the platform never reuses one accent hex as
text on both backgrounds. <https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/color>

### 9.3 How Ortu derives its light alphas

Ortu's tokens are RGB triplets, not hex, so every colour is `rgb(var(--x) / <alpha>)`. From
`src/app.css` (<https://github.com/abhijith-p-subash/ortu/blob/main/src/app.css>):

```css
:root {                        /* dark */
  --app: 8 9 12;  --surface: 19 21 27;  --raised: 28 31 40;
  --fg: 255 255 255;  --overlay: 255 255 255;
  --accent: #ff8a3d;  --sage: #aeb291;
}
:root[data-theme="light"] {
  --app: 222 226 233;  --surface: 252 253 255;  --raised: 233 236 241;
  --fg: 17 22 31;  --overlay: 17 24 39;
}
```

with the comment *"Accent colors (orange, sage) are intentionally theme-independent."* The
light theme flips only `--overlay` from white to `#111827`; every alpha in the CSS
(`overlay/[0.04]` hover, `overlay/[0.08]` selected, `overlay/[0.12]` border) therefore
composites dark-on-light in light mode and light-on-dark in dark mode. That is exactly the
compositing model used in §5. It also means Ortu has no light-mode accent *text* value: its
sage/orange type is ~2:1 on its own pale surfaces. Ortu is the visual authority, not the
accessibility authority; where they conflict, WCAG wins.

### 9.4 Disabled text — 3:1 or exempt?

**Exempt, not 3:1.** SC 1.4.3's "Incidental" exception covers *"text … that are part of an
inactive user interface component"*, and SC 1.4.11 excludes *"inactive components"*. Microsoft
says the same ("incidental text, such as text in inactive UI"). So a disabled control has no
numeric requirement. The practical floor is different: Windows' own disabled tier is 36 %
white/black, which composites to **3.39** on Clip's dark Surface but only **2.52** on light
Surface — below 3:1 by accident, not by intent. Guidance for Clip: if a disabled state is
added, use a dedicated token that keeps ≥3:1 where the disabled control must still be
discovered (an un-actionable *state*), and never implement it with parent `Opacity`, which
multiplies down over children (the visual research §13 already warns about this).

---

## 10. Limits

- Ratios are computed from declared sRGB hexes. They do **not** include ClearType/anti-aliasing
  thinning, the window drop shadow, or DPI scaling. The W3C explicitly says to evaluate
  declared colours, so the numbers are the correct ones to test against.
- Overlay composites use gamma-encoded sRGB source-over, the CSS model Ortu's alphas were
  authored against. WPF's compositor is colour-managed via scRGB, so a layered brush can differ
  by a fraction of a point from these values. The only replacements near a threshold are dark
  `Placeholder` on Raised (4.51) and light Muted on un-tinted App (5.69, corrected from 4.4953);
  everything else has ≥0.2 of margin.
- The popup entrance animates `Shell.Opacity 0.70 → 1` over 140 ms and the notification
  `0 → 1` over 150 ms (`RESEARCH-motion-ui.md` §3). During those frames text is composited
  against the desktop, so effective contrast is transiently lower. WCAG conformance is judged
  on the steady state; the motion spec's 200 ms ceiling keeps the transient short. Do not
  extend either fade to "improve" it.
- The light palette correction assumes Clip keeps its own light surfaces (`#F2F3F5 / #FFFFFF /
  #ECEEF1`). If the theme is later aligned to Ortu's exact light triplets, the corrected
  values still pass, with margin (Ortu's Raised `#E9ECF1` is slightly darker than ours, so
  contrast is slightly higher).
- No disabled states exist in the app today, so §9.4 is guidance, not a measured pair.
- `Sage/Amber/Sky/Violet` are prescribed in `RESEARCH-ortu-ui.md` but not wired into the theme
  files. Their dark values are Ortu's; their light values are derived here and will need the
  same review if the owner ever re-pins a different light palette.
