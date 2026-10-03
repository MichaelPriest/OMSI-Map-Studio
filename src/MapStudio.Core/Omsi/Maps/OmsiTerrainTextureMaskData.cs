namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTerrainTextureMaskData(
    int Width,
    int Height,
    byte[] AlphaPixels)
{
    public int PixelCount =>
        checked(
            Width *
            Height);
}

public sealed record OmsiTerrainTexturePaintResult(
    OmsiTerrainTextureMaskData Mask,
    int ChangedPixels);
