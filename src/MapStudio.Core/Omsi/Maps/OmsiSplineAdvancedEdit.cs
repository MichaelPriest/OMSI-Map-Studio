namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiSplineAdvancedEdit(
    int SourceSectionOrdinal,
    string SplinePath,
    int SplineId,
    int PreviousSplineId,
    int NextSplineId,
    bool IsHeightSpline,
    double CantStart,
    double CantEnd,
    bool IsMirrored);
