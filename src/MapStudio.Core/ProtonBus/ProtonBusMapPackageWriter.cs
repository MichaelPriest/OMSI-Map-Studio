namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusModelExport(
    string FileName,
    ProtonBusExportScene Scene);

public sealed record ProtonBusTextureExport(
    string SourcePath,
    string FileName);

public sealed record ProtonBusMapPackageRequest(
    ProtonBusMapDefinition Definition,
    IReadOnlyList<ProtonBusModelExport>
        Models,
    IReadOnlyList<ProtonBusTextureExport>?
        Textures = null);

public sealed record ProtonBusMapPackageResult(
    string OutputRoot,
    string MapDefinitionPath,
    IReadOnlyList<string>
        ModelPaths,
    IReadOnlyList<string>
        TexturePaths);

public static class ProtonBusMapPackageWriter
{
    public static ProtonBusMapPackageResult Write(
        string outputRoot,
        ProtonBusMapPackageRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            outputRoot);

        ArgumentNullException.ThrowIfNull(
            request);

        ProtonBusMapDefinitionValidator
            .ThrowIfInvalid(
                request.Definition);

        var root =
            Path.GetFullPath(
                outputRoot);

        Directory.CreateDirectory(
            root);

        var layout =
            ProtonBusMapPackageLayout
                .From(
                    request.Definition);

        var mapDefinitionPath =
            ResolveOutputPath(
                root,
                layout.MapDefinitionPath);

        EnsureDirectoryForFile(
            mapDefinitionPath);

        File.WriteAllText(
            mapDefinitionPath,
            ProtonBusMapDefinitionWriter
                .Serialize(
                    request.Definition));

        foreach (
            var directory
            in EnumerateDirectories(
                layout))
        {
            Directory.CreateDirectory(
                ResolveOutputPath(
                    root,
                    directory));
        }

        var modelPaths =
            new List<string>(
                request.Models.Count);

        foreach (
            var model
            in request.Models)
        {
            ArgumentNullException.ThrowIfNull(
                model);

            var fileName =
                NormalizeModelFileName(
                    model.FileName);

            var relativePath =
                CombineRelative(
                    layout.ModelsDirectoryPath,
                    fileName);

            var targetPath =
                ResolveOutputPath(
                    root,
                    relativePath);

            File.WriteAllBytes(
                targetPath,
                ProtonBus3dsWriter
                    .Write(
                        model.Scene));

            modelPaths.Add(
                targetPath);
        }

        var texturePaths =
            new List<string>();

        foreach (
            var texture
            in request.Textures ??
               Array.Empty<
                   ProtonBusTextureExport>())
        {
            ArgumentNullException.ThrowIfNull(
                texture);

            if (
                !File.Exists(
                    texture.SourcePath))
            {
                throw new FileNotFoundException(
                    "Proton Bus texture source was not found.",
                    texture.SourcePath);
            }

            if (
                !string.Equals(
                    Path.GetExtension(
                        texture.SourcePath),
                    ".png",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Proton Bus texture sources must already be PNG until the texture transcoder is enabled.",
                    nameof(request));
            }

            var fileName =
                NormalizePngFileName(
                    texture.FileName);

            var targetPath =
                ResolveOutputPath(
                    root,
                    CombineRelative(
                        layout.TexturesDirectoryPath,
                        fileName));

            File.Copy(
                texture.SourcePath,
                targetPath,
                overwrite: true);

            texturePaths.Add(
                targetPath);
        }

        return new(
            root,
            mapDefinitionPath,
            modelPaths.ToArray(),
            texturePaths.ToArray());
    }

    private static IEnumerable<string>
        EnumerateDirectories(
            ProtonBusMapPackageLayout
                layout)
    {
        yield return
            layout.BaseDirectoryPath;

        yield return
            layout.ModelsDirectoryPath;

        yield return
            layout.TexturesDirectoryPath;

        yield return
            layout.DestinationsDirectoryPath;

        yield return
            layout.SkinsDirectoryPath;

        yield return
            layout.AiPeopleDirectoryPath;

        yield return
            layout.AiTrainsDirectoryPath;

        yield return
            layout.AiVehiclesDirectoryPath;

        yield return
            layout.BusStopsDirectoryPath;

        yield return
            layout.TrafficLightsDirectoryPath;

        yield return
            layout.StreetLightsDirectoryPath;
    }

    private static string
        NormalizeModelFileName(
            string value)
    {
        var name =
            NormalizeFileName(
                value,
                "model");

        return name.EndsWith(
                ".3ds",
                StringComparison
                    .OrdinalIgnoreCase)
            ? name
            : name +
              ".3ds";
    }

    private static string
        NormalizePngFileName(
            string value)
    {
        var name =
            NormalizeFileName(
                value,
                "texture");

        if (
            !name.EndsWith(
                ".png",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Proton Bus package textures must use the PNG extension.",
                nameof(value));
        }

        return name;
    }

    private static string NormalizeFileName(
        string value,
        string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        var name =
            Path.GetFileName(
                value.Trim());

        if (
            !string.Equals(
                name,
                value.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{fieldName} must be a file name, not a path.",
                nameof(value));
        }

        if (
            name.Any(
                character =>
                    character >
                        127 ||
                    !(
                        char.IsAsciiLetterOrDigit(
                            character) ||
                        character is
                            ' ' or
                            '_' or
                            '-' or
                            '.'
                    )))
        {
            throw new ArgumentException(
                $"{fieldName} contains characters that are unsafe for Proton Bus packages.",
                nameof(value));
        }

        return name;
    }

    private static string ResolveOutputPath(
        string root,
        string relativePath)
    {
        var platformRelative =
            relativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    platformRelative));

        var prefix =
            root.EndsWith(
                Path.DirectorySeparatorChar)
                ? root
                : root +
                  Path.DirectorySeparatorChar;

        if (
            !fullPath.StartsWith(
                prefix,
                StringComparison
                    .OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Proton Bus package path escaped the selected output directory.");
        }

        return fullPath;
    }

    private static void EnsureDirectoryForFile(
        string path)
    {
        var directory =
            Path.GetDirectoryName(
                path);

        if (
            !string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }
    }

    private static string CombineRelative(
        string left,
        string right) =>
        left.TrimEnd(
            '/',
            '\\') +
        "/" +
        right.TrimStart(
            '/',
            '\\');
}
