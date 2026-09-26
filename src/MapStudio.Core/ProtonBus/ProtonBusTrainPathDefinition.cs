using System.Globalization;
using System.Text;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusTrainPathDefinition(
    string Prefix,
    bool Reverse = false,
    bool Loop = false,
    int MaxPathsToCheck = 999,
    bool IsSpawner = false,
    bool RandomTimeToWaitAtStart = true,
    double SpawnTimeToWaitAtStartSeconds = 20.0,
    double SpawnTimeIntervalSeconds = 120.0,
    int TrainType = 0)
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

public static class ProtonBusTrainPathDefinitionWriter
{
    public static string Serialize(ProtonBusTrainPathDefinition definition)
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
        builder.Append("randomTimeToWaitAtStart=").AppendLine(
            ToBoolean(definition.RandomTimeToWaitAtStart));
        builder.Append("spawnTimeToWaitAtStart=").AppendLine(
            FormatNumber(definition.SpawnTimeToWaitAtStartSeconds));
        builder.Append("spawnTimeInterval=").AppendLine(
            FormatNumber(definition.SpawnTimeIntervalSeconds));
        builder.Append("trainType=").AppendLine(
            definition.TrainType.ToString(CultureInfo.InvariantCulture));

        return builder.ToString();
    }

    public static void Validate(ProtonBusTrainPathDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidatePrefix(definition.Prefix);

        if (definition.MaxPathsToCheck <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "maxPathsToCheck must be greater than zero.");
        }

        if (!double.IsFinite(definition.SpawnTimeToWaitAtStartSeconds) ||
            definition.SpawnTimeToWaitAtStartSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "spawnTimeToWaitAtStart must be finite and non-negative.");
        }

        if (!double.IsFinite(definition.SpawnTimeIntervalSeconds) ||
            definition.SpawnTimeIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "spawnTimeInterval must be finite and greater than zero.");
        }

        if (definition.TrainType < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "trainType cannot be negative.");
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
                "Train path prefix must use only ASCII letters, numbers, underscore or hyphen.",
                nameof(value));
        }
    }

    private static string ToBoolean(bool value) => value ? "1" : "0";

    private static string FormatNumber(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
