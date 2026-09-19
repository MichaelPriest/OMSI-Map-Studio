namespace MapStudio.Core.Omsi.Textures;

public static class OmsiTextureAssetPathResolver
{
    private static readonly HashSet<string>
        SupportedExtensions =
            new(
                [
                    ".bmp",
                    ".dds",
                    ".gif",
                    ".jpeg",
                    ".jpg",
                    ".png",
                    ".tga",
                    ".webp"
                ],
                StringComparer.OrdinalIgnoreCase);

    public static bool TryResolveTerrainTextureMask(
        string mapDirectory,
        string relativeMapPath,
        int layerIndex,
        out string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            relativeMapPath);

        fullPath = string.Empty;

        if (layerIndex <= 0)
        {
            return false;
        }

        if (
            !Omsi.Maps.OmsiMapPathResolver
                .TryResolveTilePath(
                    mapDirectory,
                    relativeMapPath,
                    out var tilePath))
        {
            return false;
        }

        try
        {
            var mapRoot =
                Path.GetFullPath(
                    mapDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            var requiredPrefix =
                mapRoot +
                Path.DirectorySeparatorChar;

            var candidate =
                Path.GetFullPath(
                    Path.Combine(
                        mapRoot,
                        "texture",
                        "map",
                        Path.GetFileName(
                            tilePath) +
                        "." +
                        layerIndex
                            .ToString(
                                System.Globalization
                                    .CultureInfo
                                    .InvariantCulture) +
                        ".dds"));

            if (
                !candidate.StartsWith(
                    requiredPrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(candidate))
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

    public static bool TryResolveGroundTexture(
        string omsiRoot,
        string mapDirectory,
        string textureName,
        out string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            textureName);

        fullPath = string.Empty;

        var root =
            Path.GetFullPath(
                omsiRoot);

        var mapRoot =
            Path.GetFullPath(
                mapDirectory);

        var requiredRootPrefix =
            root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (
            !mapRoot.StartsWith(
                requiredRootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return TryResolve(
            root,
            textureName,
            [
                mapRoot,
                root
            ],
            out fullPath);
    }

    public static bool TryResolveSceneryTexture(
        string omsiRoot,
        string sceneryObjectFullPath,
        string meshFullPath,
        string textureName,
        out string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sceneryObjectFullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            meshFullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            textureName);

        fullPath = string.Empty;

        var sceneryRoot =
            Path.GetFullPath(
                Path.Combine(
                    omsiRoot,
                    "Sceneryobjects"));

        var objectDirectory =
            Path.GetDirectoryName(
                Path.GetFullPath(
                    sceneryObjectFullPath));

        var meshDirectory =
            Path.GetDirectoryName(
                Path.GetFullPath(
                    meshFullPath));

        if (
            string.IsNullOrWhiteSpace(
                objectDirectory) ||
            string.IsNullOrWhiteSpace(
                meshDirectory))
        {
            return false;
        }

        return TryResolve(
            sceneryRoot,
            textureName,
            [
                objectDirectory,
                Path.Combine(
                    objectDirectory,
                    "Texture"),
                meshDirectory,
                Path.Combine(
                    meshDirectory,
                    "Texture"),
                Path.GetFullPath(
                    Path.Combine(
                        meshDirectory,
                        "..",
                        "Texture"))
            ],
            out fullPath);
    }

    public static bool TryResolveSplineTexture(
        string omsiRoot,
        string splineFullPath,
        string textureName,
        out string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            splineFullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            textureName);

        fullPath = string.Empty;

        var splinesRoot =
            Path.GetFullPath(
                Path.Combine(
                    omsiRoot,
                    "Splines"));

        var splineDirectory =
            Path.GetDirectoryName(
                Path.GetFullPath(
                    splineFullPath));

        if (string.IsNullOrWhiteSpace(
                splineDirectory))
        {
            return false;
        }

        return TryResolve(
            splinesRoot,
            textureName,
            [
                splineDirectory,
                Path.Combine(
                    splineDirectory,
                    "Texture")
            ],
            out fullPath);
    }

    private static bool TryResolve(
        string allowedRoot,
        string textureName,
        IReadOnlyList<string> baseDirectories,
        out string fullPath)
    {
        fullPath = string.Empty;

        var trimmed =
            textureName.Trim();

        if (
            Path.IsPathRooted(trimmed) ||
            trimmed.Contains(
                ':',
                StringComparison.Ordinal) ||
            trimmed.StartsWith(
                @"\\",
                StringComparison.Ordinal) ||
            trimmed.StartsWith(
                "//",
                StringComparison.Ordinal) ||
            !SupportedExtensions.Contains(
                Path.GetExtension(
                    trimmed)))
        {
            return false;
        }

        try
        {
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

            var root =
                Path.GetFullPath(
                    allowedRoot)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            var requiredPrefix =
                root +
                Path.DirectorySeparatorChar;

            foreach (var baseDirectory in
                baseDirectories)
            {
                var candidate =
                    Path.GetFullPath(
                        Path.Combine(
                            baseDirectory,
                            normalized));

                if (
                    !candidate.StartsWith(
                        requiredPrefix,
                        StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(candidate))
                {
                    continue;
                }

                fullPath = candidate;
                return true;
            }

            return false;
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
