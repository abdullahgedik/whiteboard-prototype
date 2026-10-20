# Whiteboard Prototype

A simple whiteboard made in Unity: draw, fill, erase, pick colors, undo.
The board fills the window, with a compact tool deck at the bottom center.

## Features

- **Pen** with adjustable size and smoothed, anti-aliased strokes
- **Eraser** with two modes:
  - **Stroke**: removes whole strokes (and fills) it touches
  - **Area**: erases only the pixels under the brush
- **Fill bucket**: fills the enclosed area between drawings. A fill keeps its shape even if the lines around it are erased later.
- **Colors**: HSV picker, 12 default colors and 12 custom slots that are saved between sessions
- **Undo / redo** for everything, including Clear
- **Grid** that can be toggled on and off (it stays underneath the drawing)

## Controls

The number keys follow the tool deck from left to right.

| Key | Action |
|-----|--------|
| `1` | Pen |
| `2` | Eraser (press again to switch between Stroke and Area) |
| `3` | Fill |
| `4` | Open / close the color panel |
| `5` / `6` | Smaller / larger brush (mouse wheel also works) |
| `7` / `8` | Undo / redo (`Ctrl+Z` / `Ctrl+Y` also work) |
| `9` | Toggle grid |
| `Esc` | Close the color panel |

Clear is only available as a button (it can be undone).

In the color panel, click a custom slot to use it, click an empty slot (or **Save**) to store the current color, and right-click a slot to remove it.

## Getting started

1. Open the project with **Unity 6000.6.6f1** (Unity 6.6).
2. Open `Assets/Scenes/Whiteboard.unity`.
3. Press Play.

> In the editor, set the Game view to **Scale 1x** and turn off **Low Resolution Aspect Ratios** (in the aspect dropdown). Otherwise the frame is rendered at a lower resolution and stretched, which makes everything look blurry.

## Settings

Select the `Whiteboard` object in the scene to adjust:

- Pen and eraser default sizes and ranges
- Stroke smoothing (0 = raw input, higher = smoother but trails the cursor more)
- Fill tolerance
- Grid visibility, cell size and color
- UI scaling with screen DPI

The UI itself is regular scene objects under `Whiteboard UI`, so it can be edited in the scene directly.

## Project structure

```
Assets/
  Art/UI/                 Generated UI sprites (icons, rounded panels)
  Prefabs/UI/             Color swatch prefab
  Scenes/Whiteboard.unity Main scene
  Scripts/Whiteboard/
    WhiteboardApp.cs      Entry point: input handling and tool logic
    Canvas/               Drawing engine
      PaintSurface.cs     Raster canvas, strokes, fills, erasing, undo/redo
      CanvasOps.cs        Recorded drawing operations
      CanvasSnapshots.cs  Board snapshots for fast undo and erasing
      CanvasTexture.cs    GPU texture with partial uploads
      StrokeSmoother.cs   Stroke smoothing (Catmull-Rom)
    Colors/               Current color and palette
    UI/                   Tool deck, color panel, buttons, tooltips
    Editor/               Sprite generator and scene UI builder
```

## How it works

Every stroke and fill is recorded as an operation and drawn on the CPU into a texture the size of the screen. Each pixel also remembers which operation drew it, which is how the stroke eraser knows what is under the cursor.

Removing or restoring an operation (undo, redo, stroke eraser) only redraws that operation's bounding box: the area is restored from the nearest saved snapshot of the board, and only the operations overlapping it are drawn again. While drawing, only the changed part of the texture is uploaded to the GPU.

## Rebuilding the UI

**Tools > Whiteboard > Build Scene UI** regenerates the sprites in `Assets/Art/UI` and recreates the camera, event system and UI in the open scene. It replaces those objects, so any manual changes to them are lost. It runs automatically once if `Assets/Art/UI` is missing.
