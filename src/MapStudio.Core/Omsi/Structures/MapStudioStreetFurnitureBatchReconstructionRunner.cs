using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioStreetFurnitureBatchReconstructionResult(
    IReadOnlyList<MapStudioGeneratedSceneryPlacement> Placements,
    IReadOnlyList<string> ReviewFeatureIds,
    IReadOnlyList<string> MissingAssetFeatureIds,
    IReadOnlyList<string> OutsideMapFeatureIds,
    IReadOnlyList<string> BackupPaths)
{
    public int PlacedFeatureCount =>
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

public sealed class MapStudioStreetFurnitureBatchReconstructionRunner
{
    public async Task<MapStudioStreetFurnitureBatchReconstructionResult>
        RunAsync(
            string omsiRoot,
            string mapDirectory,
            string osmXml,
            MapStudioGeographicAnchor anchor,
            IReadOnlyList<OmsiAssetIndexEntry> assets,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<MapStudioSceneEvidence>>?
                additionalEvidence = null,
            CancellationToken cancellationToken = default,
            MapStudioGeoreferencedElevationSurface? elevation = null,
            IReadOnlySet<string>?
                excludedFeatureIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(osmXml);
        ArgumentNullException.ThrowIfNull(assets);

        var imported =
            new MapStudioOsmStreetFurnitureImporter()
                .Parse(osmXml);

        var eligiblePoints =
            excludedFeatureIds is null ||
            excludedFeatureIds.Count ==
                0
                ? imported.Points
                : imported.Points
                    .Where(
                        point =>
                            !excludedFeatureIds
                                .Contains(
                                    point.Id))
                    .ToArray();

        var candidates =
            new MapStudioOsmStreetFurnitureReconstructionAdapter()
                .BuildCandidates(
                    eligiblePoints);

        if (
            additionalEvidence is not null &&
            additionalEvidence.Count > 0)
        {
            candidates =
                candidates
                    .Select(
                        candidate =>
                            additionalEvidence.TryGetValue(
                                candidate.Id,
                                out var evidence)
                                ? candidate with
                                {
                                    Evidence =
                                        [
                                            ..candidate.Evidence,
                                            ..evidence
                                        ]
                                }
                                : candidate)
                    .ToArray();
        }

        var plan =
            new MapStudioSceneReconstructionPlanBuilder()
                .Build(candidates);

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

        var root =
            Path.GetFullPath(
                omsiRoot);

        var suggester =
            new MapStudioStreetFurnitureAssetSuggester();

        var missingAssetIds =
            new List<string>();

        var requests =
            new List<MapStudioGeneratedSceneryPlacementRequest>();

        foreach (
            var point in
                eligiblePoints.Where(
                    point =>
                        automaticIds.Contains(
                            point.Id)))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var asset =
                suggester.Suggest(
                    assets,
                    point);

            if (asset is null)
            {
                missingAssetIds.Add(
                    point.Id);

                continue;
            }

            var assetPath =
                ResolveAssetPath(
                    root,
                    asset.RelativePath);

            if (!File.Exists(assetPath))
            {
                missingAssetIds.Add(
                    point.Id);

                continue;
            }

            var position =
                MapStudioGeographicProjection
                    .Project(
                        anchor,
                        new MapStudioGeoRoadPoint(
                            point.Latitude,
                            point.Longitude));

            requests.Add(
                new MapStudioGeneratedSceneryPlacementRequest(
                    point.Id,
                    assetPath,
                    position,
                    HeightMeters:
                        0,
                    Rotation:
                        ResolvePlacementRotation(
                            point)));
        }

        if (requests.Count == 0)
        {
            return new MapStudioStreetFurnitureBatchReconstructionResult(
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

        return new MapStudioStreetFurnitureBatchReconstructionResult(
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
                "streetFurnitureAssetPathInvalid");
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

        if (
            !fullPath.StartsWith(
                root +
                Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "streetFurnitureAssetPathInvalid");
        }

        return fullPath;
    }

    private static double ResolvePlacementRotation(
        MapStudioGeoStreetFurniturePoint point)
    {
        if (
            point.DirectionDegrees is
                { } bearing &&
            double.IsFinite(
                bearing))
        {
            return NormalizeDegrees(
                180.0 -
                bearing);
        }

        return ResolveStableRotation(
            point.Id);
    }

    private static double NormalizeDegrees(
        double degrees)
    {
        var normalized =
            degrees %
            360.0;

        return normalized <
            0
                ? normalized +
                    360.0
                : normalized;
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
