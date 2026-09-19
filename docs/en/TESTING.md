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
- installed `[spline]`/`[spline_h]` creation requires a real neutral template of the same type in the map;
- binary `.terrain` is not interpreted/edited yet;
- O3D/spline textures load on demand for selections/previews; whole-map texture preloading is not implemented yet;
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


## Spline deletion test

1. create a detached spline copy and verify **Delete spline** removes it;
2. choose a connected spline with easy-to-identify previous/next neighbors;
3. click **Delete spline** and confirm;
4. after reload, verify the source disappeared;
5. verify neighbors that pointed at it now have the corresponding endpoint set to `-1`;
6. verify every modified tile has a backup under the same timestamp;
7. verify comments/unknown sections remain preserved.

For a conflict test, externally change a neighbor link before confirming deletion. No tile may be partially modified.


## Spline search test

1. use Explorer search with a `.sli` filename;
2. repeat using the spline ID and tile coordinate;
3. click the result and verify the spline is selected and focused;
4. create an edit preview and verify the **changed** marker in the list;
5. search for a term matching both objects and splines and verify both sections.


## Transactional link test

1. select a detached spline and note its ID;
2. select another spline with a free endpoint;
3. under **Chain links**, enter the appropriate ID as Previous or Next;
4. click **Save links**;
5. after reload, verify the neighbor received the reciprocal link;
6. switch to another free neighbor and verify the old neighbor endpoint returned to `-1`;
7. test **Disconnect draft** + **Save links** and verify both sides of the connection are released;
8. verify backups for every modified tile share the same timestamp.

Conflict test: try linking to an occupied endpoint or externally change a link before Save. No tile may be left partially modified.


## Spline Library test

1. open the Explorer **Splines** tab;
2. verify `OMSI 2/Splines` is scanned on demand;
3. search for a `.sli` not currently used in the map;
4. click **Normal**, choose a tile and verify the real profile preview;
5. adjust Z, rotation, length, radius and gradients;
6. confirm **Confirm and save**;
7. on a map with an explicit neutral normal `[spline]` template (5 zero extras), verify a new global ID, `previous=-1`, `next=-1`, and backup;
8. repeat with **Height** on a map containing an explicit neutral `[spline_h]` template (6 zero extras);
9. on a map without a compatible neutral template of the selected type, verify preview works but persistence is blocked.


## Real texture test

1. select an O3D object known to use BMP/PNG/JPG, DDS or TGA;
2. verify geometry appears immediately with base colors and receives the real texture when the asset arrives;
3. select a spline whose `.sli` declares `[texture]` and verify the extruded profile is textured;
4. start object and spline placements and verify cached textures are reused;
5. use a missing texture reference and verify fallback to material color without a fake placeholder;
6. verify paths escaping `Sceneryobjects`/`Splines` are never loaded.


## Material-state test

1. select an object with multiple materials;
2. open **Materials** and verify every declared texture shows its own state;
3. validate a found real texture and confirm **Loaded · EXT**;
4. test a missing reference and confirm **Missing file** without breaking geometry;
5. select a textured spline and verify the same state appears in **Profile**.


## Visual-prefetch test

1. open a full map with several textured object types;
2. without selecting objects, verify some materials near the active tile receive textures progressively;
3. verify `Auto textures: X/24` in the status bar and ensure X never exceeds 24;
4. change the active tile and verify new nearby resources can use any remaining budget;
5. select an object whose texture was outside the budget and verify selection still loads it normally;
6. verify nearby spline profiles load one at a time and the same asset is not requested twice.


## `[matl_alpha]` test

1. open an object with `[matl]` + `[matl_alpha] 1` and verify cutout without partial blending;
2. open an object with `[matl_alpha] 2` and verify partial transparency;
3. verify the `SCO: alpha ...` line in **Materials**;
4. test `[matl_noZwrite]` and `[matl_noZcheck]` on a known object;
5. use a `.sco` with `[matl_change]` and verify dynamic state is not applied as a static override;
6. verify a material whose index/texture name does not match the O3D receives no override.


## Bump-map test

1. select a `.sco` with static `[matl]` and `[matl_bumpmap]`;
2. verify the bump name and factor in the inspector;
3. verify the asset transitions from waiting/loading to loaded;
4. compare surface detail against an equivalent material without bump mapping;
5. temporarily remove the bump image and verify fallback without a placeholder/fake.


## Night-map test

1. select an object with static `[matl_nightmap]`;
2. verify **preview off** while Nightmap is unchecked;
3. enable **Nightmap** and verify the real asset loads;
4. verify emission without replacing the diffuse texture;
5. disable it and verify daytime preview returns;
6. verify `[matl_change]` is still not executed.
