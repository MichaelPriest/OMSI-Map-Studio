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

The current prerelease is **v0.1.0-alpha.3** and remains read-only.

It can select a real OMSI 2 installation, manually open one map, navigate 3×3 tile regions, select objects and splines, display real spline axes, extrude the selected spline's real `.sli` profile and load previews for unencrypted O3D meshes. See [Alpha Testing](docs/en/TESTING.md) for limitations and the validation checklist.

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
