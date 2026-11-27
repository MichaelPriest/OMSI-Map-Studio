using MapStudio.Core.Settings;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioUserSettingsStoreTests
{
    [Fact]
    public void SaveOmsiRootPathPersistsAndReloadsValidatedPath()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-user-settings-" +
                Guid.NewGuid()
                    .ToString("N"));

        var omsiRoot =
            Path.Combine(
                root,
                "OMSI 2");

        var settingsPath =
            Path.Combine(
                root,
                "settings.json");

        Directory.CreateDirectory(
            Path.Combine(
                omsiRoot,
                "maps"));

        try
        {
            var store =
                new MapStudioUserSettingsStore(
                    settingsPath);

            store.SaveOmsiRootPath(
                omsiRoot);

            var restored =
                new MapStudioUserSettingsStore(
                    settingsPath)
                    .TryGetValidOmsiRootPath();

            Assert.Equal(
                Path.GetFullPath(
                    omsiRoot),
                restored);

            Assert.True(
                File.Exists(
                    settingsPath));
        }
        finally
        {
            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public void InvalidSavedOmsiPathIsIgnored()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-user-settings-invalid-" +
                Guid.NewGuid()
                    .ToString("N"));

        var settingsPath =
            Path.Combine(
                root,
                "settings.json");

        Directory.CreateDirectory(
            root);

        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "OmsiRootPath": "Z:\\missing-omsi"
                }
                """);

            var store =
                new MapStudioUserSettingsStore(
                    settingsPath);

            Assert.Null(
                store.TryGetValidOmsiRootPath());
        }
        finally
        {
            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public void CorruptSettingsDoNotBreakStartup()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-user-settings-corrupt-" +
                Guid.NewGuid()
                    .ToString("N"));

        var settingsPath =
            Path.Combine(
                root,
                "settings.json");

        Directory.CreateDirectory(
            root);

        try
        {
            File.WriteAllText(
                settingsPath,
                "{not-json");

            var settings =
                new MapStudioUserSettingsStore(
                    settingsPath)
                    .Load();

            Assert.Null(
                settings.OmsiRootPath);
        }
        finally
        {
            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }
}
