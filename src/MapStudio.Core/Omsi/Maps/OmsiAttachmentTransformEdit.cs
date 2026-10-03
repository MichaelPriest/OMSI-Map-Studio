namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiAttachmentTransformEdit(
    int SourceSectionOrdinal,
    OmsiAttachmentKind Kind,
    int AttachmentId,
    string AssetPath,
    IReadOnlyList<string> ExpectedRawValues,
    double? X,
    double? Z,
    double? Y,
    double Rotation,
    double Pitch,
    double Bank,
    double? Interval,
    double? Distance);
