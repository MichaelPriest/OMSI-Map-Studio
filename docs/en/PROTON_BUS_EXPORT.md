# Proton Bus export — initial technical specification

This document records the research and initial architecture for adding Proton Bus as an OMSI Map Studio export target without mixing Proton Bus rules into OMSI parsers/writers.

## Branch state

Branch: `feature/protonbus-map-export`

Confirmed starting base on 2026-09-23:

`5bf80d739d7cdb57a19f0e106e4d2b85c82032f5`

This work does not modify `main`.

Proton Bus support must not yet be advertised as complete. This first stage only adds:

- map manifest model;
- `.map` writer;
- package layout;
- portable name/path validation;
- constants for special mesh-name tags;
- unit tests;
- this implementation plan.

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

A Proton Bus map uses a text `.map` file plus a base directory below `maps`.

Example:

```text
maps/
  City.map
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
3DS + TXT + PNG + .map
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

This is a separate stage after validating the currently used target-build format against a real example map.

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

### P1 — in progress

Completed:

- `ProtonBusExportScene` intermediate model;
- 3DS chunk planning and automatic splitting;
- local index remapping for 3DS limits.

Pending:

- explicit coordinate conversion;
- road/spline tessellation;
- terrain triangulation;
- complete material conversion.

### P2 — started

Completed:

- native binary `.3ds` writer for static meshes;
- material, object, vertex, face, face-material and UV chunks;
- full object/texture names without the legacy 12-character truncation;
- diffuse textures;
- basic transparency and self-illumination;
- automatic splitting for large meshes.

Pending:

- validate generated files directly in the target Proton Bus build;
- complete PNG/material pipeline;
- colliders generated from Map Studio properties;
- final emissive/additive rules;
- coordinate-axis conversion validated against a real fixture.

### P3

- entry points;
- stops/passengers;
- pedestrians;
- AI vehicles;
- trains.

### P4

- traffic lights;
- street lights;
- GPS/routes;
- full package validation.

### P5

- Phase 4 features;
- prefabs;
- automatic vegetation;
- target-specific optimization;
- PC/mobile profiles.

### P6

- `Export > Proton Bus` UI;
- pre-export report;
- target version selection;
- ZIP output;
- validation against a real example map.

## Requirement before advertising Proton Bus support

The Proton Bus adapter should only be registered as functional in `MapStudioSimulatorRegistry` after:

1. a simple map can be exported without Blender;
2. the package opens in the selected Proton Bus build;
3. terrain, roads, and scenery render correctly;
4. collisions work;
5. at least one route with a stop works;
6. essential traffic/pedestrian behavior is validated;
7. automated tests pass;
8. a small redistributable or generated fixture map exists.

Until then, `MapStudioSimulatorIds.ProtonBus` remains only a known simulator ID.
