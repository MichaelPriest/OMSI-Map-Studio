using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioInfrastructurePlacementResult(
    string TilePath,
    int TileX,
    int TileY,
    int ObjectId,
    double LocalX,
    double LocalZ,
    string SceneryObjectPath,
    string BackupPath,
    int SourceSectionOrdinal);

public sealed class MapStudioInfrastructureTilePlacer
{
    public async Task<MapStudioInfrastructurePlacementResult>
        PlaceAsync(
            string omsiRoot,
            string mapDirectory,
            MapStudioInfrastructureAssetResult asset,
            double heightMeters = 0,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentNullException.ThrowIfNull(asset);

        if (!double.IsFinite(heightMeters))
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMeters));
        }

        var root =
            Path.GetFullPath(omsiRoot);

        var mapRoot =
            Path.GetFullPath(mapDirectory);

        var descriptor =
            await OmsiMapCatalog
                .OpenMapAsync(
                    mapRoot,
                    cancellationToken)
                .ConfigureAwait(false);

        var tileX =
            OmsiTileGrid
                .WorldToTileX(
                    asset.WorldCenter.X);

        var tileY =
            OmsiTileGrid
                .WorldToTileY(
                    asset.WorldCenter.Z);

        var tileReference =
            descriptor.Tiles
                .FirstOrDefault(
                    tile =>
                        tile.X == tileX &&
                        tile.Y == tileY);

        if (tileReference is null)
        {
            throw new InvalidDataException(
                "infrastructureTargetTileMissing");
        }

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    mapRoot,
                    tileReference.RelativeMapPath,
                    out var tilePath) ||
            !File.Exists(tilePath))
        {
            throw new InvalidDataException(
                "infrastructureTargetTileInvalid");
        }

        var sceneryObjectPath =
            BuildSceneryObjectPath(
                root,
                asset.SceneryObjectPath);

        var tileContents =
            new List<OmsiTileContent>(
                descriptor.Tiles.Count);

        var tileReader =
            new OmsiTileReader();

        foreach (var tile in descriptor.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        mapRoot,
                        tile.RelativeMapPath,
                        out var candidatePath))
            {
                throw new InvalidDataException(
                    "infrastructureMapTilePathInvalid");
            }

            tileContents.Add(
                await tileReader
                    .ReadContentAsync(
                        candidatePath,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        var insertion =
            OmsiObjectInsertionAnalyzer
                .Analyze(
                    tileContents,
                    sceneryObjectPath);

        var objectId =
            insertion.GetNextId();

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var localX =
            OmsiTileGrid
                .WorldToLocalX(
                    asset.WorldCenter.X,
                    tileX);

        var localZ =
            OmsiTileGrid
                .WorldToLocalZ(
                    asset.WorldCenter.Z,
                    tileY);

        var append =
            OmsiTileObjectInserter
                .Append(
                    document,
                    new OmsiNewPlacedObject(
                        "0",
                        sceneryObjectPath,
                        objectId,
                        localX,
                        heightMeters,
                        localZ,
                        0,
                        0,
                        0,
                        Array.Empty<string>()));

        var backupDirectory =
            Path.Combine(
                root,
                ".mapstudio",
                "backups",
                "infrastructure-placement",
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
            backupDirectory);

        var backupPath =
            Path.Combine(
                backupDirectory,
                Path.GetFileName(
                    tilePath));

        File.Copy(
            tilePath,
            backupPath,
            overwrite: true);

        var temporaryPath =
            tilePath +
            ".mapstudio-tmp-" +
            Guid.NewGuid()
                .ToString("N");

        try
        {
            await File
                .WriteAllBytesAsync(
                    temporaryPath,
                    append.Bytes,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                temporaryPath,
                tilePath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }
        }

        return new MapStudioInfrastructurePlacementResult(
            tilePath,
            tileX,
            tileY,
            objectId,
            localX,
            localZ,
            sceneryObjectPath,
            backupPath,
            append.SourceSectionOrdinal);
    }

    private static string BuildSceneryObjectPath(
        string omsiRoot,
        string sceneryObjectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sceneryObjectPath);

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
                    '/',
                    '\\')
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
}
