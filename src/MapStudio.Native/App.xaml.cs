using Microsoft.UI.Xaml;

namespace MapStudio.Native;

public partial class App : Application
{
    public static Window? MainWindowInstance { get; private set; }

    public App()
    {
        NativeStartupDiagnostics.Write(
            "App constructor begin.");

        NativeStartupDiagnostics.WriteEnvironment();

        try
        {
            InitializeComponent();

            UnhandledException +=
                OnUnhandledException;

            NativeStartupDiagnostics.Write(
                "App InitializeComponent succeeded.");
        }
        catch (Exception exception)
        {
            NativeStartupDiagnostics.ReportFatal(
                "App.InitializeComponent",
                exception);

            throw;
        }
    }

    protected override void OnLaunched(
        LaunchActivatedEventArgs args)
    {
        NativeStartupDiagnostics.Write(
            "OnLaunched begin.");

        try
        {
            MainWindowInstance =
                new MainWindow();

            NativeStartupDiagnostics.Write(
                "MainWindow constructed.");

            MainWindowInstance.Activate();

            NativeStartupDiagnostics.Write(
                "MainWindow activated.");
        }
        catch (Exception exception)
        {
            NativeStartupDiagnostics.ReportFatal(
                "App.OnLaunched/MainWindow",
                exception);

            throw;
        }
    }

    private static void OnUnhandledException(
        object sender,
        Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        NativeStartupDiagnostics.Write(
            $"Unhandled XAML exception: {args.Exception}");

        // Do not mark the exception as handled.
        // A fatal UI error must still fail visibly after the log is written.
    }
}
