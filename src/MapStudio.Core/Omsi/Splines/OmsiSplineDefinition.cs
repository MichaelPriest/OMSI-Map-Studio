namespace MapStudio.Core.Omsi.Splines;

public sealed record OmsiSplineDefinition(
    bool Exists,
    IReadOnlyList<string> Textures,
    IReadOnlyList<OmsiSplineSurface> Surfaces)
{
    public static OmsiSplineDefinition Missing { get; } = new(
        Exists: false,
        Textures: Array.Empty<string>(),
        Surfaces: Array.Empty<OmsiSplineSurface>());
}
