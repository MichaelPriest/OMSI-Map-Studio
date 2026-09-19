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

### Placed splines

For `[spline]` and `[spline_h]`, Core conservatively interprets the base map block: `.sli` path, chain IDs, position, rotation, length, radius and start/end gradients. Later values remain in `ExtraValues`.

On Cartesian maps, the viewport uses these values to draw only the real spline axis. Straight segments use length and rotation; curves use the declared radius; axis height interpolates the start/end gradients. This preview does not invent road width, profile or texture.

`OmsiSplineDefinitionReader` reads the selected `.sli` on demand. `[texture]` provides texture names and each `[profile]` is associated with two `[profilepnt]` entries, preserving width, height, horizontal texture coordinate and longitudinal repetition factor. The path passes through `OmsiSplinePathResolver` and must remain inside `OMSI 2/Splines`.

When a spline is selected, the viewport extrudes recognized profile surfaces along its real length, radius and gradients. Other splines remain lightweight axes. Image textures are not loaded in this alpha; the profile uses a neutral material so the editor does not fake the road appearance.

Maps using `[worldcoordinates]` do not display global spline axes until the geographic conversion is implemented.

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

Selecting the OMSI root no longer triggers automatic map discovery. The root is only the trusted base for `maps`, `Sceneryobjects`, `Splines`, textures and other resources.

The user explicitly opens a map through the **Open map** button. The host accepts only a directory inside `OMSI 2/maps` that contains `global.cfg`. Only that `global.cfg` is read, followed by on-demand loading of the selected map's tiles.

- `selectOmsiRoot` only validates and registers the OMSI root;
- `selectMap` opens a native picker and reads only the chosen map's `global.cfg`;
- `loadMapRegion` loads only a tile window around the active tile;
- the default window is 3×3 (radius 1), so large maps do not open every `.map` file at once;
- previously read tiles are cached by the host for the duration of the map session;
- clicking another visible tile makes it the active center and loads the required neighbors;
- active-region tiles are processed with limited concurrency to reduce waiting without saturating storage;
- `loadSplineProfile` loads the `.sli` only when a spline is selected and caches the definition;
- `loadSceneryObjectMetadata` loads the `.sco` only when an object is selected;
- `loadSceneryObjectGeometry` loads only unencrypted `.o3d` meshes for the selected object;
- previously read metadata and geometry are cached in React.

This removes the full-map scan when selecting an installation and also avoids loading every tile in a large map. With no map open, the viewport remains empty; with a map open, only the active region receives objects, splines and other heavy data. Host exceptions continue to be converted into visible errors instead of leaving the interface stuck.

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

Selected objects can be moved and rotated as previews. When available, real O3D geometry is displayed using embedded O3D materials: diffuse color, alpha, specular and emission. Each triangle keeps its material index. Only transforms of existing placed objects can be persisted at this stage; creation/deletion and other edit types remain blocked.

## Compatibility strategy

OMSI configuration files are command-oriented text files and older maps may use legacy encodings.

The parser keeps the complete line stream, detects UTF-8/UTF-16 where identifiable, and falls back to Windows-1252. The original encoding and BOM presence are retained by the document to support safe writes.

Unknown commands remain stored and must survive an unchanged read/write round-trip.

## First vertical slice

1. User selects the OMSI 2 root directory.
2. No map is loaded automatically.
3. User clicks **Open map** and chooses a directory inside `maps`.
4. That map's `global.cfg` is parsed without destructive rewriting.
5. Real tile references are extracted.
6. Only that map's `.map` files are inspected.
7. Each tile is read once for statistics, objects and splines.
8. The real tile layout, real spline axes and map statistics are displayed.
9. The base placed-object block is interpreted safely.
10. Placed objects can be selected and inspected.
11. The selected object's `.sco` provides real metadata on demand.
12. Found `.o3d` mesh headers and sections are validated and inventoried.
13. Vertices, normals, UVs and triangles from unencrypted O3D meshes are loaded on demand.
14. The selected object's real model is displayed in the viewport with a neutral material.
15. Embedded O3D materials are applied per triangle in the preview.
16. O3D textures and `[matl_*]` extensions are loaded incrementally.
17. Splines can be selected and inspected directly in the viewport.
18. The selected spline's real `.sli` profile is loaded on demand and extruded with a neutral material.
19. Spline textures and terrain are implemented incrementally.

## Documentation rule

All official documentation must have equivalent `pt-BR` and `en` versions. A documentation change is only complete when both languages are updated.


### Full-map O3D catalog

In **Full map** mode, React derives the unique `.sco` path list from placed objects received from the host. Geometries are requested sequentially through the existing `loadSceneryObjectGeometry` command and cached by path.

The viewport groups placements by `sceneryObjectPath`. For each model with valid geometry it:

1. creates base meshes split by material;
2. uses the first placement as the source;
3. reuses the same geometry/material buffers through clones for later placements;
4. keeps markers only for models that are still loading or unsupported.

This displays a complete map without rereading the same O3D for every instance.


## Preservation-safe writes and backups

`OmsiTileObjectEditor` receives an already parsed document plus a list of object transform edits. Each object has a `SourceSectionOrdinal` assigned by `OmsiTileReader`, together with its object ID and `.sco` path.

Before changing any line, the editor verifies all three identifiers. This protects against external changes that alter tile structure between load and save.

Only six data lines inside the `[object]` block are replaced:

- X;
- Y;
- Z;
- rotation;
- pitch;
- bank.

The rest of the document remains preserved, including encoding, BOM, newline style, comments, unknown commands and `ExtraValues`.

`SafeFileTransaction` prepares the whole batch before touching destination files:

1. validates unique existing targets;
2. creates backups under `.mapstudio-backups/<timestamp>/`;
3. writes each new version to a temporary file beside its tile;
4. replaces destinations using file replacement;
5. on failure, attempts to restore already replaced files from backups;
6. keeps backups even if an error occurs.

The host never writes from stale cached tile content: Save reopens the current tile from disk, applies the minimal mutation and only then executes the transaction.


### Sceneryobjects library

The `loadSceneryLibrary` command is only triggered on demand. The host walks `Sceneryobjects` on a background task using `EnumerationOptions`, without following reparse points and while ignoring inaccessible directories.

Only relative `Sceneryobjects\\...\\file.sco` paths and filenames are sent to React. Discovered paths are also added to the host's known-object allowlist. Results remain cached until the OMSI installation changes.


## Preservation-safe object insertion

`OmsiTileObjectInserter` appends a new `[object]` block to the preserved document without reserializing existing sections.

The writer appends:

- `[object]`;
- a `HeaderValue` derived from a real instance of the same `.sco`;
- the `.sco` path;
- a new global ID;
- X, Y, Z;
- rotation, pitch and bank;
- `ExtraValues` copied from the real template.

The optional textual `Object Nr. ...` line is not generated because it is not part of the functional `[object]` block.

### Global ID allocation

Before insertion, the host rereads current tiles directly from disk. `OmsiTileElementIdScanner` searches IDs in known map element types that participate in map numbering:

- `[object]`;
- `[attachObj]`;
- `[splineAttachement]` / `[splineAttachment]`;
- `[splineAttachement_repeater]` / the `Attachment` spelling variant;
- `[spline]`;
- `[spline_h]`.

The new ID is `largest ID found + 1`. Content parsing and ID analysis reuse the same `OmsiConfigDocument` per tile to avoid parsing twice.

If no instance of the same `.sco` exists to act as a template, persistence is rejected with `objectInsertTemplateUnavailable`.

The final write reuses `SafeFileTransaction`, so the target tile is backed up under `.mapstudio-backups/<timestamp>/` before atomic replacement.
