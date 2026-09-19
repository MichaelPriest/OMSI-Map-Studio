namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTerrainTextureMask(
    int LayerIndex,
    string FileName,
    long FileSize);
