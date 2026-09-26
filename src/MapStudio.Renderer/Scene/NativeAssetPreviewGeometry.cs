using System.Numerics;

namespace MapStudio.Renderer.Scene;

public sealed record NativeAssetPreviewGeometry(
    NativeMapVertex[] Vertices,
    Vector3 Minimum,
    Vector3 Maximum,
    int SourceMeshCount,
    string? ErrorCode)
{
    public bool IsRenderable =>
        ErrorCode is null &&
        Vertices.Length >= 3;

    public int TriangleCount =>
        Vertices.Length / 3;

    public static NativeAssetPreviewGeometry Error(
        string errorCode) =>
        new(
            Array.Empty<NativeMapVertex>(),
            Vector3.Zero,
            Vector3.Zero,
            0,
            errorCode);
}
