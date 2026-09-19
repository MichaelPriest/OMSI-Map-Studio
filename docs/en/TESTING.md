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


## Envmap test

1. select an object with static `[matl_envmap]`;
2. verify file and strength in the inspector;
3. verify the real texture loads;
4. compare reflection against the same material without envmap;
5. validate strengths 0, 0.4, and 1 and confirm out-of-range values are clamped in preview.


## Runtime material-command test

1. open an object using `[matl_transmap] \S:...`;
2. verify the reference appears in the inspector as **runtime not simulated**;
3. open a material with `[matl_lightmap]` and verify the same warning;
4. verify neither command triggers fake loading/application in the viewport.


## Material diagnostics test

1. open a `.sco` with `[matl_envmap_mask]`, `[alphascale]`, or `[matl_allcolor]`;
2. verify the corresponding material shows `Not simulated:` in the inspector;
3. verify repeated commands are not duplicated;
4. verify these commands do not visually alter the material at this stage.


## LRU cache test

1. navigate/select enough resources to exceed 64 unique textures;
2. verify `Cache: X/64` never exceeds 64;
3. return to an object whose old texture was evicted and verify it can be requested again;
4. click **Clear cache** and verify `Cache: 0/64` and `Auto textures: 0/24` before progressive repopulation;
5. verify the map stays loaded and only texture assets are discarded.


## Object LOD test

1. select an object with `[LOD] 0.6`, `[LOD] 0.2`, and `[LOD] 0`;
2. verify the LOD labels in **Geometry**;
3. move the camera close and verify the highest-threshold group;
4. move away and verify switching to 0.2 and 0;
5. verify only one LOD group is active at a time;
6. verify **Global** meshes remain visible at every level.


## Spline surface test

1. open a tile with roads/splines and wait for `.sli` profile prefetch;
2. with **Spline profiles** enabled, verify nearby axes gain extruded surfaces;
3. compare curvature, width, elevation, and gradient against the axis;
4. verify real textures when available;
5. change the active tile and verify the 3×3 window follows the new area;
6. disable **Spline profiles** and verify immediate return to axis-only mode.


## Terrain diagnostics test

1. open a map tile containing `[terrain]` and a `.map.terrain` sidecar;
2. verify **Present** + **Found** and the size in the inspector;
3. test `[terrain]` without a sidecar and verify **Missing**;
4. test an existing sidecar without the marker and verify the inconsistency is visible;
5. verify no `.terrain` file is modified.

## Fullscreen and navigation test

1. open a map and press **F11**; verify the Windows frame/title bar disappears;
2. press **Esc** and verify the previous window state is restored;
3. repeat using the editor toolbar fullscreen button;
4. right-drag and verify camera orbit without accidental object selection;
5. middle-drag and verify camera-target panning;
6. use Shift + middle-drag and verify accelerated panning;
7. validate smooth zoom with the wheel and `+` / `-`;
8. use arrows and Shift + arrows to move across the map;
9. trigger progressive loading of several O3D models/textures, move the camera while loading, and verify position/target/zoom do not jump back to the initial fit;
10. verify left-click selection and edit gizmos still work;
11. validate **F**, **Home**, **1**, and **2** after manual navigation.

## Terrain-mesh test

1. open Grundorf and wait for tile loading;
2. verify **Terrain** is enabled and the flat background is replaced by the height mesh where a valid `.map.terrain` exists;
3. in the inspector, verify **60×60 cells**, **3,721 heights**, and a plausible altitude range for the active tile;
4. change the active tile in 3×3 mode and verify terrain meshes follow the loaded region;
5. disable **Terrain** and verify height geometry disappears without affecting objects/splines;
6. re-enable it and verify camera fitting respects terrain elevation instead of snapping to Y=0 on elevated maps;
7. test an invalid/truncated sidecar and verify the map still opens, diagnostics still expose the file, and no fake mesh is created;
8. verify no `.terrain` file is modified.

## Base terrain-texture test

1. open a map whose `global.cfg` contains one or more `[groundtex]` entries;
2. verify the declared layer count in the inspector;
3. verify the first real main texture loads and replaces the terrain mesh's neutral material;
4. verify visual tiling follows layer 0's `Repeating` value;
5. verify the detail texture reports its loading state in the inspector but is not visually blended at this stage;
6. temporarily rename a base texture in a test map and verify fallback to the neutral material with no fake placeholder;
7. test a path attempting to escape the OMSI installation and verify the host rejects it;
8. verify toggling **Terrain** still hides/shows the mesh without modifying map files.

## Terrain-paint mask test

1. open a map containing `texture/map/tile_*.map.N.dds` files;
2. select a tile with masks and verify the **Terrain masks** inspector entry lists the discovered indices;
3. verify layer 0 remains the base and every index N paints only the region described by its DDS mask;
4. compare index N against the corresponding `[groundtex]` entry and verify the correct main texture is used;
5. verify each layer's repeating value is respected;
6. change tiles in 3×3 mode and verify only masks for the newly active area are loaded/rendered;
7. verify transparent mask areas leave lower layers visible;
8. test an index without a matching `[groundtex]` and verify no invented texture is shown;
9. verify `.terrain_0.rdy` diagnostics (validity, vertices, triangles, and transform) without modifying any file;
10. verify objects, splines, relief, and navigation remain functional over painted terrain layers.

## Terrain-layer controls and validation test

1. open a map with multiple `[groundtex]` entries and select a tile containing numbered masks;
2. disable one layer at a time in the inspector and verify only the matching painted area disappears;
3. disable layer 0 and verify the relief falls back to its neutral material without hiding the mesh;
4. disable **Terrain paint** and verify all numbered layers disappear while relief/base stays visible;
5. re-enable **Terrain paint** and verify enabled layers return;
6. verify A8 masks show dimensions/format and the **validated** state;
7. use a different DDS format in a test map and verify **not rendered** is shown with no visual application;
8. navigate through enough tiles to exceed the mask cache and verify the editor remains stable and reloads evicted assets when needed;
9. click **Clear cache** and verify progressive repopulation without unloading the map;
10. verify no `.dds`, `.terrain`, `.rdy`, or `global.cfg` file is modified.

## DDS mask validation/coverage test

1. open a map with `tile.map.N.dds` files and select a painted tile;
2. verify the inspector reports resolution, coverage, and alpha range for each valid mask;
3. verify an empty A8 mask is reported as empty and creates no visual layer;
4. verify a fully opaque mask paints the whole layer without requiring an additional opacity texture;
5. test a truncated or unsupported DDS in a test map and verify it is marked invalid without preventing the map from opening;
6. hide base layer 0 using the visibility control and verify the base texture disappears while terrain relief remains;
7. re-enable layer 0 and verify immediate restoration;
8. switch tiles in 3×3 mode and verify empty masks do not enter the cache and only required masks are loaded.

## [groundtex] declared-resolution test

1. open a map with layers whose resolution is known;
2. verify code 8 expects a 256×256 mask and code 9 expects 512×512;
3. select a tile whose mask matches the expected dimension and verify normal rendering;
4. in a test map, temporarily replace a mask with another valid A8 DDS of a different dimension;
5. verify the inspector shows both the real and expected dimensions;
6. verify the incompatible layer is disabled and not rendered;
7. restore the correct mask and verify the layer becomes visible again without modifying `global.cfg`.


## Loading-lock test

1. open an OMSI installation and verify the animated overlay appears while it is being read;
2. open a map in full mode and verify the bar/percentage advances with tile progress;
3. while the overlay is visible, try the Explorer, Inspector, viewport, menus, and editing shortcuts and verify no edit is accepted;
4. verify progressive O3D preparation also keeps editing locked until the required visual structure is ready;
5. switch to 3×3 performance mode, change the active tile, and verify a temporary lock while the new region loads;
6. open empty Object/Spline libraries for the first time and verify the animation remains until the host returns the list;
7. trigger a read error in a test map and verify the lock disappears together with the error message;
8. verify F11 and leaving fullscreen remain available without enabling editing during the lock.


## Fullscreen, terrain, and spline test

1. enter F11 and verify the sidebar plus fixed Explorer/Inspector disappear;
2. verify the floating dock can select/move/rotate, create objects, create splines, open Explorer/Inspector, save, and control layers;
3. verify Explorer/Inspector open as drawers over the viewport and close without leaving fullscreen;
4. in Grundorf, wait for the blocking load to finish and verify the base terrain no longer appears black when its source file is BMP;
5. in the Inspector, verify a transcoded BMP texture is reported as PNG with BMP as its source;
6. in full-map mode, wait for the SLI-profile queue and verify spline surfaces appear across the whole map rather than only around the 3×3 area;
7. switch to performance mode and verify nearby profile limits remain active;
8. verify no OMSI BMP/SLI file is modified by preview rendering.


## GPU texture upload validation

1. open Grundorf and wait for the blocking load to finish;
2. verify the Inspector reports `gras.bmp` as `PNG (source BMP)`;
3. visually verify the base terrain texture is visible across the tiles rather than only the dark viewport background;
4. toggle Terrain off/on and verify the textured surface disappears and returns;
5. enable Spline profiles and verify real spline surfaces use their loaded textures;
6. if a texture is still missing, open the WebView2 console and look for `OMSI Map Studio: texture upload failed`, recording extension/MIME and error;
7. verify no OMSI file was modified during the test.


## Continuous loading, RGBA, and icon validation

1. open Grundorf in full-map mode and verify there is only one blocking structural loading screen, not a new modal for every O3D/SLI item;
2. verify O3D, SLI, and texture progress continues in the status bar in the background;
3. verify `gras.bmp` is still reported as a BMP source and terrain appears through the RGBA upload path;
4. verify BMP-based road/spline surfaces appear without depending on WebView PNG decoding;
5. check the `real O3D` counter: invalid geometry responses must be shown as failures rather than renderable models;
6. install through the EXE, launch from Start Menu, and verify the OMSI Map Studio icon in the window, taskbar, and shortcut;
7. verify no original OMSI file was modified.


## O3D long-header + bones regression

The `GeometryReader_LongHeaderBoneSection_UsesShortBoneCount` test creates a minimal extended-header O3D with long triangle indices and a bone section whose list count is UInt16. The reader must finish with renderable geometry and without `invalidBoneSection`.

During manual Grundorf validation, the map Inspector must report renderable O3D paths, failures, pending paths, and the main mesh error codes separately. SLI profiles must also report loaded profile files and the number of actual surfaces available for rendering.


## Single-loader, terrain, and icon validation

1. open Grundorf and verify that after tile loading a single “Preparing map resources” animation remains until O3D/SLI/textures stabilize;
2. verify the animation does not close and reopen for each item;
3. editing interactions must remain locked while that animation is active;
4. in the Inspector, verify `base texture upload: direct RGBA` and inspect the `average RGB` diagnostic;
5. visually verify `gras.bmp` is visible on terrain through the unlit emissive path;
6. verify textured spline surfaces are no longer dark merely because lighting is disabled;
7. install the new build through the EXE and launch both from Start Menu and directly from the executable; the OMSI Map Studio icon should appear in the window and taskbar.


## [tree] validation

1. open Grundorf in both 3×3 and full-map modes;
2. verify that `tree_medium_*.sco`, `Tree_Small_*.sco`, and other `[tree]` objects no longer appear only as yellow markers when their texture exists;
3. compare two placements of the same tree type with different stored heights/ratios and verify the viewport preserves the values written in the map `[object]`;
4. select a tree and verify the Inspector shows its texture and the range declared by `[tree]`;
5. verify the `treehelper.x` editor helper is not counted as an O3D failure for the tree;
6. verify yellow fallback markers remain only where the real asset could not be rendered.


## O3D, SLI, and lateral pan validation

1. open Grundorf in 3×3 mode and verify O3D objects no longer disappear because of embedded diffuse alpha or an unmatched LOD threshold;
2. inspect objects with multiple `[LOD]` blocks and verify at least one real LOD remains visible at every editor distance;
3. open a spline with more than two `[profilepnt]` entries and verify every cross-section strip is rendered, including road/sidewalk strips when declared;
4. focus the viewport and test `A/D` for left/right and `W/S` for forward/backward movement;
5. repeat with `Shift` for faster movement;
6. verify middle mouse and `Shift + right mouse` still pan without selecting objects.


## Loading-performance validation

1. open Grundorf in full-map mode and time from map selection until “Preparing map resources” finishes;
2. reopen during the same OMSI-root session and verify host caches are reused;
3. in performance 3×3 mode, verify the O3D counter can exceed 64 when the loaded region actually references more than 64 unique paths;
4. verify every SLI profile used by the region enters the queue, with no 48-path truncation;
5. verify BMP textures use direct RGBA upload without a second PNG payload for the same file;
6. inspect O3D objects containing transform section 0x79 and verify correct internal placement after applying the inverse transform;
7. verify heavy O3D parsing no longer freezes the UI thread.


## Structural-first loading validation

1. open Grundorf in **Full map** mode and time tile completion separately from the point where every texture finishes;
2. confirm that once tiles are consistent the viewport becomes usable while remaining O3D/SLI/textures continue progressing in the status bar;
3. confirm broad texture prefetch does not start before structural O3D and SLI paths have received responses;
4. reopen the map without changing the OMSI root and confirm reuse of parsed `.sco` and physical `.o3d` mesh caches;
5. use two `.sco` files that reference the same `.o3d` and confirm identical geometry without perceptible duplicate reading/parsing;
6. switch to 3×3 mode and confirm region changes still lock editing only while the new tiles are being read consistently;
7. confirm real errors such as `encrypted` and `unsupportedFormat` remain visible in diagnostics and are never replaced by fake geometry.


## O3D/DirectX diagnostics validation

1. open an object with an extended-header O3D containing bone section `0x54`; confirm the structure summary is valid and reports the correct count;
2. confirm an O3D whose key differs from `0xFFFFFFFF` still reports `encrypted`, with no fake geometry attempt;
3. open a `.sco` whose `[mesh]` points to `.x` and confirm `legacyDirectXMesh` in diagnostics;
4. use an unknown mesh extension and confirm it remains `unsupportedFormat`;
5. confirm `[tree]` objects using `treehelper.x` remain excluded from failure counts because they are rendered from their real tree definition.


## DirectX `.x` mesh validation

1. open a `.sco` whose `[mesh]` points to a `.x` beginning with `xof 0303txt 0032`;
2. confirm the object enters **Renderable meshes** and no longer reports `legacyDirectXMesh`;
3. validate a quad/polygon and confirm fan triangulation without holes;
4. validate UV + `MeshMaterialList` + `TextureFilename` and confirm the real texture in the viewport;
5. validate a file containing `FrameTransformMatrix` and confirm coherent local placement/orientation;
6. validate multiple `Mesh` blocks in one file;
7. use `bin`, `tzip`, and `bzip` variants and confirm `legacyDirectXUnsupportedEncoding` without crashes or fake fallback;
8. confirm the physical-file cache is reused for `.x` as well.


## SCO local transform validation

1. open a `.sco` with two `[mesh]` entries pointing to real geometry and give each a different `[new_pos]`; confirm their relative placement;
2. validate `[rot_x]/[rotx]`, `[rot_y]/[roty]`, and `[rot_z]/[rotz]` aliases;
3. test `[scale]` with one value and confirm uniform scale;
4. test `[scale]` with three values and confirm independent X/Y/Z scale;
5. confirm a mesh with no transform blocks keeps identity;
6. reuse the same physical `.o3d` or `.x` from two `.sco` files with different transforms and confirm geometry remains shared by the cache without mixing transforms.


## On-demand DDS mask validation

1. open Grundorf in **Full map** mode and confirm tile loading finishes without scanning every DDS mask pixel;
2. confirm each valid mask initially reports width/height with `hasPixelStatistics=false`;
3. enable terrain paint/a layer that uses the mask and confirm the asset is requested normally;
4. after the asset loads, confirm coverage, minimum alpha, and maximum alpha appear in diagnostics;
5. validate an empty mask and a fully opaque mask; both must keep their correct classification once the asset is loaded;
6. confirm invalid, truncated, or non-A8 DDS files remain rejected with no fake fallback.


## Parallelism and responsiveness validation

1. open Grundorf in **Full map** mode and confirm WPF/WebView remains responsive while loading;
2. confirm progress advances across multiple tiles rather than waiting for strict serial completion;
3. repeat under a runtime exposing few logical processors and confirm map opening can still use up to 8 tile reads;
4. reopen the same installation/map and confirm tile cache reuse;
5. confirm SLI profiles and SCO metadata return the same real data after moving parsing to workers;
6. validate terrain, RDY, and masks on a tile containing all sidecars and confirm results remain identical.


## O3D bone-section and loading-animation validation

1. open Grundorf and confirm the animation remains visible while O3D/.x meshes, SLI profiles, and textures are still warming up;
2. after structural loading, confirm the visual warmup animation does not block editor mouse/keyboard interaction;
3. in 3×3 performance mode, confirm the Inspector reports `renderable / total`, failures, and pending geometry for the active area;
4. validate an extended-header O3D containing section `0x54` with a `UInt32` bone count;
5. validate a legacy O3D with a `UInt16` bone count;
6. confirm a model with bones no longer becomes a yellow marker merely because section `0x54` was misaligned.


## Protected O3D validation

1. in Grundorf, confirm the Explorer reports **Real meshes** and does not count a protected payload as rendered;
2. confirm `encrypted` is presented as **Protected O3D** in the Inspector;
3. confirm the final status reports protected object-type and mesh counts;
4. confirm a protected O3D does not receive fake geometry or automatic substitution by another format;
5. confirm protected O3D markers use a different color from missing/invalid asset markers.


## Protected O3D Core validation

1. validate a v7 O3D with an extended header, protection key, and alternative seed;
2. confirm reading produces decoded positions, normals, and UVs without modifying the source file;
3. confirm triangles, materials, and transforms continue through the normal O3D pipeline;
4. in Grundorf, compare the **Real meshes** total with test.21: objects previously reported as `encrypted` should become renderable when they are inside the supported domain;
5. if a protected mesh exceeds the validated domain, confirm the explicit `protectedVertexCountUnsupported` error.


## Texture validation after O3D loading

1. open Grundorf in **Full map** mode and confirm all 21 tiles and real meshes still load;
2. compare the status bar with test.23: requested textures should be able to exceed the old 128-object-texture ceiling when the map requires more;
3. wait until **pending = 0** and confirm buildings/objects that were previously black or gray receive their textures when the files exist;
4. check the Inspector counters for loaded and failed textures;
5. if failures remain, record the codes shown under **Texture failures** to distinguish missing files from format/decoding issues.


## textureNotFound validation — test.25

1. open Grundorf in 3×3 mode and compare against test.24, which showed **177 loaded / 81 textureNotFound / 0 pending**;
2. confirm legacy references such as `*.bmp` can use a same-basename installed `*.dds`;
3. confirm textures stored in a parent pack's `Texture` directory are also resolved;
4. wait for pending = 0 and record the new `textureNotFound` total;
5. confirm **Auto requested** no longer appears as an impossible fraction after switching between Full map and 3×3.


## Tree helper validation — test.25

In Grundorf, confirm `[tree]` billboards still render while the large gray panels associated with tree helper meshes no longer appear over the map.
