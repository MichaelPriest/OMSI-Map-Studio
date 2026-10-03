using System.Runtime.InteropServices;
using System.Text;

namespace MapStudio.Native;

internal static class NativeStartupDiagnostics
{
    private const string ProductDirectory =
        "OMSI Map Studio Native Preview";

    public static string LogPath
    {
        get
        {
            var directory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    ProductDirectory,
                    "Logs");

            Directory.CreateDirectory(
                directory);

            return Path.Combine(
                directory,
                "startup.log");
        }
    }

    public static void Write(
        string message)
    {
        try
        {
            var line =
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";

            File.AppendAllText(
                LogPath,
                line,
                Encoding.UTF8);
        }
        catch
        {
        }
    }

    public static void WriteEnvironment()
    {
        Write(
            $"BaseDirectory={AppContext.BaseDirectory}");

        Write(
            $"OS={Environment.OSVersion}");

        Write(
            $"ProcessArchitecture={RuntimeInformation.ProcessArchitecture}");

        Write(
            $"Framework={RuntimeInformation.FrameworkDescription}");

        var priPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "MapStudio.Native.pri");

        Write(
            $"ProjectPRI={priPath} exists={File.Exists(priPath)}");
    }

    public static void ReportFatal(
        string stage,
        Exception exception)
    {
        Write(
            $"FATAL stage={stage} type={exception.GetType().FullName} hresult=0x{exception.HResult:X8} message={exception.Message}");

        Write(
            exception.ToString());

        try
        {
            MessageBox(
                IntPtr.Zero,
                "OMSI Map Studio Native Preview não conseguiu iniciar.\n\n" +
                $"Etapa: {stage}\n" +
                $"Erro: {exception.Message}\n\n" +
                $"Log: {LogPath}",
                "OMSI Map Studio Native Preview",
                0x00000010);
        }
        catch
        {
        }
    }

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern int MessageBox(
        IntPtr hWnd,
        string lpText,
        string lpCaption,
        uint uType);
}
