using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSceneTile(
    OmsiTileReference Reference,
    OmsiTileContent Content);

public sealed record NativeObjectEntity(
    PickingId PickingId,
    OmsiTileReference Tile,
    OmsiPlacedObject Object,
    float WorldX,
    float WorldY,
    float WorldZ);

public sealed record NativeSplineEntity(
    PickingId PickingId,
    OmsiTileReference Tile,
    OmsiPlacedSpline Spline,
    float WorldX,
    float WorldY,
    float WorldZ);

public sealed record NativeTerrainEntity(
    OmsiTileReference Tile,
    OmsiTerrainGrid Terrain);

public sealed record NativeSceneSnapshot(
    IReadOnlyList<NativeSceneTile> Tiles,
    IReadOnlyList<NativeObjectEntity> Objects,
    IReadOnlyList<NativeSplineEntity> Splines,
    IReadOnlyList<NativeTerrainEntity> Terrain)
{
    public int SelectableCount =>
        Objects.Count +
        Splines.Count;
}
