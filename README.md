# OMSI Map Studio

A modern, standalone map editor for **OMSI 2** focused on making map creation easier, safer and more visual.

> This project is completely independent from OMSI NavBR Multiplayer. It has its own repository, architecture, releases and development lifecycle.

## Product goals

- Make it possible to build an OMSI 2 map without editing configuration files by hand.
- Preserve unknown OMSI configuration data instead of silently discarding it.
- Provide a modern 3D viewport with selection, transform gizmos and visual asset browsing.
- Add safe editing features from the beginning: backups, validation and undo/redo.
- Keep compatibility with real OMSI 2 maps as the primary technical constraint.

## Initial milestone

The first development milestone is intentionally small:

1. Locate an OMSI 2 installation.
2. List available maps.
3. Parse a real `global.cfg` without destructive rewriting.
4. Build an internal map/tile model.
5. Display the initial editor shell and 3D viewport.
6. Progress toward selecting, moving and saving scenery objects safely.

## Proposed stack

- .NET 10 / C#
- React + TypeScript + Vite
- WebView2 desktop host
- Babylon.js for the 3D editor viewport
- xUnit for parser/core tests

## Status

Early development / bootstrap.
