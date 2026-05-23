# PixelPerfect — Technical Reference

A .NET 10 WinForms image editor with per-channel color-space adjustment, GPU-accelerated 3D color-space visualization, and an atomic design system.

---

## Architecture Overview

PixelPerfect follows a **layered UI architecture** (Atoms → Molecules → Organisms → Templates) with a clean service layer and async processing pipeline.

```
MainEditorForm  (Presenter — owns ImageModel + IImageService, wires events)
  └── MainLayout  (Template — composes all organisms, manages tool modes)
        ├── TopBar          (image metadata + reset)
        ├── LeftToolbar     (file / edit / theme buttons)
        ├── CanvasPanel     (pan/zoom canvas + rulers)
        │     ├── BottomToolbar  (floating pill — tool switcher)
        │     └── ColorSpaceBar  (floating pill — color-space chip selector)
        ├── ColorSpaceViewerPanel  (GPU 3D viewer — overlays CanvasPanel)
        └── ColorSettingsPanel     (right panel — sliders + swatches)
```

### Design Patterns
- **Atomic design** — tokens → simple controls → composites → pages
- **Service layer** — `IImageService` abstracts all image I/O and transforms
- **Theme system** — `IThemeable` cascades `ApplyTheme()` from root; `AppColors.ThemeChanged` event
- **Async debounce** — slider changes debounced 80 ms, processed off-UI-thread with `CancellationToken`
- **GPU rendering** — OpenGL GLSL fragment shaders ray-cast 3D geometry analytically

---

## File Reference

### `Program.cs`
Entry point. Configures high-DPI awareness and visual styles, then launches `MainEditorForm`.

---

## Models

### `Models/ImageModel.cs`
Holds the bitmap state of a loaded image.

| Member | Purpose |
|--------|---------|
| `OriginalBitmap` | Source bitmap — never modified |
| `WorkingBitmap` | Display bitmap after color adjustments |
| `FilePath`, `Format`, `FileSizeBytes`, `ColorMode` | Image metadata |
| `Width`, `Height` | Derived from `OriginalBitmap` |
| `ResetToOriginal()` | Copies `OriginalBitmap` back into `WorkingBitmap` |
| `Dispose()` | Releases both bitmaps |

Owned by `MainEditorForm`; passed to `ImageService` for transforms.

---

### `Models/ColorSettings.cs`
Configuration snapshot for per-channel color adjustments.

- `ColorSpaceMode` enum: `RGB`, `HSV`, `CMYK`, `LAB`, `YUV`, `YCbCr`
- `Channel1`–`Channel4` deltas (double), plus semantic aliases (`RedAdjust`, `HueAdjust`, etc.)
- `QuantizeCount` — number of colors for palette quantization (0 = skip)
- `IsDefault` — true when all deltas are zero
- `Clone()` — safe copy for background threads

Produced by `ColorSettingsPanel.GetSettings()`; consumed by `ImageService.ApplyColorSettings()`.

---

### `Models/ExportSettings.cs`
Configuration for saving an image.

| Property | Type | Notes |
|----------|------|-------|
| `Format` | string | `"PNG"`, `"JPEG"`, `"BMP"` |
| `SavePath` | string | Full file path |
| `JpegQuality` | int | 1–100 |

Produced by `ExportDialog`; consumed by `ImageService.ExportImage()`.

---

## Services

### `Services/IImageService.cs`
Interface defining all image operations.

```csharp
ImageModel LoadImage(string filePath);
void ExportImage(ImageModel model, ExportSettings settings);
Bitmap ApplyColorSettings(Bitmap source, ColorSettings settings);
Color SamplePixel(Bitmap bmp, int x, int y);
IReadOnlyList<Color> GetDominantColors(Bitmap bmp, int count);
```

### `Services/ImageService.cs`
Concrete implementation.

| Method | Notes |
|--------|-------|
| `LoadImage()` | Reads bitmap, clones to `ImageModel`, detects format by extension + color mode by sampling |
| `ExportImage()` | Creates directories, saves with JPEG codec for quality control |
| `ApplyColorSettings()` | Delegates to `BitmapHelper`; runs quantization if `QuantizeCount > 0` |
| `GetDominantColors()` | Delegates to `BitmapHelper.ApplyQuantization()` |

---

## Helpers

### `Helpers/ColorMath.cs`
Pure static color-space math. No GDI+ dependencies.

| Function Group | Functions |
|----------------|-----------|
| Formatting | `ToHex(Color)` |
| HSV | `ToHsv()`, `FromHsv()` |
| CMYK | `ToCmyk()`, `FromCmyk()` |
| LAB | `ToLab()`, `FromLab()` (via XYZ, D65) |
| YUV BT.601 | `ToYuv()`, `FromYuv()` |
| YCbCr BT.601 | `ToYCbCr()`, `FromYCbCr()` |
| Adjustment | `AdjustRgb/Hsv/Cmyk/Lab/Yuv/YCbCr()` |

Used by `BitmapHelper.AdjustPixel()` (CPU pipeline) and referenced by the GLSL shaders (GPU pipeline).

---

### `Helpers/BitmapHelper.cs`
Low-level pixel manipulation using `LockBits` for performance.

| Method | Notes |
|--------|-------|
| `ApplyColorSettings()` | Locks both bitmaps, iterates BGRA memory, calls `AdjustPixel()` per pixel |
| `ApplyQuantization()` | Median-cut: sample ≤ 50 000 pixels, recursively split by widest axis, average each box, map all pixels |
| `SamplePixel()` | Bounds-safe `GetPixel()` wrapper |

---

### `Helpers/ColorSpaceRenderer.cs`
Software (CPU) 3D color-space renderer using GDI+ `PathGradientBrush`. Not used in the current UI (GPU renderer is active) but kept as reference.

- `Camera` struct: `Yaw`, `Pitch`, `Zoom`, `Pan`
- `Render(mode, w, h, camera)` dispatches to mode-specific drawers
- Each drawer projects 3D geometry via `Project3D()` (yaw/pitch rotation + orthographic)

---

### `Helpers/SvgIconHelper.cs`
Loads `.svg` files from `icons/`, recolors them, caches rendered bitmaps.

- `Load(fileName, size, color)` — regex-replaces `stroke`/`fill` colors in SVG XML, renders via `Svg` library, caches by `name_size_argb`
- `ClearCache()` — dispose all cached bitmaps; called on `AppColors.ThemeChanged`

---

## UI: Atoms

### `UI/Atoms/AppColors.cs`
Central color palette with light/dark theme toggle.

| Property Group | Properties |
|----------------|-----------|
| Backgrounds | `Background`, `PanelBg`, `Canvas`, `SurfaceAlt` |
| Text | `TextPrimary`, `TextSecondary`, `TextWhite`, `TextOrange` |
| Accent | `Accent` (#FA7B3D), `AccentDark` |
| Borders | `BorderPrimary`, `BorderDark`, `BorderGray`, `BorderOrange` |
| Icons | `IconNormal` (#6B7280), `IconHover`, `IconActive` |
| Rulers | `RulerBg`, `RulerTick` |
| Fixed channel colors | `ChannelRed/Green/Blue/Yellow/Purple/Cyan` |

`ToggleTheme()` switches themes and fires `ThemeChanged`; all `IThemeable` controls subscribe and repaint.

---

### `UI/Atoms/AppFonts.cs`
Shared font singletons: `Small` (7.5pt), `Label` (8.5pt), `Value` (8.5pt bold), `Body` (9pt), `Header` (10pt), `Mono` (8.5pt Consolas). Call `DisposeAll()` on app exit.

---

### `UI/Atoms/AppSpacing.cs`
Design-token constants: gap scale, padding scale, radius scale, and fixed layout values (`LeftToolbarWidth=48`, `IconSize=24`, `RulerSize=20`, etc.).

---

### `UI/Atoms/AppSlider.cs`
Custom slider control.

- Renders: gray full track, colored filled portion, white pill-shaped thumb
- `TrackColor` — per-channel color (e.g., red for R channel)
- `Minimum`, `Maximum`, `Value` — double-precision
- `ValueChanged` event

---

### `UI/Atoms/IconButton.cs`
Square button that renders an SVG icon.

| Property | Effect |
|----------|--------|
| `SvgFileName` | Icon loaded from `icons/` |
| `IsActive` | Active state (orange bg + white icon if `UseActiveBg`) |
| `IsToggle` | Sticky click behavior |
| `UseActiveBg` | When false, suppresses orange bg (eye button style) |
| `ActiveIconColor` | Icon color when active and `UseActiveBg=false` |

---

### `UI/Atoms/IThemeable.cs`
Interface with single method `ApplyTheme()`. Implemented by all custom controls; called by `MainLayout.ApplyTheme()` to cascade theme changes.

---

## UI: Molecules

### `UI/Molecules/LabeledSlider.cs`
Two-row composite slider.

```
Row 1:  [Eye]  [Channel name]  ·······  [Value box]
Row 2:  [────────────── slider ──────────────────]
```

- Eye button: toggles visibility — swaps `Eye.svg` ↔ `Eye Closed.svg`, disables slider
- Value box: live integer readout in rounded chip
- `TrackColor`, `ChannelName`, `Minimum`, `Maximum`, `Value`
- `ValueChanged` event

---

### `UI/Molecules/ColorSwatch.cs`
Rounded-rectangle solid color display. Properties: `SwatchColor`, `ShowBorder`, `CornerRadius`. Used in `ColorSettingsPanel` for dominant colors and picked color.

---

### `UI/Molecules/ColorCountButton.cs`
Capsule chip for selecting quantization count (2 / 4 / 8 / 16 / 64 / 256).

- Selected: solid orange fill, white text
- Unselected: outlined chip
- Hover: orange border + text
- `CountValue`, `IsSelected`, `Selected` event

---

### `UI/Molecules/DialogButtonRow.cs`
Reusable dialog footer with Cancel + primary action button. Also defines `AppButton` (reusable rounded button with hover state and `IsPrimary` flag). Used in all dialogs.

---

## UI: Organisms

### `UI/Organisms/TopBar.cs`
Header bar. Displays: dimensions, format, file size, color mode. Right-aligned Reset Changes button. `UpdateImageInfo()` called by `MainEditorForm` on load.

---

### `UI/Organisms/LeftToolbar.cs`
Vertical icon toolbar. Top group: Back, Forward, Open, Eyedropper (toggle), Export. Bottom group: Theme toggle. Events: `BackClicked`, `ForwardClicked`, `OpenClicked`, `EyedropperToggled`, `ExportClicked`, `ThemeToggled`.

---

### `UI/Organisms/ColorSettingsPanel.cs`
Right-side control panel.

**Sections** (vertical):
1. Header label
2. 3–4 `LabeledSlider` instances (per-channel, reconfigured on mode change)
3. Count row: `ColorCountButton` chips (2, 4, 8, 16, 64, 256)
4. Swatches `FlowLayoutPanel`: dominant colors + picked color

**Color space slider ranges:**

| Mode | Ch1 | Ch2 | Ch3 | Ch4 |
|------|-----|-----|-----|-----|
| RGB | R ±255 | G ±255 | B ±255 | — |
| HSV | H ±180 | S ±100 | V ±100 | — |
| CMYK | C ±100 | M ±100 | Y ±100 | K ±100 |
| LAB | L ±100 | a ±128 | b ±128 | — |
| YUV | Y ±255 | U ±112 | V ±157 | — |
| YCbCr | Y ±255 | Cb ±128 | Cr ±128 | — |

Key methods: `GetSettings()`, `SetColorSpaceMode()`, `ResetToDefaults()`, `SelectCount()`, `SetPickedColor()`, `SetDominantColors()`. `SettingsChanged` event.

---

### `UI/Organisms/ColorSpaceViewerPanel.cs`
GPU-accelerated 3D color-space viewer.

**Components:**
- `ModeBar` (nested class) — custom-painted floating chip bar; no child controls → no event bubbling
- `GLControl` — native OpenGL surface

**GL setup:**
- One GLSL program compiled per `ColorSpaceMode` (6 total)
- Fullscreen quad (TriangleStrip); each pixel is ray-cast in the fragment shader
- Uniforms per frame: `uYaw`, `uPitch`, `uZoom`, `uPan`, `uRes`, `uBg`, `uColorSpace`, `uAdjust`

**Fragment shaders (GLSL 330 core):**

| Shader | Geometry | Color mapping |
|--------|----------|---------------|
| RGB | AABB cube ±1 | `(hit+1)/2` → RGB |
| HSV | Cylinder r=1, y=±1 | `atan2(z,x)` → hue, `r` → sat, `y` → val; caps: hue wheel top, black bottom |
| CMYK | AABB cube | Hit → CMY, K=0; convert to RGB |
| LAB | Unit sphere | `y` → L, XZ angle+radius → a/b; `lab2rgb()` |
| YUV | AABB cube | Hit → Y/U/V ranges; BT.601 matrix |
| YCbCr | AABB cube | Hit → Y/Cb/Cr ranges; BT.601 digital |

All shaders output `uBg` color on ray miss (instead of black).

**Mouse handling (private, no base calls — no event bubbling):**
- Left drag: orbit (`yaw += dx×0.008`, `pitch += dy×0.008`)
- Right drag: pan
- Scroll: zoom (clamped 0.2–5×)
- Left click (no drag): `GL.ReadPixels()` + Y-flip → fire `ColorPicked`

`BottomReserve` property carves space at the bottom of the GLControl so the floating `BottomToolbar` is not occluded.

Events: `ColorPicked`, `ModeChanged`.

---

### `UI/Organisms/CanvasPanel.cs`
Central image canvas.

**Rendering layers:**
1. `AppColors.Canvas` background
2. Checkerboard under image (8×8 gray tiles for transparency)
3. Image (nearest-neighbor on zoom-in, bilinear on zoom-out)
4. Workspace rulers (top + left strips, 20 px wide, auto-scaled tick step)
5. Corner fill

**Mouse:**
- Left drag: pan
- Scroll: zoom toward cursor (0.05–20×)
- Eyedropper left click: `PixelPicked` event
- `AllowDrop=true`: `FileDropped` event on image file drop

`SuppressMouseWheel` property disables scroll-zoom when the 3D viewer is active (prevents double-zoom from WM_MOUSEWHEEL bubbling).

---

### `UI/Organisms/BottomToolbar.cs`
Floating pill toolbar (4 tool buttons).

Buttons: Select → ColorSettings → Photo → ColorSpace. One active at a time.

`OnPaintBackground()` renders the parent surface behind itself (via `InvokePaintBackground`/`InvokePaint`) for true visual transparency. Window region is set to the pill shape so the area outside the pill receives no mouse events and shows through. `ToolChanged` event carries `ActiveTool` enum.

---

### `UI/Organisms/ColorSpaceBar.cs`
Floating secondary pill shown only in ColorSettings mode. Contains 6 mode chips (same as `BottomToolbar` above but for color-space selection). Same transparency mechanism as `BottomToolbar`. `ModeChanged` event carries `ColorSpaceMode`.

---

## UI: Templates

### `UI/Templates/MainLayout.cs`
Root composition control.

**Tool modes:**

| Mode | Canvas | 3D Viewer | ColorSpaceBar | ColorSettingsPanel |
|------|--------|-----------|---------------|--------------------|
| Select | ✓ | — | — | ✓ |
| ColorSettings | ✓ | — | ✓ | ✓ |
| Photo | ✓ | — | — | ✓ |
| ColorSpace | ✓ (behind) | ✓ | — | ✓ |

**Overlay positioning (`PositionOverlays()`):**
- `ColorSpaceViewer.Bounds = CanvasPanel.Bounds` (same-parent siblings → full occlusion in 3D mode)
- `BottomToolbar` and `ColorSpaceBar` are attached to the active surface (CanvasPanel or ColorSpaceViewer) via `AttachOverlaysToActiveSurface()` so their transparency blends with what is visible

**Event wiring:**
- `BottomToolbar.ToolChanged` → `OnToolChanged()`
- `ColorSpaceBar.ModeChanged` → `ColorSettingsPanel.SetColorSpaceMode()` + `ColorSpaceViewer.SetColorSpaceMode()`

---

## UI: Dialogs

### `UI/Dialogs/ExportDialog.cs`
Borderless save-as dialog. Format toggle (PNG / JPEG / BMP), path text box + folder browse, Cancel / Save. Produces `ExportSettings` on OK.

### `UI/Dialogs/ColorPickerPopup.cs`
Borderless tooltip shown after pixel pick. Displays: color swatch, hex, RGB, CMYK, LAB, HSV. Copy button. Auto-closes on Escape or focus loss.

### `UI/Dialogs/ResetDialog.cs`
Confirmation dialog for Reset Changes. Cancel / Reset.

---

## `MainEditorForm.cs`

Presenter layer. Owns `ImageModel` and `IImageService`.

**State:**

| Field | Purpose |
|-------|---------|
| `_imageService` | Concrete `ImageService` |
| `_layout` | Root `MainLayout` |
| `_model` | Current `ImageModel` (null until loaded) |
| `_displayBitmap` | Last processed output bitmap |
| `_debounce` | 80 ms `Timer` delays processing while dragging |
| `_cts` | `CancellationTokenSource` for in-flight async work |

**Key methods:**

| Method | Notes |
|--------|-------|
| `LoadImageFromPath()` | Load, update TopBar, extract 8 dominant colors, pre-select count |
| `ExportImage()` | Show dialog, apply current settings, save |
| `OnDebounce()` | Cancel previous → `Task.Run` → apply + quantize → update UI |
| `Undo()` | Reset to original |
| `Redo()` | Re-apply current sliders |
| `OnPixelPicked()` | Update sliders, show `ColorPickerPopup` near cursor |

---

## Image Processing Pipeline

```
User opens / drops file
        ↓
ImageService.LoadImage()
  → Read bitmap, detect format + color mode
  → ImageModel { OriginalBitmap, WorkingBitmap }
        ↓
User drags slider → debounce 80ms
        ↓
Task.Run() [background thread]
  → ImageService.ApplyColorSettings(original, settings)
    → BitmapHelper.ApplyColorSettings()
      → LockBits both bitmaps
      → Per-pixel: ColorMath.Adjust*() in active color space
      → UnlockBits, return new Bitmap
  → If QuantizeCount > 0:
    → BitmapHelper.ApplyQuantization()
      → Median-cut: sample → split → average → map
        ↓
Back on UI thread (if not cancelled)
  → CanvasPanel.SetBitmap(result)
  → ColorSettingsPanel.SetDominantColors(colors)
        ↓
User exports
  → ExportDialog → ExportSettings
  → ImageService.ExportImage() → disk
```

---

## Color Space Conversions

| Space | Ch1 | Ch2 | Ch3 | Ch4 | Notes |
|-------|-----|-----|-----|-----|-------|
| RGB | R 0–255 | G 0–255 | B 0–255 | — | Native |
| HSV | H 0–360° | S 0–100% | V 0–100% | — | Hue wraps |
| CMYK | C 0–100 | M 0–100 | Y 0–100 | K 0–100 | Derived from RGB |
| LAB | L 0–100 | a ±128 | b ±128 | — | D65, via XYZ |
| YUV | Y 0–255 | U ±112 | V ±157 | — | BT.601 analogue |
| YCbCr | Y 16–235 | Cb 16–240 | Cr 16–240 | — | BT.601 digital |

---

## Theme System

1. User clicks theme toggle → `AppColors.ToggleTheme()`
2. `AppColors.ThemeChanged` fires
3. `MainEditorForm` calls `ApplyTheme()` on `MainLayout`
4. `MainLayout` cascades to all children implementing `IThemeable`
5. `SvgIconHelper.ClearCache()` drops recolored icon bitmaps
6. All controls repaint with updated `AppColors` values

---

## Performance Notes

| Technique | Benefit |
|-----------|---------|
| `LockBits` pixel access | ~10× faster than `GetPixel/SetPixel` |
| 80 ms debounce + cancellation | No wasted work during slider drag |
| `Task.Run` off-UI-thread | UI stays responsive during heavy processing |
| GPU GLSL ray-cast | Orbit/zoom/pan = uniform upload only, no geometry recalc |
| Median-cut quantization | O(n log k) palette extraction |
| SVG bitmap cache | Icon SVGs rendered once per size/color |

---

## External Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `OpenTK.GLControl` | 4.0.2 | WinForms OpenGL surface |
| `OpenTK.Graphics` | 4.9.4 | OpenGL 4 API bindings |
| `Svg` | 3.4.6 | SVG file parsing + rendering |
| .NET 10 WinForms | built-in | UI framework |
