using System.Numerics;

namespace MapStudio.Renderer.Viewport;

public enum NativeSplineEndpointKind
{
    Start = 0,
    End = 1
}

public sealed record NativeSplineEndpointSnap(
    int SplineId,
    NativeSplineEndpointKind Endpoint,
    Vector3 WorldPoint,
    double Distance);
