using System.Runtime.InteropServices;
using MapStudio.Native.Interop;
using MapStudio.Native.Services;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MapStudio.Native.Controls;

public sealed partial class NativeViewport : UserControl
{
    private NativeViewportRuntime? _runtime;
    private bool _leftPressed;
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

    public void SetMapSnapshot(
        NativeMapSnapshot snapshot)
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

        var scene =
            _runtime.LoadScene(
                snapshot.Tiles
                    .Select(
                        tile =>
                            new NativeSceneTile(
                                tile.Reference,
                                tile.Content))
                    .ToArray());

        RuntimeText.Text =
            $"{snapshot.Map.DisplayName} · {scene.Tiles.Count} tiles · " +
            $"{scene.Objects.Count} objetos · {scene.Splines.Count} splines · " +
            $"{scene.SelectableCount} IDs de seleção";

        SelectionStatusChanged?.Invoke(
            this,
            $"Registry nativo pronto: {scene.SelectableCount} entidades selecionáveis.");
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

        _leftPressed =
            point.Properties
                .IsLeftButtonPressed;

        if (!_leftPressed)
        {
            return;
        }

        InputSurface.CapturePointer(
            e.Pointer);

        var message =
            $"Clique nativo: X={point.Position.X:F1} Y={point.Position.Y:F1}";

        PointerStatusChanged?.Invoke(
            this,
            message);

        SelectionStatusChanged?.Invoke(
            this,
            "ID buffer: cena OMSI será ligada na próxima etapa");

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
        if (_leftPressed)
        {
            InputSurface.ReleasePointerCapture(
                e.Pointer);
        }

        _leftPressed = false;
    }

    private void OnPointerWheelChanged(
        object sender,
        PointerRoutedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(
                InputSurface);

        PointerStatusChanged?.Invoke(
            this,
            $"Zoom nativo: delta={point.Properties.MouseWheelDelta}");

        e.Handled = true;
    }
}
