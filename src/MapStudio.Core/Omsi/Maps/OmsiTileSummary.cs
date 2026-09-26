namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileSummary(
    bool Exists,
    int ObjectCount,
    int SplineCount,
    int SplineAttachmentCount,
    bool TerrainMarkerPresent = false,
    bool TerrainFileExists = false,
    long TerrainFileSize = 0,
    bool WaterMarkerPresent = false,
    bool WaterFileExists = false,
    long WaterFileSize = 0)
{
    public static OmsiTileSummary Missing { get; } = new(
        Exists: false,
        ObjectCount: 0,
        SplineCount: 0,
        SplineAttachmentCount: 0);
}
