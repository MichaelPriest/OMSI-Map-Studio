namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiObjectTransformEdit(
    int SourceSectionOrdinal,
    string SceneryObjectPath,
    int ObjectId,
    double X,
    double Y,
    double Z,
    double Rotation,
    double Pitch,
    double Bank);
