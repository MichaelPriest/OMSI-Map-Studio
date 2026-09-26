using System.Numerics;

namespace MapStudio.Renderer.Viewport;

public enum NativeRoadElevationMode
{
    FollowTerrain,
    Elevate,
    Level,
    Lower
}

public sealed record NativeSplinePlacementControlState(
    Vector3 Start,
    Vector3 End,
    double CurveOffset,
    bool AwaitingConfirmation,
    double Length,
    double StartElevation,
    double EndElevation,
    double Gradient,
    double Radius,
    NativeRoadElevationMode ElevationMode);
