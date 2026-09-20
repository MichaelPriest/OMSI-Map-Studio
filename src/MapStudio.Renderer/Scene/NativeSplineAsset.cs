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
    public bool IsLoaded =>
        ErrorCode is null &&
        Definition.Exists &&
        Definition.Surfaces.Count > 0;
}
