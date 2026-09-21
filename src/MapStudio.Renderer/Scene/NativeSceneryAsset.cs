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
            null,
    IReadOnlyList<bool>?
        MaterialNoZWriteFlags =
            null,
    IReadOnlyList<bool>?
        MaterialNoZCheckFlags =
            null,
    IReadOnlyList<string?>?
        MaterialTransMapTexturePaths =
            null,
    IReadOnlyList<string?>?
        MaterialBumpTexturePaths =
            null,
    IReadOnlyList<double?>?
        MaterialBumpStrengths =
            null,
    IReadOnlyList<string?>?
        MaterialEnvironmentTexturePaths =
            null,
    IReadOnlyList<double?>?
        MaterialEnvironmentStrengths =
            null);

public sealed record NativeSceneryAsset(
    string SceneryObjectPath,
    string? FullPath,
    IReadOnlyList<NativeSceneryMeshAsset> Meshes,
    OmsiSceneryTreeDefinition? Tree,
    bool UsesAbsoluteHeight,
    string? ErrorCode,
    string? TreeTexturePath = null,
    string? RenderType = null)
{
    public bool IsLoaded =>
        ErrorCode is null &&
        (
            Meshes.Any(
                mesh =>
                    mesh.Geometry.IsLoaded) ||
            (
                Tree is not null &&
                !string.IsNullOrWhiteSpace(
                    TreeTexturePath)
            )
        );
}
