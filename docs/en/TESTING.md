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
- creation and copying only persist when a safe template of the same `.sco` exists; persistent deletion is not implemented yet;
- splines do not have persistent editing yet;
- binary `.terrain` is not interpreted/edited yet;
- spline/O3D image textures are not applied yet;
- `[worldcoordinates]` maps remain limited;
- encrypted O3D and `.x` files remain without geometry previews.

## Safety

Do not use the only copy of an important map during this alpha. Automatic backups and preservation-safe writes are implemented, but Save is still experimental.


## Library insertion test

Start with a `.sco` that already appears in the test map:

1. open **Library** and search for the object;
2. click **Place**;
3. click a tile;
4. verify the preview appears at the clicked point;
5. change Z and rotation;
6. confirm **Confirm and save**;
7. wait for the reload;
8. verify the new object received an ID different from all existing object/spline IDs;
9. verify the tile backup;
10. reopen the map in OMSI and validate placement.

Also choose an installed `.sco` that has never been used in the map. In Full map mode the preview should work, while persistent confirmation must stay blocked with the missing-template explanation.


## Selected-object copy test

1. select an existing object with easy-to-recognize Z/rotation/pitch/bank values;
2. optionally create an unsaved numeric transform preview;
3. in the **General** tab, click **Place copy**;
4. click a different point on an existing tile;
5. verify X/Y came from the new click while Z/rotation/pitch/bank started from the selected object values;
6. confirm **Confirm and save**;
7. wait for the reload;
8. verify the copy received a new global ID and the original object remained unchanged;
9. verify the backup under `.mapstudio-backups/<timestamp>/`.
