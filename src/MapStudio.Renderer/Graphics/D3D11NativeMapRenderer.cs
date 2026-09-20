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
    private readonly ID3D11Buffer _viewProjectionBuffer;

    private Matrix4x4 _viewProjection =
        Matrix4x4.Identity;

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
        _splineTriangleBuffer;

    private int
        _splineTriangleVertexCount;

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
        _splineVertices =
            Array.Empty<
                NativeMapVertex>();

    private IReadOnlyDictionary<
        PickingId,
        NativeTriangleRange>
        _splineRanges =
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

        _viewProjectionBuffer =
            _deviceHost.Device
                .CreateConstantBuffer<
                    Matrix4x4>();
    }

    public int VertexCount =>
        _vertexCount;

    public int ObjectTriangleVertexCount =>
        _objectTriangleVertexCount;

    public int TerrainTriangleVertexCount =>
        _terrainTriangleVertexCount;

    public int SplineTriangleVertexCount =>
        _splineTriangleVertexCount;

    public void SetViewProjection(
        Matrix4x4 viewProjection)
    {
        _viewProjection =
            viewProjection;
    }

    public void Upload(
        NativeSceneSnapshot scene,
        NativeObjectTriangleGeometry?
            objectGeometry = null,
        NativePickingProxyGeometry?
            proxyGeometry = null,
        NativeTerrainTriangleGeometry?
            terrainGeometry = null,
        NativeSplineTriangleGeometry?
            splineGeometry = null)
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

        _splineTriangleBuffer
            ?.Dispose();
        _splineTriangleBuffer = null;
        _splineTriangleVertexCount = 0;

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

        _splineVertices =
            Array.Empty<
                NativeMapVertex>();

        _splineRanges =
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
        }

        if (
            splineGeometry is not null &&
            splineGeometry
                .Vertices.Length > 0)
        {
            _splineTriangleBuffer =
                _deviceHost.Device
                    .CreateBuffer(
                        splineGeometry
                            .Vertices
                            .AsSpan(),
                        BindFlags
                            .VertexBuffer);

            _splineTriangleVertexCount =
                splineGeometry
                    .Vertices.Length;

            _splineVertices =
                splineGeometry
                    .Vertices;

            _splineRanges =
                splineGeometry
                    .Ranges;
        }

        var pickingVertices =
            _proxyVertices
                .Concat(
                    objectGeometry
                        ?.PickingVertices ??
                    Array.Empty<
                        NativeMapVertex>())
                .Concat(
                    splineGeometry
                        ?.PickingVertices ??
                    Array.Empty<
                        NativeMapVertex>())
                .ToArray();

        if (pickingVertices.Length > 0)
        {
            _pickingTriangleBuffer =
                _deviceHost.Device
                    .CreateBuffer(
                        pickingVertices
                            .AsSpan(),
                        BindFlags
                            .VertexBuffer);

            _pickingTriangleVertexCount =
                pickingVertices.Length;
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

                ApplyViewProjection(
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
                    _splineTriangleBuffer
                        is not null &&
                    _splineTriangleVertexCount >
                        0)
                {
                    context
                        .IASetPrimitiveTopology(
                            PrimitiveTopology
                                .TriangleList);

                    context
                        .IASetVertexBuffer(
                            0,
                            _splineTriangleBuffer,
                            NativeMapVertex
                                .SizeInBytes);

                    context.Draw(
                        (uint)
                            _splineTriangleVertexCount,
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

                ApplyViewProjection(
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
            _splineRanges.TryGetValue(
                pickingId,
                out range) &&
            range.VertexCount > 0 &&
            range.StartVertex >= 0 &&
            range.StartVertex +
                range.VertexCount <=
            _splineVertices.Length)
        {
            sourceVertices =
                _splineVertices;
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

    private void ApplyViewProjection(
        ID3D11DeviceContext context)
    {
        Span<Matrix4x4> data =
            stackalloc Matrix4x4[1];

        data[0] =
            _viewProjection;

        _viewProjectionBuffer
            .SetData(
                context,
                data,
                MapMode.WriteDiscard);

        context
            .VSSetConstantBuffer(
                0,
                _viewProjectionBuffer);
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

        _splineTriangleBuffer
            ?.Dispose();

        _terrainTriangleBuffer
            ?.Dispose();

        _vertexBuffer?.Dispose();
        _viewProjectionBuffer.Dispose();
        _inputLayout.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
    }
}
