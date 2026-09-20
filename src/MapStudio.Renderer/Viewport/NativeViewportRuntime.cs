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

        Navigation.FitToScene(
            Scene);

        UpdateCameraTransform();

        var assets =
            await new NativeSceneryAssetLoader()
                .LoadAsync(
                    omsiRoot,
                    Scene,
                    cancellationToken)
                .ConfigureAwait(false);

        var objectGeometry =
            new NativeObjectTriangleGeometryBuilder()
                .Build(
                    Scene,
                    assets);

        var splineAssets =
            await new NativeSplineAssetLoader()
                .LoadAsync(
                    omsiRoot,
                    Scene,
                    cancellationToken)
                .ConfigureAwait(false);

        var splineGeometry =
            new NativeSplineTriangleGeometryBuilder()
                .Build(
                    Scene,
                    splineAssets);

        var proxyGeometry =
            new NativePickingProxyGeometryBuilder()
                .Build(
                    Scene,
                    assets);

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

        LoadedSceneryAssetCount =
            assets.Values.Count(
                asset =>
                    asset.IsLoaded);

        LoadedObjectMeshCount =
            objectGeometry
                .LoadedMeshCount;

        LoadedSplineAssetCount =
            splineAssets.Values.Count(
                asset =>
                    asset.IsLoaded);

        LoadedSplineSurfaceCount =
            splineGeometry
                .RenderedSurfaceCount;

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

    public void Zoom(
        int wheelDelta)
    {
        ThrowIfDisposed();

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

        Navigation.OrbitPixels(
            deltaPixelX,
            deltaPixelY);

        UpdateCameraTransform();
        RenderInitialFrame();
    }

    public void ResetView()
    {
        ThrowIfDisposed();

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

        var resolved =
            Picking.TryResolve(
                pickingId,
                out item);

        MapRenderer.SetSelection(
            resolved
                ? pickingId
                : PickingId.None);

        RenderInitialFrame();

        return resolved;
    }

    public bool UpdateHover(
        uint pixelX,
        uint pixelY)
    {
        ThrowIfDisposed();

        var pickingId =
            MapRenderer.Pick(
                pixelX,
                pixelY);

        var resolved =
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
