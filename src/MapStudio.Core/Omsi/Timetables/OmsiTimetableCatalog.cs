namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableCatalog(
    IReadOnlyList<OmsiTimetableTrack> Tracks,
    IReadOnlyList<OmsiTimetableTrip> Trips)
{
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
        static string Normalize(
            string value) =>
            Path.GetFileNameWithoutExtension(
                value.Trim());

        return string.Equals(
            Normalize(trackName),
            Normalize(tripTrackName),
            StringComparison.OrdinalIgnoreCase);
    }
}
