namespace MapStudio.Core.Omsi.Splines;

public sealed record OmsiSplineSurface(
    int TextureIndex,
    string? TextureName,
    int AlphaMode,
    OmsiSplineProfilePoint From,
    OmsiSplineProfilePoint To);
