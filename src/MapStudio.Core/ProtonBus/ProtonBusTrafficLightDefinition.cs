using System.Globalization;
using System.Numerics;
using System.Text;

namespace MapStudio.Core.ProtonBus;

public enum ProtonBusTrafficLightColor
{
    Red,
    Yellow,
    Green
}

public sealed record ProtonBusTrafficLightPathState(
    bool Red = false,
    bool Yellow = false,
    bool Green = false,
    bool Trigger = false);

public sealed record ProtonBusTrafficLightTick(
    int Repeat,
    IReadOnlyDictionary<int, ProtonBusTrafficLightPathState> Paths);

public sealed record ProtonBusTrafficLightRealLight(
    Vector4 Color,
    double Intensity = 1.0,
    double Range = 10.0);

public sealed record ProtonBusTrafficLightDefinition(
    string Prefix,
    int PathCount,
    double TickInterval,
    double TriggerRadius,
    bool UseRealLights,
    IReadOnlyList<ProtonBusTrafficLightTick> Ticks,
    bool RandomTimestampAtStart = true,
    int FirstTickToRun = 1,
    ProtonBusTrafficLightRealLight? GreenLight = null,
    ProtonBusTrafficLightRealLight? RedLight = null,
    ProtonBusTrafficLightRealLight? YellowLight = null)
{
    public string SuggestedFileName => Prefix + ".txt";

    public string GetLightObjectName(
        int path,
        ProtonBusTrafficLightColor color)
    {
        ValidatePath(path);

        var colorName =
            color.ToString().ToLowerInvariant();

        return
            $"_{Prefix}_path{path}_{colorName}_additive_";
    }

    public string GetTriggerObjectName(int path)
    {
        ValidatePath(path);
        return $"_{Prefix}_path{path}_trigger_";
    }

    private void ValidatePath(int path)
    {
        if (path < 1 || path > PathCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(path),
                $"Traffic-light path must be between 1 and {PathCount}.");
        }
    }
}

public static class ProtonBusTrafficLightDefinitionWriter
{
    public static string Serialize(ProtonBusTrafficLightDefinition definition)
    {
        Validate(definition);

        var builder = new StringBuilder();

        builder.AppendLine("[trafficlight]");
        builder.Append("prefix=").AppendLine(definition.Prefix);
        builder.Append("howManyPaths=").AppendLine(
            definition.PathCount.ToString(CultureInfo.InvariantCulture));
        builder.Append("howManyTicks=").AppendLine(
            definition.Ticks.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append("tickInterval=").AppendLine(FormatNumber(definition.TickInterval));
        builder.Append("triggerRadius=").AppendLine(FormatNumber(definition.TriggerRadius));
        builder.Append("useRealLights=").AppendLine(ToBoolean(definition.UseRealLights));
        builder.Append("randomTimestampAtStart=").AppendLine(
            ToBoolean(definition.RandomTimestampAtStart));
        builder.Append("firstTickToRun=").AppendLine(
            definition.FirstTickToRun.ToString(CultureInfo.InvariantCulture));

        WriteRealLight(builder, "green_light", definition.GreenLight);
        WriteRealLight(builder, "red_light", definition.RedLight);
        WriteRealLight(builder, "yellow_light", definition.YellowLight);

        for (var tickIndex = 0; tickIndex < definition.Ticks.Count; tickIndex++)
        {
            var tick = definition.Ticks[tickIndex];

            builder.AppendLine();
            builder.Append('[')
                .Append("tick")
                .Append((tickIndex + 1).ToString(CultureInfo.InvariantCulture))
                .AppendLine("]");

            builder.Append("repeat=").AppendLine(
                tick.Repeat.ToString(CultureInfo.InvariantCulture));

            for (var path = 1; path <= definition.PathCount; path++)
            {
                var state =
                    tick.Paths.TryGetValue(path, out var configured)
                        ? configured
                        : new ProtonBusTrafficLightPathState();

                builder.Append($"path{path}_red=").AppendLine(ToBoolean(state.Red));
                builder.Append($"path{path}_yellow=").AppendLine(ToBoolean(state.Yellow));
                builder.Append($"path{path}_green=").AppendLine(ToBoolean(state.Green));
                builder.Append($"path{path}_trigger=").AppendLine(ToBoolean(state.Trigger));
            }
        }

        return builder.ToString();
    }

    public static void Validate(ProtonBusTrafficLightDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidatePrefix(definition.Prefix);

        if (definition.PathCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "howManyPaths must be greater than zero.");
        }

        if (definition.Ticks.Count <= 0)
        {
            throw new ArgumentException(
                "A Proton Bus traffic light requires at least one tick.",
                nameof(definition));
        }

        ValidatePositiveFinite(definition.TickInterval, "tickInterval");
        ValidatePositiveFinite(definition.TriggerRadius, "triggerRadius");

        if (definition.FirstTickToRun < 1 ||
            definition.FirstTickToRun > definition.Ticks.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "firstTickToRun must reference an existing tick.");
        }

        for (var tickIndex = 0; tickIndex < definition.Ticks.Count; tickIndex++)
        {
            var tick = definition.Ticks[tickIndex];

            if (tick.Repeat <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    $"Tick {tickIndex + 1} repeat must be greater than zero.");
            }

            foreach (var path in tick.Paths.Keys)
            {
                if (path < 1 || path > definition.PathCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(definition),
                        $"Tick {tickIndex + 1} references path {path} outside 1..{definition.PathCount}.");
                }
            }
        }

        ValidateRealLight(definition.GreenLight, "green_light");
        ValidateRealLight(definition.RedLight, "red_light");
        ValidateRealLight(definition.YellowLight, "yellow_light");
    }

    private static void WriteRealLight(
        StringBuilder builder,
        string section,
        ProtonBusTrafficLightRealLight? light)
    {
        if (light is null)
        {
            return;
        }

        builder.AppendLine();
        builder.Append('[').Append(section).AppendLine("]");
        builder.Append("colorR=").AppendLine(FormatNumber(light.Color.X));
        builder.Append("colorG=").AppendLine(FormatNumber(light.Color.Y));
        builder.Append("colorB=").AppendLine(FormatNumber(light.Color.Z));
        builder.Append("colorA=").AppendLine(FormatNumber(light.Color.W));
        builder.Append("intensity=").AppendLine(FormatNumber(light.Intensity));
        builder.Append("range=").AppendLine(FormatNumber(light.Range));
    }

    private static void ValidateRealLight(
        ProtonBusTrafficLightRealLight? light,
        string section)
    {
        if (light is null)
        {
            return;
        }

        foreach (var component in new[]
                 {
                     light.Color.X,
                     light.Color.Y,
                     light.Color.Z,
                     light.Color.W
                 })
        {
            if (!float.IsFinite(component) || component < 0 || component > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(light),
                    $"{section} color channels must be between 0 and 1.");
            }
        }

        ValidateUnit(light.Intensity, $"{section}.intensity");
        ValidatePositiveFinite(light.Range, $"{section}.range");
    }

    private static void ValidatePrefix(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '_' or '-')))
        {
            throw new ArgumentException(
                "Traffic-light prefix must use only ASCII letters, numbers, underscore or hyphen.",
                nameof(value));
        }
    }

    private static void ValidateUnit(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(
                name,
                $"{name} must be finite and between 0 and 1.");
        }
    }

    private static void ValidatePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                name,
                $"{name} must be finite and greater than zero.");
        }
    }

    private static string ToBoolean(bool value) => value ? "1" : "0";

    private static string FormatNumber(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
