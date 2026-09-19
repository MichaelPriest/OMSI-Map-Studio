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
