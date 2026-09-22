using System.Globalization;
using MapStudio.Core.Omsi.Timetables;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MapStudio.Native.Dialogs;

internal static class TimetableTripEditorDialog
{
    public static async Task<
        OmsiTimetableTrip?>
        ShowAsync(
            XamlRoot xamlRoot,
            OmsiTimetableTrip trip,
            OmsiTimetableCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(
            xamlRoot);

        ArgumentNullException.ThrowIfNull(
            trip);

        ArgumentNullException.ThrowIfNull(
            catalog);

        var stationLinkTrip =
            trip.UsesStationLinks;

        var trackBox =
            new TextBox
            {
                Header =
                    stationLinkTrip
                        ? "Destino / letreiro · campo 1 OMSI do tipo 2"
                        : "Track",
                Text =
                    trip.TrackName
            };

        var destinationBox =
            new TextBox
            {
                Header =
                    stationLinkTrip
                        ? "Linha · campo 2 OMSI do tipo 2"
                        : "Destino",
                Text =
                    trip.Destination
            };

        var lineBox =
            new TextBox
            {
                Header =
                    stationLinkTrip
                        ? "Campo 3 OMSI · deve permanecer vazio no tipo 2"
                        : "Linha",
                Text =
                    trip.Line
            };

        var reverseBox =
            new CheckBox
            {
                Content =
                    "Train reverse",
                IsChecked =
                    trip.TrainReverse
            };

        var stationsBox =
            new TextBox
            {
                Header =
                    "Stations · T2|id ou T1|id|interval|name|tile|line5|line6|line7|line8",
                AcceptsReturn =
                    true,
                TextWrapping =
                    TextWrapping.NoWrap,
                MinHeight =
                    220,
                FontFamily =
                    new FontFamily(
                        "Consolas"),
                Text =
                    string.Join(
                        Environment.NewLine,
                        trip.Stations.Select(
                            station =>
                                station switch
                                {
                                    OmsiTimetableTripStationType2
                                        type2 =>
                                        $"T2|{type2.Id}",

                                    OmsiTimetableTripStationType1
                                        type1 =>
                                        $"T1|{type1.Id}|{type1.Interval}|{type1.Name}|{type1.TileIndex}|{type1.Line5}|{type1.Line6}|{type1.Line7}|{type1.Line8}",

                                    _ =>
                                        string.Empty
                                }))
            };

        var profilesBox =
            new TextBox
            {
                Header =
                    "Profiles · avançado",
                AcceptsReturn =
                    true,
                TextWrapping =
                    TextWrapping.NoWrap,
                MinHeight =
                    180,
                FontFamily =
                    new FontFamily(
                        "Consolas"),
                Text =
                    string.Join(
                        Environment.NewLine,
                        trip.ProfileLines)
            };

        var validationText =
            new TextBlock
            {
                TextWrapping =
                    TextWrapping.Wrap,
                Foreground =
                    new SolidColorBrush(
                        Windows.UI.Color.FromArgb(
                            255,
                            255,
                            155,
                            140))
            };

        var panel =
            new StackPanel
            {
                Spacing =
                    8,
                MinWidth =
                    560
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    stationLinkTrip
                        ? "Trip tipo 2: o caminho é resolvido pela sequência de StationLinks entre os stops; não existe Track associado."
                        : "Trip tipo 1: o caminho físico vem do Track associado.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.78
            });

        panel.Children.Add(
            trackBox);
        panel.Children.Add(
            destinationBox);
        panel.Children.Add(
            lineBox);
        panel.Children.Add(
            reverseBox);
        panel.Children.Add(
            stationsBox);
        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Os perfis podem ser editados visualmente pelo botão Editar perfil no Timetable. Este campo mantém acesso aos dados OMSI brutos.",
                TextWrapping =
                    TextWrapping.Wrap,
                Opacity =
                    0.72
            });
        panel.Children.Add(
            profilesBox);
        panel.Children.Add(
            validationText);

        OmsiTimetableTrip?
            updatedTrip =
                null;

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    xamlRoot,
                Title =
                    $"Editar Trip · {trip.Name}",
                Content =
                    new ScrollViewer
                    {
                        Content =
                            panel,
                        MaxHeight =
                            650
                    },
                PrimaryButtonText =
                    "Salvar Trip",
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
                validationText.Text =
                    string.Empty;

                var stations =
                    new List<
                        OmsiTimetableTripStation>();

                var stationLineNumber =
                    0;

                foreach (
                    var rawLine in
                        stationsBox.Text
                            .Replace(
                                "\r\n",
                                "\n",
                                StringComparison.Ordinal)
                            .Split('\n'))
                {
                    stationLineNumber++;

                    var line =
                        rawLine.Trim();

                    if (
                        string.IsNullOrWhiteSpace(
                            line))
                    {
                        continue;
                    }

                    var parts =
                        line.Split(
                            '|');

                    if (
                        parts.Length ==
                            2 &&
                        string.Equals(
                            parts[0],
                            "T2",
                            StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(
                            parts[1],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var type2Id) &&
                        type2Id >=
                            0)
                    {
                        stations.Add(
                            new OmsiTimetableTripStationType2(
                                type2Id));
                        continue;
                    }

                    if (
                        parts.Length ==
                            9 &&
                        string.Equals(
                            parts[0],
                            "T1",
                            StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(
                            parts[1],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var type1Id) &&
                        type1Id >=
                            0 &&
                        int.TryParse(
                            parts[4],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var tileIndex))
                    {
                        stations.Add(
                            new OmsiTimetableTripStationType1(
                                type1Id,
                                parts[2],
                                parts[3],
                                tileIndex,
                                parts[5],
                                parts[6],
                                parts[7],
                                parts[8]));
                        continue;
                    }

                    validationText.Text =
                        $"Station inválida na linha {stationLineNumber}.";
                    args.Cancel =
                        true;
                    return;
                }

                if (stations.Count == 0)
                {
                    validationText.Text =
                        "Mantenha pelo menos uma station.";
                    args.Cancel =
                        true;
                    return;
                }

                if (
                    stationLinkTrip &&
                    !string.IsNullOrWhiteSpace(
                        lineBox.Text))
                {
                    validationText.Text =
                        "Trip tipo 2: o terceiro campo [trip] deve permanecer vazio.";
                    args.Cancel =
                        true;
                    return;
                }

                if (stationLinkTrip)
                {
                    var validation =
                        OmsiTimetableTripValidator
                            .ValidateType2StationLinks(
                                catalog,
                                stations);

                    if (!validation.IsValid)
                    {
                        validationText.Text =
                            validation.Failure switch
                            {
                                OmsiTimetableType2TripValidationFailure
                                    .InvalidStationSequence =>
                                    "Trip tipo 2: use pelo menos dois stops do tipo 2.",

                                OmsiTimetableType2TripValidationFailure
                                    .UnknownStop =>
                                    $"Trip tipo 2: o stop #{validation.StopId} não existe em Busstops.cfg.",

                                OmsiTimetableType2TripValidationFailure
                                    .MissingStationLink =>
                                    $"Trip tipo 2: não existe StationLink {validation.StartStopId} → {validation.EndStopId}.",

                                _ =>
                                    "Trip tipo 2: sequência de StationLinks inválida."
                            };

                        args.Cancel =
                            true;
                        return;
                    }
                }

                var profiles =
                    profilesBox.Text
                        .Replace(
                            "\r\n",
                            "\n",
                            StringComparison.Ordinal)
                        .Split('\n')
                        .Select(
                            value =>
                                value.Trim())
                        .Where(
                            value =>
                                !string.IsNullOrWhiteSpace(
                                    value))
                        .ToArray();

                updatedTrip =
                    trip with
                    {
                        TrackName =
                            trackBox.Text.Trim(),
                        Destination =
                            destinationBox.Text.Trim(),
                        Line =
                            lineBox.Text.Trim(),
                        TrainReverse =
                            reverseBox.IsChecked ==
                            true,
                        Stations =
                            stations.ToArray(),
                        ProfileLines =
                            profiles
                    };
            };

        if (
            await dialog.ShowAsync() !=
                ContentDialogResult.Primary)
        {
            return null;
        }

        return updatedTrip;
    }
}
