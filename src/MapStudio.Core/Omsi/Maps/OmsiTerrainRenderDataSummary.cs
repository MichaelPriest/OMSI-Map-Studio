namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTerrainRenderDataSummary(
    bool Exists,
    bool IsValid,
    long FileSize,
    uint VertexCount,
    uint TriangleCount,
    ushort MaterialCount,
    bool HasTransform,
    string? ErrorCode)
{
    public static OmsiTerrainRenderDataSummary Missing { get; } =
        new(
            Exists: false,
            IsValid: false,
            FileSize: 0,
            VertexCount: 0,
            TriangleCount: 0,
            MaterialCount: 0,
            HasTransform: false,
            ErrorCode: null);
}
