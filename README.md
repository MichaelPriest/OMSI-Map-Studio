# OMSI Map Studio

Standalone, modern map editor for **OMSI 2**, focused on making map creation easier, safer and more visual.

> This project is completely independent from OMSI NavBR Multiplayer. It has its own repository, architecture, releases and development lifecycle.

## Bootstrap goals

- Discover a real OMSI 2 installation and its maps.
- Parse `global.cfg` while preserving unknown content.
- Extract real `[map]` tile references.
- Provide a .NET 10 WPF/WebView2 desktop host.
- Provide a React + TypeScript + Babylon.js editor shell.
- Never replace missing runtime data with fake production data.

## Requirements

- .NET 10 SDK
- Node.js 22+
- Windows for the desktop host

## Core

    dotnet build src/MapStudio.Core/MapStudio.Core.csproj
    dotnet test tests/MapStudio.Core.Tests/MapStudio.Core.Tests.csproj

## UI

    cd src/MapStudio.UI
    npm install
    npm run dev

For the packaged desktop UI, run `npm run build` before building the desktop project.

## Compatibility rule

Understanding an OMSI command is optional; preserving it is not. An unchanged document must round-trip without silently losing commands the editor does not understand yet.
