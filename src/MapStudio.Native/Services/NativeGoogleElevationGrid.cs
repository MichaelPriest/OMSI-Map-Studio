namespace MapStudio.Native.Services;

public sealed record NativeGoogleElevationGrid(
    int TileX,
    int TileY,
    int Rows,
    int Columns,
    IReadOnlyList<double> Elevations,
    double MinimumElevation,
    double MaximumElevation,
    double AnchorLatitude,
    double AnchorLongitude);
