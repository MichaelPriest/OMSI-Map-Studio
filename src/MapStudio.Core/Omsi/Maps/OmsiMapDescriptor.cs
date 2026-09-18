namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiMapDescriptor(
    string DirectoryName,
    string DisplayName,
    string DirectoryPath,
    string GlobalConfigPath,
    IReadOnlyList<OmsiTileReference> Tiles);
