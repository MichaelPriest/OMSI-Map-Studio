using System.Globalization;
using System.Text;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusBusStopDefinition(
    string Prefix,
    string FriendlyName,
    int PassengerAmount,
    double DefaultPassengerRotationY,
    bool IsFirstStop = false,
    bool IsLatestStop = false,
    bool IsLeft = false,
    double Radius = 1.0,
    int MaxPathsToCheck = 30,
    IReadOnlyDictionary<int, double>?
        IndividualPassengerRotations = null)
{
    public string SuggestedFileName =>
        Prefix +
        ".txt";

    public string TriggerObjectName =>
        Prefix +
        "_trigger";

    public string GetPassengerPositionObjectName(
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

public static class ProtonBusBusStopDefinitionWriter
{
    public static string Serialize(
        ProtonBusBusStopDefinition definition)
    {
        Validate(
            definition);

        var builder =
            new StringBuilder();

        builder.AppendLine(
            "[busstop]");

        builder.Append(
            "name=")
            .AppendLine(
                definition
                    .FriendlyName);

        builder.Append(
            "isFirstStop=")
            .AppendLine(
                ToBoolean(
                    definition
                        .IsFirstStop));

        builder.Append(
            "isLatestStop=")
            .AppendLine(
                ToBoolean(
                    definition
                        .IsLatestStop));

        builder.Append(
            "isLeft=")
            .AppendLine(
                ToBoolean(
                    definition
                        .IsLeft));

        builder.Append(
            "radius=")
            .AppendLine(
                FormatNumber(
                    definition
                        .Radius));

        builder.Append(
            "paxAmount=")
            .AppendLine(
                definition
                    .PassengerAmount
                    .ToString(
                        CultureInfo
                            .InvariantCulture));

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

        builder.Append(
            "defPaxRotY=")
            .AppendLine(
                FormatNumber(
                    definition
                        .DefaultPassengerRotationY));

        foreach (
            var rotation
            in (
                definition
                    .IndividualPassengerRotations ??
                new Dictionary<int, double>()
            )
            .OrderBy(
                pair =>
                    pair.Key))
        {
            builder.AppendLine();

            builder.Append('[')
                .Append(
                    rotation.Key.ToString(
                        "000",
                        CultureInfo
                            .InvariantCulture))
                .AppendLine("]");

            builder.Append(
                "rotY=")
                .AppendLine(
                    FormatNumber(
                        rotation.Value));
        }

        return builder
            .ToString();
    }

    public static void Validate(
        ProtonBusBusStopDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        ValidatePrefix(
            definition.Prefix);

        ValidateFriendlyName(
            definition.FriendlyName);

        if (
            definition.PassengerAmount <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "Passenger amount cannot be negative.");
        }

        if (
            definition.MaxPathsToCheck <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "maxPathsToCheck must be greater than zero.");
        }

        ValidateFinite(
            definition.Radius,
            "radius");

        if (
            definition.Radius <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "Bus stop radius cannot be negative.");
        }

        ValidateFinite(
            definition
                .DefaultPassengerRotationY,
            "defPaxRotY");

        foreach (
            var rotation
            in definition
                .IndividualPassengerRotations ??
               new Dictionary<int, double>())
        {
            if (
                rotation.Key <
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    "Passenger position indices cannot be negative.");
            }

            if (
                rotation.Key >=
                definition
                    .MaxPathsToCheck)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    $"Passenger position {rotation.Key:000} is outside maxPathsToCheck={definition.MaxPathsToCheck}.");
            }

            ValidateFinite(
                rotation.Value,
                $"rotY[{rotation.Key:000}]");
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
                "Bus stop prefix must use only ASCII letters, numbers, underscore or hyphen.",
                nameof(value));
        }
    }

    private static void ValidateFriendlyName(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        if (
            value.Any(
                character =>
                    character >
                        127 ||
                    character is
                        '\r' or
                        '\n' or
                        '='))
        {
            throw new ArgumentException(
                "Bus stop friendly name must be a single portable ASCII value.",
                nameof(value));
        }
    }

    private static void ValidateFinite(
        double value,
        string fieldName)
    {
        if (
            !double.IsFinite(
                value))
        {
            throw new ArgumentException(
                $"{fieldName} must be finite.");
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
