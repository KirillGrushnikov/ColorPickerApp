# Help

<!--
The app help (the "Help" tab). Embedded into the exe at build time.
Simple Markdown: # / ## / ### - headings, "- " - lists, **bold**, `code`,
an empty line - new paragraph, a code block - between ``` lines. Comments like this are not shown.
-->

## Color formats

### RGB / RGBA

```
rgb(255, 0, 0)    rgba(255, 0, 0, 0.50)
```

Red, green, blue: 0–255, alpha 0–1. The standard on the web (CSS) and in most apps: Figma, Photoshop, Illustrator.

### HSV

```
hsv(0, 100%, 100%)    hsva(0, 100%, 100%, 0.50)
```

Hue (H) 0–360° is the position on the wheel, saturation (S) and value (V) are 0–100%. That's how the wheel and the square on the left work — handy for picking a color by eye. The **mouse wheel** over the wheel changes the hue (with Shift — by 10°). Not in CSS (it has hsl), but used in Photoshop, Unity, OpenCV.

### Normal (0..1)

```
(1.00, 0.00, 0.00)    (1.00, 0.00, 0.00, 0.50)
```

The same RGB as fractions 0–1. For shaders (GLSL/HLSL), Unity (`new Color(r, g, b)`), Godot, Blender, OpenGL.

### HEX

```
#ff0000    with alpha: #ff000080 (CSS) or #80ff0000 (XAML)
```

Hexadecimal RGB. With alpha it has 8 digits: `#RRGGBBAA` by default (alpha at the end, as in CSS and Figma); in the settings you can switch to `#AARRGGBB` (Android, WPF, XAML) and turn on uppercase. The Hex field shows alpha for translucent colors and accepts 6 or 8 digits.

### RGB-565

```
63488
```

16-bit color: 5 bits of red, 6 of green, 5 of blue, no alpha. For microcontroller TFT displays (Arduino, ESP32; ILI9341, ST7735; Adafruit GFX, TFT_eSPI). Shades get coarser.

### OKLCH

```
oklch(62.8% 0.258 29.2)    oklch(62.8% 0.258 29.2 / 0.50)
```

**L** is lightness 0–100% (as the eye sees it), **C** is chroma (saturation) roughly 0–0.37, **H** is hue 0–360°. A perceptually uniform space: equal L steps look equally different, so it's convenient for palettes and themes. Supported in CSS (Color 4), used by Tailwind 4.

On the "OKLCH" tab the hue is picked on the ring, L and C with sliders; colors the screen can't show are mapped to the nearest ones (C is reduced).

### The ƒ / # button next to the format

Turns the wrapper on and off when copying: `rgb(255, 0, 0)` ↔ `255, 0, 0`, and for HEX — `#ff0000` ↔ `ff0000`.

## Shades

11 steps 50–950, like Tailwind and Material: 50 is almost white, 950 is almost black, the original color takes the step closest in lightness. Built in OKLCH, so the steps are even and the hue doesn't drift. Copied as CSS variables, a Tailwind 4 theme, a Tailwind 3 object or a plain list.

## WCAG contrast

The luminance ratio of text and background: from 1:1 (same colors) to 21:1 (black on white).

- **Normal text:** AA — 4.5 and up, AAA — 7 and up.
- **Large text** (24 px and up, or 18.7 px bold): AA — 3 and up, AAA — 4.5 and up.
- **Icons, input borders, charts, button states:** 3 and up.

AA is the usual minimum for websites and apps, AAA is the enhanced level. "Fix text / background" finds the nearest color that passes the chosen level: only lightness changes, hue and saturation stay.

## Window layout

Tabs can be shown side by side instead of switching between them: **settings → "Window layout"**. Drag a tab with the mouse to another column, to "+ new column" (on the left or right) or to "Hidden". The main column (eyedropper, Values and the color bar) always stays. Each column makes the window wider; the height doesn't change. The "Designer" preset is Contrast | Values | History, "Classic" puts everything back. Open tabs are remembered between launches.

## Hotkeys

Assigned in the settings (the panel on the left). You can use keys with Ctrl/Alt, F1–F24 or mouse buttons: Mouse4 ("back"), Mouse5 ("forward") — with or without modifiers, the middle button only with Ctrl/Alt. A hotkey press isn't passed on to other apps.

## Eyedropper

- **Left click**, Enter or Space — pick
- **Right click** or Esc — cancel
- **Mouse wheel**, `+` and `-` — loupe zoom
- **Arrows** — move by 1 pixel (with Shift — by 10)
