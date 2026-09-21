namespace MapStudio.Native.Services;

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
    public double WidthMeters =>
        Width *
        MetersPerPixel;

    public double HeightMeters =>
        Height *
        MetersPerPixel;
}
