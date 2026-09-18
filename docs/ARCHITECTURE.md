# Architecture

OMSI Map Studio is an independent product and must not depend on OMSI NavBR Multiplayer code, runtime services or releases.

## MapStudio.Core

Owns OMSI-facing domain logic: configuration parsing, map discovery, tiles, and later scenery, splines, terrain, paths, backups and validation. It must not reference WPF or React.

## MapStudio.Desktop

Windows host for native file/folder access, Core services and WebView2.

## MapStudio.UI

Primary editor interface: Babylon.js viewport, asset browser, inspector and editing tools. Production state must come from real Core/Desktop state.

## Compatibility strategy

OMSI configuration files are keyword-driven text files. The parser stores the complete original line stream and adds structured views over it. Unknown commands remain part of the source and therefore survive unchanged round-trips.

## First vertical slice

1. Select OMSI root.
2. Enumerate folders under `maps` that contain `global.cfg`.
3. Parse the selected `global.cfg`.
4. Read real `[map]` tile references.
5. Send that state to the React editor.
6. Render real tile/object/spline data incrementally.
