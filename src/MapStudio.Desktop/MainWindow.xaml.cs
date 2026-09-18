using System.IO;
using System.Text.Json;
using System.Windows;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MapStudio.Desktop;

public partial class MainWindow : Window
{
    private readonly OmsiMapCatalog _mapCatalog = new();
    private readonly OmsiTileReader _tileReader = new();
    private readonly OmsiSceneryObjectReader _sceneryObjectReader = new();
    private readonly OmsiO3dHeaderReader _o3dHeaderReader = new();
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private IReadOnlyDictionary<string, OmsiMapDescriptor> _knownMaps =
        new Dictionary<string, OmsiMapDescriptor>(
            StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _knownSceneryObjectPaths =
        new(StringComparer.OrdinalIgnoreCase);

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
            using var message = JsonDocument.Parse(
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

                case "loadMapObjects":
                    if (TryReadString(
                            message.RootElement,
                            "directoryName",
                            out var directoryName))
                    {
                        await LoadMapObjectsAsync(directoryName);
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
            }
        }
        catch (JsonException)
        {
            PostInvalidMessage();
        }
    }

    private async Task SelectOmsiRootAsync()
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
                type = "selectionCancelled"
            });
            return;
        }

        var rootPath = dialog.FolderName;
        var mapsPath = Path.Combine(rootPath, "maps");

        if (!Directory.Exists(mapsPath))
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidOmsiRoot",
                detail = rootPath
            });
            return;
        }

        try
        {
            var maps = await _mapCatalog.DiscoverAsync(rootPath);

            _omsiRootPath = rootPath;
            _knownSceneryObjectPaths.Clear();
            _knownMaps = maps.ToDictionary(
                map => map.DirectoryName,
                StringComparer.OrdinalIgnoreCase);

            PostMessage(new
            {
                type = "omsiInstallationLoaded",
                rootPath,
                maps = maps.Select(map => new
                {
                    map.DirectoryName,
                    map.DisplayName,
                    map.DirectoryPath,
                    map.GlobalConfigPath,
                    map.UsesWorldCoordinates,
                    tiles = map.Tiles.Select(tile => new
                    {
                        tile.X,
                        tile.Y,
                        tile.RelativeMapPath,
                        fileExists =
                            tile.Summary?.Exists ?? false,
                        objectCount =
                            tile.Summary?.ObjectCount ?? 0,
                        splineCount =
                            tile.Summary?.SplineCount ?? 0,
                        splineAttachmentCount =
                            tile.Summary?.SplineAttachmentCount ?? 0
                    })
                })
            });
        }
        catch (UnauthorizedAccessException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "accessDenied",
                detail = rootPath
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

    private async Task LoadMapObjectsAsync(
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
            var objects = new List<object>();

            foreach (var tile in map.Tiles)
            {
                if (tile.Summary?.Exists != true)
                {
                    continue;
                }

                if (!OmsiMapPathResolver.TryResolveTilePath(
                        map.DirectoryPath,
                        tile.RelativeMapPath,
                        out var tilePath))
                {
                    continue;
                }

                foreach (var placedObject in
                    await _tileReader.ReadObjectsAsync(tilePath))
                {
                    _knownSceneryObjectPaths.Add(
                        placedObject.SceneryObjectPath);

                    objects.Add(new
                    {
                        tileX = tile.X,
                        tileY = tile.Y,
                        placedObject.HeaderValue,
                        placedObject.SceneryObjectPath,
                        placedObject.ObjectId,
                        placedObject.X,
                        placedObject.Y,
                        placedObject.Z,
                        placedObject.Rotation,
                        placedObject.Pitch,
                        placedObject.Bank
                    });
                }
            }

            PostMessage(new
            {
                type = "mapObjectsLoaded",
                map.DirectoryName,
                map.UsesWorldCoordinates,
                objects
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

    private async Task LoadSceneryObjectMetadataAsync(
        string? sceneryObjectPath)
    {
        if (_omsiRootPath is null ||
            string.IsNullOrWhiteSpace(sceneryObjectPath) ||
            !_knownSceneryObjectPaths.Contains(
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

            if (fileExists &&
                string.Equals(
                    Path.GetExtension(fullPath),
                    ".o3d",
                    StringComparison.OrdinalIgnoreCase))
            {
                o3d =
                    await _o3dHeaderReader.ReadAsync(
                        fullPath);
            }

            results.Add(new
            {
                declaredPath,
                fileExists,
                o3d
            });
        }

        return results;
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
        var json = JsonSerializer.Serialize(
            payload,
            _jsonOptions);

        EditorWebView.CoreWebView2
            .PostWebMessageAsJson(json);
    }
}
