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

            return;
        }

        Surface.Resize(
            width,
            height);
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

        MapRenderer.Upload(
            Scene,
            objectGeometry);

        LoadedSceneryAssetCount =
            assets.Values.Count(
                asset =>
                    asset.IsLoaded);

        LoadedObjectMeshCount =
            objectGeometry
                .LoadedMeshCount;

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

    public void RenderInitialFrame()
    {
        ThrowIfDisposed();

        if (Surface is null)
        {
            return;
        }

        if (
            Scene is null ||
            MapRenderer.VertexCount == 0)
        {
            Surface.ClearAndPresent(
                InitialClearColor);

            return;
        }

        MapRenderer.Render(
            Surface);
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
