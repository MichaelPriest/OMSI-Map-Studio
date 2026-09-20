namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiSplineTransformEdit(
    int SourceSectionOrdinal,
    string SplinePath,
    int SplineId,
    int PreviousSplineId,
    int NextSplineId,
    bool IsHeightSpline,
    double X,
    double Z,
    double Y,
    double Rotation,
    double Length,
    double Radius,
    double GradientStart,
    double GradientEnd);
