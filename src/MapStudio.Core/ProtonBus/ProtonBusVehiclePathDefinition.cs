using System.Globalization;
using System.Text;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusVehiclePathDefinition(
    string Prefix,
    bool Reverse = false,
    bool Loop = false,
    int MaxPathsToCheck = 999,
    bool IsSpawner = true,
    bool IsBusSpawner = false,
    bool RightBlinker = false,
    bool LeftBlinker = false,
    double SpawnIntervalSeconds = 5.0,
    bool ApplySpeedMultiplier = false,
    double SpeedMultiplier = 1.0)
{
    public string SuggestedFileName => Prefix + ".txt";

    public string GetWaypointObjectName(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return Prefix + "." + index.ToString("000", CultureInfo.InvariantCulture);
    }
}

public static class ProtonBusVehiclePathDefinitionWriter
{
    public static string Serialize(ProtonBusVehiclePathDefinition definition)
    {
        Validate(definition);

        var builder = new StringBuilder();

        builder.AppendLine("[automatic_setup]");
        builder.AppendLine("enabled=1");
        builder.Append("reverse=").AppendLine(ToBoolean(definition.Reverse));
        builder.Append("loop=").AppendLine(ToBoolean(definition.Loop));
        builder.AppendLine();

        builder.AppendLine("[from_3d]");
        builder.AppendLine("readFrom3D=1");
        builder.Append("prefix=").AppendLine(definition.Prefix);
        builder.Append("maxPathsToCheck=").AppendLine(
            definition.MaxPathsToCheck.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine();

        builder.AppendLine("[defaults]");
        builder.Append("isSpawner=").AppendLine(ToBoolean(definition.IsSpawner));
        builder.Append("isBusSpawner=").AppendLine(ToBoolean(definition.IsBusSpawner));
        builder.Append("rightBlinker=").AppendLine(ToBoolean(definition.RightBlinker));
        builder.Append("leftBlinker=").AppendLine(ToBoolean(definition.LeftBlinker));
        builder.Append("spawnInterval=").AppendLine(FormatNumber(definition.SpawnIntervalSeconds));
        builder.Append("applySpeedMultiplier=").AppendLine(ToBoolean(definition.ApplySpeedMultiplier));
        builder.Append("speedMultiplier=").AppendLine(FormatNumber(definition.SpeedMultiplier));

        return builder.ToString();
    }

    public static void Validate(ProtonBusVehiclePathDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidatePrefix(definition.Prefix);

        if (definition.MaxPathsToCheck <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "maxPathsToCheck must be greater than zero.");
        }

        if (!double.IsFinite(definition.SpawnIntervalSeconds) ||
            definition.SpawnIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "spawnInterval must be finite and greater than zero.");
        }

        if (!double.IsFinite(definition.SpeedMultiplier) ||
            definition.SpeedMultiplier <= 0 ||
            definition.SpeedMultiplier > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "speedMultiplier must be greater than zero and at most 2.");
        }
    }

    private static void ValidatePrefix(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '_' or '-')))
        {
            throw new ArgumentException(
                "Vehicle path prefix must use only ASCII letters, numbers, underscore or hyphen.",
                nameof(value));
        }
    }

    private static string ToBoolean(bool value) => value ? "1" : "0";

    private static string FormatNumber(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
