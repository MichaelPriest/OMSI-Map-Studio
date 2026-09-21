using System.Numerics;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
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

    private sealed record ValidationExplorerItem(
        string Severity,
        string Code,
        string DisplayText,
        string Detail,
        OmsiAssetKind? AssetKind = null,
        string? AssetPath = null);

    private sealed record LibraryGroupOption(
        OmsiAssetLibraryGroup Group,
        string Label);

    private readonly OmsiNativeSession _session =
        new();

    private NativeAssetLibraryState
        _assetLibraryState =
            NativeAssetLibraryStateStore
                .Load();

    private string?
        _referenceOverlayMapDirectory;

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

    private IReadOnlyList<
        LibraryGroupOption>
        _libraryGroupOptions =
            [
                new(
                    OmsiAssetLibraryGroup.All,
                    "Todos")
            ];

    private bool _libraryMode;
    private bool _transportMode;
    private bool _trafficMode;
    private bool _validationMode;
    private bool _junctionMode;

    private IReadOnlyList<
        NativeJunctionSuggestion>
        _junctionSuggestions =
            Array.Empty<
                NativeJunctionSuggestion>();

    private NativeJunctionSuggestion?
        _junctionPlacementTarget;

    private OmsiAssetKind?
        _dependencyRepairKind;

    private string?
        _dependencyRepairOldPath;

    private IReadOnlyList<
        ValidationExplorerItem>
        _validationItems =
            Array.Empty<
                ValidationExplorerItem>();

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

    private NativeSceneryPlacementRequest?
        _patternLineStart;

    private bool _applyingConstructionPreset;

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
        if (_junctionMode)
        {
            RefreshJunctionSuggestionFilter();
        }
        else if (_validationMode)
        {
            RefreshValidationFilter();
        }
        else if (_trafficMode)
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
        Viewport.ClearTimetableRoutePreview();
        Viewport.RestoreSceneView();

        PlaceAssetButton.Content =
            "Posicionar no mapa";

        SplinePlacementOptionsPanel.Visibility =
            Visibility.Collapsed;

        _libraryMode =
            false;

        _transportMode =
            false;

        _trafficMode =
            false;

        _validationMode =
            false;

        _junctionMode =
            false;

        _junctionPlacementTarget =
            null;

        _dependencyRepairKind =
            null;

        _dependencyRepairOldPath =
            null;

        RepairDependencyButton.Visibility =
            Visibility.Collapsed;

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

        _validationMode =
            false;

        _junctionMode =
            false;

        _junctionPlacementTarget =
            null;

        _dependencyRepairKind =
            null;

        _dependencyRepairOldPath =
            null;

        RepairDependencyButton.Visibility =
            Visibility.Collapsed;

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

    private void OnLibraryGroupSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_libraryMode)
        {
            RefreshLibrarySubcategoryOptions();
            RefreshLibraryFilter();
        }
    }

    private void OnLibrarySubcategorySelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_libraryMode)
        {
            RefreshLibraryFilter();
        }
    }

    private void OnLibraryTechnicalFilterSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_libraryMode)
        {
            RefreshLibraryFilter();
        }
    }

    private void OnLibraryViewSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_libraryMode)
        {
            var groupView =
                LibraryViewComboBox
                    .SelectedIndex ==
                0;

            var collectionView =
                LibraryViewComboBox
                    .SelectedIndex ==
                4;

            LibraryGroupComboBox.IsEnabled =
                groupView;

            LibrarySubcategoryComboBox.IsEnabled =
                groupView;

            LibraryCollectionPanel.Visibility =
                collectionView
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (collectionView)
            {
                RefreshLibraryCollectionOptions();
            }

            RefreshLibraryFilter();
        }
    }

    private void OnLibraryCollectionSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_libraryMode)
        {
            return;
        }

        UpdateCollectionButtonForSelection();

        if (
            LibraryViewComboBox
                .SelectedIndex ==
            4)
        {
            RefreshLibraryFilter();
        }
    }

    private async void OnCreateLibraryCollectionClick(
        object sender,
        RoutedEventArgs e)
    {
        var nameBox =
            new TextBox
            {
                Header =
                    "Nome da coleção",
                PlaceholderText =
                    "Ex.: Ruas favoritas"
            };

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Nova coleção",
                Content =
                    nameBox,
                PrimaryButtonText =
                    "Criar",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        var name =
            nameBox.Text
                .Trim();

        if (
            string.IsNullOrWhiteSpace(
                name) ||
            name.Length >
                80)
        {
            StatusText.Text =
                "Nome de coleção inválido.";

            return;
        }

        if (
            !_assetLibraryState
                .Collections
                .ContainsKey(
                    name))
        {
            _assetLibraryState
                .Collections[
                    name] =
                [];

            SaveAssetLibraryState();
        }

        RefreshLibraryCollectionOptions(
            name);

        StatusText.Text =
            $"Coleção “{name}” ativa.";
    }

    private async void OnDeleteLibraryCollectionClick(
        object sender,
        RoutedEventArgs e)
    {
        var name =
            GetActiveLibraryCollectionName();

        if (
            string.Equals(
                name,
                "Minha coleção",
                StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text =
                "A coleção padrão não pode ser excluída.";

            return;
        }

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Excluir coleção?",
                Content =
                    $"A coleção “{name}” será removida. Os arquivos OMSI não serão alterados.",
                PrimaryButtonText =
                    "Excluir",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Close
            };

        if (
            await dialog.ShowAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        _assetLibraryState
            .Collections
            .Remove(
                name);

        SaveAssetLibraryState();

        RefreshLibraryCollectionOptions(
            "Minha coleção");

        RefreshLibraryFilter();

        StatusText.Text =
            $"Coleção “{name}” excluída.";
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

        var hasSelection =
            selected is not null;

        RepairDependencyButton.IsEnabled =
            selected is not null &&
            _dependencyRepairKind is
                { } repairKind &&
            selected.Kind ==
                repairKind;

        FavoriteAssetButton.IsEnabled =
            hasSelection;

        CollectionAssetButton.IsEnabled =
            hasSelection;

        if (selected is not null)
        {
            FavoriteAssetButton.Content =
                _assetLibraryState
                    .Favorites
                    .Contains(
                        selected.RelativePath,
                        StringComparer
                            .OrdinalIgnoreCase)
                    ? "★ Favorito"
                    : "☆ Favoritar";

            UpdateCollectionButtonForSelection();

            RecordRecentAsset(
                selected.RelativePath);
        }

        PlaceAssetButton.IsEnabled =
            _session.CurrentMap is not null &&
            placeable;

        PlaceAssetButton.Content =
            selected?.Kind ==
                OmsiAssetKind.Spline
                ? "Construir spline"
                : "Posicionar no mapa";

        ObjectPlacementOptionsPanel.Visibility =
            selected?.Kind ==
                OmsiAssetKind.SceneryObject
                ? Visibility.Visible
                : Visibility.Collapsed;

        _patternLineStart =
            null;

        SplinePlacementOptionsPanel.Visibility =
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

    private void OnObjectPlacementModeChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        _patternLineStart =
            null;

        if (
            !_applyingConstructionPreset &&
            ConstructionPresetComboBox
                .SelectedIndex !=
            0)
        {
            ConstructionPresetComboBox
                .SelectedIndex =
                0;
        }
    }

    private void OnConstructionPresetChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (
            ConstructionPresetComboBox
                .SelectedIndex <=
            0)
        {
            return;
        }

        _applyingConstructionPreset =
            true;

        try
        {
            _patternLineStart =
                null;

            switch (
                ConstructionPresetComboBox
                    .SelectedIndex)
            {
                case 1:
                    ObjectPlacementModeComboBox.SelectedIndex =
                        2;
                    PlacementSpacingBox.Value =
                        8;
                    PlacementRandomRotationCheckBox.IsChecked =
                        true;
                    break;

                case 2:
                    ObjectPlacementModeComboBox.SelectedIndex =
                        2;
                    PlacementSpacingBox.Value =
                        25;
                    PlacementRandomRotationCheckBox.IsChecked =
                        false;
                    break;

                case 3:
                    ObjectPlacementModeComboBox.SelectedIndex =
                        6;
                    PlacementSpacingBox.Value =
                        18;
                    PlacementSetbackBox.Value =
                        7;
                    PlacementRandomRotationCheckBox.IsChecked =
                        false;
                    break;

                case 4:
                    ObjectPlacementModeComboBox.SelectedIndex =
                        3;
                    PlacementRadiusBox.Value =
                        25;
                    PlacementCountBox.Value =
                        30;
                    PlacementRandomRotationCheckBox.IsChecked =
                        true;
                    break;

                case 5:
                    ObjectPlacementModeComboBox.SelectedIndex =
                        4;
                    PlacementRowsBox.Value =
                        5;
                    PlacementColumnsBox.Value =
                        8;
                    PlacementSpacingXBox.Value =
                        3;
                    PlacementSpacingZBox.Value =
                        6;
                    PlacementRandomRotationCheckBox.IsChecked =
                        false;
                    break;
            }
        }
        finally
        {
            _applyingConstructionPreset =
                false;
        }
    }

    private static double PlacementValue(
        NumberBox box,
        double fallback) =>
        double.IsFinite(
            box.Value)
            ? box.Value
            : fallback;

    private static int PlacementIntValue(
        NumberBox box,
        int fallback,
        int minimum,
        int maximum) =>
        Math.Clamp(
            double.IsFinite(
                box.Value)
                ? (int)Math.Round(
                    box.Value)
                : fallback,
            minimum,
            maximum);

    private async void OnRepairDependencyClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _dependencyRepairKind is not
                { } repairKind ||
            string.IsNullOrWhiteSpace(
                _dependencyRepairOldPath) ||
            AssetLibraryListView.SelectedItem is not
                OmsiAssetIndexEntry replacement ||
            replacement.Kind !=
                repairKind)
        {
            StatusText.Text =
                "Selecione um asset do mesmo tipo da dependência ausente.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes do reparo.";

            return;
        }

        var oldPath =
            _dependencyRepairOldPath;

        var kind =
            repairKind ==
                OmsiAssetKind.SceneryObject
                ? PickingKind.Object
                : PickingKind.Spline;

        var confirm =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Reparar dependência ausente?",
                Content =
                    $"Ausente: {oldPath}\n\nSubstituir por: {replacement.RelativePath}\n\nA alteração será aplicada em todos os tiles do mapa e terá backup transacional.",
                PrimaryButtonText =
                    "Substituir",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await confirm.ShowAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            RepairDependencyButton.IsEnabled =
                false;

            StatusText.Text =
                $"Reparando dependência {oldPath}...";

            var result =
                await _session
                    .ReplaceMapAssetPathAsync(
                        kind,
                        oldPath,
                        replacement.RelativePath);

            _dependencyRepairKind =
                null;

            _dependencyRepairOldPath =
                null;

            RepairDependencyButton.Visibility =
                Visibility.Collapsed;

            await ApplyMapSnapshotAsync(
                result.Snapshot,
                focusActiveTile: false);

            StatusText.Text =
                result.Replacements > 0
                    ? $"Dependência reparada: {result.Replacements} referência(s) em {result.FilesSaved} arquivo(s). Backup: {result.BackupDirectory}"
                    : "Nenhuma referência foi substituída.";
        }
        catch (Exception exception)
        {
            RepairDependencyButton.IsEnabled =
                true;

            StatusText.Text =
                $"Falha ao reparar dependência: {exception.Message}";
        }
    }

    private void OnFavoriteAssetClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            AssetLibraryListView
                .SelectedItem is not
                OmsiAssetIndexEntry asset)
        {
            return;
        }

        if (
            _assetLibraryState
                .Favorites
                .Contains(
                    asset.RelativePath,
                    StringComparer
                        .OrdinalIgnoreCase))
        {
            _assetLibraryState
                .Favorites
                .RemoveAll(
                    value =>
                        string.Equals(
                            value,
                            asset.RelativePath,
                            StringComparison
                                .OrdinalIgnoreCase));
        }
        else
        {
            _assetLibraryState
                .Favorites
                .Add(
                    asset.RelativePath);
        }

        SaveAssetLibraryState();

        FavoriteAssetButton.Content =
            _assetLibraryState
                .Favorites
                .Contains(
                    asset.RelativePath,
                    StringComparer
                        .OrdinalIgnoreCase)
                ? "★ Favorito"
                : "☆ Favoritar";

        RefreshLibraryFilter();
    }

    private string GetActiveLibraryCollectionName() =>
        LibraryCollectionComboBox
            .SelectedItem
            ?.ToString() is
            { Length: > 0 } name
            ? name
            : "Minha coleção";

    private void RefreshLibraryCollectionOptions(
        string? preferred = null)
    {
        if (
            !_assetLibraryState
                .Collections
                .ContainsKey(
                    "Minha coleção"))
        {
            _assetLibraryState
                .Collections[
                    "Minha coleção"] =
                [];
        }

        var previous =
            preferred ??
            LibraryCollectionComboBox
                .SelectedItem
                ?.ToString() ??
            "Minha coleção";

        var names =
            _assetLibraryState
                .Collections
                .Keys
                .OrderBy(
                    name =>
                        string.Equals(
                            name,
                            "Minha coleção",
                            StringComparison.OrdinalIgnoreCase)
                            ? 0
                            : 1)
                .ThenBy(
                    name => name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        LibraryCollectionComboBox.ItemsSource =
            names;

        var index =
            Array.FindIndex(
                names,
                name =>
                    string.Equals(
                        name,
                        previous,
                        StringComparison.OrdinalIgnoreCase));

        LibraryCollectionComboBox.SelectedIndex =
            index >= 0
                ? index
                : 0;
    }

    private void UpdateCollectionButtonForSelection()
    {
        if (
            AssetLibraryListView
                .SelectedItem is not
                OmsiAssetIndexEntry asset)
        {
            CollectionAssetButton.Content =
                "+ Coleção";

            return;
        }

        var name =
            GetActiveLibraryCollectionName();

        var inCollection =
            _assetLibraryState
                .Collections
                .TryGetValue(
                    name,
                    out var collection) &&
            collection.Contains(
                asset.RelativePath,
                StringComparer.OrdinalIgnoreCase);

        CollectionAssetButton.Content =
            inCollection
                ? "− Coleção"
                : "+ Coleção";
    }

    private void OnCollectionAssetClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            AssetLibraryListView
                .SelectedItem is not
                OmsiAssetIndexEntry asset)
        {
            return;
        }

        var collectionName =
            GetActiveLibraryCollectionName();

        if (
            !_assetLibraryState
                .Collections
                .TryGetValue(
                    collectionName,
                    out var collection))
        {
            collection = [];
            _assetLibraryState
                .Collections[
                    collectionName] =
                collection;
        }

        if (collection.Contains(
                asset.RelativePath,
                StringComparer.OrdinalIgnoreCase))
        {
            collection.RemoveAll(
                value =>
                    string.Equals(
                        value,
                        asset.RelativePath,
                        StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            collection.Add(
                asset.RelativePath);
        }

        SaveAssetLibraryState();

        CollectionAssetButton.Content =
            collection.Contains(
                asset.RelativePath,
                StringComparer.OrdinalIgnoreCase)
                ? "− Coleção"
                : "+ Coleção";

        RefreshLibraryFilter();
    }

    private void RecordRecentAsset(
        string relativePath)
    {
        _assetLibraryState
            .Recent
            .RemoveAll(
                value =>
                    string.Equals(
                        value,
                        relativePath,
                        StringComparison.OrdinalIgnoreCase));

        _assetLibraryState
            .Recent
            .Insert(
                0,
                relativePath);

        if (
            _assetLibraryState
                .Recent.Count >
            64)
        {
            _assetLibraryState
                .Recent
                .RemoveRange(
                    64,
                    _assetLibraryState
                        .Recent.Count -
                    64);
        }

        SaveAssetLibraryState();
    }

    private void RecordAssetUsage(
        string relativePath)
    {
        _assetLibraryState
            .Usage
            .TryGetValue(
                relativePath,
                out var count);

        _assetLibraryState
            .Usage[
                relativePath] =
            count + 1;

        RecordRecentAsset(
            relativePath);
    }

    private void SaveAssetLibraryState()
    {
        try
        {
            NativeAssetLibraryStateStore
                .Save(
                    _assetLibraryState);
        }
        catch
        {
            // Personalização nunca deve bloquear edição do mapa.
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

            _patternLineStart =
                null;

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
            if (
                asset.Kind ==
                    OmsiAssetKind.Spline)
            {
                Viewport
                    .SetSplineEndpointSnapOptions(
                        SplineEndpointSnapCheckBox
                            .IsChecked ==
                        true,
                        double.IsFinite(
                            SplineEndpointSnapDistanceBox
                                .Value)
                            ? SplineEndpointSnapDistanceBox
                                .Value
                            : 5.0,
                        SplineAutoConnectCheckBox
                            .IsChecked ==
                        true);
            }

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

            if (
                asset.Kind ==
                    OmsiAssetKind.SceneryObject &&
                _junctionPlacementTarget is
                    { } junctionTarget &&
                Viewport
                    .TryFinishSceneryPlacementAtWorldPoint(
                        junctionTarget.WorldPoint,
                        junctionTarget.Rotation,
                        out var junctionRequest) &&
                junctionRequest is not
                    null)
            {
                _junctionPlacementTarget =
                    null;

                PlaceAssetButton.Content =
                    "Posicionar no mapa";

                await HandleSceneryPlacementAsync(
                    junctionRequest);

                StatusText.Text +=
                    $" · alvo geométrico #{junctionTarget.SplineA}/#{junctionTarget.SplineB}.";

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
                    : ObjectPlacementModeComboBox.SelectedIndex switch
                    {
                        2 or 6 =>
                            "Mova o ghost e clique o ponto inicial.",
                        3 =>
                            "Clique o centro do pincel/área.",
                        4 =>
                            "Clique o centro da matriz.",
                        5 =>
                            "Clique o centro do círculo.",
                        _ =>
                            "Mova o ghost sobre o terreno e clique para inserir."
                    };
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
                request.NextSplineId <
                    0 &&
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

            var linkStatus =
                request.PreviousSplineId >= 0 ||
                request.NextSplineId >= 0
                    ? $" · links {request.PreviousSplineId} → #{insertion.SplineId} → {request.NextSplineId}"
                    : string.Empty;

            StatusText.Text =
                $"Spline inserida: {request.Length:F1} m · raio {request.Radius:F1} · tile {request.Tile.X},{request.Tile.Y}{linkStatus}.";
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

            var mode =
                ObjectPlacementModeComboBox
                    .SelectedIndex;

            IReadOnlyList<
                NativeSceneryPatternPlacement>
                pattern;

            if (mode is 2 or 6)
            {
                if (_patternLineStart is null)
                {
                    _patternLineStart =
                        request;

                    if (
                        AssetLibraryListView
                            .SelectedItem is
                            OmsiAssetIndexEntry
                                selected &&
                        selected.Kind ==
                            OmsiAssetKind
                                .SceneryObject)
                    {
                        await Viewport
                            .BeginSceneryPlacementAsync(
                                _session
                                    .OmsiRootPath,
                                selected);
                    }

                    PlaceAssetButton.Content =
                        "Cancelar posicionamento";

                    StatusText.Text =
                        mode == 2
                            ? "Linha: ponto inicial definido. Clique o ponto final."
                            : "Lotes: início definido. Clique o fim da frente dos lotes.";

                    return;
                }

                var start =
                    _patternLineStart;

                _patternLineStart =
                    null;

                pattern =
                    mode == 2
                        ? NativeSceneryPlacementPatternBuilder
                            .BuildLine(
                                start,
                                request,
                                PlacementValue(
                                    PlacementSpacingBox,
                                    12),
                                PlacementRandomRotationCheckBox
                                    .IsChecked ==
                                true)
                        : NativeSceneryPlacementPatternBuilder
                            .BuildLot(
                                start,
                                request,
                                PlacementValue(
                                    PlacementSpacingBox,
                                    18),
                                PlacementValue(
                                    PlacementSetbackBox,
                                    7),
                                PlacementRandomRotationCheckBox
                                    .IsChecked ==
                                true);
            }
            else
            {
                pattern =
                    mode switch
                    {
                        3 =>
                            NativeSceneryPlacementPatternBuilder
                                .BuildArea(
                                    request,
                                    PlacementValue(
                                        PlacementRadiusBox,
                                        20),
                                    PlacementIntValue(
                                        PlacementCountBox,
                                        18,
                                        1,
                                        256),
                                    PlacementRandomRotationCheckBox
                                        .IsChecked ==
                                    true),

                        4 =>
                            NativeSceneryPlacementPatternBuilder
                                .BuildMatrix(
                                    request,
                                    PlacementIntValue(
                                        PlacementRowsBox,
                                        4,
                                        1,
                                        16),
                                    PlacementIntValue(
                                        PlacementColumnsBox,
                                        4,
                                        1,
                                        16),
                                    PlacementValue(
                                        PlacementSpacingXBox,
                                        6),
                                    PlacementValue(
                                        PlacementSpacingZBox,
                                        6),
                                    PlacementRandomRotationCheckBox
                                        .IsChecked ==
                                    true),

                        5 =>
                            NativeSceneryPlacementPatternBuilder
                                .BuildCircle(
                                    request,
                                    PlacementValue(
                                        PlacementRadiusBox,
                                        15),
                                    PlacementIntValue(
                                        PlacementCountBox,
                                        12,
                                        1,
                                        256),
                                    PlacementTangentCheckBox
                                        .IsChecked ==
                                    true,
                                    PlacementRandomRotationCheckBox
                                        .IsChecked ==
                                    true),

                        _ =>
                            [
                                new NativeSceneryPatternPlacement(
                                    request.WorldPoint.X,
                                    request.WorldPoint.Z,
                                    request.Z,
                                    request.Rotation,
                                    request.Pitch,
                                    request.Bank)
                            ]
                    };
            }

            var requests =
                BuildPatternRequests(
                    request,
                    pattern);

            if (requests.Count == 0)
            {
                throw new InvalidDataException(
                    "patternOutsideMap");
            }

            PlaceAssetButton.Content =
                "Posicionar no mapa";

            PlaceAssetButton.IsEnabled =
                false;

            StatusText.Text =
                requests.Count == 1
                    ? "Inserindo objeto com backup..."
                    : $"Inserindo {requests.Count} objetos em lote com backup...";

            var snapshot =
                requests.Count == 1
                    ? await _session
                        .InsertSceneryObjectAsync(
                            requests[0])
                    : await _session
                        .InsertSceneryObjectBatchAsync(
                            requests);

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
                requests.Count == 1
                    ? $"Objeto inserido em tile {request.Tile.X},{request.Tile.Y} com backup seguro."
                    : $"{requests.Count} objetos inseridos em lote com backup seguro.";

            RecordAssetUsage(
                request.SceneryObjectPath);

            if (
                mode == 1 &&
                AssetLibraryListView
                    .SelectedItem is
                    OmsiAssetIndexEntry
                        repeatAsset &&
                repeatAsset.Kind ==
                    OmsiAssetKind
                        .SceneryObject)
            {
                var restarted =
                    await Viewport
                        .BeginSceneryPlacementAsync(
                            _session
                                .OmsiRootPath,
                            repeatAsset);

                if (restarted)
                {
                    PlaceAssetButton.Content =
                        "Cancelar posicionamento";

                    StatusText.Text +=
                        " · Repetir continua ativo.";
                }
            }
        }
        catch (Exception exception)
        {
            _patternLineStart =
                null;

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

    private IReadOnlyList<
        NativeSceneryPlacementRequest>
        BuildPatternRequests(
            NativeSceneryPlacementRequest template,
            IReadOnlyList<
                NativeSceneryPatternPlacement>
                placements)
    {
        var map =
            _session.CurrentMap?
                .Map;

        if (map is null)
        {
            return Array.Empty<
                NativeSceneryPlacementRequest>();
        }

        var result =
            new List<
                NativeSceneryPlacementRequest>(
                    placements.Count);

        foreach (
            var placement in
                placements.Take(256))
        {
            var tileX =
                (int)Math.Floor(
                    placement.WorldX /
                    300.0);

            var tileY =
                (int)Math.Floor(
                    placement.WorldZ /
                    300.0);

            var tile =
                map.Tiles
                    .FirstOrDefault(
                        candidate =>
                            candidate.X ==
                                tileX &&
                            candidate.Y ==
                                tileY);

            if (tile is null)
            {
                continue;
            }

            result.Add(
                template with
                {
                    Tile = tile,
                    X =
                        placement.WorldX -
                        tileX *
                        300.0,
                    Y =
                        placement.WorldZ -
                        tileY *
                        300.0,
                    Z = placement.Z,
                    Rotation =
                        placement.Rotation,
                    Pitch =
                        placement.Pitch,
                    Bank =
                        placement.Bank,
                    WorldPoint =
                        new Vector3(
                            (float)
                                placement.WorldX,
                            template.WorldPoint.Y,
                            (float)
                                placement.WorldZ)
                });
        }

        return result;
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
        if (_synchronizingExplorer)
        {
            return;
        }

        if (
            ExplorerListView.SelectedItem is
                NativeJunctionSuggestion
                    suggestion)
        {
            Viewport.FocusWorldPoint(
                suggestion.WorldPoint,
                55);

            StatusText.Text =
                $"Encontro #{suggestion.SplineA}/#{suggestion.SplineB} · tile {suggestion.TileX},{suggestion.TileY} · duplo clique para usar como alvo.";

            return;
        }

        if (
            ExplorerListView.SelectedItem is not
                NativeExplorerItem item)
        {
            return;
        }

        Viewport.SelectExplorerItem(
            item,
            focus: false);
    }

    private async void OnExplorerDoubleTapped(
        object sender,
        DoubleTappedRoutedEventArgs e)
    {
        if (
            ExplorerListView.SelectedItem is
                NativeJunctionSuggestion
                    suggestion)
        {
            _junctionPlacementTarget =
                suggestion;

            Viewport.FocusWorldPoint(
                suggestion.WorldPoint,
                55);

            ObjectPlacementModeComboBox
                .SelectedIndex =
                0;

            await ActivateLibraryToolAsync(
                1,
                null,
                $"Alvo de cruzamento definido entre splines #{suggestion.SplineA} e #{suggestion.SplineB}. Escolha um SCO compatível e clique Posicionar.");

            var junctionIndex =
                _libraryGroupOptions
                    .Select(
                        (option, index) =>
                            (
                                option,
                                index
                            ))
                    .FirstOrDefault(
                        pair =>
                            pair.option.Group ==
                            OmsiAssetLibraryGroup
                                .Junctions)
                    .index;

            if (
                junctionIndex >= 0 &&
                junctionIndex <
                    _libraryGroupOptions.Count)
            {
                LibraryViewComboBox
                    .SelectedIndex =
                    0;

                LibraryGroupComboBox
                    .SelectedIndex =
                    junctionIndex;

                RefreshLibrarySubcategoryOptions();
                RefreshLibraryFilter();
            }

            return;
        }

        if (
            ExplorerListView.SelectedItem is
                ValidationExplorerItem validation &&
            validation.AssetKind is
                { } repairKind &&
            !string.IsNullOrWhiteSpace(
                validation.AssetPath))
        {
            _dependencyRepairKind =
                repairKind;

            _dependencyRepairOldPath =
                validation.AssetPath;

            _junctionPlacementTarget =
                null;

            await ActivateLibraryToolAsync(
                repairKind ==
                    OmsiAssetKind.SceneryObject
                    ? 1
                    : 2,
                null,
                $"Reparo: escolha um {(repairKind == OmsiAssetKind.SceneryObject ? "SCO" : "SLI")} para substituir {validation.AssetPath}.");

            LibraryViewComboBox.SelectedIndex =
                0;

            LibraryGroupComboBox.SelectedIndex =
                0;

            RefreshLibrarySubcategoryOptions();
            RefreshLibraryFilter();

            RepairDependencyButton.Visibility =
                Visibility.Visible;

            RepairDependencyButton.Content =
                $"Substituir {Path.GetFileName(validation.AssetPath)} pelo asset selecionado";

            RepairDependencyButton.IsEnabled =
                AssetLibraryListView.SelectedItem is
                    OmsiAssetIndexEntry selected &&
                selected.Kind ==
                    repairKind;

            return;
        }

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

            RefreshLibraryGroupOptions(
                null);

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

            RefreshLibraryGroupOptions(
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

            RefreshLibraryCollectionOptions();
            RefreshLibraryFilter();
        }
        catch (Exception exception)
        {
            LibraryStatusText.Text =
                $"Falha ao abrir biblioteca: {exception.Message}";
        }
    }

    private void RefreshLibraryGroupOptions(
        OmsiAssetKind? kind)
    {
        var previous =
            LibraryGroupComboBox
                .SelectedItem is
                LibraryGroupOption option
                ? option.Group
                : OmsiAssetLibraryGroup.All;

        _libraryGroupOptions =
            kind is null
                ? [
                    new LibraryGroupOption(
                        OmsiAssetLibraryGroup.All,
                        OmsiAssetLibraryClassifier
                            .GetDisplayName(
                                OmsiAssetLibraryGroup.All))
                  ]
                : OmsiAssetLibraryClassifier
                    .GetGroupsForKind(
                        kind.Value)
                    .Select(
                        group =>
                            new LibraryGroupOption(
                                group,
                                OmsiAssetLibraryClassifier
                                    .GetDisplayName(
                                        group)))
                    .ToArray();

        LibraryGroupComboBox.ItemsSource =
            _libraryGroupOptions;

        var index =
            _libraryGroupOptions
                .Select(
                    (item, itemIndex) =>
                        (
                            item,
                            itemIndex
                        ))
                .FirstOrDefault(
                    pair =>
                        pair.item.Group ==
                            previous)
                .itemIndex;

        if (
            index < 0 ||
            index >=
                _libraryGroupOptions.Count)
        {
            index = 0;
        }

        LibraryGroupComboBox.SelectedIndex =
            index;

        RefreshLibrarySubcategoryOptions();
        RefreshLibraryTechnicalFilterOptions(
            kind);
    }

    private void RefreshLibraryTechnicalFilterOptions(
        OmsiAssetKind? kind)
    {
        var previous =
            LibraryTechnicalFilterComboBox
                .SelectedItem
                ?.ToString() ??
            "Todos";

        var options =
            kind switch
            {
                OmsiAssetKind.SceneryObject =>
                    new[]
                    {
                        "Todos",
                        "Usados",
                        "Árvores",
                        "Carregados",
                        "Problemas"
                    },

                OmsiAssetKind.Spline =>
                    new[]
                    {
                        "Todos",
                        "Usados",
                        "Carregados",
                        "Problemas"
                    },

                _ =>
                    new[]
                    {
                        "Todos"
                    }
            };

        LibraryTechnicalFilterComboBox.ItemsSource =
            options;

        var index =
            Array.FindIndex(
                options,
                value =>
                    string.Equals(
                        value,
                        previous,
                        StringComparison.OrdinalIgnoreCase));

        LibraryTechnicalFilterComboBox.SelectedIndex =
            index >= 0
                ? index
                : 0;

        LibraryTechnicalFilterComboBox.IsEnabled =
            options.Length >
            1;
    }

    private void RefreshLibrarySubcategoryOptions()
    {
        var previous =
            LibrarySubcategoryComboBox
                .SelectedItem
                ?.ToString() ??
            "Todas";

        IEnumerable<
            OmsiAssetIndexEntry> items =
            _assetLibraryItems;

        if (
            LibraryGroupComboBox
                .SelectedItem is
                LibraryGroupOption option &&
            option.Group !=
                OmsiAssetLibraryGroup.All)
        {
            items =
                items.Where(
                    item =>
                        OmsiAssetLibraryClassifier
                            .Classify(item) ==
                        option.Group);
        }

        var values =
            items
                .Where(
                    item =>
                        item.Kind is
                            OmsiAssetKind.SceneryObject or
                            OmsiAssetKind.Spline)
                .Select(
                    OmsiAssetLibraryClassifier
                        .GetSubcategory)
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    value => value,
                    StringComparer.CurrentCultureIgnoreCase)
                .Prepend("Todas")
                .ToArray();

        LibrarySubcategoryComboBox.ItemsSource =
            values;

        var index =
            Array.FindIndex(
                values,
                value =>
                    string.Equals(
                        value,
                        previous,
                        StringComparison.OrdinalIgnoreCase));

        LibrarySubcategoryComboBox.SelectedIndex =
            index >= 0
                ? index
                : 0;
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

        var view =
            LibraryViewComboBox
                .SelectedIndex;

        if (view == 1)
        {
            var favorites =
                _assetLibraryState
                    .Favorites
                    .ToHashSet(
                        StringComparer
                            .OrdinalIgnoreCase);

            items =
                items.Where(
                    item =>
                        favorites.Contains(
                            item.RelativePath));
        }
        else if (view == 2)
        {
            var order =
                _assetLibraryState
                    .Recent
                    .Select(
                        (path, index) =>
                            (
                                path,
                                index
                            ))
                    .ToDictionary(
                        pair =>
                            pair.path,
                        pair =>
                            pair.index,
                        StringComparer
                            .OrdinalIgnoreCase);

            items =
                items.Where(
                        item =>
                            order.ContainsKey(
                                item.RelativePath))
                    .OrderBy(
                        item =>
                            order[
                                item.RelativePath]);
        }
        else if (view == 3)
        {
            items =
                items.Where(
                        item =>
                            _assetLibraryState
                                .Usage
                                .ContainsKey(
                                    item.RelativePath))
                    .OrderByDescending(
                        item =>
                            _assetLibraryState
                                .Usage[
                                    item.RelativePath]);
        }
        else if (view == 4)
        {
            var activeCollection =
                GetActiveLibraryCollectionName();

            var collection =
                _assetLibraryState
                    .Collections
                    .TryGetValue(
                        activeCollection,
                        out var paths)
                    ? paths.ToHashSet(
                        StringComparer
                            .OrdinalIgnoreCase)
                    : new HashSet<string>(
                        StringComparer
                            .OrdinalIgnoreCase);

            items =
                items.Where(
                    item =>
                        collection.Contains(
                            item.RelativePath));
        }

        if (
            view == 0 &&
            LibraryGroupComboBox
                .SelectedItem is
                LibraryGroupOption option &&
            option.Group !=
                OmsiAssetLibraryGroup.All)
        {
            items =
                items.Where(
                    item =>
                        OmsiAssetLibraryClassifier
                            .Classify(
                                item) ==
                        option.Group);
        }

        if (
            view == 0 &&
            LibrarySubcategoryComboBox
                .SelectedItem is
                string subcategory &&
            !string.Equals(
                subcategory,
                "Todas",
                StringComparison.OrdinalIgnoreCase))
        {
            items =
                items.Where(
                    item =>
                        string.Equals(
                            OmsiAssetLibraryClassifier
                                .GetSubcategory(item),
                            subcategory,
                            StringComparison.OrdinalIgnoreCase));
        }

        var technicalFilter =
            LibraryTechnicalFilterComboBox
                .SelectedItem
                ?.ToString() ??
            "Todos";

        if (
            !string.Equals(
                technicalFilter,
                "Todos",
                StringComparison.OrdinalIgnoreCase))
        {
            var technicalState =
                Viewport
                    .GetAssetTechnicalSnapshot();

            items =
                items.Where(
                    item =>
                        MatchesTechnicalFilter(
                            item,
                            technicalFilter,
                            technicalState));
        }

        if (
            !string.IsNullOrWhiteSpace(
                query))
        {
            items =
                items.Where(
                    item =>
                        OmsiAssetLibraryClassifier
                            .MatchesSmartSearch(
                                item.RelativePath,
                                query));
        }

        var filtered =
            items.ToArray();

        AssetLibraryListView.ItemsSource =
            filtered;

        if (_assetLibraryItems.Count > 0)
        {
            var groupName =
                LibraryGroupComboBox
                    .SelectedItem is
                    LibraryGroupOption selectedGroup
                    ? selectedGroup.Label
                    : "Todos";

            var viewName =
                LibraryViewComboBox
                    .SelectedItem is
                    ComboBoxItem viewItem
                    ? viewItem.Content
                        ?.ToString() ??
                      "Grupos"
                    : "Grupos";

            var subcategoryName =
                LibrarySubcategoryComboBox
                    .SelectedItem
                    ?.ToString() ??
                "Todas";

            var technicalName =
                LibraryTechnicalFilterComboBox
                    .SelectedItem
                    ?.ToString() ??
                "Todos";

            var collectionName =
                view == 4
                    ? GetActiveLibraryCollectionName()
                    : string.Empty;

            LibraryStatusText.Text =
                $"{filtered.Length} exibido(s) de {_assetLibraryItems.Count} · {viewName} · {groupName} · {subcategoryName} · {technicalName}" +
                (view == 4
                    ? $" · {collectionName}"
                    : string.Empty);
        }
    }

    private static bool MatchesTechnicalFilter(
        OmsiAssetIndexEntry item,
        string filter,
        NativeAssetTechnicalSnapshot state)
    {
        var path =
            item.RelativePath;

        return item.Kind switch
        {
            OmsiAssetKind.SceneryObject =>
                filter switch
                {
                    "Usados" =>
                        state.UsedScenery
                            .Contains(path),
                    "Árvores" =>
                        state.TreeScenery
                            .Contains(path),
                    "Carregados" =>
                        state.LoadedScenery
                            .Contains(path),
                    "Problemas" =>
                        state.ProblemScenery
                            .Contains(path),
                    _ =>
                        true
                },

            OmsiAssetKind.Spline =>
                filter switch
                {
                    "Usados" =>
                        state.UsedSplines
                            .Contains(path),
                    "Carregados" =>
                        state.LoadedSplines
                            .Contains(path),
                    "Problemas" =>
                        state.ProblemSplines
                            .Contains(path),
                    _ =>
                        true
                },

            _ =>
                true
        };
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
        _junctionPlacementTarget =
            null;

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
        _junctionPlacementTarget =
            null;

        SetSelectionModeFromShortcut(
            2);

        await ActivateLibraryToolAsync(
            2,
            null,
            "Ruas/Splines: escolha uma SLI e use o construtor reto ou curvo.");
    }

    private void OnToolCrossingsClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_session.CurrentMap is null)
        {
            StatusText.Text =
                "Cruzamentos: abra um mapa primeiro.";

            return;
        }

        _assetPreviewCancellation
            ?.Cancel();

        Viewport.CancelSceneryPlacement();
        Viewport.CancelSplinePlacement();
        Viewport.RestoreSceneView();

        SetSelectionModeFromShortcut(
            2);

        _libraryMode =
            false;

        _transportMode =
            false;

        _trafficMode =
            false;

        _validationMode =
            false;

        _junctionMode =
            true;

        _junctionPlacementTarget =
            null;

        TrafficControlPanel.Visibility =
            Visibility.Collapsed;

        TransportPanel.Visibility =
            Visibility.Collapsed;

        AssetLibraryPanel.Visibility =
            Visibility.Collapsed;

        ExplorerListView.Visibility =
            Visibility.Visible;

        ExplorerSearchBox.PlaceholderText =
            "Buscar encontros de splines...";

        ExplorerSearchBox.Text =
            string.Empty;

        _junctionSuggestions =
            Viewport
                .GetJunctionSuggestions();

        RefreshJunctionSuggestionFilter();

        StatusText.Text =
            _junctionSuggestions.Count == 0
                ? "Assistente de cruzamentos: nenhum encontro geométrico detectado nas splines carregadas."
                : $"Assistente de cruzamentos: {_junctionSuggestions.Count} encontro(s) detectado(s). Selecione para focar; duplo clique para usar o alvo.";
    }

    private void RefreshJunctionSuggestionFilter()
    {
        var query =
            ExplorerSearchBox.Text
                .Trim();

        IEnumerable<
            NativeJunctionSuggestion>
            items =
                _junctionSuggestions;

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
                                StringComparison.OrdinalIgnoreCase) ||
                        item.SplineA
                            .ToString()
                            .Contains(
                                query,
                                StringComparison.OrdinalIgnoreCase) ||
                        item.SplineB
                            .ToString()
                            .Contains(
                                query,
                                StringComparison.OrdinalIgnoreCase));
        }

        ExplorerListView.ItemsSource =
            items.ToArray();
    }

    private void OnToolTerrainClick(
        object sender,
        RoutedEventArgs e)
    {
        _junctionPlacementTarget =
            null;

        SetSelectionModeFromShortcut(
            3);

        StatusText.Text =
            "Terreno: clique no chão para selecionar tile; nivelamento e pintura ficam no Inspector.";
    }

    private async void OnToolWaterClick(
        object sender,
        RoutedEventArgs e)
    {
        _junctionPlacementTarget =
            null;

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

        _validationMode =
            false;

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
            $"Tipo: {(rule.IsKillRule ? "[kill_rule]" : "[rule]")}");
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

        _validationMode =
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
        if (
            TransportListView.SelectedItem is not
                TransportExplorerItem item)
        {
            TransportDetailText.Text =
                "Selecione um item para ver detalhes.";

            Viewport
                .ClearTimetableRoutePreview();

            return;
        }

        TransportDetailText.Text =
            item.Detail;

        if (_timetableCatalog is null)
        {
            return;
        }

        if (item.Kind == "Track")
        {
            var track =
                _timetableCatalog.Tracks
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                candidate.Name,
                                item.Key,
                                StringComparison.OrdinalIgnoreCase));

            if (track is not null)
            {
                var resolved =
                    Viewport
                        .PreviewTimetableTrack(
                            track.Entries);

                StatusText.Text =
                    $"Track {track.Name}: {resolved}/{track.Entries.Count} segmento(s) resolvido(s) no mapa carregado.";
            }

            return;
        }

        if (item.Kind == "StationLink")
        {
            var link =
                _timetableCatalog
                    .StationLinks
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                $"{candidate.StartBusStopId}>{candidate.EndBusStopId}",
                                item.Key,
                                StringComparison.OrdinalIgnoreCase));

            if (link is not null)
            {
                var resolved =
                    Viewport
                        .PreviewStationLink(
                            link.Entries);

                StatusText.Text =
                    $"StationLink {item.Key}: {resolved}/{link.Entries.Count} segmento(s) resolvido(s).";
            }

            return;
        }

        Viewport
            .ClearTimetableRoutePreview();
    }

    private async void OnToolValidationClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.CurrentMap is not
                { } snapshot ||
            _session.OmsiRootPath is not
                { } omsiRoot)
        {
            StatusText.Text =
                "Validação: abra um mapa OMSI primeiro.";

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
            false;

        _trafficMode =
            false;

        _validationMode =
            true;

        _trafficPreviewTimer.Stop();

        AssetLibraryPanel.Visibility =
            Visibility.Collapsed;

        TransportPanel.Visibility =
            Visibility.Collapsed;

        TrafficControlPanel.Visibility =
            Visibility.Collapsed;

        ExplorerListView.Visibility =
            Visibility.Visible;

        ExplorerSearchBox.PlaceholderText =
            "Buscar problemas de validação...";

        StatusText.Text =
            "Validação: analisando mapa, TTData, links e dependências...";

        try
        {
            _validationItems =
                await BuildValidationItemsAsync(
                    snapshot,
                    omsiRoot);

            RefreshValidationFilter();

            var errors =
                _validationItems.Count(
                    item =>
                        item.Severity ==
                        "Erro");

            var warnings =
                _validationItems.Count(
                    item =>
                        item.Severity ==
                        "Aviso");

            StatusText.Text =
                $"Validação concluída: {errors} erro(s) · {warnings} aviso(s) · {_validationItems.Count} item(ns).";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha na validação: {exception.Message}";
        }
    }

    private void RefreshValidationFilter()
    {
        var query =
            ExplorerSearchBox.Text
                .Trim();

        IEnumerable<
            ValidationExplorerItem> items =
                _validationItems;

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
                                StringComparison.OrdinalIgnoreCase) ||
                        item.Detail
                            .Contains(
                                query,
                                StringComparison.OrdinalIgnoreCase) ||
                        item.Code
                            .Contains(
                                query,
                                StringComparison.OrdinalIgnoreCase));
        }

        ExplorerListView.ItemsSource =
            items.ToArray();
    }

    private static async Task<IReadOnlyList<
        ValidationExplorerItem>>
        BuildValidationItemsAsync(
            NativeMapSnapshot snapshot,
            string omsiRoot)
    {
        var result =
            new List<
                ValidationExplorerItem>();

        var allIds =
            new Dictionary<
                int,
                List<string>>();

        void RegisterId(
            int id,
            string description)
        {
            if (!allIds.TryGetValue(
                    id,
                    out var values))
            {
                values = [];
                allIds[id] =
                    values;
            }

            values.Add(
                description);
        }

        foreach (
            var tile in snapshot.Tiles)
        {
            foreach (
                var item in
                    tile.Content.Objects)
            {
                RegisterId(
                    item.ObjectId,
                    $"Objeto {item.SceneryObjectPath} · tile {tile.Reference.X},{tile.Reference.Y}");

                if (
                    !OmsiSceneryObjectPathResolver
                        .TryResolve(
                            omsiRoot,
                            item.SceneryObjectPath,
                            out var fullPath) ||
                    !File.Exists(
                        fullPath))
                {
                    result.Add(
                        new ValidationExplorerItem(
                            "Erro",
                            "missing-sco",
                            $"ERRO · SCO ausente · #{item.ObjectId}",
                            $"{item.SceneryObjectPath}\nTile {tile.Reference.X},{tile.Reference.Y}",
                            OmsiAssetKind.SceneryObject,
                            item.SceneryObjectPath));
                }
            }

            foreach (
                var item in
                    tile.Content.Splines)
            {
                RegisterId(
                    item.SplineId,
                    $"Spline {item.SplinePath} · tile {tile.Reference.X},{tile.Reference.Y}");

                if (
                    !OmsiSplinePathResolver
                        .TryResolve(
                            omsiRoot,
                            item.SplinePath,
                            out var fullPath) ||
                    !File.Exists(
                        fullPath))
                {
                    result.Add(
                        new ValidationExplorerItem(
                            "Erro",
                            "missing-sli",
                            $"ERRO · SLI ausente · #{item.SplineId}",
                            $"{item.SplinePath}\nTile {tile.Reference.X},{tile.Reference.Y}",
                            OmsiAssetKind.Spline,
                            item.SplinePath));
                }
            }
        }

        foreach (
            var duplicate in allIds
                .Where(
                    pair =>
                        pair.Value.Count >
                        1))
        {
            result.Add(
                new ValidationExplorerItem(
                    "Erro",
                    "duplicate-id",
                    $"ERRO · ID duplicado · {duplicate.Key}",
                    string.Join(
                        "\n",
                        duplicate.Value)));
        }

        var fullMapLoaded =
            snapshot.Tiles.Count ==
            snapshot.Map.Tiles.Count;

        if (fullMapLoaded)
        {
            var splineIds =
                snapshot.Tiles
                    .SelectMany(
                        tile =>
                            tile.Content.Splines)
                    .Select(
                        item =>
                            item.SplineId)
                    .ToHashSet();

            foreach (
                var spline in snapshot.Tiles
                    .SelectMany(
                        tile =>
                            tile.Content.Splines))
            {
                if (
                    spline.PreviousSplineId >
                        0 &&
                    !splineIds.Contains(
                        spline.PreviousSplineId))
                {
                    result.Add(
                        new ValidationExplorerItem(
                            "Erro",
                            "broken-spline-previous",
                            $"ERRO · link anterior ausente · spline #{spline.SplineId}",
                            $"PreviousSplineId={spline.PreviousSplineId}\n{spline.SplinePath}"));
                }

                if (
                    spline.NextSplineId >
                        0 &&
                    !splineIds.Contains(
                        spline.NextSplineId))
                {
                    result.Add(
                        new ValidationExplorerItem(
                            "Erro",
                            "broken-spline-next",
                            $"ERRO · link seguinte ausente · spline #{spline.SplineId}",
                            $"NextSplineId={spline.NextSplineId}\n{spline.SplinePath}"));
                }
            }
        }
        else
        {
            result.Add(
                new ValidationExplorerItem(
                    "Aviso",
                    "partial-map",
                    "AVISO · validação parcial de links",
                    "O mapa está no modo 3×3; links para tiles não carregados não foram marcados como erro."));
        }

        var timetable =
            await new OmsiTimetableCatalogReader()
                .ReadAsync(
                    snapshot.Map.DirectoryPath);

        if (
            timetable
                .BrokenTripTrackReferenceCount >
            0)
        {
            result.Add(
                new ValidationExplorerItem(
                    "Erro",
                    "trip-track",
                    $"ERRO · {timetable.BrokenTripTrackReferenceCount} Trip→Track quebrado(s)",
                    "Há .ttp referenciando Track inexistente."));
        }

        if (
            timetable
                .BrokenStationLinkStopReferenceCount >
            0)
        {
            result.Add(
                new ValidationExplorerItem(
                    "Erro",
                    "stationlink-stop",
                    $"ERRO · {timetable.BrokenStationLinkStopReferenceCount} StationLink→Stop quebrado(s)",
                    "Há StnLinks.cfg referenciando Busstops inexistentes."));
        }

        if (
            timetable
                .BrokenLineTripReferenceCount >
            0)
        {
            result.Add(
                new ValidationExplorerItem(
                    "Erro",
                    "line-trip",
                    $"ERRO · {timetable.BrokenLineTripReferenceCount} Line→Trip quebrado(s)",
                    "Há .ttl referenciando .ttp inexistente."));
        }

        var splinePathCounts =
            new Dictionary<
                string,
                int>(
                    StringComparer.OrdinalIgnoreCase);

        var objectPathCounts =
            new Dictionary<
                string,
                int>(
                    StringComparer.OrdinalIgnoreCase);

        var splineReader =
            new OmsiSplineDefinitionReader();

        var sceneryReader =
            new OmsiSceneryObjectReader();

        foreach (
            var tile in snapshot.Tiles)
        {
            foreach (
                var spline in
                    tile.Content.Splines)
            {
                if (
                    spline.TrafficRules.Count ==
                    0)
                {
                    continue;
                }

                if (
                    !splinePathCounts.TryGetValue(
                        spline.SplinePath,
                        out var pathCount))
                {
                    pathCount = -1;

                    if (
                        OmsiSplinePathResolver
                            .TryResolve(
                                omsiRoot,
                                spline.SplinePath,
                                out var path) &&
                        File.Exists(path))
                    {
                        pathCount =
                            (
                                await splineReader
                                    .ReadAsync(path)
                            ).Paths.Count;
                    }

                    splinePathCounts[
                        spline.SplinePath] =
                        pathCount;
                }

                foreach (
                    var rule in
                        spline.TrafficRules)
                {
                    if (
                        rule.PathIndex is
                            int index &&
                        pathCount >= 0 &&
                        (
                            index < 0 ||
                            index >=
                                pathCount
                        ))
                    {
                        result.Add(
                            new ValidationExplorerItem(
                                "Erro",
                                "rule-path-index",
                                $"ERRO · Traffic Rule fora do path · spline #{spline.SplineId}",
                                $"{rule.RuleName} · path {index} · paths disponíveis {pathCount}\n{spline.SplinePath}"));
                    }
                }
            }

            foreach (
                var item in
                    tile.Content.Objects)
            {
                if (
                    item.TrafficRules.Count ==
                    0)
                {
                    continue;
                }

                if (
                    !objectPathCounts.TryGetValue(
                        item.SceneryObjectPath,
                        out var pathCount))
                {
                    pathCount = -1;

                    if (
                        OmsiSceneryObjectPathResolver
                            .TryResolve(
                                omsiRoot,
                                item.SceneryObjectPath,
                                out var path) &&
                        File.Exists(path))
                    {
                        pathCount =
                            (
                                await sceneryReader
                                    .ReadMetadataAsync(path)
                            ).Paths.Count;
                    }

                    objectPathCounts[
                        item.SceneryObjectPath] =
                        pathCount;
                }

                foreach (
                    var rule in
                        item.TrafficRules)
                {
                    if (
                        rule.PathIndex is
                            int index &&
                        pathCount >= 0 &&
                        (
                            index < 0 ||
                            index >=
                                pathCount
                        ))
                    {
                        result.Add(
                            new ValidationExplorerItem(
                                "Erro",
                                "rule-path-index",
                                $"ERRO · Traffic Rule fora do path · objeto #{item.ObjectId}",
                                $"{rule.RuleName} · path {index} · paths disponíveis {pathCount}\n{item.SceneryObjectPath}"));
                    }
                }
            }
        }

        if (result.Count == 0)
        {
            result.Add(
                new ValidationExplorerItem(
                    "OK",
                    "ok",
                    "OK · nenhuma inconsistência detectada",
                    "Assets, IDs, links, TTData e Traffic Rules passaram nas validações disponíveis."));
        }

        return result;
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

        _validationMode =
            false;

        _junctionMode =
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

        RepairDependencyButton.Visibility =
            _dependencyRepairKind is not null &&
            !string.IsNullOrWhiteSpace(
                _dependencyRepairOldPath)
                ? Visibility.Visible
                : Visibility.Collapsed;

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
        if (
            _referenceOverlayMapDirectory is
                { } overlayMap &&
            !string.Equals(
                overlayMap,
                snapshot.Map.DirectoryPath,
                StringComparison.OrdinalIgnoreCase))
        {
            ClearReferenceOverlay();
        }

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

    private async void OnCreateCoordinateMapClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_session.OmsiRootPath is null)
        {
            StatusText.Text =
                "Selecione primeiro a instalação do OMSI para criar um mapa.";

            return;
        }

        var directoryBox =
            new TextBox
            {
                Header =
                    "Nome da pasta",
                PlaceholderText =
                    "MeuMapaReal"
            };

        var displayBox =
            new TextBox
            {
                Header =
                    "Nome exibido",
                PlaceholderText =
                    "Meu mapa real"
            };

        var latitudeBox =
            new NumberBox
            {
                Header =
                    "Latitude",
                Minimum =
                    -90,
                Maximum =
                    90,
                Value =
                    -23.55052,
                SmallChange =
                    0.00001,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var longitudeBox =
            new NumberBox
            {
                Header =
                    "Longitude",
                Minimum =
                    -180,
                Maximum =
                    180,
                Value =
                    -46.633308,
                SmallChange =
                    0.00001,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    430
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Cria um novo mapa usando o template NewMap do OMSI e grava uma âncora geográfica para referências/elevacão.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.75
            });

        panel.Children.Add(
            directoryBox);

        panel.Children.Add(
            displayBox);

        panel.Children.Add(
            latitudeBox);

        panel.Children.Add(
            longitudeBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Mapa real por coordenadas",
                Content =
                    panel,
                PrimaryButtonText =
                    "Criar mapa",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        var result =
            await dialog
                .ShowAsync();

        if (
            result !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        if (
            string.IsNullOrWhiteSpace(
                directoryBox.Text) ||
            string.IsNullOrWhiteSpace(
                displayBox.Text) ||
            !double.IsFinite(
                latitudeBox.Value) ||
            !double.IsFinite(
                longitudeBox.Value))
        {
            StatusText.Text =
                "Dados inválidos para criação do mapa real.";

            return;
        }

        try
        {
            StatusText.Text =
                "Criando mapa a partir do template OMSI...";

            var created =
                await _session
                    .CreateCoordinateMapAsync(
                        directoryBox.Text,
                        displayBox.Text,
                        latitudeBox.Value,
                        longitudeBox.Value);

            _fullMapMode =
                true;

            await ApplyMapSnapshotAsync(
                created.Snapshot,
                focusActiveTile: false);

            RootText.Text =
                $"OMSI: {_session.OmsiRootPath}\nMapas encontrados: {_session.Maps.Count}";

            StatusText.Text =
                $"Mapa {created.Snapshot.Map.DisplayName} criado em {created.DirectoryPath} · âncora {created.Latitude:F6}, {created.Longitude:F6}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao criar mapa por coordenadas: {exception.Message}";
        }
    }

    private async void OnGoogleMapReferenceClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Referência Google: abra um mapa primeiro.";

            return;
        }

        NativeMapGeoreference? georeference;

        try
        {
            georeference =
                await _session
                    .LoadMapGeoreferenceAsync();
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Não foi possível ler a georreferência: {exception.Message}";

            return;
        }

        if (georeference is null)
        {
            StatusText.Text =
                "Salve primeiro a georreferência do mapa.";

            return;
        }

        var apiKeyBox =
            new PasswordBox
            {
                Header =
                    "Google Maps Platform API key",
                PlaceholderText =
                    "A chave não será salva"
            };

        var opacityBox =
            new NumberBox
            {
                Header =
                    "Opacidade",
                Minimum =
                    0.05,
                Maximum =
                    1.0,
                Value =
                    0.55,
                SmallChange =
                    0.05
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    440
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"Centro: {georeference.Latitude:F6}, {georeference.Longitude:F6} · zoom {georeference.Zoom} · {georeference.MapType}",
                TextWrapping =
                    TextWrapping.Wrap
            });

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "A imagem será encaixada pela âncora geográfica e acompanhará o relevo do terreno. Ela não participa do picking.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.75
            });

        panel.Children.Add(
            apiKeyBox);

        panel.Children.Add(
            opacityBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Referência Google sobre o terreno",
                Content =
                    panel,
                PrimaryButtonText =
                    "Carregar",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        var answer =
            await dialog
                .ShowAsync();

        if (
            answer !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        if (
            string.IsNullOrWhiteSpace(
                apiKeyBox.Password) ||
            !double.IsFinite(
                opacityBox.Value))
        {
            StatusText.Text =
                "Dados inválidos para carregar a referência Google.";

            return;
        }

        try
        {
            StatusText.Text =
                "Carregando referência Google...";

            var reference =
                await _session
                    .LoadGoogleMapReferenceAsync(
                        apiKeyBox.Password);

            var opacity =
                (float)Math.Clamp(
                    opacityBox.Value,
                    0.05,
                    1.0);

            Viewport.SetReferenceOverlay(
                new NativeReferenceOverlayDefinition(
                    reference.ImagePath,
                    reference.Width,
                    reference.Height,
                    reference.MetersPerPixel,
                    reference.AnchorWorldX,
                    reference.AnchorWorldZ,
                    opacity,
                    reference.Attribution));

            _referenceOverlayMapDirectory =
                snapshot.Map
                    .DirectoryPath;

            StatusText.Text =
                $"Referência Google ativa · {reference.WidthMeters:F1} × {reference.HeightMeters:F1} m · {reference.MetersPerPixel:F3} m/pixel · opacidade {opacity:P0}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao carregar referência Google: {exception.Message}";
        }
    }

    private void OnClearGoogleMapReferenceClick(
        object sender,
        RoutedEventArgs e)
    {
        ClearReferenceOverlay();

        StatusText.Text =
            "Referência Google removida do viewport.";
    }

    private void ClearReferenceOverlay()
    {
        Viewport.SetReferenceOverlay(
            null);

        _referenceOverlayMapDirectory =
            null;
    }

    private async void OnGoogleElevationClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.CurrentMap is not
                { } snapshot ||
            snapshot.ActiveTile is not
                { } active)
        {
            StatusText.Text =
                "Elevação Google: abra um mapa e mantenha um tile ativo.";

            return;
        }

        NativeMapGeoreference? georeference;

        try
        {
            georeference =
                await _session
                    .LoadMapGeoreferenceAsync();
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Não foi possível ler a georreferência: {exception.Message}";
            return;
        }

        if (georeference is null)
        {
            StatusText.Text =
                "Salve primeiro a georreferência do mapa.";

            return;
        }

        var apiKeyBox =
            new PasswordBox
            {
                Header =
                    "Google Maps Platform API key",
                PlaceholderText =
                    "A chave é usada somente nesta operação"
            };

        var samplesBox =
            new NumberBox
            {
                Header =
                    "Amostras por eixo",
                Minimum =
                    3,
                Maximum =
                    33,
                Value =
                    17,
                SmallChange =
                    2
            };

        var offsetBox =
            new NumberBox
            {
                Header =
                    "Offset vertical (m)",
                Minimum =
                    -10000,
                Maximum =
                    10000,
                Value =
                    0,
                SmallChange =
                    1
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    440
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"Tile ativo: {active.X},{active.Y}\nÂncora: {georeference.Latitude:F6}, {georeference.Longitude:F6}",
                TextWrapping =
                    TextWrapping.Wrap
            });

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "A chave não é salva pelo Map Studio. O relevo retornado será interpolado para a grade .terrain do tile.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.75
            });

        panel.Children.Add(
            apiKeyBox);

        panel.Children.Add(
            samplesBox);

        panel.Children.Add(
            offsetBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Google Elevation",
                Content =
                    panel,
                PrimaryButtonText =
                    "Buscar relevo",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        var answer =
            await dialog
                .ShowAsync();

        if (
            answer !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        if (
            string.IsNullOrWhiteSpace(
                apiKeyBox.Password) ||
            !double.IsFinite(
                samplesBox.Value) ||
            !double.IsFinite(
                offsetBox.Value))
        {
            StatusText.Text =
                "Dados inválidos para buscar a elevação.";

            return;
        }

        var sampleCount =
            Math.Clamp(
                (int)Math.Round(
                    samplesBox.Value),
                3,
                33);

        try
        {
            StatusText.Text =
                $"Buscando elevação real para tile {active.X},{active.Y} ({sampleCount}×{sampleCount})...";

            var grid =
                await _session
                    .LoadGoogleElevationGridAsync(
                        apiKeyBox.Password,
                        active.X,
                        active.Y,
                        sampleCount);

            var confirm =
                new ContentDialog
                {
                    XamlRoot =
                        MainRoot.XamlRoot,
                    Title =
                        "Aplicar relevo real?",
                    Content =
                        $"Tile {grid.TileX},{grid.TileY}\n" +
                        $"Amostras: {grid.Rows}×{grid.Columns}\n" +
                        $"Elevação mínima: {grid.MinimumElevation:F2} m\n" +
                        $"Elevação máxima: {grid.MaximumElevation:F2} m\n" +
                        $"Offset: {offsetBox.Value:F2} m\n\n" +
                        "O arquivo .terrain atual será salvo em backup antes da alteração.",
                    PrimaryButtonText =
                        "Aplicar",
                    CloseButtonText =
                        "Cancelar",
                    DefaultButton =
                        ContentDialogButton
                            .Close
                };

            var apply =
                await confirm
                    .ShowAsync();

            if (
                apply !=
                    ContentDialogResult
                        .Primary)
            {
                StatusText.Text =
                    "Grade de elevação carregada, mas não aplicada.";

                return;
            }

            var result =
                await _session
                    .ApplyTerrainElevationGridAsync(
                        grid,
                        offsetBox.Value);

            await ApplyMapSnapshotAsync(
                result.Snapshot,
                focusActiveTile: true);

            StatusText.Text =
                result.ChangedSamples ==
                    0
                    ? "A grade de elevação não alterou amostras do terreno."
                    : $"Relevo real aplicado: {result.ChangedSamples} amostra(s) alterada(s). Backup: {result.BackupDirectory}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha no Google Elevation: {exception.Message}";
        }
    }

    private async void OnEditMapGeoreferenceClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Abra um mapa antes de editar a georreferência.";

            return;
        }

        NativeMapGeoreference? current =
            null;

        try
        {
            current =
                await _session
                    .LoadMapGeoreferenceAsync();
        }
        catch
        {
        }

        var active =
            snapshot.ActiveTile ??
            OmsiTileRegionSelector
                .FindInitialTile(
                    snapshot.Map.Tiles);

        var latitudeBox =
            new NumberBox
            {
                Header =
                    "Latitude da âncora",
                Minimum =
                    -90,
                Maximum =
                    90,
                Value =
                    current?.Latitude ??
                    0,
                SmallChange =
                    0.00001
            };

        var longitudeBox =
            new NumberBox
            {
                Header =
                    "Longitude da âncora",
                Minimum =
                    -180,
                Maximum =
                    180,
                Value =
                    current?.Longitude ??
                    0,
                SmallChange =
                    0.00001
            };

        var tileXBox =
            new NumberBox
            {
                Header =
                    "Tile X",
                Value =
                    current?.AnchorTileX ??
                    active?.X ??
                    0
            };

        var tileYBox =
            new NumberBox
            {
                Header =
                    "Tile Y",
                Value =
                    current?.AnchorTileY ??
                    active?.Y ??
                    0
            };

        var localXBox =
            new NumberBox
            {
                Header =
                    "X local da âncora",
                Minimum =
                    0,
                Maximum =
                    300,
                Value =
                    current?.AnchorX ??
                    150
            };

        var localYBox =
            new NumberBox
            {
                Header =
                    "Y local da âncora",
                Minimum =
                    0,
                Maximum =
                    300,
                Value =
                    current?.AnchorY ??
                    150
            };

        var zoomBox =
            new NumberBox
            {
                Header =
                    "Zoom",
                Minimum =
                    0,
                Maximum =
                    22,
                Value =
                    current?.Zoom ??
                    18,
                SmallChange =
                    1
            };

        var mapType =
            new ComboBox
            {
                Header =
                    "Tipo de mapa",
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                SelectedIndex =
                    (current?.MapType
                        ?.ToLowerInvariant()) switch
                    {
                        "roadmap" => 0,
                        "satellite" => 1,
                        "terrain" => 3,
                        _ => 2
                    }
            };

        mapType.Items.Add(
            "roadmap");

        mapType.Items.Add(
            "satellite");

        mapType.Items.Add(
            "hybrid");

        mapType.Items.Add(
            "terrain");

        var coordinatesGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        coordinatesGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        coordinatesGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            latitudeBox,
            0);

        Grid.SetColumn(
            longitudeBox,
            1);

        coordinatesGrid.Children.Add(
            latitudeBox);

        coordinatesGrid.Children.Add(
            longitudeBox);

        var tileGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        tileGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        tileGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            tileXBox,
            0);

        Grid.SetColumn(
            tileYBox,
            1);

        tileGrid.Children.Add(
            tileXBox);

        tileGrid.Children.Add(
            tileYBox);

        var localGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        localGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        localGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            localXBox,
            0);

        Grid.SetColumn(
            localYBox,
            1);

        localGrid.Children.Add(
            localXBox);

        localGrid.Children.Add(
            localYBox);

        var panel =
            new StackPanel
            {
                Spacing =
                    7,
                MinWidth =
                    460
            };

        panel.Children.Add(
            coordinatesGrid);

        panel.Children.Add(
            tileGrid);

        panel.Children.Add(
            localGrid);

        panel.Children.Add(
            zoomBox);

        panel.Children.Add(
            mapType);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Georreferência do mapa",
                Content =
                    panel,
                PrimaryButtonText =
                    "Salvar",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        var result =
            await dialog
                .ShowAsync();

        if (
            result !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        var values =
            new[]
            {
                latitudeBox.Value,
                longitudeBox.Value,
                tileXBox.Value,
                tileYBox.Value,
                localXBox.Value,
                localYBox.Value,
                zoomBox.Value
            };

        if (
            values.Any(
                value =>
                    !double.IsFinite(
                        value)))
        {
            StatusText.Text =
                "Georreferência inválida.";

            return;
        }

        try
        {
            var path =
                await _session
                    .SaveMapGeoreferenceAsync(
                        new NativeMapGeoreference(
                            latitudeBox.Value,
                            longitudeBox.Value,
                            checked(
                                (int)Math.Round(
                                    tileXBox.Value)),
                            checked(
                                (int)Math.Round(
                                    tileYBox.Value)),
                            localXBox.Value,
                            localYBox.Value,
                            checked(
                                (int)Math.Round(
                                    zoomBox.Value)),
                            mapType.SelectedItem
                                ?.ToString() ??
                            "hybrid",
                            "Google Maps"));

            ClearReferenceOverlay();

            StatusText.Text =
                $"Georreferência salva em {path}. A referência visual anterior foi removida para evitar desalinhamento.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar georreferência: {exception.Message}";
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

    private async void OnConstructionSetsClick(
        object sender,
        RoutedEventArgs e)
    {
        var selection =
            _selectionInfo;

        if (
            selection is null ||
            selection.Kind !=
                PickingKind.Spline ||
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Construction Sets: selecione primeiro uma spline no mapa.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de aplicar Construction Sets.";

            return;
        }

        var tile =
            snapshot.Tiles
                .FirstOrDefault(
                    candidate =>
                        candidate.Reference.X ==
                            selection.TileX &&
                        candidate.Reference.Y ==
                            selection.TileY);

        var placedSpline =
            tile?.Content.Splines
                .FirstOrDefault(
                    candidate =>
                        candidate.SplineId ==
                            selection.EntityId);

        if (
            tile is null ||
            placedSpline is null)
        {
            StatusText.Text =
                "Não foi possível localizar a spline selecionada no snapshot atual.";

            return;
        }

        var splineEntity =
            new NativeSplineEntity(
                PickingId.None,
                tile.Reference,
                placedSpline,
                (float)(
                    tile.Reference.X *
                        300.0 +
                    placedSpline.X),
                (float)
                    placedSpline.Z,
                (float)(
                    tile.Reference.Y *
                        300.0 +
                    placedSpline.Y));

        var editing =
            new List<
                NativeConstructionSetCompanion>();

        var savedSets =
            _assetLibraryState
                .ConstructionSets
                .ToArray();

        var savedCombo =
            new ComboBox
            {
                Header =
                    "Set salvo",
                ItemsSource =
                    savedSets,
                DisplayMemberPath =
                    "Name",
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome do set",
                Text =
                    "Novo Construction Set"
            };

        var lockSpline =
            new CheckBox
            {
                Content =
                    "Vincular ao tipo desta spline",
                IsChecked =
                    true
            };

        var companionPath =
            new TextBox
            {
                Header =
                    "SCO companheiro",
                Text =
                    AssetLibraryListView
                        .SelectedItem is
                        OmsiAssetIndexEntry
                            selectedAsset &&
                    selectedAsset.Kind ==
                        OmsiAssetKind
                            .SceneryObject
                        ? selectedAsset
                            .RelativePath
                        : string.Empty,
                PlaceholderText =
                    @"SceneryobjectsPastaobjeto.sco"
            };

        var spacingBox =
            new NumberBox
            {
                Header =
                    "Espaçamento (m)",
                Minimum =
                    1,
                Maximum =
                    200,
                Value =
                    20,
                SmallChange =
                    1
            };

        var offsetBox =
            new NumberBox
            {
                Header =
                    "Offset lateral (m)",
                Minimum =
                    0,
                Maximum =
                    100,
                Value =
                    4,
                SmallChange =
                    0.5
            };

        var rotationBox =
            new NumberBox
            {
                Header =
                    "Rotação adicional (°)",
                Minimum =
                    -360,
                Maximum =
                    360,
                Value =
                    0,
                SmallChange =
                    5
            };

        var sideCombo =
            new ComboBox
            {
                Header =
                    "Lado",
                SelectedIndex =
                    2,
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        sideCombo.Items.Add(
            "Esquerdo");

        sideCombo.Items.Add(
            "Direito");

        sideCombo.Items.Add(
            "Ambos");

        var companionsList =
            new ListView
            {
                Height =
                    150,
                SelectionMode =
                    ListViewSelectionMode
                        .Single
            };

        void RefreshCompanions()
        {
            companionsList.ItemsSource =
                editing
                    .Select(
                        item =>
                            $"{item.SceneryObjectPath} · {item.Spacing:F1}m · offset {item.LateralOffset:F1}m · {DescribeConstructionSide(item.Side)} · rot {item.RotationOffset:F0}°")
                    .ToArray();
        }

        var addButton =
            new Button
            {
                Content =
                    "Adicionar companheiro",
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        addButton.Click +=
            (_, _) =>
            {
                var path =
                    companionPath.Text
                        .Trim();

                if (
                    string.IsNullOrWhiteSpace(
                        path))
                {
                    return;
                }

                var spacing =
                    double.IsFinite(
                        spacingBox.Value)
                        ? Math.Max(
                            1,
                            spacingBox.Value)
                        : 20;

                var offset =
                    double.IsFinite(
                        offsetBox.Value)
                        ? Math.Max(
                            0,
                            offsetBox.Value)
                        : 4;

                var rotation =
                    double.IsFinite(
                        rotationBox.Value)
                        ? rotationBox.Value
                        : 0;

                var side =
                    sideCombo
                        .SelectedIndex switch
                    {
                        0 =>
                            NativeConstructionSetSide.Left,
                        1 =>
                            NativeConstructionSetSide.Right,
                        _ =>
                            NativeConstructionSetSide.Both
                    };

                editing.Add(
                    new NativeConstructionSetCompanion(
                        Guid.NewGuid()
                            .ToString("N"),
                        path,
                        spacing,
                        offset,
                        side,
                        rotation));

                RefreshCompanions();
            };

        var removeButton =
            new Button
            {
                Content =
                    "Remover selecionado",
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        removeButton.Click +=
            (_, _) =>
            {
                var index =
                    companionsList
                        .SelectedIndex;

                if (
                    index < 0 ||
                    index >=
                        editing.Count)
                {
                    return;
                }

                editing.RemoveAt(
                    index);

                RefreshCompanions();
            };

        var deleteSetButton =
            new Button
            {
                Content =
                    "Excluir set salvo",
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                IsEnabled =
                    savedSets.Length >
                    0
            };

        void LoadSet(
            NativeConstructionSetDefinition?
                set)
        {
            editing.Clear();

            if (set is null)
            {
                nameBox.Text =
                    "Novo Construction Set";

                lockSpline.IsChecked =
                    true;

                RefreshCompanions();
                return;
            }

            nameBox.Text =
                set.Name;

            lockSpline.IsChecked =
                !string.IsNullOrWhiteSpace(
                    set.SplinePath);

            editing.AddRange(
                set.Companions);

            RefreshCompanions();
        }

        savedCombo.SelectionChanged +=
            (_, _) =>
            {
                LoadSet(
                    savedCombo.SelectedItem as
                        NativeConstructionSetDefinition);
            };

        deleteSetButton.Click +=
            (_, _) =>
            {
                if (
                    savedCombo.SelectedItem is not
                        NativeConstructionSetDefinition
                            selectedSet)
                {
                    return;
                }

                _assetLibraryState
                    .ConstructionSets
                    .RemoveAll(
                        item =>
                            string.Equals(
                                item.Id,
                                selectedSet.Id,
                                StringComparison.OrdinalIgnoreCase));

                SaveAssetLibraryState();

                savedSets =
                    _assetLibraryState
                        .ConstructionSets
                        .ToArray();

                savedCombo.ItemsSource =
                    savedSets;

                savedCombo.SelectedIndex =
                    -1;

                deleteSetButton.IsEnabled =
                    savedSets.Length >
                    0;

                LoadSet(null);
            };

        if (savedSets.Length > 0)
        {
            savedCombo.SelectedIndex =
                0;
        }

        var companionGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        companionGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        companionGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            spacingBox,
            0);

        Grid.SetColumn(
            offsetBox,
            1);

        companionGrid.Children.Add(
            spacingBox);

        companionGrid.Children.Add(
            offsetBox);

        var controls =
            new StackPanel
            {
                Spacing =
                    7,
                MinWidth =
                    480
            };

        controls.Children.Add(
            new TextBlock
            {
                Text =
                    $"Spline #{selection.EntityId} · {selection.AssetPath}",
                TextWrapping =
                    TextWrapping.Wrap
            });

        controls.Children.Add(
            savedCombo);

        controls.Children.Add(
            nameBox);

        controls.Children.Add(
            lockSpline);

        controls.Children.Add(
            companionPath);

        controls.Children.Add(
            companionGrid);

        controls.Children.Add(
            sideCombo);

        controls.Children.Add(
            rotationBox);

        controls.Children.Add(
            addButton);

        controls.Children.Add(
            companionsList);

        controls.Children.Add(
            removeButton);

        controls.Children.Add(
            deleteSetButton);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Construction Sets",
                Content =
                    new ScrollViewer
                    {
                        Content =
                            controls,
                        MaxHeight =
                            620
                    },
                PrimaryButtonText =
                    "Aplicar",
                SecondaryButtonText =
                    "Salvar set",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        var result =
            await dialog
                .ShowAsync();

        var name =
            nameBox.Text
                .Trim();

        if (
            result ==
                ContentDialogResult
                    .Secondary)
        {
            if (
                string.IsNullOrWhiteSpace(
                    name) ||
                editing.Count ==
                    0)
            {
                StatusText.Text =
                    "Construction Set não salvo: informe nome e pelo menos um companheiro.";

                return;
            }

            var existing =
                savedCombo.SelectedItem as
                    NativeConstructionSetDefinition;

            var saved =
                new NativeConstructionSetDefinition(
                    existing?.Id ??
                        Guid.NewGuid()
                            .ToString("N"),
                    name,
                    lockSpline.IsChecked ==
                        true
                        ? selection.AssetPath
                        : null,
                    editing
                        .Take(16)
                        .ToArray());

            _assetLibraryState
                .ConstructionSets
                .RemoveAll(
                    item =>
                        string.Equals(
                            item.Id,
                            saved.Id,
                            StringComparison.OrdinalIgnoreCase));

            _assetLibraryState
                .ConstructionSets
                .Add(
                    saved);

            SaveAssetLibraryState();

            StatusText.Text =
                $"Construction Set “{saved.Name}” salvo com {saved.Companions.Count} companheiro(s).";

            return;
        }

        if (
            result !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        if (editing.Count == 0)
        {
            StatusText.Text =
                "Construction Set vazio: adicione pelo menos um SCO companheiro.";

            return;
        }

        var definition =
            new NativeConstructionSetDefinition(
                "temporary",
                string.IsNullOrWhiteSpace(
                    name)
                    ? "Construction Set"
                    : name,
                lockSpline.IsChecked ==
                    true
                    ? selection.AssetPath
                    : null,
                editing
                    .Take(16)
                    .ToArray());

        try
        {
            var generated =
                NativeConstructionSetBuilder
                    .Build(
                        splineEntity,
                        definition);

            var groups =
                BuildConstructionSetBatchGroups(
                    generated);

            var total =
                groups.Sum(
                    group =>
                        group.Placements.Count);

            if (
                groups.Count == 0 ||
                total == 0)
            {
                StatusText.Text =
                    "Construction Set não gerou posições válidas dentro dos tiles do mapa.";

                return;
            }

            StatusText.Text =
                $"Aplicando Construction Set: {groups.Count} grupo(s), {total} objeto(s)...";

            var updated =
                await _session
                    .InsertSceneryObjectMultiBatchAsync(
                        groups);

            await ApplyMapSnapshotAsync(
                updated,
                focusActiveTile: false);

            foreach (
                var group in
                    groups)
            {
                RecordAssetUsage(
                    group
                        .SceneryObjectPath);
            }

            StatusText.Text =
                $"Construction Set aplicado: {total} objeto(s) em {groups.Count} grupo(s), com backup único.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao aplicar Construction Set: {exception.Message}";
        }
    }

    private IReadOnlyList<
        NativeSceneryPlacementBatchGroup>
        BuildConstructionSetBatchGroups(
            IReadOnlyList<
                NativeConstructionSetPlacementGroup>
                generated)
    {
        var map =
            _session.CurrentMap?
                .Map;

        if (map is null)
        {
            return Array.Empty<
                NativeSceneryPlacementBatchGroup>();
        }

        var result =
            new List<
                NativeSceneryPlacementBatchGroup>();

        foreach (
            var group in
                generated.Take(16))
        {
            var placements =
                new List<
                    NativeSceneryPlacementRequest>();

            foreach (
                var placement in
                    group.Placements
                        .Take(256))
            {
                var tileX =
                    (int)Math.Floor(
                        placement.WorldX /
                        300.0);

                var tileY =
                    (int)Math.Floor(
                        placement.WorldZ /
                        300.0);

                var tile =
                    map.Tiles
                        .FirstOrDefault(
                            candidate =>
                                candidate.X ==
                                    tileX &&
                                candidate.Y ==
                                    tileY);

                if (tile is null)
                {
                    continue;
                }

                placements.Add(
                    new NativeSceneryPlacementRequest(
                        tile,
                        group.SceneryObjectPath,
                        placement.WorldX -
                            tileX *
                            300.0,
                        placement.WorldZ -
                            tileY *
                            300.0,
                        placement.Z,
                        placement.Rotation,
                        placement.Pitch,
                        placement.Bank,
                        new Vector3(
                            (float)
                                placement.WorldX,
                            (float)
                                placement.Z,
                            (float)
                                placement.WorldZ),
                        UsesAbsoluteHeight:
                            false));
            }

            if (placements.Count > 0)
            {
                result.Add(
                    new NativeSceneryPlacementBatchGroup(
                        group
                            .SceneryObjectPath,
                        placements));
            }
        }

        return result;
    }

    private static string DescribeConstructionSide(
        NativeConstructionSetSide side) =>
        side switch
        {
            NativeConstructionSetSide.Left =>
                "esquerdo",
            NativeConstructionSetSide.Right =>
                "direito",
            _ =>
                "ambos"
        };

    private async void OnReplaceDependencyClick(
        object sender,
        RoutedEventArgs e)
    {
        var selection =
            _selectionInfo;

        if (
            selection is null ||
            selection.Kind is not
                (
                    PickingKind.Object or
                    PickingKind.Spline
                ))
        {
            StatusText.Text =
                "Substituição: selecione primeiro um objeto ou spline no mapa.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de substituir dependências.";

            return;
        }

        var expectedKind =
            selection.Kind ==
                PickingKind.Object
                ? OmsiAssetKind
                    .SceneryObject
                : OmsiAssetKind
                    .Spline;

        var suggestedPath =
            AssetLibraryListView
                .SelectedItem is
                OmsiAssetIndexEntry
                    selectedAsset &&
            selectedAsset.Kind ==
                expectedKind
                ? selectedAsset
                    .RelativePath
                : selection.AssetPath;

        var pathBox =
            new TextBox
            {
                Text =
                    suggestedPath,
                Header =
                    selection.Kind ==
                        PickingKind.Object
                        ? "Novo caminho SCO"
                        : "Novo caminho SLI",
                PlaceholderText =
                    selection.Kind ==
                        PickingKind.Object
                        ? @"SceneryobjectsPastaobjeto.sco"
                        : @"SplinesPastaua.sli",
                MinWidth = 430
            };

        var content =
            new StackPanel
            {
                Spacing = 8
            };

        content.Children.Add(
            new TextBlock
            {
                Text =
                    $"Atual: {selection.AssetPath}",
                TextWrapping =
                    TextWrapping.Wrap
            });

        content.Children.Add(
            new TextBlock
            {
                Text =
                    "A substituição será feita em todos os tiles do mapa e um backup transacional será criado.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.75
            });

        content.Children.Add(
            pathBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    selection.Kind ==
                        PickingKind.Object
                        ? "Substituir objeto no mapa inteiro"
                        : "Substituir spline no mapa inteiro",
                Content =
                    content,
                PrimaryButtonText =
                    "Substituir",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        var result =
            await dialog
                .ShowAsync();

        if (
            result !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        var newPath =
            pathBox.Text
                .Trim();

        if (string.IsNullOrWhiteSpace(
                newPath))
        {
            StatusText.Text =
                "Substituição cancelada: informe o novo caminho do asset.";

            return;
        }

        try
        {
            StatusText.Text =
                $"Substituindo {selection.AssetPath} por {newPath}...";

            var replacement =
                await _session
                    .ReplaceMapAssetPathAsync(
                        selection.Kind,
                        selection.AssetPath,
                        newPath);

            await ApplyMapSnapshotAsync(
                replacement.Snapshot,
                focusActiveTile: false);

            StatusText.Text =
                replacement.Replacements ==
                    0
                    ? "Nenhuma referência correspondente foi encontrada no mapa."
                    : $"{replacement.Replacements} referência(s) substituída(s) em {replacement.FilesSaved} arquivo(s). Backup: {replacement.BackupDirectory}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao substituir dependência: {exception.Message}";
        }
    }

    private async void OnRestoreBackupClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Restauração: abra um mapa primeiro.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de restaurar um backup.";

            return;
        }

        var backupDirectory =
            await PickFolderAsync();

        if (string.IsNullOrWhiteSpace(
                backupDirectory))
        {
            return;
        }

        var backupsRoot =
            Path.GetFullPath(
                Path.Combine(
                    snapshot.Map
                        .DirectoryPath,
                    ".mapstudio-backups"));

        var selected =
            Path.GetFullPath(
                backupDirectory);

        var relative =
            Path.GetRelativePath(
                backupsRoot,
                selected);

        var valid =
            !Path.IsPathRooted(
                relative) &&
            !relative.Equals(
                "..",
                StringComparison.Ordinal) &&
            !relative.StartsWith(
                ".." +
                Path.DirectorySeparatorChar,
                StringComparison.Ordinal) &&
            !relative.StartsWith(
                ".." +
                Path.AltDirectorySeparatorChar,
                StringComparison.Ordinal);

        if (!valid)
        {
            StatusText.Text =
                "A pasta escolhida não pertence a .mapstudio-backups do mapa aberto.";

            return;
        }

        var confirm =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Restaurar backup?",
                Content =
                    $"Origem: {selected}\n\nOs arquivos atuais serão preservados em um novo backup de rollback antes da restauração.",
                PrimaryButtonText =
                    "Restaurar",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Close
            };

        var answer =
            await confirm
                .ShowAsync();

        if (
            answer !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Restaurando backup com rollback de segurança...";

            var restored =
                await _session
                    .RestoreMapStudioBackupAsync(
                        selected);

            await ApplyMapSnapshotAsync(
                restored.Snapshot,
                focusActiveTile: false);

            StatusText.Text =
                $"{restored.FilesRestored} arquivo(s) restaurado(s). Rollback salvo em: {restored.RollbackBackupDirectory}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao restaurar backup: {exception.Message}";
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
