using MapStudio.Renderer.Graphics;
using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Viewport;

public sealed class NativeViewportRuntime : IDisposable
{
    private bool _disposed;

    public NativeViewportRuntime()
    {
        Device = new D3D11DeviceHost();
    }

    public D3D11DeviceHost Device { get; }

    public PickingRegistry<object> Picking { get; } =
        new();

    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Picking.Clear();
        Device.Dispose();
    }
}
