using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSceneryMeshAsset(
    string DeclaredPath,
    OmsiSceneryMeshTransform Transform,
    double? LodThreshold,
    OmsiO3dGeometry Geometry);

public sealed record NativeSceneryAsset(
    string SceneryObjectPath,
    string? FullPath,
    IReadOnlyList<NativeSceneryMeshAsset> Meshes,
    OmsiSceneryTreeDefinition? Tree,
    string? ErrorCode)
{
    public bool IsLoaded =>
        ErrorCode is null &&
        Meshes.Any(
            mesh =>
                mesh.Geometry.IsLoaded);
}
