namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiTrafficLightController(
    double? CycleDuration,
    IReadOnlyList<OmsiTrafficLightProgram> Programs);
