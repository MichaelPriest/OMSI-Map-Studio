namespace MapStudio.Core.Omsi.Splines;

public sealed record OmsiSplineSurface(
    int TextureIndex,
    string? TextureName,
    OmsiSplineProfilePoint From,
    OmsiSplineProfilePoint To);
