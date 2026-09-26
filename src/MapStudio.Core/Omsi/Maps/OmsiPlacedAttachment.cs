namespace MapStudio.Core.Omsi.Maps;

public enum OmsiAttachmentKind
{
    ObjectAttachment,
    SplineAttachment,
    SplineAttachmentRepeater
}

public sealed record OmsiPlacedAttachment(
    OmsiAttachmentKind Kind,
    string HeaderValue,
    string AssetPath,
    int AttachmentId,
    int? AttachedToObjectId,
    int? AttachPointIndex,
    double? X,
    double? Z,
    double? Y,
    double Rotation,
    double Pitch,
    double Bank,
    double? Interval,
    double? Distance,
    int? LabelsCount,
    IReadOnlyList<string> RawValues)
{
    public int SourceSectionOrdinal
    {
        get;
        init;
    } = -1;

    public string? VariableParentValue
    {
        get;
        init;
    }
}
