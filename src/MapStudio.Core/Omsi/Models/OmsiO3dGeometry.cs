namespace MapStudio.Core.Omsi.Models;

public sealed record OmsiO3dGeometry(
    bool IsLoaded,
    string? ErrorCode,
    float[] Positions,
    float[] Normals,
    float[] Uvs,
    uint[] Indices,
    ushort[] TriangleMaterialIndices,
    IReadOnlyList<OmsiO3dMaterial> Materials)
{
    public static OmsiO3dGeometry Error(
        string errorCode) => new(
            IsLoaded: false,
            ErrorCode: errorCode,
            Positions: Array.Empty<float>(),
            Normals: Array.Empty<float>(),
            Uvs: Array.Empty<float>(),
            Indices: Array.Empty<uint>(),
            TriangleMaterialIndices: Array.Empty<ushort>(),
            Materials: Array.Empty<OmsiO3dMaterial>());
}
