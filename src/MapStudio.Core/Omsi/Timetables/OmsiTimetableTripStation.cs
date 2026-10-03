namespace MapStudio.Core.Omsi.Timetables;

public abstract record OmsiTimetableTripStation(
    int Id);

public sealed record OmsiTimetableTripStationType2(
    int Id)
    : OmsiTimetableTripStation(Id);

public sealed record OmsiTimetableTripStationType1(
    int Id,
    string Interval,
    string Name,
    int TileIndex,
    string Line5,
    string Line6,
    string Line7,
    string Line8)
    : OmsiTimetableTripStation(Id);
