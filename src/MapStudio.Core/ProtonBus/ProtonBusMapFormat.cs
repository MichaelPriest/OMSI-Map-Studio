namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusMapDefinition(
    string MapName,
    string BaseDirectory,
    string ModelsDirectory,
    string TexturesDirectory = "textures",
    int MapModVersion = 3,
    string Preview = "preview")
{
    public string MapFileName
    {
        get
        {
            if (
                MapName.EndsWith(
                    ".map.txt",
                    StringComparison.OrdinalIgnoreCase))
            {
                return MapName;
            }

            if (
                MapName.EndsWith(
                    ".map",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    MapName +
                    ".txt";
            }

            return
                $"{MapName}.map.txt";
        }
    }
}

public sealed record ProtonBusMapPackageLayout(
    string MapDefinitionPath,
    string BaseDirectoryPath,
    string ModelsDirectoryPath,
    string TexturesDirectoryPath,
    string DestinationsDirectoryPath,
    string SkinsDirectoryPath,
    string AiPeopleDirectoryPath,
    string AiTrainsDirectoryPath,
    string AiVehiclesDirectoryPath,
    string BusStopsDirectoryPath,
    string TrafficLightsDirectoryPath,
    string StreetLightsDirectoryPath)
{
    public static ProtonBusMapPackageLayout
        From(
            ProtonBusMapDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        var basePath =
            Combine(
                "maps",
                definition.BaseDirectory);

        var modelsPath =
            Combine(
                basePath,
                "tiles",
                definition.ModelsDirectory);

        return new(
            Combine(
                "maps",
                definition.MapFileName),
            basePath,
            modelsPath,
            Combine(
                basePath,
                definition.TexturesDirectory),
            Combine(
                basePath,
                "dest"),
            Combine(
                basePath,
                "skins"),
            Combine(
                modelsPath,
                "aipeople"),
            Combine(
                modelsPath,
                "aitrains"),
            Combine(
                modelsPath,
                "aivehicles"),
            Combine(
                modelsPath,
                "busstops"),
            Combine(
                modelsPath,
                "trafficlights"),
            Combine(
                modelsPath,
                "streetlights"));
    }

    private static string Combine(
        params string[] parts) =>
        string.Join(
            "/",
            parts
                .Where(
                    part =>
                        !string.IsNullOrWhiteSpace(
                            part))
                .Select(
                    part =>
                        part
                            .Trim()
                            .Trim(
                                '/',
                                '\\')));
}

public enum ProtonBusValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ProtonBusValidationIssue(
    ProtonBusValidationSeverity Severity,
    string Code,
    string Message);

public static class ProtonBusMapDefinitionValidator
{
    public static IReadOnlyList<
        ProtonBusValidationIssue>
        Validate(
            ProtonBusMapDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        var issues =
            new List<
                ProtonBusValidationIssue>();

        ValidatePortableRelativeName(
            definition.MapName,
            "map-name",
            "Map name",
            issues);

        ValidatePortableRelativeName(
            definition.BaseDirectory,
            "base-dir",
            "Base directory",
            issues);

        ValidatePortableRelativeName(
            definition.ModelsDirectory,
            "models-dir",
            "Models directory",
            issues);

        ValidatePortableRelativeName(
            definition.TexturesDirectory,
            "textures-dir",
            "Textures directory",
            issues);

        ValidatePortableRelativeName(
            definition.Preview,
            "preview",
            "Preview name",
            issues);

        if (
            definition.MapModVersion <
            1)
        {
            issues.Add(
                new(
                    ProtonBusValidationSeverity
                        .Error,
                    "PBMAP_VERSION_INVALID",
                    "mapModVersion must be a positive integer."));
        }

        if (
            definition.MapModVersion >
            3)
        {
            issues.Add(
                new(
                    ProtonBusValidationSeverity
                        .Warning,
                    "PBMAP_VERSION_EXPERIMENTAL",
                    "Versions above mapModVersion=3 require validation against the target Proton Bus build before export."));
        }

        return issues;
    }

    public static void ThrowIfInvalid(
        ProtonBusMapDefinition definition)
    {
        var errors =
            Validate(
                definition)
                .Where(
                    issue =>
                        issue.Severity ==
                        ProtonBusValidationSeverity
                            .Error)
                .ToArray();

        if (
            errors.Length ==
            0)
        {
            return;
        }

        throw new ArgumentException(
            string.Join(
                Environment.NewLine,
                errors.Select(
                    issue =>
                        $"{issue.Code}: {issue.Message}")),
            nameof(definition));
    }

    private static void
        ValidatePortableRelativeName(
            string value,
            string fieldCode,
            string fieldName,
            ICollection<
                ProtonBusValidationIssue>
                issues)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            issues.Add(
                new(
                    ProtonBusValidationSeverity
                        .Error,
                    $"PBMAP_{fieldCode.ToUpperInvariant().Replace("-", "_")}_EMPTY",
                    $"{fieldName} cannot be empty."));
            return;
        }

        var normalized =
            value
                .Trim()
                .Replace(
                    '\\',
                    '/');

        if (
            normalized.StartsWith(
                "/",
                StringComparison.Ordinal) ||
            normalized.Contains(
                ':') ||
            normalized
                .Split(
                    '/',
                    StringSplitOptions
                        .RemoveEmptyEntries)
                .Any(
                    segment =>
                        segment ==
                            "." ||
                        segment ==
                            ".."))
        {
            issues.Add(
                new(
                    ProtonBusValidationSeverity
                        .Error,
                    $"PBMAP_{fieldCode.ToUpperInvariant().Replace("-", "_")}_NOT_RELATIVE",
                    $"{fieldName} must be a portable relative name/path."));
        }

        if (
            normalized.Any(
                character =>
                    character >
                        127 ||
                    !IsPortableCharacter(
                        character)))
        {
            issues.Add(
                new(
                    ProtonBusValidationSeverity
                        .Error,
                    $"PBMAP_{fieldCode.ToUpperInvariant().Replace("-", "_")}_UNSAFE_CHARS",
                    $"{fieldName} must use portable ASCII file-name characters only; avoid accents, cedilla and special characters."));
        }
    }

    private static bool
        IsPortableCharacter(
            char character) =>
        char.IsAsciiLetterOrDigit(
            character) ||
        character is
            ' ' or
            '_' or
            '-' or
            '.' or
            '/' or
            '\\';
}

public static class ProtonBusMapDefinitionWriter
{
    public static string Serialize(
        ProtonBusMapDefinition definition)
    {
        ProtonBusMapDefinitionValidator
            .ThrowIfInvalid(
                definition);

        return string.Join(
                   Environment.NewLine,
                   [
                       "[map]",
                       $"baseDir={definition.BaseDirectory}",
                       $"modelsDir={definition.ModelsDirectory}",
                       $"textures={definition.TexturesDirectory}",
                       $"mapModVersion={definition.MapModVersion}",
                       $"preview={definition.Preview}"
                   ]) +
               Environment.NewLine;
    }
}

public static class ProtonBusMeshNameTags
{
    public const string Transparent =
        "_transparent_";

    public const string Collider =
        "_gencol_";

    public const string Invisible =
        "_invisible_";

    public const string Emissive =
        "_emissive_";

    public const string Additive =
        "_additive_";

    public const string LowSpeedZone =
        "_low_speed_zone_";

    public const string ForceExit =
        "_force_exit_";
}
