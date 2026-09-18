namespace MapStudio.Core.Omsi.Scenery;

public static class OmsiSceneryObjectPathResolver
{
    private const string SceneryDirectoryName = "Sceneryobjects";

    public static bool TryResolve(
        string omsiRoot,
        string sceneryObjectPath,
        out string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneryObjectPath);

        fullPath = string.Empty;

        if (!string.Equals(
                Path.GetExtension(sceneryObjectPath),
                ".sco",
                StringComparison.OrdinalIgnoreCase) ||
            Path.IsPathRooted(sceneryObjectPath))
        {
            return false;
        }

        try
        {
            var sceneryRoot = Path
                .GetFullPath(Path.Combine(omsiRoot, SceneryDirectoryName))
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            var normalized = sceneryObjectPath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            var sceneryPrefix =
                SceneryDirectoryName + Path.DirectorySeparatorChar;

            if (normalized.StartsWith(
                    sceneryPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[sceneryPrefix.Length..];
            }

            var candidate = Path.GetFullPath(
                Path.Combine(sceneryRoot, normalized));

            var requiredPrefix =
                sceneryRoot + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(
                    requiredPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return false;
        }
    }
}
