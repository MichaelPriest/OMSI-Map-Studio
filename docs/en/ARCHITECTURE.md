# Architecture

**English** · [Português (Brasil)](../pt-BR/ARCHITECTURE.md)

OMSI Map Studio is an independent product and must not depend on OMSI NavBR Multiplayer code, runtime services or releases.

## MapStudio.Core

Owns OMSI-facing domain logic:

- configuration parsing;
- map discovery;
- tiles;
- future scenery objects;
- splines;
- terrain;
- paths;
- backups;
- validation.

Core must not depend on WPF, WebView2 or React.

## MapStudio.Desktop

Windows host responsible for:

- native file and folder access;
- Core services;
- WebView2 lifecycle;
- communication between C# and the interface.

The desktop host must not become the primary editor UI.

## MapStudio.UI

Primary editor interface.

Responsibilities:

- Babylon.js viewport;
- asset browser;
- property inspector;
- construction tools;
- visual validation;
- simplified editing experience.

Production state must come from real data supplied by Core/Desktop.

## Compatibility strategy

OMSI configuration files are command-oriented text files.

The parser keeps the complete original line stream and adds structured interpretations over that content. Unknown commands remain stored in the document and must survive an unchanged read/write round-trip.

## First vertical slice

1. User selects the OMSI 2 root directory.
2. Core discovers maps containing `global.cfg`.
3. `global.cfg` is parsed without destructive rewriting.
4. Real tile references are extracted.
5. Real state is sent to the React interface.
6. Tiles, objects, splines and terrain are rendered incrementally.

## Documentation rule

All official documentation must have equivalent `pt-BR` and `en` versions.

A documentation change is only complete when both languages are updated.
