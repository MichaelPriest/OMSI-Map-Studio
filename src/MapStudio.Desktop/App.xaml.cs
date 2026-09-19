using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MapStudio.Desktop;

public partial class App : Application
{
    protected override void OnStartup(
        StartupEventArgs e)
    {
        DispatcherUnhandledException +=
            OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException +=
            OnUnhandledException;

        base.OnStartup(e);
    }

    private static void
        WriteCrashLog(
            Exception exception)
    {
        try
        {
            var directory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder
                            .LocalApplicationData),
                    "OMSI Map Studio",
                    "logs");

            Directory.CreateDirectory(
                directory);

            var path =
                Path.Combine(
                    directory,
                    "startup-crash.log");

            File.AppendAllText(
                path,
                $"[{DateTimeOffset.Now:O}]\r\n{exception}\r\n\r\n");
        }
        catch
        {
            // Crash logging must never mask the original error.
        }
    }

    private static void
        OnUnhandledException(
            object? sender,
            UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is
            Exception exception)
        {
            WriteCrashLog(exception);
        }
    }

    private static void
        OnDispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);

        MessageBox.Show(
            "O OMSI Map Studio encontrou um erro ao iniciar. " +
            "Um diagnóstico foi salvo em %LOCALAPPDATA%\\OMSI Map Studio\\logs\\startup-crash.log.",
            "OMSI Map Studio",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
