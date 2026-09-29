using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public sealed record MapStudioGeneratedSceneryPlacementRequest(
    string Id,
    string SceneryObjectPath,
    MapStudioRoadPoint WorldCenter,
    double HeightMeters = 0,
    double Rotation = 0,
    double Pitch = 0,
    double Bank = 0);

public sealed record MapStudioGeneratedSceneryPlacement(
    string Id,
    string TilePath,
    int TileX,
    int TileY,
    int ObjectId,
    double LocalX,
    double LocalZ,
    string SceneryObjectPath);

public sealed record MapStudioGeneratedSceneryBatchWriteResult(
    IReadOnlyList<MapStudioGeneratedSceneryPlacement> Placements,
    IReadOnlyList<string> OutsideMapIds,
    IReadOnlyList<string> BackupPaths)
{
    public int ModifiedTileCount =>
        Placements
            .Select(
                placement =>
                    (
                        placement.TileX,
                        placement.TileY
                    ))
            .Distinct()
            .Count();
}

public sealed class MapStudioGeneratedSceneryBatchWriter
{
    public async Task<MapStudioGeneratedSceneryBatchWriteResult>
        WriteAsync(
            string omsiRoot,
            string mapDirectory,
            IReadOnlyList<MapStudioGeneratedSceneryPlacementRequest> requests,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return new MapStudioGeneratedSceneryBatchWriteResult(
                Array.Empty<MapStudioGeneratedSceneryPlacement>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        ValidateRequests(
            requests);

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

        var tiles =
            new Dictionary<
                (int X, int Y),
                TileState>();

        var maximumUsedId =
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
                    "generatedSceneryMapTilePathInvalid");
            }

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            maximumUsedId =
                Math.Max(
                    maximumUsedId,
                    OmsiTileElementIdScanner
                        .FindMaxUsedId(
                            document));

            if (
                !tiles.TryAdd(
                    (
                        tile.X,
                        tile.Y
                    ),
                    new TileState(
                        tile.X,
                        tile.Y,
                        tilePath,
                        document)))
            {
                throw new InvalidDataException(
                    "generatedSceneryDuplicateTileCoordinate");
            }
        }

        var outsideMapIds =
            new List<string>();

        var placeable =
            new List<PlannedPlacement>();

        foreach (var request in requests)
        {
            var tileX =
                OmsiTileGrid
                    .WorldToTileX(
                        request.WorldCenter.X);

            var tileY =
                OmsiTileGrid
                    .WorldToTileY(
                        request.WorldCenter.Z);

            if (
                !tiles.TryGetValue(
                    (
                        tileX,
                        tileY
                    ),
                    out var tile))
            {
                outsideMapIds.Add(
                    request.Id);

                continue;
            }

            if (
                maximumUsedId ==
                int.MaxValue)
            {
                throw new InvalidDataException(
                    "objectIdExhausted");
            }

            maximumUsedId++;

            var sceneryObjectPath =
                NormalizeSceneryObjectPath(
                    root,
                    request.SceneryObjectPath);

            placeable.Add(
                new PlannedPlacement(
                    request,
                    tile,
                    sceneryObjectPath,
                    maximumUsedId,
                    OmsiTileGrid
                        .WorldToLocalX(
                            request.WorldCenter.X,
                            tile.X),
                    OmsiTileGrid
                        .WorldToLocalZ(
                            request.WorldCenter.Z,
                            tile.Y)));
        }

        if (placeable.Count == 0)
        {
            return new MapStudioGeneratedSceneryBatchWriteResult(
                Array.Empty<MapStudioGeneratedSceneryPlacement>(),
                outsideMapIds,
                Array.Empty<string>());
        }

        var writes =
            new List<TileWrite>();

        foreach (
            var group in
                placeable
                    .GroupBy(
                        placement =>
                            (
                                placement.Tile.X,
                                placement.Tile.Y
                            )))
        {
            var placements =
                group.ToArray();

            var tile =
                placements[0]
                    .Tile;

            var appended =
                OmsiTileObjectInserter
                    .AppendMany(
                        tile.Document,
                        placements
                            .Select(
                                placement =>
                                    new OmsiNewPlacedObject(
                                        "0",
                                        placement
                                            .SceneryObjectPath,
                                        placement.ObjectId,
                                        placement.Request
                                            .WorldCenter is { }
                                            ? placement.LocalX
                                            : placement.LocalX,
                                        placement.Request
                                            .HeightMeters,
                                        placement.LocalZ,
                                        placement.Request
                                            .Rotation,
                                        placement.Request
                                            .Pitch,
                                        placement.Request
                                            .Bank,
                                        Array.Empty<string>()))
                            .ToArray());

            writes.Add(
                new TileWrite(
                    tile,
                    appended.Bytes));
        }

        var backupRoot =
            Path.Combine(
                root,
                ".mapstudio",
                "backups",
                "generated-scenery-batch",
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

        var backupPaths =
            new List<string>(
                writes.Count);

        foreach (var write in writes)
        {
            var relative =
                Path.GetRelativePath(
                    mapRoot,
                    write.Tile.Path);

            var backupPath =
                Path.Combine(
                    backupRoot,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    backupPath)!);

            File.Copy(
                write.Tile.Path,
                backupPath,
                overwrite: true);

            write.BackupPath =
                backupPath;

            backupPaths.Add(
                backupPath);
        }

        try
        {
            foreach (var write in writes)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                write.TemporaryPath =
                    write.Tile.Path +
                    ".mapstudio-tmp-" +
                    Guid.NewGuid()
                        .ToString("N");

                await File
                    .WriteAllBytesAsync(
                        write.TemporaryPath,
                        write.Bytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var write in writes)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                File.Move(
                    write.TemporaryPath!,
                    write.Tile.Path,
                    overwrite: true);

                write.TemporaryPath =
                    null;
            }
        }
        catch
        {
            foreach (var write in writes)
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        write.BackupPath) &&
                    File.Exists(
                        write.BackupPath))
                {
                    File.Copy(
                        write.BackupPath,
                        write.Tile.Path,
                        overwrite: true);
                }
            }

            throw;
        }
        finally
        {
            foreach (var write in writes)
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        write.TemporaryPath) &&
                    File.Exists(
                        write.TemporaryPath))
                {
                    File.Delete(
                        write.TemporaryPath);
                }
            }
        }

        return new MapStudioGeneratedSceneryBatchWriteResult(
            placeable
                .Select(
                    placement =>
                        new MapStudioGeneratedSceneryPlacement(
                            placement.Request.Id,
                            placement.Tile.Path,
                            placement.Tile.X,
                            placement.Tile.Y,
                            placement.ObjectId,
                            placement.LocalX,
                            placement.LocalZ,
                            placement.SceneryObjectPath))
                .ToArray(),
            outsideMapIds,
            backupPaths);
    }

    private static void ValidateRequests(
        IReadOnlyList<MapStudioGeneratedSceneryPlacementRequest> requests)
    {
        var ids =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var request in requests)
        {
            if (
                string.IsNullOrWhiteSpace(
                    request.Id) ||
                string.IsNullOrWhiteSpace(
                    request.SceneryObjectPath) ||
                !double.IsFinite(
                    request.WorldCenter.X) ||
                !double.IsFinite(
                    request.WorldCenter.Z) ||
                !double.IsFinite(
                    request.HeightMeters) ||
                !double.IsFinite(
                    request.Rotation) ||
                !double.IsFinite(
                    request.Pitch) ||
                !double.IsFinite(
                    request.Bank) ||
                !ids.Add(
                    request.Id))
            {
                throw new InvalidDataException(
                    "invalidGeneratedSceneryBatch");
            }
        }
    }

    private static string NormalizeSceneryObjectPath(
        string omsiRoot,
        string sceneryObjectPath)
    {
        var fullAssetPath =
            Path.GetFullPath(
                sceneryObjectPath);

        if (!File.Exists(fullAssetPath))
        {
            throw new InvalidDataException(
                "generatedSceneryAssetMissing");
        }

        var relative =
            Path.GetRelativePath(
                omsiRoot,
                fullAssetPath);

        if (
            string.Equals(
                relative,
                "..",
                StringComparison.Ordinal) ||
            relative.StartsWith(
                ".." +
                Path.DirectorySeparatorChar,
                StringComparison.Ordinal) ||
            relative.StartsWith(
                ".." +
                Path.AltDirectorySeparatorChar,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "generatedSceneryAssetOutsideOmsiRoot");
        }

        var normalized =
            relative
                .Replace(
                    Path.AltDirectorySeparatorChar,
                    Path.DirectorySeparatorChar)
                .Replace(
                    Path.DirectorySeparatorChar,
                    '\\');

        if (
            !normalized.StartsWith(
                "Sceneryobjects\\",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "generatedSceneryAssetNotSceneryObject");
        }

        return normalized;
    }

    private sealed record TileState(
        int X,
        int Y,
        string Path,
        OmsiConfigDocument Document);

    private sealed record PlannedPlacement(
        MapStudioGeneratedSceneryPlacementRequest Request,
        TileState Tile,
        string SceneryObjectPath,
        int ObjectId,
        double LocalX,
        double LocalZ);

    private sealed class TileWrite
    {
        public TileWrite(
            TileState tile,
            byte[] bytes)
        {
            Tile = tile;
            Bytes = bytes;
        }

        public TileState Tile { get; }
        public byte[] Bytes { get; }
        public string? BackupPath { get; set; }
        public string? TemporaryPath { get; set; }
    }
}
