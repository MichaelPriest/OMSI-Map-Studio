namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiStationLink(
    string Comment,
    string Line1,
    int StartBusStopId,
    int EndBusStopId,
    string Line4,
    string Line5,
    string Line6,
    string Line7,
    string Line8,
    string Line9,
    IReadOnlyList<OmsiStationLinkEntry> Entries);
