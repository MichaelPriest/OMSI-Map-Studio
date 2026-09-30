using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Buildings;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioBuildingBatchReconstructionResult(
    IReadOnlyList<MapStudioGeneratedSceneryPlacement> Placements,
    IReadOnlyList<string> ReviewBuildingIds,
    IReadOnlyList<string> OutsideMapBuildingIds,
    IReadOnlyList<string> RejectedGeometryBuildingIds,
    IReadOnlyList<string> BackupPaths)
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
                visualEvidenceProvider = null)
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

            if (initialReviewIds.Count > 0)
            {
                var targets =
                    projected
                        .Where(
                            building =>
                                initialReviewIds.Contains(
                                    building.Id))
                        .ToArray();

                var visualEvidence =
                    await visualEvidenceProvider
                        .AnalyzeAsync(
                            targets,
                            anchor,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (visualEvidence.Count > 0)
                {
                    var refiner =
                        new MapStudioStreetLevelBuildingRefiner();

                    projected =
                        projected
                            .Select(
                                building =>
                                    visualEvidence.TryGetValue(
                                        building.Id,
                                        out var visual)
                                        ? refiner.Refine(
                                            building,
                                            visual.Analysis)
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
                Array.Empty<string>());
        }

        var generator =
            new MapStudioFootprintBuildingAssetGenerator();

        var requests =
            new List<MapStudioGeneratedSceneryPlacementRequest>();

        foreach (
            var building in
                projected.Where(
                    building =>
                        automaticIds.Contains(
                            building.Id)))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

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
                        elevation?.SampleRelativeHeightOrDefault(
                            anchor,
                            asset.WorldCenter) ?? 0));
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
            writeResult.BackupPaths);
    }
}
