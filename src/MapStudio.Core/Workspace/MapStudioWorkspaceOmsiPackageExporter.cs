using System.Text;
using System.Text.Json;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Workspace;

public sealed record MapStudioOmsiPackageExportResult(
    string PackageRoot,
    string MapPath,
    int CopiedFiles,
    IReadOnlyList<string> IncludedRoots);

public sealed class MapStudioWorkspaceOmsiPackageExporter
{
    public async Task<MapStudioOmsiPackageExportResult>
        ExportAsync(
            string workspaceRoot,
            OmsiMapDescriptor map,
            string destinationDirectory,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            workspaceRoot);
        ArgumentNullException.ThrowIfNull(
            map);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            destinationDirectory);

        var workspace =
            Path.GetFullPath(
                    workspaceRoot)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        var mapsRoot =
            Path.Combine(
                    workspace,
                    "maps")
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        var sourceMap =
            Path.GetFullPath(
                map.DirectoryPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        if (
            !sourceMap.StartsWith(
                mapsRoot +
                    Path.DirectorySeparatorChar,
                StringComparison
                    .OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "workspaceExportMapOutsideWorkspace");
        }

        var destination =
            Path.GetFullPath(
                    destinationDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        Directory.CreateDirectory(
            destination);

        var packageName =
            SanitizeName(
                map.DirectoryName) +
            "-omsi-package";

        var packageRoot =
            GetUniqueDirectoryPath(
                Path.Combine(
                    destination,
                    packageName));

        var workspacePrefix =
            workspace +
            Path.DirectorySeparatorChar;

        if (
            packageRoot.StartsWith(
                workspacePrefix,
                StringComparison
                    .OrdinalIgnoreCase) ||
            string.Equals(
                packageRoot,
                workspace,
                StringComparison
                    .OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "workspaceExportDestinationInsideWorkspace");
        }

        Directory.CreateDirectory(
            packageRoot);

        var copied =
            0;

        var included =
            new List<string>();

        var mapTarget =
            Path.Combine(
                packageRoot,
                "maps",
                map.DirectoryName);

        copied +=
            await CopyDirectoryAsync(
                    sourceMap,
                    mapTarget,
                    cancellationToken)
                .ConfigureAwait(false);

        included.Add(
            "maps/" +
            map.DirectoryName);

        foreach (
            var rootName in
                new[]
                {
                    "Sceneryobjects",
                    "Splines"
                })
        {
            var source =
                Path.Combine(
                    workspace,
                    rootName);

            if (!Directory.Exists(
                    source))
            {
                continue;
            }

            copied +=
                await CopyDirectoryAsync(
                        source,
                        Path.Combine(
                            packageRoot,
                            rootName),
                        cancellationToken)
                    .ConfigureAwait(false);

            included.Add(
                rootName);
        }

        var textureSource =
            Path.Combine(
                workspace,
                "Texture");

        if (Directory.Exists(
                textureSource))
        {
            var textureTarget =
                Path.Combine(
                    packageRoot,
                    "Texture");

            Directory.CreateDirectory(
                textureTarget);

            foreach (
                var sourceFile in
                    Directory.EnumerateFiles(
                        textureSource,
                        "*",
                        SearchOption
                            .AllDirectories))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var relative =
                    Path.GetRelativePath(
                        textureSource,
                        sourceFile);

                if (
                    ShouldExcludeEnvironmentTexture(
                        relative))
                {
                    continue;
                }

                var target =
                    Path.Combine(
                        textureTarget,
                        relative);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        target)!);

                await CopyFileAsync(
                        sourceFile,
                        target,
                        cancellationToken)
                    .ConfigureAwait(false);

                copied++;
            }

            if (
                Directory.EnumerateFiles(
                    textureTarget,
                    "*",
                    SearchOption
                        .AllDirectories)
                .Any())
            {
                included.Add(
                    "Texture");
            }
        }

        var metadataDirectory =
            Path.Combine(
                packageRoot,
                ".mapstudio-export");

        Directory.CreateDirectory(
            metadataDirectory);

        var manifest =
            JsonSerializer.Serialize(
                new
                {
                    version = 1,
                    type =
                        "omsi-content-package",
                    map =
                        map.DirectoryName,
                    displayName =
                        map.DisplayName,
                    copiedFiles =
                        copied,
                    includedRoots =
                        included,
                    createdAtUtc =
                        DateTimeOffset.UtcNow
                },
                new JsonSerializerOptions
                {
                    WriteIndented =
                        true
                });

        await File.WriteAllTextAsync(
                Path.Combine(
                    metadataDirectory,
                    "package.json"),
                manifest,
                Encoding.UTF8,
                cancellationToken)
            .ConfigureAwait(false);

        await File.WriteAllTextAsync(
                Path.Combine(
                    packageRoot,
                    "README-MAP-STUDIO.txt"),
                BuildReadme(
                    map,
                    included),
                Encoding.UTF8,
                cancellationToken)
            .ConfigureAwait(false);

        return new MapStudioOmsiPackageExportResult(
            packageRoot,
            mapTarget,
            copied,
            included);
    }

    private static async Task<int>
        CopyDirectoryAsync(
            string sourceDirectory,
            string targetDirectory,
            CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(
            targetDirectory);

        var copied =
            0;

        foreach (
            var sourceFile in
                Directory.EnumerateFiles(
                    sourceDirectory,
                    "*",
                    SearchOption
                        .AllDirectories))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var relative =
                Path.GetRelativePath(
                    sourceDirectory,
                    sourceFile);

            var target =
                Path.Combine(
                    targetDirectory,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    target)!);

            await CopyFileAsync(
                    sourceFile,
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

            copied++;
        }

        return copied;
    }

    private static async Task CopyFileAsync(
        string source,
        string target,
        CancellationToken cancellationToken)
    {
        await using var input =
            new FileStream(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 *
                    128,
                FileOptions
                    .Asynchronous |
                FileOptions
                    .SequentialScan);

        await using var output =
            new FileStream(
                target,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 *
                    128,
                FileOptions
                    .Asynchronous);

        await input.CopyToAsync(
                output,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool
        ShouldExcludeEnvironmentTexture(
            string relativePath)
    {
        var normalized =
            relativePath
                .Replace(
                    '\\',
                    '/')
                .TrimStart(
                    '/');

        if (
            normalized.StartsWith(
                "skybox/",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName =
            Path.GetFileName(
                normalized);

        return
            string.Equals(
                fileName,
                "himmel01.bmp",
                StringComparison
                    .OrdinalIgnoreCase) ||
            string.Equals(
                fileName,
                "himmel05.bmp",
                StringComparison
                    .OrdinalIgnoreCase);
    }

    private static string
        GetUniqueDirectoryPath(
            string preferred)
    {
        if (
            !Directory.Exists(
                preferred) &&
            !File.Exists(
                preferred))
        {
            return preferred;
        }

        for (
            var index = 2;
            index <
                10_000;
            index++)
        {
            var candidate =
                preferred +
                "-" +
                index;

            if (
                !Directory.Exists(
                    candidate) &&
                !File.Exists(
                    candidate))
            {
                return candidate;
            }
        }

        throw new IOException(
            "workspaceExportUniquePathUnavailable");
    }

    private static string SanitizeName(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var result =
            new string(
                value
                    .Trim()
                    .Select(
                        character =>
                            invalid.Contains(
                                character)
                                ? '_'
                                : character)
                    .ToArray());

        return string.IsNullOrWhiteSpace(
                result)
            ? "MapStudioMap"
            : result;
    }

    private static string BuildReadme(
        OmsiMapDescriptor map,
        IReadOnlyList<string> included)
    {
        var roots =
            string.Join(
                ", ",
                included);

        return
            "OMSI Map Studio - OMSI content package\r\n" +
            "=========================================\r\n\r\n" +
            "Map: " +
            map.DisplayName +
            "\r\n" +
            "Folder: " +
            map.DirectoryName +
            "\r\n" +
            "Included roots: " +
            roots +
            "\r\n\r\n" +
            "PT-BR: Este pacote não altera sua instalação do OMSI automaticamente. " +
            "Faça backup e copie as pastas deste pacote para a raiz do OMSI 2 quando quiser testar o mapa. " +
            "O céu próprio do Workspace não é exportado para evitar sobrescrever o ambiente global do jogo.\r\n\r\n" +
            "EN: This package does not modify your OMSI installation automatically. " +
            "Back up your installation and copy these package folders into the OMSI 2 root when you want to test the map. " +
            "Workspace sky textures are intentionally excluded to avoid replacing the game's global environment.\r\n";
    }
}
