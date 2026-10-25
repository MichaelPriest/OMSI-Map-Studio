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

The Core.Tests project has many files directly in its root. Migration should be incremental and domain-based, starting with one coherent group and checking all explicit paths in `native-preview-release.yml`. The existing `IO/` folder is an example of grouping; do not move the entire suite at once.
