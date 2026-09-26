using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSplineAsset(
    string DeclaredPath,
    string? FullPath,
    OmsiSplineDefinition Definition,
    IReadOnlyList<string?>
        TexturePaths,
    string? ErrorCode)
{
    public bool CanPlace =>
        Definition.Exists &&
        !string.IsNullOrWhiteSpace(
            FullPath);

    public bool IsLoaded =>
        ErrorCode is null &&
        CanPlace &&
        Definition.Surfaces.Count > 0;
}
