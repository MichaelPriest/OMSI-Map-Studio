using System.Text;
using System.Text.Json;
using MapStudio.Core.AI;
using MapStudio.Core.Omsi.Buildings;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Props;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.Workspace;

public sealed record MapStudioWorkspaceInfo(
    string RootPath,
    string MapsPath,
    string SceneryObjectsPath,
    string SplinesPath,
    string TexturePath,
    string TemplatePath,
    bool StarterAssetsCreated);

public sealed record MapStudioWorkspaceImportResult(
    string SourcePath,
    int CopiedFiles,
    IReadOnlyList<string> DestinationDirectories);

public sealed record MapStudioWorkspaceMapImportResult(
    string SourcePath,
    string MapDirectory,
    string DirectoryName,
    string DisplayName,
    int CopiedFiles);

public sealed class MapStudioWorkspaceBootstrapper
{
    public static string GetDefaultWorkspacePath()
    {
        var documents =
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .MyDocuments);

        if (string.IsNullOrWhiteSpace(
                documents))
        {
            documents =
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData);
        }

        return Path.Combine(
            documents,
            "OMSI Map Studio",
            "Workspace");
    }

    public async Task<MapStudioWorkspaceInfo>
        EnsureAsync(
            string rootPath,
            bool seedStarterAssets = true,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootPath);

        var root =
            Path.GetFullPath(
                rootPath);

        var maps =
            Path.Combine(
                root,
                "maps");

        var scenery =
            Path.Combine(
                root,
                "Sceneryobjects");

        var splines =
            Path.Combine(
                root,
                "Splines");

        var texture =
            Path.Combine(
                root,
                "Texture");

        var template =
            Path.Combine(
                root,
                "template",
                "NewMap");

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(maps);
        Directory.CreateDirectory(scenery);
        Directory.CreateDirectory(splines);
        Directory.CreateDirectory(texture);
        Directory.CreateDirectory(template);
        Directory.CreateDirectory(
            Path.Combine(
                root,
                ".mapstudio"));

        await new MapStudioStarterTerrainTextureGenerator()
            .EnsureAsync(
                root,
                cancellationToken)
            .ConfigureAwait(false);

        await EnsureTemplateAsync(
                template,
                cancellationToken)
            .ConfigureAwait(false);

        var starterCreated =
            false;

        if (seedStarterAssets)
        {
            starterCreated =
                await EnsureStarterAssetsAsync(
                        root,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        await WriteWorkspaceManifestAsync(
                root,
                cancellationToken)
            .ConfigureAwait(false);

        return new MapStudioWorkspaceInfo(
            root,
            maps,
            scenery,
            splines,
            texture,
            template,
            starterCreated);
    }

    public async Task<string>
        CreateBlankMapAsync(
            string rootPath,
            string directoryName,
            string displayName,
            CancellationToken cancellationToken =
                default)
    {
        directoryName =
            directoryName?.Trim() ??
            string.Empty;

        displayName =
            displayName?.Trim() ??
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                directoryName) ||
            string.IsNullOrWhiteSpace(
                displayName) ||
            directoryName.Length >
                80 ||
            displayName.Length >
                120 ||
            directoryName is
                "." or ".." ||
            !string.Equals(
                Path.GetFileName(
                    directoryName),
                directoryName,
                StringComparison.Ordinal) ||
            directoryName.IndexOfAny(
                Path.GetInvalidFileNameChars()) >=
                0)
        {
            throw new InvalidDataException(
                "invalidWorkspaceMapRequest");
        }

        var info =
            await EnsureAsync(
                    rootPath,
                    seedStarterAssets:
                        false,
                    cancellationToken)
                .ConfigureAwait(false);

        var target =
            Path.GetFullPath(
                Path.Combine(
                    info.MapsPath,
                    directoryName));

        var mapsPrefix =
            info.MapsPath
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (
            !target.StartsWith(
                mapsPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "invalidWorkspaceMapRequest");
        }

        if (
            Directory.Exists(
                target) ||
            File.Exists(
                target))
        {
            throw new IOException(
                "workspaceMapAlreadyExists");
        }

        CopyDirectory(
            info.TemplatePath,
            target,
            overwrite:
                false);

        var globalPath =
            Path.Combine(
                target,
                "global.cfg");

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    globalPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var lines =
            document.Lines
                .ToList();

        SetSimpleSectionValue(
            lines,
            "name",
            displayName);

        SetSimpleSectionValue(
            lines,
            "friendlyname",
            displayName);

        var patched =
            new OmsiConfigDocument(
                lines,
                Array.Empty<
                    OmsiConfigSection>(),
                document.NewLine,
                document
                    .HasTrailingNewLine,
                document
                    .TextEncoding,
                document
                    .HasByteOrderMark);

        await File.WriteAllBytesAsync(
                globalPath,
                patched.ToBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var metadataDirectory =
            Path.Combine(
                target,
                ".mapstudio");

        Directory.CreateDirectory(
            metadataDirectory);

        var projectJson =
            JsonSerializer.Serialize(
                new
                {
                    version = 1,
                    format =
                        "mapstudio-omsi-compatible",
                    displayName,
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
                    "project.json"),
                projectJson,
                Encoding.UTF8,
                cancellationToken)
            .ConfigureAwait(false);

        return target;
    }

    public async Task<MapStudioWorkspaceMapImportResult>
        ImportMapFolderAsync(
            string workspaceRoot,
            string sourcePath,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            workspaceRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourcePath);

        var info =
            await EnsureAsync(
                    workspaceRoot,
                    seedStarterAssets:
                        false,
                    cancellationToken)
                .ConfigureAwait(false);

        var source =
            Path.GetFullPath(
                sourcePath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException(
                source);
        }

        var globalPath =
            Path.Combine(
                source,
                "global.cfg");

        if (!File.Exists(globalPath))
        {
            throw new InvalidDataException(
                "workspaceMapImportGlobalMissing");
        }

        var workspaceMaps =
            Path.GetFullPath(
                info.MapsPath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var mapsPrefix =
            workspaceMaps +
            Path.DirectorySeparatorChar;

        if (
            string.Equals(
                source,
                workspaceMaps,
                StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith(
                mapsPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "workspaceMapImportSourceAlreadyInWorkspace");
        }

        await OmsiMapCatalog
            .OpenMapAsync(
                source,
                cancellationToken)
            .ConfigureAwait(false);

        var preferredName =
            SanitizeFolderName(
                Path.GetFileName(
                    source));

        var destination =
            CreateUniqueDirectoryPath(
                Path.Combine(
                    info.MapsPath,
                    preferredName));

        var copiedFiles =
            0;

        try
        {
            copiedFiles =
                CopyDirectory(
                    source,
                    destination,
                    overwrite:
                        false);

            var imported =
                await OmsiMapCatalog
                    .OpenMapAsync(
                        destination,
                        cancellationToken)
                    .ConfigureAwait(false);

            var metadataDirectory =
                Path.Combine(
                    destination,
                    ".mapstudio");

            Directory.CreateDirectory(
                metadataDirectory);

            var importJson =
                JsonSerializer.Serialize(
                    new
                    {
                        version = 1,
                        importedFrom =
                            source,
                        importedAtUtc =
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
                        "import.json"),
                    importJson,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            return new MapStudioWorkspaceMapImportResult(
                source,
                destination,
                imported.DirectoryName,
                imported.DisplayName,
                copiedFiles);
        }
        catch
        {
            try
            {
                if (Directory.Exists(
                        destination))
                {
                    Directory.Delete(
                        destination,
                        recursive:
                            true);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    public async Task<MapStudioWorkspaceImportResult>
        ImportAssetFolderAsync(
            string workspaceRoot,
            string sourcePath,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            workspaceRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourcePath);

        var info =
            await EnsureAsync(
                    workspaceRoot,
                    seedStarterAssets:
                        false,
                    cancellationToken)
                .ConfigureAwait(false);

        var source =
            Path.GetFullPath(
                sourcePath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException(
                source);
        }

        var root =
            info.RootPath
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        var rootPrefix =
            root +
            Path.DirectorySeparatorChar;

        if (
            string.Equals(
                source,
                root,
                StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "assetImportSourceInsideWorkspace");
        }

        var sourceName =
            SanitizeFolderName(
                Path.GetFileName(
                    source));

        var destinations =
            new List<string>();

        var copied =
            0;

        var canonical =
            new[]
            {
                (
                    Name:
                        "Sceneryobjects",
                    Destination:
                        info.SceneryObjectsPath
                ),
                (
                    Name:
                        "Splines",
                    Destination:
                        info.SplinesPath
                ),
                (
                    Name:
                        "Texture",
                    Destination:
                        info.TexturePath
                )
            };

        var usedCanonical =
            false;

        foreach (
            var entry in
                canonical)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var canonicalSource =
                FindDirectoryIgnoreCase(
                    source,
                    entry.Name);

            if (canonicalSource is null)
            {
                continue;
            }

            usedCanonical =
                true;

            var destination =
                CreateUniqueDirectoryPath(
                    Path.Combine(
                        entry.Destination,
                        "Imported",
                        sourceName));

            copied +=
                CopyDirectory(
                    canonicalSource,
                    destination,
                    overwrite:
                        false);

            destinations.Add(
                destination);
        }

        if (!usedCanonical)
        {
            var files =
                EnumerateFilesSafe(
                    source,
                    cancellationToken)
                .ToArray();

            var hasSco =
                files.Any(
                    path =>
                        string.Equals(
                            Path.GetExtension(
                                path),
                            ".sco",
                            StringComparison.OrdinalIgnoreCase));

            var hasSli =
                files.Any(
                    path =>
                        string.Equals(
                            Path.GetExtension(
                                path),
                            ".sli",
                            StringComparison.OrdinalIgnoreCase));

            if (!hasSco && !hasSli)
            {
                throw new InvalidDataException(
                    "assetImportFolderHasNoScoOrSli");
            }

            if (hasSco)
            {
                var destination =
                    CreateUniqueDirectoryPath(
                        Path.Combine(
                            info.SceneryObjectsPath,
                            "Imported",
                            sourceName));

                copied +=
                    CopyDirectory(
                        source,
                        destination,
                        overwrite:
                            false);

                destinations.Add(
                    destination);
            }

            if (hasSli)
            {
                var destination =
                    CreateUniqueDirectoryPath(
                        Path.Combine(
                            info.SplinesPath,
                            "Imported",
                            sourceName));

                copied +=
                    CopyDirectory(
                        source,
                        destination,
                        overwrite:
                            false);

                destinations.Add(
                    destination);
            }
        }

        return new MapStudioWorkspaceImportResult(
            source,
            copied,
            destinations);
    }

    private static async Task
        EnsureTemplateAsync(
            string templateDirectory,
            CancellationToken cancellationToken)
    {
        var globalPath =
            Path.Combine(
                templateDirectory,
                "global.cfg");

        var tilePath =
            Path.Combine(
                templateDirectory,
                "tile_0_0.map");

        var terrainPath =
            tilePath +
            ".terrain";

        if (!File.Exists(
                globalPath))
        {
            var global =
                "[name]\r\n" +
                "Novo mapa Map Studio\r\n\r\n" +
                "[friendlyname]\r\n" +
                "Novo mapa Map Studio\r\n\r\n" +
                "[map]\r\n" +
                "0\r\n" +
                "0\r\n" +
                "tile_0_0.map\r\n";

            await File.WriteAllTextAsync(
                    globalPath,
                    global,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!File.Exists(
                tilePath))
        {
            await File.WriteAllTextAsync(
                    tilePath,
                    BuildStarterTileConfig(),
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var tileSource =
                await File.ReadAllTextAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (
                !tileSource.Contains(
                    "[groundtex]",
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                tileSource =
                    tileSource
                        .TrimEnd(
                            '\r',
                            '\n') +
                    "\r\n\r\n" +
                    BuildGroundTextureConfig();

                await File.WriteAllTextAsync(
                        tilePath,
                        tileSource,
                        Encoding.UTF8,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (!File.Exists(
                terrainPath))
        {
            var heights =
                new float[
                    61 *
                    61];

            var bytes =
                OmsiTerrainWriter.Write(
                    new OmsiTerrainGrid(
                        60,
                        heights));

            await File.WriteAllBytesAsync(
                    terrainPath,
                    bytes,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string BuildStarterTileConfig() =>
        "[terrain]\r\n\r\n" +
        BuildGroundTextureConfig();

    private static string BuildGroundTextureConfig() =>
        "[groundtex]\r\n" +
        "Texture\\mapstudio_grass.bmp\r\n" +
        "Texture\\mapstudio_grass_detail.bmp\r\n" +
        "0\r\n" +
        "1\r\n" +
        "60\r\n";

    private static async Task<bool>
        EnsureStarterAssetsAsync(
            string root,
            CancellationToken cancellationToken)
    {
        var created =
            false;

        var skyCreated =
            await new MapStudioStarterSkyGenerator()
                .EnsureAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        created =
            created ||
            skyCreated;

        var roadKitDirectory =
            Path.Combine(
                root,
                "Splines",
                MapStudioRoadKitGenerator
                    .PackFolderName);

        if (!Directory.Exists(
                roadKitDirectory))
        {
            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        var tunnelDirectory =
            Path.Combine(
                root,
                "Splines",
                MapStudioTunnelSplineGenerator
                    .PackFolderName);

        if (
            !Directory.Exists(
                tunnelDirectory) ||
            !Directory
                .EnumerateFiles(
                    tunnelDirectory,
                    "*.sli",
                    SearchOption
                        .TopDirectoryOnly)
                .Any())
        {
            await new MapStudioTunnelSplineGenerator()
                .GenerateAsync(
                    root,
                    new MapStudioTunnelSpec(
                        "Map Studio Tunnel",
                        2,
                        3.5,
                        5.2,
                        0.75,
                        10),
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        var bridgeDirectory =
            Path.Combine(
                root,
                "Splines",
                MapStudioBridgeSplineGenerator
                    .PackFolderName);

        if (
            !Directory.Exists(
                bridgeDirectory) ||
            !Directory
                .EnumerateFiles(
                    bridgeDirectory,
                    "*.sli",
                    SearchOption
                        .TopDirectoryOnly)
                .Any())
        {
            await new MapStudioBridgeSplineGenerator()
                .GenerateAsync(
                    root,
                    new MapStudioBridgeSpec(
                        "Starter Bridge 2 Lane",
                        2,
                        3.5,
                        1.5,
                        0.55),
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        var junctionDirectory =
            Path.Combine(
                root,
                "Sceneryobjects",
                MapStudioJunctionAssetGenerator
                    .RootFolderName,
                "Starter_4Way");

        if (
            !Directory.Exists(
                junctionDirectory) ||
            !File.Exists(
                Path.Combine(
                    junctionDirectory,
                    "Texture",
                    "ms_junction_asphalt.bmp")))
        {
            await new MapStudioJunctionAssetGenerator()
                .GenerateAsync(
                    root,
                    new MapStudioJunctionSpec(
                        "Starter 4Way",
                        [
                            new MapStudioJunctionArm(
                                0,
                                7,
                                2),
                            new MapStudioJunctionArm(
                                90,
                                7,
                                2),
                            new MapStudioJunctionArm(
                                180,
                                7,
                                2),
                            new MapStudioJunctionArm(
                                270,
                                7,
                                2)
                        ]),
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        var vegetationCreated =
            await new MapStudioStarterVegetationGenerator()
                .EnsureAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        created =
            created ||
            vegetationCreated;

        var propsCreated =
            await new MapStudioStarterPropGenerator()
                .EnsureAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        created =
            created ||
            propsCreated;

        var trafficCreated =
            await new MapStudioStarterTrafficGenerator()
                .EnsureAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        created =
            created ||
            trafficCreated;

        var buildingDirectory =
            Path.Combine(
                root,
                "Sceneryobjects",
                MapStudioBuildingAssetGenerator
                    .RootFolderName,
                "Starter_House");

        if (
            !Directory.Exists(
                buildingDirectory) ||
            !File.Exists(
                Path.Combine(
                    buildingDirectory,
                    "Texture",
                    "ms_building_facade.bmp")) ||
            !File.Exists(
                Path.Combine(
                    buildingDirectory,
                    "Texture",
                    "ms_building_roof.bmp")))
        {
            await new MapStudioBuildingAssetGenerator()
                .GenerateAsync(
                    root,
                    new MapStudioBuildingSpec(
                        "Starter House",
                        10,
                        8,
                        6,
                        2,
                        MapStudioBuildingRoofType
                            .Gable,
                        RoofHeightMeters:
                            2,
                        WindowsPerFloor:
                            3,
                        DoorCount:
                            1),
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        return created;
    }

    private static async Task
        WriteWorkspaceManifestAsync(
            string root,
            CancellationToken cancellationToken)
    {
        var metadata =
            Path.Combine(
                root,
                ".mapstudio");

        Directory.CreateDirectory(
            metadata);

        var path =
            Path.Combine(
                metadata,
                "workspace.json");

        if (File.Exists(path))
        {
            return;
        }

        var json =
            JsonSerializer.Serialize(
                new
                {
                    version = 1,
                    type =
                        "standalone-workspace",
                    format =
                        "omsi-compatible-content-root",
                    createdAtUtc =
                        DateTimeOffset.UtcNow
                },
                new JsonSerializerOptions
                {
                    WriteIndented =
                        true
                });

        await File.WriteAllTextAsync(
                path,
                json,
                Encoding.UTF8,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string?
        FindDirectoryIgnoreCase(
            string root,
            string name) =>
        Directory
            .EnumerateDirectories(
                root,
                "*",
                SearchOption
                    .TopDirectoryOnly)
            .FirstOrDefault(
                path =>
                    string.Equals(
                        Path.GetFileName(
                            path),
                        name,
                        StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string>
        EnumerateFilesSafe(
            string root,
            CancellationToken cancellationToken)
    {
        var pending =
            new Stack<string>();

        pending.Push(
            root);

        while (
            pending.Count >
                0)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var current =
                pending.Pop();

            string[] files;
            string[] directories;

            try
            {
                files =
                    Directory.GetFiles(
                        current);

                directories =
                    Directory.GetDirectories(
                        current);
            }
            catch (
                Exception exception)
                when (
                    exception is
                        IOException or
                        UnauthorizedAccessException or
                        DirectoryNotFoundException)
            {
                continue;
            }

            foreach (
                var file in files)
            {
                yield return file;
            }

            foreach (
                var directory in directories)
            {
                try
                {
                    if (
                        (
                            File.GetAttributes(
                                directory) &
                            FileAttributes
                                .ReparsePoint
                        ) != 0)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                pending.Push(
                    directory);
            }
        }
    }

    private static int CopyDirectory(
        string source,
        string destination,
        bool overwrite)
    {
        Directory.CreateDirectory(
            destination);

        var copied =
            0;

        foreach (
            var directory in
                Directory.EnumerateDirectories(
                    source,
                    "*",
                    SearchOption.AllDirectories))
        {
            var relative =
                Path.GetRelativePath(
                    source,
                    directory);

            Directory.CreateDirectory(
                Path.Combine(
                    destination,
                    relative));
        }

        foreach (
            var file in
                Directory.EnumerateFiles(
                    source,
                    "*",
                    SearchOption.AllDirectories))
        {
            var relative =
                Path.GetRelativePath(
                    source,
                    file);

            var target =
                Path.Combine(
                    destination,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    target)!);

            File.Copy(
                file,
                target,
                overwrite);

            copied++;
        }

        return copied;
    }

    private static string
        CreateUniqueDirectoryPath(
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
            var suffix = 2;
            suffix < 10_000;
            suffix++)
        {
            var candidate =
                preferred +
                "_" +
                suffix;

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
            "assetImportDestinationExhausted");
    }

    private static string SanitizeFolderName(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var sanitized =
            new string(
                value
                    .Select(
                        character =>
                            invalid.Contains(
                                character) ||
                            char.IsControl(
                                character)
                                ? '_'
                                : character)
                    .ToArray())
                .Trim();

        return string.IsNullOrWhiteSpace(
                sanitized)
            ? "Assets"
            : sanitized;
    }

    private static void SetSimpleSectionValue(
        List<string> lines,
        string keyword,
        string value)
    {
        var marker =
            "[" +
            keyword +
            "]";

        for (
            var index = 0;
            index < lines.Count;
            index++)
        {
            if (!string.Equals(
                    lines[index].Trim(),
                    marker,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var valueIndex =
                index +
                1;

            while (
                valueIndex <
                    lines.Count &&
                string.IsNullOrWhiteSpace(
                    lines[valueIndex]))
            {
                valueIndex++;
            }

            if (
                valueIndex <
                    lines.Count &&
                !lines[valueIndex]
                    .TrimStart()
                    .StartsWith(
                        "[",
                        StringComparison.Ordinal))
            {
                lines[valueIndex] =
                    value;

                return;
            }

            lines.Insert(
                index +
                1,
                value);

            return;
        }

        if (
            lines.Count >
                0 &&
            lines[^1].Length !=
                0)
        {
            lines.Add(
                string.Empty);
        }

        lines.Add(
            marker);

        lines.Add(
            value);
    }
}
