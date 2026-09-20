using System.Numerics;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;
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
    private double _placementPitch;
    private double _placementBank;
    private NativeAssetPreviewGeometry?
        _placementGeometry;
    private Vector3? _placementWorldPoint;

    private bool _splinePlacementActive;
    private bool _splinePlacementCurved;
    private string? _placementSplinePath;
    private NativeSplineAsset? _placementSplineAsset;
    private Vector3? _splineStartWorld;
    private Vector3? _splineEndWorld;
    private Vector3? _splinePointerWorld;
    private NativeSplinePlacementShape? _splinePlacementShape;
    private int _splinePreviousId =
        -1;
    private NativeSplinePlacementStage _splinePlacementStage =
        NativeSplinePlacementStage.AwaitingStart;

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
            !TryGetTerrainPlacementPoint(
                pixelX,
                pixelY,
                out var point))
        {
            return false;
        }

        _splinePointerWorld = point;

        NativeSplinePlacementShape? shape = null;

        if (
            _splinePlacementStage ==
                NativeSplinePlacementStage.AwaitingEnd &&
            _splineStartWorld is { } start)
        {
            NativeSplinePlacementMath.TryCreateStraight(
                start,
                point,
                out shape);
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
            _splineStartWorld = point;
            _splinePlacementStage =
                NativeSplinePlacementStage.AwaitingEnd;

            status =
                _splinePlacementCurved
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
            if (
                _splineStartWorld is not { } start ||
                !NativeSplinePlacementMath.TryCreateStraight(
                    start,
                    point,
                    out var straight) ||
                straight is null)
            {
                status = "Ponto final inválido.";
                return false;
            }

            if (_splinePlacementCurved)
            {
                _splineEndWorld = point;
                _splinePlacementStage =
                    NativeSplinePlacementStage.AwaitingCurve;
                _splinePlacementShape = straight;

                status =
                    "Final definido. Mova o cursor para curvar e clique para confirmar.";

                return true;
            }

            request =
                CreateSplinePlacementRequest(
                    straight);

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

        status = "Curva inválida.";
        return false;
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
        _splinePlacementStage =
            NativeSplinePlacementStage.AwaitingStart;

        MapRenderer.SetPlacementPreview(
            null,
            Matrix4x4.Identity);

        UpdateGizmoGeometry();
        RenderInitialFrame();
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
                kind ==
                    OmsiAssetKind.SceneryObject
                    ? 1
                    : 0,
                preview.SourceMeshCount);

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
            null);
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
                ))
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

    public int LoadedSplineSurfaceCount
    {
        get;
        private set;
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

    public void ResetView()
    {
        ThrowIfDisposed();

        CancelGizmoDrag();

        Navigation.Reset();

        UpdateCameraTransform();
        RenderInitialFrame();
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
            pickingId.Kind is
                PickingKind.Object or
                PickingKind.Spline;

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
            _selectedPickingId.IsNone)
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
            pickingId.Kind is
                PickingKind.Object or
                PickingKind.Spline;

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
                    Scene);

        MapRenderer.Upload(
            Scene,
            objectGeometry,
            proxyGeometry,
            terrainGeometry,
            splineGeometry);

        LoadedObjectMeshCount =
            objectGeometry
                .LoadedMeshCount;

        LoadedSplineSurfaceCount =
            splineGeometry
                .RenderedSurfaceCount;
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
            shape.End);
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
