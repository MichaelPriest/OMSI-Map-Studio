# Technical and functional roadmap

**English** · [Português (Brasil)](../pt-BR/ROADMAP.md)

> This document is the source of truth for OMSI Map Studio's technical and functional direction. Architecture changes, new tool families, and important compatibility gaps with the original OMSI editor must be recorded here before or together with implementation.

## Vision

OMSI Map Studio is not intended to visually copy the old OMSI editor. The goal is to **replace it functionally**, preserve compatibility with real OMSI 2 formats, and provide a modern city-builder-like construction experience.

The long-term goal is to let a map be created, loaded, edited, validated, and prepared for OMSI operation without requiring the original editor for essential tasks.

Permanent principles:

- no fake/mock production data;
- C# remains authoritative for OMSI files, formats, paths, and persistence;
- React must not invent state that belongs to Core;
- unknown commands must be preserved;
- persisted edits should have safe backup/undo whenever technically possible;
- partial parsing must never cause silent data loss;
- the project remains fully separate from OMSI NavBR Multiplayer;
- official documentation must exist in pt-BR and en;
- compatibility and safe writing take priority over convenience;
- modern UX must not change OMSI file semantics.

---

## 1. Official technology direction

### Stack to keep

The target architecture remains:

- **.NET 10 / C#** for domain logic, parsing, indexing, cache, validation, and writing;
- **WPF** as the Windows host and native integration layer;
- **WebView2** for hosting the modern interface;
- **React + TypeScript + Vite** for UI;
- **Babylon.js** for the 3D viewport, picking, materials, instancing, LOD, and editing tools;
- persistent local storage for indices/caches; the initial architecture choice is **SQLite**, optionally accompanied by binary cache files for heavy geometry.

No general migration to Unity, Unreal, or a custom C++ engine is planned.

### Why keep this architecture

It preserves clean responsibility boundaries:

    OMSI files / installation
              ↓
        MapStudio.Core
              ↓
      Asset Index / Cache
              ↓
       Desktop Bridge
              ↓
     React + Babylon.js

Core understands OMSI. The renderer understands geometry. The UI understands interaction. None of these layers should take ownership of another layer's responsibility.

### WebGL / WebGPU

Babylon remains the rendering layer. WebGPU may be adopted later when it is sufficiently reliable on the target WebView2 runtime, with a compatible fallback. Graphics-backend evolution must not require rewriting OMSI parsers.

---

## 2. Using the original OMSI editor as a reference

The original editor is OMSI itself running in editor mode, so it directly shares the simulator's loading infrastructure.

Map Studio is independent and therefore must explicitly reproduce the required knowledge.

Architectural behaviors to use as a reference:

- maps are divided into tiles;
- loading is driven by the active/visible region;
- objects, splines, terrain, and other data are associated with tiles;
- assets are accessed when required;
- the catalog of asset types grows as used content is encountered;
- the map should not need to become one monolithic 3D scene before it can be edited.

For traditional Cartesian maps, the OMSI tile remains a 300 × 300 m spatial unit when the real format uses that convention.

Map Studio does not need to reproduce the old editor's limitations. The reference is **OMSI semantics**, not the old UI.

---

## 3. New loading architecture

### 3.1 Persistent Asset Index

Create a local persistent index of the OMSI installation.

The index should progressively cover:

- `Sceneryobjects`;
- `Splines`;
- `Texture` and package-referenced textures;
- `.sco`;
- `.sli`;
- `.o3d`;
- trees;
- collision meshes;
- scripts/metadata as required;
- dependencies between assets;
- resolved paths;
- file fingerprints;
- available/missing/protected/invalid state.

The index never replaces OMSI files. It is derived data and can be rebuilt.

### 3.2 Derived cache

Cache may contain:

- parsed metadata;
- bounds;
- renderer-ready geometry;
- resolved materials;
- texture information;
- thumbnails;
- 3D previews;
- dependencies;
- expensive validation results;
- normalized spline profile data for preview;
- hashes/fingerprints for invalidation.

Files changed outside Map Studio must invalidate only affected entries.

### 3.3 Incremental indexing

The first index may scan the entire installation, but later runs should:

1. compare path + size + timestamps/hashes as required;
2. reuse valid entries;
3. update only new/changed/removed assets;
4. update dependents only when the change affects them.

### 3.4 Tile-based map streaming

Gradually replace the manual “Full map / 3×3” split with automatic streaming.

Target model:

- **ring 0 — camera/selection tile:** full data, highest priority;
- **ring 1 — immediate neighbors:** full terrain, splines, objects, and picking;
- **ring 2 — nearby region:** simplified/LOD visuals and metadata;
- **rest of map:** topology, bounds, minimap, lightweight metadata;
- unused assets may leave GPU memory while remaining in CPU/disk cache.

“Full map” may remain as a diagnostics option but should not be required for normal work.

### 3.5 Loading priority

Preferred map-opening order:

1. `global.cfg` and topology;
2. tiles and bounds;
3. active-region terrain;
4. active-region splines/roads;
5. visible objects;
6. textures and progressive detail;
7. distant content;
8. low-priority work such as thumbnails.

The interface should become usable before the entire installation has finished processing.

### 3.6 Renderer

Use, whenever safe:

- frustum culling;
- instancing/thin instances for repeated objects;
- LOD;
- GPU resource eviction outside the relevant region;
- texture reuse;
- batching where it does not break individual selection;
- simplified distant meshes;
- picking mapped to real OMSI identity, including instances.

---

## 4. Functional foundation already achieved

Map Studio already has foundations that are more modern than the original editor in several workflows:

- real map catalog;
- preservation-oriented parsing;
- real `.sco` objects;
- real `.o3d` geometry when supported;
- evolving real material/texture support;
- real `.sli` splines;
- real terrain;
- mesh picking plus geometric fallback;
- object/spline transforms;
- preview before writing;
- backups;
- undo/redo;
- visual library;
- groups/subgroups;
- favorites/recents/frequent/collections;
- previews and thumbnails;
- Single/Repeat/Line/Brush/Matrix/Circle/Lots construction;
- Construction Sets;
- start/end/curve road builder;
- snapping;
- bridges/elevated roads;
- terrain-assisted leveling;
- coordinates and geographic reference;
- real-map imagery;
- elevation grid;
- contextual Inspector;
- unified desktop/fullscreen interface;
- Map Health;
- dependency audit.

These capabilities must be preserved through all later phases.

---

## 5. Compatibility still required

Legend:

- ✅ already useful;
- 🟡 partial or read/preview only;
- ⬜ still requires a real editing tool.

| Area | Current target state |
|---|---|
| `.sco` objects | ✅ |
| `.sli` splines | ✅ |
| Select / move / rotate | ✅ |
| Spline curve, length, gradient | ✅ |
| Modern library / preview | ✅ |
| Batch construction tools | ✅ |
| Coordinates / real reference / elevation | ✅/🟡 |
| Create/delete tiles | ⬜ |
| Full tile properties | ⬜ |
| Native tile/map water | ⬜ |
| Tile lightmap / lighting | ⬜ |
| Attach object → spline | 🟡 |
| Attach object → object | 🟡 |
| Editable parent/hierarchy | ⬜ |
| Object-specific labels/options | ⬜ |
| Spline Mirror | ⬜ |
| Cant start/end | ⬜ |
| Complete to… | ⬜ |
| Spline export | ⬜ |
| Editable paths | 🟡 |
| Traffic Rules | ⬜ |
| Speed limits | ⬜ |
| Traffic density | ⬜ |
| Vehicle restrictions | ⬜ |
| Road priorities | ⬜ |
| AI paths / crossing behavior | ⬜ |
| Traffic lights / signal phases | ⬜ |
| Tracks | ⬜ |
| Trips | ⬜ |
| Stops/stations | ⬜ |
| StationLinks | ⬜ |
| Time profiles | ⬜ |
| Timetable editor | ⬜ |
| Signal Routes | ⬜ |
| Railway priorities/switches | ⬜ |
| Environment settings | ⬜ |
| Chronology support | ⬜ |
| Operational map debugging | ⬜ |

---

## 6. Official implementation order

### Phase A — performance foundation

Highest priority before greatly expanding system count.

Implement:

- Asset Index;
- persistent cache;
- incremental invalidation;
- tile streaming;
- priority loading queue;
- memory/GPU management;
- safe instancing and LOD;
- internal map-opening metrics;
- cache diagnostics.

Completion criteria:

- a map becomes editable without reparsing every asset in the installation;
- moving the camera streams regions without losing item identity;
- selection and editing remain functional while streaming.

### Phase B — complete physical map editing

Implement:

- create tile;
- delete tile with confirmation and backup;
- full tile properties;
- real water editing;
- lightmap/lighting where the format is validated;
- object/spline attachments;
- parent/hierarchy;
- object options/labels;
- Mirror;
- Cant start/end;
- Complete to…;
- Spline Export;
- safe creation/editing of sections currently only preserved as extras.

Completion criteria:

- common physical operations from the original editor no longer require opening it;
- preservation-safe round-trip remains valid.

### Phase C — paths and traffic

Implement a visual path layer independent from rendered road geometry.

Tools:

- view paths;
- select path;
- create/remove/edit path once the format is validated;
- direction;
- lane/type;
- speed limit;
- traffic density;
- priorities;
- vehicle restrictions;
- cars;
- trucks;
- buses;
- pedestrians where applicable;
- path links;
- junctions;
- AI behavior;
- traffic lights/phases;
- broken/disconnected path validation.

Desired UX:

- colored/icon path overlay;
- rules Inspector;
- filters;
- errors highlighted in scene;
- visual editing without requiring internal numeric knowledge.

### Phase D — public transport and operations

Implement:

- stops;
- stations;
- station links;
- tracks;
- trips;
- lines;
- line direction;
- stop sequence;
- time profiles;
- journeys;
- timetables;
- service/calendar concepts where supported by OMSI formats;
- HOF/line validation where applicable;
- route visualization on the map.

Create a visual line editor:

    path → track → stops → trip → profile → timetable

Completion criteria:

- a bus line can be built and validated in Map Studio without returning to the old editor for the main operational workflow.

### Phase E — signals, rail, advanced rules

Implement:

- Signal Routes;
- signals;
- switches;
- railway priorities;
- crossing/level crossing;
- signal/route dependencies;
- conflict diagnostics;
- dedicated rail tools.

### Phase F — environment and chronology

After validating the formats:

- Environment;
- editable map environment/weather data;
- Chronology;
- period-conditioned objects/changes;
- chronology-state inspection tools.

### Phase G — modern creation beyond the original editor

Continue features that need not exist in the original editor:

- city-builder-style road creation;
- curve handles;
- Bézier-like authoring when safely convertible to OMSI's model;
- smart snapping;
- auto-intersection;
- asset-backed auto-junction assistance;
- parallel avenues;
- grid roads;
- parallel road mode;
- replace tool;
- visual elevation/depression;
- cuts/embankments;
- bridges/tunnels;
- lots;
- procedural vegetation;
- distribution rules;
- district presets;
- Construction Sets;
- procedural duplication;
- alignment tools;
- even distribution;
- multi-selection.

Every procedural tool must output real, inspectable OMSI data.

### Phase H — real-world maps and geodata

Complete:

- official `[worldcoordinates]` conversion only after validating the format;
- geographic anchors;
- elevation import;
- aerial/satellite reference;
- terrain image alignment;
- optional import of permitted/licensed vector data;
- assisted street tracing;
- conversion to real user-selected splines;
- DEM terrain reconstruction;
- datum/offset validation.

Never write a format that merely resembles OMSI worldcoordinates. It must either be compatible with real OMSI or remain Map Studio-only metadata.

### Phase I — validation and debugging

Create technical map checks for:

- missing assets;
- broken paths;
- disconnected splines;
- invalid attachments;
- duplicate IDs;
- out-of-map references;
- texture problems;
- incompatible/protected O3D;
- inconsistent traffic rules;
- broken line continuity;
- disconnected stops;
- incomplete timetable;
- invalid signal routes;
- missing tiles;
- invalid terrain;
- circular dependencies where applicable.

Map Health should evolve into the central surface for these checks.

---

## 7. UI tool model

The UI should keep moving toward modern city-builder interaction patterns without copying proprietary assets.

Desired structure:

- top menu for infrequent functions;
- viewport as the dominant surface;
- one quick toolbar shared by desktop/fullscreen;
- bottom construction bar;
- contextual asset shelf;
- Explorer;
- Inspector;
- movable panels;
- contextual tools only when needed;
- visual previews;
- clear hover/selection/error states;
- advanced tools accessible without cluttering basic mode.

### Future primary modes

- Selection;
- Objects;
- Roads/Splines;
- Junctions;
- Terrain;
- Water;
- Paths/Traffic;
- Transit/Routes;
- Rail/Signals;
- Environment;
- Validation.

---

## 8. Persistence rules

Before enabling any new write path:

1. understand the real block/format;
2. have representative fixtures/tests;
3. preserve encoding;
4. preserve unknown lines/sections;
5. create backup;
6. reopen successfully;
7. validate that OMSI still accepts the result;
8. avoid normalizing/reformatting unrelated content.

When safety is not established, the tool should remain preview/read-only instead of writing an invented format.

---

## 9. Required testing by feature family

Every new feature should consider:

- stock OMSI map;
- small map;
- large map;
- missing asset;
- protected asset;
- relative paths;
- non-UTF-8 encoding;
- repeated objects;
- curved splines;
- `spline_h` where applicable;
- neighbor tiles;
- tile-edge editing;
- fullscreen and desktop;
- undo/redo;
- save + reopen;
- backup;
- third-party map.

Operational systems must add dedicated fixtures for traffic rules, tracks/trips, timetable, and signals as each phase is implemented.

---

## 10. Definition of “OMSI editor replacement”

Map Studio will not be called a complete replacement merely because it renders maps.

To reach that milestone it must safely:

- open real maps;
- navigate all relevant tiles;
- edit terrain;
- create/delete tiles;
- place/edit/remove objects;
- place/edit/remove splines;
- edit attachments;
- edit essential full properties;
- edit paths and traffic rules;
- configure required junctions/signals;
- create stops/tracks/trips;
- create and edit timetables;
- validate the map;
- save without corrupting unknown content;
- reopen the result;
- produce a map accepted by OMSI 2.

After that milestone, modern features remain differentiators rather than parity requirements.

---

## 11. What we will not do

- rewrite everything in another engine for aesthetics alone;
- use mocks to hide a missing parser;
- silently convert assets;
- replace protected content with fake geometry;
- write unvalidated OMSI parameters;
- load the entire map and installation into GPU memory by default;
- couple this project to OMSI NavBR Multiplayer;
- sacrifice compatibility to exactly copy another game's UX.

---

## 12. Practical next sequence

From the current state, the recommended sequence is:

1. stabilize current UI/picking;
2. implement Asset Index + persistent cache;
3. implement automatic tile streaming;
4. complete missing physical operations;
5. implement paths + Traffic Rules;
6. implement transit: stops/tracks/trips/timetables;
7. implement signals/rail;
8. environment/chronology;
9. expand procedural/geographic tools;
10. consolidate Map Health/validation until operational parity is reached.

This order may move when a technical dependency requires it, but none of the feature families listed above should be forgotten.
