# Icon system

**English** · [Português (Brasil)](../pt-BR/ICON_SYSTEM.md)

## Goal

OMSI Map Studio will use its own icon pack to create a visual identity consistent with OMSI's technical atmosphere without directly copying proprietary game assets.

The visual direction should evoke:

- transport engineering software;
- road signage;
- maps and infrastructure;
- compact classic OMSI controls;
- construction tools;
- fast recognition at small sizes.

The result should be cleaner, more modern, and more consistent than the original editor.

## Official format

Primary icons must be **vector SVG**.

Reasons:

- crisp at 16, 20, 24, 32, and 48 px;
- HiDPI friendly;
- CSS-colorable;
- no raster-size variants required;
- easy React integration;
- supports hover, active, disabled, and warning states.

PNG should only be used for special exports or previews.

## Visual rules

- simple shapes;
- clear at 16–20 px;
- consistent stroke weight;
- slightly technical corners, not excessively rounded;
- avoid childish appearance;
- avoid generic mobile-app appearance;
- silhouettes inspired by traffic, maps, engineering, and transport;
- primary state color controlled by CSS;
- transparent background;
- no embedded text;
- no copied logos or assets from other products.

## Required states

Every icon must work in:

- normal;
- hover;
- active/selected;
- disabled;
- warning;
- error where relevant.

Color must not be the only state indicator: button/control background, border, or contrast must also change.

## Sizes

Base sizes:

- 16 px — menus and dense lists;
- 18/20 px — main toolbar;
- 24 px — construction and Inspector;
- 32 px — cards/library;
- 48 px — categories and empty states.

## Initial pack

### Navigation/editor

- select;
- move;
- rotate;
- scale (reserved);
- focus;
- fit view;
- fullscreen;
- exit fullscreen;
- camera perspective;
- camera top;
- grid;
- snap;
- undo;
- redo;
- save;
- open;
- search;
- settings;
- explorer;
- inspector;
- layers;
- visibility on/off.

### Construction

- road;
- curved road;
- intersection;
- bridge;
- tunnel;
- building;
- house;
- tree;
- vegetation;
- grass;
- water;
- street furniture;
- utility;
- transit;
- bus stop;
- rail;
- terrain;
- bulldoze/delete;
- replace;
- duplicate;
- line placement;
- brush placement;
- matrix placement;
- circle placement;
- lots.

### OMSI-specific

- SCO object;
- SLI spline;
- O3D model;
- texture;
- tile;
- tile create;
- tile delete;
- terrain file;
- attachment;
- parent;
- spline mirror;
- spline cant;
- spline complete-to;
- spline export;
- path;
- traffic rule;
- speed limit;
- traffic density;
- vehicle restriction;
- priority;
- AI traffic;
- pedestrian;
- traffic light;
- signal phase;
- stop;
- station;
- StationLink;
- track;
- trip;
- timetable;
- signal route;
- railway switch;
- chronology;
- environment.

### Diagnostics

- map health;
- dependency;
- missing asset;
- protected asset;
- warning;
- error;
- success;
- reload;
- cache;
- asset index;
- streaming;
- GPU/memory;
- performance.

## Code organization

Planned structure:

    src/MapStudio.UI/src/icons/
      types.ts
      MapStudioIcon.tsx
      icons/
        select.svg
        move.svg
        road.svg
        ...
      index.ts

The UI should use one shared component:

    <MapStudioIcon name="road" size={20} />

Do not scatter inline SVG throughout App.tsx.

## Semantics

Icon names must represent actions or concepts rather than visual positions.

Good:

- `road`
- `save`
- `traffic-rule`
- `asset-index`

Avoid:

- `button-left`
- `icon-blue`
- `tool3`

## Accessibility

Purely decorative icons use `aria-hidden`.

Icon-only buttons must provide appropriate `aria-label` and `title`.

## Migration

Replace current symbols in groups without breaking behavior:

1. quick toolbar;
2. construction HUD;
3. Explorer/Inspector;
4. menus;
5. library;
6. diagnostics;
7. future OMSI tools.

During migration, old symbols and new SVGs must not coexist inside the same button.

## Identity rule

The pack may be **inspired by OMSI's technical atmosphere**, but all final Map Studio drawings will be original.

Do not reuse graphical files extracted from OMSI 2 or other editors.

## Next step

Create the first visual batch:

- select;
- move;
- rotate;
- road;
- intersection;
- bridge;
- building;
- tree;
- terrain;
- explorer;
- inspector;
- save;
- undo;
- redo;
- fullscreen;
- map health;
- asset index;
- streaming.

Validate that set inside the toolbar and construction bar before drawing the remaining icons.


## Implementation status

First UI batch implemented:

- shared `MapStudioIcon` component;
- original SVGs using `currentColor`, transparent backgrounds, and a consistent technical stroke;
- desktop/fullscreen quick dock migrated to select, move, rotate, fit view, focus, Explorer, Inspector, undo, redo, save, and fullscreen;
- construction HUD migrated to roads, intersections, bridges, buildings, vegetation, transit, street furniture, utilities, and terrain;
- keyboard hints remain visible where they improve operation;
- legacy Unicode symbols are removed from buttons already migrated.

The package also includes `map-health`, `asset-index`, and `streaming` diagnostic icons, ready for the next visual migration.

All SVG drawings are original Map Studio assets and do not reuse OMSI 2 graphical files.


### Second migrated batch

- technical select/move/rotate/reserved-scale toolbar;
- fit, focus, fullscreen, undo/redo, and discard;
- construction history;
- construction-set button;
- dependency audit;
- map health in the toolbar and city-builder HUD;
- active construction-category icon in the asset shelf.

New SVGs in this batch: `scale`, `construction-set`, `dependency`, `discard`, `warning`, and `success`.

### Third migrated batch

- primary navigation: Home, Open, Explorer, Tools, and Settings;
- sidebar collapse/expand controls;
- completed setup steps use the shared `success` icon;
- scenery and spline library groups use semantic SVG icon names instead of Unicode symbols;
- asset-card/shelf fallbacks use `MapStudioIcon`;
- construction sets in the HUD use the same shared icon;
- menu status combines `success`, `warning`, `streaming`, `asset-index`, and `map-health`.

New SVGs in this batch: `home`, `open`, `tools`, `settings`, `collapse`, and `expand`.

Fullscreen also reuses the same icon system in its compact navigation rail so desktop and fullscreen do not drift into separate visual identities.


### Fourth migrated batch

The fullscreen quick dock no longer uses letters as the primary representation for **Snap, Grid, Terrain, Objects, Splines, and Spline profiles**.

- `snap`, `grid`, and `profile` were added as original Map Studio SVGs;
- Terrain reuses `terrain`;
- Objects reuse `sco-object`;
- Splines reuse `sli-spline`;
- N/T/G/O/L/P remain only as secondary keyboard shortcut hints;
- active states keep the same visual semantics used across the editor.

This brings F11 mode closer to the approved visual reference without creating a second implementation of the editing tools.


### Fifth migrated batch

- new `drag` icon for all movable panel/tool handles;
- Explorer/Inspector, Map Health, and Construction Sets close actions reuse `discard`;
- Map Health states reuse `success` and `warning`;
- missing-template warning reuses `warning`.


### Sixth migrated batch

- `favorite` for Library favorites;
- `collection` for asset collections;
- `preview` for opening the real asset preview;
- placement actions continue to reuse `sco-object`, `sli-spline`, and `profile`.
