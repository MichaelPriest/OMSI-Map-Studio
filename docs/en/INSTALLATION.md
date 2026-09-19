# Windows installation (.exe)

OMSI Map Studio now keeps two Windows x64 distribution formats:

- **EXE installer**: recommended for regular users. It installs under `%LOCALAPPDATA%\Programs\OMSI Map Studio`, creates a Start Menu entry, offers an optional desktop shortcut, and includes an uninstaller.
- **Portable ZIP**: remains available for testing and no-install use.

## How the EXE is produced

The application is still published as a self-contained `.NET 10` `win-x64` desktop build. The published directory contains the WPF host, WebView2 dependencies, and the compiled React UI under `ui/`. Inno Setup packages that complete directory into a single `OMSI-Map-Studio-Setup-<version>-win-x64.exe` download.

This is intentional: the user downloads **one installer .exe**, while the installed application keeps the internal files WebView2 needs at runtime.

The installer is per-user by default and does not require administrator privileges. Installer source lives at `installer/MapStudio.iss`, and `installer/build-installer.ps1` reproduces the process locally or in CI.

## Dependencies

The package is self-contained for the .NET runtime. Windows still needs the Microsoft Edge WebView2 Runtime; this dependency remains separate and should receive a friendly host-side check before the stable release.

## Release rule

The EXE installer should be published alongside the portable ZIP and its SHA-256 file. Test releases remain prereleases and should only be produced after Core, React, and Desktop validation passes.
