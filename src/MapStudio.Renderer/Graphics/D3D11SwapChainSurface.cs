using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace MapStudio.Renderer.Graphics;

public sealed class D3D11SwapChainSurface : IDisposable
{
    private const uint BufferCount = 2;
    private static readonly Format ColorFormat =
        Format.B8G8R8A8_UNorm;

    private readonly D3D11DeviceHost _deviceHost;
    private readonly IDXGIFactory2 _factory;

    private ID3D11Texture2D? _backBuffer;
    private ID3D11RenderTargetView? _renderTargetView;
    private bool _disposed;

    public D3D11SwapChainSurface(
        D3D11DeviceHost deviceHost,
        uint width,
        uint height)
    {
        ArgumentNullException.ThrowIfNull(
            deviceHost);

        _deviceHost = deviceHost;
        _factory =
            DXGI.CreateDXGIFactory2<
                IDXGIFactory2>(
                debug: false);

        Width = Math.Max(1u, width);
        Height = Math.Max(1u, height);

        var description =
            new SwapChainDescription1
            {
                Width = Width,
                Height = Height,
                Format = ColorFormat,
                Stereo = false,
                SampleDescription =
                    new SampleDescription(
                        1,
                        0),
                BufferUsage =
                    Usage.RenderTargetOutput,
                BufferCount =
                    BufferCount,
                Scaling = Scaling.Stretch,
                SwapEffect =
                    SwapEffect.FlipSequential,
                AlphaMode =
                    AlphaMode.Ignore,
                Flags =
                    SwapChainFlags.None
            };

        SwapChain =
            _factory
                .CreateSwapChainForComposition(
                    _deviceHost.Device,
                    description);

        CreateBackBufferResources();
    }

    public IDXGISwapChain1 SwapChain { get; }

    public IntPtr NativePointer =>
        SwapChain.NativePointer;

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public void Resize(
        uint width,
        uint height)
    {
        ThrowIfDisposed();

        width = Math.Max(1u, width);
        height = Math.Max(1u, height);

        if (
            width == Width &&
            height == Height)
        {
            return;
        }

        ReleaseBackBufferResources();

        _deviceHost.Context
            .UnsetRenderTargets();

        _deviceHost.Context.Flush();

        SwapChain.ResizeBuffers(
            BufferCount,
            width,
            height,
            ColorFormat,
            SwapChainFlags.None)
            .CheckError();

        Width = width;
        Height = height;

        CreateBackBufferResources();
    }

    public void ClearAndPresent(
        Color4 color)
    {
        ThrowIfDisposed();

        var renderTarget =
            _renderTargetView ??
            throw new InvalidOperationException(
                "The native viewport does not have a render target.");

        _deviceHost.Context
            .OMSetRenderTargets(
                renderTarget);

        _deviceHost.Context
            .RSSetViewport(
                0,
                0,
                Width,
                Height);

        _deviceHost.Context
            .ClearRenderTargetView(
                renderTarget,
                color);

        SwapChain.Present(
            1,
            PresentFlags.None)
            .CheckError();
    }

    private void CreateBackBufferResources()
    {
        _backBuffer =
            SwapChain
                .GetBuffer<
                    ID3D11Texture2D>(
                    0);

        _renderTargetView =
            _deviceHost.Device
                .CreateRenderTargetView(
                    _backBuffer);
    }

    private void ReleaseBackBufferResources()
    {
        _renderTargetView?.Dispose();
        _renderTargetView = null;

        _backBuffer?.Dispose();
        _backBuffer = null;
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

        _deviceHost.Context
            .UnsetRenderTargets();

        ReleaseBackBufferResources();
        SwapChain.Dispose();
        _factory.Dispose();
    }
}
