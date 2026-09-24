# Proton Bus export — initial technical specification

This document records the research and initial architecture for adding Proton Bus as an OMSI Map Studio export target without mixing Proton Bus rules into OMSI parsers/writers.

## Branch state

Branch: `feature/protonbus-map-export`

Confirmed starting base on 2026-09-23:

`5bf80d739d7cdb57a19f0e106e4d2b85c82032f5`

This work does not modify `main`.

Proton Bus support must not yet be advertised as game-validated, but this branch now contains a functional Core export pipeline plus initial native-app integration.

Implemented on this branch:

- `.map.txt` manifest and complete package layout;
- single-tile, multi-tile, and full OMSI map-directory export through `global.cfg`;
- terrain triangulation, spline tessellation, and scenery-object conversion;
- automatic resolution of `.sli`, `.sco`, `.o3d`, `.x`, and their dependencies;
- DDS/TGA/BMP/JPG/JPEG/GIF to PNG transcoding plus safe copying of already-valid PNG files;
- automatic vehicle, pedestrian, and train paths with 3D markers and linked-OMSI-spline merging when geometry is compatible;
- `TTData` reading for bus stops, entrypoints, and GPS routes;
- synchronized OMSI traffic-light controller conversion;
- street lights derived from OMSI light points;
- native 3DS writer with materials, UVs, Proton tags, and mesh splitting;
- native `File > Export to Proton Bus...` menu independent of Inspector;
- automated Proton Bus Core validation plus native WinUI host compilation on this branch.

Visual/runtime validation in an actual Proton Bus build is still required before final compatibility can be claimed.

## Sources researched

Primary references:

- official 2019 experimental map-mod tutorial:
  https://blog.protonbus.com.br/2019/09/experimental-mods-de-mapas-no-pbs.html
- official Phase 4 page:
  https://www.protonbus.com.br/arvores/
- Phase 3 tutorial by Marcos Elias, updated September 2021, found through mirrored copies:
  https://pt.scribd.com/document/618606511/Tutorial-Mods-de-Mapas-Fase-3

The mirrored Phase 3 document should be treated as a historical technical reference until checked against the help/example files shipped with the exact target Proton Bus build.

## Confirmed basic structure

For the documented Phase 2/3 format, a Proton Bus map uses a text `.map.txt` file plus a base directory below `maps`.

Example:

```text
maps/
  City.map.txt
  City/
    dest/
    skins/
    textures/
    tiles/
      Route 1/
        *.3ds
        aipeople/
        aitrains/
        aivehicles/
        busstops/
        trafficlights/
        streetlights/
```

Basic manifest:

```ini
[map]
baseDir=City
modelsDir=Route 1
textures=textures
mapModVersion=3
preview=preview
```

For the Phase 3 generation, `mapModVersion=3` enables that generation of map features. Later versions must be validated against the exact target Proton Bus build before Map Studio emits them automatically.

## Important compatibility rules

Proton Bus interprets special names on 3D objects. Documented tags include:

- `_transparent_` — transparency;
- `_gencol_` — generated collider;
- `_invisible_` — invisible object, often combined with a collider;
- `_emissive_` — emissive material;
- `_additive_` — additive shader, recommended for traffic-light lamps;
- `_low_speed_zone_` — low-speed area;
- `_force_exit_` — forces passenger exit.

Historical documentation also recommends file names without accents, cedilla, or special characters to avoid cross-platform differences.

PNG textures are the safest option in the historical guides. For mobile targets, the old documentation recommends avoiding textures above 2048 px.

## Structural difference from OMSI

Proton Bus must not be implemented as an OMSI file-extension conversion.

OMSI relies heavily on:

- tiles;
- splines;
- scenery objects;
- external references;
- paths;
- timetable files;
- per-tile terrain.

Historically Proton Bus consumes scene geometry as `.3ds` files plus associated TXT configuration, with the map loaded in a much more aggregated way.

The target pipeline is therefore:

```text
Map Studio Domain
       |
       +-- geometry/terrain
       +-- roads and sidewalks
       +-- scenery
       +-- paths
       +-- stops
       +-- traffic lights
       +-- street lights
       +-- traffic/pedestrians/trains
       |
       v
ProtonBus Export Scene
       |
       +-- spline tessellation
       +-- coordinate transforms
       +-- bake/instancing by target capability
       +-- Proton materials/tags
       +-- marker meshes
       |
       v
3DS + TXT + PNG + .map.txt
```

## Initial Map Studio -> Proton Bus mapping

### Terrain

Map Studio terrain must be triangulated into exportable meshes. OMSI tiles must not remain a runtime requirement in the final Proton package.

### Roads and splines

Splines need to become real mesh geometry, including:

- road surface;
- sidewalks;
- curbs;
- markings;
- shoulders when present;
- elevation;
- curves;
- bridges/tunnels.

Driveable/contact surfaces should receive suitable colliders without turning the entire map into one giant physics mesh.

### Scenery objects

Objects may be:

1. baked into exported `.3ds` models; or
2. translated to Phase 4 reusable prefabs when the selected target build supports them.

This choice belongs to an export profile.

### Bus stops and passengers

Positions edited visually in Map Studio should generate:

- required marker objects in 3D;
- files under `busstops/`;
- boarding/alighting data;
- destination/route data when applicable.

### Pedestrians, traffic and trains

Map Studio internal paths need translation to the position objects and TXT structures expected in:

- `aipeople/`;
- `aivehicles/`;
- `aitrains/`.

The main workflow must not depend on Inspector. Inspector may expose advanced properties, but creation and editing must work through the visual tools.

### Traffic lights

Map Studio traffic-light state must translate to:

- machine definition under `trafficlights/`;
- unique prefix;
- path count;
- ticks/states;
- timing;
- red/yellow/green lamps;
- blocking triggers;
- matching marker meshes inside the exported 3D.

### Lighting

Street/ambient light types should generate files under `streetlights/` plus the real/fake light marker meshes required by the selected target.

### GPS/routes

Core now generates GPS meshes from OMSI timetable routes and uses the documented `_gps_<entrypoint>_` naming convention, including additional pieces for the same route.

Automatic generation is implemented; visual/navigation behavior still needs validation in the target Proton Bus build.

## 3DS exporter

The preferred direction is a native 3DS writer inside Map Studio.

Reasons:

- no mandatory Blender 2.79 installation;
- preserve long object names used by Proton commands;
- export directly from geometry already loaded by the editor;
- deterministic automated testing;
- avoid fragile external automation.

Blender can later be offered as an interoperability option, not as a required dependency.

The writer needs at minimum:

- vertices;
- triangle faces;
- UV coordinates;
- materials;
- texture references;
- transforms;
- object names;
- automatic splitting for 3DS format limits;
- preservation of Proton name tags.

## Phase 4

The official Phase 4 page describes:

- automatic/random vegetation;
- automatic/random prefabs;
- manually positioned prefabs;
- custom traffic;
- internal world grids for optimization.

These features should only be implemented after inspecting the help/example files for the selected target build. Map Studio must not invent an undocumented file format from the feature description alone.

## Implementation stages

### P0 — completed on this branch

- `ProtonBusMapDefinition`;
- manifest writer;
- package layout;
- portable validation;
- basic name tags;
- tests.

### P1 — completed in Core

- `ProtonBusExportScene` intermediate model;
- explicit conversion from Map Studio Y-up coordinates to the 3DS space used by the Proton pipeline;
- triangle winding correction after the Y/Z swap;
- OMSI spline tessellation including straights, curves, profiles, UVs and gradients;
- OMSI terrain triangulation per tile;
- bilinear terrain sampling for object placement;
- conversion of OMSI geometry loaded by Core (`.o3d` and `.x`) with placement, rotation, scale and UVs;
- relative height and `[absheight]` semantics;
- materials with texture, diffuse color, opacity and basic emissive state;
- globally unique material names per tile/object;
- 3DS chunk planning and automatic splitting;
- local index remapping for 3DS limits;
- per-tile `ProtonBusExportScene` assembly with missing-asset reporting.

### P2 — completed in Core; in-game validation pending

Completed:

- native binary `.3ds` writer for static meshes;
- material, object, vertex, face, face-material and UV chunks;
- full object/texture names without the legacy 12-character truncation;
- diffuse textures;
- basic transparency and self-illumination;
- automatic splitting for large meshes;
- automatic DDS/TGA/BMP/JPG/JPEG/GIF to PNG transcoding;
- PNG signature validation;
- OMSI collision meshes converted with `_gencol_` + `_invisible_` tags;
- protection against cross-tile texture-name collisions.

Pending:

- validate generated files directly in the target Proton Bus build;
- refine final collider rules from observed in-game behavior;
- validate final emissive/additive behavior in the Proton renderer;
- use the generated preview fixture to visually validate axes, winding, UVs and materials inside Proton Bus.

### P3 — completed in Core

- entrypoints generated from the first stop of OMSI trips;
- OMSI bus stops resolved by `TileIndex` + placed-object ID and written under `busstops/`;
- 3D bus-stop trigger markers;
- pedestrian paths under `aipeople/`;
- vehicle paths under `aivehicles/`;
- train paths under `aitrains/`;
- optional merging of linked splines into one continuous Proton path;
- safe per-spline fallback when endpoints/types/directions are incompatible;
- deterministic prefixes by tile and entity.

Note: automatically generated passenger waiting positions remain conservative; `paxAmount=0` is the default so Map Studio does not invent sidewalk waiting points.

### P4 — Core implementation completed; in-game validation pending

- OMSI traffic-light machines converted to `trafficlights/`;
- synchronized OMSI programs/phases converted to Proton Bus ticks;
- traffic-light triggers and marker meshes;
- OMSI light points converted to `streetlights/`;
- GPS/routes generated from timetable data;
- multi-tile aggregation of these functional resources.

Pending:

- validate timing, triggers, lights, GPS, and traffic behavior in the target Proton Bus build;
- refine map-specific edge cases discovered through real-map testing.

### P5

- Phase 4 features;
- prefabs;
- automatic vegetation;
- target-specific optimization;
- PC/mobile profiles.

### P6 — completed for technical preview

Completed:

- native `File > Export to Proton Bus...` menu;
- direct export of the currently open map without Inspector dependency;
- fields for map name, base directory, and model/route set;
- option to include TTData, bus stops, entrypoints, and GPS;
- detailed pre-export report in a dedicated dialog;
- non-writing preflight with blockers/warnings and counts for tiles, meshes, textures, paths, stops, entrypoints, GPS, traffic lights, and street lights;
- explicit **Map Mods Phase 3** target profile (`mapModVersion=3`);
- custom profile with unvalidated-compatibility warning;
- optional ZIP output;
- progress and primary errors surfaced through app status;
- branch CI builds the WinUI host in addition to running Proton Bus tests;
- small deterministic redistributable Proton Bus fixture generated by Core;
- CLI fixture generator;
- branch-specific preview release containing portable build, installer, SHA-256 files, and fixture.

Current preview:

`v0.2.0-alpha.5-test.10.13-protonbus`

Also includes:

- `MapStudio-ProtonBus-Validation-Fixture.zip`;
- fixture SHA-256;
- fixture installation/diagnostic README.

Pending:

- validate the preview and fixture in a real Proton Bus build;
- validate a converted real OMSI map;
- refine visual/functional differences found in-game;
- evaluate optional automatic passenger-position generation.

## Requirement before advertising Proton Bus support

The Proton Bus adapter should only be registered as functional in `MapStudioSimulatorRegistry` after:

1. a simple map can be exported without Blender;
2. the package opens in the selected Proton Bus build;
3. terrain, roads, and scenery render correctly;
4. collisions work;
5. at least one route with a stop works;
6. essential traffic/pedestrian behavior is validated;
7. automated tests pass;
8. a small redistributable or generated fixture map exists — **completed on this branch**.

Until in-game validation is complete, `MapStudioSimulatorIds.ProtonBus` remains an experimental target and must not be advertised as final compatibility.


## Current technical preview

Tag:

`v0.2.0-alpha.5-test.10.13-protonbus`

Release:

https://github.com/MichaelPriest/OMSI-Map-Studio/releases/tag/v0.2.0-alpha.5-test.10.13-protonbus

Main assets:

- portable win-x64;
- win-x64 installer;
- SHA-256 files;
- `MapStudio-ProtonBus-Validation-Fixture.zip`;
- fixture SHA-256;
- fixture README.

The fixture covers ground/collision, vehicle path, pedestrian path, train path, bus stop, entrypoint, GPS, traffic light, and street light. Its purpose is to isolate Proton Bus format/runtime issues from large OMSI-map conversion issues.
