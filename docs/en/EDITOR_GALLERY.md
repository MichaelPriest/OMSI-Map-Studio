# Native editor gallery

[Português (Brasil)](../pt-BR/EDITOR_GALLERY.md)

Images must be PNG screenshots of the actual executable, not mockups. No new verified screenshots are available in this update. Use the [validated Windows x64 build](https://github.com/MichaelPriest/OMSI-Map-Studio/actions/runs/46060172393), open an authorized OMSI 2 installation in a separate workspace and capture the real window (Alt+Print Screen or Win+Shift+S). Avoid personal data, credentials and unauthorized content.

| Screenshot to capture | Planned file |
| --- | --- |
| 3D overview and modules | `docs/images/editor/01-overview.png` |
| Road Studio: start, continuation and precision | `docs/images/editor/02-road-studio.png` |
| Longitudinal profile | `docs/images/editor/03-road-profile.png` |
| Geo Studio: OSM and Terrarium selection | `docs/images/editor/04-geo-studio.png` |
| Reconstructed terrain and tile continuity | `docs/images/editor/05-terrain.png` |
| Hydrography reference with islands | `docs/images/editor/06-hydrography.png` |
| Library and Inspector | `docs/images/editor/07-assets-inspector.png` |
| Map validation | `docs/images/editor/08-validation.png` |

Record the commit from BUILD-INFO.txt, resolution, DPI and scenario with the images. Reuse the same PNGs across both languages and embed links only after files exist. Screenshots must show actual behavior, including limitations, rather than simulating missing features.

## Automated capture

Native CI has a separate Windows capture job. It starts the real build, attempts to capture the foreground window and, when accessible, the Road Studio and Geo Studio screens without a loaded map. The `OMSI-Map-Studio-editor-screenshots` artifact includes PNGs and a SHA-256 manifest. The runner may lack an interactive desktop; capture is then reported unavailable without invalidating the build. Visual review is required before embedding screenshots in the gallery.

For a real loaded map, run from the repository root in an interactive Windows session:

```powershell
powershell -NoProfile -File tools/capture_native_editor.ps1 -ProcessId (Get-Process MapStudio.Native).Id -OutputDirectory docs/images/editor -Name 05-terrain -Commit '<commit from BUILD-INFO.txt>'
```

Keep only one editor instance open. Review PNGs for the intended panel and absence of credentials. The script neither creates artificial maps nor saves changes to the map.
