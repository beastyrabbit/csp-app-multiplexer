# CSP SUITE — DEFINITIVE IMPLEMENTATION SPECIFICATION

**Version 1.0 — final. This document is the single source of truth.**

Two Windows desktop applications, one visual identity, one literal `Theme.xaml`:

| App | Repo | Framework | Fate |
|---|---|---|---|
| **CSP Palette Companion** | `C:/projects/csp_color_palette_gen` | WPF, `net8.0-windows` | Refined in place |
| **CSP Mux** | `C:/projects/csp-app-multiplexer` | WinForms → **WPF**, `net8.0-windows` | Rewritten |

Every number in this document has been derived and summed. Where a number is load-bearing, the derivation is printed next to it. **The implementer must never have to invent a value.** If a value appears to be missing, it is a defect in this document — do not guess; the intended value is recorded in the section that owns the component.

---

## 0. GOVERNING DECISIONS

These resolve every conflict in the source material. They are binding and each states its reason.

| # | Question | **Decision** | Reason |
|---|---|---|---|
| **G1** | Cards or hairlines? | **Cards survive.** Grouping is carried by a filled `PanelBrush` surface with a 1px `BorderBrush` outline and radius 8 — not by a 1px divider on the window background. | Measured: replacing a card (2px border + 20px padding + 12px margin = 34) with a hairline band (1px rule + 16px gap + 12px gap = 29) saves **5px per card**. The entire compaction budget comes from copy deletion, control relocation, and the status-card collapse — not from removing card fills. A `#434750` outline on a `#24262B` fill survives a bad monitor; a 1.3:1 hairline disappears. We do not pay a robustness cost for a 5px gain. |
| **G2** | Shared background family | **The Companion's neutral wins.** `WindowBrush #1C1D21`, `PanelBrush #24262B`. The Mux's blue-black `#0B0E14`/`#141923` is deleted. | The Companion is "refinement not reinvention"; the Mux is rewritten from zero and absorbs it at zero cost. A colour tool must not sit on blue-cast chrome that tints the swatches beside it. |
| **G3** | Accent | **`#72D2B1` (mint), unchanged.** The Mux's indigo `#6F69F5` and its whole accent family are deleted. | User decision, already made. |
| **G4** | `Success #46D39A` | **Deleted. Accent *is* success.** No `SuccessBrush` key exists, not even as an alias. | Same hue as the accent at nearly the same value; indistinguishable at 8px dot size, so a separate token would carry no information. The Companion already resolved this (`SetSuccess` used `AccentBrush`). An alias key would invite a site to "use the right one" and drift. |
| **G5** | `Info #69A7FF` | **Deleted.** "Connecting" and "Scanning" both map to `WarningBrush` ("working on it"). | Its only consumer was the deleted `SettingsSaved` state. Three semantic hues is the whole set. |
| **G6** | Window corners & shadow | **No in-app rounding, no in-app border, no `DropShadowEffect`, no `AllowsTransparency`.** `WindowChrome.CornerRadius="0"`. On `SourceInitialized`, call `DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE=33, DWMWCP_ROUND=2)`. | Real OS rounding and the real hardware-accelerated OS shadow, identical in both apps, zero paint code. `AllowsTransparency="True"` forces software rendering and downgrades ClearType to greyscale AA — unacceptable at 11px. Deleting the Companion's outer 1px `Border` also fixes the 1px caption phase error (C12), and it removes the Mux's clipped-stripe and jagged-corner defects (M2, M3) by construction. On Windows 10 the call is a documented no-op and the window is square — acceptable and stated. |
| **G7** | Window size | **Both apps: `460 × 620`, `ResizeMode="NoResize"`.** | Identical width *and* height is the strongest available suite signal for two windows that sit side by side next to CSP. 620 is also a compatibility fix: at 1920×1080 @ 150 % the desktop is 1280×720 DIP and ≈680 DIP after the taskbar. The Companion's current `MinHeight=MaxHeight=700, NoResize` **does not fit that display and cannot be resized**. 620 fits with 60 DIP to spare. |
| **G8** | Mux settings surface | **In-window view swap, identical to the Companion's.** `SettingsForm.cs` is deleted; its controls become a `SettingsView` `Grid` inside `MainWindow.xaml`. | One settings pattern is required. The Companion's already handles Esc, focus restore, and the hidden-vs-disabled gear question. A modal would need a second themed `Window`, a second `WindowChrome`, a second drag region, and a second `ComboBox` template. The Mux's existing modal is already broken: `BorderlessForm` hardcodes the caption rule `X < Width − 110` against a 580px form, leaving a 56px dead strip on the 520px dialog. |
| **G9** | Mux commit model | **Save-on-change.** Save/Cancel deleted. | One pattern. Two reversible settings need no transaction. This structurally deletes the `SettingsSaved` state and all three of its strings. |
| **G10** | `Theme.xaml` sharing | **A third git repository, `csp-suite-theme`, added to both app repos as a git submodule at `external/csp-suite-theme`.** Each app `ProjectReference`s the path *inside its own working tree*. Build action `Page`. | A cross-repo `ProjectReference` (`..\..\..\csp-app-multiplexer\...`) is exactly as path-fragile as the `<Resource Link>` it was supposed to replace, and it makes each repo unbuildable on a clean clone. A submodule makes each repo clone-and-build standalone (`git clone --recurse-submodules`) with one literal file. NuGet was rejected: it adds a publish step and a version axis for a two-app suite with no external consumers. |
| **G11** | Fonts | Two families. `UiFontFamily` = `Segoe UI Variable Text, Segoe UI` for 13px and 15px. **`UiFontSmallFamily` = `Segoe UI Variable Small, Segoe UI Variable Text, Segoe UI` for 11px.** | 11px is below the optical range of the *Text* face; the *Small* face is the correct optical size and is what stops 11px strings rendering with over-thick stems. Claiming "nothing renders below 12px" and then shipping three 11px styles is not a solution. |
| **G12** | Weight | **`Normal` and `SemiBold` only. `Bold` is banned suite-wide.** | Bold-vs-SemiBold at equal size is the single most visible "two different apps" tell. |
| **G13** | Motion | **Two animations exist in the entire suite:** the toggle-switch thumb translate (140 ms, `CubicEase EaseOut`) and the indeterminate progress sliver (1.4 s linear, gated on visibility). **Nothing else animates.** Hover and pressed are instant setter swaps. The `MainView` ↔ `SettingsView` swap is an instant `Visibility` change. | A `ColorAnimation` on a `{StaticResource}` brush mutates the **shared singleton** in the dictionary — hovering one button would recolour every element in both apps that references that key, and it throws outright if the dictionary is ever frozen. Avoiding this needs a private unfrozen brush in every one of ~12 templates. Instant hover feedback is also *correct*: a 140 ms lag on hover reads as lag, not polish. A cross-fade between two `Grid`s in one cell composites both text stacks on top of each other unless sequenced with `Completed` handlers; not worth it. |
| **G14** | Disabled state | **Explicit brushes, never `Opacity`** — with exactly one sanctioned exception (the palette swatch, §2.14, whose entire content *is* a colour). | Today five different opacities exist (0.4 / 0.42 / 0.45 / 0.5 / 0.55) and at least one place multiplies them (`ComboBox` 0.5 × templated `ToggleButton` 0.45 = **22.5 %**). Opacity also cannot be made to pass a contrast threshold reliably: `MutedBrush` at 0.55 over `PanelBrush` lands at 3.2:1, which fails AA for 13px text. `DisabledTextBrush` is picked to clear **4.68:1** (§1.2). |
| **G15** | Disabled-with-explanation | `ToolTipService.ShowOnDisabled="True"` is **mandatory** on `SegmentRadioStyle`, `ToggleSwitchStyle`, `ButtonStyle`, `CompactButtonStyle` and the `ComboBox` style. | `UpdateSourceTooltips()` writes the "turn on X in Settings" unlock instructions onto **disabled** radios. Without this setter those explanations are invisible exactly when they matter, and the greyed control reads as broken with no recourse. Paired with G14's 4.68:1 disabled text, the control is now legible enough to hover deliberately. |
| **G16** | Focus ring geometry | A 2px `Border` drawn as the **topmost child inside the chrome**, `Margin="0"`, `CornerRadius` **equal to the chrome radius**, `BorderThickness="2"`. | The ring's outer edge coincides exactly with the chrome's outer edge, so no fill can peek at the corners and no radius arithmetic is needed. This fixes C10 (seven templates currently draw the ring with the outer radius at an inset, producing a visibly wrong curve) without introducing the corner-peek that a `radius − 2` inset-0 ring would create. Triggers use **`IsKeyboardFocused` / `IsKeyboardFocusWithin`**, never `IsFocused` — a mouse click must not draw a ring. Every template sets `FocusVisualStyle="{x:Null}"`. |
| **G17** | Elevation | **There is none.** No `DropShadowEffect` anywhere. The `ComboBox` popup, the `ToolTip`, and every card are separated by a 1px `BorderBrush` outline only. The only shadow in either app is the OS window shadow from G6. | |

---

# 1. DESIGN TOKENS

All tokens live in **`external/csp-suite-theme/src/CspSuite.Theme/Theme.xaml`**, merged into `Application.Resources` in both apps.

## 1.1 Resource keys preserved verbatim — HARD RUNTIME CONTRACT

`MainWindow.xaml.cs` resolves these five keys through `FindResource` with a **hard cast**. Renaming, deleting, or changing the type of any one of them is a runtime `ResourceReferenceKeyNotFoundException` or `InvalidCastException` at first use — **not a compile error**. Verified against the shipped source; these are the complete set:

| Key | Required runtime type | Call sites in `MainWindow.xaml.cs` |
|---|---|---|
| **`AccentBrush`** | `SolidColorBrush` | 124, 518, 534 |
| **`WarningBrush`** | `SolidColorBrush` | 139, 144, 232 |
| **`ErrorBrush`** | `SolidColorBrush` | 155, 159, 169, 526 |
| **`BorderBrush`** | `SolidColorBrush` | 438 (every runtime swatch) |
| **`SwatchButtonStyle`** | `Style`, `TargetType="Button"` | 441 (every runtime swatch) |

After the code changes in §4.7 these three additional keys become `FindResource` targets and join the contract:

| Key | Type | Introduced by |
|---|---|---|
| **`SubtleBrush`** | `SolidColorBrush` | `ApplyStatusTone(StatusTone.Neutral)` |
| **`PanelBrush`** | `SolidColorBrush` | `ApplyStatusTone(StatusTone.Neutral)` |
| **`AccentStatusBrush`**, **`WarningStatusBrush`**, **`ErrorStatusBrush`** | `SolidColorBrush` | `ApplyStatusTone` |

**Compile-time keys referenced by `StaticResource` from `MainWindow.xaml`.** A missing key here is a XAML parse failure at window load. All of these must exist in `Theme.xaml`:

`AppWindowStyle`, `WindowBrush`, `PanelBrush`, `SurfaceBrush`, `RaisedBrush`, `PressedBrush`, `BorderBrush`, `BorderHoverBrush`, `DividerBrush`, `TextBrush`, `MutedBrush`, `SubtleBrush`, `DisabledTextBrush`, `DisabledFillBrush`, `DisabledBorderBrush`, `AccentBrush`, `AccentHoverBrush`, `AccentPressedBrush`, `AccentSurfaceBrush`, `AccentBorderBrush`, `AccentTextBrush`, `FocusBrush`, `WarningBrush`, `WarningStatusBrush`, `ErrorBrush`, `ErrorStatusBrush`, `AccentStatusBrush`, `CloseHoverBrush`, `ClosePressedBrush`, `QrPaperBrush`, `QrModuleBrush`, `TitleTextStyle`, `BodyTextStyle`, `BodyStrongTextStyle`, `LabelTextStyle`, `CaptionTextStyle`, `AccentLabelStyle`, `OnAccentTextStyle`, `FieldLabelStyle`, `CardStyle`, `StripStyle`, `PrimaryButtonStyle`, `ButtonStyle`, `SecondaryButtonStyle`, `CompactButtonStyle`, `LinkButtonStyle`, `TitleButtonStyle`, `CloseButtonStyle`, `TitleToggleStyle`, `SegmentRadioStyle`, `StepperFrameStyle`, `StepperButtonStyle`, `ToggleSwitchStyle`, `DragHandoffButtonStyle`, `SwatchButtonStyle`, `PillBadgeStyle`, `ScrollThumbStyle`, `ScrollPageButtonStyle`, `GhostSwatchStyle`, `QrFrameStyle`.

**Implicit (unkeyed) styles that must survive into `Theme.xaml`.** Dropping any of these silently breaks layout:

- `TextBlock` — sets **`TextWrapping="Wrap"` globally**. Drop it and every caption in the Companion becomes single-line and clips.
- `TextBox` — supplies the count boxes' height, centred content, `MinWidth`, caret and selection brushes.
- `ComboBox`, `ComboBoxItem` — full templates; the Aero default hardcodes light chrome a `Background` setter cannot override.
- `ProgressBar` — the whole indeterminate mechanism.
- `ScrollBar` — plus its `ScrollThumbStyle` and `ScrollPageButtonStyle`.
- `ToolTip`.
- `Window`-level `ToolTipService.InitialShowDelay` / `ShowDuration` are set per-window, not in the dictionary (§3.1).

**Removed keys.** These exist in the current `App.xaml` and are deleted; every consumer is retargeted in §4:
`SectionLabelStyle` (the "SOURCE" label it served is deleted), `QuietTextStyle` (→ `CaptionTextStyle`), `ComboBoxToggleStyle` (folded into the `ComboBox` template).

## 1.2 Colour tokens — complete

**Surfaces**

| Key | Hex | Use |
|---|---|---|
| `WindowBrush` | `#1C1D21` | Window background; title-bar background (**must be non-null** or `DragMove` misses the hit test) |
| `PanelBrush` | `#24262B` | Every card, every strip, the swatch tray, the combo popup, tooltips |
| `SurfaceBrush` | `#2D3036` | Resting fill of outlined controls (buttons, inputs, segments, stepper) |
| `RaisedBrush` | `#383B43` | Hover fill; toggle track (off) |
| `PressedBrush` | `#424650` | Pressed fill |
| `DisabledFillBrush` | `#212328` | Disabled control fill |

**Lines**

| Key | Hex | Use |
|---|---|---|
| `BorderBrush` | `#434750` | Every 1px control and card outline; the runtime swatch border (`FindResource`) |
| `BorderHoverBrush` | `#5A5F6B` | Outline under hover |
| `DividerBrush` | `#33363D` | Internal separators only: title-bar underline, permission-row rules, stepper internal hairlines, QR ghost frame. **Never a control outline.** Raised from the winning design's `#2E3138` (≈1.3:1) to ≈1.6:1 because it is the only thing separating three permission rows inside one card. |
| `DisabledBorderBrush` | `#33363D` | Disabled control outline (same value as `DividerBrush`, separate key so either can move independently) |

**Text** — contrast measured against `PanelBrush #24262B` (relative luminance 0.01933)

| Key | Hex | Contrast | Use |
|---|---|---|---|
| `TextBrush` | `#F2F3F6` | 14.5 : 1 | T1 / T2 / T2s |
| `MutedBrush` | `#ADB1BA` | **7.05 : 1** | T3 / T4 — labels and captions |
| `SubtleBrush` | `#9AA0AA` | **5.76 : 1** | Placeholder text, path strings, the idle status dot |
| `DisabledTextBrush` | `#8B8F99` | **4.68 : 1** | All disabled text and glyphs. **Passes WCAG AA for normal text**, so the disabled-source unlock tooltips are readable and the greyed controls no longer read as broken. (Today's disabled segment label resolves to ≈2.3 : 1.) |

The ramp is strictly monotonic: 14.5 > 7.05 > 5.76 > 4.68. `SubtleBrush` was raised from `#818690` specifically so it stays brighter than the disabled brush.

**Accent**

| Key | Hex | Use |
|---|---|---|
| `AccentBrush` | `#72D2B1` | Primary fill; connected/online dot; success tone; selected segment text. **Also carries "success" — there is no `SuccessBrush`.** |
| `AccentHoverBrush` | `#86DEC1` | Primary button hover |
| `AccentPressedBrush` | `#5DB796` | Primary button pressed |
| `AccentSurfaceBrush` | `#24473D` | Checked segment fill; selected combo item; pin-on fill; drag-chip tile |
| `AccentBorderBrush` | `#3E6D5E` | Outline of any accent-tinted surface |
| `AccentTextBrush` | `#10271F` | Text **on** accent fill (10.9 : 1 on `AccentBrush`); toggle thumb when on |
| `FocusBrush` | `#A8E8D1` | The 2px focus ring on every dark fill |

**Semantic — three hues, no more**

| Meaning | Dot / outline | Status-strip fill |
|---|---|---|
| Good / connected / online / done | `AccentBrush` `#72D2B1` | `AccentStatusBrush` `#1F2E2A` |
| Busy / waiting / scanning / connecting | `WarningBrush` `#D8A25E` | `WarningStatusBrush` `#2E271D` |
| Failed | `ErrorBrush` `#F08078` | `ErrorStatusBrush` `#2E2222` |
| Idle / neutral | `SubtleBrush` `#9AA0AA` (dot), `BorderBrush` (outline) | `PanelBrush` `#24262B` |

The status fills are deliberately low-saturation: at a 100px strip a saturated tint reads as an alert, which is wrong for "extraction succeeded".

**Close button** (its own hue by convention, not a semantic token)

| Key | Hex |
|---|---|
| `CloseHoverBrush` | `#C42B1C` |
| `ClosePressedBrush` | `#A22318` |

**QR** (Mux only) — a machine-readable target is a function, not a taste decision

| Key | Hex | Note |
|---|---|---|
| `QrPaperBrush` | `#F2F4F1` | Quiet-zone paper. Warm off-white, not `#FFFFFF` — a pure-white slab is the loudest object in a `#1C1D21` window. Luminance 0.876. |
| `QrModuleBrush` | `#101216` | Dark modules. Luminance 0.0055. **Contrast 16.5 : 1** — decoders need ≈3 : 1, so this is 5× margin. |

**Deleted from the Mux theme, not ported:** `WindowBackground`, `CardBackground`, `CardRaised`, `CardHover`, `Border`, `PrimaryText`, `SecondaryText`, `MutedText`, `DarkText`, `DarkMutedText`, `Accent`, `AccentHover`, `AccentLight`, `AccentMuted`, `AccentBorder` (already dead code — grep confirms zero references), `Success`, `Info`, `Danger`, `Warning`. Nineteen tokens, all replaced by the above.

## 1.3 Type ramp — seven steps, three sizes, two weights

Root `Window` (both apps, via `AppWindowStyle`): `TextOptions.TextFormattingMode="Display"`, `TextOptions.TextRenderingMode="ClearType"`, `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`.

> **`Display` formatting is deliberate and is the right call here.** It quantises glyph advances to whole pixels, which is what keeps an 11px caption's stem weights even. The alternative (`Ideal`) is better for large display type and worse at 11–13px, which is the entire ramp. This matches what the Companion already sets, and both apps must set it identically.

**Every wrapping style carries `LineStackingStrategy="BlockLineHeight"`.** This is not optional: every fixed text-block height in this document is an exact multiple of a `LineHeight`, and without `BlockLineHeight` WPF uses `MaxHeight` line boxes whose height depends on the installed font's ascent/descent, so the arithmetic stops being true.

| Key | Size | Weight | LineHeight | Family | Foreground | Used for |
|---|---|---|---|---|---|---|
| **`TitleTextStyle`** (T1) | 15 | SemiBold | 20 | `UiFontFamily` | `TextBrush` | The word "Settings" on the settings page of both apps. **Nothing else.** |
| **`BodyTextStyle`** (T2) | 13 | Normal | 18 | `UiFontFamily` | `TextBrush` | `TextBox` content, `ComboBox` content and items, `SettingsPathText` |
| **`BodyStrongTextStyle`** (T2s) | 13 | SemiBold | 18 | `UiFontFamily` | `TextBrush` | **Title-bar wordmark**, `StatusText`, `ConnectionHeading`, all button labels on `SurfaceBrush`, permission-row titles, drag-chip label, settings group titles |
| **`LabelTextStyle`** (T3) | 11 | SemiBold | 15 | `UiFontSmallFamily` | `MutedBrush` | Segment labels, `ConnectionText` |
| **`CaptionTextStyle`** (T4) | 11 | Normal | **16** | `UiFontSmallFamily` | `MutedBrush` | `DetailText`, `SourceHelp`, `ConnectionInstructions`, `ActionStatusText`, `AboutText`, `SettingsNoticeText`, every permission caption, all tooltip body text |
| **`AccentLabelStyle`** (T5) | 11 | SemiBold | 15 | `UiFontSmallFamily` | `AccentBrush` | The "DRAG" tag; the Mux client-count pill |
| **`OnAccentTextStyle`** (T6) | 13 | SemiBold | 18 | `UiFontFamily` | `AccentTextBrush` | The primary button label only |

Plus one non-`TextBlock` style, because `MajorLabel`/`MinorLabel` are `Label` elements carrying `Target` bindings and a `TargetType="TextBlock"` style cannot be applied to them:

| Key | TargetType | Spec |
|---|---|---|
| **`FieldLabelStyle`** | `Label` | `FontFamily={UiFontSmallFamily}`, `FontSize=11`, `FontWeight=SemiBold`, `Foreground={MutedBrush}`, `Padding=0`, `VerticalAlignment=Center`, `VerticalContentAlignment=Center` |

**`CaptionTextStyle` keeps its existing key name** — `MainWindow.xaml` references it as a `StaticResource` and the `DetailText` height arithmetic is documented against its `LineHeight`. **Its `LineHeight` changes 15 → 16.** Every dependent height in §4 is recomputed against 16.

**Deleted:** every 10px string (all are copy this document cuts), every 12px, 14px, 16px, and 18px combination, the whole Mux point ladder (8 / 9 / 9.5 / 11 / 13 / 15 / 16 / 18 pt), and all three `Bold` weights. **41 combinations across the suite → 8.**

**T1 vs the wordmark.** The wordmark is T2s (13/SemiBold), not T1. On the settings page the wordmark ("CSP Palette Companion", title bar) and the page title ("Settings", content area) are 40px apart; giving them the same size would collapse the hierarchy at the first place the eye lands. T1 exists so the page title outranks everything in the content area.

## 1.4 Spacing scale — five steps plus a hairline

| Token | px | Meaning |
|---|---|---|
| `hairline` | **1** | Separators only. Never a gap. |
| `xs` | **4** | Title → its caption; segment inter-gap; swatch gutter; label → its field (vertical) |
| `sm` | **8** | Dot → text; card internal padding (vertical); control → control in a row; divider clearance |
| `md` | **12** | Block → block; card padding (horizontal is 10, see below); card → card |
| `lg` | **16** | Page gutter, left and right, both apps |
| `xl` | **24** | Reserved. Unused in v1. |

**Card padding is `10,8`** (horizontal 10, vertical 8) rather than a uniform token. This is the one deliberate deviation and it is documented here so it is not "fixed" later: a card sitting inside a 16px page gutter already has 16px of air on its outside; 10px inside gives an optical inset of 26 without wasting a fourth of the content width. Vertical padding is `sm` = 8 on the scale.

**Deleted magic numbers.** The current XAML's `17` (someone's 8px dot + 9px gap, hardcoded in five places) is gone: the status strip's text indent is now `dot(8) + sm(8) = 16`, produced by a grid column rather than typed. The current `3 / 5 / 6 / 7 / 9 / 11 / 14 / 20 / 30` and the Mux's `18 / 19 / 22 / 24 / 28 / 33 / 38 / 43 / 47` are all gone. **≈24 scalars → 5.**

**The one exception, deliberately:** the tooltip's vertical padding is **6**, not 8 (`Padding="8,6"`). A tooltip is a 1-to-2-line floating box; 8px vertical makes it look inflated. This is called out rather than smuggled in as "xs+2".

## 1.5 Radii — three values plus pill

| Token | px | Applies to |
|---|---|---|
| `RadiusCard` | **8** | Cards, strips, the swatch tray, the QR frame, the combo popup |
| `RadiusControl` | **6** | Every button, text box, segment, combo (closed), stepper frame, drag chip, tooltip, title-bar button |
| `RadiusSmall` | **3** | Swatches, ghost swatches, combo popup items, the drag-chip accent tile |
| `RadiusPill` | height ÷ 2 | Toggle track (10), client-count pill (10) |

Window radius belongs to DWM (G6) and is not a token. **16 radii → 3 + pill.** Focus rings use the chrome radius exactly (G16).

## 1.6 Control heights — three values plus four named specials

| Token | px | Controls |
|---|---|---|
| `HeightCompact` | **28** | Title-bar buttons (28×28), `BackButton`, `ConnectButton`, `CompactButtonStyle`, `LinkButtonStyle`, `RefreshActionsButton`, `SetupGuideButton`, `OpenPaletteButton`, `ShowSettingsFileButton` |
| `HeightDefault` | **32** | `TextBox`, `ComboBox`, segment radios, stepper frame, stepper buttons, `SecondaryButtonStyle` |
| `HeightPrimary` | **40** | `ExtractButton`, the Mux primary button, the Mux cancel/stop button. **The only 40px controls in either app — this is how you find the primary action.** |

Named specials, each stated once so nobody hunts for it:

| Element | Size |
|---|---|
| Toggle switch track | **36 × 20** (focus box 44 × 28) |
| Client-count pill | height **20** |
| Drag chip | height **36** |
| Palette swatch | **32 × 32** |

**10 heights → 3 + 4.**

---

# 2. CONTROL SPECIFICATIONS

Universal rules, applied to every template below:

- `FocusVisualStyle="{x:Null}"`.
- Focus ring per G16: 2px `Border` named `FocusRing`, topmost child inside `Chrome`, `Margin="0"`, `CornerRadius` = chrome radius, `BorderBrush="{StaticResource FocusBrush}"` (or `AccentTextBrush` on accent fills), `Visibility="Collapsed"` by default, made `Visible` by a trigger on `IsKeyboardFocused` (or `IsKeyboardFocusWithin` for composites).
- Disabled: `Cursor="Arrow"`, and the explicit disabled brushes from §1.2. **No `Opacity`.**
- Every state change is instant (G13).

## 2.1 `PrimaryButtonStyle`

| Property | Value |
|---|---|
| Height | 40 |
| Padding | `16,0` · MinWidth 96 |
| CornerRadius | 6 |
| BorderThickness | 0 |
| Content style | T6 (`OnAccentTextStyle`), centred, `TextTrimming="CharacterEllipsis"` |

| State | Fill | Ring |
|---|---|---|
| Rest | `AccentBrush` | — |
| Hover | `AccentHoverBrush` | — |
| Pressed | `AccentPressedBrush` | — |
| Disabled | `DisabledFillBrush`, 1px `DisabledBorderBrush`, text `DisabledTextBrush` | — |
| Keyboard focus | unchanged | 2px `AccentTextBrush` |

Consumers: `ExtractButton` (keeps `IsDefault="True"`), Mux `PrimaryButton`.

## 2.2 `ButtonStyle` (implicit + keyed) and `SecondaryButtonStyle`

| Property | Value |
|---|---|
| Height | 32 |
| Padding | `12,0` · MinWidth 76 |
| CornerRadius | 6 |
| Border | 1px |
| Content style | T2s |

| State | Fill | Outline | Text |
|---|---|---|---|
| Rest | `SurfaceBrush` | `BorderBrush` | `TextBrush` |
| Hover | `RaisedBrush` | `BorderHoverBrush` | `TextBrush` |
| Pressed | `PressedBrush` | `BorderHoverBrush` | `TextBrush` |
| Disabled | `DisabledFillBrush` | `DisabledBorderBrush` | `DisabledTextBrush` |
| Focus | unchanged | unchanged | + 2px `FocusBrush` ring |

`SecondaryButtonStyle` is `ButtonStyle` with no additional setters. **It does not set `HorizontalAlignment`** — the current style bakes `Left` into a visual style, which makes the secondary *look* unusable for a full-width button. Alignment is a call-site decision.

Carries `ToolTipService.ShowOnDisabled="True"` (G15).

## 2.3 `CompactButtonStyle` and `LinkButtonStyle`

`CompactButtonStyle`: identical to `ButtonStyle` at **height 28**, `Padding="10,0"`, `MinWidth="72"`. Consumers: `ConnectButton`, `RefreshActionsButton`, `SetupGuideButton`.

`LinkButtonStyle`: height **28**, `Padding="6,0"`, `CornerRadius="6"`, `Background="Transparent"`, `BorderThickness="0"`, content **T4 coloured `AccentBrush`**.

| State | Treatment |
|---|---|
| Rest | text `AccentBrush` |
| Hover | fill `SurfaceBrush`, text `AccentHoverBrush` |
| Pressed | fill `PressedBrush` |
| Disabled | text `DisabledTextBrush` |
| Focus | + 2px `FocusBrush` ring, radius 6 |

Consumers: `OpenPaletteButton`, `ShowSettingsFileButton`.

## 2.4 Title-bar buttons — `TitleButtonStyle`, `CloseButtonStyle`, `TitleToggleStyle`

**28 × 28**, `CornerRadius="6"`, `Background="Transparent"`, `BorderThickness="0"`, containing a `ContentPresenter` centred in a 12 × 12 box.

| State | Fill | `Foreground` (drives the glyph) |
|---|---|---|
| Rest | `Transparent` | `MutedBrush` |
| Hover | `SurfaceBrush` | `TextBrush` |
| Pressed | `PressedBrush` | `TextBrush` |
| Disabled | `Transparent` | `DisabledTextBrush` |
| **`CloseButtonStyle`** hover | `CloseHoverBrush` | `#FFFFFF` |
| **`CloseButtonStyle`** pressed | `ClosePressedBrush` | `#FFFFFF` |
| **`TitleToggleStyle`** `IsChecked=True` | `AccentSurfaceBrush` | `AccentBrush` |
| Focus | unchanged | + 2px `FocusBrush` ring, radius 6 |

**Glyphs are `Path` geometry, never text.** The current app uses four different glyph sources (an em dash, U+2715, U+2699, and Segoe MDL2 `&#xE718;`) at three optical weights; the Mux draws two of them with `DrawLine` in `OnPaint`. All are replaced.

**Mechanism — how a per-instance glyph reaches a shared template.** The template keeps its `ContentPresenter`. Each call site supplies an inline `Path` whose stroke/fill binds to the templated parent's `Foreground`, which the template's state triggers drive:

```xml
<Button Style="{StaticResource TitleButtonStyle}"
        shell:WindowChrome.IsHitTestVisibleInChrome="True"
        ToolTip="Minimize" Click="MinimizeButton_Click">
  <Path Width="12" Height="12" Stretch="None" SnapsToDevicePixels="True"
        StrokeThickness="1" StrokeStartLineCap="Round" StrokeEndLineCap="Round"
        Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}"
        Data="M 1,6.5 L 11,6.5"/>
</Button>
```

**Glyph geometry — 12 × 12 box, all coordinates within `[1, 11]` so nothing clips, all axis-aligned strokes on half-pixels so a 1px stroke lands on exactly one device row/column:**

| Glyph | `Data` | Stroke / Fill |
|---|---|---|
| Minimize | `M 1,6.5 L 11,6.5` | Stroke |
| Close | `M 1.5,1.5 L 10.5,10.5 M 10.5,1.5 L 1.5,10.5` | Stroke |
| **Settings** (sliders) | `M 1,3.5 L 11,3.5 M 1,8.5 L 11,8.5` plus `M 8.75,3.5 A 1.75,1.75 0 1 1 8.749,3.5 Z` and `M 6.25,8.5 A 1.75,1.75 0 1 1 6.249,8.5 Z` | lines Stroke, dots **Fill** |
| Back | `M 7.5,2 L 3.5,6 L 7.5,10` | Stroke |
| Pin | `M 6.5,6.5 L 6.5,11 M 3,2.5 L 10,2.5` plus `M 4.5,2.5 L 5,6.5 L 8,6.5 L 8.5,2.5 Z` | lines Stroke, body **Fill** |
| Stepper minus | `M 2,6.5 L 10,6.5` | Stroke |
| Stepper plus | `M 2,6.5 L 10,6.5 M 6.5,2 L 6.5,10` | Stroke |
| Combo chevron | `M 2,4.5 L 6,8.5 L 10,4.5` | Stroke |
| Drag arrow | `M 3,9 L 9,3 M 4.5,3 L 9,3 L 9,7.5` | Stroke |

> **Why sliders and not a gear.** A six-tooth gear at 12px, stroked at 1px, is ~24 path segments across 12 device pixels — teeth ~1px wide with ~1px gaps, which renders as a grey annulus with texture noise. Windows itself uses filled gears at this size. Two lines and two filled dots are unambiguous at 12px, snap to the pixel grid, and the button's tooltip already reads "Settings".

**Every interactive title-bar element carries `shell:WindowChrome.IsHitTestVisibleInChrome="True"`.** Omitting it makes the control unclickable — the chrome swallows the click as a caption drag. **Non-interactive title-bar content (`ConnectionDot`, `ConnectionText`, the wordmark) must NOT carry it** — that is exactly the defect that currently punches a 133px dead hole in the Companion's drag bar.

## 2.5 `ToggleSwitchStyle` (`CheckBox`)

| Part | Spec |
|---|---|
| Focus box | 44 × 28, `CornerRadius="14"` (the ring is drawn here, not on the track, so a 2px pill ring never eats the thumb) |
| Track | **36 × 20**, `CornerRadius="10"`, centred in the focus box |
| Thumb | **16 × 16**, `CornerRadius="8"`, `HorizontalAlignment="Left"`, `Margin="2,0,0,0"` |

| State | Track fill | Track outline | Thumb |
|---|---|---|---|
| Off | `RaisedBrush` | 1px `BorderBrush` | `MutedBrush` |
| Off hover | `PressedBrush` | 1px `BorderHoverBrush` | `TextBrush` |
| On | `AccentBrush` | 1px `AccentBorderBrush` | `AccentTextBrush` |
| On hover | `AccentHoverBrush` | 1px `AccentBorderBrush` | `AccentTextBrush` |
| Disabled | `DisabledFillBrush` | 1px `DisabledBorderBrush` | `DisabledTextBrush` |
| Focus | unchanged | unchanged | + 2px `FocusBrush` ring on the **focus box**, radius 14 |

**Animation — the one required storyboard edit.** `ThumbShift.X` animates **`0 → 16`**, 140 ms, `CubicEase{EasingMode=EaseOut}`.
Derivation: `36 (track) − 2 (left margin) − 16 (thumb) − 2 (right margin) = 16`.
The current value is `20`, correct for the old 44 × 24 track with an 18px thumb. **Leaving it at 20 puts the thumb 4px past the track edge.**

Carries `ToolTipService.ShowOnDisabled="True"`. Label sits right of the focus box at `sm` = 8, style T2s.

All three Companion permission toggles stay wired to **`Click`**, never `Checked`/`Unchecked` — programmatic `IsChecked` assignment in `ApplySettingsToUi` must not re-enter the save path.

## 2.6 `SegmentRadioStyle` (`RadioButton`)

| Property | Value |
|---|---|
| Height | 32 |
| CornerRadius | 6 |
| Padding | `8,0` |
| Content style | T3, `HorizontalAlignment="Center"`, `TextTrimming="CharacterEllipsis"`, `TextWrapping="NoWrap"` |

| State | Fill | Outline | Text |
|---|---|---|---|
| Off | `SurfaceBrush` | 1px `BorderBrush` | `MutedBrush` |
| Off hover | `RaisedBrush` | 1px `BorderHoverBrush` | `TextBrush` |
| **Checked** | `AccentSurfaceBrush` | 1px `AccentBorderBrush` | `AccentBrush` |
| Checked hover | `#2A5348` | 1px `AccentBrush` | `AccentBrush` |
| **Disabled, unchecked** | `DisabledFillBrush` | 1px `DisabledBorderBrush` | `DisabledTextBrush` |
| **Disabled, checked** | `DisabledFillBrush` | **2px `DisabledBorderBrush`** | `DisabledTextBrush` **at `FontWeight="SemiBold"` plus a 3px `DisabledTextBrush` dot** at the left of the label | 
| Focus | unchanged | unchanged | + 2px `FocusBrush` ring, radius 6 |

> **The disabled-and-checked state is reachable and must be distinguishable.** Turn off both Companion capture and Clipboard capture and all four radios disable with one still `IsChecked` (`ApplySettingsToUi` only re-selects if `CanvasSource` or `LayerSource` is enabled). Without the thicker outline and the dot, the user sees four identical grey boxes and no indication of what will be extracted. Implement as a `MultiTrigger` on `IsEnabled=False` + `IsChecked=True`, declared **after** the single-condition disabled trigger so it wins.

**`<Setter Property="ToolTipService.ShowOnDisabled" Value="True"/>` is mandatory** (G15).

Weight never changes between checked and unchecked in the *enabled* states — a weight change reflows the label and shifts the whole row.

## 2.7 Stepper — `StepperFrameStyle` + `StepperButtonStyle` + implicit `TextBox`

**One control with three zones, not three controls.**

Container: `Border` with `StepperFrameStyle` — **100 × 32**, `CornerRadius="6"`, `BorderThickness="1"`, `Background="{StaticResource SurfaceBrush}"`, **`ClipToBounds="True"`**.

Internal `Grid`, `ColumnDefinitions="28,1,42,1,28"` — total **28 + 1 + 42 + 1 + 28 = 100** ✓. Columns 1 and 3 are 1px `DividerBrush` `Rectangle`s at full height.

| Zone | Spec |
|---|---|
| **Buttons** (cols 0, 4) — `StepperButtonStyle` | 28 × 32, `CornerRadius="0"` (the frame clips the outer corners), `BorderThickness="0"`, `Background="Transparent"`, glyph `Path` per §2.4. Hover fill `RaisedBrush`; pressed `PressedBrush`; disabled glyph `DisabledTextBrush` with **no fill change** |
| **Field** (col 2) — `MajorCount` / `MinorCount` | **Must remain `TextBox`.** `Background="Transparent"`, `BorderThickness="0"`, `TextAlignment="Center"`, style T2, `MaxLength="2"`, `Padding="0"`, `CaretBrush="{StaticResource AccentBrush}"`, `SelectionBrush="{StaticResource AccentSurfaceBrush}"`, `SelectionOpacity="1"` |

| Frame state | Outline | Fill |
|---|---|---|
| Rest | 1px `BorderBrush` | `SurfaceBrush` |
| Hover anywhere (`IsMouseOver`) | 1px `BorderHoverBrush` | `SurfaceBrush` |
| Field keyboard-focused (`IsKeyboardFocusWithin`) | 1px `BorderHoverBrush` | `SurfaceBrush` + **2px `FocusBrush` ring, radius 6** |
| Disabled | 1px `DisabledBorderBrush` | `DisabledFillBrush` |

`StepperFrameStyle` carries these as `Style.Triggers` on the `Border` (it is a keyed `Style` `TargetType="Border"`, not a `ControlTemplate`; the ring is a sibling `Border` inside the frame's own `Grid`, toggled by the same triggers via `ElementName`).

**Hard contract:** `MajorCount` and `MinorCount` must remain `TextBox` instances. `ReferenceEquals(box, MajorCount)` in `RangeOf` (line 962) is the *sole* discriminator between the two ranges, and `ClampCountBox`, `TryReadCount`, `Adjust`, and `CountBox_PreviewKeyDown` all take or cast to `TextBox`.

`CountBox_MouseWheel` keeps `e.Handled = true`. Neither `MainView` is inside a `ScrollViewer`, so there is no wheel conflict — this is now a stated invariant, not an accident.

`UpdateStepperAvailability`'s `if (!IsInitialized) return;` guard **stays**. `CountBox_TextChanged` is wired on `MajorCount`, which XAML parses before `MinorCount`, so the guard is the only thing preventing a `NullReferenceException` during `InitializeComponent`. Nothing may call it before `EndInit`; `MainWindow_Loaded` keeps its explicit call.

## 2.8 `ComboBox` — full `ControlTemplate`, both closed state and popup

The Aero default hardcodes light chrome that a `Background` setter will not override. This is the largest single chunk of styling work in the port and it is what fixes the Mux's stock-light-dropdown defect.

**Closed**

| Property | Value |
|---|---|
| Height | 32 |
| CornerRadius | 6 |
| Border | 1px |
| Padding | `10,0,32,0` |
| Content | T2 |
| Chevron | `Path` per §2.4, `Stroke` bound to the templated `Foreground`, right inset 10, vertically centred |

| State | Fill | Outline | Text / chevron |
|---|---|---|---|
| Rest | `SurfaceBrush` | `BorderBrush` | `TextBrush` / `MutedBrush` |
| Hover | `RaisedBrush` | `BorderHoverBrush` | `TextBrush` / `TextBrush` |
| Open (`IsDropDownOpen`) | `RaisedBrush` | `AccentBorderBrush` | `TextBrush` / `TextBrush` |
| **Disabled** | `DisabledFillBrush` | `DisabledBorderBrush` | `DisabledTextBrush` / `DisabledTextBrush` |
| Focus | unchanged | unchanged | + 2px `FocusBrush` ring, radius 6 |

**`Opacity` is set nowhere in this template — not on the `ComboBox`, not on the templated `ToggleButton`.** That pair is what currently multiplies to 22.5 %.

**Popup**

`MaxDropDownHeight="224"` (7 × 32). `AllowsTransparency="False"`. Background `PanelBrush`, 1px `BorderBrush`, `CornerRadius="8"`, `Padding="4"`, `Margin="0,2,0,0"`. **No shadow** (G17).

**`ComboBoxItem`**

| Property | Value |
|---|---|
| Height | 32 |
| CornerRadius | 3 |
| Padding | `10,0` |
| Content | T2 |

| State | Fill | Text |
|---|---|---|
| Rest | `Transparent` | `TextBrush` |
| **`IsHighlighted="True"`** | `RaisedBrush` | `TextBrush` |
| `IsSelected="True"` | `AccentSurfaceBrush` | `AccentBrush` |
| `IsSelected` + `IsHighlighted` | `AccentSurfaceBrush` | `AccentBrush` |

> **`IsHighlighted`, not `IsMouseOver`.** WPF sets `IsHighlighted` when arrow-keying an open popup. A template that only triggers on `IsMouseOver` gives keyboard navigation of the Auto Action list **no visible selection at all** — and with a full custom template there is no Aero fallback.

Keeps `ToolTip="{Binding RelativeSource={RelativeSource Self}, Path=Content}"`.

**No `ItemTemplate`, ever.** `AutoActionPicker.ItemsSource` is a `QuickAccessActionOption[]` rendered through `ToString()`. Adding a template makes that `ToolTip` binding display an object reference.

**Placeholder overlay:** a `TextBlock` in the template, style T2 coloured `SubtleBrush`, `Visibility` driven by a `DataTrigger` on `SelectedItem == null`. Text supplied by the template: `"Select an action"`. A second overlay for `Items.Count == 0`: `"No actions loaded"`.

Carries `ToolTipService.ShowOnDisabled="True"`.

## 2.9 `CardStyle` and `StripStyle` (`Border`)

**`CardStyle`** — the grouping surface.

```
Background      = {StaticResource PanelBrush}
BorderBrush     = {StaticResource BorderBrush}
BorderThickness = 1
CornerRadius    = 8
Padding         = 10,8
```

**`StripStyle`** — a card whose `BorderBrush` and `Background` are written at runtime (the connection strip and the status strip). Same as `CardStyle` but with no `BorderBrush`/`Background` setters, so a local value or a code-behind assignment is never fighting a setter. Call sites set the initial values explicitly in XAML.

## 2.10 Status strip — `StatusPanel` (shared component, both apps)

Identical XAML in both apps. `Border` with `StripStyle`, `CornerRadius="8"`, `Padding="10,8"`, `BorderThickness="1"`.

Internal `Grid`, `RowDefinitions="28,4,50,Auto"`:

| Row | Content |
|---|---|
| **0 (28)** | `Grid ColumnDefinitions="8,8,*,Auto"` → **`StatusDot`** (`Ellipse` **8 × 8**, `VerticalAlignment="Center"`) · gap · **`StatusText`** (T2s, `VerticalAlignment="Center"`, `TextWrapping="NoWrap"`, `TextTrimming="CharacterEllipsis"`, `AutomationProperties.LiveSetting="Polite"`) · **trailing slot** |
| **1 (4)** | gap |
| **2 (50)** | **`DetailText`**, `Grid.Column` spanning from column 2 of an identical column set (indent 16 = 8 dot + 8 gap). T4, **`Height="50"`**, `TextWrapping="Wrap"`, `TextTrimming="CharacterEllipsis"`, `AutomationProperties.LiveSetting="Polite"` |
| **3 (Auto)** | **`BusyIndicator`** (`ProgressBar`), `Margin="16,8,0,0"`, `Height="3"`, `HorizontalAlignment="Stretch"`, `IsHitTestVisible="False"`, `Visibility="Collapsed"` |

**Trailing slot contents:** Companion → `OpenPaletteButton` (`LinkButtonStyle`, 28, `Collapsed` until a result). Mux → `ClientCountBadge` (`PillBadgeStyle`, 20).

**Heights, derived:**

```
border 1 + padding 8 + row0 28 + row1 4 + row2 50 + padding 8 + border 1 = 100   (idle / result)
+ row3 (margin 8 + bar 3 = 11)                                          = 111   (busy)
```

`StatusPanel` sits in an **`Auto`** grid row. The 11px growth during extraction is absorbed by the `*` swatch-tray row, which is empty at that moment (`ClearPaletteResult` ran before the first `SetProgress`), so the growth is invisible. **This is why the strip is not a fixed height** — a fixed 100 would clip the progress bar, and a fixed 111 would leave 11px of dead space in every other state.

**`DetailText Height="50"` derivation: 3 reserved lines × `CaptionTextStyle.LineHeight` 16 + 2 px slack = 50.**
The +2 is required: `UseLayoutRounding` snaps 48 down to ~47.8 at 125 % scaling and costs the third line. **If the reserved line count or the caption line height ever changes, recompute as `lines × LineHeight + 2`.**

> **`TextTrimming` does not ellipsise vertical overflow in WPF.** It applies per-line on *horizontal* overflow. A four-line string in a 50px box renders three lines and the fourth is **clipped, with no ellipsis**. `TextBlock` has no `MaxLines`. The real mechanism protecting this box is therefore the **copy budget**, not the trimming: the column is `428 − 2 (border) − 20 (padding) − 16 (indent) = 390 px`, which at 11px Segoe UI Variable Small is ≈**70 characters per line, ≈210 characters over three lines**. §6 guarantees every composed detail string fits. `SetStatus` also mirrors the full string into `DetailText.ToolTip` (existing code, line 543) and that mirror is **mandatory** as the last-resort recovery.

**Tone.** Written by `ApplyStatusTone` (§4.7), which sets `StatusDot.Fill`, `StatusPanel.BorderBrush`, and `StatusPanel.Background` together:

| Tone | Dot | Border | Background |
|---|---|---|---|
| `Neutral` | `SubtleBrush` | `BorderBrush` | `PanelBrush` |
| `Busy` | `WarningBrush` | `WarningBrush` | `WarningStatusBrush` |
| `Good` | `AccentBrush` | `AccentBrush` | `AccentStatusBrush` |
| `Bad` | `ErrorBrush` | `ErrorBrush` | `ErrorStatusBrush` |

A 1px coloured outline alone is too thin to read at this size; the low-saturation fill is what makes the state legible at a glance.

**`ProgressStageText` is deleted** — the element **and** the string-literal `switch` in `SetProgress` (lines 511–516) **and** the three `.Visibility` assignments at 517, 525, 533. Removing any subset is a `CS0103`.

## 2.11 `ProgressBar` (implicit) — `BusyIndicator`

| Property | Value |
|---|---|
| Height | 3 |
| Track | `Transparent`, `ClipToBounds="True"` |
| Sliver | `Rectangle`, **96 × 3**, `RadiusX/Y="1.5"`, `Fill="{StaticResource AccentBrush}"`, `HorizontalAlignment="Left"`, with a `TranslateTransform x:Name="SliverShift"` |

**Storyboard:** `SliverShift.X` **`From="-96" To="406"`**, `Duration="0:0:1.4"`, linear, `RepeatBehavior="Forever"`.

Derivation of `406`: the bar spans the status strip's inner width = `428 (content) − 2 (border) − 20 (padding) = 406`. At `X = 406` the sliver's left edge is at the track's right edge, so it is fully out. Travel = 502 px. **No overshoot, no dead beat** (the current template animates `−110 → 440` against a 375px bar, producing a visible stall every 1.5 s).

> **This is the one width-coupled constant in `Theme.xaml`.** Annotate it in the file: `<!-- 406 = 428 content - 2 border - 20 padding. Update with the status strip width. -->` Both apps use 428 content, so one value serves both.

**Gating — mandatory.** The `BusyIndicator` declaration in both apps carries:

```xml
IsIndeterminate="{Binding IsVisible, RelativeSource={RelativeSource Self}}"
```

and the template's `<Trigger Property="IsIndeterminate" Value="True">` uses `EnterActions`/`ExitActions` with `BeginStoryboard`/`StopStoryboard`. Today `IsIndeterminate="True"` is set once in XAML and never cleared, so a `RepeatBehavior="Forever"` storyboard runs on the composition thread for the entire process lifetime, including while collapsed, in a `Topmost` window.

## 2.12 `PillBadgeStyle` (`Border`) — Mux client count

Height **20**, `CornerRadius="10"`, `Padding="8,0"`, `MinWidth="52"`, `Background="{StaticResource AccentSurfaceBrush}"`, `BorderBrush="{StaticResource AccentBorderBrush}"`, `BorderThickness="1"`, content T5.

Zero-state (count = 0): `Background="{StaticResource SurfaceBrush}"`, `BorderBrush="{StaticResource BorderBrush}"`, text `MutedBrush`. Driven by a `DataTrigger` on the view-model's `ClientCount == 0`.

## 2.13 `DragHandoffButtonStyle` (`PaletteDragChip`)

| Property | Value |
|---|---|
| Height | 36 |
| CornerRadius | 6 |
| Border | 1px `AccentBorderBrush` |
| Fill | `AccentSurfaceBrush` |
| Padding | `10,0` |

Content (`Grid ColumnDefinitions="16,8,*,Auto"`):
1. 16 × 16 `Border`, `CornerRadius="3"`, `Background="{StaticResource AccentBrush}"`, containing the drag-arrow `Path` (§2.4) stroked `AccentTextBrush`.
2. `"Drop onto CSP Color Set"`, T2s, `VerticalAlignment="Center"`.
3. spacer.
4. `"DRAG"`, T5.

| State | Fill | Outline |
|---|---|---|
| Rest | `AccentSurfaceBrush` | `AccentBorderBrush` |
| Hover | `#2A5348` | `AccentBrush` |
| Pressed | `#1E3B33` | `AccentBrush` |
| **Dragging** (`Tag="dragging"`, set by `PaletteDragChip_MouseMove` before `DoDragDrop` and cleared after) | `#1E3B33` | 2px `AccentBrush` |
| Disabled | `DisabledFillBrush` | `DisabledBorderBrush` |
| Focus | unchanged | + 2px `FocusBrush` ring, radius 6 |
| Cursor | `Hand` at all enabled states |

> The dragging state is specified because this control's entire purpose is being dragged into another application and the current app gives zero feedback while the drag is in flight. `PaletteDragChip_MouseMove` sets `PaletteDragChip.Tag = "dragging"` immediately before `DragDrop.DoDragDrop(...)` and clears it to `null` on the line after (the call is blocking).

Must remain a `ButtonBase` — it needs `Click`, and it is both the `e.GetPosition` origin and the `DragDrop.DoDragDrop` source. 36 px is well above `SystemParameters.MinimumVerticalDragDistance` (typically 4).

## 2.14 `SwatchButtonStyle` — runtime-constructed, key in the hard contract

Runtime geometry (code-behind, `ShowPalette`): **32 × 32**, `Padding=0`, **`Margin = new Thickness(0, 0, 4, 4)`** (changed from 6 — see §4.7 edit 1), `Cursor=Hand`.

**Template — the single most fragile thing in the suite:**

```xml
<ControlTemplate TargetType="Button">
  <Grid>
    <Border x:Name="SwatchChrome" CornerRadius="3"
            Background="{TemplateBinding Background}"
            BorderBrush="{TemplateBinding BorderBrush}"
            BorderThickness="{TemplateBinding BorderThickness}"/>
    <Border x:Name="HoverRing"  CornerRadius="3" BorderThickness="2"
            BorderBrush="{StaticResource FocusBrush}" Visibility="Collapsed"/>
    <Border x:Name="FocusRing"  CornerRadius="3" BorderThickness="2"
            BorderBrush="{StaticResource FocusBrush}" Visibility="Collapsed"/>
  </Grid>
  ...
</ControlTemplate>
```

**`Background`, `BorderBrush` and `BorderThickness` must be `TemplateBinding`.** `ShowPalette` assigns all three as **local values**, which beat any style setter. A template that relies on setters renders 40 identical swatches and kills the entire feature.

| State | Treatment |
|---|---|
| Rest | colour fill + 1px `BorderBrush` (both runtime-assigned) |
| Hover | `HoverRing` visible — 2px `FocusBrush`, **drawn inside the 32px bounds** |
| Pressed | `HoverRing` visible with `BorderBrush = AccentPressedBrush` |
| Focus (`IsKeyboardFocused`) | `FocusRing` visible — 2px `FocusBrush`, inside bounds |
| **Disabled** | `SwatchChrome.Opacity = 0.5` — **the one sanctioned opacity in the suite**, because the control's entire content *is* a colour and there is no substitute brush. `PaletteSwatch_Click` disables the swatch for the duration of the async colour-set call. |

Rings stay strictly inside the 32px bounds so a highlighted swatch never overlaps a neighbour in the 4px-gutter `WrapPanel`.

`sender` is pattern-matched as `Button { Tag: RgbColor color }` — the swatch cannot become a `ToggleButton`, `Border`, or `Rectangle`.

## 2.15 `GhostSwatchStyle` (`Rectangle`) — the empty-state placeholder

`Width="32" Height="32" RadiusX="3" RadiusY="3" Fill="{StaticResource DisabledFillBrush}" Stroke="{StaticResource DividerBrush}" StrokeThickness="1" Margin="0,0,4,0"`.

Eleven of these in a horizontal `StackPanel` form `PalettePlaceholder` (§4.4). Wordless: it shows the exact size, radius, count-per-row and pitch of what is about to arrive, in 32px, replacing a two-line sentence that occupied 153px.

## 2.16 `ScrollBar` (implicit) + `ScrollThumbStyle` + `ScrollPageButtonStyle`

| Part | Spec |
|---|---|
| `ScrollBar` | `Width="8"`, `Background="Transparent"` |
| Arrow `RepeatButton`s (`ScrollPageButtonStyle`) | `Width="0" Height="0" Opacity="0" IsTabStop="False"`, empty template |
| Thumb (`ScrollThumbStyle`) | `Width="4"`, `Margin="2,0"`, `CornerRadius="2"`, `MinHeight="24"`, `Fill = BorderBrush` |
| Thumb hover | `BorderHoverBrush` |
| Thumb dragging | `MutedBrush` |

Every container that can scroll reserves **`Padding="0,0,8,0"`**, matching the bar width, so the bar never overhangs a card edge (the current settings `ScrollViewer` reserves 4 for a 9-wide bar and overhangs by 5).

## 2.17 `ToolTip` (implicit)

`Background="{StaticResource PanelBrush}"`, `BorderBrush="{StaticResource BorderBrush}"`, `BorderThickness="1"`, `CornerRadius="6"`, `Padding="8,6"`, content style T4 but **`Foreground="{StaticResource TextBrush}"`** (a tooltip is the primary surface for its own content), `MaxWidth="280"`, `TextWrapping="Wrap"`, `HasDropShadow="False"`, `Placement="Bottom"`, `VerticalOffset="4"`.

Window-level, set on both `Window` elements (inherited): `ToolTipService.InitialShowDelay="400"`, `ToolTipService.ShowDuration="20000"`. The 20 s duration stays — several code-set tooltips are full sentences.

## 2.18 `QrFrameStyle` (`Border`) — Mux only

**300 × 300**, `CornerRadius="8"`, `HorizontalAlignment="Center"`, `VerticalAlignment="Center"`.

| State | Background | Border | Content |
|---|---|---|---|
| **No code** (Idle, Scanning, Connecting, Failed, QrHidden) | `Transparent` | 1px `DividerBrush` | **empty** |
| **Code present** (Online) | `QrPaperBrush` | 0 | the `Image` (§5.5) |

An empty outlined square exactly the size of the incoming code is a slot, not a mystery — it is the ghost-swatch idea applied to the Mux. **No placeholder text, no icon, no watermark.** A decorative brand glyph parked in dead space is the same register requirement 4 deletes.

---

# 3. THE SHARED SHELL

Byte-identical XAML in both `MainWindow.xaml` files. Two strings differ.

## 3.1 Window

```xml
<Window ...
        Style="{StaticResource AppWindowStyle}"
        Width="460"  MinWidth="460"  MaxWidth="460"
        Height="620" MinHeight="620" MaxHeight="620"
        ResizeMode="NoResize"
        WindowStyle="None"
        WindowStartupLocation="Manual"
        ShowInTaskbar="True"
        ToolTipService.InitialShowDelay="400"
        ToolTipService.ShowDuration="20000"
        PreviewKeyDown="Window_PreviewKeyDown">
  <shell:WindowChrome.WindowChrome>
    <shell:WindowChrome CaptionHeight="40"
                        ResizeBorderThickness="0"
                        GlassFrameThickness="0"
                        CornerRadius="0"
                        UseAeroCaptionButtons="False"/>
  </shell:WindowChrome.WindowChrome>
```

> **All four of `Width`/`MinWidth`/`MaxWidth` and all four of `Height`/`MinHeight`/`MaxHeight` must be edited.** Editing only `Height` leaves `MinHeight=700` clamping the window and silently invalidates every number in §4.

`GlassFrameThickness="0"`, not `"0,1,0,0"`: a 1px glass frame extends the DWM frame into the client area and renders as a bright line across the top of a `#1C1D21` window, directly under the title bar's own `DividerBrush` rule. Shadow and rounding come from `DwmSetWindowAttribute` (below), not from the glass frame.

**Rounding + shadow.** In `SourceInitialized`:

```csharp
// NativeMethods.cs — add
private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
private const int DWMWCP_ROUND = 2;

[DllImport("dwmapi.dll")]
private static extern int DwmSetWindowAttribute(
    IntPtr hwnd, int attribute, ref int value, int size);

internal static void ApplyRoundedCorners(IntPtr hwnd)
{
    var preference = DWMWCP_ROUND;
    try { DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int)); }
    catch (DllNotFoundException) { /* pre-Win10 1803; square corners are acceptable */ }
}
```

Called as `NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);`. On Windows 10 the attribute is unrecognised and `DwmSetWindowAttribute` returns a non-zero HRESULT which we ignore — the window is square there, which is the documented, accepted degradation.

**DPI.** Both `app.manifest` files gain the `windowsSettings` block. The Companion's existing manifest already declares the Windows 10 `supportedOS` GUID, which is **required** for `<dpiAwareness>` to be honoured; the Mux's new manifest must declare it too. Complete Companion manifest:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="CspPaletteCompanion.App"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security><requestedPrivileges>
      <requestedExecutionLevel level="asInvoker" uiAccess="false" />
    </requestedPrivileges></security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

The Mux's manifest is identical with `name="CspMultiplexer.App"`.

> **Known consequence, accepted.** Under PerMonitorV2, `System.Windows.Forms.Screen.Bounds` and `Graphics.CopyFromScreen` both report and operate in **true physical pixels** rather than the virtualised coordinates a system-DPI-aware process sees. They are in the *same* coordinate space, so `CompanionQrScanner` needs **no code change** — but it will now capture full-resolution frames on high-DPI monitors. That is a decode-fidelity improvement; the cost is memory churn (a 4K grab is ~33 MB per monitor per scan pass). Acceptable, and noted here so it is not later diagnosed as a regression.

The Companion's csproj property `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` is **deleted** — it only emits the WinForms `ApplicationConfiguration.Initialize()` bootstrap and does nothing for a WPF entry point.

## 3.2 Title bar — 40 px, one layout

Root `Grid RowDefinitions="40,1,*"`.

**Row 0:** `Grid`, `Height="40"`, **`Background="{StaticResource WindowBrush}"`** (non-null or `DragMove` misses the hit test and the window becomes undraggable), `MouseLeftButtonDown="TitleBar_MouseLeftButtonDown"`.

`ColumnDefinitions="16,Auto,*,8,6,Auto,12,28,28,28,28,8"`

| Col | Content | Spec |
|---|---|---|
| 0 | left inset | 16 |
| 1 | **Wordmark** `TextBlock` | T2s, `VerticalAlignment="Center"`. Companion: `"CSP Palette Companion"`. Mux: `"CSP Mux"`. **No subtitle, no logo tile, no gradient monogram.** Not hit-test-visible in chrome. |
| 2 | spring | `*` |
| 3 | **`ConnectionDot`** | `Ellipse` **8 × 8**, `VerticalAlignment="Center"`. **Must remain a `Shape`** — code writes `.Fill`. Not hit-test-visible in chrome. |
| 4 | gap | 6 |
| 5 | **`ConnectionText`** | T3, `MaxWidth="96"`, **`TextWrapping="NoWrap"`**, `TextTrimming="CharacterEllipsis"`, `VerticalAlignment="Center"`, **`AutomationProperties.LiveSetting="Polite"`**. **No `ToolTip`** — see below. Not hit-test-visible in chrome. |
| 6 | gap | 12 |
| 7 | **`PinButton`** | `ToggleButton`, `TitleToggleStyle`, 28 × 28, pin glyph. Companion and **Mux both** — a proxy window is equally likely to want to float over CSP. `Checked` + `Unchecked` → `PinButton_Toggled`. |
| 8 | **`SettingsButton`** | `Button`, `TitleButtonStyle`, 28 × 28, sliders glyph. **Fixed-width column.** |
| 9 | Minimize | `Button`, `TitleButtonStyle`, 28 × 28. Unnamed. |
| 10 | Close | `Button`, `CloseButtonStyle`, 28 × 28. Unnamed. |
| 11 | right inset | 8 |

**Width check.** `16 + wordmark + spring + 8 + 6 + 96 + 12 + (4 × 28) + 8 = 258 + wordmark + spring`. Companion wordmark ("CSP Palette Companion", 13px SemiBold) ≈ **145 px** → spring = **57 px**. Mux ("CSP Mux") ≈ **58 px** → spring = **144 px**. ✔ Both clear.

**`WindowChrome.CaptionHeight="40"` must exactly equal row 0's height**, measured from y = 0. There is no outer window `Border`, so there is no 1px phase error.

**Every one of cols 7–10 carries `shell:WindowChrome.IsHitTestVisibleInChrome="True"`. Cols 1, 3 and 5 must NOT.** That is what makes the whole left two-thirds of the bar drag, and it removes the 133px dead hole the current connection pill creates.

**Why `ConnectionText` has no tooltip.** An element inside the `WindowChrome` caption region receives no mouse input, so a tooltip on it can never appear. `SetConnectionChrome`'s third parameter (the tooltip text) is therefore **deleted** in §4.7 edit 4. The information it carried — an explanation of the connection state — belongs in `ConnectionInstructions`, which is a visible live region in the connection strip. The one string it carried in the *connected* state ("Clip Studio Paint is authenticated through local Companion Mode.") is deleted by §6 regardless.

**Row 1:** `Height="1"`, `Background="{StaticResource DividerBrush}"`.

**Row 2:** content host, `Margin="16,12,16,12"`. Holds `MainView` and `SettingsView` as siblings in the same cell.

## 3.3 Connection state semantics — identical in both apps

| Condition | `ConnectionDot.Fill` | `ConnectionText` (Companion) | `ConnectionText` (Mux) |
|---|---|---|---|
| Not connected / idle | `SubtleBrush` | `Offline` | `Offline` |
| CSP found but not paired | `ErrorBrush` | `Disconnected` | — |
| Scanning for the QR | `WarningBrush` | `Scanning` | `Scanning` |
| Authenticating | `WarningBrush` | `Connecting` | `Connecting` |
| Connected / online | `AccentBrush` | `Connected` | `Connected` |
| Failed | `ErrorBrush` | `Failed` | `Failed` |

One vocabulary, six words, both apps.

## 3.4 Settings pattern — in-window view swap, both apps

**Entry** (`SettingsButton_Click`):
```
MainView.Visibility     = Collapsed
SettingsView.Visibility = Visible
SettingsButton.Visibility = Hidden        // NOT Collapsed
BackButton.Focus()
```

> `Hidden`, not `Collapsed`, is load-bearing. A ghosted sliders icon next to a Back button reads as a broken control, and `Hidden` preserves the 28px column so Minimize and Close do not jump 28px left when the settings page opens.

**Exit** — `BackButton` **or** `Esc`. `Window_PreviewKeyDown` handles `Key.Escape` only when `SettingsView.Visibility == Visible`, and invokes `BackButton_Click(BackButton, e)` synthetically with a `KeyEventArgs` — so **`BackButton_Click`'s second parameter must stay `RoutedEventArgs`**. Exit restores `SettingsButton.Visibility = Visible` and `SettingsButton.Focus()`.

**Transition:** instant (G13).

**Commit:** save-on-change in both apps. No Save/Cancel buttons anywhere. **No "changes are saved" text anywhere.**

**The `IsDefault` guard — both halves or neither.** `ExtractButton` is `IsDefault="True"`, so Enter anywhere in the window fires it; `ExtractButton_Click` opens with `if (MainView.Visibility != Visibility.Visible) return;` and that is the *only* thing preventing Enter on the settings page from launching an extraction. **The Mux's `PrimaryButton` takes `IsDefault="True"` and needs the identical guard added** — this is new code the port must not omit.

`MainView` and `SettingsView` must remain two named `UIElement`s that answer `.Visibility` correctly.

## 3.5 Window position persistence

Both apps persist `Left`/`Top` and restore them, because both sit beside CSP for a whole session and neither does today.

**Companion.** `AppSettings` (a `record` with `CurrentSchemaVersion = 1`) gains two nullable members:

```csharp
public double? WindowLeft { get; init; }
public double? WindowTop  { get; init; }
```

`System.Text.Json` maps a missing property to `null`, so existing settings files load unchanged and no schema-version bump is required.

**Mux.** `AppPreferences` becomes:

```csharp
internal sealed record AppPreferences(
    string ListenAddress,
    bool HideQrAfterFirstConnection = false,
    double? WindowLeft = null,
    double? WindowTop = null);
```

Positional records tolerate missing trailing properties on deserialisation (they take their defaults). Existing files load unchanged.

**Restore, in `SourceInitialized`, both apps:**

```csharp
var l = settings.WindowLeft;
var t = settings.WindowTop;
if (l is { } left && t is { } top &&
    System.Windows.Forms.Screen.AllScreens.Any(s =>
        s.WorkingArea.Contains((int)left + 40, (int)top + 20)))
{
    Left = left;
    Top  = top;
}
else
{
    var wa = SystemParameters.WorkArea;                    // primary display
    Left = wa.Left + (wa.Width  - Width)  / 2;
    Top  = wa.Top  + (wa.Height - Height) / 2;
}
```

The probe point is 40px right and 20px down from the top-left corner — inside the title bar — so a window whose saved monitor has been unplugged, or which was left mostly off-screen, falls back to centre. `WindowStartupLocation="Manual"` is set in XAML precisely so this code owns positioning; `CenterScreen` cannot be requested from `SourceInitialized` because the property has already been consumed by then, which is why the fallback computes the centre explicitly.

**Save, in `Closing`:** write `Left` and `Top` into the settings record before the existing save call. Wrap in the same `try/catch` as every other save.

---

# 4. COMPANION — `CSP Palette Companion`

**Window 460 × 620.**
Content box: width `460 − 16 − 16 = ` **428**; height `620 − 40 (title) − 1 (divider) − 12 (top pad) − 12 (bottom pad) = ` **555**.

## 4.1 `MainView` — block table

`Grid x:Name="MainView"`, all rows fixed or `Auto` except row 11.

| Row | Height | Block | Preserved `x:Name`s landing here |
|---|---|---|---|
| **0** | **56** + `Margin="0,0,0,12"` | **Connection strip** — `Border`, `StripStyle`, `Background="{StaticResource PanelBrush}"`, `BorderBrush="{StaticResource BorderBrush}"`, `Padding="10,8"`. Row is **`Auto`**, so `Visibility="Collapsed"` (code, line 127) reclaims the full **68** including the margin. | `ConnectionPanel`, `ConnectionHeading`, `ConnectionInstructions`, `ConnectButton` |
| **1** | **68** | **Source grid** — explicit `Grid RowDefinitions="32,4,32" ColumnDefinitions="*,4,*"`. Four `RadioButton`s with `SegmentRadioStyle`, `GroupName="Source"`, at (0,0) (0,2) (2,0) (2,2). **No margins, no `UniformGrid`** — the gap is exactly 4 and the height is exactly `32+4+32 = 68`. | `CanvasSource`, `LayerSource`, `SelectionCanvasSource`, `SelectionLayerSource` |
| **2** | 8 | gap | |
| **3** | **16** | **`SourceHelp`** — T4, `Height="16"`, `TextWrapping="NoWrap"`, `TextTrimming="CharacterEllipsis"`, `ToolTip="{Binding Text, RelativeSource={RelativeSource Self}}"` | `SourceHelp` |
| **4** | 12 | gap | |
| **5** | **32** | **Counts row** — `Grid ColumnDefinitions="*,12,*"`. Each half (208 wide) is `Grid ColumnDefinitions="Auto,8,100"`: a `Label` (`FieldLabelStyle`, `Target` bound) + a 100 × 32 stepper frame (§2.7). | `MajorLabel`, `DecreaseMajorButton`, `MajorCount`, `IncreaseMajorButton`, `MinorLabel`, `DecreaseMinorButton`, `MinorCount`, `IncreaseMinorButton` |
| **6** | 12 | gap | |
| **7** | **40** | **`ExtractButton`** — `PrimaryButtonStyle`, `HorizontalAlignment="Stretch"`, `IsDefault="True"` | `ExtractButton` |
| **8** | 12 | gap | |
| **9** | **100** (`Auto`; **111** while busy) | **Status strip** — §2.10 | `StatusPanel`, `StatusDot`, `StatusText`, `DetailText`, `BusyIndicator`, `OpenPaletteButton` |
| **10** | 12 | gap | |
| **11** | **`*`** | **Swatch tray** — §4.4 | `PalettePreview`, `PalettePlaceholder` |
| **12** | **36** + `Margin="0,12,0,0"` | **`PaletteDragChip`** — §2.13. Row is **`Auto`**; `Visibility="Collapsed"` until a result reclaims the full **48**. | `PaletteDragChip` |

**Vertical budget — every state summed:**

```
Fixed, connection strip visible, chip visible:
  68 + 68 + 8 + 16 + 12 + 32 + 12 + 40 + 12 + 100 + 12 + 48 = 428   →  tray = 555 - 428 = 127
Fixed, strip visible, no chip:
  68 + 68 + 8 + 16 + 12 + 32 + 12 + 40 + 12 + 100 + 12      = 380   →  tray = 175
Fixed, strip collapsed, chip visible:
       68 + 8 + 16 + 12 + 32 + 12 + 40 + 12 + 100 + 12 + 48 = 360   →  tray = 195
Fixed, strip collapsed, no chip:
       68 + 8 + 16 + 12 + 32 + 12 + 40 + 12 + 100 + 12      = 312   →  tray = 243
```

| State | Tray | Tray interior (−2 border −16 padding) | Swatch rows visible |
|---|---|---|---|
| Idle, disconnected | 175 | 157 | 4 |
| Idle, connected | 243 | 225 | 6 |
| **Result, connected — the working state** | **195** | **177** | **4** |
| Result, disconnected | 127 | 109 | 3 |

Every column sums to 555. ✔

**Connection strip height, derived:** `border 1 + padding 8 + ConnectionHeading (T2s, LineHeight 18) + xs gap 4 + ConnectionInstructions (T4, LineHeight 16) + padding 8 + border 1 = 1+8+18+4+16+8+1 = ` **56** ✔ — the 4px title→caption gap is on the spacing scale and is included, not omitted.

**Connection strip internals:** `Grid ColumnDefinitions="*,8,Auto"`. Col 0 is a `StackPanel` with the heading and instruction. Col 2 is `ConnectButton` (`CompactButtonStyle`, 28, `MinWidth="76"`, `VerticalAlignment="Center"`).

`ConnectionInstructions` column width = `428 − 2 (border) − 20 (padding) − heading col? no, separate rows` → the text block spans `428 − 2 − 20 − 8 − 76 = 322 px`, wrapping is on (implicit `TextBlock` style), and the block reserves exactly one 16px line. Instruction strings longer than ~57 characters will wrap and push the strip taller — **so the strip's `Height` is not fixed; the `Border` is `Auto` and 56 is the nominal.** §6 keeps every instruction ≤ 57 characters so 56 is the actual height in every state; a longer string grows the strip and shrinks the tray rather than clipping.

**Hard contract on `ConnectButton`:** its `.Content` is assigned a **bare `string`** (`"Stop"` / `"Connect"`) by the 2-second connection poll. Compose no icon+text content in XAML — the first tick wipes it.

**Hard contract on `ConnectionPanel`:** must expose `BorderBrush` (a `Border`). Code writes it at lines 144, 155, 232. Its `Background` stays the static `PanelBrush`; only the outline carries tone here. This requires **zero** code change.

**Segment width:** `(428 − 4) / 2 = 212 px` per cell. `"Selection · Canvas"` at 11px SemiBold ≈ 96 px + 16 padding = 112 ≤ 212. ✔ **No abbreviation and no font-dependent fallback branch** — a 1 × 4 row at 105px each would have been font-availability-dependent, which cannot be resolved at build time.

**`SourceHelp` width budget:** 428 px at 11px ≈ **76 characters**. The longest rewritten string (§6) is `"Runs your CSP action, then copies the selection."` at **47**. ✔ The self-bound tooltip is the safety net for a future longer string.

**Counts row width budget:** half = 208; label column = `208 − 8 − 100 = 100 px`. `"Major colors"` at 11px SemiBold ≈ 70 px. ✔ The noun **"colors" is retained** — dropping it to bare "Major"/"Minor" saves nothing (the row is 32 either way) and removes the only place in the UI that says what is being counted.

## 4.2 Deletions, itemised — where the space came from

| Deleted | Reclaimed |
|---|---|
| Title bar 42 → 40, subtitle `"Local CSP color extraction"` removed | 2 |
| Outer 1px window `Border` (top + bottom) | 2 |
| Page margins `20,14,20,16` → `16,12,16,12` | 6 |
| `"SOURCE"` section label + its 7px gap | 22 |
| `SourceHelp` `MinHeight` 30 (2 reserved lines) → 16 (1 line), margins 6 → gaps 8/12 | 14 |
| Stepper labels stacked above → inline beside (54 → 32) | 22 |
| `ExtractButton` 44 → 40, surrounding margins 14 → 12 | 8 |
| **Status card ≈267 → status strip 100** (card chrome, `DetailText` fixed block, dead progress margin, chip row, "Show palette file" row, and the mostly-empty placeholder region all collapse) | **167** |
| `ProgressStageText` + its dead 8px margin | 25 |
| Drag chip 48 → 36; second line `"Adds these colors as a new set"` deleted | 12 |
| `"Show palette file"` row deleted; **`OpenPaletteButton` relocated into the status strip's trailing slot as a 28px link at zero added height** | 43 |
| Footer `"Local processing · no artwork is uploaded"` | 21 |
| Window 700 → 620 | (−80 spent) |

**Result, measured like-for-like.** Today, in the connected + result state, the swatch `WrapPanel` receives ≈**51 px** of visible area (derivation: the status card's `*` row measures ≈267; its non-star children consume dot-row 17 + `DetailText` 51 + dead progress margin 8 + chip 56 + button row 43 = 175, and the card's own margin+border+padding is 32, leaving 60 for the palette region, minus its 9px top margin = 51). The new design gives **177 px of tray interior in the same state — a 3.5× increase inside a window that is 80 px shorter.**

## 4.3 Status strip — Companion specifics

Trailing slot (row 0, column 3) = **`OpenPaletteButton`**, `LinkButtonStyle`, height 28, `Content="Show file"`, `ToolTip="Show the .aco file in Explorer"`, `Visibility="Collapsed"` until a result. Code continues to drive `.Visibility` at lines 383 and 559 with no change.

> This is the single largest structural saving in the app and it costs nothing: `PaletteDragChip_Click` is literally `=> OpenPaletteButton_Click(sender, e)`, so today there are two adjacent controls performing one action across 43 px of chrome. The affordance survives at 0 px.

## 4.4 Swatch tray

```xml
<Border CornerRadius="8" Padding="8"
        Background="{StaticResource PanelBrush}"
        BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
  <Grid>
    <ScrollViewer VerticalScrollBarVisibility="Auto"
                  HorizontalScrollBarVisibility="Disabled">
      <WrapPanel x:Name="PalettePreview" Orientation="Horizontal" VerticalAlignment="Top"/>
    </ScrollViewer>
    <StackPanel x:Name="PalettePlaceholder" Orientation="Horizontal"
                VerticalAlignment="Top" HorizontalAlignment="Left"
                IsHitTestVisible="False">
      <!-- 11 × Rectangle with GhostSwatchStyle; the last one Margin="0" -->
    </StackPanel>
  </Grid>
</Border>
```

**`PalettePreview` must remain a raw `Panel` whose `Children` collection is directly addable.** Code calls `.Children.Clear()` and `.Children.Add(swatch)`; an `ItemsControl` breaks the build.

**`PalettePlaceholder`** is overlaid in the *same* `Grid` cell as the `ScrollViewer`, never stacked below it. Code touches only `.Visibility` (Collapsed at 423, Visible at 557) — any `UIElement` satisfies the contract.

**Swatch geometry, fully derived:**

```
Tray outer width                    428
  − border (1 + 1)                   -2  → 426
  − padding (8 + 8)                 -16  → 410 inner
Swatch pitch = 32 + Margin.Right 4  =  36
Per row: floor((410 + 4) / 36) = 11        (11 × 36 − 4 = 392 ≤ 410;  12 × 36 − 4 = 428 > 410)
With the 8px scrollbar shown: floor((402 + 4) / 36) = 11   → reflow-safe, 11 either way
Maximum result = 20 major + 20 minor = 40 swatches
Rows = ceil(40 / 11) = 4
WrapPanel height = 4 × 36 = 144      (WrapPanel line height includes the child's 4px bottom margin;
                                      it does NOT trim the trailing row's margin)
```

**Tray interior in the connected + result state = 195 − 2 − 16 = 177. 144 ≤ 177, so the 20 + 20 maximum fits without scrolling.** In the disconnected + result state the interior is 109 and a fourth row scrolls; the `ScrollViewer` is mandated by contract and handles it.

The ghost row is 11 rectangles at the same 36px pitch: `11 × 36 − 4 = 392 ≤ 410`. ✔ Same shape, same count, same position as the first real row.

## 4.5 `SettingsView` — block table

`Grid x:Name="SettingsView"`, `Visibility="Collapsed"`, `RowDefinitions="28,12,*"`.

| Row | Height | Content |
|---|---|---|
| **0** | **28** | `Grid ColumnDefinitions="28,8,*"` → **`BackButton`** (`TitleButtonStyle`, 28 × 28, back glyph, `ToolTip="Back (Esc)"`) · gap · `"Settings"` **T1**, `VerticalAlignment="Center"` |
| **1** | 12 | gap |
| **2** | **`*` = 515** | `ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" Padding="0,0,8,0"` containing a vertical `StackPanel`. **Card width = 428 − 8 = 420.** |

**Scroll-region `StackPanel` contents, in order:**

| # | Element | Height | Margin |
|---|---|---|---|
| 1 | **`SettingsNotice`** — `Border`, `CardStyle` overridden to `Background="{StaticResource ErrorStatusBrush}"` `BorderBrush="{StaticResource ErrorBrush}"`. Contains **`SettingsNoticeText`**, T4, 2 reserved lines (32). `Visibility="Collapsed"` by default. **`AutomationProperties.LiveSetting="Assertive"` — the only Assertive live region in the suite.** | `1+8+32+8+1 = ` **50** (grows if the text wraps further) | `0,0,0,12` |
| 2 | **Permissions card** — `Border`, `CardStyle`. Three rows separated by 1px `DividerBrush` `Rectangle`s with `Margin="0,8"`. | `2 + 20 + (38 + 17 + 38 + 17 + 38) = ` **170** | `0,0,0,12` |
| 3 | **`AutoActionOptionsPanel`** — `Border`, `CardStyle`. `Visibility` driven by code (lines 657–659). | `2 + 20 + (32 + 8 + 32 + 8 + 28 + 8 + 32) = ` **170** | `0,0,0,12` |
| 4 | **Meta card** — `Border`, `CardStyle`. | `2 + 20 + (16 + 4 + 28) = ` **70** | `0` |

**Stack height:**
- Base (no notice, options collapsed): `170 + 12 + 70 = ` **252** in a 515 viewport → 263 px of empty viewport **below** the content. That reads as a short page, not as a hole: nothing is bracketed by rules and nothing is anchored to the bottom.
- Options open: `170 + 12 + 170 + 12 + 70 = ` **434** → 81 px slack.
- Options open + notice: `+ 62 = ` **496** → 19 px slack.
- **Nothing can ever clip.** The `ScrollViewer` handles the pathological case (a raw `IOException` message wrapping to five lines), which is precisely the state that would otherwise silently swallow the settings path row and its "Show file" link with no gesture to recover them.

**Permission row (× 3), 38 px:** `Grid ColumnDefinitions="*,12,44"` → col 0 is a `StackPanel` with the title (T2s, 18) + `xs` 4 + the caption (T4, 1 line, 16) = 38; col 2 is the toggle's 44 × 28 focus box, `VerticalAlignment="Center"`.

Row order and names: `CompanionPermissionToggle`, `ClipboardPermissionToggle`, `AutoActionPermissionToggle`. **`AutoActionCaption`** is the caption `TextBlock` of the third row (unchanged from today's position — line 570). `AutoActionPermissionToggle` keeps its **inline `ToolTipService.ShowOnDisabled="True"`**; without it the "Requires Clipboard capture" explanation is invisible exactly when it matters.

All three are wired to **`Click`**, not `Checked`/`Unchecked`.

**`AutoActionOptionsPanel` internals, 148 px of content:**

| Sub-block | Height |
|---|---|
| Static caution caption, T4, 2 lines | 32 |
| gap | 8 |
| **`AutoActionPicker`** (`ComboBox`) | 32 |
| gap | 8 |
| `Grid ColumnDefinitions="Auto,8,Auto,*"` → **`RefreshActionsButton`** (`CompactButtonStyle`, `MinWidth="76"`) · **`SetupGuideButton`** (`CompactButtonStyle`, `MinWidth="96"`) | 28 |
| gap | 8 |
| **`ActionStatusText`**, T4, 2 reserved lines, `AutomationProperties.LiveSetting="Polite"`, `TextTrimming="CharacterEllipsis"` | 32 |

`32+8+32+8+28+8+32 = 148`; `+ 20 padding + 2 border = ` **170** ✔

**Meta card internals, 48 px of content:**

| Sub-block | Height |
|---|---|
| **`AboutText`**, T4, 1 line | 16 |
| gap | 4 |
| `Grid ColumnDefinitions="*,8,Auto"` → **`SettingsPathText`** (T2, **`TextWrapping="NoWrap"` + `TextTrimming="CharacterEllipsis"` — load-bearing**; a real `%LOCALAPPDATA%` path is ~90 characters and wraps to three lines otherwise; the full path is in the tooltip, written by code at line 115) · **`ShowSettingsFileButton`** (`LinkButtonStyle`, 28, `Content="Show file"`) | 28 |

`16+4+28 = 48`; `+ 20 + 2 = ` **70** ✔

**Deleted from `SettingsView`:** the intro caption, the three `AutomationProperties.HelpText` duplicates of the permission captions, the `"About"` section label, the `"Stored locally"` label, the `"Selection · Canvas command"` heading (the caution caption directly under the toggle already scopes it), and the footer `"Changes are saved immediately"`.

## 4.6 Live regions — all six must keep `AutomationProperties.LiveSetting`

| Element | Setting |
|---|---|
| `ConnectionText` | `Polite` |
| `ConnectionInstructions` | `Polite` |
| `StatusText` | `Polite` |
| `DetailText` | `Polite` |
| `ActionStatusText` | `Polite` |
| **`SettingsNoticeText`** | **`Assertive`** |

`Announce(UIElement)` raises `LiveRegionChanged`, which is **inert** without the declared live setting.

## 4.7 Companion code-behind changes — the complete, exhaustive list

Every C# edit required. Nothing else in `MainWindow.xaml.cs` changes except string literals (§6).

**Edit 1 — `ShowPalette`, lines 427–453.**
```csharp
Margin = new Thickness(0, 0, 4, 4),                     // was (0, 0, 6, 6)
...
ToolTip = $"{color.Name} · {color.Color.ToHex()} — set as CSP color",
```
and **delete** the `AutomationProperties.SetHelpText(swatch, "Changes the current drawing color…")` call entirely.

> The tooltip is where the click affordance now lives. `PalettePlaceholder`'s explanatory sentence is deleted and its `AutomationProperties.Name` ("Set CSP drawing color to …") already carries it for screen readers — the tooltip is what carries it for everyone else. This is deliberate: **do not delete the affordance from every sighted surface.**

**Edit 2 — `ApplyStatusTone`, lines 548–552. Signature change.**
```csharp
private enum StatusTone { Neutral, Busy, Good, Bad }

private void ApplyStatusTone(StatusTone tone)
{
    var (toneKey, surfaceKey) = tone switch
    {
        StatusTone.Good => ("AccentBrush",  "AccentStatusBrush"),
        StatusTone.Busy => ("WarningBrush", "WarningStatusBrush"),
        StatusTone.Bad  => ("ErrorBrush",   "ErrorStatusBrush"),
        _               => ("SubtleBrush",  "PanelBrush"),
    };

    var tint = (Brush)FindResource(toneKey);
    StatusDot.Fill = tint;
    StatusPanel.BorderBrush = tone == StatusTone.Neutral
        ? (Brush)FindResource("BorderBrush")
        : tint;
    StatusPanel.Background = (Brush)FindResource(surfaceKey);
}
```
Call-site changes: line 518 `ApplyStatusTone(StatusTone.Busy)` (**was `AccentBrush`** — in-progress must not render the same as success; this is the code half of the §1.2 semantic table); line 526 `ApplyStatusTone(StatusTone.Bad)`; line 534 `ApplyStatusTone(StatusTone.Good)`.
**Two new call sites:** at the end of `ClearPaletteResult` and at the end of `MainWindow_Loaded`, add `ApplyStatusTone(StatusTone.Neutral);` — without them the idle tone is whatever XAML declared and never returns after a failure.

**Edit 3 — `SetProgress`, lines 508–519.** Delete the `ProgressStageText.Text = status switch {…}` block (511–516) and the `ProgressStageText.Visibility` assignment (517). Delete the corresponding assignments in `SetFailure` (525) and `SetSuccess` (533). Delete the `ProgressStageText` element from XAML.

**Edit 4 — `SetConnectionChrome`, lines 272–282. Signature change.**
```csharp
private void SetConnectionChrome(Brush brush, string text)   // third parameter deleted
{
    ConnectionDot.Fill = brush;
    if (!string.Equals(ConnectionText.Text, text, StringComparison.Ordinal))
    {
        ConnectionText.Text = text;
        Announce(ConnectionText);
    }
    // ConnectionText.ToolTip assignment DELETED — an element inside the WindowChrome
    // caption region receives no mouse input, so the tooltip could never appear.
}
```
Five call sites (124, 139, 159, 169, and the QR-wait path) drop their third argument. **The ordinal-equality guard stays** — it is what stops the 2-second poll from spamming automation events.

**Edit 5 — `LoadAbout`, line 107.** `AboutText.Text = $"{shortVersion}GPL-3.0";`

**Edit 6 — window position.** `AppSettings` gains `WindowLeft`/`WindowTop`; `SourceInitialized` restores per §3.5; `Closing` saves. Add `NativeMethods.ApplyRoundedCorners` and call it from `SourceInitialized`.

**Edit 7 — all user-visible strings** per §6. This is ~55 literal edits across `MainWindow.xaml.cs` and ~24 across `CspAcquisitionService.cs`.

**Guards that must NOT be touched:**
- `UpdateStepperAvailability`'s `if (!IsInitialized) return;` (line 1039).
- `ExtractButton_Click`'s `if (MainView.Visibility != Visibility.Visible) return;` (line 316).
- `PermissionToggle_Click`'s `_loadingSettings` guard.
- `Source_Checked`'s `if (IsLoaded)` guard, and its wiring to `Checked` only — **never** add `Unchecked`.
- `StopAsync`-equivalent ordering — n/a for the Companion.
- `SetStatus`'s `DetailText.ToolTip = detail` mirror (line 543).

---

# 5. MUX — `CSP Mux`

**Window 460 × 620.** Same shell, same content box: **428 × 555**.

## 5.1 File disposition

| File | Fate |
|---|---|
| `MainForm.cs` (439) | **Deleted** → `MainWindow.xaml` + `MainWindow.xaml.cs` |
| `SettingsForm.cs` (171) | **Deleted** → `SettingsView` inside `MainWindow.xaml` |
| `ThemeControls.cs` (383) | **Deleted entirely.** Zero custom-painted controls survive. |
| `Program.cs` (11) | **Deleted** — `App.xaml` as `ApplicationDefinition` generates `[STAThread] Main`; keeping both is **CS0017: more than one entry point** |
| `ProxyQrRenderer.cs` | Rewritten (§5.5) |
| `CompanionQrScanner.cs` | **Deleted.** Replaced by the Companion's implementation, moved into `CspSuite.Theme`'s sibling — see below |
| `AppPreferences.cs` | Kept; gains `WindowLeft`/`WindowTop` and a guarded `Save` |
| `CspMultiplexer.Broker`, `CspMultiplexer.Protocol` | **Untouched.** `net8.0`, UI-agnostic. |

> **Scanner unification.** The Mux's `CompanionQrScanner` silently swallows every per-display capture failure, so if all monitors fail to capture it scans forever showing "Scanning" and reports nothing. The Companion's aggregates capture errors into an `AggregateException` with device names and bounds and throws when every display fails. Requirement 5 says one suite: **the Mux's copy is deleted and the Companion's file is copied into the Mux project verbatim** (namespace adjusted). It is not put in `CspSuite.Theme` — that project is `UseWPF` with no code and no WinForms reference; a shared *scanner* would need a fourth project and is not worth it for one file. Copy it, and note in both files that they must be kept in sync.

## 5.2 `MainWindow.xaml` — main view block table

`Grid x:Name="MainView"`:

| Row | Height | Block | Name |
|---|---|---|---|
| **0** | **56** | **Instruction strip** — `Border`, `StripStyle`, `Background="{StaticResource PanelBrush}"`, `BorderBrush="{StaticResource BorderBrush}"`, `Padding="10,8"`. Contains one `TextBlock`, T4, 2 reserved lines (32), `VerticalAlignment="Center"`. **Always visible.** | `InstructionText` |
| **1** | 12 | gap | |
| **2** | **`*` = 323** | **QR frame** — `Border`, `QrFrameStyle` (§2.18), **300 × 300**, centred both axes. Contains `Image x:Name="QrImage"`. | `QrFrame`, `QrImage` |
| **3** | 12 | gap | |
| **4** | **100** (`Auto`; **111** while busy) | **Status strip** — the *identical component* to the Companion's (§2.10), same element names so the two `MainWindow.xaml` files are diffable. Trailing slot = `ClientCountBadge`. | `StatusPanel`, `StatusDot`, `StatusText`, `DetailText`, `BusyIndicator`, `ClientCountBadge` |
| **5** | 12 | gap | |
| **6** | **40** | **Action row** — `Grid ColumnDefinitions="*,12,*"`. `SecondaryButton` in col 0 (`SecondaryButtonStyle` at `Height="40"`), `PrimaryButton` in col 2 (`PrimaryButtonStyle`, `IsDefault="True"`). Outside the two-button states, col 0 is `Width="0"`, `SecondaryButton.Visibility="Collapsed"`, and `PrimaryButton` carries `Grid.Column="0" Grid.ColumnSpan="3"`. | `PrimaryButton`, `SecondaryButton` |

```
56 + 12 + 323 + 12 + 100 + 12 + 40 = 555   ✔
```

QR frame centred in a 323 row → 11.5 px above and below. The frame **never changes size or position** in any state; only its fill, border and content change. **No absolute repositioning of anything, ever** — the current app rewrites `primaryButton.Location` *and* `.Size` at three separate sites, which is only correct as long as every future transition remembers both. A `Grid` with a collapsing column is correct by construction.

**Two strips, two jobs, no duplication.** The instruction strip says **what you should do**; the status strip says **what the app is doing**. This is the same split as the Companion (connect-to-CSP instruction vs extraction result). No state renders the same sentence twice.

## 5.3 Explicit state machine

The current app has **no state enum** — state is inferred from `multiplexer is not null` plus whatever `SetConnectionState` last wrote, and `Theme.MutedText` is used as an "idle" **sentinel compared by `System.Drawing.Color` struct equality**. That does not survive translation: `System.Windows.Media.Color` has different semantics and `SolidColorBrush` instances resolved from a `ResourceDictionary` compare **by reference**. This is the single highest-risk silent breakage in the port.

```csharp
internal enum ConnectionState { Idle, Scanning, Connecting, Online, QrHidden, Failed }
```

`ApplyState(ConnectionState)` is the **only** UI mutation entry point, and it is **called from the constructor** with `Idle`. That deletes the Launch/Idle divergence where the app boots showing "Offline" (ctor) and `StopAsync` later writes "Not connected" — two words for one condition, the first shown exactly once per session.

| | **Idle** | **Scanning** | **Connecting** | **Online** | **QrHidden** | **Failed** |
|---|---|---|---|---|---|---|
| `ConnectionDot` | `SubtleBrush` | `WarningBrush` | `WarningBrush` | `AccentBrush` | `AccentBrush` | `ErrorBrush` |
| `ConnectionText` | `Offline` | `Scanning` | `Connecting` | `Connected` | `Connected` | `Failed` |
| `InstructionText` | `Open CSP Companion Mode, then scan its QR.` | `Leave CSP's QR visible.` | `Leave CSP's QR visible.` | `Scan this code from each app you want to connect.` | `Show the QR to connect another app.` | *(mapped recovery sentence, §6.5)* |
| `QrFrame` | ghost | ghost | ghost | **paper + `QrImage`** | ghost | ghost |
| Status tone | `Neutral` | `Busy` | `Busy` | `Good` | `Good` | `Bad` |
| `StatusText` | `Not sharing` | `Scanning displays` | `Authenticating` | `Sharing` | `Sharing` | `Connection failed` |
| `DetailText` | *(empty)* | *(empty)* | *(empty)* | `{addr} · same Wi-Fi` **or** `This computer only.` | *(same as Online)* | *(mapped detail, §6.5)* |
| `BusyIndicator` | Collapsed | **Visible** | **Visible** | Collapsed | Collapsed | Collapsed |
| `ClientCountBadge` | `0 apps` | `0 apps` | `0 apps` | live | live | `0 apps` |
| `PrimaryButton` | `Scan CSP QR`, enabled | `Scanning…`, disabled | `Scanning…`, disabled | `Hide QR`, enabled | `Show QR`, enabled | `Scan CSP QR`, enabled |
| `SecondaryButton` | Collapsed | **`Cancel`**, enabled | **`Cancel`**, enabled | **`Stop`**, enabled | **`Stop`**, enabled | Collapsed |
| `SettingsButton` | enabled | enabled | enabled | **enabled** | enabled | enabled |

Three behavioural fixes are visible in that table:

1. **`SettingsButton` is never disabled.** Today `StartAsync` disables it and only `StopAsync` re-enables it — and since there was no path from Online back to Idle, it was dead for the rest of the session. Consequence: the network-scope `ComboBox` must now be `IsEnabled="{Binding IsStopped}"` while a session is running (§5.6), or the UI would silently accept a scope change that does nothing (the address is only read when constructing `CompanionMultiplexerOptions` inside `StartAsync`).
2. **Online is no longer terminal.** `SecondaryButton` becomes `Stop` in Online and QrHidden, calling `StopAsync()`. Today the only way to end a proxy session is to close the window.
3. **`Failed` is a persistent inline state, not a `MessageBox`.** It clears on the next `PrimaryButton` press. Today `SetConnectionState(error)` → blocking modal → `StopAsync` overwrites everything back to Idle, so the instant the user clicks OK all visual trace of the failure is gone.

`readyActivityDetail` is computed once at Online entry and cached so the Hide/Show toggle can restore it — loopback → `This computer only.`, otherwise → `{addr} · same Wi-Fi`.

`ClientCountBadge` text: `$"{n} {(n == 1 ? "app" : "apps")}"`.

## 5.4 `SettingsView` — block table

`Grid x:Name="SettingsView"`, `Visibility="Collapsed"`, `RowDefinitions="28,12,*"`. Header identical to the Companion's (`BackButton` + `"Settings"` T1). Row 2 = `ScrollViewer` (`Padding="0,0,8,0"`, viewport 515) → `StackPanel`, card width **420**.

| # | Element | Height | Margin |
|---|---|---|---|
| 1 | **`SettingsNotice`** + **`SettingsNoticeText`** (T4, `LiveSetting="Assertive"`) — carries `AppPreferences.Save` failures | 50 (Auto) | `0,0,0,12` |
| 2 | **Network card** (`CardStyle`): `"Connection scope"` T2s (18) + `xs` 4 + **`NetworkScopePicker`** `ComboBox` (32) + `xs` 4 + caption T4 1 line (16) = 74 | `2 + 20 + 74 = ` **96** | `0,0,0,12` |
| 3 | **QR-display card** (`CardStyle`): one 38px row — title T2s (18) + `xs` 4 + caption T4 (16) on the left, **`AutoHideQrToggle`** 44 × 28 on the right | `2 + 20 + 38 = ` **60** | `0,0,0,12` |
| 4 | **Meta card** (`CardStyle`): `AboutText` T4 (16) + `xs` 4 + path row (28) with **`SettingsPathText`** + **`ShowSettingsFileButton`** | `2 + 20 + 48 = ` **70** | `0` |

Stack, worst case: `50 + 12 + 96 + 12 + 60 + 12 + 70 = ` **312** in a 515 viewport. Nothing scrolls in practice; the `ScrollViewer` is there for the pathological notice.

**`NetworkScopePicker` behaviour:**
- `IsEnabled = (multiplexer is null)`, with `ToolTipService.ShowOnDisabled="True"` and `ToolTip="Stop sharing to change the network."`
- A **saved-but-currently-unavailable** address is injected into the list as a **disabled item** labelled `$"{address} · unavailable"` rather than vanishing. Today `ResolveAddress()` silently reverts to loopback with zero feedback whenever the saved adapter is down, renamed, or on a different subnet.
- `NetworkDiscovery.GetChoices()` is loaded on a background `Task` and assigned to `ItemsSource` on the Dispatcher. It is currently called synchronously on the UI thread from two places and a flaky VPN adapter stalls it.

**`AppPreferences.Save` gains a `try/catch`** writing into `SettingsNoticeText`. Today it has none and an IO failure throws out of a synchronous void handler → unhandled → crash.

## 5.5 QR pipeline — complete

**Primary path — no GDI at all. This is what ships.**

```csharp
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

internal static class ProxyQrRenderer
{
    /// <summary>Encodes the pairing URL as a frozen 1-pixel-per-module BitmapSource.</summary>
    public static BitmapSource Render(string pairingUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pairingUrl);

        var writer = new BarcodeWriterGeneric
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                // Width/Height deliberately left at 0 so Encode returns the natural
                // module matrix. Setting them makes ZXing upscale to a pixel bitmap.
                Margin = 4,                 // QR spec requires a 4-module quiet zone.
                CharacterSet = "UTF-8",
            },
        };

        BitMatrix matrix = writer.Encode(pairingUrl);
        int w = matrix.Width, h = matrix.Height;

        var dark  = ((SolidColorBrush)Application.Current.Resources["QrModuleBrush"]).Color;
        var light = ((SolidColorBrush)Application.Current.Resources["QrPaperBrush"]).Color;

        var pixels = new byte[w * h * 4];                       // Bgra32
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var c = matrix[x, y] ? dark : light;
            int i = (y * w + x) * 4;
            pixels[i + 0] = c.B; pixels[i + 1] = c.G;
            pixels[i + 2] = c.R; pixels[i + 3] = 255;
        }

        var bitmap = BitmapSource.Create(
            w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        bitmap.Freeze();                                        // cross-thread safe, cheaper to render
        return bitmap;
    }
}
```

**Display — integer module pitch is mandatory.**

```xml
<Border x:Name="QrFrame" Style="{StaticResource QrFrameStyle}" Padding="18">
  <Image x:Name="QrImage"
         Stretch="Uniform" StretchDirection="Both"
         UseLayoutRounding="True" SnapsToDevicePixels="True"
         RenderOptions.BitmapScalingMode="NearestNeighbor"/>
</Border>
```

and in code, when the source is assigned:

```csharp
var source = ProxyQrRenderer.Render(multiplexer.PairingUrl!);
QrImage.Source = source;

// Frame 300 - Padding 18*2 = 264 available. Snap to a whole number of device
// pixels per module so no module edge ever lands mid-pixel.
int modules = source.PixelWidth;                 // includes the 4-module quiet zone
int scale   = Math.Max(3, 264 / modules);
QrImage.Width = QrImage.Height = modules * scale;
```

**Derivation.** A pairing URL of this length encodes to QR version 5–7 → 37–45 modules, plus the 8 modules of quiet zone = **45–53 modules total**. `264 / 49 = 5` → a 245 px image, **exactly 5 device pixels per module at 100 % DPI**, centred in the 300 px frame with 27.5 px of `QrPaperBrush` on each side (a further ≈5.5 modules of quiet zone). Total quiet zone ≈ **9.5 modules — 2.4× the spec minimum**.

Compare to today: a 306 px bitmap at 49 modules is **6.24 px/module — non-integer**, resampled by `PictureBox.SizeMode.Zoom`, with a **2-module** quiet zone that is **out of spec**. The new pitch is 19 % smaller and *far* more scannable. At 125 % the module is 6.25 physical px; at 150 %, 7.5.

**`RenderOptions.BitmapScalingMode="NearestNeighbor"` is mandatory.** WPF's default Fant filtering blurs module edges at fractional DPI scale factors badly enough to make the code **unscannable** — a failure a `PictureBox` at 100 % DPI never exposed. This is the single most important visual-fidelity note in the entire port.

**Fallback path — if the `System.Drawing` writer is ever kept instead.** It must be written exactly like this:

```csharp
[DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

public static BitmapSource RenderViaGdi(string url, int size)
{
    using var bitmap = /* existing BarcodeWriter.Write(url) */;
    IntPtr hBitmap = bitmap.GetHbitmap();
    try
    {
        var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
            hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }
    finally { DeleteObject(hBitmap); }
}
```

> **The `try/finally` is not optional.** `Bitmap.GetHbitmap()` allocates a *new* GDI HBITMAP; `CreateBitmapSourceFromHBitmap` **copies** the pixels into a WIC bitmap and **does not take ownership**. Omitting `DeleteObject` leaks ~375 KB of GDI-heap memory *and one handle out of the process's 10 000-object GDI quota* on **every** successful `StartAsync`. It is silent, it survives `GC.Collect`, and `SafeHandle` will not save you — `IntPtr` is not tracked. The source `Bitmap` must be disposed independently, which the `using` does.

**State reset:** `QrImage.Source = null` when leaving Online or hiding the QR. With the primary path there is nothing to dispose; the explicit null-out only stops ~40 KB being rooted for the app's lifetime.

## 5.6 Dispatcher marshalling

`ClientCountChanged` is the **only** cross-thread callback into the UI. It fires from broker session threads (`OnSessionAuthenticated`, and `OnSessionClosed` from both `RunAsync`'s `finally` **and** `DisposeAsync`, which may already be on the UI thread — so a `CheckAccess` of `true` is legitimate).

```csharp
private void MultiplexerOnClientCountChanged(object? sender, ClientCountEventArgs e)
{
    if (!Dispatcher.CheckAccess())
    {
        // WPF-only failure mode WinForms did not have: posting to a dispatcher that
        // has begun shutdown throws / faults the returned DispatcherOperation, and a
        // late callback during window teardown is entirely possible.
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Normal,
            () => MultiplexerOnClientCountChanged(sender, e));
        return;
    }

    ClientCount = e.Count;                     // updates the pill
    if (e.Count > 0 && hideQrAfterFirstConnection && state == ConnectionState.Online)
    {
        ApplyState(ConnectionState.QrHidden);
    }
}
```

`Control.BeginInvoke` ≈ `DispatcherPriority.Normal`; this is the exact semantic equivalent plus the mandatory shutdown guard.

**Every `await` in the window's own async methods keeps `.ConfigureAwait(true)`** — it is already the default in WPF (the captured context is `DispatcherSynchronizationContext`), but keeping it explicit preserves readability parity with the WinForms original.

**The unsubscribe-before-dispose ordering in `StopAsync` is load-bearing and must not be reordered:**
```csharp
multiplexer.ClientCountChanged -= MultiplexerOnClientCountChanged;   // FIRST
await multiplexer.DisposeAsync();                                    // THEN
```
Disposing sessions raises `OnSessionClosed` → `ClientCountChanged`; unsubscribing first is what prevents a marshal onto a shutting-down dispatcher.

## 5.7 Cancellation and the `Closing` dance

**Capture the token once.** `operationCancellation` is currently dereferenced at three sites inside `StartAsync` while `StopAsync` disposes and nulls the field. If a scan completes on the same tick Cancel is pressed, the continuation reads a nulled or disposed field → `NullReferenceException`/`ObjectDisposedException` → today a `MessageBox` reading "Object reference not set to an instance of an object."

```csharp
private async Task StartAsync()
{
    await StopAsync();
    isBusy = true;                                     // explicit re-entrancy latch
    operationCancellation = new CancellationTokenSource();
    var token = operationCancellation.Token;           // ← capture once, use everywhere
    ...
}
```

**Distinguish a user cancel from a 15-second upstream timeout.** `UpstreamCompanionClient.SendRawAsync` links a `CancellationTokenSource(15s)` into every request, so an unresponsive CSP surfaces as an `OperationCanceledException` — which today lands in the same `catch` as a user cancel and returns silently to Idle with no message, no LED change, nothing.

```csharp
catch (OperationCanceledException)
{
    if (token.IsCancellationRequested) { await StopAsync(); }   // user cancelled → Idle
    else { ShowFailure("CSP did not respond."); }               // timeout → Failed
}
```

**Guard the primary button with `isBusy`.** `primaryButton.Enabled = false` currently happens *after* `await StopAsync()` inside `StartAsync`, so a double-click during a yielding `StopAsync` could enter `StartAsync` twice. That window is zero-width today only by accident.

**The `Closing` dance — three WPF-specific hazards:**

```csharp
private bool closingAfterCleanup;
private bool closeInProgress;

protected override async void OnClosing(CancelEventArgs e)
{
    if (closingAfterCleanup) { base.OnClosing(e); return; }

    e.Cancel = true;
    if (closeInProgress) return;          // (b) Alt+F4 / taskbar close during teardown
    closeInProgress = true;
    IsEnabled = false;

    SaveWindowPosition();
    await StopAsync();

    closingAfterCleanup = true;
    await Dispatcher.Yield(DispatcherPriority.Background);   // (a) MANDATORY
    Close();
}
```

**(a) Re-entrant `Close()` — the #1 porting hazard and a crash on the most common exit path.** `StopAsync` contains **no awaits at all** when `multiplexer` and `upstream` are both null — the user opens the app and closes it without connecting. `await` on an already-completed `Task` resumes **synchronously**, so `Close()` is invoked from *inside* `OnClosing`. WinForms tolerates recursive `Close()`. **WPF throws `InvalidOperationException`.** The dispatcher hop is what breaks the recursion.

**(b)** `IsEnabled = false` greys the custom chrome buttons but does **not** block Alt+F4, taskbar close, or `SC_CLOSE`. The `closeInProgress` latch is required.

**(c) `ShutdownMode="OnMainWindowClose"` in `App.xaml`, and never call `Application.Current.Shutdown()`.** `Shutdown()` raises `Closing` but does **not** honour `e.Cancel` — teardown would be skipped and the broker's sockets left to process exit. `Application.Run(new MainForm())` was already `OnMainWindowClose`, so this preserves behaviour exactly.

**`MessageBox` is deleted from the app.** Beyond the theming problem (a native light-themed dialog shatters the suite look), with WinForms usings present an unqualified `MessageBox` binds to `System.Windows.Forms.MessageBox` — which is exactly why the csproj carries the `<Using Remove>` items below.

## 5.8 `CspMultiplexer.App.csproj` — complete

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <!-- KEEP: Screen.AllScreens + Graphics.CopyFromScreen + BarcodeReader.Decode(Bitmap) -->
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>Assets\csp-mux.ico</ApplicationIcon>
    <AssemblyName>CSP Mux</AssemblyName>
    <RootNamespace>CspMultiplexer.App</RootNamespace>
    <Product>CSP Mux</Product>
    <Version>0.1.0</Version>
    <SelfContained>false</SelfContained>
  </PropertyGroup>

  <!-- Directory.Build.props sets ImplicitUsings=enable, and UseWindowsForms injects
       implicit System.Drawing + System.Windows.Forms usings. Under UseWPF those collide
       catastrophically with WPF types of the same name — Color, Point, Size, Rectangle,
       Brush, Pen, Image, FontStyle, Cursors, MessageBox, Application, Control, Button,
       Label, Panel, MessageBoxResult. Removing them turns every collision into a compile
       error you resolve deliberately instead of a silent wrong-type bind. -->
  <ItemGroup>
    <Using Remove="System.Drawing" />
    <Using Remove="System.Windows.Forms" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CspMultiplexer.Broker\CspMultiplexer.Broker.csproj" />
    <ProjectReference Include="..\CspMultiplexer.Protocol\CspMultiplexer.Protocol.csproj" />
    <ProjectReference Include="..\..\external\csp-suite-theme\src\CspSuite.Theme\CspSuite.Theme.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ZXing.Net.Bindings.Windows.Compatibility" Version="0.16.14" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\..\THIRD-PARTY-NOTICES.md" Link="THIRD-PARTY-NOTICES.md"
          CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

**Fallout of the `<Using Remove>` items:** `CompanionQrScanner.cs` needs explicit `using System.Drawing;` and `using System.Windows.Forms;` (for `Screen`, `Bitmap`, `Graphics`, `PixelFormat`, `CopyPixelOperation`). `ProxyQrRenderer.cs` needs neither under the primary path (§5.5). `ThemeControls.cs`, `MainForm.cs`, `SettingsForm.cs` are deleted, so their implicit-using dependencies evaporate.

**`<ApplicationHighDpiMode>` is removed** — it only emits the WinForms bootstrap and has zero effect on WPF.

**`Directory.Build.props` sets `TreatWarningsAsErrors=true` and `Nullable=enable`** repo-wide. Expect to add `<NoWarn>` entries for markup-compile diagnostics (MC30xx), and every `x:Name`-bound field and `SelectedItem` cast needs explicit null handling.

**`ApplicationIcon`** — there is currently no `.ico` anywhere in either repo. §7 specifies the asset.

## 5.9 `App.xaml` — Mux (and the identical Companion file)

```xml
<Application x:Class="CspMultiplexer.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnMainWindowClose"
             StartupUri="MainWindow.xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="pack://application:,,,/CspSuite.Theme;component/Theme.xaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

**The Companion's 832-line `App.xaml` is reduced to exactly this** (with `x:Class="CspPaletteCompanion.App.App"` and `StartupUri="MainWindow.xaml"`). Every resource moves into `Theme.xaml`. There are therefore **no local resources in either app** and no merge-order or duplicate-key question to reason about.

## 5.10 The shared theme project

**Repository layout** (`csp-suite-theme`):
```
src/CspSuite.Theme/CspSuite.Theme.csproj
src/CspSuite.Theme/Theme.xaml          (Build Action: Page)
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <EnableDefaultPageItems>false</EnableDefaultPageItems>
  </PropertyGroup>
  <ItemGroup>
    <Page Include="Theme.xaml" Generator="MSBuild:Compile" SubType="Designer"/>
  </ItemGroup>
</Project>
```

> **`Page`, not `Resource`.** `Page` compiles the dictionary to BAML, which validates every `{StaticResource}` key at build time and loads faster. `Resource` ships loose XAML parsed at runtime, so a typo in a key becomes a startup crash instead of a build error.

**Wiring, per app repo:**
```
git submodule add <url-of-csp-suite-theme> external/csp-suite-theme
git commit -m "Add shared suite theme as a submodule"
```
and the `ProjectReference` path shown in §5.8 (`..\..\external\csp-suite-theme\src\CspSuite.Theme\CspSuite.Theme.csproj`), which is **relative to the app's own working tree** and therefore stable under any clone location. Both `.sln` files add the theme project.

CI and fresh clones must use `git clone --recurse-submodules` (or `git submodule update --init`).

---

# 6. COPY — COMPLETE VERBATIM TABLE

**Register, enforced throughout:** *nouns not narration; state not reassurance; instruct only where the user cannot see what to do.*

**Two structural rules, applied as a lint over composite screen states, not string by string:**
1. **Never say the same thing on two surfaces at once.**
2. **A control's own label is not a thing to explain.**

**Case convention:** sentence case everywhere. No Title Case buttons. **ALL CAPS survives in exactly one place: the "DRAG" tag.**

**Characters:** `…` U+2026, `'` U+2019, `·` U+00B7. Save every `.xaml` and `.cs` as **UTF-8 with BOM**, or use `&#8230;` / `&#8217;` / `&#183;` in XAML.

**Every string was enumerated from the source files, including the two files earlier audits missed: `CspAcquisitionService.cs` (§6.3) and the `ComboBox` template strings (§6.4).**

## 6.1 Companion — `MainWindow.xaml`

| Line | Before | After |
|---|---|---|
| 58 | `Palette Companion` | **`CSP Palette Companion`** — the wordmark is the product name; the suite prefix belongs here, not in a tagline |
| 64 | `Local CSP color extraction` | **⌫ DELETE** — a tagline under the app's own name |
| 89 | `Checking…` (initial `ConnectionText`) | **`Offline`** — one vocabulary (§3.3); "Checking…" appears once at launch and never again |
| 103,114,121,128 | `&#xE718;` / `⚙` / `—` / `✕` (glyph content) | **⌫ Not strings** — replaced by `Path` geometry (§2.4) |
| 107 | `Keep this window above Clip Studio Paint` | **`Always on top`** |
| 115 | `Settings` (tooltip) | *keep* |
| 122 | `Minimize` | *keep* |
| 129 | `Close` | *keep* |
| 163 | `Connect for direct Canvas access` | **`Connect to CSP`** — "direct" is marketing |
| 167 | `In CSP, open "Connect to smartphone," leave the QR visible, then select Connect.` | **`In CSP, open Connect to smartphone, then Connect.`** (48 chars, fits the 322px strip column) |
| 175 | `Connect` | *keep* |
| 181 | `SOURCE` | **⌫ DELETE** — four labelled buttons need no header |
| 185,192,198,204 | `Canvas` / `Layer` / `Selection · Canvas` / `Selection · Layer` | *keep all four verbatim* |
| 215 | `Loads the complete visible canvas through Companion Mode.` | **`The whole visible canvas.`** — now byte-identical to what `UpdateSourceHelp` writes for the same state, ending a three-way inconsistency |
| 231 | `Major colors` | *keep* |
| 244 | `One fewer major color (minimum 1)` | **⌫ DELETE** |
| 251 | `Dominant colors to extract — 1 to 20. Adjust with ↑ ↓ or the mouse wheel.` | **`1–20 · ↑↓ or wheel`** — the wheel affordance is genuinely undiscoverable; keep it, halve it |
| 263 | `One more major color (maximum 20)` | **⌫ DELETE** |
| 274 | `Minor colors` | *keep* |
| 287 | `One fewer minor color (minimum 0)` | **⌫ DELETE** |
| 294 | `Supporting accent colors — 0 to 20. Adjust with ↑ ↓ or the mouse wheel.` | **`0–20 · ↑↓ or wheel`** — note the **correct range**; a single shared string here would be a lie on one of the two boxes |
| 306 | `One more minor color (maximum 20)` | **⌫ DELETE** |
| 318 | `Extract Palette` | **`Extract palette`** |
| 319 | `Read the selected source from CSP and build a color set (Enter)` (tooltip) | **`Extract palette (Enter)`** — the sentence explained a button that already says "Extract palette"; only the keyboard hint is new information |
| 344 | `Ready` (initial `StatusText`) | *keep* |
| 359 | `Canvas capture is read-only. Other access is controlled in Settings.` (initial `DetailText`) | **⌫ DELETE — `DetailText` starts empty** |
| 372 | `Loading image…` (`ProgressStageText`) | **⌫ DELETE the element** |
| 386 | `Extracted swatches appear here. Select one to make it CSP's drawing color.` | **⌫ DELETE** — replaced by the wordless ghost row; the click affordance moves to the swatch tooltip (§4.7 edit 1) |
| 404 | `Drag onto CSP's Color Set palette, or activate to show the file` (chip tooltip) | **`Drag onto CSP's Color Set palette`** — the "activate to show the file" half is now the visible "Show file" link in the status strip |
| 420 | `↗` (chip glyph text) | **⌫ DELETE** — replaced by the 16px accent tile and `Path` |
| 427 | `Drop onto CSP Color Set` | *keep* |
| 432 | `Adds these colors as a new set` | **⌫ DELETE** — explains the line directly above it |
| 441 | `DRAG` | *keep* |
| 451 | `Show palette file` | **`Show file`** — relocated into the status strip |
| 452 | `Reveal the generated .aco file in File Explorer` | **`Show the .aco file in Explorer`** |
| 461 | **`Local processing · no artwork is uploaded`** | **⌫ DELETE — named offender.** A privacy brag nailed to a desktop app with no network feature; it answers a question nobody asked and plants the suspicion it is answering |
| 481 | `← Back` | **⌫ DELETE the text** — `BackButton` becomes a 28 × 28 icon button |
| 482 | `Back to extraction (Esc)` | **`Back (Esc)`** |
| 487 | `Settings` (page title) | *keep* |
| 493 | `Choose exactly what the app may read or run. Riskier capabilities are off by default.` | **⌫ DELETE** — over-explains three labelled toggles, then brags about a safe default |
| 523 | `Companion canvas capture` | *keep* |
| 526 | `Read-only full-canvas transfer through CSP's smartphone API.` | **`Read-only. The whole canvas.`** — **"read-only" is retained here.** On the idle status line it was unsolicited reassurance; on the permission the user is being asked to *grant*, scope is specification, not a brag. The API trivia goes |
| 533 | `Allow read-only canvas transfer through Companion Mode` (toggle tooltip) | **⌫ DELETE** — duplicates the caption directly beneath it |
| 535 | `AutomationProperties.HelpText` duplicating line 526 | **⌫ DELETE** |
| 546 | `Clipboard capture` | *keep* |
| 549 | `Temporarily copies CSP pixels, then restores the previous clipboard when it is still safe.` | **`Copies pixels through the clipboard, then restores it when it can.`** — the hedge is **retained in shortened form**. It is not a weasel; the app genuinely declines to clobber a clipboard someone else changed, and it reports that at line 390. A caption promising unconditional restoration while the status line reports the exception is worse copy, not better |
| 556 | `Allow the app to use the clipboard for Layer and Selection sources` | **⌫ DELETE** — duplicates the caption |
| 558 | `HelpText` duplicating line 549 | **⌫ DELETE** |
| 569 | `Run selected CSP Auto Action` | *keep* |
| 573 | `Required only for Selection · Canvas. Also requires clipboard access.` | **`Needed for Selection · Canvas. Requires clipboard capture.`** |
| 582 | `HelpText` duplicating line 573 | **⌫ DELETE** |
| 592 | `Selection · Canvas command` | **⌫ DELETE** — the caution caption below the toggle already scopes it, and the card now sits directly under the toggle it belongs to |
| 595 | `CSP exposes the command name, not the Auto Action's recorded steps. Choose only an action built from the setup guide.` | **`Use the action from the setup guide — CSP does not expose an action's recorded steps.`** — the second clause is **retained**. It is the reason the rule exists: the app cannot verify what a user-authored automation does, on a permission that lets it execute. A bare imperative with no reason is not obeyed |
| 598 | `The Quick Access command CSP runs for the Selection · Canvas source` (combo tooltip) | **⌫ DELETE** |
| 605 | `Refresh CSP actions` | **`Refresh`** |
| 606 | `Re-read the Quick Access commands currently enabled in CSP` | **`Re-read CSP's enabled actions`** |
| 610 | `Open setup guide` | **`Setup guide`** |
| 611 | `Open the guide for building the required Auto Action` | **⌫ DELETE** — restates the label |
| 616 | `Connect to CSP, add the action to Quick Access, then refresh.` | *keep verbatim — a genuine three-step instruction* |
| 623 | `About` | **⌫ DELETE** — the version line stands alone |
| 643 | `Stored locally` | **⌫ DELETE** — the card shows a `C:\Users\…` path; that *is* the message |
| 656 | `Show file` | *keep* |
| 657 | `Reveal settings.json in File Explorer` | **`Show settings.json in Explorer`** |
| 667 | **`Changes are saved immediately`** | **⌫ DELETE — named offender.** Narrating the absence of a Save button. If saving is instant, silence is the confirmation |

## 6.2 Companion — `MainWindow.xaml.cs`

| Line | Before | After |
|---|---|---|
| 107 | `{version} · GPL-3.0 · everything runs on this PC.` | **`{version} · GPL-3.0`** |
| 126 | `Clip Studio Paint is authenticated through local Companion Mode.` (chrome tooltip) | **⌫ DELETE** — restates the visible word "Connected" with a reassurance adverb, and the parameter carrying it is removed (§4.7 edit 4) |
| 141 | `Waiting for CSP…` / `Connecting…` (`ConnectionText`) | **`Scanning`** / **`Connecting`** (§3.3) |
| 142 | `Open Clip Studio Paint; connection will continue automatically.` | **`Open Clip Studio Paint.`** |
| 143 | `Scanning all displays for CSP's Companion Mode QR code.` | **`Checking all displays.`** |
| 145 / 151 | `Stop` / `Connect` | *keep both* |
| 146 | `Stop scanning for CSP's Companion Mode QR code` | **`Stop scanning`** |
| 147 | `Stop connecting to CSP Companion Mode` (automation name) | *keep* |
| 152 | `Scan CSP's "Connect to smartphone" QR code to link Companion Mode` | **`Scan CSP's Connect to smartphone QR code`** |
| 154 | `Connect to CSP Companion Mode` (automation name) | *keep* |
| 158 | `CSP not found` (`ConnectionText`) | **`Offline`** (§3.3) |
| 159 | `Open Clip Studio Paint, then select Connect.` | **⌫ DELETE** — the tooltip parameter is removed; the instruction lives at line 164 |
| 162 | `Open CSP to enable direct Canvas access` (heading) | **`Open Clip Studio Paint`** |
| 164 | `Start Clip Studio Paint. You can select Connect now and the app will keep waiting.` | **`Start Clip Studio Paint, then Connect.`** (38 chars) |
| 168 | `Disconnected` (`ConnectionText`) | *keep* (§3.3) |
| 171 | `CSP {session.Version} is open, but Companion Mode is not connected.` | **⌫ DELETE** — the tooltip parameter is removed |
| 172 | `Connect for direct Canvas access` (heading) | **`Connect to CSP`** |
| 174 | `In CSP, open "Connect to smartphone," leave the QR visible, then select Connect.` | **`In CSP, open Connect to smartphone, then Connect.`** (48 chars) — the **only** place the full procedure appears; it fits the 322px column on one line |
| 189 | `Stopping the connection scan…` | **⌫ DELETE — set empty** |
| 224 | `Waiting for Clip Studio Paint. Open it and the scan will continue automatically.` | **`Waiting for Clip Studio Paint.`** |
| 229 | `Waiting for CSP's QR code` (heading) | **`Waiting for CSP's QR code`** *keep* |
| 231 | `In CSP, open "Connect to smartphone" and leave the QR visible. Scanning continues until connected.` | **`In CSP, open Connect to smartphone and leave the QR visible.`** (57 chars — at the column limit; verified) |
| 255 | `The QR was found, but CSP did not accept the connection ({msg}). Retrying…` | **`CSP refused the connection. Retrying…`** — the raw `{msg}` is engineering text; it is logged, not shown |
| 329 | `Check the color counts` | **`Check the counts`** |
| 330 | `Major colors must be 1–20 and minor colors must be 0–20.` | **`Major 1–20, minor 0–20.`** |
| 337 | `CSP is not running` / `Open Clip Studio Paint and a document, then try again.` | *keep* / **`Open Clip Studio Paint and a document.`** |
| 347 | `Preparing palette` | *keep* |
| 350 | `Loading the complete visible canvas from CSP…` | **⌫ DELETE — empty** |
| 351 | `Waiting for CSP to provide the requested pixels…` | **⌫ DELETE — empty** |
| 365 | `Loading image` | *keep* |
| 367 | `Analyzing {W} × {H} loaded pixels locally…` | **`{W} × {H}`** |
| 375 | `Analyzing pixels` / `Writing the extracted colors to a CSP-compatible palette file…` | *keep* / **⌫ DELETE — empty** |
| 386 | ` Only {n} distinct eligible colors were available.` | **` Only {n} distinct colors available.`** — genuinely explains why the result differs from the request; **keep** |
| 390 | ` The clipboard changed during extraction and was not overwritten.` | **` Clipboard changed during extraction; not restored.`** — material side effect; **keep** |
| 392 | ` Canvas pixels came directly from local CSP Companion Mode.` | **⌫ DELETE** — appended to *every* success; pure route-bragging |
| 398 | ` Selection came only from the active layer.` | **⌫ DELETE** — restates the source the user picked three seconds ago |
| 403 | ` Drop the palette file onto CSP's Color Set palette.` | **⌫ DELETE** — verbatim duplicate of the chip appearing at the same instant |
| 405 | `{n} colors ready` | *keep* |
| 408 | `No eligible colors` / `The source contains no opaque mid-range pixels after filtering.` | *keep* / **`No opaque mid-range pixels after filtering.`** — "after filtering" is **retained**; it is the only hint that the app's own threshold caused this, on a canvas the user can plainly see has pixels |
| 412 | `Extraction failed` / raw `exception.Message` | *keep* / `ReadableMessage(exception)` (existing helper) |
| 451 | `Changes the current drawing color in connected Clip Studio Paint.` (`HelpText`) | **⌫ DELETE** — the automation *Name* already says it |
| 443 | `{name} — {hex}` (swatch tooltip) | **`{name} · {hex} — set as CSP color`** — this is now the only sighted surface carrying the click affordance |
| 466 | `Connect to use live colors` / `Select Connect, show CSP's smartphone QR code, then choose the swatch again.` | **`Not connected`** / **`Connect, then choose the swatch again.`** |
| 477 | `The selected swatch is now Clip Studio Paint's current drawing color.` | **⌫ DELETE** — the line directly above reads `CSP color set to #72D2B1` |
| 485 | `Could not set the CSP color` | *keep* |
| 511–516 | `Stage 1 of 2 · Loading image…` / `Stage 2 of 2 · Analyzing pixels…` | **⌫ DELETE — with `ProgressStageText` and the literal-string `switch`** |
| 601 | `The whole visible canvas, read directly through Companion Mode.` (enabled Canvas tooltip) | **⌫ DELETE the tooltip in the enabled branch** — it duplicated `SourceHelp` verbatim one row above. `UpdateSourceTooltips` sets `ToolTip = null` when enabled |
| 604 | `The active layer only.` (enabled) | **⌫ DELETE — `null`** |
| 607 | `Only the selected pixels on the active layer.` (enabled) | **⌫ DELETE — `null`** |
| 610 | `The visible composite inside the selection, via your chosen CSP action.` (enabled) | **⌫ DELETE — `null`** |
| 602 | `Turn on "Companion canvas capture" in Settings to use this source.` | **`Turn on Companion canvas capture in Settings.`** — **keep**, this is the disabled control's only escape hatch |
| 605, 608, 612 | `Turn on "Clipboard capture" in Settings to use this source.` | **`Turn on Clipboard capture in Settings.`** — keep |
| 614 | `Turn on "Run selected CSP Auto Action" in Settings to use this source.` | **`Turn on Run selected CSP Auto Action in Settings.`** — keep |
| 615 | `Choose a CSP Quick Access action in Settings to use this source.` | **`Choose a CSP Quick Access action in Settings.`** — keep |
| 623 | `Loads the complete visible canvas through the read-only Companion API.` | **`The whole visible canvas.`** |
| 625 | `Temporarily copies the active layer, then restores the previous clipboard when safe.` | **`The active layer, via the clipboard.`** |
| 627 | `Runs your selected CSP action and copies the visible composite inside the selection.` | **`Runs your CSP action, then copies the selection.`** — the fact that something **executes** is material; keep the verb |
| 629 | `Temporarily copies only the selected pixels on the active layer.` | **`Selected pixels on the active layer.`** |
| 655 | `Allow the app to run the CSP action you select below` | **`Runs the CSP action selected below`** |
| 656 | `Turn on Clipboard capture first — this action route needs it` | **`Requires Clipboard capture`** |
| 683 | `No Quick Access command selected.` | **`No action selected.`** |
| 684 | `Selected: {name}` | **⌫ DELETE** — the ComboBox displays it |
| 765 / 766 | `Keep this window above Clip Studio Paint` / `Window can fall behind other windows` | **`Always on top`** / **`Always on top (off)`** — the off-state narrated the *absence* of a feature |
| 796 | `Connect Companion Mode first, then refresh CSP actions.` | **`Connect first, then refresh.`** |
| 801 | `Reading enabled Quick Access commands…` | **`Reading CSP actions…`** |
| 835 | `CSP returned no enabled Quick Access commands.` | **`CSP returned no enabled actions.`** |
| 837 | `Found {n} commands. Choose the action created from the setup guide.` | **`{n} actions. Choose the one from the setup guide.`** |
| 838 | `Verified "{name}" is currently enabled in CSP.` | **`{name} — enabled in CSP.`** — the confirmation that a *refresh* actually verified the action is real information, distinct from merely echoing a stored name; only "Verified"/"currently" go |
| 846 | `Could not read CSP actions: {message}` | *keep* |
| 873 | `Selected "{name}". CSP does not expose its internal steps; verify it matches the setup guide.` | **`{name}`** — the caution now lives permanently in the `AutoActionOptionsPanel` caption (§6.1 line 595), so it is on screen whether or not you just changed the selection |
| 912 | `Settings could not be saved: {message}` | *keep* |
| 924 | `The setup guide is missing from this build.` | *keep* |

## 6.3 Companion — `CspAcquisitionService.cs`

These strings reach `DetailText` via `SetFailure(..., acquisition.Error!)` and `acquisition.Notice`. **They are the app's entire failure and route vocabulary and no prior audit covered them.**

| Line | Before | After |
|---|---|---|
| 80 | `Direct Companion canvas capture is disabled in Settings.` | **`Companion canvas capture is off. Turn it on in Settings.`** |
| 87 | `Clipboard capture is disabled. Enable it in Settings to use layer sources.` | **`Clipboard capture is off. Turn it on in Settings.`** |
| 95 | `Clipboard capture is disabled. Selection · Canvas needs temporary clipboard access.` | **`Clipboard capture is off. Selection · Canvas needs it.`** |
| 101 | `Auto Action execution is disabled. Enable it and choose a CSP command in Settings.` | **`Auto Action execution is off. Turn it on in Settings.`** |
| 107 | `Choose the merged-selection CSP Quick Access command in Settings first.` | **`Choose a CSP Quick Access action in Settings.`** |
| 137–138 | `Direct Companion canvas capture failed. Clipboard fallback is disabled in Settings. Companion Mode said: {msg}` | **`Companion capture failed and clipboard fallback is off. {msg}`** |
| 145 | `The clipboard image contains no opaque mid-range pixels.` | **`No opaque mid-range pixels after filtering.`** — unified with lines 232, 233, 345 and `MainWindow.xaml.cs:408`; **one phrasing for one condition** |
| 154 | `Companion Mode was unavailable, so the merged clipboard image was used.` (`Notice`) | **`Used the clipboard image; Companion Mode was unavailable.`** — this is a **route change the user did not request** and materially affects what was extracted; **keep**, tightened |
| 159–160 | `Select Connect for direct Canvas access, or copy a merged canvas image first. Companion Mode said: {msg}` | **`Connect, or copy a merged canvas image first. {msg}`** |
| 178 | `Clip Studio Paint could not be activated.` | *keep* |
| 185 | `Clip Studio Paint did not remain the active window, so no keyboard commands were sent.` | **`CSP lost focus; no keyboard commands were sent.`** |
| 190 | `Windows could not send Copy to Clip Studio Paint.` | **`Could not send Copy to Clip Studio Paint.`** |
| 201 | `CSP did not copy any pixels. Select a layer with visible pixels inside the selection and try again.` | **`CSP copied nothing. Select a layer with visible pixels inside the selection.`** |
| 202 | `CSP did not copy the active layer. Check that the layer contains visible pixels and try again.` | **`CSP copied nothing. Check the active layer has visible pixels.`** |
| 232 | `The copied selection contains no opaque mid-range pixels.` | **`No opaque mid-range pixels after filtering.`** |
| 233 | `The active layer contains no opaque mid-range pixels.` | **`No opaque mid-range pixels after filtering.`** |
| 239 | `No selection pixels were copied. Create a selection in Clip Studio Paint, then try again.` | **`Nothing copied. Create a selection in Clip Studio Paint.`** |
| 240 | `The active layer did not provide a clipboard image.` | **`The active layer produced no clipboard image.`** |
| 261–262 | `CSP copied {w} × {h}px, but the canvas is {cw} × {ch}px. An active selection may be cropping the source, so extraction stopped.` | **`CSP copied {w} × {h}, but the canvas is {cw} × {ch}. A selection may be cropping the source.`** |
| 271 | `No bounded selection was detected. Create a selection smaller than the full canvas in Clip Studio Paint, then try again.` | **`No bounded selection. Create one smaller than the full canvas.`** |
| 314–315 | `CSP ran "{name}", but it did not copy pixels. Confirm that a bounded selection overlaps visible artwork.` | **`CSP ran "{name}" and copied nothing. Check the selection overlaps visible artwork.`** |
| 345 | `CSP ran "{name}", but the copied selection contains no opaque mid-range pixels.` | **`No opaque mid-range pixels after filtering.`** |
| 351 | `CSP ran "{name}", but the clipboard did not contain an image.` | **`CSP ran "{name}" and produced no image.`** |
| 358 | `Visible selection copied through CSP Quick Access ("{name}").` (`Notice`) | **⌫ DELETE — `Notice = null`** — this restates the source the user selected three seconds ago; the identical sin the route-brag at `MainWindow.xaml.cs:392` is deleted for |

**Composed success-detail budget, verified against the 210-character / 3-line reservation:**

```
"12 major + 6 minor colors."                                        26
" Only 9 distinct colors available."                               +34   →  60
" Clipboard changed during extraction; not restored."              +51   → 111
" Used the clipboard image; Companion Mode was unavailable."       +57   → 168
                                                        worst case  168  ≤ 210 ✔
```
The worst realistic composition fits three lines with 42 characters to spare. The route brag (−58), the selection semantics (−42), the trailing drop instruction (−52), and the Quick Access notice (−60) are what created that headroom.

## 6.4 Companion — `ComboBox` template strings

| Before | After |
|---|---|
| `Select an action…` | **`Select an action`** |
| `No CSP actions loaded` | **`No actions loaded`** |

## 6.5 Mux — every user-visible string

| Source | Before | After |
|---|---|---|
| `MainForm.cs:47` | `CSP Mux` (window title / wordmark) | *keep* |
| `ThemeControls.cs:355` | `M` (gradient monogram tile) | **⌫ DELETE the tile** — an app-store gradient monogram is the "this is AI" tell, and the indigo dies with the accent decision. The wordmark stands alone, matching the Companion |
| `MainForm.cs:138` | `SHARED COMPANION ACCESS` | **⌫ DELETE** — a marketing eyebrow over the only card in the window |
| `SettingsForm.cs:54` | `CONNECTION` | **⌫ DELETE** |
| `SettingsForm.cs:79` | `QR DISPLAY` | **⌫ DELETE** (all three eyebrows go, retiring `AccentLight`) |
| `MainForm.cs:142` | `Scan the proxy QR` (card title) | **⌫ DELETE** — the instruction now lives in `InstructionText` where it is state-accurate, instead of tripling with the button and the placeholder |
| `MainForm.cs:17` | `Proxy QR` (placeholder title) | **⌫ DELETE** |
| `MainForm.cs:19` | `Your shared connection code will appear here` | **⌫ DELETE** — narrating an empty box, in grey on white at 4.4:1 (fails AA) |
| `MainForm.cs:12` | `Ready when you are` | **⌫ DELETE** — chirpy anthropomorphism; the state is `Not sharing` |
| `MainForm.cs:9` / `:410` | `Offline` / `Not connected` (two words, one state) | **`Offline`** in `ConnectionText`; **`Not sharing`** in `StatusText`. Two *different* facts — the upstream link and the proxy session — now have two accurate words instead of two words for one fact |
| `MainForm.cs:14` | `Open CSP Companion Mode, then scan its QR.` | *keep verbatim — the one idle instruction* |
| `MainForm.cs:408` | `Open CSP Companion Mode, then scan its QR to start sharing.` | **⌫ DELETE** — duplicate of the above with a filler clause |
| `MainForm.cs:232` | `Looking for CSP` | **`Scanning displays`** |
| `MainForm.cs:233` | `Scanning your active displays for Clip Studio Paint's real Companion QR.` | **⌫ DELETE the detail.** `InstructionText` reads **`Leave CSP's QR visible.`** — an instruction, not narration. *(Note: the Companion's equivalent at line 143 becomes `Checking all displays.` because it is a tooltip on the Connect button, a different surface with a different job. Both apps run the same scanner and neither narrates it in `DetailText`.)* |
| `MainForm.cs:242` | `Connecting securely` | **`Authenticating`** — "securely" is an unsolicited safety brag; nobody advertises an insecure connect |
| `MainForm.cs:243` | `The CSP QR was found. Authenticating the single upstream connection now.` | **⌫ DELETE** — narrating internal architecture at the user |
| `MainForm.cs:272` | `Scan to connect` (activity title) | **⌫ DELETE** — `StatusText` reads **`Sharing`**; the instruction is in `InstructionText` |
| `MainForm.cs:272` | `Proxy online` (`topStatus`) | **`Connected`** in `ConnectionText`, **`Sharing`** in `StatusText`. Two surfaces, two facts, no duplication |
| `MainForm.cs:268` | `Ready for apps on this computer. Let each app scan this proxy code.` | **`This computer only.`** |
| `MainForm.cs:269` | `Ready on {addr}. Phones must be on the same Wi-Fi.` | **`{addr} · same Wi-Fi`** — a real, actionable constraint; keep it |
| — | *(new)* `InstructionText` in Online | **`Scan this code from each app you want to connect.`** — the instruction the deleted card title used to carry, now shown in the state where it is true |
| `MainForm.cs:338` | `QR hidden · sharing active` | **⌫ DELETE** — `StatusText` still reads `Sharing`; `PrimaryButton` reads `Show QR` |
| `MainForm.cs:339` | `Existing connections continue normally. Show the QR whenever another app wants to join.` | **⌫ DELETE.** `InstructionText` reads **`Show the QR to connect another app.`** |
| `MainForm.cs:335` | `Connected apps remain online` | **⌫ DELETE** — the same reassurance a second time on one screen |
| `MainForm.cs:334` | `QR hidden` (placeholder title) | **⌫ DELETE** — the ghost frame is self-evident |
| `MainForm.cs:287` | `Could not start sharing` | **`Connection failed`** |
| `MainForm.cs:288` | `Check that CSP's QR is visible and that the selected network is available.` | *(replaced by the exception map below; this exact sentence survives as the catch-all `InstructionText`, trimmed to* **`Check CSP's QR and the selected network.`**) |
| `MainForm.cs:318` | `Settings saved` | **⌫ DELETE the
entire state** — this is the Mux's `Changes are saved immediately`, but louder: it hijacks the main status area to announce a save |
| `MainForm.cs:320` | `New proxy sessions will accept apps on this computer.` | **⌫ DELETE** |
| `MainForm.cs:321` | `New proxy sessions will also accept devices through {addr}.` | **⌫ DELETE** |
| `MainForm.cs:23` | `Scan CSP QR` | *keep* |
| `MainForm.cs:225` | `Scanning…` (disabled button) | *keep* |
| `MainForm.cs:24` | `Cancel scan` | **`Cancel`** — two verbosity registers for one verb |
| `MainForm.cs:399` | `Hide QR` / `Show QR` | *keep both* |
| — | *(new)* `SecondaryButton` in Online / QrHidden | **`Stop`** — fixes the terminal Online state (§5.3) |
| `MainForm.cs:11` | `0 apps` / `{n} app` / `{n} apps` | *keep* |
| `MainForm.cs:29` | `Settings` (header button) | *keep as a tooltip on the 28px icon button* |
| `SettingsForm.cs:17` | `CSP Mux Settings` | **⌫ DELETE** — dead string; the form was borderless and never rendered it |
| `SettingsForm.cs:24` | `Settings` (dialog title) | *keep as the page title* |
| `SettingsForm.cs:32` | `Choose where companion apps can reach the proxy.` | **⌫ DELETE** — restates the single card directly below it |
| `SettingsForm.cs:57` | `Connection scope` | *keep — names the control* |
| `SettingsForm.cs:61` | `Choose a private network for phones and tablets, or keep access on this PC.` | **`Loopback keeps the proxy on this PC. A private network lets phones reach it.`** — **not deleted.** The prior audit deleted it because it "restates the dropdown options verbatim", then rewrote those options to bare `{address} · {name}`, leaving nothing anywhere that says which choice phones can reach. This caption is now the only place that information exists, and it is genuine instruction |
| `SettingsForm.cs:82` | `Automatically hide the QR after the first app connects` | **`Hide QR after first connection`** |
| — | *(new)* QR-display card caption | **`The proxy keeps running.`** — one line, because "hide" could reasonably be read as "stop" |
| `SettingsForm.cs:8` | `Save settings` | **⌫ DELETE** — save-on-change (G9) |
| `SettingsForm.cs:9` | `Cancel` (dialog) | **⌫ DELETE** |
| `AppPreferences.cs:62` | `This computer only  ·  127.0.0.1` | **`This computer only · 127.0.0.1`** — single spaces |
| `AppPreferences.cs:80` | `Phones + desktop  ·  {address}  ·  {network.Name}` | **`{address} · {network.Name}`** — single spaces; the "phones + desktop" meaning is carried by the card caption above |
| — | *(new)* unavailable saved address | **`{address} · unavailable`** (disabled item) |
| `ThemeControls.cs:268` | `—` / `×` (`WindowButton.Text`) | **⌫ Not strings** — paint-mode discriminators. Replaced by `Path` geometry (§2.4) |

**Mux exception mapping.** These are internal engineering strings currently piped verbatim into a `MessageBox`. They are mapped to `StatusText` + `DetailText` + `InstructionText`; the full exception text is written to the log, never to the UI.

| Exception | `StatusText` | `DetailText` | `InstructionText` |
|---|---|---|---|
| `IOException` (`"Could not connect to any CLIP STUDIO companion endpoint."`) | `Connection failed` | `Could not reach CSP.` | `Check CSP's QR is visible, then scan again.` |
| `UnauthorizedAccessException` (`"CLIP STUDIO rejected companion authentication: {reason}."`) | `Connection failed` | `CSP refused authentication. {reason}` — **`{reason}` is retained**; it is the only actionable payload | `Reopen Connect to smartphone in CSP, then scan again.` |
| `SocketException` (raw `.Message`) | `Connection failed` | `Could not open a port on {addr}.` | `Choose a different network in Settings.` |
| `OperationCanceledException` with `token.IsCancellationRequested == false` | `Connection failed` | `CSP did not respond.` | `Check CSP is still running, then scan again.` |
| `InvalidOperationException` (`"LAN listening requires explicit opt-in…"`) | `Connection failed` | `That network is not usable for sharing.` | `Choose a different network in Settings.` |
| `InvalidOperationException` (`"The multiplexer is already running."`) | — | — | **never surfaced — log only** |
| `ArgumentOutOfRangeException` (`"Maximum clients must be between 1 and 64."`) | — | — | **never surfaced — log only** |
| anything else | `Connection failed` | `Sharing could not start.` | `Check CSP's QR and the selected network.` |
| `NullReferenceException` from the `operationCancellation` race | — | — | **cannot occur once the token is captured in a local** (§5.7) |

There is no `"Something went wrong."` anywhere. Every catch-all names the operation that failed and gives a next action.

## 6.6 Copy audit — coverage statement

Enumerated from source, by file:

| File | Rows | Deleted | Rewritten | Kept verbatim |
|---|---|---|---|---|
| `MainWindow.xaml` | 54 | 22 | 22 | 10 |
| `MainWindow.xaml.cs` | 57 | 16 | 33 | 8 |
| `CspAcquisitionService.cs` | 23 | 1 | 21 | 1 |
| `App.xaml` (combo template) | 2 | 0 | 2 | 0 |
| Mux — all files | 44 | 22 | 15 | 7 |
| **Total** | **180 rows** | **61** | **93** | **26** |

Rows, not strings — five rows cover a group (four stepper tooltips, three `HelpText` duplicates, four segment labels, four button labels, three network-choice formats), so the string count is higher. The row count is what an implementer edits.

---

# 7. ICON SPECIFICATION

One glyph: **two interlocking rings**, level. It means *link* for the Mux (many apps joined to one CSP) and *mix* for the Companion (two colours meeting). Same silhouette, two colourings — siblings at a glance.

**No tilt at any size.** A rotated pair reads as decoration; a level pair reads as an instrument mark. Two equal rings also refuse to imply a hierarchy between the two apps.

**No container tile, no rounded-square badge, no background fill, no gradient, no bevel.** Transparent throughout. Windows 11's taskbar and Alt-Tab composite better without a tile, and a tile would reintroduce exactly the rounded surface G6 just removed.

## 7.1 Master geometry — 256 × 256

| Property | Value |
|---|---|
| Canvas | 256 × 256, transparent |
| Stroke width | **24** |
| Centreline radius **r** | **56** |
| Left ring centre **A** | **(96, 128)** |
| Right ring centre **B** | **(160, 128)** |
| Centre distance **d** | **64** |
| Centreline overlap | `2r − d = 112 − 64 = ` **48** |
| Outer radius | `56 + 12 = 68` → glyph extents **x ∈ [28, 228]**, **y ∈ [60, 196]** |
| Inner radius | `56 − 12 = 44` |
| Optical margins | 28 left, 28 right, 60 top, 60 bottom — symmetric on both axes; the glyph is wide, so it is left/right-tight and vertically airy, which is correct for a 1:1 tile |
| Centreline intersections | `x = 128`, `y = 128 ± √(56² − 32²) = 128 ± √2112 = ` **128 ± 45.96** → **(128, 82.04)** and **(128, 173.96)** |

## 7.2 The weave

The rings **interlock**; they are not a Venn diagram. **Left ring passes over at the top crossing; right ring passes over at the bottom.**

Draw order, three operations:

1. Stroke ring **B** (full annulus, r 56, stroke 24).
2. Stroke ring **A** (full annulus) — A is now entirely on top.
3. **Restore B at the lower crossing.** Take B's annulus, intersect it with a `RectangleGeometry` covering `y > 128`, and further intersect with a disc of radius 44 centred on **(128, 173.96)** — then draw the result on top of A.

Knockout gap: A's stroke is 24 wide; expand the restored region's boundary by **8 units on each side** so a visible break of `24 + 16 = ` **40 units of arc** separates B's restored segment from A's stroke. That gap is what makes the interlock read rather than looking like a butt joint.

In XAML this is three `Path` elements inside a `DrawingGroup`; in the `.ico` master it is a flattened 1024 × 1024 render downsampled per size.

## 7.3 Colour treatments

| | Left ring (over at top) | Right ring | Background |
|---|---|---|---|
| **CSP Mux** — base, single colour | `AccentBrush` **#72D2B1** | `AccentBrush` **#72D2B1** | transparent |
| **CSP Palette Companion** — same glyph, coloured | `AccentBrush` **#72D2B1** | `WarningBrush` **#D8A25E** | transparent |
| **Both** — monochrome contexts (tray, print, high-contrast) | `TextBrush` **#F2F3F6** on dark / **#1C1D21** on light | same | transparent |

The Companion's second hue is `WarningBrush` — **an existing suite token, not a new colour**. Mint plus warm amber is the only two-colour pairing in the palette that stays distinguishable in the common deuteranopia case, and a palette tool earning a second hue is semantically honest.

**In the Mux's monochrome version the over/under weave is the *only* thing distinguishing "interlocked" from "a figure eight"** — which is why §7.2's knockout gap is specified in units rather than left to the renderer.

## 7.4 Size ladder and the 16 px strategy

Ship **16, 20, 24, 32, 48, 64, 128, 256** in each `.ico`. Sizes ≥ 24 scale the master with the stroke snapped to a whole device pixel. **16 and 20 are hand-drawn from a separate construction — do not downscale the master**; automatic downscaling of a 2 px stroke produces 1.5 px grey edges every time.

| Size | Stroke | r | Centres | d | Weave | Notes |
|---|---|---|---|---|---|---|
| 256 | 24 | 56 | (96,128) (160,128) | 64 | full | master |
| 128 | 12 | 28 | (48,64) (80,64) | 32 | full | |
| 64 | 6 | 14 | (24,32) (40,32) | 16 | full | |
| 48 | **4** | 10.5 | (18,24) (30,24) | 12 | full | stroke rounded from 4.5 |
| 32 | 3 | 7 | (12,16) (20,16) | 8 | full | crossings 11.5 px apart — still legible |
| **20** | **2** | **5** | **(6,10) (13,10)** | **7** | **dropped — union** | outer x ∈ [0, 19], y ∈ [4, 16] |
| **16** | **2** | **4** | **(5,8) (11,8)** | **6** | **dropped — union** | outer x ∈ [0, 16], y ∈ [3, 13] |

**16 px construction, hand-hinted:**

```
stroke        = 2          (exactly 2 device pixels)
centreline r  = 4          → outer r = 5, inner r = 3
centres       = (5, 8) and (11, 8)          d = 6
outer bounding box: x ∈ [0, 16], y ∈ [3, 13]     ← every edge on a whole pixel
centreline overlap = 2r − d = 8 − 6 = 2
inner-hole separation = 2·r_inner − d = 6 − 6 = 0   ← the two holes exactly touch
```

Why these numbers and not a scaled master:

- **Every stroke edge lands on an integer.** Outer edges at `5 − 5 = 0`, `11 + 5 = 16`, `8 − 5 = 3`, `8 + 5 = 13`. Circles are curves, so only the four cardinal points snap exactly — but placing the *bounding box* on the pixel grid is what removes the grey halo along the top, bottom, left and right, which is where the eye reads the shape.
- **The two inner holes touch rather than merge.** At `d = 6` and inner radius 3 the holes are exactly tangent. One pixel closer and the negative space fuses into a horizontal peanut, which stops reading as two rings. One pixel further and the overlap disappears and it reads as two adjacent circles.
- **The weave is dropped.** At a 2 px stroke, a knockout gap would be sub-pixel and renders as a grey smudge that reads as a rendering fault. The two annuli are unioned into a single path.
- **At 16 px the two apps' icons differ by colour alone.** That is intentional: at taskbar size, hue is the only channel with enough bandwidth, and the shared silhouette is what makes them read as siblings. The user identifying "the suite" matters more at 16 px than the user identifying "which app".

**20 px** uses the same construction at `r = 5`, `stroke = 2`, `d = 7`, centres `(6,10)` and `(13,10)` → outer x ∈ [0, 19] (1 px right margin), y ∈ [4, 16].

**Rendering flags** for any in-app vector use: `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, `RenderOptions.EdgeMode` left at the default (antialiased — `Aliased` would jag the circles).

## 7.5 Where the glyph appears

- **`Assets\csp-palette-companion.ico`** and **`Assets\csp-mux.ico`**, referenced by `<ApplicationIcon>` in both csprojs. Neither repo has an icon asset today; the Mux currently ships the default .NET exe icon.
- **Nowhere inside either window.** Not in the title bar (a 16 px mark beside a 13 px wordmark is the app-store product-page register requirement 4 deletes), and not as a watermark in the Mux's empty QR frame (a decorative brand glyph parked in dead space is the same register wearing different clothes). The empty QR frame is empty.

---

# 8. IMPLEMENTATION ORDER

Eleven phases. Each ends in a buildable, runnable state. Do not begin a phase until the previous one runs.

### Phase 1 — Shared theme project
1. Create the `csp-suite-theme` repository with the layout and csproj in §5.10.
2. Write `Theme.xaml`: §1.2 brushes → §1.3 type styles → §1.4/§1.5/§1.6 tokens as `sys:Double`/`CornerRadius`/`Thickness` resources → §2 control styles and templates in the order §2.1 … §2.18.
3. Add the submodule to both app repos; add the `ProjectReference` and the `.sln` entry to both.
4. **Gate:** the theme project compiles (BAML validates every `{StaticResource}` key).

### Phase 2 — Companion theme swap, no layout change
5. Replace `App.xaml` with the eight-line merged-dictionary file (§5.9).
6. Add `FieldLabelStyle` consumers: change `MajorLabel`/`MinorLabel` to `Style="{StaticResource FieldLabelStyle}"`.
7. Retarget the removed keys: `SectionLabelStyle` → delete its one consumer (the "SOURCE" label, deleted anyway); `QuietTextStyle` → `CaptionTextStyle`.
8. **Gate:** the Companion builds and launches with the old layout and the new tokens. Every control renders; nothing is black-on-black; no `ResourceReferenceKeyNotFoundException`. Exercise every screen and every disabled state before proceeding.

### Phase 3 — Companion code-behind
9. Apply §4.7 edits 1–5 in order. Each is independently compilable.
10. **Gate:** builds clean under `TreatWarningsAsErrors`. Manual pass: extract, fail an extraction, click a swatch, open settings, toggle each permission.

### Phase 4 — Companion relayout
11. Rewrite `MainWindow.xaml` per §4.1–§4.5. Keep every `x:Name` and every event-handler attribute.
12. Change the window dimensions — **all six properties** (§3.1).
13. Add `WindowChrome` per §3.1; delete the outer 1px `Border`.
14. **Gate:** every sum in §4.1 reproduces on screen. Verify at 100 %, 125 % and 150 % scaling. Verify all four tray states.

### Phase 5 — Companion shell finishing
15. `app.manifest` (§3.1). Delete `<ApplicationHighDpiMode>` from the csproj.
16. `NativeMethods.ApplyRoundedCorners` + the `SourceInitialized` call.
17. Window position persistence (§3.5): `AppSettings` members, restore, save.
18. **Gate:** the window remembers its position across restarts; unplugging the saved monitor falls back to centre; corners are rounded on Windows 11 and square without error on Windows 10.

### Phase 6 — Companion copy
19. Apply §6.1, §6.2, §6.3, §6.4 in file order.
20. **Gate:** grep both apps for the wordlist below (§9.3). Zero hits outside the retained-with-reason set.

### Phase 7 — Mux project skeleton
21. New `App.xaml` + `App.xaml.cs` (`ApplicationDefinition`), `ShutdownMode="OnMainWindowClose"`.
22. **Delete** `Program.cs`, `ThemeControls.cs`, `MainForm.cs`, `SettingsForm.cs`, `CompanionQrScanner.cs`.
23. Copy the Companion's `CompanionQrScanner.cs` in; adjust the namespace; add `using System.Drawing;` and `using System.Windows.Forms;`.
24. New `app.manifest`; rewrite the csproj per §5.8.
25. Empty `MainWindow.xaml` with just the shell (§3.2) and a placeholder body.
26. **Gate:** the Mux builds, launches, shows a themed empty window, drags, minimises, closes.

### Phase 8 — Mux state machine and plumbing
27. `ConnectionState` enum + `ApplyState`, called from the constructor with `Idle` (§5.3).
28. Port `StartAsync`/`StopAsync` with the captured token, the `isBusy` latch, and the `OperationCanceledException` branch (§5.7).
29. `MultiplexerOnClientCountChanged` with the shutdown guard (§5.6); preserve the unsubscribe-before-dispose order.
30. `OnClosing` with the `Dispatcher.Yield` hop and the `closeInProgress` latch (§5.7).
31. `AppPreferences` gains `WindowLeft`/`WindowTop`; `Save` gains a `try/catch`.
32. **Gate:** connect, cancel mid-scan, stop while online, and close from idle, from scanning, and from online — six exits, no crash, no leaked socket. Verify the idle-close path specifically; that is the one that throws in WPF.

### Phase 9 — Mux views and QR
33. `MainWindow.xaml` main view per §5.2; `SettingsView` per §5.4.
34. `ProxyQrRenderer` rewrite (§5.5) + the integer-pitch sizing code.
35. `NetworkScopePicker` behaviour: background load, disabled-while-running, unavailable-address item.
36. **Gate:** scan a real code with a phone at 100 %, 125 % and 150 % scaling. Verify every row of the §5.3 state table. Verify §5.2's sum on screen.

### Phase 10 — Mux copy
37. Apply §6.5, including the exception map.
38. **Gate:** §9.3 wordlist grep, plus the composite-state read-through in §9.2.

### Phase 11 — Icons
39. Author the 256 master (§7.1–§7.3) as SVG; hand-draw the 16 and 20 variants (§7.4).
40. Build both `.ico` files with all eight sizes; add `<ApplicationIcon>` to both csprojs.
41. **Gate:** both icons legible and distinguishable at 16 px in the taskbar, Alt-Tab, and Explorer.

---

# 9. DEFINITION OF DONE

## 9.1 Companion checklist

**Contract — breaks silently if missed**

- [ ] All **49** `x:Name`s present with compatible types.
- [ ] `MajorCount` / `MinorCount` are `TextBox`; `PalettePreview` is a raw `Panel` with an addable `Children`; `PaletteDragChip` and every runtime swatch are `Button`; `ConnectionDot` / `StatusDot` are `Shape`; `ConnectionPanel` / `StatusPanel` expose `BorderBrush`; `AutoActionPicker` is a `Selector`; all three permission toggles derive from `ToggleButton`.
- [ ] `AccentBrush`, `WarningBrush`, `ErrorBrush`, `BorderBrush`, `SubtleBrush`, `PanelBrush`, `AccentStatusBrush`, `WarningStatusBrush`, `ErrorStatusBrush` all resolve as `SolidColorBrush`; `SwatchButtonStyle` resolves as a `Style` with `TargetType="Button"`. **A `Color` resource or a `DynamicResource`-only definition throws `InvalidCastException` at first extraction.**
- [ ] `SwatchButtonStyle`'s template `TemplateBinding`s **`Background`, `BorderBrush` and `BorderThickness`**. Verified by extracting a palette and confirming 12 distinct colours, not 12 identical chips.
- [ ] `SegmentRadioStyle` and `ToggleSwitchStyle` carry `ToolTipService.ShowOnDisabled="True"`; `AutoActionPermissionToggle` keeps its inline one. Verified by turning off both capture permissions and hovering all four disabled radios.
- [ ] All six live regions carry `AutomationProperties.LiveSetting` — five `Polite`, `SettingsNoticeText` `Assertive`.
- [ ] `ExtractButton` keeps `IsDefault="True"`; `ExtractButton_Click` keeps the `MainView.Visibility` guard. Verified by pressing Enter on the settings page and confirming nothing extracts.
- [ ] `Window_PreviewKeyDown` still reads `SettingsView.Visibility` for Esc, and still invokes `BackButton_Click(BackButton, e)` with a `KeyEventArgs` — so `BackButton_Click`'s second parameter is still `RoutedEventArgs`.
- [ ] Every title-bar **button** carries `IsHitTestVisibleInChrome="True"`; the wordmark, `ConnectionDot` and `ConnectionText` **do not**. Verified by dragging the window from the wordmark, from the empty spring, and from beside the dot.
- [ ] Title-bar `Grid` has a non-null `Background`; `CaptionHeight="40"` equals row 0's height.
- [ ] `ConnectButton.Content` is still plain-string-assignable (the 2 s poll rewrites it).
- [ ] `UpdateStepperAvailability`'s `IsInitialized` guard is present; nothing calls it before `EndInit`.
- [ ] `ProgressStageText` and its literal-string `switch` are **both** gone; no `CS0103`.
- [ ] `SettingsPathText` keeps `TextTrimming="CharacterEllipsis"` **and** `TextWrapping="NoWrap"`. Verified with a real `%LOCALAPPDATA%` path.
- [ ] The implicit `TextBlock` style keeps `TextWrapping="Wrap"`; every `LineHeight`-bearing style keeps `LineStackingStrategy="BlockLineHeight"`.
- [ ] `SetStatus`'s `DetailText.ToolTip = detail` mirror is intact.

**Layout — measured, not eyeballed**

- [ ] Window is exactly 460 × 620 and cannot be resized. All six size properties changed.
- [ ] Every row height in §4.1 measured on screen at 100 %.
- [ ] All four tray states measured: 175 / 243 / 195 / 127.
- [ ] 20 major + 20 minor extracted while connected → **4 rows, 11 per row, no scrollbar**.
- [ ] Settings page: base stack 252, options open 434, options + notice 496, all inside a 515 viewport; nothing clips; the scrollbar appears only if the notice wraps past four lines.
- [ ] The whole app re-measured at **125 %** and **150 %**. No clipped glyph, no lost text line, no shifted caption button.

**Behaviour**

- [ ] Status tone: idle neutral, extracting amber, success mint, failure red — dot, outline and fill all change together.
- [ ] `BusyIndicator` visible only during extraction; the sliver enters and exits cleanly with no stall; `IsIndeterminate` is `false` whenever it is collapsed (verify with a profiler that no storyboard runs at idle).
- [ ] Keyboard focus draws a ring on every interactive control; a mouse click draws none.
- [ ] Disabled radios are readable (4.68:1) and their unlock tooltips appear on hover.
- [ ] Window position survives a restart; a saved position on a removed monitor falls back to centre.

## 9.2 Mux checklist

**Port correctness**

- [ ] `Program.cs` deleted; single entry point; no `CS0017`.
- [ ] `<Using Remove="System.Drawing" />` **and** `<Using Remove="System.Windows.Forms" />` both present; every remaining `System.Drawing`/WinForms use is explicitly `using`-ed or fully qualified.
- [ ] `UseWPF` **and** `UseWindowsForms` both `true`.
- [ ] **Close from idle without ever connecting** — the path where `StopAsync` has no awaits and `Close()` re-enters. No `InvalidOperationException`.
- [ ] Alt+F4 during teardown does not start a second teardown.
- [ ] `Application.Shutdown()` is called nowhere; `ShutdownMode="OnMainWindowClose"`.
- [ ] `ClientCountChanged` marshals with the `HasShutdownStarted` guard; disconnect a client during window close and confirm no fault.
- [ ] `StopAsync` unsubscribes **before** `DisposeAsync`.
- [ ] `StartAsync` captures `var token = operationCancellation.Token` once; press Cancel at the exact moment a scan completes, repeatedly, and confirm no NRE.
- [ ] A 15 s upstream timeout produces the `Failed` state with `CSP did not respond.`, not a silent return to idle. (Reproduce by suspending CSP mid-authenticate.)
- [ ] `MessageBox` appears nowhere in the codebase.
- [ ] `AppPreferences.Save` cannot throw out of a handler; make the settings directory read-only and confirm the notice appears instead of a crash.
- [ ] `NetworkScopePicker` is disabled while a session is running, with a tooltip; a saved-but-unavailable address appears as a disabled item.

**QR**

- [ ] `QrCodeEncodingOptions.Margin == 4`; `Width`/`Height` left at 0 so `Encode` returns the natural module matrix (not a 300×300 one).
- [ ] `QrImage` size is an exact integer multiple of the module count.
- [ ] `RenderOptions.BitmapScalingMode="NearestNeighbor"` set.
- [ ] The `BitmapSource` is `Freeze()`d.
- [ ] If the GDI fallback is used anywhere: `DeleteObject` in a `finally`. **Verify GDI handle count is flat across 50 start/stop cycles** in Task Manager's "GDI objects" column.
- [ ] A real phone scans the code at 100 %, 125 % and 150 % scaling, at arm's length, in normal room light.

**Layout and state**

- [ ] Window exactly 460 × 620, unresizable, and **pixel-identical to the Companion** in the title-bar band — verify by screenshotting both and diffing the top 41 rows.
- [ ] §5.2's sum reproduces: 56 + 12 + 323 + 12 + 100 + 12 + 40 = 555.
- [ ] The QR frame never moves or changes size between states.
- [ ] Every cell of the §5.3 state table verified by driving each transition.
- [ ] `Stop` returns Online → Idle.
- [ ] Settings is reachable while online.
- [ ] The 125 % clipping bug is gone: all four caption buttons fully visible, the whole layout intact.

## 9.3 Suite-wide checklist

- [ ] Both apps merge the **same** `Theme.xaml` file (verify by editing one hex in the submodule and seeing both change).
- [ ] Both apps 460 wide, 620 tall, 40 px title bar, identical column structure.
- [ ] Both use the same six connection words: `Offline` · `Disconnected` · `Scanning` · `Connecting` · `Connected` · `Failed`.
- [ ] No `Bold` anywhere: `grep -rn 'FontWeight="Bold"\|FontStyle.Bold' src external`.
- [ ] No `Opacity` on a text-bearing disabled state: `grep -rn 'Opacity' external/csp-suite-theme` — the only permitted hit is `SwatchChrome` at 0.5.
- [ ] No `DropShadowEffect` anywhere.
- [ ] No `ColorAnimation` anywhere (G13 — a brush animation on a shared dictionary brush mutates the singleton).
- [ ] No `Canvas` and no `Margin`-as-absolute-position in either `MainWindow.xaml`.
- [ ] Both manifests carry the `windowsSettings` DPI block **and** the Windows 10 `supportedOS` GUID.
- [ ] Both csprojs carry `<ApplicationIcon>` and both `.ico` files exist with all eight sizes.
- [ ] Both files saved UTF-8 with BOM; `…`, `'`, `·` render correctly.

**Copy lint — run this grep over both repos.** Every hit must be on the retained-with-reason list, or it is a defect:

```
grep -rniE 'securely|directly|automatically|seamlessly|simply|everything runs|no artwork|
            saved immediately|locally|read-only|exactly what|riskier|ready when|
            your active|real Companion|will appear here|remain online|continue normally|
            something went wrong' src
```

**Retained-with-reason — the complete allowlist:**

| String | Where | Why it survives |
|---|---|---|
| `Read-only. The whole canvas.` | Companion permission caption | Scope specification on a permission the user is granting, not a brag on a status line |
| `Copies pixels through the clipboard, then restores it when it can.` | Companion permission caption | The hedge is accurate — the app declines to clobber a clipboard someone else changed, and reports that at line 390 |
| `Use the action from the setup guide — CSP does not expose an action's recorded steps.` | Companion Auto Action caption | The second clause is the reason the constraint exists, on a permission that executes user-authored automation |
| `Loopback keeps the proxy on this PC. A private network lets phones reach it.` | Mux network card caption | The only surface stating which choice phones can reach, once the option labels became bare addresses |
| `No opaque mid-range pixels after filtering.` | Companion failure detail | "after filtering" is the only hint that the app's own threshold caused this |
| `{addr} · same Wi-Fi` | Mux online detail | A real, actionable constraint |
| `Connect to CSP, add the action to Quick Access, then refresh.` | Companion settings | A genuine three-step instruction |
| `1–20 · ↑↓ or wheel` / `0–20 · ↑↓ or wheel` | Companion stepper tooltips | The wheel affordance is genuinely undiscoverable, and the two ranges genuinely differ |

**Composite-state read-through.** For each state below, screenshot the whole window and read every simultaneously visible string together. No two surfaces may say the same thing:

*Companion:* idle-disconnected · idle-CSP-not-running · scanning · connected-idle · extracting · success · failure · settings-all-off · settings-all-on-options-open · settings-save-failed.

*Mux:* Idle · Scanning · Connecting · Online · QrHidden · Failed · settings-stopped · settings-running.

---

# 10. THE SINGLE BIGGEST TRADE-OFF

**Forcing the Mux into the Companion's 460 × 620 form-shaped shell costs the QR 11 % of its linear size and costs the Mux the one composition it was actually good at.**

The Mux today is a 580 × 720 window whose entire purpose is a 334 × 334 code that a phone camera has to resolve across a desk. This specification shrinks the frame to 300 × 300 and renders inside it at 245 px (an integer 5 device-pixels-per-module), and it subordinates the code: the QR now sits in the middle slot of a five-block stack whose top is an instruction strip and whose bottom is a status strip and a button row — a chrome sandwich borrowed from an app that has a form to fill in. The Mux has no form. It has one object and one verb.

Three specific costs, accepted with eyes open:

1. **Physical module size drops ~19 %, from ≈6.2 device px to exactly 5.0 at 100 % DPI.** The offsetting fixes are real and larger: an *integer* module pitch with `NearestNeighbor` (versus today's non-integer `PictureBox.Zoom` resample, which blurs every module edge), and a quiet zone that goes from **2 modules — out of spec** to ≈9.5 modules, 2.4× the spec minimum. Net scan reliability improves. But on a small laptop panel at 150 % DPI the code is physically smaller than it was, and a phone at arm's length in poor light will fail marginally more often. **If a real user reports scan failures, the correct escape hatch is a full-window QR mode** — click the code and it fills the 428 × 428 content box with the strips hidden — **not widening the window and breaking the suite.**

2. **The Mux loses its "one big object" composition,** which is honestly the more confident of the two current layouts. Five blocks read as a tool; one card read as a purpose. We trade poise for recognisability.

3. **The Mux's idle state shows a 300 × 300 empty outlined frame** for 47 % of the content height. It is a ghost of the exact object that is about to arrive — the same device as the Companion's ghost swatch row — and it carries no text, no icon, and no watermark. That is the right call, but it is still a large quiet area in the app's most common state, and a user who has never seen the code appear has to take the frame on faith.

Two smaller admissions:

- **The Companion's settings page ends with ~263 px of empty viewport** in its base state. It is anchored top, unbracketed, inside a `ScrollViewer`, so it reads as a page that ran out of things to say rather than as a hole between two groups — which is the honest description, because there genuinely are only three permissions. The alternatives (resizing the window per view, or inventing filler) are both worse, and the second is precisely the register this whole document deletes.

- **`DividerBrush` at `#33363D` (≈1.6:1) is the weakest structural signal in the suite,** carrying the three permission rows and the stepper's internal hairlines. It is deliberately brighter than the 1.3:1 value a pure hairline system would want, because those rows sit *inside* a filled, outlined card that survives a bad monitor on its own. If the dividers still prove invisible on a cheap TN panel, **raise `DividerBrush` to `#3C4048` — the same value as `BorderBrush`.** That single token change is the first and only retreat this design needs, and it costs nothing but a little calm.

**What buys all of it back:** every alternative that preserves the Mux's composition — a wider window, its own title bar, a modal settings dialog, a QR that owns the frame — produces two applications that a user learns twice. The bet is that "learn once, know both" is worth more than the Mux's best possible standalone layout. If that bet later proves wrong, the tokens (§1), the control specs (§2), the copy (§6), the port notes (§5.5–§5.9) and the icon (§7) all survive unchanged; only §3, §5.2 and §5.4 are discarded. **The shell is the only reversible part of this specification, which is exactly the right property for its riskiest decision to have.**

---

I have the spec, the sources, and empirical verification of every disputed API claim. Writing the corrected extension.

# CSP SUITE — SPECIFICATION EXTENSION v1.1

**Appendix to `docs/design-system.md` v1.0. Binding. Where this document and v1.0 disagree, this document wins, and every such disagreement is named.**

This extension carries two overrides to v1.0 and three new features. It has been through adversarial review; §0.2 lists every defect found and how it was closed, and §0.3 records the measurements that back the corrections. **Nine claims in the first draft were wrong about the real machine or the real .NET API surface. All nine were re-derived by execution, not by argument.**

---

## 0.0 The two overrides

**OVERRIDE 1 — replaces G10.** There is no third repository and no submodule. `Theme.xaml` is duplicated verbatim in both repos and reconciled by a script. Each repo clones and builds standalone with no external path. Mechanism in **§0.1**.

**OVERRIDE 2 — the height is re-derived, not decreed.** Both `SettingsView`s gain rows; §4.5 and §5.4 are recomputed in **§4.5-R** and **§5.4-R**. The arithmetic does not force a change: **both apps stay 460 × 620**, and §4.5-R shows the sums and the cost of the alternative.

---

## 0.2 Corrections applied to the v1.1 draft

Every critical and major defect is closed. The three that were closed *differently* from what the reviewer proposed are marked **▸ decided against the review**, with the reason.

| # | Defect | Resolution | § |
|---|---|---|---|
| C1 | `%LOCALAPPDATA%` inheritance is **not** sufficient — a capability-SID ACE grants Full Control, and a non-owner group carries Read on the precedent folder | Create the temp file with a **protected DACL** + a **Medium** mandatory label. Both verified working, unprivileged. **▸ decided against the review**: a *High* label is unreachable — verified `ERROR_PRIVILEGE_NOT_HELD` | §12.4 |
| C2 | `TryRead` faults the UI thread on two 60-byte files | `required` members + explicit null-document and null-`pairingUrl` guards + `ArgumentException` in the catch. **`required` alone is not enough** — verified | §12.3 |
| C3 | Liveness does not prove the process owns the port | New check 13, `GetExtendedTcpTable(TCP_TABLE_OWNER_PID_LISTENER)`. Verified, 109 µs. New status `PortNotOwned` | §12.3 |
| C4 | Atomic write drops the cited cleanup — every failed publish orphans a credential-bearing `.tmp` forever | Restore `try/finally` + `TryDeleteTemporaryFile`; `TryDeleteOwn` sweeps orphans | §12.2 |
| C5 | Loopback rule enforced in the caller, not the sink; "three independent places" is one | Guard moved **into** `ConnectThroughMuxAsync`; the count is corrected to "one caller-side check that names the UI status, one sink-side invariant, and a third that runs inside the untrusted producer and is not counted" | §12.4, §13.2 |
| C6 | "logs" names a sink that does not exist in either repo | Struck. Failure is silent; the observable is S1/S7. Anti-leak grep extended to both repos and to `InvitationPassword` | §12.2, §13.5, §9.5 |
| C7 | `_autoConnectRequested` is **not** cleared at `:245` → the poll silently reconnects through the Mux after a drop | Cleared on every successful adopt, both routes. The false claim is deleted and the behaviour change is stated | §13.3 |
| C8 | The route status line is placed at `:244`, inside the QR-only success block the Mux route never reaches | Moved into the Mux `adopted` branch. The string is also **rewritten** — see C11 | §13.4 |
| C9 | `AdoptAsync` inherits a candidate leak: `connectionGate.WaitAsync(ct)` sits **outside** the catch, and the new 3 s CTS makes it reachable | Gate acquired with `CancellationToken.None`, wrapped. Stated as a required behavioural change, not a pure extraction | §13.2 |
| C10 | `ShowInTaskbar` / G9 / §9.4 three-way contradiction | **▸ decided against both reviews.** The premise is false: verified that WPF does **not** recreate the HWND. `ShowInTaskbar="False"` is the launch value (no cold-start assignment at all), assignment is guarded, corners are re-asserted, and §9.4's check is rewritten to something true | §11.2-A, §9.4 |
| C11 | `ClearPaletteResult` provably never clears `StatusText`, so "Connected through CSP Mux" is permanent and duplicates the title bar's `Connected` | String deleted and replaced with **`Ready · through CSP Mux`** — shares no word with `Connected`, and the next extraction's `SetStatus` is its natural reset | §13.4, §6.7-A |
| C12 | The tray branch in `OnClosing` also swallows log-off, abandoning the multiplexer | Gated on `_exitRequested && IsVisible`; `App.SessionEnding` sets `_exitRequested` first | §11.3-A |
| C13 | The row-2 gap column is fixed, so the instruction column is 314 px, not 322 — §6.2:1665 wraps and the strip becomes 72 in S5 | `ColumnDefinitions="*,Auto"` + `Margin="8,0,0,0"`. **And §6.2:1665 is re-cut to 57 characters** — it was 60 and would have wrapped even at 322 | §13.4, §6.7-C |
| C14 | `ConnectionHeading` carries the only word distinguishing S0 from S1 and is not a live region; S0/S2 empty the one live region in the strip | `ConnectionHeading` becomes the seventh `Polite` live region with an `Announce`-guarded setter | §4.6-A |
| C15 | `ConnectButton.AutomationProperties.Name` is written at `:147`/`:154` and announces "…to CSP Companion Mode" during a Mux connect | New automation-name column in the state table | §13.4 |
| C16 | S4/S7 instructions say "Scan its QR" while the tooltip on the same button says "Scan CSP's…" | Route-neutral tooltip. The scanner **cannot** be steered at one of the two codes — its predicate accepts any valid pairing URL (`CompanionCanvasService.cs:8-9`), so the honest tooltip is the neutral one | §13.4 |

Minor defects fixed inline: the `CompanionPairingCodec` citation (**108-111**, not 71-74), the size-cap TOCTOU, the `proxy` vocabulary leak, `session file` as an engineering word, the undisposed `Process`, the `§6.6-A` row count, missing `AutomationProperties.Name` on both new toggles, the DoD line that contradicted `TryDeleteOwn`, and the one-sided QR-exposure comparison. Three are listed under **Known minor deviations** with reasons.

---

## 0.3 Verification log — what was executed, and what it changed

Every row here was run on the target machine (Windows 11 Pro 26200, .NET SDK 8.0.319). **Nine draft claims did not survive.**

| Claim under test | Result | Consequence |
|---|---|---|
| `%LOCALAPPDATA%` DACL has "no ACE for Users, Everyone, or Authenticated Users" | **FALSE.** `icacls` shows an inherited `(I)(F)` for capability SID `S-1-15-3-3557520199-…-3692855932`, and the sibling `CSP Palette Companion` folder additionally carries `CodexSandboxUsers:(I)(OI)(CI)(RX)` | §12.4 rewritten; ACL code is now **mandatory** |
| `FileSecurity` + `SetSecurityDescriptorSddlForm("S:(ML;;NRNW;;;HI)", AccessControlSections.Audit)` sets a mandatory label | **FAILS**, `IOException: ERROR_PRIVILEGE_NOT_HELD` — .NET maps any SACL to `SACL_SECURITY_INFORMATION`, which needs `SeSecurityPrivilege`. Also fails for `;;;ME` | The review's proposed fix does not work. Label must go through `SetNamedSecurityInfoW` with `LABEL_SECURITY_INFORMATION` |
| `SetNamedSecurityInfoW(path, SE_FILE_OBJECT, LABEL_SECURITY_INFORMATION, …)` from a medium-IL, unprivileged process | **rc = 0** for `ME`, `LW` and `HI` SDDL alike; final `icacls` shows `Mandatory Label\Medium Mandatory Level:(NW,NR,NX)` | Specified as the label mechanism |
| `FileSystemAclExtensions.Create(FileInfo, …, FileSecurity)` compiles and runs under `net8.0-windows` with **no** `PackageReference` | **TRUE** | Specified |
| `File.Move` preserves an explicit protected DACL and the label; the capability ACE does not come back | **TRUE** — final file DACL is exactly `SYSTEM:(F)` + `User:(F)` | Temp file carries the descriptor; move order confirmed correct |
| A JSON literal `null` deserialises to a **null** document — even with `required` members | **TRUE for both** | `required` is necessary but **not sufficient**; an explicit null-document guard is mandatory |
| `"pairingUrl"` omitted → non-null doc with null property; `Decode(null)` throws | **TRUE.** `required` turns *omitted* into `JsonException` — but an explicit `"pairingUrl": null` still binds to null | Both guards specified |
| `ArgumentException.ThrowIfNullOrWhiteSpace` throws `ArgumentNullException` (null) / `ArgumentException` (whitespace) | **TRUE**, both `is ArgumentException` | Catch widened to `ArgumentException or FormatException` |
| `GetExtendedTcpTable(TCP_TABLE_OWNER_PID_LISTENER)` returns owning PIDs unprivileged, for v4 and v6, and the row vanishes when the listener stops | **TRUE**; a wrong PID is rejected | Check 13 specified |
| Cost of the port check | **109 µs** per call (100 iterations, 10.9 ms, Release) | Cheap enough to run per tick |
| `Process.GetProcessById` + `.StartTime` + `.ProcessName` is "milliseconds on a loaded machine" | **FALSE — 43 µs** per call (100 iterations, 4.3 ms) | **▸ the review's cost objection is not supported.** `Process.*` is kept |
| `OpenProcess`+`GetProcessTimes`+`QueryFullProcessImageNameW` is cheaper | **FALSE — 43 µs, identical.** And it still returns `ERROR_ACCESS_DENIED` (5) for higher-IL targets | The native path buys nothing. v1.0's rejection stands, on corrected grounds |
| `DateTime.FromFileTimeUtc(creationTime) == Process.StartTime.ToUniversalTime()` | **exactly equal**; `"O"` round-trip is lossless | Tolerance kept at 1 s for the forward-compatibility reason only |
| Assigning `ShowInTaskbar` on a **visible**, owner-less `WindowStyle="None"` window recreates the HWND and loses `DWMWA_WINDOW_CORNER_PREFERENCE` | **FALSE.** Handle identical across four transitions, `HwndSource` identical (`ReferenceEquals`), hook kept firing (121 messages), `DwmGetWindowAttribute` still returns `2` | §9.4's "square corners are the tell" check is **deleted** and replaced |
| Any logging facility exists in either repo | **FALSE.** `grep -rn 'Trace\.\|Debug\.WriteLine\|ILogger\|EventLog\|Console\.Error' --include=*.cs` over both `src` trees: **zero hits** | "logs" struck everywhere |
| `connectionGate.WaitAsync(ct)` is outside the disposing catch | **TRUE** — `CompanionCanvasService.cs:31` vs `try` at `:32` | Fixed in `AdoptAsync` |
| `_autoConnectRequested` is assigned only at `:35`, `:57`, `:187`, `:196` | **TRUE.** `:245` is a bare `return;` | Fixed |
| The private-endpoint guard is at `CompanionPairingCodec.cs:71-74` | **FALSE.** `71-74` is the query check (`FormatException`). The guard is at **`108-111`** | All three citations corrected |
| `AppSettingsService.SaveAsync` tracks and reaps its temp file | **TRUE** — `:78`, `:104`, `:106-114`, `:126-137` | Pattern restored in full |
| The Mux has a `NativeMethods.cs` | **FALSE.** The Mux project has no such file today | It is created in Phase 7 for §3.1 and extended in Phase 13 |

---

# 0.1 THEME AND CODE SHARING WITHOUT A THIRD REPO

*(Replaces G10 and §5.10 in their entirety. §1's opening line — "All tokens live in `external/csp-suite-theme/…`" — is amended to §0.1.1's paths.)*

**Decision.** `Theme.xaml` is duplicated **verbatim** in both repos. A script reconciles the copies when both are checked out side by side. Each repo clones and builds standalone.

**Why this and not a submodule.** A submodule adds a third repository, a third default branch, a detached-HEAD failure mode, and a `--recurse-submodules` clone precondition — for one developer, two sibling directories, one file, and no CI. The cost G10 was buying (a single physical file) is bought here by a 60-line script plus a build-time check, and the thing G10 was protecting against (silent drift) is *detected mechanically* either way. The genuine loss is that an edit must be pushed; the check makes forgetting a warning at the next build.

## 0.1.1 The sync set

Three tiers. Tier assignment is stated in a banner at the top of every synced file.

| Tier | Meaning | Members |
|---|---|---|
| **1** | Byte-identical. No local region. | `src/<AppProject>/Theme/Theme.xaml` |
| **2** | Identical outside one `SYNC-LOCAL` region (namespace and app-specific `using`s). | `MuxHandoffContract.cs` · `CompanionQrScanner.cs` · `TrayHost.cs` |
| **3** | Not synced. Everything else, including `MuxSessionHandoff.cs` (Mux only) and `MuxHandoffReader.cs` (Companion only). | — |

**Exact paths.**

```
csp_color_palette_gen/
  src/CspPaletteCompanion.App/Theme/Theme.xaml            ← Tier 1
  src/CspPaletteCompanion.App/MuxHandoffContract.cs       ← Tier 2
  src/CspPaletteCompanion.App/CompanionQrScanner.cs       ← Tier 2 (source of truth)
  src/CspPaletteCompanion.App/TrayHost.cs                 ← Tier 2
  tools/suite-sync.ps1
  tools/suite-sync.manifest

csp-app-multiplexer/
  src/CspMultiplexer.App/Theme/Theme.xaml                 ← Tier 1
  src/CspMultiplexer.App/MuxHandoffContract.cs            ← Tier 2
  src/CspMultiplexer.App/CompanionQrScanner.cs            ← Tier 2
  src/CspMultiplexer.App/TrayHost.cs                      ← Tier 2
  tools/suite-sync.ps1                                    ← itself synced (Tier 1 by hash)
  tools/suite-sync.manifest
```

**`Theme/Theme.xaml` gets no `<Page Include>` item.** The WPF SDK already globs `**/*.xaml` as `Page` (everything except `App.xaml`, which is `ApplicationDefinition`). An explicit item is **NETSDK1022: duplicate Page items**. The pack URI in §5.9 becomes `pack://application:,,,/Theme/Theme.xaml` in **both** apps — same assembly, BAML-compiled, so every `{StaticResource}` key is still validated at build time, which is the entire reason G10 chose `Page` over `Resource`.

**File banner, first five lines of every synced file:**

```
// ═══ CSP SUITE SHARED FILE ══════════════════════════════════════════════════
// Reconcile with tools/suite-sync.ps1 (spec §0.1). Tier 2.
//   Companion : src/CspPaletteCompanion.App/MuxHandoffContract.cs
//   Mux       : src/CspMultiplexer.App/MuxHandoffContract.cs
// ════════════════════════════════════════════════════════════════════════════
```

XAML uses `<!-- … -->` with the same five lines.

## 0.1.2 `SYNC-LOCAL` regions

Everything between the markers is **excluded from the hash** and never overwritten by `Push`/`Pull`:

```csharp
// ── SYNC-LOCAL BEGIN ──
namespace CspPaletteCompanion.App;
// ── SYNC-LOCAL END ──
```

A file may contain at most one region. A second region is a script error (exit 2) — the discipline only holds if "the local part" is one contiguous, obvious block.

## 0.1.3 `tools/suite-sync.ps1`

Sibling discovery: the script resolves its own repo root (two levels up from itself), then looks for the other repo **as a sibling directory** by name — `csp_color_palette_gen` and `csp-app-multiplexer`. `-Other <path>` overrides. **If the sibling is absent the script prints `nothing to reconcile` and exits 0.** That single line is what makes a standalone clone build.

Hash: SHA-256 over the file with (a) the `SYNC-LOCAL` block removed, (b) CRLF normalised to LF, (c) a leading UTF-8 BOM stripped **for hashing only** — §6's "UTF-8 with BOM" rule still governs what is written to disk.

```powershell
#requires -Version 7
[CmdletBinding()]
param(
    [ValidateSet('Check','Push','Pull')] [string] $Mode = 'Check',
    [string] $Other
)

$ErrorActionPreference = 'Stop'
$here = Resolve-Path (Join-Path $PSScriptRoot '..')
$names = @('csp_color_palette_gen','csp-app-multiplexer')
if (-not $Other) {
    $parent = Split-Path $here -Parent
    $mine   = Split-Path $here -Leaf
    $Other  = $names | Where-Object { $_ -ne $mine } |
              ForEach-Object { Join-Path $parent $_ } |
              Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Other) { Write-Host 'suite-sync: nothing to reconcile.'; exit 0 }

function Get-SyncHash([string] $path) {
    $text = [IO.File]::ReadAllText($path)
    $text = [Regex]::Replace($text,
        '(?s)(//|<!--)\s*──\s*SYNC-LOCAL BEGIN\s*──.*?SYNC-LOCAL END\s*──\s*(-->)?', '')
    $text = $text -replace "`r`n", "`n"
    $sha  = [Security.Cryptography.SHA256]::Create()
    ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)) |
        ForEach-Object { $_.ToString('x2') }) -join ''
}

# suite-sync.manifest: one "<tier>|<relative-path-in-this-repo>|<relative-path-in-the-other>"
$rows  = Get-Content (Join-Path $PSScriptRoot 'suite-sync.manifest') |
         Where-Object { $_ -and -not $_.StartsWith('#') }
$drift = @()

foreach ($row in $rows) {
    $tier, $mineRel, $theirsRel = $row -split '\|'
    $a = Join-Path $here  $mineRel
    $b = Join-Path $Other $theirsRel
    if (-not (Test-Path $a)) { Write-Error "missing locally: $mineRel"; exit 2 }
    if (-not (Test-Path $b)) { $drift += ,@($a,$b,'absent on the other side'); continue }
    if ((Get-SyncHash $a) -ne (Get-SyncHash $b)) {
        $newer = if ((Get-Item $a).LastWriteTimeUtc -ge (Get-Item $b).LastWriteTimeUtc)
                 { 'this repo' } else { 'the other repo' }
        $drift += ,@($a,$b,"differs; newer side: $newer")
    }
}

if (-not $drift) { Write-Host "suite-sync: $($rows.Count) file(s) in sync."; exit 0 }

if ($Mode -eq 'Check') {
    foreach ($d in $drift) { Write-Warning "suite-sync drift: $($d[0]) <-> $($d[1]) — $($d[2])" }
    exit 1
}

foreach ($d in $drift) {
    $src, $dst = if ($Mode -eq 'Push') { $d[0], $d[1] } else { $d[1], $d[0] }
    # Preserve the destination's SYNC-LOCAL block; replace everything else.
    $pattern = '(?s)((?://|<!--)\s*──\s*SYNC-LOCAL BEGIN\s*──.*?SYNC-LOCAL END\s*──\s*(?:-->)?)'
    $keep = if (Test-Path $dst) { ([Regex]::Match([IO.File]::ReadAllText($dst), $pattern)).Value } else { '' }
    $text = [IO.File]::ReadAllText($src)
    if ($keep) { $text = [Regex]::Replace($text, $pattern, { $keep }, 1) }
    [IO.File]::WriteAllText($dst, $text, (New-Object Text.UTF8Encoding $true))   # BOM, per §6
    Write-Host "suite-sync: $Mode $($d[0]) -> $dst"
}
exit 0
```

`suite-sync.manifest` is **identical in both repos** and lists both sides of every pair, so `-Mode Push` from either direction is well defined.

## 0.1.4 Build integration — `SuiteSyncCheck`

Added to **both** app csprojs. It must never break a build.

```xml
<Target Name="SuiteSyncCheck" BeforeTargets="BeforeBuild"
        Condition="'$(Configuration)' == 'Debug' AND
                   Exists('$(MSBuildProjectDirectory)\..\..\tools\suite-sync.ps1')">
  <Exec Command="pwsh -NoProfile -File &quot;$(MSBuildProjectDirectory)\..\..\tools\suite-sync.ps1&quot; -Mode Check"
        ContinueOnError="WarnAndContinue" StandardOutputImportance="high" />
</Target>
```

- **Debug only.** A Release build of a fresh clone must not depend on `pwsh` existing.
- **`ContinueOnError="WarnAndContinue"`** — drift is an MSBuild warning, not a `CS####`, so repo-wide `TreatWarningsAsErrors` does not turn a missing sibling into a broken build.
- **`Exists(...)` condition** — a source drop without `tools/` still builds.

**Cost, stated honestly.** Drift is caught at the *next Debug build of the other repo*, not at edit time. That is strictly weaker than one physical file. It is accepted because the alternative costs a repository.

---

# 11. SYSTEM TRAY — CORRECTIONS AND ADDITIONS

*(§11.1 and §11.4–§11.9 stand as drafted. §11.2, §11.3 and §11.10 are amended below; §11.11's index is reissued.)*

## 11.2-A `ApplyHostMode` and `ShowInTaskbar` — the contradiction, resolved by measurement

The draft asserted three things that cannot all hold: G9 demands the toggle take effect immediately on a visible settings page; §9.4 forbade assigning `ShowInTaskbar` while visible; and `RunInTray = true` with `ShowInTaskbar="True"` in XAML forces exactly that assignment on every cold start.

**The premise is false.** Measured on .NET 8 / Windows 11 with an owner-less `WindowStyle="None"` window: assigning `ShowInTaskbar` **true→false and false→true on a visible window preserves the HWND** (`0x7B1328` across all four transitions), preserves the `HwndSource` (`ReferenceEquals` true), keeps the installed hook receiving messages (121 messages over the run), and leaves `DwmGetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE)` reading `2`. §9.4's "square corners are the tell" check was testing for a failure mode that does not occur here.

**What is specified anyway, because a measurement on one OS build is not a guarantee:**

```xml
ShowInTaskbar="False"
```

is the **XAML launch value in both apps** — it matches the `RunInTray = true` default, so a cold start with shipped settings makes **no assignment at all**. That deletes the every-launch case outright, which is the only part of the objection that was structural.

```csharp
private void ApplyHostMode()
{
    var wantTaskbar = !_settings.RunInTray;
    if (ShowInTaskbar != wantTaskbar)
    {
        ShowInTaskbar = wantTaskbar;                 // immediate, per G9

        // Belt and braces. Verified not to be needed on Win11 26200 / .NET 8 (the
        // handle, the HwndSource, its hooks and the DWM corner preference all
        // survive) — but both calls are idempotent, cost one syscall each, and are
        // the only thing standing between a future WPF change and a silently
        // square window with a dead activation receiver.
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.ApplyRoundedCorners(hwnd);
            _tray.ReattachActivationHook(this);      // no-op if the source is unchanged
        }
    }

    _tray.SetIconVisible(_settings.RunInTray);

    // Turning tray mode off while the window is hidden would leave it unreachable:
    // no taskbar button existed while it was hidden, and the icon is about to go.
    if (!_settings.RunInTray && _hiddenToTray) { ShowFromTray(); }
}
```

`TrayHost.ReattachActivationHook` compares the current `HwndSource` to the one it holds and returns immediately when they match. **`NotifyIcon.Visible` tracks `RunInTray` only** — never window visibility — so the icon does not flicker as the window is shown and hidden.

## 11.3-A `OnClosing` — the tray branch must not swallow a non-user close

The draft gated the branch on `RunInTray` alone. On log-off, WPF raises `SessionEnding` and then tears the app down with `Application.Shutdown` semantics, which raise `Closing` and **ignore `e.Cancel`** — §5.7(c) says so and is the reason `Shutdown()` is banned. A branch that returns before `await StopAsync()` therefore abandons the `CompanionMultiplexer` and the upstream CSP link on the one exit path where the Mux is most likely to be sharing.

**Invariant: the tray hide branch runs only for a user-initiated close of a visible window.**

```csharp
protected override async void OnClosing(CancelEventArgs e)
{
    if (closingAfterCleanup) { base.OnClosing(e); return; }

    e.Cancel = true;

    // Tray branch. All four conditions are load-bearing:
    //   RunInTray        — the mode is on
    //   !_exitRequested  — this close is not the tray menu's Exit, not App.SessionEnding,
    //                      and not the second-instance Application.Shutdown path
    //   IsVisible        — a close arriving at an already-hidden window is not a hide
    //   !closeInProgress — a teardown is not already under way
    if (_settings.RunInTray && !_exitRequested && IsVisible && !closeInProgress)
    {
        HideToTray();
        return;
    }

    if (closeInProgress) return;              // (b) Alt+F4 during teardown
    closeInProgress = true;
    IsEnabled = false;

    SaveWindowPosition();
    await StopAsync();                        // Mux only; the Companion has no equivalent

    tray.Dispose();                           // before the hop, never after Close()
    closingAfterCleanup = true;
    await Dispatcher.Yield(DispatcherPriority.Background);   // (a) MANDATORY
    Close();
}
```

**`App.xaml.cs` sets `_exitRequested` before the close can propagate**, in *both* handlers:

```csharp
private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
{
    // The window's OnClosing cannot cancel a session-ending shutdown, so the tray
    // branch must be disarmed here or teardown is skipped entirely.
    if (MainWindow is MainWindow w) { w.MarkExitRequested(); }
    MuxSessionHandoff.TryDeleteOwn();          // Mux only; best effort, ~2 s budget
}
```

The same call is made from the tray menu's `Exit` (`RequestExit()`) and from the second-instance branch's `Application.Shutdown()` in `App.OnStartup`.

## 11.10 The Window card — both apps, byte-identical XAML

`AppSettings` (Companion) gains, with no `SchemaVersion` bump — identical reasoning to §3.5 line 833:

```csharp
/// <summary>
/// True: the window lives in the notification area. The close button hides it, the
/// app keeps running, and Exit lives in the tray menu. False: the window owns a
/// taskbar button for its whole life and the close button exits.
/// A settings.json written before this member existed has no such property, so
/// System.Text.Json leaves this initialiser in place and the app starts in tray
/// mode. That is the intended default; the one-time hint (§11.8) covers the change.
/// </summary>
public bool RunInTray { get; init; } = true;

/// <summary>
/// Set the first time the window is hidden to the tray, so the one-time
/// "still running" balloon is shown once per user and never again.
/// </summary>
public bool TrayHintShown { get; init; }
```

**Not added to `CapturePermission` and not answered by `IsAllowed`** (`AppSettings.cs:40-47`) — tray mode is not a capture permission.

`AppPreferences` (Mux) becomes, **append only**:

```csharp
internal sealed record AppPreferences(
    string ListenAddress,
    bool HideQrAfterFirstConnection = false,
    double? WindowLeft = null,
    double? WindowTop = null,
    bool RunInTray = true,
    bool TrayHintShown = false);
```

`System.Text.Json` binds constructor parameters by name, so JSON order is irrelevant — but the three positional construction sites at `AppPreferences.cs:24`, `:25` and `:29` compile only because every added parameter has a default and follows `ListenAddress`. Appending keeps all three untouched. `AppPreferences.Save` (`:33-40`) still gains the `try/catch` §5.4 line 1242 mandates.

**The card. Geometry reuses §4.5's permission-row spec verbatim.**

```xml
<Border Style="{StaticResource CardStyle}" Margin="0,0,0,12">
  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="12"/>
      <ColumnDefinition Width="44"/>
    </Grid.ColumnDefinitions>
    <StackPanel Grid.Column="0">
      <TextBlock Style="{StaticResource BodyStrongTextStyle}" Text="System tray"/>
      <TextBlock Style="{StaticResource CaptionTextStyle}" Margin="0,4,0,0"
                 Text="Close hides the window. Exit from the tray icon."/>
    </StackPanel>
    <CheckBox x:Name="TrayModeToggle" Grid.Column="2"
              Style="{StaticResource ToggleSwitchStyle}"
              VerticalAlignment="Center"
              AutomationProperties.Name="System tray"
              Click="TrayModeToggle_Click"/>
  </Grid>
</Border>
```

Card height per §4.5-R's corrected formula: `1 + 8 + 38 + 8 + 1 = ` **56**; **68** in the stack with its margin.

**`AutomationProperties.Name` is new and required.** A content-less `CheckBox` with its label in a sibling `TextBlock` and no tooltip (correctly deleted) announces "check box, off" and nothing else. The name costs zero pixels and is the same string in both apps, so the byte-identical-XAML claim survives. Caption width: content column = `420 − 2 − 20 − 12 − 44 = 342 px`; at the spec's own ≈5.6 px/char that is **61 characters**; the caption is **48**. ✔ One line, so the 16 px reservation holds and 56 is real.

**Placement, both apps: the Window card is always the last card before the Meta card.** Same card, same relative position, both `SettingsView`s — G7 applied to settings. It cannot sit between the Companion's Permissions card and `AutoActionOptionsPanel`: that panel is visually subordinate to the third permission row and its `Visibility` is driven by that row's toggle (§4.5 line 1021; `MainWindow.xaml.cs:657-659`). It does not go *inside* the Permissions card: that card is "what the app may read or run", and window behaviour is not a capability grant.

**No tooltip** (§6.1 lines 533, 556 delete every toggle tooltip that duplicates the caption beneath it; the toggle is never disabled, so G15 does not apply). **No off-state string** — G14's register and §6's rule against narrating an absence.

Handler, Companion (Mux identical against `preferences` and `AppPreferences.Save`):

```csharp
private async void TrayModeToggle_Click(object sender, RoutedEventArgs e)
{
    if (_loadingSettings) return;                                   // as :708-711
    _settings = _settings with { RunInTray = TrayModeToggle.IsChecked == true };
    ApplyHostMode();                                                // immediate, per G9
    await SaveSettingsAsync();                                      // :900
}
```

Wired to **`Click`**, never `Checked`/`Unchecked` (§2.5 line 372) — because `ApplySettingsToUi` (`:644-690`) gains `TrayModeToggle.IsChecked = _settings.RunInTray;` alongside the three permission assignments at `:650-652`. The `_loadingSettings` guard is copied from `PermissionToggle_Click` (`:706-711`). **`TrayHost.MarkHintShown`** writes `_settings = _settings with { TrayHintShown = true }` then `await SaveSettingsAsync()`.

## 11.11 Amendment index for §11 — reissued

| Spec section | Change |
|---|---|
| §1.1 implicit-styles list (70-78) | add implicit `ContextMenu`, `MenuItem`, `Separator` |
| §3.1 | **`ShowInTaskbar="False"`** in XAML, both apps — matches the `RunInTray` default so a cold start assigns nothing; owned by `ApplyHostMode` thereafter (§11.2-A) |
| §3.2 col 10 | Close button gains `x:Name="CloseButton"`; its `ToolTip` and `AutomationProperties.Name` become code-driven |
| §4.5 | see §4.5-R |
| §4.6 | **`ConnectionHeading` added as a seventh `Polite` live region** (§4.6-A) |
| §4.7 | new edits 8–11 (§4.7-A) |
| §5.4 | see §5.4-R |
| §5.7 | tray branch after `e.Cancel = true`, before the `closeInProgress` latch, **gated per §11.3-A**; `tray.Dispose()` before the `Dispatcher.Yield` |
| §5.8 / Companion csproj | `<Resource Include="Assets\*.ico" />`; the `SuiteSyncCheck` target (§0.1.4); **no `ProjectReference` to a theme project** |
| §5.9 | `Source="pack://application:,,,/Theme/Theme.xaml"` in both apps; `App.xaml.cs` gains five handlers |
| §5.10 | **deleted** — replaced by §0.1 |
| §6 | see §6.7 |
| §7.4 | unchanged — the tray is the consumer that justifies the 20 and 24 frames |
| §7.5 | add "the notification area" to where the glyph appears |
| §9 | see §9.4 |
| §10 | see §10-A |

---

# 12. MUX SESSION HANDOFF

Mux only. `CspMultiplexer.Broker` and `CspMultiplexer.Protocol` change by **zero lines** (A10).

## 12.0 New and changed files

| Repo | Path | Fate |
|---|---|---|
| Mux | `src/CspMultiplexer.App/MuxHandoffContract.cs` | **New.** Tier 2 of the §0.1.1 sync set. |
| Mux | `src/CspMultiplexer.App/MuxSessionHandoff.cs` | **New.** Writer + owner-checked delete + temp sweep. Tier 3. |
| Mux | `src/CspMultiplexer.App/NativeMethods.cs` | **Created in Phase 7** for §3.1's `ApplyRoundedCorners` (the Mux has no such file today — the draft's claim that both apps already have one is wrong). Gains `SetMediumMandatoryLabel` here. |
| Mux | `MainWindow.xaml.cs` | `StartAsync` publishes; `StopAsync` deletes first. |
| Mux | `App.xaml.cs` | `SessionEnding` and `ProcessExit` best-effort delete. |
| Companion | `src/CspPaletteCompanion.App/MuxHandoffContract.cs` | **New.** The same file. |

**`MuxHandoffContract.cs`, complete — byte-identical outside its four-line `SYNC-LOCAL` region:**

```csharp
// ═══ CSP SUITE SHARED FILE ══════════════════════════════════════════════════
// Reconcile with tools/suite-sync.ps1 (spec §0.1). Tier 2.
//   Companion : src/CspPaletteCompanion.App/MuxHandoffContract.cs
//   Mux       : src/CspMultiplexer.App/MuxHandoffContract.cs
// ════════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

// ── SYNC-LOCAL BEGIN ──
namespace CspPaletteCompanion.App;
// ── SYNC-LOCAL END ──

/// <summary>Everything both apps must agree on about the Mux session handoff file.</summary>
internal static class MuxHandoffContract
{
    internal const int    SchemaVersion    = 1;
    internal const string DirectoryName    = "CSP Suite";
    internal const string FileName         = "mux-session.json";
    internal const string TempPrefix       = ".mux-session.json.";
    internal const string TempSuffix       = ".tmp";
    internal const string MuxProcessName   = "CSP Mux";     // == <AssemblyName>, §5.8 line 1460
    internal const int    MaximumFileBytes = 4096;
    internal static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(1);

    internal static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DirectoryName);

    internal static string FilePath => Path.Combine(DirectoryPath, FileName);

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented               = false,
    };
}

/// <summary>
/// Every member is <c>required</c>, so System.Text.Json raises JsonException when a
/// property is missing — which the reader already maps to Malformed. Verified by
/// execution: `required` covers an OMITTED property but NOT an explicit
/// <c>"pairingUrl": null</c>, and it does NOT cover a document that is the JSON
/// literal <c>null</c> (Deserialize returns null in that case even for a required
/// record). Both remaining holes are closed explicitly in §12.3 steps 4 and 6.
/// </summary>
internal sealed record MuxSessionDocument
{
    [JsonRequired] public required int      SchemaVersion       { get; init; }
    [JsonRequired] public required string   PairingUrl          { get; init; }
    [JsonRequired] public required int      ProcessId           { get; init; }
    [JsonRequired] public required DateTime ProcessStartTimeUtc { get; init; }
}
```

Setting camelCase explicitly on the writer while the reader is also case-insensitive means a casing drift on either side cannot silently break a cross-repo contract with no compiler enforcing it. Compact, not indented: nothing human reads this file and it should not look like a settings file somebody is invited to edit.

## 12.1 The file

### Path

```
%LOCALAPPDATA%\CSP Suite\mux-session.json
```

The filename names a *session*, which is what makes "delete it when the session ends" self-evidently correct.

### Schema — four fields, version 1

```json
{
  "schemaVersion": 1,
  "pairingUrl": "https://companion.clip-studio.com/rc/en-us?s=3f9c…",
  "processId": 24680,
  "processStartTimeUtc": "2026-07-27T09:14:02.1234567Z"
}
```

| Field | Type | Why it exists |
|---|---|---|
| `schemaVersion` | `int`, required | The reader accepts `== 1` and nothing else. Load-bearing because the *meaning* of `pairingUrl` could change — e.g. if a later Mux issues a per-consumer credential rather than the shared `InvitationPassword`. A newer file must produce a clean, named refusal (§13.5 case 2a), never a misparse that hands a credential to a listener under a protocol the Companion does not implement. |
| `pairingUrl` | `string`, required | Byte-for-byte what `CompanionPairingCodec.Encode` produced at `CompanionMultiplexer.cs:79-83`, and byte-for-byte what the Companion's `Decode` already consumes on the QR path at `CompanionCanvasService.cs:26`. Publishing the **URL** rather than a decomposed address/port/password reuses the existing decoder, the existing private-endpoint guard (**`CompanionPairingCodec.cs:108-111`**), and the existing `ConnectAndAuthenticateAsync(pairing, ct)`. **Zero new parsing code.** |
| `processId` | `int`, required | Liveness (§12.3 steps 7-9), the port-ownership discriminator (step 13), and the ownership discriminator that makes delete safe (§12.2). |
| `processStartTimeUtc` | `string`, ISO-8601 `"O"`, UTC | PID-recycle defence. `DateTime` in UTC, **not `DateTimeOffset`** — the comparand (`Process.StartTime.ToUniversalTime()`) is a `DateTime`, and introducing an offset only creates a conversion that can be got wrong. Verified: `Process.StartTime.ToUniversalTime()` and `DateTime.FromFileTimeUtc(GetProcessTimes.ftCreationTime)` are *exactly* equal, and the `"O"` round-trip is lossless. |

### Fields deliberately absent, each rejected for cause

- **`endpointAddress` / `endpointPort`.** A second copy of data already inside `pairingUrl`. Two copies can disagree, and then the reader must pick a winner — and picking the plain fields over the decode result is exactly the bug that lets a forged file steer the connection while the URL looks innocent. The Companion's authority is the decode result, so the plain fields would be either dead or dangerous.
- **`publishedAtUtc`.** Age is not evidence: a file whose process is alive, start-time-matching and port-owning is live at ten seconds or ten hours.
- **`instanceId` / GUID.** `processId` already answers "is this mine?" for the delete path.
- **`generation`.** Already inside the URL — `Encode` packs it as tab-field 4 (`CompanionPairingCodec.cs:47-50`).
- **`clientCount`, `listenScope`, `muxVersion`.** The Companion branches on none of them.

UTF-8, no BOM (`System.Text.Json` default). **This is the one place in the suite that deviates from §6's "UTF-8 with BOM" rule** — that rule governs `.xaml`/`.cs` source, not machine-written JSON. Stated so the §9.3 check is not applied to it.

## 12.2 Write and delete lifecycle

### Write site — exact

`MainWindow.StartAsync`, immediately after `await multiplexer.StartAsync(token)` returns — the same point where the current WinForms code renders the QR (`MainForm.cs:261-264`) — and **before** `ApplyState(ConnectionState.Online)`.

```csharp
await multiplexer.StartAsync(token).ConfigureAwait(true);

// One write per session. InvitationPassword is minted once in the multiplexer's
// constructor (CompanionMultiplexer.cs:49) and PairingUrl once in StartAsync (:79);
// neither rotates mid-session. If either ever starts rotating, this site must republish.
if (IPAddress.IsLoopback(selectedAddress)) MuxSessionHandoff.TryPublish(multiplexer.PairingUrl!);
else                                       MuxSessionHandoff.TryDeleteStale();

ApplyState(ConnectionState.Online);
```

**Why exactly there.** `CompanionMultiplexer.StartAsync` calls `listener.Start(options.MaximumClients)` synchronously at `:77`, computes `PairingUrl` at `:79`, and only then queues the accept loop at `:84` before returning `Task.CompletedTask` at `:85`. By the time the await completes the **listen backlog is open** — a Companion connecting in that same instant is queued, not refused, even though `AcceptLoopAsync` may not have run yet. One statement earlier would publish a URL to a port that does not exist; after `ApplyState` there is a window where the UI says `Sharing` and the file is absent.

**`TryPublish` never throws.** It swallows `IOException` and `UnauthorizedAccessException`. **It does not log** — neither repo has any logging facility (verified: zero hits for `Trace.` / `Debug.WriteLine` / `ILogger` / `EventLog` / `Console.Error` across both `src` trees), and inventing one on the code path that holds the pairing URL is how a credential ends up in a second, unmanaged on-disk location that `TryDeleteOwn` never reaps. **The failure is silent; the observable is the Companion's S1 state and the QR path, which is a working path.** Failing `StartAsync` would take the *proxy* down over a *hint*.

### The write — atomic, ACL'd, and reaped

```csharp
// MuxSessionHandoff.TryPublish — the whole method's error contract is "never throws".
internal static void TryPublish(string pairingUrl)
{
    string? tmp = null;
    try
    {
        Directory.CreateDirectory(MuxHandoffContract.DirectoryPath);
        TryDeleteOrphanTempFiles();                         // reap a previous crash's residue

        using var self = Process.GetCurrentProcess();
        var document = new MuxSessionDocument
        {
            SchemaVersion       = MuxHandoffContract.SchemaVersion,
            PairingUrl          = pairingUrl,
            ProcessId           = Environment.ProcessId,
            ProcessStartTimeUtc = self.StartTime.ToUniversalTime(),
        };

        tmp = Path.Combine(MuxHandoffContract.DirectoryPath,
            $"{MuxHandoffContract.TempPrefix}{Guid.NewGuid():N}{MuxHandoffContract.TempSuffix}");

        // The DACL goes on the TEMP file, not on the destination afterwards:
        //  (1) a file created and then re-ACLed is readable for the interval between
        //      the two calls;
        //  (2) File.Move within a volume preserves the source's EXPLICIT ACEs and does
        //      not re-inherit from the destination directory — verified.
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var user = WindowsIdentity.GetCurrent().User!;
        security.SetAccessRule(new FileSystemAccessRule(
            user, FileSystemRights.FullControl, AccessControlType.Allow));
        security.SetAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));

        using (var stream = new FileInfo(tmp).Create(
                   FileMode.CreateNew,
                   FileSystemRights.WriteData | FileSystemRights.Write,
                   FileShare.None, 4096, FileOptions.WriteThrough, security))
        {
            stream.Write(JsonSerializer.SerializeToUtf8Bytes(document, MuxHandoffContract.Json));
            stream.Flush();
        }

        // Medium mandatory label with no-read-up. Best effort: if it fails the DACL
        // still holds, and the DACL is what closes the AppContainer hole.
        NativeMethods.TrySetMediumNoReadUpLabel(tmp);

        File.Move(tmp, MuxHandoffContract.FilePath, overwrite: true);   // atomic replace
        tmp = null;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                  or PlatformNotSupportedException)
    {
        // Degrade to the QR path. Never surfaced, never logged.
    }
    finally
    {
        // Mirrors AppSettingsService.SaveAsync (:78, :104, :106-114, :126-137). Without
        // this, every failed publish leaves a credential-bearing .tmp on disk forever,
        // under a random name that TryDeleteOwn does not match.
        if (tmp is not null) TryDeleteQuietly(tmp);
    }
}
```

`WriteThrough` so a crash leaves the file complete or absent, never half. `File.Move(overwrite: true)` so a Companion polling the path never observes a partial document — it sees the old file or the new one.

`TryDeleteOrphanTempFiles()` enumerates `Directory.EnumerateFiles(DirectoryPath, ".mux-session.json.*.tmp")` and deletes each best-effort. It runs on publish **and** in `TryDeleteOwn`, so a crash between `CreateNew` and `Move` — the one case the `finally` cannot cover — is reaped at the next clean start or stop.

### Delete site — exact

**First statement of `StopAsync`**, before `operationCancellation.Cancel()` and before the `ClientCountChanged` unsubscribe that §5.6 line 1384 fixes as load-bearing:

```csharp
private async Task StopAsync()
{
    MuxSessionHandoff.TryDeleteOwn();          // FIRST
    operationCancellation?.Cancel();
    if (multiplexer is not null)
    {
        multiplexer.ClientCountChanged -= MultiplexerOnClientCountChanged;
        await multiplexer.DisposeAsync().ConfigureAwait(true);
        multiplexer = null;
    }
    …
```

Deleting first makes the file's **absence** the conservative error: for the whole teardown window the file is gone while the port may still be open, and a Companion that misses the window merely falls back to manual connect. The reverse order leaves a file naming a dead port for the duration of teardown. `OnClosing` already `await StopAsync()` before `Close()`, so window close is covered with no additional code.

### `TryDeleteOwn` — two sanctioned cases, one open handle

```csharp
internal static void TryDeleteOwn()
{
    TryDeleteOrphanTempFiles();
    try
    {
        MuxSessionDocument? document;
        // FileShare.None for the verify read: while this handle is open no other
        // instance can complete a File.Move replace, so the document that is verified
        // is the document that is deleted.
        using (var stream = new FileStream(MuxHandoffContract.FilePath, FileMode.Open,
                   FileAccess.Read, FileShare.None))
        {
            if (stream.Length > MuxHandoffContract.MaximumFileBytes) return;
            document = JsonSerializer.Deserialize<MuxSessionDocument>(stream, MuxHandoffContract.Json);
        }
        if (document is null) return;

        var mine = document.ProcessId == Environment.ProcessId;
        if (mine)
        {
            using var self = Process.GetCurrentProcess();
            mine = Math.Abs((self.StartTime.ToUniversalTime() - document.ProcessStartTimeUtc).Ticks)
                   <= MuxHandoffContract.StartTimeTolerance.Ticks;
        }

        // Second sanctioned case: the recorded process is verifiably DEAD. Deleting a
        // dead instance's file is always safe and is what makes the §9.5 line about a
        // LAN-scope start true — TryDeleteStale() is this branch on its own.
        var dead = false;
        if (!mine)
        {
            try { using var other = Process.GetProcessById(document.ProcessId); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            { dead = true; }
        }

        if (mine || dead) File.Delete(MuxHandoffContract.FilePath);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                  or JsonException or NotSupportedException) { }
}
```

`TryDeleteStale()` is `TryDeleteOwn()` — the ownership check already covers both the "it is mine" and "its owner is dead" cases, so the LAN-scope write site needs no second method.

Without the ownership check, instance A's shutdown would silently unpublish instance B's live session.

### Coverage of every named case

| Case | Behaviour |
|---|---|
| **Normal shutdown** — Stop button, window close, `SecondaryButton` in Online per §5.3 | `StopAsync` deletes. Deterministic. |
| **Tray-mode close** | The window hides; **`StopAsync` does not run and the file stays**, which is correct — the proxy is still sharing. Only Exit tears down. |
| **`CompanionMultiplexer.DisposeAsync`** | Touches the file **not at all**, by design (A10). The App's `StopAsync` owns both ends. A future path that disposes the multiplexer without going through `StopAsync` leaves a file naming a **live process** whose **listener is gone** — which §12.3 check 13 catches and nothing else would. The invariant is: **the file is a hint, never a fact.** |
| **Process kill** — `taskkill /f`, Task Manager | No deletion. File is stale. Caught by §12.3 checks 8–9 before any socket opens. |
| **Machine power loss** | File survives with a PID that after reboot is absent or recycled. Three independent defences: the recorded start time, the image name, and port ownership. |
| **Logoff / OS shutdown** | `App.SessionEnding` deletes synchronously (and sets `_exitRequested`, §11.3-A), then `ProcessExit` deletes again (idempotent). Both are courtesies with a ~2 s budget, not mechanisms. |
| **Two Mux instances** | See below. |

### Two Mux instances

**Primary fix: §11.7's single-instance mutex, `Local\CspSuite.CspMux.SingleInstance`.** Its justification is independent of this feature and is the same mechanism the tray needs, so no second mechanism is introduced.

**Fallback for when the mutex is not held.** `Local\` is per-logon-session, so the same user at the console *and* over RDP legitimately gets two instances.

- **Write is last-writer-wins.** `File.Move(overwrite: true)` is atomic; readers see one complete document naming exactly one of the two live proxies. Both proxies front the *same* CSP process, so a Companion reaching either gets equivalent service.
- **Delete is ownership-checked**, and the verify read holds `FileShare.None`, so a concurrent replace cannot interleave between verify and delete.
- **Residual, accepted and named:** if B replaces A's file and then stops, the file is gone while A is still sharing, and A does not republish (publish is once per session). A Companion then sees `Absent` and falls back to manual connect for the rest of A's session. No security consequence; reachable only with the mutex not held. Listed under Known minor deviations.

### `FILE_FLAG_DELETE_ON_CLOSE` — considered and rejected

Attractive because it auto-cleans on process kill. Rejected because: (1) it does **not** survive power loss — NTFS does not replay the delete disposition across a dirty unmount — so §12.3's verification is required regardless, and once it exists `DeleteOnClose` closes only a sub-case; (2) it is incompatible with the atomic-replace write, forcing a truncate-in-place write a concurrent reader can observe half-written; (3) it holds a handle for the whole session, making the file non-copyable for diagnostics.

### Mux UI

**Nothing changes. No new string, no indicator, no settings row, no tray-menu item.** An on-screen "handoff published" line would be narration of an internal mechanism at the user — the exact register §6.5 line 1785 deletes. The only Mux-side observable is behaviour.

## 12.3 Liveness — the reader's rule

`MuxHandoffReader.TryRead()` (Companion, `src/CspPaletteCompanion.App/MuxHandoffReader.cs`) returns a discriminated result, **not `bool`/`null`** — §13.4 requires the UI to distinguish "not sharing" from "found but refused".

```csharp
internal enum MuxHandoffStatus
{
    Live, Absent, Malformed, VersionTooNew, Stale, Unverifiable, NotLoopback, PortNotOwned
}

internal readonly record struct MuxHandoffResult(MuxHandoffStatus Status, CompanionPairingInfo? Pairing);
```

**`TryRead` has a total, non-throwing contract.** Both callers — the 2-second `DispatcherTimer` tick and the connect loop — may rely on it. This is not decoration: `RefreshConnection` runs from a timer handler, and §9.4 mandates `e.Handled = false` in `DispatcherUnhandledException`, so *any* escaping exception is a crash loop at 0.5 Hz. Two 60-byte files reached that in the draft, both verified by execution.

Checks in order, cheapest and most conservative first. **Any failure returns immediately; nothing partial is carried forward.**

| # | Check | Failure → |
|---|---|---|
| **1** | `File.Exists` | `Absent` |
| **2** | Open with **`FileShare.ReadWrite \| FileShare.Delete`**; `IOException` / `UnauthorizedAccessException` | `Malformed` |
| **3** | **`stream.Length > MaximumFileBytes` (4096), taken from the opened handle** | `Malformed` |
| **4** | `JsonSerializer.Deserialize<MuxSessionDocument>(stream, …)`; `JsonException` **or a null document** | `Malformed` |
| **5** | `schemaVersion > 1` → `VersionTooNew`; `< 1` → `Malformed` | distinct statuses because the user-facing fix differs (§13.5) |
| **6** | **`string.IsNullOrWhiteSpace(document.PairingUrl)`** | `Malformed` |
| **7** | `processId <= 0` | `Malformed` |
| **8** | `using var process = Process.GetProcessById(pid)`; `ArgumentException` / `InvalidOperationException` | `Stale` |
| **9** | `process.ProcessName` vs `MuxProcessName` (`"CSP Mux"`), `OrdinalIgnoreCase` | `Stale` |
| **10** | `Math.Abs((process.StartTime.ToUniversalTime() − recorded).Ticks) <= StartTimeTolerance.Ticks` | `Stale` |
| **11** | `Win32Exception` / `InvalidOperationException` / `NotSupportedException` from `Process.StartTime` | **`Unverifiable` — fails closed** |
| **12** | `CompanionPairingCodec.Decode(pairingUrl)` with the default `allowPublicEndpoints: false`; **`ArgumentException or FormatException` → `Malformed`**; `InvalidOperationException` (**`CompanionPairingCodec.cs:108-111`**) → `NotLoopback` |  |
| **13** | `pairing.Addresses.Count >= 1` **and every** entry `IPAddress.IsLoopback` — not "any" | `NotLoopback` |
| **14** | **`NativeMethods.OwnsListener(document.ProcessId, address, pairing.Port)` is true for at least one decoded address** | `PortNotOwned` |

> **Step 3 must read the length off the opened stream, never off `FileInfo`.** A pre-open stat is a TOCTOU check, not a bound: the Mux's own `File.Move(overwrite: true)` performs exactly that replace at every session start, and step 2 mandates `FileShare.Delete` precisely so the replace can succeed while this reader holds the file. Measuring the previous inode and deserialising the new one is not a cap.

> **`FileShare.Delete` is required, not optional.** Without it in the share mask, the Mux's `File.Move(overwrite: true)` replace **and** its `StopAsync` delete both fail with a sharing violation during the moment the Companion happens to have the file open.

> **Steps 4 and 6 are both required, and `required` members do not replace either.** Verified by execution: `JsonSerializer.Deserialize<T>("null", …)` returns **null** even for a record whose every member is `required`; and an explicit `"pairingUrl": null` binds to null without a `JsonException`. `Nullable=enable` + `TreatWarningsAsErrors=true` makes this *worse*, not better — the compiler guarantees `PairingUrl` is non-null, so nobody implementing this section adds the check unless it is written here. Without step 6, `Decode(null)` throws `ArgumentNullException` from `CompanionPairingCodec.cs:58`; without step 4, `document.SchemaVersion` throws `NullReferenceException`. Both escape into the timer.

> **Step 14 is the check that makes the whole verification honest, and it is new.** Steps 8–10 prove *"a process with this PID, named CSP Mux, created at this instant is alive"*. They do **not** prove that process owns the port the credential is about to be sent to. `CompanionMultiplexerOptions.Port` defaults to **0** (`CompanionMultiplexerOptions.cs:11`) and the app never sets it, so the proxy always binds an ephemeral port; when that listener stops the port returns to the dynamic range and any unprivileged local process may bind it — **with the Mux process still running and every one of steps 8–10 passing**. §12.4(c)'s PID-recycle framing understated this: no recycling is required. `GetExtendedTcpTable(TCP_TABLE_OWNER_PID_LISTENER = 3)` returns owning PIDs to an unprivileged caller (this is `netstat -o`, not `netstat -b`) — **verified: correct for AF_INET and AF_INET6, rejects a wrong PID, and the row disappears the instant the listener stops. Measured at 109 µs per call.**

**How the start time is obtained on both sides.** Writer: `Process.GetCurrentProcess().StartTime.ToUniversalTime()`, serialised `"O"`. Reader: `process.StartTime.ToUniversalTime()`, backed on Windows by `GetProcessTimes`, whose `ftCreationTime` is a 100-ns FILETIME. **Verified: the managed and native values are exactly equal and the `"O"` round-trip is lossless.** The **one-second tolerance is kept anyway, deliberately**: if a future build routes the value through `DateTimeOffset` or a local-time conversion, exact equality converts a benign format change into a permanent, undiagnosable "Mux is not sharing" that no user can act on. One second is still eight orders of magnitude tighter than the PID-recycle window it defends.

**The native `OpenProcess` alternative — rejected, on corrected grounds.** The draft rejected `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `GetProcessTimes` because it "does not change the access-denied outcome for an elevated target". That is true — **verified: `err = 5` against `wininit`, `csrss` and `lsass`.** It was later claimed the native path should be adopted on *cost* grounds instead, because `Process.GetProcessById` "snapshots the entire process table and takes milliseconds". **That is not true here: measured at 43 µs per call versus 43 µs for the native path — identical.** The native path therefore buys nothing on either axis and is not adopted. `Process.*` is kept, **with `using`** — the draft never disposed the `Process`, and step 9 reads a property off it.

**No pre-flight socket probe.** Step 14 is a table lookup, not a handshake; the connect attempt is still the only socket opened, and it is bounded (§13.5 case 3).

**Polling cost — the exact rule.** `RefreshConnection` runs on a 2-second `DispatcherTimer` (`MainWindow.xaml.cs:47-51`).

| Situation | Work performed |
|---|---|
| `(File.GetLastWriteTimeUtc, length, existence)` unchanged **and** cached status is `Live` | **Steps 8–11 and 14 only** — measured `43 µs + 109 µs ≈ 152 µs` per tick, 0.008 % of one core at 0.5 Hz. This is what lets the strip fall out of S0 within 2 s when the Mux's listener stops without its process exiting. |
| Unchanged and cached status is not `Live` | Reuse the cached result verbatim. A `Malformed` file does not become `Live` without changing. |
| Changed | Full run, steps 1–14; cache the result **and** the decoded `CompanionPairingInfo`. |
| **At the moment of connect** | Full run, steps 1–14, **ignoring the cache**. This is the authoritative evaluation; everything else is a hint that drives wording. |

**The Companion never writes, never deletes and never creates anything under `%LOCALAPPDATA%\CSP Suite`.**

## 12.4 Security

### What is exposed

The file contains `CompanionMultiplexer.InvitationPassword` (`CompanionMultiplexer.cs:49`, from `CompanionAuthCodec.CreateRandomPassword()`), wrapped only by the XOR-with-a-7-byte-constant-plus-hex in `CompanionPairingCodec.Encode` (`CompanionPairingCodec.cs:47-53`).

**That XOR is not encryption.** The key is a compile-time constant in a shipped binary (`CompanionPairingCodec.cs:17`) and is byte-identical in both repos. **Treat the file as a plaintext credential** and design accordingly.

What that credential grants: authentication to the proxy's downstream listener (`CompanionMultiplexer.Authenticate`, lines 160-194), which forwards commands through `commandScheduler` → `upstream.SendRawAsync`. The holder gets full CSP Companion API access for as long as the Mux runs. Stated without softening.

### What is **not** exposed — CSP's upstream credential

Two independent, citable reasons:

1. **The Mux never retains CSP's password.** `UpstreamCompanionClient.AuthenticateAsync` (`UpstreamCompanionClient.cs:145-149`) mints `rotatedPassword = CompanionAuthCodec.CreateRandomPassword()` and sends `CreateAuthenticationDetail(pairing.Generation, pairing.Password, rotatedPassword)`. The QR-scanned password is consumed once, inside a local, and is never stored on the client object.
2. **`InvitationPassword` is independent.** Generated in the multiplexer's constructor from the same RNG with **no derivation from and no relationship to** the upstream password. The only value copied from upstream is `Generation` (`:50`), a CSP protocol-version marker, not a secret.

**The honest two-axis comparison** *(the draft's one-sided version is replaced)*:

> **Smaller blast radius** than leaking CSP's own pairing — the credential authenticates only to this proxy instance and dies with it, because `InvitationPassword` is per-`CompanionMultiplexer`-instance and a new instance mints a new one. **Larger exposure surface** — it is a durable file at a fixed, documented path for the whole session, where the QR was ephemeral, screen-gated, and hideable; the Mux even ships `HideQrAfterFirstConnection` (`AppPreferences.cs:11`, `MainForm.cs:362-367`) specifically to shorten that window, and this file is unaffected by it. Both are true and the design accepts the trade.

### Windows ACL — **write the ACL code**

*(This reverses the draft's "write no ACL code" instruction. The instruction rested on a claim about `%LOCALAPPDATA%` that is false on the target machine.)*

**Measured on the target machine:**

```
C:\Users\User\AppData\Local
   S-1-15-3-3557520199-…-3692855932:(I)(F)
   S-1-15-3-3557520199-…-3692855932:(I)(OI)(CI)(IO)(F)
   <machine>\User:(I)(OI)(CI)(F)
   NT AUTHORITY\SYSTEM:(I)(OI)(CI)(F)
   BUILTIN\Administrators:(I)(OI)(CI)(F)

C:\Users\User\AppData\Local\CSP Palette Companion        ← the precedent the draft cited
   <machine>\CodexSandboxUsers:(I)(OI)(CI)(RX)
   NT AUTHORITY\SYSTEM:(I)(OI)(CI)(F)
   BUILTIN\Administrators:(I)(OI)(CI)(F)
   <machine>\User:(I)(OI)(CI)(F)
```

There is a **fourth inheritable Full-Control ACE for an app-capability SID**, and the sibling folder carries a **non-owner local group with Read**. "No ACE for Users, Everyone, or Authenticated Users" and "readable only by that user, SYSTEM, and local administrators" are both wrong, and the draft's §9.5 `icacls` check would have failed on the developer's own box on day one.

This is not cosmetic. §12.4(b) below concedes that same-user *medium-integrity* processes are not a boundary. **AppContainer and low-integrity processes are the documented exception, and they are exactly the class that capability ACE admits.** A low-IL process cannot open the Mux with `PROCESS_VM_READ`, and UIPI blocks it from screen-scraping the Mux's medium-IL QR window — but it *can* read a medium-IL file with a permissive DACL. Inheritance alone would hand a live credential to the one same-user class that previously could not reach it. "Not making it worse" is the design's own criterion, and inheritance violates it.

**What is written, and what was verified about it:**

| Step | Verified |
|---|---|
| `FileSystemAclExtensions.Create(FileInfo, FileMode, FileSystemRights, FileShare, int, FileOptions, FileSecurity)` under `net8.0-windows` with **no `PackageReference`** | **works.** The removed `File.*` `FileSecurity` overloads are not the route; `FileInfo` is. |
| `SetAccessRuleProtection(isProtected: true, preserveInheritance: false)` | **required.** Adding an allow-ACE removes nothing; without protection the inherited ACEs — including the capability SID — stay and the file is no more private than before. |
| Create **with** the DACL rather than re-ACLing afterwards | **required.** A file created and then re-ACLed is readable for the interval between the two calls. |
| Put the DACL on the **temp** file | **verified correct.** `File.Move` within a volume preserves the source's explicit ACEs and does not re-inherit from the destination directory. Final measured DACL of the published file: `SYSTEM:(F)` and `<machine>\User:(F)` — **and nothing else.** The capability ACE is gone. |
| `FileSecurity.SetSecurityDescriptorSddlForm("S:(ML;;NRNW;;;HI)", AccessControlSections.Audit)` | **FAILS — `IOException: ERROR_PRIVILEGE_NOT_HELD`.** .NET maps any SACL to `SACL_SECURITY_INFORMATION`, which requires `SeSecurityPrivilege`. It fails for `;;;ME` too. **The managed API cannot set a mandatory label at all.** |
| A **High** label from a medium-IL process | **unreachable in principle.** Windows does not let a subject raise an object's label above its own token's integrity level. `HI` is not the right target even with the right API. |
| `SetNamedSecurityInfoW(path, SE_FILE_OBJECT, LABEL_SECURITY_INFORMATION, …)` from a medium-IL, unprivileged process | **works, rc = 0.** Setting a mandatory label needs only `WRITE_OWNER`, not `SeSecurityPrivilege` — that is the distinction .NET's managed surface does not expose. Final `icacls` shows `Mandatory Label\Medium Mandatory Level:(NW,NR,NX)`, and the label survives `File.Move`. |

**Therefore, `NativeMethods.TrySetMediumNoReadUpLabel` — new, in the Mux's `NativeMethods.cs`:**

```csharp
private const uint LABEL_SECURITY_INFORMATION = 0x00000010;
private const int  SE_FILE_OBJECT             = 1;

// Medium, not High: a medium-integrity process cannot raise an object above its own
// level. NR is the operative flag — it is what stops a low-IL / AppContainer process
// reading the file. NW and NX come along for free and cost nothing.
private const string MediumNoReadUpSddl = "S:(ML;;NRNWNX;;;ME)";

[DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
    string sddl, uint revision, out IntPtr pSecurityDescriptor, out uint size);

[DllImport("advapi32.dll", SetLastError = true)]
private static extern bool GetSecurityDescriptorSacl(
    IntPtr pSecurityDescriptor, out bool saclPresent, out IntPtr pSacl, out bool saclDefaulted);

[DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern uint SetNamedSecurityInfoW(
    string name, int objectType, uint securityInfo,
    IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);

[DllImport("kernel32.dll", SetLastError = true)]
private static extern IntPtr LocalFree(IntPtr handle);

/// <summary>
/// Best effort. The protected DACL is what actually closes the AppContainer hole;
/// this narrows it further against a low-integrity process running as the same user.
/// Managed FileSecurity CANNOT do this — it requests SACL_SECURITY_INFORMATION and
/// fails with ERROR_PRIVILEGE_NOT_HELD. LABEL_SECURITY_INFORMATION needs only
/// WRITE_OWNER, which the creator of the file has.
/// </summary>
internal static void TrySetMediumNoReadUpLabel(string path)
{
    if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(MediumNoReadUpSddl, 1, out var psd, out _))
        return;
    try
    {
        if (GetSecurityDescriptorSacl(psd, out _, out var pSacl, out _))
        {
            _ = SetNamedSecurityInfoW(path, SE_FILE_OBJECT, LABEL_SECURITY_INFORMATION,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, pSacl);
        }
    }
    finally { LocalFree(psd); }
}
```

**The Companion's settings file is not in scope here** and keeps `AppSettingsService`'s existing behaviour — it holds preferences, not a credential. The asymmetry is deliberate and is stated so it is not "fixed" for consistency.

### Threat model

**(a) Another user on the machine.** Cannot read the file — now by the file's own protected DACL, not by an inheritance assumption that the measurement disproved. A **local administrator** can, and can equally read the Mux's process memory, attach a debugger, or screenshot the QR. Not a boundary we can hold, and not one this design claims to hold.

**(b) Another process running as the same user, at the same integrity level.** **There is no boundary here and the design must not pretend one exists.** Such a process can read this file, read the Mux's memory, screen-scrape the QR, or hook the Companion. Windows does not isolate same-user, same-IL processes.

**(b′) Another process running as the same user at a *lower* integrity level, or in an AppContainer.** This *is* a boundary, and it is the one the feature would have broken. The protected DACL removes the capability-SID grant (verified) and the mandatory label adds no-read-up. Both are specified above.

What the design owes beyond that:

- Never `%TEMP%` (world-traversable, and a place hygiene tools index), never `%PROGRAMDATA%` (machine-wide by default), never the registry, never `HKLM`.
- **The pairing URL must never enter any string that reaches the UI.** Not `DetailText`, not `StatusText`, not a tooltip, not a shown exception message. This matters more than it looks: `SetStatus` mirrors the detail string into `DetailText.ToolTip` (`MainWindow.xaml.cs:543`, mandatory per §2.10 line 523), and `DetailText` carries `AutomationProperties.LiveSetting="Polite"` — **it is spoken aloud by assistive technology.**
- **And it must never enter a log, because there is no log.** Verified: neither repo has any logging facility. The rule is therefore absolute rather than conditional: no diagnostic sink is introduced by this feature, and if one is ever added it may record the `MuxHandoffStatus` enum name and the exception *type*, never `MuxSessionDocument`, `PairingUrl`, `CompanionPairingInfo.Password`, or any string derived from them.
- Immediate revocation on Mux exit.

**(c) Stale file pointing at a port now owned by something else.** The sharpest case, and the draft framed it too narrowly. Two routes reach it, not one:

1. *PID recycled.* Mux killed → PID reused → the stale file still names it. Blocked by checks 8–10: the conjunction of a **live** process, named `CSP Mux`, created within one second of the record is not reachable by accident.
2. *Listener gone, process alive.* **No recycling required.** The multiplexer disposed without going through `StopAsync`, or a future code path did the same; the ephemeral port (default `Port = 0`) was released and rebound by an unrelated local process. Every one of checks 8–10 passes on the **real** Mux. **This is what check 14 exists for**, and nothing else in the design would have caught it.

Residual, honestly stated:

3. **If an impostor still receives the password, it receives a string that authenticates to nothing.** It is not CSP's credential, and it is dead the moment the real multiplexer instance is gone.
4. **What check 14 does not prove.** PID + creation time + image name + port ownership proves *"the process that owns this port is a live process named CSP Mux"*. PID and creation time are readable by any unprivileged process (`Process.GetProcesses()` needs no privilege), so a forged file can honestly record a live process's identity — but it cannot make that process own a port it does not own. The residual is a hostile process that has genuinely named itself `CSP Mux`, is genuinely listening on loopback, and has written a file naming itself. At that point it has already achieved same-user code execution, which is case (b). **Not a boundary; named so nobody concludes it was overlooked.** The fix, if it ever matters, is a challenge/response over the file channel — the Companion writes a nonce to a sibling file and the real Mux echoes it — and it is **explicitly out of scope for v1**.

### Where loopback is re-validated — **two places, honestly counted**

The draft claimed three independent places. It was one. The corrected structure:

1. **Caller-side, in `MuxHandoffReader.TryRead`** — `Decode` with `allowPublicEndpoints: false` (step 12) and the loopback-only gate (step 13). These are two adjacent steps of **one method**, not two defences; they are counted once. Their job is to produce the *named UI status* (`NotLoopback` → §13.5's string). The file path must be tighter than the QR path because **the user did not point a camera at anything** — there is no human confirmation step, and `IsPrivateOrLocal` accepts `10.x`, `172.16-31.x`, `192.168.x`, `169.254.x` and IPv6 ULA/link-local. **Do not loosen the codec to accommodate this.**
2. **Sink-side, inside `CompanionCanvasService.ConnectThroughMuxAsync`** — a hard `InvalidOperationException` before any socket is opened. This is the genuinely independent one, and it is where the rule belongs: that method is what transmits `pairing.Password` to every address in the list (`CompanionModeClient.cs:65-81`). Any future second caller — a test seam, a paste-a-URL affordance, a retry helper — is covered by construction.
3. **`CompanionMultiplexer`'s constructor guard** (`CompanionMultiplexer.cs:34-42`) runs **in the Mux process**, on the far side of the boundary this section has just declared untrusted. **It is not counted as a defence for the Companion**, and citing it as one was the error.

---

# 13. COMPANION AUTO-CONNECT

Companion only. Amends §4.1, §4.5, §4.6, §4.7, §6.

## 13.0 New and changed files

| Path | Fate |
|---|---|
| `src/CspPaletteCompanion.App/MuxHandoffContract.cs` | **New**, §12.0 |
| `src/CspPaletteCompanion.App/MuxHandoffReader.cs` | **New.** §12.3's rule, including the caching rule. |
| `src/CspPaletteCompanion.App/NativeMethods.cs` | `OwnsListener` (`GetExtendedTcpTable`) |
| `src/CspPaletteCompanion.App/CompanionCanvasService.cs` | `AdoptAsync` extraction **with a behavioural fix**, `ConnectThroughMuxAsync`, `Route`; eight string rewrites (§6.7-B) |
| `src/CspPaletteCompanion.App/CspAcquisitionService.cs` | Two one-line delegations |
| `src/CspPaletteCompanion.App/MainWindow.xaml` | Connection-strip internals; the Connection card in `SettingsView` |
| `src/CspPaletteCompanion.App/MainWindow.xaml.cs` | Poll classification, route branch, `ManualConnectButton_Click`, `MuxHandoffToggle_Click`, `_autoConnectRequested` clearing |
| `src/CspPaletteCompanion.Core/Settings/AppSettings.cs` | `UseMuxWhenAvailable` |

## 13.1 The setting

```csharp
/// <summary>
/// Reads the CSP Mux session handoff file and connects through the proxy
/// instead of scanning CSP's QR code.
/// </summary>
public bool UseMuxWhenAvailable { get; init; }
```

Default `false`. **No `SchemaVersion` bump** — identical argument to §3.5 line 833. **Not added to `CapturePermission` and not answered by `IsAllowed`.**

**Card — §4.5-R item 4, byte-identical geometry to the Window card:**

```xml
<Border Style="{StaticResource CardStyle}" Margin="0,0,0,12">
  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="12"/>
      <ColumnDefinition Width="44"/>
    </Grid.ColumnDefinitions>
    <StackPanel Grid.Column="0">
      <TextBlock Style="{StaticResource BodyStrongTextStyle}"
                 Text="Use CSP Mux when it is running"/>
      <TextBlock Style="{StaticResource CaptionTextStyle}" Margin="0,4,0,0"
                 Text="Connects through CSP Mux instead of scanning CSP&#8217;s QR."/>
    </StackPanel>
    <CheckBox x:Name="MuxHandoffToggle" Grid.Column="2"
              Style="{StaticResource ToggleSwitchStyle}"
              VerticalAlignment="Center"
              AutomationProperties.Name="Use CSP Mux when it is running"
              Click="MuxHandoffToggle_Click"/>
  </Grid>
</Border>
```

Height `1 + 8 + 38 + 8 + 1 = ` **56**; **68** in the stack.

**Caption width, restated at the spec's own metric.** The draft used **5.1 px/char** for 11 px Small — a constant that appears nowhere in v1.0 (§4.1 uses 5.65, §4.1's `SourceHelp` 5.63, §2.10 line 523 uses 5.57) and whose only effect was to make a too-long string look fine. At **≈5.6 px/char**: content column = `420 − 2 − 20 − 12 − 44 = 342 px` → **61 characters**. The caption is **54**. ✔ One line, so the 16 px reservation holds and 56 is real.

**The caption says `CSP Mux`, not `the proxy`.** `proxy` was the draft's word, justified as "§6.5's established suite word" — but §6.5 *deletes* every user-visible Mux string containing it (`Scan the proxy QR`, `Proxy QR`, `Proxy online`, `New proxy sessions will…`), leaving exactly one survivor, in the *other* app, on §9.3's allowlist. One surviving instance is not an established vocabulary, and the card would otherwise give one thing two names four lines apart. This is the same offence `handoff` is banned for.

**Placement — position 4, after `AutoActionOptionsPanel`, before the Window card.** Not in the Permissions card: all three permission rows are `CapturePermission`-backed and `IsAllowed` enumerates them, so adding this would force either a fourth `CapturePermission` member — making `IsAllowed` answer a routing question — or a special-cased toggle inside a card with a uniform contract. Not between items 2 and 3: §4.5 line 1021 drives `AutoActionOptionsPanel`'s visibility from the third permission toggle. Not first: the permissions are the page's headline.

**No tooltip.** **No "requires…" clause** — the row has no dependency on any other setting.

**One writer, and it is the handler.** Wired to **`Click`**, guarded by `_loadingSettings`:

```csharp
private async void MuxHandoffToggle_Click(object sender, RoutedEventArgs e)
{
    if (_loadingSettings) return;
    _settings = _settings with { UseMuxWhenAvailable = MuxHandoffToggle.IsChecked == true };
    await SaveSettingsAsync();
}
```

`ApplySettingsToUi` (~`:650`) gains the single read `MuxHandoffToggle.IsChecked = _settings.UseMuxWhenAvailable;`. **Nothing is added to `PermissionToggle_Click`'s `with` block at `:716-721`** — the draft specified a second writer there, which would have made every permission click re-derive a routing setting from UI state, and would have wired the two toggles introduced by the same extension in two different ways for no reason. `TrayModeToggle` and `MuxHandoffToggle` are now symmetrical.

Turning the setting off while connected through the Mux does **not** drop the live connection; it gates the next connect. No state on screen claims otherwise, so no sentence is needed.

## 13.2 New types, and the refactor that keeps one connection invariant

```csharp
internal enum ConnectionRoute { None, Csp, Mux }
```

### `AdoptAsync` — an extraction **with one required behavioural change**

The draft said "do not duplicate lines 31-56; extract them." Extracting them verbatim preserves a latent leak that the new 3-second timeout makes reachable.

**The defect, verified in the shipped source:** `await connectionGate.WaitAsync(cancellationToken)` is at `CompanionCanvasService.cs:31`; the `try` that owns `catch { await candidate.DisposeAsync(); throw; }` opens at `:32`. A cancellation while blocked on the gate throws from `:31`, **outside** the catch, dropping an already-connected, already-authenticated `CompanionModeClient` with its `TcpClient` open. The gate is genuinely contended — `ResetClientAsync` (`:255-271`) holds it across `previous.DisposeAsync()`, which awaits the receive loop. With a 3 s linked CTS the sequence "extraction fails → `ResetClientAsync` holds the gate → user presses Connect → loopback auth completes in microseconds → 3 s expires waiting on the gate" leaks a live authenticated downstream session that permanently consumes one of `CompanionMultiplexerOptions.MaximumClients` (default 8; `AcceptLoopAsync` silently disposes new sockets past the cap, `CompanionMultiplexer.cs:134-138`) and permanently occupies an entry in the broker's `reconnectCredentials` dictionary — while the UI reports S7. Eight of those and the Mux stops accepting anyone.

```csharp
private ConnectionRoute _route;
internal ConnectionRoute Route => _route;

private async Task AdoptAsync(CompanionModeClient candidate, ConnectionRoute route)
{
    // The gate is acquired with CancellationToken.None, NOT the caller's token.
    // Adoption is a bounded, non-cancellable handoff: the candidate is already
    // connected and authenticated, so the only correct outcomes are "published" or
    // "disposed". This is a DEVIATION from lines 31-56 as shipped, and it is required.
    try { await connectionGate.WaitAsync(CancellationToken.None); }
    catch { await candidate.DisposeAsync(); throw; }

    var swapped = false;
    try
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsConnected) { await candidate.DisposeAsync(); return; }

        var previous = client;
        client  = candidate;
        _route  = route;
        swapped = true;
        if (previous is not null) { await previous.DisposeAsync(); }
    }
    catch when (!swapped)          // never dispose a client already published to `client`
    {
        await candidate.DisposeAsync();
        throw;
    }
    finally { connectionGate.Release(); }
}
```

The `when (!swapped)` filter is the second correction: the shipped `catch` would dispose the candidate even after `client = candidate`, i.e. dispose the live connection out from under itself. `_route` is cleared in `ResetClientWithoutLockAsync` (`:273-281`).

### `ConnectAsync` and `ConnectThroughMuxAsync`

```csharp
internal async Task ConnectAsync(CancellationToken cancellationToken)
{
    ObjectDisposedException.ThrowIf(disposed, this);
    if (IsConnected) return;

    var pairingUri = await scanner.ScanUntilFoundAsync(cancellationToken);
    var pairing    = CompanionPairingCodec.Decode(pairingUri.AbsoluteUri);
    var candidate  = await CompanionModeClient.ConnectAndAuthenticateAsync(pairing, cancellationToken);
    await AdoptAsync(candidate, ConnectionRoute.Csp);
}

internal async Task ConnectThroughMuxAsync(CompanionPairingInfo pairing, CancellationToken cancellationToken)
{
    ObjectDisposedException.ThrowIf(disposed, this);
    ArgumentNullException.ThrowIfNull(pairing);

    // SINK-SIDE INVARIANT (§12.4). MuxHandoffReader checks this too, but this is the
    // method that transmits pairing.Password to every address in the list
    // (CompanionModeClient.cs:65-81), so the rule lives where the credential leaves the
    // process — not only in the one caller that exists today. Decode's own guard
    // (CompanionPairingCodec.cs:108-111) accepts 10/172.16-31/192.168/169.254 and IPv6
    // ULA; the file path has no human confirmation step and must be tighter.
    if (pairing.Addresses.Count == 0 || !pairing.Addresses.All(IPAddress.IsLoopback))
    {
        throw new InvalidOperationException("Refusing to authenticate to a non-loopback endpoint.");
    }

    if (IsConnected) return;

    var candidate = await CompanionModeClient.ConnectAndAuthenticateAsync(pairing, cancellationToken);
    await AdoptAsync(candidate, ConnectionRoute.Mux);
}
```

**`CspAcquisitionService.ConnectThroughMuxAsync(pairing, ct)`** and **`CspAcquisitionService.Route`** are one-line delegations matching the existing shape at `CspAcquisitionService.cs:17-18` and `:15`.

**`MainWindow` fields:** `private bool _manualConnectRequested;` and `private MuxHandoffResult _handoff;`.

## 13.3 Decision tree

### Setting **OFF** — the default

`RefreshConnection` (`:118`) **does not touch the handoff file at all**. Connect press → existing behaviour, byte-for-byte: `ConnectButton_Click` (`:183`) → `StartConnectionLoop` (`:201`) → `RunConnectionLoopAsync` (`:214`) → `_locator.Find()` → foreground CSP (`:233-235`) → `_acquisition.ConnectCompanionAsync` → `scanner.ScanUntilFoundAsync`. **Zero file or process I/O added to the poll, and the connection strip is pixel-identical to §4.1's 56 px in every state.**

### Setting **ON**

```
2-second RefreshConnection poll (:118), while not connected and no connect task running:
    _handoff = MuxHandoffReader.TryRead()          // cached per §12.3's polling rule
    Live        → S0
    Absent      → S1
    all others  → S4   (VersionTooNew / Malformed / Stale / Unverifiable /
                        NotLoopback / PortNotOwned)

ConnectButton_Click / ManualConnectButton_Click:
├─ _connectTask running? → cancel; "Stop" semantics, unchanged (:185-194)
└─ else:
     _manualConnectRequested = ReferenceEquals(sender, ManualConnectButton);
     _autoConnectRequested   = true;
     StartConnectionLoop();

RunConnectionLoopAsync, at the top of each iteration:
├─ !_settings.UseMuxWhenAvailable || _manualConnectRequested
│      → QR route: existing path (:220-257). States S6 → S5 → S3.
│        On adopt: _autoConnectRequested = false;   ← NEW, see below
└─ else: full re-read (steps 1-14, cache ignored — this is the moment of commitment)
   ├─ Live → S2; ConnectThroughMuxAsync(pairing, linked 3 s token)
   │           ├─ adopted → _autoConnectRequested = false;
   │           │            SetStatus("Ready · through CSP Mux", string.Empty);
   │           │            ApplyStatusTone(StatusTone.Neutral);
   │           │            RefreshConnection(); return;
   │           └─ threw   → S7; _autoConnectRequested = false; return
   ├─ Absent → S1; _autoConnectRequested = false; return
   └─ else   → S4; _autoConnectRequested = false; return
```

**`_autoConnectRequested` is cleared on every successful adopt, on both routes. This is new code and it is required.**

The draft claimed "`_autoConnectRequested` is already false (the loop returned at `:245`)". **That is false.** The flag is assigned in exactly four places — declaration `:35`, the `Closing` lambda `:57`, the Stop branch `:187`, and the connect press `:196`. Line 245 is a bare `return;`. So after a Mux exit drops the connection, `RefreshConnection` falls past the connected early-return at `:132` and reaches `if (_autoConnectRequested && !_closing) StartConnectionLoop();` at `:177-180` with the flag still **true** — restarting the loop unattended, on the **Mux** branch (because `_manualConnectRequested` was cleared in the existing `finally` at `:260-269`), silently reconnecting through a proxy the user may have deliberately stopped, and silently overriding a user who had explicitly chosen "Scan CSP's QR instead". That is precisely what the user's decision forbids.

**Stated behaviour change:** the QR route also stops auto-restarting after a drop. Today a dropped connection silently re-enters the scan loop, which foregrounds CSP and scans every display with no user action. Under one rule for both routes, a drop returns the strip to its pre-connect state and waits for a press. This is a deliberate, user-visible change and it is the same rule the user's decision imposes on the Mux route; applying it to one route only would have been the inconsistency.

**The Mux route never loops and never falls through to the QR scan.** `_manualConnectRequested` is cleared in the existing `finally` (`:260-269`) so the next automatic attempt is automatic again. The 2-second poll independently moves the strip S1 → S0 the moment the Mux starts sharing, with no user action — which is why `Absent` does not need to wait.

## 13.4 UI surface

### No new component

Everything is §4.1's connection strip (`Border` + `StripStyle`, `Grid ColumnDefinitions="*,8,Auto"`, col 2 = `ConnectButton` with `CompactButtonStyle`) and §2.10's status strip.

**The two-action problem in S0.** S0 must offer both routes.

| Option | Verdict |
|---|---|
| A second `CompactButtonStyle` button in col 2 | **Rejected.** Widens the trailing group to `76 + 8 + 76 = 160`, cutting the instruction column from 322 to 238 px and dropping §6's budget from 57 to ~42 characters — invalidating a documented number in §4.1 line 924 for *every* state. |
| A `LinkButtonStyle` button inline at the end of the instruction row | **Chosen.** `LinkButtonStyle` already exists (§2.3), is T4 — the same style as `ConnectionInstructions` — and already serves `OpenPaletteButton`/`ShowSettingsFileButton`. |
| Repurpose `ConnectButton.Content` between routes | **Rejected.** §4.1 line 926 is a hard contract that `.Content` is a bare string rewritten by the 2-second poll, and one button cannot offer two routes. |

### Strip internals — replaces §4.1's "Connection strip internals" paragraph

```xml
<Border x:Name="ConnectionPanel" Style="{StaticResource StripStyle}"
        Background="{StaticResource PanelBrush}"
        BorderBrush="{StaticResource BorderBrush}"
        Padding="10,8" Margin="0,0,0,12">
  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>

    <Grid Grid.Column="0">
      <Grid.RowDefinitions>
        <RowDefinition Height="18"/><RowDefinition Height="4"/><RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>

      <TextBlock x:Name="ConnectionHeading" Grid.Row="0"
                 Style="{StaticResource BodyStrongTextStyle}"
                 TextWrapping="NoWrap" TextTrimming="CharacterEllipsis"
                 AutomationProperties.LiveSetting="Polite"/>

      <!-- Two columns, not three. A Collapsed child zeroes an Auto column but NEVER a
           fixed one, so a "*,8,Auto" grid would leave 8 px stranded in every state
           except S0 — making the instruction column 314 px, not the 322 px §4.1 line
           924 derives the 57-character budget from. The gap rides on the button. -->
      <Grid Grid.Row="2">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <TextBlock x:Name="ConnectionInstructions" Grid.Column="0"
                   Style="{StaticResource CaptionTextStyle}"
                   MinHeight="16" VerticalAlignment="Center"
                   AutomationProperties.LiveSetting="Polite"/>
        <Button x:Name="ManualConnectButton" Grid.Column="1"
                Style="{StaticResource LinkButtonStyle}"
                Margin="8,0,0,0"
                Content="Scan CSP&#8217;s QR instead"
                Visibility="Collapsed"
                AutomationProperties.Name="Connect to CSP by scanning its QR code"
                Click="ManualConnectButton_Click"/>
      </Grid>
    </Grid>

    <Button x:Name="ConnectButton" Grid.Column="2" Style="{StaticResource CompactButtonStyle}"
            MinWidth="76" VerticalAlignment="Center" Click="ConnectButton_Click"/>
  </Grid>
</Border>
```

**Two derived heights, both sanctioned by §4.1 line 924:**

```
Every state except S0 (link Collapsed → its column AND its margin collapse; row 2 = 16):
  border 1 + pad 8 + heading 18 + xs 4 + row2 16 + pad 8 + border 1 = 56    (§4.1, unchanged)
  instruction column = 428 − 2 border − 20 padding − 8 gap − 76 button      = 322  ✔
S0 only (link Visible; its 28 drives row 2):
  border 1 + pad 8 + heading 18 + xs 4 + row2 28 + pad 8 + border 1 = 68
  instruction is empty in S0, so the 322 − (link 130 + margin 8) = 184 px left is free
```

Link width: `Scan CSP's QR instead` is 21 characters ≈ 118 px at T4 plus `Padding="6,0"` = **130**.

§4.1 line 924 already declares the strip `Auto` with 56 as nominal and that a taller string "grows the strip and shrinks the tray rather than clipping". A 68 px strip in one pre-connect state is that clause used as intended. **Tray consequences, against §4.1's own column sums** (§4.1's `380`/`428` fixed totals with row 0 at `56+12 = 68` become `68+12 = 80`, i.e. `+12`):

```
Idle, disconnected, Mux available (S0), no chip:
  380 − 68 + 80 = 392  →  tray 555 − 392 = 163  →  interior 145.  4 swatch rows = 144 ≤ 145 ✔
Result, disconnected, Mux available (S0), chip visible:
  428 − 68 + 80 = 440  →  tray 115  →  interior 97  →  2 rows, scrolls
```

The second case scrolls, which §4.4 line 1001 already designates as the `ScrollViewer`'s job in the disconnected-plus-result state. **Nothing clips.**

**`ConnectionInstructions` gains `MinHeight="16"` — load-bearing and new.** S0 and S2 both set it empty, and §6.2 line 1662 already establishes that an empty instruction string exists in this app. With `LineStackingStrategy="BlockLineHeight"` an empty `TextBlock` measures 0, which would shrink the strip 56 → 40 and make it jump on entering S2.

**`ManualConnectButton` is visible in exactly one state — S0.** In S1/S4/S7 the `ConnectButton` itself runs the QR path, so a second control offering the same action would violate §6's rule 1.

### Exact per-state content

`ConnectButton.Content` is a bare string in every row — the §4.1 line 926 contract.

| State | Dot / `ConnectionText` (§3.3) | `ConnectionPanel.BorderBrush` | `ConnectionHeading` (T2s) | `ConnectionInstructions` (T4) | `ConnectButton.Content` | `ManualConnectButton` |
|---|---|---|---|---|---|---|
| **S0** Mux sharing, not connected | `SubtleBrush` / `Offline` | `BorderBrush` | `CSP Mux is sharing` | *(empty)* | `Connect` | **Visible** |
| **S1** Mux not sharing (file absent) | `SubtleBrush` / `Offline`; `ErrorBrush` / `Disconnected` when `_locator.Find()` is non-null | `BorderBrush` | `CSP Mux is not sharing` | `Start sharing in CSP Mux, or connect to CSP.` | `Connect` | Collapsed |
| **S2** connecting through the Mux | `WarningBrush` / `Connecting` | `WarningBrush` | `Connecting through CSP Mux` | *(empty — the 16 px line is held by `MinHeight`)* | `Stop` | Collapsed |
| **S3** connected (either route) | `AccentBrush` / `Connected` | — | *strip `Collapsed`* (`:127`) | — | — | — |
| **S4** file found, refused | `SubtleBrush` / `Offline` | `ErrorBrush` | `Cannot use CSP Mux` | per cause — §13.5 case 2, five strings | `Connect` | Collapsed |
| **S5** scanning for CSP's QR | `WarningBrush` / `Scanning` | `WarningBrush` | `Waiting for CSP's QR code` | §6.7-C's re-cut string | `Stop` | Collapsed |
| **S6** QR route, CSP not running | `WarningBrush` / `Scanning` | `WarningBrush` | `Open Clip Studio Paint` | `Waiting for Clip Studio Paint.` | `Stop` | Collapsed |
| **S7** Mux connect failed | `ErrorBrush` / `Failed` | `ErrorBrush` | `CSP Mux is not answering` | per cause — §13.5 cases 3–4, three strings | `Connect` | Collapsed |

> **Tone note — a neutral `BorderBrush` at a site that has never written one.** `ConnectionPanel.BorderBrush` is written today only as `WarningBrush` (`:144`, `:232`) or `ErrorBrush` (`:155`). S0 and S1 introduce `FindResource("BorderBrush")`. Intentional, and it needs the code change stated explicitly: a Mux that is available, or a Mux the user has simply not started, is neither a warning nor a failure. S4 keeps `ErrorBrush` because a *refused* file is genuinely anomalous — something exists and does not verify.

### "Which path is in use", post-connect

`RefreshConnection` collapses the strip when connected (`:127`), so there is no persistent strip surface in S3. The route is announced **once**, in the status strip — the "what the app is doing" surface (§5.2 line 1187), which is idle at that instant — from inside the **Mux `adopted` branch**, not from `:244`:

```csharp
// In RunConnectionLoopAsync's Mux branch, immediately after ConnectThroughMuxAsync returns.
_autoConnectRequested = false;
SetStatus("Ready · through CSP Mux", string.Empty);
ApplyStatusTone(StatusTone.Neutral);
RefreshConnection();
return;
```

**Two corrections against the draft, both load-bearing.**

*Placement.* The draft put this at `MainWindow.xaml.cs:244` — inside the QR route's success block (`:237-247`, reached only via `await _acquisition.ConnectCompanionAsync`). §13.3 gives the Mux route its own disjoint exit, so a `Route == Mux` guard at `:244` is **dead code** and the string would never have appeared. Line `:244` is left alone.

*Wording.* The draft's `Connected through CSP Mux` was justified as "one transient line … `ClearPaletteResult` is what clears this". **It is not.** `ClearPaletteResult` (`:552-560`) touches `PalettePreview`, `PalettePlaceholder`, `PaletteDragChip`, `OpenPaletteButton` and `_lastPalettePath` — nothing else — and §4.7 edit 2's `ApplyStatusTone` writes `StatusDot.Fill`, `StatusPanel.BorderBrush` and `StatusPanel.Background`, never `StatusText`. The line would have been **permanent** through the whole connected-idle state, sitting beside a title bar reading `Connected`: two simultaneously visible surfaces saying the same thing, which is §6's rule 1 and the exact ground on which `Connected to CSP` is cut for the QR route.

`Ready · through CSP Mux` shares no word with `Connected`. `Ready` is already this app's idle `StatusText` (§6.1 line 344, kept), so the line reads as the existing idle state plus the one fact that exists nowhere else. Its reset is the next extraction's `SetProgress`/`SetSuccess`/`SetFailure` — a real site, not an imagined one — and until then the sentence is simply true. Tone `Neutral`, not `Good`: `Good` belongs to extraction results.

**The QR route writes nothing.** `ConnectionText` already reads `Connected` and the user watched the scan happen.

**Where the route is actually chooseable, and therefore where it is actually named:** S0's heading beside a `Connect` button, the S0 link, and S2's heading. Once connected, switching routes requires disconnecting; the choice is offered at connect time, which is the only moment it is meaningful. Stated so it is not filed as a gap.

### Automation and tooltips

**`ConnectionHeading` is now a live region (§4.6-A).** Without it, the two resting states this whole feature exists to expose — S0 and S1, one word apart — are **silent** to assistive technology, because S0/S2 deliberately empty `ConnectionInstructions`, which is the only live region the strip had. S1 → S0 is driven by the 2-second poll with no user action and also makes a new control appear.

All writes to `ConnectionHeading` go through a setter mirroring `SetConnectionInstructions` (`:284-292`), so the ordinal guard still throttles the poll:

```csharp
private void SetConnectionHeading(string text)
{
    if (string.Equals(ConnectionHeading.Text, text, StringComparison.Ordinal)) return;
    ConnectionHeading.Text = text;
    Announce(ConnectionHeading);
}
```

The three existing bare assignments (`:162`, `:172`, `:229`) are retargeted to it.

**`ConnectButton` carries an `AutomationProperties.Name` that the draft forgot.** The existing code writes it at the same sites as the tooltip — `:147` and `:154` — so in S0 the button would have announced `Connect to CSP Companion Mode` while connecting through CSP Mux, and in S2 `Stop connecting to CSP Companion Mode` while stopping a Mux connect: both wrong, and both contradicting the sighted tooltip introduced beside them.

| State | `ConnectButton.ToolTip` | `ConnectButton` `AutomationProperties.Name` |
|---|---|---|
| S0 | `Connect through CSP Mux` — **new** | `Connect through CSP Mux` — **new** |
| S1 / S4 / S7 | `Scan a Connect to smartphone QR code` — **new, route-neutral** | `Connect to CSP Companion Mode` (unchanged, `:154`) |
| S2 | `Stop connecting` — **new** | `Stop connecting through CSP Mux` — **new** |
| S5 / S6 | `Stop scanning` (§6.2 line 1650) | `Stop connecting to CSP Companion Mode` (unchanged, `:147`) |

**Why S1/S4/S7's tooltip is route-neutral.** The draft assigned §6.2 line 1652's `Scan CSP's Connect to smartphone QR code` here, while three of the four S4 instructions and one S7 instruction point at the **Mux's** QR — an instruction and a tooltip naming different codes, on the same button, visible together the moment the user hovers. The draft itself establishes that the distinction is load-bearing (`CSP's` is called "load-bearing — this suite has two QR codes"). The neutral tooltip is not a compromise, it is the accurate statement: `CompanionQrScanner` is constructed with the predicate `uri => CompanionPairingCodec.TryDecode(uri.AbsoluteUri, out _)` (`CompanionCanvasService.cs:8-9`), which accepts **any** valid pairing URL, scans all displays, and decodes whichever it finds first. **The scanner genuinely cannot be steered at one of the two codes.** The per-cause instruction says which code to *put on screen*; the button says what it will *do*. `ManualConnectButton` keeps `Scan CSP's QR instead` because in S0 the alternative genuinely is CSP's own code, and the fuller-name-than-label pattern at `:147`/`:154` applies to its automation name.

## 13.5 Failure modes

### 1. Mux running, multiplexer not started

The user has the Mux open but has not pressed `Scan CSP QR`. No file exists — the Mux writes only after `multiplexer.StartAsync` returns. Status `Absent` → **S1**.

Heading `CSP Mux is not sharing`, **not** "is not running", deliberately: the Companion **genuinely cannot distinguish** "Mux closed" from "Mux open but idle", and the copy must not claim a distinction it cannot make. It also borrows the Mux's own word for exactly this condition — §6.5 line 1779 fixes `Not sharing` as the Mux's `StatusText` for the not-yet-started proxy — so one condition has one word across the suite. S0's `CSP Mux is sharing` and S1's `CSP Mux is not sharing` then differ by one word, which is precisely the information.

### 2. File present but not usable — five causes, five fixes

All five are **S4**: `ConnectionText = Offline`, `BorderBrush = ErrorBrush`, `ConnectionHeading = Cannot use CSP Mux`, `ConnectButton.Content = Connect` running the QR path.

| Status | `ConnectionInstructions` | Chars | Why it is separate |
|---|---|---|---|
| `VersionTooNew` | `CSP Mux is newer than this app. Update Companion.` | 49 | The fix is a Companion update; nothing about the Mux is wrong |
| `Malformed` | `Cannot read CSP Mux. Scan CSP&#8217;s QR instead.` | 43 | The draft's `The CSP Mux session file is unreadable.` named an internal artefact the user has no model for, cannot see, and — by the row's own admission — cannot act on. That is narration, the same offence `handoff` is banned for. This version names the app and gives the route, matching the shape of its four siblings |
| `NotLoopback` | `CSP Mux is sharing on a network. Scan its QR.` | 45 | Correct in both readings: LAN mode is where the Mux deliberately does not publish, so this file is either a stale scope transition or forged — and in both cases the Mux's **own** QR is the right route |
| `Unverifiable` | `Could not verify CSP Mux. Scan its QR instead.` | 47 | The elevated-Mux case. Verified: `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` returns `ERROR_ACCESS_DENIED` against a higher-integrity target, and `Process.StartTime` fails the same way. The QR path works across integrity levels because it is a screen capture, not a process query |
| `PortNotOwned` | `CSP Mux is not sharing on that port. Scan its QR.` | 51 | **New.** The Mux process is alive and correct but does not own the port the file names — the listener stopped, or something else rebound the ephemeral port. Distinct from `Stale` (process gone) and from S7 (verified, then unreachable) because the state is "the Mux is running but this file is out of date" |

All ≤ 57 (§4.1 line 924). The Companion never deletes any of these files.

### 3. File present, verified, port closed

Reachable when `StopAsync` deletes the file one tick after the connect-moment verification, or when the listener closes between check 14 and the socket.

`CompanionModeClient.ConnectAndAuthenticateAsync` catches `SocketException` per address (`CompanionModeClient.cs:83-87`) and, having exhausted `pairing.Addresses`, throws `IOException("Could not connect to any companion endpoint.")` (`:95`). → **S7**, instruction `Restart sharing in CSP Mux, or scan CSP's QR.` (45). Loop stops.

**The raw exception is neither shown nor logged.** It is discarded. There is no logging facility in either repo, and the surrounding scope holds the pairing URL.

**Bound the connect.** Link a `CancellationTokenSource(TimeSpan.FromSeconds(3))` into the token passed to `ConnectThroughMuxAsync`. A loopback connect either completes in microseconds or is refused immediately; the only way to hang is a filtered loopback port, and an unbounded hang leaves the strip on `Connecting` indefinitely with only `Stop` to escape. On expiry: **S7** with `CSP Mux did not answer. Connect to CSP instead.` (47). **Distinguish the timeout from a user cancel via `token.IsCancellationRequested`** — the identical pattern §5.7 lines 1407-1411 specifies for the Mux. **And note §13.2's `AdoptAsync` gate fix: it is this 3 s CTS that makes the shipped leak at `CompanionCanvasService.cs:31` reachable.**

### 4. Proxy refuses authentication

The multiplexer restarted between the read and the connect (regenerating `InvitationPassword` at `CompanionMultiplexer.cs:49`), or `Authenticate` rejects for `PasswordMismatch` (`:171-193`). `CompanionAuthCodec.ParseResult` → `IsAuthenticated == false` → `UnauthorizedAccessException` at `CompanionModeClient.cs:141-143`. → **S7**, instruction `CSP Mux refused the connection. Scan its QR.` (44). Separate from case 3 because the fix differs: rescan, not restart.

### 5. Mux shuts down while the Companion is connected through it

`CompanionMultiplexer.DisposeAsync` (`:88-118`) disposes every `DownstreamSession` (`:99-102`), which disposes the `TcpClient`. The Companion's receive loop sees `IOException`/`EndOfStreamException` and `IsAuthenticated` goes false — observable at `CompanionCanvasService.IsConnected` (`:15`), which the 2-second poll already reads at `MainWindow.xaml.cs:121`. The strip reappears (`:135`) and, because the file is gone by then, resolves to **S1** within ≤ 2 s.

**(a) Extract pressed inside the ≤ 2 s window.** `CompanionCanvasService.ReadAsync` throws at `:66-71`, or an in-flight request throws and `ResetClientAsync` (`:86`) clears the client. `SetFailure` shows the message — but the message at **`:69-70`** is route-specific and **wrong for a Mux user**. Eight strings in that file are fixed in §6.7-B. This file is **not covered by §6.3**, which audits `CspAcquisitionService.cs` only, and §6.6 lists no row for it at all. That is a genuine gap in the copy audit's coverage statement and §6.6-A closes it.

**(b) Mid-extraction drop on the Canvas route.** `CspAcquisitionService.AcquireCoreAsync` (`:112-130`) already falls back to the clipboard route when `AllowClipboardCapture` is on, and emits §6.3 line 1730's `Used the clipboard image; Companion Mode was unavailable.` That string remains correct **verbatim** for the Mux case; no new string, and it stays within the 210-character composed-detail budget verified at §6.3 line 1755.

**(c) No silent reconnect.** `_autoConnectRequested` is now cleared on adopt (§13.3), so after the drop the Companion does not reconnect on its own — on **either** route.

### 6. Client cap reached

`CompanionMultiplexerOptions.MaximumClients` defaults to 8 and `AcceptLoopAsync` enforces it by accepting and immediately disposing the socket (`CompanionMultiplexer.cs:134-138`). The Companion sees a mid-handshake `IOException`, **indistinguishable** from case 3, and it folds into S7. Stated as a known limitation: the proxy sends no rejection frame on that path, so no better message is derivable without a Broker protocol change, and the Broker is out of scope. **Note that §13.2's `AdoptAsync` fix is what stops this state being *reachable by our own leaks*.**

---

## 4.5-R `SettingsView` — RECOMPUTED (replaces §4.5)

`Grid x:Name="SettingsView"`, `Visibility="Collapsed"`, `RowDefinitions="28,12,*"`. **Window unchanged at 460 × 620**; content box unchanged at **428 × 555**.

| Row | Height | Content |
|---|---|---|
| **0** | **28** | `Grid ColumnDefinitions="28,8,*"` → **`BackButton`** (`TitleButtonStyle`, 28 × 28, back glyph, `ToolTip="Back (Esc)"`) · gap · `"Settings"` **T1**, `VerticalAlignment="Center"` |
| **1** | 12 | gap |
| **2** | **`*` = 515** | `ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" Padding="0,0,8,0"` containing a vertical `StackPanel`. **Card width = 428 − 8 = 420.** |

`515 = 555 − 28 − 12` ✔ — unchanged from §4.5.

**Card arithmetic, corrected: every card is `1 (border) + 8 (padding) + content + 8 (padding) + 1 (border) = content + 18`.** v1.0's §4.5 was internally inconsistent — its notice row used `1+8+32+8+1 = 50` (+18) while its permissions and options rows used `2 + 20 + content` (+22). `CardStyle`'s `Padding="10,8"` makes vertical padding 8, so **+18 is correct** and three v1.0 numbers were 4 px too large.

| # | Element | Content | Height | Margin |
|---|---|---|---|---|
| **1** | **`SettingsNotice`** — `Border`, `CardStyle` overridden to `Background="{StaticResource ErrorStatusBrush}"` `BorderBrush="{StaticResource ErrorBrush}"`. Contains **`SettingsNoticeText`**, T4, 2 reserved lines. `Visibility="Collapsed"` by default. **`AutomationProperties.LiveSetting="Assertive"` — the only Assertive live region in the suite.** | 32 | `32 + 18 = ` **50** | `0,0,0,12` |
| **2** | **Permissions card** — three 38 px rows separated by 1 px `DividerBrush` `Rectangle`s with `Margin="0,8"` (17 each) | `38 + 17 + 38 + 17 + 38 = 148` | `148 + 18 = ` **166** *(was 170)* | `0,0,0,12` |
| **3** | **`AutoActionOptionsPanel`** — `Visibility` driven by code (`:657-659`) | `32 + 8 + 32 + 8 + 28 + 8 + 32 = 148` | `148 + 18 = ` **166** *(was 170)* | `0,0,0,12` |
| **4** | **Connection card** — `MuxHandoffToggle` (§13.1) | `18 + 4 + 16 = 38` | `38 + 18 = ` **56** | `0,0,0,12` |
| **5** | **Window card** — `TrayModeToggle` (§11.10) | `18 + 4 + 16 = 38` | `38 + 18 = ` **56** | `0,0,0,12` |
| **6** | **Meta card** — `AboutText` (16) + `xs` 4 + path row (28) | `16 + 4 + 28 = 48` | `48 + 18 = ` **66** *(was 70)* | `0` |

**Stack height, every state, in a 515 viewport:**

```
Base — shipped default (no notice, options collapsed):
  166 + 12 + 56 + 12 + 56 + 12 + 66                          = 380    →  135 px slack  ✔
Notice, options collapsed:
  50 + 12 + 166 + 12 + 56 + 12 + 56 + 12 + 66                = 442    →   73 px slack  ✔
Options open (Auto Action execution on):
  166 + 12 + 166 + 12 + 56 + 12 + 56 + 12 + 66               = 558    →  −43  → scrolls
Options open + notice — the pathological state §4.5 line 1028 named:
  50 + 12 + 166 + 12 + 166 + 12 + 56 + 12 + 56 + 12 + 66     = 620    →  −105 → scrolls
```

**Nothing clips in any state.** Two states scroll; both are opt-in or exceptional, and the `ScrollViewer` mandated by §4.5 line 1028 is the mechanism.

> **Why the window did not grow, in numbers.** The smallest height that fits `options-open` is `558 + 2` (§2.10's rounding slack) `+ 105` (header 28 + gap 12 + title 40 + divider 1 + page pads 24) = **665**. That spends **45 of the 60 DIP** G7 reserved on the 1920 × 1080 @ 150 % display — the constraint that produced 620 in the first place — leaving 15 DIP against an "≈680" figure. It also enlarges the empty base-state viewport §10 already admits to from 263 px to 308. Paying the app's smallest-display margin to remove a scrollbar from an advanced state, while making the common state emptier, is the wrong trade in both directions. **620 stands, in both apps.**

**Everything else in §4.5 is unchanged:** the permission-row geometry, the row names and order (`CompanionPermissionToggle`, `ClipboardPermissionToggle`, `AutoActionPermissionToggle`), `AutoActionPermissionToggle`'s inline `ToolTipService.ShowOnDisabled="True"`, all three toggles wired to **`Click`**, the `AutoActionOptionsPanel` internals, the Meta card internals with `SettingsPathText`'s mandatory `TextWrapping="NoWrap"` + `TextTrimming="CharacterEllipsis"`, and the deletion list.

**§4.1's only amendments** are §13.4's strip internals, `ConnectionInstructions MinHeight="16"`, and two new rows in the tray table:

| State | Tray | Tray interior | Swatch rows |
|---|---|---|---|
| Idle, disconnected | 175 | 157 | 4 |
| **Idle, disconnected, Mux available (S0)** | **163** | **145** | **4** (144 ≤ 145) |
| Idle, connected | 243 | 225 | 6 |
| **Result, connected — the working state** | **195** | **177** | **4** |
| Result, disconnected | 127 | 109 | 3 |
| **Result, disconnected, Mux available (S0)** | **115** | **97** | **2**, scrolls |

## 4.6-A Live regions — now **seven**

| Element | Setting |
|---|---|
| `ConnectionText` | `Polite` |
| **`ConnectionHeading`** | **`Polite` — new (§13.4)** |
| `ConnectionInstructions` | `Polite` |
| `StatusText` | `Polite` |
| `DetailText` | `Polite` |
| `ActionStatusText` | `Polite` |
| `SettingsNoticeText` | `Assertive` |

`Announce(UIElement)` raises `LiveRegionChanged`, which is **inert** without the declared live setting — which is exactly what would have made S0 and S1 silent.

## 5.4-R `SettingsView` — RECOMPUTED (replaces §5.4)

`Grid x:Name="SettingsView"`, `Visibility="Collapsed"`, `RowDefinitions="28,12,*"`. Header identical to the Companion's. Row 2 = `ScrollViewer` (`Padding="0,0,8,0"`, viewport **515**) → `StackPanel`, card width **420**. **Window unchanged at 460 × 620; §5.2 unchanged and still sums to 555.**

| # | Element | Content | Height | Margin |
|---|---|---|---|---|
| **1** | **`SettingsNotice`** + **`SettingsNoticeText`** (T4, `LiveSetting="Assertive"`) — carries `AppPreferences.Save` failures | 32 | **50** | `0,0,0,12` |
| **2** | **Network card**: `"Connection scope"` T2s (18) + `xs` 4 + **`NetworkScopePicker`** `ComboBox` (32) + `xs` 4 + caption T4, 1 line (16) | `18 + 4 + 32 + 4 + 16 = 74` | `74 + 18 = ` **92** *(was 96)* | `0,0,0,12` |
| **3** | **QR-display card**: one 38 px row — title T2s (18) + `xs` 4 + caption T4 (16) on the left, **`AutoHideQrToggle`** 44 × 28 on the right | 38 | `38 + 18 = ` **56** *(was 60)* | `0,0,0,12` |
| **4** | **Window card** — **`TrayModeToggle`** (§11.10), byte-identical to the Companion's | 38 | `38 + 18 = ` **56** | `0,0,0,12` |
| **5** | **Meta card**: `AboutText` T4 (16) + `xs` 4 + path row (28) with **`SettingsPathText`** + **`ShowSettingsFileButton`** | `16 + 4 + 28 = 48` | `48 + 18 = ` **66** *(was 70)* | `0` |

**Stack height:**

```
Base (no notice):     92 + 12 + 56 + 12 + 56 + 12 + 66            = 306   →  209 px slack  ✔
Worst case (notice):  50 + 12 + 92 + 12 + 56 + 12 + 56 + 12 + 66  = 368   →  147 px slack  ✔
```

**Nothing scrolls in any Mux state.** The `ScrollViewer` remains for a notice that wraps past two lines.

Network-card caption width check: content column = `420 − 2 − 20 = 398 px` ≈ **71 characters** at ≈5.6 px/char; §6.5's `Loopback keeps the proxy on this PC. A private network lets phones reach it.` is 74. **This is 3 characters over at the corrected metric** — see Known minor deviations; the caption's card has 209 px of slack and its `Border` is `Auto`, so a two-line wrap grows the card to 72 and the page still does not scroll. It is recorded, not hidden.

**`NetworkScopePicker` behaviour is unchanged** from §5.4: `IsEnabled = (multiplexer is null)` with `ToolTipService.ShowOnDisabled="True"` and `ToolTip="Stop sharing to change the network."`; a saved-but-unavailable address injected as a disabled `$"{address} · unavailable"` item; `NetworkDiscovery.GetChoices()` loaded on a background `Task`. **`AppPreferences.Save` still gains its `try/catch`** — now on two features' critical path.

---

## 4.7-A Companion code-behind changes — additions to §4.7

Edits 1–7 and the "Guards that must NOT be touched" list are unchanged. Four edits are added, plus three amendments to existing ones.

**Edit 8 — `OnClosing` override.** Replace the `Closing` lambda (`:53-60`) with the override in §11.3-A, including the four-condition tray gate. The `Closed` lambda (`:61-71`) is unchanged.

**Edit 9 — tray plumbing.** Add `_exitRequested`, `_hiddenToTray`, `MarkExitRequested()`, `RequestExit()`, `HideToTray()`, `ShowFromTray()`, `ApplyHostMode()` (§11.2-A), `ReassertTopmost()`, `TrayModeToggle_Click`. Call `_tray.Attach(this)` from `MainWindow_Loaded` after `ApplySettingsToUi()` (`:80`). Add the `_hiddenToTray` guard to `RestoreCompanionWindow` (`:493`).

**Edit 10 — Mux route.** Add `_manualConnectRequested`, `_handoff`, `ManualConnectButton_Click`, `MuxHandoffToggle_Click`; the poll classification and route branch of §13.3; **`_autoConnectRequested = false` on every successful adopt, both routes**; the route status line inside the Mux `adopted` branch (§13.4). **Line `:244` is not modified.**

**Edit 11 — `ApplySettingsToUi`.** Two reads at ~`:650` (`TrayModeToggle.IsChecked`, `MuxHandoffToggle.IsChecked`). **Nothing is added at `:718`** — both new settings are written only by their own handlers, each of which opens with the `_loadingSettings` guard copied from `PermissionToggle_Click` (`:706-711`).

**Amendment to edit 4 (`SetConnectionChrome`).** The method additionally writes `NotifyIcon.Text`, **inside the existing ordinal-equality guard at `:275`** so the 2-second poll is still throttled. The signature change and the deleted third parameter are unchanged.

**New — `SetConnectionHeading`.** A guarded setter mirroring `SetConnectionInstructions` (`:284-292`); the three bare `ConnectionHeading.Text` assignments at `:162`, `:172` and `:229` are retargeted to it (§4.6-A).

**Amendment to the guards list.** Add:
- *`ConnectionInstructions.MinHeight="16"` must not be removed — without it the strip jumps 56 → 40 on entering S2.*
- *The strip's row-2 `Grid` must stay `ColumnDefinitions="*,Auto"` with the gap on `ManualConnectButton.Margin`. A fixed gap column does not collapse with the button and silently costs the instruction column 8 px in every state.*
- *`AdoptAsync` must acquire `connectionGate` with `CancellationToken.None`. Reintroducing the caller's token there reinstates the socket leak at `CompanionCanvasService.cs:31`.*

---

## 6.6-A COPY AUDIT — REVISED COVERAGE

§6.6's table gains one file and one row group; the three features add their own. **The draft's count was one short and its explanatory parenthetical was incoherent; both are corrected.**

| File | Rows | Deleted | Rewritten | Kept verbatim | New |
|---|---|---|---|---|---|
| `MainWindow.xaml` (Companion) | 54 | 22 | 22 | 10 | — |
| `MainWindow.xaml.cs` (Companion) | 57 | 16 | 33 | 8 | — |
| `CspAcquisitionService.cs` | 23 | 1 | 21 | 1 | — |
| **`CompanionCanvasService.cs`** — **gap closed, §6.7-B** | **8** | 0 | **8** | 0 | — |
| `App.xaml` (combo template) | 2 | 0 | 2 | 0 | — |
| Mux — all files | 44 | 22 | 15 | 7 | — |
| **System tray — both apps, §6.7-A rows 1–10** | **10** | — | — | — | **10** |
| **Companion auto-connect — §6.7-A rows 11–33** | **23** | — | — | — | **23** |
| **Mux session handoff** | **0** | — | — | — | **0** |
| **Total** | **221 rows** | **61** | **101** | **26** | **33** |

Verification: existing rows `54 + 57 + 23 + 8 + 2 + 44 = 188`; `22+16+1+0+0+22 = 61` deleted; `22+33+21+8+2+15 = 101` rewritten; `10+8+1+0+0+7 = 26` kept; `61 + 101 + 26 = 188` ✔. New `10 + 23 + 0 = 33`. Grand total `188 + 33 = 221`, and `61 + 101 + 26 + 33 = 221` ✔.

**Rows 1–10 are the tray group; rows 11–33 are the auto-connect group.** (The draft's parenthetical claimed row 6 was counted in both groups, which is impossible, and asserted that rows 11–30 number nineteen.)

**The Mux session handoff contributes zero user-visible strings.** That is a result, not an omission.

## 6.7 COPY — NEW AND CORRECTED STRINGS

The test each must pass is the one §6 applies everywhere: *does it instruct where the user cannot see what to do, or does it reassure them about something they can already see?*

### 6.7-A New strings

| # | String | Surface | Chars | Why it is instruction, not reassurance |
|---|---|---|---|---|
| 1 | `Show window` | Tray menu, item 1 | 11 | Names the only action available when the window is invisible. |
| 2 | `Hide window` | Tray menu, item 1 | 11 | Same item, opposite state. Computed at `Opened`, never cached. |
| 3 | `Settings` | Tray menu, item 2 | 8 | Reuses §6.1 line 115's word for the same destination. |
| 4 | `Exit` | Tray menu, item 3 | 4 | The **only** way to quit in tray mode. Not `Exit application`, not `Quit`, not `E&xit`. |
| 5 | `Hide to tray` | Close-button `ToolTip` + `AutomationProperties.Name`, tray mode | 12 | States what the control does, on a control whose glyph now means something different. `Close` survives unchanged in taskbar mode. |
| 6 | `{wordmark} · {connection word}` | `NotifyIcon.Text` | ≤36 | **Zero new vocabulary** — §3.2's wordmark and one of §3.3's six words. Longest value `CSP Palette Companion · Disconnected` = 36 ≤ 63, the `ArgumentException` limit. |
| 7 | `Still running. Exit from the tray icon.` | Companion balloon, once per user | 39 | Reports a non-obvious consequence of an action the user just took. Without it, X-hides-to-tray is indistinguishable from a crash. |
| 8 | `Still sharing. Exit from the tray icon.` | Mux balloon, once per user | 39 | Same, larger consequence: the Mux still holds the one upstream link every connected app shares. |
| 9 | `System tray` | Settings card title **and the toggle's `AutomationProperties.Name`**, both apps | 11 | Names the thing the toggle switches. |
| 10 | `Close hides the window. Exit from the tray icon.` | Settings caption, both apps | 48 | Two facts the user cannot infer from a toggle labelled "System tray". |
| 11 | `Use CSP Mux when it is running` | Settings card title **and automation name**, Companion | 30 | The condition **is** the behaviour. Rejected: `Auto-connect through CSP Mux` — "auto" flirts with the register §9.3's `automatically` lint enforces. |
| 12 | `Connects through CSP Mux instead of scanning CSP's QR.` | Settings caption, Companion | 54 | Names the route it replaces. **`proxy` removed** — §6.5 deletes every user-visible Mux string containing it, leaving one survivor in the other app; and the card must not give one thing two names four lines apart. |
| 13 | `CSP Mux is sharing` | `ConnectionHeading`, S0 | 18 | One word apart from #14. The instruction line is deliberately **empty** here. |
| 14 | `CSP Mux is not sharing` | `ConnectionHeading`, S1 | 22 | Not "is not running": the Companion **cannot** distinguish a closed Mux from an idle one. Borrows §6.5 line 1779's `Not sharing`. |
| 15 | `Start sharing in CSP Mux, or connect to CSP.` | `ConnectionInstructions`, S1 | 44 | Both routes named, both actionable. |
| 16 | `Connecting through CSP Mux` | `ConnectionHeading`, S2 | 26 | The only surface that says which route is in flight. |
| 17 | `Scan CSP's QR instead` | `ManualConnectButton`, S0 only | 21 | The user's decision made visible. `CSP's` is load-bearing — this suite has two QR codes. |
| 18 | `Connect to CSP by scanning its QR code` | `ManualConnectButton` automation name | 37 | Fuller-name-than-label pattern at `:147`/`:154`. |
| 19 | `Cannot use CSP Mux` | `ConnectionHeading`, S4 | 18 | Rejected: `CSP Mux handoff refused`. **`handoff` is added to §9.3's copy lint.** |
| 20 | `CSP Mux is newer than this app. Update Companion.` | S4, `VersionTooNew` | 49 | Names the fix, and the fix is not on the Mux side. |
| 21 | `Cannot read CSP Mux. Scan CSP's QR instead.` | S4, `Malformed` | 43 | **Rewritten from the draft.** `The CSP Mux session file is unreadable.` named an internal artefact with no user model and no action — narration. **`session file` joins `handoff` in §9.3's lint.** |
| 22 | `CSP Mux is sharing on a network. Scan its QR.` | S4, `NotLoopback` | 45 | Correct in both readings — legitimate LAN mode, or a planted file. |
| 23 | `Could not verify CSP Mux. Scan its QR instead.` | S4, `Unverifiable` | 47 | The elevated-Mux case, confirmed by measurement (`ERROR_ACCESS_DENIED`). |
| 24 | `CSP Mux is not answering` | `ConnectionHeading`, S7 | 24 | Distinguishes "verified but unreachable" from S4's "would not verify". |
| 25 | `Restart sharing in CSP Mux, or scan CSP's QR.` | S7, socket refused | 45 | Restart, not rescan. |
| 26 | `CSP Mux did not answer. Connect to CSP instead.` | S7, 3 s timeout | 47 | A filtered port is not a refused one; do not tell the user to restart something that is running. |
| 27 | `CSP Mux refused the connection. Scan its QR.` | S7, auth rejected | 44 | Rescan, not restart — the credential rotated. |
| 28 | `Connect through CSP Mux` | `ConnectButton.ToolTip` **and automation name**, S0 | 23 | The button says `Connect`; this says through what. |
| 29 | `Stop connecting` | `ConnectButton.ToolTip`, S2 | 15 | §6.2 line 1650's `Stop scanning` is **factually wrong** in S2. |
| 30 | `Ready · through CSP Mux` | `StatusText`, once, Mux route only | 23 | **Rewritten from the draft's `Connected through CSP Mux`.** That string was permanent, not transient — `ClearPaletteResult` (`:552-560`) provably never touches `StatusText` — and it sat beside a title bar reading `Connected`, i.e. §6 rule 1. This shares no word with `Connected`, extends the app's existing idle `
StatusText` (`Ready`, §6.1 line 344), and its reset is the next extraction's `SetStatus` — a real site. **The QR route writes nothing.** |
| 31 | `Scan a Connect to smartphone QR code` | `ConnectButton.ToolTip`, S1 / S4 / S7 | 36 | **New, route-neutral.** The draft used §6.2 line 1652's CSP-specific string here while three S4 instructions and one S7 instruction point at the Mux's code — an instruction and a tooltip naming different QRs on one button. `CompanionQrScanner`'s predicate (`CompanionCanvasService.cs:8-9`) accepts any valid pairing URL and decodes whichever it finds first, so the scanner genuinely cannot be steered; the neutral tooltip is the accurate one. |
| 32 | `Stop connecting through CSP Mux` | `ConnectButton` automation name, S2 | 31 | **New.** The existing code writes the automation name at `:147`; without this, S2 announces `Stop connecting to CSP Companion Mode` while stopping a Mux connect. |
| 33 | `CSP Mux is not sharing on that port. Scan its QR.` | S4, `PortNotOwned` | 51 | **New status (§12.3 check 14).** The Mux is alive and correct but does not own the port the file names. Distinct from `Stale` (process gone) and from S7 (verified, then unreachable). |

All `ConnectionInstructions` strings are ≤ 57, the budget the corrected 322 px column supports.

**Cut during this pass, and why — so the cuts are auditable:**

| Rejected string | Cut because |
|---|---|
| `Connect through the proxy. No QR needed.` (S0 instruction) | Explains a button whose tooltip already says it, beside a link that already says the alternative. §6 rule 2. **S0's instruction is empty.** |
| `Connected to CSP` (`StatusText`, QR route) | Duplicates `ConnectionText = Connected`. §6 rule 1. |
| `Connected through CSP Mux` (`StatusText`, Mux route) | **Cut in this revision.** Not transient as claimed, and it repeats `Connected` on a screen where the title bar already says it. Replaced by #30. |
| `The CSP Mux session file is unreadable.` | **Cut in this revision.** Names an internal artefact, states no action. Replaced by #21. |
| `Hide to tray · Exit from the tray menu` (Close tooltip) | The second clause duplicates the settings caption and the balloon. §6.1 line 533's precedent. |
| `Always on top` (tray menu item) | The pin is a window control; if the window is on screen `PinButton` is one click away. Keeps both menus structurally identical (G7). |
| `Stop sharing` (Mux tray menu item) | Destructive, one right-click deep, enabled in only 2 of 6 `ConnectionState` values — four states would show a greyed item that reads as broken. |
| `Handoff published` / any Mux-side indicator | Narrating an internal mechanism. §6.5 line 1785's precedent. |
| Off-state caption for `TrayModeToggle` | Narrating an absence — §6.2 line 1706's precedent. |
| Tooltip on either new toggle | Duplicates the caption directly beneath it. §6.1 lines 533, 556, 1617. |
| `Changes take effect now` on either new row | G9, and §6.1 line 667 is the named offender this repeats. |

### 6.7-B `CompanionCanvasService.cs` — the coverage gap §6.6 missed

These reach `DetailText` through `SetFailure`. Five of the eight are **route-specific and become wrong the moment a Mux user is connected**; the instruction is identical for both routes and the connection strip already names the route, so a route-agnostic string is not a loss of information — it is the removal of a lie.

| Line | Before | After |
|---|---|---|
| 69-70 | `Companion Mode is disconnected. Select Connect and leave CSP's QR code visible.` | **`Connection lost. Connect again.`** (31) |
| 106-107 | `Connect to CSP Companion Mode before choosing a swatch.` | **`Not connected. Connect, then choose the swatch again.`** (52) — aligns with §6.2 line 1686 |
| 143-144 | `Connect to CSP Companion Mode before using Selection · Canvas.` | **`Not connected. Connect, then try Selection · Canvas.`** (51) |
| 152-153 | `Add a "Copy Merged Selection" Auto Action to CSP Quick Access, then try again. Selection · Layer works without this one-time setup.` | **`Add the setup guide's action to CSP Quick Access. Selection · Layer needs no setup.`** (83) — the second clause **survives**: it names a working alternative the user cannot otherwise discover. Fits the 210-char / 3-line budget |
| 175-176 | `Connect to CSP Companion Mode before loading Quick Access commands.` | **`Not connected. Connect first, then refresh.`** (42) — aligns with §6.2 line 1707 |
| 202-203 | `Connect to CSP Companion Mode before checking CSP actions.` | **`Not connected. Connect first, then refresh.`** (42) |
| 326-328 | `The selected CSP Quick Access command is no longer available. Choose an enabled command again in Settings.` | **`That CSP action is gone. Choose another in Settings.`** (51) |
| 331-333 | `The selected CSP Quick Access command is currently disabled. Enable it in CSP or choose another command in Settings.` | **`That CSP action is disabled. Enable it in CSP, or choose another.`** (64) |

All eight are ≤ 83 characters, well inside §2.10's 210-character three-line reservation, and none contains a §9.3 lint word.

### 6.7-C One §6.2 string is re-cut — it would have wrapped

**§6.2 line 1665** is annotated in v1.0 as "57 chars — at the column limit; verified". It is **60**:

```
In CSP, open Connect to smartphone and leave the QR visible.
                                                          ^ 60
```

At §4.1's own 5.65 px/char that is 339 px against a 322 px column — it wraps to two lines, making S5's strip `1+8+18+4+32+8+1 = 72` and costing the tray 16 px. That defect exists in v1.0 independently of this extension, and it makes §9.1-A's "the strip measures 56 in every state except S0" unsatisfiable. It is fixed here because this extension is what asserts the measurement:

| Line | v1.0 | v1.1 |
|---|---|---|
| §6.2:1665 | `In CSP, open Connect to smartphone and leave the QR visible.` (60) | **`In CSP, open Connect to smartphone. Leave the QR visible.`** (57) |

Two sentences also read better than the run-on, and 57 is the documented ceiling, not an overrun.

---

## 8.1 REVISED IMPLEMENTATION ORDER

**Fourteen phases.** Each ends in a buildable, runnable state. Do not begin a phase until the previous one runs. **modified** marks a phase whose steps changed, with the change stated.

### Phase 1 — Shared theme file — **MODIFIED (§0.1)**
1. ~~Create the `csp-suite-theme` repository~~ — **deleted.** Create `src/CspPaletteCompanion.App/Theme/` and `src/CspMultiplexer.App/Theme/`.
2. Write `Theme.xaml` in the Companion: §1.2 brushes → §1.3 type styles → §1.4/§1.5/§1.6 tokens → §2 control styles and templates in the order §2.1 … §2.18, **plus the three implicit menu styles of §11.4**.
3. Add `tools/suite-sync.ps1` and `tools/suite-sync.manifest` to **both** repos (§0.1.3), then `pwsh tools/suite-sync.ps1 -Mode Push` from the Companion to create the Mux's copy.
4. Add the `SuiteSyncCheck` target (§0.1.4) to both app csprojs.
5. **Gate:** both `Theme.xaml` files hash-identical (`suite-sync` exits 0 from both repos); the Companion builds (BAML validates every `{StaticResource}` key); **`git clone` of either repo alone builds with no external path and `suite-sync` prints `nothing to reconcile`**.

### Phase 2 — Companion theme swap, no layout change — **MODIFIED**
6. Replace `App.xaml` with §5.9's file, `Source="pack://application:,,,/Theme/Theme.xaml"`. **Add no `<Page Include>` item** (NETSDK1022 — the SDK already globs it).
7. `FieldLabelStyle` consumers; retarget the removed keys, as §4.5 steps 6–7.
8. **Gate:** as §4.5's Phase-2 gate, unchanged.

### Phase 3 — Companion code-behind — unchanged
9. §4.7 edits 1–5. 10. **Gate:** unchanged.

### Phase 4 — Companion relayout — **MODIFIED**
11. Rewrite `MainWindow.xaml` per §4.1, §4.2–§4.4, and **§4.5-R**. Keep every `x:Name` and every event-handler attribute. Add `x:Name="CloseButton"`.
12. Build the connection strip with **§13.4's internals** — the row-2 grid is **`*,Auto`**, `ConnectionInstructions MinHeight="16"`, `ManualConnectButton` present with `Margin="8,0,0,0"` but `Collapsed` and inert.
13. Build the **Connection card and the Window card** (§13.1, §11.10) with their toggles present, inert, and carrying their `AutomationProperties.Name`. **Their handlers arrive in Phases 12 and 14; the layout is measured once, here.**
14. Change the window dimensions — **all six properties** (§3.1) — and set **`ShowInTaskbar="False"`** (§11.2-A). Verify `MinHeight`/`MaxHeight` are 620, not the legacy 700.
15. Add `WindowChrome` per §3.1; delete the outer 1 px `Border`.
16. **Gate:** every sum in §4.1 and **§4.5-R** reproduces on screen at 100 %, 125 % and 150 %. All six tray states. **Force the strip to 68 by making the link `Visible` by hand and confirm the tray shrinks to 163 and still fits four swatch rows. With the link `Collapsed`, measure the instruction column at 322 px, not 314.**

### Phase 5 — Companion shell finishing — unchanged
17. `app.manifest`; delete `<ApplicationHighDpiMode>`. 18. `NativeMethods.ApplyRoundedCorners` + the `SourceInitialized` call. 19. Window position persistence (§3.5). 20. **Gate:** unchanged.

### Phase 6 — Companion copy — **MODIFIED**
21. Apply §6.1, §6.2, §6.3, §6.4, **§6.7-B and §6.7-C** in file order.
22. **Gate:** §9.3's grep, now including `handoff` and `session file`. **Measure every `ConnectionInstructions` string against 322 px; none may exceed 57 characters.**

### Phase 7 — Mux project skeleton — **MODIFIED**
23. New `App.xaml` + `App.xaml.cs` (`ApplicationDefinition`), `ShutdownMode="OnMainWindowClose"`, `Source="pack://application:,,,/Theme/Theme.xaml"`.
24. Delete `Program.cs`, `ThemeControls.cs`, `MainForm.cs`, `SettingsForm.cs`, `CompanionQrScanner.cs`.
25. `pwsh tools/suite-sync.ps1 -Mode Pull` to bring in `Theme/Theme.xaml` and `CompanionQrScanner.cs`; wrap the scanner's namespace line in a `SYNC-LOCAL` region; add `using System.Drawing;` and `using System.Windows.Forms;`.
26. **New `src/CspMultiplexer.App/NativeMethods.cs`** with §3.1's `ApplyRoundedCorners` — the Mux has no such file today.
27. New `app.manifest`; rewrite the csproj per §5.8 **minus the theme `ProjectReference`, plus `<Resource Include="Assets\csp-mux.ico" />` and the `SuiteSyncCheck` target**.
28. Empty `MainWindow.xaml` with the §3.2 shell (including `x:Name="CloseButton"` and `ShowInTaskbar="False"`) and a placeholder body.
29. **Gate:** as §8's Phase-7 gate, plus `suite-sync` exits 0 from both repos.

### Phase 8 — Mux state machine and plumbing — unchanged
30–35 as §8's steps 27–32, including §5.7's `OnClosing` **without** the tray branch (Phase 12). **Gate:** the six exits — verify the idle-close path specifically.

### Phase 9 — Mux views and QR — **MODIFIED**
36. `MainWindow.xaml` main view per §5.2 (**unchanged**); `SettingsView` per **§5.4-R**, including the Window card, inert.
37–38. `ProxyQrRenderer` and `NetworkScopePicker`, unchanged.
39. **Gate:** §5.2's sum (555) and **§5.4-R's sums (306 / 368)** reproduce on screen; the phone scan at three scale factors.

### Phase 10 — Mux copy — unchanged
40–41. §6.5 including the exception map; the §9.3 grep and the composite read-through.

### Phase 11 — Icons — **MODIFIED**
42. Author the 256 master (§7.1–§7.3) as SVG; hand-draw 16 and 20 (§7.4).
43. Build both `.ico` files with all eight sizes; `<ApplicationIcon>` **and `<Resource Include>`** in both csprojs.
44. **Gate:** both icons legible and distinguishable at 16 px in the taskbar, Alt-Tab and Explorer, **and `Application.GetResourceStream(new Uri("pack://application:,,,/Assets/…ico"))` returns non-null in both apps** — the check that catches a missing `<Resource>` item before Phase 12 depends on it.

### Phase 12 — System tray, both apps — **NEW (§11)**
45. `TrayHost.cs` in the Companion: mutex + activation receiver + `NotifyIcon` + WPF `ContextMenu` + icon-size tracking + `ReattachActivationHook` + `SetIconVisible`. `-Mode Push` to create the Mux's copy; fill in its four `SYNC-LOCAL` lines.
46. `App.xaml.cs` in both: `OnStartup`, `OnExit`, `SessionEnding` (**which must call `MarkExitRequested()` first**, §11.3-A), `DispatcherUnhandledException`, `ProcessExit`.
47. Companion `OnClosing` override with the **four-condition tray gate**; the same gate inserted into the Mux's §5.7 `OnClosing` after `e.Cancel = true`, before the `closeInProgress` latch; `tray.Dispose()` before the `Dispatcher.Yield`.
48. `HideToTray` / `ShowFromTray` / **`ApplyHostMode` per §11.2-A** / `ReassertTopmost` / `RequestExit` / `MarkExitRequested` in both; the `_hiddenToTray` guard on `RestoreCompanionWindow`.
49. `RunInTray` + `TrayHintShown` in `AppSettings` and `AppPreferences`; wire `TrayModeToggle_Click` in both.
50. `SetConnectionChrome` / `ApplyState` also write `NotifyIcon.Text`.
51. **Gate:** §9.4 in full.

### Phase 13 — Mux session handoff — **NEW (§12)**
52. `MuxHandoffContract.cs` in the Mux (with `required` members); `-Mode Push` to create the Companion's copy.
53. `NativeMethods.TrySetMediumNoReadUpLabel` in the Mux (§12.4).
54. `MuxSessionHandoff.cs`: `TryPublish` (**protected DACL on the temp file, label, atomic move, `try/finally` cleanup, orphan sweep, never throws, never logs**) and `TryDeleteOwn` (**`FileShare.None` verify read; deletes when mine or when the recorded process is verifiably dead**).
55. Publish in `StartAsync` at the exact site in §12.2; delete as the **first** statement of `StopAsync`; `TryDeleteOwn` in `SessionEnding` and `ProcessExit`.
56. **Gate:** §9.5's Mux half. Confirm `git diff --stat src/CspMultiplexer.Broker src/CspMultiplexer.Protocol` is **empty**. Confirm the Broker's integration tests write nothing to `%LOCALAPPDATA%\CSP Suite`.

### Phase 14 — Companion auto-connect — **NEW (§13)**
57. `NativeMethods.OwnsListener` (`GetExtendedTcpTable`, AF_INET and AF_INET6) in the Companion.
58. `MuxHandoffReader.cs` implementing §12.3's **fourteen** ordered checks and its four-case caching rule.
59. `CompanionCanvasService`: extract `AdoptAsync` **with the `CancellationToken.None` gate and the `when (!swapped)` filter**; add `ConnectThroughMuxAsync` **with its sink-side loopback guard** and `Route`; the two `CspAcquisitionService` delegations.
60. `AppSettings.UseMuxWhenAvailable`; wire `MuxHandoffToggle_Click` and the single `ApplySettingsToUi` read. **Nothing at `:718`.**
61. `RefreshConnection` classification, `RunConnectionLoopAsync`'s route branch, **`_autoConnectRequested = false` on adopt for both routes**, `ManualConnectButton_Click`, the S0 link's visibility, the route status line **inside the Mux `adopted` branch**, `SetConnectionHeading` retargeting.
62. **Gate:** §9.5's Companion half.

---

# 9. DEFINITION OF DONE — ADDITIONS

## 9.1-A Companion — amended and added

**Amended.** Replace §9.1's settings-page line with:

- [ ] Settings page, all four states measured against **§4.5-R**: base **380**, notice **442**, options open **558**, options + notice **620**, in a **515** viewport. Base and notice show **no scrollbar**; options-open and options-plus-notice **do** scroll, and nothing clips in any of the four.
- [ ] Every card measures `content + 18`, not `content + 20`. Verified by measuring the Permissions card at **166**, not 170.

**Added.**

- [ ] Window is still exactly **460 × 620** and cannot be resized. All six size properties.
- [ ] The strip's row-2 grid is `ColumnDefinitions="*,Auto"` and the gap is `ManualConnectButton.Margin="8,0,0,0"`. **Verified by measuring `ConnectionInstructions.ActualWidth` at 322 px with the link collapsed** — a `*,8,Auto` grid yields 314 and is the defect this check exists for.
- [ ] The connection strip measures **56** in every state except S0, and **68** in S0. Measured, not eyeballed. **Includes S5, whose instruction is §6.7-C's 57-character re-cut, not v1.0's 60.**
- [ ] `ConnectionInstructions` carries `MinHeight="16"`. Verified by entering S2 and confirming the strip stays 56 rather than collapsing to 40.
- [ ] With `UseMuxWhenAvailable` **off**, the strip and every §4.1 sum are byte-for-byte the Phase-6 behaviour, and a file-system trace over 60 s of polling shows **zero** accesses to `%LOCALAPPDATA%\CSP Suite`.
- [ ] **Every live region carries `AutomationProperties.LiveSetting` — seven, not six**, `ConnectionHeading` included. Verified by entering S0 and S2 with a screen reader running and confirming each raises `LiveRegionChanged` carrying the heading text.
- [ ] **Every toggle in `SettingsView` reports a non-empty `AutomationProperties.Name`**, including `TrayModeToggle` and `MuxHandoffToggle`.
- [ ] `x:Name="CloseButton"` exists and its `ToolTip` and `AutomationProperties.Name` both change with the mode.
- [ ] `RestoreCompanionWindow` returns early when `_hiddenToTray`. Verified by hiding to tray during an active QR scan and confirming the window does **not** reappear when the scan ends.
- [ ] `ReassertTopmost` reads `_pinned`, never `Topmost`. Verified by hiding during a scan (when the loop has driven `Topmost` false) and confirming `PinButton.IsChecked` and actual topmost agree after restore.

## 9.2-A Mux — amended and added

**Amended.** Replace §9.2's settings-layout line with:

- [ ] Settings page measured against **§5.4-R**: base **306**, worst case **368**, in a **515** viewport. **Nothing scrolls in any state.** Every card measures `content + 18`. The network caption is allowed to wrap to two lines (Known minor deviations); if it does, the card measures 92 → **110** and the page still does not scroll.

**Added.**

- [ ] `git diff --stat` over `src/CspMultiplexer.Broker` and `src/CspMultiplexer.Protocol` is **empty**.
- [ ] The Broker's integration test suite writes nothing to `%LOCALAPPDATA%\CSP Suite`. Verified by running the suite with the directory deleted and confirming it is not recreated.
- [ ] `StopAsync`'s **first** statement is `MuxSessionHandoff.TryDeleteOwn()`, ahead of `operationCancellation?.Cancel()` and ahead of the `ClientCountChanged` unsubscribe.
- [ ] The tray branch in `OnClosing` sits **after `e.Cancel = true` and before the `closeInProgress` latch**, and is gated on **all four** of `RunInTray`, `!_exitRequested`, `IsVisible`, `!closeInProgress`. Verified by hiding to tray ten times and then exiting: the teardown still runs.
- [ ] **Log off while sharing.** `StopAsync` ran: broker sockets closed, the handoff file gone, the upstream CSP link disposed. It is not enough for the tray icon to have disappeared — this is the path where the tray branch would otherwise skip teardown entirely.
- [ ] `tray.Dispose()` runs **before** the `Dispatcher.Yield` hop, not after `Close()`.
- [ ] **Tray Exit from an idle Mux that never connected** — the no-await `StopAsync` path that re-enters `Close()`. No `InvalidOperationException`.

## 9.3-A Suite-wide — amended and added

**Amended.** Replace §9.3's first line with:

- [ ] `pwsh tools/suite-sync.ps1` exits **0** from **both** repos. Verified by editing one hex in one `Theme.xaml`, confirming the script exits 1 and names the file and the newer side, then `-Mode Pull` and re-running to 0.
- [ ] **Each repo clones and builds standalone.** `git clone` one repo into an empty directory with the sibling absent, then `dotnet build` — no external path, no submodule flag, and `suite-sync` prints `nothing to reconcile` and exits 0. **Repeat in Release with `pwsh` removed from `PATH`** — the `SuiteSyncCheck` target is Debug-only and `Exists()`-guarded, so it must not run at all.
- [ ] A deliberate drift produces an MSBuild **warning**, not an error, under repo-wide `TreatWarningsAsErrors`.

**Amended.** Extend the copy-lint grep with `handoff` and `session file`:

```
grep -rniE 'securely|directly|automatically|seamlessly|simply|everything runs|no artwork|
            saved immediately|locally|read-only|exactly what|riskier|ready when|
            your active|real Companion|will appear here|remain online|continue normally|
            something went wrong|handoff|session file' src
```

`handoff` and `session file` must have **zero** hits in any user-visible string. Hits in comments, type names (`MuxHandoffReader`, `MuxSessionHandoff`, `MuxHandoffContract`, `PaletteHandoffService`, `DragHandoffButtonStyle`) and file names are expected and permitted.

**Added.**

- [ ] Both apps still **460 wide, 620 tall**, 40 px title bar, identical column structure.
- [ ] **Zero new tokens**: `git diff` on `Theme.xaml` adds only the three implicit menu styles, and every value in them resolves to an existing key.
- [ ] `mux-session.json` is the **only** file in the suite written without a BOM, and §12.1 says so. **`suite-sync.ps1` writes with a BOM** (`UTF8Encoding($true)`) — verified by pushing a file and re-reading its first three bytes.
- [ ] Both apps' `SettingsView` end with the **Window card immediately above the Meta card**, with byte-identical XAML for that card including its `AutomationProperties.Name`.
- [ ] `Application.Shutdown()` appears exactly **once** in the suite — `App.OnStartup`'s second-instance branch — carries the comment naming it as the only sanctioned call, **and calls `MarkExitRequested()` before it**.
- [ ] **The tray balloon is the only OS-drawn surface in the suite.** No native dialog, no `MessageBox`, no toast, no other balloon anywhere. `grep -rn 'ShowBalloonTip\|MessageBox' src` in both repos returns exactly one `ShowBalloonTip` per app and zero `MessageBox`.

## 9.4 System tray — both apps

**Disposal — the ghost-icon guarantee**

- [ ] Icon is gone from the notification area after **each** of: tray-menu Exit; taskbar-mode X; `Application.Shutdown` via the second-instance path; Windows log-off; an unhandled UI-thread exception. **Five paths, five checks.**
- [ ] After an unhandled exception the crash still surfaces — `e.Handled` is `false` and the process still faults.
- [ ] `TrayHost.Dispose()` called twice does not throw and does not re-issue `NIM_DELETE`.
- [ ] Task Manager *End Task* leaves a stale icon that clears on mouse-over, and the **next launch is a normal first instance**. No self-heal code exists.

**Icon fidelity**

- [ ] The exact `.ico` frame is used at **100 %, 125 %, 150 % and 200 %** — 16, 20, 24, 32 — with no resampling. Verified by zooming a screenshot and confirming the hand-drawn 16 and 20 constructions, not a downscaled master.
- [ ] Changing display scale at runtime swaps the frame; the **new `Icon` is assigned before the old one is disposed**; GDI object count is flat across 50 scale changes.
- [ ] Both apps' icons sit adjacent in the notification area and are distinguishable by hue at 16 px.

**Mode switching — rewritten**

The v1.1-draft check *"`ShowInTaskbar` is never assigned while the window is visible … an HWND recreation loses `DWMWA_WINDOW_CORNER_PREFERENCE` silently, so square corners are the tell"* is **deleted**. It was unsatisfiable (G9 requires an immediate effect from a toggle on a visible page) and it tested for a failure mode that **does not occur**: measured on Windows 11 26200 / .NET 8, four `ShowInTaskbar` transitions on a visible owner-less `WindowStyle="None"` window left the HWND, the `HwndSource`, its hooks and the DWM corner preference all unchanged. Replaced by:

- [ ] Every row of §11.2's state table driven and observed, including the unreachable-through-UI cells reached by editing `settings.json` and relaunching.
- [ ] **`ShowInTaskbar="False"` is the XAML launch value in both apps**, so a cold start with shipped defaults (`RunInTray = true`) performs **no** assignment. Verified by logging `ApplyHostMode`'s branch on first launch and confirming it does not enter.
- [ ] **Toggle the setting 20 times with the window on screen**, logging `new WindowInteropHelper(this).Handle` each time. The handle is **unchanged**, the corners stay rounded, and a second launch still activates the first instance — proving the activation hook survived.
- [ ] A hidden window is still restorable after the setting is turned off programmatically (`ApplyHostMode`'s forced `ShowFromTray`).
- [ ] Minimize and hide remain distinct in both modes; the taskbar button exists whenever the window does.
- [ ] `NotifyIcon.Visible` tracks `RunInTray` only — the icon does **not** flicker as the window is shown and hidden.

**Menu**

- [ ] The menu is a WPF `ContextMenu` in suite chrome, not a `ContextMenuStrip`. Screenshot it beside the window and confirm `#24262B` on `#1C1D21`.
- [ ] Item 1's header is recomputed at every `Opened` — minimize the window between two openings and confirm it reads `Show window`.
- [ ] Arrow-keying the open menu highlights items (`IsHighlighted`, not `IsMouseOver`); a keyboard-focused item draws a 2 px `FocusBrush` ring; a disabled item uses `DisabledTextBrush` with **no `Opacity`**.
- [ ] Right-click opens the menu on **`MouseUp`**; the menu dismisses on an outside click; left single-click does nothing; left double-click always shows, never toggles.
- [ ] No `Always on top`, no Mux `Stop sharing`, no Companion `Extract palette`.

**Restore correctness**

- [ ] With the pin on, restoring from the tray puts the window **above CSP's floating palettes**. Reproduce by pinning, hiding, bringing a CSP palette forward, then double-clicking the tray icon.
- [ ] Hiding during an active QR scan does **not** un-pin the window and does **not** self-restore when the scan ends.
- [ ] Restoring never plays an unminimize animation from a taskbar rectangle.

**Single instance**

- [ ] A second launch **activates the first** and exits; only one tray icon exists. Repeat with the first instance hidden.
- [ ] A second launch fired within the first instance's `MainWindow_Loaded` still shows the window (`_pendingShow` flush).
- [ ] Companion and Mux run together without either activating the other (distinct mutex names, distinct window messages).
- [ ] The `Mutex` is a field of the surviving `TrayHost`. Verified by forcing `GC.Collect()` in the first instance and confirming a third launch is still recognised as a duplicate.

**Balloon**

- [ ] Fires **once**, on the first hide, and never again — including across restarts (`TrayHintShown` is persisted, not per-session).
- [ ] An existing pre-feature `settings.json` upgrades to tray mode with the balloon armed.

## 9.5 Session handoff and auto-connect

**File lifecycle (Mux)**

- [ ] The file appears at `%LOCALAPPDATA%\CSP Suite\mux-session.json` the instant the proxy goes Online, and **only** when bound to loopback.
- [ ] Switching to a LAN scope and starting **deletes the file this instance published, and also deletes a file whose recorded process is verifiably dead**. A file published by a *different, live* instance is left alone for the Companion to refuse. *(The v1.1 draft asserted "deletes any previous loopback file", which `TryDeleteOwn` as specified could not do; the second sanctioned delete case in §12.2 is what makes this line true.)*
- [ ] The file disappears on Stop, on window close **with Exit**, and on log-off. **In tray mode a window close does not delete it** — the proxy is still sharing.
- [ ] `taskkill /f` leaves the file; the Companion refuses it and **no socket is opened**. Verified by watching the port with `netstat` and confirming no connection attempt.
- [ ] The write is atomic: a reader looping `File.ReadAllText` across 200 start/stop cycles never sees a truncated document and never throws a sharing violation.
- [ ] **After 50 forced publish failures** (make the directory read-only mid-run), `dir /a "%LOCALAPPDATA%\CSP Suite"` contains **no** `.mux-session.json.*.tmp` files. Repeat with a hard kill between `CreateNew` and `Move` and confirm the next clean start or stop reaps the orphan.
- [ ] Two instances (console + RDP): the loser's shutdown does **not** delete the winner's file.

**File security (Mux)**

- [ ] `icacls "%LOCALAPPDATA%\CSP Suite\mux-session.json"` shows a **protected** DACL containing exactly the current user and `SYSTEM`, **and no `S-1-15-*` or `S-1-15-3-*` ACE**, and no non-owner group. *(Checking that the inherited ACL is sufficient is not a valid test — on the target machine it is not: `%LOCALAPPDATA%` carries an inherited Full-Control ACE for capability SID `S-1-15-3-3557520199-…`, and the sibling `CSP Palette Companion` folder carries `CodexSandboxUsers:(I)(OI)(CI)(RX)`.)*
- [ ] The same `icacls` shows `Mandatory Label\Medium Mandatory Level:(NW,NR,NX)`. If the label call failed, the DACL check above must still pass — the label is best-effort, the DACL is not.
- [ ] The DACL is applied to the **temp** file and survives `File.Move`. Verified by `icacls` on the temp file mid-write and on the destination after.
- [ ] **No ACL code targets the Companion's `settings.json`.** The asymmetry is deliberate (§12.4).

**Verification (Companion)**

- [ ] Every `MuxHandoffStatus` reachable and mapped to its named string: `Live`, `Absent`, `Malformed`, `VersionTooNew`, `Stale`, `Unverifiable`, `NotLoopback`, **`PortNotOwned`**.
- [ ] **A file containing the JSON literal `null`, a file with `pairingUrl` omitted, and a file with `"pairingUrl": null` each yield `Malformed`** — injected while the 2-second poll is running, with **no crash**. All three were verified to escape the draft's reader: the first as `NullReferenceException`, the other two as `ArgumentNullException` out of `CompanionPairingCodec.cs:58`.
- [ ] **`PortNotOwned` reproduced without killing the Mux**: dispose the multiplexer while the app runs (or stop the listener by any route that bypasses `StopAsync`), leaving the file and the process intact. All of checks 8–10 pass; check 14 refuses; **no socket is opened**.
- [ ] `Unverifiable` **fails closed.** Reproduce by launching the Mux elevated while the Companion runs `asInvoker`; confirm the refusal and the QR fallback.
- [ ] A hand-written file naming a **LAN** address is refused with `NotLoopback` even though the codec's `IsPrivateOrLocal` would accept it.
- [ ] A file with `schemaVersion: 2` produces `VersionTooNew`, not a misparse.
- [ ] A 5 MB file is refused on the size cap **without being deserialised**, and the cap is read from the **opened stream**, not from `FileInfo`. Verified by replacing the file between a stat and an open and confirming the large document is still refused.
- [ ] The Companion never creates, modifies or deletes anything under `%LOCALAPPDATA%\CSP Suite`. Verified with a file-system audit over a full session including every failure mode.
- [ ] The 2-second poll re-parses only when mtime, length or existence changed. When `Live` and unchanged, per-tick work is checks 8–11 and 14 only — **measure it; the reference figures are 43 µs and 109 µs, so a tick over ~1 ms means the caching rule is not implemented.**
- [ ] Every `Process` obtained by `Process.GetProcessById` is disposed (`using`).

**Security**

- [ ] `grep -rn 'PairingUrl\|pairingUrl\|InvitationPassword' src/` in **both repos** — Companion app, Mux app **and Broker** — shows no path from the URL or the password to `SetStatus`, `SetFailure`, `DetailText`, `StatusText`, any `ToolTip`, or any shown exception message. Checked by reading every call, not by grepping for the word alone. *(The draft's version grepped only `src/CspPaletteCompanion.App`, which is the one place the URL is never minted.)*
- [ ] **No logging facility exists.** `grep -rn 'Trace\.\|Debug\.WriteLine\|ILogger\|EventLog\|Console\.Error' --include=*.cs src/` in both repos returns zero hits. If a sink is ever added, no log line may contain `MuxSessionDocument`, `PairingUrl`, `CompanionPairingInfo.Password`, or any string derived from them — only the `MuxHandoffStatus` enum name and the exception type.
- [ ] The Mux's Start-path failure handler never receives a `TryPublish` exception — `TryPublish` cannot throw. Verified by making `%LOCALAPPDATA%` read-only and confirming the proxy still starts and the Companion falls back to the QR path.
- [ ] The password in the file is **not** CSP's. Verified by decoding both the Mux's handoff URL and CSP's own QR in one session and confirming the two password fields differ.
- [ ] Killing the Mux and restarting it invalidates the previous credential: a Companion holding the old pairing gets `UnauthorizedAccessException` → S7.
- [ ] **With `HideQrAfterFirstConnection` on, the handoff file is still present for the whole session** — confirmed, and accepted (§12.4's two-axis statement).
- [ ] `ConnectThroughMuxAsync` called directly with a `192.168.x` pairing **throws before any socket is opened** — verified by unit test, not by inspection of its one caller.

**Connect flow (Companion)**

- [ ] All eight states — S0…S7 — driven and screenshotted, each with the exact heading, instruction, dot colour, border brush, button content, **button tooltip, button automation name** and link visibility of §13.4's tables.
- [ ] `ManualConnectButton` is visible in **S0 and nowhere else**.
- [ ] The Mux route **never retries** and **never falls through** to the QR scan.
- [ ] Pressing `Connect` in S1/S4/S7 runs the **QR** path.
- [ ] The 3 s timeout is distinguishable from a user cancel: a filtered loopback port produces S7 with `CSP Mux did not answer. Connect to CSP instead.`, and pressing `Stop` returns to idle silently.
- [ ] Connecting through the proxy writes **`Ready · through CSP Mux`** to `StatusText` once, tone `Neutral`, **from inside the Mux `adopted` branch**; the QR route writes nothing. Verified by setting a breakpoint at `MainWindow.xaml.cs:244` and confirming the Mux route never reaches it.
- [ ] **`_autoConnectRequested` is `false` immediately after a successful adopt on both routes.** Verified by connecting, dropping the connection (kill the Mux; close CSP), and confirming the strip returns to its pre-connect state and **stays there** with no connect task started. This is the user's binding decision and it is the check for it.
- [ ] Killing the Mux while connected returns the strip to **S1 within ≤ 2 s** with no reconnect attempt.
- [ ] Extracting inside that ≤ 2 s window shows §6.7-B's route-agnostic message, not the old QR instruction.
- [ ] **No socket is leaked across 100 alternating Mux/QR connects**, verified with `netstat` and the process handle count. **Then repeat with the gate held**: force `ResetClientAsync` to hold `connectionGate` for 5 s, press Connect on the Mux route, and confirm the 3 s expiry disposes the candidate rather than orphaning an authenticated session. Confirm the Mux's client count returns to 0 and `MaximumClients` is never consumed by a leak.
- [ ] `grep -c 'connectionGate.WaitAsync' CompanionCanvasService.cs` — `AdoptAsync` is the single implementation of the connection-swap invariant, and its `WaitAsync` takes `CancellationToken.None`.

---

# 10-A. TRADE-OFFS — ADDITIONS TO §10

§10's three Mux costs and two smaller admissions stand. Four are added.

1. **The tray balloon is the suite's one OS-drawn surface.** §5.7 line 1446 deletes `MessageBox` from the suite for a design reason, not a functional one — "a native light-themed dialog shatters the suite look" — and `NotifyIcon.ShowBalloonTip` produces the same class of object: OS-drawn, system typography, the user's Windows accent, reachable by none of §1.2's brushes, §1.3's ramp or §2.17's tooltip template. It earns the exception because **the window is hidden at the moment it must speak**, so there is no in-app surface available at all. That is the entire justification, it applies to nothing else, and §9.3-A's check exists to keep it that way.

2. **Theme sharing is now reconciled, not shared.** §0.1 trades one physical file for two copies plus a script. Drift is caught at the next Debug build of the other repo, not at edit time. Accepted because the alternative is a third repository for one developer and two sibling directories.

3. **`AdoptAsync` deviates from the code it extracts.** §13.2 was framed in the draft as a pure extraction of `CompanionCanvasService.cs:31-56`. It is not: the gate acquisition moves to `CancellationToken.None` and the catch gains a filter. Both are corrections to shipped behaviour, and both are stated as deviations so a future reader diffing the two does not "restore" the leak.

4. **The QR route no longer auto-restarts after a drop.** §13.3 clears `_autoConnectRequested` on adopt for **both** routes, which is a user-visible change to existing behaviour. It follows from the user's decision applied consistently; applying it to the Mux route alone would have left one route silently reconnecting and the other not.

---

## Known minor deviations

Each is a defect that survives, with the reason it is not fixed and the bound on its consequence.

1. **`TryDeleteOwn`'s verify-then-delete is not a single atomic operation.** The read holds `FileShare.None`, so no concurrent `File.Move` replace can interleave *during* verification — but there is a sub-millisecond window between closing that handle and `File.Delete`. Closing it fully needs `SetFileInformationByHandle(FileDispositionInfo)`, a third P/Invoke. Not taken: the window is only reachable with two instances running (i.e. the §11.7 mutex not held, which requires console + RDP simultaneously), and the worst outcome is that one instance's file is removed while it still shares, degrading that user to manual connect. No security consequence, no data loss.

2. **A surviving instance never republishes.** If instance B replaces A's file and then stops, the file is gone while A is still sharing, and publish is once per session at `StartAsync`. A Companion then sees `Absent` and uses the QR path for the rest of A's session. Same reachability bound as (1). Fixing it means either a republish timer in the Mux — new machinery on a hint — or making publish reference-counted across instances, which is a distributed-state problem for a two-process local feature.

3. **§6.5's Mux network caption is 74 characters against a 71-character column** at the corrected ≈5.6 px/char metric (398 px content width). It is on §9.3's retained-with-reason allowlist and is the only surface stating which scope phones can reach, so it is not cut. Its card's `Border` is `Auto` and §5.4-R's base state has 209 px of slack, so a two-line wrap grows the card 92 → 110 and the page still does not scroll. Recorded rather than silently re-measured at a looser constant, which is how it stayed unnoticed in v1.0.

4. **Client-cap rejection is indistinguishable from a closed port.** `AcceptLoopAsync` accepts and immediately disposes past `MaximumClients` (`CompanionMultiplexer.cs:134-138`) with no rejection frame, so §13.5 case 6 folds into S7's `CSP Mux is not answering`. A better message requires a Broker protocol change, and the Broker is out of scope by A10.

5. **§5.6's event-args names in v1.0 do not match the source.** The spec writes `ClientCountEventArgs e` / `e.Count`; the real type is `CompanionClientCountChangedEventArgs` with `AuthenticatedClientCount` (`CompanionMultiplexer.cs:8-11`). Noted rather than silently corrected because §5.6 is outside this extension's scope; the implementer of Phase 8 should use the real names.