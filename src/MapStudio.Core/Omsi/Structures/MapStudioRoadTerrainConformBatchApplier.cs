using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioRoadTerrainConformTileResult(
    string TilePath,
    string TerrainPath,
    int TileX,
    int TileY,
    int ChangedSamples,
    string BackupPath);

public sealed record MapStudioRoadTerrainConformBatchResult(
    IReadOnlyList<MapStudioRoadTerrainConformTileResult> Tiles,
    int ChangedSamples)
{
    public int ModifiedTileCount =>
        Tiles.Count(
            tile =>
                tile.ChangedSamples >
                    0);
}

public sealed class MapStudioRoadTerrainConformBatchApplier
{
    private const double RoadSurfaceClearanceMeters =
        -0.12;

    public async Task<MapStudioRoadTerrainConformBatchResult>
        ApplyAsync(
            string omsiRoot,
            string mapDirectory,
            IReadOnlyList<MapStudioRoadBatchPlacement> placements,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);

        ArgumentNullException.ThrowIfNull(
            placements);

        var groundRoads =
            placements
                .Where(
                    placement =>
                        !placement.Bridge &&
                        !placement.Tunnel &&
                        !MapStudioStandardRoadCatalog
                            .IsTerrainConformProtectedPath(
                                placement.SplinePath) &&
                        double.IsFinite(
                            placement.LengthMeters) &&
                        placement.LengthMeters >
                            0.001 &&
                        double.IsFinite(
                            placement.StartHeightMeters) &&
                        double.IsFinite(
                            placement.Rotation) &&
                        double.IsFinite(
                            placement.RadiusMeters))
                .ToArray();

        if (groundRoads.Length == 0)
        {
            return new MapStudioRoadTerrainConformBatchResult(
                Array.Empty<MapStudioRoadTerrainConformTileResult>(),
                0);
        }

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

        var working =
            new Dictionary<
                (int X, int Y),
                TerrainState>();

        foreach (
            var tile in
                descriptor.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        mapRoot,
                        tile.RelativeMapPath,
                        out var tilePath))
            {
                continue;
            }

            var terrainPath =
                tilePath +
                ".terrain";

            if (!File.Exists(
                    terrainPath))
            {
                continue;
            }

            var terrain =
                await new OmsiTerrainReader()
                    .ReadAsync(
                        terrainPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            working[
                (
                    tile.X,
                    tile.Y
                )] =
                new TerrainState(
                    tilePath,
                    terrainPath,
                    tile.X,
                    tile.Y,
                    terrain,
                    terrain);
        }

        foreach (
            var road in
                groundRoads)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var worldX =
                OmsiTileGrid
                    .GetOriginX(
                        road.TileX) +
                road.LocalX;

            var worldZ =
                OmsiTileGrid
                    .GetOriginZ(
                        road.TileY) +
                road.LocalZ;

            var physicalWidth =
                double.IsFinite(
                    road.PhysicalWidthMeters) &&
                road.PhysicalWidthMeters >
                    0
                    ? Math.Clamp(
                        road.PhysicalWidthMeters,
                        3.0,
                        30.0)
                    : 7.0;

            var halfWidth =
                physicalWidth *
                    0.5 +
                0.75;

            var featherWidth =
                Math.Clamp(
                    physicalWidth *
                        0.75,
                    5.0,
                    12.0);

            var influence =
                OmsiTerrainLeveler
                    .GetSplineInfluenceBounds(
                        worldX,
                        road.StartHeightMeters,
                        worldZ,
                        road.Rotation,
                        road.LengthMeters,
                        road.RadiusMeters,
                        road.GradientStartPercent,
                        road.GradientEndPercent,
                        halfWidth +
                            featherWidth);

            foreach (
                var state in
                    working.Values)
            {
                var bounds =
                    OmsiTileGrid
                        .GetBounds(
                            state.TileX,
                            state.TileY);

                if (
                    bounds.MaxX <
                        influence.MinX ||
                    bounds.MinX >
                        influence.MaxX ||
                    bounds.MaxZ <
                        influence.MinZ ||
                    bounds.MinZ >
                        influence.MaxZ)
                {
                    continue;
                }

                var result =
                    OmsiTerrainLeveler
                        .ConformToSpline(
                            state.Current,
                            OmsiTileGrid
                                .GetOriginX(
                                    state.TileX),
                            OmsiTileGrid
                                .GetOriginZ(
                                    state.TileY),
                            worldX,
                            road.StartHeightMeters,
                            worldZ,
                            road.Rotation,
                            road.LengthMeters,
                            road.RadiusMeters,
                            road.GradientStartPercent,
                            road.GradientEndPercent,
                            halfWidth,
                            featherWidth,
                            RoadSurfaceClearanceMeters);

                state.Current =
                    result.Terrain;
            }
        }

        var backupRoot =
            Path.Combine(
                root,
                ".mapstudio",
                "backups",
                "real-world-road-terrain",
                DateTime.UtcNow
                    .ToString(
                        "yyyyMMdd-HHmmss",
                        System.Globalization
                            .CultureInfo
                            .InvariantCulture) +
                "-" +
                Guid.NewGuid()
                    .ToString("N")[..8]);

        var prepared =
            new List<PreparedTile>();

        var totalChanged =
            0;

        foreach (
            var state in
                working.Values)
        {
            var changed =
                CountChangedSamples(
                    state.Original,
                    state.Current);

            if (changed == 0)
            {
                continue;
            }

            var relative =
                Path.GetRelativePath(
                    mapRoot,
                    state.TerrainPath);

            var backupPath =
                Path.Combine(
                    backupRoot,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    backupPath)!);

            File.Copy(
                state.TerrainPath,
                backupPath,
                overwrite:
                    true);

            prepared.Add(
                new PreparedTile(
                    state.TilePath,
                    state.TerrainPath,
                    state.TileX,
                    state.TileY,
                    changed,
                    backupPath,
                    OmsiTerrainWriter
                        .Write(
                            state.Current)));

            totalChanged +=
                changed;
        }

        if (prepared.Count == 0)
        {
            return new MapStudioRoadTerrainConformBatchResult(
                Array.Empty<MapStudioRoadTerrainConformTileResult>(),
                0);
        }

        try
        {
            foreach (
                var tile in
                    prepared)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                tile.TemporaryPath =
                    tile.TerrainPath +
                    ".mapstudio-road-conform-" +
                    Guid.NewGuid()
                        .ToString("N");

                await File
                    .WriteAllBytesAsync(
                        tile.TemporaryPath,
                        tile.Bytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (
                var tile in
                    prepared)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                File.Move(
                    tile.TemporaryPath!,
                    tile.TerrainPath,
                    overwrite:
                        true);

                tile.TemporaryPath =
                    null;
            }
        }
        catch
        {
            foreach (
                var tile in
                    prepared)
            {
                if (File.Exists(
                        tile.BackupPath))
                {
                    File.Copy(
                        tile.BackupPath,
                        tile.TerrainPath,
                        overwrite:
                            true);
                }
            }

            throw;
        }
        finally
        {
            foreach (
                var tile in
                    prepared)
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

        return new MapStudioRoadTerrainConformBatchResult(
            prepared
                .Select(
                    tile =>
                        new MapStudioRoadTerrainConformTileResult(
                            tile.TilePath,
                            tile.TerrainPath,
                            tile.TileX,
                            tile.TileY,
                            tile.ChangedSamples,
                            tile.BackupPath))
                .ToArray(),
            totalChanged);
    }

    private static int CountChangedSamples(
        OmsiTerrainGrid original,
        OmsiTerrainGrid current)
    {
        if (
            original.CellCount !=
                current.CellCount ||
            original.Heights.Count !=
                current.Heights.Count)
        {
            throw new InvalidDataException(
                "roadTerrainConformGridChangedShape");
        }

        var changed =
            0;

        for (
            var index = 0;
            index <
                original.Heights.Count;
            index++)
        {
            if (
                Math.Abs(
                    original.Heights[index] -
                    current.Heights[index]) >
                0.0001f)
            {
                changed++;
            }
        }

        return changed;
    }

    private sealed class TerrainState
    {
        public TerrainState(
            string tilePath,
            string terrainPath,
            int tileX,
            int tileY,
            OmsiTerrainGrid original,
            OmsiTerrainGrid current)
        {
            TilePath =
                tilePath;

            TerrainPath =
                terrainPath;

            TileX =
                tileX;

            TileY =
                tileY;

            Original =
                original;

            Current =
                current;
        }

        public string TilePath { get; }

        public string TerrainPath { get; }

        public int TileX { get; }

        public int TileY { get; }

        public OmsiTerrainGrid Original { get; }

        public OmsiTerrainGrid Current { get; set; }
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
            TilePath =
                tilePath;

            TerrainPath =
                terrainPath;

            TileX =
                tileX;

            TileY =
                tileY;

            ChangedSamples =
                changedSamples;

            BackupPath =
                backupPath;

            Bytes =
                bytes;
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
