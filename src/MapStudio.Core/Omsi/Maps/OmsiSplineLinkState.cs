namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiSplineLinkState(
    int SplineId,
    int PreviousSplineId,
    int NextSplineId);
