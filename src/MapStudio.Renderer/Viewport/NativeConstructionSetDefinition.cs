namespace MapStudio.Renderer.Viewport;

public enum NativeConstructionSetSide
{
    Left = 0,
    Right = 1,
    Both = 2
}

public sealed record NativeConstructionSetCompanion(
    string Id,
    string SceneryObjectPath,
    double Spacing,
    double LateralOffset,
    NativeConstructionSetSide Side,
    double RotationOffset);

public sealed record NativeConstructionSetDefinition(
    string Id,
    string Name,
    string? SplinePath,
    IReadOnlyList<NativeConstructionSetCompanion> Companions);

public sealed record NativeConstructionSetPlacementGroup(
    string SceneryObjectPath,
    IReadOnlyList<NativeSceneryPatternPlacement> Placements);
