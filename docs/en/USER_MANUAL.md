# OMSI Map Studio — User Manual

> Initial user manual for the native WinUI 3 + Direct3D 11 architecture.  
> Updated for the **0.2.0-alpha.5-test.10.10-native** series.

OMSI Map Studio is still an Alpha project. Keep external backups of important maps before editing them. Several operations already create automatic backups, but shared assets can affect more than one map.

---

## 1. Interface overview

The main window is divided into:

- **3D Viewport** — map visualization and editing;
- **Project / Explorer** — map, tile, object and spline navigation;
- **Asset Library** — search and selection of objects, splines and other assets;
- **Inspector** — advanced and numeric adjustments for the current selection.

### Editor interaction rule

**No primary editor function depends on the Inspector.** The Inspector is optional and is reserved for fine/numeric adjustment.

Creation, selection, movement, rotation, duplication, splitting, curve editing, elevation, leveling, replacement and other primary operations must be available directly in the viewport, quick bars or radial wheel.

**Creator Focus / F11** increases viewport space. Panels can be minimized and the Project panel can be resized.

---

## 2. Creating or opening a project

From **File**:

- **Map Studio Workspace** — opens the standalone workspace;
- **New map...** — creates a map;
- **Import existing map...** — imports an existing map;
- **Add asset folder...** — adds external asset folders;
- **Export map as OMSI package...** — creates an OMSI-compatible package;
- **Open OMSI...** — selects/opens an OMSI installation when needed;
- **Open map from catalog...** — opens a detected map;
- **Open map folder...** — opens a map directory directly.

---

## 3. Map navigation

### Pan

Hold the **middle mouse button** and drag.

The cursor changes to a hand while panning.

### Orbit

Hold and drag the **right mouse button**.

A short right click opens contextual actions, while a drag continues to orbit the camera.

### Zoom

Use the mouse wheel over the viewport.

### Focus

Select an object or spline and use **Focus**.

### Views and visibility

The **View** menu provides perspective/top views and visibility controls for terrain, objects, splines, grid and real spline profiles.

---

## 4. Selection

Click an object or spline in the viewport.

Native picking uses an ID buffer with an enlarged click area for narrow objects, small objects and splines.

When multiple items overlap, **click again at nearly the same point**. The editor cycles through the candidates and reports positions such as `1/3 overlapping` and `2/3 overlapping`.

The **Easy selection** strip can filter selection to:

- All;
- Objects;
- Roads / splines;
- Terrain / tile.

If selection is still difficult:

1. move closer;
2. use top view for splines;
3. temporarily filter to **Objects** or **Roads / splines**;
4. verify visibility;
5. click the same point again to cycle candidates.

### 4.1 Move and rotate with a visual ghost

When **Move** or **Rotate** is active, the selected item shows a **filled ghost of its own geometry** following the pointer in real time. The original remains at its starting position until release.

- **Move (W)**: drag the selected object or spline itself.
- **Rotate (E)**: drag the item itself or use the rotation ring.
- Releasing applies the transform.
- Ctrl+Z / Ctrl+Y remain available.
- Inspector is not required.

---

## 5. Context radial wheel

A **short right click** on a selectable item opens the radial wheel.

### Objects

Typical actions:

- Move;
- Rotate;
- Duplicate;
- Delete;
- Inspector;
- Focus.

**Rotate without Inspector:** select the item, activate **Rotate** (**E**) and drag the selected item itself in the viewport. The rotation ring remains available for more precise visual control. Release the mouse to apply. When Snap is enabled, rotation uses the configured increments.

**Move without Inspector:** select the item, activate **Move** (**W**) and drag the object or spline itself in the viewport to move it across the map plane. Gizmo axes remain available for precise and vertical movement.

### Splines / roads

The road wheel exposes up to 12 actions:

- Move
- Curve
- Duplicate
- Split
- Parallel
- Raise
- Level
- Lower
- Replace
- Mirror
- Flow
- Delete

Road-only actions are hidden for normal objects.

---

## 6. Project and Asset Library

Project and Asset Library are separate areas. Dense tool sections use collapsible panels; **Asset Filters** and **Paths / Visualization** can stay collapsed to free editor space.

**Project** navigates content already in the map.

**Asset Library** is used to select content to insert or use as replacement.

The Library supports search, category/subcategory overrides and AI-assisted object/spline classification.

---

# 7. Road / spline creator

The normal workflow avoids manual coordinate entry.

1. Open **Roads**.
2. Choose a **SLI** in the Library.
3. Click **Build spline**.
4. Press and hold at the starting point.
5. Drag while watching the ghost preview.
6. Release at the endpoint.

Releasing the mouse now finalizes the dragged road and submits it for insertion. The Inspector remains available for advanced editing.

If **Build spline** cannot start, the status bar reports the missing prerequisite instead of failing silently.

---

## 7.1 Drag

Press at the start, drag the preview and release at the end.

With Continuous mode enabled, the next section can continue from the previous endpoint.

---

## 7.2 Straight

Creates a straight segment between start and end.

---

## 7.3 Curve

After defining start and end, move the pointer sideways to control curvature.

The editor calculates radius, length, rotation and gradient.

---

## 7.4 Editing an existing curve

1. select the spline;
2. choose **Edit curve** / **Curve**;
3. move the visual handle;
4. click to apply.

---

# 8. City-builder style road elevation

The Roads quick bar provides:

**[ ↓ Lower ] [ Level ] [ ↑ Raise ]**

A **Terrain** mode is also available.

## 8.1 Elevation step

Choose:

- 0.5 m;
- 1 m;
- 2 m;
- 5 m.

---

## 8.2 Raise

Use for ramps, bridges and viaduct access.

Example: start 0 m → end +6 m.

The spline receives a real gradient.

---

## 8.3 Level

Keeps the starting elevation.

Example: start +6 m → end +6 m.

Useful for bridge/viaduct decks over uneven terrain.

---

## 8.4 Lower

Use for descents, underground ramps and tunnel access.

Example: start 0 m → end -5 m.

---

## 8.5 Terrain

Returns endpoint height behavior to the real terrain.

---

## 8.6 Drag preview

The viewport can display:

- length;
- start elevation;
- end elevation;
- gradient;
- curve radius;
- elevation mode.

Example:

`52.4 m · 0 → +5 m · 9.5%`

---

## 8.7 Gradient warning

A configurable recommended maximum gradient is available.

Exceeding it displays a warning but does not automatically block creation.

---

# 9. Existing road elevation editing

For a selected spline, the radial wheel can:

- raise by the current step;
- lower by the current step;
- level at its current elevation.

The advanced Roads menu also provides **conform selected spline to terrain**.

These supported transform changes participate in the current undo/redo history.

---

# 10. Snap, auto-connect and continuous mode

**Snap** aligns nearby endpoints.

Snap distance is configurable.

**Auto-connect** can populate previous/next links when a compatible endpoint is found.

**Continuous** starts the next road segment from the endpoint of the previous one.

---

## 10.1 Auto-connect and repair links

The Roads bar includes **Auto connect** for the selected spline.

It searches compatible endpoints inside the snap distance and:

- fills missing Previous/Next links;
- repairs links pointing to spline IDs that no longer exist;
- updates the reciprocal neighbor link;
- preserves valid links;
- creates a backup before writing.

Inspector is not required.

# 11. Split spline

1. select a spline;
2. choose **Split**;
3. click the exact point where the segment should be cut.

The first section keeps the original ID, the second receives a new ID, and previous/next links are reconnected.

---

# 12. Parallel road

Set the lateral distance and use:

- **← Parallel**
- **Parallel →**

Straight segments are shifted laterally. Curves recalculate radius and length while preserving elevation/gradient.

---

# 13. Duplicate

Select a spline and choose **Duplicate** to enter copy placement.

---

# 14. Replace road type

1. select the road in the map;
2. choose another SLI in the Library;
3. use **Replace**.

The editor preserves ID, position, rotation, radius, length and previous/next links.

---

# 15. Mirror

**Mirror** toggles the native OMSI spline mirror flag.

---

# 16. Vehicle flow

**Flow** toggles supported vehicle path direction for the selected spline.

---

# 17. [spline_h]

The advanced Roads menu retains **[spline_h]** mode for OMSI height-spline workflows.

---

# 18. Objects

Objects can be placed from the Asset Library.

The camera remains in place when adding/reloading items in the same map.

Selected objects can be moved, rotated, duplicated, deleted, focused and adjusted in the Inspector.

---

# 19. Terrain

Terrain tools include point selection, leveling, raise/lower, smoothing, texture painting, Google elevation and local elevation-grid import.

Road elevation changes the spline, not the terrain mesh.

---

# 20. Create real map by area

Use **Map > Create real map by area...** to build a map without manually typing latitude/longitude.

1. Search for a **city, address or place**. Search uses OpenStreetMap/Nominatim only on an explicit user action and requires no API key.
2. Choose **OpenStreetMap** or **Google Maps** to view the area.
3. Pan and zoom the map.
4. Adjust **Area width %** and **Area height %** to enlarge or reduce the center selection frame. Geographic bounds are recalculated from the frame in real time.
5. Review estimated physical dimensions and required 300 m OMSI tiles.
6. Enter folder/name.
7. Under **OpenStreetMap roads**, choose:
   - **Create terrain / reference only**;
   - **Import roads as guides** — previews the graph in the viewport without writing splines;
   - **Generate roads automatically after preview** — classifies roads, prepares splines/junctions and requires confirmation after preview before writing.
8. Optionally enable **Google Elevation** and/or **Visual terrain reference**.
9. Click **Create area**.

The window can be moved, resized and minimized inside the editor.

**OpenStreetMap:** requires no key and is used for interactive browsing, place search and vector data. Map Studio does not bulk-download/offline-cache the public tile service. Search requests are rate-limited and identify the application.

**Google Maps:** uses the API key stored by the user in Windows Credential Manager. The interactive selector requires Maps JavaScript API access. Google Elevation can incur charges and is disabled by default.

OSM roads are projected into the map georeference and classified by type/width/lanes when those tags are available. Even in automatic mode, Map Studio shows **Preview before write** and only persists splines/junctions after confirmation.

For elevation without Google, use **Map → Import local elevation grid**. CSV/TXT/ASC data is applied to the active tile through the same safe terrain pipeline with backup; local import is per tile and is not presented as a global DEM for the entire area.

## Tile X/Y

Tile X/Y no longer occupies the Project card. Use the **Tile XY** top-bar shortcut. Its internal window is movable, resizable and minimizable. When minimized, only the title bar remains visible; the same button restores the window without losing its position.

The window lists the map's real tiles and shows which ones are **active** and **loaded in the viewport**, plus object/spline counts and the tile file path. Main actions are available directly in the window:

- **Focus** — frames a tile that is already loaded without reloading the region;
- **Load** — makes the tile active and, in performance mode, loads the 3×3 region;
- **Create** — creates a tile using the entered X/Y coordinates;
- **Delete** — activates the chosen tile and reuses the safe validated deletion with backup;
- double-clicking the list also loads/focuses the tile.

The **Create real map by area** window follows the same pattern: it can be moved, resized and minimized without closing the area picker.

## Collapsible panels

**Project actions**, **Assets · Filters** and **Paths · Visualization** can be collapsed to free workspace. Map Studio saves the expanded/collapsed state in the local profile and restores it on the next run.

## Detachable internal windows

The top toolbar can also open tools as independent internal windows:

- **Paths** — live OMSI path display filters;
- **Transport** — the full Route Studio;
- **Library** — the same real asset, favorites, collections and AI-classification panel;
- **AI panel** — active profile, provider, model, credential status and connection test.

These windows can be moved, resized, minimized and closed. **Transport** and **Library** do not create duplicate controls: the real panel is moved into the floating window and returned to Project when the window is closed.

## Multi-selection

Use **Ctrl+click** or **Shift+click** to add/remove objects and splines. To select several items visually, start dragging from an empty area of the viewport and draw a box over the desired items. With **Ctrl** or **Shift** held, the box adds items to the current group; without a modifier, it replaces the selection.

The selected group can be **moved**, **rotated**, **duplicated** and **focused** directly in the viewport. The preview/filled ghost follows the group during manipulation, and transform history treats it as one visual action. None of these operations depends on the Inspector.

When **Duplicate** is used with multiple items, Map Studio creates new IDs in a backed-up batch, initially offsets the copies by 2 m and leaves the new group selected in Move mode. Copied splines are created disconnected (previous/next = -1) for safety; reposition them and use **Auto connect** when you want to rebuild links.

Press **Delete** to remove the selected group with one confirmation and safe backup. After deletion, **Ctrl+Z** restores the batch from the real map-file backups.

## Natural movement

In **Move**, drag the selected item itself: the ghost follows the pointer on the item plane. Middle-button navigation behaves like grabbing and pulling the map.

# 21. Georeference and map reference

The Map menu includes real-coordinate map creation, georeference editing, Google elevation, local grids and Google map reference overlays.

---

# 21. Traffic paths

The viewport can display:

- **CAR**;
- **HUM**;
- **RAIL**;
- **AIR**.

SCO/SLI paths can be created, duplicated, edited and safely removed.

> Paths belong to shared assets. Editing one can affect every placed instance using that SCO/SLI.

---

# 22. Transport, Tracks, Trips and Station Links

Transport/Timetable tools include Tracks, Trips, Station Links, Lines/Tours, Profiles, stop timing, Route Studio and a detachable Timetable window.

---

# 23. Traffic lights and traffic rules

The project includes traffic-rule and traffic-light program editing. Validate these changes on a map copy while this area continues to evolve.

---

# 24. Undo / Redo

Supported transform edits use the editor history.

Structural file operations may use a separate construction/backup history.

---

# 25. Saving and backups

Some viewport edits remain pending until saved.

Before structural changes the editor may flush pending transforms to keep the map consistent.

Recommended workflow:

1. keep an external map copy;
2. make a small group of edits;
3. save;
4. reopen the map;
5. validate visually.

---

# 26. AI integration

Native OpenAI integration uses the Responses API, while the project keeps a provider-profile structure for other supported adapters.

## Connecting AI to the map

1. Open a map.
2. Click **AI: connect** in the top bar or use **AI > Connect / test AI on map...**.
3. If no active profile exists, provider configuration opens automatically.
4. Choose a provider/preset, model and API key/token.
5. mark the profile as **active**;
6. click **Test connection**;
7. save;
8. click the active AI button again to test that profile in the map context.

The status bar confirms which profile will be used by map AI tools.

Current AI-assisted functionality includes object/spline classification, reference analysis and assisted creation features exposed by the relevant tools.

API keys are stored in Windows Credential Manager and are not written into map or asset files.

---

## 26.1 Selecting the active profile

The top toolbar includes an **AI Profile** selector. Choosing a profile there makes it active immediately and persists the selection.

Use **AI: connect/test** to validate the active connection.

## 26.2 Map reference

Under **Map → Map reference over terrain...**:

- **OpenStreetMap** is the default and requires no API key;
- **Google Maps** uses the Maps Static API and requires the user's key;
- the Google key can be stored in Windows Credential Manager;
- the saved key can also be reused by Google Elevation;
- map reference opacity is configurable.

# 27. Testing Alpha builds

Use a test map or copy. After saving, reopen it and verify object/spline positions, selection, pan/orbit, undo/redo and generated backups. If the application crashes, keep the corresponding `startup.log`.

---

# 28. Common issues

## Item does not select

Move closer, use top view, verify visibility and click near the visual center.

## Radial wheel does not open

Use a short right click. Dragging is interpreted as camera orbit.

## Road is too steep

Reduce height difference or increase section length.

## Creating a bridge

Use **Raise** for the approach, then **Level** at deck elevation.

## Creating a tunnel

Use **Lower** for the entrance ramp, then **Level** at underground elevation.

## Returning to terrain

Use **Terrain** in the Roads bar.

---

# 29. Features still evolving

Planned/ongoing work includes:

- visual endpoint movement;
- advanced radius handles;
- join segments;
- automatic intersections;
- city-builder style road upgrades;
- junction creation;
- automatic ground/ramp/bridge/tunnel context recognition;
- automated pillars, guard rails, portals and tunnel wall assets.

This manual will be updated as each capability reaches a validated build.
