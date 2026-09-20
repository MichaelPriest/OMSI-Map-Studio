# User interface

**English** · [Português (Brasil)](../pt-BR/UI.md)

The Alpha.3 primary interface follows the project's approved visual concept: persistent side navigation, dedicated installation/map-opening screens, and a three-area editor layout.

## Navigation

The sidebar contains:

- Home;
- Open OMSI;
- Open map;
- Explorer;
- Tools;
- Settings.

Items that are not implemented yet may exist as visual structure, but they must not expose fake data or fake actions.

## Flow

1. **Open OMSI** registers the installation root.
2. **Open map** lets the user manually choose a directory inside `maps`.
3. After the map opens, the interface enters the **Editor**.

No map is loaded automatically when selecting the installation.

## Editor

The editor is divided into:

- **Explorer** on the left, using real object, spline and tile counts;
- **Viewport** in the center, rendered with Babylon.js;
- **Inspector** on the right, showing the open map or selected object;
- **Status bar** with real counts from the open map.

Unsupported categories such as terrain and routes are clearly marked as “in development”.

## Object inspector

When an object is selected, the inspector provides tabs for:

- General;
- Transform;
- Geometry;
- Materials.

All displayed information comes from real OMSI files. Material information uses real O3D data already interpreted by Core.

## Visual rule

The UI may follow the approved concept, but it must never invent thumbnails, maps, counts or states to look complete. Unsupported states must remain empty, disabled or explicitly marked as in development.


## Viewport base surface

While the binary `.terrain` format is not interpreted yet, existing tiles receive a neutral editor base surface. It is only spatial guidance and does not represent real terrain elevation or texture.

Missing tiles remain unfilled and highlighted separately. Spline axes and object markers are rendered above this surface.


## 3×3 performance mode

When **3×3 performance mode** is enabled, the editor works with an **active tile** and a default 3×3 window around it. The full map topology still comes from `global.cfg`, but objects, splines and other heavy data are loaded only for that region.

Clicking another visible tile changes the active-region center. Tiles already read remain cached for the map session, so returning to a previous area does not require another disk read.

Object and spline counts shown in the explorer/status are explicitly active-region counts, not whole-map totals.


## Spline selection and inspection

Blue spline axes in the active region are clickable. When a spline is selected:

- the selected axis gets a different highlight;
- the inspector shows the `.sli` file, ID, chain links, tile, position, rotation, length, radius and gradients;
- the `.sli` is read only at that point;
- the **Profile** tab shows declared textures and recognized surfaces;
- when valid `[profilepnt]` pairs exist, the viewport extrudes the spline's real surface along its path.

Geometry uses a neutral material in this alpha. The real texture name is shown in the inspector, but the image is not applied yet.


## Map loading modes

The editor now defaults to **Full map**, matching the expected behavior of the standard OMSI editor:

- every declared tile is read;
- all placed objects remain available;
- all placed splines remain available;
- navigation does not discard elements just because they moved outside a 3×3 window;
- loading uses bounded concurrency and per-tile caching;
- the UI shows progress by tile count.

The previous 3×3 streaming behavior remains available as **3×3 performance mode**. It is optional and intended for very large maps or lower-memory computers.

Every newly opened map starts in **Full map** mode.


## Visual identity

OMSI Map Studio has its own application icon: a dark-navy map grid with a stylized orange/blue route. The same symbol is used by the executable, the desktop window and the UI header. The project does not reuse the official OMSI brand or application icon.


## Real objects in Full map mode

After all map tiles are read, the UI identifies unique `.sco` paths used by placed objects and loads each O3D geometry only once.

Progress is shown as **loaded O3D models / unique models**. While a model has not been read yet, its placements remain visible as markers. As soon as geometry arrives, every object using that same model is rendered.

Babylon reuses geometry and materials per model and creates transformed clones/instances for each map placement. This avoids duplicating vertex buffers for hundreds of identical objects.


## Essential editor tools

The active toolbar currently provides:

- **Select (Q)** — selects objects and splines;
- **Move (W)** — enables the position gizmo for the selected object;
- **Rotate (E)** — enables the rotation gizmo for the selected object;
- **Focus selection (F)** — centers the camera on the selected object or spline;
- **Frame map (Home)** — returns the camera to the full-map framing;
- **Grid (G)** — toggles tile surfaces/bounds;
- **Objects (O)** — toggles objects;
- **Splines (L)** — toggles splines.

Move and rotate start as **temporary in-memory previews**. The inspector reflects the new values and the UI shows **Unsaved preview** while changes are pending.

The **Save** button (or `Ctrl+S`) persists only those object transforms. Before replacing any tile, the host creates a copy under `.mapstudio-backups/<timestamp>/` inside the map directory. The ↶ button discards all unsaved temporary transforms.

Scale remains disabled at this stage because standard OMSI placed objects do not expose a general scale field equivalent to the position/rotation fields used by the editor.


## Safe transform saving

Saving at this stage is intentionally restricted to existing `[object]` entries.

For every changed object, the editor keeps the original section identity inside its tile. On save:

1. the host reopens the current `.map` file directly from disk;
2. it verifies that the section, object ID and `.sco` path still match the edited object;
3. only X, Y, Z, rotation, pitch and bank lines are changed;
4. comments, unknown sections, extra values, encoding, BOM and newline style are preserved;
5. backups are created for every affected tile;
6. only then are files atomically replaced;
7. tile caches are invalidated and saved state is reloaded.

If the object's source identity changed since the map was opened, the batch is cancelled as a conflict instead of overwriting the file.


### History and numeric editing

Temporary transforms keep up to 100 history actions:

- `Ctrl+Z` undoes the latest transform;
- `Ctrl+Y` or `Ctrl+Shift+Z` redoes it;
- ✕ discards all pending previews;
- after a confirmed Save, temporary history is cleared.

In the inspector's **Transform** tab, X, Y, Z, rotation, pitch and bank can also be entered directly. The value becomes a preview when Enter is pressed or the field loses focus, using the same undo/redo history as gizmo edits.


## Camera navigation and snapping

The viewport provides two quick camera modes:

- **Perspective (1)**;
- **Top (2)**.

The **Snap (N)** button toggles transform snapping. Defaults are:

- movement: **0.5 m**;
- rotation: **5°**.

Both values can be changed directly in the viewport toolbar and are applied to Babylon gizmos.

## Placed-object search

Explorer provides real search over loaded placed objects. An instance can be found by:

- `.sco` filename;
- full path;
- object ID;
- tile coordinate (`x,y`).

The list renders at most 250 rows at once to keep the UI responsive. Clicking an entry selects the object and focuses the camera. Objects with pending transforms are marked as **changed**.

## Object library

The **Library** tab lists real `.sco` files found under `OMSI 2/Sceneryobjects`.

Scanning:

- starts only when the Library tab is opened;
- skips reparse points;
- ignores inaccessible directories;
- runs off the main UI thread;
- is cached while the same OMSI installation remains selected;
- has a safety limit of 50,000 entries;
- renders at most 300 results at once in the UI.

Search accepts file name and path. The **Place** action uses the preservation-safe insertion flow and only allows confirmation when the `[object]` block parameters can be derived safely.


## Placing objects from the Library

Library now provides a **Place** action.

Flow:

1. open **Library**;
2. choose a real `.sco` file;
3. click **Place**;
4. click an existing tile in the viewport;
5. X/Y are calculated in tile-local coordinates and respect snapping;
6. adjust **Z**, **Rotation**, **Pitch** and **Bank** in the placement bar;
7. confirm with **Confirm and save**.

The preview uses real O3D geometry when available. If O3D geometry cannot be interpreted, the placement point remains visible as a marker.

### Conservative alpha limitation

A new `[object]` can only be persisted when that same `.sco` already exists somewhere in the map.

This is intentional: `[object]` blocks may contain a variable number of extra values depending on the object type. Map Studio copies the `HeaderValue` and extra values from a real instance of the same `.sco` instead of inventing parameters.

An installed `.sco` that has never been used in the map can still be selected and previewed, but **Confirm and save** remains blocked in Full map mode. In performance mode, the host performs the global verification on confirmation.

The target tile is automatically backed up before the new object is written.


## Safe copy of the selected object

In the inspector's **General** tab, **Place copy** starts a new placement using the selected object's real `.sco` file.

The next viewport click defines X/Y. As its starting transform, the copy preserves Z, rotation, pitch and bank from the current selection — including an unsaved transform preview.

Confirmation reuses the exact same safe pipeline as the Library: a real template of the same `.sco`, a new global ID, tile backup and atomic write. The command does not duplicate old tile text or introduce a second writer.


## Delete object

The inspector's **General** tab provides **Delete object**. Deletion is persistent and requires confirmation.

To avoid losing work, the command is disabled while an unsaved transform preview or object placement is active. The host validates section ordinal, ID and `.sco` path again against the current tile on disk and creates a backup before atomically replacing the file.


## Numeric spline editing

When a spline is selected, the **Path** tab can preview X, Y, Z, rotation, length, radius and start/end gradient changes.

**Save spline** persists the preview with an automatic backup. IDs and **Previous / Next** links remain read-only at this stage. Spline preview is separate from the object undo/redo history and has its own **Discard preview** action.


While an unsaved spline preview exists, the editor also blocks **Place**, **Place copy** and **Delete object**, preventing an indirect preview loss when the map reloads.


## Visual spline gizmos

With a spline selected, **W** enables the move gizmo and **E** enables the rotation gizmo. Movement adjusts X/Y/Z; visual rotation is restricted to the vertical axis, matching OMSI's spline rotation field.

The selected axis and profile follow the gizmo in real time. The change enters React preview state only when dragging ends, avoiding writes or React updates on every frame. Move/rotation snapping uses the same viewport toolbar values.

The global **Save** button and `Ctrl+S` also save spline previews. The global ✕ button discards a spline preview when it is the pending edit. ↶/↷ remain object-history-only at this stage.


## Place spline copy

In a spline's **General** tab, **Place detached copy** uses the selected spline as a real template.

Flow:

1. select an existing spline;
2. click **Place detached copy**;
3. click a tile to choose the new start point;
4. adjust Z, rotation, length, radius and gradients;
5. confirm **Confirm and save**.

The host rereads the source spline from disk and validates ordinal, `.sli` path, ID, type and links before creation. The copy preserves the source `HeaderValue`, `[spline]`/`[spline_h]` type and real extra values, receives a new global ID, and is created with `previous = -1` and `next = -1`.

Starting detached is deliberate: this stage does not automatically rewrite neighboring spline chains.


## Delete spline

The **General** tab can delete detached or connected splines.

For a connected spline, the host validates reciprocal neighbors, releases endpoints pointing to the source spline, and removes the source section in the same transaction. Every affected tile receives a backup under the same timestamp.

If any neighbor changed, disappeared, or no longer points reciprocally to the source, the entire deletion is cancelled. Comments, blank lines and unknown sections remain preserved.


## Spline search in Explorer

The main Explorer search now searches loaded objects and splines. For splines, search accepts the `.sli` filename, full path, ID and tile coordinate.

Objects and splines are shown in separate sections, each capped at 250 rendered rows. Clicking a spline selects the real instance and focuses the camera; a spline with a pending preview is marked **changed**.


## previous/next link editor

In the spline **General** tab, **Chain links** lets you enter previous and next IDs. Use `-1` for a free endpoint.

The field list suggests currently loaded splines, but the host searches and validates IDs across the full map. On save it also updates reciprocal endpoints on old and new neighbors. If a new endpoint is already occupied, a neighbor disappeared, or the current chain is inconsistent, no part of the transaction is written.

**Disconnect draft** only puts `-1/-1` into the fields; persistence happens only after **Save links**.


## Installed Spline Library

The Explorer **Splines** tab scans `OMSI 2/Splines` on demand and searches up to 50,000 `.sli` files by name/path. At most 300 results are rendered.

Each file offers **Normal** and **Height**. Preview uses the real `.sli` profile, starts detached, and exposes Z, rotation, length, radius and gradients before confirmation.

The UI does not invent header/cant/skew/delta_h values. On **Confirm and save**, the host searches the full map for a real neutral template of the selected type: five explicit numeric zero extras for normal `[spline]`, or six for `[spline_h]`. If none exists, preview remains available but persistence is rejected with a missing-template message.


## O3D and spline textures

When a selected object uses textured O3D materials, Map Studio requests only textures used by that selection's meshes. The same applies to `[texture]` entries from a selected or placement spline profile.

BMP, PNG, JPG/JPEG, GIF, WebP, DDS and TGA are resolved inside the real OMSI installation. DDS/TGA use Babylon loaders. A missing file, unsafe path, or texture larger than 16 MiB leaves the existing O3D/profile material color in place instead of inventing a fake image.

Loaded textures are cached in React and can also appear on other instances of the same object/spline during the session.


## Texture state in the inspector

The object **Materials** tab now shows the real asset state per material: **Loaded**, **Loading**, **Missing file**, **Over 16 MiB**, **Access denied**, **Read failure**, or **No texture**. When loaded, the real file extension is also shown.

The spline **Profile** panel exposes the same state next to every surface. This makes it possible to distinguish geometry with no declared texture from a texture declared by O3D/`.sli` that could not be resolved.

Old “Read only” UI labels were removed: Alpha.3 operates in **preservation-safe editing** mode within the documented limitations.


## Progressive map textures

The map can now receive real textures progressively without selecting every instance. Prefetch prioritizes object and spline types closest to the active tile.

The automatic limit is **24 textures per map/session**: 16 object textures and 8 spline textures. The bottom status bar shows `Auto textures: X/24`. Selections and previews still load their textures independently of this limit.

Nearby `.sli` profiles are also prepared progressively, one at a time, so spline surfaces can become textured without triggering a massive read.


## SCO-defined transparency

O3D preview now respects static `.sco` overrides for `[matl_alpha]`, `[matl_noZwrite]`, and `[matl_noZcheck]`.

- `matl_alpha 0`: opaque texture;
- `matl_alpha 1`: alpha-test/cutout (leaves, fences, signs with transparent pixels);
- `matl_alpha 2`: partial transparency (for example glass);
- `matl_noZwrite`: does not write depth;
- `matl_noZcheck`: does not reject drawing through the Z test.

The **Materials** tab shows a `SCO:` line only when a static override was actually matched to the material. `[matl_change]` materials remain without fake runtime state because they depend on OMSI scripts/variables.


## O3D/SCO bump maps

Static materials with `[matl_bumpmap]` now load the real bump image and apply the factor defined by the `.sco`. The **Materials** tab shows the bump name, loading state, and factor.

Like the diffuse texture, a missing or unsafe bump asset gets no placeholder: the material simply continues without bump mapping.


## Nightmap layer

The viewport now has a **Nightmap** layer. Off keeps the daytime preview. On loads real static `[matl_nightmap]` assets as emission.

The inspector shows the file and state; when disabled it shows **preview off**.


## Envmap reflections

Static materials with `[matl_envmap]` now load the real reflection texture and respect the strength defined by the `.sco`.

The **Materials** tab shows envmap file, state, and strength. Missing/unsafe assets simply leave the material without the extra reflection.


## Runtime-dependent materials

The inspector identifies `Transmap` and `Lightmap` declarations from `.sco` and labels them **runtime not simulated**.

This is intentional: `\S:`/script references and OMSI variable-controlled lightmaps are not treated as static files or automatically enabled.


## Incomplete material diagnostics

When a material uses recognized commands that are not simulated yet, **Materials** shows `Not simulated:` followed by the commands.

This currently includes `[matl_envmap_mask]`, `[alphascale]`, and `[matl_allcolor]`. The goal is to expose fidelity gaps instead of silently ignoring OMSI/editor features.


## Texture-cache controls

The bottom bar shows `Cache: X/64`. Real texture assets are capped at 64 entries and the least-recent entry is automatically evicted when needed.

The layers panel now has **Clear cache**, removing loaded textures, clearing in-flight request bookkeeping, and resetting the automatic prefetch budget without unloading the map.


## Real object LOD

`.sco` objects with multiple `[LOD]` blocks no longer display every detail level at once. The active level changes according to projected screen size.

In **Geometry**, every mesh is labeled **Global** or with its threshold, for example **LOD 0.6**, making validation easier.


## Spline profiles on the map

The **Spline profiles** layer displays real nearby spline surfaces using `.sli` profile geometry, UVs, gradient, curvature, and already-loaded textures.

Rendering is limited to the active tile's 3×3 area and 120 splines per scene. Disable **Spline profiles** to return to the lightweight axis-only view.


## Terrain state

The map inspector shows, for the active tile: coordinates, `[terrain]` marker presence, `.map.terrain` sidecar presence, and file size.

It also shows a loaded-area `sidecars/markers` summary, making inconsistent tiles visible before binary editing exists.

## Fullscreen and viewport navigation

The editor now provides **native fullscreen** from the toolbar or **F11**. The WPF host removes the Windows frame and maximizes the window; **F11** or **Esc** restores the previous window state.

The viewport camera preserves position, target, and zoom when the 3D scene is rebuilt during progressive O3D/texture loading. This prevents camera jumps while the map continues filling in.

Navigation controls:

- right mouse drag: orbit the camera;
- middle mouse drag: pan;
- Shift while panning: faster movement;
- mouse wheel: progressive zoom;
- arrow keys: move across the map;
- Shift + arrows: accelerated movement;
- `+` / `-`: zoom in/out;
- **F**: focus the selected object or spline;
- **Home**: fit the map;
- **1 / 2**: perspective / top view.

The left mouse button remains reserved for selection, placement, and edit gizmos.

## Real OMSI terrain

The `.map.terrain` sidecar is now decoded as OMSI's real height grid. The validated format uses a little-endian `uint32` cell count header (normally 60), followed by `(N+1)²` little-endian `float32` heights. The standard format therefore contains **61×61 points / 3,721 heights / 14,888 bytes**.

The viewport builds a 3D mesh from those real values with spacing derived from the 300 m tile. The **Terrain** layer can be enabled/disabled. Its current material is neutral and only visualizes geometry; `.rdy` paint/layer data is not simulated yet.

The map inspector shows whether the mesh decoded successfully, cell/point counts, and the active tile altitude range. Sidecars with an invalid size/grid remain visible in diagnostics but do not receive invented geometry.

## Real base terrain texture

The map now reads real **[groundtex]** entries from `global.cfg` in declaration order. Layer 0 provides the main and detail textures used as the terrain base.

Layer 0's main texture is resolved inside the real OMSI installation, loaded by the host, and applied to the height mesh. The repeating value declared in `global.cfg` is applied to the texture UVs. If the asset is missing or the path is unsafe, the mesh keeps its neutral material instead of inventing an asset.

The detail texture from the same layer is also resolved and its state is exposed in the inspector, but **detail blending is not visually simulated yet**, because OMSI's exact blend mode/factor is still being validated. Additional [groundtex] layers remain available as real data but are not painted until their relationship with `.rdy` data is confirmed.

The inspector shows [groundtex] layer count, main texture path/state and repeating, plus detail texture path/state and repeating.

## Numbered terrain-paint masks

Beyond the base layer, Map Studio now detects real `texture/map/<tile>.map.N.dds` files. Index **N** is associated with the `[groundtex]` entry at the same index in `global.cfg`.

Each tile DDS mask is loaded only when needed and used as opacity over a copy of the same height mesh. Layer N's main texture uses its own repeating value. This allows areas painted by the OMSI editor to appear over layer 0 without replacing the real terrain relief.

The inspector shows mask indices present on the active tile. Invalid paths, indices without a matching `[groundtex]`, or missing assets do not create fake layers.

The `.terrain_0.rdy` file is also handled by a dedicated diagnostics reader. It validates vertex, triangle, material, and transform sections found in the render data, but **those coordinates do not replace the height mesh yet**, because `.rdy`-specific semantics are still being validated separately.

The detail texture declared by `[groundtex]` remains loaded for diagnostics only; detail blending is not simulated yet.

## Terrain-layer controls and validation

The inspector now lists every map `[groundtex]` entry with an individual visibility control. Layer 0 can be hidden to compare against the neutral base; numbered layers can be disabled without modifying masks or `global.cfg`.

The viewport layer panel separates **Terrain** from **Terrain paint**. Disabling terrain paint keeps the height/base mesh available and removes only numbered-mask paint layers.

The host reads DDS headers and reports width, height, pixel format, and whether the texture is alpha-only. At this stage, a numbered mask is rendered only after it is validated as **DDS A8 alpha-only**. Other formats remain visible in diagnostics as **not rendered** instead of being guessed.

Terrain caches are bounded to **32 [groundtex] textures** and **96 DDS masks**. **Clear cache** removes object, spline, terrain, and mask textures without unloading the map.

## Terrain DDS-mask validation

Numbered `texture/map/<tile>.map.N.dds` masks are now validated before rendering. The format accepted at this stage is the A8 DDS format observed in real OMSI maps: `DDS ` signature, standard header, 8 bits per pixel, and an 8-bit alpha channel.

For each mask, Core records width, height, minimum/maximum alpha, and real coverage (percentage of pixels with alpha greater than zero). The inspector exposes these values per layer.

Invalid or completely empty masks are not loaded/rendered. Fully opaque masks still apply their layer, but the editor avoids loading an unnecessary `opacityTexture`. This reduces I/O and material cost without changing the visual result.

Base-layer (index 0) visibility is also honored by the editor preview. These visualization options never modify the original map files.

## Expected terrain-mask resolution

Each `[groundtex]` resolution code is now converted to the expected real paint-mask dimension. For paintable layers, the validated relationship is a power of two: **6 → 64 px**, **7 → 128 px**, **8 → 256 px**, **9 → 512 px**, and **10 → 1024 px**. Layer 0 still has no paint mask of its own and uses code 0.

When loading `tile.map.N.dds`, the editor compares the real width/height against layer N's expected resolution. A valid A8 mask with incompatible dimensions is reported as incompatible in the inspector and is not rendered. This prevents accidentally applying a mask from another map/layer.

## Base-terrain texture fidelity

The terrain base layer uses the real texture declared by `[groundtex]` as **albedo**. Formats decoded directly by the WebView/Chromium path, such as BMP, PNG, JPEG, GIF, and WebP, are no longer forced through Babylon's texture-loader path; only DDS and TGA continue to use dedicated loaders.

Layer 0 is treated as opaque and the terrain material no longer multiplies the source texture by arbitrary editor lighting. This prevents a real loaded texture from appearing almost black because of loader/material/lighting interaction. UV orientation and repeating values continue to come from the known real data; no detail-texture blend is invented at this stage.


## Blocking loading UI

Structural loads that change editable map state now use a centered animated overlay. While selecting/reading the OMSI installation, opening a map, loading the full map, changing the active 3×3 area, reading SCO/O3D/SLI data, or initially loading libraries, the UI blocks pointer input, focus, and editing shortcuts until the host finishes the operation.

When the host exposes real progress, such as full-map tile loading and progressive O3D preparation, the overlay shows a percentage and progress bar. Operations without numeric progress use an indeterminate animation. The lock is also released on host errors so the editor cannot remain stuck.

Bounded texture prefetch remains a background visual cache after the corresponding editable structure is ready; it is not used as a reason to freeze the editor indefinitely.


## Immersive fullscreen and floating tools

In fullscreen mode, side panels and fixed application chrome are hidden so the viewport uses the entire available area. Core actions move to a floating dock: select, move, rotate, fit, focus, Explorer/Inspector, create an object from the Library, create a spline from the Library, snap, undo/redo, save, and visibility controls.

Explorer and Inspector remain available as temporary floating drawers without permanently reducing the 3D area. A shortcut strip stays visible at the bottom with Q/W/E, 1/2, N, F, Home, G, O, L, Ctrl+S, Ctrl+Z/Y, and mouse navigation.

### Terrain and splines

Real BMP textures delivered by the host are transcoded in memory to PNG before reaching the WebView. This preserves the visual texture content while avoiding variable BMP support in the browser/Babylon texture path. The original OMSI file is never modified.

In full-map mode, every spline path actually used by the map enters the real SLI-profile loading queue instead of limiting preparation to a few types near the active tile. The viewport can render up to 500 spline surfaces in full-map mode; performance mode keeps profile rendering near the active area. Spline materials are shown as albedo so editor lighting cannot make them artificially dark.


## Real GPU texture upload

Grundorf visual diagnostics showed that the host was already transcoding `gras.bmp` to PNG and the Inspector received the asset correctly, but that did not prove the texture had been successfully created inside Babylon/WebGL.

Browser-decodable image formats now explicitly use `Texture.CreateFromBase64String`, Babylon's path intended for Base64 payloads, instead of relying on a generic `data:` URL. DDS/TGA remain on their dedicated loader path. Upload failures are logged with source extension, MIME type, and the Babylon error.

For terrain and real spline surfaces, the preview also uses the real texture as an unlit emissive/albedo source. This removes editor lighting/material multiplication as a cause of an almost-black surface without inventing a replacement texture. The real OMSI file remains the only visual source.


## Continuous loading without one modal per item

The full-screen blocking overlay is now reserved for structural operations: selecting the OMSI installation, opening a map, loading the full map/region, and loading libraries. Individual SCO, O3D, SLI, and texture reads continue in the background and are reported in the status bar instead of opening and closing a modal for every resource.

## Real RGBA path for BMP textures

In addition to the diagnostic PNG, real BMP files are decoded by the host into RGBA pixels and uploaded to Babylon as `RawTexture`. This removes the browser image decoder from the critical path for BMP-based terrain and spline textures. The original OMSI file is not modified.

## Windows icon identity

The executable continues to embed `MapStudio.ico`. The process and installer shortcuts now also use the stable `MichaelPriest.OMSIMapStudio` AppUserModelID and explicitly use the EXE as the icon source, preventing generic Windows taskbar and shortcut icons.


## Single continuous loading phase

After structural map loading, the editor keeps **one continuous loading animation** while warming the visual resources for the area: O3D geometry, SLI profiles, textures, `groundtex`, and masks. The screen no longer closes and reopens for each individual item. Unlocking only occurs after a short stable period with no pending resources, avoiding flicker between sequential batches.

## BMP terrain as an unlit material

When the real base texture arrives as RGBA, the terrain material also uses the same asset through the emissive channel. This guarantees the real texture remains visible when material lighting is disabled. The Inspector keeps the RGBA diagnostic, and the host includes sampled average RGB values from the decoded pixels to distinguish a genuinely dark source from a material/GPU issue.

## Windows icon

The application now uses the executable's native shell identity again, avoiding an unregistered custom AppUserModelID when the EXE is launched directly. The window uses an explicit pack resource URI and reapplies `MapStudio.ico` at runtime, while keeping the compiled `ApplicationIcon` as fallback.


## OMSI special trees ([tree])

The editor now handles OMSI special tree objects separately from the `.x` helper used only by the original editor. The real `[tree]` block in the `.sco` provides the tree definition, while each map `[object]` placement stores the actual selected texture, height, and width/height ratio in its `ExtraValues`.

The viewport renders the tree as an unlit vertical billboard using the texture, height, and ratio stored by that exact map placement. Trees with different dimensions therefore remain different in Map Studio instead of being replaced by averages or mock values.

Repeated tree textures share a material inside the same scene to avoid creating one GPU texture copy per tree. Yellow fallback markers remain only for objects that still have no renderable representation.


## Complete O3D objects and spline profiles

O3D preview no longer uses the embedded material diffuse alpha as a global object opacity. Transparency follows real SCO directives such as `[matl_alpha]`, preventing valid meshes from disappearing when O3D diffuse alpha is not object transparency.

The editor LOD selector also keeps one real LOD visible when no raw SCO threshold directly matches the preview screen fraction. This prevents the previous state where all LOD meshes could become disabled at once.

`.sli` profiles containing three or more `[profilepnt]` entries now generate every strip between consecutive points. Previously only the first pair was used, which could render only part of a road or sidewalk profile.

## Lateral navigation

With the viewport focused, `A/D` and left/right arrows pan sideways; `W/S` and up/down arrows move forward/backward. `Shift` accelerates movement. Middle mouse still pans, and `Shift + right mouse` pans instead of orbiting.


## Map flow and editing shortcuts

After connecting the OMSI 2 folder, **Open map** displays the real maps found under `maps`. The user can search and choose a map by name; manual folder selection remains available as a fallback.

The main sidebar can be collapsed. In the editor, **All / Objects / Splines / Terrain** control which item type is clickable. Shortcuts: `Alt+1..4` switch those filters; `Alt+R` opens road creation from the spline library; `Alt+C` junction; `Alt+O` object; `Alt+T` terrain; `Alt+A` water; `Alt+G` grass; `Alt+Y` tree. These shortcuts reuse only real installed libraries and assets.


## Direct editing, easy roads, and real maps

In the viewport, click an object or spline to select it and open transform controls. In Terrain mode, clicking marks an exact point for the leveling brush. `Ctrl + arrows/WASD` advances one 300 m tile.

The **Road** flow lets the user choose a real spline and mark start/end with two clicks. Length, rotation, and grade are derived; **Level to terrain** recalculates Z/grade from the loaded terrain heights.

Under **Real map by coordinates**, the user supplies their own Google API key, latitude/longitude, and chooses roadmap/satellite/hybrid/terrain. Imagery is shown over the terrain as a construction reference. An elevation grid can be fetched for the active tile and applied to the real `.terrain` with backup.

On **Open map**, **Create real map** clones the installed `template\NewMap` and saves the coordinate anchor as Map Studio metadata. This does not yet replace OMSI's official `[worldcoordinates]` conversion.


### 3×3 tile navigator

The editor shows a compact 3×3 navigator over the viewport. The center is the active tile and the eight surrounding buttons represent existing neighboring tiles. Clicking a block changes the active tile and moves the camera to that tile center while roughly preserving the current zoom. Missing tiles are disabled. `Ctrl + arrows/WASD` remains available as a keyboard alternative.


### Real library item preview

Selecting a `.sco` or `.sli` creates a preview at the active tile center using the file's real geometry/profile. The library panel shows the active item, loaded mesh or surface/texture counts, and a **Focus preview** button. Selecting another item replaces only the preview; the map is not written until confirmation.


### City-builder-style placement flow

Item placement now follows a city-builder-like workflow without replacing OMSI's real file format. After selecting a real `.sco` object (including trees, water, grass and junction assets when represented by real scenery objects), its real geometry is rendered as a **translucent 3D ghost** and follows the cursor over valid tiles. A click pins the preview position; map files are changed only after the explicit safe-save confirmation.

Regular splines also show a real 3D cursor-following preview. In the road creator, the user selects a real `.sli`, presses at the start point and drags to the desired end point. A lateral **curve** control can then reshape the road; the editor converts that control to the real OMSI arc parameters (`rotation`, `length` and `radius`). Terrain leveling uses the actual curved arc endpoint rather than a straight-line approximation.

The Library contains a 3D preview window for the selected item using loaded real geometry/profile and textures. It does not create synthetic thumbnails.

The **Real map by coordinates** panel is no longer permanently visible over the viewport. It is opened from **Map > Real map by coordinates…** or the matching **View** menu option.

Floating viewport tools have drag handles: tile navigator, terrain leveling, real-map panel, placement bars, camera/tools, layers and the fullscreen dock can be repositioned while editing.

Scene click selection walks up the picked mesh parent chain, so child meshes still resolve to the real placed object when selection metadata lives on a parent node.

After a road has start and end points, the viewport shows a **blue control sphere** connected to the middle of the alignment. Dragging this sphere sideways changes curvature live, in a city-builder-style workflow; the placement-bar slider remains available for fine adjustment. The visual control still produces only the real parameters supported by an OMSI spline.


### Selection while moving/rotating

The temporary geometry used by **Move (W)** and **Rotate (E)** keeps the original OMSI object/spline identity in its picking metadata. Clicking the editable preview therefore keeps selecting the same real item, including `[tree]` billboards and spline profiles, without depending on the untouched map instance that remains visible until save. This changes hit-testing/selection only and does not modify OMSI coordinates, materials, textures or files.


### Grouped construction library

The real `.sco` and `.sli` libraries now use city-builder-style visual navigation. Scenery objects are separated into **Junctions**, **Bridges**, **Houses / buildings**, **Trees / vegetation**, **Transit**, **Street furniture**, **Infrastructure**, and **Other**. Splines are separated into **Roads**, **Sidewalks / paths**, **Rail**, **Bridges / tunnels**, **Markings**, and **Other**.

Classification is derived from the real asset name/path, already-loaded `.sco` metadata, and the real `[tree]` definition when available. Assets that cannot be identified safely remain under **Other**; no asset is invented or replaced with a mock.

Each card has a visual category and separate **Preview** and **Place/Create** actions. **Preview** loads the real geometry/profile in the 3D viewer without starting a map edit. Creation still uses only real `.sco/.sli` assets and the existing preservation rules.


### Advanced library: favorites, recent items, collections, and filters

The city-builder library now keeps local **Favorites**, **Recent**, and **Most used** views for scenery objects and splines. Users can also create custom **Collections**, including mixed `.sco` and `.sli` references, without changing the OMSI installation or map files.

Search recognizes common Portuguese, English, and German synonyms (for example rua/road/straße, árvore/tree/baum, and ponte/bridge/brücke). Groups expose contextual subcategories such as residential/commercial/industrial, lighting/signage, avenues/roads/one-way streets, cycle paths, and rail.

Technical filters can highlight assets used by the map, actually detected `[tree]` objects, already loaded `.sco` geometry, and loaded `.sli` profiles. Cards and the inspector show map usage, library frequency, subcategory, and 3D/profile status. Preferences live in UI-local storage; storage failures or quota limits never block real map editing or saving.


### Cached 3D thumbnails and drag-and-drop

Once a real 3D preview finishes rendering, the UI captures a lightweight JPEG thumbnail and keeps it in a local cache (up to 48 entries). The thumbnail replaces the generic card icon, producing a visual library without reloading and rendering every asset at once.

`.sco` and `.sli` cards can be dragged directly into the viewport. Drop uses the editor's existing raycast to convert screen position into a real tile/map coordinate. Objects enter the normal placement flow; splines start as a normal 20 m segment. Saving still goes through the existing bridges and backup rules.


### Batch, line, and brush placement

`.sco` objects have four construction modes: **Single**, **Repeat**, **Line**, and **Brush**. Line distributes the asset between two points using the configured spacing; Brush distributes up to 256 items in a circular area; Repeat accumulates clicked points before saving. **Varied rotation** adds deterministic rotation variation to the batch.

Operations with more than one item use the native `insertObjectBatch` command. The host validates the asset and target tiles, reserves globally unique IDs, groups writes per tile, and uses one `SafeFileTransaction` plus one backup directory for the complete operation.

**Snap/align to road** finds the nearest normal spline within the configured distance, projects the placement onto the spline axis, and adopts the local road heading. The viewport displays up to 96 simultaneous ghosts to keep preview rendering light; saved batches remain capped at 256 items.


### Matrix, circle, lots, and presets

Batch placement now includes **Matrix**, **Circle**, and **Lots**. Matrix creates rows/columns with X/Y spacing; Circle distributes items around a perimeter and can orient each asset tangentially; Lots uses two points as a street frontage and distributes houses/buildings with configurable spacing and setback.

Built-in presets include **Avenue trees**, **Street lights**, **Aligned houses**, **Green square**, and **Grid/parking**. Presets only configure placement tools; saving still uses the selected real `.sco` asset and the transactional batch pipeline.


### Construction bar and bridge/elevated mode

The main toolbar now has a dedicated **Construction** group that opens **Roads**, **Junctions**, **Bridges**, **Buildings**, **Vegetation**, **Transit**, **Street furniture**, **Infrastructure**, and **Terrain** directly. Each button only routes into the corresponding real asset library; no parallel fake asset catalog is created.

**Bridge/Elevated** mode opens the real bridge/tunnel spline group and reuses the point/drag road builder and curve handle. A configurable elevation is added to the real terrain height at both spline endpoints while preserving conversion into OMSI rotation, length, radius, and gradient fields.


### Dependency audit and quick duplication

**Check dependencies** loads the real `Sceneryobjects` and `Splines` catalogs and compares their paths against every reference in the open map. Missing `.sco` and `.sli` files appear in a floating viewport panel, separately from texture or O3D failures.

`Ctrl+D` starts placing a copy of the selected object using the exact same `.sco`, Z, rotation, pitch, and bank as the initial transform; the next click defines the new position and still uses the safe insertion pipeline.


### Geometric junction assistant

When **Construction > Junctions** is opened, the editor analyzes up to 400 loaded normal splines. Curved splines are sampled along their real arc; the system looks for useful-angle intersections, ignores simple endpoint-to-endpoint chain connections, and deduplicates nearby points.

The **Junction assistant** lists up to 64 suggestions and lets the user pick a point. A suggestion only supplies an initial position/rotation; the user still selects a real junction `.sco` from the library. The assistant never invents junction geometry or replaces OMSI dependencies.


### Transactional construction history

Single object insertions, batches, and newly inserted splines are added to a construction history. **↶ Construction** restores the backup directory produced by the operation itself.

Restoration runs in the desktop host with strict validation that only directories inside the open map's `.mapstudio-backups` tree can be used. Before restoring, the current file state is saved into another transactional backup; that backup powers **Redo**. Session history keeps the latest 40 actions and resets when switching maps.


### Contextual 3D preview scale

The `.sco` preview computes real O3D geometry dimensions after applying each mesh's declared scale, rotation, and translation, then displays **W × H × D** in meters. `[tree]` objects use their real maximum height/aspect when no O3D mesh exists.

For `.sli`, the preview displays the real profile's total width and vertical range from its surface points. This avoids introducing fake human or bus scale models.


### Smart road endpoint snapping

In the road/bridge builder, **Snap to existing road endpoints** finds the nearest endpoint of a normal spline within the configured range. The new road start/end then uses the exact endpoint coordinate, including endpoints of curved splines.

The panel shows the spline ID, which endpoint was used, and the original cursor distance. Snapping never edits, cuts, or reconnects the existing spline automatically; it only positions the new segment on the real endpoint to reduce manual adjustment.


### Technical asset diagnostics in the library

Library previews now participate in the same real texture-loading path used by selection/placement. Clicking **Preview** alone requests the textures referenced by the `.sco`/O3D or `.sli` profile, which also improves persisted 3D thumbnails.

The `.sco` inspector reports loaded/failed meshes, missing O3D files, vertices, triangles, materials, loaded/missing/pending textures, material commands that are still unsupported, and declared collision meshes. The visual status reports **healthy**, **attention/loading**, or **problems detected**.

For `.sli`, the inspector reports surface count, real profile width, loaded/missing/pending textures, and alpha-enabled surfaces. Diagnostics are read-only; they never change the asset or replace dependencies.


### Transactional multi-object batch

The desktop bridge accepts `insertObjectMultiBatch` to insert several different real `.sco` groups in one operation. The host validates each asset, finds its preservation template in the map, reserves one global ID sequence, and groups all objects per tile before writing.

A multi-batch accepts up to 16 asset types and 512 total objects, with at most 256 per type. The entire operation uses one `SafeFileTransaction` and one backup directory, preventing ID races when a construction set mixes assets such as trees and street lights.


### Composite construction sets

The movable **Sets** panel stores reusable combinations made from one real `.sli` spline plus up to 16 real `.sco` companion asset types. Each companion can configure **side** (left/right/both), **spacing**, **lateral offset**, and **additional rotation**.

When **Build set** is used, the road builder draws the real base spline. After the host actually saves that spline, companions are distributed along the returned spline geometry — including curves and gradient — and sent through `insertObjectMultiBatch`, up to 512 total objects.

Each companion `.sco` must have a preservation template in the current map. This preserves OMSI-specific extra values instead of inventing object structure. Sets live in UI-local storage and only contain asset references/placement configuration; they never copy assets.


### Start/end handles in the road builder

In addition to the blue curvature handle, road/bridge drawing now shows a **green** start handle and a **red** end handle. Both can be dragged directly in the viewport; preview length, rotation, radius, and gradient are recalculated while the point moves.

Endpoint handles reuse existing spline-end snapping. When a handle enters the configured snap range it can lock exactly onto a real road endpoint. The existing spline remains untouched; only the new unsaved road is adjusted.


### Automatic previous/next linking for snapped roads

Endpoint snapping can now also create OMSI links for the new spline. Automatic linking is armed only in safe cases:

- new road start snapped to the end of an existing spline → previous;
- new road end snapped to the start of an existing spline → next;
- the target endpoint must have the corresponding link slot free.

An incompatible or already-busy endpoint remains position-snapped only and is marked as blocked in the panel. Saving reuses the existing OmsiSplineLinkPlanner, which updates reciprocal links and rejects conflicts.

When a Construction Set is active too, operations are serialized: the spline is inserted first, links are updated second, and only then is the companion-object multi-batch written. This avoids concurrent transactions against the same tiles.


### Preservation-safe dependency replacement

The desktop host accepts replaceMapAssetPath to replace a missing path with another real installed asset. For objects, only the path line inside [object] is changed. For splines, only the path field of [spline]/[spline_h] is changed according to the map-version layout.

IDs, links, position, rotation, length, radius, gradients, and extra values remain untouched. The operation scans map tiles, writes only files that actually changed, and uses one SafeFileTransaction with backup.


### Repairing missing dependencies from the library

The missing-dependency panel now lets the user explicitly select a broken `.sco` or `.sli` path. Selecting it opens the matching library; the asset currently loaded in **Preview** becomes the replacement candidate.

Before confirmation, the panel shows the missing path, selected replacement, and how many references to that path are currently loaded. The host operation always scans every map tile, including tiles that are not currently in the viewport.

Replacement changes only the asset path and creates a transactional backup. When files change, that backup is added to the **↶ Construction** history so the operation can be undone. Replacement is never automatic.


### Map health and problem filters

The **Health** button gathers available technical diagnostics without turning missing information into false errors. The panel separately counts missing dependencies, loaded `.sco` objects with O3D/texture issues, loaded `.sli` splines with profile/texture issues, requested textures that failed, and map geometry with no renderable visual.

Both libraries now include the technical filter **⚠ Problems**. It lists only assets for which Map Studio already has evidence of failure—for example a declared missing O3D, geometry that failed to load, or a requested texture that returned missing/error. An asset that has not been loaded yet is not placed in this filter.

Health cards open the corresponding filtered library or dependency audit directly, making incomplete-map repair faster.


### Persistent movable-tool positions

Movable windows and toolbars now remember their last position in UI-local storage. Restoration also works for dynamically mounted panels such as **Map Health**, **Construction Sets**, the junction assistant, placement bars, and full-screen tools.

Restored positions are clamped to the current container dimensions, so changing resolution, window size, or full-screen mode does not leave a tool permanently outside the visible area. Local-storage failures never interfere with map editing.


### City-builder UX v2 — desktop and full screen

The viewport is now the main editor workspace. **Explorer/Library** and **Inspector** work as collapsible overlay drawers instead of permanent columns. Selecting an object or spline opens Inspector automatically; choosing a construction tool opens the appropriate library.

Construction lives in a shared **bottom-center dock** for desktop and full-screen modes, with road, junction, bridge, building, vegetation, transport, street-furniture, infrastructure, and terrain categories. A contextual strip shows the current item/mode, selection filters, and Snap.

The top area is reserved for edit, camera, and save operations. Full-screen mode uses a compact top toolbar and removes the permanent shortcut strip to keep the scene clear.

### Robust map selection

Click selection now resolves through three levels: the real pickable mesh, inherited parent-node metadata, and a geometric fallback based on the real OMSI placement/axis. This keeps incomplete/protected O3D objects, trees/billboards, and very thin splines selectable.

When a placement tool is open, clicking directly on an existing real map item prioritizes selection of that item; clicking free terrain continues to place the pending asset.


### Robust fullscreen and universal selection

Fullscreen now updates the React layout immediately and, in the desktop app, reinforces the WPF window as borderless/maximized, brings it to the foreground, and restores the previous state/size when leaving. F11, Escape, and the UI button all use the same flow. The editor layer also fills 100vw × 100vh while the native host transition is applied.

Visible objects and splines are now always clickable regardless of the current Object/Spline/All filter. Terrain handling receives the click only when no real object/spline was hit.

Objects whose O3D has no renderable visual or is missing now receive an individual pickable marker carrying the real object identity, so incomplete items can also be selected and repaired. Selection filters are now visual/organization filters rather than picking blockers.


### OMSI editor semantics and city-builder interaction references

The interface preserves OMSI Map Editor semantics: `.sco` objects, `.sli` splines, terrain, and traffic tooling remain distinct real entities/modes, while spline editing continues to respect real map length, radius, gradients, and links. The modernized workflow does not replace those values with synthetic abstractions.

The visual flow adopts modern city-builder interaction patterns: primary construction tools concentrated at the bottom, searchable asset libraries, a contextual inspector, and visible road/snap guides. These are interaction references only; no proprietary third-party assets are reused.

Desktop fullscreen now uses the physical bounds of the current monitor instead of relying only on `WindowState.Maximized`. The compact menu and status bar remain accessible. The Babylon viewport uses a `ResizeObserver` on its real container so render dimensions and picking coordinates remain synchronized during fullscreen, resizing, and movable-panel layout changes.


### Selection feedback and road guide

The viewport now uses a light outline on the real item under the cursor and a distinct outline on the active selection. The effect does not replace real materials/textures and does not make ghosts, gizmos, terrain, or overlays selectable as real map objects. This follows the OMSI editor principle of clearly showing which item will receive the click without applying an aggressive solid-blue tint.

When the easy road/bridge builder is active, the bottom bar shows a compact contextual guide with the start → end → curve sequence, endpoint snap state, automatic `previous/next` linking, snapped endpoint confirmation, and bridge elevation.


## Functional top menu and editing flow

The editor top menu has been simplified to expose only actions already supported by OMSI Map Studio:

- **File** saves pending object or spline transforms, discards previews, and opens another map;
- **Edit** exposes transform undo/redo, construction history, and Select/Move/Rotate tools;
- **View** controls Explorer, Inspector, tile navigation, fit, focus, and fullscreen;
- **Map** groups real-map coordinates, Map Health, dependency auditing, and Construction Sets.

The old disabled placeholders were removed from the active surface. The top bar now also shows a compact pending-edit count and the current map loading mode. The same pattern remains available in desktop and fullscreen, while the bottom bar stays the primary category-based construction surface.


### Movable panels

Explorer and Inspector now use the same persistent floating-tool system as the other editor tools. Each drawer has its own drag handle; its position is clamped to the usable editor area and restored from local UI storage. The panel keeps a stable size while being dragged in both desktop and fullscreen modes.

## Contextual construction asset shelf

The main bottom construction categories now expose a horizontal shelf of real assets directly over the viewport:

- **Roads/Bridges** use real `.sli` entries from the active category;
- **Junctions, buildings, vegetation, transit, street furniture, infrastructure, and objects** use real `.sco` entries;
- the shelf prioritizes favorites, recent items, and frequently used assets;
- thumbnails generated by the real 3D preview are reused when available; otherwise a category icon is shown without inventing a fake preview;
- one click selects the asset for placement/construction;
- double click opens the complete library and real 3D preview;
- the **Library** button remains available for search, filters, collections, favorites, and technical diagnostics.

When an item is selected from the shelf, the interface switches to the **contextual Inspector**. During `.sco` placement it shows the real path, group/subcategory, 3D geometry availability, safe template state, target tile/position, and rotation. During `.sli` construction it shows the real profile, `[spline]`/`[spline_h]` type, length/radius/rotation, and snap/`previous`/`next` state. This mode does not simulate data; it only reflects real loaded information or parameters of the active construction operation.

### Quick road controls

The contextual road/bridge guide in the bottom bar is now interactive as well. **Endpoint snap** toggles snapping to existing spline endpoints, **previous/next** toggles automatic chain linking, and bridge/elevated mode exposes `−`/`+` elevation controls in 0.5 m steps. The detailed elevation field remains available in the full panel, and both surfaces use the same real preview update path.


### Automatic contextual Inspector

When an object or spline is selected from the construction shelf or library, the **Inspector** now opens automatically and follows the real asset being placed. In fullscreen it replaces the previous drawer to keep the viewport clear; on desktop it opens as a floating panel. It shows the file, group, subcategory, preview/profile state, pending destination or geometry, and actions to open the full preview or cancel placement.


### Unified quick toolbar

Desktop and fullscreen now share the same **movable floating quick toolbar** for selection, move/rotate, fit/focus, snapping, undo/redo, saving and layer visibility. Its position is persisted together with the other floating tools.

The old horizontal toolbar has been removed from the active layout to reduce duplicated controls. Its functions remain available through the quick toolbar, bottom construction HUD and top menus. **Full map** and **Performance 3×3** modes are now available under **View**, while the active mode remains visible in the menu status.


### Selection filters in real viewport picking

The **All / Objects / Splines / Terrain** filters are now enforced directly by viewport picking, including real meshes, child hierarchies and geometric fallback. In **Objects** mode splines cannot steal the click; in **Splines** mode objects cannot steal it; and in **Terrain** mode pointerdown no longer selects scenery before terrain picking is processed.

### Fullscreen visual alignment

Fullscreen now follows the approved editor visual direction:

- a compact project rail keeps Map Studio identity and icon navigation visible;
- functional menus and real status remain visible at the top;
- construction categories live at the top of the viewport;
- the quick editing dock sits directly below the categories;
- Explorer, Inspector, and tile navigation remain floating panels over the map;
- the contextual subbar stays accessible near the bottom of the viewport;
- top status exposes map-ready/dirty state, load mode, local index, and map health;
- opening the asset shelf repositions the dock and floating panels to avoid overlap.

This is a UX/layout change only: the viewport still consumes real C# host state and introduces no fake data.


### Tools and Help menus

The editor top bar now follows the approved visual reference without adding decorative commands:

- **Tools** contains the real dependency audit and construction sets;
- **Help** opens a movable editor-shortcuts panel;
- the shortcut panel reuses the `floating-tool` system, so it can be repositioned without blocking the viewport;
- the old fullscreen-only shortcut strip was removed in favor of one shared panel for desktop and F11;
- **Map** stays focused on map actions such as real-map-by-coordinates and map health.

All behavior remains connected to real editor state; no fake commands are introduced.
