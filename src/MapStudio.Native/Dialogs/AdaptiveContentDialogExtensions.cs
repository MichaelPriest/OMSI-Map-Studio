using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MapStudio.Native.Dialogs;

/// <summary>
/// Keeps ContentDialog editors inside the current XamlRoot. Large forms grow up
/// to the available area and automatically fall back to scrolling instead of
/// clipping controls.
/// </summary>
internal static class AdaptiveContentDialogExtensions
{
    private const double EdgeMargin = 24;
    private const double ChromeWidth = 48;
    private const double ChromeHeight = 164;
    private const double AbsoluteMaxWidth = 1180;
    private const double AbsoluteMaxHeight = 920;

    public static async Task<ContentDialogResult> ShowAdaptiveAsync(
        this ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        Prepare(dialog);

        return await dialog.ShowAsync();
    }

    private static void Prepare(
        ContentDialog dialog)
    {
        var root = dialog.XamlRoot;
        if (root is null)
        {
            return;
        }

        var availableWidth =
            Math.Max(
                300,
                Math.Min(
                    AbsoluteMaxWidth,
                    root.Size.Width - EdgeMargin));

        var availableHeight =
            Math.Max(
                260,
                Math.Min(
                    AbsoluteMaxHeight,
                    root.Size.Height - EdgeMargin));

        dialog.MinWidth = 0;
        dialog.MinHeight = 0;
        dialog.MaxWidth = availableWidth;
        dialog.MaxHeight = availableHeight;

        var contentWidth =
            Math.Max(
                250,
                availableWidth - ChromeWidth);

        var contentHeight =
            Math.Max(
                180,
                availableHeight - ChromeHeight);

        if (dialog.Content is ScrollViewer existingScroll)
        {
            ConfigureScrollViewer(
                existingScroll,
                contentWidth,
                contentHeight);

            if (existingScroll.Content is FrameworkElement existingContent)
            {
                ClampMinimumSize(
                    existingContent,
                    contentWidth,
                    contentHeight);
            }

            return;
        }

        if (dialog.Content is not FrameworkElement content)
        {
            return;
        }

        ClampMinimumSize(
            content,
            contentWidth,
            contentHeight);

        // ContentDialog already owns the FrameworkElement at this point.
        // Detach it before re-parenting it into the adaptive ScrollViewer.
        // WinUI otherwise throws "Element is already the child of another element".
        dialog.Content =
            null;

        var scroll =
            new ScrollViewer
            {
                MaxWidth = contentWidth,
                MaxHeight = contentHeight,
                HorizontalScrollMode = ScrollMode.Enabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

        scroll.Content =
            content;

        dialog.Content =
            scroll;
    }

    private static void ConfigureScrollViewer(
        ScrollViewer scroll,
        double maxWidth,
        double maxHeight)
    {
        scroll.MaxWidth =
            double.IsFinite(scroll.MaxWidth)
                ? Math.Min(scroll.MaxWidth, maxWidth)
                : maxWidth;

        scroll.MaxHeight =
            double.IsFinite(scroll.MaxHeight)
                ? Math.Min(scroll.MaxHeight, maxHeight)
                : maxHeight;

        scroll.HorizontalScrollMode = ScrollMode.Enabled;
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        scroll.VerticalScrollMode = ScrollMode.Enabled;
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
    }

    private static void ClampMinimumSize(
        FrameworkElement element,
        double maxWidth,
        double maxHeight)
    {
        if (element.MinWidth > maxWidth)
        {
            element.MinWidth = maxWidth;
        }

        if (element.MinHeight > maxHeight)
        {
            element.MinHeight = maxHeight;
        }

        element.MaxWidth =
            double.IsFinite(element.MaxWidth)
                ? Math.Min(element.MaxWidth, maxWidth)
                : maxWidth;

        element.MaxHeight =
            double.IsFinite(element.MaxHeight)
                ? Math.Min(element.MaxHeight, maxHeight)
                : maxHeight;
    }
}
