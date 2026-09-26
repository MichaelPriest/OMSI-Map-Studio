namespace MapStudio.Core.Omsi.Splines;

public sealed record OmsiSplinePathDefinition(
    int Type,
    double X,
    double Z,
    double Width,
    int Direction);
