# Testing — v0.1.0-alpha.1

**English** · [Português (Brasil)](../pt-BR/TESTING.md)

This is the first public test build of OMSI Map Studio. It is deliberately **read-only**.

## Installation

1. Download `OMSI-Map-Studio-v0.1.0-alpha.1-win-x64.zip` from the prerelease.
2. Extract the ZIP to a regular folder.
3. Run `OMSI Map Studio.exe`.
4. Microsoft Edge WebView2 Runtime must be available on Windows.
5. Click **Open OMSI** and select the OMSI 2 root directory, the folder containing `maps`, `Sceneryobjects` and `Splines`.

The package is self-contained for .NET 10 and does not require a separate .NET Desktop Runtime installation.

## What to test

- opening and changing the OMSI installation;
- discovered map list;
- tile count and layout;
- missing-tile indication;
- object and spline counts;
- object selection on Cartesian maps;
- `.map` values in the inspector;
- `.sco` friendly name, groups and meshes;
- found/missing mesh indication;
- geometry preview for unencrypted `.o3d` meshes.

## Known limitations

- map creation and saving are not available in this alpha;
- splines and terrain are not rendered yet;
- O3D textures and materials are not applied yet;
- encrypted O3D files do not receive a geometry preview;
- `.x` files are detected but not rendered yet;
- maps using `[worldcoordinates]` use a schematic view and do not globally place objects yet;
- terrain height is not yet applied to visual object placement;
- pitch/bank and orientation for some objects still need visual validation against real maps;
- very large meshes may be rejected by the preview safety limit.

## Safety

This version has no map write operation. The **Save** button remains disabled.

Do not use this alpha as a replacement for the original editor to modify maps. Its purpose is to validate reading, compatibility and visualization before any write support is enabled.
