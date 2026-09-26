using System.Runtime.InteropServices;
using MapStudio.Renderer.Picking;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace MapStudio.Renderer.Graphics;

public sealed class D3D11PickingSurface :
    IDisposable
{
    private readonly D3D11DeviceHost
        _deviceHost;

    private ID3D11Texture2D?
        _renderTexture;

    private ID3D11RenderTargetView?
        _renderTargetView;

    private ID3D11Texture2D?
        _stagingTexture;

    private ID3D11Texture2D?
        _depthTexture;

    private ID3D11DepthStencilView?
        _depthStencilView;

    private bool _disposed;

    public D3D11PickingSurface(
        D3D11DeviceHost deviceHost)
    {
        ArgumentNullException.ThrowIfNull(
            deviceHost);

        _deviceHost = deviceHost;
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public void EnsureSize(
        uint width,
        uint height)
    {
        ThrowIfDisposed();

        width = Math.Max(1u, width);
        height = Math.Max(1u, height);

        if (
            width == Width &&
            height == Height &&
            _renderTexture is not null)
        {
            return;
        }

        ReleaseResources();

        _renderTexture =
            _deviceHost.Device
                .CreateTexture2D(
                    Format.R8G8B8A8_UNorm,
                    width,
                    height,
                    mipLevels: 1,
                    bindFlags:
                        BindFlags
                            .RenderTarget);

        _renderTargetView =
            _deviceHost.Device
                .CreateRenderTargetView(
                    _renderTexture);

        _depthTexture =
            _deviceHost.Device
                .CreateTexture2D(
                    Format.D32_Float,
                    width,
                    height,
                    mipLevels: 1,
                    bindFlags:
                        BindFlags
                            .DepthStencil);

        _depthStencilView =
            _deviceHost.Device
                .CreateDepthStencilView(
                    _depthTexture);

        var stagingDescription =
            _renderTexture.Description;

        stagingDescription.BindFlags =
            BindFlags.None;

        stagingDescription.CPUAccessFlags =
            CpuAccessFlags.Read;

        stagingDescription.Usage =
            ResourceUsage.Staging;

        _stagingTexture =
            _deviceHost.Device
                .CreateTexture2D(
                    stagingDescription);

        Width = width;
        Height = height;
    }

    public void Render(
        Action<ID3D11DeviceContext> draw)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            draw);

        var target =
            _renderTargetView ??
            throw new InvalidOperationException(
                "Picking surface is not initialized.");

        var depthStencil =
            _depthStencilView ??
            throw new InvalidOperationException(
                "Picking depth buffer is not initialized.");

        _deviceHost.Context
            .OMSetRenderTargets(
                target,
                depthStencil);

        _deviceHost.Context
            .RSSetViewport(
                0,
                0,
                Width,
                Height);

        _deviceHost.Context
            .ClearRenderTargetView(
                target,
                new Color4(
                    0,
                    0,
                    0,
                    0));

        _deviceHost.Context
            .ClearDepthStencilView(
                depthStencil,
                DepthStencilClearFlags.Depth,
                1.0f,
                0);

        draw(
            _deviceHost.Context);
    }

    public PickingId Read(
        uint x,
        uint y)
    {
        ThrowIfDisposed();

        if (
            _renderTexture is null ||
            _stagingTexture is null ||
            x >= Width ||
            y >= Height)
        {
            return PickingId.None;
        }

        _deviceHost.Context
            .CopyResource(
                _stagingTexture,
                _renderTexture);

        var mapped =
            _deviceHost.Context
                .Map(
                    _stagingTexture,
                    0,
                    MapMode.Read);

        try
        {
            var offset =
                checked(
                    (int)(
                        y *
                        mapped.RowPitch +
                        x * 4));

            var r =
                Marshal.ReadByte(
                    mapped.DataPointer,
                    offset);

            var g =
                Marshal.ReadByte(
                    mapped.DataPointer,
                    offset + 1);

            var b =
                Marshal.ReadByte(
                    mapped.DataPointer,
                    offset + 2);

            var a =
                Marshal.ReadByte(
                    mapped.DataPointer,
                    offset + 3);

            var encoded =
                (uint)(
                    r |
                    (g << 8) |
                    (b << 16) |
                    (a << 24));

            return PickingColorCodec
                .Decode(encoded);
        }
        finally
        {
            _deviceHost.Context
                .Unmap(
                    _stagingTexture,
                    0);
        }
    }

    private void ReleaseResources()
    {
        _depthStencilView?.Dispose();
        _depthStencilView = null;

        _depthTexture?.Dispose();
        _depthTexture = null;

        _renderTargetView?.Dispose();
        _renderTargetView = null;

        _renderTexture?.Dispose();
        _renderTexture = null;

        _stagingTexture?.Dispose();
        _stagingTexture = null;

        Width = 0;
        Height = 0;
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
        ReleaseResources();
    }
}
