using MapStudio.Core.Omsi.Timetables;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MapStudio.Native.Dialogs;

internal static class TimetableLineEditorDialog
{
    public static async Task<OmsiTimetableLine?> ShowAsync(
        XamlRoot xamlRoot,
        OmsiTimetableLine line,
        IReadOnlyCollection<string> knownTripNames)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        var editor = new TimetableLineEditor(line, knownTripNames);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(error);
        panel.Children.Add(editor);
        OmsiTimetableLine? result = null;
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = $"Editar Line/Tours · {line.Name}",
            Content = panel,
            PrimaryButtonText = "Salvar Line/Tours",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (
                !editor.TryBuildLine(
                    out result,
                    out var validationError))
            {
                args.Cancel = true;
                error.Text = validationError;
                return;
            }

            error.Text = string.Empty;
        };
        return await dialog.ShowAdaptiveAsync() == ContentDialogResult.Primary ? result : null;
    }
}
