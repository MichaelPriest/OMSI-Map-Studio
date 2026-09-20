namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiSplineLinkEdit(
    int SourceSectionOrdinal,
    string SplinePath,
    int SplineId,
    int OriginalPreviousSplineId,
    int OriginalNextSplineId,
    bool IsHeightSpline,
    int PreviousSplineId,
    int NextSplineId);
