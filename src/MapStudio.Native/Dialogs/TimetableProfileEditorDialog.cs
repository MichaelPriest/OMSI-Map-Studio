using MapStudio.Core.Omsi.Timetables;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MapStudio.Native.Dialogs;

internal static class TimetableProfileEditorDialog
{
    public static async Task<
        OmsiTimetableTrip?>
        ShowAsync(
            XamlRoot xamlRoot,
            OmsiTimetableTrip trip,
            IReadOnlyList<
                OmsiTimetableBusStop>
                busStops)
    {
        ArgumentNullException.ThrowIfNull(
            xamlRoot);

        ArgumentNullException.ThrowIfNull(
            trip);

        ArgumentNullException.ThrowIfNull(
            busStops);

        var workingLines =
            trip.ProfileLines
                .ToList();

        var profileCombo =
            new ComboBox
            {
                Header =
                    "Perfil de tempo",
                DisplayMemberPath =
                    nameof(
                        OmsiTimetableProfileDefinition
                            .DisplayText),
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var totalMinutesBox =
            new NumberBox
            {
                Header =
                    "Tempo total (min)",
                Minimum =
                    0.01,
                Maximum =
                    100000,
                SmallChange =
                    0.5
            };

        var applyTotalButton =
            new Button
            {
                Content =
                    "Aplicar duração",
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        var stopTimesPanel =
            new StackPanel
            {
                Spacing =
                    3,
                MinWidth =
                    520
            };

        var applyStopTableButton =
            new Button
            {
                Content =
                    "Aplicar tempos da tabela",
                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        var stopTimeEditors =
            new List<(
                int StationIndex,
                NumberBox Arrival,
                TextBlock Segment
            )>();

        var profileInfoText =
            new TextBlock
            {
                Text =
                    "Selecione um perfil.",
                FontSize =
                    11,
                TextWrapping =
                    TextWrapping.Wrap,
                Foreground =
                    new SolidColorBrush(
                        Windows.UI.Color.FromArgb(
                            255,
                            127,
                            198,
                            232))
            };

        var newProfileNameBox =
            new TextBox
            {
                Header =
                    "Novo perfil",
                PlaceholderText =
                    "Ex.: normal"
            };

        var newProfileMinutesBox =
            new NumberBox
            {
                Header =
                    "Duração (min)",
                Minimum =
                    0.01,
                Maximum =
                    100000,
                Value =
                    10,
                SmallChange =
                    0.5
            };

        var addProfileButton =
            new Button
            {
                Content =
                    "+ Criar perfil"
            };

        var deleteProfileButton =
            new Button
            {
                Content =
                    "Excluir perfil",
                IsEnabled =
                    false
            };

        var rawEditor =
            new TextBox
            {
                Header =
                    "Avançado · dados OMSI do perfil",
                AcceptsReturn =
                    true,
                TextWrapping =
                    TextWrapping.NoWrap,
                MinHeight =
                    150,
                FontFamily =
                    new FontFamily(
                        "Consolas")
            };

        List<
            OmsiTimetableProfileDefinition>
            profiles =
                [];

        var synchronizing =
            false;

        List<string> ReadRawLines()
        {
            return rawEditor.Text
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Split(
                    '\n',
                    StringSplitOptions.None)
                .Select(
                    value =>
                        value.Trim())
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .ToList();
        }

        void RefreshStopTimesTable()
        {
            stopTimesPanel.Children.Clear();
            stopTimeEditors.Clear();

            var header =
                new Grid
                {
                    ColumnSpacing =
                        8,
                    Padding =
                        new Thickness(
                            5,
                            3,
                            5,
                            3),
                    Background =
                        new SolidColorBrush(
                            Windows.UI.Color.FromArgb(
                                255,
                                11,
                                32,
                                45))
                };

            header.ColumnDefinitions.Add(
                new ColumnDefinition());

            header.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            150)
                });

            header.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            130)
                });

            var stopHeader =
                new TextBlock
                {
                    Text =
                        "Parada",
                    FontWeight =
                        Microsoft.UI.Text
                            .FontWeights
                            .SemiBold
                };

            var arrivalHeader =
                new TextBlock
                {
                    Text =
                        "Chegada (min)",
                    FontWeight =
                        Microsoft.UI.Text
                            .FontWeights
                            .SemiBold
                };

            var segmentHeader =
                new TextBlock
                {
                    Text =
                        "Trecho",
                    FontWeight =
                        Microsoft.UI.Text
                            .FontWeights
                            .SemiBold
                };

            Grid.SetColumn(
                stopHeader,
                0);
            Grid.SetColumn(
                arrivalHeader,
                1);
            Grid.SetColumn(
                segmentHeader,
                2);

            header.Children.Add(
                stopHeader);
            header.Children.Add(
                arrivalHeader);
            header.Children.Add(
                segmentHeader);

            stopTimesPanel.Children.Add(
                header);

            if (
                profileCombo.SelectedItem is not
                    OmsiTimetableProfileDefinition
                        profile)
            {
                profileInfoText.Text =
                    "Nenhum perfil. Crie um perfil para configurar os tempos.";

                return;
            }

            double?
                previousMinutes =
                    null;

            for (
                var stationIndex = 0;
                stationIndex <
                    trip.Stations.Count;
                stationIndex++)
            {
                var station =
                    trip.Stations[
                        stationIndex];

                var stop =
                    busStops
                        .FirstOrDefault(
                            candidate =>
                                candidate.Id ==
                                    station.Id);

                var current =
                    profile.StopTimes
                        .FirstOrDefault(
                            value =>
                                value.StationIndex ==
                                    stationIndex);

                var row =
                    new Grid
                    {
                        ColumnSpacing =
                            8,
                        Padding =
                            new Thickness(
                                5,
                                2,
                                5,
                                2)
                    };

                row.ColumnDefinitions.Add(
                    new ColumnDefinition());

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                150)
                    });

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                130)
                    });

                var stopLabel =
                    new TextBlock
                    {
                        Text =
                            $"{stationIndex + 1:00} · Stop {station.Id}" +
                            (
                                string.IsNullOrWhiteSpace(
                                    stop?.Name)
                                    ? string.Empty
                                    : $" · {stop.Name}"
                            ),
                        VerticalAlignment =
                            VerticalAlignment.Center,
                        TextTrimming =
                            TextTrimming.CharacterEllipsis
                    };

                var arrivalBox =
                    new NumberBox
                    {
                        Minimum =
                            0,
                        Maximum =
                            100000,
                        SmallChange =
                            0.25,
                        SpinButtonPlacementMode =
                            NumberBoxSpinButtonPlacementMode
                                .Compact,
                        Value =
                            current?.Minutes ??
                            double.NaN
                    };

                var segment =
                    current is null
                        ? (double?)null
                        : Math.Max(
                            0,
                            current.Minutes -
                            (
                                previousMinutes ??
                                0
                            ));

                var segmentText =
                    new TextBlock
                    {
                        Text =
                            segment is
                                double value
                                ? $"{value:0.###} min"
                                : "—",
                        VerticalAlignment =
                            VerticalAlignment.Center,
                        Foreground =
                            new SolidColorBrush(
                                Windows.UI.Color.FromArgb(
                                    255,
                                    127,
                                    198,
                                    232))
                    };

                Grid.SetColumn(
                    stopLabel,
                    0);
                Grid.SetColumn(
                    arrivalBox,
                    1);
                Grid.SetColumn(
                    segmentText,
                    2);

                row.Children.Add(
                    stopLabel);
                row.Children.Add(
                    arrivalBox);
                row.Children.Add(
                    segmentText);

                stopTimesPanel.Children.Add(
                    row);

                stopTimeEditors.Add(
                    (
                        stationIndex,
                        arrivalBox,
                        segmentText
                    ));

                if (current is not null)
                {
                    previousMinutes =
                        current.Minutes;
                }
            }

            profileInfoText.Text =
                $"{trip.Stations.Count} parada(s) · edite chegadas acumuladas e aplique a tabela ao perfil {profile.Name}.";
        }

        void RefreshProfileFields()
        {
            if (
                profileCombo.SelectedItem is not
                    OmsiTimetableProfileDefinition
                        profile)
            {
                totalMinutesBox.Value =
                    double.NaN;

                deleteProfileButton.IsEnabled =
                    false;

                profileInfoText.Text =
                    "Nenhum perfil. Crie um perfil para configurar os tempos.";

                RefreshStopTimesTable();

                return;
            }

            totalMinutesBox.Value =
                profile.TotalMinutes ??
                double.NaN;

            deleteProfileButton.IsEnabled =
                true;

            RefreshStopTimesTable();
        }

        void RefreshProfiles(
            int preferredIndex =
                0)
        {
            profiles =
                OmsiTimetableProfileEditor
                    .ReadProfiles(
                        workingLines)
                    .ToList();

            synchronizing =
                true;

            try
            {
                profileCombo.ItemsSource =
                    profiles;

                profileCombo.SelectedIndex =
                    profiles.Count >
                    0
                        ? Math.Clamp(
                            preferredIndex,
                            0,
                            profiles.Count -
                                1)
                        : -1;

                rawEditor.Text =
                    string.Join(
                        Environment.NewLine,
                        workingLines);
            }
            finally
            {
                synchronizing =
                    false;
            }

            RefreshProfileFields();
        }

        profileCombo.SelectionChanged +=
            (
                _,
                _
            ) =>
            {
                if (!synchronizing)
                {
                    RefreshProfileFields();
                }
            };

        applyTotalButton.Click +=
            (
                _,
                _
            ) =>
            {
                if (
                    profileCombo.SelectedIndex <
                        0 ||
                    !double.IsFinite(
                        totalMinutesBox.Value) ||
                    totalMinutesBox.Value <=
                        0)
                {
                    profileInfoText.Text =
                        "Informe um tempo total válido.";
                    return;
                }

                workingLines =
                    ReadRawLines();

                workingLines =
                    OmsiTimetableProfileEditor
                        .SetTotalMinutes(
                            workingLines,
                            profileCombo
                                .SelectedIndex,
                            totalMinutesBox
                                .Value)
                        .ToList();

                RefreshProfiles(
                    profileCombo
                        .SelectedIndex);
            };

        applyStopTableButton.Click +=
            (
                _,
                _
            ) =>
            {
                if (
                    profileCombo.SelectedIndex <
                        0)
                {
                    profileInfoText.Text =
                        "Selecione um perfil.";
                    return;
                }

                workingLines =
                    ReadRawLines();

                var applied =
                    0;

                foreach (
                    var editor in
                        stopTimeEditors)
                {
                    if (
                        !double.IsFinite(
                            editor.Arrival.Value))
                    {
                        continue;
                    }

                    if (
                        editor.Arrival.Value <
                            0)
                    {
                        profileInfoText.Text =
                            $"Parada {editor.StationIndex + 1}: tempo inválido.";
                        return;
                    }

                    workingLines =
                        OmsiTimetableProfileEditor
                            .SetManualArrivalMinutes(
                                workingLines,
                                profileCombo
                                    .SelectedIndex,
                                editor
                                    .StationIndex,
                                editor
                                    .Arrival
                                    .Value)
                            .ToList();

                    applied++;
                }

                RefreshProfiles(
                    profileCombo
                        .SelectedIndex);

                profileInfoText.Text =
                    $"{applied} tempo(s) de chegada aplicado(s) ao perfil.";
            };

        addProfileButton.Click +=
            (
                _,
                _
            ) =>
            {
                if (
                    string.IsNullOrWhiteSpace(
                        newProfileNameBox.Text) ||
                    !double.IsFinite(
                        newProfileMinutesBox.Value) ||
                    newProfileMinutesBox.Value <=
                        0)
                {
                    profileInfoText.Text =
                        "Novo perfil: informe nome e duração válidos.";
                    return;
                }

                try
                {
                    workingLines =
                        ReadRawLines();

                    workingLines =
                        OmsiTimetableProfileEditor
                            .CreateProfile(
                                workingLines,
                                newProfileNameBox
                                    .Text,
                                newProfileMinutesBox
                                    .Value)
                            .ToList();

                    newProfileNameBox.Text =
                        string.Empty;

                    RefreshProfiles(
                        int.MaxValue);
                }
                catch (Exception exception)
                {
                    profileInfoText.Text =
                        $"Não foi possível criar o perfil: {exception.Message}";
                }
            };

        deleteProfileButton.Click +=
            (
                _,
                _
            ) =>
            {
                if (
                    profileCombo.SelectedIndex <
                        0)
                {
                    return;
                }

                var index =
                    profileCombo
                        .SelectedIndex;

                workingLines =
                    ReadRawLines();

                workingLines =
                    OmsiTimetableProfileEditor
                        .DeleteProfile(
                            workingLines,
                            index)
                        .ToList();

                RefreshProfiles(
                    Math.Max(
                        0,
                        index -
                            1));
            };

        var profileGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        profileGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        profileGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        150)
            });

        Grid.SetColumn(
            profileCombo,
            0);

        Grid.SetColumn(
            totalMinutesBox,
            1);

        profileGrid.Children.Add(
            profileCombo);

        profileGrid.Children.Add(
            totalMinutesBox);

        var newProfileGrid =
            new Grid
            {
                ColumnSpacing =
                    6
            };

        newProfileGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        newProfileGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        150)
            });

        Grid.SetColumn(
            newProfileNameBox,
            0);

        Grid.SetColumn(
            newProfileMinutesBox,
            1);

        newProfileGrid.Children.Add(
            newProfileNameBox);

        newProfileGrid.Children.Add(
            newProfileMinutesBox);

        var profileActions =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,
                Spacing =
                    6
            };

        profileActions.Children.Add(
            applyTotalButton);

        profileActions.Children.Add(
            deleteProfileButton);

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    620
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Configure duração total e os tempos de chegada por parada. O Map Studio calcula o trecho anterior pela diferença entre chegadas.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            });

        panel.Children.Add(
            profileGrid);

        panel.Children.Add(
            profileActions);

        panel.Children.Add(
            new ScrollViewer
            {
                Content =
                    stopTimesPanel,
                MaxHeight =
                    330,
                HorizontalScrollMode =
                    ScrollMode.Enabled,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                VerticalScrollMode =
                    ScrollMode.Enabled,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            });

        panel.Children.Add(
            applyStopTableButton);

        panel.Children.Add(
            profileInfoText);

        panel.Children.Add(
            new Border
            {
                Height =
                    1,
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        4),
                Opacity =
                    0.24,
                Background =
                    new SolidColorBrush(
                        Microsoft.UI.Colors.White)
            });

        panel.Children.Add(
            newProfileGrid);

        panel.Children.Add(
            addProfileButton);

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Avançado: o conteúdo OMSI bruto continua disponível e é preservado no salvamento.",
                FontSize =
                    11,
                Opacity =
                    0.72,
                TextWrapping =
                    TextWrapping.Wrap
            });

        panel.Children.Add(
            rawEditor);

        RefreshProfiles();

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    xamlRoot,
                Title =
                    $"Perfis de tempo · {trip.Name}",
                Content =
                    new ScrollViewer
                    {
                        Content =
                            panel,
                        MaxHeight =
                            680
                    },
                PrimaryButtonText =
                    "Salvar perfis",
                CloseButtonText =
                    "Cancelar",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        if (
            await dialog.ShowAsync() !=
                ContentDialogResult.Primary)
        {
            return null;
        }

        var lines =
            ReadRawLines();

        return trip with
        {
            ProfileLines =
                lines
        };
    }
}
