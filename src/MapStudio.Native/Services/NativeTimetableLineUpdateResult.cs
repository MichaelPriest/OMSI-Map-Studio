using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Native.Services;

public sealed record NativeTimetableLineUpdateResult(
    OmsiTimetableLine Line,
    string BackupPath);
