using MapStudio.Renderer.Scene;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace MapStudio.Renderer.Graphics;

public sealed class D3D11NativeMapRenderer :
    IDisposable
{
    private static readonly Color4 ClearColor =
        new(
            0.025f,
            0.055f,
            0.075f,
            1.0f);

    private readonly D3D11DeviceHost _deviceHost;
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pixelShader;
    private readonly ID3D11InputLayout _inputLayout;

    private ID3D11Buffer? _vertexBuffer;
    private int _vertexCount;
    private bool _disposed;

    public D3D11NativeMapRenderer(
        D3D11DeviceHost deviceHost)
    {
        ArgumentNullException.ThrowIfNull(
            deviceHost);

        _deviceHost = deviceHost;

        var shaderPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "Shaders",
                "NativeMap.hlsl");

        ReadOnlyMemory<byte>
            vertexShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "VSMain",
                    "vs_4_0");

        ReadOnlyMemory<byte>
            pixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSMain",
                    "ps_4_0");

        _vertexShader =
            _deviceHost.Device
                .CreateVertexShader(
                    vertexShaderBytecode
                        .Span);

        _pixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    pixelShaderBytecode
                        .Span);

        InputElementDescription[]
            elements =
            [
                new(
                    "POSITION",
                    0,
                    Format
                        .R32G32B32_Float,
                    0,
                    0),
                new(
                    "COLOR",
                    0,
                    Format
                        .R32G32B32A32_Float,
                    12,
                    0)
            ];

        _inputLayout =
            _deviceHost.Device
                .CreateInputLayout(
                    elements,
                    vertexShaderBytecode
                        .Span);
    }

    public int VertexCount =>
        _vertexCount;

    public void Upload(
        NativeSceneSnapshot scene)
    {
        ThrowIfDisposed();

        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _vertexCount = 0;

        var geometry =
            new NativeMapGeometryBuilder()
                .Build(scene);

        if (geometry.Vertices.Length == 0)
        {
            return;
        }

        _vertexBuffer =
            _deviceHost.Device
                .CreateBuffer(
                    geometry.Vertices
                        .AsSpan(),
                    BindFlags
                        .VertexBuffer);

        _vertexCount =
            geometry.Vertices.Length;
    }

    public void Render(
        D3D11SwapChainSurface surface)
    {
        ThrowIfDisposed();

        surface.RenderAndPresent(
            ClearColor,
            context =>
            {
                if (
                    _vertexBuffer is
                        null ||
                    _vertexCount == 0)
                {
                    return;
                }

                context
                    .IASetPrimitiveTopology(
                        PrimitiveTopology
                            .LineList);

                context
                    .IASetInputLayout(
                        _inputLayout);

                context
                    .IASetVertexBuffer(
                        0,
                        _vertexBuffer,
                        NativeMapVertex
                            .SizeInBytes);

                context
                    .VSSetShader(
                        _vertexShader);

                context
                    .PSSetShader(
                        _pixelShader);

                context.Draw(
                    (uint)_vertexCount,
                    0);
            });
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

        _vertexBuffer?.Dispose();
        _inputLayout.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
    }
}
