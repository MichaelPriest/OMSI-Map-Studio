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
                !Tracks.Any(
                    track =>
                        KeysMatch(
                            track.Name,
                            trip.TrackName)));

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
