using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Generation.Vegetation;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioVegetationBatchReconstructionResult(
    IReadOnlyList<MapStudioGeneratedSceneryPlacement> Placements,
    IReadOnlyList<string> ReviewVegetationIds,
    IReadOnlyList<string> MissingAssetVegetationIds,
    IReadOnlyList<string> OutsideMapVegetationIds,
    IReadOnlyList<string> BackupPaths)
{
    public int PlacedVegetationCount =>
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

public sealed class MapStudioVegetationBatchReconstructionRunner
{
    public async Task<MapStudioVegetationBatchReconstructionResult>
        RunAsync(
            string omsiRoot,
            string mapDirectory,
            string osmXml,
            MapStudioGeographicAnchor anchor,
            IReadOnlyList<OmsiAssetIndexEntry> assets,
            CancellationToken cancellationToken = default,
            MapStudioGeoreferencedElevationSurface? elevation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(osmXml);
        ArgumentNullException.ThrowIfNull(assets);

        var imported =
            new MapStudioOsmVegetationImporter()
                .Parse(
                    osmXml);

        var candidates =
            new MapStudioOsmVegetationReconstructionAdapter()
                .BuildPointCandidates(
                    imported.Points);

        var plan =
            new MapStudioSceneReconstructionPlanBuilder()
                .Build(
                    candidates);

        var automaticIds =
            plan.Features
                .Where(
                    feature =>
                        feature.AutoGenerate)
                .Select(
                    feature =>
                        feature.Id)
                .ToHashSet(
                    StringComparer.Ordinal);

        var reviewIds =
            plan.Features
                .Where(
                    feature =>
                        feature.NeedsReview)
                .Select(
                    feature =>
                        feature.Id)
                .ToArray();

        if (automaticIds.Count == 0)
        {
            return new MapStudioVegetationBatchReconstructionResult(
                Array.Empty<MapStudioGeneratedSceneryPlacement>(),
                reviewIds,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var projected =
            new MapStudioOsmVegetationProjector()
                .Project(
                    imported.Points,
                    anchor);

        var root =
            Path.GetFullPath(
                omsiRoot);

        var suggester =
            new MapStudioVegetationAssetSuggester();

        var requests =
            new List<MapStudioGeneratedSceneryPlacementRequest>();

        var missingAssetIds =
            new List<string>();

        foreach (
            var point in
                projected.Where(
                    point =>
                        automaticIds.Contains(
                            point.Id)))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var asset =
                suggester.Suggest(
                    assets,
                    [point],
                    point.Kind);

            if (asset is null)
            {
                missingAssetIds.Add(
                    point.Id);

                continue;
            }

            var sceneryPath =
                ResolveAssetPath(
                    root,
                    asset.RelativePath);

            if (!File.Exists(sceneryPath))
            {
                missingAssetIds.Add(
                    point.Id);

                continue;
            }

            requests.Add(
                new MapStudioGeneratedSceneryPlacementRequest(
                    point.Id,
                    sceneryPath,
                    point.Position,
                    HeightMeters:
                        0,
                    Rotation:
                        ResolveStableRotation(
                            point.Id)));
        }

        if (requests.Count == 0)
        {
            return new MapStudioVegetationBatchReconstructionResult(
                Array.Empty<MapStudioGeneratedSceneryPlacement>(),
                reviewIds,
                missingAssetIds,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var written =
            await new MapStudioGeneratedSceneryBatchWriter()
                .WriteAsync(
                    root,
                    mapDirectory,
                    requests,
                    cancellationToken)
                .ConfigureAwait(false);

        return new MapStudioVegetationBatchReconstructionResult(
            written.Placements,
            reviewIds,
            missingAssetIds,
            written.OutsideMapIds,
            written.BackupPaths);
    }

    private static string ResolveAssetPath(
        string omsiRoot,
        string relativePath)
    {
        if (
            string.IsNullOrWhiteSpace(
                relativePath) ||
            Path.IsPathRooted(
                relativePath))
        {
            throw new InvalidDataException(
                "vegetationAssetPathInvalid");
        }

        var normalized =
            relativePath
                .Replace(
                    '\\',
                    Path.DirectorySeparatorChar)
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar);

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    omsiRoot,
                    normalized));

        var root =
            Path.GetFullPath(
                omsiRoot)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        var rootPrefix =
            root +
            Path.DirectorySeparatorChar;

        if (
            !fullPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "vegetationAssetPathInvalid");
        }

        return fullPath;
    }

    private static double ResolveStableRotation(
        string id)
    {
        unchecked
        {
            uint hash =
                2166136261;

            foreach (var character in id)
            {
                hash ^=
                    character;

                hash *=
                    16777619;
            }

            return (
                hash %
                36000
            ) /
            100.0;
        }
    }
}
