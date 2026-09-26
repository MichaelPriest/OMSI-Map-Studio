namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusGpsRouteDefinition(
    string EntrypointName,
    string? PartSuffix = null)
{
    public string ObjectName =>
        ProtonBusGpsRouteNamePlanner
            .BuildObjectName(
                EntrypointName,
                PartSuffix);
}

public static class ProtonBusGpsRouteNamePlanner
{
    public static string BuildObjectName(
        string entrypointName,
        string? partSuffix = null)
    {
        ValidateEntrypointName(
            entrypointName);

        if (
            !string.IsNullOrWhiteSpace(
                partSuffix))
        {
            ValidateSuffix(
                partSuffix);

            return
                $"_gps_{entrypointName}_{partSuffix}";
        }

        return
            $"_gps_{entrypointName}_";
    }

    public static bool MatchesEntrypoint(
        string objectName,
        string entrypointName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            objectName);

        ValidateEntrypointName(
            entrypointName);

        return objectName.Contains(
            $"_gps_{entrypointName}_",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateEntrypointName(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        if (
            value !=
            value.Trim() ||
            value.Any(
                character =>
                    character > 127 ||
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
                "GPS entrypoint name must use portable ASCII letters, numbers, spaces, underscore or hyphen.",
                nameof(value));
        }
    }

    private static void ValidateSuffix(
        string value)
    {
        if (
            value !=
            value.Trim() ||
            value.Any(
                character =>
                    character > 127 ||
                    !(
                        char.IsAsciiLetterOrDigit(
                            character) ||
                        character is
                            '_' or
                            '-' or
                            '.'
                    )))
        {
            throw new ArgumentException(
                "GPS mesh part suffix must use portable ASCII letters, numbers, underscore, hyphen or dot.",
                nameof(value));
        }
    }
}
