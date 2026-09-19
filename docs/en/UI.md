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
