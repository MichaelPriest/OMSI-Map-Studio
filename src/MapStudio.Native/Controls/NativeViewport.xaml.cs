using MapStudio.Renderer.Viewport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MapStudio.Native.Controls;

public sealed partial class NativeViewport : UserControl
{
    private NativeViewportRuntime? _runtime;
    private bool _leftPressed;

    public NativeViewport()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler<string>? PointerStatusChanged;

    public event EventHandler<string>? SelectionStatusChanged;

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            _runtime ??=
                new NativeViewportRuntime();

            RuntimeText.Text =
                $"Direct3D 11 pronto · feature level {_runtime.Device.FeatureLevel}";
        }
        catch (Exception exception)
        {
            RuntimeText.Text =
                $"Falha ao iniciar Direct3D 11: {exception.Message}";
        }
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        _runtime?.Dispose();
        _runtime = null;
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
            "ID buffer: aguardando ligação da cena OMSI");

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
