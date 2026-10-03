namespace MapStudio.Renderer.Viewport;

public sealed record NativeAssetTechnicalSnapshot(
    IReadOnlySet<string> UsedScenery,
    IReadOnlySet<string> LoadedScenery,
    IReadOnlySet<string> TreeScenery,
    IReadOnlySet<string> ProblemScenery,
    IReadOnlySet<string> UsedSplines,
    IReadOnlySet<string> LoadedSplines,
    IReadOnlySet<string> ProblemSplines)
{
    public static NativeAssetTechnicalSnapshot Empty { get; } =
        new(
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase));
}
