namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiPlacedSpline(
    string HeaderValue,
    string SplinePath,
    int SplineId,
    int PreviousSplineId,
    int NextSplineId,
    double X,
    double Z,
    double Y,
    double Rotation,
    double Length,
    double Radius,
    double GradientStart,
    double GradientEnd,
    bool IsHeightSpline,
    IReadOnlyList<string> ExtraValues);
