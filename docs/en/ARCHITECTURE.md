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
- tiles;
- future full scenery-object support;
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

## MapStudio.Desktop

Windows host responsible for native file and folder access, Core services, WebView2 lifecycle and communication between C# and the interface.

The desktop host must not become the primary editor UI.

### C# ↔ React bridge

The interface sends small commands through WebView2, such as `selectOmsiRoot`. The host performs only operations that require native machine access and returns JSON messages containing real state.

In the first functional flow:

1. React requests OMSI 2 installation selection.
2. The host opens a native `OpenFolderDialog`.
3. The host validates the presence of the `maps` directory.
4. `OmsiMapCatalog` reads real installed maps.
5. Each tile reference is inspected by `OmsiTileReader`.
6. The host sends real map names, paths, coordinates and counts to React.
7. The viewport uses those coordinates to draw the map tile layout.

Host failures are sent as stable error codes. The interface is responsible for presenting the appropriate user-facing message.

### On-demand object loading

Initial installation discovery sends only the catalog, tiles and counts. Full `[object]` blocks are loaded only after the user selects a map.

React sends `loadMapObjects` using only the directory name of a map already known by the host. The desktop resolves that name against its internal catalog; the interface does not provide an arbitrary file path.

For Cartesian maps, the viewport can represent an object position with:

- `worldX = tileX * 300 + objectX`;
- `worldZ = tileY * 300 + objectY`;
- `worldY = objectZ`.

At this stage these points are position markers only. They do not represent the real geometry of the `.sco` file.

For maps with `[worldcoordinates]`, objects are parsed and counted, but global markers remain hidden until the correct geographic conversion exists.

## MapStudio.UI

Primary editor interface, responsible for the Babylon.js viewport, asset browser, property inspector, construction tools, visual validation and simplified editing experience.

Production state must come from real data supplied by Core/Desktop.

The grid shown without an opened map is only editor-space visual guidance. Once a map is loaded, the tile layout must be generated from real coordinates read from `global.cfg`. Referenced tiles whose `.map` file is missing are highlighted separately.

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
9. Objects, splines and terrain are interpreted and rendered incrementally.

## Documentation rule

All official documentation must have equivalent `pt-BR` and `en` versions. A documentation change is only complete when both languages are updated.
