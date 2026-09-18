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
- future real `.o3d` geometry;
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

Block order and all commands that are not interpreted yet remain preserved in the source document. At this stage the editor does not interpret materials, scripts or animations. For `.o3d` files Core validates the header and walks only the binary structure required to inventory sections. At this stage it obtains vertex, triangle, material and bone counts and detects the final transform without decoding vertices for rendering.

`OmsiSceneryObjectPathResolver` restricts `.sco` resolution to the selected installation's `Sceneryobjects` directory and rejects directory traversal or different extensions.

`OmsiSceneryMeshPathResolver` resolves `[mesh]` and `[collision_mesh]` references from the `model` directory associated with the `.sco`. Both `.o3d` and `.x` files are accepted, including relative cross-package references with `..\`, as long as the final path remains inside `Sceneryobjects`. React receives only the declared path and found/missing status; the machine's absolute path is not exposed. For found `.o3d` meshes, the host also sends safe header metadata such as version and encryption state, plus structural vertex, triangle, material and bone counts.

## MapStudio.Desktop

Windows host responsible for native file and folder access, Core services, WebView2 lifecycle and communication between C# and the interface.

The desktop host must not become the primary editor UI.

### C# ↔ React bridge

The interface sends small commands through WebView2. The host performs only operations that require native machine access and returns JSON messages containing real state.

React cannot provide arbitrary paths for reading. The host keeps a list of `.sco` paths discovered from objects in maps that have already been loaded; only those paths can request metadata.

### On-demand loading

Initial discovery sends only the catalog, tiles and counts.

- `loadMapObjects` loads `[object]` blocks only when a map is selected;
- `loadSceneryObjectMetadata` loads the `.sco` only when an object is selected;
- previously read metadata is cached in React.

This avoids scanning every `.sco` in the installation at application startup.

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

At this stage selection is read-only. The editor does not modify or save transforms yet.

## Compatibility strategy

OMSI configuration files are command-oriented text files and older maps may use legacy encodings.

The parser keeps the complete line stream, detects UTF-8/UTF-16 where identifiable, and falls back to Windows-1252. The original encoding and BOM presence are retained by the document to support safe writes.

Unknown commands remain stored and must survive an unchanged read/write round-trip.

## First vertical slice

1. User selects the OMSI 2 root directory.
2. Core discovers maps containing `global.cfg`.
3. `global.cfg` is parsed without destructive rewriting.
4. Real tile references are extracted.
5. Existing `.map` tile files are inspected.
6. Real state is sent to the React interface.
7. The real tile layout and object/spline statistics are displayed.
8. The base placed-object block is interpreted safely.
9. Placed objects can be selected and inspected.
10. The selected object's `.sco` provides real metadata on demand.
11. Found `.o3d` mesh headers and sections are validated and inventoried without rendering geometry.
12. Real vertex/UV/normal reading is added for unencrypted meshes.
13. `.o3d` geometry, splines and terrain are rendered incrementally.

## Documentation rule

All official documentation must have equivalent `pt-BR` and `en` versions. A documentation change is only complete when both languages are updated.
