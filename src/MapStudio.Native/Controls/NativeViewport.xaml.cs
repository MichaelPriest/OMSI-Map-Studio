using System.Numerics;
using System.Runtime.InteropServices;
using MapStudio.Native.Interop;
using MapStudio.Native.Services;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Renderer.Scene;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Viewport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MapStudio.Native.Controls;

public sealed partial class NativeViewport : UserControl
{
    private NativeViewportRuntime? _runtime;
    private bool _leftPressed;
    private bool _isPanning;
    private bool _isOrbiting;
    private bool _isManipulatingGizmo;
    private double _lastPanX;
    private double _lastPanY;
    private long _lastHoverTick;
    private uint _lastHoverPixelX =
        uint.MaxValue;
    private uint _lastHoverPixelY =
        uint.MaxValue;
    private bool _swapChainBound;
    private bool _terrainPointPickActive;
    private bool _terrainPointPickPersistent;

    public NativeViewport()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;

        SwapChainSurface
            .CompositionScaleChanged +=
            OnCompositionScaleChanged;
    }

    public event EventHandler<string>? PointerStatusChanged;

    public event EventHandler<string>? SelectionStatusChanged;

    public event Action<
        NativePendingTransformEdit>?
        TransformEditPending;

    public event Action<
        NativeSelectionInfo?>?
        SelectionChanged;

    public event Action<
        NativeSceneryPlacementRequest>?
        SceneryPlacementRequested;

    public event Action<
        NativeSplinePlacementRequest>?
        SplinePlacementRequested;

    public event Action<
        NativeTerrainEditPoint>?
        TerrainPointSelected;

    public int PreviewTimetableTrack(
        IReadOnlyList<
            MapStudio.Core.Omsi.Timetables
                .OmsiTimetableTrackEntry>
            entries) =>
        _runtime
            ?.PreviewTimetableTrack(
                entries) ??
        0;

    public int PreviewStationLink(
        IReadOnlyList<
            MapStudio.Core.Omsi.Timetables
                .OmsiStationLinkEntry>
            entries) =>
        _runtime
            ?.PreviewStationLink(
                entries) ??
        0;

    public void ClearTimetableRoutePreview() =>
        _runtime
            ?.ClearTimetableRoutePreview();

    public IReadOnlyList<
        NativeTrafficLightProgramInfo>
        GetTrafficLightPrograms() =>
        _runtime
            ?.GetTrafficLightPrograms() ??
        Array.Empty<
            NativeTrafficLightProgramInfo>();

    public IReadOnlyList<
        NativeExplorerItem>
        GetExplorerItems() =>
        _runtime
            ?.GetExplorerItems() ??
        Array.Empty<
            NativeExplorerItem>();

    public bool SelectExplorerItem(
        NativeExplorerItem item,
        bool focus)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        var info =
            _runtime
                ?.SelectExplorerItem(
                    item.PickingId,
                    focus);

        if (info is null)
        {
            return false;
        }

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            info.Kind ==
                MapStudio.Renderer.Picking
                    .PickingKind.Object
                ? $"Objeto #{info.EntityId} selecionado pelo Explorer."
                : $"Spline #{info.EntityId} selecionada pelo Explorer.");

        return true;
    }

    public bool CanUndo =>
        _runtime?.CanUndo ??
        false;

    public bool CanRedo =>
        _runtime?.CanRedo ??
        false;

    public bool SnapEnabled =>
        _runtime?.SnapEnabled ??
        false;

    public void SetSnapEnabled(
        bool enabled)
    {
        _runtime?.SetSnapEnabled(
            enabled);

        PointerStatusChanged?.Invoke(
            this,
            enabled
                ? "Snap ativo: 0,25 m / 5°"
                : "Snap desativado");
    }

    public bool Undo()
    {
        var edit =
            _runtime?.Undo();

        if (edit is null)
        {
            return false;
        }

        TransformEditPending
            ?.Invoke(
                edit);

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            "Última transformação desfeita.");

        return true;
    }

    public bool ApplySelectionInfo(
        NativeSelectionInfo values)
    {
        var edit =
            _runtime
                ?.ApplySelectionInfo(
                    values);

        if (edit is null)
        {
            return false;
        }

        TransformEditPending
            ?.Invoke(
                edit);

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            edit.IsObject
                ? "Valores do objeto aplicados pelo Inspector."
                : "Valores da spline aplicados pelo Inspector.");

        return true;
    }

    public bool LevelSelectedSplineToTerrain(
        out double startHeight,
        out double endHeight)
    {
        startHeight = 0.0;
        endHeight = 0.0;

        var edit =
            _runtime
                ?.LevelSelectedSplineToTerrain(
                    out startHeight,
                    out endHeight);

        if (edit is null)
        {
            return false;
        }

        TransformEditPending
            ?.Invoke(
                edit);

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            $"Spline nivelada ao terreno: {startHeight:F2} → {endHeight:F2} m.");

        return true;
    }

    public bool Redo()
    {
        var edit =
            _runtime?.Redo();

        if (edit is null)
        {
            return false;
        }

        TransformEditPending
            ?.Invoke(
                edit);

        PublishSelectionInfo();

        SelectionStatusChanged?.Invoke(
            this,
            "Transformação refeita.");

        return true;
    }

    public bool IsSceneryPlacementActive =>
        _runtime
            ?.IsSceneryPlacementActive ??
        false;

    public bool IsSplinePlacementActive =>
        _runtime
            ?.IsSplinePlacementActive ??
        false;

    public void SetSplinePlacementElevationOffset(
        double offset) =>
        _runtime
            ?.SetSplinePlacementElevationOffset(
                offset);

    public void SetSplinePlacementHeightMode(
        bool isHeightSpline) =>
        _runtime
            ?.SetSplinePlacementHeightMode(
                isHeightSpline);

    public void SetSplineEasyRoadOptions(
        bool enabled,
        double curveOffset) =>
        _runtime
            ?.SetSplineEasyRoadOptions(
                enabled,
                curveOffset);

    public void SetSplineEndpointSnapOptions(
        bool enabled,
        double distance,
        bool autoConnect) =>
        _runtime
            ?.SetSplineEndpointSnapOptions(
                enabled,
                distance,
                autoConnect);

    public bool SeedSplinePlacementStart(
        System.Numerics.Vector3 start,
        int previousSplineId =
            -1) =>
        _runtime
            ?.SeedSplinePlacementStart(
                start,
                previousSplineId) ??
        false;

    public async Task<bool>
        BeginSplinePlacementAsync(
            string omsiRoot,
            OmsiAssetIndexEntry asset,
            bool curved,
            CancellationToken cancellationToken =
                default)
    {
        if (
            _runtime is null ||
            asset.Kind !=
                OmsiAssetKind.Spline)
        {
            return false;
        }

        var started =
            await _runtime
                .BeginSplinePlacementAsync(
                    omsiRoot,
                    asset.RelativePath,
                    curved,
                    cancellationToken);

        if (started)
        {
            RuntimeText.Text =
                $"Construindo spline · {asset.RelativePath}";

            SelectionStatusChanged?.Invoke(
                this,
                curved
                    ? "Curva: clique início, fim e ponto de curvatura."
                    : "Reta: clique início e fim.");
        }

        return started;
    }

    public async Task<bool>
        BeginSplinePlacementCopyAsync(
            string omsiRoot,
            string splinePath,
            bool curved,
            CancellationToken cancellationToken =
                default)
    {
        if (_runtime is null)
        {
            return false;
        }

        _runtime.SetSplineEndpointSnapOptions(
            enabled: false,
            distance: 5.0,
            autoConnect: false);

        _runtime.SetSplinePlacementElevationOffset(
            0.0);

        _runtime.SetSplineEasyRoadOptions(
            enabled: false,
            curveOffset: 0.0);

        var started =
            await _runtime
                .BeginSplinePlacementAsync(
                    omsiRoot,
                    splinePath,
                    curved,
                    cancellationToken);

        if (started)
        {
            RuntimeText.Text =
                $"Construindo cópia · {splinePath}";

            SelectionStatusChanged?.Invoke(
                this,
                curved
                    ? "Cópia curva desconectada: clique início, fim e ponto de curvatura."
                    : "Cópia reta desconectada: clique início e fim.");
        }

        return started;
    }

    public void CancelSplinePlacement()
    {
        _runtime?.CancelSplinePlacement();
    }

    public async Task<bool>
        BeginSceneryPlacementAsync(
            string omsiRoot,
            OmsiAssetIndexEntry asset,
            CancellationToken cancellationToken =
                default)
    {
        if (
            _runtime is null ||
            asset.Kind !=
                OmsiAssetKind
                    .SceneryObject)
        {
            return false;
        }

        var started =
            await _runtime
                .BeginSceneryPlacementAsync(
                    omsiRoot,
                    asset.RelativePath,
                    cancellationToken);

        if (started)
        {
            RuntimeText.Text =
                $"Posicionando · {asset.RelativePath}";

            SelectionStatusChanged?.Invoke(
                this,
                "Mova o cursor sobre o terreno e clique para posicionar o objeto.");
        }

        return started;
    }

    public async Task<bool>
        BeginSceneryPlacementCopyAsync(
            string omsiRoot,
            string sceneryObjectPath,
            double z,
            double rotation,
            double pitch,
            double bank,
            CancellationToken cancellationToken =
                default)
    {
        if (_runtime is null)
        {
            return false;
        }

        var started =
            await _runtime
                .BeginSceneryPlacementAsync(
                    omsiRoot,
                    sceneryObjectPath,
                    z,
                    rotation,
                    pitch,
                    bank,
                    cancellationToken);

        if (started)
        {
            RuntimeText.Text =
                $"Posicionando cópia · {sceneryObjectPath}";

            SelectionStatusChanged?.Invoke(
                this,
                "Cópia ativa: o próximo clique define X/Y e preserva Z, rotação, pitch e bank.");
        }

        return started;
    }

    public void BeginTerrainPointPick()
    {
        _runtime?.CancelSceneryPlacement();
        _runtime?.CancelSplinePlacement();

        _terrainPointPickPersistent =
            false;

        _terrainPointPickActive =
            true;

        PointerStatusChanged?.Invoke(
            this,
            "Terreno: clique no ponto que deseja nivelar.");
    }

    public void BeginTerrainSelectionMode()
    {
        _runtime?.CancelSceneryPlacement();
        _runtime?.CancelSplinePlacement();

        _terrainPointPickPersistent =
            true;

        _terrainPointPickActive =
            true;

        PointerStatusChanged?.Invoke(
            this,
            "Seleção de terreno ativa: clique em qualquer tile.");
    }

    public void CancelTerrainPointPick()
    {
        _terrainPointPickActive =
            false;

        _terrainPointPickPersistent =
            false;
    }

    public void CancelSceneryPlacement()
    {
        _runtime
            ?.CancelSceneryPlacement();
    }

    public async Task<NativeAssetPreviewResult?>
        PreviewAssetAsync(
            string omsiRoot,
            OmsiAssetIndexEntry asset,
            CancellationToken cancellationToken =
                default)
    {
        if (_runtime is null)
        {
            return null;
        }

        var result =
            await _runtime
                .PreviewAssetAsync(
                    omsiRoot,
                    asset.Kind,
                    asset.RelativePath,
                    cancellationToken);

        if (result.IsRenderable)
        {
            RuntimeText.Text =
                $"Prévia 3D · {asset.RelativePath} · " +
                $"{result.TriangleCount} triângulos";
        }

        return result;
    }

    public bool TryFinishSceneryPlacementAtPoint(
        double x,
        double y,
        out NativeSceneryPlacementRequest?
            request)
    {
        request =
            null;

        if (
            _runtime is null ||
            !TryConvertPointToPixels(
                x,
                y,
                out var pixelX,
                out var pixelY) ||
            !_runtime.UpdateSceneryPlacement(
                pixelX,
                pixelY))
        {
            return false;
        }

        return _runtime
            .TryFinishSceneryPlacement(
                out request);
    }

    public bool TrySeedSplinePlacementAtPoint(
        double x,
        double y,
        out string status)
    {
        status =
            "Não foi possível iniciar a spline neste ponto.";

        if (
            _runtime is null ||
            !_runtime.IsSplinePlacementActive ||
            !TryConvertPointToPixels(
                x,
                y,
                out var pixelX,
                out var pixelY))
        {
            return false;
        }

        if (
            !_runtime.TryAdvanceSplinePlacement(
                pixelX,
                pixelY,
                out var request,
                out status))
        {
            return false;
        }

        return request is null;
    }

    private bool TryConvertPointToPixels(
        double x,
        double y,
        out uint pixelX,
        out uint pixelY)
    {
        pixelX = 0;
        pixelY = 0;

        if (
            !double.IsFinite(x) ||
            !double.IsFinite(y) ||
            x < 0 ||
            y < 0)
        {
            return false;
        }

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        pixelX =
            (uint)Math.Max(
                0,
                Math.Round(
                    x *
                    scaleX));

        pixelY =
            (uint)Math.Max(
                0,
                Math.Round(
                    y *
                    scaleY));

        return true;
    }

    public bool TryFinishSceneryPlacementAtWorldPoint(
        Vector3 worldPoint,
        double rotation,
        out NativeSceneryPlacementRequest?
            request)
    {
        request =
            null;

        return _runtime
            ?.TryFinishSceneryPlacementAtWorldPoint(
                worldPoint,
                rotation,
                out request) ??
            false;
    }

    public void RestoreSceneView()
    {
        _runtime?.RestoreSceneView();

        PublishSelectionInfo();
    }

    public bool FitScene()
    {
        var fitted =
            _runtime
                ?.FitScene() ??
            false;

        if (fitted)
        {
            PointerStatusChanged?.Invoke(
                this,
                "Mapa enquadrado.");
        }

        return fitted;
    }

    public IReadOnlyList<
        NativeJunctionSuggestion>
        GetJunctionSuggestions() =>
        _runtime
            ?.GetJunctionSuggestions() ??
        Array.Empty<
            NativeJunctionSuggestion>();

    public bool FocusWorldPoint(
        Vector3 point,
        float span = 45.0f)
    {
        var focused =
            _runtime
                ?.FocusWorldPoint(
                    point,
                    span) ??
            false;

        if (focused)
        {
            PointerStatusChanged?.Invoke(
                this,
                $"Ponto focado · {point.X:F1}, {point.Z:F1}.");
        }

        return focused;
    }

    public bool FocusSelection()
    {
        var focused =
            _runtime
                ?.FocusSelection() ??
            false;

        if (focused)
        {
            PointerStatusChanged?.Invoke(
                this,
                "Seleção focada.");
        }

        return focused;
    }

    public bool FocusTile(
        int tileX,
        int tileY)
    {
        var focused =
            _runtime
                ?.FocusTile(
                    tileX,
                    tileY) ??
                false;

        if (focused)
        {
            PointerStatusChanged?.Invoke(
                this,
                $"Tile {tileX},{tileY} focado.");
        }

        return focused;
    }

    public void SetTopView()
    {
        _runtime?.SetTopView();

        PointerStatusChanged?.Invoke(
            this,
            "Câmera: vista superior.");
    }

    public void SetPerspectiveView()
    {
        _runtime?.SetPerspectiveView();

        PointerStatusChanged?.Invoke(
            this,
            "Câmera: perspectiva.");
    }

    public bool SetSceneVisibility(
        NativeSceneVisibility visibility)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime.SetSceneVisibility(
                visibility);

        if (changed)
        {
            PublishSelectionInfo();

            SelectionStatusChanged?.Invoke(
                this,
                $"Visibilidade · terreno {(visibility.TerrainVisible ? "on" : "off")} · " +
                $"objetos {(visibility.ObjectsVisible ? "on" : "off")} · " +
                $"splines {(visibility.SplinesVisible ? "on" : "off")}");
        }

        return changed;
    }

    public bool SetTerrainPaintVisible(
        bool visible) =>
        _runtime
            ?.SetTerrainPaintVisible(
                visible) ??
        false;

    public bool SetTerrainLayerVisible(
        int layerIndex,
        bool visible) =>
        _runtime
            ?.SetTerrainLayerVisible(
                layerIndex,
                visible) ??
        false;

    public bool ResetTerrainLayerVisibility() =>
        _runtime
            ?.ResetTerrainLayerVisibility() ??
        false;

    public bool SetSelectionFilter(
        NativeSelectionFilter filter)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime.SetSelectionFilter(
                filter);

        if (changed)
        {
            PublishSelectionInfo();

            SelectionStatusChanged?.Invoke(
                this,
                $"Filtro de seleção: {filter}.");
        }

        return changed;
    }

    public bool SetSplineProfilesVisible(
        bool visible)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime
                .SetSplineProfilesVisible(
                    visible);

        if (changed)
        {
            PointerStatusChanged?.Invoke(
                this,
                visible
                    ? "Perfis reais das splines visíveis."
                    : "Perfis reais das splines ocultos.");
        }

        return changed;
    }

    public bool TrafficPathsVisible =>
        _runtime?.TrafficPathsVisible ??
        false;

    public int TrafficPathLineCount =>
        _runtime?.TrafficPathLineCount ??
        0;

    public NativeAssetTechnicalSnapshot
        GetAssetTechnicalSnapshot() =>
        _runtime
            ?.GetAssetTechnicalSnapshot() ??
        NativeAssetTechnicalSnapshot.Empty;

    public bool SetTrafficPathsVisible(
        bool visible)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime
                .SetTrafficPathsVisible(
                    visible);

        if (changed)
        {
            PointerStatusChanged?.Invoke(
                this,
                visible
                    ? $"Paths OMSI visíveis · {_runtime.TrafficPathLineCount} linhas."
                    : "Paths OMSI ocultos.");
        }

        return changed;
    }

    public bool SetGridVisible(
        bool visible)
    {
        if (_runtime is null)
        {
            return false;
        }

        var changed =
            _runtime.SetGridVisible(
                visible);

        if (changed)
        {
            PointerStatusChanged?.Invoke(
                this,
                visible
                    ? "Grade do mapa visível."
                    : "Grade do mapa oculta.");
        }

        return changed;
    }

    public void SetGizmoMode(
        NativeGizmoMode mode)
    {
        _runtime?.SetGizmoMode(
            mode);

        PointerStatusChanged?.Invoke(
            this,
            mode ==
                NativeGizmoMode.Move
                ? "Gizmo: mover"
                : "Gizmo: rotacionar");
    }

    public bool SetNightPreview(
        bool enabled)
    {
        if (_runtime is null)
        {
            return false;
        }

        _runtime.SetNightPreview(
            enabled);

        return _runtime.HasSkyTexture;
    }

    public void SetReferenceOverlay(
        NativeReferenceOverlayDefinition?
            overlay)
    {
        _runtime?.SetReferenceOverlay(
            overlay);

        PointerStatusChanged?.Invoke(
            this,
            overlay is null
                ? "Referência geográfica removida."
                : $"Referência geográfica ativa · {overlay.Attribution} · opacidade {overlay.Opacity:P0}.");
    }

    public async Task SetMapSnapshotAsync(
        NativeMapSnapshot snapshot,
        string omsiRoot,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        if (_runtime is null)
        {
            SelectionStatusChanged?.Invoke(
                this,
                "Viewport Direct3D ainda não inicializado.");
            return;
        }

        SelectionStatusChanged?.Invoke(
            this,
            "Carregando SCO/O3D reais no renderer nativo...");

        var scene =
            await _runtime
                .LoadSceneAsync(
                    snapshot.Tiles
                        .Select(
                            tile =>
                                new NativeSceneTile(
                                    tile.Reference,
                                    tile.Content))
                        .ToArray(),
                    snapshot.Map,
                    omsiRoot,
                    cancellationToken);

        _runtime.RenderInitialFrame();

        RuntimeText.Text =
            $"{snapshot.Map.DisplayName} · {scene.Tiles.Count} tiles · " +
            $"{scene.Objects.Count} objetos · {scene.Splines.Count} splines · " +
            $"{_runtime.MapRenderer.TerrainTriangleVertexCount / 3} triângulos terreno · " +
            $"{_runtime.MapRenderer.SplineTriangleVertexCount / 3} triângulos spline · " +
            $"{_runtime.MapRenderer.ObjectTriangleVertexCount / 3} triângulos O3D";

        SelectionStatusChanged?.Invoke(
            this,
            $"Assets nativos: {_runtime.LoadedSceneryAssetCount} SCO · " +
            $"{_runtime.LoadedObjectMeshCount} meshes · " +
            $"{_runtime.MapRenderer.LoadedTextureCount} texturas O3D · " +
            $"{_runtime.LoadedSplineAssetCount} SLI · " +
            $"{_runtime.LoadedSplineSurfaceCount} superfícies spline · " +
            $"{_runtime.TrafficPathLineCount} linhas de path · " +
            $"{_runtime.SceneryLightPointCount} pontos de luz · " +
            $"{_runtime.TrafficLightProgramCount} programas de semáforo · " +
            $"{scene.SelectableCount} IDs de seleção.");
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            _runtime ??=
                new NativeViewportRuntime();

            EnsureNativeSurface();
            BindSwapChain();
            _runtime.RenderInitialFrame();

            RuntimeText.Text =
                $"Direct3D 11 ativo · feature level {_runtime.Device.FeatureLevel} · " +
                $"{_runtime.Surface?.Width}×{_runtime.Surface?.Height}";
        }
        catch (Exception exception)
        {
            RuntimeText.Text =
                $"Falha ao iniciar Direct3D 11: {exception.Message}";
        }
    }

    private void EnsureNativeSurface()
    {
        if (_runtime is null)
        {
            return;
        }

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        var width =
            (uint)Math.Max(
                1,
                Math.Round(
                    SwapChainSurface
                        .ActualWidth *
                    scaleX));

        var height =
            (uint)Math.Max(
                1,
                Math.Round(
                    SwapChainSurface
                        .ActualHeight *
                    scaleY));

        _runtime.EnsureSurface(
            width,
            height);
    }

    private void BindSwapChain()
    {
        if (
            _runtime is null ||
            _runtime.SwapChainPointer ==
                IntPtr.Zero)
        {
            return;
        }

        var nativePanel =
            WinRT.CastExtensions.As<
                WinUISwapChainPanelInterop
                    .ISwapChainPanelNative>(
                SwapChainSurface);

        var result =
            nativePanel.SetSwapChain(
                _runtime
                    .SwapChainPointer);

        Marshal.ThrowExceptionForHR(
            result);

        _swapChainBound = true;
    }

    private void UnbindSwapChain()
    {
        if (!_swapChainBound)
        {
            return;
        }

        var nativePanel =
            WinRT.CastExtensions.As<
                WinUISwapChainPanelInterop
                    .ISwapChainPanelNative>(
                SwapChainSurface);

        var result =
            nativePanel.SetSwapChain(
                IntPtr.Zero);

        Marshal.ThrowExceptionForHR(
            result);

        _swapChainBound = false;
    }

    private void ResizeAndRender()
    {
        if (
            _runtime is null ||
            !IsLoaded)
        {
            return;
        }

        EnsureNativeSurface();
        _runtime.RenderInitialFrame();

        RuntimeText.Text =
            $"Direct3D 11 ativo · feature level {_runtime.Device.FeatureLevel} · " +
            $"{_runtime.Surface?.Width}×{_runtime.Surface?.Height}";
    }

    private void OnSizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        try
        {
            ResizeAndRender();
        }
        catch (Exception exception)
        {
            RuntimeText.Text =
                $"Falha ao redimensionar viewport: {exception.Message}";
        }
    }

    private void OnCompositionScaleChanged(
        SwapChainPanel sender,
        object args)
    {
        try
        {
            ResizeAndRender();
        }
        catch (Exception exception)
        {
            RuntimeText.Text =
                $"Falha ao aplicar escala de DPI: {exception.Message}";
        }
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            UnbindSwapChain();
        }
        finally
        {
            _runtime?.Dispose();
            _runtime = null;
        }
    }

    private void OnPointerPressed(
        object sender,
        PointerRoutedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(
                InputSurface);

        var panPressed =
            point.Properties
                .IsMiddleButtonPressed;

        var orbitPressed =
            point.Properties
                .IsRightButtonPressed;

        if (
            panPressed ||
            orbitPressed)
        {
            _isPanning =
                panPressed;

            _isOrbiting =
                orbitPressed;
            _lastPanX =
                point.Position.X;
            _lastPanY =
                point.Position.Y;

            _runtime?.ClearHover();

            InputSurface.CapturePointer(
                e.Pointer);

            PointerStatusChanged?.Invoke(
                this,
                _isOrbiting
                    ? "Órbita 3D nativa ativa"
                    : "Pan 3D nativo ativo");

            e.Handled = true;
            return;
        }

        _leftPressed =
            point.Properties
                .IsLeftButtonPressed;

        if (!_leftPressed)
        {
            return;
        }

        InputSurface.CapturePointer(
            e.Pointer);

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        var pixelX =
            (uint)Math.Max(
                0,
                Math.Round(
                    point.Position.X *
                    scaleX));

        var pixelY =
            (uint)Math.Max(
                0,
                Math.Round(
                    point.Position.Y *
                    scaleY));

        if (
            _terrainPointPickActive &&
            _runtime is not null)
        {
            if (
                !_terrainPointPickPersistent)
            {
                _terrainPointPickActive =
                    false;
            }

            if (
                _runtime.TryGetTerrainEditPoint(
                    pixelX,
                    pixelY,
                    out var terrainPoint) &&
                terrainPoint is not null)
            {
                TerrainPointSelected
                    ?.Invoke(
                        terrainPoint);

                PointerStatusChanged?.Invoke(
                    this,
                    $"Terreno selecionado em tile {terrainPoint.Tile.X},{terrainPoint.Tile.Y} · " +
                    $"altura {terrainPoint.Height:F2} m.");
            }
            else
            {
                PointerStatusChanged?.Invoke(
                    this,
                    "Não foi possível selecionar terreno neste ponto.");
            }

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.IsSplinePlacementActive)
        {
            if (
                _runtime.TryAdvanceSplinePlacement(
                    pixelX,
                    pixelY,
                    out var splinePlacement,
                    out var splineStatus))
            {
                if (
                    splinePlacement is not null)
                {
                    SplinePlacementRequested
                        ?.Invoke(
                            splinePlacement);
                }

                PointerStatusChanged?.Invoke(
                    this,
                    splineStatus);
            }

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.IsSceneryPlacementActive)
        {
            _runtime.UpdateSceneryPlacement(
                pixelX,
                pixelY);

            if (
                _runtime.TryFinishSceneryPlacement(
                    out var placement) &&
                placement is not null)
            {
                SceneryPlacementRequested
                    ?.Invoke(
                        placement);

                PointerStatusChanged?.Invoke(
                    this,
                    $"Posicionamento solicitado em {placement.WorldPoint.X:F2}, " +
                    $"{placement.WorldPoint.Y:F2}, {placement.WorldPoint.Z:F2}.");
            }

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.TryBeginGizmoDrag(
                pixelX,
                pixelY,
                out var gizmoHandle))
        {
            _isManipulatingGizmo =
                true;

            InputSurface.CapturePointer(
                e.Pointer);

            PointerStatusChanged?.Invoke(
                this,
                $"Gizmo ativo: {gizmoHandle}");

            e.Handled = true;
            return;
        }

        var message =
            $"Clique nativo: X={point.Position.X:F1} Y={point.Position.Y:F1} · " +
            $"pixel {pixelX},{pixelY}";

        PointerStatusChanged?.Invoke(
            this,
            message);

        if (
            _runtime is not null &&
            _runtime.TryPick(
                pixelX,
                pixelY,
                out var pickingId,
                out var selected))
        {
            SelectionStatusChanged?.Invoke(
                this,
                selected switch
                {
                    OmsiPlacedObject item =>
                        $"Objeto #{item.ObjectId} · {item.SceneryObjectPath} · ID {pickingId.Value}",
                    OmsiPlacedSpline item =>
                        $"Spline #{item.SplineId} · {item.SplinePath} · ID {pickingId.Value}",
                    _ =>
                        $"{pickingId.Kind} · ID {pickingId.Value}"
                });
        }
        else
        {
            SelectionStatusChanged?.Invoke(
                this,
                "Sem seleção.");
        }

        PublishSelectionInfo();

        e.Handled = true;
    }

    private void OnPointerMoved(
        object sender,
        PointerRoutedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(
                InputSurface);

        PointerText.Text =
            $"x: {point.Position.X:F0} · y: {point.Position.Y:F0}";

        if (
            _isManipulatingGizmo &&
            _runtime is not null)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            _runtime.UpdateGizmoDrag(
                pixelX,
                pixelY);

            PointerStatusChanged?.Invoke(
                this,
                _runtime.GizmoMode ==
                    NativeGizmoMode.Move
                    ? "Movendo seleção..."
                    : "Rotacionando seleção...");

            e.Handled = true;
            return;
        }

        if (
            (
                _isPanning ||
                _isOrbiting
            ) &&
            _runtime is not null)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var deltaX =
                (
                    point.Position.X -
                    _lastPanX
                ) *
                scaleX;

            var deltaY =
                (
                    point.Position.Y -
                    _lastPanY
                ) *
                scaleY;

            _lastPanX =
                point.Position.X;

            _lastPanY =
                point.Position.Y;

            if (_isOrbiting)
            {
                _runtime.Orbit(
                    deltaX,
                    deltaY);
            }
            else
            {
                _runtime.Pan(
                    deltaX,
                    deltaY);
            }

            PointerStatusChanged?.Invoke(
                this,
                _isOrbiting
                    ? $"Órbita · zoom {_runtime.Navigation.Zoom:F2}×"
                    : $"Pan · zoom {_runtime.Navigation.Zoom:F2}×");

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.IsSplinePlacementActive)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            if (
                _runtime.UpdateSplinePlacement(
                    pixelX,
                    pixelY))
            {
                PointerStatusChanged?.Invoke(
                    this,
                    _runtime.SplinePlacementStage switch
                    {
                        NativeSplinePlacementStage.AwaitingStart =>
                            "Spline: clique no ponto inicial.",
                        NativeSplinePlacementStage.AwaitingEnd =>
                            "Spline: clique no ponto final.",
                        NativeSplinePlacementStage.AwaitingCurve =>
                            "Spline: ajuste a curva e clique para confirmar.",
                        _ =>
                            "Construindo spline..."
                    });
            }

            e.Handled = true;
            return;
        }

        if (
            _runtime is not null &&
            _runtime.IsSceneryPlacementActive)
        {
            var scaleX =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleX);

            var scaleY =
                Math.Max(
                    0.01,
                    SwapChainSurface
                        .CompositionScaleY);

            var pixelX =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.X *
                        scaleX));

            var pixelY =
                (uint)Math.Max(
                    0,
                    Math.Round(
                        point.Position.Y *
                        scaleY));

            if (
                _runtime.UpdateSceneryPlacement(
                    pixelX,
                    pixelY))
            {
                PointerStatusChanged?.Invoke(
                    this,
                    "Posicionamento: clique para inserir · botão do meio pan · botão direito órbita");
            }

            e.Handled = true;
            return;
        }

        if (_leftPressed)
        {
            PointerStatusChanged?.Invoke(
                this,
                $"Pointer capturado: X={point.Position.X:F1} Y={point.Position.Y:F1}");

            return;
        }

        UpdateHover(
            point.Position.X,
            point.Position.Y);
    }

    private void UpdateHover(
        double x,
        double y)
    {
        if (_runtime is null)
        {
            return;
        }

        var scaleX =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleX);

        var scaleY =
            Math.Max(
                0.01,
                SwapChainSurface
                    .CompositionScaleY);

        var pixelX =
            (uint)Math.Max(
                0,
                Math.Round(
                    x *
                    scaleX));

        var pixelY =
            (uint)Math.Max(
                0,
                Math.Round(
                    y *
                    scaleY));

        var now =
            Environment
                .TickCount64;

        if (
            pixelX ==
                _lastHoverPixelX &&
            pixelY ==
                _lastHoverPixelY)
        {
            return;
        }

        if (
            now -
                _lastHoverTick <
            33)
        {
            return;
        }

        _lastHoverTick =
            now;

        _lastHoverPixelX =
            pixelX;

        _lastHoverPixelY =
            pixelY;

        _runtime.UpdateHover(
            pixelX,
            pixelY);
    }

    private void OnPointerReleased(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (
            _isManipulatingGizmo &&
            _runtime is not null)
        {
            var edit =
                _runtime.EndGizmoDrag();

            if (edit is not null)
            {
                TransformEditPending
                    ?.Invoke(
                        edit);

                PublishSelectionInfo();

                SelectionStatusChanged?.Invoke(
                    this,
                    edit.IsObject
                        ? "Transformação de objeto OMSI pendente de salvamento."
                        : "Transformação de spline OMSI pendente de salvamento.");
            }
        }

        if (
            _leftPressed ||
            _isPanning ||
            _isOrbiting ||
            _isManipulatingGizmo)
        {
            InputSurface.ReleasePointerCapture(
                e.Pointer);
        }

        _leftPressed = false;
        _isPanning = false;
        _isOrbiting = false;
        _isManipulatingGizmo = false;
    }

    private void OnPointerExited(
        object sender,
        PointerRoutedEventArgs e)
    {
        _lastHoverPixelX =
            uint.MaxValue;

        _lastHoverPixelY =
            uint.MaxValue;

        _runtime?.ClearHover();
    }

    private void PublishSelectionInfo()
    {
        SelectionChanged
            ?.Invoke(
                _runtime
                    ?.GetSelectionInfo());
    }

    private void OnPointerWheelChanged(
        object sender,
        PointerRoutedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(
                InputSurface);

        if (_runtime is not null)
        {
            _runtime.Zoom(
                point.Properties
                    .MouseWheelDelta);

            PointerStatusChanged?.Invoke(
                this,
                $"Zoom nativo: {_runtime.Navigation.Zoom:F2}×");
        }

        e.Handled = true;
    }
}
