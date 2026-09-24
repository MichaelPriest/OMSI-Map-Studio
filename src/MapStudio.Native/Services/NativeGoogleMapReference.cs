namespace MapStudio.Native.Services;

public sealed record NativeMapReferenceTile(
    string ImagePath,
    int Width,
    int Height,
    double MetersPerPixel,
    double AnchorWorldX,
    double AnchorWorldZ,
    int TileX,
    int TileY)
{
    public double WidthMeters =>
        Width *
        MetersPerPixel;

    public double HeightMeters =>
        Height *
        MetersPerPixel;
}

public sealed record NativeGoogleMapReference(
    string ImagePath,
    int Width,
    int Height,
    double MetersPerPixel,
    double AnchorWorldX,
    double AnchorWorldZ,
    double Latitude,
    double Longitude,
    int Zoom,
    string MapType,
    string Attribution)
{
    public IReadOnlyList<NativeMapReferenceTile>
        Tiles
    {
        get;
        init;
    } =
        Array.Empty<
            NativeMapReferenceTile>();

    public int TileCount =>
        Tiles.Count == 0
            ? 1
            : Tiles.Count;

    public bool IsTiledMosaic =>
        Tiles.Count > 0;

    public double WidthMeters =>
        Width *
        MetersPerPixel;

    public double HeightMeters =>
        Height *
        MetersPerPixel;
}
