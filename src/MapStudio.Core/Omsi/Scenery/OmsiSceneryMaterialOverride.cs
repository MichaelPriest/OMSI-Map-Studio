namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiSceneryMaterialOverride(
    int MeshOrdinal,
    string TextureName,
    int MaterialIndex,
    int? AlphaMode,
    bool NoZWrite,
    bool NoZCheck,
    string? BumpMapTextureName,
    double? BumpMapStrength);
