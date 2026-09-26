namespace MapStudio.Core.Omsi.Timetables;

public enum OmsiTimetableType2TripValidationFailure
{
    None = 0,
    InvalidStationSequence = 1,
    UnknownStop = 2,
    MissingStationLink = 3
}

public sealed record OmsiTimetableType2TripValidationResult(
    bool IsValid,
    OmsiTimetableType2TripValidationFailure Failure,
    int? StopId = null,
    int? StartStopId = null,
    int? EndStopId = null)
{
    public static OmsiTimetableType2TripValidationResult
        Success() =>
        new(
            true,
            OmsiTimetableType2TripValidationFailure.None);
}

public static class OmsiTimetableTripValidator
{
    public static OmsiTimetableType2TripValidationResult
        ValidateType2StationLinks(
            OmsiTimetableCatalog catalog,
            IReadOnlyList<
                OmsiTimetableTripStation>
                stations)
    {
        ArgumentNullException.ThrowIfNull(
            catalog);

        ArgumentNullException.ThrowIfNull(
            stations);

        var type2 =
            stations
                .OfType<
                    OmsiTimetableTripStationType2>()
                .ToArray();

        if (
            type2.Length !=
                stations.Count ||
            type2.Length <
                2)
        {
            return new(
                false,
                OmsiTimetableType2TripValidationFailure
                    .InvalidStationSequence);
        }

        var knownStops =
            catalog.BusStops
                .Select(
                    stop =>
                        stop.Id)
                .ToHashSet();

        var missingStop =
            type2
                .Select(
                    station =>
                        station.Id)
                .FirstOrDefault(
                    id =>
                        !knownStops.Contains(
                            id),
                    -1);

        if (missingStop >= 0)
        {
            return new(
                false,
                OmsiTimetableType2TripValidationFailure
                    .UnknownStop,
                StopId:
                    missingStop);
        }

        for (
            var index = 0;
            index <
                type2.Length - 1;
            index++)
        {
            var start =
                type2[index].Id;

            var end =
                type2[index + 1].Id;

            if (
                !catalog
                    .StationLinks
                    .Any(
                        link =>
                            link.StartBusStopId ==
                                start &&
                            link.EndBusStopId ==
                                end))
            {
                return new(
                    false,
                    OmsiTimetableType2TripValidationFailure
                        .MissingStationLink,
                    StartStopId:
                        start,
                    EndStopId:
                        end);
            }
        }

        return
            OmsiTimetableType2TripValidationResult
                .Success();
    }
}
