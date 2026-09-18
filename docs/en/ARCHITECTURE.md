# Architecture

**English** · [Português (Brasil)](../pt-BR/ARCHITECTURE.md)

OMSI Map Studio is an independent product and must not depend on OMSI NavBR Multiplayer code, runtime services or releases.

## MapStudio.Core

Owns OMSI-facing domain logic:

- configuration parsing;
- detection and preservation of source file encodings;
- map discovery;
- safe `.map` tile file reading;
- tiles;
- future scenery objects;
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

Internal object and spline fields must not be interpreted by position until dedicated models and tests exist for those structures.

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
8. Objects, splines and terrain are interpreted and rendered incrementally.

## Documentation rule

All official documentation must have equivalent `pt-BR` and `en` versions. A documentation change is only complete when both languages are updated.
