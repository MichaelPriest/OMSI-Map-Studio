namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileSummary(
    bool Exists,
    int ObjectCount,
    int SplineCount,
    int SplineAttachmentCount)
{
    public static OmsiTileSummary Missing { get; } = new(
        Exists: false,
        ObjectCount: 0,
        SplineCount: 0,
        SplineAttachmentCount: 0);
}
