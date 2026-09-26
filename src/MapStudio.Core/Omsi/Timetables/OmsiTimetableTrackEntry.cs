namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableTrackEntry(
    string Comment,
    int Id,
    string Line2,
    int TileIndex,
    string Line4,
    double? Length,
    string Line6,
    string? Line7);
