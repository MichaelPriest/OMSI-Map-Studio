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
    public string KindLabel =>
        Kind ==
        PickingKind.Object
            ? "OBJETO"
            : "SPLINE";

    public string AssetName =>
        Path.GetFileName(
            AssetPath.Replace(
                '\\',
                Path.DirectorySeparatorChar));

    public string TileText =>
        $"Tile {TileX},{TileY}";

    public string DetailText =>
        $"{KindLabel} #{EntityId} · {TileText}";

    public string DisplayText =>
        $"{(Kind == PickingKind.Object ? "Objeto" : "Spline")} #{EntityId} · " +
        AssetName;
}
