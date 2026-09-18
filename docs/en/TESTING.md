# Testing — v0.1.0-alpha.3

**English** · [Português (Brasil)](../pt-BR/TESTING.md)

**v0.1.0-alpha.3** deliberately remains **read-only**.

## Installation

1. Download `OMSI-Map-Studio-v0.1.0-alpha.3-win-x64.zip`.
2. Extract the ZIP.
3. Run `OMSI Map Studio.exe`.
4. Click **Open OMSI** and select the OMSI 2 root directory.
5. Click **Open map** and manually choose a directory inside `maps`.

The package is self-contained for .NET 10. Microsoft Edge WebView2 Runtime must be available on Windows.

## What's new in this alpha

- redesigned UI based on the approved visual concept;
- maps are no longer listed or opened automatically;
- map streaming using an **active tile + 3×3 region**;
- cache for previously read tiles;
- neutral base surface for existing tiles;
- reading and drawing real `[spline]` / `[spline_h]` axes;
- direct spline selection in the viewport;
- spline inspector with IDs, position, rotation, length, radius and gradients;
- on-demand `.sli` reading;
- `[texture]`, `[profile]` and `[profilepnt]` parsing;
- extrusion of the selected spline's real profile;
- embedded O3D materials applied to selected-object previews.

## Main test checklist

- open a large map and confirm only the 3×3 region is loaded;
- click another visible tile and confirm it becomes the active tile;
- return to a previously visited area and check that cached loading is fast;
- click a blue spline axis;
- verify the `.sli` path, ID, length, radius and gradients in the inspector;
- open the **Profile** tab;
- for splines with a valid `[profile]`, verify that a strip/surface appears over the axis;
- select an object and verify spline selection is cleared, and vice versa;
- select an unencrypted O3D object and validate geometry/material preview.

## Known limitations

- saving, map creation and editing remain disabled;
- detailed spline geometry is loaded only for the selected spline;
- spline image textures are not applied yet;
- `[patchwork_chain]` and advanced spline material extensions are not rendered yet;
- binary `.terrain` is not interpreted yet;
- `[worldcoordinates]` maps remain schematic;
- encrypted O3D and `.x` files remain without geometry previews;
- O3D image textures are not applied yet.

## Safety

This alpha has no map write operation. **Save** remains disabled.
