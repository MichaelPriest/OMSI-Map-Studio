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
