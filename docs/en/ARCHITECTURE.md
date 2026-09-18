# Architecture

**English** · [Português (Brasil)](../pt-BR/ARCHITECTURE.md)

OMSI Map Studio is an independent product and must not depend on OMSI NavBR Multiplayer code, runtime services or releases.

## MapStudio.Core

Owns OMSI-facing domain logic:

- configuration parsing;
- detection and preservation of source file encodings;
- map discovery;
- safe `.map` tile file reading;
- base placed-object parsing;
- conservative `.sco` metadata reading;
- tiles;
- real unencrypted `.o3d` geometry for preview;
- splines;
- terrain;
- paths;
- backups;
- validation.

Core must not depend on WPF, WebView2 or React.

### Tile reading

`OmsiTileReader` receives the real path of a `.map` file and uses the same preservation-oriented parser as the other configuration files.

At this stage it only extracts information that can be identified safely by section:

- `[object]` count;
- `[spline]` count;
- `[splineAttachement]` / `[splineAttachment]` count;
- whether the referenced tile file exists.

Tile file references pass through `OmsiMapPathResolver`. After normalization, the path must still remain inside the map directory. Directory traversal references such as `..\\` are rejected and treated as unavailable tiles.

### Coordinate system

`OmsiMapCatalog` detects the `[worldcoordinates]` marker in `global.cfg`.

- without `[worldcoordinates]`: the Cartesian layout uses 300 m tiles;
- with `[worldcoordinates]`: the initial viewport shows only schematic tile topology until a dedicated geographic conversion exists.

The editor must not apply a 300 m Cartesian scale to world-coordinate maps.

### Placed objects

For `[object]`, Core interprets only the confirmed base block:

1. header value whose semantics are still intentionally unassigned;
2. `.sco` file path;
3. object ID;
4. `x` position;
5. `y` position;
6. `z` position;
7. rotation;
8. pitch;
9. bank.

Later values are retained in `ExtraValues` and are not assigned meaning until dedicated models and tests exist. If the base block is incomplete or invalid, the object is skipped by the structured view while the original source text remains preserved in the document.

### Scenery-object metadata

`OmsiSceneryObjectReader` uses the same preservation-oriented parser for `.sco` files and exposes only clearly identifiable metadata:

- `[friendlyname]`;
- the hierarchy declared by `[groups]`;
- references declared by `[mesh]`;
- references declared by `[collision_mesh]`.

Block order and all commands that are not interpreted yet remain preserved in the source document. At this stage the editor does not interpret materials, scripts or animations. For `.o3d` files Core validates the header, inventories sections and can decode vertex positions, normals, UVs and triangle indices when the file is not encrypted. Data is converted from OMSI's Z-up system to the Y-up system used by the viewport. Encrypted files, `.x` files and models above the safety limits remain without a geometry preview.

`OmsiSceneryObjectPathResolver` restricts `.sco` resolution to the selected installation's `Sceneryobjects` directory and rejects directory traversal or different extensions.

`OmsiSceneryMeshPathResolver` resolves `[mesh]` and `[collision_mesh]` references from the `model` directory associated with the `.sco`. Both `.o3d` and `.x` files are accepted, including relative cross-package references with `..\`, as long as the final path remains inside `Sceneryobjects`. React receives only the declared path and found/missing status; the machine's absolute path is not exposed. For found `.o3d` meshes, the host also sends safe header metadata such as version and encryption state, plus structural vertex, triangle, material and bone counts.

## MapStudio.Desktop

Windows host responsible for native file and folder access, Core services, WebView2 lifecycle and communication between C# and the interface.

The desktop host must not become the primary editor UI.

### C# ↔ React bridge

The interface sends small commands through WebView2. The host performs only operations that require native machine access and returns JSON messages containing real state.

React cannot provide arbitrary paths for reading. The host keeps a list of `.sco` paths discovered from objects in maps that have already been loaded; only those paths can request metadata.

### On-demand loading

Initial discovery reads only `global.cfg` files and tile references. It does not open every `.map` file in the installation before releasing the interface.

- `loadMapContent` loads the selected map on demand and gets counts plus objects from a single read of each tile;
- `global.cfg` files are read with limited concurrency and visible progress; one unreadable map is skipped instead of blocking the whole installation;
- each `global.cfg` read has a time limit to prevent indefinite waiting;
- selected-map tiles are processed with limited concurrency to reduce waiting without saturating storage;
- `loadSceneryObjectMetadata` loads the `.sco` only when an object is selected;
- `loadSceneryObjectGeometry` loads only unencrypted `.o3d` meshes for the selected object;
- previously read metadata and geometry are cached in React.

This avoids scanning every `.map` and `.sco` file in the installation at application startup. The host sends discovery start and progress events to the interface, and unexpected exceptions are converted into visible errors to prevent a permanent “Reading OMSI” state. When returning to a previously loaded map, React reuses the cached content.

For Cartesian maps, the viewport represents object positions with:

- `worldX = tileX * 300 + objectX`;
- `worldZ = tileY * 300 + objectY`;
- `worldY = objectZ`.

For maps with `[worldcoordinates]`, objects are parsed and counted, but global markers remain hidden until the correct geographic conversion exists.

## MapStudio.UI

Primary editor interface, responsible for the Babylon.js viewport, asset browser, property inspector, construction tools, visual validation and simplified editing experience.

Production state must come from real data supplied by Core/Desktop.

### Object selection in the viewport

On Cartesian maps, a short click finds the placed object nearest to the camera ray without creating one individual mesh per object.

The inspector displays real `.map` values and, when available, real `.sco` metadata: friendly name, groups, meshes and collision meshes. Each mesh reference also reports whether the corresponding file was found in the installation.

Dragging the camera is not treated as selection. Clicking an area without an object clears the selection.

At this stage selection is read-only. When available, the selected object's real O3D geometry is displayed using embedded O3D materials: diffuse color, alpha, specular and emission. Each triangle keeps its material index and the embedded texture name is preserved for the next image-loading stage. The editor does not modify or save transforms yet.

## Compatibility strategy

OMSI configuration files are command-oriented text files and older maps may use legacy encodings.

The parser keeps the complete line stream, detects UTF-8/UTF-16 where identifiable, and falls back to Windows-1252. The original encoding and BOM presence are retained by the document to support safe writes.

Unknown commands remain stored and must survive an unchanged read/write round-trip.

## First vertical slice

1. User selects the OMSI 2 root directory.
2. Core discovers maps containing `global.cfg`.
3. `global.cfg` is parsed without destructive rewriting.
4. Real tile references are extracted.
5. The interface immediately receives the lightweight installation catalog.
6. Only the selected map has its `.map` files inspected.
7. Each selected tile is read once for both statistics and objects.
8. The real tile layout and object/spline statistics are displayed.
9. The base placed-object block is interpreted safely.
10. Placed objects can be selected and inspected.
11. The selected object's `.sco` provides real metadata on demand.
12. Found `.o3d` mesh headers and sections are validated and inventoried.
13. Vertices, normals, UVs and triangles from unencrypted O3D meshes are loaded on demand.
14. The selected object's real model is displayed in the viewport with a neutral material.
15. Embedded O3D materials are applied per triangle in the preview.
16. O3D textures and `[matl_*]` extensions are loaded incrementally.
17. Splines and terrain are implemented incrementally.

## Documentation rule

All official documentation must have equivalent `pt-BR` and `en` versions. A documentation change is only complete when both languages are updated.
