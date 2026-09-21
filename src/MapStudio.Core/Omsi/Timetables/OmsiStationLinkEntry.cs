namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiStationLinkEntry(
    string Comment,
    int Id,
    string Line2,
    int TileIndex,
    double? Length,
    string Line5,
    string Line6,
    string Line7,
    IReadOnlyList<string> ChronoFiles);
