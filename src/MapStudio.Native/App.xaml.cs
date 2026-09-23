using Microsoft.UI.Xaml;

namespace MapStudio.Native;

public partial class App : Application
{
    private const string StartupSmokeTestArgument =
        "--startup-smoke-test";

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

            if (IsStartupSmokeTestRequested())
            {
                NativeStartupDiagnostics.Write(
                    "STARTUP_SMOKE_TEST_PASSED");

                MainWindowInstance.Close();
                Exit();
            }
        }
        catch (Exception exception)
        {
            NativeStartupDiagnostics.ReportFatal(
                "App.OnLaunched/MainWindow",
                exception);

            throw;
        }
    }

    private static bool IsStartupSmokeTestRequested() =>
        Environment
            .GetCommandLineArgs()
            .Skip(1)
            .Any(
                argument =>
                    string.Equals(
                        argument,
                        StartupSmokeTestArgument,
                        StringComparison.OrdinalIgnoreCase));

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
