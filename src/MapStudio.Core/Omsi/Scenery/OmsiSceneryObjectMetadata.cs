namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiSceneryObjectMetadata(
    bool Exists,
    string? FriendlyName,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> MeshPaths,
    IReadOnlyList<double?> MeshLodThresholds,
    IReadOnlyList<OmsiSceneryMeshTransform> MeshTransforms,
    IReadOnlyList<string> CollisionMeshPaths,
    IReadOnlyList<OmsiSceneryMaterialOverride> MaterialOverrides,
    bool UsesAbsoluteHeight,
    OmsiSceneryTreeDefinition? Tree,
    string? RenderType)
{
    public IReadOnlyList<OmsiTrafficLightController>
        TrafficLightControllers { get; init; } =
            Array.Empty<OmsiTrafficLightController>();

    public IReadOnlyList<OmsiSceneryLightPoint>
        LightPoints { get; init; } =
            Array.Empty<OmsiSceneryLightPoint>();

    public bool IsTrafficLightObject { get; init; }

    public bool UsesLightMapMapping { get; init; }

    public static OmsiSceneryObjectMetadata Missing { get; } = new(
        Exists: false,
        FriendlyName: null,
        Groups: Array.Empty<string>(),
        MeshPaths: Array.Empty<string>(),
        MeshLodThresholds:
            Array.Empty<double?>(),
        MeshTransforms:
            Array.Empty<OmsiSceneryMeshTransform>(),
        CollisionMeshPaths: Array.Empty<string>(),
        MaterialOverrides:
            Array.Empty<OmsiSceneryMaterialOverride>(),
        UsesAbsoluteHeight: false,
        Tree: null,
        RenderType: null);
}
