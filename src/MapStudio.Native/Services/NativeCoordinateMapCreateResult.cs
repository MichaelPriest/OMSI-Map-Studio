namespace MapStudio.Native.Services;

public sealed record NativeCoordinateMapCreateResult(
    NativeMapSnapshot Snapshot,
    string DirectoryPath,
    double Latitude,
    double Longitude);
