using MapStudio.Renderer.Graphics;
using MapStudio.Renderer.Picking;
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
    }

    public D3D11DeviceHost Device { get; }

    public D3D11SwapChainSurface? Surface { get; private set; }

    public PickingRegistry<object> Picking { get; } =
        new();

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

    public void RenderInitialFrame()
    {
        ThrowIfDisposed();

        Surface?.ClearAndPresent(
            InitialClearColor);
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

        Picking.Clear();
        Device.Dispose();
    }
}
