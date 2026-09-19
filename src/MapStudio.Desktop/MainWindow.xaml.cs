using System.Collections.Concurrent;
using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using MapStudio.Core.IO;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Textures;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MapStudio.Desktop;

public partial class MainWindow : Window
{
    private const int MaxConcurrentTileReads = 8;
    private const int MaxConcurrentGeometryReads = 12;
    private const int MaxTileStreamRadius = 2;
    private const long MaxTextureAssetBytes =
        16L * 1024L * 1024L;

    private readonly OmsiMapCatalog _mapCatalog = new();
    private readonly OmsiTileReader _tileReader = new();
    private readonly OmsiSceneryObjectReader _sceneryObjectReader = new();
    private readonly OmsiO3dHeaderReader _o3dHeaderReader = new();
    private readonly OmsiO3dStructureReader _o3dStructureReader = new();
    private readonly OmsiO3dGeometryReader _o3dGeometryReader = new();
    private readonly OmsiDirectXTextGeometryReader _directXGeometryReader = new();
    private readonly OmsiSplineDefinitionReader _splineDefinitionReader = new();
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim
        _geometryReadSemaphore =
            new(
                Math.Min(
                    MaxConcurrentGeometryReads,
                    Math.Max(
                        4,
                        Environment.ProcessorCount)));

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

    private readonly ConcurrentDictionary<
        string,
        Task<SceneryGeometryPayload>>
        _sceneryGeometryCache =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<
        string,
        Task<OmsiSceneryObjectMetadata>>
        _sceneryMetadataCache =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<
        string,
        Lazy<OmsiO3dGeometry>>
        _meshGeometryCache =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<
        string,
        Task<TextureAssetPayload>>
        _textureAssetCache =
            new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<SceneryLibraryEntry>?
        _sceneryLibraryCache;

    private IReadOnlyList<SplineLibraryEntry>?
        _splineLibraryCache;

    private string? _omsiRootPath;

    private bool _isFullScreen;
    private WindowStyle _windowStyleBeforeFullScreen =
        WindowStyle.SingleBorderWindow;
    private WindowState _windowStateBeforeFullScreen =
        WindowState.Normal;
    private ResizeMode _resizeModeBeforeFullScreen =
        ResizeMode.CanResize;

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
                case "setFullScreen":
                    if (
                        message.RootElement.TryGetProperty(
                            "enabled",
                            out var fullScreenElement) &&
                        fullScreenElement.ValueKind is
                            JsonValueKind.True or
                            JsonValueKind.False)
                    {
                        SetFullScreen(
                            fullScreenElement.GetBoolean());
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "selectOmsiRoot":
                    await SelectOmsiRootAsync();
                    break;

                case "selectMap":
                    await SelectMapAsync();
                    break;

                case "loadSceneryLibrary":
                    await LoadSceneryLibraryAsync();
                    break;

                case "loadSplineLibrary":
                    await LoadSplineLibraryAsync();
                    break;

                case "insertSplineFromLibrary":
                    if (
                        TryReadSplineLibraryInsertionRequest(
                            message.RootElement,
                            out var splineLibraryInsertionRequest))
                    {
                        await InsertSplineFromLibraryAsync(
                            splineLibraryInsertionRequest);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
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

                case "deleteSpline":
                    if (
                        TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var deleteSplineDirectoryName) &&
                        TryReadInt32(
                            message.RootElement,
                            "tileX",
                            out var deleteSplineTileX) &&
                        TryReadInt32(
                            message.RootElement,
                            "tileY",
                            out var deleteSplineTileY) &&
                        TryReadInt32(
                            message.RootElement,
                            "sourceSectionOrdinal",
                            out var deleteSplineSourceSectionOrdinal) &&
                        TryReadString(
                            message.RootElement,
                            "splinePath",
                            out var deleteSplinePath) &&
                        TryReadInt32(
                            message.RootElement,
                            "splineId",
                            out var deleteSplineId) &&
                        TryReadInt32(
                            message.RootElement,
                            "previousSplineId",
                            out var deleteSplinePreviousId) &&
                        TryReadInt32(
                            message.RootElement,
                            "nextSplineId",
                            out var deleteSplineNextId) &&
                        TryReadBoolean(
                            message.RootElement,
                            "isHeightSpline",
                            out var deleteSplineIsHeight))
                    {
                        await DeleteSplineAsync(
                            deleteSplineDirectoryName,
                            deleteSplineTileX,
                            deleteSplineTileY,
                            deleteSplineSourceSectionOrdinal,
                            deleteSplinePath,
                            deleteSplineId,
                            deleteSplinePreviousId,
                            deleteSplineNextId,
                            deleteSplineIsHeight);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "insertSpline":
                    if (
                        TryReadSplineInsertionRequest(
                            message.RootElement,
                            out var splineInsertionRequest))
                    {
                        await InsertSplineAsync(
                            splineInsertionRequest);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "updateSplineLinks":
                    if (
                        TryReadSplineLinkRequest(
                            message.RootElement,
                            out var splineLinkRequest))
                    {
                        await UpdateSplineLinksAsync(
                            splineLinkRequest);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "saveSplineTransforms":
                    if (
                        TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var splineSaveDirectoryName) &&
                        TryReadSplineTransformRequests(
                            message.RootElement,
                            out var splineTransformRequests))
                    {
                        await SaveSplineTransformsAsync(
                            splineSaveDirectoryName,
                            splineTransformRequests);
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

                case "loadTerrainTextureMaskAsset":
                    if (
                        TryReadString(
                            message.RootElement,
                            "requestKey",
                            out var terrainMaskRequestKey) &&
                        TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var terrainMaskDirectoryName) &&
                        TryReadString(
                            message.RootElement,
                            "relativeMapPath",
                            out var terrainMaskRelativeMapPath) &&
                        TryReadInt32(
                            message.RootElement,
                            "layerIndex",
                            out var terrainMaskLayerIndex))
                    {
                        await LoadTerrainTextureMaskAssetAsync(
                            terrainMaskRequestKey,
                            terrainMaskDirectoryName,
                            terrainMaskRelativeMapPath,
                            terrainMaskLayerIndex);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "loadGroundTextureAsset":
                    if (
                        TryReadString(
                            message.RootElement,
                            "requestKey",
                            out var groundTextureRequestKey) &&
                        TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var groundTextureDirectoryName) &&
                        TryReadString(
                            message.RootElement,
                            "texturePath",
                            out var groundTexturePath))
                    {
                        await LoadGroundTextureAssetAsync(
                            groundTextureRequestKey,
                            groundTextureDirectoryName,
                            groundTexturePath);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "loadSceneryTextureAsset":
                    if (
                        TryReadString(
                            message.RootElement,
                            "requestKey",
                            out var sceneryTextureRequestKey) &&
                        TryReadString(
                            message.RootElement,
                            "sceneryObjectPath",
                            out var textureSceneryObjectPath) &&
                        TryReadString(
                            message.RootElement,
                            "declaredMeshPath",
                            out var textureDeclaredMeshPath) &&
                        TryReadString(
                            message.RootElement,
                            "textureName",
                            out var sceneryTextureName))
                    {
                        await LoadSceneryTextureAssetAsync(
                            sceneryTextureRequestKey,
                            textureSceneryObjectPath,
                            textureDeclaredMeshPath,
                            sceneryTextureName);
                    }
                    else
                    {
                        PostInvalidMessage();
                    }
                    break;

                case "loadSplineTextureAsset":
                    if (
                        TryReadString(
                            message.RootElement,
                            "requestKey",
                            out var splineTextureRequestKey) &&
                        TryReadString(
                            message.RootElement,
                            "splinePath",
                            out var textureSplinePath) &&
                        TryReadString(
                            message.RootElement,
                            "textureName",
                            out var splineTextureName))
                    {
                        await LoadSplineTextureAssetAsync(
                            splineTextureRequestKey,
                            textureSplinePath,
                            splineTextureName);
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
        _sceneryGeometryCache.Clear();
        _sceneryMetadataCache.Clear();
        _meshGeometryCache.Clear();
        _textureAssetCache.Clear();
        _sceneryLibraryCache = null;
        _splineLibraryCache = null;

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
                    groundTextures =
                        map.GroundTextures,
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
                            splineAttachmentCount = 0,
                            terrainMarkerPresent =
                                false,
                            terrainFileExists =
                                false,
                            terrainFileSize = 0
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

    private async Task LoadSplineLibraryAsync()
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
            if (_splineLibraryCache is null)
            {
                var entries =
                    await Task.Run(
                        () =>
                            ScanSplineLibrary(
                                omsiRoot));

                if (!string.Equals(
                        _omsiRootPath,
                        omsiRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _splineLibraryCache =
                    entries;
            }

            PostMessage(new
            {
                type =
                    "splineLibraryLoaded",
                entries =
                    _splineLibraryCache
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = "Splines"
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

    private IReadOnlyList<SplineLibraryEntry>
        ScanSplineLibrary(
            string omsiRoot)
    {
        var root =
            Path.Combine(
                omsiRoot,
                "Splines");

        if (!Directory.Exists(root))
        {
            return
                Array.Empty<SplineLibraryEntry>();
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
            new List<SplineLibraryEntry>();

        foreach (var filePath in
            Directory.EnumerateFiles(
                root,
                "*.sli",
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
                "Splines\\" +
                relative;

            entries.Add(
                new SplineLibraryEntry(
                    declaredPath,
                    Path.GetFileName(
                        filePath)));

            _knownSplinePaths.TryAdd(
                declaredPath,
                0);
        }

        return entries
            .OrderBy(
                entry =>
                    entry.SplinePath,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task InsertSplineFromLibraryAsync(
        SplineLibraryInsertionRequest request)
    {
        if (
            !_knownMaps.TryGetValue(
                request.DirectoryName,
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
            _omsiRootPath is null ||
            !_knownSplinePaths
                .ContainsKey(
                    request.SplinePath) ||
            !OmsiSplinePathResolver
                .TryResolve(
                    _omsiRootPath,
                    request.SplinePath,
                    out var splineFullPath) ||
            !File.Exists(splineFullPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidSplinePath"
            });

            return;
        }

        if (map.UsesWorldCoordinates)
        {
            PostMessage(new
            {
                type = "hostError",
                code =
                    "splineInsertionWorldCoordinatesUnsupported"
            });

            return;
        }

        var targetTile =
            map.Tiles.FirstOrDefault(
                tile =>
                    tile.X ==
                        request.TargetTileX &&
                    tile.Y ==
                        request.TargetTileY);

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
            !File.Exists(targetTilePath))
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

            var template =
                OmsiSplinePlacementTemplateAnalyzer
                    .FindNeutralTemplate(
                        snapshot.Contents,
                        request.IsHeightSpline);

            if (template is null)
            {
                PostMessage(new
                {
                    type = "hostError",
                    code =
                        "splineInsertTemplateUnavailable",
                    detail =
                        request.SplinePath
                });

                return;
            }

            if (
                snapshot.MaxUsedId >=
                    int.MaxValue)
            {
                throw new InvalidDataException(
                    "splineIdExhausted");
            }

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        targetTilePath);

            var nextId =
                checked(
                    snapshot.MaxUsedId + 1);

            var result =
                OmsiTileSplineInserter
                    .Append(
                        document,
                        new OmsiNewPlacedSpline(
                            template.HeaderValue,
                            request.SplinePath,
                            nextId,
                            -1,
                            -1,
                            request.X,
                            request.Z,
                            request.Y,
                            request.Rotation,
                            request.Length,
                            request.Radius,
                            request.GradientStart,
                            request.GradientEnd,
                            request.IsHeightSpline,
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

            await SafeFileTransaction
                .WriteAllAsync(
                    [
                        new PendingFileWrite(
                            targetTilePath,
                            Path.Combine(
                                backupRoot,
                                relativePath),
                            result.Bytes)
                    ]);

            _tileContentCache
                .TryRemove(
                    targetTilePath,
                    out _);

            PostMessage(new
            {
                type = "splineInserted",
                map.DirectoryName,
                backupDirectory =
                    backupRoot,
                placedSpline = new
                {
                    tileX =
                        request.TargetTileX,
                    tileY =
                        request.TargetTileY,
                    headerValue =
                        template.HeaderValue,
                    splinePath =
                        request.SplinePath,
                    splineId = nextId,
                    sourceSectionOrdinal =
                        result.SourceSectionOrdinal,
                    previousSplineId = -1,
                    nextSplineId = -1,
                    x = request.X,
                    z = request.Z,
                    y = request.Y,
                    rotation =
                        request.Rotation,
                    length =
                        request.Length,
                    radius =
                        request.Radius,
                    gradientStart =
                        request.GradientStart,
                    gradientEnd =
                        request.GradientEnd,
                    isHeightSpline =
                        request.IsHeightSpline
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
                        "splineIdExhausted"
                        ? "splineIdExhausted"
                        : "splineInsertError",
                detail = exception.Message
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = targetTilePath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "splineInsertError",
                detail = exception.Message
            });
        }
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
                    MaxConcurrentTileReads);

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

    private async Task DeleteSplineAsync(
        string? directoryName,
        int tileX,
        int tileY,
        int sourceSectionOrdinal,
        string? splinePath,
        int splineId,
        int previousSplineId,
        int nextSplineId,
        bool isHeightSpline)
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
                splinePath))
        {
            PostInvalidMessage();
            return;
        }

        try
        {
            var snapshot =
                await ReadMapInsertionSnapshotAsync(
                    map);

            var entries =
                new List<SplineMapEntry>();

            var count =
                Math.Min(
                    map.Tiles.Count,
                    snapshot.Contents.Count);

            for (
                var index = 0;
                index < count;
                index++)
            {
                var tile =
                    map.Tiles[index];

                foreach (var spline in
                    snapshot
                        .Contents[index]
                        .Splines)
                {
                    entries.Add(
                        new SplineMapEntry(
                            tile.X,
                            tile.Y,
                            tile.RelativeMapPath,
                            spline));
                }
            }

            if (
                entries
                    .GroupBy(entry =>
                        entry.Spline.SplineId)
                    .Any(group =>
                        group.Count() > 1))
            {
                throw new InvalidDataException(
                    "duplicateSplineId");
            }

            var byId =
                entries.ToDictionary(
                    entry =>
                        entry.Spline.SplineId);

            if (
                !byId.TryGetValue(
                    splineId,
                    out var source) ||
                source.TileX != tileX ||
                source.TileY != tileY ||
                source.Spline
                    .SourceSectionOrdinal !=
                    sourceSectionOrdinal ||
                source.Spline
                    .PreviousSplineId !=
                    previousSplineId ||
                source.Spline
                    .NextSplineId !=
                    nextSplineId ||
                source.Spline
                    .IsHeightSpline !=
                    isHeightSpline ||
                !string.Equals(
                    source.Spline.SplinePath,
                    splinePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "splineSourceChanged");
            }

            var states =
                byId.ToDictionary(
                    pair => pair.Key,
                    pair =>
                        new OmsiSplineLinkState(
                            pair.Key,
                            pair.Value
                                .Spline
                                .PreviousSplineId,
                            pair.Value
                                .Spline
                                .NextSplineId));

            var unlinkPlan =
                OmsiSplineLinkPlanner
                    .Plan(
                        states,
                        splineId,
                        previousSplineId,
                        nextSplineId,
                        -1,
                        -1);

            var affectedEntries =
                unlinkPlan.Keys
                    .Where(id =>
                        id != splineId)
                    .Select(id =>
                        byId[id])
                    .Append(source)
                    .GroupBy(entry =>
                        (
                            TileX: entry.TileX,
                            TileY: entry.TileY,
                            RelativeMapPath:
                                entry.RelativeMapPath))
                    .ToArray();

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

            var unlinkedSplines = 0;

            foreach (var group in
                affectedEntries)
            {
                if (!OmsiMapPathResolver
                    .TryResolveTilePath(
                        map.DirectoryPath,
                        group.Key
                            .RelativeMapPath,
                        out var tilePath) ||
                    !File.Exists(tilePath))
                {
                    throw new InvalidDataException(
                        "invalidTilePath");
                }

                var document =
                    await OmsiConfigParser
                        .ParseFileAsync(
                            tilePath);

                var linkEdits =
                    group
                        .Where(entry =>
                            entry.Spline.SplineId !=
                                splineId &&
                            unlinkPlan.ContainsKey(
                                entry.Spline
                                    .SplineId))
                        .Select(entry =>
                        {
                            var target =
                                unlinkPlan[
                                    entry.Spline
                                        .SplineId];

                            return new OmsiSplineLinkEdit(
                                entry.Spline
                                    .SourceSectionOrdinal,
                                entry.Spline
                                    .SplinePath,
                                entry.Spline
                                    .SplineId,
                                entry.Spline
                                    .PreviousSplineId,
                                entry.Spline
                                    .NextSplineId,
                                entry.Spline
                                    .IsHeightSpline,
                                target
                                    .PreviousSplineId,
                                target
                                    .NextSplineId);
                        })
                        .ToArray();

                if (linkEdits.Length > 0)
                {
                    var linkResult =
                        OmsiTileSplineLinkEditor
                            .ApplyLinks(
                                document,
                                linkEdits);

                    unlinkedSplines +=
                        linkResult.AppliedEdits;

                    document =
                        OmsiConfigParser
                            .ParseBytes(
                                linkResult.Bytes);
                }

                byte[] bytes;

                if (
                    group.Any(entry =>
                        entry.Spline.SplineId ==
                            splineId))
                {
                    var deleteResult =
                        OmsiTileSplineDeleter
                            .Remove(
                                document,
                                sourceSectionOrdinal,
                                splinePath,
                                splineId,
                                previousSplineId,
                                nextSplineId,
                                isHeightSpline);

                    bytes =
                        deleteResult.Bytes;
                }
                else
                {
                    bytes =
                        document.ToBytes();
                }

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

                writes.Add(
                    new PendingFileWrite(
                        tilePath,
                        Path.Combine(
                            backupRoot,
                            relativePath),
                        bytes));

                editedTilePaths.Add(
                    tilePath);
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
                type = "splineDeleted",
                map.DirectoryName,
                splineId,
                deletedSplines = 1,
                unlinkedSplines,
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
                code = "splineDeleteConflict",
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
                code = "splineDeleteError",
                detail = exception.Message
            });
        }
    }

    private async Task InsertSplineAsync(
        SplineInsertionRequest request)
    {
        if (
            !_knownMaps.TryGetValue(
                request.DirectoryName,
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
            _omsiRootPath is null ||
            !_knownSplinePaths
                .ContainsKey(
                    request.SplinePath) ||
            !OmsiSplinePathResolver
                .TryResolve(
                    _omsiRootPath,
                    request.SplinePath,
                    out var splineFullPath) ||
            !File.Exists(splineFullPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidSplinePath"
            });

            return;
        }

        if (map.UsesWorldCoordinates)
        {
            PostMessage(new
            {
                type = "hostError",
                code =
                    "splineInsertionWorldCoordinatesUnsupported"
            });

            return;
        }

        var sourceTile =
            map.Tiles.FirstOrDefault(
                tile =>
                    tile.X ==
                        request.SourceTileX &&
                    tile.Y ==
                        request.SourceTileY);

        var targetTile =
            map.Tiles.FirstOrDefault(
                tile =>
                    tile.X ==
                        request.TargetTileX &&
                    tile.Y ==
                        request.TargetTileY);

        if (
            sourceTile is null ||
            targetTile is null)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "unknownTile"
            });

            return;
        }

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    map.DirectoryPath,
                    sourceTile.RelativeMapPath,
                    out var sourceTilePath) ||
            !File.Exists(sourceTilePath) ||
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    map.DirectoryPath,
                    targetTile.RelativeMapPath,
                    out var targetTilePath) ||
            !File.Exists(targetTilePath))
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

            if (
                snapshot.MaxUsedId >=
                    int.MaxValue)
            {
                throw new InvalidDataException(
                    "splineIdExhausted");
            }

            var targetDocument =
                await OmsiConfigParser
                    .ParseFileAsync(
                        targetTilePath);

            var sourceDocument =
                string.Equals(
                    sourceTilePath,
                    targetTilePath,
                    StringComparison.OrdinalIgnoreCase)
                    ? targetDocument
                    : await OmsiConfigParser
                        .ParseFileAsync(
                            sourceTilePath);

            var sourceSpline =
                OmsiTileReader
                    .ReadSplines(
                        sourceDocument)
                    .FirstOrDefault(
                        candidate =>
                            candidate
                                .SourceSectionOrdinal ==
                            request
                                .SourceSectionOrdinal);

            if (
                sourceSpline is null ||
                sourceSpline.SplineId !=
                    request.SplineId ||
                sourceSpline.PreviousSplineId !=
                    request.PreviousSplineId ||
                sourceSpline.NextSplineId !=
                    request.NextSplineId ||
                sourceSpline.IsHeightSpline !=
                    request.IsHeightSpline ||
                !string.Equals(
                    sourceSpline.SplinePath,
                    request.SplinePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "splineSourceChanged");
            }

            var nextId =
                checked(
                    snapshot.MaxUsedId + 1);

            var result =
                OmsiTileSplineInserter
                    .Append(
                        targetDocument,
                        new OmsiNewPlacedSpline(
                            sourceSpline.HeaderValue,
                            sourceSpline.SplinePath,
                            nextId,
                            -1,
                            -1,
                            request.X,
                            request.Z,
                            request.Y,
                            request.Rotation,
                            request.Length,
                            request.Radius,
                            request.GradientStart,
                            request.GradientEnd,
                            sourceSpline.IsHeightSpline,
                            sourceSpline.ExtraValues));

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
                type = "splineInserted",
                map.DirectoryName,
                backupDirectory =
                    backupRoot,
                placedSpline = new
                {
                    tileX =
                        request.TargetTileX,
                    tileY =
                        request.TargetTileY,
                    headerValue =
                        sourceSpline.HeaderValue,
                    splinePath =
                        sourceSpline.SplinePath,
                    splineId = nextId,
                    sourceSectionOrdinal =
                        result.SourceSectionOrdinal,
                    previousSplineId = -1,
                    nextSplineId = -1,
                    x = request.X,
                    z = request.Z,
                    y = request.Y,
                    rotation =
                        request.Rotation,
                    length =
                        request.Length,
                    radius =
                        request.Radius,
                    gradientStart =
                        request.GradientStart,
                    gradientEnd =
                        request.GradientEnd,
                    isHeightSpline =
                        sourceSpline.IsHeightSpline
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
                        "splineIdExhausted"
                        ? "splineIdExhausted"
                        : exception.Message ==
                            "splineSourceChanged"
                            ? "splineInsertConflict"
                            : "splineInsertError",
                detail = exception.Message
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = targetTilePath
            });
        }
        catch (IOException exception)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "splineInsertError",
                detail = exception.Message
            });
        }
    }

    private async Task UpdateSplineLinksAsync(
        SplineLinkRequest request)
    {
        if (
            !_knownMaps.TryGetValue(
                request.DirectoryName,
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
            var snapshot =
                await ReadMapInsertionSnapshotAsync(
                    map);

            var entries =
                new List<SplineMapEntry>();

            var count =
                Math.Min(
                    map.Tiles.Count,
                    snapshot.Contents.Count);

            for (
                var index = 0;
                index < count;
                index++)
            {
                var tile =
                    map.Tiles[index];

                foreach (var spline in
                    snapshot
                        .Contents[index]
                        .Splines)
                {
                    entries.Add(
                        new SplineMapEntry(
                            tile.X,
                            tile.Y,
                            tile.RelativeMapPath,
                            spline));
                }
            }

            if (
                entries
                    .GroupBy(entry =>
                        entry.Spline.SplineId)
                    .Any(group =>
                        group.Count() > 1))
            {
                throw new InvalidDataException(
                    "duplicateSplineId");
            }

            var byId =
                entries.ToDictionary(
                    entry =>
                        entry.Spline.SplineId);

            if (
                !byId.TryGetValue(
                    request.SplineId,
                    out var source) ||
                source.TileX !=
                    request.TileX ||
                source.TileY !=
                    request.TileY ||
                source.Spline
                    .SourceSectionOrdinal !=
                    request
                        .SourceSectionOrdinal ||
                source.Spline
                    .PreviousSplineId !=
                    request
                        .PreviousSplineId ||
                source.Spline
                    .NextSplineId !=
                    request
                        .NextSplineId ||
                source.Spline
                    .IsHeightSpline !=
                    request
                        .IsHeightSpline ||
                !string.Equals(
                    source.Spline.SplinePath,
                    request.SplinePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "splineLinkSourceChanged");
            }

            var states =
                byId.ToDictionary(
                    pair => pair.Key,
                    pair =>
                        new OmsiSplineLinkState(
                            pair.Key,
                            pair.Value
                                .Spline
                                .PreviousSplineId,
                            pair.Value
                                .Spline
                                .NextSplineId));

            var plan =
                OmsiSplineLinkPlanner
                    .Plan(
                        states,
                        request.SplineId,
                        request.PreviousSplineId,
                        request.NextSplineId,
                        request
                            .DesiredPreviousSplineId,
                        request
                            .DesiredNextSplineId);

            if (plan.Count == 0)
            {
                PostMessage(new
                {
                    type = "splineLinksUpdated",
                    map.DirectoryName,
                    splineId =
                        request.SplineId,
                    previousSplineId =
                        request
                            .DesiredPreviousSplineId,
                    nextSplineId =
                        request
                            .DesiredNextSplineId,
                    linksUpdated = 0,
                    filesSaved = 0,
                    backupDirectory =
                        string.Empty
                });

                return;
            }

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
                var group in plan.Keys
                    .Select(id => byId[id])
                    .GroupBy(entry =>
                        (
                            TileX: entry.TileX,
                            TileY: entry.TileY,
                            RelativeMapPath:
                                entry.RelativeMapPath)))
            {
                if (!OmsiMapPathResolver
                    .TryResolveTilePath(
                        map.DirectoryPath,
                        group.Key
                            .RelativeMapPath,
                        out var tilePath) ||
                    !File.Exists(tilePath))
                {
                    throw new InvalidDataException(
                        "invalidTilePath");
                }

                var document =
                    await OmsiConfigParser
                        .ParseFileAsync(
                            tilePath);

                var edits =
                    group.Select(entry =>
                    {
                        var target =
                            plan[
                                entry.Spline
                                    .SplineId];

                        return new OmsiSplineLinkEdit(
                            entry.Spline
                                .SourceSectionOrdinal,
                            entry.Spline
                                .SplinePath,
                            entry.Spline
                                .SplineId,
                            entry.Spline
                                .PreviousSplineId,
                            entry.Spline
                                .NextSplineId,
                            entry.Spline
                                .IsHeightSpline,
                            target
                                .PreviousSplineId,
                            target
                                .NextSplineId);
                    })
                    .ToArray();

                var result =
                    OmsiTileSplineLinkEditor
                        .ApplyLinks(
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

                writes.Add(
                    new PendingFileWrite(
                        tilePath,
                        Path.Combine(
                            backupRoot,
                            relativePath),
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
                    "splineLinksUpdated",
                map.DirectoryName,
                splineId =
                    request.SplineId,
                previousSplineId =
                    request
                        .DesiredPreviousSplineId,
                nextSplineId =
                    request
                        .DesiredNextSplineId,
                linksUpdated =
                    appliedEdits,
                filesSaved =
                    writes.Count,
                backupDirectory =
                    backupRoot
            });
        }
        catch (InvalidDataException exception)
        {
            var code =
                exception.Message switch
                {
                    "splineLinkTargetBusy" =>
                        "splineLinkTargetBusy",
                    "splineLinkNeighborMissing" =>
                        "splineLinkTargetMissing",
                    "splineLinkSelf" =>
                        "splineLinkInvalid",
                    "splineLinkDuplicateNeighbor" =>
                        "splineLinkInvalid",
                    _ =>
                        "splineLinkConflict"
                };

            PostMessage(new
            {
                type = "hostError",
                code,
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
                code = "splineLinkError",
                detail = exception.Message
            });
        }
    }

    private async Task SaveSplineTransformsAsync(
        string? directoryName,
        IReadOnlyList<SplineTransformRequest> requests)
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
                            new OmsiSplineTransformEdit(
                                request.SourceSectionOrdinal,
                                request.SplinePath,
                                request.SplineId,
                                request.PreviousSplineId,
                                request.NextSplineId,
                                request.IsHeightSpline,
                                request.X,
                                request.Z,
                                request.Y,
                                request.Rotation,
                                request.Length,
                                request.Radius,
                                request.GradientStart,
                                request.GradientEnd))
                    .ToArray();

                var result =
                    OmsiTileSplineEditor
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

                writes.Add(
                    new PendingFileWrite(
                        tilePath,
                        Path.Combine(
                            backupRoot,
                            relativePath),
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
                    "splineTransformsSaved",
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
                    MaxConcurrentTileReads);

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
                        placedObject.Bank,
                        extraValues =
                            placedObject.ExtraValues
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
                        placedSpline.SourceSectionOrdinal,
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
                                .SplineAttachmentCount,
                        terrainMarkerPresent =
                            loaded.Content.Summary
                                .TerrainMarkerPresent,
                        terrainFileExists =
                            loaded.Content.Summary
                                .TerrainFileExists,
                        terrainFileSize =
                            loaded.Content.Summary
                                .TerrainFileSize,
                        terrain =
                            loaded.Content.Terrain is null
                                ? null
                                : new
                                {
                                    cellCount =
                                        loaded.Content.Terrain
                                            .CellCount,
                                    heights =
                                        loaded.Content.Terrain
                                            .Heights
                                },
                        terrainRenderData =
                            loaded.Content
                                .TerrainRenderData,
                        terrainTextureMasks =
                            loaded.Content
                                .TerrainTextureMasks ??
                            Array.Empty<
                                OmsiTerrainTextureMask>()
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
                    MaxConcurrentTileReads);

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
                        placedObject.Bank,
                        extraValues =
                            placedObject.ExtraValues
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
                        placedSpline.SourceSectionOrdinal,
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
                                .SplineAttachmentCount,
                        terrainMarkerPresent =
                            loaded.Content.Summary
                                .TerrainMarkerPresent,
                        terrainFileExists =
                            loaded.Content.Summary
                                .TerrainFileExists,
                        terrainFileSize =
                            loaded.Content.Summary
                                .TerrainFileSize,
                        terrain =
                            loaded.Content.Terrain is null
                                ? null
                                : new
                                {
                                    cellCount =
                                        loaded.Content.Terrain
                                            .CellCount,
                                    heights =
                                        loaded.Content.Terrain
                                            .Heights
                                },
                        terrainRenderData =
                            loaded.Content
                                .TerrainRenderData,
                        terrainTextureMasks =
                            loaded.Content
                                .TerrainTextureMasks ??
                            Array.Empty<
                                OmsiTerrainTextureMask>()
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
                    Task.Run(
                        async () =>
                            await _tileReader
                                .ReadContentAsync(
                                    path)));

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

    private async Task LoadTerrainTextureMaskAssetAsync(
        string? requestKey,
        string? directoryName,
        string? relativeMapPath,
        int layerIndex)
    {
        if (
            string.IsNullOrWhiteSpace(
                requestKey) ||
            string.IsNullOrWhiteSpace(
                directoryName) ||
            string.IsNullOrWhiteSpace(
                relativeMapPath) ||
            !_knownMaps.TryGetValue(
                directoryName,
                out var map) ||
            layerIndex <= 0 ||
            layerIndex >=
                map.GroundTextures.Count ||
            !map.Tiles.Any(
                tile =>
                    string.Equals(
                        tile.RelativeMapPath,
                        relativeMapPath,
                        StringComparison.OrdinalIgnoreCase)) ||
            !OmsiTextureAssetPathResolver
                .TryResolveTerrainTextureMask(
                    map.DirectoryPath,
                    relativeMapPath,
                    layerIndex,
                    out var fullPath))
        {
            PostMissingTextureAsset(
                requestKey,
                "textureNotFound");
            return;
        }

        await LoadTextureAssetAsync(
            requestKey,
            fullPath);
    }

    private async Task LoadGroundTextureAssetAsync(
        string? requestKey,
        string? directoryName,
        string? texturePath)
    {
        if (
            _omsiRootPath is null ||
            string.IsNullOrWhiteSpace(
                requestKey) ||
            string.IsNullOrWhiteSpace(
                directoryName) ||
            string.IsNullOrWhiteSpace(
                texturePath) ||
            !_knownMaps.TryGetValue(
                directoryName,
                out var map))
        {
            PostMissingTextureAsset(
                requestKey,
                "invalidTextureSource");
            return;
        }

        var declared =
            map.GroundTextures.Any(
                layer =>
                    string.Equals(
                        layer.MainTexturePath,
                        texturePath,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        layer.DetailTexturePath,
                        texturePath,
                        StringComparison.OrdinalIgnoreCase));

        if (
            !declared ||
            !OmsiTextureAssetPathResolver
                .TryResolveGroundTexture(
                    _omsiRootPath,
                    map.DirectoryPath,
                    texturePath,
                    out var fullPath))
        {
            PostMissingTextureAsset(
                requestKey,
                "textureNotFound");
            return;
        }

        await LoadTextureAssetAsync(
            requestKey,
            fullPath);
    }

    private async Task LoadSceneryTextureAssetAsync(
        string? requestKey,
        string? sceneryObjectPath,
        string? declaredMeshPath,
        string? textureName)
    {
        if (
            _omsiRootPath is null ||
            string.IsNullOrWhiteSpace(
                requestKey) ||
            string.IsNullOrWhiteSpace(
                sceneryObjectPath) ||
            string.IsNullOrWhiteSpace(
                declaredMeshPath) ||
            string.IsNullOrWhiteSpace(
                textureName) ||
            !_knownSceneryObjectPaths
                .ContainsKey(
                    sceneryObjectPath))
        {
            PostMissingTextureAsset(
                requestKey,
                "invalidTextureSource");
            return;
        }

        if (
            !OmsiSceneryObjectPathResolver
                .TryResolve(
                    _omsiRootPath,
                    sceneryObjectPath,
                    out var sceneryObjectFullPath))
        {
            PostMissingTextureAsset(
                requestKey,
                "textureNotFound");
            return;
        }

        string textureFullPath;

        if (string.Equals(
                declaredMeshPath,
                "__tree__",
                StringComparison.Ordinal))
        {
            if (
                !OmsiTextureAssetPathResolver
                    .TryResolveSceneryObjectTexture(
                        _omsiRootPath,
                        sceneryObjectFullPath,
                        textureName,
                        out textureFullPath))
            {
                PostMissingTextureAsset(
                    requestKey,
                    "textureNotFound");
                return;
            }
        }
        else
        {
            if (
                !OmsiSceneryMeshPathResolver
                    .TryResolve(
                        _omsiRootPath,
                        sceneryObjectFullPath,
                        declaredMeshPath,
                        out var meshFullPath) ||
                !OmsiTextureAssetPathResolver
                    .TryResolveSceneryTexture(
                        _omsiRootPath,
                        sceneryObjectFullPath,
                        meshFullPath,
                        textureName,
                        out textureFullPath))
            {
                PostMissingTextureAsset(
                    requestKey,
                    "textureNotFound");
                return;
            }
        }

        await LoadTextureAssetAsync(
            requestKey,
            textureFullPath);
    }

    private async Task LoadSplineTextureAssetAsync(
        string? requestKey,
        string? splinePath,
        string? textureName)
    {
        if (
            _omsiRootPath is null ||
            string.IsNullOrWhiteSpace(
                requestKey) ||
            string.IsNullOrWhiteSpace(
                splinePath) ||
            string.IsNullOrWhiteSpace(
                textureName) ||
            !_knownSplinePaths.ContainsKey(
                splinePath))
        {
            PostMissingTextureAsset(
                requestKey,
                "invalidTextureSource");
            return;
        }

        if (
            !OmsiSplinePathResolver
                .TryResolve(
                    _omsiRootPath,
                    splinePath,
                    out var splineFullPath) ||
            !OmsiTextureAssetPathResolver
                .TryResolveSplineTexture(
                    _omsiRootPath,
                    splineFullPath,
                    textureName,
                    out var textureFullPath))
        {
            PostMissingTextureAsset(
                requestKey,
                "textureNotFound");
            return;
        }

        await LoadTextureAssetAsync(
            requestKey,
            textureFullPath);
    }

    private async Task LoadTextureAssetAsync(
        string requestKey,
        string fullPath)
    {
        try
        {
            var payload =
                await ReadTextureAssetCachedAsync(
                    fullPath);

            PostMessage(new
            {
                type = "textureAssetLoaded",
                requestKey,
                asset = payload
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMissingTextureAsset(
                requestKey,
                "accessDenied");
        }
        catch (IOException)
        {
            PostMissingTextureAsset(
                requestKey,
                "textureReadError");
        }
    }

    private async Task<TextureAssetPayload>
        ReadTextureAssetCachedAsync(
            string fullPath)
    {
        var task =
            _textureAssetCache.GetOrAdd(
                fullPath,
                path =>
                    Task.Run(
                        async () =>
                            await BuildTextureAssetAsync(
                                path)));

        try
        {
            return await task;
        }
        catch
        {
            _textureAssetCache.TryRemove(
                fullPath,
                out _);

            throw;
        }
    }

    private static async Task<TextureAssetPayload>
        BuildTextureAssetAsync(
            string fullPath)
    {
        var info =
            new FileInfo(fullPath);

        if (
            !info.Exists ||
            info.Length >
                MaxTextureAssetBytes)
        {
            return TextureAssetPayload.Missing(
                info.Exists
                    ? "textureTooLarge"
                    : "textureNotFound");
        }

        var bytes =
            await File.ReadAllBytesAsync(
                fullPath);

        var extension =
            Path.GetExtension(fullPath)
                .ToLowerInvariant();

        var sourceExtension =
            extension;

        var ddsMetadata =
            extension == ".dds"
                ? OmsiDdsTextureMetadataReader
                    .TryRead(bytes)
                : null;

        int? width = ddsMetadata?.Width;
        int? height = ddsMetadata?.Height;
        string? pixelFormat =
            ddsMetadata?.Format;
        bool? alphaOnly =
            ddsMetadata?.AlphaOnly;
        double? alphaCoverage = null;
        byte? minimumAlpha = null;
        byte? maximumAlpha = null;
        string? rgbaBase64 = null;
        string? base64Data = null;

        if (
            TryReadAlphaStatistics(
                bytes,
                width,
                height,
                alphaOnly,
                out var coverage,
                out var minimum,
                out var maximum))
        {
            alphaCoverage = coverage;
            minimumAlpha = minimum;
            maximumAlpha = maximum;
        }

        if (
            extension == ".bmp" &&
            TryDecodeBmpToRgba(
                bytes,
                out var rgbaBytes,
                out var bitmapWidth,
                out var bitmapHeight))
        {
            width = bitmapWidth;
            height = bitmapHeight;
            pixelFormat =
                GetRgbaDiagnosticLabel(
                    rgbaBytes);
            alphaOnly = false;
            rgbaBase64 =
                Convert.ToBase64String(
                    rgbaBytes);

            // Raw RGBA is consumed directly by Babylon.
            // Do not also encode/send PNG for the same BMP.
            extension = ".bmp";
        }
        else
        {
            base64Data =
                Convert.ToBase64String(
                    bytes);
        }

        return new TextureAssetPayload(
            Exists: true,
            Base64Data: base64Data,
            Extension: extension,
            SourceExtension: sourceExtension,
            RgbaBase64: rgbaBase64,
            MimeType:
                GetTextureMimeType(
                    extension),
            Width: width,
            Height: height,
            PixelFormat: pixelFormat,
            AlphaOnly: alphaOnly,
            AlphaCoverage: alphaCoverage,
            MinimumAlpha: minimumAlpha,
            MaximumAlpha: maximumAlpha,
            ErrorCode: null);
    }

    private void PostMissingTextureAsset(
        string? requestKey,
        string errorCode)
    {
        if (string.IsNullOrWhiteSpace(
                requestKey))
        {
            return;
        }

        PostMessage(new
        {
            type = "textureAssetLoaded",
            requestKey,
            asset = new
            {
                exists = false,
                base64Data =
                    (string?)null,
                extension =
                    (string?)null,
                sourceExtension =
                    (string?)null,
                rgbaBase64 =
                    (string?)null,
                mimeType =
                    (string?)null,
                width =
                    (int?)null,
                height =
                    (int?)null,
                pixelFormat =
                    (string?)null,
                alphaOnly =
                    (bool?)null,
                alphaCoverage =
                    (double?)null,
                minimumAlpha =
                    (byte?)null,
                maximumAlpha =
                    (byte?)null,
                errorCode
            }
        });
    }

    private static bool
        TryReadAlphaStatistics(
            byte[] bytes,
            int? width,
            int? height,
            bool? alphaOnly,
            out double coverage,
            out byte minimum,
            out byte maximum)
    {
        coverage = 0;
        minimum = 0;
        maximum = 0;

        if (
            alphaOnly != true ||
            width is null ||
            height is null ||
            width <= 0 ||
            height <= 0)
        {
            return false;
        }

        try
        {
            var pixelCount =
                checked(
                    width.Value *
                    height.Value);

            const int dataOffset = 128;

            if (
                bytes.Length <
                dataOffset +
                    pixelCount)
            {
                return false;
            }

            var nonZero = 0;
            minimum = byte.MaxValue;
            maximum = byte.MinValue;

            for (
                var index = 0;
                index < pixelCount;
                index++)
            {
                var alpha =
                    bytes[
                        dataOffset +
                        index];

                minimum =
                    Math.Min(
                        minimum,
                        alpha);

                maximum =
                    Math.Max(
                        maximum,
                        alpha);

                if (alpha != 0)
                {
                    nonZero++;
                }
            }

            coverage =
                nonZero /
                (double)pixelCount;

            return true;
        }
        catch (OverflowException)
        {
            coverage = 0;
            minimum = 0;
            maximum = 0;
            return false;
        }
    }

    private static bool
        TryDecodeBmpToRgba(
            byte[] source,
            out byte[] rgbaBytes,
            out int width,
            out int height)
    {
        rgbaBytes = Array.Empty<byte>();
        width = 0;
        height = 0;

        try
        {
            using var input =
                new MemoryStream(
                    source,
                    writable: false);

            var decoder =
                BitmapDecoder.Create(
                    input,
                    BitmapCreateOptions
                        .PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
            {
                return false;
            }

            var frame = decoder.Frames[0];

            var converted =
                new FormatConvertedBitmap(
                    frame,
                    PixelFormats.Bgra32,
                    null,
                    0);

            width = converted.PixelWidth;
            height = converted.PixelHeight;

            if (
                width <= 0 ||
                height <= 0)
            {
                return false;
            }

            var stride =
                checked(width * 4);

            var bgra =
                new byte[
                    checked(
                        stride *
                        height)];

            converted.CopyPixels(
                bgra,
                stride,
                0);

            rgbaBytes =
                new byte[bgra.Length];

            for (
                var index = 0;
                index < bgra.Length;
                index += 4)
            {
                rgbaBytes[index] =
                    bgra[index + 2];
                rgbaBytes[index + 1] =
                    bgra[index + 1];
                rgbaBytes[index + 2] =
                    bgra[index];
                rgbaBytes[index + 3] =
                    bgra[index + 3];
            }

            return rgbaBytes.Length ==
                checked(
                    width *
                    height *
                    4);
        }
        catch
        {
            return false;
        }
    }

    private static string
        GetRgbaDiagnosticLabel(
            byte[] rgbaBytes)
    {
        if (rgbaBytes.Length < 4)
        {
            return "BMP→PNG+RGBA";
        }

        long red = 0;
        long green = 0;
        long blue = 0;
        var samples = 0;

        var pixelCount =
            rgbaBytes.Length / 4;

        var stride =
            Math.Max(
                1,
                pixelCount / 4096);

        for (
            var pixel = 0;
            pixel < pixelCount;
            pixel += stride)
        {
            var offset =
                pixel * 4;

            red +=
                rgbaBytes[offset];
            green +=
                rgbaBytes[offset + 1];
            blue +=
                rgbaBytes[offset + 2];
            samples += 1;
        }

        if (samples == 0)
        {
            return "BMP→PNG+RGBA";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"BMP→PNG+RGBA · RGB médio {red / samples}/{green / samples}/{blue / samples}");
    }

    private static string
        GetTextureMimeType(
            string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".jpeg" => "image/jpeg",
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".dds" =>
                "application/octet-stream",
            ".tga" =>
                "application/octet-stream",
            _ =>
                "application/octet-stream"
        };

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
                        Task.Run(
                            async () =>
                                await _splineDefinitionReader
                                    .ReadAsync(path)));

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
                await ReadSceneryMetadataCachedAsync(
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
                    collisionMeshes,
                    tree = metadata.Tree
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
            var payload =
                await ReadSceneryGeometryCachedAsync(
                    sceneryObjectFullPath);

            PostMessage(new
            {
                type = "sceneryObjectGeometryLoaded",
                sceneryObjectPath,
                geometry = payload
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

    private async Task<OmsiSceneryObjectMetadata>
        ReadSceneryMetadataCachedAsync(
            string sceneryObjectFullPath)
    {
        var task =
            _sceneryMetadataCache.GetOrAdd(
                sceneryObjectFullPath,
                path =>
                    Task.Run(
                        async () =>
                            await _sceneryObjectReader
                                .ReadMetadataAsync(
                                    path)));

        try
        {
            return await task;
        }
        catch
        {
            _sceneryMetadataCache.TryRemove(
                sceneryObjectFullPath,
                out _);

            throw;
        }
    }

    private async Task<SceneryGeometryPayload>
        ReadSceneryGeometryCachedAsync(
            string sceneryObjectFullPath)
    {
        var task =
            _sceneryGeometryCache.GetOrAdd(
                sceneryObjectFullPath,
                path =>
                    Task.Run(
                        async () =>
                        {
                            await _geometryReadSemaphore
                                .WaitAsync();

                            try
                            {
                                return await
                                    BuildSceneryGeometryAsync(
                                        path);
                            }
                            finally
                            {
                                _geometryReadSemaphore
                                    .Release();
                            }
                        }));

        try
        {
            return await task;
        }
        catch
        {
            _sceneryGeometryCache.TryRemove(
                sceneryObjectFullPath,
                out _);

            throw;
        }
    }

    private async Task<SceneryGeometryPayload>
        BuildSceneryGeometryAsync(
            string sceneryObjectFullPath)
    {
        var metadata =
            await ReadSceneryMetadataCachedAsync(
                sceneryObjectFullPath);

        var meshes =
            new List<SceneryMeshGeometryPayload>(
                metadata.MeshPaths.Count);

        for (
            var meshOrdinal = 0;
            meshOrdinal <
                metadata.MeshPaths.Count;
            meshOrdinal++)
        {
            var declaredPath =
                metadata.MeshPaths[
                    meshOrdinal];

            var lodThreshold =
                metadata.MeshLodThresholds
                    .Count >
                meshOrdinal
                    ? metadata
                        .MeshLodThresholds[
                            meshOrdinal]
                    : null;

            var transform =
                metadata.MeshTransforms
                    .Count >
                meshOrdinal
                    ? metadata
                        .MeshTransforms[
                            meshOrdinal]
                    : OmsiSceneryMeshTransform
                        .Identity;

            var materialOverrides =
                metadata.MaterialOverrides
                    .Where(
                        material =>
                            material
                                .MeshOrdinal ==
                            meshOrdinal)
                    .ToArray();

            OmsiO3dGeometry geometry;

            if (!OmsiSceneryMeshPathResolver.TryResolve(
                    _omsiRootPath!,
                    sceneryObjectFullPath,
                    declaredPath,
                    out var meshFullPath))
            {
                geometry =
                    OmsiO3dGeometry.Error(
                        "invalidMeshPath");
            }
            else
            {
                geometry =
                    ReadMeshGeometryCached(
                        meshFullPath);
            }

            meshes.Add(
                new SceneryMeshGeometryPayload(
                    declaredPath,
                    lodThreshold,
                    transform,
                    materialOverrides,
                    geometry));
        }

        return new SceneryGeometryPayload(
            meshes,
            metadata.Tree,
            metadata.UsesAbsoluteHeight);
    }

    private OmsiO3dGeometry
        ReadMeshGeometryCached(
            string meshFullPath)
    {
        var extension =
            Path.GetExtension(
                meshFullPath);

        var isO3d =
            string.Equals(
                extension,
                ".o3d",
                StringComparison.OrdinalIgnoreCase);

        var isDirectX =
            string.Equals(
                extension,
                ".x",
                StringComparison.OrdinalIgnoreCase);

        if (!isO3d && !isDirectX)
        {
            return OmsiO3dGeometry.Error(
                "unsupportedFormat");
        }

        var lazy =
            _meshGeometryCache.GetOrAdd(
                meshFullPath,
                path =>
                    new Lazy<OmsiO3dGeometry>(
                        () =>
                            isO3d
                                ? _o3dGeometryReader
                                    .Read(path)
                                : _directXGeometryReader
                                    .Read(path),
                        LazyThreadSafetyMode
                            .ExecutionAndPublication));

        try
        {
            return lazy.Value;
        }
        catch
        {
            _meshGeometryCache.TryRemove(
                meshFullPath,
                out _);

            throw;
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

    private static bool TryReadSplineLibraryInsertionRequest(
        JsonElement element,
        out SplineLibraryInsertionRequest request)
    {
        request = default!;

        if (
            !TryReadString(
                element,
                "directoryName",
                out var directoryName) ||
            !TryReadString(
                element,
                "splinePath",
                out var splinePath) ||
            !TryReadBoolean(
                element,
                "isHeightSpline",
                out var isHeightSpline) ||
            !TryReadInt32(
                element,
                "targetTileX",
                out var targetTileX) ||
            !TryReadInt32(
                element,
                "targetTileY",
                out var targetTileY) ||
            !TryReadDouble(
                element,
                "x",
                out var x) ||
            !TryReadDouble(
                element,
                "y",
                out var y) ||
            !TryReadDouble(
                element,
                "z",
                out var z) ||
            !TryReadDouble(
                element,
                "rotation",
                out var rotation) ||
            !TryReadDouble(
                element,
                "length",
                out var length) ||
            length < 0 ||
            !TryReadDouble(
                element,
                "radius",
                out var radius) ||
            !TryReadDouble(
                element,
                "gradientStart",
                out var gradientStart) ||
            !TryReadDouble(
                element,
                "gradientEnd",
                out var gradientEnd))
        {
            return false;
        }

        request =
            new SplineLibraryInsertionRequest(
                directoryName!,
                splinePath!,
                isHeightSpline,
                targetTileX,
                targetTileY,
                x,
                y,
                z,
                rotation,
                length,
                radius,
                gradientStart,
                gradientEnd);

        return true;
    }

    private static bool TryReadSplineLinkRequest(
        JsonElement element,
        out SplineLinkRequest request)
    {
        request = default!;

        if (
            !TryReadString(
                element,
                "directoryName",
                out var directoryName) ||
            !TryReadInt32(
                element,
                "tileX",
                out var tileX) ||
            !TryReadInt32(
                element,
                "tileY",
                out var tileY) ||
            !TryReadInt32(
                element,
                "sourceSectionOrdinal",
                out var sourceSectionOrdinal) ||
            !TryReadString(
                element,
                "splinePath",
                out var splinePath) ||
            !TryReadInt32(
                element,
                "splineId",
                out var splineId) ||
            !TryReadInt32(
                element,
                "previousSplineId",
                out var previousSplineId) ||
            !TryReadInt32(
                element,
                "nextSplineId",
                out var nextSplineId) ||
            !TryReadBoolean(
                element,
                "isHeightSpline",
                out var isHeightSpline) ||
            !TryReadInt32(
                element,
                "desiredPreviousSplineId",
                out var desiredPreviousSplineId) ||
            !TryReadInt32(
                element,
                "desiredNextSplineId",
                out var desiredNextSplineId))
        {
            return false;
        }

        request =
            new SplineLinkRequest(
                directoryName!,
                tileX,
                tileY,
                sourceSectionOrdinal,
                splinePath!,
                splineId,
                previousSplineId,
                nextSplineId,
                isHeightSpline,
                desiredPreviousSplineId,
                desiredNextSplineId);

        return true;
    }

    private static bool TryReadSplineInsertionRequest(
        JsonElement element,
        out SplineInsertionRequest request)
    {
        request = default!;

        if (
            !TryReadString(
                element,
                "directoryName",
                out var directoryName) ||
            !TryReadInt32(
                element,
                "sourceTileX",
                out var sourceTileX) ||
            !TryReadInt32(
                element,
                "sourceTileY",
                out var sourceTileY) ||
            !TryReadInt32(
                element,
                "sourceSectionOrdinal",
                out var sourceSectionOrdinal) ||
            !TryReadString(
                element,
                "splinePath",
                out var splinePath) ||
            !TryReadInt32(
                element,
                "splineId",
                out var splineId) ||
            !TryReadInt32(
                element,
                "previousSplineId",
                out var previousSplineId) ||
            !TryReadInt32(
                element,
                "nextSplineId",
                out var nextSplineId) ||
            !TryReadBoolean(
                element,
                "isHeightSpline",
                out var isHeightSpline) ||
            !TryReadInt32(
                element,
                "targetTileX",
                out var targetTileX) ||
            !TryReadInt32(
                element,
                "targetTileY",
                out var targetTileY) ||
            !TryReadDouble(
                element,
                "x",
                out var x) ||
            !TryReadDouble(
                element,
                "y",
                out var y) ||
            !TryReadDouble(
                element,
                "z",
                out var z) ||
            !TryReadDouble(
                element,
                "rotation",
                out var rotation) ||
            !TryReadDouble(
                element,
                "length",
                out var length) ||
            length < 0 ||
            !TryReadDouble(
                element,
                "radius",
                out var radius) ||
            !TryReadDouble(
                element,
                "gradientStart",
                out var gradientStart) ||
            !TryReadDouble(
                element,
                "gradientEnd",
                out var gradientEnd))
        {
            return false;
        }

        request =
            new SplineInsertionRequest(
                directoryName!,
                sourceTileX,
                sourceTileY,
                sourceSectionOrdinal,
                splinePath!,
                splineId,
                previousSplineId,
                nextSplineId,
                isHeightSpline,
                targetTileX,
                targetTileY,
                x,
                y,
                z,
                rotation,
                length,
                radius,
                gradientStart,
                gradientEnd);

        return true;
    }

    private static bool TryReadSplineTransformRequests(
        JsonElement element,
        out IReadOnlyList<SplineTransformRequest> requests)
    {
        requests =
            Array.Empty<SplineTransformRequest>();

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
            new List<SplineTransformRequest>();

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
                    "splinePath",
                    out var splinePath) ||
                !TryReadInt32(
                    edit,
                    "splineId",
                    out var splineId) ||
                !TryReadInt32(
                    edit,
                    "previousSplineId",
                    out var previousSplineId) ||
                !TryReadInt32(
                    edit,
                    "nextSplineId",
                    out var nextSplineId) ||
                !TryReadBoolean(
                    edit,
                    "isHeightSpline",
                    out var isHeightSpline) ||
                !TryReadDouble(
                    edit,
                    "x",
                    out var x) ||
                !TryReadDouble(
                    edit,
                    "z",
                    out var z) ||
                !TryReadDouble(
                    edit,
                    "y",
                    out var y) ||
                !TryReadDouble(
                    edit,
                    "rotation",
                    out var rotation) ||
                !TryReadDouble(
                    edit,
                    "length",
                    out var length) ||
                !TryReadDouble(
                    edit,
                    "radius",
                    out var radius) ||
                !TryReadDouble(
                    edit,
                    "gradientStart",
                    out var gradientStart) ||
                !TryReadDouble(
                    edit,
                    "gradientEnd",
                    out var gradientEnd))
            {
                return false;
            }

            result.Add(
                new SplineTransformRequest(
                    tileX,
                    tileY,
                    sourceSectionOrdinal,
                    splinePath!,
                    splineId,
                    previousSplineId,
                    nextSplineId,
                    isHeightSpline,
                    x,
                    z,
                    y,
                    rotation,
                    length,
                    radius,
                    gradientStart,
                    gradientEnd));
        }

        requests = result;
        return result.Count > 0;
    }

    private static bool TryReadBoolean(
        JsonElement element,
        string propertyName,
        out bool value)
    {
        value = false;

        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return false;
        }

        if (
            property.ValueKind ==
                JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (
            property.ValueKind ==
                JsonValueKind.False)
        {
            return true;
        }

        return false;
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

    private sealed record TextureAssetPayload(
        bool Exists,
        string? Base64Data,
        string? Extension,
        string? SourceExtension,
        string? RgbaBase64,
        string? MimeType,
        int? Width,
        int? Height,
        string? PixelFormat,
        bool? AlphaOnly,
        double? AlphaCoverage,
        byte? MinimumAlpha,
        byte? MaximumAlpha,
        string? ErrorCode)
    {
        public static TextureAssetPayload
            Missing(
                string errorCode) =>
            new(
                Exists: false,
                Base64Data: null,
                Extension: null,
                SourceExtension: null,
                RgbaBase64: null,
                MimeType: null,
                Width: null,
                Height: null,
                PixelFormat: null,
                AlphaOnly: null,
                AlphaCoverage: null,
                MinimumAlpha: null,
                MaximumAlpha: null,
                ErrorCode: errorCode);
    }

    private sealed record SceneryGeometryPayload(
        IReadOnlyList<SceneryMeshGeometryPayload> Meshes,
        OmsiSceneryTreeDefinition? Tree,
        bool UsesAbsoluteHeight);

    private sealed record SceneryMeshGeometryPayload(
        string DeclaredPath,
        double? LodThreshold,
        OmsiSceneryMeshTransform Transform,
        IReadOnlyList<OmsiSceneryMaterialOverride> MaterialOverrides,
        OmsiO3dGeometry Geometry);

    private sealed record SplineLibraryEntry(
        string SplinePath,
        string FileName);

    private sealed record SplineLibraryInsertionRequest(
        string DirectoryName,
        string SplinePath,
        bool IsHeightSpline,
        int TargetTileX,
        int TargetTileY,
        double X,
        double Y,
        double Z,
        double Rotation,
        double Length,
        double Radius,
        double GradientStart,
        double GradientEnd);

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

    private sealed record SplineMapEntry(
        int TileX,
        int TileY,
        string RelativeMapPath,
        OmsiPlacedSpline Spline);

    private sealed record SplineLinkRequest(
        string DirectoryName,
        int TileX,
        int TileY,
        int SourceSectionOrdinal,
        string SplinePath,
        int SplineId,
        int PreviousSplineId,
        int NextSplineId,
        bool IsHeightSpline,
        int DesiredPreviousSplineId,
        int DesiredNextSplineId);

    private sealed record SplineInsertionRequest(
        string DirectoryName,
        int SourceTileX,
        int SourceTileY,
        int SourceSectionOrdinal,
        string SplinePath,
        int SplineId,
        int PreviousSplineId,
        int NextSplineId,
        bool IsHeightSpline,
        int TargetTileX,
        int TargetTileY,
        double X,
        double Y,
        double Z,
        double Rotation,
        double Length,
        double Radius,
        double GradientStart,
        double GradientEnd);

    private sealed record SplineTransformRequest(
        int TileX,
        int TileY,
        int SourceSectionOrdinal,
        string SplinePath,
        int SplineId,
        int PreviousSplineId,
        int NextSplineId,
        bool IsHeightSpline,
        double X,
        double Z,
        double Y,
        double Rotation,
        double Length,
        double Radius,
        double GradientStart,
        double GradientEnd);

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

    private void SetFullScreen(
        bool enabled)
    {
        if (_isFullScreen == enabled)
        {
            PostMessage(new
            {
                type = "fullScreenChanged",
                enabled = _isFullScreen
            });

            return;
        }

        if (enabled)
        {
            _windowStyleBeforeFullScreen =
                WindowStyle;
            _windowStateBeforeFullScreen =
                WindowState;
            _resizeModeBeforeFullScreen =
                ResizeMode;

            WindowStyle =
                WindowStyle.None;
            ResizeMode =
                ResizeMode.NoResize;
            WindowState =
                WindowState.Maximized;
        }
        else
        {
            WindowState =
                WindowState.Normal;
            WindowStyle =
                _windowStyleBeforeFullScreen;
            ResizeMode =
                _resizeModeBeforeFullScreen;
            WindowState =
                _windowStateBeforeFullScreen;
        }

        _isFullScreen = enabled;

        PostMessage(new
        {
            type = "fullScreenChanged",
            enabled = _isFullScreen
        });
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
