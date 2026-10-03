namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableBusStop(
    string Name,
    int TileIndex,
    int Id,
    double? ExitingPassengers,
    string Line4,
    string Line5,
    string SubName);
