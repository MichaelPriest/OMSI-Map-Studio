using MapStudio.Core.Omsi.Scenery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MapStudio.Native.Dialogs;

internal sealed record SceneryPathEditResult(
    int PathOrdinal,
    OmsiSceneryPathDefinition Path);

internal static class SceneryPathEditorDialog
{
    private sealed record PathOption(
        int Index,
        string DisplayText);

    public static async Task<
        SceneryPathEditResult?>
        ShowAsync(
            XamlRoot xamlRoot,
            string assetPath,
            IReadOnlyList<
                OmsiSceneryPathDefinition>
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
                            $"Path {index} · {GetKindLabel(path.Type)} · {GetDirectionLabel(path.Direction)} · {path.Width:F2} m"))
                .ToArray();

        var pathBox =
            new ComboBox
            {
                Header =
                    "Path do SCO",
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
            double minimum =
                -1000000,
            double maximum =
                1000000,
            double smallChange =
                0.1) =>
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

        var xBox =
            CreateNumberBox(
                "X");
        var yBox =
            CreateNumberBox(
                "Y");
        var zBox =
            CreateNumberBox(
                "Z");
        var rotationBox =
            CreateNumberBox(
                "Rotação");
        var radiusBox =
            CreateNumberBox(
                "Raio",
                -1000000,
                1000000,
                0.25);
        var lengthBox =
            CreateNumberBox(
                "Comprimento",
                0,
                1000000,
                0.25);
        var gradientStartBox =
            CreateNumberBox(
                "Gradiente inicial");
        var gradientEndBox =
            CreateNumberBox(
                "Gradiente final");
        var typeBox =
            CreateNumberBox(
                "Tipo · 0 veículo · 1 pedestre · 2 trilho · 3 aéreo",
                int.MinValue,
                int.MaxValue,
                1);
        var widthBox =
            CreateNumberBox(
                "Largura",
                0,
                1000000,
                0.1);
        var directionBox =
            CreateNumberBox(
                "Direção · 0 → · 1 ← · 2 ↔",
                int.MinValue,
                int.MaxValue,
                1);
        var blinkerBox =
            CreateNumberBox(
                "Código de seta / blinker",
                int.MinValue,
                int.MaxValue,
                1);

        var trafficLightCheck =
            new CheckBox
            {
                Content =
                    "Controlado por semáforo"
            };

        var trafficLightBox =
            CreateNumberBox(
                "Índice do semáforo",
                0,
                int.MaxValue,
                1);

        var switchDirectionCheck =
            new CheckBox
            {
                Content =
                    "Usar switchdir"
            };

        var switchDirectionBox =
            CreateNumberBox(
                "Switch direction",
                int.MinValue,
                int.MaxValue,
                1);

        var crossingProblemCheck =
            new CheckBox
            {
                Content =
                    "Crossing problem"
            };

        var validationText =
            new TextBlock
            {
                TextWrapping =
                    TextWrapping.Wrap
            };

        var coordinateGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        coordinateGrid.ColumnDefinitions.Add(
            new ColumnDefinition());
        coordinateGrid.ColumnDefinitions.Add(
            new ColumnDefinition());
        coordinateGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            xBox,
            0);
        Grid.SetColumn(
            yBox,
            1);
        Grid.SetColumn(
            zBox,
            2);

        coordinateGrid.Children.Add(
            xBox);
        coordinateGrid.Children.Add(
            yBox);
        coordinateGrid.Children.Add(
            zBox);

        var geometryGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        geometryGrid.ColumnDefinitions.Add(
            new ColumnDefinition());
        geometryGrid.ColumnDefinitions.Add(
            new ColumnDefinition());
        geometryGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            rotationBox,
            0);
        Grid.SetColumn(
            radiusBox,
            1);
        Grid.SetColumn(
            lengthBox,
            2);

        geometryGrid.Children.Add(
            rotationBox);
        geometryGrid.Children.Add(
            radiusBox);
        geometryGrid.Children.Add(
            lengthBox);

        var gradientGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        gradientGrid.ColumnDefinitions.Add(
            new ColumnDefinition());
        gradientGrid.ColumnDefinitions.Add(
            new ColumnDefinition());
        gradientGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            gradientStartBox,
            0);
        Grid.SetColumn(
            gradientEndBox,
            1);
        Grid.SetColumn(
            widthBox,
            2);

        gradientGrid.Children.Add(
            gradientStartBox);
        gradientGrid.Children.Add(
            gradientEndBox);
        gradientGrid.Children.Add(
            widthBox);

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
        behaviorGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            typeBox,
            0);
        Grid.SetColumn(
            directionBox,
            1);
        Grid.SetColumn(
            blinkerBox,
            2);

        behaviorGrid.Children.Add(
            typeBox);
        behaviorGrid.Children.Add(
            directionBox);
        behaviorGrid.Children.Add(
            blinkerBox);

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    640
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Este editor altera o arquivo SCO compartilhado. Todas as instâncias do mapa que usam este mesmo objeto receberão o novo traçado.",
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
            coordinateGrid);
        panel.Children.Add(
            geometryGrid);
        panel.Children.Add(
            gradientGrid);
        panel.Children.Add(
            behaviorGrid);
        panel.Children.Add(
            trafficLightCheck);
        panel.Children.Add(
            trafficLightBox);
        panel.Children.Add(
            switchDirectionCheck);
        panel.Children.Add(
            switchDirectionBox);
        panel.Children.Add(
            crossingProblemCheck);
        panel.Children.Add(
            validationText);

        var loading =
            false;

        void UpdateOptionalInputs()
        {
            trafficLightBox.IsEnabled =
                trafficLightCheck.IsChecked ==
                true;

            switchDirectionBox.IsEnabled =
                switchDirectionCheck.IsChecked ==
                true;
        }

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

                xBox.Value =
                    path.X;
                yBox.Value =
                    path.Y;
                zBox.Value =
                    path.Z;
                rotationBox.Value =
                    path.Rotation;
                radiusBox.Value =
                    path.Radius;
                lengthBox.Value =
                    path.Length;
                gradientStartBox.Value =
                    path.GradientStart;
                gradientEndBox.Value =
                    path.GradientEnd;
                typeBox.Value =
                    path.Type;
                widthBox.Value =
                    path.Width;
                directionBox.Value =
                    path.Direction;
                blinkerBox.Value =
                    path.BlinkerCode;

                trafficLightCheck.IsChecked =
                    path.TrafficLightIndex
                        .HasValue;

                trafficLightBox.Value =
                    path.TrafficLightIndex ??
                    0;

                switchDirectionCheck.IsChecked =
                    path.SwitchDirection
                        .HasValue;

                switchDirectionBox.Value =
                    path.SwitchDirection ??
                    0;

                crossingProblemCheck.IsChecked =
                    path.CrossingProblem;

                UpdateOptionalInputs();

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

        trafficLightCheck.Checked +=
            (
                _,
                _
            ) =>
                UpdateOptionalInputs();

        trafficLightCheck.Unchecked +=
            (
                _,
                _
            ) =>
                UpdateOptionalInputs();

        switchDirectionCheck.Checked +=
            (
                _,
                _
            ) =>
                UpdateOptionalInputs();

        switchDirectionCheck.Unchecked +=
            (
                _,
                _
            ) =>
                UpdateOptionalInputs();

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

        SceneryPathEditResult?
            result =
                null;

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    xamlRoot,
                Title =
                    $"Editar paths SCO · {Path.GetFileName(assetPath.Replace('\\', Path.DirectorySeparatorChar))}",
                Content =
                    new ScrollViewer
                    {
                        Content =
                            panel,
                        MaxHeight =
                            680
                    },
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

                var finite =
                    new[]
                    {
                        xBox.Value,
                        yBox.Value,
                        zBox.Value,
                        rotationBox.Value,
                        radiusBox.Value,
                        lengthBox.Value,
                        gradientStartBox.Value,
                        gradientEndBox.Value,
                        widthBox.Value
                    };

                if (
                    finite.Any(
                        value =>
                            !double.IsFinite(
                                value)) ||
                    lengthBox.Value <
                        0 ||
                    widthBox.Value <
                        0)
                {
                    validationText.Text =
                        "Os campos geométricos devem conter valores numéricos finitos; comprimento e largura não podem ser negativos.";
                    args.Cancel =
                        true;
                    return;
                }

                if (
                    !TryReadInt(
                        typeBox,
                        out var type) ||
                    !TryReadInt(
                        directionBox,
                        out var direction) ||
                    !TryReadInt(
                        blinkerBox,
                        out var blinkerCode))
                {
                    validationText.Text =
                        "Tipo, direção e blinker devem ser números inteiros.";
                    args.Cancel =
                        true;
                    return;
                }

                int?
                    trafficLightIndex =
                        null;

                if (
                    trafficLightCheck.IsChecked ==
                        true)
                {
                    if (
                        !TryReadInt(
                            trafficLightBox,
                            out var parsedTrafficLight) ||
                        parsedTrafficLight <
                            0)
                    {
                        validationText.Text =
                            "O índice do semáforo deve ser um inteiro maior ou igual a zero.";
                        args.Cancel =
                            true;
                        return;
                    }

                    trafficLightIndex =
                        parsedTrafficLight;
                }

                int?
                    switchDirection =
                        null;

                if (
                    switchDirectionCheck.IsChecked ==
                        true)
                {
                    if (
                        !TryReadInt(
                            switchDirectionBox,
                            out var parsedSwitchDirection))
                    {
                        validationText.Text =
                            "Switch direction deve ser um número inteiro.";
                        args.Cancel =
                            true;
                        return;
                    }

                    switchDirection =
                        parsedSwitchDirection;
                }

                var original =
                    paths[
                        option.Index];

                result =
                    new SceneryPathEditResult(
                        option.Index,
                        original with
                        {
                            X =
                                xBox.Value,
                            Y =
                                yBox.Value,
                            Z =
                                zBox.Value,
                            Rotation =
                                rotationBox.Value,
                            Radius =
                                radiusBox.Value,
                            Length =
                                lengthBox.Value,
                            GradientStart =
                                gradientStartBox.Value,
                            GradientEnd =
                                gradientEndBox.Value,
                            Type =
                                type,
                            Width =
                                widthBox.Value,
                            Direction =
                                direction,
                            BlinkerCode =
                                blinkerCode,
                            TrafficLightIndex =
                                trafficLightIndex,
                            SwitchDirection =
                                switchDirection,
                            CrossingProblem =
                                crossingProblemCheck
                                    .IsChecked ==
                                true
                        });

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
            0 =>
                "Veículo",
            _ =>
                $"Tipo {type}"
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
                $"direção {direction}"
        };
}
