using MapStudio.Native.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MapStudio.Native;

public sealed partial class MainWindow : Window
{
    private readonly OmsiNativeSession _session =
        new();

    private readonly IntPtr _windowHandle;

    public MainWindow()
    {
        InitializeComponent();

        _windowHandle =
            WindowNative.GetWindowHandle(
                this);

        var windowId =
            Microsoft.UI.Win32Interop
                .GetWindowIdFromWindow(
                    _windowHandle);

        var appWindow =
            AppWindow.GetFromWindowId(
                windowId);

        appWindow.Resize(
            new SizeInt32(
                1440,
                900));

        Viewport.PointerStatusChanged +=
            (_, message) =>
            {
                StatusText.Text = message;
            };

        Viewport.SelectionStatusChanged +=
            (_, message) =>
            {
                SelectionText.Text = message;
            };
    }

    private async void OnOpenOmsiClick(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var root =
                await PickFolderAsync();

            if (
                string.IsNullOrWhiteSpace(
                    root))
            {
                return;
            }

            StatusText.Text =
                "Lendo catálogo de mapas...";

            var maps =
                await _session
                    .SelectOmsiRootAsync(
                        root);

            RootText.Text =
                $"OMSI: {root}\nMapas encontrados: {maps.Count}";

            OpenMapButton.IsEnabled =
                true;

            OpenMapMenuItem.IsEnabled =
                true;

            StatusText.Text =
                "Instalação OMSI carregada pelo Core nativo.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao abrir OMSI: {exception.Message}";
        }
    }

    private async void OnOpenMapClick(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            if (
                _session.OmsiRootPath is
                    null)
            {
                StatusText.Text =
                    "Selecione primeiro a instalação do OMSI.";
                return;
            }

            var mapDirectory =
                await PickFolderAsync();

            if (
                string.IsNullOrWhiteSpace(
                    mapDirectory))
            {
                return;
            }

            StatusText.Text =
                "Carregando região inicial do mapa...";

            var snapshot =
                await _session
                    .OpenMapAsync(
                        mapDirectory);

            var activeTile =
                snapshot.ActiveTile is null
                    ? "—"
                    : $"{snapshot.ActiveTile.X}, {snapshot.ActiveTile.Y}";

            MapText.Text =
                $"{snapshot.Map.DisplayName}\n" +
                $"Tiles carregados: {snapshot.Tiles.Count} / {snapshot.Map.Tiles.Count}\n" +
                $"Objetos: {snapshot.ObjectCount} · Splines: {snapshot.SplineCount}\n" +
                $"Terrenos: {snapshot.TerrainCount} · Tile ativo: {activeTile}";

            Viewport.SetMapSnapshot(
                snapshot);

            StatusText.Text =
                $"Mapa {snapshot.Map.DisplayName} carregado pelo MapStudio.Core.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao abrir mapa: {exception.Message}";
        }
    }

    private async Task<string?>
        PickFolderAsync()
    {
        var picker =
            new FolderPicker
            {
                SuggestedStartLocation =
                    PickerLocationId
                        .ComputerFolder
            };

        picker.FileTypeFilter.Add("*");

        InitializeWithWindow.Initialize(
            picker,
            _windowHandle);

        var folder =
            await picker
                .PickSingleFolderAsync();

        return folder?.Path;
    }
}
