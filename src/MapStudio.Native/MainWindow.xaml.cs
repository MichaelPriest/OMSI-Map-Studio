using MapStudio.Native.Services;
using MapStudio.Renderer.Scene;
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

        Viewport.TransformEditPending +=
            edit =>
            {
                _session.StageTransformEdit(
                    edit);

                SaveChangesButton.IsEnabled =
                    _session.PendingTransformCount >
                    0;

                UndoButton.IsEnabled =
                    Viewport.CanUndo;

                RedoButton.IsEnabled =
                    Viewport.CanRedo;

                StatusText.Text =
                    $"{_session.PendingTransformCount} alteração(ões) pendente(s).";
            };
    }

    private void OnUndoClick(
        object sender,
        RoutedEventArgs e)
    {
        if (Viewport.Undo())
        {
            StatusText.Text =
                "Transformação desfeita.";

            UndoButton.IsEnabled =
                Viewport.CanUndo;

            RedoButton.IsEnabled =
                Viewport.CanRedo;
        }
    }

    private void OnRedoClick(
        object sender,
        RoutedEventArgs e)
    {
        if (Viewport.Redo())
        {
            StatusText.Text =
                "Transformação refeita.";

            UndoButton.IsEnabled =
                Viewport.CanUndo;

            RedoButton.IsEnabled =
                Viewport.CanRedo;
        }
    }

    private void OnSnapClick(
        object sender,
        RoutedEventArgs e)
    {
        var enabled =
            !Viewport.SnapEnabled;

        Viewport.SetSnapEnabled(
            enabled);

        SnapButton.Content =
            enabled
                ? "Snap: 0,25m / 5°"
                : "Snap: off";

        StatusText.Text =
            enabled
                ? "Snap de transformação ativo."
                : "Snap de transformação desativado.";
    }

    private async void OnSaveChangesClick(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            if (
                _session.PendingTransformCount ==
                0)
            {
                SaveChangesButton.IsEnabled =
                    false;

                return;
            }

            var count =
                _session
                    .PendingTransformCount;

            StatusText.Text =
                "Salvando alterações com backup...";

            await _session
                .SavePendingTransformsAsync();

            SaveChangesButton.IsEnabled =
                false;

            UndoButton.IsEnabled =
                false;

            RedoButton.IsEnabled =
                false;

            StatusText.Text =
                $"{count} alteração(ões) salva(s) com backup seguro.";

            SelectionText.Text =
                "Alterações persistidas no mapa OMSI.";
        }
        catch (Exception exception)
        {
            SaveChangesButton.IsEnabled =
                _session.PendingTransformCount >
                0;

            StatusText.Text =
                $"Falha ao salvar alterações: {exception.Message}";
        }
    }

    private void OnMoveGizmoClick(
        object sender,
        RoutedEventArgs e)
    {
        Viewport.SetGizmoMode(
            NativeGizmoMode.Move);

        StatusText.Text =
            "Ferramenta mover ativa.";
    }

    private void OnRotateGizmoClick(
        object sender,
        RoutedEventArgs e)
    {
        Viewport.SetGizmoMode(
            NativeGizmoMode.Rotate);

        StatusText.Text =
            "Ferramenta rotacionar ativa.";
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

            await Viewport
                .SetMapSnapshotAsync(
                    snapshot,
                    _session
                        .OmsiRootPath!);

            SaveChangesButton.IsEnabled =
                false;

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
