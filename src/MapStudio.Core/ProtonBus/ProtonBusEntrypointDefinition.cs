using System.Globalization;
using System.Numerics;
using System.Text;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusEntrypointDefinition(
    string Name,
    Vector3 Position,
    Vector3 RotationDegrees,
    bool IsIntercity = false,
    bool IsOutOfService = false);

public static class ProtonBusEntrypointDefinitionWriter
{
    public static string SerializeList(
        IReadOnlyList<
            ProtonBusEntrypointDefinition>
            entrypoints)
    {
        ValidateAll(
            entrypoints);

        return string.Join(
                   Environment.NewLine,
                   entrypoints.Select(
                       entrypoint =>
                           entrypoint.Name)) +
               (
                   entrypoints.Count >
                       0
                       ? Environment.NewLine
                       : string.Empty
               );
    }

    public static string SerializeDefinitions(
        IReadOnlyList<
            ProtonBusEntrypointDefinition>
            entrypoints)
    {
        ValidateAll(
            entrypoints);

        var builder =
            new StringBuilder();

        for (
            var index = 0;
            index <
                entrypoints.Count;
            index++)
        {
            var entrypoint =
                entrypoints[
                    index];

            if (
                index >
                0)
            {
                builder.AppendLine();
            }

            builder.Append('[')
                .Append(
                    "entrypoint_")
                .Append(
                    (
                        index +
                        1
                    )
                    .ToString(
                        CultureInfo
                            .InvariantCulture))
                .AppendLine("]");

            builder.Append(
                "name=")
                .AppendLine(
                    entrypoint.Name);

            builder.Append(
                "posX=")
                .AppendLine(
                    FormatNumber(
                        entrypoint
                            .Position
                            .X));

            builder.Append(
                "posY=")
                .AppendLine(
                    FormatNumber(
                        entrypoint
                            .Position
                            .Y));

            builder.Append(
                "posZ=")
                .AppendLine(
                    FormatNumber(
                        entrypoint
                            .Position
                            .Z));

            builder.Append(
                "rotX=")
                .AppendLine(
                    FormatNumber(
                        entrypoint
                            .RotationDegrees
                            .X));

            builder.Append(
                "rotY=")
                .AppendLine(
                    FormatNumber(
                        entrypoint
                            .RotationDegrees
                            .Y));

            builder.Append(
                "rotZ=")
                .AppendLine(
                    FormatNumber(
                        entrypoint
                            .RotationDegrees
                            .Z));
        }

        return builder
            .ToString();
    }

    public static void ValidateAll(
        IReadOnlyList<
            ProtonBusEntrypointDefinition>
            entrypoints)
    {
        ArgumentNullException.ThrowIfNull(
            entrypoints);

        var names =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);

        foreach (
            var entrypoint
            in entrypoints)
        {
            ArgumentNullException.ThrowIfNull(
                entrypoint);

            ValidateName(
                entrypoint.Name);

            if (
                !names.Add(
                    entrypoint.Name))
            {
                throw new ArgumentException(
                    $"Duplicate Proton Bus entrypoint name '{entrypoint.Name}'.",
                    nameof(entrypoints));
            }

            ValidateVector(
                entrypoint.Position,
                "position");

            ValidateVector(
                entrypoint
                    .RotationDegrees,
                "rotation");
        }
    }

    private static void ValidateName(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        if (
            value !=
            value.Trim())
        {
            throw new ArgumentException(
                "Entrypoint names cannot start or end with spaces.",
                nameof(value));
        }

        if (
            value.Any(
                character =>
                    !(
                        char.IsAsciiLetterOrDigit(
                            character) ||
                        character is
                            ' ' or
                            '_' or
                            '-'
                    )))
        {
            throw new ArgumentException(
                "Entrypoint names must use portable ASCII letters, numbers, spaces, underscore or hyphen.",
                nameof(value));
        }
    }

    private static void ValidateVector(
        Vector3 value,
        string fieldName)
    {
        if (
            !float.IsFinite(
                value.X) ||
            !float.IsFinite(
                value.Y) ||
            !float.IsFinite(
                value.Z))
        {
            throw new ArgumentException(
                $"Entrypoint {fieldName} must contain finite values.");
        }
    }

    private static string FormatNumber(
        float value) =>
        value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);
}
