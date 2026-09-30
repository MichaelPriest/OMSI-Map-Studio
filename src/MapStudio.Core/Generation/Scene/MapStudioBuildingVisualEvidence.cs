using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Scene;

public sealed record MapStudioBuildingVisualEvidence(
    string BuildingId,
    MapStudioBuildingReferenceAnalysis Analysis,
    IReadOnlyList<MapStudioSceneEvidence> Evidence);

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
            CancellationToken cancellationToken =
                default);
}
