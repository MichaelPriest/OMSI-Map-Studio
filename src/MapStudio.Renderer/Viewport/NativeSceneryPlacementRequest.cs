using System.Numerics;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeSceneryPlacementRequest(
    OmsiTileReference Tile,
    string SceneryObjectPath,
    double X,
    double Y,
    double Z,
    double Rotation,
    double Pitch,
    double Bank,
    Vector3 WorldPoint,
    bool UsesAbsoluteHeight);
