namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiSceneryObjectMetadata(
    bool Exists,
    string? FriendlyName,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> MeshPaths,
    IReadOnlyList<double?> MeshLodThresholds,
    IReadOnlyList<string> CollisionMeshPaths,
    IReadOnlyList<OmsiSceneryMaterialOverride> MaterialOverrides,
    OmsiSceneryTreeDefinition? Tree)
{
    public static OmsiSceneryObjectMetadata Missing { get; } = new(
        Exists: false,
        FriendlyName: null,
        Groups: Array.Empty<string>(),
        MeshPaths: Array.Empty<string>(),
        MeshLodThresholds:
            Array.Empty<double?>(),
        CollisionMeshPaths: Array.Empty<string>(),
        MaterialOverrides:
            Array.Empty<OmsiSceneryMaterialOverride>(),
        Tree: null);
}
