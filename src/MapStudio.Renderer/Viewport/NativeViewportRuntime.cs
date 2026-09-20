using System.Numerics;
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

    private PickingId _selectedPickingId =
        PickingId.None;

    private NativeGizmoHandle _activeGizmoHandle =
        NativeGizmoHandle.None;

    private Vector3 _dragAnchor;
    private Vector3 _dragTranslation;
    private float _dragRotationDegrees;
    private uint _lastDragPixelX;
    private uint _lastDragPixelY;
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

    public bool IsManipulating =>
        _activeGizmoHandle !=
        NativeGizmoHandle.None;

    public bool IsDisposed => _disposed;

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

        Scene =
            new NativeSceneBuilder()
                .Build(
                    tiles,
                    Picking);

        _selectedPickingId =
            PickingId.None;

        PendingTransformEdit =
            null;

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

            MapRenderer
                .SetSelectionPreviewTransform(
                    Matrix4x4
                        .CreateTranslation(
                            _dragTranslation));
        }
        else
        {
            _dragRotationDegrees +=
                NativeGizmoManipulationMath
                    .GetRotationDeltaDegrees(
                        deltaX,
                        deltaY);

            MapRenderer
                .SetSelectionPreviewTransform(
                    NativeGizmoManipulationMath
                        .CreateRotationPreview(
                            _activeGizmoHandle,
                            _dragAnchor,
                            _dragRotationDegrees));
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

        if (
            _dragTranslation.LengthSquared() <
                0.0000001f &&
            Math.Abs(
                _dragRotationDegrees) <
                0.0001f)
        {
            MapRenderer
                .SetSelectionPreviewTransform(
                    Matrix4x4.Identity);

            UpdateGizmoGeometry();
            RenderInitialFrame();

            return null;
        }

        PendingTransformEdit =
            ApplyManipulationToScene(
                handle,
                _dragTranslation,
                _dragRotationDegrees);

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

    public bool UpdateHover(
        uint pixelX,
        uint pixelY)
    {
        ThrowIfDisposed();

        if (IsManipulating)
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

        if (Scene is null)
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

    private NativePendingTransformEdit?
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

            ReplaceObject(
                objectEntity,
                updated);

            return new NativePendingTransformEdit(
                objectEntity.Tile,
                new OmsiObjectTransformEdit(
                    updated
                        .SourceSectionOrdinal,
                    updated
                        .SceneryObjectPath,
                    updated
                        .ObjectId,
                    updated.X,
                    updated.Y,
                    updated.Z,
                    updated.Rotation,
                    updated.Pitch,
                    updated.Bank),
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

        ReplaceSpline(
            splineEntity,
            updatedSpline);

        return new NativePendingTransformEdit(
            splineEntity.Tile,
            null,
            new OmsiSplineTransformEdit(
                updatedSpline
                    .SourceSectionOrdinal,
                updatedSpline
                    .SplinePath,
                updatedSpline
                    .SplineId,
                updatedSpline
                    .PreviousSplineId,
                updatedSpline
                    .NextSplineId,
                updatedSpline
                    .IsHeightSpline,
                updatedSpline.X,
                updatedSpline.Z,
                updatedSpline.Y,
                updatedSpline.Rotation,
                updatedSpline.Length,
                updatedSpline.Radius,
                updatedSpline
                    .GradientStart,
                updatedSpline
                    .GradientEnd));
    }

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
                _dragTranslation;
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
