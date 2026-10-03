namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableLineTableRow(
    string TourName,
    string AiGroupName,
    string TourLine3,
    string Comment,
    string TripName,
    string TripLine2,
    string DepartureText);

public static class OmsiTimetableLineTableEditor
{
    public static IReadOnlyList<
        OmsiTimetableLineTableRow>
        Flatten(
            OmsiTimetableLine line)
    {
        ArgumentNullException.ThrowIfNull(
            line);

        return line.Tours
            .SelectMany(
                tour =>
                    tour.Trips.Select(
                        trip =>
                            new OmsiTimetableLineTableRow(
                                tour.Name,
                                tour.AiGroupName,
                                tour.Line3,
                                trip.Comment,
                                trip.TripName,
                                trip.Line2,
                                OmsiTimetableDepartureTime
                                    .FormatEditorValue(
                                        trip.DepartureTime))))
            .ToArray();
    }

    public static IReadOnlyList<
        OmsiTimetableTour>
        BuildTours(
            IEnumerable<
                OmsiTimetableLineTableRow>
                source,
            IReadOnlyCollection<string>?
                knownTripNames =
                    null)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        var rows =
            source.ToArray();

        if (rows.Length == 0)
        {
            throw new InvalidDataException(
                "timetableLineRowsMissing");
        }

        HashSet<string>?
            knownTrips =
                knownTripNames is null
                    ? null
                    : knownTripNames
                        .ToHashSet(
                            StringComparer
                                .OrdinalIgnoreCase);

        var tourOrder =
            new List<string>();

        var tourData =
            new Dictionary<
                string,
                (
                    string AiGroup,
                    string Line3,
                    List<
                        OmsiTimetableAddTrip>
                        Trips
                )>(
                    StringComparer
                        .OrdinalIgnoreCase);

        for (
            var rowIndex = 0;
            rowIndex <
                rows.Length;
            rowIndex++)
        {
            var row =
                rows[
                    rowIndex];

            var tourName =
                NormalizeRequired(
                    row.TourName,
                    "timetableTourNameMissing");

            var tripName =
                NormalizeRequired(
                    row.TripName,
                    "timetableAddTripNameMissing");

            if (
                knownTrips is not null &&
                !knownTrips.Contains(
                    tripName))
            {
                throw new InvalidDataException(
                    $"timetableUnknownTrip:{rowIndex + 1}:{tripName}");
            }

            if (
                !OmsiTimetableDepartureTime
                    .TryParseEditorValue(
                        row.DepartureText,
                        out var departure))
            {
                throw new InvalidDataException(
                    $"timetableDepartureInvalid:{rowIndex + 1}");
            }

            var aiGroup =
                row.AiGroupName
                    ?.Trim() ??
                string.Empty;

            var line3 =
                row.TourLine3
                    ?.Trim() ??
                string.Empty;

            if (
                !tourData.TryGetValue(
                    tourName,
                    out var current))
            {
                current =
                    (
                        aiGroup,
                        line3,
                        []
                    );

                tourData[
                    tourName] =
                    current;

                tourOrder.Add(
                    tourName);
            }
            else if (
                !string.Equals(
                    current.AiGroup,
                    aiGroup,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    current.Line3,
                    line3,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"timetableTourMetadataConflict:{rowIndex + 1}:{tourName}");
            }

            current.Trips.Add(
                new OmsiTimetableAddTrip(
                    row.Comment
                        ?.Trim() ??
                    string.Empty,
                    tripName,
                    row.TripLine2
                        ?.Trim() ??
                    string.Empty,
                    OmsiTimetableDepartureTime
                        .FormatOmsiSeconds(
                            departure)));
        }

        return tourOrder
            .Select(
                name =>
                {
                    var data =
                        tourData[
                            name];

                    return new OmsiTimetableTour(
                        name,
                        data.AiGroup,
                        data.Line3,
                        data.Trips
                            .ToArray());
                })
            .ToArray();
    }

    private static string NormalizeRequired(
        string? value,
        string error)
    {
        var normalized =
            value?.Trim() ??
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                normalized) ||
            normalized.Contains(
                '\r') ||
            normalized.Contains(
                '\n'))
        {
            throw new InvalidDataException(
                error);
        }

        return normalized;
    }
}
