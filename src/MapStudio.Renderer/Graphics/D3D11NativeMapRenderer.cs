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

    private ID3D11Buffer?
        _objectTriangleBuffer;

    private int
        _objectTriangleVertexCount;

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

    public int ObjectTriangleVertexCount =>
        _objectTriangleVertexCount;

    public void Upload(
        NativeSceneSnapshot scene,
        NativeObjectTriangleGeometry?
            objectGeometry = null)
    {
        ThrowIfDisposed();

        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _vertexCount = 0;

        _objectTriangleBuffer
            ?.Dispose();
        _objectTriangleBuffer = null;
        _objectTriangleVertexCount = 0;

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

        if (
            objectGeometry is not null &&
            objectGeometry
                .Vertices.Length > 0)
        {
            _objectTriangleBuffer =
                _deviceHost.Device
                    .CreateBuffer(
                        objectGeometry
                            .Vertices
                            .AsSpan(),
                        BindFlags
                            .VertexBuffer);

            _objectTriangleVertexCount =
                objectGeometry
                    .Vertices.Length;
        }
    }

    public void Render(
        D3D11SwapChainSurface surface)
    {
        ThrowIfDisposed();

        surface.RenderAndPresent(
            ClearColor,
            context =>
            {
                context
                    .IASetInputLayout(
                        _inputLayout);

                context
                    .VSSetShader(
                        _vertexShader);

                context
                    .PSSetShader(
                        _pixelShader);

                if (
                    _objectTriangleBuffer
                        is not null &&
                    _objectTriangleVertexCount >
                        0)
                {
                    context
                        .IASetPrimitiveTopology(
                            PrimitiveTopology
                                .TriangleList);

                    context
                        .IASetVertexBuffer(
                            0,
                            _objectTriangleBuffer,
                            NativeMapVertex
                                .SizeInBytes);

                    context.Draw(
                        (uint)
                            _objectTriangleVertexCount,
                        0);
                }

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
                    .IASetVertexBuffer(
                        0,
                        _vertexBuffer,
                        NativeMapVertex
                            .SizeInBytes);

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

        _objectTriangleBuffer
            ?.Dispose();
        _vertexBuffer?.Dispose();
        _inputLayout.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
    }
}
