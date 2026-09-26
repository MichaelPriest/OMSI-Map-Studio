using System.Numerics;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeJunctionSuggestion(
    string Key,
    int TileX,
    int TileY,
    double X,
    double Y,
    double Rotation,
    int SplineA,
    int SplineB,
    Vector3 WorldPoint)
{
    public string DisplayText =>
        $"Splines #{SplineA} / #{SplineB} · tile {TileX},{TileY} · {X:F1},{Y:F1}";
}
