using System.Text.Json;

namespace MapStudio.Core.Settings;

public sealed record MapStudioUserSettings(
    string? OmsiRootPath = null);

public sealed class MapStudioUserSettingsStore
{
    private readonly string _settingsPath;

    public MapStudioUserSettingsStore(
        string? settingsPath = null)
    {
        _settingsPath =
            string.IsNullOrWhiteSpace(
                settingsPath)
                ? GetDefaultSettingsPath()
                : Path.GetFullPath(
                    settingsPath);
    }

    public string SettingsPath =>
        _settingsPath;

    public MapStudioUserSettings Load()
    {
        if (!File.Exists(
                _settingsPath))
        {
            return new MapStudioUserSettings();
        }

        try
        {
            var json =
                File.ReadAllText(
                    _settingsPath);

            var settings =
                JsonSerializer.Deserialize<
                    MapStudioUserSettings>(
                        json);

            return settings ??
                new MapStudioUserSettings();
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    UnauthorizedAccessException or
                    JsonException)
        {
            return new MapStudioUserSettings();
        }
    }

    public string? TryGetValidOmsiRootPath()
    {
        var path =
            Load()
                .OmsiRootPath;

        if (string.IsNullOrWhiteSpace(
                path))
        {
            return null;
        }

        try
        {
            var normalized =
                Path.GetFullPath(
                    path)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            return
                Directory.Exists(
                    normalized) &&
                Directory.Exists(
                    Path.Combine(
                        normalized,
                        "maps"))
                    ? normalized
                    : null;
        }
        catch (
            Exception exception)
            when (
                exception is
                    ArgumentException or
                    NotSupportedException or
                    PathTooLongException)
        {
            return null;
        }
    }

    public void SaveOmsiRootPath(
        string omsiRootPath)
    {
        ArgumentException
            .ThrowIfNullOrWhiteSpace(
                omsiRootPath);

        var normalized =
            Path.GetFullPath(
                omsiRootPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        if (
            !Directory.Exists(
                normalized) ||
            !Directory.Exists(
                Path.Combine(
                    normalized,
                    "maps")))
        {
            throw new InvalidDataException(
                @"A pasta selecionada não contém a estrutura OMSI 2\maps.");
        }

        Save(
            Load() with
            {
                OmsiRootPath =
                    normalized
            });
    }

    public void ClearOmsiRootPath()
    {
        Save(
            Load() with
            {
                OmsiRootPath =
                    null
            });
    }

    private void Save(
        MapStudioUserSettings settings)
    {
        var parent =
            Path.GetDirectoryName(
                _settingsPath);

        if (!string.IsNullOrWhiteSpace(
                parent))
        {
            Directory.CreateDirectory(
                parent);
        }

        var temporaryPath =
            _settingsPath +
            "." +
            Guid.NewGuid()
                .ToString("N") +
            ".tmp";

        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented =
                            true
                    }));

            File.Move(
                temporaryPath,
                _settingsPath,
                overwrite:
                    true);
        }
        finally
        {
            if (File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }
        }
    }

    private static string
        GetDefaultSettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment
                    .SpecialFolder
                    .LocalApplicationData),
            "OMSI Map Studio",
            "settings",
            "user-settings.json");
}
