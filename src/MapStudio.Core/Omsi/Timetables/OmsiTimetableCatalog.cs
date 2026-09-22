namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableCatalog(
    IReadOnlyList<OmsiTimetableTrack> Tracks,
    IReadOnlyList<OmsiTimetableTrip> Trips)
{
    public IReadOnlyList<OmsiTimetableBusStop>
        BusStops { get; init; } =
            Array.Empty<OmsiTimetableBusStop>();

    public IReadOnlyList<OmsiStationLink>
        StationLinks { get; init; } =
            Array.Empty<OmsiStationLink>();

    public IReadOnlyList<OmsiTimetableLine>
        Lines { get; init; } =
            Array.Empty<OmsiTimetableLine>();

    public int BrokenLineTripReferenceCount =>
        Lines
            .SelectMany(
                line =>
                    line.Tours)
            .SelectMany(
                tour =>
                    tour.Trips)
            .Count(
                addTrip =>
                    !Trips.Any(
                        trip =>
                            string.Equals(
                                NormalizeName(
                                    trip.Name),
                                NormalizeName(
                                    addTrip.TripName),
                                StringComparison.OrdinalIgnoreCase)));

    public int BrokenStationLinkStopReferenceCount =>
        StationLinks.Count(
            link =>
                !BusStops.Any(
                    stop =>
                        stop.Id ==
                        link.StartBusStopId) ||
                !BusStops.Any(
                    stop =>
                        stop.Id ==
                        link.EndBusStopId));

    public int BrokenTripTrackReferenceCount =>
        Trips.Count(
            trip =>
                !trip.UsesStationLinks &&
                !string.IsNullOrWhiteSpace(
                    trip.TrackName) &&
                !Tracks.Any(
                    track =>
                        KeysMatch(
                            track.Name,
                            trip.TrackName)));

    public int BrokenTripStationLinkReferenceCount =>
        Trips
            .Where(
                trip =>
                    trip.UsesStationLinks)
            .Sum(
                trip =>
                trip.Stations
                    .OfType<
                        OmsiTimetableTripStationType2>()
                    .Select(
                        station =>
                            station.Id)
                    .Zip(
                        trip.Stations
                            .OfType<
                                OmsiTimetableTripStationType2>()
                            .Select(
                                station =>
                                    station.Id)
                            .Skip(1),
                        (start, end) =>
                            (
                                Start: start,
                                End: end
                            ))
                    .Count(
                        pair =>
                            !StationLinks.Any(
                                link =>
                                    link.StartBusStopId ==
                                        pair.Start &&
                                    link.EndBusStopId ==
                                        pair.End)));

    private static bool KeysMatch(
        string trackName,
        string tripTrackName)
    {
        return string.Equals(
            NormalizeName(trackName),
            NormalizeName(tripTrackName),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(
        string value) =>
        Path.GetFileNameWithoutExtension(
            value.Trim());
}
