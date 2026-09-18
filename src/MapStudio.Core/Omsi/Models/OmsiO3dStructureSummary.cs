namespace MapStudio.Core.Omsi.Models;

public sealed record OmsiO3dStructureSummary(
    bool IsParsed,
    uint VertexCount,
    uint TriangleCount,
    ushort MaterialCount,
    uint BoneCount,
    bool HasTransform,
    string? ErrorCode)
{
    public static OmsiO3dStructureSummary Invalid(
        string errorCode) => new(
            IsParsed: false,
            VertexCount: 0,
            TriangleCount: 0,
            MaterialCount: 0,
            BoneCount: 0,
            HasTransform: false,
            ErrorCode: errorCode);
}
