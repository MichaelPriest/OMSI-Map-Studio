using System.Numerics;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeSplinePlacementRequest(
    OmsiTileReference Tile,
    string SplinePath,
    double X,
    double Y,
    double Z,
    double Rotation,
    double Length,
    double Radius,
    double GradientStart,
    double GradientEnd,
    bool IsCurved,
    Vector3 StartWorld,
    Vector3 EndWorld);
