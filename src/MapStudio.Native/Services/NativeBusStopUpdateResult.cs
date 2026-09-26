using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Native.Services;

public sealed record NativeBusStopUpdateResult(
    IReadOnlyList<OmsiTimetableBusStop> Stops,
    int UpdatedIndex,
    string BackupPath);
