# Test organization

[Português (Brasil)](README.pt-BR.md) · **English**

## Test projects

- `MapStudio.Core.Tests`: domain rules, OMSI formats, import/export, editing, generation, and persistence.
- `MapStudio.Renderer.Tests`: geometry, viewport, picking, rendering, and calculations used by the native renderer.

## Conventions

1. Keep each test in the project that references the layer under test; avoid cross-references between test projects.
2. Group new tests into domain subfolders when it improves navigation (for example, `IO/`, `Omsi/`, `Generation/`); do not create one folder per class.
3. Name tests to express the scenario and expected outcome. Preserve regression tests for previously fixed defects.
4. Before moving existing tests, search for path references in workflows, scripts, documentation, and solution files. Confirm that the SDK includes files automatically and run the tests on Windows.
5. Do not reduce coverage or replace real validation with mocks just to make reorganization easier.

## Next reorganization

The incremental migration organizes nine terrain/reader tests in `MapStudio.Core.Tests/Omsi/` and ten timetable/trip tests in `MapStudio.Core.Tests/Omsi/Timetables/`. The timetable files were moved without changing test cases, and their namespaces now use `MapStudio.Core.Tests.Omsi.Timetables`. The `ci.yml`, `native-ci.yml`, and `core-editing-tests.yml` workflows filter `tests/**` or `tests/MapStudio.Core.Tests/**`; the project uses automatic C# file inclusion rather than a manual `.csproj` list. Workflow runs for the new commit still need to confirm validation.

Seven spline and spline-path tests now live in `MapStudio.Core.Tests/Omsi/Splines/`, using the `MapStudio.Core.Tests.Omsi.Splines` namespace. The explicit `OmsiSplineTests.cs` path in `native-preview-release.yml` was updated. Core.Tests still has many files directly in its root; continue incrementally by domain and verify references before each move.
