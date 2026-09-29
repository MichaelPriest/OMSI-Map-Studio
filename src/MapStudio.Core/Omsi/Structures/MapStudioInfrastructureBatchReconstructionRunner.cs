using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioInfrastructureBatchPlacement(
    string FeatureId,
    MapStudioOsmInfrastructureKind Kind,
    string TilePath,
    int TileX,
    int TileY,
    int ObjectId,
    double LocalX,
    double LocalZ,
    string SceneryObjectPath);

public sealed record MapStudioInfrastructureBatchResult(
    IReadOnlyList<MapStudioInfrastructureBatchPlacement> Placements,
    IReadOnlyList<string> ReviewFeatureIds,
    IReadOnlyList<string> OutsideMapFeatureIds,
    IReadOnlyList<string> RejectedGeometryFeatureIds,
    IReadOnlyList<string> BackupPaths)
{
    public int GeneratedAssetCount =>
        Placements.Count;

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

public sealed class MapStudioInfrastructureBatchReconstructionRunner
{
    public async Task<MapStudioInfrastructureBatchResult>
        RunAsync(
            string omsiRoot,
            string mapDirectory,
            string osmXml,
            MapStudioGeographicAnchor anchor,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(osmXml);

        var imported =
            new MapStudioOsmInfrastructureImporter()
                .Parse(osmXml);

        var candidates =
            new MapStudioOsmInfrastructureReconstructionAdapter()
                .BuildCandidates(
                    imported.Features);

        var plan =
            new MapStudioSceneReconstructionPlanBuilder()
                .Build(candidates);

        var autoGenerateIds =
            plan.Features
                .Where(
                    feature =>
                        feature.AutoGenerate)
                .Select(
                    feature =>
                        feature.Id)
                .ToHashSet(
                    StringComparer.Ordinal);

        var reviewFeatureIds =
            plan.Features
                .Where(
                    feature =>
                        feature.NeedsReview)
                .Select(
                    feature =>
                        feature.Id)
                .ToArray();

        if (autoGenerateIds.Count == 0)
        {
            return new MapStudioInfrastructureBatchResult(
                Array.Empty<MapStudioInfrastructureBatchPlacement>(),
                reviewFeatureIds,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var selected =
            imported.Features
                .Where(
                    feature =>
                        autoGenerateIds.Contains(
                            feature.Id))
                .ToArray();

        var projected =
            new MapStudioOsmInfrastructureProjector()
                .Project(
                    selected,
                    anchor);

        var projectedIds =
            projected
                .Select(
                    feature =>
                        feature.Id)
                .ToHashSet(
                    StringComparer.Ordinal);

        var rejectedGeometryFeatureIds =
            selected
                .Where(
                    feature =>
                        !projectedIds.Contains(
                            feature.Id))
                .Select(
                    feature =>
                        feature.Id)
                .ToArray();

        if (projected.Count == 0)
        {
            return new MapStudioInfrastructureBatchResult(
                Array.Empty<MapStudioInfrastructureBatchPlacement>(),
                reviewFeatureIds,
                Array.Empty<string>(),
                rejectedGeometryFeatureIds,
                Array.Empty<string>());
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
                    "infrastructureMapTilePathInvalid");
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

            var coordinate =
                (
                    tile.X,
                    tile.Y
                );

            if (
                !tiles.TryAdd(
                    coordinate,
                    new TileState(
                        tile.X,
                        tile.Y,
                        tilePath,
                        document)))
            {
                throw new InvalidDataException(
                    "infrastructureDuplicateTileCoordinate");
            }
        }

        var placeable =
            new List<PlaceableFeature>();

        var outsideMapFeatureIds =
            new List<string>();

        foreach (var feature in projected)
        {
            var tileX =
                OmsiTileGrid
                    .WorldToTileX(
                        feature.Center.X);

            var tileY =
                OmsiTileGrid
                    .WorldToTileY(
                        feature.Center.Z);

            if (
                !tiles.TryGetValue(
                    (
                        tileX,
                        tileY
                    ),
                    out var tile))
            {
                outsideMapFeatureIds.Add(
                    feature.Id);

                continue;
            }

            placeable.Add(
                new PlaceableFeature(
                    feature,
                    tile));
        }

        if (placeable.Count == 0)
        {
            return new MapStudioInfrastructureBatchResult(
                Array.Empty<MapStudioInfrastructureBatchPlacement>(),
                reviewFeatureIds,
                outsideMapFeatureIds,
                rejectedGeometryFeatureIds,
                Array.Empty<string>());
        }

        if (
            maximumUsedId >
            int.MaxValue -
                placeable.Count)
        {
            throw new InvalidDataException(
                "objectIdExhausted");
        }

        var generated =
            new List<GeneratedPlacement>(
                placeable.Count);

        var assetGenerator =
            new MapStudioInfrastructureAssetGenerator();

        foreach (var item in placeable)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var asset =
                await assetGenerator
                    .GenerateAsync(
                        root,
                        item.Feature,
                        cancellationToken)
                    .ConfigureAwait(false);

            var sceneryObjectPath =
                BuildSceneryObjectPath(
                    root,
                    asset.SceneryObjectPath);

            maximumUsedId++;

            var localX =
                OmsiTileGrid
                    .WorldToLocalX(
                        item.Feature.Center.X,
                        item.Tile.X);

            var localZ =
                OmsiTileGrid
                    .WorldToLocalZ(
                        item.Feature.Center.Z,
                        item.Tile.Y);

            generated.Add(
                new GeneratedPlacement(
                    item.Feature,
                    item.Tile,
                    asset,
                    sceneryObjectPath,
                    maximumUsedId,
                    localX,
                    localZ));
        }

        var writes =
            new List<TileWrite>();

        foreach (
            var group in
                generated
                    .GroupBy(
                        placement =>
                            (
                                placement.Tile.X,
                                placement.Tile.Y
                            )))
        {
            var tile =
                group.First()
                    .Tile;

            var placements =
                group.ToArray();

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
                                        placement.LocalX,
                                        0,
                                        placement.LocalZ,
                                        0,
                                        0,
                                        0,
                                        Array.Empty<string>()))
                            .ToArray());

            writes.Add(
                new TileWrite(
                    tile,
                    appended.Bytes,
                    placements));
        }

        var backupRoot =
            Path.Combine(
                root,
                ".mapstudio",
                "backups",
                "infrastructure-batch",
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

        var resultPlacements =
            generated
                .Select(
                    placement =>
                        new MapStudioInfrastructureBatchPlacement(
                            placement.Feature.Id,
                            placement.Feature.Kind,
                            placement.Tile.Path,
                            placement.Tile.X,
                            placement.Tile.Y,
                            placement.ObjectId,
                            placement.LocalX,
                            placement.LocalZ,
                            placement.SceneryObjectPath))
                .ToArray();

        return new MapStudioInfrastructureBatchResult(
            resultPlacements,
            reviewFeatureIds,
            outsideMapFeatureIds,
            rejectedGeometryFeatureIds,
            backupPaths);
    }

    private static string BuildSceneryObjectPath(
        string omsiRoot,
        string sceneryObjectPath)
    {
        var root =
            Path.GetFullPath(
                omsiRoot)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        var fullAssetPath =
            Path.GetFullPath(
                sceneryObjectPath);

        var relative =
            Path.GetRelativePath(
                root,
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
                "infrastructureAssetOutsideOmsiRoot");
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
                "infrastructureAssetNotSceneryObject");
        }

        return normalized;
    }

    private sealed record TileState(
        int X,
        int Y,
        string Path,
        OmsiConfigDocument Document);

    private sealed record PlaceableFeature(
        MapStudioProjectedInfrastructureFeature Feature,
        TileState Tile);

    private sealed record GeneratedPlacement(
        MapStudioProjectedInfrastructureFeature Feature,
        TileState Tile,
        MapStudioInfrastructureAssetResult Asset,
        string SceneryObjectPath,
        int ObjectId,
        double LocalX,
        double LocalZ);

    private sealed class TileWrite
    {
        public TileWrite(
            TileState tile,
            byte[] bytes,
            IReadOnlyList<GeneratedPlacement> placements)
        {
            Tile = tile;
            Bytes = bytes;
            Placements = placements;
        }

        public TileState Tile { get; }
        public byte[] Bytes { get; }
        public IReadOnlyList<GeneratedPlacement>
            Placements { get; }
        public string? BackupPath { get; set; }
        public string? TemporaryPath { get; set; }
    }
}
