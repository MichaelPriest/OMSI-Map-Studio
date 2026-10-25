# OMSI Map Studio repository folder organization

[Português (Brasil)](../pt-BR/REPOSITORY_STRUCTURE.md) · **English**

## General rule

The repository root should contain only global entry points and configuration. Product code, tests, documentation, installer files, and tools belong in dedicated folders. Do not perform mass moves without updating references, solutions, workflows, and relative paths and running Windows CI.

## Canonical structure

```text
OMSI-Map-Studio/
├── .github/                 # repository workflows and automation
├── docs/
│   ├── pt-BR/               # Portuguese documentation
│   │   └── releases/        # Portuguese release notes
│   ├── en/                  # English documentation
│   │   └── releases/        # English release notes
│   ├── ARCHITECTURE.md      # shared architecture overview
│   └── ROADMAP.md           # shared technical direction
├── installer/               # packaging scripts/projects
├── src/
│   ├── MapStudio.Core/      # domain, OMSI formats, editing and generation
│   ├── MapStudio.Native/    # native desktop host, windows and services
│   ├── MapStudio.Renderer/  # rendering, scene, picking and shaders
│   ├── MapStudio.Desktop/   # desktop integration and assets
│   └── MapStudio.UI/        # web UI when used by the product
├── tests/                   # component-oriented tests
├── tools/                   # development utilities
├── .gitignore               # local artifact exclusions
├── Directory.Build.props    # shared .NET properties
├── README.md                # short entry point
├── README.pt-BR.md          # Portuguese overview
└── README.en.md             # English overview
```

## Maintenance rules

- **No build folders at the root**: `bin/`, `obj/`, `node_modules/`, `dist/`, logs, local profiles, and temporary results are not source code.
- **Organize Core by domain**: put new functionality in an existing subdomain (e.g. `Editing`, `Generation`, `IO`, `Omsi`, `Workspace`) instead of adding generic root folders.
- **Separate UI from domain logic**: visual components belong in Native/Renderer/UI according to responsibility; OMSI file rules and persistence must not move into the UI.
- **Tests near each component**: use the corresponding existing test project. Add a new test project only for a genuine boundary.
- **Documentation by language**: keep Portuguese and English in parallel structures.
- **Organize release notes**: notes belong under `docs/pt-BR/releases/` and `docs/en/releases/`, never at the root. Any further grouping by release line must update links and be done separately.
- **Limit global files**: new root files need a reason (global configuration, entry point, or primary instructions).
- **Do not delete or move by appearance alone**: search project files, scripts, workflows, documentation, and relative paths first.
- **No functional changes from this policy**: folder cleanup must not alter OMSI formats, editor behavior, or map content.

## Initial audit

The current Git tree has five root files and six top-level directories (`.github`, `docs`, `installer`, `src`, `tests`, `tools`). Therefore, the bulk is not in the root itself: it is primarily under `docs/*/releases`, plus files inside component projects. Local `bin/obj` or other generated folders should be treated as build artifacts and not committed.

This audit does not rename or move existing files. It establishes a target structure so cleanup can proceed safely and incrementally.
