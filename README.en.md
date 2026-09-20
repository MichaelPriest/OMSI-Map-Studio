# OMSI Map Studio

**English** · [Português (Brasil)](README.pt-BR.md)

**OMSI Map Studio** is a modern, standalone map editor for **OMSI 2**, focused on making map creation easier, safer and more visual.

> This project is completely independent from OMSI NavBR Multiplayer. It has its own repository, architecture, releases and development lifecycle.

## Initial goals

- Discover a real OMSI 2 installation and its maps.
- Parse `global.cfg` while preserving content not yet understood.
- Extract real tile references from `[map]` sections.
- Provide a .NET 10 + WPF + WebView2 desktop host.
- Provide a React + TypeScript + Babylon.js interface.
- Never replace missing real production data with fake/mock data.
- Make map building approachable for users who do not know OMSI's internal file formats.

## First test version

The current prerelease is **v0.1.0-alpha.4**. Writing is still experimental, but it now covers existing-object transforms and preservation-safe object insertion when a safe template of the same `.sco` is available, always with automatic backups.

It can select a real OMSI 2 installation, manually open a map, work in Full map or 3×3 mode, display real O3D objects and splines, edit X/Y/Z/rotation/pitch/bank, undo/redo previews, insert objects from the Library, and use **Place copy** on the selected object. See [Alpha Testing](docs/en/TESTING.md) for limitations and the validation checklist.

## Development requirements

- .NET 10 SDK
- Node.js 22+
- Windows to run the desktop host

## Core

    dotnet build src/MapStudio.Core/MapStudio.Core.csproj
    dotnet test tests/MapStudio.Core.Tests/MapStudio.Core.Tests.csproj

## UI

    cd src/MapStudio.UI
    npm install
    npm run dev

To use the packaged UI in the desktop application, run `npm run build` before building the desktop project.

## Compatibility rule

Understanding an OMSI command is optional; **preserving it is not**.

If the editor opens a command it does not understand yet, that content must still exist when the file is saved. An unchanged document must be able to round-trip without silent data loss.

## Documentation languages

All official documentation must be published in at least:

- `pt-BR`
- `en`

When documentation is added or changed, both language versions must be updated in the same change set.

Architecture documentation:

- [Architecture — English](docs/en/ARCHITECTURE.md)
- [Arquitetura — Português](docs/pt-BR/ARCHITECTURE.md)


### Spline Library

Alpha.4 also scans `OMSI 2/Splines` on demand and can preview installed `.sli` files. The library offers **Normal** and **Height** creation. Persistence is enabled only when the map contains a real neutral template of the same type: five explicit zero extras for `[spline]` and six for `[spline_h]`. Header and extras are copied from that template.


## Project roadmap

The official technical and functional direction is documented in:

- [Technical and functional roadmap — English](docs/en/ROADMAP.md)
- [Roadmap técnico e funcional — Português](docs/pt-BR/ROADMAP.md)

The roadmap defines the retained stack, Asset Index/cache, tile streaming, original-editor parity, Traffic Rules, paths, tracks/trips, timetables, signals, rail, geodata, and the criteria for considering Map Studio a functional replacement for the original editor.
