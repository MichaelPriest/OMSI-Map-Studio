using System.Collections.Concurrent;
using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using MapStudio.Core.IO;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MapStudio.Desktop;

public partial class MainWindow : Window
{
    private const int MaxConcurrentTileReads = 4;
    private const int MaxTileStreamRadius = 2;

    private readonly OmsiMapCatalog _mapCatalog = new();
    private readonly OmsiTileReader _tileReader = new();
    private readonly OmsiSceneryObjectReader _sceneryObjectReader = new();
    private readonly OmsiO3dHeaderReader _o3dHeaderReader = new();
    private readonly OmsiO3dStructureReader _o3dStructureReader = new();
    private readonly OmsiO3dGeometryReader _o3dGeometryReader = new();
    private readonly OmsiSplineDefinitionReader _splineDefinitionReader = new();
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private IReadOnlyDictionary<string, OmsiMapDescriptor> _knownMaps =
        new Dictionary<string, OmsiMapDescriptor>(
            StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<
        string,
        byte>
        _knownSceneryObjectPaths =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<
        string,
        Task<OmsiTileContent>>
        _tileContentCache =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<
        string,
        byte>
        _knownSplinePaths =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<
        string,
        Task<OmsiSplineDefinition>>
        _splineDefinitionCache =
            new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<SceneryLibraryEntry>?
        _sceneryLibraryCache;

    private string? _omsiRootPath;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await EditorWebView.EnsureCoreWebView2Async();

        EditorWebView.CoreWebView2.WebMessageReceived +=
            OnWebMessageReceived;

        var devUrl = Environment.GetEnvironmentVariable(
            "MAPSTUDIO_DEV_URL");

        if (Uri.TryCreate(
                devUrl,
                UriKind.Absolute,
                out var developmentUri))
        {
            EditorWebView.Source = developmentUri;
            return;
        }

        var uiDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "ui");

        var indexPath = Path.Combine(
            uiDirectory,
            "index.html");

        if (!File.Exists(indexPath))
        {
            EditorWebView.NavigateToString(
                "<html><body style='font-family:Segoe UI;background:#101318;color:#fff;padding:32px'>" +
                "<h1>OMSI Map Studio</h1><p>UI build not found.</p>" +
                "<p>Run npm run build in src/MapStudio.UI or set MAPSTUDIO_DEV_URL.</p>" +
                "</body></html>");
            return;
        }

        EditorWebView.CoreWebView2
            .SetVirtualHostNameToFolderMapping(
                "app.mapstudio",
                uiDirectory,
                CoreWebView2HostResourceAccessKind.DenyCors);

        EditorWebView.Source = new Uri(
            "https://app.mapstudio/index.html");
    }

    private async void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message =
                JsonDocument.Parse(
                    e.WebMessageAsJson);

            if (!message.RootElement.TryGetProperty(
                    "type",
                    out var typeElement))
            {
                return;
            }

            switch (typeElement.GetString())
            {
                case "selectOmsiRoot":
                    await SelectOmsiRootAsync();
                    break;

                case "selectMap":
                    await SelectMapAsync();
                    break;

                case "loadSceneryLibrary":
                    await LoadSceneryLibraryAsync();
                    break;

                case "insertObject":
                    if (
                        TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var insertDirectoryName) &&
                        TryReadString(
                            message.RootElement,
                            "sceneryObjectPath",
                            out var insertSceneryObjectPath) &&
                        TryReadInt32(
                            message.RootElement,
                            "tileX",
                            out var insertTileX) &&
                        TryReadInt32(
                            message.RootElement,
                            "tileY",
                            out var insertTileY) &&
                        TryReadDouble(
                            message.RootElement,
                            "x",
                            out var insertX) &&
                        TryReadDouble(
                            message.RootElement,
                            "y",
                            out var insertY) &&
                        TryReadDouble(
                            message.RootElement,
                            "z",
                            out var insertZ) &&
                        TryReadDouble(
                            message.RootElement,
                            "rotation",
                            out var insertRotation) &&
                        TryReadDouble(
                            message.RootElement,
                            "pitch",
                            out var insertPitch) &&
                        TryReadDouble(
                            message.RootElement,
                            "bank",
                            out var insertBank))
                    {
                        await InsertObjectAsync(
                            insertDirectoryName,
                            insertSceneryObjectPath,
                            insertTileX,
                            insertTileY,
                            insertX,
                            insertY,
                            insertZ,
                            insertRotation,
                            insertPitch,
                            insertBank);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "deleteObject":
                    if (
                        TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var deleteDirectoryName) &&
                        TryReadInt32(
                            message.RootElement,
                            "tileX",
                            out var deleteTileX) &&
                        TryReadInt32(
                            message.RootElement,
                            "tileY",
                            out var deleteTileY) &&
                        TryReadInt32(
                            message.RootElement,
                            "sourceSectionOrdinal",
                            out var deleteSourceSectionOrdinal) &&
                        TryReadString(
                            message.RootElement,
                            "sceneryObjectPath",
                            out var deleteSceneryObjectPath) &&
                        TryReadInt32(
                            message.RootElement,
                            "objectId",
                            out var deleteObjectId))
                    {
                        await DeleteObjectAsync(
                            deleteDirectoryName,
                            deleteTileX,
                            deleteTileY,
                            deleteSourceSectionOrdinal,
                            deleteSceneryObjectPath,
                            deleteObjectId);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "saveObjectTransforms":
                    if (
                        TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var saveDirectoryName) &&
                        TryReadObjectTransformRequests(
                            message.RootElement,
                            out var transformRequests))
                    {
                        await SaveObjectTransformsAsync(
                            saveDirectoryName,
                            transformRequests);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "loadMapFull":
                    if (TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var fullMapDirectoryName))
                    {
                        await LoadMapFullAsync(
                            fullMapDirectoryName);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "loadMapRegion":
                    if (TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var directoryName) &&
                        TryReadInt32(
                            message.RootElement,
                            "centerX",
                            out var centerX) &&
                        TryReadInt32(
                            message.RootElement,
                            "centerY",
                            out var centerY) &&
                        TryReadInt32(
                            message.RootElement,
                            "radius",
                            out var radius))
                    {
                        await LoadMapRegionAsync(
                            directoryName,
                            centerX,
                            centerY,
                            radius);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "loadSplineProfile":
                    if (TryReadString(
                            message.RootElement,
                            "splinePath",
                            out var splinePath))
                    {
                        await LoadSplineProfileAsync(
                            splinePath);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "loadSceneryObjectMetadata":
                    if (TryReadString(
                            message.RootElement,
                            "sceneryObjectPath",
                            out var sceneryObjectPath))
                    {
                        await LoadSceneryObjectMetadataAsync(
                            sceneryObjectPath);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "loadSceneryObjectGeometry":
                    if (TryReadString(
                            message.RootElement,
                            "sceneryObjectPath",
                            out var geometryObjectPath))
                    {
                        await LoadSceneryObjectGeometryAsync(
                            geometryObjectPath);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;
            }
        }
        catch (JsonException)
        {
            PostInvalidMessage();
        }
        catch (Exception exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unexpectedHostError",
                detail = exception.Message
            });
        }
    }

    private Task SelectOmsiRootAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecione a pasta raiz do OMSI 2",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            PostMessage(new
            {
                type = "selectionCancelled",
                target = "omsi"
            });

            return Task.CompletedTask;
        }

        var rootPath =
            Path.GetFullPath(
                dialog.FolderName);

        var mapsPath =
            Path.Combine(
                rootPath,
                "maps");

        if (!Directory.Exists(mapsPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidOmsiRoot",
                detail = rootPath
            });

            return Task.CompletedTask;
        }

        _omsiRootPath = rootPath;

        _knownMaps =
            new Dictionary<string, OmsiMapDescriptor>(
                StringComparer.OrdinalIgnoreCase);

        _knownSceneryObjectPaths.Clear();
        _knownSplinePaths.Clear();
        _tileContentCache.Clear();
        _splineDefinitionCache.Clear();
        _sceneryLibraryCache = null;

        PostMessage(new
        {
            type = "omsiRootSelected",
            rootPath
        });

        return Task.CompletedTask;
    }

    private async Task SelectMapAsync()
    {
        if (_omsiRootPath is null)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "omsiRootRequired"
            });

            return;
        }

        var mapsRoot =
            Path.GetFullPath(
                Path.Combine(
                    _omsiRootPath,
                    "maps"))
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var dialog = new OpenFolderDialog
        {
            Title = "Selecione a pasta do mapa dentro de OMSI 2\\maps",
            Multiselect = false,
            InitialDirectory = mapsRoot
        };

        if (dialog.ShowDialog(this) != true)
        {
            PostMessage(new
            {
                type = "selectionCancelled",
                target = "map"
            });

            return;
        }

        var selectedDirectory =
            Path.GetFullPath(
                dialog.FolderName)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var requiredPrefix =
            mapsRoot +
            Path.DirectorySeparatorChar;

        if (!selectedDirectory.StartsWith(
                requiredPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "mapOutsideOmsiMaps",
                detail = selectedDirectory
            });

            return;
        }

        var globalConfigPath =
            Path.Combine(
                selectedDirectory,
                "global.cfg");

        if (!File.Exists(globalConfigPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidMapFolder",
                detail = selectedDirectory
            });

            return;
        }

        try
        {
            var map =
                await OmsiMapCatalog.OpenMapAsync(
                    selectedDirectory);

            _knownSceneryObjectPaths.Clear();
            _tileContentCache.Clear();

            _knownMaps =
                new Dictionary<string, OmsiMapDescriptor>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    [map.DirectoryName] = map
                };

            var initialTile =
                OmsiTileRegionSelector
                    .FindInitialTile(
                        map.Tiles);

            PostMessage(new
            {
                type = "mapOpened",
                initialTile = initialTile is null
                    ? null
                    : new
                    {
                        x = initialTile.X,
                        y = initialTile.Y
                    },
                map = new
                {
                    map.DirectoryName,
                    map.DisplayName,
                    map.DirectoryPath,
                    map.GlobalConfigPath,
                    map.UsesWorldCoordinates,
                    tiles = map.Tiles.Select(
                        tile => new
                        {
                            tile.X,
                            tile.Y,
                            tile.RelativeMapPath,
                            detailsLoaded = false,
                            fileExists = false,
                            objectCount = 0,
                            splineCount = 0,
                            splineAttachmentCount = 0
                        })
                }
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = selectedDirectory
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "ioError",
                detail = exception.Message
            });
        }
        catch (Exception exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "mapOpenError",
                detail = exception.Message
            });
        }
    }

    private async Task LoadSceneryLibraryAsync()
    {
        if (_omsiRootPath is null)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "omsiRootRequired"
            });

            return;
        }

        var omsiRoot =
            _omsiRootPath;

        try
        {
            if (_sceneryLibraryCache is null)
            {
                var entries =
                    await Task.Run(
                        () =>
                            ScanSceneryLibrary(
                                omsiRoot));

                if (!string.Equals(
                        _omsiRootPath,
                        omsiRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _sceneryLibraryCache =
                    entries;
            }

            PostMessage(new
            {
                type =
                    "sceneryLibraryLoaded",
                entries =
                    _sceneryLibraryCache
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = "Sceneryobjects"
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "ioError",
                detail = exception.Message
            });
        }
    }

    private IReadOnlyList<SceneryLibraryEntry>
        ScanSceneryLibrary(
            string omsiRoot)
    {
        var root =
            Path.Combine(
                omsiRoot,
                "Sceneryobjects");

        if (!Directory.Exists(root))
        {
            return
                Array.Empty<SceneryLibraryEntry>();
        }

        var options =
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing =
                    MatchCasing.CaseInsensitive,
                AttributesToSkip =
                    FileAttributes.ReparsePoint
            };

        var entries =
            new List<SceneryLibraryEntry>();

        foreach (var filePath in
            Directory.EnumerateFiles(
                root,
                "*.sco",
                options))
        {
            if (entries.Count >= 50000)
            {
                break;
            }

            var relative =
                Path.GetRelativePath(
                    root,
                    filePath)
                .Replace(
                    Path.DirectorySeparatorChar,
                    '\\');

            var declaredPath =
                "Sceneryobjects\\" +
                relative;

            entries.Add(
                new SceneryLibraryEntry(
                    declaredPath,
                    Path.GetFileName(
                        filePath)));

            _knownSceneryObjectPaths
                .TryAdd(
                    declaredPath,
                    0);
        }

        return entries
            .OrderBy(
                entry =>
                    entry.SceneryObjectPath,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task InsertObjectAsync(
        string? directoryName,
        string? sceneryObjectPath,
        int tileX,
        int tileY,
        double x,
        double y,
        double z,
        double rotation,
        double pitch,
        double bank)
    {
        if (
            string.IsNullOrWhiteSpace(
                directoryName) ||
            !_knownMaps.TryGetValue(
                directoryName,
                out var map))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownMap"
            });

            return;
        }

        if (
            string.IsNullOrWhiteSpace(
                sceneryObjectPath) ||
            !_knownSceneryObjectPaths
                .ContainsKey(
                    sceneryObjectPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownSceneryObject"
            });

            return;
        }

        if (
            _omsiRootPath is null ||
            !OmsiSceneryObjectPathResolver
                .TryResolve(
                    _omsiRootPath,
                    sceneryObjectPath,
                    out var sceneryFullPath) ||
            !File.Exists(
                sceneryFullPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidSceneryObjectPath"
            });

            return;
        }

        if (
            map.UsesWorldCoordinates)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "objectInsertionWorldCoordinatesUnsupported"
            });

            return;
        }

        var targetTile =
            map.Tiles.FirstOrDefault(
                tile =>
                    tile.X == tileX &&
                    tile.Y == tileY);

        if (targetTile is null)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownTile"
            });

            return;
        }

        if (!OmsiMapPathResolver
            .TryResolveTilePath(
                map.DirectoryPath,
                targetTile.RelativeMapPath,
                out var targetTilePath) ||
            !File.Exists(
                targetTilePath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidTilePath"
            });

            return;
        }

        try
        {
            var snapshot =
                await ReadMapInsertionSnapshotAsync(
                    map);

            var contentAnalysis =
                OmsiObjectInsertionAnalyzer
                    .Analyze(
                        snapshot.Contents,
                        sceneryObjectPath);

            var analysis =
                new OmsiObjectInsertionAnalysis(
                    Math.Max(
                        contentAnalysis.MaxUsedId,
                        snapshot.MaxUsedId),
                    contentAnalysis
                        .MatchingObjectTemplate);

            var template =
                analysis
                    .MatchingObjectTemplate;

            if (template is null)
            {
                PostMessage(new
                {
                    type = "hostError",
                    code =
                        "objectInsertTemplateUnavailable",
                    detail =
                        sceneryObjectPath
                });

                return;
            }

            var nextId =
                analysis.GetNextId();

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        targetTilePath);

            var result =
                OmsiTileObjectInserter
                    .Append(
                        document,
                        new OmsiNewPlacedObject(
                            template.HeaderValue,
                            sceneryObjectPath,
                            nextId,
                            x,
                            y,
                            z,
                            rotation,
                            pitch,
                            bank,
                            template.ExtraValues));

            var timestamp =
                DateTimeOffset.UtcNow
                    .ToString(
                        "yyyyMMdd-HHmmssfff'Z'",
                        CultureInfo.InvariantCulture);

            var relativePath =
                Path.GetRelativePath(
                    map.DirectoryPath,
                    targetTilePath);

            if (
                relativePath.StartsWith(
                    "..",
                    StringComparison.Ordinal) ||
                Path.IsPathRooted(
                    relativePath))
            {
                throw new InvalidDataException(
                    "invalidTilePath");
            }

            var backupRoot =
                Path.Combine(
                    map.DirectoryPath,
                    ".mapstudio-backups",
                    timestamp);

            var backupPath =
                Path.Combine(
                    backupRoot,
                    relativePath);

            await SafeFileTransaction
                .WriteAllAsync(
                    [
                        new PendingFileWrite(
                            targetTilePath,
                            backupPath,
                            result.Bytes)
                    ]);

            _tileContentCache
                .TryRemove(
                    targetTilePath,
                    out _);

            PostMessage(new
            {
                type = "objectInserted",
                map.DirectoryName,
                backupDirectory =
                    backupRoot,
                placedObject = new
                {
                    tileX,
                    tileY,
                    headerValue =
                        template.HeaderValue,
                    sceneryObjectPath,
                    objectId = nextId,
                    sourceSectionOrdinal =
                        result.SourceSectionOrdinal,
                    x,
                    y,
                    z,
                    rotation,
                    pitch,
                    bank
                }
            });
        }
        catch (InvalidDataException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code =
                    exception.Message ==
                        "objectIdExhausted"
                        ? "objectIdExhausted"
                        : "objectInsertError",
                detail = exception.Message
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail =
                    targetTilePath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "objectInsertError",
                detail = exception.Message
            });
        }
    }

    private async Task DeleteObjectAsync(
        string? directoryName,
        int tileX,
        int tileY,
        int sourceSectionOrdinal,
        string? sceneryObjectPath,
        int objectId)
    {
        if (
            string.IsNullOrWhiteSpace(
                directoryName) ||
            !_knownMaps.TryGetValue(
                directoryName,
                out var map))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownMap"
            });

            return;
        }

        if (string.IsNullOrWhiteSpace(
                sceneryObjectPath))
        {
            PostInvalidMessage();
            return;
        }

        var tile =
            map.Tiles.FirstOrDefault(
                candidate =>
                    candidate.X == tileX &&
                    candidate.Y == tileY);

        if (tile is null)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownTile"
            });

            return;
        }

        if (!OmsiMapPathResolver
            .TryResolveTilePath(
                map.DirectoryPath,
                tile.RelativeMapPath,
                out var tilePath) ||
            !File.Exists(tilePath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidTilePath"
            });

            return;
        }

        try
        {
            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath);

            var result =
                OmsiTileObjectDeleter
                    .Remove(
                        document,
                        sourceSectionOrdinal,
                        sceneryObjectPath,
                        objectId);

            var timestamp =
                DateTimeOffset.UtcNow
                    .ToString(
                        "yyyyMMdd-HHmmssfff'Z'",
                        CultureInfo.InvariantCulture);

            var relativePath =
                Path.GetRelativePath(
                    map.DirectoryPath,
                    tilePath);

            if (
                relativePath.StartsWith(
                    "..",
                    StringComparison.Ordinal) ||
                Path.IsPathRooted(
                    relativePath))
            {
                throw new InvalidDataException(
                    "invalidTilePath");
            }

            var backupRoot =
                Path.Combine(
                    map.DirectoryPath,
                    ".mapstudio-backups",
                    timestamp);

            var backupPath =
                Path.Combine(
                    backupRoot,
                    relativePath);

            await SafeFileTransaction
                .WriteAllAsync(
                    [
                        new PendingFileWrite(
                            tilePath,
                            backupPath,
                            result.Bytes)
                    ]);

            _tileContentCache
                .TryRemove(
                    tilePath,
                    out _);

            PostMessage(new
            {
                type = "objectDeleted",
                map.DirectoryName,
                objectId,
                deletedObjects =
                    result.DeletedObjects,
                backupDirectory =
                    backupRoot
            });
        }
        catch (InvalidDataException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "objectDeleteConflict",
                detail = exception.Message
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = tilePath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "objectDeleteError",
                detail = exception.Message
            });
        }
    }

    private async Task<MapInsertionSnapshot>
        ReadMapInsertionSnapshotAsync(
            OmsiMapDescriptor map)
    {
        using var semaphore =
            new SemaphoreSlim(
                Math.Min(
                    MaxConcurrentTileReads,
                    Math.Max(
                        1,
                        Environment
                            .ProcessorCount)));

        var tasks =
            map.Tiles.Select(
                async tile =>
                {
                    await semaphore
                        .WaitAsync();

                    try
                    {
                        if (!OmsiMapPathResolver
                            .TryResolveTilePath(
                                map.DirectoryPath,
                                tile.RelativeMapPath,
                                out var path) ||
                            !File.Exists(path))
                        {
                            return (
                                Content:
                                    OmsiTileContent
                                        .Missing,
                                MaxUsedId: 0);
                        }

                        var document =
                            await OmsiConfigParser
                                .ParseFileAsync(
                                    path);

                        return (
                            Content:
                                OmsiTileReader
                                    .ReadContent(
                                        document),
                            MaxUsedId:
                                OmsiTileElementIdScanner
                                    .FindMaxUsedId(
                                        document));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                })
                .ToArray();

        var results =
            await Task.WhenAll(
                tasks);

        return new MapInsertionSnapshot(
            results
                .Select(result =>
                    result.Content)
                .ToArray(),
            results.Length == 0
                ? 0
                : results.Max(result =>
                    result.MaxUsedId));
    }

    private async Task SaveObjectTransformsAsync(
        string? directoryName,
        IReadOnlyList<ObjectTransformRequest> requests)
    {
        if (
            string.IsNullOrWhiteSpace(
                directoryName) ||
            !_knownMaps.TryGetValue(
                directoryName,
                out var map))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownMap"
            });

            return;
        }

        if (requests.Count == 0)
        {
            PostInvalidMessage();
            return;
        }

        try
        {
            var timestamp =
                DateTimeOffset.UtcNow
                    .ToString(
                        "yyyyMMdd-HHmmssfff'Z'",
                        CultureInfo.InvariantCulture);

            var backupRoot =
                Path.Combine(
                    map.DirectoryPath,
                    ".mapstudio-backups",
                    timestamp);

            var writes =
                new List<PendingFileWrite>();

            var editedTilePaths =
                new List<string>();

            var appliedEdits = 0;

            foreach (
                var group in requests
                    .GroupBy(request =>
                        (
                            request.TileX,
                            request.TileY)))
            {
                var tile =
                    map.Tiles.FirstOrDefault(
                        candidate =>
                            candidate.X ==
                                group.Key.TileX &&
                            candidate.Y ==
                                group.Key.TileY);

                if (tile is null)
                {
                    throw new InvalidDataException(
                        "unknownTile");
                }

                if (!OmsiMapPathResolver
                    .TryResolveTilePath(
                        map.DirectoryPath,
                        tile.RelativeMapPath,
                        out var tilePath))
                {
                    throw new InvalidDataException(
                        "invalidTilePath");
                }

                var document =
                    await OmsiConfigParser
                        .ParseFileAsync(
                            tilePath);

                var edits =
                    group.Select(
                        request =>
                            new OmsiObjectTransformEdit(
                                request.SourceSectionOrdinal,
                                request.SceneryObjectPath,
                                request.ObjectId,
                                request.X,
                                request.Y,
                                request.Z,
                                request.Rotation,
                                request.Pitch,
                                request.Bank))
                    .ToArray();

                var result =
                    OmsiTileObjectEditor
                        .ApplyTransforms(
                            document,
                            edits);

                var relativePath =
                    Path.GetRelativePath(
                        map.DirectoryPath,
                        tilePath);

                if (
                    relativePath.StartsWith(
                        "..",
                        StringComparison.Ordinal) ||
                    Path.IsPathRooted(
                        relativePath))
                {
                    throw new InvalidDataException(
                        "invalidTilePath");
                }

                var backupPath =
                    Path.Combine(
                        backupRoot,
                        relativePath);

                writes.Add(
                    new PendingFileWrite(
                        tilePath,
                        backupPath,
                        result.Bytes));

                editedTilePaths.Add(
                    tilePath);

                appliedEdits +=
                    result.AppliedEdits;
            }

            await SafeFileTransaction
                .WriteAllAsync(
                    writes);

            foreach (var tilePath in
                editedTilePaths)
            {
                _tileContentCache
                    .TryRemove(
                        tilePath,
                        out _);
            }

            PostMessage(new
            {
                type =
                    "objectTransformsSaved",
                map.DirectoryName,
                editsSaved =
                    appliedEdits,
                filesSaved =
                    writes.Count,
                backupDirectory =
                    backupRoot
            });
        }
        catch (InvalidDataException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "saveConflict",
                detail = exception.Message
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = map.DirectoryPath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "saveError",
                detail = exception.Message
            });
        }
    }

    private async Task LoadMapFullAsync(
        string? directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName) ||
            !_knownMaps.TryGetValue(
                directoryName,
                out var map))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownMap"
            });

            return;
        }

        try
        {
            var requestedTiles =
                map.Tiles.ToArray();

            var totalTiles =
                requestedTiles.Length;

            PostMessage(new
            {
                type = "mapFullLoadingStarted",
                map.DirectoryName,
                totalTiles
            });

            using var semaphore =
                new SemaphoreSlim(
                    Math.Min(
                        MaxConcurrentTileReads,
                        Math.Max(
                            1,
                            Environment.ProcessorCount)));

            var completedTiles = 0;

            var tasks = requestedTiles
                .Select(async (tile, index) =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        var content =
                            OmsiMapPathResolver
                                .TryResolveTilePath(
                                    map.DirectoryPath,
                                    tile.RelativeMapPath,
                                    out var tilePath)
                            ? await ReadTileCachedAsync(
                                tilePath)
                            : OmsiTileContent.Missing;

                        var completed =
                            Interlocked.Increment(
                                ref completedTiles);

                        if (
                            completed == totalTiles ||
                            completed % 8 == 0)
                        {
                            PostMessage(new
                            {
                                type = "mapFullLoadingProgress",
                                map.DirectoryName,
                                completedTiles = completed,
                                totalTiles
                            });
                        }

                        return (
                            Index: index,
                            Tile: tile,
                            Content: content);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                })
                .ToArray();

            var loadedTiles =
                await Task.WhenAll(tasks);

            var objects = new List<object>();
            var splines = new List<object>();

            foreach (var loaded in loadedTiles
                .OrderBy(result => result.Index))
            {
                foreach (var placedObject in
                    loaded.Content.Objects)
                {
                    _knownSceneryObjectPaths.TryAdd(
                        placedObject.SceneryObjectPath,
                        0);

                    objects.Add(new
                    {
                        tileX = loaded.Tile.X,
                        tileY = loaded.Tile.Y,
                        placedObject.HeaderValue,
                        placedObject.SceneryObjectPath,
                        placedObject.ObjectId,
                        placedObject.SourceSectionOrdinal,
                        placedObject.X,
                        placedObject.Y,
                        placedObject.Z,
                        placedObject.Rotation,
                        placedObject.Pitch,
                        placedObject.Bank
                    });
                }

                foreach (var placedSpline in
                    loaded.Content.Splines)
                {
                    _knownSplinePaths.TryAdd(
                        placedSpline.SplinePath,
                        0);

                    splines.Add(new
                    {
                        tileX = loaded.Tile.X,
                        tileY = loaded.Tile.Y,
                        placedSpline.HeaderValue,
                        placedSpline.SplinePath,
                        placedSpline.SplineId,
                        placedSpline.PreviousSplineId,
                        placedSpline.NextSplineId,
                        placedSpline.X,
                        placedSpline.Z,
                        placedSpline.Y,
                        placedSpline.Rotation,
                        placedSpline.Length,
                        placedSpline.Radius,
                        placedSpline.GradientStart,
                        placedSpline.GradientEnd,
                        placedSpline.IsHeightSpline
                    });
                }
            }

            PostMessage(new
            {
                type = "mapFullLoaded",
                map.DirectoryName,
                tiles = loadedTiles
                    .OrderBy(result => result.Index)
                    .Select(loaded => new
                    {
                        loaded.Tile.X,
                        loaded.Tile.Y,
                        loaded.Tile.RelativeMapPath,
                        detailsLoaded = true,
                        fileExists =
                            loaded.Content.Summary.Exists,
                        objectCount =
                            loaded.Content.Summary.ObjectCount,
                        splineCount =
                            loaded.Content.Summary.SplineCount,
                        splineAttachmentCount =
                            loaded.Content.Summary
                                .SplineAttachmentCount
                    }),
                objects,
                splines
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = map.DirectoryPath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "ioError",
                detail = exception.Message
            });
        }
    }

    private async Task LoadMapRegionAsync(
        string? directoryName,
        int centerX,
        int centerY,
        int radius)
    {
        if (string.IsNullOrWhiteSpace(directoryName) ||
            !_knownMaps.TryGetValue(
                directoryName,
                out var map))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownMap"
            });

            return;
        }

        if (radius < 0 ||
            radius > MaxTileStreamRadius)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidTileRegion"
            });

            return;
        }

        try
        {
            var requestedTiles =
                OmsiTileRegionSelector.Select(
                    map.Tiles,
                    centerX,
                    centerY,
                    radius);

            using var semaphore =
                new SemaphoreSlim(
                    Math.Min(
                        MaxConcurrentTileReads,
                        Math.Max(
                            1,
                            Environment.ProcessorCount)));

            var tasks = requestedTiles
                .Select(async (tile, index) =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        var content =
                            OmsiMapPathResolver
                                .TryResolveTilePath(
                                    map.DirectoryPath,
                                    tile.RelativeMapPath,
                                    out var tilePath)
                            ? await ReadTileCachedAsync(
                                tilePath)
                            : OmsiTileContent.Missing;

                        return (
                            Index: index,
                            Tile: tile,
                            Content: content);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                })
                .ToArray();

            var loadedTiles =
                await Task.WhenAll(tasks);

            var objects = new List<object>();
            var splines = new List<object>();

            foreach (var loaded in loadedTiles
                .OrderBy(result => result.Index))
            {
                foreach (var placedObject in
                    loaded.Content.Objects)
                {
                    _knownSceneryObjectPaths.TryAdd(
                        placedObject.SceneryObjectPath,
                        0);

                    objects.Add(new
                    {
                        tileX = loaded.Tile.X,
                        tileY = loaded.Tile.Y,
                        placedObject.HeaderValue,
                        placedObject.SceneryObjectPath,
                        placedObject.ObjectId,
                        placedObject.SourceSectionOrdinal,
                        placedObject.X,
                        placedObject.Y,
                        placedObject.Z,
                        placedObject.Rotation,
                        placedObject.Pitch,
                        placedObject.Bank
                    });
                }

                foreach (var placedSpline in
                    loaded.Content.Splines)
                {
                    _knownSplinePaths.TryAdd(
                        placedSpline.SplinePath,
                        0);

                    splines.Add(new
                    {
                        tileX = loaded.Tile.X,
                        tileY = loaded.Tile.Y,
                        placedSpline.HeaderValue,
                        placedSpline.SplinePath,
                        placedSpline.SplineId,
                        placedSpline.PreviousSplineId,
                        placedSpline.NextSplineId,
                        placedSpline.X,
                        placedSpline.Z,
                        placedSpline.Y,
                        placedSpline.Rotation,
                        placedSpline.Length,
                        placedSpline.Radius,
                        placedSpline.GradientStart,
                        placedSpline.GradientEnd,
                        placedSpline.IsHeightSpline
                    });
                }
            }

            PostMessage(new
            {
                type = "mapRegionLoaded",
                map.DirectoryName,
                centerX,
                centerY,
                radius,
                tiles = loadedTiles
                    .OrderBy(result => result.Index)
                    .Select(loaded => new
                    {
                        loaded.Tile.X,
                        loaded.Tile.Y,
                        loaded.Tile.RelativeMapPath,
                        detailsLoaded = true,
                        fileExists =
                            loaded.Content.Summary.Exists,
                        objectCount =
                            loaded.Content.Summary.ObjectCount,
                        splineCount =
                            loaded.Content.Summary.SplineCount,
                        splineAttachmentCount =
                            loaded.Content.Summary
                                .SplineAttachmentCount
                    }),
                objects,
                splines
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = map.DirectoryPath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "ioError",
                detail = exception.Message
            });
        }
    }

    private async Task<OmsiTileContent> ReadTileCachedAsync(
        string tilePath)
    {
        var task =
            _tileContentCache.GetOrAdd(
                tilePath,
                path =>
                    _tileReader.ReadContentAsync(
                        path));

        try
        {
            return await task;
        }
        catch
        {
            _tileContentCache.TryRemove(
                tilePath,
                out _);

            throw;
        }
    }

    private async Task LoadSplineProfileAsync(
        string? splinePath)
    {
        if (_omsiRootPath is null ||
            string.IsNullOrWhiteSpace(splinePath) ||
            !_knownSplinePaths.ContainsKey(
                splinePath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownSpline"
            });

            return;
        }

        if (!OmsiSplinePathResolver.TryResolve(
                _omsiRootPath,
                splinePath,
                out var fullPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidSplinePath"
            });

            return;
        }

        try
        {
            var task =
                _splineDefinitionCache.GetOrAdd(
                    fullPath,
                    path =>
                        _splineDefinitionReader.ReadAsync(
                            path));

            OmsiSplineDefinition definition;

            try
            {
                definition = await task;
            }
            catch
            {
                _splineDefinitionCache.TryRemove(
                    fullPath,
                    out _);

                throw;
            }

            PostMessage(new
            {
                type = "splineProfileLoaded",
                splinePath,
                definition
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = splinePath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "ioError",
                detail = exception.Message
            });
        }
    }

    private async Task LoadSceneryObjectMetadataAsync(
        string? sceneryObjectPath)
    {
        if (_omsiRootPath is null ||
            string.IsNullOrWhiteSpace(sceneryObjectPath) ||
            !_knownSceneryObjectPaths.ContainsKey(
                sceneryObjectPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownSceneryObject"
            });
            return;
        }

        if (!OmsiSceneryObjectPathResolver.TryResolve(
                _omsiRootPath,
                sceneryObjectPath,
                out var fullPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidSceneryObjectPath"
            });
            return;
        }

        try
        {
            var metadata =
                await _sceneryObjectReader.ReadMetadataAsync(
                    fullPath);

            var meshes =
                await CreateMeshReferencesAsync(
                    _omsiRootPath,
                    fullPath,
                    metadata.MeshPaths);

            var collisionMeshes =
                await CreateMeshReferencesAsync(
                    _omsiRootPath,
                    fullPath,
                    metadata.CollisionMeshPaths);

            PostMessage(new
            {
                type = "sceneryObjectMetadataLoaded",
                sceneryObjectPath,
                metadata = new
                {
                    metadata.Exists,
                    metadata.FriendlyName,
                    metadata.Groups,
                    meshes,
                    collisionMeshes
                }
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = sceneryObjectPath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "ioError",
                detail = exception.Message
            });
        }
    }

    private async Task LoadSceneryObjectGeometryAsync(
        string? sceneryObjectPath)
    {
        if (_omsiRootPath is null ||
            string.IsNullOrWhiteSpace(sceneryObjectPath) ||
            !_knownSceneryObjectPaths.ContainsKey(
                sceneryObjectPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownSceneryObject"
            });
            return;
        }

        if (!OmsiSceneryObjectPathResolver.TryResolve(
                _omsiRootPath,
                sceneryObjectPath,
                out var sceneryObjectFullPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidSceneryObjectPath"
            });
            return;
        }

        try
        {
            var metadata =
                await _sceneryObjectReader.ReadMetadataAsync(
                    sceneryObjectFullPath);

            var meshes = new List<object>(
                metadata.MeshPaths.Count);

            foreach (var declaredPath in metadata.MeshPaths)
            {
                if (!OmsiSceneryMeshPathResolver.TryResolve(
                        _omsiRootPath,
                        sceneryObjectFullPath,
                        declaredPath,
                        out var meshFullPath))
                {
                    meshes.Add(new
                    {
                        declaredPath,
                        geometry =
                            OmsiO3dGeometry.Error(
                                "invalidMeshPath")
                    });
                    continue;
                }

                if (!string.Equals(
                        Path.GetExtension(meshFullPath),
                        ".o3d",
                        StringComparison.OrdinalIgnoreCase))
                {
                    meshes.Add(new
                    {
                        declaredPath,
                        geometry =
                            OmsiO3dGeometry.Error(
                                "unsupportedFormat")
                    });
                    continue;
                }

                meshes.Add(new
                {
                    declaredPath,
                    geometry =
                        _o3dGeometryReader.Read(
                            meshFullPath)
                });
            }

            PostMessage(new
            {
                type = "sceneryObjectGeometryLoaded",
                sceneryObjectPath,
                geometry = new
                {
                    meshes
                }
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = sceneryObjectPath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "ioError",
                detail = exception.Message
            });
        }
    }

    private async Task<IReadOnlyList<object>>
        CreateMeshReferencesAsync(
            string omsiRoot,
            string sceneryObjectFullPath,
            IReadOnlyList<string> declaredPaths)
    {
        var results =
            new List<object>(
                declaredPaths.Count);

        foreach (var declaredPath in declaredPaths)
        {
            var resolved =
                OmsiSceneryMeshPathResolver.TryResolve(
                    omsiRoot,
                    sceneryObjectFullPath,
                    declaredPath,
                    out var fullPath);

            var fileExists =
                resolved &&
                File.Exists(fullPath);

            OmsiO3dHeader? o3d = null;
            OmsiO3dStructureSummary? structure = null;

            if (fileExists &&
                string.Equals(
                    Path.GetExtension(fullPath),
                    ".o3d",
                    StringComparison.OrdinalIgnoreCase))
            {
                o3d =
                    await _o3dHeaderReader.ReadAsync(
                        fullPath);

                if (o3d.IsValid)
                {
                    structure =
                        _o3dStructureReader.Read(
                            fullPath);
                }
            }

            results.Add(new
            {
                declaredPath,
                fileExists,
                o3d,
                structure
            });
        }

        return results;
    }

    private static bool TryReadObjectTransformRequests(
        JsonElement element,
        out IReadOnlyList<ObjectTransformRequest> requests)
    {
        requests =
            Array.Empty<ObjectTransformRequest>();

        if (
            !element.TryGetProperty(
                "edits",
                out var editsElement) ||
            editsElement.ValueKind !=
                JsonValueKind.Array)
        {
            return false;
        }

        var result =
            new List<ObjectTransformRequest>();

        foreach (var edit in
            editsElement.EnumerateArray())
        {
            if (
                !TryReadInt32(
                    edit,
                    "tileX",
                    out var tileX) ||
                !TryReadInt32(
                    edit,
                    "tileY",
                    out var tileY) ||
                !TryReadInt32(
                    edit,
                    "sourceSectionOrdinal",
                    out var sourceSectionOrdinal) ||
                !TryReadString(
                    edit,
                    "sceneryObjectPath",
                    out var sceneryObjectPath) ||
                !TryReadInt32(
                    edit,
                    "objectId",
                    out var objectId) ||
                !TryReadDouble(
                    edit,
                    "x",
                    out var x) ||
                !TryReadDouble(
                    edit,
                    "y",
                    out var y) ||
                !TryReadDouble(
                    edit,
                    "z",
                    out var z) ||
                !TryReadDouble(
                    edit,
                    "rotation",
                    out var rotation) ||
                !TryReadDouble(
                    edit,
                    "pitch",
                    out var pitch) ||
                !TryReadDouble(
                    edit,
                    "bank",
                    out var bank))
            {
                return false;
            }

            result.Add(
                new ObjectTransformRequest(
                    tileX,
                    tileY,
                    sourceSectionOrdinal,
                    sceneryObjectPath!,
                    objectId,
                    x,
                    y,
                    z,
                    rotation,
                    pitch,
                    bank));
        }

        requests = result;
        return result.Count > 0;
    }

    private static bool TryReadDouble(
        JsonElement element,
        string propertyName,
        out double value)
    {
        value = 0;

        return element.TryGetProperty(
                propertyName,
                out var property) &&
            property.ValueKind ==
                JsonValueKind.Number &&
            property.TryGetDouble(
                out value) &&
            double.IsFinite(value);
    }

    private sealed record MapInsertionSnapshot(
        IReadOnlyList<OmsiTileContent> Contents,
        int MaxUsedId);

    private sealed record SceneryLibraryEntry(
        string SceneryObjectPath,
        string FileName);

    private sealed record ObjectTransformRequest(
        int TileX,
        int TileY,
        int SourceSectionOrdinal,
        string SceneryObjectPath,
        int ObjectId,
        double X,
        double Y,
        double Z,
        double Rotation,
        double Pitch,
        double Bank);

    private sealed class InlineProgress<T>(
        Action<T> handler) : IProgress<T>
    {
        public void Report(T value) =>
            handler(value);
    }

    private static bool TryReadInt32(
        JsonElement element,
        string propertyName,
        out int value)
    {
        value = 0;

        return element.TryGetProperty(
                propertyName,
                out var property) &&
            property.ValueKind ==
                JsonValueKind.Number &&
            property.TryGetInt32(
                out value);
    }

    private static bool TryReadString(
        JsonElement element,
        string propertyName,
        out string? value)
    {
        value = null;

        if (!element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();

        return !string.IsNullOrWhiteSpace(value);
    }

    private void PostInvalidMessage()
    {
        PostMessage(new
        {
            type = "hostError",
            code = "invalidMessage"
        });
    }

    private void PostMessage(object payload)
    {
        var json =
            JsonSerializer.Serialize(
                payload,
                _jsonOptions);

        EditorWebView.CoreWebView2
            .PostWebMessageAsJson(json);
    }
}
