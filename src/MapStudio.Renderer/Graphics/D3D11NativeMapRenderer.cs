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
    private readonly ID3D11PixelShader _texturedPixelShader;
    private readonly ID3D11PixelShader _terrainLayerPixelShader;
    private readonly ID3D11InputLayout _inputLayout;
    private readonly ID3D11Buffer _viewProjectionBuffer;
    private readonly ID3D11SamplerState _textureSampler;
    private readonly ID3D11SamplerState _maskSampler;
    private readonly ID3D11SamplerState _skySampler;
    private readonly ID3D11DepthStencilState _skyDepthState;
    private readonly ID3D11BlendState _alphaBlendState;
    private readonly NativeGpuTextureLoader _textureLoader;
    private readonly ID3D11Buffer _skyTriangleBuffer;
    private readonly int _skyTriangleVertexCount;

    private NativeGpuTexture? _skyTexture;
    private string? _skyTexturePath;

    private readonly Dictionary<
        string,
        NativeGpuTexture>
        _textureCache =
            new(
                StringComparer
                    .OrdinalIgnoreCase);

    private readonly HashSet<string>
        _failedTexturePaths =
            new(
                StringComparer
                    .OrdinalIgnoreCase);

    private Matrix4x4 _viewProjection =
        Matrix4x4.Identity;

    private ID3D11Buffer? _vertexBuffer;
    private int _vertexCount;

    private ID3D11Buffer?
        _terrainTriangleBuffer;

    private int
        _terrainTriangleVertexCount;

    private IReadOnlyList<
        NativeMaterialBatch>
        _terrainMaterialBatches =
            Array.Empty<
                NativeMaterialBatch>();

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
        _hoverTriangleBuffer;

    private int
        _hoverTriangleVertexCount;

    private ID3D11Buffer?
        _selectionTriangleBuffer;

    private int
        _selectionTriangleVertexCount;

    private ID3D11Buffer?
        _gizmoTriangleBuffer;

    private int
        _gizmoTriangleVertexCount;

    private ID3D11Buffer?
        _placementPreviewBuffer;

    private int
        _placementPreviewVertexCount;

    private Matrix4x4
        _placementPreviewTransform =
            Matrix4x4.Identity;

    private PickingId _hoverPickingId =
        PickingId.None;

    private PickingId _selectionPickingId =
        PickingId.None;

    private Vector3 _cameraPosition =
        Vector3.Zero;

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

    private IReadOnlyList<
        NativeMaterialBatch>
        _objectMaterialBatches =
            Array.Empty<
                NativeMaterialBatch>();

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

    private IReadOnlyList<
        NativeMaterialBatch>
        _splineMaterialBatches =
            Array.Empty<
                NativeMaterialBatch>();

    private NativeMapVertex[]
        _proxyVertices =
            Array.Empty<
                NativeMapVertex>();

    private NativeMapVertex[]
        _scenePickingVertices =
            Array.Empty<
                NativeMapVertex>();

    private NativeMapVertex[]
        _gizmoPickingVertices =
            Array.Empty<
                NativeMapVertex>();

    private Matrix4x4
        _selectionPreviewTransform =
            Matrix4x4.Identity;

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

        ReadOnlyMemory<byte>
            texturedPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSTextured",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            terrainLayerPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSTerrainLayer",
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

        _texturedPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    texturedPixelShaderBytecode
                        .Span);

        _terrainLayerPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    terrainLayerPixelShaderBytecode
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
                    0),
                new(
                    "TEXCOORD",
                    0,
                    Format
                        .R32G32_Float,
                    28,
                    0),
                new(
                    "TEXCOORD",
                    1,
                    Format
                        .R32G32_Float,
                    36,
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

        _textureSampler =
            _deviceHost.Device
                .CreateSamplerState(
                    SamplerDescription
                        .LinearWrap);

        _maskSampler =
            _deviceHost.Device
                .CreateSamplerState(
                    SamplerDescription
                        .LinearClamp);

        _skySampler =
            _deviceHost.Device
                .CreateSamplerState(
                    new SamplerDescription(
                        Filter.MinMagMipLinear,
                        TextureAddressMode.Wrap,
                        TextureAddressMode.Clamp,
                        TextureAddressMode.Clamp));

        _skyDepthState =
            _deviceHost.Device
                .CreateDepthStencilState(
                    DepthStencilDescription
                        .None);

        _alphaBlendState =
            _deviceHost.Device
                .CreateBlendState(
                    BlendDescription
                        .NonPremultiplied);

        _textureLoader =
            new NativeGpuTextureLoader(
                _deviceHost);

        var skyGeometry =
            new NativeSkySphereGeometryBuilder()
                .Build();

        _skyTriangleBuffer =
            _deviceHost.Device
                .CreateBuffer(
                    skyGeometry
                        .Vertices
                        .AsSpan(),
                    BindFlags
                        .VertexBuffer);

        _skyTriangleVertexCount =
            skyGeometry
                .Vertices.Length;
    }

    public int VertexCount =>
        _vertexCount;

    public int ObjectTriangleVertexCount =>
        _objectTriangleVertexCount;

    public int TerrainTriangleVertexCount =>
        _terrainTriangleVertexCount;

    public int SplineTriangleVertexCount =>
        _splineTriangleVertexCount;

    public int LoadedTextureCount =>
        _textureCache.Count;

    public bool HasSkyTexture =>
        _skyTexture is not null;

    public string? SkyTexturePath =>
        _skyTexturePath;

    public bool SetSkyTexture(
        string? path)
    {
        ThrowIfDisposed();

        var normalized =
            string.IsNullOrWhiteSpace(
                path)
                ? null
                : Path.GetFullPath(
                    path);

        if (
            string.Equals(
                normalized,
                _skyTexturePath,
                StringComparison.OrdinalIgnoreCase) &&
            _skyTexture is not null)
        {
            return true;
        }

        _skyTexture?.Dispose();
        _skyTexture = null;
        _skyTexturePath = null;

        if (
            normalized is null ||
            !File.Exists(normalized))
        {
            return false;
        }

        var texture =
            _textureLoader
                .TryLoad(
                    normalized);

        if (texture is null)
        {
            return false;
        }

        _skyTexture =
            texture;

        _skyTexturePath =
            normalized;

        return true;
    }

    public void SetViewProjection(
        Matrix4x4 viewProjection,
        Vector3 cameraPosition)
    {
        _viewProjection =
            viewProjection;

        if (
            _cameraPosition !=
            cameraPosition)
        {
            _cameraPosition =
                cameraPosition;

            RebuildHighlights();
        }
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

        _terrainMaterialBatches =
            Array.Empty<
                NativeMaterialBatch>();

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

        _hoverTriangleBuffer
            ?.Dispose();
        _hoverTriangleBuffer = null;
        _hoverTriangleVertexCount = 0;

        _selectionTriangleBuffer
            ?.Dispose();
        _selectionTriangleBuffer = null;
        _selectionTriangleVertexCount = 0;

        _gizmoTriangleBuffer
            ?.Dispose();
        _gizmoTriangleBuffer = null;
        _gizmoTriangleVertexCount = 0;

        _placementPreviewBuffer
            ?.Dispose();
        _placementPreviewBuffer = null;
        _placementPreviewVertexCount = 0;
        _placementPreviewTransform =
            Matrix4x4.Identity;

        _scenePickingVertices =
            Array.Empty<
                NativeMapVertex>();

        _gizmoPickingVertices =
            Array.Empty<
                NativeMapVertex>();

        _selectionPreviewTransform =
            Matrix4x4.Identity;

        _hoverPickingId =
            PickingId.None;

        _selectionPickingId =
            PickingId.None;

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

        _splineMaterialBatches =
            Array.Empty<
                NativeMaterialBatch>();

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

        if (geometry.Vertices.Length > 0)
        {
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

            _terrainMaterialBatches =
                terrainGeometry
                    .MaterialBatches;
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

            _objectMaterialBatches =
                objectGeometry
                    .MaterialBatches;
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

            _splineMaterialBatches =
                splineGeometry
                    .MaterialBatches;
        }

        UpdateTextureCache(
            _terrainMaterialBatches
                .Concat(
                    _splineMaterialBatches)
                .Concat(
                    _objectMaterialBatches)
                .ToArray());

        _scenePickingVertices =
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

        RebuildPickingBuffer();
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

                DrawSkyGeometry(
                    context);

                ApplyViewProjection(
                    context);

                context
                    .PSSetShader(
                        _pixelShader);

                DrawTerrainGeometry(
                    context);

                DrawSplineGeometry(
                    context);

                DrawObjectGeometry(
                    context);

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
                    _hoverTriangleBuffer
                        is not null &&
                    _hoverTriangleVertexCount >
                        0)
                {
                    context
                        .IASetPrimitiveTopology(
                            PrimitiveTopology
                                .TriangleList);

                    context
                        .IASetVertexBuffer(
                            0,
                            _hoverTriangleBuffer,
                            NativeMapVertex
                                .SizeInBytes);

                    context.Draw(
                        (uint)
                            _hoverTriangleVertexCount,
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

                if (
                    _gizmoTriangleBuffer
                        is not null &&
                    _gizmoTriangleVertexCount >
                        0)
                {
                    context
                        .IASetPrimitiveTopology(
                            PrimitiveTopology
                                .TriangleList);

                    context
                        .IASetVertexBuffer(
                            0,
                            _gizmoTriangleBuffer,
                            NativeMapVertex
                                .SizeInBytes);

                    context.Draw(
                        (uint)
                            _gizmoTriangleVertexCount,
                        0);
                }

                if (
                    _placementPreviewBuffer
                        is not null &&
                    _placementPreviewVertexCount >
                        0)
                {
                    context
                        .IASetPrimitiveTopology(
                            PrimitiveTopology
                                .TriangleList);

                    context
                        .IASetVertexBuffer(
                            0,
                            _placementPreviewBuffer,
                            NativeMapVertex
                                .SizeInBytes);

                    ApplyViewProjection(
                        context,
                        _placementPreviewTransform *
                        _viewProjection);

                    context.Draw(
                        (uint)
                            _placementPreviewVertexCount,
                        0);
                }
            });

        RenderPicking(
            surface.Width,
            surface.Height);
    }

    private void DrawSkyGeometry(
        ID3D11DeviceContext context)
    {
        if (
            _skyTexture is null ||
            _skyTriangleVertexCount <=
                0)
        {
            return;
        }

        context
            .OMSetBlendState(
                null);

        context
            .OMSetDepthStencilState(
                _skyDepthState);

        context
            .IASetPrimitiveTopology(
                PrimitiveTopology
                    .TriangleList);

        context
            .IASetVertexBuffer(
                0,
                _skyTriangleBuffer,
                NativeMapVertex
                    .SizeInBytes);

        var skyTransform =
            Matrix4x4.CreateScale(
                10_000.0f) *
            Matrix4x4.CreateTranslation(
                _cameraPosition);

        ApplyViewProjection(
            context,
            skyTransform *
            _viewProjection);

        context
            .PSSetSampler(
                0,
                _skySampler);

        context
            .PSSetShader(
                _texturedPixelShader);

        context
            .PSSetShaderResource(
                0,
                _skyTexture.View);

        context.Draw(
            (uint)
                _skyTriangleVertexCount,
            0);

        context
            .PSUnsetShaderResource(
                0);

        context
            .OMSetDepthStencilState(
                null);

        context
            .PSSetShader(
                _pixelShader);
    }

    private void DrawTerrainGeometry(
        ID3D11DeviceContext context)
    {
        DrawMaterialGeometry(
            context,
            _terrainTriangleBuffer,
            _terrainTriangleVertexCount,
            _terrainMaterialBatches);
    }

    private void DrawSplineGeometry(
        ID3D11DeviceContext context)
    {
        DrawMaterialGeometry(
            context,
            _splineTriangleBuffer,
            _splineTriangleVertexCount,
            _splineMaterialBatches);
    }

    private void DrawObjectGeometry(
        ID3D11DeviceContext context)
    {
        DrawMaterialGeometry(
            context,
            _objectTriangleBuffer,
            _objectTriangleVertexCount,
            _objectMaterialBatches);
    }

    private void DrawMaterialGeometry(
        ID3D11DeviceContext context,
        ID3D11Buffer? vertexBuffer,
        int vertexCount,
        IReadOnlyList<
            NativeMaterialBatch> batches)
    {
        if (
            vertexBuffer is null ||
            vertexCount <= 0)
        {
            return;
        }

        context
            .IASetPrimitiveTopology(
                PrimitiveTopology
                    .TriangleList);

        context
            .IASetVertexBuffer(
                0,
                vertexBuffer,
                NativeMapVertex
                    .SizeInBytes);

        if (batches.Count == 0)
        {
            context
                .OMSetBlendState(
                    null);

            context
                .PSSetShader(
                    _pixelShader);

            context.Draw(
                (uint)vertexCount,
                0);

            return;
        }

        context
            .PSSetSampler(
                0,
                _textureSampler);

        context
            .PSSetSampler(
                1,
                _maskSampler);

        foreach (var batch in batches)
        {
            if (
                batch.VertexCount <=
                    0)
            {
                continue;
            }

            NativeGpuTexture? texture =
                null;

            var hasTexture =
                false;

            if (
                batch.TexturePath is
                    { Length: > 0 }
                    texturePath)
            {
                hasTexture =
                    _textureCache
                        .TryGetValue(
                            texturePath,
                            out texture);
            }

            if (
                batch.MaskTexturePath is
                    { Length: > 0 }
                    maskPath)
            {
                NativeGpuTexture? maskTexture =
                    null;

                var hasMask =
                    _textureCache
                        .TryGetValue(
                            maskPath,
                            out maskTexture);

                if (
                    !hasTexture ||
                    !hasMask)
                {
                    continue;
                }

                context
                    .OMSetBlendState(
                        _alphaBlendState);

                context
                    .PSSetShader(
                        _terrainLayerPixelShader);

                context
                    .PSSetShaderResource(
                        0,
                        texture!.View);

                context
                    .PSSetShaderResource(
                        1,
                        maskTexture!.View);
            }
            else if (hasTexture)
            {
                context
                    .OMSetBlendState(
                        null);

                context
                    .PSUnsetShaderResource(
                        1);

                context
                    .PSSetShader(
                        _texturedPixelShader);

                context
                    .PSSetShaderResource(
                        0,
                        texture!.View);
            }
            else
            {
                context
                    .OMSetBlendState(
                        null);

                context
                    .PSUnsetShaderResource(
                        0);

                context
                    .PSUnsetShaderResource(
                        1);

                context
                    .PSSetShader(
                        _pixelShader);
            }

            context.Draw(
                (uint)
                    batch.VertexCount,
                (uint)
                    batch.StartVertex);
        }

        context
            .OMSetBlendState(
                null);

        context
            .PSUnsetShaderResource(
                0);

        context
            .PSUnsetShaderResource(
                1);

        context
            .PSSetShader(
                _pixelShader);
    }

    private void UpdateTextureCache(
        IReadOnlyList<
            NativeMaterialBatch> batches)
    {
        var diffusePaths =
            batches
                .Where(
                    batch =>
                        !string.IsNullOrWhiteSpace(
                            batch.TexturePath))
                .Select(
                    batch =>
                        batch.TexturePath!)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray();

        var maskPaths =
            batches
                .Where(
                    batch =>
                        !string.IsNullOrWhiteSpace(
                            batch.MaskTexturePath))
                .Select(
                    batch =>
                        batch.MaskTexturePath!)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase);

        var requested =
            diffusePaths
                .Concat(
                    maskPaths)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .Take(512)
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase);

        maskPaths
            .IntersectWith(
                requested);

        var stale =
            _textureCache.Keys
                .Where(
                    path =>
                        !requested
                            .Contains(path))
                .ToArray();

        foreach (var path in stale)
        {
            _textureCache[path]
                .Dispose();

            _textureCache.Remove(
                path);
        }

        _failedTexturePaths
            .RemoveWhere(
                path =>
                    !requested
                        .Contains(path));

        foreach (
            var path in requested)
        {
            if (
                _textureCache
                    .ContainsKey(path) ||
                _failedTexturePaths
                    .Contains(path))
            {
                continue;
            }

            var texture =
                maskPaths.Contains(
                    path)
                    ? _textureLoader
                        .TryLoadAlphaMask(
                            path)
                    : _textureLoader
                        .TryLoad(
                            path);

            if (texture is null)
            {
                _failedTexturePaths
                    .Add(path);

                continue;
            }

            _textureCache[path] =
                texture;
        }
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

    public void SetPlacementPreview(
        NativeAssetPreviewGeometry? geometry,
        Matrix4x4 transform)
    {
        _placementPreviewBuffer
            ?.Dispose();

        _placementPreviewBuffer = null;
        _placementPreviewVertexCount = 0;
        _placementPreviewTransform =
            transform;

        if (
            geometry is null ||
            geometry.Vertices.Length == 0)
        {
            return;
        }

        var vertices =
            geometry.Vertices
                .Select(
                    vertex =>
                        new NativeMapVertex(
                            vertex.Position,
                            new Vector4(
                                0.10f,
                                0.72f,
                                1.0f,
                                1.0f)))
                .ToArray();

        _placementPreviewBuffer =
            _deviceHost.Device
                .CreateBuffer(
                    vertices.AsSpan(),
                    BindFlags
                        .VertexBuffer);

        _placementPreviewVertexCount =
            vertices.Length;
    }

    public void SetPlacementPreviewTransform(
        Matrix4x4 transform)
    {
        _placementPreviewTransform =
            transform;
    }

    public void SetGizmoGeometry(
        NativeGizmoGeometry? geometry)
    {
        _gizmoTriangleBuffer
            ?.Dispose();

        _gizmoTriangleBuffer = null;
        _gizmoTriangleVertexCount = 0;

        _gizmoPickingVertices =
            geometry
                ?.PickingVertices ??
            Array.Empty<
                NativeMapVertex>();

        if (
            geometry is not null &&
            geometry.Vertices.Length > 0)
        {
            _gizmoTriangleBuffer =
                _deviceHost.Device
                    .CreateBuffer(
                        geometry.Vertices
                            .AsSpan(),
                        BindFlags
                            .VertexBuffer);

            _gizmoTriangleVertexCount =
                geometry
                    .Vertices.Length;
        }

        RebuildPickingBuffer();
    }

    public void SetSelectionPreviewTransform(
        Matrix4x4 transform)
    {
        if (
            _selectionPreviewTransform ==
            transform)
        {
            return;
        }

        _selectionPreviewTransform =
            transform;

        RebuildSelection();
    }

    private void RebuildPickingBuffer()
    {
        _pickingTriangleBuffer
            ?.Dispose();

        _pickingTriangleBuffer = null;
        _pickingTriangleVertexCount = 0;

        var combined =
            _scenePickingVertices
                .Concat(
                    _gizmoPickingVertices)
                .ToArray();

        if (combined.Length == 0)
        {
            return;
        }

        _pickingTriangleBuffer =
            _deviceHost.Device
                .CreateBuffer(
                    combined.AsSpan(),
                    BindFlags
                        .VertexBuffer);

        _pickingTriangleVertexCount =
            combined.Length;
    }

    public bool SetHover(
        PickingId pickingId)
    {
        if (
            pickingId ==
            _selectionPickingId)
        {
            pickingId =
                PickingId.None;
        }

        if (
            pickingId ==
            _hoverPickingId)
        {
            return false;
        }

        _hoverPickingId =
            pickingId;

        RebuildHover();

        return true;
    }

    public bool SetSelection(
        PickingId pickingId)
    {
        if (
            pickingId ==
            _selectionPickingId)
        {
            return false;
        }

        _selectionPickingId =
            pickingId;

        if (
            _hoverPickingId ==
            pickingId)
        {
            _hoverPickingId =
                PickingId.None;

            RebuildHover();
        }

        RebuildSelection();

        return true;
    }

    private void RebuildHighlights()
    {
        RebuildHover();
        RebuildSelection();
    }

    private void RebuildHover()
    {
        _hoverTriangleBuffer
            ?.Dispose();

        _hoverTriangleBuffer = null;
        _hoverTriangleVertexCount = 0;

        var vertices =
            BuildHighlight(
                _hoverPickingId,
                new Vector4(
                    0.05f,
                    0.45f,
                    1.0f,
                    1.0f),
                0.035f);

        if (vertices.Length == 0)
        {
            return;
        }

        _hoverTriangleBuffer =
            _deviceHost.Device
                .CreateBuffer(
                    vertices.AsSpan(),
                    BindFlags
                        .VertexBuffer);

        _hoverTriangleVertexCount =
            vertices.Length;
    }

    private void RebuildSelection()
    {
        _selectionTriangleBuffer
            ?.Dispose();

        _selectionTriangleBuffer = null;
        _selectionTriangleVertexCount = 0;

        var vertices =
            BuildHighlight(
                _selectionPickingId,
                new Vector4(
                    1.0f,
                    0.10f,
                    0.05f,
                    1.0f),
                0.065f);

        if (
            _selectionPreviewTransform !=
            Matrix4x4.Identity)
        {
            for (
                var index = 0;
                index < vertices.Length;
                index++)
            {
                var vertex =
                    vertices[index];

                vertices[index] =
                    new NativeMapVertex(
                        Vector3.Transform(
                            vertex.Position,
                            _selectionPreviewTransform),
                        vertex.Color);
            }
        }

        if (vertices.Length == 0)
        {
            return;
        }

        _selectionTriangleBuffer =
            _deviceHost.Device
                .CreateBuffer(
                    vertices.AsSpan(),
                    BindFlags
                        .VertexBuffer);

        _selectionTriangleVertexCount =
            vertices.Length;
    }

    private NativeMapVertex[] BuildHighlight(
        PickingId pickingId,
        Vector4 color,
        float cameraBias)
    {
        if (
            pickingId.IsNone ||
            !TryGetHighlightSource(
                pickingId,
                out var sourceVertices,
                out var range))
        {
            return Array.Empty<
                NativeMapVertex>();
        }

        return
            NativeHighlightGeometryBuilder
                .Build(
                    sourceVertices,
                    range,
                    _cameraPosition,
                    color,
                    cameraBias);
    }

    private bool TryGetHighlightSource(
        PickingId pickingId,
        out NativeMapVertex[] sourceVertices,
        out NativeTriangleRange range)
    {
        if (
            _objectRanges.TryGetValue(
                pickingId,
                out range) &&
            IsValidRange(
                _objectVertices,
                range))
        {
            sourceVertices =
                _objectVertices;

            return true;
        }

        if (
            _splineRanges.TryGetValue(
                pickingId,
                out range) &&
            IsValidRange(
                _splineVertices,
                range))
        {
            sourceVertices =
                _splineVertices;

            return true;
        }

        if (
            _proxyRanges.TryGetValue(
                pickingId,
                out range) &&
            IsValidRange(
                _proxyVertices,
                range))
        {
            sourceVertices =
                _proxyVertices;

            return true;
        }

        sourceVertices =
            Array.Empty<
                NativeMapVertex>();

        range =
            default;

        return false;
    }

    private static bool IsValidRange(
        NativeMapVertex[] vertices,
        NativeTriangleRange range) =>
        range.VertexCount > 0 &&
        range.StartVertex >= 0 &&
        range.StartVertex +
            range.VertexCount <=
        vertices.Length;

    private void ApplyViewProjection(
        ID3D11DeviceContext context) =>
        ApplyViewProjection(
            context,
            _viewProjection);

    private void ApplyViewProjection(
        ID3D11DeviceContext context,
        Matrix4x4 viewProjection)
    {
        Span<Matrix4x4> data =
            stackalloc Matrix4x4[1];

        data[0] =
            viewProjection;

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

        _hoverTriangleBuffer
            ?.Dispose();

        _selectionTriangleBuffer
            ?.Dispose();

        _gizmoTriangleBuffer
            ?.Dispose();

        _placementPreviewBuffer
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
        foreach (
            var texture in
                _textureCache.Values)
        {
            texture.Dispose();
        }

        _textureCache.Clear();
        _failedTexturePaths.Clear();

        _skyTexture?.Dispose();
        _skyTexture = null;
        _skyTriangleBuffer.Dispose();
        _alphaBlendState.Dispose();
        _skyDepthState.Dispose();
        _skySampler.Dispose();
        _maskSampler.Dispose();
        _textureSampler.Dispose();
        _inputLayout.Dispose();
        _terrainLayerPixelShader.Dispose();
        _texturedPixelShader.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
    }
}
