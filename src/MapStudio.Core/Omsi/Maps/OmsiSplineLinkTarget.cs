namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiSplineLinkTarget(
    int PreviousSplineId,
    int NextSplineId);
