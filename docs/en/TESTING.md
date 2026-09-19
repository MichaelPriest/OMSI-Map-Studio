# Testing — v0.1.0-alpha.3

**English** · [Português (Brasil)](../pt-BR/TESTING.md)

**v0.1.0-alpha.3** can now save only **placed-object transforms**. Other editing operations remain blocked.

## Installation

1. Download and extract the Alpha.3 package.
2. Run `OMSI Map Studio.exe`.
3. Click **Open OMSI** and select the OMSI 2 root directory.
4. Click **Open map** and manually choose a folder inside `maps`.

## Main checklist

- open Grundorf or another test map;
- confirm **Full map** is the default mode;
- validate tile, O3D object and spline loading;
- select an object;
- press **W**, drag the gizmo and confirm **Unsaved preview**;
- press **E** and rotate the object;
- use `Ctrl+Z` and `Ctrl+Y` to validate undo/redo;
- in the **Transform** tab, type an exact X/Y/Z or rotation value and press Enter;
- press **F** to focus selection;
- use **G**, **O** and **L** to toggle grid, objects and splines;
- click ↶ and verify the preview returns to the source transform;
- repeat a transform and click **Save** or press `Ctrl+S`;
- wait for the map to reload;
- verify the new position/rotation remains after reload;
- verify a `.mapstudio-backups/<timestamp>/` directory was created under the map;
- inspect the saved tile as text and verify unknown sections/comments were not removed.

## Conflict test

With an unsaved preview pending, externally change the identity of that same `[object]` block (for example its ID or `.sco` path) before Save. Map Studio must cancel the batch and show a conflict instead of silently overwriting the tile.

## Known limitations

- Save only persists position/rotation/pitch/bank for existing `[object]` entries;
- creating, duplicating or deleting objects is not persisted yet;
- splines do not have persistent editing yet;
- binary `.terrain` is not interpreted/edited yet;
- spline/O3D image textures are not applied yet;
- `[worldcoordinates]` maps remain limited;
- encrypted O3D and `.x` files remain without geometry previews.

## Safety

Do not use the only copy of an important map during this alpha. Automatic backups and preservation-safe writes are implemented, but Save is still experimental.
