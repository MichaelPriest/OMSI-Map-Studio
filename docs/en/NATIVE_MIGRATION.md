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
