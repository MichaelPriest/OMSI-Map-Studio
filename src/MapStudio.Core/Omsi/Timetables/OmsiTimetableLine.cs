namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableLine(
    string FilePath,
    string RelativePath,
    string Name,
    string Comment1,
    string Comment2,
    bool UserAllowed,
    string Priority,
    IReadOnlyList<OmsiTimetableTour> Tours);
