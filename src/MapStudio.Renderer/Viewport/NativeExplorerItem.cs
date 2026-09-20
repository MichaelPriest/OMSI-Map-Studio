using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeExplorerItem(
    PickingId PickingId,
    PickingKind Kind,
    int EntityId,
    int TileX,
    int TileY,
    string AssetPath)
{
    public string DisplayText =>
        $"{(Kind == PickingKind.Object ? "Objeto" : "Spline")} #{EntityId} · " +
        Path.GetFileName(
            AssetPath.Replace(
                '\\',
                Path.DirectorySeparatorChar));
}
