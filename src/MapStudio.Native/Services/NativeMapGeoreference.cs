namespace MapStudio.Native.Services;

public sealed record NativeMapGeoreference(
    double Latitude,
    double Longitude,
    int AnchorTileX,
    int AnchorTileY,
    double AnchorX,
    double AnchorY,
    int Zoom,
    string MapType,
    string Provider);
