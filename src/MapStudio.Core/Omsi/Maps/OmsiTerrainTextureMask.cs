namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTerrainTextureMask(
    int LayerIndex,
    string FileName,
    long FileSize,
    bool IsValid,
    int Width,
    int Height,
    bool HasPixelStatistics,
    double Coverage,
    byte MinimumAlpha,
    byte MaximumAlpha,
    string? ErrorCode);
