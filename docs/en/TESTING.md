# Testing — v0.1.0-alpha.3

**English** · [Português (Brasil)](../pt-BR/TESTING.md)

**v0.1.0-alpha.3** now supports experimental preservation-safe writes for existing object and spline transforms, plus safe object insertion/copy/deletion within the documented limitations.

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

- creation and copying only persist when a safe template of the same `.sco` exists;
- free-form spline creation and automatic previous/next link editing are not implemented yet; current creation is a detached copy of a real spline;
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
5. change Z, rotation, pitch and bank;
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


## Safe deletion test

1. select an existing object;
2. verify **Delete object** is available with no pending previews;
3. create a preview and verify deletion becomes disabled;
4. discard the preview;
5. click **Delete object** and confirm;
6. wait for the reload;
7. verify only the selected object disappeared;
8. verify the backup under `.mapstudio-backups/<timestamp>/`;
9. verify comments, blank lines and the following section were preserved in the tile.

For a conflict test, externally change the object's ID or `.sco` path before confirmation. Deletion must be cancelled without overwriting the tile.


## Spline editing test

1. select an existing spline;
2. press **W**, move the gizmo and verify the axis/profile follow the drag;
3. press **E** and verify only the spline's horizontal rotation can change;
4. verify viewport snapping is respected;
5. open **Path** and change X/Y/Z, rotation, length, radius or a gradient;
6. click ✕ or **Discard preview** and verify original values return;
7. repeat a change and use **Save spline**, global **Save**, or `Ctrl+S`;
8. wait for reload and verify the change persisted;
9. verify the backup under `.mapstudio-backups/<timestamp>/`;
10. verify ID, previous/next, extras, comments and unknown sections were not changed in the tile.

Externally change ID, `.sli` path, `[spline]`/ `[spline_h]` type or previous/next links before Save to verify the host cancels the write as a conflict.


## Spline copy test

1. select an existing spline;
2. in **General**, click **Place detached copy**;
3. click another point on the map;
4. verify the preview preserves the source type, length, radius, rotation and gradients;
5. adjust the desired values;
6. confirm **Confirm and save**;
7. wait for reload;
8. verify the new spline received a new global ID;
9. inspect the tile and verify the new spline has `previous = -1` and `next = -1`;
10. verify the original spline and its links were not changed;
11. verify the backup under `.mapstudio-backups/<timestamp>/`.

For a conflict test, externally change the source spline ID, path, type or links after placement starts. Creation must be cancelled.
