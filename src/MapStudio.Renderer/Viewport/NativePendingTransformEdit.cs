using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Renderer.Viewport;

public sealed record NativePendingTransformEdit(
    OmsiTileReference Tile,
    OmsiObjectTransformEdit? ObjectEdit,
    OmsiSplineTransformEdit? SplineEdit)
{
    public bool IsObject =>
        ObjectEdit is not null;

    public bool IsSpline =>
        SplineEdit is not null;
}
