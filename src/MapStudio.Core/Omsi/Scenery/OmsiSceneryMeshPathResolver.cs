namespace MapStudio.Core.Omsi.Scenery;

public static class OmsiSceneryMeshPathResolver
{
    private static readonly string[] SupportedExtensions =
        [".o3d", ".x"];

    public static bool TryResolve(
        string omsiRoot,
        string sceneryObjectFullPath,
        string declaredMeshPath,
        out string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneryObjectFullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredMeshPath);

        fullPath = string.Empty;

        var extension = Path.GetExtension(declaredMeshPath);

        if (!SupportedExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var trimmed = declaredMeshPath.Trim();

        if (trimmed.Contains(':', StringComparison.Ordinal) ||
            trimmed.StartsWith(@"\\", StringComparison.Ordinal) ||
            trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var sceneryRoot = Path
                .GetFullPath(Path.Combine(omsiRoot, "Sceneryobjects"))
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            var objectDirectory =
                Path.GetDirectoryName(
                    Path.GetFullPath(sceneryObjectFullPath));

            if (string.IsNullOrWhiteSpace(objectDirectory))
            {
                return false;
            }

            var normalized = trimmed
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            var candidate = Path.GetFullPath(
                Path.Combine(
                    objectDirectory,
                    "model",
                    normalized));

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
