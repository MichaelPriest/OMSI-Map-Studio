using System.Numerics;
using System.Runtime.InteropServices;
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
    [StructLayout(
        LayoutKind.Sequential)]
    private struct ViewportShaderConstants
    {
        public Matrix4x4
            ViewProjection;

        public Vector4
            CameraPosition;
    }


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
    private readonly ID3D11PixelShader _alphaCutoutPixelShader;
    private readonly ID3D11PixelShader _alphaBlendPixelShader;
    private readonly ID3D11PixelShader _alphaCutoutTransMapPixelShader;
    private readonly ID3D11PixelShader _alphaBlendTransMapPixelShader;
    private readonly ID3D11PixelShader _nightMaterialPixelShader;
    private readonly ID3D11PixelShader _nightMaterialCutoutPixelShader;
    private readonly ID3D11PixelShader _nightMaterialBlendPixelShader;
    private readonly ID3D11PixelShader _nightMaterialCutoutTransMapPixelShader;
    private readonly ID3D11PixelShader _nightMaterialBlendTransMapPixelShader;
    private readonly ID3D11PixelShader _terrainLayerPixelShader;
    private readonly ID3D11PixelShader _terrainBaseDetailPixelShader;
    private readonly ID3D11PixelShader _terrainLayerDetailPixelShader;
    private readonly ID3D11InputLayout _inputLayout;
    private readonly ID3D11Buffer _viewProjectionBuffer;
    private readonly ID3D11Buffer _materialPreviewBuffer;
    private readonly ID3D11SamplerState _textureSampler;
    private readonly ID3D11SamplerState _maskSampler;
    private readonly ID3D11SamplerState _skySampler;
    private readonly ID3D11DepthStencilState _skyDepthState;
    private readonly ID3D11DepthStencilState _depthReadState;
    private readonly ID3D11DepthStencilState _depthDisabledState;
    private readonly ID3D11BlendState _alphaBlendState;
    private readonly ID3D11RasterizerState _terrainRasterizerState;
    private readonly NativeGpuTextureLoader _textureLoader;
    private readonly ID3D11Buffer _skyTriangleBuffer;
    private readonly int _skyTriangleVertexCount;

    private NativeGpuTexture? _skyTexture;
    private string? _skyTexturePath;
    private bool _nightPreviewEnabled;

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

    private ID3D11Buffer? _objectGuideBuffer;
    private int _objectGuideVertexCount;

    private ID3D11Buffer? _splineGuideBuffer;
    private int _splineGuideVertexCount;

    private bool _gridVisible =
        true;

    private bool _splineProfilesVisible =
        true;

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

    private NativeMapVertex[]
        _objectPickingVertices =
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

    private NativeMapVertex[]
        _splinePickingVertices =
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

    private NativeSceneVisibility
        _visibility =
            NativeSceneVisibility.All;

    private NativeSelectionFilter
        _selectionFilter =
            NativeSelectionFilter.All;

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
            alphaCutoutPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSAlphaCutout",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            alphaBlendPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSAlphaBlend",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            alphaCutoutTransMapPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSAlphaCutoutTransMap",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            alphaBlendTransMapPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSAlphaBlendTransMap",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            nightMaterialPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSNightMaterial",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            nightMaterialCutoutPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSNightMaterialCutout",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            nightMaterialBlendPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSNightMaterialBlend",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            nightMaterialCutoutTransMapPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSNightMaterialCutoutTransMap",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            nightMaterialBlendTransMapPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSNightMaterialBlendTransMap",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            terrainLayerPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSTerrainLayer",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            terrainBaseDetailPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSTerrainBaseDetail",
                    "ps_4_0");

        ReadOnlyMemory<byte>
            terrainLayerDetailPixelShaderBytecode =
                Compiler.CompileFromFile(
                    shaderPath,
                    "PSTerrainLayerDetail",
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

        _alphaCutoutPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    alphaCutoutPixelShaderBytecode
                        .Span);

        _alphaBlendPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    alphaBlendPixelShaderBytecode
                        .Span);

        _alphaCutoutTransMapPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    alphaCutoutTransMapPixelShaderBytecode
                        .Span);

        _alphaBlendTransMapPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    alphaBlendTransMapPixelShaderBytecode
                        .Span);

        _nightMaterialPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    nightMaterialPixelShaderBytecode
                        .Span);

        _nightMaterialCutoutPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    nightMaterialCutoutPixelShaderBytecode
                        .Span);

        _nightMaterialBlendPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    nightMaterialBlendPixelShaderBytecode
                        .Span);

        _nightMaterialCutoutTransMapPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    nightMaterialCutoutTransMapPixelShaderBytecode
                        .Span);

        _nightMaterialBlendTransMapPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    nightMaterialBlendTransMapPixelShaderBytecode
                        .Span);

        _terrainLayerPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    terrainLayerPixelShaderBytecode
                        .Span);

        _terrainBaseDetailPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    terrainBaseDetailPixelShaderBytecode
                        .Span);

        _terrainLayerDetailPixelShader =
            _deviceHost.Device
                .CreatePixelShader(
                    terrainLayerDetailPixelShaderBytecode
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
                    0),
                new(
                    "TEXCOORD",
                    2,
                    Format
                        .R32G32_Float,
                    44,
                    0),
                new(
                    "NORMAL",
                    0,
                    Format
                        .R32G32B32_Float,
                    52,
                    0),
                new(
                    "TANGENT",
                    0,
                    Format
                        .R32G32B32A32_Float,
                    64,
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
                    ViewportShaderConstants>();

        _materialPreviewBuffer =
            _deviceHost.Device
                .CreateConstantBuffer<
                    Vector4>();

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

        _depthReadState =
            _deviceHost.Device
                .CreateDepthStencilState(
                    DepthStencilDescription
                        .DepthRead);

        _depthDisabledState =
            _deviceHost.Device
                .CreateDepthStencilState(
                    DepthStencilDescription
                        .None);

        _alphaBlendState =
            _deviceHost.Device
                .CreateBlendState(
                    BlendDescription
                        .NonPremultiplied);

        _terrainRasterizerState =
            _deviceHost.Device
                .CreateRasterizerState(
                    RasterizerDescription
                        .CullNone);

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

    public bool NightPreviewEnabled =>
        _nightPreviewEnabled;

    public NativeSceneVisibility SceneVisibility =>
        _visibility;

    public NativeSelectionFilter SelectionFilter =>
        _selectionFilter;

    public bool GridVisible =>
        _gridVisible;

    public bool SplineProfilesVisible =>
        _splineProfilesVisible;

    public bool SetSplineProfilesVisible(
        bool visible)
    {
        ThrowIfDisposed();

        if (
            _splineProfilesVisible ==
                visible)
        {
            return false;
        }

        _splineProfilesVisible =
            visible;

        return true;
    }

    public bool SetGridVisible(
        bool visible)
    {
        ThrowIfDisposed();

        if (_gridVisible == visible)
        {
            return false;
        }

        _gridVisible =
            visible;

        return true;
    }

    public bool SetSceneVisibility(
        NativeSceneVisibility visibility)
    {
        ThrowIfDisposed();

        if (_visibility == visibility)
        {
            return false;
        }

        _visibility =
            visibility;

        if (
            !_visibility.IsPickingKindVisible(
                _hoverPickingId.Kind))
        {
            _hoverPickingId =
                PickingId.None;

            RebuildHover();
        }

        if (
            !_visibility.IsPickingKindVisible(
                _selectionPickingId.Kind))
        {
            _selectionPickingId =
                PickingId.None;

            RebuildSelection();
        }

        RebuildScenePickingVertices();

        return true;
    }

    public bool SetSelectionFilter(
        NativeSelectionFilter filter)
    {
        ThrowIfDisposed();

        if (_selectionFilter == filter)
        {
            return false;
        }

        _selectionFilter =
            filter;

        if (
            !IsPickingKindEnabled(
                _hoverPickingId.Kind))
        {
            _hoverPickingId =
                PickingId.None;

            RebuildHover();
        }

        if (
            !IsPickingKindEnabled(
                _selectionPickingId.Kind))
        {
            _selectionPickingId =
                PickingId.None;

            RebuildSelection();
        }

        RebuildScenePickingVertices();

        return true;
    }

    public void SetNightPreview(
        bool enabled)
    {
        ThrowIfDisposed();

        _nightPreviewEnabled =
            enabled;
    }

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

        _objectGuideBuffer?.Dispose();
        _objectGuideBuffer = null;
        _objectGuideVertexCount = 0;

        _splineGuideBuffer?.Dispose();
        _splineGuideBuffer = null;
        _splineGuideVertexCount = 0;

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

        _objectPickingVertices =
            Array.Empty<
                NativeMapVertex>();

        _objectRanges =
            new Dictionary<
                PickingId,
                NativeTriangleRange>();

        _splineVertices =
            Array.Empty<
                NativeMapVertex>();

        _splinePickingVertices =
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

        if (geometry.GridVertices.Length > 0)
        {
            _vertexBuffer =
                _deviceHost.Device
                    .CreateBuffer(
                        geometry.GridVertices
                            .AsSpan(),
                        BindFlags
                            .VertexBuffer);

            _vertexCount =
                geometry.GridVertices.Length;
        }

        if (
            geometry.ObjectGuideVertices.Length >
            0)
        {
            _objectGuideBuffer =
                _deviceHost.Device
                    .CreateBuffer(
                        geometry
                            .ObjectGuideVertices
                            .AsSpan(),
                        BindFlags
                            .VertexBuffer);

            _objectGuideVertexCount =
                geometry
                    .ObjectGuideVertices
                    .Length;
        }

        if (
            geometry.SplineGuideVertices.Length >
            0)
        {
            _splineGuideBuffer =
                _deviceHost.Device
                    .CreateBuffer(
                        geometry
                            .SplineGuideVertices
                            .AsSpan(),
                        BindFlags
                            .VertexBuffer);

            _splineGuideVertexCount =
                geometry
                    .SplineGuideVertices
                    .Length;
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

            _objectPickingVertices =
                objectGeometry
                    .PickingVertices;

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

            _splinePickingVertices =
                splineGeometry
                    .PickingVertices;

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

        RebuildScenePickingVertices();
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

                if (_visibility.TerrainVisible)
                {
                    DrawTerrainGeometry(
                        context);
                }

                if (
                    _visibility.SplinesVisible &&
                    _splineProfilesVisible)
                {
                    DrawSplineGeometry(
                        context);
                }

                if (_visibility.ObjectsVisible)
                {
                    DrawObjectGeometry(
                        context);
                }

                if (_gridVisible)
                {
                    DrawLineGeometry(
                        context,
                        _vertexBuffer,
                        _vertexCount);
                }

                if (_visibility.ObjectsVisible)
                {
                    DrawLineGeometry(
                        context,
                        _objectGuideBuffer,
                        _objectGuideVertexCount);
                }

                if (_visibility.SplinesVisible)
                {
                    DrawLineGeometry(
                        context,
                        _splineGuideBuffer,
                        _splineGuideVertexCount);
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

    private static void DrawLineGeometry(
        ID3D11DeviceContext context,
        ID3D11Buffer? buffer,
        int vertexCount)
    {
        if (
            buffer is null ||
            vertexCount <= 0)
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
                buffer,
                NativeMapVertex
                    .SizeInBytes);

        context.Draw(
            (uint)vertexCount,
            0);
    }

    private void DrawTerrainGeometry(
        ID3D11DeviceContext context)
    {
        DrawMaterialGeometry(
            context,
            _terrainTriangleBuffer,
            _terrainTriangleVertexCount,
            _terrainMaterialBatches,
            forceDoubleSided: true);
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
            NativeMaterialBatch> batches,
        bool forceDoubleSided = false)
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
                .OMSetDepthStencilState(
                    null);

            context
                .PSSetShader(
                    _pixelShader);

            context
                .RSSetState(
                    forceDoubleSided
                        ? _terrainRasterizerState
                        : null);

            context.Draw(
                (uint)vertexCount,
                0);

            context
                .RSSetState(
                    null);

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

            context
                .RSSetState(
                    forceDoubleSided ||
                    batch.DoubleSided
                        ? _terrainRasterizerState
                        : null);

            if (batch.NoZCheck)
            {
                context
                    .OMSetDepthStencilState(
                        _depthDisabledState);
            }
            else if (batch.NoZWrite)
            {
                context
                    .OMSetDepthStencilState(
                        _depthReadState);
            }
            else
            {
                context
                    .OMSetDepthStencilState(
                        null);
            }

            NativeGpuTexture? texture =
                null;

            var hasTexture =
                batch.TexturePath is
                    { Length: > 0 }
                    texturePath &&
                _textureCache
                    .TryGetValue(
                        texturePath,
                        out texture);

            NativeGpuTexture?
                detailTexture =
                    null;

            var hasDetailTexture =
                batch.DetailTexturePath is
                    { Length: > 0 }
                    detailPath &&
                _textureCache
                    .TryGetValue(
                        detailPath,
                        out detailTexture);

            NativeGpuTexture?
                bumpTexture =
                    null;

            var hasBumpTexture =
                batch.BumpTexturePath is
                    { Length: > 0 }
                    bumpPath &&
                _textureCache
                    .TryGetValue(
                        bumpPath,
                        out bumpTexture);

            NativeGpuTexture?
                environmentTexture =
                    null;

            var hasEnvironmentTexture =
                batch.EnvironmentTexturePath is
                    { Length: > 0 }
                    environmentPath &&
                _textureCache
                    .TryGetValue(
                        environmentPath,
                        out environmentTexture);

            ApplyMaterialPreview(
                context,
                batch,
                hasBumpTexture,
                hasEnvironmentTexture);

            if (hasBumpTexture)
            {
                context
                    .PSSetShaderResource(
                        4,
                        bumpTexture!.View);
            }
            else
            {
                context
                    .PSUnsetShaderResource(
                        4);
            }

            if (hasEnvironmentTexture)
            {
                context
                    .PSSetShaderResource(
                        5,
                        environmentTexture!.View);
            }
            else
            {
                context
                    .PSUnsetShaderResource(
                        5);
            }

            if (
                batch.MaskTexturePath is
                    { Length: > 0 }
                    maskPath)
            {
                var hasMask =
                    _textureCache
                        .TryGetValue(
                            maskPath,
                            out var maskTexture);

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
                        hasDetailTexture
                            ? _terrainLayerDetailPixelShader
                            : _terrainLayerPixelShader);

                context
                    .PSSetShaderResource(
                        0,
                        texture!.View);

                context
                    .PSSetShaderResource(
                        1,
                        maskTexture!.View);

                context
                    .PSUnsetShaderResource(
                        2);

                if (hasDetailTexture)
                {
                    context
                        .PSSetShaderResource(
                            3,
                            detailTexture!.View);
                }
                else
                {
                    context
                        .PSUnsetShaderResource(
                            3);
                }
            }
            else if (hasTexture)
            {
                var alphaMode =
                    batch.AlphaMode ??
                    0;

                context
                    .OMSetBlendState(
                        alphaMode == 2
                            ? _alphaBlendState
                            : null);

                NativeGpuTexture?
                    transMapTexture =
                        null;

                var hasTransMap =
                    batch.TransMapTexturePath is
                        { Length: > 0 }
                        transMapPath &&
                    _textureCache
                        .TryGetValue(
                            transMapPath,
                            out transMapTexture);

                if (hasTransMap)
                {
                    context
                        .PSSetShaderResource(
                            1,
                            transMapTexture!.View);
                }
                else
                {
                    context
                        .PSUnsetShaderResource(
                            1);
                }

                var secondaryPath =
                    _nightPreviewEnabled
                        ? batch.NightTexturePath ??
                            batch.LightTexturePath
                        : null;

                NativeGpuTexture?
                    secondaryTexture =
                        null;

                var hasSecondary =
                    secondaryPath is
                        { Length: > 0 } &&
                    _textureCache
                        .TryGetValue(
                            secondaryPath,
                            out secondaryTexture);

                ID3D11PixelShader shader;

                if (hasDetailTexture)
                {
                    shader =
                        _terrainBaseDetailPixelShader;

                    context
                        .PSSetShaderResource(
                            3,
                            detailTexture!.View);

                    context
                        .PSUnsetShaderResource(
                            2);
                }
                else if (hasSecondary)
                {
                    shader =
                        alphaMode switch
                        {
                            1 when hasTransMap =>
                                _nightMaterialCutoutTransMapPixelShader,
                            2 when hasTransMap =>
                                _nightMaterialBlendTransMapPixelShader,
                            1 =>
                                _nightMaterialCutoutPixelShader,
                            2 =>
                                _nightMaterialBlendPixelShader,
                            _ =>
                                _nightMaterialPixelShader
                        };

                    context
                        .PSSetShaderResource(
                            2,
                            secondaryTexture!.View);
                }
                else
                {
                    shader =
                        alphaMode switch
                        {
                            1 when hasTransMap =>
                                _alphaCutoutTransMapPixelShader,
                            2 when hasTransMap =>
                                _alphaBlendTransMapPixelShader,
                            1 =>
                                _alphaCutoutPixelShader,
                            2 =>
                                _alphaBlendPixelShader,
                            _ =>
                                _texturedPixelShader
                        };

                    context
                        .PSUnsetShaderResource(
                            2);

                    context
                        .PSUnsetShaderResource(
                            3);
                }

                context
                    .PSSetShader(
                        shader);

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
                    .PSUnsetShaderResource(
                        2);

                context
                    .PSUnsetShaderResource(
                        3);

                context
                    .PSUnsetShaderResource(
                        4);

                context
                    .PSUnsetShaderResource(
                        5);

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
            .OMSetDepthStencilState(
                null);

        context
            .RSSetState(
                null);

        context
            .PSUnsetShaderResource(
                0);

        context
            .PSUnsetShaderResource(
                1);

        context
            .PSUnsetShaderResource(
                2);

        context
            .PSUnsetShaderResource(
                3);

        context
            .PSUnsetShaderResource(
                4);

        context
            .PSUnsetShaderResource(
                5);

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

        var secondaryPaths =
            batches
                .SelectMany(
                    batch =>
                        new[]
                        {
                            batch.NightTexturePath,
                            batch.LightTexturePath,
                            batch.DetailTexturePath,
                            batch.TransMapTexturePath,
                            batch.BumpTexturePath,
                            batch.EnvironmentTexturePath
                        })
                .Where(
                    path =>
                        !string.IsNullOrWhiteSpace(
                            path))
                .Select(
                    path => path!)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase);

        var requested =
            diffusePaths
                .Concat(
                    maskPaths)
                .Concat(
                    secondaryPaths)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .Take(768)
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

    private void RebuildScenePickingVertices()
    {
        var vertices =
            new List<NativeMapVertex>(
                _proxyVertices.Length +
                _objectPickingVertices.Length +
                _splinePickingVertices.Length);

        foreach (
            var pair in _proxyRanges
                .OrderBy(
                    item =>
                        item.Value.StartVertex))
        {
            if (
                !IsPickingKindEnabled(
                    pair.Key.Kind) ||
                !IsValidRange(
                    _proxyVertices,
                    pair.Value))
            {
                continue;
            }

            vertices.AddRange(
                _proxyVertices
                    .AsSpan(
                        pair.Value.StartVertex,
                        pair.Value.VertexCount)
                    .ToArray());
        }

        if (
            _visibility.ObjectsVisible &&
            _selectionFilter.Allows(
                PickingKind.Object))
        {
            vertices.AddRange(
                _objectPickingVertices);
        }

        if (
            _visibility.SplinesVisible &&
            _selectionFilter.Allows(
                PickingKind.Spline))
        {
            vertices.AddRange(
                _splinePickingVertices);
        }

        _scenePickingVertices =
            vertices.ToArray();

        RebuildPickingBuffer();
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
            !pickingId.IsNone &&
            !IsPickingKindEnabled(
                pickingId.Kind))
        {
            pickingId =
                PickingId.None;
        }
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
            !pickingId.IsNone &&
            !IsPickingKindEnabled(
                pickingId.Kind))
        {
            pickingId =
                PickingId.None;
        }
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
            !IsPickingKindEnabled(
                pickingId.Kind))
        {
            sourceVertices =
                Array.Empty<
                    NativeMapVertex>();

            range =
                default;

            return false;
        }

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

    private bool IsPickingKindEnabled(
        PickingKind kind)
    {
        if (kind == PickingKind.None)
        {
            return true;
        }

        return
            _visibility
                .IsPickingKindVisible(
                    kind) &&
            _selectionFilter
                .Allows(kind);
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
        Span<ViewportShaderConstants>
            data =
                stackalloc
                    ViewportShaderConstants[1];

        data[0] =
            new ViewportShaderConstants
            {
                ViewProjection =
                    viewProjection,
                CameraPosition =
                    new Vector4(
                        _cameraPosition,
                        1.0f)
            };

        _viewProjectionBuffer
            .SetData(
                context,
                data,
                MapMode.WriteDiscard);

        context
            .VSSetConstantBuffer(
                0,
                _viewProjectionBuffer);

        context
            .PSSetConstantBuffer(
                0,
                _viewProjectionBuffer);
    }

    private void ApplyMaterialPreview(
        ID3D11DeviceContext context,
        NativeMaterialBatch batch,
        bool hasBumpTexture,
        bool hasEnvironmentTexture)
    {
        Span<Vector4> data =
            stackalloc Vector4[1];

        data[0] =
            new Vector4(
                hasBumpTexture
                    ? (float)Math.Clamp(
                        batch.BumpStrength ??
                            1.0,
                        0.0,
                        4.0)
                    : 0.0f,
                hasEnvironmentTexture
                    ? (float)Math.Clamp(
                        batch.EnvironmentStrength ??
                            1.0,
                        0.0,
                        1.0)
                    : 0.0f,
                hasBumpTexture
                    ? 1.0f
                    : 0.0f,
                hasEnvironmentTexture
                    ? 1.0f
                    : 0.0f);

        _materialPreviewBuffer
            .SetData(
                context,
                data,
                MapMode.WriteDiscard);

        context
            .PSSetConstantBuffer(
                1,
                _materialPreviewBuffer);
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

        _splineGuideBuffer?.Dispose();
        _objectGuideBuffer?.Dispose();
        _vertexBuffer?.Dispose();
        _materialPreviewBuffer.Dispose();
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
        _terrainRasterizerState.Dispose();
        _alphaBlendState.Dispose();
        _depthDisabledState.Dispose();
        _depthReadState.Dispose();
        _skyDepthState.Dispose();
        _skySampler.Dispose();
        _maskSampler.Dispose();
        _textureSampler.Dispose();
        _inputLayout.Dispose();
        _terrainLayerDetailPixelShader.Dispose();
        _terrainBaseDetailPixelShader.Dispose();
        _terrainLayerPixelShader.Dispose();
        _nightMaterialBlendTransMapPixelShader.Dispose();
        _nightMaterialCutoutTransMapPixelShader.Dispose();
        _nightMaterialBlendPixelShader.Dispose();
        _nightMaterialCutoutPixelShader.Dispose();
        _nightMaterialPixelShader.Dispose();
        _alphaBlendTransMapPixelShader.Dispose();
        _alphaCutoutTransMapPixelShader.Dispose();
        _alphaBlendPixelShader.Dispose();
        _alphaCutoutPixelShader.Dispose();
        _texturedPixelShader.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
    }
}
