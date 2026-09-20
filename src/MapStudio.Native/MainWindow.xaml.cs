using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace MapStudio.Native;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var hwnd =
            WindowNative.GetWindowHandle(this);

        var windowId =
            Microsoft.UI.Win32Interop
                .GetWindowIdFromWindow(hwnd);

        var appWindow =
            AppWindow.GetFromWindowId(
                windowId);

        appWindow.Resize(
            new SizeInt32(
                1440,
                900));

        Viewport.PointerStatusChanged +=
            (_, message) =>
            {
                StatusText.Text = message;
            };

        Viewport.SelectionStatusChanged +=
            (_, message) =>
            {
                SelectionText.Text = message;
            };
    }
}
