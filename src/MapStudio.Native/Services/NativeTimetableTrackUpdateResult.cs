using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Native.Services;

public sealed record NativeTimetableTrackUpdateResult(
    OmsiTimetableTrack Track,
    string BackupPath);
