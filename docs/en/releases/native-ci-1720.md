# Native build 1720 — November 27, 2026

[Português (Brasil)](../../pt-BR/releases/native-ci-1720.md)

- [Validated run](https://github.com/MichaelPriest/OMSI-Map-Studio/actions/runs/46060172393), commit `db032ec9af806a1a240eaf57e724d2aeac55d144`.
- [PR #21](https://github.com/MichaelPriest/OMSI-Map-Studio/pull/21) merged to main at `9f2c59ab0a0168256b59c63a312612c07c597729`.
- Artifact: `OMSI-Map-Studio-Windows-x64-test`, self-contained Windows x64, 99,345,444 bytes. GitHub-reported artifact ZIP SHA-256: `cb4c654d0f2d5541c2bef730d10ed9c8ca3957bae6fb2e015e735e9aeed2fb3f`. Expected expiration: December 11, 2026 UTC.
- General CI, editing tests, Core, renderer, WinUI compilation and publishing passed.

## Changes

Keyless Terrarium elevation, common datum across tiles and roads, OSM hydrography reference with islands, progress and cancellation, and SQLite connection cleanup in Windows tests.

## Acceptance

Extract the full ZIP, check BUILD-INFO.txt and SHA256SUMS.txt, and open a test workspace. Import a small area, apply Terrarium, reconstruct roads and inspect elevations and tile seams after reopening. Enable the OSM reference and inspect rivers, lakes and islands. Test cancellation before writing. Open the result in OMSI 2. Physical water is not generated in this stage.

This is a CI artifact, not a new numbered prerelease. Real screenshots and hands-on acceptance remain pending.
