namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiPlacedObject(
    string HeaderValue,
    string SceneryObjectPath,
    int ObjectId,
    double X,
    double Y,
    double Z,
    double Rotation,
    double Pitch,
    double Bank,
    IReadOnlyList<string> ExtraValues)
{
    public int SourceSectionOrdinal { get; init; } = -1;
}
