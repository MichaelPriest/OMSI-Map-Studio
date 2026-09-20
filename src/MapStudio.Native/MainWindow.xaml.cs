using MapStudio.Core.Omsi.Indexing;
using MapStudio.Native.Services;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MapStudio.Native;

public sealed partial class MainWindow : Window
{
    private readonly OmsiNativeSession _session =
        new();

    private readonly IntPtr _windowHandle;

    private NativeSelectionInfo?
        _selectionInfo;

    private IReadOnlyList<
        NativeExplorerItem>
        _explorerItems =
            Array.Empty<
                NativeExplorerItem>();

    private bool _synchronizingExplorer;

    private IReadOnlyList<
        OmsiAssetIndexEntry>
        _assetLibraryItems =
            Array.Empty<
                OmsiAssetIndexEntry>();

    private bool _libraryMode;

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

        Viewport.SelectionChanged +=
            info =>
            {
                _selectionInfo =
                    info;

                ApplyInspectorButton.IsEnabled =
                    info is not null;

                if (info is null)
                {
                    InspectorTypeText.Text =
                        "Tipo: —";

                    InspectorAssetText.Text =
                        "Arquivo: —";

                    InspectorTileText.Text =
                        "Tile: —";

                    InspectorXBox.Value =
                        double.NaN;

                    InspectorYBox.Value =
                        double.NaN;

                    InspectorZBox.Value =
                        double.NaN;

                    InspectorRotationBox.Value =
                        double.NaN;

                    InspectorObjectFields.Visibility =
                        Visibility.Collapsed;

                    InspectorSplineFields.Visibility =
                        Visibility.Collapsed;

                    return;
                }

                var isObject =
                    info.Kind ==
                    PickingKind.Object;

                InspectorTypeText.Text =
                    $"Tipo: {(isObject ? "Objeto" : "Spline")} #{info.EntityId}";

                InspectorAssetText.Text =
                    $"Arquivo: {info.AssetPath}";

                InspectorTileText.Text =
                    $"Tile: {info.TileX}, {info.TileY}";

                InspectorXBox.Value =
                    info.X;

                InspectorYBox.Value =
                    info.Y;

                InspectorZBox.Value =
                    info.Z;

                InspectorRotationBox.Value =
                    info.Rotation;

                InspectorObjectFields.Visibility =
                    isObject
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                InspectorSplineFields.Visibility =
                    isObject
                        ? Visibility.Collapsed
                        : Visibility.Visible;

                if (isObject)
                {
                    InspectorPitchBox.Value =
                        info.Pitch ??
                        0;

                    InspectorBankBox.Value =
                        info.Bank ??
                        0;
                }
                else
                {
                    InspectorLengthBox.Value =
                        info.Length ??
                        0;

                    InspectorRadiusBox.Value =
                        info.Radius ??
                        0;

                    InspectorGradientStartBox.Value =
                        info.GradientStart ??
                        0;

                    InspectorGradientEndBox.Value =
                        info.GradientEnd ??
                        0;
                }

                SynchronizeExplorerSelection(
                    info);
            };
    }

    private void OnExplorerSearchTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_libraryMode)
        {
            RefreshLibraryFilter();
        }
        else
        {
            RefreshExplorerFilter();
        }
    }

    private void OnSceneExplorerModeClick(
        object sender,
        RoutedEventArgs e)
    {
        _libraryMode =
            false;

        ExplorerListView.Visibility =
            Visibility.Visible;

        AssetLibraryPanel.Visibility =
            Visibility.Collapsed;

        ExplorerSearchBox.PlaceholderText =
            "Buscar objetos e splines...";

        RefreshExplorerFilter();
    }

    private async void OnLibraryModeClick(
        object sender,
        RoutedEventArgs e)
    {
        _libraryMode =
            true;

        ExplorerListView.Visibility =
            Visibility.Collapsed;

        AssetLibraryPanel.Visibility =
            Visibility.Visible;

        ExplorerSearchBox.PlaceholderText =
            "Buscar na biblioteca...";

        await LoadAssetLibraryAsync();
    }

    private async void OnRefreshLibraryClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_session.OmsiRootPath is null)
        {
            return;
        }

        try
        {
            RefreshLibraryButton.IsEnabled =
                false;

            LibraryStatusText.Text =
                "Indexando instalação OMSI...";

            var progress =
                new Progress<
                    OmsiAssetIndexProgress>(
                    value =>
                    {
                        LibraryStatusText.Text =
                            $"Indexando... {value.ExaminedFiles} arquivos · " +
                            $"{value.CandidateFiles} assets";
                    });

            var result =
                await _session
                    .RefreshAssetLibraryAsync(
                        progress);

            LibraryStatusText.Text =
                $"Índice atualizado: {result.TotalEntries} assets · " +
                $"+{result.AddedFiles} · ~{result.UpdatedFiles} · " +
                $"-{result.RemovedFiles}";

            await LoadAssetLibraryAsync();
        }
        catch (Exception exception)
        {
            LibraryStatusText.Text =
                $"Falha ao indexar: {exception.Message}";
        }
        finally
        {
            RefreshLibraryButton.IsEnabled =
                _session.OmsiRootPath is
                    not null;
        }
    }

    private async void OnLibraryKindSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (
            _libraryMode &&
            _session.OmsiRootPath is
                not null)
        {
            await LoadAssetLibraryAsync();
        }
    }

    private void OnAssetLibraryDoubleTapped(
        object sender,
        DoubleTappedRoutedEventArgs e)
    {
        if (
            AssetLibraryListView.SelectedItem is not
                OmsiAssetIndexEntry asset)
        {
            return;
        }

        var normalized =
            asset.RelativePath
                .Replace(
                    '\\',
                    '/');

        var usage =
            _explorerItems
                .FirstOrDefault(
                    item =>
                        string.Equals(
                            item.AssetPath
                                .Replace(
                                    '\\',
                                    '/'),
                            normalized,
                            StringComparison
                                .OrdinalIgnoreCase));

        if (usage is not null)
        {
            _libraryMode =
                false;

            ExplorerListView.Visibility =
                Visibility.Visible;

            AssetLibraryPanel.Visibility =
                Visibility.Collapsed;

            ExplorerSearchBox.PlaceholderText =
                "Buscar objetos e splines...";

            RefreshExplorerFilter();

            Viewport.SelectExplorerItem(
                usage,
                focus: true);

            StatusText.Text =
                $"Uso do asset focado no mapa: {asset.RelativePath}.";

            return;
        }

        StatusText.Text =
            $"Asset indexado: {asset.RelativePath} · prévia 3D nativa será o próximo checkpoint.";
    }

    private void OnExplorerSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (
            _synchronizingExplorer ||
            ExplorerListView.SelectedItem is not
                NativeExplorerItem item)
        {
            return;
        }

        Viewport.SelectExplorerItem(
            item,
            focus: false);
    }

    private void OnExplorerDoubleTapped(
        object sender,
        DoubleTappedRoutedEventArgs e)
    {
        if (
            ExplorerListView.SelectedItem is
                NativeExplorerItem item)
        {
            Viewport.SelectExplorerItem(
                item,
                focus: true);

            StatusText.Text =
                $"Câmera focada em {item.DisplayText}.";
        }
    }

    private void RefreshExplorer()
    {
        _explorerItems =
            Viewport.GetExplorerItems();

        RefreshExplorerFilter();
    }

    private void RefreshExplorerFilter()
    {
        var query =
            ExplorerSearchBox.Text
                .Trim();

        IEnumerable<
            NativeExplorerItem> items =
            _explorerItems;

        if (
            !string.IsNullOrWhiteSpace(
                query))
        {
            items =
                items.Where(
                    item =>
                        item.DisplayText
                            .Contains(
                                query,
                                StringComparison
                                    .OrdinalIgnoreCase) ||
                        item.AssetPath
                            .Contains(
                                query,
                                StringComparison
                                    .OrdinalIgnoreCase) ||
                        item.TileX
                            .ToString()
                            .Contains(
                                query,
                                StringComparison
                                    .OrdinalIgnoreCase) ||
                        item.TileY
                            .ToString()
                            .Contains(
                                query,
                                StringComparison
                                    .OrdinalIgnoreCase));
        }

        ExplorerListView.ItemsSource =
            items.ToArray();
    }

    private async Task LoadAssetLibraryAsync()
    {
        if (_session.OmsiRootPath is null)
        {
            _assetLibraryItems =
                Array.Empty<
                    OmsiAssetIndexEntry>();

            AssetLibraryListView.ItemsSource =
                _assetLibraryItems;

            return;
        }

        try
        {
            var kind =
                GetSelectedLibraryKind();

            _assetLibraryItems =
                await _session
                    .GetAssetLibraryAsync(
                        kind);

            var stats =
                await _session
                    .GetAssetLibraryStatisticsAsync();

            LibraryStatusText.Text =
                stats.TotalEntries == 0
                    ? "Índice vazio. Clique em Atualizar para catalogar a instalação."
                    : $"{stats.TotalEntries} assets · " +
                      $"{stats.SceneryObjects} SCO · {stats.Splines} SLI · " +
                      $"{stats.Models} modelos · {stats.Textures} texturas";

            RefreshLibraryFilter();
        }
        catch (Exception exception)
        {
            LibraryStatusText.Text =
                $"Falha ao abrir biblioteca: {exception.Message}";
        }
    }

    private OmsiAssetKind?
        GetSelectedLibraryKind() =>
        LibraryKindComboBox
            .SelectedIndex switch
        {
            1 =>
                OmsiAssetKind
                    .SceneryObject,
            2 =>
                OmsiAssetKind
                    .Spline,
            3 =>
                OmsiAssetKind
                    .Model,
            4 =>
                OmsiAssetKind
                    .Texture,
            _ =>
                null
        };

    private void RefreshLibraryFilter()
    {
        var query =
            ExplorerSearchBox.Text
                .Trim();

        IEnumerable<
            OmsiAssetIndexEntry> items =
            _assetLibraryItems;

        if (
            !string.IsNullOrWhiteSpace(
                query))
        {
            items =
                items.Where(
                    item =>
                        item.RelativePath
                            .Contains(
                                query,
                                StringComparison
                                    .OrdinalIgnoreCase));
        }

        AssetLibraryListView.ItemsSource =
            items.ToArray();
    }

    private void SynchronizeExplorerSelection(
        NativeSelectionInfo info)
    {
        var item =
            _explorerItems
                .FirstOrDefault(
                    candidate =>
                        candidate.Kind ==
                            info.Kind &&
                        candidate.EntityId ==
                            info.EntityId &&
                        candidate.TileX ==
                            info.TileX &&
                        candidate.TileY ==
                            info.TileY);

        if (item is null)
        {
            return;
        }

        _synchronizingExplorer =
            true;

        try
        {
            ExplorerListView.SelectedItem =
                item;

            ExplorerListView.ScrollIntoView(
                item);
        }
        finally
        {
            _synchronizingExplorer =
                false;
        }
    }

    private void OnApplyInspectorClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectionInfo is null)
        {
            return;
        }

        var numericValues =
            new[]
            {
                InspectorXBox.Value,
                InspectorYBox.Value,
                InspectorZBox.Value,
                InspectorRotationBox.Value
            };

        if (
            numericValues.Any(
                value =>
                    !double.IsFinite(
                        value)))
        {
            StatusText.Text =
                "Inspector contém valor numérico inválido.";

            return;
        }

        var current =
            _selectionInfo;

        var updated =
            current with
            {
                X =
                    InspectorXBox.Value,
                Y =
                    InspectorYBox.Value,
                Z =
                    InspectorZBox.Value,
                Rotation =
                    InspectorRotationBox.Value,
                Pitch =
                    current.Kind ==
                        PickingKind.Object
                        ? InspectorPitchBox.Value
                        : null,
                Bank =
                    current.Kind ==
                        PickingKind.Object
                        ? InspectorBankBox.Value
                        : null,
                Length =
                    current.Kind ==
                        PickingKind.Spline
                        ? InspectorLengthBox.Value
                        : null,
                Radius =
                    current.Kind ==
                        PickingKind.Spline
                        ? InspectorRadiusBox.Value
                        : null,
                GradientStart =
                    current.Kind ==
                        PickingKind.Spline
                        ? InspectorGradientStartBox.Value
                        : null,
                GradientEnd =
                    current.Kind ==
                        PickingKind.Spline
                        ? InspectorGradientEndBox.Value
                        : null
            };

        var optionalValues =
            current.Kind ==
                PickingKind.Object
                ? new[]
                {
                    updated.Pitch
                        .GetValueOrDefault(),
                    updated.Bank
                        .GetValueOrDefault()
                }
                : new[]
                {
                    updated.Length
                        .GetValueOrDefault(),
                    updated.Radius
                        .GetValueOrDefault(),
                    updated.GradientStart
                        .GetValueOrDefault(),
                    updated.GradientEnd
                        .GetValueOrDefault()
                };

        if (
            optionalValues.Any(
                value =>
                    !double.IsFinite(
                        value)))
        {
            StatusText.Text =
                "Inspector contém valor numérico inválido.";

            return;
        }

        if (Viewport.ApplySelectionInfo(
                updated))
        {
            StatusText.Text =
                "Valores do Inspector aplicados ao estado OMSI.";
        }
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

            RefreshLibraryButton.IsEnabled =
                true;

            await LoadAssetLibraryAsync();

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

            RefreshExplorer();

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
