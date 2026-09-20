using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSceneryMeshAsset(
    string DeclaredPath,
    string FullPath,
    OmsiSceneryMeshTransform Transform,
    double? LodThreshold,
    OmsiO3dGeometry Geometry,
    IReadOnlyList<string?>
        MaterialTexturePaths,
    IReadOnlyList<string?>?
        MaterialNightTexturePaths =
            null,
    IReadOnlyList<string?>?
        MaterialLightTexturePaths =
            null,
    IReadOnlyList<int?>?
        MaterialAlphaModes =
            null);

public sealed record NativeSceneryAsset(
    string SceneryObjectPath,
    string? FullPath,
    IReadOnlyList<NativeSceneryMeshAsset> Meshes,
    OmsiSceneryTreeDefinition? Tree,
    bool UsesAbsoluteHeight,
    string? ErrorCode)
{
    public bool IsLoaded =>
        ErrorCode is null &&
        Meshes.Any(
            mesh =>
                mesh.Geometry.IsLoaded);
}
