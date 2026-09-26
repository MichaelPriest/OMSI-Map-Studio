namespace MapStudio.Renderer.Viewport;

public sealed record NativeReferenceOverlayDefinition(
    string ImagePath,
    int Width,
    int Height,
    double MetersPerPixel,
    double AnchorWorldX,
    double AnchorWorldZ,
    float Opacity,
    string Attribution);
