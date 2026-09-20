# OMSI Map Studio native migration

This track rebuilds the editor on the native Windows architecture without discarding the OMSI domain already implemented.

## Target stack

- .NET 10;
- WinUI 3 / Windows App SDK;
- Direct3D 11;
- Vortice.Windows;
- MapStudio.Core remains the authority for formats, reading, validation and persistence;
- SQLite/cache remain reusable;
- no WebView2 in the primary viewport;
- React does not control renderer lifetime.

The foundation uses Windows App SDK 2.5.1 and Vortice.Direct3D11 3.8.3.

## Strategy

Migration runs in parallel with the current editor.

### Phase N0 — foundation

- new `MapStudio.Renderer` project;
- new `MapStudio.Native` project;
- Direct3D 11 initialized in its own runtime;
- mouse input received directly by WinUI;
- stable picking ID registry;
- ID-buffer codec;
- dedicated Windows CI.

### Phase N1 — minimal OMSI viewport

- connect `SwapChainPanel` to a DXGI swap chain;
- perspective/top camera;
- Gundorf terrain;
- real O3D objects;
- splines;
- ID buffer;
- hover and selection;
- DPI validation at 100/125/150/200%.

### Phase N2 — editing

- move gizmo;
- rotate gizmo;
- snap;
- Inspector;
- save through MapStudio.Core;
- undo/redo.

### Phase N3 — complete interface

- native Explorer;
- libraries;
- previews;
- construction tools;
- terrain;
- real-map tools;
- diagnostics/Map Health;
- fullscreen;
- shortcuts.

### Phase N4 — replacement

The native version replaces the WebView2 host only after sufficient functional parity and real-world validation. Until then, the current application remains available for comparison.

## Selection

Native selection will not depend on object material, transparency or texture. Every OMSI entity receives a `PickingId`. A dedicated pass writes that ID into an integer render target. The pixel under the pointer directly identifies the selected entity.

This removes the fallback chain that became necessary in the WebView2/Babylon viewport.


### Checkpoint N0.1 — Direct3D presentation

The foundation now creates an `IDXGISwapChain1` for composition, associates it with the `SwapChainPanel` through `ISwapChainPanelNative`, creates the backbuffer/RTV and presents a real first Direct3D 11 frame.

Backbuffer sizing uses WinUI `CompositionScaleX/Y`, so the renderer works in physical pixels and reacts to DPI/window-size changes without CSS/WebView2.


### Checkpoint N0.2 — native OMSI session

The WinUI host now opens the real OMSI folder and a map folder using the native Windows picker. `MapStudio.Core` is called directly, without a WebView2 bridge, to discover maps, open `global.cfg`, choose the initial tile and load the real 3×3 region.

The native UI already displays real counts for loaded tiles, objects, splines and terrain grids. The next checkpoint converts this Core snapshot into GPU buffers.


### Checkpoint N1.1 — native GPU navigation

The native overview now has a viewport transform executed by the vertex shader. Mouse-wheel zoom and right/middle-button pan update only a Direct3D constant buffer; O3D geometry is not rebuilt on every movement.

The same transform is used by both the visible pass and the ID-buffer pass, keeping selection pixels aligned with objects after navigating the map.


### Checkpoint N1.2 — real OMSI terrain

The native renderer now converts each tile's real height grid into GPU triangles. The first visualization mode remains top-down, but it already uses real elevation data for color/depth and prepares the same mesh for the upcoming perspective camera.

SCO objects that do not use `[absheight]` also receive bilinear terrain interpolation before O3D transforms, preserving the existing editor rule.


### Checkpoint N1.3 — real depth for viewport and ID buffer

Both the visible viewport and the selection pass now have their own Direct3D depth buffer. Selection no longer depends on triangle submission order: when objects/proxies overlap, the picking pixel keeps the nearest surface according to depth.

The same depth rule is used by the visible frame and the ID buffer, bringing selector behavior closer to a native 3D editor.
