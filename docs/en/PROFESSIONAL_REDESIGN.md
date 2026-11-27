# Professional redesign

[Português](../pt-BR/PROFESSIONAL_REDESIGN.md)

## Stage 5 — compact layout

Compact module ribbon, wrapping headings, separate Inspector context row, scrollable library controls, capped side panels below 1200 DIPs and docked Geo Studio priority over the asset tray. Search results use a dedicated row. OMSI formats unchanged. Validate at 1024 × 720, 1366 × 768 and 1920 × 1080, light/dark themes and 100/125/150% DPI. Revised screenshots pending.

## Stage 6 — visual hierarchy

Two desktop navigation rows, frequent actions near the menu, secondary actions grouped under More. Flat scrollable context bar, reserved camera strip, compact scrollable vertical rail, two-row project actions, minimap initially collapsed, asset tray capped to workspace height, opaque Geo Studio surface. Existing control names and event handlers preserved. Validate More, module switching, fullscreen, asset library and Geo Studio using CI screenshots.

## Stage 7 — grouped Road Studio

Drawing, straight, curve, snap and continuous modes come first; curve/endpoints editing, connections/parallel roads and elevation use expandable groups. Precision, roundabout, review, profile and advanced tools remain accessible. Compact asset thumbnails and actions; proportional tray cap only when docked. Existing controls and handlers preserved. Visual and editing acceptance pending.

## Stage 8 — focused Inspector

Fixed header and selection, independently scrollable properties, Apply/Duplicate/Delete at the bottom when available. Core geometry remains accessible; terrain matching and links are collapsible. Terrain tools separate brush, textures and polygons. Lighter borders and status ellipsis. Existing control names and handlers preserved. Validate selection, editing and persistence on Windows, including compact heights.

## Stage 9 — viewport and projects

Below 1250 DIPs, tool selection uses the side library instead of automatically opening the bottom tray. Search remains visible, Explorer tabs stay available, actions have bounded height and thumbnails have dedicated space. Vertical rail no longer overlaps viewport. Module icons reuse native vector geometry. Empty-scene welcome actions create, open, import or open Geo Studio; hidden with a map or another tool. Stage 8 passed CI; stage 9 still requires CI and captures.

## Stage 10 — clean visual language

Neutral graphite/light palette, blue selection, subtle dividers and compact modules. Desktop rail only shows selection, vegetation and crossings rather than duplicating the top modules; fullscreen restores all rail shortcuts. Existing controls and handlers preserved, high contrast dictionary unchanged. Validate light/dark themes, tool switching and F11. Screenshots pending.

## Stage 11 — clean layout structure

Restored pre-stage-10 palette: focus on arrangement rather than color. Camera/grid controls grouped in View inside context strip; removed dedicated viewport strip. Project and Properties accessible in the same strip. Automatic Inspector collapses when empty and opens for editable selection or terrain tools; manual toggle disables automation, re-enable from View. Single-line Explorer tabs and collapsed project actions. Fullscreen restore controls retained. Fixed static rail guard while preserving module controls and top ribbon access. CI and visual acceptance pending.

## Stage 12 — separate startup and editing

Welcome view occupies the workspace without exposing empty panels; four actual project actions, explicit editor entry and return via View when no map is open. Fullscreen hides welcome. Explorer uses a compact flat list with type/details and asset path tooltip. Simplified header branding. Palette and engine unchanged. Stage 12 CI and screenshots pending.

## Stage 13 — fewer toolbars and contextual assets

Horizontal module ribbon hidden by default, available in View. Compact rail includes Geo Studio and validation. Library prioritizes search, filters and results; asset actions appear on selection below the list in sidebar or beside it when docked. Capture automation dismisses welcome and invokes visible module controls through UI Automation. Palette and engine unchanged; stage 13 build and screenshots pending.
