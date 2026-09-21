using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Native.Services;

public sealed record NativeStationLinkUpdateResult(
    IReadOnlyList<OmsiStationLink> Links,
    int UpdatedIndex,
    string BackupPath);
