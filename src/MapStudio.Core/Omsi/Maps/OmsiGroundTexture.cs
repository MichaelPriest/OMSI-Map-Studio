namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiGroundTexture(
    string MainTexturePath,
    string DetailTexturePath,
    int ResolutionCode,
    double MainTextureRepeating,
    double DetailTextureRepeating)
{
    public int? MaskResolution =>
        ResolutionCode is > 0 and <= 12
            ? 1 << ResolutionCode
            : null;
}
