# Real OMSI assets in map creation

[Português (Brasil)](../pt-BR/OMSI_NATIVE_ASSETS.md) · **English**

## Product decision

When creating a map intended for OMSI 2, **content installed with the user's own OMSI installation is the first choice**. Map Studio should make it easy to reuse installed roads/splines, objects, bridges, tunnels, signs, street furniture, vegetation, and other assets.

Map Studio's Road Kit and procedural generators remain complementary tools. They must not silently replace compatible real OMSI assets when those assets are available.

## Expected behavior

1. **Select a real OMSI 2 installation** and index available asset folders without assuming every installation contains the same packages.
2. **Build the library from real files**:
   - `Splines/**/*.sli` for installed roads, lanes, sidewalks, rails, and other spline profiles;
   - `Sceneryobjects/**/*.sco` for objects, buildings, vegetation, signs, structures, bridges, and tunnel pieces;
   - dependencies referenced by assets, including supported O3D, textures, and auxiliary files.
3. **Provide useful search and filters** by type, name, folder/package, and dependency availability. Categorization is library metadata only and must not modify the source file.
4. **Show real previews** of the selected asset's geometry and textures, clearly identifying missing files or unsupported formats.
5. **Place assets in a real map** using appropriate OMSI formats and templates, with preview, validation, backup, and transactional persistence. Never invent unknown records or fields to force insertion.
6. **Bridges and tunnels**: let users find and combine installed OMSI assets—for example road splines, structural pieces, portals, walls, ceilings, and supporting objects. Procedural geometry remains an explicit alternative when preferred or when no compatible asset exists.
7. **Dependencies and portability**: before export/sharing, report referenced and missing assets. Do not automatically bundle third-party content or modify original installation files.
8. **No fake substitutions**: if no compatible real asset is found, explain that and offer the procedural Road Kit as a clearly identified alternative. Never present demo objects or mocks as installed assets.

## Safety and compatibility

- Never edit the global OMSI asset libraries directly; modify map files and keep source assets intact.
- Preserve unknown commands and existing OMSI references.
- Respect bridges, tunnels, and grade-separated structures when connecting roads or conforming terrain.
- Show each asset's source and relative path.
- Use representative fixtures and regression tests; mocks may exist in isolated tests only, never as content presented as real in the application.

## Acceptance criteria

- Selecting an OMSI installation makes its real assets available in the library without manual file-by-file import.
- Users can find and preview an installed `.sli` spline and `.sco` object and insert copies into a map with safe persistence.
- Installed bridge/tunnel assets are searchable and usable; procedural structures are never mislabeled as official OMSI assets.
- Missing/incompatible dependencies are reported before persistence or export.
- The source installation remains unchanged.
- Validation includes Core tests and native Windows build/tests; passing compilation alone is not sufficient to declare the feature complete.

## Implementation sequence

1. Verify and consolidate installation indexing and asset dependency tracking.
2. Ensure the library prioritizes real assets from the active installation and shows their source.
3. Integrate real SCO/SLI insertion into the creation workflow with preview and backup.
4. Add bridge/tunnel-oriented filters and workflows using real asset combinations.
5. Validate against a Windows installation with real OMSI content and document format limitations.
