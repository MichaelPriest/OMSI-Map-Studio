namespace MapStudio.Core.Omsi.Maps;

public static class OmsiMapPathResolver
{
    public static bool TryResolveTilePath(
        string mapDirectory,
        string relativeMapPath,
        out string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeMapPath);

        try
        {
            var root = Path.GetFullPath(mapDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var normalizedRelativePath = relativeMapPath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var candidate = Path.GetFullPath(Path.Combine(root, normalizedRelativePath));
            var rootPrefix = root + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                fullPath = string.Empty;
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
            fullPath = string.Empty;
            return false;
        }
    }
}
