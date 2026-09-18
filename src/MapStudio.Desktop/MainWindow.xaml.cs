using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace MapStudio.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await EditorWebView.EnsureCoreWebView2Async();

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
}
