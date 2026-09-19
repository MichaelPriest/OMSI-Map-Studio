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


## Delete detached spline

The **General** tab can delete a spline only when `previous = -1` and `next = -1`.

The host rereads the tile, validates ordinal, `.sli` path, ID, type and links, and removes only the command line and functional spline data. Comments, blank lines and unknown sections are preserved. The tile is backed up before atomic replacement.

Connected splines remain protected until transactional editing of neighboring links is implemented.


## Spline search in Explorer

The main Explorer search now searches loaded objects and splines. For splines, search accepts the `.sli` filename, full path, ID and tile coordinate.

Objects and splines are shown in separate sections, each capped at 250 rendered rows. Clicking a spline selects the real instance and focuses the camera; a spline with a pending preview is marked **changed**.


## previous/next link editor

In the spline **General** tab, **Chain links** lets you enter previous and next IDs. Use `-1` for a free endpoint.

The field list suggests currently loaded splines, but the host searches and validates IDs across the full map. On save it also updates reciprocal endpoints on old and new neighbors. If a new endpoint is already occupied, a neighbor disappeared, or the current chain is inconsistent, no part of the transaction is written.

**Disconnect draft** only puts `-1/-1` into the fields; persistence happens only after **Save links**.
