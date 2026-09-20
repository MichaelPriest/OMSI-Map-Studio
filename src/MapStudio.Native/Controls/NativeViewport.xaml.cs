using System.Runtime.InteropServices;
using MapStudio.Native.Interop;
using MapStudio.Native.Services;
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
    private double _lastPanX;
    private double _lastPanY;
    private bool _swapChainBound;

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
                    omsiRoot,
                    cancellationToken);

        _runtime.RenderInitialFrame();

        RuntimeText.Text =
            $"{snapshot.Map.DisplayName} · {scene.Tiles.Count} tiles · " +
            $"{scene.Objects.Count} objetos · {scene.Splines.Count} splines · " +
            $"{_runtime.MapRenderer.TerrainTriangleVertexCount / 3} triângulos terreno · " +
            $"{_runtime.MapRenderer.ObjectTriangleVertexCount / 3} triângulos O3D";

        SelectionStatusChanged?.Invoke(
            this,
            $"Assets nativos: {_runtime.LoadedSceneryAssetCount} SCO · " +
            $"{_runtime.LoadedObjectMeshCount} meshes · " +
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
                .IsMiddleButtonPressed ||
            point.Properties
                .IsRightButtonPressed;

        if (panPressed)
        {
            _isPanning = true;
            _lastPanX =
                point.Position.X;
            _lastPanY =
                point.Position.Y;

            InputSurface.CapturePointer(
                e.Pointer);

            PointerStatusChanged?.Invoke(
                this,
                "Pan nativo ativo");

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
            _isPanning &&
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

            _runtime.Pan(
                deltaX,
                deltaY);

            PointerStatusChanged?.Invoke(
                this,
                $"Pan · zoom {_runtime.Navigation.Zoom:F2}×");

            e.Handled = true;
            return;
        }

        if (_leftPressed)
        {
            PointerStatusChanged?.Invoke(
                this,
                $"Pointer capturado: X={point.Position.X:F1} Y={point.Position.Y:F1}");
        }
    }

    private void OnPointerReleased(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (
            _leftPressed ||
            _isPanning)
        {
            InputSurface.ReleasePointerCapture(
                e.Pointer);
        }

        _leftPressed = false;
        _isPanning = false;
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
