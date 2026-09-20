namespace MapStudio.Core.Omsi.Textures;

public sealed record OmsiDdsTextureMetadata(
    int Width,
    int Height,
    string Format,
    bool AlphaOnly);
