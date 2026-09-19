namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiNewPlacedObject(
    string HeaderValue,
    string SceneryObjectPath,
    int ObjectId,
    double X,
    double Y,
    double Z,
    double Rotation,
    double Pitch,
    double Bank,
    IReadOnlyList<string> ExtraValues);
