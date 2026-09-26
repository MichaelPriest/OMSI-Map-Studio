using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Native.Services;

public sealed record NativeTimetableTripUpdateResult(
    OmsiTimetableTrip Trip,
    string BackupPath);
