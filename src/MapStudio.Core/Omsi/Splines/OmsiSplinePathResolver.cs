namespace MapStudio.Core.Omsi.Splines;

public static class OmsiSplinePathResolver
{
    public static bool TryResolve(
        string omsiRoot,
        string declaredSplinePath,
        out string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredSplinePath);

        fullPath = string.Empty;

        var trimmed =
            declaredSplinePath.Trim();

        if (!string.Equals(
                Path.GetExtension(trimmed),
                ".sli",
                StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains(
                ':',
                StringComparison.Ordinal) ||
            trimmed.StartsWith(
                @"\\",
                StringComparison.Ordinal) ||
            trimmed.StartsWith(
                "//",
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var splinesRoot = Path
                .GetFullPath(
                    Path.Combine(
                        omsiRoot,
                        "Splines"))
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            var normalized =
                trimmed
                    .Replace(
                        '\\',
                        Path.DirectorySeparatorChar)
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar)
                    .TrimStart(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            var splinesPrefix =
                "Splines" +
                Path.DirectorySeparatorChar;

            if (normalized.StartsWith(
                    splinesPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized =
                    normalized[
                        splinesPrefix.Length..];
            }

            var candidate =
                Path.GetFullPath(
                    Path.Combine(
                        splinesRoot,
                        normalized));

            var requiredPrefix =
                splinesRoot +
                Path.DirectorySeparatorChar;

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
