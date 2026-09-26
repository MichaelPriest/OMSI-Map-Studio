using System.Numerics;
using System.Runtime.InteropServices;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Vegetation;
using MapStudio.Native.Interop;
using MapStudio.Native.Services;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Renderer.Scene;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Viewport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using Windows.System;

namespace MapStudio.Native.Controls;

public enum NativeSelectionContextAction
{
    Move,
    Rotate,
    Duplicate,
    Delete,
    Focus,
    Inspector,
    EditCurve,
    EditEndpoints,
    Split,
    Parallel,
    Elevate,
    Level,
    Lower,
    Replace,
    Mirror,
    Flow
}

public sealed partial class NativeViewport : UserControl
{
    private NativeViewportRuntime? _runtime;
    private readonly System.Threading.SemaphoreSlim
        _mapSnapshotReloadGate =
            new(1, 1);
    private readonly object
        _mapSnapshotReloadSync =
            new();
    private CancellationTokenSource?
        _mapSnapshotReloadCancellation;
    private long _mapSnapshotReloadSequence;
    private bool _leftPressed;
    private bool _isPanning;
    private bool _isOrbiting;
    private bool _rightPressed;
    private double _rightPressX;
    private double _rightPressY;
    private bool _isSplineDragCreating;
    private bool _splineSplitPickActive;
    private bool _splineJoinPickActive;
    private int _splineJoinSourceId = -1;
    private double _splineJoinMaximumRadius = 200.0;
    private bool _isManipulatingGizmo;
    private bool _selectionBoxPending;
    private bool _isSelectionBoxDragging;
    private bool _selectionBoxAdditive;
    private double _selectionBoxStartX;
    private double _selectionBoxStartY;
    private double _lastPanX;
    private double _lastPanY;
    private bool _navigationRenderingHooked;
    private bool _navigationFramePending;
    private uint _pendingNavigationPixelX;
    private uint _pendingNavigationPixelY;
    private double _pendingOrbitDeltaX;
    private double _pendingOrbitDeltaY;
    private long _lastHoverTick;
    private uint _lastHoverPixelX =
        uint.MaxValue;
    private uint _lastHoverPixelY =
        uint.MaxValue;
    private bool _swapChainBound;
    private bool _terrainPointPickActive;
    private bool _terrainPointPickPersistent;
    private bool _terrainBrushPreviewEnabled;
    private double _terrainBrushRadiusMeters = 20.0;
    private double _terrainBrushFeather = 0.25;
    private double _lastPointerLogicalX = double.NaN;
    private double _lastPointerLogicalY = double.NaN;
    private readonly InputCursor _defaultCursor =
        InputSystemCursor.Create(
            InputSystemCursorShape.Arrow);
    private readonly InputCursor _panCursor =
        InputSystemCursor.Create(
            InputSystemCursorShape.Hand);
    private readonly InputCursor _orbitCursor =
        InputSystemCursor.Create(
            InputSystemCursorShape.SizeAll);

    public NativeViewport()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;

        SwapChainSurface
            .CompositionScaleChanged +=
            OnCompositionScaleChanged;
    }

    public event EventHandler<string>? PointerStatusChanged;

    public event EventHandler<string>? SelectionStatusChanged;

    public event Action<
        NativePendingTransformEdit>?
        TransformEditPending;

    public event Action<
        NativeSelectionInfo?>?
        SelectionChanged;

    public event Action<
        NativeSceneryPlacementRequest>?
        SceneryPlacementRequested;

    public event Action<
        NativeSplinePlacementRequest>?
        SplinePlacementRequested;

    public event Action<
        NativeSplinePlacementControlState?>?
        SplinePlacementControlStateChanged;

    public event Action<
        NativeTerrainEditPoint>?
        TerrainPointSelected;

    public event Action<
        NativeSelectionContextAction>?
        SelectionContextActionRequested;

    public event Action<
        NativeSplineSplitRequest>?
        SplineSplitRequested;

    public event Action<
        NativeTrafficPathNode>?
        TrafficPathNodeFocused;

    public event Action<
        NativeTrafficPathNode>?
        TrafficPathNodeEditRequested;

    public IReadOnlyList<NativeSelectionInfo>
        GetSelectedSelectionInfos() =>
        _runtime
            ?.GetSelectedSelectionInfos() ??
        Array.Empty<NativeSelectionInfo>();

    public bool TryBuildSelectedGroupDuplicateRequests(
        Vector3 worldOffset,
        out IReadOnlyList<NativeSceneryPlacementRequest>
            objectRequests,
        out IReadOnlyList<NativeSplinePlacementRequest>
            splineRequests,
        out string status)
    {
        objectRequests =
            Array.Empty<NativeSceneryPlacementRequest>();

        splineRequests =
            Array.Empty<NativeSplinePlacementRequest>();

        status =
            "Duplicar grupo indisponível.";

        return
            _runtime is not null &&
            _runtime
                .TryBuildSelectedGroupDuplicateRequests(
                    worldOffset,
                    out objectRequests,
                    out splineRequests,
                    out status);
    }

    public int SelectSelectionInfos(
        IReadOnlyList<NativeSelectionInfo>
            selections,
        bool focus)
    {
        if (_runtime is null)
        {
            return 0;
        }

        var selected =
            _runtime
                .SelectSelectionInfos(
                    selections,
                    focus);

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            selected.Count >
                1
                ? $"{selected.Count} itens selecionados."
                : selected.Count ==
                    1
                    ? "1 item selecionado."
                    : "Sem seleção.");

        return
            selected.Count;
    }

    public bool TryBuildSplineXExport(
        IReadOnlyCollection<int> splineIds,
        out NativeSplineXExportResult? result,
        out string status)
    {
        result =
            null;

        status =
            "Spline Export indisponível.";

        return _runtime is not null &&
            _runtime
                .TryBuildSplineXExport(
                    splineIds,
                    out result,
                    out status);
    }

    public bool TryBuildSplineCompleteToRequest(
        int sourceSplineId,
        int targetSplineId,
        double maximumRadius,
        out NativeSplinePlacementRequest? request,
        out string status)
    {
        request = null;
        status =
            "Complete to indisponível.";

        return _runtime is not null &&
            _runtime
                .TryBuildSplineCompleteToRequest(
                    sourceSplineId,
                    targetSplineId,
                    maximumRadius,
                    out request,
                    out status);
    }

    public NativeProceduralJunctionPlan
        BuildProceduralJunctionPlan(
            MapStudioRoadGraph graph) =>
        _runtime
            ?.BuildProceduralJunctionPlan(
                graph) ??
        new NativeProceduralJunctionPlan(
            Array.Empty<
                NativeProceduralJunctionPlanItem>(),
            graph.Junctions.Count);

    public NativeProceduralRoadPlacementBuildResult
        BuildProceduralRoadPlacementRequests(
            MapStudioRoadGraph graph) =>
        _runtime
            ?.BuildProceduralRoadPlacementRequests(
                graph) ??
        new NativeProceduralRoadPlacementBuildResult(
            Array.Empty<
                NativeSplinePlacementRequest>(),
            Array.Empty<
                NativeProceduralRoadPlacementLink>(),
            graph.Segments.Count);

    public NativeOsmVegetationPlacementBuildResult
        BuildOsmVegetationPlacementRequests(
            IReadOnlyList<MapStudioProjectedVegetationPoint> points,
            string sceneryObjectPath,
            bool randomRotation) =>
        _runtime
            ?.BuildOsmVegetationPlacementRequests(
                points,
                sceneryObjectPath,
                randomRotation) ??
        new NativeOsmVegetationPlacementBuildResult(
            Array.Empty<NativeSceneryPlacementRequest>(),
            points.Count);

    public NativeVegetationPreviewGeometry
        PreviewVegetationPoints(
            IReadOnlyList<MapStudioProjectedVegetationPoint> points) =>
        _runtime
            ?.PreviewVegetationPoints(
                points) ??
        new NativeVegetationPreviewGeometry(
            [],
            0,
            points.Count);

    public void ClearVegetationPreview() =>
        _runtime
            ?.ClearVegetationPreview();

    public NativeBuildingFootprintPreviewGeometry
        PreviewBuildingFootprints(
            IReadOnlyList<MapStudioProjectedBuildingFootprint> buildings) =>
        _runtime
            ?.PreviewBuildingFootprints(
                buildings) ??
        new NativeBuildingFootprintPreviewGeometry(
            [],
            0,
            buildings.Count);

    public void ClearBuildingFootprintPreview() =>
        _runtime
            ?.ClearBuildingFootprintPreview();

    public NativeProceduralRoadPreviewGeometry
        PreviewProceduralRoadGraph(
            MapStudioRoadGraph graph) =>
        _runtime
            ?.PreviewProceduralRoadGraph(
                graph) ??
        new NativeProceduralRoadPreviewGeometry(
            [],
            0,
            0);

    public void ClearProceduralRoadPreview() =>
        _runtime
            ?.ClearProceduralRoadPreview();

    public int PreviewTimetableTrack(
        IReadOnlyList<
            MapStudio.Core.Omsi.Timetables
                .OmsiTimetableTrackEntry>
            entries) =>
        _runtime
            ?.PreviewTimetableTrack(
                entries) ??
        0;

    public int PreviewStationLink(
        IReadOnlyList<
            MapStudio.Core.Omsi.Timetables
                .OmsiStationLinkEntry>
            entries) =>
        _runtime
            ?.PreviewStationLink(
                entries) ??
        0;

    public void ClearTimetableRoutePreview() =>
        _runtime
            ?.ClearTimetableRoutePreview();

    public IReadOnlyList<
        NativeTrafficLightProgramInfo>
        GetTrafficLightPrograms() =>
        _runtime
            ?.GetTrafficLightPrograms() ??
        Array.Empty<
            NativeTrafficLightProgramInfo>();

    public void SetTrafficSignalPhasePreview(
        string programName,
        int objectId,
        int signalCode,
        double seconds,
        double cycleDuration)
    {
        TrafficSignalPreviewPanel.Visibility =
            Visibility.Visible;

        TrafficSignalProgramText.Text =
            $"{programName} · objeto #{objectId}";

        TrafficSignalPhaseText.Text =
            NativeTrafficLightProgramInfo
                .DescribeSignalCode(
                    signalCode) +
            $" [{signalCode}]";

        TrafficSignalTimeText.Text =
            $"t={seconds:F2}s / {Math.Max(0, cycleDuration):F2}s";

        var redActive =
            signalCode is >= 0 and <= 5;

        var yellowActive =
            signalCode is >= 3 and <= 5 or
                >= 9 and <= 11;

        var greenActive =
            signalCode is >= 6 and <= 8;

        TrafficSignalRedLamp.Opacity =
            redActive
                ? 1.0
                : 0.18;

        TrafficSignalYellowLamp.Opacity =
            yellowActive
                ? 1.0
                : 0.18;

        TrafficSignalGreenLamp.Opacity =
            greenActive
                ? 1.0
                : 0.18;
    }

    public void ClearTrafficSignalPhasePreview()
    {
        TrafficSignalPreviewPanel.Visibility =
            Visibility.Collapsed;

        TrafficSignalRedLamp.Opacity =
            0.18;

        TrafficSignalYellowLamp.Opacity =
            0.18;

        TrafficSignalGreenLamp.Opacity =
            0.18;
    }

    public IReadOnlyList<
        NativeExplorerItem>
        GetExplorerItems() =>
        _runtime
            ?.GetExplorerItems() ??
        Array.Empty<
            NativeExplorerItem>();

    public bool SelectExplorerItem(
        NativeExplorerItem item,
        bool focus)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        var info =
            _runtime
                ?.SelectExplorerItem(
                    item.PickingId,
                    focus);

        if (info is null)
        {
            return false;
        }

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            info.Kind ==
                MapStudio.Renderer.Picking
                    .PickingKind.Object
                ? $"Objeto #{info.EntityId} selecionado pelo Explorer."
                : $"Spline #{info.EntityId} selecionada pelo Explorer.");

        return true;
    }

    public bool CanUndo =>
        _runtime?.CanUndo ??
        false;

    public bool CanRedo =>
        _runtime?.CanRedo ??
        false;

    public bool SnapEnabled =>
        _runtime?.SnapEnabled ??
        false;

    public void SetSnapEnabled(
        bool enabled)
    {
        _runtime?.SetSnapEnabled(
            enabled);

        PointerStatusChanged?.Invoke(
            this,
            enabled
                ? "Snap ativo: 0,25 m / 5°"
                : "Snap desativado");
    }

    public bool Undo()
    {
        var edit =
            _runtime?.Undo();

        if (edit is null)
        {
            return false;
        }

        foreach (
            var pendingEdit in
                _runtime!
                    .LastTransformEdits)
        {
            TransformEditPending
                ?.Invoke(
                    pendingEdit);
        }

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            _runtime.LastTransformEdits.Count >
                1
                ? $"Transformação de {_runtime.LastTransformEdits.Count} itens desfeita."
                : "Última transformação desfeita.");

        return true;
    }

    public bool ApplySelectionInfo(
        NativeSelectionInfo values)
    {
        var edit =
            _runtime
                ?.ApplySelectionInfo(
                    values);

        if (edit is null)
        {
            return false;
        }

        TransformEditPending
            ?.Invoke(
                edit);

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            edit.IsObject
                ? "Valores do objeto aplicados pelo Inspector."
                : "Valores da spline aplicados pelo Inspector.");

        return true;
    }

    public bool LevelSelectedSplineToTerrain(
        out double startHeight,
        out double endHeight)
    {
        startHeight = 0.0;
        endHeight = 0.0;

        var edit =
            _runtime
                ?.LevelSelectedSplineToTerrain(
                    out startHeight,
                    out endHeight);

        if (edit is null)
        {
            return false;
        }

        TransformEditPending
            ?.Invoke(
                edit);

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            $"Spline nivelada ao terreno: {startHeight:F2} → {endHeight:F2} m.");

        return true;
    }

    public bool Redo()
    {
        var edit =
            _runtime?.Redo();

        if (edit is null)
        {
            return false;
        }

        foreach (
            var pendingEdit in
                _runtime!
                    .LastTransformEdits)
        {
            TransformEditPending
                ?.Invoke(
                    pendingEdit);
        }

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            _runtime.LastTransformEdits.Count >
                1
                ? $"Transformação de {_runtime.LastTransformEdits.Count} itens refeita."
                : "Transformação refeita.");

        return true;
    }

    public bool IsSceneryPlacementActive =>
        _runtime
            ?.IsSceneryPlacementActive ??
        false;

    public bool IsSplinePlacementActive =>
        _runtime
            ?.IsSplinePlacementActive ??
        false;

    public int ReferenceOverlayBatchCount =>
        _runtime
            ?.ReferenceOverlayBatchCount ??
        0;

    public int ReferenceOverlayTriangleCount =>
        _runtime
            ?.ReferenceOverlayTriangleCount ??
        0;

    public bool IsSplineJoinPickActive =>
        _splineJoinPickActive;

    public NativeSplinePlacementControlState?
        SplinePlacementControlState =>
        _runtime
            ?.GetSplinePlacementControlState();

    public void SetSplinePlacementElevationOffset(
        double offset) =>
        _runtime
            ?.SetSplinePlacementElevationOffset(
                offset);

    public void SetSplinePlacementElevationMode(
        NativeRoadElevationMode mode) =>
        _runtime
            ?.SetSplinePlacementElevationMode(
                mode);

    public void SetSplinePlacementHeightMode(
        bool isHeightSpline) =>
        _runtime
            ?.SetSplinePlacementHeightMode(
                isHeightSpline);

    public void SetSplineEasyRoadOptions(
        bool enabled,
        double curveOffset) =>
        _runtime
            ?.SetSplineEasyRoadOptions(
                enabled,
                curveOffset);

    public bool TryBeginEasyRoadCurveControl(
        out string status)
    {
        status =
            "Estrada fácil indisponível.";

        if (
            _runtime is null ||
            !_runtime
                .TryBeginEasyRoadCurveControl(
                    out status))
        {
            return false;
        }

        SplinePlacementControlStateChanged
            ?.Invoke(
                _runtime
                    .GetSplinePlacementControlState());

        return true;
    }

    public bool TryApplyEasyRoadControlPoints(
        double startX,
        double startZ,
        double endX,
        double endZ)
    {
        if (
            _runtime is null ||
            !_runtime
                .TrySetEasyRoadControlPoints(
                    startX,
                    startZ,
                    endX,
                    endZ,
                    out var state))
        {
            return false;
        }

        SplinePlacementControlStateChanged
            ?.Invoke(
                state);

        return true;
    }

    public bool TryConfirmEasyRoad(
        out NativeSplinePlacementRequest?
            request,
        out string status)
    {
        request =
            null;

        status =
            "Estrada fácil indisponível.";

        if (_runtime is null)
        {
            return false;
        }

        var result =
            _runtime
                .TryConfirmEasyRoad(
                    out request,
                    out status);

        if (result)
        {
            SplinePlacementControlStateChanged
                ?.Invoke(
                    null);
        }

        return result;
    }

    public void SetSplineEndpointSnapOptions(
        bool enabled,
        double distance,
        bool autoConnect) =>
        _runtime
            ?.SetSplineEndpointSnapOptions(
                enabled,
                distance,
                autoConnect);

    public bool TryGetAutoConnectLinksForSelectedSpline(
        double maximumDistance,
        out int previousSplineId,
        out int nextSplineId,
        out string status)
    {
        previousSplineId =
            -1;

        nextSplineId =
            -1;

        status =
            "Auto conectar indisponível.";

        return _runtime is not null &&
            _runtime
                .TryGetAutoConnectLinksForSelectedSpline(
                    maximumDistance,
                    out previousSplineId,
                    out nextSplineId,
                    out status);
    }

    public bool SeedSplinePlacementStart(
        System.Numerics.Vector3 start,
        int previousSplineId =
            -1) =>
        _runtime
            ?.SeedSplinePlacementStart(
                start,
                previousSplineId) ??
        false;

    public async Task<bool>
        BeginSplinePlacementAsync(
            string omsiRoot,
            OmsiAssetIndexEntry asset,
            bool curved,
            CancellationToken cancellationToken =
                default)
    {
        if (
            _runtime is null ||
            asset.Kind !=
                OmsiAssetKind.Spline)
        {
            return false;
        }

        CancelSplineJoinPick();

        var started =
            await _runtime
                .BeginSplinePlacementAsync(
                    omsiRoot,
                    asset.RelativePath,
                    curved,
                    cancellationToken);

        if (started)
        {
            RuntimeText.Text =
                $"Construindo spline · {asset.RelativePath}";

            SelectionStatusChanged?.Invoke(
                this,
                curved
                    ? "Curva: clique início, fim e ponto de curvatura."
                    : "Reta: clique início e fim.");
        }

        return started;
    }

    public async Task<bool>
        BeginSplinePlacementCopyAsync(
            string omsiRoot,
            string splinePath,
            bool curved,
            CancellationToken cancellationToken =
                default)
    {
        if (_runtime is null)
        {
            return false;
        }

        CancelSplineJoinPick();

        _runtime.SetSplineEndpointSnapOptions(
            enabled: false,
            distance: 5.0,
            autoConnect: false);

        _runtime.SetSplinePlacementElevationOffset(
            0.0);

        _runtime.SetSplinePlacementElevationMode(
            NativeRoadElevationMode
                .FollowTerrain);

        _runtime.SetSplineEasyRoadOptions(
            enabled: false,
            curveOffset: 0.0);

        var started =
            await _runtime
                .BeginSplinePlacementAsync(
                    omsiRoot,
                    splinePath,
                    curved,
                    cancellationToken);

        if (started)
        {
            RuntimeText.Text =
                $"Construindo cópia · {splinePath}";

            SelectionStatusChanged?.Invoke(
                this,
                curved
                    ? "Cópia curva desconectada: clique início, fim e ponto de curvatura."
                    : "Cópia reta desconectada: clique início e fim.");
        }

        return started;
    }

    public void CancelSplinePlacement()
    {
        _runtime?.CancelSplinePlacement();
        CancelSplineJoinPick();

        SplinePlacementControlStateChanged
            ?.Invoke(
                null);
    }

    public void SetSceneryRoadSnapOptions(
        bool enabled,
        double distance) =>
        _runtime
            ?.SetSceneryRoadSnapOptions(
                enabled,
                distance);

    public async Task<bool>
        BeginSceneryPlacementAsync(
            string omsiRoot,
            OmsiAssetIndexEntry asset,
            CancellationToken cancellationToken =
                default)
    {
        if (
            _runtime is null ||
            asset.Kind !=
                OmsiAssetKind
                    .SceneryObject)
        {
            return false;
        }

        CancelSplineJoinPick();

        var started =
            await _runtime
                .BeginSceneryPlacementAsync(
                    omsiRoot,
                    asset.RelativePath,
                    cancellationToken);

        if (started)
        {
            RuntimeText.Text =
                $"Posicionando · {asset.RelativePath}";

            SelectionStatusChanged?.Invoke(
                this,
                "Mova o cursor sobre o terreno e clique para posicionar o objeto.");
        }

        return started;
    }

    public async Task<bool>
        BeginSceneryPlacementCopyAsync(
            string omsiRoot,
            string sceneryObjectPath,
            double z,
            double rotation,
            double pitch,
            double bank,
            CancellationToken cancellationToken =
                default)
    {
        if (_runtime is null)
        {
            return false;
        }

        CancelSplineJoinPick();

        _runtime
            .SetSceneryRoadSnapOptions(
                enabled: false,
                distance: 8.0);

        var started =
            await _runtime
                .BeginSceneryPlacementAsync(
                    omsiRoot,
                    sceneryObjectPath,
                    z,
                    rotation,
                    pitch,
                    bank,
                    cancellationToken);

        if (started)
        {
            RuntimeText.Text =
                $"Posicionando cópia · {sceneryObjectPath}";

            SelectionStatusChanged?.Invoke(
                this,
                "Cópia ativa: o próximo clique define X/Y e preserva Z, rotação, pitch e bank.");
        }

        return started;
    }

    public void SetTerrainBrushPreview(
        bool enabled,
        double radiusMeters,
        double feather)
    {
        _terrainBrushPreviewEnabled =
            enabled;

        if (
            double.IsFinite(
                radiusMeters) &&
            radiusMeters >
                0)
        {
            _terrainBrushRadiusMeters =
                radiusMeters;
        }

        if (
            double.IsFinite(
                feather))
        {
            _terrainBrushFeather =
                Math.Clamp(
                    feather,
                    0,
                    1);
        }

        if (!enabled)
        {
            TerrainBrushLayer.Visibility =
                Visibility.Collapsed;

            return;
        }

        if (
            double.IsFinite(
                _lastPointerLogicalX) &&
            double.IsFinite(
                _lastPointerLogicalY))
        {
            UpdateTerrainBrushVisual(
                _lastPointerLogicalX,
                _lastPointerLogicalY);
        }
    }

    private void UpdateTerrainBrushVisual(
        double logicalX,
        double logicalY)
    {
        if (
            !_terrainBrushPreviewEnabled ||
            _runtime is null)
        {
            TerrainBrushLayer.Visibility =
                Visibility.Collapsed;

            return;
        }

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        var pixelX =
            (uint)Math.Max(
                0,
                Math.Round(
                    logicalX *
                    scaleX));

        var pixelY =
            (uint)Math.Max(
                0,
                Math.Round(
                    logicalY *
                    scaleY));

        if (
            !_runtime.TryGetTerrainBrushScreenRadius(
                pixelX,
                pixelY,
                _terrainBrushRadiusMeters,
                out var centerPixel,
                out var radiusPixels))
        {
            TerrainBrushLayer.Visibility =
                Visibility.Collapsed;

            return;
        }

        var logicalCenterX =
            centerPixel.X /
            scaleX;

        var logicalCenterY =
            centerPixel.Y /
            scaleY;

        var logicalRadius =
            radiusPixels /
            (
                (
                    scaleX +
                    scaleY
                ) *
                0.5
            );

        logicalRadius =
            Math.Clamp(
                logicalRadius,
                4.0,
                2000.0);

        var outerSize =
            logicalRadius *
            2.0;

        TerrainBrushOuterRing.Width =
            outerSize;

        TerrainBrushOuterRing.Height =
            outerSize;

        Canvas.SetLeft(
            TerrainBrushOuterRing,
            logicalCenterX -
            logicalRadius);

        Canvas.SetTop(
            TerrainBrushOuterRing,
            logicalCenterY -
            logicalRadius);

        var innerRadius =
            logicalRadius *
            (
                1.0 -
                _terrainBrushFeather
            );

        var innerSize =
            innerRadius *
            2.0;

        TerrainBrushInnerRing.Width =
            innerSize;

        TerrainBrushInnerRing.Height =
            innerSize;

        Canvas.SetLeft(
            TerrainBrushInnerRing,
            logicalCenterX -
            innerRadius);

        Canvas.SetTop(
            TerrainBrushInnerRing,
            logicalCenterY -
            innerRadius);

        TerrainBrushInnerRing.Visibility =
            _terrainBrushFeather >
                0.001
                ? Visibility.Visible
                : Visibility.Collapsed;

        TerrainBrushLabelText.Text =
            $"raio {_terrainBrushRadiusMeters:0.#} m · feather {_terrainBrushFeather:P0}";

        Canvas.SetLeft(
            TerrainBrushLabel,
            logicalCenterX +
            logicalRadius +
            8);

        Canvas.SetTop(
            TerrainBrushLabel,
            logicalCenterY -
            12);

        TerrainBrushLayer.Visibility =
            Visibility.Visible;
    }

    public void BeginTerrainPointPick()
    {
        CancelSplineJoinPick();
        _runtime?.CancelSceneryPlacement();
        _runtime?.CancelSplinePlacement();

        _terrainPointPickPersistent =
            false;

        _terrainPointPickActive =
            true;

        PointerStatusChanged?.Invoke(
            this,
            "Terreno: clique no ponto que deseja nivelar.");
    }

    public void BeginTerrainSelectionMode()
    {
        CancelSplineJoinPick();
        _runtime?.CancelSceneryPlacement();
        _runtime?.CancelSplinePlacement();

        _terrainPointPickPersistent =
            true;

        _terrainPointPickActive =
            true;

        PointerStatusChanged?.Invoke(
            this,
            "Seleção de terreno ativa: clique em qualquer tile.");
    }

    public void CancelTerrainPointPick()
    {
        _terrainPointPickActive =
            false;

        _terrainPointPickPersistent =
            false;
    }

    public void CancelSceneryPlacement()
    {
        _runtime
            ?.CancelSceneryPlacement();

        CancelSplineJoinPick();
    }

    public async Task<NativeAssetPreviewResult?>
        PreviewAssetAsync(
            string omsiRoot,
            OmsiAssetIndexEntry asset,
            CancellationToken cancellationToken =
                default)
    {
        if (_runtime is null)
        {
            return null;
        }

        var result =
            await _runtime
                .PreviewAssetAsync(
                    omsiRoot,
                    asset.Kind,
                    asset.RelativePath,
                    cancellationToken);

        if (result.IsRenderable)
        {
            RuntimeText.Text =
                $"Prévia 3D · {asset.RelativePath} · " +
                $"{result.TriangleCount} triângulos";
        }

        return result;
    }

    public bool TryFinishSceneryPlacementAtPoint(
        double x,
        double y,
        out NativeSceneryPlacementRequest?
            request)
    {
        request =
            null;

        if (
            _runtime is null ||
            !TryConvertPointToPixels(
                x,
                y,
                out var pixelX,
                out var pixelY) ||
            !_runtime.UpdateSceneryPlacement(
                pixelX,
                pixelY))
        {
            return false;
        }

        return _runtime
            .TryFinishSceneryPlacement(
                out request);
    }

    public bool TrySeedSplinePlacementAtPoint(
        double x,
        double y,
        out string status)
    {
        status =
            "Não foi possível iniciar a spline neste ponto.";

        if (
            _runtime is null ||
            !_runtime.IsSplinePlacementActive ||
            !TryConvertPointToPixels(
                x,
                y,
                out var pixelX,
                out var pixelY))
        {
            return false;
        }

        if (
            !_runtime.TryAdvanceSplinePlacement(
                pixelX,
                pixelY,
                out var request,
                out status))
        {
            return false;
        }

        return request is null;
    }

    private bool TryConvertPointToPixels(
        double x,
        double y,
        out uint pixelX,
        out uint pixelY)
    {
        pixelX = 0;
        pixelY = 0;

        if (
            !double.IsFinite(x) ||
            !double.IsFinite(y) ||
            x < 0 ||
            y < 0)
        {
            return false;
        }

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        pixelX =
            (uint)Math.Max(
                0,
                Math.Round(
                    x *
                    scaleX));

        pixelY =
            (uint)Math.Max(
                0,
                Math.Round(
                    y *
                    scaleY));

        return true;
    }

    public bool TryFinishSceneryPlacementAtWorldPoint(
        Vector3 worldPoint,
        double rotation,
        out NativeSceneryPlacementRequest?
            request)
    {
        request =
            null;

        return _runtime
            ?.TryFinishSceneryPlacementAtWorldPoint(
                worldPoint,
                rotation,
                out request) ??
            false;
    }

    public void RestoreSceneView()
    {
        _runtime?.RestoreSceneView();

        PublishSelectionInfo();
    }

    public bool FitScene()
    {
        var fitted =
            _runtime
                ?.FitScene() ??
            false;

        if (fitted)
        {
            PointerStatusChanged?.Invoke(
                this,
                "Mapa enquadrado.");
        }

        return fitted;
    }

    public IReadOnlyList<
        NativeJunctionSuggestion>
        GetJunctionSuggestions() =>
        _runtime
            ?.GetJunctionSuggestions() ??
        Array.Empty<
            NativeJunctionSuggestion>();

    public bool FocusWorldPoint(
        Vector3 point,
        float span = 45.0f)
    {
        var focused =
            _runtime
                ?.FocusWorldPoint(
                    point,
                    span) ??
            false;

        if (focused)
        {
            PointerStatusChanged?.Invoke(
                this,
                $"Ponto focado · {point.X:F1}, {point.Z:F1}.");
        }

        return focused;
    }

    public bool BeginSelectedSplineCurveEdit(
        out string status)
    {
        status =
            "Curva indisponível.";

        return _runtime
            ?.BeginSelectedSplineCurveEdit(
                out status) ??
            false;
    }

    public void CancelSelectedSplineCurveEdit() =>
        _runtime
            ?.CancelSelectedSplineCurveEdit();

    public bool BeginSelectedSplineEndpointEdit(
        out string status)
    {
        status =
            "Pontas indisponíveis.";

        return _runtime
            ?.BeginSelectedSplineEndpointEdit(
                out status) ??
            false;
    }

    public void CancelSelectedSplineEndpointEdit() =>
        _runtime
            ?.CancelSelectedSplineEndpointEdit();

    public bool BeginSplineSplitPick()
    {
        if (
            _runtime?.GetSelectionInfo()
                ?.Kind !=
            MapStudio.Renderer.Picking
                .PickingKind.Spline)
        {
            return false;
        }

        CancelSplineJoinPick();

        _splineSplitPickActive =
            true;

        PointerStatusChanged?.Invoke(
            this,
            "Dividir: clique no ponto da spline onde deseja cortar.");

        return true;
    }

    public void CancelSplineSplitPick()
    {
        _splineSplitPickActive =
            false;
    }

    public bool BeginSplineJoinPick(
        double maximumRadius,
        out string status)
    {
        status =
            string.Empty;

        var selection =
            _runtime
                ?.GetSelectionInfo();

        if (
            selection is null ||
            selection.Kind !=
                MapStudio.Renderer.Picking
                    .PickingKind.Spline)
        {
            status =
                "Unir: selecione a spline de origem.";
            return false;
        }

        if (
            selection.NextSplineId >=
                0)
        {
            status =
                $"Unir: o fim da spline #{selection.EntityId} já possui vínculo Next.";
            return false;
        }

        if (
            !double.IsFinite(
                maximumRadius) ||
            maximumRadius <=
                0)
        {
            status =
                "Unir: raio máximo inválido.";
            return false;
        }

        _runtime
            ?.CancelSelectedSplineCurveEdit();

        _runtime
            ?.CancelSelectedSplineEndpointEdit();

        _runtime
            ?.CancelSplinePlacement();

        _runtime
            ?.CancelSceneryPlacement();

        CancelSplineSplitPick();

        _splineJoinPickActive =
            true;

        _splineJoinSourceId =
            selection.EntityId;

        _splineJoinMaximumRadius =
            maximumRadius;

        status =
            $"Unir: origem #{selection.EntityId}. Clique na spline destino com início livre.";

        PointerStatusChanged?.Invoke(
            this,
            status);

        return true;
    }

    public void CancelSplineJoinPick()
    {
        _splineJoinPickActive =
            false;

        _splineJoinSourceId =
            -1;

        _splineJoinMaximumRadius =
            200.0;
    }

    public bool TryCreateParallelSplineRequest(
        double lateralOffset,
        out NativeSplinePlacementRequest? request,
        out string status)
    {
        request =
            null;

        status =
            "Paralela indisponível.";

        return _runtime
            ?.TryCreateParallelSelectionRequest(
                lateralOffset,
                out request,
                out status) ??
            false;
    }

    public bool FocusSelection()
    {
        var focused =
            _runtime
                ?.FocusSelection() ??
            false;

        if (focused)
        {
            PointerStatusChanged?.Invoke(
                this,
                "Seleção focada.");
        }

        return focused;
    }

    public bool FocusTile(
        int tileX,
        int tileY)
    {
        var focused =
            _runtime
                ?.FocusTile(
                    tileX,
                    tileY) ??
                false;

        if (focused)
        {
            PointerStatusChanged?.Invoke(
                this,
                $"Tile {tileX},{tileY} focado.");
        }

        return focused;
    }

    public void SetTopView()
    {
        _runtime?.SetTopView();

        PointerStatusChanged?.Invoke(
            this,
            "Câmera: vista superior.");
    }

    public void SetPerspectiveView()
    {
        _runtime?.SetPerspectiveView();

        PointerStatusChanged?.Invoke(
            this,
            "Câmera: perspectiva.");
    }

    public bool SetSceneVisibility(
        NativeSceneVisibility visibility)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime.SetSceneVisibility(
                visibility);

        if (changed)
        {
            PublishSelectionInfo();

            SelectionStatusChanged?.Invoke(
                this,
                $"Visibilidade · terreno {(visibility.TerrainVisible ? "on" : "off")} · " +
                $"objetos {(visibility.ObjectsVisible ? "on" : "off")} · " +
                $"splines {(visibility.SplinesVisible ? "on" : "off")}");
        }

        return changed;
    }

    public bool SetTerrainPaintVisible(
        bool visible) =>
        _runtime
            ?.SetTerrainPaintVisible(
                visible) ??
        false;

    public bool SetTerrainLayerVisible(
        int layerIndex,
        bool visible) =>
        _runtime
            ?.SetTerrainLayerVisible(
                layerIndex,
                visible) ??
        false;

    public bool ResetTerrainLayerVisibility() =>
        _runtime
            ?.ResetTerrainLayerVisibility() ??
        false;

    public bool SetSelectionFilter(
        NativeSelectionFilter filter)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime.SetSelectionFilter(
                filter);

        if (changed)
        {
            PublishSelectionInfo();

            SelectionStatusChanged?.Invoke(
                this,
                $"Filtro de seleção: {filter}.");
        }

        return changed;
    }

    public bool SetSplineProfilesVisible(
        bool visible)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime
                .SetSplineProfilesVisible(
                    visible);

        if (changed)
        {
            PointerStatusChanged?.Invoke(
                this,
                visible
                    ? "Perfis reais das splines visíveis."
                    : "Perfis reais das splines ocultos.");
        }

        return changed;
    }

    public bool TrafficPathsVisible =>
        _runtime?.TrafficPathsVisible ??
        false;

    public int TrafficPathLineCount =>
        _runtime?.TrafficPathLineCount ??
        0;

    public NativeTrafficPathDisplayOptions
        TrafficPathDisplayOptions =>
            _runtime?
                .TrafficPathDisplayOptions ??
            NativeTrafficPathDisplayOptions
                .TransportOverview;

    public NativeTrafficPathGeometry?
        TrafficPathGeometry =>
            _runtime?
                .TrafficPathGeometry;

    public bool TrafficPathSelectedOnly =>
        _runtime?
            .TrafficPathSelectedOnly ??
        false;

    public int? TrafficPathFocusedIndex =>
        _runtime?
            .TrafficPathFocusedIndex;

    public bool SetTrafficPathFocusedIndex(
        int? pathIndex)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime
                .SetTrafficPathFocusedIndex(
                    pathIndex);

        if (changed)
        {
            if (pathIndex.HasValue)
            {
                var choice =
                    GetTrafficPathChoicesForSelection()
                        .FirstOrDefault(
                            item =>
                                item.Index ==
                                pathIndex.Value);

                if (choice is not null)
                {
                    SetTrafficPathFocusPreview(
                        choice);
                }
            }
            else
            {
                ClearTrafficPathFocusPreview();
            }

            PointerStatusChanged?.Invoke(
                this,
                pathIndex.HasValue
                    ? $"Paths OMSI: faixa {pathIndex.Value} isolada no item selecionado."
                    : "Paths OMSI: todas as faixas do item selecionado disponíveis.");
        }

        return changed;
    }

    public void SetJunctionFocusPreview(
        NativeJunctionSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(
            suggestion);

        JunctionFocusPanel.Visibility =
            Visibility.Visible;

        JunctionFocusText.Text =
            $"Splines #{suggestion.SplineA} / #{suggestion.SplineB} · tile {suggestion.TileX},{suggestion.TileY}";

        JunctionFocusHintText.Text =
            $"X {suggestion.X:F1} · Y {suggestion.Y:F1} · giro {suggestion.Rotation:F1}° · usar como alvo";
    }

    public void ClearJunctionFocusPreview()
    {
        JunctionFocusPanel.Visibility =
            Visibility.Collapsed;

        JunctionFocusText.Text =
            "Splines — / —";
    }

    public void SetTrafficPathFocusPreview(
        NativeTrafficPathChoice choice)
    {
        ArgumentNullException.ThrowIfNull(
            choice);

        TrafficPathFocusPanel.Visibility =
            Visibility.Visible;

        TrafficPathFocusText.Text =
            $"Path {choice.Index} · {choice.KindLabel} {choice.DirectionLabel} · {choice.Width:F2} m";

        TrafficPathFocusHintText.Text =
            "Duplo clique no nó para editar";
    }

    public void ClearTrafficPathFocusPreview()
    {
        TrafficPathFocusPanel.Visibility =
            Visibility.Collapsed;

        TrafficPathFocusText.Text =
            "Path —";
    }

    private void SetTrafficPathFocusPreview(
        NativeTrafficPathNode node)
    {
        var choice =
            GetTrafficPathChoicesForSelection()
                .FirstOrDefault(
                    item =>
                        item.Index ==
                        node.PathIndex);

        if (choice is not null)
        {
            SetTrafficPathFocusPreview(
                choice);
        }
        else
        {
            TrafficPathFocusPanel.Visibility =
                Visibility.Visible;

            TrafficPathFocusText.Text =
                $"Path {node.PathIndex}";
        }

        TrafficPathFocusHintText.Text =
            node.IsStart
                ? "Nó inicial · duplo clique para editar"
                : "Nó final · duplo clique para editar";
    }

    public bool SetTrafficPathSelectedOnly(
        bool selectedOnly)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime
                .SetTrafficPathSelectedOnly(
                    selectedOnly);

        if (changed)
        {
            PointerStatusChanged?.Invoke(
                this,
                selectedOnly
                    ? "Paths OMSI: mostrando apenas o item selecionado."
                    : "Paths OMSI: mostrando todos os itens carregados.");
        }

        return changed;
    }

    public void RefreshTrafficPathDisplay() =>
        _runtime?
            .RefreshTrafficPathDisplay();

    public IReadOnlyList<
        NativeTrafficPathChoice>
        GetTrafficPathChoicesForSelection() =>
            _runtime?
                .GetTrafficPathChoicesForSelection() ??
            Array.Empty<
                NativeTrafficPathChoice>();

    public bool SetTrafficPathDisplayOptions(
        NativeTrafficPathDisplayOptions
            options)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime
                .SetTrafficPathDisplayOptions(
                    options);

        if (changed)
        {
            var geometry =
                _runtime
                    .TrafficPathGeometry;

            PointerStatusChanged?.Invoke(
                this,
                geometry is null
                    ? "Paths OMSI: nenhum caminho disponível."
                    : $"Paths OMSI: {geometry.PathCount} caminho(s) filtrado(s) · {geometry.LineCount} linha(s).");
        }

        return changed;
    }

    public NativeAssetTechnicalSnapshot
        GetAssetTechnicalSnapshot() =>
        _runtime
            ?.GetAssetTechnicalSnapshot() ??
        NativeAssetTechnicalSnapshot.Empty;

    public bool SetTrafficPathsVisible(
        bool visible)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime
                .SetTrafficPathsVisible(
                    visible);

        if (changed)
        {
            if (!visible)
            {
                ClearTrafficPathFocusPreview();
            }

            PointerStatusChanged?.Invoke(
                this,
                visible
                    ? $"Paths OMSI visíveis · {_runtime.TrafficPathLineCount} linhas."
                    : "Paths OMSI ocultos.");
        }

        return changed;
    }

    public bool SetGridVisible(
        bool visible)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime.SetGridVisible(
                visible);

        if (changed)
        {
            PointerStatusChanged?.Invoke(
                this,
                visible
                    ? "Grade do mapa visível."
                    : "Grade do mapa oculta.");
        }

        return changed;
    }

    public void SetGizmoMode(
        NativeGizmoMode mode)
    {
        _runtime?.SetGizmoMode(
            mode);

        PointerStatusChanged?.Invoke(
            this,
            mode ==
                NativeGizmoMode.Move
                ? "Gizmo: mover"
                : "Gizmo: rotacionar");
    }

    public bool SetNightPreview(
        bool enabled)
    {
        if (_runtime is null)
        {
            return false;
        }

        _runtime.SetNightPreview(
            enabled);

        return _runtime.HasSkyTexture;
    }

    public void SetReferenceOverlay(
        NativeReferenceOverlayDefinition?
            overlay)
    {
        SetReferenceOverlays(
            overlay is null
                ? null
                : [
                    overlay
                ]);
    }

    public void SetReferenceOverlays(
        IReadOnlyList<
            NativeReferenceOverlayDefinition>?
            overlays)
    {
        _runtime?.SetReferenceOverlays(
            overlays);

        var first =
            overlays?
                .FirstOrDefault();

        PointerStatusChanged?.Invoke(
            this,
            first is null
                ? "Referência geográfica removida."
                : $"Referência geográfica ativa · {first.Attribution} · {overlays!.Count} textura(s) · opacidade {first.Opacity:P0}.");
    }

    public async Task SetMapSnapshotAsync(
        NativeMapSnapshot snapshot,
        string omsiRoot,
        CancellationToken cancellationToken =
            default,
        [System.Runtime.CompilerServices.CallerMemberName]
        string caller =
            "")
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        var requestId =
            System.Threading.Interlocked
                .Increment(
                    ref _mapSnapshotReloadSequence);

        using var linkedCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        lock (_mapSnapshotReloadSync)
        {
            _mapSnapshotReloadCancellation
                ?.Cancel();

            _mapSnapshotReloadCancellation =
                linkedCancellation;
        }

        MapStudio.Native.NativeStartupDiagnostics.Write(
            $"SetMapSnapshot request id={requestId} caller={caller} tiles={snapshot.Tiles.Count} objects={snapshot.ObjectCount} splines={snapshot.SplineCount}");

        var gateAcquired =
            false;

        var reloadStopwatch =
            System.Diagnostics.Stopwatch
                .StartNew();

        try
        {
            await _mapSnapshotReloadGate
                .WaitAsync(
                    linkedCancellation.Token);

            gateAcquired =
                true;

            var latestRequestId =
                System.Threading.Volatile
                    .Read(
                        ref _mapSnapshotReloadSequence);

            if (requestId != latestRequestId)
            {
                MapStudio.Native.NativeStartupDiagnostics.Write(
                    $"SetMapSnapshot coalesced-before-load id={requestId} latest={latestRequestId} caller={caller}");

                return;
            }

            var runtime =
                _runtime;

            if (runtime is null)
            {
                SelectionStatusChanged?.Invoke(
                    this,
                    "Viewport Direct3D ainda não inicializado.");

                MapStudio.Native.NativeStartupDiagnostics.Write(
                    $"SetMapSnapshot skipped-no-runtime id={requestId} caller={caller}");

                return;
            }

            SelectionStatusChanged?.Invoke(
                this,
                "Carregando SCO/O3D reais no renderer nativo...");

            MapStudio.Native.NativeStartupDiagnostics.Write(
                $"SetMapSnapshot begin id={requestId} caller={caller} thread={Environment.CurrentManagedThreadId}");

            var scene =
                await runtime
                    .LoadSceneAsync(
                        snapshot.Tiles
                            .Select(
                                tile =>
                                    new NativeSceneTile(
                                        tile.Reference,
                                        tile.Content))
                            .ToArray(),
                        snapshot.Map,
                        omsiRoot,
                        linkedCancellation.Token);

            latestRequestId =
                System.Threading.Volatile
                    .Read(
                        ref _mapSnapshotReloadSequence);

            if (requestId != latestRequestId)
            {
                MapStudio.Native.NativeStartupDiagnostics.Write(
                    $"SetMapSnapshot superseded-after-load id={requestId} latest={latestRequestId} caller={caller} elapsedMs={reloadStopwatch.ElapsedMilliseconds}");

                return;
            }

            runtime.RenderInitialFrame();

            RuntimeText.Text =
                $"{snapshot.Map.DisplayName} · {scene.Tiles.Count} tiles · " +
                $"{scene.Objects.Count} objetos · {scene.Splines.Count} splines · " +
                $"{runtime.MapRenderer.TerrainTriangleVertexCount / 3} triângulos terreno · " +
                $"{runtime.MapRenderer.SplineTriangleVertexCount / 3} triângulos spline · " +
                $"{runtime.MapRenderer.ObjectTriangleVertexCount / 3} triângulos O3D";

            SelectionStatusChanged?.Invoke(
                this,
                $"Assets nativos: {runtime.LoadedSceneryAssetCount} SCO · " +
                $"{runtime.LoadedObjectMeshCount} meshes · " +
                $"{runtime.MapRenderer.LoadedTextureCount} texturas O3D · " +
                $"{runtime.LoadedSplineAssetCount} SLI · " +
                $"{runtime.LoadedSplineSurfaceCount} superfícies spline · " +
                $"{runtime.TrafficPathLineCount} linhas de path · " +
                $"{runtime.SceneryLightPointCount} pontos de luz · " +
                $"{runtime.TrafficLightProgramCount} programas de semáforo · " +
                $"{scene.SelectableCount} IDs de seleção.");

            MapStudio.Native.NativeStartupDiagnostics.Write(
                $"SetMapSnapshot complete id={requestId} caller={caller} elapsedMs={reloadStopwatch.ElapsedMilliseconds} loadedScenery={runtime.LoadedSceneryAssetCount} loadedSplines={runtime.LoadedSplineAssetCount} splineTriangles={runtime.MapRenderer.SplineTriangleVertexCount / 3} objectTriangles={runtime.MapRenderer.ObjectTriangleVertexCount / 3}");
        }
        catch (OperationCanceledException)
            when (
                !cancellationToken
                    .IsCancellationRequested &&
                (
                    requestId !=
                        System.Threading.Volatile
                            .Read(
                                ref _mapSnapshotReloadSequence) ||
                    !IsLoaded
                ))
        {
            MapStudio.Native.NativeStartupDiagnostics.Write(
                $"SetMapSnapshot superseded-cancelled id={requestId} caller={caller} elapsedMs={reloadStopwatch.ElapsedMilliseconds}");
        }
        finally
        {
            if (gateAcquired)
            {
                _mapSnapshotReloadGate
                    .Release();
            }

            lock (_mapSnapshotReloadSync)
            {
                if (
                    ReferenceEquals(
                        _mapSnapshotReloadCancellation,
                        linkedCancellation))
                {
                    _mapSnapshotReloadCancellation =
                        null;
                }
            }

            MapStudio.Native.NativeStartupDiagnostics.Write(
                $"SetMapSnapshot end id={requestId} caller={caller} elapsedMs={reloadStopwatch.ElapsedMilliseconds}");
        }
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            _runtime ??=
                new NativeViewportRuntime();

            _runtime.DiagnosticSink =
                message =>
                    MapStudio.Native
                        .NativeStartupDiagnostics
                        .Write(
                            message);

            EnsureNativeSurface();
            BindSwapChain();
            _runtime.RenderInitialFrame();

            RuntimeText.Text =
                $"Direct3D 11 ativo · feature level {_runtime.Device.FeatureLevel} · " +
                $"{_runtime.Surface?.Width}×{_runtime.Surface?.Height}";
        }
        catch (Exception exception)
        {
            RuntimeText.Text =
                $"Falha ao iniciar Direct3D 11: {exception.Message}";
        }
    }

    private void EnsureNativeSurface()
    {
        if (_runtime is null)
        {
            return;
        }

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        var width =
            (uint)Math.Max(
                1,
                Math.Round(
                    SwapChainSurface
                        .ActualWidth *
                    scaleX));

        var height =
            (uint)Math.Max(
                1,
                Math.Round(
                    SwapChainSurface
                        .ActualHeight *
                    scaleY));

        _runtime.EnsureSurface(
            width,
            height);
    }

    private void BindSwapChain()
    {
        if (
            _runtime is null ||
            _runtime.SwapChainPointer ==
                IntPtr.Zero)
        {
            return;
        }

        var nativePanel =
            WinRT.CastExtensions.As<
                WinUISwapChainPanelInterop
                    .ISwapChainPanelNative>(
                SwapChainSurface);

        var result =
            nativePanel.SetSwapChain(
                _runtime
                    .SwapChainPointer);

        Marshal.ThrowExceptionForHR(
            result);

        _swapChainBound = true;
    }

    private void UnbindSwapChain()
    {
        if (!_swapChainBound)
        {
            return;
        }

        var nativePanel =
            WinRT.CastExtensions.As<
                WinUISwapChainPanelInterop
                    .ISwapChainPanelNative>(
                SwapChainSurface);

        var result =
            nativePanel.SetSwapChain(
                IntPtr.Zero);

        Marshal.ThrowExceptionForHR(
            result);

        _swapChainBound = false;
    }

    private void ResizeAndRender()
    {
        if (
            _runtime is null ||
            !IsLoaded)
        {
            return;
        }

        EnsureNativeSurface();
        _runtime.RenderInitialFrame();

        RuntimeText.Text =
            $"Direct3D 11 ativo · feature level {_runtime.Device.FeatureLevel} · " +
            $"{_runtime.Surface?.Width}×{_runtime.Surface?.Height}";
    }

    private void OnSizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        try
        {
            ResizeAndRender();
        }
        catch (Exception exception)
        {
            RuntimeText.Text =
                $"Falha ao redimensionar viewport: {exception.Message}";
        }
    }

    private void OnCompositionScaleChanged(
        SwapChainPanel sender,
        object args)
    {
        try
        {
            ResizeAndRender();
        }
        catch (Exception exception)
        {
            RuntimeText.Text =
                $"Falha ao aplicar escala de DPI: {exception.Message}";
        }
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        StopNavigationRendering();

        lock (_mapSnapshotReloadSync)
        {
            _mapSnapshotReloadCancellation
                ?.Cancel();
        }

        try
        {
            UnbindSwapChain();
        }
        finally
        {
            _runtime?.Dispose();
            _runtime = null;
        }
    }

    private void OnDoubleTapped(
        object sender,
        DoubleTappedRoutedEventArgs e)
    {
        if (
            _runtime is null ||
            !_runtime.TrafficPathsVisible)
        {
            return;
        }

        var point =
            e.GetPosition(
                InputSurface);

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        var pixelX =
            (uint)Math.Max(
                0,
                Math.Round(
                    point.X *
                    scaleX));

        var pixelY =
            (uint)Math.Max(
                0,
                Math.Round(
                    point.Y *
                    scaleY));

        if (
            !_runtime.TryFocusTrafficPathNode(
                pixelX,
                pixelY,
                out var pathNode) ||
            pathNode is null)
        {
            return;
        }

        PublishSelectionInfo();

        SetTrafficPathFocusPreview(
            pathNode);

        TrafficPathNodeFocused
            ?.Invoke(
                pathNode);

        TrafficPathNodeEditRequested
            ?.Invoke(
                pathNode);

        SelectionStatusChanged?.Invoke(
            this,
            $"Path {pathNode.PathIndex}: edição solicitada pelo nó no viewport.");

        PointerStatusChanged?.Invoke(
            this,
            $"Path {pathNode.PathIndex}: abrindo editor real [path]...");

        e.Handled =
            true;
    }

    private void OnPointerPressed(
        object sender,
        PointerRoutedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(
                InputSurface);

        var panPressed =
            point.Properties
                .IsMiddleButtonPressed;

        var orbitPressed =
            point.Properties
                .IsRightButtonPressed;

        if (panPressed)
        {
            HideSelectionRadialMenu();

            var panScaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var panScaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var panPixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        panScaleX));

            var panPixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        panScaleY));

            _isPanning =
                _runtime
                    ?.BeginPointerPan(
                        panPixelX,
                        panPixelY) ??
                true;

            _lastPanX =
                point.Position.X;
            _lastPanY =
                point.Position.Y;

            _runtime?.ClearHover();

            ProtectedCursor =
                _panCursor;

            InputSurface.CapturePointer(
                e.Pointer);

            PointerStatusChanged?.Invoke(
                this,
                "Pan: agarre o mapa e puxe na direção desejada.");

            e.Handled = true;
            return;
        }

        if (orbitPressed)
        {
            HideSelectionRadialMenu();

            _rightPressed =
                true;

            _rightPressX =
                point.Position.X;
            _rightPressY =
                point.Position.Y;

            _lastPanX =
                point.Position.X;
            _lastPanY =
                point.Position.Y;

            _runtime?.ClearHover();

            InputSurface.CapturePointer(
                e.Pointer);

            PointerStatusChanged?.Invoke(
                this,
                "Botão direito: clique abre ações · arraste orbita");

            e.Handled = true;
            return;
        }

        _leftPressed =
            point.Properties
                .IsLeftButtonPressed;

        if (_leftPressed)
        {
            HideSelectionRadialMenu();
        }

        if (!_leftPressed)
        {
            return;
        }

        InputSurface.CapturePointer(
            e.Pointer);

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        var pixelX =
            (uint)Math.Max(
                0,
                Math.Round(
                    point.Position.X *
                    scaleX));

        var pixelY =
            (uint)Math.Max(
                0,
                Math.Round(
                    point.Position.Y *
                    scaleY));

        if (
            _runtime is not null &&
            _runtime
                .IsSelectedSplineEndpointEditActive)
        {
            var started =
                _runtime
                    .TryBeginSelectedSplineEndpointDrag(
                        pixelX,
                        pixelY,
                        out var endpointStatus);

            PointerStatusChanged?.Invoke(
                this,
                endpointStatus);

            e.Handled =
                true;

            if (started)
            {
                SelectionStatusChanged?.Invoke(
                    this,
                    endpointStatus);
            }

            return;
        }

        if (
            _runtime is not null &&
            _runtime
                .IsSelectedSplineCurveEditActive)
        {
            var started =
                _runtime
                    .TryBeginSelectedSplineCurveDrag(
                        pixelX,
                        pixelY,
                        out var curveStatus);

            PointerStatusChanged?.Invoke(
                this,
                curveStatus);

            if (started)
            {
                SelectionStatusChanged?.Invoke(
                    this,
                    curveStatus);
            }

            e.Handled =
                true;

            return;
        }

        if (
            _splineJoinPickActive &&
            _runtime is not null)
        {
            if (
                !_runtime.TryPick(
                    pixelX,
                    pixelY,
                    out _,
                    out var joinTarget,
                    cycleCandidates:
                        false) ||
                joinTarget is not
                    OmsiPlacedSpline targetSpline)
            {
                PointerStatusChanged?.Invoke(
                    this,
                    "Unir: clique em uma spline destino.");

                e.Handled =
                    true;

                return;
            }

            if (
                targetSpline.SplineId ==
                    _splineJoinSourceId)
            {
                PointerStatusChanged?.Invoke(
                    this,
                    "Unir: escolha outra spline como destino.");

                e.Handled =
                    true;

                return;
            }

            if (
                !_runtime
                    .TryBuildSplineCompleteToRequest(
                        _splineJoinSourceId,
                        targetSpline.SplineId,
                        _splineJoinMaximumRadius,
                        out var joinRequest,
                        out var joinStatus) ||
                joinRequest is null)
            {
                PointerStatusChanged?.Invoke(
                    this,
                    joinStatus +
                    " Escolha outra spline ou use Complete to avançado.");

                SelectionStatusChanged?.Invoke(
                    this,
                    joinStatus);

                e.Handled =
                    true;

                return;
            }

            var sourceId =
                _splineJoinSourceId;

            CancelSplineJoinPick();

            SplinePlacementRequested
                ?.Invoke(
                    joinRequest);

            SelectionStatusChanged?.Invoke(
                this,
                $"Unir: #{sourceId} → nova ligação → #{targetSpline.SplineId}.");

            PointerStatusChanged?.Invoke(
                this,
                joinStatus +
                " Inserindo ligação...");

            e.Handled =
                true;

            return;
        }

        if (
            _splineSplitPickActive &&
            _runtime is not null)
        {
            _splineSplitPickActive =
                false;

            if (
                _runtime
                    .TryCreateSelectedSplineSplitRequest(
                        pixelX,
                        pixelY,
                        out var splitRequest,
                        out var splitStatus) &&
                splitRequest is not null)
            {
                SplineSplitRequested
                    ?.Invoke(
                        splitRequest);
            }

            PointerStatusChanged?.Invoke(
                this,
                splitStatus);

            e.Handled =
                true;

            return;
        }

        if (
            _terrainPointPickActive &&
            _runtime is not null)
        {
            if (
                !_terrainPointPickPersistent)
            {
                _terrainPointPickActive =
                    false;
            }

            if (
                _runtime.TryGetTerrainEditPoint(
                    pixelX,
                    pixelY,
                    out var terrainPoint) &&
                terrainPoint is not null)
            {
                TerrainPointSelected
                    ?.Invoke(
                        terrainPoint);

                PointerStatusChanged?.Invoke(
                    this,
                    $"Terreno selecionado em tile {terrainPoint.Tile.X},{terrainPoint.Tile.Y} · " +
                    $"altura {terrainPoint.Height:F2} m.");
            }
            else
            {
                PointerStatusChanged?.Invoke(
                    this,
                    "Não foi possível selecionar terreno neste ponto.");
            }

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.IsSplinePlacementActive)
        {
            var dragCreation =
                _runtime.SplinePlacementStage ==
                    NativeSplinePlacementStage
                        .AwaitingStart &&
                !_runtime.SplinePlacementCurved;

            if (
                _runtime.TryAdvanceSplinePlacement(
                    pixelX,
                    pixelY,
                    out var splinePlacement,
                    out var splineStatus))
            {
                if (dragCreation)
                {
                    _isSplineDragCreating =
                        true;

                    PointerStatusChanged?.Invoke(
                        this,
                        "Rua: arraste até o ponto final e solte para criar.");
                }
                else
                {
                    if (
                        splinePlacement is not null)
                    {
                        SplinePlacementRequested
                            ?.Invoke(
                                splinePlacement);
                    }

                    PointerStatusChanged?.Invoke(
                        this,
                        splineStatus);
                }

                SplinePlacementControlStateChanged
                    ?.Invoke(
                        _runtime
                            .GetSplinePlacementControlState());
            }

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.IsSceneryPlacementActive)
        {
            _runtime.UpdateSceneryPlacement(
                pixelX,
                pixelY);

            if (
                _runtime.TryFinishSceneryPlacement(
                    out var placement) &&
                placement is not null)
            {
                SceneryPlacementRequested
                    ?.Invoke(
                        placement);

                PointerStatusChanged?.Invoke(
                    this,
                    $"Posicionamento solicitado em {placement.WorldPoint.X:F2}, " +
                    $"{placement.WorldPoint.Y:F2}, {placement.WorldPoint.Z:F2}.");
            }

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.TryFocusTrafficPathNode(
                pixelX,
                pixelY,
                out var pathNode) &&
            pathNode is not null)
        {
            PublishSelectionInfo();

            SetTrafficPathFocusPreview(
                pathNode);

            TrafficPathNodeFocused
                ?.Invoke(
                    pathNode);

            var kindLabel =
                pathNode.Type switch
                {
                    1 => "HUM",
                    2 => "RAIL",
                    3 => "AIR",
                    _ => "CAR"
                };

            var endLabel =
                pathNode.IsStart
                    ? "início"
                    : "fim";

            SelectionStatusChanged?.Invoke(
                this,
                $"Path {pathNode.PathIndex} · {kindLabel} · nó de {endLabel}");

            PointerStatusChanged?.Invoke(
                this,
                $"Path {pathNode.PathIndex} focado pelo nó de {endLabel}.");

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.TryBeginGizmoDrag(
                pixelX,
                pixelY,
                out var gizmoHandle))
        {
            _isManipulatingGizmo =
                true;

            InputSurface.CapturePointer(
                e.Pointer);

            PointerStatusChanged?.Invoke(
                this,
                $"Gizmo ativo: {gizmoHandle}");

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.TryBeginDirectMove(
                pixelX,
                pixelY,
                out var directMoveHandle))
        {
            _isManipulatingGizmo =
                true;

            InputSurface.CapturePointer(
                e.Pointer);

            PointerStatusChanged?.Invoke(
                this,
                "Mover: arraste o próprio item selecionado · solte para aplicar.");

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.TryBeginDirectRotation(
                pixelX,
                pixelY,
                out var directRotationHandle))
        {
            _isManipulatingGizmo =
                true;

            InputSurface.CapturePointer(
                e.Pointer);

            PointerStatusChanged?.Invoke(
                this,
                "Girar: arraste o próprio item para rotacionar · solte para aplicar.");

            e.Handled = true;
            return;
        }

        var message =
            $"Clique nativo: X={point.Position.X:F1} Y={point.Position.Y:F1} · " +
            $"pixel {pixelX},{pixelY}";

        PointerStatusChanged?.Invoke(
            this,
            message);

        var additiveSelection =
            (
                e.KeyModifiers &
                (
                    VirtualKeyModifiers.Control |
                    VirtualKeyModifiers.Shift
                )
            ) !=
            0;

        var pickingId =
            PickingId.None;

        object? selected =
            null;

        var picked =
            _runtime is not null &&
            _runtime.TryPick(
                pixelX,
                pixelY,
                out pickingId,
                out selected,
                additiveSelection:
                    additiveSelection);

        if (picked)
        {
            _selectionBoxPending =
                false;

            var selectionMessage =
                selected switch
                {
                    OmsiPlacedObject item =>
                        $"Objeto #{item.ObjectId} · {item.SceneryObjectPath} · ID {pickingId.Value}",
                    OmsiPlacedSpline item =>
                        $"Spline #{item.SplineId} · {item.SplinePath} · ID {pickingId.Value}",
                    _ =>
                        $"{pickingId.Kind} · ID {pickingId.Value}"
                };

            if (
                _runtime!
                    .LastPickCandidateCount >
                1)
            {
                selectionMessage +=
                    $" · {_runtime.LastPickCandidatePosition}/{_runtime.LastPickCandidateCount} sobrepostos · clique novamente para alternar";
            }

            if (
                _runtime
                    .SelectedItemCount >
                1)
            {
                selectionMessage +=
                    $" · {_runtime.SelectedItemCount} itens selecionados · Ctrl+clique adiciona/remove · Shift+clique também";
            }

            SelectionStatusChanged?.Invoke(
                this,
                selectionMessage);
        }
        else
        {
            _selectionBoxPending =
                _runtime is not
                    null;

            _isSelectionBoxDragging =
                false;

            _selectionBoxAdditive =
                additiveSelection;

            _selectionBoxStartX =
                point.Position.X;

            _selectionBoxStartY =
                point.Position.Y;

            SelectionStatusChanged?.Invoke(
                this,
                additiveSelection
                    ? "Arraste para adicionar itens com a caixa de seleção."
                    : "Arraste em área vazia para selecionar vários itens.");
        }

        PublishSelectionInfo();

        e.Handled = true;
    }

    private void OnPointerMoved(
        object sender,
        PointerRoutedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(
                InputSurface);

        _lastPointerLogicalX =
            point.Position.X;

        _lastPointerLogicalY =
            point.Position.Y;

        UpdateTerrainBrushVisual(
            point.Position.X,
            point.Position.Y);

        if (
            !_isPanning &&
            !_isOrbiting)
        {
            PointerText.Text =
                $"x: {point.Position.X:F0} · y: {point.Position.Y:F0}";
        }

        if (
            _selectionBoxPending &&
            _leftPressed)
        {
            var deltaX =
                point.Position.X -
                _selectionBoxStartX;

            var deltaY =
                point.Position.Y -
                _selectionBoxStartY;

            if (
                !_isSelectionBoxDragging &&
                (
                    Math.Abs(
                        deltaX) >=
                        6 ||
                    Math.Abs(
                        deltaY) >=
                        6
                ))
            {
                _isSelectionBoxDragging =
                    true;

                SelectionBoxLayer.Visibility =
                    Visibility.Visible;
            }

            if (_isSelectionBoxDragging)
            {
                UpdateSelectionBoxVisual(
                    point.Position.X,
                    point.Position.Y);

                PointerStatusChanged?.Invoke(
                    this,
                    _selectionBoxAdditive
                        ? "Caixa de seleção: adicionando ao grupo atual."
                        : "Caixa de seleção: solte para selecionar o grupo.");

                e.Handled =
                    true;

                return;
            }
        }

        if (
            _runtime is not null &&
            _runtime
                .IsSelectedSplineEndpointDragging &&
            _leftPressed)
        {
            var endpointScaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var endpointScaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var endpointPixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        endpointScaleX));

            var endpointPixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        endpointScaleY));

            _runtime
                .UpdateSelectedSplineEndpointDrag(
                    endpointPixelX,
                    endpointPixelY,
                    out var endpointStatus);

            PointerStatusChanged?.Invoke(
                this,
                endpointStatus);

            e.Handled =
                true;

            return;
        }

        if (
            _runtime is not null &&
            _runtime
                .IsSelectedSplineCurveDragging &&
            _leftPressed)
        {
            var curveScaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var curveScaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var curvePixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        curveScaleX));

            var curvePixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        curveScaleY));

            if (
                _runtime
                    .UpdateSelectedSplineCurveEdit(
                        curvePixelX,
                        curvePixelY))
            {
                PointerStatusChanged?.Invoke(
                    this,
                    _runtime
                        .GetSelectedSplineCurveEditStatus());
            }

            e.Handled =
                true;

            return;
        }

        if (
            _isSplineDragCreating &&
            _runtime is not null)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            NativeSplinePlacementRequest?
                request =
                    null;

            var completed =
                _runtime.TryAdvanceSplinePlacement(
                    pixelX,
                    pixelY,
                    out request,
                    out var status);

            if (
                completed &&
                request is null &&
                _runtime.SplineEasyRoadEnabled &&
                _runtime.SplinePlacementStage ==
                    NativeSplinePlacementStage
                        .AwaitingEasyRoadConfirm)
            {
                completed =
                    _runtime.TryConfirmEasyRoad(
                        out request,
                        out status);
            }

            if (
                completed &&
                request is not null)
            {
                SplinePlacementRequested
                    ?.Invoke(
                        request);
            }

            PointerStatusChanged?.Invoke(
                this,
                completed
                    ? status
                    : "Rua: não foi possível concluir neste ponto.");

            SplinePlacementControlStateChanged
                ?.Invoke(
                    _runtime
                        .GetSplinePlacementControlState());

            _isSplineDragCreating =
                false;
        }

        if (
            _isManipulatingGizmo &&
            _runtime is not null)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            _runtime.UpdateGizmoDrag(
                pixelX,
                pixelY);

            PointerStatusChanged?.Invoke(
                this,
                _runtime.GizmoMode ==
                    NativeGizmoMode.Move
                    ? "Movendo seleção..."
                    : "Rotacionando seleção...");

            e.Handled = true;
            return;
        }

        if (
            _rightPressed &&
            !_isOrbiting)
        {
            var rightDeltaX =
                point.Position.X -
                _rightPressX;

            var rightDeltaY =
                point.Position.Y -
                _rightPressY;

            if (
                Math.Sqrt(
                    rightDeltaX *
                    rightDeltaX +
                    rightDeltaY *
                    rightDeltaY) >=
                6.0)
            {
                _isOrbiting =
                    true;

                _lastPanX =
                    point.Position.X;

                _lastPanY =
                    point.Position.Y;

                ProtectedCursor =
                    _orbitCursor;

                PointerStatusChanged?.Invoke(
                    this,
                    "Órbita 3D nativa ativa");
            }
            else
            {
                e.Handled =
                    true;
                return;
            }
        }

        if (
            (
                _isPanning ||
                _isOrbiting
            ) &&
            _runtime is not null)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            var deltaX =
                (
                    point.Position.X -
                    _lastPanX
                ) *
                scaleX;

            var deltaY =
                (
                    point.Position.Y -
                    _lastPanY
                ) *
                scaleY;

            _lastPanX =
                point.Position.X;

            _lastPanY =
                point.Position.Y;

            if (_isOrbiting)
            {
                QueueOrbitFrame(
                    deltaX,
                    deltaY);
            }
            else
            {
                QueuePanFrame(
                    pixelX,
                    pixelY);
            }

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.IsSplinePlacementActive)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            if (
                _runtime.UpdateSplinePlacement(
                    pixelX,
                    pixelY))
            {
                var controlState =
                    _runtime
                        .GetSplinePlacementControlState();

                if (
                    controlState is not null &&
                    controlState.Length >
                        0.01)
                {
                    var radiusText =
                        Math.Abs(
                            controlState.Radius) >
                        0.01
                            ? $" · R {Math.Abs(controlState.Radius):F1} m"
                            : string.Empty;

                    var modeText =
                        controlState.ElevationMode switch
                        {
                            NativeRoadElevationMode.Elevate =>
                                "elevar",
                            NativeRoadElevationMode.Level =>
                                "nivelar",
                            NativeRoadElevationMode.Lower =>
                                "baixar",
                            _ =>
                                "terreno"
                        };

                    PointerStatusChanged?.Invoke(
                        this,
                        $"{controlState.Length:F1} m · " +
                        $"{controlState.StartElevation:+0.0;-0.0;0.0} → " +
                        $"{controlState.EndElevation:+0.0;-0.0;0.0} m · " +
                        $"{controlState.Gradient:+0.0;-0.0;0.0}%{radiusText} · " +
                        modeText);
                }
                else
                {
                    PointerStatusChanged?.Invoke(
                        this,
                        _isSplineDragCreating
                            ? "Rua: arrastando prévia · solte para criar."
                            : _runtime.SplinePlacementStage switch
                            {
                                NativeSplinePlacementStage.AwaitingStart =>
                                    "Rua: clique e arraste a partir do ponto inicial.",
                                NativeSplinePlacementStage.AwaitingEnd =>
                                    "Rua: mova até o ponto final.",
                                NativeSplinePlacementStage.AwaitingCurve =>
                                    "Curva: mova o cursor para definir a curvatura e clique.",
                                NativeSplinePlacementStage.AwaitingEasyRoadCurveControl =>
                                    "Curva visual: mova lateralmente e clique para fixar.",
                                NativeSplinePlacementStage.AwaitingEasyRoadConfirm =>
                                    "Rua pronta para confirmar.",
                                _ =>
                                    "Construindo rua..."
                            });
                }

                SplinePlacementControlStateChanged
                    ?.Invoke(
                        controlState);
            }

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.IsSceneryPlacementActive)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            if (
                _runtime.UpdateSceneryPlacement(
                    pixelX,
                    pixelY))
            {
                PointerStatusChanged?.Invoke(
                    this,
                    "Posicionamento: clique para inserir · botão do meio pan · botão direito órbita");
            }

            e.Handled = true;
            return;
        }

        if (_leftPressed)
        {
            PointerStatusChanged?.Invoke(
                this,
                $"Pointer capturado: X={point.Position.X:F1} Y={point.Position.Y:F1}");

            return;
        }

        UpdateHover(
            point.Position.X,
            point.Position.Y);
    }

    private void UpdateHover(
        double x,
        double y)
    {
        if (_runtime is null)
        {
            return;
        }

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        var pixelX =
            (uint)Math.Max(
                0,
                Math.Round(
                    x *
                    scaleX));

        var pixelY =
            (uint)Math.Max(
                0,
                Math.Round(
                    y *
                    scaleY));

        var now =
            Environment
                .TickCount64;

        if (
            pixelX ==
                _lastHoverPixelX &&
            pixelY ==
                _lastHoverPixelY)
        {
            return;
        }

        if (
            now -
                _lastHoverTick <
            33)
        {
            return;
        }

        _lastHoverTick =
            now;

        _lastHoverPixelX =
            pixelX;

        _lastHoverPixelY =
            pixelY;

        _runtime.UpdateHover(
            pixelX,
            pixelY);
    }

    private void OnPointerReleased(
        object sender,
        PointerRoutedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(
                InputSurface);

        if (
            _rightPressed &&
            !_isOrbiting)
        {
            TryOpenSelectionRadialMenu(
                point.Position.X,
                point.Position.Y);
        }

        var selectionRuntime =
            _runtime;

        if (
            selectionRuntime is not null &&
            selectionRuntime
                .IsSelectedSplineEndpointDragging)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            if (
                selectionRuntime
                    .TryFinishSelectedSplineEndpointDrag(
                        pixelX,
                        pixelY,
                        out var endpointEdit,
                        out var endpointStatus) &&
                endpointEdit is not null)
            {
                TransformEditPending
                    ?.Invoke(
                        endpointEdit);

                PublishSelectionInfo();

                SelectionStatusChanged?.Invoke(
                    this,
                    endpointStatus);
            }

            PointerStatusChanged?.Invoke(
                this,
                endpointStatus);
        }

        if (
            selectionRuntime is not null &&
            selectionRuntime
                .IsSelectedSplineCurveDragging)
        {
            var curveScaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var curveScaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var curvePixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        curveScaleX));

            var curvePixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        curveScaleY));

            if (
                selectionRuntime
                    .TryFinishSelectedSplineCurveEdit(
                        curvePixelX,
                        curvePixelY,
                        out var curveEdit,
                        out var curveStatus) &&
                curveEdit is not null)
            {
                TransformEditPending
                    ?.Invoke(
                        curveEdit);

                PublishSelectionInfo();

                SelectionStatusChanged?.Invoke(
                    this,
                    curveStatus);
            }

            PointerStatusChanged?.Invoke(
                this,
                curveStatus);
        }

        if (
            _isSelectionBoxDragging &&
            selectionRuntime is not null)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var startPixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        _selectionBoxStartX *
                        scaleX));

            var startPixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        _selectionBoxStartY *
                        scaleY));

            var endPixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var endPixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            var selectedCount =
                selectionRuntime
                    .SelectInRectangle(
                        startPixelX,
                        startPixelY,
                        endPixelX,
                        endPixelY,
                        _selectionBoxAdditive);

            PublishSelectionInfo();

            SelectionStatusChanged?.Invoke(
                this,
                selectedCount ==
                    0
                    ? "Caixa de seleção sem itens."
                    : $"{selectedCount} item(ns) selecionado(s) pela caixa.");

            PointerStatusChanged?.Invoke(
                this,
                selectedCount ==
                    0
                    ? "Seleção por caixa concluída sem itens."
                    : $"Seleção por caixa: {selectedCount} item(ns).");
        }

        ResetSelectionBox();

        if (
            _isManipulatingGizmo &&
            _runtime is not null)
        {
            var edit =
                _runtime.EndGizmoDrag();

            if (edit is not null)
            {
                foreach (
                    var pendingEdit in
                        _runtime.LastTransformEdits)
                {
                    TransformEditPending
                        ?.Invoke(
                            pendingEdit);
                }

                PublishSelectionInfo();

                SelectionStatusChanged?.Invoke(
                    this,
                    _runtime.LastTransformEdits.Count >
                        1
                        ? $"{_runtime.LastTransformEdits.Count} itens transformados em grupo · alterações pendentes de salvamento."
                        : edit.IsObject
                            ? "Transformação de objeto OMSI pendente de salvamento."
                            : "Transformação de spline OMSI pendente de salvamento.");
            }
        }

        if (
            _isSplineDragCreating &&
            _runtime is not null &&
            _runtime.IsSplinePlacementActive)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            if (
                _runtime.TryAdvanceSplinePlacement(
                    pixelX,
                    pixelY,
                    out var splinePlacement,
                    out var splineStatus))
            {
                if (
                    splinePlacement is null &&
                    _runtime.SplinePlacementStage ==
                        NativeSplinePlacementStage
                            .AwaitingEasyRoadConfirm)
                {
                    _runtime.TryConfirmEasyRoad(
                        out splinePlacement,
                        out splineStatus);
                }

                if (splinePlacement is not null)
                {
                    SplinePlacementRequested
                        ?.Invoke(
                            splinePlacement);
                }

                PointerStatusChanged?.Invoke(
                    this,
                    splinePlacement is not null
                        ? "Rua definida pelo arrasto · inserindo..."
                        : splineStatus);

                SplinePlacementControlStateChanged
                    ?.Invoke(
                        _runtime
                            .GetSplinePlacementControlState());
            }
            else
            {
                PointerStatusChanged?.Invoke(
                    this,
                    "Rua: não foi possível definir o ponto final. Solte sobre um tile carregado e com distância maior do ponto inicial.");
            }
        }

        if (
            _leftPressed ||
            _isPanning ||
            _isOrbiting ||
            _rightPressed ||
            _isManipulatingGizmo)
        {
            InputSurface.ReleasePointerCapture(
                e.Pointer);
        }

        if (
            _isPanning ||
            _isOrbiting)
        {
            FlushPendingNavigationFrame();
        }

        if (_isPanning)
        {
            _runtime
                ?.EndPointerPan();
        }
        else if (_isOrbiting)
        {
            _runtime
                ?.EndOrbit();
        }

        StopNavigationRendering();

        _leftPressed = false;
        _isPanning = false;
        _isOrbiting = false;
        _rightPressed = false;
        _isSplineDragCreating = false;
        _isManipulatingGizmo = false;
        _selectionBoxPending = false;
        _isSelectionBoxDragging = false;

        ProtectedCursor =
            _defaultCursor;
    }

    private void QueuePanFrame(
        uint pixelX,
        uint pixelY)
    {
        _pendingNavigationPixelX =
            pixelX;

        _pendingNavigationPixelY =
            pixelY;

        _navigationFramePending =
            true;

        EnsureNavigationRendering();
    }

    private void QueueOrbitFrame(
        double deltaX,
        double deltaY)
    {
        _pendingOrbitDeltaX +=
            deltaX;

        _pendingOrbitDeltaY +=
            deltaY;

        _navigationFramePending =
            true;

        EnsureNavigationRendering();
    }

    private void EnsureNavigationRendering()
    {
        if (_navigationRenderingHooked)
        {
            return;
        }

        Microsoft.UI.Xaml.Media
            .CompositionTarget.Rendering +=
            OnNavigationRendering;

        _navigationRenderingHooked =
            true;
    }

    private void OnNavigationRendering(
        object? sender,
        object args)
    {
        if (!_navigationFramePending)
        {
            if (
                !_isPanning &&
                !_isOrbiting)
            {
                StopNavigationRendering();
            }

            return;
        }

        FlushPendingNavigationFrame();
    }

    private void FlushPendingNavigationFrame()
    {
        if (
            !_navigationFramePending ||
            _runtime is null)
        {
            return;
        }

        _navigationFramePending =
            false;

        if (_isOrbiting)
        {
            var deltaX =
                _pendingOrbitDeltaX;

            var deltaY =
                _pendingOrbitDeltaY;

            _pendingOrbitDeltaX =
                0;

            _pendingOrbitDeltaY =
                0;

            if (
                Math.Abs(deltaX) >
                    0.0001 ||
                Math.Abs(deltaY) >
                    0.0001)
            {
                _runtime.Orbit(
                    deltaX,
                    deltaY);
            }

            return;
        }

        _pendingOrbitDeltaX =
            0;

        _pendingOrbitDeltaY =
            0;

        if (_isPanning)
        {
            _runtime.UpdatePointerPan(
                _pendingNavigationPixelX,
                _pendingNavigationPixelY);
        }
    }

    private void StopNavigationRendering()
    {
        if (_navigationRenderingHooked)
        {
            Microsoft.UI.Xaml.Media
                .CompositionTarget.Rendering -=
                OnNavigationRendering;

            _navigationRenderingHooked =
                false;
        }

        _navigationFramePending =
            false;

        _pendingOrbitDeltaX =
            0;

        _pendingOrbitDeltaY =
            0;
    }

    private void UpdateSelectionBoxVisual(
        double currentX,
        double currentY)
    {
        var left =
            Math.Min(
                _selectionBoxStartX,
                currentX);

        var top =
            Math.Min(
                _selectionBoxStartY,
                currentY);

        SelectionBoxBorder.Width =
            Math.Abs(
                currentX -
                _selectionBoxStartX);

        SelectionBoxBorder.Height =
            Math.Abs(
                currentY -
                _selectionBoxStartY);

        Canvas.SetLeft(
            SelectionBoxBorder,
            left);

        Canvas.SetTop(
            SelectionBoxBorder,
            top);
    }

    private void ResetSelectionBox()
    {
        SelectionBoxLayer.Visibility =
            Visibility.Collapsed;

        SelectionBoxBorder.Width =
            0;

        SelectionBoxBorder.Height =
            0;
    }

    private void TryOpenSelectionRadialMenu(
        double x,
        double y)
    {
        if (_runtime is null)
        {
            return;
        }

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        var pixelX =
            (uint)Math.Max(
                0,
                Math.Round(
                    x *
                    scaleX));

        var pixelY =
            (uint)Math.Max(
                0,
                Math.Round(
                    y *
                    scaleY));

        if (
            !_runtime.TryPick(
                pixelX,
                pixelY,
                out _,
                out var selected,
                cycleCandidates:
                    false) ||
            selected is null)
        {
            HideSelectionRadialMenu();
            PublishSelectionInfo();
            return;
        }

        var info =
            _runtime
                .GetSelectionInfo();

        if (info is null)
        {
            HideSelectionRadialMenu();
            return;
        }

        PublishSelectionInfo();

        RadialMenuSelectionText.Text =
            info.Kind ==
                MapStudio.Renderer.Picking
                    .PickingKind.Object
                ? $"Objeto #{info.EntityId}"
                : $"Spline #{info.EntityId}";

        if (
            info.Kind ==
                MapStudio.Renderer.Picking
                    .PickingKind.Spline)
        {
            ConfigureRadialButton(
                RadialTopButton,
                "Mover",
                NativeSelectionContextAction.Move);

            ConfigureRadialButton(
                RadialUpperRightButton,
                "Curvar",
                NativeSelectionContextAction.EditCurve);

            ConfigureRadialButton(
                RadialLowerRightButton,
                "Duplicar",
                NativeSelectionContextAction.Duplicate);

            ConfigureRadialButton(
                RadialBottomButton,
                "Excluir",
                NativeSelectionContextAction.Delete);

            ConfigureRadialButton(
                RadialLowerLeftButton,
                "Dividir",
                NativeSelectionContextAction.Split);

            ConfigureRadialButton(
                RadialUpperLeftButton,
                "Pontas",
                NativeSelectionContextAction.EditEndpoints);

            ConfigureRadialButton(
                RadialTopRightButton,
                "Elevar",
                NativeSelectionContextAction.Elevate);

            ConfigureRadialButton(
                RadialRightButton,
                "Nivelar",
                NativeSelectionContextAction.Level);

            ConfigureRadialButton(
                RadialBottomRightButton,
                "Baixar",
                NativeSelectionContextAction.Lower);

            ConfigureRadialButton(
                RadialBottomLeftButton,
                "Substituir",
                NativeSelectionContextAction.Replace);

            ConfigureRadialButton(
                RadialLeftButton,
                "Espelhar",
                NativeSelectionContextAction.Mirror);

            ConfigureRadialButton(
                RadialTopLeftButton,
                "Fluxo",
                NativeSelectionContextAction.Flow);

            SetSplineRadialExtraVisibility(
                Visibility.Visible);
        }
        else
        {
            SetSplineRadialExtraVisibility(
                Visibility.Collapsed);

            ConfigureRadialButton(
                RadialTopButton,
                "Mover",
                NativeSelectionContextAction.Move);

            ConfigureRadialButton(
                RadialUpperRightButton,
                "Girar",
                NativeSelectionContextAction.Rotate);

            ConfigureRadialButton(
                RadialLowerRightButton,
                "Duplicar",
                NativeSelectionContextAction.Duplicate);

            ConfigureRadialButton(
                RadialBottomButton,
                "Excluir",
                NativeSelectionContextAction.Delete);

            ConfigureRadialButton(
                RadialLowerLeftButton,
                "Inspector",
                NativeSelectionContextAction.Inspector);

            ConfigureRadialButton(
                RadialUpperLeftButton,
                "Focar",
                NativeSelectionContextAction.Focus);
        }

        var left =
            Math.Clamp(
                x -
                190,
                4,
                Math.Max(
                    4,
                    InputSurface.ActualWidth -
                    384));

        var top =
            Math.Clamp(
                y -
                190,
                4,
                Math.Max(
                    4,
                    InputSurface.ActualHeight -
                    384));

        Canvas.SetLeft(
            SelectionRadialMenu,
            left);

        Canvas.SetTop(
            SelectionRadialMenu,
            top);

        RadialMenuLayer.Visibility =
            Visibility.Visible;

        PointerStatusChanged?.Invoke(
            this,
            "Ações rápidas da seleção abertas.");
    }

    private void SetSplineRadialExtraVisibility(
        Visibility visibility)
    {
        RadialTopRightButton.Visibility =
            visibility;

        RadialRightButton.Visibility =
            visibility;

        RadialBottomRightButton.Visibility =
            visibility;

        RadialBottomLeftButton.Visibility =
            visibility;

        RadialLeftButton.Visibility =
            visibility;

        RadialTopLeftButton.Visibility =
            visibility;
    }

    private static void ConfigureRadialButton(
        Button button,
        string label,
        NativeSelectionContextAction action)
    {
        var content =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,
                Spacing =
                    4,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        content.Children.Add(
            new FontIcon
            {
                Glyph =
                    GetRadialActionGlyph(
                        action),
                FontSize =
                    12
            });

        content.Children.Add(
            new TextBlock
            {
                Text =
                    label,
                FontSize =
                    10,
                VerticalAlignment =
                    VerticalAlignment.Center
            });

        button.Content =
            content;

        button.Tag =
            action.ToString();

        ToolTipService.SetToolTip(
            button,
            label);
    }

    private static string GetRadialActionGlyph(
        NativeSelectionContextAction action) =>
        action switch
        {
            NativeSelectionContextAction.Move =>
                "↔",
            NativeSelectionContextAction.Rotate =>
                "⟳",
            NativeSelectionContextAction.Duplicate =>
                "⧉",
            NativeSelectionContextAction.Delete =>
                "✕",
            NativeSelectionContextAction.Focus =>
                "◎",
            NativeSelectionContextAction.Inspector =>
                "⚙",
            NativeSelectionContextAction.EditCurve =>
                "⌒",
            NativeSelectionContextAction.EditEndpoints =>
                "↔",
            NativeSelectionContextAction.Split =>
                "✂",
            NativeSelectionContextAction.Parallel =>
                "∥",
            NativeSelectionContextAction.Elevate =>
                "↑",
            NativeSelectionContextAction.Level =>
                "━",
            NativeSelectionContextAction.Lower =>
                "↓",
            NativeSelectionContextAction.Replace =>
                "⇄",
            NativeSelectionContextAction.Mirror =>
                "⇋",
            NativeSelectionContextAction.Flow =>
                "→",
            _ =>
                "•"
        };

    private void HideSelectionRadialMenu()
    {
        if (
            RadialMenuLayer.Visibility ==
            Visibility.Visible)
        {
            RadialMenuLayer.Visibility =
                Visibility.Collapsed;
        }
    }

    private void OnRadialMenuActionClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            sender is not
                Button
                {
                    Tag: string actionText
                } ||
            !Enum.TryParse<
                NativeSelectionContextAction>(
                    actionText,
                    ignoreCase:
                        true,
                    out var action))
        {
            return;
        }

        HideSelectionRadialMenu();

        SelectionContextActionRequested
            ?.Invoke(
                action);
    }

    private void OnPointerExited(
        object sender,
        PointerRoutedEventArgs e)
    {
        _lastHoverPixelX =
            uint.MaxValue;

        _lastHoverPixelY =
            uint.MaxValue;

        _runtime?.ClearHover();

        TerrainBrushLayer.Visibility =
            Visibility.Collapsed;

        _lastPointerLogicalX =
            double.NaN;

        _lastPointerLogicalY =
            double.NaN;

        if (
            !_isPanning &&
            !_isOrbiting)
        {
            ProtectedCursor =
                _defaultCursor;
        }
    }

    private void PublishSelectionInfo()
    {
        SelectionChanged
            ?.Invoke(
                _runtime
                    ?.GetSelectionInfo());
    }

    private void OnPointerWheelChanged(
        object sender,
        PointerRoutedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(
                InputSurface);

        if (_runtime is not null)
        {
            _runtime.Zoom(
                point.Properties
                    .MouseWheelDelta);

            PointerStatusChanged?.Invoke(
                this,
                $"Zoom nativo: {_runtime.Navigation.Zoom:F2}×");
        }

        e.Handled = true;
    }
}
