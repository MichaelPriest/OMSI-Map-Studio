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


## Tile streaming on large maps

The editor works with an **active tile** and a default 3×3 window around it. The full map topology still comes from `global.cfg`, but objects, splines and other heavy data are loaded only for that region.

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
