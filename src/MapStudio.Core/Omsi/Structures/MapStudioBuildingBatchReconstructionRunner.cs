using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Buildings;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Indexing;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioBuildingBatchReconstructionResult(
    IReadOnlyList<MapStudioGeneratedSceneryPlacement> Placements,
    IReadOnlyList<string> ReviewBuildingIds,
    IReadOnlyList<string> OutsideMapBuildingIds,
    IReadOnlyList<string> RejectedGeometryBuildingIds,
    IReadOnlyList<string> BackupPaths,
    int VisualRefinedBuildingCount = 0,
    int InstalledOmsiBuildingCount = 0,
    int ProceduralBuildingCount = 0)
{
    public int GeneratedBuildingCount =>
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

public sealed class MapStudioBuildingBatchReconstructionRunner
{
    public async Task<MapStudioBuildingBatchReconstructionResult>
        RunAsync(
            string omsiRoot,
            string mapDirectory,
            string osmXml,
            MapStudioGeographicAnchor anchor,
            CancellationToken cancellationToken = default,
            MapStudioGeoreferencedElevationSurface? elevation = null,
            IMapStudioBuildingVisualEvidenceProvider?
                visualEvidenceProvider = null,
            IProgress<MapStudioBuildingVisualEvidenceProgress>?
                visualEvidenceProgress = null,
            IReadOnlyList<OmsiAssetIndexEntry>?
                installedAssets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(osmXml);

        var imported =
            new MapStudioOsmBuildingImporter()
                .Parse(
                    osmXml);

        var projected =
            new MapStudioOsmBuildingProjector()
                .Project(
                    imported.Buildings,
                    anchor);

        var projectedIds =
            projected
                .Select(
                    building =>
                        building.Id)
                .ToHashSet(
                    StringComparer.Ordinal);

        var rejectedGeometryIds =
            imported.Buildings
                .Where(
                    building =>
                        !projectedIds.Contains(
                            building.Id))
                .Select(
                    building =>
                        building.Id)
                .ToArray();

        var adapter =
            new MapStudioOsmBuildingReconstructionAdapter();

        var planBuilder =
            new MapStudioSceneReconstructionPlanBuilder();

        var candidates =
            adapter
                .BuildCandidates(
                    projected);

        var plan =
            planBuilder
                .Build(
                    candidates);

        var visualRefinedBuildingCount =
            0;

        if (
            visualEvidenceProvider is not null)
        {
            var initialReviewIds =
                plan.Features
                    .Where(
                        feature =>
                            feature.NeedsReview)
                    .Select(
                        feature =>
                            feature.Id)
                    .ToHashSet(
                        StringComparer.Ordinal);

            var targets =
                projected
                    .Where(
                        building =>
                            initialReviewIds.Contains(
                                building.Id) ||
                            NeedsVisualAppearanceRefinement(
                                building))
                    .ToArray();

            if (targets.Length > 0)
            {

                var visualEvidence =
                    await visualEvidenceProvider
                        .AnalyzeAsync(
                            targets,
                            anchor,
                            visualEvidenceProgress,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (visualEvidence.Count > 0)
                {
                    visualRefinedBuildingCount =
                        visualEvidence.Count;

                    var refiner =
                        new MapStudioStreetLevelBuildingRefiner();

                    projected =
                        projected
                            .Select(
                                building =>
                                    visualEvidence.TryGetValue(
                                        building.Id,
                                        out var visual)
                                        ? AttachVisualAttribution(
                                            refiner.Refine(
                                                building,
                                                visual.Analysis),
                                            visual.Evidence)
                                        : building)
                            .ToArray();

                    candidates =
                        adapter
                            .BuildCandidates(
                                projected)
                            .Select(
                                candidate =>
                                    visualEvidence.TryGetValue(
                                        candidate.Id,
                                        out var visual)
                                        ? candidate with
                                        {
                                            Evidence =
                                                [
                                                    ..candidate.Evidence,
                                                    ..visual.Evidence
                                                ]
                                        }
                                        : candidate)
                            .ToArray();

                    plan =
                        planBuilder
                            .Build(
                                candidates);
                }
            }
        }

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
            return new MapStudioBuildingBatchReconstructionResult(
                Array.Empty<MapStudioGeneratedSceneryPlacement>(),
                reviewIds,
                Array.Empty<string>(),
                rejectedGeometryIds,
                Array.Empty<string>(),
                visualRefinedBuildingCount);
        }

        var generator =
            new MapStudioFootprintBuildingAssetGenerator();

        var originalBuildingAssets =
            (installedAssets ??
             Array.Empty<OmsiAssetIndexEntry>())
                .Where(
                    asset =>
                        asset.Kind ==
                            OmsiAssetKind.SceneryObject &&
                        MapStudioOriginalOmsiAssetCatalog
                            .Classify(
                                asset) ==
                            MapStudioOriginalOmsiAssetRole
                                .BuildingObject)
                .Where(
                    asset =>
                        File.Exists(
                            ResolveAssetPath(
                                omsiRoot,
                                asset.RelativePath)))
                .OrderBy(
                    asset =>
                        asset.RelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var requests =
            new List<MapStudioGeneratedSceneryPlacementRequest>();

        var installedOmsiBuildingCount =
            0;

        var proceduralBuildingCount =
            0;

        foreach (
            var building in
                projected.Where(
                    building =>
                        automaticIds.Contains(
                            building.Id)))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var installedAsset =
                SelectInstalledBuildingAsset(
                    originalBuildingAssets,
                    building);

            if (installedAsset is not null)
            {
                requests.Add(
                    new MapStudioGeneratedSceneryPlacementRequest(
                        building.Id,
                        ResolveAssetPath(
                            omsiRoot,
                            installedAsset.RelativePath),
                        building.Center,
                        HeightMeters:
                            0,
                        Rotation:
                            ResolveBuildingRotation(
                                building)));

                installedOmsiBuildingCount++;
                continue;
            }

            var asset =
                await generator
                    .GenerateAsync(
                        omsiRoot,
                        building,
                        cancellationToken)
                    .ConfigureAwait(false);

            requests.Add(
                new MapStudioGeneratedSceneryPlacementRequest(
                    building.Id,
                    asset.SceneryObjectPath,
                    asset.WorldCenter,
                    HeightMeters:
                        0));

            proceduralBuildingCount++;
        }

        var writeResult =
            await new MapStudioGeneratedSceneryBatchWriter()
                .WriteAsync(
                    omsiRoot,
                    mapDirectory,
                    requests,
                    cancellationToken)
                .ConfigureAwait(false);

        return new MapStudioBuildingBatchReconstructionResult(
            writeResult.Placements,
            reviewIds,
            writeResult.OutsideMapIds,
            rejectedGeometryIds,
            writeResult.BackupPaths,
            visualRefinedBuildingCount,
            installedOmsiBuildingCount,
            proceduralBuildingCount);
    }

    private static OmsiAssetIndexEntry?
        SelectInstalledBuildingAsset(
            IReadOnlyList<OmsiAssetIndexEntry> assets,
            MapStudioProjectedBuildingFootprint building)
    {
        if (assets.Count == 0)
        {
            return null;
        }

        var typeTokens =
            GetBuildingTypeTokens(
                building.BuildingType);

        var ranked =
            assets
                .Select(
                    asset =>
                        (
                            Asset:
                                asset,
                            Score:
                                typeTokens.Count(
                                    token =>
                                        NormalizeAssetText(
                                            asset.RelativePath)
                                            .Contains(
                                                token,
                                                StringComparison.Ordinal))
                        ))
                .OrderByDescending(
                    item =>
                        item.Score)
                .ThenBy(
                    item =>
                        item.Asset.RelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var bestScore =
            ranked[0].Score;

        var pool =
            ranked
                .Where(
                    item =>
                        item.Score ==
                            bestScore)
                .Select(
                    item =>
                        item.Asset)
                .ToArray();

        var index =
            StableHash(
                building.Id) %
            pool.Length;

        return pool[index];
    }

    private static IReadOnlyList<string>
        GetBuildingTypeTokens(
            string? buildingType)
    {
        var normalized =
            NormalizeAssetText(
                buildingType ??
                string.Empty);

        if (
            normalized.Contains(
                "apart",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "residential",
                StringComparison.Ordinal))
        {
            return
            [
                "wohn",
                "block",
                "apart",
                "residential"
            ];
        }

        if (
            normalized.Contains(
                "house",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "detached",
                StringComparison.Ordinal))
        {
            return
            [
                "haus",
                "house",
                "wohn"
            ];
        }

        if (
            normalized.Contains(
                "industrial",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "warehouse",
                StringComparison.Ordinal))
        {
            return
            [
                "industrie",
                "halle",
                "factory",
                "warehouse"
            ];
        }

        if (
            normalized.Contains(
                "commercial",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "retail",
                StringComparison.Ordinal))
        {
            return
            [
                "laden",
                "shop",
                "commercial",
                "retail"
            ];
        }

        return Array.Empty<string>();
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
                "buildingAssetPathInvalid");
        }

        var normalized =
            relativePath
                .Replace(
                    '\\',
                    Path.DirectorySeparatorChar)
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar);

        var root =
            Path.GetFullPath(
                omsiRoot)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    normalized));

        var rootPrefix =
            root +
            Path.DirectorySeparatorChar;

        if (
            !fullPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "buildingAssetPathInvalid");
        }

        return fullPath;
    }

    private static double ResolveBuildingRotation(
        MapStudioProjectedBuildingFootprint building)
    {
        if (building.Points.Count < 2)
        {
            return 0;
        }

        var bestLengthSquared =
            double.NegativeInfinity;

        var bestRotation =
            0.0;

        for (
            var index = 0;
            index < building.Points.Count;
            index++)
        {
            var next =
                (
                    index +
                    1
                ) %
                building.Points.Count;

            var dx =
                building.Points[next].X -
                building.Points[index].X;

            var dz =
                building.Points[next].Z -
                building.Points[index].Z;

            var lengthSquared =
                dx * dx +
                dz * dz;

            if (
                lengthSquared <=
                    bestLengthSquared)
            {
                continue;
            }

            bestLengthSquared =
                lengthSquared;

            bestRotation =
                Math.Atan2(
                    dx,
                    dz) *
                180.0 /
                Math.PI;
        }

        return
            (
                bestRotation %
                360.0 +
                360.0
            ) %
            360.0;
    }

    private static int StableHash(
        string value)
    {
        unchecked
        {
            uint hash =
                2166136261;

            foreach (var character in value)
            {
                hash ^=
                    character;

                hash *=
                    16777619;
            }

            return
                (int)(
                    hash &
                    0x7fffffff);
        }
    }

    private static string NormalizeAssetText(
        string value) =>
        new(
            value
                .ToLowerInvariant()
                .Select(
                    character =>
                        char.IsLetterOrDigit(
                            character)
                            ? character
                            : ' ')
                .ToArray());

    private static bool NeedsVisualAppearanceRefinement(
        MapStudioProjectedBuildingFootprint building) =>
        building.RoofType ==
            MapStudioBuildingRoofType.Unknown ||
        string.IsNullOrWhiteSpace(
            building.FacadeMaterial) ||
        string.IsNullOrWhiteSpace(
            building.FacadeColour) ||
        string.IsNullOrWhiteSpace(
            building.RoofMaterial) ||
        string.IsNullOrWhiteSpace(
            building.RoofColour);

    private static MapStudioProjectedBuildingFootprint
        AttachVisualAttribution(
            MapStudioProjectedBuildingFootprint building,
            IReadOnlyList<MapStudioSceneEvidence> evidence)
    {
        var existing =
            building.SourceAttributions ??
            Array.Empty<string>();

        var added =
            evidence
                .Select(
                    item =>
                        string.Join(
                            " | ",
                            item.Source.ToString(),
                            item.ReferenceId ??
                                string.Empty,
                            item.Notes ??
                                string.Empty))
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .ToArray();

        if (added.Length == 0)
        {
            return building;
        }

        return building with
        {
            SourceAttributions =
                existing
                    .Concat(
                        added)
                    .Distinct(
                        StringComparer.Ordinal)
                    .ToArray()
        };
    }
}
