using MapStudio.Core.Omsi.Timetables;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MapStudio.Native.Dialogs;

internal sealed class TimetableLineEditor : ContentControl
{
    private sealed record RowEditor(
        Grid Row,
        TextBox Tour,
        TextBox AiGroup,
        TextBox Line3,
        TextBox Comment,
        ComboBox Trip,
        TextBox TripLine2,
        TextBox Departure);

    private readonly Func<OmsiTimetableLine> _buildLine;

    public TimetableLineEditor(
        OmsiTimetableLine line,
        IReadOnlyCollection<string> knownTripNames)
    {
        ArgumentNullException.ThrowIfNull(
            line);

        ArgumentNullException.ThrowIfNull(
            knownTripNames);

        var priorityBox =
            new TextBox
            {
                Header =
                    "Priority",
                Text =
                    line.Priority,
                Width =
                    120
            };

        var userAllowedBox =
            new CheckBox
            {
                Content =
                    "Jogador permitido [userallowed]",
                IsChecked =
                    line.UserAllowed,
                VerticalAlignment =
                    VerticalAlignment.Bottom
            };

        var lineOptionsGrid =
            new Grid
            {
                ColumnSpacing =
                    12
            };

        lineOptionsGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        lineOptionsGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        Grid.SetColumn(
            priorityBox,
            0);

        Grid.SetColumn(
            userAllowedBox,
            1);

        lineOptionsGrid.Children.Add(
            priorityBox);

        lineOptionsGrid.Children.Add(
            userAllowedBox);

        var knownTrips =
            knownTripNames
                .Where(
                    name =>
                        !string.IsNullOrWhiteSpace(
                            name))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    name => name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        var existingRows =
            OmsiTimetableLineTableEditor
                .Flatten(
                    line);

        var tripOptions =
            knownTrips
                .Concat(
                    existingRows
                        .Select(
                            row =>
                                row.TripName))
                .Where(
                    name =>
                        !string.IsNullOrWhiteSpace(
                            name))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    name => name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        var rowsPanel =
            new StackPanel
            {
                Spacing =
                    3,
                MinWidth =
                    1120
            };

        static void ConfigureColumns(
            Grid grid)
        {
            foreach (
                var width in
                    new[]
                    {
                        130d,
                        120d,
                        80d,
                        150d,
                        180d,
                        80d,
                        110d
                    })
            {
                grid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                width)
                    });
            }

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });
        }

        static TextBlock CreateHeader(
            string text,
            int column)
        {
            var block =
                new TextBlock
                {
                    Text =
                        text,
                    FontSize =
                        10,
                    FontWeight =
                        Microsoft.UI.Text
                            .FontWeights
                            .SemiBold,
                    Foreground =
                        new SolidColorBrush(
                            Windows.UI.Color.FromArgb(
                                255,
                                158,
                                220,
                                244)),
                    Margin =
                        new Thickness(
                            5,
                            0,
                            5,
                            3)
                };

            Grid.SetColumn(
                block,
                column);

            return block;
        }

        var headerGrid =
            new Grid
            {
                ColumnSpacing =
                    5,
                Background =
                    new SolidColorBrush(
                        Windows.UI.Color.FromArgb(
                            255,
                            11,
                            32,
                            45)),
                Padding =
                    new Thickness(
                        4)
            };

        ConfigureColumns(
            headerGrid);

        var headers =
            new[]
            {
                "Tour",
                "AI Group",
                "Line3",
                "Comentário",
                "Trip",
                "Line2",
                "Saída",
                string.Empty
            };

        for (
            var column = 0;
            column <
                headers.Length;
            column++)
        {
            headerGrid.Children.Add(
                CreateHeader(
                    headers[column],
                    column));
        }

        rowsPanel.Children.Add(
            headerGrid);

        var rowEditors =
            new List<
                RowEditor>();

        void AddRow(
            OmsiTimetableLineTableRow?
                source)
        {
            var last =
                rowEditors
                    .LastOrDefault();

            var row =
                new Grid
                {
                    ColumnSpacing =
                        5,
                    Padding =
                        new Thickness(
                            4,
                            2,
                            4,
                            2),
                    Background =
                        new SolidColorBrush(
                            Windows.UI.Color.FromArgb(
                                255,
                                7,
                                23,
                                34))
                };

            ConfigureColumns(
                row);

            var tourBox =
                new TextBox
                {
                    Text =
                        source?.TourName ??
                        last?.Tour.Text ??
                        line.Tours
                            .FirstOrDefault()
                            ?.Name ??
                        "Tour 1",
                    PlaceholderText =
                        "Tour"
                };

            var aiGroupBox =
                new TextBox
                {
                    Text =
                        source?.AiGroupName ??
                        last?.AiGroup.Text ??
                        line.Tours
                            .FirstOrDefault()
                            ?.AiGroupName ??
                        "Busses",
                    PlaceholderText =
                        "Busses"
                };

            var line3Box =
                new TextBox
                {
                    Text =
                        source?.TourLine3 ??
                        last?.Line3.Text ??
                        "0",
                    PlaceholderText =
                        "0"
                };

            var commentBox =
                new TextBox
                {
                    Text =
                        source?.Comment ??
                        string.Empty,
                    PlaceholderText =
                        "comentário"
                };

            var tripBox =
                new ComboBox
                {
                    ItemsSource =
                        tripOptions,
                    SelectedItem =
                        source?.TripName ??
                        knownTrips
                            .FirstOrDefault(),
                    HorizontalAlignment =
                        HorizontalAlignment.Stretch
                };

            var tripLine2Box =
                new TextBox
                {
                    Text =
                        source?.TripLine2 ??
                        "0",
                    PlaceholderText =
                        "0"
                };

            var departureBox =
                new TextBox
                {
                    Text =
                        source?.DepartureText ??
                        "08:00:00",
                    PlaceholderText =
                        "08:00:00"
                };

            ToolTipService.SetToolTip(
                departureBox,
                "Aceita HH:mm, HH:mm:ss ou segundos OMSI.");

            var removeButton =
                new Button
                {
                    Content =
                        "✕",
                    Padding =
                        new Thickness(
                            9,
                            5,
                            9,
                            5),
                    Tag =
                        row
                };

            ToolTipService.SetToolTip(
                removeButton,
                "Remover esta saída");

            FrameworkElement[]
                controls =
                [
                    tourBox,
                    aiGroupBox,
                    line3Box,
                    commentBox,
                    tripBox,
                    tripLine2Box,
                    departureBox,
                    removeButton
                ];

            for (
                var column = 0;
                column <
                    controls.Length;
                column++)
            {
                Grid.SetColumn(
                    controls[column],
                    column);

                row.Children.Add(
                    controls[column]);
            }

            var editor =
                new RowEditor(
                    row,
                    tourBox,
                    aiGroupBox,
                    line3Box,
                    commentBox,
                    tripBox,
                    tripLine2Box,
                    departureBox);

            rowEditors.Add(
                editor);

            rowsPanel.Children.Add(
                row);

            removeButton.Click +=
                (
                    sender,
                    _
                ) =>
                {
                    if (
                        rowEditors.Count <=
                            1 ||
                        sender is not
                            Button button ||
                        button.Tag is not
                            Grid target)
                    {
                        return;
                    }

                    var targetEditor =
                        rowEditors
                            .FirstOrDefault(
                                candidate =>
                                    ReferenceEquals(
                                        candidate.Row,
                                        target));

                    if (targetEditor is null)
                    {
                        return;
                    }

                    rowEditors.Remove(
                        targetEditor);

                    rowsPanel.Children.Remove(
                        target);
                };
        }

        foreach (
            var row in
                existingRows)
        {
            AddRow(
                row);
        }

        if (rowEditors.Count == 0)
        {
            AddRow(
                null);
        }

        var addRowButton =
            new Button
            {
                Content =
                    "+ Adicionar saída",
                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        addRowButton.Click +=
            (
                _,
                _
            ) =>
                AddRow(
                    null);

        var tableScroll =
            new ScrollViewer
            {
                Content =
                    rowsPanel,
                HorizontalScrollMode =
                    ScrollMode.Enabled,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                VerticalScrollMode =
                    ScrollMode.Enabled,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                MaxHeight =
                    440
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    720
            };

        panel.Children.Add(
            lineOptionsGrid);

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Tabela de horários · cada linha representa um [addtrip]. Tours com o mesmo nome são agrupados no arquivo .ttl.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            });

        panel.Children.Add(
            addRowButton);

        panel.Children.Add(
            tableScroll);

        Content = panel;
        _buildLine = () => line with
        {
            Priority = priorityBox.Text.Trim(),
            UserAllowed = userAllowedBox.IsChecked == true,
            Tours = OmsiTimetableLineTableEditor.BuildTours(
                rowEditors.Select(editor => new OmsiTimetableLineTableRow(
                    editor.Tour.Text, editor.AiGroup.Text, editor.Line3.Text,
                    editor.Comment.Text, editor.Trip.SelectedItem?.ToString() ?? string.Empty,
                    editor.TripLine2.Text, editor.Departure.Text)),
                knownTrips)
        };
    }

    public OmsiTimetableLine BuildLine() => _buildLine();
}
