# Development priorities — OMSI Map Studio

Updated for branch `feature/native-winui-d3d11`.

This queue is the practical reference for continued development. PR #3 stays DRAFT and must not be merged while Alpha blockers remain open.

## P0 — Alpha stability blockers

Goal: produce a new native build that can be validated without a known risk of corruption, tile misalignment, or basic editing regressions.

- ✅ Canonical 300 m OMSI grid, including negative coordinates.
- ✅ `.terrain` edge stitching when creating tiles.
- ✅ Persisted 3×3 `global.cfg` catalog.
- ✅ Correct centering for odd/even real-map areas.
- ✅ Preserve camera navigation when tiles are created/deleted.
- ✅ Sequential 3×3 creation regression test.
- ✅ On-disk round trip: create a 3×3 catalog, write terrains, reopen through Core, and validate every shared edge.
- ⏭ Generate the next Native Feature Preview from a green HEAD.
- ⏭ Executable smoke test: create ±X/±Y tiles, 3×3, 2×2/4×4 area maps, save, close, and reopen.
- ⏭ Smoke test selection, Move/Rotate, Undo/Redo, batch delete, and save without Inspector.
- ⏭ Validate installer/portable startup and crash logs before promotion.

P0 exit criterion: the candidate opens, edits, saves, and reopens test maps without tile misalignment, corruption, or reproducible crashes in primary workflows.

## P1 — performance and large maps

Goal: make the editor predictable on large real-world maps.

- Persistent derived cache for geometry, materials, and thumbnails.
- Full priority-aware loading queue.
- Controlled CPU/GPU resource eviction outside the active region.
- Safe instancing.
- LOD for terrain/objects/splines where it does not break picking or editing.
- Opening, streaming, memory, and rebuild metrics.
- Keep selection/editing responsive during streaming.

P1 exit criterion: navigating the map does not reprocess the whole installation and CPU/GPU memory remains controlled.

## P2 — OMSI editor operational parity

Goal: avoid relying on the old editor for the main construction and operation workflows.

- Complete tile properties.
- Validated tile lightmap/lighting editing.
- Complete attachments, including fields still read-only.
- Parent/hierarchy and object-specific labels/options.
- Spline Mirror.
- Safe intermediate-tile deletion with reference reindexing.
- Fully editable paths and continuity diagnostics.
- AI paths, crossings, and advanced traffic behavior.
- Advanced timetable services/calendars and remaining operational cases.
- Signal Routes.
- Railway switches, priorities, and level crossings.
- Environment.
- Chronology.
- Formal read/write support for `[worldcoordinates]` maps.
- Complete Map Health validation for paths, splines, attachments, IDs, timetable, signals, terrain, and references.

P2 exit criterion: an OMSI map can be built, operated, validated, and saved without returning to the original editor for essential workflows.

## P3 — modern tooling and expansion

Goal: extend Map Studio beyond original-editor parity.

- Safe OSM multipolygon inner rings/courtyards.
- More advanced DEM and datum/offset validation.
- Additional auto-junction and procedural construction tools.
- Lots, districts, distribution, and alignment tooling.
- Continued Building/Road/Bridge/Tunnel Studio evolution.
- Additional AI assistance without making core functions AI-dependent.
- Other simulator adapters only when they have real implementations.

## Outside this branch

Licensing, subscriptions, Stripe, serials, the commercial website, and entitlement enforcement remain in a separate branch/workstream and are not part of `feature/native-winui-d3d11`.
