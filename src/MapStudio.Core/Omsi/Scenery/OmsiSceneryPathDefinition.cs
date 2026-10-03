namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiSceneryPathDefinition(
    double X,
    double Y,
    double Z,
    double Rotation,
    double Radius,
    double Length,
    double GradientStart,
    double GradientEnd,
    int Type,
    double Width,
    int Direction,
    int BlinkerCode,
    int? TrafficLightIndex,
    int? SwitchDirection,
    bool CrossingProblem);
