namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiTrafficLightPhase(
    int SignalCode,
    double Duration);
