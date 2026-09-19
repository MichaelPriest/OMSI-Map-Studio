using System.Runtime.InteropServices;
using System.Windows;

namespace MapStudio.Desktop;

public partial class App : Application
{
    private const string AppUserModelId =
        "MichaelPriest.OMSIMapStudio";

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern int
        SetCurrentProcessExplicitAppUserModelID(
            string appID);

    protected override void OnStartup(
        StartupEventArgs e)
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(
                AppUserModelId);
        }
        catch
        {
            // Shell identity must never block startup.
        }

        base.OnStartup(e);
    }
}
