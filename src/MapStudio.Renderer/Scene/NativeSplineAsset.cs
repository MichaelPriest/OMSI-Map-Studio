using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSplineAsset(
    string DeclaredPath,
    string? FullPath,
    OmsiSplineDefinition Definition,
    string? ErrorCode)
{
    public bool IsLoaded =>
        ErrorCode is null &&
        Definition.Exists &&
        Definition.Surfaces.Count > 0;
}
