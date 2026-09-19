namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiSceneryMaterialOverride(
    int MeshOrdinal,
    string TextureName,
    int MaterialIndex,
    int? AlphaMode,
    bool NoZWrite,
    bool NoZCheck,
    string? BumpMapTextureName,
    double? BumpMapStrength,
    string? NightMapTextureName,
    string? EnvironmentMapTextureName,
    double? EnvironmentMapStrength,
    string? TransMapSource,
    string? LightMapTextureName,
    IReadOnlyList<string> UnsupportedCommands);
