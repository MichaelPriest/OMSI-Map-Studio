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

    public Func<OmsiTimetableLine, OmsiTimetableLine, Task<bool>>? SaveLineAsync { get; set; }

    public Func<OmsiTimetableTrip, OmsiTimetableTrip, Task<bool>>? SaveTripAsync { get; set; }

    private TimetableLineEditor? _lineEditor;
    private OmsiTimetableLine? _editingLine;
    private bool _savingLine;
    private bool _savingTrip;

    public event Action<string>?
        TripRouteRequested;

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

        _catalog = catalog;
        // Catalog refreshes must not replace an open draft.
        if (_lineEditor is not null)
        {
            return;
        }

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

            EditTripButton.IsEnabled =
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

        EditTripButton.IsEnabled =
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
            !_savingTrip &&
            ScheduleListView.SelectedItem is
                TimetableScheduleRow;

        FocusTripButton.IsEnabled =
            selected;

        EditTripButton.IsEnabled =
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

    private async void OnEditTripClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            ScheduleListView.SelectedItem is not
                TimetableScheduleRow row ||
            _savingTrip)
        {
            return;
        }

        var trip =
            _catalog.Trips
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            row.TripName,
                            StringComparison.OrdinalIgnoreCase));

        if (trip is null)
        {
            LineSummaryText.Text =
                $"Trip {row.TripName} não foi encontrado no catálogo atual.";
            return;
        }

        var updatedTrip =
            await TimetableTripEditorDialog
                .ShowAsync(
                    RootGrid.XamlRoot,
                    trip,
                    _catalog);

        if (updatedTrip is null)
        {
            return;
        }

        await SaveTripFromWindowAsync(
            trip,
            updatedTrip);
    }

    private async void OnEditProfileClick(
        object sender,
        RoutedEventArgs e)
    {
        if (
            ScheduleListView.SelectedItem is not
                TimetableScheduleRow row)
        {
            return;
        }

        var trip =
            _catalog.Trips
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            row.TripName,
                            StringComparison.OrdinalIgnoreCase));

        if (trip is null)
        {
            return;
        }

        var updatedTrip =
            await TimetableProfileEditorDialog
                .ShowAsync(
                    RootGrid.XamlRoot,
                    trip,
                    _catalog.BusStops);

        if (updatedTrip is null)
        {
            return;
        }

        await SaveTripFromWindowAsync(
            trip,
            updatedTrip);
    }

    private async Task SaveTripFromWindowAsync(
        OmsiTimetableTrip sourceTrip,
        OmsiTimetableTrip updatedTrip)
    {
        if (
            _savingTrip ||
            SaveTripAsync is null)
        {
            return;
        }

        _savingTrip =
            true;

        FocusTripButton.IsEnabled =
            false;
        EditTripButton.IsEnabled =
            false;
        EditProfileButton.IsEnabled =
            false;

        try
        {
            if (
                !await SaveTripAsync(
                    sourceTrip,
                    updatedTrip))
            {
                LineSummaryText.Text =
                    $"Trip {sourceTrip.Name}: salvamento não concluído. Consulte o status da janela principal.";
            }
        }
        catch (Exception exception)
        {
            LineSummaryText.Text =
                $"Trip {sourceTrip.Name}: falha ao salvar · {exception.Message}";
        }
        finally
        {
            _savingTrip =
                false;

            var selected =
                _lineEditor is null &&
                ScheduleListView.SelectedItem is
                    TimetableScheduleRow;

            FocusTripButton.IsEnabled =
                selected;
            EditTripButton.IsEnabled =
                selected;
            EditProfileButton.IsEnabled =
                selected;
        }
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

    private void OnEditLineClick(object sender, RoutedEventArgs e)
    {
        if (LineComboBox.SelectedItem is not OmsiTimetableLine line || _lineEditor is not null)
            return;

        _editingLine = line;
        _lineEditor = new TimetableLineEditor(
            line, _catalog.Trips.Select(trip => trip.Name).ToArray());
        EditorHost.Content = _lineEditor;
        EditorPanel.Visibility = Visibility.Visible;
        ScheduleListView.Visibility = Visibility.Collapsed;
        ScheduleHeader.Visibility = Visibility.Collapsed;
        LineComboBox.IsEnabled = false;
        EditLineButton.IsEnabled = false;
        EditTripButton.IsEnabled = false;
        EditProfileButton.IsEnabled = false;
        FocusTripButton.IsEnabled = false;
        EditorStatusText.Text = "Edite a tabela e salve, ou descarte para voltar.";
    }

    private async void OnSaveLineClick(object sender, RoutedEventArgs e)
    {
        if (_savingLine || _lineEditor is null || _editingLine is null)
            return;

        try
        {
            if (
                !_lineEditor.TryBuildLine(
                    out var updatedLine,
                    out var validationError) ||
                updatedLine is null)
            {
                EditorStatusText.Text =
                    validationError;
                return;
            }

            var save = SaveLineAsync
                ?? throw new InvalidOperationException("Salvamento indisponível.");
            _savingLine = true;
            EditorHost.IsEnabled = false;
            SaveLineButton.IsEnabled = false;
            DiscardLineButton.IsEnabled = false;
            EditorStatusText.Text = "Salvando Line/Tours...";
            if (await save(_editingLine, updatedLine))
            {
                EndLineEditing();
            }
            else
            {
                EditorStatusText.Text = "Não foi possível concluir o salvamento. A tabela foi mantida. Consulte o status da janela principal.";
            }
        }
        catch (Exception exception)
        {
            EditorStatusText.Text = $"Não foi possível salvar: {exception.Message}";
        }
        finally
        {
            _savingLine = false;
            EditorHost.IsEnabled = true;
            SaveLineButton.IsEnabled = true;
            DiscardLineButton.IsEnabled = true;
        }
    }

    private void OnDiscardLineClick(object sender, RoutedEventArgs e)
    {
        if (!_savingLine)
            EndLineEditing();
    }

    private void EndLineEditing()
    {
        _lineEditor = null;
        _editingLine = null;
        EditorHost.Content = null;
        EditorPanel.Visibility = Visibility.Collapsed;
        ScheduleListView.Visibility = Visibility.Visible;
        ScheduleHeader.Visibility = Visibility.Visible;
        LineComboBox.IsEnabled = true;
        SetCatalog(_catalog);
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

        appWindow.Closing += (_, args) =>
        {
            if (_lineEditor is null)
                return;
            args.Cancel = true;
            EditorStatusText.Text = _savingLine
                ? "Aguarde o salvamento terminar."
                : "Salve ou descarte a edição antes de fechar a janela.";
        };

        appWindow.Resize(
            new SizeInt32(
                1120,
                720));
    }
}
