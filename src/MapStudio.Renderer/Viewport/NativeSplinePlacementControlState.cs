using System.Numerics;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeSplinePlacementControlState(
    Vector3 Start,
    Vector3 End,
    double CurveOffset,
    bool AwaitingConfirmation);
