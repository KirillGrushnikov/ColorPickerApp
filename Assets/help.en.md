# Help

<!--
Program help (Help tab). Embedded into exe at build.
Supports simple Markdown: # / ## / ### - headings, "- " - lists, **bold**, `code`,
empty line - new paragraph, code block - between lines ```. Such comments are not shown.
-->

## Color formats

A color can be written in several ways. Below is what each format means, where it is used, and what it looks like.

### RGB / RGBA

**What it is:** red, green, and blue channels: 0–255. Alpha is transparency: 0–1.

**Where it is used:** web (CSS), Figma, Photoshop, Illustrator, and most graphics programs.

### HSV

**What it is:** hue **H** — 0–360° (position on the color wheel), saturation **S** and brightness **V** — 0–100%.

**Where it is used:** convenient for picking a color “by eye.” This is how the wheel and square on the left work. CSS does not have this format — it has `hsl`; the format is available in Photoshop, Unity, OpenCV.

**Tip:** the mouse wheel over the wheel changes hue. With Shift — 10° steps.

### Normal (0..1)

**What it is:** the same RGB, but the values are written as fractions 0–1.

**Where it is used:** shaders (GLSL/HLSL), Unity (`new Color(r, g, b)`), Godot, Blender, OpenGL.

### HEX

**What it is:** 16-bit color: 5 bits red, 6 bits green, 5 bits blue. No alpha.

**Where it is used:** TFT displays of microcontrollers: Arduino, ESP32; ILI9341, ST7735; Adafruit GFX, TFT_eSPI.

**Feature:** shades become coarser because fewer bits are allocated per channel.

### OKLCH

**What it is:** **L** — lightness 0–100% (as perceived by the eye), **C** — chroma, i.e. saturation, approximately 0–0.37, **H** — hue 0–360°.

**Why it is needed:** the space is perceptually uniform. The same step in L gives an equally noticeable difference, so it is convenient for building palettes and themes.

**Where it is used:** CSS (Color 4), Tailwind 4.

**“OKLCH” tab:** hue is selected on the wheel, L and C — with sliders. Colors that the screen cannot display are brought to the nearest ones — C is reduced.

### CMYK

**What it is:** a subtractive model for printing. **C** — Cyan; **M** — Magenta; **Y** — Yellow; **K** — Key/Black. Values — 0–100%: the higher the percentage, the more ink.

**Where it is used:** printing industry, Photoshop, Illustrator, InDesign, PDF for print.

**Important:** CMYK on screen is only a preview. The final color depends on the ICC profile, paper, ink, and printing press. Therefore, the same RGB can produce different CMYK, and the exact color on paper cannot be guaranteed from the on-screen appearance.

**Alpha:** CMYK has no alpha channel. Transparency is set separately by layout tools or is not supported.

### The ƒ / # button next to the format

Turns the wrapper on and off when copying:

- `rgb(255, 0, 0)` ↔ `255, 0, 0`
- for HEX: `#ff0000` ↔ `ff0000`

## Shade scale

11 steps: 50–950, as in Tailwind and Material.

- 50 — almost white.
- 950 — almost black.
- The source color is placed at the nearest step by lightness.

The scale is built in OKLCH, so the steps are even and the hue does not drift.

Copied as CSS variables, a Tailwind 4 theme, a Tailwind 3 object, or a list.

## WCAG contrast

Contrast is the ratio of text and background luminance: from 1:1 (same colors) to 21:1 (black on white).

- **Normal text:** AA — from 4.5, AAA — from 7.
- **Large text** — from 24 px or from 18.7 px bold: AA — from 3, AAA — from 4.5.
- **Icons, field borders, graphics, button states:** from 3.

AA is the usual minimum for websites and applications. AAA is an increased level.

“Pick text / background” finds the nearest color that passes the selected level. Only lightness changes; hue and saturation remain the same.

## Window layout

Tabs can be opened side by side instead of switching between them: **settings → “Window layout”**.

- Drag a tab with the mouse to another column.
- You can drag it to “+ new column” — on the left or right.
- You can drag it to “Hidden”.

The main column — the eyedropper, “Values”, and the color strip — always stays in place. Each column widens the window; the height does not change.

The “Designer” preset — Contrast | Values | History. “Classic” returns everything as it was. Open tabs are remembered between launches.

## Hotkeys

Assigned in settings, on the left panel.

You can use:

- keys with Ctrl/Alt;
- F1–F24;
- mouse buttons: Mouse4 (“back”), Mouse5 (“forward”) — with or without modifiers;
- middle mouse button — only with Ctrl/Alt.

Pressing a hotkey is not passed to other programs.

## Eyedropper

- **Left mouse button**, Enter, or Space — select.
- **Right mouse button** or Esc — cancel.
- **Mouse wheel**, `+`, and `-` — magnifier zoom.
- **Arrow keys** — move by 1 pixel. With Shift — by 10.