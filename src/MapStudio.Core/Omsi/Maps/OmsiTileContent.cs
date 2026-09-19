namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileContent(
    OmsiTileSummary Summary,
    IReadOnlyList<OmsiPlacedObject> Objects,
    IReadOnlyList<OmsiPlacedSpline> Splines,
    OmsiTerrainGrid? Terrain = null,
    OmsiTerrainRenderDataSummary? TerrainRenderData = null)
{
    public static OmsiTileContent Missing { get; } = new(
        OmsiTileSummary.Missing,
        Array.Empty<OmsiPlacedObject>(),
        Array.Empty<OmsiPlacedSpline>());
}
