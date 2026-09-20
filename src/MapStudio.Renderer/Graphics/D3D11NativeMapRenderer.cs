using System.Numerics;
using MapStudio.Renderer.Picking;
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
    private readonly ID3D11Buffer _viewTransformBuffer;

    private Vector4 _viewTransform =
        new(
            0,
            0,
            1,
            0);

    private ID3D11Buffer? _vertexBuffer;
    private int _vertexCount;

    private ID3D11Buffer?
        _terrainTriangleBuffer;

    private int
        _terrainTriangleVertexCount;

    private ID3D11Buffer?
        _objectTriangleBuffer;

    private int
        _objectTriangleVertexCount;

    private ID3D11Buffer?
        _pickingTriangleBuffer;

    private int
        _pickingTriangleVertexCount;

    private ID3D11Buffer?
        _selectionTriangleBuffer;

    private int
        _selectionTriangleVertexCount;

    private NativeMapVertex[]
        _objectVertices =
            Array.Empty<
                NativeMapVertex>();

    private IReadOnlyDictionary<
        PickingId,
        NativeTriangleRange>
        _objectRanges =
            new Dictionary<
                PickingId,
                NativeTriangleRange>();

    private NativeMapVertex[]
        _proxyVertices =
            Array.Empty<
                NativeMapVertex>();

    private IReadOnlyDictionary<
        PickingId,
        NativeTriangleRange>
        _proxyRanges =
            new Dictionary<
                PickingId,
                NativeTriangleRange>();

    private readonly D3D11PickingSurface
        _pickingSurface;

    private bool _disposed;

    public D3D11NativeMapRenderer(
        D3D11DeviceHost deviceHost)
    {
        ArgumentNullException.ThrowIfNull(
            deviceHost);

        _deviceHost = deviceHost;

        _pickingSurface =
            new D3D11PickingSurface(
                _deviceHost);

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

        _viewTransformBuffer =
            _deviceHost.Device
                .CreateConstantBuffer<
                    Vector4>();
    }

    public int VertexCount =>
        _vertexCount;

    public int ObjectTriangleVertexCount =>
        _objectTriangleVertexCount;

    public int TerrainTriangleVertexCount =>
        _terrainTriangleVertexCount;

    public void SetViewTransform(
        Vector4 transform)
    {
        _viewTransform = transform;
    }

    public void Upload(
        NativeSceneSnapshot scene,
        NativeObjectTriangleGeometry?
            objectGeometry = null,
        NativePickingProxyGeometry?
            proxyGeometry = null,
        NativeTerrainTriangleGeometry?
            terrainGeometry = null)
    {
        ThrowIfDisposed();

        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _vertexCount = 0;

        _terrainTriangleBuffer
            ?.Dispose();
        _terrainTriangleBuffer = null;
        _terrainTriangleVertexCount = 0;

        _objectTriangleBuffer
            ?.Dispose();
        _objectTriangleBuffer = null;
        _objectTriangleVertexCount = 0;

        _pickingTriangleBuffer
            ?.Dispose();
        _pickingTriangleBuffer = null;
        _pickingTriangleVertexCount = 0;

        _selectionTriangleBuffer
            ?.Dispose();
        _selectionTriangleBuffer = null;
        _selectionTriangleVertexCount = 0;

        _objectVertices =
            Array.Empty<
                NativeMapVertex>();

        _objectRanges =
            new Dictionary<
                PickingId,
                NativeTriangleRange>();

        _proxyVertices =
            proxyGeometry?.Vertices ??
            Array.Empty<
                NativeMapVertex>();

        _proxyRanges =
            proxyGeometry?.Ranges ??
            new Dictionary<
                PickingId,
                NativeTriangleRange>();

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
            terrainGeometry is not null &&
            terrainGeometry
                .Vertices.Length > 0)
        {
            _terrainTriangleBuffer =
                _deviceHost.Device
                    .CreateBuffer(
                        terrainGeometry
                            .Vertices
                            .AsSpan(),
                        BindFlags
                            .VertexBuffer);

            _terrainTriangleVertexCount =
                terrainGeometry
                    .Vertices.Length;
        }

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

            _objectVertices =
                objectGeometry
                    .Vertices;

            _objectRanges =
                objectGeometry
                    .Ranges;

            var combinedPicking =
                new NativeMapVertex[
                    _proxyVertices.Length +
                    objectGeometry
                        .PickingVertices
                        .Length];

            _proxyVertices
                .AsSpan()
                .CopyTo(
                    combinedPicking
                        .AsSpan());

            objectGeometry
                .PickingVertices
                .AsSpan()
                .CopyTo(
                    combinedPicking
                        .AsSpan(
                            _proxyVertices
                                .Length));

            if (
                combinedPicking.Length >
                0)
            {
                _pickingTriangleBuffer =
                    _deviceHost.Device
                        .CreateBuffer(
                            combinedPicking
                                .AsSpan(),
                            BindFlags
                                .VertexBuffer);

                _pickingTriangleVertexCount =
                    combinedPicking
                        .Length;
            }
        }

        if (
            _pickingTriangleBuffer is
                null &&
            _proxyVertices.Length > 0)
        {
            _pickingTriangleBuffer =
                _deviceHost.Device
                    .CreateBuffer(
                        _proxyVertices
                            .AsSpan(),
                        BindFlags
                            .VertexBuffer);

            _pickingTriangleVertexCount =
                _proxyVertices.Length;
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

                ApplyViewTransform(
                    context);

                context
                    .PSSetShader(
                        _pixelShader);

                if (
                    _terrainTriangleBuffer
                        is not null &&
                    _terrainTriangleVertexCount >
                        0)
                {
                    context
                        .IASetPrimitiveTopology(
                            PrimitiveTopology
                                .TriangleList);

                    context
                        .IASetVertexBuffer(
                            0,
                            _terrainTriangleBuffer,
                            NativeMapVertex
                                .SizeInBytes);

                    context.Draw(
                        (uint)
                            _terrainTriangleVertexCount,
                        0);
                }

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
                        not null &&
                    _vertexCount > 0)
                {
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
                }

                if (
                    _selectionTriangleBuffer
                        is not null &&
                    _selectionTriangleVertexCount >
                        0)
                {
                    context
                        .IASetPrimitiveTopology(
                            PrimitiveTopology
                                .TriangleList);

                    context
                        .IASetVertexBuffer(
                            0,
                            _selectionTriangleBuffer,
                            NativeMapVertex
                                .SizeInBytes);

                    context.Draw(
                        (uint)
                            _selectionTriangleVertexCount,
                        0);
                }
            });

        RenderPicking(
            surface.Width,
            surface.Height);
    }

    private void RenderPicking(
        uint width,
        uint height)
    {
        _pickingSurface.EnsureSize(
            width,
            height);

        _pickingSurface.Render(
            context =>
            {
                if (
                    _pickingTriangleBuffer
                        is null ||
                    _pickingTriangleVertexCount <=
                        0)
                {
                    return;
                }

                context
                    .IASetPrimitiveTopology(
                        PrimitiveTopology
                            .TriangleList);

                context
                    .IASetInputLayout(
                        _inputLayout);

                context
                    .IASetVertexBuffer(
                        0,
                        _pickingTriangleBuffer,
                        NativeMapVertex
                            .SizeInBytes);

                context
                    .VSSetShader(
                        _vertexShader);

                ApplyViewTransform(
                    context);

                context
                    .PSSetShader(
                        _pixelShader);

                context.Draw(
                    (uint)
                        _pickingTriangleVertexCount,
                    0);
            });
    }

    public PickingId Pick(
        uint x,
        uint y) =>
        _pickingSurface.Read(
            x,
            y);

    public void SetSelection(
        PickingId pickingId)
    {
        _selectionTriangleBuffer
            ?.Dispose();
        _selectionTriangleBuffer = null;
        _selectionTriangleVertexCount = 0;

        if (pickingId.IsNone)
        {
            return;
        }

        NativeMapVertex[] sourceVertices;
        NativeTriangleRange range;

        if (
            _objectRanges.TryGetValue(
                pickingId,
                out range) &&
            range.VertexCount > 0 &&
            range.StartVertex >= 0 &&
            range.StartVertex +
                range.VertexCount <=
            _objectVertices.Length)
        {
            sourceVertices =
                _objectVertices;
        }
        else if (
            _proxyRanges.TryGetValue(
                pickingId,
                out range) &&
            range.VertexCount > 0 &&
            range.StartVertex >= 0 &&
            range.StartVertex +
                range.VertexCount <=
            _proxyVertices.Length)
        {
            sourceVertices =
                _proxyVertices;
        }
        else
        {
            return;
        }

        var selected =
            new NativeMapVertex[
                range.VertexCount];

        var color =
            new System.Numerics.Vector4(
                1.0f,
                0.10f,
                0.05f,
                1.0f);

        for (
            var index = 0;
            index < selected.Length;
            index++)
        {
            var source =
                sourceVertices[
                    range.StartVertex +
                    index];

            selected[index] =
                new NativeMapVertex(
                    source.Position,
                    color);
        }

        _selectionTriangleBuffer =
            _deviceHost.Device
                .CreateBuffer(
                    selected.AsSpan(),
                    BindFlags
                        .VertexBuffer);

        _selectionTriangleVertexCount =
            selected.Length;
    }

    private void ApplyViewTransform(
        ID3D11DeviceContext context)
    {
        Span<Vector4> data =
            stackalloc Vector4[1];

        data[0] =
            _viewTransform;

        _viewTransformBuffer
            .SetData(
                context,
                data,
                MapMode.WriteDiscard);

        context
            .VSSetConstantBuffer(
                0,
                _viewTransformBuffer);
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

        _pickingSurface.Dispose();

        _selectionTriangleBuffer
            ?.Dispose();

        _pickingTriangleBuffer
            ?.Dispose();

        _objectTriangleBuffer
            ?.Dispose();

        _terrainTriangleBuffer
            ?.Dispose();

        _vertexBuffer?.Dispose();
        _viewTransformBuffer.Dispose();
        _inputLayout.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
    }
}
