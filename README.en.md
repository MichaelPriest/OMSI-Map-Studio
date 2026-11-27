# OMSI Map Studio

**English** · [Português (Brasil)](README.pt-BR.md)

Standalone **OMSI 2** map editor with a native 3D viewport, construction tools and real-world map generation. Independent from OMSI NavBR Multiplayer.

## Test the native editor

- [Windows x64 build — CI 1720](https://github.com/MichaelPriest/OMSI-Map-Studio/actions/runs/46060172393) — November 27, 2026, commit `db032ec9`; download the `OMSI-Map-Studio-Windows-x64-test` artifact (GitHub sign-in may be required; 14-day retention).
- [Main branch CI after integration](https://github.com/MichaelPriest/OMSI-Map-Studio/actions/runs/46060526562).
- [Latest numbered prerelease at this update: v0.2.0-alpha.5-test.10.143-native](https://github.com/MichaelPriest/OMSI-Map-Studio/releases/tag/v0.2.0-alpha.5-test.10.143-native).

Extract the entire test ZIP into a separate folder and run `MapStudio.Native.exe`. Use a test workspace and preserve original maps. Passing CI does not replace hands-on validation in OMSI 2.

## Features

| Area | Features |
| --- | --- |
| 3D editor | Navigation, selection, O3D objects, SLI splines, terrain and tile editing |
| Road Studio | Direct drawing, continuation, precision mode, roundabouts and read-only longitudinal profile |
| Geo Studio | Keyless OSM reference, layered reconstruction, Terrarium and Open-Meteo elevation |
| Construction | Asset library, object, building, terrain and infrastructure tools |
| Safety | Backups, validation and preservation of unknown OMSI content |

The November 27, 2026 update integrates Terrarium elevation shared by roads and terrain, OSM hydrographic reference with islands, and download cancellation. Water remains a visual reference; physical water surfaces, full roundabout integration and visual acceptance remain pending.

## Documentation

- [User manual](docs/en/USER_MANUAL.md)
- [Roadmap](docs/en/ROADMAP.md)
- [Native architecture](docs/pt-BR/NATIVE_MIGRATION.md)
- [OpenMapper parity](docs/pt-BR/OPENMAPPER_NATIVE_PARITY.md)
- [Build 1720 notes](docs/en/releases/native-ci-1720.md)
- [Gallery and real screenshot checklist](docs/en/EDITOR_GALLERY.md)

## Development

Native stack: .NET 10, WinUI 3, Direct3D 11/Vortice and SQLite. Core is authoritative for OMSI formats. WPF/WebView2 and React/Babylon.js belong to the legacy track.

```powershell
dotnet test tests/MapStudio.Core.Tests/MapStudio.Core.Tests.csproj
dotnet test tests/MapStudio.Renderer.Tests/MapStudio.Renderer.Tests.csproj
dotnet build src/MapStudio.Native/MapStudio.Native.csproj -c Release -p:Platform=x64
```

Building the native host requires Windows. Official documentation is maintained in Brazilian Portuguese and English. Never replace missing real data with fabricated data or discard unknown commands on save.
