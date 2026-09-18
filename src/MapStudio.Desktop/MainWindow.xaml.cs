using System.IO;
using System.Text.Json;
using System.Windows;
using MapStudio.Core.Omsi.Maps;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MapStudio.Desktop;

public partial class MainWindow : Window
{
    private readonly OmsiMapCatalog _mapCatalog = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await EditorWebView.EnsureCoreWebView2Async();
        EditorWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        var devUrl = Environment.GetEnvironmentVariable("MAPSTUDIO_DEV_URL");

        if (Uri.TryCreate(devUrl, UriKind.Absolute, out var developmentUri))
        {
            EditorWebView.Source = developmentUri;
            return;
        }

        var uiDirectory = Path.Combine(AppContext.BaseDirectory, "ui");
        var indexPath = Path.Combine(uiDirectory, "index.html");

        if (!File.Exists(indexPath))
        {
            EditorWebView.NavigateToString(
                "<html><body style='font-family:Segoe UI;background:#101318;color:#fff;padding:32px'>" +
                "<h1>OMSI Map Studio</h1><p>UI build not found.</p>" +
                "<p>Run npm run build in src/MapStudio.UI or set MAPSTUDIO_DEV_URL.</p>" +
                "</body></html>");
            return;
        }

        EditorWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.mapstudio",
            uiDirectory,
            CoreWebView2HostResourceAccessKind.DenyCors);

        EditorWebView.Source = new Uri("https://app.mapstudio/index.html");
    }

    private async void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);

            if (!message.RootElement.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            switch (typeElement.GetString())
            {
                case "selectOmsiRoot":
                    await SelectOmsiRootAsync();
                    break;
            }
        }
        catch (JsonException)
        {
            PostMessage(new
            {
                type = "hostError",
                code = "invalidMessage"
            });
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
                    tiles = map.Tiles.Select(tile => new
                    {
                        tile.X,
                        tile.Y,
                        tile.RelativeMapPath,
                        fileExists = tile.Summary?.Exists ?? false,
                        objectCount = tile.Summary?.ObjectCount ?? 0,
                        splineCount = tile.Summary?.SplineCount ?? 0,
                        splineAttachmentCount = tile.Summary?.SplineAttachmentCount ?? 0
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

    private void PostMessage(object payload)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        EditorWebView.CoreWebView2.PostWebMessageAsJson(json);
    }
}
