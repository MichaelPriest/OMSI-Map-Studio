using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeTrafficLightProgramInfo(
    int ObjectId,
    int TileX,
    int TileY,
    string AssetPath,
    int ControllerIndex,
    int ProgramIndex,
    string ProgramName,
    double? DeclaredCycleDuration,
    IReadOnlyList<OmsiTrafficLightPhase> Phases)
{
    public string DisplayText =>
        $"{ProgramName} · objeto #{ObjectId} · {AssetPath}";

    public double EffectiveCycleDuration
    {
        get
        {
            if (
                DeclaredCycleDuration is
                    > 0)
            {
                return DeclaredCycleDuration
                    .Value;
            }

            return Phases.Sum(
                phase =>
                    Math.Max(
                        0,
                        phase.Duration));
        }
    }

    public OmsiTrafficLightPhase?
        GetPhaseAt(
            double seconds)
    {
        if (Phases.Count == 0)
        {
            return null;
        }

        var duration =
            EffectiveCycleDuration;

        if (duration <= 0)
        {
            return Phases[0];
        }

        var time =
            seconds %
            duration;

        if (time < 0)
        {
            time += duration;
        }

        var cursor = 0.0;

        foreach (
            var phase in
                Phases)
        {
            cursor +=
                Math.Max(
                    0,
                    phase.Duration);

            if (time < cursor)
            {
                return phase;
            }
        }

        return Phases[^1];
    }

    public static string DescribeSignalCode(
        int signalCode) =>
        signalCode switch
        {
            >= 0 and <= 2 =>
                "Vermelho",
            >= 3 and <= 5 =>
                "Vermelho + amarelo",
            >= 6 and <= 8 =>
                "Verde",
            >= 9 and <= 11 =>
                "Amarelo",
            _ =>
                "Apagado/Especial"
        };
}
