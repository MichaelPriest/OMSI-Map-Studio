# OMSI Map Studio native migration

This track rebuilds the editor on the native Windows architecture without discarding the OMSI domain already implemented.

## Target stack

- .NET 10;
- WinUI 3 / Windows App SDK;
- Direct3D 11;
- Vortice.Windows;
- MapStudio.Core remains the authority for formats, reading, validation and persistence;
- SQLite/cache remain reusable;
- no WebView2 in the primary viewport;
- React does not control renderer lifetime.

The foundation uses Windows App SDK 2.5.1 and Vortice.Direct3D11 3.8.3.

## Strategy

Migration runs in parallel with the current editor.

### Phase N0 — foundation

- new `MapStudio.Renderer` project;
- new `MapStudio.Native` project;
- Direct3D 11 initialized in its own runtime;
- mouse input received directly by WinUI;
- stable picking ID registry;
- ID-buffer codec;
- dedicated Windows CI.

### Phase N1 — minimal OMSI viewport

- connect `SwapChainPanel` to a DXGI swap chain;
- perspective/top camera;
- Gundorf terrain;
- real O3D objects;
- splines;
- ID buffer;
- hover and selection;
- DPI validation at 100/125/150/200%.

### Phase N2 — editing

- move gizmo;
- rotate gizmo;
- snap;
- Inspector;
- save through MapStudio.Core;
- undo/redo.

### Phase N3 — complete interface

- native Explorer;
- libraries;
- previews;
- construction tools;
- terrain;
- real-map tools;
- diagnostics/Map Health;
- fullscreen;
- shortcuts.

### Phase N4 — replacement

The native version replaces the WebView2 host only after sufficient functional parity and real-world validation. Until then, the current application remains available for comparison.

## Selection

Native selection will not depend on object material, transparency or texture. Every OMSI entity receives a `PickingId`. A dedicated pass writes that ID into an integer render target. The pixel under the pointer directly identifies the selected entity.

This removes the fallback chain that became necessary in the WebView2/Babylon viewport.


### Checkpoint N0.1 — Direct3D presentation

The foundation now creates an `IDXGISwapChain1` for composition, associates it with the `SwapChainPanel` through `ISwapChainPanelNative`, creates the backbuffer/RTV and presents a real first Direct3D 11 frame.

Backbuffer sizing uses WinUI `CompositionScaleX/Y`, so the renderer works in physical pixels and reacts to DPI/window-size changes without CSS/WebView2.


### Checkpoint N0.2 — native OMSI session

The WinUI host now opens the real OMSI folder and a map folder using the native Windows picker. `MapStudio.Core` is called directly, without a WebView2 bridge, to discover maps, open `global.cfg`, choose the initial tile and load the real 3×3 region.

The native UI already displays real counts for loaded tiles, objects, splines and terrain grids. The next checkpoint converts this Core snapshot into GPU buffers.


### Checkpoint N1.1 — native GPU navigation

The native overview now has a viewport transform executed by the vertex shader. Mouse-wheel zoom and right/middle-button pan update only a Direct3D constant buffer; O3D geometry is not rebuilt on every movement.

The same transform is used by both the visible pass and the ID-buffer pass, keeping selection pixels aligned with objects after navigating the map.


### Checkpoint N1.2 — real OMSI terrain

The native renderer now converts each tile's real height grid into GPU triangles. The first visualization mode remains top-down, but it already uses real elevation data for color/depth and prepares the same mesh for the upcoming perspective camera.

SCO objects that do not use `[absheight]` also receive bilinear terrain interpolation before O3D transforms, preserving the existing editor rule.


### Checkpoint N1.3 — real depth for viewport and ID buffer

Both the visible viewport and the selection pass now have their own Direct3D depth buffer. Selection no longer depends on triangle submission order: when objects/proxies overlap, the picking pixel keeps the nearest surface according to depth.

The same depth rule is used by the visible frame and the ID buffer, bringing selector behavior closer to a native 3D editor.


### Checkpoint N1.4 — perspective 3D camera and world-space geometry

The native viewport no longer pre-projects terrain, splines, and O3D meshes into a 2D overview. GPU buffers now preserve real X/Y/Z world coordinates and the vertex shader receives a perspective `ViewProjection` matrix.

The camera frames the loaded OMSI region, uses the mouse wheel for dolly/zoom, the middle button for map-plane panning, and the right button for 3D orbiting. Resize and DPI changes recalculate projection without rebuilding geometry.

Terrain uses the OMSI elevation as the real Y axis. O3D objects keep SCO/O3D transforms and bilinear terrain placement before reaching the GPU. Splines and selection proxies are world-space as well.

The visible frame and ID Buffer use the exact same camera matrix and their independent depth buffers, so picking remains aligned in 3D perspective.


### Checkpoint N1.5 — real SLI profile splines

The native renderer now reads every `.sli` through `MapStudio.Core` and extrudes the real surfaces defined by `[profile]` / `[profilepnt]` along each spline's length and curvature.

The mesh uses the real profile width and height together with instance radius, rotation, and start/end gradients. Longitudinal elevation integrates the percentage gradient along the spline instead of flattening it onto terrain.

The height rule was also corrected: OMSI splines use their own absolute elevation, while terrain interpolation remains specific to relative scenery objects. The ID Buffer receives the same real spline mesh, keeping the proxy only as an auxiliary click area.

At this checkpoint the profile geometry is real; SLI texture application remains a following material refinement.


### Checkpoint N1.6 — blue hover and red selection with depth

The native viewport now mirrors the OMSI editor interaction behavior: an item under the pointer receives a blue highlight and the selected item receives a red highlight.

Hover and selection use the same `PickingId` as the ID Buffer and prefer real O3D or spline geometry; proxies remain only as a selection fallback. Hover is cleared when the pointer leaves the viewport or camera pan/orbit begins.

Because the viewport now has a real depth buffer, highlight copies receive a very small offset toward the camera. This keeps overlays visible without relying on draw order or disabling scene depth.


### Checkpoint N2.1 — native move and rotate gizmos

The editing phase has started in the Direct3D viewport. Selecting an object or spline now creates a real 3D gizmo at the entity insertion point, with dedicated handles in the same ID Buffer used by the rest of the scene.

**Move** exposes X/Y/Z and constrains each drag to the chosen axis. **Rotate** exposes X/Y/Z for SCO/O3D objects; splines use Y rotation only, matching the rotation field available in the OMSI map format.

During drag, the red selected geometry receives a preview transform without rebuilding the entire map for every pointer pixel. On release, the transform is applied to the snapshot entity and converted directly into an `OmsiObjectTransformEdit` or `OmsiSplineTransformEdit`, becoming a real edit pending Core persistence.

Gizmo handles use `PickingKind.Gizmo` and dedicated IDs, so they cannot collide with object or spline IDs even when drawn in front of the same geometry.


### Checkpoint N2.2 — transactional transform persistence

Transforms produced by the gizmos can now be accumulated by the native host and saved into the real OMSI map. The host deduplicates successive edits to the same entity and groups changes by tile before writing.

Persistence reuses `OmsiTileObjectEditor` and `OmsiTileSplineEditor`; no parallel format is introduced. Every modified tile is processed through `SafeFileTransaction`, which creates a backup under `.mapstudio-backups`, stages a temporary file, and performs atomic replacement with rollback on failure.

After a successful write, affected tiles are read again through `MapStudio.Core` and pending state is cleared. The WinUI interface exposes **Save changes** only while transforms are pending.


### Checkpoint N2.3 — native undo/redo and snapping

The runtime keeps transform history as before/after pairs using the same `OmsiObjectTransformEdit` and `OmsiSplineTransformEdit` types used for persistence. **Undo** and **Redo** reapply these states to the snapshot, rebuild the required renderer state, and stage the resulting version again for safe persistence.

Snapping can be toggled from the native UI. The initial configuration quantizes movement to **0.25 m** and rotation to **5°**. The snapped value drives the red preview, the gizmo, and the final OMSI edit so the saved result matches what was displayed during dragging.


### Checkpoint N2.4 — native Inspector bound to real selection state

The WinUI Inspector now consumes the selected entity state directly from the renderer. For objects it shows ID, tile, SCO path, OMSI coordinates, rotation, pitch, and bank. For splines it shows the SLI path, OMSI coordinates, rotation, length, radius, and start/end gradients.

The Inspector refreshes after ID-buffer selection, gizmo move/rotation, and undo/redo operations. There is no duplicate XAML-side model: displayed values come from the same native snapshot that produces geometry and persisted edits.


### Checkpoint N2.5 — numeric editing through the Inspector

The native Inspector is no longer read-only and can edit the selected entity transform. Objects expose X/Y/Z, rotation, pitch, and bank. Splines expose X/Y/Z, rotation, length, radius, and start/end gradients.

Pressing **Apply values** updates the same snapshot used by the viewport, creates a before/after history pair, refreshes native geometry, stages the OMSI transform for persistence, and remains compatible with Undo/Redo and Save changes.

Gizmo and Inspector are therefore two interfaces over the same native editing model, with no duplicated state.


### Checkpoint N3.1 — native Explorer with selection and focus

N3 starts with a WinUI Explorer fed directly by the renderer snapshot. Real objects and splines are listed with type, ID, asset path, and tile, without a parallel inventory model.

Search filters the display name, asset path, and tile coordinates. Selecting an Explorer item uses the same scene `PickingId` and updates the red highlight, gizmo, and Inspector. Double-clicking also repositions the 3D camera over the entity insertion point.

Selections made directly in the viewport are synchronized back into the list and scrolled into view, keeping Explorer and viewport as two views over the same native state.


### Checkpoint N3.2 — native library backed by the existing SQLite index

The WinUI asset library reuses `OmsiAssetIndex` from `MapStudio.Core`. Its SQLite database is stored in the user-local cache and separated per OMSI installation, avoiding a full installation scan every time the editor starts.

The interface switches between **Scene** and **Library**, can filter all assets or only SCO, SLI, models, and textures, and searches by relative path. The **Refresh** action runs the incremental index refresh and reports real examined-file and candidate counts.

When a library asset is already used in the loaded region, double-clicking finds its first scene usage and focuses the 3D camera. Assets not yet used remain available in the catalog for the following native preview/placement stage.


### Checkpoint N3.3 — native 3D library preview

The indexed library now has real visual preview support for **SCO/O3D objects** and **SLI splines**. Selecting a compatible asset loads the actual OMSI installation file directly, without creating a fake map entity and without replacing the currently open map snapshot.

For SCO assets, the preview uses the real O3D/X meshes, SCO-declared transforms, LOD selection, and diffuse material colors. For SLI assets, the renderer extrudes the real `[profile]/[profilepnt]` profile into a navigable 3D sample.

The viewport temporarily enters preview mode and frames the asset bounds. Orbit, pan, and zoom keep using the same Direct3D camera. Returning to **Scene** restores the map buffers from the in-memory snapshot and already loaded assets, without reopening the whole map.

Standalone models and textures remain available in the index, but this checkpoint intentionally limits visual preview to SCO/O3D and SLI; those categories will expand together with materials/textures and placement.


### Checkpoint N3.4 — native SCO object placement

The library can now start placement of a SCO object directly on the open map. The viewport restores the scene, loads the asset's real geometry, and shows a separate **blue 3D ghost** outside the ID Buffer, so the preview cannot be mistaken for an existing entity.

The pointer is converted into a perspective-camera world ray. Intersection is refined against the loaded terrain's real height and, when snapping is enabled, X/Z are quantized to 0.25 m before the final height sample. Placement is accepted only inside an actually loaded tile.

On click, the host converts the world position into OMSI tile-local coordinates and uses `OmsiTileObjectInserter` to append a real `[object]` section. The next ID is computed across objects and splines from every map tile to avoid collisions. When the same asset already exists in the map, its header and extra values are preserved as a template; tree assets without a template use the real texture/height/aspect metadata declared by the SCO.

Insertion is persisted immediately through `SafeFileTransaction`, with a backup under `.mapstudio-backups`, and the changed tile is read back through Core before the viewport is refreshed. Splines remain outside this checkpoint because correct spline placement needs a point/curve construction tool rather than treating them as ordinary objects.


### Checkpoint N3.5 — native spline construction with points and curves

The SLI library can now start a construction tool directly in the Direct3D viewport. The selected asset uses the real `.sli` profile to render a 3D ghost before anything is written.

Two workflows are available:

- **Straight:** the first click defines the start and the second click defines the end.
- **Curve:** the first click defines the start, the second locks the end, and a third point controls curvature. The editor solves the circle through all three points and converts it into the actual OMSI fields: initial rotation, arc length, and signed radius.

Points come from the perspective camera raycast against the real terrain height. The 0.25 m snap is also applied during construction. Height difference between start and end is converted into start/end percentage gradient, allowing the spline to follow elevation instead of being flattened.

Insertion uses `OmsiTileSplineInserter`. The new ID is calculated globally across objects and splines from all map tiles. When a compatible spline exists, its header and extra values are reused; otherwise Core looks for a neutral normal-spline template. Writing goes through `SafeFileTransaction`, creates a backup, and reloads the modified tile before refreshing the scene.

This checkpoint establishes the foundation for a city-editor-style road tool. Next refinements are segment continuity, editable post-placement handles, snapping to existing endpoints, and sequential construction without leaving the tool.


### Checkpoint N3.6 — snapping to spline endpoints

The construction tool snap now recognizes the actual endpoints of already loaded splines. While creating a new road/spline, the pointer position still comes from terrain intersection and the 0.25 m grid, but it also searches existing spline starts and ends within a camera-distance-aware tolerance.

When an endpoint is found, the new point uses that endpoint's exact X/Z position and Y height. This prevents small gaps and vertical mismatches when starting or ending a segment near an existing road.

The final endpoint is calculated from the spline's real parametric geometry through NativeSplinePathMath, so curved and graded segments are supported as well.


### Checkpoint N3.7 — sequential spline construction

The SLI tool now has a **Continue segments** mode, enabled by default in the library. After a segment is persisted and the tile is reloaded, the viewport restarts the same tool and uses the previous segment's exact `EndWorld` as the next segment start.

This removes the need to return to the library and click the joint again. In straight mode, each following segment only needs its new endpoint. In curve mode, the start is already fixed and the user defines the new end plus the curvature control point.

This checkpoint guarantees geometric position and height continuity. Automatic `PreviousSplineId` / `NextSplineId` rewriting remains a separate Core refinement because the current editor validates those links but does not rewrite them yet.


### Checkpoint N3.8 — logical Previous/Next chaining

Sequential construction now also preserves the logical links used by the OMSI format. The next segment request carries the previous spline ID. During insertion, the new `[spline]` section receives that value as `PreviousSplineId`, while the previous spline is updated to point to the new ID through `NextSplineId`.

Core now has a dedicated spline-link editor. It validates ordinal, path, ID, and the original Previous/Next values before writing, preventing silent edits when a source file changed after it was read.

When both segments live in the same tile, insertion and link update are combined in the same document. When they span different tiles, both files are included in the same `SafeFileTransaction`; each receives a backup and the operation is handled as one transactional write.

This means continuous construction no longer creates only geometrically touching segments: the sequence is also linked through the OMSI map IDs.


### Checkpoint N3.9 — native fullscreen and shortcuts

The WinUI host now provides real Windows fullscreen through `AppWindowPresenterKind.FullScreen`, toggled with **F11**. **Esc** exits fullscreen and also immediately cancels an active placement/construction tool before affecting the window state.

Core editor shortcuts now live directly in the native host:

- **W** activates the Move gizmo;
- **E** activates the Rotate gizmo;
- **Ctrl+S** saves pending transforms through the same safe backup workflow;
- **Ctrl+Z** undoes the last transform;
- **Ctrl+Y** redoes the transform;
- **F11** toggles fullscreen;
- **Esc** cancels placement/construction or exits fullscreen.

W/E and Ctrl+Z/Ctrl+Y do not intercept keys while focus is inside a `TextBox`, `RichEditBox`, `PasswordBox`, or `NumberBox`, preserving typing and Inspector field editing. Focus is resolved through `FocusManager` using the window `XamlRoot`.


### Checkpoint N3.10 — resizable and collapsible native panels

The WinUI workspace no longer relies on rigid widths for Explorer and Inspector. Two native separators between the side panels and the viewport let users resize them by dragging without affecting the Direct3D surface architecture.

Explorer can range from 220 to 520 px and Inspector from 240 to 560 px, with additional limits that preserve a useful minimum viewport area. The latest widths are kept in memory when a panel is collapsed.

The **View** menu can now toggle Explorer and Inspector independently. Collapsing sets both the panel and splitter column to zero width; restoring brings the panel back at its last used width. The `SwapChainPanel` continues reacting to `SizeChanged`, so the Direct3D backbuffer immediately follows the newly available space.

### Checkpoint N3.11 — safe native object and spline deletion

Entity deletion from the React editor has been migrated into the WinUI host. The Inspector now exposes **Delete selected**, and the **Delete** key triggers the same flow whenever focus is not inside a text input.

For objects, the host validates tile, ID, SCO path, and source section ordinal before removing the `[object]` section through `OmsiTileObjectDeleter`.

For splines, the operation reads map-wide IDs, rejects duplicate spline IDs, and uses `OmsiSplineLinkPlanner` to safely release reciprocal Previous/Next links on neighboring splines before removing the selected spline section. Changes spanning multiple tiles are committed in a single `SafeFileTransaction`.

Deletion is blocked while transforms are pending or while a placement/construction tool is active. The UI asks for confirmation before writing and states that a backup will be created under `.mapstudio-backups`.

After completion, affected loaded tiles are read back through Core so the viewport, Explorer, and Inspector return to the real persisted map state.

### Checkpoint N3.12 — native object copy with real placement

The React editor's **Place copy** flow has been migrated into the WinUI Inspector. When a real object is selected, the **Place copy** button and **Ctrl+D** start a new placement using the same SCO file.

The copy initially preserves the selected object's real transform fields: **Z, rotation, pitch, and bank**. The next terrain click defines the new horizontal X/Y position. For relative objects, the ghost adds the preserved Z offset to the real terrain height; for absolute-height objects it keeps the selected absolute Z.

The 3D ghost uses the same rotation/pitch/bank that will be persisted, avoiding a mismatch between preview and saved output. The new instance still receives a free global ID and is written through the existing `OmsiTileObjectInserter` + `SafeFileTransaction` flow with automatic backup.

Ctrl+D does not intercept typing while focus is inside an editable field, and the flow remains disabled for `worldcoordinates` maps until the dedicated georeferencing migration is implemented.

### Checkpoint N3.13 — disconnected spline copy and native link editor

The WinUI Inspector now migrates two more real-spline workflows from the React editor.

**Place copy** / **Ctrl+D** also works when a spline is selected. The tool reuses the same real SLI as its template, automatically starts straight or curved construction based on the selected radius, and creates the new spline disconnected with Previous/Next initialized to `-1`. Height splines remain blocked until the dedicated `[spline_h]` flow is migrated.

The spline Inspector now also exposes **Previous ID** and **Next ID**. **Save links** runs `OmsiSplineLinkPlanner` against map-wide IDs, validates reciprocal links and endpoint availability, then applies every required change through `OmsiTileSplineLinkEditor`.

When a change touches splines in different tiles, every affected file participates in the same `SafeFileTransaction` and receives a backup. After writing, affected loaded tiles are read back and the edited spline is reselected in the viewport/Explorer with its new links.

### Checkpoint N3.14 — terrain leveling in the native host

The first real terrain editing tool from the React editor has been migrated into the WinUI Inspector.

The **Terrain** section can activate **Pick point on map**. The next viewport click casts the Direct3D camera ray against the real terrain height and resolves the tile, local coordinates, and current elevation. The Inspector automatically initializes the target height with the sampled value.

The user can then edit **target height**, **brush radius**, and **feather**. **Apply leveling** runs `OmsiTerrainLeveler.LevelCircularBrush` against the real `.terrain` file and writes the result through `SafeFileTransaction`, creating a backup before replacement.

After writing, the tile is read back through `MapStudio.Core` and the viewport is rebuilt using the new topography. The operation is blocked while object/spline transforms are pending so two different edit transactions are not mixed.

This checkpoint covers circular leveling. Raise/lower dragging, terrain texture painting, tile creation/removal, and DEM import are still pending.

### Checkpoint N3.15 — full map, 3×3 performance mode, and tile navigator

The WinUI host now migrates both map loading modes from the React editor. **Full map** is the native architecture default and loads every map tile; **3×3 performance mode** keeps only the radius-1 region around the active tile.

Full-map reading is capped at six concurrent tile loads so large maps do not launch hundreds of file reads/parses at once.

The **Map** menu can switch between both modes without reopening the map. Switching is blocked while transforms are pending so unsaved edits are not discarded.

The Explorer gains a tile navigator with X/Y fields, a **Go** action, and four directional controls. In full-map mode, navigation only changes the active tile and focuses the camera on its center. In 3×3 mode, navigation reloads the region around the new tile and rebuilds the viewport with that area's real objects, splines, and terrain.

After any reload, the Inspector, visual history, and terrain tool are synchronized with the new real Core snapshot.

### Checkpoint N3.16 — O3D diffuse textures in Direct3D 11

The native renderer no longer treats every O3D mesh only as a diffuse color. The vertex pipeline now preserves real model UV coordinates and groups triangles into material/texture batches.

`NativeSceneryAssetLoader` resolves real texture files declared by O3D materials through `OmsiTextureAssetPathResolver`, including the Core's safe DDS replacement lookup.

Direct3D 11 loads textures that Windows Imaging Component can decode, creates `ID3D11Texture2D`/Shader Resource Views, and uses a textured pixel shader with a linear-wrap sampler. Low alpha is clipped in the shader so basic fence, vegetation, and masked-material cutouts are no longer rendered as fully opaque blocks.

Missing or undecodable textures do not break the map: the batch falls back to the O3D diffuse color. The cache keeps only textures requested by the current scene and caps the first pass at 256 distinct paths to avoid unbounded memory use on large maps.

This checkpoint covers base diffuse textures for O3D objects. Advanced SCO overrides (`[matl]`, night maps, bump/environment/light maps), spline textures, and terrain paint remain for later checkpoints.

### Checkpoint N3.17 — real SLI spline textures

The native renderer now preserves the texture parameters declared by `[profilepnt]` and applies the real textures used by SLI surfaces.

Each `NativeSplineAsset` resolves texture files through `OmsiTextureAssetPathResolver.TryResolveSplineTexture`. During tessellation, horizontal UV comes from `TextureX` and longitudinal UV follows `distance × TextureScale`, matching the OMSI spline format's longitudinal tiling semantics.

Surfaces are grouped into material/texture batches and use the same WIC/Direct3D 11 cache introduced for O3D objects. If a texture is missing or cannot be decoded, the spline keeps rendering through the untextured fallback instead of disappearing or aborting map loading.

This allows roads, sidewalks, curbs, and other SLI surfaces to stop relying on the generic gray color in the native viewport.

### Checkpoint N3.18 — real base terrain texture

The native renderer now uses the first real `[groundtex]` entry from `global.cfg` as the terrain base texture.

`NativeTerrainTriangleGeometryBuilder` receives the real map descriptor and OMSI root, resolves `MainTexturePath` through `OmsiTextureAssetPathResolver.TryResolveGroundTexture`, and generates per-tile UVs using the declared `MainTextureRepeating` value.

The texture is sent through the same WIC/Direct3D 11 cache already used by O3D and SLI rendering. If the file is missing or cannot be resolved, terrain keeps the height-color fallback instead of failing.

Mapping restarts on each tile and spans the 0–300 m tile area using the map-defined repeat value, preserving the OMSI scale.

This checkpoint covers the base layer. Additional `tile_*.map.N.dds` paint masks, secondary detail texture, and paint editing remain for the next step.

### Checkpoint N3.19 — painted terrain layers with DDS masks

The native renderer now also applies terrain layers painted by OMSI through `tile_*.map.N.dds` files.

Each valid mask uses its `LayerIndex` to select the matching `[groundtex]` entry from `global.cfg`. The layer's main texture keeps its own repeat value while the mask uses normalized 0–1 UV coordinates across the 300 m tile.

Native vertices now carry two UV sets: one for the repeated material texture and one dedicated to the tile mask. The `PSTerrainLayer` shader combines the layer texture with mask alpha and alpha-blends it over the base terrain.

OMSI A8 DDS masks are read directly by the renderer and expanded into an RGBA GPU texture, avoiding dependence on variable WIC support for that format. An invalid mask or texture simply skips that layer.

To avoid z-fighting between base and overlays without changing map data, each overlay receives only a renderer-side millimeter-scale height offset. No persisted `.terrain` elevation is changed.

Secondary `[groundtex]` detail textures and mask painting/editing are still pending.

### Checkpoint N3.20 — native OMSI sky and day/night preview

The WinUI host now renders the real OMSI sky in Direct3D 11 instead of relying only on the viewport clear color.

The runtime looks for `Texture\himmel01.bmp` for daytime preview and `Texture\himmel05.bmp` for nighttime preview. When those files are absent, it keeps compatibility with the React implementation and falls back to `Texture\skybox\day01.bmp` or `night01.bmp`.

The sky is rendered on a textured sphere centered on the camera. Its geometry stays outside the ID Buffer and uses a no-depth-write state, so it does not affect selection and cannot occlude distant objects, splines, or terrain.

Horizontal mapping mirrors the React viewport orientation while vertical sampling is clamped at the texture edge to avoid pole seams.

The native host top bar now exposes a **Night** option that switches between daytime and nighttime sky immediately without reloading the map.

### Checkpoint N3.21 — SCO nightmap/lightmap overrides in the native renderer

The native pipeline now loads SCO material overrides for each mesh and material index.

`NativeSceneryAssetLoader` uses the `MaterialOverrides` already parsed by `MapStudio.Core` and resolves `[matl_nightmap]` and `[matl_lightmap]` through the same safe scenery texture resolver used by normal O3D materials.

Material batches now preserve the base texture, night map, light map, and the `matl_alpha` value for following stages. Day mode keeps the current diffuse rendering behavior. When **Night** is enabled, the renderer selects the night map first and falls back to the light map when no night map exists.

The night shader combines the base texture with the secondary texture without changing map files. The same **Night** control that swaps the sky can therefore also preview façades, signs, and other surfaces that provide nighttime material maps.

This checkpoint does not yet change depth state or blending for `matl_alpha`, `matl_noZwrite`, and `matl_noZcheck`; bump maps and environment maps remain for a separate checkpoint.

### Checkpoint N3.22 — native matl_alpha and noZ states

The native renderer now honors all three SCO alpha modes:

- `matl_alpha 0`: opaque material; texture alpha does not cut holes in geometry.
- `matl_alpha 1`: binary alpha cutout; pixels below the alpha threshold are discarded and remaining pixels stay opaque.
- `matl_alpha 2`: partial transparency with alpha blending.

The same modes are also applied when a material has a night map/light map, through equivalent nighttime shaders.

`matl_noZwrite` now uses a read-only depth state, keeping scene depth testing without writing depth. `matl_noZcheck` disables depth testing for that material batch. After each batch, the renderer restores default states so terrain, splines, and other objects are not affected.

Flags are loaded directly from Core `MaterialOverrides` and remain associated with the correct material in each mesh.

### Checkpoint N3.23 — native terrain mask painting

The WinUI host now edits the same A8 DDS masks OMSI uses to paint additional `[groundtex]` layers.

`MapStudio.Core` now provides alpha-pixel reading, valid A8 DDS writing, and a circular brush with radius, target alpha, and feather. The brush works in the tile's real 0–300 m local coordinates and interpolates existing alpha toward the requested value, allowing a layer to be painted or erased.

In the Inspector, the same point selected by the terrain tool can be used with **Paint texture at point**. The user chooses the `groundtex` layer index, alpha from 0–255, radius, and feather. Layer 0 remains the base texture; painting only targets layers 1 and above.

When `tile_*.map.N.dds` already exists, it is replaced through `SafeFileTransaction` with an automatic backup. When the layer has no mask yet, the editor creates a new DDS using the mask resolution declared by the `[groundtex]` `ResolutionCode` and publishes it atomically through a temporary file + rename.

After writing, the tile is read back through Core and the Direct3D viewport immediately rebuilds the painted layers. The UI never supplies an arbitrary output path: mask name and destination are derived from the real tile and validated layer.

### Checkpoint N3.24 — complete [groundtex] detail texture support

The native terrain material now also uses each `[groundtex]` entry's `DetailTexturePath` and `DetailTextureRepeating`.

The Direct3D vertex now carries a third dedicated UV set so it can preserve at the same time:
- main terrain texture UVs;
- normalized 0–1 DDS mask UVs;
- independent detail texture UVs.

Both main and detail textures are resolved through the same `OmsiTextureAssetPathResolver`. Base terrain and painted layers can therefore use different repeat counts exactly as declared by `global.cfg`.

The `PSTerrainBaseDetail` and `PSTerrainLayerDetail` shaders modulate detail over the main material without changing paint-mask alpha. If a detail texture is missing or cannot be decoded, rendering falls back to the previous non-detail path.

The GPU texture cache now also tracks detail texture files and releases them when the map/scene changes.

### Checkpoint N3.25 — Top/Perspective camera in the native host

The camera mode switch already present in the React editor has been migrated into the WinUI host's **View** menu.

**Top view** preserves the current target and camera distance while moving pitch close to 90°, with a small safety margin that avoids the `CreateLookAt` singularity caused by a direction exactly parallel to the Y axis.

**Perspective** restores the editor's default orientation without changing the focused point or current zoom. This makes it possible to work on the same tile/object while switching between a near-orthographic overhead inspection and 3D navigation.

Both modes reuse the same camera, ID Buffer, and raycasts; there is no parallel viewport or hidden React state.

### Checkpoint N3.26 — real Terrain, Objects, and Splines visibility

The React editor's scene visibility controls have been migrated into the WinUI host's **View** menu.

**Terrain**, **Objects**, and **Splines** can now be shown or hidden independently without unloading the map or rebuilding the Core snapshot.

Visibility also controls the ID Buffer. When Objects or Splines are hidden:
- their geometry is no longer drawn;
- proxies and real triangles from that category are removed from the picking buffer;
- hover and selection for that category are cleared;
- gizmos do not remain active on an invisible entity;
- Explorer selection is rejected while the category is hidden.

This prevents an invisible entity from still blocking clicks on visible geometry behind it.

Terrain does not participate in the object/spline ID Buffer; hiding it affects rendering only. Terrain editing tools continue to use mathematical raycasts against the real loaded terrain mesh.

### Checkpoint N3.27 — native category selection filter

The React editor's selection filter has been migrated into the WinUI host's top toolbar.

The user can switch between:
- **Select: all**;
- **Objects only**;
- **Splines only**.

The filter does not change scene visibility. It rebuilds only the ID Buffer used by hover/click, removing excluded categories. For example, in **Splines only** a visible object in front no longer blocks selecting a spline behind it.

The current selection, hover, and gizmos are cleared when they are no longer valid for the new filter. Explorer selection follows the same filter so the list and viewport cannot enter different selection states.

Terrain mode remains separate because native terrain editing uses the dedicated leveling/painting raycast rather than the object/spline ID Buffer.

### Checkpoint N3.28 — grid independent from scene guides

The React editor's **Grid** control has been migrated into the WinUI host's **View** menu.

The renderer's previous combined line buffer has been split into three sets:
- tile borders / terrain subdivision grid;
- object guide markers;
- spline guide lines.

As a result, disabling **Grid** hides only the terrain reference mesh. Object markers and spline guides continue to follow their own category visibility.

The change does not rebuild the map or alter picking, materials, or OMSI geometry; it only enables/disables the Direct3D 11 grid line buffer.

### Checkpoint N3.29 — view shortcut and selection-focus parity

The native toolbar now migrates the quick shortcuts used by the React version:

- **F** focuses the camera on the selected object or spline;
- **N** toggles transform snapping;
- **T** toggles terrain visibility;
- **G** toggles only the grid;
- **O** toggles objects;
- **L** toggles splines;
- **P** toggles only the real 3D spline profiles.

Focus uses the native ID-buffer selection and the same anchor used by gizmos, without creating a second camera or parallel state.

**Real spline profiles** is now separate from logical spline visibility. Turning P off hides the textured SLI mesh while spline guide lines remain visible and selectable as long as Splines itself is enabled. This more closely matches the React viewport toolbar workflow.

Shortcuts do not intercept typing when focus is inside TextBox, RichEditBox, PasswordBox, or NumberBox controls.

### Checkpoint N3.30 — Q/Alt+1…4 selection modes and fit-map

The React editor's quick selection modes have been migrated to the WinUI host:

- **Q** returns to general selection;
- **Alt+1** selects all categories;
- **Alt+2** filters objects only;
- **Alt+3** filters splines only;
- **Alt+4** activates terrain/tile selection;
- **Home** frames the loaded map.

The native selection filter now includes a `Terrain` mode. In this mode objects, splines, and gizmos are removed from the picking ID Buffer and clicks use the real terrain-mesh raycast instead. The mode remains active after each click until the user changes filters.

Entering Terrain automatically makes the terrain layer visible if it had been hidden. The selected point feeds the same Inspector already used by leveling and DDS-mask painting, so selection and editing share the same real state.

The **Fit** button and Home key use `NativeViewportNavigation.FitToScene`, keeping a single camera and recalculating framing from the real loaded-scene bounds.

