namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiSceneryObjectMetadata(
    bool Exists,
    string? FriendlyName,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> MeshPaths,
    IReadOnlyList<string> CollisionMeshPaths,
    IReadOnlyList<OmsiSceneryMaterialOverride> MaterialOverrides)
{
    public static OmsiSceneryObjectMetadata Missing { get; } = new(
        Exists: false,
        FriendlyName: null,
        Groups: Array.Empty<string>(),
        MeshPaths: Array.Empty<string>(),
        CollisionMeshPaths: Array.Empty<string>(),
        MaterialOverrides:
            Array.Empty<OmsiSceneryMaterialOverride>());
}
