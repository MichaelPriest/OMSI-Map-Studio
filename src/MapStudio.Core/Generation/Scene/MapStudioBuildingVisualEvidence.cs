using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Scene;

public sealed record MapStudioBuildingVisualEvidence(
    string BuildingId,
    MapStudioBuildingReferenceAnalysis Analysis,
    IReadOnlyList<MapStudioSceneEvidence> Evidence);

public sealed record MapStudioBuildingVisualEvidenceProgress(
    int CompletedBuildings,
    int TotalBuildings,
    string Message);

public interface IMapStudioBuildingVisualEvidenceProvider
{
    Task<IReadOnlyDictionary<
        string,
        MapStudioBuildingVisualEvidence>>
        AnalyzeAsync(
            IReadOnlyList<
                MapStudioProjectedBuildingFootprint>
                buildings,
            MapStudioGeographicAnchor anchor,
            IProgress<MapStudioBuildingVisualEvidenceProgress>?
                progress = null,
            CancellationToken cancellationToken =
                default);
}
