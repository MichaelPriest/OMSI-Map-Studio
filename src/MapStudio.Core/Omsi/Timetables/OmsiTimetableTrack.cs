namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableTrack(
    string FilePath,
    string RelativePath,
    string Name,
    string Comment1,
    string Comment2,
    IReadOnlyList<OmsiTimetableTrackEntry> Entries);
