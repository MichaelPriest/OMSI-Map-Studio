using System.Globalization;
using System.Text;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusPedestrianPathDefinition(
    string Prefix,
    bool Reverse = false,
    bool Loop = false,
    int MaxPathsToCheck = 999,
    bool IsSpawner = true,
    double SpawnIntervalSeconds = 5.0,
    bool AllowBicycle = true)
{
    public string SuggestedFileName =>
        Prefix +
        ".txt";

    public string GetWaypointObjectName(
        int index)
    {
        if (
            index <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index));
        }

        return
            Prefix +
            "." +
            index.ToString(
                "000",
                CultureInfo.InvariantCulture);
    }
}

public static class ProtonBusPedestrianPathDefinitionWriter
{
    public static string Serialize(
        ProtonBusPedestrianPathDefinition
            definition)
    {
        Validate(
            definition);

        var builder =
            new StringBuilder();

        builder.AppendLine(
            "[automatic_setup]");

        builder.AppendLine(
            "enabled=1");

        builder.Append(
            "reverse=")
            .AppendLine(
                ToBoolean(
                    definition
                        .Reverse));

        builder.Append(
            "loop=")
            .AppendLine(
                ToBoolean(
                    definition
                        .Loop));

        builder.AppendLine();

        builder.AppendLine(
            "[from_3d]");

        builder.AppendLine(
            "readFrom3D=1");

        builder.Append(
            "prefix=")
            .AppendLine(
                definition.Prefix);

        builder.Append(
            "maxPathsToCheck=")
            .AppendLine(
                definition
                    .MaxPathsToCheck
                    .ToString(
                        CultureInfo
                            .InvariantCulture));

        builder.AppendLine();

        builder.AppendLine(
            "[defaults]");

        builder.Append(
            "isSpawner=")
            .AppendLine(
                ToBoolean(
                    definition
                        .IsSpawner));

        builder.Append(
            "spawnInterval=")
            .AppendLine(
                FormatNumber(
                    definition
                        .SpawnIntervalSeconds));

        builder.Append(
            "allowBicycle=")
            .AppendLine(
                ToBoolean(
                    definition
                        .AllowBicycle));

        return builder
            .ToString();
    }

    public static void Validate(
        ProtonBusPedestrianPathDefinition
            definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        ValidatePrefix(
            definition.Prefix);

        if (
            definition.MaxPathsToCheck <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "maxPathsToCheck must be greater than zero.");
        }

        if (
            !double.IsFinite(
                definition
                    .SpawnIntervalSeconds) ||
            definition
                .SpawnIntervalSeconds <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "spawnInterval must be finite and greater than zero.");
        }
    }

    private static void ValidatePrefix(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        if (
            value.Any(
                character =>
                    !(
                        char.IsAsciiLetterOrDigit(
                            character) ||
                        character is
                            '_' or
                            '-'
                    )))
        {
            throw new ArgumentException(
                "Pedestrian path prefix must use only ASCII letters, numbers, underscore or hyphen.",
                nameof(value));
        }
    }

    private static string ToBoolean(
        bool value) =>
        value
            ? "1"
            : "0";

    private static string FormatNumber(
        double value) =>
        value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);
}
