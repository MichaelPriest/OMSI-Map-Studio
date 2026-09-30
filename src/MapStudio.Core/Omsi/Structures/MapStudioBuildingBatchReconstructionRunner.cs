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
            MapStudioGeoreferencedElevationSurface? elevation = null)
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

        var candidates =
            new MapStudioOsmBuildingReconstructionAdapter()
                .BuildCandidates(
                    projected);

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
