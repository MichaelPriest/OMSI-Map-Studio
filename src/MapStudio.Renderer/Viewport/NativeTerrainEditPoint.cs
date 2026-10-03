using System.Numerics;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeTerrainEditPoint(
    OmsiTileReference Tile,
    double LocalX,
    double LocalY,
    double Height,
    Vector3 WorldPoint);
