using System.Numerics;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Vegetation;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Timetables;
using MapStudio.Renderer.Graphics;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Vortice.Mathematics;

namespace MapStudio.Renderer.Viewport;

public sealed class NativeViewportRuntime : IDisposable
{
    private static readonly Color4 InitialClearColor =
        new(
            0.035f,
            0.075f,
            0.105f,
            1.0f);

    private IReadOnlyDictionary<
        string,
        NativeSceneryAsset>
        _sceneryAssets =
            new Dictionary<
                string,
                NativeSceneryAsset>(
                StringComparer
                    .OrdinalIgnoreCase);

    private IReadOnlyDictionary<
        string,
        NativeSplineAsset>
        _splineAssets =
            new Dictionary<
                string,
                NativeSplineAsset>(
                StringComparer
                    .OrdinalIgnoreCase);

    private readonly Stack<
        NativeTransformHistoryEntry>
        _undoStack =
            new();

    private readonly Stack<
        NativeTransformHistoryEntry>
        _redoStack =
            new();

    private PickingId _selectedPickingId =
        PickingId.None;

    private NativeGizmoHandle _activeGizmoHandle =
        NativeGizmoHandle.None;

    private Vector3 _dragAnchor;
    private Vector3 _dragTranslation;
    private float _dragRotationDegrees;
    private uint _lastDragPixelX;
    private uint _lastDragPixelY;
    private bool _assetPreviewActive;
    private bool _sceneryPlacementActive;
    private string? _placementSceneryPath;
    private bool _placementUsesAbsoluteHeight;
    private double? _placementZOverride;
    private double _placementRotation;
    private double _placementBaseRotation;
    private double _placementPitch;
    private double _placementBank;
    private bool _sceneryRoadSnapEnabled;
    private double _sceneryRoadSnapDistance =
        8.0;
    private NativeAssetPreviewGeometry?
        _placementGeometry;
    private Vector3? _placementWorldPoint;

    private bool _splinePlacementActive;
    private bool _splinePlacementCurved;
    private bool _splinePlacementIsHeight;
    private bool _splineEasyRoadEnabled;
    private double _splineEasyRoadCurveOffset;
    private string? _placementSplinePath;
    private NativeSplineAsset? _placementSplineAsset;
    private Vector3? _splineStartWorld;
    private Vector3? _splineEndWorld;
    private Vector3? _splinePointerWorld;
    private NativeSplinePlacementShape? _splinePlacementShape;
    private int _splinePreviousId =
        -1;

    private int _splineNextId =
        -1;

    private bool _splineEndpointSnapEnabled =
        true;

    private double _splineEndpointSnapDistance =
        5.0;

    private bool _splineAutoConnectEnabled =
        true;

    private double _splineElevationOffset;
    private NativeSplinePlacementStage _splinePlacementStage =
        NativeSplinePlacementStage.AwaitingStart;

    private OmsiMapDescriptor? _mapDescriptor;
    private string? _omsiRoot;
    private bool _nightPreviewEnabled;

    private NativeReferenceOverlayDefinition?
        _referenceOverlay;

    private NativeSceneVisibility
        _sceneVisibility =
            NativeSceneVisibility.All;

    private NativeSelectionFilter
        _selectionFilter =
            NativeSelectionFilter.All;

    private bool _disposed;

    public NativeViewportRuntime()
    {
        Device = new D3D11DeviceHost();

        MapRenderer =
            new D3D11NativeMapRenderer(
                Device);
    }

    public D3D11DeviceHost Device { get; }

    public D3D11SwapChainSurface? Surface { get; private set; }

    public D3D11NativeMapRenderer MapRenderer { get; }

    public NativeViewportNavigation Navigation { get; } =
        new();

    public PickingRegistry<object> Picking { get; } =
        new();

    public NativeSceneSnapshot? Scene { get; private set; }

    public NativeGizmoMode GizmoMode { get; private set; } =
        NativeGizmoMode.Move;

    public NativePendingTransformEdit? PendingTransformEdit
    {
        get;
        private set;
    }

    public bool SnapEnabled
    {
        get;
        private set;
    }

    public float MoveSnapMeters { get; } =
        0.25f;

    public float RotateSnapDegrees { get; } =
        5.0f;

    public bool CanUndo =>
        _undoStack.Count >
        0;

    public bool CanRedo =>
        _redoStack.Count >
        0;

    public bool IsManipulating =>
        _activeGizmoHandle !=
        NativeGizmoHandle.None;

    public bool IsDisposed => _disposed;

    public bool IsAssetPreviewActive =>
        _assetPreviewActive;

    public bool IsSceneryPlacementActive =>
        _sceneryPlacementActive;

    public bool IsSplinePlacementActive =>
        _splinePlacementActive;

    public bool SplinePlacementCurved =>
        _splinePlacementCurved;

    public NativeSplinePlacementStage SplinePlacementStage =>
        _splinePlacementStage;

    public NativeSplinePlacementControlState?
        GetSplinePlacementControlState()
    {
        if (
            !_splinePlacementActive ||
            !_splineEasyRoadEnabled ||
            _splineStartWorld is not { } start)
        {
            return null;
        }

        var end =
            _splinePlacementStage ==
                NativeSplinePlacementStage
                    .AwaitingEasyRoadConfirm
                ? _splineEndWorld
                : _splinePointerWorld;

        return end is { } value
            ? new NativeSplinePlacementControlState(
                start,
                value,
                _splineEasyRoadCurveOffset,
                _splinePlacementStage ==
                    NativeSplinePlacementStage
                        .AwaitingEasyRoadConfirm)
            : null;
    }

    public NativeSceneVisibility SceneVisibility =>
        _sceneVisibility;

    public NativeSelectionFilter SelectionFilter =>
        _selectionFilter;

    public bool TryBuildSplineXExport(
        IReadOnlyCollection<int> splineIds,
        out NativeSplineXExportResult? result,
        out string status)
    {
        ThrowIfDisposed();

        result =
            null;

        status =
            string.Empty;

        if (
            Scene is null ||
            splineIds.Count == 0)
        {
            status =
                "Spline Export: selecione ao menos uma spline carregada.";
            return false;
        }

        var idSet =
            splineIds
                .ToHashSet();

        var selected =
            Scene.Splines
                .Where(
                    entity =>
                        idSet.Contains(
                            entity.Spline
                                .SplineId))
                .OrderBy(
                    entity =>
                        entity.Spline
                            .SplineId)
                .ToArray();

        if (
            selected.Length !=
                idSet.Count)
        {
            status =
                "Spline Export: uma ou mais splines selecionadas não estão carregadas no viewport.";
            return false;
        }

        var filteredScene =
            new NativeSceneSnapshot(
                Scene.Tiles,
                [],
                selected,
                []);

        var geometry =
            new NativeSplineTriangleGeometryBuilder()
                .Build(
                    filteredScene,
                    _splineAssets);

        if (
            geometry.LoadedSplineCount !=
                selected.Length ||
            geometry.Vertices.Length ==
                0)
        {
            status =
                "Spline Export: uma ou mais SLI não possuem geometria renderizável carregada.";
            return false;
        }

        var first =
            selected[0];

        var origin =
            new Vector3(
                first.WorldX,
                first.WorldY,
                first.WorldZ);

        result =
            new NativeSplineXExporter()
                .Build(
                    geometry,
                    origin,
                    selected.Length);

        status =
            $"Spline Export: {result.SplineCount} spline(s), {result.TriangleCount} triângulo(s).";

        return true;
    }

    public bool TryBuildSplineCompleteToRequest(
        int sourceSplineId,
        int targetSplineId,
        double maximumRadius,
        out NativeSplinePlacementRequest? request,
        out string status)
    {
        ThrowIfDisposed();

        if (Scene is null)
        {
            request = null;
            status =
                "Complete to: nenhum mapa está carregado no viewport.";
            return false;
        }

        return NativeSplineCompleteToSolver
            .TryCreateRequest(
                Scene,
                sourceSplineId,
                targetSplineId,
                maximumRadius,
                out request,
                out status);
    }

    public NativeProceduralJunctionPlan
        BuildProceduralJunctionPlan(
            MapStudioRoadGraph graph)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            graph);

        if (Scene is null)
        {
            return new NativeProceduralJunctionPlan(
                Array.Empty<
                    NativeProceduralJunctionPlanItem>(),
                graph.Junctions.Count);
        }

        return new NativeProceduralJunctionPlanBuilder()
            .Build(
                Scene,
                graph);
    }

    public NativeProceduralRoadPlacementBuildResult
        BuildProceduralRoadPlacementRequests(
            MapStudioRoadGraph graph)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            graph);

        if (Scene is null)
        {
            return new NativeProceduralRoadPlacementBuildResult(
                Array.Empty<
                    NativeSplinePlacementRequest>(),
                Array.Empty<
                    NativeProceduralRoadPlacementLink>(),
                graph.Segments.Count);
        }

        return new NativeProceduralRoadPlacementBuilder()
            .Build(
                Scene,
                graph);
    }

    public NativeOsmVegetationPlacementBuildResult
        BuildOsmVegetationPlacementRequests(
            IReadOnlyList<MapStudioProjectedVegetationPoint> points,
            string sceneryObjectPath,
            bool randomRotation)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            points);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sceneryObjectPath);

        if (Scene is null)
        {
            return new NativeOsmVegetationPlacementBuildResult(
                Array.Empty<NativeSceneryPlacementRequest>(),
                points.Count);
        }

        return new NativeOsmVegetationPlacementBuilder()
            .Build(
                Scene,
                points,
                sceneryObjectPath,
                randomRotation);
    }

    public NativeBuildingFootprintPreviewGeometry
        PreviewBuildingFootprints(
            IReadOnlyList<MapStudioProjectedBuildingFootprint> buildings)
    {
        ThrowIfDisposed();

        if (Scene is null)
        {
            return new NativeBuildingFootprintPreviewGeometry(
                [],
                0,
                buildings.Count);
        }

        var geometry =
            new NativeBuildingFootprintPreviewGeometryBuilder()
                .Build(
                    Scene,
                    buildings);

        MapRenderer
            .SetTimetableRoutePreview(
                geometry.Vertices);

        RenderInitialFrame();

        return geometry;
    }

    public void ClearBuildingFootprintPreview()
    {
        ThrowIfDisposed();

        MapRenderer
            .SetTimetableRoutePreview(
                null);

        RenderInitialFrame();
    }

    public NativeVegetationPreviewGeometry
        PreviewVegetationPoints(
            IReadOnlyList<MapStudioProjectedVegetationPoint> points)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            points);

        if (Scene is null)
        {
            return new NativeVegetationPreviewGeometry(
                [],
                0,
                points.Count);
        }

        var geometry =
            new NativeVegetationPreviewGeometryBuilder()
                .Build(
                    Scene,
                    points);

        MapRenderer
            .SetTimetableRoutePreview(
                geometry.Vertices);

        RenderInitialFrame();

        return geometry;
    }

    public void ClearVegetationPreview()
    {
        ThrowIfDisposed();

        MapRenderer
            .SetTimetableRoutePreview(
                null);

        RenderInitialFrame();
    }

    public NativeProceduralRoadPreviewGeometry
        PreviewProceduralRoadGraph(
            MapStudioRoadGraph graph)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            graph);

        if (Scene is null)
        {
            return new NativeProceduralRoadPreviewGeometry(
                [],
                0,
                0);
        }

        var geometry =
            new NativeProceduralRoadPreviewGeometryBuilder()
                .Build(
                    Scene,
                    graph);

        MapRenderer
            .SetTimetableRoutePreview(
                geometry.Vertices);

        RenderInitialFrame();

        return geometry;
    }

    public void ClearProceduralRoadPreview()
    {
        ThrowIfDisposed();

        MapRenderer
            .SetTimetableRoutePreview(
                null);

        RenderInitialFrame();
    }

    public int PreviewTimetableTrack(
        IReadOnlyList<
            OmsiTimetableTrackEntry>
            entries)
    {
        ThrowIfDisposed();

        if (Scene is null)
        {
            return 0;
        }

        var references =
            entries
                .Select(
                    entry =>
                        new NativeTimetablePathReference(
                            entry.Id,
                            entry.Line2,
                            entry.Length))
                .ToArray();

        return SetTimetableRoutePreview(
            references);
    }

    public int PreviewStationLink(
        IReadOnlyList<
            OmsiStationLinkEntry>
            entries)
    {
        ThrowIfDisposed();

        if (Scene is null)
        {
            return 0;
        }

        var references =
            entries
                .Select(
                    entry =>
                        new NativeTimetablePathReference(
                            entry.Id,
                            entry.Line2,
                            entry.Length))
                .ToArray();

        return SetTimetableRoutePreview(
            references);
    }

    public void ClearTimetableRoutePreview()
    {
        ThrowIfDisposed();

        MapRenderer
            .SetTimetableRoutePreview(
                null);

        RenderInitialFrame();
    }

    private int SetTimetableRoutePreview(
        IReadOnlyList<
            NativeTimetablePathReference>
            references)
    {
        if (Scene is null)
        {
            return 0;
        }

        var geometry =
            new NativeTimetableRouteGeometryBuilder()
                .Build(
                    Scene,
                    _splineAssets,
                    _sceneryAssets,
                    references);

        MapRenderer
            .SetTimetableRoutePreview(
                geometry.Vertices);

        RenderInitialFrame();

        return geometry.ResolvedReferenceCount;
    }

    public IReadOnlyList<
        NativeTrafficLightProgramInfo>
        GetTrafficLightPrograms()
    {
        if (Scene is null)
        {
            return Array.Empty<
                NativeTrafficLightProgramInfo>();
        }

        var result =
            new List<
                NativeTrafficLightProgramInfo>();

        foreach (
            var entity in
                Scene.Objects)
        {
            if (
                !_sceneryAssets.TryGetValue(
                    entity.Object
                        .SceneryObjectPath,
                    out var asset) ||
                asset
                    .TrafficLightControllers
                    .Count == 0)
            {
                continue;
            }

            for (
                var controllerIndex = 0;
                controllerIndex <
                    asset
                        .TrafficLightControllers
                        .Count;
                controllerIndex++)
            {
                var controller =
                    asset
                        .TrafficLightControllers[
                            controllerIndex];

                for (
                    var programIndex = 0;
                    programIndex <
                        controller.Programs.Count;
                    programIndex++)
                {
                    var program =
                        controller.Programs[
                            programIndex];

                    result.Add(
                        new NativeTrafficLightProgramInfo(
                            entity.Object
                                .ObjectId,
                            entity.Tile.X,
                            entity.Tile.Y,
                            entity.Object
                                .SceneryObjectPath,
                            controllerIndex,
                            programIndex,
                            program.Name,
                            controller
                                .CycleDuration,
                            program.Phases));
                }
            }
        }

        return result;
    }

    public bool GridVisible =>
        MapRenderer.GridVisible;

    public bool TrafficPathsVisible =>
        MapRenderer.TrafficPathsVisible;

    public int TrafficPathLineCount =>
        MapRenderer.TrafficPathLineCount;

    public bool SetTrafficPathsVisible(
        bool visible)
    {
        ThrowIfDisposed();

        var changed =
            MapRenderer
                .SetTrafficPathsVisible(
                    visible);

        if (changed)
        {
            RenderInitialFrame();
        }

        return changed;
    }

    public void SetSplineEndpointSnapOptions(
        bool enabled,
        double distance,
        bool autoConnect)
    {
        ThrowIfDisposed();

        _splineEndpointSnapEnabled =
            enabled;

        _splineEndpointSnapDistance =
            Math.Clamp(
                double.IsFinite(
                    distance)
                    ? distance
                    : 5.0,
                0.5,
                30.0);

        _splineAutoConnectEnabled =
            autoConnect;
    }

    public void SetSplinePlacementElevationOffset(
        double offset)
    {
        ThrowIfDisposed();

        _splineElevationOffset =
            Math.Clamp(
                double.IsFinite(offset)
                    ? offset
                    : 0.0,
                -100.0,
                300.0);
    }

    public void SetSplinePlacementHeightMode(
        bool isHeightSpline)
    {
        ThrowIfDisposed();

        _splinePlacementIsHeight =
            isHeightSpline;

        if (isHeightSpline)
        {
            _splineEasyRoadEnabled =
                false;
        }
    }

    public void SetSplineEasyRoadOptions(
        bool enabled,
        double curveOffset)
    {
        ThrowIfDisposed();

        _splineEasyRoadEnabled =
            enabled;

        _splineEasyRoadCurveOffset =
            Math.Clamp(
                double.IsFinite(
                    curveOffset)
                    ? curveOffset
                    : 0.0,
                -500.0,
                500.0);

        if (
            !_splinePlacementActive ||
            Scene is null ||
            _placementSplineAsset is null ||
            _splineStartWorld is not { } start)
        {
            return;
        }

        var end =
            _splinePlacementStage switch
            {
                NativeSplinePlacementStage
                    .AwaitingEnd =>
                    _splinePointerWorld,
                NativeSplinePlacementStage
                    .AwaitingEasyRoadConfirm =>
                    _splineEndWorld,
                _ =>
                    null
            };

        if (end is not { } endPoint)
        {
            return;
        }

        NativeSplinePlacementShape? shape;

        var valid =
            _splineEasyRoadEnabled
                ? NativeSplinePlacementMath
                    .TryCreateArcFromOffset(
                        start,
                        endPoint,
                        _splineEasyRoadCurveOffset,
                        out shape)
                : NativeSplinePlacementMath
                    .TryCreateStraight(
                        start,
                        endPoint,
                        out shape);

        _splinePlacementShape =
            valid
                ? shape
                : null;

        if (
            _splinePlacementShape is
                not { } previewShape)
        {
            MapRenderer.SetPlacementPreview(
                null,
                Matrix4x4.Identity);

            RenderInitialFrame();
            return;
        }

        var geometry =
            new NativeSplinePlacementGeometryBuilder()
                .Build(
                    _placementSplineAsset,
                    previewShape);

        MapRenderer.SetPlacementPreview(
            geometry.IsRenderable
                ? geometry
                : null,
            Matrix4x4.Identity);

        RenderInitialFrame();
    }

    public bool SeedSplinePlacementStart(
        Vector3 start,
        int previousSplineId =
            -1)
    {
        ThrowIfDisposed();

        if (
            !_splinePlacementActive ||
            Scene is null)
        {
            return false;
        }

        var tileX =
            (int)Math.Floor(
                start.X /
                300.0f);

        var tileY =
            (int)Math.Floor(
                start.Z /
                300.0f);

        if (
            !Scene.Tiles.Any(
                tile =>
                    tile.Reference.X ==
                        tileX &&
                    tile.Reference.Y ==
                        tileY))
        {
            return false;
        }

        _splineStartWorld =
            start;

        _splinePreviousId =
            previousSplineId;

        _splineNextId =
            -1;

        _splineEndWorld =
            null;

        _splinePointerWorld =
            start;

        _splinePlacementShape =
            null;

        _splinePlacementStage =
            NativeSplinePlacementStage
                .AwaitingEnd;

        MapRenderer.SetPlacementPreview(
            null,
            Matrix4x4.Identity);

        RenderInitialFrame();

        return true;
    }

    public async Task<bool>
        BeginSplinePlacementAsync(
            string omsiRoot,
            string splinePath,
            bool curved,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(splinePath);

        if (_assetPreviewActive)
        {
            RestoreSceneView();
        }

        if (Scene is null)
        {
            return false;
        }

        CancelGizmoDrag();
        CancelSceneryPlacement();
        CancelSplinePlacement();

        var asset =
            await new NativeSplineAssetLoader()
                .LoadAssetAsync(
                    omsiRoot,
                    splinePath,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!asset.IsLoaded)
        {
            return false;
        }

        _splinePlacementActive = true;
        _splinePlacementCurved = curved;
        _placementSplinePath = splinePath;
        _placementSplineAsset = asset;
        _splineStartWorld = null;
        _splineEndWorld = null;
        _splinePointerWorld = null;
        _splinePlacementShape = null;
        _splinePreviousId = -1;
        _splineNextId = -1;
        _splinePlacementStage =
            NativeSplinePlacementStage.AwaitingStart;

        MapRenderer.SetHover(PickingId.None);
        MapRenderer.SetSelection(PickingId.None);
        MapRenderer.SetGizmoGeometry(null);
        MapRenderer.SetPlacementPreview(
            null,
            Matrix4x4.Identity);

        return true;
    }

    public bool UpdateSplinePlacement(
        uint pixelX,
        uint pixelY)
    {
        ThrowIfDisposed();

        if (
            !_splinePlacementActive ||
            Scene is null ||
            _placementSplineAsset is null)
        {
            return false;
        }

        if (
            _splinePlacementStage ==
                NativeSplinePlacementStage
                    .AwaitingEasyRoadConfirm)
        {
            return true;
        }

        if (
            !TryGetTerrainPlacementPoint(
                pixelX,
                pixelY,
                out var point))
        {
            return false;
        }

        point.Y +=
            (float)
                _splineElevationOffset;

        _splinePointerWorld = point;

        NativeSplinePlacementShape? shape = null;

        if (
            _splinePlacementStage ==
                NativeSplinePlacementStage.AwaitingEnd &&
            _splineStartWorld is { } start)
        {
            if (
                _splineEasyRoadEnabled &&
                !_splinePlacementIsHeight)
            {
                NativeSplinePlacementMath
                    .TryCreateArcFromOffset(
                        start,
                        point,
                        _splineEasyRoadCurveOffset,
                        out shape);
            }
            else
            {
                NativeSplinePlacementMath
                    .TryCreateStraight(
                        start,
                        point,
                        out shape);
            }
        }
        else if (
            _splinePlacementStage ==
                NativeSplinePlacementStage.AwaitingCurve &&
            _splineStartWorld is { } curveStart &&
            _splineEndWorld is { } curveEnd)
        {
            NativeSplinePlacementMath.TryCreateArc(
                curveStart,
                curveEnd,
                point,
                out shape);
        }

        _splinePlacementShape = shape;

        if (shape is null)
        {
            MapRenderer.SetPlacementPreview(
                null,
                Matrix4x4.Identity);

            RenderInitialFrame();
            return true;
        }

        var geometry =
            new NativeSplinePlacementGeometryBuilder()
                .Build(
                    _placementSplineAsset,
                    shape);

        MapRenderer.SetPlacementPreview(
            geometry.IsRenderable
                ? geometry
                : null,
            Matrix4x4.Identity);

        RenderInitialFrame();
        return true;
    }

    public bool TryAdvanceSplinePlacement(
        uint pixelX,
        uint pixelY,
        out NativeSplinePlacementRequest? request,
        out string status)
    {
        ThrowIfDisposed();

        request = null;
        status = string.Empty;

        if (
            !_splinePlacementActive ||
            Scene is null ||
            string.IsNullOrWhiteSpace(
                _placementSplinePath))
        {
            return false;
        }

        if (
            !UpdateSplinePlacement(
                pixelX,
                pixelY) ||
            _splinePointerWorld is not { } point)
        {
            return false;
        }

        if (
            _splinePlacementStage ==
            NativeSplinePlacementStage.AwaitingStart)
        {
            NativeSplineEndpointSnap?
                startSnap =
                    null;

            if (
                _splineEndpointSnapEnabled &&
                !_splinePlacementIsHeight)
            {
                startSnap =
                    NativeSplineEndpointSnapFinder
                        .FindFreeEndpoint(
                            Scene,
                            point,
                            NativeSplineEndpointKind.End,
                            _splineEndpointSnapDistance);

                if (startSnap is not null)
                {
                    point =
                        startSnap.WorldPoint;
                }
            }

            _splineStartWorld =
                point;

            _splinePreviousId =
                _splineAutoConnectEnabled &&
                startSnap is not null
                    ? startSnap.SplineId
                    : -1;

            _splineNextId =
                -1;

            _splinePlacementStage =
                NativeSplinePlacementStage.AwaitingEnd;

            status =
                startSnap is not null
                    ? $"Início encaixado na spline #{startSnap.SplineId} ({startSnap.Distance:F2} m). " +
                      (
                          _splineAutoConnectEnabled
                              ? "Previous será conectado automaticamente."
                              : "Auto-link desativado."
                      )
                    : _splineEasyRoadEnabled
                        ? "Início definido. Clique no ponto final para criar a via pelo modo Estrada fácil."
                        : _splinePlacementCurved
                            ? "Início definido. Clique no ponto final; depois ajuste a curva."
                            : "Início definido. Clique no ponto final para criar a spline.";

            MapRenderer.SetPlacementPreview(
                null,
                Matrix4x4.Identity);

            return true;
        }

        if (
            _splinePlacementStage ==
            NativeSplinePlacementStage.AwaitingEnd)
        {
            NativeSplineEndpointSnap?
                endSnap =
                    null;

            if (
                _splineEndpointSnapEnabled &&
                !_splinePlacementIsHeight)
            {
                endSnap =
                    NativeSplineEndpointSnapFinder
                        .FindFreeEndpoint(
                            Scene,
                            point,
                            NativeSplineEndpointKind.Start,
                            _splineEndpointSnapDistance,
                            _splinePreviousId);

                if (endSnap is not null)
                {
                    point =
                        endSnap.WorldPoint;
                }
            }

            _splineNextId =
                _splineAutoConnectEnabled &&
                endSnap is not null
                    ? endSnap.SplineId
                    : -1;

            if (
                _splineStartWorld is not { } start)
            {
                status = "Ponto inicial inválido.";
                return false;
            }

            NativeSplinePlacementShape?
                endShape;

            var endShapeValid =
                _splineEasyRoadEnabled &&
                !_splinePlacementIsHeight
                    ? NativeSplinePlacementMath
                        .TryCreateArcFromOffset(
                            start,
                            point,
                            _splineEasyRoadCurveOffset,
                            out endShape)
                    : NativeSplinePlacementMath
                        .TryCreateStraight(
                            start,
                            point,
                            out endShape);

            if (
                !endShapeValid ||
                endShape is null)
            {
                status = "Ponto final inválido.";
                return false;
            }

            if (
                _splineEasyRoadEnabled &&
                !_splinePlacementIsHeight)
            {
                _splineEndWorld =
                    point;

                _splinePointerWorld =
                    point;

                _splinePlacementShape =
                    endShape;

                _splinePlacementStage =
                    NativeSplinePlacementStage
                        .AwaitingEasyRoadConfirm;

                var previewGeometry =
                    new NativeSplinePlacementGeometryBuilder()
                        .Build(
                            _placementSplineAsset!,
                            endShape);

                MapRenderer
                    .SetPlacementPreview(
                        previewGeometry.IsRenderable
                            ? previewGeometry
                            : null,
                        Matrix4x4.Identity);

                RenderInitialFrame();

                status =
                    endShape.IsCurved
                        ? $"Fim definido · offset {_splineEasyRoadCurveOffset:+0.0;-0.0;0.0} m. Ajuste os pontos/curva e confirme."
                        : "Fim definido. Ajuste os pontos se necessário e confirme a Estrada fácil.";

                return true;
            }

            if (_splinePlacementCurved)
            {
                _splineEndWorld = point;
                _splinePlacementStage =
                    NativeSplinePlacementStage.AwaitingCurve;
                _splinePlacementShape = endShape;

                status =
                    endSnap is not null
                        ? $"Final encaixado na spline #{endSnap.SplineId} ({endSnap.Distance:F2} m). Ajuste a curva e clique para confirmar."
                        : "Final definido. Mova o cursor para curvar e clique para confirmar.";

                return true;
            }

            request =
                CreateSplinePlacementRequest(
                    endShape);

            CancelSplinePlacement();
            status =
                "Spline reta pronta para inserção.";

            return request is not null;
        }

        if (
            _splinePlacementStage ==
                NativeSplinePlacementStage.AwaitingCurve &&
            _splineStartWorld is { } curveStart &&
            _splineEndWorld is { } curveEnd &&
            NativeSplinePlacementMath.TryCreateArc(
                curveStart,
                curveEnd,
                point,
                out var curvedShape) &&
            curvedShape is not null)
        {
            request =
                CreateSplinePlacementRequest(
                    curvedShape);

            CancelSplinePlacement();

            status =
                curvedShape.IsCurved
                    ? "Spline curva pronta para inserção."
                    : "Controle colinear: spline reta pronta para inserção.";

            return request is not null;
        }

        if (
            _splinePlacementStage ==
                NativeSplinePlacementStage
                    .AwaitingEasyRoadConfirm)
        {
            status =
                "Estrada fácil aguardando confirmação pelo painel.";

            return true;
        }

        status = "Curva inválida.";
        return false;
    }

    public bool TrySetEasyRoadControlPoints(
        double startX,
        double startZ,
        double endX,
        double endZ,
        out NativeSplinePlacementControlState?
            state)
    {
        ThrowIfDisposed();

        state = null;

        if (
            !_splinePlacementActive ||
            !_splineEasyRoadEnabled ||
            _splinePlacementStage !=
                NativeSplinePlacementStage
                    .AwaitingEasyRoadConfirm ||
            Scene is null ||
            _placementSplineAsset is null ||
            !double.IsFinite(startX) ||
            !double.IsFinite(startZ) ||
            !double.IsFinite(endX) ||
            !double.IsFinite(endZ))
        {
            return false;
        }

        if (
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    Scene,
                    startX,
                    startZ,
                    out var startHeight) ||
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    Scene,
                    endX,
                    endZ,
                    out var endHeight))
        {
            return false;
        }

        var start =
            new Vector3(
                (float)startX,
                (float)(
                    startHeight +
                    _splineElevationOffset),
                (float)startZ);

        var end =
            new Vector3(
                (float)endX,
                (float)(
                    endHeight +
                    _splineElevationOffset),
                (float)endZ);

        if (
            !NativeSplinePlacementMath
                .TryCreateArcFromOffset(
                    start,
                    end,
                    _splineEasyRoadCurveOffset,
                    out var shape) ||
            shape is null)
        {
            return false;
        }

        _splineStartWorld =
            start;

        _splineEndWorld =
            end;

        _splinePointerWorld =
            end;

        _splinePlacementShape =
            shape;

        _splinePreviousId =
            -1;

        _splineNextId =
            -1;

        var geometry =
            new NativeSplinePlacementGeometryBuilder()
                .Build(
                    _placementSplineAsset,
                    shape);

        MapRenderer
            .SetPlacementPreview(
                geometry.IsRenderable
                    ? geometry
                    : null,
                Matrix4x4.Identity);

        RenderInitialFrame();

        state =
            GetSplinePlacementControlState();

        return state is not null;
    }

    public bool TryConfirmEasyRoad(
        out NativeSplinePlacementRequest?
            request,
        out string status)
    {
        ThrowIfDisposed();

        request =
            null;

        status =
            string.Empty;

        if (
            !_splinePlacementActive ||
            !_splineEasyRoadEnabled ||
            _splinePlacementStage !=
                NativeSplinePlacementStage
                    .AwaitingEasyRoadConfirm ||
            _splinePlacementShape is not
                { } shape)
        {
            status =
                "Estrada fácil ainda não possui uma prévia pronta para confirmar.";

            return false;
        }

        request =
            CreateSplinePlacementRequest(
                shape);

        if (request is null)
        {
            status =
                "Não foi possível criar a solicitação da Estrada fácil.";

            return false;
        }

        status =
            shape.IsCurved
                ? "Estrada fácil curva confirmada."
                : "Estrada fácil reta confirmada.";

        CancelSplinePlacement();

        return true;
    }

    public void CancelSplinePlacement()
    {
        if (!_splinePlacementActive)
        {
            return;
        }

        _splinePlacementActive = false;
        _splinePlacementCurved = false;
        _placementSplinePath = null;
        _placementSplineAsset = null;
        _splineStartWorld = null;
        _splineEndWorld = null;
        _splinePointerWorld = null;
        _splinePlacementShape = null;
        _splinePreviousId = -1;
        _splineNextId = -1;
        _splinePlacementStage =
            NativeSplinePlacementStage.AwaitingStart;

        MapRenderer.SetPlacementPreview(
            null,
            Matrix4x4.Identity);

        UpdateGizmoGeometry();
        RenderInitialFrame();
    }

    public void SetSceneryRoadSnapOptions(
        bool enabled,
        double distance)
    {
        _sceneryRoadSnapEnabled =
            enabled;

        _sceneryRoadSnapDistance =
            double.IsFinite(
                distance)
                ? Math.Clamp(
                    distance,
                    1.0,
                    50.0)
                : 8.0;
    }

    public Task<bool>
        BeginSceneryPlacementAsync(
            string omsiRoot,
            string sceneryObjectPath,
            CancellationToken cancellationToken =
                default) =>
        BeginSceneryPlacementAsync(
            omsiRoot,
            sceneryObjectPath,
            null,
            0,
            0,
            0,
            cancellationToken);

    public async Task<bool>
        BeginSceneryPlacementAsync(
            string omsiRoot,
            string sceneryObjectPath,
            double? zOverride,
            double rotation,
            double pitch,
            double bank,
            CancellationToken cancellationToken =
                default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sceneryObjectPath);

        if (_assetPreviewActive)
        {
            RestoreSceneView();
        }

        if (Scene is null)
        {
            return false;
        }

        CancelGizmoDrag();
        CancelSplinePlacement();

        var asset =
            await new NativeSceneryAssetLoader()
                .LoadAssetAsync(
                    omsiRoot,
                    sceneryObjectPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var geometry =
            new NativeAssetPreviewGeometryBuilder()
                .BuildScenery(
                    asset);

        if (!geometry.IsRenderable)
        {
            return false;
        }

        _sceneryPlacementActive =
            true;

        _placementSceneryPath =
            sceneryObjectPath;

        _placementUsesAbsoluteHeight =
            asset.UsesAbsoluteHeight;

        _placementZOverride =
            zOverride;

        _placementRotation =
            rotation;

        _placementBaseRotation =
            rotation;

        _placementPitch =
            pitch;

        _placementBank =
            bank;

        _placementGeometry =
            geometry;

        _placementWorldPoint =
            null;

        MapRenderer.SetHover(
            PickingId.None);

        MapRenderer.SetSelection(
            PickingId.None);

        MapRenderer.SetGizmoGeometry(
            null);

        MapRenderer.SetPlacementPreview(
            geometry,
            Matrix4x4
                .CreateFromYawPitchRoll(
                    (float)(
                        _placementRotation *
                        Math.PI /
                        180.0),
                    (float)(
                        _placementPitch *
                        Math.PI /
                        180.0),
                    (float)(
                        _placementBank *
                        Math.PI /
                        180.0)));

        return true;
    }

    public bool UpdateSceneryPlacement(
        uint pixelX,
        uint pixelY)
    {
        ThrowIfDisposed();

        if (
            !_sceneryPlacementActive ||
            Scene is null ||
            Surface is null ||
            _placementGeometry is null)
        {
            return false;
        }

        if (
            !Navigation.TryGetWorldRay(
                pixelX,
                pixelY,
                Surface.Width,
                Surface.Height,
                out var origin,
                out var direction) ||
            Math.Abs(
                direction.Y) <
            0.00001f)
        {
            return false;
        }

        var height =
            Navigation.Target.Y;

        Vector3 point =
            default;

        for (
            var iteration = 0;
            iteration < 4;
            iteration++)
        {
            var distance =
                (
                    height -
                    origin.Y
                ) /
                direction.Y;

            if (
                distance <=
                0)
            {
                return false;
            }

            point =
                origin +
                direction *
                distance;

            height =
                (float)
                    NativeTerrainSampler
                        .GetHeightAtWorldPoint(
                            Scene,
                            point.X,
                            point.Z);
        }

        if (SnapEnabled)
        {
            point.X =
                MathF.Round(
                    point.X /
                    MoveSnapMeters) *
                MoveSnapMeters;

            point.Z =
                MathF.Round(
                    point.Z /
                    MoveSnapMeters) *
                MoveSnapMeters;

            height =
                (float)
                    NativeTerrainSampler
                        .GetHeightAtWorldPoint(
                            Scene,
                            point.X,
                            point.Z);
        }

        _placementRotation =
            _placementBaseRotation;

        if (
            _sceneryRoadSnapEnabled &&
            NativeSceneryRoadSnapper
                .FindNearest(
                    Scene,
                    point,
                    _sceneryRoadSnapDistance) is
                { } roadSnap)
        {
            point.X =
                roadSnap.WorldPoint.X;

            point.Z =
                roadSnap.WorldPoint.Z;

            _placementRotation =
                roadSnap.Rotation;

            height =
                (float)
                    NativeTerrainSampler
                        .GetHeightAtWorldPoint(
                            Scene,
                            point.X,
                            point.Z);
        }

        var tileX =
            (int)Math.Floor(
                point.X /
                300.0f);

        var tileY =
            (int)Math.Floor(
                point.Z /
                300.0f);

        if (
            !Scene.Tiles.Any(
                tile =>
                    tile.Reference.X ==
                        tileX &&
                    tile.Reference.Y ==
                        tileY))
        {
            return false;
        }

        point.Y =
            _placementZOverride.HasValue
                ? _placementUsesAbsoluteHeight
                    ? (float)
                        _placementZOverride.Value
                    : height +
                        (float)
                            _placementZOverride.Value
                : height;

        _placementWorldPoint =
            point;

        MapRenderer
            .SetPlacementPreviewTransform(
                Matrix4x4
                    .CreateFromYawPitchRoll(
                        (float)(
                            _placementRotation *
                            Math.PI /
                            180.0),
                        (float)(
                            _placementPitch *
                            Math.PI /
                            180.0),
                        (float)(
                            _placementBank *
                            Math.PI /
                            180.0)) *
                Matrix4x4
                    .CreateTranslation(
                        point));

        RenderInitialFrame();

        return true;
    }

    public bool TryFinishSceneryPlacement(
        out NativeSceneryPlacementRequest?
            request)
    {
        ThrowIfDisposed();

        request =
            null;

        if (
            !_sceneryPlacementActive ||
            Scene is null ||
            _placementWorldPoint is not
                { } point ||
            string.IsNullOrWhiteSpace(
                _placementSceneryPath))
        {
            return false;
        }

        var tileX =
            (int)Math.Floor(
                point.X /
                300.0f);

        var tileY =
            (int)Math.Floor(
                point.Z /
                300.0f);

        var tile =
            Scene.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            tileX &&
                        item.Reference.Y ==
                            tileY);

        if (tile is null)
        {
            return false;
        }

        request =
            new NativeSceneryPlacementRequest(
                tile.Reference,
                _placementSceneryPath,
                point.X -
                    tileX *
                    300.0,
                point.Z -
                    tileY *
                    300.0,
                _placementZOverride ??
                    (
                        _placementUsesAbsoluteHeight
                            ? point.Y
                            : 0.0
                    ),
                _placementRotation,
                _placementPitch,
                _placementBank,
                point,
                _placementUsesAbsoluteHeight);

        CancelSceneryPlacement();

        return true;
    }

    public bool TryFinishSceneryPlacementAtWorldPoint(
        Vector3 worldPoint,
        double rotation,
        out NativeSceneryPlacementRequest?
            request)
    {
        ThrowIfDisposed();

        request =
            null;

        if (
            !_sceneryPlacementActive ||
            Scene is null ||
            string.IsNullOrWhiteSpace(
                _placementSceneryPath) ||
            !float.IsFinite(
                worldPoint.X) ||
            !float.IsFinite(
                worldPoint.Z))
        {
            return false;
        }

        var point =
            worldPoint;

        var terrainHeight =
            NativeTerrainSampler
                .GetHeightAtWorldPoint(
                    Scene,
                    point.X,
                    point.Z);

        point.Y =
            _placementZOverride.HasValue
                ? _placementUsesAbsoluteHeight
                    ? (float)
                        _placementZOverride.Value
                    : (float)(
                        terrainHeight +
                        _placementZOverride.Value)
                : (float)terrainHeight;

        var tileX =
            (int)Math.Floor(
                point.X /
                300.0f);

        var tileY =
            (int)Math.Floor(
                point.Z /
                300.0f);

        var tile =
            Scene.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            tileX &&
                        item.Reference.Y ==
                            tileY);

        if (tile is null)
        {
            return false;
        }

        _placementRotation =
            rotation;

        request =
            new NativeSceneryPlacementRequest(
                tile.Reference,
                _placementSceneryPath,
                point.X -
                    tileX *
                    300.0,
                point.Z -
                    tileY *
                    300.0,
                _placementZOverride ??
                    (
                        _placementUsesAbsoluteHeight
                            ? point.Y
                            : 0.0
                    ),
                rotation,
                _placementPitch,
                _placementBank,
                point,
                _placementUsesAbsoluteHeight);

        CancelSceneryPlacement();

        return true;
    }

    public void CancelSceneryPlacement()
    {
        if (!_sceneryPlacementActive)
        {
            return;
        }

        _sceneryPlacementActive =
            false;

        _placementSceneryPath =
            null;

        _placementZOverride =
            null;

        _placementRotation =
            0;

        _placementBaseRotation =
            0;

        _placementPitch =
            0;

        _placementBank =
            0;

        _placementGeometry =
            null;

        _placementWorldPoint =
            null;

        MapRenderer.SetPlacementPreview(
            null,
            Matrix4x4.Identity);

        UpdateGizmoGeometry();
        RenderInitialFrame();
    }

    public async Task<NativeAssetPreviewResult>
        PreviewAssetAsync(
            string omsiRoot,
            OmsiAssetKind kind,
            string relativePath,
            CancellationToken cancellationToken =
                default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            relativePath);

        CancelGizmoDrag();
        CancelSceneryPlacement();
        CancelSplinePlacement();

        if (
            kind ==
            OmsiAssetKind.Texture)
        {
            if (_assetPreviewActive)
            {
                RestoreSceneView();
            }

            if (
                !TryResolveIndexedAssetPath(
                    omsiRoot,
                    relativePath,
                    out var texturePath))
            {
                return new NativeAssetPreviewResult(
                    kind,
                    relativePath,
                    false,
                    0,
                    0,
                    "previewTexturePathInvalid");
            }

            var thumbnail =
                new NativeTextureThumbnailGenerator()
                    .RenderBmp(
                        texturePath);

            return new NativeAssetPreviewResult(
                kind,
                relativePath,
                thumbnail.Length > 54,
                0,
                0,
                thumbnail.Length > 54
                    ? null
                    : "previewTextureDecodeFailed",
                thumbnail.Length > 54
                    ? thumbnail
                    : null);
        }

        var builder =
            new NativeAssetPreviewGeometryBuilder();

        NativeAssetPreviewGeometry preview;

        if (
            kind ==
            OmsiAssetKind.SceneryObject)
        {
            var asset =
                await new NativeSceneryAssetLoader()
                    .LoadAssetAsync(
                        omsiRoot,
                        relativePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            preview =
                builder.BuildScenery(
                    asset);
        }
        else if (
            kind ==
            OmsiAssetKind.Spline)
        {
            var asset =
                await new NativeSplineAssetLoader()
                    .LoadAssetAsync(
                        omsiRoot,
                        relativePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            preview =
                builder.BuildSpline(
                    asset);
        }
        else if (
            kind ==
            OmsiAssetKind.Model)
        {
            if (
                !TryResolveIndexedAssetPath(
                    omsiRoot,
                    relativePath,
                    out var modelPath))
            {
                preview =
                    NativeAssetPreviewGeometry
                        .Error(
                            "previewModelPathInvalid");
            }
            else
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var geometry =
                    string.Equals(
                        Path.GetExtension(
                            modelPath),
                        ".x",
                        StringComparison
                            .OrdinalIgnoreCase)
                        ? new OmsiDirectXTextGeometryReader()
                            .Read(
                                modelPath)
                        : new OmsiO3dGeometryReader()
                            .Read(
                                modelPath);

                preview =
                    builder.BuildModel(
                        geometry);
            }
        }
        else
        {
            preview =
                NativeAssetPreviewGeometry
                    .Error(
                        "previewUnsupportedAssetKind");
        }

        if (!preview.IsRenderable)
        {
            return new NativeAssetPreviewResult(
                kind,
                relativePath,
                false,
                0,
                preview.SourceMeshCount,
                preview.ErrorCode);
        }

        var thumbnail =
            new NativeAssetThumbnailGenerator()
                .RenderBmp(
                    preview);

        _assetPreviewActive =
            true;

        MapRenderer.SetHover(
            PickingId.None);

        MapRenderer.SetSelection(
            PickingId.None);

        MapRenderer.SetGizmoGeometry(
            null);

        var emptyScene =
            new NativeSceneSnapshot(
                Array.Empty<
                    NativeSceneTile>(),
                Array.Empty<
                    NativeObjectEntity>(),
                Array.Empty<
                    NativeSplineEntity>(),
                Array.Empty<
                    NativeTerrainEntity>());

        var objectGeometry =
            new NativeObjectTriangleGeometry(
                preview.Vertices,
                Array.Empty<
                    NativeMapVertex>(),
                new Dictionary<
                    PickingId,
                    NativeTriangleRange>(),
                Array.Empty<
                    NativeMaterialBatch>(),
                kind is
                    OmsiAssetKind.SceneryObject or
                    OmsiAssetKind.Model
                    ? 1
                    : 0,
                preview.SourceMeshCount);

        MapRenderer.SetReferenceOverlay(
            null);

        MapRenderer.Upload(
            emptyScene,
            objectGeometry);

        Navigation.FitToBounds(
            preview.Minimum,
            preview.Maximum);

        UpdateCameraTransform();
        RenderInitialFrame();

        return new NativeAssetPreviewResult(
            kind,
            relativePath,
            true,
            preview.TriangleCount,
            preview.SourceMeshCount,
            null,
            thumbnail);
    }

    public void RestoreSceneView()
    {
        ThrowIfDisposed();

        if (!_assetPreviewActive)
        {
            return;
        }

        _assetPreviewActive =
            false;

        if (Scene is null)
        {
            RenderInitialFrame();
            return;
        }

        Navigation.FitToScene(
            Scene);

        UploadSceneGeometry();

        MapRenderer.SetSelection(
            _selectedPickingId);

        UpdateCameraTransform();
        UpdateGizmoGeometry();
        RenderInitialFrame();
    }

    public NativeSelectionInfo?
        GetSelectionInfo()
    {
        if (
            Scene is null ||
            _selectedPickingId.IsNone ||
            !IsSelectionKindEnabled(
                    _selectedPickingId.Kind))
        {
            return null;
        }

        var objectEntity =
            Scene.Objects
                .FirstOrDefault(
                    entity =>
                        entity.PickingId ==
                        _selectedPickingId);

        if (objectEntity is not null)
        {
            var item =
                objectEntity.Object;

            return new NativeSelectionInfo(
                PickingKind.Object,
                item.ObjectId,
                objectEntity.Tile.X,
                objectEntity.Tile.Y,
                item.SceneryObjectPath,
                item.X,
                item.Y,
                item.Z,
                item.Rotation,
                item.Pitch,
                item.Bank,
                null,
                null,
                null,
                null);
        }

        var splineEntity =
            Scene.Splines
                .FirstOrDefault(
                    entity =>
                        entity.PickingId ==
                        _selectedPickingId);

        if (splineEntity is null)
        {
            return null;
        }

        var spline =
            splineEntity.Spline;

        return new NativeSelectionInfo(
            PickingKind.Spline,
            spline.SplineId,
            splineEntity.Tile.X,
            splineEntity.Tile.Y,
            spline.SplinePath,
            spline.X,
            spline.Y,
            spline.Z,
            spline.Rotation,
            null,
            null,
            spline.Length,
            spline.Radius,
            spline.GradientStart,
            spline.GradientEnd,
            spline.PreviousSplineId,
            spline.NextSplineId,
            spline.IsHeightSpline);
    }

    public NativePendingTransformEdit?
        ApplySelectionInfo(
            NativeSelectionInfo values)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            values);

        CancelGizmoDrag();

        if (
            Scene is null ||
            _selectedPickingId.IsNone ||
            values.Kind !=
                _selectedPickingId.Kind)
        {
            return null;
        }

        NativeTransformHistoryEntry?
            history = null;

        if (
            values.Kind ==
            PickingKind.Object)
        {
            var entity =
                Scene.Objects
                    .FirstOrDefault(
                        item =>
                            item.PickingId ==
                            _selectedPickingId &&
                            item.Object.ObjectId ==
                            values.EntityId);

            if (entity is null)
            {
                return null;
            }

            var source =
                entity.Object;

            var updated =
                source with
                {
                    X = values.X,
                    Y = values.Y,
                    Z = values.Z,
                    Rotation =
                        values.Rotation,
                    Pitch =
                        values.Pitch ??
                        source.Pitch,
                    Bank =
                        values.Bank ??
                        source.Bank
                };

            var before =
                CreateObjectEdit(
                    entity.Tile,
                    source);

            var after =
                CreateObjectEdit(
                    entity.Tile,
                    updated);

            ReplaceObject(
                entity,
                updated);

            SelectObjectEdit(
                entity.Tile,
                after.ObjectEdit!);

            history =
                new NativeTransformHistoryEntry(
                    before,
                    after);
        }
        else if (
            values.Kind ==
            PickingKind.Spline)
        {
            var entity =
                Scene.Splines
                    .FirstOrDefault(
                        item =>
                            item.PickingId ==
                            _selectedPickingId &&
                            item.Spline.SplineId ==
                            values.EntityId);

            if (entity is null)
            {
                return null;
            }

            var source =
                entity.Spline;

            var updated =
                source with
                {
                    X = values.X,
                    Y = values.Y,
                    Z = values.Z,
                    Rotation =
                        values.Rotation,
                    Length =
                        values.Length ??
                        source.Length,
                    Radius =
                        values.Radius ??
                        source.Radius,
                    GradientStart =
                        values.GradientStart ??
                        source.GradientStart,
                    GradientEnd =
                        values.GradientEnd ??
                        source.GradientEnd
                };

            var before =
                CreateSplineEdit(
                    entity.Tile,
                    source);

            var after =
                CreateSplineEdit(
                    entity.Tile,
                    updated);

            ReplaceSpline(
                entity,
                updated);

            SelectSplineEdit(
                entity.Tile,
                after.SplineEdit!);

            history =
                new NativeTransformHistoryEntry(
                    before,
                    after);
        }

        if (history is null)
        {
            return null;
        }

        _undoStack.Push(
            history);

        _redoStack.Clear();

        PendingTransformEdit =
            history.After;

        RefreshSelectedScene();

        return
            PendingTransformEdit;
    }

    public NativePendingTransformEdit?
        LevelSelectedSplineToTerrain(
            out double startHeight,
            out double endHeight)
    {
        ThrowIfDisposed();

        startHeight = 0.0;
        endHeight = 0.0;

        if (
            Scene is null ||
            _selectedPickingId.Kind !=
                PickingKind.Spline)
        {
            return null;
        }

        var entity =
            Scene.Splines
                .FirstOrDefault(
                    item =>
                        item.PickingId ==
                        _selectedPickingId);

        if (
            entity is null ||
            entity.Spline.Length <=
                0.001)
        {
            return null;
        }

        var endFrame =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    entity.Spline.Length);

        if (
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    Scene,
                    entity.WorldX,
                    entity.WorldZ,
                    out startHeight) ||
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    Scene,
                    endFrame.Center.X,
                    endFrame.Center.Z,
                    out endHeight))
        {
            return null;
        }

        var gradient =
            (
                endHeight -
                startHeight
            ) /
            entity.Spline.Length *
            100.0;

        var info =
            GetSelectionInfo();

        if (info is null)
        {
            return null;
        }

        return ApplySelectionInfo(
            info with
            {
                Z = startHeight,
                GradientStart = gradient,
                GradientEnd = gradient
            });
    }

    public IReadOnlyList<
        NativeExplorerItem>
        GetExplorerItems()
    {
        if (Scene is null)
        {
            return Array.Empty<
                NativeExplorerItem>();
        }

        return
            Scene.Objects
                .Select(
                    entity =>
                        new NativeExplorerItem(
                            entity.PickingId,
                            PickingKind.Object,
                            entity.Object.ObjectId,
                            entity.Tile.X,
                            entity.Tile.Y,
                            entity.Object
                                .SceneryObjectPath))
                .Concat(
                    Scene.Splines
                        .Select(
                            entity =>
                                new NativeExplorerItem(
                                    entity.PickingId,
                                    PickingKind.Spline,
                                    entity.Spline.SplineId,
                                    entity.Tile.X,
                                    entity.Tile.Y,
                                    entity.Spline
                                        .SplinePath)))
                .OrderBy(
                    item =>
                        item.Kind)
                .ThenBy(
                    item =>
                        item.AssetPath,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ThenBy(
                    item =>
                        item.EntityId)
                .ToArray();
    }

    public NativeSelectionInfo?
        SelectExplorerItem(
            PickingId pickingId,
            bool focus)
    {
        ThrowIfDisposed();

        if (
            Scene is null ||
            pickingId.Kind is not
                (
                    PickingKind.Object or
                    PickingKind.Spline
                ) ||
            !IsSelectionKindEnabled(
                    pickingId.Kind))
        {
            return null;
        }

        var exists =
            pickingId.Kind ==
                PickingKind.Object
                ? Scene.Objects.Any(
                    item =>
                        item.PickingId ==
                        pickingId)
                : Scene.Splines.Any(
                    item =>
                        item.PickingId ==
                        pickingId);

        if (!exists)
        {
            return null;
        }

        _selectedPickingId =
            pickingId;

        MapRenderer.SetHover(
            PickingId.None);

        MapRenderer.SetSelection(
            pickingId);

        MapRenderer
            .SetSelectionPreviewTransform(
                Matrix4x4.Identity);

        if (
            focus &&
            TryGetSelectionAnchor(
                out var anchor))
        {
            Navigation.FocusOn(
                anchor,
                preferredDistance:
                    Math.Clamp(
                        Navigation.Distance *
                        0.35f,
                        35.0f,
                        180.0f));

            UpdateCameraTransform();
        }

        UpdateGizmoGeometry();
        RenderInitialFrame();

        return
            GetSelectionInfo();
    }

    public bool FocusSelection()
    {
        ThrowIfDisposed();

        if (
            _selectedPickingId.IsNone ||
            !TryGetSelectionAnchor(
                out var anchor))
        {
            return false;
        }

        Navigation.FocusOn(
            anchor,
            preferredDistance:
                Math.Clamp(
                    Navigation.Distance *
                    0.35f,
                    35.0f,
                    180.0f));

        UpdateCameraTransform();
        RenderInitialFrame();

        return true;
    }

    public bool FocusTile(
        int tileX,
        int tileY)
    {
        ThrowIfDisposed();

        if (
            Scene is null ||
            !Scene.Tiles.Any(
                tile =>
                    tile.Reference.X ==
                        tileX &&
                    tile.Reference.Y ==
                        tileY))
        {
            return false;
        }

        var worldX =
            tileX *
            300.0f +
            150.0f;

        var worldZ =
            tileY *
            300.0f +
            150.0f;

        var worldY =
            (float)
                NativeTerrainSampler
                    .GetHeightAtWorldPoint(
                        Scene,
                        worldX,
                        worldZ);

        Navigation.FocusOn(
            new Vector3(
                worldX,
                worldY,
                worldZ),
            preferredDistance:
                Math.Clamp(
                    Navigation.Distance *
                    0.55f,
                    80.0f,
                    450.0f));

        UpdateCameraTransform();
        RenderInitialFrame();

        return true;
    }

    public IntPtr SwapChainPointer =>
        Surface?.NativePointer ??
        IntPtr.Zero;

    public void EnsureSurface(
        uint width,
        uint height)
    {
        ThrowIfDisposed();

        if (Surface is null)
        {
            Surface =
                new D3D11SwapChainSurface(
                    Device,
                    width,
                    height);
        }
        else
        {
            Surface.Resize(
                width,
                height);
        }

        UpdateCameraTransform();
    }

    public async Task<
        NativeSceneSnapshot>
        LoadSceneAsync(
            IReadOnlyList<NativeSceneTile>
                tiles,
            OmsiMapDescriptor map,
            string omsiRoot,
            CancellationToken cancellationToken =
                default)
    {
        ThrowIfDisposed();

        _assetPreviewActive =
            false;

        _sceneryPlacementActive =
            false;

        _placementSceneryPath =
            null;

        _placementZOverride =
            null;

        _placementRotation =
            0;

        _placementPitch =
            0;

        _placementBank =
            0;

        _placementGeometry =
            null;

        _placementWorldPoint =
            null;

        _splinePlacementActive = false;
        _splinePlacementCurved = false;
        _placementSplinePath = null;
        _placementSplineAsset = null;
        _splineStartWorld = null;
        _splineEndWorld = null;
        _splinePointerWorld = null;
        _splinePlacementShape = null;
        _splinePlacementStage =
            NativeSplinePlacementStage.AwaitingStart;

        _mapDescriptor =
            map;

        _omsiRoot =
            omsiRoot;

        ApplySkyTexture();

        Scene =
            new NativeSceneBuilder()
                .Build(
                    tiles,
                    Picking);

        _selectedPickingId =
            PickingId.None;

        PendingTransformEdit =
            null;

        _undoStack.Clear();
        _redoStack.Clear();

        Navigation.FitToScene(
            Scene);

        UpdateCameraTransform();

        _sceneryAssets =
            await new NativeSceneryAssetLoader()
                .LoadAsync(
                    omsiRoot,
                    Scene,
                    cancellationToken)
                .ConfigureAwait(false);

        _splineAssets =
            await new NativeSplineAssetLoader()
                .LoadAsync(
                    omsiRoot,
                    Scene,
                    cancellationToken)
                .ConfigureAwait(false);

        UploadSceneGeometry();

        LoadedSceneryAssetCount =
            _sceneryAssets.Values.Count(
                asset =>
                    asset.IsLoaded);

        LoadedSplineAssetCount =
            _splineAssets.Values.Count(
                asset =>
                    asset.IsLoaded);

        return Scene;
    }

    public int LoadedSceneryAssetCount
    {
        get;
        private set;
    }

    public int LoadedObjectMeshCount
    {
        get;
        private set;
    }

    public int LoadedSplineAssetCount
    {
        get;
        private set;
    }

    public NativeAssetTechnicalSnapshot
        GetAssetTechnicalSnapshot()
    {
        var usedScenery =
            Scene?.Objects
                .Select(
                    item =>
                        item.Object
                            .SceneryObjectPath)
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase) ??
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);

        var usedSplines =
            Scene?.Splines
                .Select(
                    item =>
                        item.Spline
                            .SplinePath)
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase) ??
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);

        var loadedScenery =
            _sceneryAssets
                .Where(
                    pair =>
                        pair.Value
                            .IsLoaded)
                .Select(
                    pair =>
                        pair.Key)
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase);

        var treeScenery =
            _sceneryAssets
                .Where(
                    pair =>
                        pair.Value
                            .IsLoaded &&
                        pair.Value.Tree is
                            not null)
                .Select(
                    pair =>
                        pair.Key)
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase);

        var problemScenery =
            _sceneryAssets
                .Where(
                    pair =>
                        !pair.Value
                            .IsLoaded ||
                        pair.Value
                            .ErrorCode is
                            not null)
                .Select(
                    pair =>
                        pair.Key)
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase);

        var loadedSplines =
            _splineAssets
                .Where(
                    pair =>
                        pair.Value
                            .IsLoaded)
                .Select(
                    pair =>
                        pair.Key)
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase);

        var problemSplines =
            _splineAssets
                .Where(
                    pair =>
                        !pair.Value
                            .IsLoaded ||
                        pair.Value
                            .ErrorCode is
                            not null)
                .Select(
                    pair =>
                        pair.Key)
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase);

        return new NativeAssetTechnicalSnapshot(
            usedScenery,
            loadedScenery,
            treeScenery,
            problemScenery,
            usedSplines,
            loadedSplines,
            problemSplines);
    }

    public int LoadedSplineSurfaceCount
    {
        get;
        private set;
    }

    public int SceneryLightPointCount
    {
        get;
        private set;
    }

    public int TrafficLightProgramCount
    {
        get;
        private set;
    }

    public bool NightPreviewEnabled =>
        _nightPreviewEnabled;

    public bool HasSkyTexture =>
        MapRenderer.HasSkyTexture;

    public void SetNightPreview(
        bool enabled)
    {
        ThrowIfDisposed();

        _nightPreviewEnabled =
            enabled;

        MapRenderer.SetNightPreview(
            enabled);

        ApplySkyTexture();
        RenderInitialFrame();
    }

    public void SetSnapEnabled(
        bool enabled)
    {
        ThrowIfDisposed();

        CancelGizmoDrag();

        SnapEnabled =
            enabled;

        UpdateGizmoGeometry();
        RenderInitialFrame();
    }

    public void SetGizmoMode(
        NativeGizmoMode mode)
    {
        ThrowIfDisposed();

        if (
            GizmoMode ==
            mode)
        {
            return;
        }

        CancelGizmoDrag();

        GizmoMode =
            mode;

        UpdateGizmoGeometry();
        RenderInitialFrame();
    }

    public void Zoom(
        int wheelDelta)
    {
        ThrowIfDisposed();

        CancelGizmoDrag();

        Navigation.ZoomByWheel(
            wheelDelta);

        UpdateCameraTransform();
        RenderInitialFrame();
    }

    public void Pan(
        double deltaPixelX,
        double deltaPixelY)
    {
        ThrowIfDisposed();

        if (Surface is null)
        {
            return;
        }

        CancelGizmoDrag();

        Navigation.PanPixels(
            deltaPixelX,
            deltaPixelY,
            Surface.Width,
            Surface.Height);

        UpdateCameraTransform();
        RenderInitialFrame();
    }

    public void Orbit(
        double deltaPixelX,
        double deltaPixelY)
    {
        ThrowIfDisposed();

        CancelGizmoDrag();

        Navigation.OrbitPixels(
            deltaPixelX,
            deltaPixelY);

        UpdateCameraTransform();
        RenderInitialFrame();
    }

    public bool FitScene()
    {
        ThrowIfDisposed();

        if (Scene is null)
        {
            return false;
        }

        CancelGizmoDrag();

        Navigation.FitToScene(
            Scene);

        UpdateCameraTransform();
        RenderInitialFrame();

        return true;
    }

    public void ResetView()
    {
        ThrowIfDisposed();

        CancelGizmoDrag();

        Navigation.Reset();

        UpdateCameraTransform();
        RenderInitialFrame();
    }

    public void SetTopView()
    {
        ThrowIfDisposed();

        CancelGizmoDrag();

        Navigation.SetTopView();

        UpdateCameraTransform();
        RenderInitialFrame();
    }

    public void SetPerspectiveView()
    {
        ThrowIfDisposed();

        CancelGizmoDrag();

        Navigation.SetPerspectiveView();

        UpdateCameraTransform();
        RenderInitialFrame();
    }

    public void SetReferenceOverlay(
        NativeReferenceOverlayDefinition?
            overlay)
    {
        ThrowIfDisposed();

        _referenceOverlay =
            overlay;

        UploadReferenceOverlay();
        RenderInitialFrame();
    }

    public IReadOnlyList<
        NativeJunctionSuggestion>
        GetJunctionSuggestions()
    {
        if (Scene is null)
        {
            return Array.Empty<
                NativeJunctionSuggestion>();
        }

        return new NativeJunctionSuggestionBuilder()
            .Build(
                Scene);
    }

    public bool FocusWorldPoint(
        Vector3 point,
        float span = 45.0f)
    {
        ThrowIfDisposed();

        if (
            Scene is null ||
            !float.IsFinite(point.X) ||
            !float.IsFinite(point.Y) ||
            !float.IsFinite(point.Z))
        {
            return false;
        }

        var half =
            Math.Clamp(
                span,
                8.0f,
                300.0f) *
            0.5f;

        Navigation.FitToBounds(
            point -
                new Vector3(
                    half,
                    MathF.Max(
                        4.0f,
                        half *
                        0.2f),
                    half),
            point +
                new Vector3(
                    half,
                    MathF.Max(
                        4.0f,
                        half *
                        0.2f),
                    half));

        UpdateCameraTransform();
        RenderInitialFrame();

        return true;
    }

    public bool SetSceneVisibility(
        NativeSceneVisibility visibility)
    {
        ThrowIfDisposed();

        if (_sceneVisibility == visibility)
        {
            return false;
        }

        CancelGizmoDrag();

        _sceneVisibility =
            visibility;

        var selectionHidden =
            !_selectedPickingId.IsNone &&
            !IsSelectionKindEnabled(
                    _selectedPickingId.Kind);

        if (selectionHidden)
        {
            _selectedPickingId =
                PickingId.None;

            MapRenderer.SetSelection(
                PickingId.None);

            MapRenderer
                .SetSelectionPreviewTransform(
                    Matrix4x4.Identity);

            MapRenderer.SetGizmoGeometry(
                null);
        }

        MapRenderer.SetSceneVisibility(
            visibility);

        RenderInitialFrame();

        return true;
    }

    public bool SetTerrainPaintVisible(
        bool visible)
    {
        ThrowIfDisposed();

        if (
            !MapRenderer
                .SetTerrainPaintVisible(
                    visible))
        {
            return false;
        }

        RenderInitialFrame();

        return true;
    }

    public bool SetTerrainLayerVisible(
        int layerIndex,
        bool visible)
    {
        ThrowIfDisposed();

        if (
            !MapRenderer
                .SetTerrainLayerVisible(
                    layerIndex,
                    visible))
        {
            return false;
        }

        RenderInitialFrame();

        return true;
    }

    public bool ResetTerrainLayerVisibility()
    {
        ThrowIfDisposed();

        if (
            !MapRenderer
                .ResetTerrainLayerVisibility())
        {
            return false;
        }

        RenderInitialFrame();

        return true;
    }

    public bool SetSelectionFilter(
        NativeSelectionFilter filter)
    {
        ThrowIfDisposed();

        if (_selectionFilter == filter)
        {
            return false;
        }

        CancelGizmoDrag();

        _selectionFilter =
            filter;

        var selectionFiltered =
            !_selectedPickingId.IsNone &&
            !IsSelectionKindEnabled(
                _selectedPickingId.Kind);

        if (selectionFiltered)
        {
            _selectedPickingId =
                PickingId.None;

            MapRenderer.SetSelection(
                PickingId.None);

            MapRenderer
                .SetSelectionPreviewTransform(
                    Matrix4x4.Identity);

            MapRenderer.SetGizmoGeometry(
                null);
        }

        MapRenderer.SetSelectionFilter(
            filter);

        RenderInitialFrame();

        return true;
    }

    public bool SetSplineProfilesVisible(
        bool visible)
    {
        ThrowIfDisposed();

        if (
            !MapRenderer
                .SetSplineProfilesVisible(
                    visible))
        {
            return false;
        }

        RenderInitialFrame();

        return true;
    }

    public bool SetGridVisible(
        bool visible)
    {
        ThrowIfDisposed();

        if (
            !MapRenderer.SetGridVisible(
                visible))
        {
            return false;
        }

        RenderInitialFrame();

        return true;
    }

    public bool TryPick(
        uint pixelX,
        uint pixelY,
        out PickingId pickingId,
        out object? item)
    {
        ThrowIfDisposed();

        item = null;

        if (
            _assetPreviewActive ||
            _sceneryPlacementActive ||
            _splinePlacementActive)
        {
            pickingId =
                PickingId.None;

            return false;
        }

        pickingId =
            MapRenderer.Pick(
                pixelX,
                pixelY);

        var selectable =
            (
                pickingId.Kind is
                    PickingKind.Object or
                    PickingKind.Spline
            ) &&
            IsSelectionKindEnabled(
                    pickingId.Kind);

        var resolved =
            selectable &&
            Picking.TryResolve(
                pickingId,
                out item);

        if (!resolved)
        {
            item = null;
            pickingId =
                PickingId.None;
        }

        _selectedPickingId =
            pickingId;

        MapRenderer.SetSelection(
            pickingId);

        MapRenderer.SetSelectionPreviewTransform(
            Matrix4x4.Identity);

        UpdateGizmoGeometry();
        RenderInitialFrame();

        return resolved;
    }

    public bool TryBeginGizmoDrag(
        uint pixelX,
        uint pixelY,
        out NativeGizmoHandle handle)
    {
        ThrowIfDisposed();

        handle =
            NativeGizmoHandle.None;

        if (
            _assetPreviewActive ||
            _sceneryPlacementActive ||
            _splinePlacementActive ||
            Surface is null ||
            Scene is null ||
            _selectedPickingId.IsNone ||
            !IsSelectionKindEnabled(
                    _selectedPickingId.Kind))
        {
            return false;
        }

        var pickingId =
            MapRenderer.Pick(
                pixelX,
                pixelY);

        if (
            !NativeGizmoIds.TryGetHandle(
                pickingId,
                out handle) ||
            !IsHandleAllowed(
                handle))
        {
            handle =
                NativeGizmoHandle.None;

            return false;
        }

        if (
            !TryGetSelectionAnchor(
                out _dragAnchor))
        {
            handle =
                NativeGizmoHandle.None;

            return false;
        }

        _activeGizmoHandle =
            handle;

        _dragTranslation =
            Vector3.Zero;

        _dragRotationDegrees =
            0;

        _lastDragPixelX =
            pixelX;

        _lastDragPixelY =
            pixelY;

        MapRenderer.SetHover(
            PickingId.None);

        return true;
    }

    public void UpdateGizmoDrag(
        uint pixelX,
        uint pixelY)
    {
        ThrowIfDisposed();

        if (
            !IsManipulating ||
            Surface is null)
        {
            return;
        }

        var deltaX =
            (long)pixelX -
            _lastDragPixelX;

        var deltaY =
            (long)pixelY -
            _lastDragPixelY;

        _lastDragPixelX =
            pixelX;

        _lastDragPixelY =
            pixelY;

        if (
            _activeGizmoHandle is
                NativeGizmoHandle.MoveX or
                NativeGizmoHandle.MoveY or
                NativeGizmoHandle.MoveZ)
        {
            _dragTranslation +=
                NativeGizmoManipulationMath
                    .GetMoveDelta(
                        _activeGizmoHandle,
                        Navigation
                            .CameraPosition,
                        Navigation
                            .Target,
                        Navigation
                            .Distance,
                        Surface.Height,
                        deltaX,
                        deltaY);

            var effectiveTranslation =
                GetEffectiveTranslation();

            MapRenderer
                .SetSelectionPreviewTransform(
                    Matrix4x4
                        .CreateTranslation(
                            effectiveTranslation));
        }
        else
        {
            _dragRotationDegrees +=
                NativeGizmoManipulationMath
                    .GetRotationDeltaDegrees(
                        deltaX,
                        deltaY);

            var effectiveRotation =
                GetEffectiveRotation();

            MapRenderer
                .SetSelectionPreviewTransform(
                    NativeGizmoManipulationMath
                        .CreateRotationPreview(
                            _activeGizmoHandle,
                            _dragAnchor,
                            effectiveRotation));
        }

        UpdateGizmoGeometry();
        RenderInitialFrame();
    }

    public NativePendingTransformEdit?
        EndGizmoDrag()
    {
        ThrowIfDisposed();

        if (
            !IsManipulating ||
            Scene is null)
        {
            return null;
        }

        var handle =
            _activeGizmoHandle;

        _activeGizmoHandle =
            NativeGizmoHandle.None;

        var effectiveTranslation =
            GetEffectiveTranslation();

        var effectiveRotation =
            GetEffectiveRotation();

        if (
            effectiveTranslation.LengthSquared() <
                0.0000001f &&
            Math.Abs(
                effectiveRotation) <
                0.0001f)
        {
            MapRenderer
                .SetSelectionPreviewTransform(
                    Matrix4x4.Identity);

            UpdateGizmoGeometry();
            RenderInitialFrame();

            return null;
        }

        var history =
            ApplyManipulationToScene(
                handle,
                effectiveTranslation,
                effectiveRotation);

        if (history is not null)
        {
            _undoStack.Push(
                history);

            _redoStack.Clear();

            PendingTransformEdit =
                history.After;
        }
        else
        {
            PendingTransformEdit =
                null;
        }

        _dragTranslation =
            Vector3.Zero;

        _dragRotationDegrees =
            0;

        MapRenderer
            .SetSelectionPreviewTransform(
                Matrix4x4.Identity);

        UploadSceneGeometry();

        MapRenderer.SetSelection(
            _selectedPickingId);

        UpdateGizmoGeometry();
        RenderInitialFrame();

        return
            PendingTransformEdit;
    }

    public void CancelGizmoDrag()
    {
        if (!IsManipulating)
        {
            return;
        }

        _activeGizmoHandle =
            NativeGizmoHandle.None;

        _dragTranslation =
            Vector3.Zero;

        _dragRotationDegrees =
            0;

        MapRenderer
            .SetSelectionPreviewTransform(
                Matrix4x4.Identity);

        UpdateGizmoGeometry();
    }

    public NativePendingTransformEdit?
        Undo()
    {
        ThrowIfDisposed();

        CancelGizmoDrag();

        if (_undoStack.Count == 0)
        {
            return null;
        }

        var entry =
            _undoStack.Pop();

        if (
            !ApplyPendingTransformToScene(
                entry.Before))
        {
            _undoStack.Push(
                entry);

            return null;
        }

        _redoStack.Push(
            entry);

        PendingTransformEdit =
            entry.Before;

        RefreshSelectedScene();

        return
            PendingTransformEdit;
    }

    public NativePendingTransformEdit?
        Redo()
    {
        ThrowIfDisposed();

        CancelGizmoDrag();

        if (_redoStack.Count == 0)
        {
            return null;
        }

        var entry =
            _redoStack.Pop();

        if (
            !ApplyPendingTransformToScene(
                entry.After))
        {
            _redoStack.Push(
                entry);

            return null;
        }

        _undoStack.Push(
            entry);

        PendingTransformEdit =
            entry.After;

        RefreshSelectedScene();

        return
            PendingTransformEdit;
    }

    public bool UpdateHover(
        uint pixelX,
        uint pixelY)
    {
        ThrowIfDisposed();

        if (
            _assetPreviewActive ||
            _sceneryPlacementActive ||
            _splinePlacementActive ||
            IsManipulating)
        {
            return false;
        }

        var pickingId =
            MapRenderer.Pick(
                pixelX,
                pixelY);

        var selectable =
            (
                pickingId.Kind is
                    PickingKind.Object or
                    PickingKind.Spline
            ) &&
            IsSelectionKindEnabled(
                    pickingId.Kind);

        var resolved =
            selectable &&
            Picking.TryResolve(
                pickingId,
                out _);

        var changed =
            MapRenderer.SetHover(
                resolved
                    ? pickingId
                    : PickingId.None);

        if (changed)
        {
            RenderInitialFrame();
        }

        return resolved;
    }

    public void ClearHover()
    {
        ThrowIfDisposed();

        if (
            MapRenderer.SetHover(
                PickingId.None))
        {
            RenderInitialFrame();
        }
    }

    private bool IsSelectionKindEnabled(
        PickingKind kind)
    {
        if (kind == PickingKind.None)
        {
            return true;
        }

        return
            _sceneVisibility
                .IsPickingKindVisible(
                    kind) &&
            _selectionFilter
                .Allows(kind);
    }

    public void RenderInitialFrame()
    {
        ThrowIfDisposed();

        if (Surface is null)
        {
            return;
        }

        if (
            Scene is null &&
            !_assetPreviewActive)
        {
            Surface.ClearAndPresent(
                InitialClearColor);

            return;
        }

        MapRenderer.Render(
            Surface);
    }

    private void UploadSceneGeometry()
    {
        if (Scene is null)
        {
            return;
        }

        var objectGeometry =
            new NativeObjectTriangleGeometryBuilder()
                .Build(
                    Scene,
                    _sceneryAssets);

        var splineGeometry =
            new NativeSplineTriangleGeometryBuilder()
                .Build(
                    Scene,
                    _splineAssets);

        var proxyGeometry =
            new NativePickingProxyGeometryBuilder()
                .Build(
                    Scene,
                    _sceneryAssets);

        var terrainGeometry =
            new NativeTerrainTriangleGeometryBuilder()
                .Build(
                    Scene,
                    _mapDescriptor ??
                        throw new InvalidOperationException(
                            "Map descriptor is not loaded."),
                    _omsiRoot ??
                        throw new InvalidOperationException(
                            "OMSI root is not loaded."));

        var trafficPathGeometry =
            new NativeTrafficPathGeometryBuilder()
                .Build(
                    Scene,
                    _splineAssets,
                    _sceneryAssets);

        var sceneryLightGeometry =
            new NativeSceneryLightGeometryBuilder()
                .Build(
                    Scene,
                    _sceneryAssets);

        var waterGeometry =
            new NativeWaterTriangleGeometryBuilder()
                .Build(
                    Scene);

        MapRenderer.Upload(
            Scene,
            objectGeometry,
            proxyGeometry,
            terrainGeometry,
            splineGeometry,
            trafficPathGeometry,
            sceneryLightGeometry,
            waterGeometry);

        UploadReferenceOverlay();

        LoadedObjectMeshCount =
            objectGeometry
                .LoadedMeshCount;

        LoadedSplineSurfaceCount =
            splineGeometry
                .RenderedSurfaceCount;

        SceneryLightPointCount =
            sceneryLightGeometry
                .LightPointCount;

        TrafficLightProgramCount =
            _sceneryAssets.Values
                .SelectMany(
                    asset =>
                        asset
                            .TrafficLightControllers)
                .Sum(
                    controller =>
                        controller
                            .Programs.Count);
    }

    private void UploadReferenceOverlay()
    {
        if (
            Scene is null ||
            _referenceOverlay is null ||
            _assetPreviewActive)
        {
            MapRenderer
                .SetReferenceOverlay(
                    null);

            return;
        }

        var geometry =
            new NativeReferenceOverlayGeometryBuilder()
                .Build(
                    Scene,
                    _referenceOverlay);

        MapRenderer
            .SetReferenceOverlay(
                geometry);
    }

    private NativeTransformHistoryEntry?
        ApplyManipulationToScene(
            NativeGizmoHandle handle,
            Vector3 translation,
            float rotationDegrees)
    {
        if (
            Scene is null ||
            _selectedPickingId.IsNone)
        {
            return null;
        }

        var objectEntity =
            Scene.Objects
                .FirstOrDefault(
                    entity =>
                        entity.PickingId ==
                        _selectedPickingId);

        if (objectEntity is not null)
        {
            var source =
                objectEntity.Object;

            var updated =
                source with
                {
                    X =
                        source.X +
                        translation.X,
                    Y =
                        source.Y +
                        translation.Z,
                    Z =
                        source.Z +
                        translation.Y,
                    Rotation =
                        source.Rotation +
                        (
                            handle ==
                            NativeGizmoHandle.RotateY
                                ? rotationDegrees
                                : 0
                        ),
                    Pitch =
                        source.Pitch +
                        (
                            handle ==
                            NativeGizmoHandle.RotateX
                                ? rotationDegrees
                                : 0
                        ),
                    Bank =
                        source.Bank +
                        (
                            handle ==
                            NativeGizmoHandle.RotateZ
                                ? rotationDegrees
                                : 0
                        )
                };

            var objectBefore =
                CreateObjectEdit(
                    objectEntity.Tile,
                    source);

            var objectAfter =
                CreateObjectEdit(
                    objectEntity.Tile,
                    updated);

            ReplaceObject(
                objectEntity,
                updated);

            return
                new NativeTransformHistoryEntry(
                    objectBefore,
                    objectAfter);
        }

        var splineEntity =
            Scene.Splines
                .FirstOrDefault(
                    entity =>
                        entity.PickingId ==
                        _selectedPickingId);

        if (splineEntity is null)
        {
            return null;
        }

        var spline =
            splineEntity.Spline;

        var updatedSpline =
            spline with
            {
                X =
                    spline.X +
                    translation.X,
                Y =
                    spline.Y +
                    translation.Z,
                Z =
                    spline.Z +
                    translation.Y,
                Rotation =
                    spline.Rotation +
                    (
                        handle ==
                        NativeGizmoHandle.RotateY
                            ? rotationDegrees
                            : 0
                    )
            };

        var splineBefore =
            CreateSplineEdit(
                splineEntity.Tile,
                spline);

        var splineAfter =
            CreateSplineEdit(
                splineEntity.Tile,
                updatedSpline);

        ReplaceSpline(
            splineEntity,
            updatedSpline);

        return
            new NativeTransformHistoryEntry(
                splineBefore,
                splineAfter);
    }

    private NativePendingTransformEdit
        CreateObjectEdit(
            OmsiTileReference tile,
            OmsiPlacedObject item) =>
        new(
            tile,
            new OmsiObjectTransformEdit(
                item.SourceSectionOrdinal,
                item.SceneryObjectPath,
                item.ObjectId,
                item.X,
                item.Y,
                item.Z,
                item.Rotation,
                item.Pitch,
                item.Bank),
            null);

    private NativePendingTransformEdit
        CreateSplineEdit(
            OmsiTileReference tile,
            OmsiPlacedSpline item) =>
        new(
            tile,
            null,
            new OmsiSplineTransformEdit(
                item.SourceSectionOrdinal,
                item.SplinePath,
                item.SplineId,
                item.PreviousSplineId,
                item.NextSplineId,
                item.IsHeightSpline,
                item.X,
                item.Z,
                item.Y,
                item.Rotation,
                item.Length,
                item.Radius,
                item.GradientStart,
                item.GradientEnd));

    private bool ApplyPendingTransformToScene(
        NativePendingTransformEdit edit)
    {
        if (Scene is null)
        {
            return false;
        }

        if (
            edit.ObjectEdit is
                { } objectEdit)
        {
            var entity =
                Scene.Objects
                    .FirstOrDefault(
                        item =>
                            item.Tile.X ==
                                edit.Tile.X &&
                            item.Tile.Y ==
                                edit.Tile.Y &&
                            item.Object
                                .SourceSectionOrdinal ==
                                objectEdit
                                    .SourceSectionOrdinal &&
                            item.Object.ObjectId ==
                                objectEdit.ObjectId &&
                            string.Equals(
                                item.Object
                                    .SceneryObjectPath,
                                objectEdit
                                    .SceneryObjectPath,
                                StringComparison
                                    .OrdinalIgnoreCase));

            if (entity is null)
            {
                return false;
            }

            var updated =
                entity.Object with
                {
                    X = objectEdit.X,
                    Y = objectEdit.Y,
                    Z = objectEdit.Z,
                    Rotation =
                        objectEdit.Rotation,
                    Pitch =
                        objectEdit.Pitch,
                    Bank =
                        objectEdit.Bank
                };

            ReplaceObject(
                entity,
                updated);

            SelectObjectEdit(
                edit.Tile,
                objectEdit);

            return true;
        }

        if (
            edit.SplineEdit is not
                { } splineEdit)
        {
            return false;
        }

        var splineEntity =
            Scene.Splines
                .FirstOrDefault(
                    item =>
                        item.Tile.X ==
                            edit.Tile.X &&
                        item.Tile.Y ==
                            edit.Tile.Y &&
                        item.Spline
                            .SourceSectionOrdinal ==
                            splineEdit
                                .SourceSectionOrdinal &&
                        item.Spline.SplineId ==
                            splineEdit.SplineId &&
                        string.Equals(
                            item.Spline
                                .SplinePath,
                            splineEdit
                                .SplinePath,
                            StringComparison
                                .OrdinalIgnoreCase));

        if (splineEntity is null)
        {
            return false;
        }

        var updatedSpline =
            splineEntity.Spline with
            {
                X = splineEdit.X,
                Z = splineEdit.Z,
                Y = splineEdit.Y,
                Rotation =
                    splineEdit.Rotation,
                Length =
                    splineEdit.Length,
                Radius =
                    splineEdit.Radius,
                GradientStart =
                    splineEdit
                        .GradientStart,
                GradientEnd =
                    splineEdit
                        .GradientEnd
            };

        ReplaceSpline(
            splineEntity,
            updatedSpline);

        SelectSplineEdit(
            edit.Tile,
            splineEdit);

        return true;
    }

    private void SelectObjectEdit(
        OmsiTileReference tile,
        OmsiObjectTransformEdit edit)
    {
        if (Scene is null)
        {
            return;
        }

        _selectedPickingId =
            Scene.Objects
                .FirstOrDefault(
                    item =>
                        item.Tile.X ==
                            tile.X &&
                        item.Tile.Y ==
                            tile.Y &&
                        item.Object
                            .SourceSectionOrdinal ==
                            edit
                                .SourceSectionOrdinal &&
                        item.Object.ObjectId ==
                            edit.ObjectId)
                ?.PickingId ??
            PickingId.None;
    }

    private void SelectSplineEdit(
        OmsiTileReference tile,
        OmsiSplineTransformEdit edit)
    {
        if (Scene is null)
        {
            return;
        }

        _selectedPickingId =
            Scene.Splines
                .FirstOrDefault(
                    item =>
                        item.Tile.X ==
                            tile.X &&
                        item.Tile.Y ==
                            tile.Y &&
                        item.Spline
                            .SourceSectionOrdinal ==
                            edit
                                .SourceSectionOrdinal &&
                        item.Spline.SplineId ==
                            edit.SplineId)
                ?.PickingId ??
            PickingId.None;
    }

    private void RefreshSelectedScene()
    {
        MapRenderer
            .SetSelectionPreviewTransform(
                Matrix4x4.Identity);

        UploadSceneGeometry();

        MapRenderer.SetSelection(
            _selectedPickingId);

        UpdateGizmoGeometry();
        RenderInitialFrame();
    }

    private Vector3 GetEffectiveTranslation() =>
        SnapEnabled
            ? NativeGizmoManipulationMath
                .SnapTranslation(
                    _dragTranslation,
                    MoveSnapMeters)
            : _dragTranslation;

    private float GetEffectiveRotation() =>
        SnapEnabled
            ? NativeGizmoManipulationMath
                .SnapRotation(
                    _dragRotationDegrees,
                    RotateSnapDegrees)
            : _dragRotationDegrees;

    private void ReplaceObject(
        NativeObjectEntity entity,
        OmsiPlacedObject updated)
    {
        if (Scene is null)
        {
            return;
        }

        var tiles =
            Scene.Tiles
                .Select(
                    tile =>
                    {
                        if (
                            tile.Reference.X !=
                                entity.Tile.X ||
                            tile.Reference.Y !=
                                entity.Tile.Y)
                        {
                            return tile;
                        }

                        var objects =
                            tile.Content.Objects
                                .Select(
                                    item =>
                                        MatchesObject(
                                            item,
                                            entity.Object)
                                            ? updated
                                            : item)
                                .ToArray();

                        return
                            tile with
                            {
                                Content =
                                    tile.Content with
                                    {
                                        Objects =
                                            objects
                                    }
                            };
                    })
                .ToArray();

        Scene =
            new NativeSceneBuilder()
                .Build(
                    tiles,
                    Picking);
    }

    private void ReplaceSpline(
        NativeSplineEntity entity,
        OmsiPlacedSpline updated)
    {
        if (Scene is null)
        {
            return;
        }

        var tiles =
            Scene.Tiles
                .Select(
                    tile =>
                    {
                        if (
                            tile.Reference.X !=
                                entity.Tile.X ||
                            tile.Reference.Y !=
                                entity.Tile.Y)
                        {
                            return tile;
                        }

                        var splines =
                            tile.Content.Splines
                                .Select(
                                    item =>
                                        MatchesSpline(
                                            item,
                                            entity.Spline)
                                            ? updated
                                            : item)
                                .ToArray();

                        return
                            tile with
                            {
                                Content =
                                    tile.Content with
                                    {
                                        Splines =
                                            splines
                                    }
                            };
                    })
                .ToArray();

        Scene =
            new NativeSceneBuilder()
                .Build(
                    tiles,
                    Picking);
    }

    private static bool MatchesObject(
        OmsiPlacedObject candidate,
        OmsiPlacedObject selected) =>
        ReferenceEquals(
            candidate,
            selected) ||
        (
            candidate.SourceSectionOrdinal ==
                selected.SourceSectionOrdinal &&
            candidate.ObjectId ==
                selected.ObjectId &&
            string.Equals(
                candidate.SceneryObjectPath,
                selected.SceneryObjectPath,
                StringComparison.OrdinalIgnoreCase)
        );

    private static bool MatchesSpline(
        OmsiPlacedSpline candidate,
        OmsiPlacedSpline selected) =>
        ReferenceEquals(
            candidate,
            selected) ||
        (
            candidate.SourceSectionOrdinal ==
                selected.SourceSectionOrdinal &&
            candidate.SplineId ==
                selected.SplineId &&
            string.Equals(
                candidate.SplinePath,
                selected.SplinePath,
                StringComparison.OrdinalIgnoreCase)
        );

    private bool IsHandleAllowed(
        NativeGizmoHandle handle)
    {
        if (
            GizmoMode ==
            NativeGizmoMode.Move)
        {
            return
                handle is
                    NativeGizmoHandle.MoveX or
                    NativeGizmoHandle.MoveY or
                    NativeGizmoHandle.MoveZ;
        }

        if (
            _selectedPickingId.Kind ==
            PickingKind.Spline)
        {
            return
                handle ==
                NativeGizmoHandle.RotateY;
        }

        return
            handle is
                NativeGizmoHandle.RotateX or
                NativeGizmoHandle.RotateY or
                NativeGizmoHandle.RotateZ;
    }

    private bool TryGetSelectionAnchor(
        out Vector3 anchor)
    {
        anchor =
            Vector3.Zero;

        if (
            Scene is null ||
            _selectedPickingId.IsNone)
        {
            return false;
        }

        var objectEntity =
            Scene.Objects
                .FirstOrDefault(
                    entity =>
                        entity.PickingId ==
                        _selectedPickingId);

        if (objectEntity is not null)
        {
            var usesAbsoluteHeight =
                _sceneryAssets
                    .TryGetValue(
                        objectEntity.Object
                            .SceneryObjectPath,
                        out var asset) &&
                asset.UsesAbsoluteHeight;

            var terrainOffset =
                usesAbsoluteHeight
                    ? 0.0
                    : NativeTerrainSampler
                        .GetHeightAtObject(
                            Scene,
                            objectEntity);

            anchor =
                new Vector3(
                    objectEntity.WorldX,
                    objectEntity.WorldY +
                        (float)
                            terrainOffset,
                    objectEntity.WorldZ);

            return true;
        }

        var splineEntity =
            Scene.Splines
                .FirstOrDefault(
                    entity =>
                        entity.PickingId ==
                        _selectedPickingId);

        if (splineEntity is null)
        {
            return false;
        }

        anchor =
            new Vector3(
                splineEntity.WorldX,
                splineEntity.WorldY,
                splineEntity.WorldZ);

        return true;
    }

    private void UpdateGizmoGeometry()
    {
        if (
            _assetPreviewActive ||
            _sceneryPlacementActive)
        {
            MapRenderer.SetGizmoGeometry(
                null);

            return;
        }

        if (
            _selectedPickingId.IsNone ||
            !TryGetSelectionAnchor(
                out var anchor))
        {
            MapRenderer
                .SetGizmoGeometry(
                    null);

            return;
        }

        if (
            IsManipulating &&
            _activeGizmoHandle is
                NativeGizmoHandle.MoveX or
                NativeGizmoHandle.MoveY or
                NativeGizmoHandle.MoveZ)
        {
            anchor =
                _dragAnchor +
                GetEffectiveTranslation();
        }

        var size =
            Math.Clamp(
                Navigation.Distance *
                0.065f,
                4.0f,
                120.0f);

        var geometry =
            new NativeGizmoGeometryBuilder()
                .Build(
                    anchor,
                    GizmoMode,
                    size,
                    fullRotation:
                        _selectedPickingId
                            .Kind ==
                        PickingKind.Object);

        MapRenderer.SetGizmoGeometry(
            geometry);
    }

    private bool TryGetTerrainPlacementPoint(
        uint pixelX,
        uint pixelY,
        out Vector3 point)
    {
        point = default;

        if (
            Scene is null ||
            Surface is null ||
            !Navigation.TryGetWorldRay(
                pixelX,
                pixelY,
                Surface.Width,
                Surface.Height,
                out var origin,
                out var direction) ||
            Math.Abs(direction.Y) < 0.00001f)
        {
            return false;
        }

        var height =
            Navigation.Target.Y;

        for (
            var iteration = 0;
            iteration < 4;
            iteration++)
        {
            var distance =
                (height - origin.Y) /
                direction.Y;

            if (distance <= 0)
            {
                return false;
            }

            point =
                origin +
                direction *
                distance;

            height =
                (float)
                    NativeTerrainSampler
                        .GetHeightAtWorldPoint(
                            Scene,
                            point.X,
                            point.Z);
        }

        if (SnapEnabled)
        {
            point.X =
                MathF.Round(
                    point.X /
                    MoveSnapMeters) *
                MoveSnapMeters;

            point.Z =
                MathF.Round(
                    point.Z /
                    MoveSnapMeters) *
                MoveSnapMeters;

            height =
                (float)
                    NativeTerrainSampler
                        .GetHeightAtWorldPoint(
                            Scene,
                            point.X,
                            point.Z);
        }

        point.Y = height;

        if (
            _splinePlacementActive &&
            SnapEnabled)
        {
            var endpointSnapDistance =
                Math.Clamp(
                    Navigation.Distance *
                    0.003f,
                    0.5f,
                    3.0f);

            if (
                NativeSplineEndpointSnapper
                    .TrySnap(
                        Scene,
                        point,
                        endpointSnapDistance,
                        out var snapped))
            {
                point =
                    snapped;
            }
        }

        var tileX =
            (int)Math.Floor(
                point.X /
                300.0f);

        var tileY =
            (int)Math.Floor(
                point.Z /
                300.0f);

        if (
            !Scene.Tiles.Any(
                tile =>
                    tile.Reference.X == tileX &&
                    tile.Reference.Y == tileY))
        {
            return false;
        }

        return true;
    }

    public bool TryGetTerrainEditPoint(
        uint pixelX,
        uint pixelY,
        out NativeTerrainEditPoint?
            editPoint)
    {
        ThrowIfDisposed();

        editPoint =
            null;

        if (
            Scene is null ||
            !TryGetTerrainPlacementPoint(
                pixelX,
                pixelY,
                out var point))
        {
            return false;
        }

        var tileX =
            (int)Math.Floor(
                point.X /
                300.0f);

        var tileY =
            (int)Math.Floor(
                point.Z /
                300.0f);

        var tile =
            Scene.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            tileX &&
                        item.Reference.Y ==
                            tileY);

        if (tile is null)
        {
            return false;
        }

        editPoint =
            new NativeTerrainEditPoint(
                tile.Reference,
                point.X -
                    tileX *
                    300.0,
                point.Z -
                    tileY *
                    300.0,
                point.Y,
                point);

        return true;
    }

    private NativeSplinePlacementRequest?
        CreateSplinePlacementRequest(
            NativeSplinePlacementShape shape)
    {
        if (
            Scene is null ||
            string.IsNullOrWhiteSpace(
                _placementSplinePath))
        {
            return null;
        }

        var tileX =
            (int)Math.Floor(
                shape.Start.X /
                300.0f);

        var tileY =
            (int)Math.Floor(
                shape.Start.Z /
                300.0f);

        var tile =
            Scene.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X == tileX &&
                        item.Reference.Y == tileY);

        if (tile is null)
        {
            return null;
        }

        return new NativeSplinePlacementRequest(
            tile.Reference,
            _placementSplinePath,
            _splinePreviousId,
            shape.Start.X -
                tileX *
                300.0,
            shape.Start.Z -
                tileY *
                300.0,
            shape.Start.Y,
            shape.Rotation,
            shape.Length,
            shape.Radius,
            shape.GradientStart,
            shape.GradientEnd,
            shape.IsCurved,
            shape.Start,
            shape.End,
            _splineNextId,
            _splinePlacementIsHeight);
    }

    private static bool TryResolveIndexedAssetPath(
        string root,
        string relativePath,
        out string fullPath)
    {
        fullPath =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                root) ||
            string.IsNullOrWhiteSpace(
                relativePath) ||
            Path.IsPathRooted(
                relativePath))
        {
            return false;
        }

        try
        {
            var normalizedRoot =
                Path.GetFullPath(
                        root)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            var rootPrefix =
                normalizedRoot +
                Path.DirectorySeparatorChar;

            var normalizedRelative =
                relativePath
                    .Replace(
                        Path.AltDirectorySeparatorChar,
                        Path.DirectorySeparatorChar)
                    .TrimStart(
                        Path.DirectorySeparatorChar);

            var candidate =
                Path.GetFullPath(
                    Path.Combine(
                        normalizedRoot,
                        normalizedRelative));

            if (
                !candidate.StartsWith(
                    rootPrefix,
                    StringComparison
                        .OrdinalIgnoreCase) ||
                !File.Exists(
                    candidate))
            {
                return false;
            }

            fullPath =
                candidate;

            return true;
        }
        catch (
            Exception exception)
            when (
                exception is
                    ArgumentException or
                    NotSupportedException or
                    PathTooLongException or
                    IOException or
                    UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void ApplySkyTexture()
    {
        if (
            string.IsNullOrWhiteSpace(
                _omsiRoot))
        {
            MapRenderer.SetSkyTexture(
                null);

            return;
        }

        var fileName =
            _nightPreviewEnabled
                ? "himmel05.bmp"
                : "himmel01.bmp";

        var enhancedFileName =
            _nightPreviewEnabled
                ? "night01.bmp"
                : "day01.bmp";

        var primary =
            Path.Combine(
                _omsiRoot,
                "Texture",
                fileName);

        string? resolved =
            File.Exists(primary)
                ? primary
                : null;

        if (resolved is null)
        {
            var enhanced =
                Path.Combine(
                    _omsiRoot,
                    "Texture",
                    "skybox",
                    enhancedFileName);

            if (File.Exists(enhanced))
            {
                resolved =
                    enhanced;
            }
        }

        MapRenderer.SetSkyTexture(
            resolved);
    }

    private void UpdateCameraTransform()
    {
        if (Surface is null)
        {
            return;
        }

        MapRenderer.SetViewProjection(
            Navigation
                .GetViewProjection(
                    Surface.Width,
                    Surface.Height),
            Navigation
                .CameraPosition);

        UpdateGizmoGeometry();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException
            .ThrowIf(
                _disposed,
                this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Surface?.Dispose();
        Surface = null;

        Scene = null;
        Picking.Clear();
        MapRenderer.Dispose();
        Device.Dispose();
    }
}
