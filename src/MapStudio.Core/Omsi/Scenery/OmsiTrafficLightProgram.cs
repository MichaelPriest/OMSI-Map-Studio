namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiTrafficLightProgram(
    string Name,
    IReadOnlyList<OmsiTrafficLightPhase> Phases)
{
    public double TotalDuration =>
        Phases.Sum(
            phase =>
                phase.Duration);
}
