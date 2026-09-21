using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Timetables;
using MapStudio.Core.Omsi.Traffic;
using MapStudio.Native.Services;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MapStudio.Native;

public sealed partial class MainWindow : Window
{
    private sealed record TransportExplorerItem(
        string Kind,
        string Key,
        string DisplayText,
        string Detail);

    private sealed record TrafficRuleExplorerItem(
        PickingKind OwnerKind,
        int EntityId,
        int TileX,
        int TileY,
        string AssetPath,
        OmsiTrafficRule Rule,
        string DisplayText,
        string Detail);

    private readonly OmsiNativeSession _session =
        new();

    private readonly IntPtr _windowHandle;

    private readonly AppWindow _appWindow;

    private NativeSelectionInfo?
        _selectionInfo;

    private NativeTerrainEditPoint?
        _terrainEditPoint;

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
    private bool _transportMode;
    private bool _trafficMode;

    private readonly DispatcherTimer
        _trafficPreviewTimer =
            new();

    private IReadOnlyList<
        NativeTrafficLightProgramInfo>
        _trafficPrograms =
            Array.Empty<
                NativeTrafficLightProgramInfo>();

    private IReadOnlyList<
        TrafficRuleExplorerItem>
        _trafficRuleItems =
            Array.Empty<
                TrafficRuleExplorerItem>();

    private IReadOnlyList<
        OmsiUnscheduledVehicleGroup>
        _trafficVehicleGroups =
            Array.Empty<
                OmsiUnscheduledVehicleGroup>();

    private OmsiTimetableCatalog?
        _timetableCatalog;

    private IReadOnlyList<
        TransportExplorerItem>
        _transportItems =
            Array.Empty<
                TransportExplorerItem>();

    private CancellationTokenSource?
        _assetPreviewCancellation;

    private bool _resizingExplorerPanel;
    private bool _resizingInspectorPanel;
    private bool _fullMapMode = true;
    private bool _mapLoadModeChanging;

    private double _explorerPanelWidth =
        300;

    private double _inspectorPanelWidth =
        320;

    public MainWindow()
    {
        InitializeComponent();

        _trafficPreviewTimer.Interval =
            TimeSpan.FromMilliseconds(
                250);

        _trafficPreviewTimer.Tick +=
            OnTrafficPreviewTimerTick;

        SelectionFilterComboBox.SelectionChanged +=
            OnSelectionFilterChanged;

        _windowHandle =
            WindowNative.GetWindowHandle(
                this);

        var windowId =
            Microsoft.UI.Win32Interop
                .GetWindowIdFromWindow(
                    _windowHandle);

        _appWindow =
            AppWindow.GetFromWindowId(
                windowId);

        _appWindow.Resize(
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

        Viewport.SceneryPlacementRequested +=
            async request =>
            {
                await HandleSceneryPlacementAsync(
                    request);
            };

        Viewport.SplinePlacementRequested +=
            async request =>
            {
                await HandleSplinePlacementAsync(
                    request);
            };

        Viewport.TerrainPointSelected +=
            point =>
            {
                _terrainEditPoint =
                    point;

                TerrainTargetHeightBox.Value =
                    point.Height;

                TerrainPointText.Text =
                    $"Tile {point.Tile.X},{point.Tile.Y} · " +
                    $"X {point.LocalX:F2} · Y {point.LocalY:F2} · " +
                    $"altura {point.Height:F2} m";

                ApplyTerrainLevelButton.IsEnabled =
                    true;

                ApplyTerrainPaintButton.IsEnabled =
                    _session.CurrentMap?
                        .Map
                        .GroundTextures
                        .Count >
                    1;

                StatusText.Text =
                    "Ponto de terreno selecionado. Ajuste nivelamento ou pintura, raio e feather.";

                if (
                    SelectionFilterComboBox
                        .SelectedIndex ==
                    3)
                {
                    Viewport
                        .BeginTerrainSelectionMode();
                }
            };

        Viewport.SelectionChanged +=
            info =>
            {
                _selectionInfo =
                    info;

                ApplyInspectorButton.IsEnabled =
                    info is not null;

                DeleteSelectionButton.IsEnabled =
                    info is not null;

                DuplicateSelectionButton.IsEnabled =
                    info is not null;

                SaveSplineLinksButton.IsEnabled =
                    info?.Kind ==
                    PickingKind.Spline;

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

                    InspectorPreviousSplineIdBox.Value =
                        double.NaN;

                    InspectorNextSplineIdBox.Value =
                        double.NaN;

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

                    InspectorPreviousSplineIdBox.Value =
                        info.PreviousSplineId ??
                        -1;

                    InspectorNextSplineIdBox.Value =
                        info.NextSplineId ??
                        -1;
                }

                SynchronizeExplorerSelection(
                    info);
            };
    }

    private void OnExplorerSearchTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_trafficMode)
        {
            RefreshTrafficFilter();
        }
        else if (_transportMode)
        {
            RefreshTransportFilter();
        }
        else if (_libraryMode)
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
        _assetPreviewCancellation
            ?.Cancel();

        _assetPreviewCancellation
            ?.Dispose();

        _assetPreviewCancellation =
            null;

        Viewport.CancelSceneryPlacement();
        Viewport.CancelSplinePlacement();
        Viewport.RestoreSceneView();

        PlaceAssetButton.Content =
            "Posicionar no mapa";

        SplineCurveCheckBox.Visibility =
            Visibility.Collapsed;

        SplineContinuousCheckBox.Visibility =
            Visibility.Collapsed;

        _libraryMode =
            false;

        _transportMode =
            false;

        _trafficMode =
            false;

        _trafficPreviewTimer.Stop();

        TrafficControlPanel.Visibility =
            Visibility.Collapsed;

        ExplorerListView.Visibility =
            Visibility.Visible;

        AssetLibraryPanel.Visibility =
            Visibility.Collapsed;

        TransportPanel.Visibility =
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

        _transportMode =
            false;

        _trafficMode =
            false;

        _trafficPreviewTimer.Stop();

        TrafficControlPanel.Visibility =
            Visibility.Collapsed;

        ExplorerListView.Visibility =
            Visibility.Collapsed;

        AssetLibraryPanel.Visibility =
            Visibility.Visible;

        TransportPanel.Visibility =
            Visibility.Collapsed;

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

    private async void OnAssetLibrarySelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var selected =
            AssetLibraryListView.SelectedItem as
                OmsiAssetIndexEntry;

        var placeable =
            selected?.Kind is
                OmsiAssetKind.SceneryObject or
                OmsiAssetKind.Spline;

        PlaceAssetButton.IsEnabled =
            _session.CurrentMap is not null &&
            placeable;

        PlaceAssetButton.Content =
            selected?.Kind ==
                OmsiAssetKind.Spline
                ? "Construir spline"
                : "Posicionar no mapa";

        SplineCurveCheckBox.Visibility =
            selected?.Kind ==
                OmsiAssetKind.Spline
                ? Visibility.Visible
                : Visibility.Collapsed;

        SplineContinuousCheckBox.Visibility =
            selected?.Kind ==
                OmsiAssetKind.Spline
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (
            !_libraryMode ||
            _session.OmsiRootPath is null ||
            AssetLibraryListView.SelectedItem is not
                OmsiAssetIndexEntry asset)
        {
            return;
        }

        _assetPreviewCancellation
            ?.Cancel();

        _assetPreviewCancellation
            ?.Dispose();

        _assetPreviewCancellation =
            new CancellationTokenSource();

        try
        {
            var result =
                await Viewport
                    .PreviewAssetAsync(
                        _session.OmsiRootPath,
                        asset,
                        _assetPreviewCancellation
                            .Token);

            if (
                result is null)
            {
                return;
            }

            StatusText.Text =
                result.IsRenderable
                    ? $"Prévia 3D nativa: {asset.RelativePath} · {result.TriangleCount} triângulos."
                    : asset.Kind is
                        OmsiAssetKind.Model or
                        OmsiAssetKind.Texture
                        ? $"Prévia 3D desta categoria ainda não está habilitada: {asset.RelativePath}."
                        : $"Não foi possível gerar a prévia: {asset.RelativePath} · {result.ErrorCode}.";
        }
        catch (
            OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha na prévia do asset: {exception.Message}";
        }
    }

    private async void OnPlaceAssetClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            Viewport.CancelSceneryPlacement();
            Viewport.CancelSplinePlacement();

            PlaceAssetButton.Content =
                AssetLibraryListView.SelectedItem is
                    OmsiAssetIndexEntry selectedAsset &&
                selectedAsset.Kind ==
                    OmsiAssetKind.Spline
                    ? "Construir spline"
                    : "Posicionar no mapa";

            StatusText.Text =
                "Posicionamento cancelado.";

            return;
        }

        if (
            _session.OmsiRootPath is null ||
            _session.CurrentMap is null ||
            AssetLibraryListView.SelectedItem is not
                OmsiAssetIndexEntry asset ||
            asset.Kind is not
                (
                    OmsiAssetKind.SceneryObject or
                    OmsiAssetKind.Spline
                ))
        {
            return;
        }

        _assetPreviewCancellation
            ?.Cancel();

        Viewport.RestoreSceneView();

        try
        {
            var started =
                asset.Kind ==
                    OmsiAssetKind.Spline
                    ? await Viewport
                        .BeginSplinePlacementAsync(
                            _session.OmsiRootPath,
                            asset,
                            SplineCurveCheckBox.IsChecked ==
                                true)
                    : await Viewport
                        .BeginSceneryPlacementAsync(
                            _session.OmsiRootPath,
                            asset);

            if (!started)
            {
                StatusText.Text =
                    $"Não foi possível preparar o asset: {asset.RelativePath}.";

                return;
            }

            PlaceAssetButton.Content =
                "Cancelar posicionamento";

            StatusText.Text =
                asset.Kind ==
                    OmsiAssetKind.Spline
                    ? SplineCurveCheckBox.IsChecked ==
                        true
                        ? "Spline curva: clique início, fim e ponto de curvatura."
                        : "Spline reta: clique início e fim."
                    : "Mova o ghost sobre o terreno e clique para inserir.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao iniciar posicionamento: {exception.Message}";
        }
    }

    private async Task HandleSplinePlacementAsync(
        NativeSplinePlacementRequest request)
    {
        if (_session.OmsiRootPath is null)
        {
            return;
        }

        try
        {
            PlaceAssetButton.Content =
                "Construir spline";

            PlaceAssetButton.IsEnabled =
                false;

            if (
                _session.PendingTransformCount >
                0)
            {
                StatusText.Text =
                    "Salvando transformações antes da spline...";

                await _session
                    .SavePendingTransformsAsync();

                SaveChangesButton.IsEnabled =
                    false;
            }

            StatusText.Text =
                request.IsCurved
                    ? "Inserindo spline curva com backup..."
                    : "Inserindo spline reta com backup...";

            var insertion =
                await _session
                    .InsertSplineAsync(
                        request);

            var snapshot =
                insertion.Snapshot;

            await Viewport
                .SetMapSnapshotAsync(
                    snapshot,
                    _session.OmsiRootPath);

            ClearInspectorSelectionState();
            RefreshExplorer();

            UndoButton.IsEnabled =
                false;

            RedoButton.IsEnabled =
                false;

            var selectedAsset =
                AssetLibraryListView.SelectedItem as
                    OmsiAssetIndexEntry;

            var canContinue =
                SplineContinuousCheckBox.IsChecked ==
                    true &&
                selectedAsset?.Kind ==
                    OmsiAssetKind.Spline;

            if (canContinue)
            {
                var restarted =
                    await Viewport
                        .BeginSplinePlacementAsync(
                            _session.OmsiRootPath,
                            selectedAsset!,
                            SplineCurveCheckBox.IsChecked ==
                                true);

                if (
                    restarted &&
                    Viewport.SeedSplinePlacementStart(
                        request.EndWorld,
                        insertion.SplineId))
                {
                    PlaceAssetButton.IsEnabled =
                        true;

                    PlaceAssetButton.Content =
                        "Cancelar posicionamento";

                    StatusText.Text =
                        $"Spline inserida ({request.Length:F1} m). Próximo segmento iniciado no endpoint anterior.";

                    return;
                }
            }

            PlaceAssetButton.IsEnabled =
                selectedAsset?.Kind is
                    OmsiAssetKind.SceneryObject or
                    OmsiAssetKind.Spline;

            StatusText.Text =
                $"Spline inserida: {request.Length:F1} m · raio {request.Radius:F1} · tile {request.Tile.X},{request.Tile.Y}.";
        }
        catch (Exception exception)
        {
            PlaceAssetButton.IsEnabled =
                AssetLibraryListView.SelectedItem is
                    OmsiAssetIndexEntry asset &&
                asset.Kind is
                    OmsiAssetKind.SceneryObject or
                    OmsiAssetKind.Spline;

            StatusText.Text =
                $"Falha ao inserir spline: {exception.Message}";
        }
    }

    private async Task HandleSceneryPlacementAsync(
        NativeSceneryPlacementRequest
            request)
    {
        if (_session.OmsiRootPath is null)
        {
            return;
        }

        try
        {
            PlaceAssetButton.Content =
                "Posicionar no mapa";

            PlaceAssetButton.IsEnabled =
                false;

            if (
                _session.PendingTransformCount >
                0)
            {
                StatusText.Text =
                    "Salvando transformações antes da inserção...";

                await _session
                    .SavePendingTransformsAsync();

                SaveChangesButton.IsEnabled =
                    false;
            }

            StatusText.Text =
                "Inserindo objeto no tile OMSI com backup...";

            var snapshot =
                await _session
                    .InsertSceneryObjectAsync(
                        request);

            await Viewport
                .SetMapSnapshotAsync(
                    snapshot,
                    _session.OmsiRootPath);

            RefreshExplorer();

            UndoButton.IsEnabled =
                false;

            RedoButton.IsEnabled =
                false;

            PlaceAssetButton.IsEnabled =
                AssetLibraryListView.SelectedItem is
                    OmsiAssetIndexEntry asset &&
                asset.Kind ==
                    OmsiAssetKind
                        .SceneryObject;

            StatusText.Text =
                $"Objeto inserido em tile {request.Tile.X},{request.Tile.Y} com backup seguro.";
        }
        catch (Exception exception)
        {
            PlaceAssetButton.IsEnabled =
                AssetLibraryListView.SelectedItem is
                    OmsiAssetIndexEntry asset &&
                asset.Kind ==
                    OmsiAssetKind
                        .SceneryObject;

            StatusText.Text =
                $"Falha ao inserir objeto: {exception.Message}";
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
            _assetPreviewCancellation
                ?.Cancel();

            Viewport.RestoreSceneView();

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
            asset.Kind is
                OmsiAssetKind.SceneryObject or
                OmsiAssetKind.Spline
                ? $"Prévia 3D ativa para {asset.RelativePath}. Use o mouse no viewport para inspecionar."
                : $"Asset indexado: {asset.RelativePath}.";
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

    private void ClearInspectorSelectionState()
    {
        _selectionInfo =
            null;

        ApplyInspectorButton.IsEnabled =
            false;

        DeleteSelectionButton.IsEnabled =
            false;

        DuplicateSelectionButton.IsEnabled =
            false;

        SaveSplineLinksButton.IsEnabled =
            false;

        SelectionText.Text =
            "Sem seleção";

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

        InspectorPitchBox.Value =
            double.NaN;

        InspectorBankBox.Value =
            double.NaN;

        InspectorLengthBox.Value =
            double.NaN;

        InspectorRadiusBox.Value =
            double.NaN;

        InspectorGradientStartBox.Value =
            double.NaN;

        InspectorGradientEndBox.Value =
            double.NaN;

        InspectorPreviousSplineIdBox.Value =
            double.NaN;

        InspectorNextSplineIdBox.Value =
            double.NaN;

        InspectorObjectFields.Visibility =
            Visibility.Collapsed;

        InspectorSplineFields.Visibility =
            Visibility.Collapsed;
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

    private void OnPickTerrainPointClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_session.CurrentMap is null)
        {
            StatusText.Text =
                "Abra um mapa OMSI antes de editar o terreno.";

            return;
        }

        _terrainEditPoint =
            null;

        ApplyTerrainLevelButton.IsEnabled =
            false;

        ApplyTerrainPaintButton.IsEnabled =
            false;

        TerrainPointText.Text =
            "Clique no terreno no viewport...";

        Viewport.BeginTerrainPointPick();

        StatusText.Text =
            "Ferramenta de terreno ativa: clique no ponto que deseja nivelar.";
    }

    private async void OnApplyTerrainLevelClick(
        object sender,
        RoutedEventArgs e)
    {
        var point =
            _terrainEditPoint;

        if (point is null)
        {
            return;
        }

        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de editar o terreno.";

            return;
        }

        var targetHeight =
            TerrainTargetHeightBox.Value;

        var radius =
            TerrainBrushRadiusBox.Value;

        var feather =
            TerrainBrushFeatherBox.Value;

        if (
            !double.IsFinite(targetHeight) ||
            !double.IsFinite(radius) ||
            !double.IsFinite(feather) ||
            radius <= 0 ||
            feather < 0 ||
            feather > 1)
        {
            StatusText.Text =
                "Valores de nivelamento do terreno são inválidos.";

            return;
        }

        try
        {
            ApplyTerrainLevelButton.IsEnabled =
                false;

            StatusText.Text =
                $"Nivelando terreno do tile {point.Tile.X},{point.Tile.Y} com backup...";

            var snapshot =
                await _session
                    .LevelTerrainAsync(
                        point,
                        targetHeight,
                        radius,
                        feather);

            if (_session.OmsiRootPath is null)
            {
                throw new InvalidOperationException(
                    "Instalação OMSI não selecionada.");
            }

            await Viewport
                .SetMapSnapshotAsync(
                    snapshot,
                    _session.OmsiRootPath);

            ClearInspectorSelectionState();
            RefreshExplorer();

            _terrainEditPoint =
                null;

            ApplyTerrainPaintButton.IsEnabled =
                false;

            TerrainPointText.Text =
                "Nivelamento aplicado. Escolha outro ponto para continuar.";

            StatusText.Text =
                $"Terreno nivelado para {targetHeight:F2} m · raio {radius:F1} m · feather {feather:F2}.";
        }
        catch (Exception exception)
        {
            ApplyTerrainLevelButton.IsEnabled =
                _terrainEditPoint is not null;

            StatusText.Text =
                $"Falha ao nivelar terreno: {exception.Message}";
        }
    }

    private async void OnApplyTerrainPaintClick(
        object sender,
        RoutedEventArgs e)
    {
        var point =
            _terrainEditPoint;

        var snapshot =
            _session.CurrentMap;

        if (
            point is null ||
            snapshot is null)
        {
            return;
        }

        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de pintar o terreno.";

            return;
        }

        var layerValue =
            TerrainPaintLayerBox.Value;

        var alphaValue =
            TerrainPaintAlphaBox.Value;

        var radius =
            TerrainBrushRadiusBox.Value;

        var feather =
            TerrainBrushFeatherBox.Value;

        if (
            !double.IsFinite(layerValue) ||
            Math.Truncate(layerValue) !=
                layerValue ||
            layerValue < 1 ||
            layerValue >=
                snapshot.Map
                    .GroundTextures
                    .Count)
        {
            StatusText.Text =
                snapshot.Map
                    .GroundTextures
                    .Count <= 1
                    ? "Este mapa não possui camadas groundtex pintáveis além da base."
                    : $"Camada inválida. Use um índice entre 1 e {snapshot.Map.GroundTextures.Count - 1}.";

            return;
        }

        if (
            !double.IsFinite(alphaValue) ||
            Math.Truncate(alphaValue) !=
                alphaValue ||
            alphaValue < 0 ||
            alphaValue > 255 ||
            !double.IsFinite(radius) ||
            !double.IsFinite(feather) ||
            radius <= 0 ||
            feather < 0 ||
            feather > 1)
        {
            StatusText.Text =
                "Valores de pintura inválidos: alpha 0–255, raio maior que zero e feather entre 0 e 1.";

            return;
        }

        var layer =
            checked(
                (int)layerValue);

        var alpha =
            checked(
                (byte)alphaValue);

        try
        {
            ApplyTerrainPaintButton.IsEnabled =
                false;

            ApplyTerrainLevelButton.IsEnabled =
                false;

            StatusText.Text =
                $"Pintando groundtex {layer} no tile {point.Tile.X},{point.Tile.Y}...";

            var updated =
                await _session
                    .PaintTerrainTextureAsync(
                        point,
                        layer,
                        alpha,
                        radius,
                        feather);

            if (_session.OmsiRootPath is null)
            {
                throw new InvalidOperationException(
                    "Instalação OMSI não selecionada.");
            }

            await Viewport
                .SetMapSnapshotAsync(
                    updated,
                    _session.OmsiRootPath);

            ClearInspectorSelectionState();
            RefreshExplorer();

            _terrainEditPoint =
                null;

            TerrainPointText.Text =
                $"Pintura aplicada na camada {layer}. Escolha outro ponto para continuar.";

            StatusText.Text =
                $"Groundtex {layer} pintado · alpha {alpha} · raio {radius:F1} m · feather {feather:F2}.";
        }
        catch (Exception exception)
        {
            ApplyTerrainLevelButton.IsEnabled =
                _terrainEditPoint is not null;

            ApplyTerrainPaintButton.IsEnabled =
                _terrainEditPoint is not null &&
                _session.CurrentMap?
                    .Map
                    .GroundTextures
                    .Count >
                1;

            StatusText.Text =
                $"Falha ao pintar textura do terreno: {exception.Message}";
        }
    }

    private async void OnSaveSplineLinksClick(
        object sender,
        RoutedEventArgs e)
    {
        var selection =
            _selectionInfo;

        if (
            selection is null ||
            selection.Kind !=
                PickingKind.Spline)
        {
            return;
        }

        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de alterar vínculos.";

            return;
        }

        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            StatusText.Text =
                "Cancele a ferramenta de posicionamento antes de alterar vínculos.";

            return;
        }

        var previousValue =
            InspectorPreviousSplineIdBox.Value;

        var nextValue =
            InspectorNextSplineIdBox.Value;

        if (
            !double.IsFinite(previousValue) ||
            !double.IsFinite(nextValue) ||
            Math.Truncate(previousValue) !=
                previousValue ||
            Math.Truncate(nextValue) !=
                nextValue ||
            previousValue <
                int.MinValue ||
            previousValue >
                int.MaxValue ||
            nextValue <
                int.MinValue ||
            nextValue >
                int.MaxValue)
        {
            StatusText.Text =
                "Anterior ID e Próxima ID precisam ser números inteiros.";

            return;
        }

        var desiredPrevious =
            (int)previousValue;

        var desiredNext =
            (int)nextValue;

        if (
            desiredPrevious ==
                selection.PreviousSplineId &&
            desiredNext ==
                selection.NextSplineId)
        {
            StatusText.Text =
                "Os vínculos da spline não foram alterados.";

            return;
        }

        try
        {
            SaveSplineLinksButton.IsEnabled =
                false;

            StatusText.Text =
                $"Atualizando vínculos da spline #{selection.EntityId} com backup...";

            var snapshot =
                await _session
                    .UpdateSplineLinksAsync(
                        selection,
                        desiredPrevious,
                        desiredNext);

            if (_session.OmsiRootPath is null)
            {
                throw new InvalidOperationException(
                    "Instalação OMSI não selecionada.");
            }

            await Viewport
                .SetMapSnapshotAsync(
                    snapshot,
                    _session.OmsiRootPath);

            RefreshExplorer();

            var refreshedItem =
                _explorerItems
                    .FirstOrDefault(
                        item =>
                            item.Kind ==
                                PickingKind.Spline &&
                            item.EntityId ==
                                selection.EntityId);

            if (refreshedItem is not null)
            {
                Viewport.SelectExplorerItem(
                    refreshedItem,
                    focus: false);
            }

            UndoButton.IsEnabled =
                false;

            RedoButton.IsEnabled =
                false;

            StatusText.Text =
                $"Vínculos da spline #{selection.EntityId} atualizados com segurança.";
        }
        catch (Exception exception)
        {
            SaveSplineLinksButton.IsEnabled =
                _selectionInfo?.Kind ==
                PickingKind.Spline;

            StatusText.Text =
                $"Falha ao atualizar vínculos: {exception.Message}";
        }
    }

    private async void OnDuplicateSelectionClick(
        object sender,
        RoutedEventArgs e)
    {
        await StartSelectionCopyPlacementAsync();
    }

    private async Task StartSelectionCopyPlacementAsync()
    {
        var selection =
            _selectionInfo;

        if (selection is null)
        {
            return;
        }

        if (
            selection.Kind ==
                PickingKind.Spline)
        {
            await StartSplineCopyPlacementAsync(
                selection);

            return;
        }

        if (
            selection.Kind !=
                PickingKind.Object)
        {
            return;
        }

        if (
            _session.OmsiRootPath is null ||
            _session.CurrentMap is null)
        {
            StatusText.Text =
                "Abra um mapa OMSI antes de criar uma cópia.";

            return;
        }

        if (
            _session.CurrentMap.Map
                .UsesWorldCoordinates)
        {
            StatusText.Text =
                "Cópia de objeto em mapa com worldcoordinates ainda não está habilitada no host nativo.";

            return;
        }

        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            StatusText.Text =
                "Cancele a ferramenta de posicionamento atual antes de criar a cópia.";

            return;
        }

        Viewport.RestoreSceneView();

        try
        {
            var started =
                await Viewport
                    .BeginSceneryPlacementCopyAsync(
                        _session.OmsiRootPath,
                        selection.AssetPath,
                        selection.Z,
                        selection.Rotation,
                        selection.Pitch ?? 0,
                        selection.Bank ?? 0);

            if (!started)
            {
                StatusText.Text =
                    $"Não foi possível preparar a cópia de {selection.AssetPath}.";

                return;
            }

            StatusText.Text =
                $"Cópia do objeto #{selection.EntityId}: clique no terreno para definir a nova posição.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao iniciar cópia: {exception.Message}";
        }
    }

    private async Task StartSplineCopyPlacementAsync(
        NativeSelectionInfo selection)
    {
        if (
            _session.OmsiRootPath is null ||
            _session.CurrentMap is null)
        {
            StatusText.Text =
                "Abra um mapa OMSI antes de criar uma cópia.";

            return;
        }

        if (
            selection.IsHeightSpline ==
                true)
        {
            StatusText.Text =
                "Cópia de spline de altura ainda não está habilitada no host nativo.";

            return;
        }

        if (
            _session.CurrentMap.Map
                .UsesWorldCoordinates)
        {
            StatusText.Text =
                "Cópia de spline em mapa com worldcoordinates ainda não está habilitada no host nativo.";

            return;
        }

        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            StatusText.Text =
                "Cancele a ferramenta de posicionamento atual antes de criar a cópia.";

            return;
        }

        Viewport.RestoreSceneView();

        try
        {
            var curved =
                Math.Abs(
                    selection.Radius ??
                    0) >
                0.001;

            var started =
                await Viewport
                    .BeginSplinePlacementCopyAsync(
                        _session.OmsiRootPath,
                        selection.AssetPath,
                        curved);

            if (!started)
            {
                StatusText.Text =
                    $"Não foi possível preparar a cópia de {selection.AssetPath}.";

                return;
            }

            StatusText.Text =
                $"Cópia desconectada da spline #{selection.EntityId}: defina a nova geometria no viewport.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao iniciar cópia da spline: {exception.Message}";
        }
    }

    private async void OnDeleteSelectionClick(
        object sender,
        RoutedEventArgs e)
    {
        await DeleteCurrentSelectionAsync();
    }

    private async Task DeleteCurrentSelectionAsync()
    {
        var selection =
            _selectionInfo;

        if (selection is null)
        {
            return;
        }

        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de excluir.";

            return;
        }

        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            StatusText.Text =
                "Cancele a ferramenta de posicionamento antes de excluir.";

            return;
        }

        var isObject =
            selection.Kind ==
            PickingKind.Object;

        var label =
            isObject
                ? $"objeto #{selection.EntityId}"
                : $"spline #{selection.EntityId}";

        var detail =
            isObject
                ? "O objeto será removido do tile OMSI."
                : "A spline será removida e os vínculos recíprocos dos vizinhos serão liberados.";

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    $"Excluir {label}?",
                Content =
                    $"{detail}\n\nUm backup seguro será criado antes de gravar.",
                PrimaryButtonText =
                    "Excluir",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Close
            };

        var result =
            await dialog.ShowAsync();

        if (
            result !=
            ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            DeleteSelectionButton.IsEnabled =
                false;

            ApplyInspectorButton.IsEnabled =
                false;

            StatusText.Text =
                $"Excluindo {label} com backup seguro...";

            var snapshot =
                await _session
                    .DeleteSelectionAsync(
                        selection);

            if (_session.OmsiRootPath is null)
            {
                throw new InvalidOperationException(
                    "Instalação OMSI não selecionada.");
            }

            await Viewport
                .SetMapSnapshotAsync(
                    snapshot,
                    _session.OmsiRootPath);

            ClearInspectorSelectionState();
            RefreshExplorer();

            UndoButton.IsEnabled =
                false;

            RedoButton.IsEnabled =
                false;

            SaveChangesButton.IsEnabled =
                false;

            StatusText.Text =
                $"{(isObject ? "Objeto" : "Spline")} #{selection.EntityId} excluído(a) com backup.";
        }
        catch (Exception exception)
        {
            DeleteSelectionButton.IsEnabled =
                _selectionInfo is not null;

            ApplyInspectorButton.IsEnabled =
                _selectionInfo is not null;

            StatusText.Text =
                $"Falha ao excluir {label}: {exception.Message}";
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
        RoutedEventArgs e) =>
        UndoTransform();

    private void OnRedoClick(
        object sender,
        RoutedEventArgs e) =>
        RedoTransform();

    private void UndoTransform()
    {
        if (!Viewport.Undo())
        {
            return;
        }

        StatusText.Text =
            "Transformação desfeita.";

        UndoButton.IsEnabled =
            Viewport.CanUndo;

        RedoButton.IsEnabled =
            Viewport.CanRedo;
    }

    private void RedoTransform()
    {
        if (!Viewport.Redo())
        {
            return;
        }

        StatusText.Text =
            "Transformação refeita.";

        UndoButton.IsEnabled =
            Viewport.CanUndo;

        RedoButton.IsEnabled =
            Viewport.CanRedo;
    }

    private void OnToolSelectionClick(
        object sender,
        RoutedEventArgs e)
    {
        OnSceneExplorerModeClick(
            sender,
            e);

        SetSelectionModeFromShortcut(
            0);

        StatusText.Text =
            "Ferramenta Seleção ativa: clique no cenário para selecionar e editar.";
    }

    private async void OnToolObjectsClick(
        object sender,
        RoutedEventArgs e)
    {
        SetSelectionModeFromShortcut(
            1);

        await ActivateLibraryToolAsync(
            1,
            null,
            "Objetos: biblioteca SCO pronta para posicionar e editar.");
    }

    private async void OnToolSplinesClick(
        object sender,
        RoutedEventArgs e)
    {
        SetSelectionModeFromShortcut(
            2);

        await ActivateLibraryToolAsync(
            2,
            null,
            "Ruas/Splines: escolha uma SLI e use o construtor reto ou curvo.");
    }

    private async void OnToolCrossingsClick(
        object sender,
        RoutedEventArgs e)
    {
        SetSelectionModeFromShortcut(
            1);

        await ActivateLibraryToolAsync(
            1,
            "cross",
            "Cruzamentos: mostrando objetos de cenário; refine a busca pelo nome do pacote quando necessário.");
    }

    private void OnToolTerrainClick(
        object sender,
        RoutedEventArgs e)
    {
        SetSelectionModeFromShortcut(
            3);

        StatusText.Text =
            "Terreno: clique no chão para selecionar tile; nivelamento e pintura ficam no Inspector.";
    }

    private async void OnToolWaterClick(
        object sender,
        RoutedEventArgs e)
    {
        await ActivateLibraryToolAsync(
            1,
            "water",
            "Água: atalho de assets ativo. O editor nativo dedicado de planos de água entra na próxima etapa.");
    }

    private async void OnToolTrafficClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_session.CurrentMap is null)
        {
            StatusText.Text =
                "Tráfego: abra um mapa OMSI primeiro.";

            return;
        }

        _assetPreviewCancellation
            ?.Cancel();

        Viewport.CancelSceneryPlacement();
        Viewport.CancelSplinePlacement();
        Viewport.RestoreSceneView();

        SetSelectionModeFromShortcut(
            0);

        Viewport
            .SetTrafficPathsVisible(
                true);

        _libraryMode =
            false;

        _transportMode =
            false;

        _trafficMode =
            true;

        ExplorerListView.Visibility =
            Visibility.Collapsed;

        AssetLibraryPanel.Visibility =
            Visibility.Collapsed;

        TransportPanel.Visibility =
            Visibility.Collapsed;

        TrafficControlPanel.Visibility =
            Visibility.Visible;

        ExplorerSearchBox.PlaceholderText =
            "Buscar programas de semáforo...";

        _trafficPrograms =
            Viewport
                .GetTrafficLightPrograms();

        _trafficRuleItems =
            BuildTrafficRuleItems(
                _session.CurrentMap);

        _trafficVehicleGroups =
            await new OmsiUnscheduledVehicleGroupReader()
                .ReadMapAsync(
                    _session.CurrentMap
                        .Map
                        .DirectoryPath);

        TrafficVehicleGroupComboBox.ItemsSource =
            _trafficVehicleGroups;

        TrafficVehicleGroupComboBox.SelectedIndex =
            _trafficVehicleGroups.Count >
                0
                ? 0
                : -1;

        TrafficRulePresetComboBox.ItemsSource =
            OmsiTrafficRulePresets.All;

        TrafficRulePresetComboBox.SelectedIndex =
            0;

        RefreshTrafficFilter();

        TrafficStatusText.Text =
            $"{Viewport.TrafficPathLineCount} linhas de path · " +
            $"{_trafficPrograms.Count} programa(s) de semáforo · " +
            $"{_trafficRuleItems.Count} regra(s) aplicada(s) · " +
            $"{_trafficVehicleGroups.Count} grupo(s) de veículo.";

        TrafficProgramListView.SelectedIndex =
            _trafficPrograms.Count > 0
                ? 0
                : -1;

        StatusText.Text =
            "Tráfego: paths reais e preview de semáforos ativos.";
    }

    private void RefreshTrafficFilter()
    {
        var query =
            ExplorerSearchBox.Text
                .Trim();

        if (
            TrafficViewComboBox
                .SelectedIndex ==
            1)
        {
            IEnumerable<
                TrafficRuleExplorerItem>
                rules =
                    _trafficRuleItems;

            if (
                !string.IsNullOrWhiteSpace(
                    query))
            {
                rules =
                    rules.Where(
                        item =>
                            item.DisplayText
                                .Contains(
                                    query,
                                    StringComparison
                                        .OrdinalIgnoreCase) ||
                            item.Detail
                                .Contains(
                                    query,
                                    StringComparison
                                        .OrdinalIgnoreCase) ||
                            item.AssetPath
                                .Contains(
                                    query,
                                    StringComparison
                                        .OrdinalIgnoreCase));
            }

            TrafficRuleListView.ItemsSource =
                rules.ToArray();

            return;
        }

        IEnumerable<
            NativeTrafficLightProgramInfo>
            items =
                _trafficPrograms;

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
                                    .OrdinalIgnoreCase));
        }

        TrafficProgramListView.ItemsSource =
            items.ToArray();
    }

    private IReadOnlyList<
        TrafficRuleExplorerItem>
        BuildTrafficRuleItems(
            NativeMapSnapshot snapshot)
    {
        var result =
            new List<
                TrafficRuleExplorerItem>();

        foreach (
            var tile in
                snapshot.Tiles)
        {
            foreach (
                var item in
                    tile.Content.Splines)
            {
                foreach (
                    var rule in
                        item.TrafficRules)
                {
                    result.Add(
                        CreateTrafficRuleItem(
                            PickingKind.Spline,
                            item.SplineId,
                            tile.Reference.X,
                            tile.Reference.Y,
                            item.SplinePath,
                            rule));
                }
            }

            foreach (
                var item in
                    tile.Content.Objects)
            {
                foreach (
                    var rule in
                        item.TrafficRules)
                {
                    result.Add(
                        CreateTrafficRuleItem(
                            PickingKind.Object,
                            item.ObjectId,
                            tile.Reference.X,
                            tile.Reference.Y,
                            item.SceneryObjectPath,
                            rule));
                }
            }
        }

        return result;
    }

    private static TrafficRuleExplorerItem
        CreateTrafficRuleItem(
            PickingKind ownerKind,
            int entityId,
            int tileX,
            int tileY,
            string assetPath,
            OmsiTrafficRule rule)
    {
        var preset =
            OmsiTrafficRulePresets
                .TryMatch(
                    rule.RuleName,
                    rule.NumericValue);

        var displayName =
            preset?.DisplayName ??
            rule.RuleName;

        var pathText =
            rule.PathIndex
                ?.ToString() ??
            "?";

        var groupText =
            rule.VehicleGroupIndex
                ?.ToString() ??
            "?";

        return new TrafficRuleExplorerItem(
            ownerKind,
            entityId,
            tileX,
            tileY,
            assetPath,
            rule,
            $"{displayName} · path {pathText} · grupo {groupText}",
            $"{(ownerKind == PickingKind.Spline ? "Spline" : "Objeto")} #{entityId} · tile {tileX},{tileY}\n" +
            $"{assetPath}\n" +
            $"Path: {pathText}\n" +
            $"Keyword: {rule.RuleName}\n" +
            $"Valor: {rule.RawValue}\n" +
            $"Grupo: {groupText}\n" +
            $"Tipo: {(rule.IsKillRule ? "[kill_rule]" : "[rule]")}"));
    }

    private void OnTrafficViewSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var rules =
            TrafficViewComboBox
                .SelectedIndex ==
            1;

        TrafficSignalsPanel.Visibility =
            rules
                ? Visibility.Collapsed
                : Visibility.Visible;

        TrafficRulesPanel.Visibility =
            rules
                ? Visibility.Visible
                : Visibility.Collapsed;

        _trafficPreviewTimer.Stop();

        TrafficPlayButton.Content =
            "▶ Play";

        ExplorerSearchBox.PlaceholderText =
            rules
                ? "Buscar Traffic Rules..."
                : "Buscar programas de semáforo...";

        RefreshTrafficFilter();

        TrafficDetailText.Text =
            rules
                ? "Selecione uma Traffic Rule."
                : "Selecione um programa de semáforo.";
    }

    private void OnTrafficRuleSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (
            TrafficRuleListView.SelectedItem is
                TrafficRuleExplorerItem
                    item)
        {
            TrafficDetailText.Text =
                item.Detail;

            FocusTrafficRuleOwnerButton.IsEnabled =
                true;

            return;
        }

        FocusTrafficRuleOwnerButton.IsEnabled =
            false;
    }

    private void OnFocusTrafficRuleOwnerClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            TrafficRuleListView.SelectedItem is not
                TrafficRuleExplorerItem
                    rule)
        {
            return;
        }

        var explorer =
            _explorerItems
                .FirstOrDefault(
                    item =>
                        item.Kind ==
                            rule.OwnerKind &&
                        item.EntityId ==
                            rule.EntityId &&
                        item.TileX ==
                            rule.TileX &&
                        item.TileY ==
                            rule.TileY);

        if (explorer is null)
        {
            StatusText.Text =
                "Dono da Traffic Rule não está no conjunto de tiles carregado.";
            return;
        }

        if (
            Viewport.SelectExplorerItem(
                explorer,
                focus: true))
        {
            StatusText.Text =
                $"Traffic Rule focada no mapa · path {rule.Rule.PathIndex?.ToString() ?? "?"}.";
        }
    }

    private void OnTrafficProgramSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (
            TrafficProgramListView
                .SelectedItem is not
                NativeTrafficLightProgramInfo
                    program)
        {
            TrafficPhaseListView.ItemsSource =
                null;

            TrafficPhaseText.Text =
                "Fase: —";

            TrafficDetailText.Text =
                "Selecione um programa.";

            return;
        }

        var duration =
            Math.Max(
                1.0,
                program
                    .EffectiveCycleDuration);

        TrafficPreviewTimeSlider.Maximum =
            duration;

        if (
            TrafficPreviewTimeSlider.Value >
                duration)
        {
            TrafficPreviewTimeSlider.Value =
                0;
        }

        TrafficPhaseListView.ItemsSource =
            program.Phases
                .Select(
                    (phase, index) =>
                        $"F{index + 1} · " +
                        $"{NativeTrafficLightProgramInfo.DescribeSignalCode(phase.SignalCode)} " +
                        $"[{phase.SignalCode}] · {phase.Duration:F2}s")
                .ToArray();

        TrafficDetailText.Text =
            $"Objeto #{program.ObjectId} · tile {program.TileX},{program.TileY}\n" +
            $"{program.AssetPath}\n" +
            $"Programa: {program.ProgramName} · ciclo {duration:F2}s";

        UpdateTrafficPhasePreview();
    }

    private void OnTrafficPreviewTimeChanged(
        object sender,
        RangeBaseValueChangedEventArgs e)
    {
        if (_trafficMode)
        {
            UpdateTrafficPhasePreview();
        }
    }

    private void UpdateTrafficPhasePreview()
    {
        if (
            TrafficProgramListView
                .SelectedItem is not
                NativeTrafficLightProgramInfo
                    program)
        {
            return;
        }

        var phase =
            program.GetPhaseAt(
                TrafficPreviewTimeSlider
                    .Value);

        TrafficPhaseText.Text =
            phase is null
                ? "Fase: —"
                : $"t={TrafficPreviewTimeSlider.Value:F2}s · " +
                  $"{NativeTrafficLightProgramInfo.DescribeSignalCode(phase.SignalCode)} [{phase.SignalCode}]";
    }

    private void OnTrafficPlayClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_trafficPreviewTimer.IsEnabled)
        {
            _trafficPreviewTimer.Stop();

            TrafficPlayButton.Content =
                "▶ Play";

            return;
        }

        _trafficPreviewTimer.Start();

        TrafficPlayButton.Content =
            "⏸ Pausar";
    }

    private void OnTrafficPreviewTimerTick(
        object? sender,
        object e)
    {
        if (
            !_trafficMode ||
            TrafficProgramListView
                .SelectedItem is not
                NativeTrafficLightProgramInfo
                    program)
        {
            _trafficPreviewTimer.Stop();
            return;
        }

        var duration =
            Math.Max(
                1.0,
                program
                    .EffectiveCycleDuration);

        var next =
            TrafficPreviewTimeSlider.Value +
            0.25;

        TrafficPreviewTimeSlider.Value =
            next >= duration
                ? 0
                : next;
    }

    private async void OnToolTransportClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_session.CurrentMap is
            not { } snapshot)
        {
            StatusText.Text =
                "Transporte: abra um mapa OMSI primeiro.";

            return;
        }

        _assetPreviewCancellation
            ?.Cancel();

        Viewport.CancelSceneryPlacement();
        Viewport.CancelSplinePlacement();
        Viewport.RestoreSceneView();

        _libraryMode =
            false;

        _transportMode =
            true;

        _trafficMode =
            false;

        _trafficPreviewTimer.Stop();

        TrafficControlPanel.Visibility =
            Visibility.Collapsed;

        ExplorerListView.Visibility =
            Visibility.Collapsed;

        AssetLibraryPanel.Visibility =
            Visibility.Collapsed;

        TransportPanel.Visibility =
            Visibility.Visible;

        ExplorerSearchBox.PlaceholderText =
            "Buscar Tracks, Trips, Stops e StationLinks...";

        TransportStatusText.Text =
            "Lendo TTData...";

        try
        {
            _timetableCatalog =
                await new OmsiTimetableCatalogReader()
                    .ReadAsync(
                        snapshot.Map
                            .DirectoryPath);

            RefreshTransportItems();

            TransportStatusText.Text =
                $"{_timetableCatalog.Tracks.Count} Tracks · " +
                $"{_timetableCatalog.Trips.Count} Trips · " +
                $"{_timetableCatalog.BusStops.Count} Stops · " +
                $"{_timetableCatalog.StationLinks.Count} StationLinks · " +
                $"{_timetableCatalog.Lines.Count} Lines · " +
                $"{_timetableCatalog.BrokenTripTrackReferenceCount} Trip→Track quebrado(s) · " +
                $"{_timetableCatalog.BrokenStationLinkStopReferenceCount} StnLink→Stop quebrado(s) · " +
                $"{_timetableCatalog.BrokenLineTripReferenceCount} Line→Trip quebrado(s).";

            StatusText.Text =
                "Transporte: TTData real carregado no editor nativo.";
        }
        catch (Exception exception)
        {
            TransportStatusText.Text =
                $"Falha ao ler TTData: {exception.Message}";
        }
    }

    private void RefreshTransportItems()
    {
        if (_timetableCatalog is null)
        {
            _transportItems =
                Array.Empty<
                    TransportExplorerItem>();

            TransportListView.ItemsSource =
                _transportItems;

            return;
        }

        _transportItems =
            TransportKindComboBox
                .SelectedIndex switch
            {
                1 =>
                    _timetableCatalog.Trips
                        .Select(
                            trip =>
                                new TransportExplorerItem(
                                    "Trip",
                                    trip.Name,
                                    $"{trip.Name} · linha {trip.Line} → {trip.Destination}",
                                    $"Track: {trip.TrackName}\n" +
                                    $"Estações: {trip.Stations.Count}\n" +
                                    $"Train reverse: {(trip.TrainReverse ? "sim" : "não")}\n" +
                                    $"Arquivo: {trip.RelativePath}"))
                        .ToArray(),
                2 =>
                    _timetableCatalog.BusStops
                        .Select(
                            stop =>
                                new TransportExplorerItem(
                                    "Stop",
                                    stop.Id.ToString(),
                                    $"{stop.Id} · {stop.Name}",
                                    $"Tile index: {stop.TileIndex}\n" +
                                    $"Subnome: {stop.SubName}"))
                        .ToArray(),
                3 =>
                    _timetableCatalog.StationLinks
                        .Select(
                            link =>
                                new TransportExplorerItem(
                                    "StationLink",
                                    $"{link.StartBusStopId}>{link.EndBusStopId}",
                                    $"{link.StartBusStopId} → {link.EndBusStopId} · {link.Comment}",
                                    $"Entradas: {link.Entries.Count}\n" +
                                    $"Comprimento/ref: {link.Line1}"))
                        .ToArray(),
                4 =>
                    _timetableCatalog.Lines
                        .Select(
                            line =>
                                new TransportExplorerItem(
                                    "Line",
                                    line.Name,
                                    $"{line.Name} · {line.Tours.Count} tour(s)",
                                    $"Arquivo: {line.RelativePath}\n" +
                                    $"Prioridade: {line.Priority}\n" +
                                    $"Jogador permitido: {(line.UserAllowed ? "sim" : "não")}\n" +
                                    $"Trips agendados: {line.Tours.Sum(tour => tour.Trips.Count)}"))
                        .ToArray(),
                _ =>
                    _timetableCatalog.Tracks
                        .Select(
                            track =>
                                new TransportExplorerItem(
                                    "Track",
                                    track.Name,
                                    $"{track.Name} · {track.Entries.Count} segmentos",
                                    $"Arquivo: {track.RelativePath}\n" +
                                    $"Comentário: {track.Comment1} {track.Comment2}"))
                        .ToArray()
            };

        RefreshTransportFilter();
    }

    private void RefreshTransportFilter()
    {
        var query =
            ExplorerSearchBox.Text
                .Trim();

        IEnumerable<
            TransportExplorerItem> items =
                _transportItems;

        if (!string.IsNullOrWhiteSpace(
                query))
        {
            items =
                items.Where(
                    item =>
                        item.DisplayText.Contains(
                            query,
                            StringComparison.OrdinalIgnoreCase) ||
                        item.Detail.Contains(
                            query,
                            StringComparison.OrdinalIgnoreCase) ||
                        item.Key.Contains(
                            query,
                            StringComparison.OrdinalIgnoreCase));
        }

        TransportListView.ItemsSource =
            items.ToArray();
    }

    private void OnTransportKindSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_transportMode)
        {
            RefreshTransportItems();
        }
    }

    private void OnTransportSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        TransportDetailText.Text =
            TransportListView.SelectedItem is
                TransportExplorerItem item
                ? item.Detail
                : "Selecione um item para ver detalhes.";
    }

    private void OnToolValidationClick(
        object sender,
        RoutedEventArgs e)
    {
        OnSceneExplorerModeClick(
            sender,
            e);

        StatusText.Text =
            _session.CurrentMap is null
                ? "Validação: abra um mapa para iniciar a análise."
                : "Validação: atalho preparado; a varredura dedicada de referências, paths e dependências será ligada nesta área.";
    }

    private async Task ActivateLibraryToolAsync(
        int kindIndex,
        string? searchText,
        string status)
    {
        _libraryMode =
            true;

        _transportMode =
            false;

        _trafficMode =
            false;

        _trafficPreviewTimer.Stop();

        TrafficControlPanel.Visibility =
            Visibility.Collapsed;

        TransportPanel.Visibility =
            Visibility.Collapsed;

        ExplorerListView.Visibility =
            Visibility.Collapsed;

        AssetLibraryPanel.Visibility =
            Visibility.Visible;

        ExplorerSearchBox.PlaceholderText =
            "Buscar na biblioteca...";

        if (
            LibraryKindComboBox
                .SelectedIndex !=
            kindIndex)
        {
            LibraryKindComboBox
                .SelectedIndex =
                kindIndex;
        }

        ExplorerSearchBox.Text =
            searchText ??
            string.Empty;

        await LoadAssetLibraryAsync();

        StatusText.Text =
            status;
    }

    private void OnSelectionFilterChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        ApplySelectionMode(
            SelectionFilterComboBox
                .SelectedIndex);

    private void ApplySelectionMode(
        int selectedIndex)
    {
        var filter =
            selectedIndex switch
            {
                1 =>
                    NativeSelectionFilter.Objects,
                2 =>
                    NativeSelectionFilter.Splines,
                3 =>
                    NativeSelectionFilter.Terrain,
                _ =>
                    NativeSelectionFilter.All
            };

        if (
            filter ==
                NativeSelectionFilter.Terrain)
        {
            if (
                !ShowTerrainMenuItem.IsChecked)
            {
                ShowTerrainMenuItem.IsChecked =
                    true;

                ApplySceneVisibility();
            }

            Viewport.SetSelectionFilter(
                filter);

            Viewport.BeginTerrainSelectionMode();

            StatusText.Text =
                "Seleção filtrada para terreno/tile. Clique no terreno para selecionar.";

            return;
        }

        Viewport.CancelTerrainPointPick();

        Viewport.SetSelectionFilter(
            filter);

        StatusText.Text =
            filter switch
            {
                NativeSelectionFilter.Objects =>
                    "Seleção filtrada para objetos.",
                NativeSelectionFilter.Splines =>
                    "Seleção filtrada para splines.",
                _ =>
                    "Seleção liberada para objetos e splines."
            };
    }

    private void SetSelectionModeFromShortcut(
        int selectedIndex)
    {
        if (
            SelectionFilterComboBox
                .SelectedIndex ==
            selectedIndex)
        {
            ApplySelectionMode(
                selectedIndex);

            return;
        }

        SelectionFilterComboBox
            .SelectedIndex =
            selectedIndex;
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

    private void OnNightPreviewChecked(
        object sender,
        RoutedEventArgs e) =>
        ApplyNightPreview(
            true);

    private void OnNightPreviewUnchecked(
        object sender,
        RoutedEventArgs e) =>
        ApplyNightPreview(
            false);

    private void ApplyNightPreview(
        bool enabled)
    {
        var loaded =
            Viewport.SetNightPreview(
                enabled);

        StatusText.Text =
            loaded
                ? enabled
                    ? "Preview noturno ativo com céu OMSI."
                    : "Preview diurno ativo com céu OMSI."
                : _session.OmsiRootPath is null
                    ? "Selecione a instalação OMSI para carregar o céu."
                    : "Textura de céu OMSI não encontrada; mantendo o fundo padrão.";
    }

    private async void OnSaveChangesClick(
        object sender,
        RoutedEventArgs e) =>
        await SavePendingChangesAsync();

    private async Task SavePendingChangesAsync()
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
                _session.PendingTransformCount;

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

    private void OnFullscreenClick(
        object sender,
        RoutedEventArgs e) =>
        ToggleFullscreen();

    private void OnPerspectiveViewClick(
        object sender,
        RoutedEventArgs e)
    {
        Viewport.SetPerspectiveView();

        StatusText.Text =
            "Câmera em perspectiva.";
    }

    private void OnTopViewClick(
        object sender,
        RoutedEventArgs e)
    {
        Viewport.SetTopView();

        StatusText.Text =
            "Câmera em vista superior.";
    }

    private void OnFitSceneClick(
        object sender,
        RoutedEventArgs e) =>
        FitCurrentScene();

    private void FitCurrentScene()
    {
        if (!Viewport.FitScene())
        {
            StatusText.Text =
                "Abra um mapa antes de enquadrar a cena.";

            return;
        }

        StatusText.Text =
            "Mapa enquadrado na câmera.";
    }

    private void OnFocusSelectionClick(
        object sender,
        RoutedEventArgs e) =>
        FocusCurrentSelection();

    private void FocusCurrentSelection()
    {
        if (!Viewport.FocusSelection())
        {
            StatusText.Text =
                "Selecione um objeto ou spline antes de focar.";

            return;
        }

        StatusText.Text =
            "Câmera focada na seleção.";
    }

    private void ApplySceneVisibility()
    {
        var visibility =
            new NativeSceneVisibility(
                TerrainVisible:
                    ShowTerrainMenuItem.IsChecked,
                ObjectsVisible:
                    ShowObjectsMenuItem.IsChecked,
                SplinesVisible:
                    ShowSplinesMenuItem.IsChecked);

        if (
            Viewport.SetSceneVisibility(
                visibility))
        {
            StatusText.Text =
                $"Visibilidade: terreno {(visibility.TerrainVisible ? "on" : "off")} · " +
                $"objetos {(visibility.ObjectsVisible ? "on" : "off")} · " +
                $"splines {(visibility.SplinesVisible ? "on" : "off")}.";
        }
    }

    private void OnSceneVisibilityClick(
        object sender,
        RoutedEventArgs e) =>
        ApplySceneVisibility();

    private void OnGridVisibilityClick(
        object sender,
        RoutedEventArgs e)
    {
        var visible =
            ShowGridMenuItem.IsChecked;

        if (
            Viewport.SetGridVisible(
                visible))
        {
            StatusText.Text =
                visible
                    ? "Grade do mapa visível."
                    : "Grade do mapa oculta.";
        }
    }

    private void OnSplineProfilesVisibilityClick(
        object sender,
        RoutedEventArgs e)
    {
        var visible =
            ShowSplineProfilesMenuItem
                .IsChecked;

        if (
            Viewport
                .SetSplineProfilesVisible(
                    visible))
        {
            StatusText.Text =
                visible
                    ? "Perfis reais das splines visíveis."
                    : "Perfis reais das splines ocultos; guias continuam visíveis.";
        }
    }

    private void OnToggleExplorerClick(
        object sender,
        RoutedEventArgs e) =>
        ToggleExplorerPanel();

    private void OnToggleInspectorClick(
        object sender,
        RoutedEventArgs e) =>
        ToggleInspectorPanel();

    private void ToggleExplorerPanel()
    {
        var visible =
            ExplorerColumn.Width.Value >
            0;

        if (visible)
        {
            _explorerPanelWidth =
                Math.Max(
                    220,
                    ExplorerColumn
                        .Width.Value);

            ExplorerColumn.Width =
                new GridLength(0);

            ExplorerSplitterColumn.Width =
                new GridLength(0);

            ExplorerPanel.Visibility =
                Visibility.Collapsed;

            StatusText.Text =
                "Explorer recolhido.";

            return;
        }

        ExplorerPanel.Visibility =
            Visibility.Visible;

        ExplorerColumn.Width =
            new GridLength(
                Math.Clamp(
                    _explorerPanelWidth,
                    220,
                    520));

        ExplorerSplitterColumn.Width =
            new GridLength(6);

        StatusText.Text =
            "Explorer restaurado.";
    }

    private void ToggleInspectorPanel()
    {
        var visible =
            InspectorColumn.Width.Value >
            0;

        if (visible)
        {
            _inspectorPanelWidth =
                Math.Max(
                    240,
                    InspectorColumn
                        .Width.Value);

            InspectorColumn.Width =
                new GridLength(0);

            InspectorSplitterColumn.Width =
                new GridLength(0);

            InspectorPanel.Visibility =
                Visibility.Collapsed;

            StatusText.Text =
                "Inspector recolhido.";

            return;
        }

        InspectorPanel.Visibility =
            Visibility.Visible;

        InspectorColumn.Width =
            new GridLength(
                Math.Clamp(
                    _inspectorPanelWidth,
                    240,
                    560));

        InspectorSplitterColumn.Width =
            new GridLength(6);

        StatusText.Text =
            "Inspector restaurado.";
    }

    private void OnPanelSplitterPointerPressed(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        _resizingExplorerPanel =
            ReferenceEquals(
                sender,
                ExplorerSplitter);

        _resizingInspectorPanel =
            ReferenceEquals(
                sender,
                InspectorSplitter);

        if (
            !_resizingExplorerPanel &&
            !_resizingInspectorPanel)
        {
            return;
        }

        element.CapturePointer(
            e.Pointer);

        e.Handled = true;
    }

    private void OnPanelSplitterPointerMoved(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (
            !_resizingExplorerPanel &&
            !_resizingInspectorPanel)
        {
            return;
        }

        var point =
            e.GetCurrentPoint(
                WorkspaceGrid);

        if (_resizingExplorerPanel)
        {
            var maximum =
                Math.Max(
                    220,
                    Math.Min(
                        520,
                        WorkspaceGrid.ActualWidth -
                            420));

            _explorerPanelWidth =
                Math.Clamp(
                    point.Position.X,
                    220,
                    maximum);

            ExplorerColumn.Width =
                new GridLength(
                    _explorerPanelWidth);
        }
        else if (_resizingInspectorPanel)
        {
            var maximum =
                Math.Max(
                    240,
                    Math.Min(
                        560,
                        WorkspaceGrid.ActualWidth -
                            420));

            _inspectorPanelWidth =
                Math.Clamp(
                    WorkspaceGrid.ActualWidth -
                        point.Position.X,
                    240,
                    maximum);

            InspectorColumn.Width =
                new GridLength(
                    _inspectorPanelWidth);
        }

        e.Handled = true;
    }

    private void OnPanelSplitterPointerReleased(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(
                e.Pointer);
        }

        _resizingExplorerPanel =
            false;

        _resizingInspectorPanel =
            false;

        e.Handled = true;
    }


    private void OnFullscreenAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        ToggleFullscreen();
        args.Handled = true;
    }

    private void OnMoveAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        Viewport.SetGizmoMode(
            NativeGizmoMode.Move);

        StatusText.Text =
            "Ferramenta mover ativa (W).";

        args.Handled = true;
    }

    private void OnRotateAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        Viewport.SetGizmoMode(
            NativeGizmoMode.Rotate);

        StatusText.Text =
            "Ferramenta rotacionar ativa (E).";

        args.Handled = true;
    }

    private async void OnSaveAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        await SavePendingChangesAsync();
    }

    private void OnUndoAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        UndoTransform();
        args.Handled = true;
    }

    private void OnRedoAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        RedoTransform();
        args.Handled = true;
    }

    private async void OnDuplicateAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        if (_selectionInfo is null)
        {
            return;
        }

        args.Handled = true;

        await StartSelectionCopyPlacementAsync();
    }

    private async void OnDeleteAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        args.Handled = true;

        await DeleteCurrentSelectionAsync();
    }

    private void OnFocusSelectionAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        FocusCurrentSelection();
        args.Handled = true;
    }

    private void OnSnapAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        OnSnapClick(
            this,
            new RoutedEventArgs());

        args.Handled = true;
    }

    private void OnTerrainVisibilityAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        ShowTerrainMenuItem.IsChecked =
            !ShowTerrainMenuItem.IsChecked;

        ApplySceneVisibility();
        args.Handled = true;
    }

    private void OnGridVisibilityAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        ShowGridMenuItem.IsChecked =
            !ShowGridMenuItem.IsChecked;

        Viewport.SetGridVisible(
            ShowGridMenuItem.IsChecked);

        StatusText.Text =
            ShowGridMenuItem.IsChecked
                ? "Grade do mapa visível (G)."
                : "Grade do mapa oculta (G).";

        args.Handled = true;
    }

    private void OnObjectsVisibilityAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        ShowObjectsMenuItem.IsChecked =
            !ShowObjectsMenuItem.IsChecked;

        ApplySceneVisibility();
        args.Handled = true;
    }

    private void OnSplinesVisibilityAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        ShowSplinesMenuItem.IsChecked =
            !ShowSplinesMenuItem.IsChecked;

        ApplySceneVisibility();
        args.Handled = true;
    }

    private void OnSplineProfilesVisibilityAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        ShowSplineProfilesMenuItem.IsChecked =
            !ShowSplineProfilesMenuItem
                .IsChecked;

        Viewport
            .SetSplineProfilesVisible(
                ShowSplineProfilesMenuItem
                    .IsChecked);

        StatusText.Text =
            ShowSplineProfilesMenuItem
                .IsChecked
                ? "Perfis reais das splines visíveis (P)."
                : "Perfis reais das splines ocultos (P).";

        args.Handled = true;
    }

    private void OnSelectAllAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        SetSelectionModeFromShortcut(
            0);

        StatusText.Text =
            "Ferramenta selecionar ativa (Q).";

        args.Handled = true;
    }

    private void OnFitSceneAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        FitCurrentScene();
        args.Handled = true;
    }

    private void OnSelectionAllAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        SetSelectionModeFromShortcut(
            0);

        args.Handled = true;
    }

    private void OnSelectionObjectsAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        SetSelectionModeFromShortcut(
            1);

        args.Handled = true;
    }

    private void OnSelectionSplinesAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        SetSelectionModeFromShortcut(
            2);

        args.Handled = true;
    }

    private void OnSelectionTerrainAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        SetSelectionModeFromShortcut(
            3);

        args.Handled = true;
    }

    private void OnPerspectiveAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        Viewport.SetPerspectiveView();

        StatusText.Text =
            "Câmera em perspectiva (1).";

        args.Handled = true;
    }

    private void OnTopAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        Viewport.SetTopView();

        StatusText.Text =
            "Câmera em vista superior (2).";

        args.Handled = true;
    }

    private void OnEscapeAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            Viewport.CancelSceneryPlacement();
            Viewport.CancelSplinePlacement();

            PlaceAssetButton.Content =
                AssetLibraryListView.SelectedItem is
                    OmsiAssetIndexEntry asset &&
                asset.Kind ==
                    OmsiAssetKind.Spline
                    ? "Construir spline"
                    : "Posicionar no mapa";

            StatusText.Text =
                "Ferramenta de posicionamento cancelada.";

            args.Handled = true;
            return;
        }

        if (IsFullscreen())
        {
            ExitFullscreen();
            args.Handled = true;
            return;
        }

        if (!IsTextInputFocused())
        {
            SetSelectionModeFromShortcut(
                0);

            StatusText.Text =
                "Modo Selecionar restaurado (Esc).";

            args.Handled = true;
        }
    }

    private bool IsTextInputFocused()
    {
        if (MainRoot.XamlRoot is null)
        {
            return false;
        }

        var focused =
            FocusManager.GetFocusedElement(
                MainRoot.XamlRoot);

        return
            focused is TextBox or
            RichEditBox or
            PasswordBox or
            NumberBox;
    }

    private bool IsFullscreen() =>
        _appWindow.Presenter?.Kind ==
        AppWindowPresenterKind.FullScreen;

    private void ToggleFullscreen()
    {
        if (IsFullscreen())
        {
            ExitFullscreen();
            return;
        }

        _appWindow.SetPresenter(
            AppWindowPresenterKind.FullScreen);

        StatusText.Text =
            "Tela cheia ativa · F11 ou Esc para sair.";
    }

    private void ExitFullscreen()
    {
        _appWindow.SetPresenter(
            AppWindowPresenterKind.Default);

        StatusText.Text =
            "Tela cheia desativada.";
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

    private async void OnFullMapModeClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _fullMapMode ||
            _mapLoadModeChanging ||
            _session.CurrentMap is null)
        {
            return;
        }

        await ChangeMapLoadModeAsync(
            fullMap: true);
    }

    private async void OnPerformanceMapModeClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !_fullMapMode ||
            _mapLoadModeChanging ||
            _session.CurrentMap is null)
        {
            return;
        }

        await ChangeMapLoadModeAsync(
            fullMap: false);
    }

    private async Task ChangeMapLoadModeAsync(
        bool fullMap)
    {
        var current =
            _session.CurrentMap;

        if (current is null)
        {
            return;
        }

        try
        {
            _mapLoadModeChanging =
                true;

            StatusText.Text =
                fullMap
                    ? "Carregando mapa completo..."
                    : "Carregando região 3×3...";

            NativeMapSnapshot snapshot;

            if (fullMap)
            {
                snapshot =
                    await _session
                        .LoadFullMapAsync();
            }
            else
            {
                var center =
                    current.ActiveTile ??
                    OmsiTileRegionSelector
                        .FindInitialTile(
                            current.Map.Tiles) ??
                    throw new InvalidDataException(
                        "mapTileNotFound");

                snapshot =
                    await _session
                        .LoadRegionAsync(
                            center.X,
                            center.Y,
                            radius: 1);
            }

            _fullMapMode =
                fullMap;

            await ApplyMapSnapshotAsync(
                snapshot,
                focusActiveTile: true);

            StatusText.Text =
                fullMap
                    ? $"Mapa completo carregado: {snapshot.Tiles.Count} tiles."
                    : $"Modo desempenho 3×3 ativo em {snapshot.ActiveTile?.X},{snapshot.ActiveTile?.Y}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao trocar modo de carregamento: {exception.Message}";
        }
        finally
        {
            _mapLoadModeChanging =
                false;
        }
    }

    private async void OnTileNavigatorGoClick(
        object sender,
        RoutedEventArgs e)
    {
        var x =
            TileNavigatorXBox.Value;

        var y =
            TileNavigatorYBox.Value;

        if (
            !double.IsFinite(x) ||
            !double.IsFinite(y) ||
            Math.Truncate(x) != x ||
            Math.Truncate(y) != y ||
            x < int.MinValue ||
            x > int.MaxValue ||
            y < int.MinValue ||
            y > int.MaxValue)
        {
            StatusText.Text =
                "Informe coordenadas inteiras de tile.";

            return;
        }

        await NavigateToTileAsync(
            (int)x,
            (int)y);
    }

    private async void OnTileNavigateClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            sender is not Button button ||
            button.Tag is not string tag)
        {
            return;
        }

        var parts =
            tag.Split(',');

        if (
            parts.Length != 2 ||
            !int.TryParse(
                parts[0],
                out var dx) ||
            !int.TryParse(
                parts[1],
                out var dy))
        {
            return;
        }

        var active =
            _session.CurrentMap
                ?.ActiveTile;

        if (active is null)
        {
            return;
        }

        await NavigateToTileAsync(
            active.X + dx,
            active.Y + dy);
    }

    private async Task NavigateToTileAsync(
        int tileX,
        int tileY)
    {
        var current =
            _session.CurrentMap;

        if (
            current is null ||
            _mapLoadModeChanging)
        {
            return;
        }

        if (
            !current.Map.Tiles.Any(
                tile =>
                    tile.X == tileX &&
                    tile.Y == tileY))
        {
            StatusText.Text =
                $"O mapa não possui o tile {tileX},{tileY}.";

            return;
        }

        try
        {
            _mapLoadModeChanging =
                true;

            if (_fullMapMode)
            {
                var snapshot =
                    _session.SetActiveTile(
                        tileX,
                        tileY);

                UpdateMapSummary(
                    snapshot);

                TileNavigatorXBox.Value =
                    tileX;

                TileNavigatorYBox.Value =
                    tileY;

                if (
                    !Viewport.FocusTile(
                        tileX,
                        tileY))
                {
                    StatusText.Text =
                        $"Tile {tileX},{tileY} não está disponível no viewport.";
                    return;
                }

                StatusText.Text =
                    $"Tile {tileX},{tileY} focado no mapa completo.";
                return;
            }

            StatusText.Text =
                $"Carregando região 3×3 em {tileX},{tileY}...";

            var region =
                await _session
                    .LoadRegionAsync(
                        tileX,
                        tileY,
                        radius: 1);

            await ApplyMapSnapshotAsync(
                region,
                focusActiveTile: true);

            StatusText.Text =
                $"Região 3×3 carregada em {tileX},{tileY}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao navegar para o tile: {exception.Message}";
        }
        finally
        {
            _mapLoadModeChanging =
                false;
        }
    }

    private async Task ApplyMapSnapshotAsync(
        NativeMapSnapshot snapshot,
        bool focusActiveTile)
    {
        if (_session.OmsiRootPath is null)
        {
            throw new InvalidOperationException(
                "Instalação OMSI não selecionada.");
        }

        await Viewport
            .SetMapSnapshotAsync(
                snapshot,
                _session.OmsiRootPath);

        ClearInspectorSelectionState();

        _terrainEditPoint =
            null;

        ApplyTerrainLevelButton.IsEnabled =
            false;

        ApplyTerrainPaintButton.IsEnabled =
            false;

        TerrainPointText.Text =
            "Nenhum ponto selecionado.";

        RefreshExplorer();
        UpdateMapSummary(
            snapshot);

        SaveChangesButton.IsEnabled =
            false;

        UndoButton.IsEnabled =
            false;

        RedoButton.IsEnabled =
            false;

        if (
            focusActiveTile &&
            snapshot.ActiveTile is
                { } active)
        {
            Viewport.FocusTile(
                active.X,
                active.Y);
        }
    }

    private void UpdateMapSummary(
        NativeMapSnapshot snapshot)
    {
        var activeTile =
            snapshot.ActiveTile is null
                ? "—"
                : $"{snapshot.ActiveTile.X}, {snapshot.ActiveTile.Y}";

        MapText.Text =
            $"{snapshot.Map.DisplayName}\n" +
            $"Tiles carregados: {snapshot.Tiles.Count} / {snapshot.Map.Tiles.Count}\n" +
            $"Objetos: {snapshot.ObjectCount} · Splines: {snapshot.SplineCount}\n" +
            $"Terrenos: {snapshot.TerrainCount} · Tile ativo: {activeTile}";

        MapLoadModeText.Text =
            _fullMapMode
                ? "Carregamento: mapa completo"
                : "Carregamento: desempenho 3×3";

        if (
            snapshot.ActiveTile is
                { } active)
        {
            TileNavigatorXBox.Value =
                active.X;

            TileNavigatorYBox.Value =
                active.Y;
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

            _fullMapMode =
                true;

            StatusText.Text =
                "Carregando mapa completo...";

            var snapshot =
                await _session
                    .OpenMapAsync(
                        mapDirectory,
                        loadFullMap: true);

            await ApplyMapSnapshotAsync(
                snapshot,
                focusActiveTile: false);

            StatusText.Text =
                $"Mapa {snapshot.Map.DisplayName} carregado pelo MapStudio.Core · " +
                $"{snapshot.Tiles.Count} tiles.";
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
