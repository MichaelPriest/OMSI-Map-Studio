namespace MapStudio.Core.Omsi.Splines;

public sealed record OmsiSplineDefinition(
    bool Exists,
    IReadOnlyList<string> Textures,
    IReadOnlyList<OmsiSplineSurface> Surfaces)
{
    public IReadOnlyList<OmsiSplinePathDefinition>
        Paths { get; init; } =
            Array.Empty<OmsiSplinePathDefinition>();

    public static OmsiSplineDefinition Missing { get; } = new(
        Exists: false,
        Textures: Array.Empty<string>(),
        Surfaces: Array.Empty<OmsiSplineSurface>());
}
