namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableTrip(
    string FilePath,
    string RelativePath,
    string Name,
    string Comment1,
    string Comment2,
    string TrackName,
    string Destination,
    string Line,
    bool TrainReverse,
    IReadOnlyList<OmsiTimetableTripStation> Stations,
    IReadOnlyList<string> ProfileLines)
{
    public bool UsesStationLinks =>
        Stations.Count >= 2 &&
        Stations.All(
            station =>
                station is
                    OmsiTimetableTripStationType2);

    public string EffectiveTrackName =>
        UsesStationLinks
            ? string.Empty
            : TrackName;

    public string EffectiveDestination =>
        UsesStationLinks
            ? TrackName
            : Destination;

    public string EffectiveLine =>
        UsesStationLinks
            ? Destination
            : Line;
}
