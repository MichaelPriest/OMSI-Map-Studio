using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeSelectionInfo(
    PickingKind Kind,
    int EntityId,
    int TileX,
    int TileY,
    string AssetPath,
    double X,
    double Y,
    double Z,
    double Rotation,
    double? Pitch,
    double? Bank,
    double? Length,
    double? Radius,
    double? GradientStart,
    double? GradientEnd);
