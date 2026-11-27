# Repository structure

[Português (Brasil)](../pt-BR/REPOSITORY_STRUCTURE.md)

## Root

```text
.github/        automation and CI
src/            editor and library source
tests/          automated tests by domain
docs/           documentation, releases and gallery
installer/      Windows packaging
tools/          maintenance and capture scripts
README.md       project entry point
Directory.Build.props  shared MSBuild configuration
.gitignore      local file rules
```

Localized READMEs remain at the root to preserve public links. `Directory.Build.props` must remain above projects for automatic MSBuild discovery. Do not commit build binaries, caches, local OMSI installations or temporary reports.

## Source

- `src/MapStudio.Native`: WinUI 3 host, controls, services and ViewModels.
- `src/MapStudio.Core`: OMSI formats, generation, configuration and workspace.
- `src/MapStudio.Renderer`: Direct3D 11, scene, picking and viewport.
- `src/MapStudio.Desktop` and `src/MapStudio.UI`: legacy track retained for compatibility.

## Tests

Project files stay in their original locations. C# files were moved by domain without changing content, namespaces or project references. SDK globs automatically include subdirectories. CI still tests the complete projects.

| Folder | Moved files |
| --- | ---: |
| `tests/MapStudio.Core.Tests/AI` | 6 |
| `tests/MapStudio.Core.Tests/Commercial` | 1 |
| `tests/MapStudio.Core.Tests/Editing` | 2 |
| `tests/MapStudio.Core.Tests/Generation` | 1 |
| `tests/MapStudio.Core.Tests/Generation/Buildings` | 7 |
| `tests/MapStudio.Core.Tests/Generation/Geography` | 3 |
| `tests/MapStudio.Core.Tests/Generation/Infrastructure` | 10 |
| `tests/MapStudio.Core.Tests/Generation/Roads` | 21 |
| `tests/MapStudio.Core.Tests/Generation/Scene` | 9 |
| `tests/MapStudio.Core.Tests/Generation/Terrain` | 8 |
| `tests/MapStudio.Core.Tests/Generation/Vegetation` | 8 |
| `tests/MapStudio.Core.Tests/Omsi` | 9 |
| `tests/MapStudio.Core.Tests/Omsi/Assets` | 9 |
| `tests/MapStudio.Core.Tests/Omsi/Config` | 1 |
| `tests/MapStudio.Core.Tests/Omsi/Objects` | 3 |
| `tests/MapStudio.Core.Tests/Omsi/Scenery` | 4 |
| `tests/MapStudio.Core.Tests/Omsi/Splines` | 7 |
| `tests/MapStudio.Core.Tests/Omsi/Tiles` | 13 |
| `tests/MapStudio.Core.Tests/Omsi/Timetables` | 13 |
| `tests/MapStudio.Core.Tests/Settings` | 1 |
| `tests/MapStudio.Core.Tests/Simulators` | 1 |
| `tests/MapStudio.Core.Tests/Workspace` | 2 |
| `tests/MapStudio.Renderer.Tests/Graphics` | 5 |
| `tests/MapStudio.Renderer.Tests/Picking` | 4 |
| `tests/MapStudio.Renderer.Tests/Scene` | 12 |
| `tests/MapStudio.Renderer.Tests/Scene/Objects` | 7 |
| `tests/MapStudio.Renderer.Tests/Scene/Roads` | 10 |
| `tests/MapStudio.Renderer.Tests/Scene/Terrain` | 3 |
| `tests/MapStudio.Renderer.Tests/Scene/Traffic` | 2 |
| `tests/MapStudio.Renderer.Tests/Viewport` | 16 |

Place new tests in their corresponding domain. Historical release notes may refer to paths predating this reorganization.

## Images and builds

Reviewed screenshots belong in `docs/images/editor/`; temporary captures and packages in ignored `artifacts/`. See the [gallery](EDITOR_GALLERY.md) and [manual](USER_MANUAL.md).
