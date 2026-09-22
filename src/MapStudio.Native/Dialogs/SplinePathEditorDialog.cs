using MapStudio.Core.Omsi.Splines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MapStudio.Native.Dialogs;

internal sealed record SplinePathEditResult(
    int PathOrdinal,
    OmsiSplinePathDefinition Path);

internal static class SplinePathEditorDialog
{
    private sealed record PathOption(
        int Index,
        string DisplayText);

    public static async Task<
        SplinePathEditResult?>
        ShowAsync(
            XamlRoot xamlRoot,
            string assetPath,
            IReadOnlyList<
                OmsiSplinePathDefinition>
                paths,
            int? initialPathOrdinal =
                null)
    {
        ArgumentNullException.ThrowIfNull(
            xamlRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            assetPath);

        ArgumentNullException.ThrowIfNull(
            paths);

        if (paths.Count == 0)
        {
            return null;
        }

        var options =
            paths
                .Select(
                    (
                        path,
                        index
                    ) =>
                        new PathOption(
                            index,
                            $"Path {index} · {GetKindLabel(path.Type)} · {GetDirectionLabel(path.Direction)} · {path.Width:F2} m · offset X {path.X:F2}"))
                .ToArray();

        var pathBox =
            new ComboBox
            {
                Header =
                    "Path da spline",
                ItemsSource =
                    options,
                DisplayMemberPath =
                    nameof(
                        PathOption.DisplayText),
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        NumberBox CreateNumberBox(
            string header,
            double minimum,
            double maximum,
            double smallChange) =>
            new()
            {
                Header =
                    header,
                Minimum =
                    minimum,
                Maximum =
                    maximum,
                SmallChange =
                    smallChange,
                SpinButtonPlacementMode =
                    NumberBoxSpinButtonPlacementMode.Compact
            };

        var typeBox =
            CreateNumberBox(
                "Tipo · 0 veículo · 1 pedestre · 2 trilho · 3 aéreo",
                0,
                3,
                1);

        var xBox =
            CreateNumberBox(
                "Offset X",
                -1000000,
                1000000,
                0.1);

        var zBox =
            CreateNumberBox(
                "Offset Z",
                -1000000,
                1000000,
                0.05);

        var widthBox =
            CreateNumberBox(
                "Largura",
                0,
                1000000,
                0.1);

        var directionBox =
            CreateNumberBox(
                "Direção · 0 → · 1 ← · 2 ↔",
                0,
                2,
                1);

        var grid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());
        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            xBox,
            0);

        Grid.SetColumn(
            zBox,
            1);

        grid.Children.Add(
            xBox);

        grid.Children.Add(
            zBox);

        var behaviorGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        behaviorGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        behaviorGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            typeBox,
            0);

        Grid.SetColumn(
            directionBox,
            1);

        behaviorGrid.Children.Add(
            typeBox);

        behaviorGrid.Children.Add(
            directionBox);

        var validationText =
            new TextBlock
            {
                TextWrapping =
                    TextWrapping.Wrap
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    540
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Este editor altera o arquivo SLI compartilhado. Todas as splines do mapa que usam este mesmo asset receberão a nova faixa.",
                TextWrapping =
                    TextWrapping.Wrap
            });

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    assetPath,
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.72
            });

        panel.Children.Add(
            pathBox);

        panel.Children.Add(
            grid);

        panel.Children.Add(
            widthBox);

        panel.Children.Add(
            behaviorGrid);

        panel.Children.Add(
            validationText);

        var loading =
            false;

        void LoadPath(
            int index)
        {
            if (
                index < 0 ||
                index >=
                    paths.Count)
            {
                return;
            }

            loading =
                true;

            try
            {
                var path =
                    paths[
                        index];

                typeBox.Value =
                    path.Type;

                xBox.Value =
                    path.X;

                zBox.Value =
                    path.Z;

                widthBox.Value =
                    path.Width;

                directionBox.Value =
                    path.Direction;

                validationText.Text =
                    $"Editando path {index} · {GetKindLabel(path.Type)} · {GetDirectionLabel(path.Direction)}.";
            }
            finally
            {
                loading =
                    false;
            }
        }

        pathBox.SelectionChanged +=
            (
                _,
                _
            ) =>
            {
                if (
                    loading ||
                    pathBox.SelectedItem is not
                        PathOption option)
                {
                    return;
                }

                LoadPath(
                    option.Index);
            };

        var initialIndex =
            Math.Clamp(
                initialPathOrdinal ??
                    0,
                0,
                paths.Count -
                    1);

        pathBox.SelectedIndex =
            initialIndex;

        LoadPath(
            initialIndex);

        SplinePathEditResult?
            result =
                null;

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    xamlRoot,
                Title =
                    $"Editar paths SLI · {Path.GetFileName(assetPath.Replace('\\', Path.DirectorySeparatorChar))}",
                Content =
                    panel,
                PrimaryButtonText =
                    "Salvar path",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        dialog.PrimaryButtonClick +=
            (
                _,
                args
            ) =>
            {
                if (
                    pathBox.SelectedItem is not
                        PathOption option)
                {
                    validationText.Text =
                        "Selecione um path.";
                    args.Cancel =
                        true;
                    return;
                }

                if (
                    !TryReadInt(
                        typeBox,
                        out var type) ||
                    type is
                        < 0 or
                        > 3)
                {
                    validationText.Text =
                        "Tipo deve ser um inteiro entre 0 e 3.";
                    args.Cancel =
                        true;
                    return;
                }

                if (
                    !TryReadInt(
                        directionBox,
                        out var direction) ||
                    direction is
                        < 0 or
                        > 2)
                {
                    validationText.Text =
                        "Direção deve ser 0, 1 ou 2.";
                    args.Cancel =
                        true;
                    return;
                }

                if (
                    !double.IsFinite(
                        xBox.Value) ||
                    !double.IsFinite(
                        zBox.Value) ||
                    !double.IsFinite(
                        widthBox.Value) ||
                    widthBox.Value <
                        0)
                {
                    validationText.Text =
                        "Offset X, offset Z e largura devem conter valores válidos; largura não pode ser negativa.";
                    args.Cancel =
                        true;
                    return;
                }

                result =
                    new SplinePathEditResult(
                        option.Index,
                        new OmsiSplinePathDefinition(
                            type,
                            xBox.Value,
                            zBox.Value,
                            widthBox.Value,
                            direction));

                validationText.Text =
                    string.Empty;
            };

        return
            await dialog.ShowAsync() ==
                ContentDialogResult.Primary
                ? result
                : null;
    }

    private static bool TryReadInt(
        NumberBox box,
        out int value)
    {
        value =
            0;

        if (
            !double.IsFinite(
                box.Value))
        {
            return false;
        }

        var rounded =
            Math.Round(
                box.Value);

        if (
            Math.Abs(
                rounded -
                box.Value) >
                0.0000001 ||
            rounded <
                int.MinValue ||
            rounded >
                int.MaxValue)
        {
            return false;
        }

        value =
            checked(
                (int)rounded);

        return true;
    }

    private static string GetKindLabel(
        int type) =>
        type switch
        {
            1 =>
                "Pedestre",
            2 =>
                "Trilho",
            3 =>
                "Aéreo",
            _ =>
                "Veículo"
        };

    private static string GetDirectionLabel(
        int direction) =>
        direction switch
        {
            0 =>
                "→",
            1 =>
                "←",
            2 =>
                "↔",
            _ =>
                "?"
        };
}
