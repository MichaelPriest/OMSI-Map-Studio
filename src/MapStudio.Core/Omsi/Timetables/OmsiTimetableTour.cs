namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableTour(
    string Name,
    string AiGroupName,
    string Line3,
    IReadOnlyList<OmsiTimetableAddTrip> Trips);
