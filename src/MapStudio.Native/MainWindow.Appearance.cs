using Microsoft.UI.Xaml;
using System;
using System.IO;

namespace MapStudio.Native;

public sealed partial class MainWindow
{
    private static readonly string AppearancePreferencePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OMSI Map Studio",
            "appearance.txt");

    private void OnThemeSettingsLoaded(object sender, RoutedEventArgs e)
    {
        var theme = ElementTheme.Default;

        try
        {
            if (File.Exists(AppearancePreferencePath))
            {
                theme = File.ReadAllText(AppearancePreferencePath).Trim().ToLowerInvariant() switch
                {
                    "light" => ElementTheme.Light,
                    "dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
        }
        catch (IOException)
        {
            // Keep the system theme if the preference cannot be read.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep the system theme if the preference cannot be read.
        }

        MainRoot.RequestedTheme = theme;
    }

    private void OnThemeSystemClick(object sender, RoutedEventArgs e) =>
        SetAppearanceTheme(ElementTheme.Default, "system");

    private void OnThemeLightClick(object sender, RoutedEventArgs e) =>
        SetAppearanceTheme(ElementTheme.Light, "light");

    private void OnThemeDarkClick(object sender, RoutedEventArgs e) =>
        SetAppearanceTheme(ElementTheme.Dark, "dark");

    private void SetAppearanceTheme(ElementTheme theme, string preference)
    {
        MainRoot.RequestedTheme = theme;

        try
        {
            var directory = Path.GetDirectoryName(AppearancePreferencePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = AppearancePreferencePath + ".tmp";
            File.WriteAllText(temporaryPath, preference);
            File.Move(temporaryPath, AppearancePreferencePath, overwrite: true);
        }
        catch (IOException)
        {
            // Applying the theme is more important than persisting the preference.
        }
        catch (UnauthorizedAccessException)
        {
            // Applying the theme is more important than persisting the preference.
        }
    }
}
