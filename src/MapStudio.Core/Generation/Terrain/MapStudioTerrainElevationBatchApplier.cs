using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Terrain;

using MapStudio.Core.Omsi.Maps;

public sealed record MapStudioTerrainElevationTileResult(
    string TilePath,
    string TerrainPath,
    int TileX,
    int TileY,
    int ChangedSamples,
    string BackupPath);

public sealed record MapStudioTerrainElevationBatchResult(
    IReadOnlyList<MapStudioTerrainElevationTileResult> Tiles,
    int ChangedSamples,
    int UncoveredSamples)
{
    public int ModifiedTileCount =>
        Tiles.Count(
            tile =>
                tile.ChangedSamples > 0);
}

public sealed class MapStudioTerrainElevationBatchApplier
{
    public async Task<MapStudioTerrainElevationBatchResult>
        ApplyAsync(
            string omsiRoot,
            string mapDirectory,
            MapStudioGeographicAnchor anchor,
            MapStudioGeoreferencedElevationSurface surface,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentNullException.ThrowIfNull(surface);

        var root =
            Path.GetFullPath(
                omsiRoot);

        var mapRoot =
            Path.GetFullPath(
                mapDirectory);

        var descriptor =
            await OmsiMapCatalog
                .OpenMapAsync(
                    mapRoot,
                    cancellationToken)
                .ConfigureAwait(false);

        var backupRoot =
            Path.Combine(
                root,
                ".mapstudio",
                "backups",
                "real-world-elevation",
                DateTime.UtcNow
                    .ToString(
                        "yyyyMMdd-HHmmss",
                        System.Globalization
                            .CultureInfo
                            .InvariantCulture) +
                "-" +
                Guid.NewGuid()
                    .ToString("N")[..8]);

        Directory.CreateDirectory(
            backupRoot);

        var prepared =
            new List<PreparedTile>(
                descriptor.Tiles.Count);

        var totalChanged =
            0;

        var uncovered =
            0;

        foreach (var tile in descriptor.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        mapRoot,
                        tile.RelativeMapPath,
                        out var tilePath) ||
                !File.Exists(tilePath))
            {
                throw new InvalidDataException(
                    "realWorldElevationTilePathInvalid");
            }

            var terrainPath =
                tilePath +
                ".terrain";

            if (!File.Exists(terrainPath))
            {
                throw new InvalidDataException(
                    "realWorldElevationTerrainMissing");
            }

            var terrain =
                await new OmsiTerrainReader()
                    .ReadAsync(
                        terrainPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var sampleCount =
                terrain.SampleCount;

            var spacing =
                OmsiTileGrid.TileSize /
                terrain.CellCount;

            var heights =
                terrain.Heights
                    .ToArray();

            var changed =
                0;

            for (
                var row = 0;
                row < sampleCount;
                row++)
            {
                var worldZ =
                    tile.Y *
                        OmsiTileGrid.TileSize +
                    row *
                        spacing;

                for (
                    var column = 0;
                    column < sampleCount;
                    column++)
                {
                    var worldX =
                        tile.X *
                            OmsiTileGrid.TileSize +
                        column *
                            spacing;

                    if (
                        !surface.TrySampleRelativeHeight(
                            anchor,
                            new MapStudioRoadPoint(
                                worldX,
                                worldZ),
                            out var height))
                    {
                        uncovered++;
                        continue;
                    }

                    if (
                        height <
                            float.MinValue ||
                        height >
                            float.MaxValue)
                    {
                        throw new InvalidDataException(
                            "realWorldElevationHeightOutOfRange");
                    }

                    var index =
                        row *
                            sampleCount +
                        column;

                    var value =
                        (float)height;

                    if (
                        Math.Abs(
                            heights[index] -
                            value) <=
                        0.0001f)
                    {
                        continue;
                    }

                    heights[index] =
                        value;

                    changed++;
                }
            }

            var relative =
                Path.GetRelativePath(
                    mapRoot,
                    terrainPath);

            var backupPath =
                Path.Combine(
                    backupRoot,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    backupPath)!);

            File.Copy(
                terrainPath,
                backupPath,
                overwrite: true);

            var bytes =
                OmsiTerrainWriter
                    .Write(
                        new OmsiTerrainGrid(
                            terrain.CellCount,
                            heights));

            prepared.Add(
                new PreparedTile(
                    tilePath,
                    terrainPath,
                    tile.X,
                    tile.Y,
                    changed,
                    backupPath,
                    bytes));

            totalChanged +=
                changed;
        }

        if (uncovered > 0)
        {
            try
            {
                if (Directory.Exists(
                        backupRoot))
                {
                    Directory.Delete(
                        backupRoot,
                        recursive: true);
                }
            }
            catch
            {
                // No terrain file has been modified yet.
            }

            throw new InvalidDataException(
                $"realWorldElevationCoverageIncomplete:{uncovered}");
        }

        try
        {
            foreach (var tile in prepared)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                tile.TemporaryPath =
                    tile.TerrainPath +
                    ".mapstudio-elevation-" +
                    Guid.NewGuid()
                        .ToString("N");

                await File
                    .WriteAllBytesAsync(
                        tile.TemporaryPath,
                        tile.Bytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var tile in prepared)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                File.Move(
                    tile.TemporaryPath!,
                    tile.TerrainPath,
                    overwrite: true);

                tile.TemporaryPath =
                    null;
            }
        }
        catch
        {
            foreach (var tile in prepared)
            {
                if (File.Exists(tile.BackupPath))
                {
                    File.Copy(
                        tile.BackupPath,
                        tile.TerrainPath,
                        overwrite: true);
                }
            }

            throw;
        }
        finally
        {
            foreach (var tile in prepared)
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        tile.TemporaryPath) &&
                    File.Exists(
                        tile.TemporaryPath))
                {
                    File.Delete(
                        tile.TemporaryPath);
                }
            }
        }

        return new MapStudioTerrainElevationBatchResult(
            prepared
                .Select(
                    tile =>
                        new MapStudioTerrainElevationTileResult(
                            tile.TilePath,
                            tile.TerrainPath,
                            tile.TileX,
                            tile.TileY,
                            tile.ChangedSamples,
                            tile.BackupPath))
                .ToArray(),
            totalChanged,
            uncovered);
    }

    private sealed class PreparedTile
    {
        public PreparedTile(
            string tilePath,
            string terrainPath,
            int tileX,
            int tileY,
            int changedSamples,
            string backupPath,
            byte[] bytes)
        {
            TilePath = tilePath;
            TerrainPath = terrainPath;
            TileX = tileX;
            TileY = tileY;
            ChangedSamples = changedSamples;
            BackupPath = backupPath;
            Bytes = bytes;
        }

        public string TilePath { get; }

        public string TerrainPath { get; }

        public int TileX { get; }

        public int TileY { get; }

        public int ChangedSamples { get; }

        public string BackupPath { get; }

        public byte[] Bytes { get; }

        public string? TemporaryPath { get; set; }
    }
}
