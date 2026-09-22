using MapStudio.Core.Omsi.Timetables;
using MapStudio.Native.Dialogs;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT.Interop;

namespace MapStudio.Native;

public sealed partial class TimetableWindow
    : Window
{
    private sealed record TimetableScheduleRow(
        string TourName,
        string AiGroupName,
        string TripName,
        string DepartureText,
        string DestinationText,
        string RouteText);

    public event Action<
        OmsiTimetableLine,
        OmsiTimetableLine>?
        LineSaveRequested;

    public event Action<string>?
        TripRouteRequested;

    public event Action<string>?
        TripProfileRequested;

    private OmsiTimetableCatalog
        _catalog;

    public TimetableWindow(
        OmsiTimetableCatalog catalog)
    {
        _catalog =
            catalog ??
            throw new ArgumentNullException(
                nameof(
                    catalog));

        InitializeComponent();

        ResizeWindow();

        SetCatalog(
            catalog);
    }

    public void SetCatalog(
        OmsiTimetableCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(
            catalog);

        var previous =
            LineComboBox
                .SelectedItem is
                OmsiTimetableLine selected
                ? selected.Name
                : null;

        _catalog =
            catalog;

        LineComboBox.ItemsSource =
            _catalog.Lines;

        if (_catalog.Lines.Count == 0)
        {
            LineComboBox.SelectedIndex =
                -1;

            ScheduleListView.ItemsSource =
                Array.Empty<
                    TimetableScheduleRow>();

            FocusTripButton.IsEnabled =
                false;

            EditProfileButton.IsEnabled =
                false;

            EditLineButton.IsEnabled =
                false;

            LineSummaryText.Text =
                "Nenhuma Line (.ttl) carregada.";

            return;
        }

        var index =
            string.IsNullOrWhiteSpace(
                previous)
                ? 0
                : _catalog.Lines
                    .Select(
                        (line, lineIndex) =>
                            (
                                line,
                                lineIndex
                            ))
                    .FirstOrDefault(
                        pair =>
                            string.Equals(
                                pair.line.Name,
                                previous,
                                StringComparison.OrdinalIgnoreCase))
                    .lineIndex;

        if (
            index < 0 ||
            index >=
                _catalog.Lines.Count)
        {
            index =
                0;
        }

        LineComboBox.SelectedIndex =
            index;

        EditLineButton.IsEnabled =
            true;

        RefreshSchedule();
    }

    private void OnLineSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        FocusTripButton.IsEnabled =
            false;

        EditProfileButton.IsEnabled =
            false;

        RefreshSchedule();
    }

    private void OnScheduleSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var selected =
            ScheduleListView.SelectedItem is
                TimetableScheduleRow;

        FocusTripButton.IsEnabled =
            selected;

        EditProfileButton.IsEnabled =
            selected;
    }

    private void OnScheduleDoubleTapped(
        object sender,
        Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        RequestSelectedTripRoute();

        e.Handled =
            true;
    }

    private void OnFocusTripClick(
        object sender,
        RoutedEventArgs e) =>
        RequestSelectedTripRoute();

    private void OnEditProfileClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            ScheduleListView.SelectedItem is not
                TimetableScheduleRow row)
        {
            return;
        }

        TripProfileRequested
            ?.Invoke(
                row.TripName);
    }

    private void RequestSelectedTripRoute()
    {
        if (
            ScheduleListView.SelectedItem is not
                TimetableScheduleRow row)
        {
            return;
        }

        TripRouteRequested
            ?.Invoke(
                row.TripName);
    }

    private async void OnEditLineClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            LineComboBox.SelectedItem is not
                OmsiTimetableLine line)
        {
            return;
        }

        var updatedLine =
            await TimetableLineEditorDialog
                .ShowAsync(
                    RootGrid.XamlRoot,
                    line,
                    _catalog.Trips
                        .Select(
                            trip =>
                                trip.Name)
                        .ToArray());

        if (updatedLine is null)
        {
            return;
        }

        LineSaveRequested
            ?.Invoke(
                line,
                updatedLine);
    }

    private void RefreshSchedule()
    {
        if (
            LineComboBox.SelectedItem is not
                OmsiTimetableLine line)
        {
            ScheduleListView.ItemsSource =
                Array.Empty<
                    TimetableScheduleRow>();

            LineSummaryText.Text =
                "Selecione uma Line.";

            return;
        }

        var rows =
            new List<
                TimetableScheduleRow>();

        foreach (
            var tour in
                line.Tours)
        {
            foreach (
                var scheduledTrip in
                    tour.Trips)
            {
                var trip =
                    _catalog.Trips
                        .FirstOrDefault(
                            candidate =>
                                string.Equals(
                                    candidate.Name,
                                    scheduledTrip.TripName,
                                    StringComparison.OrdinalIgnoreCase));

                var destination =
                    trip is null
                        ? "Trip não encontrado"
                        : string.IsNullOrWhiteSpace(
                            trip.EffectiveLine)
                            ? trip.EffectiveDestination
                            : $"{trip.EffectiveLine} · {trip.EffectiveDestination}";

                var route =
                    trip is null
                        ? "—"
                        : trip.UsesStationLinks
                            ? "StationLinks"
                            : string.IsNullOrWhiteSpace(
                                trip.EffectiveTrackName)
                                ? "Track não informado"
                                : $"Track {trip.EffectiveTrackName}";

                rows.Add(
                    new TimetableScheduleRow(
                        tour.Name,
                        tour.AiGroupName,
                        scheduledTrip.TripName,
                        OmsiTimetableDepartureTime
                            .FormatEditorValue(
                                scheduledTrip.DepartureTime),
                        destination,
                        route));
            }
        }

        ScheduleListView.ItemsSource =
            rows;

        LineSummaryText.Text =
            $"{line.Name} · {line.Tours.Count} tour(s) · {rows.Count} saída(s) · " +
            $"priority {line.Priority} · jogador {(line.UserAllowed ? "permitido" : "bloqueado")}";
    }

    private void ResizeWindow()
    {
        var handle =
            WindowNative
                .GetWindowHandle(
                    this);

        var windowId =
            Microsoft.UI.Win32Interop
                .GetWindowIdFromWindow(
                    handle);

        var appWindow =
            AppWindow
                .GetFromWindowId(
                    windowId);

        appWindow.Resize(
            new SizeInt32(
                1120,
                720));
    }
}
