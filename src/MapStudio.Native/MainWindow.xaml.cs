using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MapStudio.Core.AI;
using MapStudio.Core.Commercial;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Generation.Vegetation;
using MapStudio.Core.Omsi.Buildings;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Timetables;
using MapStudio.Core.Omsi.Traffic;
using MapStudio.Core.Workspace;
using MapStudio.Native.Controls;
using MapStudio.Native.Dialogs;
using MapStudio.Native.Services;
using MapStudio.Native.ViewModels;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MapStudio.Native;

public sealed partial class MainWindow : Window
{
    private sealed record TransportTripRouteOption(
        bool UsesStationLinks,
        string Label,
        string TrackName);

    private sealed record TransportStationLinkSourceOption(
        string Kind,
        string Key,
        string Label);

    private sealed record TransportRouteStepItem(
        int Sequence,
        int EntityId,
        string PathIndex,
        double? Length,
        string SourceLabel,
        string DisplayText);

    private sealed record ConstructionHistoryEntry(
        string Label,
        string MapDirectory,
        string BackupDirectory);

    private sealed record TrafficRuleExplorerItem(
        PickingKind OwnerKind,
        int EntityId,
        int TileX,
        int TileY,
        string AssetPath,
        int RuleIndex,
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

    private sealed record LibraryCategoryTreeItem(
        OmsiAssetLibraryGroup Group,
        string? Subcategory,
        string Label)
    {
        public override string ToString() =>
            Label;
    }

    private sealed record MapCatalogViewItem(
        OmsiMapDescriptor Map,
        string DisplayText);

    private sealed record TileManagerViewItem(
        int X,
        int Y,
        string DisplayText,
        bool IsActive,
        bool IsLoaded);

    private sealed record AttachmentInspectorItem(
        OmsiTileReference Tile,
        OmsiPlacedAttachment Attachment,
        string DisplayText);

    private sealed class AssetLibraryViewItem
        : INotifyPropertyChanged
    {
        private Microsoft.UI.Xaml.Media.ImageSource?
            _thumbnailSource;

        public AssetLibraryViewItem(
            OmsiAssetIndexEntry asset,
            string displayName,
            string detail,
            string kindLabel,
            Microsoft.UI.Xaml.Media.ImageSource?
                thumbnailSource)
        {
            Asset =
                asset;

            DisplayName =
                displayName;

            Detail =
                detail;

            KindLabel =
                kindLabel;

            _thumbnailSource =
                thumbnailSource;
        }

        public event PropertyChangedEventHandler?
            PropertyChanged;

        public OmsiAssetIndexEntry Asset
        {
            get;
        }

        public string DisplayName
        {
            get;
        }

        public string Detail
        {
            get;
        }

        public string KindLabel
        {
            get;
        }

        public string RelativePath =>
            Asset.RelativePath;

        public Microsoft.UI.Xaml.Media.ImageSource?
            ThumbnailSource =>
            _thumbnailSource;

        public void SetThumbnailPath(
            string? path)
        {
            _thumbnailSource =
                string.IsNullOrWhiteSpace(
                    path) ||
                !File.Exists(path)
                    ? null
                    : new Microsoft.UI.Xaml.Media.Imaging
                        .BitmapImage(
                            new Uri(
                                path,
                                UriKind.Absolute));

            PropertyChanged
                ?.Invoke(
                    this,
                    new PropertyChangedEventArgs(
                        nameof(
                            ThumbnailSource)));
        }
    }

    private sealed record RoadProfileOption(
        string Label,
        string ProfileId,
        int LaneCount,
        bool OneWay,
        double WidthMeters);

    private sealed record AiProfileOption(
        string Label,
        MapStudioAiConnectionProfile? Profile);

    private readonly OmsiNativeSession _session =
        new();

    private readonly List<MapStudioRoadTrace>
        _proceduralRoadTraces =
            [];

    private readonly List<MapStudioRoadPoint>
        _activeRoadTracePoints =
            [];

    private bool _proceduralRoadTraceMode;
    private int _proceduralRoadTraceSequence;
    private RoadProfileOption?
        _activeRoadProfile;

    private MapStudioCommercialState
        _commercialState =
            MapStudioCommercialState
                .DevelopmentPreview();

    private MapStudioAiConnectionSettings
        _aiConnectionSettings =
            NativeAiConnectionSettingsStore
                .Load();

    private bool
        _refreshingAiProfileSelector;

    private NativeAssetLibraryState
        _assetLibraryState =
            NativeAssetLibraryStateStore
                .Load();

    private readonly List<
        ConstructionHistoryEntry>
        _constructionUndoStack = [];

    private readonly List<
        ConstructionHistoryEntry>
        _constructionRedoStack = [];

    private string?
        _constructionHistoryMapDirectory;

    private bool _isMapToolPaletteDragging;
    private uint _mapToolPaletteDragPointerId;
    private double _mapToolPaletteDragStartX;
    private double _mapToolPaletteDragStartY;
    private double _mapToolPaletteOriginX;
    private double _mapToolPaletteOriginY;

    private string?
        _referenceOverlayMapDirectory;

    private NativeGoogleMapReference?
        _activeGoogleMapReference;

    private string?
        _terrainLayerVisibilityMapDirectory;

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
        OmsiAssetIndexEntry>
        _assetLibraryAllItems =
            Array.Empty<
                OmsiAssetIndexEntry>();

    private string?
        _assetLibraryCacheRootPath;

    private bool
        _syncingLibraryFilters;

    private readonly Dictionary<
        string,
        AssetLibraryViewItem>
        _assetLibraryViewItemCache =
            new(
                StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<
        LibraryGroupOption>
        _libraryGroupOptions =
            [
                new(
                    OmsiAssetLibraryGroup.All,
                    "Todos")
            ];

    private bool _libraryMode;
    private bool _mapMode;
    private CancellationTokenSource?
        _mapExplorerRefreshCancellation;
    private bool _assetPlacementOptionsExpanded;
    private bool _transportMode;
    private bool _transportPathsVisible;
    private bool _transportTrackRecordMode;
    private bool _transportTrackRecordBusy;
    private bool _syncingTrafficPathControls;
    private bool _syncingTransportPathChoice;
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

    private readonly DispatcherTimer
        _libraryFilterDebounceTimer =
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

    private readonly TransportViewModel
        _transportViewModel =
            new();

    private TimetableWindow?
        _timetableWindow;

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

    private bool _desktopExplorerWasVisible = true;
    private bool _desktopInspectorWasVisible = true;

    private bool _draggingFullscreenExplorer;
    private bool _draggingFullscreenInspector;
    private uint _fullscreenPanelDragPointerId;
    private double _fullscreenPanelDragStartX;
    private double _fullscreenPanelDragStartY;
    private double _fullscreenPanelDragOriginX;
    private double _fullscreenPanelDragOriginY;
    private double _fullscreenExplorerOffsetX;
    private double _fullscreenExplorerOffsetY;
    private double _fullscreenInspectorOffsetX;
    private double _fullscreenInspectorOffsetY;

    private bool _resizingFullscreenExplorer;
    private uint _fullscreenExplorerResizePointerId;
    private double _fullscreenExplorerResizeStartX;
    private double _fullscreenExplorerResizeStartY;
    private double _fullscreenExplorerResizeOriginWidth;
    private double _fullscreenExplorerResizeOriginHeight;

    private NativeRoadElevationMode _roadElevationMode =
        NativeRoadElevationMode.FollowTerrain;

    private double _fullscreenExplorerHeight =
        650;

    private bool _fullMapMode = true;
    private bool _mapLoadModeChanging;
    private bool _tileManagerDialogOpen;
    private bool _standaloneWorkspaceInitialized;
    private int _loadingOperationDepth;

    private double _explorerPanelWidth =
        310;

    private double _inspectorPanelWidth =
        330;

    public MainWindow()
    {
        InitializeComponent();

        RefreshAiConnectionUi();

        _trafficPreviewTimer.Interval =
            TimeSpan.FromMilliseconds(
                250);

        _trafficPreviewTimer.Tick +=
            OnTrafficPreviewTimerTick;

        _libraryFilterDebounceTimer.Interval =
            TimeSpan.FromMilliseconds(
                160);

        _libraryFilterDebounceTimer.Tick +=
            (_, _) =>
            {
                _libraryFilterDebounceTimer.Stop();

                if (_libraryMode)
                {
                    RefreshLibraryFilter();
                }
            };

        TrafficViewComboBox.SelectionChanged +=
            OnTrafficViewSelectionChanged;

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

        var displayArea =
            DisplayArea.GetFromWindowId(
                windowId,
                DisplayAreaFallback.Primary);

        var workArea =
            displayArea.WorkArea;

        var initialWidth =
            Math.Min(
                1440,
                Math.Max(
                    320,
                    workArea.Width - 32));

        var initialHeight =
            Math.Min(
                900,
                Math.Max(
                    300,
                    workArea.Height - 48));

        _appWindow.Resize(
            new SizeInt32(
                initialWidth,
                initialHeight));

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

        Viewport.SelectionContextActionRequested +=
            async action =>
            {
                switch (action)
                {
                    case NativeSelectionContextAction.Move:
                        Viewport.SetGizmoMode(
                            NativeGizmoMode.Move);
                        StatusText.Text =
                            "Ferramenta mover ativa.";
                        break;

                    case NativeSelectionContextAction.Rotate:
                        Viewport.SetGizmoMode(
                            NativeGizmoMode.Rotate);
                        StatusText.Text =
                            "Ferramenta girar ativa.";
                        break;

                    case NativeSelectionContextAction.Duplicate:
                        if (_selectionInfo is not null)
                        {
                            await StartSelectionCopyPlacementAsync();
                        }
                        break;

                    case NativeSelectionContextAction.Delete:
                        await DeleteCurrentSelectionAsync();
                        break;

                    case NativeSelectionContextAction.Focus:
                        FocusCurrentSelection();
                        break;

                    case NativeSelectionContextAction.EditCurve:
                        if (
                            Viewport
                                .BeginSelectedSplineCurveEdit(
                                    out var curveStatus))
                        {
                            StatusText.Text =
                                curveStatus;
                        }
                        else
                        {
                            StatusText.Text =
                                curveStatus;
                        }
                        break;

                    case NativeSelectionContextAction.Split:
                        Viewport.CancelSplinePlacement();

                        StatusText.Text =
                            Viewport
                                .BeginSplineSplitPick()
                                ? "Ruas: Dividir ativo. Clique no ponto do trecho."
                                : "Ruas: selecione uma spline para dividir.";
                        break;

                    case NativeSelectionContextAction.Parallel:
                        await CreateParallelRoadAsync(
                            1.0);
                        break;

                    case NativeSelectionContextAction.Elevate:
                        ShiftSelectedRoadElevation(
                            GetRoadElevationStep());
                        break;

                    case NativeSelectionContextAction.Level:
                        LevelSelectedRoadAtCurrentHeight();
                        break;

                    case NativeSelectionContextAction.Lower:
                        ShiftSelectedRoadElevation(
                            -GetRoadElevationStep());
                        break;

                    case NativeSelectionContextAction.Replace:
                        OnRoadReplaceTypeClick(
                            this,
                            null!);
                        break;

                    case NativeSelectionContextAction.Mirror:
                        OnRoadMirrorClick(
                            this,
                            null!);
                        break;

                    case NativeSelectionContextAction.Flow:
                        OnRoadReverseFlowClick(
                            this,
                            null!);
                        break;

                    case NativeSelectionContextAction.Inspector:
                        var inspectorVisible =
                            InspectorPanel.Visibility ==
                                Visibility.Visible &&
                            (
                                IsFullscreen() ||
                                InspectorColumn.Width.Value >
                                    0
                            );

                        if (!inspectorVisible)
                        {
                            ToggleInspectorPanel();
                        }

                        StatusText.Text =
                            "Inspector aberto para a seleção atual.";
                        break;
                }
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

        Viewport.SplineSplitRequested +=
            async request =>
            {
                await HandleSplineSplitAsync(
                    request);
            };

        Viewport.SplinePlacementControlStateChanged +=
            state =>
            {
                var visible =
                    state is not null &&
                    state.AwaitingConfirmation;

                EasyRoadControlGrid.Visibility =
                    visible
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                if (state is null)
                {
                    return;
                }

                EasyRoadStartXBox.Value =
                    state.Start.X;

                EasyRoadStartZBox.Value =
                    state.Start.Z;

                EasyRoadEndXBox.Value =
                    state.End.X;

                EasyRoadEndZBox.Value =
                    state.End.Z;

                var maxGradient =
                    double.IsFinite(
                        RoadMaxGradientBox.Value)
                        ? Math.Clamp(
                            RoadMaxGradientBox.Value,
                            1.0,
                            40.0)
                        : 12.0;

                if (
                    state.Length >
                        0.1 &&
                    Math.Abs(
                        state.Gradient) >
                        maxGradient)
                {
                    StatusText.Text =
                        $"⚠ Rua com inclinação {state.Gradient:+0.0;-0.0;0.0}% · " +
                        $"limite recomendado {maxGradient:0.#}%.";
                }
            };

        Viewport.TerrainPointSelected +=
            point =>
            {
                if (_proceduralRoadTraceMode)
                {
                    var roadPoint =
                        new MapStudioRoadPoint(
                            point.WorldPoint.X,
                            point.WorldPoint.Z);

                    if (
                        _activeRoadTracePoints.Count ==
                            0 ||
                        _activeRoadTracePoints[^1]
                            .DistanceTo(
                                roadPoint) >=
                            0.20)
                    {
                        _activeRoadTracePoints.Add(
                            roadPoint);
                    }

                    FinishProceduralRoadTraceMenuItem
                        .IsEnabled =
                        _activeRoadTracePoints.Count >=
                        2;

                    StatusText.Text =
                        $"Traçado procedural: {_activeRoadTracePoints.Count} ponto(s) na linha atual · clique novos pontos ou finalize a linha.";

                    return;
                }

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

                RaiseTerrainButton.IsEnabled =
                    true;

                LowerTerrainButton.IsEnabled =
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

        Viewport.TrafficPathNodeFocused +=
            node =>
            {
                TrafficPathSelectedOnlyCheckBox.IsChecked =
                    true;

                TransportPathsSelectedOnlyCheckBox.IsChecked =
                    true;

                Viewport
                    .SetTrafficPathSelectedOnly(
                        true);

                Viewport
                    .SetTrafficPathFocusedIndex(
                        node.PathIndex);

                if (_transportMode)
                {
                    TransportPathIndexBox.Value =
                        node.PathIndex;

                    RefreshTransportPathChoices();
                }

                UpdateTrafficPathStatusText();

                var kindLabel =
                    node.Type switch
                    {
                        1 => "HUM",
                        2 => "RAIL",
                        3 => "AIR",
                        _ => "CAR"
                    };

                StatusText.Text =
                    $"Path {node.PathIndex} · {kindLabel} · " +
                    (
                        node.IsStart
                            ? "nó inicial"
                            : "nó final"
                    ) +
                    " focado no viewport.";
            };

        Viewport.SelectionChanged +=
            info =>
            {
                _selectionInfo =
                    info;

                if (_transportMode)
                {
                    Viewport
                        .SetTrafficPathFocusedIndex(
                            null);

                    RefreshTransportPathChoices();
                }

                if (
                    Viewport.TrafficPathSelectedOnly)
                {
                    Viewport
                        .RefreshTrafficPathDisplay();

                    UpdateTrafficPathStatusText();
                }

                if (
                    _transportTrackRecordMode &&
                    !_transportTrackRecordBusy &&
                    info is
                    {
                        Kind:
                            PickingKind.Object or
                            PickingKind.Spline
                    })
                {
                    _ =
                        AppendTransportSelectionToTrackAsync(
                            info);
                }

                ApplyInspectorButton.IsEnabled =
                    info is not null;

                DeleteSelectionButton.IsEnabled =
                    info is not null;

                DuplicateSelectionButton.IsEnabled =
                    info is not null;

                SaveSplineLinksButton.IsEnabled =
                    info?.Kind ==
                    PickingKind.Spline;

                EditSplineAdvancedButton.IsEnabled =
                    info?.Kind ==
                    PickingKind.Spline;

                EditSceneryPathButton.IsEnabled =
                    info?.Kind is
                        PickingKind.Object or
                        PickingKind.Spline;

                AddAssetPathButton.IsEnabled =
                    info?.Kind is
                        PickingKind.Object or
                        PickingKind.Spline;

                DuplicateAssetPathButton.IsEnabled =
                    info?.Kind is
                        PickingKind.Object or
                        PickingKind.Spline;

                DeleteAssetPathButton.IsEnabled =
                    info?.Kind is
                        PickingKind.Object or
                        PickingKind.Spline;

                CompleteToSplineButton.IsEnabled =
                    info?.Kind ==
                        PickingKind.Spline &&
                    info.NextSplineId ==
                        -1;

                ExportSplineXButton.IsEnabled =
                    info?.Kind ==
                    PickingKind.Spline;

                LevelSplineToTerrainButton.IsEnabled =
                    info is
                    {
                        Kind:
                            PickingKind.Spline,
                        Length:
                            > 0.001
                    };

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

                    InspectorTransformCard.Visibility =
                        Visibility.Collapsed;

                    InspectorSelectionActionsCard.Visibility =
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

                InspectorTransformCard.Visibility =
                    Visibility.Visible;

                InspectorSelectionActionsCard.Visibility =
                    Visibility.Visible;

                InspectorContextBadgeText.Text =
                    isObject
                        ? "OBJETO"
                        : "RUA";

                InspectorContextSubtitleText.Text =
                    isObject
                        ? "Transformação, modelo e ações do objeto selecionado"
                        : "Geometria, inclinação, vínculos e ações da via selecionada";

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
        MapToolPaletteTranslate.X =
            _assetLibraryState
                .ToolPaletteOffsetX;

        MapToolPaletteTranslate.Y =
            _assetLibraryState
                .ToolPaletteOffsetY;

        SetActiveMapTool(
            ToolSelectionButton);

        UpdateExplorerModeVisual(
            SceneExplorerModeButton,
            "Cena e conteúdo do mapa");

        Activated +=
            OnMainWindowActivatedInitializeWorkspace;
    }

    private void UpdateExplorerModeVisual(
        Button activeButton,
        string subtitle)
    {
        var libraryWorkspace =
            ReferenceEquals(
                activeButton,
                LibraryExplorerModeButton);

        SetLibraryWorkspaceChrome(
            libraryWorkspace);

        ExplorerModeSubtitleText.Text =
            subtitle;

        var defaultBackground =
            MainRoot.Resources[
                "ExplorerModeDefaultBackgroundBrush"] as
                    Microsoft.UI.Xaml.Media.Brush;

        var defaultBorder =
            MainRoot.Resources[
                "ExplorerModeDefaultBorderBrush"] as
                    Microsoft.UI.Xaml.Media.Brush;

        var activeBackground =
            MainRoot.Resources[
                "ExplorerModeActiveBackgroundBrush"] as
                    Microsoft.UI.Xaml.Media.Brush;

        var activeBorder =
            MainRoot.Resources[
                "ExplorerModeActiveBorderBrush"] as
                    Microsoft.UI.Xaml.Media.Brush;

        foreach (
            var button in
                new[]
                {
                    SceneExplorerModeButton,
                    LibraryExplorerModeButton,
                    MapExplorerModeButton,
                    TransportExplorerModeButton
                })
        {
            button.Background =
                ReferenceEquals(
                    button,
                    activeButton)
                    ? activeBackground
                    : defaultBackground;

            button.BorderBrush =
                ReferenceEquals(
                    button,
                    activeButton)
                    ? activeBorder
                    : defaultBorder;

            button.BorderThickness =
                new Thickness(
                    ReferenceEquals(
                        button,
                        activeButton)
                        ? 1.5
                        : 1);
        }

        _mapMode =
            ReferenceEquals(
                activeButton,
                MapExplorerModeButton);

        if (!_mapMode)
        {
            _mapExplorerRefreshCancellation
                ?.Cancel();
        }

        MapExplorerPanel.Visibility =
            _mapMode
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void SetLibraryWorkspaceChrome(
        bool libraryWorkspace)
    {
        ExplorerPanelTitleText.Text =
            libraryWorkspace
                ? "BIBLIOTECA"
                : "PROJETO";

        ExplorerModeGrid.Visibility =
            libraryWorkspace
                ? Visibility.Collapsed
                : Visibility.Visible;

        ExplorerSearchGrid.Visibility =
            libraryWorkspace
                ? Visibility.Collapsed
                : Visibility.Visible;

        ProjectSummaryCard.Visibility =
            libraryWorkspace
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void OnAssetLibrarySearchTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!_libraryMode)
        {
            return;
        }

        _libraryFilterDebounceTimer.Stop();
        _libraryFilterDebounceTimer.Start();
    }

    private void OnCloseAssetLibraryModeClick(
        object sender,
        RoutedEventArgs e)
    {
        OnSceneExplorerModeClick(
            sender,
            e);
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
        else if (_mapMode)
        {
            RefreshMapExplorer();
        }
        else if (_libraryMode)
        {
            _libraryFilterDebounceTimer.Stop();
            _libraryFilterDebounceTimer.Start();
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
        LibraryThumbnailImage.Source =
            null;

        LibraryThumbnailBorder.Visibility =
            Visibility.Collapsed;

        _assetPreviewCancellation
            ?.Cancel();

        _assetPreviewCancellation
            ?.Dispose();

        _assetPreviewCancellation =
            null;

        SetTransportTrackRecordMode(
            false);

        Viewport.CancelSceneryPlacement();
        Viewport.CancelSplinePlacement();
        Viewport.ClearTimetableRoutePreview();
        Viewport.RestoreSceneView();

        PlaceAssetButton.Content =
            "Posicionar no mapa";

        SplinePlacementOptionsPanel.Visibility =
            Visibility.Collapsed;

        _libraryFilterDebounceTimer.Stop();

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

        UpdateExplorerModeVisual(
            SceneExplorerModeButton,
            "Cena e conteúdo do mapa");

        RefreshExplorerFilter();
    }

    private void OnMapExplorerModeClick(
        object sender,
        RoutedEventArgs e)
    {
        _assetPreviewCancellation
            ?.Cancel();

        _libraryFilterDebounceTimer.Stop();
        _trafficPreviewTimer.Stop();

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

        TrafficControlPanel.Visibility =
            Visibility.Collapsed;

        ExplorerListView.Visibility =
            Visibility.Collapsed;

        AssetLibraryPanel.Visibility =
            Visibility.Collapsed;

        TransportPanel.Visibility =
            Visibility.Collapsed;

        ExplorerSearchBox.PlaceholderText =
            "Buscar tile por coordenada...";

        UpdateExplorerModeVisual(
            MapExplorerModeButton,
            "Tiles, coordenadas e estrutura do mapa");

        RefreshMapExplorer();

        StatusText.Text =
            _session.CurrentMap is null
                ? "Mapa: abra ou crie um mapa para visualizar os tiles."
                : "Mapa: tiles exibidos diretamente no painel Projeto.";
    }

    private void RefreshMapExplorer()
    {
        _mapExplorerRefreshCancellation
            ?.Cancel();

        _mapExplorerRefreshCancellation
            ?.Dispose();

        var cancellation =
            new CancellationTokenSource();

        _mapExplorerRefreshCancellation =
            cancellation;

        var snapshot =
            _session.CurrentMap;

        if (snapshot is null)
        {
            MapTileListView.ItemsSource =
                Array.Empty<TileManagerViewItem>();

            MapExplorerStatusText.Text =
                "Abra ou crie um mapa para visualizar os tiles.";

            return;
        }

        var query =
            ExplorerSearchBox.Text
                .Trim();

        var mapTiles =
            snapshot.Map.Tiles
                .Select(
                    tile =>
                        (
                            X: tile.X,
                            Y: tile.Y
                        ))
                .ToArray();

        var loadedTiles =
            snapshot.Tiles
                .Select(
                    tile =>
                        (
                            X: tile.Reference.X,
                            Y: tile.Reference.Y,
                            Objects:
                                tile.Content.Objects.Count,
                            Splines:
                                tile.Content.Splines.Count
                        ))
                .ToArray();

        var activeX =
            snapshot.ActiveTile?.X;

        var activeY =
            snapshot.ActiveTile?.Y;

        MapExplorerStatusText.Text =
            $"Lendo {mapTiles.Length} tile(s)...";

        _ =
            RefreshMapExplorerAsync(
                mapTiles,
                loadedTiles,
                activeX,
                activeY,
                query,
                cancellation.Token);
    }

    private async Task RefreshMapExplorerAsync(
        (int X, int Y)[] mapTiles,
        (
            int X,
            int Y,
            int Objects,
            int Splines
        )[] loadedTiles,
        int? activeX,
        int? activeY,
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            var items =
                await Task.Run(
                    () =>
                    {
                        cancellationToken
                            .ThrowIfCancellationRequested();

                        var loadedCoordinates =
                            loadedTiles
                                .GroupBy(
                                    tile =>
                                        (
                                            tile.X,
                                            tile.Y
                                        ))
                                .ToDictionary(
                                    group =>
                                        group.Key,
                                    group =>
                                        group.First());

                        IEnumerable<
                            IGrouping<
                                (int X, int Y),
                                (int X, int Y)>> groups =
                                    mapTiles
                                        .GroupBy(
                                            tile =>
                                                (
                                                    tile.X,
                                                    tile.Y
                                                ));

                        if (
                            !string.IsNullOrWhiteSpace(
                                query))
                        {
                            groups =
                                groups.Where(
                                    group =>
                                        $"{group.Key.X},{group.Key.Y}"
                                            .Contains(
                                                query,
                                                StringComparison.OrdinalIgnoreCase) ||
                                        group.Key.X
                                            .ToString(
                                                CultureInfo.InvariantCulture)
                                            .Contains(
                                                query,
                                                StringComparison.OrdinalIgnoreCase) ||
                                        group.Key.Y
                                            .ToString(
                                                CultureInfo.InvariantCulture)
                                            .Contains(
                                                query,
                                                StringComparison.OrdinalIgnoreCase));
                        }

                        var result =
                            groups
                                .OrderBy(
                                    group =>
                                        group.Key.Y)
                                .ThenBy(
                                    group =>
                                        group.Key.X)
                                .Select(
                                    group =>
                                    {
                                        cancellationToken
                                            .ThrowIfCancellationRequested();

                                        var isActive =
                                            activeX ==
                                                group.Key.X &&
                                            activeY ==
                                                group.Key.Y;

                                        var isLoaded =
                                            loadedCoordinates
                                                .TryGetValue(
                                                    group.Key,
                                                    out var loaded);

                                        var detail =
                                            isLoaded
                                                ? $"objetos {loaded.Objects} · splines {loaded.Splines}"
                                                : "fora da região carregada";

                                        var referenceCount =
                                            group.Count();

                                        if (
                                            referenceCount >
                                                1)
                                        {
                                            detail +=
                                                $" · {referenceCount} refs";
                                        }

                                        return new TileManagerViewItem(
                                            group.Key.X,
                                            group.Key.Y,
                                            $"{(isActive ? "●" : "○")} Tile {group.Key.X},{group.Key.Y} · {detail}",
                                            isActive,
                                            isLoaded);
                                    })
                                .ToArray();

                        cancellationToken
                            .ThrowIfCancellationRequested();

                        return result;
                    },
                    cancellationToken);

            if (
                cancellationToken.IsCancellationRequested ||
                !_mapMode)
            {
                return;
            }

            MapTileListView.ItemsSource =
                items;

            MapTileListView.SelectedItem =
                items.FirstOrDefault(
                    item =>
                        item.IsActive);

            MapExplorerStatusText.Text =
                $"{mapTiles.Length} referência(s) de tile · {loadedTiles.Length} carregado(s) no viewport · {items.Length} coordenada(s) exibida(s).";
        }
        catch (OperationCanceledException)
        {
            // Uma nova atualização substituiu esta sem bloquear a UI.
        }
        catch (Exception exception)
        {
            NativeStartupDiagnostics.Write(
                $"Map explorer refresh failure type={exception.GetType().FullName} hresult=0x{exception.HResult:X8} message={exception.Message}");

            NativeStartupDiagnostics.Write(
                exception.ToString());

            if (
                _mapMode &&
                !cancellationToken
                    .IsCancellationRequested)
            {
                MapExplorerStatusText.Text =
                    $"Falha ao listar tiles: {exception.Message}";
            }
        }
    }

    private async void OnMapTileFocusClick(
        object sender,
        RoutedEventArgs e)
    {
        await FocusSelectedMapTileAsync();
    }

    private async void OnMapTileListDoubleTapped(
        object sender,
        DoubleTappedRoutedEventArgs e)
    {
        await FocusSelectedMapTileAsync();
    }

    private async Task FocusSelectedMapTileAsync()
    {
        if (
            MapTileListView.SelectedItem is not
                TileManagerViewItem selected)
        {
            StatusText.Text =
                "Mapa: selecione um tile primeiro.";

            return;
        }

        await NavigateToTileAsync(
            selected.X,
            selected.Y);

        RefreshMapExplorer();
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

        UpdateExplorerModeVisual(
            LibraryExplorerModeButton,
            "Assets, categorias, IA, favoritos e coleções");

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
            BeginLoading(
                "Atualizando biblioteca",
                _session.IsStandaloneWorkspace
                    ? "Indexando assets do Workspace..."
                    : "Indexando assets da instalação OMSI...");

            RefreshLibraryButton.IsEnabled =
                false;

            LibraryStatusText.Text =
                _session.IsStandaloneWorkspace
                    ? "Indexando Workspace Map Studio..."
                    : "Indexando instalação OMSI...";

            var progress =
                new Progress<
                    OmsiAssetIndexProgress>(
                    value =>
                    {
                        var detail =
                            $"Indexando... {value.ExaminedFiles} arquivos · " +
                            $"{value.CandidateFiles} assets";

                        LibraryStatusText.Text =
                            detail;

                        UpdateLoading(
                            "Atualizando biblioteca",
                            detail);
                    });

            var result =
                await _session
                    .RefreshAssetLibraryAsync(
                        progress);

            LibraryStatusText.Text =
                $"Índice atualizado: {result.TotalEntries} assets · " +
                $"+{result.AddedFiles} · ~{result.UpdatedFiles} · " +
                $"-{result.RemovedFiles}";

            await LoadAssetLibraryAsync(
                forceReload: true);
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

            EndLoading();
        }
    }

    private async void OnLibraryKindSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (
            !_libraryMode ||
            _syncingLibraryFilters ||
            _session.OmsiRootPath is
                not { } root)
        {
            return;
        }

        if (
            _assetLibraryAllItems.Count >
                0 &&
            string.Equals(
                _assetLibraryCacheRootPath,
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            ApplyLibraryKindFilter();
            return;
        }

        await LoadAssetLibraryAsync();
    }

    private void OnLibraryGroupSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_libraryMode && !_syncingLibraryFilters)
        {
            RefreshLibrarySubcategoryOptions();
            RefreshLibraryFilter();
        }
    }

    private void OnLibrarySubcategorySelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_libraryMode && !_syncingLibraryFilters)
        {
            RefreshLibraryFilter();
        }
    }

    private void OnLibraryTechnicalFilterSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_libraryMode && !_syncingLibraryFilters)
        {
            RefreshLibraryFilter();
        }
    }

    private void OnLibraryViewSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_libraryMode && !_syncingLibraryFilters)
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

            LibraryCategoryTreeView.IsEnabled =
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
        if (
            !_libraryMode ||
            _syncingLibraryFilters)
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
            await dialog.ShowAdaptiveAsync() !=
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
            await dialog.ShowAdaptiveAsync() !=
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


    private void OnAssetPlacementOptionsToggleClick(
        object sender,
        RoutedEventArgs e)
    {
        _assetPlacementOptionsExpanded =
            !_assetPlacementOptionsExpanded;

        AssetPlacementOptionsToggleButton.Content =
            _assetPlacementOptionsExpanded
                ? "Opções avançadas ▴"
                : "Opções avançadas ▾";

        UpdateAssetPlacementOptionsVisibility(
            GetSelectedAssetLibraryEntry()
                ?.Kind);
    }

    private void UpdateAssetPlacementOptionsVisibility(
        OmsiAssetKind? kind)
    {
        ObjectPlacementOptionsPanel.Visibility =
            _assetPlacementOptionsExpanded &&
            kind ==
                OmsiAssetKind.SceneryObject
                ? Visibility.Visible
                : Visibility.Collapsed;

        SplinePlacementOptionsPanel.Visibility =
            _assetPlacementOptionsExpanded &&
            kind ==
                OmsiAssetKind.Spline
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private async void OnAssetLibrarySelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var selected =
            GetSelectedAssetLibraryEntry();

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

        AiClassifyAssetButton.IsEnabled =
            placeable;

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

        AssetPlacementOptionsToggleButton.IsEnabled =
            placeable;

        UpdateAssetPlacementOptionsVisibility(
            selected?.Kind);

        _patternLineStart =
            null;

        if (
            !_libraryMode ||
            _session.OmsiRootPath is null ||
            GetSelectedAssetLibraryEntry() is not
                { } asset)
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

            if (
                result.IsRenderable &&
                result.ThumbnailBmp is
                    { Length: > 54 })
            {
                var thumbnailPath =
                    await SaveAssetThumbnailAsync(
                        asset,
                        result.ThumbnailBmp);

                LibraryThumbnailImage.Source =
                    new Microsoft.UI.Xaml.Media.Imaging
                        .BitmapImage(
                            new Uri(
                                thumbnailPath));

                LibraryThumbnailBorder.Visibility =
                    Visibility.Visible;
            }
            else
            {
                LibraryThumbnailImage.Source =
                    null;

                LibraryThumbnailBorder.Visibility =
                    Visibility.Collapsed;
            }

            StatusText.Text =
                result.IsRenderable
                    ? asset.Kind ==
                        OmsiAssetKind.Texture
                        ? $"Prévia de textura: {asset.RelativePath} · thumbnail atualizado."
                        : $"Prévia 3D nativa: {asset.RelativePath} · {result.TriangleCount} triângulos · thumbnail geométrico atualizado."
                    : asset.Kind ==
                        OmsiAssetKind.Texture
                        ? $"Não foi possível decodificar a textura: {asset.RelativePath} · {result.ErrorCode}."
                        : asset.Kind ==
                            OmsiAssetKind.Model
                            ? $"Não foi possível renderizar o modelo: {asset.RelativePath} · {result.ErrorCode}."
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
                    PlacementAlignRoadCheckBox.IsChecked =
                        true;
                    break;

                case 2:
                    ObjectPlacementModeComboBox.SelectedIndex =
                        2;
                    PlacementSpacingBox.Value =
                        25;
                    PlacementRandomRotationCheckBox.IsChecked =
                        false;
                    PlacementAlignRoadCheckBox.IsChecked =
                        true;
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
                    PlacementAlignRoadCheckBox.IsChecked =
                        true;
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
                    PlacementAlignRoadCheckBox.IsChecked =
                        false;
                    break;

                case 5:
                    ObjectPlacementModeComboBox.SelectedIndex =
                        4;
                    PlacementRowsBox.Value =
                        5;
                    PlacementColumnsBox.Value =
                        8;
                    PlacementAlignRoadCheckBox.IsChecked =
                        false;
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
            GetSelectedAssetLibraryEntry() is not
                { } replacement ||
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
            await confirm.ShowAdaptiveAsync() !=
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

    private async void OnAiClassifyAssetClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .AiAssistance,
                "Classificação de assets por IA"))
        {
            return;
        }

        if (
            GetSelectedAssetLibraryEntry() is not
                { } asset ||
            asset.Kind is not
                (
                    OmsiAssetKind.SceneryObject or
                    OmsiAssetKind.Spline
                ))
        {
            StatusText.Text =
                "IA: selecione um objeto SCO ou spline SLI.";
            return;
        }

        var activeProfile =
            _aiConnectionSettings
                .GetActiveProfile();

        if (activeProfile is null)
        {
            StatusText.Text =
                "IA: configure e ative um perfil em IA → Configurar provedores.";
            return;
        }

        try
        {
            AiClassifyAssetButton.IsEnabled =
                false;

            StatusText.Text =
                $"IA: classificando {asset.RelativePath} com {activeProfile.DisplayName}...";

            var provider =
                NativeAiProviderFactory
                    .Create(
                        activeProfile);

            if (
                provider is not
                    IMapStudioAssetClassificationProvider
                    classifier)
            {
                StatusText.Text =
                    $"IA: o adapter {activeProfile.AdapterId} ainda não oferece classificação de assets. Use o adapter OpenAI nesta build.";
                return;
            }

            var heuristicGroup =
                OmsiAssetLibraryClassifier
                    .Classify(
                        asset);

            var heuristicSubcategory =
                OmsiAssetLibraryClassifier
                    .GetSubcategory(
                        asset);

            var result =
                await classifier
                    .AnalyzeAssetClassificationAsync(
                        new MapStudioAssetClassificationRequest(
                            asset.RelativePath,
                            asset.Kind,
                            heuristicGroup,
                            heuristicSubcategory));

            if (result.Confidence < 0.45)
            {
                StatusText.Text =
                    $"IA: sugestão ignorada por baixa confiança ({result.Confidence:P0}). Classificação local mantida.";
                return;
            }

            _assetLibraryState
                .AiClassifications[
                    asset.RelativePath] =
                new NativeAssetAiClassification(
                    result.Group,
                    result.Subcategory,
                    result.Confidence,
                    activeProfile.DisplayName,
                    activeProfile.Model,
                    DateTimeOffset.UtcNow);

            SaveAssetLibraryState();

            _assetLibraryViewItemCache
                .Clear();

            RefreshLibraryGroupOptions(
                GetSelectedLibraryKind());

            RefreshLibraryFilter();

            StatusText.Text =
                $"IA: {asset.RelativePath} → {OmsiAssetLibraryClassifier.GetDisplayName(result.Group)} / {result.Subcategory} · confiança {result.Confidence:P0}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"IA: falha ao classificar asset: {exception.Message}";
        }
        finally
        {
            AiClassifyAssetButton.IsEnabled =
                GetSelectedAssetLibraryEntry()
                    ?.Kind is
                    OmsiAssetKind.SceneryObject or
                    OmsiAssetKind.Spline;
        }
    }

    private void OnFavoriteAssetClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            GetSelectedAssetLibraryEntry() is not
                { } asset)
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
            GetSelectedAssetLibraryEntry() is not
                { } asset)
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
            GetSelectedAssetLibraryEntry() is not
                { } asset)
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
                GetSelectedAssetLibraryEntry() is
                    { } selectedAsset &&
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

        if (_session.OmsiRootPath is null)
        {
            StatusText.Text =
                "Construção: nenhum Workspace/OMSI ativo. Abra um mapa antes de posicionar assets.";
            return;
        }

        if (_session.CurrentMap is null)
        {
            StatusText.Text =
                "Construção: nenhum mapa está aberto.";
            return;
        }

        if (
            GetSelectedAssetLibraryEntry() is not
                { } asset)
        {
            StatusText.Text =
                "Construção: selecione primeiro um asset na Biblioteca.";
            return;
        }

        if (
            asset.Kind is not
                (
                    OmsiAssetKind.SceneryObject or
                    OmsiAssetKind.Spline
                ))
        {
            StatusText.Text =
                $"Construção: {asset.RelativePath} não é um SCO/SLI posicionável.";
            return;
        }

        _assetPreviewCancellation
            ?.Cancel();

        Viewport.RestoreSceneView();

        try
        {
            if (
                asset.Kind ==
                    OmsiAssetKind.SceneryObject)
            {
                Viewport
                    .SetSceneryRoadSnapOptions(
                        PlacementAlignRoadCheckBox
                            .IsChecked ==
                        true,
                        double.IsFinite(
                            PlacementRoadSnapDistanceBox
                                .Value)
                            ? PlacementRoadSnapDistanceBox
                                .Value
                            : 8.0);
            }

            if (
                asset.Kind ==
                    OmsiAssetKind.Spline)
            {
                var placeHeightSpline =
                    SplineHeightCheckBox
                        .IsChecked ==
                    true;

                Viewport
                    .SetSplinePlacementHeightMode(
                        placeHeightSpline);

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

                Viewport
                    .SetSplinePlacementElevationOffset(
                        double.IsFinite(
                            SplineElevationOffsetBox
                                .Value)
                            ? SplineElevationOffsetBox
                                .Value
                            : 0.0);

                Viewport
                    .SetSplineEasyRoadOptions(
                        !placeHeightSpline &&
                        SplineEasyRoadCheckBox
                            .IsChecked ==
                        true,
                        double.IsFinite(
                            SplineCurveOffsetBox
                                .Value)
                            ? SplineCurveOffsetBox
                                .Value
                            : 0.0);
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
                    asset.Kind ==
                        OmsiAssetKind.Spline
                        ? $"Não foi possível iniciar a construção da SLI: {asset.RelativePath}. Verifique se o arquivo existe e é uma spline válida."
                        : $"Não foi possível preparar o asset: {asset.RelativePath}.";

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
                    ? SplineHeightCheckBox.IsChecked ==
                        true
                        ? SplineCurveCheckBox.IsChecked ==
                            true
                            ? "[spline_h] curva: clique início, fim e ponto de curvatura."
                            : "[spline_h] altura: clique início e fim."
                        : SplineEasyRoadCheckBox.IsChecked ==
                        true
                        ? "Rua: clique no início, arraste a prévia e solte no fim." +
                          (
                              Math.Abs(
                                  SplineElevationOffsetBox.Value) >
                                  0.001
                                  ? $" · elevação {SplineElevationOffsetBox.Value:+0.0;-0.0;0.0} m"
                                  : string.Empty
                          )
                        : SplineCurveCheckBox.IsChecked ==
                            true
                            ? "Spline curva manual: clique início, fim e ponto de curvatura."
                            : "Rua reta: clique no início, arraste e solte no fim." +
                              (
                                  Math.Abs(
                                      SplineElevationOffsetBox.Value) >
                                      0.001
                                      ? $" · elevação {SplineElevationOffsetBox.Value:+0.0;-0.0;0.0} m"
                                      : string.Empty
                              )
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

            RegisterConstructionHistory(
                "Inserir spline");

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
                GetSelectedAssetLibraryEntry();

            var canContinue =
                !request.IsHeightSpline &&
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
                GetSelectedAssetLibraryEntry() is
                    { } asset &&
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
                        GetSelectedAssetLibraryEntry() is
                            { } selected &&
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
                GetSelectedAssetLibraryEntry() is
                    { } asset &&
                asset.Kind ==
                    OmsiAssetKind
                        .SceneryObject;

            StatusText.Text =
                requests.Count == 1
                    ? $"Objeto inserido em tile {request.Tile.X},{request.Tile.Y} com backup seguro."
                    : $"{requests.Count} objetos inseridos em lote com backup seguro.";

            RecordAssetUsage(
                request.SceneryObjectPath);

            RegisterConstructionHistory(
                requests.Count == 1
                    ? "Inserir objeto"
                    : $"Inserir {requests.Count} objetos");

            if (
                mode == 1 &&
                GetSelectedAssetLibraryEntry() is
                    { } repeatAsset &&
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
                GetSelectedAssetLibraryEntry() is
                    { } asset &&
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

    private void OnAssetLibraryDragItemsStarting(
        object sender,
        DragItemsStartingEventArgs e)
    {
        var asset =
            e.Items
                .Select(
                    item =>
                        item switch
                        {
                            AssetLibraryViewItem view =>
                                view.Asset,
                            OmsiAssetIndexEntry entry =>
                                entry,
                            _ =>
                                null
                        })
                .FirstOrDefault(
                    item =>
                        item is not null);

        if (
            asset is null ||
            asset.Kind is not
                (
                    OmsiAssetKind
                        .SceneryObject or
                    OmsiAssetKind
                        .Spline
                ) ||
            _session.CurrentMap is null)
        {
            e.Cancel =
                true;

            return;
        }

        var kind =
            asset.Kind ==
                OmsiAssetKind
                    .SceneryObject
                ? "object"
                : "spline";

        e.Data.SetText(
            kind +
            "\n" +
            asset.RelativePath);

        e.Data.RequestedOperation =
            DataPackageOperation.Copy;
    }

    private void OnViewportLibraryDragOver(
        object sender,
        DragEventArgs e)
    {
        if (
            _session.CurrentMap is null ||
            !e.DataView.Contains(
                StandardDataFormats.Text))
        {
            return;
        }

        e.AcceptedOperation =
            DataPackageOperation.Copy;

        e.DragUIOverride.Caption =
            "Posicionar no mapa";

        e.DragUIOverride
            .IsCaptionVisible =
            true;

        e.Handled =
            true;
    }

    private async void OnViewportLibraryDrop(
        object sender,
        DragEventArgs e)
    {
        if (
            _session.CurrentMap is null ||
            _session.OmsiRootPath is null ||
            !e.DataView.Contains(
                StandardDataFormats.Text))
        {
            return;
        }

        var payload =
            await e.DataView
                .GetTextAsync();

        var separator =
            payload.IndexOf(
                '\n');

        if (
            separator <= 0 ||
            separator >=
                payload.Length - 1)
        {
            return;
        }

        var kind =
            payload[..separator]
                .Trim();

        var path =
            payload[(separator + 1)..]
                .Trim();

        var expectedKind =
            string.Equals(
                kind,
                "object",
                StringComparison.Ordinal)
                ? OmsiAssetKind
                    .SceneryObject
                : string.Equals(
                    kind,
                    "spline",
                    StringComparison.Ordinal)
                    ? OmsiAssetKind
                        .Spline
                    : (OmsiAssetKind?)
                        null;

        if (expectedKind is null)
        {
            return;
        }

        var asset =
            _assetLibraryItems
                .FirstOrDefault(
                    candidate =>
                        candidate.Kind ==
                            expectedKind.Value &&
                        string.Equals(
                            candidate
                                .RelativePath,
                            path,
                            StringComparison
                                .OrdinalIgnoreCase));

        if (asset is null)
        {
            StatusText.Text =
                "O asset arrastado não está mais disponível na biblioteca.";

            return;
        }

        var position =
            e.GetPosition(
                Viewport);

        e.AcceptedOperation =
            DataPackageOperation.Copy;

        e.Handled =
            true;

        await HandleLibraryAssetDropAsync(
            asset,
            position.X,
            position.Y);
    }

    private async Task HandleLibraryAssetDropAsync(
        OmsiAssetIndexEntry asset,
        double x,
        double y)
    {
        var root =
            _session.OmsiRootPath;

        if (
            root is null ||
            _session.CurrentMap is null)
        {
            return;
        }

        _assetPreviewCancellation
            ?.Cancel();

        Viewport.RestoreSceneView();
        Viewport.CancelSceneryPlacement();
        Viewport.CancelSplinePlacement();

        if (
            asset.Kind ==
            OmsiAssetKind
                .SceneryObject)
        {
            ObjectPlacementModeComboBox
                .SelectedIndex =
                0;

            var started =
                await Viewport
                    .BeginSceneryPlacementAsync(
                        root,
                        asset);

            if (
                !started ||
                !Viewport
                    .TryFinishSceneryPlacementAtPoint(
                        x,
                        y,
                        out var request) ||
                request is null)
            {
                Viewport.CancelSceneryPlacement();

                StatusText.Text =
                    "Não foi possível posicionar o objeto no ponto solto.";

                return;
            }

            await HandleSceneryPlacementAsync(
                request);

            StatusText.Text +=
                " · inserido por arrastar/soltar.";

            return;
        }

        if (
            asset.Kind !=
            OmsiAssetKind.Spline)
        {
            return;
        }

        SplineHeightCheckBox.IsChecked =
            false;

        SplineEasyRoadCheckBox.IsChecked =
            false;

        SplineCurveCheckBox.IsChecked =
            false;

        Viewport
            .SetSplinePlacementHeightMode(
                false);

        Viewport
            .SetSplineEasyRoadOptions(
                enabled: false,
                curveOffset: 0);

        var splineStarted =
            await Viewport
                .BeginSplinePlacementAsync(
                    root,
                    asset,
                    curved: false);

        if (
            !splineStarted ||
            !Viewport
                .TrySeedSplinePlacementAtPoint(
                    x,
                    y,
                    out var status))
        {
            Viewport.CancelSplinePlacement();

            StatusText.Text =
                "Não foi possível iniciar a spline no ponto solto.";

            return;
        }

        PlaceAssetButton.Content =
            "Cancelar posicionamento";

        StatusText.Text =
            status +
            " · início definido por arrastar/soltar.";
    }

    private void OnAssetLibraryDoubleTapped(
        object sender,
        DoubleTappedRoutedEventArgs e)
    {
        if (
            GetSelectedAssetLibraryEntry() is not
                { } asset)
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
                GetSelectedAssetLibraryEntry() is
                    { } selected &&
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

        LevelSplineToTerrainButton.IsEnabled =
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

        InspectorTransformCard.Visibility =
            Visibility.Collapsed;

        InspectorSelectionActionsCard.Visibility =
            Visibility.Collapsed;

        InspectorContextBadgeText.Text =
            "GERAL";

        InspectorContextSubtitleText.Text =
            "Selecione um item ou escolha uma ferramenta";
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

    private string GetAssetThumbnailPath(
        OmsiAssetIndexEntry asset)
    {
        var root =
            _session.OmsiRootPath ??
            string.Empty;

        var keySource =
            root +
            "|" +
            asset.Kind +
            "|" +
            asset.RelativePath +
            "|" +
            asset.Size +
            "|" +
            asset.LastWriteUtcTicks;

        var hash =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8
                        .GetBytes(
                            keySource)))
                .ToLowerInvariant();

        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .LocalApplicationData),
            "OMSI Map Studio",
            "Cache",
            "Thumbnails",
            hash +
            ".bmp");
    }

    private OmsiAssetLibraryGroup
        GetEffectiveAssetGroup(
            OmsiAssetIndexEntry asset)
    {
        if (
            _assetLibraryState
                .AiClassifications
                .TryGetValue(
                    asset.RelativePath,
                    out var classification) &&
            classification.Confidence >=
                0.45)
        {
            return classification.Group;
        }

        return OmsiAssetLibraryClassifier
            .Classify(
                asset);
    }

    private string GetEffectiveAssetSubcategory(
        OmsiAssetIndexEntry asset)
    {
        if (
            _assetLibraryState
                .AiClassifications
                .TryGetValue(
                    asset.RelativePath,
                    out var classification) &&
            classification.Confidence >=
                0.45 &&
            !string.IsNullOrWhiteSpace(
                classification.Subcategory))
        {
            return classification.Subcategory;
        }

        return OmsiAssetLibraryClassifier
            .GetSubcategory(
                asset);
    }

    private AssetLibraryViewItem
        CreateAssetLibraryViewItem(
            OmsiAssetIndexEntry asset)
    {
        var cacheKey =
            (_session.OmsiRootPath ??
                string.Empty) +
            "|" +
            asset.Kind +
            "|" +
            asset.RelativePath +
            "|" +
            asset.Size +
            "|" +
            asset.LastWriteUtcTicks;

        if (
            _assetLibraryViewItemCache
                .TryGetValue(
                    cacheKey,
                    out var cached))
        {
            return cached;
        }

        var group =
            GetEffectiveAssetGroup(
                    asset);

        var groupName =
            OmsiAssetLibraryClassifier
                .GetDisplayName(
                    group);

        var subcategory =
            GetEffectiveAssetSubcategory(
                    asset);

        var detail =
            string.IsNullOrWhiteSpace(
                subcategory)
                ? groupName
                : groupName +
                  " · " +
                  subcategory;

        var kindLabel =
            asset.Kind switch
            {
                OmsiAssetKind.SceneryObject =>
                    "SCO",
                OmsiAssetKind.Spline =>
                    "SLI",
                OmsiAssetKind.Model =>
                    "3D",
                OmsiAssetKind.Texture =>
                    "TEX",
                _ =>
                    asset.Kind.ToString()
            };

        var item =
            new AssetLibraryViewItem(
                asset,
                Path.GetFileName(
                    asset.RelativePath),
                detail,
                kindLabel,
                null);

        _assetLibraryViewItemCache[
            cacheKey] =
            item;

        return item;
    }

    private void OnAssetLibraryContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (
            args.InRecycleQueue ||
            args.Item is not
                AssetLibraryViewItem item ||
            item.ThumbnailSource is not null)
        {
            return;
        }

        var thumbnailPath =
            GetAssetThumbnailPath(
                item.Asset);

        if (
            File.Exists(
                thumbnailPath))
        {
            item.SetThumbnailPath(
                thumbnailPath);
        }
    }

    private OmsiAssetIndexEntry?
        GetSelectedAssetLibraryEntry() =>
        AssetLibraryListView
            .SelectedItem switch
        {
            AssetLibraryViewItem view =>
                view.Asset,
            OmsiAssetIndexEntry asset =>
                asset,
            _ =>
                null
        };

    private async Task<string>
        SaveAssetThumbnailAsync(
            OmsiAssetIndexEntry asset,
            byte[] bytes)
    {
        var path =
            GetAssetThumbnailPath(
                asset);

        var directory =
            Path.GetDirectoryName(
                path) ??
            throw new InvalidOperationException(
                "thumbnailCacheDirectoryUnavailable");

        Directory.CreateDirectory(
            directory);

        await File.WriteAllBytesAsync(
            path,
            bytes);

        var visibleItem =
            AssetLibraryListView
                .Items
                .OfType<
                    AssetLibraryViewItem>()
                .FirstOrDefault(
                    item =>
                        string.Equals(
                            item.Asset
                                .RelativePath,
                            asset.RelativePath,
                            StringComparison
                                .OrdinalIgnoreCase));

        visibleItem
            ?.SetThumbnailPath(
                path);

        var files =
            new DirectoryInfo(
                directory)
                .EnumerateFiles(
                    "*.bmp")
                .OrderByDescending(
                    file =>
                        file.LastWriteTimeUtc)
                .ToArray();

        foreach (
            var stale in
                files.Skip(64))
        {
            try
            {
                stale.Delete();
            }
            catch
            {
                // Thumbnail cleanup must never block editing.
            }
        }

        return path;
    }

    private async Task LoadAssetLibraryAsync(
        bool forceReload = false)
    {
        var root =
            _session.OmsiRootPath;

        if (root is null)
        {
            _assetLibraryItems =
                Array.Empty<
                    OmsiAssetIndexEntry>();

            _assetLibraryAllItems =
                Array.Empty<
                    OmsiAssetIndexEntry>();

            _assetLibraryCacheRootPath =
                null;

            _assetLibraryViewItemCache
                .Clear();

            AssetLibraryListView.ItemsSource =
                Array.Empty<
                    AssetLibraryViewItem>();

            RefreshLibraryGroupOptions(
                null);

            return;
        }

        try
        {
            var rootChanged =
                !string.Equals(
                    _assetLibraryCacheRootPath,
                    root,
                    StringComparison.OrdinalIgnoreCase);

            if (
                forceReload ||
                rootChanged ||
                _assetLibraryAllItems.Count ==
                    0)
            {
                _assetLibraryAllItems =
                    await _session
                        .GetAssetLibraryAsync(
                            null);

                _assetLibraryCacheRootPath =
                    root;

                _assetLibraryViewItemCache
                    .Clear();
            }

            ApplyLibraryKindFilter();

            var total =
                _assetLibraryAllItems.Count;

            var scenery =
                _assetLibraryAllItems.Count(
                    item =>
                        item.Kind ==
                        OmsiAssetKind.SceneryObject);

            var splines =
                _assetLibraryAllItems.Count(
                    item =>
                        item.Kind ==
                        OmsiAssetKind.Spline);

            var models =
                _assetLibraryAllItems.Count(
                    item =>
                        item.Kind ==
                        OmsiAssetKind.Model);

            var textures =
                _assetLibraryAllItems.Count(
                    item =>
                        item.Kind ==
                        OmsiAssetKind.Texture);

            if (total == 0)
            {
                LibraryStatusText.Text =
                    "Índice vazio. Clique em Atualizar para catalogar a instalação.";
            }
            else
            {
                LibraryStatusText.Text =
                    $"{total} assets em cache · " +
                    $"{scenery} SCO · {splines} SLI · " +
                    $"{models} modelos · {textures} texturas";
            }
        }
        catch (Exception exception)
        {
            LibraryStatusText.Text =
                $"Falha ao abrir biblioteca: {exception.Message}";
        }
    }

    private void ApplyLibraryKindFilter()
    {
        var kind =
            GetSelectedLibraryKind();

        _assetLibraryItems =
            kind is null
                ? _assetLibraryAllItems
                : _assetLibraryAllItems
                    .Where(
                        item =>
                            item.Kind ==
                                kind.Value)
                    .ToArray();

        _syncingLibraryFilters =
            true;

        try
        {
            RefreshLibraryGroupOptions(
                kind);

            RefreshLibraryCollectionOptions();
        }
        finally
        {
            _syncingLibraryFilters =
                false;
        }

        RefreshLibraryFilter();
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
        RefreshLibraryCategoryTree();
        RefreshLibraryTechnicalFilterOptions(
            kind);
    }

    private void RefreshLibraryCategoryTree()
    {
        LibraryCategoryTreeView
            .RootNodes
            .Clear();

        var root =
            new TreeViewNode
            {
                Content =
                    new LibraryCategoryTreeItem(
                        OmsiAssetLibraryGroup.All,
                        null,
                        "Todas as categorias"),
                IsExpanded =
                    true
            };

        foreach (
            var option in
                _libraryGroupOptions
                    .Where(
                        item =>
                            item.Group !=
                                OmsiAssetLibraryGroup.All))
        {
            var groupNode =
                new TreeViewNode
                {
                    Content =
                        new LibraryCategoryTreeItem(
                            option.Group,
                            null,
                            option.Label)
                };

            var subcategories =
                _assetLibraryItems
                    .Where(
                        item =>
                            item.Kind is
                                OmsiAssetKind.SceneryObject or
                                OmsiAssetKind.Spline &&
                            GetEffectiveAssetGroup(
                                    item) ==
                                option.Group)
                    .Select(
                        GetEffectiveAssetSubcategory)
                    .Where(
                        value =>
                            !string.IsNullOrWhiteSpace(
                                value))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .OrderBy(
                        value => value,
                        StringComparer.CurrentCultureIgnoreCase);

            foreach (
                var subcategory in
                    subcategories)
            {
                groupNode.Children.Add(
                    new TreeViewNode
                    {
                        Content =
                            new LibraryCategoryTreeItem(
                                option.Group,
                                subcategory,
                                subcategory)
                    });
            }

            root.Children.Add(
                groupNode);
        }

        LibraryCategoryTreeView
            .RootNodes
            .Add(
                root);
    }

    private void OnLibraryCategoryTreeSelectionChanged(
        TreeView sender,
        TreeViewSelectionChangedEventArgs args)
    {
        if (
            !_libraryMode ||
            _syncingLibraryFilters ||
            sender.SelectedNode?.Content is not
                LibraryCategoryTreeItem selected)
        {
            return;
        }

        var groupIndex =
            _libraryGroupOptions
                .Select(
                    (item, index) =>
                        (
                            item,
                            index
                        ))
                .FirstOrDefault(
                    pair =>
                        pair.item.Group ==
                            selected.Group)
                .index;

        if (
            groupIndex >= 0 &&
            groupIndex <
                _libraryGroupOptions.Count &&
            LibraryGroupComboBox.SelectedIndex !=
                groupIndex)
        {
            LibraryGroupComboBox.SelectedIndex =
                groupIndex;
        }

        var subcategory =
            selected.Subcategory;

        if (
            string.IsNullOrWhiteSpace(
                subcategory))
        {
            LibrarySubcategoryComboBox.SelectedIndex =
                0;

            RefreshLibraryFilter();
            return;
        }

        var subcategoryIndex =
            -1;

        for (
            var index = 0;
            index <
                LibrarySubcategoryComboBox
                    .Items.Count;
            index++)
        {
            if (
                string.Equals(
                    LibrarySubcategoryComboBox
                        .Items[index]
                        ?.ToString(),
                    subcategory,
                    StringComparison.OrdinalIgnoreCase))
            {
                subcategoryIndex =
                    index;

                break;
            }
        }

        if (subcategoryIndex >= 0)
        {
            LibrarySubcategoryComboBox.SelectedIndex =
                subcategoryIndex;
        }

        RefreshLibraryFilter();
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
                        GetEffectiveAssetGroup(item) ==
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
                    GetEffectiveAssetSubcategory)
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
            AssetLibrarySearchBox.Text
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
                        GetEffectiveAssetGroup(
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
                            GetEffectiveAssetSubcategory(item),
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
            filtered
                .Select(
                    CreateAssetLibraryViewItem)
                .ToArray();

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

        RaiseTerrainButton.IsEnabled =
            false;

        LowerTerrainButton.IsEnabled =
            false;

        ApplyTerrainPaintButton.IsEnabled =
            false;

        TerrainPointText.Text =
            "Clique no terreno no viewport...";

        Viewport.BeginTerrainPointPick();

        StatusText.Text =
            "Ferramenta de terreno ativa: clique no ponto que deseja nivelar.";
    }

    private async void OnRaiseTerrainClick(
        object sender,
        RoutedEventArgs e) =>
        await ApplyTerrainOffsetAsync(
            1);

    private async void OnLowerTerrainClick(
        object sender,
        RoutedEventArgs e) =>
        await ApplyTerrainOffsetAsync(
            -1);

    private async Task ApplyTerrainOffsetAsync(
        double direction)
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

        var magnitude =
            TerrainBrushDeltaBox.Value;

        var radius =
            TerrainBrushRadiusBox.Value;

        var feather =
            TerrainBrushFeatherBox.Value;

        if (
            !double.IsFinite(
                magnitude) ||
            magnitude <= 0 ||
            !double.IsFinite(
                radius) ||
            !double.IsFinite(
                feather) ||
            radius <= 0 ||
            feather < 0 ||
            feather > 1)
        {
            StatusText.Text =
                "Valores do pincel incremental são inválidos.";

            return;
        }

        var delta =
            magnitude *
            (
                direction >= 0
                    ? 1
                    : -1
            );

        try
        {
            ApplyTerrainLevelButton.IsEnabled =
                false;

            RaiseTerrainButton.IsEnabled =
                false;

            LowerTerrainButton.IsEnabled =
                false;

            ApplyTerrainPaintButton.IsEnabled =
                false;

            StatusText.Text =
                delta > 0
                    ? $"Elevando terreno do tile {point.Tile.X},{point.Tile.Y} em {delta:F2} m..."
                    : $"Abaixando terreno do tile {point.Tile.X},{point.Tile.Y} em {Math.Abs(delta):F2} m...";

            var snapshot =
                await _session
                    .OffsetTerrainAsync(
                        point,
                        delta,
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

            RaiseTerrainButton.IsEnabled =
                false;

            LowerTerrainButton.IsEnabled =
                false;

            TerrainPointText.Text =
                delta > 0
                    ? "Terreno elevado. Escolha outro ponto para continuar."
                    : "Terreno abaixado. Escolha outro ponto para continuar.";

            StatusText.Text =
                $"{(delta > 0 ? "Terreno elevado" : "Terreno abaixado")} · incremento {Math.Abs(delta):F2} m · raio {radius:F1} m · feather {feather:F2}.";
        }
        catch (Exception exception)
        {
            var hasPoint =
                _terrainEditPoint is not
                    null;

            ApplyTerrainLevelButton.IsEnabled =
                hasPoint;

            RaiseTerrainButton.IsEnabled =
                hasPoint;

            LowerTerrainButton.IsEnabled =
                hasPoint;

            ApplyTerrainPaintButton.IsEnabled =
                hasPoint &&
                _session.CurrentMap?
                    .Map
                    .GroundTextures
                    .Count >
                1;

            StatusText.Text =
                $"Falha ao alterar terreno: {exception.Message}";
        }
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

            RaiseTerrainButton.IsEnabled =
                false;

            LowerTerrainButton.IsEnabled =
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

            RaiseTerrainButton.IsEnabled =
                false;

            LowerTerrainButton.IsEnabled =
                false;

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

            RaiseTerrainButton.IsEnabled =
                _terrainEditPoint is not null;

            LowerTerrainButton.IsEnabled =
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

            RaiseTerrainButton.IsEnabled =
                _terrainEditPoint is not null;

            LowerTerrainButton.IsEnabled =
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

    private void OnLevelSelectedSplineToTerrainClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo is null ||
            _selectionInfo.Kind !=
                PickingKind.Spline)
        {
            return;
        }

        if (
            Viewport
                .LevelSelectedSplineToTerrain(
                    out var startHeight,
                    out var endHeight))
        {
            StatusText.Text =
                $"Rua/spline nivelada ao terreno: {startHeight:F2} → {endHeight:F2} m. Salve as alterações para persistir.";
        }
        else
        {
            StatusText.Text =
                "Não foi possível nivelar: o início ou o fim da spline está fora do terreno carregado.";
        }
    }

    private async void OnRoadAutoConnectClick(
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
            StatusText.Text =
                "Auto conectar: selecione primeiro uma rua/spline.";
            return;
        }

        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            StatusText.Text =
                "Auto conectar: finalize ou cancele o posicionamento atual.";
            return;
        }

        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Auto conectar: salvando transformações pendentes...";

            await _session
                .SavePendingTransformsAsync();

            SaveChangesButton.IsEnabled =
                false;
        }

        var distance =
            double.IsFinite(
                SplineEndpointSnapDistanceBox
                    .Value)
                ? SplineEndpointSnapDistanceBox
                    .Value
                : 5.0;

        if (
            !Viewport
                .TryGetAutoConnectLinksForSelectedSpline(
                    distance,
                    out var desiredPrevious,
                    out var desiredNext,
                    out var detectionStatus))
        {
            StatusText.Text =
                detectionStatus;
            return;
        }

        if (
            desiredPrevious ==
                selection.PreviousSplineId &&
            desiredNext ==
                selection.NextSplineId)
        {
            StatusText.Text =
                detectionStatus;
            return;
        }

        try
        {
            StatusText.Text =
                $"Auto conectar: corrigindo vínculos da spline #{selection.EntityId}...";

            var snapshot =
                await _session
                    .UpdateSplineLinksAsync(
                        selection,
                        desiredPrevious,
                        desiredNext,
                        repairBrokenLinks:
                            true);

            RegisterConstructionHistory(
                "Auto conectar spline");

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

            StatusText.Text =
                $"Auto conectar concluído: spline #{selection.EntityId} · Previous {desiredPrevious} · Next {desiredNext}." +
                (
                    string.IsNullOrWhiteSpace(
                        _session.LastBackupDirectory)
                        ? string.Empty
                        : $" · backup {_session.LastBackupDirectory}"
                );
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Auto conectar falhou: {exception.Message}";
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

    private async void OnExportSplineXClick(
        object sender,
        RoutedEventArgs e)
    {
        var selection =
            _selectionInfo;

        var snapshot =
            _session.CurrentMap;

        if (
            selection is null ||
            selection.Kind !=
                PickingKind.Spline ||
            snapshot is null)
        {
            return;
        }

        var list =
            new ListView
            {
                Header =
                    "Splines carregadas",
                SelectionMode =
                    ListViewSelectionMode.Multiple,
                MaxHeight =
                    360,
                MinWidth =
                    420
            };

        var seen =
            new HashSet<int>();

        foreach (
            var tile in
                snapshot.Tiles)
        {
            foreach (
                var spline in
                    tile.Content.Splines
                        .OrderBy(
                            item =>
                                item.SplineId))
            {
                if (
                    !seen.Add(
                        spline.SplineId))
                {
                    continue;
                }

                var item =
                    new ListViewItem
                    {
                        Content =
                            $"#{spline.SplineId} · tile {tile.Reference.X},{tile.Reference.Y} · {spline.SplinePath}",
                        Tag =
                            spline.SplineId
                    };

                item.IsSelected =
                    spline.SplineId ==
                    selection.EntityId;

                list.Items.Add(
                    item);
            }
        }

        if (list.Items.Count == 0)
        {
            StatusText.Text =
                "Spline Export: não há splines carregadas.";
            return;
        }

        var note =
            new TextBlock
            {
                Text =
                    "Exporta a geometria real das SLI selecionadas para um único arquivo DirectX .x, usando como origem o início da primeira spline. UVs são preservadas; texturas não são incorporadas, mantendo o fluxo clássico de preparação no Blender.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    10
            };

        panel.Children.Add(
            list);
        panel.Children.Add(
            note);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Spline Export (.x)",
                Content =
                    panel,
                PrimaryButtonText =
                    "Exportar",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        var ids =
            list.SelectedItems
                .OfType<
                    ListViewItem>()
                .Select(
                    item =>
                        item.Tag)
                .OfType<int>()
                .Distinct()
                .ToArray();

        if (
            ids.Length == 0)
        {
            StatusText.Text =
                "Spline Export: selecione ao menos uma spline.";
            return;
        }

        if (
            !Viewport
                .TryBuildSplineXExport(
                    ids,
                    out var export,
                    out var exportStatus) ||
            export is null)
        {
            StatusText.Text =
                exportStatus;
            return;
        }

        var picker =
            new FileSavePicker
            {
                SuggestedFileName =
                    $"spline_export_{selection.EntityId}"
            };

        picker.FileTypeChoices.Add(
            "DirectX model (.x)",
            new List<string>
            {
                ".x"
            });

        InitializeWithWindow.Initialize(
            picker,
            _windowHandle);

        var file =
            await picker
                .PickSaveFileAsync();

        if (file is null)
        {
            StatusText.Text =
                "Spline Export cancelado.";
            return;
        }

        try
        {
            await File
                .WriteAllTextAsync(
                    file.Path,
                    export.Content,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier:
                            false));

            StatusText.Text =
                $"Spline Export concluído: {export.SplineCount} spline(s), {export.TriangleCount} triângulo(s) · {file.Path}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Spline Export falhou: {exception.Message}";
        }
    }

    private async void OnCompleteToSplineClick(
        object sender,
        RoutedEventArgs e)
    {
        var selection =
            _selectionInfo;

        var snapshot =
            _session.CurrentMap;

        if (
            selection is null ||
            selection.Kind !=
                PickingKind.Spline ||
            snapshot is null)
        {
            return;
        }

        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de usar Complete to.";

            return;
        }

        if (
            selection.NextSplineId >=
                0)
        {
            StatusText.Text =
                "Complete to: o fim da spline selecionada já possui vínculo Next.";

            return;
        }

        var candidates =
            snapshot.Tiles
                .SelectMany(
                    tile =>
                        tile.Content.Splines)
                .Where(
                    spline =>
                        spline.SplineId !=
                            selection.EntityId &&
                        spline.PreviousSplineId <
                            0 &&
                        !spline.IsHeightSpline &&
                        string.Equals(
                            spline.SplinePath,
                            selection.AssetPath,
                            StringComparison.OrdinalIgnoreCase))
                .OrderBy(
                    spline =>
                        spline.SplineId)
                .ToArray();

        if (candidates.Length == 0)
        {
            StatusText.Text =
                "Complete to: não há outra spline carregada com início livre e a mesma SLI.";

            return;
        }

        var targetComboBox =
            new ComboBox
            {
                Header =
                    "Destino (início livre)",
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                MinWidth =
                    320
            };

        foreach (
            var candidate in
                candidates)
        {
            targetComboBox.Items.Add(
                new ComboBoxItem
                {
                    Content =
                        $"#{candidate.SplineId} · {candidate.SplinePath}",
                    Tag =
                        candidate.SplineId
                });
        }

        targetComboBox.SelectedIndex =
            0;

        var maximumRadiusBox =
            new NumberBox
            {
                Header =
                    "Raio máximo (m)",
                Value =
                    200,
                Minimum =
                    1,
                Maximum =
                    2_000_000,
                SmallChange =
                    10,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Inline
            };

        var note =
            new TextBlock
            {
                Text =
                    "Liga o fim da spline selecionada ao início da spline destino. Para preservar os paths, esta versão exige a mesma SLI nos dois lados. O solver aceita reta ou uma única curva circular tangente; se não houver solução segura dentro do raio máximo, nada é gravado.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    10
            };

        panel.Children.Add(
            targetComboBox);
        panel.Children.Add(
            maximumRadiusBox);
        panel.Children.Add(
            note);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    $"Complete to · spline #{selection.EntityId}",
                Content =
                    panel,
                PrimaryButtonText =
                    "Calcular e conectar",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        if (
            targetComboBox.SelectedItem is not
                ComboBoxItem targetItem ||
            targetItem.Tag is not
                int targetSplineId ||
            !double.IsFinite(
                maximumRadiusBox.Value))
        {
            StatusText.Text =
                "Complete to: destino ou raio máximo inválido.";
            return;
        }

        if (
            !Viewport
                .TryBuildSplineCompleteToRequest(
                    selection.EntityId,
                    targetSplineId,
                    maximumRadiusBox.Value,
                    out var request,
                    out var solveStatus) ||
            request is null)
        {
            StatusText.Text =
                solveStatus;
            return;
        }

        try
        {
            CompleteToSplineButton.IsEnabled =
                false;

            StatusText.Text =
                solveStatus +
                " Gravando com backup...";

            var insertion =
                await _session
                    .InsertSplineAsync(
                        request);

            RegisterConstructionHistory(
                "Complete to");

            if (_session.OmsiRootPath is null)
            {
                throw new InvalidOperationException(
                    "Instalação OMSI não selecionada.");
            }

            await Viewport
                .SetMapSnapshotAsync(
                    insertion.Snapshot,
                    _session.OmsiRootPath);

            ClearInspectorSelectionState();
            RefreshExplorer();

            var inserted =
                _explorerItems
                    .FirstOrDefault(
                        item =>
                            item.Kind ==
                                PickingKind.Spline &&
                            item.EntityId ==
                                insertion.SplineId);

            if (inserted is not null)
            {
                Viewport.SelectExplorerItem(
                    inserted,
                    focus:
                        true);
            }

            StatusText.Text =
                $"Complete to concluído: #{selection.EntityId} → #{insertion.SplineId} → #{targetSplineId} · comprimento {request.Length:F2} m · raio {request.Radius:F2} m.";
        }
        catch (Exception exception)
        {
            CompleteToSplineButton.IsEnabled =
                _selectionInfo is
                {
                    Kind:
                        PickingKind.Spline,
                    NextSplineId:
                        -1
                };

            StatusText.Text =
                $"Complete to falhou: {exception.Message}";
        }
    }

    private async void OnEditSplineAdvancedClick(
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
                "Salve as transformações pendentes antes de alterar Cant/Mirror.";

            return;
        }

        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            StatusText.Text =
                "Cancele a ferramenta de posicionamento antes de alterar Cant/Mirror.";

            return;
        }

        var source =
            _session.CurrentMap?
                .Tiles
                .FirstOrDefault(
                    tile =>
                        tile.Reference.X ==
                            selection.TileX &&
                        tile.Reference.Y ==
                            selection.TileY)
                ?.Content.Splines
                .FirstOrDefault(
                    spline =>
                        spline.SplineId ==
                            selection.EntityId &&
                        string.Equals(
                            spline.SplinePath,
                            selection.AssetPath,
                            StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            StatusText.Text =
                "Não foi possível localizar a spline selecionada no tile carregado.";

            return;
        }

        var cantStartBox =
            new NumberBox
            {
                Header =
                    "Cant inicial",
                Value =
                    source.CantStart,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Inline
            };

        var cantEndBox =
            new NumberBox
            {
                Header =
                    "Cant final",
                Value =
                    source.CantEnd,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Inline
            };

        var mirrorCheckBox =
            new CheckBox
            {
                Content =
                    "Mirror",
                IsChecked =
                    source.IsMirrored
            };

        var description =
            new TextBlock
            {
                Text =
                    "Os valores são gravados nos campos avançados reais da seção [spline]/[spline_h]. O restante do bloco é preservado e um backup é criado antes da alteração.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    10
            };

        panel.Children.Add(
            cantStartBox);
        panel.Children.Add(
            cantEndBox);
        panel.Children.Add(
            mirrorCheckBox);
        panel.Children.Add(
            description);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    $"Spline #{selection.EntityId} · Cant / Mirror",
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

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        if (
            !double.IsFinite(
                cantStartBox.Value) ||
            !double.IsFinite(
                cantEndBox.Value))
        {
            StatusText.Text =
                "Cant inicial/final precisam ser valores numéricos válidos.";

            return;
        }

        try
        {
            EditSplineAdvancedButton.IsEnabled =
                false;

            StatusText.Text =
                $"Salvando Cant/Mirror da spline #{selection.EntityId} com backup...";

            var updated =
                await _session
                    .UpdateSplineAdvancedAsync(
                        selection,
                        cantStartBox.Value,
                        cantEndBox.Value,
                        mirrorCheckBox
                            .IsChecked ==
                        true);

            if (_session.OmsiRootPath is null)
            {
                throw new InvalidOperationException(
                    "Instalação OMSI não selecionada.");
            }

            await Viewport
                .SetMapSnapshotAsync(
                    updated.Snapshot,
                    _session.OmsiRootPath);

            RefreshExplorer();

            var refreshedItem =
                _explorerItems
                    .FirstOrDefault(
                        item =>
                            item.Kind ==
                                PickingKind.Spline &&
                            item.EntityId ==
                                selection.EntityId &&
                            item.TileX ==
                                selection.TileX &&
                            item.TileY ==
                                selection.TileY);

            if (refreshedItem is not null)
            {
                Viewport.SelectExplorerItem(
                    refreshedItem,
                    focus:
                        false);
            }

            StatusText.Text =
                $"Spline #{selection.EntityId}: Cant {updated.Spline.CantStart:G4} → {updated.Spline.CantEnd:G4}, Mirror {(updated.Spline.IsMirrored ? "ativo" : "desativado")}. Backup: {updated.BackupPath}";
        }
        catch (Exception exception)
        {
            EditSplineAdvancedButton.IsEnabled =
                _selectionInfo?.Kind ==
                PickingKind.Spline;

            StatusText.Text =
                $"Falha ao salvar Cant/Mirror: {exception.Message}";
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

            Viewport
                .SetSplinePlacementHeightMode(
                    selection.IsHeightSpline ==
                    true);

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
                selection.IsHeightSpline ==
                    true
                    ? $"Cópia [spline_h] desconectada da spline #{selection.EntityId}: defina a nova geometria no viewport."
                    : $"Cópia desconectada da spline #{selection.EntityId}: defina a nova geometria no viewport.";
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
        var selections =
            Viewport
                .GetSelectedSelectionInfos()
                .GroupBy(
                    selection =>
                        (
                            selection.Kind,
                            selection.EntityId,
                            selection.TileX,
                            selection.TileY,
                            selection.AssetPath
                        ))
                .Select(
                    group =>
                        group.First())
                .ToArray();

        if (
            selections.Length ==
                0 &&
            _selectionInfo is not
                null)
        {
            selections =
                [
                    _selectionInfo
                ];
        }

        if (selections.Length == 0)
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

        var objectCount =
            selections.Count(
                selection =>
                    selection.Kind ==
                    PickingKind.Object);

        var splineCount =
            selections.Count(
                selection =>
                    selection.Kind ==
                    PickingKind.Spline);

        var single =
            selections.Length ==
                1
                ? selections[0]
                : null;

        var label =
            single is not null
                ? single.Kind ==
                    PickingKind.Object
                    ? $"objeto #{single.EntityId}"
                    : $"spline #{single.EntityId}"
                : $"{selections.Length} itens";

        var detail =
            single is not null
                ? single.Kind ==
                    PickingKind.Object
                    ? "O objeto será removido do tile OMSI."
                    : "A spline será removida e os vínculos recíprocos dos vizinhos serão liberados."
                : $"Serão removidos {objectCount} objeto(s) e {splineCount} spline(s). " +
                  "Cada alteração usa a rotina segura com backup; vínculos de splines são atualizados conforme necessário.";

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    $"Excluir {label}?",
                Content =
                    $"{detail}\n\nEsta operação grava os arquivos do mapa com backup seguro.",
                PrimaryButtonText =
                    selections.Length >
                        1
                        ? "Excluir todos"
                        : "Excluir",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Close
            };

        var result =
            await dialog.ShowAdaptiveAsync();

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
                selections.Length >
                    1
                    ? $"Excluindo {selections.Length} itens com backup seguro..."
                    : $"Excluindo {label} com backup seguro...";

            NativeMapSnapshot? snapshot =
                null;

            foreach (
                var selection in
                    selections
                        .OrderBy(
                            selection =>
                                selection.Kind ==
                                    PickingKind.Spline
                                    ? 1
                                    : 0))
            {
                snapshot =
                    await _session
                        .DeleteSelectionAsync(
                            selection);
            }

            if (
                snapshot is null ||
                _session.OmsiRootPath is null)
            {
                throw new InvalidOperationException(
                    "Não foi possível recarregar o mapa após a exclusão.");
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
                selections.Length >
                    1
                    ? $"{selections.Length} itens excluídos com backup · {objectCount} objeto(s) · {splineCount} spline(s)."
                    : $"{(single!.Kind == PickingKind.Object ? "Objeto" : "Spline")} #{single.EntityId} excluído(a) com backup.";
        }
        catch (Exception exception)
        {
            DeleteSelectionButton.IsEnabled =
                _selectionInfo is not
                    null;

            ApplyInspectorButton.IsEnabled =
                _selectionInfo is not
                    null;

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

    private void RegisterConstructionHistory(
        string label)
    {
        var mapDirectory =
            _session.CurrentMap?
                .Map.DirectoryPath;

        var backupDirectory =
            _session
                .LastBackupDirectory;

        if (
            string.IsNullOrWhiteSpace(
                mapDirectory) ||
            string.IsNullOrWhiteSpace(
                backupDirectory) ||
            !Directory.Exists(
                backupDirectory))
        {
            return;
        }

        if (
            !string.Equals(
                _constructionHistoryMapDirectory,
                mapDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            _constructionUndoStack.Clear();
            _constructionRedoStack.Clear();
            _constructionHistoryMapDirectory =
                mapDirectory;
        }

        _constructionUndoStack.Add(
            new ConstructionHistoryEntry(
                label,
                mapDirectory,
                backupDirectory));

        if (
            _constructionUndoStack.Count >
            32)
        {
            _constructionUndoStack
                .RemoveAt(0);
        }

        _constructionRedoStack.Clear();
        RefreshConstructionHistoryUi();
    }

    private void RefreshConstructionHistoryUi()
    {
        var currentMap =
            _session.CurrentMap?
                .Map.DirectoryPath;

        if (
            !string.Equals(
                currentMap,
                _constructionHistoryMapDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            _constructionUndoStack.Clear();
            _constructionRedoStack.Clear();
            _constructionHistoryMapDirectory =
                currentMap;
        }

        UndoConstructionMenuItem.IsEnabled =
            _constructionUndoStack.Count >
            0;

        RedoConstructionMenuItem.IsEnabled =
            _constructionRedoStack.Count >
            0;

        UndoConstructionMenuItem.Text =
            _constructionUndoStack.Count >
                0
                ? "Desfazer construção · " +
                  _constructionUndoStack[^1]
                      .Label
                : "Desfazer construção";

        RedoConstructionMenuItem.Text =
            _constructionRedoStack.Count >
                0
                ? "Refazer construção · " +
                  _constructionRedoStack[^1]
                      .Label
                : "Refazer construção";
    }

    private async void OnUndoConstructionClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _constructionUndoStack.Count ==
            0)
        {
            return;
        }

        var entry =
            _constructionUndoStack[^1];

        if (
            !string.Equals(
                _session.CurrentMap?
                    .Map.DirectoryPath,
                entry.MapDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            RefreshConstructionHistoryUi();
            return;
        }

        try
        {
            StatusText.Text =
                "Desfazendo construção: " +
                entry.Label;

            var restored =
                await _session
                    .RestoreMapStudioBackupAsync(
                        entry.BackupDirectory);

            _constructionUndoStack.RemoveAt(
                _constructionUndoStack.Count -
                1);

            _constructionRedoStack.Add(
                new ConstructionHistoryEntry(
                    entry.Label,
                    entry.MapDirectory,
                    restored
                        .RollbackBackupDirectory));

            await ApplyMapSnapshotAsync(
                restored.Snapshot,
                focusActiveTile: false);

            RefreshConstructionHistoryUi();

            StatusText.Text =
                "Construção desfeita: " +
                entry.Label;
        }
        catch (Exception exception)
        {
            StatusText.Text =
                "Falha ao desfazer construção: " +
                exception.Message;
        }
    }

    private async void OnRedoConstructionClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _constructionRedoStack.Count ==
            0)
        {
            return;
        }

        var entry =
            _constructionRedoStack[^1];

        if (
            !string.Equals(
                _session.CurrentMap?
                    .Map.DirectoryPath,
                entry.MapDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            RefreshConstructionHistoryUi();
            return;
        }

        try
        {
            StatusText.Text =
                "Refazendo construção: " +
                entry.Label;

            var restored =
                await _session
                    .RestoreMapStudioBackupAsync(
                        entry.BackupDirectory);

            _constructionRedoStack.RemoveAt(
                _constructionRedoStack.Count -
                1);

            _constructionUndoStack.Add(
                new ConstructionHistoryEntry(
                    entry.Label,
                    entry.MapDirectory,
                    restored
                        .RollbackBackupDirectory));

            await ApplyMapSnapshotAsync(
                restored.Snapshot,
                focusActiveTile: false);

            RefreshConstructionHistoryUi();

            StatusText.Text =
                "Construção refeita: " +
                entry.Label;
        }
        catch (Exception exception)
        {
            StatusText.Text =
                "Falha ao refazer construção: " +
                exception.Message;
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

    private void SetActiveMapTool(
        Button activeButton)
    {
        _mapMode =
            false;

        if (MapExplorerPanel is not null)
        {
            MapExplorerPanel.Visibility =
                Visibility.Collapsed;
        }

        var defaultBackground =
            (Microsoft.UI.Xaml.Media.Brush)
                MainRoot.Resources[
                    "MapToolDefaultBackgroundBrush"];

        var defaultBorder =
            (Microsoft.UI.Xaml.Media.Brush)
                MainRoot.Resources[
                    "MapToolDefaultBorderBrush"];

        var activeBackground =
            (Microsoft.UI.Xaml.Media.Brush)
                MainRoot.Resources[
                    "MapToolActiveBackgroundBrush"];

        var activeBorder =
            (Microsoft.UI.Xaml.Media.Brush)
                MainRoot.Resources[
                    "MapToolActiveBorderBrush"];

        var buttons =
            new[]
            {
                ToolSelectionButton,
                ToolObjectsButton,
                ToolSplinesButton,
                ToolBridgesButton,
                ToolTunnelsButton,
                ToolBuildingsButton,
                ToolVegetationButton,
                ToolTransitAssetsButton,
                ToolStreetFurnitureButton,
                ToolUtilitiesButton,
                ToolCrossingsButton,
                ToolTerrainButton,
                ToolWaterButton,
                ToolTrafficButton,
                ToolTransportButton,
                ToolValidationButton
            };

        foreach (var button in buttons)
        {
            button.Background =
                defaultBackground;

            button.BorderBrush =
                defaultBorder;

            button.BorderThickness =
                new Thickness(1);

            button.Opacity =
                0.88;
        }

        activeButton.Background =
            activeBackground;

        activeButton.BorderBrush =
            activeBorder;

        activeButton.BorderThickness =
            new Thickness(2);

        activeButton.Opacity =
            1;

        UpdateContextToolPalette(
            activeButton);
    }

    private void UpdateContextToolPalette(
        Button activeButton)
    {
        var panels =
            new FrameworkElement[]
            {
                SelectionContextPanel,
                ObjectContextPanel,
                RoadContextPanel,
                BridgeContextPanel,
                BuildingContextPanel,
                VegetationContextPanel,
                CrossingContextPanel,
                TerrainContextPanel,
                WaterContextPanel,
                TrafficContextPanel,
                TransportContextPanel,
                GenericContextPanel
            };

        foreach (var panel in panels)
        {
            panel.Visibility =
                Visibility.Collapsed;
        }

        FrameworkElement targetPanel =
            GenericContextPanel;

        var title =
            "Ferramenta";

        if (ReferenceEquals(
                activeButton,
                ToolSelectionButton) ||
            ReferenceEquals(
                activeButton,
                ToolValidationButton))
        {
            targetPanel =
                SelectionContextPanel;

            title =
                ReferenceEquals(
                    activeButton,
                    ToolValidationButton)
                    ? "Validação"
                    : "Seleção fácil";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolObjectsButton) ||
            ReferenceEquals(
                activeButton,
                ToolStreetFurnitureButton) ||
            ReferenceEquals(
                activeButton,
                ToolUtilitiesButton))
        {
            targetPanel =
                ObjectContextPanel;

            title =
                ReferenceEquals(
                    activeButton,
                    ToolStreetFurnitureButton)
                    ? "Sinalização"
                    : ReferenceEquals(
                        activeButton,
                        ToolUtilitiesButton)
                        ? "Infraestrutura"
                        : "Objetos";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolSplinesButton))
        {
            targetPanel =
                RoadContextPanel;

            title =
                "Ruas";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolBridgesButton) ||
            ReferenceEquals(
                activeButton,
                ToolTunnelsButton))
        {
            targetPanel =
                BridgeContextPanel;

            title =
                "Pontes / Túneis";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolBuildingsButton))
        {
            targetPanel =
                BuildingContextPanel;

            title =
                "Prédios";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolVegetationButton))
        {
            targetPanel =
                VegetationContextPanel;

            title =
                "Vegetação";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolCrossingsButton))
        {
            targetPanel =
                CrossingContextPanel;

            title =
                "Cruzamentos";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolTerrainButton))
        {
            targetPanel =
                TerrainContextPanel;

            title =
                "Terreno";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolWaterButton))
        {
            targetPanel =
                WaterContextPanel;

            title =
                "Água";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolTrafficButton))
        {
            targetPanel =
                TrafficContextPanel;

            title =
                "Tráfego";
        }
        else if (
            ReferenceEquals(
                activeButton,
                ToolTransportButton) ||
            ReferenceEquals(
                activeButton,
                ToolTransitAssetsButton))
        {
            targetPanel =
                TransportContextPanel;

            title =
                "Transporte";
        }

        ContextToolTitleText.Text =
            title;

        targetPanel.Visibility =
            Visibility.Visible;

        InspectorContextBadgeText.Text =
            title.ToUpperInvariant();

        InspectorContextSubtitleText.Text =
            ReferenceEquals(
                activeButton,
                ToolTerrainButton)
                ? "Nivelamento, pincel, suavização e textura do terreno"
                : ReferenceEquals(
                    activeButton,
                    ToolSplinesButton)
                    ? "Geometria, inclinação, links e snap da via selecionada"
                    : ReferenceEquals(
                        activeButton,
                        ToolTrafficButton)
                        ? "Controladores, regras e sinalização do mapa"
                        : ReferenceEquals(
                            activeButton,
                            ToolTransportButton)
                            ? "Tracks, Trips, Stops, StationLinks e Lines/Tours"
                            : "Transformação e dados do item selecionado";

        InspectorTerrainFields.Visibility =
            ReferenceEquals(
                activeButton,
                ToolTerrainButton)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void OnToolSelectionClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolSelectionButton);

        OnSceneExplorerModeClick(
            sender,
            e);

        SetSelectionModeFromShortcut(
            0);

        StatusText.Text =
            "Seleção fácil ativa: clique no item; clique novamente no mesmo ponto para alternar itens sobrepostos.";
    }

    private async void OnToolObjectsClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolObjectsButton);

        _junctionPlacementTarget =
            null;

        SetSelectionModeFromShortcut(
            1);

        await ActivateLibraryToolAsync(
            1,
            null,
            "Objetos: biblioteca SCO pronta para posicionar e editar.");
    }
    private void OnSplineHeightToggle(
        object sender,
        RoutedEventArgs e)
    {
        var isHeight =
            SplineHeightCheckBox
                .IsChecked ==
            true;

        if (isHeight)
        {
            SplineEasyRoadCheckBox.IsChecked =
                false;

            SplineContinuousCheckBox.IsChecked =
                false;
        }

        SplineEasyRoadCheckBox.IsEnabled =
            !isHeight;

        SplineContinuousCheckBox.IsEnabled =
            !isHeight;

        SplineEndpointSnapCheckBox.IsEnabled =
            !isHeight;

        SplineEndpointSnapDistanceBox.IsEnabled =
            !isHeight;

        SplineAutoConnectCheckBox.IsEnabled =
            !isHeight;

        Viewport
            .SetSplinePlacementHeightMode(
                isHeight);

        Viewport
            .SetSplineEasyRoadOptions(
                !isHeight &&
                SplineEasyRoadCheckBox
                    .IsChecked ==
                true,
                double.IsFinite(
                    SplineCurveOffsetBox
                        .Value)
                    ? SplineCurveOffsetBox
                        .Value
                    : 0.0);

        StatusText.Text =
            isHeight
                ? "[spline_h] ativo: grava spline de altura; Estrada fácil, continuidade e auto-link de vias normais ficam desativados."
                : "[spline] normal ativo.";
    }

    private void OnSplineEasyRoadToggle(
        object sender,
        RoutedEventArgs e)
    {
        if (
            SplineEasyRoadCheckBox
                .IsChecked ==
            true)
        {
            SplineCurveCheckBox.IsChecked =
                false;
        }

        EasyRoadControlGrid.Visibility =
            Visibility.Collapsed;

        Viewport
            .SetSplineEasyRoadOptions(
                SplineEasyRoadCheckBox
                    .IsChecked ==
                true,
                double.IsFinite(
                    SplineCurveOffsetBox
                        .Value)
                    ? SplineCurveOffsetBox
                        .Value
                    : 0.0);

        StatusText.Text =
            SplineEasyRoadCheckBox
                .IsChecked ==
            true
                ? "Estrada fácil ativa: dois cliques criam a via; ajuste o offset lateral para curvar."
                : "Estrada fácil desativada: use reta ou curva manual de 3 cliques.";
    }

    private void OnApplyEasyRoadPointsClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !double.IsFinite(
                EasyRoadStartXBox.Value) ||
            !double.IsFinite(
                EasyRoadStartZBox.Value) ||
            !double.IsFinite(
                EasyRoadEndXBox.Value) ||
            !double.IsFinite(
                EasyRoadEndZBox.Value))
        {
            StatusText.Text =
                "Estrada fácil: coordenadas inválidas.";

            return;
        }

        if (
            Viewport
                .TryApplyEasyRoadControlPoints(
                    EasyRoadStartXBox.Value,
                    EasyRoadStartZBox.Value,
                    EasyRoadEndXBox.Value,
                    EasyRoadEndZBox.Value))
        {
            StatusText.Text =
                "Estrada fácil: pontos atualizados na prévia. Auto-link foi limpo porque os endpoints mudaram.";

            return;
        }

        StatusText.Text =
            "Estrada fácil: não foi possível aplicar os pontos; mantenha início e fim sobre terreno carregado.";
    }

    private void OnAdjustEasyRoadCurveOnMapClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            Viewport
                .TryBeginEasyRoadCurveControl(
                    out var status))
        {
            SplineCurveOffsetBox.Value =
                Viewport
                    .SplinePlacementControlState
                    ?.CurveOffset ??
                SplineCurveOffsetBox.Value;
        }

        StatusText.Text =
            status;
    }

    private async void OnConfirmEasyRoadClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !Viewport
                .TryConfirmEasyRoad(
                    out var request,
                    out var status) ||
            request is null)
        {
            StatusText.Text =
                status;

            return;
        }

        EasyRoadControlGrid.Visibility =
            Visibility.Collapsed;

        await HandleSplinePlacementAsync(
            request);

        StatusText.Text +=
            " · confirmado pela prévia da Estrada fácil.";
    }

    private void OnSplineCurveOffsetChanged(
        NumberBox sender,
        NumberBoxValueChangedEventArgs e)
    {
        if (
            Viewport is null ||
            SplineEasyRoadCheckBox is null)
        {
            return;
        }

        Viewport
            .SetSplineEasyRoadOptions(
                SplineEasyRoadCheckBox
                    .IsChecked ==
                true,
                double.IsFinite(
                    e.NewValue)
                    ? e.NewValue
                    : 0.0);
    }

    private async void OnToolSplinesClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolSplinesButton);

        _junctionPlacementTarget =
            null;

        SplineElevationOffsetBox.Value =
            0;

        _roadElevationMode =
            NativeRoadElevationMode
                .FollowTerrain;

        Viewport
            .SetSplinePlacementElevationMode(
                _roadElevationMode);

        Viewport
            .SetSplinePlacementElevationOffset(
                0.0);

        RoadElevationText.Text =
            "Elevação: terreno";

        RoadSnapButton.Content =
            "Snap: on";

        RoadContinuousButton.Content =
            "Contínuo: on";


        SplineHeightCheckBox.IsChecked =
            false;

        SplineEasyRoadCheckBox.IsEnabled =
            true;

        SplineContinuousCheckBox.IsEnabled =
            true;

        SplineEndpointSnapCheckBox.IsEnabled =
            true;

        SplineEndpointSnapDistanceBox.IsEnabled =
            true;

        SplineAutoConnectCheckBox.IsEnabled =
            true;
        SplineEasyRoadCheckBox.IsChecked =
            true;

        SplineCurveOffsetBox.Value =
            0;

        SplineCurveCheckBox.IsChecked =
            false;

        SetSelectionModeFromShortcut(
            2);

        await ActivateLibraryToolAsync(
            2,
            null,
            "Ruas: escolha uma SLI e clique-arraste do início ao fim. Use Curva, ↑/↓, Snap e Contínuo na barra inferior.");
    }

    private void ConfigureRoadPreset(
        bool heightMode,
        bool easyRoad,
        bool manualCurve,
        string status)
    {
        SplineHeightCheckBox.IsChecked =
            heightMode;

        SplineEasyRoadCheckBox.IsChecked =
            easyRoad &&
            !heightMode;

        SplineCurveCheckBox.IsChecked =
            manualCurve &&
            !heightMode;

        SplineCurveOffsetBox.Value =
            0;

        SplineContinuousCheckBox.IsEnabled =
            !heightMode;

        SplineEndpointSnapCheckBox.IsEnabled =
            !heightMode;

        SplineEndpointSnapDistanceBox.IsEnabled =
            !heightMode;

        SplineAutoConnectCheckBox.IsEnabled =
            !heightMode;

        SplineEasyRoadCheckBox.IsEnabled =
            !heightMode;

        Viewport
            .SetSplinePlacementHeightMode(
                heightMode);

        Viewport
            .SetSplineEasyRoadOptions(
                easyRoad &&
                !heightMode,
                0.0);

        StatusText.Text =
            status;
    }

    private void OnRoadEditCurveClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo?.Kind !=
                PickingKind.Spline)
        {
            StatusText.Text =
                "Ruas: selecione uma spline para editar a curva.";
            return;
        }

        if (
            !Viewport
                .BeginSelectedSplineCurveEdit(
                    out var status))
        {
            StatusText.Text =
                status;
            return;
        }

        StatusText.Text =
            status;
    }

    private void OnRoadSplitClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo?.Kind !=
                PickingKind.Spline)
        {
            StatusText.Text =
                "Ruas: selecione uma spline antes de dividir.";
            return;
        }

        Viewport.CancelSplinePlacement();

        if (
            !Viewport
                .BeginSplineSplitPick())
        {
            StatusText.Text =
                "Ruas: não foi possível iniciar o modo Dividir.";
            return;
        }

        StatusText.Text =
            "Ruas: Dividir ativo. Clique no ponto exato do trecho onde deseja cortar.";
    }

    private async Task HandleSplineSplitAsync(
        NativeSplineSplitRequest request)
    {
        try
        {
            if (
                _session.PendingTransformCount >
                0)
            {
                await _session
                    .SavePendingTransformsAsync();

                SaveChangesButton.IsEnabled =
                    false;
            }

            StatusText.Text =
                "Ruas: dividindo trecho com backup...";

            var result =
                await _session
                    .SplitSplineAsync(
                        request);

            RegisterConstructionHistory(
                "Dividir spline");

            await Viewport
                .SetMapSnapshotAsync(
                    result.Snapshot,
                    _session.OmsiRootPath!);

            ClearInspectorSelectionState();
            RefreshExplorer();

            StatusText.Text =
                $"Ruas: spline #{result.FirstSplineId} dividida; novo segmento #{result.SecondSplineId}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Ruas: falha ao dividir: {exception.Message}";
        }
    }

    private async Task CreateParallelRoadAsync(
        double direction)
    {
        if (
            _selectionInfo?.Kind !=
                PickingKind.Spline)
        {
            StatusText.Text =
                "Ruas: selecione uma spline para criar a paralela.";
            return;
        }

        var distance =
            double.IsFinite(
                RoadParallelOffsetBox.Value)
                ? Math.Clamp(
                    RoadParallelOffsetBox.Value,
                    0.5,
                    30.0)
                : 3.5;

        if (
            !Viewport
                .TryCreateParallelSplineRequest(
                    distance *
                        direction,
                    out var request,
                    out var status) ||
            request is null)
        {
            StatusText.Text =
                status;
            return;
        }

        StatusText.Text =
            status;

        await HandleSplinePlacementAsync(
            request);
    }

    private async void OnRoadParallelLeftClick(
        object sender,
        RoutedEventArgs e)
    {
        await CreateParallelRoadAsync(
            -1.0);
    }

    private async void OnRoadParallelRightClick(
        object sender,
        RoutedEventArgs e)
    {
        await CreateParallelRoadAsync(
            1.0);
    }

    private async void OnRoadReverseFlowClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo?.Kind !=
                PickingKind.Spline)
        {
            StatusText.Text =
                "Ruas: selecione uma spline para inverter o fluxo.";
            return;
        }

        try
        {
            if (
                _session.PendingTransformCount >
                0)
            {
                await _session
                    .SavePendingTransformsAsync();

                SaveChangesButton.IsEnabled =
                    false;
            }

            StatusText.Text =
                "Ruas: preparando variante de fluxo...";

            var result =
                await _session
                    .TogglePlacedSplineVehicleFlowAsync(
                        _selectionInfo);

            await Viewport
                .SetMapSnapshotAsync(
                    result.Snapshot,
                    _session.OmsiRootPath!);

            ClearInspectorSelectionState();
            RefreshExplorer();

            StatusText.Text =
                result.Reversed
                    ? $"Ruas: fluxo invertido neste trecho · {result.ReversedVehiclePathCount} path(s) de veículo alterado(s)."
                    : "Ruas: fluxo restaurado para a SLI original.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Ruas: não foi possível inverter o fluxo: {exception.Message}";
        }
    }

    private async void OnRoadReplaceTypeClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo?.Kind !=
                PickingKind.Spline)
        {
            StatusText.Text =
                "Ruas: selecione no mapa o trecho que será substituído.";
            return;
        }

        var replacement =
            GetSelectedAssetLibraryEntry();

        if (
            replacement?.Kind !=
                OmsiAssetKind.Spline)
        {
            StatusText.Text =
                "Ruas: escolha na Biblioteca a SLI que substituirá o trecho selecionado.";
            return;
        }

        if (
            string.Equals(
                replacement.RelativePath,
                _selectionInfo.AssetPath,
                StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text =
                "Ruas: o trecho já usa essa SLI.";
            return;
        }

        try
        {
            if (
                _session.PendingTransformCount >
                0)
            {
                await _session
                    .SavePendingTransformsAsync();

                SaveChangesButton.IsEnabled =
                    false;
            }

            var snapshot =
                await _session
                    .ReplacePlacedSplinePathAsync(
                        _selectionInfo,
                        replacement.RelativePath);

            await Viewport
                .SetMapSnapshotAsync(
                    snapshot,
                    _session.OmsiRootPath!);

            ClearInspectorSelectionState();
            RefreshExplorer();

            StatusText.Text =
                $"Ruas: tipo substituído por {replacement.RelativePath}. Geometria e links foram preservados.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Ruas: falha ao substituir tipo: {exception.Message}";
        }
    }

    private async void OnRoadMirrorClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo?.Kind !=
                PickingKind.Spline)
        {
            StatusText.Text =
                "Ruas: selecione uma spline para espelhar.";
            return;
        }

        try
        {
            if (
                _session.PendingTransformCount >
                0)
            {
                await _session
                    .SavePendingTransformsAsync();

                SaveChangesButton.IsEnabled =
                    false;
            }

            var source =
                _session.CurrentMap
                    ?.Tiles
                    .SelectMany(
                        tile =>
                            tile.Content.Splines)
                    .FirstOrDefault(
                        spline =>
                            spline.SplineId ==
                            _selectionInfo.EntityId &&
                            string.Equals(
                                spline.SplinePath,
                                _selectionInfo.AssetPath,
                                StringComparison
                                    .OrdinalIgnoreCase));

            if (source is null)
            {
                StatusText.Text =
                    "Ruas: a spline selecionada não foi encontrada no snapshot.";
                return;
            }

            var update =
                await _session
                    .UpdateSplineAdvancedAsync(
                        _selectionInfo,
                        source.CantStart,
                        source.CantEnd,
                        !source.IsMirrored);

            await Viewport
                .SetMapSnapshotAsync(
                    update.Snapshot,
                    _session.OmsiRootPath!);

            ClearInspectorSelectionState();
            RefreshExplorer();

            StatusText.Text =
                update.Spline.IsMirrored
                    ? "Ruas: spline espelhada."
                    : "Ruas: espelhamento removido.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Ruas: falha ao espelhar: {exception.Message}";
        }
    }

    private async void OnRoadDuplicateClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo?.Kind !=
                PickingKind.Spline)
        {
            StatusText.Text =
                "Ruas: selecione uma spline para duplicar.";
            return;
        }

        await StartSelectionCopyPlacementAsync();
    }

    private void OnRoadLevelSelectedClick(
        object sender,
        RoutedEventArgs e) =>
        LevelSelectedRoadAtCurrentHeight();

    private void ShiftSelectedRoadElevation(
        double delta)
    {
        if (
            _selectionInfo?.Kind !=
                PickingKind.Spline)
        {
            StatusText.Text =
                "Ruas: selecione uma spline para alterar a elevação.";
            return;
        }

        var target =
            Math.Clamp(
                _selectionInfo.Z +
                delta,
                -1000.0,
                3000.0);

        if (
            !Viewport.ApplySelectionInfo(
                _selectionInfo with
                {
                    Z = target
                }))
        {
            StatusText.Text =
                "Ruas: não foi possível alterar a elevação da spline selecionada.";
            return;
        }

        StatusText.Text =
            $"Ruas: spline #{_selectionInfo.EntityId} deslocada verticalmente para {target:F2} m.";
    }

    private void LevelSelectedRoadAtCurrentHeight()
    {
        if (
            _selectionInfo?.Kind !=
                PickingKind.Spline)
        {
            StatusText.Text =
                "Ruas: selecione uma spline para nivelar.";
            return;
        }

        if (
            !Viewport.ApplySelectionInfo(
                _selectionInfo with
                {
                    GradientStart = 0.0,
                    GradientEnd = 0.0
                }))
        {
            StatusText.Text =
                "Ruas: não foi possível nivelar a spline selecionada.";
            return;
        }

        StatusText.Text =
            $"Ruas: spline #{_selectionInfo.EntityId} nivelada na cota atual {_selectionInfo.Z:F2} m.";
    }

    private double GetRoadElevationStep() =>
        RoadElevationStepBox.SelectedIndex switch
        {
            0 => 0.5,
            2 => 2.0,
            3 => 5.0,
            _ => 1.0
        };

    private void ApplyRoadElevationMode(
        NativeRoadElevationMode mode,
        double elevationValue,
        string status)
    {
        _roadElevationMode =
            mode;

        SplineElevationOffsetBox.Value =
            elevationValue;

        Viewport
            .SetSplinePlacementElevationMode(
                mode);

        Viewport
            .SetSplinePlacementElevationOffset(
                elevationValue);

        RoadElevationText.Text =
            mode switch
            {
                NativeRoadElevationMode.Elevate =>
                    $"Elevação: {Math.Abs(elevationValue):+0.#;-0.#;0} m",
                NativeRoadElevationMode.Lower =>
                    $"Elevação: {-Math.Abs(elevationValue):+0.#;-0.#;0} m",
                NativeRoadElevationMode.Level =>
                    "Elevação: nível",
                _ =>
                    "Elevação: terreno"
            };

        StatusText.Text =
            status;
    }

    private void AdjustRoadElevation(
        double delta)
    {
        var current =
            _roadElevationMode switch
            {
                NativeRoadElevationMode.Elevate =>
                    Math.Abs(
                        double.IsFinite(
                            SplineElevationOffsetBox.Value)
                            ? SplineElevationOffsetBox.Value
                            : 0.0),

                NativeRoadElevationMode.Lower =>
                    -Math.Abs(
                        double.IsFinite(
                            SplineElevationOffsetBox.Value)
                            ? SplineElevationOffsetBox.Value
                            : 0.0),

                _ =>
                    0.0
            };

        var updated =
            Math.Clamp(
                current +
                delta,
                -100.0,
                300.0);

        if (
            Math.Abs(
                updated) <
            0.001)
        {
            ApplyRoadElevationMode(
                NativeRoadElevationMode.Level,
                0.0,
                "Ruas: Nivelar ativo · o fim manterá a mesma cota do início.");
            return;
        }

        var mode =
            updated >
            0
                ? NativeRoadElevationMode.Elevate
                : NativeRoadElevationMode.Lower;

        ApplyRoadElevationMode(
            mode,
            updated,
            mode ==
                NativeRoadElevationMode.Elevate
                ? $"Ruas: elevar · fim {updated:+0.0;-0.0;0.0} m em relação ao início."
                : $"Ruas: baixar · fim {updated:+0.0;-0.0;0.0} m em relação ao início.");
    }

    private void OnRoadElevationUpClick(
        object sender,
        RoutedEventArgs e) =>
        AdjustRoadElevation(
            GetRoadElevationStep());

    private void OnRoadElevationDownClick(
        object sender,
        RoutedEventArgs e) =>
        AdjustRoadElevation(
            -GetRoadElevationStep());

    private void OnRoadLevelElevationClick(
        object sender,
        RoutedEventArgs e) =>
        ApplyRoadElevationMode(
            NativeRoadElevationMode.Level,
            0.0,
            "Ruas: Nivelar ativo · o trecho manterá a cota do ponto inicial.");

    private void OnRoadElevationResetClick(
        object sender,
        RoutedEventArgs e) =>
        ApplyRoadElevationMode(
            NativeRoadElevationMode.FollowTerrain,
            0.0,
            "Ruas: seguindo novamente a altura real do terreno.");

    private void OnRoadSnapToggleClick(
        object sender,
        RoutedEventArgs e)
    {
        var enabled =
            SplineEndpointSnapCheckBox
                .IsChecked !=
            true;

        SplineEndpointSnapCheckBox.IsChecked =
            enabled;

        SplineAutoConnectCheckBox.IsChecked =
            enabled;

        var distance =
            double.IsFinite(
                SplineEndpointSnapDistanceBox
                    .Value)
                ? SplineEndpointSnapDistanceBox
                    .Value
                : 5.0;

        Viewport
            .SetSplineEndpointSnapOptions(
                enabled,
                distance,
                enabled);

        RoadSnapButton.Content =
            enabled
                ? "Snap: on"
                : "Snap: off";

        StatusText.Text =
            enabled
                ? "Ruas: snap e conexão automática ativos."
                : "Ruas: snap desativado.";
    }

    private void OnRoadContinuousToggleClick(
        object sender,
        RoutedEventArgs e)
    {
        var enabled =
            SplineContinuousCheckBox
                .IsChecked !=
            true;

        SplineContinuousCheckBox.IsChecked =
            enabled;

        RoadContinuousButton.Content =
            enabled
                ? "Contínuo: on"
                : "Contínuo: off";

        StatusText.Text =
            enabled
                ? "Ruas: próximos segmentos continuarão do endpoint anterior."
                : "Ruas: cada segmento será criado separadamente.";
    }

    private void OnRoadPresetEasyClick(
        object sender,
        RoutedEventArgs e)
    {
        OnToolSplinesClick(
            sender,
            e);

        ConfigureRoadPreset(
            heightMode:
                false,
            easyRoad:
                true,
            manualCurve:
                false,
            "Ruas: modo Arrastar ativo. Pressione no início, arraste a prévia e solte no fim.");
    }

    private void OnRoadPresetStraightClick(
        object sender,
        RoutedEventArgs e)
    {
        OnToolSplinesClick(
            sender,
            e);

        ConfigureRoadPreset(
            heightMode:
                false,
            easyRoad:
                false,
            manualCurve:
                false,
            "Ruas: reta por arrasto ativa. Pressione, arraste e solte.");
    }

    private void OnRoadPresetCurveClick(
        object sender,
        RoutedEventArgs e)
    {
        OnToolSplinesClick(
            sender,
            e);

        ConfigureRoadPreset(
            heightMode:
                false,
            easyRoad:
                false,
            manualCurve:
                true,
            "Ruas: curva visual ativa. Defina início/fim e mova o cursor para ajustar a curvatura.");
    }

    private void OnRoadPresetHeightClick(
        object sender,
        RoutedEventArgs e)
    {
        OnToolSplinesClick(
            sender,
            e);

        ConfigureRoadPreset(
            heightMode:
                true,
            easyRoad:
                false,
            manualCurve:
                false,
            "Ruas: [spline_h] ativa. Use ↑/↓ para controlar a elevação sem abrir o Inspector.");
    }

    private async void OnToolBridgesClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolBridgesButton);

        SplineElevationOffsetBox.Value =
            5;

        _roadElevationMode =
            NativeRoadElevationMode
                .FollowTerrain;

        Viewport
            .SetSplinePlacementElevationMode(
                _roadElevationMode);

        Viewport
            .SetSplinePlacementElevationOffset(
                5.0);

        SplineHeightCheckBox.IsChecked =
            false;

        SplineEasyRoadCheckBox.IsEnabled =
            true;

        SplineContinuousCheckBox.IsEnabled =
            true;

        SplineEndpointSnapCheckBox.IsEnabled =
            true;

        SplineEndpointSnapDistanceBox.IsEnabled =
            true;

        SplineAutoConnectCheckBox.IsEnabled =
            true;
        SplineEasyRoadCheckBox.IsChecked =
            true;

        SplineCurveOffsetBox.Value =
            0;

        SplineCurveCheckBox.IsChecked =
            false;

        SetSelectionModeFromShortcut(
            2);

        await ActivateLibraryGroupToolAsync(
            2,
            OmsiAssetLibraryGroup.Bridges,
            "Pontes: Estrada fácil ativa com +5 m. Escolha a SLI e ajuste elevação/curva conforme necessário.");
    }

    private async void OnToolBuildingsClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolBuildingsButton);

        SetSelectionModeFromShortcut(
            1);

        await ActivateLibraryGroupToolAsync(
            1,
            OmsiAssetLibraryGroup.Buildings,
            "Prédios: casas, comércio, indústria e equipamentos públicos.");
    }

    private async void OnToolVegetationClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolVegetationButton);

        SetSelectionModeFromShortcut(
            1);

        await ActivateLibraryGroupToolAsync(
            1,
            OmsiAssetLibraryGroup.Vegetation,
            "Vegetação: árvores, arbustos e grama.");
    }

    private async void OnToolTransitAssetsClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolTransitAssetsButton);

        SetSelectionModeFromShortcut(
            1);

        await ActivateLibraryGroupToolAsync(
            1,
            OmsiAssetLibraryGroup.Transit,
            "Assets de transporte: pontos, abrigos, terminais, garagens e estações.");
    }

    private async void OnToolStreetFurnitureClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolStreetFurnitureButton);

        SetSelectionModeFromShortcut(
            1);

        await ActivateLibraryGroupToolAsync(
            1,
            OmsiAssetLibraryGroup.StreetFurniture,
            "Mobiliário: postes, iluminação, placas, bancos, cercas e sinalização.");
    }

    private async void OnToolUtilitiesClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolUtilitiesButton);

        SetSelectionModeFromShortcut(
            1);

        await ActivateLibraryGroupToolAsync(
            1,
            OmsiAssetLibraryGroup.Utilities,
            "Infraestrutura: energia, água, saneamento e utilidades.");
    }

    private void OnToolCrossingsClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolCrossingsButton);

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
        SetActiveMapTool(
            ToolTerrainButton);

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
        SetActiveMapTool(
            ToolWaterButton);

        _junctionPlacementTarget =
            null;

        if (
            _session.CurrentMap is not
                { } snapshot ||
            snapshot.ActiveTile is not
                { } active)
        {
            StatusText.Text =
                "Água: abra um mapa e mantenha um tile ativo.";

            return;
        }

        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de editar a água.";

            return;
        }

        _assetPreviewCancellation
            ?.Cancel();

        Viewport.CancelSceneryPlacement();
        Viewport.CancelSplinePlacement();
        Viewport.RestoreSceneView();

        var loaded =
            snapshot.Tiles
                .FirstOrDefault(
                    tile =>
                        tile.Reference.X ==
                            active.X &&
                        tile.Reference.Y ==
                            active.Y);

        if (loaded is null)
        {
            StatusText.Text =
                $"Água: o tile ativo {active.X},{active.Y} não está carregado no viewport.";

            return;
        }

        var existingWater =
            loaded.Content.Water;

        var hasWaterState =
            existingWater is not null ||
            loaded.Content.Summary
                .WaterMarkerPresent ||
            loaded.Content.Summary
                .WaterFileExists;

        var initial =
            existingWater?.Heights
                .ToArray();

        if (
            initial is null ||
            initial.Length !=
                OmsiWaterGrid.HeightCount)
        {
            var terrain =
                loaded.Content.Terrain;

            if (
                terrain is not null &&
                terrain.Heights.Count ==
                    terrain.SampleCount *
                    terrain.SampleCount)
            {
                var last =
                    terrain.SampleCount -
                    1;

                initial =
                    [
                        terrain.Heights[0],
                        terrain.Heights[last],
                        terrain.Heights[
                            last *
                                terrain.SampleCount],
                        terrain.Heights[
                            terrain.Heights.Count -
                                1]
                    ];
            }
            else
            {
                initial =
                    [0, 0, 0, 0];
            }
        }

        NumberBox CreateHeightBox(
            string header,
            double value) =>
            new()
            {
                Header =
                    header,
                Minimum =
                    -10000,
                Maximum =
                    10000,
                Value =
                    value,
                SmallChange =
                    0.10,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var h00Box =
            CreateHeightBox(
                "Canto 0,0 · oeste/norte",
                initial[0]);

        var h10Box =
            CreateHeightBox(
                "Canto 1,0 · leste/norte",
                initial[1]);

        var h01Box =
            CreateHeightBox(
                "Canto 0,1 · oeste/sul",
                initial[2]);

        var h11Box =
            CreateHeightBox(
                "Canto 1,1 · leste/sul",
                initial[3]);

        var levelButton =
            new Button
            {
                Content =
                    "Nivelar os quatro cantos pela média"
            };

        levelButton.Click +=
            (_, _) =>
            {
                var values =
                    new[]
                    {
                        h00Box.Value,
                        h10Box.Value,
                        h01Box.Value,
                        h11Box.Value
                    };

                if (
                    values.Any(
                        value =>
                            !double.IsFinite(
                                value)))
                {
                    return;
                }

                var level =
                    values.Average();

                h00Box.Value =
                    level;
                h10Box.Value =
                    level;
                h01Box.Value =
                    level;
                h11Box.Value =
                    level;
            };

        var grid =
            new Grid
            {
                ColumnSpacing =
                    8,
                RowSpacing =
                    8
            };

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        grid.RowDefinitions.Add(
            new RowDefinition());

        grid.RowDefinitions.Add(
            new RowDefinition());

        Grid.SetColumn(
            h00Box,
            0);

        Grid.SetRow(
            h00Box,
            0);

        Grid.SetColumn(
            h10Box,
            1);

        Grid.SetRow(
            h10Box,
            0);

        Grid.SetColumn(
            h01Box,
            0);

        Grid.SetRow(
            h01Box,
            1);

        Grid.SetColumn(
            h11Box,
            1);

        Grid.SetRow(
            h11Box,
            1);

        grid.Children.Add(
            h00Box);

        grid.Children.Add(
            h10Box);

        grid.Children.Add(
            h01Box);

        grid.Children.Add(
            h11Box);

        var panel =
            new StackPanel
            {
                Spacing =
                    10,
                MinWidth =
                    520
            };

        panel.Children.Add(
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    InfoBarSeverity
                        .Informational,
                Title =
                    $"Água do tile {active.X},{active.Y}",
                Message =
                    existingWater is not null
                        ? "Os quatro valores abaixo são as alturas reais do sidecar .map.water. O preview D3D11 usa exatamente esses quatro cantos."
                        : "O tile ainda não possui uma grade de água válida. Os valores iniciais foram derivados dos quatro cantos do terreno quando disponíveis; revise-os antes de salvar."
            });

        panel.Children.Add(
            grid);

        panel.Children.Add(
            levelButton);

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Salvar cria/atualiza [water] e tile_X_Y.map.water com backup. Remover Água apaga o marcador e o sidecar com rollback em caso de falha.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.75
            });

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Editor de água OMSI",
                Content =
                    panel,
                PrimaryButtonText =
                    "Salvar água",
                SecondaryButtonText =
                    hasWaterState
                        ? "Remover água"
                        : string.Empty,
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
            answer ==
                ContentDialogResult
                    .Secondary &&
            hasWaterState)
        {
            try
            {
                StatusText.Text =
                    $"Removendo água do tile {active.X},{active.Y} com backup...";

                var removed =
                    await _session
                        .RemoveTileWaterAsync(
                            active.X,
                            active.Y);

                await ApplyMapSnapshotAsync(
                    removed.Snapshot,
                    focusActiveTile:
                        false);

                StatusText.Text =
                    string.IsNullOrWhiteSpace(
                        removed.BackupDirectory)
                        ? $"Tile {active.X},{active.Y} já não possuía água."
                        : $"Água removida do tile {active.X},{active.Y}. Backup: {removed.BackupDirectory}";
            }
            catch (Exception exception)
            {
                StatusText.Text =
                    $"Falha ao remover água: {exception.Message}";
            }

            return;
        }

        if (
            answer !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        var heights =
            new[]
            {
                h00Box.Value,
                h10Box.Value,
                h01Box.Value,
                h11Box.Value
            };

        if (
            heights.Any(
                value =>
                    !double.IsFinite(
                        value) ||
                    value <
                        -10000 ||
                    value >
                        10000))
        {
            StatusText.Text =
                "Água não salva: informe quatro alturas finitas válidas.";

            return;
        }

        try
        {
            StatusText.Text =
                $"Salvando água do tile {active.X},{active.Y} com backup...";

            var result =
                await _session
                    .SetTileWaterAsync(
                        active.X,
                        active.Y,
                        new OmsiWaterGrid(
                            heights
                                .Select(
                                    value =>
                                        (float)value)
                                .ToArray()));

            await ApplyMapSnapshotAsync(
                result.Snapshot,
                focusActiveTile:
                    false);

            StatusText.Text =
                $"Água salva no tile {active.X},{active.Y}. Backup: {result.BackupDirectory}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar água: {exception.Message}";
        }
    }

    private async void OnToolTrafficClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolTrafficButton);

        if (_session.CurrentMap is null)
        {
            StatusText.Text =
                "Tráfego: abra um mapa primeiro.";

            return;
        }

        _assetPreviewCancellation
            ?.Cancel();

        Viewport.CancelSceneryPlacement();
        Viewport.CancelSplinePlacement();
        Viewport.RestoreSceneView();

        SetSelectionModeFromShortcut(
            0);

        var trafficPathOptions =
            NativeTrafficPathDisplayOptions
                .TransportOverview;

        Viewport
            .SetTrafficPathDisplayOptions(
                trafficPathOptions);

        Viewport
            .SetTrafficPathSelectedOnly(
                false);

        TransportPathsSelectedOnlyCheckBox.IsChecked =
            false;

        TrafficPathSelectedOnlyCheckBox.IsChecked =
            false;

        SynchronizeTrafficPathControls(
            trafficPathOptions,
            fromTransport: false);

        Viewport
            .SetTrafficPathsVisible(
                true);

        _transportPathsVisible =
            true;

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

        UpdateTrafficPathStatusText();

        TrafficStatusText.Text +=
            $" {_trafficVehicleGroups.Count} grupo(s) de veículo.";

        TrafficProgramListView.SelectedIndex =
            _trafficPrograms.Count > 0
                ? 0
                : -1;

        StatusText.Text =
            "Tráfego: paths reais e preview de semáforos ativos.";
    }

    private async void OnEditSceneryPathClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo is not
                { } selection ||
            selection.Kind is not
                (
                    PickingKind.Object or
                    PickingKind.Spline
                ) ||
            _session.OmsiRootPath is not
                { } root ||
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Paths: selecione um objeto ou uma spline no mapa.";
            return;
        }

        try
        {
            EditSceneryPathButton.IsEnabled =
                false;

            int editedPathOrdinal;
            string updatedAssetPath;
            string backupPath;

            if (
                selection.Kind ==
                    PickingKind.Object)
            {
                if (
                    !OmsiSceneryObjectPathResolver
                        .TryResolve(
                            root,
                            selection.AssetPath,
                            out var target) ||
                    !File.Exists(
                        target))
                {
                    StatusText.Text =
                        "Paths SCO: o arquivo do objeto selecionado não foi encontrado.";
                    return;
                }

                StatusText.Text =
                    $"Paths SCO: lendo {Path.GetFileName(target)}...";

                var metadata =
                    await new OmsiSceneryObjectReader()
                        .ReadMetadataAsync(
                            target);

                if (
                    metadata.Paths.Count ==
                        0)
                {
                    StatusText.Text =
                        "Paths SCO: este objeto não possui seções [path].";
                    return;
                }

                var edit =
                    await SceneryPathEditorDialog
                        .ShowAsync(
                            MainRoot.XamlRoot,
                            selection.AssetPath,
                            metadata.Paths,
                            Viewport
                                .TrafficPathFocusedIndex);

                if (edit is null)
                {
                    return;
                }

                StatusText.Text =
                    $"Paths SCO: salvando path {edit.PathOrdinal} com backup...";

                var updated =
                    await _session
                        .UpdateSceneryPathAsync(
                            selection.AssetPath,
                            edit.PathOrdinal,
                            edit.Path);

                editedPathOrdinal =
                    edit.PathOrdinal;

                updatedAssetPath =
                    updated.AssetPath;

                backupPath =
                    updated.BackupPath;
            }
            else
            {
                if (
                    !OmsiSplinePathResolver
                        .TryResolve(
                            root,
                            selection.AssetPath,
                            out var target) ||
                    !File.Exists(
                        target))
                {
                    StatusText.Text =
                        "Paths SLI: o arquivo da spline selecionada não foi encontrado.";
                    return;
                }

                StatusText.Text =
                    $"Paths SLI: lendo {Path.GetFileName(target)}...";

                var definition =
                    await new OmsiSplineDefinitionReader()
                        .ReadAsync(
                            target);

                if (
                    definition.Paths.Count ==
                        0)
                {
                    StatusText.Text =
                        "Paths SLI: esta spline não possui seções [path].";
                    return;
                }

                var edit =
                    await SplinePathEditorDialog
                        .ShowAsync(
                            MainRoot.XamlRoot,
                            selection.AssetPath,
                            definition.Paths,
                            Viewport
                                .TrafficPathFocusedIndex);

                if (edit is null)
                {
                    return;
                }

                StatusText.Text =
                    $"Paths SLI: salvando path {edit.PathOrdinal} com backup...";

                var updated =
                    await _session
                        .UpdateSplinePathAsync(
                            selection.AssetPath,
                            edit.PathOrdinal,
                            edit.Path);

                editedPathOrdinal =
                    edit.PathOrdinal;

                updatedAssetPath =
                    updated.AssetPath;

                backupPath =
                    updated.BackupPath;
            }

            await ApplyMapSnapshotAsync(
                snapshot,
                focusActiveTile:
                    false);

            var owner =
                _explorerItems
                    .FirstOrDefault(
                        item =>
                            item.Kind ==
                                selection.Kind &&
                            item.EntityId ==
                                selection.EntityId &&
                            item.TileX ==
                                selection.TileX &&
                            item.TileY ==
                                selection.TileY &&
                            string.Equals(
                                item.AssetPath,
                                selection.AssetPath,
                                StringComparison.OrdinalIgnoreCase));

            if (owner is not null)
            {
                Viewport.SelectExplorerItem(
                    owner,
                    focus:
                        false);

                Viewport
                    .SetTrafficPathFocusedIndex(
                        editedPathOrdinal);

                if (
                    Viewport
                        .TrafficPathSelectedOnly)
                {
                    Viewport
                        .RefreshTrafficPathDisplay();
                }
            }

            UpdateTrafficPathStatusText();

            StatusText.Text =
                $"Path {(selection.Kind == PickingKind.Object ? "SCO" : "SLI")} {editedPathOrdinal} salvo · {Path.GetFileName(updatedAssetPath)} · backup {backupPath}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar path: {exception.Message}";
        }
        finally
        {
            EditSceneryPathButton.IsEnabled =
                _selectionInfo?.Kind is
                    PickingKind.Object or
                    PickingKind.Spline;
        }
    }

    private async void OnAddAssetPathClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo is not
                { } selection ||
            selection.Kind is not
                (
                    PickingKind.Object or
                    PickingKind.Spline
                ) ||
            _session.OmsiRootPath is not
                { } root ||
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Novo path: selecione um objeto ou uma spline.";
            return;
        }

        var suggestedType =
            TrafficPathModeComboBox
                .SelectedIndex switch
            {
                1 => 1,
                2 => 2,
                _ => 0
            };

        var typeBox =
            new ComboBox
            {
                Header =
                    "Tipo",
                ItemsSource =
                    new[]
                    {
                        "Veículo",
                        "Pedestre",
                        "Trilho",
                        "Aéreo"
                    },
                SelectedIndex =
                    suggestedType,
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var widthBox =
            new NumberBox
            {
                Header =
                    "Largura (m)",
                Minimum =
                    0,
                Maximum =
                    1000000,
                Value =
                    3,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Compact
            };

        var directionBox =
            new ComboBox
            {
                Header =
                    "Direção",
                ItemsSource =
                    new[]
                    {
                        "0 · →",
                        "1 · ←",
                        "2 · ↔"
                    },
                SelectedIndex =
                    0,
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var xBox =
            new NumberBox
            {
                Header =
                    selection.Kind ==
                        PickingKind.Spline
                        ? "Offset X"
                        : "X",
                Minimum =
                    -1000000,
                Maximum =
                    1000000,
                Value =
                    0,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Compact
            };

        var zBox =
            new NumberBox
            {
                Header =
                    selection.Kind ==
                        PickingKind.Spline
                        ? "Offset Z"
                        : "Z",
                Minimum =
                    -1000000,
                Maximum =
                    1000000,
                Value =
                    0,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Compact
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    420
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "O novo path é gravado diretamente no asset compartilhado. Depois você pode usar Editar paths do item para ajustar todos os parâmetros avançados.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            });

        panel.Children.Add(
            typeBox);
        panel.Children.Add(
            widthBox);
        panel.Children.Add(
            directionBox);
        panel.Children.Add(
            xBox);
        panel.Children.Add(
            zBox);

        var lengthBox =
            new NumberBox
            {
                Header =
                    "Comprimento (m)",
                Minimum =
                    0,
                Maximum =
                    1000000,
                Value =
                    10,
                SmallChange =
                    0.25,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Compact
            };

        var rotationBox =
            new NumberBox
            {
                Header =
                    "Rotação",
                Minimum =
                    -1000000,
                Maximum =
                    1000000,
                Value =
                    0,
                SmallChange =
                    1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Compact
            };

        if (
            selection.Kind ==
                PickingKind.Object)
        {
            panel.Children.Add(
                lengthBox);
            panel.Children.Add(
                rotationBox);
        }

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    selection.Kind ==
                        PickingKind.Object
                        ? "Novo path SCO"
                        : "Novo path SLI",
                Content =
                    panel,
                PrimaryButtonText =
                    "Criar path",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        if (
            typeBox.SelectedIndex is
                < 0 or
                > 3 ||
            directionBox.SelectedIndex is
                < 0 or
                > 2 ||
            !double.IsFinite(
                widthBox.Value) ||
            !double.IsFinite(
                xBox.Value) ||
            !double.IsFinite(
                zBox.Value) ||
            widthBox.Value <
                0)
        {
            StatusText.Text =
                "Novo path: parâmetros inválidos.";
            return;
        }

        if (
            selection.Kind ==
                PickingKind.Object &&
            (
                !double.IsFinite(
                    lengthBox.Value) ||
                !double.IsFinite(
                    rotationBox.Value) ||
                lengthBox.Value <
                    0
            ))
        {
            StatusText.Text =
                "Novo path SCO: comprimento/rotação inválidos.";
            return;
        }

        try
        {
            AddAssetPathButton.IsEnabled =
                false;

            var previousCount =
                0;

            string updatedAssetPath;
            string backupPath;

            if (
                selection.Kind ==
                    PickingKind.Object)
            {
                if (
                    !OmsiSceneryObjectPathResolver
                        .TryResolve(
                            root,
                            selection.AssetPath,
                            out var target) ||
                    !File.Exists(
                        target))
                {
                    StatusText.Text =
                        "Novo path SCO: asset não encontrado.";
                    return;
                }

                previousCount =
                    (
                        await new OmsiSceneryObjectReader()
                            .ReadMetadataAsync(
                                target)
                    ).Paths.Count;

                var updated =
                    await _session
                        .AddSceneryPathAsync(
                            selection.AssetPath,
                            new OmsiSceneryPathDefinition(
                                xBox.Value,
                                0,
                                zBox.Value,
                                rotationBox.Value,
                                0,
                                lengthBox.Value,
                                0,
                                0,
                                typeBox.SelectedIndex,
                                widthBox.Value,
                                directionBox.SelectedIndex,
                                0,
                                null,
                                null,
                                false));

                updatedAssetPath =
                    updated.AssetPath;
                backupPath =
                    updated.BackupPath;
            }
            else
            {
                if (
                    !OmsiSplinePathResolver
                        .TryResolve(
                            root,
                            selection.AssetPath,
                            out var target) ||
                    !File.Exists(
                        target))
                {
                    StatusText.Text =
                        "Novo path SLI: asset não encontrado.";
                    return;
                }

                previousCount =
                    (
                        await new OmsiSplineDefinitionReader()
                            .ReadAsync(
                                target)
                    ).Paths.Count;

                var updated =
                    await _session
                        .AddSplinePathAsync(
                            selection.AssetPath,
                            new OmsiSplinePathDefinition(
                                typeBox.SelectedIndex,
                                xBox.Value,
                                zBox.Value,
                                widthBox.Value,
                                directionBox.SelectedIndex));

                updatedAssetPath =
                    updated.AssetPath;
                backupPath =
                    updated.BackupPath;
            }

            await ApplyMapSnapshotAsync(
                snapshot,
                focusActiveTile:
                    false);

            var owner =
                _explorerItems
                    .FirstOrDefault(
                        item =>
                            item.Kind ==
                                selection.Kind &&
                            item.EntityId ==
                                selection.EntityId &&
                            item.TileX ==
                                selection.TileX &&
                            item.TileY ==
                                selection.TileY &&
                            string.Equals(
                                item.AssetPath,
                                selection.AssetPath,
                                StringComparison.OrdinalIgnoreCase));

            if (owner is not null)
            {
                Viewport.SelectExplorerItem(
                    owner,
                    focus:
                        false);

                Viewport
                    .SetTrafficPathFocusedIndex(
                        previousCount);

                if (
                    Viewport
                        .TrafficPathSelectedOnly)
                {
                    Viewport
                        .RefreshTrafficPathDisplay();
                }
            }

            UpdateTrafficPathStatusText();

            StatusText.Text =
                $"Novo path criado como índice {previousCount} · {Path.GetFileName(updatedAssetPath)} · backup {backupPath}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao criar path: {exception.Message}";
        }
        finally
        {
            AddAssetPathButton.IsEnabled =
                _selectionInfo?.Kind is
                    PickingKind.Object or
                    PickingKind.Spline;
        }
    }

    private async void OnDuplicateAssetPathClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo is not
                { } selection ||
            selection.Kind is not
                (
                    PickingKind.Object or
                    PickingKind.Spline
                ) ||
            _session.OmsiRootPath is not
                { } root ||
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Duplicar path: selecione um objeto ou uma spline.";
            return;
        }

        var sourceOrdinal =
            Math.Max(
                0,
                Viewport
                    .TrafficPathFocusedIndex ??
                0);

        try
        {
            DuplicateAssetPathButton.IsEnabled =
                false;

            string updatedAssetPath;
            string backupPath;
            int duplicatedOrdinal;

            if (
                selection.Kind ==
                    PickingKind.Object)
            {
                if (
                    !OmsiSceneryObjectPathResolver
                        .TryResolve(
                            root,
                            selection.AssetPath,
                            out var target) ||
                    !File.Exists(
                        target))
                {
                    StatusText.Text =
                        "Duplicar path SCO: asset não encontrado.";
                    return;
                }

                var metadata =
                    await new OmsiSceneryObjectReader()
                        .ReadMetadataAsync(
                            target);

                if (
                    sourceOrdinal >=
                        metadata.Paths.Count)
                {
                    StatusText.Text =
                        $"Duplicar path SCO: índice {sourceOrdinal} não existe.";
                    return;
                }

                var updated =
                    await _session
                        .DuplicateSceneryPathAsync(
                            selection.AssetPath,
                            sourceOrdinal,
                            metadata.Paths[
                                sourceOrdinal]);

                duplicatedOrdinal =
                    metadata.Paths.Count;

                updatedAssetPath =
                    updated.AssetPath;

                backupPath =
                    updated.BackupPath;
            }
            else
            {
                if (
                    !OmsiSplinePathResolver
                        .TryResolve(
                            root,
                            selection.AssetPath,
                            out var target) ||
                    !File.Exists(
                        target))
                {
                    StatusText.Text =
                        "Duplicar path SLI: asset não encontrado.";
                    return;
                }

                var definition =
                    await new OmsiSplineDefinitionReader()
                        .ReadAsync(
                            target);

                if (
                    sourceOrdinal >=
                        definition.Paths.Count)
                {
                    StatusText.Text =
                        $"Duplicar path SLI: índice {sourceOrdinal} não existe.";
                    return;
                }

                var updated =
                    await _session
                        .DuplicateSplinePathAsync(
                            selection.AssetPath,
                            sourceOrdinal,
                            definition.Paths[
                                sourceOrdinal]);

                duplicatedOrdinal =
                    definition.Paths.Count;

                updatedAssetPath =
                    updated.AssetPath;

                backupPath =
                    updated.BackupPath;
            }

            await ApplyMapSnapshotAsync(
                snapshot,
                focusActiveTile:
                    false);

            var owner =
                _explorerItems
                    .FirstOrDefault(
                        item =>
                            item.Kind ==
                                selection.Kind &&
                            item.EntityId ==
                                selection.EntityId &&
                            item.TileX ==
                                selection.TileX &&
                            item.TileY ==
                                selection.TileY &&
                            string.Equals(
                                item.AssetPath,
                                selection.AssetPath,
                                StringComparison.OrdinalIgnoreCase));

            if (owner is not null)
            {
                Viewport.SelectExplorerItem(
                    owner,
                    focus:
                        false);

                Viewport
                    .SetTrafficPathFocusedIndex(
                        duplicatedOrdinal);

                if (
                    Viewport
                        .TrafficPathSelectedOnly)
                {
                    Viewport
                        .RefreshTrafficPathDisplay();
                }
            }

            UpdateTrafficPathStatusText();

            StatusText.Text =
                $"Path duplicado como índice {duplicatedOrdinal} · {Path.GetFileName(updatedAssetPath)} · backup {backupPath}. Use Editar paths do item para ajustar a nova faixa.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao duplicar path: {exception.Message}";
        }
        finally
        {
            DuplicateAssetPathButton.IsEnabled =
                _selectionInfo?.Kind is
                    PickingKind.Object or
                    PickingKind.Spline;
        }
    }

    private async void OnDeleteAssetPathClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _selectionInfo is not
                { } selection ||
            selection.Kind is not
                (
                    PickingKind.Object or
                    PickingKind.Spline
                ) ||
            _session.OmsiRootPath is not
                { } root ||
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Excluir path: selecione um objeto ou uma spline.";
            return;
        }

        var sourceOrdinal =
            Math.Max(
                0,
                Viewport
                    .TrafficPathFocusedIndex ??
                0);

        try
        {
            DeleteAssetPathButton.IsEnabled =
                false;

            var pathCount =
                0;

            if (
                selection.Kind ==
                    PickingKind.Object)
            {
                if (
                    !OmsiSceneryObjectPathResolver
                        .TryResolve(
                            root,
                            selection.AssetPath,
                            out var target) ||
                    !File.Exists(
                        target))
                {
                    StatusText.Text =
                        "Excluir path SCO: asset não encontrado.";
                    return;
                }

                pathCount =
                    (
                        await new OmsiSceneryObjectReader()
                            .ReadMetadataAsync(
                                target)
                    ).Paths.Count;
            }
            else
            {
                if (
                    !OmsiSplinePathResolver
                        .TryResolve(
                            root,
                            selection.AssetPath,
                            out var target) ||
                    !File.Exists(
                        target))
                {
                    StatusText.Text =
                        "Excluir path SLI: asset não encontrado.";
                    return;
                }

                pathCount =
                    (
                        await new OmsiSplineDefinitionReader()
                            .ReadAsync(
                                target)
                    ).Paths.Count;
            }

            if (
                sourceOrdinal >=
                    pathCount)
            {
                StatusText.Text =
                    $"Excluir path: índice {sourceOrdinal} não existe.";
                return;
            }

            var confirm =
                new ContentDialog
                {
                    XamlRoot =
                        MainRoot.XamlRoot,
                    Title =
                        $"Excluir path {sourceOrdinal}?",
                    Content =
                        new TextBlock
                        {
                            Text =
                                pathCount == 1
                                    ? "Este é o último path do asset. A exclusão removerá toda a circulação definida neste SCO/SLI e afetará todas as instâncias que usam o arquivo. Um backup será criado."
                                    : "A exclusão afeta todas as instâncias que usam este SCO/SLI. Um backup será criado antes da alteração.",
                            TextWrapping =
                                TextWrapping.Wrap
                        },
                    PrimaryButtonText =
                        "Excluir path",
                    CloseButtonText =
                        "Cancelar",
                    DefaultButton =
                        ContentDialogButton.Close
                };

            if (
                await confirm.ShowAdaptiveAsync() !=
                    ContentDialogResult.Primary)
            {
                return;
            }

            StatusText.Text =
                $"Excluindo path {sourceOrdinal} com backup...";

            NativeAssetPathDeleteResult
                updated;

            if (
                selection.Kind ==
                    PickingKind.Object)
            {
                updated =
                    await _session
                        .DeleteSceneryPathAsync(
                            selection.AssetPath,
                            sourceOrdinal);
            }
            else
            {
                updated =
                    await _session
                        .DeleteSplinePathAsync(
                            selection.AssetPath,
                            sourceOrdinal);
            }

            await ApplyMapSnapshotAsync(
                snapshot,
                focusActiveTile:
                    false);

            var owner =
                _explorerItems
                    .FirstOrDefault(
                        item =>
                            item.Kind ==
                                selection.Kind &&
                            item.EntityId ==
                                selection.EntityId &&
                            item.TileX ==
                                selection.TileX &&
                            item.TileY ==
                                selection.TileY &&
                            string.Equals(
                                item.AssetPath,
                                selection.AssetPath,
                                StringComparison.OrdinalIgnoreCase));

            if (owner is not null)
            {
                Viewport.SelectExplorerItem(
                    owner,
                    focus:
                        false);

                Viewport
                    .SetTrafficPathFocusedIndex(
                        updated.RemainingPathCount >
                            0
                            ? Math.Min(
                                sourceOrdinal,
                                updated.RemainingPathCount -
                                    1)
                            : null);

                if (
                    Viewport
                        .TrafficPathSelectedOnly)
                {
                    Viewport
                        .RefreshTrafficPathDisplay();
                }
            }

            UpdateTrafficPathStatusText();

            StatusText.Text =
                $"Path {sourceOrdinal} excluído · {Path.GetFileName(updated.AssetPath)} · {updated.RemainingPathCount} path(s) restante(s) · backup {updated.BackupPath}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao excluir path: {exception.Message}";
        }
        finally
        {
            DeleteAssetPathButton.IsEnabled =
                _selectionInfo?.Kind is
                    PickingKind.Object or
                    PickingKind.Spline;
        }
    }

    private async void OnAddTrafficSignalClick(
        object sender,
        RoutedEventArgs e)
    {
        SetSelectionModeFromShortcut(
            1);

        await ActivateLibraryGroupToolAsync(
            1,
            OmsiAssetLibraryGroup
                .StreetFurniture,
            "Sinalização: escolha um semáforo e coloque-o no mapa.");

        ExplorerSearchBox.Text =
            "traffic";

        RefreshLibraryFilter();
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
                for (
                    var ruleIndex = 0;
                    ruleIndex <
                        item.TrafficRules.Count;
                    ruleIndex++)
                {
                    result.Add(
                        CreateTrafficRuleItem(
                            PickingKind.Spline,
                            item.SplineId,
                            tile.Reference.X,
                            tile.Reference.Y,
                            item.SplinePath,
                            ruleIndex,
                            item.TrafficRules[
                                ruleIndex]));
                }
            }

            foreach (
                var item in
                    tile.Content.Objects)
            {
                for (
                    var ruleIndex = 0;
                    ruleIndex <
                        item.TrafficRules.Count;
                    ruleIndex++)
                {
                    result.Add(
                        CreateTrafficRuleItem(
                            PickingKind.Object,
                            item.ObjectId,
                            tile.Reference.X,
                            tile.Reference.Y,
                            item.SceneryObjectPath,
                            ruleIndex,
                            item.TrafficRules[
                                ruleIndex]));
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
            int ruleIndex,
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
            ruleIndex,
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
        if (
            TrafficViewComboBox is null ||
            TrafficSignalsPanel is null ||
            TrafficRulesPanel is null ||
            EditTrafficProgramButton is null ||
            TrafficProgramListView is null ||
            TrafficPlayButton is null ||
            ExplorerSearchBox is null ||
            TrafficDetailText is null)
        {
            return;
        }

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

        EditTrafficProgramButton.Visibility =
            rules
                ? Visibility.Collapsed
                : TrafficProgramListView.SelectedItem is
                    NativeTrafficLightProgramInfo
                    ? Visibility.Visible
                    : Visibility.Collapsed;

        EditTrafficProgramButton.IsEnabled =
            !rules &&
            TrafficProgramListView.SelectedItem is
                NativeTrafficLightProgramInfo;

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

            DeleteTrafficRuleButton.IsEnabled =
                true;

            if (
                item.Rule.PathIndex is
                    int pathIndex)
            {
                TrafficRulePathIndexBox.Value =
                    pathIndex;
            }

            if (
                item.Rule.NumericValue is
                    double numeric)
            {
                TrafficRuleValueBox.Value =
                    numeric;
            }

            TrafficRuleKillCheckBox.IsChecked =
                item.Rule.IsKillRule;

            if (
                item.Rule.VehicleGroupIndex is
                    int groupIndex &&
                groupIndex >= 0 &&
                groupIndex <
                    _trafficVehicleGroups.Count)
            {
                TrafficVehicleGroupComboBox.SelectedIndex =
                    groupIndex;
            }

            var preset =
                OmsiTrafficRulePresets
                    .TryMatch(
                        item.Rule.RuleName,
                        item.Rule.NumericValue);

            if (preset is not null)
            {
                var presetIndex =
                    OmsiTrafficRulePresets.All
                        .ToList()
                        .FindIndex(
                            candidate =>
                                candidate.Key ==
                                    preset.Key);

                if (presetIndex >= 0)
                {
                    TrafficRulePresetComboBox.SelectedIndex =
                        presetIndex;
                }
            }

            return;
        }

        FocusTrafficRuleOwnerButton.IsEnabled =
            false;

        DeleteTrafficRuleButton.IsEnabled =
            false;
    }


    private void OnTrafficRulePresetSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (
            TrafficRulePresetComboBox
                .SelectedItem is not
                OmsiTrafficRulePreset
                    preset)
        {
            return;
        }

        if (
            preset.FixedValue is
                double fixedValue)
        {
            TrafficRuleValueBox.Value =
                fixedValue;
        }

        TrafficRuleValueBox.IsEnabled =
            preset.RequiresCustomValue;
    }

    private async void OnApplyTrafficRuleClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.CurrentMap is not
                { } snapshot ||
            _session.OmsiRootPath is not
                { } omsiRoot ||
            TrafficRulePresetComboBox
                .SelectedItem is not
                OmsiTrafficRulePreset
                    preset ||
            !double.IsFinite(
                TrafficRulePathIndexBox.Value))
        {
            StatusText.Text =
                "Traffic Rule: selecione uma regra/preset e informe um path válido.";
            return;
        }

        PickingKind ownerKind;
        int entityId;
        int tileX;
        int tileY;
        int replaceIndex;

        if (
            TrafficRuleListView.SelectedItem is
                TrafficRuleExplorerItem
                    selected)
        {
            ownerKind =
                selected.OwnerKind;

            entityId =
                selected.EntityId;

            tileX =
                selected.TileX;

            tileY =
                selected.TileY;

            replaceIndex =
                selected.RuleIndex;
        }
        else if (
            _selectionInfo is
                { } mapSelection &&
            mapSelection.Kind is
                PickingKind.Object or
                PickingKind.Spline)
        {
            ownerKind =
                mapSelection.Kind;

            entityId =
                mapSelection.EntityId;

            tileX =
                mapSelection.TileX;

            tileY =
                mapSelection.TileY;

            replaceIndex =
                -1;
        }
        else
        {
            StatusText.Text =
                "Traffic Rule: selecione uma regra existente ou selecione um objeto/spline no mapa para adicionar.";
            return;
        }

        var currentRules =
            GetOwnerTrafficRules(
                snapshot,
                ownerKind,
                tileX,
                tileY,
                entityId);

        if (currentRules is null)
        {
            StatusText.Text =
                "Traffic Rule: dono não encontrado no tile carregado.";
            return;
        }

        var pathIndex =
            checked(
                (int)Math.Round(
                    TrafficRulePathIndexBox.Value));

        if (pathIndex < 0)
        {
            StatusText.Text =
                "Traffic Rule: path index inválido.";
            return;
        }

        double value;

        if (preset.RequiresCustomValue)
        {
            if (
                !double.IsFinite(
                    TrafficRuleValueBox.Value))
            {
                StatusText.Text =
                    "Traffic Rule: informe um valor numérico.";
                return;
            }

            value =
                TrafficRuleValueBox.Value;
        }
        else
        {
            value =
                preset.FixedValue ??
                (
                    double.IsFinite(
                        TrafficRuleValueBox.Value)
                        ? TrafficRuleValueBox.Value
                        : 0
                );
        }

        var groupIndex =
            Math.Max(
                0,
                TrafficVehicleGroupComboBox
                    .SelectedIndex);

        var newRule =
            new OmsiTrafficRule(
                TrafficRuleKillCheckBox.IsChecked ==
                    true,
                pathIndex,
                preset.SerializedRuleName,
                value.ToString(
                    "G17",
                    CultureInfo.InvariantCulture),
                value,
                groupIndex,
                []);

        var rules =
            currentRules.ToList();

        if (
            replaceIndex >= 0 &&
            replaceIndex <
                rules.Count)
        {
            rules[
                replaceIndex] =
                newRule;
        }
        else
        {
            rules.Add(
                newRule);
        }

        try
        {
            ApplyTrafficRuleButton.IsEnabled =
                false;

            StatusText.Text =
                replaceIndex >= 0
                    ? "Atualizando Traffic Rule com backup..."
                    : "Adicionando Traffic Rule com backup...";

            var updated =
                await _session
                    .UpdateTrafficRulesAsync(
                        ownerKind,
                        tileX,
                        tileY,
                        entityId,
                        rules);

            await Viewport
                .SetMapSnapshotAsync(
                    updated.Snapshot,
                    omsiRoot);

            _trafficRuleItems =
                BuildTrafficRuleItems(
                    updated.Snapshot);

            RefreshTrafficFilter();

            TrafficRuleListView.SelectedItem =
                _trafficRuleItems
                    .FirstOrDefault(
                        item =>
                            item.OwnerKind ==
                                ownerKind &&
                            item.EntityId ==
                                entityId &&
                            item.TileX ==
                                tileX &&
                            item.TileY ==
                                tileY &&
                            item.RuleIndex ==
                                (
                                    replaceIndex >= 0
                                        ? replaceIndex
                                        : rules.Count - 1
                                ));

            StatusText.Text =
                $"Traffic Rule salva · {preset.DisplayName} · path {pathIndex} · backup {updated.BackupPath}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar Traffic Rule: {exception.Message}";
        }
        finally
        {
            ApplyTrafficRuleButton.IsEnabled =
                true;
        }
    }

    private async void OnDeleteTrafficRuleClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            TrafficRuleListView.SelectedItem is not
                TrafficRuleExplorerItem
                    selected ||
            _session.CurrentMap is not
                { } snapshot ||
            _session.OmsiRootPath is not
                { } omsiRoot)
        {
            return;
        }

        var currentRules =
            GetOwnerTrafficRules(
                snapshot,
                selected.OwnerKind,
                selected.TileX,
                selected.TileY,
                selected.EntityId);

        if (
            currentRules is null ||
            selected.RuleIndex <
                0 ||
            selected.RuleIndex >=
                currentRules.Count)
        {
            return;
        }

        var rules =
            currentRules.ToList();

        rules.RemoveAt(
            selected.RuleIndex);

        try
        {
            DeleteTrafficRuleButton.IsEnabled =
                false;

            var updated =
                await _session
                    .UpdateTrafficRulesAsync(
                        selected.OwnerKind,
                        selected.TileX,
                        selected.TileY,
                        selected.EntityId,
                        rules);

            await Viewport
                .SetMapSnapshotAsync(
                    updated.Snapshot,
                    omsiRoot);

            _trafficRuleItems =
                BuildTrafficRuleItems(
                    updated.Snapshot);

            TrafficRuleListView.SelectedItem =
                null;

            RefreshTrafficFilter();

            StatusText.Text =
                $"Traffic Rule excluída · backup {updated.BackupPath}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao excluir Traffic Rule: {exception.Message}";
        }
    }

    private static IReadOnlyList<OmsiTrafficRule>?
        GetOwnerTrafficRules(
            NativeMapSnapshot snapshot,
            PickingKind ownerKind,
            int tileX,
            int tileY,
            int entityId)
    {
        var tile =
            snapshot.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            tileX &&
                        item.Reference.Y ==
                            tileY);

        if (tile is null)
        {
            return null;
        }

        if (
            ownerKind ==
            PickingKind.Spline)
        {
            return tile.Content.Splines
                .FirstOrDefault(
                    item =>
                        item.SplineId ==
                            entityId)
                ?.TrafficRules;
        }

        if (
            ownerKind ==
            PickingKind.Object)
        {
            return tile.Content.Objects
                .FirstOrDefault(
                    item =>
                        item.ObjectId ==
                            entityId)
                ?.TrafficRules;
        }

        return null;
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

            EditTrafficProgramButton.Visibility =
                Visibility.Collapsed;

            EditTrafficProgramButton.IsEnabled =
                false;

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
            $"Controller {program.ControllerIndex} · programa {program.ProgramIndex}\n" +
            $"Programa: {program.ProgramName} · ciclo {duration:F2}s";

        EditTrafficProgramButton.Visibility =
            Visibility.Visible;

        EditTrafficProgramButton.IsEnabled =
            true;

        UpdateTrafficPhasePreview();
    }

    private async void OnEditTrafficProgramClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            TrafficProgramListView
                .SelectedItem is not
                NativeTrafficLightProgramInfo
                    program ||
            _session.CurrentMap is not
                { } snapshot ||
            _session.OmsiRootPath is not
                { } omsiRoot)
        {
            return;
        }

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome do programa",
                Text =
                    program.ProgramName
            };

        var useCycleBox =
            new CheckBox
            {
                Content =
                    "Gravar [traffic_lights_group] / ciclo declarado",
                IsChecked =
                    program
                        .DeclaredCycleDuration
                        .HasValue
            };

        var cycleBox =
            new NumberBox
            {
                Header =
                    "Duração do ciclo (s)",
                Minimum =
                    0,
                Maximum =
                    86400,
                Value =
                    program
                        .DeclaredCycleDuration ??
                    program
                        .EffectiveCycleDuration,
                SmallChange =
                    0.5
            };

        var phasesBox =
            new TextBox
            {
                Header =
                    "Fases · signalCode|duração",
                AcceptsReturn =
                    true,
                TextWrapping =
                    TextWrapping.NoWrap,
                MinWidth =
                    500,
                MinHeight =
                    260,
                FontFamily =
                    new Microsoft.UI.Xaml.Media.FontFamily(
                        "Consolas"),
                Text =
                    string.Join(
                        Environment.NewLine,
                        program.Phases
                            .Select(
                                phase =>
                                    $"{phase.SignalCode}|{phase.Duration.ToString("G17", CultureInfo.InvariantCulture)}"))
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    520
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"Objeto #{program.ObjectId} · {program.AssetPath}\n" +
                    "Atenção: o programa é salvo no SCO e afeta todas as instâncias desse asset.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.8
            });

        panel.Children.Add(
            nameBox);

        panel.Children.Add(
            useCycleBox);

        panel.Children.Add(
            cycleBox);

        panel.Children.Add(
            phasesBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    $"Editar semáforo · {program.ProgramName}",
                Content =
                    new ScrollViewer
                    {
                        Content =
                            panel,
                        MaxHeight =
                            620
                    },
                PrimaryButtonText =
                    "Salvar programa",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        var name =
            nameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                name))
        {
            StatusText.Text =
                "Semáforo não salvo: informe o nome do programa.";
            return;
        }

        var phases =
            new List<
                OmsiTrafficLightPhase>();

        var lineNumber =
            0;

        foreach (
            var rawLine in
                phasesBox.Text
                    .Replace(
                        "\r\n",
                        "\n",
                        StringComparison.Ordinal)
                    .Split('\n'))
        {
            lineNumber++;

            var line =
                rawLine.Trim();

            if (string.IsNullOrWhiteSpace(
                    line))
            {
                continue;
            }

            var parts =
                line.Split(
                    '|');

            if (
                parts.Length !=
                    2 ||
                !int.TryParse(
                    parts[0].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var signalCode) ||
                !double.TryParse(
                    parts[1].Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var duration) ||
                !double.IsFinite(
                    duration) ||
                duration < 0)
            {
                StatusText.Text =
                    $"Semáforo não salvo: fase inválida na linha {lineNumber}.";
                return;
            }

            phases.Add(
                new OmsiTrafficLightPhase(
                    signalCode,
                    duration));
        }

        if (phases.Count == 0)
        {
            StatusText.Text =
                "Semáforo não salvo: mantenha pelo menos uma fase.";
            return;
        }

        double? cycleDuration =
            null;

        if (
            useCycleBox.IsChecked ==
                true)
        {
            if (
                !double.IsFinite(
                    cycleBox.Value) ||
                cycleBox.Value <
                    0)
            {
                StatusText.Text =
                    "Semáforo não salvo: ciclo inválido.";
                return;
            }

            cycleDuration =
                cycleBox.Value;
        }

        try
        {
            EditTrafficProgramButton.IsEnabled =
                false;

            StatusText.Text =
                $"Salvando programa {program.ProgramName} no SCO com backup...";

            var updated =
                await _session
                    .UpdateTrafficLightProgramAsync(
                        program,
                        name,
                        cycleDuration,
                        phases);

            await Viewport
                .SetMapSnapshotAsync(
                    snapshot,
                    omsiRoot);

            _trafficPrograms =
                Viewport
                    .GetTrafficLightPrograms();

            RefreshTrafficFilter();

            var refreshed =
                _trafficPrograms
                    .FirstOrDefault(
                        candidate =>
                            candidate.ObjectId ==
                                program.ObjectId &&
                            candidate.ControllerIndex ==
                                program.ControllerIndex &&
                            candidate.ProgramIndex ==
                                program.ProgramIndex);

            TrafficProgramListView.SelectedItem =
                refreshed;

            TrafficStatusText.Text =
                $"{Viewport.TrafficPathLineCount} linhas de path · " +
                $"{_trafficPrograms.Count} programa(s) de semáforo · " +
                $"{_trafficRuleItems.Count} regra(s) aplicada(s) · " +
                $"{_trafficVehicleGroups.Count} grupo(s) de veículo.";

            StatusText.Text =
                $"Programa {updated.Program.Name} salvo · {updated.Program.Phases.Count} fase(s) · backup {updated.BackupPath}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar semáforo: {exception.Message}";
        }
        finally
        {
            EditTrafficProgramButton.IsEnabled =
                TrafficProgramListView.SelectedItem is
                    NativeTrafficLightProgramInfo &&
                TrafficViewComboBox.SelectedIndex ==
                    0;
        }
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
        SetActiveMapTool(
            ToolTransportButton);

        if (_session.CurrentMap is
            not { } snapshot)
        {
            StatusText.Text =
                "Transporte: abra um mapa primeiro.";

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

        UpdateExplorerModeVisual(
            TransportExplorerModeButton,
            "Tracks, Trips, Station Links e horários");

        TransportStatusText.Text =
            "Lendo TTData...";

        _transportPathsVisible =
            false;

        var transportPathOptions =
            NativeTrafficPathDisplayOptions
                .TransportOverview;

        Viewport
            .SetTrafficPathDisplayOptions(
                transportPathOptions);

        Viewport
            .SetTrafficPathSelectedOnly(
                false);

        TransportPathsSelectedOnlyCheckBox.IsChecked =
            false;

        TrafficPathSelectedOnlyCheckBox.IsChecked =
            false;

        SynchronizeTrafficPathControls(
            transportPathOptions,
            fromTransport: false);

        TransportPathsButton.Content =
            "Mostrar Paths";

        Viewport
            .SetTrafficPathsVisible(
                false);

        TransportRouteStepsListView.ItemsSource =
            null;

        TransportRouteStatusText.Text =
            "Selecione Track, Trip, StationLink ou Line para inspecionar o caminho.";

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
                $"{_timetableCatalog.BrokenTripStationLinkReferenceCount} Trip→StationLink quebrado(s) · " +
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
        _transportViewModel.Load(
            _timetableCatalog,
            TransportKindComboBox
                .SelectedIndex);

        _transportItems =
            _transportViewModel.Items;

        RefreshTransportFilter();

        if (_timetableCatalog is not null)
        {
            _timetableWindow
                ?.SetCatalog(
                    _timetableCatalog);
        }
    }

    private void RefreshTransportFilter()
    {
        TransportListView.ItemsSource =
            _transportViewModel
                .Filter(
                    ExplorerSearchBox.Text);
    }


    private void OnOpenTimetableWindowClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_timetableCatalog is null)
        {
            StatusText.Text =
                "Timetable: nenhum TTData carregado.";

            return;
        }

        if (_timetableWindow is null)
        {
            _timetableWindow =
                new TimetableWindow(
                    _timetableCatalog);

            _timetableWindow.TripRouteRequested +=
                tripName =>
                {
                    SelectTransportWorkspace(
                        1,
                        $"Trip {tripName}: rota selecionada pela janela Timetable.");

                    var selected =
                        _transportItems
                            .FirstOrDefault(
                                candidate =>
                                    candidate.Kind ==
                                        "Trip" &&
                                    string.Equals(
                                        candidate.Key,
                                        tripName,
                                        StringComparison.OrdinalIgnoreCase));

                    if (selected is not null)
                    {
                        TransportListView.SelectedItem =
                            selected;

                        RefreshTransportRouteWorkbench(
                            selected,
                            preview:
                                true);

                        StatusText.Text =
                            $"Timetable: Trip {tripName} focado no Route Studio e no mapa.";
                    }
                };

            _timetableWindow.SaveTripAsync =
                SaveTimetableTripAsync;

            _timetableWindow.SaveLineAsync =
                SaveTimetableLineAsync;

            _timetableWindow.Closed +=
                (
                    _,
                    _
                ) =>
                {
                    _timetableWindow =
                        null;
                };
        }
        else
        {
            _timetableWindow
                .SetCatalog(
                    _timetableCatalog);
        }

        _timetableWindow
            .Activate();

        StatusText.Text =
            "Timetable aberto em janela separada.";
    }

    private void OnTransportStepTracksClick(
        object sender,
        RoutedEventArgs e) =>
        SelectTransportWorkspace(
            0,
            "Tracks: monte o caminho físico clicando as faixas no mapa.");

    private void OnTransportStepStationLinksClick(
        object sender,
        RoutedEventArgs e) =>
        SelectTransportWorkspace(
            3,
            "Station Links: crie trechos reutilizáveis entre duas paradas.");

    private void OnTransportStepTripsClick(
        object sender,
        RoutedEventArgs e) =>
        SelectTransportWorkspace(
            1,
            "Trips: use Track (tipo 1) ou sequência de StationLinks entre paradas (tipo 2).");

    private void OnTransportStepProfilesClick(
        object sender,
        RoutedEventArgs e)
    {
        SelectTransportWorkspace(
            1,
            "Perfis: selecione um Trip e use Editar perfil de tempo.");

        TransportProfilesButton.IsEnabled =
            TransportListView.SelectedItem is
                TransportExplorerItem item &&
            item.Kind == "Trip";
    }

    private void OnTransportStepTimetableClick(
        object sender,
        RoutedEventArgs e) =>
        SelectTransportWorkspace(
            4,
            "Horários: Lines/Tours, partidas, AI Group, prioridade e permissão do jogador.");

    private void OnTransportStepStopsClick(
        object sender,
        RoutedEventArgs e) =>
        SelectTransportWorkspace(
            2,
            "Paradas: cadastre e edite os Bus Stops usados por Trips e Station Links.");

    private void SelectTransportWorkspace(
        int selectedIndex,
        string message)
    {
        if (
            TransportKindComboBox.SelectedIndex !=
                selectedIndex)
        {
            TransportKindComboBox.SelectedIndex =
                selectedIndex;
        }
        else
        {
            RefreshTransportItems();
        }

        TransportRouteStatusText.Text =
            message;
    }

    private void OnTrafficPathIsolationChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (
            sender is not CheckBox source)
        {
            return;
        }

        var selectedOnly =
            source.IsChecked ==
            true;

        TransportPathsSelectedOnlyCheckBox.IsChecked =
            selectedOnly;

        TrafficPathSelectedOnlyCheckBox.IsChecked =
            selectedOnly;

        Viewport
            .SetTrafficPathSelectedOnly(
                selectedOnly);

        UpdateTrafficPathStatusText();

        StatusText.Text =
            selectedOnly
                ? "Paths OMSI: mostrando somente as faixas do objeto/spline selecionado."
                : "Paths OMSI: mostrando as faixas de todos os itens carregados.";
    }

    private void OnTransportPathFilterChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (_syncingTrafficPathControls)
        {
            return;
        }

        var options =
            new NativeTrafficPathDisplayOptions(
                Vehicles:
                    TransportPathsVehiclesCheckBox.IsChecked ==
                    true,
                Pedestrians:
                    TransportPathsPedestriansCheckBox.IsChecked ==
                    true,
                Rails:
                    TransportPathsRailsCheckBox.IsChecked ==
                    true,
                Air:
                    TransportPathsAirCheckBox.IsChecked ==
                    true,
                ShowWidthEdges:
                    TransportPathsWidthCheckBox.IsChecked ==
                    true,
                ShowDirectionArrows:
                    TransportPathsArrowsCheckBox.IsChecked ==
                    true,
                HighlightSignalControlled:
                    TransportPathsSignalsCheckBox.IsChecked ==
                    true,
                ShowNodes:
                    TransportPathsNodesCheckBox.IsChecked ==
                    true,
                ShowTypeLabels:
                    TransportPathsLabelsCheckBox.IsChecked ==
                    true);

        ApplyTrafficPathDisplayOptions(
            options,
            fromTransport: true);
    }

    private void OnTrafficPathModeChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        ApplyTrafficPathModeFromTrafficControls();

    private void OnTrafficPathModeChanged(
        object sender,
        RoutedEventArgs e) =>
        ApplyTrafficPathModeFromTrafficControls();

    private void ApplyTrafficPathModeFromTrafficControls()
    {
        if (
            _syncingTrafficPathControls ||
            TrafficPathModeComboBox is null ||
            TrafficPathWidthCheckBox is null ||
            TrafficPathArrowsCheckBox is null ||
            TrafficPathSignalsCheckBox is null ||
            TrafficPathNodesCheckBox is null ||
            TrafficPathLabelsCheckBox is null ||
            Viewport is null)
        {
            return;
        }

        var mode =
            TrafficPathModeComboBox.SelectedIndex;

        var options =
            new NativeTrafficPathDisplayOptions(
                Vehicles:
                    mode is 0 or 3,
                Pedestrians:
                    mode is 1 or 3,
                Rails:
                    mode is 2 or 3,
                Air:
                    mode == 3,
                ShowWidthEdges:
                    TrafficPathWidthCheckBox.IsChecked ==
                    true,
                ShowDirectionArrows:
                    TrafficPathArrowsCheckBox.IsChecked ==
                    true,
                HighlightSignalControlled:
                    TrafficPathSignalsCheckBox.IsChecked ==
                    true,
                ShowNodes:
                    TrafficPathNodesCheckBox.IsChecked ==
                    true,
                ShowTypeLabels:
                    TrafficPathLabelsCheckBox.IsChecked ==
                    true);

        ApplyTrafficPathDisplayOptions(
            options,
            fromTransport: false);
    }

    private void ApplyTrafficPathDisplayOptions(
        NativeTrafficPathDisplayOptions
            options,
        bool fromTransport)
    {
        Viewport
            .SetTrafficPathDisplayOptions(
                options);

        SynchronizeTrafficPathControls(
            options,
            fromTransport);

        UpdateTrafficPathStatusText();
    }

    private void SynchronizeTrafficPathControls(
        NativeTrafficPathDisplayOptions
            options,
        bool fromTransport)
    {
        _syncingTrafficPathControls =
            true;

        try
        {
            if (!fromTransport)
            {
                TransportPathsVehiclesCheckBox.IsChecked =
                    options.Vehicles;

                TransportPathsPedestriansCheckBox.IsChecked =
                    options.Pedestrians;

                TransportPathsRailsCheckBox.IsChecked =
                    options.Rails;

                TransportPathsAirCheckBox.IsChecked =
                    options.Air;

                TransportPathsWidthCheckBox.IsChecked =
                    options.ShowWidthEdges;

                TransportPathsArrowsCheckBox.IsChecked =
                    options.ShowDirectionArrows;

                TransportPathsNodesCheckBox.IsChecked =
                    options.ShowNodes;

                TransportPathsLabelsCheckBox.IsChecked =
                    options.ShowTypeLabels;

                TransportPathsSignalsCheckBox.IsChecked =
                    options.HighlightSignalControlled;
            }

            if (
                options.Vehicles &&
                !options.Pedestrians &&
                !options.Rails &&
                !options.Air)
            {
                TrafficPathModeComboBox.SelectedIndex =
                    0;
            }
            else if (
                !options.Vehicles &&
                options.Pedestrians &&
                !options.Rails &&
                !options.Air)
            {
                TrafficPathModeComboBox.SelectedIndex =
                    1;
            }
            else if (
                !options.Vehicles &&
                !options.Pedestrians &&
                options.Rails &&
                !options.Air)
            {
                TrafficPathModeComboBox.SelectedIndex =
                    2;
            }
            else
            {
                TrafficPathModeComboBox.SelectedIndex =
                    3;
            }

            TrafficPathWidthCheckBox.IsChecked =
                options.ShowWidthEdges;

            TrafficPathArrowsCheckBox.IsChecked =
                options.ShowDirectionArrows;

            TrafficPathNodesCheckBox.IsChecked =
                options.ShowNodes;

            TrafficPathLabelsCheckBox.IsChecked =
                options.ShowTypeLabels;

            TrafficPathSignalsCheckBox.IsChecked =
                options.HighlightSignalControlled;
        }
        finally
        {
            _syncingTrafficPathControls =
                false;
        }
    }

    private void UpdateTrafficPathStatusText()
    {
        var geometry =
            Viewport
                .TrafficPathGeometry;

        if (geometry is null)
        {
            return;
        }

        var summary =
            $"{geometry.PathCount} paths · " +
            $"{geometry.VehiclePathCount} veículos · " +
            $"{geometry.PedestrianPathCount} pedestres · " +
            $"{geometry.RailPathCount} trilhos · " +
            $"{geometry.AirPathCount} aéreo · " +
            $"{geometry.LineCount} linhas.";

        if (_trafficMode)
        {
            TrafficStatusText.Text =
                summary +
                $" {_trafficPrograms.Count} programa(s) de semáforo · " +
                $"{_trafficRuleItems.Count} regra(s).";
        }

        if (_transportMode)
        {
            TransportRouteStatusText.Text =
                $"Visualização: {summary}";
        }
    }

    private void RefreshTransportPathChoices()
    {
        if (
            TransportPathChoiceComboBox is null ||
            TransportPathIndexBox is null ||
            TransportPathChoiceHintText is null)
        {
            return;
        }

        var choices =
            Viewport
                .GetTrafficPathChoicesForSelection();

        _syncingTransportPathChoice =
            true;

        try
        {
            var previous =
                double.IsFinite(
                    TransportPathIndexBox.Value)
                    ? checked(
                        (int)Math.Round(
                            TransportPathIndexBox.Value))
                    : 0;

            TransportPathChoiceComboBox.ItemsSource =
                choices;

            var selectedIndex =
                choices
                    .Select(
                        (choice, index) =>
                            new
                            {
                                choice.Index,
                                ListIndex =
                                    index
                            })
                    .FirstOrDefault(
                        item =>
                            item.Index ==
                                previous)
                    ?.ListIndex ??
                0;

            TransportPathChoiceComboBox.SelectedIndex =
                choices.Count > 0
                    ? selectedIndex
                    : -1;

            if (
                choices.Count > 0 &&
                TransportPathChoiceComboBox.SelectedItem is
                    NativeTrafficPathChoice choice)
            {
                TransportPathIndexBox.Value =
                    choice.Index;

                TransportPathChoiceHintText.Text =
                    $"{choices.Count} path(s) disponíveis neste item · {choice.KindLabel} {choice.DirectionLabel} · {GetTransportPathMetadataHint(choice)}.";
            }
            else
            {
                TransportPathChoiceHintText.Text =
                    "Este item não possui paths OMSI ou nenhuma spline/objeto está selecionado.";
            }

            UpdateTransportAddSelectionAvailability();
        }
        finally
        {
            _syncingTransportPathChoice =
                false;
        }
    }

    private void UpdateTransportAddSelectionAvailability()
    {
        if (TransportAddSelectionButton is null)
        {
            return;
        }

        if (
            TransportListView.SelectedItem is not
                TransportExplorerItem item)
        {
            TransportAddSelectionButton.IsEnabled =
                false;
            return;
        }

        if (item.Kind == "Track")
        {
            TransportAddSelectionButton.IsEnabled =
                _selectionInfo is
                    {
                        Kind:
                            PickingKind.Object or
                            PickingKind.Spline
                    };
            return;
        }

        if (
            item.Kind !=
                "StationLink" ||
            _selectionInfo is null ||
            TransportPathChoiceComboBox.SelectedItem is not
                NativeTrafficPathChoice choice)
        {
            TransportAddSelectionButton.IsEnabled =
                false;
            return;
        }

        TransportAddSelectionButton.IsEnabled =
            TryCreateStationLinkEntryFromKnownMetadata(
                _selectionInfo.EntityId,
                choice.Index.ToString(
                    CultureInfo.InvariantCulture),
                out _);
    }

    private string GetTransportPathMetadataHint(
        NativeTrafficPathChoice choice)
    {
        if (
            _timetableCatalog is null ||
            _selectionInfo is null)
        {
            return "metadata TTData ainda não verificada";
        }

        return TryCreateStationLinkEntryFromKnownMetadata(
                _selectionInfo.EntityId,
                choice.Index.ToString(
                    CultureInfo.InvariantCulture),
                out _)
            ? "pronto para StationLink"
            : "sem metadata segura para StationLink";
    }

    private void OnTransportPathChoiceSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (
            _syncingTransportPathChoice ||
            TransportPathChoiceComboBox.SelectedItem is not
                NativeTrafficPathChoice choice)
        {
            return;
        }

        TransportPathIndexBox.Value =
            choice.Index;

        if (
            _transportMode &&
            Viewport.TrafficPathSelectedOnly)
        {
            Viewport
                .SetTrafficPathFocusedIndex(
                    choice.Index);
        }

        TransportPathChoiceHintText.Text =
            $"Path {choice.Index} · {choice.KindLabel} · direção {choice.DirectionLabel} · largura {choice.Width:F2} m · {GetTransportPathMetadataHint(choice)}.";

        UpdateTransportAddSelectionAvailability();
    }

    private void OnTransportPreviousLaneClick(
        object sender,
        RoutedEventArgs e) =>
        StepTransportPathChoice(
            -1);

    private void OnTransportNextLaneClick(
        object sender,
        RoutedEventArgs e) =>
        StepTransportPathChoice(
            1);

    private void StepTransportPathChoice(
        int delta)
    {
        if (
            TransportPathChoiceComboBox.Items.Count ==
                0)
        {
            StatusText.Text =
                "Faixas: selecione uma spline/objeto que possua paths OMSI.";
            return;
        }

        var current =
            Math.Max(
                0,
                TransportPathChoiceComboBox.SelectedIndex);

        var next =
            Math.Clamp(
                current +
                    delta,
                0,
                TransportPathChoiceComboBox.Items.Count -
                    1);

        TransportPathChoiceComboBox.SelectedIndex =
            next;
    }

    private void OnTransportKindSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_transportMode)
        {
            return;
        }

        Viewport
            .ClearTimetableRoutePreview();

        TransportRouteStepsListView.ItemsSource =
            null;

        TransportRouteStatusText.Text =
            "Selecione um item para inspecionar o caminho.";

        TransportRemoveStepButton.IsEnabled =
            false;

        TransportMoveStepUpButton.IsEnabled =
            false;

        TransportMoveStepDownButton.IsEnabled =
            false;

        TransportProfilesButton.IsEnabled =
            false;

        RefreshTransportItems();

        TransportNewButton.Content =
            TransportKindComboBox.SelectedIndex switch
            {
                1 => "+ Novo Trip",
                2 => "+ Nova parada",
                3 => "+ Novo StationLink",
                4 => "+ Nova Line",
                _ => "+ Novo Track"
            };
    }

    private async void OnTransportNewClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_timetableCatalog is null)
        {
            StatusText.Text =
                "Transporte: TTData ainda não foi carregado.";
            return;
        }

        try
        {
            switch (
                TransportKindComboBox
                    .SelectedIndex)
            {
                case 1:
                    await CreateTransportTripAsync();
                    break;

                case 2:
                    await CreateTransportStopAsync();
                    break;

                case 3:
                    await CreateTransportStationLinkAsync();
                    break;

                case 4:
                    await CreateTransportLineAsync();
                    break;

                default:
                    await CreateTransportTrackAsync();
                    break;
            }
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao criar item de transporte: {exception.Message}";
        }
    }

    private async Task CreateTransportTrackAsync()
    {
        if (
            _selectionInfo is null ||
            _selectionInfo.Kind is not
                (
                    PickingKind.Object or
                    PickingKind.Spline
                ))
        {
            StatusText.Text =
                "Novo Track: selecione primeiro uma spline ou objeto com path no mapa.";
            return;
        }

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome do Track (.ttr)",
                Text =
                    $"Track_{DateTime.Now:HHmmss}"
            };

        var pathBox =
            new NumberBox
            {
                Header =
                    "Path index do segmento selecionado",
                Minimum =
                    0,
                Maximum =
                    100000,
                Value =
                    Math.Max(
                        0,
                        TransportPathIndexBox.Value)
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    420
            };

        panel.Children.Add(
            nameBox);

        panel.Children.Add(
            pathBox);

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"Segmento inicial: {_selectionInfo.Kind} #{_selectionInfo.EntityId}. Depois use + seleção para montar o restante do caminho visualmente.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.75
            });

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Novo Track / caminho",
                Content =
                    panel,
                PrimaryButtonText =
                    "Criar Track",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        var pathIndex =
            checked(
                (int)Math.Round(
                    pathBox.Value));

        var entry =
            new OmsiTimetableTrackEntry(
                "0:",
                _selectionInfo.EntityId,
                pathIndex.ToString(
                    CultureInfo.InvariantCulture),
                -1,
                string.Empty,
                null,
                string.Empty,
                null);

        var created =
            await _session
                .CreateTimetableTrackAsync(
                    nameBox.Text,
                    [entry]);

        await ReloadTransportCatalogAsync(
            "Track",
            created.Name);

        StatusText.Text =
            $"Track {created.Name} criado com o segmento #{entry.Id}:{entry.Line2}.";
    }

    private async Task CreateTransportTripAsync()
    {
        if (_timetableCatalog is null)
        {
            return;
        }

        var canCreateType1 =
            _timetableCatalog.Tracks.Count >
                0;

        var canCreateType2 =
            _timetableCatalog.BusStops.Count >=
                2 &&
            _timetableCatalog.StationLinks.Count >
                0;

        if (
            !canCreateType1 &&
            !canCreateType2)
        {
            StatusText.Text =
                "Novo Trip: crie um Track (tipo 1) ou ao menos 2 stops conectados por StationLinks (tipo 2).";
            return;
        }

        var routeOptions =
            new List<
                TransportTripRouteOption>();

        if (canCreateType2)
        {
            routeOptions.Add(
                new TransportTripRouteOption(
                    true,
                    "Tipo 2 · StationLinks entre paradas · sem Track",
                    string.Empty));
        }

        routeOptions.AddRange(
            _timetableCatalog.Tracks
                .Select(
                    track =>
                        new TransportTripRouteOption(
                            false,
                            $"Tipo 1 · Track {track.Name}",
                            track.Name)));

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome do Trip (.ttp)",
                Text =
                    $"Trip_{DateTime.Now:HHmmss}"
            };

        var routeBox =
            new ComboBox
            {
                Header =
                    "Tipo de rota",
                ItemsSource =
                    routeOptions,
                DisplayMemberPath =
                    nameof(
                        TransportTripRouteOption.Label),
                SelectedIndex =
                    0,
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var destinationBox =
            new TextBox
            {
                Header =
                    "Destino / letreiro"
            };

        var lineBox =
            new TextBox
            {
                Header =
                    "Linha"
            };

        var stationIdsBox =
            new TextBox
            {
                Header =
                    "Stops em sequência · um ID por linha",
                AcceptsReturn =
                    true,
                MinHeight =
                    140,
                FontFamily =
                    new Microsoft.UI.Xaml.Media.FontFamily(
                        "Consolas"),
                Text =
                    string.Join(
                        Environment.NewLine,
                        _timetableCatalog.BusStops
                            .Take(2)
                            .Select(
                                stop =>
                                    stop.Id.ToString(
                                        CultureInfo.InvariantCulture)))
            };

        var profileBox =
            new TextBox
            {
                Header =
                    "Perfil de tempo OMSI · linhas de Profiles",
                AcceptsReturn =
                    true,
                MinHeight =
                    120,
                FontFamily =
                    new Microsoft.UI.Xaml.Media.FontFamily(
                        "Consolas"),
                Text =
                    "[profile]" +
                    Environment.NewLine +
                    "standard"
            };

        var reverseBox =
            new CheckBox
            {
                Content =
                    "Train reverse"
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    520
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Tipo 1 usa um Track completo. Tipo 2 não usa Track: o Map Studio resolve cada par de stops pela cadeia de StationLinks.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            });

        panel.Children.Add(nameBox);
        panel.Children.Add(routeBox);
        panel.Children.Add(destinationBox);
        panel.Children.Add(lineBox);
        panel.Children.Add(stationIdsBox);
        panel.Children.Add(profileBox);
        panel.Children.Add(reverseBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Novo Trip / rota",
                Content =
                    new ScrollViewer
                    {
                        Content =
                            panel,
                        MaxHeight =
                            650
                    },
                PrimaryButtonText =
                    "Criar Trip",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary ||
            routeBox.SelectedItem is not
                TransportTripRouteOption route)
        {
            return;
        }

        var stationIds =
            stationIdsBox.Text
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Split(
                    '\n',
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions
                        .TrimEntries)
                .Select(
                    value =>
                        int.TryParse(
                            value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var id)
                            ? id
                            : -1)
                .ToArray();

        if (
            stationIds.Length == 0 ||
            stationIds.Any(
                id =>
                    id < 0))
        {
            StatusText.Text =
                "Novo Trip: informe IDs de stops válidos.";
            return;
        }

        if (
            !route.UsesStationLinks &&
            string.IsNullOrWhiteSpace(
                lineBox.Text))
        {
            StatusText.Text =
                "Novo Trip tipo 1: informe a linha para manter a rota associada ao Track.";
            return;
        }

        var stations =
            stationIds
                .Select(
                    id =>
                        (OmsiTimetableTripStation)
                            new OmsiTimetableTripStationType2(
                                id))
                .ToArray();

        if (route.UsesStationLinks)
        {
            var validation =
                OmsiTimetableTripValidator
                    .ValidateType2StationLinks(
                        _timetableCatalog,
                        stations);

            if (!validation.IsValid)
            {
                var message =
                    validation.Failure switch
                    {
                        OmsiTimetableType2TripValidationFailure
                            .InvalidStationSequence =>
                            "use pelo menos dois stops do tipo 2.",

                        OmsiTimetableType2TripValidationFailure
                            .UnknownStop =>
                            $"o stop #{validation.StopId} não existe em Busstops.cfg.",

                        OmsiTimetableType2TripValidationFailure
                            .MissingStationLink =>
                            $"não existe StationLink {validation.StartStopId} → {validation.EndStopId}.",

                        _ =>
                            "sequência de StationLinks inválida."
                    };

                StatusText.Text =
                    $"Novo Trip tipo 2: {message}";
                return;
            }
        }

        var profiles =
            profileBox.Text
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Split(
                    '\n',
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions
                        .TrimEntries);

        var rawTripField1 =
            route.UsesStationLinks
                ? destinationBox.Text
                : route.TrackName;

        var rawTripField2 =
            route.UsesStationLinks
                ? lineBox.Text
                : destinationBox.Text;

        var rawTripField3 =
            route.UsesStationLinks
                ? string.Empty
                : lineBox.Text;

        var created =
            await _session
                .CreateTimetableTripAsync(
                    nameBox.Text,
                    rawTripField1,
                    rawTripField2,
                    rawTripField3,
                    reverseBox.IsChecked ==
                        true,
                    stations,
                    profiles);

        await ReloadTransportCatalogAsync(
            "Trip",
            created.Name);

        StatusText.Text =
            created.UsesStationLinks
                ? $"Trip {created.Name} criado · tipo 2 StationLinks · linha {created.EffectiveLine} · {created.Stations.Count} stop(s)."
                : $"Trip {created.Name} criado · Track {created.EffectiveTrackName} · {created.Stations.Count} stop(s).";
    }


    private async Task CreateTransportStopAsync()
    {
        var defaultId =
            _timetableCatalog?.BusStops
                .Select(
                    stop =>
                        stop.Id)
                .DefaultIfEmpty(
                    0)
                .Max() +
            1 ??
            1;

        var idBox =
            new NumberBox
            {
                Header =
                    "ID da parada",
                Minimum =
                    0,
                Maximum =
                    int.MaxValue,
                Value =
                    defaultId
            };

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome",
                Text =
                    $"Parada {defaultId}"
            };

        var subNameBox =
            new TextBox
            {
                Header =
                    "Subnome"
            };

        var tileBox =
            new NumberBox
            {
                Header =
                    "Tile index OMSI",
                Minimum =
                    -1,
                Maximum =
                    int.MaxValue,
                Value =
                    -1
            };

        var passengersBox =
            new NumberBox
            {
                Header =
                    "Passageiros saindo",
                Minimum =
                    0,
                Maximum =
                    100000,
                Value =
                    0
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    420
            };

        panel.Children.Add(idBox);
        panel.Children.Add(nameBox);
        panel.Children.Add(subNameBox);
        panel.Children.Add(tileBox);
        panel.Children.Add(passengersBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Nova parada",
                Content =
                    panel,
                PrimaryButtonText =
                    "Criar parada",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        var stop =
            new OmsiTimetableBusStop(
                nameBox.Text.Trim(),
                checked(
                    (int)Math.Round(
                        tileBox.Value)),
                checked(
                    (int)Math.Round(
                        idBox.Value)),
                passengersBox.Value,
                "0",
                "0",
                subNameBox.Text.Trim());

        await _session
            .AddBusStopAsync(
                stop);

        await ReloadTransportCatalogAsync(
            "Stop",
            stop.Id.ToString(
                CultureInfo.InvariantCulture));

        StatusText.Text =
            $"Parada #{stop.Id} · {stop.Name} criada.";
    }

    private async Task CreateTransportStationLinkAsync()
    {
        if (
            _timetableCatalog is null ||
            _timetableCatalog.BusStops.Count <
                2)
        {
            StatusText.Text =
                "Novo StationLink: são necessários ao menos 2 stops.";
            return;
        }

        var sourceOptions =
            new List<
                TransportStationLinkSourceOption>();

        var selectedPathIndex =
            checked(
                (int)Math.Round(
                    Math.Max(
                        0,
                        TransportPathIndexBox.Value)));

        if (
            _selectionInfo is
                {
                    Kind:
                        PickingKind.Object or
                        PickingKind.Spline
                } selectedInfo &&
            TryCreateStationLinkEntryFromKnownMetadata(
                selectedInfo.EntityId,
                selectedPathIndex.ToString(
                    CultureInfo.InvariantCulture),
                out _))
        {
            sourceOptions.Add(
                new TransportStationLinkSourceOption(
                    "Selection",
                    $"{selectedInfo.EntityId}|{selectedPathIndex}",
                    $"Seleção atual · {selectedInfo.Kind} #{selectedInfo.EntityId} · path {selectedPathIndex}"));
        }

        sourceOptions.AddRange(
            _timetableCatalog.Tracks
                .Where(
                    track =>
                        track.Entries.Count >
                            0)
                .Select(
                    track =>
                        new TransportStationLinkSourceOption(
                            "Track",
                            track.Name,
                            $"Track · {track.Name} · {track.Entries.Count} segmento(s)")));

        sourceOptions.AddRange(
            _timetableCatalog.StationLinks
                .Select(
                    (link, index) =>
                        (
                            Link: link,
                            Index: index
                        ))
                .Where(
                    item =>
                        item.Link.Entries.Count >
                            0)
                .Select(
                    item =>
                        new TransportStationLinkSourceOption(
                            "StationLink",
                            item.Index.ToString(
                                CultureInfo.InvariantCulture),
                            $"StationLink · {item.Link.StartBusStopId} → {item.Link.EndBusStopId} · #{item.Index} · {item.Link.Entries.Count} segmento(s)")));

        if (sourceOptions.Count == 0)
        {
            StatusText.Text =
                "Novo StationLink: não há Track nem StationLink existente com metadata TTData segura para usar como caminho inicial.";
            return;
        }

        var stopOptions =
            _timetableCatalog.BusStops
                .Select(
                    stop =>
                        $"{stop.Id} · {stop.Name}")
                .ToArray();

        var startBox =
            new ComboBox
            {
                Header =
                    "Stop inicial",
                ItemsSource =
                    stopOptions,
                SelectedIndex =
                    0,
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var endBox =
            new ComboBox
            {
                Header =
                    "Stop final",
                ItemsSource =
                    stopOptions,
                SelectedIndex =
                    Math.Min(
                        1,
                        stopOptions.Length -
                            1),
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var sourceBox =
            new ComboBox
            {
                Header =
                    "Fonte inicial do caminho",
                ItemsSource =
                    sourceOptions,
                DisplayMemberPath =
                    nameof(
                        TransportStationLinkSourceOption.Label),
                SelectedIndex =
                    0,
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var commentBox =
            new TextBox
            {
                Header =
                    "Nome / comentário",
                Text =
                    "StationLink Map Studio"
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    500
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "O caminho inicial reutiliza metadata real já existente em TTData. Se o path atualmente selecionado no mapa já for conhecido por Track/StationLink, ele aparece como fonte direta. Depois refine no Route Studio com + seleção, Gravar caminho, mover e remover.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            });

        panel.Children.Add(startBox);
        panel.Children.Add(endBox);
        panel.Children.Add(sourceBox);
        panel.Children.Add(commentBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Novo StationLink",
                Content =
                    panel,
                PrimaryButtonText =
                    "Criar StationLink",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary ||
            sourceBox.SelectedItem is not
                TransportStationLinkSourceOption source)
        {
            return;
        }

        var startIndex =
            startBox.SelectedIndex;

        var endIndex =
            endBox.SelectedIndex;

        if (
            startIndex < 0 ||
            endIndex < 0 ||
            startIndex == endIndex)
        {
            StatusText.Text =
                "StationLink: escolha stops inicial e final diferentes.";
            return;
        }

        var startStop =
            _timetableCatalog.BusStops[
                startIndex];

        var endStop =
            _timetableCatalog.BusStops[
                endIndex];

        OmsiStationLinkEntry[] entries;

        if (
            source.Kind ==
                "Selection")
        {
            var parts =
                source.Key.Split(
                    '|',
                    StringSplitOptions.TrimEntries);

            if (
                parts.Length !=
                    2 ||
                !int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var entityId) ||
                !int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var selectedSourcePathIndex) ||
                !TryCreateStationLinkEntryFromKnownMetadata(
                    entityId,
                    selectedSourcePathIndex.ToString(
                        CultureInfo.InvariantCulture),
                    out var selectedEntry))
            {
                StatusText.Text =
                    "StationLink: a seleção atual perdeu os metadados TTData necessários.";
                return;
            }

            entries =
                [
                    selectedEntry with
                    {
                        Comment =
                            "0:",
                        ChronoFiles =
                            Array.Empty<string>()
                    }
                ];
        }
        else if (source.Kind == "Track")
        {
            var track =
                _timetableCatalog.Tracks
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                candidate.Name,
                                source.Key,
                                StringComparison.OrdinalIgnoreCase));

            if (track is null)
            {
                StatusText.Text =
                    "StationLink: o Track usado como fonte não está mais disponível.";
                return;
            }

            entries =
                track.Entries
                    .Select(
                        (entry, index) =>
                            new OmsiStationLinkEntry(
                                $"{index}:",
                                entry.Id,
                                entry.Line2,
                                entry.TileIndex,
                                entry.Length,
                                entry.Line4,
                                entry.Line6,
                                entry.Line7 ??
                                    string.Empty,
                                Array.Empty<string>()))
                    .ToArray();
        }
        else if (
            source.Kind ==
                "StationLink" &&
            int.TryParse(
                source.Key,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sourceLinkIndex) &&
            sourceLinkIndex >= 0 &&
            sourceLinkIndex <
                _timetableCatalog
                    .StationLinks.Count)
        {
            entries =
                _timetableCatalog
                    .StationLinks[
                        sourceLinkIndex]
                    .Entries
                    .Select(
                        (entry, index) =>
                            entry with
                            {
                                Comment =
                                    $"{index}:",
                                ChronoFiles =
                                    Array.Empty<string>()
                            })
                    .ToArray();
        }
        else
        {
            StatusText.Text =
                "StationLink: fonte inicial inválida.";
            return;
        }

        if (entries.Length == 0)
        {
            StatusText.Text =
                "StationLink: a fonte escolhida não possui segmentos.";
            return;
        }

        var link =
            new OmsiStationLink(
                commentBox.Text.Trim(),
                "0",
                startStop.Id,
                endStop.Id,
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                entries);

        var links =
            await _session
                .AddStationLinkAsync(
                    link);

        await ReloadTransportCatalogAsync(
            "StationLink",
            (links.Count - 1)
                .ToString(
                    CultureInfo.InvariantCulture));

        StatusText.Text =
            $"StationLink {startStop.Name} → {endStop.Name} criado com {entries.Length} segmento(s) · fonte {source.Label}.";
    }

    private async Task CreateTransportLineAsync()
    {
        if (
            _timetableCatalog is null ||
            _timetableCatalog.Trips.Count ==
                0)
        {
            StatusText.Text =
                "Nova Line: crie pelo menos um Trip primeiro.";
            return;
        }

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome da Line (.ttl)",
                Text =
                    $"Line_{DateTime.Now:HHmmss}"
            };

        var tripBox =
            new ComboBox
            {
                Header =
                    "Trip inicial",
                ItemsSource =
                    _timetableCatalog.Trips
                        .Select(
                            trip =>
                                trip.Name)
                        .ToArray(),
                SelectedIndex =
                    0,
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var tourBox =
            new TextBox
            {
                Header =
                    "Nome do Tour",
                Text =
                    "Tour 1"
            };

        var aiGroupBox =
            new TextBox
            {
                Header =
                    "AI Group",
                Text =
                    "Busses"
            };

        var departureBox =
            new TextBox
            {
                Header =
                    "Departure time OMSI (segundos)",
                Text =
                    "28800"
            };

        var priorityBox =
            new TextBox
            {
                Header =
                    "Priority",
                Text =
                    "2"
            };

        var userAllowed =
            new CheckBox
            {
                Content =
                    "User allowed",
                IsChecked =
                    true
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    460
            };

        panel.Children.Add(nameBox);
        panel.Children.Add(tripBox);
        panel.Children.Add(tourBox);
        panel.Children.Add(aiGroupBox);
        panel.Children.Add(departureBox);
        panel.Children.Add(priorityBox);
        panel.Children.Add(userAllowed);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Nova Line / Tour",
                Content =
                    panel,
                PrimaryButtonText =
                    "Criar Line",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary ||
            tripBox.SelectedItem is not
                string tripName)
        {
            return;
        }

        if (
            !double.TryParse(
                departureBox.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var departure) ||
            !double.IsFinite(
                departure) ||
            departure < 0)
        {
            StatusText.Text =
                "Nova Line: departure time inválido.";
            return;
        }

        var tour =
            new OmsiTimetableTour(
                tourBox.Text.Trim(),
                aiGroupBox.Text.Trim(),
                "0",
                [
                    new OmsiTimetableAddTrip(
                        "Created with OMSI Map Studio",
                        tripName,
                        "0",
                        departure.ToString(
                            "G17",
                            CultureInfo.InvariantCulture))
                ]);

        var created =
            await _session
                .CreateTimetableLineAsync(
                    nameBox.Text,
                    priorityBox.Text,
                    userAllowed.IsChecked ==
                        true,
                    [tour]);

        await ReloadTransportCatalogAsync(
            "Line",
            created.Name);

        StatusText.Text =
            $"Line {created.Name} criada · Tour {tour.Name} · Trip {tripName}.";
    }

    private void OnTransportRecordClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            TransportListView.SelectedItem is not
                TransportExplorerItem item ||
            item.Kind is not
                (
                    "Track" or
                    "StationLink"
                ))
        {
            SetTransportTrackRecordMode(
                false);

            StatusText.Text =
                "Gravar caminho: selecione um Track ou StationLink.";
            return;
        }

        SetTransportTrackRecordMode(
            !_transportTrackRecordMode);

        StatusText.Text =
            _transportTrackRecordMode
                ? $"Gravação de caminho ativa em {item.Kind} {item.Key}: clique as splines/objetos na ordem da rota."
                : $"Gravação de caminho encerrada em {item.Kind} {item.Key}.";
    }

    private void SetTransportTrackRecordMode(
        bool enabled)
    {
        _transportTrackRecordMode =
            enabled;

        TransportRecordButton.Content =
            enabled
                ? "■ Encerrar gravação"
                : "Gravar caminho";

        TransportRecordButton.IsEnabled =
            TransportListView.SelectedItem is
                TransportExplorerItem item &&
            item.Kind is
                "Track" or
                "StationLink";

        TransportPathIndexBox.IsEnabled =
            !enabled;

        if (!enabled)
        {
            _transportTrackRecordBusy =
                false;
        }
    }

    private async Task AppendTransportSelectionToTrackAsync(
        NativeSelectionInfo info)
    {
        if (
            !_transportTrackRecordMode ||
            _transportTrackRecordBusy ||
            _timetableCatalog is null ||
            TransportListView.SelectedItem is not
                TransportExplorerItem item ||
            item.Kind is not
                (
                    "Track" or
                    "StationLink"
                ))
        {
            return;
        }

        var pathIndex =
            checked(
                (int)Math.Round(
                    Math.Max(
                        0,
                        TransportPathIndexBox.Value)));

        var pathIndexText =
            pathIndex.ToString(
                CultureInfo.InvariantCulture);

        _transportTrackRecordBusy =
            true;

        try
        {
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

                if (track is null)
                {
                    return;
                }

                if (
                    track.Entries.Count >
                        0 &&
                    track.Entries[^1].Id ==
                        info.EntityId &&
                    string.Equals(
                        track.Entries[^1].Line2,
                        pathIndexText,
                        StringComparison.Ordinal))
                {
                    StatusText.Text =
                        $"Gravar caminho: #{info.EntityId}:{pathIndex} já é o último segmento.";
                    return;
                }

                var entries =
                    track.Entries
                        .ToList();

                entries.Add(
                    new OmsiTimetableTrackEntry(
                        $"{entries.Count}:",
                        info.EntityId,
                        pathIndexText,
                        -1,
                        string.Empty,
                        null,
                        string.Empty,
                        null));

                await _session
                    .UpdateTimetableTrackAsync(
                        track,
                        entries);

                await ReloadTransportCatalogAsync(
                    "Track",
                    track.Name);

                TransportRouteStepsListView.SelectedIndex =
                    entries.Count -
                    1;

                SetTransportTrackRecordMode(
                    true);

                StatusText.Text =
                    $"Gravar caminho · Track {track.Name}: #{info.EntityId}:{pathIndex} adicionado · {entries.Count} segmento(s).";

                return;
            }

            if (
                !int.TryParse(
                    item.Key,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var linkIndex) ||
                linkIndex < 0 ||
                linkIndex >=
                    _timetableCatalog
                        .StationLinks.Count)
            {
                return;
            }

            var link =
                _timetableCatalog
                    .StationLinks[
                        linkIndex];

            if (
                link.Entries.Count >
                    0 &&
                link.Entries[^1].Id ==
                    info.EntityId &&
                string.Equals(
                    link.Entries[^1].Line2,
                    pathIndexText,
                    StringComparison.Ordinal))
            {
                StatusText.Text =
                    $"Gravar caminho: #{info.EntityId}:{pathIndex} já é o último segmento do StationLink.";
                return;
            }

            if (
                !TryCreateStationLinkEntryFromKnownMetadata(
                    info.EntityId,
                    pathIndexText,
                    out var template))
            {
                StatusText.Text =
                    $"Gravar StationLink: #{info.EntityId}:{pathIndex} não tem metadados TTData seguros conhecidos; trecho ignorado.";
                return;
            }

            var linkEntries =
                link.Entries
                    .ToList();

            linkEntries.Add(
                template with
                {
                    Comment =
                        $"{linkEntries.Count}:",
                    ChronoFiles =
                        Array.Empty<string>()
                });

            await _session
                .UpdateStationLinkAsync(
                    linkIndex,
                    link with
                    {
                        Entries =
                            linkEntries
                    });

            await ReloadTransportCatalogAsync(
                "StationLink",
                linkIndex.ToString(
                    CultureInfo.InvariantCulture));

            TransportRouteStepsListView.SelectedIndex =
                linkEntries.Count -
                    1;

            SetTransportTrackRecordMode(
                true);

            StatusText.Text =
                $"Gravar caminho · StationLink {link.StartBusStopId} → {link.EndBusStopId}: #{info.EntityId}:{pathIndex} adicionado · {linkEntries.Count} segmento(s).";
        }
        catch (Exception exception)
        {
            SetTransportTrackRecordMode(
                false);

            StatusText.Text =
                $"Gravação de caminho interrompida: {exception.Message}";
        }
        finally
        {
            _transportTrackRecordBusy =
                false;
        }
    }

    private async void OnTransportAddSelectionClick(
        object sender,
        RoutedEventArgs e)
    {
        SetTransportTrackRecordMode(
            false);

        if (
            _timetableCatalog is null ||
            TransportListView.SelectedItem is not
                TransportExplorerItem item ||
            item.Kind is not
                (
                    "Track" or
                    "StationLink"
                ) ||
            _selectionInfo is null ||
            _selectionInfo.Kind is not
                (
                    PickingKind.Object or
                    PickingKind.Spline
                ))
        {
            StatusText.Text =
                "Route Studio: selecione um Track/StationLink e uma spline/objeto no mapa.";
            return;
        }

        var pathIndex =
            checked(
                (int)Math.Round(
                    TransportPathIndexBox.Value));

        var pathIndexText =
            pathIndex.ToString(
                CultureInfo.InvariantCulture);

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

            if (track is null)
            {
                return;
            }

            var entries =
                track.Entries
                    .ToList();

            entries.Add(
                new OmsiTimetableTrackEntry(
                    $"{entries.Count}:",
                    _selectionInfo.EntityId,
                    pathIndexText,
                    -1,
                    string.Empty,
                    null,
                    string.Empty,
                    null));

            try
            {
                await _session
                    .UpdateTimetableTrackAsync(
                        track,
                        entries);

                await ReloadTransportCatalogAsync(
                    "Track",
                    track.Name);

                TransportRouteStepsListView.SelectedIndex =
                    entries.Count -
                    1;

                StatusText.Text =
                    $"Track {track.Name}: segmento #{_selectionInfo.EntityId}:{pathIndex} adicionado.";
            }
            catch (Exception exception)
            {
                StatusText.Text =
                    $"Falha ao adicionar segmento ao Track: {exception.Message}";
            }

            return;
        }

        if (
            !int.TryParse(
                item.Key,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var linkIndex) ||
            linkIndex < 0 ||
            linkIndex >=
                _timetableCatalog
                    .StationLinks.Count)
        {
            return;
        }

        if (
            !TryCreateStationLinkEntryFromKnownMetadata(
                _selectionInfo.EntityId,
                pathIndexText,
                out var template))
        {
            StatusText.Text =
                $"StationLink: #{_selectionInfo.EntityId}:{pathIndex} não possui tile/length/metadados seguros em Tracks ou StationLinks carregados; nada foi gravado.";
            return;
        }

        var link =
            _timetableCatalog
                .StationLinks[
                    linkIndex];

        var linkEntries =
            link.Entries
                .ToList();

        linkEntries.Add(
            template with
            {
                Comment =
                    $"{linkEntries.Count}:",
                ChronoFiles =
                    Array.Empty<string>()
            });

        try
        {
            await _session
                .UpdateStationLinkAsync(
                    linkIndex,
                    link with
                    {
                        Entries =
                            linkEntries
                    });

            await ReloadTransportCatalogAsync(
                "StationLink",
                linkIndex.ToString(
                    CultureInfo.InvariantCulture));

            TransportRouteStepsListView.SelectedIndex =
                linkEntries.Count -
                    1;

            StatusText.Text =
                $"StationLink {link.StartBusStopId} → {link.EndBusStopId}: segmento #{_selectionInfo.EntityId}:{pathIndex} adicionado com metadados TTData existentes.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao adicionar segmento ao StationLink: {exception.Message}";
        }
    }

    private bool TryCreateStationLinkEntryFromKnownMetadata(
        int entityId,
        string pathIndex,
        out OmsiStationLinkEntry entry)
    {
        entry =
            null!;

        return
            _timetableCatalog is not null &&
            OmsiStationLinkEntryResolver
                .TryCreateFromKnownMetadata(
                    _timetableCatalog,
                    entityId,
                    pathIndex,
                    out entry);
    }

    private bool TryValidateStationLinkTrip(
        IReadOnlyList<
            OmsiTimetableTripStation> stations,
        out string error)
    {
        error =
            string.Empty;

        if (_timetableCatalog is null)
        {
            error =
                "TTData não está carregado.";
            return false;
        }

        var validation =
            OmsiTimetableTripValidator
                .ValidateType2StationLinks(
                    _timetableCatalog,
                    stations);

        if (validation.IsValid)
        {
            return true;
        }

        error =
            validation.Failure switch
            {
                OmsiTimetableType2TripValidationFailure
                    .InvalidStationSequence =>
                    "use pelo menos dois stops do tipo 2.",

                OmsiTimetableType2TripValidationFailure
                    .UnknownStop =>
                    $"o stop #{validation.StopId} não existe em Busstops.cfg.",

                OmsiTimetableType2TripValidationFailure
                    .MissingStationLink =>
                    $"não existe StationLink {validation.StartStopId} → {validation.EndStopId}.",

                _ =>
                    "sequência de StationLinks inválida."
            };

        return false;
    }

    private async void OnTransportRouteDragItemsCompleted(
        ListViewBase sender,
        DragItemsCompletedEventArgs args)
    {
        if (
            args.Items.FirstOrDefault() is not
                TransportRouteStepItem step ||
            TransportListView.SelectedItem is not
                TransportExplorerItem item ||
            item.Kind is not
                ("Track" or "StationLink"))
        {
            return;
        }

        var oldIndex =
            step.Sequence -
            1;

        var newIndex =
            sender.Items.IndexOf(
                step);

        if (
            newIndex < 0 ||
            newIndex == oldIndex)
        {
            return;
        }

        sender.SelectedItem =
            step;

        await MutateSelectedRouteStepAsync(
            moveDelta:
                newIndex -
                oldIndex,
            remove: false);

        StatusText.Text =
            $"{item.Kind}: trecho movido de {oldIndex + 1} para {newIndex + 1}.";
    }

    private async void OnTransportRemoveStepClick(
        object sender,
        RoutedEventArgs e) =>
        await MutateSelectedRouteStepAsync(
            moveDelta: 0,
            remove: true);

    private async void OnTransportMoveStepUpClick(
        object sender,
        RoutedEventArgs e) =>
        await MutateSelectedRouteStepAsync(
            moveDelta: -1,
            remove: false);

    private async void OnTransportMoveStepDownClick(
        object sender,
        RoutedEventArgs e) =>
        await MutateSelectedRouteStepAsync(
            moveDelta: 1,
            remove: false);

    private async Task MutateSelectedRouteStepAsync(
        int moveDelta,
        bool remove)
    {
        if (
            _timetableCatalog is null ||
            TransportListView.SelectedItem is not
                TransportExplorerItem item ||
            item.Kind is not
                (
                    "Track" or
                    "StationLink"
                ) ||
            TransportRouteStepsListView.SelectedItem is not
                TransportRouteStepItem step)
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

            if (track is null)
            {
                return;
            }

            var entries =
                track.Entries
                    .ToList();

            var index =
                step.Sequence -
                1;

            if (
                index < 0 ||
                index >= entries.Count)
            {
                return;
            }

            var nextSelection =
                index;

            if (remove)
            {
                if (entries.Count <= 1)
                {
                    StatusText.Text =
                        "Track precisa manter ao menos um segmento.";
                    return;
                }

                entries.RemoveAt(
                    index);

                nextSelection =
                    Math.Min(
                        index,
                        entries.Count -
                            1);
            }
            else
            {
                var target =
                    index +
                    moveDelta;

                if (
                    target < 0 ||
                    target >= entries.Count)
                {
                    return;
                }

                var movedEntry =
                    entries[index];

                entries.RemoveAt(
                    index);

                entries.Insert(
                    target,
                    movedEntry);

                nextSelection =
                    target;
            }

            try
            {
                await _session
                    .UpdateTimetableTrackAsync(
                        track,
                        entries);

                await ReloadTransportCatalogAsync(
                    "Track",
                    track.Name);

                TransportRouteStepsListView.SelectedIndex =
                    nextSelection;

                StatusText.Text =
                    remove
                        ? $"Track {track.Name}: segmento removido."
                        : $"Track {track.Name}: ordem dos segmentos atualizada.";
            }
            catch (Exception exception)
            {
                StatusText.Text =
                    $"Falha ao alterar Track: {exception.Message}";
            }

            return;
        }

        if (
            !int.TryParse(
                item.Key,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var linkIndex) ||
            linkIndex < 0 ||
            linkIndex >=
                _timetableCatalog
                    .StationLinks.Count)
        {
            return;
        }

        var link =
            _timetableCatalog
                .StationLinks[
                    linkIndex];

        var linkEntries =
            link.Entries
                .ToList();

        var linkEntryIndex =
            step.Sequence -
            1;

        if (
            linkEntryIndex < 0 ||
            linkEntryIndex >=
                linkEntries.Count)
        {
            return;
        }

        var nextLinkSelection =
            linkEntryIndex;

        if (remove)
        {
            if (linkEntries.Count <= 1)
            {
                StatusText.Text =
                    "StationLink precisa manter pelo menos 1 segmento.";
                return;
            }

            linkEntries.RemoveAt(
                linkEntryIndex);

            nextLinkSelection =
                Math.Min(
                    linkEntryIndex,
                    linkEntries.Count -
                        1);
        }
        else
        {
            var target =
                linkEntryIndex +
                moveDelta;

            if (
                target < 0 ||
                target >=
                    linkEntries.Count)
            {
                return;
            }

            var movedEntry =
                linkEntries[
                    linkEntryIndex];

            linkEntries.RemoveAt(
                linkEntryIndex);

            linkEntries.Insert(
                target,
                movedEntry);

            nextLinkSelection =
                target;
        }

        try
        {
            var updated =
                link with
                {
                    Entries =
                        linkEntries
                };

            await _session
                .UpdateStationLinkAsync(
                    linkIndex,
                    updated);

            await ReloadTransportCatalogAsync(
                "StationLink",
                linkIndex.ToString(
                    CultureInfo.InvariantCulture));

            TransportRouteStepsListView.SelectedIndex =
                nextLinkSelection;

            StatusText.Text =
                remove
                    ? $"StationLink {link.StartBusStopId} → {link.EndBusStopId}: segmento removido."
                    : $"StationLink {link.StartBusStopId} → {link.EndBusStopId}: ordem dos segmentos atualizada.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao alterar StationLink: {exception.Message}";
        }
    }

    private async void OnTransportProfilesClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _timetableCatalog is null ||
            TransportListView.SelectedItem is not
                TransportExplorerItem item ||
            item.Kind !=
                "Trip")
        {
            return;
        }

        var trip =
            _timetableCatalog.Trips
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            item.Key,
                            StringComparison.OrdinalIgnoreCase));

        if (trip is null)
        {
            return;
        }

        var updatedTrip =
            await TimetableProfileEditorDialog
                .ShowAsync(
                    MainRoot.XamlRoot,
                    trip,
                    _timetableCatalog
                        .BusStops);

        if (updatedTrip is null)
        {
            return;
        }

        await SaveTimetableTripProfilesAsync(
            trip,
            updatedTrip);
    }

    private async Task SaveTimetableTripProfilesAsync(
        OmsiTimetableTrip sourceTrip,
        OmsiTimetableTrip updatedTrip)
    {
        if (
            await SaveTimetableTripAsync(
                sourceTrip,
                updatedTrip))
        {
            StatusText.Text =
                $"Trip {updatedTrip.Name}: {OmsiTimetableProfileEditor.ReadProfiles(updatedTrip.ProfileLines).Count} perfil(is) de tempo salvo(s).";
        }
    }

    private async Task<bool> SaveTimetableTripAsync(
        OmsiTimetableTrip sourceTrip,
        OmsiTimetableTrip updatedTrip)
    {
        try
        {
            EditTrackButton.IsEnabled =
                false;

            StatusText.Text =
                $"Salvando Trip {sourceTrip.Name} com backup...";

            var updated =
                await _session
                    .UpdateTimetableTripAsync(
                        sourceTrip,
                        updatedTrip);

            await ReloadTransportCatalogAsync(
                "Trip",
                updated.Trip.Name);

            if (
                TransportListView.SelectedItem is
                    TransportExplorerItem selected)
            {
                RefreshTransportRouteWorkbench(
                    selected,
                    preview:
                        true);
            }

            StatusText.Text =
                updated.Trip.UsesStationLinks
                    ? $"Trip {updated.Trip.Name} salvo · tipo 2 StationLinks · {updated.Trip.Stations.Count} stop(s) · backup {updated.BackupPath}."
                    : $"Trip {updated.Trip.Name} salvo · Track {updated.Trip.EffectiveTrackName} · {updated.Trip.Stations.Count} station(s) · backup {updated.BackupPath}.";

            return true;
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar Trip: {exception.Message}";
            return false;
        }
        finally
        {
            EditTrackButton.IsEnabled =
                TransportListView.SelectedItem is
                    TransportExplorerItem selected &&
                selected.Kind is
                    "Track" or
                    "Trip" or
                    "Stop" or
                    "StationLink" or
                    "Line";
        }
    }

    private async Task ReloadTransportCatalogAsync(
        string kind,
        string key)
    {
        if (_session.CurrentMap is null)
        {
            return;
        }

        _timetableCatalog =
            await new OmsiTimetableCatalogReader()
                .ReadAsync(
                    _session.CurrentMap
                        .Map
                        .DirectoryPath);

        RefreshTransportItems();

        TransportListView.SelectedItem =
            _transportItems
                .FirstOrDefault(
                    candidate =>
                        candidate.Kind ==
                            kind &&
                        string.Equals(
                            candidate.Key,
                            key,
                            StringComparison.OrdinalIgnoreCase));
    }

    private async void OnEditTrackClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _timetableCatalog is null ||
            TransportListView.SelectedItem is not
                TransportExplorerItem item)
        {
            return;
        }

        if (item.Kind == "Trip")
        {
            await EditTripAsync(
                item);
            return;
        }

        if (item.Kind == "Line")
        {
            await EditTimetableLineAsync(
                item);
            return;
        }

        if (item.Kind == "Stop")
        {
            await EditBusStopAsync(
                item);
            return;
        }

        if (item.Kind == "StationLink")
        {
            await EditStationLinkAsync(
                item);
            return;
        }

        if (item.Kind != "Track")
        {
            return;
        }

        var track =
            _timetableCatalog.Tracks
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            item.Key,
                            StringComparison.OrdinalIgnoreCase));

        if (track is null)
        {
            return;
        }

        var editor =
            new TextBox
            {
                Header =
                    "Segmentos do Track · ID:pathIndex",
                AcceptsReturn =
                    true,
                TextWrapping =
                    TextWrapping.NoWrap,
                MinWidth =
                    520,
                MinHeight =
                    300,
                FontFamily =
                    new Microsoft.UI.Xaml.Media.FontFamily(
                        "Consolas"),
                Text =
                    string.Join(
                        Environment.NewLine,
                        track.Entries
                            .Select(
                                entry =>
                                    $"{entry.Id}:{entry.Line2}"))
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Uma linha por segmento. Reordene, remova ou adicione entradas. Para novas linhas, o Map Studio grava o formato compacto OMSI; metadata estendida existente é preservada por posição.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.75
            });

        panel.Children.Add(
            editor);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    $"Editar Track · {track.Name}",
                Content =
                    new ScrollViewer
                    {
                        Content =
                            panel,
                        MaxHeight =
                            560
                    },
                PrimaryButtonText =
                    "Salvar Track",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        var parsed =
            new List<
                (int Id, string PathIndex)>();

        var lineNumber =
            0;

        foreach (
            var rawLine in
                editor.Text
                    .Replace(
                        "\r\n",
                        "\n",
                        StringComparison.Ordinal)
                    .Split('\n'))
        {
            lineNumber++;

            var line =
                rawLine.Trim();

            if (
                string.IsNullOrWhiteSpace(
                    line))
            {
                continue;
            }

            var separator =
                line.IndexOf(
                    ':');

            if (
                separator <= 0 ||
                separator >=
                    line.Length - 1 ||
                !int.TryParse(
                    line[..separator]
                        .Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id) ||
                id < 0)
            {
                StatusText.Text =
                    $"Track não salvo: linha {lineNumber} inválida. Use ID:pathIndex.";

                return;
            }

            var pathIndex =
                line[(separator + 1)..]
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                    pathIndex))
            {
                StatusText.Text =
                    $"Track não salvo: pathIndex vazio na linha {lineNumber}.";

                return;
            }

            parsed.Add(
                (
                    id,
                    pathIndex
                ));
        }

        if (parsed.Count == 0)
        {
            StatusText.Text =
                "Track não salvo: mantenha pelo menos um segmento.";

            return;
        }

        var entries =
            parsed
                .Select(
                    (value, index) =>
                    {
                        if (
                            index <
                            track.Entries.Count)
                        {
                            var source =
                                track.Entries[
                                    index];

                            return source with
                            {
                                Comment =
                                    string.IsNullOrWhiteSpace(
                                        source.Comment)
                                        ? $"{index}:"
                                        : source.Comment,
                                Id =
                                    value.Id,
                                Line2 =
                                    value.PathIndex
                            };
                        }

                        return new OmsiTimetableTrackEntry(
                            $"{index}:",
                            value.Id,
                            value.PathIndex,
                            -1,
                            string.Empty,
                            null,
                            string.Empty,
                            null);
                    })
                .ToArray();

        try
        {
            EditTrackButton.IsEnabled =
                false;

            StatusText.Text =
                $"Salvando Track {track.Name} com backup...";

            var updated =
                await _session
                    .UpdateTimetableTrackAsync(
                        track,
                        entries);

            _timetableCatalog =
                await new OmsiTimetableCatalogReader()
                    .ReadAsync(
                        _session.CurrentMap!
                            .Map
                            .DirectoryPath);

            RefreshTransportItems();

            var refreshed =
                _transportItems
                    .FirstOrDefault(
                        candidate =>
                            candidate.Kind ==
                                "Track" &&
                            string.Equals(
                                candidate.Key,
                                updated.Track.Name,
                                StringComparison.OrdinalIgnoreCase));

            TransportListView.SelectedItem =
                refreshed;

            StatusText.Text =
                $"Track {updated.Track.Name} salvo · {updated.Track.Entries.Count} segmento(s) · backup {updated.BackupPath}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar Track: {exception.Message}";
        }
        finally
        {
            EditTrackButton.IsEnabled =
                TransportListView.SelectedItem is
                    TransportExplorerItem selected &&
                selected.Kind ==
                    "Track";
        }
    }

    private async Task EditBusStopAsync(
        TransportExplorerItem item)
    {
        if (
            _timetableCatalog is null ||
            !int.TryParse(
                item.Key,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var stopIndex) ||
            stopIndex < 0 ||
            stopIndex >=
                _timetableCatalog
                    .BusStops.Count)
        {
            return;
        }

        var stop =
            _timetableCatalog
                .BusStops[
                    stopIndex];

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome",
                Text =
                    stop.Name
            };

        var subNameBox =
            new TextBox
            {
                Header =
                    "Subnome",
                Text =
                    stop.SubName
            };

        var idBox =
            new NumberBox
            {
                Header =
                    "ID",
                Minimum =
                    0,
                Maximum =
                    int.MaxValue,
                Value =
                    stop.Id
            };

        var tileBox =
            new NumberBox
            {
                Header =
                    "Tile index",
                Minimum =
                    -1,
                Maximum =
                    int.MaxValue,
                Value =
                    stop.TileIndex
            };

        var exitingBox =
            new NumberBox
            {
                Header =
                    "Passageiros saindo",
                Minimum =
                    0,
                Maximum =
                    1000000,
                Value =
                    stop.ExitingPassengers ??
                    0,
                SmallChange =
                    1
            };

        var line4Box =
            new TextBox
            {
                Header =
                    "Line4",
                Text =
                    stop.Line4
            };

        var line5Box =
            new TextBox
            {
                Header =
                    "Line5",
                Text =
                    stop.Line5
            };

        var idGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        idGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        idGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            idBox,
            0);

        Grid.SetColumn(
            tileBox,
            1);

        idGrid.Children.Add(
            idBox);

        idGrid.Children.Add(
            tileBox);

        var rawGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        rawGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        rawGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            line4Box,
            0);

        Grid.SetColumn(
            line5Box,
            1);

        rawGrid.Children.Add(
            line4Box);

        rawGrid.Children.Add(
            line5Box);

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    460
            };

        panel.Children.Add(
            nameBox);

        panel.Children.Add(
            subNameBox);

        panel.Children.Add(
            idGrid);

        panel.Children.Add(
            exitingBox);

        panel.Children.Add(
            rawGrid);

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Alterar o ID pode afetar StationLinks. IDs duplicados são bloqueados.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.75
            });

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    $"Editar Stop · {stop.Id}",
                Content =
                    panel,
                PrimaryButtonText =
                    "Salvar Stop",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        if (
            !double.IsFinite(
                idBox.Value) ||
            !double.IsFinite(
                tileBox.Value) ||
            !double.IsFinite(
                exitingBox.Value))
        {
            StatusText.Text =
                "Stop não salvo: valores numéricos inválidos.";
            return;
        }

        var updatedStop =
            new OmsiTimetableBusStop(
                nameBox.Text.Trim(),
                checked(
                    (int)Math.Round(
                        tileBox.Value)),
                checked(
                    (int)Math.Round(
                        idBox.Value)),
                exitingBox.Value,
                line4Box.Text.Trim(),
                line5Box.Text.Trim(),
                subNameBox.Text.Trim());

        try
        {
            EditTrackButton.IsEnabled =
                false;

            StatusText.Text =
                $"Salvando Stop {stop.Id} com backup...";

            var updated =
                await _session
                    .UpdateBusStopAsync(
                        stopIndex,
                        updatedStop);

            _timetableCatalog =
                await new OmsiTimetableCatalogReader()
                    .ReadAsync(
                        _session.CurrentMap!
                            .Map
                            .DirectoryPath);

            RefreshTransportItems();

            TransportListView.SelectedItem =
                _transportItems
                    .FirstOrDefault(
                        candidate =>
                            candidate.Kind ==
                                "Stop" &&
                            candidate.Key ==
                                stopIndex.ToString(
                                    CultureInfo.InvariantCulture));

            var saved =
                updated.Stops[
                    updated.UpdatedIndex];

            StatusText.Text =
                $"Stop {saved.Id} salvo · {saved.Name} · backup {updated.BackupPath}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar Stop: {exception.Message}";
        }
        finally
        {
            EditTrackButton.IsEnabled =
                TransportListView.SelectedItem is
                    TransportExplorerItem selected &&
                selected.Kind is
                    "Track" or
                    "Trip" or
                    "Stop" or
                    "StationLink" or
                    "Line";
        }
    }

    private async Task EditTimetableLineAsync(
        TransportExplorerItem item)
    {
        if (_timetableCatalog is null)
        {
            return;
        }

        var line =
            _timetableCatalog.Lines
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            item.Key,
                            StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return;
        }

        var updatedLine =
            await TimetableLineEditorDialog
                .ShowAsync(
                    MainRoot.XamlRoot,
                    line,
                    _timetableCatalog
                        .Trips
                        .Select(
                            trip =>
                                trip.Name)
                        .ToArray());

        if (updatedLine is null)
        {
            return;
        }

        await SaveTimetableLineAsync(
            line,
            updatedLine);
    }

    private bool _savingTimetableLine;

    private async Task<bool> SaveTimetableLineAsync(
        OmsiTimetableLine sourceLine,
        OmsiTimetableLine updatedLine)
    {
        if (_savingTimetableLine)
        {
            StatusText.Text = "Uma Line já está sendo salva. Aguarde e tente novamente.";
            return false;
        }

        // A draft from another catalog/map must never overwrite the current Line.
        if (_timetableCatalog is null ||
            !_timetableCatalog.Lines.Any(line => ReferenceEquals(line, sourceLine)))
        {
            StatusText.Text = "O catálogo mudou durante a edição. Descarte e reabra a Line antes de salvar.";
            return false;
        }

        _savingTimetableLine = true;
        try
        {
            EditTrackButton.IsEnabled =
                false;

            StatusText.Text =
                $"Salvando tabela Line/Tours {sourceLine.Name} com backup...";

            var updated =
                await _session
                    .UpdateTimetableLineAsync(
                        sourceLine,
                        updatedLine);

            _timetableCatalog =
                await new OmsiTimetableCatalogReader()
                    .ReadAsync(
                        _session.CurrentMap!
                            .Map
                            .DirectoryPath);

            // Reveal the saved Line even when an Explorer search previously hid it.
            ExplorerSearchBox.Text = string.Empty;
            TransportKindComboBox.SelectedIndex = 4;
            RefreshTransportItems();

            TransportListView.SelectedItem =
                _transportItems
                    .FirstOrDefault(
                        candidate =>
                            candidate.Kind ==
                                "Line" &&
                            string.Equals(
                                candidate.Key,
                                updated.Line.Name,
                                StringComparison.OrdinalIgnoreCase));

            if (TransportListView.SelectedItem is TransportExplorerItem lineItem)
            {
                RefreshTransportRouteWorkbench(lineItem, preview: true);
            }

            StatusText.Text =
                $"Line/Tours {updated.Line.Name} salvo pela tabela · {updated.Line.Tours.Count} tour(s) · {updated.Line.Tours.Sum(tour => tour.Trips.Count)} saída(s) · backup {updated.BackupPath}.";
            return true;
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar Line/Tours: {exception.Message}";
            return false;
        }
        finally
        {
            _savingTimetableLine = false;
            EditTrackButton.IsEnabled =
                TransportListView.SelectedItem is
                    TransportExplorerItem selected &&
                selected.Kind is
                    "Track" or
                    "Trip" or
                    "StationLink" or
                    "Line";
        }
    }

    private async Task EditTripAsync(
        TransportExplorerItem item)
    {
        if (_timetableCatalog is null)
        {
            return;
        }

        var trip =
            _timetableCatalog.Trips
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            item.Key,
                            StringComparison.OrdinalIgnoreCase));

        if (trip is null)
        {
            return;
        }

        var updatedTrip =
            await TimetableTripEditorDialog
                .ShowAsync(
                    MainRoot.XamlRoot,
                    trip,
                    _timetableCatalog);

        if (updatedTrip is null)
        {
            return;
        }

        await SaveTimetableTripAsync(
            trip,
            updatedTrip);
    }

    private async Task EditStationLinkAsync(
        TransportExplorerItem item)
    {
        if (
            _timetableCatalog is null ||
            !int.TryParse(
                item.Key,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var linkIndex) ||
            linkIndex < 0 ||
            linkIndex >=
                _timetableCatalog
                    .StationLinks.Count)
        {
            return;
        }

        var link =
            _timetableCatalog
                .StationLinks[
                    linkIndex];

        var commentBox =
            new TextBox
            {
                Header =
                    "Comentário / nome",
                Text =
                    link.Comment
            };

        var startBox =
            new NumberBox
            {
                Header =
                    "BusStop inicial",
                Minimum =
                    0,
                Maximum =
                    int.MaxValue,
                Value =
                    link.StartBusStopId
            };

        var endBox =
            new NumberBox
            {
                Header =
                    "BusStop final",
                Minimum =
                    0,
                Maximum =
                    int.MaxValue,
                Value =
                    link.EndBusStopId
            };

        var metadataBox =
            new TextBox
            {
                Header =
                    "Campos StnLink · 7 linhas: Line1, Line4...Line9",
                AcceptsReturn =
                    true,
                TextWrapping =
                    TextWrapping.NoWrap,
                MinHeight =
                    150,
                FontFamily =
                    new Microsoft.UI.Xaml.Media.FontFamily(
                        "Consolas"),
                Text =
                    string.Join(
                        Environment.NewLine,
                        new[]
                        {
                            link.Line1,
                            link.Line4,
                            link.Line5,
                            link.Line6,
                            link.Line7,
                            link.Line8,
                            link.Line9
                        })
            };

        var entriesBox =
            new TextBox
            {
                Header =
                    "Entradas · id|pathIndex|tile|length|line5|line6|line7|chrono1;chrono2",
                AcceptsReturn =
                    true,
                TextWrapping =
                    TextWrapping.NoWrap,
                MinHeight =
                    260,
                FontFamily =
                    new Microsoft.UI.Xaml.Media.FontFamily(
                        "Consolas"),
                Text =
                    string.Join(
                        Environment.NewLine,
                        link.Entries
                            .Select(
                                entry =>
                                    $"{entry.Id}|{entry.Line2}|{entry.TileIndex}|{entry.Length?.ToString("G17", CultureInfo.InvariantCulture) ?? "0"}|{entry.Line5}|{entry.Line6}|{entry.Line7}|{string.Join(";", entry.ChronoFiles)}"))
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    580
            };

        panel.Children.Add(
            commentBox);

        var stopsGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        stopsGrid.ColumnDefinitions.Add(
            new ColumnDefinition());
        stopsGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            startBox,
            0);
        Grid.SetColumn(
            endBox,
            1);

        stopsGrid.Children.Add(
            startBox);
        stopsGrid.Children.Add(
            endBox);

        panel.Children.Add(
            stopsGrid);
        panel.Children.Add(
            metadataBox);
        panel.Children.Add(
            entriesBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    $"Editar StationLink #{linkIndex}",
                Content =
                    new ScrollViewer
                    {
                        Content =
                            panel,
                        MaxHeight =
                            650
                    },
                PrimaryButtonText =
                    "Salvar StationLink",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        var metadata =
            metadataBox.Text
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Split('\n');

        if (
            metadata.Length !=
                7)
        {
            StatusText.Text =
                "StationLink não salvo: os campos StnLink devem ter exatamente 7 linhas.";
            return;
        }

        var entries =
            new List<
                OmsiStationLinkEntry>();

        var lineNumber =
            0;

        foreach (
            var rawLine in
                entriesBox.Text
                    .Replace(
                        "\r\n",
                        "\n",
                        StringComparison.Ordinal)
                    .Split('\n'))
        {
            lineNumber++;

            var line =
                rawLine.Trim();

            if (string.IsNullOrWhiteSpace(
                    line))
            {
                continue;
            }

            var parts =
                line.Split(
                    '|');

            if (
                parts.Length is
                    < 7 or > 8 ||
                !int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id) ||
                id < 0 ||
                !int.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var tileIndex) ||
                !double.TryParse(
                    parts[3],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var length) ||
                !double.IsFinite(
                    length))
            {
                StatusText.Text =
                    $"StationLink não salvo: entrada inválida na linha {lineNumber}.";
                return;
            }

            var chrono =
                parts.Length ==
                    8 &&
                !string.IsNullOrWhiteSpace(
                    parts[7])
                    ? parts[7]
                        .Split(
                            ';',
                            StringSplitOptions
                                .RemoveEmptyEntries |
                            StringSplitOptions
                                .TrimEntries)
                    : Array.Empty<string>();

            entries.Add(
                new OmsiStationLinkEntry(
                    $"{entries.Count}:",
                    id,
                    parts[1].Trim(),
                    tileIndex,
                    length,
                    parts[4].Trim(),
                    parts[5].Trim(),
                    parts[6].Trim(),
                    chrono));
        }

        if (
            entries.Count ==
            0 ||
            !double.IsFinite(
                startBox.Value) ||
            !double.IsFinite(
                endBox.Value))
        {
            StatusText.Text =
                "StationLink não salvo: mantenha pelo menos uma entrada e stops válidos.";
            return;
        }

        var updatedLink =
            new OmsiStationLink(
                commentBox.Text.Trim(),
                metadata[0].Trim(),
                checked(
                    (int)Math.Round(
                        startBox.Value)),
                checked(
                    (int)Math.Round(
                        endBox.Value)),
                metadata[1].Trim(),
                metadata[2].Trim(),
                metadata[3].Trim(),
                metadata[4].Trim(),
                metadata[5].Trim(),
                metadata[6].Trim(),
                entries.ToArray());

        try
        {
            EditTrackButton.IsEnabled =
                false;

            StatusText.Text =
                $"Salvando StationLink #{linkIndex} com backup...";

            var updated =
                await _session
                    .UpdateStationLinkAsync(
                        linkIndex,
                        updatedLink);

            _timetableCatalog =
                await new OmsiTimetableCatalogReader()
                    .ReadAsync(
                        _session.CurrentMap!
                            .Map
                            .DirectoryPath);

            RefreshTransportItems();

            TransportListView.SelectedItem =
                _transportItems
                    .FirstOrDefault(
                        candidate =>
                            candidate.Kind ==
                                "StationLink" &&
                            candidate.Key ==
                                linkIndex.ToString(
                                    CultureInfo.InvariantCulture));

            StatusText.Text =
                $"StationLink #{linkIndex} salvo · {updated.Links[updated.UpdatedIndex].Entries.Count} entrada(s) · backup {updated.BackupPath}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao salvar StationLink: {exception.Message}";
        }
        finally
        {
            EditTrackButton.IsEnabled =
                TransportListView.SelectedItem is
                    TransportExplorerItem selected &&
                selected.Kind is
                    "Track" or
                    "Trip" or
                    "StationLink" or
                    "Line";
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
            EditTrackButton.Visibility =
                Visibility.Collapsed;

            EditTrackButton.IsEnabled =
                false;

            TransportPreviewButton.IsEnabled =
                false;

            TransportFocusStepButton.IsEnabled =
                false;

            TransportRecordButton.IsEnabled =
                false;

            TransportAddSelectionButton.IsEnabled =
                false;

            TransportRemoveStepButton.IsEnabled =
                false;

            TransportMoveStepUpButton.IsEnabled =
                false;

            TransportMoveStepDownButton.IsEnabled =
                false;

            TransportProfilesButton.IsEnabled =
                false;

            TransportRouteStepsListView.ItemsSource =
                null;

            TransportRouteStatusText.Text =
                "Selecione Track, Trip, StationLink ou Line para inspecionar o caminho.";

            TransportDetailText.Text =
                "Selecione um item para ver detalhes.";

            Viewport
                .ClearTimetableRoutePreview();

            return;
        }

        TransportDetailText.Text =
            item.Detail;

        var editable =
            item.Kind is
                "Track" or
                "Trip" or
                "Stop" or
                "StationLink" or
                "Line";

        EditTrackButton.Visibility =
            editable
                ? Visibility.Visible
                : Visibility.Collapsed;

        EditTrackButton.IsEnabled =
            editable;

        TransportRecordButton.IsEnabled =
            item.Kind is
                "Track" or
                "StationLink";

        if (
            item.Kind is not
                (
                    "Track" or
                    "StationLink"
                ))
        {
            SetTransportTrackRecordMode(
                false);
        }

        UpdateTransportAddSelectionAvailability();

        TransportProfilesButton.IsEnabled =
            item.Kind == "Trip";

        EditTrackButton.Content =
            item.Kind switch
            {
                "Trip" =>
                    "Editar Trip / rota",
                "Stop" =>
                    "Editar Stop / parada",
                "StationLink" =>
                    "Editar StationLink",
                "Line" =>
                    "Editar Line / Tours",
                _ =>
                    "Editar Track / caminho"
            };

        RefreshTransportRouteWorkbench(
            item,
            preview: true);
    }

    private void RefreshTransportRouteWorkbench(
        TransportExplorerItem item,
        bool preview)
    {
        var steps =
            BuildTransportRouteSteps(
                item);

        TransportRouteStepsListView.ItemsSource =
            new System.Collections.ObjectModel.ObservableCollection<
                TransportRouteStepItem>(
                steps);

        TransportRouteStepsListView.SelectedIndex =
            steps.Count > 0
                ? 0
                : -1;

        TransportPreviewButton.IsEnabled =
            steps.Count > 0;

        TransportFocusStepButton.IsEnabled =
            steps.Count > 0;

        if (steps.Count == 0)
        {
            Viewport
                .ClearTimetableRoutePreview();

            TransportRouteStatusText.Text =
                item.Kind == "Stop"
                    ? "Parada selecionada. Edite identificação, tile e dados de passageiros; StationLinks e Trips definem o caminho."
                    : "Nenhum segmento de path resolvido para este item.";

            return;
        }

        var resolved =
            preview
                ? PreviewTransportItem(
                    item)
                : 0;

        TransportRouteStatusText.Text =
            preview
                ? $"{item.Kind}: {resolved}/{steps.Count} segmento(s) desenhado(s) no mapa · selecione um segmento para focar."
                : $"{item.Kind}: {steps.Count} segmento(s) no caminho.";
    }

    private IReadOnlyList<
        TransportRouteStepItem>
        BuildTransportRouteSteps(
            TransportExplorerItem item)
    {
        if (_timetableCatalog is null)
        {
            return Array.Empty<
                TransportRouteStepItem>();
        }

        return OmsiTimetableRouteResolver
            .Resolve(
                _timetableCatalog,
                item.Kind,
                item.Key)
            .Select(
                step =>
                    new TransportRouteStepItem(
                        step.Sequence,
                        step.EntityId,
                        step.PathIndex,
                        step.Length,
                        step.SourceLabel,
                        $"{step.Sequence:000} · ID {step.EntityId} · path {step.PathIndex} · {step.SourceLabel}"))
            .ToArray();
    }


    private int PreviewTransportItem(
        TransportExplorerItem item)
    {
        var steps =
            BuildTransportRouteSteps(
                item);

        if (steps.Count == 0)
        {
            Viewport
                .ClearTimetableRoutePreview();

            return 0;
        }

        var entries =
            steps
                .Select(
                    (step, index) =>
                        new OmsiTimetableTrackEntry(
                            $"{index}:",
                            step.EntityId,
                            step.PathIndex,
                            -1,
                            string.Empty,
                            step.Length,
                            string.Empty,
                            null))
                .ToArray();

        return Viewport
            .PreviewTimetableTrack(
                entries);
    }

    private int PreviewTransportStep(
        TransportRouteStepItem step)
    {
        ArgumentNullException.ThrowIfNull(
            step);

        var entry =
            new OmsiTimetableTrackEntry(
                "0:",
                step.EntityId,
                step.PathIndex,
                -1,
                string.Empty,
                step.Length,
                string.Empty,
                null);

        return Viewport
            .PreviewTimetableTrack(
                [entry]);
    }

    private void OnTransportValidateClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_timetableCatalog is null)
        {
            StatusText.Text =
                "Validação TTData indisponível: nenhum catálogo carregado.";
            return;
        }

        var loadedIds =
            Viewport
                .GetExplorerItems()
                .Select(
                    item =>
                        item.EntityId)
                .ToHashSet();

        var unresolvedSegments =
            _timetableCatalog.Tracks
                .SelectMany(
                    track =>
                        track.Entries
                            .Select(
                                entry =>
                                    entry.Id))
                .Concat(
                    _timetableCatalog
                        .StationLinks
                        .SelectMany(
                            link =>
                                link.Entries
                                    .Select(
                                        entry =>
                                            entry.Id)))
                .Count(
                    id =>
                        !loadedIds.Contains(
                            id));

        var problems =
            _timetableCatalog
                .BrokenTripTrackReferenceCount +
            _timetableCatalog
                .BrokenTripStationLinkReferenceCount +
            _timetableCatalog
                .BrokenStationLinkStopReferenceCount +
            _timetableCatalog
                .BrokenLineTripReferenceCount;

        TransportRouteStatusText.Text =
            $"Validação · Trip→Track quebrado: {_timetableCatalog.BrokenTripTrackReferenceCount} · " +
            $"Trip→StationLink quebrado: {_timetableCatalog.BrokenTripStationLinkReferenceCount} · " +
            $"StationLink→Stop quebrado: {_timetableCatalog.BrokenStationLinkStopReferenceCount} · " +
            $"Line→Trip quebrado: {_timetableCatalog.BrokenLineTripReferenceCount} · " +
            $"segmentos fora do viewport atual: {unresolvedSegments}.";

        StatusText.Text =
            problems == 0
                ? "TTData: referências estruturais válidas. Segmentos fora da região carregada são informativos."
                : $"TTData: {problems} referência(s) quebrada(s) encontrada(s).";
    }

    private void OnTransportPreviewClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            TransportListView.SelectedItem is
                TransportExplorerItem item)
        {
            RefreshTransportRouteWorkbench(
                item,
                preview: true);
        }
    }

    private void OnTransportPathsClick(
        object sender,
        RoutedEventArgs e)
    {
        _transportPathsVisible =
            !_transportPathsVisible;

        if (_transportPathsVisible)
        {
            Viewport
                .RefreshTrafficPathDisplay();
        }

        Viewport
            .SetTrafficPathsVisible(
                _transportPathsVisible);

        TransportPathsButton.Content =
            _transportPathsVisible
                ? "Ocultar Paths"
                : "Mostrar Paths";

        StatusText.Text =
            _transportPathsVisible
                ? $"Transporte: {Viewport.TrafficPathLineCount} linhas de path OMSI visíveis."
                : "Transporte: paths auxiliares ocultos; preview da rota permanece disponível.";
    }

    private void OnTransportClearPreviewClick(
        object sender,
        RoutedEventArgs e)
    {
        Viewport
            .ClearTimetableRoutePreview();

        TransportRouteStatusText.Text =
            "Preview da rota limpo. Os dados continuam carregados para edição.";

        StatusText.Text =
            "Preview de transporte limpo.";
    }

    private void OnTransportRouteStepSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var selectedStep =
            TransportRouteStepsListView.SelectedItem is
                TransportRouteStepItem;

        TransportFocusStepButton.IsEnabled =
            selectedStep;

        var editableRoute =
            selectedStep &&
            TransportListView.SelectedItem is
                TransportExplorerItem item &&
            item.Kind is
                "Track" or
                "StationLink";

        TransportRemoveStepButton.IsEnabled =
            editableRoute;

        TransportMoveStepUpButton.IsEnabled =
            editableRoute &&
            TransportRouteStepsListView.SelectedIndex >
                0;

        TransportMoveStepDownButton.IsEnabled =
            editableRoute &&
            TransportRouteStepsListView.SelectedIndex >=
                0 &&
            TransportRouteStepsListView.SelectedIndex <
                TransportRouteStepsListView.Items.Count -
                1;

        if (
            !_transportMode ||
            TransportRouteStepsListView.SelectedItem is not
                TransportRouteStepItem step)
        {
            return;
        }

        var resolved =
            PreviewTransportStep(
                step);

        TransportRouteStatusText.Text =
            resolved > 0
                ? $"Trecho {step.Sequence:000} isolado no mapa · ID {step.EntityId} · path {step.PathIndex} · {step.SourceLabel}. Use Rota completa para restaurar o caminho."
                : $"Trecho {step.Sequence:000} · ID {step.EntityId} · path {step.PathIndex} não pôde ser resolvido no viewport atual.";

        SynchronizeTransportStepSelectionOnMap(
            step);
    }

    private void SynchronizeTransportStepSelectionOnMap(
        TransportRouteStepItem step)
    {
        if (_transportTrackRecordMode)
        {
            return;
        }

        var target =
            Viewport
                .GetExplorerItems()
                .FirstOrDefault(
                    candidate =>
                        candidate.EntityId ==
                            step.EntityId);

        if (target is not null)
        {
            Viewport
                .SelectExplorerItem(
                    target,
                    focus: false);
        }

        if (
            !int.TryParse(
                step.PathIndex,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var pathIndex))
        {
            return;
        }

        TransportPathIndexBox.Value =
            pathIndex;

        Viewport
            .SetTrafficPathFocusedIndex(
                pathIndex);

        var matchingChoice =
            TransportPathChoiceComboBox.Items
                .OfType<
                    NativeTrafficPathChoice>()
                .Select(
                    (choice, index) =>
                        new
                        {
                            Choice =
                                choice,
                            Index =
                                index
                        })
                .FirstOrDefault(
                    item =>
                        item.Choice.Index ==
                            pathIndex);

        if (matchingChoice is not null)
        {
            TransportPathChoiceComboBox.SelectedIndex =
                matchingChoice.Index;
        }
        else
        {
            TransportPathChoiceHintText.Text =
                $"Path {pathIndex} usado pela rota não está disponível no item carregado.";
        }
    }

    private void OnTransportFocusStepClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            TransportRouteStepsListView.SelectedItem is not
                TransportRouteStepItem step)
        {
            return;
        }

        PreviewTransportStep(
            step);

        var target =
            Viewport
                .GetExplorerItems()
                .FirstOrDefault(
                    candidate =>
                        candidate.EntityId ==
                            step.EntityId);

        if (target is null)
        {
            StatusText.Text =
                $"Segmento {step.Sequence}: ID {step.EntityId} não está carregado no viewport atual.";
            return;
        }

        if (
            !Viewport.SelectExplorerItem(
                target,
                focus: true))
        {
            StatusText.Text =
                $"Não foi possível focar o ID {step.EntityId}.";
            return;
        }

        StatusText.Text =
            $"Rota · segmento {step.Sequence} · ID {step.EntityId} · path {step.PathIndex} · {step.SourceLabel}.";
    }

    private async void OnToolValidationClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolValidationButton);

        if (
            _session.CurrentMap is not
                { } snapshot ||
            _session.OmsiRootPath is not
                { } omsiRoot)
        {
            StatusText.Text =
                "Validação: abra um mapa primeiro.";

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
            items
                .OrderBy(
                    item =>
                        item.Severity switch
                        {
                            "Erro" => 0,
                            "Aviso" => 1,
                            _ => 2
                        })
                .ThenBy(
                    item =>
                        item.Code,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    item =>
                        item.DisplayText,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
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

        var missingSceneryDependencies =
            new Dictionary<
                string,
                List<string>>(
                    StringComparer.OrdinalIgnoreCase);

        var missingSplineDependencies =
            new Dictionary<
                string,
                List<string>>(
                    StringComparer.OrdinalIgnoreCase);

        static void RegisterMissingDependency(
            Dictionary<
                string,
                List<string>> target,
            string path,
            string usage)
        {
            if (
                !target.TryGetValue(
                    path,
                    out var values))
            {
                values = [];
                target[path] =
                    values;
            }

            values.Add(
                usage);
        }

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
                    RegisterMissingDependency(
                        missingSceneryDependencies,
                        item.SceneryObjectPath,
                        $"Objeto #{item.ObjectId} · tile {tile.Reference.X},{tile.Reference.Y}");
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
                    RegisterMissingDependency(
                        missingSplineDependencies,
                        item.SplinePath,
                        $"Spline #{item.SplineId} · tile {tile.Reference.X},{tile.Reference.Y}");
                }
            }
        }

        foreach (
            var dependency in
                missingSceneryDependencies
                    .OrderBy(
                        pair =>
                            pair.Key,
                        StringComparer.OrdinalIgnoreCase))
        {
            result.Add(
                new ValidationExplorerItem(
                    "Erro",
                    "missing-sco",
                    $"ERRO · SCO ausente · {Path.GetFileName(dependency.Key)} · {dependency.Value.Count} uso(s)",
                    $"{dependency.Key}\n" +
                    string.Join(
                        "\n",
                        dependency.Value
                            .Take(12)) +
                    (
                        dependency.Value.Count >
                            12
                            ? $"\n… +{dependency.Value.Count - 12} uso(s)"
                            : string.Empty
                    ),
                    OmsiAssetKind.SceneryObject,
                    dependency.Key));
        }

        foreach (
            var dependency in
                missingSplineDependencies
                    .OrderBy(
                        pair =>
                            pair.Key,
                        StringComparer.OrdinalIgnoreCase))
        {
            result.Add(
                new ValidationExplorerItem(
                    "Erro",
                    "missing-sli",
                    $"ERRO · SLI ausente · {Path.GetFileName(dependency.Key)} · {dependency.Value.Count} uso(s)",
                    $"{dependency.Key}\n" +
                    string.Join(
                        "\n",
                        dependency.Value
                            .Take(12)) +
                    (
                        dependency.Value.Count >
                            12
                            ? $"\n… +{dependency.Value.Count - 12} uso(s)"
                            : string.Empty
                    ),
                    OmsiAssetKind.Spline,
                    dependency.Key));
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
                .BrokenTripStationLinkReferenceCount >
            0)
        {
            result.Add(
                new ValidationExplorerItem(
                    "Erro",
                    "trip-stationlink",
                    $"ERRO · {timetable.BrokenTripStationLinkReferenceCount} Trip→StationLink quebrado(s)",
                    "Há Trip tipo 2 com pares consecutivos de stops sem StationLink correspondente em StnLinks.cfg."));
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

    private async Task ActivateLibraryGroupToolAsync(
        int kindIndex,
        OmsiAssetLibraryGroup group,
        string status)
    {
        _junctionPlacementTarget =
            null;

        _dependencyRepairKind =
            null;

        _dependencyRepairOldPath =
            null;

        RepairDependencyButton.Visibility =
            Visibility.Collapsed;

        await ActivateLibraryToolAsync(
            kindIndex,
            null,
            status);

        LibraryViewComboBox.SelectedIndex =
            0;

        var groupIndex =
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
                            group)
                .index;

        if (
            groupIndex >= 0 &&
            groupIndex <
                _libraryGroupOptions.Count)
        {
            LibraryGroupComboBox.SelectedIndex =
                groupIndex;
        }

        RefreshLibrarySubcategoryOptions();
        RefreshLibraryFilter();
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
                    ? _session.IsStandaloneWorkspace
                        ? "Preview noturno ativo com céu do Workspace."
                        : "Preview noturno ativo com céu OMSI."
                    : _session.IsStandaloneWorkspace
                        ? "Preview diurno ativo com céu do Workspace."
                        : "Preview diurno ativo com céu OMSI."
                : _session.OmsiRootPath is null
                    ? "Ative o Workspace Map Studio ou selecione uma instalação do OMSI para carregar um céu."
                    : _session.IsStandaloneWorkspace
                        ? "Textura de céu do Workspace não encontrada; mantendo o fundo padrão."
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

    private void OnTerrainPaintVisibilityClick(
        object sender,
        RoutedEventArgs e)
    {
        var visible =
            ShowTerrainPaintMenuItem
                .IsChecked;

        if (
            Viewport
                .SetTerrainPaintVisible(
                    visible))
        {
            StatusText.Text =
                visible
                    ? "Pintura do terreno visível."
                    : "Pintura do terreno oculta; base, lightmap e referência permanecem visíveis.";
        }
    }

    private void SetTerrainLayerVisibility(
        int layerIndex,
        ToggleMenuFlyoutItem item)
    {
        if (
            Viewport
                .SetTerrainLayerVisible(
                    layerIndex,
                    item.IsChecked))
        {
            StatusText.Text =
                $"Groundtex {layerIndex} {(item.IsChecked ? "visível" : "oculto")}.";
        }
    }

    private void RefreshTerrainLayerVisibilityMenu(
        NativeMapSnapshot snapshot)
    {
        if (
            string.Equals(
                _terrainLayerVisibilityMapDirectory,
                snapshot.Map.DirectoryPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _terrainLayerVisibilityMapDirectory =
            snapshot.Map.DirectoryPath;

        Viewport
            .ResetTerrainLayerVisibility();

        ShowTerrainPaintMenuItem.IsChecked =
            true;

        TerrainLayersMenuItem.Items.Clear();

        var groundTextures =
            snapshot.Map.GroundTextures;

        var hasPaintLayers =
            groundTextures.Count >
            1;

        ShowTerrainPaintMenuItem.IsEnabled =
            hasPaintLayers;

        TerrainLayersMenuItem.IsEnabled =
            hasPaintLayers;

        if (!hasPaintLayers)
        {
            TerrainLayersMenuItem.Items.Add(
                new MenuFlyoutItem
                {
                    Text =
                        "Sem camadas pintadas",
                    IsEnabled =
                        false
                });

            return;
        }

        for (
            var index = 1;
            index < groundTextures.Count;
            index++)
        {
            var layerIndex =
                index;

            var texturePath =
                groundTextures[index]
                    .MainTexturePath;

            var textureName =
                string.IsNullOrWhiteSpace(
                    texturePath)
                    ? $"groundtex {index}"
                    : Path.GetFileName(
                        texturePath
                            .Replace(
                                '\\',
                                Path.DirectorySeparatorChar));

            var item =
                new ToggleMenuFlyoutItem
                {
                    Text =
                        $"{index}: {textureName}",
                    IsChecked =
                        true
                };

            item.Click +=
                (_, _) =>
                    SetTerrainLayerVisibility(
                        layerIndex,
                        item);

            TerrainLayersMenuItem.Items.Add(
                item);
        }
    }

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

    private void OnMinimizeExplorerPanelClick(
        object sender,
        RoutedEventArgs e) =>
        ToggleExplorerPanel();

    private void OnRestoreExplorerPanelClick(
        object sender,
        RoutedEventArgs e) =>
        ToggleExplorerPanel();


    private void OnToggleInspectorClick(
        object sender,
        RoutedEventArgs e) =>
        ToggleInspectorPanel();

    private void OnMinimizeInspectorPanelClick(
        object sender,
        RoutedEventArgs e) =>
        ToggleInspectorPanel();

    private void OnRestoreInspectorPanelClick(
        object sender,
        RoutedEventArgs e) =>
        ToggleInspectorPanel();

    private void UpdatePanelRestoreButtons()
    {
        var explorerVisible =
            ExplorerPanel.Visibility ==
                Visibility.Visible &&
            (
                IsFullscreen() ||
                ExplorerColumn.Width.Value >
                    0);

        var inspectorVisible =
            InspectorPanel.Visibility ==
                Visibility.Visible &&
            (
                IsFullscreen() ||
                InspectorColumn.Width.Value >
                    0);

        RestoreExplorerPanelButton.Visibility =
            explorerVisible
                ? Visibility.Collapsed
                : Visibility.Visible;

        RestoreInspectorPanelButton.Visibility =
            inspectorVisible
                ? Visibility.Collapsed
                : Visibility.Visible;
    }


    private void ToggleExplorerPanel()
    {
        if (IsFullscreen())
        {
            ExplorerPanel.Visibility =
                ExplorerPanel.Visibility ==
                    Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            StatusText.Text =
                ExplorerPanel.Visibility ==
                    Visibility.Visible
                    ? "Explorer flutuante visível."
                    : "Explorer flutuante oculto.";

            UpdatePanelRestoreButtons();
            return;
        }

        var visible =
            ExplorerColumn.Width.Value >
            0;

        if (visible)
        {
            _explorerPanelWidth =
                Math.Max(
                    220,
                    ExplorerColumn.Width.Value);

            ExplorerColumn.Width =
                new GridLength(0);

            ExplorerSplitterColumn.Width =
                new GridLength(0);

            ExplorerPanel.Visibility =
                Visibility.Collapsed;

            StatusText.Text =
                "Explorer recolhido.";

            UpdatePanelRestoreButtons();
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

        UpdatePanelRestoreButtons();
    }

    private void ToggleInspectorPanel()
    {
        if (IsFullscreen())
        {
            InspectorPanel.Visibility =
                InspectorPanel.Visibility ==
                    Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            StatusText.Text =
                InspectorPanel.Visibility ==
                    Visibility.Visible
                    ? "Inspector flutuante visível."
                    : "Inspector flutuante oculto.";

            UpdatePanelRestoreButtons();
            return;
        }

        var visible =
            InspectorColumn.Width.Value >
            0;

        if (visible)
        {
            _inspectorPanelWidth =
                Math.Max(
                    240,
                    InspectorColumn.Width.Value);

            InspectorColumn.Width =
                new GridLength(0);

            InspectorSplitterColumn.Width =
                new GridLength(0);

            InspectorPanel.Visibility =
                Visibility.Collapsed;

            StatusText.Text =
                "Inspector recolhido.";

            UpdatePanelRestoreButtons();
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

        UpdatePanelRestoreButtons();
    }

    private void BeginFullscreenPanelDrag(
        UIElement element,
        PointerRoutedEventArgs e,
        bool explorer)
    {
        if (!IsFullscreen())
        {
            return;
        }

        var point =
            e.GetCurrentPoint(
                MainRoot);

        _fullscreenPanelDragPointerId =
            e.Pointer.PointerId;

        _fullscreenPanelDragStartX =
            point.Position.X;

        _fullscreenPanelDragStartY =
            point.Position.Y;

        if (explorer)
        {
            _draggingFullscreenExplorer =
                true;

            _fullscreenPanelDragOriginX =
                FullscreenExplorerTranslate.X;

            _fullscreenPanelDragOriginY =
                FullscreenExplorerTranslate.Y;
        }
        else
        {
            _draggingFullscreenInspector =
                true;

            _fullscreenPanelDragOriginX =
                FullscreenInspectorTranslate.X;

            _fullscreenPanelDragOriginY =
                FullscreenInspectorTranslate.Y;
        }

        element.CapturePointer(
            e.Pointer);

        e.Handled =
            true;
    }

    private void MoveFullscreenPanelDrag(
        PointerRoutedEventArgs e,
        bool explorer)
    {
        if (!IsFullscreen() ||
            e.Pointer.PointerId !=
                _fullscreenPanelDragPointerId ||
            (explorer
                ? !_draggingFullscreenExplorer
                : !_draggingFullscreenInspector))
        {
            return;
        }

        var point =
            e.GetCurrentPoint(
                MainRoot);

        var dx =
            point.Position.X -
            _fullscreenPanelDragStartX;

        var dy =
            point.Position.Y -
            _fullscreenPanelDragStartY;

        var maxX =
            Math.Max(
                80,
                MainRoot.ActualWidth -
                280);

        var maxY =
            Math.Max(
                80,
                MainRoot.ActualHeight -
                240);

        var transform =
            explorer
                ? FullscreenExplorerTranslate
                : FullscreenInspectorTranslate;

        transform.X =
            Math.Clamp(
                _fullscreenPanelDragOriginX +
                    dx,
                -maxX,
                maxX);

        transform.Y =
            Math.Clamp(
                _fullscreenPanelDragOriginY +
                    dy,
                -40,
                maxY);

        e.Handled =
            true;
    }

    private void EndFullscreenPanelDrag(
        UIElement? element,
        PointerRoutedEventArgs e,
        bool explorer)
    {
        if (e.Pointer.PointerId !=
            _fullscreenPanelDragPointerId)
        {
            return;
        }

        if (explorer)
        {
            _draggingFullscreenExplorer =
                false;

            _fullscreenExplorerOffsetX =
                FullscreenExplorerTranslate.X;

            _fullscreenExplorerOffsetY =
                FullscreenExplorerTranslate.Y;
        }
        else
        {
            _draggingFullscreenInspector =
                false;

            _fullscreenInspectorOffsetX =
                FullscreenInspectorTranslate.X;

            _fullscreenInspectorOffsetY =
                FullscreenInspectorTranslate.Y;
        }

        element?.ReleasePointerCapture(
            e.Pointer);

        e.Handled =
            true;
    }

    private void OnExplorerFloatingDragPressed(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            BeginFullscreenPanelDrag(
                element,
                e,
                explorer:
                    true);
        }
    }

    private void OnExplorerFloatingDragMoved(
        object sender,
        PointerRoutedEventArgs e) =>
        MoveFullscreenPanelDrag(
            e,
            explorer:
                true);

    private void OnExplorerFloatingDragReleased(
        object sender,
        PointerRoutedEventArgs e) =>
        EndFullscreenPanelDrag(
            sender as UIElement,
            e,
            explorer:
                true);

    private void OnExplorerResizeGripPressed(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (
            !IsFullscreen() ||
            sender is not UIElement element)
        {
            return;
        }

        var point =
            e.GetCurrentPoint(
                MainRoot);

        _resizingFullscreenExplorer =
            true;

        _fullscreenExplorerResizePointerId =
            e.Pointer.PointerId;

        _fullscreenExplorerResizeStartX =
            point.Position.X;

        _fullscreenExplorerResizeStartY =
            point.Position.Y;

        _fullscreenExplorerResizeOriginWidth =
            Math.Max(
                300,
                ExplorerPanel.ActualWidth);

        _fullscreenExplorerResizeOriginHeight =
            Math.Max(
                360,
                ExplorerPanel.ActualHeight);

        element.CapturePointer(
            e.Pointer);

        e.Handled =
            true;
    }

    private void OnExplorerResizeGripMoved(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (
            !IsFullscreen() ||
            !_resizingFullscreenExplorer ||
            e.Pointer.PointerId !=
                _fullscreenExplorerResizePointerId)
        {
            return;
        }

        var point =
            e.GetCurrentPoint(
                MainRoot);

        var dx =
            point.Position.X -
            _fullscreenExplorerResizeStartX;

        var dy =
            point.Position.Y -
            _fullscreenExplorerResizeStartY;

        var maxWidth =
            Math.Max(
                320,
                Math.Min(
                    760,
                    MainRoot.ActualWidth -
                        32));

        var maxHeight =
            Math.Max(
                420,
                MainRoot.ActualHeight -
                    96);

        var width =
            Math.Clamp(
                _fullscreenExplorerResizeOriginWidth +
                    dx,
                300,
                maxWidth);

        var height =
            Math.Clamp(
                _fullscreenExplorerResizeOriginHeight +
                    dy,
                360,
                maxHeight);

        ExplorerPanel.Width =
            width;

        ExplorerPanel.MaxHeight =
            maxHeight;

        ExplorerPanel.Height =
            height;

        _explorerPanelWidth =
            width;

        _fullscreenExplorerHeight =
            height;

        e.Handled =
            true;
    }

    private void OnExplorerResizeGripReleased(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (
            e.Pointer.PointerId !=
                _fullscreenExplorerResizePointerId)
        {
            return;
        }

        _resizingFullscreenExplorer =
            false;

        if (
            sender is UIElement element)
        {
            element.ReleasePointerCapture(
                e.Pointer);
        }

        _explorerPanelWidth =
            Math.Max(
                300,
                ExplorerPanel.ActualWidth);

        _fullscreenExplorerHeight =
            Math.Max(
                360,
                ExplorerPanel.ActualHeight);

        StatusText.Text =
            $"Projeto redimensionado · {_explorerPanelWidth:F0} × {_fullscreenExplorerHeight:F0}.";

        e.Handled =
            true;
    }

    private void OnInspectorFloatingDragPressed(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            BeginFullscreenPanelDrag(
                element,
                e,
                explorer:
                    false);
        }
    }

    private void OnInspectorFloatingDragMoved(
        object sender,
        PointerRoutedEventArgs e) =>
        MoveFullscreenPanelDrag(
            e,
            explorer:
                false);

    private void OnInspectorFloatingDragReleased(
        object sender,
        PointerRoutedEventArgs e) =>
        EndFullscreenPanelDrag(
            sender as UIElement,
            e,
            explorer:
                false);

    private void OnMapToolPaletteDragPressed(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (
            sender is not
                UIElement element)
        {
            return;
        }

        var point =
            e.GetCurrentPoint(
                MainRoot);

        _isMapToolPaletteDragging =
            true;

        _mapToolPaletteDragPointerId =
            e.Pointer.PointerId;

        _mapToolPaletteDragStartX =
            point.Position.X;

        _mapToolPaletteDragStartY =
            point.Position.Y;

        _mapToolPaletteOriginX =
            MapToolPaletteTranslate.X;

        _mapToolPaletteOriginY =
            MapToolPaletteTranslate.Y;

        element.CapturePointer(
            e.Pointer);

        e.Handled =
            true;
    }

    private void OnMapToolPaletteDragMoved(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (
            !_isMapToolPaletteDragging ||
            e.Pointer.PointerId !=
                _mapToolPaletteDragPointerId)
        {
            return;
        }

        var point =
            e.GetCurrentPoint(
                MainRoot);

        var nextX =
            _mapToolPaletteOriginX +
            point.Position.X -
            _mapToolPaletteDragStartX;

        var nextY =
            _mapToolPaletteOriginY +
            point.Position.Y -
            _mapToolPaletteDragStartY;

        var maxX =
            Math.Max(
                40,
                MainRoot.ActualWidth /
                    2.0 -
                60);

        var maxY =
            Math.Max(
                40,
                MainRoot.ActualHeight -
                100);

        MapToolPaletteTranslate.X =
            Math.Clamp(
                nextX,
                -maxX,
                maxX);

        MapToolPaletteTranslate.Y =
            Math.Clamp(
                nextY,
                -maxY,
                40);

        e.Handled =
            true;
    }

    private void OnMapToolPaletteDragReleased(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (
            !_isMapToolPaletteDragging ||
            e.Pointer.PointerId !=
                _mapToolPaletteDragPointerId)
        {
            return;
        }

        _isMapToolPaletteDragging =
            false;

        if (
            sender is
                UIElement element)
        {
            element.ReleasePointerCapture(
                e.Pointer);
        }

        _assetLibraryState =
            new NativeAssetLibraryState
            {
                Favorites =
                    _assetLibraryState
                        .Favorites,
                Recent =
                    _assetLibraryState
                        .Recent,
                Usage =
                    _assetLibraryState
                        .Usage,
                Collections =
                    _assetLibraryState
                        .Collections,
                ConstructionSets =
                    _assetLibraryState
                        .ConstructionSets,
                ToolPaletteOffsetX =
                    MapToolPaletteTranslate.X,
                ToolPaletteOffsetY =
                    MapToolPaletteTranslate.Y
            };

        SaveAssetLibraryState();

        StatusText.Text =
            "Posição da barra de ferramentas salva.";

        e.Handled =
            true;
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

    private void OnQuickCreateAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        args.Handled =
            true;

        var routed =
            new RoutedEventArgs();

        switch (sender.Key)
        {
            case Windows.System.VirtualKey.R:
                OnToolSplinesClick(
                    sender,
                    routed);
                break;

            case Windows.System.VirtualKey.C:
                OnToolCrossingsClick(
                    sender,
                    routed);
                break;

            case Windows.System.VirtualKey.O:
                OnToolObjectsClick(
                    sender,
                    routed);
                break;

            case Windows.System.VirtualKey.B:
                OnToolBuildingsClick(
                    sender,
                    routed);
                break;

            case Windows.System.VirtualKey.G:
            case Windows.System.VirtualKey.Y:
                OnToolVegetationClick(
                    sender,
                    routed);
                break;

            case Windows.System.VirtualKey.T:
                OnToolTerrainClick(
                    sender,
                    routed);
                break;

            case Windows.System.VirtualKey.A:
                OnToolWaterClick(
                    sender,
                    routed);
                break;

            default:
                args.Handled =
                    false;
                break;
        }
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
        Viewport.CancelSelectedSplineCurveEdit();

        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            Viewport.CancelSceneryPlacement();
            Viewport.CancelSplinePlacement();

            var selectedAsset =
                GetSelectedAssetLibraryEntry();

            PlaceAssetButton.Content =
                selectedAsset?.Kind ==
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

    private void BeginLoading(
        string title,
        string? detail = null)
    {
        _loadingOperationDepth++;

        LoadingTitleText.Text =
            title;

        LoadingDetailText.Text =
            detail ??
            string.Empty;

        LoadingDetailText.Visibility =
            string.IsNullOrWhiteSpace(
                detail)
                ? Visibility.Collapsed
                : Visibility.Visible;

        LoadingProgressRing.IsActive =
            true;

        LoadingProgressBar.IsIndeterminate =
            true;

        LoadingOverlay.Visibility =
            Visibility.Visible;
    }

    private void UpdateLoading(
        string title,
        string? detail = null)
    {
        if (_loadingOperationDepth <=
            0)
        {
            return;
        }

        LoadingTitleText.Text =
            title;

        LoadingDetailText.Text =
            detail ??
            string.Empty;

        LoadingDetailText.Visibility =
            string.IsNullOrWhiteSpace(
                detail)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void EndLoading()
    {
        if (_loadingOperationDepth >
            0)
        {
            _loadingOperationDepth--;
        }

        if (_loadingOperationDepth >
            0)
        {
            return;
        }

        _loadingOperationDepth =
            0;

        LoadingProgressRing.IsActive =
            false;

        LoadingProgressBar.IsIndeterminate =
            false;

        LoadingOverlay.Visibility =
            Visibility.Collapsed;
    }

    private void UpdateContentRootSummary(
        int? mapCount = null)
    {
        var root =
            _session.OmsiRootPath;

        if (string.IsNullOrWhiteSpace(
                root))
        {
            RootText.Text =
                "Nenhuma fonte de conteúdo ativa.";

            return;
        }

        var count =
            mapCount ??
            _session.Maps.Count;

        RootText.Text =
            _session.IsStandaloneWorkspace
                ? $"Workspace · {count} mapa(s) · {root}"
                : $"OMSI · {count} mapa(s) · {root}";
    }

    private void SetContentRootModeLabel(
        string label)
    {
        RootModeText.Text =
            label;

        FullscreenRootModeText.Text =
            label;
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

        _desktopExplorerWasVisible =
            ExplorerPanel.Visibility ==
            Visibility.Visible &&
            ExplorerColumn.Width.Value >
            0;

        _desktopInspectorWasVisible =
            InspectorPanel.Visibility ==
            Visibility.Visible &&
            InspectorColumn.Width.Value >
            0;

        _appWindow.SetPresenter(
            AppWindowPresenterKind.FullScreen);

        DesktopChromePanel.Visibility =
            Visibility.Collapsed;

        FullscreenEditorBar.Visibility =
            Visibility.Visible;

        MainRoot.RowDefinitions[2].Height =
            new GridLength(0);

        EnterFullscreenPanelLayout();

        StatusText.Text =
            "Creator Focus ativo · painéis flutuantes podem ser movidos · F11 ou Esc para sair.";
    }

    private void EnterFullscreenPanelLayout()
    {
        ExplorerColumn.Width =
            new GridLength(0);

        ExplorerSplitterColumn.Width =
            new GridLength(0);

        InspectorSplitterColumn.Width =
            new GridLength(0);

        InspectorColumn.Width =
            new GridLength(0);

        ExplorerSplitter.Visibility =
            Visibility.Collapsed;

        InspectorSplitter.Visibility =
            Visibility.Collapsed;

        Grid.SetColumn(
            ExplorerPanel,
            0);

        Grid.SetColumnSpan(
            ExplorerPanel,
            5);

        ExplorerPanel.HorizontalAlignment =
            HorizontalAlignment.Left;

        ExplorerPanel.VerticalAlignment =
            VerticalAlignment.Top;

        var explorerMaxHeight =
            Math.Max(
                420,
                MainRoot.ActualHeight -
                96);

        ExplorerPanel.Width =
            Math.Clamp(
                _explorerPanelWidth,
                300,
                620);

        ExplorerPanel.MinHeight =
            360;

        ExplorerPanel.MaxHeight =
            explorerMaxHeight;

        ExplorerPanel.Height =
            Math.Clamp(
                _fullscreenExplorerHeight,
                360,
                explorerMaxHeight);

        ExplorerPanel.Margin =
            new Thickness(
                12,
                68,
                0,
                24);

        ExplorerResizeGrip.Visibility =
            Visibility.Visible;

        Canvas.SetZIndex(
            ExplorerPanel,
            65);

        Grid.SetColumn(
            InspectorPanel,
            0);

        Grid.SetColumnSpan(
            InspectorPanel,
            5);

        InspectorPanel.HorizontalAlignment =
            HorizontalAlignment.Right;

        InspectorPanel.VerticalAlignment =
            VerticalAlignment.Top;

        InspectorPanel.Width =
            Math.Clamp(
                _inspectorPanelWidth,
                310,
                440);

        InspectorPanel.MaxHeight =
            Math.Max(
                360,
                MainRoot.ActualHeight -
                210);

        InspectorPanel.Margin =
            new Thickness(
                0,
                68,
                12,
                128);

        Canvas.SetZIndex(
            InspectorPanel,
            65);

        FullscreenExplorerTranslate.X =
            _fullscreenExplorerOffsetX;

        FullscreenExplorerTranslate.Y =
            _fullscreenExplorerOffsetY;

        FullscreenInspectorTranslate.X =
            _fullscreenInspectorOffsetX;

        FullscreenInspectorTranslate.Y =
            _fullscreenInspectorOffsetY;

        ExplorerPanel.Visibility =
            _desktopExplorerWasVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

        InspectorPanel.Visibility =
            _desktopInspectorWasVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

        UpdatePanelRestoreButtons();
    }

    private void ExitFullscreen()
    {
        _fullscreenExplorerOffsetX =
            FullscreenExplorerTranslate.X;

        _fullscreenExplorerOffsetY =
            FullscreenExplorerTranslate.Y;

        _fullscreenInspectorOffsetX =
            FullscreenInspectorTranslate.X;

        _fullscreenInspectorOffsetY =
            FullscreenInspectorTranslate.Y;

        _draggingFullscreenExplorer =
            false;

        _draggingFullscreenInspector =
            false;

        _resizingFullscreenExplorer =
            false;

        FullscreenExplorerTranslate.X =
            0;

        FullscreenExplorerTranslate.Y =
            0;

        FullscreenInspectorTranslate.X =
            0;

        FullscreenInspectorTranslate.Y =
            0;

        _appWindow.SetPresenter(
            AppWindowPresenterKind.Default);

        DesktopChromePanel.Visibility =
            Visibility.Visible;

        FullscreenEditorBar.Visibility =
            Visibility.Collapsed;

        MainRoot.RowDefinitions[2].Height =
            new GridLength(30);

        Grid.SetColumn(
            ExplorerPanel,
            0);

        Grid.SetColumnSpan(
            ExplorerPanel,
            1);

        ExplorerPanel.HorizontalAlignment =
            HorizontalAlignment.Stretch;

        ExplorerPanel.VerticalAlignment =
            VerticalAlignment.Stretch;

        ExplorerPanel.Width =
            double.NaN;

        ExplorerPanel.Height =
            double.NaN;

        ExplorerPanel.MinHeight =
            0;

        ExplorerPanel.MaxHeight =
            double.PositiveInfinity;

        ExplorerResizeGrip.Visibility =
            Visibility.Collapsed;

        ExplorerPanel.Margin =
            new Thickness(8);

        Canvas.SetZIndex(
            ExplorerPanel,
            0);

        Grid.SetColumn(
            InspectorPanel,
            4);

        Grid.SetColumnSpan(
            InspectorPanel,
            1);

        InspectorPanel.HorizontalAlignment =
            HorizontalAlignment.Stretch;

        InspectorPanel.VerticalAlignment =
            VerticalAlignment.Stretch;

        InspectorPanel.Width =
            double.NaN;

        InspectorPanel.MaxHeight =
            double.PositiveInfinity;

        InspectorPanel.Margin =
            new Thickness(8);

        Canvas.SetZIndex(
            InspectorPanel,
            0);

        ExplorerSplitter.Visibility =
            Visibility.Visible;

        InspectorSplitter.Visibility =
            Visibility.Visible;

        if (_desktopExplorerWasVisible)
        {
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
        }
        else
        {
            ExplorerPanel.Visibility =
                Visibility.Collapsed;

            ExplorerColumn.Width =
                new GridLength(0);

            ExplorerSplitterColumn.Width =
                new GridLength(0);
        }

        if (_desktopInspectorWasVisible)
        {
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
        }
        else
        {
            InspectorPanel.Visibility =
                Visibility.Collapsed;

            InspectorColumn.Width =
                new GridLength(0);

            InspectorSplitterColumn.Width =
                new GridLength(0);
        }

        StatusText.Text =
            "Layout desktop World Builder restaurado.";
    }

    private void OnFullscreenGridClick(
        object sender,
        RoutedEventArgs e)
    {
        ShowGridMenuItem.IsChecked =
            !ShowGridMenuItem.IsChecked;

        OnGridVisibilityClick(
            sender,
            e);
    }

    private void OnFullscreenNightClick(
        object sender,
        RoutedEventArgs e)
    {
        NightPreviewCheckBox.IsChecked =
            NightPreviewCheckBox.IsChecked !=
            true;
    }

    private void OnResetFullscreenPanelsClick(
        object sender,
        RoutedEventArgs e)
    {
        _fullscreenExplorerOffsetX =
            0;

        _fullscreenExplorerOffsetY =
            0;

        _fullscreenInspectorOffsetX =
            0;

        _fullscreenInspectorOffsetY =
            0;

        FullscreenExplorerTranslate.X =
            0;

        FullscreenExplorerTranslate.Y =
            0;

        FullscreenInspectorTranslate.X =
            0;

        FullscreenInspectorTranslate.Y =
            0;

        ExplorerPanel.Visibility =
            Visibility.Visible;

        InspectorPanel.Visibility =
            Visibility.Visible;

        StatusText.Text =
            "Painéis flutuantes restaurados às bordas do Creator Focus.";
    }

    private void OnMoveGizmoClick(
        object sender,
        RoutedEventArgs e)
    {
        Viewport.SetGizmoMode(
            NativeGizmoMode.Move);

        StatusText.Text =
            "Mover ativo: arraste o próprio item no mapa para mover no plano, ou use os eixos do gizmo para ajuste preciso. Inspector não é necessário.";
    }

    private void OnRotateGizmoClick(
        object sender,
        RoutedEventArgs e)
    {
        Viewport.SetGizmoMode(
            NativeGizmoMode.Rotate);

        StatusText.Text =
            "Girar ativo: arraste o próprio item selecionado ou o anel do gizmo. Inspector não é necessário.";
    }

    private async void
        OnMainWindowActivatedInitializeWorkspace(
            object sender,
            WindowActivatedEventArgs e)
    {
        if (_standaloneWorkspaceInitialized)
        {
            return;
        }

        _standaloneWorkspaceInitialized =
            true;

        await ActivateStandaloneWorkspaceAsync(
            announce:
                false);
    }

    private async Task
        ActivateStandaloneWorkspaceAsync(
            bool announce)
    {
        BeginLoading(
            "Inicializando Workspace Map Studio",
            "Preparando catálogo, biblioteca e editor standalone...");

        try
        {
            StatusText.Text =
                "Inicializando Workspace Map Studio...";

            var maps =
                await _session
                    .SelectStandaloneWorkspaceAsync();

            var root =
                _session.OmsiRootPath!;

            SetContentRootModeLabel(
                "WORKSPACE");

            UpdateContentRootSummary(
                maps.Count);

            OpenMapButton.IsEnabled =
                true;

            OpenMapMenuItem.IsEnabled =
                true;

            RefreshMapCatalogMenuItem.IsEnabled =
                true;

            RefreshLibraryButton.IsEnabled =
                true;

            UpdateLoading(
                "Indexando biblioteca do Workspace",
                "Localizando SCO, SLI, modelos e texturas...");

            var progress =
                new Progress<
                    OmsiAssetIndexProgress>(
                    value =>
                    {
                        var detail =
                            $"Indexando Workspace... {value.ExaminedFiles} arquivos · {value.CandidateFiles} assets";

                        LibraryStatusText.Text =
                            detail;

                        UpdateLoading(
                            "Indexando biblioteca do Workspace",
                            detail);
                    });

            await _session
                .RefreshAssetLibraryAsync(
                    progress);

            UpdateLoading(
                "Preparando editor",
                $"{maps.Count} mapa(s) encontrados · carregando biblioteca...");

            await LoadAssetLibraryAsync();

            StatusText.Text =
                announce
                    ? "Workspace Map Studio ativo. O OMSI é opcional."
                    : "Workspace Map Studio pronto · editor standalone ativo.";
        }
        catch (Exception exception)
        {
            SetContentRootModeLabel(
                "WORKSPACE !");

            StatusText.Text =
                $"Falha ao inicializar Workspace: {exception.Message}";
        }
        finally
        {
            EndLoading();
        }
    }

    private async void OnOpenWorkspaceClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as alterações pendentes antes de trocar para o Workspace.";

            return;
        }

        await ActivateStandaloneWorkspaceAsync(
            announce:
                true);
    }

    private async void OnCreateStandaloneMapClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as alterações pendentes antes de criar outro mapa.";

            return;
        }

        var directoryBox =
            new TextBox
            {
                Header =
                    "Pasta do projeto",
                PlaceholderText =
                    "Ex.: Minha_Cidade"
            };

        var displayNameBox =
            new TextBox
            {
                Header =
                    "Nome do mapa",
                PlaceholderText =
                    "Ex.: Minha Cidade"
            };

        var note =
            new TextBlock
            {
                Text =
                    "O mapa será criado no Workspace Map Studio com terreno inicial e formato compatível com o pipeline OMSI. O jogo não precisa estar instalado.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    10,
                MinWidth =
                    420
            };

        panel.Children.Add(
            directoryBox);

        panel.Children.Add(
            displayNameBox);

        panel.Children.Add(
            note);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Novo mapa",
                Content =
                    panel,
                PrimaryButtonText =
                    "Criar e abrir",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Criando mapa no Workspace...";

            if (!_session.IsStandaloneWorkspace)
            {
                await ActivateStandaloneWorkspaceAsync(
                    announce:
                        false);
            }

            var snapshot =
                await _session
                    .CreateStandaloneMapAsync(
                        directoryBox.Text,
                        displayNameBox.Text);

            _fullMapMode =
                true;

            await ApplyMapSnapshotAsync(
                snapshot,
                focusActiveTile:
                    false);

            SetContentRootModeLabel(
                "WORKSPACE");

            UpdateContentRootSummary();

            StatusText.Text =
                $"Mapa “{snapshot.Map.DisplayName}” criado e aberto sem depender do OMSI.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao criar mapa: {exception.Message}";
        }
    }

    private async void OnImportStandaloneMapClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as alterações pendentes antes de importar outro mapa.";

            return;
        }

        var source =
            await PickFolderAsync();

        if (string.IsNullOrWhiteSpace(
                source))
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Importando mapa externo para o Workspace...";

            var snapshot =
                await _session
                    .ImportStandaloneMapFolderAsync(
                        source);

            _fullMapMode =
                true;

            SetContentRootModeLabel(
                "WORKSPACE");

            UpdateContentRootSummary();

            await ApplyMapSnapshotAsync(
                snapshot,
                focusActiveTile:
                    false);

            StatusText.Text =
                $"Mapa “{snapshot.Map.DisplayName}” importado e aberto no Workspace. Adicione as pastas de itens caso existam dependências externas.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao importar mapa: {exception.Message}";
        }
    }

    private async void OnAddAssetFolderClick(
        object sender,
        RoutedEventArgs e)
    {
        var source =
            await PickFolderAsync();

        if (string.IsNullOrWhiteSpace(
                source))
        {
            return;
        }

        try
        {
            if (!_session.IsStandaloneWorkspace)
            {
                await ActivateStandaloneWorkspaceAsync(
                    announce:
                        false);
            }

            StatusText.Text =
                "Importando pasta de itens para o Workspace...";

            var result =
                await _session
                    .ImportWorkspaceAssetFolderAsync(
                        source);

            await LoadAssetLibraryAsync();

            StatusText.Text =
                $"Pasta adicionada: {result.CopiedFiles} arquivo(s) importado(s) em {result.DestinationDirectories.Count} pacote(s).";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao adicionar pasta de itens: {exception.Message}";
        }
    }

    private async void OnExportWorkspaceOmsiPackageClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !_session.IsStandaloneWorkspace ||
            _session.OmsiRootPath is not
                { } workspaceRoot ||
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Exportar pacote OMSI: abra um mapa do Workspace Map Studio primeiro.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as alterações pendentes antes de exportar o pacote OMSI.";

            return;
        }

        var destination =
            await PickFolderAsync();

        if (
            string.IsNullOrWhiteSpace(
                destination))
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Exportando mapa e assets do Workspace para um pacote OMSI seguro...";

            var result =
                await new MapStudioWorkspaceOmsiPackageExporter()
                    .ExportAsync(
                        workspaceRoot,
                        snapshot.Map,
                        destination);

            StatusText.Text =
                $"Pacote OMSI exportado: {result.CopiedFiles} arquivo(s) · {result.PackageRoot}. " +
                "O OMSI original não foi alterado.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Exportar pacote OMSI falhou: {exception.Message}";
        }
    }

    private async void OnOpenOmsiClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.PendingTransformCount >
                0)
        {
            StatusText.Text =
                "Salve as alterações pendentes antes de trocar para a fonte OMSI.";

            return;
        }

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

            SetContentRootModeLabel(
                "OMSI");

            UpdateContentRootSummary(
                maps.Count);

            OpenMapButton.IsEnabled =
                true;

            OpenMapMenuItem.IsEnabled =
                true;

            RefreshMapCatalogMenuItem.IsEnabled =
                true;

            RefreshLibraryButton.IsEnabled =
                true;

            await LoadAssetLibraryAsync();

            StatusText.Text =
                "Fonte OMSI ativa. Use “Workspace” para voltar ao editor standalone.";
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

            BeginLoading(
                fullMap
                    ? "Carregando mapa completo"
                    : "Carregando região 3×3",
                "Lendo tiles, terreno e dependências...");

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

            EndLoading();
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

            BeginLoading(
                _fullMapMode
                    ? $"Focando tile {tileX},{tileY}"
                    : $"Carregando tile {tileX},{tileY}",
                _fullMapMode
                    ? "Atualizando câmera e seleção..."
                    : "Atualizando a região 3×3 do viewport...");

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

            EndLoading();
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

        UpdateLoading(
            "Renderizando mapa",
            $"{snapshot.Tiles.Count} tile(s) · {snapshot.ObjectCount} objeto(s) · {snapshot.SplineCount} spline(s)");

        await Viewport
            .SetMapSnapshotAsync(
                snapshot,
                _session.OmsiRootPath);

        RefreshTerrainLayerVisibilityMenu(
            snapshot);

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
            $"{snapshot.Tiles.Count}/{snapshot.Map.Tiles.Count} tiles · " +
            $"{snapshot.ObjectCount} objetos · {snapshot.SplineCount} splines · " +
            $"tile {activeTile}";

        FullscreenMapTitleText.Text =
            snapshot.Map.DisplayName;

        MapLoadModeText.Text =
            _fullMapMode
                ? "Completo"
                : "3×3";

        if (
            snapshot.ActiveTile is
                { } active)
        {
            TileNavigatorXBox.Value =
                active.X;

            TileNavigatorYBox.Value =
                active.Y;
        }

        if (_mapMode)
        {
            RefreshMapExplorer();
        }
    }

    private async void OnTileManagerClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_tileManagerDialogOpen)
        {
            StatusText.Text =
                "Gerenciador de tiles já está aberto.";

            return;
        }

        _tileManagerDialogOpen =
            true;

        try
        {
            await ShowTileManagerAsync();
        }
        catch (Exception exception)
        {
            NativeStartupDiagnostics.Write(
                $"Tile manager failure type={exception.GetType().FullName} hresult=0x{exception.HResult:X8} message={exception.Message}");

            NativeStartupDiagnostics.Write(
                exception.ToString());

            StatusText.Text =
                $"Falha ao abrir o Gerenciador de tiles: {exception.Message}";
        }
        finally
        {
            _tileManagerDialogOpen =
                false;
        }
    }

    private async Task ShowTileManagerAsync()
    {
        var snapshot =
            _session.CurrentMap;

        if (snapshot is null)
        {
            StatusText.Text =
                "Abra um mapa antes de usar o Gerenciador de tiles.";

            return;
        }

        var loadedGroups =
            snapshot.Tiles
                .GroupBy(
                    tile =>
                        (
                            tile.Reference.X,
                            tile.Reference.Y
                        ))
                .ToArray();

        var loadedByCoordinate =
            loadedGroups
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First());

        var duplicateLoadedCoordinates =
            loadedGroups
                .Sum(
                    group =>
                        Math.Max(
                            0,
                            group.Count() - 1));

        var mapGroups =
            snapshot.Map.Tiles
                .GroupBy(
                    tile =>
                        (
                            tile.X,
                            tile.Y
                        ))
                .OrderBy(
                    group =>
                        group.Key.Y)
                .ThenBy(
                    group =>
                        group.Key.X)
                .ToArray();

        var duplicateMapCoordinates =
            mapGroups
                .Sum(
                    group =>
                        Math.Max(
                            0,
                            group.Count() - 1));

        var items =
            mapGroups
                .Select(
                    group =>
                    {
                        var tile =
                            group.First();

                        var isActive =
                            snapshot.ActiveTile?.X ==
                                tile.X &&
                            snapshot.ActiveTile?.Y ==
                                tile.Y;

                        var isLoaded =
                            loadedByCoordinate
                                .TryGetValue(
                                    (
                                        tile.X,
                                        tile.Y
                                    ),
                                    out var loaded);

                        var detail =
                            isLoaded &&
                            loaded is not null
                                ? $"objetos {loaded.Content.Objects.Count} · splines {loaded.Content.Splines.Count}"
                                : "fora da região carregada";

                        if (group.Count() > 1)
                        {
                            detail +=
                                $" · {group.Count()} referências no global.cfg";
                        }

                        return new TileManagerViewItem(
                            tile.X,
                            tile.Y,
                            $"{(isActive ? "●" : "○")} Tile {tile.X},{tile.Y} · {detail}",
                            isActive,
                            isLoaded);
                    })
                .ToArray();

        var list =
            new ListView
            {
                Height =
                    Math.Min(
                        460,
                        Math.Max(
                            180,
                            items.Length *
                                42)),
                SelectionMode =
                    ListViewSelectionMode
                        .Single,
                DisplayMemberPath =
                    nameof(
                        TileManagerViewItem
                            .DisplayText),
                ItemsSource =
                    items
            };

        list.SelectedItem =
            items.FirstOrDefault(
                item =>
                    item.IsActive);

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    520
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"{snapshot.Map.Tiles.Count} tile(s) no mapa · {snapshot.Tiles.Count} carregado(s) no viewport",
                FontSize =
                    16,
                FontWeight =
                    Microsoft.UI.Text
                        .FontWeights
                        .SemiBold
            });

        panel.Children.Add(
            list);

        var duplicateMessage =
            duplicateMapCoordinates > 0 ||
            duplicateLoadedCoordinates > 0
                ? $" Foram detectadas {duplicateMapCoordinates} referência(s) duplicada(s) no catálogo e {duplicateLoadedCoordinates} tile(s) carregado(s) com coordenada repetida; o gerenciador consolidou essas entradas para evitar falha."
                : string.Empty;

        panel.Children.Add(
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    duplicateMessage.Length > 0
                        ? InfoBarSeverity.Warning
                        : InfoBarSeverity.Informational,
                Title =
                    duplicateMessage.Length > 0
                        ? "Mapa contém coordenadas de tile repetidas"
                        : "Gerenciamento seguro",
                Message =
                    "Use Criar tile para expandir o mapa. Excluir tile continua restrito a tiles vazios e seguros para evitar deslocar índices usados por entrypoints ou dados operacionais." +
                    duplicateMessage
            });

        var xamlRoot =
            MainRoot.XamlRoot;

        if (xamlRoot is null)
        {
            StatusText.Text =
                "Gerenciador de tiles indisponível: a janela ainda não terminou de carregar.";

            NativeStartupDiagnostics.Write(
                "Tile manager aborted because MainRoot.XamlRoot is null.");

            return;
        }

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    xamlRoot,
                Title =
                    "Gerenciador de tiles",
                Content =
                    panel,
                PrimaryButtonText =
                    "Focar tile",
                CloseButtonText =
                    "Fechar",
                DefaultButton =
                    ContentDialogButton
                        .Primary,
                IsPrimaryButtonEnabled =
                    list.SelectedItem is
                    TileManagerViewItem
            };

        list.SelectionChanged +=
            (_, _) =>
            {
                dialog.IsPrimaryButtonEnabled =
                    list.SelectedItem is
                    TileManagerViewItem;
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary ||
            list.SelectedItem is not
                TileManagerViewItem selected)
        {
            return;
        }

        await NavigateToTileAsync(
            selected.X,
            selected.Y);
    }

    private async void OnDeleteActiveMapTileClick(
        object sender,
        RoutedEventArgs e)
    {
        var snapshot =
            _session.CurrentMap;

        var activeTile =
            snapshot?.ActiveTile;

        if (
            snapshot is null ||
            activeTile is null)
        {
            StatusText.Text =
                "Abra um mapa e selecione um tile antes de excluir.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de excluir um tile.";

            return;
        }

        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            StatusText.Text =
                "Cancele a ferramenta de posicionamento antes de excluir um tile.";

            return;
        }

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    $"Excluir tile {activeTile.X},{activeTile.Y}?",
                Content =
                    "Por segurança, o Map Studio só exclui um tile vazio que seja a última entrada [map] e que não esteja referenciado por entrypoints. Todos os arquivos do tile e o global.cfg serão copiados para backup antes da remoção.",
                PrimaryButtonText =
                    "Excluir tile",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Close
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        try
        {
            StatusText.Text =
                $"Validando e excluindo tile {activeTile.X},{activeTile.Y}...";

            var result =
                await _session
                    .DeleteTileSafelyAsync(
                        activeTile.X,
                        activeTile.Y);

            await ApplyMapSnapshotAsync(
                result.Snapshot,
                focusActiveTile:
                    true);

            UpdateContentRootSummary();

            StatusText.Text =
                $"Tile {result.DeletedTile.X},{result.DeletedTile.Y} excluído com {result.DeletedFiles} arquivo(s) preservados no backup: {result.BackupDirectory}";
        }
        catch (Exception exception)
        {
            var message =
                exception.Message switch
                {
                    var value when value.Contains(
                        "cannotDeleteOnlyMapTile",
                        StringComparison.Ordinal) =>
                        "O único tile do mapa não pode ser excluído.",

                    var value when value.Contains(
                        "tileDeletionWouldShiftMapIndices",
                        StringComparison.Ordinal) =>
                        "Esse tile não é a última entrada do global.cfg. A exclusão foi bloqueada para não deslocar índices usados pelo mapa.",

                    var value when value.Contains(
                        "tileNotEmpty",
                        StringComparison.Ordinal) =>
                        "O tile contém objetos ou splines. Remova/mova o conteúdo antes de excluir o tile.",

                    var value when value.Contains(
                        "tileReferencedByEntrypoint",
                        StringComparison.Ordinal) =>
                        "O tile está referenciado por um entrypoint e não pode ser excluído com segurança.",

                    var value when value.Contains(
                        "globalMapSectionsNotCanonical",
                        StringComparison.Ordinal) =>
                        "O global.cfg possui entradas [map] não canônicas; revise o mapa antes da exclusão.",

                    _ =>
                        exception.Message
                };

            StatusText.Text =
                $"Falha ao excluir tile: {message}";
        }
    }

    private async void OnCreateMapTileClick(
        object sender,
        RoutedEventArgs e)
    {
        var snapshot =
            _session.CurrentMap;

        if (snapshot is null)
        {
            StatusText.Text =
                "Abra um mapa antes de criar um tile.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de criar um tile.";

            return;
        }

        if (
            Viewport.IsSceneryPlacementActive ||
            Viewport.IsSplinePlacementActive)
        {
            StatusText.Text =
                "Cancele a ferramenta de posicionamento antes de criar um tile.";

            return;
        }

        var baseTile =
            snapshot.ActiveTile ??
            OmsiTileRegionSelector
                .FindInitialTile(
                    snapshot.Map.Tiles);

        var tileXBox =
            new NumberBox
            {
                Header =
                    "Tile X",
                Minimum =
                    -100000,
                Maximum =
                    100000,
                Value =
                    (baseTile?.X ?? 0) +
                    1,
                SmallChange =
                    1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var tileYBox =
            new NumberBox
            {
                Header =
                    "Tile Y",
                Minimum =
                    -100000,
                Maximum =
                    100000,
                Value =
                    baseTile?.Y ??
                    0,
                SmallChange =
                    1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var grid =
            new Grid
            {
                ColumnSpacing =
                    8
            };

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            tileXBox,
            0);

        Grid.SetColumn(
            tileYBox,
            1);

        grid.Children.Add(
            tileXBox);

        grid.Children.Add(
            tileYBox);

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    420
            };

        panel.Children.Add(
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    InfoBarSeverity
                        .Informational,
                Title =
                    "Tile OMSI real",
                Message =
                    "O tile será criado a partir do template oficial NewMap do OMSI. O global.cfg será anexado sem reordenar tiles existentes e terá backup automático."
            });

        panel.Children.Add(
            grid);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Criar tile",
                Content =
                    panel,
                PrimaryButtonText =
                    "Criar",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        if (
            !double.IsFinite(
                tileXBox.Value) ||
            !double.IsFinite(
                tileYBox.Value))
        {
            StatusText.Text =
                "Coordenadas de tile inválidas.";

            return;
        }

        var tileX =
            checked(
                (int)Math.Round(
                    tileXBox.Value));

        var tileY =
            checked(
                (int)Math.Round(
                    tileYBox.Value));

        if (
            Math.Abs(
                tileXBox.Value -
                tileX) >
                0.0001 ||
            Math.Abs(
                tileYBox.Value -
                tileY) >
                0.0001)
        {
            StatusText.Text =
                "As coordenadas do tile precisam ser números inteiros.";

            return;
        }

        try
        {
            StatusText.Text =
                $"Criando tile {tileX},{tileY} a partir do template da fonte ativa...";

            var result =
                await _session
                    .CreateTileFromTemplateAsync(
                        tileX,
                        tileY);

            await ApplyMapSnapshotAsync(
                result.Snapshot,
                focusActiveTile:
                    true);

            UpdateContentRootSummary();

            StatusText.Text =
                $"Tile {result.Tile.X},{result.Tile.Y} criado com {result.CreatedFiles.Count} arquivo(s). Backup: {result.BackupDirectory}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao criar tile: {exception.Message}";
        }
    }

    private async void OnCreateCoordinateMapClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_session.OmsiRootPath is null)
        {
            StatusText.Text =
                "Ative o Workspace Map Studio ou selecione uma instalação do OMSI para criar um mapa.";

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
                "Criando mapa a partir do template da fonte ativa...";

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

            UpdateContentRootSummary();

            StatusText.Text =
                $"Mapa {created.Snapshot.Map.DisplayName} criado em {created.DirectoryPath} · âncora {created.Latitude:F6}, {created.Longitude:F6}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao criar mapa por coordenadas: {exception.Message}";
        }
    }

    private async void OnMapReferenceClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "Referência de mapa: abra um mapa primeiro.";
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

        var providerBox =
            new ComboBox
            {
                Header =
                    "Provedor",
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                SelectedIndex =
                    0
            };

        providerBox.Items.Add(
            "OpenStreetMap · sem chave");

        providerBox.Items.Add(
            "Google Maps · API key");

        var apiKeyBox =
            new PasswordBox
            {
                Header =
                    "Google Maps Platform API key",
                PlaceholderText =
                    NativeMapCredentialStore
                        .HasGoogleMapsApiKey()
                        ? "Chave já salva no Windows · deixe vazio para reutilizar"
                        : "Necessária somente para Google Maps",
                IsEnabled =
                    false
            };

        var saveKeyCheckBox =
            new CheckBox
            {
                Content =
                    "Salvar chave Google com segurança no Windows",
                IsEnabled =
                    false,
                IsChecked =
                    true
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

        var providerInfo =
            new TextBlock
            {
                Text =
                    "OpenStreetMap usa o tile necessário da posição atual, com atribuição e cache local. Google Maps usa Maps Static API e requer chave/billing.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.8
            };

        providerBox.SelectionChanged +=
            (_, _) =>
            {
                var google =
                    providerBox.SelectedIndex ==
                        1;

                apiKeyBox.IsEnabled =
                    google;

                saveKeyCheckBox.IsEnabled =
                    google;
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    470
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"Centro: {georeference.Latitude:F6}, {georeference.Longitude:F6} · zoom {georeference.Zoom}",
                TextWrapping =
                    TextWrapping.Wrap
            });

        panel.Children.Add(
            providerInfo);

        panel.Children.Add(
            providerBox);

        panel.Children.Add(
            apiKeyBox);

        panel.Children.Add(
            saveKeyCheckBox);

        panel.Children.Add(
            opacityBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Referência de mapa sobre o terreno",
                Content =
                    panel,
                PrimaryButtonText =
                    "Carregar",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        if (
            !double.IsFinite(
                opacityBox.Value))
        {
            StatusText.Text =
                "Opacidade inválida.";
            return;
        }

        try
        {
            NativeGoogleMapReference reference;

            if (
                providerBox.SelectedIndex ==
                    1)
            {
                var apiKey =
                    string.IsNullOrWhiteSpace(
                        apiKeyBox.Password)
                        ? NativeMapCredentialStore
                            .TryGetGoogleMapsApiKey()
                        : apiKeyBox.Password.Trim();

                if (
                    string.IsNullOrWhiteSpace(
                        apiKey))
                {
                    StatusText.Text =
                        "Google Maps: informe uma API key ou use OpenStreetMap.";
                    return;
                }

                StatusText.Text =
                    "Carregando referência Google Maps...";

                reference =
                    await _session
                        .LoadGoogleMapReferenceAsync(
                            apiKey);

                if (
                    saveKeyCheckBox
                        .IsChecked ==
                    true)
                {
                    NativeMapCredentialStore
                        .SaveGoogleMapsApiKey(
                            apiKey);
                }
            }
            else
            {
                StatusText.Text =
                    "Carregando referência OpenStreetMap...";

                reference =
                    await _session
                        .LoadOpenStreetMapReferenceAsync();
            }

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
                snapshot.Map.DirectoryPath;

            _activeGoogleMapReference =
                reference;

            MapReferenceAttributionText.Text =
                reference.Attribution;

            MapReferenceAttributionBorder.Visibility =
                Visibility.Visible;

            StatusText.Text =
                $"Referência ativa: {reference.Attribution} · {reference.WidthMeters:F1} × {reference.HeightMeters:F1} m · {reference.MetersPerPixel:F3} m/pixel · opacidade {opacity:P0}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao carregar referência de mapa: {exception.Message}";
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

            _activeGoogleMapReference =
                reference;

            MapReferenceAttributionText.Text =
                reference.Attribution;

            MapReferenceAttributionBorder.Visibility =
                Visibility.Visible;

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
            "Referência de mapa removida do viewport.";
    }

    private void ClearReferenceOverlay()
    {
        Viewport.SetReferenceOverlay(
            null);

        _referenceOverlayMapDirectory =
            null;

        _activeGoogleMapReference =
            null;

        MapReferenceAttributionText.Text =
            string.Empty;

        MapReferenceAttributionBorder.Visibility =
            Visibility.Collapsed;
    }

    private async void OnImportLocalElevationGridClick(
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
                "Elevação local: abra um mapa e mantenha um tile ativo.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de importar elevação.";

            return;
        }

        var picker =
            new FileOpenPicker
            {
                SuggestedStartLocation =
                    PickerLocationId
                        .DocumentsLibrary
            };

        picker.FileTypeFilter.Add(
            ".csv");
        picker.FileTypeFilter.Add(
            ".txt");
        picker.FileTypeFilter.Add(
            ".asc");

        InitializeWithWindow.Initialize(
            picker,
            _windowHandle);

        var file =
            await picker
                .PickSingleFileAsync();

        if (file is null)
        {
            return;
        }

        MapStudioElevationGrid grid;

        try
        {
            StatusText.Text =
                "Lendo grade local de elevação...";

            grid =
                await new MapStudioElevationGridReader()
                    .ReadAsync(
                        file.Path);
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao ler elevação local: {exception.Message}";

            return;
        }

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
                    1,
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
                    450
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"Arquivo: {file.Name}\n" +
                    $"Formato: {grid.SourceFormat}\n" +
                    $"Grade: {grid.Rows} × {grid.Columns} ({grid.SampleCount:N0} amostras)\n" +
                    $"Elevação: {grid.MinimumElevation:F2} m → {grid.MaximumElevation:F2} m\n" +
                    $"Destino: tile {active.X},{active.Y}",
                TextWrapping =
                    TextWrapping
                        .Wrap
            });

        panel.Children.Add(
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    InfoBarSeverity
                        .Informational,
                Title =
                    "Interpolação para o terreno OMSI",
                Message =
                    "A grade será redimensionada/interpolada para a malha .terrain do tile ativo usando o mesmo pipeline seguro da elevação Google. CSV/TXT representam diretamente a área de 300 × 300 m do tile; arquivos ESRI ASCII têm os valores de altura lidos, mas CRS/georreferência externa ainda não são aplicados nesta versão."
            });

        panel.Children.Add(
            offsetBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Importar grade local de elevação",
                Content =
                    panel,
                PrimaryButtonText =
                    "Aplicar ao tile",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        if (
            !double.IsFinite(
                offsetBox.Value))
        {
            StatusText.Text =
                "Offset de elevação inválido.";

            return;
        }

        try
        {
            StatusText.Text =
                $"Aplicando grade {grid.Rows}×{grid.Columns} ao tile {active.X},{active.Y}...";

            var result =
                await _session
                    .ApplyLocalTerrainElevationGridAsync(
                        active.X,
                        active.Y,
                        grid,
                        offsetBox.Value);

            await ApplyMapSnapshotAsync(
                result.Snapshot,
                focusActiveTile:
                    false);

            StatusText.Text =
                result.ChangedSamples >
                    0
                    ? $"Elevação local aplicada: {result.ChangedSamples:N0} amostra(s) alteradas. Backup: {result.BackupDirectory}"
                    : "A grade local não alterou nenhuma amostra do terreno.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao aplicar elevação local: {exception.Message}";
        }
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
                    NativeMapCredentialStore
                        .HasGoogleMapsApiKey()
                        ? "Chave já salva no Windows · deixe vazio para reutilizar"
                        : "Informe a chave Google Maps Platform"
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

        var apiKey =
            string.IsNullOrWhiteSpace(
                apiKeyBox.Password)
                ? NativeMapCredentialStore
                    .TryGetGoogleMapsApiKey()
                : apiKeyBox.Password.Trim();

        if (
            string.IsNullOrWhiteSpace(
                apiKey) ||
            !double.IsFinite(
                samplesBox.Value) ||
            !double.IsFinite(
                offsetBox.Value))
        {
            StatusText.Text =
                "Google Elevation: informe uma API key ou salve a chave pela Referência de mapa.";

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
                        apiKey,
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

            if (
                result.ChangedSamples >
                0)
            {
                RegisterConstructionHistory(
                    "Aplicar relevo real");
            }

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

    private async void OnRefreshMapCatalogClick(
        object sender,
        RoutedEventArgs e)
    {
        var root =
            _session.OmsiRootPath;

        if (root is null)
        {
            StatusText.Text =
                "Ative o Workspace Map Studio ou abra uma instalação do OMSI.";

            return;
        }

        try
        {
            RefreshMapCatalogMenuItem.IsEnabled =
                false;

            StatusText.Text =
                "Atualizando catálogo de mapas instalados...";

            var maps =
                await _session
                    .RefreshMapCatalogAsync();

            UpdateContentRootSummary(
                maps.Count);

            StatusText.Text =
                $"Catálogo atualizado: {maps.Count} mapa(s) encontrado(s). O mapa aberto foi preservado.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao atualizar catálogo: {exception.Message}";
        }
        finally
        {
            RefreshMapCatalogMenuItem.IsEnabled =
                _session.OmsiRootPath is not
                    null;
        }
    }

    private async void OnOpenMapCatalogClick(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
        if (_session.OmsiRootPath is null)
        {
            StatusText.Text =
                "Ative o Workspace Map Studio ou abra uma instalação do OMSI.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de abrir outro mapa.";

            return;
        }

        var maps =
            _session.Maps
                .OrderBy(
                    map =>
                        map.DisplayName,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ThenBy(
                    map =>
                        map.DirectoryName,
                    StringComparer
                        .OrdinalIgnoreCase)
                .Select(
                    map =>
                        new MapCatalogViewItem(
                            map,
                            $"{map.DisplayName} · {map.DirectoryName} · {map.Tiles.Count} tile(s)"))
                .ToArray();

        if (maps.Length == 0)
        {
            StatusText.Text =
                "Nenhum mapa OMSI foi encontrado no catálogo.";

            return;
        }

        var searchBox =
            new TextBox
            {
                Header =
                    "Buscar mapa",
                PlaceholderText =
                    "Nome, pasta ou caminho..."
            };

        var list =
            new ListView
            {
                Height =
                    360,
                SelectionMode =
                    ListViewSelectionMode
                        .Single,
                DisplayMemberPath =
                    nameof(
                        MapCatalogViewItem
                            .DisplayText),
                ItemsSource =
                    maps
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    620
            };

        panel.Children.Add(
            searchBox);

        panel.Children.Add(
            list);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Mapas instalados",
                Content =
                    panel,
                PrimaryButtonText =
                    "Abrir",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary,
                IsPrimaryButtonEnabled =
                    false
            };

        list.SelectionChanged +=
            (_, _) =>
            {
                dialog.IsPrimaryButtonEnabled =
                    list.SelectedItem is
                    MapCatalogViewItem;
            };

        searchBox.TextChanged +=
            (_, _) =>
            {
                var query =
                    searchBox.Text
                        .Trim();

                list.ItemsSource =
                    string.IsNullOrWhiteSpace(
                        query)
                        ? maps
                        : maps
                            .Where(
                                item =>
                                    item.Map.DisplayName
                                        .Contains(
                                            query,
                                            StringComparison
                                                .OrdinalIgnoreCase) ||
                                    item.Map.DirectoryName
                                        .Contains(
                                            query,
                                            StringComparison
                                                .OrdinalIgnoreCase) ||
                                    item.Map.DirectoryPath
                                        .Contains(
                                            query,
                                            StringComparison
                                                .OrdinalIgnoreCase))
                            .ToArray();

                list.SelectedItem =
                    null;
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary ||
            list.SelectedItem is not
                MapCatalogViewItem selected)
        {
            return;
        }

        await OpenMapDirectoryAsync(
            selected.Map.DirectoryPath);
    
        }
        catch (Exception exception)
        {
            NativeStartupDiagnostics.Write(
                $"Open map catalog failure type={exception.GetType().FullName} hresult=0x{exception.HResult:X8} message={exception.Message}");

            NativeStartupDiagnostics.Write(
                exception.ToString());

            StatusText.Text =
                $"Falha ao abrir catálogo de mapas: {exception.Message}";
        }
    }

    private async Task OpenMapDirectoryAsync(
        string mapDirectory)
    {
        BeginLoading(
            "Carregando mapa",
            Path.GetFileName(
                mapDirectory));

        try
        {
            _fullMapMode =
                true;

            StatusText.Text =
                "Carregando mapa completo...";

            var snapshot =
                await _session
                    .OpenMapAsync(
                        mapDirectory,
                        loadFullMap: true);

            UpdateLoading(
                "Montando viewport nativo",
                $"{snapshot.Tiles.Count} tile(s) · preparando terreno, objetos, splines e texturas");

            await ApplyMapSnapshotAsync(
                snapshot,
                focusActiveTile: false);

            StatusText.Text =
                $"Mapa {snapshot.Map.DisplayName} carregado pelo MapStudio.Core · " +
                $"{snapshot.Tiles.Count} tiles.";
        }
        finally
        {
            EndLoading();
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
                    "Ative o Workspace Map Studio ou abra uma instalação do OMSI.";

                return;
            }

            if (
                _session.PendingTransformCount >
                0)
            {
                StatusText.Text =
                    "Salve as transformações pendentes antes de abrir outro mapa.";

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

            await OpenMapDirectoryAsync(
                mapDirectory);
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
                    GetSelectedAssetLibraryEntry() is
                        { } selectedAsset &&
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

            RegisterConstructionHistory(
                "Construction Set");

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
            GetSelectedAssetLibraryEntry() is
                { } selectedAsset &&
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

    private static IReadOnlyList<RoadProfileOption>
        GetProceduralRoadProfiles() =>
        [
            new(
                "Mão única 1 faixa · 3,5 m",
                @"Splines\MapStudio_RoadKit\ms_road_oneway_3_5m.sli",
                1,
                true,
                3.5),
            new(
                "Mão única 2 faixas · 7 m",
                @"Splines\MapStudio_RoadKit\ms_road_oneway_2lane_7m.sli",
                2,
                true,
                7.0),
            new(
                "Mão única 3 faixas · 10,5 m",
                @"Splines\MapStudio_RoadKit\ms_road_oneway_3lane_10_5m.sli",
                3,
                true,
                10.5),
            new(
                "Rua 2 faixas · 7 m",
                @"Splines\MapStudio_RoadKit\ms_road_2lane_7m.sli",
                2,
                false,
                7.0),
            new(
                "Rua 2 faixas + calçada",
                @"Splines\MapStudio_RoadKit\ms_road_2lane_7m_sidewalk.sli",
                2,
                false,
                11.0),
            new(
                "Avenida 4 faixas + calçada",
                @"Splines\MapStudio_RoadKit\ms_avenue_4lane_14m_sidewalk.sli",
                4,
                false,
                18.0),
            new(
                "Avenida dividida 4 faixas",
                @"Splines\MapStudio_RoadKit\ms_avenue_divided_4lane.sli",
                4,
                false,
                20.0),
            new(
                "Via de pedestres · 3 m",
                @"Splines\MapStudio_RoadKit\ms_pedestrian_3m.sli",
                0,
                false,
                3.0)
        ];

    private static RoadProfileOption
        SelectProceduralRoadProfile(
            MapStudioGeoRoadTrace road,
            IReadOnlyList<RoadProfileOption> profiles)
    {
        var highway =
            road.Highway
                ?.Trim()
                .ToLowerInvariant();

        if (
            highway is
                "footway" or
                "pedestrian" or
                "path" or
                "steps" or
                "cycleway")
        {
            return profiles[7];
        }

        if (road.OneWay == true)
        {
            return road.LaneCount switch
            {
                >= 3 =>
                    profiles[2],
                2 =>
                    profiles[1],
                _ =>
                    profiles[0]
            };
        }

        if (
            highway is
                "motorway" or
                "trunk" or
                "motorway_link" or
                "trunk_link")
        {
            return profiles[6];
        }

        if (
            road.LaneCount is >= 4 ||
            highway is
                "primary" or
                "primary_link")
        {
            return profiles[5];
        }

        if (
            highway is
                "service")
        {
            return profiles[3];
        }

        return profiles[4];
    }

    private static RoadProfileOption
        SelectProceduralRoadProfile(
            MapStudioProjectedRoadReference road,
            IReadOnlyList<RoadProfileOption> profiles)
    {
        var kind =
            road.Kind
                ?.Trim()
                .ToLowerInvariant();

        if (
            kind is
                "footway" or
                "pedestrian" or
                "path" or
                "steps" or
                "cycleway")
        {
            return profiles[7];
        }

        if (road.OneWay == true)
        {
            return road.LaneCount switch
            {
                >= 3 =>
                    profiles[2],
                2 =>
                    profiles[1],
                _ =>
                    profiles[0]
            };
        }

        if (
            kind is
                "motorway" or
                "trunk" or
                "motorway_link" or
                "trunk_link")
        {
            return profiles[6];
        }

        if (
            road.LaneCount is >= 4 ||
            kind is
                "primary" or
                "primary_link" or
                "avenue")
        {
            return profiles[5];
        }

        if (kind is "service")
        {
            return profiles[3];
        }

        return profiles[4];
    }

    private async void OnAnalyzeGoogleRoadReferenceWithAiClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .AiAssistance,
                "Análise de vias por IA") ||
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .ProceduralRoads,
                "Gerador procedural de vias"))
        {
            return;
        }

        if (
            _session.CurrentMap is not
                { } snapshot)
        {
            StatusText.Text =
                "IA de vias: abra um mapa primeiro.";

            return;
        }

        if (_proceduralRoadTraceMode)
        {
            StatusText.Text =
                "Finalize ou limpe a linha manual atual antes da análise por IA.";

            return;
        }

        var reference =
            _activeGoogleMapReference;

        if (
            reference is null ||
            !string.Equals(
                _referenceOverlayMapDirectory,
                snapshot.Map.DirectoryPath,
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(
                reference.ImagePath))
        {
            StatusText.Text =
                "IA de vias: carregue primeiro Mapa → Referência Google sobre o terreno.";

            return;
        }

        var activeProfile =
            _aiConnectionSettings
                .GetActiveProfile();

        if (activeProfile is null)
        {
            StatusText.Text =
                "IA de vias: configure e ative um perfil em IA → Configurar provedores.";

            return;
        }

        if (
            !NativeAiProviderFactory
                .IsImplemented(
                    activeProfile))
        {
            StatusText.Text =
                $"IA de vias: adapter {activeProfile.AdapterId} ainda não implementado nesta build.";

            return;
        }

        var mimeType =
            GetAiImageMimeType(
                reference.ImagePath);

        if (mimeType is null)
        {
            StatusText.Text =
                "IA de vias: a referência precisa estar em PNG, JPG/JPEG ou WEBP.";

            return;
        }

        var notesBox =
            new TextBox
            {
                Header =
                    "Orientações para a IA",
                PlaceholderText =
                    "Opcional · ex.: ignore estacionamentos, trace apenas ruas públicas, avenida principal tem 4 faixas...",
                AcceptsReturn =
                    true,
                TextWrapping =
                    TextWrapping.Wrap,
                MinHeight =
                    72
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    520
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"{reference.Width}×{reference.Height}px · {reference.WidthMeters:F1}×{reference.HeightMeters:F1} m · {reference.MetersPerPixel:F3} m/pixel",
                TextWrapping =
                    TextWrapping.Wrap
            });

        panel.Children.Add(
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    InfoBarSeverity
                        .Informational,
                Title =
                    "Apenas preview",
                Message =
                    "A IA detectará eixos de vias na imagem Google. O resultado será projetado pela mesma âncora georreferenciada e exibido no D3D11; nada será gravado até você usar Gerar vias."
            });

        panel.Children.Add(
            notesBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Detectar vias da referência com IA",
                Content =
                    panel,
                PrimaryButtonText =
                    "Analisar",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            StatusText.Text =
                $"IA de vias: analisando com {activeProfile.DisplayName}...";

            var bytes =
                await File
                    .ReadAllBytesAsync(
                        reference.ImagePath);

            var provider =
                NativeAiProviderFactory
                    .Create(
                        activeProfile);

            var analysis =
                await provider
                    .AnalyzeRoadReferenceAsync(
                        new MapStudioRoadReferenceRequest(
                            [
                                new MapStudioAiImageReference(
                                    bytes,
                                    mimeType,
                                    Path.GetFileName(
                                        reference.ImagePath))
                            ],
                            string.IsNullOrWhiteSpace(
                                notesBox.Text)
                                ? null
                                : notesBox.Text));

            if (analysis.Roads.Count == 0)
            {
                StatusText.Text =
                    "IA de vias: nenhuma via válida foi detectada.";

                return;
            }

            var projected =
                MapStudioRoadReferenceProjector
                    .Project(
                        analysis,
                        new MapStudioRoadReferenceImageProjection(
                            reference.Width,
                            reference.Height,
                            reference.MetersPerPixel,
                            reference.AnchorWorldX,
                            reference.AnchorWorldZ));

            var pointCount =
                projected.Sum(
                    road =>
                        road.Points.Count);

            if (
                projected.Count >
                    5_000 ||
                pointCount >
                    100_000)
            {
                StatusText.Text =
                    $"IA de vias recusada por segurança: {projected.Count} linha(s), {pointCount} ponto(s).";

                return;
            }

            var profiles =
                GetProceduralRoadProfiles();

            var added =
                0;

            foreach (
                var road in projected)
            {
                var points =
                    road.Points
                        .Where(
                            point =>
                                double.IsFinite(
                                    point.X) &&
                                double.IsFinite(
                                    point.Z))
                        .Aggregate(
                            new List<
                                MapStudioRoadPoint>(),
                            (list, point) =>
                            {
                                if (
                                    list.Count ==
                                        0 ||
                                    list[^1]
                                        .DistanceTo(
                                            point) >=
                                        0.20)
                                {
                                    list.Add(
                                        point);
                                }

                                return list;
                            });

                if (points.Count < 2)
                {
                    continue;
                }

                var roadProfile =
                    SelectProceduralRoadProfile(
                        road,
                        profiles);

                _proceduralRoadTraces.Add(
                    new MapStudioRoadTrace(
                        $"ai-{++_proceduralRoadTraceSequence}",
                        points,
                        roadProfile.ProfileId,
                        road.LaneCount ??
                            roadProfile.LaneCount,
                        road.OneWay ??
                            roadProfile.OneWay,
                        road.WidthMeters ??
                            roadProfile.WidthMeters));

                added++;
            }

            if (added == 0)
            {
                StatusText.Text =
                    "IA de vias: os eixos detectados não produziram traçados utilizáveis.";

                return;
            }

            AnalyzeProceduralRoadGraphMenuItem
                .IsEnabled =
                true;

            ClearProceduralRoadGraphMenuItem
                .IsEnabled =
                true;

            var graph =
                BuildProceduralRoadGraph();

            var preview =
                Viewport
                    .PreviewProceduralRoadGraph(
                        graph);

            StatusText.Text =
                $"IA de vias: {added} linha(s) adicionadas · {graph.Segments.Count} segmentos · {graph.Junctions.Count} cruzamentos · preview {preview.RenderedSegmentCount} segmentos.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"IA de vias falhou: {exception.Message}";
        }
    }

    private async void OnImportProceduralRoadGeoJsonClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .ProceduralRoads,
                "Importação GeoJSON"))
        {
            return;
        }

        if (_session.CurrentMap is null)
        {
            StatusText.Text =
                "Importação GeoJSON: abra um mapa primeiro.";

            return;
        }

        if (_proceduralRoadTraceMode)
        {
            StatusText.Text =
                "Finalize ou limpe a linha manual atual antes de importar GeoJSON.";

            return;
        }

        var georeference =
            await _session
                .LoadMapGeoreferenceAsync();

        if (georeference is null)
        {
            StatusText.Text =
                "Importação GeoJSON: o mapa precisa ter .mapstudio/georeference.json.";

            return;
        }

        var picker =
            new FileOpenPicker
            {
                SuggestedStartLocation =
                    PickerLocationId
                        .DocumentsLibrary
            };

        picker.FileTypeFilter.Add(
            ".geojson");

        picker.FileTypeFilter.Add(
            ".json");

        InitializeWithWindow.Initialize(
            picker,
            _windowHandle);

        var file =
            await picker
                .PickSingleFileAsync();

        if (file is null)
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Importando vias GeoJSON...";

            var json =
                await File
                    .ReadAllTextAsync(
                        file.Path);

            var imported =
                new MapStudioGeoJsonRoadImporter()
                    .Parse(
                        json);

            var pointCount =
                imported.Traces.Sum(
                    trace =>
                        trace.Points.Count);

            if (
                imported.Traces.Count >
                    5_000 ||
                pointCount >
                    100_000)
            {
                StatusText.Text =
                    $"GeoJSON recusado por segurança: {imported.Traces.Count} linha(s), {pointCount} ponto(s). Limite: 5.000 linhas / 100.000 pontos.";

                return;
            }

            var anchor =
                new MapStudioGeographicAnchor(
                    georeference.Latitude,
                    georeference.Longitude,
                    georeference.AnchorTileX *
                        300.0 +
                    georeference.AnchorX,
                    georeference.AnchorTileY *
                        300.0 +
                    georeference.AnchorY);

            var profiles =
                GetProceduralRoadProfiles();

            var added =
                0;

            var skipped =
                imported.IgnoredFeatureCount;

            foreach (
                var geoTrace in
                    imported.Traces)
            {
                var points =
                    new List<
                        MapStudioRoadPoint>(
                            geoTrace.Points.Count);

                foreach (
                    var geoPoint in
                        geoTrace.Points)
                {
                    var projected =
                        MapStudioGeographicProjection
                            .Project(
                                anchor,
                                geoPoint);

                    if (
                        points.Count ==
                            0 ||
                        points[^1]
                            .DistanceTo(
                                projected) >=
                            0.20)
                    {
                        points.Add(
                            projected);
                    }
                }

                if (points.Count < 2)
                {
                    skipped++;
                    continue;
                }

                var profile =
                    SelectProceduralRoadProfile(
                        geoTrace,
                        profiles);

                var traceId =
                    $"geo-{++_proceduralRoadTraceSequence}-{geoTrace.Id}";

                _proceduralRoadTraces.Add(
                    new MapStudioRoadTrace(
                        traceId,
                        points,
                        profile.ProfileId,
                        geoTrace.LaneCount ??
                            profile.LaneCount,
                        geoTrace.OneWay ??
                            profile.OneWay,
                        geoTrace.WidthMeters ??
                            profile.WidthMeters));

                added++;
            }

            if (added == 0)
            {
                StatusText.Text =
                    "GeoJSON não contém linhas de via válidas.";

                return;
            }

            AnalyzeProceduralRoadGraphMenuItem
                .IsEnabled =
                true;

            ClearProceduralRoadGraphMenuItem
                .IsEnabled =
                true;

            var graph =
                BuildProceduralRoadGraph();

            var preview =
                Viewport
                    .PreviewProceduralRoadGraph(
                        graph);

            StatusText.Text =
                $"GeoJSON importado: {added} linha(s), {graph.Segments.Count} segmento(s), {graph.Junctions.Count} cruzamento(s). " +
                $"Preview D3D11: {preview.RenderedSegmentCount} segmento(s). Ignorados: {skipped}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao importar GeoJSON: {exception.Message}";
        }
    }

    private async void OnImportProceduralRoadOsmClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .ProceduralRoads,
                "Importação OSM"))
        {
            return;
        }

        if (_session.CurrentMap is null)
        {
            StatusText.Text =
                "Importação OSM: abra um mapa primeiro.";

            return;
        }

        if (_proceduralRoadTraceMode)
        {
            StatusText.Text =
                "Finalize ou limpe a linha manual atual antes de importar OSM.";

            return;
        }

        var georeference =
            await _session
                .LoadMapGeoreferenceAsync();

        if (georeference is null)
        {
            StatusText.Text =
                "Importação OSM: o mapa precisa ter .mapstudio/georeference.json.";

            return;
        }

        var picker =
            new FileOpenPicker
            {
                SuggestedStartLocation =
                    PickerLocationId
                        .DocumentsLibrary
            };

        picker.FileTypeFilter.Add(
            ".osm");

        picker.FileTypeFilter.Add(
            ".xml");

        InitializeWithWindow.Initialize(
            picker,
            _windowHandle);

        var file =
            await picker
                .PickSingleFileAsync();

        if (file is null)
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Importando vias OSM...";

            var xml =
                await File
                    .ReadAllTextAsync(
                        file.Path);

            var imported =
                new MapStudioOsmRoadImporter()
                    .Parse(
                        xml);

            var pointCount =
                imported.Traces.Sum(
                    trace =>
                        trace.Points.Count);

            if (
                imported.Traces.Count >
                    5_000 ||
                pointCount >
                    100_000)
            {
                StatusText.Text =
                    $"OSM recusado por segurança: {imported.Traces.Count} linha(s), {pointCount} ponto(s). Limite: 5.000 linhas / 100.000 pontos.";

                return;
            }

            var anchorGeo =
                new MapStudioGeographicAnchor(
                    georeference.Latitude,
                    georeference.Longitude,
                    georeference.AnchorTileX *
                        300.0 +
                    georeference.AnchorX,
                    georeference.AnchorTileY *
                        300.0 +
                    georeference.AnchorY);

            var profiles =
                GetProceduralRoadProfiles();

            var added =
                0;

            var skipped =
                imported.IgnoredWayCount;

            foreach (
                var geoTrace in
                    imported.Traces)
            {
                var points =
                    new List<
                        MapStudioRoadPoint>(
                            geoTrace.Points.Count);

                foreach (
                    var geoPoint in
                        geoTrace.Points)
                {
                    var projected =
                        MapStudioGeographicProjection
                            .Project(
                                anchorGeo,
                                geoPoint);

                    if (
                        points.Count ==
                            0 ||
                        points[^1]
                            .DistanceTo(
                                projected) >=
                            0.20)
                    {
                        points.Add(
                            projected);
                    }
                }

                if (points.Count < 2)
                {
                    skipped++;
                    continue;
                }

                var profile =
                    SelectProceduralRoadProfile(
                        geoTrace,
                        profiles);

                _proceduralRoadTraces.Add(
                    new MapStudioRoadTrace(
                        $"osm-{++_proceduralRoadTraceSequence}-{geoTrace.Id}",
                        points,
                        profile.ProfileId,
                        geoTrace.LaneCount ??
                            profile.LaneCount,
                        geoTrace.OneWay ??
                            profile.OneWay,
                        geoTrace.WidthMeters ??
                            profile.WidthMeters));

                added++;
            }

            if (added == 0)
            {
                StatusText.Text =
                    "OSM não contém ways de via utilizáveis.";

                return;
            }

            AnalyzeProceduralRoadGraphMenuItem
                .IsEnabled =
                true;

            ClearProceduralRoadGraphMenuItem
                .IsEnabled =
                true;

            var graph =
                BuildProceduralRoadGraph();

            var preview =
                Viewport
                    .PreviewProceduralRoadGraph(
                        graph);

            StatusText.Text =
                $"OSM importado: {added} via(s), {graph.Segments.Count} segmento(s), {graph.Junctions.Count} cruzamento(s). " +
                $"Preview D3D11: {preview.RenderedSegmentCount} segmento(s). Ways ignorados: {skipped}. Referências de node ausentes: {imported.MissingNodeReferenceCount}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao importar OSM: {exception.Message}";
        }
    }

    private async void OnStartProceduralRoadTraceClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .ProceduralRoads,
                "Gerador procedural de vias"))
        {
            return;
        }

        if (_session.CurrentMap is null)
        {
            StatusText.Text =
                "Gerador de vias: abra um mapa primeiro.";

            return;
        }

        if (_proceduralRoadTraceMode)
        {
            StatusText.Text =
                "Finalize ou limpe a linha atual antes de iniciar outra.";

            return;
        }

        var profiles =
            GetProceduralRoadProfiles();

        var combo =
            new ComboBox
            {
                Header =
                    "Perfil da via",
                ItemsSource =
                    profiles,
                DisplayMemberPath =
                    nameof(
                        RoadProfileOption
                            .Label),
                SelectedIndex =
                    3,
                HorizontalAlignment =
                    HorizontalAlignment
                        .Stretch,
                MinWidth =
                    420
            };

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Nova linha de via",
                Content =
                    combo,
                PrimaryButtonText =
                    "Começar traçado",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary ||
            combo.SelectedItem is not
                RoadProfileOption profile)
        {
            return;
        }

        _activeRoadProfile =
            profile;

        _activeRoadTracePoints
            .Clear();

        _proceduralRoadTraceMode =
            true;

        FinishProceduralRoadTraceMenuItem
            .IsEnabled =
            false;

        ClearProceduralRoadGraphMenuItem
            .IsEnabled =
            true;

        Viewport
            .BeginTerrainSelectionMode();

        StatusText.Text =
            $"Traçado procedural ativo · {profile.Label}. Clique pontos sucessivos sobre o terreno; use Ferramentas → Gerador procedural de vias → Finalizar linha atual.";
    }

    private MapStudioRoadGraph BuildProceduralRoadGraph()
    {
        var smoothedTraces =
            new MapStudioRoadTraceSmoother()
                .Smooth(
                    _proceduralRoadTraces);

        return new MapStudioRoadGraphBuilder()
            .Build(
                smoothedTraces);
    }

    private void OnFinishProceduralRoadTraceClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !_proceduralRoadTraceMode ||
            _activeRoadProfile is null ||
            _activeRoadTracePoints.Count <
                2)
        {
            StatusText.Text =
                "Traçado procedural: defina pelo menos dois pontos.";

            return;
        }

        var trace =
            new MapStudioRoadTrace(
                $"trace-{++_proceduralRoadTraceSequence}",
                _activeRoadTracePoints
                    .ToArray(),
                _activeRoadProfile
                    .ProfileId,
                _activeRoadProfile
                    .LaneCount,
                _activeRoadProfile
                    .OneWay,
                _activeRoadProfile
                    .WidthMeters);

        _proceduralRoadTraces.Add(
            trace);

        _activeRoadTracePoints
            .Clear();

        _proceduralRoadTraceMode =
            false;

        _activeRoadProfile =
            null;

        FinishProceduralRoadTraceMenuItem
            .IsEnabled =
            false;

        AnalyzeProceduralRoadGraphMenuItem
            .IsEnabled =
            _proceduralRoadTraces.Count >
            0;

        ClearProceduralRoadGraphMenuItem
            .IsEnabled =
            true;

        Viewport
            .CancelTerrainPointPick();

        var graph =
            BuildProceduralRoadGraph();

        StatusText.Text =
            $"Linha registrada. Grafo: {_proceduralRoadTraces.Count} linha(s), {graph.Segments.Count} segmento(s), {graph.Junctions.Count} cruzamento(s).";
    }

    private async void OnAnalyzeProceduralRoadGraphClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            _proceduralRoadTraces.Count ==
            0)
        {
            StatusText.Text =
                "Gerador de vias: nenhum traçado registrado.";

            return;
        }

        MapStudioRoadGraph graph;

        try
        {
            graph =
                BuildProceduralRoadGraph();
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao analisar traçado: {exception.Message}";

            return;
        }

        var preview =
            Viewport
                .PreviewProceduralRoadGraph(
                    graph);

        var details =
            graph.Junctions.Count ==
                0
                ? "Nenhum cruzamento foi detectado."
                : string.Join(
                    Environment.NewLine,
                    graph.Junctions
                        .Take(20)
                        .Select(
                            junction =>
                                $"Nó {junction.NodeId} · grau {junction.Degree} · " +
                                $"X {junction.Position.X:F2} / Z {junction.Position.Z:F2} · " +
                                string.Join(
                                    ", ",
                                    junction.TraceIds)));

        var content =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    540
            };

        content.Children.Add(
            new TextBlock
            {
                Text =
                    $"{_proceduralRoadTraces.Count} linha(s) · {graph.Nodes.Count} nós · " +
                    $"{graph.Segments.Count} segmentos · {graph.Junctions.Count} cruzamento(s)\n" +
                    $"Preview D3D11: {preview.RenderedSegmentCount} segmento(s) · {preview.RenderedJunctionCount} marcador(es).",
                FontSize =
                    16,
                FontWeight =
                    Microsoft.UI.Text
                        .FontWeights
                        .SemiBold
            });

        content.Children.Add(
            new TextBlock
            {
                Text =
                    details,
                TextWrapping =
                    TextWrapping
                        .Wrap
            });

        content.Children.Add(
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    InfoBarSeverity
                        .Informational,
                Title =
                    "Preview antes de gravar",
                Message =
                    "O preview já usa o traçado suavizado que será persistido. Segmentos contínuos da mesma linha recebem auto-link somente em nós lineares de grau 2; nós de grau 3/4 não são ligados através do cruzamento e recebem junctions próprios gerados antes da gravação."
            });

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Grafo procedural de vias",
                Content =
                    content,
                PrimaryButtonText =
                    "Gerar vias",
                CloseButtonText =
                    "Fechar",
                DefaultButton =
                    ContentDialogButton
                        .Close
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        var root =
            _session.OmsiRootPath;

        if (
            root is null ||
            _session.CurrentMap is null)
        {
            StatusText.Text =
                "Gerador de vias: OMSI/mapa não está disponível.";

            return;
        }

        var placement =
            Viewport
                .BuildProceduralRoadPlacementRequests(
                    graph);

        var junctionPlan =
            Viewport
                .BuildProceduralJunctionPlan(
                    graph);

        if (
            junctionPlan.SkippedJunctions >
                0)
        {
            StatusText.Text =
                $"Geração cancelada: {junctionPlan.SkippedJunctions} cruzamento(s) ficaram fora do terreno carregado.";

            return;
        }

        if (
            placement.SkippedSegments >
                0 ||
            placement.Requests.Count !=
                graph.Segments.Count)
        {
            StatusText.Text =
                $"Geração cancelada: {placement.SkippedSegments} segmento(s) ficaram fora do terreno carregado. Use Mapa completo e revise o traçado.";

            return;
        }

        try
        {
            if (
                _session.PendingTransformCount >
                0)
            {
                StatusText.Text =
                    "Salvando transformações antes de gerar as vias...";

                await _session
                    .SavePendingTransformsAsync();

                SaveChangesButton.IsEnabled =
                    false;
            }

            StatusText.Text =
                "Preparando Road Kit original...";

            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root);

            var junctionGroups =
                new List<
                    NativeSceneryPlacementBatchGroup>();

            if (
                junctionPlan.Items.Count >
                    0)
            {
                StatusText.Text =
                    $"Gerando {junctionPlan.UniqueAssetCount} tipo(s) de junction próprio(s)...";

                var generatedAssets =
                    new Dictionary<
                        string,
                        string>(
                            StringComparer
                                .OrdinalIgnoreCase);

                foreach (
                    var topology in
                        junctionPlan.Items
                            .GroupBy(
                                item =>
                                    item.AssetName,
                                StringComparer
                                    .OrdinalIgnoreCase))
                {
                    var sample =
                        topology.First();

                    var asset =
                        await new MapStudioJunctionAssetGenerator()
                            .GenerateAsync(
                                root,
                                sample.Spec);

                    var sceneryRoot =
                        Path.Combine(
                            root,
                            "Sceneryobjects");

                    var relativePath =
                        Path.GetRelativePath(
                            sceneryRoot,
                            asset.SceneryObjectPath)
                            .Replace(
                                Path.DirectorySeparatorChar,
                                '\\');

                    generatedAssets[
                        sample.AssetName] =
                        relativePath;
                }

                foreach (
                    var topology in
                        junctionPlan.Items
                            .GroupBy(
                                item =>
                                    item.AssetName,
                                StringComparer
                                    .OrdinalIgnoreCase))
                {
                    var sceneryPath =
                        generatedAssets[
                            topology.Key];

                    var requests =
                        topology
                            .Select(
                                item =>
                                    new NativeSceneryPlacementRequest(
                                        item.Tile,
                                        sceneryPath,
                                        item.X,
                                        item.Y,
                                        0,
                                        item.Rotation,
                                        0,
                                        0,
                                        item.WorldPoint,
                                        false))
                            .ToArray();

                    foreach (
                        var chunk in
                            requests.Chunk(
                                256))
                    {
                        junctionGroups.Add(
                            new NativeSceneryPlacementBatchGroup(
                                sceneryPath,
                                chunk));
                    }
                }
            }

            StatusText.Text =
                $"Gerando {placement.Requests.Count} spline(s) em uma única transação...";

            var insertion =
                await _session
                    .InsertSplineBatchAsync(
                        placement.Requests,
                        placement.Links);

            var finalSnapshot =
                insertion.Snapshot;

            var junctionBackupDirectories =
                new List<string>();

            try
            {
                foreach (
                    var batch in
                        junctionGroups
                            .Chunk(
                                16))
                {
                    StatusText.Text =
                        $"Inserindo junctions automáticos · {batch.Sum(group => group.Placements.Count)} objeto(s)...";

                    finalSnapshot =
                        await _session
                            .InsertSceneryObjectMultiBatchAsync(
                                batch);

                    if (
                        !string.IsNullOrWhiteSpace(
                            _session.LastBackupDirectory))
                    {
                        junctionBackupDirectories.Add(
                            _session.LastBackupDirectory!);
                    }
                }
            }
            catch
            {
                try
                {
                    await _session
                        .RestoreMapStudioBackupAsync(
                            insertion.BackupDirectory);
                }
                catch
                {
                    // Keep the original junction exception as the primary failure.
                }

                throw;
            }

            RegisterConstructionHistory(
                "Gerar vias procedurais");

            await ApplyMapSnapshotAsync(
                finalSnapshot,
                focusActiveTile:
                    false);

            Viewport
                .ClearProceduralRoadPreview();

            _proceduralRoadTraceMode =
                false;

            _activeRoadProfile =
                null;

            _activeRoadTracePoints
                .Clear();

            _proceduralRoadTraces
                .Clear();

            FinishProceduralRoadTraceMenuItem
                .IsEnabled =
                false;

            AnalyzeProceduralRoadGraphMenuItem
                .IsEnabled =
                false;

            ClearProceduralRoadGraphMenuItem
                .IsEnabled =
                false;

            StatusText.Text =
                $"{insertion.SplineIds.Count} spline(s) procedurais + {junctionPlan.Items.Count} junction(s) próprios gravados. " +
                $"Backup das vias: {insertion.BackupDirectory}" +
                (
                    junctionBackupDirectories.Count >
                        0
                        ? $" · backup(s) de junction: {junctionBackupDirectories.Count}"
                        : string.Empty
                );
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao gerar vias procedurais: {exception.Message}";
        }
    }

    private void OnClearProceduralRoadGraphClick(
        object sender,
        RoutedEventArgs e)
    {
        _proceduralRoadTraceMode =
            false;

        _activeRoadProfile =
            null;

        _activeRoadTracePoints
            .Clear();

        _proceduralRoadTraces
            .Clear();

        Viewport
            .CancelTerrainPointPick();

        Viewport
            .ClearProceduralRoadPreview();

        FinishProceduralRoadTraceMenuItem
            .IsEnabled =
            false;

        AnalyzeProceduralRoadGraphMenuItem
            .IsEnabled =
            false;

        ClearProceduralRoadGraphMenuItem
            .IsEnabled =
            false;

        StatusText.Text =
            "Traçado procedural limpo.";
    }

    private async void OnCreateBridgeClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolBridgesButton);

        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .ProceduralRoads,
                "Criador de pontes"))
        {
            return;
        }

        var root =
            _session.OmsiRootPath;

        if (root is null)
        {
            StatusText.Text =
                "Criador de pontes: ative o Workspace Map Studio ou selecione uma instalação do OMSI.";
            return;
        }

        var nameBox =
            new TextBox
            {
                Header = "Nome",
                Text = "Ponte 2 faixas",
                PlaceholderText = "Ex.: Ponte central"
            };

        var lanesBox =
            new NumberBox
            {
                Header = "Número de faixas",
                Minimum = 1,
                Maximum = 8,
                Value = 2,
                SmallChange = 1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Inline
            };

        var laneWidthBox =
            new NumberBox
            {
                Header = "Largura de cada faixa (m)",
                Minimum = 2.5,
                Maximum = 6,
                Value = 3.5,
                SmallChange = 0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Inline
            };

        var sidewalkBox =
            new NumberBox
            {
                Header = "Calçada lateral (m)",
                Minimum = 0,
                Maximum = 5,
                Value = 1.5,
                SmallChange = 0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Inline
            };

        var deckBox =
            new NumberBox
            {
                Header = "Espessura do tabuleiro (m)",
                Minimum = 0.2,
                Maximum = 2,
                Value = 0.55,
                SmallChange = 0.05,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Inline
            };

        var oneWayCheckBox =
            new CheckBox
            {
                Content = "Mão única"
            };

        var note =
            new TextBlock
            {
                Text =
                    "A ponte é gerada como uma SLI própria do Map Studio, com tabuleiro, pista, calçadas, marcações e paths de tráfego. Depois ela pode ser desenhada reta ou curva pela Estrada fácil e elevada pelo controle de elevação.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity = 0.78
            };

        var panel =
            new StackPanel
            {
                Spacing = 10
            };

        panel.Children.Add(nameBox);
        panel.Children.Add(lanesBox);
        panel.Children.Add(laneWidthBox);
        panel.Children.Add(sidewalkBox);
        panel.Children.Add(deckBox);
        panel.Children.Add(oneWayCheckBox);
        panel.Children.Add(note);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Criador de pontes",
                Content =
                    panel,
                PrimaryButtonText =
                    "Gerar ponte",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Criador de pontes: gerando spline e texturas...";

            var result =
                await new MapStudioBridgeSplineGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioBridgeSpec(
                            nameBox.Text,
                            (int)Math.Round(
                                lanesBox.Value),
                            laneWidthBox.Value,
                            sidewalkBox.Value,
                            deckBox.Value,
                            oneWayCheckBox
                                .IsChecked ==
                            true));

            StatusText.Text =
                "Ponte gerada; atualizando biblioteca...";

            await _session
                .RefreshAssetLibraryAsync();

            await LoadAssetLibraryAsync();

            SplineElevationOffsetBox.Value = 5;
            SplineHeightCheckBox.IsChecked = false;
            SplineEasyRoadCheckBox.IsEnabled = true;
            SplineContinuousCheckBox.IsEnabled = true;
            SplineEndpointSnapCheckBox.IsEnabled = true;
            SplineEndpointSnapDistanceBox.IsEnabled = true;
            SplineAutoConnectCheckBox.IsEnabled = true;
            SplineEasyRoadCheckBox.IsChecked = true;
            SplineCurveOffsetBox.Value = 0;
            SplineCurveCheckBox.IsChecked = false;

            SetSelectionModeFromShortcut(
                2);

            await ActivateLibraryGroupToolAsync(
                2,
                OmsiAssetLibraryGroup.Bridges,
                $"Ponte criada: {result.RelativeSplinePath}. Estrada fácil ativa com +5 m; ajuste elevação e curva conforme necessário." +
                (
                    result.BackupDirectory is null
                        ? string.Empty
                        : $" Backup da versão anterior: {result.BackupDirectory}"
                ));
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Criador de pontes falhou: {exception.Message}";
        }
    }

    private async void OnCreateTunnelClick(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveMapTool(
            ToolTunnelsButton);

        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .ProceduralRoads,
                "Criador de túneis"))
        {
            return;
        }

        var root =
            _session.OmsiRootPath;

        if (root is null)
        {
            StatusText.Text =
                "Criador de túneis: ative o Workspace Map Studio ou selecione uma instalação do OMSI.";

            return;
        }

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome",
                Text =
                    "Túnel 2 faixas",
                PlaceholderText =
                    "Ex.: Túnel central"
            };

        var lanesBox =
            new NumberBox
            {
                Header =
                    "Número de faixas",
                Minimum =
                    1,
                Maximum =
                    8,
                Value =
                    2,
                SmallChange =
                    1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Inline
            };

        var laneWidthBox =
            new NumberBox
            {
                Header =
                    "Largura de cada faixa (m)",
                Minimum =
                    2.5,
                Maximum =
                    6,
                Value =
                    3.5,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Inline
            };

        var heightBox =
            new NumberBox
            {
                Header =
                    "Altura interna (m)",
                Minimum =
                    3.5,
                Maximum =
                    15,
                Value =
                    5.2,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Inline
            };

        var shoulderBox =
            new NumberBox
            {
                Header =
                    "Folga lateral / acostamento (m)",
                Minimum =
                    0,
                Maximum =
                    5,
                Value =
                    0.75,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Inline
            };

        var archSegmentsBox =
            new NumberBox
            {
                Header =
                    "Suavidade do arco",
                Minimum =
                    4,
                Maximum =
                    24,
                Value =
                    10,
                SmallChange =
                    1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Inline
            };

        var oneWayCheckBox =
            new CheckBox
            {
                Content =
                    "Mão única"
            };

        var note =
            new TextBlock
            {
                Text =
                    "O túnel é gerado como uma SLI OMSI real: pista, marcações, paredes, teto em arco e paths de tráfego. Depois da geração ele aparece em Pontes / túneis e pode ser desenhado com a mesma ferramenta Estrada fácil, inclusive em curvas e gradientes.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    10
            };

        panel.Children.Add(
            nameBox);
        panel.Children.Add(
            lanesBox);
        panel.Children.Add(
            laneWidthBox);
        panel.Children.Add(
            heightBox);
        panel.Children.Add(
            shoulderBox);
        panel.Children.Add(
            archSegmentsBox);
        panel.Children.Add(
            oneWayCheckBox);
        panel.Children.Add(
            note);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Criador de túneis",
                Content =
                    panel,
                PrimaryButtonText =
                    "Gerar túnel",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Criador de túneis: gerando spline e texturas OMSI...";

            var spec =
                new MapStudioTunnelSpec(
                    nameBox.Text,
                    (int)Math.Round(
                        lanesBox.Value),
                    laneWidthBox.Value,
                    heightBox.Value,
                    shoulderBox.Value,
                    (int)Math.Round(
                        archSegmentsBox.Value),
                    oneWayCheckBox
                        .IsChecked ==
                    true);

            var result =
                await new MapStudioTunnelSplineGenerator()
                    .GenerateAsync(
                        root,
                        spec);

            StatusText.Text =
                "Túnel gerado; atualizando biblioteca...";

            await _session
                .RefreshAssetLibraryAsync();

            await LoadAssetLibraryAsync();

            SplineElevationOffsetBox.Value =
                0;

            SplineHeightCheckBox.IsChecked =
                false;

            SplineEasyRoadCheckBox.IsEnabled =
                true;

            SplineContinuousCheckBox.IsEnabled =
                true;

            SplineEndpointSnapCheckBox.IsEnabled =
                true;

            SplineEndpointSnapDistanceBox.IsEnabled =
                true;

            SplineAutoConnectCheckBox.IsEnabled =
                true;

            SplineEasyRoadCheckBox.IsChecked =
                true;

            SplineCurveOffsetBox.Value =
                0;

            SplineCurveCheckBox.IsChecked =
                false;

            SetSelectionModeFromShortcut(
                2);

            await ActivateLibraryGroupToolAsync(
                2,
                OmsiAssetLibraryGroup.Bridges,
                $"Túnel criado: {result.RelativeSplinePath}. Selecione-o na biblioteca e desenhe entrada, curva e saída com Estrada fácil." +
                (
                    result.BackupDirectory is null
                        ? string.Empty
                        : $" Backup da versão anterior: {result.BackupDirectory}"
                ));
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Criador de túneis falhou: {exception.Message}";
        }
    }

    private async void OnInstallRoadKitClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .ProceduralRoads,
                "Road Kit"))
        {
            return;
        }

        var root =
            _session.OmsiRootPath;

        if (root is null)
        {
            StatusText.Text =
                "Road Kit: ative o Workspace Map Studio ou selecione uma instalação do OMSI.";

            return;
        }

        try
        {
            StatusText.Text =
                "Road Kit: gerando splines e texturas originais...";

            var result =
                await new MapStudioRoadKitGenerator()
                    .InstallOrUpdateAsync(
                        root);

            StatusText.Text =
                "Road Kit gerado; atualizando biblioteca...";

            await _session
                .RefreshAssetLibraryAsync();

            await LoadAssetLibraryAsync();

            StatusText.Text =
                result.BackupDirectory is null
                    ? $"Road Kit instalado: {result.SplineRelativePaths.Count} spline(s) próprias."
                    : $"Road Kit atualizado: {result.SplineRelativePaths.Count} spline(s). Backup: {result.BackupDirectory}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Road Kit falhou: {exception.Message}";
        }
    }

    private void RefreshAiConnectionUi()
    {
        var profiles =
            _aiConnectionSettings
                .Profiles
                .Select(
                    profile =>
                        new AiProfileOption(
                            profile.DisplayName +
                            " · " +
                            profile.AdapterId,
                            profile))
                .ToArray();

        _refreshingAiProfileSelector =
            true;

        try
        {
            AiProfileSelectorBox.ItemsSource =
                profiles;

            AiProfileSelectorBox.DisplayMemberPath =
                nameof(
                    AiProfileOption.Label);

            AiProfileSelectorBox.SelectedItem =
                profiles.FirstOrDefault(
                    option =>
                        string.Equals(
                            option.Profile?.Id,
                            _aiConnectionSettings
                                .ActiveProfileId,
                            StringComparison
                                .OrdinalIgnoreCase));
        }
        finally
        {
            _refreshingAiProfileSelector =
                false;
        }

        var activeProfile =
            _aiConnectionSettings
                .GetActiveProfile();

        if (activeProfile is null)
        {
            AiStatusButton.Content =
                "IA: conectar";

            ToolTipService.SetToolTip(
                AiStatusButton,
                "Nenhum perfil ativo. Clique para configurar a IA usada pelas ferramentas do mapa.");

            AiConnectMenuItem.Text =
                "Conectar / testar IA no mapa...";

            AiActiveProfileMenuItem.Text =
                "Nenhum perfil ativo";

            return;
        }

        var hasCredential =
            NativeAiCredentialStore
                .HasSecret(
                    activeProfile.Id);

        AiStatusButton.Content =
            $"IA: {activeProfile.DisplayName}";

        ToolTipService.SetToolTip(
            AiStatusButton,
            hasCredential
                ? $"Perfil ativo: {activeProfile.DisplayName} · {activeProfile.AdapterId} · credencial protegida no Windows."
                : $"Perfil ativo: {activeProfile.DisplayName} · {activeProfile.AdapterId} · sem credencial salva.");

        AiConnectMenuItem.Text =
            "Testar IA ativa no mapa...";

        AiActiveProfileMenuItem.Text =
            $"Ativa: {activeProfile.DisplayName} · {activeProfile.AdapterId}";
    }

    private void OnAiProfileSelectorChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (
            _refreshingAiProfileSelector)
        {
            return;
        }

        var selected =
            AiProfileSelectorBox
                .SelectedItem as
                AiProfileOption;

        if (
            selected?.Profile is not
                { } profile)
        {
            return;
        }

        _aiConnectionSettings =
            new MapStudioAiConnectionSettings(
                profile.Id,
                _aiConnectionSettings
                    .Profiles)
            .Normalize();

        NativeAiConnectionSettingsStore
            .Save(
                _aiConnectionSettings);

        RefreshAiConnectionUi();

        StatusText.Text =
            $"IA: perfil ativo alterado para '{profile.DisplayName}'. Clique IA para testar a conexão.";
    }

    private async void OnConnectAiToMapClick(
        object sender,
        RoutedEventArgs e)
    {
        var activeProfile =
            _aiConnectionSettings
                .GetActiveProfile();

        if (activeProfile is null)
        {
            StatusText.Text =
                "IA: escolha um provedor, salve a credencial e marque o perfil como ativo.";

            OnAiProvidersClick(
                sender,
                e);

            return;
        }

        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .AiAssistance,
                "Conexão de IA"))
        {
            return;
        }

        try
        {
            AiStatusButton.IsEnabled =
                false;

            AiStatusButton.Content =
                "IA: testando...";

            StatusText.Text =
                $"IA: testando {activeProfile.DisplayName} para as ferramentas do mapa...";

            await NativeAiProviderFactory
                .TestConnectionAsync(
                    activeProfile,
                    NativeAiCredentialStore
                        .TryGetSecret(
                            activeProfile.Id));

            AiStatusButton.Content =
                $"IA: {activeProfile.DisplayName} · conectada";

            StatusText.Text =
                $"IA conectada: {activeProfile.DisplayName} · {activeProfile.AdapterId} · {activeProfile.Model}. Esse perfil será usado pelas ferramentas de IA do mapa.";
        }
        catch (Exception exception)
        {
            AiStatusButton.Content =
                $"IA: {activeProfile.DisplayName} · erro";

            StatusText.Text =
                $"IA: falha ao conectar {activeProfile.DisplayName}: {exception.Message}";
        }
        finally
        {
            AiStatusButton.IsEnabled =
                true;
        }
    }

    private async void OnAiProvidersClick(
        object sender,
        RoutedEventArgs e)
    {
        var profiles =
            _aiConnectionSettings
                .Profiles
                .ToList();

        var options =
            new List<AiProfileOption>
            {
                new(
                    "Novo perfil...",
                    null)
            };

        options.AddRange(
            profiles.Select(
                profile =>
                    new AiProfileOption(
                        profile.DisplayName +
                        " · " +
                        profile.AdapterId,
                        profile)));

        var profileCombo =
            new ComboBox
            {
                Header =
                    "Perfil",
                ItemsSource =
                    options,
                DisplayMemberPath =
                    nameof(
                        AiProfileOption
                            .Label),
                HorizontalAlignment =
                    HorizontalAlignment
                        .Stretch
            };

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome",
                PlaceholderText =
                    "Ex.: Meu modelo local"
            };

        var quickProviderBox =
            new ComboBox
            {
                Header =
                    "Configuração rápida",
                PlaceholderText =
                    "Escolha um provedor",
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                ItemsSource =
                    new[]
                    {
                        "OpenAI · Responses API",
                        "OpenAI-compatible",
                        "Ollama · local",
                        "LM Studio · local",
                        "Anthropic",
                        "Google Gemini"
                    }
            };

        var adapterBox =
            new TextBox
            {
                Header =
                    "Adapter ID",
                PlaceholderText =
                    "openai, openai-compatible, ollama, lmstudio, anthropic ou gemini"
            };

        var endpointBox =
            new TextBox
            {
                Header =
                    "Endpoint",
                PlaceholderText =
                    "Opcional · http(s)://..."
            };

        var modelBox =
            new TextBox
            {
                Header =
                    "Modelo",
                PlaceholderText =
                    "Ex.: gpt-5.6-terra, gpt-5.6, modelo local..."
            };

        var tokenBox =
            new PasswordBox
            {
                Header =
                    "Token / API key",
                PlaceholderText =
                    "OpenAI/Anthropic/Gemini exigem API key; local pode dispensar",
                PasswordRevealMode =
                    PasswordRevealMode
                        .Peek
            };

        var clearCredentialCheckBox =
            new CheckBox
            {
                Content =
                    "Remover credencial salva deste perfil"
            };

        var credentialStatusText =
            new TextBlock
            {
                FontSize =
                    11,
                Foreground =
                    new Microsoft.UI.Xaml.Media
                        .SolidColorBrush(
                            Windows.UI.Color
                                .FromArgb(
                                    255,
                                    120,
                                    149,
                                    173)),
                TextWrapping =
                    TextWrapping
                        .Wrap
            };

        var localCheckBox =
            new CheckBox
            {
                Content =
                    "Executa localmente/offline"
            };

        var activeCheckBox =
            new CheckBox
            {
                Content =
                    "Usar como perfil ativo"
            };

        var adapterSupportText =
            new TextBlock
            {
                FontSize =
                    11,
                TextWrapping =
                    TextWrapping.Wrap,
                Foreground =
                    new Microsoft.UI.Xaml.Media
                        .SolidColorBrush(
                            Windows.UI.Color
                                .FromArgb(
                                    255,
                                    132,
                                    181,
                                    205))
            };

        void UpdateAdapterHelp(
            bool applyDefaults)
        {
            var adapter =
                adapterBox.Text
                    .Trim()
                    .ToLowerInvariant();

            string? defaultEndpoint =
                null;

            var forceLocal =
                false;

            adapterSupportText.Text =
                adapter switch
                {
                    "openai" =>
                        "OpenAI oficial · Responses API com visão. Padrão: gpt-5.6-terra. API key obrigatória e protegida no Windows Credential Manager.",
                    "openai-compatible" =>
                        "Operacional · API de chat/completions compatível com OpenAI. Endpoint e modelo são obrigatórios; token depende do servidor.",
                    "ollama" =>
                        "Operacional · servidor local via API compatível com OpenAI. Endpoint padrão: http://localhost:11434/v1.",
                    "lmstudio" =>
                        "Operacional · servidor local via API compatível com OpenAI. Endpoint padrão: http://localhost:1234/v1.",
                    "anthropic" =>
                        "Operacional · Anthropic Messages API com imagem/base64 e resposta estruturada. Modelo e API key são obrigatórios.",
                    "gemini" =>
                        "Operacional · Google Gemini generateContent com inline_data. Modelo e API key são obrigatórios.",
                    "" =>
                        "Informe um adapter. Adapters operacionais: openai, openai-compatible, ollama, lmstudio, anthropic e gemini.",
                    _ =>
                        "Adapter ainda não implementado diretamente. Para serviços com API compatível, use openai-compatible."
                };

            switch (adapter)
            {
                case "openai":
                    defaultEndpoint =
                        "https://api.openai.com/v1/responses";
                    if (
                        applyDefaults &&
                        string.IsNullOrWhiteSpace(
                            modelBox.Text))
                    {
                        modelBox.Text =
                            "gpt-5.6-terra";
                    }
                    break;

                case "ollama":
                    defaultEndpoint =
                        "http://localhost:11434/v1";
                    forceLocal =
                        true;
                    break;

                case "lmstudio":
                    defaultEndpoint =
                        "http://localhost:1234/v1";
                    forceLocal =
                        true;
                    break;

                case "anthropic":
                    defaultEndpoint =
                        "https://api.anthropic.com/v1/messages";
                    break;

                case "gemini":
                    defaultEndpoint =
                        "https://generativelanguage.googleapis.com/v1beta";
                    break;
            }

            if (
                applyDefaults &&
                string.IsNullOrWhiteSpace(
                    endpointBox.Text) &&
                defaultEndpoint is not
                    null)
            {
                endpointBox.Text =
                    defaultEndpoint;
            }

            if (
                applyDefaults &&
                forceLocal)
            {
                localCheckBox.IsChecked =
                    true;
            }
        }

        quickProviderBox.SelectionChanged +=
            (_, _) =>
            {
                switch (
                    quickProviderBox
                        .SelectedIndex)
                {
                    case 0:
                        adapterBox.Text =
                            "openai";
                        endpointBox.Text =
                            "https://api.openai.com/v1/responses";
                        modelBox.Text =
                            "gpt-5.6-terra";
                        localCheckBox.IsChecked =
                            false;

                        if (
                            string.IsNullOrWhiteSpace(
                                nameBox.Text))
                        {
                            nameBox.Text =
                                "OpenAI";
                        }

                        break;

                    case 1:
                        adapterBox.Text =
                            "openai-compatible";
                        localCheckBox.IsChecked =
                            false;
                        break;

                    case 2:
                        adapterBox.Text =
                            "ollama";
                        endpointBox.Text =
                            "http://localhost:11434/v1";
                        localCheckBox.IsChecked =
                            true;

                        if (
                            string.IsNullOrWhiteSpace(
                                nameBox.Text))
                        {
                            nameBox.Text =
                                "Ollama";
                        }

                        break;

                    case 3:
                        adapterBox.Text =
                            "lmstudio";
                        endpointBox.Text =
                            "http://localhost:1234/v1";
                        localCheckBox.IsChecked =
                            true;

                        if (
                            string.IsNullOrWhiteSpace(
                                nameBox.Text))
                        {
                            nameBox.Text =
                                "LM Studio";
                        }

                        break;

                    case 4:
                        adapterBox.Text =
                            "anthropic";
                        endpointBox.Text =
                            "https://api.anthropic.com/v1/messages";
                        localCheckBox.IsChecked =
                            false;
                        break;

                    case 5:
                        adapterBox.Text =
                            "gemini";
                        endpointBox.Text =
                            "https://generativelanguage.googleapis.com/v1beta";
                        localCheckBox.IsChecked =
                            false;
                        break;
                }

                UpdateAdapterHelp(
                    applyDefaults:
                        false);
            };

        void LoadOption(
            AiProfileOption? option)
        {
            var profile =
                option?.Profile;

            nameBox.Text =
                profile
                    ?.DisplayName ??
                string.Empty;

            adapterBox.Text =
                profile
                    ?.AdapterId ??
                "openai";

            endpointBox.Text =
                profile
                    ?.Endpoint ??
                string.Empty;

            modelBox.Text =
                profile
                    ?.Model ??
                string.Empty;

            localCheckBox.IsChecked =
                profile
                    ?.IsLocal ??
                false;

            tokenBox.Password =
                string.Empty;

            clearCredentialCheckBox.IsChecked =
                false;

            var hasCredential =
                profile is not null &&
                NativeAiCredentialStore
                    .HasSecret(
                        profile.Id);

            tokenBox.PlaceholderText =
                hasCredential
                    ? "Credencial já salva · deixe vazio para manter"
                    : "Opcional";

            credentialStatusText.Text =
                profile is null
                    ? "Novo perfil: nenhuma credencial salva."
                    : hasCredential
                        ? "Credencial protegida no Windows Credential Manager."
                        : "Nenhuma credencial salva para este perfil.";

            activeCheckBox.IsChecked =
                profile is not null &&
                string.Equals(
                    _aiConnectionSettings
                        .ActiveProfileId,
                    profile.Id,
                    StringComparison
                        .OrdinalIgnoreCase);

            UpdateAdapterHelp(
                applyDefaults:
                    profile is null);
        }

        adapterBox.TextChanged +=
            (_, _) =>
            {
                var selectedProfile =
                    (profileCombo
                        .SelectedItem as
                        AiProfileOption)
                    ?.Profile;

                UpdateAdapterHelp(
                    applyDefaults:
                        selectedProfile is
                            null);
            };

        profileCombo.SelectionChanged +=
            (_, _) =>
            {
                LoadOption(
                    profileCombo
                        .SelectedItem as
                        AiProfileOption);
            };

        var selectedIndex =
            0;

        if (
            _aiConnectionSettings
                .ActiveProfileId is
                { } activeId)
        {
            var activeIndex =
                options.FindIndex(
                    option =>
                        string.Equals(
                            option.Profile
                                ?.Id,
                            activeId,
                            StringComparison
                                .OrdinalIgnoreCase));

            if (activeIndex >= 0)
            {
                selectedIndex =
                    activeIndex;
            }
        }

        profileCombo.SelectedIndex =
            selectedIndex;

        LoadOption(
            options[
                selectedIndex]);

        var info =
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    InfoBarSeverity
                        .Informational,
                Title =
                    "Credenciais ficam separadas do perfil",
                Message =
                    "Nome, adapter, endpoint e modelo ficam no JSON local. Token/API key é salvo separadamente no Windows Credential Manager e nunca é gravado no mapa ou nos assets."
            };


        var testConnectionButton =
            new Button
            {
                Content =
                    "Testar conexão",
                HorizontalAlignment =
                    HorizontalAlignment
                        .Stretch
            };

        testConnectionButton.Click +=
            async (_, _) =>
            {
                if (
                    !EnsureCommercialFeature(
                        MapStudioEntitlementKeys
                            .AiAssistance,
                        "Teste de IA"))
                {
                    return;
                }

                try
                {
                    testConnectionButton
                        .IsEnabled =
                        false;

                    info.Severity =
                        InfoBarSeverity
                            .Informational;

                    info.Title =
                        "Testando conexão...";

                    info.Message =
                        "Nenhuma configuração será salva durante o teste.";

                    var selectedProfile =
                        (profileCombo.SelectedItem as
                            AiProfileOption)
                        ?.Profile;

                    var temporaryProfile =
                        new MapStudioAiConnectionProfile(
                            selectedProfile
                                ?.Id ??
                            "connection-test",
                            string.IsNullOrWhiteSpace(
                                nameBox.Text)
                                ? "Teste de conexão"
                                : nameBox.Text,
                            adapterBox.Text,
                            endpointBox.Text,
                            modelBox.Text,
                            localCheckBox
                                .IsChecked ==
                            true)
                        .Normalize();

                    await NativeAiProviderFactory
                        .TestConnectionAsync(
                            temporaryProfile,
                            string.IsNullOrWhiteSpace(
                                tokenBox.Password)
                                ? null
                                : tokenBox.Password);

                    info.Severity =
                        InfoBarSeverity
                            .Success;

                    info.Title =
                        "Conexão OK";

                    info.Message =
                        $"O adapter {temporaryProfile.AdapterId} respondeu usando o modelo {temporaryProfile.Model}.";
                }
                catch (Exception exception)
                {
                    info.Severity =
                        InfoBarSeverity
                            .Error;

                    info.Title =
                        "Falha na conexão";

                    info.Message =
                        exception.Message;
                }
                finally
                {
                    testConnectionButton
                        .IsEnabled =
                        true;
                }
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    560
            };

        panel.Children.Add(
            info);

        panel.Children.Add(
            profileCombo);

        panel.Children.Add(
            quickProviderBox);

        panel.Children.Add(
            nameBox);

        panel.Children.Add(
            adapterBox);

        panel.Children.Add(
            adapterSupportText);

        panel.Children.Add(
            endpointBox);

        panel.Children.Add(
            modelBox);

        panel.Children.Add(
            tokenBox);

        panel.Children.Add(
            testConnectionButton);

        panel.Children.Add(
            credentialStatusText);

        panel.Children.Add(
            clearCredentialCheckBox);

        panel.Children.Add(
            localCheckBox);

        panel.Children.Add(
            activeCheckBox);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Provedores de IA",
                Content =
                    panel,
                PrimaryButtonText =
                    "Salvar",
                SecondaryButtonText =
                    "Excluir",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        var answer =
            await dialog
                .ShowAsync();

        var selected =
            profileCombo.SelectedItem as
                AiProfileOption;

        if (
            answer ==
                ContentDialogResult
                    .Secondary)
        {
            if (selected?.Profile is null)
            {
                StatusText.Text =
                    "IA: nenhum perfil existente selecionado para excluir.";

                return;
            }

            var removedId =
                selected.Profile.Id;

            profiles.RemoveAll(
                profile =>
                    string.Equals(
                        profile.Id,
                        removedId,
                        StringComparison
                            .OrdinalIgnoreCase));

            var active =
                string.Equals(
                    _aiConnectionSettings
                        .ActiveProfileId,
                    removedId,
                    StringComparison
                        .OrdinalIgnoreCase)
                    ? null
                    : _aiConnectionSettings
                        .ActiveProfileId;

            _aiConnectionSettings =
                new MapStudioAiConnectionSettings(
                    active,
                    profiles)
                .Normalize();

            NativeAiConnectionSettingsStore
                .Save(
                    _aiConnectionSettings);

            NativeAiCredentialStore
                .DeleteSecret(
                    removedId);

            RefreshAiConnectionUi();

            StatusText.Text =
                $"IA: perfil '{selected.Profile.DisplayName}' e sua credencial foram excluídos.";

            return;
        }

        if (
            answer !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        try
        {
            var id =
                selected?.Profile
                    ?.Id ??
                Guid.NewGuid()
                    .ToString("N");

            var profile =
                new MapStudioAiConnectionProfile(
                    id,
                    nameBox.Text,
                    adapterBox.Text,
                    endpointBox.Text,
                    modelBox.Text,
                    localCheckBox.IsChecked ==
                        true)
                .Normalize();

            profiles.RemoveAll(
                existing =>
                    string.Equals(
                        existing.Id,
                        profile.Id,
                        StringComparison
                            .OrdinalIgnoreCase));

            profiles.Add(
                profile);

            var active =
                activeCheckBox.IsChecked ==
                    true
                    ? profile.Id
                    : string.Equals(
                        _aiConnectionSettings
                            .ActiveProfileId,
                        profile.Id,
                        StringComparison
                            .OrdinalIgnoreCase)
                        ? null
                        : _aiConnectionSettings
                            .ActiveProfileId;

            _aiConnectionSettings =
                new MapStudioAiConnectionSettings(
                    active,
                    profiles)
                .Normalize();

            NativeAiConnectionSettingsStore
                .Save(
                    _aiConnectionSettings);

            if (
                clearCredentialCheckBox
                    .IsChecked ==
                true)
            {
                NativeAiCredentialStore
                    .DeleteSecret(
                        profile.Id);
            }
            else if (
                !string.IsNullOrWhiteSpace(
                    tokenBox.Password))
            {
                NativeAiCredentialStore
                    .SaveSecret(
                        profile.Id,
                        tokenBox.Password);
            }

            RefreshAiConnectionUi();

            var credentialMessage =
                NativeAiCredentialStore
                    .HasSecret(
                        profile.Id)
                    ? " · credencial protegida no Windows Credential Manager"
                    : " · sem credencial salva";

            StatusText.Text =
                (
                    activeCheckBox.IsChecked ==
                        true
                        ? $"IA: perfil ativo '{profile.DisplayName}' salvo."
                        : $"IA: perfil '{profile.DisplayName}' salvo."
                ) +
                credentialMessage;
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"IA: não foi possível salvar o perfil: {exception.Message}";
        }
    }

    private async void OnImportOsmVegetationClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .CoreEditor,
                "Importação de vegetação OSM"))
        {
            return;
        }

        if (
            _session.CurrentMap is not
                { } snapshot ||
            _session.OmsiRootPath is not
                { } root)
        {
            StatusText.Text =
                "Vegetação OSM: abra um mapa no Workspace ou na fonte OMSI.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de importar vegetação OSM.";

            return;
        }

        var georeference =
            await _session
                .LoadMapGeoreferenceAsync();

        if (georeference is null)
        {
            StatusText.Text =
                "Vegetação OSM: o mapa precisa ter .mapstudio/georeference.json.";

            return;
        }

        IReadOnlyList<OmsiAssetIndexEntry>
            sceneryAssets;

        try
        {
            sceneryAssets =
                await _session
                    .GetAssetLibraryAsync(
                        OmsiAssetKind
                            .SceneryObject);
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Vegetação OSM: não foi possível abrir a biblioteca de assets: {exception.Message}";

            return;
        }

        var vegetationAssets =
            sceneryAssets
                .Where(
                    asset =>
                        GetEffectiveAssetGroup(
                                asset) ==
                        OmsiAssetLibraryGroup
                            .Vegetation)
                .OrderBy(
                    asset =>
                        asset.RelativePath,
                    StringComparer
                        .CurrentCultureIgnoreCase)
                .ToArray();

        if (vegetationAssets.Length == 0)
        {
            StatusText.Text =
                "Vegetação OSM: nenhum SCO classificado como vegetação foi encontrado. Atualize a biblioteca de assets primeiro.";

            return;
        }

        var picker =
            new FileOpenPicker
            {
                SuggestedStartLocation =
                    PickerLocationId
                        .DocumentsLibrary
            };

        picker.FileTypeFilter.Add(
            ".osm");

        picker.FileTypeFilter.Add(
            ".xml");

        InitializeWithWindow.Initialize(
            picker,
            _windowHandle);

        var file =
            await picker
                .PickSingleFileAsync();

        if (file is null)
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Lendo árvores, arbustos, tree rows, hedges e áreas de vegetação OSM...";

            var xml =
                await File
                    .ReadAllTextAsync(
                        file.Path);

            var imported =
                new MapStudioOsmVegetationImporter()
                    .Parse(
                        xml);

            var linearImported =
                new MapStudioOsmVegetationLineImporter()
                    .Parse(
                        xml);

            var areaImported =
                new MapStudioOsmVegetationAreaImporter()
                    .Parse(
                        xml);

            if (
                imported.Points.Count >
                    20_000 ||
                linearImported.Lines.Count >
                    5_000 ||
                areaImported.Areas.Count >
                    2_500)
            {
                StatusText.Text =
                    $"OSM de vegetação recusado por segurança: {imported.Points.Count} ponto(s), {linearImported.Lines.Count} linha(s), {areaImported.Areas.Count} área(s).";

                return;
            }

            if (
                imported.Points.Count == 0 &&
                linearImported.Lines.Count == 0 &&
                areaImported.Areas.Count == 0)
            {
                StatusText.Text =
                    "Nenhuma vegetação suportada foi encontrada. São aceitos natural=tree, natural=shrub, natural=tree_row, barrier=hedge, natural=wood, landuse=forest e natural=scrub.";

                return;
            }

            var anchorGeo =
                new MapStudioGeographicAnchor(
                    georeference.Latitude,
                    georeference.Longitude,
                    georeference.AnchorTileX *
                        300.0 +
                    georeference.AnchorX,
                    georeference.AnchorTileY *
                        300.0 +
                    georeference.AnchorY);

            var projectedPoints =
                new MapStudioOsmVegetationProjector()
                    .Project(
                        imported.Points,
                        anchorGeo);

            const double linearSpacingMeters =
                4.0;

            var linearPoints =
                new MapStudioVegetationLineSampler()
                    .ProjectAndSample(
                        linearImported.Lines,
                        anchorGeo,
                        spacingMeters:
                            linearSpacingMeters,
                        maxPoints:
                            10_000);

            const double forestSpacingMeters =
                14.0;

            const double scrubSpacingMeters =
                7.0;

            var areaPoints =
                new MapStudioVegetationAreaScatterer()
                    .ProjectAndScatter(
                        areaImported.Areas,
                        anchorGeo,
                        forestSpacingMeters:
                            forestSpacingMeters,
                        scrubSpacingMeters:
                            scrubSpacingMeters,
                        maxPoints:
                            10_000);

            var projected =
                projectedPoints
                    .Concat(
                        linearPoints)
                    .Concat(
                        areaPoints)
                    .ToArray();

            var mapTileKeys =
                snapshot.Map.Tiles
                    .Select(
                        tile =>
                            (
                                tile.X,
                                tile.Y
                            ))
                    .ToHashSet();

            var candidates =
                projected
                    .Where(
                        point =>
                            mapTileKeys.Contains(
                                (
                                    (int)Math.Floor(
                                        point.Position.X /
                                        300.0),
                                    (int)Math.Floor(
                                        point.Position.Z /
                                        300.0)
                                )))
                    .ToArray();

            if (candidates.Length == 0)
            {
                Viewport
                    .ClearVegetationPreview();

                StatusText.Text =
                    "Nenhum ponto de vegetação OSM projetado cai dentro dos tiles deste mapa.";

                return;
            }

            var maxBatch =
                Math.Min(
                    256,
                    candidates.Length);

            var defaultCount =
                Math.Min(
                    64,
                    maxBatch);

            var countBox =
                new NumberBox
                {
                    Header =
                        "Quantidade nesta operação",
                    Minimum =
                        1,
                    Maximum =
                        maxBatch,
                    Value =
                        defaultCount,
                    SmallChange =
                        1,
                    SpinButtonPlacementMode =
                        NumberBoxSpinButtonPlacementMode
                            .Compact
                };

            var treeAssetCombo =
                new ComboBox
                {
                    Header =
                        "Asset para árvores / tree rows (SCO)",
                    ItemsSource =
                        vegetationAssets,
                    DisplayMemberPath =
                        nameof(
                            OmsiAssetIndexEntry
                                .RelativePath),
                    HorizontalAlignment =
                        HorizontalAlignment
                            .Stretch
                };

            var shrubAssetCombo =
                new ComboBox
                {
                    Header =
                        "Asset para arbustos / hedges (SCO)",
                    ItemsSource =
                        vegetationAssets,
                    DisplayMemberPath =
                        nameof(
                            OmsiAssetIndexEntry
                                .RelativePath),
                    HorizontalAlignment =
                        HorizontalAlignment
                            .Stretch
                };

            var currentAsset =
                GetSelectedAssetLibraryEntry();

            var currentIndex =
                currentAsset is not null &&
                currentAsset.Kind ==
                    OmsiAssetKind
                        .SceneryObject &&
                GetEffectiveAssetGroup(
                        currentAsset) ==
                    OmsiAssetLibraryGroup
                        .Vegetation
                    ? Array.FindIndex(
                        vegetationAssets,
                        asset =>
                            string.Equals(
                                asset.RelativePath,
                                currentAsset
                                    .RelativePath,
                                StringComparison
                                    .OrdinalIgnoreCase))
                    : -1;

            var assetSuggester =
                new MapStudioVegetationAssetSuggester();

            var treeSuggestion =
                assetSuggester
                    .Suggest(
                        vegetationAssets,
                        candidates,
                        MapStudioOsmVegetationKind
                            .Tree);

            var shrubSuggestion =
                assetSuggester
                    .Suggest(
                        vegetationAssets,
                        candidates,
                        MapStudioOsmVegetationKind
                            .Shrub);

            var treeSuggestedIndex =
                treeSuggestion is null
                    ? -1
                    : Array.FindIndex(
                        vegetationAssets,
                        asset =>
                            string.Equals(
                                asset.RelativePath,
                                treeSuggestion
                                    .RelativePath,
                                StringComparison
                                    .OrdinalIgnoreCase));

            var shrubSuggestedIndex =
                shrubSuggestion is null
                    ? -1
                    : Array.FindIndex(
                        vegetationAssets,
                        asset =>
                            string.Equals(
                                asset.RelativePath,
                                shrubSuggestion
                                    .RelativePath,
                                StringComparison
                                    .OrdinalIgnoreCase));

            treeAssetCombo.SelectedIndex =
                currentIndex >=
                    0 &&
                candidates.Any(
                    point =>
                        point.Kind ==
                        MapStudioOsmVegetationKind
                            .Tree)
                    ? currentIndex
                    : treeSuggestedIndex >=
                        0
                        ? treeSuggestedIndex
                        : 0;

            shrubAssetCombo.SelectedIndex =
                currentIndex >=
                    0 &&
                candidates.All(
                    point =>
                        point.Kind ==
                        MapStudioOsmVegetationKind
                            .Shrub)
                    ? currentIndex
                    : shrubSuggestedIndex >=
                        0
                        ? shrubSuggestedIndex
                        : 0;

            var randomRotationCheckBox =
                new CheckBox
                {
                    Content =
                        "Rotação variada e determinística",
                    IsChecked =
                        true
                };

            var selectedForPreview =
                candidates
                    .Take(
                        defaultCount)
                    .ToArray();

            var preview =
                Viewport
                    .PreviewVegetationPoints(
                        selectedForPreview);

            var previewText =
                new TextBlock
                {
                    Text =
                        $"Preview: {preview.RenderedPointCount} ponto(s) no terreno carregado · {preview.SkippedPointCount} sem terreno carregado.",
                    TextWrapping =
                        TextWrapping.Wrap
                };

            var treeCount =
                projected
                    .Count(
                        point =>
                            point.Kind ==
                            MapStudioOsmVegetationKind
                                .Tree);

            var shrubCount =
                projected
                    .Count(
                        point =>
                            point.Kind ==
                            MapStudioOsmVegetationKind
                                .Shrub);

            var treeRowCount =
                linearImported.Lines
                    .Count(
                        line =>
                            line.Kind ==
                            MapStudioOsmVegetationLineKind
                                .TreeRow);

            var hedgeCount =
                linearImported.Lines
                    .Count(
                        line =>
                            line.Kind ==
                            MapStudioOsmVegetationLineKind
                                .Hedge);

            var forestAreaCount =
                areaImported.Areas
                    .Count(
                        area =>
                            area.Kind ==
                            MapStudioOsmVegetationAreaKind
                                .Forest);

            var scrubAreaCount =
                areaImported.Areas
                    .Count(
                        area =>
                            area.Kind ==
                            MapStudioOsmVegetationAreaKind
                                .Scrub);

            var details =
                new TextBlock
                {
                    Text =
                        $"OSM: {imported.Points.Count} node(s) individual(is) + {linearImported.Lines.Count} linha(s) + {areaImported.Areas.Count} área(s).\n" +
                        $"Após amostragem/dispersão: {treeCount} ponto(s) de árvore · {shrubCount} ponto(s) de arbusto/hedge · tree rows: {treeRowCount} · hedges: {hedgeCount} · floresta/bosque: {forestAreaCount} · scrub: {scrubAreaCount}.\n" +
                        $"Dentro do catálogo do mapa: {candidates.Length} · nodes ignorados: {imported.IgnoredNodeCount} · ways lineares ignorados: {linearImported.IgnoredWayCount} · refs lineares ausentes: {linearImported.MissingNodeReferenceCount} · áreas ignoradas: {areaImported.IgnoredWayCount} · relations recusadas/sem suporte seguro: {areaImported.IgnoredRelationCount} · refs de área ausentes: {areaImported.MissingNodeReferenceCount}.",
                    TextWrapping =
                        TextWrapping.Wrap
                };

            var panel =
                new StackPanel
                {
                    Spacing =
                        8,
                    MinWidth =
                        620
                };

            panel.Children.Add(
                details);

            panel.Children.Add(
                treeAssetCombo);

            panel.Children.Add(
                shrubAssetCombo);

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        "As seleções iniciais usam apenas os SCOs reais indexados e tentam combinar species/genus/leaf_type do OSM com o nome/caminho do asset. Você pode trocar os dois assets antes de inserir.",
                    TextWrapping =
                        TextWrapping.Wrap,
                    Opacity =
                        0.78
                });

            panel.Children.Add(
                countBox);

            panel.Children.Add(
                randomRotationCheckBox);

            panel.Children.Add(
                previewText);

            panel.Children.Add(
                new InfoBar
                {
                    IsOpen =
                        true,
                    IsClosable =
                        false,
                    Severity =
                        InfoBarSeverity
                            .Informational,
                    Title =
                        "Assets reais + terreno real",
                    Message =
                        $"Tree rows e hedges são amostrados a cada {linearSpacingMeters:F0} m. Áreas de floresta/bosque usam dispersão determinística de aproximadamente {forestSpacingMeters:F0} m e scrub de {scrubSpacingMeters:F0} m, sempre dentro do polígono OSM. Multipolygons com apenas outer rings são aceitos; relações com inner rings continuam recusadas para não preencher holes incorretamente. O preview mostra os pontos de colocação; na confirmação o Map Studio usa os SCOs escolhidos da instalação do OMSI, encaixa cada item na altura real do terreno carregado e grava tudo em uma única transação com backup."
                });

            var dialog =
                new ContentDialog
                {
                    XamlRoot =
                        MainRoot.XamlRoot,
                    Title =
                        "Importar vegetação do OSM",
                    Content =
                        panel,
                    PrimaryButtonText =
                        $"Inserir até {defaultCount}",
                    CloseButtonText =
                        "Cancelar",
                    DefaultButton =
                        ContentDialogButton
                            .Close,
                    IsPrimaryButtonEnabled =
                        preview.RenderedPointCount >
                        0
                };

            countBox.ValueChanged +=
                (_, args) =>
                {
                    if (
                        !double.IsFinite(
                            args.NewValue))
                    {
                        return;
                    }

                    var count =
                        Math.Clamp(
                            (int)Math.Round(
                                args.NewValue),
                            1,
                            maxBatch);

                    preview =
                        Viewport
                            .PreviewVegetationPoints(
                                candidates
                                    .Take(
                                        count)
                                    .ToArray());

                    previewText.Text =
                        $"Preview: {preview.RenderedPointCount} ponto(s) no terreno carregado · {preview.SkippedPointCount} sem terreno carregado.";

                    dialog.PrimaryButtonText =
                        $"Inserir até {count}";

                    dialog.IsPrimaryButtonEnabled =
                        preview.RenderedPointCount >
                        0;
                };

            var result =
                await dialog
                    .ShowAsync();

            if (
                result !=
                    ContentDialogResult.Primary)
            {
                Viewport
                    .ClearVegetationPreview();

                return;
            }

            if (
                treeAssetCombo.SelectedItem is not
                    OmsiAssetIndexEntry treeAsset ||
                shrubAssetCombo.SelectedItem is not
                    OmsiAssetIndexEntry shrubAsset)
            {
                Viewport
                    .ClearVegetationPreview();

                StatusText.Text =
                    "Vegetação OSM: selecione assets SCO válidos para árvores e arbustos.";

                return;
            }

            var requestedCount =
                Math.Clamp(
                    (int)Math.Round(
                        countBox.Value),
                    1,
                    maxBatch);

            var selected =
                candidates
                    .Take(
                        requestedCount)
                    .ToArray();

            var treePoints =
                selected
                    .Where(
                        point =>
                            point.Kind ==
                            MapStudioOsmVegetationKind
                                .Tree)
                    .ToArray();

            var shrubPoints =
                selected
                    .Where(
                        point =>
                            point.Kind ==
                            MapStudioOsmVegetationKind
                                .Shrub)
                    .ToArray();

            var groupedPlacements =
                new List<
                    (
                        string Path,
                        IReadOnlyList<
                            NativeSceneryPlacementRequest>
                            Requests,
                        int Skipped
                    )>();

            if (treePoints.Length > 0)
            {
                var treePlacement =
                    Viewport
                        .BuildOsmVegetationPlacementRequests(
                            treePoints,
                            treeAsset.RelativePath,
                            randomRotationCheckBox
                                .IsChecked ==
                            true);

                groupedPlacements.Add(
                    (
                        treeAsset.RelativePath,
                        treePlacement.Requests,
                        treePlacement.SkippedPointCount
                    ));
            }

            if (shrubPoints.Length > 0)
            {
                var shrubPlacement =
                    Viewport
                        .BuildOsmVegetationPlacementRequests(
                            shrubPoints,
                            shrubAsset.RelativePath,
                            randomRotationCheckBox
                                .IsChecked ==
                            true);

                groupedPlacements.Add(
                    (
                        shrubAsset.RelativePath,
                        shrubPlacement.Requests,
                        shrubPlacement.SkippedPointCount
                    ));
            }

            Viewport
                .ClearVegetationPreview();

            var groups =
                groupedPlacements
                    .Where(
                        item =>
                            item.Requests.Count >
                            0)
                    .GroupBy(
                        item =>
                            item.Path,
                        StringComparer
                            .OrdinalIgnoreCase)
                    .Select(
                        group =>
                            new NativeSceneryPlacementBatchGroup(
                                group.Key,
                                group
                                    .SelectMany(
                                        item =>
                                            item.Requests)
                                    .ToArray()))
                    .ToArray();

            if (groups.Length == 0)
            {
                StatusText.Text =
                    "Vegetação OSM: nenhum ponto selecionado possui terreno carregado para uma colocação segura.";

                return;
            }

            var insertionCount =
                groups.Sum(
                    group =>
                        group.Placements.Count);

            var skippedCount =
                groupedPlacements.Sum(
                    item =>
                        item.Skipped);

            StatusText.Text =
                $"Inserindo {insertionCount} item(ns) de vegetação OSM com backup...";

            var updated =
                await _session
                    .InsertSceneryObjectMultiBatchAsync(
                        groups);

            RegisterConstructionHistory(
                "Importar vegetação OSM");

            foreach (
                var group in groups)
            {
                RecordAssetUsage(
                    group.SceneryObjectPath);
            }

            await ApplyMapSnapshotAsync(
                updated,
                focusActiveTile:
                    false);

            StatusText.Text =
                $"{insertionCount} item(ns) de vegetação OSM inseridos" +
                (
                    skippedCount >
                        0
                        ? $" · {skippedCount} ignorado(s) sem terreno carregado"
                        : string.Empty
                ) +
                $". Backup: {_session.LastBackupDirectory}";
        }
        catch (Exception exception)
        {
            Viewport
                .ClearVegetationPreview();

            StatusText.Text =
                $"Falha ao importar vegetação OSM: {exception.Message}";
        }
    }

    private async void OnImportOsmBuildingsClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .BuildingStudio,
                "Importação de edifícios OSM"))
        {
            return;
        }

        if (
            _session.CurrentMap is not
                { } snapshot ||
            _session.OmsiRootPath is not
                { } root)
        {
            StatusText.Text =
                "Edifícios OSM: abra um mapa no Workspace ou na fonte OMSI.";

            return;
        }

        if (
            _session.PendingTransformCount >
            0)
        {
            StatusText.Text =
                "Salve as transformações pendentes antes de gerar edifícios OSM.";

            return;
        }

        var georeference =
            await _session
                .LoadMapGeoreferenceAsync();

        if (georeference is null)
        {
            StatusText.Text =
                "Edifícios OSM: o mapa precisa ter .mapstudio/georeference.json.";

            return;
        }

        var picker =
            new FileOpenPicker
            {
                SuggestedStartLocation =
                    PickerLocationId
                        .DocumentsLibrary
            };

        picker.FileTypeFilter.Add(
            ".osm");

        picker.FileTypeFilter.Add(
            ".xml");

        InitializeWithWindow.Initialize(
            picker,
            _windowHandle);

        var file =
            await picker
                .PickSingleFileAsync();

        if (file is null)
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Lendo footprints de edifícios OSM...";

            var xml =
                await File
                    .ReadAllTextAsync(
                        file.Path);

            var imported =
                new MapStudioOsmBuildingImporter()
                    .Parse(
                        xml);

            var pointCount =
                imported.Buildings.Sum(
                    building =>
                        building.Points.Count);

            if (
                imported.Buildings.Count >
                    10_000 ||
                pointCount >
                    250_000)
            {
                StatusText.Text =
                    $"OSM de edifícios recusado por segurança: {imported.Buildings.Count} footprint(s), {pointCount} ponto(s).";

                return;
            }

            var anchorGeo =
                new MapStudioGeographicAnchor(
                    georeference.Latitude,
                    georeference.Longitude,
                    georeference.AnchorTileX *
                        300.0 +
                    georeference.AnchorX,
                    georeference.AnchorTileY *
                        300.0 +
                    georeference.AnchorY);

            var projected =
                new MapStudioOsmBuildingProjector()
                    .Project(
                        imported.Buildings,
                        anchorGeo);

            var mapTileKeys =
                snapshot.Map.Tiles
                    .Select(
                        tile =>
                            (
                                tile.X,
                                tile.Y
                            ))
                    .ToHashSet();

            var candidates =
                projected
                    .Where(
                        building =>
                            mapTileKeys.Contains(
                                (
                                    (int)Math.Floor(
                                        building.Center.X /
                                        300.0),
                                    (int)Math.Floor(
                                        building.Center.Z /
                                        300.0)
                                )))
                    .Take(128)
                    .ToArray();

            if (candidates.Length == 0)
            {
                Viewport
                    .ClearBuildingFootprintPreview();

                StatusText.Text =
                    "Nenhum footprint OSM projetado cai dentro dos tiles deste mapa.";

                return;
            }

            var defaultCount =
                Math.Min(
                    32,
                    candidates.Length);

            var countBox =
                new NumberBox
                {
                    Header =
                        "Quantidade para gerar nesta operação",
                    Minimum =
                        1,
                    Maximum =
                        candidates.Length,
                    Value =
                        defaultCount,
                    SmallChange =
                        1,
                    SpinButtonPlacementMode =
                        NumberBoxSpinButtonPlacementMode
                            .Compact
                };

            NativeBuildingFootprintPreviewGeometry
                preview =
                    Viewport
                        .PreviewBuildingFootprints(
                            candidates
                                .Take(
                                    defaultCount)
                                .ToArray());

            var previewText =
                new TextBlock
                {
                    Text =
                        $"Preview: {preview.RenderedBuildingCount} visível(is) · {preview.SkippedBuildingCount} fora dos tiles carregados.",
                    TextWrapping =
                        TextWrapping.Wrap
                };

            var details =
                new TextBlock
                {
                    Text =
                        $"Encontrados: {imported.Buildings.Count} footprint(s) · projetados: {projected.Count} · dentro do catálogo do mapa: {candidates.Length}.\n" +
                        $"Ways ignorados: {imported.IgnoredWayCount} · node refs ausentes: {imported.MissingNodeReferenceCount} · relations multipolygon recusadas/sem suporte seguro: {imported.IgnoredRelationCount}.",
                    TextWrapping =
                        TextWrapping.Wrap
                };

            var panel =
                new StackPanel
                {
                    Spacing =
                        8,
                    MinWidth =
                        560
                };

            panel.Children.Add(
                details);

            panel.Children.Add(
                countBox);

            panel.Children.Add(
                previewText);

            panel.Children.Add(
                new InfoBar
                {
                    IsOpen =
                        true,
                    IsClosable =
                        false,
                    Severity =
                        InfoBarSeverity
                            .Informational,
                    Title =
                        "Footprint real",
                    Message =
                        "O O3D preserva o contorno OSM e a altura/andares quando disponíveis. Gable permanece restrito a footprints quadriláteros; Hip/Pyramidal é gerado em footprints convexos com 3 ou mais lados; Shed/Skillion usa um plano inclinado e também funciona em footprints simples irregulares ou côncavos. Quando uma forma de roof não puder ser construída com segurança, o topo permanece plano."
                });

            var dialog =
                new ContentDialog
                {
                    XamlRoot =
                        MainRoot.XamlRoot,
                    Title =
                        "Gerar edifícios a partir do OSM",
                    Content =
                        panel,
                    PrimaryButtonText =
                        $"Gerar {defaultCount}",
                    CloseButtonText =
                        "Cancelar",
                    DefaultButton =
                        ContentDialogButton
                            .Close
                };

            countBox.ValueChanged +=
                (_, args) =>
                {
                    if (
                        !double.IsFinite(
                            args.NewValue))
                    {
                        return;
                    }

                    var count =
                        Math.Clamp(
                            (int)Math.Round(
                                args.NewValue),
                            1,
                            candidates.Length);

                    preview =
                        Viewport
                            .PreviewBuildingFootprints(
                                candidates
                                    .Take(
                                        count)
                                    .ToArray());

                    previewText.Text =
                        $"Preview: {preview.RenderedBuildingCount} visível(is) · {preview.SkippedBuildingCount} fora dos tiles carregados.";

                    dialog.PrimaryButtonText =
                        $"Gerar {count}";
                };

            if (
                await dialog.ShowAdaptiveAsync() !=
                    ContentDialogResult.Primary)
            {
                Viewport
                    .ClearBuildingFootprintPreview();

                return;
            }

            var generateCount =
                Math.Clamp(
                    (int)Math.Round(
                        countBox.Value),
                    1,
                    candidates.Length);

            var selected =
                candidates
                    .Take(
                        generateCount)
                    .ToArray();

            var generator =
                new MapStudioFootprintBuildingAssetGenerator();

            var groups =
                new List<
                    NativeSceneryPlacementBatchGroup>(
                        selected.Length);

            var generated =
                0;

            foreach (
                var building in selected)
            {
                StatusText.Text =
                    $"Gerando edifício OSM {generated + 1}/{selected.Length}: {building.Name ?? building.Id}...";

                var asset =
                    await generator
                        .GenerateAsync(
                            root,
                            building);

                var relativePath =
                    Path.GetRelativePath(
                            root,
                            asset.SceneryObjectPath)
                        .Replace(
                            Path.DirectorySeparatorChar,
                            '\\')
                        .Replace(
                            Path.AltDirectorySeparatorChar,
                            '\\');

                var tileX =
                    (int)Math.Floor(
                        building.Center.X /
                        300.0);

                var tileY =
                    (int)Math.Floor(
                        building.Center.Z /
                        300.0);

                var tile =
                    snapshot.Map.Tiles
                        .First(
                            item =>
                                item.X ==
                                    tileX &&
                                item.Y ==
                                    tileY);

                var request =
                    new NativeSceneryPlacementRequest(
                        tile,
                        relativePath,
                        building.Center.X -
                            tileX *
                            300.0,
                        building.Center.Z -
                            tileY *
                            300.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        new Vector3(
                            (float)building.Center.X,
                            0,
                            (float)building.Center.Z),
                        false);

                groups.Add(
                    new NativeSceneryPlacementBatchGroup(
                        relativePath,
                        [request]));

                generated++;
            }

            StatusText.Text =
                $"Inserindo {groups.Count} edifício(s) OSM no mapa em batch...";

            var updated =
                await _session
                    .InsertSceneryObjectMultiBatchAsync(
                        groups);

            RegisterConstructionHistory(
                "Gerar edifícios OSM");

            await ApplyMapSnapshotAsync(
                updated,
                focusActiveTile:
                    false);

            Viewport
                .ClearBuildingFootprintPreview();

            await _session
                .RefreshAssetLibraryAsync();

            await LoadAssetLibraryAsync();

            StatusText.Text =
                $"{generated} edifício(s) OSM gerados e inseridos. Backup do mapa: {_session.LastBackupDirectory}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao gerar edifícios OSM: {exception.Message}";
        }
    }

    private async void OnBuildingStudioClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            !EnsureCommercialFeature(
                MapStudioEntitlementKeys
                    .BuildingStudio,
                "Building Studio"))
        {
            return;
        }

        var root =
            _session.OmsiRootPath;

        if (root is null)
        {
            StatusText.Text =
                "Building Studio: ative o Workspace Map Studio ou selecione uma instalação do OMSI.";

            return;
        }

        var nameBox =
            new TextBox
            {
                Header =
                    "Nome do asset",
                Text =
                    "Nova construção"
            };

        var widthBox =
            new NumberBox
            {
                Header =
                    "Largura (m)",
                Minimum =
                    1,
                Maximum =
                    500,
                Value =
                    10,
                SmallChange =
                    0.5,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var depthBox =
            new NumberBox
            {
                Header =
                    "Profundidade (m)",
                Minimum =
                    1,
                Maximum =
                    500,
                Value =
                    10,
                SmallChange =
                    0.5,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var heightBox =
            new NumberBox
            {
                Header =
                    "Altura das paredes (m)",
                Minimum =
                    1,
                Maximum =
                    500,
                Value =
                    6,
                SmallChange =
                    0.5,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var floorsBox =
            new NumberBox
            {
                Header =
                    "Andares",
                Minimum =
                    1,
                Maximum =
                    300,
                Value =
                    2,
                SmallChange =
                    1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var windowsPerFloorBox =
            new NumberBox
            {
                Header =
                    "Janelas por andar",
                Minimum =
                    0,
                Maximum =
                    64,
                Value =
                    0,
                SmallChange =
                    1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var doorCountBox =
            new NumberBox
            {
                Header =
                    "Portas na fachada",
                Minimum =
                    0,
                Maximum =
                    16,
                Value =
                    0,
                SmallChange =
                    1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var windowWidthBox =
            new NumberBox
            {
                Header =
                    "Largura janela (m)",
                Minimum =
                    0.30,
                Maximum =
                    10,
                Value =
                    1.2,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var windowHeightBox =
            new NumberBox
            {
                Header =
                    "Altura janela (m)",
                Minimum =
                    0.30,
                Maximum =
                    10,
                Value =
                    1.2,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var doorWidthBox =
            new NumberBox
            {
                Header =
                    "Largura porta (m)",
                Minimum =
                    0.50,
                Maximum =
                    10,
                Value =
                    1.0,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var doorHeightBox =
            new NumberBox
            {
                Header =
                    "Altura porta (m)",
                Minimum =
                    1.20,
                Maximum =
                    10,
                Value =
                    2.1,
                SmallChange =
                    0.1,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact
            };

        var roofCombo =
            new ComboBox
            {
                Header =
                    "Telhado",
                HorizontalAlignment =
                    HorizontalAlignment
                        .Stretch,
                SelectedIndex =
                    0
            };

        roofCombo.Items.Add(
            "Plano");

        roofCombo.Items.Add(
            "Duas águas");

        roofCombo.Items.Add(
            "Quatro águas");

        roofCombo.Items.Add(
            "Uma água");

        var roofHeightBox =
            new NumberBox
            {
                Header =
                    "Altura do telhado (m)",
                Minimum =
                    0.25,
                Maximum =
                    100,
                Value =
                    2,
                SmallChange =
                    0.25,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode
                        .Compact,
                IsEnabled =
                    false
            };

        roofCombo.SelectionChanged +=
            (_, _) =>
            {
                roofHeightBox.IsEnabled =
                    roofCombo.SelectedIndex >
                    0;
            };

        var referenceImagePaths =
            new List<string>();

        var facadePathBox =
            new TextBox
            {
                Header =
                    "Imagens de referência / fachada",
                IsReadOnly =
                    true,
                PlaceholderText =
                    "Opcional · até 8 imagens"
            };

        var facadeButton =
            new Button
            {
                Content =
                    "Escolher imagens..."
            };

        facadeButton.Click +=
            async (_, _) =>
            {
                var images =
                    await PickImageFilesAsync();

                if (
                    images.Count ==
                    0)
                {
                    return;
                }

                referenceImagePaths
                    .Clear();

                referenceImagePaths
                    .AddRange(
                        images.Take(
                            8));

                facadePathBox.Text =
                    referenceImagePaths.Count ==
                        1
                        ? referenceImagePaths[0]
                        : $"{referenceImagePaths.Count} imagens · principal: {Path.GetFileName(referenceImagePaths[0])}";
            };

        var activeAiProfile =
            _aiConnectionSettings
                .GetActiveProfile();

        var aiInfo =
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    InfoBarSeverity
                        .Informational,
                Title =
                    "IA opcional",
                Message =
                    activeAiProfile is null
                        ? "Nenhum perfil de IA ativo. O Building Studio continua 100% manual/local."
                        : NativeAiProviderFactory.IsImplemented(
                            activeAiProfile)
                            ? $"Perfil pronto: {activeAiProfile.DisplayName} · {activeAiProfile.AdapterId}. A IA pode analisar a imagem e apenas preencher os campos abaixo."
                            : $"Perfil configurado: {activeAiProfile.DisplayName} · adapter {activeAiProfile.AdapterId}. Esse adapter ainda não possui implementação real nesta build."
            };

        var aiNotesBox =
            new TextBox
            {
                Header =
                    "Observações para IA",
                PlaceholderText =
                    "Opcional · ex.: fachada tem 3 andares, largura conhecida, telhado visto de lado...",
                AcceptsReturn =
                    true,
                TextWrapping =
                    TextWrapping
                        .Wrap,
                MinHeight =
                    58
            };

        var analyzeWithAiButton =
            new Button
            {
                Content =
                    "Analisar referências com IA",
                HorizontalAlignment =
                    HorizontalAlignment
                        .Stretch,
                IsEnabled =
                    activeAiProfile is not
                        null
            };

        analyzeWithAiButton.Click +=
            async (_, _) =>
            {
                if (
                    !EnsureCommercialFeature(
                        MapStudioEntitlementKeys
                            .AiAssistance,
                        "Análise por IA"))
                {
                    return;
                }

                if (activeAiProfile is null)
                {
                    aiInfo.Severity =
                        InfoBarSeverity
                            .Warning;

                    aiInfo.Message =
                        "Configure e ative um perfil em IA → Configurar provedores.";

                    return;
                }

                if (
                    !NativeAiProviderFactory
                        .IsImplemented(
                            activeAiProfile))
                {
                    aiInfo.Severity =
                        InfoBarSeverity
                            .Warning;

                    aiInfo.Message =
                        $"O adapter {activeAiProfile.AdapterId} ainda não está implementado nesta build.";

                    return;
                }

                var aiImagePaths =
                    referenceImagePaths
                        .Where(
                            path =>
                                File.Exists(
                                    path))
                        .Select(
                            path =>
                                (
                                    Path:
                                        path,
                                    MimeType:
                                        GetAiImageMimeType(
                                            path)
                                ))
                        .Where(
                            item =>
                                item.MimeType is
                                    not null)
                        .Take(
                            8)
                        .ToArray();

                if (
                    aiImagePaths.Length ==
                    0)
                {
                    aiInfo.Severity =
                        InfoBarSeverity
                            .Warning;

                    aiInfo.Message =
                        referenceImagePaths.Count ==
                            0
                            ? "Escolha primeiro uma ou mais imagens de referência/fachada."
                            : "Para análise por IA use PNG, JPG/JPEG ou WEBP. BMP/TGA/DDS ainda podem ser usados manualmente como fachada principal.";

                    return;
                }

                try
                {
                    analyzeWithAiButton
                        .IsEnabled =
                        false;

                    aiInfo.Severity =
                        InfoBarSeverity
                            .Informational;

                    aiInfo.Message =
                        $"Analisando {aiImagePaths.Length} referência(s) com {activeAiProfile.DisplayName}...";

                    const long maximumImageBytes =
                        20L *
                        1024L *
                        1024L;

                    const long maximumTotalBytes =
                        64L *
                        1024L *
                        1024L;

                    long totalBytes =
                        0;

                    var imageReferences =
                        new List<
                            MapStudioAiImageReference>(
                                aiImagePaths.Length);

                    foreach (
                        var item in
                            aiImagePaths)
                    {
                        var length =
                            new FileInfo(
                                item.Path)
                                .Length;

                        if (
                            length >
                            maximumImageBytes)
                        {
                            throw new InvalidDataException(
                                $"aiBuildingReferenceImageTooLarge:{Path.GetFileName(item.Path)}");
                        }

                        totalBytes +=
                            length;

                        if (
                            totalBytes >
                            maximumTotalBytes)
                        {
                            throw new InvalidDataException(
                                "aiBuildingReferenceImagesTooLarge");
                        }

                        var bytes =
                            await File
                                .ReadAllBytesAsync(
                                    item.Path);

                        imageReferences.Add(
                            new MapStudioAiImageReference(
                                bytes,
                                item.MimeType!,
                                Path.GetFileName(
                                    item.Path)));
                    }

                    var primaryFacadePath =
                        referenceImagePaths
                            .FirstOrDefault(
                                path =>
                                    File.Exists(
                                        path));

                    var provider =
                        NativeAiProviderFactory
                            .Create(
                                activeAiProfile);

                    var spec =
                        await new MapStudioAiBuildingSpecService()
                            .AnalyzeAsync(
                                provider,
                                new MapStudioBuildingReferenceRequest(
                                    imageReferences,
                                    string.IsNullOrWhiteSpace(
                                        aiNotesBox.Text)
                                        ? null
                                        : aiNotesBox.Text),
                                nameBox.Text,
                                primaryFacadePath);

                    widthBox.Value =
                        spec.WidthMeters;

                    depthBox.Value =
                        spec.DepthMeters;

                    heightBox.Value =
                        spec.WallHeightMeters;

                    floorsBox.Value =
                        spec.FloorCount;

                    windowsPerFloorBox.Value =
                        spec.WindowsPerFloor;

                    doorCountBox.Value =
                        spec.DoorCount;

                    windowWidthBox.Value =
                        spec.WindowWidthMeters;

                    windowHeightBox.Value =
                        spec.WindowHeightMeters;

                    doorWidthBox.Value =
                        spec.DoorWidthMeters;

                    doorHeightBox.Value =
                        spec.DoorHeightMeters;

                    roofCombo.SelectedIndex =
                        spec.RoofType switch
                        {
                            MapStudioBuildingRoofType.Gable =>
                                1,
                            MapStudioBuildingRoofType.Hip =>
                                2,
                            MapStudioBuildingRoofType.Shed =>
                                3,
                            _ =>
                                0
                        };

                    roofHeightBox.Value =
                        Math.Max(
                            0.25,
                            spec.RoofHeightMeters);

                    aiInfo.Severity =
                        InfoBarSeverity
                            .Success;

                    aiInfo.Message =
                        $"Análise de {aiImagePaths.Length} referência(s) concluída por {activeAiProfile.DisplayName}. Revise dimensões, andares e telhado antes de gerar o asset.";
                }
                catch (Exception exception)
                {
                    aiInfo.Severity =
                        InfoBarSeverity
                            .Error;

                    aiInfo.Message =
                        $"Falha na análise por IA: {exception.Message}";
                }
                finally
                {
                    analyzeWithAiButton
                        .IsEnabled =
                        true;
                }
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    520
            };

        panel.Children.Add(
            aiInfo);

        panel.Children.Add(
            aiNotesBox);

        panel.Children.Add(
            analyzeWithAiButton);

        panel.Children.Add(
            nameBox);

        var sizeGrid =
            new Grid
            {
                ColumnSpacing =
                    8
            };

        sizeGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        sizeGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            widthBox,
            0);

        Grid.SetColumn(
            depthBox,
            1);

        sizeGrid.Children.Add(
            widthBox);

        sizeGrid.Children.Add(
            depthBox);

        panel.Children.Add(
            sizeGrid);

        panel.Children.Add(
            heightBox);

        panel.Children.Add(
            floorsBox);

        var openingCountGrid =
            new Grid
            {
                ColumnSpacing =
                    8
            };

        openingCountGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        openingCountGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            windowsPerFloorBox,
            0);

        Grid.SetColumn(
            doorCountBox,
            1);

        openingCountGrid.Children.Add(
            windowsPerFloorBox);

        openingCountGrid.Children.Add(
            doorCountBox);

        panel.Children.Add(
            openingCountGrid);

        var windowSizeGrid =
            new Grid
            {
                ColumnSpacing =
                    8
            };

        windowSizeGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        windowSizeGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            windowWidthBox,
            0);

        Grid.SetColumn(
            windowHeightBox,
            1);

        windowSizeGrid.Children.Add(
            windowWidthBox);

        windowSizeGrid.Children.Add(
            windowHeightBox);

        panel.Children.Add(
            windowSizeGrid);

        var doorSizeGrid =
            new Grid
            {
                ColumnSpacing =
                    8
            };

        doorSizeGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        doorSizeGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            doorWidthBox,
            0);

        Grid.SetColumn(
            doorHeightBox,
            1);

        doorSizeGrid.Children.Add(
            doorWidthBox);

        doorSizeGrid.Children.Add(
            doorHeightBox);

        panel.Children.Add(
            doorSizeGrid);

        panel.Children.Add(
            roofCombo);

        panel.Children.Add(
            roofHeightBox);

        panel.Children.Add(
            facadePathBox);

        panel.Children.Add(
            facadeButton);

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Building Studio",
                Content =
                    panel,
                PrimaryButtonText =
                    "Gerar asset OMSI",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton
                        .Primary
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult
                    .Primary)
        {
            return;
        }

        try
        {
            StatusText.Text =
                "Building Studio: gerando SCO/O3D...";

            var roofType =
                roofCombo.SelectedIndex switch
                {
                    1 =>
                        MapStudioBuildingRoofType.Gable,
                    2 =>
                        MapStudioBuildingRoofType.Hip,
                    3 =>
                        MapStudioBuildingRoofType.Shed,
                    _ =>
                        MapStudioBuildingRoofType.Flat
                };

            var spec =
                new MapStudioBuildingSpec(
                    nameBox.Text,
                    widthBox.Value,
                    depthBox.Value,
                    heightBox.Value,
                    (int)Math.Round(
                        floorsBox.Value),
                    roofType,
                    roofHeightBox.Value,
                    referenceImagePaths
                        .FirstOrDefault(
                            path =>
                                File.Exists(
                                    path)),
                    (int)Math.Round(
                        windowsPerFloorBox.Value),
                    (int)Math.Round(
                        doorCountBox.Value),
                    windowWidthBox.Value,
                    windowHeightBox.Value,
                    doorWidthBox.Value,
                    doorHeightBox.Value);

            var result =
                await new MapStudioBuildingAssetGenerator()
                    .GenerateAsync(
                        root,
                        spec);

            StatusText.Text =
                "Building Studio: asset gerado; atualizando biblioteca...";

            await _session
                .RefreshAssetLibraryAsync();

            await LoadAssetLibraryAsync();

            StatusText.Text =
                result.BackupDirectory is null
                    ? $"Building Studio: {Path.GetFileName(result.SceneryObjectPath)} criado."
                    : $"Building Studio: asset atualizado. Backup: {result.BackupDirectory}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Building Studio falhou: {exception.Message}";
        }
    }

    private void OnOpenUserManualPtBrClick(
        object sender,
        RoutedEventArgs e) =>
        OpenUserManual(
            "Manual-pt-BR.pdf",
            "Manual-pt-BR.md");

    private void OnOpenUserManualEnClick(
        object sender,
        RoutedEventArgs e) =>
        OpenUserManual(
            "Manual-en.pdf",
            "Manual-en.md");

    private void OpenUserManual(
        string pdfFileName,
        string fallbackFileName)
    {
        try
        {
            var docsDirectory =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Docs");

            var pdfPath =
                Path.Combine(
                    docsDirectory,
                    pdfFileName);

            var fallbackPath =
                Path.Combine(
                    docsDirectory,
                    fallbackFileName);

            var manualPath =
                File.Exists(
                    pdfPath)
                    ? pdfPath
                    : fallbackPath;

            if (
                !File.Exists(
                    manualPath))
            {
                StatusText.Text =
                    $"Manual não encontrado em {docsDirectory}.";
                return;
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        manualPath,
                    UseShellExecute =
                        true
                });

            StatusText.Text =
                manualPath.EndsWith(
                    ".pdf",
                    StringComparison
                        .OrdinalIgnoreCase)
                    ? "Manual do Usuário em PDF aberto."
                    : "Manual do Usuário aberto em formato de texto porque o PDF não está disponível nesta build.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Não foi possível abrir o manual: {exception.Message}";
        }
    }

    private async void OnCommercialStatusClick(
        object sender,
        RoutedEventArgs e)
    {
        var mode =
            _commercialState
                .EnforcementEnabled
                ? "Cobrança/licença ativa"
                : "Pré-lançamento · cobrança ainda não aplicada";

        var status =
            _commercialState
                .Status
                .ToString();

        var content =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    480
            };

        content.Children.Add(
            new TextBlock
            {
                Text =
                    mode,
                FontSize =
                    18,
                FontWeight =
                    Microsoft.UI.Text
                        .FontWeights
                        .SemiBold
            });

        content.Children.Add(
            new TextBlock
            {
                Text =
                    $"Estado: {status}\n" +
                    "Billing planejado: Stripe via backend seguro.\n" +
                    "O desktop não armazenará chave secreta da Stripe.\n" +
                    "Checkout, portal do cliente, webhooks e entitlement serão validados no servidor.\n\n" +
                    "Nesta fase de desenvolvimento todas as funções permanecem liberadas.",
                TextWrapping =
                    TextWrapping
                        .Wrap
            });

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Assinatura e licença",
                Content =
                    content,
                CloseButtonText =
                    "Fechar"
            };

        await dialog
            .ShowAsync();
    }

    private bool EnsureCommercialFeature(
        string entitlement,
        string featureName)
    {
        var gate =
            MapStudioFeatureGate
                .Evaluate(
                    _commercialState,
                    entitlement);

        if (gate.Allowed)
        {
            return true;
        }

        StatusText.Text =
            $"{featureName}: assinatura/licença necessária.";

        return false;
    }

    private async void OnInspectAttachmentsClick(
        object sender,
        RoutedEventArgs e)
    {
        var snapshot =
            _session.CurrentMap;

        if (snapshot is null)
        {
            StatusText.Text =
                "Abra um mapa antes de inspecionar attachments.";

            return;
        }

        var items =
            snapshot.Tiles
                .SelectMany(
                    tile =>
                        (
                            tile.Content
                                .Attachments ??
                            Array.Empty<
                                OmsiPlacedAttachment>()
                        )
                        .Select(
                            attachment =>
                            {
                                var detail =
                                    attachment.Kind switch
                                    {
                                        OmsiAttachmentKind
                                            .ObjectAttachment =>
                                            $"parent #{attachment.AttachedToObjectId?.ToString() ?? "?"} · attach point {attachment.AttachPointIndex?.ToString() ?? "?"}",

                                        _ =>
                                            $"X {attachment.X?.ToString("F2", CultureInfo.InvariantCulture) ?? "?"} · Z {attachment.Z?.ToString("F2", CultureInfo.InvariantCulture) ?? "?"} · Y {attachment.Y?.ToString("F2", CultureInfo.InvariantCulture) ?? "?"} · intervalo {attachment.Interval?.ToString("F2", CultureInfo.InvariantCulture) ?? "?"} · distância {attachment.Distance?.ToString("F2", CultureInfo.InvariantCulture) ?? "?"}"
                                    };

                                var parent =
                                    string.IsNullOrWhiteSpace(
                                        attachment
                                            .VariableParentValue)
                                        ? string.Empty
                                        : $" · varparent {attachment.VariableParentValue}";

                                return new AttachmentInspectorItem(
                                    tile.Reference,
                                    attachment,
                                    $"Tile {tile.Reference.X},{tile.Reference.Y} · {attachment.Kind} · #{attachment.AttachmentId}\n" +
                                    $"{attachment.AssetPath}\n" +
                                    detail +
                                    parent);
                            }))
                .ToArray();

        var list =
            new ListView
            {
                Height =
                    420,
                SelectionMode =
                    ListViewSelectionMode
                        .Single,
                DisplayMemberPath =
                    nameof(
                        AttachmentInspectorItem
                            .DisplayText),
                ItemsSource =
                    items
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    650
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"{items.Length} attachment(s) reconhecido(s) em {snapshot.Tiles.Count} tile(s) carregado(s).",
                FontSize =
                    16,
                FontWeight =
                    Microsoft.UI.Text
                        .FontWeights
                        .SemiBold
            });

        panel.Children.Add(
            list);

        panel.Children.Add(
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    InfoBarSeverity
                        .Informational,
                Title =
                    "Round-trip preservativo",
                Message =
                    "Transformações numéricas de attachments válidos podem ser editadas com backup. Asset, IDs, parent/attach point, varparent e campos desconhecidos permanecem preservados e somente leitura. Em modo desempenho, a lista cobre apenas os tiles carregados."
            });

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Attachments do mapa",
                Content =
                    panel,
                PrimaryButtonText =
                    "Editar...",
                CloseButtonText =
                    "Fechar",
                IsPrimaryButtonEnabled =
                    false
            };

        list.SelectionChanged +=
            (_, _) =>
            {
                dialog.IsPrimaryButtonEnabled =
                    list.SelectedItem is
                        AttachmentInspectorItem;
            };

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary ||
            list.SelectedItem is not
                AttachmentInspectorItem selected)
        {
            return;
        }

        await EditAttachmentAsync(
            selected);
    }

    private static NumberBox CreateAttachmentNumberBox(
        string header,
        double value)
    {
        return new NumberBox
        {
            Header =
                header,
            Minimum =
                -1_000_000,
            Maximum =
                1_000_000,
            Value =
                value,
            SmallChange =
                0.1,
            SpinButtonPlacementMode =
                NumberBoxSpinButtonPlacementMode
                    .Compact
        };
    }

    private async Task EditAttachmentAsync(
        AttachmentInspectorItem item)
    {
        var attachment =
            item.Attachment;

        NumberBox? xBox =
            null;

        NumberBox? zBox =
            null;

        NumberBox? yBox =
            null;

        NumberBox? intervalBox =
            null;

        NumberBox? distanceBox =
            null;

        var rotationBox =
            CreateAttachmentNumberBox(
                "Rotação",
                attachment.Rotation);

        var pitchBox =
            CreateAttachmentNumberBox(
                "Pitch",
                attachment.Pitch);

        var bankBox =
            CreateAttachmentNumberBox(
                "Bank",
                attachment.Bank);

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    520
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    $"{attachment.Kind} · #{attachment.AttachmentId}\n{attachment.AssetPath}",
                TextWrapping =
                    TextWrapping.Wrap,
                FontWeight =
                    Microsoft.UI.Text
                        .FontWeights
                        .SemiBold
            });

        if (
            attachment.Kind !=
            OmsiAttachmentKind
                .ObjectAttachment)
        {
            xBox =
                CreateAttachmentNumberBox(
                    "X",
                    attachment.X ??
                        0);

            zBox =
                CreateAttachmentNumberBox(
                    "Z",
                    attachment.Z ??
                        0);

            yBox =
                CreateAttachmentNumberBox(
                    "Y",
                    attachment.Y ??
                        0);

            intervalBox =
                CreateAttachmentNumberBox(
                    "Intervalo",
                    attachment.Interval ??
                        0);

            distanceBox =
                CreateAttachmentNumberBox(
                    "Distância",
                    attachment.Distance ??
                        0);

            panel.Children.Add(
                xBox);

            panel.Children.Add(
                zBox);

            panel.Children.Add(
                yBox);
        }
        else
        {
            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Parent #{attachment.AttachedToObjectId?.ToString() ?? "?"} · attach point {attachment.AttachPointIndex?.ToString() ?? "?"} · esses vínculos são preservados.",
                    TextWrapping =
                        TextWrapping.Wrap,
                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Windows.UI.Color.FromArgb(
                                255,
                                120,
                                149,
                                173))
                });
        }

        panel.Children.Add(
            rotationBox);

        panel.Children.Add(
            pitchBox);

        panel.Children.Add(
            bankBox);

        if (
            intervalBox is not
                null &&
            distanceBox is not
                null)
        {
            panel.Children.Add(
                intervalBox);

            panel.Children.Add(
                distanceBox);
        }

        if (
            !string.IsNullOrWhiteSpace(
                attachment
                    .VariableParentValue))
        {
            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        $"varparent: {attachment.VariableParentValue} (somente leitura)",
                    TextWrapping =
                        TextWrapping.Wrap
                });
        }

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    MainRoot.XamlRoot,
                Title =
                    "Editar attachment",
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

        if (
            await dialog.ShowAdaptiveAsync() !=
                ContentDialogResult.Primary)
        {
            return;
        }

        var edit =
            new OmsiAttachmentTransformEdit(
                attachment
                    .SourceSectionOrdinal,
                attachment.Kind,
                attachment.AttachmentId,
                attachment.AssetPath,
                attachment.RawValues,
                xBox?.Value ??
                    attachment.X,
                zBox?.Value ??
                    attachment.Z,
                yBox?.Value ??
                    attachment.Y,
                rotationBox.Value,
                pitchBox.Value,
                bankBox.Value,
                intervalBox?.Value ??
                    attachment.Interval,
                distanceBox?.Value ??
                    attachment.Distance);

        try
        {
            StatusText.Text =
                $"Salvando attachment #{attachment.AttachmentId}...";

            var result =
                await _session
                    .UpdateAttachmentAsync(
                        item.Tile,
                        edit);

            await ApplyMapSnapshotAsync(
                result.Snapshot,
                focusActiveTile:
                    false);

            StatusText.Text =
                $"Attachment #{result.Attachment.AttachmentId} atualizado com backup: {result.BackupDirectory}";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Falha ao editar attachment: {exception.Message}";
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

    private static string?
        GetAiImageMimeType(
            string path) =>
        Path.GetExtension(
                path)
            .ToLowerInvariant() switch
        {
            ".png" =>
                "image/png",
            ".jpg" or
            ".jpeg" =>
                "image/jpeg",
            ".webp" =>
                "image/webp",
            _ =>
                null
        };

    private async Task<IReadOnlyList<string>>
        PickImageFilesAsync()
    {
        var picker =
            new FileOpenPicker
            {
                SuggestedStartLocation =
                    PickerLocationId
                        .PicturesLibrary
            };

        picker.FileTypeFilter.Add(
            ".png");
        picker.FileTypeFilter.Add(
            ".jpg");
        picker.FileTypeFilter.Add(
            ".jpeg");
        picker.FileTypeFilter.Add(
            ".bmp");
        picker.FileTypeFilter.Add(
            ".tga");
        picker.FileTypeFilter.Add(
            ".webp");
        picker.FileTypeFilter.Add(
            ".dds");

        InitializeWithWindow.Initialize(
            picker,
            _windowHandle);

        var files =
            await picker
                .PickMultipleFilesAsync();

        return files
            .Take(
                8)
            .Select(
                file =>
                    file.Path)
            .Where(
                path =>
                    !string.IsNullOrWhiteSpace(
                        path))
            .ToArray();
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
