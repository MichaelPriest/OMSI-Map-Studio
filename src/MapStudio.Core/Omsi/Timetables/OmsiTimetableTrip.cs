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
    IReadOnlyList<string> ProfileLines);
