namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiMapDescriptor(
    string DirectoryName,
    string DisplayName,
    string DirectoryPath,
    string GlobalConfigPath,
    bool UsesWorldCoordinates,
    IReadOnlyList<OmsiTileReference> Tiles,
    IReadOnlyList<OmsiGroundTexture> GroundTextures);
