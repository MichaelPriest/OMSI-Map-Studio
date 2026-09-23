using System.Numerics;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeSplineSplitRequest(
    NativeSelectionInfo Selection,
    double SplitDistance,
    double FirstLength,
    double FirstGradientStart,
    double FirstGradientEnd,
    OmsiTileReference SecondTile,
    double SecondX,
    double SecondY,
    double SecondZ,
    double SecondRotation,
    double SecondLength,
    double SecondRadius,
    double SecondGradientStart,
    double SecondGradientEnd,
    Vector3 SplitWorld,
    Vector3 EndWorld);
