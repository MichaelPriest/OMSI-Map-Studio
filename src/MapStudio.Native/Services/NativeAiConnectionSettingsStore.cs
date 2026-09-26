using System.Text.Json;
using MapStudio.Core.AI;

namespace MapStudio.Native.Services;

public static class NativeAiConnectionSettingsStore
{
    private static readonly JsonSerializerOptions
        JsonOptions =
            new(
                JsonSerializerDefaults.Web)
            {
                WriteIndented =
                    true
            };

    public static MapStudioAiConnectionSettings
        Load()
    {
        var path =
            GetPath();

        if (!File.Exists(path))
        {
            return MapStudioAiConnectionSettings
                .Empty;
        }

        try
        {
            var json =
                File.ReadAllText(
                    path);

            var settings =
                JsonSerializer
                    .Deserialize<
                        MapStudioAiConnectionSettings>(
                            json,
                            JsonOptions);

            return settings
                ?.Normalize() ??
                MapStudioAiConnectionSettings
                    .Empty;
        }
        catch
        {
            return MapStudioAiConnectionSettings
                .Empty;
        }
    }

    public static void Save(
        MapStudioAiConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        var normalized =
            settings.Normalize();

        var path =
            GetPath();

        var directory =
            Path.GetDirectoryName(
                path) ??
            throw new InvalidOperationException(
                "aiSettingsDirectoryUnavailable");

        Directory.CreateDirectory(
            directory);

        var payload =
            JsonSerializer.Serialize(
                normalized,
                JsonOptions);

        var tempPath =
            path +
            "." +
            Guid.NewGuid()
                .ToString("N") +
            ".tmp";

        File.WriteAllText(
            tempPath,
            payload);

        File.Move(
            tempPath,
            path,
            overwrite:
                true);
    }

    public static string GetPath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .LocalApplicationData),
            "OMSI Map Studio",
            "settings",
            "ai-providers.json");
}
