namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiSplineLinkEdit(
    int SourceSectionOrdinal,
    string SplinePath,
    int SplineId,
    int PreviousSplineId,
    int NextSplineId,
    bool IsHeightSpline,
    int NewPreviousSplineId,
    int NewNextSplineId);
